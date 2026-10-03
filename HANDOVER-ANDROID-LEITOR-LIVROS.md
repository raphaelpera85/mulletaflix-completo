# Handover: leitor de livros no APK MulletaFlix

**Atualizado:** 2026-10-02  
**Escopo desta sessão:** aplicativo Android (APK). Não alterar servidor nem portal.

## Objetivo e estado atual

O usuário relatou que tocar em um livro abria uma tela em paisagem, parecida com o player de vídeo, mas não abria o livro. A causa encontrada foi que a ação genérica de detalhe encaminhava `Book` para `VideoPlayerScreen`, que força orientação paisagem. O aplicativo ainda não tinha rota nem leitor Android dedicado.

Foi implementada uma rota de leitura EPUB no APK usando Readium 3.4.0 e o endpoint autenticado já existente no servidor. Livros em telefone/tablet recebem ação **Ler livro**; livros não usam player de vídeo. A leitura roda na orientação atual do dispositivo. A ação fica indisponível na TV, conforme a política do app para essa plataforma.

O código e o APK Debug compilam. Os testes JVM do módulo passaram. **Ainda não houve revisão adversarial do diff nem teste visual/end-to-end em emulador ou dispositivo.** Não considerar esta implementação pronta para produção ou release até cumprir as pendências abaixo.

## Alterações desta correção

No projeto `MulletaFlix-android/`:

- `feature/item-detail/src/main/java/org/mulletaflix/feature/itemdetail/DetailPlaybackTarget.kt`: distingue ação `ReadBook` de `PlayVideo`; impede Livro de entrar na rota de vídeo; limita leitura ao telefone/tablet.
- `feature/item-detail/src/main/java/org/mulletaflix/feature/itemdetail/ItemDetailScreen.kt`: mostra ação **Ler livro** e encaminha o ID para a rota própria.
- `feature/item-detail/src/main/java/org/mulletaflix/feature/itemdetail/BookReaderScreen.kt`: nova tela Compose de leitura, controles anterior/próximo, loading, erro e retry. Não define orientação fixa.
- `feature/item-detail/src/main/java/org/mulletaflix/feature/itemdetail/BookReaderViewModel.kt`: chama endpoint com a sessão Retrofit existente, guarda EPUB temporário no cache, abre publicação Readium e trata erros HTTP/formato.
- `core/api/src/main/java/org/mulletaflix/core/api/MulletaFlixApiService.kt`: adiciona `GET BookReader/Items/{itemId}/BookReader/Epub` com resposta em streaming.
- `app/src/main/java/org/mulletaflix/android/navigation/MulletaFlixNavHost.kt`: registra `reader/book/{itemId}` e conecta detalhe à tela leitora.
- `feature/item-detail/src/test/java/org/mulletaflix/feature/itemdetail/DetailPlaybackTargetTest.kt`: cobre seleção de ação para Livro, exclusão do player de vídeo e regra para TV.
- `feature/item-detail/build.gradle.kts`, `app/build.gradle.kts`, `gradle/libs.versions.toml`: dependências Readium/desugaring necessárias.

## Evidência de validação

Executado em `MulletaFlix-android/`:

```powershell
.\gradlew.bat --no-daemon --no-parallel --max-workers=1 :feature:item-detail:testDebugUnitTest :feature:item-detail:compileDebugKotlin :app:compileDebugKotlin
.\gradlew.bat --no-daemon --no-parallel --max-workers=1 :app:assembleDebug
```

Ambos terminaram com `BUILD SUCCESSFUL` e código de saída 0. Relatórios do módulo `:feature:item-detail:testDebugUnitTest`: 62 testes, 0 falhas, 0 erros, 0 ignorados. `git diff --check` também terminou sem erro; Git mostrou apenas avisos de conversão LF/CRLF.

APK local gerado: `MulletaFlix-android/app/build/outputs/apk/debug/app-debug.apk` (38.611.474 bytes). É **Debug**, não assinado para produção e não pode ser publicado nem usado como release oficial.

`adb devices -l` não encontrou dispositivos conectados. O executável ADB existe em `C:\Android\Sdk\platform-tools\adb.exe`; use-o explicitamente ou configure `PATH`.

## Pendências para a próxima IA

