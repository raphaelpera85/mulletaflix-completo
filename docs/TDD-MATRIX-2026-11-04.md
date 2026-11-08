# Matriz TDD global — MulletaFlix — 2026-11-04

## Objetivo

Consolidar os cenários TDD por superfície do projeto. Cada item distingue:

- **RED**: reprodução/teste que deve falhar antes da correção;
- **GREEN**: comportamento mínimo que passa após a correção;
- **REGRESSION**: suíte/gate que protege contra regressão;
- **STATUS**: evidência observada nesta homologação.

A matriz não transforma um teste ignorado, bloqueado ou executado apenas com smoke em aceite formal.

## Regras de execução

1. Preservar alterações locais existentes; não executar `reset`, `checkout`, `clean`, commit, push ou release.
2. Não reutilizar credenciais ou tokens; valores sensíveis são `[REDACTED]`.
3. Para comportamento novo, executar RED → GREEN → REFACTOR → regressão focada.
4. Usar stage descartável para E2E e MariaDB; nunca alimentar stage com biblioteca de produção.
5. Aceite formal permanece **partial** enquanto houver gates bloqueados, ignorados relevantes ou sem evidência autorizada.

## Servidor .NET

| ID | Cenário | RED | GREEN | REGRESSION | Status/evidência |
|---|---|---|---|---|---|
| S-01 | Build da solução Release | Compilação falha se houver erro de código/analisador impeditivo | `dotnet build MulletaFlix.sln --configuration Release --no-restore -m:1` | Repetir após alterações do servidor | **PASS** — exit 0, sem erro de compilação observado |
| S-02 | Suíte não-Integration da solução | Projeto/teste quebrado reproduz falha | `dotnet test MulletaFlix.sln -c Release --no-build --no-restore --filter 'FullyQualifiedName!~Integration' -m:1` | Rodar solução novamente | **PASS técnico com ressalva** — 5.621 aprovados; 101 ignorados; 15 projetos com resultados visíveis |
| S-03 | Providers/TMDb e imagens | Item sem ProviderId não resolve imagem | Resolver por `Name`/`OriginalTitle`/ano e preservar IDs válidos | Providers completo + testes focais | **PASS** — 977/977 na execução registrada; testes de imagem 98/98; fluxo de séries 3/3 |
| S-04 | API de imagens/remotas | Rota protegida ou payload incorreto falha | Contrato `RemoteImages`, paginação e autenticação preservados | `Jellyfin.Api.Tests` e focais | **PASS** — 831 aprovados/3 ignorados no lote anterior; foco `RemoteImageController` 5/5 |
|| S-05 | Server.Implementations | Caminho de persistência/metadata falha | Correção mínima com cobertura focada | Suíte do projeto | **PASS técnico** — 1.757 aprovados/95 ignorados no lote auditado |
|| S-05.1 | Retry de persistência sob lock MariaDB | Commit transitório em `MediaStreamInfos`/mapas falha | Repetir a substituição inteira e preservar linhas anteriores após falha transitória | `ItemRepositoryTransientRetryTests` + `ServerPersistenceMariaDbTests` | **PASS técnico parcial** — foco 17/17 e suíte MariaDB real 34/35 aprovados, 1 ignorado (`SaveImagesAsync_RetriesAfterMariaDbProcessRestartDuringDelete`); primeira tentativa foi limitada por `max_connections` baixo do scratch e passou após subir o limite para 512. Isso ainda não prova a visibilidade do item no endpoint autenticado |
|| S-06 | Métricas/redaction | Labels/valores sensíveis aparecem em `/metrics` | Adapter configurado antes do startup e labels sanitizados | Testes de redaction + runtime | **PASS** — redaction local 8/8 e smoke live anterior sem labels pesquisados |
|| S-07 | Runtime instalado e restore oficial | Processo/endpoint não sobe ou restauração perde chaves persistidas | Versão 12.1.51; restore oficial, `/health` e `/System/Info/Public` HTTP 200 | Probe pós-restore, log `Startup complete`, contagens read-only e API/UI autenticadas | **PASS parcial** — restore oficial concluído; `users=16`, `baseitems=194203`, `peoplebaseitemmap=850927`, `activitylogs=2671`, usuário `Raphael` presente, `/health` 200 Healthy e API pública 12.1.51. Smoke browser não autenticado renderizou login sem erros e confirmou endpoints públicos; login e catálogo autenticados ainda pendentes; sem serviço Windows `MulletaFlixServer` |
| S-08 | Trust boundary/ACL | Restore directory aceita identidade não confiável | ACL mínima preservando usuário de execução/SYSTEM/Administradores | Probe de startup e instalação | **PASS** — causa de `UnauthorizedAccessException` corrigida |
| S-09 | Concorrência Embedded MariaDB | Segunda instância disputa mutex global | Falha fechada e diagnóstica, sem duas instâncias simultâneas | Teste de lifecycle/cleanup | **PARCIAL** — mutex reproduzido; testes concorrentes não contam como aceite |
| S-10 | Integração Mongo/MariaDB real | Ambiente ausente ou processo bloqueado | Datadir/porta descartáveis e cleanup comprovado | Suítes Integration completas | **PENDENTE/BLOQUEADO** — não promover por exit code sem contagem detalhada |
| S-11 | Mutação autenticada de mídia/capa | Sem credencial autorizada, cenário não pode ser exercitado | Atualização/refresh/download somente em fixture autorizada | Testes autenticados e leitura posterior | **PENDENTE** — nenhum refresh/download/mutação real foi executado |

