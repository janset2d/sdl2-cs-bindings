using Build.Data.BindingGeneration.Models;
using Build.Data.Manifest.Models;
using Build.Results;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Validation.BindingGeneration;

public sealed class BindingGenerationManifestValidator(ICakeContext context, IEnumerable<IBindingFamilyValidator> validators)
{
    private readonly ICakeContext _context = context ?? throw new ArgumentNullException(nameof(context));
    private readonly string[] _registeredIds = validators?.Select(validator => validator.ValidatorId).ToArray() ?? throw new ArgumentNullException(nameof(validators));

    public ValidationReport Validate(ManifestConfig manifest, DirectoryPath repositoryRoot)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(repositoryRoot);
        var errors = new List<ValidationCheck>();
        CheckUnique(manifest.PackageFamilies.Select(family => family.Name), "package_families.name", errors);
        CheckUnique(manifest.PackageFamilies.Select(family => family.LibraryRef).OfType<string>(), "package_families.library_ref", errors);
        CheckUnique(manifest.LibraryManifests.Select(library => library.Name), "library_manifests.name", errors);
        var settings = manifest.BindingGeneration;
        CheckUnique(settings.FamilyOrder, "binding_generation.family_order", errors);

        var enabledFamilies = manifest.PackageFamilies
            .Where(family => manifest.LibraryManifests.Any(library => library.Name == family.LibraryRef && library.BindingGeneration.Enabled))
            .Select(family => family.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var id in settings.FamilyOrder.Where(id => !enabledFamilies.Contains(id)))
        {
            AddError(errors, $"binding_generation.family_order entry '{id}' is not an enabled family.");
        }

        foreach (var id in enabledFamilies.Where(id => !settings.FamilyOrder.Contains(id, StringComparer.Ordinal)).Order(StringComparer.Ordinal))
        {
            AddError(errors, $"binding_generation.family_order is missing enabled family '{id}'.");
        }

        CheckPath(settings.BaseRsp, "binding_generation.base_rsp", false, repositoryRoot, errors);
        CheckPath(settings.PerHeaderRspDir, "binding_generation.per_header_rsp_dir", true, repositoryRoot, errors);
        CheckPath(settings.PlatformHeaderShimsDir, "binding_generation.platform_header_shims_dir", true, repositoryRoot, errors);
        CheckUnique(settings.PlatformViews.Select(view => view.Name), "binding_generation.platform_views.name", errors);
        CheckUnique(settings.AllPlatformMacros, "binding_generation.all_platform_macros", errors);

        foreach (var view in settings.PlatformViews)
        {
            CheckIdentity(view.Name, "binding_generation.platform_views.name", errors);
            CheckIdentity(view.SupportedOs, $"binding_generation.platform_views[{view.Name}].supported_os", errors);
            CheckUnique(view.Defines, $"binding_generation.platform_views[{view.Name}].defines", errors);
        }

        foreach (var family in manifest.PackageFamilies)
        {
            CheckIdentity(family.Name, "package_families.name", errors);
        }

        foreach (var library in manifest.LibraryManifests)
        {
            CheckIdentity(library.Name, "library_manifests.name", errors);
            var profile = library.BindingGeneration;
            var field = $"library_manifests[{library.Name}].binding_generation";
            CheckIdentity(profile.ManagedNamespace, field + ".managed_namespace", errors);
            CheckIdentity(profile.PrimaryClassName, field + ".primary_class_name", errors);

            if (!profile.Enabled)
            {
                continue;
            }

            CheckPath(profile.Rsp, field + ".rsp", false, repositoryRoot, errors);
            CheckPath(profile.GenerationProject, field + ".generation_project", false, repositoryRoot, errors);
            CheckHeaders(profile, field, errors);
            CheckUnique(profile.RequiredFunctions.Select(function => function.Name), field + ".required_functions.name", errors);
            CheckUnique(profile.RequiredConstants.Select(constant => constant.Name), field + ".required_constants.name", errors);

            foreach (var family in manifest.PackageFamilies.Where(family => family.LibraryRef == library.Name))
            {
                try
                {
                    ValidatorIdIntegrityCheck.AssertKnown(profile with { FamilyId = family.Name }, _registeredIds);
                }
                catch (CakeException exception)
                {
                    AddError(errors, exception.Message);
                }
            }
        }

        return new ValidationReport(errors);
    }

    private static void CheckHeaders(BindingGenerationConfig profile, string field, List<ValidationCheck> errors)
    {
        if (profile.HeaderSet is not { } headers)
        {
            AddError(errors, field + ".header_set is required for an enabled family.");
            return;
        }

        CheckRelativePath(headers.IncludeDir, field + ".header_set.include_dir", errors);
        CheckUnique(headers.Headers.Select(header => header.Name), field + ".header_set.headers.name", errors);
        CheckUnique(headers.Headers.Select(header => header.Order), field + ".header_set.headers.order", errors);

        foreach (var header in headers.Headers)
        {
            CheckRelativePath(header.Name, field + ".header_set.headers.name", errors);
        }

        foreach (var sensitive in profile.PlatformSensitiveHeaders.Where(sensitive => !headers.Headers.Any(header => header.Name == sensitive)))
        {
            AddError(errors, $"{field}.platform_sensitive_headers entry '{sensitive}' is not in header_set.headers.");
        }
    }

    private void CheckPath(string path, string field, bool directory, DirectoryPath root, List<ValidationCheck> errors)
    {
        if (!CheckRelativePath(path, field, errors))
        {
            return;
        }

        var exists = directory ? _context.DirectoryExists(root.Combine(path)) : _context.FileExists(root.CombineWithFilePath(path));

        if (!exists)
        {
            AddError(errors, $"{field} path '{path}' does not exist in the repository.");
        }
    }

    private static bool CheckRelativePath(string path, string field, List<ValidationCheck> errors)
    {
        if (string.IsNullOrWhiteSpace(path) || path.StartsWith('/', StringComparison.Ordinal) || path.StartsWith('\\', StringComparison.Ordinal)
            || path.Contains(':', StringComparison.Ordinal) || path.Replace('\\', '/').Split('/').Contains("..", StringComparer.Ordinal))
        {
            AddError(errors, $"{field} path '{path}' must be repository-relative without traversal.");
            return false;
        }

        return true;
    }

    private static void CheckIdentity(string value, string field, List<ValidationCheck> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            AddError(errors, field + " must not be empty.");
        }
    }

    private static void CheckUnique<T>(IEnumerable<T> values, string field, List<ValidationCheck> errors)
    {
        foreach (var group in values.GroupBy(value => value).Where(group => group.Count() > 1))
        {
            AddError(errors, $"{field} contains duplicate '{group.Key}'.");
        }
    }

    private static void AddError(List<ValidationCheck> errors, string message)
    {
        errors.Add(new ValidationCheck("Binding generation manifest", ValidationSeverity.Error, message));
    }
}
