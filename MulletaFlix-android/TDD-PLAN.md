# Plano de TDD do MulletaFlix Android

## RED-GREEN — preservar rascunho de solicitação de mídia — 2026-10-10

- `INTENT`: HomeScreen currently holds the media-request title, type, year and notes in remember; the new integration test expects those draft fields to survive saved-instance-state restoration; the intended mobile behavior is to retain user input across Android activity recreation without durable storage or automatic resubmission.
- `RED`: `:feature:home:connectedDebugAndroidTest -Pandroid.testInstrumentationRunnerArguments.expectedDeviceProfile=PHONE -Pandroid.testInstrumentationRunnerArguments.class=org.mulletaflix.feature.home.HomeMediaRequestDraftRestorationTest` — falhou pela razão esperada: após restauração, o campo de título não continha `Duna`.
- `GREEN`: mesmo teste passou 1/1 em PHONE/API 35 e 1/1 em TABLET/API 35, via `tools/with-emulator.ps1`; título, tipo, ano e observações são restaurados após recriação do estado salvo. `rememberSaveable` mantém os quatro campos; diálogo, progresso e resultado de envio não são restaurados.
- `QUALITY GATE`: `testDebugUnitTest :feature:home:lintDebug :app:lintDebug :app:assembleDebug :feature:home:compileDebugAndroidTestKotlin :app:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL` (881 tarefas acionáveis; 32 executadas, 849 atualizadas). Lint, build e compilação dos testes instrumentados aprovados.
- `GRAPHIFY`: `graphify update .` — 10.980 nós, 28.675 arestas, 489 comunidades; gerados `graphify-out/graph.json`, `graph.html` e `GRAPH_REPORT.md`. Consulta `graphify query "HomeMediaRequestDraftRestorationTest HomeScreen rememberSaveable" --budget 700` localizou o teste novo (linha 31), `HomeScreen()` e `rememberSaveable`. Avisos: CLI 0.9.79 usa skill 0.9.84; parser preexistente reporta sintaxe em `TvHomeRefreshIntegrationTest.kt:53`; 83 arquivos não classificados e 28 sem símbolos. A atualização extrai código; não faz extração semântica dos Markdown.
- `LIMITAÇÕES`: o teste usa `StateRestorationTester` para o mecanismo Compose de estado salvo; não comprova restauração após encerramento forçado do processo, persistência durável, envio ao servidor ou fluxo em hardware/TV. Sem versão/release/portal/servidor.

## RED-GREEN — registros e trailers MOBI/PalmDOC — 2026-10-10

- `INTENT`: aceitar blocos de texto MOBI/PalmDOC divididos em vários registros, removendo trailers indicados por `extra_data_flags` antes da descompressão e preservando caracteres UTF-8 que cruzam a fronteira dos registros.
- `RED`: `:feature:item-detail:testDebugUnitTest --tests "org.mulletaflix.feature.itemdetail.MobiBookTextExtractorTest"` — teste novo falhou como esperado com `O conteúdo MOBI não corresponde ao tamanho declarado.` porque bytes de trailer eram tratados como conteúdo.
- `GREEN`: `MobiBookTextExtractorTest` passou 5/5; fixture com dois registros verifica PalmDOC, sobreposição UTF-8, VWI reverso de dois bytes (trailer de 130 bytes), remoção de marcadores sem sobreposição; entrada com trailer truncado é rejeitada.
- `QUALITY GATE`: `:feature:item-detail:testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL`; 197 testes JVM, 0 falhas/erros/ignorados.
- `DEVICE`: `BookReaderScreenIntegrationTest.mobiResponseFromServerIsDecodedAndRenderedInTheReader` passou 1/1 em PHONE/API 35 e 1/1 em TABLET/API 35. Fixture HTTP/Compose usa dois registros, quebra UTF-8 e trailers com VWI de 130 bytes; ambos os wrappers confirmaram GPU NVIDIA e encerraram os emuladores.
- `GRAPHIFY`: consulta localizou `MobiBookTextExtractor`, `removeTrailingEntries`, `decodeBackwardVariableInteger` e `MobiBookTextExtractorTest`; grafo Android atualizado (10.961 nós, 28.634 arestas, 505 comunidades) e grafo agregado atualizado (63.638 nós, 162.531 arestas, 1.604 comunidades). Arquivos Markdown são registrados neste plano, mas não recebem extração semântica pelo `graphify update`.
- `LIMITAÇÕES`: validado com fixture sintética compatível com o formato; ainda falta executar um MOBI real confirmado pelo servidor/usuário. Sem release/portal/servidor.

## RED-GREEN — leitura limitada de MOBI/PalmDOC — 2026-10-10

- `INTENT`: o leitor rejeitava `application/x-mobipocket-ebook` e um MOBI com MIME genérico caía no caminho EPUB; os testes exigem abrir MOBI/PalmDOC não criptografado com compressão 1/2 como texto paginado, enquanto o plano anterior reservava MOBI/AZW não suportado à conversão do servidor.
- `RED`: `:feature:item-detail:testDebugUnitTest --tests "org.mulletaflix.feature.itemdetail.BookReaderPayloadPolicyTest"` — os dois novos casos falharam como esperado: MOBI continuava listado como incompatível e recebia limite genérico de 512 MiB.
- `GREEN`: `MobiBookTextExtractorTest` passou 4/4 e `BookReaderPayloadPolicyTest` passou 29/29. Cobertura inclui HTML UTF-8, PalmDOC comprimido Windows-1252, back-reference sobreposta, MIME/assinatura genérica, limite de transferência, DRM, HUFF/CDIC, índice e tamanho inválidos.
- `DEVICE`: `BookReaderScreenIntegrationTest.mobiResponseFromServerIsDecodedAndRenderedInTheReader` passou 1/1 em PHONE/API 35 e 1/1 em TABLET/API 35. Retrofit + `MockWebServer` + Compose confirmaram texto e rota; `tools/with-emulator.ps1` confirmou QEMU/NVIDIA RTX 3050 e encerrou os AVDs.
- `QUALITY GATE`: `:feature:item-detail:testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL`; suíte do módulo: 196 testes, 0 falhas/erros/ignorados; lint e build Debug aprovados.
- `DECISÃO`: código agora permite apenas MOBI/PalmDOC não criptografado, sem compressão ou com compressão PalmDOC; formato bruto é lido por offsets. Arquivo máximo 64 MiB e texto descompactado 8 MiB.
- `TWINS`: busca pelas strings `application/x-mobipocket-ebook` e `application/vnd.amazon.mobi8-ebook` no código do APK identificou o MIME compatível no parser/política e o MIME AZW3 na lista de rejeição; sem outro bloqueio MOBI equivalente.
- `LIMITAÇÕES`: não inclui AZW3/KF8, HUFF/CDIC, DRM, MIME `application/vnd.amazon.mobi8-ebook` nem fixture de catálogo real. Sem mudança de versão, servidor, APK de produção, release ou portal.

## RED-GREEN — atualização da Home ao retomar na TV com carga ativa — 2026-10-10

- `INTENT`: a retomada da Home da TV chama `refreshIfIdle`, que descarta a atualização enquanto uma carga está ativa; a especificação de `HomeRefreshPolicy`/`HomeScreen` exige atualização imediata ao voltar a `Resumed`, sem duplicar requisições periódicas.
- `RED`: `:feature:home:testDebugUnitTest --tests "org.mulletaflix.feature.home.HomeViewModelTest.refresh after TV resume runs once the active initial load completes"` — falhou como esperado: a carga de retomada não foi iniciada após a carga inicial concluir (`resumeCalls` permaneceu 1 em vez de 2). Uma execução inicial do teste antes da implementação falhou apenas por referência ainda inexistente e não conta como evidência RED.
- `GREEN`: o teste focal passou 1/1 após `refreshAfterActiveLoadOnResume()` aguardar a carga atual e atualizar uma única vez. `:feature:home:connectedDebugAndroidTest` na TV/API 34 passou 36/36, 0 falhas/erros/ignorados; novo teste instrumentado confirma handlers independentes para retomada e tick periódico.
- `QUALITY GATE`: `testDebugUnitTest lintDebug :app:assembleDebug :feature:home:compileDebugAndroidTestKotlin --no-daemon --console=plain` concluiu `BUILD SUCCESSFUL`; 241 XML, 1.581 testes JVM, 0 falhas/erros/ignorados. Lint e build Debug passaram. Após o ajuste final do teste instrumentado, `TvRefreshEffectTest` foi executado novamente e o wrapper terminou com `BUILD SUCCESSFUL`.
- `DEVICE`: `MulletaflixTvApi34`, GPU NVIDIA RTX 3050 verificada pelo wrapper; 36 testes passaram e o AVD foi encerrado após a execução. Escopo funcional de refresh automático é TV; telefone/tablet mantêm refresh manual.
- `TWINS`: busca pelo acoplamento entre callback imediato de retomada e `refreshIfIdle` encontrou três análogos em `feature/library/.../LibraryScreen.kt`, `feature/library/.../FavoritesScreen.kt` e `feature/live-tv/.../LiveTvScreen.kt`; suas políticas/jobs independentes não foram alterados neste incremento e seguem para auditoria própria.
- `GRAPHIFY`: investigação qualificou `feature/home/.../TvRefreshEffect` após encontrar homônimo na Biblioteca. `graphify update MulletaFlix-android` após a mudança: 10.786 nós, 28.135 arestas, 491 comunidades; HTML/relatório regenerados. `explain` ligou `HomeViewModel` a testes e TODO Android. CLI informa que Markdown não é reextraído semanticamente nesta execução. Parser AST ainda aponta falha preexistente em `TvHomeRefreshIntegrationTest.kt:53` e 28 arquivos sem símbolos.
- `LIMITAÇÕES`: nenhuma mudança de servidor, portal, versão, APK de produção ou release. Validação em hardware/servidor de produção não realizada.

## RED-GREEN — opções de timer de suspensão até 180 min — 2026-10-10

- `INTENT`: `SleepTimerMenu` oferece apenas 15–90 minutos, embora `normalizeSleepTimerMinutes` aceite até 180; menu e política devem oferecer as mesmas durações, incluindo 120 e 180, preservando seleção por toque, rádio acessível, rolagem e D-pad.
- `RED`: `:feature:player:connectedDebugAndroidTest` no AVD TV/API 34 executou `SleepTimerMenuTest` 5 testes; o teste novo falhou pela razão esperada: não havia nó clicável “120 minutos”; os 4 casos anteriores passaram. A tentativa inicial PHONE incluiu o caso TV-only e não é usada como evidência RED.
- `GREEN`: `SleepTimerPolicyTest` passou 8/8; `SleepTimerMenuTest` passou 5/5 em TV/API 34, incluindo foco D-pad; a seleção de 120/180 passou isoladamente em PHONE/API 35 e TABLET/API 35.
- `QUALITY GATE`: `:feature:player:testDebugUnitTest --tests 'org.mulletaflix.feature.player.SleepTimerPolicyTest'` passou; comando global `testDebugUnitTest lintDebug :app:assembleDebug :feature:player:compileDebugAndroidTestKotlin --no-daemon --console=plain` terminou `BUILD SUCCESSFUL`. Relatórios agregados: 241 XML, 1.580 testes JVM, 0 falhas/erros/ignorados.
- `DEVICE`: wrapper `tools/with-emulator.ps1` confirmou NVIDIA RTX 3050 e encerrou os AVDs PHONE, TABLET e TV após uso.
- `GRAPHIFY`: `graphify update MulletaFlix-android` extraiu 758 arquivos de código; `cluster-only` concluiu com 10.775 nós e 479 comunidades. A consulta não foi limitada corretamente na primeira tentativa e não serve como evidência de ligação específica. A extração AST reportou erro de parser preexistente em `TvHomeRefreshIntegrationTest.kt:53` e 28 arquivos sem símbolos. A atualização CLI cobre código, não reextrai semanticamente os Markdown alterados; essa limitação permanece explícita. O CLI Graphify 0.9.84 não lista um subcomando literal `refine`; foi usado `cluster-only` para reagrupamento.
- `LIMITAÇÕES`: sem alteração de versão, release, portal ou servidor; nenhuma duração customizada foi adicionada, somente opções já aceitas pela política.

