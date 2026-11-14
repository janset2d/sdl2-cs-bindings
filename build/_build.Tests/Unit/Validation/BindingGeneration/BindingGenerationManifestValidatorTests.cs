using System.Collections.Immutable;
using Build.Data;
using Build.Data.BindingGeneration.Models;
using Build.Data.Manifest.Models;
using Build.Tests.Fixtures;
using Build.Validation;
using Build.Validation.BindingGeneration;
using Microsoft.Extensions.DependencyInjection;

namespace Build.Tests.Unit.Validation.BindingGeneration;

public sealed class BindingGenerationManifestValidatorTests
{
    [Test]
    public async Task Validate_Should_Accept_Real_Profiles_Without_Installed_Headers()
    {
        await AssertErrors(ManifestFixture.RealManifest);
    }

    [Test]
    public async Task Validate_Should_Refuse_Duplicate_Families()
    {
        var manifest = ManifestFixture.RealManifest;
        manifest = manifest with { PackageFamilies = manifest.PackageFamilies.Add(manifest.PackageFamilies[0]) };
        await AssertErrors(manifest, "package_families.name contains duplicate 'sdl2-core'.", "package_families.library_ref contains duplicate 'SDL2'.");
    }

    [Test]
    public async Task Validate_Should_Refuse_Duplicate_Libraries()
    {
        var manifest = ManifestFixture.RealManifest;
        manifest = manifest with { LibraryManifests = manifest.LibraryManifests.Add(manifest.LibraryManifests[0]) };
        await AssertErrors(manifest, "library_manifests.name contains duplicate 'SDL2'.");
    }

    [Test]
    public async Task Validate_Should_Refuse_Duplicate_Order_Entries()
    {
        var manifest = ManifestFixture.RealManifest;
        await AssertErrors(manifest with { BindingGeneration = manifest.BindingGeneration with { FamilyOrder = manifest.BindingGeneration.FamilyOrder.Add("sdl2-core") } },
            "binding_generation.family_order contains duplicate 'sdl2-core'.");
    }

    [Test]
    public async Task Validate_Should_Refuse_Missing_Order_Entries()
    {
        var manifest = ManifestFixture.RealManifest;
        await AssertErrors(manifest with
        {
            BindingGeneration = manifest.BindingGeneration with { FamilyOrder = manifest.BindingGeneration.FamilyOrder.Remove("sdl2-core", StringComparer.Ordinal) },
        },
            "binding_generation.family_order is missing enabled family 'sdl2-core'.");
    }

    [Test]
    public async Task Validate_Should_Refuse_Unknown_Order_Entries()
    {
        var manifest = ManifestFixture.RealManifest;
        await AssertErrors(manifest with { BindingGeneration = manifest.BindingGeneration with { FamilyOrder = manifest.BindingGeneration.FamilyOrder.Add("sdl2-unknown") } },
            "binding_generation.family_order entry 'sdl2-unknown' is not an enabled family.");
    }

    [Test]
    [Arguments("managed_namespace")]
    [Arguments("primary_class_name")]
    public async Task Validate_Should_Refuse_Empty_Profile_Identities(string field)
    {
        var profile = ManifestFixture.RealCoreLibrary.BindingGeneration;
        profile = field == "managed_namespace" ? profile with { ManagedNamespace = " " } : profile with { PrimaryClassName = " " };
        await AssertErrors(WithCore(profile), $"library_manifests[SDL2].binding_generation.{field} must not be empty.");
    }

    [Test]
    public async Task Validate_Should_Refuse_Empty_Family_Identity()
    {
        var manifest = ManifestFixture.RealManifest;
        manifest = manifest with
        {
            PackageFamilies = manifest.PackageFamilies.SetItem(0, manifest.PackageFamilies[0] with { Name = " " }),
            BindingGeneration = manifest.BindingGeneration with { FamilyOrder = manifest.BindingGeneration.FamilyOrder.SetItem(0, " ") },
        };
        await AssertErrors(manifest, "package_families.name must not be empty.");
    }

