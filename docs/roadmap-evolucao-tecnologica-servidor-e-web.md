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
- [x] **T0.2 — Mapear fluxos e dependências.** Mapa estático documentado abaixo, com fontes de código por fluxo. Lacunas operacionais e medições continuam nas tarefas correspondentes.
- [ ] **T0.3 — Criar conjunto de cenários representativo.** Incluir biblioteca pequena/grande, mídia local e Telegram, interrupção de banco/rede, cache cheio, Windows e Linux.
- [ ] **T0.4 — Definir limites de regressão.** Fixar budgets iniciais para tempo de boot, tamanho do bundle web, latência de busca, espaço temporário e memória; calibrar com medições reais, não valores arbitrários.

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
- [ ] **T1.2 — Criar indicadores de operação no painel.** Saúde/degradação de MongoDB e MariaDB, espaço do cache, fila mais antiga, itens em retry, throughput e falhas por etapa.
- [ ] **T1.3 — Separar logs operacionais de auditoria.** IDs de correlação, retenção configurável e remoção/redação de tokens, credenciais, URLs assinadas e dados pessoais.
- [ ] **T1.4 — Expor health checks úteis.** Diferenciar processo ativo de serviço pronto; reportar dependências essenciais e estado degradado sem expor segredos publicamente.
  - [x] O `/ready` do Nebula usa `GetComponentHealthAsync` e verifica MongoDB + listeners FTP/HTTP; não materializa listas completas de uploads para responder readiness.
- [ ] **T1.5 — Definir alertas e diagnósticos.** Alertar fila sem progresso, backup vencido, disco próximo do limite, erro repetido de provedor e falha de restauração.

**Aceite:** um incidente de teste pode ser rastreado do endpoint/tarefa até a dependência causadora; health check não revela configuração sensível; métricas não usam títulos, caminhos ou IDs de usuário como labels de alta cardinalidade.

### Fase 2 — Nebula: fila, concorrência e recuperação (P0)

- [ ] **T2.1 — Especificar máquina de estados durável.** Definir estados, transições, lease/heartbeat, retomada após reinício e como distinguir retry, falha permanente e cancelamento.
- [ ] **T2.2 — Fortalecer idempotência e deduplicação.** Revalidar identidade canônica e estado Telegram antes de baixar, enviar ou reenfileirar; não duplicar arquivos concluídos.
- [ ] **T2.3 — Tornar prioridade observável e consistente.** Aplicar prioridade explícita de solicitações, ordem de categorias configurada e A–Z dentro da categoria; mostrar na interface posição, motivo e próximo item.
- [ ] **T2.4 — Implementar backpressure e fairness.** Limites independentes por operação/rede, proteção contra rajadas e evitar starvation de tarefas não prioritárias.
- [ ] **T2.5 — Adicionar recuperação operacional.** Retentativas com backoff/jitter, limite de tentativas, fila de falhas com reprocessamento administrativo e cancelamento seguro.
- [ ] **T2.6 — Revisar limpeza e startup.** Varredura/cleanup incremental, concorrência limitada, checkpoint e progresso reportado; inicialização não deve bloquear o host por uma limpeza completa.
- [ ] **T2.7 — Atualizar a listagem virtual após upload Nebula.** Persistir primeiro `completed` e as partes Telegram no MongoDB; em seguida invalidar somente a pasta afetada via rclone RC `vfs/refresh`. Falha no refresh não reverte o upload; não copiar a mídia integral para N: e não reduzir globalmente `--dir-cache-time` salvo como fallback medido.

**Aceite:** reiniciar o servidor durante download/upload retoma ou encerra o trabalho de modo consistente; cenário de retry não produz duplicação; prioridades efetivas coincidem com a ordem exibida; tarefas não ficam indefinidamente sem progresso.

### Fase 3 — Reprodução e cache temporário (P0)

- [ ] **T3.1 — Formalizar o contrato do cache de reprodução.** Cache em disco com limite configurável, chave canônica, política de expiração/evicção, espaço reservado e comportamento quando o volume está cheio.
  - [x] Expirar entradas após 1 hora sem atividade; manter limpeza periódica a cada 5 minutos e proteger leases ativos.
  - [x] Configurar cota máxima e reserva de espaço livre; evictar mídias inativas por LRU e aplicar limites na gravação e limpeza periódica.
  - [x] Quando cache/capacidade falhar, entregar os bytes ao fluxo de reprodução sem persistir o bloco.