## RED-GREEN — tentativa única e retry limpo do logout — 2026-10-10

- `INTENT`: ao repetir logout após erro, remover imediatamente a mensagem obsoleta e indicar progresso; enquanto a limpeza estiver em curso, aceitar apenas um pedido e executar no máximo um callback de sucesso. O app só sai após `LogoutUseCase` confirmar sucesso; erros de sessão são mostrados por `ProfileScreen`.
- `RED`: `:feature:user:testDebugUnitTest --tests 'org.mulletaflix.feature.user.UserProfileViewModelTest.retrying logout clears stale error before request starts' --tests 'org.mulletaflix.feature.user.UserProfileViewModelTest.duplicate logout requests while first request is pending are ignored' --no-daemon --console=plain` — ambos falharam como esperado: a mensagem anterior persistiu no retry e duas chamadas chegaram ao repositório.
- `GREEN`: mesmo comando focal — 2/2 aprovados. `logout` agora publica `isLoggingOut` e limpa `error` antes de iniciar a coroutine; chamadas repetidas durante o pedido são ignoradas.
- `QUALITY GATE`: `testDebugUnitTest :feature:user:lintDebug :app:lintDebug :app:assembleDebug :app:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL`; 241 XML, 1.580 testes JVM, 0 falhas/erros/ignorados.
- `DEVICE`: `ProfileLogoutFailureTest` PHONE/API 35 — 1/1 aprovado, 0 falhas/erros/ignorados; wrapper confirmou GPU NVIDIA e encerrou o emulador.
- `LIMITAÇÕES`: teste do retry usa `CompletableDeferred` e fake do repositório; falha física de DataStore/disco permanece fora do escopo. Sem versão ou release.

## Evidência de feedback de logout na interface — 2026-10-10

- Teste instrumentado `ProfileLogoutFailureTest.failedLogoutRendersErrorAndDoesNotInvokeNavigation` cobre a tela `ProfileScreen` e `UserProfileViewModel` reais com `AuthRepository` de teste que falha no logout.
- Resultado: mensagem de erro observável na tela, `isLoggingOut` retorna a `false` e callback que representa navegação permanece em zero chamadas.
- `:feature:user:connectedDebugAndroidTest` — 1 teste aprovado, 0 falhas/erros/ignorados, PHONE/API 35, NVIDIA confirmada pelo wrapper.
- Este incremento adiciona verificação instrumentada, sem alterar código de produção; não reivindica novo ciclo RED-GREEN de implementação. A primeira execução falhou por seletor de diálogo inadequado no teste; a versão final aciona o ViewModel e verifica a renderização.
- Limitação: falha é injetada no limite do repositório; DataStore/disco real não são forçados a falhar.

## Escopo e situação

Este plano cobre todo o aplicativo Android: `:app`, `:core:api`, `:core:common`, `:core:testing`, `:data`, `:design-system`, `:domain` e todos os `:feature:*` registrados em `settings.gradle.kts`.

O projeto já tem suítes JVM e instrumentadas em todos os módulos de produto. A existência de testes ou a aprovação da suíte não prova que testes legados foram escritos antes da implementação. Não há evidência histórica RED-GREEN consolidada para o legado. A partir da adoção deste plano, cada mudança executável deve registrar e demonstrar o ciclo TDD; os comportamentos legados serão protegidos quando forem alterados e as lacunas abaixo serão fechadas por prioridade.

Não reportar percentual de cobertura sem relatório gerado por ferramenta e escopo explícito. Não converter quantidade de testes em cobertura ou homologação.

## Ciclo obrigatório por mudança

Antes de editar código de produção:

1. Declare o comportamento observável e a regressão que o teste detectará (`INTENT`).
2. Escreva um teste pequeno na camada proprietária do comportamento, com fixture determinística e expectativa independente da implementação.
3. Execute o teste isolado. Registre o comando e confirme falha pela razão esperada. Erro de compilação, fixture ou ambiente não é `RED` válido.
4. Implemente a menor mudança que torne o teste verde.
5. Execute o mesmo teste e a suíte do módulo; refatore somente após o verde.
6. Execute a suíte JVM do projeto, lint dos módulos afetados, compilação Debug e instrumentação nos perfis afetados.
7. Registre limitações e confirme que o relatório contém testes executados, sem falhas, erros ou execução totalmente ignorada.

Formato obrigatório do registro na descrição da mudança ou handoff:

```text
INTENT: <regressão observável e requisito relacionado>
RED: <comando> — falha esperada: <assertion/resultado>
GREEN: <comando> — <testes aprovados/falhas/erros/ignorados>
QUALITY GATE: <comandos e resultados>
DEVICE: <PHONE/TABLET/TV ou não aplicável, com evidência>
LIMITAÇÕES: <lacunas reais ou nenhuma>
```

Teste que passa antes da implementação, falha por setup ou verifica apenas chamada de mock não comprova RED. Use fakes para dependências externas lentas; mantenha a lógica e os efeitos sob teste reais.

## Inventário das suítes existentes

Auditoria estática em 2026-10-08 confirmou testes JVM e/ou instrumentados nos módulos de produto: `:app`, `:core:common`, `:core:api`, `:domain`, `:data`, `:design-system` e todos os `:feature:*`. `:core:testing` contém apenas fixtures constantes e não precisa de suíte própria enquanto não adicionar lógica executável. `tools/` tem testes PowerShell para os validadores de perfil e resultados Android.

`TESTING.md` é o catálogo operacional de cenários e emuladores. Use-o junto com esta matriz de propriedade; o texto do catálogo não prova execução recente, cobertura percentual nem histórico RED-GREEN. A auditoria não encontrou prova histórica RED para o legado. Proteja cada comportamento com TDD quando ele for alterado e feche as lacunas P1/P2 abaixo em tarefas separadas.

Escopo funcional Android a manter coberto: autenticação/Quick Connect e LAN; sessão, navegação e deep links; Home e bibliotecas; busca e ordenação; detalhes e leitura de livros; reprodução, áudio, legendas, PiP e Cast; downloads e cache; TV ao vivo; SyncPlay; perfil e preferências; rede/offline; atualização, instalação, empacotamento e UX PHONE/TABLET/TV. Use JVM para regras/estados, HTTP local para contratos, instrumentação para integração Android e servidor autorizado apenas para E2E real.

### Registro TDD desta execução

- `INTENT`: a tela Perfil só pode navegar após `LogoutUseCase` confirmar que a sessão foi limpa; se retornar `Result.failure`, preservar a tela, encerrar o estado de carregamento e expor erro em `UserProfileUiState`.
- `RED`: `:feature:user:testDebugUnitTest --tests 'org.mulletaflix.feature.user.UserProfileViewModelTest.failed logout keeps the current destination and exposes the failure' --no-daemon --console=plain` — falhou na asserção de callback: a navegação era executada apesar do `Result.failure`.
- `GREEN`: `:feature:user:testDebugUnitTest --tests 'org.mulletaflix.feature.user.UserProfileViewModelTest' --no-daemon --console=plain` — 8/8 passaram; falha mantém a tela, remove progresso e publica erro; sucesso preserva o callback.
- `QUALITY GATE`: `testDebugUnitTest :feature:user:lintDebug :app:lintDebug :app:assembleDebug :app:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL`; 241 relatórios XML, 1.578 testes JVM, 0 falhas/erros/ignorados; `git diff --check` passou.
- `DEVICE`: não aplicável ao layout; o ProfileScreen já renderiza `uiState.error`, sem alteração visual. Nenhum emulador foi iniciado.
- `GRAPHIFY`: grafo de código atualizado e clusterizado — 9.544 nós e 23.779 arestas; query confirmou ligação de `UserProfileViewModel` com `ProfileScreen` e testes. O modo `--code-only` ignorou 11 Markdown; extração semântica completa requer LLM não configurado. Pacote local 0.9.58 é anterior à skill 0.9.79.
- `LIMITAÇÕES`: não exercita falha de persistência via DataStore real nem inspeção Compose do card de erro; o teste de ViewModel verifica estado e callback. Sem release.

- `INTENT`: após autenticar outra conta no mesmo servidor, token, usuário e nome novos devem substituir a identidade anterior e permanecer persistidos ao recriar `SessionRepositoryImpl`; URL selecionada e DeviceId devem permanecer estáveis.
- `RED`: não havia teste de integração da substituição persistida, sem falha comportamental prévia demonstrada. A primeira execução do teste novo falhou porque o fixture MockWebServer usava HTTP em claro, bloqueado corretamente pela política Android; isso é falha de setup, não RED válido.
- `GREEN`: `:data:connectedDebugAndroidTest` filtrado para `SessionRepositoryPersistenceTest`, PHONE/API 35 — `BUILD SUCCESSFUL`; o teste usa HTTPS local com certificado isolado e `AuthRepositoryImpl`/`SessionRepositoryImpl` reais. Conferir XML instrumentado no quality gate final.
- `QUALITY GATE`: `:data:connectedDebugAndroidTest` foi repetido após a revisão e passou 4/4, 0 falhas/erros/ignorados. `testDebugUnitTest :data:lintDebug :app:lintDebug :app:assembleDebug :data:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL`; 240 XML, 1.569 testes JVM, 0 falhas/erros/ignorados.
- `DEVICE`: AVD PHONE/API 35 via `tools/with-emulator.ps1`; o wrapper confirmou GPU NVIDIA e encerrou o emulador.
- `LIMITAÇÕES`: cobre o repositório e persistência DataStore, não a navegação de UI após login nem troca de servidor. Graphify atualizou `graphify-out` (10.670 nós, 27.813 arestas); mantém aviso preexistente de sintaxe em `feature/home/src/androidTest/java/org/mulletaflix/feature/home/TvHomeRefreshIntegrationTest.kt:53`. Nenhuma mudança de produção, build de release ou publicação.

- `INTENT`: um link de mídia válido, aberto com sessão autenticada no servidor indicado, deve despachar o pedido ao endpoint exato do usuário/item, não apenas ser aceito pelo parser.
- `RED`: não havia evidência instrumentada da entrega no `NavHost` real; lacuna de integração, sem RED comportamental válido antes do novo caso.
- `GREEN`: `MainActivitySessionNavigationTest` passou 4/4 em PHONE/API 35. Com sessão completa, a Activity recebe `mulletaflix://details` e o `MockWebServer` confirma `GET /Users/{userId}/Items/{itemId}` exatamente. A resposta é 200 JSON local; a renderização visual do título não foi confirmada por este teste.
- `QUALITY GATE`: `testDebugUnitTest :app:lintDebug :app:assembleDebug :app:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL`; 240 relatórios XML, 1.567 testes JVM, 0 falhas/erros/ignorados.
- `DEVICE`: AVD PHONE/API 35, confirmado na NVIDIA e encerrado pelo wrapper.
- `LIMITAÇÕES`: cobre dispatch do link de inicialização para o mesmo `serverId`; não cobre o conteúdo visual completo, link recebido após login ou divergência de servidor em Activity real. A checagem de atualização da Activity pode acessar GitHub.

- `INTENT`: credenciais persistidas sem URL válida não podem marcar o AuthViewModel como autenticado, pois a seleção de servidor avançava para login e o Login redirecionava uma sessão parcial à Home.
- `RED`: `:feature:auth:testDebugUnitTest --tests 'org.mulletaflix.feature.auth.AuthViewModelTest.orphaned credentials without server url are not an authenticated session' --no-daemon --console=plain` — falha de assertion reproduzível: o estado era `isAuthenticated=true` apesar da URL vazia. A instrumentação também reproduziu timeout ao não encontrar a tela de autenticação.
- `GREEN`: o teste focal passou após derivar autenticação da URL, token e ID de usuário; teste de transição valida sessão completa e remoção separada de URL, token e ID. `MainActivitySessionNavigationTest` no PHONE/API 35 passou 3/3, confirmando ausência de sessão, sessão parcial com token+usuário mas URL vazia permanecendo em seleção, e sessão completa abrindo Home. O setup preserva a sessão original e não executa sobre credenciais parciais que o contrato público não permite restaurar com exatidão.
- `QUALITY GATE`: `testDebugUnitTest :app:lintDebug :app:assembleDebug :app:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL`; 240 relatórios XML, 1.567 testes JVM, 0 falhas/erros/ignorados.
- `DEVICE`: teste instrumentado no AVD PHONE/API 35 com GPU NVIDIA; wrapper encerrou o emulador.
- `LIMITAÇÕES`: fluxo de autenticação completo contra serviço remoto e troca/logout permanecem fora deste teste; sem release.

