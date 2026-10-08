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
| Regra pura, política, parser, ordenação ou estado | JVM no módulo dono | Property/boundary tests para entrada ampla |
| Coroutines, Flow, ViewModel, sessão ou persistência | JVM com dispatchers/fakes determinísticos | DataStore/Room instrumentado para integração Android real |
| HTTP, autenticação, retry ou contrato do cliente | JVM com servidor HTTP local e fixtures | Ambiente de servidor de teste autorizado para E2E |
| Compose, acessibilidade, navegação, rolagem ou foco | Compose UI test | PHONE/TABLET/TV conforme o comportamento |
| Activity, ciclo de vida, PiP, TTS, player, codecs ou APIs de plataforma | Instrumentação | Dispositivo físico quando hardware, engine ou saída acústica forem relevantes |
| Empacotamento e assinatura | Teste dos scripts/configuração e validação do artefato | Regras globais de release/publicação seguem instruções de nível superior; esta matriz define apenas a validação Android |

### Quality Gate completo

Execute na raiz `MulletaFlix-android`. Rode o lint do módulo alterado e do app:

```powershell
.\gradlew.bat testDebugUnitTest :feature:item-detail:lintDebug :app:lintDebug :app:assembleDebug --no-daemon --console=plain
```

Troque `:feature:item-detail:lintDebug` pelo módulo afetado. O Quality Gate local comprovou 1.515 testes JVM, zero falhas e erros em 2026-10-08. Esse número é um baseline de execução, não uma métrica de cobertura nem garantia de que todos os comportamentos existentes tenham sido auditados em RED-GREEN.

Para instrumentação, use `tools\with-emulator.ps1`, informe `expectedDeviceProfile` e selecione os testes relevantes. A suíte não depende de instalar uma release no aparelho. Perfis e comandos ficam na seção “Perfis de emulador para UX adaptativa”.

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
| Perfil de Usuário (`:feature:user`) | Carregamento de perfil, fallback de sessão offline, alternância rápida de usuário, limpeza de cache e logout | JVM |
| Detalhes do Item (`:feature:item-detail`) | Filmes, séries, temporadas/episódios, faixas de álbuns, favoritos, assistidos, playlists e erros; uma coluna em 411 dp e tablet retrato 600 dp, painel hero+detalhes rolável em 840 dp, TV preserva coluna única; hero compacta poster para 88 dp abaixo de 360 dp ou em fonte ≥1,5×, mantém capa 2:3 inteira e permite alcançar/acionar última ação após rolagem vertical e horizontal; leitores EPUB/CBZ retêm progresso isolado por servidor/conta/livro; CBZ lista páginas naturalmente e decodifica uma página limitada ao espaço visível | JVM + Compose instrumentado em telefone, tablet API 35 e TV API 34 |
| Tela do leitor EPUB (`:feature:item-detail`) | Compose + ViewModel + Retrofit + EPUB válido via fixture HTTP local + Readium; erro 503, retry, TOC aninhado, `<span>` de grupo sem ação e com semântica heading, salto para capítulo e texto confirmado no DOM do WebView, controles de navegação/tamanho de fonte e ação acessível para ouvir livro; recuperação de cache órfão EPUB/CBZ e proteção de arquivo ativo/não pertencente ao leitor | JVM + instrumentado PHONE/TABLET API 35; `BookReaderScreenIntegrationTest` 8/8 em cada perfil, 2026-10-08 |
| Leitura em voz alta (`:feature:item-detail`) | TTS nos trechos de texto paginado; narração Readium em EPUB inicia na posição visível, acompanha posição/locutor, usa idioma declarado no conteúdo, pausa em segundo plano e encerra em navegação manual; PDF extrai e narra somente texto selecionável, uma página por vez, avança páginas com texto e para ao navegar/ir para segundo plano; suporte do PDF depende de Android 15+ ou extensão S 13+ em Android 11–14; sem OCR para páginas digitalizadas | JVM + Compose instrumentado PHONE/TABLET API 35; TV API 34 confirma controle oculto quando extensão não suporta; cenários PDF validam texto, avanço e parada manual; não cobre saída acústica/idiomas instalados em aparelhos físicos nem TalkBack manual |
| Mapeamento de mídia (`:data`) | Tipo, imagens, 4K, HD, HDR, Dolby Vision, Atmos | JVM |
| Cache de imagens e playback | Verificar limpeza Coil em memória/disco e intervalo de 1 h; mídia de streaming online não é persistida; cache Media3 de downloads permanece após a limpeza; `TestDriver` simula o período do WorkManager | JVM + WorkManager/Coil/Media3 instrumentados; horário real do sistema não simulado |
| Cache offline & Downloads | Conversão entidade/domínio, progresso de reprodução, fila isolada por conta + servidor + mídia, persistência de série/temporada/episódio no `DownloadRequest.data`, retry seguro sem reassociar entradas legadas e seleção do próximo episódio completo no mesmo servidor | JVM + Media3 instrumentado |
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
| Seleção de servidor | Logo, URL, descoberta LAN por UDP em Wi-Fi/Ethernet, tentativa pelo socket sem vínculo apenas para destinos sem envio bem-sucedido em qualquer interface, preservação do socket simples quando não há rede local exposta; fechamento de sockets após falha de vínculo | JVM + Compose instrumentado |
| Quick Connect | Prazo monotônico de cinco minutos inclui latência; código continua aguardando resposta sem exibir contagem expirada | JVM + Compose instrumentado em celular, tablet e TV |
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

Testes de TV devem validar foco remoto, grade compacta e atualização em primeiro plano. Testes de tablet devem validar conteúdo centralizado, rolagem e capas retangulares sem aplicar a política de foco da TV.

`PlayerSeekControlsTest` cobre os saltos configuráveis de 5/10/15/30 segundos e seus rótulos acessíveis. `ChoiceDialogOptionsTest` cobre seleção do intervalo nos Ajustes. Execute ambos em PHONE, TABLET e TV com `tools\with-emulator.ps1`; a configuração fica local no DataStore e não exige servidor.
