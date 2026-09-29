# Roadmap de evolução tecnológica — servidor e frontend web

**Data:** 28/09/2026
**Status:** em execução progressiva; consultar “Registro de execução” para evidências e pendências.
**Escopo:** servidor MulletaFlix para Windows e Linux e frontend web distribuído junto ao servidor. O aplicativo Android/iOS não faz parte deste roadmap.

## Objetivo

Consolidar oportunidades de evolução em confiabilidade, desempenho, segurança, operação e experiência do usuário. Priorizar por evidência e risco, preservando a arquitetura existente e evitando introduzir infraestrutura ou dependências sem benefício medido.

## Estado atual observado

- O servidor já compila para `net10.0`; .NET 10 é LTS, com suporte anunciado até novembro de 2028.
- O cliente web usa React 18, TypeScript, Vite 7, Material UI, TanStack Query e Swiper.
- Há ferramentas existentes de testes: Vitest, Playwright e Selenium/Cucumber, além de `build:check`, ESLint e Stylelint.
- O servidor já integra MariaDB, MongoDB/Nebula, Telegram, FFmpeg e cache local de reprodução. Este plano propõe melhorar sua operação, não substituir esses componentes sem uma decisão baseada em métricas.

## Princípios e limites

1. Medir antes e depois; não aceitar ganhos presumidos.
2. Resiliência e modo degradado: indisponibilidade de internet, GPU ou provedor externo não pode impedir o uso normal do servidor.
3. Dados e correções reversíveis: manter origem, histórico e opção de rollback para mudanças de catálogo.
4. Processar grandes bibliotecas incrementalmente, com limites de concorrência e cancelamento.
5. Manter Windows e Linux como plataformas de produção de primeira classe.
6. Não adotar um broker externo, cache distribuído, SSR ou reescrita do frontend sem demonstrar necessidade.
7. Curadoria por IA está fora do escopo ativo e permanece apenas no backlog futuro; não iniciar sua implementação neste ciclo.

## Skills por frente de trabalho

As skills abaixo são roteamento de especialidade por tarefa; Gauntlet Loop define a barra de qualidade para todo código e Fable Method/Loop/Judge organiza evidência, execução e revisão adversarial. Caveman orienta concisão das mensagens de trabalho; Cavecrew é usado para investigação/delegação com escopo isolado.

| Frente | Skills principais |
| --- | --- |
| T0 baseline e requisitos operacionais | `backend-architect`, `data-engineer`, `fable-method` |
| T1 observabilidade e saúde | `backend-architect`, `gauntlet-loop`, `fable-judge` |
| T2 fila Nebula e recuperação | `backend-architect`, `data-engineer`, `gauntlet-loop` |
| T3 reprodução/cache | `backend-architect`, `gauntlet-loop`, `fable-loop`, `fable-judge` |
| T4 backups/restauração e T5 bancos | `data-engineer`, `backend-architect`, `gauntlet-loop` |
| T6 APIs/segurança e T7 FFmpeg | `backend-architect`, `gauntlet-loop`, `fable-judge` |
| T8 catálogo determinístico | `data-engineer`, `backend-architect`, `gauntlet-loop` |
| W1 experiência e W2 acessibilidade | `frontend-design`, `gauntlet-loop`, `fable-judge` |
| W3 desempenho e W4 automação web | `frontend-design`, `backend-architect` (contratos), `gauntlet-loop`, `fable-loop` |

## Backlog priorizado

### Fase 0 — Baseline, inventário e critérios de qualidade (P0)

- [ ] **T0.1 — Definir baseline de produção.** Registrar tempo de inicialização, memória, uso de disco, latências p50/p95 dos endpoints mais usados, duração das tarefas Nebula e volume de mídia processado.
  - [x] Capturar amostra inicial read-only da instância instalada: versão/ambiente, startup aproximado, memória/CPU, espaço livre e latência local de endpoints públicos.
  - [ ] Repetir sob carga representativa e incluir métricas autenticadas de duração/volume Nebula, footprint dos dados, Linux e amostra temporal suficiente.
- [x] **T0.2 — Mapear fluxos e dependências.** Mapa estático documentado abaixo, com fontes de código por fluxo. Lacunas operacionais e medições continuam nas tarefas correspondentes.
- [ ] **T0.3 — Criar conjunto de cenários representativo.** Incluir biblioteca pequena/grande, mídia local e Telegram, interrupção de banco/rede, cache cheio, Windows e Linux.
  - [x] Cobrir catálogo STRM de 10 e 2.000 títulos; falha de fetch seguida de retry/cache hit; fetch de rede com erro; cache cheio com lease; stream local e primeiro bloco sem esperar o próximo fetch.
  - [x] Adicionar job CI focado em cenários Nebula e health checks de dependências para `ubuntu-latest` e `windows-latest`; lock de arquivo com `FileShare.None` fica explicitamente restrito ao Windows.
  - [x] Cobrir a transição do health check Mongo indisponível (`Unhealthy`) para disponível (`Healthy`) em verificações sucessivas, sem reiniciar o servidor.
  - [x] Corrigir a validação condicional do keystore no workflow: steps passam a consultar flag de ambiente, sem referenciar `secrets` diretamente em `if`.
  - [ ] Confirmar os dois jobs em execução remota após publicação do workflow e adicionar recuperação integrada banco/Telegram sem depender de serviços ou credenciais de produção.
- [ ] **T0.4 — Definir limites de regressão.** Fixar budgets iniciais para tempo de boot, tamanho do bundle web, latência de busca, espaço temporário e memória; calibrar com medições reais, não valores arbitrários.
  - [x] Medir o artefato web de produção e conferir o gate existente de 1.536 KiB por arquivo JS/CSS.
  - [x] Criar relatório local reproduzível para tamanhos brutos, gzip e Brotli do HTML inicial, assets referenciados e conjunto JS/CSS completo.
  - [ ] Completar séries representativas de boot, busca, recursos, tráfego comprimido e uso do cache antes de definir budgets globais.

**Aceite:** relatório reproduzível com ambiente, comandos, métricas iniciais e limitações; sem alteração de comportamento do produto.

#### Mapa de fluxos e dependências (T0.2)

| Fluxo | Caminho observado | Persistência/integração | Consumidores e evidência no código |
| --- | --- | --- | --- |
| Inicialização Nebula | `ApplicationStarted` aciona `NebulaHostedService`; inicia envio, downloader STRM e, se configurado, montagem do drive em sequência. Após a montagem, chama `ILibraryManager.ValidateMediaLibrary` para indexar a unidade. | Configuração `nebulaftp`; `NebulaFtpManager` constrói os componentes e controla seu ciclo de vida. | `NebulaHostedService.StartAsync`/`OnApplicationStarted`; `NebulaFtpManager.StartEnvioAsync`/`StartDownloaderAsync`; registro em `Startup` e `CoreAppHost`. |
| Upload local para Telegram | `NebulaStagingWatcher` observa staging, recupera fila do MongoDB, ordena prioridades, reivindica tarefas e chama `NebulaUploadEngine`; o engine valida duplicidade, envia partes ao Telegram e conclui o documento MongoDB. | MongoDB `ftp.files` mantém estado, caminho, partes e dados de envio; Telegram armazena as partes. Sincronização com Supabase é complementar e pode sincronizar nós após escrita. | `NebulaStagingWatcher.Start`/`EnqueueFile`/worker; `NebulaUploadEngine.ProcessFileUploadAsync`; `NebulaFtpManager.StartEnvioAsync`. |
| Download de STRM para staging | `NebulaDownloaderEngine` resolve o `.strm`, baixa/combina partes, grava o arquivo no staging, registra o trabalho no MongoDB e emite `OnUploadReady`; o watcher encaminha o arquivo ao upload. | Origem de partes Telegram e metadados MongoDB; staging local é temporário e a fila de envio continua sendo dona da etapa de upload. | `NebulaDownloaderEngine` e `EnqueueFileInMongoAsync`; callback ligado em `NebulaFtpManager.StartDownloaderAsync`; `NebulaStagingWatcher.EnqueueMediaFromDownloader`. |
| Reprodução via N: / FTP | `NebulaFileSystem` lista documentos virtuais MongoDB; leitura usa `NebulaChunkedStream`, que busca partes do Telegram e adquire lease pelo accessor do cache compartilhado. | MongoDB `ftp.files` é catálogo/metadado; Telegram é origem dos bytes; cache local é acelerador temporário, não fonte de verdade. | `NebulaFileSystem`/`NebulaChunkedStream`; `NebulaTelegramPool`; `NebulaPlaybackCacheAccessor`. |
| Reprodução HTTP Nebula | `NebulaHttpStreamServer` cria `NebulaChunkedStream` a partir do documento MongoDB e das partes Telegram; o mesmo accessor liga a stream ao cache configurado. | Mesma fonte MongoDB/Telegram; cache guarda blocos com limite, reserva, expiração e proteção de leases. | `NebulaHttpStreamServer`; endpoints de status, caminho e limpeza em `NebulaFtpController`; tela `dashboard/playback/nebulacache`. |
| Banco MariaDB do servidor | `Program` inicia ou detecta MariaDB local; `MySqlDatabaseProvider` configura provider EF, banco/schema e pool para os repositórios do servidor. | Dados Jellyfin/MulletaFlix em MariaDB; não confundir com MongoDB `ftp` usado pelo Nebula nem com SQLite legado do plugin. | `MariaDbProcessManager.StartMariaDbAsync`; `MySqlDatabaseProvider.Initialise`; contextos em `src/Jellyfin.Database/.../Contexts`. |
| MongoDB Nebula e sincronização Supabase | `NebulaMongoContext` conecta ao database `ftp` e usa coleções `files`, `users`, `bot_tokens` e `operation_replays`; `NebulaSupabaseSyncService` faz sincronização delta/backup e rotina periódica configurada pelo manager. | MongoDB é a fonte operacional Nebula; Supabase recebe cópia/sincronização remota e pode restaurar catálogo conforme fluxo de inicialização. | `NebulaMongoContext`; `NebulaFtpManager.StartEnvioAsync`/`BackupMongoToSupabaseAsync`; `NebulaSupabaseSyncService.StartContinuousSync`/`PerformBackupAsync`. |
| Backups | `FullSystemBackup.BackupService` cria/restaura arquivos de backup do servidor via API própria. Em separado, `NebulaSupabaseSyncService` sincroniza dados MongoDB/usuários ao Supabase. | Backup local do sistema e cópia remota Nebula têm conteúdos, destinos, agendamentos e validações diferentes; sucesso de um não prova restauração do outro. | `BackupController`/`BackupService`; `NebulaFtpController`/`NebulaFtpManager`/`NebulaSupabaseSyncService`; telas web de backups e manutenção Nebula. |
| Varredura de bibliotecas e metadados | O scanner Jellyfin indexa caminhos configurados, inclusive `.strm`; eventos de item alimentam `NebulaMetadataExportService` e `NotificationsLibraryNotifier`. O watcher de staging é outro scanner, dedicado aos arquivos a enviar. | MariaDB guarda catálogo Jellyfin; MongoDB/Supabase recebem catálogo operacional Nebula e metadados exportados para a árvore virtual. | `ILibraryManager` e eventos `ItemAdded`/`ItemUpdated`; `NebulaMetadataExportService`; `NotificationsLibraryNotifier`; `NebulaStagingWatcher`. |
| Solicitações e reports na web | Autocomplete consulta `UserFeedback/MediaSuggestions`; envio grava `ActivityLog` e chama `PrioritizeMedia`; painel carrega atividades e cruza títulos com `MediaRequestCatalog` para separar pendentes/incluídos. Reports de reprodução também são atividades. | Activity log persiste solicitações/reports; catálogo STRM é construído de raízes configuradas e cacheado pelo manager; prioridade é aplicada às filas Nebula. | `UserFeedbackController`; `NebulaFtpManager.SearchMediaSuggestions`/`GetMediaSuggestionCatalog`/`PrioritizeMedia`; `UserFeedbackListPage`; toolbar web. |

**Limites deste mapa:** rastreamento de código e contratos, não medição de produção. Não comprova latência, throughput, restauração real, estado de serviços ou sincronização de dados em uma instalação ativa; esses itens seguem em T0.1/T0.3/T0.4, T1, T2 e T4.

#### T0.1 — Amostra operacional inicial (parcial; 28/09/2026)

- Processo observado: servidor instalado em `C:\Program Files\MulletaFlix\Server\MulletaFlix.exe`, versão `12.1.5`, PID `11108`; não houve restart nem alteração da instalação.
- Em 50 GETs sequenciais somente de `/ready`, todas as respostas foram HTTP 200. Header do servidor: p50 **5,41 ms**, p95 **9,74 ms**; tempo medido pelo cliente: p50 **7,47 ms**, p95 **15,75 ms** (percentis nearest-rank, N=50).
- Working set do processo foi observado em **1,84 GiB** antes da bateria e **3,17 GiB** logo depois; após 10 segundos sem novas chamadas, **3,22 GiB**, com **1,38 GiB** de memória física disponível no host. Isto é correlação durante a amostragem, não prova de que as requisições causaram o crescimento; nenhuma outra atividade do host foi isolada.
- Limite: este é um endpoint de prontidão, não p50/p95 dos endpoints mais usados, nem duração de startup, throughput de Nebula ou tamanho da biblioteca. T0.1 continua aberta até coleta representativa, com janela sem concorrência e medição dos cenários definidos.

### Fase 1 — Observabilidade e operação (P0)

- [ ] **T1.1 — Adicionar instrumentação OpenTelemetry.** Traces e métricas correlacionando requisição, consulta, varredura, transferência Nebula, chamadas Telegram e sessão de reprodução.
  - [x] Parcial: listener HTTP Nebula cria spans `ActivitySource`, aceita `traceparent` W3C sem preservar `tracestate`, registra método/rota/status e tipo de erro sem incluir caminho, token ou identificador de mídia.
  - [x] Parcial: exportação OTLP opt-in por variáveis padrão OpenTelemetry para traces ASP.NET Core/Nebula e métricas de requisições ASP.NET Core; endpoint compartilhado ou endpoint específico por sinal. Sem endpoint, nenhum exporter é registrado. Caminho, query, URL completa e user-agent são removidos dos spans HTTP. Ver [guia de observabilidade](observability.md).
  - [x] Parcial: ciclo de upload Nebula emite span filho (quando há contexto pai) e métricas de total/duração com resultado de baixa cardinalidade; títulos, nomes, caminhos e IDs não são incluídos.
  - [x] Parcial: downloads multipart STRM/arquivos físicos emitem span e métricas de total/duração; URL, query, nome e caminho não são atributos. Cancelamento só é classificado como cancelado quando token de cancelamento foi solicitado; timeout continua falha.
- [ ] **T1.2 — Criar indicadores de operação no painel.** Saúde/degradação de MongoDB e MariaDB, espaço do cache, fila mais antiga, itens em retry, throughput e falhas por etapa.
  - [x] Parcial: painel Nebula exibe health do MariaDB usando o check nomeado do contexto principal e atualiza a cada 30 s; não expõe detalhes de conexão. O MongoDB já era consultado pelo endpoint de saúde Nebula.
  - [x] Parcial: painel mostra tamanho/arquivos do cache, leases de reprodução, pré-cache e espaço no volume; cache não inicializado aparece como indisponível, em vez de zeros falsos. Bytes/arquivos são contadores em memória, mantidos nas mutações e carregados com snapshot inicial.
  - [x] Parcial: endpoint leve conta itens `staging/queued` e falhas com `retry_after`; busca somente o item pendente mais antigo por projeção/limite 1. A tela atualiza esse resumo a cada 30 s sem materializar a fila inteira.
  - [x] Parcial: resumo Mongo agrega arquivos concluídos e bytes enviados nos últimos 60 minutos, além de mídias com falha recente agrupadas por etapa; painel exibe estes dados sem nome/caminho/IDs como métricas. A janela é móvel e índices específicos suportam as consultas.
  - [ ] Pendente: validar agregações e índices contra MongoDB de produção/volume representativo; instrumentar também exceções e falhas não encaminhadas pelos ramos explícitos do upload, além de confirmar completude do histórico.
- [ ] **T1.3 — Separar logs operacionais de auditoria.** IDs de correlação, retenção configurável e remoção/redação de tokens, credenciais, URLs assinadas e dados pessoais.
  - [x] Parcial: eventos de exceção HTTP registram o `TraceIdentifier`/`X-Correlation-ID` seguro para correlacionar resposta e log; não adicionam query string ou URL completa.
  - [x] Parcial: limpeza diária agora inclui logs Serilog e aplica `LogFileRetentionDays`; sink padrão não limita mais arquivos por contagem. A configuração padrão legada é normalizada antes do logger iniciar; override `logging.json` não é alterado.
  - [ ] Pendente: concluir auditoria/redação de todos os logs e testar retenção com configurações personalizadas além dos cenários focados.
- [ ] **T1.4 — Expor health checks úteis.** Diferenciar processo ativo de serviço pronto; reportar dependências essenciais e estado degradado sem expor segredos publicamente.
  - [x] O `/ready` do Nebula usa `GetComponentHealthAsync` e verifica MongoDB + listeners FTP/HTTP; não materializa listas completas de uploads para responder readiness.
- [x] **T1.5 — Definir alertas e diagnósticos.** Alertar fila sem progresso, backup vencido, disco próximo do limite, erro repetido de provedor e falha de restauração.

**Aceite:** um incidente de teste pode ser rastreado do endpoint/tarefa até a dependência causadora; health check não revela configuração sensível; métricas não usam títulos, caminhos ou IDs de usuário como labels de alta cardinalidade.

### Fase 2 — Nebula: fila, concorrência e recuperação (P0)

- [x] **T2.1 — Especificar máquina de estados durável.** Estados, transições, lease/heartbeat, retomada após reinício e distinção entre retry, falha permanente e cancelamento documentados abaixo. A implementação está separada em T2.2, T2.4 e T2.5.
- [ ] **T2.2 — Fortalecer idempotência e deduplicação.** Revalidar identidade canônica e estado Telegram antes de baixar, enviar ou reenfileirar; não duplicar arquivos concluídos.
  - [x] Decisões de deduplicação de mídia exigem `completed` e conjunto de partes contíguo, com IDs Telegram e soma de tamanhos igual ao arquivo; compatibilidade de documentos legados sem `parts` exige ID raiz e tamanho válido.
  - [x] Falha com partes parciais mantém arquivo local para retomada; parte isolada deixa de ser prova de conclusão.
  - [ ] Validar concorrência real de produtores/retries contra MongoDB, identidade canônica entre caminhos diferentes e estado remoto Telegram indisponível.
- [ ] **T2.3 — Tornar prioridade observável e consistente.** Aplicar prioridade explícita de solicitações, ordem de categorias configurada e A–Z dentro da categoria; mostrar na interface posição, motivo e próximo item.
  - [x] Parcial: dashboard Nebula expõe posição planejada na varredura do downloader, justificativa (solicitação ou categoria/A–Z) e próximo título; a posição é explicitamente do plano transitório e não representa fila persistida de upload.
  - [x] Parcial: o dashboard também exibe posição, razão e próximo dequeue do snapshot atual da fila fairness dos workers; identifica que títulos pendentes no Mongo ainda não admitidos não fazem parte desse snapshot.
  - [ ] Pendente: tornar posição/prioridade duráveis e reconciliáveis para toda a fila Mongo, validar concorrência/prioridade após restart e alinhar a ordem de categorias a uma configuração única, em vez de manter regras duplicadas.
- [ ] **T2.4 — Implementar backpressure e fairness.** Limites independentes por operação/rede, proteção contra rajadas e evitar starvation de tarefas não prioritárias.
  - [x] Parcial: fila de upload usa fairness FIFO ponderada; após quatro seleções prioritárias, escolhe um item normal se houver item normal aguardando. Sem fila normal, prioritários continuam sem pausa.
  - [ ] Pendente: definir/validar limites independentes de rede/operação, controle de rajadas e fairness sob carga concorrente real, incluindo trabalho já reivindicado por múltiplos workers.
- [ ] **T2.5 — Adicionar recuperação operacional.** Retentativas com backoff/jitter, limite de tentativas, fila de falhas com reprocessamento administrativo e cancelamento seguro.
  - [x] Parcial: backoff/jitter, limite de 8 tentativas, reprocessamento de falhas/cancelamentos e cancelamento persistido com checkpoint entre partes; dashboard administrativo lista e controla uploads ativos.
  - [ ] Pendente: exercitar cancelamento/falha/restart com MongoDB isolado e ciclo real Telegram; auditar corridas de múltiplos workers e resposta incerta do Telegram.
- [ ] **T2.6 — Revisar limpeza e startup.** Varredura/cleanup incremental, concorrência limitada, checkpoint e progresso reportado; inicialização não deve bloquear o host por uma limpeza completa.
  - [x] Parcial: limpeza de staging sai do loop principal, executa em segundo plano e impede sobreposição; verifica cancelamento entre entradas e informa início, raízes concluídas e duração. Encerramento aguarda a limpeza.
  - [ ] Pendente: travessia incremental com checkpoint persistido/reiniciável e progresso detalhado dentro de raízes grandes; teste de volume, startup e cancelamento em filesystem/Mongo representativos.
- [ ] **T2.7 — Atualizar a listagem virtual após upload Nebula.** Persistir primeiro `completed` e as partes Telegram no MongoDB; em seguida invalidar somente a pasta afetada via rclone RC `vfs/refresh`. Falha no refresh não reverte o upload; não copiar a mídia integral para N: e não reduzir globalmente `--dir-cache-time` salvo como fallback medido.

**Aceite:** reiniciar o servidor durante download/upload retoma ou encerra o trabalho de modo consistente; cenário de retry não produz duplicação; prioridades efetivas coincidem com a ordem exibida; tarefas não ficam indefinidamente sem progresso.

#### T2.1 — Contrato da máquina de estados da fila de upload

Esta seção define o contrato-alvo para upload Nebula no MongoDB. A especificação está concluída; implementação parcial está registrada em T2.2/T2.4/T2.5 e no histórico abaixo. A fila ainda mantém compatibilidade com `queued`, `staging`, `uploading`, `failed` e `completed`, com `cancelled` persistido em ações explícitas. `modified_at` continua funcionando como lease implícito de uma hora; heartbeat/lease explícito e fencing permanecem pendentes. Não confundir a cobertura parcial com o contrato inteiro implementado.

Estados canônicos pretendidos:

