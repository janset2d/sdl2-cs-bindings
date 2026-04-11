# Playbook: Adding a New SDL Satellite Library

> Step-by-step recipe for adding a new SDL2 (or SDL3) satellite library to the project.

## Example: Adding SDL2_net

This walkthrough uses SDL2_net as the example, but the process is the same for any satellite.

## Step 1: Update vcpkg.json

Add the new library as a dependency with its features:

```json
{
  "name": "sdl2-net"
}
```

If the library has optional features, add them:

```json
{
  "name": "sdl2-mixer",
  "features": ["mpg123", "libflac", "opusfile", "libmodplug", "wavpack"]
}
```

Add a version override:

```json
{
  "name": "sdl2-net",
  "version": "2.2.0",
  "port-version": 3
}
```

## Step 2: Update build/manifest.json

Add a new entry to `library_manifests`:

```json
{
  "name": "SDL2_net",
  "vcpkg_name": "sdl2-net",
  "vcpkg_version": "2.2.0",
  "vcpkg_port_version": 3,
  "native_lib_name": "SDL2.Net.Native",
  "native_lib_version": "2.2.0.0",
  "core_lib": false,
  "primary_binaries": [
    {
      "os": "Windows",
      "patterns": ["SDL2_net.dll"]
    },
    {
      "os": "Linux",
      "patterns": ["libSDL2_net*"]
    },
    {
      "os": "OSX",
      "patterns": ["libSDL2_net*.dylib"]
    }
  ]
}
```

## Step 3: Create Binding Project

Create `src/SDL2.Net/SDL2.Net.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="../SDL2.Core/SDL2.Core.csproj" />
    <ProjectReference Include="../native/SDL2.Net.Native/SDL2.Net.Native.csproj" />
  </ItemGroup>

  <PropertyGroup>
    <PackageId>Janset.SDL2.Net</PackageId>
    <Description>C# bindings for SDL2_net — cross-platform networking.</Description>
    <PackageTags>$(PackageTags);networking;tcp;udp</PackageTags>
  </PropertyGroup>

  <!-- Include binding source -->
  <!-- NOTE: SDL2_net is NOT part of flibitijibibo/SDL2-CS -->
  <!-- You'll need to write or find a community binding -->
  <ItemGroup>
    <Compile Include="SDL2_net.cs" />
  </ItemGroup>
</Project>
```

**Important**: Unlike other satellites, SDL2_net bindings are NOT included in the SDL2-CS submodule. You'll need to either:
- Write P/Invoke declarations manually (~20 functions, small API)
- Find a community binding file
- Wait for Phase 4 auto-generation

## Step 4: Create Native Package Project

Create `src/native/SDL2.Net.Native/`:

```
SDL2.Net.Native/
├── SDL2.Net.Native.csproj
├── runtimes/              ← Populated by harvest pipeline
│   ├── win-x64/native/
│   ├── win-arm64/native/
│   ├── linux-x64/native/
│   └── ...
└── buildTransitive/       ← Optional: MSBuild targets for .NET Framework
    └── Janset.SDL2.Net.Native.targets
```

`SDL2.Net.Native.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <PackageId>Janset.SDL2.Net.Native</PackageId>
    <Description>Native SDL2_net libraries for all supported platforms.</Description>
  </PropertyGroup>
</Project>
```

The shared `src/native/Directory.Build.props` handles most of the packaging configuration.

## Step 5: Add to Solution

```bash
dotnet sln Janset.SDL2.sln add src/SDL2.Net/SDL2.Net.csproj --solution-folder src
dotnet sln Janset.SDL2.sln add src/native/SDL2.Net.Native/SDL2.Net.Native.csproj --solution-folder src/native
```

## Step 6: Verify vcpkg Build

Test that vcpkg can build the new library:

```bash
# For your platform
./external/vcpkg/vcpkg install sdl2-net --triplet x64-windows-release
```

## Step 7: Test Harvest

```bash
cd build/_build
dotnet run -- --target Harvest --library SDL2_net --rid win-x64
```

Check `artifacts/harvest_output/SDL2_net/rid-status/win-x64.json` for results.

## Step 8: Update Documentation

- [ ] Add library to `docs/onboarding.md` satellite coverage table
- [ ] Add version to `docs/plan.md` version tracking table
- [ ] Update `README.md` if the library is user-facing
- [ ] Update meta-package dependency list (when meta-package exists)

## Checklist

- [ ] vcpkg.json updated with dependency + features + version override
- [ ] manifest.json updated with library definition
- [ ] Binding project created (`src/SDL2.{Name}/`)
- [ ] Native package project created (`src/native/SDL2.{Name}.Native/`)
- [ ] Added to solution
- [ ] vcpkg build verified locally
- [ ] Harvest tested for at least one RID
- [ ] Documentation updated
