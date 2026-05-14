// SPDX-License-Identifier: MIT
// Standard ClangSharp helper attribute, manually-provided (consumer responsibility).
// Mirrors the definition used by ppy/SDL3-CS, terrafx/*, dotnet/win32metadata, etc.
// Stripped from Release builds via [Conditional("DEBUG")] — informational only.

using System;
using System.Diagnostics;

namespace Janset.Spike.SDL2.Gfx;

[AttributeUsage(
    AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Property |
    AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.ReturnValue,
    AllowMultiple = false,
    Inherited = true)]
[Conditional("DEBUG")]
internal sealed class NativeTypeNameAttribute : Attribute
{
    public NativeTypeNameAttribute(string name)
    {
        Name = name;
    }

    public string Name { get; }
}
