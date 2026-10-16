# Testes do MulletaFlix Android

### Caracterização de descoberta e recuperação HTTPS na LAN — 2026-10-16

Como o requisito de certificado HTTPS válido na LAN foi confirmado, foram adicionados três testes de caracterização sem mudar código produtivo: `LocalServerDiscoveryTest` preserva esquema HTTPS, porta, caminho e `serverId` da resposta de descoberta; `ServerSelectionPolicyTest` seleciona automaticamente o endpoint HTTPS LAN apenas para a identidade salva; `LanServerRecoveryPolicyTest` mantém esse endpoint elegível para recuperação autenticada. Resultado focal: 4/4, 23/23 e 27/27 (54 no total; zero falhas/erros/ignorados). Quality gate: `testDebugUnitTest :feature:auth:lintDebug :app:lintDebug :app:assembleDebug :feature:auth:compileDebugAndroidTestKotlin :app:compileDebugAndroidTestKotlin` BUILD SUCCESSFUL (881 tarefas; 8 executadas, 873 `UP-TO-DATE`). Reexecução forçada de `testDebugUnitTest --rerun-tasks`: 1.788 testes em 256 XML, 0 falhas/erros/ignorados (470 tarefas executadas). Isto valida preservação e seleção de URL em regras puras; não faz handshake TLS, não verifica certificado, nem comprova tráfego em LAN física. HTTPS real segue pendente.

### Tentativa inconclusiva Android 17/API 37 — 2026-10-16

`LocalServerDiscoveryPermissionTest` não iniciou: no AVD isolado `MulletaFlixAndroid17Api37_20261016`, `sys.boot_completed` permaneceu vazio apesar de serviços do Package Manager iniciarem parcialmente. O wrapper foi interrompido antes do Gradle/AndroidJUnit; zero casos contabilizáveis, sem XML novo. AVD encerrado sem limpeza dos dados. Classificação: bloqueio de infraestrutura, não falha do app nem aprovação. Android 17/API 37 e a permissão `ACCESS_LOCAL_NETWORK` continuam sem cobertura instrumentada verificada. Registro de rodada adicionado ao JSON local do portal; a implantação pública não foi confirmada.

### Gate geral após diagnóstico de boot API 37 — 2026-10-16

`testDebugUnitTest --rerun-tasks --no-daemon --console=plain` terminou `BUILD SUCCESSFUL` em 11m10s: 1.788 testes em 256 XML, zero falhas, erros ou ignorados (470 tarefas executadas). `Invoke-Pester -Path .\tools -PassThru`: 51/51; parser PowerShell: 24 scripts limpos. Testes do portal: 27/27. Isso verifica regressão JVM/harness/portal, mas não desbloqueia `LocalServerDiscoveryPermissionTest`, não executado por falha de boot do AVD. Sem alteração de runtime, versão ou release.


### Android 17/API 37 — repetição isolada com Vulkan desativado — 2026-10-16

Criado o AVD isolado `MulletaFlixAndroid17Api37VulkanOff_20261016` (Google APIs, 4 GiB, GPU host), iniciado em `emulator-5560` com `-feature -Vulkan`; NVIDIA RTX 3050 foi confirmada em `nvidia-smi`. `sys.boot_completed=1` e Package Manager chegou a responder, mas o serviço `package` ficou indisponível intermitentemente e `sys.system_server.start_count` atingiu 28. A execução de `LocalServerDiscoveryPermissionTest` não produziu relatório JUnit verificável e foi interrompida; contagem contabilizável: zero testes. Emulador foi encerrado sem limpar seus dados; ADB terminou sem dispositivos. Resultado inconclusivo de infraestrutura, não falha funcional nem aprovação. O novo AVD permanece preservado para inspeção. A cobertura API 37 para `ACCESS_LOCAL_NETWORK` continua sem homologação.

Requisito confirmado pelo responsável: servidores também podem oferecer HTTPS com certificado válido na LAN. O cliente deve manter validação normal da cadeia/certificado; esta confirmação não autoriza aceitar certificados inválidos nem mudar servidor. Teste real de TLS em LAN continua pendente.

### Persistência de evidência por fase do harness — 2026-10-16

`Test-AndroidDownloadProcessRestart.ps1` agora grava para cada fase executada (inclusive sucesso) o resultado bruto do runner, snapshot da fixture, logcat filtrado e status explícito dentro da pasta UUID de diagnóstico. Helper valida coerência entre saída AndroidJUnit e status; falha do runner não pode ser rotulada como sucesso. Teste integrado TABLET/API 35 passou prepare/verify/cleanup 1/1 cada; três conjuntos de artefatos estão em `validation-evidence/2026-10-16/download-process-restart-diagnostics/a94e2689-d949-4775-b6c5-cbc58365f8a5`. Logcat contém ruído sistêmico histórico, portanto não é evidência isolada de causalidade. Suíte Pester tools: 48/48. Gate Gradle completo (`testDebugUnitTest :app:lintDebug :app:assembleDebug :app:compileDebugAndroidTestKotlin --rerun-tasks --no-daemon`) passou: BUILD SUCCESSFUL, 871 tarefas executadas; avisos de depreciação Kotlin/Compose registrados. Sem homologação formal.

### Repetição diagnóstica da retomada offline — TABLET/API 35 — 2026-10-16

`tools/Test-AndroidDownloadProcessRestart.ps1` passou nas três fases: preparação, retomada visual após `force-stop`/relançamento e cleanup UUID, 1/1 cada. Range retomou no byte 1.034.976 e recebeu os 3.852.679 bytes restantes; a tela mostrou o título, Media3 chegou a `READY`, avançou offline e não fez requests à origem bloqueada. O wrapper confirmou NVIDIA RTX 3050 e encerrou o AVD. A instalação debug/AndroidTest terminou `BUILD SUCCESSFUL` (431 tarefas, 2 executadas/429 `UP-TO-DATE`). A falha histórica `failureReason=1`/0 bytes/zero requests não se reproduziu; esta execução não explica sua causa nem prova estabilidade prolongada. AVD sintético, sem servidor/hardware reais ou promoção de homologação.

### Teste real do caminho de preservação de falha — AndroidJUnit + TABLET/API 35 — 2026-10-16

O canário `failureEvidenceCanaryFailsOnlyWhenExplicitlyRequested` foi executado por `tools/Test-AndroidDownloadProcessRestart.ps1 -ExerciseFailureEvidenceCanary` no AVD TABLET/API 35. O runner produziu a falha intencional esperada (`Tests run: 1, Failures: 1`); o harness exige contagem exata e zero requests à fixture antes de aceitar o cenário, preservando runner/fixture/logcat em `validation-evidence/2026-10-16/download-process-restart-diagnostics/3ae8e1da-779d-4f11-b87b-11324538f192`. `cleanupIsolatedHostProcessFixture` passou 1/1. O canário não executa uma jornada de download/player; contudo, a inicialização do app instrumentado iniciou o serviço Media3 com `RESUME_DOWNLOADS`, efeito observado no logcat. A fixture não recebeu requests; o harness agora também reprova se isso mudar. Por segurança a validação foi feita somente no AVD descartável sem dados de usuário. O wrapper confirmou NVIDIA RTX 3050 e encerrou o AVD; ADB ficou vazio. Pester focal passou 4/4 e a suíte final de `tools` passou 46/46; parser PowerShell sem erros. As duas tentativas anteriores encontraram divergências no nome do método/argumento e no prefixo do artefato; não foram aprovadas como evidência. Nenhuma assinatura/release ou homologação formal foi promovida.

### Repetição da retomada offline — TABLET/API 35 — 2026-10-16

`Test-AndroidDownloadProcessRestart.ps1` completou preparação, retomada visual após reinício do processo e limpeza UUID: 1/1 por fase. Range retomou no byte 1.039.501 e recebeu 3.848.154 bytes; o título apareceu, Media3 chegou a `READY`, avançou offline e a auditoria não observou pedidos depois de bloquear a origem. Instalação debug/AndroidTest: `BUILD SUCCESSFUL`, 431 tarefas (2 executadas, 429 `UP-TO-DATE`). O wrapper confirmou NVIDIA RTX 3050 e fechou o AVD; `adb devices` terminou vazio. Como a execução passou, não exercitou a gravação de evidência numa falha real; nenhuma pasta de diagnóstico foi criada. Fixture sintética; não valida hardware/servidor reais nem promove APK-H07/H17 ou aceite formal.

### Repetição adicional TABLET da retomada offline — 2026-10-16

`tools/Test-AndroidDownloadProcessRestart.ps1` passou em TABLET/API 35: preparação, retomada visual depois de `force-stop`/relançamento e cleanup UUID, 1/1 por fase. A fixture registrou Range no byte 1.038.304 e concluiu os 3.849.351 bytes restantes; título visível, Media3 `READY`, avanço offline e origem desativada sem novos pedidos. O wrapper confirmou NVIDIA RTX 3050 e encerrou o AVD; `adb devices` terminou vazio. A invocação anterior desta mesma tentativa falhou no encaminhamento de argumentos do wrapper antes do teste e não conta como execução. Sem falha instrumentada, não se criou pasta de evidência; persistência após falha runtime real permanece não exercitada. Sintético; não fecha APK-H07/H17 nem aceite formal.

### Repetição final TABLET após endurecer o harness — 2026-10-16

Após corrigir a persistência para aceitar runner sem texto, a jornada passou em TABLET/API 35: preparação, retomada visual e cleanup UUID 1/1 por fase; Range retomou em 1.033.984 e completou 3.853.671 bytes, `VideoPlayerScreen` exibiu o título, Media3 chegou a `READY`, avançou offline e não houve requests à origem bloqueada. A repetição imediatamente anterior tinha falhado antes de qualquer request (`state=FAILED`, `failureReason=1`, `bytes=0`); cleanup UUID passou, mas a gravação foi rejeitada pelo parâmetro de saída vazia. A execução verde final não exercitou o caminho de falha após a correção. Pester de todas as ferramentas passou 42/42; parser PowerShell sem erros; gate Gradle reportado abaixo no TDD (testes JVM `UP-TO-DATE`). Wrapper confirmou NVIDIA RTX 3050, fechou o AVD e `adb devices` terminou vazio. A causa da falha sem request permanece desconhecida; fixture não valida servidor/mídia/hardware reais nem promove homologação formal.

### Repetição TABLET e persistência controlada do diagnóstico — 2026-10-16

Após adicionar um gravador de evidência para falhas de instrumentação, a jornada `Test-AndroidDownloadProcessRestart.ps1 -ExpectedProfile TABLET` passou: preparação, retomada visual e cleanup 1/1 por fase; Range retomou em 1.040.640 e completou 3.847.015 bytes, o player ficou `READY`, avançou offline e a origem bloqueada não recebeu pedidos. Como a execução foi verde, o caminho de gravação por falha não foi acionado em runtime; Pester validou separadamente que runner, snapshot da fixture e logs filtrados são persistidos fora do diretório temporário. `Invoke-Pester -Path .\tools` passou 37/37. Instalação debug/AndroidTest passou (431 tarefas: 2 executadas, 429 `UP-TO-DATE`); wrapper confirmou NVIDIA RTX 3050, encerrou o AVD e `adb devices` terminou vazio. Evidência sintética; não valida servidor/mídia/hardware reais nem promove APK-H07/H17 ou aceite formal.

### Reteste do diagnóstico de crash antes da fixture — TABLET/API 35 — 2026-10-16

O filtro ampliado foi exercitado durante uma repetição por `tools/with-emulator.ps1` + `tools/Test-AndroidDownloadProcessRestart.ps1`. A fase `preparePartialDownloadForHostProcessRestart` retornou `INSTRUMENTATION_RESULT: shortMsg=Process crashed` e `INSTRUMENTATION_CODE: 0`; `/status` mostrou `requests=[]`, `completedRequests=[]` e `rangeStarts=[]`. O logcat selecionado mostrou `AndroidJUnitRunner` iniciando `newApplication`/`onCreate`, seguido de `ActivityManager: Crash of app ... AndroidJUnitRunner`; não trouxe stack trace nem causa raiz. O runner respondeu `OK (1 test)` na limpeza UUID. O harness retornou erro porque a preparação não satisfez seu contrato de exatamente um teste aprovado. Portanto o teste não chegou ao download/player e não indica defeito de produção. O wrapper encerrou o AVD temporário e a NVIDIA RTX 3050 foi confirmada. A primeira invocação desta repetição falhou na passagem de argumentos do wrapper antes do teste e não conta como execução.

### Estabilidade da troca de servidor pelo Perfil — repetição adicional — 2026-10-16

O cenário focal `MainActivitySessionNavigationTest#switchingServerFromProfileDoesNotRestorePreviousProfileOnBackAfterLogin` passou em três execuções independentes no PHONE/API 35 e três no TABLET/API 35, todas 1/1, sem falhas, erros ou skips. XMLs preservados em `validation-evidence/2026-10-16/server-switch-{PHONE,TABLET}-API35-repeat-{1,2,3}.xml`. Cada execução usou `tools/with-emulator.ps1`, confirmou NVIDIA RTX 3050 e encerrou o AVD; ADB terminou vazio. Somadas às duas aprovações anteriores por perfil, são cinco repetições verdes após o ajuste da asserção. A falha TABLET repeat 2 anterior permanece sem causa raiz; fixtures/MockWebServer não validam servidor ou aparelho real e APK-H02 segue em validação formal.

### Revalidação da atualização automática da Home — Android TV — 2026-10-16

`TvHomeRefreshIntegrationTest` passou 2/2 no AVD `MulletaflixCodexTvApi34_20261015` (API 34): o feed visível é substituído ao retornar à Home e a biblioteca de livros permanece oculta. XML: `feature/home/build/outputs/androidTest-results/connected/debug/TEST-MulletaflixCodexTvApi34_20261015(AVD) - 14.xml` (2026-10-16 15:40 local). `tools/with-emulator.ps1` confirmou NVIDIA RTX 3050; ADB terminou sem dispositivos. Revalidação de testes existentes, sem mudança no runtime. Fixture sintética; não cobre TV física, servidor real, atualização contínua prolongada nem homologação formal.

### Retomada de download instável antes da fase do player — TABLET/API 35 — 2026-10-16

Uma repetição integrada falhou em `awaitDownload(STATE_COMPLETED)` após 120 s: Media3 permaneceu `QUEUED` (`state=0`), `3.989.823/4.887.655` bytes (81,63%), `stopReason=0`, `failureReason=0`; a fase visual do player não iniciou. O script só chama a verificação depois de o host registrar término da resposta Range, mas `response.finish` da fixture não prova que Media3 persistiu todos os bytes. Não há causa estabelecida.

O diagnóstico do harness também preserva sinais do runner Android: `AndroidRuntime`, `FATAL EXCEPTION`, sinais nativos, `AndroidJUnitRunner`, morte do processo e bloqueios de início do serviço. Isso foi adicionado após uma tentativa TABLET/API 35 em que `preparePartialDownloadForHostProcessRestart` retornou `Process crashed` antes de qualquer requisição à fixture; o log anterior mostrava o bloqueio de início do serviço, mas filtrava os eventos necessários para identificar a causa. Essa tentativa não alcançou o cenário nem comprova defeito no APK. Os testes Pester do filtro passaram 2/2, incluindo logcat vazio, e o parser PowerShell não apontou erros.

Na repetição integrada posterior, preparação, retomada visual após reinício do processo e cleanup passaram 1/1 por fase. Range iniciou no byte 1.034.880, completou os 3.852.775 bytes restantes, o player avançou offline e a origem bloqueada não recebeu pedidos. Quando esta anotação foi escrita, o filtro ampliado ainda não havia sido exercitado em falha instrumentada; o reteste descrito acima agora o exercitou sem revelar a causa do crash. As falhas históricas de posição 0 ms também seguem sem explicação.

Verificações nesta rodada: `Invoke-Pester -Path .\tools` — 36/36 aprovados após REDs esperados para logcat vazio e filtro que ignorava crash do runner; parser PowerShell sem erros; Gradle instalou `:app:installDebug` e `:app:installDebugAndroidTest` (`BUILD SUCCESSFUL`, 431 tarefas; 2 executadas/429 `UP-TO-DATE`); integração passou prepare/verify/cleanup 1/1 por fase. AVD TABLET temporário foi executado pelo wrapper com GPU NVIDIA e desligado; `adb devices` ficou sem dispositivos. A primeira tentativa teve falha de instrumentação antes da fixture e não conta como falha funcional. Sem alteração de produção, servidor, portal, versão ou homologação formal.

### Corrida de conclusão do download no fixture offline — 2026-10-16