    [Test]
    public async Task Validate_Should_Refuse_Empty_Library_Identity()
    {
        var manifest = ManifestFixture.RealManifest;
        manifest = manifest with
        {
            PackageFamilies = manifest.PackageFamilies.SetItem(0, manifest.PackageFamilies[0] with { LibraryRef = " " }),
            LibraryManifests = manifest.LibraryManifests.SetItem(0, manifest.LibraryManifests[0] with { Name = " " }),
        };
        await AssertErrors(manifest, "library_manifests.name must not be empty.");
    }

    [Test]
    public async Task Validate_Should_Refuse_Missing_Header_Inventory()
    {
        await AssertErrors(WithCore(ManifestFixture.RealCoreLibrary.BindingGeneration with { HeaderSet = null }),
            "library_manifests[SDL2].binding_generation.header_set is required for an enabled family.");
    }

    [Test]
    public async Task Validate_Should_Refuse_Duplicate_Header_Names()
    {
        var profile = ManifestFixture.RealCoreLibrary.BindingGeneration;
        var headers = profile.HeaderSet!;
        await AssertErrors(WithCore(profile with { HeaderSet = headers with { Headers = headers.Headers.Add(headers.Headers[0] with { Order = 999 }) } }),
            "library_manifests[SDL2].binding_generation.header_set.headers.name contains duplicate 'SDL_assert.h'.");
    }

    [Test]
    public async Task Validate_Should_Refuse_Duplicate_Header_Order_Values()
    {
        var profile = ManifestFixture.RealCoreLibrary.BindingGeneration;
        var headers = profile.HeaderSet!;
        await AssertErrors(WithCore(profile with { HeaderSet = headers with { Headers = headers.Headers.Add(headers.Headers[0] with { Name = "extra.h" }) } }),
            "library_manifests[SDL2].binding_generation.header_set.headers.order contains duplicate '10'.");
    }

    [Test]
    public async Task Validate_Should_Refuse_Sensitive_Headers_Outside_Inventory()
    {
        await AssertErrors(WithCore(ManifestFixture.RealCoreLibrary.BindingGeneration with { PlatformSensitiveHeaders = ["outside.h"] }),
            "library_manifests[SDL2].binding_generation.platform_sensitive_headers entry 'outside.h' is not in header_set.headers.");
    }

    [Test]
    public async Task Validate_Should_Refuse_Duplicate_View_Names()
    {
        var manifest = ManifestFixture.RealManifest;
        var settings = manifest.BindingGeneration;
        await AssertErrors(manifest with { BindingGeneration = settings with { PlatformViews = settings.PlatformViews.Add(settings.PlatformViews[0]) } },
            "binding_generation.platform_views.name contains duplicate 'WindowsDesktop'.");
    }

    [Test]
    [Arguments("name")]
    [Arguments("supported_os")]
    public async Task Validate_Should_Refuse_Empty_View_Identities(string field)
    {
        var manifest = ManifestFixture.RealManifest;
        var settings = manifest.BindingGeneration;
        var view = settings.PlatformViews[0];
        view = field == "name" ? view with { Name = " " } : view with { SupportedOs = " " };
        await AssertErrors(manifest with { BindingGeneration = settings with { PlatformViews = settings.PlatformViews.SetItem(0, view) } },
            field == "name" ? "binding_generation.platform_views.name must not be empty." : "binding_generation.platform_views[WindowsDesktop].supported_os must not be empty.");
    }

    [Test]
    public async Task Validate_Should_Refuse_Duplicate_Macro_Tokens()
    {
        var manifest = ManifestFixture.RealManifest;
        var settings = manifest.BindingGeneration;
        await AssertErrors(manifest with { BindingGeneration = settings with { AllPlatformMacros = settings.AllPlatformMacros.Add(settings.AllPlatformMacros[0]) } },
            "binding_generation.all_platform_macros contains duplicate '_WIN32'.");
    }

