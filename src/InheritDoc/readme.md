InheritDoc
==========

This [MSBuild Task]( https://docs.microsoft.com/en-us/visualstudio/msbuild/msbuild-tasks) automatically replaces `<inheritdoc />` tags in your .NET XML documentation with the actual inherited docs.  By integrating with MSBuild, this tool has access to the exact arguments passed to the compiler -- including all assembly references -- making it both simpler and more capable than other documentation post-processing tools.  As it processes `<inheritdoc />` elements, it is able to accurately resolve base types whether they come from the target framework, referenced NuGet packages, or project references.  This allows intelligent mapping of documentation from base types and members to yours.  For example, it can identify when you change the name of a method parameter from the base type’s definition and update the documentation accordingly.  It can also remove documentation for non-public types/members to reduce the size of your published XML docs.

How to Use It
-------------

1) Add some `<inheritdoc />` tags to your XML documentation comments.

    This tool’s handling of `<inheritdoc />` tags is based on the draft [design document]( https://github.com/dotnet/csharplang/blob/812e220fe2b964d17f353cb684aa341418618b6e/proposals/inheritdoc.md) used for Roslyn's support in Visual Studio, which is in turn based on the `<inheritdoc />` support in [Sandcastle Help File Builder]( https://ewsoftware.github.io/XMLCommentsGuide/html/86453FFB-B978-4A2A-9EB5-70E118CA8073.htm#TopLevelRules) (SHFB).

2) Add the [SauceControl.InheritDoc](https://www.nuget.org/packages/SauceControl.InheritDoc) NuGet package reference to your project.

    This is a development-only dependency; it will not be deployed with or referenced by your compiled app/library.

3) Build your project as you normally would.

    The XML docs will be post-processed automatically with each non-debug build, whether you use Visual Studio, dotnet CLI, or anything else that hosts the MSBuild engine.

For more details and examples, see the [project home page](https://github.com/saucecontrol/InheritDoc)
