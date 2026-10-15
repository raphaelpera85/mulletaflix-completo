# Testes do MulletaFlix Android

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

### PDF escaneado: busca OCR pela tela do leitor — 2026-10-15

`BookReaderScreenIntegrationTest.scannedPdfOcrSearchFindsAndOpensMatchingPageOffline` abre no Compose um PDF sintético sem camada textual, usa o recognizer OCR local, busca `pagina vertical`, mostra o trecho reconhecido da página 2 e navega até ela. Passou 1/1 em PHONE/API 35 (21,859 s) e TABLET/API 35 (20,288 s), 0 falhas/erros/ignorados; XMLs independentes em `validation-evidence/2026-10-15/pdf-scanned-search-UI-{PHONE,TABLET}-API35.xml`. Isso caracteriza a jornada de UI local com OCR, mas não valida arquivo de mídia real, hardware, TalkBack nem bateria/memória em uso prolongado. Não houve alteração de produção.

### OCR e busca em PDF digitalizado paisagem/retrato — 2026-10-15

`PdfBookReaderIntegrationTest.scannedPdfPageIsRecognizedOfflineAndIndexedBySearch` usa duas páginas sintéticas sem camada textual: paisagem 1200×600 e retrato 600×1200. PHONE/API 35 reconheceu offline ambas e a busca encontrou cada frase na página correta; 1/1 em 9,123 s, XML `validation-evidence/2026-10-15/pdf-scanned-landscape-portrait-ocr-search-PHONE-API35.xml`. Isso prova duas orientações e busca multipágina numa fixture, não resolução variável, PDF extenso, mídia real, alfabetos não latinos, tablet/TV ou uso de memória/bateria em hardware.

Quality gate final da rodada: `testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug :feature:item-detail:compileDebugAndroidTestKotlin --no-daemon --console=plain` terminou `BUILD SUCCESSFUL` (871 tarefas; 6 executadas e 865 `UP-TO-DATE`). Os testes JVM estavam `UP-TO-DATE`, então não se afirma reexecução integral deles nesta rodada; o AndroidTest focal foi executado separadamente conforme XML acima.

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
| SyncPlay realtime (`:core:api`) | Handshake/comandos via `events`; falha HTTP 503 emite `Disconnected` e reconecta; close remoto com status e frame sem status concluem handshake (`1005` sentinela respondido com `1000`); `stop()` cancela retry confirmado como ativo | JVM + MockWebServer/peer TCP local (`SyncPlayRealtimeClientTest`, `SyncPlayCleartextPolicyTest`, `SyncPlayRealtimeGroupSwitchTest`); quality gate de 13/10/2026. Sem validação em servidor/dispositivo real |
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

### Imagem de MOBI híbrido completo em Android TV — 2026-10-15

`PlainTextBookReaderIntegrationTest.hybridMobiImageRendersAtOriginalPositionInFullDocumentAfterScroll` passou 1/1 em Android TV/API 34 (134,355 s), 0 falhas/erros/ignorados. O teste percorre a fixture AZW3/MOBI híbrida até Compose, rola para a imagem sintética na posição original e confirma pixel magenta e descrição acessível. XML: `validation-evidence/2026-10-15/hybrid-mobi-full-document-tv-api34.xml`. Wrapper confirmou NVIDIA RTX 3050, encerrou `MulletaflixCodexTvApi34_20261015`, e ADB ficou sem dispositivos. Não valida navegação por D-pad/OK, TalkBack falado, arquivo autorizado, hardware físico ou servidor; a homologação formal não avança.
