// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using System.Security;
using System.IO.Compression;
using System.Collections.Generic;

using Mono.Cecil;

public enum ApiLevel { None, Private, Internal, Public }

internal class InheritDocProcessor
{
	private static class DocElementNames
	{
		public static readonly XName Doc = XName.Get("doc");
		public static readonly XName InheritDoc = XName.Get("inheritdoc");
		public static readonly XName Member = XName.Get("member");
		public static readonly XName Members = XName.Get("members");
		public static readonly XName Param = XName.Get("param");
		public static readonly XName TypeParam = XName.Get("typeparam");
		public static readonly XName Overloads = XName.Get("overloads");
		public static readonly XName Redirect = XName.Get("redirect");
		public static readonly XName Returns = XName.Get("returns");
		public static readonly XName Value = XName.Get("value");
	}

	private static class DocAttributeNames
	{
		public static readonly XName _visited = XName.Get(nameof(_visited));
		public static readonly XName _trimmed = XName.Get(nameof(_trimmed));
		public static readonly XName _gencode = XName.Get(nameof(_gencode));
		public static readonly XName Cref = XName.Get("cref");
		public static readonly XName Name = XName.Get("name");
		public static readonly XName Path = XName.Get("path");
	}

	private static readonly XName[] inheritSkipIfExists = [
		XName.Get("example"),
		XName.Get("exclude"),
		XName.Get("filterpriority"),
		XName.Get("preliminary"),
		XName.Get("summary"),
		XName.Get("remarks"),
		XName.Get("returns"),
		XName.Get("threadsafety"),
		XName.Get("value")
	 ];

	private static readonly XName[] inheritSkipIfMatch = [
		XName.Get("cref"),
		XName.Get("href"),
		XName.Get("name"),
		XName.Get("vref"),
		XName.Get("xref")
	];

	private static readonly string refFolderToken = Path.DirectorySeparatorChar + "ref" + Path.DirectorySeparatorChar;
	private static readonly string libFolderToken = Path.DirectorySeparatorChar + "lib" + Path.DirectorySeparatorChar;

