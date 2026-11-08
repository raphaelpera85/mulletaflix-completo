# Homologação global — MulletaFlix — 2026-11-04

## Stage alinhado ao runtime produtivo 12.1.51 e regressão Playwright bloqueada — 2026-11-08 05:46 BRT

- O `stage/` foi sincronizado com `C:\Program Files\MulletaFlix\Server`, que é o runtime instalado 12.1.51. SHA-256 confirmou igualdade para as DLLs principais, GetAvatar e `MulletaFlix-web/index.html`.
- O stage anterior foi preservado em `C:\Users\Raphael\.mulletaflix-tmp\stage-backup-before-12.1.51-20261108-051102`; nenhuma alteração foi feita no runtime produtivo, no datadir `C:\ProgramData\MulletaFlix\Server` ou na porta `8096`.
- A execução Playwright iniciou com `MulletaFlix version: 12.1.51`, stage/MariaDB isolados (`33100`) e fixtures sintéticas. Autenticação administrativa e vários cenários administrativos passaram.
- A regressão não pôde ser aceita: o cenário 12 falhou; o cenário 17 retornou `MovieCount=0` e `items=[]` após o refresh; o cenário 18 permaneceu bloqueado por mais de 30 minutos sem resultado final. A execução foi encerrada sem `raw-results.json` final.
- O log do servidor registrou posteriormente análise de `Synthetic Movie (2026)` e `Synthetic Movie Two (2026)`, evidenciando uma divergência entre processamento interno e visibilidade na consulta autenticada. Isso permanece como bloqueio de diagnóstico, não como GREEN nem como correção de produto.
- Verificação pós-encerramento encontrou somente os processos produtivos em `3306`; nenhum processo scratch permaneceu. Homologação global continua **PARTIAL**.

## Runtime instalado 12.1.51 — restauração oficial concluída; aceite autenticado ainda pendente — 2026-11-07 04:45 BRT

- A execução autorizada com o backup oficial de `04/11/2026` terminou sem interrupção: o log registrou `Restored primary and media-domain databases`, `Restored MulletaFlix system` às **04:34:56 BRT** e `Core startup complete`/`Startup complete` às **04:35:44 BRT**.
- O runtime instalado é `12.1.51`, executa em `C:\Program Files\MulletaFlix\Server` com `--datadir C:\ProgramData\MulletaFlix\Server`; às **04:44:50 BRT**, `/health` respondeu HTTP 200 `Healthy` e `/System/Info/Public` respondeu `Version=12.1.51`, `TargetAbi=12.1.51` e `StartupWizardCompleted=true`.
- Consultas read-only ao MariaDB restaurado retornaram `users=16`, `baseitems=194203`, `peoplebaseitemmap=850927`, `activitylogs=2671` e `Username=Raphael` com `COUNT(*)=1`. O aumento em relação às contagens intermediárias é compatível com a indexação do catálogo observada no log.
- A primeira tentativa de restore em `12.1.51` permanece preservada como falha histórica de validação de `ActivityLogs`; a segunda execução passou a restauração completa após a correção de desserialização de chaves persistidas com setters privados. Nenhum arquivo JSON, senha ou banco foi editado manualmente.
- O login real e a navegação autenticada não foram executados: o browser vault não tinha login salvo e a solicitação de salvamento foi recusada. Nenhuma credencial foi solicitada, copiada ou registrada.
- Estado global continua **PARTIAL**: restauração/startup/catalogo read-only GREEN; aceite autenticado/UI, Android instrumentado, Linux/POSIX, CI hospedada, integrações reais e aceite formal de produção permanecem pendentes.
- Evidência operacional: `C:\ProgramData\MulletaFlix\Server\log\log_20261107.log`; evento correspondente no portal: `runtime-installed-1251-official-backup-restore-complete-2026-11-07-0445`.

## Auditoria do checklist AG Kit e scanner de segurança — 2026-11-07 05:31 BRT

- O RED foi reproduzido: o scanner varria diretórios de ferramentas/artefatos gerados, podia exceder o timeout e o checklist aceitava exit code `0` sem interpretar findings.
- O TDD RED/GREEN corrigiu o harness: exclusão de `.agents`, `.build`, `artifacts`, `logs`, `stage`, caches e saídas de teste; reconhecimento de middleware de headers aninhado; checklist em modo JSON fail-closed (`total_findings > 0` falha).
- A regressão completa do scanner terminou: dependências sem findings, configuração `[OK]`, e 12 padrões XSS restantes após o TDD do placeholder Bearer. O checklist agora para em Security Scan com falha explícita.
- O finding Bearer foi revisado sem copiar o valor e confirmado como placeholder conhecido de fixture; tokens não-placeholder continuam sendo detectados. Os 12 padrões `innerHTML` ainda exigem triagem contextual. Homologação global permanece **PARTIAL**.

## Inventário estrutural read-only — 2026-11-07 11:05 BRT

- Os quatro componentes foram enumerados sem escrita: Server 5.955 arquivos, Web 1.157, Android 1.744 e Packaging 45.
- Código principal observado: Server 2.824 C# / 477.398 linhas; Web 1.015 TypeScript/TSX / 150.368; Android 790 Kotlin / 117.280; Packaging 20 PowerShell / 2.606.
- A contagem bruta é contaminada por JSONs/fixtures/relatórios, principalmente em Server e Android; `pygount` não está instalado e a métrica não substitui análise semântica ou cobertura.

