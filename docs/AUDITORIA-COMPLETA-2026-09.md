# Auditoria completa do MulletaFlix — Setembro/2026

> Consolidação de: (1) documentação existente no repositório (`TODO-AUDITORIA.md`, `TODO-APP.md`, `AI-HANDOFF-MARIADB-ONLY.md`, `APK-HANDOFF-DEEPSEEK.md`, `docs/auditoria-completa-performance.md`, `docs/backend-auditoria-roadmap-tecnico.md`, `NEBULA_BUGFIX_TODO.md`); (2) inspeção direta da estrutura do repositório no computador do usuário; e (3) **verificação direta no Git público** (`github.com/raphaelpera85/mulletaflix-completo`, clonado nesta sessão) — commits, tags e conteúdo real de arquivos-chave, para confirmar quais achados abaixo já estão corrigidos em produção e quais continuam abertos. A seção 0 documenta essa verificação.

---

## 0. Verificação direta no Git (atualização desta rodada)

Uma rodada anterior desta auditoria citou "servidor em produção na versão 12.0.69" e "árvore de trabalho sem nenhum commit" — ambas as afirmações vinham de handoffs textuais desatualizados. O usuário confirmou que a produção já está na **12.0.92**, então cloneei o repositório público do GitHub e verifiquei ao vivo. Achados desta verificação:

- **A árvore está commitada.** `main` está no commit `b8f53d9` (26/09, 00:38 local), com histórico normal. A afirmação anterior de "266–319 arquivos sem commit" está **superada** — não reflete mais o estado real.
- **Existe uma branch de release separada da `main`**: `release/v12.0.92`, tag `v12.0.92` (commit `5a5420e5`, 25/09 23:10). É o padrão do projeto: `main` recebe desenvolvimento contínuo, e uma branch `release/vX.Y.Z` é cortada para virar o pacote publicado.
- **Ponto de divergência**: `main` e `release/v12.0.92` compartilham o ancestral comum `03d503a1` ("fix(nebula-ui): corrige badge de prioridade...", 22/09). A partir daí:
  - `release/v12.0.92` (o que está em produção agora) só recebeu **3 commits**, todos de empacotamento/versão (bump para 12.0.92, notas de release, cache do Gradle) — **nenhuma mudança de código** além do que já existia em `03d503a1`.
  - `main` recebeu **10 commits adicionais** desde então (22/09 a 26/09), com features/correções que **ainda não foram para produção**: preparação do Android v1.2.94, rastreio de estado de conexão do SyncPlay, "guided setup e descoberta de mídia", "sistema de feedback do usuário e features cross-platform", testes de contrato JSON para índices de faixa padrão, entre outros.
- **Conclusão prática**: a produção (12.0.92) **inclui** todas as correções críticas registradas até 22/09 (ver abaixo — confirmado lendo o código real dessa branch), mas **não inclui** o trabalho feito em `main` nos últimos dias. Isso não é um bug — é o fluxo normal de release — mas significa que o que está em `main` precisa ser testado e passar por um novo corte de release antes de chegar aos usuários.

### Itens do achado antigo re-verificados lendo o código real (não apenas a documentação)

| Achado | Verificação no código (release/v12.0.92 = main, arquivo idêntico nos dois) | Conclusão |
|---|---|---|
| **DADOS-1** — conflito de tracking do EF Core no refresh de episódios | `ItemPersistenceService.cs:656` usa `context.Entry(current).CurrentValues.SetValues(entity)` com comentário explícito citando a exceção original ("cannot be tracked because another instance..."). | ✅ **Corrigido e presente em produção.** |
| **E-4 / PeopleRepository** — full scan de 52 mil linhas a cada atualização de metadado | `PeopleRepository.cs:130` usa `Enumerable.Contains(candidateNames, e.Name)` com comentário explicando por que a forma anterior (`candidateNames.Contains`) quebrava a tradução do EF. | ✅ **Corrigido e presente em produção.** |
| **DADOS-2** — séries homônimas fundidas (`A Agência` 2020/2024) por perda do ano | `SeriesResolver.cs` (Emby.Server.Implementations) preenche `ProductionYear = seriesInfo.Year` nos três pontos de criação de série. | ✅ **Causa corrigida no código e em produção.** ⚠️ Os dois registros já corrompidos no banco desta instalação **continuam precisando de correção manual** (isso não é algo que git ou código resolvem). |
| **HTTP-1** — todo segmento HLS reconstrói o `StreamState` inteiro | Nenhum cache (`MemoryCache`, `ConcurrentDictionary` por `PlaySessionId`) encontrado em `DynamicHlsController.cs` nem nos arredores de `GetDynamicSegment`. | ❌ **Ainda aberto em produção.** Maior achado de performance P0 sem correção. |
| **FRONT-1 / F-3** — boot do cliente web bloqueado por import estático de rotas | `RootAppRouter.tsx` continua importando `DASHBOARD_APP_ROUTES`, `EXPERIMENTAL_APP_ROUTES`, `STABLE_APP_ROUTES`, `WIZARD_APP_ROUTES` de forma estática (linhas 10-13), sem `lazy()`. | ❌ **Ainda aberto em produção.** |
| **Android — assinatura de release cai para chave de debug** | `MulletaFlix-android/app/build.gradle.kts:35` ainda tem `initWith(getByName("debug"))` como fallback quando as variáveis de ambiente de assinatura não estão definidas. | ❌ **Ainda aberto.** Maior risco de segurança do app Android, sem mudança. |