## Frontend Web

| ID | Cenário | RED | GREEN | REGRESSION | Status/evidência |
|---|---|---|---|---|---|
| W-01 | TypeScript | DTO/rota incompatível falha em `tsc` | `npm run build:check` | Rodar após mudanças de fonte | **PASS** |
| W-02 | Lint | Regra ESLint quebra | `npm run lint:changed` | Lint focado + build | **PASS** |
| W-03 | Unit/Vitest | Componente/helper falha | `npm run test -- --run` | Suíte completa | **PASS** — 421/421 no gate registrado |
| W-04 | Bundle produção | Artefato ausente ou budget excedido | `npm run build:production` + `npm run verify:build` | Revalidar hash/contagem após empacotamento | **PASS fonte** — 1.929 artefatos; 294,3 KB gzip; budget 420 KB |
| W-05 | Wizard stage | Formulário/manual login quebra | Stage isolado com PTY | Repetir wizard | **PASS** — 2/2 |
| W-06 | Busca unificada stage | Rota/API de busca quebra | Stage isolado com PTY | Repetir busca | **PASS** — 1/1 |
| W-07 | Item Details com mídia | Preparação sem paths falha explicitamente; helper selecionava a pasta `movies` em vez de um item de filme e a asserção de metadata secundária exigia conteúdo que o frontend não gera para filmes sem metadata | Stage com `MFLX_MOVIES_PATH`/`MFLX_SERIES_PATH` válidos e descartáveis, filtro `IsFolder=false`/`IncludeItemTypes=Movie` e metadata secundária opcional | Item Details + limpeza | **PASS** — spec 33 passou **3/3** em stage autenticado via PTY; a suíte Playwright completa da mesma execução teve 22 aprovados, 15 falhas e 2 ignorados |
| W-08 | Login/auth visual | Credencial inválida ou fluxo divergente | Login autorizado em fixture | E2E autenticado completo | **PARCIAL** — admin, usuário comum e login passaram na execução ampla; a suíte terminou 22/39 aprovados, com falhas visuais/funcionais restantes e sem uso de produção |
| W-09 | Imagens/capas | `RemoteImages` vazio/URL antiga retorna erro | Contrato visual e HTTP validado com item fixture | Filmes, séries, temporadas e episódios | **PENDENTE** — divergência de bundle instalado e validação visual ainda abertas |
| W-10 | Acessibilidade de Home/Item Details | axe reporta comboboxes sem nome acessível e spec isolado não conclui o wizard | Labels semânticos nos quatro selects de tracks e pré-condição de wizard no spec | `31-accessibility-home-and-details.spec.ts` em stage autenticado | **PASS focal** — 1/1 aprovado em 2026-11-05; a suíte ampla ainda não foi reconciliada |
| W-11 | AdSense após intro | Fluxo exige dois vídeos e overlay após a intro, mas fixture/execução não o materializa | Dois vídeos sintéticos + configuração AdSense em stage descartável | `22-adsense-flow.spec.ts` com bundle reconstruído no stage | **GREEN focal não determinístico** — TDD `playIntroThenMain.test.ts` passou **4/4**; houve 1 execução focal com **1 falha/1 ignorado** por ausência do overlay após 120 s, seguida de **3 rechecagens 1/1 GREEN**. Causa do self-managed player permanece aberta; não promover estabilidade global |
| W-12 | Paginação e bibliotecas reconhecidas | Scan pode terminar sem itens ou rota pode ficar presa em `Nothing here` | Bibliotecas criadas, itens reconhecidos e rotas devem renderizar `#moviesPage`/`#tvshowsPage` após scan | `99-pagination.spec.ts` + `media-library.test.mjs` em stage descartável | **GREEN focal** — TDD do wait do harness passou **2/2** e `99-pagination.spec.ts` passou **2/2** após corrigir navegação duplicada para `/home`, asserção do nome de log e folders do usuário comum |
| W-12.1 | Diagnóstico movie-only após refresh explícito | Biblioteca existe, mas catálogo pode permanecer vazio sem diagnóstico reproduzível | Criar com `refreshLibrary=false`, iniciar `RefreshLibrary` explicitamente e validar contagem/itens reais | `21-media-recognition.spec.ts` em stage descartável | **GREEN** — **3/3 execuções consecutivas**, `MovieCount=2`, `ItemCount=3` e dois itens `Movie`/`Video`; reduz o risco do harness, sem fechar scanner em produção |
|| W-13 | Multi-user playback | Segundo contexto inicia HLS, mas não reporta `POST /Sessions/Playing`; sessão fica com fila e sem `NowPlayingItem` | Fallback de intro deve manter o player válido e emitir `reportPlaybackStart` para a mídia principal em dois contextos | `21-multi-user-playback.spec.ts` + trace de rede/console; `src/components/htmlMediaHelper.test.ts`; `media-library.test.mjs` | **GREEN focal 2/2 consecutivo** — lifecycle HLS corrigido e a criação da biblioteca foi serializada para não iniciar scan concorrente antes do refresh explícito. O TDD do helper passou, `npm run build:check` passou e `21 - Multi user playback` passou **1/1 em duas execuções consecutivas**, com stage descartável, PTY e fixtures sintéticas; a segunda execução registrou os dois filmes sintéticos e os dois playbacks. Isso não fecha a homologação global nem prova isoladamente uma correção do scanner do produto |
| W-14 | Regressão ampla pós-HLS | Falhas de contrato/fixture em gates E2E restantes | Executar todas as specs em stage descartável, preservar reports e classificar cada falha | Suíte Playwright completa + snapshots | **GREEN local** — execução válida mais recente: **21 specs / 40 testes, 40 aprovados, 0 falhas, 0 timeouts, 0 instáveis e 0 ignorados**, exit code 0, ~14,45 min, incluindo o novo `21-media-recognition.spec.ts`. O wrapper isolado iniciou Vite descartável para fixtures dev; runtime instalado não foi alterado |
| W-14.1 | Scroll buttons em fixture real e dinâmica | Stage Kestrel não serve `/@fs` nem o caminho de desenvolvimento `/elements/emby-scroller/emby-scroller.ts` | Config dedicado Vite deve renderizar as duas fixtures e manter navegação/RTL/teclado | `npm run test:playwright:scrollbuttons` | **GREEN focal 2/2** — passou em 10,6 s; falhas na suíte ampla classificadas como incompatibilidade de runner, sem patch de produto |
| W-14.2 | Pagination após criação/refresh de bibliotecas | API cria bibliotecas, mas o harness podia reentrar em `/home` e prender `Dashboard.navigate` | Stage deve reconhecer itens e renderizar `#moviesPage`/`#tvshowsPage` para admin e usuário comum | `99-pagination.spec.ts` isolado + `stage.test.mjs` + reports sem segredo | **GREEN focal 2/2** — passou em ~2,4 min; correção limitada ao harness/teste, sem patch no servidor ou runtime instalado |
| W-14.3 | Fixtures de Requests/Nebula/Backup/A11y no runner dedicado | Runner amplo não serve módulos/fixtures Vite e produz elementos ausentes | Configs dedicados Vite devem montar cada fixture e validar interação/acessibilidade | `npm run test:playwright:requests` + `npm run test:playwright:a11y` | **GREEN focal 11 casos** — requests **8/8** e a11y **3/3**; as 11 falhas equivalentes da suíte ampla foram reclassificadas como incompatibilidade do runner, sem patch de produto |
| W-14.4 | Regressão ampla após GREEN de AdSense | Reconciliar os gates após fechar AdSense, pagination e Unified Search | Stage descartável completo, com reports preservados e sem processos residuais | `run-isolated-stage.mjs` sem grep | **RED/BLOCKED** — 28 aprovados, 11 falhas e 0 timeouts em 39 testes; AdSense 2/2, Unified Search 1/1 e multi-user 1/1 passaram; permanecem os cenários 24–29 |
| W-14.5 | Unified Search e navegação SPA assíncrona | `Dashboard.navigate` pode manter o Promise pendente depois de a hash e a tela já mudarem | Harness deve concluir pela hash alvo, limitar a espera do Promise e propagar rejeições explícitas | `stage.test.mjs` + `23-search.spec.ts` | **GREEN focal** — TDD **2/2** e E2E **1/1** em 38,7 s; snapshot `mflx-playwright-preserved-20261105-unified-search-green` |
|| W-14.6 | Ordem de criação/refresh da biblioteca | `addVirtualFolder` podia iniciar scan concorrente antes do refresh explícito, deixando o stage com folder criado e catálogo vazio | Criar a biblioteca com `refreshLibrary=false` e aguardar o refresh explícito antes de validar contagem/item real | `media-library.test.mjs` + `21-multi-user-playback.spec.ts` | **GREEN focal** — TDD do contrato passou; multi-user passou **1/1 em duas execuções consecutivas** após a mudança, com logs da segunda execução mostrando 2 filmes reconhecidos e 2 playbacks iniciados. Diagnósticos históricos vazios permanecem como risco residual; sem promoção para homologação global |
|| W-14.7 | Origem Vite para fixtures no runner amplo | Kestrel não serve `/@fs`, imports TS/TSX e módulos de desenvolvimento; 11 testes falham apesar dos configs Vite dedicados verdes | O runner deve iniciar Vite via `webServer`, manter Kestrel para API/stage e direcionar as fixtures para `PW_FIXTURE_BASE_URL` | `stage.test.mjs` + regressão Playwright completa | **GREEN** — TDD **3/3**; fixtures `24`–`29` passaram **11/11** e a regressão ampla passou **39/39**. Snapshot `mflx-playwright-preserved-20261105-full-regression-after-vite-fixture-origin` |

