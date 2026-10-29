# RNNoise para Windows x64

Biblioteca compilada do repositório oficial, com o modelo padrão incorporado. Não depende de GPU, conexão de rede ou download durante o uso.

- Código: https://github.com/xiph/rnnoise/tree/70f1d256acd4b34a572f999a05c87bf00b67730d
- Revisão: `70f1d256acd4b34a572f999a05c87bf00b67730d`.
- Modelo: https://media.xiph.org/rnnoise/models/rnnoise_data-0a8755f8e2d834eff6a54714ecc7d75f9932e845df35f8b59bc52a7cfe6e8b37.tar.gz
- SHA-256 do modelo: `0a8755f8e2d834eff6a54714ecc7d75f9932e845df35f8b59bc52a7cfe6e8b37`.
- SHA-256 da DLL distribuída nesta compilação: `bbb5dc1371e07193c5fb178f164aa6a672728b74b696e71038e71209d8c3f74b`.
- Compilador: Visual Studio 2022 / MSVC 14.44, Windows x64, `/O2 /MT /std:c11`.
- A compilação usa o caminho C portátil, sem exigir AVX/AVX2 e com runtime C incorporado.
- Licenças e avisos de terceiros: `RNNoise-NOTICES.txt`, copiado junto do executável.

Para reconstruir com as ferramentas C++ x64 do Visual Studio instaladas, execute `./Native/Build-RNNoise.ps1` a partir da raiz do projeto. O script baixa a revisão fixada, verifica o SHA-256 do modelo e recompila `Native/rnnoise.dll`. O hash da DLL pode variar com compilador e metadados de compilação. A compilação normal do aplicativo apenas copia a DLL pronta.

O processamento recebe quadros de 480 amostras mono a 48 kHz, com amplitude equivalente a PCM de 16 bits. A camada C# mantém um quadro de saída para aceitar leituras de qualquer tamanho. Somado ao atraso de dois quadros da versão utilizada, isso corresponde a 30 ms adicionais quando a supressão está ativa. Desativar a supressão ou comparar o original descarta os quadros pendentes; reativar cria um estado novo.