Esta tabela substitui a seção de riscos operacionais da rodada anterior no que diz respeito ao estado do servidor: **não é mais verdade que a produção esteja desatualizada em relação às correções críticas de dados** (DADOS-1, DADOS-2, E-4) — essas já estão na 12.0.92. O que resta é o backlog de performance/segurança que nunca dependeu de "aplicar update", e sim de trabalho ainda não feito.

---

## 1. Visão geral do projeto

O MulletaFlix é um fork em .NET 10 do Jellyfin/Emby, com:

| Componente | Stack | Pasta |
|---|---|---|
| Servidor | .NET 10, 25+ projetos, MariaDB como único banco (SQLite removido) | `MulletaFlix-master/` |
| Web | React + TypeScript | `MulletaFlix-web-master/` |
| Android | Kotlin + Compose, Clean Architecture multi-módulo | `MulletaFlix-android/` |
| iOS | Nativo, em desenvolvimento | `MulletaFlix-iOS/` |
| Nebula | Python standalone (fora do .NET) — integração MongoDB + Telegram + FTP/HTTP para staging/streaming de arquivos, com backup para Supabase | `nebula/` (raiz do repo) |
| Empacotamento Windows | Instalador NSIS | `MulletaFlix-packaging-master/` |

O projeto já passou por **~30 rodadas de auditoria de performance**, uma auditoria de segurança dedicada (`NEBULA_BUGFIX_TODO.md`, concluída) e centenas de correções incrementais no app Android, todas documentadas com evidência de execução (builds com exit 0, contagem de testes). O valor desta rodada é **consolidar o que ainda está em aberto**, confirmar contra o código real o que já foi corrigido, sinalizar o que precisa de ação humana, e apontar riscos que nenhuma rodada anterior cobriu.

### Nota estrutural corrigida
O `README.md` sugere que o Nebula tem uma camada C# dentro do servidor .NET. Na inspeção desta sessão, `MulletaFlix-master/Nebula/` (dentro do fork .NET) está **vazio** (só um `__pycache__` órfão) — toda a integração Nebula real é o projeto Python em `nebula/` na raiz do repositório, standalone. Vale confirmar se essa é a arquitetura pretendida ou se há um resíduo de migração incompleta.

---

## 2. Achados abertos — Backend .NET (servidor)

### P0 — Crítico

| # | Achado | Local | Status confirmado |
|---|---|---|---|
| **HTTP-1** | Todo request de segmento HLS reconstrói o estado de streaming inteiro (resolução de media source, 2× path fallback, decisão de transcoding, novo `StreamState`). | `DynamicHlsController.cs`, `StreamStateCache.cs` | ✅ **Implementado e validado em `main`** (commit `89c38b8e`, 11 testes em `StreamStateCacheTests.cs`). |
| **FRONT-1 / F-3** | Cascata de boot serial no cliente web: `RootAppRouter` importava estaticamente os arrays de rotas do dashboard/experimental. | `RootAppRouter.tsx`, `src/index.tsx` | ✅ **Implementado e validado em `main`** (`patchRoutesOnNavigation` no commit `79284557` e preload paralelo na Rodada 33, commit `4d3cbe85`). |
| ~~DADOS-1~~ | ~~Conflito de tracking do EF Core~~ | `ItemPersistenceService.cs` | ✅ **Confirmado corrigido em produção** (ver seção 0). Removido da lista de abertos. |
| ~~DADOS-2 (código)~~ | ~~Séries homônimas fundidas por perda do ano~~ | `SeriesResolver.cs` | ✅ **Causa raiz confirmada corrigida em produção.** Continua pendente apenas a correção manual dos dois registros já danificados no banco desta instalação (ver Riscos operacionais). |