## Android

| ID | Cenário | RED | GREEN | REGRESSION | Status/evidência |
|---|---|---|---|---|---|
| A-01 | JVM/unit | Teste JUnit/MockK/Truth falha | `./gradlew.bat testDebugUnitTest --no-daemon --console=plain` | Repetir todos os módulos | **PASS** — 1.788/1.788 no gate auditado |
| A-02 | Lint/assemble | Lint ou APK não compila | `lintDebug assembleDebug` | Build debug após mudanças | **PASS** — BUILD SUCCESSFUL |
| A-03 | Instrumentação | AVD/device indisponível ou teste falha | `connectedDebugAndroidTest` com AVD autorizado | Perfis phone/tablet/TV e navegação | **BLOQUEADO** — nenhum device/AVD ativo comprovado nesta execução |
| A-04 | APK/harness | Metadados, assinatura ou preparação falham | Scripts de verificação passam | Repetir scripts de APK | **PARCIAL** — helpers executados; aceite de instalação/dispositivo não comprovado |

## Nebula/Python

| ID | Cenário | RED | GREEN | REGRESSION | Status/evidência |
|---|---|---|---|---|---|
| N-01 | Nebula pytest | Teste de fila/mount falha | `pytest -q tests` no ambiente disponível | Repetir com dependências declaradas | **PASS técnico parcial** — 211 passed/1 skipped registrados |
| N-02 | NebulaPython | Teste async/dependência falha | `PYTHONPATH=. python -m pytest -q tests` | Suíte + requirements-test | **PASS técnico parcial** — 726 passed/2 skipped registrados |
| N-03 | Compilação Python | Sintaxe inválida | `python -m compileall -q .` | Executar em cada pacote | **PASS** nos caminhos auditados |
| N-04 | Lint/typecheck POSIX | Ferramenta ausente ou falha | ruff/mypy com ambiente POSIX completo | CI Linux/POSIX | **BLOQUEADO** — `venv/bin/python`/dependências POSIX não comprovados |
| N-05 | Path aliases e locks | Identidade física/lógica divergente | Testes fail-closed e sem lease stale | Suíte Nebula + runtime | **PARCIAL** — contrato documentado; homologação física permanece aberta |

