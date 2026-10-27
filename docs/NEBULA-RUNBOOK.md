# Runbook operacional do Nebula

## Cooldown de falhas por caminho

Os rastreadores C# e Python comparam caminhos sem diferenciar maiúsculas/minúsculas no Windows e com diferenciação nos demais sistemas. As cópias Python normalizam também as chaves antigas do cache ao carregá-lo. Assim, em Linux, uma falha em `Show.mkv` não coloca `show.mkv` em cooldown: os nomes podem representar arquivos distintos em um filesystem case-sensitive. As regressões automatizadas validam o comparer em Windows; Linux nativo ainda precisa ser executado para homologação de plataforma.

## Validação do runtime Linux empacotado — 2026-10-25

O job de release executa um smoke do conteúdo efetivamente empacotado depois de instalar o `requirements.lock` numa venv temporária: compara o lock embarcado e executa import de `main` e `strm_downloader.py --help`, com timeout e sem `.env`, chamada HTTP ao Telegram, MongoDB, FTP ou início do worker. Ver `MulletaFlix-master/docs/nebula-strm-download.md`.

O gate foi implementado e sua CI/execução Linux hospedada continua pendente; não interpretar a presença do teste no workflow como resultado de execução.

## Sinal de prontidão versus primeira varredura — 2026-10-25

O worker emite `MONGO-READY` depois de atualizar em memória o cache de duplicidade consultado no MongoDB e antes de descobrir/varrer as raízes STRM. Esse sinal não confirma a criação de índices físicos no MongoDB, a conclusão do scan ou a existência de downloads na fila.

O status agora separa candidatos encontrados no título atual de itens realmente na fila; o downloader processa esses candidatos sequencialmente, então a contagem por título não é uma fila global. Falha ao enumerar uma raiz emite `[STRM-SCAN-ROOT-ERROR]` e aparece como erro de descoberta em vez de “sem mídia elegível”. Ainda é necessário validar esses estados no runtime instalado; os testes locais não comprovam produção.

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

## Downloader não fica pronto ao iniciar

O startup do Downloader tenta até três vezes quando o worker encerra antes do sinal de prontidão ou quando a espera expira (5 s e 10 s entre tentativas). Consulte `NebulaFtp/Logs` e `NebulaFtp/Status` para identificar a causa de cada falha; o retry não substitui correção de dependência/configuração e não se aplica a toda exceção. Após a recuperação, confirme scan e transferência com mídia controlada.

## Lease e recuperação de uploads

- Cada claim de upload grava `worker_id`, um `lease_id` exclusivo da geração e `lease_until`. A lease dura 300 segundos; C# e Python renovam o heartbeat a cada 60 segundos e nos checkpoints de progresso.
- Mutação de progresso, falha, cancelamento, conclusão e liberação de retry só é aceita para o mesmo `_id`, `worker_id` e `lease_id`, enquanto `lease_until` ainda está no futuro. Uma lease expirada não pode ser renovada ou readquirida pelo mesmo ciclo, mesmo que o scanner ainda não tenha emitido o próximo claim.
- Antes de repetir uma tentativa Telegram após falha ambígua, o worker revalida a geração no Mongo. Se perder ou não conseguir renovar a lease, interrompe novas tentativas, não altera o registro da geração seguinte e preserva o arquivo local para recuperação.
- O retry automático permanece habilitado. O fencing impede gravações de workers obsoletos no Mongo; ele não desfaz uma parte que o Telegram já aceitou. Portanto não representa garantia de entrega exatamente uma vez.
- O scanner Mongo marca cada registro `staging` antes de resolver o caminho e a pasta de destino. Se esse processamento falhar antes de inserir o item na fila, o scanner devolve o registro a `queued` e agenda nova tentativa para 60 segundos depois; registros adiados não são reclamados antes de `next_retry_at`. O update exige que o status ainda seja `staging`, para não reabrir um upload que um worker já assumiu. Se o Mongo também recusar a gravação de recuperação, o log informa a falha e o registro pode continuar `staging`; restaure a conexão e inspecione esse registro antes de qualquer alteração manual.
- Em incidente, confira no log a mensagem de lease perdida/expirada e o estado `lease_until`; deixe o scanner de recuperação criar uma nova geração. Não altere `worker_id`/`lease_id` manualmente e não apague o arquivo de stage para “destravar” a fila.