## Métricas separadas de produção, testes e configuração — 2026-11-07 11:10 BRT

- Server: produção 2.329 arquivos/389.663 linhas; testes 705/135.239; configuração 2.980/1.685.417.
- Web: produção 903/137.687; testes 116/12.844; configuração 140/139.691.
- Android: produção 380/52.706; testes 516/67.840; configuração 999/5.323.457.
- Packaging: produção 23/3.357; testes 6/786; configuração 17/2.727.
- Classificação heurística por caminho, nome e extensão; JSON/XML/Gradle/fixtures não foram tratados como complexidade de produção.

## Correção do falso positivo Bearer Token — 2026-11-07 11:15 BRT

- O TDD reproduziu a diferença entre placeholder Bearer conhecido e token não-placeholder.
- O scanner agora ignora somente placeholders explícitos de teste; tokens não-placeholder continuam sendo encontrados.
- Regressão do scanner: 6/6; `py_compile` e `git diff --check`: PASS.
- Scanner completo: dependências PASS, secrets PASS, configuração PASS, padrões com 3 atribuições `innerHTML` para revisão contextual.
- O checklist continua bloqueado no P0 Security Scan, corretamente fail-closed; não há homologação GREEN.

## Triagem contextual de `innerHTML` — 2026-11-07 11:25 BRT

- O TDD do scanner passou 7/7 e removeu 9 falsos positivos de atribuições estáticas/limpezas.
- As duas construções dinâmicas do GetAvatar foram corrigidas: `escapeHtml` agora codifica aspas simples/duplas para uso seguro em atributos; TDD DOM com payloads perigosos passou 2/2.
- O scanner ainda reporta as duas linhas por ser heurístico; 1 ocorrência permanece aberta no bundle minificado do IntroSkipper, cujo helper ainda exige fonte/mapa e revisão de callers.
- TDD focal do GetAvatar: 2/2, confirmando escaping de `id`, `name` e URL nas duas construções dinâmicas.
- Auditoria AST do IntroSkipper: o único sink `innerHTML` recebe markup literal ou descrições de opções construídas localmente; nenhum fluxo de `fetch`, `ApiClient` ou resposta externa alcança o helper. Contrato estático `test_introskipper_sink.mjs` passou 1/1. O scanner permanece conservador até existir fonte/mapa ou uma refatoração estrutural autorizada.
- Gate de artefato GetAvatar passou em publish self-contained descartável usando o comando oficial: o DLL publicado contém os escapes `&quot;` e `&#39;`. O DLL antigo encontrado em `bin/.../publish` foi identificado como stale e não foi promovido.
- Regressão .NET focal GetAvatar/AvatarController/AvatarFileStore passou **44/44**, com 0 falhas e 3 skips Linux/POSIX; os skips permanecem fora do aceite multiplataforma.
- Smoke browser não autenticado no runtime 12.1.51: tela de login renderizou sem erros de console; endpoints públicos de identidade, usuários públicos, Quick Connect, branding e splash responderam 200. A rota `/System/Configuration/branding` respondeu 401 sem sessão, comportamento esperado.
- Drift do runtime confirmado por leitura pública: `/GetAvatar/ClientScript` responde HTTP 200, mas o `escapeHtml` instalado ainda retorna apenas `node.innerHTML`; source e publish scratch retornam também `&quot;` e `&#39;`. A página de configuração exige sessão (HTTP 401). A correção permanece não aplicada.
- Relatório: `docs/security/innerhtml-triage-2026-11-07.md`.
- O checklist permanece fail-closed até a revisão/teste do item aberto e a decisão sobre as duas construções dinâmicas mitigadas.

## Runtime instalado 12.1.50 — startup GREEN, dados ainda não reconciliados — 2026-11-07 01:12 BRT

- O pacote `v12.1.50` foi aplicado pelo procedimento elevado; o payload instalado terminou com `missing=0` e `mismatches=0`, e a DLL instalada coincide por SHA-256 com o pacote.
- O runtime iniciou com o datadir oficial `C:\ProgramData\MulletaFlix\Server` e permanece executando; `/health` respondeu HTTP 200 (`Healthy`), `/System/Info/Public` respondeu `Version=12.1.50` e `StartupWizardCompleted=true`.
- O log confirmou `20260615195427_InitialMySqlCreate` antes das migrações de código e `Startup complete` às 01:12:47 BRT. A falha anterior `mulletaflix.users` inexistente foi reproduzida antes da correção e não reapareceu nesta inicialização.
- Consulta read-only ao banco após a migração retornou `users=0` e `baseitems=1`. O backup oficial de 04/11 contém `Database/Users.json` e `Database/BaseItems.json` com dados substanciais. Portanto, o gate de startup/ordem de migração está GREEN, mas a reconciliação/restauração dos dados históricos não está comprovada; não conceder aceite de dados ou produção.
- Release GitHub `v12.1.50` confirmou os assets `mulletaflix-update-win-x64.zip` e `mulletaflix_12.1.50_windows-x64.exe`.
- Estado global permanece **PARTIAL**: dados do runtime, jornada autenticada, Android instrumentado, Linux/POSIX, CI hospedada, integrações reais e aceite formal continuam pendentes.