### P1 — Alto

**Banco de dados (MariaDB):**
- PK GUID `char(36)` replicada em 26 índices secundários de `baseitems` (~73 MB de espaço redundante); migrar para `binary(16)` — Onda 3, requer janela dedicada com plano de rollback.
- `Type` (varchar) repetido em 9 de 27 índices (~28 MB) — não executado.
- `DeleteItem` inlineia até ~78 mil GUIDs em 22 `ExecuteDelete` numa única transação (~65 MB de SQL) — `ItemPersistenceService.cs:145-166`.
- Filtro `e.Data.Contains(...)` sobre JSON `longtext` faz full scan em 7 filtros — `TranslateQuery.cs:1092-1131`.
- Busca de descendentes materializa ids e inlineia parâmetro por nível de hierarquia (3 round-trips) — `DescendantQueryHelper.cs:90-106`; correção proposta com `WITH RECURSIVE` não aplicada.
- Índice `IX_BaseItems_FullTextSearch` é BTREE comum, mas o código que o usaria (`MySqlDatabaseProvider.cs:393-401`) faz `MATCH...AGAINST`, que exige FULLTEXT — **bug latente, nunca é de fato exercitado** (não falha porque nunca é chamado). Decisão pendente: criar FULLTEXT real ou remover o método morto.
- Drift de schema: `IX_BaseItems_TopParentId_Type_IsVirtualItem_SeriesId_DateCreated` consta no histórico de migrations mas não existe no schema real — reconciliação pendente.

**HTTP/Streaming & Caching:**
- ✅ **H-5**: Segmentos de legenda HLS com ETag forte determinístico e suporte a 304 Not Modified — `SubtitleController.cs:520` (implementado na Rodada 31).
- ✅ **H-15**: Caching de fontes de fallback em memória (5m) com ETag e 304 Not Modified — `SubtitleController.cs:568-659` (implementado na Rodada 34, 166 testes passando).
- ✅ **Clone de `MediaSourceInfo[]`**: Clone explícito implementado em `MediaStream`/`MediaAttachment`/`MediaSourceInfo` (commit `89c38b8e`).
- ✅ **H-8**: Stream resolvido duas vezes no mesmo request eliminado — `UniversalAudioController.cs` e `AudioHelper.cs`.
- ✅ **Cache Eviction Policy (5m)**: `NebulaPlaybackCache.cs` (default lifetime e cleanup reduzidos de 1h para 5m com retenção ativa de leases) e `mount_drive_n.py` (`--vfs-cache-max-age 10m`) para controle estrito de espaço em disco (Rodada 34).
- `/Users/{id}/Views` buscado de 4 lugares diferentes com `staleTime` de 1s — `F-4`. (Pendente.)

**Concorrência / sync-over-async no backend:**
- ✅ **H-13**: Teto configurável de jobs ffmpeg simultâneos (`MaxConcurrentTranscodingJobs` via semáforo assíncrono) e eliminação de locks globais em `TranscodeManager._activeTranscodingJobs` usando `ConcurrentDictionary` (implementado na Rodada 34, 66 testes passando).
- ✅ **H-10**: Endpoint legado de trickplay com busca direta sem enumerar diretório inteiro (implementado na Rodada 31).
- Lock global de Live TV segurado através de `await` que abre stream remoto, serializando usuários em canais diferentes — `MediaSourceManager.cs:619`.
- `GetAwaiter().GetResult()` (sync-over-async) em: manifest de trickplay chamado a cada poll de `/Sessions` (`DtoService.cs:327`); `DisplayPreferencesManager.cs` (linhas 42, 60, 104, 113, 122); e várias repositories no scan (`PeopleRepository.cs:126,166`, `MediaStreamRepository.cs:47`, `ChapterRepository.cs:81`, `MediaAttachmentRepository.cs:39`, `LinkedChildrenService.cs:175`).
- Stripe de lock escolhido pelo primeiro item do lote em `ItemPersistenceService.cs:290` — lotes que compartilham itens podem colidir com exclusão perdida.
- Migração/DDL de domínio rodam incondicionalmente em todo boot (12 probes de `information_schema` + 4 `AnyAsync`) — `JellyfinMigrationService.cs:356`, risco de custo alto se a tabela de domínio estiver vazia.

