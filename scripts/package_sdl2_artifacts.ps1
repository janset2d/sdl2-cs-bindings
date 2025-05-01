# Define base directories
$ARTIFACT_BASE_DIR = "E:\temp\sdl-artifacts"
$VCPKG_DIR = "E:\repos\os-github-repos\vcpkg\installed\x64-windows-release"
$VCPKG_SHARE_DIR = "$VCPKG_DIR\share"

# Initialize hashtable to track copied DLLs and their artifact directories
$copiedDlls = @{}

# Function to get dependencies of a DLL using dumpbin
function Get-Dependencies {
    param (
        [string]$dllPath
    )
    $dumpbinOutput = & "dumpbin" /dependents $dllPath | Where-Object { $_ -match "\.dll" }
    $dependencies = $dumpbinOutput | ForEach-Object { $_.Trim() } | Where-Object { $_ -notmatch "Dump of file" }
    return $dependencies
}

# Function to collect all dependencies with one level of recursion
function Collect-AllDependencies {
    param (
        [string]$dllPath,
        [ref]$visited
    )
    $deps = @()
    $dllName = Split-Path $dllPath -Leaf
    if ($visited.Value -contains $dllName) { return $deps }
    $visited.Value += $dllName

    # Get direct dependencies
    $dependencies = Get-Dependencies $dllPath
    foreach ($dep in $dependencies) {
        $depPath = "$VCPKG_DIR\bin\$dep"
        if (Test-Path $depPath) {
            $deps += $depPath
            # Recurse one level: get dependencies of this dependency
            $subDependencies = Get-Dependencies $depPath
            foreach ($subDep in $subDependencies) {
                $subDepPath = "$VCPKG_DIR\bin\$subDep"
                if ((Test-Path $subDepPath) -and ($subDepPath -notin $deps)) {
                    $deps += $subDepPath
                }
            }
        }
    }
    return $deps | Sort-Object | Get-Unique
}

# Function to map DLLs to vcpkg package names
function Get-PackageName {
    param (
        [string]$dllName
    )
    $packageMap = @{
        "SDL2.dll" = "sdl2"
        "SDL2_image.dll" = "sdl2-image"
        "SDL2_gfx.dll" = "sdl2-gfx"
        "SDL2_mixer.dll" = "sdl2-mixer"
        "SDL2_ttf.dll" = "sdl2-ttf"
        "avif.dll" = "libavif"
        "jpeg62.dll" = "libjpeg-turbo"
        "libpng16.dll" = "libpng"
        "tiff.dll" = "tiff"
        "libwebp.dll" = "libwebp"
        "libwebpdemux.dll" = "libwebp"
        "libyuv.dll" = "libyuv"
        "liblzma.dll" = "liblzma"
        "libsharpyuv.dll" = "libwebp"
        "zlib1.dll" = "zlib"
        "freetype.dll" = "freetype"
        "brotlicommon.dll" = "brotli"
        "brotlidec.dll" = "brotli"
        "bz2.dll" = "bzip2"
        "FLAC.dll" = "libflac"
        "fluidsynth-3.dll" = "fluidsynth"
        "glib-2.0-0.dll" = "glib"
        "iconv-2.dll" = "libiconv"
        "intl-8.dll" = "libintl"
        "modplug.dll" = "libmodplug"
        "libxmp.dll" = "libxmp"
        "mpg123.dll" = "mpg123"
        "ogg.dll" = "libogg"
        "opus.dll" = "opus"
        "pcre2-8.dll" = "pcre2"
        "vorbis.dll" = "libvorbis"
        "vorbisfile.dll" = "libvorbis"
        "wavpackdll.dll" = "wavpack"
    }
    return $packageMap[$dllName]
}

# List of libraries to package
$libraries = @("SDL2.dll", "SDL2_image.dll", "SDL2_gfx.dll", "SDL2_mixer.dll", "SDL2_ttf.dll")