## Probe read-only do runtime instalado — 2026-11-05 21:33 BRT

- `C:\Program Files\MulletaFlix\Server\MulletaFlix.exe`: versão de arquivo/produto **12.1.46**; última gravação `2026-11-03 21:37:38`.
- Porta `8096`: **fechada**; `/health`: HTTP `000` por conexão recusada.
- Nenhuma inicialização, instalação, aplicação de bundle, reinício ou alteração de produção foi realizada.
- O aceite do runtime permanece bloqueado por exigir autorização operacional explícita para iniciar/reparar/reinstalar.

## Inventário read-only do bundle instalado — 2026-11-05

- `index.html` instalado referencia `./assets/index-zMNR1fFC.js`.
- `MulletaFlix-web-master/dist/index.html` atual referencia `./assets/index-BpjyqBDC.js`.
- O payload instalado contém múltiplos artefatos históricos no diretório `assets` (**15.190 JS / 16.447 arquivos**); o `dist` atual contém **638 JS / 1.867 arquivos** em `dist/assets`.
- Isso confirma divergência de payload; não prova sozinho defeito funcional e não autoriza substituição do bundle.

## Auditoria do marcador `Token:` no portal — 2026-11-06 16:21 BRT

- A única ocorrência de `Token:` em `portal-site/homologacao-status.json` está em uma evidência histórica que documenta o parâmetro `-Token` e termina em “nenhum”.
- Não há valor de token, senha, chave ou credencial associado à ocorrência.
- O bloqueio de segurança foi classificado como **documentação textual**, não exposição de segredo.

## Reconciliação de checklist stale — 2026-11-06

- `TODO-AUDITORIA.md` foi corrigido onde ainda dizia que a instalação era 12.1.45; a probe atual confirma executável **12.1.46**, parado.
- A linha histórica de 2026-11-03 permanece preservada como histórico; o item corrente agora distingue versão instalada de bundle/working tree não aplicados.
- O checklist também deixou de tratar a regressão Playwright ampla como pendente: o resultado atual é **40/40 GREEN**.

## Contrato do workflow CI — 2026-11-06

- Os testes estáticos de cobertura do workflow passaram **15/15**, cobrindo MariaDB Windows/Linux, restore Linux privilegiado, MongoDB, auditoria NuGet, packaging, Nebula e serialização de testes.
- Isso confirma que `.github/workflows/ci.yml` declara os gates esperados; não substitui a execução hospedada, que continua bloqueada por billing/infraestrutura.

## Probe dos ambientes externos locais — 2026-11-06 16:52 BRT

- `adb devices -l`: nenhum dispositivo/emulador conectado.
- `wsl.exe -l -v`: nenhuma distribuição Linux instalada.
- `docker info`: Docker indisponível (`command not found`).
- `Tools/NebulaPython/venv/bin/python`: ausente; Python Windows disponível em `3.14.9`, não substitui o ambiente POSIX exigido.
- Nenhuma instalação, download de distribuição, criação de venv falso ou processo externo foi iniciado.

## Regressão Playwright completa após novo TDD de catalogação — 2026-11-05 21:28 BRT

- Execução feita pelo wrapper correto `scripts/playwright/run-isolated-stage.mjs`, com `MFLX_MOVIES_PATH` e `MFLX_SERIES_PATH` explícitos, Vite gerenciado pelo `webServer` do Playwright e stage/MariaDB descartáveis.
- Resultado: **21 specs / 40 testes, 40 aprovados, 0 falhas, 0 instáveis e 0 ignorados**, exit code `0`, duração aproximada de **14,45 min**.
- O novo `21-media-recognition.spec.ts` foi incluído na regressão ampla; o restante da suíte permaneceu GREEN, incluindo AdSense **2/2** e Multi-user.
- Evidências preservadas em `C:/Users/Raphael/AppData/Local/Temp/mflx-playwright-preserved-20261106-valid-full-regression-isolated-wrapper`.
- Cleanup confirmado: nenhum stage, Playwright, Vite ou `MulletaFlix.dll` ativo; MariaDB produtivo PID `8792` permaneceu preservado.

## Diagnóstico movie-only determinístico após refresh explícito — 2026-11-05 15:13 BRT

- Criado `tests/playwright/specs/21-media-recognition.spec.ts` para observar o contrato de catalogação sem depender do diagnóstico histórico removido.
- O TDD executa `addVirtualFolder(..., false)`, dispara `RefreshLibrary` explicitamente e valida contagem e itens reais.
- Resultado: **3/3 execuções consecutivas GREEN**, cada uma ~1,2 min; `MovieCount=2`, `ItemCount=3` e dois filmes sintéticos (`Synthetic Movie (2024)` e `Synthetic Movie Two (2024)`), ambos `Type=Movie`/`MediaType=Video`.
- O resultado reduz o risco de corrida no harness e fornece diagnóstico reproduzível no stage atual; não equivale a aceite do scanner em produção nem elimina os gates de runtime instalado.
- Evidências: `mflx-playwright-preserved-20261105-media-recognition-diagnostic-redgreen`, `...-diagnostic-2` e `...-diagnostic-3`.

## AdSense self-player — falha intermitente e três rechecagens GREEN — 2026-11-05 15:00 BRT

