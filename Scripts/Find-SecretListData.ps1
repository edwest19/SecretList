# Find-SecretListData.ps1
#
# Copyright (c) 2026 edwest19
#
# AI Disclaimer: This script was generated with the assistance of Claude
# (Anthropic AI), under the direction and review of edwest19.
#
# Purpose:
#   SecretList's data (records.txt, schema.md, print-debug.log) lives inside
#   Windows' per-package sandbox at:
#     %LOCALAPPDATA%\Packages\<PackageFamilyName>\LocalState\
#   The <PackageFamilyName> folder name is not a fixed, guessable string
#   (it depends on the debug/store package identity), so this script finds
#   it for you.
#
#   This script is READ-ONLY. It never writes, moves, or deletes anything.
#   It exists purely so you can locate your own sandboxed data if something
#   ever needs manual inspection or a manual backup.

$ErrorActionPreference = 'Stop'

$packagesRoot = Join-Path $env:LOCALAPPDATA 'Packages'

Write-Host "Searching for SecretList data under:" -ForegroundColor Cyan
Write-Host "  $packagesRoot`n"

# records.txt is the anchor file - if it exists, this is a live SecretList data folder
$matches = Get-ChildItem -Path $packagesRoot -Recurse -Filter 'records.txt' -ErrorAction SilentlyContinue

if (-not $matches -or $matches.Count -eq 0) {
    Write-Host "No records.txt found under $packagesRoot." -ForegroundColor Yellow
    Write-Host "This likely means SecretList hasn't been run yet, or was run unpackaged"
    Write-Host "(unpackaged debug runs don't use the per-package LocalState folder)."
    return
}

foreach ($match in $matches) {
    $dataFolder = $match.Directory.FullName

    Write-Host "Found data folder:" -ForegroundColor Green
    Write-Host "  $dataFolder`n"

    # List the known SecretList files in that folder, if present
    $knownFiles = 'records.txt', 'schema.md', 'print-debug.log'
    foreach ($name in $knownFiles) {
        $path = Join-Path $dataFolder $name
        if (Test-Path $path) {
            $item = Get-Item $path
            Write-Host ("  {0,-18} {1,10:N0} bytes   last modified {2}" -f $item.Name, $item.Length, $item.LastWriteTime)
        }
        else {
            Write-Host ("  {0,-18} (not present)" -f $name) -ForegroundColor DarkGray
        }
    }
    Write-Host ""
}

# If more than one match, these are likely different package identities
# (e.g. debug AUMID vs a Store-installed package) - both are legitimate sandboxes.
if ($matches.Count -gt 1) {
    Write-Host "Note: multiple package folders were found. Each corresponds to a" -ForegroundColor Yellow
    Write-Host "different package identity (e.g. debug vs. Store install) - this is normal`n"
}

# Offer to open the folder(s) in Explorer - ask first, never auto-open
$response = Read-Host "Open the folder in Explorer? (y/N)"
if ($response -match '^[Yy]') {
    foreach ($match in $matches) {
        Start-Process explorer.exe -ArgumentList $match.Directory.FullName
    }
}