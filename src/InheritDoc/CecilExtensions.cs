// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

using System;
using System.Linq;
using System.Collections.Generic;

using Mono.Cecil;
using Mono.Cecil.Cil;

internal static class CecilExtensions
{
	private static readonly string[] generatedCodeAttributes = [
		"System.CodeDom.Compiler.GeneratedCodeAttribute",
		"System.Diagnostics.DebuggerNonUserCodeAttribute",
		"System.Runtime.CompilerServices.CompilerGeneratedAttribute"
	];

	// These are ignored as the base for types, defaulting the doc inheritance target to an implemented interface instead.
	private static readonly string[] ignoredBaseTypes = [
		"System.Object",
		"System.ValueType",
		"System.Enum",
		"System.Delegate",
		"System.MulticastDelegate"
	];

	private static readonly IEnumerable<string> emptyStringEnumerable = new[] { string.Empty };

	// DocID generation and type/parameter encoding are described here:
	// https://docs.microsoft.com/en-us/cpp/build/reference/dot-xml-file-processing
	// https://github.com/dotnet/csharplang/blob/master/spec/documentation-comments.md#id-string-format
	// Roslyn's actual implementation can be found in:
	// http://sourceroslyn.io/#Microsoft.CodeAnalysis.CSharp/DocumentationComments/DocumentationCommentIDVisitor.cs
	// http://sourceroslyn.io/#Microsoft.CodeAnalysis.CSharp/DocumentationComments/DocumentationCommentIDVisitor.PartVisitor.cs
	// Different compilers/tools generate different encodings, so we generate a list of candidates that includes each style.
	public static string GetDocID(this TypeReference t) => encodeTypeName(t).Select(t => "T:" + t).First();

	public static IEnumerable<string> GetDocID(this EventDefinition e) => encodeTypeName(e.DeclaringType).SelectMany(t => encodeMemberName(e.Name).Select(m => "E:" + t + "." + m));

	public static IEnumerable<string> GetDocID(this FieldDefinition f) => encodeTypeName(f.DeclaringType).SelectMany(t => encodeMemberName(f.Name).Select(m => "F:" + t + "." + m));

	public static IEnumerable<string> GetDocID(this PropertyDefinition p)
	{
		var docID = encodeTypeName(p.DeclaringType).SelectMany(t => encodeMemberName(p.Name).Select(m => "P:" + t + "." + m));
		
		if (p.HasParameters)
			docID = docID.SelectMany(m => encodeMethodParams(p.Parameters).Select(p => m + p));

		return docID;
	}

	public static IEnumerable<string> GetDocID(this MethodDefinition m)
	{
		if (m.IsEventMethod())
			return getEventForMethod(m).GetDocID();

		if (m.IsPropertyMethod())
			return getPropertyForMethod(m).GetDocID();

		var docID = encodeTypeName(m.DeclaringType).SelectMany(t => encodeMemberName(m.Name).Select(m => "M:" + t + "." + m));

		if (m.HasGenericParameters)
			docID = docID.Select(id => id + "``" + m.GenericParameters.Count);

		if (m.HasParameters)
			docID = docID.SelectMany(id => encodeMethodParams(m.Parameters).Select(p => id + p));

		if (m.Name is "op_Implicit" or "op_Explicit")
			docID = docID.SelectMany(id => encodeTypeName(m.ReturnType).Select(t => id + "~" + t));

		return docID;
	}

	public static bool IsGeneratedCode(this TypeDefinition t) => (t.HasCustomAttributes && t.CustomAttributes.Any(a => generatedCodeAttributes.Contains(a.AttributeType.FullName))) || (t.IsNested && t.DeclaringType.IsGeneratedCode());

	public static bool IsGeneratedCode(this MethodDefinition m)
	{
		if (m.HasCustomAttributes && m.CustomAttributes.Any(a => generatedCodeAttributes.Contains(a.AttributeType.FullName)))
			return true;

		if (m.IsPropertyMethod())
			return getPropertyForMethod(m).IsGeneratedCode();

		if (m.IsEventMethod())
			return getEventForMethod(m).IsGeneratedCode();

		return m.DeclaringType.IsGeneratedCode();
	}

	public static bool IsGeneratedCode(this EventDefinition e) => e.HasCustomAttributes && e.CustomAttributes.Any(a => generatedCodeAttributes.Contains(a.AttributeType.FullName)) || e.DeclaringType.IsGeneratedCode();

