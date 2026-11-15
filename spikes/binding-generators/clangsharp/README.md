# ppy-Style ClangSharp Spike

This spike tests whether ClangSharpPInvokeGenerator plus thin orchestration can produce the desired multi-TFM, multi-OS, source-generated `Janset.SDL2.Core` + `Janset.SDL2.Image` + `Janset.SDL2.GFX` + `Janset.SDL2.TTF` + `Janset.SDL2.Mixer` library packages.

Generation runs through the `GenerateBindings` Cake target, with read-only planning by default and
explicit `--execute`. See [maintenance](../../../docs/engineering/binding-generator-maintenance.md)
for full/single-family generation and evidence commands. Compat and Modern remain internal backends of one family artifact.

The [root tool manifest](../../../dotnet-tools.json) pins ClangSharp. The
[Cake wrapper](../../../build/_build/Tools/ClangSharp/) renders and captures one invocation.

[Platform-header shims](../../../build/binding-generation/shims/platform-headers/) supply missing system SDK
headers for Windows-local synthetic platform passes. Do not treat shim-enabled output as final production
evidence; real platform generation still needs native Linux/macOS runners.
