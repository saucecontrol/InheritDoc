// Copyright © Clinton Ingram and Contributors.  Licensed under the MIT License.

// <auto-generated /> (not really, but this disables analyzer warnings)

using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace InheritDocTest
{
	/// <summary>Namespace InheritDocTest</summary>
	[CompilerGenerated] internal class NamespaceDoc { }
}

//
// Base Types
//

/// <summary>Interface IX</summary>
public interface IX
{
	/// <summary>Method X</summary>
	void X();
}

/// <summary>Interface IZ</summary>
public interface IZ
{
	/// <summary>Property P</summary>
	int P { get; }

	/// <summary>Event E</summary>
	event EventHandler E;
}

/// <summary>Interface IG <typeparamref name="TG"/></summary>
/// <typeparam name="TG">TypeParam TG</typeparam>
public interface IG<TG>
{
	/// <summary>Method M</summary>
	/// <typeparam name="U">TypeParam U</typeparam>
	/// <param name="p">Param p</param>
	/// <returns>Return <typeparamref name="U" />[]</returns>
	unsafe U[] M<U>(U* p) where U : unmanaged;

	/// <summary>Nested Interface IG&lt;TG&gt;.IN</summary>
	/// <typeparam name="TM">TypeParam TM</typeparam>
	public interface IN<TM>
	{
		/// <summary>Method M <typeparamref name="TM" /></summary>
		/// <param name="p">Param p</param>
		public void M(IN<TM> p);
	}
}

/// <summary>Class G <typeparamref name="T"/></summary>
/// <typeparam name="T">TypeParam T</typeparam>
public abstract class G<T> where T : class
{
	/// <summary>Method M</summary>
	/// <param name="t">Param t</param>
	/// <returns>Return <typeparamref name="T" /> <paramref name="t" /></returns>
	public virtual T M(in T t) => default;

	/// <summary>Property this[]</summary>
	/// <param name="idx">Param idx</param>
	/// <value>Value <typeparamref name="T" /></value>
	public abstract T this[int idx] { get; }
}

//
// Inherited Types
//

/// <inheritdoc />
public interface IY : IX
{
	/// <summary>Method Y</summary>
	/// <param name="p">Param p</param>
	void Y(int[,] p);
}

/// <summary>Class B</summary>
public class B : IY
{
	internal const string T_ID = nameof(B);
	internal const string T_ID_ND = T_ID + "." + nameof(ND);
	internal const string M_ID_X = T_ID + "." + nameof(IX) + "#" + nameof(IX.X);
	internal const string M_ID_O = T_ID + "." + nameof(O) + "(" + nameof(System) + "." + nameof(String) + "[])";
	internal const string M_ID_P = T_ID + "." + nameof(P);

	void IX.X() { }

	/// <inheritdoc />
	public virtual void Y(int[,] p) { }

	/// <summary>Overloaded Method O</summary>
	/// <param name="s">Param s</param>
	/// <param name="t">Param t</param>
	/// <param name="u">Param u</param>
	/// <returns>Return</returns>
	public static bool O(string[] s, string t, string u) => default;

	/// <inheritdoc cref="O(string[], string, string)" />
	public static void O(string[] s) { }

	/// <summary>Method P</summary>
	protected internal void P() { }

	/// <summary>Class B.NC</summary>
	public class NC { }

	/// <inheritdoc />
	public class ND : NC { }
}

/// <inheritdoc />
public class C : B, IZ
{
	internal new const string T_ID = nameof(C);
	internal const string M_ID_Y = T_ID + "." + nameof(Y) + "(" + nameof(System) + "." + nameof(Int32) + "[0:,0:])";
	internal const string M_ID_M = T_ID + "." + nameof(M) + "``1";
	internal const string M_ID_N = T_ID + "." + nameof(N) + "(" + nameof(System) + "." + nameof(Int32) + ")";
	internal const string P_ID = T_ID + "." + nameof(IZ) + "#" + nameof(IZ.P);
	internal const string E_ID = T_ID + "." + nameof(IZ) + "#" + nameof(IZ.E);
	internal const string F_ID = T_ID + "." + nameof(F);

	/// <inheritdoc cref="String.Empty" />
	public string F;

	/// <inheritdoc />
	public override void Y(int[,] q) { }

	int IZ.P => default;

	event EventHandler IZ.E { add { } remove { } }

	/// <inheritdoc cref="Exception.GetType" />
	/// <typeparam name="T"><inheritdoc cref="IEnumerable{T}" /></typeparam>
	public virtual void M<T>() { }

