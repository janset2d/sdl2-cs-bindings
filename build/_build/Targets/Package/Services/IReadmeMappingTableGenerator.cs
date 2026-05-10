namespace Build.Targets.Package.Services;

public interface IReadmeMappingTableGenerator
{
    Task UpdateAsync(CancellationToken ct = default);
}
