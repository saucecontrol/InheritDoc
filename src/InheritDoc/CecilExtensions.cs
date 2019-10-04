using System.Linq;
using System.Collections.Generic;

using Mono.Cecil;

internal static class CecilExtensions
{
	private const string compilerGeneratedAttribute = "System.Runtime.CompilerServices.CompilerGeneratedAttribute";

	private const string namespaceDocPlaceholderType = "NamespaceDoc";

	private static readonly string[] ignoredModifiers = new[] {
		"System.Runtime.InteropServices.InAttribute",
		"System.Runtime.CompilerServices.CallConvCdecl"
	};

	private static readonly string[] ignoredBaseTypes = new[] {
		"System.Object",
		"System.ValueType",
		"System.Enum",
		"System.Delegate",
		"System.MulticastDelegate"
	};

	// DocID generation and type/parameter encoding is described here:
	// https://docs.microsoft.com/en-us/cpp/build/reference/dot-xml-file-processing
	public static string GetDocID(this TypeDefinition t) => "T:" + encodeTypeName(t);

	public static string GetDocID(this EventDefinition e) => "E:" + encodeTypeName(e.DeclaringType) + "." + encodeMemberName(e.Name);

	public static string GetDocID(this PropertyDefinition p) => "P:" + encodeTypeName(p.DeclaringType) + "." + encodeMemberName(p.Name) + encodeMethodParams(p.Parameters);

	public static string GetDocID(this MethodDefinition m)
	{
		if (m.IsEventMethod())
			return getEventForMethod(m).GetDocID();

		if (m.IsPropertyMethod())
			return getPropertyForMethod(m).GetDocID();

		string docID = "M:" + encodeTypeName(m.DeclaringType) + "." + encodeMemberName(m.Name);

		if (m.HasGenericParameters)
			docID += "``" + m.GenericParameters.Count;

		if (m.HasParameters)
			docID += encodeMethodParams(m.Parameters);

		if (m.Name == "op_Implicit" || m.Name == "op_Explicit")
			docID += "~" + encodeTypeName(m.ReturnType);

		return docID;
	}

	public static bool IsCompilerGenerated(this TypeDefinition t) => (t.HasCustomAttributes && t.CustomAttributes.Any(a => a.AttributeType.FullName == compilerGeneratedAttribute)) || (t.IsNested && t.DeclaringType.IsCompilerGenerated());

	public static bool IsCompilerGenerated(this MethodDefinition m) => m.HasCustomAttributes && m.CustomAttributes.Any(a => a.AttributeType.FullName == compilerGeneratedAttribute);

	public static bool IsNamespaceDocPlaceholder(this TypeDefinition t) => t.Name == namespaceDocPlaceholderType;

	public static bool IsEventMethod(this MethodDefinition m) => m.IsFire || m.IsAddOn || m.IsRemoveOn;

	public static bool IsPropertyMethod(this MethodDefinition m) => m.IsGetter || m.IsSetter;

	public static IEnumerable<TypeReference> GetBaseCandidates(this TypeDefinition t)
	{
		var it = t;

		while (t.BaseType != null && !ignoredBaseTypes.Contains(t.BaseType.FullName))
		{
			yield return t.BaseType;
			t = t.BaseType.Resolve();
		}

		foreach (var i in it.Interfaces)
			yield return i.InterfaceType;

		if (t.BaseType != null && ignoredBaseTypes.Contains(t.BaseType.FullName))
			yield return t.BaseType;
	}

	public static IEnumerable<MethodDefinition> GetBaseCandidates(this MethodDefinition m)
	{
		if ((m.IsVirtual && m.IsReuseSlot) || m.IsConstructor)
			foreach (var md in getBaseCandidatesFromType(m, m.DeclaringType.BaseType))
				yield return md;

		foreach (var md in m.DeclaringType.Interfaces.SelectMany(i => getBaseCandidatesFromType(m, i.InterfaceType)))
			yield return md;
	}

	private static EventDefinition getEventForMethod(MethodDefinition method) => method.DeclaringType.Events.First(e => e.InvokeMethod == method || e.AddMethod == method || e.RemoveMethod == method);

	private static PropertyDefinition getPropertyForMethod(MethodDefinition method) => method.DeclaringType.Properties.First(p => p.GetMethod == method || p.SetMethod == method);

