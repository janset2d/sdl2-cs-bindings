using Build.Data.BindingGeneration.Models;
using Build.Results;

namespace Build.Data.BindingGeneration;

public interface IBindingGenerationConfigRepository
{
    public Result<BindingGenerationConfig, BindingGenerationConfigError> Load(string familyId);
    public IReadOnlyList<string> EnumerateEnabledFamilies();
}
