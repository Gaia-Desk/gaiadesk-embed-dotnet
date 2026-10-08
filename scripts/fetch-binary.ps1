<#
.SYNOPSIS
  Download the native gaiadesk_embed.dll for a release into runtimes/win-x64/native/.

.EXAMPLE
  ./scripts/fetch-binary.ps1 0.1.0
#>
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidatePattern('^\d+\.\d+\.\d+([-.][0-9A-Za-z.-]+)?$')]
    [string]$Version
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot
$dest = Join-Path $root 'runtimes/win-x64/native'
$url = "https://github.com/Gaia-Desk/gaiadesk-embed-dotnet/releases/download/v$Version/gaiadesk_embed-win-x64.zip"

$tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("gaiadesk-embed-" + [System.Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tmp | Out-Null
try {
    $zip = Join-Path $tmp 'gaiadesk_embed-win-x64.zip'
    Write-Host "Downloading $url"
    Invoke-WebRequest -Uri $url -OutFile $zip -UseBasicParsing

    $extract = Join-Path $tmp 'x'
    Expand-Archive -Path $zip -DestinationPath $extract
    $dll = Get-ChildItem -Path $extract -Recurse -Filter 'gaiadesk_embed.dll' | Select-Object -First 1
    if (-not $dll) {
        throw "gaiadesk_embed.dll is not in $url"
    }

    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    Copy-Item -Path $dll.FullName -Destination (Join-Path $dest 'gaiadesk_embed.dll') -Force
    $licence = Get-ChildItem -Path $extract -Recurse -Filter 'LICENSE-BINARY' | Select-Object -First 1
    if ($licence) {
        Copy-Item -Path $licence.FullName -Destination (Join-Path $dest 'LICENSE-BINARY') -Force
    }
    $hash = (Get-FileHash -Algorithm SHA256 (Join-Path $dest 'gaiadesk_embed.dll')).Hash.ToLowerInvariant()
    Write-Host "gaiadesk_embed.dll $Version -> $dest (sha256 $hash)"
}
finally {
    Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue
}
