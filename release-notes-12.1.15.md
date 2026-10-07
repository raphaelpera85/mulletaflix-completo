# MulletaFlix Server v12.1.15

Esta versão traz a migração completa do ecossistema de rede e dados do Nebula para Python com supervisão de processos no MulletaFlix Server C#:

### 🚀 Principais Melhorias e Novidades

- **Migração do Nebula para Python supervisionado:**
  - **FTP nativo em Python (`aioftp`):** Operação assíncrona na porta 2121 com autenticação MongoDB.
  - **Servidor de Streaming HTTP (`aiohttp`):** Servindo reprodução direta e HLS na porta 2123.
  - **Motor de Upload Multi-Bot:** Integração Pyrogram com pool de bots assíncrono e balanceamento de carga.
  - **STRM Downloader Multipart (`strm_downloader.py`):** Download acelerado de mídias com até 32 conexões paralelas e controle de backpressure.
- **Cache Local de Reprodução (`NEBULA_PLAYBACK_CACHE_ROOT`):**
  - Integração entre `ftp/pathio.py` e `NebulaPlaybackCache` via leases IPC locais, garantindo reprodução instantânea a partir de chunks em disco para seeks rápidos.
- **Correção no Supabase Sync:**
  - Redução da paginação de restauração de 500 para 100 itens por lote, eliminando timeouts de query (`HTTP 500 - code 57014 statement timeout`).
- **Web UI & Telemetria:**
  - Captura e streaming em tempo real do progresso, velocidade de download e status do motor Python para a interface web.

### 📦 Artefatos
- `mulletaflix-update-win-x64.zip`: Pacote de atualização in-place.
- `mulletaflix_12.1.15_windows-x64.exe`: Instalador executável completo para Windows x64.