**Frontend web:**
- Grades sem virtualização/`content-visibility`; leitor de EPUB carrega todos os spine documents em série; fonte de ícones + CSS somam >380 KB; poster sem `aspect-ratio` (CLS); polling do dashboard Nebula frequente demais; transições CSS em propriedades de layout. (F-6, F-7, F-9 a F-15 — lista completa com arquivo:linha em `docs/auditoria-completa-performance.md`).

### P2 — Médio
- Volume de log alto (~130 MB/dia) com ruído de heartbeats repetidos e 403 de terceiros, sem correção registrada.
- Endpoint legado de trickplay enumera o diretório de transcode inteiro por request de segmento.
- Sem teto de jobs ffmpeg simultâneos; lock global varrido até 2× por request de segmento.
- `UnidentifiedMediaCleanupTask` enfileira um `QueueRefresh` por item numa única rajada (decisão de produto, não bug mecânico).
- Poll de 500ms do `StrmProbeScheduledTask` seguraria um slot de tarefa em vez de usar sinalização — fora de escopo das rodadas anteriores.
- 28 ocorrências de `ffmpeg image extraction timed out (10000ms)` registradas na mesma janela do incidente de tracking (antes da correção) — cada uma mata o processo e segura o pool de thumbnails. Correção existe (`FFmpeg:imgExtractPerfTradeoff`) mas não foi ativada: é decisão de produto (muda o frame escolhido para todos os itens).

### P3 / Retratado (não é mais um achado válido — registrado para não ser re-aberto por engano)
- Índices "redundantes" de `userdata` (E-12): tabela tem só 15 linhas, correção não compensa.
- Query por-tile de trickplay (H-7): `trickplayinfos` tem 0 linhas nesta instalação, sem tráfego de tile.
- Limpeza de órfãos O(dirs×files) do Nebula (S-3): custo real medido é ~0,2s por ciclo — não compensa um release.
- "285 KB de date-fns fora da critical path" (F-2) — só o helper de locale saiu do chunk; o `vendor-date-fns` continua no boot por 4 componentes com import estático (mesmo mecanismo do FRONT-1).

### Não corrigível em código
- Bloqueio 403 de fontes de metadados atrás de Cloudflare — é o fingerprint TLS do próprio .NET, nenhuma troca de header resolve.

---

## 3. Nebula (integração Python — MongoDB/Telegram/FTP)

Auditoria de segurança dedicada já concluída (`NEBULA_BUGFIX_TODO.md`, todos os itens marcados como resolvidos): remoção de segredos hardcoded, tokens de bot deixaram de vazar em respostas de API, credenciais FTP/rclone deixaram de ser hardcoded, correção de colisão de nomes de arquivo, validação de ownership de upload, validação de range HTTP, validação de partes do Telegram, RLS habilitado no Supabase. Suíte focada: 144/144 (3 falhas restantes só por ausência de MariaDB de teste no ambiente).

Inspeção adicional desta sessão nos arquivos centrais (`server.py`, `tg.py`, `control_plane.py`, `feed_ftp.py`):
- Senhas com bcrypt, comparação de tempo constante (`hmac.compare_digest`) na autenticação HTTP, timeouts explícitos em toda chamada de rede, uso consistente de locks — **sem achados novos de segurança**.
- ~~Único ponto de menor qualidade: um `except:` totalmente genérico no parser de comando FTP~~ (`nebula/ftp/server.py:339`). **✅ Corrigido nesta rodada** — trocado por `except Exception:` (commit já escrito no seu PC, falta rodar `aplicar-commits.ps1`).
- Arquivos sensíveis reais existem no disco (`.env`, ~28 sessões `.session` do Telethon, `rclone-nebula.conf`), mas estão listados no `.gitignore` do próprio `nebula/`. **Recomenda-se confirmar que nunca foram commitados historicamente** (`git log --all -- nebula/.env`), já que o gitignore só protege daqui para frente.

Do README, ponto de segurança de rede já documentado como decisão de risco explícita do operador: FTP/HTTP nativo do Nebula não tem FTPS; hosts fora de loopback exigem `AllowInsecureRemoteFtp=true` explícito para funcionar sem TLS.

---

## 4. Aplicativo Android

O app tem cobertura de teste extensa (>1.000 testes unitários) e uma cadência de release muito ativa. A maior parte dos itens já documentados está corrigida. Em aberto (confirmado no código atual):

