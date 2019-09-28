#pragma warning disable CS1591 // missing XML docs

using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using System.Diagnostics;

using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class InheritDocTests
{
#if NET46
	static readonly string referencePath = Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles(x86)"), @"Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6\mscorlib.dll");
#elif NETCOREAPP2_1
	static readonly string referencePath = Path.Combine(Environment.GetEnvironmentVariable("USERPROFILE"), @".nuget\packages\microsoft.netcore.app\2.1.0\ref\netcoreapp2.1\System.Runtime.dll");
#endif

	static readonly string assemblyPath = typeof(InheritDocTests).Assembly.Location;
	static readonly string documentPath = Path.Combine(Path.GetDirectoryName(assemblyPath), Path.GetFileNameWithoutExtension(assemblyPath) + ".xml");

	static XElement processedDocs;

	[ClassInitialize]
	public static void InheritDocProcess(TestContext ctx)
	{
		string outPath = documentPath + ".after";

		InheritDocProcessor.InheritDocs(assemblyPath, documentPath, new[] { referencePath }, Array.Empty<string>(), outPath, new DebugLogger());

		using var stmdoc = File.Open(outPath, FileMode.Open);
		processedDocs = XDocument.Load(stmdoc, LoadOptions.PreserveWhitespace).Root.Element("members");
	}

	[TestMethod]
	public void NamespaceDocPlaceholderReplaced()
	{
		var doc = getDocWithID("N:" + nameof(InheritDocTest));
		var ele = doc?.XPathSelectElement("summary");
		Assert.AreEqual("Namespace InheritDocTest", ele?.Value);
	}

	[TestMethod]
	public void ExplicitInterfaceMethodImplementationInserted()
	{
		var doc = getDocWithID("M:" + B.M_ID_X);
		var ele = doc?.XPathSelectElement("summary");
		Assert.AreEqual("Method X", ele?.Value);
	}

	[TestMethod]
	public void ExplicitInterfacePropertyImplementationInserted()
	{
		var doc = getDocWithID("P:" + C.P_ID);
		var ele = doc?.XPathSelectElement("summary");
		Assert.AreEqual("Property P", ele?.Value);
	}

	[TestMethod]
	public void ExplicitInterfaceEventImplementationInserted()
	{
		var doc = getDocWithID("E:" + C.E_ID);
		var ele = doc?.XPathSelectElement("summary");
		Assert.AreEqual("Event E", ele?.Value);
	}

	[TestMethod]
	public void ConstructorInherits()
	{
		var doc = getDocWithID("M:" + GIG<string>.M_ID_ctor);
		var ele = doc?.XPathSelectElement("summary");
		Assert.AreEqual("Constructor GG", ele?.Value);
	}

	[TestMethod]
	public void MethodTypeParamRemapped()
	{
		var doc = getDocWithID("M:" + GIG<string>.M_ID);
		var ele = doc?.XPathSelectElement("typeparam[@name='MT']");
		Assert.AreEqual("TypeParam U", ele?.Value);
	}

	[TestMethod]
	public void ClassTypeParamRemapped()
	{
		var doc = getDocWithID("T:" + GIG<string>.T_ID);
		var ele = doc?.XPathSelectElement("typeparam[@name='TT']");
		Assert.AreEqual("TypeParam T", ele?.Value);
	}

	[TestMethod]
	public void TypeParamRefRemapped()
	{
		var doc = getDocWithID("M:" + GIG<string>.M_ID);
		var ele = doc?.XPathSelectElement("returns/typeparamref[@name='MT']");
		Assert.IsNotNull(ele);
	}

	[TestMethod]
	public void UnusedParamsTrimmed()
	{
		var doc = getDocWithID("M:" + B.M_ID_O);
		Assert.AreEqual(1, doc?.Elements("param").Count() ?? 0);
	}

	[TestMethod]
	public void ParamRemapped()
	{
		var doc = getDocWithID("M:" + GGI.M_ID);
		var ele = doc?.XPathSelectElement("param[@name='x']");
		Assert.AreEqual("Param t", ele?.Value);
	}

	[TestMethod]
	public void ParamRefRemapped()
	{
		var doc = getDocWithID("M:" + GGI.M_ID);
		var ele = doc?.XPathSelectElement("returns/paramref[@name='x']");
		Assert.IsNotNull(ele);
	}

	[TestMethod]
	public void InterfaceInherits()
	{
		var doc = getDocWithID("T:" + nameof(IY));
		var ele = doc?.XPathSelectElement("summary");
		Assert.AreEqual("Interface IX", ele?.Value);
	}

	[TestMethod]
	public void ClassInherits()
	{
		var doc = getDocWithID("T:" + C.T_ID);
		var ele = doc?.XPathSelectElement("summary");
		Assert.AreEqual("Class B", ele?.Value);
	}

	[TestMethod]
	public void StructInheritsFromRefDocs()
	{
		var doc = getDocWithID("T:" + D.T_ID);
		var ele = doc?.XPathSelectElement("summary");
		Assert.AreEqual("Defines a generalized method that a value type or class implements to create a type-specific method for determining equality of instances.", ele?.Value);
	}

	[TestMethod]
	public void ClassInheritsFromRefDocs()
	{
		var doc = getDocWithID("T:" + GXI.T_ID);
		var ele = doc?.XPathSelectElement("summary");
		Assert.AreEqual("Provides support for lazy initialization.", ele?.Value);
	}

	[TestMethod]
	public void MethodInheritsFromRefDocs()
	{
		var doc = getDocWithID("M:" + D.M_ID_EqualsOverride);
		var ele = doc?.XPathSelectElement("summary");
		Assert.AreEqual("Indicates whether this instance and a specified object are equal.", ele?.Value);
	}

	[TestMethod]
	public void MethodInheritsFromCref()
	{
		var doc = getDocWithID("M:" + C.M_ID_M);
		var ele = doc?.XPathSelectElement("summary");
		Assert.AreEqual("Gets the runtime type of the current instance.", ele?.Value);
	}

	[TestMethod]
	public void InheritedDocsAreAddedToExisting()
	{
		var doc = getDocWithID("M:" + C.M_ID_M);
		var ele = doc?.Element("typeparam");
		Assert.IsNotNull(ele);
	}

	[TestMethod]
	public void NestedInheritDocReplaced()
	{
		var doc = getDocWithID("M:" + C.M_ID_M);
		var ele = doc?.Element("typeparam");
		Assert.AreEqual("The type of objects to enumerate.", ele?.Value);
	}

	[TestMethod]
	public void InheritedDocsFilteredByPath()
	{
		var doc = getDocWithID("M:" + C.M_ID_N);
		var ele = doc?.Element("param");
		Assert.AreEqual("The message that describes the error.", ele?.Value);
	}

	[TestMethod]
	public void WhitespacePreserved()
	{
		var doc = getDocWithID("T:" + W.T_ID);
		var ele = doc?.XPathSelectElement("summary/see[1]");
		Assert.IsTrue(ele?.NextNode?.IsWhiteSpace() ?? false);
	}

	private static XElement? getDocWithID(string docID) => processedDocs.Elements("member").FirstOrDefault(m => (string)m.Attribute("name") == docID);

	private class DebugLogger : ILogger
	{
		void ILogger.Write(ILogger.Severity severity, string msg)
		{
			if (severity >= ILogger.Severity.Info)
				Debug.Write(msg);
		}
	}
}