1. Leia este handover e os arquivos listados abaixo antes de editar. Preserve alterações locais alheias à tarefa.
2. Faça revisão adversarial Cavecrew do diff atual. Verifique especialmente API do Readium experimental, navegação/back, lifecycle, limpeza do arquivo temporário, títulos longos, acessibilidade e erros de rede/autenticação.
3. Execute teste instrumentado em telefone ou tablet. Inicie AVD só durante o teste e encerre ao terminar, usando `tools/with-emulator.ps1` descrito em `MulletaFlix-android/TESTING.md`. Confirme que o leitor abre, pagina, volta, tenta novamente após erro e permanece na orientação escolhida pelo usuário. Teste também rotação manual durante a leitura.
4. Teste com usuário de QA e livros reais do servidor: EPUB direto; PDF, MOBI/AZW, TXT e HTML que o endpoint do servidor converte para EPUB; respostas 401/403/404/415; corpo vazio, tipo MIME incorreto e interrupção de rede. Nunca coloque credenciais reais em testes, relatórios ou handover.
5. O leitor móvel atual aceita apenas conteúdo cujo `Content-Type` indique EPUB. CBZ/CBR/outros arquivos de quadrinhos recebidos sem conversão EPUB exibem erro de formato. Decida escopo e implemente suporte próprio apenas se requisito do usuário pedir; não altere servidor.
6. Acrescente testes para tela/ViewModel e contrato HTTP, incluindo autorização/token, estados de loading/erro/retry, MIME, arquivo/cache e navegação. Reavalie se o endpoint exige `@Streaming` e se a resposta fecha em falhas/cancelamento.
7. Reexecute no mínimo `:feature:item-detail:testDebugUnitTest`, `:app:compileDebugKotlin`, `:app:assembleDebug` e os testes instrumentados aplicáveis. Registre códigos de saída e resultados reais.
8. Não altere versão nem publique por causa deste handover. A versão configurada em `gradle/libs.versions.toml` é `1.3.81` (`versionCode` 381). Antes de qualquer release futura, confira a versão do APK previamente enviado, `MulletaFlix-android/VERSIONING.md`, notas, certificado/keystore de produção e assinatura do artefato. Só gere APK de produção e atualize o portal quando houver solicitação/autorização para release; jamais publique o APK Debug.

## Arquivos a ler

### Regras obrigatórias do repositório

- `AGENTS.md` (raiz do repositório): escopo e instruções globais. Nesta conversa, o usuário restringiu o trabalho ao APK; servidor e portal não são áreas de implementação.
- `.agents/rules/mulletaflix-conventions.md`: convenções de arquitetura.
- `.agents/rules/gauntlet-loop.md`: processo Builder/Evaluator e evidência obrigatória.
- `MulletaFlix-android/README.md`: módulos, stack e configuração do APK.
- `MulletaFlix-android/TESTING.md`: comandos, cobertura, perfis de AVD e wrapper que inicia/fecha emuladores.
- `MulletaFlix-android/TODO-APP.md`: roadmap/pendências anteriores do APK; diferencie itens já concluídos de pendências abertas.
- `MulletaFlix-android/VERSIONING.md`, `MulletaFlix-android/release-notes-v1.3.81.md` e `MulletaFlix-android/signing/SIGNING-KEY-ROTATION.md`: leia antes de mexer em versão ou release. Não assuma que uma keystore local é a oficial sem verificar fingerprint.

### Código do fluxo de livros

- `MulletaFlix-android/feature/item-detail/src/main/java/org/mulletaflix/feature/itemdetail/BookReaderViewModel.kt`
- `MulletaFlix-android/feature/item-detail/src/main/java/org/mulletaflix/feature/itemdetail/BookReaderScreen.kt`
- `MulletaFlix-android/feature/item-detail/src/main/java/org/mulletaflix/feature/itemdetail/ItemDetailScreen.kt`
- `MulletaFlix-android/feature/item-detail/src/main/java/org/mulletaflix/feature/itemdetail/DetailPlaybackTarget.kt`
- `MulletaFlix-android/feature/item-detail/src/test/java/org/mulletaflix/feature/itemdetail/DetailPlaybackTargetTest.kt`
- `MulletaFlix-android/core/api/src/main/java/org/mulletaflix/core/api/MulletaFlixApiService.kt`
- `MulletaFlix-android/core/api/src/main/java/org/mulletaflix/core/api/di/NetworkModule.kt` e interceptors de autenticação/sessão relacionados.
- `MulletaFlix-android/app/src/main/java/org/mulletaflix/android/navigation/MulletaFlixNavHost.kt`
- `MulletaFlix-android/feature/player/src/main/java/org/mulletaflix/feature/player/VideoPlayerScreen.kt`: orientação do player; garantir que tela de livro não reutilize esse fluxo.
- `MulletaFlix-android/domain/src/main/java/org/mulletaflix/domain/model/MediaItem.kt` (confirme caminho real) e `MediaItemType`: IDs e tipo `Book` usados na navegação.
- `MulletaFlix-android/feature/item-detail/build.gradle.kts`, `MulletaFlix-android/app/build.gradle.kts`, `MulletaFlix-android/gradle/libs.versions.toml`: dependências e desugaring.
- `MulletaFlix-android/tools/with-emulator.ps1`: lifecycle automático dos AVDs.

