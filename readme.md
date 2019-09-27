
InheritDoc
==========

This [MSBuild Task]( https://docs.microsoft.com/en-us/visualstudio/msbuild/msbuild-tasks) takes a different approach from other documentation post-processing tools.  By integrating with MSBuild, it has access to the exact arguments passed to the compiler, including assembly references and the output assembly and XML documentation file paths.  As it processes `<inheritdoc />` elements, it is able to more accurately resolve base types whether they come from the target framework, referenced NuGet packages, or project references.  This more accurate resolution of references means it can be more clever about mapping documentation from base types and members to yours.  For example, it can identify when you change the name of a method parameter from the base type’s definition and update the documentation accordingly.

How it Works
------------

The InheritDoc `Task` inserts itself between the `CoreCompile` and `CopyFilesToOutputDirectory` steps in the MSBuild process, making a backup copy of the documentation file output from the compiler and then processing it to replace `<inheritdoc />` tags.  The output of InheritDoc is then used for the remainder of your build process.  The XML documentation in your output (bin) folder will be the processed version.  If you have further steps, such as building a NuGet package, the updated XML file will used in place of the original, meaning `<inheritdoc />` Just Works™.

This enhances the new support for `<inheritdoc />` in Roslyn (available starting in the [VS 16.4 preview](https://docs.microsoft.com/en-us/visualstudio/releases/2019/release-notes-preview#net-productivity-164P1) builds), making it available to all downstream consumers of your documentation.  When using tools such as [DocFX](https://dotnet.github.io/docfx/spec/triple_slash_comments_spec.html#inheritdoc), you will no longer be [subject](https://github.com/dotnet/docfx/issues/3699) to [limitations](https://github.com/dotnet/docfx/issues/1306) around `<inheritdoc />` tag usage because the documentation will already have those tags replaced with the upstream docs.

How to Use It
-------------

1) Add some `<inheritdoc />` tags to your XML documentation comments.

    This tool’s handling of `<inheritdoc />` tags is based on the draft [design document]( https://github.com/dotnet/csharplang/blob/812e220fe2b964d17f353cb684aa341418618b6e/proposals/inheritdoc.md) used for the new prototype Roslyn support, which is in turn based on the `<inheritdoc />` support in [Sandcastle Help File Builder]( https://ewsoftware.github.io/XMLCommentsGuide/html/86453FFB-B978-4A2A-9EB5-70E118CA8073.htm#TopLevelRules) (SHFB).

2) Add the [SauceControl.InheritDoc](https://www.nuget.org/packages/SauceControl.InheritDoc) NuGet package reference to your project.

    This is a design-time only dependency; it will not be deployed with or referenced by your compiled app/library.

3) There is no 3.

    Once the package reference is added to your project, the XML docs will be processed automatically with each build.

Note: InheritDoc can be enabled or disabled by setting the `InheritDocEnabled` MSBuild property in your project.  This may be useful if you wish to skip the overhead of running InheritDoc on debug builds, for example.

```XML
<PropertyGroup Condition="'$(Configuration)'=='Debug'">
    <InheritDocEnabled>false</InheritDocEnabled>
</PropertyGroup>
```

Some Examples
-------------

Consider the following C#

```C#
/// <summary>Interface IX</summary>
public interface IX
{
    /// <summary>Method X</summary>
    void X();
}

/// <inheritdoc />
public interface IY : IX
{
    /// <summary>Method Y</summary>
    void Y();
}

/// <summary>Class A</summary>
public class A : IY
{
    void IX.X() { }

    /// <inheritdoc />
    public virtual void Y() { }

    /// <summary>Method M</summary>
    /// <typeparam name="T">TypeParam T</typeparam>
    /// <param name="t">Param t</param>
    /// <returns>Return value <paramref name="t" /> of type <typeparamref name="T" /></returns>
    public virtual void M<T>(T t) { }

    /// <summary>Overloaded Method O</summary>
    /// <param name="s">Param s</param>
    /// <param name="t">Param t</param>
    /// <param name="u">Param u</param>
    public static void O(string[] s, string t, string u) { }

    /// <inheritdoc cref="O(string[], string, string)" />
    public static void O(string[] s) { }
}

/// <inheritdoc />
public class B : A
{
    /// <inheritdoc />
    public override void Y() { }

    /// <inheritdoc />
    public override void M<TValue>(TValue value) { }
}
```

Once processed, the output XML documentation will look like this (results abbreviated and comments added manually to highlight features)

```XML
<member name="T:IX">
    <summary>Interface IX</summary>
</member>
<member name="M:IX.X">
    <summary>Method X</summary>
</member>
<member name="T:IY">
    <summary>Interface IX</summary> <!-- inherited from IX -->
</member>
<member name="M:IY.Y">
    <summary>Method Y</summary>
</member>
<member name="T:A">
    <summary>Class A</summary>
</member>
<member name="M:A.Y">
    <summary>Method Y</summary> <!-- inherited from IY -->
</member>
<member name="M:A.M``1(``0)">
    <summary>Method M</summary>
    <typeparam name="T">TypeParam T</typeparam>
    <param name="t">Param t</param>
    <returns>Return value <paramref name="t" /> of type <typeparamref name="T" /></returns>
</member>
<member name="M:A.O(System.String[],System.String,System.String)">
    <summary>Overloaded Method O</summary>
    <param name="s">Param s</param>
    <param name="t">Param t</param>
    <param name="u">Param u</param>
</member>
<member name="M:A.O(System.String[])"> 
    <summary>Overloaded Method O</summary> <!-- inherited (by cref) from overload -->
    <param name="s">Param s</param>
    <!-- unused parameters automatically removed -->
</member>
<member name="T:B">
    <summary>Class A</summary> <!-- inherited from A -->
</member>
<member name="M:B.Y">
    <summary>Method Y</summary> <!-- inherited from IY (recursively through A) -->
</member>
<member name="M:B.M``1(``0)">
    <summary>Method M</summary> <!-- inherited from A -->
    <typeparam name="TValue">TypeParam T</typeparam> <!-- typeparam updated to match override's name -->
    <param name="value">Param t</param> <!-- param updated to match override's name -->
    <returns>Return value <paramref name="value" /> of type <typeparamref name="TValue" /></returns> <!-- paramref and typeparamref updated as well -->
</member>
<member name="M:A.IX#X"> <!-- explicit interface implementation doc added automatically -->
    <summary>Method X</summary>
</member>
```

Advanced Examples
-----------------

Although the .NET compilers don't allow adding namespace documentation comments, some tools (including SHFB) have a [convention](https://stackoverflow.com/questions/793210/xml-documentation-for-a-namespace) for declaring them in code. InheritDoc follows this convention.

Note that both the `[CompilerGenerated]` attribute and the class name `NamespaceDoc` are required by InheritDoc.

```C#
namespace InheritDocTest
{
    /// <summary>Namespace InheritDocTest</summary>
    [CompilerGenerated] internal class NamespaceDoc { }
}
```

Will output:

```XML
<member name="N:InheritDocTest">
    <summary>Namespace InheritDocTest</summary>
</member>
```


InheritDoc also supports the `path` attribute defined in the Roslyn draft design doc, which is analogous to the `select` attribute in SHFB.

In this example, we define a custom Exception class that for some reason doesn't inherit from `System.Exeption` and yet we want to use its documentation anyway.

```C#
public class ExceptionForSomeReasonNotInheritedFromSystemException
{
    /// <inheritdoc cref="Exception(string)" />
    /// <param name="theErrorMessage"><inheritdoc cref="Exception(string)" path="/param[@name='message']/node()" /></param>
    ExceptionForSomeReasonNotInheritedFromSystemException(string theErrorMessage) { }
}
```

Outputs:

```XML
<member name="M:ExceptionForSomeReasonNotInheritedFromSystemException.#ctor(System.String)">
    <summary>Initializes a new instance of the <see cref="T:System.Exception"></see> class with a specified error message.</summary>
    <param name="theErrorMessage">The message that describes the error.</param>
</member>
```

Notice the `message` `param` element was excluded automatically because there was no matching parameter on the target constructor, however with a nested `<inheritdoc />` and a custom selector, we were able to extract the contents from that `param` element into a new one with a matching name.

Known Issues
------------

### Bad NETStandard Docs

When targeting `netstandard2.0`, if you attempt to inherit docs from any types/members in the framework you will receive errors from InheritDoc related to malformed XML in `netstandard.xml` and then subsequent errors for each member whose documentation could not be found.  All shipping versions of `NETStandard.Library` v2.0 have bad XML documentation.  This has been corrected in `netstandard2.1` builds, but since InheritDoc resolves documentation based on assembly references at compile time, it will find the bad docs for `netstandard2.0` targets.

If this displeases you, you may register your discontent by commenting on the [associated issue](https://github.com/dotnet/standard/issues/1527) in the NETStandard repo.  In the meantime, you can either replace the `netstandard.xml` file in your NuGet package cache with a corrected file, or you can configure InheritDoc to load additional documentation file(s) that include the types referenced.  For example:

```XML
<ItemGroup Condition="'$(TargetFramework)'=='netstandard2.0'">
    <InheritDocReference Include="\path\to\netstandard2.1.xml" />
</ItemGroup>
```

You may also use `InheritDocReference` to add additional documentation files for types referenced in `cref` attributes that are not part of the project's reference assemblies or that could not be resolved automatically.

### Roslyn Analyzer Bug

If you attempt to build the test project from this repo using the current (as of SDK 3.0.100) version of Roslyn, it may fail with an exception related to resolving explicit interface implementations.  There are some tricky ones in the test cases.  This issue has been fixed in preview versions of Roslyn, so either use a VS 16.4 Preview build or a .NET Core SDK 5.0 preview build to get the updated version.

Troubleshooting
---------------

Because this MSBuild Task is supposed to Just Work™, there is very little configuration to do.  If it doesn't work for you, check the detailed output from MSBuild (e.g. `dotnet build -v detailed`) and look for `InheritDoc` in the logs.  You should have some info about why the task failed to run in there.  Issue reports are, of course, welcome with good repro steps.
