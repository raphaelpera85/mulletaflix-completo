# Playwright — regressão ampla e correção focada — 2026-11-04

## Regressão completa com o TDD movie-only incluído — 2026-11-05 21:28 BRT

- Wrapper: `scripts/playwright/run-isolated-stage.mjs`; paths sintéticos de filmes e séries explícitos; Vite descartável gerenciado pelo `webServer`.
- Resultado: **21 specs / 40 testes, 40 aprovados, 0 falhas, 0 instáveis e 0 ignorados**, exit code `0`, aproximadamente **14,45 min**.
- Evidência: `C:/Users/Raphael/AppData/Local/Temp/mflx-playwright-preserved-20261106-valid-full-regression-isolated-wrapper`.
- O teste novo `21-media-recognition.spec.ts` foi absorvido pela regressão sem introduzir falhas. Stage e MariaDB descartáveis foram encerrados após o run.

## Diagnóstico movie-only determinístico — 2026-11-05 15:13 BRT

- Criado `tests/playwright/specs/21-media-recognition.spec.ts` para substituir o diagnóstico histórico removido.
- O cenário cria a biblioteca sem scan automático, dispara `RefreshLibrary` explicitamente e consulta `getItemCounts`/`getItems`.
- **3/3 execuções consecutivas GREEN**, ~1,2 min cada: `MovieCount=2`, `ItemCount=3` e dois filmes sintéticos reconhecidos como `Movie`/`Video`.
- Evidências: `C:/Users/Raphael/AppData/Local/Temp/mflx-playwright-preserved-20261105-media-recognition-diagnostic-redgreen`, `...-diagnostic-2` e `...-diagnostic-3`.
- Isso valida a pré-condição de catalogação no stage descartável atual; não fecha scanner em produção ou runtime instalado.

## AdSense self-player intermitente — 2026-11-05 15:00 BRT

- Uma execução focal do teste `shows the ad after the intro and resumes the main media` falhou com **1 falha / 1 ignorado** após **195,2 s**, aguardando `.adsenseInterstitialOverlay` por 120 s.
- O mesmo teste passou em **3 rechecagens consecutivas 1/1**, em aproximadamente **1,3 min** cada.
- Classificação: **GREEN focal não determinístico**. A causa entre lifecycle/timing do player self-managed e materialização do overlay continua sem prova causal; nenhuma nova correção de produto foi aplicada.
- Evidências: `C:/Users/Raphael/AppData/Local/Temp/mflx-playwright-preserved-20261105-adsense-final-self-player` e `...-adsense-self-player-recheck*`.

## Regressão ampla GREEN após origem Vite para fixtures — 2026-11-05 14:34 BRT

- O RED foi isolado no harness: o runner amplo usava Kestrel para servir fixtures que dependem de transformação Vite (`/@fs`, imports TypeScript/TSX e módulos de desenvolvimento).
- Correção mínima: `playwright.stage.config.ts` inicia Vite via `webServer` em porta descartável; `PW_FIXTURE_BASE_URL` direciona fixtures 24–29 para essa origem; a página/API principal continua no Kestrel do stage.
- TDD `tests/playwright/support/stage.test.mjs`: **3/3**.
- Regressão válida: **20 specs / 39 testes, 39 aprovados, 0 falhas, 0 timeouts, 0 instáveis e 0 ignorados**, exit code `0`, aproximadamente **13,8 min**.
- Cenários 24–29: **11/11** aprovados no runner amplo.
- Snapshot: `C:/Users/Raphael/AppData/Local/Temp/mflx-playwright-preserved-20261105-full-regression-after-vite-fixture-origin`.
- Nenhuma alteração no runtime instalado, banco produtivo, bundle instalado ou mídia de produção.

O gate Playwright amplo foi reconciliado como GREEN local; isso não equivale a homologação global completa.

## Regressão ampla após serialização do refresh — 2026-11-05 13:33 BRT

- Execução completa com `MFLX_MOVIES_PATH` e `MFLX_SERIES_PATH` explícitos, stage descartável, PTY e fixtures sintéticas.
- **20 specs / 39 testes: 28 aprovados, 11 falhas, 0 timeouts, 0 instáveis e 0 ignorados**, exit code `1`, aproximadamente **15,4 min**.
- Multi-user, AdSense, Unified Search e Pagination passaram; as falhas permaneceram nos cenários `24`–`29`: scroll buttons, My Requests, autocomplete, Nebula, backup e accessibility My Requests.
- Evidência: `C:/Users/Raphael/AppData/Local/Temp/mflx-playwright-preserved-20261105-full-regression-after-refresh-sequencing-with-series`.
- Cleanup confirmado: nenhum stage/runner/Vite/MulletaFlix ficou ativo; MariaDB produtivo PID `8792` foi preservado.

