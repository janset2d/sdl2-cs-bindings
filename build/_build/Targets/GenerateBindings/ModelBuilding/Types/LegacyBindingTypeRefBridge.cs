using Build.Targets.GenerateBindings.Model;

namespace Build.Targets.GenerateBindings.ModelBuilding.Types;

internal static class LegacyBindingTypeRefBridge
{
    public static NativeTypeRef ToNative(BindingTypeRef typeRef)
    {
        ArgumentNullException.ThrowIfNull(typeRef);

        if (typeRef.IsOpaqueHandle)
        {
            return NativeTypeRef.OpaqueHandle(typeRef.ManagedName, typeRef.ManagedName, typeRef.OwningFamilyId, sourceHeader: null);
        }

        var pointerDepth = CountTrailingStars(typeRef.ManagedName);
        if (typeRef.IsPointer && pointerDepth > 0)
        {
            var elementName = typeRef.ManagedName[..^pointerDepth];
            var elementType = NativeTypeRef.Primitive(elementName, elementName, NativeAbiShape.Of(elementName))
                with { OwningFamilyId = typeRef.OwningFamilyId };

            return NativeTypeRef.Indirection(elementType, pointerDepth, typeRef.ManagedName);
        }

        return NativeTypeRef.Primitive(typeRef.ManagedName, typeRef.ManagedName, NativeAbiShape.Of(typeRef.ManagedName))
            with { OwningFamilyId = typeRef.OwningFamilyId };
    }

    private static int CountTrailingStars(string value)
    {
        var count = 0;
        for (var i = value.Length - 1; i >= 0 && value[i] == '*'; i--)
        {
            count++;
        }

        return count;
    }
}
