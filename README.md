# vibeVocal Studio

Painel local para tratar o áudio do microfone e enviá-lo ao VB-CABLE.

## Abrir

Execute `dist\vibeVocal Studio.exe` ou `Abrir vibeVocal Studio.cmd`.

1. Em **Entrada do microfone**, selecione o seu microfone. Se nenhum estiver salvo, o aplicativo usa o microfone padrão do Windows.
2. Em **Saída para o cabo virtual**, use **CABLE Input (VB-Audio Virtual Cable)**. Evite a variante de 16 canais para uso comum.
3. Clique em **Iniciar**.
4. No Discord, jogo, OBS ou aplicativo de chamada, escolha **CABLE Output (VB-Audio Virtual Cable)** como microfone.
5. Você pode minimizar ou fechar a janela: o áudio continua funcionando na bandeja, perto do relógio.

O aplicativo não muda o dispositivo padrão do Windows. A captura começa ao clicar em Iniciar ou ao abrir o programa com **Iniciar áudio automaticamente** ativado. Não há gravação de áudio em arquivos nem envio pela rede.

## Abrir com o Windows e usar a bandeja

No painel **Inicialização e bandeja**:

- **Abrir com o Windows:** registra o aplicativo para abrir ao entrar na sua conta do Windows. Desmarcar remove esse registro.
- **Iniciar áudio automaticamente:** inicia o microfone e o VB-CABLE com os ajustes salvos ao abrir o aplicativo. Se os dispositivos ainda estiverem carregando, tenta novamente por até aproximadamente 28 segundos.
- **Abrir direto na bandeja:** mantém a janela oculta ao abrir. O ícone de microfone azul fica na área próxima ao relógio, possivelmente dentro da seta de ícones ocultos.

Minimizar ou clicar no **X** recolhe a janela e mantém o processamento. Para ajustar, dê dois cliques no ícone ou use **botão direito → Abrir vibeVocal Studio**. Abrir o atalho novamente também traz a mesma janela, sem iniciar outra captura. Para encerrar o programa e parar o áudio, use **botão direito → Sair**. O desligamento normal do Windows também encerra o aplicativo.

