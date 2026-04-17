namespace Build.Modules.Contracts;

public interface IPackageTaskRunner
{
    Task RunAsync(CancellationToken cancellationToken = default);
}
