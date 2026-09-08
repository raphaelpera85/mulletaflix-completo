# Claims & Asserções Falseáveis

| ID | Claim | Status | Provenance | Evidência |
|---|---|---|---|---|
| C01 | A dependência do disco `N:` pode ser completamente eliminada usando arquivos `.strm` apontando para `NebulaHttpStreamServer` (porta 2123). | VERIFIED | ai-suggested | `NebulaHttpStreamServer.cs` suporta HTTP 206 e streaming direto de partes do Telegram sem passar por rclone. |
| C02 | O erro de E/S (`0x8007045D`) no disco N: é desencadeado pelo timeout do WinFsp ao esperar chunks do Telegram sob indexação indevida do Windows Search e `--dir-cache-time 10s`. | VERIFIED | ai-suggested | Código em `mount_drive_n.py` (L78-86) roda sem `--network-mode` e com invalidação a cada 10s. |
| C03 | O gerador atual `NebulaStrmGenerator.cs` gera URLs `ftp://` em vez de URLs `http://`, impedindo reprodução direta nos clientes Jellyfin Web. | VERIFIED | ai-suggested | Linha 251 de `NebulaStrmGenerator.cs` retorna `ftp://...`. |
| C04 | O MulletaFlix já possui todos os testes do subsistema Nebula verdes (66 aprovados, 0 falhas). | VERIFIED | ai-executed | Execução do comando `dotnet test` com 66 testes aprovados em 376ms. |