| Estado | Significado | Terminal? | Campos de controle esperados |
| --- | --- | --- | --- |
| `staging` | Arquivo detectado/registrado, mas ainda indisponível ou não validado para upload. | Não | `queued_at`, `modified_at` |
| `queued` | Pronto para um worker reivindicar agora. | Não | `queued_at`; sem lease ativo |
| `uploading` | Worker possui lease válido e pode gravar progresso/partes. | Não | `worker_id`, `lease_id` único por reivindicação, `lease_until`, `heartbeat_at`, `attempt_started_at` |
| `retry_wait` | Falha transitória; aguarda `retry_after` antes de voltar a `queued`. | Não | `retry_count`, `retry_after`, `last_failure_at`, `failure_stage`, `failed_reason` sanitizado |
| `failed` | Falha permanente ou limite de tentativas excedido; exige ação/reprocessamento administrativo. | Sim, até ação explícita | `retry_count`, `last_failure_at`, `failure_stage`, `failed_reason`, `terminal_reason` |
| `cancelled` | Operador solicitou cancelamento; worker encerrou cooperativamente e não iniciará novas partes. | Sim, até ação explícita | `cancel_requested_at`, `cancelled_at`, `cancelled_by` quando disponível |
| `completed` | Todas as partes foram validadas e os identificadores Telegram persistidos. | Sim e imutável no fluxo normal | `completed_at`, `parts`, `tg_file_id`, tamanho final |

Transições permitidas:

| Origem | Evento/guarda | Destino | Efeito obrigatório |
| --- | --- | --- | --- |
| ausente | Arquivo detectado | `staging` ou `queued` | Upsert idempotente; nunca substituir mídia `completed`. |
| `staging` | Arquivo estável e validado | `queued` | Preservar identidade e tempo original de entrada na fila. |
| `queued` | Claim atômico e elegível | `uploading` | Criar novo `lease_id`, definir dono/expiração e incrementar tentativa. |
| `retry_wait` | `retry_after <= now` | `queued` | Manter partes parciais válidas e identidade; limpar somente erro retryável anterior. |
| `uploading` | Parte confirmada | `uploading` | Persistir progresso antes de avançar; renovar `heartbeat_at`/`lease_until` sob ownership. |
| `uploading` | Todas as partes confirmadas, tamanho/identidade válidos | `completed` | Persistir conclusão atomicamente antes de notificar Supabase/rclone; remover campos do lease/retry. |
| `uploading` | Erro transitório e tentativas abaixo do limite | `retry_wait` | Aplicar backoff com jitter e limite; preservar somente partes verificadas. |
| `uploading` | Erro permanente ou limite atingido | `failed` | Não fazer retry automático; preservar diagnóstico sanitizado e partes para inspeção/reprocessamento seguro. |
| `uploading` | Lease expirado ou processo encerrado | `queued` | Recuperação por lease expirado; manter partes verificadas. Worker antigo perde escrita via fencing. |
| `queued`, `staging`, `retry_wait` | Cancelamento explícito | `cancelled` | Cancelar sem claim e impedir novos claims. |
| `uploading` | Cancelamento explícito | `uploading` até confirmação do worker, então `cancelled` | Persistir pedido; deixar a chamada Telegram em andamento terminar, gravar checkpoint confirmado e consultar o pedido antes de iniciar outra parte. Não converter shutdown em cancelamento explícito. |
| `failed`, `cancelled` | Reprocessamento administrativo explícito | `queued` | Registrar ator/horário/motivo; resetar apenas contadores/campos definidos pela ação, sem apagar partes válidas. |

Invariantes e compatibilidade:

1. Toda mutação de worker exige `_id + lease_id` (fencing token); `worker_id` sozinho não identifica uma execução e não pode autorizar um worker antigo após reaquisição.
2. Claim é uma única operação atômica condicional. Só `queued` e `retry_wait` elegível podem ser reivindicados; recuperação de `uploading` ocorre somente após expiração do lease, nunca por idade de `modified_at` isolada.
3. `heartbeat_at` atualiza em intervalos menores que o lease; lease deve tolerar pausas de processo/rede configuradas e ser renovado antes da expiração. Se renovar falhar, worker interrompe uploads e não pode concluir.
4. Cancelamento do host durante shutdown não equivale a cancelamento solicitado pelo usuário: checkpoint e expiração do lease permitem retomar. `cancelled` exige intenção explícita persistida.
5. `completed` é terminal para workers, varredura de staging e retries. Correção administrativa de registro concluído é fluxo separado, auditado e fora da transição normal.
6. Retries preservam partes confirmadas, mas verificam identidade/Telegram antes de reutilizar; nunca inferem conclusão apenas por `parts` não vazio.
7. Migração legada deve mapear `failed` com `retry_after` para `retry_wait`; `failed` sem data de retry para revisão/`failed`; `uploading` só pode ser retomado após confirmar instância única ou lease vencido. Registros sem campos novos continuam legíveis durante rollout; não fazer migração destrutiva em lote.
8. Estados/status e causas expostos à interface usam vocabulário estável e sem nomes de arquivo, caminhos ou segredos em labels de métricas.

Limite de evidência atual: `ClaimFileForUploadAsync` realiza claim atômico, mas usa `modified_at` com limiar fixo de uma hora; `RequeueInterruptedUploadsAsync` recupera uploads interrompidos no startup; gravações aceitam `worker_id` como proteção opcional, sem fencing token. Cancelamento persistido e falhas terminais já existem parcialmente; heartbeat/lease explícito e validação integrada permanecem pendentes. Essas diferenças justificam manter T2.2/T2.5 abertas e exigem rollout compatível com documentos antigos.

### Fase 3 — Reprodução e cache temporário (P0)

- [ ] **T3.1 — Formalizar o contrato do cache de reprodução.** Cache em disco com limite configurável, chave canônica, política de expiração/evicção, espaço reservado e comportamento quando o volume está cheio.
  - [x] Canonizar IDs MongoDB sem diferenciar caixa e caminhos absolutos com segmentos equivalentes; separar namespaces de ID, caminho e chave opaca, preservando sensibilidade a caixa em caminhos Linux. Identidades ausentes são rejeitadas no cache; streams sem identidade recebem chave exclusiva por instância.
  - [x] Expirar entradas após 1 hora sem atividade; manter limpeza periódica a cada 5 minutos e proteger leases ativos.
  - [x] Configurar cota máxima e reserva de espaço livre; evictar mídias inativas por LRU e aplicar limites na gravação e limpeza periódica.
  - [x] Quando cache/capacidade falhar, entregar os bytes ao fluxo de reprodução sem persistir o bloco.
- [ ] **T3.2 — Validar leitura em partes e prefetch.** Garantir que a mídia original começa a tocar enquanto o cache pré-carrega as partes necessárias, com limites de concorrência e cancelamento ao encerrar/trocar a sessão.
  - [x] Limitar a duas mídias com pré-cache integral ativo e quatro aguardando; não enfileirar prefetch-ahead além de dois chunks concorrentes. Os chunks antecipados usam o cache persistente compartilhado, evitando novo download do mesmo bloco entre ranges/leitores.
  - [x] Provar por teste do `NebulaChunkedStream` que o primeiro bloco é entregue e persistido enquanto o prefetch do próximo chunk continua bloqueado.
  - [x] Monitorar eventos `PlaybackStart`/`PlaybackStopped` por sessão; trocar/encerrar uma reprodução cancela o pré-cache da mídia anterior apenas quando nenhum outro cliente permanece nela. Eventos de parada sem identidade suficiente são ignorados; se ainda houver um range aberto, o cancelamento fica pendente até o último lease fechar, e novo playback remove essa intenção. Se a mídia retomar durante a resolução do cancelamento, o monitor reinicia o pré-cache.
  - [x] Um lease novo revoga uma intenção de cancelamento antes de ela sinalizar o token. Cancel callbacks usam `CancellationTokenSource.CancelAsync`; o descarte da origem espera esses callbacks sem bloquear a finalização síncrona do estado. Teste cobre callback deliberadamente bloqueado sem bloquear a chamada de cancelamento.
  - [ ] Validar ponta a ponta em runtime o vínculo entre eventos reais Jellyfin, caminho STRM, documento Mongo e chave de cache, além de confirmar reprodução contínua durante troca/parada. O cancelamento por lease continua como fallback com tolerância de 2 minutos para encerramentos sem evento.
- [x] **T3.3 — Preservar leases ativos.** Limpeza não remove conteúdo usado por leitores ou downloads em andamento; liberar lease mesmo em exceção, cancelamento e encerramento do servidor.
  - [x] Limpeza manual e por cota ignoram mídias com lease, prefetch ou fetch de chunk em andamento; mudança de configuração não interrompe sessões ativas.
  - [x] Lease é liberado mesmo quando a operação sob `using` lança exceção, quando o fetch subjacente é cancelado (a leitura cancela, mas o lease permanece com o chamador até ele descartar explicitamente) e quando o `Dispose()` do cache ocorre antes do lease individual ser descartado durante o encerramento do servidor.
- [x] **T3.4 — Prevenir duplicação de downloads concorrentes.** Uma única operação por parte/arquivo atende leitores simultâneos; cancelamento de um leitor não cancela nem duplica o download compartilhado.
- [x] **T3.5 — Exibir diagnóstico de cache.** Bytes e arquivos em cache, hits/misses, latência Telegram, prefetch em andamento, leases, erros e limpeza segura; DTO, endpoint e painel incluem também cancelamentos e métricas de execução da limpeza.
  - [x] Painel usa os nomes atuais do DTO e mostra ocupação da cota, limite configurado, espaço livre/reserva e leases ativos.
  - [x] DTO e painel exibem contadores desde a inicialização para hits/misses, tentativas/falhas de fetch, latência média, pré-cache ativo/em fila e erros de persistência.
  - [x] Expandir telemetria para falhas/tempo de limpeza, downloads cancelados e limpezas ignoradas por contenção; testar o DTO/API serializado com valores ativos e testar a contenção determinística do cleanup.
- [ ] **T3.6 — Fazer testes de falha e recuperação.** Rede lenta/interrompida, parte ausente, servidor reiniciado, cliente cancelado, mudança de caminho e disco cheio.
  - [x] Cobrir cancelamento do último leitor, cancelamento de um leitor com outros aguardando e rejeição/cancelamento de operações no descarte do cache.
  - [x] Cobrir cota cheia, reserva mínima, evicção inativa, redução de cota existente e limpeza manual durante fetch.
  - [x] Parte declarada mas indisponível/truncada agora gera `IOException` em vez de fim normal antecipado; cobrir leitores síncronos e assíncronos.
  - [x] Recriar o componente de cache sobre o mesmo diretório e validar reuso do chunk persistido sem nova chamada à origem; isso simula a recuperação do cache, não substitui teste de reinicialização do host completo.
  - [x] Simular `.partial` truncado após interrupção; validar que é contado no uso de disco, preservado durante lease ativo e removido quando o diretório da mídia expira.
  - [x] Trocar o cache no accessor com streams reais: stream aberta conclui no cache/lease original e stream criada após a troca lê e persiste no novo cache. Isso não substitui a verificação do endpoint de configuração num host em execução.

**Aceite:** a reprodução direta do disco permanece inalterada; com Nebula, o cache não impede o primeiro frame, não remove partes ativas e demonstra redução mensurável de pausas em cenários equivalentes.

### Fase 4 — Backups e recuperação de dados (P0)

- [x] **T4.1 — Inventariar dados recuperáveis.** Inventário estático de MongoDB, MariaDB/EF, usuários, configuração/segredos, índices, dados de operação, mídia, STRM e temporários; cobertura observada e lacunas em [inventario-dados-e-recuperacao.md](inventario-dados-e-recuperacao.md). Volumes/contagens reais permanecem sem inspeção e pertencem ao baseline T0.1/T4.5.
- [ ] **T4.2 — Garantir consistência do backup.** Definir snapshot/backup seguro para cada banco, criptografia, retenção, rotação, verificação de integridade e política de remoção remota.
  - [x] Resposta HTTP ao gravar histórico `nebula_backups` agora é validada; rejeição/erro faz o ciclo reportar falha em vez de declarar sucesso.
  - [x] Backup geral é montado em `.partial`, reaberto e lido integralmente antes de ser promovido atomicamente; nome final tem sufixo único para evitar colisão e truncamento por timestamp.
  - [x] Opção `Database=false` agora omite exportação do banco e otimização, grava lista de tabelas vazia no manifesto e foi exercitada pelo fluxo real de criação do ZIP.
  - [x] Corrigida a resolução do caminho de backup em Windows: preserva raiz da unidade/volume ao calcular espaço livre; antes, o caminho `C:\...` era reconstruído como `\C:...`, resultando em espaço `-1` e rejeição indevida do backup.
  - [ ] Pendente: snapshot consistente, criptografia, retenção/rotação, integridade e política de remoção remota.
  - [x] Backup/restauração exclusiva de usuários não informa sucesso com zero registros quando a origem relacional está ausente ou a tabela/consulta Supabase falha; erro fica explícito para painel/log.
  - [x] Falhas HTTP/JSON ao listar usuários remotos ou excluir registros antigos agora invalidam o backup. Lista local vazia continua protegida contra exclusão em massa.
  - [x] Rejeição HTTP no envio de usuários FTP do MongoDB agora encerra a sincronização com erro, sem informar sucesso com contagem zero; respostas 2xx e 503 cobertas em testes.
  - [x] Leituras HTTP ou respostas JSON inválidas de usuários FTP e tokens de bot durante restauração MongoDB agora invalidam a operação e propagam erro; sucesso com tabelas vazias segue válido.
- [ ] **T4.3 — Criar teste automático de restauração isolada.** Restaurar em diretório/instância temporária, validar contagens e consultas críticas e registrar duração/resultado.
  - [x] Teste opt-in de ciclo completo MariaDB em schema aleatório: backup, remoção do registro de fixture, restore e consulta do registro restaurado; usa apenas `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING` configurada explicitamente.
  - [ ] Expandir validação para MongoDB/arquivos e registrar duração/contagens do conjunto completo; a cobertura atual não substitui validação de recuperação remota.
  - [x] Exercitar `RestoreBackupAsync` com ZIP fixture e destinos temporários isolados; validar contagem de arquivos, conteúdo consultável de usuário/configuração, coleção e NFO, e duração finita.
  - [x] Criar/restaurar banco MariaDB descartável com schema aleatório, validar linha crítica e tipo recuperado; executar somente quando `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING` aponta explicitamente para uma instância de teste.
  - [x] Exercitar restauração relacional em banco EF InMemory exclusivo por teste; validar resultado, contagem, duração reportada, campos restaurados e consulta por ID. Isto não substitui teste com MariaDB temporário nem recuperação MongoDB.
  - [ ] Pendente: validar backup/restauração MongoDB e recuperação ponta a ponta de todos os conteúdos, incluindo arquivos e fontes remotas; complementar com contagens e RTO/RPO medidos.
- [ ] **T4.4 — Expor status acionável no painel.** Última execução, próxima execução, destino, conteúdo incluído, tamanho, validação, erros e ação de teste/restauração protegida.
  - [x] Parcial: painel web resume última execução/erro e exibe data, destino e opções do backup mais recente. Próximo disparo para triggers de calendário é calculado pelo servidor e enviado em UTC; agendas intervaladas/startup não recebem data inventada.
  - [x] Corrigido contrato da ação de validação: cliente agora usa `GET /System/Backup/Validate?path=...`, conforme endpoint, e oferece resultado válido/inválido/erro no diálogo do backup.
  - [x] Manifesto da API informa o tamanho real do ZIP em bytes; painel formata o tamanho e permanece compatível com versões do servidor que ainda não enviam esse campo.
  - [x] Validação percorre as entradas em streaming e compara CRC-32 e tamanho descompactado; payload corrompido é rejeitado mesmo quando o manifesto segue legível.
  - [x] A restauração existente mantém confirmação explícita na web e controller protegido por `RequiresElevation`; isso não substitui teste de restauração isolado.
  - [x] A checagem de espaço do backup agora resolve caminho absoluto corretamente em Windows e Unix; teste usa diretório temporário e confirma volume/espaço identificados.
  - [x] Parcial: exercício MariaDB isolado e verificação de conteúdo/relatório local comprovados; validação CRC continua distinta de restore.
  - [ ] Pendente: apresentar no painel escopo/cobertura consolidada de todos os destinos e o estado do último exercício completo incluindo MongoDB/arquivos.
- [ ] **T4.5 — Testar recuperação completa.** Documentar RPO/RTO realistas e exercício de recuperação em ambiente Windows e Linux.

**Aceite:** backup com status “sucesso” só após integridade validada; restauração de teste passa sem tocar nos dados de produção; falhas e backups vencidos geram alerta.

### Fase 5 — Banco, consultas e cache de aplicação (P1)

- [ ] **T5.1 — Medir consultas caras e pool de conexões.** Baseline de consultas lentas, espera por conexão, lock, índice usado e custo de varredura.
- [ ] **T5.2 — Otimizar consultas por evidência.** Paginação, projeção mínima, evitar materialização precoce, índices validados com plano de execução e limites para consultas grandes.
- [ ] **T5.3 — Revisar consistência entre MongoDB/MariaDB.** Documentar fonte de verdade por entidade, sincronização, reconciliação e comportamento diante de falha parcial.
- [ ] **T5.4 — Avaliar HybridCache para metadados.** Aplicar somente a leituras repetidas e seguras; definir chave que inclui identidade/idioma/permissão, TTL e invalidação. Não cachear respostas personalizadas como públicas.
- [ ] **T5.5 — Avaliar Change Streams somente se cabível.** Verificar a topologia MongoDB primeiro: Change Streams exigem replica set ou cluster fragmentado. Se a instalação permanecer standalone, manter polling/indexação incremental ou planejar a mudança operacional separadamente.
- [ ] **T5.6 — Testar carga e degradação.** Pool esgotado, conexão reiniciada, alto volume de consultas, índice ausente e migração/upgrade.

**Aceite:** melhoria demonstrada em benchmark e plano de consulta, sem regressão de consistência; cache invalida dados alterados; estratégia MongoDB compatível com a topologia realmente suportada.

### Fase 6 — APIs, segurança e configuração (P1)

- [ ] **T6.1 — Revisar autenticação/autorização por endpoint.** Principalmente solicitações, reports, uploads, operação Nebula, cache, backups e diagnósticos.
  - [x] Auditoria por leitura de código concluída nas áreas nomeadas: `UserFeedbackController` (solicitações/reports, auditado em etapa anterior desta sessão), `NebulaFtpController` (operação Nebula, `[Authorize(Policy = RequiresElevation)]` em todo o controller), `BackupController` (backups, elevação em todo o controller), `ServerHealthController` e `PlaybackReportsController`/`DashboardController` (diagnósticos, elevação em todo o controller ou por ação). Nenhuma rota exposta sem `[Authorize]` explícito ou herdado da política do controller foi encontrada nessas áreas.
  - [x] Verificado por amostragem ampla (~50 controllers, incluindo os herdados do Jellyfin) que todo endpoint que aceita `userId` como parâmetro opcional passa por `RequestHelpers.GetUserId(User, userId)`, que lança `SecurityException` quando um usuário não administrador tenta acessar dados de outro `userId` — inclusive as rotas legadas por rota (`Users/{userId}/PlayedItems/{itemId}`, `Users/{userId}/Items`) delegam para o mesmo método que já contém o guard, não o contornam. Comportamento já coberto por teste existente (`RequestHelpersTests.GetUserId_IsUser` espera `SecurityException`).
  - [x] Auditoria de endpoints de sessão/streaming (`SessionController`, `DynamicHlsController`, `MediaInfoController`) concluída: `SessionController` teve todos os métodos roteados verificados com `[Authorize]`; `DynamicHlsController` e `MediaInfoController` exigem `[Authorize]` no controller, herdado por todas as ações. A auditoria não encontrou `[AllowAnonymous]` nessas três superfícies.
  - [ ] Pendente: revisão individual de todas as ações elevadas fora das áreas auditadas e execução de fuzzing/teste de autorização cruzada em servidor real ainda não realizadas.
- [ ] **T6.2 — Aplicar rate limits e limites concorrentes seletivos.** Diferenciar login, busca, ações caras e operações de administração; retornar `429`/`Retry-After` sem degradar reprodução normal.
  - [x] `RateLimitMiddleware` já diferenciava tentativas de login (`/Users/Authenticate`, `/Users/Register`) de tráfego anônimo geral, isentando ativos estáticos, bootstrap público e loopback; agora as respostas `429` de ambos os caminhos também incluem o cabeçalho `Retry-After` (segundos até a entrada mais antiga sair da janela, arredondado para cima, nunca zero).
  - [x] Rate limits seletivos adicionados para rotas autenticadas e anônimas: busca (`/Search`, `/Items/RemoteSearch`) usa janela de 10 s/60 requisições por IP; administração (`/ActivityLog`, `/Backup`, `/Configuration`, `/Dashboard`, `/Environment`, `/Plugins`, `/ScheduledTasks`, `/ServerHealth`, `/System`) usa 10 s/20; Nebula (`/NebulaFtp`, incluindo operações de upload/download) usa 10 s/10. Cada categoria retorna `429` com `Retry-After`; loopback e bootstrap público continuam isentos.
  - [ ] Pendente: limites ainda são por IP e não há medição específica por usuário/worker; calibração com carga real e limites concorrentes por operação continuam pendentes.
- [ ] **T6.3 — Validar entrada e caminhos de arquivo.** Tamanho, tipo, canonicalização, traversal, symlinks, extensões e acesso por usuário.
  - [x] `LibraryStructureController.RenameVirtualFolder` agora canonicaliza os caminhos derivados dos nomes de biblioteca e rejeita com `400 Bad Request` nomes que escapem de `DefaultUserViewsPath` por `..`, sejam a própria raiz ou apenas compartilhem um prefixo textual com a raiz. Isso impede que uma operação administrativa de renomeação mova diretórios fora da área de views padrão.
  - [ ] Pendente: auditoria de symlinks/junctions, extensões/tipos de arquivo e demais endpoints que aceitam caminhos físicos ainda não concluída.
