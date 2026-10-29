$ErrorActionPreference = 'Stop'
$outputDirectory = Join-Path $PSScriptRoot '..\artifacts'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$voice = New-Object -ComObject SAPI.SpVoice
$stream = New-Object -ComObject SAPI.SpFileStream
$format = New-Object -ComObject SAPI.SpAudioFormat
try {
    $format.Type = 38
    $stream.Format = $format
    $stream.Open((Join-Path $outputDirectory 'noise-test-speech.wav'), 3, $false)
    $voice.AudioOutputStream = $stream
    $voice.Rate = 0
    $voice.Speak('Este é um teste do microfone. O ruído do ventilador deve diminuir enquanto a minha voz continua clara. Um, dois, três. Estou falando em volume normal e verificando o som de cada palavra.') | Out-Null
}
finally {
    $stream.Close()
    [Runtime.InteropServices.Marshal]::FinalReleaseComObject($stream) | Out-Null
    [Runtime.InteropServices.Marshal]::FinalReleaseComObject($voice) | Out-Null
    [Runtime.InteropServices.Marshal]::FinalReleaseComObject($format) | Out-Null
}
