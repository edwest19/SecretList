<#
  ============================================================================
  SecretList
  Copyright (c) 2026 edwest19. Licensed under the MIT License (see LICENSE).
  AI-GENERATED CODE: This file was written by Claude (Anthropic). It is
  edwest19/SecretLead's Test-Msix.ps1 with SecretList's names.
  ============================================================================

  Checks a packaged SecretList MSIX before it is signed or published. Used by
  .github/workflows/build.yml (every pull request), and the release workflow once it exists.

  It fails unless:
    - the package's Publisher and Version are the expected ones;
    - it depends on no other package, so users install nothing else (self-contained);
    - it contains the app, SecretList.Core and the .NET runtime (coreclr.dll);
    - it asks for no permission except runFullTrust.
#>
param(
    [Parameter(Mandatory)] [string] $Path,
    [Parameter(Mandatory)] [string] $Version,
    [Parameter(Mandatory)] [string] $Publisher
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$fullPath = (Resolve-Path -LiteralPath $Path).Path
$zip = [System.IO.Compression.ZipFile]::OpenRead($fullPath)
try {
    $names = @($zip.Entries | ForEach-Object { $_.FullName })
    $entry = $zip.GetEntry('AppxManifest.xml')
    if ($null -eq $entry) { throw "$Path has no AppxManifest.xml." }
    $reader = [System.IO.StreamReader]::new($entry.Open())
    try { [xml] $manifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
}
finally {
    $zip.Dispose()
}

function Get-Nodes([string] $localName) {
    @($manifest.SelectNodes("//*[local-name()='$localName']"))
}

$identity = Get-Nodes 'Identity' | Select-Object -First 1
if ($identity.GetAttribute('Publisher') -cne $Publisher) {
    throw "Publisher is '$($identity.GetAttribute('Publisher'))'; expected '$Publisher'."
}
if ($identity.GetAttribute('Version') -cne $Version) {
    throw "Version is '$($identity.GetAttribute('Version'))'; expected '$Version'."
}

$dependencies = @(Get-Nodes 'PackageDependency' | ForEach-Object { $_.GetAttribute('Name') })
if ($dependencies.Count -gt 0) {
    throw "The MSIX depends on other packages ($($dependencies -join ', ')). It must be self-contained."
}

$capabilities = @(Get-Nodes 'Capabilities' | ForEach-Object { $_.ChildNodes } |
                  Where-Object { $_.NodeType -eq 'Element' } | ForEach-Object { $_.GetAttribute('Name') })
if (($capabilities -join ',') -cne 'runFullTrust') {
    throw "The MSIX asks for permissions '$($capabilities -join ', ')'. Only runFullTrust is allowed."
}

$executable = (Get-Nodes 'Application' | Select-Object -First 1).GetAttribute('Executable')
foreach ($required in @($executable, 'SecretList.Core.dll', 'coreclr.dll')) {
    if ($names -notcontains $required) {
        throw "The MSIX doesn't contain $required."
    }
}

$windowsAppSdk = @($names | Where-Object { $_ -match '^(Microsoft\.WindowsAppRuntime|Microsoft\.UI\.Xaml|Microsoft\.ui\.xaml)[^/]*\.dll$' })
$size = (Get-Item -LiteralPath $fullPath).Length
Write-Host "MSIX check passed: $(Split-Path -Leaf $fullPath), $($names.Count) files, $size bytes."
Write-Host "  Publisher:    $($identity.GetAttribute('Publisher'))"
Write-Host "  Version:      $($identity.GetAttribute('Version'))"
Write-Host "  App:          $executable"
Write-Host "  Dependencies: none (self-contained); .NET runtime included (coreclr.dll)"
Write-Host "  Permissions:  $($capabilities -join ', ')"
Write-Host "  Windows App SDK files: $($windowsAppSdk -join ', ')"
