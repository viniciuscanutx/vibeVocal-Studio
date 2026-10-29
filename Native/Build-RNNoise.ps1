$ErrorActionPreference = 'Stop'
$revision = '70f1d256acd4b34a572f999a05c87bf00b67730d'
$modelHash = '0a8755f8e2d834eff6a54714ecc7d75f9932e845df35f8b59bc52a7cfe6e8b37'
$workspace = Split-Path $PSScriptRoot -Parent
$buildRoot = Join-Path $workspace 'artifacts\rnnoise-native-build'
$archivePath = Join-Path $buildRoot 'source.zip'
$modelPath = Join-Path $buildRoot 'model.tar.gz'
New-Item -ItemType Directory -Path $buildRoot -Force | Out-Null
Invoke-WebRequest -Uri ('https://api.github.com/repos/xiph/rnnoise/zipball/' + $revision) -OutFile $archivePath
Expand-Archive -LiteralPath $archivePath -DestinationPath (Join-Path $buildRoot 'source') -Force
$sourceDirectory = (Get-ChildItem (Join-Path $buildRoot 'source') -Directory -Filter 'xiph-rnnoise-*' | Select-Object -First 1).FullName
if ((Get-Content (Join-Path $sourceDirectory 'model_version') -Raw).Trim() -ne $modelHash) {
    throw 'A revisão de código não corresponde ao modelo esperado.'
}
Invoke-WebRequest -Uri ('https://media.xiph.org/rnnoise/models/rnnoise_data-' + $modelHash + '.tar.gz') -OutFile $modelPath
if ((Get-FileHash -LiteralPath $modelPath -Algorithm SHA256).Hash -ne $modelHash) {
    throw 'Hash SHA-256 do modelo diferente do esperado.'
}
tar -xzf $modelPath -C $sourceDirectory
if ($LASTEXITCODE -ne 0) { throw 'Não foi possível extrair o modelo.' }
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$visualStudioPath = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (!$visualStudioPath) { throw 'Instale as ferramentas C++ x64 do Visual Studio para recompilar a biblioteca.' }
Import-Module (Join-Path $visualStudioPath 'Common7\Tools\Microsoft.VisualStudio.DevShell.dll')
Enter-VsDevShell -VsInstallPath $visualStudioPath -SkipAutomaticLocation -DevCmdArguments '-arch=x64 -host_arch=x64' | Out-Null
$sourceNames = @('denoise.c', 'rnn.c', 'pitch.c', 'kiss_fft.c', 'celt_lpc.c', 'nnet.c', 'nnet_default.c', 'parse_lpcnet_weights.c', 'rnnoise_data.c', 'rnnoise_tables.c')
$sources = $sourceNames | ForEach-Object { Join-Path $sourceDirectory ('src\' + $_) }
Push-Location $buildRoot
try {
    & cl.exe /nologo /O2 /MT /LD /std:c11 /DWIN32 /DRNNOISE_BUILD /DDLL_EXPORT /D_CRT_SECURE_NO_WARNINGS ('/I' + (Join-Path $sourceDirectory 'include')) ('/I' + (Join-Path $sourceDirectory 'src')) $sources /Fernnoise.dll
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar RNNoise.' }
}
finally {
    Pop-Location
}
Copy-Item -LiteralPath (Join-Path $buildRoot 'rnnoise.dll') -Destination (Join-Path $PSScriptRoot 'rnnoise.dll') -Force
Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'rnnoise.dll') -Algorithm SHA256 | Format-List
