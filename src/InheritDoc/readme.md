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

For more details and examples, see the [project home page](https://github.com/saucecontrol/InheritDoc)
