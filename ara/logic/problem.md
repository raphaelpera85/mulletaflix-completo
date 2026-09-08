# Definição do Problema: Instabilidade do Disco N e Dependência no MulletaFlix

## 1. Descrição do Problema
O MulletaFlix depende do subsistema Nebula para enviar mídias ao Telegram e recuperá-las sob demanda. No entanto:
1. As bibliotecas do Jellyfin estão cadastradas apontando para `N:\Filmes`, `N:\Series`.
2. Se a unidade `N:` não estiver montada, nenhum filme reproduz e o Jellyfin acusa arquivo não encontrado.
3. O disco `N:` é montado através do rclone + WinFsp consumindo um servidor FTP local C# (`NebulaFileSystem`). O disco sofre quedas frequentes, desmonta durante o uso e gera erros de E/S (`ERROR_IO_DEVICE`).

## 2. Lacunas e Causas Identificadas
- O rclone roda com `--dir-cache-time 10s`, gerando tempestades de chamadas `LIST` via FTP.
- O WinFsp monta o disco sem `--network-mode`, fazendo o Windows Search e o Windows Defender tentarem ler arquivos gigabytes de vídeo do Telegram, causando timeouts.
- O MulletaFlix não gera arquivos `.strm` locais automaticamente no término dos uploads, forçando a dependência de um disco emulatório no kernel.
