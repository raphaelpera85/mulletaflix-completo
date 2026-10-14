# Testes do MulletaFlix Android

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
- Gate observado em 2026-10-12: 1.701 testes JVM em 250 XML, sem falhas/erros/
  ignorados; ambos os lint, assemble Debug e compilação AndroidTest concluíram.
  Isso não substitui Compose/instrumentação em PHONE/TABLET/TV.

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

`MainActivitySessionNavigationTest.switchingServerFromProfileDoesNotRestorePreviousProfileOnBackAfterLogin` cobre Home → Perfil → seleção de outro servidor → login → Home → Voltar. A troca iniciada no Perfil deve limpar a pilha da Home anterior; Voltar da nova Home encerra a Activity sem restaurar o Perfil/servidor anterior. Em 2026-10-14, a classe completa passou 10/10 em PHONE/API 35 com MockWebServer; isso não valida servidor ou dispositivo real.

O teste `crossServerDeepLinkAuthenticatesOnTargetServerAndLoadsItemWithoutRequestingOldServer` cobre um link recebido de outro servidor. Configure explicitamente `SessionRepository.setBaseUrl(oldServerUrl)` no fixture antes de salvar a sessão: `saveSession` não é substituto dessa seleção de endpoint. Em falha pós-login, o teste reporta requisições dos dois servidores, endpoint/sessão persistidos e texto visível. Duas execuções anteriores da classe completa foram intermitentes; veja o resultado focal e a pendência de estabilidade na seção abaixo.

```powershell
.\tools\with-emulator.ps1 -AvdName MulletaflixApi35 -Port 5572 -Command .\gradlew.bat -CommandArgument @(':app:connectedDebugAndroidTest', '-Pandroid.testInstrumentationRunnerArguments.expectedDeviceProfile=PHONE', '-Pandroid.testInstrumentationRunnerArguments.class=org.mulletaflix.android.MainActivitySessionNavigationTest#settingsLogoutClearsPersistedSessionBeforeShowingServerSelection', '--no-daemon', '--console=plain')
```

### Deep link cross-server e recuperação LAN

`MainActivitySessionNavigationTest.crossServerDeepLinkAuthenticatesOnTargetServerAndLoadsItemWithoutRequestingOldServer` cobre sessão A → seleção manual de B → login → mídia do link. Após autenticação, Voltar retorna à Home autenticada em B. O caso deve rodar com `LanServerRecovery` ativo; não pare o worker no fixture para contornar alternância de endpoint. `DeepLinkNavigationPolicyTest` valida a sequência pura de destinos.

O helper `launchMainActivity` da classe adiciona `EXTRA_SUPPRESS_APP_UPDATE_CHECK_FOR_TEST`: ele suprime a consulta ao GitHub somente em builds debug e apenas nas Activities iniciadas pela fixture. Política JVM garante que build não-debug continue verificando atualizações mesmo se receber esse extra. Os testes próprios de `AppUpdateCheckEffect` continuam cobrindo lifecycle e periodicidade.

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
| Perfil de Usuário (`:feature:user`) | Carregamento de perfil, fallback de sessão offline, alternância rápida de usuário, limpeza de cache e logout; em Ajustes, falha de logout mantém o destino e sucesso dispara navegação só após limpeza da sessão | JVM (`SettingsViewModelTest`) + Compose de falha em Perfil |
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
| TV ao vivo / EPG (`:feature:live-tv`) | Canais e guia, agendamento otimista, resolução limitada do ID do timer, retry sem reabrir guia, respostas obsoletas após offline/troca de sessão, pré-validação de sessão/rede ao cancelar timer; ações acessíveis na TV | JVM + Compose instrumentado |
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

Use `tools\with-emulator.ps1` para iniciar um AVD somente durante um comando; o script encerra o processo criado no bloco `finally`:

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