## GREEN focal após serialização de criação/refresh das bibliotecas — 2026-11-05 12:58 BRT

- O TDD `tests/playwright/support/media-library.test.mjs` passou após `addVirtualFolder` deixar de iniciar scan automático concorrente; `RefreshLibrary` permanece explícito.
- `21 - Multi user playback` passou **1/1 em duas execuções consecutivas** com stage descartável, PTY e fixtures sintéticas.
- A segunda execução registrou no log os dois filmes sintéticos reconhecidos e playback iniciado para os dois usuários. Snapshots: `C:/Users/Raphael/AppData/Local/Temp/mflx-playwright-preserved-20261105-multiuser-refresh-sequencing-fix` e `...-pass2`.
- Resultado classificado como **GREEN focal do harness/pré-condição**, não como homologação global do scanner: a regressão ampla Kestrel continua com a contagem anterior de **28/39 aprovados e 11 falhas**, e os diagnósticos históricos vazios permanecem no histórico.

## Atualização crítica — reconhecimento de mídia não determinístico — 2026-11-05 12:40 BRT

- Sete execuções instrumentadas do diagnóstico movie-only passaram como teste de diagnóstico, mas somente **1/7** materializou mídia: seis retornaram `MovieCount=0`, `SeriesCount=0`, `ItemCount=0` e `getItems=[]`; uma retornou **2 filmes** e `ItemCount=3`.
- Em todas, o virtual folder `Filmes` foi criado (`CollectionType=movies`), isolando o problema após a criação da biblioteca: reconhecimento/materialização/timing do scanner.
- Duas tentativas seguintes de `21 - Multi user playback` falharam esperando a segunda sessão: `Expected: 2`, `Received: 0`, timeout de 90 s; o snapshot mostra o segundo contexto ainda no login.
- Uma execução posterior passou **1/1** após uma rodada com mídia materializada. Portanto, o GREEN do multi-user é atualmente **não determinístico** e não pode ser usado para fechar o gate de catálogo ou estabilidade E2E.
- O problema deve ser tratado como bloqueio de reconhecimento de mídia antes de novas correções HLS/playback.

Evidências preservadas nos snapshots `mflx-playwright-preserved-20261105-instrumented*`, `mflx-playwright-preserved-20261105-multiuser-pathfix`, `mflx-playwright-preserved-20261105-multiuser-hls-instrumented` e `mflx-playwright-preserved-20261105-hls-final`.

## Regressão ampla após GREEN de AdSense — 2026-11-05

- Stage descartável, PTY e fixtures sintéticas; **20 specs / 39 testes**.
- Resultado auditável após a correção de Unified Search: **28 aprovados, 11 falhas, 0 timeouts, 0 instáveis e 0 ignorados**, exit code `1`, duração aproximada de **15,4 min**.
- Multi-user playback: **1/1 aprovado**; AdSense: **2/2 aprovados**; Unified Search: **1/1 aprovado**; Pagination: GREEN focal preservado.
- As 11 falhas restantes continuam concentradas nos configs/fixtures que exigem recursos Vite dev (`24`–`29`).
- Evidências preservadas em `C:/Users/Raphael/AppData/Local/Temp/mflx-playwright-preserved-20261105-full-regression-after-unified-search`.
- Nenhuma alteração foi aplicada ao runtime instalado `12.1.46`; nenhum processo stage permaneceu ativo.

## Unified Search GREEN focal após correção do harness — 2026-11-05

- O RED da regressão ampla foi reproduzido como timeout de 120 s em `navigateStage`, embora a rota de busca já tivesse renderizado.
- `navigateStage` agora trata a mudança efetiva da hash como conclusão, sem depender indefinidamente do `Promise` de `Dashboard.navigate`; erros explícitos continuam sendo propagados.
- TDD `tests/playwright/support/stage.test.mjs`: **2/2 aprovados**, incluindo o caso de Promise pendente após mudança da hash.
- `23 - Unified search`: **1/1 aprovado** em 38,7 s, stage descartável, PTY e fixtures sintéticas.
- Evidência: `C:/Users/Raphael/AppData/Local/Temp/mflx-playwright-preserved-20261105-unified-search-green`.
- A nova regressão ampla passou Unified Search; permanecem 11 falhas nos cenários `24`–`29`.