## Checkpoint local e envio Telegram ambíguo

- O worker Python guarda o journal em `<SESSIONS_DIR>/upload-checkpoints` (ou no diretório definido por `NEBULA_UPLOAD_CHECKPOINT_DIR`). O journal é atômico e registra a intenção da parte antes do POST; cada confirmação associa UUID da geração, índice e SHA-256 do conteúdo.
- Se a chamada Telegram falhar sem resposta conclusiva, o worker procura a parte pelo UUID/índice/hash em todos os bots configurados. Só uma busca completa sem correspondência após a janela de visibilidade (até 31 s) permite retry; erro, truncamento ou falta de capacidade de busca é inconclusivo e agenda retry automático após 60 s, preservando o stage.
- Na retomada, checkpoints do journal e Mongo são reconciliados. Progresso Mongo anterior sem hash só é migrado se a identidade do arquivo for compatível por tamanho e data; divergência/corrupção exige nova geração para não misturar conteúdo. Falha ao ler/gravar o journal não autoriza envio e é adiada para retry.
- Se uma parte de um lote paralelo falhar ou o worker for cancelado, as tarefas irmãs são canceladas e aguardadas antes de marcar o item como falho/liberar a fila. O journal mantém a reconciliação para partes Telegram cuja aceitação ficou ambígua; confira o retry antes de remover checkpoints.
- O scanner reencaminha uploads com lease expirada sem reiniciar o serviço; o fencing impede o worker antigo de alterar o registro da nova geração.
- Limite conhecido: Telegram não fornece chave de idempotência para este fluxo e uma parte aceita pode continuar invisível à busca além da janela. A política mantém retry automático conforme decisão do usuário, com risco residual de duplicata; não prometa exactly-once. Em incidente, preserve o stage e os arquivos do journal para investigação.

## Reconciliação do stage

O scanner identifica registros por `local_path` exato ou pela combinação de nome, pasta (`parent`) e status. Títulos iguais em séries/bibliotecas distintas não indicam que o arquivo atual já foi enviado; preserve o arquivo de stage se a reconciliação Mongo falhar e investigue o `parent` resolvido antes de qualquer remoção manual.

## Download de origem sem suporte a Range

Quando a origem ignora `Range`, um destino existente só é reutilizado se o tamanho corresponder ao `Content-Length`. Sem tamanho confiável, o downloader busca novamente para um arquivo temporário e mantém o destino anterior até concluir; divergência de tamanho rejeita a resposta e preserva o destino.

## Backup e restore Supabase

O backup automático MongoDB → Supabase tenta executar imediatamente ao iniciar o ciclo. Em sucesso, aguarda o intervalo configurado (atualmente 1 hora). Em resultado malsucedido, exceção transitória ou timeout, mantém o serviço ativo e tenta novamente após 1, 2, 4, 8, 16 e 32 minutos, limitado ao intervalo normal (mínimo de 5 minutos). Um sucesso zera o contador e restaura a cadência normal. O cancelamento solicitado pelo encerramento do serviço interrompe o ciclo. O retry é automático; acompanhe `[SUPABASE-AUTO-SYNC]` e `LastBackupStatus` para confirmar o resultado, pois tentativas não equivalem a backup concluído.

Nas consultas ao cursor e ao histórico remoto, respostas HTTP 429 e 5xx são repetidas até três tentativas. Quando a resposta inclui `Retry-After` válido, o cliente aguarda esse período (limitado a 60 segundos); sem cabeçalho, com data expirada ou valor inválido, usa o atraso incremental curto local. Falhas de autenticação e outras respostas não transitórias não são repetidas. Cancelamento do serviço também interrompe a espera.

