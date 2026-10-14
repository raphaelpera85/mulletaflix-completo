# Plano de TDD do MulletaFlix Android

## RED-GREEN — autenticação e navegação ao trocar servidor pelo Perfil — 2026-10-14

- `INTENT`: code removes only `SERVER_SELECTION` after explicit login, even when switching from Profile; the regression test expects Back to exit instead of restoring the previous Profile; `DEVELOPMENT-GUIDE.md` requires the server-switch flow and session stack to be tested without restoring the previous account/server.
- `RED`: a diagnostic PHONE/API 35 run confirmed the login POST and new Home, then failed the Back assertion: the old Home/Profile remained below the new Home. The run also exposed that the runner option for one method had executed all 10 class tests; only the target scenario failed (9 passed, 1 failed).
- `GREEN`: `MulletaFlixNavHost` now remembers when a switch starts specifically in Profile and clears the prior Home stack on successful login only for that path. Normal login and deep-link routing retain their existing `SERVER_SELECTION` behavior. Focused scenario passed 1/1; full `MainActivitySessionNavigationTest` passed 11/11 in PHONE/API 35, including cancellation → logout → independent login.
- `QUALITY GATE`: `testDebugUnitTest` produced 1,762 tests across 255 XML reports, 0 failures/errors/ignored; `:feature:auth:lintDebug`, `:app:lintDebug`, `:app:assembleDebug`, `:feature:auth:compileDebugAndroidTestKotlin` and `:app:compileDebugAndroidTestKotlin` passed. NVIDIA GPU verified by the emulator wrapper; temporary AVD closed after each run.
- `LIMITES`: MockWebServer and AVD only; no real media server/device. APK-H02 remains in validation and formal acceptance remains pending. No version bump, release, portal change, server change, commit or push.
- `TWINS`: searched server-switch success routing and Profile callbacks; one matching success callback in `MulletaFlixNavHost.kt`, handled by the change. Deep-link mismatch separately clears Home before auth and retains its existing behavior.
- `REVIEW`: Cavecrew reviewer raised a possible stale-origin marker after abandoning a switch. Added `canceledProfileServerSwitchDoesNotAffectLoginAfterLogout`; it passed before any marker-reset change, so the proposed stale-marker defect was not reproduced and no extra production change was made. The initial attempt failed only because the test waited for an off-screen profile option to be displayed before scrolling; changing that setup wait to `assertExists` let the UI scroll continue.
- `ESCOPO`: somente APK, teste e documentação Android. Sem mudança do servidor, portal, versão ou release; APK-H02 permanece em validação e aceite formal pendente.

## RED-GREEN — validação e normalização de identificadores em ToggleFavoriteUseCase e TogglePlayedUseCase — 2026-10-14

- `INTENT`: `ToggleFavoriteUseCase` e `TogglePlayedUseCase` devem validar identificadores de usuário e de mídia não-vazios/não-brancos (`IllegalArgumentException`), normalizar espaços (`trim()`), delegar as operações corretas ao `MediaRepository` e retornar o novo estado booleano do item.
- `RED`: `:domain:testDebugUnitTest --tests "org.mulletaflix.domain.usecase.UseCaseTest"` falhou 2/40 antes da implementação (`ToggleFavoriteUseCase rejects blank userId or itemId with IllegalArgumentException` e `TogglePlayedUseCase rejects blank userId or itemId with IllegalArgumentException` falharam porque os use cases delegavam sem validar).
- `GREEN`: `ToggleFavoriteUseCase.kt` e `TogglePlayedUseCase.kt` atualizados com sanitização de `cleanUser` e `cleanItem`. Se vazios após trim, retornam `Result.failure(IllegalArgumentException)`. Identificadores limpos são repassados ao repositório. 40/40 testes de `UseCaseTest` passaram.
- `QUALITY GATE`: `testDebugUnitTest` global totalizou 1.756 testes JVM em 254 relatórios XML, 0 falhas/erros/ignorados. `:domain:lintDebug`, `:app:lintDebug`, `:app:assembleDebug`, compilação AndroidTest de `domain` e `app` passaram.
- `LIMITES`: testes unitários em JVM com repositório fake isolado; não cobrem concorrência remota ou validação contra o backend real.
- `ESCOPO`: somente APK e testes no módulo `domain`. Sem bump, release, commit ou push.

## RED-GREEN — validação, normalização e delegação em GetFavoriteItemsUseCase — 2026-10-14

- `INTENT`: `GetFavoriteItemsUseCase` deve validar parâmetros de entrada (`userId` não-vazio, `startIndex >= 0`, `limit > 0`), normalizar `userId` e `searchTerm` com `trim()`, converter buscas vazias em `null`, delegar filtros de favoritos ao `MediaRepository` e propagar sucessos ou falhas.
- `RED`: `:domain:testDebugUnitTest --tests "org.mulletaflix.domain.usecase.GetFavoriteItemsUseCaseTest"` falhou 1/5 antes da implementação (`trims userId and searchTerm before passing to repository` falhou esperando `"user-trimmed"` mas recebendo `"  user-trimmed  "`).
- `GREEN`: `GetFavoriteItemsUseCase.kt` atualizado para passar `userId = userId.trim()`. 5 testes unitários passaram 5/5.
- `QUALITY GATE`: `testDebugUnitTest` global totalizou 1.754 testes JVM em 254 relatórios XML, 0 falhas/erros/ignorados. `:domain:lintDebug`, `:app:lintDebug`, `:app:assembleDebug`, compilação AndroidTest de `domain` e `app` passaram.
- `LIMITES`: teste unitário da camada de domínio com repositório fake isolado; não substitui o teste integrado do repositório HTTP com MockWebServer ou servidor real.
- `ESCOPO`: somente APK e testes no módulo `domain`. Sem bump, release, commit ou push.

## RED-GREEN — normalização de whitespace no roteamento de deep links — 2026-10-13


- `INTENT`: `DeepLinkNavigationPolicy` deve normalizar identificadores de mídia ao avaliar se o item solicitado já está visível (`shouldMarkMediaDeepLinkHandled`), se deve navegar (`shouldNavigateToMediaDeepLink`), e ao enfileirar rota após login (`routesAfterSuccessfulLogin`), impedindo navegações duplicadas ou rotas com espaços em branco espúrios.
- `RED`: `:app:testDebugUnitTest --tests "org.mulletaflix.android.navigation.DeepLinkNavigationPolicyTest"` falhou 3/25 antes da implementação: asserções de correspondência com espaços ao redor do item ID falharam e `routesAfterSuccessfulLogin` gerou rota `"detail/  movie-123  \t"`.
- `GREEN`: `cleanTarget = targetItemId?.trim().orEmpty()`, comparações utilizam `currentItemId?.trim() == cleanTarget`, e `routesAfterSuccessfulLogin` aplica `pendingItemId?.trim()`. Suíte focal passou 25/25.
- `QUALITY GATE`: `testDebugUnitTest` global totalizou 1.749 testes JVM em 253 relatórios XML, 0 falhas/erros/ignorados. `:app:lintDebug`, `:app:assembleDebug` e `:app:compileDebugAndroidTestKotlin` concluídos com sucesso.
- `LIMITES`: testes unitários focados nas políticas puras de navegação; validação em Activity real de back stack depende de testes instrumentados e hardware/dispositivos.
- `ESCOPO`: somente APK e testes no módulo `app`. Sem bump, release, commit ou push.

## RED-GREEN — validação e normalização de versão em CheckAppUpdateUseCase — 2026-10-13


- `INTENT`: `CheckAppUpdateUseCase` deve validar versão não vazia/branca antes de consultar o repositório, normalizar espaços e prefixos `v`/`V`, e repassar adequadamente os resultados de atualização disponível, indisponível e falhas de rede.
- `RED`: `:domain:testDebugUnitTest --tests "org.mulletaflix.domain.usecase.CheckAppUpdateUseCaseTest"` falhou 2/5 antes da implementação. `normalizes version string` falhou esperando `"1.3.5"` e recebendo `"  v1.3.5  "`; `fails fast with illegal argument exception when version is empty or blank` falhou por delegar a chamada em vez de rejeitar com `IllegalArgumentException`.
- `GREEN`: `CheckAppUpdateUseCase` valida `trimmed.isNotEmpty()` e remove prefixos `v`/`V`. Se vazia após normalização, retorna `Result.failure(IllegalArgumentException)`. Versão válida e limpa é repassada para `appUpdateRepository.checkForUpdate(cleanVersion)`. 5 testes unitários passaram 5/5.
- `QUALITY GATE`: `:domain:lintDebug :domain:testDebugUnitTest` concluídos com sucesso. Suíte unitária global `testDebugUnitTest`: 1.746 testes JVM em 253 relatórios XML, 0 falhas/erros/ignorados. `:app:lintDebug`, `:app:assembleDebug`, `:domain:compileDebugAndroidTestKotlin` e `:app:compileDebugAndroidTestKotlin` passaram sem erros.
- `LIMITES`: teste unitário da camada de domínio com repositório fake; não substitui o teste de integração HTTP nem consulta externa ao GitHub.
- `ESCOPO`: somente APK e testes no módulo `domain`. Sem bump, release, commit ou push.

## Verificação instrumentada de callbacks de conectividade em API 24 e 35 — 2026-10-13


- **Escopo:** cobertura de teste, sem alterar implementação do APK. O teste novo deriva a versão de `Build.VERSION.SDK_INT`; em API 24, confirma que `onAvailable` agenda lookup legacy de capabilities e medição antes de publicar estado. Em API 35, confirma o caminho de capabilities entregues.
- **PHONE/API 35:** `ConnectivityNetworkCallbackTest` passou 3/3; GPU NVIDIA RTX 3050 verificada e AVD encerrado pelo wrapper.
- **PHONE/API 24:** `ConnectivityNetworkCallbackTest` passou 3/3 na imagem Google APIs x86_64; GPU NVIDIA RTX 3050 verificada e AVD encerrado pelo wrapper. Relatório confirma zero falhas, erros ou ignorados.
- **Limites:** o callback/fallback é exercitado com `ConnectivityManager` e capabilities da rede ativa do AVD, mas o scheduler legado é capturado por callback de teste; isso não prova roteador LAN, múltiplas redes, cancelamento/desregistro do Flow ou aparelho físico. API 25 não executada.
- **TDD:** não houve mudança de produção nem correção atribuída a RED/GREEN nesta rodada; a mudança aumenta caracterização por API e torna observável a ramificação antiga.

## RED-GREEN — remover falso negativo do harness de cold start do leitor — 2026-10-13

- INTENT: code now accepts a cold-start run without `/BookReader/Status`; the failing check expects that run to pass when item metadata and `/BookReader/Epub` load without the optional poll; `TODO-APP.md` and the cold-start investigation in `TDD-PLAN.md` state the status poll is best-effort and may be canceled when content arrives first.
- `RED`: `Invoke-Pester -Path .\tools\Test-AndroidBookMainProcessColdStart.Tests.ps1` failed 1/1 at the contract assertion because the host's required-route list included `/BookReader/Status`.
- `GREEN`: removed the status route from the host-required set and updated the success message. The same Pester test passed 1/1; PowerShell parser and `node --check tools/BookReaderColdStartFixture.mjs` passed.
- `INTEGRAÇÃO`: `tools\with-emulator.ps1` opened disposable `MulletaflixColdStartTempApi35_20261011` on PHONE/API 35; GPU NVIDIA verified. `tools\Test-AndroidBookMainProcessColdStart.ps1 -AvdName MulletaflixColdStartTempApi35_20261011 -Port 5562` installed Debug/test APKs, captured session 1/1, prepared fixture 1/1, killed PID 3555, relaunched deep link and rendered the book in PID 3644 (1/1), restored the prior session (1/1). Host found only required item and EPUB routes. Wrapper closed the temporary AVD; ADB empty.
- `LIMITES`: synthetic server/content only; no physical device, production media, Back/D-pad, broader Android quality gate or release validation yet. APK-H21 and formal acceptance remain open. No server change or production release.

## RED-GREEN — responder ao fechamento remoto do WebSocket SyncPlay — 2026-10-13

