namespace Build.Targets.GenerateBindings.Model;

/// <summary>
/// Opaque-handle type (e.g. <c>SDL_Window</c>, <c>SDL_Renderer</c>, <c>SDL_Texture</c>).
/// <c>CsHandleEmitter</c> emits each as
/// <c>readonly partial struct &lt;Name&gt;(nint value)</c> with the full Rule 2
/// feature set: <c>IsNull</c>, <c>Null</c>, <c>IEquatable&lt;T&gt;</c>, implicit
/// <c>nint</c> conversion, equality operators, <c>[DebuggerDisplay]</c>. Holds no
/// fields beyond <see cref="Name"/> because the handle shape is uniform across
/// all opaque types — only the name varies.
/// </summary>
public sealed record BindingHandle(string Name);
