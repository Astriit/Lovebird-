# Rebuild Lovebird 1.0 (needs .NET 8 SDK and 7-Zip on PATH)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$stage = Join-Path $env:TEMP 'lovebird_stage'

if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Path $stage | Out-Null

dotnet publish (Join-Path $root 'src/Lovebird/Lovebird.csproj') -c Release -r win-x64 `
  --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $stage

Copy-Item (Join-Path $root 'payload/*') (Join-Path $stage 'payload') -Recurse -Force
Copy-Item (Join-Path $root 'docs') $stage -Recurse -Force
Copy-Item (Join-Path $root 'README.txt') $stage -Force

7z a -t7z -mx=9 -m0=lzma2:d=32m -ms=on -y (Join-Path $root 'payload.7z') "$stage\*"

dotnet publish (Join-Path $root 'src/Lovebird.Setup/Lovebird.Setup.csproj') -c Release -r win-x64 `
  --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -o (Join-Path $env:TEMP 'lovebird_setup')

Copy-Item (Join-Path $stage 'Lovebird.exe') $root -Force
Copy-Item (Join-Path $env:TEMP 'lovebird_setup/Lovebird Setup.exe') $root -Force
Write-Host 'Done — Lovebird.exe and Lovebird Setup.exe are updated.'