O primeiro reteste TABLET após as falhas anteriores parou em 89,4% (`4.368.544/4.887.655` bytes), sem falha HTTP ou `failureReason`. A inspeção do fluxo mostrou que o script desligava a origem quando o host HTTP terminava de enviar a resposta, antes do AndroidTest observar `Download.STATE_COMPLETED`; o término da resposta do host não comprova que o Media3 persistiu/indexou o download todo. Ajustei apenas a sequência do harness: o teste agora bloqueia a origem via `POST /disable-media` depois dos asserts de estado `COMPLETED`, contagem integral de bytes e MIME; o PowerShell não desativa mais o fixture antecipadamente. Nenhum assert foi relaxado e nenhum código de produção mudou.

Após a correção, a jornada TABLET/API 35 passou 2 execuções completas consecutivas: preparação, retomada após reinício do processo e limpeza isolada passaram 1/1 cada; Range retomou em 1.042.381 e 1.042.925, e em ambas o download completou (3.845.274 e 3.844.730 bytes restantes), a reprodução offline avançou e a origem bloqueada não recebeu requests. Uma terceira inicialização do wrapper não chegou a iniciar AVD ou teste; foi interrompida depois de ADB permanecer sem dispositivos e não conta como aprovação nem como falha funcional. O AVD `MulletaflixApi35` não foi tocado. As duas execuções verdes reduzem a evidência da falha prévia a uma corrida do harness, mas ainda não cobrem aparelho, mídia ou servidor reais nem demonstram estabilidade mais ampla.

Gate Android forçado após a alteração: `testDebugUnitTest --rerun-tasks :app:lintDebug :app:assembleDebug :app:compileDebugAndroidTestKotlin --no-daemon --console=plain` concluiu `BUILD SUCCESSFUL` em 22m25s, 871/871 tarefas executadas. XMLs recontados: 1.785 testes em 256 relatórios, zero falhas, erros e ignorados. `git diff --check` passou (avisos apenas de conversão LF/CRLF do Git no Windows).

### Estabilidade da retomada offline visual — repetição PHONE/TABLET — 2026-10-16

Repetição adicional TABLET/API 35 em `MulletaflixCodexTabletApi35_20261015`: preparação, retomada após `force-stop`/relançamento e limpeza passaram 1/1 por fase. Range reiniciou no byte 1.032.992 e recebeu os 3.854.663 bytes restantes; Media3 completou o download, a tela exibiu o título, atingiu `READY`, avançou ≥500 ms e a auditoria não detectou pedido à origem bloqueada. Essa execução confirma o caminho verde, mas não explica as falhas anteriores com posição parada em 0 ms; não houve mudança produtiva. Wrapper confirmou GPU NVIDIA e desligou o AVD; ADB terminou sem dispositivos. Um invocation preliminar nesta sessão apontou caminho inexistente para o script e falhou antes de executar testes; não conta como resultado funcional.

Repetições adicionais de `DownloadProcessRestartIntegrationTest` em PHONE/API 35 e TABLET/API 35, com `offline-process-restart.mkv` sintético (H.264/AAC, ~20 s), sessão local e origem HTTP bloqueada durante o playback. PHONE passou preparação, retomada visual e limpeza UUID, 1/1 por fase. TABLET repetição 1 e 2 falharam no limite de avanço: posição permaneceu 0 ms por 30 s; na primeira `isPlaying=false`, na segunda `isPlaying=true`, ambas com `playWhenReady=true`. Repetição TABLET 3 passou 1/1 e limpeza; Range retomou em 921.421 e recebeu os 3.966.234 bytes restantes. A intermitência não foi atribuída ao runtime nem corrigida; o assert de avanço ≥500 ms foi mantido. Diagnósticos adicionais ficam apenas no AndroidTest. AVDs temporários encerrados pelo wrapper; GPU NVIDIA RTX 3050 confirmada e ADB sem dispositivos ao fim.

Gate forçado `testDebugUnitTest --rerun-tasks :app:lintDebug :app:assembleDebug :app:compileDebugAndroidTestKotlin --no-daemon --console=plain`: `BUILD SUCCESSFUL`, 871/871 tarefas executadas; XMLs locais somam 1.785 testes em 256 relatórios, 0 falhas, 0 erros e 0 ignorados. Lint, assembleDebug e compilação AndroidTest incluídos. `git diff --check` passou (avisos apenas de conversão LF/CRLF). Não promove APK-H07/H17, matriz ou aceite formal; servidor, mídia real, hardware e estabilidade prolongada continuam pendentes.

### Retomada offline visual após morte do processo — TABLET e Android TV — 2026-10-16

A jornada `tools/Test-AndroidDownloadProcessRestart.ps1` passou em dois perfis AVD adicionais, além das execuções PHONE documentadas abaixo. TABLET/API 35 (`MulletaflixCodexTabletApi35_20261015`, porta 5558): preparação, retomada visual após `force-stop`/relançamento e limpeza isolada passaram 1/1 por fase; Range retomou em 1.035.968 e recebeu 3.851.687 bytes. TV/API 34 (`MulletaflixCodexTvApi34_20261015`, porta 5556): 1/1 por fase; Range retomou em 1.034.880 e recebeu 3.852.775 bytes. Em ambos, o título ficou visível, Media3 chegou a `READY`, a reprodução avançou e o endpoint de auditoria não detectou acesso à origem de mídia depois de desabilitada.

O harness aceita `-ExpectedProfile PHONE|TABLET|TV` (PHONE por padrão) e encaminha o serial/avaliação do perfil ao runner. A execução inicial do tablet revelou uma asserção AndroidTest que fixava `emulator-5556`; ela foi generalizada para o emulador explicitamente escolhido, enquanto o script PowerShell continua limitando a instalação ao serial/AVD solicitado e recusa outro dispositivo conectado. O wrapper confirmou GPU NVIDIA e encerrou os AVDs; a fase de limpeza UUID passou. Instrumentação direta via `am instrument` não gera XML individual. AVDs, fixture Matroska sintética e sessão local não validam servidor/mídia reais, hardware físico, TalkBack, estabilidade prolongada ou aceite formal; APK-H07/H17 e `formalSignoff` não avançam.

### Repetição independente da retomada offline após morte do processo — 2026-10-16

Uma segunda execução integrada independente passou em PHONE/API 35: preparação, verificação visual após morte do processo e limpeza isolada passaram 1/1 cada. Range retomou no byte 1.040.096 e recebeu 3.847.559 bytes restantes; `VideoPlayerScreen` mostrou o título, Media3 chegou a `READY`, a posição avançou ≥500 ms e o endpoint de auditoria não observou pedidos à origem de mídia bloqueada. Com a execução anterior de 16/10, são duas execuções verdes independentes no AVD sintético. Instrumentação via ADB não gera XML individual; saída do runner preservada nesta sessão. Wrapper confirmou NVIDIA e encerrou o AVD PHONE temporário; ADB terminou sem dispositivos.

Tentativa inicial pela porta 5558 expirou no boot sem iniciar teste; repetição pela porta documentada 5556 foi concluída. Isso é limitação de inicialização do AVD, não falha funcional. Fixtures/sessão sintéticas não validam servidor, mídia autorizada ou hardware reais, nem TV/tablet ou aceite formal.

### Retomada offline visual com teardown do ViewModelStore — 2026-10-16

Reteste após revisão de recursos: `DownloadProcessRestartIntegrationTest.verifyPartialDownloadResumedByFreshApplicationProcess` passou 1/1 em PHONE/API 35; preparação e cleanup UUID também passaram 1/1 cada. Range retomou em 1.046.400 bytes, recebeu 3.841.255 bytes e completou o fixture. `VideoPlayerScreen` exibiu o título, Media3 chegou a `READY`, posição avançou ≥500 ms e endpoint de auditoria confirmou zero requisições à origem bloqueada. Teardown remove a tela por estado Compose, aguarda ociosidade, limpa o `ViewModelStore` manual na main thread e afirma que a chave foi removida. `am instrument` não produziu XML individual.

Gate após a correção: `testDebugUnitTest :app:lintDebug :app:assembleDebug :app:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL`, 871 tarefas, 1 executada e 870 `UP-TO-DATE`; a suíte JVM ficou `UP-TO-DATE` e não conta como reexecução. Wrapper confirmou NVIDIA RTX 3050, desligou o AVD PHONE temporário e ADB terminou vazio. `git diff --check` passou antes da última sincronização documental; fixture sintética, não valida servidor/hardware reais nem promove APK-H07/H17 ou aceite formal.

### Prioridade CSS `!important` HTML/KF8 — 2026-10-15

`PlainTextBookDocumentTest` passou 45/45; TDD cobre cascata `!important`, ordem-fonte entre blocos `<style>`, não vazamento por `<br>`, `<img/>`, `<span/>` e `<embed>`, além de `/` dentro de atributo quoted. `PlainTextBookReaderIntegrationTest.htmlBookImportantStylesheetOverridesNormalInlineFormatting` passou 1/1 em PHONE/API 35, TABLET/API 35 e TV/API 34. XMLs preservados em `validation-evidence/2026-10-15/html-important-cascade-{phone-api35,tablet-api35,tv-api34}.xml`. AVDs temporários usaram GPU NVIDIA confirmada pelo wrapper, foram encerrados; ADB terminou sem dispositivo. Gate forçado `testDebugUnitTest --rerun-tasks :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin :app:compileDebugAndroidTestKotlin --no-daemon --console=plain`: `BUILD SUCCESSFUL`, 881 tarefas; 1.784 testes JVM em 256 XML, 0 falhas/erros/ignorados.

### Revalidação do ciclo de vida do player — TV/tablet — 2026-10-15

- TV/API 34: `PlayerBackgroundPlaybackTest` passou 3/3; tablet/API 35:
  `PlayerPictureInPictureLifecycleTest` passou 1/1. XMLs:
  `validation-evidence/2026-10-15/player-background-playback-TV-API34.xml` e
  `player-pip-close-TABLET-API35.xml`.
- AVDs dedicados e atualmente desligados: `MulletaflixCodexTvApi34_20261015`
  (`android-tv; x86; tv_1080p`) e
  `MulletaflixCodexTabletApi35_20261015`
  (`google_apis_playstore_tablet; x86_64; pixel_tablet`). Foram executados pelo
  wrapper, que confirmou GPU NVIDIA e os encerrou; ADB terminou sem dispositivos.
  O AVD pessoal reservado `MulletaflixApi35` não foi usado.
- Mídia WAV e PiP são fixtures locais; isso não valida stream/servidor remoto,
  hardware físico ou aceite formal.

### Decoração textual HTML/KF8 — 2026-10-15

`PlainTextBookDocumentTest.preserves semantic and bounded CSS underline and line-through styles` valida `<u>/<ins>/<s>/<strike>/<del>` e `text-decoration` limitado (`underline`, `line-through`, `none`) inline/embutido. `PlainTextBookReaderIntegrationTest.htmlBookPreservesUnderlineAndLineThroughFormatting` inspeciona os `SpanStyle` gerados pela tela Compose; passou 1/1 em PHONE/API 35 (6,300 s), zero falhas/erros/ignorados. XML em `validation-evidence/2026-10-15/kf8-text-decoration-PHONE-API35.xml`.

Quality gate completo forçado em 15/10/2026: 1.779 testes JVM em 256 XML, 0 falhas/erros/ignorados; `:feature:item-detail:lintDebug`, `:app:lintDebug`, `:app:assembleDebug` e compilação AndroidTest dos dois escopos passaram (`BUILD SUCCESSFUL`, 881 tarefas). O wrapper `tools/with-emulator.ps1` verificou GPU NVIDIA no AVD PHONE descartável e encerrou o emulador; não usei `MulletaflixApi35`. Fixture sintética; não valida livro real, TV/tablet, TalkBack, CSS externo ou homologação formal.

### Compatibilidade CBR após atualização do Junrar — 2026-10-15

