<#
.SYNOPSIS
    Builds the Kazo.Jarvis release zip: the application (Kazo.Jarvis.Windows) and Kazo.Jarvis.Updater,
    self-contained, win-x64, in one folder (ADR 0001, section 4; US1.18).

.DESCRIPTION
    Output: artifacts/Kazo.Jarvis-<Version>-win-x64.zip, files at the root of the zip.
    The update check looks for an asset named exactly "Kazo.Jarvis-<version>-win-x64.zip" in each GitHub
    release of Kazo-ca/Jarvis, and reads the version from the tag, "v<Version>" (v0.2.0 or v0.2.0-beta.1).
    Publish 0.x versions as pre-releases. Runs on Windows or on Linux with PowerShell 7.

.EXAMPLE
    ./build/Publish-Release.ps1 -Version 0.2.0
    gh release create v0.2.0 artifacts/Kazo.Jarvis-0.2.0-win-x64.zip --prerelease --title "Kazo.Jarvis 0.2.0"
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+(-[0-9A-Za-z.]+)?$')]
    [string] $Version,

    # Where the zip goes; artifacts/ at the root of the repository by default.
    [string] $Output
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $Output) { $Output = Join-Path $root 'artifacts' }
$runtime = 'win-x64'
$name = "Kazo.Jarvis-$Version-$runtime"
$folder = Join-Path $Output $name
$zip = Join-Path $Output "$name.zip"

if (Test-Path $folder) { Remove-Item $folder -Recurse -Force }
if (Test-Path $zip) { Remove-Item $zip -Force }

# Same runtime and version for both programs: they share the runtime files of the folder.
$common = @('-c', 'Release', '-r', $runtime, '--self-contained', 'true', "-p:Version=$Version", '-o', $folder)
foreach ($project in 'src/Kazo.Jarvis.Windows/Kazo.Jarvis.Windows.csproj', 'src/Kazo.Jarvis.Updater/Kazo.Jarvis.Updater.csproj') {
    Write-Host "Publishing $project ($Version, $runtime)..."
    dotnet publish (Join-Path $root $project) @common
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $project" }
}

foreach ($exe in 'Kazo.Jarvis.Windows.exe', 'Kazo.Jarvis.Updater.exe') {
    if (-not (Test-Path (Join-Path $folder $exe))) { throw "$exe is missing from $folder" }
}

Compress-Archive -Path (Join-Path $folder '*') -DestinationPath $zip
Write-Host "Done: $zip"
Write-Host "Next: gh release create v$Version `"$zip`" --prerelease --title `"Kazo.Jarvis $Version`""