	/// <inheritdoc cref="Exception(string)" />
	/// <param name="i"><inheritdoc cref="Exception(string)" path="/param[@name='message']/node()" /></param>
	public virtual void N(int i) { }
}

/// <inheritdoc />
public struct D : IEquatable<D>
{
	internal const string T_ID = nameof(D);
	internal const string M_ID_EqualsExplicit = T_ID + "." + nameof(System) + "#" + nameof(IEquatable<D>) + "{" + nameof(D) + "}#" + nameof(Equals) + "(" + nameof(D) + ")";
	internal const string M_ID_EqualsOverride = T_ID + "." + nameof(Equals) + "(" + nameof(System) + "." + nameof(Object) + ")";

	bool IEquatable<D>.Equals(D other) => default;

	/// <inheritdoc />
	public override bool Equals(object obj) => default;

	/// <inheritdoc />
	public override int GetHashCode() => default;
}

/// <summary>Enum E</summary>
public enum E
{
	/// <summary>EM</summary>
	EM,
	/// <inheritdoc cref="EM" />
	EI,
	/// <inheritdoc cref="AttributeTargets.Enum" />
	ES
}

/// <inheritdoc />
public class GGI : GG<string[]>
{
	new internal const string T_ID = nameof(GGI);
	internal const string M_ID = T_ID + "." + nameof(M) + "(" + nameof(System) + "." + nameof(String) + "[]@)";
	internal const string P_ID = T_ID + ".Item(" + nameof(System) + "." + nameof(Int32) + ")";

	/// <inheritdoc />
	public GGI() { }

	/// <inheritdoc />
	public override string[] M(in string[] x) => default;

	/// <inheritdoc />
	public override string[] this[int i] => default;
}

/// <inheritdoc />
/// <typeparam name="V"><inheritdoc cref="IComparable{T}" path="/typeparam[@name='T']/node()" /></typeparam>
#pragma warning disable 1712
public class GX<V, U> : Lazy<U>, IEquatable<Lazy<U>>, IComparable<V>
#pragma warning restore 1712
{
	/// <inheritdoc />
	public override int GetHashCode() => default;

	/// <inheritdoc />
	public bool Equals(Lazy<U> other) => default;

	/// <inheritdoc />
	/// <param name="other"><typeparamref name="V"/></param>
	public int CompareTo(V other) => default;
}

/// <inheritdoc />
/// <typeparam name="T">TypeParam T</typeparam>
public class GXI<T> : Lazy<string[]>, IEquatable<string>
{
	internal const string T_ID = nameof(GXI<T>) + "`1";
	internal const string M_ID_OpImplicit = T_ID + ".op_Implicit(" + nameof(GXI<T>) + "{`0})~" + nameof(System) + "." + nameof(String);

	/// <inheritdoc />
	public bool Equals(string o) => default;

	/// <inheritdoc cref="Equals(string)" />
	public static implicit operator string(GXI<T> o) => default;
}

/// <inheritdoc />
public class GIG<TT> : GG<TT>, IG<TT>, IG<TT>.IN<TT> where TT : class
{
	new internal const string T_ID = nameof(GIG<TT>) + "`1";
	internal const string M_ID_MImplicit = T_ID + "." + nameof(M) + "(" + nameof(IG<TT>) + "{`0}." + nameof(IG<TT>.IN<TT>) + "{`0})";
	internal const string M_ID_MExplicit = T_ID + "." + nameof(IG<TT>) + "{" + nameof(TT) + "}#" + nameof(IG<TT>.M) + "``1(``0*)";
	internal const string M_ID_ctor = T_ID + ".#ctor";

	/// <inheritdoc />
	public GIG() { }

	/// <inheritdoc />
	public void M(IG<TT>.IN<TT> q) { }

	unsafe MT[] IG<TT>.M<MT>(MT* mtp) => default;
}

/// <inheritdoc cref="IG{TG}" />
public class GIS<TT> : GG<TT>, IG<TT> where TT : class
{
	new internal const string T_ID = nameof(GIS<TT>) + "`1";
	internal const string M_ID = T_ID + "." + nameof(M) + "``1(``0*)";

	/// <inheritdoc />
	unsafe public virtual MT[] M<MT>(MT* mtp) where MT : unmanaged => default;
}

/// <inheritdoc />
public class GG<U> : G<U> where U : class
{
	internal const string T_ID = nameof(GG<U>) + "`1";
	internal const string P_ID_this = T_ID + ".Item(" + nameof(System) + "." + nameof(Int32) + ")";

	/// <summary>Constructor GG</summary>
	public GG() { }

	/// <inheritdoc />
	public override U this[int i] => default;
}

/// <inheritdoc />
public class GII : IG<string>
{
	internal const string T_ID = nameof(GII);