As leituras do cursor do catálogo e do histórico de backups (`nebula_backups`) também repetem até três vezes quando recebem HTTP 429/5xx, falha de rede ou timeout; erros HTTP permanentes não são repetidos. Se as três tentativas falharem, o ciclo externo mantém o erro visível e agenda o próximo retry pelo backoff acima.

O status e o progresso de backup/restore mostram a mensagem operacional normalizada e limitada a 512 caracteres; o log estruturado mantém a exceção completa para diagnóstico. Se o erro indicar conexão recusada em `localhost:27017`, confirme que o serviço MongoDB está iniciado e ouvindo nessa porta. A tentativa automática será retomada pelo backoff; não declare backup concluído até `LastBackupStatus` confirmar sucesso.

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

## Retomada de downloads multipart

O downloader STRM e a materialização do feeder aguardam todos os workers multipart terminarem antes de propagar uma falha. Assim, partes concluídas continuam disponíveis para retomada na próxima execução; um destino preexistente só é substituído após a montagem completa. Em falhas, preserve os arquivos `.partN`/`.download.partN` para o retry automático e não os remova enquanto o downloader estiver ativo. A retomada revalida os tamanhos locais e solicita somente os intervalos faltantes.

## Validação local do pathio — 2026-10-24

O conflito preexistente em `nebula/ftp/pathio.py` foi resolvido preservando o cache de playback com fallback para Telegram e a leitura de hierarquias Mongo com `parent` BSON ObjectId ou caminho legado. A listagem continua aplicando filtros de publicação/visibilidade, deduplicação e cache do documento usado para compor cada entrada. Suíte Nebula Python 3.10: `python -m pytest tests -q` passou 167, ignorou 1 e falhou 0; `python -m compileall -q ftp tests` passou. Ruff não estava instalado. Resolução staged, sem commit; alterações preexistentes dos demais arquivos permaneceram. Isto não valida runtime/produção nem fecha a homologação formal (2/7, 28,6%). Ver sessão `2026-10-24-MulletaFlix-nebula-pathio-merge-validation-2219`.

## Paridade da cópia Python empacotada — 2026-10-24

A cópia de runtime em `MulletaFlix-master/Tools/NebulaPython/ftp/pathio.py` já tinha resolução Mongo por parent BSON/caminho e lease do cache, mas não filtrava itens sem payload Telegram publicado nem diretórios sem descendentes publicados, não deduplicava nomes equivalentes e não falhava quando uma parte remota era truncada. O fluxo foi alinhado à cópia Nebula aninhada, mantendo o lease e erros explícitos de leitura. Testes focados passaram 42/42; suíte completa do pacote Python passou 696/696; `compileall` e `git diff --check` passaram. WSL/Docker indisponíveis; Linux nativo e runtime instalado não verificados. Sem release/deploy; homologação formal continua 2/7 (28,6%). Ver sessão `2026-10-24-MulletaFlix-nebula-packaged-pathio-parity-2224`.

## Descendentes publicados em hierarquia Mongo por ObjectId — 2026-10-24

Teste RED mostrou que a navegação aceitava `parent` por ObjectId, mas `_has_published_descendant` só pesquisava caminhos; por isso uma pasta `Series` com mídia publicada abaixo podia desaparecer. Agora a busca percorre diretórios por níveis, propagando IDs e aliases legacy, tanto na fonte empacotada quanto no Nebula aninhado. Suíte empacotada passou 697/697; Nebula aninhado passou 167, ignorou 1, falhou 0; `compileall` passou nas duas. A homologação permanece 2/7; Linux e produção continuam sem validação. Ver sessão `2026-10-24-MulletaFlix-nebula-objectid-directory-visibility-2230`.