- [ ] **T6.4 — Revisar segredos, logs e transporte.** Proteção em repouso, rotação, permissões de arquivo e redação de credenciais; revisar dependências vulneráveis.
  - [x] Corrigida a resolução runtime vulnerável `Newtonsoft.Json 9.0.1` proveniente de FubarDev FTP e do MVC legado no plugin GetAvatar, com referência direta à versão central `13.0.3`; artefatos Release atuais auditados sem a versão afetada. Isso não conclui a auditoria geral de dependências, segredos ou transporte.
  - [x] Auditoria de redação de credenciais no Nebula: `NebulaFtpController.GetConfig()` (endpoint que a UI de administração usa para popular o formulário) já redigia `Password`, `HttpStreamToken`, `MongoDbConnectionString`, `ApiHash`, `SupabaseKey` e `BotTokens` via `CreateSafeConfigResponse`, e `NebulaFtpManager.GetBots()` já retornava só `MaskedToken` (últimos 4 caracteres) em vez do token completo — mas esse comportamento de leitura não tinha nenhum teste de regressão (só o caminho de escrita/preservação tinha). Auditoria de logs (`AddDownloaderLog`, `_logger.Log*` em `NebulaFtpManager`/`NebulaTelegramPool`) confirmou que nenhuma chamada embute o token/senha/URL com credenciais embutidas — usam apenas `ex.Message`, status HTTP ou identificadores não sensíveis.
  - [x] Permissões de arquivo em repouso: `BaseConfigurationManager.SaveConfiguration()`/`SaveConfiguration(key, config)` (usado por todos os arquivos de configuração, incluindo `nebulaftp.xml` com segredos em texto plano) agora restringe o arquivo recém-escrito a `UserRead|UserWrite` via `File.SetUnixFileMode` em plataformas não-Windows, dentro do mesmo `lock` que a serialização — eliminando a permissão padrão `644` (legível por qualquer conta local) da maioria das distribuições Linux. Falha ao restringir (ex.: sistema de arquivos sem suporte) é registrada como aviso e não interrompe o salvamento. Em Windows, o ACL NTFS padrão já restringe por conta/administrador e a chamada é ignorada (evita `PlatformNotSupportedException`).
  - [ ] Pendente: criptografia do conteúdo do arquivo de configuração (não só permissões do arquivo), rotação de segredos e revisão ampla de dependências fora do Newtonsoft.Json já corrigido.
- [ ] **T6.5 — Validar configurações seguras por padrão.** Rede local/remota, TLS, CORS, headers, contas administrativas e exposição de endpoints internos.
  - [x] `CorsPolicyProvider` (CORS restrito a same-origin quando `CorsHosts` vazio/curinga, sem `AllowCredentials`), TLS mínimo 1.2 (`WebHostBuilderExtensions`) e CSP restrita sem origens de terceiros amplas (`SecurityHeadersMiddleware`) — todos já corrigidos anteriormente (ver `SECURITY_AUDIT_REPORT.md`) — agora têm cobertura de teste automatizado real: `CorsPolicyProviderTests` (4 casos novos, cobrindo hosts vazios, curinga isolado e hosts explícitos) complementa `SecurityHeadersMiddlewareTests` já existente. Nenhuma dessas propriedades tinha teste de regressão antes desta etapa.
  - [x] Política de lockout de contas administrativas validada no fluxo real `UserManager.AuthenticateUser` → `UserAuthenticationService` → `IncrementInvalidLoginAttemptCount`: o contador incrementa em falhas, `IsDisabled` é persistido ao atingir `LoginAttemptsBeforeLockout`, novas tentativas são rejeitadas com `SecurityException` e login correto zera o contador. A correção de persistência usa `dbContext.Update(user)` para incluir a navegação `Permissions`; o teste anterior poderia passar pelo contador em memória sem provar o bloqueio persistido.
  - [x] Política de acesso anônimo restrito à LAN validada e corrigida: `AnonymousLanAccessHandler` agora trata explicitamente endereços loopback como locais antes de consultar `INetworkManager.IsInLocalNetwork`. `GetNormalizedRemoteIP()` normaliza IP ausente para loopback, mas a implementação anterior apenas comentava que loopback seria aceito e, com uma rede mock/configuração que não o classificasse, negava o acesso.
  - [x] Exposição de configuração de plugins corrigida: `DashboardController.GetDashboardConfigurationPage` agora exige `Policies.RequiresElevation`, impedindo que a rota que serve páginas/scripts de configuração — incluindo o recurso da integração Nebula — seja acessível sem elevação. `GetConfigurationPages` e os controllers de diagnóstico já tinham a mesma proteção.
  - [x] Binding do servidor de setup inicial agora é protegido por middleware que aceita apenas loopback ou endereços classificados como LAN por `INetworkManager`; requisições remotas recebem `401 Unauthorized` antes de alcançar health check, logger, informações públicas ou UI de setup. A mesma regra foi centralizada em `SetupServer.IsLocalNetworkRequest` e aplicada ao endpoint de logs e ao indicador visual da UI.
  - [x] Endpoints internos de observabilidade do servidor principal (`/metrics`, `/health`, `/ready`) agora exigem `Policies.LocalAccessOrRequiresElevation` via metadata de endpoint; antes eram mapeados sem autorização explícita. Isso mantém probes locais e usuários elevados funcionando, mas impede exposição remota/anônima de métricas e detalhes de saúde.
  - [ ] Pendente: revisão abrangente de outras superfícies de telemetria/diagnóstico fora desses endpoints e validação de integração com monitoramento remoto autenticado.

**Aceite:** testes de autorização impedem acesso cruzado entre usuários; abuso de endpoint não esgota worker/banco; logs e mensagens de erro não revelam segredos.

### Fase 7 — FFmpeg, transcodificação e suporte de hardware (P1)

- [ ] **T7.1 — Criar diagnóstico do FFmpeg/FFprobe.** Versão, codecs, filtros, acelerações compiladas, drivers, permissões e teste simples de execução.
- [ ] **T7.2 — Detectar capacidades por host.** Windows e Linux; D3D11VA/Media Foundation, Quick Sync e VAAPI conforme hardware/build disponível.
- [ ] **T7.3 — Implementar seleção com fallback.** Escolher modo suportado e voltar a software em falha, sem impedir Direct Play ou reprodução.
- [ ] **T7.4 — Limitar concorrência de transcodes.** Limite ajustável com fila e visibilidade no painel; teste de cancelamento e liberação de recursos.
- [ ] **T7.5 — Validar compatibilidade por codec/container.** Matriz real de mídia, legendas, busca/seek, áudio e reprodução remota.

**Aceite:** aceleração só aparece como ativa após teste real; fallback funciona; qualidade, sincronismo e seek preservados; CPU/GPU e número de transcodes visíveis.

### Fase 8 — Validação determinística do catálogo e provedores (P2)

- [ ] **T8.1 — Criar relatório determinístico de inconsistências.** Comparar nome de pasta/arquivo, NFO, título original, ano, tipo de mídia, ID do provedor, poster/backdrop e categoria.
- [ ] **T8.2 — Validar fontes por tipo de catálogo.** Para livros, testar busca e identificadores do Open Library (ISBN, edição e obra); para vídeo, comparar provedores compatíveis com o tipo. Armazenar ID/origem e respeitar limites/termos; não usar API de livros para varredura em massa.
- [ ] **T8.3 — Preservar proveniência e campos travados.** Registrar origem, data e confiança por campo; NFO local e IDs explícitos não devem ser silenciosamente substituídos.
- [ ] **T8.4 — Implementar modo de auditoria sem escrita.** Gerar candidatos e evidências para revisão; filtros por categoria, confiança e erro.
- [ ] **T8.5 — Aplicar correções determinísticas com segurança.** Exigir aprovação explícita, registrar estado anterior e permitir rollback de metadados, imagens e NFO.
- [ ] **T8.6 — Revisar direitos e termos de provedores.** Credenciais, limites, atribuição, uso permitido e armazenamento de imagens/dados.

**Aceite:** conjunto de teste rotulado mede precisão/recall; nenhuma alteração automática em massa; toda correção determinística exige aprovação, evidência, histórico e rollback. Esta fase não usa modelos de IA.

## Backlog do frontend web

### Fase W1 — Confiabilidade e fluxo de usuário (P0)

- [ ] **W1.1 — Padronizar estados de página.** Loading/skeleton, vazio, erro com retry, offline/degradado e sucesso em home, busca, detalhes, solicitações e telas do painel.
  - [x] Página de detalhes (`controllers/itemDetails`): quando o carregamento do item (ou do `SeriesTimer`) falha, a página ficava permanentemente em branco após o spinner — sem mensagem, sem ação. Agora exibe `#itemDetailLoadError` com `ErrorDefault` e botão "Retry" que rechama `loadData()`; o conteúdo principal (`detailPageWrapperContainer`, `detailLogo`) fica oculto enquanto o erro é mostrado e volta a aparecer em nova tentativa bem-sucedida ou ao reabrir a página.
  - [ ] Pendente: home, busca (parcialmente coberta em rotas React), solicitações e telas do painel — auditoria completa de todos os estados (skeleton, offline/degradado) ainda não realizada; apenas o "erro sem retry" da página de detalhes foi corrigido nesta etapa.
- [ ] **W1.2 — Cobrir solicitação de mídia de ponta a ponta.** Botão próximo a Favoritos quando autorizado; autocomplete com debounce/cancelamento; estados de indexação, resultados e inclusão existente; grids separados para fila pendente e títulos já incluídos; posição/prioridade e confirmação do envio.
- [ ] **W1.3 — Corrigir navegação de carrosséis e resultados.** Setas visíveis quando aplicáveis, estados disabled corretos, rolagem por teclado/controle remoto, foco e comportamento responsivo.
- [ ] **W1.4 — Tratar falhas de API sem tela vazia.** Erros de rede/servidor mostram contexto e ação possível; logs técnicos ficam no console/telemetria sem expor detalhes sensíveis ao usuário.
- [ ] **W1.5 — Consolidar componentes e tokens de UI.** Harmonizar controles React/MUI e componentes Jellyfin legados sem reescrita global; priorizar cabeçalho, botões, grids, diálogos, alertas e estados.

**Aceite:** Playwright confirma todos os estados e caminhos; não há página de solicitação/grid permanentemente vazia quando API responde com dados ou erro; ações podem ser concluídas com mouse, teclado e layout estreito.

### Fase W2 — Acessibilidade e navegação em dispositivos (P1)

- [ ] **W2.1 — Auditoria WCAG 2.2 AA.** Contraste, foco, semântica, labels, mensagens de status, zoom/reflow, orientação e alvos de toque.
- [ ] **W2.2 — Navegação por teclado e controle remoto.** Ordem de foco, setas de carrossel, escape de diálogos, retorno de foco e atalhos sem armadilhas.
- [ ] **W2.3 — Automatizar axe com Playwright.** Rodar nas páginas principais; complementar com avaliação manual por teclado/leitor de tela.
- [ ] **W2.4 — Melhorar formulários e erros.** Label visível, validação junto ao campo, sugestão para corrigir e anúncio acessível de sucesso/falha.

**Aceite:** zero violações críticas/altas automatizáveis nas páginas alvo; checklist manual teclado/controle remoto; nenhum componente interativo sem nome/foco acessível.

### Fase W3 — Desempenho percebido (P1)

- [ ] **W3.1 — Medir Web Vitals por rota.** LCP, INP, CLS e TTFB, com dados de laboratório e medição real opt-in/anônima se aprovada.
- [ ] **W3.2 — Definir budgets de bundle e recursos.** Inspecionar chunks e dependências grandes; carregar rotas administrativas, PDF/EPUB e ferramentas pesadas sob demanda.
- [ ] **W3.3 — Otimizar posters/backdrops.** Dimensões adequadas, lazy loading fora do viewport, prioridade para imagem principal, placeholders BlurHash e reserva de espaço.
- [ ] **W3.4 — Ajustar cache de dados TanStack Query.** `staleTime`, chaves, invalidação, cancelamento e retry conforme semântica; evitar refetches duplicados e respostas de busca fora de ordem.
- [ ] **W3.5 — Avaliar virtualização.** Só adotar em grids/listas após benchmark demonstrar custo; preservar navegação por teclado, acessibilidade e medição de rolagem.
- [ ] **W3.6 — Testar redes e dispositivos lentos.** Throttling, CPU lenta, telas pequenas, TV e navegação por controle remoto.

**Aceite:** metas por rota são documentadas; nenhum chunk inicial acima do orçamento aprovado; melhoria comprovada em dispositivos/rede de referência sem regressão de imagem ou navegação.

### Fase W4 — Testes visuais e prevenção de regressão (P1)

- [ ] **W4.1 — Completar cobertura Playwright dos fluxos críticos.** Busca, detalhes, solicitações, reprodução, perfil e painel de gestão.
- [ ] **W4.2 — Adicionar snapshots visuais estáveis.** Viewports celular, desktop e TV; baseline revisto por pessoa; ambiente de navegador fixado.
- [ ] **W4.3 — Validar interações e paginação.** Setas, scroll, foco, estados de carregamento e carregamento de mais resultados.
- [ ] **W4.4 — Integrar quality gate do frontend.** `npm run build:check`, `npm test`, ESLint/Stylelint, `npm run build:production`, verificação do artefato e Playwright relevante.
  - [x] Parcial: a rota de operações Nebula agora passa `npm run lint:changed`, `npm run build:check`, `npm test -- --run` (208 testes) e `npm run build:production`; falta cobertura Playwright/renderizada, confirmação visual responsiva e validar o artefato integrado ao servidor.
  - [x] Isolar os testes de `viewContainer` do módulo global `Dashboard` não usado nesses cenários e aguardar as Promises de `loadView`, evitando callbacks do polyfill após o teardown do `jsdom`.
- [ ] **W4.5 — Definir política para flaky tests.** Diagnóstico com trace/screenshot; nenhuma instabilidade escondida por retries ilimitados.

**Aceite:** rotas críticas têm teste funcional e visual; regressão de layout/ausência de grids é detectada antes da release; pipeline registra artefatos de falha.

## Ordem e dependências

1. **T0 baseline** precede qualquer decisão sobre performance, limites ou troca de tecnologia.
2. **T1 observabilidade** habilita diagnóstico e verificação de T2–T8 e W3.
3. **T2 fila, T3 reprodução e T4 backup** são frentes de confiabilidade prioritárias; cada uma deve ter teste isolado e cenário de falha.
4. **W1 estados e fluxos** pode começar após contratos mínimos de API/estados serem acordados; não depende de nova tecnologia.
5. **W2 e W4** acompanham cada melhoria visual, não ficam apenas para o fim.
6. **T5–T7 e W3** seguem o baseline e precisam demonstrar ganho mensurável.
7. **T8 catálogo determinístico** depende de proveniência e auditoria sem escrita; curadoria por IA não faz parte desta execução.

## Quality gate para cada tarefa executada

### Servidor

- Testes .NET focados no componente alterado e regressão relacionada; ampliar a suíte conforme o alcance.
- Build e pacote de produção para Windows e Linux quando aplicável.
- Testes de integração/falha para bancos, Telegram, fila e reprodução conforme o fluxo modificado.
- Critérios Gauntlet do repositório: evidência de terminal e código de saída zero; qualquer falha deve ser corrigida e reexecutada.

### Frontend web

- `npm run build:check`
- `npm test`
- `npm run lint` e `npm run stylelint` nos arquivos aplicáveis
- `npm run build:production` e `npm run verify:build`
- `npm run test:playwright:user`, `npm run test:playwright:admin` ou `npm run test:playwright:full` conforme as rotas alteradas
- Verificação visual e manual responsiva/acessível para mudanças de interface

### Definition of Done e release

- [ ] Diff revisado; testes relevantes e Quality Bar aprovados em configuração de produção.
- [ ] Notas de release descrevem apenas mudanças realmente incluídas e validadas.
- [ ] Só iniciar a release depois que **todas as melhorias ativas deste roadmap** estiverem implementadas e validadas; não publicar releases intermediárias deste ciclo.
- [ ] Na release final, incluir assets de produção do servidor para Windows (ZIP + EXE) e Linux quando a versão/build for aplicável.
- [ ] Atualizar e publicar o portal com a mesma versão/notas e validar a versão pública.
- [ ] Consultar API de releases e confirmar assets/versão publicados.

Este documento é backlog em execução; não autoriza publicar uma release antes do gate acima.

## Registro de execução

### 28/09/2026 — Primeiro span correlacionável do listener Nebula (T1.1 parcial)

- `NebulaHttpStreamServer` agora emite `ActivityKind.Server` pela fonte `MulletaFlix.Nebula.HttpStreamServer`; aceita contexto W3C recebido e limita tags a método HTTP normalizado, rota fixa, status e tipo de exceção. Caminhos, query string e credenciais não entram no span.
- As métricas Prometheus existentes continuam ativas. Não foi adicionada dependência nem exportador OpenTelemetry; sem listener/exportador registrado, a fonte não coleta nem persiste spans. O restante da correlação (ASP.NET, bancos, scanner, Telegram, upload/download e sessão de reprodução) segue pendente.
- Teste focado `NebulaStreamEngineTests`: 22 aprovados; suíte completa `Jellyfin.Server.Implementations.Tests`: 979 aprovados, 38 ignorados, 0 falhas. `dotnet build Jellyfin.Server/Jellyfin.Server.csproj -c Release --no-restore`: exit 0, 0 erros; `git diff --check`: exit 0. Avisos conhecidos incluem NU1903 para `Newtonsoft.Json` 9.0.1 e avisos de analisadores.
- Nenhum serviço instalado foi reiniciado. Nenhuma release foi criada ou publicada; todas as melhorias ativas ainda precisam ser concluídas antes do release único.

### 28/09/2026 — Exportação OTLP opt-in e instrumentação HTTP (T1.1 parcial)

- Adicionadas dependências OpenTelemetry para hosting, protocolo OTLP e instrumentação ASP.NET Core. Exportação ativa somente quando `OTEL_EXPORTER_OTLP_ENDPOINT`, `OTEL_EXPORTER_OTLP_TRACES_ENDPOINT` ou `OTEL_EXPORTER_OTLP_METRICS_ENDPOINT` estiver configurada; nenhuma tentativa de conexão local implícita. O guia documenta gRPC e HTTP/protobuf.
- Traces incluem instrumentação HTTP ASP.NET Core e spans do listener Nebula; métricas incluem requisições ASP.NET Core. O endpoint Prometheus existente permanece independente. Tags com caminho, query, URL completa e user-agent são removidas; `tracestate` não é propagado pelo listener Nebula.
- Testes `MulletaFlixOpenTelemetryExtensionsTests`: 7 aprovados; suíte completa `Jellyfin.Server.Tests`: 29 aprovados, 0 falhas; suíte completa `Jellyfin.Server.Implementations.Tests`: 979 aprovados, 38 ignorados, 0 falhas; build Release de `Jellyfin.Server`: exit 0, 0 erros; `git diff --check`: exit 0. Persistem avisos NU1903 para `Newtonsoft.Json` 9.0.1.
- Limite: nenhum collector OTLP foi conectado, então exportação de rede não foi verificada em runtime. Instrumentação de banco, varreduras, Telegram, upload/download e sessão de reprodução continua pendente; T1.1 permanece aberta. Nenhum pacote/release foi criado ou publicado.

### 28/09/2026 — Tracing e métricas do ciclo de upload Nebula (T1.1 parcial)

- `NebulaUploadEngine` emite span interno ao redor do ciclo completo do worker. Registra apenas operação, resultado e tipo de erro; exceções de cancelamento são classificadas como canceladas sem status de erro.
- Adicionadas métricas OTEL de contagem e duração de uploads com atributo único de resultado (`success`, `failure` ou `cancelled`). A instrumentação não inclui nome/caminho, pasta, ID Telegram ou ID MongoDB; métrica habilitada junto à configuração OTLP de métricas.
- Teste focado `UploadEngine_EmitsActivityWithoutMediaIdentifiers`: 1 aprovado, validando span, métrica e ausência de identificadores. Suíte completa `Jellyfin.Server.Implementations.Tests`: 980 aprovados, 38 ignorados, 0 falhas; `Jellyfin.Server.Tests`: 29 aprovados, 0 falhas; build Release do servidor: exit 0, 0 erros; `git diff --check`: exit 0. Permanecem avisos NU1903 para `Newtonsoft.Json` 9.0.1 e analisadores existentes.
- Limite: nenhum collector OTLP foi conectado. Teste verifica emissão local via `ActivityListener`/`MeterListener`, não entrega pela rede. Spans detalhados de Telegram, MongoDB, MariaDB, scanner, download e sessão de reprodução seguem pendentes. T1.1 segue aberta; nenhuma release foi criada/publicada.

### 28/09/2026 — Tracing e métricas dos downloads Nebula (T1.1 parcial)

- `NebulaDownloaderEngine.DownloadMultipartAsync` emite span e métricas para transferências de STRM e arquivos físicos. Resultado/duração usam somente atributo de resultado com baixa cardinalidade; URL assinada, query, caminho, nome e IDs não entram na telemetria.
- Cancelamentos só recebem resultado `cancelled` quando o token do chamador foi cancelado. Timeout do `HttpClient` permanece falha, com status de erro no span.
- Teste focado `Downloader_EmitsActivityAndMetricsWithoutUrlOrPath`: 1 aprovado; suíte completa `Jellyfin.Server.Implementations.Tests`: 981 aprovados, 38 ignorados, 0 falhas; `Jellyfin.Server.Tests`: 29 aprovados, 0 falhas; build Release do servidor e `git diff --check`: exit 0. Persistem avisos NU1903 para `Newtonsoft.Json` 9.0.1 e avisos de analisadores.
- Sem collector ativo, exportação de rede não foi verificada. Nenhuma release foi criada ou publicada.

### 28/09/2026 — Baseline inicial da instância Windows (T0.1 parcial)

- Instância observada por leitura local: MulletaFlix `12.1.5`, executável instalado em `C:\Program Files\MulletaFlix\Server\MulletaFlix.exe`; Windows 11 Pro `10.0.26200`, 12 processadores lógicos, 15,7 GiB RAM. Processo iniciou às 10:16:04; log registra `Startup complete` às 10:16:13.699 (-03:00), estimativa de startup de ~9,7 s (um único boot).
- Medição de 20 GETs sequenciais locais, todos HTTP 200: `/health` p50 10,88 ms / p95 33,92 ms; `/ready` 10,12 / 19,59 ms; `/System/Info/Public` 1,66 / 8,53 ms; `/web/` 2,02 / 26,63 ms. Amostra aquecida, localhost e sem concorrência; não representa picos de uso.
- Em 10 amostras a cada 2 s, working set variou de 2,07 a 3,21 GiB; CPU média do processo foi 3,27% dos 12 processadores no intervalo. O working set é memória física mapeada ao processo, não heap gerenciado; metodologia segue [Get-Process](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.management/get-process?view=powershell-7.5). `dotnet-counters` não está disponível no host, então heap/GC não foram medidos.
- Volume C: tem 80,73 GiB livres de aproximadamente 476 GiB. Isso é espaço livre do volume, não footprint de cache/dados MulletaFlix.
- `/metrics` respondeu 200 (408.052 bytes), mas não expôs as séries `nebula_http_requests_total`, `nebula_http_request_duration_seconds` nem `nebula_http_saturated_requests_total` listadas no runbook. A instrumentação dessas séries é acionada por requisições ao listener HTTP Nebula; como não foi gerado tráfego por esse listener durante a amostra, a ausência no scrape não prova que a instrumentação esteja ausente. Duração de tarefas e volume processado não foram coletados: `/NebulaFtp/Status` exige autenticação administrativa, que não foi usada; nenhuma credencial foi lida.
- Nenhuma configuração foi alterada, nenhum serviço foi reiniciado e nenhuma carga sintética foi gerada além dos GETs de medição. T0.1 permanece parcial: faltam repetição sob carga, Linux, footprint local isolado, latências de endpoints autenticados e métricas de tarefas/volume Nebula.