### Segurança / Build
- Em `app/build.gradle.kts:35`, quando as variáveis de ambiente de assinatura (`KEYSTORE_PATH` etc.) não estão definidas, o build de *release* cai silenciosamente para `initWith(getByName("debug"))` — ou seja, **releases já publicadas podem ter sido assinadas com a chave de debug**. É uma decisão registrada do usuário (mudar para falhar o build quebraria o fluxo atual). **Mitigação aplicada nesta rodada**: `logger.warn(...)` explícito antes do fallback, para o risco parar de ser silencioso (commit já escrito no seu PC, falta rodar `aplicar-commits.ps1`). Continua sendo o item de maior risco de segurança do app: qualquer pessoa com a chave de debug padrão do AGP pode assinar um APK que o Android tratará como atualização legítima — decisão de aceitar o risco ou migrar para assinatura real segue pendente.

### Funcionalidade não implementada
- **Cast/Chromecast não funciona de fato**: o botão (`MediaRouteButton`) existe e o app observa sessões, mas **não há `RemoteMediaClient` em lugar nenhum** — estabelecer uma sessão Cast não envia nenhuma mídia. Bloqueado por falta de receptor próprio e hardware real para testar.
- Reprodução offline não lista faixas de áudio/legenda (dependem de metadados do servidor, indisponíveis offline) — funcionalidade a desenhar, não bug pontual.
- Cache offline de favoritos/"continuar assistindo" foi deliberadamente removido (tinha bug de chave que corromperia dados entre dois usuários) e nunca foi redesenhado.

### Dados/API — riscos "silenciosos"
- `SearchRepositoryImpl` suprime aviso de truncamento de busca quando `TotalRecordCount == tamanho da página`; inofensivo contra o servidor de referência, mas quebraria contra um fork/proxy diferente.
- `BaseItemDtoQueryResultDto.totalRecordCount` é `Int` não-nullable — um JSON com `null` explícito faria o parser Moshi falhar a busca inteira em vez de tratar como "desconhecido".
- Índices de faixa de áudio/legenda escolhidos pelo servidor podem ser descartados em certos fluxos — explicitamente marcado como "não mexer sem sessão real".
- Risco ainda não comprovado: a suposição de que a ordem dos streams retornados pelo servidor corresponde 1:1 à ordem dos grupos do Media3 (mapeamento de faixas de áudio/legenda) não foi validada com mídia real multi-faixa.

### Trabalho em `main` ainda não lançado (achado novo desta verificação)
Confirmado no git: desde 22/09, a `main` recebeu commits com "Prepare Android 1.2.94 release and improve deep-link server switching", "Track SyncPlay connection state across reconnects" e "Add JSON contract tests for default media stream indices" que **não estão na branch de release atual**. Vale revisar esse lote antes do próximo corte de release para não perder esse trabalho nem lançá-lo sem o gate de testes que os handoffs anteriores exigem.

### QA / Testes
- Vários itens do backlog P0 dependem de sessão/credencial real e **nunca foram exercitados** por teste automatizado: login tradicional real, Quick Connect ponta-a-ponta, descoberta LAN real, reprodução real com retomada/seek/retry, seleção real de faixas, validação de capas com mídia real, foco remoto/D-pad real na TV.
- Auditoria de acessibilidade fora do player já auditado segue com pendências.
- Teste de instabilidade de rede (flakiness) durante login/descoberta/playback ainda não existe.
- Automatizar entrada de senha com caractere especial via UI do teste instrumentado ainda não é confiável.

### Build/Release — processo
- Regra de publicar em blocos de 10 versões significa que **várias builds locais validadas nunca chegaram a ser publicadas remotamente**.
- Tamanho do APK não identifica a build de forma confiável — só SHA-256 + `versionCode` são confiáveis para comparação.
- Falhas transitórias conhecidas e não investigadas a fundo: lock de arquivo do Windows em `classes.jar` durante empacotamento; falha de lint por `FileNotFoundException` em arquivo gerado por KSP; falha ao rodar `connectedDebugAndroidTest` com mais de ~8-10 módulos simultâneos.

---

## 5. Instalador Windows / Empacotamento

- **Validação em VM limpa continua pendente**: não há host/VM Windows elevado disponível nos ambientes de automação usados até aqui.
- As releases `v12.0.46` a `v12.0.62` (18 versões) foram publicadas **sem o instalador executável** por um bug no pipeline; corrigido a partir da `v12.0.63` com uma guarda fail-closed, mas essas 18 releases antigas não foram retro-corrigidas.
- `dist/` chegou a acumular ~15 GB de instaladores e APKs antigos; já foi limpo uma vez, mas sem processo automático contínuo.

---

