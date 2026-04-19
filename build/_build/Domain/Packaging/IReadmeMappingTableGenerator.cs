namespace Build.Domain.Packaging;

public interface IReadmeMappingTableGenerator
{
    Task UpdateAsync(CancellationToken cancellationToken = default);
}