	private static IEnumerable<MethodDefinition> getBaseCandidatesFromType(MethodDefinition om, TypeReference bt)
	{
		var genMap = new Dictionary<TypeReference, TypeReference>();

		while (bt != null)
		{
			var rbt = (bt.IsGenericInstance ? ((GenericInstanceType)bt).ElementType : bt).Resolve();

			if (bt.IsGenericInstance)
			{
				var gi = (GenericInstanceType)bt;
				for (int i = 0; i < gi.GenericArguments.Count; i++)
					genMap[rbt.GenericParameters[i]] = gi.GenericArguments[i];

				foreach (var ga in genMap.Where(kv => genMap.ContainsKey(kv.Value)).ToList())
					genMap[ga.Key] = genMap[ga.Value];
			}

			foreach (var bm in rbt.Methods.Where(m => m.Name == om.Name && m.Parameters.Count == om.Parameters.Count))
			{
				if (!areParamTypesEquivalent(bm.ReturnType, om.ReturnType, genMap))
					continue;

				bool match = true;
				for (int i = 0; i < bm.Parameters.Count; i++)
				{
					if (!areParamTypesEquivalent(bm.Parameters[i].ParameterType, om.Parameters[i].ParameterType, genMap))
					{
						match = false;
						break;
					}
				}

				if (match)
					yield return bm;
			}

			bt = rbt.BaseType;
		}
	}

	private static bool areParamTypesEquivalent(TypeReference mp, TypeReference op, IDictionary<TypeReference, TypeReference> genMap)
	{
		if (mp is IModifierType mpm && op is IModifierType opm)
			return areParamTypesEquivalent(mpm.ModifierType, opm.ModifierType, genMap) && areParamTypesEquivalent(mpm.ElementType, opm.ElementType, genMap);

		if (mp is ArrayType mpa && op is ArrayType opa)
			return mpa.Rank == opa.Rank && areParamTypesEquivalent(mpa.ElementType, opa.ElementType, genMap);

		if (mp is TypeSpecification mpe && op is TypeSpecification ope)
			return areParamTypesEquivalent(mpe.ElementType, ope.ElementType, genMap);

		return mp.MetadataToken == op.MetadataToken || mp.Resolve() == op.Resolve() || (mp.IsGenericParameter && genMap.ContainsKey(mp) && areParamTypesEquivalent(genMap[mp], op, genMap));
	}

	private static string? encodeMethodParams(ICollection<ParameterDefinition> mp) => mp.Count > 0 ? "(" + string.Join(",", mp.Select(p => encodeTypeName(p.ParameterType))) + ")" : null;

	private static string encodeMemberName(string mn) => mn.Replace('.', '#').Replace('<', '{').Replace('>', '}').Replace(',', '@');

	private static string encodeTypeName(TypeReference tr)
	{
		string? suffix = null;

		while (!tr.IsGenericInstance && !tr.IsFunctionPointer && tr is TypeSpecification ts)
		{
			if (tr.IsPointer)
				suffix = "*" + suffix;
			else if (tr.IsByReference)
				suffix = "@" + suffix;
			else if (tr.IsPinned)
				suffix = "^" + suffix;
			else if (tr is IModifierType mt && !ignoredModifiers.Contains(mt.ModifierType.FullName))
				suffix = (tr.IsRequiredModifier ? "|" : "!") + mt.ModifierType.FullName + suffix;
			else if (tr is ArrayType at)
				suffix = "[" + string.Join(",", at.Dimensions.Select(d => d.IsSized ? d.LowerBound?.ToString() + ":" + d.UpperBound?.ToString() : null)) + "]" + suffix;

			tr = ts.ElementType;
		}

		if (tr.IsFunctionPointer)
		{
			var fp = (FunctionPointerType)tr;
			return "=FUNC:" + encodeTypeName(fp.ReturnType) + encodeMethodParams(fp.Parameters) + suffix;
		}

		if (tr.IsGenericParameter)
		{
			var gp = (GenericParameter)tr;
			var gpi = (gp.DeclaringType?.GenericParameters ?? gp.DeclaringMethod?.GenericParameters).Single(g => g.Name == gp.Name);
			return (gp.DeclaringType is null ? "``" : "`") + gpi.Position + suffix;
		}

		string typeName = tr.FullName.Replace('/', '.');

		if (tr.IsGenericInstance)
		{
			var gi = (GenericInstanceType)tr;
			typeName = typeName.Substring(0, typeName.IndexOf('`')) + "{" + string.Join(",", gi.GenericArguments.Select(ga => encodeTypeName(ga))) + "}";

			while (tr.IsNested)
			{
				suffix = "." + tr.Name + suffix;
				tr = tr.DeclaringType;
			}
		}

		return typeName + suffix;
	}

	internal class RefAssemblyResolver : DefaultAssemblyResolver
	{
		public static RefAssemblyResolver Create(string[] refAssemblies)
		{
			var resolver = new RefAssemblyResolver();

			foreach (var assemblyFile in refAssemblies)
				resolver.RegisterAssembly(AssemblyDefinition.ReadAssembly(assemblyFile, new ReaderParameters { AssemblyResolver = resolver }));

			return resolver;
		}

		private RefAssemblyResolver() { }
	}
}
