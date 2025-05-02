using Cake.Common;
using Cake.Common.IO;
using Cake.Common.IO.Paths;
using Cake.Core;
using Cake.Frosting;

namespace Build.Context;

public sealed class BuildContext : FrostingContext
{
    public bool Delay { get; set; }

    public BuildContext(ICakeContext context)
        : base(context)
    {
        BuildConfiguration = context.Argument("config", "Release");

        SolutionRootDir = context.Directory("../../");
        SrcDir = SolutionRootDir + context.Directory("src");
        TestsDir = SolutionRootDir + context.Directory("tests");
        BuildDir = SolutionRootDir + context.Directory("build");

        SlnFilePath = SolutionRootDir + context.File("LocalStack.sln");

        var vcpkgArg = context.Argument("vcpkg-dir", default(string));

        if (!string.IsNullOrWhiteSpace(vcpkgArg))
        {
            // check if relative path
            VcPkgDir = context.Directory(vcpkgArg);
        }

        var sdlArtifactsDir = context.Argument("sdl-artifact-dir", default(string));

        if (!string.IsNullOrWhiteSpace(sdlArtifactsDir))
        {
            // check if relative path
            SdlArtifactsDir = context.Directory(sdlArtifactsDir);
        }
    }

    public string BuildConfiguration { get; }

    public ConvertableFilePath SlnFilePath { get; }

    public ConvertableDirectoryPath SolutionRootDir { get; }

    public ConvertableDirectoryPath SrcDir { get; }

    public ConvertableDirectoryPath TestsDir { get; }

    public ConvertableDirectoryPath BuildDir { get; }

    public ConvertableDirectoryPath? VcPkgDir { get; }

    public ConvertableDirectoryPath? SdlArtifactsDir { get; }
}