	public static (int replaced, int total, int trimmed) InheritDocs(string asmPath, string docPath, string outPath, string[] refPaths, string[] addPaths, ApiLevel trimLevel, ILogger logger)
	{
		static bool isInheritDocCandidate(XElement m) =>
			!string.IsNullOrEmpty((string)m.Attribute(DocAttributeNames.Name)) && !m.HasAttribute(DocAttributeNames._visited) && m.Descendants(DocElementNames.InheritDoc).Any();

		static XDocument loadDoc(string path)
		{
			using var stmdoc = File.Open(path, FileMode.Open);
			return XDocument.Load(stmdoc, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
		}

		var doc = loadDoc(docPath);
		var docMembers = doc.Root.Element(DocElementNames.Members);
		int beforeCount = docMembers.Descendants(DocElementNames.InheritDoc).Count();

		if (beforeCount == 0 && trimLevel == ApiLevel.None)
			return (0, 0, 0);

		using var resolver = CecilExtensions.RefAssemblyResolver.Create(asmPath, refPaths);
		using var asm = AssemblyDefinition.ReadAssembly(asmPath, new ReaderParameters { AssemblyResolver = resolver });

		var types =
			asm.Modules
			.SelectMany(m => m.Types)
			.SelectManyRecursive(t => t.NestedTypes)
			.ToList()
		;

		var docMap = generateDocMap(types, docMembers, trimLevel, logger);
		var asmTypes = types.Select(t => t.GetDocID()).ToHashSet();

		var refCref = docMap.Values.SelectMany(v => v.Select(l => l.Cref)).Where(c => !asmTypes.Contains(getTypeIDFromDocID(c))).ToHashSet();
		var refDocs = getRefDocs(refPaths, addPaths, refCref, logger);
		logger.Write(ILogger.Severity.Diag, "External ref docs found: " + refDocs.Root.Elements(DocElementNames.Member).Count().ToString());

		beforeCount = docMembers.Descendants(DocElementNames.InheritDoc).Count(dm => !dm.Ancestors(DocElementNames.Member).Any(m => m.HasAttribute(DocAttributeNames._trimmed) || m.HasAttribute(DocAttributeNames._gencode)));

		var mem = default(XElement);
		while ((mem = docMembers.Elements(DocElementNames.Member).FirstOrDefault(isInheritDocCandidate)) is not null)
			replaceInheritDoc(docPath, mem, docMap, docMembers, refDocs, logger);

		var totrim = docMembers.Elements(DocElementNames.Member).Where(m => m.HasAttribute(DocAttributeNames._trimmed) || (m.HasAttribute(DocAttributeNames._gencode) && !m.Elements().Any(e => e.Name != DocElementNames.InheritDoc))).ToList();
		int trimCount = totrim.Count;

		foreach (var md in totrim)
		{
			string reason = md.HasAttribute(DocAttributeNames._gencode) && !md.Elements().Any(e => e.Name != DocElementNames.InheritDoc) ? "generated" : trimLevel == ApiLevel.Private ? "private" : "non-public";
			logger.Write(ILogger.Severity.Diag, "Trimming " + reason + " doc: " + (string)md.Attribute(DocAttributeNames.Name));

			if (md.PreviousNode.IsWhiteSpace())
				md.PreviousNode.Remove();

			md.Remove();
		}

		int afterCount = docMembers.Descendants(DocElementNames.InheritDoc).Count(dm => !dm.Ancestors(DocElementNames.Member).Any(m => m.HasAttribute(DocAttributeNames._gencode)));

		foreach (var md in docMembers.Elements(DocElementNames.Member).Where(m => m.HasAttribute(DocAttributeNames._visited) || m.HasAttribute(DocAttributeNames._gencode)))
		{
			md.SetAttributeValue(DocAttributeNames._visited, null);
			md.SetAttributeValue(DocAttributeNames._gencode, null);
		}

		using var writer = XmlWriter.Create(outPath, new XmlWriterSettings { Encoding = new UTF8Encoding(false), IndentChars = "    " });
		doc.Save(writer);

		return (beforeCount - afterCount, beforeCount, trimCount);
	}

	private static IDictionary<string, IEnumerable<DocMatch>> generateDocMap(IList<TypeDefinition> types, XElement docMembers, ApiLevel trimLevel, ILogger logger)
	{
		var docMap = new Dictionary<string, IEnumerable<DocMatch>>();

		foreach (var t in types)
		{
			string typeID = t.GetDocID();
			var memDocs = findDocsByID(docMembers, typeID);

			if (t.IsGeneratedCode())
			{
				// Several tools include this hack to output namespace documentation
				// https://stackoverflow.com/questions/793210/xml-documentation-for-a-namespace
				if (t.IsNamespaceDocPlaceholder())
				{
					foreach (var md in memDocs)
						md.SetAttributeValue(DocAttributeNames.Name, "N:" + t.Namespace);

					continue;
				}

				foreach (var md in memDocs)
					md.SetAttributeValue(DocAttributeNames._gencode, true);
			}

			if (t.GetApiLevel() <= trimLevel)
			{
				foreach (var md in memDocs)
					md.SetAttributeValue(DocAttributeNames._trimmed, true);
			}

			if (memDocs.Descendants(DocElementNames.InheritDoc).Any())
			{
				logger.Write(ILogger.Severity.Diag, "Processing DocID: " + typeID);

				var crefs = memDocs.Descendants(DocElementNames.InheritDoc).Select(i => (string)i.Attribute(DocAttributeNames.Cref)).Where(c => !string.IsNullOrWhiteSpace(c)).ToHashSet();
				var dml = new List<DocMatch>();
				docMap.Add(typeID, dml);

				foreach (var bt in t.GetBaseCandidates())
				{
					string cref = bt.Resolve().GetDocID();
					if (dml.Count == 0 || crefs.Contains(cref))
						dml.Add(new DocMatch(cref, t, bt));

					if (crefs.Count == 0)
						break;
				}

				foreach (var cref in crefs.Where(c => !dml.Any(dm => dm.Cref == c)))
					dml.Add(new DocMatch(cref, t));
			}

			foreach (var (m, idx, memID) in t.Methods.SelectMany(m => m.GetDocID().Select((d, i) => (m, i, d))))
			{
				if (docMap.ContainsKey(memID))
					continue;

				var om = m.Overrides.Select(o => o.Resolve()).FirstOrDefault(o => o.DeclaringType.IsInterface);

				// If no docs for public explicit interface implementation, inject them
				// including the whitespace they would have had if they had been there.
				if (idx == 0 && (om?.DeclaringType.GetApiLevel() ?? ApiLevel.None) > trimLevel && t.GetApiLevel() > trimLevel && !findDocsByID(docMembers, memID).Any())
				{
					if (docMembers.LastNode.IsWhiteSpace())
						docMembers.LastNode.Remove();

					docMembers.Add(
						new XText("\n        "),
							new XElement(DocElementNames.Member,
								new XAttribute(DocAttributeNames.Name, memID),
								new XAttribute(DocAttributeNames._gencode, true),
								new XText("\n            "), new XElement(DocElementNames.InheritDoc),
							new XText("\n        ")),
						new XText("\n    ")
					);
				}

				var methDocs = findDocsByID(docMembers, memID);
				if ((om?.DeclaringType.GetApiLevel() ?? m.GetApiLevel()) <= trimLevel)
				{
					foreach (var md in methDocs)
						md.SetAttributeValue(DocAttributeNames._trimmed, true);
				}

				if (m.IsGeneratedCode())
				{
					foreach (var md in methDocs)
						md.SetAttributeValue(DocAttributeNames._gencode, true);
				}

				if (methDocs.Descendants(DocElementNames.InheritDoc).Any())
				{
					logger.Write(ILogger.Severity.Diag, "Processing DocID: " + memID);

					var crefs = methDocs.Descendants(DocElementNames.InheritDoc).Select(i => (string)i.Attribute(DocAttributeNames.Cref)).Where(c => !string.IsNullOrWhiteSpace(c)).ToHashSet();
					var dml = new List<DocMatch>();

					var bases = (om is not null ? (new[] { om }) : [ ]).Concat(m.GetBaseCandidates());
					foreach (var (bm, cref) in bases.SelectMany(bm => bm.GetDocID().Select(d => (bm, d))))
					{
						if (dml.Count == 0 || crefs.Contains(cref))
							dml.Add(new DocMatch(cref, m, bm));

						if (crefs.Count == 0)
							break;
					}

					foreach (var cref in crefs.Where(c => !dml.Any(dm => dm.Cref == c)))
						dml.Add(new DocMatch(cref, m));

					if (dml.Count > 0)
						docMap.Add(memID, dml);
					else
						logger.Write(ILogger.Severity.Info, "No inherit candidate for: " + memID);
				}
			}

			foreach (var fd in t.Fields.Where(f => f.GetApiLevel() <= trimLevel).SelectMany(f => f.GetDocID().SelectMany(d => findDocsByID(docMembers, d))))
				fd.SetAttributeValue(DocAttributeNames._trimmed, true);
		}

		return docMap;
	}

	private static void replaceInheritDoc(string file, XElement mem, IDictionary<string, IEnumerable<DocMatch>> docMap, XElement members, XDocument refDocs, ILogger logger)
	{
		if (mem.HasAttribute(DocAttributeNames._visited))
			return;

		mem.SetAttributeValue(DocAttributeNames._visited, true);
		string memID = mem.Attribute(DocAttributeNames.Name).Value;
		docMap.TryGetValue(memID, out var dml);

		foreach (var inh in mem.Descendants(DocElementNames.InheritDoc).ToArray())
		{
			string? cref = (string)inh.Attribute(DocAttributeNames.Cref) ?? dml?.FirstOrDefault()?.Cref;
			if (string.IsNullOrEmpty(cref))
			{
				if (!mem.HasAttribute(DocAttributeNames._trimmed) && !mem.HasAttribute(DocAttributeNames._gencode))
					logger.Warn(ErrorCodes.NoBase, file, mem.SourceLine(), mem.SourceColumn(), "Cref not present and no base could be found for: " + memID);

				continue;
			}

			var dm = dml?.FirstOrDefault(d => d.Cref == cref) ?? new DocMatch(cref!);

			var doc = findDocsByID(refDocs.Root, cref!).FirstOrDefault();
			if (doc is not null)
			{
				inheritDocs(file, memID, inh, doc, dm, logger);
				continue;
			}

			doc = findDocsByID(members, cref!).FirstOrDefault();
			if (doc is null)
			{
				if (!mem.HasAttribute(DocAttributeNames._trimmed) && !mem.HasAttribute(DocAttributeNames._gencode))
					logger.Warn(ErrorCodes.NoDocs, file, mem.SourceLine(), mem.SourceColumn(), "No matching documentation could be found for: " + memID + ", which attempts to inherit from: " + cref);

				continue;
			}

			if (doc.Descendants(DocElementNames.InheritDoc).Any())
				replaceInheritDoc(file, doc, docMap, members, refDocs, logger);

			inheritDocs(file, memID, inh, doc, dm, logger);
		}
	}

	private static void inheritDocs(string file, string memID, XElement inh, XElement doc, DocMatch dm, ILogger logger)
	{
		static void removeDoc(List<XNode> nodes, int pos)
		{
			if (nodes.Count > pos + 1 && nodes[pos + 1].IsWhiteSpace())
				nodes.RemoveAt(pos + 1);

			nodes.RemoveAt(pos);
		}

		logger.Write(ILogger.Severity.Diag, "Inheriting docs from: " + dm.Cref);

		string xpath = (string)inh.Attribute(DocAttributeNames.Path) ?? "node()";
		var docBase = new XElement(doc);

		if (!xpath.StartsWith("/") && inh.Parent.Name != DocElementNames.Member)
		{
			string startPath = inh.Parent.Name.LocalName;
			for (var up = inh.Parent.Parent; up.Name != DocElementNames.Member; up = up.Parent)
				startPath = up.Name + "/" + startPath;

			string? parName = (string)inh.Parent.Attribute(DocAttributeNames.Name);
			if (!string.IsNullOrEmpty(parName))
				startPath += "[@" + DocAttributeNames.Name + "='" + SecurityElement.Escape(parName) + "']";

			docBase = docBase.XPathSelectElement(startPath);
		}

		var nodes = (docBase?.XPathSelectNodes(xpath) ?? XElement.EmptySequence).ToList();
		for (int i = nodes.Count - 1; i >= 0; --i)
		{
			if (nodes[i] is not XElement elem)
				continue;

			var ename = elem.Name;

			if (ename == DocElementNames.Returns && !dm.HasReturn || ename == DocElementNames.Value && !dm.HasValue)
			{
				removeDoc(nodes, i);
				continue;
			}

			if (ename == DocElementNames.Param || ename == DocElementNames.TypeParam)
			{
				string? pname = (string)elem.Attribute(DocAttributeNames.Name);
				var pmap = ename == DocElementNames.Param ? dm.ParamMap : dm.TypeParamMap;

				if (!pmap.ContainsKey(pname))
				{
					removeDoc(nodes, i);
					continue;
				}

				elem.SetAttributeValue(DocAttributeNames.Name, pmap[pname]);
				foreach (var pref in nodes.OfType<XElement>().DescendantNodesAndSelf().OfType<XElement>().Where(e => e.Name == (ename.LocalName + "ref") && (string)e.Attribute(DocAttributeNames.Name) == pname))
					pref.SetAttributeValue(DocAttributeNames.Name, pmap[pname]);
			}

			// Doc inheritance rules built for compatibility with SHFB modulo the name change of the "select" attribute to "path"
			// https://ewsoftware.github.io/XMLCommentsGuide/html/86453FFB-B978-4A2A-9EB5-70E118CA8073.htm#TopLevelRules
			var matchAttributes = elem.Attributes().Where(a => !inheritSkipIfExists.Contains(ename) && inheritSkipIfMatch.Contains(a.Name));

			string mpath = ename.LocalName;
			if (matchAttributes.Any())
				mpath += "[" + string.Join(" and ", matchAttributes.Select(a => "@" + a.Name + "='" + SecurityElement.Escape(a.Value) + "'")) + "]";

			var pmatch = inh.Parent.XPathSelectElement(mpath);
			if (ename == DocElementNames.Overloads || (pmatch is not null && (inheritSkipIfExists.Contains(ename) || matchAttributes.Any())))
				removeDoc(nodes, i);
		}

		if (nodes.Count > 0 && nodes[nodes.Count - 1].IsWhiteSpace())
			nodes.RemoveAt(nodes.Count - 1);
		if (nodes.Count > 0 && nodes[0].IsWhiteSpace())
			nodes.RemoveAt(0);

		if (nodes.Count == 0 && !inh.Ancestors(DocElementNames.Member).Any(m => m.HasAttribute(DocAttributeNames._trimmed) || m.HasAttribute(DocAttributeNames._gencode)))
			logger.Warn(ErrorCodes.NoNode, file, inh.SourceLine(), inh.SourceColumn(), "No matching non-duplicate nodes found for: " + memID + ", which attempts to inherit from: " + dm.Cref + " path=\"" + xpath + "\"");
		else
			inh.ReplaceWith(nodes);
	}

	static XDocument getRefDocs(IReadOnlyCollection<string> refAssemblies, IReadOnlyCollection<string> refDocs, IReadOnlyCollection<string> refCref, ILogger logger)
	{
		var doc = new XDocument(new XElement(DocElementNames.Doc));
		if (refCref.Count == 0)
			return doc;

		var docPaths = refAssemblies
			.Select(path => Path.ChangeExtension(path, ".xml"))
			.Concat(refAssemblies.Select(p => Path.GetDirectoryName(p)).Distinct().Select(p => Path.Combine(p!, "namespaces.xml"))
			.Concat(refDocs));

		foreach (string docPath in docPaths)
		{
			string path = Path.GetFullPath(docPath);
			logger.Write(ILogger.Severity.Diag, "Trying ref doc path: " + path);

			if (!File.Exists(path))
			{
				// Some NuGet packages have XML docs in the "lib" folder but not in the "ref" folder or vice-versa.
				if (path.Contains(refFolderToken) && File.Exists(path.Replace(refFolderToken, libFolderToken)))
					path = path.Replace(refFolderToken, libFolderToken);
				else if (path.Contains(libFolderToken) && File.Exists(path.Replace(libFolderToken, refFolderToken)))
					path = path.Replace(libFolderToken, refFolderToken);
				else
					continue;

				logger.Write(ILogger.Severity.Info, "Using alt ref doc path: " + docPath + " -> " + path);
			}

			using var xrd = getDocReader(path, logger);
			if (xrd is null)
				continue;

			try
			{
				while (!xrd.EOF)
				{
					if (xrd.NodeType != XmlNodeType.Element || xrd.Name != DocElementNames.Member)
					{
						xrd.Read();
						continue;
					}

					if (refCref.Contains(xrd.GetAttribute(DocAttributeNames.Name.LocalName)))
						doc.Root.Add(XNode.ReadFrom(xrd));
					else
						xrd.Skip();
				}
			}
			catch (XmlException ex)
			{
				// https://github.com/dotnet/standard/issues/1527
				if (!Path.GetFileName(path).Equals("netstandard.xml", StringComparison.OrdinalIgnoreCase))
					logger.Warn(ErrorCodes.BadXml, path, ex.LineNumber, ex.LinePosition, "XML parse error in referenced doc: " + ex.Message);
			}
		}

		return doc;
	}

	private static XmlReader? getDocReader(string docPath, ILogger logger)
	{
		var rdr = default(StreamReader);
		var xrd = default(XmlReader);

		try
		{
			if (Path.GetExtension(docPath).Equals(".gz", StringComparison.OrdinalIgnoreCase))
				rdr = new StreamReader(new GZipStream(File.OpenRead(docPath), CompressionMode.Decompress));
			else
				rdr = new StreamReader(docPath);

			xrd = XmlReader.Create(rdr, new XmlReaderSettings { IgnoreWhitespace = true, CloseInput = true });
			if (!xrd.ReadToFollowing(DocElementNames.Doc.LocalName))
			{
				logger.Warn(ErrorCodes.BadXml, docPath, 0, 0, "Not a doc file");
				xrd.Dispose();

				return null;
			}

			// Some VS-installed .NET Framework targeting packs have placeholder XML docs that redirect to a shared location.
			string redir = xrd.GetAttribute(DocElementNames.Redirect.LocalName);
			if (!string.IsNullOrEmpty(redir))
			{
				string newDocPath = Util.ReplacePathSpecialFolder(redir);
				logger.Write(ILogger.Severity.Info, "Doc file redirected: " + docPath + " -> " + newDocPath);
				xrd.Dispose();

				return File.Exists(newDocPath) ? getDocReader(newDocPath, logger) : null;
			}

			return xrd;
		}
		catch (Exception ex)
		{
			logger.Warn(ErrorCodes.BadXml, docPath, 0, 0, "Error loading doc file, skipping: " + ex.ToString());
			xrd?.Dispose();
			rdr?.Dispose();

			return null;
		}
	}

	private static string getTypeIDFromDocID(string docID)
	{
		if (docID[0] == 'T')
			return docID;

		int paren = docID.IndexOf('(');
		int lastDot = docID.LastIndexOf('.', paren == -1 ? docID.Length - 1 : paren);
		return "T" + docID.Substring(1, lastDot - 1);
	}

	private static IEnumerable<XElement> findDocsByID(XElement container, string docID) => container.Elements(DocElementNames.Member).Where(m => (string)m.Attribute(DocAttributeNames.Name) == docID);

	private class DocMatch(string cref)
	{
		private static readonly IReadOnlyDictionary<string, string> emptyMap = new Dictionary<string, string>();

		public string Cref = cref;
		public IReadOnlyDictionary<string, string> TypeParamMap = emptyMap;
		public IReadOnlyDictionary<string, string> ParamMap = emptyMap;
		public bool HasReturn = false;
		public bool HasValue = false;

		public DocMatch(string cref, TypeReference t, TypeReference? bt = null) : this(cref)
		{
			if (t.HasGenericParameters && (bt?.IsGenericInstance ?? true))
			{
				var tpm = new Dictionary<string, string>();

				if (bt is not null)
				{
					var ga = ((GenericInstanceType)bt).GenericArguments;
					var rbt = bt.Resolve();

					foreach (var tp in t.GenericParameters.Where(ga.Contains))
						tpm.Add(rbt.GenericParameters[ga.IndexOf(tp)].Name, tp.Name);
				}
				else
				{
					foreach (var tp in t.GenericParameters)
						tpm.Add(tp.Name, tp.Name);
				}

				TypeParamMap = tpm;
			}
		}

		public DocMatch(string cref, MethodDefinition m, MethodDefinition? bm = null) : this(cref)
		{
			if (m.HasParameters)
			{
				var pm = new Dictionary<string, string>();
				foreach (var pp in m.Parameters)
					pm.Add(bm?.Parameters[pp.Index].Name ?? pp.Name, pp.Name);

				ParamMap = pm;
			}

			if (m.HasGenericParameters)
			{
				var tpm = new Dictionary<string, string>();
				foreach (var tp in m.GenericParameters)
					tpm.Add(bm?.GenericParameters[tp.Position].Name ?? tp.Name, tp.Name);

				TypeParamMap = tpm;
			}

			HasReturn = m.HasReturnValue();
			HasValue = m.IsPropertyMethod();
		}
	}
}

internal interface ILogger
{
	public enum Severity { Diag, Info, Message, Warn, Error }

	void Write(Severity severity, string msg);
	void Warn(string code, string file, int line, int column, string msg);
}

internal static class ErrorCodes
{
	public const string BadXml = "IDT001";
	public const string NoDocs = "IDT002";
	public const string NoBase = "IDT003";
	public const string NoNode = "IDT004";
}