- [ ] **T3.2 — Validar leitura em partes e prefetch.** Garantir que a mídia original começa a tocar enquanto o cache pré-carrega as partes necessárias, com limites de concorrência e cancelamento ao encerrar/trocar a sessão.
  - [x] Limitar a duas mídias com pré-cache integral ativo e quatro aguardando; não enfileirar prefetch-ahead além de dois chunks concorrentes. Os chunks antecipados usam o cache persistente compartilhado, evitando novo download do mesmo bloco entre ranges/leitores.
  - [x] Provar por teste do `NebulaChunkedStream` que o primeiro bloco é entregue e persistido enquanto o prefetch do próximo chunk continua bloqueado.
  - [x] Monitorar eventos `PlaybackStart`/`PlaybackStopped` por sessão; trocar/encerrar uma reprodução cancela o pré-cache da mídia anterior apenas quando nenhum outro cliente permanece nela. Eventos de parada sem identidade suficiente são ignorados; se ainda houver um range aberto, o cancelamento fica pendente até o último lease fechar, e novo playback remove essa intenção. Se a mídia retomar durante a resolução do cancelamento, o monitor reinicia o pré-cache.
  - [ ] Validar ponta a ponta em runtime o vínculo entre eventos reais Jellyfin, caminho STRM, documento Mongo e chave de cache, além de confirmar reprodução contínua durante troca/parada. O cancelamento por lease continua como fallback com tolerância de 2 minutos para encerramentos sem evento.
- [ ] **T3.3 — Preservar leases ativos.** Limpeza não remove conteúdo usado por leitores ou downloads em andamento; liberar lease mesmo em exceção, cancelamento e encerramento do servidor.
  - [x] Limpeza manual e por cota ignoram mídias com lease, prefetch ou fetch de chunk em andamento; mudança de configuração não interrompe sessões ativas.
- [x] **T3.4 — Prevenir duplicação de downloads concorrentes.** Uma única operação por parte/arquivo atende leitores simultâneos; cancelamento de um leitor não cancela nem duplica o download compartilhado.
- [ ] **T3.5 — Exibir diagnóstico de cache.** Bytes e arquivos em cache, hits/misses, latência Telegram, prefetch em andamento, leases, erros e limpeza segura.
  - [x] Painel usa os nomes atuais do DTO e mostra ocupação da cota, limite configurado, espaço livre/reserva e leases ativos.
- [ ] **T3.6 — Fazer testes de falha e recuperação.** Rede lenta/interrompida, parte ausente, servidor reiniciado, cliente cancelado, mudança de caminho e disco cheio.
  - [x] Cobrir cancelamento do último leitor, cancelamento de um leitor com outros aguardando e rejeição/cancelamento de operações no descarte do cache.
  - [x] Cobrir cota cheia, reserva mínima, evicção inativa, redução de cota existente e limpeza manual durante fetch.

**Aceite:** a reprodução direta do disco permanece inalterada; com Nebula, o cache não impede o primeiro frame, não remove partes ativas e demonstra redução mensurável de pausas em cenários equivalentes.

### Fase 4 — Backups e recuperação de dados (P0)

- [ ] **T4.1 — Inventariar dados recuperáveis.** MongoDB, MariaDB, usuários, configuração, índices, credenciais e dados de operação; classificar mídia volumosa separadamente.
- [ ] **T4.2 — Garantir consistência do backup.** Definir snapshot/backup seguro para cada banco, criptografia, retenção, rotação, verificação de integridade e política de remoção remota.
- [ ] **T4.3 — Criar teste automático de restauração isolada.** Restaurar em diretório/instância temporária, validar contagens e consultas críticas e registrar duração/resultado.
- [ ] **T4.4 — Expor status acionável no painel.** Última execução, próxima execução, destino, conteúdo incluído, tamanho, validação, erros e ação de teste/restauração protegida.
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
- [ ] **T6.2 — Aplicar rate limits e limites concorrentes seletivos.** Diferenciar login, busca, ações caras e operações de administração; retornar `429`/`Retry-After` sem degradar reprodução normal.
- [ ] **T6.3 — Validar entrada e caminhos de arquivo.** Tamanho, tipo, canonicalização, traversal, symlinks, extensões e acesso por usuário.
- [ ] **T6.4 — Revisar segredos, logs e transporte.** Proteção em repouso, rotação, permissões de arquivo e redação de credenciais; revisar dependências vulneráveis.
- [ ] **T6.5 — Validar configurações seguras por padrão.** Rede local/remota, TLS, CORS, headers, contas administrativas e exposição de endpoints internos.

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

### 28/09/2026 — Mapeamento estático de fluxos (T0.2 concluída)

- Documentada a cadeia de startup, ingestão/upload Nebula, download STRM, reprodução FTP/HTTP com cache, MariaDB, MongoDB/Supabase, scanners, backups e telas web de solicitações/cache.
- Separadas as responsabilidades entre MariaDB do catálogo do servidor, MongoDB `ftp` do Nebula, Telegram como armazenamento de partes, staging local, cache temporário e Supabase como destino remoto de sincronização/backup.
- Identificada distinção importante: `FullSystemBackup` e backup/sincronização Nebula para Supabase são fluxos diferentes; validar um não comprova restauração do outro.
- Escopo foi leitura de código e documentação de dependências; não houve mudança comportamental nem teste de runtime. T0.1/T0.3/T0.4 continuam abertas para medições e cenários reproduzíveis.

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
