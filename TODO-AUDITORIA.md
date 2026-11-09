# Regressão final pós-merge e atualização da matriz — 2026-11-08 12:17 BRT

- [x] Merge auditado com `origin/main` concluído localmente sem conflitos remanescentes (`unmerged=0`); 30 arquivos conflitantes foram resolvidos, combinando as mudanças remotas e auditadas.
- [x] Build Release da solução .NET terminou com 0 erros; regressão serial filtrada com TRX exclusivo passou **6.028 aprovados, 101 ignorados, 0 falhas/6.129 casos**.
- [x] Web passou **430/430 testes em 80 arquivos**, `tsc --noEmit`, verificação de 1.928 artefatos e budget de 295,8 KB gzip inicial contra 420 KB.
- [x] Harness de isolamento passou **15/15**; contrato do portal passou **379/379** e o JSON foi validado.
- [~] Os 101 ignorados incluem Linux/Unix, Mongo/Supabase e requisitos de ambiente; casos `Integration` foram excluídos pelo filtro. Android instrumentado, Linux/POSIX, CI hospedada, runtime instalado, catálogo autenticado e playback continuam pendentes.
- [x] Nenhum runtime de produção, MariaDB produtivo, credencial ou artefato gerado foi alterado; relatórios TRX descartáveis foram removidos após a contagem.

# Runtime instalado 12.1.51 — restauração oficial concluída; aceite autenticado pendente — 2026-11-07 04:45 BRT

## Stage atualizado para 12.1.51 e reexecução Playwright — 2026-11-08 05:46 BRT

- [x] `stage/` foi sincronizado com `C:/Program Files/MulletaFlix/Server`; hashes SHA-256 coincidem para `MulletaFlix.dll`, `MulletaFlix.Server.Implementations.dll`, `MulletaFlix.Plugin.GetAvatar.dll` e `MulletaFlix-web/index.html`.
- [x] O stage anterior foi preservado em `C:/Users/Raphael/.mulletaflix-tmp/stage-backup-before-12.1.51-20261108-051102`; produção em `C:/ProgramData/MulletaFlix/Server:8096` não foi alterada.
- [x] O log da nova execução confirmou `MulletaFlix version: 12.1.51`, DLL em `stage/`, MariaDB descartável na porta `33100` e fixtures sintéticas explícitas.
- [~] A regressão Playwright não terminou: o teste 12 falhou, o teste 17 falhou com `MovieCount=0`/itens vazios após o refresh, e o teste 18 permaneceu sem progresso por mais de 30 minutos; a execução foi encerrada sem resumo JSON final.
- [~] O scanner/materialização segue bloqueio prioritário: o log posterior registra análise de `Synthetic Movie (2026)` e `Synthetic Movie Two (2026)`, mas a consulta autenticada do teste ainda observou zero itens. Não promover como falha definitiva de produto nem como GREEN do harness sem diagnóstico adicional.
- [x] Após o encerramento, nenhum Node/Playwright/Vite/dotnet/MariaDB scratch permaneceu ativo; somente o MariaDB produtivo em `3306` permaneceu.

## TDD do isolamento de integrações e timeout do catálogo — 2026-11-08 07:36 BRT

- [x] RED: o contrato novo falhou porque o helper de configuração isolada ainda não exportava `buildIsolatedNebulaConfiguration`; depois, o contrato de `IntroSkipper` reproduziu o mesmo RED antes da implementação.
- [x] GREEN: os contratos do wrapper passaram **15/15**; `node --check` passou em `run-suite.mjs` e `run-isolated-stage-helpers.mjs`; `git diff --check` passou.
- [x] O stage agora grava explicitamente `NotificationsEnabled=false`, `TelegramNotificationsEnabled=false`, canais/chat/tokens vazios, Supabase desabilitado e `IntroSkipper.xml` com `AutoDetectIntros=false`; o harness não deve herdar o `ChatId` padrão produtivo nem iniciar análise automática durante o catálogo.
- [x] A execução anterior preservou evidência de que o stage 12.1.51 tentava notificar o canal padrão e executava análise IntroSkipper; essa fonte de ruído foi removida no harness, sem alterar produção.
- [~] Mesmo com notificações externas e análise automática desabilitadas, o teste focal com `ensureMediaLibrariesReady` não retornou em 2 minutos: o log registrou `Scan Media Library Completed` às 07:31:28 BRT, mas não houve `MEDIA_RECOGNITION_DIAGNOSTIC`, nem consulta autenticada concluída. O cenário foi encerrado após o logout do teste e cleanup.
- [~] Bloqueio atual refinado: a prontidão do catálogo/API (`getItemCounts` ou chamada anterior de tarefas) permanece pendurada ou não observável após o scan; ainda não é possível atribuir causalidade definitiva ao produto ou ao cliente legado sem uma chamada HTTP com timeout explícito e evidência de status/resposta.
- [x] Após o encerramento, nenhuma instância Node/dotnet/MariaDB scratch permaneceu; somente o MariaDB produtivo em `3306` foi observado.

## TDD do diagnóstico de timeout de reconhecimento — 2026-11-08 20:44 BRT

- [x] RED reproduzido contra o helper anterior: o timeout retornava apenas `Media recognition did not stabilize before timeout.` e descartava os últimos counts observados.
- [x] GREEN: `waitForMediaRecognition` agora inclui `MovieCount` e `SeriesCount` finais na exceção; teste focal passou **4/4**.
- [x] Regressão web: **80/80 arquivos, 430/430 testes**; `tsc --noEmit` passou; **1.928 artefatos** válidos e **295,8 KB gzip / 420 KB** no budget.
- [~] A mudança melhora a evidência do harness, mas não prova a causa do scanner nem promove catálogo autenticado, runtime ou playback.

## Regressão NebulaPython — 2026-11-08 20:56 BRT

- [x] Suíte completa executada com `uv run --with-requirements requirements-test.txt`: **726 aprovados, 2 ignorados, 0 falhas** em **52,14 s** na execução com razões dos skips.
- [x] Skips classificados: `test_upload_checkpoint.py:49` cobre branch POSIX de sync após rename; `test_upload_checkpoint.py:72` cobre `MoveFileExW`/`MOVEFILE_WRITE_THROUGH` do Windows.
- [~] `pytest_asyncio` emitiu depreciação porque `asyncio_default_fixture_loop_scope` não está configurado; não bloqueou a suíte, mas é melhoria de configuração pendente.
- [~] Não houve homologação MongoDB, FTP real, Supabase real, produção ou runtime instalado; a evidência é da suíte isolada Python.

## Baseline Ruff NebulaPython — 2026-11-08 21:01 BRT

- [~] `uv run --with ruff -- ruff check .` terminou exit 1 com **379 findings**; não foi aplicado `--fix`.
- [x] Regras dominantes mapeadas: `BLE001` **126**, `B023` **37**, `I001` **25**, `S110` **23**, `RUF059` **16**, `UP045` **14**, `F401` **14**, `UP006` **14**, `UP041` **11**, `UP024` **11**, `TRY004` **10** e `PIE807` **9**.
- [x] Arquivos com maior concentração: `main.py` **94**, `tools/supabase_sync.py` **65**, `tools/strm_downloader.py` **58**, `tools/feed_ftp.py` **43**, `ftp/server.py` **23**, `ftp/pathio.py` **18** e `control_plane.py` **13**.
- [~] O baseline mistura qualidade, exceções amplas e regras de segurança; não corrigir em massa sem fatias TDD e revisão de comportamento. `BLE001` e `S110` exigem triagem semântica, não autofix cego.
- [x] Corrigida uma ocorrência segura de `I001` em `upload_checkpoint.py` (remoção de linha em branco extra no bloco de imports), sem alteração comportamental.
- [x] Regressão focada de `tests/test_upload_checkpoint.py`: **17 aprovados, 2 ignorados, 0 falhas**; regressão NebulaPython completa: **726 aprovados, 2 ignorados, 0 falhas** em **35,23 s**.
- [~] Baseline pós-correção: **376 findings**; dois `TRY004` de `upload_checkpoint.py` foram marcados como exceções intencionais porque trocar `ValueError` por `TypeError` quebra a tradução existente de journal inválido para `UploadCheckpointUnavailableError`.
- [x] TDD/regressão do checkpoint: focal **17 aprovados, 2 ignorados**; suíte NebulaPython **726 aprovados, 2 ignorados, 0 falhas**. A decisão preserva o contrato público e reduz somente o falso positivo de lint.
- [x] Correção segura adicional em `main.py`: Ruff reorganizou somente três blocos `I001` de imports, sem alteração comportamental; o baseline global caiu para **373 findings** (`I001`: 21).
- [x] Regressão após a reordenação: suíte NebulaPython **726 aprovados, 2 ignorados, 0 falhas**; warning `pytest_asyncio` permanece classificado.
- [x] Correção segura adicional em `tools/supabase_sync.py`: Ruff reorganizou somente três blocos `I001` de imports, sem alteração comportamental; o baseline global caiu para **370 findings** (`I001`: 18).
- [x] Regressão após a reordenação: suíte NebulaPython **726 aprovados, 2 ignorados, 0 falhas**; não houve integração Supabase/Mongo real.
- [x] TDD RED/GREEN do callback `os.walk` em `tools/strm_downloader.py`: o teste reproduziu o fechamento tardio na raiz errada e passou após vincular `scan_root` como argumento padrão.
- [x] Ruff `B023` do `strm_downloader.py`: **0 findings**; baseline global caiu para **369 findings**. Regressão NebulaPython: **727 aprovados, 2 ignorados, 0 falhas**.
- [~] O restante de `B023` está concentrado em `main.py` (**36 ocorrências**) e requer fatias separadas por fluxo assíncrono.

## Backup Nebula → Supabase — diagnóstico e correção — 2026-11-08 21:42 BRT

- [x] Reproduzida a causa no fluxo `POST /NebulaFtp/Supabase/Backup`: quando URL/chave não estão configuradas, `BackupMongoToSupabaseAsync` retornava falha em segundo plano sem registrar `SupabaseLastBackupStatus`, `SupabaseLastBackupAttemptTime` ou log operacional; a UI mantinha "Nenhum backup realizado ainda" após exibir apenas "Solicitação de backup enviada".
- [x] TDD RED: `BackupMongoToSupabaseAsync_RecordsMissingConfigurationFailure` falhou porque `SupabaseLastBackupFailed` permanecia `null`.
- [x] GREEN: o ramo de configuração ausente agora registra `[SUPABASE-ERRO]`, persiste o resultado de falha e grava o replay idempotente antes de retornar.
- [x] Teste focal GREEN: **1/1**; `NebulaFtpController` focal: **36/36**; regressão Nebula filtrada: **739 aprovados, 38 ignorados, 0 falhas / 777**.
- [x] Build Release da solução: **0 erros, 32 avisos**.
- [~] O runtime instalado `12.1.51` em `C:/Program Files/MulletaFlix/Server` não foi alterado; a correção está no source e não deve ser considerada ativa em produção sem pacote controlado, rollback e autorização operacional.
- [x] TDD da UI: `NebulaFtpConfigPage.test.mjs` passou **2/2**, cobrindo falha persistida e fallback de configuração legada.
- [x] A página agora consulta `GET /NebulaFtp/Supabase/Status` e exibe `LastBackupStatus`/conectividade persistidos, evitando que o card permaneça em "Nenhum backup realizado ainda" após uma falha.
- [~] O contrato visual foi validado por view model, mas ainda não houve aceite browser autenticado no runtime instalado nem backup real contra Supabase.

- [x] Restauração oficial do backup de 04/11 concluída; o log registrou `Restored primary and media-domain databases`, `Restored MulletaFlix system` às 04:34:56 BRT e `Startup complete` às 04:35:44 BRT.
- [x] Runtime `12.1.51` em `C:/Program Files/MulletaFlix/Server` com datadir oficial `C:/ProgramData/MulletaFlix/Server`; `/health` 200 `Healthy`; `/System/Info/Public` confirma `Version=12.1.51`, `TargetAbi=12.1.51` e wizard concluído.
- [x] Banco restaurado confirmado por leitura: `users=16`, `baseitems=194203`, `peoplebaseitemmap=850927`, `activitylogs=2671`; `Username='Raphael'` ocorre uma vez.
- [~] Login real e navegação autenticada ainda pendentes: browser vault sem login salvo e solicitação de salvamento recusada; nenhuma credencial foi solicitada ou registrada.
- [~] Homologação global permanece `PARTIAL`: Android instrumentado, Linux/POSIX, CI hospedada, integrações reais e aceite formal de produção continuam abertos.
- [x] Portal e documentação atualizados; evento `runtime-installed-1251-official-backup-restore-complete-2026-11-07-0445`; contrato do portal passou 368/368.

## Recheck dos bloqueios de ambiente — 2026-11-08 01:53 BRT

- [x] Runtime instalado revalidado em leitura: PID 19932 é `C:/Program Files/MulletaFlix/Server/MulletaFlix.exe`, iniciado com `--datadir C:/ProgramData/MulletaFlix/Server`; porta `8096` responde `/health` HTTP 200. Isso valida o release instalado, não a working tree nem o artefato corrigido do PR #2.
- [x] MariaDB `3306` (PID 10980) e MongoDB `27017` (PID 4412) estão ouvindo; permanecem classificados como serviços de produção e não foram usados em testes destrutivos ou de concorrência.
- [x] ADB existe em `C:/Android/Sdk/platform-tools/adb.exe`, mas `adb devices -l` não listou dispositivos; WSL respondeu sem distribuições instaladas; Docker/Podman e coletores OTLP não estão disponíveis no PATH.
- [~] Homologação global continua `PARTIAL`: Android instrumentado, Linux/POSIX, CI hospedada, login/catálogo autenticados, runtime com o bundle GetAvatar corrigido e aceite formal de produção seguem pendentes.
- [x] Evento `environment-blockers-reprobe-2026-11-08-0153` adicionado ao portal; contrato passou **369/369**.

## Reexecução da suíte web após falha intermitente — 2026-11-08 02:12 BRT

- [~] Uma execução ampla de `npm test -- --run` terminou com exit 1, mas a saída detalhada dessa primeira tentativa não foi preservada.
- [x] Investigação focal: `src/apps/dashboard/routes/action-log.test.tsx` passou **4/4** isoladamente.
- [x] Regressão ampla posterior: três execuções consecutivas passaram **78 arquivos / 425 testes** cada; `npm run build:check` deve continuar sendo executado no gate final.
- [~] Warnings persistentes de `jsdom`/`webcomponents.js` foram observados (`getComputedStyle` com pseudo-elementos e callback após `window` indisponível). Nenhuma falha de produto foi reproduzida; a estabilidade do harness/teardown continua melhoria aberta e não deve ser mascarada por retries.
- [x] Evento `web-vitest-flake-reprobe-2026-11-08-0212` adicionado ao portal; status permanece `partial`.
- [x] Rechecagem no checkout publicado: o teste focal passou **8/8** e quatro execuções paralelas completas passaram **80 arquivos / 430 testes** cada, sem timeout do `ActionLogPage`.
- [~] A flakiness histórica não foi reproduzida; os warnings de teardown permanecem melhoria de harness. Não alterar produto nem promover o gate web integral enquanto Playwright autenticado, ESLint baseline e ambientes externos continuarem pendentes.
- [x] Evento `web-vitest-parallel-recheck-2026-11-08-1230` adicionado ao portal.

## Gate ESLint web — 2026-11-08 02:26 BRT

- [~] `npm run lint` terminou exit 1 com **1.415 problemas**: **1.305 erros** e **110 warnings**.
- [~] Lint focal dos arquivos web alterados reportou **240 problemas**: **226 erros** e **14 warnings**. A maior parte é baseline legado (`any`, complexidade, `@ts-nocheck`, scripts de harness e regras Sonar), não uma regressão causalmente atribuída nesta sessão.
- [x] `npm run build:check` e as três reexecuções Vitest continuam verdes; lint não foi mascarado nem corrigido em massa.
- [x] Evento `web-eslint-baseline-2026-11-08-0226` adicionado ao portal; gate web permanece `partial`.

## Gates de artefato e estilo web — 2026-11-08 02:29 BRT

- [x] `npm run verify:build` passou: **1.928 arquivos válidos**, limite individual **1.536 KiB**, payload inicial **295,8 KB gzip** contra orçamento de **420 KB**.
- [x] `npm run stylelint` passou sem findings.
- [~] Evidência local não equivale a CI hospedada, distribuição no runtime Kestrel, browser autenticado ou produção; ESLint segue parcial.
- [x] Evento `web-build-artifact-gates-2026-11-08-0229` adicionado ao portal.

## TDD do parser linear dos artefatos web — 2026-11-08 02:36 BRT

- [x] RED: teste novo falhou porque o helper `verify-build-artifact-helpers.mjs` ainda não exportava `extractSitemapUrls`.
- [x] GREEN: parser linear para `<loc>` e meta tags substituiu regexes com risco de backtracking; TDD passou **3/3**.
- [x] ESLint focal dos dois scripts passou; `npm run verify:build`, `npm run build:check` e Vitest passaram **425/425**.
- [~] Warnings `jsdom/webcomponents` continuam na suíte ampla; não são atribuídos ao parser.
- [x] Evento `web-build-artifact-parser-tdd-2026-11-08-0236` adicionado ao portal.

## TDD do nome de stage isolado do Playwright — 2026-11-08 02:47 BRT

- [x] RED: teste novo falhou porque `run-isolated-stage-helpers.mjs` ainda não existia.
- [x] GREEN: `createStageDirName` passou **1/1**, usando UUID em vez de `Math.random`.
- [x] `npm run verify:build`, `npm run build:check` e Vitest passaram **425/425**.
- [~] ESLint focal ainda acusa **2 findings** nos diretórios temporários atribuídos a `TEMP`/`TMP`; exige análise de ACL e permanece fora deste slice.
- [x] Nenhum Playwright foi executado e nenhuma mídia real foi usada.
- [x] Evento `web-playwright-stage-name-tdd-2026-11-08-0247` adicionado ao portal.

## TDD do ambiente temporário isolado do Playwright — 2026-11-08 02:54 BRT

- [x] RED: o teste falhou porque `buildStageEnvironment` não era exportado.
- [x] GREEN: `buildStageEnvironment` passou **2/2** e deixou o ambiente do launcher imutável.
- [x] ESLint focal passou sem findings; `verify:build`, `build:check` e Vitest passaram **425/425**.
- [~] Warnings `jsdom/webcomponents` continuam conhecidos; ACL efetiva do diretório e execução Playwright permanecem não exercitadas.
- [x] Evento `web-playwright-stage-env-tdd-2026-11-08-0254` adicionado ao portal.

## Reprobe read-only da ACL do stage temporário — 2026-11-08 02:57 BRT

- [x] `icacls` confirmou na raiz e em um stage filho somente SYSTEM, Administradores e o usuário `Raphael`, com Full Control herdável.
- [x] `tasklist` não mostrou Node/Playwright/Vite/dotnet ativos; somente o runtime instalado `MulletaFlix.exe` PID 19932 estava ativo.
- [~] Foram encontrados **171 diretórios stage antigos**; nenhum foi removido por falta de decisão explícita de retenção/limpeza.
- [~] A ACL local é compatível com a política observada, mas isso não substitui execução completa do Playwright nem aceite de runtime.
- [x] Evento `web-playwright-temp-acl-reprobe-2026-11-08-0257` adicionado ao portal.

## TDD de limpeza do stage Playwright — 2026-11-08 03:05 BRT

- [x] RED: `stage-safety.test.mjs` falhou porque `buildOwnedStageCleanupTargets` não existia.
- [x] GREEN: cleanup restrito ao diretório fornecido pelo wrapper; suíte focal passou **9/9**.
- [x] `node --check`, ESLint focal, `verify:build`, `build:check` e Vitest **425/425** passaram.
- [~] A limpeza cobre os caminhos de sucesso, erro e ausência de specs, mas ainda não foi exercitada por Playwright completo.
- [~] Os **171 resíduos antigos** não foram apagados por esta alteração; requerem retenção/limpeza operacional separada.
- [x] Evento `web-playwright-stage-cleanup-tdd-2026-11-08-0305` adicionado ao portal.

## Probe real do cleanup sem specs — 2026-11-08 03:08 BRT

- [x] Executado com `PW_RESET_STAGE=false`, `--no-reset-stage`, specs vazios e marker sintético.
- [x] Resultado: `status=no-specs`, `specCount=0`, `stageReachable=true`; o diretório fornecido pelo wrapper foi removido.
- [x] Nenhum Node/Playwright/Vite/dotnet ficou ativo após a execução; o runtime instalado não foi tocado.
- [~] Esta prova exercita somente cleanup; não equivale à suíte Playwright completa.
- [~] Os **171 diretórios antigos** permanecem preservados.
- [x] Evento `web-playwright-stage-cleanup-probe-2026-11-08-0308` adicionado ao portal.

## Auditoria do checklist AG Kit e scanner de segurança — 2026-11-07 05:31 BRT

- [x] RED reproduzido: `security_scan.py` varria `.agents`/artefatos gerados, demorava além do limite e o checklist tratava exit code `0` como GREEN mesmo com findings.
- [x] TDD do escopo passou RED/GREEN: diretórios de agentes/artefatos agora são excluídos; `checklist.py` consome o JSON do scanner e falha quando `summary.total_findings > 0`.
- [x] TDD do detector de configuração passou RED/GREEN: o scanner reconhece middleware aninhado `SecurityHeadersMiddleware.cs`; o projeto real deixou de gerar falso positivo de headers ausentes.
- [x] Regressão do scanner: dependências sem findings; configuração `[OK]`; após TDD do placeholder, a execução completa terminou em 12 padrões XSS, com o checklist parando em Security Scan em vez de declarar GREEN.
- [x] O finding de secret foi revisado sem copiar o valor: é um placeholder Bearer conhecido em fixture de teste; o scanner agora ignora somente placeholders explícitos e continua reportando tokens não-placeholder.
- [~] Os 12 padrões restantes são atribuições `innerHTML`: as superfícies GetAvatar usam `escapeHtml`; NebulaFTP usa texto/DOM nodes nos dados variáveis; o helper minificado do IntroSkipper ainda exige revisão manual. UX/SEO não foram reexecutados após o fail-closed porque o checklist para no gate P0.

## Inventário estrutural read-only — 2026-11-07 11:05 BRT

- [x] Componentes inventariados: Server 5.955 arquivos, Web 1.157, Android 1.744 e Packaging 45.
- [x] Linhas de código observadas: Server 2.824 C# / 477.398; Web 1.015 TypeScript/TSX / 150.368; Android 790 Kotlin / 117.280; Packaging 20 PowerShell / 2.606.
- [~] JSONs/fixtures dominam a contagem bruta de Server/Android; a métrica não deve ser usada como complexidade sem separar produção, testes e configuração. `pygount` não está instalado.

## Métricas separadas de produção, testes e configuração — 2026-11-07 11:10 BRT

- [x] Server: produção 2.329/389.663 linhas; testes 705/135.239; configuração 2.980/1.685.417.
- [x] Web: produção 903/137.687; testes 116/12.844; configuração 140/139.691.
- [x] Android: produção 380/52.706; testes 516/67.840; configuração 999/5.323.457.
- [x] Packaging: produção 23/3.357; testes 6/786; configuração 17/2.727.
- [~] Classificação heurística; métricas de configuração/fixtures não representam complexidade algorítmica e não substituem análise semântica.

## Correção do falso positivo Bearer Token — 2026-11-07 11:15 BRT

- [x] RED: TDD reproduziu 2 findings para um placeholder conhecido e um token não-placeholder.
- [x] GREEN: `security_scan.py` passou a ignorar somente placeholders Bearer explícitos.
- [x] Regressão: scanner TDD 6/6, `py_compile` e `git diff --check` passaram.
- [x] Scanner completo: secrets 0, padrões `innerHTML` 3, críticos/altos 0; checklist global continua exit code 1 por revisão XSS pendente.

## Triagem contextual de `innerHTML` — 2026-11-07 11:25 BRT

- [x] TDD do scanner passou 7/7: 9 atribuições estáticas/limpezas deixaram de gerar falso positivo; construções dinâmicas continuam reportadas.
- [x] As duas construções dinâmicas do GetAvatar foram corrigidas: `escapeHtml` agora codifica também aspas simples/duplas para uso em atributos; TDD DOM com payloads perigosos passou 2/2.
- [~] O scanner ainda reporta as duas linhas por ser heurístico; permanece 1 risco aberto em `MulletaFlix.Plugin.IntroSkipper/Configuration/introskipper.js:1`.
- [x] Alterações de produto limitaram-se à correção reproduzida do escaping de aspas no GetAvatar; nenhum código seguro foi alterado apenas para silenciar o scanner. Relatório: `docs/security/innerhtml-triage-2026-11-07.md`.
- [x] TDD focal do GetAvatar passou 2/2: contratos de escaping para `id`, `name` e URL permanecem presentes nas duas construções dinâmicas.
- [x] Auditoria AST do IntroSkipper concluída: não há source map nem fonte legível no checkout; o único sink `innerHTML` recebe apenas markup literal, `t.description`/`t.warning` de opções construídas por objetos literais e uma descrição computada local. Contrato estático `test_introskipper_sink.mjs` passou 1/1.
- [~] Manter o sink do IntroSkipper em revisão de defesa em profundidade: o scanner continua conservador e o bundle permanece sem fonte/mapa para permitir uma correção estrutural pequena.
- [x] Gate de artefato GetAvatar: o publish self-contained oficial foi refeito em scratch com `dotnet publish ... -r win-x64`; `MulletaFlix.Plugin.GetAvatar.dll` publicado contém `&quot;` e `&#39;`. Um publish anterior em `bin/.../publish` estava obsoleto e não conta como evidência.
- [x] Regressão .NET focal GetAvatar/AvatarController/AvatarFileStore: **44 aprovados, 0 falhas, 3 ignorados**, exit 0; os 3 skips são testes Linux/POSIX e não contam como aceite multiplataforma.
- [x] Smoke browser não autenticado do runtime 12.1.51: login renderiza sem erros de console; `/System/Info/Public`, `/users/public`, `/QuickConnect/Enabled`, `/Branding/Configuration` e splash respondem corretamente. `/System/Configuration/branding` retorna 401 como esperado sem sessão.
- [x] Drift do runtime confirmado: `/GetAvatar/ClientScript` HTTP 200 ainda retorna `escapeHtml` sem `&quot;`/`&#39;`, enquanto source e publish scratch contêm ambos. `/web/ConfigurationPage?name=GetAvatar` exige autenticação HTTP 401. A correção não está aplicada na instalação.

## Histórico: Runtime instalado 12.1.50 GREEN após correção da ordem de migrações — 2026-11-07 01:12 BRT

- [x] Pacote `v12.1.50` aplicado pelo procedimento elevado; payload instalado conferido com `missing=0`, `mismatches=0` e SHA-256 de `MulletaFlix.dll` igual ao pacote.
- [x] Runtime instalado iniciou com processo persistente, listener em `8096`, `/health` HTTP 200 (`Healthy`), `/System/Info/Public` versão `12.1.50` e `StartupWizardCompleted=true`; está executando com o datadir oficial `C:/ProgramData/MulletaFlix/Server`.
- [x] Log confirmou `20260615195427_InitialMySqlCreate` aplicado antes das migrações de código e `Startup complete` às 01:12:47 BRT.
- [~] A correção resolve o startup em schema vazio/parcial, mas a contagem read-only atual do banco é `users=0`, `baseitems=1`. O backup oficial de 04/11 contém `Database/Users.json` e `Database/BaseItems.json` grandes; não declarar preservação até executar o procedimento oficial de restauração/reconciliação, sem restauração destrutiva improvisada.
- [~] Homologação global permanece `PARTIAL`: Android instrumentado, Linux/POSIX, CI hospedada, integrações reais, dados do runtime e aceite formal de produção continuam abertos.

Evidência: release GitHub `v12.1.50`; log `C:/ProgramData/MulletaFlix/Server/log/log_20261107.log`; runtime em `C:/Program Files/MulletaFlix/Server`.

# Regressão ampla GREEN após compatibilidade Vite/Kestrel — 2026-11-05 14:34 BRT

- [~] Probe read-only do runtime instalado em 2026-11-05 21:33 BRT: `MulletaFlix.exe` é 12.1.46, mas `8096` está fechado e `/health` retorna HTTP 000. Iniciar, reparar, reinstalar ou aplicar bundle exige autorização operacional explícita.
- [x] Regressão ampla válida após incluir o TDD movie-only: **21 specs / 40 testes, 40 aprovados, 0 falhas, 0 instáveis e 0 ignorados**, exit code 0, aproximadamente 14,45 min, usando o wrapper isolado, paths explícitos e Vite gerenciado.
- [x] Criado TDD `21-media-recognition.spec.ts` para catalogação movie-only após refresh explícito; passou **3/3 consecutivos**, com `MovieCount=2`, `ItemCount=3` e dois filmes sintéticos `Movie`/`Video`. Isso fecha a pré-condição do stage/harness, não o aceite do scanner em produção.
- [~] AdSense self-player reabriu risco residual: uma execução focal falhou aguardando `.adsenseInterstitialOverlay` por 120 s (**1 falha / 1 ignorado**, 195,2 s), mas **3 rechecagens consecutivas passaram 1/1**. Sem nova correção de produto; investigar timing/lifecycle do player self-managed antes de promover estabilidade.
- [x] Corrigido o runner Playwright para iniciar Vite como `webServer` descartável e direcionar fixtures/módulos de desenvolvimento para uma origem Vite explícita, sem expor `/@fs` no Kestrel.
- [x] TDD `tests/playwright/support/stage.test.mjs`: **3/3**, incluindo a seleção de `PW_FIXTURE_BASE_URL`.
- [x] Regressão ampla válida com `MFLX_MOVIES_PATH` e `MFLX_SERIES_PATH` explícitos: **21 specs / 40 testes, 40 aprovados, 0 falhas, 0 timeouts, 0 instáveis e 0 ignorados**, exit code `0`, aproximadamente **14,45 min**, incluindo `21-media-recognition.spec.ts`.
- [x] Os cenários `24`–`29` passaram **11/11** no runner amplo após a correção; a suíte inteira passou sem alterar produto/runtime instalado.
- [~] Homologação global ainda permanece **PARTIAL** por runtime instalado parado, Android instrumentado, Linux/POSIX, CI hospedada, integrações reais e bundle instalado divergente.

Evidência: `C:/Users/Raphael/AppData/Local/Temp/mflx-playwright-preserved-20261105-full-regression-after-vite-fixture-origin`.

# Regressão ampla após serialização do refresh — 2026-11-05 13:33 BRT

- [x] Reexecução completa com paths explícitos de filmes e séries: **20 specs / 39 testes, 28 aprovados, 11 falhas, 0 timeouts, 0 instáveis e 0 ignorados**, exit code `1`, aproximadamente **15,4 min**.
- [x] Multi-user, AdSense, Unified Search e Pagination passaram; as 11 falhas continuam concentradas nos cenários `24`–`29` por incompatibilidade de fixtures/módulos Vite dev no runner Kestrel.
- [~] Homologação global permanece `PARTIAL`; não converter os 28 aprovados em aceite formal.

Evidência: `C:/Users/Raphael/AppData/Local/Temp/mflx-playwright-preserved-20261105-full-regression-after-refresh-sequencing-with-series`.

# Próximo avanço — serialização do refresh de bibliotecas — 2026-11-05 12:58 BRT

- [x] TDD `tests/playwright/support/media-library.test.mjs` passou para impedir scan automático concorrente no `addVirtualFolder`.
- [x] `21 - Multi user playback` passou **1/1 em duas execuções consecutivas** em stage descartável após a correção do harness; a segunda execução reconheceu 2 filmes sintéticos e iniciou playback para 2 usuários.
- [~] Tratar o resultado como GREEN focal de preparação/harness. Ainda falta repetir a regressão Kestrel completa e obter diagnóstico movie-only reproduzível antes de declarar o scanner do produto resolvido.
- [~] Manter homologação global `PARTIAL` até fechar runtime instalado, Android instrumentado, Linux/POSIX, CI hospedada e integrações reais; a regressão Playwright ampla já está GREEN em 40/40.

Evidências: `C:/Users/Raphael/AppData/Local/Temp/mflx-playwright-preserved-20261105-multiuser-refresh-sequencing-fix` e `...-pass2`.

# Regressão Playwright ampla após GREEN do multi-user — 2026-11-05

## Atualização crítica — catálogo de mídia não determinístico e multi-user — 2026-11-05 12:40 BRT

