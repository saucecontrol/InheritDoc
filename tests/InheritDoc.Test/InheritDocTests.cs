// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

#pragma warning disable CS1591 // missing XML docs

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;
using System.Diagnostics;
using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class InheritDocTests
{
#if NET48
	const string corlibPath = @".nuget\packages\microsoft.netframework.referenceassemblies.net48\1.0.0\build\.NETFramework\v4.8\mscorlib.dll";
#elif NET6_0
	const string corlibPath = @".nuget\packages\microsoft.netcore.app.ref\7.0.0\ref\net7.0\System.Runtime.dll";
#endif

	static readonly string assemblyPath = typeof(InheritDocTests).Assembly.Location;
	static readonly string documentPath = Path.Combine(Path.GetDirectoryName(assemblyPath), Path.GetFileNameWithoutExtension(assemblyPath) + ".xml");
	static readonly string[] referencePaths = new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), corlibPath.Replace('\\', Path.DirectorySeparatorChar)), typeof(InheritDocProcessor).Assembly.Location };
	static readonly DebugLogger logger = new();

	static XElement processedDocs;
	static XElement processedDocsPrivateTrim;

	[ClassInitialize]
	public static void InheritDocProcess(TestContext _)
	{
		string outPath = documentPath + ".after";
		string outPathPrivateTrim = documentPath + ".privateTrim.after";

		var (replaced, total, trimmed) = InheritDocProcessor.InheritDocs(assemblyPath, documentPath, outPath, referencePaths, [ ], ApiLevel.Internal, logger);
		logger.Write(ILogger.Severity.Message, $"replaced {replaced} of {total} and removed {trimmed}");

		using var stmdoc = File.Open(outPath, FileMode.Open);
		processedDocs = XDocument.Load(stmdoc, LoadOptions.PreserveWhitespace).Root.Element("members");

		var task = new InheritDocTask { AssemblyPath = assemblyPath, InDocPath = documentPath, OutDocPath = outPathPrivateTrim, RefAssemblyPaths = string.Join(";", referencePaths), TrimLevel = ApiLevel.Private.ToString(), Logger = logger };
		task.Execute();

		using var stmdocPrivateTrim = File.Open(outPathPrivateTrim, FileMode.Open);
		processedDocsPrivateTrim = XDocument.Load(stmdocPrivateTrim, LoadOptions.PreserveWhitespace).Root.Element("members");
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
	public void NestedClassInherits()
	{
		var ele = getDocElement("T:" + B.T_ID_ND, "summary");
		Assert.AreEqual("Class B.NC", ele?.Value);
	}

	[TestMethod]
	public void PropertyWithParamInherits()
	{
		var ele = getDocElement("P:" + GG<string>.P_ID_this, "summary");
		Assert.AreEqual("Property this[]", ele?.Value);
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
	public void NestedGenericParamsAssigned()
	{
		var ele = getDocElement("M:" + GIG<string>.M_ID_MImplicit, "summary");
		Assert.AreEqual("Method M ", ele?.Value);
	}

	[TestMethod]
	public void MethodTypeParamRemapped()
	{
		var ele = getDocElement("M:" + GIG<string>.M_ID_MExplicit, "typeparam[@name='MT']");
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
	public void MethodTypeParamRefRemapped()
	{
		var ele = getDocElement("M:" + GIG<string>.M_ID_MExplicit, "returns/typeparamref[@name='MT']");
		Assert.IsNotNull(ele);
	}

	[TestMethod]
	public void MethodTypeParamRefRemappedFromClass()
	{
		var ele = getDocElement("M:" + GIG<string>.M_ID_MImplicit, "summary/typeparamref[@name='TT']");
		Assert.IsNotNull(ele);
	}

	[TestMethod]
	public void ClosedTypeParamRefReplaced()
	{
		var ele = getDocElement("P:" + GGI.P_ID, "value/see[@cref='T:System.String[]']");
		Assert.IsNotNull(ele);
	}

	[TestMethod]
	public void UnusedTypeParamsTrimmed()
	{
		var ele = getDocElement("T:" + D.T_ID, ".");
		Assert.AreEqual(0, ele?.Elements("typeparam").Count() ?? 0);
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
		var ele = getDocElement("T:" + GXI<string>.T_ID, "summary");
		Assert.AreEqual("Provides support for lazy initialization.", ele?.Value);
	}

	[TestMethod]
	public void MethodInheritsFromRefDocs()
	{
		var ele = getDocElement("M:" + D.M_ID_EqualsOverride, "summary");
		Assert.AreEqual("Indicates whether this instance and a specified object are equal.", ele?.Value);
	}

	[TestMethod]
	public void ExplicitGenericInterfaceImplInherits()
	{
		var ele = getDocElement("M:" + D.M_ID_EqualsExplicit, "summary");
		Assert.IsNotNull(ele);
	}

	[TestMethod]
	public void MethodInheritsFromCref()
	{
		var ele = getDocElement("M:" + C.M_ID_M, "summary");
		Assert.AreEqual("Gets the runtime type of the current instance.", ele?.Value);
	}

	[TestMethod]
	public void MDArrayMethodParam()
	{
		var ele = getDocElement("M:" + C.M_ID_Y, "summary");
		Assert.AreEqual("Method Y", ele?.Value);
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
	public void PrimaryConstructorInheritsFromBase()
	{
		var ele = getDocElement("M:" + PC.M_ID_ctor, "summary");
		Assert.AreEqual("Constructor GG", ele?.Value);
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

	[TestMethod]
	public void MultiParamGenericExplicitProperty()
	{
		var ele = getDocElement("P:" + ImplementsIDictionary.P_ID_Keys, "summary");
		Assert.IsNotNull(ele);
	}

	[TestMethod]
	public void MultiParamGenericExplicitMethod()
	{
		var ele = getDocElement("M:" + ImplementsIDictionary.M_ID_Add, "summary");
		Assert.IsNotNull(ele);
	}

	[TestMethod]
	public void InternalClassImplementsInterfaceExplicit(){
		var ele = getDocElement("M:" + ImplementsICollection.M_ID_ADD, "summary");
		Assert.IsNull(ele);
	}

	[TestMethod]
	public void InternalClassImplementsInterfaceImplicit()
	{
		var ele = getDocElement("M:" + ImplementsICollection.M_ID_CLEAR, "summary");
		Assert.IsNull(ele);
	}

	[TestMethod]
	public void InternalClassImplementsInterfaceExplicitPrivateTrim(){
		var ele = getDocElementPrivateTrim("M:" + ImplementsICollection.M_ID_ADD, "summary");
		Assert.IsNotNull(ele);
	}

	[TestMethod]
	public void InternalClassImplementsInterfaceImplicitPrivateTrim()
	{
		var ele = getDocElementPrivateTrim("M:" + ImplementsICollection.M_ID_CLEAR, "summary");
		Assert.IsNotNull(ele);
	}

	[TestMethod]
	public void NoBaseWarning()
	{
		bool warn = logger.Warnings.Any(w => w.code == ErrorCodes.NoBase && w.msg.Contains("M:" + W.M_ID_NotInherited));
		Assert.IsTrue(warn);
	}

	[TestMethod]
	public void NoBaseWarningIgnoredForGenerated()
	{
		bool warn = logger.Warnings.Any(w => w.code == ErrorCodes.NoBase && w.msg.Contains("P:" + W.P_ID_NotInherited));
		Assert.IsFalse(warn);
	}

	private static XElement? getDocElement(string docID, string xpath) =>
		processedDocs.Elements("member").FirstOrDefault(m => (string)m.Attribute("name") == docID)?.XPathSelectElement(xpath);

	private static XElement? getDocElementPrivateTrim(string docID, string xpath) =>
		processedDocsPrivateTrim.Elements("member").FirstOrDefault(m => (string)m.Attribute("name") == docID)?.XPathSelectElement(xpath);

	private class DebugLogger : ILogger
	{
		public readonly List<(string code, string msg)> Warnings = [ ];

		public void Write(ILogger.Severity severity, string msg)
		{
			if (severity >= ILogger.Severity.Info)
				Debug.WriteLine(msg);
		}

		public void Warn(string code, string file, int line, int column, string msg)
		{
			Warnings.Add((code, msg));
			Debug.WriteLine(code + ": " + msg);
		}
	}
}
