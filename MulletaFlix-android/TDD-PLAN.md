# Plano de TDD do MulletaFlix Android

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
4. **Navegação de produção:** `app/.../MulletaFlixNavHost.kt` — sessão ausente/válida/incompleta, restauração, logout, deep link frio e `onNewIntent` no grafo real. Os testes de rota simplificados continuam úteis, mas não substituem esta integração.
5. **Build/package APK:** `build-app-package.ps1` — `-SkipBuild` não pode aceitar APK antigo/versão incompatível; artefato ausente ou assinatura divergente deve falhar sem copiar/publicar.
6. **Fluxos visíveis de features:** `LiveTvScreen` deve enviar o ID correto ao iniciar canal/gravação; `ProfileScreen` deve refletir confirmação de logout/troca de conta; Downloads devem sobreviver a reinício/retomar e abrir arquivo offline; Login/registro devem demonstrar validação, erro e sucesso; SyncPlay deve conectar ações da tela, eventos e estado final.

### P2 — persistência, conectividade e UX adaptativa

1. `SearchHistoryRepositoryImpl`: persistência, isolamento por usuário, limite/ordem, duplicatas, remoção, limpeza e JSON corrompido.
2. `HomeFeedCacheRepositoryImpl`: DataStore concreto, reconstrução do repositório e estado corrompido.
3. `ConnectivityNetworkMonitor`: estado inicial, mudanças em múltiplas redes, rede tarifada e desregistro do callback ao cancelar Flow.
4. `CheckAppUpdateUseCase`: atualização disponível/indisponível e erro do repositório.
5. Acessibilidade integrada em telas e diálogos, com ordem TalkBack/foco, escala de fonte e estados visuais; complementar a cobertura existente de componentes e contraste.
6. Matriz de layout/interação para PHONE, TABLET e TV: alvo touch, clipping, escala, rolagem e foco D-pad. Um teste portátil genérico não comprova os três perfis.

### P3 — suporte de testes

`:core:testing` contém fixtures compartilhadas. Não precisa de teste próprio enquanto permanecer apenas como dados constantes; se fixtures ganharem lógica, cobrir determinismo, valores padrão e casos representativos no próprio módulo.

- **Quality gate no CI Android:** `../.github/workflows/ci.yml` executa `./gradlew test` e `./gradlew assembleDebug`, mas não declara lint Android, instrumentação em emulador nem upload dos relatórios de testes Android/JUnit. Adicionar lint e relatórios como evidência; avaliar instrumentação PHONE/TABLET/TV em runners que suportem AVD, sem fazer os gates básicos dependerem de secrets.

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

### Revalidação Android — 2026-10-08

- `QUALITY GATE`: `testDebugUnitTest :core:common:lintDebug :core:api:lintDebug :app:lintDebug :app:assembleDebug --no-daemon --console=plain` — `BUILD SUCCESSFUL`; relatórios XML: 238 arquivos, 1.545 testes JVM, 0 falhas, 0 erros e 0 ignorados. Lint e build Debug passaram.
- `DEVICE`: `AppUpdateInstallerIntegrationTest` 6/6 e `SessionRepositoryPersistenceTest` 3/3 no AVD PHONE/API 35. `HomeLibraryNavigationTest` 3/3 no AVD TV/API 34; `books_libraryIsNotRenderedOnAndroidTv` confirmou ausência de Livros e presença de Filmes na tela real Compose. `tools/with-emulator.ps1` confirmou NVIDIA RTX 3050 para cada AVD e encerrou os emuladores; `adb devices` ficou sem devices.
- `RED/GREEN`: as mudanças de updater/sessão revisadas já estavam no worktree antes desta revalidação; não atribuir RED-GREEN retrospectivo aos testes acima. O compilador inicialmente revelou que o fake de `Interceptor.Chain` do teste off-subnet precisava implementar novos membros do OkHttp 5; após completar o fake, as classes `CleartextRequestProtectionTest` e `AppUpdateDownloaderTest` executaram sem falhas.
- `LIMITAÇÕES`: nenhuma release ou APK de produção foi gerado. A configuração global de cleartext permanece pendente (APK-H12); testes de rota controlada não substituem ensaio de rede real. A solicitação de incluir OCR offline requer autorização explícita para acrescentar a dependência empacotada.

## Critério de conclusão do plano

- TDD RED-GREEN demonstrado em toda mudança executável nova ou alterada.
- Lacunas P1 encerradas por testes primeiro, com resultados registrados.
- Lacunas P2 e P3 fechadas na sequência, sem ignorar perfis de dispositivo afetados.
- Suite completa, lint e build passam; instrumentação executa casos nos perfis pertinentes.
- Histórico antigo sem evidência permanece explicitamente “não auditado”; nunca reclassificar testes retroativamente como TDD sem prova.
