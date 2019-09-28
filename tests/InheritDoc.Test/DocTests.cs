#pragma warning disable CA2231,CA1815,CA1052,CS0659,IDE0060 // various warnings about Equals/GetHashCode/Operator== overloads and unused parameters

using System;
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

/// <summary>Interface IG</summary>
public interface IG<T>
{
	/// <summary>Method M</summary>
	/// <typeparam name="U">TypeParam U</typeparam>
	/// <param name="p">Param p</param>
	/// <returns>Return <typeparamref name="U" />[]</returns>
	unsafe U[] M<U>(U* p) where U : unmanaged;
}

/// <summary>Class G</summary>
/// <typeparam name="T">TypeParam T</typeparam>
public abstract class G<T> where T : class
{
	/// <summary>Method M</summary>
	/// <param name="t">Param t</param>
	/// <returns>Return <typeparamref name="T" /> <paramref name="t" /></returns>
	public virtual T M(in T t) => default;

	/// <summary>Property P</summary>
	/// <value>Value <typeparamref name="T" /></value>
	public abstract T P { get; }
}

//
// Inherited Types
//

/// <inheritdoc />
public interface IY : IX
{
	/// <summary>Method Y</summary>
	void Y();
}

/// <summary>Class B</summary>
public class B : IY
{
	internal const string T_ID = nameof(B);
	internal const string M_ID_X = T_ID + "." + nameof(IX) + "#" + nameof(IX.X);
	internal const string M_ID_O = T_ID + "." + nameof(O) + "(" + nameof(System) + "." + nameof(String) + "[])";

	void IX.X() { }

	/// <inheritdoc />
	public virtual void Y() { }

	/// <summary>Overloaded Method O</summary>
	/// <param name="s">Param s</param>
	/// <param name="t">Param t</param>
	/// <param name="u">Param u</param>
	public static void O(string[] s, string t, string u) { }

	/// <inheritdoc cref="O(string[], string, string)" />
	public static void O(string[] s) { }
}

/// <inheritdoc />
public class C : B, IZ
{
	internal new const string T_ID = nameof(C);
	internal const string M_ID_Y = T_ID + "." + nameof(Y);
	internal const string M_ID_M = T_ID + "." + nameof(M) + "``1";
	internal const string M_ID_N = T_ID + "." + nameof(N) + "(" + nameof(System) + "." + nameof(Int32) + ")";
	internal const string P_ID = T_ID + "." + nameof(IZ) + "#" + nameof(IZ.P);
	internal const string E_ID = T_ID + "." + nameof(IZ) + "#" + nameof(IZ.E);

	/// <inheritdoc />
	public override void Y() { }

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
	internal const string M_ID_EqualsExplcit = T_ID + "." + nameof(System) + "#" + nameof(IEquatable<D>) + "{" + nameof(D) + "}#" + nameof(Equals) + "(" + nameof(D) + ")";
	internal const string M_ID_EqualsOverride = T_ID + "." + nameof(Equals) + "(" + nameof(System) + "." + nameof(Object) + ")";

	bool IEquatable<D>.Equals(D other) => default;

	/// <inheritdoc />
	public override bool Equals(object obj) => default;
}

/// <inheritdoc />
public class GGI : GG<string>
{
	internal const string T_ID = nameof(GGI);
	internal const string M_ID = T_ID + "." + nameof(M) + "(" + nameof(System) + "." + nameof(String) + "@)";
	internal const string P_ID = T_ID + "." + nameof(P);

	/// <inheritdoc />
	public GGI() { }

	/// <inheritdoc />
	public override string M(in string x) => default;

	/// <inheritdoc />
	public override string P => default;
}

/// <inheritdoc />
public class GG<U> : G<U> where U : class
{
	/// <summary>Constructor GG</summary>
	public GG() { }

	/// <inheritdoc />
	public override U P => default;
}

/// <inheritdoc />
public class GI : G<string>
{
	internal const string T_ID = nameof(GI);
	internal const string M_ID = T_ID + "." + nameof(M) + "(" + nameof(System) + "." + nameof(String) + "@)";
	internal const string P_ID = T_ID + "." + nameof(P);

	/// <inheritdoc />
	public override string M(in string s) => default;

	/// <inheritdoc />
	public override string P => default;
}

/// <inheritdoc />
public class GX<U> : Lazy<U>
{
	internal const string T_ID = nameof(GX<U>) + "`1";
	internal const string M_ID_GetHashCode = T_ID + "." + nameof(GetHashCode);

	/// <inheritdoc />
	public override int GetHashCode() => default;
}

/// <inheritdoc />
public class GXI : Lazy<string>, IEquatable<string>
{
	internal const string T_ID = nameof(GXI);

	/// <inheritdoc />
	public bool Equals(string other) => default;
}

/// <inheritdoc />
public class GIG<TT> : GG<TT>, IG<TT> where TT : class
{
	internal const string T_ID = nameof(GIG<TT>) + "`1";
	internal const string M_ID = T_ID + "." + nameof(IG<TT>) + "{" + nameof(TT) + "}#" + nameof(M) + "``1(``0*)";
	internal const string M_ID_ctor = T_ID + ".#ctor";

	/// <inheritdoc />
	public GIG() { }

	unsafe MT[] IG<TT>.M<MT>(MT* mtp) => default;
}

/// <inheritdoc />
public class GII : IG<string>
{
	internal const string T_ID = nameof(GII);
	internal const string M_ID = T_ID + "." + nameof(M) + "``1(``0*)";

	/// <inheritdoc />
	unsafe public T[] M<T>(T* tp) where T : unmanaged => default;
}

/// <summary>
///     Class W
///     <see cref="B" />
///     <see cref="C" />
/// </summary>
public class W
{
	internal const string T_ID = nameof(W);
}