- `INTENT`: a Activity real deve iniciar no fluxo de autenticação sem sessão válida e na Home quando existe sessão completa persistida; a política unitária isolada não comprova a decisão inicial integrada.
- `RED`: não foi observado RED comportamental válido porque a lacuna era ausência de teste de integração, não um defeito conhecido; a primeira tentativa falhou na compilação do teste por uso de API Compose interna, corrigida antes da execução funcional.
- `GREEN`: `:app:connectedDebugAndroidTest` filtrado para `org.mulletaflix.android.MainActivitySessionNavigationTest`, PHONE/API 35 — 2 testes aprovados, 0 falhas/erros/ignorados. Usa `SessionRepositoryImpl` real e `MockWebServer` local.
- `QUALITY GATE`: `testDebugUnitTest :app:lintDebug :app:assembleDebug :app:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL`; 240 relatórios XML, 1.565 testes JVM, 0 falhas/erros/ignorados.
- `DEVICE`: PHONE/API 35 via AVD NVIDIA, encerrado pelo wrapper.
- `LIMITAÇÕES`: não cobre sessão incompleta, logout, troca de servidor, deep link autenticado ou TABLET/TV. Sem mudança de produção e sem release.

- `INTENT`: um servidor de mídia na LAN pode continuar acessível quando o Wi‑Fi/Ethernet não tem saída para a internet; o monitor e a redescoberta automática após mudança de rede solicitavam somente redes com `NET_CAPABILITY_INTERNET`.
- `RED`: `:core:common:testDebugUnitTest --tests 'org.mulletaflix.core.common.network.ConnectivityNetworkPolicyTest'` — 3 testes, uma falha comportamental esperada ao tentar considerar Wi‑Fi local sem capability Internet como acesso possível.
- `GREEN`: classe focada aprovada após a política distinguir rota Internet de transporte Wi‑Fi/Ethernet; `ConnectivityNetworkRequestTest` passou 1/1 instrumentado no PHONE/API 35, confirmando que a solicitação de callbacks não exige capability Internet.
- `QUALITY GATE`: `.\gradlew.bat testDebugUnitTest :app:lintDebug :app:assembleDebug :app:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL`; 240 relatórios XML, 1.565 testes JVM, 0 falhas/erros/ignorados.
- `DEVICE`: PHONE/API 35; o teste examina a configuração Android do `NetworkRequest`, não depende do hardware de rede.
- `LIMITAÇÕES`: não foi criado Wi‑Fi isolado real no emulador; descoberta/alcance do servidor ainda depende da rede e da conexão ao endpoint. O callback usa transports Wi‑Fi/Ethernet como potencial acesso local, não como confirmação de que o host específico está acessível.

- `INTENT`: a confirmação de limpeza dizia que apagaria o histórico de “deste usuário”, embora o ViewModel limpe somente o escopo ativo servidor+conta; o botão de remoção de cada termo também tinha descrição TalkBack genérica.
- `RED`: `:feature:search:connectedDebugAndroidTest` filtrado para `SearchHistoryDialogTest`, PHONE/API 35 — 4 testes, 2 falhas esperadas por texto de escopo e descrição sem o termo; 0 erros e 0 ignorados.
- `GREEN`: o mesmo filtro após a alteração — PHONE/API 35 4/4 e TV/API 34 4/4, sem falhas/erros/ignorados; wrapper confirmou NVIDIA para o QEMU e encerrou cada AVD.
- `QUALITY GATE`: `.\gradlew.bat testDebugUnitTest :app:lintDebug :app:assembleDebug :app:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL`; 240 relatórios XML, 1.564 testes JVM, 0 falhas/erros/ignorados.
- `DEVICE`: PHONE e TV; conteúdo semântico comum, sem alteração de layout/perfil. TABLET não executado nesta rodada.
- `LIMITAÇÕES`: validação Compose de semântica e diálogo; não substitui auditoria TalkBack manual em dispositivo físico nem fecha toda a lacuna de acessibilidade integrada P2. Revisão adversarial delegada não ocorreu: limite atual de agentes foi atingido. Nenhum artefato/release foi produzido.

- `INTENT`: cancelar download enquanto a leitura HTTP está bloqueada deve cancelar a chamada OkHttp e apagar o APK parcial.
- `RED`: `.\gradlew.bat :core:common:testDebugUnitTest --tests 'org.mulletaflix.core.common.update.AppUpdateDownloaderTest' --no-daemon --console=plain` — sem o hook de cancelamento, o teste expirou no `cancelAndJoin` enquanto a leitura travada não recebia `Call.cancel()`.
- `GREEN`: mesmo teste passou após vincular cancelamento do `Job` a `Call.cancel()`; teste usa condição de monitor para confirmar que a thread já está bloqueada antes do cancelamento.
- `INTENT`: a fronteira do downloader não deve aceitar como completo um `ResponseBody` que declara mais bytes do que realmente entrega.
- `RED`: teste com `Call.Factory` controlada, `Content-Length` 4 e corpo de 3 bytes falhou antes da checagem de integridade.
- `GREEN`: a comparação do tamanho copiado com o comprimento declarado produz `Error` e o arquivo parcial não é preservado. Esta é uma defesa do contrato da fronteira injetável; não indica bug no transporte HTTP real, que já detecta EOF prematuro.
- `QUALITY GATE`: `testDebugUnitTest`, lint de `core:common`, `core:api` e `app`, e `app:assembleDebug` passaram; 1.485 testes nos XML atuais, 0 falhas/erros/ignorados.
- `DEVICE`: JVM, não aplicável. `TvHomeRefreshIntegrationTest` foi validado separadamente no AVD TV.
- `LIMITAÇÕES`: histórico RED-GREEN legado não auditado; exclusão em falha/cancelamento é best-effort e não reporta falha do filesystem.

- `INTENT`: o prazo monotônico do Quick Connect deve incluir a latência da inicialização, sem impedir a confirmação de sessão quando a resposta inicial já informa autorização.
- `RED`: teste `quick connect deadline includes initiation request latency` falhou antes da correção ao esperar 210 s restantes após 90 s de latência e receber 300 s; o novo teste `already authorized quick connect confirms session when initiation finishes after deadline` falhou porque o fluxo não chamou a confirmação de sessão após a resposta autorizada tardia.
- `GREEN`: `:feature:auth:testDebugUnitTest` passou com os dois casos; o fluxo captura o prazo antes da chamada inicial e permite somente a primeira consulta imediata se ela reportar autorização, ainda com timeout de rede limitado. Não estende prazo nem permite novas consultas depois do limite.
- `QUALITY GATE`: `testDebugUnitTest :feature:auth:lintDebug :core:common:lintDebug :core:api:lintDebug :app:lintDebug :app:assembleDebug` — BUILD SUCCESSFUL; relatórios XML: 236 arquivos, 1.521 testes, 0 falhas, 0 erros, 0 ignorados.
- `DEVICE`: `LoginFormSemanticsTest` passou nos AVDs `MulletaflixApi35` (PHONE/API 35), `MulletaflixTabletApi35` (TABLET/API 35) e `MulletaflixTvApi34` (TV/API 34); os três executaram via `tools\\with-emulator.ps1` com verificação NVIDIA e encerramento automático.
- `LIMITAÇÕES`: a instrumentação valida semântica/tela de login por perfil, não Quick Connect ponta-a-ponta contra servidor real. O teste de ViewModel cobre prazo e conclusão usando repositório fake. Cobertura percentual não calculada.

- `INTENT`: depois que o usuário nega a permissão de instalação, tentar de novo deve reinstalar o mesmo APK baixado, sem repetir download; se o arquivo tiver sido removido ou alterado, rejeitar o cache, baixar novamente e nunca instalar bytes diferentes dos validados.
- `RED`: os testes de retry em `AppUpdateViewModelTest` e `SettingsViewModelTest` falharam antes da implementação porque a segunda tentativa chamava `downloadApk` novamente. Depois, `PendingAppUpdateApkTest.does not return a cached APK whose bytes changed after download` falhou porque a entrada existente era aceita sem revalidar o SHA-256.
- `GREEN`: os três testes focados passaram; ambos ViewModels reaproveitam a entrada somente para a mesma versão/URL/hash, recalculam SHA-256 em IO antes de reutilizar, e invalidam arquivo ausente ou alterado. A tela mantém o controle ocupado e mostra “Verificando APK...” enquanto calcula o digest.
- `QUALITY GATE`: `testDebugUnitTest :core:common:lintDebug :feature:settings:lintDebug :app:lintDebug :app:assembleDebug` — BUILD SUCCESSFUL; relatórios XML: 237 arquivos, 1.526 testes, 0 falhas, 0 erros, 0 ignorados. Execução final focada `:core:common:testDebugUnitTest :feature:settings:testDebugUnitTest :app:testDebugUnitTest :feature:settings:lintDebug :app:lintDebug :app:assembleDebug` também passou após as últimas mudanças.
- `DEVICE`: não executado; a correção não muda layout responsivo, lifecycle nem APIs dependentes do dispositivo. Teste de ViewModel cobre os dois pontos de entrada; a apresentação “Verificando APK...” não tem teste instrumentado dedicado.
- `LIMITAÇÕES`: não foi gerada release. Sem checksum publicado, o digest confirma que o arquivo não mudou desde o download desta sessão, não a autenticidade do servidor. Integridade depende do HTTPS e/ou do SHA-256 esperado quando fornecido.

## Seleção de testes

| Camada/comportamento | Primeiro teste | Integração complementar |
| --- | --- | --- |
| Política, parser, mapper, regra de domínio | JVM no módulo proprietário | Casos limite e entradas inválidas |
| Coroutines, Flow, ViewModel | JVM com scheduler/dispatcher determinístico | Persistência Android instrumentada quando relevante |
| Retrofit, autenticação, headers, retries | JVM + MockWebServer | Servidor de teste autorizado; nunca credencial real |
| DataStore, Room, WorkManager, FileProvider, intents | JVM para decisões puras | Instrumentação Android para integração de plataforma |
| Compose, navegação, rolagem, acessibilidade, D-pad | Teste de estado/política JVM quando separável | Compose instrumentado no PHONE, TABLET ou TV afetado |
| Player, PiP, lifecycle, TTS, codecs, downloads offline | JVM para políticas e estados | Instrumentação; aparelho físico quando hardware/saída for parte do requisito |
| Empacotamento/assinatura | Pester ou teste de script com arquivos temporários controlados | Inspecionar metadados, versão, assinatura e hash do artefato |

## Lacunas auditadas e ordem de fechamento

Prioridades abaixo resultam de auditoria estática dos módulos e são tarefas de teste, não afirmações de que a funcionalidade esteja quebrada. Criar cada teste primeiro e confirmar `RED` antes de qualquer correção.

### P1 — segurança, sessão e fluxos críticos

