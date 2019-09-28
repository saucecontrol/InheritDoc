using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using System.Security;
using System.Collections.Generic;

using Mono.Cecil;

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
	}

	private static class DocAttributeNames
	{
		public static readonly XName _visited = XName.Get("_visited");
		public static readonly XName Cref = XName.Get("cref");
		public static readonly XName Name = XName.Get("name");
		public static readonly XName Path = XName.Get("path");
	}

	private static readonly XName[] inheritSkipIfExists = new[] {
		XName.Get("example"),
		XName.Get("exclude"),
		XName.Get("filterpriority"),
		XName.Get("preliminary"),
		XName.Get("summary"),
		XName.Get("remarks"),
		XName.Get("returns"),
		XName.Get("threadsafety"),
		XName.Get("value")
	 };

	private static readonly XName[] inheritSkipIfMatch = new[] {
		XName.Get("cref"),
		XName.Get("href"),
		XName.Get("name"),
		XName.Get("vref"),
		XName.Get("xref")
	};

	private static readonly string refFolderToken = Path.DirectorySeparatorChar + "ref" + Path.DirectorySeparatorChar;
	private static readonly string libFolderToken = Path.DirectorySeparatorChar + "lib" + Path.DirectorySeparatorChar;

	public static void InheritDocs(string asmPath, string docPath, string[] refPaths, string[] addPaths, string outPath, ILogger logger)
	{
		var doc = loadDoc(docPath);
		using var resolver = new CecilExtensions.RefAssemblyResolver(refPaths);
		using var asm = AssemblyDefinition.ReadAssembly(asmPath, new ReaderParameters { AssemblyResolver = resolver });

		var types =
			asm.Modules
			.SelectMany(m => m.Types)
			.SelectManyRecursive(t => t.NestedTypes)
			.Where(t => !t.IsCompilerGenerated() || t.IsNamespaceDocPlaceholder())
			.ToList()
		;

		var docMap = new Dictionary<string, DocMatch>();
		var docMembers = doc.Root.Element(DocElementNames.Members);

		foreach (var t in types)
		{
			string typeID = t.GetDocID();
			var memDocs = findDocsByID(docMembers, typeID);

			// Several tools include this hack to output namespace documentation
			// https://stackoverflow.com/questions/793210/xml-documentation-for-a-namespace
			if (t.IsCompilerGenerated() && t.IsNamespaceDocPlaceholder())
			{
				foreach (var md in memDocs)
					md.SetAttributeValue(DocAttributeNames.Name, "N:" + t.Namespace);

				continue;
			}

			foreach (var md in memDocs.Where(m => m.Descendants(DocElementNames.InheritDoc).Any()))
			{
				var bt = t.GetDocBaseCandidates().FirstOrDefault();
				if (bt != null)
				{
					var rbt = bt.Resolve();
					var dm = new DocMatch(rbt.GetDocID());
					docMap.Add(typeID, dm);

					if (t.HasGenericParameters && bt.IsGenericInstance)
					{
						var ga = ((GenericInstanceType)bt).GenericArguments;
						var tpm = new Dictionary<string, string>();
						foreach (var tp in t.GenericParameters.Where(p => ga.Contains(p)))
							tpm.Add(rbt.GenericParameters[ga.IndexOf(tp)].Name, tp.Name);

						dm.TypeParamMap = tpm;
					}
				}
			}

			foreach (var m in t.Methods.Where(m => !m.IsCompilerGenerated() || m.IsEventMethod() || m.IsPropertyMethod()))
			{
				string memID = m.GetDocID();

				var om = m.Overrides.Select(o => o.Resolve()).FirstOrDefault(o => o.DeclaringType.IsInterface);

				// If no docs for public explicit interface implementation, inject them.
				if (t.IsPublic && (om?.DeclaringType.IsPublic ?? false) && !findDocsByID(docMembers, memID).Any())
					docMembers.Add(new XElement(DocElementNames.Member, new XAttribute(DocAttributeNames.Name, memID), new XElement(DocElementNames.InheritDoc)));

				foreach (var md in findDocsByID(docMembers, memID).Where(me => me.Descendants(DocElementNames.InheritDoc).Any() && !docMap.ContainsKey(memID)))
				{
					logger.Write(ILogger.Severity.Diag, "Processing DocID: " + memID);

					om ??= m.GetDocBaseCandidates().FirstOrDefault();
					if (om != null)
					{
						var dm = new DocMatch(om.GetDocID());
						docMap.Add(memID, dm);

						if (m.HasParameters)
						{
							var pm = new Dictionary<string, string>();
							foreach (var pp in m.Parameters)
								pm.Add(om.Parameters[pp.Index].Name, pp.Name);

							dm.ParamMap = pm;
						}

						if (m.HasGenericParameters)
						{
							var tpm = new Dictionary<string, string>();
							foreach (var tp in m.GenericParameters)
								tpm.Add(om.GenericParameters[tp.Position].Name, tp.Name);

							dm.TypeParamMap = tpm;
						}
					}
					else if (md.Descendants(DocElementNames.InheritDoc).FirstOrDefault(i => i.HasAttribute(DocAttributeNames.Cref)) is XElement inh)
					{
						var dm = new DocMatch(inh.Attribute(DocAttributeNames.Cref).Value);
						docMap.Add(memID, dm);

						if (m.HasParameters)
							dm.ParamMap = m.Parameters.ToDictionary(p => p.Name, p => p.Name);

						if (m.HasGenericParameters)
							dm.TypeParamMap = m.GenericParameters.ToDictionary(p => p.Name, p => p.Name);
					}
					else
					{
						logger.Write(ILogger.Severity.Warn, "No inherit candidate for: " + memID);
					}
				}
			}
		}

		var expCref = docMembers.Elements(DocElementNames.Member).SelectMany(m => m.Descendants(DocElementNames.InheritDoc).Select(d => (string)d.Attribute(DocAttributeNames.Cref)).Where(c => !string.IsNullOrWhiteSpace(c)));
		var inhCref = docMap.Values.Select(v => v.Cref);

		var asmTypes = types.Select(t => t.GetDocID()).ToHashSet();
		var refCref = expCref.Concat(inhCref).Where(c => !asmTypes.Contains(getTypeIDFromDocID(c))).ToHashSet();

		int toReplace = docMembers.Elements(DocElementNames.Member).Count(me => me.Descendants(DocElementNames.InheritDoc).Any(i => i.HasAttribute(DocAttributeNames.Cref) || docMap.ContainsKey((string)me.Attribute(DocAttributeNames.Name))));
		logger.Write(ILogger.Severity.Diag, "Member docs to replace: " + toReplace.ToString());
		logger.Write(ILogger.Severity.Diag, "Member docs unresolved: " + (docMembers.Elements(DocElementNames.Member).Count(me => me.Descendants(DocElementNames.InheritDoc).Any()) - toReplace).ToString());

		var refDocs = getRefDocs(refPaths, addPaths, refCref, logger);
		logger.Write(ILogger.Severity.Diag, "External ref docs found: " + refDocs.Root.Elements(DocElementNames.Member).Count().ToString());
		foreach (var cref in refCref.Where(c => !findDocsByID(refDocs.Root, c).Any()))
			logger.Write(ILogger.Severity.Warn, "External ref doc not found: " + cref);

		var mem = default(XElement);
		while ((mem = docMembers.Elements(DocElementNames.Member).FirstOrDefault(m => isInheritDocCandidate(m))) != null)
			replaceInheritDoc(mem, docMap, docMembers, refDocs, logger);

		foreach (var md in docMembers.Elements(DocElementNames.Member).Where(m => m.HasAttribute(DocAttributeNames._visited)))
			md.SetAttributeValue(DocAttributeNames._visited, null);

		using var writer = new XmlTextWriter(outPath, new UTF8Encoding(false)) { Formatting = Formatting.Indented, Indentation = 4 };
		doc.Save(writer);
	}

	private static void replaceInheritDoc(XElement mem, Dictionary<string, DocMatch> docMap, XElement members, XDocument refDocs, ILogger logger)
	{
		if (mem.HasAttribute(DocAttributeNames._visited))
			return;

		mem.SetAttributeValue(DocAttributeNames._visited, true);
		string memID = mem.Attribute(DocAttributeNames.Name).Value;
		docMap.TryGetValue(memID, out var dm);

		foreach (var inh in mem.Descendants(DocElementNames.InheritDoc).ToArray())
		{
			string? cref = (string)inh.Attribute(DocAttributeNames.Cref) ?? dm?.Cref;
			if (string.IsNullOrEmpty(cref))
			{
				logger.Write(ILogger.Severity.Warn, "Cref not present and no base could be found for: " + memID);
				continue;
			}

			dm ??= new DocMatch(cref!);

			var doc = findDocsByID(refDocs.Root, cref!).FirstOrDefault();
			if (doc != null)
			{
				inheritDocs(inh, doc, dm, logger);
				continue;
			}

			doc = findDocsByID(members, cref!).FirstOrDefault();
			if (doc is null)
				continue;

			if (doc.Descendants(DocElementNames.InheritDoc).Any())
				replaceInheritDoc(doc, docMap, members, refDocs, logger);

			inheritDocs(inh, doc, dm, logger);
		}
	}

	private static void inheritDocs(XElement inh, XElement doc, DocMatch dm, ILogger logger)
	{
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
			if (docBase is null)
			{
				logger.Write(ILogger.Severity.Warn, "No matching doc found for: " + startPath);
				return;
			}
		}

		var nodes = docBase.XPathSelectNodes(xpath).ToList();
		for (int i = nodes.Count - 1; i >= 0; --i)
		{
			if (!(nodes[i] is XElement elem))
				continue;

			var ename = elem.Name;

			if (ename == DocElementNames.Param || ename == DocElementNames.TypeParam)
			{
				string? pname = (string)elem.Attribute(DocAttributeNames.Name);
				var pmap = ename == DocElementNames.Param ? dm.ParamMap : dm.TypeParamMap;

				if (!pmap.ContainsKey(pname))
				{
					if (nodes.Count > i + 1 && nodes[i + 1].IsWhiteSpace())
						nodes.RemoveAt(i + 1);

					nodes.RemoveAt(i);
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
			if (ename == DocElementNames.Overloads || (pmatch != null && (inheritSkipIfExists.Contains(ename) || matchAttributes.Any())))
			{
				if (nodes.Count > i + 1 && nodes[i + 1].IsWhiteSpace())
					nodes.RemoveAt(i + 1);

				nodes.RemoveAt(i);
				continue;
			}
		}

		if (nodes.Count > 2)
		{
			if (nodes[0].IsWhiteSpace())
				nodes.RemoveAt(0);
			if (nodes[nodes.Count - 1].IsWhiteSpace())
				nodes.RemoveAt(nodes.Count - 1);
		}

		inh.ReplaceWith(nodes);
	}

	private static bool isInheritDocCandidate(XElement m) => !string.IsNullOrEmpty((string)m.Attribute(DocAttributeNames.Name)) && !m.HasAttribute(DocAttributeNames._visited) && m.Descendants(DocElementNames.InheritDoc).Any();

	static XDocument getRefDocs(string[] refAssemblies, string[] refDocs, IReadOnlyCollection<string> refCref, ILogger logger)
	{
		var doc = new XDocument(new XElement(DocElementNames.Doc));
		if (refCref.Count == 0)
			return doc;

		var docPaths = refAssemblies
			.Select(path => Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path) + ".xml"))
			.Concat(refAssemblies.Select(p => Path.GetDirectoryName(p)).Distinct().Select(p => Path.Combine(p!, "namespaces.xml"))
			.Concat(refDocs));

		foreach (string docPath in docPaths)
		{
			string path = docPath;
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
				logger.Write(ILogger.Severity.Warn, "XML parse error in doc file: " + path + " -- " + ex.Message);
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
			rdr = new StreamReader(docPath);
			xrd = XmlReader.Create(rdr, new XmlReaderSettings { IgnoreWhitespace = true, CloseInput = true });

			if (!xrd.ReadToFollowing(DocElementNames.Doc.LocalName))
			{
				logger.Write(ILogger.Severity.Warn, "Not a doc file: " + docPath);
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
			logger.Write(ILogger.Severity.Warn, "Error loading doc file, skipping: " + ex.ToString());
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

	private static XDocument loadDoc(string path)
	{
		using var stmdoc = File.Open(path, FileMode.Open);
		return XDocument.Load(stmdoc, LoadOptions.PreserveWhitespace);
	}

	private static IEnumerable<XElement> findDocsByID(XElement container, string docID) => container.Elements(DocElementNames.Member).Where(m => (string)m.Attribute(DocAttributeNames.Name) == docID);

	private class DocMatch
	{
		private static readonly IReadOnlyDictionary<string, string> emptyMap = new Dictionary<string, string>();

		public string Cref;
		public IReadOnlyDictionary<string, string> TypeParamMap = emptyMap;
		public IReadOnlyDictionary<string, string> ParamMap = emptyMap;

		public DocMatch(string cref) => Cref = cref;
	}
}

internal interface ILogger
{
	public enum Severity { Diag, Info, Warn, Error }

	void Write(Severity severity, string msg);
}
