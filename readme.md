[![NuGet](https://buildstats.info/nuget/SauceControl.InheritDoc)](https://www.nuget.org/packages/SauceControl.InheritDoc/) [![Build Status](https://dev.azure.com/saucecontrol/InheritDoc/_apis/build/status/saucecontrol.InheritDoc?branchName=master)](https://dev.azure.com/saucecontrol/InheritDoc/_build/latest?definitionId=2&branchName=master) [![Test Results](https://img.shields.io/azure-devops/tests/saucecontrol/InheritDoc/2?logo=azure-devops)](https://dev.azure.com/saucecontrol/InheritDoc/_build/latest?definitionId=2&branchName=master)

InheritDoc
==========

This [MSBuild Task]( https://docs.microsoft.com/en-us/visualstudio/msbuild/msbuild-tasks) automatically replaces `<inheritdoc />` tags in your .NET XML documentation with the actual inherited docs.

How to Use It
-------------

1) Add some `<inheritdoc />` tags to your XML documentation comments.

    This tool’s handling of `<inheritdoc />` tags is based on the [design document]( https://github.com/dotnet/csharplang/blob/812e220fe2b964d17f353cb684aa341418618b6e/proposals/inheritdoc.md) used for Roslyn's support in Visual Studio, which is in turn based on the `<inheritdoc />` support in [Sandcastle Help File Builder]( https://ewsoftware.github.io/XMLCommentsGuide/html/86453FFB-B978-4A2A-9EB5-70E118CA8073.htm#TopLevelRules) (SHFB).

2) Add the [SauceControl.InheritDoc](https://www.nuget.org/packages/SauceControl.InheritDoc) NuGet package reference to your project.

    This is a development-only dependency; it will not be deployed with or referenced by your compiled app/library.

3) Build your project as you normally would.

    The XML docs will be post-processed automatically with each non-debug build, whether you use Visual Studio, dotnet CLI, or anything else that hosts the MSBuild engine.

Additional Features
-------------------

* Updates the contents of inherited docs to replace `param` and `typeparam` names that changed in the inheriting type or member.

* Supports trimming your published XML doc files of any types or members not publicly visible in your API.

* Validates your usage of `<inheritdoc />` and warns you if no documentation exists or if your `cref`s or `path`s are incorrect.

How it Works
------------

The InheritDoc task inserts itself between the `Compile` and `CopyFilesToOutputDirectory` steps in the MSBuild process.  It uses the arguments passed to the compiler to find your assembly, the XML doc file, and all referenced assemblies, and processes it to replace `<inheritdoc />` tags.  The output of InheritDoc is then written to your output (bin) directory and is used for the remainder of your build process.  If you have further steps, such as building a NuGet package, the updated XML file will used in place of the original, meaning `<inheritdoc />` Just Works™.

This enhances the new support for `<inheritdoc />` in Roslyn (available starting in [VS 16.4](https://docs.microsoft.com/en-us/visualstudio/releases/2019/release-notes-preview#net-productivity-164P1)), making it available to all downstream consumers of your documentation.  When using tools such as [DocFX](https://dotnet.github.io/docfx/spec/triple_slash_comments_spec.html#inheritdoc), you will no longer be [subject](https://github.com/dotnet/docfx/issues/3699) to [limitations](https://github.com/dotnet/docfx/issues/1306) around `<inheritdoc />` tag usage because the documentation will already have those tags replaced with the upstream docs.

Requirements
------------

InheritDoc requires MSBuild 16.0 or greater, which is included with the .NET SDK or with Visual Studio 2019 or later.

Some Examples
-------------

Click to expand samples:

<details>
<summary>Basic <code>&lt;inheritdoc /&gt;</code> Usage (Automatic Inheritance)</summary>

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

</details>

<details>
<summary>Explicit Inheritance</summary>

InheritDoc also supports the `path` attribute defined in the Roslyn draft design doc, which is analogous to the `select` attribute in SHFB.

In this example, we define a custom Exception class that for some reason doesn't inherit from `System.Exception` and yet we want to use its documentation anyway.

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

</details>

<details>
<summary>Namespace Documentation</summary>

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

</details>

Configuration
-------------

#### Enabling/Disabling InheritDoc

InheritDoc is enabled by default for all non-debug builds.  It can be enabled or disabled explicitly by setting the `InheritDocEnabled` MSBuild property in your project.

```XML
<PropertyGroup>
    <InheritDocEnabled>false</InheritDocEnabled>
</PropertyGroup>
```

Alternatively, you can conditionally include the NuGet package only for specific configurations.

```XML
<ItemGroup Condition="'$(Configuration)'=='Dist'">
    <PackageReference Include="SauceControl.InheritDoc" Version="2.*" PrivateAssets="all" />
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
<ItemGroup>
    <InheritDocReference Include="\path\to\somedocs.xml" />
    <InheritDocReference Include="\path\to\moredocs.xml" />
</ItemGroup>
```

#### Using InheritDoc With Multi-Targeted Projects

If you are multi-targeting using the new(er) SDK-style projects and the `TargetFrameworks` property, you must ensure that you are not generating multiple XML documentation outputs to the same file path.

If you configure the XML documentation output from the project property page in Visual Studio, you may end up with something like:

```XML
<PropertyGroup>
    <DocumentationFile>MyProject.xml</DocumentationFile> <!-- NOOOOOOO! -->
</PropertyGroup>
```

The above configuration will create a single `MyProject.xml` file in your project root for all target frameworks and all build configurations.  Since the dotnet build server builds multiple target framework outputs in parallel there will be a race condition for access to that file.

The simpler configuration, supported in all multi-targeting capable SDK versions, is:

```XML
<PropertyGroup>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
</PropertyGroup>
```

This will automatically name your XML file with the same base as the assembly name and will create it in the correct `obj` folder alongside the assembly.

#### Using InheritDoc in Docker

The .NET SDK Docker images set an environment variable that instructs the NuGet client not to extract XML documentation files during package restore.  This is done in the interest of time and space savings, as explained in https://github.com/dotnet/dotnet-docker/issues/2790, however this may prevent InheritDoc from resolving documentation from NuGet package references.

The default behavior can be restored by clearing the environment variable in your own `Dockerfile`.

```ini
ENV NUGET_XMLDOC_MODE=
```

More documentation on the environment variables used by NuGet client can be found [here](https://learn.microsoft.com/en-us/nuget/reference/cli-reference/cli-ref-environment-variables).

#### Disabling InheritDoc Build Warnings

Warnings can be selectively disabled with the MSBuild standard `NoWarn` property.  For example:

```XML
<PropertyGroup>
    <NoWarn>$(NoWarn);IDT002</NoWarn>
</PropertyGroup>
```

#### Possible Warnings

| Code | Description |
|------|-------------|
|IDT001| Indicates a referenced XML documentation file could not be loaded or parsed or that the file did not contain documentation in the standard schema. |
|IDT002| Indicates incomplete XML docs for the target assembly or one of its external references. i.e. an inheritance candidate was identified but had no documentaion to inherit. |
|IDT003| May indicate you used `<inheritdoc />` on a type/member with no identifiable base. You may correct this warning by using the `cref` attribute to identify the base explicitly. |
|IDT004| May indicate an incorrect XPath value in a `path` attribute or a duplicate/superfluous or self-referencing `<inheritdoc />` tag. |

Troubleshooting
---------------

When it runs, `InheritDocTask` will log a success message to the build output for each processed file, telling you what it did.  For example:

```
InheritDocTask replaced 55 of 55 inheritdoc tags and removed 60 non-public member docs in /path/to/MyProject.xml
```

If you don't see the message(s), it didn't run for some reason.  Check the detailed output from MSBuild (e.g. `dotnet build -v detailed`) or use the [MSBuild Log Viewer](https://msbuildlog.com/) and look for `InheritDoc` in the logs for clues.  Issue reports are, of course, welcome with good repro steps.
