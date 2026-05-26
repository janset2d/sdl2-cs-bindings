# ppy-Style ClangSharp Spike

This spike tests whether ClangSharpPInvokeGenerator plus thin orchestration can produce the desired SDL2.Core plus SDL2_image binding shape with less owned code.

Commands:

```pwsh
python spikes/binding-generators/clangsharp/generate_bindings.py --family all --vcpkg-triplet x64-windows-hybrid
python spikes/binding-generators/clangsharp/generate_bindings.py --family all --execute --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims
python spikes/binding-generators/clangsharp/generate_bindings.py --family core --execute --vcpkg-triplet x64-windows-hybrid --use-platform-header-shims
```

The script is dry-run by default and prints commands only. Add `--execute` to clean the selected family/families' `Generated/` roots and run ClangSharp through the spike-local .NET tool manifest. Compat and Modern are generated together; they are internal backends, not CLI units.

`--use-platform-header-shims` is a Windows-local spike aid for synthetic Linux/macOS/iOS platform passes. It adds `shims/platform-headers/` for missing system SDK headers such as `endian.h`, `AvailabilityMacros.h`, and `TargetConditionals.h`. Do not treat shim-enabled output as final production evidence; real platform generation still needs native Linux/macOS runners.