- [x] Sete execuções instrumentadas independentes do diagnóstico movie-only terminaram **1/1** do ponto de vista do teste, mas somente **1/7** materializou itens: seis retornaram `MovieCount=0`, `SeriesCount=0`, `ItemCount=0` e `getItems=[]`; uma retornou `MovieCount=2` e `ItemCount=3`, com dois filmes sintéticos reconhecidos.
- [x] O `movieFolder` foi criado de forma consistente (`Name=Filmes`, `CollectionType=movies`), portanto o RED está após a criação da biblioteca, no reconhecimento/materialização dos itens ou no timing do scanner; não é ausência do virtual folder.
- [x] Duas novas execuções de `21 - Multi user playback` falharam esperando a segunda sessão (`Expected: 2`, `Received: 0`, timeout de 90 s). O snapshot da falha mostra o contexto secundário ainda na tela de login; evidência preservada em `mflx-playwright-preserved-20261105-multiuser-pathfix` e `mflx-playwright-preserved-20261105-multiuser-hls-instrumented`.
- [x] Uma execução posterior passou **1/1** em 1,5 min, depois de uma rodada em que a mídia foi materializada. O GREEN permanece **não determinístico** e não deve fechar o gate de catálogo nem provar estabilidade do multi-user.
- [~] Próximo TDD deve capturar o contrato de reconhecimento: `addVirtualFolder` + `RefreshLibrary` só podem liberar o cenário quando houver contagem/item real, e devem registrar diagnóstico quando a pasta existir sem itens.

Evidências: `C:/Users/Raphael/AppData/Local/Temp/mflx-playwright-preserved-20261105-instrumented`, `...-instrumented-pass2`, `...-instrumented-pass3`, `...-instrumented-green`, `...-instrumented-green2`, `...-instrumented-green3`, `...-instrumented-green4`, `...-multiuser-pathfix`, `...-multiuser-hls-instrumented` e `...-hls-final`.

## Regressão ampla após GREEN de AdSense — 2026-11-05

- [x] Execução completa atual: **20 specs, 39 testes, 28 aprovados, 11 falhas, 0 timeouts, 0 instáveis e 0 ignorados**, exit code `1`, aproximadamente **15,4 min**.
- [x] Multi-user playback passou **1/1**; AdSense passou **2/2**; Unified Search passou **1/1**; Pagination permanece GREEN focal.
- [~] 11 falhas restantes nos cenários `24`–`29`, concentradas em fixtures/módulos Vite dev não servidos pelo runner Kestrel.
- Evidências: `C:/Users/Raphael/AppData/Local/Temp/mflx-playwright-preserved-20261105-full-regression-after-unified-search`.

## Unified Search GREEN focal após correção do harness — 2026-11-05

- [x] RED reproduzido: `navigateStage` aguardava indefinidamente o `Promise` de `Dashboard.navigate`, embora a hash e a tela de busca já tivessem mudado.
- [x] Correção mínima: o harness aguarda a hash alvo, limita a espera do Promise e propaga erros explícitos.
- [x] TDD `stage.test.mjs`: **2/2**; `23 - Unified search`: **1/1** em 38,7 s.
- [x] Regressão ampla pós-correção: Unified Search não reapareceu como falha; permanecem somente os 11 casos `24`–`29`.
- Evidência: `C:/Users/Raphael/AppData/Local/Temp/mflx-playwright-preserved-20261105-unified-search-green`.


- [x] Execução completa em stage descartável, via PTY, com fixtures sintéticas e `MFLX_MOVIES_PATH`/`MFLX_SERIES_PATH` explícitos.
- [x] Resultado auditável: **20 specs, 39 testes, 24 aprovados, 13 falhas, 0 instáveis e 2 ignorados**, exit code `1`, duração aproximada de **15,5 min**.
- [x] Falhas classificadas sem alterar produção: AdSense (1), scroll buttons (2), My Requests (2), request autocomplete (2), Nebula navigation (2), backup coverage (2), accessibility My Requests (1) e pagination (1).
- [x] Multi-user playback não reapareceu como falha nesta rodada; o gate focal permanece **GREEN 1/1**.
- [~] Próxima ação: triagem independente por contrato/fixture, começando por `24-scrollbuttons.spec.ts` e `99-pagination.spec.ts`; não enfraquecer asserções nem aplicar correção sem RED/GREEN/regressão focal.
- Evidências: `MulletaFlix-web-master/reports/playwright/playwright-summary.json`, `raw-results.json` e snapshot `C:/Users/Raphael/AppData/Local/Temp/mflx-playwright-preserved-20261105-full-regression-after-multiuser`.

## Scroll buttons e pagination — diagnóstico pós-regressão — 2026-11-05

- [x] `24-scrollbuttons.spec.ts` passou **2/2** com `npm run test:playwright:scrollbuttons` em servidor Vite dedicado, em **10,6 s**.
- [x] As duas falhas da suíte ampla foram classificadas como incompatibilidade do runner de stage: a fixture usa `@fs/.../searchresults.tsx` e `/elements/emby-scroller/emby-scroller.ts`, mas o servidor Kestrel do stage devolve 404 para esses endpoints de desenvolvimento. Nenhum patch de produto foi aplicado.
- [x] `99-pagination.spec.ts` teve RED reproduzido e depois passou **2/2** em aproximadamente **2,4 min** após corrigir a navegação duplicada para `/home`, a asserção do nome do log e a consulta de folders do usuário comum.
- [x] O trace de pagination não foi preservado porque continha metadados de autenticação do stage; screenshot, `error-context.md`, summary e raw results permanecem disponíveis.
- [x] Configuração dedicada `npm run test:playwright:requests` passou **8/8**: My Requests, autocomplete, Nebula navigation e backup coverage.
- [x] Configuração dedicada `npm run test:playwright:a11y` passou **3/3**: backup coverage e accessibility My Requests.
- [x] Das 13 falhas brutas da suíte ampla, **11 foram reclassificadas como incompatibilidade do runner/configuração de fixtures**; pagination e AdSense agora possuem GREEN focal.
- [~] AdSense permanece com evidência histórica 2/2, mas a nova execução focal teve 1 falha/1 ignorado e só depois 3 rechecagens 1/1; classificar como GREEN focal não determinístico até isolar o caminho self-managed. Não aplicar bundle ao runtime instalado.

## AdSense playback — TDD GREEN — 2026-11-05

- [x] RED reproduzido: o trace mostrou `intro playback failed, falling back to main item` sem `.adsenseInterstitialOverlay`.
- [x] TDD RED/GREEN em `src/utils/playback/playIntroThenMain.test.ts`: callback de erro assíncrono aguardado antes do fallback; **4/4** aprovados.
- [x] `playbackmanager.ts`: interstitial executado no fallback da intro e nos caminhos de parada de players locais/não locais, com continuidade segura se o script falhar.
- [x] `22 - AdSense playback flow`: **2/2** em aproximadamente **2,4 min**, script normal e falha de script, stage descartável e fixtures sintéticas.
- [x] Bundle reconstruído e copiado somente para `stage/MulletaFlix-web`; runtime instalado `12.1.46` intocado.
- Evidência: `C:/Users/Raphael/AppData/Local/Temp/mflx-playwright-preserved-20261105-adsense-final-async-fallback`.

# Playwright Search/Player — TDD GREEN — 2026-11-05

- [x] RED reproduzido na suíte ampla: clique no link de detalhes interceptado pelo overlay de playback.
- [x] Correção mínima no harness: clique disparado no próprio `<a>` e seletor compatível com `#itemDetailPage`/`#itemDetailsPage` visível.
- [x] GREEN focado: `32-accessibility-search-and-player.spec.ts` **1/1 aprovado** em stage autenticado descartável.
- [~] Reexecutar a suíte ampla para reconciliar a contagem histórica de **22 aprovados, 15 falhas e 2 ignorados**.

Sessão: [[Sessões/2026-11-05 - MulletaFlix Search Player GREEN e regressão Playwright]].

# TDD focal Multi-user playback — HLS lifecycle — 2026-11-05

- [x] RED preservado no `21-multi-user-playback.spec.ts`: o segundo contexto não publicava `NowPlayingItem` nem `POST /Sessions/Playing`.
- [x] Contrato de mídia isolado: o segundo `PlaybackInfo` exige HLS (`SupportsDirectPlay=false`, `SupportsDirectStream=false`, `TranscodingSubProtocol=hls`); `master.m3u8`, `main.m3u8` e segmentos responderam HTTP 200.
- [x] Frontend isolado: o segundo contexto registra `playing url` HLS e carrega `playback/video/index.html`, mas não registra `Active player`, `playbackstart` ou `POST /Sessions/Playing`.
- [x] Hipótese de duração descartada com fixture temporariamente alongada para seis repetições do arquivo de `20.021 s`; o segundo contexto continuou sem `NowPlayingItem`.
- [x] Correção GREEN: eventos HLS locais alinhados para `hlsManifestParsed`/`hlsError`; listener registrado antes de `attachMedia`/`loadSource`; cleanup do player no erro propagado preservado no `playbackmanager`.
- [x] TDD focal `htmlMediaHelper.test.ts` + `playbackmanager.intro-failure.test.ts`: **3/3 aprovados**; `npm run build:check`: passou.
- [x] Regressão E2E final: `21 - Multi user playback` **1/1 aprovado** em stage descartável, PTY, fixtures sintéticas, em **1,5 min**. Bundle copiado somente para `stage/MulletaFlix-web`; runtime instalado não foi alterado.
- [~] Reconhecimento geral de mídia e pagination continuam pendentes; o GREEN de multi-user foi validado com a fixture materializada nessa execução e não encerra os demais gates.

# TDD focal — reconhecimento de mídia no Playwright stage — 2026-11-05

- [x] RED reproduzido: o cenário podia prosseguir após o tempo de settle sem itens reconhecidos, levando a skip de multi-user playback.
- [x] GREEN do harness: `ensureMediaLibrariesReady` aguarda `waitForMediaRecognition`; `media-library.test.mjs` passou **2/2**, incluindo o caso de biblioteca somente de filmes.
- [x] `21-multi-user-playback.spec.ts` passou a solicitar somente `MOVIES_LIBRARY`; séries não são pré-condição do cenário.
- [x] Dois diagnósticos movie-only independentes concluíram **1/1** na porta `18096`, usando fixtures longa e completa: biblioteca criada, mas `MovieCount=0`, `SeriesCount=0`, `ItemCount=0` e `getItems` vazio em ambos; uma rodada histórica reconheceu as fixtures, então a causa do scanner é reproduzível nas rodadas atuais, mas permanece inconclusiva.
- [~] Fixture de 300 s terminou por timeout em `waitForMediaRecognition`, sem criar usuários ou iniciar playback. Uma tentativa posterior de refresh direto foi bloqueada pelo mutex MariaDB do stage. Não classificar o multi-user playback como falha nova de lifecycle até a mídia sintética ser materializada de forma estável.

Sessões: [[Sessões/2026-11-05 - Diagnóstico Multi-user playback]] e [[Sessões/2026-11-05 - Revalidação reconhecimento mídia e multi-user playback]].

# TDD focal de persistência após bloqueio do Item Details — 2026-11-04 17:54 BRT

- [x] `IntroSkipper.Integration.Tests`, filtro `ItemRepositoryTransientRetryTests`: **17 aprovados, 0 falhas, 0 ignorados**.
- [x] Cobertos erros transitórios MariaDB `1205`/`1213`, retry da substituição completa, falha permanente sem retry e preservação de linhas anteriores após o limite de tentativas.
- [x] Regressão MariaDB descartável real `ServerPersistenceMariaDbTests`: **34 aprovados, 0 falhas, 1 ignorado**. A primeira tentativa falhou por `Too many connections` causado pelo limite padrão do scratch; a repetição com `max_connections=512` passou.
- [x] A persistência isolada está verde no foco e na suíte MariaDB; o Item Details stage foi revalidado depois que o helper passou a filtrar `IsFolder=false`/`IncludeItemTypes=Movie` e o spec deixou metadata secundária opcional conforme o frontend.
- [x] TDD/E2E Item Details: spec 33 passou **3/3** em stage autenticado descartável; a suíte Playwright completa ainda registra **22 aprovados, 15 falhas e 2 ignorados**, mantendo a homologação global parcial.

Sessão: [[Sessões/2026-11-04 - MulletaFlix retry de persistência e visibilidade Item Details]].

# Testes focais de RemoteImages e payload — 2026-11-04 12:50 BRT

- [x] `Jellyfin.Api.Tests`, filtro `RemoteImageController`: **5 aprovados, 0 falhas, 0 ignorados**.
- [x] `Jellyfin.Providers.Tests`, filtros `RemoteImagePayloadValidator` e `ProviderManagerRemoteImageValidation`: **9 aprovados, 0 falhas, 0 ignorados**.
- [x] Rodada anterior de `ItemImageProviderTests` + `ImageSaverTests`: **98 aprovados, 0 falhas, 0 ignorados**.
- [x] Contrato de RemoteImages, autorização e validação de payload possuem cobertura focal verde.
- [~] Item Details visual continua bloqueado/inconclusivo: paths sintéticos foram fornecidos em scratch, bibliotecas criadas e `ItemId` normalizado no helper, mas `/Users/{userId}/Items` retornou coleção vazia enquanto o log registrou scanner/IntroSkipper e deadlocks MariaDB.
- [~] Bundle web instalado continua divergente do bundle fonte; aplicação requer autorização operacional explícita.

Sessão: [[Sessões/2026-11-04 - MulletaFlix testes focais de RemoteImages e payload]].

# Auditoria upstream e execução Playwright com PTY — 2026-11-04 05:33 BRT

- [x] Isolado `stdin is not a tty`: runner sem PTY falhava antes de fornecer evidência; execução com PTY completou o stage.
- [x] Playwright Wizard: **2/2 aprovados**.
- [x] Playwright Unified search: **1/1 aprovado (51,5 s)**.
- [x] Servidor: testes focais de imagens **98/98 aprovados** (`ItemImageProviderTests` + `ImageSaverTests`).
- [x] Comparação read-only com Jellyfin upstream: controller local preserva o contrato e acrescenta cancelamento da requisição, validação de URL remota e metadados de provider/idioma; fallback TMDb por título/ano e filtro de idioma são melhorias locais deliberadas.
- [~] Item Details continua bloqueado/inconclusivo mesmo com `MFLX_MOVIES_PATH` e `MFLX_SERIES_PATH` sintéticos; não inferir caminhos nem usar produção. A API autenticada não retornou item utilizável.

Sessão: [[Sessões/2026-11-04 - MulletaFlix Playwright PTY e auditoria upstream de imagens]].

# Homologação E2E: pré-condições de mídia e login — 2026-11-04 05:17 BRT

- [x] Endurecido `tests/playwright/support/admin-user.mjs`: candidatos de usuário/senha agora são procurados dentro do `#loginPage` visível, tolerando o formulário moderno sem IDs legados.
- [x] Endurecido `tests/playwright/support/wizard.mjs`: o teste aguarda e valida a resposta de `POST /Startup/User` antes de prosseguir.
- [x] Corrigido `tests/playwright/support/media-library.mjs`: removidos caminhos hardcoded `D:\\Users\\Raphael\\Videos\\Filmes/Series`; o stage agora exige `MFLX_MOVIES_PATH` e `MFLX_SERIES_PATH` e valida existência local antes de chamar `POST /Library/VirtualFolders`.
- [x] Verificação estática/build: `node --check` dos três helpers, `npm run build:check`, `npm run lint:changed`, `npm run build:production` e `npm run verify:build` aprovados; 1.929 artefatos e 294,3 KB gzip dentro do budget de 420 KB.
- [~] E2E `Item Details` avançou além do login e foi reexecutado com fixtures sintéticas; continua parcial porque a API autenticada responde vazia apesar de logs internos do scanner. Não inferir diretórios nem usar produção.
- [~] A reexecução posterior após adicionar a asserção de `POST /Startup/User` expirou no terminal; não conta como aprovação.

Sessão: [[Sessões/2026-11-04 - MulletaFlix E2E pre-condições de mídia e login]].

# Homologação de palco isolado (14 specs E2E) e alinhamento TMDb TV upstream — 2026-11-04 04:35 BRT

- [x] **Suítes Playwright Stage E2E e UI (`MulletaFlix-web-master`)**:
  - `00-wizard.spec.ts`: **2/2 aprovados (11.5s)**.
  - `01-bootstrap.spec.ts`: **1/1 aprovado (14.1s)**.
  - `10-admin.spec.ts`: **4/4 aprovados (33.0s)** (sincronizada navegação pós-edição de usuário e filtro de alerts de sucesso).
  - `11-admin-extended.spec.ts`: **3/3 aprovados**.
  - `12-admin-surfaces.spec.ts`: **3/3 aprovados**.
  - `20-user.spec.ts`: **3/3 aprovados (29.8s)** (invalidação automática de credenciais residuais com `serverId` distinto em instâncias isoladas).
  - `23-search.spec.ts`: **1/1 aprovado (1.1m)** (resiliência em `logoutViaDashboard` cobrindo hash routes e `#selectServerPage`).
  - `24-scrollbuttons.spec.ts`: **2/2 aprovados (22.1s)**.
  - `25-myrequests.spec.ts`: **2/2 aprovados** (390px e 1280px).
  - `26-request-autocomplete.spec.ts`: **2/2 aprovados** (seleção por teclado e debounce).
  - `27-nebula-navigation.spec.ts`: **2/2 aprovados** (390px e 1280px).
  - `28-backup-coverage.spec.ts`: **2/2 aprovados** (390px e 1280px).
  - `29-accessibility.spec.ts`: **1/1 aprovado** (WCAG 2.2 AA automatizado com `axe-core`).
  - `30-playlists.spec.ts`: **1/1 aprovado (1.2m)** (criação, persistência e navegação com restart real do palco).
  - Total homologado na camada de palco e UI: **14 specs / 29 casos de teste aprovados (0 falhas)**.
- [x] **Endurecimento TMDb TV e Alinhamento Upstream (`MulletaFlix-master`)**:
  - `TmdbSeasonImageProvider.cs` e `TmdbEpisodeImageProvider.cs`: fallback `FindParent<Series>()` para temporadas e episódios desanexados sem navegação relacional ativa.
  - `TmdbEpisodeImageProvider.cs`: null-guard em `series?.DisplayOrder`.
  - `TmdbClientManager.cs`: parâmetros `string? displayOrder` explicitamente anuláveis com `string.IsNullOrEmpty(displayOrder)` em `GetSeriesGroupAsync`, `GetEpisodeAsync` e `GetEpisodeImagesAsync`.
  - Suíte `Jellyfin.Providers.Tests`: **950 aprovados, 0 falhas, 13 ignorados/963**.
- [x] **Contrato e Integridade de Produção**:
  - `node --test portal-site/tests/homologacao.test.mjs`: **367/367 aprovados**.
  - Servidor de produção (`127.0.0.1:8096`), MariaDB de produção (`127.0.0.1:3306`) e MongoDB de produção (`127.0.0.1:27017`) permaneceram 100% operacionais e intocados.

Sessão: [[Sessões/2026-11-04 - MulletaFlix mapeamento e homologacao integrada]].

# Atualização da homologação E2E e resiliência de login — 2026-11-04 03:55 BRT

- [x] Diagnóstico da falha ampliada: wizard do stage reportou `StartupWizardCompleted=true` embora o teste de limpeza exigisse `false`; admin/usuário falharam porque `.btnManual` estava oculto após `showVisualForm()` carregar usuários públicos.
- [x] Corrigido helper E2E `loginWithManualCredentials` em `MulletaFlix-web-master/e2e/support/utils.ts`; `10-admin.spec.ts` e `20-user.spec.ts` usam o fluxo resiliente sem alterar produção.
- [x] `npm run build:check`, `npm run lint:changed`, `npm run build:production` e `npm run verify:build` aprovados; testes focais **6/6**; bundle **1.929 artefatos / 294,3 KB gzip / budget 420 KB**.
- [x] Comparação read-only manual com Jellyfin upstream concluída após falha das duas delegações (`HTTP 401: User not found`): `RemoteImageController` local preserva contrato upstream; fallback TMDb por `Name`/`OriginalTitle`/ano é melhoria intencional para itens sem `ProviderId`; nenhum novo patch de provider/controller indicado.
- [~] E2E autenticado ainda não reexecutado: ambiente não possui `MFLX_ADMIN_PASSWORD`, `MFLX_COMMON_PASSWORD` nem caminhos de biblioteca; não solicitar nem registrar credenciais.

Sessões: [[Sessões/2026-11-04 - MulletaFlix capas imagens idioma e fallback TMDb]]; [[Sessões/2026-11-04 - MulletaFlix imagens TMDb series seasons episodes]]; [[Sessões/2026-11-04 - MulletaFlix auditoria bundle web instalado e imagens]]; [[Sessões/2026-11-04 - MulletaFlix Playwright stage login resilience]]; [[Sessões/2026-11-04 - MulletaFlix comparação Jellyfin upstream imagens]].

- [x] **Qualidade Web (Frontend Quality Gate `W4.4`)**:
  - Corrigido tratamento de exceção em `MulletaFlix-web-master/vite.config.ts` (`sonarjs/no-ignored-exceptions` e `@typescript-eslint/no-unused-vars`).
  - Atualizado `MulletaFlix-web-master/.escheckrc` de `es5` legado para `ecmaVersion: "es2022"`, `module: true` conforme a arquitetura moderna Vite/React 18.
  - `npm run lint:changed`: **4/4 arquivos modificados aprovados (0 erros, 0 avisos)**.
  - `npm run stylelint`: **Aprovado (0 erros)**.
  - `npm run escheck`: **642/642 arquivos de distribuição verificados e 100% compatíveis com ES13/ES2022**.
  - `npm run verify:build`: **1.929 artefatos válidos**, payload inicial **294,3 KB gzip** (dentro do budget de **420 KB gzip**).
  - `npm run build:check`: `tsc --noEmit` concluído com **0 erros** de TypeScript em todo o frontend.
- [x] **Sincronização e Integridade de Stage (`stage/` e `.build/server-publish`)**:
  - Diagnosticada defasagem de binários em `stage/` (estavam estagnados desde 19/10/2026 antes da introdução dos mutexes isolados de MariaDB e suporte a E2E).
  - Publicado `Jellyfin.Server` em Release `net10.0` para `.build/server-publish` e sincronizado com `stage/`.
  - Sincronizados artefatos de runtime de `Tools/NebulaPython` (`upload_checkpoint.py`, `requirements.lock`, etc.) e `MulletaFlix-web/` (dist build).
  - Executado `MulletaFlix-packaging-master/scripts/validate-stage.ps1`: **Stage integrity validation passed: 100% aprovado**.