1. **Atualização do APK:** `core/common/.../AppUpdateDownloader.kt` — testes unitários agora cobrem sucesso com progresso monotônico, checksum válido/inválido, integridade estrutural do APK/manifesto, corpo vazio, HTTP 503, URL não confiável, comprimento truncado, cancelamento durante leitura bloqueada e remoção do parcial. A classe `AppUpdateDownloaderTest` e a classe `CleartextRequestProtectionTest` passaram no gate focal executado nesta retomada. Esses testes existentes comprovam regressões cobertas, mas este ciclo não observou RED comportamental para cada caso; não os classificar retroativamente como TDD. A remoção em falha/cancelamento continua best-effort; falha de exclusão no sistema de arquivos não é reportada ao usuário. RED-GREEN também comprovado para rejeitar user-info, portas iniciais diferentes de 443, caminhos com segmentos `.`/`..` literais ou percent-encoded, redirects HTTPS para hosts externos e redirects cross-origin em portas diferentes de 443. Redirect para `release-assets.githubusercontent.com` e redirects same-origin preservados. `AppUpdateHttpsRedirectIntegrationTest` passou 7/7 no AVD PHONE API 35 usando NVIDIA. Uma defesa de contrato adicional rejeita `ResponseBody` cujo tamanho efetivamente copiado diverge do `Content-Length`; seu teste usa `Call.Factory` controlada e não afirma defeito no transporte HTTP real (o OkHttp já trata EOF HTTP prematuro). RED-GREEN também confirmado no instalador: falha de `canRequestPackageInstalls()` por `SecurityException` não derruba Android 15; a integração verifica o fluxo de configurações sem iniciá-las, URI FileProvider/cache legível e captura da intent de instalação sem abrir app externo. Para builds distribuídos pelo Google Play, a permissão restrita `REQUEST_INSTALL_PACKAGES` não é adicionada para autoatualização; a instalação fora da loja continua sujeita à política da loja e ao consentimento/permissão do sistema. O fluxo de origem Google Play está coberto no registro TDD mais recente.
2. **Sessão persistida:** `data/.../SessionRepositoryImpl.kt` — instrumentação em `MulletaflixApi35` agora cobre salvar/reabrir/limpar sessão, manter URL e DeviceId no logout, CRUD/idempotência de servidores salvos e proteção da entrada oficial. RED-GREEN também cobre a versão do servidor oficial sem verificação. Troca de servidor/conta e isolamento formal de credenciais permanecem pendentes.
3. **SyncPlay realtime:** `core/api/.../SyncPlayRealtimeClient.kt` — start/stop, troca de grupo, eventos, falha, cancelamento e reconexão obsoleta com WebSocket controlado.
4. **Navegação de produção:** `app/.../MulletaFlixNavHost.kt` — `MainActivitySessionNavigationTest` cobre em Activity real sessão ausente, sessão parcial com credenciais órfãs/URL vazia, sessão completa e deep links no mesmo servidor e entre servidores (PHONE/API 35, 5/5). `AuthViewModel` só considera autenticado quando há URL, token e ID de usuário; logout e troca completa de servidor/conta iniciada pela tela de perfil seguem pendentes. `SessionRepositoryPersistenceTest` confirma que login com outra conta no mesmo servidor substitui e persiste a identidade, conservando URL e DeviceId. `MainActivityDeepLinkRestoreTest` cobre deep link frio pendente/consumido e restauração da sequência após recriação.
   - `INTENT`: ao iniciar com sessão salva, URL, token, usuário e `serverId` precisam vir do mesmo snapshot persistido antes de montar a navegação; caso contrário, um deep link de outro servidor pode ser processado quando o ID atual ainda está `null`.
   - `CHANGE`: `SessionRepository.getSessionState()` oferece o estado coeso; `SessionRepositoryImpl` o mapeia de uma única emissão do DataStore; `MainActivity` não monta o NavHost até receber esse estado e deriva URL, credenciais, autenticação e `serverId` do mesmo snapshot. O método default da interface, para fakes leves, é explicitamente não atômico; implementações persistidas devem sobrescrevê-lo.
   - `TEST`: `testDebugUnitTest` 1.569/1.569, `:core:api:lintDebug`, `:data:lintDebug`, `:app:lintDebug`, `:app:assembleDebug`, `:app:compileDebugAndroidTestKotlin` e `git diff --check` passaram. `SessionRepositoryPersistenceTest` passou 4/4 em PHONE/API 35 e verifica snapshot completo após recriar o DataStore.
    - `E2E RED`: ao tornar o MockWebServer A realmente alcançável (`localhost` dentro do AVD), a Activity requisitou `/Users/old-server-user/Items/server-b-item` em A antes de chegar à seleção; o teste falhou por chamada indevida, reproduzindo bug real de cold start (não HTTP 502 nem falha do fixture).
    - `E2E GREEN`: a rota inicial agora compara `serverId` antes de compor o detalhe; mismatch inicia em seleção e inicializa `switchingServer`, bloqueando auto-conexão em A. A suíte PHONE/API 35 completa passa 5/5 com A e B alcançáveis.
   - `INTENT`: um deep link apontando ao servidor B, recebido enquanto a sessão pertence ao A, mantém a seleção de servidor e não verifica/conecta automaticamente usando a URL salva de A.
   - `RED/GREEN`: `ServerSelectionPolicyTest` falhou antes da correção porque `automaticServerCandidate` devolvia `serverUrl` durante `switchingServer`; após a política receber esse estado e bloqueá-lo, o teste focal passou (20/20). A tela passa `switchingServer` como chave do efeito e como entrada da política.
   - `RED/GREEN ViewModel`: teste `view model initialization does not request login options from the saved endpoint` falhou antes da correção porque a criação do ViewModel iniciava `getAvailableUsers()` e `isQuickConnectEnabled()` na URL salva; depois as duas chamadas foram movidas para `prepareLoginOptions()`, acionado apenas no Login ativo e fora de uma troca. O módulo `:feature:auth:testDebugUnitTest` passou após a alteração.
    - `INSTRUMENTAÇÃO`: PHONE/API 35, `MainActivitySessionNavigationTest` 5/5. O caso A→B comprova: nenhuma chamada a A; GET `/System/Info/Public` e `/Users/Public` em B; POST `/Users/AuthenticateByName` em B com credenciais verificadas pelo mock; GET `/Users/{userId}/Items/{itemId}` em B; após recriar `SessionRepositoryImpl`, URL, token, usuário e `serverId` B persistidos. Não declara renderização visual do item nem autenticação em servidor real.
    - `QUALITY GATE FINAL`: `testDebugUnitTest` totalizou 1.577 testes, 0 falhas/erros/ignorados; `:app:lintDebug`, `:app:assembleDebug`, `:app:compileDebugAndroidTestKotlin` e `git diff --check` passaram. Não foi alterada versão nem gerado APK de produção.
5. **Build/package APK:** metadados `applicationId`, `versionName` e `versionCode` do APK agora são lidos por `aapt dump badging` e comparados à configuração/versão solicitada antes da assinatura e cópia. Pester 3.4 passou 4 testes, incluindo rejeição de `versionName` e `versionCode` antigos e `applicationId` divergente; APK previamente existente foi inspecionado como `org.mulletaflix.android` / `1.3.82` / `382`. Permanece pendente teste de integração automatizado do empacotador com APK sintético/SDK stub, inclusive prova de não sobrescrita dos destinos em caso de rejeição. Nenhuma release foi gerada nesta tarefa.
6. **Fluxos visíveis de features:** `LiveTvScreen` deve enviar o ID correto ao iniciar canal/gravação; `ProfileScreen` deve refletir confirmação de logout/troca de conta; Downloads devem sobreviver a reinício/retomar e abrir arquivo offline; Login/registro devem demonstrar validação, erro e sucesso; SyncPlay deve conectar ações da tela, eventos e estado final.

### P2 — persistência, conectividade e UX adaptativa

1. `SearchHistoryRepositoryImpl`: persistência, isolamento por usuário **e servidor**, limite/ordem, duplicatas, remoção, limpeza por servidor e limpeza total por usuário entre servidores; JSON corrompido. O escopo servidor/conta agora tem RED-GREEN nesta rodada: servidores diferentes isolam o histórico, LAN/URL pública com o mesmo ID estável compartilham, e “limpar dados locais” remove todos os escopos do usuário. Dados antigos sem identidade de servidor não são migrados automaticamente porque sua origem não pode ser inferida; a limpeza total também apaga a chave legada.
2. `HomeFeedCacheRepositoryImpl`: DataStore concreto, reconstrução do repositório e estado corrompido.
3. `ConnectivityNetworkMonitor`: RED-GREEN comprova que rotas Wi-Fi/Ethernet locais sem `NET_CAPABILITY_INTERNET` contam como acessíveis e que o `NetworkRequest` não exige Internet (instrumentado 1/1 em PHONE/API 35). Após revisão, callbacks não consultam capabilities/medição sincronamente: API 26+ usa capabilities entregues; API 24–25 agenda uma reconciliação no main looper após o callback retornar. Teste unitário verifica o limite 24/25/26/35; `ConnectivityNetworkCallbackTest` passou 2/2 em PHONE/API 35 cobrindo espera de capabilities e estado tarifado/perda. A estratégia de API 24–25 ainda não foi executada em AVD antigo; múltiplas redes e desregistro ao cancelar Flow seguem sem teste de integração. O histórico RED da adaptação de compatibilidade não foi registrado antes do código e não é alegado como TDD demonstrado.
4. `CheckAppUpdateUseCase`: atualização disponível/indisponível e erro do repositório.
5. Acessibilidade integrada em telas e diálogos, com ordem TalkBack/foco, escala de fonte e estados visuais; complementar a cobertura existente de componentes e contraste.
6. Matriz de layout/interação para PHONE, TABLET e TV: alvo touch, clipping, escala, rolagem e foco D-pad. Um teste portátil genérico não comprova os três perfis.

### P3 — suporte de testes

`:core:testing` contém fixtures compartilhadas. Não precisa de teste próprio enquanto permanecer apenas como dados constantes; se fixtures ganharem lógica, cobrir determinismo, valores padrão e casos representativos no próprio módulo.

- **Quality gate no CI Android:** `../.github/workflows/ci.yml` agora executa `./gradlew test lintDebug` e guarda relatórios JUnit/lint no job Android mesmo se uma etapa anterior falhar; build Debug continua separado. Instrumentação em emulador e matriz PHONE/TABLET/TV permanecem fora dos runners CI atuais, a avaliar sem fazer os gates básicos dependerem de secrets.

## Auditoria TDD do projeto Android — 2026-10-08

- Escopo: 18 módulos Gradle registrados, incluindo 17 módulos de produto e `:core:testing` como fixtures compartilhadas.
- Inventário estático: 239 arquivos de teste JVM com 1.532 anotações `@Test`; 143 arquivos instrumentados com 440 anotações `@Test`. Essas contagens descrevem fontes, não cobertura, nem garantem que cada caso tenha sido executado nesta auditoria.
- Baseline executado nesta auditoria: `.\gradlew.bat testDebugUnitTest --no-daemon --console=plain` — `BUILD SUCCESSFUL`; 238 relatórios XML, 1.532 testes, 0 falhas, 0 erros e 0 ignorados.
- O legado não recebe selo retroativo de TDD: não há histórico RED-GREEN consolidado. A exigência começa em mudanças novas/alteradas; lacunas existentes devem ser fechadas em ordem P1 → P2 → P3 com teste RED válido antes do código.
- Instrumentação existente cobre fluxos de integração nos AVDs, mas sua execução local depende de `tools\with-emulator.ps1`; esta auditoria não executou os 440 casos instrumentados nem a matriz completa de PHONE/TABLET/TV.
- Sem relatório JaCoCo/Kover aferido para o escopo, não declarar percentual de cobertura. Suíte verde é baseline de regressão, não homologação funcional integral.

### Registro TDD — atualização sem download prévio para instalações Google Play