- Uma execução focal do teste `shows the ad after the intro and resumes the main media` falhou com **1 falha / 1 ignorado** após **195,2 s**: `.adsenseInterstitialOverlay` não apareceu em 120 s.
- O erro foi preservado em `C:/Users/Raphael/AppData/Local/Temp/mflx-playwright-preserved-20261105-adsense-final-self-player`; o contexto mostrou a tela de detalhes antes do timeout, sem evidência de que o overlay tenha sido materializado.
- O mesmo teste foi reexecutado **3 vezes consecutivas** em stage descartável, com os mesmos paths sintéticos explícitos: **3/3 aprovados**, aproximadamente **1,3 min por execução**.
- Classificação: **GREEN focal não determinístico / risco residual**, não uma falha funcional confirmada nem uma correção nova. A causa permanece aberta entre timing/lifecycle do player self-managed e materialização do interstitial.
- Snapshots das rechecagens: `mflx-playwright-preserved-20261105-adsense-self-player-recheck`, `...-recheck-2` e `...-recheck-3`. Runtime instalado, bundle instalado e produção permaneceram intocados.

## Regressão Playwright ampla GREEN após compatibilidade Vite/Kestrel — 2026-11-05 14:34 BRT

- O runner passou a iniciar o Vite como `webServer` descartável e os specs de fixtures passaram a navegar pela origem Vite explícita; o Kestrel continua responsável pelo stage/API.
- TDD `tests/playwright/support/stage.test.mjs`: **3/3**.
- Regressão completa válida em stage descartável, PTY, fixtures sintéticas e `MFLX_MOVIES_PATH`/`MFLX_SERIES_PATH` explícitos: **20 specs / 39 testes, 39 aprovados, 0 falhas, 0 timeouts, 0 instáveis e 0 ignorados**, exit code `0`, aproximadamente **13,8 min**.
- Os cenários `24`–`29` passaram **11/11** no runner amplo; nenhuma alteração foi aplicada ao produto compilado ou ao runtime instalado.
- Evidência preservada em `C:/Users/Raphael/AppData/Local/Temp/mflx-playwright-preserved-20261105-full-regression-after-vite-fixture-origin`.

O gate Playwright amplo deixa de ser bloqueio local. A homologação global permanece **PARTIAL** por gates de instalação, Android instrumentado, Linux/POSIX, CI hospedada, integrações reais e bundle instalado divergente.

## Regressão ampla após serialização do refresh — 2026-11-05 13:33 BRT

- Reexecução completa em stage descartável, PTY, fixtures sintéticas e com `MFLX_MOVIES_PATH`/`MFLX_SERIES_PATH` explícitos.
- Resultado auditável: **20 specs / 39 testes; 28 aprovados, 11 falhas, 0 timeouts, 0 instáveis e 0 ignorados**, exit code `1`, duração aproximada de **15,4 min**.
- `21 - Multi user playback`, AdSense, Unified Search e Pagination passaram nesta rodada; as 11 falhas continuam nos cenários `24`–`29` do runner Kestrel, já aprovados **13/13** nas configurações Vite dedicadas.
- Evidência preservada em `C:/Users/Raphael/AppData/Local/Temp/mflx-playwright-preserved-20261105-full-regression-after-refresh-sequencing-with-series`.
- Stage encerrou sem processos residuais; MariaDB produtivo PID `8792` permaneceu ativo e o runtime instalado não foi alterado. Homologação global continua **PARTIAL**.

## GREEN focal após correção da ordem de criação/refresh das bibliotecas — 2026-11-05 12:58 BRT

- TDD `tests/playwright/support/media-library.test.mjs`: passou com a garantia de que `addVirtualFolder` não inicia scan concorrente (`refreshLibrary=false`); o refresh explícito continua controlando a catalogação.
- `21 - Multi user playback`: passou **1/1 em duas execuções consecutivas** após a mudança do harness, em stage descartável, PTY e fixtures sintéticas.
- Nos logs da segunda execução, o stage reconheceu `Synthetic Movie (2024)` e `Synthetic Movie Two (2024)` e registrou playback iniciado para os dois usuários; evidências preservadas em `mflx-playwright-preserved-20261105-multiuser-refresh-sequencing-fix` e `...-pass2`.
- A correção elimina a corrida conhecida entre criação da biblioteca e refresh, mas não prova sozinha uma correção no scanner do produto: os diagnósticos históricos `6/7` vazios permanecem registrados como evidência anterior e a regressão ampla Kestrel não foi reexecutada.
- Runtime instalado, banco produtivo e bundle instalado permaneceram intocados; homologação global continua **PARTIAL**.

## Atualização crítica — reconhecimento de mídia e multi-user não determinísticos — 2026-11-05 12:40 BRT

- Sete diagnósticos movie-only instrumentados: **6/7** retornaram `MovieCount=0`, `SeriesCount=0`, `ItemCount=0` e `getItems=[]`; **1/7** materializou dois filmes sintéticos (`MovieCount=2`, `ItemCount=3`).
- O virtual folder `Filmes` foi criado em todas as rodadas, isolando o bloqueio após a criação da biblioteca.
- Duas execuções de multi-user posteriores falharam na espera da segunda sessão (`Expected: 2`, `Received: 0`); uma execução posterior passou **1/1** após materialização de mídia.
- O GREEN de multi-user deve ser rebaixado para **GREEN não determinístico / não estável** até o reconhecimento de mídia ser reproduzível.
- Homologação global permanece **PARTIAL**; catálogo/scanner é agora o bloqueio técnico prioritário antes de nova promoção de playback.