### 28/09/2026 — Baseline do artefato web (T0.4 parcial)

- Build de produção atualizado via `npm run build:production`; verificação `npm run build:check`, suíte Vitest e `npm run verify:build` passaram. Vitest: 26 arquivos e 208 testes aprovados. Build reportou avisos conhecidos do Rollup sobre diretiva `use client` ignorada; não houve erro e o verificador passou.
- Artefato `MulletaFlix-web-master/dist`: 1.895 arquivos, 63.345.873 bytes (60,41 MiB); 677 arquivos JS/CSS em `assets/`. Maior chunk: `vendor-jellyfin-Bb1keG9m.js`, 435,3 KiB. Gate atual rejeita arquivo JS/CSS em `assets/` acima de 1.536 KiB; não mede soma transferida comprimida nem custo real de execução.
- A saída foi regenerada em `MulletaFlix-web-master/dist`, pois o Vite resolve `build.outDir` relativo ao `root: src`; a raiz `dist` (137 arquivos, 13.360.505.353 bytes, incluindo 28 instaladores Windows) permaneceu inalterada. Nenhum arquivo dessa pasta foi removido.
- T0.4 permanece parcial: uma compilação não estabelece tendência nem margem segura de regressão; faltam medições repetidas de startup, busca, tráfego comprimido, memória/disco e cache sob carga representativa.

### 28/09/2026 — Medição reproduzível do tráfego web (T0.4 parcial)

- Adicionado `MulletaFlix-web-master/scripts/report-bundle-transfer.mjs`. Executar de qualquer diretório com `node MulletaFlix-web-master/scripts/report-bundle-transfer.mjs` para medir HTML, os assets JS/CSS referenciados por `index.html` e todos os JS/CSS em `dist/assets`; usa gzip nível 6 e Brotli qualidade 5, sem dependência nova.
- No artefato atual: HTML + 8 assets iniciais = 1.205.717 bytes brutos, 281.927 bytes gzip simulado e 259.288 bytes Brotli simulado. Os 677 arquivos JS/CSS somam 20.622.168 bytes brutos, 6.299.664 gzip e 5.781.805 Brotli.
- Os bytes comprimidos são estimativas por arquivo, não respostas HTTP observadas: negociação, cabeçalhos, compressão do servidor, cache do navegador e assets carregados sob demanda não foram medidos. Não usar o total de todos os chunks como tráfego de uma sessão.
- Verificação daquela execução: `node scripts/report-bundle-transfer.mjs`, `npm run build:check`, `npm test -- --reporter=dot`, `npm run verify:build` e ESLint do script saíram com código 0; 26 arquivos/208 testes passaram, mas a suíte ainda imprimiu `ReferenceError: window is not defined` após o resumo. Essa falha tardia foi corrigida na execução registrada em “Teardown limpo nos testes de `viewContainer`”. `git diff --check` também passou.
- T0.4 permanece parcial: faltam medições repetidas em runtime, busca, compressão HTTP real, recursos/disco e cache sob carga representativa; nenhuma budget global foi definida.

### 28/09/2026 — Teardown limpo nos testes de `viewContainer` (W4.4 parcial)

- A reprodução isolada confirmou `ReferenceError: window is not defined` depois dos testes e exit code 0. A causa era importar a árvore global `Dashboard` em testes que não exercitam esse módulo; isso carregava o polyfill `webcomponents-lite`, cujo `MutationObserver` ainda recebia mutações durante o teardown do `jsdom`.
- Os testes agora mockam somente `Dashboard.getPluginUrl` (não usado nesses casos) e aguardam a Promise de `loadView`. Não há alteração de produção nem supressão de erro; os cenários continuam validando script inline, resolução de URLs e preservação de script/stylesheet externos.
- Verificação após correção: teste isolado `viewContainer.test.ts` — 3/3; suíte Vitest — 26 arquivos/208 testes e exit 0, sem o `ReferenceError`; `npm run build:check`, ESLint dos arquivos alterados, `npm run build:production` e `npm run verify:build` — exit 0; 1.895 artefatos aprovados. Permanecem avisos do Rollup sobre diretivas `use client`, falhas de fetch esperadas em testes jsdom e aviso de `getComputedStyle` não implementado.
- Busca por chamadas não aguardadas de `viewContainer.loadView` encontrou zero outros casos; o consumidor de produção encadeia a Promise. `git diff --check` passou. W4.4 continua aberta para Stylelint/Playwright e integração completa do quality gate.

### 28/09/2026 — Cache compartilhado Nebula (T3.1 parcial, T3.3 parcial, T3.4 concluída, T3.5 parcial, T3.6 parcial)

- Corrigido o cancelamento por leitor: cada requisição pode cancelar sua própria espera sem interromper leitores restantes; o fetch compartilhado é cancelado quando o último leitor sai.
- O descarte do cache cancela fetches em andamento e chamadas novas passam a ser rejeitadas; entradas concorrentes são removidas somente se ainda corresponderem à mesma operação.
- Alinhada expiração de inatividade para 1 hora; a limpeza periódica permanece em 5 minutos e preserva leases ativos.
- Adicionadas cota máxima e reserva configuráveis no painel; o cache evicta diretórios inativos por LRU, respeita mídias/leitores/fetches ativos e não falha a reprodução quando não pode gravar.
- A limpeza periódica aplica também redução de cota já configurada; a limpeza manual ignora operações de chunk em andamento. Alterar apenas os limites atualiza o cache existente sem interromper a sessão; mudar caminho aguarda o cache anterior ficar ocioso antes de descartá-lo.
- O painel agora corresponde ao DTO real e exibe ocupação da cota, limite, espaço livre/reserva e leases. Ainda faltam hits/misses, latência Telegram, prefetch em andamento e erros.
- Testes cobrem cota cheia, reserva mínima, evicção inativa, redução de cota existente, limpeza manual durante fetch, cancelamento independente/total e descarte.
- Corrigidas corridas de cancelamento/descarte de fetches compartilhados e prefetch; leases, início de fetch, limpeza e evicção agora são sincronizados para não remover conteúdo que acabou de entrar em uso. Reduzir a cota aplica a evicção imediatamente.
- A troca do caminho do cache atualiza também a referência compartilhada usada por novas streams FTP/HTTP, enquanto leases existentes continuam no cache anterior até terminarem.
- A API genérica preserva caminho/cota/reserva, devolve esses campos no GET e valida limites; caminho omitido mantém o atual e caminho vazio volta ao padrão. Limpeza alcança arquivos persistidos mesmo sem cache inicializado e informa falha real de remoção.
- A tela do cache trata as respostas JSON corretamente, diferencia caminho omitido/vazio e mostra o resultado efetivo da limpeza, evitando sucesso visual sem a operação correspondente.
- Verificação Release: 992 testes no projeto `Jellyfin.Server.Implementations.Tests` (954 aprovados, 38 ignorados, 0 falhas); API: 178 aprovados, 0 falhas; web: 208 testes aprovados. `dotnet build Jellyfin.Server/Jellyfin.Server.csproj -c Release --no-restore`, `npm run build:check`, `npm run lint:changed`, `npm run build:production` e `npm run verify:build` concluídos com exit 0. Persistem aviso de advisory de alta severidade em `Newtonsoft.Json`, avisos de bundle/analisadores e mensagem jsdom `window is not defined` após resumo dos testes, sem falha no processo.
- Risco a medir antes de otimização: gravações de cache ainda são serializadas pelo gate global para proteger cota/reserva/evicção. Benchmark concorrente está pendente em T3.2; não paralelizar sem preservar as invariantes e demonstrar ganho.
- T3.1 permanece aberta porque a chave canônica e alguns cenários de falha ainda precisam de revisão; T3.3/T3.5/T3.6 também parciais conforme itens não marcados.
- Nenhuma release foi criada/publicada; aguardar conclusão integral das melhorias ativas conforme decisão do usuário.

### 28/09/2026 — Falha explícita para parte ausente no stream (T3.6 parcial)

- Uma mídia cuja parte anunciada no catálogo retorna zero bytes ou termina antes do tamanho informado não deve parecer concluída. O stream agora lança `IOException` tanto na leitura síncrona quanto assíncrona, inclusive se o trecho truncado já estiver no cache em memória.
- Testes cobrem parte vazia em ambas as APIs de leitura e parte truncada na leitura síncrona; `NebulaPlaybackCacheTests`: 39/39 aprovados em Release. A primeira execução identificou a variante síncrona já mantida no cache em memória; a verificação foi aplicada também a esse caminho.
- Suíte completa Implementations: 1.016 aprovados, 38 ignorados, 0 falhas; build Release do servidor e `git diff --check` passaram. Avisos existentes: advisory NU1903 de Newtonsoft.Json 9.0.1 e avisos StyleCop/xUnit não relacionados.
- T3.6 segue parcial: faltam restart real, caminho alterado com readers ativos e cenários integrados de rede/disco. Nenhuma release foi criada/publicada por decisão do usuário.

### 28/09/2026 — Reuso do cache após recriação (T3.6 parcial)

- Teste recria `NebulaPlaybackCache` apontando para o mesmo diretório após descarte da instância anterior; o chunk correto é servido do disco, sem segundo fetch, e os contadores da nova instância indicam cache hit.
- O teste é isolado e não conecta a MongoDB/Telegram nem acessa mídia do usuário; valida persistência do componente, não uma reinicialização end-to-end do processo Jellyfin.
- Quality gate: teste isolado 1/1; suíte completa `Jellyfin.Server.Implementations.Tests` — 1.017 aprovados, 38 ignorados, 0 falhas; `dotnet build Jellyfin.Server/Jellyfin.Server.csproj -c Release --no-restore` e `git diff --check` passaram. Permanecem avisos NU1903 de Newtonsoft.Json 9.0.1.
- T3.6 e T2.3 continuam parciais; nenhum teste usou MongoDB/Telegram reais, nenhuma mídia de produção foi acessada e nenhuma release intermediária foi criada.

### 28/09/2026 — Recuperação de gravação parcial do cache (T3.6 parcial)

- Teste simula interrupção com um chunk `.partial` de 2 bytes para tamanho esperado de 4; ao recriar o cache, ele ignora o incompleto, busca novamente e substitui atomicamente pelos bytes completos.
- Outra regressão comprova que `.partial` conta no espaço/arquivos, não é removido enquanto existe lease da mídia e é eliminado junto à pasta após expiração ociosa.
- Confirmado no código: a limpeza já enumera todos os arquivos da pasta e o download remove seu temporário em `finally`; não foi necessária mudança de produção para esse caso.
- Quality gate Release: cache Nebula — 42/42; suíte completa `Jellyfin.Server.Implementations.Tests` — 1.019 aprovados, 38 ignorados, 0 falhas; build Release do servidor e `git diff --check` passaram. Persistem avisos NU1903 existentes de Newtonsoft.Json 9.0.1.
- T3.6 segue parcial: sem reinício end-to-end do host, mudança de diretório com leitor ativo e carga real de rede/disco. T2.3 também permanece parcial. Nenhuma release criada/publicada.

### 28/09/2026 — Troca de caminho com playback ativo (T3.6 parcial)

- O teste anterior exercitava somente leases obtidos diretamente do accessor; foi ampliado para abrir streams reais e bloquear a leitura da stream antiga durante a troca.
- Após a troca, uma stream nova lê e grava no novo cache; a stream já aberta termina com os bytes corretos no cache original, mantendo o lease antigo válido durante toda a leitura.
- Quality gate Release: teste específico 1/1; suíte completa `Jellyfin.Server.Implementations.Tests` — 1.019 aprovados, 38 ignorados, 0 falhas; build Release do servidor e `git diff --check` passaram. NU1903 existente para Newtonsoft.Json 9.0.1 permanece.
- Cobertura local do accessor concluída; T3.6 permanece parcial por faltar troca via endpoint em host rodando, reinício end-to-end e carga real. Nenhuma release publicada.

### 28/09/2026 — Limites e persistência do prefetch Nebula (T3.2 parcial)

- O pré-cache integral admite duas mídias ativas e quatro aguardando; além desse limite, a reprodução continua por demanda sem criar mais tarefas de pré-cache.
- O prefetch-ahead é especulativo: no máximo dois chunks concorrentes; quando os slots estão ocupados, a especulação é descartada e o leitor baixa o chunk se/ quando requisitado.
- Os chunks antecipados passam pelo cache persistente compartilhado, permitindo coalescência entre leitores/ranges e evitando downloads duplicados.
- Testes verificam limite ativo e fila de pré-cache, reutilização do mesmo chunk entre leitores, persistência e que a leitura demandada não espera pelo semáforo de prefetch-ahead.
- Verificação Release: suíte completa `Jellyfin.Server.Implementations.Tests` com 961 aprovados, 38 ignorados, 0 falhas; build do servidor com 0 erros; `git diff --check` concluído. Permanecem avisos NU1903/analisadores existentes.
- T3.2 segue parcial: falta teste integrado que prove primeiro bloco antes do prefetch restante e sinal explícito/seguro de encerramento/troca da sessão para cancelar a mídia anterior. O cancelamento atual por leases mantém janela de tolerância de 2 minutos para streams HTTP de ranges.
- Nenhum servidor instalado foi reiniciado e nenhuma release/portal foi publicada.

### 28/09/2026 — Primeiro bloco durante pré-cache Nebula (T3.2 parcial)

- Adicionado um seam interno de fetch no `NebulaChunkedStream`, sem alterar o construtor público usado em produção, para exercitar de forma determinística concorrência do prefetch integral e leitura sob demanda.
- A revisão adversarial identificou uma quebra de assinatura pública preexistente no diff acumulado; a assinatura anterior com `NebulaPlaybackCache` foi restaurada e o `NebulaPlaybackCacheAccessor` ficou restrito às sobrecargas internas.
- Novo teste comprova que o primeiro chunk é devolvido e escrito no cache local enquanto o fetch do chunk seguinte permanece bloqueado; o teste libera o fetch ao final e espera o pré-cache terminar.
- Teste focado Release: 1 aprovado, 0 falhas. Suíte completa `Jellyfin.Server.Implementations.Tests`: 962 aprovados, 38 ignorados, 0 falhas. Build Release do servidor: 0 erros; permanecem avisos conhecidos de dependência/analisadores. `git diff --check` passou.
- T3.2 continua parcial: falta validação real da sequência de reprodução e vínculo Jellyfin → Mongo → cache em runtime. Nenhuma release será criada até todas as melhorias ativas do roadmap serem implementadas e validadas.

### 28/09/2026 — Cancelamento do pré-cache pelo ciclo de sessão (T3.2 parcial)

- Registrado `NebulaPlaybackSessionMonitor` como hosted service, escutando os eventos reais do `ISessionManager`. Ele acompanha mídia e `PlaySessionId` por sessão, solicita cancelamento ao trocar/parar e protege sessões concorrentes que ainda usam o mesmo caminho.
- O caminho STRM é resolvido novamente no Mongo para obter a mesma chave de mídia usada pelo cache; se houver range aberto, a intenção de cancelar aguarda o último lease, e um novo prefetch da mesma mídia limpa essa intenção.
- Testes Release cobrem compartilhamento de mídia entre sessões, troca de título, parada atrasada ou sem identidade, cancelamento isolado e adiado, retomada durante a resolução, lease ativo e execução de callbacks fora do lock global.
- Suíte `Jellyfin.Server.Implementations.Tests`: 970 aprovados, 38 ignorados, 0 falhas; suíte `Jellyfin.Api.Tests`: 178 aprovados, 0 falhas; build Release do servidor: 0 erros. `git diff --check` passou. Permanecem avisos NU1903/analisadores conhecidos.
- Preservada compatibilidade pública: assinatura original do construtor HTTP restaurada e novo método de cancelamento da interface recebe implementação padrão compatível.
- A validação real da sequência Jellyfin → Mongo → cache durante reprodução ainda falta; nenhuma release foi criada/publicada.

### 28/09/2026 — Cancelamento assíncrono do pré-cache (T3.2/T3.3 parcial)

- Revisão adversarial encontrou a janela entre marcar cancelamento e sinalizar o token; `Acquire` agora revoga pedidos ainda pendentes, enquanto um cancelamento já sinalizado permanece irrevogável.
- Callbacks de cancelamento agora são disparados por `CancellationTokenSource.CancelAsync`; o descarte da origem é adiado até os callbacks concluírem e não bloqueia sob o lock do estado.
- Novo teste segura um callback de token e confirma que a chamada `CancelPrefetch` retorna antes do callback liberar; depois da liberação, o estado pendente termina normalmente.
- Verificação Release: 5 testes focados `CancelPrefetch` aprovados; suíte completa `Jellyfin.Server.Implementations.Tests` com 971 aprovados, 38 ignorados, 0 falhas; `Jellyfin.Api.Tests` com 178 aprovados, 0 falhas; build do servidor com 0 erros; `git diff --check` passou. Persistem avisos NU1903/analisadores existentes.
- A revisão adversarial encontrou aviso duplicado quando callback lançava; o registro foi centralizado no descarte e os testes focados passaram novamente. A validação runtime Jellyfin → Mongo → cache continua pendente; nenhum servidor instalado foi reiniciado.
- Nenhuma release ou publicação do portal foi criada.

### 28/09/2026 — Diagnóstico do pré-cache Nebula (T3.5 parcial)

- Acrescentados contadores atômicos de hit/miss, fetches/falhas, duração média do fetch e erro de persistência, sem logging por chunk ou consulta extra no caminho de reprodução.
- O endpoint `NebulaFtp/PlaybackCache` expõe os contadores acumulados desde a inicialização; o painel mostra também pré-cache integral ativo/em fila. Estados de pré-cache substituídos durante cancelamento continuam no rastreador até terminar, para não subcontar trabalho ativo ou fila.
- Cancelamentos do fetch compartilhado também passaram a usar `CancelAsync`, com liberação do token postergada até callbacks concluírem; teste garante que `Dispose` retorna mesmo com callback deliberadamente bloqueado. Teste da fila verifica 2 ativos/4 aguardando.
- Teste cobre hit após download e erro do fetch. Suíte completa de implementações: 973 aprovados, 38 ignorados, 0 falhas; API: 178 aprovados; testes do painel web: 208 aprovados; TypeScript, lint, build web de produção e verificador de artefatos passaram; build Release do servidor: 0 erros. Avisos NU1903 e mensagens conhecidas do Vite/jsdom permanecem.
- T3.5 permanece parcial: ainda medir falha/duração da limpeza e separar cancelamentos esperados de falhas. A validação runtime Jellyfin → Mongo → cache segue aberta; nenhuma release/portal foi publicada por decisão explícita do usuário.

### 28/09/2026 — Diagnóstico de limpeza e cancelamento (T3.5 parcial)

- O status do cache agora inclui cancelamentos de fetch, tentativas/falhas de limpeza, duração e horário UTC da última tentativa; limpezas ignoradas devido à contenção no semáforo são contadas separadamente e não classificadas como falha.
- O endpoint e painel web apresentam essas métricas; teste do controller verifica o payload serializado em camelCase com valores ativos. Teste do cache verifica que contenção é registrada como ignorada, sem marcar falha, e que uma limpeza subsequente é contabilizada.
- Validações finais: cache focado — 33 aprovados; suíte Implementations — 974 aprovados/38 ignorados; API focada — 16 e suíte API — 178 aprovados; Vitest — 208 aprovados; TypeScript, lint alterado, build web de produção, verificação de 1.895 artefatos e build Release do servidor (0 erros) passaram. A primeira execução da suíte completa revelou uma corrida no teste de cancelamento; o teste agora aguarda a métrica compartilhada e a suíte completa passou.
- Revisão adversarial independente apontou a contenção não registrada; incluída métrica `cacheCleanupSkipped`, distinta de falhas, com cobertura determinística. Avisos conhecidos: NU1903 para Newtonsoft.Json 9.0.1 e alertas preexistentes de Vite/jsdom/analisadores. Nenhum processo/servidor instalado foi reiniciado; sem release ou publicação do portal enquanto o roadmap ativo não estiver completo.

### 28/09/2026 — Mapeamento estático de fluxos (T0.2 concluída)

- Documentada a cadeia de startup, ingestão/upload Nebula, download STRM, reprodução FTP/HTTP com cache, MariaDB, MongoDB/Supabase, scanners, backups e telas web de solicitações/cache.
- Separadas as responsabilidades entre MariaDB do catálogo do servidor, MongoDB `ftp` do Nebula, Telegram como armazenamento de partes, staging local, cache temporário e Supabase como destino remoto de sincronização/backup.
- Identificada distinção importante: `FullSystemBackup` e backup/sincronização Nebula para Supabase são fluxos diferentes; validar um não comprova restauração do outro.
- Escopo foi leitura de código e documentação de dependências; não houve mudança comportamental nem teste de runtime. T0.1/T0.3/T0.4 continuam abertas para medições e cenários reproduzíveis.

### 28/09/2026 — Cenários Nebula pequenos/grandes e matriz CI (T0.3 parcial)

- Adicionado cenário de catálogo STRM com 10 e 2.000 títulos, cobrindo indexação e busca do último item; adicionada recuperação após falha simulada de fetch, seguida de cache hit. Os testes existentes também cobrem cache cheio/lease, mídia local, primeiro chunk independente do prefetch seguinte e health checks com dependências indisponíveis.
- A CI agora executa os testes representativos e health checks em `ubuntu-latest` e `windows-latest`. A asserção de bloqueio de exclusão baseada em `FileShare.None` roda apenas no Windows, cujas regras de compartilhamento diferem do unlink em Unix.
- Validação local Windows/Release: testes focados — 58 implementações + 10 health checks aprovados; suíte completa `Jellyfin.Server.Implementations.Tests` — 977 aprovados, 38 ignorados, 0 falhas; suíte completa `Jellyfin.Server.Tests` — 21 aprovados, 0 falhas. O YAML da workflow foi analisado com o parser `yaml` e passou. Avisos existentes incluem NU1903 para Newtonsoft.Json 9.0.1 e analisadores.
- Limitação: a matriz foi configurada, mas ainda não executada pelo GitHub Actions; não foi feita recuperação integrada contra MongoDB/Telegram reais. Nenhum serviço de produção foi acessado. Por isso T0.3 permanece parcial e nenhuma release/portal será publicada até a conclusão e validação de todas as melhorias ativas.

### 28/09/2026 — Recuperação do health check Mongo (T0.3 parcial)