- `INTENT`: encerrar com segurança o WebSocket quando o servidor inicia o close handshake; emitir `Disconnected`, reconectar enquanto a sala continua ativa e cancelar o retry ao sair.
- `RED`: o cenário com close remoto que não inclui status falhou esperando `Connected → Disconnected → Connected`; antes do fallback, o peer TCP registrou EOF sem receber quadro de resposta, pois OkHttp representa esse close como sentinela 1005 e rejeita o código reservado. O teste de fechamento remoto padrão também expirou antes da implementação de `onClosing` responder.
- `GREEN`: `listenerFor.onClosing` confirma geração/grupo/socket atuais e responde com mesmo código/razão, exceto a sentinela 1005, que vira status válido 1000 sem motivo. O peer TCP verifica máscara e código no quadro de resposta; MockWebServer confirma falha HTTP 503 (`Disconnected` → `Connected`) e close padrão (`Connected` → `Disconnected` → `Connected`). O teste de `stop()` aguarda ausência do job de conexão inicial e presença do job ativo de retry antes de sair; confirma uma única requisição após o backoff.
- `FOCAL`: `SyncPlayRealtimeClientTest` passou 8/8; `SyncPlayCleartextPolicyTest` e `SyncPlayRealtimeGroupSwitchTest` passaram no gate. XML confirma os cenários de failure, remote close com/sem status, stop e troca de grupo sem falhas.
- `QUALITY GATE`: `testDebugUnitTest :core:api:lintDebug :app:lintDebug :app:assembleDebug :core:api:compileDebugAndroidTestKotlin :app:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL`; agregação direta dos XMLs: 1.740 testes JVM, 252 relatórios, 0 falhas/erros/ignorados.
- `EVIDÊNCIA EXTERNA`: a documentação do OkHttp 5.4.0 define `onClosing` como indicação do peer remoto de que não enviará mais mensagens e `onClosed` como conexão liberada após ambos os peers indicarem encerramento ([WebSocketListener](https://raw.githubusercontent.com/square/okhttp/parent-5.4.0/okhttp/src/commonJvmAndroid/kotlin/okhttp3/WebSocketListener.kt)).
- `LIMITES`: MockWebServer/JVM, sem aparelho físico nem servidor de mídia real. A homologação formal permanece 11/21 áreas locais concluídas, 1 em validação, 9 pendentes e aceite `pending`. Sem APK/release, bump ou mudança do servidor.

## RED-GREEN — isolar a checagem externa de atualização nos testes de navegação — 2026-10-13

- `INTENT`: chamadas de navegação devem testar Activity/servidores sintéticos sem consultar GitHub; APK de produção deve continuar verificando atualizações no retorno ao primeiro plano.
- `RED`: novo `AppUpdatePolicyTest.test-only suppression disables update checks only in debug builds` falhou por asserção em `AppUpdatePolicyTest.kt:13` com a política provisória sempre verdadeira (5 testes, 1 falha). A primeira execução focal teve quoting inválido do filtro (`Task 'suppression\\' not found`) e foi descartada como erro de comando.
- `GREEN`: `shouldCheckForAppUpdates` permite suprimir somente quando `BuildConfig.DEBUG` e a flag do teste estão presentes. `MainActivitySessionNavigationTest.launchMainActivity` injeta a flag nas quatro rotas de Activity do fixture; build de produção ignora o opt-out. Política cobre debug suprimido, debug normal e produção com flag.
- `INTEGRAÇÃO`: `MainActivitySessionNavigationTest` passou 9/9 PHONE/API 35; XML novo registra 0 falhas/erros/ignorados. Wrapper confirmou QEMU NVIDIA, encerrou AVD e ADB ficou vazio.
- `QUALITY GATE`: além do teste de política, o follow-up de lifecycle abaixo concluiu o quality gate global (`testDebugUnitTest :app:lintDebug :app:assembleDebug :app:compileDebugAndroidTestKotlin`) com 1.735 testes JVM/252 XML e zero falhas/erros/ignorados. Após a revisão independente apontar que o output padrão é sobrescrito, a suíte de navegação foi reexecutada; XML preservado em `validation-evidence/2026-10-13/MainActivitySessionNavigationTest-PHONE-API35.xml`.
- `TWINS`: busca de `AppUpdateCheckEffect(` e de todos os `ActivityScenario.launch(` confirmou um único callsite de produção na MainActivity e que todos os lançamentos da classe de navegação passam pelo helper com a flag.
- `LIMITES`: nenhuma consulta de rede do atualizador é iniciada nesse cenário por controle de fluxo; não instrumentamos tráfego de outros componentes. Não altera aceitação formal, runtime de produção, versão ou release.

## RED-GREEN — impedir iniciação duplicada do Quick Connect — 2026-10-13

- `INTENT`: taps repetidos durante iniciação ou espera de autorização não devem substituir PIN ativo nem criar outra solicitação no servidor.
- `RED`: `AuthViewModelTest.duplicate quick connect initiation while request is in flight is ignored` falhou antes da implementação; a execução da classe reportou a falha no teste, antes da guarda de estado.
- `GREEN`: `initiateQuickConnect()` agora retorna quando `isLoading` ou `isWaitingForQuickConnect` já estão ativos e define `isLoading=true` sincronamente antes de lançar a coroutine de rede. `AuthViewModelTest` cobre duas chamadas antes do despacho, uma duplicata após o repositório iniciar e enquanto ele aguarda, e outra com o PIN visível; uma API call e o PIN original são preservados.
- `INTEGRAÇÃO`: `MainActivitySessionNavigationTest.quickConnectAuthorizationPersistsSessionAndOpensHome` passou 1/1 via `:app:connectedDebugAndroidTest`, PHONE/API 35, usando MockWebServer; XML confirma zero falhas/erros/ignorados. Wrapper confirmou GPU NVIDIA e encerrou o AVD.
- `QUALITY GATE`: `testDebugUnitTest :feature:auth:lintDebug :app:lintDebug :app:assembleDebug :app:compileDebugAndroidTestKotlin` — `BUILD SUCCESSFUL`; 1.732 testes JVM em 252 XMLs, zero falhas/erros/ignorados.
- `TWINS`: busca por `initiateQuickConnect()` e `cancelQuickConnectPolling()` confirmou uma única implementação de produção em `AuthViewModel`; os demais resultados são testes/stubs de `AuthRepository`, não outros pontos de iniciação. Login/cadastro têm guards próprios.
- `LIMITES`: a correção impede duplicação iniciada no cliente; não demonstra disponibilidade nem autorização contra servidor real. Sem mudança de contrato, UI, release, portal ou servidor.

## RED-GREEN — feedback e retry local da persistência do rascunho — 2026-10-13

- `INTENT`: uma falha de armazenamento do rascunho deve ser visível e recuperável na tela, sem apagar os campos nem reenviar solicitação ao servidor.
- `TWINS`: busca de feedback inline/retry em formulários Android não encontrou outro formulário com persistência privada por conta/servidor; o rascunho da Home é o único caminho equivalente.
- `RED`: `HomeMediaRequestDraftRestorationTest.failedDraftSaveShowsLocalRetryAndDoesNotSubmitRequest` falhou como esperado por comportamento: ComposeTimeoutException após 5 s esperando a mensagem de falha, pois o erro local não era exposto na interface. Na mesma execução, 3 testes existentes passaram, 1 falhou, sem erros ou ignorados.
- `GREEN`: `HomeState` agora expõe IDLE/SAVING/SAVED/FAILED, sempre condicionando atualizações ao hash da conta/servidor e ao rascunho atual. A tela anuncia a transição por região acessível polite; quando FAILED, apresenta ação Material de pelo menos 48 dp para retry local do mesmo escopo. O retry não chama `requestMedia` nem envia POST.
- `TESTES`: teste JVM focal comprova FAILED → SAVED, segunda persistência concluída e zero chamadas de solicitação. `HomeMediaRequestDraftRestorationTest` passou 4/4 em PHONE/API 35 (`MulletaflixCodexPhoneApi35_20261012`), TABLET/API 35 (`MulletaflixTabletApi35`) e TV/API 34 (`MulletaflixTvApi34`); o teste também afirma `SemanticsProperties.LiveRegion = LiveRegionMode.Polite`, confirma mensagem e retry e zero POST. Os wrappers encerraram os AVDs e verificaram a GPU NVIDIA.
- `QUALITY GATE`: `testDebugUnitTest :feature:home:lintDebug :app:lintDebug :app:assembleDebug :feature:home:compileDebugAndroidTestKotlin --no-daemon --console=plain` concluiu `BUILD SUCCESSFUL`; XML JVM: 1.727 testes/252 relatórios, 0 falhas/erros/ignorados, 871 tarefas acionáveis. `git diff --check` passou após a revisão final.
- `LIMITES`: falha de armazenamento sintética; TalkBack físico, armazenamento cheio real e aparelho/servidor reais não foram validados. A matriz formal permanece 11/21 locais, 1 em validação e 9 pendentes. Sem bump, release, portal ou mudança no servidor. Delegação Cavecrew indisponível por limite de threads; revisão local adversarial do comportamento e do diff executada.

## RED-GREEN — preservar rascunho ao trocar servidor após falha local — 2026-10-13

- `INTENT`: uma falha ao salvar o formulário de solicitação não pode apagar a única cópia em memória nem expor esse conteúdo ao trocar de servidor; ao voltar ao escopo original, o rascunho deve ser restaurado e a persistência pode ser tentada novamente.
- `RED`: `HomeViewModelTest.failed draft remains isolated and restores after switching servers` falhou pelo comportamento esperado: após alternar A → B → A, o título de A voltou vazio (`expected draftA, was empty`).
- `GREEN`: guardar snapshots pendentes por hash de conta/servidor, priorizar o snapshot do escopo autenticado ao restaurar e serializar gravações; a tela de B recebe apenas o rascunho de B. Um retorno ao escopo A restaura seus campos e tenta salvá-los. Não há reenvio do POST.
- `TESTES`: teste focal `:feature:home:testDebugUnitTest --tests "org.mulletaflix.feature.home.HomeViewModelTest.failed draft remains isolated and restores after switching servers"` passou 1/1; suíte do módulo Home passou. Quality gate completo passou: 1.726 testes JVM em 252 XMLs, 0 falhas/erros/ignorados; lint dos módulos common/domain/data/home/app, `assembleDebug` e compilação AndroidTest de data/home/app — `BUILD SUCCESSFUL` (897 tarefas acionáveis). `git diff --check` passou.
- `LIMITES`: falha de DataStore simulada por `IOException`; sem hardware físico, servidor real ou instrumentação adicional nesta alteração lógica. O retry é disparado ao retornar ao escopo ou por flush/edição, não por loop automático. Sem release, mudança de versão, portal ou servidor. Revisão de subagente não disponível por limite de threads; revisão local do diff executada.

## RED-GREEN — retry seguro de falha ao salvar rascunho — 2026-10-13

- `INTENT`: erro transitório ao persistir campos locais não deve apagar a única cópia pendente nem afetar o envio da solicitação ao servidor.
- `RED`: `HomeViewModelTest.failed draft persistence remains pending for a later local retry` executou e falhou por comportamento: após o primeiro `save` lançar `IOException`, `flushMediaRequestDraft()` não tentou salvar novamente (`expected 2, was 1`). A falha inicial de compilação por imports ausentes foi corrigida antes e não conta como RED.
- `GREEN`: persistência retorna sucesso/falha; debounce e flush removem a referência pendente somente após `MediaRequestDraftStore.save` concluir. Exceções de cancelamento continuam propagadas; demais falhas locais são tratadas sem crash e deixam o rascunho apto a um próximo flush/edição. Não há retry de POST.
- `TESTES`: caso focal passou 1/1; suíte completa `:feature:home:testDebugUnitTest` passou. Gate `testDebugUnitTest :feature:home:lintDebug :app:lintDebug :app:assembleDebug :feature:home:compileDebugAndroidTestKotlin :app:compileDebugAndroidTestKotlin` concluiu `BUILD SUCCESSFUL`; relatórios finais: 1.725 testes/252 XMLs, zero falhas/erros/ignorados.
- `LIMITE`: retry exige novo flush/edição; não é loop automático. O teste injeta uma falha sintética do armazenamento e não valida indisponibilidade permanente nem hardware físico. Sem mudança de versão/release/portal/servidor.

## RED-GREEN — rascunho durável de solicitação de mídia — 2026-10-13

- `INTENT`: manter título, tipo, ano e observações locais, privados à conta/servidor autenticados; recuperar os campos após recriar a tela/processo sem registrar estado de envio nem reenviar o pedido.
- `TWINS`: busca `rememberSaveable` de campos de formulário localizou este formulário na Home; as outras ocorrências encontradas guardam estado de filtros, busca, leitor e preferências visuais, não formulários autenticados equivalentes.
- `RED`: `:feature:home:testDebugUnitTest --tests 'org.mulletaflix.feature.home.HomeViewModelTest.media request draft is restored for the authenticated account and server'` compilou e falhou por comportamento: após a emissão da sessão, `mediaRequestDraftLoaded` continuava falso e o rascunho do store não aparecia no estado.
- `GREEN`: escopo SHA-256 dedicado à identidade servidor/usuário (sem persistir URL, token, deviceId ou ID bruto); DataStore privado por escopo, com máximo de 8 contas/servidores retidos; campos limitados a 200/40/4/1.000 caracteres. Home hidrata o diálogo, agrupa gravações durante digitação por 250 ms e força flush ao sair da Home/ir para segundo plano; sucesso limpa os campos, erro mantém o rascunho. A seleção por sugestão continua removendo ano antigo quando a sugestão não fornece ano.
- `TESTES`: suíte `HomeViewModelTest` cobre restauração, alternância/isolamento de conta, flush antes do debounce e sucesso sem reenvio automático; `FeedbackRequestSessionTest` cobre hash estável entre aliases, isolamento e ausência de credenciais no hash. `MediaRequestDraftStorePersistenceTest` passou 2/2 em PHONE/API 35: novo DataStore abriu o mesmo arquivo, separou duas contas, limpou somente o escopo pedido e removeu o mais antigo acima de 8. `HomeMediaRequestDraftRestorationTest` passou 3/3 em PHONE/API 35, TABLET/API 35 e TV/API 34; XML atual da TV confirma 3 testes e zero falhas/erros/ignorados.
- `QUALITY GATE`: `testDebugUnitTest :core:common:lintDebug :domain:lintDebug :data:lintDebug :feature:home:lintDebug :app:lintDebug :app:assembleDebug :data:compileDebugAndroidTestKotlin :feature:home:compileDebugAndroidTestKotlin :app:compileDebugAndroidTestKotlin --console=plain` — `BUILD SUCCESSFUL`, 897 tarefas; XML JVM totaliza 1.724 testes em 252 relatórios, 0 falhas/erros/ignorados. Os wrappers confirmaram NVIDIA e encerraram cada AVD.
- `RETESTE REAL`: `testDebugUnitTest --rerun-tasks --no-daemon --console=plain` executou novamente os testes unitários, não apenas tarefas `UP-TO-DATE`: `BUILD SUCCESSFUL`, 470 tarefas executadas; leitura dos XMLs ao terminar confirmou 1.724 testes em 252 suítes, 0 falhas/erros/ignorados. Warnings de depreciação/opt-in existentes não interromperam a compilação.
- `GRAFO`: `graphify update .` foi executado após as alterações no código, testes e documentação; `graphify explain MediaRequestDraftStoreImpl`, `HomeViewModel` e `HomeMediaRequestDraftRestorationTest` confirmou caminhos da implementação, DI e testes. Métricas e hashes atuais ficam na nota `MulletaFlix — Grafo de arquitetura` do vault; o grafo é índice, não evidência de runtime.
- `LIMITES`: hardware físico e uso com servidor/conteúdo reais seguem pendentes. A morte real do processo em AVD foi coberta no registro de continuação logo abaixo. TABLET exigiu segunda tentativa com timeout de boot ampliado para 8 min; passou. Sem release, bump, publicação, servidor ou portal. Revisão independente Cavecrew indisponível por limite de threads; revisão adversarial local feita.

## GREEN — restauração do rascunho após morte do processo principal — 2026-10-13

- `INTENT`: provar a persistência em processo Android novo, além de recriação do DataStore/ViewModel e restauração Compose.
- `HARNESS`: as fases de instrumentação encerravam a Activity principal ao retornarem; por isso a preparação grava uma fixture sintética diretamente no `MediaRequestDraftStoreImpl`, e o host é responsável por abrir MainActivity, capturar PID e executar `am force-stop` entre fases. Digitação e edição continuam sob o teste Compose `HomeMediaRequestDraftRestorationTest`.
- `RESULTADO`: `enterMediaRequestDraftBeforeMainProcessRestart` passou 1/1; host confirmou parada do PID 3843; `mediaRequestDraftIsRestoredAfterMainProcessRestart` passou 1/1 e a tela exibiu título, tipo, ano e observações no PID 3921. Evidência da execução: `tools/Test-AndroidBookMainProcessColdStart.ps1 -Scenario MediaRequestDraft`.
- `LIMPEZA`: `restoreOriginalSessionAfterColdStartFixture` passou 1/1; `cleanupOnlyTheHostProcessRestartDraft` passou 1/1 e conferiu escopo vazio. AVD temporária `MulletaflixColdStartTempApi35_20261011` iniciada/fechada pelo wrapper; GPU QEMU NVIDIA RTX 3050 verificada; ADB sem dispositivos ao terminar.
- `LIMITES`: sessão e conteúdo foram sintéticos em PHONE/API 35; nenhuma entrada de teclado foi simulada neste harness específico. Hardware, servidor/conteúdo reais, reinício do aparelho, TalkBack e homologação formal continuam pendentes. O resultado não muda versão, release, portal ou servidor.

## Reteste instrumentado do leitor DOCX — 2026-10-13

- `VERIFICAÇÃO`: a implementação do sumário DOCX e seu teste Compose já existiam; esta rodada não altera comportamento nem declara um RED novo.
- `GREEN OBSERVADO`: `DocxBookReaderIntegrationTest` passou 2/2 em PHONE/API 35: texto DOCX renderizado e salto de capítulo pelo sumário com locator persistido/restaurado no documento.
- `AMBIENTE`: `tools/with-emulator.ps1` abriu o AVD temporário `MulletaflixCodexPhoneApi35_20261012`; o wrapper confirmou o PID QEMU na NVIDIA RTX 3050 e encerrou o AVD ao terminar. AVD reservada `MulletaflixApi35` não foi usada; ADB ficou sem dispositivos conectados.
- `LIMITES`: não prova DOCX real do servidor, acessibilidade manual nem UX TABLET/TV. Evidência completa no XML `feature/item-detail/build/outputs/androidTest-results/connected/debug/TEST-MulletaflixCodexPhoneApi35_20261012(AVD) - 15.xml`.

## RED-GREEN — fallback LAN para servidor autenticado — 2026-10-13

- `INTENT`: a recuperação após duas falhas LAN não pode substituir a sessão por um endpoint público fixo que talvez pertença a outro servidor.
- `RED`: com a nova política ainda sem validação de identidade, `LanServerRecoveryPolicyTest` executou 26 casos; 2 falharam exatamente para endpoint salvo de outro `serverId` e ausência de `serverId` autenticado. Gradle: `BUILD FAILED`, 2 falhas esperadas.
- `GREEN`: o fallback exige `serverId` autenticado e escolhe endpoint remoto salvo, alcançável e com identidade correspondente. Continua exigindo endpoint atual local e duas falhas consecutivas. Sem candidato seguro, não troca URL. `LanServerRecoveryPolicyTest`: 26 testes, 0 falhas/erros/ignorados.
- `INTEGRAÇÃO`: removido o `lanServerRecovery.stop()` temporário do AndroidTest cross-server para exercitar o worker real. Em 2026-10-13, o teste focal passou 1/1 e a classe completa `MainActivitySessionNavigationTest` passou 9/9 em PHONE/API 35, com XML novo. O caso confirmou autenticação e GET do item no servidor B, retorno à Home e zero requests ao servidor A. `LanServerRecoveryPolicyTest` passou novamente 26/26.
- `LIMITE`: uma classe completa verde não explica nem elimina a falha histórica 8/9; preservar tarefa de repetição/estabilidade. MockWebServer e AVD não comprovam LAN física nem homologação formal.
- `ESCOPO`: política/testes LAN, AndroidTest Activity e documentação APK. Não muda homologação, versão, release, portal ou servidor.

## RED-GREEN — reduzir o custo de reconstrução textual KF8 — 2026-10-12

- `INTENT`: manter a reconstrução textual de fragments KF8 real sem cópias quadráticas e evitar renderizar o HTML inteiro em `Spannable` antes de convertê-lo em texto simples.
- `RED`: o teste Robolectric da fixture AZW3 real atravessando `BookReaderViewModel` estourava o timeout de 1 minuto (`UncompletedCoroutinesError`); dump de thread localizou a espera no parse HTML/Spannable. A reconstrução antiga também copiava prefixos repetidamente ao inserir fragments. RED observado foi o teste real não completar no limite anterior; não foi criada expectativa artificial sobre algoritmo/complexidade.
- `GREEN`: `Kf8SegmentedByteBuffer` usa split/merge em árvore treap determinística para inserir fragments e achata o conteúdo uma única vez. O parser HTML agora percorre texto/tags linearmente, decodifica entidades e mantém marcadores de títulos e quebras necessários ao leitor plain-text.
- `TESTES`: testes cobrem inserção no meio, offsets, início/fim e mesmo offset; testes HTML preservam entidade numérica Unicode, parágrafos e títulos; integração Robolectric usa resposta HTTP mockada com fixture AZW3 real e confirma texto no estado final. A fixture ViewModel concluiu em 24,0 s nesta medição, contra 73,3 s na observação anterior (≈3×); é medição de host, não garantia em dispositivo.
- `TWINS`: searched `copyOfRange\(0, insertion\)|copyOfRange\(insertion` em `feature/item-detail/src/main` — 0 sites de cópia incremental remanescentes; inserções estão encapsuladas no buffer usado por `Kf8HtmlReconstructor`.
- `QUALITY GATE`: `gradlew.bat testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin :app:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL` em 3m57s; 250 XML, 1.701 testes JVM, 0 falhas/erros/ignorados. Lint do módulo/app, assemble Debug e Kotlin AndroidTest do módulo/app aprovados. `git diff --check` limpo.
- `GRAPHIFY`: `graphify update . --no-cluster` — 11.601 nós/30.740 arestas; `explain Kf8SegmentedByteBuffer` confirmou chamada pela reconstrução e três testes; `explain renderHtmlText` confirmou relação com `htmlToBookDocument` e scanner. Export HTML agregado limitado: 89 comunidades, 0 arestas entre comunidades; sidecar reconstruído por estar desatualizado. CLI 0.9.79 (skill local 0.9.84), 84 arquivos não classificados, extração parcial conhecida de `TvHomeRefreshIntegrationTest.kt` (linha 53) e 29 fontes sem símbolos.
- `LIMITES`: HTML vira texto simples; CSS, imagens e links KF8 não são renderizados. Sem instrumentação nesta tarefa; AVD `MulletaflixApi35` preservada.
- `ESCOPO`: somente APK e documentação. Sem servidor, portal, versão, release, publicação, commit ou push.

## RED-GREEN — token de sessão protegido em repouso — Android — 2026-10-11

- `INTENT`: o app persistia o token em texto legível no Preferences DataStore; o teste exige que o valor legado desapareça, que só reste ciphertext versionado e que a sessão seja restaurável; a documentação Android recomenda Android Keystore para chaves e marca `EncryptedSharedPreferences` como obsoleto.
- `ESCOPO`: `data/SessionRepositoryImpl`, cifragem/migração de token, testes instrumentados, TDD/TODO e Graphify; APK somente, sem servidor, portal, versão, release, commit ou push.
- `RED`: teste instrumentado de persistência falhou antes da implementação porque o token aparecia como valor legível na sessão persistida (`AssertionError: token não pode ficar legível no DataStore de sessão`).
- `GREEN`: AES-GCM/NoPadding, IV aleatório de 96 bits e tag de 128 bits; chave AES-256 não exportável no Android Keystore; envelope versionado `v1:` com Base64 e AAD vinculada ao campo do app. O ciphertext fica junto aos metadados na mesma transação DataStore, garantindo troca atômica de conta e evitando associação de token antigo a identidade nova. Tokens legados migram atomicamente; ciphertext inválido usa token legado recuperável quando presente, senão limpa identidade/autenticação preservando URL e dispositivo. Logout apaga token claro/cifrado e identidade.
- `ANDROIDTEST`: `:data:connectedDebugAndroidTest` via `tools/with-emulator.ps1`, AVD temporário PHONE/API 35 acelerado pela NVIDIA RTX 3050: 8 testes, 0 falhas/erros/ignorados. Cobertura inclui valor cifrado e restauração, migração legada, ciphertext corrompido com e sem fallback legado, login em outra conta, logout e metadados persistentes.
- `QUALITY BAR`: `testDebugUnitTest :data:lintDebug :app:lintDebug :app:assembleDebug :data:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL`; 248 suítes/1.675 testes JVM, 0 falhas/erros/ignorados; lint, APK Debug e compilação AndroidTest aprovados. `git diff --check` limpo.
- `REVIEW`: revisão adversarial apontou inicialmente fallback de migração incompleto e gravação em dois DataStores não atômica; ambos corrigidos estruturalmente. Releitura confirmou mutex compartilhado, API AES-GCM compatível com minSdk 24 e sem dependência nova. AVD `MulletaflixApi35` preservado sem interação.
- `GRAPHIFY`: `graphify update .` após código e documentação — 64.224 nós, 164.310 arestas, 1.579 comunidades; `graphify explain SessionRepositoryImpl` confirmou relações com testes instrumentados e consumidoras; `graphify explain SessionTokenCrypto` confirmou relação com encrypt/decrypt e Keystore. A CLI instalada reextraiu código, mas informou que documentos Markdown precisam do fluxo `/graphify --update` no assistente; por isso, semântica dos textos TDD/TODO não foi reextraída. CLI 0.9.79 vs skill 0.9.84; 3 arquivos SQL sem parser, 4 fontes com extração sintática parcial e 174 arquivos sem símbolos.
- `MOBILE CHECKPOINT`: Android Kotlin nativo; nenhuma UI ou nova dependência. Contrato público de sessão preservado; plaintext existe somente no valor de sessão em memória enquanto consumido. Sem novo gesto/layout/renderização. MFRI: 8.
- `STATUS`: concluído localmente; sem bump, artefato de produção, release, portal, commit ou push.

## RED-GREEN — contexto nos resultados de busca de livros — 2026-10-11

- `INTENT`: `searchResults` rotulava cada ocorrência com `chunks[index].take(100)`; o teste exige contexto em torno do termo; `TODO-APP.md` mantém a busca navegável e `BookTextSearch.contextualSnippet` já fornece excerto limitado para PDF.
- `RED`: `BookTextSearchTest.search result snippet centers on a late accent-insensitive match` falhou antes da implementação (`AssertionError` na primeira asserção), pois a ocorrência tardia em “Ação estratégica” ficava fora dos primeiros 100 caracteres.
- `GREEN`: resultados do texto paginado reutilizam `contextualSnippet`, centralizando o excerto no termo sem perder acentos; a regressão testa busca sem acento, posição do termo, índice de navegação e limite de 140 caracteres.
- `QUALITY BAR`: `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL` em 4m45s; 248 suites/1.675 testes JVM, zero falhas/erros/ignorados; lint do módulo/app, assemble Debug e compilação dos AndroidTests do módulo aprovados. Teste focal `BookTextSearchTest`: 4/4.
- `GRAPHIFY`: `graphify update .` — 11.448 nós, 30.117 arestas, 492 comunidades; `graphify explain BookTextSearchDialog` confirmou as conexões com `BookReaderScreen` e testes de UI. CLI 0.9.79 vs skill 0.9.84; o atualizador avisou uma fonte preexistente de TV com extração sintática parcial e 28 arquivos sem símbolos.
- `REVIEW`: avaliação adversarial independente sem findings em offsets, acentos ou limite; nenhum arquivo ou AVD alterado.
- `SCOPE`: APK local somente; AVD protegida mantida intocada; instrumentação visual permanece pendente; sem bump, artefato de produção, release, portal, commit ou push.

## RED-GREEN — sumário navegável em livros ODT — 2026-10-11

- `INTENT`: o extrator ODT lia texto de `<text:h>`, mas descartava capítulos; o teste exige títulos, hierarquia e posição navegável no trecho certo; `text:outline-level` define o nível do heading ODF.
- `RED`: `OdtBookTextExtractorTest.opens ODT outline headings as navigable chapters at their text chunks` falhou antes da implementação (`AssertionError`), pois `PlainTextBookDocument.open` retornava capítulos vazios. A revisão apontou offsets desalinhados após CRLF; novo teste falhou em `assertFalse` com `&#13;&#10;` antes do título.
- `GREEN`: parser preserva título com spans/entidades, offsets e nível `text:outline-level`; nível ausente assume 1, profundidade é limitada a 9 e capítulos vazios não entram no sumário, limitado a 10.000. Os offsets são ajustados ao trim e ligados aos chunks.
- `QUALITY BAR`: `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL` em 4m26s; 248 suites/1.674 testes JVM, 0 falhas/erros/ignorados; `git diff --check` aprovado. Teste focal ODT aprovado.
- `GRAPHIFY`: `graphify update .` — 64.189 nós, 164.223 arestas, 1.584 comunidades; `graphify explain OdtBookTextExtractor` confirmou ligações ao parser XML, `Content`/`Heading` e extração. Avisos: versão CLI 0.9.79 vs skill 0.9.84, 3 SQL sem parser, 4 fontes com extração sintática parcial e 174 arquivos sem símbolos.
- `REVIEW`: duas revisões adversariais; a correção CRLF foi retida. CR isolado vira LF sem alteração de comprimento, logo não desloca offsets.
- `SCOPE`: APK local somente; AVD reservada intocada; sem versão, artefato de produção, release, portal, commit ou push.

## RED-GREEN — limite MOBI pelo tamanho declarado — 2026-10-11

- `INTENT`: o extrator limitava texto MOBI ao teto global, deixando registros PalmDOC expandirem além do tamanho anunciado; o teste exige rejeição no primeiro bloco literal ou back-reference que cruza o tamanho declarado; `TODO-APP.md` promete validar tamanho e manter leitura limitada.
- `RED`: `MobiBookTextExtractorTest.rejects PalmDOC output as soon as it exceeds the declared text length` falhou antes da alteração (`ComparisonFailure`): o parser só acusava divergência depois de descompactar toda a saída.
- `GREEN`: a saída agora usa o tamanho declarado como limite efetivo; escrita literal e back-reference são recusadas antes de crescer o buffer além desse limite. O teto global de 8 MiB permanece validado antes da alocação.
- `QUALITY BAR`: `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL`; relatórios `testDebugUnitTest`: 1.671 testes, 0 falhas/erros/ignorados; `git diff --check` passou.
- `GRAPHIFY`: `graphify update .` — 64.180 nós, 164.196 arestas, 1.605 comunidades; `graphify explain MobiBookTextExtractor` confirmou conexões ao parser e ao stream limitado. CLI 0.9.79 vs skill 0.9.84; atualização estrutural não reextrai semanticamente prosa Markdown.
- `SCOPE`: APK local somente; sem AVD, bump, artefato de produção, release, portal, commit ou push.

## RED-GREEN — sumário de livros HTML e MOBI — 2026-10-11

- `INTENT`: o leitor extraía texto HTML/MOBI, mas descartava capítulos; testes exigem capítulos apenas de cabeçalhos visíveis `<h1>`–`<h6>`, profundidade correta e navegação ao trecho certo. `TODO-APP.md` diz que formatos sem estrutura não devem mostrar capítulos fictícios e registra HTML/MOBI como formatos de leitura direta.
- `RED`: testes de HTML com cabeçalho oculto/script e título depois do primeiro trecho, e de MOBI com cabeçalho, falharam porque `chapters` vinha vazio. Revisão adversarial encontrou heading aninhado com título externo contaminado; regressão foi adicionada.
- `GREEN`: sanitiza antes de reconhecer cabeçalhos; converte cabeçalhos para tags internas únicas, captura offsets durante a conversão Android HTML, remove marcadores antes de expor texto e mapeia títulos/chunks ao modelo comum do sumário. Eventos por marcador mantêm headings aninhados separados.
- `QUALITY BAR`: testes focais `PlainTextBookDocumentTest` + `MobiBookTextExtractorTest` passaram; `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL`; 1.670 testes JVM, 0 falhas/erros/ignorados; `git diff --check` limpo.
- `GRAPHIFY`: `graphify update .` — 64.178 nós, 164.188 arestas, 1.467 comunidades; `graphify explain htmlToBookDocument` liga conversor a `open`, sanitização, marcação e modelo de capítulos; `PlainTextBookDocumentTest` conecta cenários e TDD. CLI 0.9.79 vs skill 0.9.84; 3 SQL sem parser, 4 arquivos com extração parcial e 174 sem símbolos. Update estrutural cobre código; prosa TDD/TODO não foi reextraída semanticamente.
- `LIMITAÇÕES`: nenhum AVD, servidor, portal, bump de versão ou release. Validação MOBI real permanece pendente; instrumentação não foi executada porque AVD está reservada pelo usuário.

## RED-GREEN — sumário navegável em livros Markdown — 2026-10-11

- `INTENT`: o parser já converte Markdown em texto seguro, mas omite os cabeçalhos do sumário comum; leitores Markdown devem navegar pelos cabeçalhos ATX sem indexar linhas dentro de blocos cercados.
- `RED`: `PlainTextBookDocumentTest.builds Markdown table of contents from headings outside code fences` falhou com títulos ausentes; testes de mais de 10.000 capítulos e cerca indentada em quatro espaços também reproduziram limites/parsing ausentes.
- `GREEN`: parser mantém níveis e títulos formatados de cabeçalhos ATX externos a cercas, associa-os ao chunk correto, respeita até três colunas de recuo (tabs contam por tab-stop de quatro), limita sumário a 10.000 entradas como DOCX e calcula offsets com o mesmo predicado Unicode do trim.
- `TEST`: `PlainTextBookDocumentTest` — 25/25; qualidade Android completa — `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin` — `BUILD SUCCESSFUL`; 1.668 testes JVM aprovados, 0 falhas/erros/ignorados; `git diff --check` aprovado.
- `GRAPHIFY`: atualização do grafo raiz — 64.165 nós/165.459 arestas; `graphify explain` confirmou parser e teste relacionados. Graphify 0.9.79 está abaixo da skill 0.9.84; 3 SQL sem parser, 4 arquivos com extração sintática parcial e 174 sem símbolos.
- `REVIEW`: revisores Cavecrew encontraram fechamento indevido com recuo inválido e ausência de limite; correções adicionaram também cobertura do limite exato e whitespace Unicode.
- `LIMITAÇÕES`: AVD `MulletaflixApi35` permanece reservado pelo usuário; nenhuma instrumentação foi executada. Sem alteração de versão, servidor, portal ou release.

## Revalidação Compose — navegação do sumário Markdown — 2026-10-14

- `INTENT`: parser e modelo de capítulos Markdown já navegam para o chunk correto; a nova integração deve abrir o sumário Compose, selecionar um cabeçalho em outro chunk, exibir título/corpo e preservar o locator correspondente.
- `COBERTURA`: `PlainTextBookReaderIntegrationTest.markdownTableOfContentsNavigatesToSecondChunkAndRestoresItsLocator` cria documento Markdown sintético com dois cabeçalhos separados por mais de 1.200 caracteres, renderiza as ações comuns de sumário/leitor, seleciona o segundo capítulo, verifica o texto no chunk de destino e confere round-trip do locator.
- `TENTATIVA INICIAL`: falhou com `IndexOutOfBoundsException: Index 1 out of bounds for length 1` antes de compor UI; a fixture interpolada não produziu o segundo capítulo. Não é RED válido de comportamento do aplicativo. A fixture foi reescrita com linhas explícitas e uma asserção do conjunto de títulos antes de indexar.
- `GREEN`: o teste passou 1/1 em TABLET/API 35 no AVD `MulletaflixTtsVoiceTabletApi35`, via wrapper `tools/with-emulator.ps1`. XML: `feature/item-detail/build/outputs/androidTest-results/connected/debug/TEST-MulletaflixTtsVoiceTabletApi35(AVD) - 15.xml`, 1 teste, 0 falhas/erros/ignorados. Wrapper verificou QEMU na NVIDIA RTX 3050, encerrou somente o AVD iniciado para o teste; `adb devices -l` terminou sem dispositivos.
- `LIMITES`: fixture sintética e interação Compose automatizada; não comprova TalkBack manual, controle remoto, arquivo real ou aparelho físico. APK-H21 e aceite formal não mudam por este teste isolado. Sem alteração de produção, versão, release, assinatura, servidor ou portal.

## RED-GREEN — trechos contextuais na busca de PDF — 2026-10-11

- `INTENT`: `BookPdfTextSearch` exibia sempre o começo da página e podia exceder 140 caracteres ao alinhar limites UTF-16; o teste exige contexto da ocorrência real, sem distinção de caixa/acentos, limite estrito e pares substitutos íntegros. `TODO-APP.md` estabelece busca página a página para PDFs com texto selecionável; este incremento melhora o trecho de resultado sem alterar navegação.
- `RED`: `BookPdfTextSearchTest.snippet shows a late accent-insensitive match in the page` compilou e falhou na asserção do contexto porque a ocorrência ficava após os primeiros 140 caracteres.
- `GREEN`: resultado agora centraliza o trecho na primeira ocorrência, mantém acentos originais, adiciona reticências quando há texto omitido e limita a 140 caracteres; teste adicional cobre corte junto a emoji/surrogate pair.
- `QUALITY BAR`: `BookPdfTextSearchTest` passou 6/6. `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin :app:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL` em 6m; XML JVM agregados: 1.664 testes, 0 falhas/erros/ignorados. `git diff --check` limpo.
- `GRAPHIFY`: `graphify update . --no-cluster` — 64.155 nós e 165.428 arestas. `graphify explain "contextualSnippet"` liga o helper ao fluxo `BookPdfTextSearch.search`; `graphify explain "BookPdfTextSearchTest"` confirma os cenários e referências ao TDD/TODO. Avisos: CLI 0.9.79 vs skill 0.9.84; 3 SQL sem parser, 4 arquivos parcialmente extraídos e 174 sem símbolos.
- `ESCOPO`: APK local, algoritmo e testes de busca PDF e documentação Android; AVD preservada intacta; sem bump, produção, release, portal ou mudança no servidor.

## RED-GREEN — páginas de código ANSI no leitor RTF — 2026-10-11

- `INTENT`: `RtfBookTextParser` decodificava todos os escapes `\'hh` como Windows-1252 e ignorava `\ansicpgN`; o teste exige usar a página declarada e preservar o estado entre grupos. `TODO-APP.md`, seção “Leitura direta de formatos de livro no APK”, promete converter escapes RTF sem restringir páginas de código. A especificação Microsoft identifica `\ansicpgN` como página de código ANSI padrão para as sequências de texto RTF: https://learn.microsoft.com/en-us/openspecs/exchange_server_protocols/ms-oxrtfex/1e44fa4c-3ea3-402c-8b21-fb19a617e43b
- `RED`: antes do parser, os testes CP1251 e página desconhecida falharam pelas razões esperadas: texto cirílico decodificado incorretamente e ausência do erro de página incompatível.
- `GREEN`: parser resolve `\ansicpgN`, mantém CP1252 por padrão, decodifica sequências contíguas com o charset declarado e rejeita charset/bytes não suportados sem corrupção silenciosa. Cobertura adicional valida escopo de grupo e UTF-8.
- `QUALITY BAR`: classe `RtfBookTextParserTest` passou 9/9; `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL` em 3m34s; 248 relatórios XML, 1.662 testes JVM, 0 falhas/erros/ignorados; `git diff --check` limpo.
- `GRAPHIFY`: `graphify update . --no-cluster` reconstruiu 64.148 nós e 165.407 arestas; `graphify explain "RtfBookTextParser"` mostra `appendAnsiEscapeRun`, `ansiCharset` e ligação ao registro TDD; `RtfBookTextParserTest` liga os quatro novos cenários ao plano e ao TODO de formatos. Avisos conhecidos: pacote Graphify 0.9.79 vs skill 0.9.84, 3 SQL sem parser e extração parcial em 4 arquivos.
- `ESCOPO`: somente parser/testes do APK e documentação Android; sem servidor, portal, versão, pacote, release, commit ou push; AVD preservada não foi acessada e instrumentação não foi executada.

## RED-GREEN — limites para locators persistidos do leitor — 2026-10-11

- `INTENT`: a gravação limitava marcadores a 8.192 caracteres UTF-16, mas o decoder ignorava o limite; a retomada de leitura também lia e gravava locators sem teto. A especificação Android registra persistência/restauração da posição e os marcadores existentes já têm limite de tamanho.
- `RED`: `BookReaderProgressStoreTest.ignores persisted bookmarks whose locator exceeds the write limit` e `does not store a progress locator larger than the reader size limit` reproduziram o excesso com locators Readium válidos; o segundo teste falhou porque o progresso foi persistido/restaurado.
- `GREEN`: limite compartilhado de 8.192 caracteres UTF-16 em gravação/decodificação de progresso e marcadores, antes do parser Readium; payload agregado de marcadores limitado por `MAX_BOOKMARKS_JSON_CHARS`. Testes preservam locators válidos e recusam as três entradas excessivas.
- `QUALITY BAR`: cinco testes focais passaram; `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL` em 3m23s; 248 relatórios XML, 1.658 testes, zero falhas/erros/ignorados; `git diff --check` limpo.
- `TESTABILITY`: decoders ficaram testáveis dentro do módulo sem reflexão; depois da revisão de progresso, `:feature:item-detail:testDebugUnitTest` passou com 235 testes, zero falhas/erros/ignorados; suíte e lint do módulo repetidos em seguida.
- `GRAPHIFY`: `graphify update . --no-cluster` concluiu com 11.390 nós e 30.080 arestas; `graphify explain` confirmou referências de `BookReaderProgressStore` à classe de teste e de `BookReaderProgressStoreTest` ao plano de testes. CLI 0.9.79 (skill 0.9.84) advertiu extração parcial preexistente em `TvHomeRefreshIntegrationTest.kt` e 28 arquivos sem símbolos.
- `ESCOPO`: somente APK e documentação Android; sem versão, servidor, portal, assinatura, pacote ou publicação; AVD preservada não foi consultada/alterada; instrumentação Android não executada.

## RED-GREEN — cancelamento cooperativo da busca textual de livros — 2026-10-11

- `INTENT`: `BookTextSearch.search` percorria todos os trechos sem ponto de suspensão ou verificação de cancelamento; o teste espera parar quando a consulta antiga for cancelada. `TODO-APP.md` exige cancelar a busca anterior ao substituir a consulta e manter resultados sincronizados.
- `RED`: `BookTextSearchTest.stops scanning text chunks promptly when the search coroutine is cancelled` compilou e falhou pela asserção esperada: leu 10.000 trechos em vez de parar após 5.
- `GREEN`: a busca agora é suspend e verifica o contexto de coroutine por trecho; extensão do documento também é suspend. Teste novo + classe `BookTextSearchTest` passaram.
- `QUALITY BAR`: `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL` (3m07s); 247 relatórios XML, 1.653 testes JVM, zero falhas/erros/ignorados; `git diff --check` limpo.
- `GRAPHIFY`: `graphify update . --no-cluster` concluiu com 11.373 nós e 30.037 arestas; `graphify explain "BookTextSearch"`, `graphify explain "BookReaderScreen"` e o novo teste confirmaram vínculos busca→diálogo→teste. CLI 0.9.79 (skill 0.9.84) reportou extração parcial preexistente em `TvHomeRefreshIntegrationTest.kt`.
- `ESCOPO`: somente APK Android e docs TDD; sem servidor, versão, assinatura, portal ou publicação.

## RED-GREEN — hierarquia real de estilos DOCX no sumário — 2026-10-11

- `INTENT`: o extrator resolvia somente IDs padrão `Heading1–9`/nível direto e perdia headings com `styleId` personalizado; teste espera resolver a referência do parágrafo, herdar `outlineLvl` por `basedOn` e respeitar o nível direto. Microsoft Open XML documenta que `pStyle` referencia `styleId` e que ID difere do nome mostrado: https://learn.microsoft.com/en-us/office/open-xml/word/how-to-create-and-add-a-paragraph-style-to-a-word-processing-document
- `RED`: `PlainTextBookDocumentTest.resolves custom DOCX paragraph headings through inherited styles` falhou por capítulos vazios antes da implementação.
- `GREEN`: extrator indexa apenas estilos de parágrafo, resolve `basedOn` com detecção de ciclo e limites explícitos; teste focal passou junto dos testes de limite/arquivo.
- `QUALITY BAR`: `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL` (3m26s); 247 relatórios XML, 1.652 testes JVM, 0 falhas/erros/ignorados. `git diff --check` sem erros.
- `GRAPHIFY`: `graphify update . --no-cluster` concluiu com 11.368 nós e 30.021 arestas; `graphify explain "DocxBookTextExtractor"` e `graphify explain "PlainTextBookDocumentTest"` confirmaram ligações parser→estilos→teste. Um teste Android preexistente (`TvHomeRefreshIntegrationTest.kt`) foi parcialmente extraído pela versão local 0.9.79.
- `ESCOPO`: somente APK local; não alterar servidor, versão, portal, assinatura ou publicação. AVD `MulletaflixApi35` continua reservada; compilar testes, sem instrumentação.

## RED-GREEN — estilos DOCX herdados de Heading padrão — 2026-10-11

- `INTENT`: resolvedor seguia `basedOn` apenas enquanto cada ancestral existia em `styles.xml`; teste espera que cadeia personalizada herde nível de `Heading1` mesmo sem definição local completa. Open XML define `basedOn` como ID do estilo pai e define herança de estilo: https://learn.microsoft.com/en-us/dotnet/api/documentformat.openxml.wordprocessing.basedon?view=openxml-3.0.1
- `RED`: `PlainTextBookDocumentTest.resolves custom DOCX paragraph headings through inherited styles` compilou e falhou na asserção esperada porque `Capítulo baseado em Heading1` não foi indexado.
- `GREEN`: o resolvedor agora confere nível explícito em cada ancestral, reconhece Heading1–9 no caminho e encerra em ciclo/limite; o teste focal passou.
- `UI`: o teste Compose existente de sumário DOCX ganhou cadeia `CustomChapter → LocalIntermediate → Heading1`, navegação para heading direto e personalizado, e verificação do locator. Compilou; execução instrumentada foi tentada no AVD Tablet, mas o wrapper recusou corretamente porque o AVD preservado `emulator-5572` estava visível como dispositivo adicional. O wrapper encerrou o Tablet e verificação final confirmou que apenas o AVD preservado continuou aberto; nenhuma interação nele foi feita.
- `QUALITY BAR`: `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL` (3m05s); 247 relatórios XML, 1.652 testes JVM, zero falhas/erros/ignorados. `git diff --check` sem erros.
- `GRAPHIFY`: `graphify update . --no-cluster` concluiu com 11.369 nós e 30.023 arestas; `graphify explain` pós-atualização confirma parser DOCX, resolver de estilos e testes integrados. Graphify 0.9.79 sinalizou extração parcial do teste preexistente `TvHomeRefreshIntegrationTest.kt`.
- `ESCOPO`: somente APK local; manter as mudanças existentes; sem bump, produção, portal, servidor ou publicação.

## RED-GREEN — sumário de capítulos em DOCX — 2026-10-11

- `INTENT`: o leitor já extrai o texto de DOCX, mas não mostra navegação por capítulos; deve derivar entradas de estilos `Heading 1`–`Heading 9` ou `outlineLvl`, apontar para o trecho correto e manter o locator de retomada.
- `RED`: `PlainTextBookDocumentTest.builds DOCX chapter navigation from Word heading styles and outline levels` falhou porque `document.chapters` estava vazio.
- `GREEN`: o extrator retorna texto e metadados limitados a 10.000 capítulos; o leitor mapeia offsets aos trechos existentes. O teste focal passou.
- `UI`: foi acrescentado teste Compose para abrir sumário, selecionar capítulo e validar trecho/locator; compilação AndroidTest é obrigatória, execução em AVD aguarda liberação do dispositivo preservado.
- `QUALITY BAR`: `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL` em 4m01s; 247 relatórios XML, 1.650 testes JVM, 0 falhas/erros/ignorados. Focal `PlainTextBookDocumentTest` também passou; `git diff --check` sem erros.
- `GRAPHIFY`: `graphify update . --no-cluster` reindexou 1.058 arquivos de código; explicar os nós do extrator/tela/testes após a atualização.
- `ESCOPO`: somente APK local; nenhuma mudança em servidor, versão, portal, assinatura, APK de produção ou release.

## Regressão de navegação Activity para logout em Ajustes — 2026-10-11

- `ESCOPO`: cenário instrumentado novo, sem alterar comportamento de produção. Como o fluxo já fora corrigido e os testes de ViewModel já passaram na etapa anterior, este teste de integração de regressão não foi apresentado como RED retroativo.
- `CENÁRIO`: sessão completa persistida → abrir Home na Activity → abrir “Mais ações”/“Configurações” → “Sair” → aguardar seleção de servidor; valida token e usuário limpos e URL do servidor preservada.
- `ANDROID`: execução focal PHONE/API 35 via `tools/with-emulator.ps1` — 1 teste aprovado, 0 falhas, 0 ignorados. O wrapper confirmou QEMU na GPU NVIDIA e deixou `adb devices` sem emuladores ao encerrar.
- `SUÍTE DA CLASSE`: depois das duas falhas intermitentes anteriores (9 testes), uma nova execução integral passou: 9/9 PHONE/API 35, incluindo `crossServerDeepLinkAuthenticatesOnTargetServerAndLoadsItemWithoutRequestingOldServer`; XML `TEST-MulletaflixAptoidePhone(AVD) - 15.xml`, dispositivo `emulator-5574`, zero falhas/erros/ignorados. O teste registra chamadas, credenciais sem segredo, endpoint salvo e estado visível, e prepara explicitamente a URL do servidor A. Esta única repetição verde não elimina a flakiness nem comprova uma correção de produção; causa funcional/timing ainda em aberto.
- `ISOLAMENTO`: a primeira tentativa com AVD separado revelou que Gradle selecionaria todos os emuladores conectados, incluindo `MulletaflixApi35`; ela foi cancelada imediatamente. A repetição limitou `ANDROID_SERIAL=emulator-5574`, e o XML confirma execução apenas no AptoidePhone. `MulletaflixApi35` não foi limpo nem desligado; como o despacho multi-device chegou a iniciar, seu estado deve ser conferido antes de reutilizá-lo.
- `QUALITY BAR`: `testDebugUnitTest :app:lintDebug :app:assembleDebug :app:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL`; 247 XML, 1.648 testes JVM, 0 falhas/erros/ignorados. `git diff --check` sem erros (apenas avisos de conversão LF/CRLF).
- `GRAPHIFY`: após mudanças em código e documentação, `graphify update . --no-cluster` — 64.074 nós/165.184 arestas; `graphify explain crossServerDeepLinkAuthenticatesOnTargetServerAndLoadsItemWithoutRequestingOldServer` encontrou a instrumentação e relações com `SessionRepositoryImpl`, `TESTING.md` e `TDD-PLAN.md`. Avisos de parser no repositório: 3 SQL sem tree-sitter, 4 fontes com erros sintáticos parciais, 174 arquivos sem símbolos; CLI 0.9.79 vs skill 0.9.84.
- `STATUS`: teste de logout isolado e uma execução da classe completa passaram; o histórico de duas falhas intermitentes permanece, sem correção de produção demonstrada. Manter o fluxo cross-server pendente até reproduções repetidas e estáveis. Não reutilizar `MulletaflixApi35` antes de o usuário conferir a sessão/dispositivo.

## RED-GREEN — logout confiável em Ajustes — 2026-10-11

- `INTENT`: Ajustes navegava à seleção de servidor imediatamente, antes de confirmar `LogoutUseCase`; deve permanecer na tela em caso de falha, mostrar erro recuperável e navegar apenas após sucesso.
- `RED`: os testes de falha/sucesso foram escritos primeiro. A primeira execução só falhou ao compilar e não conta como RED segundo `TESTING.md`; após adicionar temporariamente uma implementação neutra compilável, as duas asserções de comportamento falharam como esperado.
- `GREEN`: `SettingsViewModel` agora bloqueia pedidos duplicados, publica progresso, preserva o destino e expõe fallback de erro em falha; navegação só ocorre após sucesso. `SettingsScreen` aguarda o resultado e mostra erro visível.
- `UNIT`: os dois testes focais passaram após GREEN. Quality gate `testDebugUnitTest :feature:settings:lintDebug :app:lintDebug :app:assembleDebug :app:compileDebugAndroidTestKotlin` — BUILD SUCCESSFUL; 247 relatórios XML, 1.648 testes JVM, 0 falhas/erros/ignorados.
- `DEVICE`: `:feature:settings:connectedDebugAndroidTest` — 18/18 PHONE/API 35, 0 falhas/erros/ignorados; wrapper confirmou QEMU na NVIDIA RTX 3050 e encerrou o AVD.
- `GRAPHIFY`: `graphify update .` — 11.325 nós, 29.753 arestas; `graphify cluster-only .` refinou para 465 comunidades. `explain SettingsViewModel`, `SettingsScreen` e teste focal confirmaram relações. CLI 0.9.79 difere da skill 0.9.84; um teste Home continua parcialmente extraído.
- `LIMITAÇÃO`: à época desta alteração, faltava navegação pela tela Settings real em Activity; essa lacuna está coberta no registro “Regressão de navegação Activity para logout em Ajustes” acima. Casos integrados de troca de servidor e retorno de deep link cross-server continuam pendentes.
- `ESCOPO`: APK local; sem bump, pacote de produção, portal, release, commit ou push.

## RED-GREEN — fallbacks para mensagens vazias em cadastro, downloads, TV ao vivo e SyncPlay — 2026-10-11

- `INTENT`: dez fluxos propagavam `message ?: fallback`, aceitando texto vazio/em branco; código deve exibir fallback localizado e manter mensagem útil do servidor.
- `RED`: dez testes novos falharam pela comparação entre fallback esperado e espaços recebidos: cadastro (1), preparação/fila de download (2), canais/guia (2), SyncPlay (5). No cadastro, a fonte correta é `RegistrationResult.message`; o teste foi alinhado ao contrato observado em `RegistrationResult` antes do RED final.
- `GREEN`: cadastro agora valida mensagem de resposta não branca; os outros nove fluxos usam `Throwable.userFacingMessageOr()`. As ações também encerram os indicadores de progresso ao falhar.
- `QUALITY BAR`: `testDebugUnitTest :feature:auth:lintDebug :feature:item-detail:lintDebug :feature:live-tv:lintDebug :feature:sync-play:lintDebug :app:lintDebug :app:assembleDebug` — `BUILD SUCCESSFUL`; relatórios XML agregados após o teste de preservação: 1.646 testes, 0 falhas/erros/ignorados.
- `TWINS`: busca por `(localizedMessage|error.message|e.message|err.message|result.message) ?: ` encontrou zero fallbacks de exceção; a única ocorrência restante usa `state.message ?: ""` para estado de UI.
- `GRAPHIFY`: `graphify update .` e `explain` confirmaram relações ViewModel/telas/testes nos quatro módulos. Avisos: CLI 0.9.79 versus skill 0.9.84, extração parcial de teste Home e 28 arquivos sem símbolos.
- `ESCOPO`: somente APK local; sem mudança de versão, servidor, portal, APK de produção ou publicação.

## RED-GREEN — fallbacks em playlists, atualização e gravação de TV — 2026-10-11

- `INTENT`: oito falhas de atualização/playlists/gravações usavam `error.message ?: fallback`; o teste espera fallback localizado para exceção em branco e preservação do texto útil, conforme `Throwable.userFacingMessageOr()`.
- `RED`: cinco testes novos falharam antes da mudança: operações de playlist em detalhes, lista/títulos de playlists, erro ao consultar update, agendar gravação e cancelar gravação.
- `GREEN`: os oito caminhos reutilizam o helper comum.
- `UNIT`: os cinco testes focais passaram depois da correção; suíte global agregada: 1.635 testes, 0 falhas/erros/ignorados.
- `QUALITY BAR`: `testDebugUnitTest :feature:item-detail:lintDebug :feature:settings:lintDebug :feature:live-tv:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin` — `BUILD SUCCESSFUL`; `git diff --check` passou.
- `TWINS`: oito dos dez sites anteriores foram corrigidos; restam os dois avisos de narração em `BookReaderScreen.kt`, cuja validação de interface requer uma falha reproduzível do fluxo PDF/EPUB.
- `GRAPHIFY`: atualizar após a edição/documentação e conferir as ligações de ViewModels, telas e testes.
- `ESCOPO`: APK local; sem mudança de versão, assinatura, artefato de produção, portal ou publicação.

## RED-GREEN — mensagens de erro recuperáveis na biblioteca — 2026-10-11

- `INTENT`: cinco fluxos de biblioteca repassavam `error.message ?: fallback`, aceitando strings em branco; testes exigem texto recuperável preservando o texto de exceções úteis, conforme o contrato de `Throwable.userFacingMessageOr()`.
- `RED`: cinco testes novos falharam antes da correção: erro inicial, paginação, opções de filtros, catálogo completo aleatório e Minha Lista; todos recebiam espaços no estado.
- `GREEN`: os cinco caminhos usam o helper comum; mensagens não vazias continuam intactas.
- `UNIT`: cinco testes focais passaram após GREEN; suíte global agregada: 1.630 testes, 0 falhas/erros/ignorados.
- `QUALITY BAR`: `testDebugUnitTest :feature:library:lintDebug :app:lintDebug :app:assembleDebug` — `BUILD SUCCESSFUL`; `git diff --check` passou.
- `TWINS`: escopo remove cinco dos 15 sites previamente encontrados; 10 sites fora da biblioteca continuam para incrementos futuros.
- `GRAPHIFY`: atualização após código/documentação e `explain LibraryViewModel` confirmaram as relações com a tela e os testes.
- `ESCOPO`: somente APK local, sem bump, produção, release, portal, commit ou push.

## Validação — fallback de erros na narração de livros — 2026-10-11

- `INTENT`: exceções da extração de texto PDF e inicialização de narração EPUB devem exibir fallback localizado se a mensagem estiver ausente/branca; mensagens úteis devem continuar visíveis.
- `EVIDÊNCIA PRÉVIA`: `ThrowableMessagesTest` já cobre exceções sem mensagem, com espaços e com mensagem útil; teste PDF integrado cobre início, avanço e parada da narração.
- `MUDANÇA`: ambos os `catch` usam `Throwable.userFacingMessageOr()`; nenhum código de servidor ou release foi alterado.
- `DEVICE`: `BookReaderScreenIntegrationTest.pdfSpeechReadsVisiblePageAdvancesAndStopsOnManualNavigation` passou 1/1 no PHONE/API 35 via wrapper; AVD usou NVIDIA e foi encerrado. O cenário valida fluxo PDF, mas não injeta exceção no `catch`.
- `QUALITY BAR`: `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin` — `BUILD SUCCESSFUL`; relatórios agregados 1.635 testes JVM, 0 falhas/erros/ignorados; `git diff --check` aprovado.
- `LIMITAÇÃO TDD`: não há seam de falha injetável na tela para reproduzir especificamente esses dois `catch`; o RED/GREEN do consumidor não foi demonstrado. Os testes existentes de `Throwable.userFacingMessageOr()` cobrem fallback branco e preservação de mensagem útil.
- `GRAPHIFY`: `graphify update .` — 11.291 nós, 29.636 arestas, 497 comunidades; `explain BookReaderScreen` confirmou vínculos com integração e plano TDD. Avisos: CLI 0.9.79 versus skill 0.9.84, extração parcial de `TvHomeRefreshIntegrationTest.kt` e 28 arquivos sem símbolos.
- `ESCOPO`: APK local; sem mudança de versão, APK de produção, portal ou publicação.

## RED-GREEN — fallback em erro de abertura do leitor — 2026-10-11

- `INTENT`: o `BookReaderViewModel` repassava `error.message ?: fallback`, que aceita mensagem vazia/branca; o teste espera uma orientação recuperável, conforme o TODO do leitor sobre mensagens de erro/retry.
- `RED`: `BookReaderViewModelTest.load shows a fallback when the failure message is blank` falhou antes da alteração (`ComparisonFailure`, linha 118); a exceção `"   "` chegou ao estado sem fallback.
- `GREEN`: o catch do carregamento usa `Throwable.userFacingMessageOr()` compartilhado; o fallback textual continua “Não foi possível abrir este livro.”.
- `UNIT`: teste focal aprovado; suíte completa agregada: 1.625 testes, 0 falhas/erros/ignorados.
- `QUALITY BAR`: `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin` — `BUILD SUCCESSFUL`; `git diff --check` passou.
- `TWINS`: busca global por `error.message ?: ` encontrou 15 outros pontos em Settings, leitor de áudio, playlists, detalhes, biblioteca e TV ao vivo; separados desta mudança e precisam de correções/testes em incrementos posteriores. Busca por `localizedMessage ?: ` encontrou zero.
- `GRAPHIFY`: atualização após código/documentos e `explain` do ViewModel/teste concluídos abaixo.
- `ESCOPO`: APK local apenas; sem bump, produção, release, portal, commit ou push.

## RED-GREEN — fallback compartilhado para erro sem mensagem — 2026-10-11

- `INTENT`: pontos do app usavam `localizedMessage ?: fallback`, que aceita texto vazio/branco; testes esperam uma mensagem recuperável quando a exceção não traz texto; o guia Android exige que falhas não sejam silenciosas.
- `RED`: o teste `ItemDetailViewModelTest.loadItem uses a fallback when failure message is blank` falhou pela comparação esperada após receber somente espaços. `PlaybackIssueReportDialogTest.blankFailureMessageShowsActionableFallback` também falhou antes da correção no PHONE/API 35.
- `GREEN`: helper `Throwable.userFacingMessageOr()` em `:core:common` usa `localizedMessage` somente se `isNotBlank()`. Aplicado aos oito sites vulneráveis encontrados em Home, detalhes/leitor/relato e download/atualização; Home e perfil também reutilizam o helper onde já tinham lógica equivalente. Cancelamentos continuam sem mensagem.
- `UNIT`: testes do helper cobrem mensagem nula, em branco e válida; teste do ViewModel de detalhes e testes existentes de logout/troca de usuário passaram.
- `DEVICE`: `PlaybackIssueReportDialogTest.blankFailureMessageShowsActionableFallback` — 1/1 PHONE/API 35, 0 falhas/ignorados; confirma aviso visível e opção de salvar para envio posterior. Wrapper verificou GPU NVIDIA e encerrou o AVD.
- `QUALITY BAR`: suíte `testDebugUnitTest`, lint de `:core:common`, `:feature:home`, `:feature:item-detail`, `:feature:user` e `:app`, `:app:assembleDebug`, compilações AndroidTest de item-detail/app — `BUILD SUCCESSFUL`; XML: 1.624 testes, 0 falhas/erros/ignorados.
- `TWINS`: pesquisado `localizedMessage\s*\?:` em todos os arquivos Kotlin — zero ocorrências restantes.
- `GRAPHIFY`: atualização e `explain` do helper/testes registrados após atualizar código e documentação; Markdown continua fora da extração semântica da CLI.
- `ESCOPO`: somente APK; sem bump, assinatura/artefato de produção, portal, release, commit ou push.

## RED-GREEN — fallback para erros sem mensagem no perfil — 2026-10-11

- `INTENT`: falhas de logout ou troca de usuário com mensagem localizada vazia/em branco devem exibir texto acionável, sem navegar como se a operação tivesse sido concluída.
- `RED`: os dois novos testes de `UserProfileViewModelTest` falharam antes da correção porque o estado recebeu mensagem vazia/branca.
- `GREEN`: ambos os caminhos agora descartam mensagem em branco com `isNotBlank()` e usam o fallback localizado apropriado. Os dois testes unitários focados passaram.
- `DEVICE`: `ProfileLogoutFailureTest.failedLogoutWithBlankMessageRendersFallbackAndDoesNotInvokeNavigation` passou no PHONE/API 35; verifica mensagem renderizada e ausência de navegação. O wrapper NVIDIA iniciou e encerrou o AVD.
- `QUALITY BAR`: `testDebugUnitTest :feature:user:lintDebug :app:lintDebug :app:assembleDebug :feature:user:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL`; 246 relatórios XML: 1.620 testes, 0 falhas, 0 erros e 0 ignorados.
- `TWINS`: ambos os caminhos do `UserProfileViewModel` foram corrigidos. Busca em `feature/**/*.kt` encontrou usos independentes de `localizedMessage ?: ` em Home e detalhes/leitor/relato; não alterados neste escopo.
- `GRAPHIFY`: `graphify update .` — 11.245 nós, 29.506 arestas e 521 comunidades. `explain UserProfileViewModel` e `explain failedLogoutWithBlankMessageRendersFallbackAndDoesNotInvokeNavigation` ligam ViewModel, tela, teste unitário/instrumentado e esta nota (relação da nota inferida por título); Markdown não é semanticamente reextraído pela CLI. Avisos: versão instalada 0.9.79 vs instrução 0.9.84, parser parcial em `TvHomeRefreshIntegrationTest.kt:53`, 28 fontes sem símbolos e 83 recursos não classificados.
- `ESCOPO`: APK local; sem servidor, bump de versão, pacote de produção, portal, release, commit ou push.

## RED-GREEN — texto legível com MIME binário genérico — 2026-10-11

- `INTENT`: alguns servidores entregam livros em texto simples com `application/octet-stream`; continuar rejeitando binário arbitrário, mas reconhecer texto UTF-8/Windows-1252 legível e abri-lo no leitor de texto.
- `RED`: testes de policy esperam texto legível como `PLAIN_TEXT` e bytes binários de controle como `UNSUPPORTED`; teste do ViewModel exige o conteúdo carregado. Ambos falharam antes da implementação.
- `GREEN`: detecção limitada a amostra de 4 KiB, rejeita NUL e exige pelo menos 98% de caracteres imprimíveis; decodificação estrita UTF-8 com fallback Windows-1252. MIME explícito e EPUB estrutural preservam prioridade. ViewModel roteia a resposta genérica identificada como texto pelo leitor de texto.
- `UNIT`: testes focados de `BookReaderPayloadPolicyTest` e `BookReaderViewModelTest` — `BUILD SUCCESSFUL`.
- `DEVICE`: `BookReaderScreenIntegrationTest` para texto legível e binário não suportado — 2/2 PHONE/API 35; teste verifica estado carregado, conteúdo visível e mensagem acionável. Wrapper confirmou GPU NVIDIA e encerrou o AVD.
- `QUALITY BAR`: `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL`; XML agregado: 1.618 testes, 0 falhas, 0 erros, 0 ignorados.
- `GRAPHIFY`: atualização final e consultas `explain` registradas após sincronizar a documentação.
- `ESCOPO`: sem servidor, bump de versão, APK de produção, portal, release, commit ou push.

## RED-GREEN — não presumir EPUB para binário desconhecido — 2026-10-11

- `INTENT`: respostas `application/octet-stream` sem assinatura de um formato conhecido eram encaminhadas como EPUB por padrão, podendo exibir uma falha de parser confusa; exigir EPUB MIME explícito ou `META-INF/container.xml` e sinalizar formatos desconhecidos claramente.
- `RED`: `BookReaderPayloadPolicyTest.generic payload must have an EPUB container instead of defaulting unknown data to EPUB` compilou e falhou pela asserção esperada, pois o payload arbitrário era classificado como EPUB. Uma primeira tentativa de teste não compilou por referenciar um enum ainda inexistente; foi corrigida e não foi considerada evidência RED.
- `GREEN`: EPUB genérico só é selecionado com estrutura ZIP contendo `META-INF/container.xml`; MIME EPUB continua autoritativo; formatos por assinatura/MIME já suportados mantêm prioridade. O ViewModel passa a mostrar mensagem explícita e limpa recursos se o payload não corresponder a formato suportado.
- `UNIT`: `:feature:item-detail:testDebugUnitTest --tests ...BookReaderPayloadPolicyTest --tests ...BookReaderViewModelTest` — `BUILD SUCCESSFUL`; validou policy (incluindo desconhecido, EPUB com contêiner e MIME EPUB explícito) e estado de erro do ViewModel.
- `QUALITY BAR`: `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin` — `BUILD SUCCESSFUL`; XML agregado: 1.617 testes, 0 falhas, 0 erros, 0 ignorados.
- `DEVICE`: `BookReaderScreenIntegrationTest.unsupportedGenericPayloadShowsActionableFormatMessageInReader` — 1/1 PHONE/API 35; mensagem foi exibida e a rota do leitor confirmada; `tools/with-emulator.ps1` validou NVIDIA RTX 3050 e encerrou o AVD. Primeira execução teve falha apenas numa asserção inválida sobre corpo de GET; corrigida para verificar o caminho da requisição, repetição passou.
- `GRAPHIFY`: `graphify update .` — 11.229 nós, 29.442 arestas, 458 comunidades; `explain detectBookPayloadFormat()` mostra o fluxo para o ViewModel e testes, e `explain unsupportedGenericPayloadShowsActionableFormatMessageInReader` confirma o teste UI. `adb devices` final sem emuladores ativos. Permanecem avisos do parser parcialmente extraído e de Graphify 0.9.79 vs skill 0.9.84.
- `LIMITAÇÕES`: não testa cada variação de EPUB em servidor real; a detecção estrutural genérica exige o `META-INF/container.xml`, conforme o contrato do EPUB. Sem mudança de servidor, versão, pacote ou release.

## RED-GREEN — leitura de quadrinhos em página dupla adaptativa — 2026-10-11

- `INTENT`: o leitor de quadrinhos exibia só uma página em qualquer largura; testes esperam duas páginas apenas em tablets com largura mínima de 840 dp e largura menor da classe de tablet (600 dp), enquanto celulares e Android TV permanecem em página única. `TESTING.md` define UX separada por telefone/tablet/TV; o requisito de layout duplo vem do objetivo atual de melhorar a UX por dispositivo.
- `RED`: `ComicBookSpreadPolicyTest` falhou em runtime nos dois casos antes da regra correta: tablet largo não ativava spread e página seguinte não era incluída.
- `GREEN`: política calcula páginas visíveis com limites do arquivo; Compose decodifica e exibe somente uma ou duas páginas visíveis; controles de tablet avançam/retornam duas páginas, incluindo final ímpar.
- `DEVICE`: `ComicBookReaderIntegrationTest` — 7/7 em PHONE/API 35 (`MulletaflixApi35`), TABLET/API 35 (`MulletaflixTabletApi35`) e TV/API 34 (`MulletaflixTvApi34`); todos os wrappers confirmaram GPU NVIDIA e encerraram o AVD. Tablet mostra página 2 ao lado da 1; telefone/TV não mostram; navegação por pares e página final ímpar aprovadas.
- `QUALITY BAR`: `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL`; XML da suíte JVM: 1.615 testes, 0 falhas, 0 erros, 0 ignorados.
- `GRAFO`: `graphify update .` — 11.220 nós, 29.411 arestas, 496 comunidades; `graphify explain` liga o compositor, `BookReaderScreen()` e os testes de política/Compose. Permanecem avisos conhecidos de 1 arquivo parcialmente parseado, 28 arquivos sem símbolos e 83 recursos sem classificação; Graphify 0.9.79 instalado versus skill 0.9.84.
- `LIMITAÇÕES`: fixtures CBZ geradas nos testes; não valida quadrinhos reais, desempenho em aparelho físico, TalkBack manual, nem CBR real do servidor. Nenhuma release, portal ou alteração de servidor.

## RED-GREEN — idioma da narração TTS no leitor paginado — 2026-10-10

- `INTENT`: oferecer nos leitores PDF/texto paginado uma escolha entre idiomas disponíveis no mecanismo TTS, com padrão do aparelho e preferência persistida por servidor/conta; carregar idiomas somente quando solicitado.
- `RED`: teste Compose `textBookNarrationLanguageCanBeSelectedAndResetToDeviceDefault` foi adicionado primeiro e falhou no PHONE/API 35 porque a ação `book-speech-language` não estava visível.
- `GREEN`: controlador inicializa TTS sob demanda, carrega opções instaladas, aplica a escolha e reinicia o trecho ativo ao alterar idioma; preferência local no DataStore é normalizada e isolada por servidor/conta. Cobertura unitária de inicialização preguiçosa, aplicação antes da fala, reinício/descartar callback obsoleto e persistência/isolamento.
- `DEVICE`: `:feature:item-detail:compileDebugAndroidTestKotlin` passou; `BookReaderScreenIntegrationTest.textBookNarrationLanguageCanBeSelectedAndResetToDeviceDefault` passou 1/1 no PHONE/API 35. O teste abriu o diálogo, selecionou inglês, verificou DataStore/estado e voltou ao idioma padrão; wrapper confirmou GPU NVIDIA e encerrou o AVD.
- `GATE`: `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin :app:compileDebugAndroidTestKotlin` — `BUILD SUCCESSFUL`; 1.606 testes JVM, 0 falhas/erros/ignorados. `git diff --check` aprovado.
- `LIMITAÇÕES`: teste com engine falso e sem áudio/motor real; idiomas efetivamente instalados e qualidade da voz variam por dispositivo. Sem release, servidor ou portal.

## RED-GREEN — recuperar idioma TTS salvo que deixou de estar disponível — 2026-10-10

- `INTENT`: o seletor atual falha a inicialização quando não consegue aplicar o locale salvo; o teste novo exige fallback ao locale do aparelho, remoção da preferência obsoleta e abertura do seletor; `TODO-APP.md` promete a opção padrão e idiomas efetivamente disponíveis.
- `RED`: `BookSpeechPlaybackControllerTest.unavailable saved language falls back to device default and keeps selector available` falhou antes da correção: requisição de idiomas retornou falha e não aceitou o padrão.
- `GREEN`: adaptador Android resolve suporte antes de aplicar locale e tenta padrão; controlador, se a preferência antiga falhar, seleciona padrão, limpa a preferência via ViewModel/DataStore e conclui a consulta de idiomas.
- `UNIT`: `BookSpeechPlaybackControllerTest` e `BookSpeechLocalePolicyTest` passaram após GREEN; a política cobre locale salvo instalado, fallback do aparelho, tag inválida e nenhum locale compatível.
- `DEVICE`: `BookReaderScreenIntegrationTest.staleSavedNarrationLanguageCanBeRecoveredFromTheLanguageSelector` passou 1/1 no PHONE/API 35; restaurou `it-IT` obsoleto, abriu o seletor, confirmou idioma padrão e persistência removida. AVD usou GPU NVIDIA e foi encerrado pelo wrapper.
- `GATE`: `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin :app:compileDebugAndroidTestKotlin` — `BUILD SUCCESSFUL`; 1.611 testes JVM, 0 falhas/erros/ignorados; `git diff --check` aprovado.
- `LIMITAÇÕES`: mecanismo e saída de áudio reais variam por dispositivo; validado com engine falso no teste Compose e política executável, sem voz física. Sem release, servidor ou portal.

## RED-GREEN — busca de conteúdo EPUB via Readium — 2026-10-10

- `INTENT`: estender a busca adicionada ao leitor de texto para EPUB, usando o SearchService real da publicação e permitindo saltar para uma ocorrência em outro capítulo.
- `RED`: o teste novo foi escrito antes da implementação, mas sua primeira execução instrumentada ocorreu depois da alteração; portanto não há execução baseline pré-implementação registrada. Na primeira execução, a asserção procurou uma frase contínua no rótulo, porém Readium separa contexto em `before/highlight/after`; corrigido o critério para termo único e navegação ao segundo capítulo.
- `GREEN`: `searchBookPublication` consome páginas do SearchIterator, define busca sem distinção de caixa/diacríticos, limita a 100 locators e fecha o iterator em `finally`. Diálogo apresenta snippet acessível, navega com Locator e trata exceção com mensagem recuperável.
- `DEVICE`: `BookReaderScreenIntegrationTest.epubSearchFindsAndNavigatesToMatchingText` passou 1/1 e `BookTextSearchDialogTest` passou 2/2 no PHONE/API 35, AVD `SmartMeasureApi35Play`, wrapper confirmou GPU NVIDIA e encerrou o emulador. Compilação AndroidTest e gate `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin :app:compileDebugAndroidTestKotlin` passaram (`BUILD SUCCESSFUL`).
- `UNIT / GRAFO`: gate executou 1.596 testes JVM, 0 falhas/erros/ignorados. `graphify update .` atualizou o grafo Android para 11.033 nós, 28.858 arestas e 519 comunidades; query confirmou nós ligados de `BookReaderScreen()`, `searchBookPublication()`, `BookTextSearchDialogTest` e teste EPUB de integração. Avisos Graphify preexistentes: extração parcial de `TvHomeRefreshIntegrationTest.kt:53`, 28 arquivos sem símbolos e CLI 0.9.79 versus skill 0.9.84; `graphify label` exige configuração LLM não disponível e não altera a extração.
- `LIMITAÇÕES`: cobertura do EPUB de fixture do Readium, sem livro real servido pelo Mulletaflix; PDF, quadrinhos e demais documentos binários seguem sem busca textual quando não expõem conteúdo indexável. Sem release, servidor ou portal.

## RED-GREEN — busca textual em PDF — 2026-10-10

- `INTENT`: permitir localizar texto selecionável em PDFs sem OCR, aproveitando a extração por página já suportada pelo Android e navegando até a página correspondente.
- `RED`: `BookPdfTextSearchTest` foi executado antes da implementação e falhou na compilação por ausência de `BookPdfTextSearch` (RED esperado).
- `GREEN`: a busca verifica uma página por vez, ignora caixa e diacríticos pela política comum, limita a 100 resultados, não lê páginas para consulta vazia e respeita cancelamento entre páginas. O botão só aparece quando o sistema oferece extração PDF; selecionar resultado pausa a narração e abre a página.
- `UNIT`: `:feature:item-detail:testDebugUnitTest --tests org.mulletaflix.feature.itemdetail.BookPdfTextSearchTest` — `BUILD SUCCESSFUL`; quatro casos aprovados.
- `DEVICE`: `BookReaderScreenIntegrationTest.pdfSearchFindsAndNavigatesToMatchingPageWhenPlatformSupportsExtraction` passou junto com a classe de integração do leitor: 14/14 em PHONE/API 35; 0 falhas, erros ou ignorados. A fixture PDF foi servida por MockWebServer e a seleção moveu a leitura para a página 2. O wrapper confirmou GPU NVIDIA e encerrou o AVD.
- `QUALITY GATE`: `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin :app:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL`; relatórios JVM do projeto totalizam 1.600 testes, 0 falhas/erros/ignorados; `git diff --check` passou.
- `LIMITAÇÕES`: sem OCR; PDF baseado somente em imagem não é pesquisável. Em sistemas sem extração nativa compatível, a ação não é exibida. Nenhuma release, portal ou mudança no servidor.

## RED-GREEN — busca local no leitor de livros — 2026-10-10

- `INTENT`: Leitor não permite localizar termos nos formatos de texto já carregados; o pedido contínuo de melhorar funcionalidades pede busca interna; busca local por trechos deve permitir localizar e abrir trecho sem acessar servidor.
- `RED`: `BookTextSearchTest` primeiro não compilou sem a política; após adicionar um retorno vazio provisório, ambos os testes falharam em assertions: termo com acento/capitalização não era encontrado e limite de resultados não funcionava.
- `GREEN`: política local normaliza caixa e diacríticos, ignora consulta vazia e limita resultados a 100. Diálogo Compose executa busca fora da UI, lista resultados virtualizados e abre o trecho selecionado; botão só aparece para livros expostos como texto paginado.
- `DEVICE`: `BookTextSearchDialogTest.searchResultOpensMatchingChunk` passou 1/1 em PHONE/API 35, TABLET/API 35 e Android TV/API 34. Cada AVD foi iniciado/encerrado por `tools/with-emulator.ps1`; QEMU confirmado na NVIDIA RTX 3050.
- `LIMITAÇÕES`: busca abrange formatos diretos de texto paginado (TXT, HTML, Markdown, RTF, FB2, MOBI, DOCX e ODT); não pesquisa dentro do conteúdo EPUB Readium, PDF ou páginas de quadrinhos. Máximo de 100 trechos por busca. Nenhuma release, portal ou alteração no servidor.

## Cobertura integrada — autorização concluída do Quick Connect — 2026-10-10

- `INTENT`: comprovar a jornada integrada da Activity principal desde o início do Quick Connect até a persistência da sessão e a Home, usando servidor HTTP sintético e credenciais fictícias.
- `RED / DIAGNÓSTICO`: novo `MainActivitySessionNavigationTest.quickConnectAuthorizationPersistsSessionAndOpensHome` compilou, mas a primeira fixture retornou `{"Authenticated":true}` sem `Code` e `Secret`; o DTO Retrofit exige ambos, então o fluxo não passou da consulta. A falha foi da fixture incompleta, não evidência de defeito no aplicativo.
- `GREEN`: corrigida a resposta de `QuickConnect/Connect` para cumprir o DTO (`Code`, `Secret`, `Authenticated`). O teste passou: PIN iniciado, polling executado, `POST /Users/AuthenticateWithQuickConnect`, sessão gravada (`token`, `userId`, `serverId`) e navegação para Home. PHONE/API 35, TABLET/API 35 e TV/API 34, 1/1 em cada perfil, 0 falhas/erros/ignorados.
- `QUALITY GATE`: teste focado `:app:connectedDebugAndroidTest -Pandroid.testInstrumentationRunnerArguments.expectedDeviceProfile=<PHONE|TABLET|TV> -Pandroid.testInstrumentationRunnerArguments.class=org.mulletaflix.android.MainActivitySessionNavigationTest#quickConnectAuthorizationPersistsSessionAndOpensHome --no-daemon --console=plain` — `BUILD SUCCESSFUL` nos três AVDs; cada wrapper confirmou QEMU na NVIDIA RTX 3050 e encerrou o emulador após o uso. Gate global `testDebugUnitTest :feature:auth:lintDebug :app:lintDebug :app:assembleDebug :app:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL`; XMLs JVM: 1.532 testes, 0 falhas/erros/ignorados.
- `AVALIAÇÃO`: nenhuma mudança no código de produção foi necessária; Quick Connect tem testes unitários para autorização/falhas e agora cobertura Activity ponta a ponta com MockWebServer. A fixture corrigida também documenta o contrato HTTP esperado.
- `LIMITAÇÕES`: confirmação ainda não executada em servidor Mulletaflix de homologação com conta real ou dispositivo físico. Nenhuma release, portal ou servidor alterado.

## Investigação — harness para morte do processo principal do leitor — 2026-10-10

- `INTENT`: validar em PHONE que o progresso do leitor e a sessão sobrevivem ao encerramento real do processo `org.mulletaflix.android.debug`, com nova Activity e backend HTTP determinístico.
- `EXPERIMENTO`: o teste integrado foi temporariamente ampliado para avançar um livro servido por MockWebServer, fechar a Activity, executar `am force-stop` no pacote principal e relançar o mesmo deep link. `:app:compileDebugAndroidTestKotlin` passou; execução PHONE/API 35 terminou em `connectedDebugAndroidTest FAILED`, XML `tests=1 failures=1 errors=0 skipped=0`, tempo 6,215 s, sem mensagem nem stack trace.
- `AVALIAÇÃO`: o comando destrutivo foi disparado pelo runner que instrumenta o próprio APK principal; portanto a execução não separa com segurança a morte do app do ciclo de vida do runner. O resultado não comprova persistência nem defeito de produção. A prova inadequada foi removida e a cobertura anterior de `ActivityScenario.recreate()` foi restaurada.
- `REGRESSÃO`: `MainActivitySessionNavigationTest.authenticatedBookDeepLinkOpensReaderAndSurvivesActivityRecreation` passou novamente 1/1 em PHONE/API 35; XML confirma 0 falhas, 0 erros, 0 ignorados. Quality gate `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :app:compileDebugAndroidTestKotlin --no-daemon --console=plain` passou; os XML JVM totalizam 1.594 testes, 0 falhas/erros/ignorados.
- `GRAFO`: após atualizar documentação e estado final, `graphify update .` regenerou 10.986 nós, 28.696 arestas e 489 comunidades. `explain BookReaderProgressProcessRestartTest` confirmou o harness isolado e a fixture HTTP com fases separadas; `explain authenticatedBookDeepLinkOpensReaderAndSurvivesActivityRecreation` confirmou que a cobertura da Activity principal é apenas recriação da Activity. Graphify reportou que `graphify label` é necessário para atualizar os nomes das comunidades alteradas.
- `PRÓXIMO PASSO`: criar/usar um orquestrador de teste independente do pacote principal (runner externo + fixture HTTP persistente entre fases), que capture PID antes/depois, sobreviva ao `force-stop` e restaure a sessão de teste sem limpar dados do usuário.
- `STATUS`: APK-H21 permanece pendente; nenhuma mudança de produção, release, portal, servidor ou dados de usuário.

## Harness independente para cold start da Activity do leitor — 2026-10-11

- `INTENT`: fechar a lacuna APK-H21 com um teste host-orquestrado que mate o processo principal entre fases instrumentadas, relance o deep link de livro e confirme que a rota HTTP real foi consultada.
- `RED`: primeira execução real no PHONE/API 35 passou na preparação e confirmou morte do PID principal (PID 3533), mas a fase do deep link falhou com `IllegalArgumentException: Missing instrumentation argument: bookColdStartSessionSnapshot`. A saída `println` do teste não chegou ao host via `adb shell am instrument -w`; o orquestrador não tinha snapshot para cleanup, então a sessão anterior do AVD pode ter sido substituída pela sessão fictícia. O wrapper fechou o AVD sem limpar dados; não repetir até o usuário escolher como tratar o AVD.
- `FIX DO HARNESS`: captura separada e somente leitura agora precede a gravação de qualquer sessão fixture; usa `Instrumentation.sendStatus` para retornar snapshot, host exige captura antes de iniciar fase mutável, mascara o valor nos logs e restaura em `finally`. Não usa `pm clear`; `force-stop` fica restrito ao pacote debug.
- `DEVICE / RED`: na AVD temporária isolada `MulletaflixColdStartTempApi35_20261011`, primeira captura caiu (`Process crashed`) sem modificar a sessão. Na repetição com diagnóstico de AndroidRuntime, captura passou 1/1 e preparação passou 1/1; `finally` restaurou a sessão vazia 1/1. O host então falhou porque pressupunha que salvar a sessão iniciava o processo principal (nenhum PID); portanto o cold start ainda não ocorreu. Wrapper confirmou QEMU na RTX 3050 e fechou a AVD. APKs de teste instalados sem limpar dados.
- `DEVICE / EXECUÇÃO POSTERIOR`: corrigida inicialização explícita para o componente `org.mulletaflix.android.MainActivity`; o ciclo PHONE/API 35 confirmou captura, preparação, PID 3493 encerrado, PID principal 3566 reportado pela Activity, deep link e conteúdo do livro visíveis, restauração da sessão 1/1. A execução inteira ainda terminou com exit 1 porque o host exigiu `/BookReader/Status`, rota não solicitada nesse carregamento rápido.
- `SURPRESA`: `BookReaderViewModel.fetchBookReaderBody()` atrasa a consulta de status por 1 s e cancela o monitor quando o corpo EPUB chega. Portanto a rota Status é best-effort e não é contrato garantido de cold start; manter essa expectativa transforma leitura bem-sucedida em falso negativo. Não foi alterada após três ciclos de correção/validação do mesmo harness.
- `AVD ORIGINAL`: primeira tentativa no `MulletaflixApi35` alterou possivelmente a sessão salva antes de falhar; snapshot anterior não pôde ser recuperado. Nenhum `pm clear` ou nova execução foi feita nele. Aguardar confirmação do usuário antes de tocar nessa AVD.
- `BUILD`: `testDebugUnitTest :app:lintDebug :app:assembleDebug :app:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL`; XML: 1.613 testes, 0 falhas/erros/ignorados. Parser PowerShell, `node --check` e `git diff --check` também passaram após a última alteração.
- `QUALITY GATE`: `testDebugUnitTest :app:lintDebug :app:assembleDebug --no-daemon --console=plain` — `BUILD SUCCESSFUL` (861 tarefas, 3 executadas, 858 up-to-date); relatórios XML: 1.613 testes, 0 falhas, 0 erros, 0 ignorados. `git diff --check` passou.
- `DEVICE`: AVD isolada temporária usou NVIDIA RTX 3050; captura/restauração do snapshot confirmadas. A AVD original não foi repetida e aguarda decisão do usuário.
- `GRAPHIFY`: `graphify update .` atualizou o grafo Android para 11.207 nós, 29.368 arestas e 501 comunidades; `graphify explain MainActivityBookColdStartTest` confirmou relações com repositório de sessão e fases capture/prepare/restore. CLI 0.9.79 vs skill 0.9.84; labels atualizados por hub, sem backend LLM.
- `AVALIAÇÃO`: cold start da Activity e renderização do livro passaram na AVD isolada, com PID novo e sessão restaurada. A automação permanece vermelha pelo requisito indevido de `/BookReader/Status`; não contar o harness como aprovado. Não reutilizar `MulletaflixApi35` até Raphael orientar sobre a possível sessão sintética.
- `PRÓXIMO`: aguardar orientação sobre `MulletaflixApi35`. Em ciclo futuro, revisar o contrato do harness: exigir somente as rotas necessárias à renderização, ou atrasar a fixture se Status for requisito explícito; depois rerodar instrumentação e quality gate. Snapshot deve ser recebido antes de mutar sessão.
- `ESCOPO`: somente APK/testes Android; sem mudança de produção, bump, release, portal ou servidor. A UI do livro abriu após cold start, mas o script encerrou com falha por rota Status ausente; AVD original ainda pode ter sessão sintética.

## Cobertura instrumentada — manter Quick Connect ativo ao recriar Activity — 2026-10-10

- `INTENT`: ampliar a cobertura de Activity real para garantir que a aba Quick Connect, PIN e polling do `AuthViewModel` sobrevivam à recriação sem gravar segredo em armazenamento durável.
- `BASELINE`: a inspeção de `HEAD` confirmou que a implementação já usa `rememberSaveable` para a aba e não cancela o ViewModel ao descartar a composição; nenhuma mudança de produção foi necessária nesta tarefa.
- `TESTE`: teste instrumentado `MainActivitySessionNavigationTest.quickConnectCodeAndPollingSurviveActivityRecreation` passou 1/1 em PHONE/API 35. Com servidor HTTP sintético local, verifica PIN visível após recriação e uma nova consulta `GET /QuickConnect/Connect` com o mesmo segredo. Duas tentativas mais amplas anteriores falharam depois da recriação, na simulação da etapa de autorização; essa parte foi retirada do escopo do teste, que valida somente preservação e continuidade do polling.
- `QUALITY GATE`: `testDebugUnitTest :feature:auth:lintDebug :app:lintDebug :app:assembleDebug :feature:auth:compileDebugAndroidTestKotlin :app:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL`; 1.594 testes JVM, 0 falhas/erros/ignorados; lint, build e compilação de AndroidTests aprovados.
- `TWINS`: `rg` por cancelamento de Quick Connect achou os handlers intencionais ao voltar à aba Entrar e ao cancelar explicitamente; nenhum `DisposableEffect` cancela durante descarte/recriação de composição.
- `GRAPHIFY`: `graphify update .` após teste e documentação — 10.985 nós, 28.694 arestas, 474 comunidades; `explain AuthViewModel` confirmou relações com `LoginScreen`, NavHost, polling e testes; `explain LoginScreen` confirmou uso do ViewModel e rota. CLI 0.9.79 vs skill 0.9.84; aviso de parse preexistente em `TvHomeRefreshIntegrationTest.kt:53`, 83 arquivos não classificados e 28 sem símbolos.
- `LIMITAÇÕES`: essa verificação cobre recriação de Activity e continuidade do polling em PHONE/API 35, não aprovação/expiração contra servidor real, fechamento explícito da tela, TABLET, Android TV ou hardware. Sem release, APK de produção, servidor ou portal.

## RED-GREEN — preservar rascunho de solicitação de mídia — 2026-10-10

- `INTENT`: HomeScreen uses `remember` for request-dialog visibility and `rememberSaveable` for title, type, year and notes; the integration test expects an open dialog and its draft to survive saved-instance-state restoration; the intended behavior is to restore UI state without durable storage or automatic resubmission.
- `RED`: `:feature:home:connectedDebugAndroidTest -Pandroid.testInstrumentationRunnerArguments.expectedDeviceProfile=PHONE -Pandroid.testInstrumentationRunnerArguments.class=org.mulletaflix.feature.home.HomeMediaRequestDraftRestorationTest` — falhou pela razão esperada: após restauração, o diálogo não existia e o seletor `media-request-title` não encontrava nó.
- `GREEN`: o teste falhou primeiro como esperado: após `emulateSavedInstanceStateRestore()`, o nó `media-request-title` não existia porque o diálogo fechava. `rememberSaveable` aplicado à visibilidade fez o teste passar 1/1 em PHONE/API 35, TABLET/API 35 e TV/API 34; título, tipo, ano, observações e diálogo aberto foram restaurados. A mesma prova confirma que Cancelar continua fechado após nova restauração e que reabrir recupera o rascunho.
- `QUALITY GATE`: `testDebugUnitTest :feature:home:lintDebug :app:lintDebug :app:assembleDebug :feature:home:compileDebugAndroidTestKotlin :app:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL` (881 tarefas acionáveis; 32 executadas, 849 atualizadas); 1.594 testes JVM em 242 relatórios, 0 falhas/erros/ignorados. Lint, assemble e compilação dos testes instrumentados aprovados.
- `GRAFO`: `graphify update .` — 10.990 nós, 28.711 arestas, 494 comunidades; consulta `graphify query "HomeMediaRequestDraftRestorationTest requestDraftSurvivesSavedInstanceStateRestoration HomeScreen"` localizou teste, tela e diálogo. Avisos conhecidos: parser parcial em `TvHomeRefreshIntegrationTest.kt:53`, 83 arquivos sem classificação e 28 sem símbolos; renomeação das comunidades requer `graphify label`.
- `GRAPHIFY`: `graphify update .` — 10.980 nós, 28.675 arestas, 489 comunidades; gerados `graphify-out/graph.json`, `graph.html` e `GRAPH_REPORT.md`. Consulta `graphify query "HomeMediaRequestDraftRestorationTest HomeScreen rememberSaveable" --budget 700` localizou o teste novo (linha 31), `HomeScreen()` e `rememberSaveable`. Avisos: CLI 0.9.79 usa skill 0.9.84; parser preexistente reporta sintaxe em `TvHomeRefreshIntegrationTest.kt:53`; 83 arquivos não classificados e 28 sem símbolos. A atualização extrai código; não faz extração semântica dos Markdown.
- `LIMITAÇÕES`: o teste usa `StateRestorationTester` para o mecanismo Compose de estado salvo; não comprova encerramento forçado do processo, persistência durável, envio ao servidor ou hardware físico. O formulário fechado voluntariamente segue fechado; progresso/resultado de envio não são persistidos nem repetidos. Sem versão/release/portal/servidor.

## RED-GREEN — ano obsoleto após selecionar sugestão de mídia — 2026-10-10

- `INTENT`: HomeScreen updated request year only when suggestion.year was non-null; the integrated UI test expects absent suggestion year to clear manual input; optional year must not submit stale data.
- `RED`: `HomeMediaRequestDraftRestorationTest.selectingSuggestionWithoutYearDoesNotSubmitPreviouslyEnteredYear` passou pela UI real, recebeu sugestão sem ano e falhou na asserção de envio: esperado `null`, recebido `2024`.
- `GREEN`: ao selecionar sugestão, `requestYear` agora recebe `suggestion.year?.toString().orEmpty()`; formulário enviou `year = null`. A classe instrumentada passou 2/2 em PHONE/API 35, TABLET/API 35 e TV/API 34, sem falhas/erros/ignorados.
- `QUALITY GATE`: `testDebugUnitTest :feature:home:lintDebug :app:lintDebug :app:assembleDebug :feature:home:compileDebugAndroidTestKotlin :app:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL` (881 tarefas acionáveis, 32 executadas e 849 atualizadas); 1.594 testes JVM em 242 XML, 0 falhas/erros/ignorados. Sem versão/release/portal/servidor.

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
- `TWINS`: nota histórica retificada após nova inspeção: `LibraryScreen`, `FavoritesScreen` e `LiveTvScreen` já encaminham retomada aos respectivos `refreshOnResume`; a afirmação de que estavam sem alteração ficou desatualizada. Testes atuais incluem `LibraryViewModelTest`, `FavoritesViewModelTest`, `LiveTvViewModelTest`, `TvRefreshEffectTest` e `LiveTvRefreshEffectTest`; essa verificação estática não declara nova execução instrumentada nesta tarefa.
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
3. **SyncPlay realtime:** cobertura distribuída entre `SyncPlayRealtimeClientTest`, `SyncPlayRealtimeGroupSwitchTest`, `SyncPlayRealtimeMessageTest`, `SyncPlayReconnectPolicyTest` e `SyncPlayCleartextPolicyTest`. Start/retry de leitura de sessão, cancelamento antes da URL, troca de sala, parser, reconexão após falha de handshake e rejeição de geração/socket obsoletos têm cobertura. Novo Android-independent integration test com MockWebServer prova `onOpen` e `onMessage` chegando como `Connected` e `Command` pelo fluxo público `events` (1/1 em 13/10/2026). Ainda falta integrar `onClosed`/`onFailure`, emissão de `Disconnected` e cancelamento de reconnect após saída; não declarar esta frente totalmente fechada.
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
5. **Build/package APK:** metadados `applicationId`, `versionName` e `versionCode` são validados por `aapt dump badging` contra a configuração antes de assinar/copiar. Pester 3.4: 4 testes unitários de metadados e integração `Build-AppPackage.Tests.ps1` (1/1, revalidada em 13/10/2026) rejeita APK sintético inválido pelo `aapt` do SDK instalado e prova que hashes/conteúdo dos dois destinos preexistentes permanecem intactos. Portanto a lacuna de proteção contra sobrescrita está coberta; a fixture usa Android SDK real, não stub. APK previamente existente foi inspecionado como `org.mulletaflix.android` / `1.3.82` / `382`. Nenhum APK válido foi empacotado/assinado nesta verificação e nenhuma release foi gerada.
6. **Fluxos visíveis de features:** `LiveTvScreen` deve enviar o ID correto ao iniciar canal/gravação; `ProfileScreen` deve refletir confirmação de logout/troca de conta; Downloads devem sobreviver a reinício/retomar e abrir arquivo offline; Login/registro devem demonstrar validação, erro e sucesso; SyncPlay deve conectar ações da tela, eventos e estado final.

### P2 — persistência, conectividade e UX adaptativa

1. `SearchHistoryRepositoryImpl`: persistência, isolamento por usuário **e servidor**, limite/ordem, duplicatas, remoção, limpeza por servidor e limpeza total por usuário entre servidores; JSON corrompido. O escopo servidor/conta agora tem RED-GREEN nesta rodada: servidores diferentes isolam o histórico, LAN/URL pública com o mesmo ID estável compartilham, e “limpar dados locais” remove todos os escopos do usuário. Dados antigos sem identidade de servidor não são migrados automaticamente porque sua origem não pode ser inferida; a limpeza total também apaga a chave legada.
2. `HomeFeedCacheRepositoryImpl`: identidade fallback recebeu RED/GREEN em 13/10/2026: HTTPS sem porta e `:443` agora compartilham identidade; HTTP `:80` equivale à porta implícita e `:8443` permanece distinto. `HomeFeedCacheScopeTest` passou 5/5. Em 13/10/2026, `HomeFeedCacheRepositoryPersistenceTest` passou 4/4 em PHONE/API 35 com Preferences DataStore real: snapshot sobrevive a recriação do DataStore e repositório, JSON inválido é removido e substituível, escrita de sessão inativa não sobrescreve a sessão ativa e limpeza obsoleta preserva gravação posterior. A injeção do DataStore foi extraída para provider Hilt que retorna a mesma store singleton do contexto; não houve mudança semântica no cache. A primeira tentativa JVM no Windows falhou ao renomear o arquivo temporário da Preferences DataStore; a validação foi movida para AndroidTest, onde o filesystem Android passou. Esta cobertura é caracterização da implementação existente e não tem RED comportamental; não alegar TDD RED/GREEN para os quatro cenários. Processo Android morto/reiniciado, armazenamento degradado e falha real de I/O continuam fora deste teste.
3. `ConnectivityNetworkMonitor`: RED-GREEN comprova que rotas Wi-Fi/Ethernet locais sem `NET_CAPABILITY_INTERNET` contam como acessíveis e que o `NetworkRequest` não exige Internet (instrumentado 1/1 em PHONE/API 35). Após revisão, callbacks não consultam capabilities/medição sincronamente: API 26+ usa capabilities entregues; API 24–25 agenda reconciliação posterior ao callback. Teste unitário verifica fronteira 24/25/26/35. Em 13/10/2026, `ConnectivityNetworkCallbackTest` passou 3/3 em PHONE/API 24 e 3/3 em PHONE/API 35; novo teste deriva SDK real e observa a chamada do fallback antigo, sem alteração de produção nem alegação de RED. Múltiplas redes, roteador LAN real e desregistro ao cancelar Flow seguem sem teste de integração; API 25 ainda não foi executada.
4. `CheckAppUpdateUseCase`: atualização disponível/indisponível, falha de rede/IO do repositório, validação de versão não-branca e normalização de espaços e prefixos `v`/`V`. Coberto por RED-GREEN em 13/10/2026 (`CheckAppUpdateUseCaseTest`, 5/5 aprovados; suíte global 1.746 testes JVM em 253 XML, 0 falhas).
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
- `LIMITAÇÕES`: nenhuma release ou APK de produção foi gerado. A configuração global de cleartext permanece pendente (APK-H12); testes de rota controlada não substituem ensaio de rede real.

### TDD — OCR local para PDFs digitalizados (APK local; sem release; 2026-10-12)

- `INTENT`: PDFs compostos por imagens devem poder ser pesquisados e narrados no leitor Android, além dos formatos de livro previamente cobertos. O usuário autorizou incluir OCR empacotado/offline.
- `RED`: `:feature:item-detail:testDebugUnitTest --tests "org.mulletaflix.feature.itemdetail.PdfOcrAvailabilityTest" --no-daemon --console=plain` falhou antes da implementação porque a política de extração nativa desabilitava as funções para API 24–34.
- `IMPLEMENTAÇÃO`: `com.google.mlkit:text-recognition:16.0.1` bundled Latin. `PdfBookDocument` mantém texto nativo como caminho preferido; se a página não possui texto selecionável, renderiza somente essa página até 2048 px por lado e usa ML Kit local. Texto reconhecido é limitado ao limite existente de 64 KiB e cache LRU de três páginas; Bitmap é reciclado e o recognizer aguarda reconhecimento ativo antes de fechar. Nenhum texto OCR é persistido nem enviado ao servidor.
- `GREEN`: teste unitário de disponibilidade em Android minSdk 24+ aprovado. O teste instrumentado TABLET/API 35 com PDF de página rasterizada passou 1/1, `failures=0`, `errors=0`, `skipped=0`: reconheceu texto offline e encontrou a consulta pela página certa via `BookPdfTextSearch`. Repetição após refinamento do ciclo de vida também passou 1/1; wrapper confirmou GPU NVIDIA e encerrou o AVD (`test-result-exit-code.txt` = 0).
- `QUALITY GATE amplo`: `testDebugUnitTest ... lintDebug ... assembleDebug ... compileDebugAndroidTestKotlin` — `BUILD FAILED` após 15m21s, 275 testes executados no módulo item-detail, 1 falha: `BookReaderViewModelTest.real AZW3 response loads readable KF8 text through the view model`, `UncompletedCoroutinesError: After waiting for 1m, the test body did not run to completion` (XML `feature/item-detail/build/test-results/testDebugUnitTest/TEST-org.mulletaflix.feature.itemdetail.BookReaderViewModelTest.xml`). É um teste de caminho MOBI/KF8, não de PDF/OCR; não atribuir sua falha à nova função sem evidência causal.
- `GATE focal`: `:feature:item-detail:testDebugUnitTest --tests "org.mulletaflix.feature.itemdetail.PdfOcrAvailabilityTest" :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin :app:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL` em 8m38s; teste OCR unitário, lint módulo/app, APK Debug local e compilação de AndroidTest módulo/app aprovados. Este não substitui a suíte JVM completa, que segue com a falha acima.
- `LIMITES`: OCR Latin; qualidade depende de resolução, orientação, contraste e idioma. Textos nativos continuam com fidelidade superior; a resolução limitada é trade-off para memória. Ainda não foi feita validação em arquivo real do usuário, PDF multipágina extenso, acessibilidade/TTS acústico, TV, release ou APK de produção. Custo aproximado documentado pelo fornecedor: +4 MB no pacote.
- `ESCOPO`: somente Android/APK, fonte, testes e documentação. Sem bump, release, portal, servidor, commit ou push.

- `REVISÃO FINAL`: suíte ampla falhou somente no teste preexistente de AZW3/ViewModel por `UncompletedCoroutinesError`; não foi corrigido nem atribuído ao OCR sem causalidade. O gate focal acima passou. AVD `MulletaflixApi35` permaneceu intocada; não havia processo do emulador tablet após o wrapper concluir.

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

### TDD — jornada integrada de solicitação de mídia

- `INTENT`: `HomeScreen` abre o formulário, `HomeViewModel` consulta sugestões e valida a sessão, e `UserFeedbackRepositoryImpl` envia o POST autenticado; esta jornada atravessava UI, estado, repositório e HTTP, mas não tinha uma verificação integrada única. O teste espera GET de sugestão seguido por POST com título sugerido, tipo localizado, ano, observações e identidade da sessão. `TODO-APP.md` documenta sugestão/autocomplete e envio de solicitação, deixando a validação contra servidor implantado explicitamente pendente.
- `QUALITY BAR`: compilar o teste instrumentado; executá-lo no PHONE API 35; executar suíte JVM Android, lint e assemble Debug; verificar diff e atualizar Graphify.
- `RED/GREEN`: sem alteração de lógica de produção, portanto não alego teste de comportamento vermelho pré-fix. A primeira execução do teste falhou por tentar rolar um botão fora de contêiner rolável; removida a ação desnecessária, o fluxo alcançou o servidor mockado.
- `EVIDÊNCIA`: `:app:compileDebugAndroidTestKotlin` e quality gate `testDebugUnitTest :app:lintDebug :app:assembleDebug :app:compileDebugAndroidTestKotlin` — `BUILD SUCCESSFUL`; 1.600 testes JVM somados dos XMLs, 0 falhas/erros/ignorados. `HomeMediaRequestJourneyIntegrationTest` — 1/1 no PHONE API 35; Graphify atualizado (11.100 nós, 29.018 arestas, 504 comunidades) e consultas confirmaram ligações do teste com `HomeScreen()` e `UserFeedbackRepositoryImpl`. `git diff --check` passou; lint aponta 0 erros e 142 avisos agregados, sem alertas para o novo teste.
- `ESCOPO`: somente regressão de instrumentação do APK. Sem acesso/alteração ao servidor real, release, versão ou portal.

### TDD — salvar relato de reprodução para envio posterior (APK local; sem release)

- `INTENT`: a tela de detalhes enviava o relato imediatamente, mas numa falha só mostrava erro/retry; o contrato de `PlaybackIssueQueue` documenta persistência sem credenciais e sincronização apenas no escopo da conta/servidor compatíveis. O teste exige salvar o relato para envio posterior e recusar sessão trocada.
- `RED`: `ItemDetailViewModelTest` falhou na compilação antes da implementação por inexistência do parâmetro `playbackIssueQueue` e do método `queuePlaybackIssue`.
- `GREEN`: a ViewModel valida a mesma sessão, item e categoria, limita os campos e grava somente ID, hash de escopo, item, categoria, descrição e horário. Testes unitários cobrem escopo correto, sucesso e sessão trocada.
- `UI`: `PlaybackIssueReportDialogTest` 2/2 em PHONE API 35, verificando falha de envio, ação da fila e ocultação da ação antes da falha. O wrapper confirmou GPU NVIDIA e encerrou o AVD.
- `EVIDÊNCIA`: testes dirigidos e `:feature:item-detail:connectedDebugAndroidTest --tests ...PlaybackIssueReportDialogTest` — `BUILD SUCCESSFUL`, UI 2/2 PHONE API 35. Quality gate `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin :app:compileDebugAndroidTestKotlin` — `BUILD SUCCESSFUL`; 1.602 testes JVM, zero falhas/erros/ignorados. `git diff --check` passou com avisos apenas de conversão LF→CRLF já existentes.
- `GRAPHIFY`: atualizado depois do código e documentação (11.118 nós, 29.103 arestas, 481 comunidades). `graphify explain` confirmou que o diálogo é chamado por `ItemDetailScreen`, coberto pelos dois testes Compose, e que `queuePlaybackIssue` constrói `QueuedPlaybackIssue`.
- `LIMITAÇÕES`: mock HTTP não comprova recebimento pelo servidor real; Graphify instalado é 0.9.79 diante das instruções 0.9.84 e reporta erro de parsing preexistente em `TvHomeRefreshIntegrationTest.kt:53`, 83 arquivos não classificados e 28 sem símbolos.
- `ESCOPO`: somente APK; sem bump, artefato de produção, release, servidor ou portal.
### TDD — consultas concorrentes na busca de livros (APK local; sem release)

- `INTENT`: o diálogo de busca deixava editar o texto mas ignorava nova busca enquanto havia uma ativa e podia manter resultados obsoletos depois da edição; os testes exigem que a nova consulta substitua a anterior, que resultados antigos desapareçam imediatamente e que respostas atrasadas não sobrescrevam a atual. `TODO-APP.md` define a busca local fora da UI, limitada e ligada à consulta submetida.
- `RED`: `newerQueryCanReplaceInFlightSearchAndIgnoreLateResults` falhou esperando a segunda consulta ser executada; `editingQueryClearsResultsFromPreviousQuery` falhou porque o resultado antigo ainda ficava visível ao editar.
- `GREEN`: capturar a consulta submetida, cancelar o trabalho anterior, invalidar callbacks com geração monotônica e limpar resultados ao editar. A busca permanece em `Dispatchers.IO`; erros/resultados/finalização só atualizam a consulta vigente.
- `DEVICE`: `BookTextSearchDialogTest` — 4/4 PHONE/API 35 (`MulletaflixApi35`), 4/4 TABLET/API 35 (`MulletaflixTabletApi35`) e 4/4 Android TV/API 34 (`MulletaflixTvApi34`). Cada wrapper confirmou o uso da GPU NVIDIA e encerrou o AVD.
- `QUALITY BAR`: `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin :app:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL` em 2m29s; 1.611 testes JVM, 0 falhas/erros/ignorados. `git diff --check` passou; avisos apenas de conversão de LF para CRLF.
- `ESCOPO/LIMITAÇÕES`: teste de UI usa fixture Compose e não valida catálogo real. Nenhuma mudança no servidor, portal, versão ou release; build Debug usado somente para testes locais.

## Cobertura de segurança do empacotador de APK — sem release

- `INTENT`: `build-app-package.ps1` valida o APK antes de copiar os destinos; a integração deve rejeitar APK sintético ilegível e preservar os dois artefatos preexistentes sem truncar nem criar arquivos novos.
- `RED/GREEN`: mudança somente em cobertura; não foi observada regressão de produção nem se alega um RED do empacotador. A primeira tentativa de harness falhou porque PowerShell 7.6 não permite `Add-Type` como ConsoleApplication; o teste passou a usar o `aapt` instalado. A execução seguinte expôs newline criado pelo fixture nos arquivos-sentinela; fixture corrigida com `-NoNewline`.
- `EVIDÊNCIA`: `Invoke-Pester -Script .\tools\Build-AppPackage.Tests.ps1 -PassThru` — 1/1 passou. Teste copiou o script e validadores para pasta temporária, usou APK sintético inválido e SDK local, e confirmou conteúdo e SHA-256 inalterados nos destinos `mulletaflix-app-v1.3.83.apk` e `mulletaflix-app.apk`; pasta temporária removida em `finally`. `testDebugUnitTest --rerun-tasks --no-daemon --console=plain` — `BUILD SUCCESSFUL`; 1.611 testes JVM, 0 falhas/erros/ignorados; 470 tarefas executadas.
- `LIMITAÇÕES`: exercita rejeição de APK ilegível, não gera APK binário válido nem simula keystore; testes unitários existentes cobrem incompatibilidade de package, versionName e versionCode. Nenhum APK real foi empacotado ou assinado; sem mudança do servidor, portal, versão ou release.
## Ordenação alfabética da fila de downloads offline — APK, sem release

- `INTENT`: tornar a fila Offline navegável por nome do título nos dois sentidos sem remover a ordenação por uso de armazenamento.
- `RED`: testes de ordenação adicionados antes do código falharam na compilação por ausência de `TitleAscending`/`TitleDescending`; isso confirmou que as opções ainda não existiam. Não houve alegação de falha de runtime anterior.
- `GREEN`: `DownloadStorageOrder` passou a oferecer Título A–Z e Título Z–A. A ordenação pt-BR inclui todos os estados da fila e desempata títulos equivalentes por `downloadId` crescente, sem inverter a estabilidade no modo descendente. O seletor Compose permite ativar ambos.
- `TEST`: `DownloadStorageTest` focal passou com `--rerun-tasks`; `DownloadsSearchTest` instrumentado passou em PHONE/API 35, TABLET/API 35 e TV/API 34 (14 testes por perfil, 0 falhas/erros/ignorados). Os wrappers confirmaram GPU NVIDIA e encerraram os AVDs. XML final da execução TV registra 14/14 aprovados.
- `QUALITY BAR`: `testDebugUnitTest` — 1.613 testes JVM, 0 falhas/erros/ignorados; `:feature:downloads:lintDebug`, `:app:lintDebug`, `:app:assembleDebug` e `:feature:downloads:compileDebugAndroidTestKotlin` — `BUILD SUCCESSFUL`; `git diff --check` aprovado.
- `SCOPE`: somente APK; nenhuma mudança de servidor/portal, versão, assinatura, pacote de produção ou release.

## Aparência configurável no leitor de livros — APK local; sem release — 2026-10-11

- `INTENT`: the reader currently has font-size and speech preferences but no appearance mode; the task asks for new APK functionality, and Readium 3.4.0 documents color overrides for the EPUB renderer, while current PDF/comic renderers are image/document surfaces that should remain unmodified.
- `RED`: `BookReaderScreenIntegrationTest.genericBinaryMimeWithReadableUtf8IsRenderedAsPlainText` falhou em runtime porque o leitor não tinha ação “Aparência da leitura”; erro foi a semântica ausente esperada, não compilação/setup.
- `GREEN`: adicionados Original/Sépia/Escuro, paletas de alto contraste para texto e cores Readium para EPUB, diálogo acessível, atualização dinâmica do controller, persistência DataStore por servidor/conta e isolamento de sessões concorrentes; PDF/quadrinhos continuam sem controle/filtro.
- `DEVICE`: `BookReaderScreenIntegrationTest#genericBinaryMimeWithReadableUtf8IsRenderedAsPlainText` — 1/1 PHONE API 35, 1/1 TABLET API 35 e 1/1 Android TV API 34; cada wrapper confirmou QEMU na GPU NVIDIA RTX 3050 e encerrou o AVD. `BookReaderReadiumIntegrationTest` — 11/11 PHONE API 35, incluindo persistência isolada por conta/servidor e atualização dinâmica das cores EPUB sem perder localização.
- `QUALITY BAR`: `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL`; 1.648 testes JVM, 0 falhas/erros/ignorados; `git diff --check` passou (avisos de normalização LF→CRLF).
- `GRAPHIFY`: `graphify update . --no-cluster` — 64.090 nós/165.230 arestas; `graphify explain "BookReaderAppearance"` confirmou conexões com UI, ViewModel, DataStore e os três modos. Avisos: Graphify 0.9.79 vs instruções 0.9.84, 522 arquivos não classificados, 3 SQL sem parser, 4 extrações parciais e 174 arquivos sem símbolos.
- `LIMITAÇÕES`: EPUBs usados são fixtures, não catálogo real; TalkBack/controle remoto ainda requerem validação manual. Revisores paralelos indisponíveis pelo limite de agentes ativos; revisão adversarial local feita. Sem mudança/release do servidor, portal, versão, assinatura ou APK de produção.

## TDD — mensagem específica para AZW3/KF8 ainda não suportado (APK local; sem release)

- `INTENT`: quando o servidor retorna `application/vnd.amazon.mobi8-ebook`, o leitor rejeita o conteúdo por segurança/compatibilidade, mas mostra o mesmo erro genérico dos demais MIME incompatíveis. O teste exige informar AZW3/KF8 e sugerir EPUB, sem marcar conteúdo parcial como carregado; MIME incompatíveis alheios ao Kindle mantêm a mensagem genérica.
- `RED`: `BookReaderViewModelTest.AZW3 response explains the unsupported format and offers EPUB as an alternative` executou e falhou na comparação da mensagem do estado com a cópia do aplicativo. Uma primeira tentativa isolada de testar uma função ainda inexistente falhou na compilação e foi substituída pelo teste observável do ViewModel; não é contabilizada como RED comportamental.
- `GREEN`: a política central de erro normaliza MIME (caixa e parâmetros), retorna orientação específica para AZW3/KF8 e preserva o erro genérico nos outros MIME; o ViewModel usa a política existente antes de gravar cache/decodificar.
- `TWINS`: searched `application/vnd.amazon.mobi8-ebook` in production reader sources - found 2 decision references (the existing unsupported-format guard and the new specific-message mapper); no duplicate policy. `BookReaderViewModel.load` remains the single message-mapper call site.
- `QUALITY GATE`: teste focal passou após o RED; `./gradlew testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL`; relatórios JVM somam 1.676 testes, zero falhas/erros/ignorados. `git diff --check` aprovado. Nenhum teste instrumentado foi executado.
- `REVISÃO`: revisor adversarial independente não encontrou achados; confirmou normalização de MIME, fechamento do corpo e recusa antes de gravar cache.
- `GRAPHIFY`: `graphify update . --no-cluster` — 64.229 nós e 165.630 arestas; `graphify explain "bookReaderIncompatibleContentTypeMessage()"` confirmou a chamada de `.load()` do ViewModel. Avisos conhecidos: CLI 0.9.79 vs skill 0.9.84, 3 arquivos SQL sem parser, 4 fontes com extração parcial e 174 arquivos sem símbolos; a CLI avisa que documentação Markdown exige fluxo `/graphify --update` no assistente.
- `LIMITE`: não implementa decodificação AZW3/KF8, compressão HUFF/CDIC nem DRM; sugere EPUB apenas condicionalmente. Sem teste instrumentado por AVD reservado pelo usuário.
### Guard contra aplicação de testes Android em dispositivo ADB não selecionado — 2026-10-11

- `INTENT`: o wrapper encerrava somente o AVD que criou, mas o Gradle `connectedDebugAndroidTest` enumerava todos os devices conectados e podia executar também no AVD de uso pessoal preservado pelo usuário. `TESTING.md` exige uso do wrapper e encerramento apenas do AVD criado por ele.
- `RED`: os 3 testes iniciais falharam porque `Assert-AndroidInstrumentationTargetIsolated` não existia. Depois, ao conferir a saída real de `adb devices`, a fixture positiva com a linha final vazia falhou porque o parâmetro PowerShell recusava string vazia.
- `GREEN`: o helper permite linhas vazias de saída, ignora linhas não-dispositivo e exige exatamente um registro: o serial escolhido, no estado `device`. `with-emulator.ps1` aplica a verificação imediatamente antes de invocar Gradle e falha antes da instrumentação se houver outro device; nunca desconecta dispositivos. Chamada direta do helper com a saída real atual foi recusada por identificar `emulator-5572` como adicional ao serial de teste `emulator-5576`.
- `TEST`: `Invoke-Pester -Path .\tools -PassThru` — 27/27, zero falhas/erros/ignorados. Quality Gate `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL`; relatórios XML totalizam 1.648 testes JVM, 0 falhas/erros/ignorados; nenhuma instrumentação foi iniciada nesta verificação.
- `GRAPHIFY`: `graphify update . --no-cluster` — 64.094 nós/165.236 arestas; `graphify explain "Assert-AndroidInstrumentationTargetIsolated()"` encontrou o helper atualizado. Avisos: pacote 0.9.79 vs skill 0.9.84, 522 arquivos não classificados, 3 SQL sem parser, 4 extrações parciais, 174 arquivos sem símbolos.
- `LIMITAÇÕES`: o teste valida o guard com fixtures e com a lista real ADB, mas a integração do wrapper com o Gradle não foi exercitada em dispositivo, pois o AVD `MulletaflixApi35` está reservado à inspeção do usuário. Sem mudança de servidor/portal, versão ou release.

## TDD — rejeitar conteúdo binário RTF truncado — 2026-10-11

- `INTENT`: no leitor RTF, `\\binN` mede bytes originais, não caracteres depois da decodificação; rejeitar tamanhos ausentes/negativos/truncados e preservar conteúdo binário válido, inclusive quando sequência UTF-8 multibyte reduzir a contagem de caracteres.
- `RED`: `rejects a truncated binary payload instead of accepting partial RTF` falhou pela asserção esperada depois de parser ocultar truncamento; `counts binary RTF payload bytes before UTF-8 decoding` falhou em runtime com `IOException` quando dois bytes binários válidos UTF-8 viraram um caractere e o parser consumiu o fechamento do grupo.
- `GREEN`: `preserveBinaryByteOffsets` percorre os bytes RTF, valida `\\binN` antes da decodificação e troca somente bytes binários por espaços ASCII um-para-um; `PlainTextBookDocument.open` usa a saída antes da decodificação. `skipBinaryPayload` também valida comprimento, usa soma `Long` e rejeita truncamento.
- `TWINS`: searched `preserveBinaryByteOffsets` - found 1 production call site: `PlainTextBookDocument.open`; transformação é aplicada somente quando o payload é RTF.
- `QUALITY GATE`: `RtfBookTextParserTest` passou 13/13; `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL` em 2m57s; 248 relatórios XML somaram 1.680 JVM tests, 0 falhas/erros/ignorados. `git diff --check` passou; avisos apenas normalização LF→CRLF.
- `REVISÃO`: Cavecrew revisão adversarial de scanner, tamanho em bytes, payload completo/truncado e controle RTF: sem achados. Uma fixture truncada inicialmente continha bytes suficientes; corrigida para um tamanho declarado acima da entrada, e teste passou.
- `GRAPHIFY`: `graphify update . --no-cluster` — 11.485 nós, 30.346 arestas. `graphify explain preserveBinaryByteOffsets` confirmou chamadas da abertura do documento, teste e TDD. Avisos: CLI 0.9.79 vs skill 0.9.84, 83 arquivos não classificados, 1 extração parcial conhecida e 28 fontes sem símbolos.
- `SCOPE`: APK local; sem servidor, portal, mudança de versão, release ou publicação. Sem instrumentação: parser é coberto por JVM e o AVD permanece reservado ao usuário.

## TDD — charset meta de HTML direto — 2026-10-11

- `INTENT`: Aceitar HTML direto como fallback em texto sem WebView, removendo conteúdo ativo/oculto com varredura linear, respeitando charset declarado; limitar a 2 MiB.
- `RED`: `PlainTextBookDocumentTest.honors HTML meta charset when response has no charset` falhou em runtime ao esperar texto japonês de HTML Shift_JIS; a abertura usava UTF-8/fallback Windows-1252 em vez da declaração `<meta charset>`.
- `GREEN`: prescan em scanner HTML já existente e limitado a 1 KiB; aceita `<meta charset>` e `http-equiv=Content-Type`, exige charset legado na meta correta, ignora comentários/elementos de texto bruto (incluindo title/textarea), continua após rótulo inválido, mapeia apenas rótulos meta UTF-16 para UTF-8 e aliases meta Latin-1/ASCII para Windows-1252, mantendo o charset HTTP literal e precedência do BOM. Não adiciona dependências.
- `TWINS`: searched `htmlMetaCharset` - found 0 other sites: none (one declaration and its single intended decoder call are the only occurrences).
- `TEST`: cinco regressões JVM validam Shift_JIS direto, metadado legado/self-closing, rótulo inválido seguido de válido, declarações falsas em comentário/script/title/description, aliases Latin-1, UTF-16, precedência HTTP e limite de 1 KiB. REDs comportamentais observados para meta ausente, falsa declaração em title e ISO-8859-1 com pontuação Windows-1252; classe focal passou após implementação.
- `QUALITY GATE`: `gradlew.bat testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL`; 248 relatórios somaram 1.685 testes, 0 falhas/erros/ignorados. `git diff --check` passou (avisos somente LF→CRLF). Nenhuma instrumentação executada.
- `REVISÃO`: Cavecrew identificou 13 riscos nas iterações de revisão: falsos positivos em `content`, atributos, nomes de tag inválidos (incluindo pontuação) e elementos de texto bruto title/textarea/plaintext; falha em continuar após label inválido; limite de prescan acima de 1 KiB; mapeamento de UTF-16 e aliases Latin-1/ASCII; e normalização indevida do charset HTTP. Corrigidos com scanner HTML existente e fixtures; confirmação final após leitura do nome até o delimitador HTML pendente.
- `GRAPHIFY`: `graphify update . --no-cluster` atualizou grafo para 11.491 nós/30.368 arestas; `graphify explain PlainTextBookDocument` confirmou decode → sniff de charset → `htmlMetaCharset`. Avisos: Graphify 0.9.79 vs skill 0.9.84, 83 arquivos sem classificação, extração parcial conhecida de teste Android da Home e 28 fontes sem símbolos. Markdown atualizado no TDD não foi semanticamente reextraído.
- `SCOPE`: somente APK local; sem AVD (mantido para inspeção do usuário), alteração de servidor/portal, bump, build de produção, release, commit ou push.
# TDD — decodificação HUFF/CDIC para livros MOBI (APK local; sem release)

- `INTENT`: parser rejeitava compression HUFF/CDIC 17480; teste exige decodificar texto MOBI não protegido; HUFF/CDIC usa registros HUFF/CDIC com tabela de códigos e dicionário de frases, conforme a documentação de formato Mobipocket.
- `RED`: `:feature:item-detail:testDebugUnitTest --tests 'org.mulletaflix.feature.itemdetail.MobiBookTextExtractorTest.reads HUFF CDIC compressed text records' --no-daemon --console=plain` — falhou no caminho de leitura porque o parser ainda rejeitava HUFF/CDIC. Uma falha anterior compilando o fixture foi erro do próprio teste e foi corrigida antes do RED válido.
- `GREEN`: implementação `HuffCdicTextDecoder` interpreta tabela/dicionário, decodifica códigos MSB-first e integra os registros de texto MOBI respeitando o tamanho descompactado declarado. Testes também cobrem blocos independentes, excesso do tamanho declarado e ciclo recursivo de frases.
- `PADDING`: revisão especializada apontou risco de bits finais incompletos; teste RED reproduziu `IOException` num último byte com três bits de padding, e o decoder agora ignora somente o código final incompleto. O limite global do texto declarado continua verificando saída truncada. Focal da classe: 11 testes aprovados.
- `LIMITES`: continuam limites de tamanho do arquivo/dicionário, tamanho máximo de texto, quantidade de entradas/códigos e profundidade/ciclo de frases; DRM e AZW3/KF8 seguem rejeitados.
- `INTENT` declarado: HUFF/CDIC torna-se legível em MOBI sem DRM; não se promete compatibilidade geral sem validar arquivos reais. Consulte [MobileRead MOBI format](https://wiki.mobileread.com/wiki/MOBI) e [kindling-mobi HUFF/CDIC documentation](https://docs.rs/kindling-mobi/latest/kindling/huffcdic/index.html).
- `REVISÃO`: correção importante após conferir a tabela de offsets: ela numera a partir do início do registro 0, incluindo os 16 bytes PalmDOC. Os campos HUFF record/count são 112/116 absolutos; 128/132 são EXTH flags/reservados. Parser e fixture foram ajustados para 112/116 conforme [calibre MOBI format](https://github.com/kovidgoyal/calibre/blob/master/format_docs/pdb/mobi.txt).
- `QUALITY GATE`: `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL`; 248 relatórios JVM somam 1.690 testes, 0 falhas/erros/ignorados. Focal `MobiBookTextExtractorTest`: 11/11. `git diff --check` sem erros de whitespace (após registro final).
- `DEVICE`: nenhum teste conectado; o AVD `MulletaflixApi35` permanece reservado para conferência do usuário, sem inspeção, inicialização ou encerramento.
- `ESCOPO`: somente código/documentação do APK local. Nenhuma mudança de servidor/portal, bump de versão, APK de produção, release, commit ou push.

## RED-GREEN — detectar AZW3/KF8 com MIME genérico (APK local; sem release)

- `INTENT`: KF8/AZW3 enviado com MIME genérico é classificado como MOBI legado; o teste exige identificá-lo e exibir o aviso específico; calibre reconhece KF8 standalone pela versão/índice SKEL e híbrido pelo EXTH 121 apontando após BOUNDARY.
- `RED`: a fixture standalone v8 enviada como `application/octet-stream` foi classificada incorretamente como `PLAIN_TEXT`, sem caminho de erro AZW3/KF8. A revisão identificou novo risco: um `INDX` mínimo, sem estrutura `TAGX`, também seria aceito; um teste com assinatura `INDX` e seção `TAGX` inválida reproduziu o falso positivo. A fixture híbrida cobre EXTH 121 → `BOUNDARY` → header KF8; a flag EXTH foi corrigida para 32 bits antes de contar esse caso como RED válido.
- `GREEN`: MIME genérico agora inspeciona o banco PDB/MOBI; standalone exige MOBI v8 e SKEL apontando a `INDX` cujo header e offset TAGX coincidem, com TAGX alinhado/limitado e marcador EOF; híbrido exige EXTH 121 apontando para header v8 após `BOUNDARY`. O ViewModel orienta conversão para EPUB e deixa publicação/página/texto vazios. Teste negativo rejeita `INDX` com TAGX inválido.
- `TESTES`: `MobiBookTextExtractorTest` cobre standalone, híbrido e MOBI v8 malformado; `BookReaderViewModelTest` cobre o erro final sob MIME genérico. Os 4 testes focais passaram. Quality gate `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL`; 248 relatórios somam 1.694 testes, 0 falhas/erros/ignorados.
- `REVISÃO`: especialistas encontraram falso positivo potencial em MOBI v8, validação superficial de `INDX` e falta de teste do ViewModel; critérios foram endurecidos e a cobertura inclui TAGX presente porém inválido e mensagem real do ViewModel. Revisor final confirmou a fixture/critério TAGX e nenhum novo achado.
- `GRAPHIFY`: `graphify update .` — 11.535 nós, 30.387 arestas, 514 comunidades; `graphify explain MobiBookTextExtractor` confirma `hasKf8Database`/`supportsKf8` conectados ao parser, e `graphify explain BookPayloadFormat` mostra `UNSUPPORTED_AZW3` no classificador. Avisos conhecidos: pacote 0.9.79 vs skill 0.9.84, 83 arquivos não classificados, 1 fonte de Home AndroidTest parcialmente extraída e 28 fontes sem símbolos.
- `REFERÊNCIA`: parser calibre, `headers.py` — leitura da versão/índices KF8, EXTH e sentinel híbrido: https://github.com/kovidgoyal/calibre/blob/master/src/calibre/ebooks/mobi/reader/headers.py . Detecção não implementa reconstrução HTML de AZW3/KF8.
- `DEVICE`: nenhum teste conectado nem inspeção/inicialização/encerramento do AVD `MulletaflixApi35`; reservado para conferência do usuário.
- `ESCOPO`: exclusivamente APK; sem servidor, portal, bump de versão, APK de produção, release, commit ou push.

## RED-GREEN — leitura textual de AZW3/KF8 standalone (APK local; sem release)

- `INTENT`: um AZW3/KF8 standalone, não criptografado, deve abrir texto legível mesmo quando chega com MIME genérico; testar com uma amostra real, além de validar limites e formatos malformados.
- `RED`: `MobiBookTextExtractorTest.reads a real unencrypted AZW3 publication` falhou porque o payload válido era identificado como não suportado antes de extrair conteúdo. Uma tentativa posterior de fazer a mesma prova no ViewModel usando seu salto para `Dispatchers.IO` terminou por timeout no helper de teste (estado continuou carregando); o teste foi removido, não contado como RED funcional, e a integração assíncrona segue aberta.
- `GREEN`: `Kf8HtmlReconstructor` lê índices INDX/TAGX/IDXT, fluxo textual FDST, skeletons e fragments com limites de registros, bytes, entradas e offsets. O extrator reutiliza descompressão MOBI existente e encaminha o HTML reconstruído ao parser plain-text. O classificador aceita o MIME KF8 e bytes identificáveis com MIME genérico; DRM e estruturas inválidas continuam rejeitados. O fluxo híbrido ainda usa fallback legado e está listado como pendência.
- `FIXTURE`: manual AZW3 de teste hospedado pelo projeto calibre, 6.478.933 bytes, SHA-256 `AD9D0490CBB1A544F058D3344AE8D6C874A6ECFFC78D5584A2F5FB2B0EC3F340`; procedência/licença registrada em `feature/item-detail/src/test/resources/books/README.md`. Uso apenas em teste JVM, não incluído no APK.
- `TESTES`: caso real comprova detecção e conteúdo “calibre is an e-book library manager”; fixtures sintéticas mantêm cobertura de MOBI antigo, standalone KF8 malformado e fallback híbrido. O teste no ViewModel usando fixture completa não foi aprovado por timeout, então a leitura ponta a ponta através desse dispatcher ainda não é alegada.
- `TWINS`: searched `Kf8HtmlReconstructor` - production call sites are the MOBI extractor's AZW3 v8 path; class is exercised by `MobiBookTextExtractorTest`. `supportsKf8` remains the shared signature/MIME detector used by format classification and payload limits; no second production reconstruction path.
- `QUALITY GATE`: `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin :app:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL` (4m03s). 248 XML JVM reports: 1.695 tests, 0 failures, 0 errors, 0 skipped. Both lint tasks, Debug assemble, and AndroidTest Kotlin compilation passed. `git diff --check` passed (Git reported only LF→CRLF normalization warnings).
- `GRAPHIFY`: `graphify update .` — 11.555 nós, 30.443 arestas, 509 comunidades; `graphify explain Kf8HtmlReconstructor` mostra `reconstruct()` conectado a `readIndex`, `parseTags`, `parseTagValues` e `readTextFlow`; `graphify explain MobiBookTextExtractor` mostra `extractHtml()`, `supportsKf8()` e `hasKf8Database()`. Avisos: CLI 0.9.79 vs instrução 0.9.84; 84 arquivos ignorados por extensão, uma extração parcial conhecida no teste de atualização da Home TV, e 29 arquivos sem símbolos. Markdown TDD aparece como referência inferida após update, sem extração semântica de documentação.
- `DEVICE`: sem instrumentação nem qualquer controle do AVD `MulletaflixApi35`, mantido até conferência do usuário.
- `PENDÊNCIAS`: testar integração completa no ViewModel/Compose depois de liberar o AVD; dar preferência ao fluxo KF8 em MOBI híbrido; validar imagens, estilos, links e outros arquivos AZW3 reais.
- `ESCOPO`: só código e documentação do APK; sem servidor, portal, versão, APK de produção, release, commit ou push.

## RED-GREEN — priorizar KF8 em MOBI híbrido (APK local; sem release) — 2026-10-12

- `INTENT`: o extrator atual usa os registros do cabeçalho MOBI 7 em arquivos híbridos; o teste novo deve exigir o texto KF8 quando houver um cabeçalho híbrido válido; `TODO-APP.md` registra como pendente priorizar esse fluxo KF8. A documentação do calibre confirma que o cabeçalho KF8 é o registro apontado pelo EXTH 121, precedido por BOUNDARY, e que seus registros de texto começam após esse cabeçalho.
- `RED`: o teste com amostra AZW3 real embrulhada em estrutura híbrida MOBI 7 falhou observavelmente ao retornar `LEGACY MOBI 7 CONTENT` em vez do texto calibre. Após a implementação inicial, o teste híbrido sintético incompleto falhou com “tipo de compressão ... incompatível”; isso expôs seleção prematura de uma estrutura KF8 anunciada, mas inválida.
- `GREEN`: a leitura agora toma compressão, comprimento, charset, paginação, HUFF/CDIC e texto do cabeçalho KF8 selecionado, iniciando texto após o registro do cabeçalho. A seleção exige EXTH 121 e BOUNDARY, cabeçalho v8, FDST e índices de divisão/esqueleto INDX com TAGX validado; estrutura KF8 incompleta mantém o fallback ao texto MOBI 7.
- `TESTES`: o focal com AZW3 real híbrido, o fallback da fixture sintética incompleta e a abertura AZW3 standalone passaram juntos em `:feature:item-detail:testDebugUnitTest` (3 testes, `BUILD SUCCESSFUL`). RED/GREEN ocorreu no extrator; a integração assíncrona pelo ViewModel permanece fora desta afirmação.
- `TWINS`: searched `findHybridKf8HeaderIndex` in produção, testes e documentação: um seletor com dois pontos de chamada em produção (detecção e extração) e dois cenários cobrindo híbrido KF8 real e fallback para híbrido incompleto; sem seletor duplicado.
- `QUALITY GATE`: `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin :app:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL` em 3m59s; 248 relatórios XML, 1.696 testes JVM, 0 falhas/erros/ignorados. Ambos os lint, assemble Debug e compilação AndroidTest do módulo e app passaram. `git diff --check` aprovado; Git informou apenas normalização LF→CRLF.
- `GRAPHIFY`: `graphify query "MOBI hybrid KF8 selection and parsing" --budget 1000` encontrou parser, teste e TODO como referências relacionadas. `graphify update .` — 11.561 nós, 30.475 arestas, 501 comunidades; `graphify explain findHybridKf8HeaderIndex` confirmou chamadas por extração e detecção e ligação à nota TDD. Avisos: CLI 0.9.79 vs instrução 0.9.84, 84 arquivos sem extensão suportada, uma extração parcial conhecida no teste Android Home TV e 29 fontes sem símbolos.
- `REVISÃO`: tentativa de revisão independente via Cavecrew não foi iniciada porque o limite de agentes ativos do ambiente foi atingido; revisão adversarial local verifica fallback malformado, offsets e fontes de cabeçalho. O AVD `MulletaflixApi35` permanece reservado e não foi acessado.
- `ESCOPO`: somente leitor APK e documentação Android. Sem mudança do servidor/portal, versão, assinatura, build de produção, release, publicação, commit ou push.

## Integração real AZW3 pelo ViewModel — cobertura JVM (APK local; sem release) — 2026-10-12

- `OBJETIVO`: cobrir a pendência documentada em `TODO-APP.md`: um AZW3 não criptografado recebido com MIME genérico deve atravessar resposta HTTP, cópia/cache, detecção, reconstrução KF8 e `BookReaderViewModel`, terminando com texto legível no estado.
- `COBERTURA`: novo teste Robolectric usa a fixture real calibre `calibre-user-manual.azw3` por resposta `application/octet-stream`; confirma `isLoading=false`, sem erro, presença da frase do manual em `state.textBook` e ausência de `publication`/`pageBook`.
- `LIMITE DE TEMPO`: o helper compartilhado aguarda 5 s por padrão, mas a leitura real da fixture levou 53,1 s no relatório XML; este caso usa limite específico de 120 s. Não houve alteração de comportamento de produção, então não se declara RED-GREEN de implementação.
- `TESTE FOCAL`: `:feature:item-detail:testDebugUnitTest --tests "org.mulletaflix.feature.itemdetail.BookReaderViewModelTest.real AZW3 response loads readable KF8 text through the view model" --no-daemon --console=plain` — `BUILD SUCCESSFUL`; relatório mostra 1 execução, 0 falhas/erros/ignorados, 53,1 s.
- `QUALITY GATE`: `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin :app:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL` em 5m09s; 248 XML, 1.697 JVM, 0 falhas/erros/ignorados. Lint, assemble Debug e compilação dos testes Android do módulo/app passaram. `git diff --check` sem erros, apenas avisos Git LF→CRLF.
- `GRAPHIFY`: query AZW3/ViewModel encontrou `BookReaderViewModel` e testes relacionados; update resultou em 11.563 nós e 30.483 arestas, 497 comunidades. `graphify explain BookReaderViewModel` confirmou o nó `.load()` e relações com tela/testes; explain do teste confirmou fixture/estado ligado ao ViewModel.
- `DISPOSITIVO`: nenhum AVD ou ADB usado; instrumentação Compose PHONE/TABLET/TV continua pendente porque o telefone `MulletaflixApi35` está reservado.
- `REVISÃO`: Cavecrew/subagentes recusados pelo limite de threads ativos; diff/teste foram revisados localmente, sem alegar crítica independente.
- `ESCOPO`: teste e documentação do APK; sem código de produção, servidor, portal, versão, assinatura, release, publicação, commit ou push.

## Revisão de status — preferência KF8 em MOBI híbrido — 2026-10-12

- `ACHADO`: a seção histórica “Leitura de MOBI/PalmDOC sem DRM” ainda listava como pendente a preferência KF8 em híbridos, mas a implementação e os testes já foram concluídos em seção posterior.
- `EVIDÊNCIA`: `MobiBookTextExtractorTest.prefers KF8 text in a real hybrid MOBI publication` usa o manual calibre AZW3 como fluxo KF8, envolve-o em uma fixture MOBI híbrida e confirma presença do texto KF8 e ausência do texto MOBI 7 legado. `reads the legacy text stream from a hybrid MOBI KF8 when available` mantém cobertura do fallback quando KF8 não está disponível.
- `ATUALIZAÇÃO`: backlog reconciliado. Continua pendente apenas validar amostras reais do usuário/servidor e variações de recursos; nenhum teste foi executado nesta atualização documental.
- `ESCOPO`: documentação Android/APK somente; sem mudanças de código, release, servidor ou portal.

## RED-GREEN — navegação por fragmentos internos HTML/KF8 — 2026-10-12

- `INTENT`: o leitor plain-text descartava `href` ao converter HTML/KF8, então referências internas não moviam o leitor. Links `#fragment` devem resolver `id` e `a[name]` para o chunk visível de destino e expor ação acessível no Compose. Destinos inexistentes, URLs externas e arquivos diferentes não podem abrir conteúdo externo.
- `RED`: `PlainTextBookDocumentTest.maps same-document fragment links to visible anchor IDs and legacy names` foi executado antes da lógica de âncoras e falhou na asserção de links acionáveis: o parser produzia zero. O XML focal está em `feature/item-detail/build/test-results/testDebugUnitTest/`.
- `GREEN`: o scanner HTML preserva atributos dos headings durante a marcação, reconhece âncoras `id`/`name` e entidades HTML, mantém apenas referências `href="#…"` com destino resolvido, mapeia spans para chunks/offsets e limita marcadores a 10.000. A tela cria `LinkAnnotation.Clickable` acessível, seleciona o trecho e rola até a linha do alvo, inclusive no mesmo chunk; o callback de trecho continua persistindo progresso.
- `TESTES`: teste focal JVM de resolução e offset passou. `BookReaderScreenIntegrationTest.plainTextReaderExposesInternalAnchorAsAccessibleLinkAndSelectsTargetChunk` cobre destino em chunk posterior; `plainTextReaderScrollsToAnchorWhenTargetIsInTheSameChunk` cobre rolagem para alvo no mesmo chunk. Ambos compilam no AndroidTest; o teste instrumentado ainda não foi executado em dispositivo.
- `TWINS`: searched 'HTML-to-plain-text href loss / renderHtmlText' - found 0 other sites: HTML direct and MOBI/KF8 share htmlToBookDocument; EPUB uses Readium separately. `mapHtmlLinksToChunks` tem um único mapeador de produção; os demais usos são chamadas/testes.
- `QUALITY GATE`: `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin :app:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL`; 881 tarefas acionáveis, 35 executadas/846 atualizadas; 250 XML, 1.704 testes JVM, 0 falhas/erros/ignorados. `git diff --check` aprovado, com avisos de normalização LF→CRLF.
- `GRAPHIFY`: atualização final de 2026-10-12 via `graphify update . --no-cluster`: 11.636 nós e 30.820 arestas; exportação HTML agregada contém 89 comunidades e 0 arestas entre comunidades. SHA-256 da cópia no vault confere com `MulletaFlix-android/graphify-out/graph.html`. Limitações conhecidas: CLI 0.9.79 vs recomendação 0.9.84, 84 arquivos não classificados, uma extração parcial já conhecida e 29 arquivos sem símbolos; sidecar estava desatualizado e as comunidades foram reconstruídas.
- `DISPOSITIVO`: a compilação instrumentada faz parte do gate. Execução Compose em PHONE/TABLET/TV e TalkBack/D-pad não será alegada sem ADB/AVD liberada; `MulletaflixApi35` permanece reservada.
- `ESCOPO`: somente APK/leitor e documentação; sem servidor, portal, versão, assinatura, release, publicação, commit ou push.

## Ciclo TDD — busca OCR em quadrinhos (2026-10-12)

- INTENT: a busca da tela incluía texto, EPUB e PDF, mas excluía páginas CBZ/CBR; permitir localizar texto contido em imagens de quadrinhos e abrir a página encontrada.
- RED observado: `:feature:item-detail:testDebugUnitTest --tests org.mulletaflix.feature.itemdetail.BookPdfTextSearchTest` falhou antes da implementação por `Unresolved reference: isBookSearchAvailable` no teste de política de busca.
- GREEN: política de disponibilidade agora aceita fontes paginadas com extração; `ComicBookOcr` reconhece sob demanda com ML Kit Latin offline, cache máximo de 3 páginas e decode de lado máximo 2048 px; resultados usam `BookPageSource` locator e abrem a página correspondente.
- Cobertura: teste focal de busca passou; `ComicBookOcrIntegrationTest` gera uma imagem com texto, empacota CBZ e valida OCR offline + resultado de página. Em 2026-10-12, o teste executou no TABLET/API 35 pelo wrapper: `:feature:item-detail:connectedDebugAndroidTest` filtrado para `ComicBookOcrIntegrationTest` — `BUILD SUCCESSFUL` (37 s). Confirma ML Kit real em CBZ no emulador, mas não substitui amostras do usuário nem valida CBR.
- Revisão encontrou risco de `close()` esperar OCR no thread da UI; lifecycle agora marca fechamento sem bloquear e fecha o recognizer após a última inferência ativa.
- Limite de cancelamento: cooperativo entre páginas. OCR corrente termina antes de reciclar o bitmap, evitando liberar memória nativa enquanto ML Kit ainda lê. O ciclo seguinte adicionou progresso acessível por página; TalkBack/visual permanece pendente em dispositivo.
- Gates anteriores: `testDebugUnitTest`, lint do item-detail e app, `assembleDebug`, compilação AndroidTest item-detail e app — `BUILD SUCCESSFUL` (2026-10-12). Execução adicional no TABLET/API 35 registrada acima; wrapper confirmou NVIDIA RTX 3050 e encerrou o AVD iniciado. AVD `MulletaflixApi35` reservada.

## Ciclo TDD — progresso da busca paginada — 2026-10-12

- INTENT: varredura longa de quadrinhos só informa “Buscando…”; informar páginas concluídas e progresso acessível sem recompor a UI para cada página.
- RED: teste de contrato temporário para callback paginado compilou e falhou na asserção enquanto a API não existia. Removido após adicionar testes comportamentais, para evitar depender do formato JVM da assinatura Kotlin.
- GREEN: `BookPdfTextSearch` informa páginas lidas com no máximo 100 checkpoints periódicos, além de início/fim quando não coincidem. Para 5.000 páginas, 101 notificações no total; teste de fronteira em 199 páginas também confirma 101. Progresso é emitido após decodificar/ler cada página, logo não anuncia 100% antes da última leitura.
- Testes JVM cobrem monotonicidade, limite, consulta vazia e cancelamento no callback que impede começar a página seguinte. Compose AndroidTest confere texto de progresso, `ProgressBarRangeInfo` e `LiveRegionMode.Polite`; compilado, não executado em dispositivo.
- Gates em 2026-10-12: suíte `testDebugUnitTest`, lint item-detail/app, `assembleDebug` e compilação AndroidTest do módulo/app — `BUILD SUCCESSFUL`. AVD `MulletaflixApi35` segue reservada e ADB ausente; execução visual continua pendente.
- Auditoria final do grafo Android: `graphify update . --no-cluster` — 11.664 nós/30.902 links; `graphify explain BookPdfTextSearch` e `BookTextSearchDialog` confirmaram ligações ao teste Compose. Export agregado: 89 comunidades, 0 ligações intercomunidade; CLI 0.9.79 (skill 0.9.84), 84 arquivos sem classificação, um teste com extração sintática parcial, 29 sem símbolos. HTML no vault conferido por SHA-256. Grafo raiz do repositório, atualizado na mesma data: 64.429 nós/166.238 links. TWINS: busca por `BookPdfTextSearch.search` e `Text("Buscando…")` encontrou 0 outros fluxos paginados sem progresso; “Buscando…” permanece apenas como estado inicial indeterminado do mesmo diálogo.

## Ciclo TDD — cancelamento explícito da busca de livros — 2026-10-12

- INTENT: o diálogo não oferece cancelamento explícito da busca em andamento; o teste exige uma ação acessível que cancele a tarefa; o guia de testes define Compose instrumentado para interação e acessibilidade, e o backlog registra cancelamento da busca OCR como validação pendente.
- RED: `BookTextSearchDialogTest.cancelButtonStopsSearchAndReturnsDialogToIdleState` executado no TABLET/API 35 falhou 1/1 porque não encontrou o texto `Cancelar busca` enquanto a consulta estava ativa.
- GREEN: diálogo agora expõe “Cancelar busca” somente durante a busca; incrementa a geração para invalidar resultados obsoletos, limpa progresso/estado de carregamento e cancela o Job. Pesquisa seguinte pode começar sem fechar o leitor.
- Limite: o cancelamento de coroutine não interrompe a inferência ML Kit já iniciada. `BookReaderScreen` mantém essa extração não cancelável até ML Kit liberar o bitmap; páginas seguintes não devem começar depois do cancelamento. O teste UI usa uma busca suspensa controlável e não afirma cancelamento interno do ML Kit.
- TESTES: o caso focal passou no TABLET/API 35; `BookTextSearchDialogTest` completa passou 6/6 no mesmo perfil, cobrindo busca, falha, troca/edição de query, progresso e cancelamento/reinício. QEMU foi confirmado na NVIDIA RTX 3050 pelo wrapper; AVD `MulletaflixTabletApi35` encerrou no fim.
- QUALITY GATE: `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin :app:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL` em 2m23s; 250 XML retidos somam 1.708 casos JVM, 0 falhas/erros/ignorados. Esta contagem inclui relatórios de builds anteriores. `git diff --check` sem erros.
- Sem dependências, bump, release, assinatura, portal, servidor, commit ou push.

## RED-GREEN — narração OCR de quadrinhos — 2026-10-12

- `INTENT`: BookReaderScreen habilita narração paginada apenas para PDF; o novo teste CBZ exige falar o texto OCR da imagem e avançar à próxima página; o backlog registra narração OCR para PDF, não para quadrinhos.
- `RED`: `BookReaderScreenIntegrationTest.comicSpeechRecognizesCurrentPageOfflineAndAdvancesAfterUtterance` falhou pelo motivo esperado: controle acessível `Ler quadrinhos em voz alta` ausente na UI. Não foi falha de compilação nem de fixture.
- `GREEN`: `BookReaderScreen` agora disponibiliza narração paginada quando há extração nativa PDF ou OCR CBZ/CBR; processa OCR sequencialmente fora da UI, mantém a página/bitmap protegido até ML Kit concluir, fala os trechos reconhecidos e só então passa à próxima página. O controle acessível reutiliza velocidade e seletor de idioma. Voltar interrompe pelo callback comum sem chamada duplicada ao motor.
- `DISPOSITIVO`: `tools/with-emulator.ps1` em `MulletaflixTabletApi35`, TABLET/API 35. `comicSpeechRecognizesCurrentPageOfflineAndAdvancesAfterUtterance` — `BUILD SUCCESSFUL`, 1 teste, 0 falhas/erros/ignorados; `pdfSpeechReadsVisiblePageAdvancesAndStopsOnManualNavigation` — `BUILD SUCCESSFUL`, 1 teste, 0 falhas/erros/ignorados. NVIDIA RTX 3050 confirmada pelo wrapper; AVD encerrado ao terminar cada execução.
- `QUALITY GATE`: `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin :app:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL`; XMLs JVM somam 1.708 casos, 0 falhas/erros/ignorados; `git diff --check` sem erros (avisos de LF/CRLF).
- `GRAPHIFY`: `update . --no-cluster` e `cluster-only . --no-label` executados após código/testes/documentação; export Android sincronizado no vault e SHA-256 comparado. `explain` confirmou tela, OCR, teste e TDD. O grafo raiz foi extraído, mas cluster-only recusou rebuild por diferença de um nó; sem `--force`, preservando o HTML raiz. Contagens, hashes e avisos constam na nota de arquitetura do vault.
- `TWINS`: busca de `pdfSpeechSupported`, `Ler PDF em voz alta`, `stopPdfSpeech` e `ComicBookPageControls` encontrou configuração PDF intencional, label padrão compatível com testes/chamadas existentes e chamada de produção única parametrizada pela fonte; não resta símbolo `stopPdfSpeech`.
- `PENDÊNCIAS`: instrumentar fala de CBR, variações reais autorizadas de CBZ/CBR, escrita não Latin, áudio físico, TalkBack/D-pad, PHONE/TV, uso prolongado e latência/bateria. Inferência ML Kit já iniciada termina antes de liberar o bitmap; cancelar pode aguardar a página atual.
- `ESCOPO`: somente APK e documentação; sem alteração do servidor/portal, versão, keystore, artefato ou release.

## VERIFICAÇÃO — narração OCR de CBR com texto conhecido — 2026-10-12

- `INTENÇÃO`: o fluxo de produção já trata `CbrBookArchive` junto ao leitor paginado e ao `ComicBookOcr`; a lacuna era evidência positiva, pois a fixture CBR anterior não tinha texto reconhecível. O teste exige duas frases OCR conhecidas, encaminhamento ao TTS e avanço somente após o callback, conforme o contrato de narração paginada acima.
- `FIXTURE`: `feature/item-detail/src/androidTest/assets/books/cbr-ocr-pages.cbr`, arquivo RAR4 sintético gerado localmente com duas páginas PNG de texto; WinRAR validou integridade. Asset usado somente em AndroidTest, sem dependência de WinRAR na compilação ou execução do app.
- `DEVICE`: `BookReaderScreenIntegrationTest.cbrSpeechRecognizesCurrentPageOfflineAndAdvancesAfterUtterance` passou 1/1 no AVD `MulletaflixTabletApi35`, TABLET/API 35; XML reporta 0 falhas, erros ou ignorados. Afirmações verificadas: “MULLETA PAGE ONE” e “MULLETA PAGE TWO” chegaram ao motor TTS falso; a página 1 continuou visível antes do callback, depois avançou e narrou a página 2 após a notificação de utterance concluída.
- `QUALITY GATE`: `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin :app:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL`; 250 relatórios XML somam 1.708 testes JVM, 0 falhas, 0 erros, 0 ignorados. A execução AndroidTest é contabilizada separadamente (1/1), não dentro dos XML JVM.
- `TDD`: teste é homologação/cobertura acrescentada para comportamento já implementado; nenhum `RED` novo foi alegado. A tentativa prévia com a fixture CC0 sem texto falhou antes do TTS por ausência de OCR, não por bug de produção; essa evidência inconclusiva não é uma regressão reproduzível e não foi mascarada.
- `LIMITAÇÕES`: logcat confirma seleção da versão local do modelo ML Kit Latin, mas a conectividade do AVD não foi desativada; o teste não comprova operação integral em modo avião. OCR/encadeamento usam páginas sintéticas no tablet e TTS falso; não comprovam fala audível, formatos RAR diversos/arquivos licenciados reais, idiomas não Latin, busca textual CBR, PHONE/TV, TalkBack, latência, bateria ou memória prolongada. Sem alteração do código de produção, dependências, versão, release, servidor ou portal.

## VERIFICAÇÃO — busca OCR CBR com texto conhecido — 2026-10-12

- `ESCOPO`: homologação instrumentada da busca já conectada no leitor; nenhuma alteração de comportamento em produção e nenhum RED alegado.
- `TESTE`: `ComicBookOcrIntegrationTest.cbrPageIsRecognizedAndReturnedAsSearchHit` carrega a fixture RAR4 sintética, abre `CbrBookArchive`, reconhece as páginas com `ComicBookOcr` e pesquisa “page two”. Confirma leitura das páginas 0 e 1, um resultado na página 2 (índice 1) e trecho contendo “PAGE TWO”.
- `EXECUÇÃO`: wrapper `tools/with-emulator.ps1` no AVD TABLET/API 35, GPU NVIDIA confirmada; teste focal executado 1/1, sem falha/erro/skip. O wrapper encerrou o AVD.
- `LIMITE`: o caso exercita leitor de arquivo, OCR e indexador de página, mas não clica no resultado pela UI Compose nem valida navegação do leitor CBR ponta a ponta. Fixture sintética Latin; rede do emulador ativa, portanto “offline” descreve o modelo empacotado e não uma execução em modo avião. Arquivos reais, outras escritas, PHONE/TV, TalkBack, memória/latência/bateria seguem pendentes.
- `ESCOPO EXTERNO`: sem mudança de código de produção, dependência, versão, release, assinatura, servidor, portal, commit ou push.

## VERIFICAÇÃO — navegação da busca OCR CBR pela UI — 2026-10-12

- `ESCOPO`: cobrir o fluxo Compose já implementado de abertura do resultado CBR; sem mudança no comportamento de produção e sem alegar RED funcional.
- `CENÁRIO`: `BookReaderScreenIntegrationTest.cbrSearchResultOpensMatchingPage` serve a fixture RAR4 via MockWebServer, espera a página 1, pesquisa “page two”, confirma o resultado “Página 2” com “PAGE TWO”, toca nele e observa “Página 2 de 2”. Assim valida tela, OCR, busca, locator e navegação de resultado juntos.
- `PRIMEIRA EXECUÇÃO`: falhou no próprio teste porque `assertTextContains` comparava o texto inteiro do botão, cujo texto de semântica aparece entre colchetes. A substring esperada existia; a asserção foi corrigida com `substring = true`. Isso não foi falha funcional do aplicativo nem RED de produção.
- `GREEN`: execução focal no TABLET/API 35 pelo wrapper `tools/with-emulator.ps1`; 1/1 passou após a correção da asserção. Relatório confirma o método nomeado sem falha/erro/skip.
- `REPLICAÇÃO DE PERFIL`: em 2026-10-12, o mesmo método passou em PHONE/API 35 (`SmartMeasureApi35Play`), XML `tests=1 failures=0 errors=0 skipped=0`. O wrapper confirmou uso da GPU NVIDIA e encerrou o AVD; `adb devices -l` ficou vazio.
- `QUALITY GATE`: `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin :app:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL` após o teste.
- `LIMITES`: CBR RAR4 sintético com texto Latin, servidor HTTP local de teste e TABLET; não cobre livros reais/autorizados diversos, PHONE, TalkBack, aparelho físico, modo avião, bateria ou memória prolongada. TV continua sem biblioteca/leitor de livros por regra de produto.
- `ESCOPO EXTERNO`: sem código de produção, dependência, release, servidor, portal, commit ou push.

## RED-GREEN — cancelamento da espera por OCR CBR — 2026-10-12

- `INTENÇÃO`: ao cancelar a busca, a coroutine/UI não deve ficar aguardando uma Task ML Kit que não oferece cancelamento cooperativo; entretanto, bitmap de entrada e recognizer não podem ser liberados enquanto a inferência nativa ainda os usa.
- `RED`: `ComicBookOcrTest.cancelingRecognitionReturnsPromptlyAndKeepsBitmapAliveUntilTaskCompletes` foi executado antes da correção e falhou no limite temporal “canceling search must not wait for ML Kit”, reproduzindo o bloqueio.
- `GREEN`: a implementação aguarda a Task via `suspendCancellableCoroutine`; cancelamento retorna imediatamente. A limpeza dos recursos foi transferida ao callback de conclusão, serializada e idempotente; `close()` adia o fechamento do recognizer enquanto houver inferência ativa. O mesmo teste unitário passou 1/1 e verifica que o bitmap continua vivo, recognizer aberto durante a Task e ambos liberados após sua conclusão.
- `INTEGRAÇÃO`: `BookReaderScreenIntegrationTest.cbrSearchResultOpensMatchingPage` repetido em PHONE/API 35 no `SmartMeasureApi35Play`: 1 teste, 0 falhas/erros/ignorados; UI encontrou o trecho e abriu Página 2 de 2.
- `QUALITY GATE`: `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin :app:compileDebugAndroidTestKotlin --no-daemon --console=plain` terminou `BUILD SUCCESSFUL` (881 ações; 38 executadas). `git diff --check` sem erro. Wrapper confirmou GPU NVIDIA e desligou o emulador; nenhum device permaneceu conectado.
- `LIMITE`: cancelar a busca/coroutine não cancela a inferência nativa ML Kit já iniciada; ela pode continuar até completar, retendo bitmap/recognizer durante esse intervalo. Fixture sintética, não é evidência de cancelamento do próprio ML Kit, aparelho físico, TalkBack, carga longa ou CBR real autorizado. Sem nova dependência, versão/release, assinatura, portal ou servidor.

## RED-GREEN — cancelamento da espera por OCR PDF — 2026-10-12

- `INTENT`: PdfBookDocument currently blocks in Tasks.await and BookReaderScreen makes lookup NonCancellable; the failing test expects cancel to return while Task retains bitmap; TODO says cancellation propagated and existing CBR contract requires safe deferred cleanup.
- `RED`: `PdfBookDocumentOcrTest.cancelingRecognitionReturnsPromptlyAndKeepsBitmapAliveUntilTaskCompletes` falhou antes da correção na asserção de retorno imediato após 500 ms, reproduzindo a espera bloqueante da busca cancelada. A etapa de conclusão da Task confirmou que a limpeza ocorria somente depois da inferência.
- `GREEN`: a espera da Task ML Kit agora usa `suspendCancellableCoroutine`; cancelamento encerra a coroutine sem aguardar inferência nativa, e o callback mantém Bitmap, permit e recognizer até a conclusão, liberando cada recurso de forma idempotente. Os caminhos de busca e narração do leitor deixam de envolver esse trabalho em `NonCancellable`.
- `UNIT`: `PdfBookDocumentOcrTest` e regressão `ComicBookOcrTest` — 1/1 cada, 0 falhas/erros/ignorados. Ambas verificam retorno rápido, recursos vivos enquanto a Task está pendente e limpeza após a conclusão.
- `DEVICE`: `PdfBookReaderIntegrationTest.scannedPdfPageIsRecognizedOfflineAndIndexedBySearch` passou 1/1 em PHONE/API 35 (`SmartMeasureApi35Play`), OCR da página digitalizada e localização pela busca. XML: `tests=1 failures=0 errors=0 skipped=0`. O wrapper confirmou GPU NVIDIA, encerrou o AVD e `adb devices -l` ficou vazio.
- `QUALITY GATE`: `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin :app:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL` (2m36s; 881 ações, 35 executadas e 846 atualizadas). `git diff --check` sem erros; apenas avisos existentes de normalização LF/CRLF.
- `TWINS`: busca em `MulletaFlix-android` por `Tasks.await\(` encontrou 0 ocorrências. `NonCancellable + Dispatchers.IO` encontrou 1 uso em `BookReaderViewModel.kt:296`, somente para fechar um livro órfão no `finally`; não é espera de OCR e foi mantido para limpeza de lifecycle.
- `LIMITE`: cancelar encerra a espera/coroutine da busca, não o processamento nativo ML Kit já iniciado. A inferência pode continuar até a Task concluir, mantendo Bitmap/recognizer vivos nesse período. Fixture sintética Latin e PHONE virtual; PDF real variado, TalkBack, dispositivo físico, carga/memória e outros alfabetos continuam pendentes. Sem dependência, versão, release, assinatura, portal ou servidor alterados.

## Cobertura instrumentada de erro e retry RTF — 2026-10-12

- `INTENT`: `PlainTextBookDocument` rejeita payload RTF truncado em `\\binN`; garantir no fluxo Compose + ViewModel + HTTP mockado que erro seja acionável, texto parcial não apareça e retry válido recupere leitura.
- `RED/GREEN`: nenhuma alteração de lógica de produção foi necessária. O primeiro comando apontou tarefa AndroidTest do módulo incorreta e falhou na descoberta da classe; repetido com `:feature:item-detail:connectedDebugAndroidTest`, o teste passou. Não contar falha de descoberta como RED de comportamento.
- `DEVICE`: `BookReaderScreenIntegrationTest.truncatedRtfShowsErrorWithoutPartialTextAndRetryLoadsValidRtf` passou 1/1 em PHONE/API 35 no AVD `SmartMeasureApi35Play`. Confirmou a mensagem localizada, ausência de texto parcial, texto íntegro após retry e duas requisições à rota `/BookReader/Items/{id}/BookReader/Epub`.
- `LIMITE`: MockWebServer e RTF sintético; não homologa servidor real, TalkBack, controle D-pad/TV, arquivos RTF diversos nem aparelho físico. Nenhum código de produção, dependência, versão, APK de produção, release, assinatura, portal ou servidor foi alterado.

## VERIFICAÇÃO — cancelamento da extração de narração ao trocar de livro — 2026-10-12

- `INTENT`: ao trocar o `itemId` sem desmontar a tela, o job de narração do livro anterior deve ser cancelado e não pode iniciar fala obsoleta; a nova tela deve carregar o livro B.
- `RED`: o AndroidTest falhou antes da correção pela asserção `Changing books must cancel pending page extraction`; não foi falha de compilação/setup. O teste foi reforçado para distinguir B por 3 páginas e verificar as rotas HTTP de A e B.
- `GREEN`: `DisposableEffect(itemId)` captura o estado do job pertencente àquele item e cancela somente esse job ao descartar a chave; também interrompe a fala paginada sem encerrar o controlador compartilhado da tela. O teste espera a tela B, exige o `finally` da extração A cancelada, confere as duas requisições e rejeita fala obsoleta.
- `DISPOSITIVO`: `BookReaderScreenIntegrationTest.switchingBookCancelsPendingPageNarrationExtraction` passou 1/1 em PHONE/API 35 no AVD temporário `MulletaflixCodexPhoneApi35`; zero falhas, erros ou ignorados. O wrapper confirmou QEMU na NVIDIA RTX 3050 e encerrou o AVD; AVDs preexistentes foram preservados.
- `QUALITY GATE`: `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin :app:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL`; 252 XML somam 1.710 testes JVM, 0 falhas/erros/ignorados. `git diff --check` passou, com avisos Git de normalização LF/CRLF.
- `REVISÃO`: Cavecrew reviewer apontou risco de o teste aceitar conteúdo antigo como B; o teste foi endurecido com contagem de páginas diferente e verificação das duas rotas. Revisão local do diff e do cleanup por `itemId` concluída.
- `LIMITE`: instrumentação cobre troca enquanto a extração está suspensa, não troca durante áudio já iniciado, TTS audível, TV, TalkBack/D-pad ou aparelho físico. Sem bump, release, assinatura, servidor, portal, commit ou push.

## Cobertura — troca de livro durante TTS ativo — 2026-10-12

- `ESCOPO`: lacuna de cobertura; a auditoria do código indicava cleanup do engine ao trocar `itemId`, sem teste Compose para fala já iniciada. Nenhum comportamento de produção foi alterado nesta etapa e não se declara RED funcional.
- `TESTE`: `BookReaderScreenIntegrationTest.switchingBookStopsActivePageNarrationAndIgnoresLateSpeechCallback` inicia o livro A com texto maior que um trecho de TTS, troca para B, exige parada do engine A e invoca o callback antigo; a asserção impede o callback de iniciar o próximo trecho. Espera também o livro B distinto e verifica suas rotas.
- `REVISÃO`: o Cavecrew reviewer observou que a primeira fixture cabia num só trecho e poderia mascarar aceitação do callback. Texto repetido foi ampliado para gerar múltiplos trechos; esse é o resultado revalidado.
- `DISPOSITIVO`: AndroidTest PHONE/API 35 passou 1/1 em 2026-10-12 (XML: `TEST-MulletaflixCodexPhoneApi35(AVD) - 15.xml`, `failures=0 errors=0 skipped=0`). AVD temporário iniciado/encerrado pelo wrapper com GPU NVIDIA; nenhuma AVD reservada foi alterada.
- `QUALITY GATE`: `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin :app:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL` em 1m02s; `git diff --check` sem erros (avisos de normalização LF/CRLF).
- `LIMITE`: engine TTS fake, sem saída acústica; só PHONE/API 35. ON_STOP, TV, TalkBack/D-pad, aparelho físico, troca em EPUB/Readium e homologação integral continuam pendentes. Sem release, bump, servidor, portal, commit ou push.

## Revalidação da leitura não-EPUB — 2026-10-12

- Escopo: conferir a implementação OCR já existente para páginas CBZ/CBR e PDFs digitalizados; nenhuma mudança de código ou dependência foi necessária nesta rodada.
- JVM focal, execução forçada: `:feature:item-detail:testDebugUnitTest --tests ComicBookOcrTest --tests PdfBookDocumentOcrTest --tests BookPdfTextSearchTest --tests PlainTextBookDocumentTest --rerun-tasks` — 47 testes, 0 falhas/erros/ignorados.
- Dispositivo: PHONE/API 35 temporário, via `tools/with-emulator.ps1`, GPU NVIDIA RTX 3050 confirmada. `ComicBookOcrIntegrationTest` passou 2/2 (fixtures CBZ e CBR, OCR/busca offline); `PdfBookReaderIntegrationTest.scannedPdfPageIsRecognizedOfflineAndIndexedBySearch` passou 1/1. O wrapper encerrou o AVD e `adb devices -l` ficou vazio.
- Quality Gate: `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin :app:compileDebugAndroidTestKotlin` passou; a suíte foi reexecutada em seguida com `testDebugUnitTest --rerun-tasks`: 1.710 testes em 252 relatórios XML, 0 falhas/erros/ignorados. Warnings de API experimental/depreciação permanecem, sem falha.
- Limites: fixtures sintéticas; OCR Latin, sem modo avião comprovado, mídia real autorizada, dispositivos físicos, TV, TalkBack, latência/bateria ou memória limitada. Cancelar encerra a espera da UI, não a inferência nativa já iniciada; bitmap e recognizer são liberados após a Task.
- Escopo APK local; sem alteração de versão, assinatura, release, publicação, servidor, portal, commit ou push. Sem executar o AVD `MulletaflixApi35` reservado.

## Integração — limpeza horária Coil e download Media3 — 2026-10-12

- `INTENÇÃO`: provar que o worker periódico de limpeza de imagens não altera downloads Media3 explicitamente concluídos e que a reprodução continua disponível offline.
- `COBERTURA`: o AndroidTest usa o repositório/download manager de produção e fixture HTTP local; depois de confirmar `Download.STATE_COMPLETED`, agenda `ImageCacheCleanup` com WorkManager de teste, insere uma chave Coil temporária, aciona o atraso periódico com `TestDriver` e confirma a remoção da imagem. O download permanece `COMPLETED`; após desligar o MockWebServer, o PlayerViewModel chega a `STATE_READY` e o servidor não recebe requisição adicional.
- `RED/GREEN`: nenhuma falha de comportamento de produção foi reproduzida; a lacuna era falta de integração ponta a ponta. O primeiro compile apontou somente import estático ausente no teste, corrigido antes da primeira instrumentação; não é contado como RED. Revisão independente encontrou risco baixo de o singleton WorkManager não ser restaurado após falha parcial; o `finally` agora restaura o delegate original em qualquer cenário. A execução final passou.
- `DISPOSITIVO`: `OfflinePlayerProductionDownloadFlowTest.productionDownloadRepositoryFeedsOfflinePlayerFromCacheWithoutNetwork` passou 1/1 em TABLET/API 35 em `MulletaflixTabletApi35`, 0 falhas/erros/ignorados. XML: `app/build/outputs/androidTest-results/connected/debug/TEST-MulletaflixTabletApi35(AVD) - 15.xml`. Wrapper confirmou QEMU na GPU NVIDIA e encerrou a AVD.
- `QUALITY GATE`: `testDebugUnitTest :app:lintDebug :feature:player:lintDebug :app:assembleDebug :app:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL` (874 ações; 6 executadas). `git diff --check` executado após as atualizações documentais.
- `LIMITE`: `TestDriver` simula o vencimento e a execução periódica, não uma hora real nem a janela de agendamento do SO. Teste usa MockWebServer e mídia sintética; rede/aparelho físico, duração longa e imagens reais ainda não cobertos. Nenhum código de produção, dependência, versão, APK de produção, servidor ou portal alterado.

## RED-GREEN — retorno à Home após login por deep link cross-server — 2026-10-12

- `INTENT`: depois de autenticar no servidor B para abrir um link recebido enquanto a sessão era do servidor A, Voltar do detalhe deve retornar à Home já autenticada em B, não finalizar a Activity.
- `RED`: os dois novos casos em `DeepLinkNavigationPolicyTest` foram escritos antes da mudança. Com o helper inicial contendo somente Home, 1 dos 22 testes falhou pela razão esperada: faltava a rota de detalhe na pilha. Uma primeira execução AndroidTest compilou com chamada de API inexistente e não conta como RED; corrigida para usar `OnBackPressedDispatcher` da Activity, o runner permaneceu ativo sem relatório por mais de três minutos e foi interrompido pelo wrapper. A tentativa de dispositivo é inconclusiva.
- `GREEN`: `routesAfterSuccessfulLogin` agora produz Home + detalhe para link pendente e apenas Home no login comum. `MulletaFlixNavHost` navega sequencialmente, removendo a seleção de servidor apenas ao inserir a Home.
- `UNIT`: `DeepLinkNavigationPolicyTest` passou 22/22 após a implementação.
- `ANDROID`: após as tentativas anteriores sem relatório, o teste focal `crossServerDeepLinkAuthenticatesOnTargetServerAndLoadsItemWithoutRequestingOldServer` passou 1/1 em PHONE/API 35, XML `TEST-MulletaflixCodexPhoneApi35_20261012(AVD) - 15.xml`, serial `emulator-5576`, 0 falhas/erros/ignorados. Confirmou credenciais fictícias enviadas a B, item e sessão persistida em B, retorno à Home e fila de requisições de A vazia.
- `QUALITY GATE`: lint do módulo item-detail e app, `assembleDebug` e compilação AndroidTest de ambos concluíram `BUILD SUCCESSFUL`. `testDebugUnitTest --rerun-tasks` executou 1.712 testes em 252 XMLs, 0 falhas/erros/ignorados. Warnings de compilador existentes não falharam o build.
- `RETESTE`: em 2026-10-13, a classe completa foi executada no AVD temporário `MulletaflixCodexPhoneApi35_20261012`, com NVIDIA verificada. Resultado 8/9; o cross-server falhou após 34,2 s: só três GETs foram observados em B, sem POST de login; URL/sessão e texto da UI retornaram ao servidor padrão e mostraram erro de credenciais. A repetição focal imediata passou 1/1 no mesmo AVD. Investigação estática confirmou os sinais, mas não a origem; uma corrida com servidores salvos é hipótese, não fato.
- `LIMITE`: a falha da classe completa contradiz estabilidade demonstrada. Investigar se seleção do servidor e campos do formulário permanecem em B ao enviar; reforçar observabilidade sem relaxar asserts e repetir a classe. Não atualizar homologação formal. Sem alteração de servidor/portal, versão, assinatura, APK de produção ou publicação.

## RED-GREEN — isolamento de URLs MockWebServer no teste cross-server — 2026-10-13

- `INTENÇÃO`: nenhuma URL de MockWebServer criada pela classe deve permanecer na lista persistida de servidores salvos ao final de cada teste.
- `RED`: a nova asserção de teardown falhou pelo motivo esperado no teste focal, reportando `[http://localhost:36447]` como URL de MockWebServer persistida sem registro para limpeza. A limpeza de segurança removeu o endpoint e o wrapper encerrou o AVD.
- `GREEN`: `trackMockServerUrl` registra cada servidor tanto na auditoria da classe quanto em `temporarySavedServerUrls`; o teardown remove endpoints não registrados como fallback e então verifica que a interseção ficou vazia. Focal cross-server passou 1/1; a classe completa `MainActivitySessionNavigationTest` passou 9/9, zero falhas/erros/ignorados.
- `AMBIENTE`: PHONE/API 35, AVD temporário `MulletaflixAptoidePhone`, GPU NVIDIA RTX 3050 confirmada pelo wrapper; após a execução completa, ADB sem dispositivos conectados.
- `LIMITES`: o defeito de fixture é real, mas não foi provado como causa da falha histórica 8/9; repetir execuções e validar LAN física continuam pendentes. Não muda comportamento/runtime do APK nem status da homologação formal. Sem release, portal ou servidor.

## RED-GREEN — isolamento de URLs MockWebServer no teste cross-server — 2026-10-13

- `INTENÇÃO`: nenhuma URL de MockWebServer criada pela classe deve permanecer na lista persistida de servidores salvos ao final de cada teste.
- `RED`: a nova asserção de teardown falhou pelo motivo esperado no teste focal, reportando `[http://localhost:36447]` como URL de MockWebServer persistida sem registro para limpeza. A limpeza de segurança removeu o endpoint e o wrapper encerrou o AVD.
- `GREEN`: `trackMockServerUrl` registra cada servidor tanto na auditoria da classe quanto em `temporarySavedServerUrls`; o teardown remove endpoints não registrados como fallback e então verifica que a interseção ficou vazia. Focal cross-server passou 1/1; a classe completa `MainActivitySessionNavigationTest` passou 9/9, zero falhas/erros/ignorados.
- `AMBIENTE`: PHONE/API 35, AVD temporário `MulletaflixAptoidePhone`, GPU NVIDIA RTX 3050 confirmada pelo wrapper; após a execução completa, ADB sem dispositivos conectados.
- `LIMITES`: o defeito de fixture é real, mas não foi provado como causa da falha histórica 8/9; repetir execuções e validar LAN física continuam pendentes. Não muda comportamento/runtime do APK nem status da homologação formal. Sem release, portal ou servidor.

## Revalidação instrumentada — biblioteca de livros oculta na Android TV — 2026-10-13

- `INTENÇÃO`: confirmar no runtime Android TV que `LibraryTiles`, com a política de perfil de produção e feed sintético, não compõe o card Livros e mantém Filmes disponível.
- `TESTE`: `HomeLibraryNavigationTest#books_libraryIsNotRenderedOnAndroidTv` já existia; passou 1/1 no AVD `MulletaflixTvApi34` (API 34), zero falhas, erros ou ignorados. XML novo: `feature/home/build/outputs/androidTest-results/connected/debug/TEST-MulletaflixTvApi34(AVD) - 14.xml`.
- `EXECUÇÃO`: `tools/with-emulator.ps1` verificou o perfil TV e confirmou o processo QEMU do AVD usando NVIDIA GeForce RTX 3050; desligou o emulador ao concluir. `adb devices -l` terminou sem dispositivos.
- `RED/GREEN`: nenhuma alteração de comportamento nem código nesta revalidação; não atribuir RED-GREEN retrospectivo ao teste já existente.
- `LIMITE`: o teste compõe o componente `LibraryTiles`, não executa a Activity/HomeScreen completa nem consulta o servidor real; aparelho físico também não foi validado. A matriz de comportamento em celular/tablet segue baseada nos testes existentes; esta execução não a revalidou.
- `ESCOPO`: APK local, teste/documentação; sem mudança de versão, release, assinatura, servidor, portal, commit ou push.

## Repetição — navegação cross-server na Activity principal — 2026-10-13

- `INTENÇÃO`: reavaliar a intermitência anterior 8/9 na classe `MainActivitySessionNavigationTest`, sem alterar asserts nem o comportamento do APK.
- `EXECUÇÃO`: duas repetições integrais consecutivas em `MulletaflixAptoidePhone` (PHONE/API 35), com `tools/with-emulator.ps1`; ambas terminaram `BUILD SUCCESSFUL` e cada XML observado mostrou 9 testes, 0 falhas, 0 erros e 0 ignorados. O caso cross-server passou nas duas.
- `EVIDÊNCIA RETIDA`: o relatório AndroidTest do mesmo AVD sobrescreve o XML de execução anterior. XML atualmente disponível: `app/build/outputs/androidTest-results/connected/debug/TEST-MulletaflixAptoidePhone(AVD) - 15.xml`, timestamp 2026-10-13T04:16:40; a primeira execução observada ocorreu às 04:11:55 e também mostrou 9/9 antes de ser substituída.
- `AMBIENTE`: wrapper confirmou o processo QEMU usando NVIDIA GeForce RTX 3050, verificou PHONE/API 35 e encerrou o AVD; `adb devices -l` ficou sem dispositivos.
- `CONCLUSÃO`: resultado positivo repetido, causa da execução histórica 8/9 continua desconhecida; não declarar correção da intermitência nem homologação formal.
- `ESCOPO`: somente diagnóstico/teste/documentação do APK. Sem alteração de código de produção, servidor, portal, versão, release, assinatura, commit ou push.

## Revalidação integrada — livros ocultos e refresh da Home Android TV — 2026-10-13

- `INTENÇÃO`: verificar no perfil TV que a `HomeScreen` integrada não expõe livros no feed/biblioteca e que o conteúdo se atualiza quando a tela volta ao primeiro plano.
- `EVIDÊNCIA`: os testes já existentes `TvHomeRefreshIntegrationTest#booksLibraryFromLoadedFeedIsHiddenByRealTvHomeScreen` e `returning_to_tv_home_replaces_visible_media_with_refreshed_feed` passaram 2/2 no AVD `MulletaflixTvApi34` (API 34), zero falhas/erros/ignorados. XML: `feature/home/build/outputs/androidTest-results/connected/debug/TEST-MulletaflixTvApi34(AVD) - 14.xml`, timestamp 2026-10-13T04:25:11Z.
- `AMBIENTE`: `tools/with-emulator.ps1` verificou perfil TV e QEMU na NVIDIA GeForce RTX 3050; encerrou o AVD ao finalizar. `adb devices -l` ficou sem dispositivos.
- `QUALITY GATE`: `gradlew.bat testDebugUnitTest --no-daemon --console=plain` terminou `BUILD SUCCESSFUL`; XMLs de 252 relatórios agregados: 1.719 testes, 0 falhas/erros/ignorados.
- `RED/GREEN`: nenhuma implementação foi modificada; esta é revalidação de cenários existentes e não comprova RED-GREEN histórico.
- `LIMITES`: feed controlado/sintético e emulador API 34; TV física, ligação ao servidor real e homologação formal continuam pendentes.
- `ESCOPO`: validação e documentação do APK. Sem alteração de código de produção, versão, assinatura, release, servidor, portal, commit ou push.

## RED-GREEN — seleção de voz na narração paginada de livros — 2026-10-13

- `INTENÇÃO`: permitir selecionar/persistir voz do mecanismo Android por idioma, isolada por servidor/conta, com fallback seguro quando uma voz deixar de estar disponível.
- `RED`: `applies saved voice after engine initialization and before first utterance` falhou pela asserção de voz ainda não aplicada antes da primeira fala. A falha de compilação ocorrida antes, por ausência da nova API durante o scaffolding do teste, não foi tratada como evidência RED.
- `GREEN`: controller aplica voz salva após inicializar; ViewModel salva por identidade ativa e descarta gravações obsoletas; diálogo expõe vozes e status online/offline; engine reverte preferência inválida ao padrão. Testes unitários do controller/ViewModel passaram.
- `INSTRUMENTAÇÃO`: seleção e persistência Compose passaram 1/1 em PHONE/API 35 e 1/1 em TABLET/API 35. XML tablet: `feature/item-detail/build/outputs/androidTest-results/connected/debug/TEST-MulletaflixTtsVoiceTabletApi35(AVD) - 15.xml` (1 teste, 0 falhas/erros/ignorados). Ambos AVDs foram fechados; `adb devices -l` sem dispositivos.
- `QUALITY GATE`: em 2026-10-13, 1.731 testes JVM em 252 XMLs, zero falhas/erros/ignorados; `:feature:item-detail:lintDebug`, `:app:lintDebug`, `:app:assembleDebug`, `:feature:item-detail:compileDebugAndroidTestKotlin` e `git diff --check` aprovados.
- `LIMITES`: não comprova áudio realmente audível nem o catálogo/comportamento de engines de terceiros; dispositivo físico e TalkBack permanecem pendentes. O leitor EPUB/Readium usa seu fluxo de narração separado e não foi alterado. Sem versão/release/portal/servidor.

## Revalidação — sumário DOCX em tablet — 2026-10-13

- `INTENÇÃO`: preencher a matriz de UX adaptativa para o fluxo de leitura DOCX em tablet; TV não é alvo porque a biblioteca Livros deve ficar oculta nesse perfil.
- `EVIDÊNCIA`: `DocxBookReaderIntegrationTest` passou 2/2 em `MulletaflixTabletApi35` (TABLET/API 35): `extractedDocxTextIsRenderedForReading` e `docxTableOfContentsJumpsToWordHeadingAndRestoresLocator`; XML `feature/item-detail/build/outputs/androidTest-results/connected/debug/TEST-MulletaflixTabletApi35(AVD) - 15.xml`, timestamp 2026-10-13T09:28:26.
- `AMBIENTE`: execução por `tools/with-emulator.ps1`; wrapper verificou QEMU na NVIDIA RTX 3050 e encerrou o AVD. `adb devices -l` ficou sem dispositivos.
- `RED/GREEN`: nenhuma implementação alterada; validação do teste existente, não atribuir RED-GREEN retrospectivo.
- `LIMITES`: fixtures DOCX sintéticas; não cobre arquivos reais autorizados, aparelho físico, TalkBack ou robustez prolongada. Não é homologação formal.
- `ESCOPO`: validação/documentação do APK; sem alteração de versão, assinatura, release, publicação, servidor ou portal.

## Integração Compose — sumário MOBI e locator — 2026-10-13

- `INTENÇÃO`: fechar a lacuna entre o teste JVM que extrai capítulos MOBI e a tela real do leitor; seleção do segundo capítulo deve exibir o trecho certo, persistir seu locator e permitir que outro ViewModel carregue a mesma posição.
- `COBERTURA`: `BookReaderScreenIntegrationTest.mobiContentsNavigateToChapterAndRestoreTheSavedLocator` serve fixture MOBI por MockWebServer, abre o sumário acessível da tela Compose, seleciona o segundo capítulo após o primeiro chunk, verifica o texto no chunk de destino, lê o locator persistido no DataStore e chama `load` num novo ViewModel para confirmar a restauração.
- `RED/GREEN`: tarefa de cobertura de integração, sem alteração de comportamento de produção; não há RED comportamental atribuível ao produto. A primeira execução do teste falhou porque o harness chamou `ComposeTestRule.setContent` duas vezes na mesma Activity. O XML apontou `IllegalStateException` do harness; a verificação foi ajustada para carregar um novo ViewModel sem substituir o conteúdo Compose. A execução final passou.
- `DISPOSITIVO`: execução final 1/1 em `MulletaflixTabletApi35` (TABLET/API 35), XML `feature/item-detail/build/outputs/androidTest-results/connected/debug/TEST-MulletaflixTabletApi35(AVD) - 15.xml`; wrapper confirmou QEMU na NVIDIA RTX 3050 e encerrou a AVD. O AVD reservado `MulletaflixApi35` não foi utilizado.
- `QUALITY GATE`: `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin :app:compileDebugAndroidTestKotlin --no-daemon --console=plain` concluiu `BUILD SUCCESSFUL`; 1.732 testes em 252 relatórios, zero falhas/erros/ignorados; 881 ações, 7 executadas e 874 up-to-date.
- `LIMITES`: fixture sintética, não arquivo real servido pelo servidor; PHONE, aparelho físico, TalkBack e diversidade de MOBI/KF8 permanecem pendentes. Não atualizar homologação formal por contagem de testes. Nenhum código de produção, dependência, versão, APK de produção, servidor ou portal alterado.

### Revalidação PHONE — sumário MOBI e locator — 2026-10-13

- `INTENÇÃO`: estender a matriz do teste Compose já aprovado em TABLET para PHONE, sem alterar o comportamento do APK.
- `EVIDÊNCIA`: `BookReaderScreenIntegrationTest.mobiContentsNavigateToChapterAndRestoreTheSavedLocator` passou 1/1 em `MulletaflixCodexPhoneApi35_20261012` (PHONE/API 35), XML `feature/item-detail/build/outputs/androidTest-results/connected/debug/TEST-MulletaflixCodexPhoneApi35_20261012(AVD) - 15.xml`, timestamp 2026-10-13T09:56:55; 0 falhas, 0 erros, 0 ignorados.
- `AMBIENTE`: wrapper confirmou QEMU na GPU NVIDIA RTX 3050, encerrou a AVD e ADB terminou sem dispositivos. AVD reservada `MulletaflixApi35` não foi usada.
- `RED/GREEN`: revalidação de teste existente; sem edição de comportamento e sem RED/GREEN atribuível nesta rodada.
- `LIMITES`: MockWebServer e MOBI sintético; não valida arquivo real, servidor, aparelho físico ou TalkBack. Não é homologação formal.
- `ESCOPO`: instrumentação e documentação APK; sem alteração de produção, versão, assinatura, release, publicação, servidor ou portal.

## Integração Media3 — pausa/retomada por download — 2026-10-13

- `INTENT`: pausar uma mídia não suspende a fila inteira; downloads irmãos continuam, bytes persistidos são preservados e retomar conclui somente o item pausado sem alterar a preferência global.
- `COBERTURA`: teste Compose de linha valida ação acessível e callback; testes JVM ViewModel validam IDs/resultados individuais; AndroidTest cria dois requests Media3 com sessão/cache/metadados temporários, pausa um após progresso parcial, deixa o irmão completar e retoma o primeiro via Range.
- `RED/GREEN`: a cobertura Compose foi observada falhando antes da implementação porque a linha não expunha a ação acessível por item. Nesta revalidação, a integração revelou dois problemas de harness, não do produto: a chamada inicial usou `itemId` onde o método espera `requestId`; depois disso, MockWebServer sem suporte Range/206 não permitia concluir a retomada. A fixture agora responde com `Content-Range`; GREEN final 1/1. Não rotular falhas do harness como regressões de produção.
- `QUALITY GATE`: `testDebugUnitTest :feature:downloads:lintDebug :app:lintDebug :app:assembleDebug --no-daemon --console=plain` — BUILD SUCCESSFUL; 1.734 testes em 252 relatórios, 0 falhas/erros/ignorados. Compilação AndroidTest de downloads/app passou. `OfflinePlayerProductionDownloadFlowTest.pausingOnePartialDownloadPreservesItWhileItsSiblingContinues` passou 1/1 em PHONE/API 35.
- `DISPOSITIVO`: `tools/with-emulator.ps1` abriu `MulletaflixCodexPhoneApi35_20261012` na porta 5580; QEMU foi confirmado na NVIDIA RTX 3050; o wrapper encerrou o AVD e `adb devices -l` ficou sem dispositivos. AVD reservada `MulletaflixApi35` não foi usada.
- `LIMITES`: MockWebServer, conteúdo sintético e armazenamento do AVD; sem servidor real, rede instável, pouco espaço ou aparelho físico. Evidência amplia cobertura local de Downloads, não encerra APK-H17 nem muda o aceite formal geral. Sem bump, APK de produção ou release.

## Isolamento do efeito de atualização — 2026-10-13

- `INTENT`: tornar a checagem periódica de atualização desativável pela fixture AndroidTest para evitar tráfego externo incidental, preservando a checagem normal no APK de produção.
- `RED`: `AppUpdateCheckEffectTest.disabling_a_running_effect_stops_future_update_callbacks` falhou pela asserção esperada: callbacks subiram de 2 para 5 após `enabled=false`, pois o efeito não observava o parâmetro.
- `GREEN`: `LaunchedEffect` agora inclui `enabled` na chave e termina imediatamente quando falso. `MainActivity` compõe o efeito após resolver sessão e fornece `shouldCheckForAppUpdates(BuildConfig.DEBUG, suppressUpdateCheckForTest)` como `enabled`; a policy garante que o extra só afeta build debug. `AppUpdateCheckEffectTest` passou 2/2 em PHONE/API 35; `AppUpdatePolicyTest` passou 5/5; `MainActivitySessionNavigationTest` passou 9/9 em evidência anterior.
- `QUALITY GATE`: `testDebugUnitTest :app:lintDebug :app:assembleDebug :app:compileDebugAndroidTestKotlin --no-daemon --console=plain` concluiu `BUILD SUCCESSFUL`; 1.735 testes JVM/252 XML, zero falhas, erros ou ignorados.
- `LIMITE`: a alteração torna os testes herméticos sem certificar área de produto. Matriz formal permanece 11/21 localmente evidenciadas, 1 em validação, 9 pendentes, aceite formal pendente. Sem release/APK de produção e sem mudança de servidor.
- `INTENT`: today the Activity starts the external update check after the session is resolved; the navigation test needs to be hermetic; production behavior must remain unchanged, so suppression stays limited to debug/test fixtures.
- `TWINS`: searched `AppUpdateCheckEffect(` and `ActivityScenario.launch(` across app sources; production has the MainActivity effect callsite, and the navigation suite launches only through its test helper.

## Integração do fluxo de eventos WebSocket SyncPlay — 2026-10-13

- `INTENT`: confirmar no transporte WebSocket real do cliente que os callbacks de conexão e mensagem alimentam o fluxo público `events`, além dos testes isolados do parser.
- `COBERTURA`: teste em `SyncPlayRealtimeClientTest` conecta a `MockWebServer`, recebe handshake e envia `SyncPlayCommand`; coleta `Connected` e o objeto `Command` decodificado via `client.events`.
- `VERIFICAÇÃO`: `:core:api:testDebugUnitTest --tests 'org.mulletaflix.core.api.SyncPlayRealtimeClientTest.websocket handshake and command are delivered through the events flow' --no-daemon --console=plain` — `BUILD SUCCESSFUL`, 1/1 teste.
- `HARNESS`: uma primeira versão bloqueou o event loop do `runBlocking` com espera síncrona de latch; mover a espera do handshake para `Dispatchers.IO` corrigiu o harness. Tentativas de iniciar fechamento remoto no MockWebServer não entregaram o callback no prazo; o teste final limita-se a eventos de conexão e comando e não afirma cobertura de `Disconnected`.
- `LIMITES`: sem mudança de produção, servidor real ou aparelho físico. Integração de `onClosed`/`onFailure`, emissão de `Disconnected` e cancelamento de reconexão após `stop()` seguem pendentes. A evidência foi sincronizada no portal após o quality gate; sem APK/release de produção.

## RED-GREEN — fechamento remoto e cancelamento do retry SyncPlay — 2026-10-13

- `GREEN`: `SyncPlayRealtimeClient.onClosing` responde somente à conexão atual; o sentinela 1005 (frame remoto sem status) é convertido para 1000. Casos locais cobrem handshake HTTP 503, close remoto com e sem status, emissão/reconexão, resposta mascarada válida e `stop()` antes da repetição.
- `HARNESS`: o peer TCP lê upgrade e resposta pelo mesmo `DataInputStream`. O teste de cancelamento usa `TestCoroutineScheduler`, captura o Job de retry durante backoff suspenso, chama `stop()` e avança o relógio virtual; nenhuma segunda requisição é enviada.
- `VERIFICAÇÃO`: testes `SyncPlayRealtimeClientTest` passaram. Gate completo — `testDebugUnitTest :core:api:lintDebug :app:lintDebug :app:assembleDebug :core:api:compileDebugAndroidTestKotlin :app:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL`, 1.740 testes JVM/252 relatórios, zero falhas/erros/ignorados. Portal local: 9/9; `git diff --check` passou.
- `LIMITE`: teste loopback/MockWebServer não confirma servidor de mídia, rede real, aparelhos físicos ou dois players reais. Homologação permanece 11/21 áreas com evidência, 1 em validação, 9 pendentes, `formalSignoff=pending`. Nenhuma release ou publicação pública.