## Regressão ampla após GREEN do multi-user — 2026-11-05

Execução completa em stage descartável, via PTY, com fixtures sintéticas e paths explícitos:

- **20 specs / 39 testes**;
- **24 aprovados, 13 falhas, 0 instáveis, 2 ignorados**;
- exit code `1`, duração aproximada de **15,5 min**;
- snapshot: `C:/Users/Raphael/AppData/Local/Temp/mflx-playwright-preserved-20261105-full-regression-after-multiuser`.

Falhas observadas:

- AdSense: overlay `.adsenseInterstitialOverlay` ausente;
- Scroll buttons: heading `Movies` ausente e `emby-scroller.ts` não carregou;
- My Requests: regiões acessíveis não renderizaram nos dois viewports;
- Request autocomplete: `input[name="title"]` ausente nos dois cenários;
- Nebula navigation: `#globalStatusBadge` ausente nos dois viewports;
- Backup coverage: região `ZIP local do servidor` ausente nos dois viewports;
- Accessibility My Requests: heading `Minhas solicitações` ausente;
- Pagination: `#moviesPage` ausente após reconhecimento.

O gate `21 - Multi user playback` não falhou nesta rodada ampla e continua GREEN focal **1/1**. As falhas acima permanecem independentes até triagem por contrato/fixture. Nenhuma alteração foi aplicada ao runtime instalado.

Relatórios preservados no checkout durante a execução:

- `MulletaFlix-web-master/reports/playwright/playwright-summary.json`;
- `MulletaFlix-web-master/reports/playwright/raw-results.json`.

## Diagnóstico pós-regressão — scroll buttons e pagination — 2026-11-05

### Scroll buttons

O spec `24-scrollbuttons.spec.ts` passou **2/2** com `npm run test:playwright:scrollbuttons`, usando a configuração dedicada que inicia Vite em `127.0.0.1:8097`, em **10,6 s**.

As duas falhas observadas na suíte ampla foram do ambiente de execução, não uma correção comprovada no produto:

- a primeira fixture importa `tests/playwright/fixtures/searchresults.tsx` via `/@fs/...`, endpoint servido pelo Vite, mas ausente no Kestrel do stage (`404`), deixando a tela preta e sem `Movies`;
- a segunda tenta carregar `/elements/emby-scroller/emby-scroller.ts`, enquanto o arquivo fonte real está em `src/elements/emby-scroller/emby-scroller.ts`, e o Kestrel do stage também retorna `404`.

Conclusão: os dois casos devem ser executados pelo config dedicado ou o runner de stage precisa fornecer um mecanismo explícito para fixtures de desenvolvimento. Não foi aplicado patch de produto nem enfraquecida asserção.

### Requests, Nebula, backup e acessibilidade

As falhas restantes desses grupos também foram revalidadas com os configs dedicados:

- `npm run test:playwright:requests`: **8/8 aprovados** — My Requests 2/2, request autocomplete 2/2, Nebula navigation 2/2 e backup coverage 2/2;
- `npm run test:playwright:a11y`: **3/3 aprovados** — backup coverage 2/2 e accessibility My Requests 1/1.

Assim, **11 das 13 falhas brutas** da regressão ampla foram classificadas como incompatibilidade do runner amplo com fixtures/configs Vite, não como falhas funcionais confirmadas. Pagination e AdSense agora possuem GREEN focal; a regressão ampla e os gates externos continuam pendentes.

### Pagination

`99-pagination.spec.ts` foi reexecutado isoladamente em stage descartável: **1 falha e 1 não executado**, aproximadamente **1,7 min**. A criação das bibliotecas `Filmes`/`Series` e o refresh foram solicitados, porém a navegação terminou em `Nothing here` e `#moviesPage` não apareceu.

O trace de pagination não foi preservado porque continha metadados de autenticação do stage. Permanecem o screenshot, `error-context.md`, `playwright-summary.json`, `playwright-summary.md` e `raw-results.json`; nenhum segredo foi copiado para documentação, portal ou Vault.

