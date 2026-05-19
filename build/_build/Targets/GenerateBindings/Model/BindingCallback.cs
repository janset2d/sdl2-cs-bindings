namespace Build.Targets.GenerateBindings.Model;

/// <summary>
/// Public function-pointer typedef in the binding surface (e.g.
/// <c>SDL_EventFilter</c>, <c>SDL_AudioCallback</c>, <c>SDL_HintCallback</c>).
/// <c>CsCallbackEmitter</c> emits these as
/// <c>delegate* unmanaged[Cdecl]&lt;...&gt;</c> where representable;
/// <c>[UnmanagedFunctionPointer]</c> + <c>delegate</c> shapes lend themselves to
/// legacy-TFM friendly overloads in Phase 3F.
/// </summary>
public sealed record BindingCallback(
    string Name,
    NativeTypeRef ReturnType,
    IReadOnlyList<BindingParameter> Parameters);