## 6. Riscos operacionais (ação humana necessária)

1. ~~Servidor de produção desatualizado~~ — **superado**: confirmado que a produção (12.0.92) já inclui as correções críticas de dados (DADOS-1, E-4, causa raiz da DADOS-2).
2. **Duas séries já corrompidas no banco desta instalação** (`A Agência 2020/2024`) precisam de correção manual via painel (Identificar → série correta → atualizar metadados) — o código corrigido não repara dados já gravados.
3. **Chave de assinatura de debug em releases Android publicadas** — decisão consciente registrada, aviso explícito adicionado nesta rodada, mas vale reavaliar dado o risco de segurança.
4. **Onda 3 de otimização de banco (PK `binary(16)`, normalização de `Type`)** precisa de janela dedicada com plano de rollback.
5. **10 commits em `main` não incluídos na release 12.0.92** (Android 1.2.94, SyncPlay, guided setup, feedback do usuário) — decidir se entram no próximo corte de release ou se há razão para segurá-los.
6. **Múltiplas sessões de IA atuando no mesmo repositório simultaneamente** (confirmado: commits em `main` e em `release/v12.0.92` no mesmo minuto, 26/09 00:32) — vale coordenar para evitar colisões futuras (já ocorreu pelo menos um episódio de arquivo sobrescrito, registrado no handoff de banco de dados).

---

## 7. Recomendações priorizadas

1. Corrigir manualmente as duas séries "A Agência" já corrompidas no banco.
2. Decidir o destino dos 10 commits pendentes em `main` (revisar, testar, e incluir ou não no próximo release).
3. Priorizar **HTTP-1** (cache de `StreamState` por sessão) e **FRONT-1/F-3** (rotas lazy no cliente web) na próxima rodada de performance — são os dois P0 de maior impacto ainda sem correção, ambos confirmados abertos no código atual em produção.
4. Decidir o destino da chave de assinatura Android (aceitar o risco formalmente ou migrar para assinatura real antes da próxima release pública).
5. Reavaliar a arquitetura Nebula: confirmar se a pasta `MulletaFlix-master/Nebula/` vazia (dentro do .NET) é resíduo a remover.
6. Planejar a Onda 3 de banco de dados (índices PK/Type) como projeto isolado, com janela de manutenção e rollback testado.
7. Fechar o backlog P1 de HTTP/streaming (H-8) e de concorrência backend (B-4, B-6 a B-8, B-11) — ver `claude/HANDOFF-DESENVOLVIMENTO-LOCAL-2026-09-26.md` para a lista de execução em ordem.

---

## 8. Fontes desta consolidação

- `TODO-AUDITORIA.md`, `TODO-APP.md`, `AI-HANDOFF-MARIADB-ONLY.md`, `APK-HANDOFF-DEEPSEEK.md` (arquivos do projeto, changelogs com evidência de execução)
- `MulletaFlix-master/docs/auditoria-completa-performance.md`, `docs/backend-auditoria-roadmap-tecnico.md`, `docs/feature-implementation-plan.md`, `MulletaFlix-master/NEBULA_BUGFIX_TODO.md` (lidos via acesso ao computador do usuário)
- Inspeção direta de `nebula/server.py`, `nebula/tg.py`, `nebula/control_plane.py`, `nebula/tools/feed_ftp.py` (lidos via acesso ao computador do usuário)
- **`github.com/raphaelpera85/mulletaflix-completo` clonado nesta sessão** (branches `main` e `release/v12.0.92`, tags, e leitura direta de `ItemPersistenceService.cs`, `PeopleRepository.cs`, `SeriesResolver.cs`, `DynamicHlsController.cs`, `RootAppRouter.tsx`, `build.gradle.kts`) — esta é a única seção com verificação por execução real de `git log`/`git diff`/`grep`, não apenas leitura de documentação.
- Correções desta rodada (Nebula `except`, aviso de chave de debug Android, clone explícito de `MediaSourceInfo`) — ver `claude/HANDOFF-DESENVOLVIMENTO-LOCAL-2026-09-26.md` para status de aplicação/commit de cada uma.

**Limitação restante**: ainda não foi possível rodar `dotnet test`/`npm test` de verdade (o sandbox desta sessão não tem acesso de rede ao NuGet, apenas a npm/pypi/crates/go) nem consultar o MariaDB de produção diretamente — a confirmação da correção das duas séries "A Agência" continua dependendo de ação manual no painel do usuário, não verificável por git, e a validação do fix H-6 depende de build/test real no Claude Code local.