### GREEN focal — Pagination — 2026-11-05

O RED foi reduzido a ajustes de harness, sem alteração no servidor:

- `navigateStage` tentava navegar novamente para `/home` quando a rota já era `/home`; `Dashboard.navigate` deixava a promessa interna pendente e a próxima navegação ficava presa em `home`/`Nothing here`;
- o texto do link de log misturava o nome do arquivo com o timestamp, mas o teste comparava o texto inteiro com o título do log;
- o usuário comum não recebe os virtual folders na mesma forma que o administrador; o teste passou a reutilizar os folders já obtidos pelo admin, sem relaxar a verificação de rota/página.

Correções mínimas em `tests/playwright/support/stage.mjs`, `tests/selenium-cucumber/support/stage.ts` e `tests/playwright/specs/99-pagination.spec.ts`. TDD RED/GREEN em `tests/playwright/support/stage.test.mjs`.

Resultado: `99-pagination.spec.ts` **2/2 GREEN** em stage descartável, PTY e fixtures sintéticas, em aproximadamente **2,4 min**. O bundle/runtime instalado não foi alterado.

## Execução ampla autenticada

A execução ampla em stage descartável autenticado, via PTY, observou **39 testes: 22 aprovados, 15 falhas e 2 ignorados**. Esse resultado permanece como evidência histórica da rodada ampla; o relatório gerado pelo runner foi posteriormente sobrescrito por execuções focadas e não deve ser interpretado como a mesma rodada.

Falhas agrupadas:

- Multi-user playback: sessões de playback não apareceram na consulta autenticada.
- AdSense playback: a fixture possuía apenas um vídeo reproduzível, mas o cenário exige dois.
- Scroll buttons: heading `Movies` ausente e fixture dinâmica não carregou `emby-scroller.ts`.
- My Requests e backup: regiões acessíveis esperadas não renderizaram.
- Request autocomplete: `input[name="title"]` ausente.
- Nebula navigation: `#globalStatusBadge` ausente.
- Search/Player accessibility: clique no link de detalhes foi interceptado pelo overlay de playback.
- Pagination: `#moviesPage` não ficou visível após reconhecimento.

## Correção focada

O cenário `32-accessibility-search-and-player.spec.ts` falhava porque o overlay de playback interceptava o clique no link de detalhes. O teste passou a disparar o clique semântico no próprio elemento `<a>` e aceita os dois contratos de página de detalhes existentes (`#itemDetailPage` legado e `#itemDetailsPage` moderno), sempre exigindo uma página visível.

GREEN observado em nova execução isolada:

- `Search and Player pages meet automated WCAG 2.2 AA accessibility checks...`: **1 aprovado**.
- Stage descartável em porta não produtiva, com fixtures sintéticas.
- Produção, credenciais e banco produtivo não foram alterados.

A suíte ampla ainda precisa ser reexecutada para reconciliar a contagem global após essa correção. Nenhuma falha foi convertida em ignorado ou removida.

## Atualização de regressão — 2026-11-05

- `31-accessibility-home-and-details.spec.ts`: **GREEN 1/1** em stage descartável autenticado, após corrigir os quatro `<select>` de tracks em `src/controllers/itemDetails/index.html`, que tinham `label=""` e eram reportados pelo axe como comboboxes sem nome acessível. O spec isolado também passou a completar o wizard antes do login quando executado sozinho.
- AdSense: o RED histórico foi reproduzido antes da correção; o trace mostrou que a intro sintética falhava e o fallback iniciava o filme sem aguardar o interstitial. O cenário foi corrigido e revalidado abaixo em **GREEN 2/2**.
- Pagination: o resultado RED anterior foi corrigido no harness; a execução focada posterior passou **2/2**. O RED histórico permanece preservado acima como evidência de diagnóstico, não como estado atual.
- A suíte ampla permanece pendente; resultados focados sobrescrevem os relatórios do runner e não substituem a rodada ampla histórica de 21 aprovados, falhas múltiplas e 2 não executados.

## GREEN focal — Multi-user playback — 2026-11-05

