# Contrato da API Nebula

Este documento descreve a API administrativa atualmente exposta pelo servidor. A superfície existente usa o prefixo `/NebulaFtp` para manter compatibilidade com o frontend MulletaFlix.

## Regras gerais

- Todos os endpoints exigem autenticação e a policy `RequiresElevation`.
- O cliente deve enviar `X-Correlation-ID` opcionalmente. O servidor valida o valor e devolve um ID normalizado na resposta.
- Segredos nunca são devolvidos por `GET /NebulaFtp/Config`: `MongoDbConnectionString`, `ApiHash`, `BotTokens`, `Password`, `HttpStreamToken` e `SupabaseKey` são sempre vazios.
- Ao salvar configuração, um segredo vazio preserva o valor já armazenado. Para alterar um segredo, envie o novo valor explicitamente.
- Cancelamento de requisição é propagado para as operações assíncronas; o cliente deve tratar a operação como inconclusiva até consultar `Status` ou `Supabase/Status`.
- Operações de manutenção (`GenerateStrm`, `PruneCompleted`, `Supabase/Backup` e `Supabase/Restore`) são serializadas no servidor.
- Essas quatro operações aceitam o header opcional `X-Idempotency-Key` (1–128 caracteres `A-Z`, `a-z`, `0-9`, `.`, `_`, `:`, `-`). A mesma chave e operação devolvem o resultado já concluído por até 15 minutos, inclusive após reinicialização, usando a coleção Mongo `operation_replays` com índice único e TTL; o cache em memória permanece como fallback quando o Mongo está indisponível. Segredos e payloads de configuração não são persistidos; sem header, o comportamento é o tradicional.
- `NebulaStatusDto.MaintenanceOperation` informa a última operação de manutenção, seu estado, timestamps, duração, erro resumido e, quando disponível, `ProgressPercent`/`ProgressText` para backup e restore.

## Endpoints

| Método | Rota | Resposta | Efeito |
|---|---|---|---|
| `GET` | `/NebulaFtp/Status` | `200 NebulaStatusDto` | Estado do Envio, Downloader, modo streaming e última operação de manutenção. |
| `GET` | `/NebulaFtp/Health` | `200 NebulaComponentHealthDto` | Checks individuais de MongoDB, Telegram, listener FTP e listener HTTP; não devolve segredos. |
| `GET` | `/NebulaFtp/Logs?serverOffset=0&downloaderOffset=0` | `200 NebulaLogsDto` | Logs paginados por offset. |
| `GET` | `/NebulaFtp/Config` | `200 NebulaFtpConfiguration` seguro | Configuração sem segredos. |
| `POST` | `/NebulaFtp/Config` | `204` | Atualiza configuração preservando segredos omitidos. |
| `POST` | `/NebulaFtp/Config/Secrets` | `204` | Rotaciona apenas os segredos enviados; nunca devolve valores secretos. |
| `POST` | `/NebulaFtp/Actions/StartEnvio` | `200 boolean` | Inicia FTP/HTTP; body opcional `{ "streamOnly": boolean }`. |
| `POST` | `/NebulaFtp/Actions/StopEnvio` | `200 boolean` | Para FTP/HTTP e libera recursos quando possível. |
| `POST` | `/NebulaFtp/Actions/StartDownloader` | `200 boolean` | Inicia o Downloader STRM. |
| `POST` | `/NebulaFtp/Actions/StopDownloader` | `200 boolean` | Para o Downloader STRM. |
| `POST` | `/NebulaFtp/Actions/MountDriveN` | `200 boolean` | Monta a unidade configurada. |
| `POST` | `/NebulaFtp/Actions/UnmountDriveN` | `200 boolean` | Desmonta a unidade quando não há consumidor ativo. |
| `POST` | `/NebulaFtp/Actions/GenerateStrm` | `200 boolean` | Gera a biblioteca STRM. |
| `POST` | `/NebulaFtp/Actions/PruneCompleted` | `200 boolean` | Corrige registros concluídos/inconsistentes. |
| `GET` | `/NebulaFtp/Bots` | `200 NebulaBotDto[]` | Lista bots sem expor tokens. |
| `POST` | `/NebulaFtp/Bots` | `200 NebulaBotDto[]` | Cria ou atualiza bot. |
| `DELETE` | `/NebulaFtp/Bots/{index}` | `200 NebulaBotDto[]` | Remove bot pelo índice. |
| `POST` | `/NebulaFtp/Bots/Sync` | `200 NebulaBotDto[]` | Sincroniza bots das variáveis de ambiente. |
| `GET` | `/NebulaFtp/Supabase/Status` | `200 NebulaSupabaseStatusDto` | Estado da integração sem devolver a chave. |
| `POST` | `/NebulaFtp/Supabase/Test` | `200 NebulaSupabaseTestResponseDto` | Testa a integração configurada. |
| `POST` | `/NebulaFtp/Supabase/Backup` | `200 NebulaSupabaseBackupResultDto` | Sincroniza MongoDB para Supabase. |
| `POST` | `/NebulaFtp/Supabase/Restore` | `200 NebulaSupabaseRestoreResultDto` | Restaura Supabase para MongoDB. |
| `GET` | `/NebulaFtp/Supabase/SqlScript` | `200 text/plain` | Retorna script de preparação das tabelas. |

## Estados HTTP

- `200`: operação processada; o corpo contém o DTO ou booleano da operação.
- `204`: configuração salva com sucesso.
- `400`: body ausente ou inválido em endpoints que exigem payload.
- `401`: sessão ausente ou inválida.
- `403`: usuário autenticado sem elevação administrativa.
- `404`: rota não encontrada ou recurso inexistente.
- `499`/cancelamento equivalente do host: a requisição foi cancelada pelo cliente; consulte o status antes de repetir operações não idempotentes.
- `5xx`: falha inesperada do servidor ou dependência; repetir somente após consultar status e logs.

## Idempotência e repetição

`StartEnvio`, `StopEnvio`, `StartDownloader` e `StopDownloader` são seguros para repetição: o manager detecta o estado atual e evita duplicar recursos. Backups e restaurações concorrentes são serializados; uma nova chamada deve aguardar a anterior terminar ou ser cancelada pelo cliente. O cliente deve usar `X-Correlation-ID` para correlacionar tentativas e consultar os logs.

## Versionamento

`/NebulaFtp` é a versão compatível atual e não deve receber mudanças incompatíveis. Novos campos devem ser opcionais e novos endpoints devem ser aditivos. Uma alteração incompatível deve ser publicada sob `/NebulaFtp/v2`, mantendo a versão atual durante o período de migração do frontend e dos clientes externos.
