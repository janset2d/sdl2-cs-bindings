using System.Reflection;
using Build.Host;

namespace Build.Tests.Unit.CompositionRoot;

/// <summary>
/// Architecture-level dependency direction tests per ADR-002 §2.8.
/// <para>
/// Three invariants are asserted against the production <c>Build</c> assembly:
/// </para>
/// <list type="number">
///   <item><description>Domain has no outward dependencies on Application or Tasks.</description></item>
///   <item><description>Infrastructure does not reach into Application or Tasks.</description></item>
///   <item><description>Tasks reach behavior through Application; only DTO/result types and Cake tool wrappers may cross in from Domain/Infrastructure.</description></item>
/// </list>
/// <para>
/// Cake framework and NuGet.Versioning references are permitted from any layer (framework glue)
/// — only cross-layer Build.* references are asserted.
/// </para>
/// </summary>
public sealed class LayerDependencyTests
{
    private const string DomainPrefix = "Build.Domain.";
    private const string ApplicationPrefix = "Build.Application.";
    private const string InfrastructurePrefix = "Build.Infrastructure.";
    private const string TasksPrefix = "Build.Tasks.";

    private static readonly Assembly BuildAssembly = typeof(BuildContext).Assembly;

    [Test]
    public async Task Domain_Should_Not_Reference_Application_Or_Tasks()
    {
        var violations = FindViolations(
            sourcePrefix: DomainPrefix,
            forbiddenPrefixes: [ApplicationPrefix, TasksPrefix, InfrastructurePrefix]);

        await Assert.That(violations)
            .IsEmpty()
            .Because(FormatViolations(violations));
    }

    [Test]
    public async Task Infrastructure_Should_Not_Reference_Application_Or_Tasks()
    {
        var violations = FindViolations(
            sourcePrefix: InfrastructurePrefix,
            forbiddenPrefixes: [ApplicationPrefix, TasksPrefix]);

        await Assert.That(violations)
            .IsEmpty()
            .Because(FormatViolations(violations));
    }

    [Test]
    public async Task Tasks_Should_Not_Reference_Domain_Or_Infrastructure_Services_Outside_Dtos_And_Tools()
    {
        // Target shape:
        //   (a) behavior flows through Application services
        //   (b) Domain / Infrastructure DTOs under `.Models.` or `.Results.` may cross the
        //       boundary because they are the task's data currency.
        // What Tasks must NOT hold is a Domain / Infrastructure service seam, concrete or
        // interface. Application runners/factories own that orchestration boundary.
        var violations = FindViolations(
            sourcePrefix: TasksPrefix,
            forbiddenPrefixes: [DomainPrefix, InfrastructurePrefix],
            isAllowedReference: IsDomainOrInfrastructureDtoOrTool);

        await Assert.That(violations)
            .IsEmpty()
            .Because(FormatViolations(violations));
    }

    private static bool IsDomainOrInfrastructureDtoOrTool(Type referenced)
    {
        var ns = referenced.Namespace ?? string.Empty;

        // Cake Tool wrappers live under Build.Infrastructure.Tools.* and follow the
        // Cake Frosting convention: `Tool<TSettings>` subclasses and helper wrappers are
        // instantiated directly (by Aliases extension methods or task DI). They are not
        // regular "services" that need a domain interface — they ARE the Cake adapter
        // boundary. Tasks holding them is canonical per ADR-002 §2.6.
        if (ns.StartsWith("Build.Infrastructure.Tools", StringComparison.Ordinal))
        {
            return true;
        }

        return ns.Contains(".Models.", StringComparison.Ordinal)
            || ns.EndsWith(".Models", StringComparison.Ordinal)
            || ns.Contains(".Results.", StringComparison.Ordinal)
            || ns.EndsWith(".Results", StringComparison.Ordinal);
    }

    private static List<string> FindViolations(
        string sourcePrefix,
        string[] forbiddenPrefixes,
        Func<Type, bool>? isAllowedReference = null)
    {
        var violations = new List<string>();

        var sourceTypes = SafeGetTypes(BuildAssembly)
            .Where(t => IsInNamespace(t, sourcePrefix) && !IsCompilerGenerated(t));

        foreach (var sourceType in sourceTypes)
        {
            foreach (var referenced in GetReferencedTypes(sourceType))
            {
                if (referenced.Namespace is null)
                {
                    continue;
                }

                if (isAllowedReference is not null && isAllowedReference(referenced))
                {
                    continue;
                }

                foreach (var forbidden in forbiddenPrefixes)
                {
                    if (referenced.Namespace.StartsWith(forbidden, StringComparison.Ordinal))
                    {
                        violations.Add($"  {sourceType.FullName} -> {referenced.FullName}");
                    }
                }
            }
        }

        return violations.Distinct(StringComparer.Ordinal).OrderBy(v => v, StringComparer.Ordinal).ToList();
    }

    private static HashSet<Type> GetReferencedTypes(Type type)
    {
        var types = new HashSet<Type>();

        if (type.BaseType is not null)
        {
            types.Add(type.BaseType);
        }

        foreach (var iface in type.GetInterfaces())
        {
            types.Add(iface);
        }

        const BindingFlags AllMembers = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (var field in type.GetFields(AllMembers))
        {
            AddWithGenerics(types, field.FieldType);
        }

        foreach (var prop in type.GetProperties(AllMembers))
        {
            AddWithGenerics(types, prop.PropertyType);
        }

        foreach (var method in type.GetMethods(AllMembers))
        {
            AddWithGenerics(types, method.ReturnType);
            foreach (var parameter in method.GetParameters())
            {
                AddWithGenerics(types, parameter.ParameterType);
            }
        }

        foreach (var ctor in type.GetConstructors(AllMembers))
        {
            foreach (var parameter in ctor.GetParameters())
            {
                AddWithGenerics(types, parameter.ParameterType);
            }
        }

        return types;
    }

    private static void AddWithGenerics(HashSet<Type> set, Type type)
    {
        set.Add(type);
        if (type.IsGenericType)
        {
            foreach (var genericArg in type.GetGenericArguments())
            {
                set.Add(genericArg);
            }
        }
    }

    private static bool IsInNamespace(Type type, string prefix)
        => type.Namespace is not null && type.Namespace.StartsWith(prefix, StringComparison.Ordinal);

    private static bool IsCompilerGenerated(Type type)
        => type.Name.Contains('<', StringComparison.Ordinal)
        || type.Name.Contains('>', StringComparison.Ordinal)
        || type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), inherit: false);

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }

    private static string FormatViolations(List<string> violations)
        => violations.Count == 0
            ? "no violations"
            : $"layer dependency violations ({violations.Count}):\n" + string.Join('\n', violations);
}
