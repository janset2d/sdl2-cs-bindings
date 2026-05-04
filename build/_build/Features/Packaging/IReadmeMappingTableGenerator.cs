namespace Build.Features.Packaging;

public interface IReadmeMappingTableGenerator
{
    Task UpdateAsync(CancellationToken ct = default);
}