- [x] **Playwright E2E em Servidor Stage Real (`W4.1`..`W4.4`, `T6.1.22`)**:
  - Diagnosticada restrição de DACL do `BackupRestoreAclValidator` no Windows: o disco `D:\` possui ACE herdada de `Usuários autenticados:(M)` e `%TEMP%` possui `CodexSandboxUsers:(M)`, acionando `UnauthorizedAccessException`.
  - Isolado `stage-data` em diretório temporário limpo dentro do perfil do usuário (`C:\Users\Raphael\.mulletaflix-tmp\mflx-playwright-stage-...`), cujos ancestrais contêm estritamente `Raphael`, `SYSTEM` e `Administradores`.
  - Executado `node ./scripts/playwright/run-suite.mjs --grep="wizard"` contra o stage server real (`12.1.46`) em porta isolada `18096` com MariaDB embarcado na porta `33100`:
    - `ok 1 [chromium] › 00 - Wizard › stage starts clean and wizard pages are reachable`
    - `ok 2 [chromium] › 00 - Wizard › completes the wizard end-to-end (7.8s)`
    - **2 passed (11.5s)** com Chromium real headless!
    - Relatórios salvos em `MulletaFlix-web-master/reports/playwright/playwright-summary.json` e `.md` com status `"success"`.
  - Processo de stage e MariaDB encerrados de forma limpa; servidor de produção (`8096`/`3306`) permaneceu totalmente online e intocado durante todo o teste.

Sessão: [[Sessões/2026-11-04 - MulletaFlix mapeamento e homologacao integrada]].

# Correção de opções de capas e fallback de idioma — 2026-11-04 02:01 BRT

- [x] Diagnóstico read-only do item exibido: `PreferredMetadataLanguage=pt-BR`, `MetadataCountryCode=BR`, ano `1995`, mas sem `ProviderId` TMDb/IMDb persistido; isso fazia `TmdbMovieImageProvider` retornar vazio e a tela mostrar `0-0 de 0`.
- [x] TDD RED/GREEN: `TmdbMovieImageProvider` agora procura o filme por título/ano quando não há IDs válidos e só então consulta posters/backdrops/logos.
- [x] TDD RED/GREEN: refresh automático não inclui mais imagens `en` como fallback para bibliotecas em `pt-BR`; imagens neutras e no idioma preferido continuam elegíveis. Imagens manuais/existentes não foram apagadas.
- [x] Providers: **946 aprovados, 13 ignorados, 0 falhas/959**; testes focais de imagem/idioma **104/104**; build Release da solução **0 erros** (avisos existentes).
- [x] Hotfix aplicado pelo procedimento elevado em `MulletaFlix.dll` e `MediaBrowser.Providers.dll`; hashes do payload e instalação conferidos; runtime reiniciado como usuário normal no PID `38304`.
- [x] Runtime pós-hotfix: versão/ABI `12.1.46`, wizard concluído, `/health` HTTP 200, `Startup complete` no log.
- [~] Aplicação visual ainda requer recarregar a página e repetir a busca/refresh; imagens inglesas já persistidas não são removidas automaticamente para preservar arte manual e evitar mutação destrutiva.
- [~] Homologação autenticada de item real/provedor externo continua parcial; nenhum refresh/download real foi executado nesta sessão.
- [x] Auditoria read-only confirmou que `TmdbSeriesImageProvider`, `TmdbSeasonImageProvider` e `TmdbEpisodeImageProvider` dependiam do `ProviderId` TMDb da série; criado resolver compartilhado por `Name`/`OriginalTitle` e ano, mantendo ID positivo como prioridade.
- [x] TDD focal de série/temporada/episódio: **3/3**; Providers: **950 aprovados, 13 ignorados, 0 falhas/963**; build Release: **0 erros**.
- [x] Novo conjunto reaplicado pelo procedimento elevado oficial; `copy_exit=3`; hashes payload/instalação coincidem; runtime reiniciado no PID `36704`; versão/ABI `12.1.46`; `/health` HTTP 200.
- [~] Nenhum refresh/download autenticado foi executado; validação visual de série/temporada/episódio ainda requer recarga do navegador e item real.
- [x] Auditoria web confirmou divergência de artefato: `dist/index.html` referencia `index-zMNR1fFC.js`; instalação referencia `index-C2qZimXg.js` e contém 1.422 JS contra 639 no `dist` atual. `npm run build:check`, testes focais **6/6**, `npm run build:production` e `npm run verify:build` passaram.
- [~] Bundle web atual ainda não foi aplicado ao runtime; validação visual autenticada permanece pendente.

Sessões: [[Sessões/2026-11-04 - MulletaFlix capas imagens idioma e fallback TMDb]]; [[Sessões/2026-11-04 - MulletaFlix imagens TMDb series seasons episodes]]; [[Sessões/2026-11-04 - MulletaFlix auditoria bundle web instalado e imagens]]; [[Sessões/2026-11-04 - MulletaFlix Playwright stage login resilience]].

# Mapeamento completo das partes não homologadas, lista de tarefas e homologação local de runtime isolado + Playwright/a11y — 2026-11-04 00:30 BRT

## 1. Mapeamento completo do que ainda não foi homologado no sistema

- **Trilha 1 — Servidor Windows/Linux (`server` — formal `2/7` = `28,6%`, `5/7` áreas pendentes)**:
  - **7 itens `in_progress` no roadmap**: `T0.1` (matriz de ambientes de teste), `T0.3` (gates de homologação contínua por entrega), `T2.2` (máquina de estados de transferência Nebula em serviço real), `T4.2` (fluxo unificado de solicitações em produção), `T5.6` (limites de concorrência FFmpeg/Trickplay sob carga real), `T6.1` (métricas Prometheus/OpenTelemetry em runtime), `T6.2` (health checks granulares e degradação controlada).
  - **40 itens `pending` de Servidor/Persistência/Observabilidade no roadmap**:
    - *Dependência externa Telegram/Supabase/Provedores/Produção*: `ROADMAP.005`, `ROADMAP.006`, `T2.1.4`, `T2.1.5`, `T2.3.5`, `T2.3.6`, `T2.4.6`, `T2.5.4`, `T2.6.5`, `T6.4.12`, `T6.4.16`, `T9.5.15`.
    - *Dependência de host Linux/POSIX (`O_NOFOLLOW`/`openat2`/`statx`/bind mounts/POSIX ACL) ou CI hospedada GitHub Actions (billing)*: `ROADMAP.011`, `ROADMAP.015`, `T0.1.5`, `T6.2.15`, `T6.2.19`, `T6.2.21`.
    - *Dependência de carga representativa/benchmark em biblioteca de escala*: `T1.1.3`, `T1.1.4`, `T1.2.4`, `T1.3.4`, `T3.1.4`, `T3.2.5`, `T5.6.3`, `T6.2.11`, `T6.2.12`, `T6.2.13`, `T6.5.9`.
    - *Itens executáveis localmente (homologados nesta rodada)*: `ROADMAP.010`, `ROADMAP.014` (`Jellyfin.Server.Integration.Tests` 146/146 em 26 classes contra MariaDB descartável), isolamento de mutex MariaDB por porta não padrão (`MariaDbProcessManager.ResolveStartupMutexNames`), guarda `SecureConnectionProcessManager.ShouldSkipForIsolatedTestEnvironment` e smoke live isolado de `MulletaFlix.dll` (`18096`/`33188`) validando `/health`, `/System/Info/Public` e redaction de `/metrics` lado a lado com a produção (`8096`/`3306`).
- **Trilha 2 — Frontend Web (`web` — status `monitoring`, 32 itens `pending` em `W1..W4` e `T9.1..T9.5`)**:
  - *Itens executáveis localmente via Playwright + axe-core + bundle budget (homologados nesta rodada)*: `W1.1` (`25-myrequests.spec.ts` e `26-request-autocomplete.spec.ts` em `390px` e `1280px`), `W1.2`/`T4.4` (`27-nebula-navigation.spec.ts` e `28-backup-coverage.spec.ts` em `390px` e `1280px`), `W1.3` (`24-scrollbuttons.spec.ts` teclado, foco visível, LTR/RTL e estado `disabled`), `W2.1`/`W2.2`/`W2.3` (`29-accessibility.spec.ts` WCAG 2.2 AA automatizado com `axe-core` em estados `success`, `loading`, `error` e `empty` em `390px` e `1280px`), `W3.1`/`W3.2` (`npm run verify:build` 294,3 KB gzip / budget 420 KB).
  - *Itens pendentes dependentes de stack E2E completa autenticada com mídia/playback real ou refatoração estrutural ampla*: `W1.5` (consolidação integral de tokens/componentes legados), `W2.4`/`W2.4.1` (legendas/contraste em player real), `W3.3`/`W3.3.1` (virtualização e paginação em bibliotecas gigantes reais), `W4.1`..`W4.5.4` e `T9.1`..`T9.4` (jornadas E2E completas com playback HLS, multiusuário e catálogo real).
- **Trilha 3 — Aplicativo Android APK (`android` — formal `10/21` = `47,6%`, `2` `in_progress`, `9` `pending`, `47` checkboxes abertos em `MulletaFlix-android/TODO-APP.md`)**:
  - *Em validação (`in_progress`)*: `APK-H02` (Autenticação, sessão e troca de conta/servidor — `MainActivitySessionNavigationTest` 12/12 em PHONE/TABLET API 35, pendente servidor e hardware reais) e `APK-H12` (Downloads offline e sincronização — pendente validação em aparelho real).
  - *Pendentes (`pending`)*: `APK-H13` (Live TV e guia EPG real), `APK-H14` (Android TV / D-pad / Leanback em TV física), `APK-H15` (Player nativo ExoPlayer/Media3 com HLS/legendas/áudio real), `APK-H16` (Cast para receiver Chromecast/Web real), `APK-H17` (Downloads sob falha/espaço limitado em aparelho), `APK-H18` (Instalação, atualização e assinatura com keystore de produção), `APK-H19` (Aceite em dispositivos físicos — celular, tablet e TV), `APK-H20` (Aceite formal do responsável pelo produto), `APK-H21` (Leitura de EPUB/CBZ/AZW3 servidos pela instância real).

## 2. Lista de tarefas priorizada e execução nesta sessão

- [x] **Tarefa 1 (Servidor — Concorrência de instância isolada lado a lado com produção)**: `MariaDbProcessManager.ResolveStartupMutexNames(bool requireEmbeddedProcess, int port)` agora usa `"Global\\MulletaFlix.EmbeddedMariaDb.Port.{port}"` quando `port != 3306`, eliminando o bloqueio do mutex global de produção (`3306`) registrado em `server-metrics-runtime-smoke-mariadb-lock-2026-11-03-2335`.
- [x] **Tarefa 2 (Servidor — Isolamento de Nginx/DuckDNS em modo de teste/smoke)**: `SecureConnectionProcessManager.ShouldSkipForIsolatedTestEnvironment` ignora o bootstrap externo quando `MFLX_E2E_TEST_MODE=true` ou `MFLX_DISABLE_EXTERNAL_BOOTSTRAP=true`. `MetricsLabelRedactionTests` ampliado e aprovado **14/14** em Release.
- [x] **Tarefa 3 (Servidor — Smoke live isolado `18096`/`33188` e prova de redaction de `/metrics`)**: `MulletaFlix.dll` (`12.1.46`) iniciado em diretório isolado com MariaDB embarcado na porta `33188` e HTTP na porta `18096` sem tocar a produção (`8096`/`3306`). `/System/Info/Public` respondeu HTTP 200 (`Version=12.1.46`), `/health` respondeu HTTP 200 (`Healthy`), e `/metrics` respondeu HTTP 200 (**326 linhas / 28.317 bytes**) com **0 ocorrências** de `mysqlconnector`, `pool_name`, `User ID`, `Server=` ou `33188`. Instância isolada encerrada e limpa (`Port 18096 listening: False`, `Port 33188 listening: False`).
- [x] **Tarefa 4 (Frontend Web — Correção do scan de dependências Vite + homologação Playwright E2E e WCAG 2.2 AA)**: corrigido `MulletaFlix-web-master/vite.config.ts` (`htmlPlugin.resolveId` retorna `{ id: source, external: true }` quando `options?.scan` é verdadeiro e `resolve.dedupe` unifica `react`, `react-dom`, `@emotion/react` e `@emotion/styled`), eliminando a falha do scan esbuild em `*.template.html` e a duplicação de instância React em dev server.
- [x] **Tarefa 5 (Frontend Web — Execução das 3 suítes Playwright + bundle budget)**:
  - `npx playwright test -c playwright.a11y.config.ts`: **3/3 aprovados** (`28-backup-coverage.spec.ts` 390px/1280px e `29-accessibility.spec.ts` WCAG 2.2 AA `axe-core` em `success`/`loading`/`error`/`empty` em 390px/1280px).
  - `npx playwright test -c playwright.scrollbuttons.config.ts`: **2/2 aprovados** (`24-scrollbuttons.spec.ts` navegação por teclado, `:focus-visible`, LTR/RTL e estado `disabled`).
  - `npx playwright test -c playwright.requests.config.ts`: **8/8 aprovados** (`25-myrequests.spec.ts` 390px/1280px, `26-request-autocomplete.spec.ts` teclado/debounce/stale response, `27-nebula-navigation.spec.ts` 390px/1280px, `28-backup-coverage.spec.ts` 390px/1280px).
  - `npm run verify:build`: **1.929 artefatos válidos**, payload inicial **294,3 KB gzip** (dentro do budget de **420 KB gzip**).
- [ ] **Tarefa 6 (Bloqueios externos / infraestrutura / hardware humano)**:
  - [x] Contrato local do workflow CI revalidado em **15/15** testes estáticos; jobs e filtros esperados estão declarados, mas isso não credita execução hospedada.
  - [x] Probe local reexecutado: `adb` sem devices, WSL sem distribuição, Docker indisponível e `Tools/NebulaPython/venv/bin/python` ausente; nenhum workaround falso foi criado.
  - Resolver bloqueio de billing no GitHub Actions e executar jobs Linux privilegiados (`O_NOFOLLOW`/`openat2`/POSIX ACL/bind mount).
  - Validar provedores externos reais (TMDb, OMDb, Fanart, AudioDB, OpenLibrary) com opt-in `MULLETAFLIX_RUN_LIVE_PROVIDER_TESTS=1` e credenciais válidas.
  - Validar ciclo real Telegram Bot API + Supabase + MongoDB em homologação operacional.
  - Executar matriz Android (`APK-H02`, `APK-H12`..`APK-H21`) em dispositivos físicos (celular, tablet, Android TV com controle D-pad e receiver Cast) e assinar APK de release com keystore de produção.

# Homologação local integrada: MongoDB descartável, MariaDB descartável, LocalMetadata, NebulaPython e Web — 2026-11-03 23:45 BRT

- [x] MongoDB 8.3 descartável (`127.0.0.1:27099`) + Python 3.10 locked venv: corrigido cursor UUID em `NebulaSupabaseRestoreMongoTests.cs`; suíte `Category=RequiresMongo` em `Jellyfin.Server.Implementations.Tests` passou **38/38 (0 falhas, 0 ignorados)** (`NebulaQueueConcurrencyMongoTests` 31/31 com cross-initializer C#/Python, `NebulaSupabaseRestoreMongoTests` 2/2, `NebulaSupabaseBackupBotTokensMongoTests` 2/2, `NebulaPlaybackRecoveryMongoTests` 1/1, `MongoTopologyCapabilityTests` 2/2).
- [x] MariaDB 11.4.4 descartável (`127.0.0.1:33189`): corrigido `BackupRestoreMariaDbTests.CreateDisposableConnection` para encaminhar `disposableInstanceConfirmed: true` a `MariaDbTestEnvironment.Validate`.
- [x] `IntroSkipper.Integration.Tests` contra MariaDB descartável: **96/96 aprovados (0 falhas, 0 ignorados)**, incluindo `BackupRestoreMariaDbTests` **4/4** e `SaveImagesAsync_RetriesAfterMariaDbProcessRestartDuringDelete` **1/1** (com restart real do `mysqld.exe` descartável bloqueado em trigger `BEFORE DELETE`).
- [x] Suítes MariaDB de `Users` em `Jellyfin.Server.Implementations.Tests`: **64 aprovados, 1 ignorado (Linux `lower_case_table_names == 0`), 0 falhas/65**; `MariaDbProcessManagerLifecycleTests` em `Jellyfin.Server.Tests`: **3/3 aprovados**.
- [x] `Jellyfin.Server.Integration.Tests` (`ROADMAP.010` e `ROADMAP.014`) executada por classe isolada contra MariaDB descartável com `mysqldump` no `PATH`: **146/146 casos únicos aprovados (0 falhas, 0 ignorados)** nas 26 classes, incluindo `ImageControllerProfileUploadTests` **1/1** e `StartupBackupRecoveryTests` **1/1**.
- [x] TDD RED/GREEN em `MediaBrowser.LocalMetadata`: `EpisodeLocalImageProvider` compara subpasta `metadata` com `OrdinalIgnoreCase`; `LocalImageProvider` normaliza `Extension.ToLowerInvariant()` ao ordenar por `BaseItem.SupportedImageExtensions`; `LibraryManagerImageConversionTests` passou **6/6**.
- [x] `Tools/NebulaPython` (`pytest` na venv Python 3.10 locked): **726 aprovados, 2 ignorados (POSIX), 0 falhas**; scripts PowerShell de packaging **5/5**; testes Node de release **33/33**; `MulletaFlix-web-master` Vitest **76 arquivos / 421 testes aprovados** e `tsc --noEmit` **0 erros**.
- [x] Instâncias descartáveis `127.0.0.1:27099` e `127.0.0.1:33189` encerradas e diretórios temporários limpos; runtime de produção (`8096`), MariaDB operacional (`3306`) e MongoDB operacional (`27017`) intocados.
- [~] Homologação formal permanece 2/7 (28,6%) até validação em Linux/POSIX, CI hospedada, provedores externos reais e aceite de produção.

# Auditoria comparativa Jellyfin: imagens, capas, subpastas e TMDb — 2026-11-03 22:12 BRT

- [x] TDD RED/GREEN para `MetadataService.RefreshWithProviders`: precedência de `localItem.Images` sobre `localItem.RemoteImages` e preservação de múltiplos backdrops em `localItem.RemoteImages`.
- [x] TDD RED/GREEN para `ItemImageProvider.MergeImages`: helper `IsImageStoredWithMedia` reconhece subpastas (`extrafanart`, `extrathumbs`, `metadata`) e rejeita caminhos relativos/prefixos colidentes.
- [x] TDD RED/GREEN para provedores TMDb (`TmdbPersonImageProvider`, `TmdbMovieProvider`, `TmdbSeriesProvider`, `TmdbSeasonProvider`, `TmdbEpisodeProvider`, `TmdbBoxSetProvider`, `TmdbPersonProvider`, `Tmdb*ImageProvider`): validação `int.TryParse > 0`, idioma `MetadataLanguage` em `FindByExternalIdAsync`, fallback quando `/find` retorna lista vazia e correção de precedência `??`.
- [x] TDD RED/GREEN para `LibraryManager.ConvertImageToLocal` e `ImageController.GetImageInternal`: fallback entre todas as URLs separadas por `|`, preservação de stub em falha transitória (`5xx`/rede) e retorno `204 NoContent` (com tentativa de recuperação de capa de livro) em vez de HTTP 500.
- [x] Evidência Release serial: `Jellyfin.Providers.Tests` **944 aprovados, 13 ignorados, 0 falhas/957**; `Jellyfin.Server.Implementations.Tests` **1.755 aprovados, 95 ignorados, 0 falhas/1.850**; `Jellyfin.Api.Tests` **831 aprovados, 3 ignorados, 0 falhas/834**; build `MulletaFlix.sln` **0 erros**.
- [~] Runtime instalado `12.1.45` validado somente por leitura (`/health` e `/System/Info/Public` HTTP 200); correções estão apenas na working tree e homologação formal permanece 2/7 (28,6%).

# Probe pós-pacote 12.1.46 — 2026-11-03 22:25 BRT

- [x] Probe read-only confirmou processo ativo PID `24732`, versão **12.1.45**.
- [x] `/health`, `/System/Info/Public` e `/Users/Public`: HTTP 200.
- [!] `/metrics`: HTTP 200, **477.526 bytes**, ainda com `pool_name` 158, `User ID` 158, `Server=` 158 e host 160.
- [~] Pacote 12.1.46 ainda não aplicado; homologação pós-release não começou.

# Pacote release 12.1.46 — 2026-11-03 22:17 BRT

- [x] Build Release serial da solução concluído com **0 erros e 4 avisos**.
- [x] ZIP gerado: `dist/mulletaflix-update-win-x64.zip`, 128.092.167 bytes, SHA-256 `B9DA1ED28ED281A8FD89A28EC885B0BD167D61CBF49BEE148E932795D88C70B6`.
- [x] ZIP validado: 2.524 entradas, `MulletaFlix.dll`, `apply-update.ps1`, `Tools/NebulaPython/stream_service.py` e 1.970 entradas web.
- [x] Instalador NSIS gerado: `dist/mulletaflix_12.1.46_windows-x64.exe`, 415.470.877 bytes, SHA-256 `2937C7D3958C75EA156FB381CF5B69CD93E4D677AAE06521F0902E503268734B`.
- [~] Runtime instalado permanece em 12.1.45; aplicação, restart e probe pós-atualização dependem da aplicação manual pelo operador.

# Revalidação runtime e inventário SaveImage — 2026-11-03 19:42 BRT

- [x] Probe read-only: processo ativo, versão **12.1.45**, `/health`, `/System/Info/Public` e `/Users/Public` HTTP 200.
- [x] `/Items?Limit=1` respondeu HTTP 401 sem credencial; nenhuma credencial foi inferida.
- [!] `/metrics` respondeu HTTP 200 com **339.050 bytes** e labels potencialmente sensíveis: `pool_name` 186, `User ID` 186, `Server=` 186, host 188.
- [x] Inventário read-only de `SaveImage`, `SetImageSource`, `RemoteImageInfo` e `ProviderName` não encontrou novo gap confirmado.
- [~] Runtime instalado não contém correções da working tree; validação autenticada de item/capa/refresh continua bloqueada.

# Regressão ampla API — 2026-11-03 19:32 BRT

- [x] `dotnet test tests/Jellyfin.Api.Tests/Jellyfin.Api.Tests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName!~Integration' --verbosity minimal -m:1` passou **830/833**, com **3 ignorados e 0 falhas**.
- [x] Cobertura inclui controllers, autorização, imagens remotas, persistência de imagens, cancelamento HTTP e rotas administrativas.
- [~] Três testes Linux/ACL/concurrency permanecem ignorados; runtime instalado, MariaDB real, CI hospedada e produção continuam sem aceite.

# Regressão ampla Server Implementations — 2026-11-03 18:43 BRT

- [x] `dotnet test tests/Jellyfin.Server.Implementations.Tests/Jellyfin.Server.Implementations.Tests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName!~Integration' --verbosity minimal -m:1` passou **1.753/1.848**, com **95 ignorados e 0 falhas**.
- [x] Resultado cobre ampla área do servidor em Release serial; casos ignorados permanecem explícitos e não contam como aprovados.
- [x] Comparação read-only com Jellyfin upstream revisada para providers, imagens e metadata.
- [~] Não valida runtime instalado, Linux/ACL, MariaDB real, providers externos, item/capa reais ou CI hospedada.

# Contorno seguro MariaDB e locks — 2026-11-03 18:02 BRT

- [x] `ItemPersistenceServiceTests` Release serial passou **39/39**, 0 falhas, 0 ignorados.
- [x] Harness de integração rechecado: exige `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING`, loopback, porta não padrão e confirmação `I_ACKNOWLEDGE_TESTS_CREATE_AND_DROP_SCHEMAS`.
- [x] Variáveis ausentes; binaries standalone conhecidos não encontrados.
- [x] Proxy para `3306` recusado: burlaria o fail-closed e poderia criar/apagar schemas na base operacional.
- [~] Integração MariaDB permanece bloqueada até instância descartável real em porta não padrão; alternativas seguras: Docker/Podman/WSL ou binário standalone.

# Revalidação ampla Providers — 2026-11-03 17:57 BRT

- [x] `dotnet test tests/Jellyfin.Providers.Tests/Jellyfin.Providers.Tests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName!~Integration' --verbosity minimal -m:1` passou **931/931**, 0 falhas, 0 ignorados.
- [x] Resultado cobre evidência local ampla de providers/fallbacks e substitui o filtro focal 84/84 como registro principal.
- [~] Resultado não concede crédito ao runtime instalado nem valida TMDb/item real/capa persistida.

# Revalidação provider/fallback — 2026-11-03 17:54 BRT

- [x] Grafo existente consultado para providers, idioma, fallback e proveniência: **65.443 nós**.
- [x] Código confirma `RemoteImageInfo.ProviderName`/`Language` por imagem; TMDb neutro (`xx`) não recebe idioma solicitado.
- [x] `GetImageLanguagesParam` mantém preferência, `null` e inglês sem duplicar fallback.
- [x] Filtro focal Release passou **84/84**, 0 falhas; build do projeto concluído com **0 erros e 19 avisos existentes**.
- [~] `/Items` do runtime instalado exige autenticação: HTTP **401**; `/Users/Public` HTTP **200** com `[]`.
- [ ] Repetir consulta de item real, capa persistida e refresh após autorização/credencial de homologação; não inferir credenciais.

# Verificação da redaction local de métricas — 2026-11-03 17:44 BRT

- [x] Auditoria confirmou `Jellyfin.Server/Startup.cs` conectado a `MetricsLabelRedactor.ShouldPublishInstrument` no `MeterAdapter`.
- [x] `MetricsLabelRedactionTests` passou **8/8** em Release; o teste cobre remoção de host/porta/usuário e exclusão do meter `MySqlConnector`.
- [~] O binário instalado `12.1.45` ainda expõe labels sensíveis em `/metrics`; teste local não concede crédito ao runtime.
- [ ] Instalar working tree por procedimento controlado e autorizado; repetir probe read-only.

# Probe read-only do runtime instalado — 2026-11-03 17:33 BRT

- [x] Runtime instalado confirmado: `MulletaFlix.exe` versão **12.1.45**, PID **24732**; `/health` e `/System/Info/Public` responderam HTTP **200**.
- [!] `/metrics` respondeu HTTP 200 com **363.566 bytes** e ainda publicou `pool_name` em **158** ocorrências, `User ID` em **158**, `Server=` em **158** e `127.0.0.1` em **160**; valores foram redigidos e não preservados.
- [~] Probe foi somente leitura. A working tree continua sem crédito no runtime; não houve instalação, restart, refresh, alteração de configuração/banco, mutação de mídia, commit, push ou release.
- [ ] Repetir probe após instalação controlada e autorizada para validar redaction de métricas e correções de imagens/metadata.

# Cancelamento em metadata savers — 2026-11-03 16:33 BRT

- [x] RED reproduziu contrato sem token em `IProviderManager.SaveMetadataAsync`; testes focais foram adicionados antes da correção.
- [x] GREEN adicionou overload compatível com `CancellationToken`, encaminha o token a `IMetadataSaver.SaveAsync` nos ramos com e sem arquivo e preserva `OperationCanceledException`; `LibraryManager` usa o overload cancelável.
- [x] Evidência: testes focais `SaveMetadataAsync` **2/2**; `Jellyfin.Providers.Tests` sem Integration **930/930**; regressão serial **5.557 aprovados, 98 ignorados, 0 falhas/5.655 casos**, 15 assemblies; build Release **0 erros e 600 avisos existentes**.
- [~] Correção somente na working tree; runtime instalado 12.1.45, providers remotos reais, CI hospedada, Linux/POSIX, instalação persistente e produção continuam sem aceite.

# Fallback após identificação por provider — 2026-11-03 16:12 BRT

- [x] RED reproduziu `SearchProviderName` filtrando os demais providers; teste falhou porque `OtherProvider` não era chamado.
- [x] GREEN preserva todos os providers e ordena o provider selecionado primeiro; resultado identificado continua prioritário, mas provider vazio/falho permite fallback.
- [x] Atualizados testes `RefreshWithProviders_IdentifySearchProvider_PrioritizesSelectedProvider` e `RefreshWithProviders_IdentifySelectedProviderWithoutMetadata_FallsBackToOtherProvider`.
- [x] Evidência: TDD focal **2/2**; `MetadataServiceTests` **50/50**; `Jellyfin.Providers.Tests` sem Integration **928/928**; regressão serial **5.555 aprovados, 98 ignorados, 0 falhas/5.653 casos**; build Release **0 erros, 594 avisos existentes**.
- [~] Correção somente na working tree; providers remotos reais, runtime instalado, CI hospedada e Linux/POSIX continuam sem aceite.

# Contrato de falha em providers remotos — 2026-11-03 15:55 BRT

- [x] RED reproduziu provider remoto indisponível convertido em coleção vazia por `ProviderManager.GetImages`; teste falhou porque nenhum erro chegava ao caller.
- [x] GREEN preserva falhas por provider, lança `AggregateException` apenas quando nenhum provider retorna imagem e mantém fallback quando outro provider responde.
- [x] `ItemImageProvider.RefreshImages` captura falha agregada como `RefreshResult.Failures`, sem avançar o refresh; `OperationCanceledException` continua propagada.
- [x] Evidência: testes focais ProviderManager **2/2** e ItemImageProvider **1/1**; Providers sem Integration **927/927**; regressão serial atual **5.554 aprovados, 98 ignorados, 0 falhas/5.652 casos**; build Release **0 erros, 594 avisos existentes**; `git diff --check` passou.
- [~] Resultado `5.549/5.647` de execução no-background foi superseded por regressão atual após build; resultado `82/82` cobriu apenas último TRX por sobrescrita de arquivo, não suíte completa.
- [~] Correção somente na working tree; runtime instalado, providers remotos reais, CI hospedada, Linux/POSIX e produção continuam sem aceite.

# Proveniência em imagens dinâmicas — 2026-11-03 15:37 BRT

- [x] RED reproduziu perda de `ProviderName` em imagens HTTP e stream de `IDynamicImageProvider`: testes exigiram `MockDynamicProvider` e falharam com `null`.
- [x] GREEN gravou `provider.Name` em imagens HTTP e nas imagens salvas por stream/arquivo em `ItemImageProvider.RefreshFromProvider`.
- [x] Evidência: testes focais **3/3**; `Jellyfin.Providers.Tests` sem Integration **924/924**; build Release serial **0 erros, 20 avisos existentes**; `git diff --check` passou.
- [~] Correção somente na working tree; runtime instalado `12.1.45`, providers remotos reais, CI hospedada, Linux/POSIX e produção continuam sem aceite.

# Proveniência em image stubs remotos — 2026-11-03 15:18 BRT

- [x] RED reproduziu perda de proveniência quando `RemoteImageInfo.ProviderName` vinha vazio: `SaveImageStub` chamava `SetImageSource` sem o provider atual.
- [x] GREEN passou o `IRemoteImageProvider` aos caminhos singular e múltiplo de `SaveImageStub`; `SetImageSource` agora aplica `provider.Name` como fallback, mantendo `Language` recebido.
- [x] Evidência: `RefreshImages_RemoteStubWithoutProviderName_UsesRemoteProviderName` passou **1/1**; `Jellyfin.Providers.Tests` Release passou **924 aprovados, 13 ignorados, 0 falhas/937**; `git diff --check` passou.
- [x] Tentativa concorrente encontrou lock de `MediaBrowser.Controller.dll` por `testhost`; build Release serial posterior passou com **0 erros e 565 avisos existentes**. Runtime instalado `12.1.45` continua sem a correção.
- [x] Build Release serial repetido após a regressão concorrente; `git diff --check` passou.
- [x] Regressão serial completa sem Integration recuperada do log `mulletaflix-regression-20261103-live-cancel-final.log`: **5.551 aprovados, 98 ignorados, 0 falhas/5.649 casos**, 15 assemblies, exit 0.

# Revisão adversarial do cancelamento Live TV — 2026-11-03 15:18 BRT

- [x] RED reproduziu dois gaps: ausência do método público de três argumentos e cancelamento durante geração de NFO não era relançado.
- [x] GREEN preservou overload de três argumentos, propagou token a `SaveSeriesNfoAsync`/`SaveVideoNfoAsync`, verificou cancelamento após carregar configuração NFO e tratou cancelamento normal no callback assíncrono de `RecordingsManager` sem log de erro.
- [x] Evidência: `RecordingsMetadataManagerTests` **4/4**; projeto `Jellyfin.LiveTv.Tests` **84/84**; build Release da solução **0 erros, 565 avisos existentes**.
- [x] Regressão serial completa sem Integration foi concluída após o patch: **5.551 aprovados, 98 ignorados, 0 falhas/5.649 casos**, 15 assemblies, exit 0.
- [~] Runtime instalado `12.1.45` não contém a correção. Não houve instalação, restart, refresh ou mutação de produção.

# Cancelamento nas imagens de gravações Live TV — 2026-11-03 15:18 BRT

- [x] RED reproduziu a ausência de token em `RecordingsMetadataManager.SaveRecordingMetadata`; o novo teste não compilou enquanto a API pública aceitava somente três argumentos.
- [x] GREEN adicionou `CancellationToken` opcional, propagou o token da gravação em `RecordingsManager`, encaminhou-o a todas as conversões de imagens e preservou `OperationCanceledException` nos ramos de imagem.
- [x] Evidência: `RecordingsMetadataManagerTests` **82/82**; build Release da solução **0 erros, 565 avisos existentes**; `git diff --check` aprovado. Busca de twins por `ConvertImageToLocal` encontrou callers de produção com token; usos sem token restantes estão nos testes de compatibilidade.
- [~] Runtime instalado `12.1.45` não contém a correção. Não houve instalação, restart, refresh ou mutação de produção.
- [ ] PENDING: instalar a correção por procedimento autorizado e repetir probe read-only do runtime — awaiting your authorization.

# Atualização de imagens: propagação de cancelamento — 2026-11-03 14:38 BRT

- [x] RED reproduziu contrato ausente: `ImageControllerIndexTests` não compilava ao exigir `CancellationToken` em `ILibraryManager.UpdateImagesAsync`.
- [x] GREEN adicionou token opcional a `UpdateImagesAsync` e `RunMetadataSavers`; `LibraryManager` encaminha cancelamento a `ConvertImageToLocal` e `SaveImagesAsync`, com verificação por imagem.
- [x] Propagados tokens disponíveis em `ImageController`, `BaseItem.RefreshMetadata`, `Folder` e `UpdateItemsAsync`.
- [x] Evidência: `ImageControllerIndexTests` **3/3**; `LibraryManagerImageConversionTests` **2/2**; `Jellyfin.Api.Tests` sem Integration **830 aprovados, 3 ignorados, 0 falhas/833**; regressão serial sem Integration **5.545 aprovados, 98 ignorados, 0 falhas/5.643 casos observados**; build Release **0 erros, 600 avisos existentes**; `git diff --check` aprovado. O agregado histórico do portal **5.642/5.740** foi preservado, sem mistura de denominadores.
- [~] Runtime instalado `12.1.45` não contém a correção. Não houve instalação, restart, refresh ou mutação de produção.
- [ ] PENDING: instalar a correção por procedimento autorizado e repetir probe read-only do runtime — awaiting your authorization.

# Auditoria — cancelamento na conversão de imagem — 2026-11-03 14:20 BRT

- [x] Adicionar teste TDD para exigir o mesmo `CancellationToken` em `SaveImage` e `UpdateToRepositoryAsync` durante `ConvertImageToLocal`; RED anterior falhou por contrato de interface ausente.
- [x] Alterar `ILibraryManager.ConvertImageToLocal` com token opcional e encaminhar o token no `LibraryManager`.
- [x] Propagar tokens disponíveis em `ImageController` e `GuideManager`.
- [x] Validar `LibraryManagerImageConversionTests`: **2/2**; build Release da solução: **0 erros, 600 avisos existentes**.
- [~] Runtime instalado 12.1.45 não contém a correção; instalação/restart seguem bloqueados sem autorização e procedimento controlado.

# Probe read-only de produção e comparação upstream — 2026-11-03 14:04 BRT

- [x] Runtime instalado confirmado: `MulletaFlix.exe` versão `12.1.45`, PID `24732`; MariaDB empacotado versão `11.4.4`, PID `8792`, ouvindo somente em `127.0.0.1:3306`. `/health`, `/System/Info/Public` e `/metrics` responderam HTTP 200.
- [!] `/metrics` do binário instalado publicou `pool_name` em **158** linhas, com **158** ocorrências de `User ID`, **158** de `Server=` e **188** de `127.0.0.1`; valores foram redigidos e não preservados. Isso corrige a observação anterior de 130 linhas: a correção local não está instalada.
- [x] Consultas MariaDB read-only passaram: versão `11.4.4-MariaDB`, `max_connections=300`, buffer pool `256 MiB`, **0 transações ativas** e três conexões de aplicação em `Sleep` no instante do probe.
- [!] `SHOW ENGINE INNODB STATUS` confirmou o mecanismo do deadlock: uma transação fazia `INSERT` em lote em `BaseItemImageInfos` enquanto outra executava `DELETE ... WHERE ItemId = ...`; ambas disputaram o índice `IX_BaseItemImageInfos_ItemId_ImageType`. MariaDB reverteu a transação de inserção.
- [x] A working tree atual já serializa `SaveImagesAsync` e `DeleteItem` com stripes locais + lock MariaDB, além de retry; o binário instalado 12.1.45 ainda não contém essa proteção nem a redaction de métricas.
- [x] Verificação local serial: `MetricsLabelRedactionTests` **8/8** e `ItemPersistenceServiceTests` **39/39**. Isso não concede crédito ao runtime instalado.
- [x] Comparação read-only com Jellyfin upstream: o upstream atual mantém `DELETE` + `INSERT` em `SaveImagesAsync` sem a serialização/retry local; o refresh upstream preserva prune parcial de backdrops somente após novos downloads. A divergência local é uma mitigação deliberada, não uma incompatibilidade acidental. Fontes: `Jellyfin.Server.Implementations/Item/ItemPersistenceService.cs` e `MediaBrowser.Providers/Manager/ItemImageProvider.cs` no repositório oficial.
- [~] Incidente operacional permanece aberto. Não houve restart, instalação, refresh, alteração de configuração, alteração de banco ou mutação de mídia.

# Log de produção: deadlocks, contenção de arquivos e starvation — 2026-11-03 13:59 BRT

- [x] Analisado log anexado com **5.981 linhas**, **732 entradas estruturadas** no intervalo `07:10:15.464`–`11:28:56.252`: **279 ERR, 312 WRN, 141 INF**.
- [!] P0/P1 operacional: **182** ocorrências de deadlock MariaDB; **124** comandos SQL falharam, repetidamente em `DELETE FROM BaseItemImageInfos`; houve **96** retries de imagens e **29** retries de persistência de itens. O log termina durante novo retry; não prova sucesso posterior nem exaustão.
- [!] P1: **21** avisos de heartbeat do Kestrel, com atraso máximo observado de **1m14,232s**, compatível com starvation/contensão grave durante a carga.
- [!] P1: **59** falhas de gravação de imagens por arquivo em uso, além de falhas de blurhash/dimensões; **16** erros de metadata saver por arquivos temporários MariaDB ausentes (`#sql-temptable-*`).
- [~] **23** autenticações falharam porque `request.AppVersion` chegou vazio; **13** tentativas posteriores foram bloqueadas pelo rate limit. Indício de cliente/probe inválido em loop, não de credencial preservada.
- [x] Não foram encontrados `pool_name`, connection string, senha, `UnauthorizedAccessException`, startup ou shutdown no recorte fornecido.
- [~] O log de produção não contém `processId`/`assemblyVersion` no retry de imagens, enquanto a working tree atual já os registra em `ItemPersistenceService.cs`; isso é consistente com runtime anterior às correções atuais, mas não substitui prova de versão do processo.
- [~] Nenhuma mutação foi executada no runtime. Aceite de produção permanece aberto até correlacionar deadlocks com `SHOW ENGINE INNODB STATUS`, processos/conexões e resultado final dos retries.

# Cancelamento em ItemLookup e regressão sistêmica — 2026-11-03 13:45 BRT

- [x] RED reproduziu `ItemLookupController` ignorando cancelamento HTTP em buscas remotas e `ApplySearchCriteria`.
- [x] GREEN substituiu todos os `CancellationToken.None` do controller por `HttpContext.RequestAborted`; buscas remotas, refresh, fallback de imagem e persistência agora respeitam abort do request.
- [x] TDD focal passou **3/3**; busca confirmou **0** ocorrências de `CancellationToken.None` em `ItemLookupController.cs` e `RemoteImageController.cs`.
- [x] API sem Integration passou **828 aprovados, 3 ignorados, 0 falhas/831**; build Release passou **0 erros**; regressão serial sem Integration passou **5.642 aprovados, 98 ignorados, 0 falhas/5.740**.
- [~] Runtime instalado não contém a correção; providers reais, CI/Linux, instalação persistente e homologação formal global permanecem pendentes.

# Runtime instalado expõe label sensível em `/metrics` — 2026-11-03 13:34 BRT

- [x] Probe read-only confirmou runtime instalado `12.1.45` em `C:\\Program Files\\MulletaFlix\\Server\\MulletaFlix.exe`, PID `24732`; `/health` e `/System/Info/Public` responderam HTTP 200.
- [x] `/metrics` respondeu HTTP 200, mas publicou `pool_name` do MySqlConnector em **130 linhas**; os valores foram redigidos e não foram preservados.
- [x] A working tree contém `MetricsLabelRedactor`; `MetricsLabelRedactionTests` passou **8/8** em Release.
- [~] Correção local ainda não foi instalada. Não creditar runtime 12.1.45; novo probe após instalação autorizada permanece necessário. Nenhuma mutação de produção foi executada.

# Cancelamento HTTP em imagens remotas e regressão sistêmica — 2026-11-03 13:20 BRT

- [x] RED confirmou que `RemoteImageController` enviava `CancellationToken.None` ao lookup de `RemoteImageInfo`.
- [x] GREEN usa `HttpContext.RequestAborted` em `GetRemoteImages` e `DownloadRemoteImage`, incluindo busca de candidatos, `SaveImage` e persistência.
- [x] TDD focal de cancelamento passou **2/2**; `RemoteImageControllerTests` passou; API sem Integration passou **826 aprovados, 3 ignorados, 0 falhas/829**.
- [x] Build Release passou com 0 erros; regressão serial sem Integration passou **5.640 aprovados, 98 ignorados, 0 falhas/5.738**; portal passou **361/361**.
- [~] Runtime instalado 12.1.45, provider remoto real, CI/Linux, instalação persistente, produção e homologação formal global permanecem pendentes.

# Fallback de providers após falha não-HTTP — 2026-11-03 12:57 BRT

- [x] RED reproduziu `IOException` no primeiro candidato interrompendo `RefreshImages` singular.
- [x] GREEN continua para o próximo candidato em falhas não-HTTP e propaga cancelamento sem converter `OperationCanceledException` em sucesso vazio.
- [x] Evidência: teste focal 1/1; Providers sem Integration 923/923; regressão serial sem Integration 5.638 aprovados, 98 ignorados, 0 falhas/5.736; build Release 0 erros.
- [~] Runtime instalado 12.1.45, providers remotos reais, CI/Linux, instalação persistente, produção e homologação formal global permanecem pendentes.