	/// <inheritdoc />
	private GII() { }

	/// <inheritdoc />
	unsafe public T[] M<T>(T* tp) where T : unmanaged => default;
}

/// <inheritdoc />
/// <param name="s">Param s</param>
public class PC(string s) : GGI
{
	new internal const string T_ID = nameof(PC);
	internal const string M_ID_ctor = T_ID + ".#ctor(" + nameof(System) + "." + nameof(String) + ")";

	string S => s;
}

/// <summary>
///     Class W
///     <see cref="B" />
///     <see cref="C" />
/// </summary>
public class W
{
	/// <summary>Internal Field T_ID</summary>
	internal const string T_ID = nameof(W);
	internal const string F_ID = T_ID + "." + nameof(T_ID);
	internal const string M_ID_NotInherited = T_ID + "." + nameof(MNotInherited);
	internal const string P_ID_NotInherited = T_ID + "." + nameof(PNotInherited);

	/// <inheritdoc />
	public void MNotInherited() { }

	/// <inheritdoc />
	[GeneratedCode("SG", "1")]
	public int PNotInherited { get; set; }

	/// <summary>M managed</summary>
	/// <param name="ptr">fnptr</param>
	public unsafe void M(delegate*<void> ptr) { }

	/// <inheritdoc />
	public override string ToString() => default;
}

/// <inheritdoc cref="GIG{TT}" />
/// <typeparam name="U">U</typeparam>
public class GB<U> : B { }

/// <inheritdoc />
[CompilerGenerated]
public interface IG { }

//
// Tricky Generic Case
//

/// <inheritdoc />
public class ImplementsIDictionary : IDictionary<string, string>
{
	internal const string T_ID = nameof(ImplementsIDictionary);
	internal const string P_ID_Keys = T_ID + ".System#Collections#Generic#IDictionary{System#String,System#String}#Keys";
	internal const string M_ID_Add = T_ID + ".System#Collections#Generic#IDictionary{System#String,System#String}#Add(System.String,System.String)";

	string IDictionary<string, string>.this[string key] { get => default; set => _ = value; }
	ICollection<string> IDictionary<string, string>.Keys => default;
	ICollection<string> IDictionary<string, string>.Values => default;
	int ICollection<KeyValuePair<string, string>>.Count => default;
	bool ICollection<KeyValuePair<string, string>>.IsReadOnly => default;

	void IDictionary<string, string>.Add(string key, string value) { }
	void ICollection<KeyValuePair<string, string>>.Add(KeyValuePair<string, string> item) { }
	void ICollection<KeyValuePair<string, string>>.Clear() { }
	bool ICollection<KeyValuePair<string, string>>.Contains(KeyValuePair<string, string> item) => default;
	bool IDictionary<string, string>.ContainsKey(string key) => default;
	void ICollection<KeyValuePair<string, string>>.CopyTo(KeyValuePair<string, string>[] array, int arrayIndex) { }
	IEnumerator<KeyValuePair<string, string>> IEnumerable<KeyValuePair<string, string>>.GetEnumerator() => default;
	IEnumerator IEnumerable.GetEnumerator() => default;
	bool IDictionary<string, string>.Remove(string key) => default;
	bool ICollection<KeyValuePair<string, string>>.Remove(KeyValuePair<string, string> item) => default;
	bool IDictionary<string, string>.TryGetValue(string key, out string value) => throw new NotImplementedException();
}

//
// Internal Type
//

/// <inheritdoc />
internal class ImplementsICollection : ICollection<string>
{
	internal const string T_ID = nameof(ImplementsICollection);
	internal const string I_ID = nameof(System) + "#" + nameof(System.Collections) + "#" + nameof(System.Collections.Generic) + "#" + nameof(ICollection<string>) + "{" + nameof(System) + "#" + nameof(String) + "}";
	internal const string M_ID_ADD = T_ID + "." + nameof(Add) + "(" + nameof(System) + "." + nameof(String) + ")";
	internal const string M_ID_CLEAR = T_ID + "." + I_ID + "#" + nameof(ICollection<string>.Clear);

	int ICollection<string>.Count => default;
	bool ICollection<string>.IsReadOnly => default;

	/// <inheritdoc />
	public void Add(string item) { }

	void ICollection<string>.Clear() { }
	bool ICollection<string>.Contains(string item) => default;
	void ICollection<string>.CopyTo(string[] array, int arrayIndex) { }
	bool ICollection<string>.Remove(string item) => default;

	IEnumerator<string> IEnumerable<string>.GetEnumerator() => default;
	IEnumerator IEnumerable.GetEnumerator() => default;
}