O registro de inicialização fica apenas na conta atual, em `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, valor `vibeVocal Studio`. Mantenha o aplicativo na mesma pasta. Se movê-lo, desmarque e marque novamente **Abrir com o Windows** na nova localização. O Windows pode administrar ou desativar a entrada em seus próprios aplicativos de inicialização.

Se a captura falhar em segundo plano, o ícone mostra o estado e o aplicativo solicita que você confira os dispositivos. A escuta nos fones continua iniciando desligada; iniciar automaticamente ativa o envio ao VB-CABLE.

## Ouvir o próprio microfone

No painel Controle de voz, escolha seu fone no seletor ao lado de **Ouvir meu microfone**, marque a opção e clique em **Iniciar**. Se o áudio já estiver ativo, basta marcar a opção. Você ouve a voz após os ganhos, equalização e demais efeitos, enquanto o envio ao VB-CABLE continua normalmente. O mudo também silencia o retorno.

Desmarque a opção para desligar o retorno ou trocar o dispositivo. O fone escolhido é salvo; a escuta sempre começa desligada ao abrir o aplicativo. Use fones para evitar eco e microfonia. Pode haver atraso devido aos buffers de áudio e ao dispositivo de saída.

## Controles

- **Ganho de entrada:** de −24 a +24 dB, aplicado digitalmente depois da captura. Não altera o botão físico do microfone nem corrige áudio já distorcido na entrada.
- **Ganho de saída:** de −24 a +12 dB, aplicado depois do tratamento.
- **Equalização:** graves em 160 Hz, presença em 3 kHz e agudos em 8 kHz. Cada controle varia entre −12 e +12 dB.
- **Cortar graves abaixo de 80 Hz:** reduz vibrações e ruídos de baixa frequência.
- **Suprimir ruído durante a fala:** reduz ventilador, chiado e outros ruídos usando RNNoise, localmente, antes dos ganhos e demais efeitos. Começa ativada e a escolha é salva. Funciona tanto no VB-CABLE quanto no retorno para os fones.
- **Gate:** silencia ruídos nas pausas. Não faz remoção de ruído durante a fala. Se cortar sílabas, diminua o limiar ou desligue o gate.
- **Compressor:** reduz a diferença entre partes baixas e altas da voz. Limiar e proporção são ajustáveis.
- **Limitador:** proteção com teto de −1 dBFS, sempre ativo.
- **Silenciar:** zera a saída do aplicativo, sem mudar o mudo físico do microfone.
- **Comparar original:** ignora supressão de ruído, ganhos, equalização, gate e compressor. O limitador e o mudo continuam ativos.
- **Buffer:** 20, 40 ou 80 ms. Pare o processamento para mudar. Comece em 40 ms; aumente se houver estalos. Esse número não representa a latência total entre falar e ouvir.

Os ajustes ficam em `%LOCALAPPDATA%\VibeVocalStudio\settings.json`, uma pasta fixa da sua conta do Windows, compartilhada entre o executável de desenvolvimento e o de distribuição. Ao abrir a versão atual pela primeira vez, ela importa o arquivo válido mais recente das pastas antigas `Data` do aplicativo, de `dist` ou de `bin` deste projeto, preservando os originais. Depois da importação, os arquivos antigos não substituem os novos ajustes. Mudo e comparação com o original não são restaurados ao abrir. **Iniciar áudio automaticamente** e **Abrir direto na bandeja** são salvos nesse arquivo; o registro de abertura com o Windows é controlado separadamente. As cópias atualizadas compartilham também a mesma instância do aplicativo.

## Salvar e reaplicar um perfil de voz

No painel **Perfil de voz**, clique em **Salvar perfil** para guardar seus ganhos, equalização, corte de graves, supressão de ruído, gate e compressor. **Reaplicar perfil** recupera esses ajustes sem trocar dispositivos, buffer, preferências de inicialização, mudo ou comparação com o original.

O perfil fica em `%LOCALAPPDATA%\VibeVocalStudio\voice-profile.json`. Existe um perfil salvo; clicar em **Salvar perfil** novamente substitui o anterior. O salvamento automático continua guardando seus últimos ajustes separadamente, inclusive ao encerrar antes do prazo de 500 ms. Mudar os controles ou restaurar padrões não apaga o perfil. Ao reabrir, o aplicativo usa os últimos ajustes automáticos; o perfil pode ser reaplicado pelo botão quando você quiser.

## Reduzir ruído enquanto fala

No painel **Redução de ruído**, mantenha **Suprimir ruído durante a fala** marcada e clique em **Iniciar**. Para comparar, ative **Ouvir meu microfone** com fones e ligue/desligue somente a opção de supressão enquanto fala. Deixe **Comparar original** desmarcada para ouvir o tratamento.

O gate continua independente e atua nas pausas. Se estiver cortando o começo ou fim das palavras, diminua o limiar do gate ou desligue **Ativar gate**. A supressão continua funcionando com o gate desligado.

A supressão trabalha em mono a 48 kHz e acrescenta cerca de 30 ms ao caminho de áudio, além dos buffers e dispositivos. Pode alterar um pouco o timbre; não garante eliminar qualquer ruído, outras vozes ou distorção já capturada. Os ganhos e demais ajustes existentes são preservados ao atualizar.

## Se não sair som

- Confirme que a entrada é o seu microfone e que a saída é CABLE Input.
- No aplicativo de destino, escolha CABLE Output, e não o microfone físico.
- Confira o mudo físico, o ganho físico do microfone e a permissão de microfone para aplicativos da área de trabalho no Windows.
- Se a saída estiver ocupada, escolha CABLE Input padrão e feche o programa que estiver usando o cabo em modo exclusivo.
- Depois de desconectar ou reconectar o dispositivo, pare e clique em Atualizar dispositivos.

## Desenvolvimento

Requer .NET 10 SDK para compilar. O executável distribuído para Windows x64 usa o .NET 10 Desktop Runtime já presente neste PC. NAudio.Wasapi 2.4.0 e NAudio.Core 2.4.0 são dependências MIT. O RNNoise é compilado do código oficial e incluído em `Native/rnnoise.dll`; origem, revisão e instruções em `Native/README.md`. Mantenha a DLL junto do executável ao distribuir o aplicativo.

```powershell
./Build.ps1
dotnet run --project tests/VibeVocalStudio.Tests.csproj -c Release
dotnet run --project tests/VibeVocalStudio.Tests.csproj -c Release -- --profiles
./tests/New-NoiseTestSpeech.ps1
dotnet run --project tests/VibeVocalStudio.Tests.csproj -c Release -- --noise
dotnet run --project tests/VibeVocalStudio.Tests.csproj -c Release -- --hardware
```

O último comando abre o microfone temporariamente e verifica a recepção pelo VB-CABLE. Salva apenas métricas em `artifacts/hardware-validation.json`, sem armazenar áudio. O teste `--profiles` usa arquivos isolados para verificar migração entre cópias, salvamento automático, botões de perfil, fechamento e reabertura completos da interface; não abre captura nem altera ajustes reais.

O teste `--noise` usa voz sintética local do Windows e ruído gerado, sem gravar o microfone. O relatório registra atenuação, diferenças contra referências limpas, preservação da voz, alternância ao vivo, persistência e tempo de processamento em `artifacts/noise-validation.json`. A avaliação perceptiva no ambiente real continua sendo necessária.

O teste `dotnet run --project tests/VibeVocalStudio.Tests.csproj -c Release -- --startup` verifica iniciar com janela oculta, recolher/restaurar, instância única, encerrar e persistir preferências. Usa captura silenciada e simula o registro de inicialização, sem mudar a configuração do Windows.

Referências: [RNNoise](https://github.com/xiph/rnnoise), [NAudio](https://github.com/naudio/NAudio), [VB-CABLE](https://vb-audio.com/Cable/), [WASAPI](https://learn.microsoft.com/en-us/windows/win32/coreaudio/wasapi).