# Download manual de imagens remotas e isolamento da suíte API — 2026-11-03 12:43 BRT

- [x] RED reproduziu perda de `ProviderName`/`Language` em `RemoteImageController.DownloadRemoteImage` após `SaveImage`.
- [x] GREEN resolve `RemoteImageInfo` pela URL/tipo selecionados, copia proveniência para `ItemImageInfo` e mantém persistência via `UpdateToRepositoryAsync`.
- [x] TDD focal: `RemoteImageControllerTests.DownloadRemoteImage_PersistsSelectedRemoteImageProvenance` passou 1/1 após RED.
- [x] Corrida de `BaseItem.LibraryManager` corrigida com coleção não paralela `LibraryManagerTests`; filtro combinado de quatro classes passou 5/5.
- [x] API sem Integration: 824 aprovados, 3 ignorados, 0 falhas/827. Build Release: 0 erros, 576 avisos existentes.
- [~] O runtime instalado 12.1.45 não contém a working tree; falta integração real com repositório/provider remoto e aceite sistêmico global.

# Seleção exclusiva do provider identificado e regressão sistêmica — 2026-11-03 11:28 BRT

- [x] RED reproduziu execução de provider não selecionado quando `SearchResult.SearchProviderName` estava preenchido em `MetadataService.RefreshWithProviders`.
- [x] GREEN restringiu a lista de metadata providers ao provider identificado, com comparação ordinal case-insensitive; sem nome identificado, mantém a lista completa.
- [x] Evidência: teste focal `RefreshWithProviders_IdentifySearchProvider_OnlyRunsSelectedProvider` 1/1; `MetadataServiceTests` 49/49; Providers sem Integration 921/921; regressão serial 5.537 aprovados, 98 ignorados, 0 falhas/5.635; build Release 0 erros/593 avisos existentes.
- [x] Revalidado o caso anteriormente observado como falha `SaveRemoteResultImages_SingularFailureRecoveredByNextProvider_DoesNotRemainFailed`: 1/1 isolado; a falha não reproduziu após serialização.
- [~] Comparação com Jellyfin upstream concluída em modo read-only; a restrição local é mais estrita que a ordenação upstream e ainda precisa de validação com providers remotos reais.
- [~] Homologação sistêmica permanece aberta: runtime instalado 12.1.45 sem a working tree, CI/Linux, instalação persistente, providers reais, segurança, carga, telemetria, autorização ponta a ponta e produção sem aceite formal.

# Rodada de proteção de imagens, referências compartilhadas e fallback inglês — 2026-11-03 10:47 BRT

- [x] Reproduzido em RED e corrigido em GREEN o prune parcial que removia stale sem replacement; refresh agora poda somente replacements efetivos e prioriza stale não-inglês no fallback inglês.
- [x] Reproduzido em RED e corrigido em GREEN o risco de perda de dados por path compartilhado entre tipos/entidades; delete físico exige índice global disponível e ausência de referência em qualquer tipo/entidade. Sem índice ou em erro, fluxo fica detach-only.
- [x] Reproduzido em RED e corrigido em GREEN o erro de filesystem; `IOException` gera warning e não impede `RemoveImages` lógico.
- [x] Coberto branch de stream de provider dinâmico; `ProviderName` é preservado após `SaveImage` em HTTP, arquivo e stream.
- [x] Evidência: `ItemImageProviderTests` 85/85; `Jellyfin.Providers.Tests` sem Integration 920 aprovados, 0 ignorados, 0 falhas; build Release 0 erros/593 avisos existentes; regressão serial 5.536 aprovados, 98 ignorados, 0 falhas/5.634 casos; `git diff --check` aprovado.
- [~] Homologação sistêmica permanece aberta: runtime instalado 12.1.45 não contém a working tree; providers remotos reais, fallback entre múltiplos providers, CI hospedada, Linux/POSIX, instalação persistente, produção, segurança, carga, telemetria e autorização ponta a ponta ainda não possuem aceite formal.

## PR #1 — recuperar logs antes de corrigir — 2026-11-02


- [x] Confirmado PR aberto, head `9a14e93b15e20deabdca159c04f8867d2a8292bb`, 77 commits, 401 arquivos e run `41005367220` com 16 jobs falhos.
- [x] Worktree local solicitado recriado na branch `codex/fail-closed-inplace-updater`; nenhum código alterado.
- [x] Annotations dos 16 jobs do run mais recente e do job de build do run `41005367220` confirmam um bloqueio de billing da conta GitHub; os jobs não iniciaram.
- [ ] Após o operador resolver o bloqueio de billing, repetir a CI; corrigir somente falhas de código demonstradas pelos jobs que efetivamente iniciarem.
- [ ] Homologação formal continua em 2/7 (28,6%). Não criar release ou publicar portal antes da evidência e dos gates.
## Retomada e revalidação após encerramento forçado — 2026-11-02 22:25 BRT

Reabri `main` (`fa1289b6`) e confirmei ausência de processos `dotnet`/`testhost` remanescentes. Os processos Python encontrados pertencem ao Nebula (`main.py`) e à rotina `clean_already_sent.py`; foram preservados. O estado atual contém os retries transacionais em `ChapterRepository`, `MediaStreamRepository` e `PeopleRepository`; a errata abaixo de 21:35 descreve estado histórico anterior às implementações das 21:42–21:58.

`dotnet test MulletaFlix-master/tests/IntroSkipper.Integration.Tests/IntroSkipper.Integration.Tests.csproj --no-restore --configuration Release --filter FullyQualifiedName~ItemRepositoryTransientRetryTests` terminou com exit 0: 17 aprovados, 0 ignorados/falhas, cerca de 12 s incluindo build. Nenhum banco/serviço operacional foi acessado. O teste MariaDB descartável 1/1 das 22:18 é evidência anterior, não repetida. MariaDB documenta que 1205 reverte a instrução por padrão, e não necessariamente a transação inteira; o código descarta contexto/transação e repete a unidade completa. Homologação 2/7 (28,6%), sem crédito; sem release. Linux, CI hospedada, runtime instalado e produção pendentes.

# Retries transacionais de capítulos, streams e pessoas — 2026-11-02 21:58 BRT

Implementados em `ChapterRepository.SaveChapters/SaveChaptersAsync`, `MediaStreamRepository.SaveMediaStreams/SaveMediaStreamsAsync` e `PeopleRepository.UpdatePeople`: cada tentativa refaz a unidade transacional completa com contexto novo, aceita apenas códigos MariaDB 1205/1213, limita a três tentativas com pausas de 150/300 ms e registra warning. Em PeopleRepository, o lock do item é mantido durante o ciclo de retry; construtores de dois argumentos continuam compatíveis.

Validação focal total: `ItemRepositoryTransientRetryTests` passou 17/17 no Windows/.NET 10; cobre 1205/1213, até três tentativas, preservação da relação antiga e não-retry de erro permanente. `dotnet format whitespace ... --verify-no-changes` e `git diff --check` passaram. MariaDB descartável, Linux, CI hospedada, runtime instalado e produção não foram validados. Homologação formal segue 2/7 (28,6%), sem crédito; nenhuma release.

## MariaDB real: retry após lock wait timeout — 2026-11-02 22:18 BRT

O teste `SaveChaptersAsync_RetriesAfterRealMariaDbLockTimeout` agora configura `innodb_lock_wait_timeout=1` por sessão, via interceptor EF Core e factory de contexto exclusiva do teste. Executado em MariaDB 11.4.4 descartável na porta 33179 com timeout global 50 s: obteve 1205, observou o warning de retry, liberou o lock e confirmou a substituição concluída; global permaneceu 50 s. Integração real 1/1; `ItemRepositoryTransientRetryTests` 17/17; format focal e `git diff --check` passaram. Instância descartável foi encerrada. Dados/logs temporários ficaram em `%TEMP%`; MariaDB operacional na 3306 não foi acessado. Sem crédito formal (2/7), sem release; Linux, CI hospedada, runtime instalado e produção pendentes.

## Revisão da evidência de rollback — 2026-11-02 22:04 BRT

O interceptor do teste de `PeopleRepository` agora injeta 1205/1213 no commit, depois das gravações transacionais, em vez de falhar no início da transação. O caso de retry verifica que o mapa antigo foi restaurado antes da tentativa seguinte. Reexecução de `ItemRepositoryTransientRetryTests`: 17/17. MariaDB real segue pendente.

## Rollback pós-escrita em todos os repos — 2026-11-02 22:08 BRT

O teste agora usa o mesmo interceptor de commit para ChapterRepository, MediaStreamRepository e PeopleRepository, nos caminhos síncronos e assíncronos. Isso substitui falhas interceptadas antes do DELETE e comprova recuperação do conjunto anterior após falha depois das gravações. `ItemRepositoryTransientRetryTests`: 17/17; formatter e `git diff --check` aprovados. SQLite simula códigos MariaDB; não substitui o lock real descartável.

# Errata: retries de transações em capítulos e streams — 2026-11-02 21:35 BRT

A conferência do checkout atual (branch main, HEAD fa1289b686bf7130315b6f36ab417064f054e8c9) contradiz afirmações anteriores no roadmap e no vault. `ChapterRepository.SaveChapters`, `ChapterRepository.SaveChaptersAsync`, `MediaStreamRepository.SaveMediaStreams` e `MediaStreamRepository.SaveMediaStreamsAsync` executam DELETE + INSERT em transação sem retry para MariaDB 1205/1213. Callers confirmados: `ChapterManager.SaveChapters` (síncrono); `FFProbeVideoInfo` e `AudioFileProber` usam os caminhos assíncronos. Não há testes desses repositórios no checkout atual. O retry configurado em `ItemPersistenceService` não é aplicado automaticamente a esses contextos.

Registros anteriores que afirmam retries implementados (roadmap, ficha e sessões de 02/11) não são corroborados pelo código e pelos worktrees atuais; tratá-los como desatualizados. A hipótese de que mudanças de código/teste tenham se perdido numa sessão interrompida não foi comprovada. Nenhum código foi alterado nesta auditoria; testes não executados. Próximo passo: reconstruir teste de repetição integral com contexto/transação novos e validar 1205/1213 em fixture, seguido de MariaDB descartável opt-in. Sem crédito de homologação ou release. Fonte de técnica: https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency — retry precisa envolver a transação completa.

# Auditoria NextUp com catálogo ampliado — 2026-11-02 21:28 BRT

- [x] Experimento limitado em MariaDB descartável: fixture de 2.000 episódios fora do filtro; falhas de expectativa do teste documentadas e mudanças experimentais revertidas.
- [x] Contrato do portal validado: 360/360; página local atualizada para 693 eventos, lote externo 3/10.
- [x] Existe evidência limitada de `ANALYZE FORMAT=JSON` em MariaDB 11.4.4 com fixture pequena de 6 séries/19 episódios: query block 0,568 ms; isso não representa escala nem prova ganho.
- [x] A tentativa de ampliar o fixture com 2.000 episódios fora do filtro foi revertida por produzir expectativa inválida e não capturar o plano ampliado; nenhuma lógica de produção mudou.
- [ ] Continua pendente um catálogo/fixture representativo que permita interpretar linhas examinadas, loops e duração sem pressupostos frágeis. Linux, CI hospedada, runtime instalado e produção seguem pendentes; formal 2/7 (28,6%), sem release.

# Regressão ampla da solução do servidor — HEAD fa1289b — 2026-10-27 14:25 BRT

# Auditoria de restore e falha TLS do Nebula — 2026-10-27 18:31 BRT

- [x] `FullyQualifiedName~FullSystemBackup` passou em Windows/.NET 10 Release: 142 aprovados, 16 ignorados, 0 falhas/158.
- [x] Revisão Linux confirmou que o workflow contém job privilegiado Ubuntu e filtro estrito para os testes Linux; esta máquina não tem runtime Linux utilizável, então o job remoto não foi consultado nem executado nesta sessão.
- [x] Log STRM registra 95 falhas `TLSV1_UNRECOGNIZED_NAME`. Dois `.strm` usados como amostra apontam para `cn53.org`; handshake TLS básico para o host passou localmente. Testes Python 3.10 do downloader: 99/99.
- [ ] URL completa e redirect do provedor não foram acessados; runtime instalado que gerou o log não foi encontrado neste host. Causa de TLS permanece sem confirmação e sem correção de código; não enfraquecer validação TLS.
- [ ] Testes Linux/ACL e evidência CI hospedada continuam pendentes. Restore P0 e homologação formal 2/7 (28,6%) permanecem abertos; esta sessão não cria release.

- [x] `dotnet test MulletaFlix.sln --configuration Release --no-restore --no-build --filter 'FullyQualifiedName!~Integration' --verbosity quiet -m:1` terminou com exit code 0 no Windows/.NET 10 Release.
- [x] TRX por projeto reconciliados: 17 relatórios, 5.473 `Passed`, 98 `NotExecuted`, 0 falhas/5.571 resultados. Os 98 não são aprovações; casos de integração com FQN contendo `Integration` ficaram fora do filtro.
- [ ] Linux/ACL, integrações/opt-ins MariaDB e MongoDB, CI hospedada, runtime instalado e produção seguem pendentes. Formal permanece 2/7 (28,6%), sem crédito/release.

# Restore — reexecução atual FullSystemBackup Windows — 2026-10-27 17:07 BRT

- [x] Reexecutado em Release no checkout atual: 142 aprovados, 16 ignorados, 0 falhas/158; exit code 0.
- [x] TRX classifica 14 skips Linux (dirfd, O_NOFOLLOW, statx, POSIX ACL, symlink e mounts) e 2 skips macOS.
- [ ] Linux/ACL/TOCTOU, CI recuperável, runtime instalado e produção continuam sem validação. Os skips não contam como aprovação; restore P0 permanece aberto, homologação formal 2/7 (28,6%), sem crédito/release.

# Restore — confirmação do sinal de encerramento após agendamento — 2026-10-27 14:34 BRT

- [x] O teste `ScheduleRestoreAndRestartServer_SetsRestorePathAndKeepsOperationsBlocked` agora aguarda o callback de `IHostApplicationLifetime.StopApplication()` e verifica uma única chamada após o atraso de agendamento.
- [x] Teste focal Windows/.NET 10 Release passou 1/1; caminho de restore, `ShouldRestart` e bloqueio de operação concorrente continuam verificados.
- [ ] Este teste valida somente o sinal de shutdown. O caso “replay do archive agendado no startup” abaixo cobre duas chamadas do gate no mesmo processo de teste, mas não executa o loop real de StartApp/StartServer. Em produção o host reinicia no mesmo processo por StopAsync/RunAsync; não há relançamento do executável. Linux/ACL, CI hospedada, runtime instalado e produção continuam pendentes; T6.2 parcial, formal 2/7, sem crédito/release.

# Restore — replay do archive agendado no startup — 2026-10-27 14:50 BRT

- [x] `ScheduledRestore_RestoresArchiveBeforeStartupInitialization` liga o agendamento real de `BackupService`, a espera por `StopApplication`, a transferência dos campos estáticos por `Program.CaptureRestartState` (helper chamado pelo `StartServer`) e os helpers `RunAfterBackupRecoveryAsync` + `ContinueAfterBackupRecoveryAsync` do startup seguinte.
- [x] Windows/.NET 10 Release: teste focal passou 1/1; o formatter passou e o contrato do portal passou 358/358. O conteúdo do archive foi aplicado antes da migração/inicialização; o gate interrompe esta passagem e o journal termina removido. O teste restaura os campos estáticos para seus valores prévios.
- [ ] Não executa `Program.Main` nem o loop real de `StartApp`/`StartServer` com o host operacional. O restart de produção repete o host no mesmo processo, não cria processo novo. Linux/ACL, CI hospedada, runtime instalado e produção seguem pendentes; T6.2 parcial, formal 2/7 (28,6%), sem crédito ou release.

# T6.2 — admissão concorrente e exclusão do restore — reauditoria 2026-10-27 14:00 BRT

- [x] `POST /Backup/Restore` e `POST /Backup/RestorePointInTime` agora entram no semáforo global das operações de backup pesado, compartilhado com `POST /Backup/Create` e restores Supabase.
- [x] Testes determinísticos cobrem ambos os endpoints: concorrente recebe `429` com `Retry-After: 1`; playback GET continua; capacidade retorna após liberar a requisição.
- [x] Reauditoria confirmou que `BackupService.TryBeginOperation` mantém a exclusão operacional em `_backupOperationInProgress` do início ao fim de backup/restore assíncrono. `ScheduleRestoreAndRestartServer` retém o gate até o processo ser encerrado; a recuperação de restore mantém o mesmo gate.
- [x] Regressão atual em Windows/.NET 10 Release: `ConcurrentRestoreRequests_AreRejectedWithoutBlockingPlayback` passou 2/2 e `CreateBackupAsync_RejectsConcurrentOperationAndReleasesGateAfterFailure` 1/1. O segundo teste exercita restore/scheduling rejeitados enquanto backup está ativo e liberação após falha.
- [x] `RestoreBackupAsync_RejectsConcurrentBackupUntilRestoreCompletes` bloqueia deterministicamente o restore após criar o sidecar do arquivo original e comprova que `CreateBackupAsync` é rejeitado até o restore concluir. Os três testes focais de gate passaram juntos: 3/3 em Windows/.NET 10 Release.
- [x] Revalidação no HEAD `fa1289b`: suíte `FullyQualifiedName~FullSystemBackup` passou 141, ignorou 16 e falhou 0/157 em Windows/.NET 10 Release; os ignorados são gates de plataforma/privilégio e não validam Linux.
- [ ] O ciclo real de reinício após scheduling, carga representativa, calibração, CI hospedada, Linux, runtime instalado e produção seguem pendentes; T6.2 parcial, formal 2/7, sem release.

# Pessoas órfãs e concorrência com atualização — correção confirmada em 2026-10-27

MariaDB 11.4.4 descartável reproduziu o defeito de limpeza: `DeleteItem` apagava `BaseItems`, a FK removia `PeopleBaseItemMap` por `ON DELETE CASCADE` e a limpeza posterior já não encontrava os `PeopleId` para remover `Peoples` sem referências. O teste `DeleteItem_RemovesPeopleRowsMadeOrphanByDeletedItem` falhou antes da correção e passou após capturar os IDs antes da exclusão.

Também foi reproduzida a corrida independente com `PeopleRepository.UpdatePeople`: o update podia inserir uma pessoa após `DeleteItem` capturar os IDs para limpeza. `UpdatePeople` agora adquire o mesmo stripe local e advisory lock MariaDB, e confirma que o item ainda existe antes de iniciar a transação. Assim, a exclusão e a atualização do mesmo item serializam; updates posteriores à remoção retornam sem gravar mapas/pessoas.

Evidência em Windows/.NET 10 Release: `DeleteItem_ConcurrentWithUpdatePeople_DoesNotLeaveOrphanPeople` passou 1/1 e `DeleteItem_RemovesPeopleRowsMadeOrphanByDeletedItem` 1/1 contra MariaDB 11.4.4 descartável; `UpdatePeople_WaitsForSameItemPersistenceLockBeforeOpeningDatabaseContext` e `ItemPersistenceServiceTests` passaram 1/1 e 39/39. A suíte completa anterior a esta última correção passou 1.738, ignorou 95 e falhou 0/1.833; ela não foi repetida depois do patch concorrente. Contrato CI MariaDB/Linux 4/4 e portal 338/338. A regressão foi adicionada ao filtro do CI. CI hospedada, runtime instalado e produção não validados; homologação formal permanece 2/7 (28,6%), sem release.

# Auditoria histórica: concorrência entre remoção e atualização de pessoas — 2026-10-27 09:49 BRT

A regressão integral `Jellyfin.Server.Implementations.Tests` passou 1.738, ignorou 95 e falhou 0/1.833 em Windows/.NET 10 Release. Auditoria estática identificou escritores distintos de `PeopleBaseItemMap`: `ItemPersistenceService.DeleteItemsUnderLocks` apaga mapas e depois remove registros `Peoples` sem referências; `PeopleRepository.UpdatePeople` altera mapas sem participar das stripes/advisory locks de exclusão. A FK `PeopleId` usa `ON DELETE CASCADE`. O teste MariaDB existente valida `UpdatePeople` sequencial, não essa interleaving.

Classificação original, superada pela evidência acima: a limpeza determinística de órfãos era defeituosa. A hipótese separada de concorrência com `UpdatePeople` continua sem reprodução e sem correção atribuída. Sem acesso a banco ativo. Formal continua 2/7; agregados preservados.

# Corrida ao reconstruir as tags herdadas — 2026-10-27 09:40 BRT

`UpdateInheritedValues` executava um `ExecuteDelete` global em `ItemValuesMap`/`InheritedTags` enquanto `SaveItems` podia inserir linhas para itens. O cleanup agora adquire as 16 stripes locais, em ordem crescente, e os advisory locks MariaDB correspondentes antes de abrir a transação. A implementação usa o protocolo de locks já existente; não cria APIs nem dependências.

Validação: regressão unitária focada 1/1; `ItemPersistenceServiceTests` 38/38 em Windows/.NET 10 Release; contrato do job CI MariaDB/Linux 4/4; formatter whitespace passou; `git diff --check` passou. A integração MariaDB adicionada ao filtro de CI foi ignorada localmente por ausência da variável de conexão descartável. CI hospedada segue sem execução recuperável; runtime instalado/produção não foram verificados. Formal permanece 2/7; não alterar o agregado nem publicar release.

# Restore — regressão completa Windows Release — 2026-10-27 09:21 BRT
- [x] `FullyQualifiedName~FullSystemBackup`: 140 aprovados, 16 ignorados, 0 falhas/156 em Windows/.NET 10 Release; inclui serviço de backup/restore, journal, replay e operações relativas de diretório.
- [ ] Os 16 ignorados exigem Linux/macOS ou mounts privilegiados e não validam esses ambientes. ACL/TOCTOU Linux, CI atual, runtime instalado e produção continuam pendentes; formal 2/7, sem crédito/release.

# Restore ACL — auditoria de disponibilidade Linux/CI — 2026-10-27 09:19 BRT
- [x] Rechecado o host: WSL não instalado; Docker e Podman indisponíveis.
- [x] GitHub Actions público: run CI 39608614450 (21/10, commit `d51f4f38`) está falho; o job “Server build & unit tests” aparece falho sem etapas e a API de logs retorna 404 `BlobNotFound`.
- [ ] Nenhuma execução Linux foi obtida. Não inferir causa do CI nem marcar ACL Linux validada. Restore P0 e homologação formal 2/7 permanecem abertos; sem crédito/release.

# Restore ACL — rename de ancestral no Windows — 2026-10-27 09:13 BRT
- [x] Novo teste `OpenRoot_PreventsRenamingAnAncestorWhileAuthorityIsOpen` confirmou que o handle de ancestral retido durante `OpenRoot` bloqueia `Directory.Move` no Windows.
- [x] Teste focal: 1/1; classe `BackupRestoreDirectoryTests` Release: 45 aprovados, 11 ignorados, 0 falhas/56. Os ignorados são Linux e não contam como validação Linux.
- [ ] Evidência limitada à semântica de handle Windows; TOCTOU entre operações relativas, ACL POSIX, CI hospedada, runtime instalado e produção seguem pendentes. Restore P0 aberto; homologação formal 2/7, sem crédito/release.

# Restore ACL — resistência a rename da raiz no Windows — 2026-10-27 09:09 BRT
- [x] Novo teste `OpenRoot_PreventsRenamingTheRootWhileAuthorityIsOpen` confirmou que o handle retido por `BackupRestoreDirectory.OpenRoot` impede `Directory.Move` enquanto aberto e permite rename após dispose.
- [x] `BackupRestoreDirectoryTests` Release: 44 aprovados, 11 ignorados, 0 falhas/55. Os skips continuam sendo cenários Linux de ACL/syscalls/mount e não contam como validação Linux.
- [ ] Isso valida a semântica de compartilhamento de handle no Windows, não a janela de TOCTOU entre operações relativas, ACL POSIX, CI hospedada, runtime/produção. Restore P0 permanece aberto; formal 2/7, sem crédito/release.

# Regressão ampla e correção do encerramento do Nebula — 2026-10-27 09:00 BRT
- [x] A regressão serial Release da solução encontrou `ObjectDisposedException` em `NebulaPythonCacheLeaseHost.AcceptLoopAsync` ao parar o listener durante `AcceptAsync`; o descarte agora absorve essa exceção somente se o cancelamento já ocorreu.
- [x] `NebulaPlaybackCacheTests`: 60/60; o teste que falhou foi repetido 10/10; suíte completa `Jellyfin.Server.Implementations.Tests`: 1.735 aprovados, 95 ignorados, 0 falhas/1.830.
- [x] Reconciliando TRX da solução (substituindo o TRX inicial da implementação pelo rerun verde): 5.456 aprovados, 98 `NotExecuted`, 0 falhas/5.554 resultados em 17 arquivos; integração marcada foi excluída pelo filtro.
- [ ] WSL/Docker não estão instalados neste host; Linux/ACL, CI hospedada, runtime instalado e produção continuam sem validação. Formal 2/7; auditoria agregada distinta 5.465/5.493 não muda; sem release.

# Revalidação do mutex e do ciclo de vida MariaDB — 2026-10-27 08:48 BRT
- [x] `MariaDbStartupMutexTests` e `MariaDbProcessManagerLifecycleTests` passaram juntos 5/5 em Windows/.NET 10 Release. O teste integrado executou o MariaDB empacotado em porta dinâmica e datadir exclusivo; start-stop-start manteve uma linha inserida.
- [x] Pós-teste: somente PID 31236 segue como `mysqld` e escuta em 127.0.0.1:3306; nenhum diretório `MfxMariaDbLifecycle-*` sob Temp. A instância operacional não foi usada pelo teste.
- [ ] Isso não valida CI hospedada, Linux, a DLL/runtime do servidor instalado nem produção; formal continua 2/7, sem crédito/release.

## Rodada 2026-11-03 — refresh sem operação e runtime instalado

- [x] Corrigir preferência localizada de backdrop: propagar `preferredLanguage` até `RefreshFromProvider` e cobrir fallback HTTP não-imagem; testes focais 1/1 + 1/1, `ItemImageProviderTests` 81/81 após incluir preferência global em item vazio e isolamento de estado estático, Providers 916 aprovados/13 ignorados.
- [x] Limpar arquivo local de backdrop remoto substituído quando `ProviderName` comprova proveniência remota, sem apagar backdrop manual sem proveniência; TDD focal 1/1, `RemoveImages` preserva comportamento não destrutivo.
- [x] Prunar somente a quantidade de backdrops obsoletos que recebeu replacement quando resposta remota falha parcialmente; TDD `RefreshImages_PartialBackdropReplacementKeepsUnreplacedStaleBackdrop` 1/1.
- [~] Arquivos compartilhados entre entidades ainda não possuem verificação global de referências; implementação evita duplicata no próprio item e mantém essa limitação documentada.
- [x] Expandir regressão de backdrops para múltiplos remotos obsoletos: `RefreshImages_ReplacesMultipleStaleRemoteBackdropsBeforePruning` passou 1/1; `ItemImageProviderTests` 75/75.
- [x] Serializar `ImageSaverTests` na coleção não paralela `LibraryManagerTests` após revisão adversarial identificar mutação de estado global; filtro passou 5/5.
- [x] Corrigir substituição de backdrop remoto obsoleto quando existe backdrop manual e limite configurado baixo: TDD RED reproduziu ausência do download com limite 1; GREEN passou 2/2, classe `ItemImageProviderTests` 74/74, Providers sem Integration 909/909 após repetir a suíte e build Release 0 erros.
- [~] A ordem upstream de backdrops ainda coloca candidatos sem idioma antes de candidatos localizados em `RefreshFromProvider`; comportamento é paridade Jellyfin, mas permanece sem homologação de produto para limites/múltiplos backdrops.

- [x] `MetadataService.RefreshMetadata` não avança `DateLastRefreshed` quando `MetadataRefreshMode` e `ImageRefreshMode` são `None`; teste RED/GREEN `RefreshMetadata_NoRefresh_DoesNotAdvanceDateLastRefreshed`, Providers 898/911, build Release 0 erros.
- [x] `ValidationOnly` continua sem avançar `DateLastRefreshed`; teste `RefreshMetadata_ValidationOnly_DoesNotAdvanceDateLastRefreshed` passou junto no filtro focal.
- [ ] Validar correção no runtime instalado: o executável instalado é 12.1.46, está parado e não contém a working tree/bundle atual validados; requer instalação/restart controlado, sem alterar produção automaticamente.
- [~] Probe read-only do runtime instalado revalidado em 2026-11-03 07:30: processo `MulletaFlix.exe` 12.1.45 ativo, `/health` HTTP 200 `Healthy` e `/System/Info/Public` HTTP 200; não credita correções da working tree.
- [~] Regressão serial do servidor sem Integration revalidada em 2026-11-03: **5.532 aprovados, 98 ignorados, 0 falhas, 5.630 casos**; skips de plataforma/opt-in permanecem pendentes.
- [~] `ImageProcessor`, `ImageSaver` incluindo fallback em múltiplos caminhos e streams non-seekable, IDs TMDb inválidos nos providers de imagens, persistência MariaDB de imagens, preferência global/local de idioma, fallback textual e limpeza conservadora de backdrops remotos têm cobertura TDD local concluída; referências compartilhadas entre entidades, corrida de refresh, fallback entre múltiplos providers e validação em runtime instalado continuam pendentes.
- [x] Corrigir colisão de cache entre `PercentPlayed` e `UnwatchedCount`; teste `ImageProcessorCacheKeyTests` 1/1, filtro API/imagens 3/3, build Release 0 erros.
- [x] Corrigir `ImageController.SetItemImageByIndex` para encaminhar `imageIndex`; teste TDD 1/1, filtro `ImageController` 53/53, build Release 0 erros.
- [x] Corrigir perda de `ProviderName`/`Language` em `LibraryManager.ConvertImageToLocal`; teste TDD 1/1, regressão Implementations 1.752/1.847 com 95 ignorados, build Release 0 erros.
- [x] Corrigir retry truncado em `ImageSaver`: reset de streams seekable e buffer de streams não seekable com retry path; `ImageSaverTests` 3/3, Providers 899/912 com 13 ignorados, build Release 0 erros.
- [x] Preservar `ProviderName` nos caminhos HTTP e arquivo de `IDynamicImageProvider`; TDD 2/2, filtro `ItemImageProviderTests` 73/73, Providers 901/914 com 13 ignorados, build Release 0 erros.
- [x] Tratar IDs TMDb inválidos em providers de imagens de séries, temporadas, episódios e box sets com `TryParse`; TDD RED/GREEN 4/4, filtro combinado de imagens/TMDb 36/36, `git diff --check` limpo. Providers de metadata não-imagem seguem fora deste slice.
- [x] Preservar idioma regional explícito em `BaseItem.GetPreferredMetadataLanguage`; remover conversão global `pt-PT`→`pt-BR` e manter normalização somente no adaptador TMDb. TDD RED/GREEN 1/1, `BaseItemTests` 13/13, Providers sem Integration 905/905, build Release 0 erros.
- [ ] Limpar/revalidar caches no runtime instalado somente após instalar/reiniciar build contendo a correção.

# Revalidação TMDB: preferência de idioma e fallback de capas — 2026-10-27 08:45 BRT
- [x] Testes focados `TmdbUtilsTests|ItemImageProviderTests|ProviderManagerImageProviderTests|MetadataServiceTests|TmdbMovieImageProviderFlowTests`: 144 aprovados, 0 ignorados/falhas em Windows/.NET 10 Release.
- [x] Suíte completa `Jellyfin.Providers.Tests`: 884 aprovados, 13 integrações externas ignoradas, 0 falhas/897.
- [ ] Não houve chamada ao TMDb/CDN, inspeção/refresh de *Invasão Zumbi 2*, nem validação de imagem persistida/interface, runtime, produção ou CI. Imagens antigas sem proveniência (`ProviderName` ausente) seguem protegidas de substituição automática por poderem ser arte manual; requerem recuperação administrativa explícita. Formal 2/7, sem crédito/release.

