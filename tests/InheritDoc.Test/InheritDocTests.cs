#pragma warning disable CS1591 // missing XML docs

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;
using System.Diagnostics;

using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class InheritDocTests
{
#if NET48
	const string corlibPath = @".nuget\packages\microsoft.netframework.referenceassemblies.net48\1.0.0\build\.NETFramework\v4.8\mscorlib.dll";
#elif NETCOREAPP3_1
	const string corlibPath = @".nuget\packages\microsoft.netcore.app.ref\3.1.0\ref\netcoreapp3.1\System.Runtime.dll";
#endif

	static readonly string assemblyPath = typeof(InheritDocTests).Assembly.Location;
	static readonly string documentPath = Path.Combine(Path.GetDirectoryName(assemblyPath), Path.GetFileNameWithoutExtension(assemblyPath) + ".xml");
	static readonly string[] referencePaths = new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), corlibPath.Replace('\\', Path.DirectorySeparatorChar)), typeof(InheritDocProcessor).Assembly.Location };

	static XElement processedDocs;

	[ClassInitialize]
	public static void InheritDocProcess(TestContext _)
	{
		string outPath = documentPath + ".after";

		var log = new DebugLogger() as ILogger;
		var res = InheritDocProcessor.InheritDocs(assemblyPath, documentPath, outPath, referencePaths, Array.Empty<string>(), ApiLevel.Internal, log);
		log.Write(ILogger.Severity.Message, $"replaced {res.Item1} of {res.Item2} and removed {res.Item3}");

		using var stmdoc = File.Open(outPath, FileMode.Open);
		processedDocs = XDocument.Load(stmdoc, LoadOptions.PreserveWhitespace).Root.Element("members");
	}

	[TestMethod]
	public void NamespaceDocPlaceholderReplaced()
	{
		var ele = getDocElement("N:" + nameof(InheritDocTest), "summary");
		Assert.AreEqual("Namespace InheritDocTest", ele?.Value);
	}

	[TestMethod]
	public void ExplicitInterfaceMethodImplementationInserted()
	{
		var ele = getDocElement("M:" + B.M_ID_X, "summary");
		Assert.AreEqual("Method X", ele?.Value);
	}

	[TestMethod]
	public void ExplicitInterfacePropertyImplementationInserted()
	{
		var ele = getDocElement("P:" + C.P_ID, "summary");
		Assert.AreEqual("Property P", ele?.Value);
	}

	[TestMethod]
	public void ExplicitInterfaceEventImplementationInserted()
	{
		var ele = getDocElement("E:" + C.E_ID, "summary");
		Assert.AreEqual("Event E", ele?.Value);
	}

	[TestMethod]
	public void ConstructorInherits()
	{
		var ele = getDocElement("M:" + GIG<string>.M_ID_ctor, "summary");
		Assert.AreEqual("Constructor GG", ele?.Value);
	}

	[TestMethod]
	public void MethodTypeParamRemapped()
	{
		var ele = getDocElement("M:" + GIG<string>.M_ID, "typeparam[@name='MT']");
		Assert.AreEqual("TypeParam U", ele?.Value);
	}

	[TestMethod]
	public void ClassTypeParamRemapped()
	{
		var ele = getDocElement("T:" + GIG<string>.T_ID, "typeparam[@name='TT']");
		Assert.AreEqual("TypeParam T", ele?.Value);
	}

	[TestMethod]
	public void CrefOverridesDefaultBase()
	{
		var ele = getDocElement("T:" + GIS<string>.T_ID, "typeparam[@name='TT']");
		Assert.AreEqual("TypeParam TG", ele?.Value);
	}

	[TestMethod]
	public void TypeParamRefRemapped()
	{
		var ele = getDocElement("M:" + GIG<string>.M_ID, "returns/typeparamref[@name='MT']");
		Assert.IsNotNull(ele);
	}

	[TestMethod]
	public void UnusedParamsTrimmed()
	{
		var ele = getDocElement("M:" + B.M_ID_O, ".");
		Assert.AreEqual(1, ele?.Elements("param").Count() ?? 0);
	}

	[TestMethod]
	public void ReturnsTrimmed()
	{
		var ele = getDocElement("M:" + B.M_ID_O, "returns");
		Assert.IsNull(ele);
	}

	[TestMethod]
	public void ParamRemapped()
	{
		var ele = getDocElement("M:" + GGI.M_ID, "param[@name='x']");
		Assert.AreEqual("Param t", ele?.Value);
	}

	[TestMethod]
	public void ParamRefRemapped()
	{
		var ele = getDocElement("M:" + GGI.M_ID, "returns/paramref[@name='x']");
		Assert.IsNotNull(ele);
	}

	[TestMethod]
	public void InterfaceInherits()
	{
		var ele = getDocElement("T:" + nameof(IY), "summary");
		Assert.AreEqual("Interface IX", ele?.Value);
	}

	[TestMethod]
	public void ClassInherits()
	{
		var ele = getDocElement("T:" + C.T_ID, "summary");
		Assert.AreEqual("Class B", ele?.Value);
	}

	[TestMethod]
	public void StructInheritsFromRefDocs()
	{
		var ele = getDocElement("T:" + D.T_ID, "summary");
		Assert.AreEqual("Defines a generalized method that a value type or class implements to create a type-specific method for determining equality of instances.", ele?.Value);
	}

	[TestMethod]
	public void ClassInheritsFromRefDocs()
	{
		var ele = getDocElement("T:" + GXI.T_ID, "summary");
		Assert.AreEqual("Provides support for lazy initialization.", ele?.Value);
	}

	[TestMethod]
	public void MethodInheritsFromRefDocs()
	{
		var ele = getDocElement("M:" + D.M_ID_EqualsOverride, "summary");
		Assert.AreEqual("Indicates whether this instance and a specified object are equal.", ele?.Value);
	}

	[TestMethod]
	public void MethodInheritsFromCref()
	{
		var ele = getDocElement("M:" + C.M_ID_M, "summary");
		Assert.AreEqual("Gets the runtime type of the current instance.", ele?.Value);
	}

	[TestMethod]
	public void InheritedDocsAreAddedToExisting()
	{
		var ele = getDocElement("M:" + C.M_ID_M, "typeparam");
		Assert.IsNotNull(ele);
	}

	[TestMethod]
	public void NestedInheritDocReplaced()
	{
		var ele = getDocElement("M:" + C.M_ID_M, "typeparam");
		Assert.AreEqual("The type of objects to enumerate.", ele?.Value);
	}

	[TestMethod]
	public void InheritedDocsFilteredByPath()
	{
		var ele = getDocElement("M:" + C.M_ID_N, "param");
		Assert.AreEqual("The message that describes the error.", ele?.Value);
	}

	[TestMethod]
	public void WhitespacePreserved()
	{
		var ele = getDocElement("T:" + W.T_ID, "summary/see[1]");
		Assert.IsTrue(ele?.NextNode?.IsWhiteSpace() ?? false);
	}

	[TestMethod]
	public void InternalMembersTrimmed()
	{
		var ele = getDocElement("F:" + W.F_ID, "summary");
		Assert.IsNull(ele);
	}

	[TestMethod]
	public void ProtectedMembersPreserved()
	{
		var ele = getDocElement("M:" + B.M_ID_P, "summary");
		Assert.IsNotNull(ele);
	}

	private static XElement? getDocElement(string docID, string xpath) =>
		processedDocs.Elements("member").FirstOrDefault(m => (string)m.Attribute("name") == docID)?.XPathSelectElement(xpath);

	private class DebugLogger : ILogger
	{
		void ILogger.Write(ILogger.Severity severity, string msg)
		{
			if (severity >= ILogger.Severity.Info)
				Debug.WriteLine(msg);
		}

		void ILogger.Warn(string code, string file, int line, int column, string msg) => Debug.WriteLine(code + ": " + msg);
	}
}