Evidências preservadas nos snapshots `mflx-playwright-preserved-20261105-instrumented*`, `mflx-playwright-preserved-20261105-multiuser-pathfix`, `mflx-playwright-preserved-20261105-multiuser-hls-instrumented` e `mflx-playwright-preserved-20261105-hls-final`.

## Regressão ampla após GREEN de AdSense — 2026-11-05

- **20 specs / 39 testes** em stage descartável, PTY e fixtures sintéticas.
- **28 aprovados, 11 falhas, 0 timeouts, 0 instáveis, 0 ignorados**, exit code `1`, duração aproximada de **15,4 min**.
- Multi-user **1/1**, AdSense **2/2** e Unified Search **1/1** passaram; Pagination permanece GREEN focal.
- As 11 falhas restantes estão nos cenários `24`–`29`, associados à incompatibilidade do runner Kestrel com fixtures/módulos Vite dev.
- Evidência: `C:/Users/Raphael/AppData/Local/Temp/mflx-playwright-preserved-20261105-full-regression-after-unified-search`.
- O runtime instalado, banco produtivo e bundle instalado não foram alterados.

O aceite global continua **PARTIAL**. O próximo gate funcional é reconciliar os cenários `24`–`29` em configurações compatíveis, sem mascarar falhas.

## Unified Search GREEN focal após correção do harness — 2026-11-05

- RED reproduzido: timeout de 120 s em `navigateStage`, com a rota já renderizada no snapshot.
- Correção mínima no harness: aguardar a hash efetiva da rota, limitando a espera do `Promise` de `Dashboard.navigate` e preservando a propagação de erro.
- TDD `stage.test.mjs`: **2/2**; `23 - Unified search`: **1/1** em 38,7 s.
- Evidência: `C:/Users/Raphael/AppData/Local/Temp/mflx-playwright-preserved-20261105-unified-search-green`.

Unified Search deixou de ser bloqueio funcional independente em execução focal. O aceite global continua **PARTIAL** pela regressão ampla residual e gates externos.

## GREEN focal AdSense playback — 2026-11-05

- RED reproduzido antes da correção: a intro sintética falhava e `playIntroThenMain` iniciava o filme imediatamente, sem aguardar o interstitial.
- TDD `src/utils/playback/playIntroThenMain.test.ts`: **4/4 aprovados**, incluindo espera do callback assíncrono de erro antes do fallback.
- `22 - AdSense playback flow`: **2/2 aprovados** em aproximadamente **2,4 min**, cobrindo carregamento normal e falha do script AdSense em stage descartável com fixtures sintéticas.
- Bundle reconstruído/copiado somente para `stage/MulletaFlix-web`; runtime instalado `12.1.46`, banco produtivo e bundle instalado permaneceram intocados.
- Evidência: `C:/Users/Raphael/AppData/Local/Temp/mflx-playwright-preserved-20261105-adsense-final-async-fallback`.

AdSense permanece com evidência focal GREEN, mas não determinística: uma falha histórica foi seguida por três rechecagens GREEN e a regressão completa atual passou 2/2. O veredito global continua **PARTIAL** por bundle instalado divergente, Android instrumentado, Linux/POSIX, CI hospedada, integrações reais e demais gates externos.

## Atualização da regressão ampla pós-HLS — 2026-11-05

Nova execução completa do Playwright em stage descartável, PTY e fixtures sintéticas:

- **20 specs / 39 testes**;
- **24 aprovados, 13 falhas, 0 instáveis, 2 ignorados**;
- exit code `1`, duração aproximada de **15,5 min**;
- nenhum processo stage residual permaneceu;
- runtime instalado `12.1.46`, banco produtivo e bundle instalado permaneceram intocados.

Classificação bruta da regressão ampla: AdSense (1), scroll buttons (2), My Requests (2), request autocomplete (2), Nebula navigation (2), backup coverage (2), accessibility My Requests (1) e pagination (1). O cenário multi-user não reapareceu como falha e mantém GREEN focal **1/1**.

O veredito formal permanece **PARTIAL**. Scroll buttons, requests/a11y e pagination têm GREEN focal; AdSense e os gates de plataforma/instalação/CI continuam pendentes.

Evidências: `MulletaFlix-web-master/reports/playwright/playwright-summary.json`, `raw-results.json` e `C:/Users/Raphael/AppData/Local/Temp/mflx-playwright-preserved-20261105-full-regression-after-multiuser`.

### Resultado da triagem inicial

- `24-scrollbuttons.spec.ts`: **GREEN 2/2** no config dedicado Vite (`npm run test:playwright:scrollbuttons`, 10,6 s). As falhas da suíte ampla foram classificadas como incompatibilidade do runner Kestrel com endpoints de desenvolvimento `/@fs/...` e `/elements/...`, não como defeito confirmado do componente.
- `99-pagination.spec.ts`: o RED histórico foi reproduzido e depois corrigido no harness. A execução focal atual passou **2/2** após evitar navegação duplicada para `/home`, normalizar o nome do arquivo de log e reutilizar os folders do administrador para o usuário comum.
- O trace de pagination foi removido por conter metadados de autenticação do stage; somente evidências sem segredo foram mantidas.

