using System.Text.Json;
using Build.Host.Cake;
using Cake.Core;
using Cake.Core.Configuration;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Core.Tooling;
using Microsoft.CodeAnalysis;

namespace Build.Targets.GenerateBindings;

/// <summary>Records actual process arguments at Cake's process boundary, not a second tool rendering.</summary>
internal sealed class ClangSharpInvocationCapture(ICakeContext context, ICollection<string> diagnostics) : ICakeContext, IProcessRunner
{
    private static readonly JsonSerializerOptions CompactJson = new() { WriteIndented = false };
    public IFileSystem FileSystem => context.FileSystem;
    public ICakeEnvironment Environment => context.Environment;
    public IGlobber Globber => context.Globber;
    public ICakeLog Log => context.Log;
    public ICakeArguments Arguments => context.Arguments;
    public IProcessRunner ProcessRunner => this;
    public IRegistry Registry => context.Registry;
    public IToolLocator Tools => context.Tools;
    public ICakeDataResolver Data => context.Data;
    public ICakeConfiguration Configuration => context.Configuration;
    public IToolInstaller ToolInstaller => context.ToolInstaller;
    public IServiceProvider ServiceProvider => context.ServiceProvider;

    public IProcess Start(FilePath filePath, ProcessSettings settings)
    {
        var args = settings.Arguments
            .Select(argument => CommandLineParser.SplitCommandLineIntoArguments(argument.Render(), removeHashComments: false).Single());
        diagnostics.Add(context.SerializeJson(new
        {
            executable = filePath.FullPath,
            cwd = settings.WorkingDirectory.FullPath,
            argv = args.Prepend("dotnet").ToArray(),
        }, CompactJson));
        return context.ProcessRunner.Start(filePath, settings);
    }
}