- `INTENT`: quando `PackageManager` identifica `com.android.vending` como instalador, os fluxos de atualização em Home e Ajustes devem abrir a ficha da Play Store antes de baixar APK; falha ao abrir loja não deve iniciar download silenciosamente. Origens desconhecidas/alternativas mantêm o fluxo atual de APK.
- `RED`: o teste do ViewModel de Ajustes falhou pela expectativa de abrir a loja sem chamar `downloadApk`; o teste de política falhou inicialmente porque o predicado ainda retornava `false`.
- `GREEN`: testes focados de política e ViewModels passaram; `testDebugUnitTest` completo — 1.532 testes, 0 falhas/erros/ignorados. `AppUpdateInstallerIntegrationTest` executou 6/6 no AVD PHONE API 35, incluindo comparação com os metadados reais do PackageManager.
- `QUALITY GATE`: suíte JVM completa aprovada nesta auditoria. Não foi executada nesta tarefa uma nova rodada de lint ou `assembleDebug`; isso permanece no gate final da mudança de produção.
- `DEVICE`: AVD `MulletaflixApi35`, PHONE/API 35; 6 testes instrumentados, 0 falhas, 0 erros, 0 ignorados. A detecção depende dos metadados de origem disponibilizados pelo Android.
- `LIMITAÇÕES`: o teste de integração compara a política com a origem reportada pelo AVD; não abre a Google Play nem instala/atualiza um APK via Play. Instalação lateral/origem desconhecida segue baixando o APK conforme o fluxo existente.

`TWINS`: searched `openGooglePlay = openPlayStoreUpdate` — found 2 other sites: `app/src/main/java/org/mulletaflix/android/MainActivity.kt`, `feature/settings/src/main/java/org/mulletaflix/feature/settings/SettingsScreen.kt`.

## Frentes do projeto e propriedade

| Módulo | Foco TDD e limite de validação |
| --- | --- |
| `:core:common` | Políticas comuns, conectividade, atualização/instalação e utilitários; instrumentar APIs Android que não podem ser demonstradas na JVM. |
| `:core:api` | Contratos HTTP/WebSocket, autenticação, redirecionamento, erros, identidade e cancelamento. |
| `:domain` | Use cases, decisões de negócio, paginação e estados independentes de Android. |
| `:data` | Repositórios, mapeamento, isolamento por conta/servidor e persistência concreta. |
| `:design-system` | Semântica acessível, foco remoto, dimensões e políticas de dispositivo. |
| `:feature:auth` | Login, cadastro, Quick Connect, validação, erros e transição de sessão. |
| `:feature:home` | Descoberta, refresh, solicitações, navegação por toque/D-pad e ocultação de Livros na TV. |
| `:feature:library` | Filtros, ordenação ascendente/descendente, paginação, refresh e exclusão de livros na TV. |
| `:feature:item-detail` | Tipos de mídia, capas, leitores, progresso, controles de downloads por dispositivo. |
| `:feature:player` | Playback, áudio/legendas, lifecycle, OSD, PiP, Cast e pausa por perfil. |
| `:feature:search` | Debounce, filtros, histórico, resultados e diferenças TV/portáteis. |
| `:feature:downloads` | Fila, estados, ações remotas/touch, retomar e abrir mídia offline. |
| `:feature:live-tv` | Guia, canais, gravações, timers e início de playback. |
| `:feature:settings` | Persistência de preferências, tema, cache e opções específicas por dispositivo. |
| `:feature:user` | Troca de conta, perfil, logout e isolamento de preferências. |
| `:feature:sync-play` | Estado de sala, eventos realtime, refresh, entrada, saída e falhas. |
| `:app` | Inicialização, DI, navegação de produção, intents/deep links, serviços e integração de módulos. |

## Quality gate

Execute da raiz `MulletaFlix-android`:

```powershell
.\gradlew.bat testDebugUnitTest --no-daemon --console=plain
.\gradlew.bat :<modulo-afetado>:lintDebug :app:lintDebug :app:assembleDebug --no-daemon --console=plain
```

Use `tools\with-emulator.ps1` para instrumentação. Informe `expectedDeviceProfile=PHONE`, `TABLET` ou `TV`, execute os casos afetados em cada perfil e confira XML/log para testes executados, ignorados, erros e falhas. Consulte `TESTING.md` para comandos, emuladores e cenários existentes.

Quality gate executado em 2026-10-08 neste workspace: 231 arquivos XML, 1.485 testes JVM, 0 falhas, 0 erros e 0 ignorados; `:core:common:lintDebug`, `:core:api:lintDebug`, `:app:lintDebug` e `:app:assembleDebug` passaram. `TvHomeRefreshIntegrationTest` também executou 2/2 no AVD `MulletaflixTvApi34`, perfil TV, 0 falhas, 0 erros e 0 ignorados; o wrapper fechou o emulador ao terminar. Esses números registram execuções, não cobertura e não comprovam RED-GREEN histórico.

Verificação instrumentada adicional de Livros na Android TV em 2026-10-08: `TvHomeRefreshIntegrationTest` 2/2, `LibraryOfflineReconnectFlowTest` 2/2 e `BookReaderRouteAvailabilityTest` 1/1 passaram no AVD `MulletaflixTvApi34` (API 34, GPU NVIDIA). Resultado: Home e biblioteca não exibem livros, inclusive com catálogo persistido e falha de atualização; rota direta do leitor é bloqueada na TV e permanece disponível em dispositivos portáteis. O wrapper encerrou o AVD após cada execução. Isso confirma os comportamentos cobertos, não valida interação com servidor real nem prova histórico RED-GREEN dos testes preexistentes.

### Registro TDD — validação estrutural do APK baixado

- `INTENT`: o downloader marca qualquer resposta não vazia como concluída; os testes exigem rejeitar payload sem ZIP contendo manifesto Android não vazio; o plano define um artefato instalável e acompanha integridade do download como lacuna P1.
- `RED`: teste de HTTP 200 com corpo HTML falhou porque era emitido como `Completed`; em seguida, `AppUpdateDownloaderTest.apk archive with empty manifest is rejected` falhou porque ZIP com manifesto vazio também era aceito.
- `GREEN`: `:core:common:testDebugUnitTest --tests 'org.mulletaflix.core.common.update.AppUpdateDownloaderTest' --tests 'org.mulletaflix.core.common.update.ApkIntegrityTest'` — passou; rejeita payload não-ZIP ou manifesto ausente/vazio e remove arquivo parcial.
- `QUALITY GATE`: `testDebugUnitTest :core:common:lintDebug :app:lintDebug :app:assembleDebug --no-daemon --console=plain` — `BUILD SUCCESSFUL`; 237 arquivos XML, 1.528 testes, 0 falhas, 0 erros e 0 ignorados.
- `DEVICE`: JVM; alteração valida o formato antes da integração do instalador. Sem interação de dispositivo necessária.
- `LIMITAÇÕES`: validação estrutural não comprova assinatura confiável; checksum esperado permanece a verificação criptográfica quando fornecido. Testes de progresso, HTTP inválido, corpo vazio e permissão do instalador seguem pendentes.

`TWINS`: busca por `Completed(downloadedFile)` — nenhum outro ponto de produção conclui o download do APK sem passar pela validação estrutural.

### Registro TDD — instalador do APK e ausência de permissão restrita

- `INTENT`: evitar crash no Android 8+ quando a consulta de origem desconhecida lança `SecurityException`; o fluxo deve direcionar à configuração do próprio app quando disponível e expor o APK de cache por FileProvider. Escopo limitado ao APK; sem abrir instalador/configurações externos no teste.
- `RED`: teste instrumentado em `MulletaflixApi35` falhou com `SecurityException: Need to declare android.permission.REQUEST_INSTALL_PACKAGES to call this api` em `AppUpdateInstaller.canRequestPackageInstalls()`.
- `GREEN`: `AppUpdateInstaller` trata a exceção como permissão não concedida. O teste de integração captura a intent para configurações em `ContextWrapper`; outro teste exercita o provider manifesto real e lê os bytes através de `contentResolver`. Execução PHONE/API 35: `:app:connectedDebugAndroidTest` — 3 testes aprovados, sem iniciar UI externa. A intent `ACTION_VIEW` é verificada condicionalmente caso o dispositivo tenha a permissão concedida, mas essa condição não ocorre no manifesto atual.
- `POLÍTICA`: não adicionar `REQUEST_INSTALL_PACKAGES` ao manifesto para autoatualização. A política Google Play restringe essa permissão e não permite seu uso para atualizar o próprio app, salvo a exceção de gestão de dispositivos; versão distribuída pela Play deve adotar fluxo de atualização pela loja.
- `LIMITAÇÕES`: a intent final `ACTION_VIEW` depende de a permissão estar realmente concedida, condição ausente neste APK de teste. O teste não declara que autoatualização direta funciona em instalações Google Play; roteamento de atualização para Play Store continua pendente.
- `QUALITY GATE`: `testDebugUnitTest :core:common:lintDebug :app:lintDebug :app:assembleDebug --no-daemon --console=plain` — `BUILD SUCCESSFUL`; 237 XML, 1.528 testes JVM, 0 falhas/erros/ignorados. `git diff --check` passou.
- `DEVICE`: `:app:connectedDebugAndroidTest` com `expectedDeviceProfile=PHONE`, AVD `MulletaflixApi35` (Android 15), NVIDIA via wrapper — 3/3 testes aprovados, 0 falhas/erros/ignorados; wrapper encerrou o emulador.
- `TWINS`: busca `rg -n "canRequestPackageInstalls\\(" core app feature --glob '*.kt'` — uma ocorrência de produção, em `AppUpdateInstaller`; as demais são chamada interna/teste. Nenhuma implementação paralela da consulta de permissão encontrada.

### Registro TDD — persistência da sessão e versão oficial não verificada

- `INTENT`: código de produção inventava a versão `12.0.2` para o servidor oficial salvo; o teste exige `null` até uma verificação real; `AuthState` documenta que a nuvem pode ser atualizada independentemente do APK e que a versão só deve aparecer após o handshake.
- `RED`: `:data:connectedDebugAndroidTest` com `expectedDeviceProfile=PHONE` e classe `SessionRepositoryPersistenceTest` — falhou como esperado: `expected null, but was:<12.0.2>`; os outros dois testes passaram.
- `GREEN`: mesma classe instrumentada após remover a versão fixa — 3/3 aprovados. Também comprova salvar/reabrir/limpar credenciais da sessão e manutenção do servidor selecionado/DeviceId, além de atualização/removal e preservação do servidor oficial na lista salva.
- `QUALITY GATE`: `testDebugUnitTest :data:lintDebug :app:lintDebug :app:assembleDebug --no-daemon --console=plain` — `BUILD SUCCESSFUL`; 237 XML, 1.528 testes JVM, 0 falhas, 0 erros e 0 ignorados; `git diff --check` passou.
- `LIMITAÇÕES`: cobertura da persistência concreta executada no AVD PHONE; isolamento entre múltiplas contas/servidores e migração de arquivos preexistentes continuam pendentes.
- `DEVICE`: AVD `MulletaflixApi35` (Android 15), perfil PHONE, QEMU confirmado na NVIDIA via `tools/with-emulator.ps1`; wrapper encerrou o emulador.
- `TWINS`: searched `version\\s*=\\s*"12\\.0\\.2"` — found 0 other production sites: none. The 3 remaining matches are test fixtures in `feature/auth/src/test/java/org/mulletaflix/feature/auth/AuthViewModelTest.kt`.

### Registro TDD — atualização direcionada à Play Store

