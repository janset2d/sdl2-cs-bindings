using Build.Data.BindingGeneration;
using Build.Data.BindingGeneration.Models;
using Build.Results;
using Build.Targets.GenerateBindings;
using Build.Targets.GenerateBindings.HeaderSet;
using Build.Targets.GenerateBindings.Parsing;
using Build.Tests.Fixtures;
using Build.Validation.BindingGeneration;
using Cake.Core;
using CppAst;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using static Build.Tests.Fixtures.BindingGenerationFixture;

namespace Build.Tests.Scenarios.GenerateBindings;

/// <summary>
/// Fail-closed orchestration coverage for <c>GenerateBindingsTask</c>. The real
/// CppAst parse + libclang.runtime.linux-x64 dependency exceeds what the
/// FakeCakeWorld can stand in for, so the happy path is validated by the manual
/// <c>tools.cs generate-bindings</c> smoke. These scenarios cover task-level
/// guards that fire before parse work begins (triplet, libclang version,
/// header resolver). Validator behaviour is covered by per-validator unit tests
/// in <c>Build.Tests.Unit.Validation.BindingGeneration</c>; config-load
/// behaviour by the repository's own unit + round-trip tests.
/// </summary>
public sealed class GenerateBindingsTaskScenarioTests
{
    [Test]
    public async Task RunAsync_Should_Throw_CakeException_When_Triplet_Not_Linux()
    {
        var world = FakeCakeWorld.CreateWindows();   // x64-windows-hybrid

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception).IsNotNull();
        await Assert.That(result.Exception).IsTypeOf<CakeException>();
        await Assert.That(result.Exception!.Message).Contains("Linux-canonical");
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Libclang_Asserter_Fails()
    {
        var world = FakeCakeWorld.CreateLinux();
        var libclangAsserter = Substitute.For<ILibclangVersionAsserter>();
        libclangAsserter.When(a => a.Assert()).Do(_ => throw new CakeException("libclang version mismatch: expected 20.1.x"));

        var result = await CreateHost(world, libclangAsserter: libclangAsserter).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception).IsNotNull();
        await Assert.That(result.Exception).IsTypeOf<CakeException>();
        await Assert.That(result.Exception!.Message).Contains("libclang version mismatch");
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Vcpkg_Include_Dir_Is_Missing()
    {
        // Config repository returns ["sdl2-core"] enabled + a valid config, but no
        // vcpkg_installed/x64-linux-hybrid/include/SDL2/ seeded → HeaderSetResolver
        // throws before any parse work. Verifies the resolver guards the task body
        // once config loads.
        var world = FakeCakeWorld.CreateLinux();

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception).IsNotNull();
        await Assert.That(result.Exception).IsTypeOf<CakeException>();
        await Assert.That(result.Exception!.Message).Contains("include directory was not found");
    }

    [Test]
    public async Task RunAsync_Should_Emit_Configured_CSharp_Identity()
    {
        var world = FakeCakeWorld.CreateLinux();
        var config = Sdl2CoreConfig() with
        {
            ManagedNamespace = "Example.Bindings",
            PrimaryClassName = "ExampleApi",
        };
        world.WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL_video.h", string.Empty);
        var parser = Substitute.For<ICppAstParseRunner>();
        parser
            .Parse(Arg.Any<BindingGenerationConfig>(), Arg.Any<ResolvedHeaderSet>(), Arg.Any<PlatformParseView>())
            .Returns(call => new CppAstParseResult(call.Arg<PlatformParseView>(), []));

        var result = await CreateHost(world, parser: parser, configRepository: CreateEnabledRepository(config)).RunAsync();

        await Assert.That(result.Success).IsTrue();
        var commands = world.ReadAllText("artifacts/generated-bindings-preview/sdl2-core/Platform/Neutral/Commands.g.cs");
        await Assert.That(commands).Contains("namespace Example.Bindings;");
        await Assert.That(commands).Contains("internal static unsafe partial class ExampleApiNative");
        await Assert.That(commands).DoesNotContain("namespace Janset.SDL2.Core;");
        await Assert.That(commands).DoesNotContain("SDL2Native");
    }

    private static TargetTestHost<GenerateBindingsTask> CreateHost(
        FakeCakeWorld world,
        ILibclangVersionAsserter? libclangAsserter = null,
        ICppAstParseRunner? parser = null,
        IBindingGenerationConfigRepository? configRepository = null,
        IEnumerable<IBindingFamilyValidator>? validators = null)
    {
        var repo = configRepository ?? CreateEnabledSdl2CoreRepository();
        return new TargetTestHost<GenerateBindingsTask>(world)
            .WithServices(services =>
            {
                services.AddSingleton<ParseDiagnosticFormatter>();
                services.AddSingleton<HeaderSetResolver>();
                services.AddSingleton(libclangAsserter ?? Substitute.For<ILibclangVersionAsserter>());
                services.AddSingleton(parser ?? Substitute.For<ICppAstParseRunner>());
                services.AddSingleton(repo);
                if (validators is not null)
                {
                    foreach (var v in validators) services.AddSingleton(v);
                }
            });
    }

    private static IBindingGenerationConfigRepository CreateEnabledSdl2CoreRepository()
    {
        return CreateEnabledRepository(Sdl2CoreConfig());
    }

    private static IBindingGenerationConfigRepository CreateEnabledRepository(BindingGenerationConfig config)
    {
        var repo = Substitute.For<IBindingGenerationConfigRepository>();
        repo.EnumerateEnabledFamilies().Returns([config.FamilyId]);
        repo.Load(config.FamilyId).Returns(
            Result<BindingGenerationConfig, BindingGenerationConfigError>.Success(config));
        return repo;
    }
}