- `npm run test:playwright:requests`: **8/8 GREEN**.
- `npm run test:playwright:a11y`: **3/3 GREEN**.
- Com a revalidação dedicada, 11 das 13 falhas brutas da rodada histórica foram incompatibilidades de runner/configuração de fixtures; a regressão ampla válida posterior foi reexecutada com o wrapper isolado e terminou **40/40 GREEN**, incluindo AdSense e o novo diagnóstico movie-only.

## Veredito

**HOMOLOGAÇÃO GLOBAL: PARTIAL — não aprovada como aceite formal.**

A working tree foi inventariada e os gates locais mais importantes foram reexecutados. O servidor .NET, o frontend fonte, o Android JVM/build, Nebula/Python disponível, packaging focado, portal e a regressão Playwright ampla têm evidência técnica positiva. A regressão Playwright mais recente passou **40/40** após o runner separar a origem Vite das APIs Kestrel e incluir o TDD movie-only; permanecem bloqueios objetivos no bundle instalado divergente do `dist`, runtime instalado atualmente parado, Android instrumentado, Linux/POSIX completo, lote de release com falha, integrações reais e CI hospedada.

## Escopo auditado

- `MulletaFlix-master`: servidor .NET 10, providers, API, implementações, runtime instalado.
- `MulletaFlix-web-master`: TypeScript/React, Vitest, build e Playwright stage.
- `MulletaFlix-android`: JVM, lint, assemble e disponibilidade de instrumentação.
- `MulletaFlix-packaging-master` e `tools/release`: scripts, stage, segurança e release tests.
- `nebula` e `MulletaFlix-master/Tools/NebulaPython`: pytest, compileall e limites de ambiente.
- `portal-site`: contrato do status e suíte Node.
- Jellyfin upstream: comparação read-only dos contratos de imagens, sem copiar alterações nem publicar.

## Inventário e integridade

- Working tree preservada; não houve reset, clean, commit, push ou release.
- Branch/HEAD do checkout principal e alterações prévias foram preservados; há alterações locais anteriores ao ciclo.
- Inventário filtrado do checkout (excluindo vendor/build/cache): 4 `AGENTS.md`, 8 soluções, 38 `build.gradle.kts`, 102 `*.csproj` e 738 Markdown.
- `graphify-out/graph.json` possui 65.429 nós e 0 arestas; portanto não é evidência válida de dependências.
- Não foram lidos, reutilizados ou gravados segredos. Credenciais/tokens observados permanecem `[REDACTED]`.
- Processos residuais stage foram verificados: não havia `run-isolated-stage`, runner Playwright ou processo `stage` ativo no encerramento desta rodada.

## Gates executados

### Servidor

- `dotnet build MulletaFlix.sln --configuration Release --no-restore -m:1`: **passou**.
- `dotnet test MulletaFlix.sln --configuration Release --no-build --no-restore --filter 'FullyQualifiedName!~Integration' -m:1`: **5.621 aprovados e 101 ignorados** nos projetos com resultados visíveis; integração não entra neste número.
- Providers: **977/977** na execução registrada; testes de imagem focais **98/98**; fluxo TMDb de séries **3/3**.
- API: lote auditado **831 aprovados/3 ignorados**; `RemoteImageController` focal **5/5**.
- Server implementations: **1.757 aprovados/95 ignorados** no lote auditado.
- TDD focal de retry de persistência: `ItemRepositoryTransientRetryTests` **17/17 aprovados**. A regressão contra MariaDB descartável real `ServerPersistenceMariaDbTests` passou **34/35**, com 1 ignorado por contrato (`SaveImagesAsync_RetriesAfterMariaDbProcessRestartDuringDelete`). A primeira execução foi bloqueada por `Too many connections` no scratch; após configurar `max_connections=512`, a suíte passou. Esse resultado confirma a persistência sob concorrência no escopo testado, mas não resolve nem prova a visibilidade do item no endpoint autenticado do stage.
- Runtime instalado: versão **12.1.46**; `/health` e `/System/Info/Public` HTTP 200; wizard concluído; sem serviço Windows `MulletaFlixServer`.
- Nenhum refresh, download TMDb, alteração de item, capa, biblioteca ou banco de produção foi executado.

### Web