O endpoint do servidor foi consultado somente como contrato de compatibilidade; servidor não foi alterado. Se necessário, confira `MulletaFlix-master/Jellyfin.Api/Controllers/BookReaderController.cs` e a implementação `BookConversionService.cs`. O endpoint entrega/converte formatos compatíveis para EPUB e pode retornar 415 para formatos não suportados; confirme o comportamento real do servidor de QA antes de ampliar suporte no APK.

## Skills e metodologias

### Usar durante continuação do desenvolvimento

- **Caveman:** `D:\Users\Raphael\Documents\Projetos\mulletaflix\.agents\skills\caveman\SKILL.md`. Estilo de comunicação desta conversa; não comprimir código nem documentação humana.
- **Caveman Compress:** `C:\Users\Raphael\.agents\skills\caveman-compress\SKILL.md`. Usar quando solicitado para comprimir arquivo de memória/documentação; preserva blocos de código, caminhos, comandos e estrutura. Não substituir documentação legível sem pedido.
- **Caveman Learn:** `C:\Users\Raphael\.agents\skills\caveman-learn\SKILL.md`. Só aplicar após mostrar dados/simulação e obter consentimento por edição; nunca afirmar economia não medida.
- **Cavecrew:** `D:\Users\Raphael\Documents\Projetos\mulletaflix\.agents\skills\cavecrew\SKILL.md`. Usar investigador para localizar fluxo amplo e reviewer para revisar diff. A revisão reviewer desta alteração continua pendente.
- **Gauntlet Loop:** `D:\Users\Raphael\Documents\Projetos\mulletaflix\.agents\skills\gauntlet-loop\SKILL.md`. Obrigatório: definir quality bar, construir, rodar comandos reais, corrigir falhas e registrar evidência.
- **Fable Method / Fable Loop / Fable Judge:** `C:\Users\Raphael\.agents\skills\fable-method\SKILL.md`, `C:\Users\Raphael\.agents\skills\fable-loop\SKILL.md`, `C:\Users\Raphael\.agents\skills\fable-judge\SKILL.md`. Manter intenção e critérios de aceite explícitos, iteração e avaliação separada.
- **Everything Claude Code:** `C:\Users\Raphael\.agents\skills\everything-claude-code\SKILL.md`. Consulte para orientações gerais de engenharia/agentes; as regras do projeto e o usuário prevalecem.
- **Android Dev:** `C:\Users\Raphael\.agents\skills\android-dev\SKILL.md` e referência específica em `C:\Users\Raphael\.agents\skills\android-dev\references\native-android.md` / `detailed-guide.md`.
- **Android Jetpack Compose Expert:** `C:\Users\Raphael\.agents\skills\android-jetpack-compose-expert\SKILL.md`. Aplicar em estado Compose, ViewModel, efeitos e navegação.
- **Android CLI:** `C:\Users\Raphael\.agents\skills\android-cli\SKILL.md`. Usar para diagnóstico ADB/SDK/AVD. A CLI `android` não estava no PATH; ADB está no SDK informado acima.
- **Android UI Verification:** `C:\Users\Raphael\.agents\skills\android_ui_verification\SKILL.md`. Requer AVD/dispositivo ativo; calibrar tamanho, inspecionar UI e validar screenshot/estados. Ainda não aplicado por falta de dispositivo conectado.
- **Mobile Design:** `C:\Users\Raphael\.agents\skills\mobile-design\SKILL.md`. Antes de novo trabalho de interface, ler as referências obrigatórias: `mobile-design-thinking.md`, `touch-psychology.md`, `mobile-performance.md`, `mobile-backend.md`, `mobile-testing.md`, `mobile-debugging.md` e `platform-android.md` na mesma pasta da skill. A skill pede checkpoint de plataforma/framework/dispositivos e riscos antes do código.

## Notas de estado do workspace

O diretório de trabalho tinha outras alterações fora desta correção no encerramento: `.gitignore`, `MulletaFlix-android/signing/SIGNING-KEY-ROTATION.md`, `tools/Prepare-AndroidDeveloperVerification.ps1`, `tools/Prepare-AndroidDeveloperVerification.Tests.ps1` e anexos em `.codex-remote-attachments/`. Preserve esses arquivos; não reverta nem inclua no escopo sem inspecionar autoria e intenção.

## Fable checkpoints

INTENT: Ao selecionar um livro no telefone/tablet, abrir EPUB no leitor nativo do APK em vez de iniciar o player de vídeo ou forçar paisagem.

TWINS: Busca no fluxo Android encontrou rota específica `BOOK_READER`; detalhes de Livro usam `ReadBook`, enquanto `videoPlayer(...)` restante atende outros fluxos. Repetir a busca antes de refatorar para evitar um segundo encaminhamento de Livro.
