param([string]$Version = '1.0.0')
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.dotnet'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$output = Join-Path $PSScriptRoot 'artifacts\installer'
$publish = Join-Path $output 'app'
if (Test-Path $publish) { Remove-Item -LiteralPath $publish -Recurse -Force }
dotnet publish (Join-Path $PSScriptRoot 'VibeVocalStudio.csproj') -c Release -r win-x64 --self-contained true -o $publish -p:Version=$Version
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$iscc = @(
    (Get-Command iscc -ErrorAction SilentlyContinue).Source,
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 não encontrado. Instale com: winget install JRSoftware.InnoSetup' }
& $iscc "/DAppVersion=$Version" "/DPublishDir=$publish" "/DOutputDir=$output" (Join-Path $PSScriptRoot 'installer\vibeVocal-Studio.iss')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