- `INTENT`: quando o APK não declara `REQUEST_INSTALL_PACKAGES`, a atualização não deve abrir uma tela de fontes desconhecidas que não pode conceder a permissão; deve encaminhar à ficha deste aplicativo na Google Play, ou à URL HTTPS da Play Store se o app Play Store não puder atender a intent `market:`.
- `RED`: `:app:connectedDebugAndroidTest` filtrado para `AppUpdateInstallerIntegrationTest#installFallsBackToGooglePlayWhenTheManifestCannotRequestPackageInstalls`, AVD `MulletaflixApi35`, perfil PHONE, GPU host — falhou pela asserção esperada `quando instalação lateral não é permitida, deve abrir a página da loja`; o fluxo anterior retornava `false` depois de abrir configurações.
- `GREEN`: o teste focado passou (1/1); depois a classe `AppUpdateInstallerIntegrationTest` passou 5/5, incluindo fallback HTTPS, FileProvider e captura de intents sem abrir UI externa.
- `QUALITY GATE`: `testDebugUnitTest :core:common:lintDebug :app:lintDebug :app:assembleDebug --no-daemon --console=plain` — `BUILD SUCCESSFUL`; 237 arquivos XML JVM, 1.528 testes, 0 falhas, 0 erros e 0 ignorados. Lint e `assembleDebug` passaram; `git diff --check` passou (avisos apenas de conversão LF/CRLF do Git).
- `DEVICE`: AVD `MulletaflixApi35` (Android 15/API 35), perfil PHONE; `tools\\with-emulator.ps1 -GpuMode host` confirmou QEMU na GPU NVIDIA e encerrou o emulador após a execução. Classe de instrumentação 5/5.
- `LIMITAÇÕES` (naquele ciclo): os ViewModels ainda baixavam o APK antes do encaminhamento à loja; esse comportamento foi corrigido e testado no registro TDD mais recente. O teste captura intents e não abre a Play Store, não publica nem instala atualização real; teste do ramo de fontes desconhecidas com permissão declarada não foi executado. Se nem loja nem handler HTTPS existirem, o chamador recebe `false`.

`TWINS`: searched `openUnknownAppSourcesSettings(context)` — found 0 other production sites outside `AppUpdateInstaller`; the settings handoff is centralized.

### Registro TDD — identidade após validação da rota HTTP LAN

- `INTENT`: ClientIdentityInterceptor must defer session reads for dynamic HTTP LAN hosts until the connected-route guard accepts the socket; same-origin redirects reuse that captured identity, cross-origin redirects never receive it; TODO-APP P1 requires LAN HTTP and rejects non-local cleartext before credentials.
- `RED`: o teste de hostname DNS local falhou ao observar a identidade antes do guard de rota; o teste do cliente de legendas com interceptor envolvido falhou porque o segundo estágio de identidade não estava instalado; o teste de redirect same-origin falhou ao observar duas leituras do token em vez de uma.
- `GREEN`: `:core:api:testDebugUnitTest` + `:app:testDebugUnitTest` filtrados para `CleartextRequestProtectionTest`, `OfflineSubtitleNetworkPolicyTest` e `OfflineSubtitleHttpTransferTest` passaram. A classe `CleartextRequestProtectionTest` executou 11/11, incluindo redirects same-origin/cross-origin. `CleartextTrafficPolicyTest` comprova que DNS local resolvido fora da sub-rede é negado.
- `QUALITY GATE`: `testDebugUnitTest :core:common:lintDebug :core:api:lintDebug :app:lintDebug :app:assembleDebug --no-daemon --console=plain` — `BUILD SUCCESSFUL`; 238 relatórios XML, 1.539 testes JVM, 0 falhas, 0 erros e 0 ignorados. `git diff --check` passou.
- `DEVICE`: `AndroidCleartextPolicyIntegrationTest` — 3/3, PHONE API 35, AVD `MulletaflixApi35`; o wrapper confirmou QEMU na NVIDIA e encerrou o emulador. O teste instrumentado cobre política empacotada e tráfego HTTP na interface LAN; não é um teste end-to-end de hostname local resolvido fora da sub-rede.
- `LIMITAÇÕES`: `Network Security Config` ainda admite cleartext amplo para suportar CIDR dinâmico; APK-H12 permanece aberto. Um novo teste focal constrói rota `.local` para endereço remoto e verifica rejeição antes das leituras de token/DeviceId; a classe `CleartextRequestProtectionTest` passou ao ser reexecutada nesta retomada. Esta alteração cobre identidade por cabeçalho, não tokens `api_key` em query strings. O teste usa rota/socket controlados, não valida descoberta DNS nem pacote de rede em dispositivo real. Suítes .NET, Python, web, iOS e scripts não foram executadas nesta rodada; plano de projeto em `TDD-PLAN.md` continua incremental.

`TWINS`: searched `ClientIdentityInterceptor\(` — found 26 other sites: `core/api/src/test/**` (20), `app/src/test/**` (3), `app/src/androidTest/**` (2), `core/api/src/androidTest/**` (1); no parallel production constructor.

### Registro TDD — troca de sala SyncPlay e callback WebSocket obsoleto

- `INTENT`: ao trocar de sala, o callback tardio `onClosed` do WebSocket anterior não pode limpar o socket atual nem marcar a nova conexão como desconectada.
- `RED`: mutação temporária fez `isCurrentSyncPlayConnection(...)` retornar `true` para qualquer callback; `:core:api:testDebugUnitTest --tests "org.mulletaflix.core.api.SyncPlayReconnectPolicyTest" --no-daemon --console=plain` falhou como esperado em `rejects callbacks from a stale connection generation` (`SyncPlayReconnectPolicyTest.kt:55`, 7 testes, 1 falha). Mutação revertida.
- `GREEN`: `:core:api:testDebugUnitTest --tests "org.mulletaflix.core.api.SyncPlayReconnectPolicyTest" --tests "org.mulletaflix.core.api.SyncPlayRealtimeGroupSwitchTest" --no-daemon --console=plain` passou. O teste de política valida diretamente geração/socket atuais; a integração MockWebServer valida dois upgrades reais, conexão da nova sala antes de concluir o fechamento da antiga e os dois handshakes. O latch confirma o handshake no servidor, não a conclusão do callback no cliente; por isso removi a observação negativa de 500 ms que não tinha barreira determinística.
- `QUALITY GATE`: `testDebugUnitTest :core:api:lintDebug :app:lintDebug :app:assembleDebug --no-daemon --console=plain` — `BUILD SUCCESSFUL` em 55s; 239 relatórios XML sob `build/test-results/testDebugUnitTest`, 1.548 testes JVM, 0 falhas, 0 erros e 0 ignorados. `git diff --check` passou.
- `DEVICE`: não aplicável; integração JVM no transporte WebSocket OkHttp. Não valida conexão com servidor público.
- `LIMITAÇÕES`: cobre troca de sala e fechamento atrasado; não cobre todos os eventos SyncPlay nem substitui teste ponta a ponta. Sem release.

### Revalidação Android — 2026-10-08

- `QUALITY GATE`: `testDebugUnitTest :core:common:lintDebug :core:api:lintDebug :app:lintDebug :app:assembleDebug --no-daemon --console=plain` — `BUILD SUCCESSFUL`; relatórios XML: 239 arquivos, 1.548 testes JVM, 0 falhas, 0 erros e 0 ignorados. Lint e build Debug passaram.
- `DEVICE`: `AppUpdateInstallerIntegrationTest` 6/6 e `SessionRepositoryPersistenceTest` 3/3 no AVD PHONE/API 35. `HomeLibraryNavigationTest` 3/3 no AVD TV/API 34; `books_libraryIsNotRenderedOnAndroidTv` confirmou ausência de Livros e presença de Filmes na tela real Compose. `tools/with-emulator.ps1` confirmou NVIDIA RTX 3050 para cada AVD e encerrou os emuladores; `adb devices` ficou sem devices.
- `RED/GREEN`: as mudanças de updater/sessão revisadas já estavam no worktree antes desta revalidação; não atribuir RED-GREEN retrospectivo aos testes acima. O compilador inicialmente revelou que o fake de `Interceptor.Chain` do teste off-subnet precisava implementar novos membros do OkHttp 5; após completar o fake, as classes `CleartextRequestProtectionTest` e `AppUpdateDownloaderTest` executaram sem falhas.
- `LIMITAÇÕES`: nenhuma release ou APK de produção foi gerado. A configuração global de cleartext permanece pendente (APK-H12); testes de rota controlada não substituem ensaio de rede real. A solicitação de incluir OCR offline requer autorização explícita para acrescentar a dependência empacotada.

## Critério de conclusão do plano

- TDD RED-GREEN demonstrado em toda mudança executável nova ou alterada.
- Lacunas P1 encerradas por testes primeiro, com resultados registrados.
- Lacunas P2 e P3 fechadas na sequência, sem ignorar perfis de dispositivo afetados.
- Suite completa, lint e build passam; instrumentação executa casos nos perfis pertinentes.
- Histórico antigo sem evidência permanece explicitamente “não auditado”; nunca reclassificar testes retroativamente como TDD sem prova.

### Registro TDD — restauração de deep link na recriação da Activity

- `INTENT`: `MainActivity.onCreate` reinterpreta o intent de lançamento em toda recriação, mesmo depois que o `NavHost` consome o pedido; o teste exige que pedidos consumidos continuem ausentes e os pendentes preservem pedido e sequência. `TDD-PLAN.md` P1 pede integração de navegação e lifecycle no app real.
- `RED`: `:app:connectedDebugAndroidTest` com `MainActivityDeepLinkRestoreTest`, perfil PHONE/API 35 — `consumedLaunchLinkDoesNotReturnAfterActivityRecreation` falhou: `A consumed launch link must stay consumed expected null, but was:<MediaDeepLinkRequest(itemId=deep-link-restore-test, sequence=1, serverId=null)>`; 2 testes, 1 falha.
- `GREEN`: mesma classe instrumentada no AVD PHONE/API 35 — 2/2 aprovados, 0 falhas/erros/ignorados. `ActivityScenario.recreate()` confirma que o estado consumido fica `null`, que pedido pendente mantém sequence `2` e que `onNewIntent` seguinte avança para `3`.
- `QUALITY GATE`: `testDebugUnitTest :app:lintDebug :app:assembleDebug --no-daemon --console=plain` — `BUILD SUCCESSFUL` em 4m59s; 239 XML, 1.548 testes JVM, 0 falhas/erros/ignorados; lint app e build Debug passaram. `git diff --check` passou.
- `DEVICE`: `tools\\with-emulator.ps1`, `MulletaflixApi35` (Android 15/API 35), perfil PHONE; QEMU confirmado na NVIDIA e encerrado pelo wrapper.
- `LIMITAÇÕES`: não valida deep link com servidor real, login/autenticação, mismatch de servidor ou restauração completa do back stack do `NavHost`; sem APK de produção/release.
- `TWINS`: searched `mediaDeepLinkRequest(intent, ++deepLinkSequence)` — found 1 other site: `MainActivity.onNewIntent`; intentional new-delivery path, not launch-intent reparse on recreation.

### Registro de cobertura — despacho HTTP de formatos de livro já suportados

- `INTENT`: proteger o contrato existente HTTP → MIME/assinatura → modelo de leitor para respostas PDF, CBZ e texto. Esta rodada só adicionou testes; não alterou comportamento de produção nem afirma defeito corrigido.
- `JVM`: `:feature:item-detail:testDebugUnitTest --tests 'org.mulletaflix.feature.itemdetail.BookReaderViewModelTest'` — `BUILD SUCCESSFUL`; inclui resposta CBZ com `application/octet-stream` e TXT `text/plain`, confirmando leitor e conteúdo correspondentes.
- `DEVICE`: `BookReaderScreenIntegrationTest` no AVD PHONE/API 35 — 9/9, 0 falhas/erros/ignorados; `pdfHttpPayloadUsesTheNativePaginatedReader` confirma a rota HTTP, `PdfBookDocument` e duas páginas PDF. `with-emulator.ps1` confirmou QEMU na GPU NVIDIA e encerrou o AVD.
- `QUALITY GATE`: `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug --no-daemon --console=plain` — `BUILD SUCCESSFUL`; 239 relatórios XML, 1.550 testes JVM, 0 falhas, 0 erros e 0 ignorados; `git diff --check` aprovado.
- `TDD`: sem RED comportamental nesta rodada, pois a implementação já aceitava esses formatos e o objetivo era cobrir lacuna de regressão. Uma primeira fixture PDF em Robolectric falhou ao usar `PdfDocument` fechado antes da execução; isso foi classificado como problema de fixture/ambiente e movido para instrumentação Android, sem tratar como falha do produto.
- `LIMITAÇÕES`: o ViewModel de PDF tem agora fluxo HTTP validado em instrumento; não foram adicionados casos de limite/truncamento ou MIME incompatível nesta rodada. Sem mudança de produção, bump, APK/release, servidor ou portal.