- Adicionado teste sequencial do contrato `NebulaHealthCheck`: estado Mongo indisponível retorna `Unhealthy`; na verificação seguinte, após a dependência voltar, retorna `Healthy` sem reinicializar o servidor.
- Validação Release: `NebulaHealthCheckTests` — 9 aprovados; suíte completa `Jellyfin.Server.Tests` — 22 aprovados; `dotnet build Jellyfin.Server/Jellyfin.Server.csproj -c Release --no-restore` — exit 0, 0 erros. Permanecem avisos NU1903 para Newtonsoft.Json 9.0.1.
- Limite: estado de dependência é simulado no contrato do health check; não prova conexão/reconexão com instância real MongoDB. Execução da CI remota também segue pendente.

### 28/09/2026 — Readiness leve do Nebula (T1.4 parcial)

- O health check `/ready` deixou de chamar `GetStatusAsync`, que materializava filas completas de uploads no MongoDB. Agora usa `GetComponentHealthAsync`, que verifica o ping MongoDB e o estado dos listeners FTP/HTTP sem carregar filas operacionais.
- A resposta contém apenas indicadores booleanos de MongoDB, listeners e readiness Telegram; não inclui strings de conexão, tokens, endereços privados ou dados de mídia. MongoDB/listeners indisponíveis resultam em `Unhealthy`; Telegram permanece informativo e não bloqueia readiness do transporte local.
- Testes adicionados para Nebula desabilitado, componentes saudáveis, Mongo indisponível, listeners indisponíveis, exceção, cancelamento e ausência de Telegram configurado. `dotnet test tests/Jellyfin.Server.Tests/Jellyfin.Server.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~NebulaHealthCheckTests`: 8 aprovados, 0 falhas. `dotnet build Jellyfin.Server/Jellyfin.Server.csproj -c Release --no-restore`: exit 0, 0 erros; permanece NU1903 para `Newtonsoft.Json` 9.0.1.
- `git diff --check`: exit 0. Busca por `GetStatusAsync(cancellationToken)` em `Jellyfin.Server/Health` não encontrou outros health checks com a mesma materialização de filas.
- Não houve alteração/restart do servidor instalado nem publicação. T1.4 segue parcial: health checks de outras dependências, estado degradado, exposição pública e garantia de não vazamento precisam de revisão ponta a ponta.

### 28/09/2026 — Descoberta automática de mídia no disco N após upload

- Requisito registrado: upload concluído deve tornar a mídia visível automaticamente na listagem virtual
  do `N:`, sem copiar o arquivo integralmente para a unidade.
- O `N:` é uma montagem virtual do FTP Nebula via rclone. A fonte de verdade é o documento MongoDB com
  `status = completed` e partes Telegram válidas (`tg_file_id`/`tg_file`).
- Causa atual da demora: a montagem usa `--dir-cache-time 24h`, permitindo que o rclone preserve uma
  listagem antiga após o MongoDB ser atualizado.
- Implementação planejada: persistir primeiro o estado final e, depois, invalidar somente a pasta afetada
  usando o RC do rclone e `vfs/refresh`. Reduzir globalmente `--dir-cache-time` fica como fallback, pois
  aumenta consultas `LIST` e pode reintroduzir instabilidade.
- Aceite: visibilidade sem desmontar/remontar, refresh limitado à pasta, falha do refresh sem reverter o
  upload e teste comprovando a ordem MongoDB, refresh e listagem.

#### Progresso de implementação (28/09/2026)

- O upload concluído agenda um `vfs/refresh` apenas para a pasta virtual correspondente, depois da gravação
  de `completed` no MongoDB. Não há cópia da mídia para N: nem alteração do TTL global de 24 horas.
- O RC do rclone é restrito a `127.0.0.1`, usa credenciais aleatórias no ambiente do processo (não na linha
  de comando), e a chamada é autenticada. Falhas e timeout são registrados sem desfazer o upload.
- Refreshes de uma mesma pasta são agrupados; um upload durante refresh em andamento agenda no máximo uma
  nova atualização após a atual. Pastas diferentes continuam independentes. O helper desabilita RC quando
  as credenciais não estão presentes e falha fechado quando apenas uma delas é fornecida.
- Verificação: suíte completa `Jellyfin.Server.Implementations.Tests` com 958 aprovados, 38 ignorados e
  0 falhas (inclui 219 testes Nebula de upload/fila); build Release do servidor concluído com 0 erros;
  sintaxe Python e argumento RC do helper validados; `git diff --check` concluído. Permanecem avisos
  existentes de NU1903 (`Newtonsoft.Json` 9.0.1) e de analisadores.
- T2.7 permanece **parcial**: ainda falta uma validação integrada controlada confirmando o upload no Mongo,
  o refresh autenticado no rclone real e a visibilidade imediata no N: sem reiniciar a montagem. Nenhum
  servidor instalado foi reiniciado e nenhuma release/portal foi publicada.

### 28/09/2026 — Painel de dependências e cache (T1.2 parcial)

- A tela Operações Nebula agora consulta o health check do MariaDB e apresenta saúde MongoDB/MariaDB, além de ocupação da cota e volume do cache, leases ativos e estado do pré-cache. A tela de Cache Nebula informa explicitamente quando o componente ainda não foi inicializado.
- Um endpoint de resumo conta documentos pendentes/retry e projeta somente nome e timestamp do item mais antigo, limitado a um registro; foi adicionado índice Mongo por `type/status/queued_at`. A tela deixa explícito quando o Mongo não está disponível.
- O status do cache deixou de percorrer a árvore de diretórios quando o serviço de cache não está inicializado; esse estado não é mais representado como ocupação zero válida. Naquela etapa, o cache ativo ainda caminhava os arquivos e o painel limitava a consulta a 120 s; os contadores incrementais descritos no registro de 28/09 removem essa varredura por consulta.
- Validação: API — 180 testes aprovados; suíte completa Implementations — 981 aprovados/38 ignorados/0 falhas (inclui 34 testes de cache); build Release do servidor compilado pela suíte com 0 erros; TypeScript — sucesso; Vitest — 208 testes aprovados/26 arquivos; `npm run build:production` — sucesso.
- A primeira execução Vitest teve 2 suítes impedidas por erro transitório de leitura `UNKNOWN` na configuração PostCSS (201 testes passaram); repetição após build passou integralmente. Avisos de Vite `use client`, dependência dinâmica/estática e NU1903 Newtonsoft.Json 9.0.1 permanecem.
- Limite: a consulta Mongo do resumo foi compilada e coberta pelo contrato HTTP, mas ainda não foi exercitada contra MongoDB de produção; throughput/falhas por etapa ainda não foram implementados. A contagem do cache deixou de caminhar pelo disco por consulta, mas ainda merece teste prolongado com cache pré-existente e mutações concorrentes. T1.2 permanece parcial; nenhuma release criada/publicada conforme o gate do usuário.

### 28/09/2026 — Métricas incrementais do cache de reprodução (T1.2 parcial)

- O cache agora enumera os arquivos existentes apenas uma vez ao iniciar e mantém tamanho/quantidade em memória; gravações, substituições atômicas, evicção, expiração e limpeza ajustam os valores sob o gate de armazenamento. Status e controle de cota consultam os contadores, sem varredura recursiva por polling.
- Testes focados de cache: 36 aprovados; suíte completa `Jellyfin.Server.Implementations.Tests`: 983 aprovados, 38 ignorados, 0 falhas. Uma primeira execução focada teve falha de limpeza enquanto o teste mantinha um lease aberto — comportamento esperado pela proteção de reprodução — e uma falha transitória no teste de descarte; o cenário foi isolado, o teste de limpeza passou a usar stream sem lease e a repetição focada passou integralmente.
- Ainda pendente: resumo da fila com dados Mongo em ambiente real, throughput e falhas por etapa no painel; T1.2 permanece parcial. Nenhuma release foi criada/publicada, conforme o gate do usuário.

### 28/09/2026 — Throughput e falhas recentes por etapa (T1.2 parcial)

- O resumo de fila Mongo agora inclui janela móvel de 60 minutos, quantidade de arquivos concluídos e bytes enviados, mais mídias com falha recente agrupadas em etapas de disponibilidade Telegram, transferência, integridade ou desconhecida. Falhas são registradas com timestamp e categoria de baixa cardinalidade; sucesso limpa estado de retry/motivo, preservando a última falha recente para diagnóstico.
- Foram adicionados índices para consultas por upload concluído e falhas recentes; a tela Operações Nebula exibe atividade e categorias de falha. Atualização do cache de reprodução alinhada à consulta operacional, agora a cada 30 segundos, pois ocupação deixou de exigir varredura de disco.
- Testes: `Jellyfin.Server.Implementations.Tests` — 988 aprovados, 38 ignorados, 0 falhas; API focada — 18 aprovados; Vitest — 208 aprovados/26 arquivos; TypeScript (`npm run build:check`), build de produção web, build Release do servidor e `git diff --check` passaram. Avisos existentes: NU1903 para Newtonsoft.Json 9.0.1 e diretivas `use client` ignoradas pelo Vite.
- Limite: não foi feita consulta contra MongoDB real; falhas classificadas são as rotas explícitas de `FailUploadAsync`, e o agregado conta documentos/mídias com última falha na janela, não cada tentativa individual. T1.2 permanece parcial; nenhuma release foi criada/publicada, conforme o gate do usuário.

### 28/09/2026 — Workflow remoto de cenários estava inválido (T0.3 parcial)

- Consulta pública do GitHub confirmou que as duas execuções no commit `19b5093` encerraram instantaneamente sem jobs; anotação do workflow: `Unrecognized named-value: 'secrets'` nas três condições `if` do job Android. Assim, os jobs Nebula Windows/Linux ainda não tiveram execução remota confirmada nesse commit.
- Corrigido `.github/workflows/ci.yml`: a disponibilidade do keystore é convertida em flag no ambiente do job e os três steps condicionais usam `env`, sem expor o segredo nos logs.
- Verificações locais: YAML parseado sem erros; as três condições não referenciam `secrets` diretamente; `git diff --check` passou. O workflow remoto ainda não foi reexecutado porque a alteração não foi publicada; não houve push nem release.
- T0.3 permanece parcial: além da confirmação remota, falta cenário integrado de recuperação banco/Telegram sem credenciais ou serviços de produção.

### 28/09/2026 — Correlação nos logs de exceção HTTP (T1.3 parcial)

- Os três eventos de erro do `ExceptionMiddleware` agora incluem o identificador de correlação gerado/validado pelo middleware de requisição. A mensagem continua usando método e caminho, sem query string ou URL completa.
- Testes da API: suíte completa — 181 aprovados; teste focado após adicionar verificação contra vazamento de query string — 1 aprovado. Build ocorreu em configuração Release e `git diff --check` passou.
- T1.3 continua parcial: retenção Serilog e revisão abrangente/redação de outros logs ainda não foram feitas.

### 28/09/2026 — Redação adicional de logs sensíveis (T1.3 parcial)

- Removidos tokens, código/segredo Quick Connect, IDs de usuário em emissão de token, query strings e URLs potencialmente assinadas dos logs de stream, resposta lenta, Supabase, atualizações, plugins, webhooks e download de imagens. Eventos mantêm status, rota/método ou tipo de operação para diagnóstico.
- Regressões verificam que token de sessão, código e segredo Quick Connect não aparecem nas mensagens capturadas. Testes Release: API — 181 aprovados; conjunto focado de logs — 29 aprovados. A suíte completa de Implementations teve uma falha intermitente no teste preexistente `DisposingCacheDoesNotWaitForBlockingSharedFetchCancellationCallback` (988 aprovados, 38 ignorados); repetido isoladamente, passou (1/1). Uma execução completa anterior havia passado com 989 aprovados e 38 ignorados.
- `git diff --check` passou. T1.3 permanece parcial: falta alinhar retenção configurável ao Serilog e concluir auditoria/testes dos demais caminhos de log. Nenhum artefato ou release foi criado/publicado.

### 28/09/2026 — Retenção de logs alinhada à configuração (T1.3 parcial)

- `DeleteLogFileTask` agora remove logs Serilog e legados após `LogFileRetentionDays`. O padrão Serilog deixa `retainedFileCountLimit` ilimitado; a rotina `AlignLogRetention` migra a configuração padrão existente antes do logger iniciar e mantém intacto o override `logging.json`.
- Testes verificam limpeza de logs antigos e preservação dos recentes, migração sem tocar no override, e comportamento real do File sink com 40 rotações sem limite fixo. Suítes Release: Implementations — 990 aprovados, 38 ignorados; Server — 31 aprovados. Build Release do servidor e `git diff --check` passaram.
- Aviso pré-existente: NU1903 para Newtonsoft.Json 9.0.1. T1.3 segue parcial pela auditoria restante de logs; nenhuma release foi criada/publicada.

### 28/09/2026 — Contrato da máquina de estados de upload Nebula (T2.1)

- Especificados estados, transições permitidas, claim com lease/heartbeat/fencing, persistência de progresso e partes, retomada após lease expirado, classificação de retry/falha terminal/cancelamento e compatibilidade com documentos Mongo legados.
- Auditoria do código atual confirmou gaps: `failed` sempre recebe retry de 1 minuto sem teto; `modified_at` é lease implícito de 1 hora; startup re-enfileira todo `uploading`; `worker_id` pode ser opcional nas mutações; não há estado persistido de cancelamento.
- T2.1 fica concluída como especificação. Implementação do contrato fica nas tarefas T2.2/T2.4/T2.5. Nenhum estado/dado de produção foi alterado. Validação documental: `git diff --check` e revisão cruzada da especificação com os métodos atuais do Mongo e watcher; nenhuma release criada/publicada.

### 28/09/2026 — Deduplicação Nebula valida conjunto completo de partes (T2.2 parcial)

- O código tratava qualquer parte com `tg_file_id` como upload completo em buscas de mídia já enviada, limpeza de staging e deduplicação do downloader. Isso podia descartar o arquivo local após uma falha parcial.
- Adicionado validador de conclusão: exige status `completed`, tamanho conhecido, partes em sequência sem lacunas, IDs Telegram, estados de parte concluídos e soma exata do tamanho. Documentos legados sem array de partes só são aceitos com tamanho e ID Telegram na raiz; array vazio/inválido não é aceito.
- O scanner agora preserva arquivos de uploads `failed` com partes parciais e os reencaminha para retomada; o motor já valida cada parte antes de reutilizá-la.
- A remoção incremental de STRM agora também exige partes íntegras quando o documento diz `completed`; registros incompletos não apagam a fonte local.
- Testes Release focados de watcher/upload/deduplicação: 231 aprovados; suíte completa `Jellyfin.Server.Implementations.Tests`: 991 aprovados, 38 ignorados, 0 falhas. O projeto compila durante a execução da suíte; `git diff --check` passou após atualizar este registro. Aviso NU1903 para Newtonsoft.Json 9.0.1 permanece.
- T2.2 continua parcial: a proteção local está coberta, mas concorrência de múltiplos produtores, matching canônico sob corrida e confirmação com Telegram/Mongo real ainda não foram exercitados. Nenhuma mídia de produção foi alterada; nenhuma release criada/publicada.

### 28/09/2026 — Prioridade planejada visível no dashboard (T2.3 parcial)

- O status de download do Nebula agora carrega posição/total no plano da varredura, justificativa da prioridade e próximo título; o painel separa essa previsão da fila persistida de upload.
- A ordenação existente continua priorizando solicitações, depois categoria e A–Z; teste cobre a explicação de prioridade solicitada e da ordem padrão.
- Validação final desta etapa: `dotnet test tests/Jellyfin.Server.Implementations.Tests/Jellyfin.Server.Implementations.Tests.csproj -c Release --no-restore` passou com 993 aprovados, 38 ignorados e 0 falhas; `dotnet build Jellyfin.Server/Jellyfin.Server.csproj -c Release --no-restore` passou com 0 erros; `npm run build:check` passou; `git diff --check` passou. Permanecem avisos preexistentes de NU1903/Newtonsoft.Json 9.0.1.
- T2.3 permanece parcial até a fila de envio persistida mostrar posição/razão efetivas e a política única de categoria ser validada. Nenhuma release foi criada/publicada.

### 28/09/2026 — Fairness e backpressure da fila Nebula (T2.4 parcial)

- A seleção anterior drenava a fila prioritária enquanto ela não esvaziasse, permitindo starvation dos uploads comuns sob solicitações contínuas.
- Introduzido scheduler FIFO com limite de quatro itens prioritários consecutivos quando há trabalho normal esperando; sem trabalho normal, prioridade não é atrasada. Promoção de fila ocorre sob lock curto; logs são emitidos fora do lock.
- Testes cobrem turno normal após quatro prioridades, prioridade contínua sem fila normal e promoção seletiva preservando FIFO.
- A fila em memória agora tem teto de oito posições por worker e reserva até uma posição por worker para retries locais. Produtores externos respeitam o limite menor; estouro remove a chave de deduplicação e agenda rescan local, sem abandonar o arquivo. A contagem inclui itens comuns e prioritários.
- A restauração Mongo agora percorre cursor em lotes de 128, em vez de materializar todos os uploads pendentes na memória; ela para quando atinge o limite dos produtores e continua nas varreduras periódicas. O rescan local lazy percorre staging e para ao preencher a fila.
- O pool Telegram mantém cooldown por bot ao receber HTTP 429: respeita `retry_after` (limitado a 300 s), usa 5 s como fallback seguro e deixa bots saudáveis continuarem atendendo. Isso evita selecionar repetidamente um bot ainda em flood-wait sem impor atraso fixo aos demais.
- Cobertura atual: fairness, prioridade sem fila normal, promoção FIFO, limite de produtor/reserva e produtores concorrentes não ultrapassando capacidade.
- Cobertura adicional: `retry_after` válido, fallback quando ausente e limite superior do cooldown.
- Validação: suíte completa passou com 999 aprovados, 38 ignorados e 0 falhas; build Release do servidor passou com 0 erros; `git diff --check` passou. Persistem avisos NU1903 de Newtonsoft.Json 9.0.1.
- Limite: T2.4 permanece parcial até teste sustentado com carga e confirmação de comportamento com Telegram/Mongo reais; limites existentes por upload, worker e bot foram mapeados, mas precisam ser medidos/alinhados. Revisão adversarial delegada não executou porque o limite de agentes estava cheio; revisão local do diff feita. Nenhuma release criada/publicada.

### 28/09/2026 — Backoff de falhas de upload Nebula (T2.5 parcial)

- Falhas de upload deixam de repetir em intervalo fixo de 1 minuto: `retry_after` passa a usar backoff exponencial com jitter de ±20%, começando em 1 minuto e limitado a 1 hora. A contagem existente `retry_count` determina a próxima janela; documentos legados sem contador iniciam na primeira tentativa.
- O cooldown adaptativo por bot para HTTP 429, registrado na seção T2.4, complementa o backoff do arquivo: o bot respeita o flood-wait e os outros bots seguem disponíveis.
- Testes da política verificam progressão, limite de 1 hora e jitter; testes do pool verificam flood-wait válido, fallback de 5 s e teto de 300 s.
- Validação Release: testes focados — 9 aprovados; suíte completa — 1007 aprovados, 38 ignorados e 0 falhas; build do servidor — 0 erros. A suíte completa teve uma falha intermitente preexistente em `DisposingCacheDoesNotWaitForBlockingSharedFetchCancellationCallback`, passou isolada e a repetição completa passou. NU1903 existente para Newtonsoft.Json 9.0.1 permanece.
- T2.5 permanece parcial: faltam limite de tentativas/falha terminal, tela/ação administrativa de reprocessamento e cancelamento seguro. Nenhuma release criada/publicada.

### 28/09/2026 — Falhas terminais e reprocessamento administrativo (T2.5 parcial)

- Após 8 tentativas automáticas, o upload passa a falha terminal persistida (`failure_terminal`), sem `retry_after`. Recuperação no startup, claim e reconciliação horária excluem explicitamente esses registros; um documento terminal não volta à fila por ter `retry_after` ausente.
- O painel Nebula lista até 100 falhas terminais recentes e permite reprocessar individualmente. A API de retry exige elevação; o manager só aceita caminhos existentes dentro das raízes de staging configuradas. Reprocessamento preserva partes já enviadas, reinicia o contador automático e insere na fila ativa (ou persiste `queued` para recuperação posterior).
- Testes Release: política de retry — 11 aprovados; suíte completa Implementations — 1.010 aprovados, 38 ignorados, 0 falhas; suíte API — 183 aprovados, 0 falhas; build Release do servidor — 0 erros; `git diff --check` passou. Permanecem avisos NU1903 já conhecidos para Newtonsoft.Json 9.0.1.
- T2.5 segue parcial: cancelamento seguro não foi implementado; não houve ensaio integrado com MongoDB/Telegram reais nem teste de carga operacional. Nenhuma release foi criada/publicada; todas as melhorias ativas precisam estar concluídas e validadas antes da release única do ciclo.

### 28/09/2026 — Cancelamento seguro de uploads Nebula (T2.5 parcial)

- A API elevada lista uploads canceláveis e persiste a solicitação com usuário/horário. Itens ainda não reivindicados passam diretamente a `cancelled`; uploads ativos terminam a parte Telegram em curso, salvam o checkpoint confirmado e param antes da próxima parte. Shutdown continua recuperável e não é tratado como cancelamento explícito.
- O painel mostra uploads ativos/progresso, permite cancelar, lista cancelados junto das falhas terminais e permite retomar um cancelado sem apagar partes confirmadas. O watcher não reencaminha uma mídia cancelada automaticamente.
- Transições de conclusão e falha agora excluem atomicamente solicitações de cancelamento. Revisão local encontrou e corrigiu duas corridas: falha sobrescrevendo cancelamento e remoção da origem em deduplicação que não concluiu; cancelamentos sob resposta de rede ambígua ainda requerem reconciliação com Telegram.
- Consultas de recuperação agora excluem estado `cancelled` e pedidos ainda pendentes; solicitações de mídia persistidas são registradas no watcher antes de iniciar a restauração/workers, evitando dequeue sem prioridade no primeiro ciclo (T2.3 parcial).
- Validação Release: `Jellyfin.Server.Implementations.Tests` — 1.010 aprovados, 38 ignorados, 0 falhas; `Jellyfin.Api.Tests` — 186 aprovados, 0 falhas; `npm run build:check` — exit 0; `git diff --check` — exit 0. O build compilou a implementação alterada; aviso NU1903 preexistente para Newtonsoft.Json 9.0.1 permanece.
- Limites: sem MongoDB isolado/Telegram real, teste sustentado ou revisão adversarial delegada (limite de agentes atingido); nenhuma operação foi executada no MongoDB de produção. T2.5 permanece parcial e nenhuma release foi criada/publicada, conforme o gate único solicitado.

### 28/09/2026 — Ordem efetiva de uploads no dashboard (T2.3 parcial)