- `npm run build:check`: **passou**.
- `npm run lint:changed`: **passou**.
- Vitest: **421/421** no gate registrado.
- `npm run build:production` e `npm run verify:build`: **passaram**; **1.929 artefatos**, **294,3 KB gzip**, limite **420 KB**.
- Playwright stage com PTY: Wizard **2/2**, Unified search **1/1**.
- Item Details: **PASS 3/3** em stage descartável autenticado. A investigação mostrou que o helper selecionava a pasta `movies` em vez de um item de filme (`IsFolder=false`, `IncludeItemTypes=Movie`); após a correção, o cenário passou. O teste também foi alinhado ao contrato real de `getSecondaryMediaInfoHtml`, que retorna vazio para filmes quando não há metadata secundária. A contagem **22 aprovados, 15 falhas e 2 ignorados** pertence à rodada histórica anterior; a regressão ampla posterior passou **39/39**, sem alterar o veredito global parcial.
- Acessibilidade Home/Item Details: **PASS focal 1/1** em 2026-11-05. Os quatro selects de tracks em `src/controllers/itemDetails/index.html` receberam labels semânticos (`Video`, `Audio`, `Subtitles`); o spec isolado autenticado passou após o helper concluir o wizard quando executado sozinho.
- AdSense: **evidência histórica GREEN 2/2**, mas a rechecagem de 2026-11-05 registrou uma falha focal seguida de 3 passes consecutivos; o status atual é **GREEN focal não determinístico**. O TDD aguarda o callback assíncrono do erro de intro; o playbackmanager tenta mostrar o interstitial no fallback e nos eventos de parada da intro. O runtime instalado não foi alterado.
- Pagination: **GREEN focal 2/2**. O helper de navegação agora ignora a rota já ativa, evitando a promessa pendente de `Dashboard.navigate` em `/home`; o spec também compara somente o nome do arquivo de log e reutiliza os folders conhecidos do administrador no cenário de usuário comum.
- Reconhecimento de mídia no stage: diagnósticos históricos inconsistentes permanecem registrados, mas o fluxo completo de pagination reconheceu filmes/séries e passou **2/2** nesta execução focal. Isso não equivale a homologar todas as integrações reais do scanner.
- A fonte referencia `index-zMNR1fFC.js`, enquanto a instalação contém `index-C2qZimXg.js` e acúmulo de artefatos antigos; não aplicar bundle sem procedimento autorizado.

#### Mapa de falhas Playwright restantes

Execução ampla autenticada em stage descartável: **22 passados, 15 falhos, 2 ignorados**. Em rodada ampla posterior, a execução terminou com **21 aprovados, falhas múltiplas, 2 não executados e exit code 1** após aproximadamente 18,7 minutos. O registro detalhado da rodada posterior foi sobrescrito por execuções focadas; portanto somente a contagem final e a saída consolidada do processo são preservadas. As falhas foram preservadas como pendências independentes, sem crédito para aceite global:

- Multi-user playback — **GREEN focal 1/1**: o contrato de eventos HLS foi alinhado para `hlsManifestParsed`/`hlsError`, o listener é registrado antes de `attachMedia`/`loadSource`, e o cleanup do player no erro propagado permanece coberto por TDD. `htmlMediaHelper.test.ts` + `playbackmanager.intro-failure.test.ts` passaram **3/3**; `npm run build:check` passou; bundle reconstruído somente no stage; `21 - Multi user playback` passou **1/1** em **1,5 min** com fixtures sintéticas. O runtime instalado 12.1.46 não foi alterado.
- O diagnóstico histórico de HLS continua registrado como RED anterior: primeiro contexto com `POST /Sessions/Playing`/`NowPlayingItem`, segundo com `NowPlayingQueue` e `player cannot be null`. A correção focal foi validada no gate posterior; não se atribui esse GREEN ao endpoint de sessões.
- AdSense playback: **GREEN focal 2/2** após aguardar o interstitial no fallback assíncrono da intro; a execução ampla ainda precisa ser repetida para reconciliar a contagem histórica.
- Scroll buttons: duas expectativas de biblioteca/scroller falharam (heading `Movies` ausente; script `emby-scroller.ts` não carregou).
- My Requests/backup: regiões acessíveis esperadas não foram renderizadas em quatro casos.
- Request autocomplete: o input `input[name="title"]` não apareceu nos dois cenários.
- Nebula navigation: `#globalStatusBadge` não apareceu nos dois viewports.
- Accessibility Search/Player: **corrigido em execução focada 1/1**; a rodada ampla ainda precisa ser repetida para reconciliar a contagem global.
- Pagination: **GREEN focal 2/2** na execução posterior; a falha ampla histórica foi reclassificada como combinação de navegação duplicada, asserção de log e consulta de folders sob usuário comum.

Esses casos exigem triagem própria: confirmar rota/fixture/contrato antes de alterar produção ou enfraquecer asserções.

### Android

- `testDebugUnitTest`: **1.788/1.788** no gate auditado.
- `lintDebug` e `assembleDebug`: **BUILD SUCCESSFUL**.
- Instrumentação: **não aceita**; `adb`/AVD ativo não foi comprovado nesta rodada.
- Smoke JVM/build não equivale a aceite de instalação, hardware, TV/tablet ou troca de servidor.

### Nebula/Python

- Nebula: **211 passed/1 skipped** registrado.
- NebulaPython: **726 passed/2 skipped** registrado.
- `compileall`: passou nos caminhos auditados.
- Lint/typecheck e CI Linux/POSIX: **bloqueados/inconclusivos** por ausência comprovada de `venv/bin/python`/ambiente completo.

### Packaging e portal

- Scripts de packaging, `validate-stage.ps1` e testes de segurança focados foram executados; o teste DuckDNS passou explicitamente com validação de caminho, reparse point, owner e ACL.
- O lote `tools/release/tests/*.test.mjs` + `*.test.sh` foi isolado por arquivo: todos os `.test.mjs` e scripts shell passaram, exceto `linux-nebula-python-dependencies.test.sh`, que terminou **exit 127** porque `venv/bin/python` não existe no ambiente POSIX disponível.
- `portal-site/homologacao-status.json` foi sincronizado sem segredos, adicionando o evento global desta sessão.
- Suíte portal: **367/367** após atualizar as expectativas de cobertura do roadmap para 357 concluídos, 74 pendentes, 7 em execução e 438 totais.
- `git diff --check`: passou.

