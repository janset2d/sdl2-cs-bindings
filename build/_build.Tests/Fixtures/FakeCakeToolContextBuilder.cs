using Cake.Core;
using Cake.Core.IO;
using Cake.Core.Tooling;
using Cake.Testing;
using NSubstitute;

namespace Build.Tests.Fixtures;

/// <summary>
/// Creates a deterministic ICakeContext with fake process and tool resolution behavior.
/// </summary>
public sealed class FakeCakeToolContextBuilder
{
    private readonly FakeFileSystem _fileSystem;
    private readonly FakeEnvironment _environment;

    private FilePath? _toolPath;
    private IReadOnlyList<string> _standardOutput = [];
    private IReadOnlyList<string> _standardError = [];
    private Exception? _startException;
    private int _exitCode;
    private ProcessCapture? _processCapture;

    public FakeCakeToolContextBuilder(FakeFileSystem fileSystem, FakeEnvironment environment)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _exitCode = 0;
    }

    public FakeCakeToolContextBuilder WithToolPath(FilePath toolPath)
    {
        _toolPath = toolPath ?? throw new ArgumentNullException(nameof(toolPath));
        return this;
    }

    public FakeCakeToolContextBuilder WithStandardOutput(IReadOnlyList<string> standardOutput)
    {
        _standardOutput = standardOutput ?? throw new ArgumentNullException(nameof(standardOutput));
        return this;
    }

    public FakeCakeToolContextBuilder WithStandardError(IReadOnlyList<string> standardError)
    {
        _standardError = standardError ?? throw new ArgumentNullException(nameof(standardError));
        return this;
    }

    public FakeCakeToolContextBuilder WithStartException(Exception startException)
    {
        _startException = startException ?? throw new ArgumentNullException(nameof(startException));
        return this;
    }

    public FakeCakeToolContextBuilder WithExitCode(int exitCode)
    {
        _exitCode = exitCode;
        return this;
    }

    public FakeCakeToolContextBuilder WithProcessCapture(out ProcessCapture processCapture)
    {
        processCapture = new ProcessCapture();
        _processCapture = processCapture;
        return this;
    }

    public ICakeContext Build()
    {
        if (_toolPath is null)
        {
            throw new InvalidOperationException("Tool path must be configured before building the context.");
        }

        if (!_fileSystem.GetFile(_toolPath).Exists)
        {
            _fileSystem.CreateFile(_toolPath);
        }

        var processRunner = Substitute.For<IProcessRunner>();
        processRunner.Start(Arg.Any<FilePath>(), Arg.Any<ProcessSettings>())
            .Returns(call =>
            {
                var settings = (ProcessSettings)call[1];
                if (_processCapture is not null)
                {
                    _processCapture.Settings = settings;
                }

                if (_startException is not null)
                {
                    throw _startException;
                }

                var process = new FakeProcess();
                process.SetExitCode(_exitCode);
                process.SetStandardOutput(_standardOutput);
                process.SetStandardError(_standardError);
                return process;
            });

        var toolLocator = Substitute.For<IToolLocator>();
        toolLocator.Resolve(Arg.Any<string>()).Returns(_toolPath);
        toolLocator.Resolve(Arg.Any<IEnumerable<string>>()).Returns(_toolPath);

        var context = Substitute.For<ICakeContext>();
        context.Log.Returns(new FakeLog());
        context.Environment.Returns(_environment);
        context.FileSystem.Returns(_fileSystem);
        context.ProcessRunner.Returns(processRunner);
        context.Tools.Returns(toolLocator);

        return context;
    }
}

public sealed class ProcessCapture
{
    public ProcessSettings? Settings { get; set; }
}
