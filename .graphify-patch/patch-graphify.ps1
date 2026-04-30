#!/usr/bin/env pwsh
# Patches the installed graphify package's detect.py so `/graphify .` from this
# repo's root will include `build/` and `.github/` in the scanned corpus.
# See README.md for rationale and verification steps.

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet('patch', 'unpatch')]
    [string]$Action
)

$ErrorActionPreference = 'Stop'

# --- locate the installed detect.py via the active Python interpreter ---
$detectPath = (& python -c "import graphify.detect; print(graphify.detect.__file__)" 2>$null) | Select-Object -First 1
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($detectPath)) {
    throw "Could not locate graphify.detect via Python. Is graphifyy installed in the active interpreter?"
}
$detectPath = (Resolve-Path $detectPath).Path
$backupPath = "$detectPath.bak"

# --- patch markers (literal strings, must be unique inside detect.py) ---
$skipDirsBefore = '"dist", "build", "target", "out",'
$skipDirsAfter  = '"dist", "target", "out",'

$hiddenBefore = 'if not d.startswith(".")'
$hiddenAfter  = 'if (not d.startswith(".") or d == ".github")'

$docExtBefore = "DOC_EXTENSIONS = {'.md', '.mdx', '.txt', '.rst', '.html'}"
$docExtAfter  = "DOC_EXTENSIONS = {'.md', '.mdx', '.txt', '.rst', '.html', '.yml', '.yaml', '.json'}"

function Read-FileText([string]$path) {
    return [System.IO.File]::ReadAllText($path)
}
function Write-FileText([string]$path, [string]$content) {
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($path, $content, $utf8NoBom)
}

switch ($Action) {
    'patch' {
        if (Test-Path $backupPath) {
            throw "Backup already exists at '$backupPath'. Looks like detect.py is already patched. Run 'unpatch' first, or delete the backup manually if you know what you're doing."
        }

        $content = Read-FileText $detectPath

        if (-not $content.Contains($skipDirsBefore)) {
            throw "Marker not found: '$skipDirsBefore'. graphify upstream may have changed shape — inspect '$detectPath' manually before patching."
        }
        if (-not $content.Contains($hiddenBefore)) {
            throw "Marker not found: '$hiddenBefore'. graphify upstream may have changed shape — inspect '$detectPath' manually before patching."
        }
        if (-not $content.Contains($docExtBefore)) {
            throw "Marker not found: '$docExtBefore'. graphify upstream may have changed shape — inspect '$detectPath' manually before patching."
        }

        # Backup BEFORE writing so we can always recover the original.
        Copy-Item -LiteralPath $detectPath -Destination $backupPath

        $patched = $content.Replace($skipDirsBefore, $skipDirsAfter)
        $patched = $patched.Replace($hiddenBefore,   $hiddenAfter)
        $patched = $patched.Replace($docExtBefore,   $docExtAfter)

        Write-FileText $detectPath $patched

        # Re-read and verify all edits actually stuck on disk.
        $verify = Read-FileText $detectPath
        if ($verify.Contains($skipDirsBefore)) {
            throw "Patch verification failed: 'build' is still in _SKIP_DIRS. Restore from '$backupPath'."
        }
        if (-not $verify.Contains($hiddenAfter)) {
            throw "Patch verification failed: '.github' allowance not present. Restore from '$backupPath'."
        }
        if (-not $verify.Contains($docExtAfter)) {
            throw "Patch verification failed: DOC_EXTENSIONS expansion not present. Restore from '$backupPath'."
        }

        Write-Host "Patched:  $detectPath"
        Write-Host "Backup:   $backupPath"
        Write-Host ""
        Write-Host "build/ and .github/ are now scannable by /graphify ."
        Write-Host ".yml/.yaml/.json files are now classified as documents."
        Write-Host "Run unpatch if you ever need stock graphify behavior back."
    }
    'unpatch' {
        if (-not (Test-Path $backupPath)) {
            throw "No backup at '$backupPath'. Nothing to restore."
        }

        Move-Item -LiteralPath $backupPath -Destination $detectPath -Force

        $verify = Read-FileText $detectPath
        if (-not $verify.Contains($skipDirsBefore)) {
            throw "Unpatch verification failed: original marker '$skipDirsBefore' not present after restore. Inspect '$detectPath' manually."
        }
        if (-not $verify.Contains($hiddenBefore)) {
            throw "Unpatch verification failed: original marker '$hiddenBefore' not present after restore. Inspect '$detectPath' manually."
        }
        if (-not $verify.Contains($docExtBefore)) {
            throw "Unpatch verification failed: original DOC_EXTENSIONS marker not present after restore. Inspect '$detectPath' manually."
        }

        Write-Host "Restored: $detectPath"
        Write-Host "Backup removed."
    }
}