- A fila fairness agora gera snapshot de dequeue sem remover itens nem alterar seu contador de fairness. O status Nebula expõe posição e razão de cada item carregado; painel lista os cinco próximos e identifica o próximo dequeue. A razão distingue faixa prioritária de turno normal por fairness.
- O painel separa a ordem da fila residente nos workers da contagem Mongo de pendências, evitando tratar itens ainda não admitidos na fila local como posições exatas.
- Testes Release: `NebulaFairUploadQueueTests` — 7 aprovados; suíte `Jellyfin.Server.Implementations.Tests` — 1.012 aprovados, 38 ignorados, 0 falhas; `Jellyfin.Api.Tests` — 186 aprovados; `npm run build:check` — exit 0; `npm test -- --run` — 208 aprovados; `git diff --check` — exit 0.
- `npm run lint:changed` não passou: ESLint reportou 32 erros na rota Nebula, nas linhas de campos de API, complexidade e blocos preexistentes de renderização/cancelamento; nenhum erro foi reportado nas linhas do novo snapshot da fila. A limpeza restante está registrada em W4.4.
- Limites: snapshot é instantâneo e não promete reserva diante de workers concorrentes; posições e prioridades de toda a fila ainda não são persistidas nem reconciliadas ao reiniciar. Sem inspeção renderizada com runtime autenticado, Mongo/Telegram de produção ou teste de carga. T2.3 segue parcial; nenhuma release foi criada/publicada.

### 28/09/2026 — Limpeza de staging fora do loop de varredura (T2.6 parcial)

- A limpeza recursiva agora roda em tarefa de fundo single-flight no startup e após resync completo; não bloqueia restauração periódica nem novo pedido de rescan. O encerramento cancela cooperativamente e aguarda a tarefa.
- Cancelamento é verificado entre entradas do filesystem; o progresso informa início, raízes concluídas e tempo total. Falha de uma raiz continua best-effort e não interrompe uploads.
- Testes Release de `NebulaStagingWatcherTests`: 6 aprovados, incluindo cancelamento antes de percorrer/deletar entradas; `git diff --check` pendente nesta etapa.
- T2.6 permanece parcial: sem checkpoint reiniciável dentro de raízes grandes, medição com volume representativo nem teste de startup/cancelamento no host real. Nenhuma release foi criada/publicada.

### 28/09/2026 — Quality gate incremental da tela Nebula (W4.4 parcial)

- A rota `dashboard/routes/nebula` tinha 32 violações ESLint: complexidade concentrada no componente de página, condições aninhadas, handlers inline e chaves JSON `snake_case` incompatíveis com a regra de nomes TypeScript.
- Extraídos componentes focados para saúde do banco, resumo/snapshot da fila, uploads canceláveis/falhos e cache. Handlers agora usam callbacks estáveis e `data-upload-id`; o dicionário das etapas mantém as chaves externas sem desativar regras de lint. Estados e textos da interface foram preservados.
- Validação real: `npm run lint:changed` passou para a rota; `npm run build:check` passou; Vitest passou com 208 testes/26 arquivos; `npm run build:production` passou; `git diff --check` passou.
- O build mantém avisos Vite preexistentes de diretivas `use client` e imports mistos. Sem Playwright, navegação autenticada ou revisão visual nesta etapa; W4.4 segue parcial. Nenhuma release foi criada/publicada.

### 28/09/2026 — Liberação de leases sob exceção, cancelamento e encerramento (T3.3 concluída)

- INTENT: provar que um lease ativo do cache de reprodução (`NebulaPlaybackCache.Acquire`) é sempre liberado nos três cenários citados no aceite da tarefa — exceção dentro do escopo `using`, cancelamento da operação de fetch subjacente e descarte do cache (`Dispose()`) antes do lease individual ser descartado, cenário que ocorre durante o encerramento do servidor (`NebulaFtpManager.StopEnvioAsync` chama `_playbackCache?.Dispose()`).
- Adicionados três testes: `Lease_IsReleasedEvenWhenOperationInsideUsingBlockThrows` (uma exceção lançada dentro do `using var lease = cache.Acquire(...)` ainda decrementa `ActiveLeasesCount` via o `finally` implícito do `using`); `Lease_IsReleasedWhenTheUnderlyingFetchIsCanceled` (cancelar o token do `GetOrFetchChunkAsync` não libera o lease sozinho — o chamador, isto é, o `NebulaChunkedStream`, continua dono do lease até seu próprio `Dispose()`, e o lease correto libera normalmente depois); `Lease_DisposalAfterCacheShutdownDoesNotThrow` (descartar o cache primeiro e o lease depois não lança `ObjectDisposedException` nem qualquer outra exceção, replicando a ordem real de shutdown do `NebulaFtpManager`).
- A investigação confirmou no código existente (`PlaybackLease.Dispose()` usa `Interlocked.Exchange` para idempotência, e `Release(string mediaKey)` em `NebulaPlaybackCache.cs:977` apenas retorna cedo se a chave não estiver em `_activeMedia`) que a implementação já era segura para os três cenários; os testes formalizam essa garantia e a protegem contra regressão futura, sem alteração de produção.
- Testes focados `Lease_*`: 3/3 aprovados. Suíte completa `Jellyfin.Server.Implementations.Tests` Release: 1.048 aprovados, 38 ignorados, 0 falhas (1.045 anteriores + 3 novos). Build Release do servidor: 0 erros. `git diff --check`: exit 0 (avisos apenas de conversão LF/CRLF).
- T3.3 concluída — cobre lease sob exceção, cancelamento e shutdown, além dos cenários de limpeza manual/cota já cobertos anteriormente. Nenhuma release foi criada/publicada.

### 28/09/2026 — Alertas operacionais: fila, backup, disco, provedor e restauração (T1.5 concluída)

- INTENT: implementar os cinco alertas nomeados no roadmap (fila sem progresso, backup vencido, disco próximo do limite, erro repetido de provedor, falha de restauração) como avaliação determinística sobre sinais já agregados, sem I/O direto, para permitir teste isolado e reduzir risco de vazamento de dados sensíveis nas mensagens.
- Criado `OperationalAlertEvaluator` (`MediaBrowser.Model/Nebula/OperationalAlertEvaluator.cs`), classe pura e sem dependências que recebe `OperationalAlertInputs` (sinais já agregados: data do item pendente mais antigo, data do último backup bem-sucedido, proporção de espaço livre por papel de pasta, contagem recente de falhas por etapa, falha de restauração) e devolve `OperationalAlertDto[]` ordenados por severidade. Mensagens usam apenas texto de baixa cardinalidade, sem caminho, nome de arquivo, URL ou credencial.
- Persistido o resultado da última restauração Supabase→MongoDB, que antes só existia para backup: `NebulaFtpConfiguration.SupabaseLastRestoreTime/Status/Failed` são gravados tanto no caminho de sucesso/falha normal quanto no `catch` de exceção não tratada de `RestoreSupabaseToMongoAsync`, e expostos em `NebulaSupabaseStatusDto.LastRestoreTime/Status/Failed`.
- Adicionado `GET /ServerHealth/Alerts` em `ServerHealthController`, que agrega: posição/idade do item mais antigo e falhas recentes por etapa da fila Nebula (`GetUploadQueueSummaryAsync`), último backup local bem-sucedido (`IBackupService.EnumerateBackups`) e do Supabase (`GetSupabaseStatusAsync`), estado da última restauração Supabase, e proporção de espaço livre das pastas cache/backup/transcode/log (`ISystemManager.GetSystemStorageInfo`). Falha de uma fonte opcional (Nebula desabilitado, Supabase não configurado) não impede os demais alertas de serem avaliados — capturada e ignorada individualmente, sem interromper a resposta.
- Web: novo hook `useOperationalAlerts` (`src/hooks/useOperationalAlerts.ts`) consulta o endpoint com TanStack Query (`staleTime` de 30s); novo `OperationalAlertsCard` (`src/apps/dashboard/components/widgets/OperationalAlertsCard.tsx`) exibe estados de carregamento, erro com retry, "nenhum alerta ativo" e a lista de alertas por severidade; integrado ao `ServerHealthPanel` existente na tela `/dashboard/server-health`.
- Testes .NET focados: `OperationalAlertEvaluatorTests` (`Jellyfin.Model.Tests`) — 21 aprovados, cobrindo os 5 gatilhos (limite exato e abaixo/acima), ausência de sinal (sem backup, sem fila pendente), múltiplos papéis de disco afetados, ordenação por severidade e ausência de caminho/URL/credencial nas mensagens. `ServerHealthControllerTests` (`Jellyfin.Api.Tests`) — 3 aprovados, cobrindo sinais saudáveis sem alertas, restauração falha + backup ausente gerando dois alertas críticos, e exceção do Nebula não bloqueando os demais alertas.
- Validação Release: `Jellyfin.Model.Tests` — 684 aprovados, 0 falhas; `Jellyfin.Api.Tests` — 189 aprovados, 0 falhas; suíte completa `Jellyfin.Server.Implementations.Tests` — 1.045 aprovados, 38 ignorados, 0 falhas; `dotnet build Jellyfin.Server/Jellyfin.Server.csproj -c Release --no-restore` — 0 erros. `git diff --check` — exit 0 (avisos apenas de conversão LF/CRLF). Avisos de analisadores pré-existentes (SA1402/SA1623/NU1903) inalterados; nenhum novo aviso introduzido pelos arquivos criados além do padrão SA1402 já presente em `NebulaDtos.cs`.
- Limite: os limiares (120 min de fila parada, 7 dias sem backup, 10% de disco livre, 5 falhas repetidas) são valores padrão configuráveis via `OperationalAlertInputs`, mas ainda não são expostos como configuração de usuário nem calibrados contra volume de produção; isso é trabalho futuro, fora do escopo mínimo do item. O endpoint não foi exercitado contra uma instância em execução (sem MongoDB/Supabase/disco reais); a validação é por teste unitário com dependências mockadas. T1.5 é considerada concluída porque implementa e testa os cinco alertas nomeados no aceite da Fase 1, não porque calibra os limiares para produção — calibração de limiar pertence a T0.4/T1.2 conforme evidência acumulada.
- Nenhuma release foi criada/publicada. Nenhum servidor instalado foi reiniciado.

### 28/09/2026 — Correção de `stripTrailingYear` e quality gate web verde (W1.2 parcial)

- INTENT: `classifyMediaRequests`/`isMediaRequestIncluded` precisam separar corretamente solicitações pendentes de títulos já incluídos por título normalizado, categoria e ano; a implementação anterior de `stripTrailingYear` tinha uma variável `lastCharacter` redeclarada no mesmo escopo, quebrando a transformação do bundle (`esbuild`/Vite) e, isoladamente, misturava a lógica de colchetes/parênteses com a de separador `-`/`–`, fazendo `Atomic (2024)` não normalizar para o mesmo título de `Atomic`.
- Corrigido: a função agora primeiro detecta e remove `)`/`]` finais antes de procurar o ano, e só depois decide entre remover o par de parênteses/colchetes de abertura ou um separador `-`/`–`, sem reusar o nome de variável. Também substituído `String.prototype.at` (não suportado no alvo `ES2020`/`lib` do `tsconfig.json`) por um helper `lastChar` baseado em `slice(-1)`, compatível com o build.
- Testes focados `mediaRequestUtils.test.ts`: 5/5 aprovados (categoria distinta, remake por ano, normalização de título com pontuação, ambiguidade legada sem ano e separação pending/included). Suíte completa `npm test -- --run`: 28 arquivos, 217 testes aprovados, 0 falhas. `npm run build:check` (tsc --noEmit): exit 0, sem erros. `npm run lint:changed`: exit 0, sem violações no arquivo alterado. `npm run build:production`: exit 0. `npm run verify:build`: 1.895 artefatos válidos, limite de 1.536 KiB respeitado. `git diff --check`: exit 0 (aviso preexistente de conversão LF/CRLF).
- Limite: não há teste de renderização (`@testing-library/react` não está entre as dependências do projeto; não foi adicionada dependência nova sem aprovação). Os estados de carregamento/erro/lista vazia já existem em `UserFeedbackListPage.tsx` (spinner via `MaterialReactTable` state, `Alert` com retry em erro, tabelas MRT mostram estado vazio nativo), mas não foram exercitados por teste automatizado nem inspeção visual em runtime nesta etapa.
- W1.2 permanece **parcial**: a classificação título/categoria/ano está corrigida e coberta por teste, mas autocomplete com debounce/cancelamento, botão de solicitação junto a Favoritos, estados de indexação, posição/prioridade visível e confirmação de envio continuam pendentes. Nenhuma release foi criada/publicada.

### 29/09/2026 — Permissões restritas de arquivo (owner-only) nos arquivos de configuração em disco (T6.4 parcial)

- INTENT: arquivos de configuração do servidor (`ServerConfiguration.xml`, `nebulaftp.xml` e todos os demais salvos por `BaseConfigurationManager`) eram escritos com `XmlSerializer.SerializeToFile` sem nenhum ajuste de permissão. Em Linux, a permissão padrão do processo (normalmente `644`, controlada pelo `umask` do sistema) deixa esses arquivos legíveis por **qualquer conta local**, mesmo contendo segredos em texto plano (`Password`, `ApiHash`, `BotTokens`, `SupabaseKey`, `MongoDbConnectionString` do Nebula). A redação de resposta HTTP (T6.4 anterior) protege a rede, mas não protege o arquivo em disco de um usuário local não privilegiado.
- Adicionado `RestrictConfigurationFilePermissions(path)` em `BaseConfigurationManager.cs`, chamado dentro do mesmo `lock (_configurationSyncLock)` logo após cada `XmlSerializer.SerializeToFile`, em ambos os pontos de escrita (`SaveConfiguration()` sem chave, para a config principal do servidor, e `SaveConfiguration(string key, object configuration)`, usado por toda config nomeada incluindo `nebulaftp`). Em plataformas não-Windows, aplica `UnixFileMode.UserRead | UnixFileMode.UserWrite` via `File.SetUnixFileMode`; falhas (ex.: sistema de arquivos sem suporte a bits Unix) são logadas como aviso, nunca interrompem o salvamento. Em Windows a chamada é pulada (evita `PlatformNotSupportedException`; o ACL NTFS padrão já restringe por conta/administrador).
- Testes novos em `tests/Jellyfin.Server.Implementations.Tests/Configuration/ServerConfigurationManagerFilePermissionsTests.cs` (arquivo/pasta de teste novos): `SaveConfiguration_RestrictsFilePermissionsToOwnerOnUnix` constrói um `ServerConfigurationManager` real com `ServerApplicationPaths` apontando para um diretório temporário, chama `SaveConfiguration()` e verifica `File.GetUnixFileMode(...) == UserRead|UserWrite` (usa `Assert.SkipWhen(OperatingSystem.IsWindows(), ...)`, já que bits Unix não existem neste SO de desenvolvimento); `SaveConfiguration_DoesNotThrowOnAnyPlatform` garante que o fluxo nunca lança exceção em nenhum SO (roda em Windows também).
- Validação: testes focados 1 aprovado + 1 ignorado (Windows, esperado); suíte completa `Jellyfin.Server.Implementations.Tests`: 1051 aprovados (1050+1), 39 ignorados (38+1), 0 falhas; build completo `dotnet build MulletaFlix.sln -c Release`: 0 erros (208 avisos pré-existentes, nenhum novo).
- Limite: o teste que valida o bit Unix real não pode ser executado neste ambiente Windows (fica `[SKIP]`) — a lógica foi validada por leitura de código, pela API documentada do .NET (`File.SetUnixFileMode`) e pelo teste de "não lança exceção em nenhuma plataforma"; uma validação em runner Linux real (CI) é recomendada antes de considerar este item 100% coberto. T6.4 continua **parcial**: falta criptografia do conteúdo (não só permissão do arquivo), rotação de segredos e revisão ampla de dependências.

### 29/09/2026 — Teste de regressão para a redação de segredos do config Nebula (T6.4 parcial)

- INTENT: `NebulaFtpController.GetConfig()` — o endpoint que a UI administrativa chama para popular o formulário de configuração — já redigia todos os campos de segredo (`Password`, `HttpStreamToken`, `MongoDbConnectionString`, `ApiHash`, `SupabaseKey`, `BotTokens`) via `CreateSafeConfigResponse`, mas esse comportamento nunca tinha sido testado diretamente; só o caminho de escrita (`UpdateConfig`/`PreserveExistingSecretValues`, que reaplica segredos existentes quando o campo chega vazio) tinha teste. Uma futura mudança em `CreateSafeConfigResponse` (por exemplo, esquecer de redigir um campo novo) vazaria credenciais para qualquer sessão de admin sem que nenhum teste falhasse.
- Adicionado `GetConfig_RedactsAllSecretFieldsFromResponse`: monta uma configuração com todos os seis campos de segredo preenchidos, chama `GetConfig()` e confirma que cada um retorna `string.Empty`, enquanto campos não sensíveis (`ServerHost`, `ServerPort`, `Username`) continuam presentes na resposta.
- Auditoria complementar de logs: nenhuma chamada `_logger.Log*`/`AddDownloaderLog`/`EmitServerLog` em `NebulaFtpManager.cs` ou `NebulaTelegramPool.cs` embute o token do bot, a senha FTP ou uma URL com credenciais — os logs usam `ex.Message`, código de status HTTP ou nomes de mídia, nunca os valores brutos de `token`/`password`/`endpoint`. `NebulaFtpManager.GetBots()` já retorna apenas `MaskedToken` (últimos 4 caracteres via `MaskBotToken`), nunca o token completo.
- Testes focados `NebulaFtpControllerTests`: 24/24 aprovados (23 + 1 novo). Suíte completa `Jellyfin.Api.Tests` Release: 194/194 aprovados (193 + 1), 0 falhas.
- Limite: T6.4 continua **parcial**. Proteção em repouso (criptografia do arquivo `nebulaftp.xml`/config no disco), rotação de segredos e revisão ampla de dependências vulneráveis (além do Newtonsoft.Json já corrigido) não foram auditadas nesta etapa. Nenhuma release foi criada/publicada.

### 29/09/2026 — Política de acesso anônimo LAN e loopback (T6.5 parcial)

- INTENT: validar os endpoints de primeiro setup protegidos por `AnonymousLanAccessPolicy` e confirmar que acesso local/loopback não é confundido com acesso remoto.
- O teste novo `tests/Jellyfin.Api.Tests/Auth/AnonymousLanAccessPolicy/AnonymousLanAccessHandlerTests.cs` cobre loopback (`127.0.0.1`), endereço LAN, endereço remoto e `RemoteIpAddress` ausente.
- A execução RED revelou uma inconsistência real: `GetNormalizedRemoteIP()` converte IP ausente em loopback, mas `AnonymousLanAccessHandler` só aceitava `null` ou endereços classificados pelo `INetworkManager`; assim, loopback podia ser rejeitado quando não estivesse explicitamente na lista de LAN. O handler foi corrigido para aceitar `IPAddress.IsLoopback(ip)` diretamente.
- Validação focada: 4/4 testes aprovados; suíte completa `Jellyfin.Api.Tests` Release: 198/198 aprovados, 0 falhas; `dotnet build MulletaFlix.sln -c Release --no-restore`: 0 erros (2 avisos XML/analisadores já existentes).
- Limite: T6.5 permanece **parcial**. Ainda falta isolar o binding do servidor/setup administrativo a loopback/LAN e auditar a exposição de endpoints internos de diagnóstico/telemetria. Nenhuma release foi criada/publicada.
### 29/09/2026 — Proteção da página de configuração de plugins (T6.5 parcial)

- INTENT: auditar endpoints internos que servem diagnósticos e recursos administrativos. `DashboardController.GetDashboardConfigurationPage` retornava HTML/JavaScript de páginas de configuração sem atributo `[Authorize]`, apesar de `GetConfigurationPages` exigir elevação.
- Correção: a rota `GET web/ConfigurationPage` agora exige `Policies.RequiresElevation`, protegendo também os recursos da configuração Nebula servidos por esse método.
- Teste novo em `SecurityAuthorizationTests.SensitiveDiagnosticsAndPluginConfigurationEndpoints_RequireElevation`: verifica elevação em `ActivityLogController`, `PlaybackReportsController`, `ServerHealthController` e nos dois endpoints relevantes de `DashboardController`. A execução RED falhou especificamente na rota sem atributo; após a correção, passou.
- Validação: teste focado aprovado; suíte completa `Jellyfin.Api.Tests` Release: 199/199 aprovados; build completo Release: 0 erros.
- Limite: T6.5 permanece **parcial**. Ainda falta isolar o binding do servidor/setup administrativo a loopback/LAN e concluir a auditoria de endpoints internos de diagnóstico/telemetria. Nenhuma release foi criada/publicada.

### 29/09/2026 — Isolamento do servidor de setup a loopback/LAN (T6.5 parcial)

- INTENT: o servidor temporário de primeiro setup calculava interfaces de bind a partir da configuração de rede e podia ficar acessível por interfaces remotas; suas rotas de health, informações públicas e UI de setup não tinham uma barreira comum baseada no endereço de origem.
- Correção: `SetupServer` agora usa middleware global após `UseForwardedHeaders` para rejeitar com `401 Unauthorized` qualquer requisição cujo IP normalizado não seja loopback nem seja classificado como LAN por `INetworkManager`. A mesma decisão foi centralizada em `IsLocalNetworkRequest` e reutilizada pelo logger de startup e pelo indicador `localNetworkRequest` da UI.
- Testes novos em `tests/Jellyfin.Server.Tests/ServerSetupApp/SetupServerNetworkAccessTests.cs`: loopback e LAN permitidos, endereço remoto rejeitado e ausência de `INetworkManager` rejeitada. A execução RED confirmou a ausência da proteção antes da implementação; depois da correção, o teste passou.
- Validação: teste focado `SetupServerNetworkAccessTests`: 4/4 aprovados; suíte completa `Jellyfin.Server.Tests` Release: 39/39 aprovados; `dotnet build MulletaFlix.sln -c Release --no-restore`: 0 erros; `git diff --check`: sem erros.
- Limite: T6.5 permanece **parcial**. A proteção implementada cobre o servidor temporário de setup; ainda falta auditoria abrangente dos endpoints internos de diagnóstico/telemetria do servidor principal. Nenhuma release foi criada/publicada.

### 29/09/2026 — Auditoria adicional de endpoints internos de sistema (T6.5 parcial)

