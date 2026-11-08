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

Validação mais recente: `Jellyfin.Server.Implementations.Tests` passou 764 testes, com 38 testes condicionais ignorados; frontend passou typecheck, lint dos arquivos alterados sem erros e 196 testes; o teste de loading global passou 4/4; a validação de artefatos confirmou `serviceworker.js`; suíte Nebula passou com 149 testes; `validate-stage.ps1` passou com todos os artefatos essenciais, assemblies compatíveis e assets referenciados presentes; a suíte de integração passou 111 testes, com 3 ignorados. O smoke test limpo está preparado e validado sintaticamente, mas exige host/VM Windows elevado; a persistência Mongo foi compilada, mas não exercitada contra servidor local porque `mongod` não está instalado neste ambiente.

## 2026-10-27 — Scheduler de biblioteca: cancelamento no shutdown

- Corrigido o deadlock em que um `Enqueue` bloqueado pelo canal cheio não podia terminar após `ApplicationStopping` cancelar os workers; as esperas de escrita e de workset agora recebem o token do host.
- Mantido o escoamento gracioso no `DisposeAsync` explícito e o guard de descarte reentrante por callback.
- Evidência Release: scheduler 9/9 e `Jellyfin.Controller.Tests` 72/72; frontend Vitest 421/421, typecheck/build/verify aprovados; npm audit 0 vulnerabilidades. Pacotes finais e CI ainda pendentes.
- Release alvo 12.1.39; CI hospedada, pacotes e produção pendentes.
