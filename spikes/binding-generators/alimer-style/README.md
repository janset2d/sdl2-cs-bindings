# Alimer-Style CppAst Spike

This spike tests whether a direct, SDL-aware CppAst generator can stay readable while handling SDL2.Core plus SDL2_image.

Principles:

- Prefer explicit loops, `if`, and `switch`.
- Keep SDL policy visible and local.
- Avoid DI, build-host abstractions, validators, and profile frameworks.
- Start Windows-local-debugging-first.

Initial command:

```pwsh
dotnet run --project spikes/binding-generators/alimer-style/src/Janset.Sdl2.AlimerSpike.Generator/Janset.Sdl2.AlimerSpike.Generator.csproj -- --vcpkg-triplet x64-windows-hybrid
```