## Packaging, portal e CI

| ID | Cenário | RED | GREEN | REGRESSION | Status/evidência |
|---|---|---|---|---|---|
| P-01 | Scripts packaging | Assert/script falha | Execução direta dos scripts e `validate-stage.ps1` | Repetir no stage descartável | **PASS técnico parcial** — scripts executados com exit 0; contagens individuais devem ser mantidas |
| P-02 | Segurança DuckDNS | Caminho/reparse/owner/ACL inválidos | `windows-duckdns-config-security.tests.ps1` | Repetir com updater real da working tree | **PASS** — mensagem explícita de testes aprovados |
| P-03 | Release tests | `.test.mjs`/`.test.sh` falha | Cada teste passa com pré-requisitos | Lote completo de release | **PARCIAL/BLOQUEADO** — todos os `.test.mjs` e scripts `.test.sh` passaram individualmente, exceto `linux-nebula-python-dependencies.test.sh`, que terminou exit 127 por ausência de `venv/bin/python` |
| P-04 | Portal status | JSON/contratos inconsistentes | status sincronizado, sem segredos, testes Node verdes | `node --test portal-site/tests/homologacao.test.mjs` | **PASS** — 368/368 após atualização da cobertura; `git diff --check` sem erro |
| P-05 | CI hospedada | Workflow não executa ou diverge local | Jobs reais verdes | GitHub Actions/artefatos | **PENDENTE** — não foi executada/publicada neste ciclo |
|| P-06 | Checklist de segurança fail-closed | Scanner retorna findings com exit code 0 ou timeouta em artefatos | Scanner limitado ao código relevante; checklist interpreta JSON e bloqueia findings | TDD `test_security_scan.py` + `test_checklist.py` + checklist completo | **PASS do harness / bloqueio real preservado** — TDD do scanner 7/7; scanner completo 3 findings heurísticos (`innerHTML` dinâmico: 2 GetAvatar corrigidos e 1 IntroSkipper aberto); checklist falha explicitamente no P0 em vez de declarar GREEN |