    [Test]
    public async Task Validate_Should_Refuse_Duplicate_View_Defines()
    {
        var manifest = ManifestFixture.RealManifest;
        var settings = manifest.BindingGeneration;
        var view = settings.PlatformViews[0];
        await AssertErrors(manifest with
        {
            BindingGeneration = settings with { PlatformViews = settings.PlatformViews.SetItem(0, view with { Defines = view.Defines.Add(view.Defines[0]) }) },
        }, "binding_generation.platform_views[WindowsDesktop].defines contains duplicate '_WIN32=1'.");
    }

    [Test]
    [Arguments("functions", "SDL_Init")]
    [Arguments("constants", "SDL_INIT_TIMER")]
    public async Task Validate_Should_Refuse_Duplicate_Required_Declarations(string kind, string name)
    {
        var profile = ManifestFixture.RealCoreLibrary.BindingGeneration;
        profile = kind == "functions"
            ? profile with { RequiredFunctions = profile.RequiredFunctions.Add(profile.RequiredFunctions[0]) }
            : profile with { RequiredConstants = profile.RequiredConstants.Add(profile.RequiredConstants[0]) };
        await AssertErrors(WithCore(profile), $"library_manifests[SDL2].binding_generation.required_{kind}.name contains duplicate '{name}'.");
    }

    [Test]
    public async Task Validate_Should_Refuse_Unknown_Enabled_Validator_Ids_In_Ordinal_Order()
    {
        var profile = ManifestFixture.RealCoreLibrary.BindingGeneration with
        {
            Validators = new Dictionary<string, bool>(StringComparer.Ordinal) { ["z-unknown"] = true, ["a-unknown"] = true }.ToImmutableDictionary(StringComparer.Ordinal),
        };
        await AssertErrors(WithCore(profile), "Binding generation family 'sdl2-core' enables unknown validator id(s): a-unknown, z-unknown. " +
            "Register the validator or fix build/manifest.json library_manifests[].binding_generation.validators.");
    }

    [Test]
    public async Task Validate_Should_Ignore_Disabled_Validator_Ids_And_Disabled_Family_Paths()
    {
        var manifest = WithCore(ManifestFixture.RealCoreLibrary.BindingGeneration with
        {
            Enabled = false,
            Rsp = "../not-checked.rsp",
            GenerationProject = "../not-checked.csproj",
            HeaderSet = null,
            Validators = new Dictionary<string, bool>(StringComparer.Ordinal) { ["unknown"] = true }.ToImmutableDictionary(StringComparer.Ordinal),
        });
        await AssertErrors(manifest with
        {
            BindingGeneration = manifest.BindingGeneration with { FamilyOrder = manifest.BindingGeneration.FamilyOrder.Remove("sdl2-core", StringComparer.Ordinal) },
        });
        await AssertErrors(WithCore(ManifestFixture.RealCoreLibrary.BindingGeneration with
        {
            Validators = new Dictionary<string, bool>(StringComparer.Ordinal) { ["unknown"] = false }.ToImmutableDictionary(StringComparer.Ordinal),
        }));
    }

