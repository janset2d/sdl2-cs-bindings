namespace Build.Targets.GenerateBindings.Model;

public sealed record NativeAbiShape(string StorageName, int? SizeBytes, bool IsBlittable)
{
    public static NativeAbiShape Of(string storageName, int? sizeBytes = null, bool isBlittable = true) =>
        new(storageName, sizeBytes, isBlittable);
}
