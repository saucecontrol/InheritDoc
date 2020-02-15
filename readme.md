[![NuGet](https://buildstats.info/nuget/SauceControl.InheritDoc)](https://www.nuget.org/packages/SauceControl.InheritDoc/) [![Build Status](https://dev.azure.com/saucecontrol/InheritDoc/_apis/build/status/saucecontrol.InheritDoc?branchName=master)](https://dev.azure.com/saucecontrol/InheritDoc/_build/latest?definitionId=2&branchName=master) [![Test Results](https://img.shields.io/azure-devops/tests/saucecontrol/InheritDoc/2?logo=azure-devops)](https://dev.azure.com/saucecontrol/InheritDoc/_build/latest?definitionId=2&branchName=master)

InheritDoc
==========

This [MSBuild Task]( https://docs.microsoft.com/en-us/visualstudio/msbuild/msbuild-tasks) automatically replaces `<inheritdoc />` tags in your .NET XML documentation with the actual inherited docs.  By integrating with MSBuild, this tool has access to the exact arguments passed to the compiler -- including all assembly references -- making it both simpler and more capable than other documentation post-processing tools.  As it processes `<inheritdoc />` elements, it is able to more accurately resolve base types whether they come from the target framework, referenced NuGet packages, or project references.  This means it can be more clever about mapping documentation from base types and members to yours.  For example, it can identify when you change the name of a method parameter from the base type’s definition and update the documentation accordingly.  It can also remove documentation for non-public types/members to reduce the size of your published XML docs.

How to Use It
-------------

1) Add some `<inheritdoc />` tags to your XML documentation comments.

    This tool’s handling of `<inheritdoc />` tags is based on the draft [design document]( https://github.com/dotnet/csharplang/blob/812e220fe2b964d17f353cb684aa341418618b6e/proposals/inheritdoc.md) used for the new prototype Roslyn support, which is in turn based on the `<inheritdoc />` support in [Sandcastle Help File Builder]( https://ewsoftware.github.io/XMLCommentsGuide/html/86453FFB-B978-4A2A-9EB5-70E118CA8073.htm#TopLevelRules) (SHFB).

2) Add the [SauceControl.InheritDoc](https://www.nuget.org/packages/SauceControl.InheritDoc) NuGet package reference to your project.

    This is a development-only dependency; it will not be deployed with or referenced by your compiled app/library.

3) Build your project as you normally would.

    The XML docs will be post-processed automatically with each build, whether you use Visual Studio, dotnet CLI, or anything else that hosts the MSBuild engine.

How it Works
------------

The InheritDoc task inserts itself between the `CoreCompile` and `CopyFilesToOutputDirectory` steps in the MSBuild process, making a backup copy of the documentation file output from the compiler and then processing it to replace `<inheritdoc />` tags.  It uses the arguments passed to the compiler to find your assembly, the XML doc file, and all referenced assemblies.  The output of InheritDoc is then used for the remainder of your build process.  The XML documentation in your output (bin) folder will be the processed version.  If you have further steps, such as building a NuGet package, the updated XML file will used in place of the original, meaning `<inheritdoc />` Just Works™.

This enhances the new support for `<inheritdoc />` in Roslyn (available starting in [VS 16.4](https://docs.microsoft.com/en-us/visualstudio/releases/2019/release-notes-preview#net-productivity-164P1)), making it available to all downstream consumers of your documentation.  When using tools such as [DocFX](https://dotnet.github.io/docfx/spec/triple_slash_comments_spec.html#inheritdoc), you will no longer be [subject](https://github.com/dotnet/docfx/issues/3699) to [limitations](https://github.com/dotnet/docfx/issues/1306) around `<inheritdoc />` tag usage because the documentation will already have those tags replaced with the upstream docs.

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
    /// <returns>
    /// Returns value <paramref name="t" />
    /// of type <typeparamref name="T" />
    /// </returns>
    public virtual T M<T>(T t) => t;

    /// <summary>Method P</summary>
    private void P() { }

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
    public override TValue M<TValue>(TValue value) => value;
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
    <returns>
    Return value <paramref name="t" />
    of type <typeparamref name="T" />
    </returns>
</member>
<!-- private method A.P doc removed -->
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
    <returns>
    Returns value <paramref name="value" /> <!-- paramref and typeparamref updated as well -->
    of type <typeparamref name="TValue" />
    </returns>
</member>
<member name="M:A.IX#X"> <!-- explicit interface implementation doc added automatically -->
    <summary>Method X</summary>
</member>
```

Advanced Examples
-----------------

Although the .NET compilers [don't allow](https://github.com/dotnet/csharplang/issues/315) adding namespace documentation comments, some tools (including SHFB) have a [convention](https://stackoverflow.com/a/52381674/4926931) for declaring them in code. InheritDoc follows this convention.

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

Notice the `param` element for `message` was excluded automatically because there was no matching parameter on the target constructor, however with a nested `<inheritdoc />` and a custom selector, we were able to extract the contents from that `param` element into a new one with the correct name.

Configuration
-------------

#### Enabling/Disabling InheritDoc

InheritDoc is enabled by default for all normal builds.  It can be disabled by setting the `InheritDocEnabled` MSBuild property in your project.  This may be used if you wish to skip the overhead of running InheritDoc on debug builds, for example.

```XML
<PropertyGroup Condition="'$(Configuration)'=='Debug'">
    <InheritDocEnabled>false</InheritDocEnabled>
</PropertyGroup>
```

The same can be achieved by conditionally incuding the NuGet package.

```XML
<ItemGroup Condition="'$(Configuration)'!='Debug'">
    <PackageReference Include="SauceControl.InheritDoc" Version="1.0.0" PrivateAssets="all" />
</ItemGroup>
```

*NuGet tools may include a more verbose version of the `PackageReference` tag when you add the package to your project.  The above example is all that's actually necessary.

#### Configuring Doc Trimming

By default, InheritDoc will remove documentation for any types/members that are not part of the assembly's public API from the output XML.  This behavior can be configured by setting the `InheritDocTrimLevel` property to one of: `none`, `private`, or `internal`.  Docs belonging to types/members with API visibility at or below the `InheritDocTrimLevel` will be removed.  The default setting is `internal`.

If your internal types/members are available to other assemblies (by means of `InternalsVisibleToAttribute`) and those projects are not part of the same Visual Studio solution, you may wish to preserve the internal member docs by setting `InheritDocTrimLevel` to `private`.

```XML
<PropertyGroup>
    <InheritDocTrimLevel>private</InheritDocTrimLevel>
</PropertyGroup>
```

#### Adding Candidate Docs for Inheritance

InheritDoc will automatically discover XML documentation files alongside assemblies referenced by your project.  If necessary, additional XML documentation files can be manually included with `InheritDocReference`.  For example:

```XML
<ItemGroup Condition="'$(TargetFramework)'=='netstandard2.0'">
    <InheritDocReference Include="\path\to\netstandard2.1.xml" />
    <InheritDocReference Include="\path\to\moredocs.xml" />
</ItemGroup>
```

#### Disabling InheritDoc Build Warnings

Warnings can be selectively disabled with the MSBuild standard `NoWarn` property.  For example:

```XML
<PropertyGroup Condition="'$(TargetFramework)'=='netstandard2.0'">
    <NoWarn>$(NoWarn);IDT001</NoWarn>
</PropertyGroup>
```

#### Possible Warnings

| Code | Description |
|------|-------------|
|IDT001| Indicates a referenced XML documentation file could not be loaded or parsed or that the file did not contain documentation in the standard schema. |
|IDT002| Indicates incomplete XML docs for the target assembly or one of its external references. i.e. an inheritance candidate was identified but had no documentaion to inherit. |
|IDT003| May indicate you used `<inheritdoc />` on a type/member with no identifiable base. You may correct this warning by using the `cref` attribute to identify the base explicitly. |
|IDT004| May indicate an incorrect XPath value in a `path` attribute or a duplicate/superfluous or self-referencing `<inheritdoc />` tag. |

Known Issues
------------

### Bad NETStandard Docs

When targeting `netstandard2.0`, if you attempt to inherit docs from any types/members in the framework you will receive a warning from InheritDoc related to malformed XML in `netstandard.xml` and then subsequent warnings for each member whose documentation could not be found.  All shipping versions of `NETStandard.Library` v2.0 have bad XML documentation.  This has been corrected in `netstandard2.1` builds, but since InheritDoc resolves documentation based on assembly references at compile time, it will find the bad docs if you have a `netstandard2.0` target.

If this displeases you, you may register your discontent by commenting on the [associated issue](https://github.com/dotnet/standard/issues/1527) in the NETStandard repo.  In the meantime, the following configuration will work around the issue.

```XML
<PropertyGroup Condition="'$(TargetFramework)'=='netstandard2.0'">
    <NoWarn>$(NoWarn);IDT001</NoWarn>
</PropertyGroup>

<ItemGroup Condition="'$(TargetFramework)'=='netstandard2.0'">
    <PackageDownload Include="NETStandard.Library.Ref" Version="[2.1.0]" />
    <InheritDocReference Include="$([MSBuild]::EnsureTrailingSlash('$(NugetPackageRoot)'))netstandard.library.ref\2.1.0\ref\netstandard2.1\netstandard.xml" />
</ItemGroup>
```

`PackageDownload` ensures the package is in the local NuGet cache without actually adding a dependency, and then the corrected/updated `netstandard.xml` is added as an explicit doc reference.  This will resolve the `IDT002` warnings.  The `IDT001` warning can then be ignored if it applies only to `netstandard.xml`.

Troubleshooting
---------------

When it runs, `InheritDocTask` will log a success message to the build output, telling you what it did.  If you don't see the message, it didn't run for some reason.  Check the detailed output from MSBuild (e.g. `dotnet build -v detailed`) and look for `InheritDoc` in the logs for clues.  Issue reports are, of course, welcome with good repro steps.