## Correções já aplicadas e verificadas no ciclo amplo

1. Fallback TMDb por título, título original e ano para filmes sem ProviderId.
2. Resolução da série pai para imagens de séries, temporadas e episódios sem ProviderId.
3. IDs TMDb válidos continuam prioritários.
4. Remoção de fallback automático inglês para biblioteca `pt-BR`.
5. Configuração de métricas por adapter antes do startup e redaction live/local.
6. ACLs de trust boundary corrigidas sem trocar o usuário de execução.
7. Login Playwright resiliente a formulário manual, campos modernos e cartões públicos.
8. Preparação Playwright sem caminhos hardcoded; bibliotecas de stage exigem paths explícitos.
9. Wizard Playwright aguarda e valida o retorno HTTP de `POST /Startup/User`.
10. Contrato local de `RemoteImages` comparado ao Jellyfin upstream sem divergência relevante que justifique novo patch.
11. Harness de Item Details normaliza `ItemId` retornado por `GET /Library/VirtualFolders` para o campo `Id` usado pelo helper.
12. Helper de Item Details filtra itens de filme (`IsFolder=false`, `IncludeItemTypes=Movie`) para não abrir detalhes da pasta da biblioteca; o spec torna metadata secundária opcional conforme o contrato do frontend.

## Mapa de correções e melhorias

### Prioridade P0 — desbloquear aceite

- **P0.1 — Item Details stage**: **resolvido no escopo do spec 33**. RED reproduzido; o helper selecionava uma pasta em vez de um filme e a asserção de metadata secundária contrariava o contrato do frontend. Após correção mínima do harness, o spec passou 3/3 em stage descartável autenticado. A suíte Playwright completa ainda tem 15 falhas e 2 ignorados, que permanecem pendentes.
- **P0.2 — release tests**: rodar cada `.test.mjs` e `.test.sh` isoladamente, capturar o primeiro erro e corrigir na origem; não aceitar o lote por exit code parcial.
- **P0.3 — bundle runtime**: reconciliar fonte/dist/instalação por procedimento oficial, com hash, reinício controlado e smoke posterior. Requer autorização operacional explícita.
- **P0.4 — autenticação segura**: revogar/substituir token exposto; disponibilizar credencial de stage por vault/variável segura, sem colar segredo no chat.
- **P0.5 — cleanup/concurrency**: manter stage e MariaDB descartáveis, documentar mutex global e impedir execução concorrente nos mesmos arquivos.

### Prioridade P1 — cobertura de plataforma

- **P1.1 — Android instrumentado**: provisionar AVD descartável e executar perfis phone/tablet/TV; registrar logs, screenshots e limpeza.
- **P1.2 — Linux/POSIX**: provisionar ambiente Python conforme `pyproject.toml`/requirements e executar Nebula, packaging e scripts nativos.
- **P1.3 — integrações**: executar MongoDB/MariaDB em datadir scratch, portas não produtivas e cleanup verificável; publicar contagens por classe/caso.
- **P1.4 — CI hospedada**: reproduzir gates em Actions e anexar artefatos; smoke local não substitui CI.

### Prioridade P2 — robustez e manutenção

- **P2.1 — remover divergência de bundle**: evitar acúmulo de `index-*.js`/assets na instalação e adicionar teste de consistência manifest → instalação.
- **P2.2 — observabilidade de frontend**: isolar `Could not get current user undefined`, `No routes matched location "/home"` e 404 de imagem com teste de runtime, não apenas tsc/lint.
- **P2.3 — contratos DTO**: gerar/validar tipos a partir do C# real, respeitando PascalCase de enums/propriedades e testes de wire format.
- **P2.4 — testes de imagem**: expandir fixtures para idioma, ausência de IDs, URLs múltiplas, capas persistidas e erros HTTP sem apagar imagens manuais.
- **P2.5 — auditoria de testes**: separar contagens `passed/skipped/failed`, eliminar números históricos no portal e impedir que exit 0 sem resumo seja promovido automaticamente.

## Pendências não executadas por autorização/ambiente

- **PENDING: aplicar bundle web validado ao runtime — awaiting your authorization.**
- **PENDING: concluir a suíte E2E autenticada/visual — credencial de stage foi usada somente localmente; a execução ampla terminou 22/39 aprovados e ainda possui 15 falhas e 2 ignorados.**
- **PENDING: executar Android instrumentado, CI hospedada e integrações em infraestrutura descartável — awaiting environment availability.**

## Critério para mudar de `partial` para aprovado

Somente após: (1) zero falhas abertas nos gates obrigatórios; (2) bloqueios P0 reproduzidos e regressões verdes; (3) bundle executado ser o bundle validado; (4) E2E autenticado/visual e Android instrumentado observados; (5) integração e Linux/POSIX com contagens detalhadas; (6) release tests isolados e lote verde; (7) portal, Vault e artefatos sem segredos; (8) decisão explícita de aceite formal.