## Novos TDDs de correções prioritárias

1. **Item Details stage**: criar fixture descartável de filme/série e executar RED sem paths; GREEN com paths temporários; regressão em todos os cenários de mídia.
2. **Bundle instalado**: comparar manifest/hash fonte versus instalação; RED com divergência; GREEN somente após procedimento oficial autorizado; regressão com smoke runtime.
3. **RemoteImages visual**: fixture autenticada somente em stage; RED para lista vazia/404; GREEN para contrato e render; regressão por tipo `Primary`, `Backdrop` e idiomas.
4. **MariaDB mutex**: RED com segunda instância; GREEN com falha determinística e cleanup; regressão em testes de lifecycle sem tocar `3306`.
5. **Release tests**: isolar o primeiro `.test.mjs`/`.test.sh` que falha; reproduzir RED; corrigir script ou fixture; executar o lote completo.
6. **Linux/POSIX**: provisionar ambiente descartável conforme manifesto; RED no ambiente ausente; GREEN com `venv/bin/python`; regressão Nebula/Python e packaging.
7. **Android instrumentado**: levantar AVD descartável; RED sem device; GREEN em phone/tablet/TV; regressão de navegação, sessão e atualização.
8. **GetAvatar DOM XSS**: RED com payloads contendo aspas em `Id`/`Name`; GREEN após escapar aspas simples/duplas; regressão estática e DOM em `jsdom` (2/2 e 2/2).

## Critério de encerramento

A matriz só pode ser marcada como completa quando todos os itens bloqueados tiverem evidência observável, os testes ignorados estiverem justificados por contrato, o bundle em runtime for o bundle validado, e os gates autenticados/visuais/CI/instalação tiverem sido executados sem credenciais expostas.
