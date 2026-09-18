# Diretrizes de Streaming e Montagem do Nebula (MulletaFlix)

## 1. Prioridade de Acesso e Indexação de Mídias
- **Sempre priorizar arquivos `.strm` locais**:
  - As bibliotecas do Jellyfin devem ser configuradas apontando para diretórios físicos locais onde ficam os arquivos `.strm` (ex: `D:\MulletaFlixMedia\Filmes`).
  - Os arquivos `.strm` devem apontar para o servidor de stream HTTP nativo: `http://127.0.0.1:2123/stream?id={encodedPath}`.
  - **Evitar** apontar bibliotecas ativas do Jellyfin diretamente para `N:\` para garantir que o catálogo e a reprodução nunca dependam da montagem de drivers virtuais no kernel do Windows.

## 2. Flags Mandatórias para Montagem Rclone/WinFsp (Disco N:)
- Caso o disco `N:` seja montado para exploração manual no Windows Explorer, o comando rclone DEVE conter obrigatoriamente:
  - `--network-mode`: obriga o Windows a tratar o volume como compartilhamento de rede, desativando indexação automática pelo `SearchIndexer.exe` e Defender.
  - `--dir-cache-time 24h` (ou superior): nunca usar tempos curtos (ex: 10s) que sobrecarregam as conexões FTP.
  - `--vfs-cache-mode full`: **NUNCA** utilizar `writes` ou `off`. Leitores de mídia e o Windows Explorer realizam leituras esparsas de cabeçalhos (ex: Matroska/MP4); com `writes`, o socket FTP é fechado prematuramente, disparando erro 501 no FubarDev FTP Server, derrubando o socket de controle e congelando todo o driver WinFsp com erro de dispositivo de E/S.
  - `--vfs-read-chunk-size 16M` e `--vfs-read-chunk-size-limit 512M`: alinhamento com os chunks de 16MB do Telegram MTProto.
  - `--buffer-size 32M`, `--timeout 60s` e `--low-level-retries 10`: tolerância a variações de latência de rede do Telegram.

## 3. Ciclo de Upload e Geração de STRM
- Sempre que um upload para o Telegram for finalizado com sucesso no `NebulaFtpManager`, o sistema deve invocar a geração do arquivo `.strm` correspondente na pasta local de mídias para que o vídeo fique disponível para streaming imediatamente.
