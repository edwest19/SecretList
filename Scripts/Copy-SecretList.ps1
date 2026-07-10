# Backup-SecretList.ps1
#
# Copies the SecretList repo (excluding bin/ and obj/) into a timestamped
# folder under a destination root, then zips that same copy into a
# timestamped .zip right alongside it. Run this from anywhere - it doesn't
# need to live in the repo itself.
#
# Usage:
#   .\Backup-SecretList.ps1 -SourceRepo "C:\Users\edwes\Projects\SecretList" -DestRoot "C:\Users\edwes\OneDrive\Secret List"
#
# Both parameters have defaults below, so it still runs with no arguments -
# edit the defaults if you want, or just pass -SourceRepo/-DestRoot each time.

param(
    [string]$SourceRepo = "C:\Users\edwes\Projects\SecretList",
    [string]$DestRoot = "C:\Users\edwes\OneDrive\Secret List"
)

$Timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$DestFolder = Join-Path $DestRoot "SecretList_$Timestamp"
$ZipPath = Join-Path $DestRoot "SecretList_$Timestamp.zip"

if (-not (Test-Path $SourceRepo)) {
    Write-Error "Source repo path not found: $SourceRepo"
    exit 1
}

if (-not (Test-Path $DestRoot)) {
    New-Item -ItemType Directory -Path $DestRoot -Force | Out-Null
}

Write-Host "Source repo:"
Write-Host "  $SourceRepo"
Write-Host "Copying repo (excluding bin/obj) to:"
Write-Host "  $DestFolder"

# Robocopy's /XD excludes directories by name at any depth - this skips
# every bin/ and obj/ folder in the tree, not just top-level ones.
robocopy $SourceRepo $DestFolder /E /XD bin obj /NFL /NDL /NJH /NJS /NC /NS /NP

# Robocopy's "success" exit codes are 0-7, not just 0 - anything 8+ is a
# real failure (e.g. access denied, path too long).
if ($LASTEXITCODE -ge 8) {
    Write-Error "Robocopy reported a failure (exit code $LASTEXITCODE). Aborting before zipping."
    exit 1
}

Write-Host "Copy complete. Creating zip:"
Write-Host "  $ZipPath"

Compress-Archive -Path (Join-Path $DestFolder "*") -DestinationPath $ZipPath -Force

Write-Host ""
Write-Host "Done."
Write-Host "  Folder: $DestFolder"
Write-Host "  Zip:    $ZipPath"