# Restore ACL — revalidação da política padrão escolhida — 2026-10-27 08:41 BRT
- [x] A política já implementada corresponde à escolha: dono UID/serviço e root são confiáveis; escrita por group/other, named users não confiáveis e filesystem compartilhado/desconhecido falham fechados. No Windows, a fonte valida owner/DACL e NTFS/ReFS.
- [x] `BackupRestoreDirectoryTests` Release no Windows: 43 aprovados, 11 ignorados, 0 falhas/54. Os 11 skips detalhados são casos Linux de ACL POSIX, symlink/rename e bind mount; não são aprovações de Linux.
- [x] Contrato do CI `ci-test-runner-serialization.test.mjs`: 2/2; o job Ubuntu privilegiado seleciona os dois testes ACL Linux (named user e other-write) e exige resultado `Passed` para todos os 14 casos esperados.
- [ ] Execução Linux efetiva de ACL padrão/nomeada, syscalls e filesystem/mount continua pendente, assim como CI atual, runtime e produção. Formal permanece 2/7; sem crédito/release.

# NextUp — fusão de leitura dos dados assistidos — atualização 2026-10-27 08:37 BRT
- [x] Com `includeWatchedForRewatching`, a leitura com `UserData` agora calcula numa passagem a posição assistida mais alta e a data mais recente por série. As linhas de mesmo item com `CustomDataKey` distintas são preservadas na comparação da data.
- [x] Sem rewatch, mantém-se o filtro `Any(UserData)` e a seleção anterior. Os filtros de acesso e de episódio/temporada continuam na consulta; o limite de candidatos não mudou.
- [x] `NextUpQueryOptimizationTests`: 8/8 Release. Suíte `Jellyfin.Server.Implementations.Tests`: 1.735 aprovados, 95 ignorados, 0 falhas/1.830, Windows/.NET 10 Release.
- [ ] Tradução/plano MariaDB e benchmark não foram executados. A redução de uma leitura/materialização no ramo de rewatch não prova menor latência em produção; seleção relacional de candidatos, contagem/paginação e homologação formal continuam pendentes (2/7, sem crédito).

# NextUp — requisitos para seleção relacional sem regressão — 2026-10-27 08:09 BRT
- [x] Revisados chamadores e contratos: `GetNextUpBatched` recebe todas as chaves no NextUp geral; `GetResult` calcula `TotalRecordCount` materializando a lista completa antes de aplicar `StartIndex` e `Limit`.
- [x] `GetNextUpEpisodesBatch` aplica filtros de acesso, séries, episódios especiais, virtualidade, reproduzidos e rewatch. A seleção preserva `OrderBy`/`ThenBy` estáveis; o teste diferencial existente cobre posições nulas e empates.
- [ ] Não aplicar `Take` por série ou global antes de contar/ordenar. Qualquer redução SQL precisa definir desempate estável para posições/LastPlayedDate repetidos e manter as semânticas de nullable; medir tradução, plano, linhas lidas, latência e memória em MariaDB.
- [ ] Auditoria estática apenas nesta sessão; sem SQL/MariaDB/benchmark. Homologação formal 2/7 (28,6%), sem crédito.

# Regressão de ordenação de candidatos NextUp — 2026-10-27
- [x] Teste diferencial compara a seleção em passagem única com `OrderBy`/`ThenBy` estáveis, cobrindo múltiplas séries, temporadas/episódios nulos e posições empatadas.
- [x] `NextUpQueryOptimizationTests`: 8 aprovados, 0 ignorados, 0 falhas em Windows/.NET 10 Release; `dotnet format style --verify-no-changes` e `git diff --check` passaram.
- [ ] MariaDB, plano SQL e benchmark não foram executados nesta verificação; seleção relacional e homologação formal permanecem pendentes.

# NextUp — requisito de contagem e paginação — auditoria 2026-10-27
- [x] `NextUpQuery.EnableTotalRecordCount` é `true` por padrão; `TVSeriesManager.GetNextUpBatched` reúne e ordena os resultados antes de `GetResult`, que materializa tudo quando a contagem está habilitada.
- [x] Um limite aplicado antes do cálculo pode mudar `TotalRecordCount`, `StartIndex`, `Limit`, a ordem com rewatch e o conjunto de episódios devolvido. A seleção relacional precisa preservar esse contrato.
- [ ] Planejar a consulta relacional sem truncar o conjunto usado para total/paginação; depois medir carga MariaDB. Esta sessão foi inspeção estática, sem alteração de código.

# NFO do dashboard com falha ao carregar usuários — validação 2026-10-27 08:06 BRT
- [x] A rota `nfo.tsx` depende apenas da configuração para sair do loading; usuários ausentes/erro não bloqueiam o formulário e a opção `Nenhum` continua disponível.
- [x] `nfo.test.tsx`: 3/3 passaram em Vitest; ESLint focal passou. O caso cobre configuração carregada, `useUsers` em erro e API autenticada pelo `ApiProvider`.
- [ ] Teste visual/manual com servidor real não executado. Item da rota web validado localmente; homologação formal do servidor permanece 2/7 (28,6%), sem crédito.

# P1 NextUp — processamento de candidatos em passagem única — atualização 2026-10-27 07:52 BRT
- [x] `NextUpService` não guarda mais listas completas de episódios assistidos/não assistidos, grupos de candidatos ou ordenações por série; mantém somente o candidato escolhido por série durante a enumeração.
- [x] Testes NextUp focais: 6/6 passaram em Windows/.NET 10 Release, incluindo seleção do episódio não assistido mais cedo e rewatch após a posição reproduzida mais recentemente. Suíte completa Implementations: 1.733 aprovados, 95 ignorados, 0 falhas/1.828. `dotnet format style --verify-no-changes` nos dois arquivos e `git diff --check` passaram.
- [ ] As consultas ainda leem as linhas candidatas de todas as chaves; seleção relacional/limite geral e medição de plano, latência e memória em MariaDB continuam pendentes. Nenhum dado de produção acessado; formal 2/7, sem crédito/release.

# Auditoria P1 do lote NextUp — atualização 2026-10-27 07:45 BRT
- [x] Revisados os chamadores: o NextUp geral consulta todas as séries com atividade recente sem limite geral; o fluxo por ID de série limita a uma chave.
- [x] `GetNextUpSeriesKeys` só projeta chaves, mas `GetNextUpEpisodesBatch` materializa todos os episódios assistidos, candidatos não assistidos e datas de rewatch das chaves recebidas; agrupamento/ordenação ocorrem em memória.
- [ ] Projetar seleção relacional dos episódios necessários sem alterar especiais/rewatch; validar tradução e resultados em MariaDB e medir plano, latência e memória. Nenhum código de produto alterado nesta auditoria.

# Deadlock MariaDB repetido em persistência de imagens — atualização 2026-10-27 07:42 BRT
- [x] Anexo SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, igual ao incidente anterior. Repete erro MariaDB 1213 no `DELETE BaseItemImageInfos` para o mesmo item, com chamadas concorrentes; HTTP 200 do pedido não confirma commit de imagens.
- [x] Fonte local serializa `SaveImagesAsync` por stripe local e advisory `GET_LOCK` por stripe de schema/item; regressão `ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests` passou 43/43 em Windows/.NET 10 Release.
- [ ] Preparação de MariaDB descartável foi bloqueada pela política da ferramenta antes da execução; nenhuma instância ou datadir foi criado. Runtime emissor não identificado, CI hospedada sem novo run, produção e Linux pendentes. Homologação formal 2/7 (28,6%) e auditoria 5.465/5.493 sem crédito; sem release.

# Regressão ampla .NET Release — atualização 2026-10-27 07:27 BRT
- [x] `dotnet test MulletaFlix.sln --configuration Release --no-restore --no-build --filter 'FullyQualifiedName!~Integration' --verbosity quiet -m:1 --results-directory <TEMP> --logger trx`: exit 0; 17 TRX, 5.450 aprovados, 98 NotExecuted, 0 falhas/5.548 resultados brutos. Os 15 assemblies com casos passaram; os 2 assemblies Integration não tiveram casos selecionados pelo filtro.
- [x] TRX classificam os 98 NotExecuted: 38 MongoDB opt-in, 32 MariaDB opt-in, 25 plataforma/SO, 2 benchmarks e 1 `/dev/shm`. Não tratados como aprovações.
- [x] Não alterar agregado distinto 5.465/5.493 nem homologação formal 2/7 (28,6%): execução bruta inclui opt-ins e não equivale à união deduplicada das execuções complementares.
- [ ] Linux/ACL, CI hospedada (bloqueio de cobrança), runtime instalado e produção continuam pendentes. Sem alteração de runtime ou release.

# Gate concorrente de imagens e diagnóstico da CI — atualização 2026-10-27 07:11 BRT
- [x] Novo teste MariaDB `SaveItems_ConcurrentWithDeleteItemForSameItem_CompletesWithoutOrphanedImages`: 1/1 em MariaDB 11.4.4 descartável, porta 49387; a classe `ServerPersistenceMariaDbTests` passou 26, ignorou 1 cenário que exige reinício externo e falhou 0/27 com `max-connections=500`. A tentativa inicial com o limite padrão falhou 7 testes por `Too many connections`; foi repetida com limite ampliado no banco temporário. O processo foi encerrado e somente PID 31236 segue em 3306; o datadir descartável ficou em `%TEMP%\mfx-audit-mariadb-20261027` porque a remoção recursiva foi bloqueada pela ferramenta.
- [x] Anotação do GitHub confirmou a causa externa: `The job was not started because your account is locked due to a billing issue.` Run CI `39608614450` não iniciou os 13 jobs; release preflight falhou e Linux/publicação foram skipped. Nenhum teste hospedado executou.
- [ ] A conta GitHub precisa ser desbloqueada pela resolução de cobrança para que CI hospedada produza evidência. Linux/ACL, runtime instalado e produção permanecem sem validação; formal 2/7, auditoria 5.465/5.493, sem crédito/release.

# Persistência MariaDB e startup após restore — atualização 2026-10-27 06:52 BRT
- [x] `StartupBackupRecoveryTests.CreateHost_ReplaysInterruptedRestoreBeforeMigrationsAndCompletesStartup`: recovery/journal, migrations reais, `InitializeServices`, startup tasks e `/System/Info/Public` no host; MariaDB 11.4.4 descartável, teste 1/1.
- [x] Isolamento do schema IntroSkipper por teste e limpeza em teardown; `IntroSkipperMariaDbTests` passou 12/12 e schema padrão de produção não foi removido.
- [ ] Executar gates Linux/ACL e CI hospedada; a investigação do workflow 39608614450 segue pendente, pois a integração não forneceu steps/logs.
- Auditoria formal permanece 2/7; execução Windows local não concede sign-off, instalação ou release.
# Reenvio do deadlock MariaDB em imagens — 2026-10-27 05:59 BRT
- [x] Anexo conferido: SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, exatamente igual ao log já auditado. Erros 1213 ocorrem em `DELETE BaseItemImageInfos` para o mesmo item; o trecho não tem PID, versão nem desfecho.
- [x] `ItemPersistenceServiceTests` passou 37/37 em Release. A fonte atual serializa imagens por stripe local e advisory lock MariaDB; mensagem de retry no log é diferente da atual, logo não comprova que veio desse checkout.
- [ ] Não validar MariaDB concorrente/produção sem banco descartável e identificação segura do runtime. Instalação e produção continuam sem confirmação; formal 2/7, auditoria 5.465/5.493, sem crédito/release.
# Reconciliar DLL instalada e fonte no deadlock de imagens — 2026-10-27 02:04 BRT
- [x] DLL instalada é 12.1.35, gravada em 21/10/2026 10:15:24, SHA-256 `6BCB074F679722F2F1D547E94DC40A81B8BAF90F4261D07235F1340B044C60BD`; saída local .NET Release é 12.1.32, gravada em 27/10/2026 01:54:20, SHA-256 `E3386F4F98F7D0D1DB3D6425BD48F77B7422B420BB27620F4953C24E4730CCE1`.
- [x] Não há processo MulletaFlix/Jellyfin ativo para correlacionar. A DLL instalada não pode ser tratada como build das alterações locais.
- [ ] O log histórico não tem PID/versão/hash; não é possível identificar seu binário emissor. Integrações exigem MariaDB descartável explicitamente configurada; CI hospedada e produção continuam pendentes. Formal 2/7, sem crédito/release.

# Reauditoria dos escritores de BaseItemImageInfos — 2026-10-27 02:03 BRT
- [x] Todos os escritores de runtime encontrados estão em ItemPersistenceService: SaveImagesCoreAsync, UpdateOrInsertItemsCore e DeleteItemsUnderLocks. Cada fluxo coordena as stripes locais e o advisory lock do banco antes das operações transacionais.
- [x] FixBaseItemImageInfos, a rotina de migração restante, adquire as 16 advisory locks antes de alterar as linhas. Testes de integração existentes cobrem refresh concorrente, SaveItems + SaveImagesAsync e DeleteItem + refresh.
- [ ] Integrações MariaDB não foram executadas nesta sessão: MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING não configurada, e os cenários devem usar um banco descartável explícito. O log repetido não identifica um writer fora do processo nem o runtime instalado; CI/runtime/produção pendentes. Formal 2/7, sem release.

# Deadlock de gravação de imagens — registrar exaustão do retry — 2026-10-27 02:00 BRT
- [x] Após a última retentativa transitória, `SaveImagesAsync` registra erro terminal com item, quantidade de tentativas, PID e versão do assembly; a exceção original continua sendo propagada.
- [x] `ItemPersistenceServiceTests`: 37/37 aprovados em Windows/.NET 10 Release; `git diff --check` passou nos dois arquivos tocados.
- [ ] A fixture simula a falha no momento de abrir o contexto; deadlock real sob concorrência MariaDB, correlação do runtime instalado, CI hospedada e produção continuam pendentes. O log anexado permanece duplicado e não prova falha da fonte atual. Formal 2/7 (28,6%), sem crédito/release.

# B-5 — validar limite LRU do stream Nebula — 2026-10-27 01:12 BRT
- [x] Corrigida documentação stale: o limite atual já é 8 chunks (~8 MiB por stream cheio), não 64 MiB.
- [x] Adicionada regressão que lê nove chunks e confirma nova busca do primeiro após eviction LRU; teste focal Release passou 1/1.
- [ ] Medir memória agregada/runtime em carga; runtime instalado/produção seguem sem prova. Homologação formal 2/7, sem crédito/release.

# B-4 — concorrência ao abrir streams Live TV — 2026-10-27 01:12 BRT
- [x] Provider fornece chave de concorrência; default usa o token, Live TV usa ID da fonte compartilhável. O lock global de ownership não cobre a chamada de rede. A revisão adversarial refutou a primeira versão só por token; a implementação final coordena tokens distintos que compartilham uma fonte.
- [x] Testes determinísticos confirmam paralelismo de chaves diferentes e que tokens distintos com a mesma chave veem/reutilizam o stream registrado; `MediaSourceManagerTests` 33/33 e mapeamento da chave Live TV 3/3.
- [ ] Validar limite de tuners por host com fontes diferentes, providers Live TV reais, carga e runtime/produção. Homologação formal segue 2/7 (28,6%), sem crédito/release.

# B-3 — reduzir contenção por item na persistência de progresso — 2026-10-27 00:51 BRT
- [x] `UserDataManager.SaveUserDataAsync` usa `AsyncKeyedLocker<Guid>` por `ItemId`; operações de itens diferentes deixam de competir pela mesma stripe.
- [x] Regressão determinística pausa uma gravação e prova que outra, com colisão na antiga stripe, termina sem esperar; regressão `UserDataManagerTests|SessionManagerTests` passou 15/15 Release.
- [x] `dotnet format style` direcionado passou nos dois projetos. Interface e semântica da transação/cache/evento/reidratação preservadas; `UserDataManager` singleton agora descarta o locker.
- [ ] B-3 parcial: medir a reconsulta de todas as linhas para `item.UserData`, executar perfil/carga MariaDB e validar runtime/produção; formal permanece 2/7, sem crédito ou release.

# Reauditoria da telemetria de respostas lentas — 2026-10-26 23:20 BRT
- [x] `ResponseTimeMiddlewareTests` passou 7/7 em Windows/.NET 10 Release; `Information` funciona com Debug desligado, com redação, supressão e teto do mapa cobertos.
- [x] O P0 O-1 está resolvido na fonte/testes. A auditoria de performance foi corrigida, sem duplicar o defeito como pendente.
- [ ] Binário/configuração/log de produção não verificados. Homologação formal segue 2/7 (28,6%), sem crédito/release. Ver vault [[Sessões/2026-10-26-MulletaFlix-slow-response-telemetry-reaudit-2320]].
## Restore — reexecução após morte do processo — 2026-10-26 23:18 BRT

`dotnet test MulletaFlix-master/tests/Jellyfin.Server.Implementations.Tests/Jellyfin.Server.Implementations.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~BackupServiceTests.RestoreBackupAsync_RecoversAfterRestoreProcessIsTerminated` passou 1/1 em Windows/.NET 10 Release. O teste existente cria ZIP com manifest e payload de 64 MiB, encerra abruptamente o processo filho após criar staging, confirma journal/temporário, inicia outro processo para `RecoverInterruptedRestoreAsync`, compara todo o payload restaurado e verifica limpeza do journal/sidecars. Isto prova replay real em processo filho nesse cenário Windows. Não valida ACL POSIX, inicialização integral por `Program` nem integração HTTP no mesmo ciclo. Homologação formal segue 2/7 (28,6%), sem crédito/release. Ver evento portal `server-restore-process-crash-replay-2026-10-26-2318` e sessão `2026-10-26-MulletaFlix-restore-process-crash-replay-2318`.
## Restore — rota HTTP, agendador e shutdown do TestServer — 2026-10-26 23:12 BRT

`BackupControllerAuthorizationHttpTests` passou 2/2 em Release com `BackupService` concreto. Via `POST /Backup/Restore`, autenticação Administrator, verificou 204, caminho sanitizado, `ShouldRestart`, notificação ao host e encerramento do host `TestServer` via `IHostApplicationLifetime` real. `ProgramStartupRecoveryTests` passou 8/8 separadamente e prova recovery→initialize pelo probe subprocessual. O HTTP usa um arquivo de texto dummy: não valida ZIP, replay, restart de processo nem reabertura do servidor real. Avisos do build são preexistentes em outros arquivos do projeto de testes. Homologação formal 2/7 (28,6%), sem crédito/release. Ver evento portal `server-restore-http-entrypoint-test-2026-10-26-2312` e sessão `2026-10-26-MulletaFlix-restore-http-startup-recovery-test-2312`.

## Gate privilegiado Linux restore — contrato de seleção — 2026-10-26 23:08 BRT

`node --test tools/release/tests/ci-test-runner-serialization.test.mjs` passou 2/2. O contrato verifica os 14 nomes únicos do filtro do workflow contra a lista esperada do TRX e confirma que os 14 métodos estão declarados como xUnit `[Fact]`/`[Theory]` em `BackupRestoreDirectoryTests.cs`/`BackupServiceTests.cs`. Isto valida a ligação estática, não a execução Linux. WSL não tem distribuição; Docker e Podman não estão disponíveis. O Actions público mais recente observado (CI `39608614450`, commit `d51f4f38d2b4148ab49394c001567ad273bd57aa`) falhou e não expôs passos/logs do job servidor; portanto não é possível concluir que o gate correu. O checkout local `64a33dcf5595fb6f429c65206be20dbdd4ae0bd0` está oito commits atrás e a mudança local não está no CI remoto. Também falta teste integrado HTTP→agendamento/restart→recuperação no startup. Homologação formal 2/7 (28,6%), sem crédito/release.
# Revalidação do deadlock repetido em imagens — 2026-10-26 23:02 BRT

- [x] SHA-256 53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2 confirma que o anexo repete log anterior; não traz identidade do processo, resultado final do retry nem relatório InnoDB.
- [x] Fonte atual mantém stripe estática por ItemId e advisory MariaDB antes da transação; testes unitários focais passaram 52/52 em Windows/.NET 10 Release.
- [ ] Integração MariaDB nesta sessão, correlação do runtime emissor, CI hospedada e produção pendentes. Não alterar os agregados: auditoria 5.465/5.493; formal 2/7 (28,6%). Sem release/publicação remota. Ver vault [[Sessões/2026-10-26-MulletaFlix-image-deadlock-duplicate-rerun-2302]].

# Nebula STRM — revalidação do pacote Linux — 2026-10-26 22:56 BRT
- [x] O log de deadlock repetido não é o escopo desta verificação. Auditoria confirma que `control_plane._generate_strm()` importa `generate_strm.py`, já incluído pelo copiador e verificado pelo integrador do stage.
- [x] `tools/release/tests/linux-nebula-runtime-package.test.sh`: passou; pacote copiado contém `generate_strm.py`.
- [x] Python 3.10, em `MulletaFlix-master/Tools/NebulaPython`: `test_control_plane_operations.py` + `test_generate_strm.py`, 27 aprovados.
- [ ] Sem teste de pacote instalado, MongoDB real, mídia real ou CI Linux hospedada. Homologação formal continua 2/7 (28,6%), sem crédito/release.
- Portal atualizado com evento `server-nebula-strm-linux-package-audit-2026-10-26-2256`; contrato `node --test portal-site/tests/homologacao.test.mjs` passou 318/318 após atualização. Mapa conferido, sem mudança estrutural. Ver sessão `2026-10-26-MulletaFlix-nebula-strm-package-audit-2256`.

# Restore P0 — reexecução focal Windows — 2026-10-26 22:52 BRT
- [x] Anexo de deadlock comparado por SHA-256: `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, idêntico ao incidente já auditado.
- [x] `dotnet test MulletaFlix-master/tests/Jellyfin.Server.Implementations.Tests/Jellyfin.Server.Implementations.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~FullSystemBackup' --logger 'console;verbosity=minimal'`: 136 aprovados, 16 ignorados, 0 falhas/152 em Windows/.NET 10 Release.
- [ ] Os 16 ignorados incluem cenários Linux/macOS; WSL não tem distribuição instalada. Gate Linux privilegiado hospedado, runtime emissor do log e produção continuam pendentes. Formal 2/7 (28,6%), sem crédito/release.
- Portal atualizado com evento `server-restore-windows-focused-rerun-2026-10-26-2252`; contrato `node --test portal-site/tests/homologacao.test.mjs` passou 318/318; JSON e IDs únicos validados; `git diff --check` passou com avisos apenas de normalização de fim de linha. Mapa técnico conferido, estrutura inalterada. Ver sessão `2026-10-26-MulletaFlix-restore-windows-focused-rerun-2252`.

# T5.6 — teste permanente do ciclo MariaDB embarcado — 2026-10-26 21:52 BRT
- [x] Adicionado teste de regressão start-stop-second-start com porta dinâmica, schema aleatório exclusivo e datadir temporário; a linha de controle foi confirmada após o segundo start; a inicialização falha fechada se o processo da porta não pertencer ao teste; cobre o MariaDbProcessManager real em Windows.
- [x] CI Windows baixa/stageia MariaDB 11.4.4 ao lado do assembly e exige exatamente um resultado Passed no TRX; contrato Node verifica job, runtime, filtro e fixture.
- [x] Validação local Windows/.NET 10 Release: focal 1/1; suíte Jellyfin.Server.Tests com runtime stageado: 87/87 Passed, 0 NotExecuted, 0 Failed; sete contratos CI 16/16; portal 312/312; git diff --check aprovado.
- [ ] CI hospedada, runtime instalado, upgrade de schema sob carga e produção pendentes. Evidência suplementar: auditoria agregada preservada em 5.465/5.493; homologação formal segue 2/7 (28,6%), sem crédito/release.
# T5.6 — smoke do lifecycle real do MariaDB embarcado — 2026-10-26 21:45 BRT
- [x] Start a frio, stop gracioso e segundo start concluíram 1/1 usando os binários MariaDB locais, porta efêmera e datadir exclusivo temporário; processo e dados temporários foram removidos/verificados.
- [x] `MariaDbStartupMutexTests` passou 4/4 em Windows/.NET 10 Release. O smoke C# era temporário e foi removido; nenhuma fonte de produção foi modificada.
- [ ] Não valida upgrade de schema sob carga, CI hospedada, pacote/runtime instalado completo nem produção; formal continua 2/7 (28,6%), sem crédito ou release.

# Reauditoria do deadlock de imagens e ACL do restore — 2026-10-26 21:29 BRT
- [x] SHA-256 do anexo `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2` confirma que é o mesmo log histórico; sem PID/assembly nem desfecho dos retries. A fonte atual mantém lock por item e advisory `GET_LOCK` antes da transação; isso não identifica o runtime que emitiu o log.
- [x] Cavecrew revisou a exceção Windows para pais do namespace. Ela já está documentada na decisão de 24/10: permite somente criar subdiretórios sob handles validados; nega criação de arquivos e direitos de substituição. O diretório final segue a regra estrita. Não reverti essa exceção sem evidência de violação do contrato aprovado.
- [x] `BackupRestoreDirectoryTests` Windows/.NET 10 Release: 39 aprovados, 10 ignorados por plataforma, 0 falhas.
- [ ] Linux nativo, CI hospedada, runtime emissor do log e produção continuam sem evidência; homologação formal 2/7 (28,6%), sem crédito ou release.
- Portal atualizado; contrato de homologação executado após o registro. Mapa técnico conferido, sem mudança estrutural. Ver sessão `2026-10-26-MulletaFlix-deadlock-and-restore-acl-reaudit-2129`.

# T5.6 — auditoria de writers e locks das imagens — 2026-10-26 20:53 BRT
- [x] Revisados todos os writers de produção de `BaseItemImageInfos`: `SaveImagesAsync`, `SaveBaseItemEntities` dentro de `UpdateOrInsertItemsCore`, deleção de itens e migração `FixBaseItemImageInfos`. Nenhum writer de runtime foi encontrado sem sincronização local e advisory MariaDB.
- [x] A migração `FixDates` não usa stripes locais, mas toma os 16 advisory locks MariaDB. Em providers sem advisory locks, seu estágio `CoreInitialisation` ocorre antes de `InitializeServices`, portanto a inicialização normal precede os writers da biblioteca.
- [ ] Runtime instalado, correlação com log novo, CI hospedada e produção continuam pendentes; esta auditoria estática não prova o comportamento do binário instalado.
- Sem alteração de código; contrato do portal passou 304/304. Agregados permanecem 5.465/5.493 e formal 2/7 (28,6%), sem crédito ou release. Ver sessão `2026-10-26-MulletaFlix-image-persistence-writer-lock-audit-2053`.

# T5.6 — reenvio de deadlock sem processo servidor ativo — 2026-10-26 20:50 BRT
- [x] SHA-256 do anexo `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2` confirma que é o mesmo log de 23/10, sem evidência nova; erro MariaDB 1213 continua em DELETE concorrente de `BaseItemImageInfos`, e tentativas não têm desfecho no trecho.
- [x] Inspeção local de processos/serviços não encontrou processo MulletaFlix/Jellyfin ativo. Nenhum banco foi acessado e nenhum serviço foi iniciado ou reiniciado.
- [ ] Sem PID/assembly histórico, não é possível atribuir o log à DLL instalada 12.1.35 ou à fonte local. Runtime, correlação com log novo, CI hospedada e produção seguem pendentes.
- Agregado técnico permanece 5.465/5.493; homologação formal 2/7 (28,6%), sem crédito. Nenhuma release ou instalação.

# Regressão ampla e testhost da API — 2026-10-26 20:33 BRT
- [x] A mesma regressão, serializada com `-m:1`, terminou exit 0: 5.401 aprovados, 97 ignorados, 0 falhas/5.498 em 15 assemblies com testes; dois assemblies Integration foram filtrados sem casos selecionados.
- [x] TRX classifica ignorados: 38 exigem Mongo opt-in, 32 MariaDB opt-in, 25 plataforma/SO, 2 benchmarks. CI define jobs dedicados para Mongo e MariaDB; último run público não chegou a executar steps.
- [x] O crash CLR `0x80131506` apareceu na execução sem serialização; API passou isolada 792/795. A causa não foi determinada. CI unit-test passa a usar `-m:1`, modo que passou localmente; teste de contrato da configuração passou 1/1.
- [x] Contrato portal 304/304. Auditoria agregada 5.465/5.493 e homologação formal 2/7 (28,6%) mantidas.
- [ ] CI hospedada no checkout atualizado segue pendente: último run disponível falhou antes dos steps. Runtime DLL 12.1.35 ainda difere do checkout 12.1.32; log não tem versão/hash emissor.
- Nenhum banco operacional/runtime foi alterado; sem release ou publicação remota. Mapa técnico conferido, estrutura inalterada.

# T5.6 — divergência do runtime instalado para persistência de imagens — 2026-10-26 20:16 BRT
- [x] Inspecionada somente leitura a DLL em `C:\Program Files\MulletaFlix\Server`: 12.1.35, SHA-256 `0D9465B7EC1549CB70C063A2B7B199E8A6B645F6E454CDBFD008A8D0B3F9D30D`; não contém marcadores de advisory lock nem da nova mensagem de identidade de runtime. Checkout declara 12.1.32 e a fonte contém os dois.
- [x] Sem processo MulletaFlix/Jellyfin ativo; serviço Nginx do MulletaFlix ativo. Nenhum banco acessado ou serviço reiniciado.
- [x] Portal atualizado; contrato `node --test portal-site/tests/homologacao.test.mjs` passou 303/303; JSON válido, sem IDs de evento duplicados; diff check passou.
- [ ] Divergência não associa essa DLL ao processo que emitiu o log e não prova que o retry histórico falhou. Precisamos de build/release compatível e identificação do assembly emissor antes da validação de instalação.
- Evidência publicada no contrato local do portal. Auditoria técnica 5.465/5.493 e formal 2/7 (28,6%) permanecem iguais; sem crédito ou release.

