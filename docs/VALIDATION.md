# Validação — 16/10/2026

- Compilação Release: concluída sem erros nem avisos.
- 18 verificações de DSP, conversão de canais, reamostragem, persistência e desempenho: aprovadas.
- 7 verificações da interface WPF: aprovadas. Sliders atualizam o processamento; iniciar, parar, mudo e restauração funcionaram. Nenhum erro de binding observado.
- Microfone USB real em 48 kHz para CABLE Input em 48 kHz: captação e recepção pelo CABLE Output confirmadas em testes de aproximadamente 3 segundos por buffer, com 20, 40 e 80 ms.
- Mudo: saída do processador zerada nos três buffers.
- Descartes de fila: zero nos testes curtos. Fila observada de até 10 ms. Essas medições não representam latência total nem estabilidade de longa duração.
- O teste usou apenas métricas; não salvou gravações do microfone.
- Inspeção visual: painel renderizado e conferido; janela pequena possui rolagem.
- Executável publicado em `dist` e inicialização validada pelo modo de renderização.

## Limites da validação

Não houve avaliação auditiva da voz, chamada real em Discord/jogo, desconexão física durante uso ou teste prolongado de estabilidade. Os ajustes finais de ganho, gate e equalização dependem da voz e do ambiente do usuário.

Relatórios detalhados: `artifacts/hardware-validation.json` e `artifacts/ui-validation.json`.

## Retorno para ouvir o microfone

- Adicionada escuta do sinal após os efeitos, em fone ou alto-falante selecionado, com fila independente da saída VB-CABLE.
- 21 verificações de sinal, persistência e desempenho aprovadas, incluindo a identidade entre o sinal encaminhado ao retorno e ao cabo e a aplicação do mudo aos dois caminhos.
- 13 verificações de retorno aprovadas no dispositivo de reprodução instalado: iniciar/desligar repetidamente, receber amostras mantendo captura principal, rejeitar VB-CABLE como retorno e encerrar a escuta ao parar o áudio.
- 10 verificações de interface aprovadas, incluindo o novo controle de escuta. Nenhum erro de binding observado.
- O teste no dispositivo físico foi feito com sinal silenciado. A escuta subjetiva e o atraso percebido ficam para validação do usuário.
- O dispositivo escolhido é persistido; a opção de escuta inicia desligada.

Relatório adicional: `artifacts/monitor-validation.json`.

## Supressão de ruído durante a fala

- RNNoise oficial, revisão `70f1d256acd4b34a572f999a05c87bf00b67730d`, compilado para Windows x64 com MSVC. Modelo conferido por SHA-256. DLL publicada idêntica à DLL validada.
- 21 verificações anteriores de DSP aprovadas; 19 verificações específicas de supressão aprovadas.
- Chiado branco sintético, com gate desligado: atenuação de aproximadamente 33,2 dB após o primeiro segundo. Mistura de chiado e tons de 120/240 Hz: aproximadamente 71,3 dB. São resultados desses sinais de teste, não garantias para qualquer ambiente.
- Fala sintética local em português com ruído adicionado: diferença residual contra a mesma fala limpa também processada correspondeu a 21,6 dB, frente a 14,2 dB da mistura inicial. A referência igualmente processada controla alterações de timbre e fase da biblioteca; essa métrica não equivale a uma avaliação perceptiva. A comparação direta contra a referência sem processamento foi 11,4 dB e também consta no relatório.
- Na fala ativa, correlação de 0,963 com a referência original alinhada e nível RMS relativo de 0,954. Alinhamento de 1.440 amostras confirma 30 ms adicionais entre entrada e saída da etapa de supressão.
- Blocos com tamanhos irregulares geraram exatamente o mesmo áudio que um bloco único. Desligar e religar descarta o áudio pendente; comparação com original e mudo funcionaram. Entrada a 44,1 kHz estéreo é convertida para 48 kHz mono antes do processamento.
- Tempo de processamento com sinal contínuo e todos os efeitos: média de 1,035 ms e percentil 99 de 1,761 ms por bloco de 20 ms neste PC. Teste sintético curto, sem garantia de estabilidade prolongada.
- 6 verificações de captura/recepção e mudo com microfone USB e VB-CABLE, em buffers de 20/40/80 ms: aprovadas, sem descartes de fila. Nesta rodada, a entrada física permaneceu zerada; o teste confirma transporte e inicialização, não qualidade de supressão na voz real.
- 13 verificações de retorno e 13 de interface aprovadas. Supressão foi alternada durante a captura sem interromper a sessão. Fones testados com mudo ativo. Nenhum erro de binding WPF.
- Janela publicada renderizada e conferida. Configurações anteriores comparadas campo a campo e preservadas. Adicionado apenas `NoiseSuppressionEnabled`, ativado por padrão e persistente.
- Compilação e publicação concluídas sem erros ou avisos. A supressão funciona sem rede; avaliação auditiva com a voz e o ambiente do usuário continua pendente.

Relatório adicional: `artifacts/noise-validation.json`. A amostra `artifacts/noise-test-speech.wav` é voz sintetizada pelo Windows, não uma gravação do microfone.

## Inicialização automática e bandeja

- 23 verificações específicas aprovadas: iniciar áudio com janela oculta, preservar ajustes, manter captura ao minimizar/fechar, restaurar janela, encerrar, permitir fechamento na saída da sessão, persistir preferências, cancelar inicialização e tratar falhas no registro.
- Os testes de preferências usam um registro simulado. O registro real foi ativado somente na publicação, para a conta atual, apontando para `<pasta do app>\dist\vibeVocal Studio.exe --startup`, com o caminho do executável entre aspas.
- Instância única validada em testes e na versão publicada: uma segunda abertura encerrou a segunda instância e restaurou a primeira janela. Uma abertura repetida com `--startup` permanece silenciosa.
- Publicação inicializada com a janela oculta; a sessão de reprodução no CABLE Input foi confirmada ativa para o processo publicado e a biblioteca RNNoise estava carregada. Fechar pelo X recolheu a janela e a sessão de áudio continuou ativa.
- 13 verificações de interface e 21 verificações anteriores de DSP também passaram. Compilação/publicação final sem avisos ou erros.
- Ganhos, filtros, supressão, gate, compressor, buffer e dispositivos comparados e preservados. `AutoStartAudio` e `StartInTray` ativados. O retorno nos fones continua começando desligado.
- O aplicativo publicado foi deixado funcionando na bandeja. O próximo login real e o desligamento do Windows não foram executados durante a validação; a entrada de inicialização, o comando equivalente e a permissão de fechamento foram verificados sem reiniciar o PC.

Relatórios: `artifacts/startup-validation.json` e `artifacts/running-app-validation.json`. Configuração anterior preservada em `artifacts/settings-before-startup-update.json`.