- Auditoria realizada em `SystemController`, `EnvironmentController`, `StartupController`, `ClientLogController` e nos endpoints de health/configuração já cobertos anteriormente.
- Confirmado que armazenamento do sistema, listagem/download de logs e configuração de ambiente exigem `Policies.RequiresElevation` ou `Policies.FirstTimeSetupOrElevated`; mutações do wizard exigem `Policies.AnonymousLanAccessPolicy`; upload de logs do cliente exige autenticação e limite de 1.000.000 bytes.
- Ampliado `SecurityAuthorizationTests.SensitiveDiagnosticsAndPluginConfigurationEndpoints_RequireElevation` para verificar `SystemController.GetSystemStorage`, `GetServerLogs`, `GetLogFile` e a política de `EnvironmentController`.
- Validação: teste focado aprovado; suíte completa `Jellyfin.Api.Tests` Release: 199/199 aprovados; `git diff --check`: sem erros.
- Limite: a auditoria não transforma T6.5 em concluída — endpoints públicos intencionais (`System/Info/Public`, `System/Ping`) continuam sem autenticação, enquanto uma revisão automatizada completa de todas as superfícies de telemetria/health fora dos controllers ainda precisa ser feita. Nenhuma release foi criada/publicada.

### 29/09/2026 — Proteção dos endpoints de métricas e health (T6.5 parcial)

- Auditoria em `Jellyfin.Server/Startup.cs` encontrou `MapMetrics()`, `/health` e `/ready` registrados sem metadata de autorização, apesar de exporem observabilidade e estado de componentes internos.
- Correção: os três endpoints agora usam `Policies.LocalAccessOrRequiresElevation`, preservando acesso local e de usuários elevados e bloqueando acesso remoto/anônimo pelo pipeline de autorização.
- Validação: `dotnet build MulletaFlix.sln -c Release --no-restore`: 0 erros; testes focados de servidor (OpenTelemetry + isolamento do setup): 11/11 aprovados; suíte `Jellyfin.Api.Tests` Release: 199/199 aprovados.
- Limite: T6.5 permanece **parcial**. Falta revisar outras superfícies de telemetria/diagnóstico e validar monitoramento remoto autenticado. Nenhuma release foi criada/publicada.

### 29/09/2026 — Lockout de autenticação persistido e validado (T6.5 parcial)

- INTENT: validar a política de contas administrativas `LoginAttemptsBeforeLockout` no caminho real de autenticação, não apenas por inspeção de propriedades ou contador em memória. O risco identificado era `SetPermission(PermissionKind.IsDisabled, true)` alterar a navegação `Permissions` sem que a atualização fosse persistida.
- Correção: `UserManager.UpdateUserInternalAsync(UsersDbContext, User)` passou de `Attach(user)` + estado `Modified` somente na entidade raiz para `dbContext.Update(user)`, permitindo que o grafo com entidades já identificadas — incluindo `Permissions` — seja persistido. Sem isso, o contador poderia avançar enquanto o bloqueio não sobreviveria a uma nova leitura do usuário.
- Testes novos em `tests/Jellyfin.Server.Implementations.Tests/Users/UserManagerAuthenticationLockTests.cs`: autenticação real com `DefaultAuthenticationProvider` e hash de senha real; três falhas incrementam o contador e persistem `IsDisabled`; a quarta tentativa lança `SecurityException`; login correto zera o contador. Também permanecem os dois testes de serialização por identidade.
- Validação: teste focado `UserManagerAuthenticationLockTests`: 4/4 aprovados; `git diff --check` sem problemas. A execução recompilou as dependências e apresentou apenas avisos de analisadores/XML já existentes, sem erro de compilação ou teste. A suíte completa de Implementations terminou com 1 falha intermitente já conhecida em `NebulaPlaybackCacheTests.DisposingCacheDoesNotWaitForBlockingSharedFetchCancellationCallback` (1052 aprovados, 39 ignorados); a repetição isolada desse teste passou (1/1), portanto a falha não foi atribuída ao lockout.
- Limite: T6.5 permanece **parcial**. Ainda faltam auditoria/validação de binding administrativo em rede local/remota e exposição de endpoints internos de diagnóstico/telemetria. Nenhuma release foi criada/publicada.
### 29/09/2026 — Estado de erro com retry na página de detalhes (W1.1 parcial)

- INTENT: `controllers/itemDetails/index.ts` (a página de detalhes de item, legado não-React) não tinha nenhum estado de erro visível — quando `loadData()` falhava (item inexistente, erro de rede/servidor), o único efeito era `console.error`; o spinner de `loading.withLoading` desaparecia e a página ficava permanentemente em branco, exatamente a falha que W1.1 e W1.4 pedem para eliminar.
- Adicionado bloco `#itemDetailLoadError` ao template (`index.html`) com mensagem e botão "Retry"; `showLoadError()`/`hideLoadError()` alternam a visibilidade entre esse bloco e o conteúdo principal (`detailPageWrapperContainer`, `detailLogo`). O botão de retry rechama `loadData()`, que já limpa o estado de erro no início (`hideLoadError()`) antes de tentar de novo — mesmo padrão de retry usado nos componentes React (`LoadErrorMessage`).
- Validação: `tsc --noEmit` exit 0; `npx eslint` no arquivo não introduziu nenhum erro novo (o arquivo já tinha 48 erros pré-existentes de `any`/complexidade cognitiva, todos anteriores a esta mudança — confirmado que `npm run lint:changed` já exclui esse arquivo da baseline por ser legado); suíte completa `npm test -- --run`: 28 arquivos, 217 testes, 0 falhas, contagem inalterada (não há precedente de teste automatizado para `controllers/`, diretório 100% legado sem nenhum `.test.ts`). `npm run build:production`: exit 0. `npm run verify:build`: 1.897 artefatos válidos, inalterado.
- Limite: não há teste automatizado cobrindo esse comportamento especificamente (ausência de infraestrutura de teste DOM para módulos legados fora de React); a validação foi por leitura de código e pelos checks de build/lint/typecheck. W1.1 permanece **parcial** — home, busca, solicitações e telas do painel ainda não tiveram auditoria completa de todos os estados (skeleton, offline/degradado); apenas o "erro sem retry" da página de detalhes foi corrigido. Nenhuma release foi criada/publicada.

### 29/09/2026 — Cobertura de teste para CORS/TLS/CSP já corrigidos (T6.5 parcial)

- INTENT: `SECURITY_AUDIT_REPORT.md` documenta CORS, TLS e CSP como "FIXED", mas apenas CSP (`SecurityHeadersMiddlewareTests`) tinha teste de regressão real; `CorsPolicyProvider` — o componente que corrigiu a falha crítica CVSS 7.5 do relatório — não tinha nenhum teste, então uma futura regressão (reintroduzir `AllowAnyOrigin().AllowCredentials()` por engano) passaria silenciosamente por toda a suíte.
- Adicionado `tests/Jellyfin.Server.Tests/Configuration/CorsPolicyProviderTests.cs` (4 testes): hosts vazios e curinga isolado (`["*"]`) produzem política sem origens explícitas e `SupportsCredentials == false`; hosts explícitos produzem política restrita exatamente a esses hosts com credenciais permitidas; `AllowAnyMethod`/`AllowAnyHeader` sempre presentes (seguro porque a restrição real é por origem, não por método/header). `SECURITY_AUDIT_REPORT.md` atualizado para referenciar esse teste como evidência viva da correção.
- Testes focados: 4/4 aprovados. Suíte completa `Jellyfin.Server.Tests` Release: 35/35 aprovados (31 + 4), 0 falhas.
- Verificado por leitura direta que TLS mínimo 1.2 (`WebHostBuilderExtensions.cs:98,122`) e a CSP restrita sem origens amplas de terceiros (`SecurityHeadersMiddleware.cs`) continuam como o relatório descreve — nenhuma regressão encontrada nesses dois itens.
- Limite: T6.5 continua **parcial**. Rede local/remota (isolamento de bind de endpoints administrativos a loopback/LAN), políticas de conta administrativa (força de senha, lockout) e exposição de endpoints internos de diagnóstico/telemetria não foram auditados nesta etapa. Nenhuma release foi criada/publicada.

### 29/09/2026 — Auditoria de autorização por endpoint: solicitações, Nebula, backups, diagnósticos e proteção cruzada de userId (T6.1 parcial)

- INTENT: cobrir a auditoria mínima exigida pelo aceite de T6.1 ("solicitações, reports, uploads, operação Nebula, cache, backups e diagnósticos") e verificar a proteção cruzada de `userId` — o vetor clássico de IDOR em APIs REST que expõem dados por usuário.
- Auditoria de decoradores `[Authorize]`/`[AllowAnonymous]` em ~50 controllers (`Jellyfin.Api/Controllers`): todos os controllers nas áreas nomeadas (`UserFeedbackController`, `NebulaFtpController`, `BackupController`, `ServerHealthController`, `PlaybackReportsController`, `DashboardController`, `DevicesController`, `ActionLogController`, `PackageController`, `ScheduledTasksController`, `ActivityLogController`) já exigem `[Authorize]` — a maioria com `Policies.RequiresElevation` — seja no nível do controller ou de cada ação individualmente. `BrandingController` é o único sem `[Authorize]`, mas expõe apenas configuração de tema/CSS pública por design (usada na tela de login antes da autenticação).
- Auditoria do guard cruzado de `userId`: `RequestHelpers.GetUserId(ClaimsPrincipal, Guid?)` é chamado por todo endpoint identificado que aceita um `userId` opcional (`ItemsController`, `PlaystateController`, `DisplayPreferencesController`, entre outros); ele lança `SecurityException` quando um usuário não administrador informa um `userId` diferente do seu próprio. Verificado especificamente que as rotas legadas por segmento de rota (`Users/{userId}/PlayedItems/{itemId}`, `Users/{userId}/Items`) delegam para os métodos que já contêm esse guard (`MarkPlayedItemLegacy` chama `MarkPlayedItem`, que roda `RequestHelpers.GetUserId` internamente) — não é possível contornar a checagem usando a rota antiga.
- Este comportamento já tinha cobertura de teste prévia (`RequestHelpersTests.GetUserId_IsUser` espera `SecurityException`); nenhum teste novo foi necessário porque a auditoria não encontrou brechas — apenas confirmou uma proteção já existente e já testada.
- Suíte `Jellyfin.Api.Tests` Release (sem alteração de produção nesta auditoria): 193/193 aprovados, 0 falhas — reexecutada apenas para confirmar que a sessão de leitura de código não deixou nenhuma edição acidental.
- Limite: a auditoria foi só leitura de código, sem teste de penetração automatizado nem exercício real do servidor rodando. `SessionController`, `DynamicHlsController` e `MediaInfoController` (endpoints de sessão/streaming) e a revisão individual de todas as ações com `[Authorize(Policy = ...)]` específico não foram cobertas nesta etapa — ficam pendentes. T6.1 permanece **parcial**. Nenhuma release foi criada/publicada.

### 29/09/2026 — Auditoria de autorização de sessão e streaming (T6.1 parcial)

- Auditoria focada em `SessionController`, `DynamicHlsController` e `MediaInfoController`.
- `SessionController` teve todos os métodos públicos roteados verificados por reflexão; cada método com atributo HTTP possui `[Authorize]`. `DynamicHlsController` e `MediaInfoController` possuem `[Authorize]` no nível do controller, herdado por todas as ações. Nenhuma dessas três superfícies contém `[AllowAnonymous]`.
- Adicionado `SecurityAuthorizationTests.SessionStreamingEndpoints_RequireAuthorization`, que verifica as rotas do controller de sessão e a autorização herdada dos controllers de mídia/streaming.
- Validação: teste focado aprovado; suíte completa `Jellyfin.Api.Tests` Release: 211/211 aprovados; `dotnet build MulletaFlix.sln -c Release --no-restore`: 0 erros.
- Limite: T6.1 permanece **parcial**. Ainda falta revisar individualmente todas as ações elevadas fora das áreas auditadas e executar fuzzing/teste de autorização cruzada com o servidor em execução. Nenhuma release foi criada/publicada.

### 29/09/2026 — `Retry-After` nas respostas 429 de rate limit (T6.2 parcial)

- INTENT: o aceite de T6.2 exige `429`/`Retry-After` — `RateLimitMiddleware` já retornava `429 Too Many Requests` para login e tráfego anônimo, mas nunca enviava o cabeçalho `Retry-After`, deixando o cliente sem indicação de quando tentar novamente (RFC 9110 §10.2.3).
- `RateLimitEntry.IsBlocked` agora calcula o tempo restante até a entrada mais antiga da janela expirar (`oldest + window - now`), arredondado para cima em segundos inteiros e nunca menor que 1; o valor é escrito em `context.Response.Headers.RetryAfter` nos dois caminhos bloqueados (login e anônimo geral), cada um com sua própria janela (15 min / 10 s).
- Testes novos: `AnonymousRequests_BlockedResponse_IncludesRetryAfterHeader` (cabeçalho presente, valor entre 1 e 10s — a janela anônima) e `LoginAttempts_BlockedResponse_IncludesRetryAfterHeader` (cabeçalho presente, valor entre 1 e 900s — a janela de login). Suíte focada `RateLimitMiddlewareTests`: 26/26 aprovados (24 + 2 novos). Suíte completa `Jellyfin.Api.Tests` Release: 193/193 aprovados (191 + 2), 0 falhas. Build Release do servidor: 0 erros.
- Limite: T6.2 continua **parcial**. O aceite também pede diferenciação de limites para busca, ações administrativas caras e uploads/downloads Nebula — hoje `RateLimitMiddleware` só distingue login de "todo o resto anônimo"; usuários autenticados não têm limite algum aplicado (rota deliberada do design atual, mas fora do escopo original do item). Esse trabalho de segmentação fica para uma próxima etapa. Nenhuma release foi criada/publicada.

### 29/09/2026 — Canonicalização de caminhos na renomeação de bibliotecas (T6.3 parcial)

- INTENT: `LibraryStructureController.RenameVirtualFolder` construía `currentPath` e `newPath` diretamente com `Path.Combine(DefaultUserViewsPath, name)`. Embora a ação exigisse setup/elevated, nomes contendo `..` poderiam resolver fora da raiz esperada e alcançar uma operação de `Directory.Move` sobre caminho externo.
- Correção: adicionada `IsPathWithinRoot`, que compara caminhos completos com separador de diretório e comparação adequada à plataforma. A ação rejeita com `400 Bad Request` qualquer nome que escape da raiz, aponte para a raiz ou apenas compartilhe prefixo textual com ela.
- Testes novos em `LibraryStructureControllerPathTests`: descendente válido, traversal por pai, raiz exata e diretório irmão com prefixo compartilhado. Teste focado: 4/4 aprovados.
- Validação: suíte completa `Jellyfin.Api.Tests` Release: 210/210 aprovados; `dotnet build MulletaFlix.sln -c Release --no-restore`: 0 erros.
- Limite: T6.3 permanece **parcial**. Symlinks/junctions, validação de tipo/extensão e outros endpoints que aceitam caminhos físicos ainda precisam de auditoria. Nenhuma release foi criada/publicada.

### 29/09/2026 — Rate limits seletivos para busca, administração e Nebula (T6.2 parcial)

- INTENT: concluir a diferenciação mínima exigida por T6.2 sem aplicar limite às rotas normais de reprodução. O middleware anterior só limitava login e tráfego anônimo geral; rotas caras de busca, administração e operações Nebula ficavam sem bucket próprio, inclusive para usuários autenticados.
- Correção: `RateLimitMiddleware` agora classifica `/Search` e `/Items/RemoteSearch` como busca (60/10 s por IP), `/ActivityLog`, `/Backup`, `/Configuration`, `/Dashboard`, `/Environment`, `/Plugins`, `/ScheduledTasks`, `/ServerHealth` e `/System` como administração (20/10 s), e `/NebulaFtp` como Nebula (10/10 s). Cada categoria tem armazenamento separado, retorna `429` com `Retry-After` e preserva as isenções de loopback e bootstrap público.
- Testes adicionados em `RateLimitMiddlewareTests`: classificação com limite de segmento de rota e bloqueio de uma sequência autenticada de operações Nebula após 10 requisições. A execução também encontrou e corrigiu uma regressão em que `/System/Info/Public` seria classificado como administração; bootstrap público continua fora dos buckets seletivos.
- Validação: suíte focada `RateLimitMiddlewareTests`: 33/33 aprovados; suíte completa `Jellyfin.Api.Tests` Release: 206/206 aprovados; `dotnet build MulletaFlix.sln -c Release --no-restore`: 0 erros.
- Limite: T6.2 permanece **parcial**. Os limites são por IP, sem controle específico de concorrência por worker/operação ou calibração com carga real. Nenhuma release foi criada/publicada.

### 29/09/2026 — Endpoint "minhas solicitações" e página de status do usuário (W1.2 avança; ainda parcial)

- INTENT: fechar as lacunas restantes de W1.2 identificadas na entrada anterior. Investigação encontrou que `components/userFeedback/userFeedback.ts` já implementa autocomplete com debounce (250ms), cancelamento de corrida por sequência de requisição, navegação por teclado (setas/Enter/Escape), ARIA combobox completo e confirmação via toast — e já está conectado tanto em `UserViewNav.tsx` (botão "Solicitar mídia" ao lado do botão "Favoritos" na toolbar, visível para qualquer usuário autenticado) quanto em `hometab.ts`. O que faltava de fato: (1) nenhum endpoint retornava as solicitações do próprio usuário — só o admin via `ActivityLogController` (`RequiresElevation`); (2) não havia página alguma para o usuário ver o status/posição de suas solicitações nos grids pendente/incluído.
- Backend: adicionado `ActivityLogQuery.UserId` (filtro exato, distinto de `HasUserId` que só testa presença) e o `Where` correspondente em `ActivityManager.GetPagedResultAsync`. Novo endpoint `GET UserFeedback/MediaRequests` em `UserFeedbackController` (autenticado, sem elevação) retorna `QueryResult<ActivityLogEntry>` filtrado por `User.GetUserId()` e `Type == "MediaRequest"`, com `limit` clamped entre 1 e 500.
- Frontend: `mediaRequestUtils.ts` (antes acoplado à rota dashboard) foi movido para `src/utils/mediaRequests.ts` — local neutro, já que `apps/dashboard` e `apps/experimental` não podem importar um do outro. `UserFeedbackListPage.tsx` (admin) atualizado para importar do novo caminho, sem mudança de comportamento. Novo hook `hooks/api/useMediaRequests.ts` (`useMyClassifiedMediaRequests`) reusa a mesma classificação normalizada de título/categoria/ano para separar pendente/incluído. Nova página `apps/experimental/routes/myrequests.tsx` ("Minhas solicitações", rota `/myrequests`) mostra os dois grids com loading/erro-com-retry/vazio tratados explicitamente. Novo botão de navegação em `UserViewNav.tsx`, ao lado do botão de solicitar, linka para essa página.
- Testes .NET novos: `ActivityManagerTests` (2 testes: filtro por `UserId` isola só as entradas do solicitante e se combina com `Type`) + `UserFeedbackControllerTests` (2 testes: `GetMyMediaRequests` usa `User.GetUserId()`/`Type=MediaRequest` e faz *clamp* de `limit` fora do intervalo). Suíte `Jellyfin.Server.Implementations.Tests` Release: 1.050 aprovados (1.048 + 2), 0 falhas. Suíte `Jellyfin.Api.Tests` Release: 191 aprovados (185 + 6, incluindo os 4 testes pré-existentes de `UserFeedbackController` e os 2 novos), 0 falhas.
- Testes web: `mediaRequests.test.ts` (movido, mesmos 5 casos) — 5/5 aprovados isoladamente. Suíte completa `npm test -- --run`: 28 arquivos, 217 testes aprovados, 0 falhas (contagem idêntica à entrada anterior porque o arquivo foi movido 1:1, não duplicado). `npm run build:check` (tsc --noEmit): exit 0. `npx eslint` nos 7 arquivos tocados: exit 0, sem violações. `npm run build:production`: exit 0. `npm run verify:build`: 1.897 artefatos válidos (1.895 → 1.897, dois arquivos novos: `myrequests` chunk e hook), limite individual de 1.536 KiB respeitado.
- Limite: não há teste Playwright/`@testing-library/react` exercitando a renderização real da página `myrequests.tsx` nem o clique no botão da toolbar (dependência não instalada, decisão anterior de não adicionar sem aprovação permanece). O item "posição/prioridade" do aceite (mostrar a posição na fila de download, não só pendente/incluído) não foi implementado — `NebulaDownloadStatusDto.QueuePosition` existe no backend mas não está exposto por `UserFeedback/MediaRequests`; ficaria por conta de correlacionar a fila do Nebula com o título do `ActivityLog`, o que não existe hoje como link direto (a fila referencia caminhos de arquivo, não o texto livre da solicitação). Também não há teste de acessibilidade automatizado para a nova página, embora ela siga o mesmo padrão MUI/`Alert`/`Loading` já usado em `userprofile.tsx`.
- W1.2 permanece **parcial**: autocomplete/debounce/cancelamento, botão próximo a Favoritos, grids pendente/incluído e confirmação do envio estão implementados e testados. Falta apenas o indicador de posição/prioridade na fila, que exige correlacionar `ActivityLog` (texto livre) com `NebulaDownloadStatusDto.QueuePosition` (caminho de arquivo) — trabalho de rastreamento adicional no backend do Nebula, não coberto nesta etapa. Nenhuma release foi criada/publicada.

### 28/09/2026 — Diagnóstico do cache de reprodução concluído (T3.5)

- A API e o painel expõem tamanho/arquivos, hits/misses, leases, downloads Telegram (incluindo falha e cancelamento), latência média, pré-cache ativo/em fila, erros de persistência, e execução/falha/contenção/duração/data da última limpeza.
- Testes Release executados nesta validação: `NebulaPlaybackCacheTests` — 36 aprovados; `NebulaFtpControllerTests` — 23 aprovados. Cobrem contadores, falha e recuperação de fetch, contenção de limpeza e serialização JSON camelCase dos novos campos.
- T3.5 concluída. T3.6 permanece aberta para falhas e recuperação integrada de reprodução; nenhuma release foi criada/publicada.

## Backlog futuro — fora do ciclo ativo

- Curadoria por IA local ou remota para nomes, imagens e NFO: não iniciar desenvolvimento neste ciclo. Reavaliar apenas após as tarefas determinísticas de catálogo, proveniência, aprovação e rollback serem concluídas e o usuário autorizar um novo escopo.

## Indicadores de sucesso do roadmap

- Nenhuma fila sem progresso sem alerta; retomada após reinício validada.
- Zero duplicações em reprocessamento e deduplicação Nebula.
- Menos pausas de reprodução medidas em sessões equivalentes, sem aumentar indefinidamente o uso de disco.
- Backup validado por restauração periódica, não apenas por upload concluído.
- Erros de API e UI diagnosticáveis por correlação sem exposição de segredos.
- Tarefas críticas do usuário concluíveis em celular, desktop e TV; nenhum grid essencial renderizado em branco sem estado de erro.
- Metas de Web Vitals, bundle e acessibilidade medidas e acompanhadas por rota.
- Curadoria por IA permanece opcional, auditável e reversível; precisão demonstrada em conjunto rotulado antes de qualquer automação.