# Reenvio repetido do deadlock de imagens — 2026-10-26 20:14 BRT
- [x] Anexo SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2` é o mesmo log de 23/10: MariaDB 1213 ao apagar `BaseItemImageInfos` sob concorrência; sem PID/assembly ou desfecho das retentativas.
- [x] Reexecução `ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests`: 50 aprovados, 0 ignorados, 0 falhas/50 em Windows/.NET 10 Release. Não acessou banco operacional.
- [x] Portal homologação atualizado; `node --test portal-site/tests/homologacao.test.mjs`: 302 aprovados, 0 falhas.
- [ ] Correlação com versão/hash do runtime instalado, CI hospedada atual e produção pendentes. Não há evidência nova de falha após a correção local.
- Evidência suplementar; não somar ao agregado 5.465/5.493. Homologação formal 2/7 (28,6%), sem crédito nem release.

# T5.6 — ownership do mutex no lifecycle async do MariaDB — 2026-10-26 20:05 BRT
- [x] Corrigida a liberação cross-thread: uma thread proprietária dedicada mantém e libera o Mutex nomeado após o shutdown; aquisição mantém suporte a mutex abandonado, fallback e timeout limitado.
- [x] Testes direcionados: 4/4 (ownership após continuação async, chamadas concorrentes de dispose, timeout, cancelamento e abandono); projeto `Jellyfin.Server.Tests` Release: 86/86.
- [x] Contrato do portal 301/301; `dotnet format whitespace` direcionado nos dois projetos e `git diff --check` nos arquivos alterados passaram.
- [ ] Sem runtime instalado, CI hospedada ou produção nesta verificação. Formal 2/7 (28,6%); sem release; auditoria agregada não muda.

# T5.6 — upgrade de schema e persistência MariaDB — 2026-10-26 15:42 BRT
- [x] Novo teste `LatestImageProvenanceMigration_PreservesExistingImageRows`: migrou de `20260926050701_DropUnusedFullTextSearchIndex` para `20261022212236_AddRemoteImageProvenance` e confirmou a preservação do registro de imagem. Passou 1/1 em MariaDB 11.4.4 descartável.
- [x] `ServerPersistenceMariaDbTests`: 24 aprovados, 1 ignorado, 0 falhas (25 total). O skip é o restart sem marcadores nesta execução; restart real continua validado separadamente 1/1.
- [x] MariaDB de teste encerrado; porta 33218 fechada e instância 3306/PID 31236 preservada. O fluxo `Program` aguarda `CoreInitialisation` antes de `InitializeServices`; tráfego concorrente durante schema upgrade não é um gate representativo do startup normal.
- [ ] CI hospedada do checkout atual, carga de escritas/DELETE durante o runtime, runtime instalado e produção seguem pendentes. Formal 2/7 (28,6%), sem release.

# T5.6 — restart MariaDB durante persistência de imagens — 2026-10-26 15:29 BRT
- [x] Retry de `SaveImagesAsync` ampliado para dez tentativas em falhas de conexão; deadlock e lock timeout (1205/1213) continuam com três.
- [x] Teste integrado exige sinal durante persistência, reinício do mesmo MariaDB 11.4 isolado, readiness e confirmação da imagem final; gate CI valida container exclusivo na porta 33099.
- [x] Execução real local aprovada 1/1 com MariaDB 11.4.4 em datadir descartável e processo próprio na porta 33159; dotnet Release terminou em 38,7 s. O processo foi reiniciado durante o trigger `DELETE`; porta 3306 permaneceu no PID 31236 e não foi acessada.
- [x] Unitários focados 35/35; integração de restart 1/1; contrato CI 3/3; contrato do portal 288/288. A integração compilou e passou ligada à instância descartável.
- [ ] CI hospedada, runtime instalado, escrita/DELETE sob carga concorrente e upgrade do schema sob carga seguem pendentes. Produção não validada. Sem release; homologação formal 2/7 (28,6%).

# T5.6 — benchmark sintético do índice de imagens — 2026-10-26 15:00 BRT
- [x] MariaDB 11.4.4 descartável: consulta por `ItemId` em 100.000 linhas, quatro correspondentes; `EXPLAIN` usou índice composto (ref, estimativa 4) e sem índice fez scan (ALL, estimativa 99.881).
- [x] `mysqlslap`, 5 rodadas de 1.000 consultas sequenciais: índice média 0,068 s (0,062–0,079); sem índice 28,809 s (27,547–31,547), ~423x neste cenário sintético.
- [ ] Medir operações de escrita/DELETE, concorrência representativa, restart do MariaDB durante operação e upgrade do schema sob carga. Este número não representa produção; T5.6 aberto, homologação formal 2/7, sem release.

# T5.6 — comportamento na ausência do índice de imagens — 2026-10-26 14:48 BRT
- [x] Teste integrado remove FK e índice composto somente no schema MariaDB descartável; com 2.048 linhas não relacionadas, `EXPLAIN` confirmou `type=ALL` e `key` nula.
- [x] `SaveImagesAsync` persistiu as duas imagens após a remoção do índice; `ServerPersistenceMariaDbTests` passou 23/23.
- [x] MariaDB 11.4.4 de teste em 127.0.0.1:33159 encerrado e porta fechada; 3306 operacional não acessada.
- [ ] Estimativa do otimizador não atingiu o limite arbitrário de 2.048; removido esse limite e mantida a checagem determinística `type=ALL`/chave nula. Medição de latência com/sem índice, restart do banco durante operação e upgrade sob carga seguem pendentes. Formal 2/7, sem release.
# T5.6 — validação do plano seletivo de imagens — 2026-10-26 14:36 BRT
- [x] `SaveImagesAsync_UsesCompositeItemImageIndexForSelectiveLookup` passou 1/1 em MariaDB 11.4.4 descartável. Com 2.048 registros irrelevantes, `EXPLAIN` escolheu `IX_BaseItemImageInfos_ItemId_ImageType` e estimou no máximo quatro linhas.
- [x] `ServerPersistenceMariaDbTests` passou 22/22 com `max_connections=256`; instância descartável na porta 33159 encerrada e porta fechada. Instância operacional na 3306 não foi conectada.
- [x] `dotnet format ... --verify-no-changes` passou. Contrato do portal passou 284/284 após atualização do evento.
- [ ] Primeira execução completa teve 16 aprovações e 6 falhas por limite de conexões da instância de teste; a repetição configurada passou 22/22. Isso não representa falha do código, mas requisito de capacidade para esta suíte.
- [ ] Benchmark de carga, restart MariaDB durante operação, degradação sem índice, upgrade sob carga e CI hospedada seguem pendentes; T5.6 aberto. Formal 2/7 (28,6%), sem crédito/release.

# T5.6 — revalidação de concorrência da persistência de imagens — 2026-10-26 15:57 BRT
- [x] `ItemPersistenceServiceTests` passou 35/35 em Windows/.NET 10 Release.
- [x] `ServerPersistenceMariaDbTests` passou 24/25: 24 aprovados, 1 ignorado, 0 falhas, MariaDB 11.4.4 descartável na porta 33219. Concorrência SaveImages/SaveItems, múltiplos stripes e DeleteItem executada. Restart requer marcadores e já passou separado 1/1.
- [x] Processo/diretório de teste verificados e encerrados; porta 33219 fechada. Banco operacional 3306 não acessado. Anexo repetido; SHA-256 coincide com incidentes anteriores.
- [ ] Execução CI hospedada atual, runtime instalado e produção permanecem pendentes. Formal 2/7 (28,6%); nenhum crédito/release.

# Regressão ampla do servidor — 2026-10-26 16:10 BRT
- [x] Repetição Release sem build/restore: 5.379 aprovados, 96 ignorados, 0 falhas/5.475; 15 resumos de projetos.
- [x] Auditoria de writers: BaseItemImageInfos mutado em ItemPersistenceService; FixDates usa as 16 locks. Sem novo writer desbloqueado encontrado.
- [ ] Fresh run exit 1 sem diagnóstico preservado; projetos Integration, MariaDB/Mongo opt-in, CI hospedada, runtime instalado e produção continuam sem validação nesta rodada.
- Homologação formal 2/7 (28,6%); sem alteração de runtime ou release.

- [ ] Portal fonte atualizado e contrato 292/292; porta local 4173 sem listener, validar visualização renderizada quando o portal local estiver ativo.

# Regressão compilada do servidor — 2026-10-26 16:23 BRT
- [x] Repetição Release com build terminou exit 0; Server.Implementations.Tests 1.693/1.786 (93 ignorados); API 789/792 (3 ignorados).
- [ ] Diagnosticar primeiro exit 1 não foi possível porque a saída completa não foi preservada; teste opt-in MariaDB/Mongo, projetos Integration, Linux/macOS, CI hospedada, runtime e produção seguem pendentes.


# Restore P0 — regressão Windows atual — 2026-10-26 16:32 BRT
- [x] FullSystemBackup recompilado e executado em Windows/.NET 10 Release: 133 aprovados, 15 ignorados, 0 falhas/148.
- [ ] 15 skips incluem Linux/macOS; WSL não instalado, Docker/Podman indisponíveis. Linux/syscalls, ACL/mount efetivos e CI hospedada do checkout seguem pendentes.
- Sem alteração de runtime, sem crédito formal (2/7), sem release. Ver sessão do vault.
- [x] Portal `homologacao-status.json` parseia; `node --test portal-site/tests/homologacao.test.mjs` passou 294/294.

# Regressão completa Nebula — 2026-10-26 16:36 BRT
- [x] Suíte inteira Python passou em Windows/Python 3.10.11 isolado: 723 aprovados, 2 ignorados, 0 falhas/725. Os skips são ramos POSIX de durabilidade cobertos pela implementação Windows `MoveFileExW`.
- [x] Repetição com motivos de skip confirmou os dois casos; venv preexistente reutilizada sem instalar/atualizar dependências.
- [ ] Linux nativo, runtime/serviço instalado, CI hospedada atual e produção seguem sem evidência. A tentativa global foi bloqueada pelo guard. Homologação formal 2/7 (28,6%), sem release.
- [x] Portal atualizado e contrato validado após registrar essa execução.

# Revalidação do log de deadlock — 2026-10-26 16:38 BRT
- [x] O novo anexo é byte a byte o mesmo incidente já registrado (SHA-256 53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2); reexecutados `ItemPersistenceServiceTests` + `ItemPersistenceDatabaseLockTests`: 50/50 em Windows/.NET 10 Release.
- [ ] Log não identifica PID/assembly/versão; não prova que a fonte atual está instalada nem se a gravação final persistiu. MariaDB de produção não acessada. Homologação formal 2/7, sem release.

# T5.6 — regressão MariaDB isolada — 2026-10-26 16:43 BRT
- [x] Classe `ServerPersistenceMariaDbTests` em MariaDB 11.4.4 descartável: 24 aprovados, 1 skip, 0 falhas/25. Cobriu escrita concorrente, DELETE, advisory locks, recuperação de conexão e operações relacionadas.
- [x] Skip: teste de restart do processo exige marcadores próprios. Processo MariaDB 11.4.4 de teste encerrado, porta 33167 fechada; serviço operacional 3306/PID 31236 preservado sem acesso.
- [ ] Carga prolongada/representativa, CI hospedada atual, runtime instalado e produção seguem pendentes. Diretórios de teste permanecem em `%TEMP%`; a ferramenta bloqueou sua remoção recursiva. Formal 2/7, sem release.
- Observação de harness: execução inicial com `--skip-grant-tables` impediu CREATE USER; execução seguinte bateu no limite default de conexões. Repetição com grants ativos e `max_connections=256` passou.

# Auditoria P1 — propriedade do mutex de startup MariaDB — 2026-10-26 19:57 BRT
- [x] Regressão e correção concluídas em 2026-10-26: thread proprietária dedicada mantém/cria/libera o mutex; preserva abandono, cancelamento e timeout no fallback.
- [x] Testes direcionados 4/4; projeto `Jellyfin.Server.Tests` 86/86; contrato portal 301/301.
- [x] Smoke isolado cold start/stop/second start 1/1 registrado em T5.6 (21:52 BRT); não acessou MariaDB operacional.
- [x] Revalidação 26/10 22:43 BRT: MariaDbStartupMutexTests 4/4; teste end-to-end foi ignorado porque o runtime portable não estava staged.
- [ ] Runtime instalado, CI hospedada e produção seguem pendentes; o smoke e unitários locais não dão crédito formal.
- Formal 2/7; sem validação do runtime instalado/produção e sem release.

## CI remota continua sem chegar aos steps — 2026-10-26 20:39 BRT
- [x] Consulta somente leitura à API pública confirmou que o run mais recente observado continua sendo CI `39608614450`, no commit `d51f4f38` de 21/10: 13 jobs terminaram failure em 3–4 segundos, sem steps reportados.
- [x] Release run `39608677913` falhou no preflight de tag, sem steps; empacotamento Linux e publicação ficaram skipped. Nenhum dos cinco runs recentes é posterior a 21/10.
- [x] Branch local `main` está 8 commits atrás de `origin/main`; working tree contém 224 caminhos alterados/não rastreados. O CI remoto não cobre o checkout local atual.
- [ ] Causa da recusa/encerramento anterior aos steps e validação hospedada do checkout atual continuam pendentes. Não atribuir a falha ao código dos jobs; sem release.
- Auditoria agregada permanece 5.465/5.493; homologação formal 2/7 (28,6%), sem crédito. Nenhuma mudança em runtime, banco ou produção.

## Logs dos jobs remotos sem artefato disponível — 2026-10-26 20:42 BRT
- [x] GitHub API de logs consultada para o job de servidor `119251247983` e o preflight `119251465001`; ambos retornaram `404 BlobNotFound`.
- [x] As respostas confirmam indisponibilidade dos logs solicitados; não revelam causa e não provam que build/teste iniciou ou falhou.
- [ ] CI hospedada no checkout atual ainda não executou steps. Não recuperar diagnóstico por API nesta rodada; requer novo run que grave logs e steps.
- Agregados mantidos: 5.465/5.493 automáticos, formal 2/7 (28,6%). Sem crédito, sem release.

## Regressão Release compilada e serial — 2026-10-26 20:46 BRT
- [x] `dotnet test MulletaFlix-master/MulletaFlix.sln --configuration Release --no-restore --filter 'FullyQualifiedName!~Integration' --results-directory %TEMP%/mfx-server-build-regression-20261026-2045 --verbosity minimal --logger trx -m:1`: exit 0 com build.
- [x] 17 TRX: 15 assemblies com casos, 5.401 aprovados, 97 ignorados, 0 falhas/5.498. Os dois assemblies Integration foram filtrados sem casos selecionados.
- [x] Skip reasons: 38 Mongo opt-in, 32 MariaDB opt-in, 25 plataforma/filesystem, 2 benchmarks. Skips continuam fora dos aprovados.
- [ ] Linux, os opt-ins (sem serviços na suíte geral), CI hospedada sobre o checkout atual, runtime instalado e produção pendentes. O run local repete casos já registrados; agregado 5.465/5.493 e formal 2/7 permanecem. Sem release.
# T5.6 — revalidação do anexo e da CI remota — 2026-10-26 20:57 BRT
- [x] SHA-256 do anexo `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2` é idêntico ao log de 23/10; continua mostrando MariaDB 1213 em `DELETE BaseItemImageInfos`, sem desfecho das retentativas.
- [x] GitHub Actions run `39608614450`, commit `d51f4f38` de 21/10: 13 jobs failure, nenhum step reportado. Evidência não identifica a causa nem valida o checkout local.
- [x] Contrato do portal `node --test portal-site/tests/homologacao.test.mjs`: 304/304 aprovados, 0 ignorados/falhas.
- [ ] Checkout local segue 8 commits atrás e 224 caminhos modificados/não rastreados. Correlação do log novo, CI atualizada, runtime e produção pendentes. Sem mudança de código, banco ou serviço; sem release. Agregado 5.465/5.493 e formal 2/7 (28,6%) inalterados.

# T5.6 — comparação do log com o runtime instalado — 2026-10-26 20:59 BRT
- [x] Fonte atual de `SaveImagesAsync` registra PID e versão do assembly nas falhas; o anexo antigo não contém esses campos.
- [x] DLL instalada permanece 12.1.35, sem os marcadores textuais presentes na fonte; SHA-256 `0D9465B7EC1549CB70C063A2B7B199E8A6B645F6E454CDBFD008A8D0B3F9D30D`. Nenhum processo MulletaFlix/Jellyfin ativo; Nginx segue ativo.
- [ ] Essa diferença prova que o runtime instalado não contém o diagnóstico da fonte local, mas não associa o log histórico a essa DLL nem comprova o resultado das tentativas. Não instalar árvore local atrasada/suja. Checkout: 8 commits atrás e 224 caminhos modificados/não rastreados.
- Sem alteração de código, serviço, banco, instalação ou release. CI, runtime atualizado, log correlacionável e produção seguem pendentes; formal 2/7 (28,6%).
- [x] Após a atualização do portal, `node --test portal-site/tests/homologacao.test.mjs`: 304 aprovados, 0 ignorados/falhas.
- [x] Releitura da DLL instalada confirmou também ausência do literal `GET_LOCK` usado pelo `ItemPersistenceDatabaseLock` da fonte atual. O binário instalado não contém esse mecanismo de sincronização; isso ainda não prova que ele emitiu o log antigo sem PID/versão.

# T5.6 — regressão unitária focal no estado atual — 2026-10-26 21:03 BRT
- [x] `dotnet test` Release, `--no-restore`, filtro `FullyQualifiedName~ItemPersistence`: recompilou fonte e passou 50/50, 0 ignorados/falhas. Build emitiu avisos de analisadores/documentação, sem erros.
- [ ] Isso não executa testes integrados MariaDB nem valida concorrência de banco, runtime instalado ou produção. Agregado 5.465/5.493 e formal 2/7 (28,6%) sem mudança; teste é suplementar, sem crédito/release.

# T5.6 — concorrência integrada em MariaDB descartável — 2026-10-26 21:07 BRT
- [x] MariaDB 11.4.4 isolado, datadir temporário, loopback/porta 33259. `DeleteItem_ConcurrentWithImageRefreshes_CompletesWithoutDeadlock`, `SaveImagesAsync_WaitsForDatabaseAdvisoryLockHeldByAnotherSession` e `DeleteItem_WaitsForDatabaseAdvisoryLockHeldByAnotherSession`: 3/3 aprovados.
- [x] MariaDB operacional em 3306/PID 31236 não foi acessado; instância descartável encerrada, porta de teste fechada e variável de conexão removida.
- [ ] Remoção recursiva dos dados temporários foi bloqueada; restam ~150 MB de dados sintéticos sob `%TEMP%` em `mfx-t56-mariadb-20261026-2108`. Carga longa, CI atual, runtime e produção pendentes. Sem crédito agregado/formal, sem release.
# T5.6 — reteste determinístico após reenvio do deadlock — 2026-10-26 21:15 BRT
- [x] O log anexado tem SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, idêntico ao de 23/10; mostra deadlock 1213 em DELETE de `BaseItemImageInfos` e retries sem desfecho, não comprova commit nem versão emissora.
- [x] Recompilação e testes focais em MariaDB 11.4.4 isolado passaram 3/3, zero skips: refresh de imagem mantido bloqueado antes de iniciar `DeleteItem`, espera por advisory lock em SaveImagesAsync e DeleteItem. Processo temporário encerrou e porta 33259 fechou; banco operacional 3306 não acessado.
- [x] Contrato do portal e memória do projeto atualizados; fonte/mapa conferidos sem alteração estrutural.
- [ ] DLL instalada, correlação por PID/versão/hash, CI hospedada, produção e carga prolongada permanecem pendentes. Agregado 5.465/5.493; homologação formal 2/7 (28,6%), sem crédito ou release.
- Ver sessão `2026-10-26-MulletaFlix-t56-deterministic-image-delete-overlap-2115`.
# Auditoria Nebula — inicialização normal e regressão de fila — 2026-10-26 21:23 BRT
- [x] Lifecycle test passou a cobrir `STREAM_ONLY=True/False`; modo full valida cleanup, restauração de uploads, scanner Mongo e workers grandes/pequenos. `stats_reporter` agora tem expectativa explícita para `queue_identity` já gravada pelo código.
- [x] Regressão completa Python 3.10.11 isolada: 724 aprovados, 2 skips POSIX intencionais, 0 falhas. Cavecrew review encontrou e a execução final confirmou asserção de ausência do cleanup no modo streaming-only.
- [ ] Processo de serviço real, Mongo/Telegram descartáveis integrados até estado final do upload, Linux, runtime instalado, CI atual e produção continuam pendentes. A regressão não dá crédito formal nem ao agregado do servidor.
- [ ] Imagens antigas sem proveniência ainda não podem ser distinguidas com segurança de capas manuais; requer fluxo explícito para recuperação sem sobrescrever arte manual.
- Portal local atualizado; sem release/publicação remota. Ver sessão `2026-10-26-MulletaFlix-nebula-full-mode-startup-audit-2123`.
# T5.6 — atualizar imagens remotas sem idioma de origem — 2026-10-26 21:35 BRT
- [x] Refresh agora detecta imagens com provedor remoto conhecido e idioma ausente; seleciona idioma preferido e usa inglês como fallback. Capa sem provedor segue preservada.
- [x] Testes direcionados 4/4; conjunto `ItemImageProviderTests.RefreshImages` 47/47 em Windows/.NET 10 Release.
- [ ] Imagens antigas sem provedor e sem idioma não podem ser distinguidas de arte manual; permanecem sem substituição automática. Necessário fluxo explícito de reparo administrativo.
- [ ] Nenhuma integração TMDb/CDN, banco, runtime instalado, CI hospedada ou produção foi testada. Formal 2/7 (28,6%), sem crédito/release.
# CI do servidor — incluir contrato de serialização na suíte de contratos — 2026-10-26 21:38 BRT
- [x] O workflow CI chamava cinco contratos Node e omitia `ci-test-runner-serialization.test.mjs`, embora este verifique `-m:1` no comando de regressão do servidor. Incluído no comando de contratos existente.
- [x] Seis contratos CI: 14/14 aprovados em Node local.
- [ ] CI hospedada ainda não validou este checkout; o job de restore Linux privilegiado também não foi executado nesta sessão. Sem crédito formal/release; homologação 2/7 (28,6%).
# Restore P0 — proteger os testes privilegiados de Linux no contrato da CI — 2026-10-26 21:40 BRT
- [x] Adicionado contrato que exige os dez testes de fronteira no job Linux com `sudo`, verifica inclusão no filtro e falha se qualquer caso faltar ou não terminar Passed; a verificação TRX já configurada trata Skip como não aprovado.
- [x] O teste novo passou; seis contratos da CI 15/15. Contrato do portal atualizado para 310/310.
- [ ] WSL e Docker indisponíveis neste host; os dez cenários Linux reais e o runner hospedado ainda não foram executados. Restore P0 e formal 2/7 (28,6%) permanecem abertos, sem crédito/release.
# Regressão Release ampla local — 2026-10-26 22:06 BRT
- [x] Solução .NET 10 Release serial, sem rebuild, filtro `FullyQualifiedName!~Integration`: 15 assemblies, 5.403 aprovados, 98 ignorados, 0 falhas/5.501 casos selecionados; exit code 0.
- [x] Dois assemblies de integração não tiveram casos correspondentes ao filtro; skips mantidos como não executados. O teste lifecycle MariaDB foi ignorado sem runtime stageado nesta suíte e já possui evidência focal separada 1/1.
- [x] Anexo recebido nesta sessão comparado por SHA-256: idêntico ao deadlock já registrado; sem PID/assembly ou resultado final novo.
- [ ] Linux nativo, CI hospedada, runtime instalado/produção e formalização 2/7 continuam pendentes. Esta execução não altera o agregado 5.465/5.493 nem gera release.
# MariaDB descartável: regressão de usuário e billing — 2026-10-26 22:11 BRT
- [x] `UserManagerNormalizedUsernameTests|BillingDatabaseTests` passou 31/32 em Windows/.NET 10 Release; 1 caso ignorado porque exige `lower_case_table_names=0`, não disponível no MariaDB Windows.
- [x] MariaDB 11.4.4 isolada em porta dinâmica 60327, bind 127.0.0.1, datadir temporário; processo terminou e porta fechou. MariaDB operacional PID 31236/3306 não acessada.
- [ ] O skip exige Linux; CI hospedada, runtime instalado, produção e homologação formal 2/7 (28,6%) continuam pendentes. Não somar ao agregado auditável 5.465/5.493; sem release.
# Persistência de metadados com lookup O(1) — 2026-10-26 22:19 BRT
- [x] `UpdateOrInsertItemsCore` compara providers por `(ProviderId, ProviderValue)` e campos bloqueados por `Id` usando `HashSet`, removendo buscas `Any` aninhadas sem mudar a semântica de cardinalidade/pertencimento.
- [x] Testes direcionados 2/2; suíte `Jellyfin.Server.Implementations.Tests` Windows/.NET 10 Release: 1.707 aprovados, 94 ignorados, 0 falhas/1.801. Formatter `--verify-no-changes` aprovado. Cavecrew não encontrou regressão; duplicatas não têm caso dedicado.
- [ ] Benchmark antes/depois, CI hospedada, Linux e runtime/produção não validados. Auditoria agregada segue 5.465/5.493; homologação formal 2/7 (28,6%), sem crédito ou release.
# Restore: ampliar gate Linux e endurecer validação ACL — 2026-10-26 22:35 BRT
- [x] Gate privilegiado Linux exige 14 testes de fronteira e parser TRX reprova ausentes, Skip e outcomes diferentes de Passed.
- [x] Corrigida a máscara POSIX aplicada indevidamente a `other::`; agora `user::` e `other::` usam permissões diretas. Testes unitário e Linux real cobrem o caso `other::write` com máscara sem escrita.
- [x] TrustedInstaller segue permitido apenas como owner da âncora Windows; teste real de ACE de escrita continua rejeitando essa concessão. `FullSystemBackup` Release: 136 aprovados, 16 ignorados, 0 falhas/152; `BackupRestoreDirectoryTests`: 42 aprovados, 11 ignorados, 0 falhas/53.
- [ ] Linux real, CI hospedada do checkout atual, ACL de instalação/runtime e produção pendentes; gate restore P0 e homologação formal 2/7 (28,6%) continuam abertos, sem crédito ou release.

# Rechecagem CI hospedada e ambiente Linux — 2026-10-26 23:39 BRT
- [x] GitHub API: `main` permanece em `d51f4f38`; checkout está em `64a33dcf`, 8 commits atrás, sem commits à frente.
- [x] CI run `39608614450`: 13 jobs concluídos como failure sem steps/logs disponíveis; release run `39608677913`: preflight falhou e build Linux/publicação foram skipped. Logs para jobs `119251247983` e `119251465001` retornaram 404 `BlobNotFound`; causa desconhecida.
- [ ] WSL ausente e Docker não instalado neste host. Linux gate, nova CI do checkout e restore integrado ficam pendentes; não atribuir falhas ao código sem logs. Formal continua 2/7 (28,6%).

# Restore — revalidação Windows e revisão independente — 2026-10-26 23:42 BRT
- [x] Rebuild Release atual + `FullyQualifiedName~FullSystemBackup`: 136 Passed, 16 NotExecuted, 0 Failed/152.
- [x] Revisão Cavecrew da transação, diretórios por handle e ACL não encontrou vulnerabilidade adicional confirmada; confirma staging/rollback/sidecars e limites da validação estática.
- [ ] 16 casos de plataforma, incluindo Linux ACL/syscalls/symlink/mount, não executados aqui. CI hospedada sem logs; runtime/produção não conferidos. Restore P0/formal 2/7 permanecem abertos, sem release.

# Restore: impedir resultados TRX duplicados no gate — 2026-10-26 22:38 BRT
- [x] Verificador Linux deixou de converter os resultados em dicionário por nome curto. Agora avalia cada linha TRX, rejeita nomes esperados duplicados, itens inesperados e todos os outcomes diferentes de Passed; quantidade deve ser exatamente 14.
- [x] Contrato Node exige a sincronização exata dos 14 nomes de filtro e TRX e verifica as condições fail-closed do parser.
- [ ] Execução Linux privilegiada e CI hospedada continuam pendentes. Sem crédito formal ou release.

# Revalidação do mutex MariaDB — 2026-10-26 22:43 BRT
- [x] Fonte usa thread proprietária dedicada para adquirir/liberar mutex; cobre timeout, cancelamento e abandono.
- [x] MariaDbStartupMutexTests: 4/4 Passed em Windows/.NET 10 Release.
- [x] Smoke start-stop-start 1/1 executado anteriormente em T5.6; esta sessão não o reexecutou.
- [ ] Runtime instalado, CI hospedada e produção pendentes; sem crédito formal/release. Ver evento server-mariadb-startup-mutex-revalidation-2026-10-26-2243.

- [x] Atualização do portal e git diff --check; contratos portal/CI 319/319 após correção da asserção do evento.

# Smoke start-stop-start reexecutado — 2026-10-26 22:47 BRT
- [x] `MariaDbProcessManagerLifecycleTests.StartStopStart_UsesTheSameIsolatedDataDirectory`: 1/1 Passed com MariaDB 11.4.4 reportado pelo runtime instalado, porta dinâmica e datadir temporário; SQL sobreviveu ao segundo start.
- [x] Processo operacional existente permaneceu intocado; teste confirmou processo próprio obrigatório e usou datadir distinto.
- [ ] Não comparado hash do runtime com ZIP da CI; runtime empacotado, CI hospedada e produção pendentes. Formal 2/7; sem crédito/release.

- [x] Portal local e contratos CI 320/320 após corrigir asserções de texto; git diff --check e JSON válidos.
# Runtime instalado — telemetria O-1 — 2026-10-26 23:23 BRT
- [x] `AppData\Local\MulletaFlix\config\logging.default.json`: `Serilog.MinimumLevel.Default=Information`.
- [x] Binário instalado reporta 12.1.35; fonte local declara 12.1.32. Só `MulletaFlixNginx` estava ativo; processo/serviço principal e logs do servidor não encontrados nesta checagem.
- [ ] Comparar o binário instalado com artefato de build identificável, iniciar/verificar o serviço em runtime e confirmar `Slow HTTP Response` sob carga controlada.
- [ ] Homologação formal segue 2/7 (28,6%); sem crédito ou release.

# Supabase delta sem sort redundante — 2026-10-26 23:28 BRT
- [x] Consulta de presença do catálogo usa `select=id&limit=1`; timestamp usado como watermark continua vindo do checkpoint de backup concluído.
- [x] Removida criação do índice que só atendia `ORDER BY updated_at`; índice remoto pré-existente não é removido.
- [x] `NebulaSupabaseSyncTests` 65/65 e contrato SQL/RLS 1/1, Release.
- [ ] Supabase real não foi consultado; ainda não prova a causa específica do HTTP 500 nem valida o backup end-to-end. Formal 2/7 (28,6%), sem crédito ou release.

# Regressão ampla local — 2026-10-26 23:37 BRT
- [x] Solução .NET Release, 17 TRX: 5.410 Passed, 98 NotExecuted, 0 Failed/5.508.
- [x] Nebula Python 3.14: 724 Passed, 2 Skipped; frontend web: 418 Passed em 75 arquivos; `tsc --noEmit` passou.
- [ ] Os ignorados requerem bancos reais/opt-in, Linux/macOS ou recursos locais; não executar CI hospedada, serviço instalado, produção nem build de produção web nesta rodada. Não alterar agregados do portal nem conceder crédito formal: 2/7 (28,6%).

# B-6 — DTO de playback sem sync-over-async nos fluxos internos — 2026-10-27 01:34 BRT
- [x] `SessionManager` e `SessionController.ReportViewing` aguardam o caminho `GetBaseItemDtoAsync`; o campo Trickplay permanece para as miniaturas do player web.
- [x] Implementação default de `ISessionManager.ReportNowViewingItemAsync` preserva compatibilidade com implementadores existentes; `SessionManager` sobrescreve com fluxo assíncrono.
- [x] Release tests: `SessionManagerTests` 14/14 e `SessionControllerAuthorizationTests` 12/12; formatter direcionado e `git diff --check` aprovados.
- [ ] CI hospedada, runtime instalado e produção não validados; contrato síncrono legado segue disponível. Formal 2/7 (28,6%), sem crédito/release.
- [x] Log anexado tem SHA-256 idêntico ao incidente registrado anteriormente; não inclui PID/versão e não permite vincular o deadlock ao runtime atual.

# B-7 a B-12 — reconciliação de achados da auditoria — 2026-10-27 01:42 BRT
- [x] Fonte conferida: B-7 e B-11 usam `SaveChanges()` em métodos síncronos; busca nos cinco repositórios não encontrou `.GetAwaiter().GetResult()` nem `SaveChangesAsync(default)`.
- [x] B-8 locks de todos os itens em ordem determinística; B-9 atraso só após falha transitória; B-10 membership com `HashSet<Guid>`; B-12 hash memoizado por `Type`.
- [x] `UserDataManagerTests`: 3/3 Passed em Windows/.NET 10 Release. B-3 permanece parcial: cobertura funcional não mede custo da reidratação nem contenção MariaDB.
- [ ] Runtime/produção e carga permanecem pendentes para B-7 a B-12. Homologação formal 2/7 (28,6%), sem crédito/release.
- [x] Auditoria e evento local do portal atualizados; portal ainda não publicado remotamente; mapa técnico conferido, sem mudança estrutural. Ver sessão `2026-10-27-MulletaFlix-performance-audit-reconcile-b7-b12-0142`.

# Reenvio do log de deadlock de imagens — 2026-10-27 01:42 BRT
- [x] O anexo `14e0dd32-7aa9-4278-a883-b4acaa4ced72/Texto colado.txt` tem SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, idêntico aos anexos anteriores. Mostra MariaDB 1213 em `DELETE BaseItemImageInfos` para o mesmo ItemId em múltiplas threads e retries, mas não o desfecho nem PID/versão/assembly.
- [x] Não é evidência nova de regressão após a mitigação no checkout. A fonte protege gravações por item e advisory lock antes da transação; o trecho repetido não identifica qual runtime o produziu.
- [ ] Para concluir causa em execução: coletar log novo com versão/identidade do processo e resultado de cada retry, além do deadlock report do InnoDB; confirmar o binário realmente carregado. Não consultar nem alterar produção sem essa evidência.

# H-2 — estado mutável e chave incompleta no cache HLS — 2026-10-27 01:55 BRT
- [x] Removida a cópia dos timestamps de cada segmento para o `StreamState` cacheado; o callback da resposta usa posição final calculada do request próprio.
- [x] Cache hit aplica seek do request ao estado compartilhado somente sob lock da playlist, imediatamente antes de iniciar o ffmpeg.
- [x] Chave inclui as opções de encode omitidas (incluindo `MaxRefFrames`, `MaxVideoBitDepth`, sample rate, stream copy, segment options e `StreamOptions` determinísticas); valores nullable distinguem ausência de zero.
- [x] Campos textuais da chave usam framing comprimento+valor, evitando colisão quando `Container`/`SegmentContainer` ou opções textuais contêm `|`; regressão cobre dois pares distintos com chave antiga idêntica.
- [x] Release local Windows/.NET 10: `DynamicHlsControllerTests` 7/7 e `StreamStateCacheTests` 18/18; formatter passou. A revisão paralela ficou indisponível por dispositivo offline e não foi contabilizada como aprovação.
- [x] Teste do contrato do portal passou 351/351; JSON válido e `git diff --check` sem erros.
- [ ] Não executado teste HTTP end-to-end com requests HLS simultâneos, cliente de reprodução real, runtime instalado, produção ou carga. Homologação formal 2/7 (28,6%), sem crédito/release.
- [x] Portal e memória do projeto atualizados no checkout; mapa técnico conferido, sem dependência/módulo novo. Ver sessões `2026-10-27-MulletaFlix-hls-cache-state-safety-0155` e `2026-10-27-MulletaFlix-hls-cache-key-delimiter-collision-1340`.

# Validação adicional do deadlock BaseItemImageInfos — 2026-10-27 02:10 BRT
- [x] Log repetido: SHA-256 53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2; registra pelo menos 11 retries simultâneos no mesmo ItemId e heartbeat atrasado 2,35 s, mas sem PID/versão/caller/desfecho.
- [x] Integração Release contra MariaDB 11.4.4 descartável (127.0.0.1:33564): 24 Passed, 1 Skipped, 0 Failed/25; duração 1m14s. O cenário de reinício do processo DB foi ignorado fora de CI.
- [x] MariaDB temporária encerrada e processo instalado em 3306 permaneceu ativo. Nenhum dado operacional foi consultado ou alterado.
- [ ] O teste não reproduz a rajada observada, não valida runtime/produção nem CI remota. Necessário log novo com PID, versão do assembly, resultado terminal e relatório InnoDB para identificar a execução.
- [ ] Homologação formal permanece 2/7 (28,6%); sem crédito/release. Portal atualizado apenas localmente.
- [x] Mapa técnico conferido; sem alteração de estrutura. Sessão: 2026-10-27-MulletaFlix-image-deadlock-mariadb-validation-0210.

# Regressão Release ampla do servidor — 2026-10-27 02:18 BRT
- [x] Solução .NET Release, `-m:1`, filtro `FullyQualifiedName!~Integration`: 15 assemblies, 5.433 Passed, 98 Skipped, 0 Failed/5.531; exit code 0. Skips mantidos como não executados.
- [x] Nebula Python via `uv run --isolated` com lock de produção/teste: 724 Passed, 2 Skipped, 0 Failed em 35,91 s. Tentativa inicial no Python global foi bloqueada pelo guard antes da coleta; o runtime global não recebeu instalação.
- [x] CI local/portal recebeu evidência; resultados TRX por projeto foram sobrescritos por nome repetido, então contagem .NET foi agregada a partir dos 15 resumos console. Saída bruta fica em `%TEMP%` desta sessão.
- [ ] Filtro omite integrações .NET e não valida Linux/CI remota, runtime instalado, serviços reais ou produção. Integração de persistência MariaDB foi executada à parte na sessão anterior. Formal 2/7 (28,6%), sem crédito/release.
- [x] Mapa técnico conferido, sem mudança estrutural. Ver sessão `2026-10-27-MulletaFlix-server-regression-release-0218`.
# Revalidação do anexo MariaDB — 2026-10-27 02:22 BRT
- [x] SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`: log idêntico aos anexos já auditados. Confirma DELETE concorrente em `BaseItemImageInfos`, deadlock 1213 e retry 2; não contém PID/versão nem desfecho terminal.
- [x] Estado local conferido: sem processo MulletaFlix/Jellyfin; apenas o `mysqld` instalado em 3306 está ativo. DLL instalada segue 12.1.35; checkout/build local 12.1.32. Isto demonstra divergência, não identifica o emissor histórico do log.
- [x] A correção local serializa `SaveImagesAsync` por `ItemId` e advisory lock MariaDB; a integração MariaDB descartável foi aprovada 24/25 na sessão 02:10, com 1 teste CI-only ignorado. Regressão ampla .NET e Nebula Python passou na sessão 02:18.
- [ ] Runtime instalado/produção, CI remota e relatório InnoDB para uma ocorrência nova continuam pendentes. Homologação formal 2/7 (28,6%); sem crédito/release. O portal local recebeu evento de revalidação; sem publicação remota.