    [Test]
    [Arguments("base_rsp")]
    [Arguments("per_header_rsp_dir")]
    [Arguments("platform_header_shims_dir")]
    [Arguments("rsp")]
    [Arguments("generation_project")]
    public async Task Validate_Should_Refuse_Missing_Generation_Inputs(string field)
    {
        var manifest = WithPath(field, "missing-input");
        var world = FakeCakeWorld.CreateLinux();
        BindingGenerationFixture.SeedGenerationInputs(world, ManifestFixture.RealManifest);
        var report = await ValidateAsync(world, manifest);
        var prefix = field is "rsp" or "generation_project" ? "library_manifests[SDL2].binding_generation." : "binding_generation.";
        await Assert.That(report.Errors.Select(error => error.Message)).IsEquivalentTo(
            [$"{prefix}{field} path 'missing-input' does not exist in the repository."], TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    [Arguments("base_rsp", "../escape.rsp")]
    [Arguments("per_header_rsp_dir", "/absolute")]
    [Arguments("platform_header_shims_dir", "C:\\absolute")]
    [Arguments("rsp", "..\\escape.rsp")]
    [Arguments("generation_project", "\\\\host\\share\\project.csproj")]
    [Arguments("include_dir", "../escape")]
    [Arguments("headers.name", "../escape.h")]
    [Arguments("rsp", " ")]
    public async Task Validate_Should_Refuse_Rooted_Traversing_Or_Empty_Paths(string field, string path)
    {
        var manifest = WithPath(field, path);
        var world = FakeCakeWorld.CreateLinux();
        BindingGenerationFixture.SeedGenerationInputs(world, ManifestFixture.RealManifest);
        var prefix = field switch
        {
            "rsp" or "generation_project" => "library_manifests[SDL2].binding_generation.",
            "include_dir" or "headers.name" => "library_manifests[SDL2].binding_generation.header_set.",
            _ => "binding_generation.",
        };
        var report = await ValidateAsync(world, manifest);
        await Assert.That(report.Errors.Select(error => error.Message)).IsEquivalentTo(
            [$"{prefix}{field} path '{path}' must be repository-relative without traversal."], TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    private static ManifestConfig WithPath(string field, string path)
    {
        var manifest = ManifestFixture.RealManifest;
        var settings = manifest.BindingGeneration;
        var profile = ManifestFixture.RealCoreLibrary.BindingGeneration;
        return field switch
        {
            "base_rsp" => manifest with { BindingGeneration = settings with { BaseRsp = path } },
            "per_header_rsp_dir" => manifest with { BindingGeneration = settings with { PerHeaderRspDir = path } },
            "platform_header_shims_dir" => manifest with { BindingGeneration = settings with { PlatformHeaderShimsDir = path } },
            "rsp" => WithCore(profile with { Rsp = path }),
            "generation_project" => WithCore(profile with { GenerationProject = path }),
            "include_dir" => WithCore(profile with { HeaderSet = profile.HeaderSet! with { IncludeDir = path } }),
            "headers.name" => WithCore(profile with
            {
                HeaderSet = profile.HeaderSet! with { Headers = profile.HeaderSet.Headers.SetItem(0, profile.HeaderSet.Headers[0] with { Name = path }) },
            }),
            _ => throw new ArgumentException("Unknown path field.", nameof(field)),
        };
    }

    private static ManifestConfig WithCore(BindingGenerationConfig profile)
    {
        var manifest = ManifestFixture.RealManifest;
        return manifest with { LibraryManifests = manifest.LibraryManifests.SetItem(0, manifest.LibraryManifests[0] with { BindingGeneration = profile }) };
    }

    private static async Task<Build.Results.ValidationReport> ValidateAsync(FakeCakeWorld world, ManifestConfig manifest)
    {
        var services = new ServiceCollection().AddFakeCakeWorld(world);
        services.AddData();
        services.AddValidators();
        await using var provider = services.BuildServiceProvider();
        var validator = new BindingGenerationManifestValidator(world.CakeContext, provider.GetServices<IBindingFamilyValidator>());
        return validator.Validate(manifest, world.RepoRoot);
    }

    private static async Task AssertErrors(ManifestConfig manifest, params string[] expected)
    {
        var world = FakeCakeWorld.CreateLinux();
        BindingGenerationFixture.SeedGenerationInputs(world, manifest);
        var report = await ValidateAsync(world, manifest);
        await Assert.That(report.Errors.Select(error => error.Message).ToArray()).IsEquivalentTo(expected, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }
}
