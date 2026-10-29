# vibeVocal Studio

Aplicativo local para Windows, C# / .NET 10 / WPF. Captura pelo WASAPI compartilhado e reprodução somente nas saídas do VB-CABLE. NAudio.Wasapi 2.4 fornece a integração com o áudio do Windows. Não muda o dispositivo padrão nem instala drivers.

## Escopo

- Selecionar microfone e saída VB-CABLE, iniciar/parar e silenciar.
- Ganho digital de entrada e saída, filtro de graves, equalização de graves/presença/agudos, gate com histerese, compressor e limitador.
- Supressão contínua RNNoise, antes dos ganhos, em mono a 48 kHz. Controle independente do gate, com comparação ao vivo e estado persistido. O mesmo sinal tratado alimenta cabo e retorno.
- Medidores de entrada/saída, indicação de saturação, tempo de processamento e quantidade de áudio na fila.
- Buffers de 20, 40 e 80 ms. A fila é limitada para evitar atraso crescente quando os clocks dos dispositivos diferem.
- Persistência local dos ajustes e opções independentes para iniciar o áudio e abrir na bandeja.
- Inicialização por conta do Windows, ícone de bandeja com abrir/sair e instância única por instalação. Fechar ou minimizar recolhe a janela; sair encerra a captura.

## Organização

Audio: enumeração de dispositivos, ciclo de captura/reprodução e processamento do sinal.
Models: ajustes imutáveis e modelos de dispositivos.
Services: persistência, bandeja, inicialização e instância única.
ViewModels: estado e comandos do painel.
Views: apresentação WPF.
tests: sinais sintéticos para validar DSP e execução opcional de testes com o hardware.

## Interface

Paleta: fundo aço #EDF1F5, superfície #FFFFFF, texto azul escuro #182D45, ação azul #2764D8, sinal verde #168778, atenção âmbar #BA5B16.
Tipografia: Segoe UI Variable / Segoe UI, números de ganho grandes com unidades menores. Texto alinhado à esquerda.
Layout: dispositivos à esquerda; controles principais e medidores à direita; processamento abaixo; estado no rodapé. O ganho é o destaque visual. Navegação por teclado e nomes acessíveis em controles.

## Validação

Build sem avisos; testes de ganho, silêncio, bypass, gate, compressão, limitador, canais e resampling; teste real de microfone para VB-CABLE com métricas sem gravar áudio em arquivo; renderização da janela para inspeção visual. Qualidade percebida de voz exige escuta pelo usuário.
