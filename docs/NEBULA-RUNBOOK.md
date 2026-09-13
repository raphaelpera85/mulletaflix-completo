# Runbook operacional do Nebula

## Escopo

Use este procedimento para diagnosticar o NebulaFTP, o streaming HTTP e as operações de backup/restore. Não copie credenciais para tickets, logs ou URLs.

## Verificação rápida

1. Verifique liveness/readiness:

   ```text
   GET /health
   GET /ready
   ```

   `/health` inclui o banco da aplicação e o check do Nebula. `/ready` executa somente checks marcados como readiness.

2. Verifique o estado funcional autenticado como administrador:

   ```text
   GET /NebulaFtp/Status
   GET /NebulaFtp/Health
   GET /NebulaFtp/Logs?serverOffset=0&downloaderOffset=0
   ```

   Use `Health` para separar falha de dependência: `MongoConnected` é baseado em ping real quando o contexto está ativo; `TelegramReady` depende de sessão inicializada com bots disponíveis; os listeners indicam o estado efetivo das escutas FTP/HTTP. Em `Status`, use `MaintenanceOperation` para acompanhar execução, duração e erro das operações longas. Clientes que possam repetir POSTs devem enviar uma `X-Idempotency-Key` estável por operação.

   Para rotacionar credenciais sem reenviar a configuração inteira, use `POST /NebulaFtp/Config/Secrets` com apenas os campos que devem mudar. O endpoint nunca retorna os valores e os campos não enviados permanecem inalterados.

3. Consulte as métricas Prometheus e filtre:

   ```text
   nebula_http_requests_total
   nebula_http_request_duration_seconds
   nebula_http_saturated_requests_total
   ```

   Saturação recorrente indica que `MaxActiveConnections` está baixo, que o pool Telegram está lento ou que há clientes abusando do endpoint.

## Falha de streaming

- Confirme que o token foi enviado preferencialmente como `Authorization: Bearer`.
- Verifique se a resposta é `401`, `404`, `416` ou `503`; não tente corrigir esses estados alterando o código do cliente sem registrar a rota e o `X-Correlation-ID`.
- `503` com `Retry-After: 5` significa limite de concorrência atingido; aguarde e investigue as métricas.
- `416` normalmente indica um intervalo inválido. Valide `Range` e o tamanho do arquivo.
- Para acesso remoto, confirme `AllowInsecureRemoteFtp=true` somente se a rede for confiável. O servidor nativo não oferece FTPS; prefira bind local ou um proxy TLS.

## Falha de autenticação FTP

- Cinco falhas para a mesma conta em uma janela de 15 minutos provocam bloqueio temporário.
- Um login válido limpa o contador dessa conta.
- Não desative o rate limit para contornar o bloqueio; corrija a credencial ou aguarde a janela expirar.
- Se credenciais aparecem em `.strm`, desative `EmbedFtpCredentialsInStrmUrls` e regenere os arquivos. O padrão seguro não grava segredos em URLs.

## Reinício controlado

1. Pare o Envio/Downloader pela interface Nebula ou pelos endpoints `NebulaFtp/Actions/StopEnvio` e `NebulaFtp/Actions/StopDownloader`.
2. Aguarde as operações ativas terminarem; confirme em `NebulaFtp/Status`.
3. Reinicie o serviço MulletaFlix.
4. Valide `/ready`, depois `NebulaFtp/Status` e as métricas.

Não remova arquivos de sessão Telegram, banco ou stage durante um incidente sem backup e sem registrar o motivo.

## Backup e restore Supabase

1. Consulte `GET /NebulaFtp/Supabase/Status`.
2. Execute backup somente após confirmar que a URL e a chave estão configuradas:

   ```text
   POST /NebulaFtp/Supabase/Backup
   ```

3. Confirme `LastBackupStatus` e a quantidade de arquivos.
4. Para restore, interrompa operações de escrita, confirme o backup escolhido e execute:

   ```text
   POST /NebulaFtp/Supabase/Restore
   ```

5. Após o restore, valide `/ready`, o status do Nebula e uma leitura de arquivo antes de iniciar novos uploads.

Restore é uma operação destrutiva para o estado atual. Preserve o estado anterior e registre quem autorizou a operação.

## Stage/E2E

O runner Playwright deve iniciar um stage descartável com `MFLX_DISABLE_EXTERNAL_BOOTSTRAP=true`, `MFLX_E2E_TEST_MODE=true` e banco `mulletaflix_e2e`. Nunca aponte `PW_BASE_URL` para uma instância compartilhada: o runner recusa stage já ativo quando essa variável é informada.

## Escalonamento

Ao abrir incidente, inclua:

- horário UTC e versão do servidor;
- rota, status HTTP e `X-Correlation-ID`;
- saída sanitizada de `NebulaFtp/Status` e `NebulaFtp/Logs`;
- valores das métricas RED no intervalo;
- se o problema ocorreu em localhost, LAN ou proxy TLS.

Remova tokens, senhas, URIs MongoDB/Supabase e URLs STRM autenticadas antes de compartilhar qualquer evidência.
