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

## Retomada de envio Telegram

- O motor C# mantém partes confirmadas em `DataPath/nebula_upload_checkpoints`. No motor Python, o journal fica em `SESSIONS_DIR/upload-checkpoints`; `NEBULA_UPLOAD_CHECKPOINT_DIR` permite configurar outro diretório. A versão 2 registra SHA-256 de cada parte nos checkpoints locais e Mongo e usa o marcador também na legenda Telegram. O fingerprint do arquivo inteiro continua sendo metadado auxiliar, não prova de identidade. Os journals não guardam caminhos da fonte nem tokens; aplique ACLs de dados do servidor.
- Antes de `sendDocument`, o Python registra a intenção com o SHA-256 dos mesmos bytes enviados. Depois da resposta ou de uma reconciliação bem-sucedida, grava a confirmação local antes do checkpoint Mongo. Na retomada, os motores recalculam, em leituras limitadas à faixa de cada parte, os hashes das partes confirmadas e intenções pendentes, comparando journal e Mongo. Não há pré-leitura integral em uploads novos.
- Checkpoints legados sem hash, incompletos ou divergentes não são combinados com a fonte atual: a geração UUID é rotacionada, o estado Mongo é limpo sob a lease vigente e uma nova tentativa recomeça sem misturar partes. Mensagens antigas no canal podem permanecer órfãs; preservam-se automaticamente as tentativas de upload.
- Em resultado ambíguo e em intenção pendente após reinício, os motores tentam buscar no histórico do canal por UUID, parte e SHA-256 via cliente MTProto. A busca diferencia `FOUND`, `NOT_FOUND` (consulta concluída) e `INCONCLUSIVE` (erro, API ausente ou consulta incompleta). Uma consulta inconclusiva bloqueia o reenvio naquela tentativa e aciona o retry automático com backoff/fila; no Python, o stage é preservado para o scanner reenfileirar. O Bot API não fornece chave de idempotência para `sendDocument`; se buscas concluídas não mostrarem uma mensagem aceita ou sua visibilidade atrasar, duplicata residual ainda é possível. Não classifique isso como exactly-once.
- Se o fingerprint da fonte mudou ou o checkpoint legado/incompleto exige nova geração, o fluxo rotaciona o UUID e não mistura partes; mensagens antigas no canal podem permanecer órfãs. Se o journal estiver estruturalmente corrompido, preserve-o para diagnóstico: o loader C# interrompe com `InvalidDataException`, a fila usa retry/backoff até reparo ou limite terminal, e nenhuma parte é enviada enquanto a leitura falhar. Não interprete corrupção como ausência de upload nem reenvie às cegas.
- O journal C# rejeita hashes pendentes nulos, duplicados, fora da faixa ou com SHA-256 inválido; também rejeita partes confirmadas e números pendentes duplicados. Essas falhas preservam o checkpoint original e interrompem de modo fail-closed.
- O instalador/pacote Windows e Linux precisa conter `upload_checkpoint.py`. A validação local não substitui teste integrado com MongoDB e Telegram isolados nem homologação do servidor instalado.

## Catálogo de solicitações STRM

- Consulte `GET /UserFeedback/MediaSuggestions/Status` para verificar `State`, `IndexedTitleCount`, `RootCount` e `FailedRootCount`.
- `ReadyWithWarnings` significa que títulos acessíveis foram indexados, mas uma ou mais raízes tiveram diretórios sem leitura. O serviço continua percorrendo as pastas acessíveis e registra a contagem de diretórios ignorados; não trate um catálogo parcial como completo.
- Corrija permissões/caminhos da biblioteca e solicite novo refresh do catálogo antes de concluir que um título não existe.

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

Falhas de backup/restore mostram uma causa resumida no status e no log de operações;
o logger do servidor mantém a exceção completa para diagnóstico. Se o erro apontar
MongoDB indisponível, confirme primeiro a prontidão/conexão do Mongo antes de repetir
o backup; não aumente indiscriminadamente os retries em uma dependência recusando conexão.

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

## Resultado ambíguo em upload Telegram

Antes de `sendDocument`, o Nebula grava no journal local a intenção da parte
(UUID do arquivo, índice da parte e identidade do canal). Se a chamada retornar
erro de transporte ou resposta ilegível, o motor pesquisa o histórico do canal
via sessão MTProto e só considera correspondência exata quando UUID, marcador
`PARTE: n/total` e documento coincidem. Uma parte encontrada é registrada como
confirmada no journal para retomar sem novo POST. Se a consulta falhar ou não
concluir, a tentativa não envia a parte: o item retorna à fila automática com
backoff e deve consultar o histórico novamente. No Python, o stage local é
preservado quando há partes confirmadas para permitir essa retomada. Se a busca
concluir e não encontrar a mensagem, o retry automático pode enviar; atrasos de
visibilidade ainda podem resultar em duplicata. Portanto, não se promete
exactly-once.
Homologação real do cliente/canal, runtime instalado e produção continuam
obrigatórios antes de release.

O runtime Python também grava `upload_claim_id` exclusivo em cada claim
(`legacy`, `parallel` e `small`). Checkpoint, progresso, falha e conclusão
filtram pelo `_id`, `worker_id`, `status=uploading` e token da claim. Se outra
instância já re-enfileirou e reivindicou o documento, o worker anterior para e
mantém o staging, sem persistir ou apagar o estado novo. Ao concluir/falhar, o
token é removido; o claim seguinte sempre gera outro. Ausência de token ou
worker no runtime atual falha em modo seguro.

O pacote Linux deve incluir `upload_checkpoint.py`, importado pelo `main.py`;
os testes de pacote comparam o módulo copiado com a fonte. O validador de stage
Windows também compara o hash SHA-256 do módulo para rejeitar uma pasta stage
antiga ou mista.

### Fencing do ciclo de upload Mongo

Cada reivindicação (`lease_id`) identifica uma geração do upload, não apenas o
worker. O watcher deve repassar o token ao motor e progresso, falha,
cancelamento, conclusão (inclusive duplicata) e liberação para retry devem
condicionar a escrita a `worker_id` + `lease_id`. Assim, uma resposta atrasada do
ciclo anterior não altera o documento quando o mesmo worker recebe uma nova
reivindicação. Documentos legados sem token só aceitam chamadas legadas sem
token; não misture uma chamada tokenizada com estado legado. Isso não desfaz
uma chamada já aceita pelo Telegram: reconciliação e retries automáticos são
mantidos, com risco residual de duplicata e sem garantia exactly-once.