### 28/09/2026 — Identidade canônica do cache de playback (T3.1 parcial)

- Chaves de ObjectId MongoDB agora são normalizadas sem distinção de caixa; caminhos absolutos são normalizados (`.`/`..`) com regras de caixa do sistema operacional e ficam em namespace separado de IDs e chaves opacas.
- Cache rejeita identidade ausente em vez de agrupar tudo em `unknown-media`; cada stream sem identidade estável recebe chave própria para impedir reutilização de bytes de outra stream.
- Testes específicos: 3/3 aprovados. Suíte completa `Jellyfin.Server.Implementations.Tests` em Release: 1.022 aprovados, 38 ignorados, 0 falhas. Build Release do servidor concluído com 0 erros. Persistem avisos NU1903 de Newtonsoft.Json 9.0.1.
- T3.1 continua parcial até revisão global do contrato e evidência dos cenários restantes. Nenhuma mídia/DB de produção foi acessada e nenhuma release foi criada/publicada, conforme decisão do usuário.

### 28/09/2026 — Backup de usuários não mascara origem/tabela ausente (T4.2 parcial)

- As rotinas separadas de backup/restauração de usuários podiam retornar sucesso com zero usuários quando faltava `IDbContextFactory<UsersDbContext>`, a tabela `mulletaflix_users` não existia ou o Supabase rejeitava a leitura. Agora retornam falha explícita, sem marcar operações não realizadas como concluídas.
- Testes locais usam EF InMemory e handler HTTP controlado; não acessam MongoDB, Supabase ou dados reais. Faltam ainda verificação dos lotes, snapshot consistente e teste isolado de restauração.
- Verificação Release: 4 testes novos; suíte `Jellyfin.Server.Implementations.Tests` com 1.026 aprovados/38 ignorados e 0 falhas; build Release do servidor com 0 erros; `git diff --check` passou. Avisos NU1903 preexistentes de Newtonsoft.Json 9.0.1 permanecem.
- Nenhuma release foi criada/publicada; todas as melhorias ativas do roadmap continuam sendo pré-requisito da release final.

### 28/09/2026 — Falhas na reconciliação remota invalidam o backup (T4.2 parcial)

- Erros ao listar usuários FTP/MulletaFlix remotos, respostas fora do formato JSON esperado e exclusões remotas rejeitadas agora propagam falha para o resultado.
- Mantida a proteção de não excluir registros remotos quando a lista local está vazia. Exclusões já concluídas antes de uma falha não são revertidas; a próxima execução pode reconciliar novamente com segurança.
- Testes focados `NebulaSupabaseSyncTests`: 12 aprovados; suíte completa `Jellyfin.Server.Implementations.Tests` Release: 1.028 aprovados, 38 ignorados, 0 falhas. Build Release do servidor: 0 erros. `git diff --check` passou (avisos apenas de conversão LF/CRLF do checkout).
- T4.2 permanece parcial: snapshot consistente, criptografia, retenção/rotação e exercícios de restauração ainda não foram implementados. Nenhuma release foi criada/publicada, conforme decisão do usuário.

### 28/09/2026 — Primeiro teste isolado de restauração de usuários (T4.3 parcial)

- Adicionado teste de restauração relacional em EF InMemory isolado por teste. Confirma usuário persistido e consultável por ID, campos restaurados, contagem, duração finita/não negativa, status de sucesso e chamada remota simulada.
- Testes focados `NebulaSupabaseSyncTests`: 13 aprovados; suíte completa `Jellyfin.Server.Implementations.Tests` Release: 1.029 aprovados, 38 ignorados, 0 falhas. Build Release do servidor: 0 erros. `git diff --check` passou; somente avisos de conversão LF/CRLF no checkout.
- T4.3 continua parcial: EF InMemory não valida comportamento do provider MariaDB; recuperação MongoDB nem contagens/consultas críticas do conjunto completo ainda não foram testadas. Nenhuma release criada/publicada.

### 28/09/2026 — Backup FTP falha quando Supabase rejeita lote (T4.2 parcial)

- O backup MongoDB podia ignorar uma resposta HTTP não bem-sucedida ao enviar usuários FTP e continuar como sucesso com contagem zero. O envio agora lança erro; contagem só é informada após resposta 2xx.
- Testes focados `NebulaSupabaseSyncTests`: 15 aprovados, incluindo HTTP 503 e sucesso HTTP 201; suíte completa Implementations Release: 1.031 aprovados, 38 ignorados, 0 falhas. Build Release do servidor: 0 erros. `git diff --check` passou; avisos restantes são NU1903 preexistente e conversão LF/CRLF.
- T4.2 permanece parcial: consistência por snapshot, criptografia, retenção/rotação e prova completa de restauração continuam abertas. Nenhuma release foi criada/publicada.
- TWINS: a busca por condicionais `if (resp*.IsSuccessStatusCode)` sem ramo de falha encontrou dois fluxos de restauração em `PerformRestoreAsync` (usuários FTP e tokens de bots, linhas próximas a 678 e 727); ambos seguem explicitamente pendentes em T4.2.

### 28/09/2026 — Restauração MongoDB não mascara falhas HTTP/JSON (T4.2 parcial)

- Leituras rejeitadas ou respostas que não sejam arrays JSON nas tabelas de usuários FTP e tokens agora fazem a restauração geral falhar; erros são preservados no resultado. Tabelas vazias com resposta válida continuam aceitas.
- Testes focados `NebulaSupabaseSyncTests`: 18 aprovados, 0 falhas; cobertura para HTTP 503 em cada tabela e sucesso com tabelas vazias. Suíte completa `Jellyfin.Server.Implementations.Tests` Release: 1.034 aprovados, 38 ignorados, 0 falhas. Build Release do servidor: 0 erros (3 avisos NU1903 preexistentes de Newtonsoft.Json 9.0.1); `git diff --check` passou com avisos de conversão LF/CRLF no checkout.
- T4.2 permanece parcial: snapshot consistente, criptografia, retenção/rotação e exercícios de recuperação ainda abertos. Nenhuma release criada/publicada.
- TWINS: busca `rg -n -U "if \\(resp(Users|Tokens)\\.IsSuccessStatusCode)[\\s\\S]{0,800}?\\n\\s*\\}" Jellyfin.Server.Implementations/Nebula/NebulaSupabaseSyncService.cs` encontrou 0 ocorrências; as duas leituras correspondentes agora têm ramo explícito de falha.

### 28/09/2026 — Histórico rejeitado invalida backup MongoDB (T4.2 parcial)

- INTENT: `PerformBackupAsync` só atualiza `LastSuccessfulBackupTime` e retorna sucesso depois do Supabase confirmar também a gravação de `nebula_backups`; erro HTTP ou de transporte propaga para o resultado de falha.
- Testes focados `NebulaSupabaseSyncTests`: 20 aprovados, incluindo 503 rejeitado e 201 aceito para o histórico. Suíte completa `Jellyfin.Server.Implementations.Tests` Release: 1.036 aprovados, 38 ignorados, 0 falhas. Build Release do servidor: 0 erros; 3 avisos NU1903 preexistentes do Newtonsoft.Json 9.0.1. `git diff --check` passou com avisos de conversão LF/CRLF.
- T4.2 continua parcial: snapshot consistente, criptografia, retenção/rotação, integridade e exclusões remotas seguem abertas. Nenhuma release foi criada/publicada; todas as melhorias ativas do roadmap continuam como pré-requisito da release final.
- TWINS: rastreamento do `SendAsync` que escrevia em `nebula_backups` encontrou resposta descartada; o caminho agora verifica `IsSuccessStatusCode` e lança apenas o código HTTP (sem ecoar o corpo remoto) para que o fluxo externo marque o backup como falho.

### 28/09/2026 — Backup geral só publica arquivo ZIP íntegro (T4.2 parcial)

- INTENT: o caminho final do backup só pode apontar para um arquivo fechado e legível; geração interrompida fica isolada como `.partial` e nome com timestamp de milissegundos + GUID evita colisões concorrentes.
- Implementado: após fechar o ZIP temporário, o servidor reabre e percorre todas as entradas antes de promover com `File.Move`; falhas removem apenas o temporário, sem apagar um backup anterior válido.
- Testes focados `BackupServiceTests`: 9 aprovados, incluindo arquivo legível e arquivo truncado. Eles exercitam o validador, mas ainda não geram um backup integral com todos os contextos; essa integração fica pendente em T4.3. Suíte completa `Jellyfin.Server.Implementations.Tests` Release: 1.038 aprovados, 38 ignorados, 0 falhas. Build Release do servidor: 0 erros e 3 avisos NU1903 preexistentes de Newtonsoft.Json 9.0.1. `git diff --check` passou; avisos restantes são conversão LF/CRLF já presente no checkout.
- T4.2 continua parcial: falta snapshot consistente entre contextos/fontes, criptografia, checksums autenticados, retenção/rotação e reconciliação remota. Nenhuma release criada/publicada, conforme o gate do roadmap.
- TWINS: inspeção adversarial de `File.OpenWrite(backupPath)` e nome com precisão de segundos encontrou escrita diretamente no destino final, potencialmente visível antes de concluída e sujeita a colisão; geração temporária validada + promoção atômica corrige o caminho sem apagar o arquivo anterior.

### 28/09/2026 — Restauração de arquivos verificada em instância temporária (T4.3 parcial)

- Teste chama `BackupService.RestoreBackupAsync` com um ZIP fixture sem banco e configurações de destino exclusivas em `%TEMP%`; confirma três arquivos restaurados, lê o JSON do usuário e da coleção, valida o NFO e mede duração finita.
- Quality bar parcial: fluxo real de extração e consultas de conteúdo passaram, sem acessar dados de produção. Ainda não prova tabelas relacionais, contexto Mongo, nem criação/restauração completa do ZIP; MariaDB descartável e ciclo completo permanecem pendentes em T4.3.
- Teste focado `RestoreBackupAsync_RestoresFilesIntoAnIsolatedInstanceAndReportsQueryableContent`: 1 aprovado; classe `BackupServiceTests`: 10 aprovados. Suíte completa `Jellyfin.Server.Implementations.Tests` Release: 1.039 aprovados, 38 ignorados, 0 falhas. Build Release do servidor: 0 erros e 3 avisos NU1903 preexistentes de Newtonsoft.Json 9.0.1. Nenhuma release criada/publicada.

### 28/09/2026 — Painel operacional de backup e validação de integridade (T4.4 parcial)

- INTENT: `A validação de backup deve chamar o GET /System/Backup/Validate?path=...; o painel deve mostrar o último resultado e o agendamento configurado com estados de erro legíveis.`
- A tela de backups agora resume resultado/erro da tarefa agendada, próximo disparo calculado a partir dos triggers diário, semanal ou intervalo, e data/destino/opções/tamanho do backup mais recente. O diálogo de detalhes permite validar a integridade e distingue arquivo válido, inválido e falha da chamada.
- `BackupManifestDto.SizeBytes` é derivado do tamanho do ZIP no disco. Teste isolado compara o campo com `FileInfo.Length` do fixture; cliente web aceita ausência do campo para compatibilidade com servidor antigo.
- Conteúdos mostrados no resumo/detalhes agora vêm somente dos flags presentes no manifesto; o caso `Database=false` não é mais apresentado como incluído.
- A validação manual percorre cada entrada ZIP e compara CRC-32 e tamanho expandido com o diretório central. Regressão corrompe um payload mantendo o manifesto legível e confirma retorno inválido. CRC incremental usa buffer pooled e limpo ao retornar, com memória limitada.
- Corrigido contrato incompatível da ação: cliente enviava `POST` com corpo enquanto o controller expõe `GET` com `path` na query.
- Direção visual: painel administrativo utilitário, mantendo MUI/tema existentes; o acento lateral e o chip de estado formam a âncora operacional. DFII subjetivo: 16 (impacto 2 + adequação 5 + viabilidade 5 + desempenho 5 − risco de consistência 1). Não é comprovação de usabilidade.
- Verificação: testes focados de agenda/conteúdo 5/5; servidor rejeita payload corrompido e informa tamanho manifesto; suíte `Jellyfin.Server.Implementations.Tests` Release 1.041 aprovados/38 ignorados/0 falhas; suíte `Jellyfin.Common.Tests` 31/31; build Release do servidor 0 erros (3 avisos NU1903 conhecidos do Newtonsoft.Json 9.0.1). Suíte web 213 testes/27 arquivos; `npm run build:check`, `npm run lint:changed` e `npm run build:production` concluídos com código 0. Build web mantém avisos conhecidos de diretivas `use client` e chunks/importações do Vite; sem erro. Não houve inspeção visual interativa por navegador nesta rodada.
- T4.4 permanece parcial: próxima execução ainda é estimada no fuso do navegador; escopo/cobertura real do destino e exercício de restauração isolada com relatório seguem pendentes. Validar CRC e o botão protegido de restaurar não comprovam recuperação real.
- Nenhuma release foi criada ou publicada. O gate definido pelo usuário permanece: concluir todas as melhorias ativas do roadmap antes da release.

### 28/09/2026 — Próxima execução calculada no backend (T4.4 parcial)

- O contrato `TaskInfo` agora inclui `NextExecutionTimeUtc`; o servidor calcula schedules diários/semanais usando o relógio e fuso locais do host. Agendas de intervalo, startup, inválidas ou mistas retornam `null` deliberadamente, pois a próxima execução real não pode ser inferida com fidelidade só pelos triggers serializados.
- O painel parou de calcular próxima execução com relógio do navegador e consome o instante UTC mais o offset do servidor naquela data; exibe a hora civil do host com o offset explícito, sem conversão pelo timezone do cliente.
- Testes cobrem horário diário futuro, disparo diário no instante exato, diário vencido, dia da semana configurado, triggers não suportados/inválidos e retorno UTC. Testes web verificam renderização do horário civil do host com offset, timestamp válido e valores ausentes/malformados.
- Quality bar: `Jellyfin.Model.Tests` Release 663 aprovados; `Jellyfin.Server.Implementations.Tests` Release 1.041 aprovados/38 ignorados; build Release do servidor 0 erros (avisos existentes NU1903 e warnings XML); suíte web 212 testes/27 arquivos; `npm run build:check`, `npm run lint:changed` e `npm run build:production` código 0 (avisos Vite existentes). Nenhuma release criada/publicada.
- TWINS: comparação adversarial com `DailyTrigger.Start` e `WeeklyTrigger.GetNextTriggerDateTime` cobriu disparo exatamente no limite, data/hora vencida, virada semanal e fuso/offset; schedules por intervalo não são estimáveis sem `_lastStartDate`, por isso o contrato retorna indisponível em vez de um valor presumido.

### 28/09/2026 — Escopo do backup e resolução de volume (T4.2/T4.4 parcial)

- INTENT: `Database=false` precisa produzir um ZIP sem tabelas/otimização do MariaDB, e a validação de espaço deve reconhecer caminhos absolutos Windows/Unix sem confundir unidade ou volume.
- Corrigido `StorageHelper.ResolvePath`: agora canonicaliza o caminho, inicia a caminhada na raiz retornada pelo sistema operacional e mantém resolução de links; em Windows não descarta mais `C:\`/UNC. O teste detectou o defeito indiretamente porque o fluxo real de backup recusava uma unidade com espaço suficiente devido a `FreeSpace=-1`.
- `CreateBackupAsync` agora só otimiza e lê `MulletaFlixDbContext` quando `Database=true`. Quando desabilitado, manifesta `DatabaseTables=[]`; o teste lê o ZIP criado e verifica ausência de `Database/`, flags corretos, lista vazia e nenhuma chamada ao factory/otimização.
- Testes focados `CreateBackupAsync_WhenDatabaseIsExcluded_DoesNotReadOrOptimizeDatabase` e `GetFreeSpaceOf_ResolvesAbsolutePathFromItsVolumeRoot`: 2/2 aprovados. Suíte completa `Jellyfin.Server.Implementations.Tests` Release: 1.043 aprovados, 38 ignorados, 0 falhas. Build Release do servidor: 0 erros, 3 avisos NU1903 conhecidos de Newtonsoft.Json 9.0.1.
- T4.2 segue parcial: consistência transacional entre fontes, criptografia, retenção/rotação e política de remoção remota ainda pendentes. O teste de restore cobre apenas MariaDB; MongoDB/arquivos e recuperação remota continuam sem prova ponta a ponta.
- Nenhuma release criada/publicada. O gate exige concluir todas as melhorias ativas do roadmap antes da release.

### 28/09/2026 — Ciclo completo de backup/restore MariaDB (T4.3 parcial)

- INTENT: provar backup e restauração relacional real sem se conectar ao banco ativo na porta `3306`.
- Foi iniciada uma instância descartável MariaDB 11.4 no diretório temporário dedicado e porta `13306`; a connection string do teste é explicitamente configurada e o teste gera nome de schema aleatório, remove somente esse schema ao final e limpa seu diretório de fixtures.
- A primeira execução encontrou falha concreta: migration `20260718183000_AddMidiaStorageOnlineMediaMetadata` não tinha atributos de registro EF Core, portanto a tabela não era criada mesmo após `MigrateAsync`; a enumeração das tabelas pelo backup então falhava com “table doesn't exist”. A migration agora declara explicitamente `DbContext` e id, e a execução subsequente confirmou o caminho.
- O teste `RestoreBackupAsync_RestoresDatabaseRowsOnDisposableMariaDb` cria a tabela via migrations, insere fixture, gera backup real com DB, apaga a fixture e chama `RestoreBackupAsync`; consulta posterior confirmou que o registro e seu tipo foram restaurados. Resultado focado: 1/1 aprovado. Suíte `Jellyfin.Server.Implementations.Tests`: 1.043 aprovados, 38 ignorados, 0 falhas; build Release do servidor: 0 erros e 3 avisos NU1903 preexistentes.
- O teste recusa explicitamente host fora do loopback ou porta padrão `3306`; só cria schema com nome aleatório após configuração explícita. Cobertura inclui as duas recusas e o ciclo real em MariaDB isolado `13306`: 3/3 aprovados.
- A instância descartável foi encerrada; verificações confirmaram que a porta `13306` não está escutando e a porta `3306` continua pertencendo ao processo anterior (PID 6512). O diretório temporário do datadir permanece parado: a política do ambiente bloqueou a remoção recursiva, então não forcei outra forma de exclusão. A pasta de fixtures de restore foi removida pelo próprio teste.
- Ainda parcial: o teste cobre MariaDB apenas; MongoDB e demais arquivos/conteúdos remotos, contagens completas, duração observável e cenário de falha permanecem pendentes. A suite MariaDB fixa existente não foi executada porque ela usa a porta ativa `3306` e elimina schema estático.
- Nenhuma release criada/publicada. O gate exige concluir todas as melhorias ativas do roadmap antes da release.

### 28/09/2026 — Contrato de respostas da restauração Nebula (T4.2/T4.3 parcial)

- Adicionados testes para garantir que respostas HTTP 2xx com JSON no formato errado nas tabelas `nebula_users` e `nebula_bot_tokens` invalidam a restauração e não atualizam `LastSuccessfulRestoreTime`.
- Quality gate focado `MongoRestoreFailsWhen`: 4 aprovados, 0 falhas. Testes usam handler HTTP controlado, EF InMemory e nenhum acesso ao MongoDB, Supabase real ou dados de produção.
- Isso cobre validação do contrato remoto, não prova persistência MongoDB. O restore completo remoto permanece pendente em T4.3; sem release até o término de todas as melhorias ativas do roadmap.

### 28/09/2026 — Remoção de Newtonsoft.Json vulnerável no runtime (T6.4 parcial)

- INTENT: eliminar o `Newtonsoft.Json 9.0.1` ainda resolvido por dependências legadas sem aplicar uma substituição global a todas as dependências transitivas.
- O grafo apontou duas origens: `FubarDev.FtpServer` (via `Scrutor`/`Microsoft.Extensions.DependencyModel`) e `Microsoft.AspNetCore.Mvc.Core 2.3.0` no plugin GetAvatar. Ambos os projetos agora referenciam explicitamente a versão central `13.0.3`; o advisory oficial marca versões anteriores a `13.0.1` como afetadas por DoS [GHSA-5crp-9r3c-p9vr](https://github.com/advisories/GHSA-5crp-9r3c-p9vr). O pin é localizado aos dois grafos afetados.
- Evidência: suíte completa `Jellyfin.Server.Implementations.Tests` Release — 1.045 aprovados, 38 ignorados, 0 falhas; build Release do servidor — 0 erros (915 avisos de analisadores já existentes); build GetAvatar Release para `net10.0`, `win-x64` e `linux-x64` — 0 erros. Os quatro manifests `.deps.json` gerados foram conferidos e nenhum contém `Newtonsoft.Json 9.0.1`; `git diff --check` passou.
- T6.4 continua aberto para proteção/rotação de segredos, transporte e revisão ampla de dependências. Nenhuma release criada/publicada; o gate continua exigindo concluir todas as melhorias ativas.

## Referências técnicas

Fontes oficiais consultadas em 28/09/2026. Disponibilidade, suporte e requisitos de hardware devem ser revalidados antes de qualquer implementação.

### Servidor

- [.NET releases and support](https://learn.microsoft.com/en-us/dotnet/core/releases-and-support)
- [.NET caching e HybridCache](https://learn.microsoft.com/en-us/dotnet/core/extensions/caching)
- [OpenTelemetry para .NET](https://opentelemetry.io/docs/languages/dotnet/)
- [Rate limiting ASP.NET Core (.NET 10)](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0)
- [.NET health checks](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/diagnostic-health-checks)
- [MongoDB Change Streams — disponibilidade e requisitos](https://www.mongodb.com/docs/manual/changestreams/)
- [Documentação FFmpeg](https://www.ffmpeg.org/ffmpeg.html)
- [Qwen3-VL no Ollama](https://ollama.com/library/qwen3-vl)
- [Ollama — saídas estruturadas](https://ollama.com/blog/structured-outputs)
- [Open Library — APIs de busca e catálogo](https://openlibrary.org/developers/api)

### Frontend web

- [Vite — recursos, importação dinâmica e divisão de código](https://vite.dev/guide/features)
- [TanStack Query — referência/cache](https://tanstack.com/query/latest/docs/framework/react/reference/classes/Query)
- [TanStack Virtual](https://tanstack.com/virtual/latest/docs/introduction)
- [React Compiler](https://react.dev/learn/react-compiler/introduction)
- [Web Vitals — medição](https://web.dev/articles/vitals-measurement-getting-started)
- [WCAG 2.2](https://www.w3.org/TR/WCAG22/)
- [Playwright — testes de acessibilidade](https://playwright.dev/docs/accessibility-testing)
- [Playwright — comparações visuais](https://playwright.dev/docs/test-snapshots)