	public static bool IsGeneratedCode(this PropertyDefinition p) => p.HasCustomAttributes && p.CustomAttributes.Any(a => generatedCodeAttributes.Contains(a.AttributeType.FullName)) || p.DeclaringType.IsGeneratedCode();

	public static bool IsNamespaceDocPlaceholder(this TypeDefinition t) => t.Name == "NamespaceDoc";

	public static bool IsEventMethod(this MethodDefinition m) => m.IsFire || m.IsAddOn || m.IsRemoveOn;

	public static bool IsPropertyMethod(this MethodDefinition m) => m.IsGetter || m.IsSetter;

	public static bool HasReturnValue(this MethodDefinition m) => m.ReturnType is TypeReference tr && tr.FullName != "System.Void";

	public static ApiLevel GetApiLevel(this TypeDefinition t)
	{
		int level = (int)ApiLevel.Public;

		while (t.IsNested)
		{
			if (t.IsNestedPrivate)
				return ApiLevel.Private;

			level = Math.Min(level, (int)(t.IsNestedAssembly || t.IsNestedFamilyAndAssembly ? ApiLevel.Internal : ApiLevel.Public));
			t = t.DeclaringType;
		}

		return (ApiLevel)Math.Min(level, (int)(t.IsNotPublic ? ApiLevel.Internal : ApiLevel.Public));
	}

	public static ApiLevel GetApiLevel(this MethodDefinition m)
	{
		if (m.IsPropertyMethod())
			return getPropertyForMethod(m).GetApiLevel();

		if (m.IsEventMethod())
			return getEventForMethod(m).GetApiLevel();

		return (ApiLevel)Math.Min((int)m.DeclaringType.GetApiLevel(), (int)getApiLevel(m));
	}

	public static ApiLevel GetApiLevel(this EventDefinition e) => (ApiLevel)Math.Min((int)e.DeclaringType.GetApiLevel(), Math.Max(Math.Max((int)getApiLevel(e.InvokeMethod), (int)getApiLevel(e.AddMethod)), (int)getApiLevel(e.RemoveMethod)));

	public static ApiLevel GetApiLevel(this PropertyDefinition p) => (ApiLevel)Math.Min((int)p.DeclaringType.GetApiLevel(), Math.Max((int)getApiLevel(p.GetMethod), (int)getApiLevel(p.SetMethod)));

	public static ApiLevel GetApiLevel(this FieldDefinition f) => (ApiLevel)Math.Min((int)f.DeclaringType.GetApiLevel(), (int)getApiLevel(f));

