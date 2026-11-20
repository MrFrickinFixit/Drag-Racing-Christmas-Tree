[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$IsccPath,
    [string]$OutputRoot
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($Version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$') { throw 'Version must be SemVer, such as 1.0.0 or 1.1.0-beta.1.' }
$numericVersion = ($Version -split '-')[0]
foreach ($part in $numericVersion.Split('.')) { if ([long]$part -gt 65535) { throw 'Version components must be at most 65535.' } }
$repo = Split-Path -Parent $PSScriptRoot
if (-not $OutputRoot) { $OutputRoot = Join-Path $repo "artifacts\v$Version" }
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
if ((Test-Path -LiteralPath $OutputRoot) -and @(Get-ChildItem -LiteralPath $OutputRoot -Force).Count -gt 0) { throw "Output folder is not empty: $OutputRoot. Choose a fresh -OutputRoot or a new version." }

if (-not $IsccPath) {
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) { $IsccPath = $command.Source }
    else {
        $candidates = @(
            (Join-Path $repo '.tools\InnoSetup\ISCC.exe'),
            'C:\Program Files\Inno Setup 7\ISCC.exe',
            'C:\Program Files (x86)\Inno Setup 7\ISCC.exe',
            'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
            (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7\ISCC.exe'),
            (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
        )
        $IsccPath = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    }
}
if (-not $IsccPath -or -not (Test-Path -LiteralPath $IsccPath)) { throw 'Install Inno Setup 6.3+ / 7, or supply -IsccPath with the full path to ISCC.exe.' }

function Invoke-Dotnet([string[]]$Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed (exit $LASTEXITCODE): $($Arguments -join ' ')" }
}

Invoke-Dotnet -Arguments @('run', '--project', (Join-Path $repo 'tests\PracticeTree.Checks.csproj'), '-c', 'Release', '--no-launch-profile')
New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null
$publish = Join-Path $OutputRoot 'publish'
Invoke-Dotnet -Arguments @('publish', (Join-Path $repo 'PracticeTree.csproj'), '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '-o', $publish, "-p:Version=$Version", '-p:PublishTrimmed=false', '-p:DebugType=None', '-p:DebugSymbols=false')
$runtime = Get-Content -LiteralPath (Join-Path $publish 'PracticeTree.runtimeconfig.json') -Raw | ConvertFrom-Json
if (-not ($runtime.runtimeOptions.PSObject.Properties.Name -contains 'includedFrameworks')) { throw 'Publish is not self-contained.' }
foreach ($required in @('PracticeTree.exe', 'PracticeTree.dll', 'coreclr.dll', 'hostfxr.dll', 'PresentationFramework.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $publish $required))) { throw "Missing published file: $required" }
}
Copy-Item -LiteralPath (Join-Path $repo 'README.md') -Destination (Join-Path $publish 'README.md')
if (Test-Path -LiteralPath (Join-Path $repo 'docs')) { Copy-Item -LiteralPath (Join-Path $repo 'docs') -Destination (Join-Path $publish 'docs') -Recurse }
$zip = Join-Path $OutputRoot "PracticeTree-$Version-win-x64.zip"
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $zip -CompressionLevel Optimal
& $IsccPath "/DAppVersion=$Version" "/DFileVersion=$numericVersion.0" "/DPublishDir=$publish" "/DInstallerOutputDir=$OutputRoot" (Join-Path $PSScriptRoot 'PracticeTree.iss')
if ($LASTEXITCODE -ne 0) { throw "Inno Setup compilation failed: $LASTEXITCODE" }
$setup = Join-Path $OutputRoot "PracticeTree-$Version-win-x64-Setup.exe"
if (-not (Test-Path -LiteralPath $setup)) { throw 'Installer output was not created.' }
$lines = foreach ($file in @($setup, $zip)) {
    $hash = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $([IO.Path]::GetFileName($file))"
}
[IO.File]::WriteAllLines((Join-Path $OutputRoot 'SHA256SUMS.txt'), [string[]]$lines, [Text.UTF8Encoding]::new($false))
Write-Host "Release artifacts: $OutputRoot"