### Registro TDD — comandos da fila offline sempre via DownloadService

- `INTENT`: toda mutação de fila (adicionar, remover, pausar e retomar), inclusive a restauração de uma fila marcada como pausada, deve passar pelo `DownloadService` para manter o ciclo de vida foreground e o estado persistido coerentes.
- `RED`: `:app:testDebugUnitTest --tests org.mulletaflix.android.service.DownloadQueueGuardTest` — 2 testes falharam pelas razões esperadas: chamadas diretas `manager.pauseDownloads()`/`manager.resumeDownloads()` e ausência de `sendPauseDownloads` no repositório.
- `GREEN`: o teste focal passou em nova execução. `testDebugUnitTest :app:lintDebug :app:assembleDebug --no-daemon --console=plain` — `BUILD SUCCESSFUL`; 239 relatórios XML, 1.556 testes JVM, 0 falhas, 0 erros e 0 ignorados.
- `QUALITY GATE`: o gate acima passou; `git diff --check` passou. Uma execução intermediária teve falha transitória do KSP por arquivo gerado ausente; foi repetida e a execução final concluiu com código 0.
- `DEVICE`: não aplicável à mudança estrutural dos comandos. A integração existente confirma download completo e reprodução offline, mas não mata/reinicia o processo nem valida retomada de download incompleto.
- `LIMITAÇÕES`: a retomada após morte real do processo e em fila parcial continua pendente para teste instrumentado/orquestrado isolado; não simular isso encerrando o processo do runner. Nenhuma mudança de versão, APK de produção, release, servidor ou portal.
- `TWINS`: buscadas chamadas diretas `manager.addDownload/removeDownload/pauseDownloads/resumeDownloads` em `Media3DownloadRepository.kt`; 0 ocorrências restantes.

### Verificação instrumentada — persistência do índice ao recriar DownloadManager

- `INTENT`: uma fila pausada e enfileirada no índice local deve sobreviver à recriação do `DownloadManager`; ao retomar, deve concluir usando o cache isolado, sem iniciar tráfego antes do comando de retomada.
- `DEVICE`: `DownloadManagerCleartextIntegrationTest` no PHONE API 35 — 2/2 aprovados, 0 falhas/erros/ignorados; inclui download local/redirect e recriação do manager com SQLite/cache temporários. `tools/with-emulator.ps1` confirmou QEMU na NVIDIA RTX 3050 e deixou `adb devices` sem AVD após terminar.
- `QUALITY GATE`: `testDebugUnitTest :app:lintDebug :app:assembleDebug :app:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL`; 239 relatórios XML, 1.556 testes JVM, 0 falhas, 0 erros e 0 ignorados.
- `TDD`: cobertura de integração adicionada para um contrato Media3 já existente; não foi feita alegação de RED de produção nem alterado código produtivo nesta etapa.
- `LIMITAÇÕES`: recriar `DownloadManager` valida o índice/cache persistentes, mas não simula a morte do processo Android nem comprova retomada de bytes parciais. O ensaio real exige duas execuções de instrumentação coordenadas no host e fixture fora do processo-alvo; não executar `force-stop` no runner. Sem mudança de versão, APK/release, servidor ou portal.

### RED/GREEN — retomada de download parcial após morte real do processo Android

- `INTENT`: confirmar com bytes reais que a fila Media3 e o arquivo parcial persistem após `force-stop`, e que o próximo cold start reconecta a fila ao `DownloadService` automaticamente, sem exigir abrir a tela Downloads.
- `RED`: baseline sem injeção eager executou `preparePartialDownloadForHostProcessRestart` (1/1) e parou o processo; após relaunch, o fixture registrou apenas o GET inicial, nenhum Range. O host falhou explicitamente com `No partial byte range was resumed after process death; observed offset=0`.
- `GREEN`: `MulletaFlixApp` injeta eager `DownloadRepository`, construindo `Media3DownloadRepository` no início do processo; seu startup existente manda `sendResumeDownloads` ao `DownloadService`. No AVD PHONE/API 35, o fixture registrou GET inicial de 0 e GET Range de 6,620,576; a resposta recomeçada transferiu os 10,156,640 bytes restantes (16 MiB menos o offset persistido), completou, e o APK não reenviou o prefixo.
- `TESTE`: `DownloadProcessRestartIntegrationTest` separa preparação, evidência de resposta recomeçada e limpeza; `tools/Test-AndroidDownloadProcessRestart.ps1` instala os APKs de teste uma vez, para o app apenas após o runner sair, inicia o app novamente, exige Range não-zero e response completo, e remove apenas a entrada UUID do teste pelo serviço.
- `GPU/AVD`: `tools/with-emulator.ps1 -AvdName MulletaflixApi35 -Port 5556 -GpuMode host` comprovou o QEMU na NVIDIA RTX 3050 e encerrou o AVD automaticamente. Não foi feito wipe nem apagada fila do usuário; o pré-requisito recusa fila ativa ou pausada antes de enfileirar o fixture.
- `LIMITAÇÕES`: execução real valida app debug em PHONE/API 35 e um GET com suporte a Range; ainda não valida morte sob diferentes condições de processo/OS, API 37, TV/tablet, ou servidor real. Sem bump, build release, APK de produção/publicação, servidor ou portal.

### Teste instrumentado — deep link de mídia com sessão autenticada

- `INTENT`: proteger entrada na Activity com sessão completa e verificar que um deep link `mulletaflix://details` busca exatamente o usuário/item identificados no link, sem depender do servidor público.
- `DEVICE`: `MainActivitySessionNavigationTest` PHONE/API 35 — 4/4 passaram: sem sessão, sessão incompleta, sessão completa na Home e deep link autenticado com `GET /Users/{userId}/Items/{itemId}` em MockWebServer. `tools/with-emulator.ps1` confirmou GPU NVIDIA e encerrou o AVD.
- `ISOLAMENTO`: os dados de sessão e `deviceId` originais são restaurados; se a sessão preexistente não puder ser restaurada com segurança, o teardown não a limpa. Chamadas de mídia do teste usam MockWebServer. A checagem de atualização do app iniciada pela Activity não foi substituída e pode acessar GitHub; o teste não é totalmente hermético quanto a essa checagem.
- `QUALITY GATE`: `testDebugUnitTest :app:lintDebug :app:assembleDebug :app:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL`; `git diff --check` passou.
- `TDD`: integração nova para contrato existente; sem RED de produção ou mudança de comportamento de produção nesta etapa. A primeira fixture 404 apenas demonstrou que um erro não prova o caminho de sucesso; resposta JSON 200 e observação do GET exato substituíram-na.
- `LIMITAÇÕES`: não valida o conteúdo visual completo da tela de detalhes nem autenticação contra servidor real; sem bump, APK de produção/release, servidor ou portal.

### RED-GREEN — confirmação duplicada de troca de usuário

- `INTENT`: enquanto uma autenticação para troca de usuário está pendente, novas confirmações não devem enviar logins duplicados nem executar callbacks duplicados. A tela já desabilita o botão com `isSwitchingUser`; o ViewModel deve proteger o mesmo contrato.
- `RED`: `:feature:user:testDebugUnitTest --tests "org.mulletaflix.feature.user.UserProfileViewModelTest.repeated switch confirmation while login is pending submits only once" --no-daemon --console=plain` — falhou pela regressão esperada: `expected:<1> but was:<2>` chamadas de login.
- `GREEN`: o mesmo teste passou ao marcar a troca ativa sincronamente antes de enfileirar a coroutine; verificou 1 login, 1 callback e fechamento do diálogo após sucesso.
- `QUALITY GATE`: `testDebugUnitTest :feature:user:lintDebug :app:lintDebug :app:assembleDebug --no-daemon --console=plain` — `BUILD SUCCESSFUL`; `git diff --check` passou.
- `REVISÃO`: revisão adversarial de `UserProfileViewModel` e teste não encontrou problemas.
- `LIMITAÇÕES`: não valida alternância completa de servidor/conta com DataStore real; esse fluxo persiste como lacuna P1. Nenhum bump, release, servidor ou portal.

### RED-GREEN — back stack de deep link autenticado em cold start

- `INTENT`: iniciar um deep link autenticado para a biblioteca atual deve empilhar o detalhe sobre Home, para Voltar retornar à Home em vez de finalizar a Activity. A regra já é compatível com a entrega assíncrona de links no `MulletaFlixNavHost`; `TODO-APP.md` mantinha restauração completa do back stack pendente.
- `RED`: a primeira prova instrumentada enviou Voltar e falhou com `No compose hierarchies found in the app`: o detalhe era a rota inicial e o dispatcher encerrou a Activity. A asserção de política também falhou porque `initialDestinationForSession` devolvia `detail/{itemId}` em links do mesmo servidor.
- `GREEN`: a política mantém `SERVER_SELECTION` para mismatch de `serverId`, mas inicia links compatíveis ou sem servidor identificado em `HOME`; o efeito do NavHost navega para o detalhe. `DeepLinkNavigationPolicyTest` passou 20/20.
- `DEVICE`: `MainActivitySessionNavigationTest` passou 5/5 em PHONE/API 35 e 5/5 em TV/API 34; o caso autenticado confirma GET do item no MockWebServer e retorno à Home pelo dispatcher real. Ambos AVDs executaram via wrapper na GPU NVIDIA e foram encerrados automaticamente.
- `QUALITY GATE`: `testDebugUnitTest :app:lintDebug :app:assembleDebug :app:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL`; 241 relatórios XML, 1.577 testes JVM, 0 falhas/erros/ignorados; `git diff --check` passou.
- `GRAPHIFY`: grafo Android atualizado e clusterizado — 9.530 nós, 23.774 arestas, 424 comunidades; query confirmou ligação entre política, Activity e testes. `--code-only` não atualiza semanticamente 11 Markdown/77 imagens; full extraction requer configuração de LLM indisponível nesta sessão. Versão local 0.9.58 é anterior à skill 0.9.79.
- `LIMITAÇÕES`: não valida link cross-server após login, logout, TABLET, conteúdo visual detalhado ou servidor real. Sem release.

### RED-GREEN — retomar refresh sem encadear ticks periódicos

- `INTENT`: Ao retomar Biblioteca, Favoritos ou TV ao Vivo durante uma carga, aguardar e atualizar uma vez; ticks periódicos não devem enfileirar cargas sucessivas.
- `RED`: os testes foram escritos primeiro para exigir a API explícita `refreshOnResume` e verificar que o timer não gera uma chamada adicional durante carga ativa; a primeira execução falhou na compilação porque essa API ainda não existia.
- `GREEN`: callbacks de retomada e timer agora são separados nos efeitos e nas três ViewModels; testes unitários validam refresh pós-carga coalescido e ausência de refresh extra nos ticks.
- `DEVICE`: `:feature:library:connectedDebugAndroidTest` com `TvRefreshEffectTest` e `:feature:live-tv:connectedDebugAndroidTest` com `LiveTvRefreshEffectTest` passaram em AVD TV/API 34; wrapper confirmou NVIDIA e encerrou o AVD.
- `QUALITY GATE`: 17 tarefas de módulos `:testDebugUnitTest` mais `testDebugUnitTest :app:lintDebug :app:assembleDebug` — `BUILD SUCCESSFUL`; relatórios agregados: 1.587 testes, 0 falhas, 0 erros, 0 ignorados. Lint das features library/live-tv passou.
- `DEVICE`: testes instrumentados específicos de `TvRefreshEffectTest` e `LiveTvRefreshEffectTest` passaram; ambos AVDs TV/API 34, NVIDIA confirmada, sem emulador restante aberto.
- `GRAPHIFY`: `graphify update .` concluiu após a alteração — 10.811 nós, 28.237 arestas, 514 comunidades; query confirmou relações entre as ViewModels, callbacks e telas.
- `LIMITAÇÕES`: cobertura instrumentada exercita ciclo de vida/callback; não verifica rede contra servidor real. Sem bump, APK de produção/release, servidor ou portal.

### RED-GREEN — manter a tela ativa durante a leitura