# CI hospedada bloqueada por cobrança — 2026-10-27 02:25 BRT
- [x] API pública do GitHub consultada: CI `39608614450` e Release `39608677913`, ambas no commit remoto `d51f4f38d2b4148ab49394c001567ad273bd57aa` (21/10/2026). O endpoint de annotations informa: “The job was not started because your account is locked due to a billing issue.” Os jobs falharam em 3–4 segundos, runner ID 0, steps vazios; logs de job não estão disponíveis.
- [x] `origin/main` aponta para `d51f4f38d2b4148ab49394c001567ad273bd57aa`; checkout HEAD `64a33dcf5595fb6f429c65206be20dbdd4ae0bd0`, oito commits atrás. Regressões locais aprovadas não certificam o commit remoto nem o estado atual da branch.
- [ ] Restaurar a capacidade de execução do GitHub Actions resolvendo a pendência de cobrança na conta e disparar CI/release preflight no commit de produção atualizado. Até lá, CI hospedada, artefato e release não homologados; formal do servidor continua 2/7 (28,6%). Portal local atualizado, sem publicação remota.

# Reauditoria do ciclo MariaDB do servidor — 2026-10-27 02:27 BRT
- [x] `Program` aguarda `StartMariaDbAsync` e `StopMariaDbAsync`; `WaitForDatabaseAsync` usa espera `Task.Delay(250, cancellationToken)`, sem polling bloqueante no caminho de prontidão.
- [x] Busca de chamadores: `StopMariaDb` síncrono chama `.GetAwaiter().GetResult()`, mas não tem uso interno encontrado. É wrapper legado público; não removido sem confirmar compatibilidade externa.
- [x] `MariaDbProcessManagerLifecycleTests.StartStopStart_UsesTheSameIsolatedDataDirectory`: Windows/.NET 10 Release, 1 Passed, 0 Skipped/Failed; executou start-stop-start e conferiu dado persistido em diretório efêmero, posteriormente removido pelo teste.
- [ ] Linux/CI hospedada e runtime instalado seguem pendentes; a CI remota está bloqueada pela cobrança da conta. Não atribuir o resultado local à branch remota. Homologação formal 2/7 (28,6%), sem crédito/release. Portal local atualizado.

# Subconsulta de álbuns recentes em MariaDB — 2026-10-27 02:40 BRT
- [x] `GetLatestItemList_Music_UsesMariaDbSubqueryAndDeduplicatesAlbums` passou 1/1 em MariaDB portátil 11.4.4, Windows/.NET 10 Release, porta dinâmica loopback 49270. Tradução SQL, ordenação e deduplicação verificadas; banco operacional 3306 não acessado.
- [ ] EXPLAIN/carga de catálogo representativo, CI hospedada, runtime instalado e produção pendentes. Homologação formal 2/7 (28,6%), sem crédito/release.
- [x] Portal de homologação atualizado localmente; publicação remota não feita. Datadir temporário sintético foi mantido após bloqueio da ferramenta à remoção recursiva.

# Consulta de álbuns recentes — 2026-10-27 02:29 BRT
- [x] `BaseItemRepository.GetLatestItemList(CollectionType.music)` mantém IDs distintos dos álbuns em subconsulta `IQueryable`, sem materializar lista de tamanho catálogo.
- [x] Novo teste de resultado/limite/filtro passou; `BaseItemRepositoryLatestTvShowTests`: 3 Passed, 0 Skipped/Failed em Release.
- [x] Reconciliado E-8: o stream de filmes/séries para no limite de cards distintos, mas não limita linhas SQL; duplicidade alta/ausência de limite pode ampliar leitura. Benchmark e EXPLAIN MariaDB pendentes.
- [x] Log de deadlock nesta conversa tem SHA idêntico ao incidente registrado; sem PID/versão/desfecho e não atribui falha ao checkout atual.
- [ ] Plano/carga MariaDB, CI hospedada, runtime instalado e produção não validados. Formal permanece 2/7 (28,6%), sem crédito/release.
- [x] Portal de homologação atualizado no checkout local; publicação remota não realizada.
# Reauditoria estática da persistência de itens — 2026-10-27 02:46 BRT
- [x] Chamadores internos conferidos: `SaveItems` → `UpdateOrInsertItems` → `UpdateOrInsertItemsCore`. Leituras de itens, providers e campos de metadata são agrupadas; a afirmação de consultas repetidas em loops não foi confirmada e foi retirada do resumo P0.
- [x] Escritas relacionadas podem gerar `ExecuteDelete` por item quando há mudanças em providers, imagens explicitamente informadas, campos bloqueados ou tipos de trailer. É custo condicional, ainda sem perfil representativo que prove gargalo.
- [x] O fluxo usa `SemaphoreSlim.Wait` e persistência EF síncrona; a busca não encontrou `GetAwaiter().GetResult()` nesse caminho. A contenção é risco a medir, não falha demonstrada.
- [ ] EXPLAIN/carga representativa e telemetria de scan pendentes. Nenhum código de produto alterado; homologação formal continua 2/7 (28,6%), sem crédito/release. Ver evento portal `server-item-persistence-static-reaudit-2026-10-27-0246` e sessão `2026-10-27-MulletaFlix-item-persistence-static-reaudit-0246`.
# Correlação do deadlock de imagens — binário instalado e fonte — 2026-10-27 02:51 BRT
- [x] O anexo atual tem SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2` (mesmo log já conhecido): 12 comandos DELETE falhos, 11 deadlocks 1213, 12 retries e um alerta de heartbeat; não há sucesso final nem PID/assembly no trecho.
- [x] Inspeção read-only distinguiu os dois arquivos instalados: `MulletaFlix.dll` 12.1.35, SHA-256 `6BCB074F679722F2F1D547E94DC40A81B8BAF90F4261D07235F1340B044C60BD`; `MulletaFlix.Server.Implementations.dll` 12.1.35, SHA-256 `0D9465B7EC1549CB70C063A2B7B199E8A6B645F6E454CDBFD008A8D0B3F9D30D`. A segunda não contém `ItemPersistenceDatabaseLock`, `mfx:item:` nem o novo template com PID/versão.
- [x] A fonte atual adquire stripe local e lock advisory MariaDB antes da transação; `SaveImagesAsync` mantém a stripe durante retries. `ItemPersistenceServiceTests`: 37 aprovados, 0 ignorados/falhas em Windows/.NET 10 Release.
- [ ] Nenhum processo servidor estava ativo; conexão de integração MariaDB não está configurada. Não é possível atribuir o log ao binário nem validar replay MariaDB, runtime ou produção. Homologação formal 2/7 (28,6%), sem crédito/release. Portal atualizado localmente, sem publicação remota. Ver evento `server-image-deadlock-installed-runtime-correlation-2026-10-27-0251` e sessão `2026-10-27-MulletaFlix-image-deadlock-runtime-correlation-0251`.
# Reauditoria do bootstrap de schema do UserManager — 2026-10-27 02:54 BRT
- [x] `IUserManager.InitializeAsync` tem callers de produção apenas nas rotas `StartupController`; os getters síncronos usam `.GetAwaiter().GetResult()` somente sob `!_schemaInitialized`. Após o guard, o bootstrap não se repete por request. `GetFirstUser` força bootstrap, mas só foi encontrado nas mesmas rotas.
- [x] Corrigida a descrição P1 no roadmap: risco reclassificado para P2 e limitado à primeira consulta síncrona de usuário em cada processo quando o schema ainda não foi inicializado.
- [x] `UserManagerTests|UserManagerNormalizedUsernameTests`: 16 Passed, 27 NotExecuted/Skipped, 0 Failed/43 em Windows/.NET 10 Release.
- [ ] Os 27 skips exigem MariaDB opt-in; não validam schema bootstrap relacional. Startup do host, MariaDB/runtime/produção seguem pendentes. Sem mudança de código; formal 2/7 (28,6%), sem crédito/release. Ver evento portal `server-user-manager-schema-cold-path-audit-2026-10-27-0254` e sessão `2026-10-27-MulletaFlix-user-manager-schema-cold-path-0254`.

# Restore P0 — ancora os diretórios de destino ao handle da raiz — 2026-10-27 04:18 BRT
- [x] `OpenTargetDirectoryCore` agora atravessa os componentes a partir da raiz configurada aberta; caminhos em cache são reabertos sob o pai autorizado e comparados pela identidade do diretório.
- [x] `BackupRestoreDirectoryTests` + `BackupServiceTests` Release: 128 Passed, 16 Skipped por plataforma/ambiente, 0 Failed/144.
- [ ] Linux nativo, ACL/mount reais, races de rename/unlink, CI hospedada, runtime instalado e produção continuam pendentes. Restore P0 e homologação formal 2/7 (28,6%) seguem abertos, sem crédito/release.
- [x] Portal de homologação atualizado localmente com resultado/limites; remoto não publicado. Ver `server-restore-root-anchored-path-traversal-2026-10-27-0418` e sessão `2026-10-27-MulletaFlix-restore-root-anchored-path-traversal-0418`.
# Restore: regressão Windows e contrato integral do gate Linux — 2026-10-27 04:27 BRT
- [x] `FullSystemBackup` Release Windows/.NET 10: 137 Passed, 16 Skipped por plataforma, 0 Failed/153.
- [x] Revisão ACL: TrustedInstaller é exceção somente para owner da âncora; grant de escrita continua negado e já tem teste Windows.
- [x] Contrato dedicado da CI compara os 14 casos entre o filtro Linux privilegiado, expectativa TRX e métodos xUnit; teste focal 2/2.
- [x] Contrato do portal reexecutado: 332/332, com verificações representativas dos três casos já cobertos. Linux nativo/CI hospedada e ACL operacional continuam sem evidência; restore P0 e homologação formal 2/7 (28,6%) permanecem abertos.

# Restore: journal antes de migrações pré-iniciais — 2026-10-27 05:15 BRT
- [x] Recovery do journal e restore solicitado movidos para antes das migrações pré-iniciais; a migração permanece única entre reinícios.
- [x] `GetTranscodePath` (cria diretório se necessário) também ocorre somente após recovery.
- [x] `ProgramStartupRecoveryTests` Release 4/4; teste com archive/journal real Release 1/1; payload verificado antes do callback de migração.
- [ ] Host real/migrações concretas, Linux/ACL, CI hospedada, runtime instalado e produção pendentes; homologação formal segue 2/7, sem crédito/release.
# Auditoria 2026-10-27 — ciclo de vida Nebula e integração restore

- [x] Corrigir shutdown Nebula que podia pular a espera quando `UPLOAD_QUEUE.empty()` ocultava upload já retirado pelo worker; regressão de ciclo de vida 3/3 e suíte isolada 725 aprovados, 2 ignorados.
- [x] Extrair a composição usada pelo startup para `Program.RunAfterBackupRecoveryAsync` e testar o mesmo helper com `BackupService` concreto e sinal explícito de conclusão antes da inicialização. `ProgramStartupRecoveryTests` passou 9/9.
- [x] Integrar journal ativo aos helpers de recovery/continuação usados pelo entry point; provar payload antes da continuação (`RecoverInterruptedRestoreAsync_ProgramStartupGateReplaysJournalBeforeInitialization`, Windows Release 1/1).
- [ ] Validar boot integral do host após restore (migrations/`InitializeServices`), Linux/ACL operacional, CI hospedada, runtime instalado e produção.
- [ ] Manter validação Linux/ACL, runtime instalado e produção pendente; homologação formal permanece 2/7.

## Restore Linux — aceite condicional do modelo de confiança — 2026-10-27 05:35 BRT

- [x] A política escolhida pelo usuário trata UID do serviço e root como principals confiáveis; a fonte ACL Linux nega escrita a group/other e named users não autorizados. Rename por essas identidades não é bypass de traversal por ator não confiável.
- [x] Risco residual explicitado: principal confiável pode renomear a árvore e causar divergência do destino configurado ou falha/integridade. Decisão `2026-10-27-restore-linux-trusted-rename-threat-model` registrada no vault.
- [ ] ACL Linux observável, teste de rename adversarial, CI no checkout atual, runtime instalado e produção continuam pendentes; não fecha homologação nem gate de release.

# Rechecagem read-only do runtime instalado — 2026-10-27 05:37 BRT
- Nenhum processo MulletaFlix/Jellyfin em execução; `MulletaFlixNginx` está ativo.
- DLL instalada `MulletaFlix.Server.Implementations.dll`: versão 12.1.35, 1.774.080 bytes, last-write 21/10/2026 10:15:18 -03. Nenhum banco/serviço foi tocado.
- Isso não correlaciona o log histórico nem valida checkout 12.1.32; nenhuma mudança de runtime ou release.

## Regressão focal do deadlock de imagens — 2026-10-27 05:41 BRT
- [x] `ItemPersistenceServiceTests` + `ItemPersistenceDatabaseLockTests`: 52/52 em Windows/.NET 10 Release, exit code 0.
- [x] Log anexado confirmado como duplicata por SHA-256; MariaDB 1213 durante `DELETE BaseItemImageInfos`, sem PID/versão nem desfecho do retry.
- [ ] Não executar MariaDB contra instância ativa não identificada. Integração descartável, Linux/CI hospedada, runtime instalado e produção seguem pendentes. Homologação formal 2/7 (28,6%), auditoria 5.465/5.493 sem crédito ou release.

## Compilação de integração e regressão ampla — 2026-10-27 05:55 BRT
- [x] Corrigido CS1061 no teste `FixDatesImageMigration_WaitsForAllImageAdvisoryLocks`: adicionado `InternalsVisibleTo("IntroSkipper.Integration.Tests")` ao assembly MulletaFlix. `FixBaseItemImageInfos` permanece internal.
- [x] `dotnet test MulletaFlix.sln --configuration Release --no-restore`: exit code 0. `Jellyfin.Server.Implementations.Tests` 1.729 aprovados / 95 ignorados / 0 falhas.
- [x] `Jellyfin.Api.Tests` 809/3/0; `Jellyfin.Providers.Tests` 884/13/0; execução focada `IntroSkipper.Integration.Tests` 20 aprovados / 48 ignorados / 0 falhas.
- [ ] 48 integrações MariaDB omitidas sem conexão descartável; Linux, Mongo opt-in, CI hospedada, runtime instalado e produção continuam pendentes. Homologação formal 2/7 (28,6%); auditoria 5.465/5.493 sem crédito ou release.

## Restore seguido de boot integral do servidor — 2026-10-27 06:32 BRT
- [x] Gate de integração opt-in prepara journal/arquivos de restore interrompido e verifica conteúdo restaurado e journal removido antes da migração pré-inicial.
- [x] `MariaDbTestEnvironment` agora exige opt-in explícito de instância descartável antes de qualquer teste MariaDB destrutivo; a CI só confirma no job que inicia serviço MariaDB próprio. Testes de validação do endpoint passaram 10/10.
- [x] Com MariaDB 11.4.4 descartável: build de `Jellyfin.Server.Integration.Tests` sem avisos/erros; `StartupBackupRecoveryTests.CreateHost_ReplaysInterruptedRestoreBeforeMigrationsAndCompletesStartup` passou 1/1, incluindo assert explícita do callback pré-migração. O host executou migrations, `InitializeServices`, startup tasks e respondeu com sucesso a `/System/Info/Public`.
- [x] Contrato estático de cobertura MariaDB na CI passou 4/4; portal local atualizado com `server-full-startup-after-restore-mariadb-2026-10-27-0626`.
- [x] Review final: schema de plugin agora é exclusivo por host de teste e removido em teardown; build final 0 warnings/0 errors, startup 1/1, IntroSkipper MariaDB 12/12 e schema fixo ausente ao final.
- [x] CI contract 4/4; portal 334/334; diff check sem whitespace errors. Instância de teste 49374 encerrada e produção 3306/PID 31236 preservada.
- [ ] Linux/ACL, CI hospedada, runtime instalado e produção pendentes. Datadirs descartáveis ficaram em `%TEMP%`; formal 2/7 (28,6%), auditoria 5.465/5.493, sem crédito/release.
# Divergência da correção de persistência de imagens com origin/main — 2026-10-27 07:32 BRT
- [x] `git fetch origin main` atualizou a referência sem tocar a árvore de trabalho: `origin/main` = `d51f4f38` / v12.1.35, 8 commits à frente do HEAD `64a33dcf`.
- [x] O release remoto ainda apaga `BaseItemImageInfos` sem `ItemPersistenceDatabaseLock` e registra retry sem PID/AssemblyVersion. A correção local está em alterações não commitadas no helper, serviço e testes; não foi mesclada/publicada.
- [x] Anexo repetido tem SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`; sem identidade do processo ou desfecho final.
- [x] Contrato local `node --test portal-site/tests/homologacao.test.mjs`: 337/337 aprovados, sem falhas.
- [x] Worktree de correção na base `d51f4f38`/v12.1.35: filtro `ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests` passou 43/43, 0 ignorados/falhas. Build teve avisos de estilo/documentação em outros arquivos.
- [ ] Reconciliar a correção sobre v12.1.35 numa árvore isolada, revisar o diff sobreposto e executar unitários + integração MariaDB descartável; depois requer CI desbloqueada, Linux/ACL, runtime e produção. Formal 2/7, auditoria 5.465/5.493, sem release.

