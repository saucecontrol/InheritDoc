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
		processedDocs = XDocument.Load(stmdoc).Root.Element("members");
	}

	[TestMethod]
	public void NamespaceDocPlaceholderReplaced()
	{
		var doc = getDocWithID("N:" + nameof(InheritDocTest));
		Assert.AreEqual("Namespace InheritDocTest", doc?.Value);
	}

	[TestMethod]
	public void ExplicitInterfaceMethodImplementationInserted()
	{
		var doc = getDocWithID("M:" + B.M_ID_X);
		Assert.AreEqual("Method X", doc?.Value);
	}

	[TestMethod]
	public void ExplicitInterfacePropertyImplementationInserted()
	{
		var doc = getDocWithID("P:" + C.P_ID);
		Assert.AreEqual("Property P", doc?.Value);
	}

	[TestMethod]
	public void ExplicitInterfaceEventImplementationInserted()
	{
		var doc = getDocWithID("E:" + C.E_ID);
		Assert.AreEqual("Event E", doc?.Value);
	}

	[TestMethod]
	public void ConstructorInherits()
	{
		var doc = getDocWithID("M:" + GIG<string>.M_ID_ctor);
		Assert.AreEqual("Constructor GG", doc?.Value);
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
		Assert.AreEqual("Interface IX", doc?.Value);
	}

	[TestMethod]
	public void ClassInherits()
	{
		var doc = getDocWithID("T:" + C.T_ID);
		Assert.AreEqual("Class B", doc?.Value);
	}

	[TestMethod]
	public void StructInheritsFromRefDocs()
	{
		var doc = getDocWithID("T:" + D.T_ID);
		Assert.AreEqual("Defines a generalized method that a value type or class implements to create a type-specific method for determining equality of instances.", doc?.Value);
	}

	[TestMethod]
	public void ClassInheritsFromRefDocs()
	{
		var doc = getDocWithID("T:" + GXI.T_ID);
		Assert.AreEqual("Provides support for lazy initialization.", doc?.Value);
	}

	[TestMethod]
	public void MethodInheritsFromRefDocs()
	{
		var doc = getDocWithID("M:" + D.M_ID_EqualsOverride);
		Assert.AreEqual("Indicates whether this instance and a specified object are equal.", doc?.Element("summary")?.Value);
	}

	[TestMethod]
	public void MethodInheritsFromCref()
	{
		var doc = getDocWithID("M:" + C.M_ID_M);
		Assert.AreEqual("Gets the runtime type of the current instance.", doc?.Element("summary")?.Value);
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