- O contrato do adaptador local HLS foi alinhado aos eventos emitidos pela integração (`hlsManifestParsed` e `hlsError`); o listener de startup permanece registrado antes de `attachMedia`/`loadSource`.
- `htmlMediaHelper.test.ts` + `playbackmanager.intro-failure.test.ts`: **3/3 aprovados**; `npm run build:check`: **passou**.
- Bundle de produção reconstruído e copiado somente para `stage/MulletaFlix-web`; nenhum bundle foi aplicado ao runtime instalado.
- `21 - Multi user playback`: **1/1 aprovado** em stage descartável, PTY, fixtures sintéticas, duração **1,5 min**; relatório preservado em `C:/Users/Raphael/AppData/Local/Temp/mflx-playwright-preserved-20261105-hls-final2`.
- A hipótese anterior de catálogo vazio permanece uma pendência independente para pagination/diagnósticos; esta execução materializou a mídia suficiente para validar o playback.

## GREEN focal — AdSense playback — 2026-11-05

- RED reproduzido em `22-adsense-flow.spec.ts`: `.adsenseInterstitialOverlay` não aparecia após o clique de playback; o trace registrou `intro playback failed, falling back to main item` sem interstitial.
- TDD RED/GREEN em `src/utils/playback/playIntroThenMain.test.ts`: o callback assíncrono de erro agora é aguardado antes do fallback; a suíte passou **4/4**.
- `playbackmanager.ts` exibe o interstitial no fallback de intro e também nos caminhos de parada de players locais/não locais, continuando o playback se o AdSense falhar.
- `22 - AdSense playback flow`: **2/2 aprovados** em aproximadamente **2,4 min**, cobrindo script carregado e falha de script, em stage descartável com fixtures sintéticas.
- Relatório preservado em `C:/Users/Raphael/AppData/Local/Temp/mflx-playwright-preserved-20261105-adsense-final-async-fallback`.
- `npm run build:check`, Vitest focal e `npm run build:production` passaram; bundle copiado somente para `stage/MulletaFlix-web`.
- O runtime instalado `12.1.46` não foi alterado. AdSense deixou de ser bloqueio funcional web independente; a homologação global continua **PARTIAL** pelos demais gates.

## Atualização de reconhecimento de mídia — 2026-11-05

- O harness `ensureMediaLibrariesReady` passou a aguardar reconhecimento de mídia antes de prosseguir; TDD RED/GREEN em `tests/playwright/support/media-library.test.mjs`: **1/1**.
- A execução focal mostrou que `addVirtualFolder` cria as bibliotecas e a tarefa `RefreshLibrary` termina, e o servidor consegue enumerar as pastas e o arquivo `.mkv` sintético pelo endpoint de ambiente; ainda assim `MovieCount=0`, `SeriesCount=0` e `getItems` retorna zero itens em diagnósticos anteriores.
- Esse diagnóstico de reconhecimento permanece **RED/BLOCKED** para pagination e para a estabilidade geral do scanner. O gate multi-user foi posteriormente validado em execução GREEN separada, com fixture materializada no stage.

## Diagnóstico focal — Multi-user playback — 2026-11-05

O cenário `21-multi-user-playback.spec.ts` foi inicialmente **RED/BLOCKED**; o gate focal foi fechado em GREEN sem alterar o runtime instalado.

- O diagnóstico histórico mostrou o primeiro contexto enviando `POST /Sessions/Playing` com `NowPlayingItem`, enquanto o segundo ficava com `NowPlayingQueue` e `player cannot be null` no fallback de intro.
- O contrato local foi corrigido para ouvir os eventos realmente emitidos pela integração HLS (`hlsManifestParsed` e `hlsError`); o listener permanece registrado antes de `attachMedia`/`loadSource`.
- O TDD focal `htmlMediaHelper.test.ts` + `playbackmanager.intro-failure.test.ts` passou **3/3**; `npm run build:check` passou.
- Após reconstruir o bundle e copiá-lo somente para `stage/MulletaFlix-web`, `21 - Multi user playback` passou **1/1** em **1,5 min**, usando PTY, stage descartável e fixtures sintéticas. O relatório foi preservado em `C:/Users/Raphael/AppData/Local/Temp/mflx-playwright-preserved-20261105-hls-final2`.
- A fixture longa e os diagnósticos de catálogo permanecem evidência independente para pagination/reconhecimento; não são mais bloqueio desta execução GREEN de playback.

O patch experimental de cleanup em `playbackmanager.ts` permanece coberto pelo teste de falha de intro. Nenhuma alteração foi aplicada ao runtime instalado 12.1.46, e o veredito global continua **PARTIAL** pelos demais gates abertos.