- `INTENT`: `BookReaderScreen` hoje não impede que a tela apague durante a leitura; o teste instrumentado falhará esperando `FLAG_KEEP_SCREEN_ON` enquanto o leitor estiver composto e esperando restauração após sua remoção; o objetivo do usuário permite melhorias de UX no APK, e o comentário existente no player documenta o precedente de manter a tela ligada durante reprodução ativa.
- `RED`: execução focada em `MulletaflixApi35` (PHONE/API 35) falhou 1/1 na asserção de que o leitor visível liga `FLAG_KEEP_SCREEN_ON` (não falha de compilação).
- `GREEN`: efeito de composição ativa o flag só enquanto `BookReaderScreen` está na árvore e restaura o estado previamente observado na janela. A Activity é resolvida percorrendo `ContextWrapper.baseContext`; o teste fornece contexto encapsulado, alterna leitor visível/ausente, verifica flag inicialmente desligado e preservação se já estava ligado.
- `REVISÃO ADVERSARIAL`: o revisor independente identificou que o cast direto `Context as? Activity` falhava sob um wrapper. O mesmo teste foi atualizado primeiro e reproduziu o RED no PHONE; a resolução pela cadeia corrigiu o caso, aprovado novamente em PHONE e TABLET.
- `DEVICE`: `readerKeepsScreenOnOnlyWhileVisibleAndRestoresPreviousWindowState` passou 1/1 em PHONE/API 35 e 1/1 em TABLET/API 35; `tools/with-emulator.ps1` confirmou QEMU usando GPU NVIDIA e encerrou cada AVD.
- `QUALITY GATE`: `:feature:item-detail:testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug --no-daemon --console=plain` — `BUILD SUCCESSFUL`; testes unitários do módulo item-detail: 190, 0 falhas/erros/ignorados; `git diff --check` executado após documentação.
- `GRAPHIFY`: grafo do subprojeto Android atualizado após mudanças na tela, teste e documentação; evidência de nós/arestas registrada após execução.
- `LIMITAÇÕES`: build produzido é apenas `app-debug.apk`, sem assinatura de produção; nenhuma mudança de versão, publicação, servidor ou portal.

### TDD — instrumentar a paginação EPUB e o progresso salvo

- `INTENT`: os controles EPUB Próxima/Anterior já estão habilitados e os localizadores são gravados no DataStore; o teste instrumentado deve demonstrar que os cliques atualizam e persistem a posição e que voltar recupera a anterior. O backlog mantinha paginação EPUB sem teste de interação.
- `RED/GREEN`: esta adição é cobertura do contrato que já existia, não correção de regressão de produção. A primeira execução expirou porque o novo teste procurava conteúdo do WebView como nó Compose; ajustado para inspecionar o DOM com `awaitWebViewText`, padrão já usado pelo teste de abertura do EPUB. A navegação e a persistência passaram sem mudança de produção. A remoção do localizador agora ocorre em `finally`, inclusive em falhas.
- `DEVICE`: `epubPaginationControlsMoveAndPersistTheReadingPosition` — 1/1 PHONE/API 35; classe `BookReaderScreenIntegrationTest` — 11/11 PHONE/API 35. Fixture EPUB entregue por `MockWebServer` local, sem servidor externo.
- `QUALITY GATE`: `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:connectedDebugAndroidTest` — `BUILD SUCCESSFUL`; 1.587 testes JVM, 0 falhas/erros/ignorados; classe instrumentada 11/11, 0 falhas/erros/ignorados. Após reforço do `finally`, o caso focado passou novamente 1/1. `git diff --check` passou.
- `REVISÃO`: revisão Android adversarial confirmou que só o teste muda; recomendou limpeza garantida, incluída. A evidência não comprova encerramento real do processo nem restauração ao reabrir o leitor.
- `GRAPHIFY`: atualizar o grafo Android após o teste e os documentos e conferir o nó/relação da integração EPUB.
- `LIMITAÇÕES`: executado apenas no perfil PHONE (único AVD disponível neste ambiente); D-pad/Back do sistema, falha de renderização, restauração em processo novo e validação de servidor/mídia reais seguem no APK-H21. Nenhuma mudança de versão, APK de produção/release, servidor ou portal.

### Cobertura de navegação do leitor na Activity principal

- `ESCOPO`: garantir que um deep link autenticado de livro abra o `BookReaderScreen` pela navegação real de `MainActivity` e que a tela/rota permaneça visível após recriação da Activity.
- `TESTE`: `MainActivitySessionNavigationTest.authenticatedBookDeepLinkOpensReaderAndSurvivesActivityRecreation` usa `MockWebServer`, sessão autenticada e deep link `mulletaflix://details`; valida detalhe, ação “Ler livro”, conteúdo EPUB e endpoint solicitado, chama `ActivityScenario.recreate()` e confirma o conteúdo após a recriação.
- `DEVICE`: PHONE/API 35, teste instrumentado direcionado 1/1 aprovado, 0 falhas/erros/ignorados; wrapper confirmou QEMU na NVIDIA RTX 3050 e encerrou o AVD.
- `LIMITAÇÃO`: `ActivityScenario.recreate()` valida somente recriação de Activity no mesmo processo. Não comprova morte do processo da aplicação principal, restauração após cold start, backend/mídia reais, outros dispositivos, falhas de renderização ou acessibilidade. APK-H21 continua aberto para essas validações.
- `QUALITY GATE`: `testDebugUnitTest`, `:app:lintDebug`, `:app:assembleDebug` e `:app:compileDebugAndroidTestKotlin` executados em conjunto; registrar o resultado consolidado ao concluir a execução.

### Plano — progresso do leitor após morte do processo

- [x] Adicionar instrumentação em fases para abrir livro de texto via ViewModel, salvar locator escopado por usuário/servidor e verificá-lo num ViewModel novo.
- [x] Adicionar harness host que instala os testes, executa a fase inicial, confirma `force-stop` do pacote alvo, inicia segunda fase e remove somente o locator com UUID do teste. Cada fase usa MockWebServer local novo.
- [x] Executar no perfil PHONE/API 35 com GPU NVIDIA; as três fases passaram e o AVD encerrou pelo wrapper.
- [x] Quality Bar: `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug` — `BUILD SUCCESSFUL`; 1.587 testes JVM, 0 falhas/erros/ignorados. `:feature:item-detail:compileDebugAndroidTestKotlin` passou.
- [x] Revisão adversarial encontrou falta de identidade exata do AVD e cleanup que só emitia aviso; harness agora exige o AVD solicitado e falha se cleanup UUID não concluir. A execução bifásica repetida após as correções passou, incluindo cleanup 1/1.
- [x] `git diff --check` passou; Graphify atualizado após teste, harness e documentação e consulta confirmou o novo teste e suas relações.

### TDD — restauração do progresso de leitura após morte do processo Android

- `INTENT`: `BookReaderProgressStore` grava Locators por conta/servidor no Preferences DataStore e `BookReaderViewModel.load` restaura Locators compatíveis; o teste de processo novo espera o mesmo Locator após `force-stop`; `TODO-APP.md` mantém a recuperação após encerramento real do processo Android em aberto.
- `RED`: não alegado para produção; o contrato de gravação/restauração já existia. O primeiro compile detectou somente erro no teste (`const val` com inicializador `String.repeat()`), corrigido antes de executar comportamento.
- `GREEN`: `BookReaderProgressProcessRestartTest` grava o locator do índice 3 (4º trecho) após carregar `text/plain`, aguarda leitura idêntica no DataStore e confirma isolamento de usuário/servidor numa segunda chamada de instrumentação, com ViewModel e MockWebServer novos.
- `PROCESS`: `tools/Test-AndroidBookReaderProgressRestart.ps1` executou três fases no PHONE/API 35; após a primeira, `am force-stop` encerrou `org.mulletaflix.feature.itemdetail.test` e o harness confirmou que o pacote não tinha processo ativo antes de iniciar a segunda. Fases: salvar 1/1, restauração 1/1, limpeza UUID 1/1.
- `REVISÃO ADVERSARIAL`: exigida correspondência entre `-AvdName` e o AVD ativo; cleanup UUID agora faz a execução falhar se não for comprovado. Reexecução posterior passou nas três fases.
- `QUALITY GATE`: `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug` — `BUILD SUCCESSFUL`; 1.587 testes JVM, 0 falhas/erros/ignorados. `:feature:item-detail:compileDebugAndroidTestKotlin` — `BUILD SUCCESSFUL`; `git diff --check` passou.
- `GRAPHIFY`: atualização após adicionar o teste, endurecer o harness e documentar a limitação; grafo final desta continuação com 10.859 nós, 28.387 arestas, 500 comunidades. A consulta confirmou relações entre `BookReaderScreen`, `BookReaderViewModel` e o fluxo de persistência.
- `LIMITAÇÃO`: o Android matou o alvo isolado gerado para instrumentação do módulo, não o `applicationId` `org.mulletaflix.android.debug`; prova a persistência real do DataStore e leitura por novo ViewModel, mas cold start/Activity/renderização integrados ao APK principal continuam pendentes. Sem mudança de produção, release, servidor ou portal.
- `DIAGNÓSTICO PENDENTE`: tentativa adicional de compor `BookReaderScreen` na fase de restauração manteve Locator e índice corretos, mas o Compose test host não expôs os nós dos trechos; três tentativas variaram espera de UI, visibilidade e presença sem sucesso. Extensão foi removida para não deixar teste vermelho; investigar a árvore semântica/host de Activity antes de retomar. Não prova defeito de produção.

### Quality gate global — retomada 2026-10-10

- `INTENT`: confirmar que o estado atual do APK compila e que a regressão reportada anteriormente — biblioteca de livros visível na Android TV — continua impedida durante carga, falha de atualização, modo offline e reconexão.
- `QUALITY GATE`: `./gradlew testDebugUnitTest :app:lintDebug :app:assembleDebug --no-daemon --console=plain` — `BUILD SUCCESSFUL`; relatórios JVM: 1.594 testes, 0 falhas, 0 erros, 0 ignorados (242 XML).
- `DEVICE`: `:feature:library:connectedDebugAndroidTest` filtrado para `LibraryOfflineReconnectFlowTest`, AVD `MulletaflixTvApi34`, perfil TV — 2/2, 0 falhas/erros/ignorados. `tools/with-emulator.ps1` confirmou QEMU na NVIDIA RTX 3050, validou o perfil e encerrou o AVD.
- `GRAFO`: consulta Graphify sobre a política HTTP/LAN localizou `AndroidCleartextPolicyIntegrationTest`, guards dos clientes, `CleartextTrafficPolicy` e o pendente APK-H12. Restringir globalmente cleartext sem quebrar LAN dinâmica continua sem solução estática segura; nenhum comportamento foi alterado nesta rodada.
- `LIMITAÇÕES`: nenhum teste contra servidor/catálogo real ou TalkBack manual; build é Debug. `git diff --check` passou com avisos de conversão LF→CRLF nos arquivos já modificados. Sem bump, APK de produção, release, portal ou mudança no servidor.

### Homologação instrumentada — ocultação de Livros em Android TV — 2026-10-10

- `INTENT`: livros e audiolivros não devem aparecer para quem usa a versão Android TV, inclusive ao navegar pela Home, Biblioteca e Busca.
- `DEVICE`: `:feature:home:connectedDebugAndroidTest`, `:feature:library:connectedDebugAndroidTest` e `:feature:search:connectedDebugAndroidTest`, em `MulletaflixTvApi34`, perfil TV — respectivamente 36/36, 36/36 e 17/17; zero falhas, erros ou ignorados. Testes incluem ausência da biblioteca Books, títulos Book/Audiobook em busca e ocultação após offline/reconexão.
- `AMBIENTE`: wrapper verificou QEMU na NVIDIA RTX 3050 e fechou o AVD ao completar.
- `LIMITAÇÕES`: feed de teste/instrumentado, não servidor e catálogo reais; nenhum comportamento ou versão alterados. Sem APK de produção ou publicação.