foreach ($lib in $libraries) {
    Write-Host "Packaging $lib..." -ForegroundColor Cyan

    # Create artifact directory
    $libBaseName = $lib -replace '\.dll$', ''
    $artifactDir = "$ARTIFACT_BASE_DIR\$libBaseName\native"
    New-Item -Path $artifactDir -ItemType Directory -Force | Out-Null

    # Copy main DLL and track it
    $dllPath = "$VCPKG_DIR\bin\$lib"
    if (Test-Path $dllPath) {
        Copy-Item $dllPath -Destination $artifactDir
        Write-Host "  Copied $lib to $artifactDir" -ForegroundColor Green
        if (-not $copiedDlls.ContainsKey($lib)) {
            $copiedDlls[$lib] = @($artifactDir)
        } elseif ($artifactDir -notin $copiedDlls[$lib]) {
            $copiedDlls[$lib] += $artifactDir
        }
    } else {
        Write-Host "  Error: $lib not found at $dllPath" -ForegroundColor Red
        continue
    }

    # Collect dependencies
    Write-Host "  Collecting dependencies for $lib..." -ForegroundColor Yellow
    $visited = New-Object System.Collections.ArrayList
    $dependencies = Collect-AllDependencies -dllPath $dllPath -visited ([ref]$visited)

    # Copy dependencies, excluding SDL2.dll for satellite libraries, and track them
    foreach ($depPath in $dependencies) {
        $depName = Split-Path $depPath -Leaf
        if ($depName -ne "SDL2.dll" -or $lib -eq "SDL2.dll") {
            Copy-Item $depPath -Destination $artifactDir
            Write-Host "    Copied dependency $depName" -ForegroundColor Green
            if (-not $copiedDlls.ContainsKey($depName)) {
                $copiedDlls[$depName] = @($artifactDir)
            } elseif ($artifactDir -notin $copiedDlls[$depName]) {
                $copiedDlls[$depName] += $artifactDir
            }

            # Explicitly include libxmp.dll for SDL2_mixer.dll if modplug.dll is present
            if ($lib -eq "SDL2_mixer.dll" -and $depName -eq "modplug.dll") {
                $libxmpPath = "$VCPKG_DIR\bin\libxmp.dll"
                if (Test-Path $libxmpPath -and ($libxmpPath -notin $dependencies)) {
                    Copy-Item $libxmpPath -Destination $artifactDir
                    Write-Host "    Copied dependency libxmp.dll (required by modplug.dll)" -ForegroundColor Green
                    if (-not $copiedDlls.ContainsKey("libxmp.dll")) {
                        $copiedDlls["libxmp.dll"] = @($artifactDir)
                    } elseif ($artifactDir -notin $copiedDlls["libxmp.dll"]) {
                        $copiedDlls["libxmp.dll"] += $artifactDir
                    }
                }
            }
        } else {
            Write-Host "    Skipped copying SDL2.dll for $lib" -ForegroundColor Yellow
        }
    }

    # Create license directory
    $licenseDir = "$ARTIFACT_BASE_DIR\$libBaseName\licenses"
    New-Item -Path $licenseDir -ItemType Directory -Force | Out-Null

    # Copy license for the main library
    $mainPackage = Get-PackageName -dllName $lib
    $mainLicensePath = "$VCPKG_SHARE_DIR\$mainPackage\copyright"
    if (Test-Path $mainLicensePath) {
        Copy-Item $mainLicensePath -Destination "$licenseDir\$mainPackage.txt"
        Write-Host "  Copied license for $mainPackage" -ForegroundColor Green
    } else {
        Write-Host "  Warning: License file not found for $mainPackage at $mainLicensePath" -ForegroundColor Yellow
    }

    # Always include SDL2 license for satellite libraries
    if ($lib -ne "SDL2.dll") {
        $sdl2LicensePath = "$VCPKG_SHARE_DIR\sdl2\copyright"
        if (Test-Path $sdl2LicensePath) {
            Copy-Item $sdl2LicensePath -Destination "$licenseDir\sdl2.txt"
            Write-Host "  Copied license for sdl2 (dependency)" -ForegroundColor Green
        } else {
            Write-Host "  Warning: License file not found for sdl2 at $sdl2LicensePath" -ForegroundColor Yellow
        }
    }

    # Copy licenses for other dependencies (excluding SDL2.dll for satellite libraries)
    foreach ($depPath in $dependencies) {
        $depName = Split-Path $depPath -Leaf
        if ($depName -ne "SDL2.dll") {
            $depPackage = Get-PackageName -dllName $depName
            if ($depPackage) {
                $depLicensePath = "$VCPKG_SHARE_DIR\$depPackage\copyright"
                if (Test-Path "$licenseDir\$depPackage.txt") {
                    continue
                }
                if (Test-Path $depLicensePath) {
                    Copy-Item $depLicensePath -Destination "$licenseDir\$depPackage.txt"
                    Write-Host "  Copied license for $depPackage (dependency $depName)" -ForegroundColor Green
                } else {
                    Write-Host "  Warning: License file not found for $depPackage at $depLicensePath" -ForegroundColor Yellow
                }
            }
        }
    }

    # Copy license for libxmp if modplug.dll is present
    if ($lib -eq "SDL2_mixer.dll" -and ($dependencies | Where-Object { (Split-Path $_ -Leaf) -eq "modplug.dll" })) {
        $libxmpLicensePath = "$VCPKG_SHARE_DIR\libxmp\copyright"
        if (Test-Path $libxmpLicensePath -and -not (Test-Path "$licenseDir\libxmp.txt")) {
            Copy-Item $libxmpLicensePath -Destination "$licenseDir\libxmp.txt"
            Write-Host "  Copied license for libxmp (dependency for modplug.dll)" -ForegroundColor Green
        }
    }
}

# Sanity check for overlapping DLLs with hash comparison
Write-Host "Performing sanity check for overlapping DLLs..." -ForegroundColor Cyan
$overlapsFound = $false

foreach ($dll in $copiedDlls.Keys) {
    $dirs = $copiedDlls[$dll]
    if ($dirs.Count -gt 1) {
        $overlapsFound = $true
        Write-Host "Overlap detected: $dll appears in:" -ForegroundColor Yellow
        $hashes = @{}
        foreach ($dir in $dirs) {
            $dllPath = Join-Path $dir $dll
            $hash = (Get-FileHash -Path $dllPath -Algorithm SHA256).Hash
            Write-Host "  - $dir (Hash: $hash)" -ForegroundColor Yellow
            $hashes[$dir] = $hash
        }
        if (($hashes.Values | Select-Object -Unique | Measure-Object).Count -eq 1) {
            Write-Host "    OK: All instances of $dll are identical (likely not a problem)" -ForegroundColor Green
        } else {
            Write-Host "    WARNING: Different versions of $dll detected (potential issue)" -ForegroundColor Red
        }
    }
}

if (-not $overlapsFound) {
    Write-Host "No overlapping DLLs found." -ForegroundColor Green
} else {
    Write-Host "Sanity check complete with overlaps detected." -ForegroundColor Yellow
}

Write-Host "Packaging complete. Artifacts are in $ARTIFACT_BASE_DIR." -ForegroundColor Cyan