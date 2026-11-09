# Find-SecretListData.ps1
#
# Copyright (c) 2026 edwest19
#
# AI Disclaimer: This script was generated with the assistance of Claude
# (Anthropic AI), under the direction and review of edwest19.
#
# AI-generated (Claude): rewritten to find SecretList's folders by its package
# name instead of searching every package for records.txt, and to stop storing
# the results in $matches, which is PowerShell's automatic $Matches variable:
# the -match on the "Open in Explorer?" answer overwrote it, so the folders
# never opened.
#
# Purpose:
#   SecretList's data (records.txt and schema.md) lives in its MSIX package
#   data folder:
#     %LOCALAPPDATA%\Packages\<package name>_<publisher ID>\LocalState\
#   The package name is fixed (see Package.appxmanifest). The publisher ID
#   depends on who published the package, so development builds and signed
#   releases have different folders. This script lists every one it finds.
#
#   This script is READ-ONLY. It never writes, moves, or deletes anything.
#   It exists so you can find your own data to inspect it or copy it.

$ErrorActionPreference = 'Stop'

$packageName = 'C63ABE6F-95F6-41FC-A615-D59429F044E4'
$packagesRoot = Join-Path $env:LOCALAPPDATA 'Packages'

Write-Host "Looking for SecretList data folders ($packageName) under:" -ForegroundColor Cyan
Write-Host "  $packagesRoot`n"

$dataFolders = @(
    Get-ChildItem -LiteralPath $packagesRoot -Directory -Filter "${packageName}_*" -ErrorAction SilentlyContinue |
        ForEach-Object { Join-Path $_.FullName 'LocalState' } |
        Where-Object { Test-Path -LiteralPath $_ }
)

if ($dataFolders.Count -eq 0) {
    Write-Host "No SecretList data folder found." -ForegroundColor Yellow
    Write-Host "SecretList hasn't been run yet for this Windows user."
    return
}

foreach ($dataFolder in $dataFolders) {
    Write-Host "Found data folder:" -ForegroundColor Green
    Write-Host "  $dataFolder`n"

    foreach ($name in 'records.txt', 'schema.md') {
        $path = Join-Path $dataFolder $name
        if (Test-Path -LiteralPath $path) {
            $item = Get-Item -LiteralPath $path
            Write-Host ("  {0,-12} {1,10:N0} bytes   last modified {2}" -f $item.Name, $item.Length, $item.LastWriteTime)
        }
        else {
            Write-Host ("  {0,-12} (not present)" -f $name) -ForegroundColor DarkGray
        }
    }
    Write-Host ""
}

if ($dataFolders.Count -gt 1) {
    Write-Host "Note: more than one folder was found. Each belongs to a different publisher" -ForegroundColor Yellow
    Write-Host "(for example a development build and a signed release). Each has its own data.`n"
}

# Offer to open the folder(s) in Explorer - ask first, never auto-open.
$response = Read-Host "Open the folder(s) in Explorer? (y/N)"
if ($response -match '^[Yy]') {
    foreach ($dataFolder in $dataFolders) {
        Start-Process explorer.exe -ArgumentList "`"$dataFolder`""
    }
}