	public static IEnumerable<TypeReference> GetBaseCandidates(this TypeDefinition t)
	{
		var it = t;

		while (t.BaseType is not null && !ignoredBaseTypes.Contains(t.BaseType.FullName))
		{
			yield return t.BaseType;
			t = t.BaseType.Resolve();
		}

		foreach (var i in it.Interfaces)
			yield return i.InterfaceType;

		if (t.BaseType is not null && ignoredBaseTypes.Contains(t.BaseType.FullName))
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

	public static MethodDefinition? GetBaseConstructor(this MethodDefinition m)
	{
		foreach (var ins in m.Body.Instructions.Where(i => i.OpCode == OpCodes.Call && i.Operand is MethodReference))
		{
			var tgt = ((MethodReference)ins.Operand).Resolve();
			if (tgt.IsConstructor)
				return tgt;
		}

		return null;
	}

	public static TypeReference? GetNextBase(this TypeDefinition t, TypeDefinition bt)
	{
		var rb = t;
		do rb = rb.BaseType?.Resolve();
		while (rb is not null && rb != bt);

		if (rb == bt)
			return t.BaseType!;

		foreach (var i in t.Interfaces)
		{
			rb = i.InterfaceType.Resolve();
			if (rb == bt || rb.GetNextBase(bt) is not null)
				return i.InterfaceType;
		}

		return null;
	}

	public static Dictionary<string, TypeReference>? GetTypeParamMap(this TypeDefinition t, TypeDefinition? bt)
	{
		if (!t.HasGenericParameters && !(bt?.HasGenericParameters).GetValueOrDefault())
			return null;

		var ctm = new Dictionary<string, TypeReference>();

		if ((bt?.HasGenericParameters).GetValueOrDefault())
		{
			var stack = new Stack<TypeReference>();
			var nbt = (TypeReference)t;
			var nrt = t;
			do
			{
				nbt = nrt.GetNextBase(bt!);
				if (nbt is null)
					break;

				nrt = nbt.Resolve();
				stack.Push(nbt);
			}
			while (nrt != bt);

			if (stack.Count != 0 && stack.Pop() is GenericInstanceType gi && gi.Resolve() == bt)
			{
				foreach (var gp in bt!.GenericParameters)
					ctm[gp.Name] = gi.GenericArguments[gp.Position];
			}

			foreach (var gb in stack.Where(b => b.IsGenericInstance).Cast<GenericInstanceType>())
			{
				var rb = gb.Resolve();
				foreach (var kv in ctm.Where(e => e.Value.IsGenericParameter && rb.GenericParameters.Contains(e.Value)).ToList())
					ctm[kv.Key] = gb.GenericArguments[rb.GenericParameters.IndexOf((GenericParameter)kv.Value)];
			}
		}

		foreach (var gp in t.GenericParameters.Where(p => !ctm.ContainsKey(p.Name)))
			ctm.Add(gp.Name, gp);

		return ctm;
	}

	private static ApiLevel getApiLevel(MethodDefinition? m) => m is null ? ApiLevel.None : m.IsPrivate ? ApiLevel.Private : m.IsAssembly || m.IsFamilyAndAssembly ? ApiLevel.Internal : ApiLevel.Public;

	private static ApiLevel getApiLevel(FieldDefinition? f) => f is null ? ApiLevel.None : f.IsPrivate ? ApiLevel.Private : f.IsAssembly || f.IsFamilyAndAssembly ? ApiLevel.Internal : ApiLevel.Public;

	private static EventDefinition getEventForMethod(MethodDefinition m) => m.DeclaringType.Events.First(e => e.InvokeMethod == m || e.AddMethod == m || e.RemoveMethod == m);

	private static PropertyDefinition getPropertyForMethod(MethodDefinition m) => m.DeclaringType.Properties.First(p => p.GetMethod == m || p.SetMethod == m);

	private static IEnumerable<MethodDefinition> getBaseCandidatesFromType(MethodDefinition om, TypeReference bt)
	{
		var genMap = new Dictionary<TypeReference, TypeReference>();

		while (bt is not null)
		{
			var rbt = bt.Resolve();
			if (bt.IsGenericInstance)
			{
				var gi = (GenericInstanceType)bt;
				for (int i = 0; i < gi.GenericArguments.Count; i++)
					genMap[rbt.GenericParameters[i]] = gi.GenericArguments[i];

				foreach (var ga in genMap.Where(kv => genMap.ContainsKey(kv.Value)).ToList())
					genMap[ga.Key] = genMap[ga.Value];
			}

			foreach (var bm in rbt.Methods.Where(m => m.Name == om.Name && m.Parameters.Count == om.Parameters.Count && m.GenericParameters.Count == om.GenericParameters.Count))
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
			return mpm.ModifierType.FullName == opm.ModifierType.FullName && areParamTypesEquivalent(mpm.ElementType, opm.ElementType, genMap);

		if (mp is ArrayType mpa && op is ArrayType opa)
			return mpa.Rank == opa.Rank && areParamTypesEquivalent(mpa.ElementType, opa.ElementType, genMap);

		if (mp is TypeSpecification mpe && op is TypeSpecification ope)
			return areParamTypesEquivalent(mpe.ElementType, ope.ElementType, genMap);

		return mp.MetadataToken == op.MetadataToken || mp.Resolve() == op.Resolve() || (mp.IsGenericParameter && genMap.ContainsKey(mp) && areParamTypesEquivalent(genMap[mp], op, genMap));
	}

	private static string encodeGenericParameter(GenericParameter gp) => (gp.DeclaringType is null ? "``" : "`") + (gp.DeclaringType?.GenericParameters ?? gp.DeclaringMethod?.GenericParameters).Single(g => g.Name == gp.Name).Position;

	private static IEnumerable<string> encodeMethodParams(ICollection<ParameterDefinition> mp)
	{
		if (mp.Count == 0)
		{
			yield return string.Empty;
			yield break;
		}

		var sl = emptyStringEnumerable;
		foreach (var pl in mp.Select(p => encodeTypeName(p.ParameterType)))
			sl = sl.SelectMany(s => pl.Select(p => s + "," + p));

		foreach (string s in sl)
			yield return "(" + s.Substring(1) + ")";
	}

	private static IEnumerable<string> encodeMemberName(string mn)
	{
		string en = mn.Replace('.', '#').Replace('<', '{').Replace('>', '}');
		yield return en;

		if (en.Contains(','))
			yield return en.Replace(',', '@');
	}

	private static IEnumerable<string> encodeGenericInstance(GenericInstanceType gi, string? suffix)
	{
		var type = gi.ElementType;
		var args = gi.GenericArguments;
		int consumed = 0;

		string name = null!;
		while (true)
		{
			int idx = type.Name.LastIndexOf('`');
			if (idx >= 0)
			{
				int cnt = int.Parse(type.Name.Substring(idx + 1));
				name = "{" + string.Join(",", args.Skip(args.Count - consumed - cnt).Take(cnt).Select(ga => encodeTypeName(ga).First())) + "}" + name;
				consumed += cnt;
			}

			name = (idx >= 0 ? type.Name.Substring(0, idx) : type.Name) + name;
			if (!type.IsNested)
			{
				if (!string.IsNullOrEmpty(type.Namespace))
					name = type.Namespace + "." + name;

				break;
			}

			name = "." + name;
			type = type.DeclaringType;
		}

		yield return name + suffix;
	}

	private static IEnumerable<string> encodeTypeName(TypeReference tr, string? suffix = null)
	{
		if (tr is GenericParameter gp)
		{
			yield return encodeGenericParameter(gp) + suffix;
			yield break;
		}

		if (tr is not TypeSpecification ts)
		{
			// The only remaining special type we should be able to hit here is a nested type.  Rather than
			// unwind that properly, we can just fix up the name (IL uses / separator, so replace with .).
			yield return tr.FullName.Replace('/', '.') + suffix;
			yield break;
		}

		var te = ts.ElementType;
		foreach (var nm in ts switch {
			{ IsPointer:     true } => encodeTypeName(te, "*" + suffix),
			{ IsByReference: true } => encodeTypeName(te, "@" + suffix),
			{ IsPinned:      true } => encodeTypeName(te, "^" + suffix),
			ArrayType           at  => encodeTypeName(te, "[" + string.Join(",", at.Dimensions.Select(d => d.IsSized ? d.LowerBound?.ToString() + ":" + d.UpperBound?.ToString() : null)) + "]" + suffix),
			IModifierType       mt  => encodeTypeName(te, suffix).Concat(encodeTypeName(te, (ts.IsRequiredModifier ? "|" : "!") + mt.ModifierType.FullName + suffix)),
			FunctionPointerType fp  => encodeTypeName(fp.ReturnType).SelectMany(t => encodeMethodParams(fp.Parameters).Select(p => "=FUNC:" + t + p + suffix)),
			GenericInstanceType gi  => encodeGenericInstance(gi, suffix),
			_                       => encodeTypeName(te, suffix)
		}) yield return nm;
	}

	internal sealed class RefAssemblyResolver : IAssemblyResolver
	{
		private readonly Dictionary<string, AssemblyDefinition> cache = new(StringComparer.Ordinal);

		public static RefAssemblyResolver Create(string mainAssembly, string[] refAssemblies)
		{
			var resolver = new RefAssemblyResolver();
			var rparams = new ReaderParameters { AssemblyResolver = resolver };

			foreach (string assemblyFile in refAssemblies.Concat(new[] { mainAssembly }))
			{
				var assembly = AssemblyDefinition.ReadAssembly(assemblyFile, rparams);
				resolver.cache[assembly.FullName] = assembly;
			}

			return resolver;
		}

		private RefAssemblyResolver() { }

		public AssemblyDefinition Resolve(AssemblyNameReference name)
		{
			static bool isCompatibleName(AssemblyNameReference name, AssemblyNameReference cname) =>
				cname.Name == name.Name && cname.PublicKeyToken.SequenceEqual(name.PublicKeyToken) && cname.Version >= name.Version;

			if (!cache.TryGetValue(name.FullName, out var match))
				cache[name.FullName] = match = cache.Values.FirstOrDefault(c => isCompatibleName(name, c.Name));

			return match ?? throw new AssemblyResolutionException(name);
		}

		public AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters _) => Resolve(name);

		public void Dispose()
		{
			foreach (var asm in cache.Values)
				asm?.Dispose();

			cache.Clear();
		}
	}
}