# Persistência concorrente de imagens — validação MariaDB descartável — 2026-10-27 08:19 BRT
- [x] Log SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2` confirma MariaDB 1213 em `DELETE BaseItemImageInfos`; HTTP 200 não confirma commit e o recorte não contém PID/versão nem resultado final.
- [x] `SaveImagesAsync_ConcurrentRefreshesForSameItem_PersistOneCompleteImageSet` passou 1/1 em Windows/.NET 10 Release contra MariaDB 11.4.4 descartável em loopback/porta 51588. A instância isolada foi encerrada; MariaDB operacional 3306/PID 31236 não foi acessado.
- [x] Fonte do checkout serializa gravações por stripe local e `GET_LOCK` MariaDB por schema/stripe antes da transação. O formato do retry no anexo é anterior à mensagem de diagnóstico atual e não identifica o assembly emissor.
- [ ] DLL/runtime do log, CI hospedada, instalação e produção não foram correlacionados; nenhuma release nesta sessão. Formal 2/7 (28,6%) e auditoria 5.465/5.493 permanecem sem crédito.
# Revalidação unitária do deadlock de imagens — 2026-10-27 08:27 BRT
- [x] O anexo recebido tem SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, igual aos registros anteriores; não acrescenta PID/AssemblyVersion nem resultado final das retentativas.
- [x] `dotnet test MulletaFlix-master/tests/Jellyfin.Server.Implementations.Tests/Jellyfin.Server.Implementations.Tests.csproj --no-restore --filter 'FullyQualifiedName~ItemPersistenceServiceTests|FullyQualifiedName~ItemPersistenceDatabaseLockTests'`: 52/52 passaram em Windows/.NET 10 Debug. O build emitiu avisos StyleCop/xUnit em outros arquivos do projeto.
- [x] `node --test portal-site/tests/homologacao.test.mjs`: 337/337; `git diff --check` nos arquivos atualizados: exit 0.
- [x] Revisão NextUp corrigiu a hipótese anterior: o `limit + 10` em `TVSeriesManager` não alcança o fluxo geral; a consulta por `SeriesId` retorna antes. Mantém-se o risco real documentado de materialização de todos os episódios candidatos e a exigência de preservar contagem/paginação.
- [ ] Teste unitário não identifica o runtime do log nem valida produção. O teste MariaDB concorrente 1/1 já consta no evento portal de 08:19; instalação/runtime, CI hospedada e produção pendentes. Não acessar o MariaDB operacional. Formal 2/7 (28,6%), auditoria 5.465/5.493 sem crédito; sem release.
# Nebula — serialização do estado e reanálise de deadlock repetido — 2026-10-27 09:28 BRT
- [x] eed_ftp.mark_seen agora serializa alteração do conjunto e persistência; tomic_write_json protege o arquivo temporário por processo contra escritas concorrentes. Regressão determinística adicionada.
- [x] Suíte NebulaPython: 726 aprovados, 2 ignorados, 0 falhas; teste concorrente repetido 10/10; py_compile e git diff --check passaram. Ruff indisponível; execução pelo Python global foi corretamente bloqueada pelo guard do runtime isolado.
- [x] Anexo MariaDB SHA-256 53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2 é idêntico ao incidente já registrado. Fonte local contém stripe por item, advisory lock MariaDB e retries limitados; o log não identifica PID/assembly nem resultado terminal, logo não permite atribuir a emissão ou confirmar persistência no runtime em uso.
- [ ] Não houve novo teste MariaDB para este anexo duplicado, CI remota, Linux, runtime instalado ou produção. Auditoria agregada não muda; homologação formal 2/7, sem crédito. Nenhuma release/publicação remota.

# NextUp — desempate estável antes do limite — 2026-10-27 10:24 BRT
- [x] `GetNextUpSeriesKeys` ordena agora por `SeriesPresentationUniqueKey` quando `LastPlayedDate` empata, antes de aplicar `Limit`.
- [x] O teste `GetNextUpSeriesKeys_AppliesLimit` usa datas iguais; `NextUpQueryOptimizationTests` passou 8/8 em Windows/.NET 10 Release.
- [x] Revisão adversarial não encontrou erro no diff; identificou limite: suíte usa EF InMemory e não testa tradução Pomelo.
- [ ] Adicionar/verificar integração MariaDB para confirmar tradução/ordenação SQL e medir a seleção relacional pendente dos candidatos de episódio. CI hospedada, runtime e produção sem validação; homologação formal permanece 2/7 (28,6%), sem release.

# NextUp — integrar teste de desempate MariaDB no CI — 2026-10-27 10:30 BRT
- [x] Adicionado teste real de `NextUpService.GetNextUpSeriesKeys` com duas séries, `LastPlayedDate` igual e `Limit=1`; deve confirmar `SeriesA` em MariaDB.
- [x] O job `user-mariadb-linux-tests` e o contrato estático do workflow incluem o novo caso.
- [ ] Execução local compilou e reconheceu o teste, mas foi ignorada sem MariaDB descartável; Docker indisponível. A execução hospedada ainda não foi observada, então tradução Pomelo permanece sem resultado. Sem crédito formal (2/7) ou release.

# NextUp — auditoria da seleção SQL — 2026-10-27 10:58 BRT
- [x] Reexecutado `NextUpQueryOptimizationTests` no Windows/.NET 10 Release: 8 aprovados, 0 ignorados/falhas.
- [x] Cavecrew revisou o query shape sem escrita nem acesso a banco: episódios não assistidos, assistidos e linhas de rewatch continuam enumerados em lote; o índice por tipo/série/temporada/episódio já existe; `GetResult` precisa continuar vendo o conjunto completo antes da paginação/contagem.
- [x] `GetNextUpEpisodesBatch_SelectsEarliestUnplayedEpisodeOnMariaDb` valida a seleção de até um candidato não assistido por série, inclusive especiais, virtuais e posições nulas, e a tradução MariaDB `ROW_NUMBER() ... <= 1`; passou 1/1 na instância portátil descartável.
- [ ] EXPLAIN/ANALYZE, volume/latência/memória em catálogo representativo, rewatch relacional, CI hospedada, runtime e produção seguem pendentes. Cavecrew identificou e o teste agora cobre a semântica do último episódio assistido com posição parcial. Formal 2/7 (28,6%), sem crédito/release.

# MariaDB descartável — prontidão de ciclo local — 2026-10-27 11:03 BRT
- [x] `MariaDbProcessManagerLifecycleTests.StartStopStart_UsesTheSameIsolatedDataDirectory` passou 1/1 em Windows/.NET 10 Release: start, stop, segundo start, leitura do marcador persistido, shutdown e limpeza do diretório temporário.
- [x] Após a execução, a lista de processos contém somente o `mysqld` operacional preexistente; o teste não deixou instância descartável ativa.
- [ ] A integração NextUp continua sem executar Pomelo/MariaDB: o teste de ciclo não valida consulta de candidatos. Próximo passo é provisionar a conexão descartável via fixture sem tocar o processo operacional e executar o teste relacional/EXPLAIN. Formal 2/7, sem crédito/release.

# NextUp — seleção SQL em MariaDB — 2026-10-27 11:24 BRT
- [x] Implementada seleção relacional do primeiro episódio não assistido por série; MariaDB confirma SQL `ROW_NUMBER()` e filtro de ranking `<= 1`.
- [x] Regressão encontrada na revisão Cavecrew foi corrigida: posições de candidatos continuam respeitando o último episódio assistido na ordenação existente; se ele tiver temporada ou episódio nulo, não se aplica corte por posição. Integração descartável passou 1/1.
- [x] Suíte `Jellyfin.Server.Implementations.Tests`: 1.739 aprovados, 95 ignorados de plataforma/ambiente, 0 falhas/1.834 em Windows/.NET 10 Release.
- [ ] Medir plano, latência e memória em catálogo representativo; o `ANALYZE` do fixture sintético da seleção não assistida está registrado na seção 11:45. Rewatch agora tem seleção relacional; plano medido e benchmark representativo continuam pendentes. CI hospedada, runtime instalado e produção pendentes. Formal 2/7; sem release.

# NextUp — ANALYZE do seletor MariaDB — 2026-10-27 11:45–11:48 BRT
- [x] Teste descartável executou `ANALYZE FORMAT=JSON` sobre o SQL e parâmetros capturados do serviço; no fixture de cinco séries/16 episódios, o plano observou contagens incluindo 4 e 16 linhas.
- [x] Rerun passou 1/1 e registrou 0,429 ms no query block. Após revisão, teste não depende de aliases internos do plano e ignora métricas `r_rows` nulas.
- [ ] Fixture pequeno e aquecido: não extrapolar para biblioteca real. Gerar volume representativo, comparar antes/depois com planos e métricas equivalentes, e avaliar o ramo de rewatch. CI hospedada/runtime/produção pendentes; formal 2/7, sem release.

# HTTP condicional — ETag em listas, weak validators e wildcard — 2026-10-27 10:37 BRT
- [x] Compartilhado helper `ConditionalRequestExtensions.IfNoneMatchMatches` entre imagem e legenda; lista, comparação fraca e `*` cobertos por testes.
- [x] A validação de imagem usa `If-Modified-Since` apenas se `If-None-Match` estiver ausente, conforme precedência.
- [x] Regressão Release: `ConditionalRequestExtensionsTests|ImageControllerSplashscreenTests|ImageControllerUserImageAuthorizationTests|SubtitleControllerTests` passou 44/44, incluindo precedência validada por ação pública do controller.
- [ ] Runtime, CI hospedada e produção pendentes; homologação formal continua 2/7, sem crédito/release.

# NextUp — desempate determinístico do rewatch — 2026-10-27 12:01 BRT
- [x] No empate de `LastPlayedDate`, a seleção escolhe maior temporada/episódio; empate completo usa maior `Guid` estável.
- [x] MariaDB descartável validou a seleção por posição, Guid e data nula contra data válida: 1/1. `NextUpQueryOptimizationTests`: 8/8.
- [ ] O rewatch já seleciona no SQL e passou teste de semântica no MariaDB descartável; ainda falta medir seu plano e comparar desempenho com carga representativa. CI hospedada, runtime instalado e produção pendentes. Formal 2/7 (28,6%); sem release.

# NextUp — SQL para seleção de rewatch — 2026-10-27 12:30 BRT
- [x] Rewatch ranqueado no SQL por data, posição e ID; projeção escalar do ID vencedor evita `EmptyProjectionMember`, com carga de entidades pela chave.
- [x] MariaDB descartável 1/1, fixture 6 séries/19 episódios, incluindo episódio sem posição antes do próximo numerado; NextUp unitário 8/8; formatter e diff-check aprovados.
- [ ] Benchmark representativo, plano/análise do rewatch, CI hospedada, Linux, runtime instalado e produção pendentes. Formal 2/7, sem crédito/release.

# NextUp — ANALYZE do rewatch — 2026-10-27 12:52 BRT
- [x] Teste exige que a consulta capturada após reset contenha `LastPlayedDate` e `ROW_NUMBER() OVER`, então o ANALYZE não reutiliza uma consulta ranqueada anterior.
- [x] MariaDB 11.4.4 descartável passou 1/1; fixture 6 séries/19 episódios, query block 0,568 ms. `dotnet format whitespace` passou.
- [ ] Medir antes/depois com volume representativo, cache controlado e memória; esse fixture pequeno não demonstra ganho. Linux/CI hospedada/runtime/produção pendentes. Formal 2/7, sem release.
# Revalidação do log duplicado de deadlock em imagens — 2026-10-27 13:32 BRT
- [x] O anexo tem SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, idêntico ao incidente anterior. Repete MariaDB 1213 em `DELETE BaseItemImageInfos`; não contém PID, AssemblyVersion ou desfecho das retentativas.
- [x] Filtro Release `ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests`: 54/54 aprovados no checkout atual. O checkout usa stripe local por item, advisory lock MariaDB e retries limitados.
- [ ] O teste confirma a fonte local, não o binário que emitiu o log nem produção. A integração MariaDB descartável anterior 1/1 não foi repetida nesta sessão; runtime instalado, CI hospedada e produção seguem sem correlação. Formal 2/7 (28,6%), sem crédito/release.
# Scheduler da biblioteca — cancelamento real no cleanup — 2026-10-27
- [x] Sincronizar acesso ao registry de workers e tolerar a fonte já descartada enquanto cleanup tenta cancelar.
- [x] Corrigir o loop de leitura do canal para usar o token vinculado cancelado pelo cleanup; o teste real após 65 s reproduziu e confirmou a correção.
- [x] Impedir enqueues durante o cancelamento pendente; `DisposeAsync` drena enqueues admitidos e aguarda workers, inclusive quando chamado simultaneamente.
- [x] Tornar determinística a asserção de backpressure e cobrir cleanup real, enqueue durante cleanup, e descarte concorrente.
- [x] Windows/.NET 10 Release: Controller 69/69; solução serial 17 TRX, 5.477 aprovados, 98 `NotExecuted` e 0 falhas; portal 359/359; formatter direcionado, revisão estática e diff-check passaram.
- [ ] CI hospedada, Linux, runtime instalado e homologação de produção. Homologação formal permanece 2/7; sem release.

Revalidação em 27/10/2026 16:30 BRT: `LimitedConcurrencyLibrarySchedulerTests` passou 4/4 em Windows/.NET 10 Release no checkout atual. O contador global continua sem mudança; esta execução focal não cobre CI, Linux, runtime ou produção.


## Restore Linux gate — ambiente e branch rechecados — 2026-10-27 17:12 BRT

Rechecagem do host: wsl.exe --list --quiet não listou distribuição; Docker e Podman não estão instalados/disponíveis. git fetch origin concluiu sem erro e manteve origin/main em d51f4f38; checkout main fa1289b6 segue 1 commit à frente e 8 atrás. Há alterações locais não commitadas em Program.cs, scheduler e testes que também diferem do upstream; não houve merge/rebase para preservar esse trabalho. Consulta GitHub do commit remoto devolveu lista vazia de status checks; isso não comprova execução CI Linux nem causa do run falho previamente observado. Restore P0, Linux/ACL, CI hospedada, runtime/produção continuam sem validação; formal 2/7, sem crédito/release.


## Restore agendado — segunda passagem do loop de startup — 2026-10-27 17:16 BRT

O teste ScheduledRestore_RestoresArchiveBeforeStartupInitialization agora verifica que ContinueAfterBackupRecoveryAsync limpa _restoreFromBackup e que a próxima chamada do gate, com nova instância de BackupService no mesmo processo, continua para migração e inicialização em vez de reaplicar o archive. O teste focal passou 1/1 em Windows/.NET 10 Release; FullSystemBackup passou 142 aprovados, 16 ignorados por plataforma e 0 falhas/158. O primeiro filtro FQN incorreto selecionou zero testes; a repetição com filtro pelo nome do teste executou e passou. Não houve mudança na lógica de produção. Linux/ACL, CI hospedada, runtime e host operacional continuam pendentes; formal 2/7, sem crédito/release.
# Nebula: bootstrap gravável e reconciliação Supabase — 2026-10-27 17:25 BRT
- [x] O log de produção confirma que o bootstrap tentava criar `.venv` em `Program Files`, recebia WinError 5 e mesmo assim prosseguia para iniciar workers. O script instala agora a venv e grava `python.path` no diretório de dados do servidor; o processo passa esse caminho ao PowerShell e o resolvedor mantém compatibilidade com marcador antigo. Falha de dependência agora é propagada, impedindo worker iniciado em estado incompleto.
- [x] Cursor GUID do PostgREST agora usa UUID sem aspas; reconciliação `nebula_users` ignora chaves `login` nulas/vazias, evitando abortar o backup por registros legados sem identidade.
- [x] Testes direcionados: Supabase 3/3; ciclo de restart no mesmo processo 1/1; integridade de staging/pacote Nebula aprovada; `git diff --check` sem erros.
- [ ] Não foi executada suíte .NET completa, CI hospedada, Linux, instalador limpo, nem reteste em produção. O log também mostra N: indisponível e heartbeats Kestrel longos; o primeiro continua pendente de validação operacional e o segundo não tem causa atribuída. Não apagar linhas antigas com login vazio sem política/backup específico.
- Homologação formal permanece 2/7 (28,6%); auditoria agregada não foi recalculada. Nenhum dado remoto foi editado; nenhuma release foi criada/publicada. Portal atualizado no checkout local, não implantado.
- Sessão: `2026-10-27-MulletaFlix-nebula-runtime-supabase-log-fix-1725.md`; mapa técnico conferido e atualizado.
## Revalidação Nebula — bootstrap e paginação Supabase — 2026-10-27 17:31 BRT
- [x] O log de 17:19 ainda registra a tentativa de venv em `Program Files` (WinError 5) e falha de reconciliação `nebula_users` por chave inválida/repetida; é evidência do runtime que gerou o log, não de que o patch local esteja instalado.
- [x] `FindPythonExe` prioriza agora o marcador gravável em `CommonApplicationPaths.DataPath` e mantém o marcador legado como fallback. O teste cobre preferência e fallback.
- [x] O fallback do script de instalação sem `MULLETAFLIX_DATA_PATH` agora aponta para `<ProgramData>\\MulletaFlix\\Server\\data`, que corresponde ao `DataPath` criado em `BaseApplicationPaths`.
- [x] Teste focado .NET 10: 1/1 aprovado; `Assert-StageIntegrity.Tests.ps1` aprovado.
- [ ] Release não executada: checkout `main` diverge (`1` à frente/`8` atrás) e contém outras alterações locais alheias, sem CI/instalador/publicação validada. Não instalar em produção; requer sincronização segura da base, build completo e revalidação.
- [ ] Supabase continua pendente de verificação pós-release; filtragem local de login vazio não evita chaves não únicas caso o esquema remoto não corresponda ao esperado. O log mostra upload concluído e reconciliação interrompida, sem evidência de remoção remota.
- [ ] N: não determinado; revisão estática não confirmou bug de fonte para o sintoma e o runtime indicado pelo log falha antes do mount. Kestrel heartbeat sem causa comprovada.
- [x] Mapa técnico conferido; mudou apenas a precedência/fallback do resolvedor Python, sem novo módulo, API ou dependência.

## Supabase — correlação do log de backup Nebula — 2026-10-27 17:36 BRT
- [x] Logs somente leitura do projeto MulletFlix-Nebula correlacionados ao incidente: user-agent `MulletaFlix-Server/12.0`, GET de `nebula_users` sem os filtros atuais, paginação repetindo o cursor e uma chamada a `mulletaflix_users` com cursor UUID citado recebendo PostgREST 400/22P02.
- [x] Schema observado: `nebula_users.login` é chave primária e a tabela contém 7 linhas; duplicatas de login não são permitidas pelo schema atual. A query SQL agregada falhou com erro interno da integração, portanto não houve contagem independente dos valores.
- [x] Teste adicionado para garantir que filtros de login nulo/vazio acompanhem a paginação por cursor; teste focal 3/3 passou.
- [ ] Instalar e testar binário com patch. O processo MulletaFlix não estava ativo e a porta 8096 não escutava nesta checagem.
- [ ] Avaliar advisories do Supabase sem aplicar mudanças: `set_nebula_updated_at` com search_path mutável e índice duplicado em `mulletaflix_users`.
- Formal 2/7 (28,6%), sem crédito; sem release.
Supabase query retry 17:37 BRT: agregado read-only confirmou 7 logins totais/distintos, 0 nulos e 0 vazios/espaços; não há duplicatas nebula_users no snapshot atual. A consulta anterior de agregação falhou transientemente; sucesso da repetição registrada.

## Validação sobre base remota limpa — 2026-10-27 17:41 BRT
- [x] Patch de Nebula/Supabase aplicou limpo sobre `origin/main` d51f4f38 em checkout isolado curto `C:\mf`.
- [x] Windows/.NET 10: `NebulaDownloaderStartupTests|NebulaSupabaseSyncTests` 84/84 aprovados; staging integrity passou.
- [ ] Pipeline completo de release, Linux e runtime de produção pós-patch continuam pendentes; nenhuma tag ou publicação.
- [ ] Checkout principal continua 1 commit à frente/8 atrás, com alterações locais antigas. Não integrar nem publicar o conjunto inteiro sem revisão de escopo.

## Reauditoria do scheduler/Nebula — 2026-10-27 22:07 BRT

- [x] O risco de cancelamento cooperativo foi tratado em sessão posterior; consulte a seção abaixo para novos testes e contrato atualizado. O callback não cooperativo continua sendo um limite de descarte gracioso.

## Scheduler — token de cancelamento para callbacks — 2026-10-27 22:21 BRT

- [x] `Enqueue` entrega token de cancelamento ao callback e `Folder` repassa o token a validação e atualização recursiva; escrita no canal também cancela com caller/shutdown.
- [x] Callbacks de cancelamento que lançam exceção são registrados sem interromper a limpeza. Teste focal do scheduler 6/6 e suíte `Jellyfin.Controller.Tests` 71/71, Windows/.NET 10 Release; `git diff --check` aprovado.
- [ ] Callbacks que não observam cancelamento ainda podem prolongar `DisposeAsync`; Linux, CI hospedada, runtime instalado e produção não validados. Formatter direcionado não concluiu; reportou ordenação de imports preexistente em `Folder.cs`.
- [x] A branch limpa `codex/scheduler-callback-cancellation` foi validada e publicada. Commits `0813ca64` (código/testes) e `69d81ca4` (docs). Suíte Controller 75/75, scheduler 12/12 e caller-cancel 1/1.
- [ ] PR não criado: integração GitHub retornou 403 e GitHub CLI não possui sessão autenticada. Sem merge/release; runtime Linux/produção pendentes. Homologação formal 2/7 (28,6%).

## Teste MariaDB opt-in sem interromper instância operacional — 2026-11-02 21:19 BRT

- [x] Corrigido o bloqueio: inicializações internas com `requireEmbeddedProcess=true` usam mutex de teste separado; entrada pública de produção mantém mutex global.
- [x] O ciclo start/stop/start rejeita explicitamente 3306. Os três consumidores `GetFreePort()` da classe MariaDB agora impedem essa colisão.
- [x] Windows/.NET 10 Release: teste NextUp MariaDB 1/1; start-stop-start 1/1; `MariaDbStartupMutexTests` 4/4. Servidor operacional e porta 3306 permaneceram ativos.
- [ ] Dataset NextUp representativo, Linux e CI hospedada permanecem pendentes. Formal 2/7, sem crédito ou release.

## Restore — retomada após encerramento forçado — 2026-11-02 22:40 BRT
- [x] Suíte Windows `BackupRestoreDirectoryTests|BackupServiceTests`: 133 aprovados, 16 ignorados, 0 falhas (149 casos).
- [x] Contrato que confere os 14 cenários Linux no workflow: 2/2 aprovados.
- [x] Após retomar, nenhuma execução `dotnet`/`testhost`/`MSBuild` nem diretório temporário `mfx-restore-audit-*` ficou ativo.
- [ ] Linux real, ACL/mount/race na plataforma, CI hospedada e produção continuam sem validação. Testes ignorados no Windows não contam como aprovados Linux.
- [ ] PR #1 ainda sem causa raiz: `gh` não autenticado e logs GitHub não recuperáveis pelo conector. Sem alteração ou publicação especulativa.
- Homologação formal permanece 2/7 (28,6%), sem crédito. Portal local 702 eventos/lote 10; externo pendente de reconciliação por divergência do baseline remoto.
- Mapa técnico consultado; nenhuma estrutura alterada. Oportunidade pesquisada: avaliar `openat2` somente após validar kernel mínimo e comparação isolada com a implementação atual baseada em `openat`/handles.

## PR #1 — conferência dos metadados do runner — 2026-11-02 22:44 BRT
- [x] `gh pr view` e `gh pr checks` tentados com `--repo github.com/raphaelpera85/mulletaflix-completo`; ambos exigem `gh auth login`.
- [x] GitHub API confirma run 41005367220 no merge ref `3bbbd58cc575541ec339d7d3bcc1ca65f5b7d1e8`, enquanto o head do PR é `9a14e93b15e20deabdca159c04f8867d2a8292bb`. Os 16 jobs terminaram `failure`, com `steps=[]` e `runner_id=0`; o job de build tem os mesmos limites.
- [x] O download do log do job retorna 404 `BlobNotFound`; não há checks atuais listados para o head no diagnóstico conectado.
- [ ] Evidência posiciona a falha antes de passos de runner visíveis, mas não revela a causa raiz. Não editar nem rerodar por inferência. Para continuar: restaurar sessão `gh` em `github.com` e verificar runs/annotations/logs disponíveis, ou disponibilizar logs exportados.

## PR #1 — histórico das execuções da branch — 2026-11-02 22:46 BRT
- [x] PR permanece aberto; API do Actions lista 46 execuções na branch e a mais recente é run 41283703952 (01/11, failure, head cbc380c9...).
- [x] Job `Server build & unit tests`: steps ausentes no wrapper e log devolve `BlobNotFound`. O run anterior também mostrou jobs sem passos/runner atribuído.
- [ ] Sem causa raiz ou logs: não alterar código e não repetir reruns. Acesso necessário: `gh auth login --hostname github.com` ou logs exportados.
- A tentativa de consultar distros WSL por processo temporizado foi rejeitada pela política da ferramenta antes de executar; nenhum estado WSL foi inferido.

## PR #1 — annotations vinculadas ao merge ref — 2026-11-02 22:47 BRT
- [x] Run mais recente `41283703952` permanece failure; a lista de check runs mostra 2 annotations em cada job.
- [x] O endpoint de annotations não está disponível no GitHub Fetch; o diagnóstico de PR rejeita check/job ids porque pertencem ao merge SHA, não ao head do PR. `gh` também continua sem autenticação.
- [ ] Conteúdo das annotations e logs permanecem inacessíveis; causa raiz sem confirmação. Próximo passo necessário: autenticação `gh` no GitHub ou exportação das annotations/logs.
- [ ] Consulta de WSL não foi iniciada porque o executor bloqueou o wrapper temporizado; não afirmar suporte Linux local.

# T6.4 — redação de URLs em logs de provedores e guia — 2026-11-02 23:34 BRT

- Redação aplicada em NartoDramaClient, GoodShortClient, provedores YouTube e GuideManager; transporte, URL de requisição, metadados e conteúdo de mídia não mudam.
- Providers focal (YouTube/NartoDrama/GoodShort/redator): 62/62; Live TV focal: 15/15; formato focal aprovado; contrato local do portal: 361/361.
- Teste YouTube simula falha HTTP com URL .strm sintética contendo userinfo e api_key; logs capturados não contêm os valores.
- T6.4 segue parcial: varredura encontrou outros produtores e sinks em DramaBox, ShortMax, ListenBrainz, ItemImageProvider e outros módulos. Sem auditoria integral, Linux, binário instalado ou produção; homologação formal 2/7 (28,6%), sem release/publicação remota.
# T6.4 — continuação: redator compartilhado de URLs remotas — 2026-11-02 23:44 BRT

- `RemoteUrlLogRedactor` agora reside em Helpers de Providers e atende `ItemImageProvider`, ListenBrainz Labs, DramaBox, MidiaStorageOnline, NartoDrama, GoodShort e YouTube.
- Providers focal: 150/150 em Windows/.NET 10 Release. Contrato do portal: 361/361 após corrigir timestamps de atualização no snapshot. `dotnet format whitespace --verify-no-changes` passou.
- Busca ampliada encontrou exceções ainda a revisar em ShortMax e no tratamento de erro de imagens do YouTube. T6.4 permanece parcial.
- Escopo de busca não prova ausência em todos os logs/sinks. T6.4 segue parcial; Linux, runtime instalado e produção pendentes. Homologação formal 2/7; sem release ou publicação remota.
# T6.4 — continuação: exceções de transporte externas — 2026-11-02 23:49 BRT

- Cobertos sinks externos adicionais: ShortMax; YouTube metadata/images; ListenBrainz Similar; TMDb Similar Movies/TV; erro JSON de GoodShort. O redator compartilhado preserva mensagens após remover URLs sensíveis.
- Providers focal cumulativo: 152/152 Windows/.NET 10 Release; format focal passou; contrato portal atualizado para a mesma evidência.
- Pesquisa literal de `_logger.Log*(ex, ...)` nos plugins deixou dois contextos locais. Esse padrão não cobre loggers indiretos ou todos os sinks. T6.4 continua parcial e ainda requer auditoria de outros módulos/call-sites.
- Oportunidade: avaliar redação classificada central via `Microsoft.Extensions.Telemetry` conforme documentação Microsoft. Dependências/compatibilidade não foram verificadas; não foi adotada.
### Continuação T6.4 — exceções em fronteiras comuns de providers e Live TV (2026-11-02 23:56 BRT)

Providers/Live TV agora sanitizam texto de exceção antes do logger em `ProviderManager`, `MetadataService`, OpenLibrary, letras e legendas, `GuideManager` e chamadas de imagem/headend da Schedules Direct. Mensagens de erro de refresh originadas por providers também removem URLs. A chamada HTTP da Schedules Direct mantém host redigido, status HTTP e código de serviço; o corpo bruto, que pode ecoar dados, deixou de ser registrado. Nenhuma requisição ou metadado persistido foi alterado.

Verificações Windows/.NET 10 Release: `Jellyfin.Providers.Tests` 893 aprovados, 13 ignorados, 0 falhas/906; `Jellyfin.LiveTv.Tests` 81/81; `git diff --check` aprovado. `dotnet format whitespace --verify-no-changes` nos arquivos alterados teve verificação parcial: a ferramenta parou em `CHARSET` nos arquivos preexistentes `LyricManager.cs` e `SchedulesDirect.cs`; os demais arquivos selecionados passaram. A busca estática ainda encontra outros logs com exceção bruta fora dos pontos confirmados; T6.4 permanece parcial.

Após fechamento forçado informado pelo usuário, não havia processo de build/test `dotnet` nem processo `git`/`gh` ativo na inspeção. Duas instâncias `Nebula` e duas rotinas `clean_already_sent.py` estavam ativas e não foram encerradas. O histórico do processo já fechado não permite identificar a execução que ficou presa por cinco horas.

Sem release, instalação, publicação remota ou crédito formal. Homologação permanece 2/7 (28,6%). O mapa técnico foi conferido e atualizado: fluxos de log foram ampliados em classes existentes; sem novo módulo, API ou dependência.
## Persistência MariaDB de imagens — 2026-11-03 05:41 BRT

- [x] MariaDB 11.4.4 descartável executado em `127.0.0.1:51588`, com datadir temporário, `max-connections=500`, sem acesso à instância operacional 3306.
- [x] Filtro focal de persistência de imagens passou 11 aprovados, 1 ignorado, 0 falhas; o skip é o caso opt-in de reinício do MariaDB durante delete.
- [x] `ServerPersistenceMariaDbTests` passou 34 aprovados, 1 ignorado, 0 falhas/35 após corrigir fixture que criava `UserData` sem `User`; o teste NextUp passou 1/1 isoladamente antes da reexecução completa.
- [x] Cleanup confirmado: `51588=no-listener`, `scratch-dirs=0`. Build Release passou com 0 erros e 603 avisos existentes.
- [ ] Permanecem pendentes CI hospedada, Linux nativo, instalação persistente, runtime instalado com working tree corrigida e produção. Homologação formal continua 2/7 (28,6%), sem release.
# Fallback de imagem de livro, proveniência dinâmica e idioma — 2026-11-03 12:22 BRT

- [x] RED/GREEN comprovou persistência de fallback de capa de livro com `UpdateItemAsync(..., ItemUpdateType.ImageUpdate, ...)`; `ImageControllerBookFallbackTests` passou 2/2.
- [x] RED/GREEN removeu `ProviderName` remoto de imagens produzidas por `IDynamicImageProvider` local em HTTP, stream e arquivo; testes dinâmicos passaram 4/4.
- [x] Preferência global em item vazio passou 1/1 após correção da fixture de protocolo de arquivo; nenhum novo ajuste de produção foi necessário nesse caminho.
- [x] Auditoria de `MetadataService` encerrada sem correção: `MetadataResult.RemoteImages` só transporta URL e tipo, portanto não perde proveniência ou idioma disponíveis.
- [x] Evidência: Providers 921/921; API 823 aprovados + 3 ignorados; regressão 5.537 aprovados + 98 ignorados/5.635; build Release 0 erros/595 avisos.
- [~] Homologação global segue aberta: runtime instalado 12.1.45 não contém a working tree; providers reais, CI/Linux, instalação persistente, segurança, carga, telemetria, autorização ponta a ponta e produção sem aceite formal.
## Slice 2026-11-03 — falha de imagem local não avança refresh

- [x] RED reproduziu `DateLastRefreshed` avançando após `IOException` do provider local durante validação de imagens.
- [x] GREEN marca `hasRefreshedImages = false` quando `localImagesFailed`; providers remotos continuam bloqueados e o refresh não é considerado concluído.
- [x] Comparação read-only com Jellyfin upstream confirmou o mesmo contrato.
- [x] Teste focal `RefreshMetadata_LocalImageValidationFailure_DoesNotAdvanceDateLastRefreshed`: **1/1**; `MetadataServiceTests`: **51/51**; `Jellyfin.Providers.Tests` sem Integration: **931/931**; build Release: **0 erros e 20 avisos existentes**.
- [~] Correção somente na working tree; runtime instalado 12.1.45, providers remotos reais, CI hospedada, Linux/POSIX, instalação persistente e produção continuam sem aceite.

## Slice 2026-11-03 — persistência de DateLastRefreshed

- [x] RED reproduziu refresh completo sem providers chamando zero vezes `ILibraryManager.UpdateItemAsync`; `DateLastRefreshed` mudava apenas em memória.
- [x] GREEN adicionou `refreshStampNeedsSaving` e inclui o marcador na condição de persistência do item.
- [x] Teste focal `RefreshMetadata_FullRefresh_PersistsRefreshStampWhenNoProviders`: **1/1**; `MetadataServiceTests`: **52/52**; `Jellyfin.Providers.Tests` sem Integration: **932/932**; regressão serial da solução sem Integration: **5.559 aprovados, 98 ignorados, 0 falhas/5.657 casos**; build Release: **0 erros e 594 avisos existentes**.
- [x] Comparação read-only com Jellyfin upstream confirmou a necessidade de persistir o refresh stamp.
- [~] Correção somente na working tree; runtime instalado 12.1.45, providers remotos reais, CI hospedada, Linux/POSIX, instalação persistente e produção continuam sem aceite.
## Aplicação do pacote 12.1.46 — 2026-11-03 22:38 BRT

- [x] ZIP de atualização aplicado por procedimento elevado; cópia retornou código `robocopy 3`, dentro da faixa documentada de sucesso.
- [x] Payload validado contra a instalação: nenhum arquivo ausente ou divergência de tamanho; DLLs-chave conferidas por SHA-256.
- [x] Log confirmou runtime **12.1.46** carregado durante a tentativa de inicialização.
- [!] Startup abortou antes de abrir endpoints: `UnauthorizedAccessException: The restore directory DACL grants write access to an untrusted identity`.
- [~] Processo não permanece ativo; `/health` indisponível. Aceite de instalação, métricas, redaction e testes autenticados continuam pendentes.
- [ ] Autorizar correção controlada das ACLs dos roots de dados, preservando o usuário de execução, SYSTEM e Administradores, e repetir o probe pós-update.
## Probe pós-update 12.1.46 — 2026-11-03 23:06 BRT

- [x] ACLs necessárias ao trust boundary corrigidas com autorização explícita; escrita de `BUILTIN\\Usuários` removida de `C:\ProgramData` e ACEs `Creator Owner` removidas apenas do namespace MulletaFlix validado pelo recovery.
- [x] Runtime reiniciado sem elevação e permaneceu vivo: PID `27268`, arquivo `MulletaFlix.exe` versão **12.1.46**.
- [x] `/health`, `/System/Info/Public` (`Version=12.1.46`, `TargetAbi=12.1.46`) e `/Users/Public`: HTTP 200; log contém `Startup complete`.
- [!] `/metrics`: HTTP 200, mas ainda publica `mysqlconnector_*` com `pool_name` em **160** linhas e ocorrências de host; redaction esperada na working tree não foi comprovada no runtime.
- [~] Gate de instalação/startup concluído; observabilidade, autenticação de itens/capas reais, MariaDB descartável, Linux/CI e providers externos continuam pendentes.
## Rechecagem de startup após update 12.1.46 — 2026-11-03 23:19 BRT

- [x] Reprobe inicial confirmou ausência de processo e endpoints indisponíveis.
- [x] Startup repetido sem elevação com `MulletaFlix.exe --datadir C:\ProgramData\MulletaFlix\Server` permaneceu vivo por 60 segundos no PID `38480`.
- [x] `/health`, `/System/Info/Public` e `/Users/Public`: HTTP 200; `Version=12.1.46`, `TargetAbi=12.1.46`; log contém `Core startup complete` e `Startup complete` às 23:17:24.
- [x] Falha anterior de ACL não foi reproduzida após a correção autorizada.
- [!] `MulletaFlixServer` não está instalado como serviço Windows; o executável não possui autostart de serviço nesta instalação.
- [~] Redaction de `/metrics` continua pendente; status global permanece `partial`.

## Correção de configuração do MeterAdapter — 2026-11-03 23:30 BRT

- [x] Diagnóstico: `StartListening(new MeterAdapterOptions { ... })` era chamado em `Configure`, depois da inicialização potencial do adapter padrão; isso não garantia que o filtro alcançasse o endpoint vivo.
- [x] Correção: `Startup.ConfigureServices` agora usa `Metrics.ConfigureMeterAdapter` com `MetricsLabelRedactor.ShouldPublishInstrument`; o segundo `StartListening` foi removido.
- [x] `MetricsLabelRedactionTests`: **8/8 aprovados**.
- [x] `Jellyfin.Server.Tests`: **88 aprovados, 3 ignorados, 0 falhas/91**.
- [x] `Jellyfin.Server/Jellyfin.Server.csproj`: build Release com **0 erros e 4 avisos XML existentes**.
- [~] Solução completa não aceita nesta rodada: há teste não rastreado pré-existente em `LibraryManagerImageConversionTests.cs` com erros de compilação fora desta correção.
- [!] Runtime instalado `12.1.46` ainda não contém a correção; `/metrics` só poderá ser reavaliado após novo artefato e aplicação controlada.

## Smoke do runtime construído bloqueado por MariaDB — 2026-11-03 23:35 BRT

- [x] Tentativa read-only com binário recém-compilado e datadir/config/log/cache descartáveis em scratch.
- [x] Porta não produtiva preparada; nenhuma alteração foi feita no runtime instalado, banco, mídia ou configuração de produção.
- [!] Mutex global normal bloqueou a segunda instância; `MFLX_E2E_TEST_MODE=true` isolou o mutex da aplicação, mas o startup parou no lock global do MariaDB embutido: `Timed out waiting for another MulletaFlix process to release the embedded MariaDB lock.`
- [x] Scratch removido e processos de teste encerrados.
- [~] `/metrics` do runtime instalado continua pendente; a versão instalada ainda não contém a correção de `Metrics.ConfigureMeterAdapter`.

## Lock global MariaDB e rechecagem live de métricas — 2026-11-03 23:58 BRT

- [x] Código de `MariaDbProcessManager` revisado: runtime normal usa `Global\\MulletaFlix.EmbeddedMariaDb`; `MULLETAFLIX_DB_PORT` muda a porta, não o mutex.
- [x] Confirmado que `EmbeddedMariaDb.Tests` só é usado por chamadas internas com `requireEmbeddedProcess: true`; o startup normal não expõe opção segura para segundo runtime concorrente.
- [x] Runtime instalado PID `38480` permanece ativo; `/metrics` HTTP 200 com **130 linhas `mysqlconnector_*` e 130 ocorrências `pool_name`**.
- [x] Nenhum `testhost`/`Integration.Tests` ativo na rechecagem.
- [~] Smoke do binário corrigido continua bloqueado sem parar produção ou obter MariaDB isolado; não alterar mutex nem desativar recovery como atalho.

## Hotfix live da redaction de métricas — 2026-11-04 00:17 BRT

- [x] Autorização explícita recebida para gerar/aplicar candidato, parar PID `38480`, reiniciar e validar o runtime.
- [x] Hotfix mínimo aplicado: somente `MulletaFlix.dll`; build SHA-256 `8E8553CA311394D3CEE5457009A9D90EDAA0D9737B026821AEEE317EF79379DF`.
- [x] Helper elevado retornou `robocopy=1`, mas a comparação detectou que o DLL não havia sido trocado; cópia elevada de confirmação com `/IS /IT` retornou `3` e os hashes passaram a coincidir.
- [x] Runtime reiniciado como usuário normal no PID `23440`; `/System/Info/Public`, `/health` e `/Users/Public`: HTTP 200; versão/ABI `12.1.46`.
- [x] Log contém `Core startup complete` e `Startup complete`.
- [x] `/metrics` live: HTTP 200; `mysqlconnector_*` **0**, `pool_name` **0**, `User ID`/`server`/`host` nas formas verificadas **0**.
- [x] Gate de redaction instalada aprovado; homologação global permanece `partial` por gates externos e de infraestrutura.

## Auditoria pós-hotfix de imagens e capas — 2026-11-04 00:25 BRT

- [x] Comparação read-only repetida com Jellyfin upstream em `MetadataService`, `ItemImageProvider`, `ImageController` e `RemoteImageController`.
- [x] Não foi encontrado novo defeito que justificasse alteração adicional; as divergências locais permanecem cobertas por testes: precedência de imagens locais, múltiplos backdrops, subpastas seguras, fallback de URLs, preservação de stub, `204` em falha lazy e proveniência de imagem remota.
- [x] `Jellyfin.Providers.Tests`: **944/944**, 0 falhas, 0 ignorados.
- [x] `Jellyfin.Api.Tests`: **831 aprovados, 3 ignorados, 0 falhas/834**.
- [x] `Jellyfin.Server.Implementations.Tests`: **1.757 aprovados, 95 ignorados, 0 falhas/1.852**.
- [x] Runtime PID `23440` rechecado; `/metrics` continua com `mysqlconnector_*` e `pool_name` em **0**.
- [x] Matriz read-only sem credencial: itens/imagens remotas protegidas retornaram `401`; imagem anônima para GUID inexistente retornou `404`.
- [~] Item/capa/metadata reais continuam sem aceite por ausência de credencial autorizada; nenhuma mutação foi executada.

## Wizard exibido em instalação existente — 2026-11-04 00:35 BRT

- [x] Diagnóstico: datadir correto e runtime PID `23440`, mas `/System/Info/Public` e `config/system.xml` indicavam `StartupWizardCompleted=false`.
- [x] Banco existente confirmado somente por leitura: **16 usuários** e aproximadamente **193 mil itens**; não era instalação vazia.
- [x] Correção oficial aplicada por `POST /Startup/Complete`, sem editar banco, criar usuário ou alterar biblioteca.
- [x] Endpoint retornou HTTP 204; `/System/Info/Public` agora informa `StartupWizardCompleted=true`.
- [x] `config/system.xml` agora contém `IsStartupWizardCompleted=true`; `/health`: HTTP 200; processo permanece ativo.
- [x] Log registrou `Saving system configuration`.
- [~] A aba do navegador que já estava no wizard pode exigir reload manual para sair de `/web/#/wizard/start`.
- [x] Navegação limpa após a correção chegou à rota de login (`/web/#/login?...`), não ao wizard; o Chrome do usuário não foi controlado porque o perfil real estava bloqueado por uma instância aberta.

## Causa raiz do wizard em instalação existente — 2026-11-04 00:40 BRT

- [x] Busca de escritores: somente `StartupController.CompleteWizard` atribui `IsStartupWizardCompleted=true`; o modelo inicia com `false`.
- [x] Nenhum escritor de produção encontrado redefinindo o flag para `false` durante update/startup.
- [x] Migração usa o flag para distinguir seed inicial de migração normal, mas não infere conclusão pela quantidade de usuários/itens.
- [x] Decisão: não adicionar heurística automática baseada em dados populados; isso poderia mascarar setup parcialmente concluído.
- [x] Instalação atual foi corrigida pelo endpoint oficial e permanece com `StartupWizardCompleted=true`.
## Atualização 2026-11-05 — Playwright focal

- **GREEN:** `31-accessibility-home-and-details.spec.ts` passou 1/1 em stage descartável autenticado após labels semânticos nos quatro selects de tracks de Item Details e pré-condição de wizard no spec isolado.
- **GREEN focal:** `22-adsense-flow.spec.ts` passou 2/2 após o fallback assíncrono da intro aguardar o interstitial; a regressão ampla histórica ainda não foi reconciliada.
- **GREEN focal:** `99-pagination.spec.ts` passou 2/2 após corrigir a espera da navegação SPA, a asserção do nome de log e a consulta de folders do usuário comum; a regressão ampla histórica ainda não foi reconciliada.
- **BLOCKED refinado:** `21-multi-user-playback.spec.ts` prova playback no primeiro contexto, mas o segundo fica com fila intro + filme sem `NowPlayingItem`; trace registra fallback de intro e `player cannot be null` antes de qualquer `POST /Sessions/Playing`. Próximo TDD deve cobrir o ciclo de vida do player no fallback.
- A homologação global permanece **PARTIAL**; não executar release, deploy ou aplicar bundle ao runtime instalado.