`gradle/libs.versions.toml` atualiza a dependência existente Junrar 8.1.1 para 8.1.2; `dependencyInsight` confirmou 8.1.2 em `debugRuntimeClasspath`. `:feature:item-detail:testDebugUnitTest` passou e `CbrBookArchiveTest` passou 5/5 com fixture RAR4/CC0. `CbrBookReaderIntegrationTest.realRarPagesRenderAndNavigateWithReaderControls` passou 1/1 no AVD Android TV API 34 (NVIDIA verificada; wrapper encerrou o AVD). O gate completo passou: 1.776 testes JVM/256 XML, 0 falhas/erros/ignorados, `:feature:item-detail:lintDebug`, `:app:lintDebug`, `:app:assembleDebug`, e compilação AndroidTest da feature e do app (`BUILD SUCCESSFUL`, 881 tarefas). Auditoria do fonte v8.1.2 confirmou que o app chama `Archive(File, ArchiveOptions)`, portanto não usa o caminho `InputStream` reportado no antigo [issue #63](https://github.com/junrar/junrar/issues/63). O construtor ainda executa `setVolume`/`setChannel`/`readHeaders` e mantém cabeçalhos em lista interna antes de `CbrBookArchive` aplicar `MAX_ARCHIVE_ENTRIES`; a API de opções usada não fornece limite de cabeçalhos. Fixtures atuais não demonstram RAR5, arquivos reais ou aparelho físico. A mitigação de memória permanece pendente; não há um limite de produto aprovado nem teste que prove mitigação.

Rechecagem de capacidade upstream em 2026-10-16: a página oficial ainda marca v8.1.2 como release mais recente. `javap` no artefato 8.1.2 resolvido pelo Gradle confirma que `ArchiveOptions` expõe `password`, `maxDictionarySize` e `UnrarCallback`, sem limite de contagem de cabeçalhos. O código-fonte oficial da `Archive` mantém coleção interna de blocos; nenhuma opção configurável de máximo de cabeçalhos foi encontrada na release nem na branch principal consultada. Esta inspeção é evidência de API, não medição de pico de heap ou compatibilidade de alternativa. Não houve teste RAR5/arquivo real nem mudança de produção; antes de projetar uma mitigação é necessário um orçamento de recursos aprovado e benchmark com fixtures representativas.

### PDF escaneado: busca OCR pela tela do leitor — 2026-10-15

`BookReaderScreenIntegrationTest.scannedPdfOcrSearchFindsAndOpensMatchingPageOffline` abre no Compose um PDF sintético sem camada textual, usa o recognizer OCR local, busca `pagina vertical`, mostra o trecho reconhecido da página 2 e navega até ela. Passou 1/1 em PHONE/API 35 (21,859 s) e TABLET/API 35 (20,288 s), 0 falhas/erros/ignorados; XMLs independentes em `validation-evidence/2026-10-15/pdf-scanned-search-UI-{PHONE,TABLET}-API35.xml`. Isso caracteriza a jornada de UI local com OCR, mas não valida arquivo de mídia real, hardware, TalkBack nem bateria/memória em uso prolongado. Não houve alteração de produção.

### OCR e busca em PDF digitalizado paisagem/retrato — 2026-10-15

`PdfBookReaderIntegrationTest.scannedPdfPageIsRecognizedOfflineAndIndexedBySearch` usa duas páginas sintéticas sem camada textual: paisagem 1200×600 e retrato 600×1200. PHONE/API 35 reconheceu offline ambas e a busca encontrou cada frase na página correta; 1/1 em 9,123 s, XML `validation-evidence/2026-10-15/pdf-scanned-landscape-portrait-ocr-search-PHONE-API35.xml`. Isso prova duas orientações e busca multipágina numa fixture, não resolução variável, PDF extenso, mídia real, alfabetos não latinos, tablet/TV ou uso de memória/bateria em hardware.

Quality gate final da rodada anterior: `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin --no-daemon --console=plain` terminou `BUILD SUCCESSFUL` (871 tarefas; 6 executadas e 865 `UP-TO-DATE`). Os testes JVM estavam `UP-TO-DATE`, então não se afirma reexecução integral deles naquela rodada.

### Deep link de mídia mostra título retornado pelo servidor — 2026-10-15

`MainActivitySessionNavigationTest.authenticatedMediaDeepLinkOpensDetailsAndRequestsLinkedItem` agora valida o resultado visível, não apenas o GET: depois de abrir `mulletaflix://details`, a tela de detalhes precisa exibir `Linked media fixture`, nome literal retornado pelo MockWebServer. PHONE/API 35 passou 1/1 (7,951 s), 0 falhas/erros/ignorados; XML preservado em `validation-evidence/2026-10-15/authenticated-media-deep-link-visible-title-PHONE-API35.xml`. A UI já suportava renderizar o nome, portanto foi somente reforço de cobertura, sem alteração de produção. Não cobre servidor/aparelho real nem tablet/TV.

### Execução da navegação por perfil — 2026-10-15

#### Estabilidade do Perfil após troca de servidor

O teste `MainActivitySessionNavigationTest#switchingServerFromProfileDoesNotRestorePreviousProfileOnBackAfterLogin` passou em PHONE/API 35 e TABLET/API 35 após separar a espera por `Meu Perfil`, a presença semântica de `New user`, uma única rolagem e a asserção de exibição. Evidências finais: `validation-evidence/2026-10-15/server-switch-stability-PHONE-API35-repeat-3.xml` (18,358 s) e `...-TABLET-API35-repeat-3.xml` (13,728 s). Uma repetição anterior no TABLET falhou na asserção mesmo com o texto na árvore semântica; o XML da falha foi preservado como `...-TABLET-API35-repeat-2-failure.xml`. Não sabemos a causa interna porque a asserção estava dentro de `runCatching`; essa lacuna permanece aberta. Sessão e servidor são sintéticos; não prova backend ou dispositivo real. Os wrappers verificaram NVIDIA e encerraram AVDs; ADB ficou sem dispositivos.

`MainActivitySessionNavigationTest` tem 12 jornadas PHONE/TABLET e duas jornadas Live TV exclusivas de Android TV. O wrapper confirma o perfil do AVD e, quando executa `:app:connectedDebugAndroidTest` em PHONE/TABLET, exclui automaticamente a anotação `org.mulletaflix.android.AndroidTvOnly`; isso evita que `AssumptionViolatedException` da runner seja reportada como falha. O helper de teste navega pelo menu compacto no telefone e pelos botões diretos no Home amplo do tablet.

PHONE e TABLET: passe `expectedDeviceProfile` e classe; não é necessário filtro manual:

```powershell
.\tools\with-emulator.ps1 -AvdName MulletaflixCodexPhoneApi35_20261012 -Port 5558 -Command .\gradlew.bat -CommandArgument @(':app:connectedDebugAndroidTest', '-Pandroid.testInstrumentationRunnerArguments.expectedDeviceProfile=PHONE', '-Pandroid.testInstrumentationRunnerArguments.class=org.mulletaflix.android.MainActivitySessionNavigationTest', '--no-daemon', '--console=plain')
.\tools\with-emulator.ps1 -AvdName MulletaflixTabletApi35 -Port 5554 -Command .\gradlew.bat -CommandArgument @(':app:connectedDebugAndroidTest', '-Pandroid.testInstrumentationRunnerArguments.expectedDeviceProfile=TABLET', '-Pandroid.testInstrumentationRunnerArguments.class=org.mulletaflix.android.MainActivitySessionNavigationTest', '--no-daemon', '--console=plain')
```

TV: selecione apenas jornadas com `AndroidTvOnly`; a classe toda contém navegação touch PHONE/TABLET e não deve ser executada sem filtro num AVD TV:

```powershell
.\tools\with-emulator.ps1 -AvdName MulletaflixTvApi34 -Port 5556 -Command .\gradlew.bat -CommandArgument @(':app:connectedDebugAndroidTest', '-Pandroid.testInstrumentationRunnerArguments.expectedDeviceProfile=TV', '-Pandroid.testInstrumentationRunnerArguments.class=org.mulletaflix.android.MainActivitySessionNavigationTest', '-Pandroid.testInstrumentationRunnerArguments.annotation=org.mulletaflix.android.AndroidTvOnly', '--no-daemon', '--console=plain')
```

Resultados atuais: PHONE 12/12, TABLET 12/12, TV 2/2. XMLs por perfil em `validation-evidence/2026-10-15/`. A cobertura usa MockWebServer/AVDs e não equivale a servidor real, mídia decodificada, hardware físico nem aceite formal.

Reteste completo da classe após a alteração da asserção de identidade (15/10/2026): PHONE/API 35 12/12 em 81,406 s e TABLET/API 35 12/12 em 71,331 s, 0 falhas/erros/ignorados por perfil. XMLs `MainActivitySessionNavigationTest-{PHONE,TABLET}-API35-repeat-3.xml`. Ambos incluem troca de servidor, login, Quick Connect, troca rápida de usuário, deep links, restauração e logout. Sessões e endpoints são sintéticos; não validar servidor real nem homologação formal com estes resultados.

### Estabilidade após troca de servidor pelo Perfil — 2026-10-15

`MainActivitySessionNavigationTest#switchingServerFromProfileDoesNotRestorePreviousProfileOnBackAfterLogin` passou novamente 1/1 em PHONE/API 35 (17,618 s) e TABLET/API 35 (12,394 s), repeats 4, após a alteração diagnóstica da asserção. XMLs independentes em `validation-evidence/2026-10-15/server-switch-stability-{PHONE,TABLET}-API35-repeat-4.xml`. Junto dos repeats 3, há duas aprovações por perfil depois da mudança; o repeat 2 TABLET continua preservado como falha. Wrapper confirmou NVIDIA e encerrou os AVDs. A causa original segue indeterminada; fixtures usam MockWebServer e não comprovam servidor/dispositivo reais nem homologação APK-H02.

### Troca de conta — identidade visível em PHONE e TABLET

`MainActivitySessionNavigationTest#profileQuickSwitchPersistsSelectedAccountAndKeepsServerAndDevice` também valida a UI após a troca rápida: a sessão persistida contém a conta nova, seu nome fica no cabeçalho do Perfil e a conta anterior volta à lista de alternância. PHONE/API 35 e TABLET/API 35 passaram 1/1 cada; XMLs independentes em `validation-evidence/2026-10-15/profile-quick-switch-visible-identity-{PHONE,TABLET}-API35.xml`. O teste usa MockWebServer e comprova apenas troca de conta no mesmo servidor; autenticação real, troca entre servidores e homologação formal permanecem pendentes.

### APK-H02: troca de servidor e seleção por perfil — 2026-10-15

`MainActivitySessionNavigationTest#switchingServerFromProfileDoesNotRestorePreviousProfileOnBackAfterLogin` agora confirma também a identidade após autenticação no servidor novo. O fixture responde `GET /Users/Me`; o teste exige essa chamada, vê `Perfil de New user` na Home (menu compacto PHONE, descrição acessível TABLET), abre Perfil e vê `New user`, volta e verifica que a sessão anterior não retorna. PHONE/API 35 e TABLET/API 35 passaram 1/1 cada. XMLs independentes: `validation-evidence/2026-10-15/server-switch-profile-identity-{PHONE,TABLET}-API35.xml`.

Reteste focal repetido em 2026-10-15: PHONE/API 35 passou 1/1 (14,195 s) e TABLET/API 35 passou 1/1 (11,408 s), cada um em AVD separado pelo wrapper. XMLs: `validation-evidence/2026-10-15/server-switch-stability-{PHONE,TABLET}-API35-repeat-1.xml`. São fixtures sintéticas/MockWebServer; a repetição aumenta evidência local, mas não prova backend real, hardware físico ou aceite formal. APK-H02 permanece em validação. A execução da classe inteira (14 casos) teve 12 cenários PHONE aprovados e dois cenários Android TV registrados como `AssumptionViolatedException`/falhas no PHONE; não deve ser considerada verde. Execuções isoladas por perfil e a anotação `AndroidTvOnly` são necessárias; ver comandos no início deste arquivo. Sem servidor real/hardware.

Quality gate desta rodada: `testDebugUnitTest :app:lintDebug :app:assembleDebug :app:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL`; 1.776 testes JVM/256 XML, 0 falhas/erros/ignorados. Assemble Debug e demais tarefas sem mudanças estavam `UP-TO-DATE`; nenhum artefato de produção ou release foi criado.

### Revalidação integral JVM e gate Android local — 2026-10-15

Execução forçada da suíte completa com `gradlew.bat testDebugUnitTest
--rerun-tasks --no-daemon --console=plain`: 1.774 testes em 256 relatórios
XML, 0 falhas, 0 erros e 0 ignorados; 470 tarefas executadas. A execução
anterior sem `--rerun-tasks` terminou com 470 tarefas `UP-TO-DATE` e não foi
contada como nova execução. Gate posterior:
`:feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug
:feature:item-detail:compileDebugAndroidTestKotlin
:app:compileDebugAndroidTestKotlin --no-daemon --console=plain` —
`BUILD SUCCESSFUL`, 727 tarefas, 45 executadas e 682 `UP-TO-DATE`.
`:app:assembleDebug` ficou `UP-TO-DATE`, sem novo empacotamento. Nenhum
AndroidTest foi executado nesse gate; package Debug não é artefato de produção.
Esta revalidação confirma a árvore local atual, mas não valida servidor,
dispositivos físicos, mídia autorizada, acessibilidade manual nem aceite
formal. O resumo e evidências por área continuam em `HOMOLOGACAO-APK.md`.

### Busca OCR em quadrinhos: cancelar durante inferência e repetir — 2026-10-14

`BookTextSearchDialogTest.cancelingComicOcrSearchReturnsToIdleAndIgnoresLateRecognitionBeforeRetry` compõe o diálogo real com `BookPdfTextSearch` e `ComicBookOcr`, usando página/Task controladas. Verifica retorno imediato ao estado ocioso após cancelar, bitmap mantido vivo até a Task ML Kit completar, serialização da consulta seguinte pelo semáforo OCR, descarte da bitmap antiga e ausência de resultado obsoleto antes/depois da nova busca. Passou 1/1 em PHONE/API 35 e 1/1 em TABLET/API 35 em 14/10/2026; XMLs separados em `validation-evidence/2026-10-14/comic-ocr-search-cancel-{phone,tablet}-api35.xml`. AndroidTest Kotlin compilou. São Tasks e conteúdo sintéticos: não medem OCR real, TalkBack, arquivo extenso, TV ou hardware físico. Cancelamento não interrompe inferência nativa já iniciada.

### Progresso acessível da busca paginada — 2026-10-15

`BookTextSearchDialogTest.paginatedSearchShowsAccessibleDeterminateProgress` verifica texto visível “Analisadas 3 de 10 páginas”, valor `ProgressBarRangeInfo(3, 0..10)` e `LiveRegionMode.Polite`. PHONE/API 35 passou 1/1 em 15/10/2026; XML `validation-evidence/2026-10-15/book-text-search-progress-phone-api35.xml`. É fixture de progresso controlado: valida a árvore semântica e atualização visual, não fala audível do TalkBack nem OCR de arquivo real. Validação manual de TalkBack e medições em conteúdo extenso continuam pendentes.

## Processo obrigatório: Test-Driven Development (TDD)

O plano de adoção, auditoria de lacunas e ordem de fechamento por prioridade estão em [`TDD-PLAN.md`](TDD-PLAN.md). Este documento mantém os comandos e cenários de teste detalhados.

Aplicar Red-Green-Refactor a toda correção, funcionalidade e mudança de comportamento em qualquer módulo Android. Esta regra cobre `:app`, `:core:api`, `:core:common`, `:data`, `:design-system`, `:domain` e todos os módulos `:feature:*`. `:core:testing` contém suporte a testes; valide-o por meio dos módulos consumidores.

1. **RED:** escreva um teste pequeno para o comportamento esperado. Execute-o antes de alterar código de produção. Confirme que falha pela regressão esperada. Erro de compilação, fixture inválida ou falha de setup não contam como RED.
2. **GREEN:** implemente a menor mudança possível. Reexecute o mesmo teste e confirme aprovação.
3. **REFACTOR:** melhore o desenho somente com o teste verde. Reexecute os testes afetados.
4. **GAUNTLET:** antes de concluir, execute a suíte JVM completa, lint e build Debug; rode testes instrumentados em cada perfil envolvido. Revise os relatórios para confirmar que testes executaram, não foram todos ignorados.
5. **EVIDÊNCIA:** registre comandos, RED esperado, GREEN, contagens de testes/falhas/erros/ignorados e limitações. Não alegue cobertura percentual sem relatório de cobertura.

Não escreva primeiro código de produção para depois “cobri-lo” com teste. Mudanças somente em documentação ou recursos sem comportamento executável dispensam RED; valide links, sintaxe e diff. Nunca enfraqueça teste para fazer a suíte passar.

### Seleção da camada de teste

| Comportamento | Teste primário | Complemento quando necessário |
| --- | --- | --- |
| Navegação por links em HTML/KF8 plain-text | JVM valida resolução de `id`/`a[name]`, offset e filtros de segurança | Compose instrumentado valida clique, seleção do chunk e rolagem até o alvo no mesmo/diferente chunk; PHONE/TABLET/TV para toque, TalkBack e D-pad |
| Regra pura, política, parser, ordenação ou estado | JVM no módulo dono | Property/boundary tests para entrada ampla |
| Coroutines, Flow, ViewModel, sessão ou persistência | JVM com dispatchers/fakes determinísticos | DataStore/Room instrumentado para integração Android real |
| HTTP, autenticação, retry ou contrato do cliente | JVM com servidor HTTP local e fixtures | Ambiente de servidor de teste autorizado para E2E |
| Compose, acessibilidade, navegação, rolagem ou foco | Compose UI test | PHONE/TABLET/TV conforme o comportamento |
| Activity, ciclo de vida, PiP, TTS, player, codecs ou APIs de plataforma | Instrumentação | Dispositivo físico quando hardware, engine ou saída acústica forem relevantes |
| Empacotamento e assinatura | Teste dos scripts/configuração e validação do artefato | Regras globais de release/publicação seguem instruções de nível superior; esta matriz define apenas a validação Android |

### Livros AZW3/KF8 — parser e carga

- `MobiBookTextExtractorTest`: AZW3 real standalone e híbrido KF8, seleção do
  fluxo KF8, fallback MOBI 7 para estrutura híbrida incompleta e rejeição de
  estruturas malformadas.
- `Kf8SegmentedByteBufferTest`: inserção por offsets, ordem para offsets iguais
  e limites no começo/fim do conteúdo.
- `PlainTextBookDocumentTest`: conversão de tags HTML para texto, entidades,
  parágrafos e marcadores de título.
- `BookReaderViewModelTest.real AZW3 response loads readable KF8 text through
  the view model`: resposta HTTP mockada com fixture real, texto no estado final
  e ausência de erro/publicação paginada.
- `PlainTextBookReaderIntegrationTest.kf8EmbeddedImageIsResolvedAndRenderedInlineInSingleChunk`:
  resolve uma referência `kindle:embed` para um PNG sintético, abre o HTML como
  `PlainTextBookDocument` e verifica texto adjacente, descrição acessível e pixel
  central após Coil carregar. Passou 1/1 em PHONE/API 35 via wrapper em
  14/10/2026. Isola a camada de resolução/renderização; não percorre o mesmo
  arquivo MOBI híbrido pelo extrator até Compose, que continua pendente.
- `PlainTextBookReaderIntegrationTest.hybridMobiImageFromExtractorRendersInFocusedComposeExcerpt`:
  carrega a fixture AZW3 calibre autorizada, constrói em memória um MOBI
  híbrido com PNG sintético na faixa legada, abre o híbrido pelo extrator real,
  encontra os bytes exatos da imagem injetada e confirma pixels magenta, texto e
  descrição acessível no Compose. PHONE/API 35 e TABLET/API 35 passaram 1/1
  cada em 14/10/2026. Para evitar que a virtualização do livro grande oculte o
  nó de imagem fora da viewport, o teste cria um HTML mínimo com os bytes
  efetivamente extraídos; portanto ainda não prova que a imagem na posição
  original do documento completo aparece após rolagem. XMLs preservados em
  `validation-evidence/2026-10-14/`. Fixture incluída apenas no APK AndroidTest,
  nunca no APK de produção.
- `PlainTextBookReaderIntegrationTest.hybridMobiImageRendersAtOriginalPositionInFullDocumentAfterScroll`:
  conserva o MOBI híbrido completo aberto pelo extrator, seleciona a imagem
  sintética pelos bytes extraídos e usa `performScrollToNode` para revelar o nó
  inline no chunk/offset original da `LazyColumn`; confirma descrição e pixel
  magenta após rolagem. PHONE/API 35 passou 1/1 (91,837 s) e TABLET/API 35
  passou 1/1 (91,720 s) em 14/10/2026, via wrapper com GPU NVIDIA e AVD
  encerrado ao fim. Evidência
  arquivada em `validation-evidence/2026-10-14/hybrid-mobi-full-document-phone-api35.xml`
  e `hybrid-mobi-full-document-tablet-api35.xml`. A fixture continua sintética
  e restrita ao APK AndroidTest; não equivale a livro comercial, hardware real,
  TalkBack ou homologação formal.
- Gate observado em 2026-10-12: 1.701 testes JVM em 250 XML, sem falhas/erros/
  ignorados; ambos os lint, assemble Debug e compilação AndroidTest concluíram.
  Isso não substitui Compose/instrumentação em PHONE/TABLET/TV.

### Sumário Markdown — navegação Compose

`PlainTextBookReaderIntegrationTest.markdownTableOfContentsNavigatesToSecondChunkAndResolvesItsLocator`
valida a abertura do sumário, seleção de um cabeçalho Markdown em outro chunk,
renderização do título/corpo e resolução do locator para o chunk escolhido (não
persistência do progresso entre sessões). Fixture sintética; rode em
TABLET/API 35 pelo wrapper:

```powershell
.\tools\with-emulator.ps1 -AvdName MulletaflixTtsVoiceTabletApi35 -Port 5582 -Command .\gradlew.bat -CommandArgument @(':feature:item-detail:connectedDebugAndroidTest', '-Pandroid.testInstrumentationRunnerArguments.expectedDeviceProfile=TABLET', '-Pandroid.testInstrumentationRunnerArguments.class=org.mulletaflix.feature.itemdetail.PlainTextBookReaderIntegrationTest#markdownTableOfContentsNavigatesToSecondChunkAndResolvesItsLocator', '--no-daemon', '--console=plain')
```

Esse teste cobre interação Compose em AVD, não validação manual de TalkBack,
arquivo real ou aparelho físico.

### Quality Gate completo

Execute na raiz `MulletaFlix-android`. Rode o lint do módulo alterado e do app:

```powershell
.\gradlew.bat testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug --no-daemon --console=plain
```

Troque `:feature:item-detail:lintDebug` pelo módulo afetado. O Quality Gate completo executado em 2026-10-09 comprovou 1.556 testes JVM nos relatórios XML, zero falhas, erros ou ignorados. Esse número é um baseline de execução, não uma métrica de cobertura nem garantia de que todos os comportamentos existentes tenham sido auditados em RED-GREEN.

Para instrumentação, use `tools\with-emulator.ps1`, informe `expectedDeviceProfile` e selecione os testes relevantes. Antes de chamar o Gradle, o wrapper exige que o AVD solicitado seja o único dispositivo listado pelo ADB; se outro emulador ou aparelho estiver conectado (inclusive offline), a execução é recusada sem desconectar nem desligar os demais. Isso evita que `connectedDebugAndroidTest` aplique o APK de teste em um dispositivo não selecionado. A suíte não depende de instalar uma release no aparelho. Perfis e comandos ficam na seção “Perfis de emulador para UX adaptativa”.

### Rascunho do formulário de solicitação de mídia

O rascunho fica no DataStore privado do app e é indexado por hash da identidade servidor/conta. O repositório persiste somente título, tipo, ano e observações; nunca credenciais, URL, identificador bruto, estado de envio ou resultado. A Home agrupa gravações durante a digitação e faz flush ao sair/ir para segundo plano. Não adicione retries automáticos do POST.

- JVM: `HomeViewModelTest` cobre restauração, troca de escopo, flush do rascunho pendente e ausência de reenvio automático; `FeedbackRequestSessionTest` cobre estabilidade/isolamento do hash.
- Falha local transitória: `HomeViewModelTest.failed draft persistence remains pending for a later local retry` simula um primeiro `MediaRequestDraftStore.save` que falha, chama o flush e confirma que a segunda gravação persiste os mesmos campos. A nova tentativa é apenas local; o POST não é repetido.
- Feedback e retry na Home: `HomeViewModelTest.draft save failure is observable and local retry recovers without request submission` comprova a transição FAILED → SAVED após uma falha transitória e zero solicitações remotas. `HomeMediaRequestDraftRestorationTest.failedDraftSaveShowsLocalRetryAndDoesNotSubmitRequest` valida mensagem, semântica `LiveRegionMode.Polite`, ação de retry e nenhuma chamada POST; passou junto com os 3 casos existentes (4/4) em PHONE/API 35, TABLET/API 35 e TV/API 34. A falha local fica explicada até recuperação; o texto permanece na tela enquanto aberta, mas armazenamento cheio permanente e hardware físico não foram validados.
- Troca de servidor após falha: `HomeViewModelTest.failed draft remains isolated and restores after switching servers` comprova que o rascunho pendente de A não aparece no formulário de B, volta a ser exibido ao retornar a A e pode então ser persistido.
- DataStore em PHONE/API 35: `MediaRequestDraftStorePersistenceTest` recria o DataStore usando o mesmo arquivo temporário e confere isolamento, limpeza seletiva e limite de oito escopos.
- UI: `HomeMediaRequestDraftRestorationTest` valida os quatro campos carregados no formulário, restauração do estado Compose e seleção de sugestão sem ano. Rode em PHONE/API 35, TABLET/API 35 e TV/API 34 usando `tools\with-emulator.ps1`.
- Processo principal: execute `tools\with-emulator.ps1` com AVD PHONE descartável e passe `-Scenario MediaRequestDraft` ao script `tools\Test-AndroidBookMainProcessColdStart.ps1`. O host prepara sessão/rascunho sintéticos, inicia MainActivity, confirma PID, executa `am force-stop`, relança Activity em nova fase e valida os quatro campos. A fase de preparação grava fixture diretamente no DataStore de produção; entrada digitada é coberta separadamente pelo teste Compose. O `finally` restaura a sessão capturada e apaga apenas o escopo UUID do rascunho.
- Em 2026-10-13, em `MulletaflixColdStartTempApi35_20261011`/API 35, PID 3843 foi encerrado e PID 3921 apresentou o rascunho; restauração de sessão e limpeza seletiva passaram. O wrapper confirmou NVIDIA RTX 3050 e fechou o AVD; ADB ficou sem dispositivos.
- Isso comprova cold start do processo em AVD, não uso real contínuo, recuperação após reboot, hardware físico nem homologação formal. Não marque aceite formal somente com esses resultados.
- Para a jornada do leitor, `tools\Test-AndroidBookMainProcessColdStart.ps1` exige as rotas de metadata do item e `/BookReader/Epub`; `/BookReader/Status` é best-effort e não pode ser condição de aprovação, pois a Activity cancela o polling quando o corpo chega primeiro. A regressão do contrato é coberta por `Invoke-Pester -Path .\tools\Test-AndroidBookMainProcessColdStart.Tests.ps1`. A execução cold-start precisa de AVD PHONE descartável já aberto na porta especificada, e deve ser envolvida por `tools\with-emulator.ps1` para abrir/fechar o AVD e validar NVIDIA.

### Integração Activity: logout em Ajustes

O `MainActivitySessionNavigationTest.settingsLogoutClearsPersistedSessionBeforeShowingServerSelection` percorre a navegação real do telefone, executa logout com DataStore real e confirma que o token/usuário foram removidos antes de mostrar a seleção de servidor; a URL permanece para reconexão.

`MainActivitySessionNavigationTest.switchingServerFromProfileDoesNotRestorePreviousProfileOnBackAfterLogin` cobre Home → Perfil → seleção de outro servidor → login → Home → Voltar. A troca iniciada no Perfil deve limpar a pilha da Home anterior; Voltar da nova Home encerra a Activity sem restaurar o Perfil/servidor anterior. Em 2026-10-14, a classe completa passou 11/11 em PHONE/API 35 com MockWebServer. Em 2026-10-15, após mudança preexistente da classe para 12 cenários, passou 12/12 em PHONE/API 35; a repetição seguinte não concluiu boot do AVD e uma terceira tentativa iniciou o runner mas ficou sem progresso por mais de 5 min, sem relatório final, sendo interrompida e limpa pelo wrapper. O risco histórico de intermitência permanece sem causa confirmada; evidência aprovada: `validation-evidence/2026-10-15/MainActivitySessionNavigationTest-PHONE-API35-repeat-1.xml`. Isso não valida servidor ou dispositivo real.

Revalidação focal em 2026-10-15: uma repetição falhou no `assertIsDisplayed()` do nome do perfil embora `New user` estivesse na árvore semântica. O teste agora chama `performScrollTo()` antes de confirmar a identidade visível, pois o conteúdo vive dentro de uma coluna rolável. Depois da alteração, passou 1/1 no PHONE/API 35 e 1/1 no TABLET/API 35; evidências `validation-evidence/2026-10-15/server-switch-scroll-visibility-{PHONE,TABLET}-API35.xml`. Isto valida que o nome é acessível/visível após rolagem, não que a sessão seja aceita por backend real. A falha histórica de autenticação POST/401 é distinta e permanece pendente.

`MainActivitySessionNavigationTest.canceledProfileServerSwitchDoesNotAffectLoginAfterLogout` cobre cancelar a seleção pelo Perfil, voltar ao Perfil, encerrar sessão e autenticar depois por um fluxo independente. Em 2026-10-14 passou 1/1; o cenário não reproduziu reutilização prejudicial do marcador da origem anterior. A classe completa passou 11/11 no PHONE/API 35 com fixtures sintéticas.

`MainActivitySessionNavigationTest.profileQuickSwitchPersistsSelectedAccountAndKeepsServerAndDevice` percorre Home → Perfil → alternância rápida → confirmação e usa `SessionRepositoryImpl`/DataStore reais. O teste confirma que URL, `serverId` do mesmo servidor e `deviceId` são preservados, enquanto `userId`, nome e token refletem a conta selecionada. Em 2026-10-14 passou 1/1 em PHONE/API 35 com MockWebServer; isso não valida autenticação num servidor real nem troca de servidor.

O teste `crossServerDeepLinkAuthenticatesOnTargetServerAndLoadsItemWithoutRequestingOldServer` cobre um link recebido de outro servidor. Configure explicitamente `SessionRepository.setBaseUrl(oldServerUrl)` no fixture antes de salvar a sessão: `saveSession` não é substituto dessa seleção de endpoint. Em falha pós-login, o teste reporta requisições dos dois servidores, endpoint/sessão persistidos e texto visível. Duas execuções anteriores da classe completa foram intermitentes; veja o resultado focal e a pendência de estabilidade na seção abaixo.

```powershell
.\tools\with-emulator.ps1 -AvdName MulletaflixApi35 -Port 5572 -Command .\gradlew.bat -CommandArgument @(':app:connectedDebugAndroidTest', '-Pandroid.testInstrumentationRunnerArguments.expectedDeviceProfile=PHONE', '-Pandroid.testInstrumentationRunnerArguments.class=org.mulletaflix.android.MainActivitySessionNavigationTest#settingsLogoutClearsPersistedSessionBeforeShowingServerSelection', '--no-daemon', '--console=plain')
```

### Deep link cross-server e recuperação LAN

`MainActivitySessionNavigationTest.crossServerDeepLinkAuthenticatesOnTargetServerAndLoadsItemWithoutRequestingOldServer` cobre sessão A → seleção manual de B → login → mídia do link. Após autenticação, Voltar retorna à Home autenticada em B. O caso deve rodar com `LanServerRecovery` ativo; não pare o worker no fixture para contornar alternância de endpoint. `DeepLinkNavigationPolicyTest` valida a sequência pura de destinos.

O helper `launchMainActivity` da classe adiciona `EXTRA_SUPPRESS_APP_UPDATE_CHECK_FOR_TEST`: ele suprime a consulta ao GitHub somente em builds debug e apenas nas Activities iniciadas pela fixture. Política JVM garante que build não-debug continue verificando atualizações mesmo se receber esse extra. Os testes próprios de `AppUpdateCheckEffect` continuam cobrindo lifecycle e periodicidade.

Revalidação da estabilidade em 2026-10-15: uma execução completa PHONE/API 35 passou 12/12 (0 falhas, erros ou ignorados), com relatório em `validation-evidence/2026-10-15/MainActivitySessionNavigationTest-PHONE-API35-repeat-1.xml`. A tentativa seguinte expirou no boot do AVD após 4 min, antes do Gradle; uma nova tentativa com timeout de boot ampliado iniciou instrumentação, mas ficou sem progresso por mais de 5 min e sem XML final, então foi interrompida. O wrapper fechou o AVD e ADB terminou sem dispositivos. Não interpretar a execução aprovada isolada como estabilidade demonstrada; a falha histórica permanece sem causa confirmada.

A política de recuperação só pode voltar de um endereço LAN a um endereço remoto persistido que tenha o mesmo `serverId` da sessão autenticada. URL padrão global não identifica, por si só, o mesmo servidor. `LanServerRecoveryPolicyTest` valida identidade, ausência de identidade, endereço local e limiar de duas falhas; passou 26/26 em 2026-10-13. O teardown de `MainActivitySessionNavigationTest` também audita isolamento: toda URL de MockWebServer deve entrar na lista de remoção; um fallback remove endpoints da própria classe e a asserção falha se encontrar URL não registrada. Em 2026-10-13, essa asserção deu RED ao detectar `http://localhost:36447`; após centralizar o registro em `trackMockServerUrl`, o AndroidTest focal passou 1/1 e a classe completa 9/9 em PHONE/API 35, com o worker LAN ativo. NVIDIA foi confirmada e o wrapper deixou ADB sem dispositivos. Uma execução histórica falhou 8/9 e sua causa permanece não confirmada; a falha de fixture observada agora não prova ser a origem. O XML da classe é `app/build/outputs/androidTest-results/connected/debug/TEST-MulletaflixAptoidePhone(AVD) - 15.xml`; cada execução o substitui. MockWebServer/AVD não substituem validação em LAN física, e não alteram homologação formal.

### Adoção nos módulos existentes

O projeto já possui testes JVM e instrumentados em todos os módulos de produto. Isso não prova que todos foram escritos em TDD. A evidência RED-GREEN de testes legados não é presumida. Ao alterar uma área legada, acrescente primeiro a regressão e migre o comportamento tocado para este ciclo. Novos comportamentos devem nascer com teste; auditorias retroativas de cobertura e de histórico de RED permanecem pendentes. O baseline de testes não equivale a percentual de homologação.

## Comandos

```powershell
.\gradlew.bat test
.\gradlew.bat assembleDebug
.\gradlew.bat :app:connectedDebugAndroidTest
```

## Cobertura automatizada atual

| Área | Cenários | Tipo |
| --- | --- | --- |
| UseCases de Domínio (`:domain`) | Obtenção de perfil, logout, detalhes de mídia, toggle favorito, toggle assistido, descoberta do próximo episódio (`GetNextEpisodeUseCase`) | JVM |
| Perfil de Usuário (`:feature:user`) | Carregamento de perfil, fallback de sessão offline, alternância rápida de usuário, limpeza de cache e logout; a tela de Perfil confirma cancelar/confirmar logout e troca de usuário (PHONE/API 35, 4/4); falha de logout não navega | JVM (`UserProfileViewModelTest`) + Compose (`ProfileSessionConfirmationTest`, `ProfileLogoutFailureTest`) |
| Fallback de erros (`:core:common`, auth, Home, item-detail, TV ao vivo, SyncPlay, perfil) | Mensagem localizada ausente/em branco usa texto recuperável; mensagens úteis da exceção ou resposta do servidor são preservadas; cancelar continua sem erro visível; relato de reprodução mostra aviso e opção de fila | JVM + Compose instrumentado PHONE API 35 |
| Cache Home e Preferences DataStore (`:data`) | URL fallback normaliza HTTP 80/HTTPS 443 sem colapsar portas não padrão; persistência ao recriar DataStore/repositório, JSON inválido removido e recuperável, sessão inativa não sobrescreve sessão ativa e limpeza obsoleta preserva gravação nova | JVM (`HomeFeedCacheScopeTest`, 5/5) + AndroidTest PHONE/API 35 (`HomeFeedCacheRepositoryPersistenceTest`, 4/4; DataStore Preferences real e arquivo temporário). Não testa kill/restart do processo Android nem falha física de I/O. |
| Detalhes do Item (`:feature:item-detail`) | Filmes, séries, temporadas/episódios, faixas de álbuns, favoritos, assistidos, playlists e erros; uma coluna em 411 dp e tablet retrato 600 dp, painel hero+detalhes rolável em 840 dp, TV preserva coluna única; hero compacta poster para 88 dp abaixo de 360 dp ou em fonte ≥1,5×, mantém capa 2:3 inteira e permite alcançar/acionar última ação após rolagem vertical e horizontal; leitores EPUB/CBZ retêm progresso isolado por servidor/conta/livro; CBZ/CBR usa duas páginas apenas em tablets com menor largura ≥600 dp e largura atual ≥840 dp; celular, TV, PDF e janelas compactas ficam em página única; controles avançam em pares sem cortar última página | JVM + Compose instrumentado em telefone, tablet API 35 e TV API 34 |
| Tela do leitor EPUB (`:feature:item-detail`) | Compose + ViewModel + Retrofit + EPUB válido via fixture HTTP local + Readium; erro 503, retry, TOC aninhado, `<span>` de grupo sem ação e com semântica heading, salto para capítulo e texto confirmado no DOM do WebView, controles de navegação/tamanho de fonte e ação acessível para ouvir livro; recuperação de cache órfão EPUB/CBZ e proteção de arquivo ativo/não pertencente ao leitor | JVM + instrumentado PHONE/TABLET API 35; `BookReaderScreenIntegrationTest` 8/8 em cada perfil, 2026-10-08 |
| Leitor RTF (`:feature:item-detail`) | Conteúdo RTF truncado por `\\binN` apresenta erro recuperável sem exibir texto parcial; retry com RTF válido remove o erro e renderiza texto; ambas as requisições usam a rota do leitor | `BookReaderScreenIntegrationTest.truncatedRtfShowsErrorWithoutPartialTextAndRetryLoadsValidRtf` passou 1/1 em PHONE/API 35 (`SmartMeasureApi35Play`) em 2026-10-12; fixture sintética via MockWebServer; TalkBack/D-pad não verificados |
| Livros MOBI/AZW3/KF8 (`:feature:item-detail`) | MOBI sem DRM com PalmDOC/HUFF-CDIC, AZW3/KF8 por INDX/TAGX/FDST, preferência KF8 em MOBI híbrido e fallback MOBI 7; resposta AZW3 via HTTP mockado e ViewModel; reconstrução em segmentos e conversão HTML linear; headings/entidades preservados; Compose valida sumário MOBI, salto para capítulo, persistência do locator e leitura do locator por novo ViewModel | JVM/Robolectric com fixture AZW3 calibre; `BookReaderScreenIntegrationTest.mobiContentsNavigateToChapterAndRestoreTheSavedLocator` passou 1/1 em TABLET/API 35 e 1/1 em PHONE/API 35 em 2026-10-13. Fixture sintética; aparelho físico, arquivo real e TalkBack ainda pendentes |
| Busca em livros (`:feature:item-detail`) | Busca ignora caixa/acentos, limita a 100 resultados e percorre página a página; EPUB usa Readium, PDF prioriza camada nativa e usa OCR Latin offline, CBZ/CBR usa OCR Latin sob demanda; progresso acessível informa páginas concluídas, com até 100 checkpoints periódicos e início/fim quando não coincidem; ação explícita cancela job/UI, limpa progresso e permite nova busca; OCR de quadrinhos mantém cache LRU de 3 páginas e bitmap até 2048 px por lado | JVM valida progresso monotônico, limite de callbacks e cancelamento entre páginas; `ComicBookOcrTest` e `PdfBookDocumentOcrTest` provam que cancelar a espera retorna sem aguardar ML Kit e que bitmap/recognizer só são liberados quando a Task termina; Compose AndroidTest da busca em TABLET/API 35, 6/6, incluindo progresso acessível e cancelamento/reinício; OCR/busca por página passou com CBZ e CBR sintéticos; `BookReaderScreenIntegrationTest.cbrSearchResultOpensMatchingPage` passou 1/1 em TABLET/API 35 e PHONE/API 35, confirmando query, trecho “Página 2” e abertura da página 2; revalidação PHONE/API 35 em 2026-10-12: `ComicBookOcrIntegrationTest` passou 2/2 (CBZ+CBR) e `PdfBookReaderIntegrationTest.scannedPdfPageIsRecognizedOfflineAndIndexedBySearch` passou 1/1; gate JVM realmente reexecutado com 1.710 testes em 252 relatórios, 0 falhas/erros/ignorados, lint e assemble Debug concluídos. A inferência nativa iniciada continua até a Task completar, mantendo recursos vivos. Arquivos reais autorizados, TalkBack/D-pad, TV e teste CBR em TV pendentes; TV não expõe leitor Livros |
| Progresso e marcadores do leitor (`:feature:item-detail`) | Isolamento por conta/servidor/mídia; limite compartilhado de 8.192 caracteres UTF-16 na gravação/restauração de progresso e marcadores; JSON agregado de marcadores limitado para até 20 locators | JVM (`BookReaderProgressStoreTest`: locators válidos preservados; progresso/marcador excessivos e payload agregado rejeitados) |
| Sumário em DOCX (`:feature:item-detail`) | Extrai `Heading 1`–`Heading 9`, níveis diretos e `styleId` personalizado; herda `outlineLvl` por `basedOn` até estilo local ou heading padrão, ignora estilo de caractere/ciclo, limita styles XML, estilos e capítulos, mapeia heading ao trecho/locator; Compose navega e salva locator | JVM + PHONE/API 35 2/2 e TABLET/API 35 2/2 em `MulletaflixTabletApi35`, 2026-10-13; ambos validam renderização, salto pelo sumário e restauração do locator. Fixtures sintéticas; arquivos DOCX reais/aparelho físico pendentes. TV não aplicável porque livros ficam ocultos por regra de produto; AVD reservada `MulletaflixApi35` não foi usada |
| Aparência da leitura (`:feature:item-detail`) | Seletor de aparência em EPUB e texto paginado; Original preserva estilo editorial, Sépia/Escuro aplicam cores contrastantes; preferência persiste por conta/servidor; controles não aparecem em PDF/quadrinhos | JVM + Compose/DataStore/Readium instrumentado em PHONE, TABLET e TV |
| Leitura em voz alta (`:feature:item-detail`) | TTS nos trechos de texto paginado; narração Readium em EPUB inicia na posição visível, acompanha posição/locutor, usa idioma declarado no conteúdo, pausa em segundo plano e encerra em navegação manual; PDF tenta texto nativo e aplica OCR Latin bundled/offline se não houver camada textual; CBZ/CBR usam OCR Latin local por página, sob demanda, com cache curto; avanço acompanha conclusão do TTS, controle de velocidade, seletores de idioma e de voz por idioma/país. Preferência de voz é isolada por servidor/conta; vozes sem rede aparecem antes e vozes que requerem conexão são identificadas; voz removida/incompatível retorna ao padrão | JVM + Compose instrumentado PHONE/TABLET API 35; seleção de voz e persistência passou 1/1 em PHONE e 1/1 em TABLET/API 35 (XML tablet preservado); controller/ViewModel cobrem aplicação antes da fala, troca durante fala, callback antigo e limpeza de preferência inválida. Quality gate 2026-10-13: 1.732 testes JVM/252 relatórios, 0 falhas/erros/ignorados; lint módulo/app, assembleDebug e compilação AndroidTest passaram. CBZ OCR/TTS e avanço por 2 páginas passou 1/1 no TABLET/API 35; CBR OCR/TTS e avanço por 2 páginas passou 1/1 no TABLET/API 35 usando fixture sintética RAR4 com texto conhecido e motor TTS falso; regressão PDF/avanço/parada manual passou 1/1 no TABLET/API 35. Logcat confirmou modelo ML Kit Latin local, mas o AVD manteve conectividade (sem prova de operação em modo avião). Não comprova saída audível, catálogo de engine de terceiros, arquivos reais variados, idiomas não Latin, TalkBack, perfis TV nem uso prolongado de memória/bateria |
| Troca de livro durante narração (`:feature:item-detail`) | Troca durante extração suspensa cancela o trabalho e evita fala obsoleta (`switchingBookCancelsPendingPageNarrationExtraction`); troca durante TTS ativo interrompe o engine antigo e ignora callback tardio sem iniciar trecho seguinte (`switchingBookStopsActivePageNarrationAndIgnoresLateSpeechCallback`, texto >1 trecho) | Ambos AndroidTests PHONE/API 35 passaram 1/1 em AVD temporário em 2026-10-12. Callback tardio testado no engine antigo; wrapper confirmou NVIDIA e encerrou AVD. ON_STOP, TalkBack/D-pad, TV e aparelho físico não cobertos |
| Mapeamento de mídia (`:data`) | Tipo, imagens, 4K, HD, HDR, Dolby Vision, Atmos | JVM |
| Cache de imagens e playback | Limpeza periódica Coil confirma intervalo configurado de 1 h e apaga a entrada temporária; download Media3 concluído do fluxo de produção permanece `COMPLETED` após a limpeza e reproduz offline até `STATE_READY`, sem nova requisição ao servidor. Streaming online não é persistido. | Instrumentado TABLET/API 35: `OfflinePlayerProductionDownloadFlowTest.productionDownloadRepositoryFeedsOfflinePlayerFromCacheWithoutNetwork` 1/1 em 2026-10-12; `WorkManager TestDriver` simula o período vencido, sem aguardar uma hora real. |
| Cache offline & Downloads | Conversão entidade/domínio, progresso de reprodução, fila isolada por conta + servidor + mídia, persistência de série/temporada/episódio no `DownloadRequest.data`, retry seguro sem reassociar entradas legadas e seleção do próximo episódio completo no mesmo servidor | JVM + Media3 instrumentado |
| Pausa individual de download (`:app`, `:feature:downloads`) | A ação por linha altera somente o `stopReason` daquele request; download irmão continua, bytes parciais são preservados, retomada usa Range até completar e a pausa global permanece inalterada | `OfflinePlayerProductionDownloadFlowTest.pausingOnePartialDownloadPreservesItWhileItsSiblingContinues`: 1/1 PHONE/API 35 em 13/10/2026; AVD via wrapper, GPU NVIDIA confirmada e AVD encerrado. Fixture MockWebServer responde 206/Content-Range; não valida rede/servidor físico. |
| Retomada de download e reprodução offline após morte de processo (`:app`, Media3) | Fixture H.264/AAC: process death real, retomada HTTP Range, leitura do cache de produção e auditoria de origem são cobertos historicamente pelo teste Android; extensão tenta renderizar `VideoPlayerScreen`/`PlayerView` no mesmo processo retomado | Evidência anterior (15/10): preparação/verificação/cleanup 1/1, PHONE/API 35, Range 1.029.421 e restante completo; ViewModel `READY` + player cache-only avançou sem upstream. Retestes visuais nesta sessão: preparação/cleanup 1/1 por execução; um timeout de 45 s encontrou `QUEUED` a 91,4%, sem erro/stop reason. Com prazo de 120 s, download chegou a `COMPLETED` e bytes/MIME esperados, mas estado Compose não mostrou título nem erro em 30 s. Jornada visual continua inconclusiva; não promover como homologada. AVD NVIDIA encerrado e ADB vazio; diagnóstico em `TDD-PLAN.md`. |
| Download de temporada (`:feature:item-detail`) | Celular/tablet anunciam progresso como região acessível; botão de preparação desativa durante lote e Cancelar fica acionável. Android TV não mostra ações de download de título/temporada, progresso nem Cancelar, mesmo com lote ativo; foco permanece nas ações visíveis | JVM + Compose instrumentado em TV/tablet |
| Downloads por dispositivo | Política central permite Downloads em celular/tablet; Android TV oculta atalhos na Home e grupo em Ajustes, e redireciona acesso direto à fila para Home | JVM + Compose instrumentado em Home/Ajustes/detalhes TV; `DownloadsRouteAvailabilityTest` valida redirecionamento na TV (2/2) |
| Downloads UI (`:feature:downloads`) | Estado de loading/vazio, busca/filtros, mostrar espaço por título e total conhecido, ordenar por maior/menor uso com tamanhos desconhecidos no fim, anúncio de progresso | JVM + Compose instrumentado |
| Player OSD (`:feature:player`) | Auto-ocultar após reprodução iniciada na TV (inclusive após pausa temporária), manter controles antes de iniciar e no celular pausado, revelar via setas/OK no controle remoto | JVM + Compose instrumentado em TV |
| Busca (`:feature:search`) | Debounce, filtros por tipo, sugestões filtradas, histórico limitado, remoção individual de histórico, erro e retry; Livros, sugestões e resultados do tipo livro ocultos na TV, com navegação D-pad nos filtros restantes; celular/tablet mantêm Livros | JVM + Compose instrumentado em celular e TV |
| Home & Descoberta (`:feature:home`) | Carregamento assíncrono, expiração de sessão, refresh da TV sem bloqueio por perfil lento, recarga após troca automática da URL pública para LAN com proteção contra resposta obsoleta, seções recentes indexadas por ID, retry isolado de Adicionados Recentemente por biblioteca (preserva seções irmãs, mostra progresso e descarta resposta obsoleta após refresh/sessão/servidor), ocultação de faixas/erros de livros na TV, estado vazio correto quando só há livros, sem afetar celular/tablet; barra compacta do celular mantém solicitação, busca, Minha Lista e Mais ações visíveis em viewport de 320 dp; card comum envia ID da biblioteca e TV ao vivo usa callback dedicado ao toque; foco D-pad abre biblioteca com um pressionamento central na TV | JVM + Compose instrumentado; retry touch 1/1 telefone e tablet, foco D-pad/OK na TV 2/2; cards de biblioteca 2/2 telefone, 2/2 tablet, 3/3 TV |
| Feedback por título (`:feature:home`, `:feature:item-detail`, `:core:api`, `:data`) | Compose da solicitação valida título/ano, espera sessão válida antes de habilitar envio, tipo/payload, edição após falha e bloqueio de campos/Cancelar/Voltar durante envio; solicitação e relato ignoram fechamento externo enquanto enviam; ação de relato acessível leva ID do título; testes do repositório validam DTOs, falha e propagação de cancelamento; contrato HTTP verifica POST, rota, JSON e identidade autenticada do dispositivo | JVM + Compose instrumentado + MockWebServer |
| Biblioteca (`:feature:library`) | Filtros por gênero/ano/classificação indicativa, aplicar/cancelar/limpar, validação de ano, persistência, ordenação e paginação; ordenação aleatória falha de forma explícita se a consulta do catálogo completo falhar, preserva itens no refresh e permite retry; Android TV mantém Book/Audiobook excluídos após filtro, ordenação, atualização e retorno da rede | JVM + Compose instrumentado em TV/tablet |
| TV ao vivo / EPG (`:feature:live-tv`) | Canais e guia, agendamento otimista, resolução limitada do ID do timer, retry sem reabrir guia, respostas obsoletas após offline/troca de sessão, pré-validação de sessão/rede ao cancelar timer; `LiveTvPlaybackNavigationTest` cobre callback da tela (2/2); `MainActivitySessionNavigationTest` percorre Home → TV ao vivo → player e comprova que canal e gravação chegam ao `PlaybackInfo` com o ID selecionado (2/2 em TV/API 34); tuner também envia o mesmo ID a `LiveStreams/Open` | JVM + Compose/Activity instrumentado; `LiveTvPlaybackNavigationTest` e `MainActivitySessionNavigationTest` em TV/API 34; relatórios duráveis em `validation-evidence/2026-10-15/live-tv-*-player-journey-tv-api34.xml`. Fixtures MockWebServer; não provam decodificação/stream real, servidor de produção nem TV física |
| Player (`:feature:player`) | Pular capítulos, gestos de volume/brilho, pausar em `ON_STOP`; PiP automático/fallback restrito a celular/tablet; teste de política de ciclo de vida em Activity de teste na TV envia `KEYCODE_HOME` via UiAutomation e valida Activity parada + callback de pausa; integração com WAV/SRT local muda tamanho/cor/fundo pela tela real de Ajustes e confirma DataStore compartilhado, atualização do `PlayerViewModel`, renderização via helpers de produção e reprodução contínua (não instancia `VideoPlayerScreen` completa nem valida servidor remoto); telefone/tablet confirmam PiP do sistema tocando e pausam ao encerrar Activity; ocultação/restauração dos overlays via `StateFlow`, recuperação, seleção de faixas, sidecar de legenda externa (MIME/URL/token/ID exclusivo), Cast oculta sidecars e o controle de envio não é composto no perfil TV (permanece no móvel), auto-play (`NextEpisodePolicy`) e bloqueio de seek sem busca/duração | JVM + Compose/MediaItem/StateFlow instrumentado; integração Ajustes/player PHONE API 35: 1/1 |
| Preferências de mídia por perfil | Isolamento de áudio/legendas por usuário e identidade do servidor; alternância A→B→A; gravação tardia no perfil de origem; migração do ajuste global legado para o primeiro perfil | DataStore instrumentado em TV + tablet |
| UX por dispositivo | A Home instrumenta `MediaSection` real e mede a largura da capa pelo perfil detectado: 130 dp no celular, 149,5 dp no tablet e 117 dp na TV; foco D-pad/centro validado só na TV, retomada por D-pad/centro e alvo touch mínimo | Android instrumentado nos três AVDs |
| Atualização e rede em TV | Verificação periódica apenas enquanto Android TV está em primeiro plano; eliminação de verificações concorrentes e de varreduras LAN obsoletas | JVM + Compose/Lifecycle instrumentado |
| Cast e PiP | Estado do receptor e comandos do mini player; regras de PiP por API, retângulo de origem, entrada automática e ocultação do OSD | JVM + Compose/Media3 instrumentado |
| SyncPlay (`:feature:sync-play`) | Controles remotos; polling periódico não cancela consulta de salas em andamento; atualização manual ainda substitui consulta antiga; respostas de sessão anterior não contaminam usuário atual; falha de refresh preserva snapshot marcado stale e sala ativa, bloqueando entrada até uma resposta nova; feedback visual de stale e botão desabilitado | JVM + Compose instrumentado em telefone e Android TV |
| SyncPlay realtime (`:core:api`, `:feature:sync-play`, `:app`) | Handshake/comandos via `events`; falha HTTP 503 emite `Disconnected` e reconecta; close remoto com/sem status conclui handshake; `stop()` cancela retry. Jornada Compose integra entrada, comando, fechamento remoto, estado “Reconectando…” e retorno a conectado, sem status duplicado | JVM + MockWebServer/peer TCP local; `SyncPlayScreenRealtimeJourneyTest` PHONE/API 35, TABLET/API 35 e TV/API 34, 2/2 por perfil (16/10/2026). Sessão/repositório sintéticos; backend/autenticação reais e homologação formal não cobertos |

### SyncPlay: feedback visual de reconexão — 2026-10-16

- TDD RED: `remoteSocketCloseShowsReconnectStateUntilTheClientConnectsAgain` expirou esperando o texto “Reconectando à sincronização em tempo real…” depois do servidor remoto fechar o socket. A UI continuava mostrando o estado genérico inicial e a conexão duplicava o texto de estado em nós/snackbar.
- GREEN: `SyncPlayUiState.realtimeConnectionStatus` distingue `INACTIVE`, `CONNECTING`, `CONNECTED` e `RECONNECTING`. A tela apresenta um único rótulo de estado; eventos de conexão não geram snackbar duplicado, enquanto eventos de conteúdo continuam usando o feedback existente.
- Jornada completa `SyncPlayScreenRealtimeJourneyTest`: 2/2 em PHONE/API 35, TABLET/API 35 e TV/API 34. MockWebServer/WebSocket e UI/ViewModel/client são reais; sessão e repositório de grupo são sintéticos. Somente o relatório XML de TV (2/2) permaneceu no diretório Gradle; PHONE/TABLET foram confirmados na saída BUILD SUCCESSFUL do wrapper, mas não tiveram XML individual persistido. XML de TV disponível em `app/build/outputs/androidTest-results/connected/debug/TEST-MulletaflixTvApi34(AVD) - 14.xml` (saída de build, não versionada).
- Gate forçado integral: `testDebugUnitTest :feature:sync-play:lintDebug :app:lintDebug :app:assembleDebug :feature:sync-play:compileDebugAndroidTestKotlin :app:compileDebugAndroidTestKotlin --rerun-tasks --no-daemon --console=plain` — BUILD SUCCESSFUL, 1.785 testes JVM em 256 XML, 0 falhas/erros/ignorados; lint, assemble e compilação AndroidTest aprovados (881 tarefas).
- Sem servidor/autenticação/DI de produção, dispositivo físico ou release. Essa evidência técnica local não promove APK-H10 nem a homologação formal.

### SyncPlay: comando durante preparação de mídia — 2026-10-16

- `SyncPlayPlayerRealtimeIntegrationTest.pausePositionReceivedWhileQueueItemLoadsIsAppliedAfterMediaPreparation` exercita fila e `Pause`/`Unpause` pelo cliente WebSocket real (MockWebServer), `PlayerViewModel` real e Media3 com WAV sintético de 120 s. Posição de fila inicia em 1 s; comandos mais recentes terminam em Pause a 7 s.
- RED observado: depois de `STATE_READY`, Media3 estava pausado mas em 0 ms; um `seekTo` anterior à timeline pronta não preservava a posição. Correção conserva posição/intenção pendentes e as aplica ao receber prontidão; a posição/intenção mais recente sobrepõe a fila antiga.
- GREEN e XML: PHONE/API 35 `validation-evidence/2026-10-16/syncplay-player-realtime/phone-api35.xml`; TABLET/API 35 `.../tablet-api35.xml`; TV/API 34 `.../tv-api34.xml`. Cada perfil: 1 teste, 0 falhas/erros/ignorados; AVD descartável, wrapper, NVIDIA confirmada e encerrado após uso.
- Quality gate forçado 2026-10-16: suíte JVM 1.784/1.784 em 256 XML; lint player/app, `assembleDebug`, compilação AndroidTest app/player passaram (`BUILD SUCCESSFUL`, 881 tarefas). Avisos não bloqueantes existentes incluem APIs depreciadas/experimentais em fontes diversas.
- Limites: sessão/sala sintéticas, WebSocket local e arquivo sintético; não comprova servidor/autenticação/DI real, protocolo end-to-end com backend, redes reais, dispositivo físico ou aceite formal. Não usar esta evidência para fechar APK-H10 nem `formalSignoff`.

Revalidação independente em PHONE/API 35: `:feature:player:connectedDebugAndroidTest` executou `SyncPlayPlayerRealtimeIntegrationTest` 1/1 aprovado via `tools/with-emulator.ps1`; XML `feature/player/build/outputs/androidTest-results/connected/debug/TEST-MulletaflixCodexPhoneApi35_20261012(AVD) - 15.xml`. O AVD temporário foi encerrado pelo wrapper; `adb devices` ficou vazio. Quality gate forçado na mesma sessão (`testDebugUnitTest --rerun-tasks :feature:player:lintDebug :feature:sync-play:lintDebug :app:lintDebug :app:assembleDebug :feature:player:compileDebugAndroidTestKotlin :feature:sync-play:compileDebugAndroidTestKotlin :app:compileDebugAndroidTestKotlin --no-daemon --console=plain`) terminou `BUILD SUCCESSFUL` em 23m27s: 891 tarefas, 1.785 testes JVM em 256 XML, zero falhas/erros/ignorados. A repetição local não substitui validação em servidor/hardware reais nem promove APK-H10 ou a homologação formal.

### Revalidação combinada SyncPlay PHONE/TABLET/TV — 2026-10-16

- `:feature:sync-play:testDebugUnitTest :feature:player:testDebugUnitTest --rerun-tasks`: 332 testes JVM, zero falhas, erros ou ignorados.
- `SyncPlayScreenRealtimeJourneyTest`: PHONE/API 35, TABLET/API 35 e TV/API 34 passaram; cada execução confirmou relatório instrumentado recém-gerado com testes executados e zero falhas/erros/ignorados.
- `SyncPlayPlayerRealtimeIntegrationTest`: PHONE/API 35, TABLET/API 35 e TV/API 34 passaram sob os mesmos critérios estritos do wrapper.
- Os diretórios de resultados Gradle por módulo são sobrescritos a cada perfil: permaneceram apenas o XML final da tela (PHONE, 2/2) e do player (TV, 1/1); os relatórios dos outros quatro perfis foram verificados pelo wrapper no término de cada execução, mas não permanecem arquivados individualmente. Não inferir resultados por perfil a partir dos XMLs retidos.
- Quality gate: `:feature:player:lintDebug :feature:sync-play:lintDebug :app:lintDebug :app:assembleDebug :feature:player:compileDebugAndroidTestKotlin :feature:sync-play:compileDebugAndroidTestKotlin :app:compileDebugAndroidTestKotlin` — `BUILD SUCCESSFUL`, 737 tarefas (3 executadas, 734 atualizadas).
- Wrapper confirmou uso da GPU NVIDIA nos AVDs temporários e encerrou os emuladores; verificação posterior: `adb devices` vazio e nenhum processo Emulator/QEMU ativo.
- Limites: sala/sessão, MockWebServer/WebSocket e WAV são sintéticos; sem validação contra backend de produção, aparelho físico, rede real ou aceite formal. Esta repetição não altera o status da homologação nem fecha APK-H10.
| Seleção de servidor | Logo, URL, descoberta LAN por UDP em Wi-Fi/Ethernet, tentativa pelo socket sem vínculo apenas para destinos sem envio bem-sucedido em qualquer interface, preservação do socket simples quando não há rede local exposta; fechamento de sockets após falha de vínculo | JVM + Compose instrumentado |
| Conectividade (`:core:common`) | Network callbacks esperam capabilities em API moderna; API 24 agenda lookup legacy após `onAvailable`; deriva estado tarifado e limpa rede perdida | `ConnectivityNetworkCallbackTest` 3/3 PHONE/API 24 e 3/3 PHONE/API 35; NVIDIA verificada, AVDs fechados pelo wrapper |
| Quick Connect | Prazo monotônico de cinco minutos inclui latência; código continua aguardando resposta sem exibir contagem expirada; ViewModel reserva `isLoading` sincronamente e ignora nova iniciação enquanto há requisição ou PIN pendente, preservando PIN e polling ativos | JVM: `AuthViewModelTest.duplicate quick connect initiation while request is in flight is ignored` cobre início simultâneo e PIN já aguardando; classe completa 42/42. AndroidTest `MainActivitySessionNavigationTest.quickConnectAuthorizationPersistsSessionAndOpensHome` validado em PHONE/API 35 com MockWebServer; confirmação em servidor/dispositivo reais segue pendente |
| Retry do instalador APK (`:app`, `:feature:settings`, `:core:common`) | Após permissão negada, reutiliza o mesmo APK para a mesma versão/URL/hash; SHA-256 revalidado em IO antes da instalação; APK removido/alterado/incompatível força novo download; estado de verificação mantém o diálogo/botão ocupado | JVM (PendingAppUpdateApkTest + ViewModel tests); texto Compose “Verificando APK...” sem instrumentação dedicada nesta alteração |
| Instalador/encaminhamento de atualização Android (`:app`, `:core:common`) | AVD PHONE API 35: consulta de permissão sem crash quando `REQUEST_INSTALL_PACKAGES` não está declarada; sem essa permissão abre a ficha do app na Play Store, com fallback HTTPS se a Play Store estiver indisponível; tela de fontes desconhecidas continua direcionada ao próprio app apenas quando a permissão está declarada; FileProvider real lê APK em `cache/updates`; teste captura intents sem abrir UI externa | `AppUpdateInstallerIntegrationTest` instrumentado (PHONE API 35); instalação direta `ACTION_VIEW` depende de permissão concedida e não é habilitada pelo manifesto de produção da Play |
| Sessão persistida (`:data`) | Sessão gravada e reaberta; logout remove credenciais mas mantém URL/DeviceId; servidores salvos sobrevivem à recriação, atualizam sem duplicar e não removem o oficial; versão inicial do oficial permanece desconhecida até handshake | `SessionRepositoryPersistenceTest` (instrumentado, PHONE API 35) |
| Downloader de atualização (`:core:common`) | Cancelamento interrompe chamada HTTP e remove arquivo parcial; `Content-Length` inconsistente e resposta não-APK (ZIP inválido ou sem manifesto Android não vazio) são recusados; verificação de SHA-256 | JVM com `Call.Factory` controlada e arquivos temporários |
| Build & Packaging | Variante debug; release exige keystore e fingerprint oficial; empacotamento inclusive `-SkipBuild` e publicação rejeitam certificado divergente antes de copiar ou escrever no GitHub | Gradle + Pester + apksigner |

### Cancelamento da busca OCR em PDF — Compose e retry

`BookTextSearchDialogTest.cancelingPdfOcrSearchReturnsToIdleAndIgnoresLateRecognitionBeforeRetry` integra o diálogo Compose, `BookPdfTextSearch` e `PdfBookDocument` com PDF válido de página branca e recognizer controlado. Verifica idle após cancelar, Bitmap vivo enquanto a Task está pendente, serialização da busca nova, descarte do resultado antigo e liberação do Bitmap ao concluir cada Task. Passou 1/1 em PHONE/API 35 e 1/1 em TABLET/API 35 em 2026-10-15; XMLs preservados em `validation-evidence/2026-10-15/pdf-ocr-search-cancel-{phone,tablet}-api35.xml`. O recognizer/conteúdo OCR são sintéticos; o teste valida a integração/vida útil, não reconhecimento ML Kit de mídia real, hardware, TalkBack, latência ou bateria.

## Validação dependente de ambiente

Os fluxos abaixo estão implementados no aplicativo, mas precisam de um servidor Mulletaflix de teste com usuário, bibliotecas e mídia para uma validação end-to-end sem dados falsos:

- autenticação por usuário e Quick Connect;
- home, bibliotecas, detalhes, temporadas e episódios;
- reprodução, retomada e envio de progresso;
- legendas, faixas de áudio, qualidade e Cast;
- Live TV, gravações, downloads e SyncPlay;
- logout, cache, preferências e troca de servidor.

O contrato HTTP local de feedback confirma que o aplicativo envia os campos esperados a `UserFeedback/MediaRequests` e `UserFeedback/PlaybackIssues` com um snapshot atômico da sessão que iniciou o envio. Os ViewModels cancelam a ação se a sessão mudar antes do início assíncrono; um teste HTTP troca servidor e conta antes do roteamento e verifica que host, token e dispositivo continuam sendo os originais. Isso não comprova que as rotas estejam implantadas ou registrando os dados no servidor; a validação autenticada em servidor de teste e a conferência no painel administrativo continuam pendentes.

Legendas externas têm validação unitária de SRT/VTT/ASS/SSA/TTML/DFXP, rota fallback, IDs sem colisão com `Format.id` embutido, URL relativa/same-origin, credenciais em URLs externas e política Cast. Teste Android reproduz cue SRT de fixture local enquanto usuário abre o menu de aparência e muda tamanho/cor/fundo; isso verifica renderização Media3 e estado de playback, mas não autenticação nem stream real do servidor. Reprodução remota e suporte no receiver Cast continuam pendentes de ambiente. O Cast padrão do Media3 não encaminha `SubtitleConfiguration` local.

O APK Android TV não anuncia um receptor Cast próprio nesta versão. Ocultar o controle de envio no perfil TV não torna o dispositivo descobrível/recebedor para outros apps ou para a versão Web; isso requer uma implementação de receiver compatível e a integração correspondente na Web, ainda pendentes.

Esses cenários devem ser executados com dados de teste controlados; credenciais reais não devem ser armazenadas nos testes ou relatórios.

## Perfis de emulador para UX adaptativa

Use os AVDs abaixo para validar superfícies diferentes:

- `MulletaflixApi35`: telefone, Android 15, API 35.
- `MulletaflixTabletApi35`: tablet, Android 15, 2560x1600, API 35.
- `MulletaflixTvApi34`: Android TV, Android 14, 1920x1080, API 34.

Comandos úteis:

```powershell
& "$env:ANDROID_HOME\emulator\emulator.exe" -avd MulletaflixTvApi34 -port 5556 -gpu host
& "$env:ANDROID_HOME\emulator\emulator.exe" -avd MulletaflixTabletApi35 -port 5558 -gpu host
& "$env:ANDROID_HOME\platform-tools\adb.exe" devices
```

Use `tools\with-emulator.ps1` para iniciar um AVD somente durante um comando. O `finally` encerra primeiro o AVD pelo ADB, aguarda os processos QEMU capturados antes do shutdown e força apenas os handles ainda vivos cuja identidade (início e caminho) continua igual. Falhas do comando são propagadas depois do `finally`, garantindo limpeza também no caminho de erro. `tools\EmulatorProcessTree.Tests.ps1` cobre a captura dos descendentes e o isolamento de processos não pertencentes ao AVD.

Antes do boot, o wrapper localiza o arquivo `<AVD>.ini` entre os diretórios indicados por `ANDROID_AVD_HOME`, `ANDROID_USER_HOME`, perfil do usuário e caminho legado `ANDROID_SDK_HOME`. Se `ANDROID_AVD_HOME` apontar para uma pasta sem o arquivo de configuração, o wrapper corrige a variável apenas no processo de teste; se não localizar o AVD, falha imediatamente em vez de aguardar o timeout de boot. Comandos diretos ao `emulator.exe` dependem de as variáveis do ambiente já apontarem para o diretório correto.

O boot aceita até 4 minutos por padrão. Para uma imagem Android nova que ainda inicializa serviços do sistema, aumente somente o limite da execução (4–20 minutos), por exemplo `-BootTimeoutMinutes 12`; a espera extra não altera o critério de sucesso do teste.

O wrapper inicia por padrão com `-gpu host` e configura a preferência Windows de alto desempenho apenas para `emulator.exe` e `qemu-system-x86_64.exe`, para o notebook preferir a NVIDIA RTX 3050 sem alterar a preferência global. Antes dos testes, ele confirma que o serial ADB corresponde ao AVD e à porta solicitados, exige `nvidia-smi` e verifica o PID QEMU exato na lista de processos da NVIDIA; se qualquer vínculo ou uso da GPU dedicada não puder ser provado, falha. Isso também vale ao reutilizar um emulador já aberto. Testes instrumentados rejeitam qualquer modo diferente de `host`. O driver WDDM pode mostrar `N/A` para memória por processo; o total de memória exibido por `nvidia-smi` é global e não prova quanto pertence ao QEMU, então não deve ser tratado como medição individual de VRAM. `-gpu host` acelera os gráficos do emulador na GPU selecionada pelo Windows; a memória RAM do Android convidado continua sendo RAM do sistema compartilhada, não pode ser transferida para VRAM. `-GpuMode swiftshader` é fallback manual somente para comandos não instrumentados.

Tentativa Android 17/API 37 em 13/10/2026: AVD temporário `MulletaflixLanPermApi37_20261013`, imagem Google APIs 37.0 rev. 6, conectou ao ADB e ao serviço Package Manager, mas não publicou `sys.boot_completed`; `sys.system_server.start_count` chegou a 18. O wrapper foi interrompido antes do Gradle/instrumentação, encerrou o AVD e deixou ADB sem dispositivos. GPU NVIDIA foi confirmada durante a execução. Nenhum teste foi contado; não usar esta tentativa como aprovação ou reprovação da permissão `ACCESS_LOCAL_NETWORK`.

Todo teste instrumentado executado pelo wrapper deve declarar `expectedDeviceProfile=PHONE`, `TABLET` ou `TV`; ele confere o perfil real do AVD antes de iniciar Gradle. Exemplo em TV:

```powershell
.\tools\with-emulator.ps1 -AvdName MulletaflixTvApi34 -Port 5556 -Command .\gradlew.bat -CommandArgument @(':app:connectedDebugAndroidTest', '-Pandroid.testInstrumentationRunnerArguments.expectedDeviceProfile=TV', '--no-daemon', '--console=plain')
```

O wrapper aceita tarefas instrumentadas somente no formato explícito `:module:connectedDebugAndroidTest`, exige um perfil e valida um relatório novo com testes executados para cada módulo solicitado. Falha se todos forem ignorados ou se houver falhas/erros. Isso detecta perfil errado e casos em que o runner não instala o APK, mas Gradle informa `BUILD SUCCESSFUL`.

`HomeAdaptiveUsageTest` também confere o perfil dentro do teste e mede a largura dos cards:

```powershell
.\tools\with-emulator.ps1 -AvdName MulletaflixTvApi34 -Port 5556 -Command .\gradlew.bat -CommandArgument @(':feature:home:connectedDebugAndroidTest', '-Pandroid.testInstrumentationRunnerArguments.expectedDeviceProfile=TV', '--no-daemon', '--console=plain')
```

Use `PHONE`, `TABLET` ou `TV` conforme o perfil esperado.

`HomeLibraryNavigationTest` valida toque nos cards de biblioteca nos três perfis. `HomeLibraryRemoteNavigationTest` valida a ativação por D-pad e exige o perfil TV real:

```powershell
.\tools\with-emulator.ps1 -AvdName MulletaflixTvApi34 -Port 5556 -Command .\gradlew.bat -CommandArgument @(':feature:home:connectedDebugAndroidTest', '-Pandroid.testInstrumentationRunnerArguments.expectedDeviceProfile=TV', '-Pandroid.testInstrumentationRunnerArguments.class=org.mulletaflix.feature.home.HomeLibraryRemoteNavigationTest', '--no-daemon', '--console=plain')
```

`TvHomeRefreshIntegrationTest` executa a `HomeScreen` real com `HomeViewModel`: verifica livros ocultos nas faixas e na biblioteca Livros, conserva Filmes e valida a atualização do feed ao voltar ao primeiro plano. Para rodar a classe no AVD TV:

```powershell
.\tools\with-emulator.ps1 -AvdName MulletaflixTvApi34 -Port 5556 -Command .\gradlew.bat -CommandArgument @(':feature:home:connectedDebugAndroidTest', '-Pandroid.testInstrumentationRunnerArguments.expectedDeviceProfile=TV', '-Pandroid.testInstrumentationRunnerArguments.class=org.mulletaflix.feature.home.TvHomeRefreshIntegrationTest', '--no-daemon', '--console=plain')
```

Revalidação observada em 2026-10-13: 2/2 testes, 0 falhas/erros/ignorados; XML `feature/home/build/outputs/androidTest-results/connected/debug/TEST-MulletaflixTvApi34(AVD) - 14.xml`. Isso valida Home integrada com feed sintético no AVD API 34, não TV física nem servidor real.

Testes de TV devem validar foco remoto, grade compacta e atualização em primeiro plano. Testes de tablet devem validar conteúdo centralizado, rolagem e capas retangulares sem aplicar a política de foco da TV.

`PlayerSeekControlsTest` cobre os saltos configuráveis de 5/10/15/30 segundos e seus rótulos acessíveis. `ChoiceDialogOptionsTest` cobre seleção do intervalo nos Ajustes. Execute ambos em PHONE, TABLET e TV com `tools\with-emulator.ps1`; a configuração fica local no DataStore e não exige servidor.

### Efeito de checagem de atualização e isolamento da fixture

`AppUpdateCheckEffectTest` valida que o callback roda enquanto a Activity está
resumed, para no background e é cancelado quando `enabled` muda para `false`.
O teste de desativação confirma callbacks ativos antes da mudança e avança o
relógio Compose após a recomposição; `Thread.sleep` não prova que o loop
periódico continuou. `MainActivity` compõe o efeito quando a sessão está
resolvida e passa a decisão de política via `enabled`. A política só deixa a
fixture debug desativar a chamada externa; build de produção ignora o opt-out.
Em 2026-10-13, RED observou callbacks subirem de 2 para 5; GREEN passou 2/2 em
PHONE/API 35. Esta evidência cobre o harness/lifecycle, não uma área de
funcionalidade homologada.

### Revalidação Readium e jornada EPUB em tablet e Android TV — 2026-10-16

Comando executado pelo wrapper, com AVDs temporários, GPU NVIDIA confirmada e encerramento ao final:

```powershell
.\tools\with-emulator.ps1 -AvdName MulletaflixCodexTabletApi35_20261015 -Port 5558 -Command .\gradlew.bat -CommandArgument @(':feature:item-detail:connectedDebugAndroidTest', '-Pandroid.testInstrumentationRunnerArguments.expectedDeviceProfile=TABLET', '-Pandroid.testInstrumentationRunnerArguments.class=org.mulletaflix.feature.itemdetail.BookReaderReadiumIntegrationTest', '--no-daemon', '--console=plain')
.\tools\with-emulator.ps1 -AvdName MulletaflixCodexTvApi34_20261015 -Port 5556 -Command .\gradlew.bat -CommandArgument @(':feature:item-detail:connectedDebugAndroidTest', '-Pandroid.testInstrumentationRunnerArguments.expectedDeviceProfile=TV', '-Pandroid.testInstrumentationRunnerArguments.class=org.mulletaflix.feature.itemdetail.BookReaderReadiumIntegrationTest', '--no-daemon', '--console=plain')
.\tools\with-emulator.ps1 -AvdName MulletaflixCodexTabletApi35_20261015 -Port 5558 -Command .\gradlew.bat -CommandArgument @(':feature:item-detail:connectedDebugAndroidTest', '-Pandroid.testInstrumentationRunnerArguments.expectedDeviceProfile=TABLET', '-Pandroid.testInstrumentationRunnerArguments.class=org.mulletaflix.feature.itemdetail.BookReaderScreenIntegrationTest#epubSearchFindsAndNavigatesToMatchingText', '--no-daemon', '--console=plain')
.\tools\with-emulator.ps1 -AvdName MulletaflixCodexTvApi34_20261015 -Port 5556 -Command .\gradlew.bat -CommandArgument @(':feature:item-detail:connectedDebugAndroidTest', '-Pandroid.testInstrumentationRunnerArguments.expectedDeviceProfile=TV', '-Pandroid.testInstrumentationRunnerArguments.class=org.mulletaflix.feature.itemdetail.BookReaderScreenIntegrationTest#epubSearchFindsAndNavigatesToMatchingText', '--no-daemon', '--console=plain')
```

Resultado: `BookReaderReadiumIntegrationTest` passou 11/11 em TABLET/API 35 e 11/11 em TV/API 34; `BookReaderScreenIntegrationTest.epubSearchFindsAndNavigatesToMatchingText` passou 1/1 em cada perfil. Todos os relatórios indicam 0 falhas/erros/ignorados. XMLs preservados em `validation-evidence/2026-10-16/BookReaderReadiumIntegrationTest-{TABLET-API35,TV-API34}.xml` e `BookReaderScreenIntegrationTest-epub-search-{TABLET-API35,TV-API34}.xml`. A jornada abre busca na tela Compose, consulta e seleciona resultado e confirma texto do capítulo de destino. EPUB servido por MockWebServer é sintético; Compose semântico não valida operação física por toque/D-pad, fala do TalkBack, arquivo autorizado ou servidor real. Não promove APK-H21 nem aceite formal.

### Ações do menu do leitor por D-pad — Android TV/API 34 — 2026-10-16

```powershell
.\tools\with-emulator.ps1 -AvdName MulletaflixCodexTvApi34_20261015 -Port 5556 -Command .\gradlew.bat -CommandArgument @(':feature:item-detail:connectedDebugAndroidTest', '-Pandroid.testInstrumentationRunnerArguments.expectedDeviceProfile=TV', '-Pandroid.testInstrumentationRunnerArguments.class=org.mulletaflix.feature.itemdetail.BookReaderProgressActionsTest', '--no-daemon', '--console=plain')
```

Resultado: 4/4 testes aprovados, 0 falhas/erros/skips. Os três testes TV usam `Key.DirectionCenter` e assertam foco/navegação para reiniciar, salvar posição, abrir lista e renomear marcador; o quarto cobre cancelar reinício. XML em `validation-evidence/2026-10-16/BookReaderProgressActionsTest-TV-API34.xml`. O input é simulado pela regra Compose no AVD; não representa controle remoto físico, TalkBack falado, arquivo real ou serviço. Não promove APK-H21 ou aceite formal.

### Leitor KF8 e imagens inline — 2026-10-14

Cobertura JVM/Robolectric em `Kf8ImageResourcesTest`, `PlainTextBookDocumentTest`
e `MobiBookTextExtractorTest` valida imagens locais `data:image`, referências
KF8, registros CONT/CRES, offset do cabeçalho, IDs globais de híbridos, sentinel
legado, limites de bytes/expansão e rejeição de dimensões inseguras via
`BitmapFactory` bounds. O caso híbrido usa fixture derivada da
amostra de teste calibre existente, não conteúdo do servidor.

Comandos e evidência desta sessão:

- `:feature:item-detail:testDebugUnitTest --tests '...Kf8ImageResourcesTest' --tests '...PlainTextBookDocumentTest.preserves bounded embedded raster images in readable HTML' --tests '...PlainTextBookDocumentTest.ignores remote and oversized HTML images instead of loading them' --tests '...MobiBookTextExtractorTest.reads a real unencrypted AZW3 publication' --tests '...MobiBookTextExtractorTest.prefers KF8 text in a real hybrid MOBI publication'`: `BUILD SUCCESSFUL` após os casos focais (incluindo nova repetição de limite/híbrido).
- `testDebugUnitTest`: `BUILD SUCCESSFUL` na árvore final, 470 tarefas/5 executadas/465 UP-TO-DATE.
- `:feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin :app:compileDebugAndroidTestKotlin`: `BUILD SUCCESSFUL` na árvore final incluindo limite de dimensão, 727 tarefas/28 executadas/699 UP-TO-DATE.
- `BookReaderScreenIntegrationTest.embeddedBookImageIsRenderedInlineWithAccessibleDescription` passou 1/1 em PHONE/API 35 (AVD `MulletaflixCodexPhoneApi35_20261012`) e 1/1 em TABLET/API 35 (AVD `MulletaflixTtsVoiceTabletApi35`) pelo wrapper; GPU NVIDIA RTX 3050 confirmada em ambos, os dois AVDs encerrados pelo wrapper e ADB sem dispositivos ao final. Confirma HTML `data:image` no Compose e semântica da descrição; o fixture é PNG 1×1, não valida fidelidade visual em dimensão real nem o caminho completo de recurso `kindle:embed` de um arquivo KF8.
- Em complemento (14/10/2026), `BookReaderScreenIntegrationTest.embeddedBookImagePixelsAreRenderedInlineWithAccessibleDescription` passou 1/1 em PHONE/API 35 e 1/1 em TABLET/API 35. O MockWebServer entrega PNG sintético 96×64; `captureToImage()` verifica pixel central magenta além do texto e da descrição. O XML persistente do TABLET registra 1 teste, 0 falhas/erros/ignorados; wrapper confirmou NVIDIA RTX 3050 nos dois perfis e encerrou os AVDs. O teste valida HTML `data:image` a Compose, não resolução `kindle:embed` no mesmo fluxo.
- A integração instrumentada híbrido→Compose agora cobre o mesmo documento completo e sua rolagem até a imagem virtualizada; PHONE/API 35 e TABLET/API 35 passaram 1/1. Seguem pendentes arquivos autorizados diversos, TalkBack/D-pad, hardware e servidor real. APK-H21 continua pendente.
- Limite: a fixture é derivada de AZW3 calibre com PNG sintético; não é mídia comercial. Não afirmar homologação em hardware ou fechamento de APK-H21.
- Histórico de 14/10/2026: quatro tentativas iniciais com outro arranjo falharam por pixel de imagem não alvo ou timeout de visibilidade com `performScrollToIndex`; isso não comprovou falha de produção. A abordagem que preserva o documento e usa `performScrollToNode` passou depois. Detalhes no `TDD-PLAN.md`.

Revalidação do módulo em 2026-10-15: `:feature:item-detail:testDebugUnitTest --rerun-tasks --no-daemon --console=plain` terminou `BUILD SUCCESSFUL`; 303 testes em 38 relatórios XML, 0 falhas, 0 erros, 0 ignorados (110 tarefas executadas). A chamada inicial sem `--rerun-tasks` deixou a tarefa `UP-TO-DATE` e não foi contada como execução. Este gate reexecuta os testes JVM do módulo; não executa instrumentação nem substitui o quality gate Android completo.

Curadoria da homologação em 2026-10-15: confirmado no `BookReaderPayloadPolicy` e extratores que o APK faz parsing local de EPUB, MOBI/AZW3/KF8, texto/HTML/Markdown/RTF/FB2, PDF, ODT e CBZ/CBR. APK-H21 ainda não está formalmente homologada: cobertura com fixtures/mocks não comprova arquivos autorizados, MIME/API autenticados, hardware, retomada nem controle remoto em todos os formatos.

### Ênfase textual HTML/KF8 — 2026-10-15

- `PlainTextBookReaderIntegrationTest.htmlBookPreservesBoldAndItalicFormatting` verifica texto sem estilo, `<strong>`, `<em>` e CSS inline combinado, inspecionando os intervalos de `AnnotatedString` no Compose.
- `PlainTextBookDocumentTest` cobre agora também regras CSS embutidas de classe, elemento/ID e descarte de seletores dentro de `@media`; a classe passou 39/39, zero falhas/erros/ignorados.
- AndroidTest focal do Compose passou 1/1 em PHONE/API 35 e 1/1 em TABLET/API 35; ambos com 0 falhas/erros/ignorados. XMLs preservados em `validation-evidence/2026-10-15/kf8-inline-formatting-{phone,tablet}-api35.xml`. AVDs descartáveis executados via `tools/with-emulator.ps1`; GPU NVIDIA confirmada e ambos encerrados (ADB sem dispositivos).
- Limite de CSS: só `<style>` embutido delimitado (256 Ki caracteres/2.048 regras), seletores simples de tag/classe/ID e `tag.class`/`tag#id`, `font-weight`/`font-style` e decoração. Não carrega CSS externo; combinadores, pseudo-seletores, precedência completa de `!important`, at-rules aninhadas, fontes e fidelidade integral KF8 seguem pendentes. O caso JVM é HTML sintético; não valida arquivo KF8 real, TalkBack, dispositivo físico, servidor real ou aceite formal APK-H21.
- Quality gate Android em 2026-10-15: `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin :app:compileDebugAndroidTestKotlin --no-daemon --console=plain` — `BUILD SUCCESSFUL`, 881 tarefas (8 executadas, 873 `UP-TO-DATE`). Os XMLs JVM disponíveis totalizaram 1.778 testes em 256 relatórios, 0 falhas/erros/ignorados; a classe focal do parser foi efetivamente reexecutada (39/39), demais tarefas `UP-TO-DATE` não são consideradas execução nova. Lint do módulo/app, `assembleDebug` e compilação AndroidTests concluídos.

### Reset de estilo normal aninhado HTML/KF8 — 2026-10-15

- TDD RED: `PlainTextBookReaderIntegrationTest.htmlBookExplicitNormalStyleResetsInheritedBoldAndItalic` falhou em PHONE/API 35 porque o texto filho com `font-weight: normal; font-style: normal` não tinha spans Compose `FontWeight.Normal`/`FontStyle.Normal`, enquanto os trechos em volta permaneciam bold/italic.
- GREEN: spans preservam `Boolean?` (ausente diferente de falso) e profundidade; `BookReaderScreen` resolve peso/estilo pela declaração ativa mais interna. O teste Compose passou 1/1 em PHONE/API 35, TABLET/API 35 e TV/API 34.
- Revisão adversarial encontrou que `text-decoration:none` em filho anulava decoração propagada do pai. `htmlBookTextDecorationPropagatesThroughDescendantWithNone` também teve RED e passou 1/1 nos três perfis depois de manter sublinhado/tachado ancestral acumulativos. A classe completa do leitor passou 15/15 em PHONE/API 35.
- `PlainTextBookDocumentTest` passou 41/41. Suíte JVM integral forçada com `testDebugUnitTest --rerun-tasks`: 1.780 testes em 256 XML, zero falhas/erros/ignorados (470 tarefas executadas). Lint do módulo/app, `assembleDebug` e compilação dos AndroidTests passaram (`BUILD SUCCESSFUL`, 727 tarefas; 64 executadas, 663 `UP-TO-DATE`).
- XMLs focais e classe em `validation-evidence/2026-10-15/`: `kf8-style-reset-{phone,tablet}-api35.xml`, `kf8-style-reset-tv-api34.xml`, `kf8-inline-formatting-phone-api35.xml` e `kf8-decoration-propagation-{phone,tablet}-api35.xml`, `kf8-decoration-propagation-tv-api34.xml`. NVIDIA confirmada e AVDs encerrados pelo wrapper; ADB vazio.
- Limites: CSS embutido segue subset, sem cascata geral, seletores avançados ou fontes; fixtures/AVDs não provam leitura de AZW3 autorizado, servidor real, TalkBack falado, navegação remota ou hardware físico. APK-H21 e aceite formal continuam pendentes.

### SyncPlay isolado pela identidade do servidor — 2026-10-15

- `SyncPlayViewModel` agora consome o snapshot `SessionState` e compara usuário + `RemotePlaybackIdentity`; servidor diferente invalida sala, WebSocket e refresh antigo mesmo quando o userId é igual. URLs pública e LAN do mesmo serverId continuam sendo a mesma sessão.
- TDD: `switching servers with the same user clears the active room` mostrou RED antes da correção (`activeGroupId` continuava `g1`) e GREEN depois; `switching LAN and public URLs for the same server keeps the active room` passou. Ambos passaram no teste focal. `SyncPlayViewModelTest`: 23 testes, 0 falhas/erros/ignorados.
- Gate executado em 2026-10-15: `testDebugUnitTest :feature:sync-play:lintDebug :app:lintDebug :app:assembleDebug :feature:sync-play:compileDebugAndroidTestKotlin :app:compileDebugAndroidTestKotlin --no-daemon --console=plain` terminou `BUILD SUCCESSFUL`; relatórios presentes totalizam 1.776 testes em 256 XML, zero falhas/erros/ignorados. O `SyncPlayViewModelTest` (23 testes) foi executado; demais testes sem alteração estavam `UP-TO-DATE`, não forçados a reexecutar. 881 tarefas (85 executadas, 796 UP-TO-DATE). AndroidTests foram compilados, não executados. Sem AVD, servidor real, bump, release ou portal.
- Limite: testes usam SessionState e repositório sintéticos. Não verificam desconexão do WebSocket contra serviço real, reconexão, dispositivos físicos nem comportamento do player após troca de servidor. A homologação formal não foi promovida.

### Jornada de evento realtime visível no SyncPlay — 2026-10-16

`SyncPlayScreenRealtimeJourneyTest.joiningRoomRendersCommandReceivedByRealWebSocketClient` integra `SyncPlayScreen` e `SyncPlayViewModel` reais com `SyncPlayRealtimeClient` real. O teste toca em “Entrar na sala”; o repositório de salas responde sinteticamente, o WebSocket conecta ao MockWebServer local e recebe um `SyncPlayCommand` `Pause` para a sala ativa. Asserções verificam sala ativa, `realtimeConnected`, mensagem no estado (`Pause recebido pelo grupo`) e texto de conexão visível.

PHONE/API 35, TABLET/API 35 e TV/API 34: 1/1 por perfil, zero falhas/erros/ignorados; tempos 5,256 s, 5,381 s e 9,585 s. XMLs individuais em `validation-evidence/2026-10-16/SyncPlayScreenRealtimeJourneyTest-{phone-api35,tablet-api35,tv-api34}.xml`. A primeira execução PHONE chegou à mensagem correta, mas falhou porque o mesmo texto aparece duas vezes (evento persistente e snackbar); o teste final verifica o estado de conexão visível, sem fixar a quantidade duplicada. Uma execução TABLET anterior falhou somente no teardown: MockWebServer não recebeu confirmação de fechamento a tempo. O servidor de fixture passou a confirmar `onClosing`, e o teste espera o socket fechar; repetições finais passaram nos três perfis. Nenhuma falha de produção foi demonstrada.

Execução pelo `tools/with-emulator.ps1` em PHONE/API 35, TABLET/API 35 e TV/API 34; GPU NVIDIA confirmada, cada AVD encerrado pelo wrapper e `adb devices` vazio após os testes. Limites: servidor e sessão são sintéticos; não exercita DI/autenticação de produção, API real, player sincronizado, reconexão na UI nem hardware físico. Não promove homologação formal.

### Imagem de MOBI híbrido completo em Android TV — 2026-10-15

`PlainTextBookReaderIntegrationTest.hybridMobiImageRendersAtOriginalPositionInFullDocumentAfterScroll` passou 1/1 em Android TV/API 34 (134,355 s), 0 falhas/erros/ignorados. O teste percorre a fixture AZW3/MOBI híbrida até Compose, rola para a imagem sintética na posição original e confirma pixel magenta e descrição acessível. XML: `validation-evidence/2026-10-15/hybrid-mobi-full-document-tv-api34.xml`. Wrapper confirmou NVIDIA RTX 3050, encerrou `MulletaflixCodexTvApi34_20261015`, e ADB ficou sem dispositivos. Não valida navegação por D-pad/OK, TalkBack falado, arquivo autorizado, hardware físico ou servidor; a homologação formal não avança.

### Diagnóstico da tela após retomada offline — 2026-10-15

`DownloadProcessRestartIntegrationTest` executou em PHONE/API 35 pelo wrapper; preparação passou 1/1, Range após reinício iniciou em 1.032.832, download completou e cleanup passou 1/1. O teste leu/assertou a sessão DataStore sintética. A verificação de UI falhou: em 30 s, `PlayerState` continuou sem título/erro e o player permaneceu `IDLE`, sem erro/item. Compilação AndroidTest passou. O wrapper encerrou o AVD e ADB ficou sem devices. Investigar disparo do `LaunchedEffect`, lifecycle/raiz Compose e início de `loadOffline`; não declarar defeito do app sem localizar a causa. Isso não é evidência aprovada de UI nem altera matriz formal.

Repetição diagnóstica PHONE/API 35: o download retomou via Range (1.046.400), preparação/limpeza passaram, mas a UI expirou novamente. Marcadores mostraram Activity `RESUMED`, composição observada (`SideEffect=true`) e o marcador `LaunchedEffect(Unit)` independente não foi observado (`false`) até o timeout; avançar um frame do relógio Compose não mudou o resultado. Media3 seguiu `IDLE`, sem item/erro. Esta tentativa não avaliou o efeito da tela: investigar primeiro configuração/dispatcher do `createAndroidComposeRule` v2 e recomposer/lifecycle do host com um teste mínimo de controle. Compilação AndroidTest passou; wrapper encerrou AVD e ADB ficou vazio. Sem atribuir falha ao runtime do APK.

Controle mínimo e reteste da jornada (15/10/2026): `composeHostRunsLaunchedEffectWithoutPlayerScreen` passou 1/1 em PHONE/API 35; repetido com `waitForIdle()` explícito passou 1/1 (4,336 s), RTX 3050 confirmada. `:app:compileDebugAndroidTestKotlin` passou. O controle confirma que o host simples executa `LaunchedEffect` após sincronização Compose. Na jornada completa, Range retomou em 1.039.744, transferência completou e cleanup passou 1/1, mas a UI expirou após 30 s mesmo com `composeRule.waitForIdle()`: Activity `RESUMED`, `screenComposed=true`, `hostLaunchedEffectStarted=false`, `PlayerState` sem título/erro e Media3 `IDLE` sem item/erro. Resultado não aponta causa de produção; agora comparar composição vazia, ViewModel e tela real em degraus. O wrapper encerrou o AVD temporário; ADB ficou vazio. A tarefa instrumentada encerrou com código 1 por falha da fase visual.

Teste de rotação do host (15/10/2026): novo teste `offlinePlaybackHostKeepsComposeContentWhenOrientationChanges` falhou antes da correção com `No compose hierarchies found`, confirmando que solicitar outra orientação destruía a composição da Activity debug. `app/src/debug/AndroidManifest.xml` agora espelha o `configChanges` já declarado pela MainActivity de produção; o teste exige texto visível e que `LaunchedEffect(Unit)` não reinicie, passando 1/1 em PHONE/API 35 (8,817 s). XML preservado em `validation-evidence/2026-10-15/offline-player-host-orientation-PHONE-API35.xml`. A jornada integral então retomou Range no byte 1.046.701 e validou a preparação, mas a composição de `VideoPlayerScreen` encontrou `IllegalStateException` do Media3 Cast/MediaRouter por acesso fora da thread principal sob o dispatcher da regra Compose v2. Isso é uma falha do cenário instrumentado; não demonstra falha equivalente em runtime de produção. O teste focal foi executado no AVD `MulletaflixCodexPhoneApi35_20261012`, GPU NVIDIA confirmada, encerrado pelo wrapper; ADB terminou vazio. A fase visual Cast não tem relatório individual arquivado.

Reteste integrado (16/10/2026): o harness trocou para `androidx.compose.ui.test.junit4.createAndroidComposeRule`, a mesma regra clássica usada pelo teste de alvo Cast, e deixou de chamar `setContent` uma segunda vez no `finally` (a Activity/rule controla o teardown). A primeira execução após a correção passou 1/1: título “Retomada offline” visível, `Player.STATE_READY`, posição avançando ≥500 ms e endpoint de auditoria sem requisição à origem de mídia bloqueada. Repetição independente posterior também passou 1/1; Range retomou em 1.040.096 bytes e recebeu 3.847.559 bytes restantes. Cada execução teve preparação 1/1, fase integrada 1/1 e cleanup UUID 1/1. A execução é `am instrument` direto; não gera XML individual, portanto a saída do runner foi observada e o limite foi mantido explícito. O wrapper confirmou NVIDIA e encerrou somente o AVD PHONE temporário; ADB ficou vazio. Fixture e sessão sintéticas; sem validação de servidor/hardware reais ou promoção formal.

Quality gate APK em 15/10/2026: `testDebugUnitTest :app:lintDebug :app:assembleDebug :app:compileDebugAndroidTestKotlin --no-daemon --console=plain` terminou `BUILD SUCCESSFUL` (871 tarefas; 7 executadas, restante up-to-date). A suíte foi forçada separadamente com `testDebugUnitTest --rerun-tasks --no-daemon --console=plain`: 1.784 testes em 256 XML, 0 falhas, 0 erros, 0 ignorados; 470 tarefas executadas. `git diff --check` também passou antes das atualizações documentais. APK-H07/H17 e a matriz formal não avançam.

### Recuperação offline de legendas: identidade de downloads legados — 2026-10-16

Comando: `:app:testDebugUnitTest --tests "org.mulletaflix.android.service.OfflineSubtitleRecoveryPolicyTest" --no-daemon --console=plain`. Resultado confirmado no XML `app/build/test-results/testDebugUnitTest/TEST-org.mulletaflix.android.service.OfflineSubtitleRecoveryPolicyTest.xml`: 8 testes, 0 falhas, 0 erros e 0 ignorados. O caso `skips legacy entries without owner and downloads without external subtitles` usa download legado com legenda, mas sem proprietário, e verifica que não entra na recuperação da sessão atual. A implementação e o teste já existiam; esta sessão corrigiu o status desatualizado do TODO, sem alteração de código produtivo, dependência ou homologação formal. Teste unitário JVM apenas; não exercita servidor real ou dispositivo.
# Android 17 / permissão de rede local — 2026-10-16

- Foi instalado no SDK local o system image `system-images;android-37.2;google_apis_ps16k;x86_64` e criado o AVD descartável `MulletaFlixAndroid17Api37_20261016` (Pixel 6, 4 GB RAM, GPU host). O AVD antigo foi preservado.
- O boot chegou a `sys.boot_completed=1`, mas `system_server` reiniciou repetidamente (contador observado: 7); Gradle encerrou sem relatório AndroidTest novo. Portanto `LocalServerDiscoveryPermissionTest` não tem resultado contabilizável nesta tentativa; não é aprovação nem falha funcional do APK.
- Repetir em ambiente com AVD Android 17 estável e conferir XML/contagem do runner antes de registrar cobertura. A permissão `ACCESS_LOCAL_NETWORK`/descoberta API 37 continua sem homologação.

### Diagnóstico de boot com artefatos persistidos — 2026-10-16

- `tools/Save-AndroidEmulatorBootEvidence.ps1` registra, antes do shutdown do wrapper, estado de boot/Package Manager, propriedades de boot, serviços relevantes e linhas filtradas de logcat; contexto próximo de abort/crash é preservado e tokens/credenciais conhecidos são redigidos.
- Artefatos: `validation-evidence/2026-10-16/emulator-boot-diagnostics/MulletaFlixAndroid17Api37_20261016-5560-174534/`. Evidência: `surfaceflinger` recebeu `SIGABRT` em `RegionSampling`, falhou quatro vezes antes do boot e derrubou `system_server`; contador `sys.system_server.start_count=13`; `sys.boot_completed` vazio e Package Manager não pronto na captura.
- Tentativa AndroidJUnit terminou antes do runner. A primeira invocação omitiu `expectedDeviceProfile=PHONE` e foi rejeitada antes de Gradle; a segunda incluiu o perfil e expirou no boot. Contagem: zero testes e nenhum XML novo. Wrapper encerrou o AVD sem limpar dados; `adb devices` terminou vazio.
- Pester `tools/Save-AndroidEmulatorBootEvidence.Tests.ps1`: 3/3; suíte `tools/`: 51/51 antes do ajuste final de contexto; parser PowerShell limpo. Reexecutar a suíte inteira depois das mudanças do filtro de stack trace. Isso valida captura de evidência, não o APK nem a permissão API 37.
- Próximo: avaliar outra imagem/versão de Emulator após o diagnóstico; não afrouxar `sys.boot_completed` nem contar execução ausente. `ACCESS_LOCAL_NETWORK` API 37 segue sem homologação.

### Revalidação Android TV: Home atualizada e livros ocultos — 2026-10-16

Comando: `tools/with-emulator.ps1 -AvdName MulletaflixCodexTvApi34_20261015 -Port 5560 -BootTimeoutMinutes 4 -Command gradlew.bat -CommandArgument @(':feature:home:connectedDebugAndroidTest', '-Pandroid.testInstrumentationRunnerArguments.expectedDeviceProfile=TV', '-Pandroid.testInstrumentationRunnerArguments.class=org.mulletaflix.feature.home.TvHomeRefreshIntegrationTest', '--no-daemon', '--console=plain')`. Resultado `BUILD SUCCESSFUL`; relatório preservado em `validation-evidence/2026-10-16/tv-home-refresh-library-hidden-TV-API34.xml`: 2 testes, 0 falhas/erros/ignorados. `booksLibraryFromLoadedFeedIsHiddenByRealTvHomeScreen` e `returning_to_tv_home_replaces_visible_media_with_refreshed_feed` passaram. Wrapper confirmou QEMU na NVIDIA RTX 3050, encerrou o AVD e ADB terminou sem dispositivos. A Home e feed são reais no app, mas os dados são sintéticos; não valida TV física, servidor remoto, duração prolongada ou aceite formal.
### Android 17/API 37 Play Store — tentativa de validação de rede local — 2026-10-16

- Instalado o system image `system-images;android-37.2;google_apis_playstore_ps16k;x86_64` e criado AVD isolado `MulletaFlixAndroid17PlayApi37_20261016`; configuração Play Store habilitada, GPU host/NVIDIA e RAM 4 GiB. Nenhum AVD anterior foi alterado.
- A tentativa via `tools/with-emulator.ps1` não chegou ao runner: dispositivo `emulator-5560` ficou `offline`, depois desapareceu após reiniciar ADB; nenhum processo Gradle/test runner permaneceu e não houve relatório JUnit novo. O processo do emulador recém-criado foi encerrado; dispositivo órfão desconectado do ADB.
- Resultado: infraestrutura inconclusiva, **zero testes executados**; não contar como falha do aplicativo nem aprovação. Nenhuma evidência de execução local foi promovida.
- Próximo passo: investigar log de inicialização/QEMU e compatibilidade Play Store API 37 antes de nova tentativa. Não repetir com AVD enquanto não houver `device` online e boot completo.
