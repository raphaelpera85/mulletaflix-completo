# MulletaFlix Android - Plano de Desenvolvimento & Checklist de Funcionalidades (TODO)

Este documento rastreia o status de implementação de todas as funcionalidades, módulos, telas e componentes do aplicativo oficial **MulletaFlix Android**.
O código completo do app está localizado em: [`MulletaFlix-android/`](file:///d:/Users/Raphael/Documents/Projetos/mulletaflix/MulletaFlix-android)

## Trabalho APK sem release — Acessibilidade da troca rápida de perfil

- [x] Opções de perfil anunciam semântica de botão e ação específica para alternar para o usuário; avatar decorativo não duplica o anúncio do nome.
- [x] Teste Compose verifica papel, rótulo e despacho do usuário selecionado; teste separado verifica solicitação de foco do componente.
- [x] Android TV: 5 testes instrumentados aprovados; tablet: teste instrumentado de semântica/interação por toque aprovado. O foco remoto ponta a ponta/D-pad não é comprovado por esses testes.
- [x] `testDebugUnitTest` concluiu com `BUILD SUCCESSFUL`; `:app:lintDebug` concluiu isolado com `BUILD SUCCESSFUL` após falha interna transitória do analisador quando combinado com a suíte.
- [x] `:app:assembleRelease` concluiu com `BUILD SUCCESSFUL`; APK local v1.3.55 (`versionCode=356`), 7.421.251 bytes, SHA-256 `EEC9EB091E725B88A12D6D09D29F088CF9EEF13B3845D44D7C6F7F9E7A522773`.
- [ ] Nenhuma nova versão ou release oficial criada nesta tarefa; notas da futura release devem descrever somente essas melhorias após validação do APK correspondente.

---

## Trabalho APK sem release — Restauração de áudio e legendas após reconstrução da fonte

- [x] Manter a restauração pendente quando o player ainda não expõe grupos de faixas, desde que os metadados do servidor continuem anunciando a faixa escolhida.
- [x] Encerrar a tentativa quando a faixa for selecionada ou deixar de constar nos metadados; cobrir retry e os dois casos de encerramento com testes unitários.
- [x] Gates APK: `testDebugUnitTest` (1.057 testes, 0 falhas/erros/skips), `:app:lintDebug` e `:app:assembleRelease` — todos `BUILD SUCCESSFUL`.
- [x] APK local de validação sem bump/publicação: v1.3.55 (`versionCode=356`), 7.421.251 bytes, SHA-256 `4C231325210A9C82A39494E0DEF048977446E5E4ACE86463CA08F1A9FC2DA9A6`.
- [ ] Nenhuma release oficial foi criada. Quando autorizada, as notas devem espelhar precisamente as melhorias e correções incluídas e validadas no APK.

---

## Trabalho APK v1.3.55 — Cache offline durável e comandos SyncPlay consistentes

- [x] Um comando SyncPlay novo invalida o comando agendado anterior; o player revalida sala, mídia e geração antes de aplicar.
- [x] Atualização da fila invalida comandos pendentes da faixa anterior.
- [x] Cache dos downloads offline movido de `cacheDir` removível pelo sistema para `noBackupFilesDir` persistente, com migração dos arquivos existentes e fallback não destrutivo.
- [x] Se pastas antiga e persistente coexistirem, arquivos sem conflito são mesclados sem sobrescrita; conflitos mantêm a pasta antiga ativa e preservam ambas as cópias.
- [x] O player continua lendo downloads do cache, mas streams não baixados não são gravados nele.
- [x] Testes cobrem migração, cache persistente existente, conflito, falha de migração e a configuração somente leitura do player.
- [x] Teste instrumentado executa o `CacheDataSource` real: lê bytes baixados sem tráfego upstream e reproduz stream sem gravar bytes no cache.
- [x] Gate da rodada anterior: `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` concluídos com `BUILD SUCCESSFUL`; 980 testes, 0 falhas, 0 ignorados.
- [x] APK intermediário da rodada anterior: `dist/interim-v1.3.55/mulletaflix-app-v1.3.55.apk`, 7.372.099 bytes, SHA-256 `FD9ED53B0CEA115F7EA87E160D7CC0EF398164AD3E461DB0F241EF8C99EDAB10`.
- [x] Gate após extrair a factory e adicionar o teste instrumentado do cache: `testDebugUnitTest` (980 testes, 0 falhas, 0 ignorados), `:app:lintDebug`, `:app:assembleRelease` e suíte Android TV concluídos com `BUILD SUCCESSFUL`.
- [x] O APK v1.3.55 que já existia foi verificado (7.372.099 bytes, SHA-256 `BDB61D2BF10650043C30E9A017B692FBFBAD22B28C010D9707E47A8ADF35B092`) e não foi sobrescrito.
- [ ] Publicação remota: não feita; versões intermediárias seguem a cadência de release do APK.

---

## Release v1.3.54 — Eventos SyncPlay limitados à sala ativa (APK)

- [x] O APK agora descarta comandos, atualizações da sala e da fila cujo `groupId` não corresponde à sala ativa; eventos de conexão/desconexão também são ignorados sem uma sala ativa.
- [x] Testes unitários cobrem eventos da sala correta, de outra sala e recebidos sem sala ativa.
- [x] Gate Android: `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` concluídos com `BUILD SUCCESSFUL`.
- [x] APK local: `dist/mulletaflix-app-v1.3.54.apk`, 7.372.095 bytes, SHA-256 `9AD33AB2132E8FBF32C0E7F55AB799DC1704B365920A2ED6C461614C6524EC9C`.
- [ ] Publicação remota da release do APK: mantida pendente nesta conversa APK-only.

---

## Release v1.3.53 — Teste instrumentado para notificações do SyncPlay (APK)

- [x] O efeito de Snackbar do SyncPlay foi isolado em componente testável, mantendo a mensagem persistente nos controles da sala.
- [x] Teste instrumentado confirma no Android TV que eventos repetidos com a mesma mensagem continuam visíveis.
- [x] Gate Android: `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` concluídos com `BUILD SUCCESSFUL`.
- [x] APK local: `dist/mulletaflix-app-v1.3.53.apk`, 7.372.099 bytes, SHA-256 `0AFFDEBDC32FA19A8964C659DD10552309E6550A716C1E713829234000B7B505`.
- [ ] Publicação remota da release do APK: mantida pendente nesta conversa APK-only.

---

## Release v1.3.52 — Feedback visual de eventos do SyncPlay (APK)

- [x] Eventos de conexão, desconexão, comandos, sala e fila agora geram notificações transitórias visíveis no APK.
- [x] Cada evento possui sequência própria, então eventos consecutivos com a mesma mensagem não são descartados pela UI.
- [x] O estado persistente continua mostrando o último evento nos controles da sala, enquanto a Snackbar fornece confirmação imediata.
- [x] Teste unitário cobre as mensagens de eventos em tempo real.
- [x] Gate Android: `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` concluídos com `BUILD SUCCESSFUL`.
- [x] APK local: `dist/mulletaflix-app-v1.3.52.apk`, 7.372.099 bytes, SHA-256 `1E01A68D925F1D0DD2C185C592FBE2CDC07D52EAE4E3F0D3295FA6368BEDAD99`.
- [ ] Publicação remota da release do APK: mantida pendente nesta conversa APK-only.

---

## Release v1.3.51 — Estado consistente ao trocar automaticamente para a LAN (APK)

- [x] A descoberta LAN invalida usuários e Quick Connect do endpoint anterior antes de concluir a verificação do novo servidor.
- [x] O carregamento persistido do endpoint público é ignorado quando a descoberta já selecionou um endereço local diferente, evitando mistura de estados.
- [x] Teste unitário cobre a transição público → LAN e confirma que o seletor de usuários e Quick Connect não exibem dados antigos.
- [x] Android TV: 7/7 testes instrumentados de autenticação aprovados, sem skips ou falhas; emulador encerrado automaticamente.
- [x] Gate Android: `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` concluídos com `BUILD SUCCESSFUL`.
- [x] APK local: `dist/mulletaflix-app-v1.3.51.apk`, 7.369.451 bytes, SHA-256 `A7F9D2B526420D15757ACC62DD31622B67EE9A75A276874011A5601EBCF56461`.
- [ ] Publicação remota da release do APK: mantida pendente nesta conversa APK-only.

---

## Release v1.3.50 — Cobertura do retry de disponibilidade do Quick Connect (APK)

- [x] O teste unitário do `AuthViewModel` cobre falha de `QuickConnect/Enabled`, mensagem acionável, retry e recuperação para disponibilidade confirmada.
- [x] A cobertura instrumentada continua validando os estados de carregamento, erro com retry e disponibilidade autorizada na Android TV.
- [x] Gate Android: `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` concluídos com `BUILD SUCCESSFUL`.
- [x] APK local: `dist/mulletaflix-app-v1.3.50.apk`, 7.369.451 bytes, SHA-256 `535524D2DAC96442DBE6B7441E88FE69BAA40B51E6ECF47611723BC9DAACAFA9`.
- [ ] Publicação remota da release do APK: mantida pendente nesta conversa APK-only.

---

## Release v1.3.49 — Retry acionável para falha de disponibilidade do Quick Connect (APK)

- [x] Falhas de timeout/rede ao consultar `QuickConnect/Enabled` deixam de prender a tela indefinidamente em “Verificando”.
- [x] A tela exibe a mensagem acionável retornada pela política de conexão e o botão focável `Tentar novamente`.
- [x] O retry reinicia a verificação no servidor atualmente selecionado e invalida o estado anterior.
- [x] Teste instrumentado cobre a mensagem de erro e a ação de retry, além dos estados desconhecido e disponível.
- [x] Gate Android: `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` concluídos com `BUILD SUCCESSFUL`.
- [x] Android TV: 7/7 testes instrumentados de autenticação aprovados, sem skips ou falhas; emulador encerrado automaticamente.
- [x] APK local: `dist/mulletaflix-app-v1.3.49.apk`, 7.369.451 bytes, SHA-256 `A7596996CF623877C54A2581AAC5DD694BFA94AEA96CB4CE61E0E19111713E85`.
- [ ] Publicação remota da release do APK: mantida pendente nesta conversa APK-only.

---

## Release v1.3.48 — Quick Connect aguarda a disponibilidade real do servidor (APK)

- [x] O botão de geração não aparece enquanto `QuickConnect/Enabled` ainda está sendo verificado.
- [x] A tela exibe estado explícito `Verificando Quick Connect…`, evitando iniciar a operação antes da resposta do servidor.
- [x] O fluxo desativado continua orientando o usuário para login com usuário e senha.
- [x] Teste instrumentado cobre a ausência do botão durante o estado desconhecido; o teste de início autorizado continua coberto.
- [x] Gate Android: `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` concluídos com `BUILD SUCCESSFUL`.
- [x] Android TV: 6/6 testes instrumentados de autenticação aprovados, sem skips ou falhas; emulador encerrado automaticamente.
- [x] APK local: `dist/mulletaflix-app-v1.3.48.apk`, 7.369.451 bytes, SHA-256 `A14D76317B408B4A60269F65B1B1DE4DD7B87B0795DEF086C4BC45193FF5357E`.
- [ ] Publicação remota da release do APK: mantida pendente nesta conversa APK-only.

---

## Release v1.3.47 — Correção de micro-seeks nas atualizações de fila SyncPlay (APK)

- [x] Atualizações de fila com diferença de até 750 ms não forçam seek, reduzindo microtravadas durante a reprodução.
- [x] Drift acima da tolerância continua corrigido para a posição autoritativa do servidor.
- [x] Comandos explícitos `Pause`, `Unpause` e `Seek` continuam usando a posição autoritativa recebida.
- [x] Teste unitário cobre drift pequeno, drift significativo e posição negativa.
- [x] Gate Android: `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` concluídos com `BUILD SUCCESSFUL`.
- [x] Android TV: 15/15 testes instrumentados aprovados na Biblioteca; SyncPlay reportou 0 testes; emulador encerrado automaticamente.
- [x] APK local: `dist/mulletaflix-app-v1.3.47.apk`, 7.369.451 bytes, SHA-256 `592D64DFD0C04DC0BD8E5B8CBC036ED5AAB28FCAD2D3E4CAFC61CA939C630E46`.
- [ ] Publicação remota da release do APK: mantida pendente nesta conversa APK-only.

---

## Release v1.3.46 — Estado visual da reconexão SyncPlay no player (APK)

- [x] O player exibe aviso não bloqueante quando o SyncPlay perde a conexão e tenta reconectar.
- [x] O aviso não aparece para mídia offline nem para sessões SyncPlay conectadas, evitando ruído visual.
- [x] O layout usa espaçamento próprio quando o aviso offline também está visível e permanece compatível com TV/D-pad.
- [x] Teste unitário cobre as mensagens dos estados `NONE`, `CONNECTED` e `RECONNECTING`.
- [x] Gate Android: `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` concluídos com `BUILD SUCCESSFUL`.
- [x] Android TV: 15/15 testes instrumentados aprovados na Biblioteca; SyncPlay reportou 0 testes; emulador encerrado automaticamente.
- [x] APK local: `dist/mulletaflix-app-v1.3.46.apk`, 7.369.451 bytes, SHA-256 `3657713368B1447CC46854929F2BA9A93913BCC99532841ECCA62E2BEA4BAC30`.
- [ ] Publicação remota da release do APK: mantida pendente nesta conversa APK-only.

---

## Release v1.3.45 — Proteção contra callbacks obsoletos do SyncPlay (APK)

- [x] Mensagens recebidas por um WebSocket substituído durante reconexão ou troca de sala agora são descartadas antes de alcançar o player.
- [x] A mesma validação de identidade foi aplicada aos callbacks de abertura, mensagem, fechamento e falha.
- [x] Teste unitário cobre socket ativo, socket antigo e callback sem conexão ativa.
- [x] Gate Android: `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` concluídos com `BUILD SUCCESSFUL`.
- [x] Android TV: 15/15 testes instrumentados aprovados na Biblioteca; SyncPlay reportou 0 testes; emulador encerrado automaticamente.
- [x] APK local: `dist/mulletaflix-app-v1.3.45.apk`, 7.369.451 bytes, SHA-256 `4B2A2AD74A36059423C69E56FA4680ABD5D1D7EFFEC76F2EA1464CEA0FBEA113`.
- [ ] Publicação remota da release do APK: mantida pendente nesta conversa APK-only.

---

## Release v1.3.44 — Posicionamento autoritativo nos comandos SyncPlay (APK)

- [x] `Pause` e `Unpause` agora aplicam `PositionTicks` do servidor antes de alterar o estado do player, evitando divergência após comandos remotos.
- [x] `Stop` continua sem forçar uma posição recebida, preservando o contrato semântico do comando.
- [x] Teste unitário cobre a conversão de posição para `Pause`/`Unpause` e a ausência de seek em `Stop`.
- [x] Gate Android: `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` concluídos com `BUILD SUCCESSFUL`.
- [x] Android TV: 15/15 testes instrumentados aprovados na Biblioteca; SyncPlay reportou 0 testes; emulador encerrado automaticamente.
- [x] APK local: `dist/mulletaflix-app-v1.3.44.apk`, 7.369.451 bytes, SHA-256 `71192BE76FB02E4EA3541E4FDDE52DCB2EF8815FBD7AB4EB692368775E354DD6`.
- [ ] Publicação remota da release do APK: mantida pendente nesta conversa APK-only.

---

## Release v1.3.43 — Reconexão resiliente do SyncPlay (APK)

- [x] WebSocket SyncPlay reconecta automaticamente após falha ou fechamento inesperado enquanto a sala continua ativa.
- [x] Backoff limitado de 1, 2, 4 e 8 segundos evita tempestade de conexões durante perda de Wi‑Fi/Internet.
- [x] Saída da sala cancela a reconexão pendente e impede que uma conexão antiga volte a controlar o player.
- [x] Testes unitários cobrem os limites da política de backoff, incluindo tentativas negativas e tentativas prolongadas.
- [x] Gate Android: `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` concluídos com `BUILD SUCCESSFUL`.
- [x] Android TV: 15/15 testes instrumentados aprovados na Biblioteca; SyncPlay reportou 0 testes; emulador encerrado automaticamente.
- [x] APK local: `dist/mulletaflix-app-v1.3.43.apk`, 7.369.451 bytes, SHA-256 `CC13834674027F4D3313D649FCAEA986B6244C22AF0CCE0FCECDA3BB265ADED9`.
- [ ] Publicação remota da release do APK: mantida pendente nesta conversa APK-only.

---

## Release v1.3.42 — Aplicação de comandos SyncPlay no player (APK)

- [x] Contrato WebSocket atualizado com `PlaylistItemId`, `ItemId`, `PositionTicks`, `When` e estado da fila conforme o servidor.
- [x] Player aplica automaticamente pausa, retomada, parada e busca recebidas da sala ativa, respeitando a mídia e o grupo atuais.
- [x] Atualizações de fila trocando a mídia atual são aplicadas ao player com posição e estado de reprodução sincronizados.
- [x] Comandos de outra sala, outra mídia ou sem identificação de playlist são descartados para evitar controles indevidos.
- [x] Testes unitários cobrem parser de comandos/atualização de fila e política de filtragem/posição.
- [x] Gate Android: `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` concluídos com `BUILD SUCCESSFUL`.
- [x] Android TV: 15/15 testes instrumentados aprovados na Biblioteca; SyncPlay reportou 0 testes; emulador encerrado automaticamente.
- [x] APK local: `dist/mulletaflix-app-v1.3.42.apk`, 7.369.451 bytes, SHA-256 `FBB62AF1042548A8C7176B780970597F2147C43D607208257700EC8B37B3482A`.
- [ ] Publicação remota da release do APK: mantida pendente nesta conversa APK-only.

---

## Release v1.3.41 — SyncPlay em tempo real (APK)

- [x] Cliente WebSocket autenticado no endpoint `/socket`, usando `api_key` e `deviceId` da sessão.
- [x] Parser coberto por testes para `SyncPlayCommand` e `SyncPlayGroupUpdate`; mensagens não relacionadas são ignoradas.
- [x] Ciclo de vida da conexão ligado à entrada/saída da sala SyncPlay, com estado de conexão e último evento visíveis na UI.
- [x] Gate Android executado com `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` (`BUILD SUCCESSFUL`).
- [x] Android TV: 15 testes instrumentados, 15 aprovados, 0 falhas e 0 ignorados; emulador encerrado automaticamente.
- [x] APK local gerado: `dist/mulletaflix-app-v1.3.41.apk`, 7.353.067 bytes, SHA-256 `BB29FA73BBA9D1AEE38D512F148CC087A3165F17CA0570EB680FFD0E5C35D54F`.
- [x] Aplicar comandos remotos automaticamente ao player local (concluído na v1.3.42).

---

## 🏛️ 1. Arquitetura & Infraestrutura (Clean Architecture + Multi-module)

- [x] **Configuração Gradle & Version Catalog (`libs.versions.toml`)**
  - [x] AGP 8.7.3, Kotlin 2.0.21, Compose BOM 2024.12.01, Media3 1.5.0, Hilt 2.53.1, Room 2.6.1, Retrofit 2.11.0, Moshi 1.15.2, Ktor Client
- [x] **Divisão Modular**
  - [x] `:app` (Orquestração, Navigation, Services, Splash, Cast Options)
  - [x] `:core:common` (Result, Dispatchers, Extensions, NetworkMonitor)
  - [x] `:core:api` (Retrofit, Ktor, DTOs, Interceptors, WebSocket)
  - [x] `:domain` (Modelos puros, Interfaces de Repositórios, UseCases)
  - [x] `:data` (Room DB, DataStore, Mappers, Implementação dos Repositórios)
  - [x] `:design-system` (Tokens, Cores, Temas, Tipografia Noto Sans, Componentes)
  - [x] `:feature:auth`
  - [x] `:feature:home`
  - [x] `:feature:library`
  - [x] `:feature:item-detail`
  - [x] `:feature:player`
  - [x] `:feature:search`
  - [x] `:feature:downloads`
  - [x] `:feature:live-tv`
  - [x] `:feature:user`
  - [x] `:feature:sync-play`
  - [x] `:feature:settings`

---

## 🔐 2. Autenticação & Sessão (`:feature:auth`)

- [x] **Seleção e Conexão de Servidor**
  - [x] Descoberta local de servidores (SSDP / UDP broadcast)
  - [x] Entrada manual de URL (HTTP/HTTPS com validação de certificados locais/IPs privados)
  - [x] Histórico de servidores conectados e status de latência
- [x] **Autenticação de Usuário**
  - [x] Login por Nome de Usuário e Senha
  - [x] Quick Connect (Código de 6 dígitos para autenticação rápida em smart devices/navegadores)
  - [x] Seleção visual de usuários com avatares em servidores públicos
  - [x] Armazenamento seguro de token (`MediaBrowser Token="..."`) via EncryptedDataStore
  - [x] Renovação e interceptor de autenticação em todas as requisições (`AuthInterceptor`)

---

## 🏠 3. Home & Descoberta (`:feature:home`)

- [x] **Hero Banner Dinâmico**
  - [x] Destaques em carrossel animado com backdrop de alta resolução
  - [x] Ações rápidas: Assistir Agora, Adicionar à Minha Lista, Detalhes
- [x] **Fileiras de Mídia (Horizontal Rows)**
  - [x] Continue Assistindo (com barra de progresso e porcentagem precisa)
  - [x] Próximos Episódios (Next Up para séries em andamento)
  - [x] Adicionados Recentemente (Filmes, Séries, Álbuns)
  - [x] Recomendações personalizadas baseadas no histórico
  - [x] Bibliotecas Principais (Atalhos rápidos para Filmes, Séries, Músicas, Live TV)
- [x] **Atualização e Cache**
  - [x] Pull-to-refresh com feedback tátil
  - [x] Carregamento paralelo não-bloqueante via Coroutines (`HomeViewModel`)

---

## 📚 4. Navegação por Bibliotecas (`:feature:library`)

- [x] **Visualização e Layouts**
  - [x] Alternância entre Grade (Grid) e Lista (List)
  - [x] Paginação infinita fluida com pre-fetching
- [x] **Filtros e Ordenação**
  - [x] Ordenação por Nome, Data de Adição, Ano de Lançamento, Nota da Crítica, Duração
  - [x] Filtros por Gênero, Ano, Classificação Indicativa, Status de Reprodução (Assistido/Não Assistido)
  - [x] Filtro alfabético rápido (A-Z jump bar)

---

## 🎬 5. Detalhes do Item (`:feature:item-detail`)

- [x] **Apresentação Multimídia**
  - [x] Suporte adaptativo para Filmes, Séries, Episódios, Álbuns de Música e Livros
  - [x] Pôster, Logo oficial em alta resolução, Backdrop cinematográfico e Trilha sonora temática (theme song)
- [x] **Metadados Completos**
  - [x] Sinopse com expansão de texto
  - [x] Classificação indicativa, Ano, Duração, Resolução (4K, HDR, Dolby Vision, 1080p), Codecs de áudio
  - [x] Elenco e Equipe técnica com fotos e navegação por ator
  - [x] Itens Similares recomendados
- [x] **Séries & Temporadas**
  - [x] Seletor de temporadas em abas/dropdown
  - [x] Lista de episódios com thumbnails, sinopses e indicador de progresso
- [x] **Ações do Usuário**
  - [x] Marcar como Assistido/Não Assistido
  - [x] Favoritar / Desfavoritar
  - [x] Download para reprodução offline

---

## ▶️ 6. Player Avançado de Vídeo & Áudio (`:feature:player`)

- [x] **Motor de Reprodução (ExoPlayer Media3)**
  - [x] Streaming HLS, DASH e Direct Play com fallback automático para Transcoding
  - [x] Picture-in-Picture (PiP) automático ao sair do app
  - [x] Suporte a rotação automática e bloqueio de orientação da tela
  - [x] Controle de brilho e volume por gestos verticais nas laterais da tela
- [x] **Interface OSD (On-Screen Display)**
  - [x] Controles modernos de play/pause, avançar/retroceder 10s
  - [x] Barra de progresso com visualização de capítulos e thumbnails de busca
  - [x] Botões "Pular Introdução" (Skip Intro) e "Pular Créditos" (Skip Credits)
- [x] **Seleção de Faixas & Qualidade**
  - [x] Seleção de faixas de áudio (Dolby Atmos, 5.1, Estéreo, idiomas secundários)
  - [x] Seleção de legendas (embutidas e externas via OpenSubtitles / SRT / VTT)
  - [x] Ajuste de velocidade de reprodução (0.5x até 2.0x)
  - [x] Ajuste manual de taxa de bits / resolução máxima
- [x] **Transmissão Externa**
  - [x] Suporte nativo a Google Cast (Chromecast) integrado
- [x] **Sincronização com o Servidor**
  - [x] Relatórios periódicos de progresso (a cada 5 segundos) para manter o progresso exato no servidor

---

## 🔍 7. Busca Global Instantânea (`:feature:search`)

- [x] **Mecanismo de Busca em Tempo Real**
  - [x] Busca instantânea com debounce de digitação
  - [x] Chips de filtro rápido por tipo de mídia (Filmes, Séries, Músicas, Pessoas)
  - [x] Histórico de buscas recentes com remoção individual e limpeza total
  - [x] Resultados agrupados por categoria com navegação direta

---

## 💾 8. Downloads & Reprodução Offline (`:feature:downloads`)

- [x] **Gerenciador de Downloads**
  - [x] Integração com `Media3 DownloadService` para downloads estáveis em segundo plano
  - [x] Notificação de progresso persistente com pausa e cancelamento
  - [x] Fila de downloads priorizada com controle de Wi-Fi apenas
- [x] **Armazenamento Local & Banco de Dados**
  - [x] Registro Room para itens baixados e verificação de integridade
  - [x] Reprodução offline transparente no Player sem necessidade de internet

---

## 📺 9. Live TV & Guia de Programação (EPG) (`:feature:live-tv`)

- [x] **Canais Ao Vivo**
  - [x] Lista de canais com logos oficiais e programas atuais
  - [x] Sintonização instantânea de stream de TV
- [x] **Guia Eletrônico de Programação (EPG)**
  - [x] Grade de horários por canal com navegação temporal
  - [x] Detalhes do programa ao vivo e sinopse
  - [x] Ação para agendar gravações (DVR) no servidor

---

## 👥 10. SyncPlay (Sessões Sincronizadas) (`:feature:sync-play`)

- [x] **Salas de Sincronização**
  - [x] Criação, listagem, entrada e saída via endpoints REST oficiais do MulletaFlix
  - [x] Comandos remotos de Pausar, Retomar e Parar via endpoints REST oficiais
  - [x] Lista de participantes retornada pelo servidor
  - [x] Listener WebSocket para aplicar automaticamente no player local as ações iniciadas por outros participantes
  - [x] Sincronização de Play/Seek/Stop e troca de mídia em tempo real no player Android
  - [ ] Sincronização fina de buffering/latência e notificações visuais detalhadas em tempo real

---

## 🎨 11. Design System, Temas & Visual (`:design-system`)

- [x] **Paleta Oficial MulletaFlix**
  - [x] Preto como cor primária (`#0F0F0F`), vermelho de destaque (`#E50914`) e fundos cinematográficos quase pretos
  - [x] Tipografia oficial Noto Sans
- [x] **Suporte aos 8 Temas do MulletaFlix Web**
  - [x] Padrão Escuro (Dark)
  - [x] Padrão Claro (Light)
  - [x] Tema Netflix (Vermelho vibrante)
  - [x] Tema Purple Haze (Roxo neon)
  - [x] Tema Blue Radiance (Azul ciano moderno)
  - [x] Tema Windows Media Center (WMC)
  - [x] Tema Apple TV (Minimalista translúcido)
  - [x] Dynamic Color (Material You no Android 12+)
- [x] **Componentes Reutilizáveis**
  - [x] `MediaCard` (Modos Portrait, Landscape, Square e Banner com badges 4K/HDR e progresso)
  - [x] `MulletaFlixTopAppBar` e `MulletaFlixNavigationBar`
- [x] Estados de Loading com Shimmer Effect e Tratamento de Erros amigável

## 54. Filtros de downloads offline (v1.0.43)
- [x] Filtrar a fila por todos, downloads em andamento, concluídos e falhos.
- [x] Combinar o filtro de status com a busca por título sem alterar a ordem original.
- [x] Cobrir a seleção do filtro na UI e a combinação de status com busca nos testes.

## 55. Resumo do armazenamento offline (v1.0.44)
- [x] Exibir no APK os bytes baixados e o tamanho total conhecido pelo Media3.
- [x] Informar claramente quando o servidor ainda não forneceu o tamanho total.
- [x] Cobrir a agregação dos dados de armazenamento em teste unitário.

## 56. Menus de faixas roláveis (v1.0.45)
- [x] Permitir rolagem vertical em listas longas de áudio e legendas.
- [x] Cobrir uma lista longa no teste instrumentado do player.

---

## ⚙️ 12. Configurações & Perfil (`:feature:settings` & `:feature:user`)

- [x] **Configurações Gerais**
  - [x] Seletor interativo de temas em tempo real
  - [x] Configuração de qualidade padrão de streaming e downloads
  - [x] Preferências de áudio e legendas padrão
  - [x] Gerenciamento de cache e limpeza de dados
- [x] **Perfil de Usuário**
  - [x] Exibição de foto de perfil e permissões
  - [x] Alternância rápida entre usuários cadastrados no servidor
- [x] Ação de logout seguro com limpeza de sessão

## 169. Downloads offline isolados por conta (v1.1.61)

- [x] Escopar IDs de requisição do Media3 pelo usuário ativo.
- [x] Armazenar proprietário e ID público da mídia nos metadados locais.
- [x] Exibir, remover e limpar somente downloads da conta atual.
- [x] Cobrir identidade e filtragem por usuário com testes unitários.

## 170. Feedback de falhas nas ações offline (v1.1.62)

- [x] Expor falhas de retry, remoção, limpeza, pausa, retomada e Wi-Fi somente.
- [x] Exibir a mensagem de erro em Snackbar sem interromper a tela de downloads.
- [x] Permitir limpar o aviso após a apresentação e cobrir erros em testes de ViewModel.

## 171. Mensagens de falha dos downloads (v1.1.63)

- [x] Converter códigos de falha do Media3 em mensagens de retry amigáveis.
- [x] Preservar o código técnico apenas como diagnóstico complementar.
- [x] Cobrir ausência de falha, falha desconhecida e códigos inesperados em testes unitários.

## 172. Feedback da limpeza total local (v1.1.64)

- [x] Informar sucesso ou falha da limpeza total de dados locais.
- [x] Preservar downloads offline durante a limpeza geral.
- [x] Só concluir a ação após limpar preferências e encerrar a sessão.
- [x] Cobrir o fluxo completo em teste de ViewModel.

## 173. Posições offline isoladas por conta (v1.1.65)

- [x] Escopar a retomada de downloads pela conta ativa.
- [x] Migrar uma posição legada sem expor a posição entre usuários.
- [x] Limpar chaves escopadas e legadas ao concluir a reprodução.
- [x] Cobrir a separação entre usuários em teste unitário.
## 174. Inicialização robusta da reprodução offline (v1.1.66)
- [x] Usar a sessão persistida quando o observador de autenticação ainda não emitiu o usuário.
- [x] Abortar a preparação se a conta mudar enquanto a sessão é resolvida.
- [x] Cobrir prioridade, fallback e ausência de usuário em testes unitários.
## 175. Autoridade da sessão offline durante troca de conta (v1.1.67)
- [x] Priorizar a sessão persistida quando o observador ainda contém a conta anterior.
- [x] Usar o usuário observado somente como fallback durante o bootstrap.
- [x] Cobrir a condição de corrida entre cache e sessão persistida em teste unitário.
## 176. Espaço real para downloads offline (v1.1.68)
- [x] Substituir o limite estático de 10 GB pelo espaço utilizável real do cache de downloads.
- [x] Atualizar o indicador periodicamente sem bloquear a interface.
- [x] Cobrir arredondamento e formatação do espaço disponível em teste unitário.
## 177. Feedback das ações no detalhe (v1.1.69)
- [x] Informar sucesso ao favoritar ou marcar uma mídia como assistida.
- [x] Informar falha do servidor e preservar o rollback do estado otimista.
- [x] Cobrir rollback e mensagem acionável em teste de ViewModel.
## 178. Links de compartilhamento públicos (v1.1.70)
- [x] Evitar que o compartilhamento do APK gere links de localhost ou loopback.
- [x] Usar a URL pública oficial somente para hosts locais.
- [x] Preservar URLs reais de LAN/internet e cobrir a normalização em teste.
## 179. Busca resiliente durante refresh (v1.1.71)
- [x] Preservar resultados válidos quando uma atualização falhar.
- [x] Exibir aviso acionável sem remover a navegação existente.
- [x] Cobrir falha de refresh e preservação dos resultados em teste.
## 180. Retry de streaming com posição (v1.1.72)
- [x] Repetir a fonte remota já preparada sem recarregar toda a mídia.
- [x] Preservar a posição conhecida no momento da falha.
- [x] Cobrir a política de posição do retry em teste unitário.
## 181. Recuperação de rede com posição (v1.1.73)
- [x] Usar a maior posição conhecida quando a rede retornar após uma falha.
- [x] Evitar que o reset interno do player retome o streaming do início.
- [x] Cobrir o cenário de posição zerada após erro em teste unitário.
## 182. Deep link consumível (v1.1.74)
- [x] Abrir links de mídia recebidos somente uma vez por destino.
- [x] Preservar a abertura do link após o login sem reabrir ao voltar para a Home.
- [x] Permitir que um novo link substitua o link já tratado.
- [x] Cobrir a política de consumo do deep link em testes unitários.
## 183. Recuperação LAN ao perder a rede (v1.1.75)
- [x] Reexecutar a descoberta quando uma rede disponível for perdida.
- [x] Evitar manter um endereço privado obsoleto durante a sessão.
- [x] Voltar automaticamente à URL pública quando o servidor LAN desaparecer.
- [x] Cobrir o fallback LAN com variações de URL em teste unitário.

## 184. Semântica completa nos cards de mídia (v1.1.76)
- [x] Anunciar ao TalkBack os estados ao vivo, assistido e Minha Lista.
- [x] Expor qualidade, episódios pendentes e progresso de reprodução.
- [x] Evitar anunciar progresso obsoleto para itens já assistidos.
- [x] Cobrir os rótulos acessíveis com testes unitários.

## 185. Retry seguro da API em oscilações transitórias (v1.1.77)
- [x] Repetir somente requisições de leitura idempotentes.
- [x] Cobrir respostas 408, 425, 429 e 5xx com orçamento limitado.
- [x] Respeitar `Retry-After` sem bloquear por períodos excessivos.
- [x] Não repetir login, progresso ou comandos de alteração.
- [x] Cobrir a política de retry com testes unitários.

## 186. Recuperação automática da TV ao vivo após rede (v1.1.78)

- [x] Atualizar o catálogo de canais quando a conectividade voltar.
- [x] Exibir aviso não bloqueante enquanto o dispositivo estiver offline.
- [x] Evitar refresh duplicado na emissão inicial ou em estados inalterados.
- [x] Cobrir a transição offline/online com testes unitários.

## 187. Preservação de faixas no retry do player (v1.1.79)

- [x] Preservar a seleção manual de áudio durante a reconstrução das faixas.
- [x] Preservar a seleção manual de legenda durante retry e reconexão.
- [x] Manter legendas explicitamente desativadas após a recuperação.
- [x] Cobrir a política de recuperação de faixas com testes unitários.

## 188. Idiomas legíveis nas faixas do player (v1.1.80)

- [x] Converter códigos de idioma do servidor em nomes compreensíveis.
- [x] Cobrir variantes como `pt-BR`, `por`, `eng` e `en-US`.
- [x] Preservar títulos explícitos enviados pelo servidor.
- [x] Cobrir a apresentação de idiomas com testes unitários.
## 189. Fallback LAN resiliente a perda transitória (v1.1.81)
- [x] Evitar troca imediata para a URL pública após uma única descoberta sem resposta.
- [x] Exigir duas ausências consecutivas antes de abandonar um endpoint privado ativo.
- [x] Resetar o contador assim que o servidor LAN autenticado voltar a responder.
- [x] Cobrir a política com teste unitário.

## 190. Descoberta LAN por interface local (v1.1.82)
- [x] Vincular probes UDP às redes Wi-Fi e Ethernet locais elegíveis.
- [x] Manter fallback compatível para dispositivos sem rede local exposta pelo sistema.
- [x] Coletar respostas de múltiplas interfaces sem duplicar servidores.
- [x] Cobrir o orçamento de retries da varredura com teste unitário.

## 191. Qualidade adaptativa para rede medida (v1.1.83)
- [x] Detectar o estado de rede medida pelo Android.
- [x] Limitar o modo automático a 720p em rede medida.
- [x] Preservar escolhas manuais de qualidade do usuário.
- [x] Cobrir a política com testes unitários.

## 192. Transparência da qualidade adaptativa (v1.1.84)
- [x] Expor no estado do player quando a rede atual é medida.
- [x] Informar no menu de qualidade que o modo Auto está limitado a 720p.
- [x] Manter as escolhas manuais sem alteração.
- [x] Cobrir os rótulos da qualidade adaptativa com teste unitário.

## 193. UX adaptativa para celular, tablet e TV (v1.1.85)
- [x] Definir contratos de layout por família de dispositivo.
- [x] Centralizar e limitar o conteúdo em tablets.
- [x] Ampliar artes e espaçamento para navegação por foco na TV.
- [x] Criar testes unitários e testes de uso Compose para as três superfícies.

## 194. Grade e atualização automática na Android TV (v1.1.86)
- [x] Reduzir o tamanho mínimo das capas na TV para exibir mais títulos por linha.
- [x] Usar colunas previsíveis e amigáveis ao controle remoto na TV.
- [x] Atualizar a biblioteca automaticamente enquanto a tela estiver em primeiro plano.
- [x] Cobrir a densidade e o cálculo de colunas com testes unitários.

## 195. Foco visual para navegação por controle remoto (v1.1.87)
- [x] Exibir escala e borda vermelha ao focar cards na Android TV.
- [x] Aplicar o tratamento de foco nas seções da Home e na grade da biblioteca.
- [x] Preservar a geometria de cards em celular e tablet.
- [x] Cobrir a política de escala e borda com teste unitário.

## 196. Rótulos de qualidade fornecidos pelo servidor (v1.1.87)
- [x] Preservar nomes de qualidade desconhecidos no menu do player.
- [x] Continuar normalizando resoluções conhecidas e aliases de Auto.
- [x] Manter o aviso de rede medida somente no modo Auto.
- [x] Cobrir a apresentação dos rótulos com teste unitário e instrumentado.

## 197. Foco remoto em busca e TV ao vivo (v1.1.88)
- [x] Aplicar foco remoto aos cards de resultados da busca na TV.
- [x] Aplicar foco remoto aos canais e gravações da TV ao vivo.
- [x] Preservar o layout original em celular e tablet.
- [x] Validar foco real em `MediaCard` com teste Compose instrumentado.

## 198. Atualização automática da Home na Android TV (v1.1.89)
- [x] Atualizar a Home automaticamente a cada 60 segundos enquanto estiver visível na TV.
- [x] Interromper o ciclo ao sair da tela ou perder o estado RESUMED.
- [x] Preservar o comportamento explícito de atualização em celular e tablet.
- [x] Cobrir a política de intervalo por tipo de dispositivo com testes unitários.

## 199. Correção do Quick Connect (v1.1.90)
- [x] Usar `POST /QuickConnect/Initiate` conforme o contrato do servidor.
- [x] Consultar `GET /QuickConnect/Connect?secret=...` durante o polling.
- [x] Preservar a autenticação e o armazenamento da sessão após autorização.
- [x] Cobrir os verbos, caminhos e query parameters com teste de contrato Retrofit.

## 200. Autenticação completa do Quick Connect (v1.1.91)
- [x] Interpretar `GET /QuickConnect/Connect` como estado, sem esperar token.
- [x] Trocar o secret autorizado em `POST /Users/AuthenticateWithQuickConnect`.
- [x] Encerrar polling após expiração, erro 401/404 ou orçamento de cinco minutos.
- [x] Manter falhas transitórias de rede elegíveis para retry.
- [x] Cobrir estado, troca de autenticação e política de polling nos testes.

## 201. Quick Connect com validade visível e cópia do PIN (v1.1.92)
- [x] Mostrar o tempo restante do código durante a autorização.
- [x] Permitir copiar o PIN com uma ação acessível.
- [x] Limpar o contador ao autorizar, cancelar ou expirar.
- [x] Cobrir duração, decremento e limite inferior do contador.

## 202. Leitura de QR para conexão ao servidor (v1.1.93)
- [x] Ler QR usando o Google Code Scanner quando o dispositivo possuir câmera.
- [x] Aceitar URL HTTP/HTTPS direta e payload `mulletaflix://server?url=...`.
- [x] Validar o payload antes de preencher a URL e ocultar a ação em TV sem câmera.
- [x] Cobrir URLs diretas, URLs codificadas e payloads inválidos.

## 203. Grade de TV compacta e focável (v1.1.94)
- [x] Reduzir a escala dos cards nas fileiras da Home para mostrar mais títulos simultaneamente.
- [x] Ajustar o tamanho mínimo da grade de bibliotecas para equilibrar densidade e legibilidade em TV.
- [x] Preservar o destaque visual do item focado pelo controle remoto.
- [x] Cobrir o contrato responsivo de TV com testes unitários.
- [x] Atualizar a Home imediatamente ao abrir ou retornar ao primeiro plano na TV.

## 204. Atualização imediata da Biblioteca em TV (v1.1.95)
- [x] Recarregar a Biblioteca assim que a tela entra em `RESUMED`.
- [x] Continuar atualizando a lista automaticamente a cada 60 segundos enquanto visível.
- [x] Evitar carga inicial duplicada ao combinar o ciclo de visibilidade com o refresh periódico.
- [x] Cobrir a política de atualização com teste unitário.

## 205. Atualização automática da TV ao vivo (v1.1.96)
- [x] Atualizar canais e gravações imediatamente ao abrir ou retornar à tela na Android TV.
- [x] Atualizar a grade de TV ao vivo a cada 60 segundos enquanto visível.
- [x] Preservar o refresh manual em telefone e tablet.
- [x] Cobrir os intervalos e o comportamento por dispositivo com testes unitários.

## 206. Minha Lista adaptativa para TV (v1.1.97)
- [x] Trocar a grade fixa de três colunas por uma grade adaptativa em tablet e TV.
- [x] Aplicar foco visual e navegação remota aos cards da TV.
- [x] Atualizar Minha Lista imediatamente e a cada 60 segundos enquanto visível na TV.
- [x] Preservar três colunas e refresh manual no telefone.
- [x] Cobrir colunas e política de refresh com testes unitários.

## 207. Verificação instrumentada da Minha Lista (v1.1.97)
- [x] Validar o contrato de três colunas no telefone.
- [x] Validar a grade adaptativa no tablet.
- [x] Validar a grade densa e o foco remoto na TV.

## 208. Busca adaptativa e histórico focável em tablet/TV (v1.2.35)
- [x] Limitar e centralizar o conteúdo da busca em tablets e TVs para melhorar a leitura.
- [x] Tornar cada item do histórico uma ação única de repetir busca navegável pelo controle remoto.
- [x] Preservar o toque e a densidade originais no celular.
- [x] Validar a política de largura e o histórico focável com testes unitários e instrumentados em TV e tablet.

## 209. Ordenação transacional da Biblioteca (v1.2.36)
- [x] Permitir selecionar o campo e a direção Ascendente/Descendente antes de aplicar.
- [x] Enviar uma única consulta ao servidor para cada decisão completa de ordenação.
- [x] Persistir campo e direção juntos nas preferências locais.
- [x] Validar o menu e a consulta combinada com testes unitários e instrumentados em TV e tablet.

## 210. Preferências de faixas na preparação inicial (v1.2.37)
- [x] Enviar ao servidor o índice da faixa de áudio preferida quando ela estiver disponível nos dados da mídia.
- [x] Enviar ao servidor o índice da legenda preferida quando ela estiver disponível nos dados da mídia.
- [x] Preservar o fallback para a resposta de `PlaybackInfo` quando o servidor só fornece os streams na fonte de reprodução.
- [x] Cobrir a seleção inicial explícita de idioma com teste unitário e manter o lint do player verde.

## 211. Contrato do payload de reprodução (v1.2.38)
- [x] Verificar o corpo real de `PlaybackInfo` enviado pelo repositório de dados.
- [x] Cobrir índices de áudio, legenda, posição de retomada e flags de streaming.
- [x] Confirmar que a integração mantém uma única chamada ao servidor.

## 212. Home sem chamadas durante indisponibilidade de rede (v1.2.39)
- [x] Impedir chamadas ao servidor na carga inicial quando o dispositivo está offline.
- [x] Impedir que uma atualização manual offline gere uma requisição desnecessária.
- [x] Recarregar automaticamente a Home após a reconexão.
- [x] Cobrir a corrida de inicialização e a recuperação de rede no teste do ViewModel.

## 213. Quick Connect autorizado na resposta inicial (v1.2.40)
- [x] Autenticar imediatamente quando o servidor retornar `authenticated=true` ao iniciar o código.
- [x] Manter o polling de três segundos para códigos ainda pendentes.
- [x] Limpar PIN e secret após a autenticação imediata.
- [x] Cobrir o caminho autorizado com teste do ViewModel.

## 214. Contraste acessível do vermelho da marca (v1.2.41)
- [x] Medir o contraste real do vermelho vívido `#E50914` sobre as superfícies escuras: 3,84:1 na surface e 4,18:1 no background, abaixo do mínimo AA de 4,5:1 para texto normal.
- [x] Separar o vermelho em dois papéis: preenchimento/anel de foco (`MulletaFlixRed`) e texto/ícone (`colorScheme.secondary`).
- [x] Elevar automaticamente o acento de todos os temas (Dark, Netflix, Purple Haze, Blue Radiance) até 4,5:1 antes de entregar ao `MaterialTheme`.
- [x] Manter o indicador de aba e os botões preenchidos com o vermelho vívido, onde o texto branco sobre ele mede 4,79:1.
- [x] Cobrir a política com testes de contraste WCAG 2.2 por tema.
- [x] Registrar a verificação instrumentada de 39 testes na Android TV contra o servidor real.

## 215. Contraste de texto secundário e limites de componente (v1.2.42)
- [x] Medir, pela própria implementação, o contraste de cada alpha reduzido sobre as cinco superfícies escuras.
- [x] Elevar apenas os alphas que reprovam (branco 0,4; `onSurface` 0,4 e 0,5), preservando os que já passam (branco 0,5 e tudo em 0,6+).
- [x] Corrigir `DarkOutline` de `#424242` (1,83:1) para `#808080` (4,66:1), atendendo o mínimo de 3:1 do SC 1.4.11 para a borda de campos de texto.
- [x] Aplicar a correção em `ProfileScreen` e `ServerSelectionScreen` e centralizar o fundo do fluxo de autenticação.
- [x] Cobrir a política com testes que reprovam os alphas antigos e aprovam os limites de componente.

## 216. Guia EPG travado após atualização de canais (v1.2.42)
- [x] Corrigir `refresh()` da TV ao vivo, que incrementava `guideGeneration` sem limpar `isLoadingGuide`, deixando o guia com spinner infinito e o botão "Guia EPG" desabilitado até reiniciar o app.
- [x] Cancelar o job do guia ao invalidá-lo, para não desperdiçar uma resposta já obsoleta.
- [x] Corrigir o transporte falso do teste, que usava `NonCancellable` e mascarava o requisito.
- [x] Provar que o teste reprova sem a correção (16 testes, 1 falha) e passa com ela.

## 217. Deep link: servidor de destino, id fantasma e entrega repetida (v1.2.42)
- [x] Ler `serverId` do link (query ou fragmento) e expor `MediaLink(itemId, serverId)`.
- [x] Deixar de interpretar o segmento de rota `web` como id de mídia, que abria `detail/web` com erro.
- [x] Entregar deep links por sequência em vez de por id, para que abrir o mesmo link duas vezes navegue nas duas vezes.
- [x] Limpar o link pendente ao consumi-lo, para que sair e entrar novamente não reabra o detalhe antigo.

## 218. Compartilhamento utilizável fora da rede local (v1.2.42)
- [x] Trocar qualquer endereço privado (loopback e faixas 10/172.16-31/192.168/169.254) pelo endpoint público ao gerar o link.
- [x] Incluir `serverId` no link compartilhado, para o destinatário resolver o item no servidor correto.
- [x] Mover a classificação de host local para o design-system, usada por LAN e compartilhamento com a mesma regra.
- [x] Enviar também `EXTRA_SUBJECT` e cobrir cada tipo de mídia com seu próprio id.

## 219. Paginação da biblioteca (v1.2.42)
- [x] Encerrar a paginação quando o servidor devolve página vazia, mesmo com total maior, evitando sentinela carregando para sempre.
- [x] Descartar o catálogo anterior ao trocar de biblioteca, evitando pular os primeiros itens quando a primeira página falha.
- [x] Remover os campos write-only `currentStartIndex` e `totalItems`, resíduo da paginação antiga.
- [x] Aplicar a mesma política em Minha Lista.

## 220. Auditoria de logs e polling em segundo plano (v1.2.42)
- [x] Confirmar que não há nenhuma chamada de log nos 195 arquivos de produção e que `HttpLoggingInterceptor` está em `Level.NONE` incondicional.
- [x] Adicionar teste-guarda que reprova o build se surgir `Log.`, `println`, `Timber.`, `printStackTrace` ou `System.out/err` no módulo de API, e se o nível do logger deixar de ser `NONE`.
- [x] Provar que o guarda reprova com uma chamada de log injetada.
- [x] Fazer o polling de Configurações (30 s) e SyncPlay (5 s) rodar somente com a tela em `RESUMED`.

## 221. Anúncio único do cartão de mídia (v1.2.43)
- [x] Inspecionar a árvore de semântica no próprio aparelho (`printToLog` + `adb logcat`) em vez de presumir o comportamento do merge.
- [x] Tornar decorativos (capa, selos, overlay, barra de progresso) os elementos que entravam como nós próprios dentro do nó mesclado.
- [x] Impedir que o selo `AO VIVO` seja concatenado em texto cru na descrição falada.
- [x] Unificar o `contentDescription` duplicado do ícone de favorito (parâmetro e bloco `semantics`).
- [x] Documentar por teste a repetição ainda presente no fallback de capa quebrada, com a medição real e a condição para remover o teste.
- [x] Avaliar casting/espelhamento: o botão funciona, mas nada envia mídia para a sessão; implementar exigiria receptor próprio e teste com hardware real.
- [x] Confirmar que não há nenhuma chamada de log nos 195 arquivos de produção e que `HttpLoggingInterceptor` está em `Level.NONE` incondicional.
- [x] Adicionar teste-guarda que reprova o build se surgir `Log.`, `println`, `Timber.`, `printStackTrace` ou `System.out/err` no módulo de API, e se o nível do logger deixar de ser `NONE`.
- [x] Provar que o guarda reprova com uma chamada de log injetada.
- [x] Fazer o polling de Configurações (30 s) e SyncPlay (5 s) rodar somente com a tela em `RESUMED`.

## 222. Política única de instalação da atualização (v1.2.88)
- [x] Extrair a classificação do resultado da instalação (abriu, recusou, explodiu) e as mensagens canônicas para `:core:common/update/AppUpdateInstallOutcome.kt`.
- [x] Usar a política compartilhada nos dois fluxos de atualização (aviso da `MainActivity` e Centro de Atualizações), mantendo o comportamento observável de cada tela.
- [x] Injetar a instalação como lambda no `SettingsViewModel`, como já era no `AppUpdateViewModel`, para cobrir o ramo de instalação em testes de JVM.
- [x] Cobrir sucesso, recusa e exceção nos testes do `SettingsViewModel` e os três desfechos (com mensagens literais) nos testes da política, incluindo provas por reversão.

## 223. Índices de faixas padrão vindos do servidor (v1.2.89)
- [x] Mapear `DefaultAudioStreamIndex` e `DefaultSubtitleStreamIndex` de `MediaSourceDto` para `MediaSource`.
- [x] Preservar `-1` como escolha explícita de legendas desativadas.
- [x] Cobrir o mapper com teste unitário; a reprodução E2E com uma mídia autenticada real continua pendente porque a sessão de teste retornou HTTP 400.

## 224. Contrato JSON das faixas padrão (v1.2.90)
- [x] Validar a desserialização real de `DefaultAudioStreamIndex` e `DefaultSubtitleStreamIndex` com Moshi.
- [x] Preservar compatibilidade com payloads antigos que não possuem esses campos.

## 225. Descoberta LAN IPv6 consistente (v1.2.91)
- [x] Reconhecer endereços IPv6 privados, link-local e loopback na regra compartilhada de servidor local.
- [x] Impedir que `::` seja usado como endpoint discável mesmo sendo um endereço local de escuta.
- [x] Fazer o status do perfil reutilizar a mesma política de classificação da recuperação LAN.
- [x] Cobrir IPv6 na política de recuperação e na apresentação do perfil.

## 226. Compatibilidade futura do Kotlin (v1.2.92)
- [x] Remover o aviso de visibilidade futura do `copy()` em `MediaDeepLinkRequest` com `ConsistentCopyVisibility`.
- [x] Fixar o alvo da anotação `@ApplicationContext` no parâmetro do repositório de downloads.
- [x] Confirmar testes, lint, pacote, instalação na TV e release após a atualização de versão.

## 227. Deep link troca automaticamente para o servidor correto (v1.2.93)
- [x] Redirecionar links com `serverId` diferente para a tela de seleção de servidor.
- [x] Preservar o item pendente durante a troca de servidor e autenticação.
- [x] Não interromper o fluxo quando o usuário já estiver em seleção/login.
- [x] Cobrir a decisão de redirecionamento com testes unitários.

## 228. Suíte instrumentada do app no Android TV (v1.2.94)
- [x] Executar os 13 testes instrumentados do módulo `app` no AVD `MulletaflixTvApi34`.
- [x] Declarar explicitamente o opt-in da API experimental do Coil usada pela cadeia falsa do teste de cache.
- [x] Confirmar build instrumentado sem falhas e fechamento automático do emulador.

## 229. Teste de Cast compatível com AVD sem Google Play Services (v1.2.95)
- [x] Detectar a ausência do módulo Google Cast no AVD antes de inicializar `CastContext`.
- [x] Ignorar somente o teste que depende do módulo ausente, mantendo a medição ativa em dispositivos compatíveis.
- [x] Executar a suíte de player no Android TV e confirmar zero falhas, com um skip documentado.
- [x] Validar o alvo de toque do Cast em um dispositivo/AVD com Google Play Services Cast disponível.

## 230. Fallback público após falha de qualquer servidor LAN descoberto (v1.2.96)
- [x] Tentar o servidor salvo/público quando o endpoint LAN escolhido falhar, mesmo se outro anúncio LAN estiver em primeiro lugar.
- [x] Não iniciar fallback adicional quando a própria URL pública falhar.
- [x] Cobrir os dois caminhos com teste unitário de política.

## 231. Refresh automático confiável ao retornar para a TV (v1.2.97)
- [x] Extrair o ciclo de atualização da Home para um efeito observável e testável.
- [x] Aplicar o mesmo ciclo à Library, com atualização imediata ao entrar em `RESUMED` e intervalo periódico.
- [x] Cobrir `ON_STOP → ON_START → ON_RESUME` em teste instrumentado no AVD de Android TV.

## 232. Cobertura independente do refresh da Library (QA pós-release)
- [x] Adicionar um teste instrumentado específico para o efeito de refresh da Library.
- [x] Confirmar duas atualizações imediatas: entrada inicial e retorno ao `RESUMED`.
- [x] Manter a release APK-only inalterada, pois `androidTest` não modifica o binário distribuído.

## 233. Validação Cast em AVD com Google Play Services
- [x] Executar os 14 testes instrumentados do módulo player no `MulletaflixApi35` (`google_apis_playstore`).
- [x] Confirmar `0 skipped` e `0 failed`, incluindo o teste real do `MediaRouteButton`.
- [x] Registrar a evidência sem gerar nova release, pois a execução não alterou o código de produção.

## 234. Suíte instrumentada no tablet Android 15
- [x] Executar `:app:connectedDebugAndroidTest` no `MulletaflixTabletApi35`.
- [x] Confirmar 13/13 testes aprovados, 0 skips e 0 falhas.
- [x] Confirmar encerramento automático do emulador após a execução.

## 235. Matriz instrumentada completa por dispositivo
- [x] Confirmar 13/13 testes no celular `MulletaflixApi35` (Android 15, Play Store).
- [x] Confirmar 13/13 testes no tablet `MulletaflixTabletApi35` (Android 15, Play Store).
- [x] Confirmar 13/13 testes no TV `MulletaflixTvApi34` (Android 14, Android TV).
- [x] Confirmar zero skips e zero falhas nas três execuções.

## 236. Smoke conectado ao servidor público (QA pós-release v1.2.97)
- [x] Confirmar `System/Info/Public` no endpoint DuckDNS com HTTP 200 e servidor v12.0.78.
- [x] Confirmar autenticação da conta de teste diretamente pela API com HTTP 200 e token retornado, sem registrar credenciais.
- [x] Repetir a interação no AVD celular com coordenadas derivadas do dump UI, mantendo o `MainActivity` em foreground e sem `FATAL EXCEPTION` no logcat.
- [ ] Automatizar a entrada de senha com caractere especial por UI sem depender de `adb input text`, pois o shell/teclado do AVD perde caracteres em campos Compose; não tratar a tentativa atual como prova de navegação pós-login.
- [x] Confirmar conteúdo autenticado: 4 bibliotecas, 8.020 mídias no total e 20 títulos retornados na consulta de filmes/séries.

## 237. Automação confiável do formulário de login (v1.2.98)
- [x] Adicionar tags semânticas estáveis para usuário, senha e envio do login e expô-las como `resource-id` para UIAutomator.
- [x] Cobrir entrada Compose com senha contendo caractere especial (`Bug309c*`) sem depender do parser do `adb input text`.
- [x] Executar o teste instrumentado de autenticação no AVD celular: 3/3, 0 skipped, 0 failed.
- [x] Gerar e instalar localmente o APK `v1.2.98` (`versionCode=299`) no AVD celular.
- [x] Revalidar lint isolado após a geração dos artefatos intermediários: `:app:lintDebug` passou com código 0.
- [x] Revalidar `:app:assembleRelease` e o teste de autenticação após a semântica `testTagsAsResourceId`: 3/3, 0 skipped, 0 failed.
- [x] Validar Quick Connect no servidor público: `QuickConnect/Enabled` respondeu `true` e `QuickConnect/Initiate` respondeu HTTP 200 com identidade do cliente.
- [x] Rejeitar resposta Quick Connect sem código/segredo e normalizar espaços antes do polling.
- [x] Expor tags estáveis para aba, botão de geração e código Quick Connect; teste instrumentado total: 4/4, 0 skipped, 0 failed.
- [x] Cobrir o contrato HTTP real do Quick Connect com `MockWebServer`: POST, rota, payload e cabeçalho `Authorization` do cliente.
- [x] Pacote final local: 7.336.683 bytes, SHA-256 `D4C6002CA8A7205B91EBE5A1E5E37557178E012AD5662E7DB4F7E2D5180B0BE3`.

## 238. Grade de capas compacta para Android TV (v1.2.99)
- [x] Ajustar o tamanho mínimo das capas considerando a largura lógica real de 480dp em TVs 1080p.
- [x] Exibir 5 colunas no modo confortável e 6 no modo compacto no AVD `MulletaflixTvApi34`.
- [x] Aplicar a mesma política à tela de favoritos.
- [x] Validar política unitária e suíte instrumentada da biblioteca: 10/10 no Android TV.
- [x] Gerar APK local `v1.2.99`, `versionCode=300`, com lint e release build aprovados.
- [ ] Publicar release remota somente quando o bloco de release definido no handoff for fechado.

## 239. Catálogo ampliado de idiomas do player (v1.3.0)
- [x] Expandir as preferências de áudio e legenda para 15 idiomas selecionáveis, mantendo português, inglês, espanhol, francês e alemão.
- [x] Normalizar códigos ISO comuns retornados pelo servidor, incluindo variantes de italiano, japonês, coreano, chinês, russo, árabe, neerlandês, turco, polonês e hindi.
- [x] Exibir rótulos amigáveis para as novas faixas de áudio e legenda no player.
- [x] Cobrir `MediaLanguage`, `TrackLabelPolicy` e `SettingsOptionLists` com testes unitários.
- [x] Executar `:app:lintDebug` e `:app:assembleRelease` com código 0.
- [x] Executar a suíte instrumentada do player no AVD celular: 14/14, 0 skipped, 0 failed.
- [x] Gerar APK local `v1.3.0`, `versionCode=301`, 7.336.679 bytes, SHA-256 `0717495296CC4B69FEFCAFF9959291C55352069B347098570FA917170937D03A`.
- [ ] Publicar release remota somente quando o bloco de release definido no handoff for fechado.

## 240. Densidade adaptativa específica para tablets (v1.3.1)
- [x] Diferenciar tablet de celular pela largura mínima de 600dp, sem confundir tablet com Android TV.
- [x] Usar capas de 140dp no modo confortável e 116dp no modo compacto em tablets.
- [x] Preservar a política compacta da TV: 5 colunas confortáveis e 6 compactas em 480dp.
- [x] Cobrir celular, tablet e TV em `LibraryGridDensityTest`.
- [x] Suíte instrumentada da biblioteca na TV: 10/10, 0 skipped, 0 failed.
- [x] `:app:lintDebug` e `:app:assembleRelease` aprovados.
- [x] Corrigir `build-app-package.ps1` para ler `appVersion` do catálogo Gradle quando `versionName` usa `libs.versions.appVersion`.
- [x] Gerar APK local `v1.3.1`, `versionCode=302`, 7.336.683 bytes, SHA-256 `22EDA29A522E37CD3DD4D1BA1402A9E0FD6F6DF921B3DCAE7E56AB0FAF43C47A`.
- [ ] Publicar release remota somente quando o bloco de release definido no handoff for fechado.

## 241. Densidade compartilhada em Minha Lista (v1.3.2)
- [x] Fazer a tela de favoritos consumir a mesma preferência de densidade da biblioteca.
- [x] Aplicar modo confortável/compacto de forma consistente em celular, tablet e TV.
- [x] Cobrir a política com `FavoritesPresentationPolicyTest` e validar o ciclo do `FavoritesViewModel`.
- [x] Suíte instrumentada da biblioteca na TV: 10/10, 0 skipped, 0 failed.
- [x] Unitários completos: `testDebugUnitTest` aprovado.
- [x] `:app:lintDebug` e `:app:assembleRelease` aprovados.
- [x] Gerar APK local `v1.3.2`, `versionCode=303`, 7.336.679 bytes, SHA-256 `856EAB52E2153D7916C64E18B894BE49C1A9DBF15DBE9CCF12233681871E9D38`.
- [ ] Publicar release remota somente quando o bloco de release definido no handoff for fechado.

## 242. Atualização manual de metadados no detalhe (v1.3.3)
- [x] Adicionar ação de atualização no detalhe sem sair da tela, recarregando metadados, imagens, NFO e episódios pelo `ItemDetailViewModel`.
- [x] Exibir estado de carregamento acessível no botão: `Atualizar detalhes` / `Atualizando detalhes`.
- [x] Cobrir a política de descrição da ação com `DetailRefreshPolicyTest`.
- [x] Executar `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` com código 0.
- [x] Executar a suíte instrumentada do módulo item-detail no AVD `MulletaflixApi35`: 14/14, 0 skipped, 0 failed.
- [x] Gerar APK local `v1.3.3`, `versionCode=304`, 7.336.679 bytes, SHA-256 `EB3EAC284A7FCFC9B603D36E561CBC7145CE52B0094F3C139CFF82F966825172`.
- [x] Confirmar `versionName=1.3.3`/`versionCode=304` com `aapt` e encerrar o emulador (`emulator=0`, `qemu=0`).
- [ ] Publicar release remota somente quando o bloco de release definido no handoff for fechado.

## 243. Sugestões de busca durante a digitação (v1.3.4)
- [x] Ativar a API existente `Search/Hints` no fluxo de busca do APK.
- [x] Debounce curto de 180 ms, cancelamento de consultas antigas e ocultação das sugestões quando a busca completa começa.
- [x] Filtrar por tipo selecionado, remover IDs duplicados e limitar a oito sugestões para manter a lista fluida.
- [x] Adicionar painel Compose rolável, acessível e amigável ao D-pad/TV para abrir diretamente o título sugerido.
- [x] Cobrir ViewModel com hints filtrados/deduplicados e painel com teste instrumentado.
- [x] Executar `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` com código 0.
- [x] Suíte instrumentada de busca no AVD `MulletaflixApi35`: 9/9, 0 skipped, 0 failed.
- [x] Conferir v1.3.3 antes do bump: `versionCode=304`, SHA-256 `EB3EAC284A7FCFC9B603D36E561CBC7145CE52B0094F3C139CFF82F966825172`.
- [x] Gerar APK local `v1.3.4`, `versionCode=305`, 7.336.679 bytes, SHA-256 `64DEA7056D284D9B71F97D6069E9BB2850186863F796B83C03D40D9760950129`.
- [x] Confirmar `versionName=1.3.4`/`versionCode=305` com `aapt` e encerrar o emulador (`emulator=0`, `qemu=0`).
- [ ] Publicar release remota somente quando o bloco de release definido no handoff for fechado.

## 244. Restauração do scroll em Biblioteca e Minha Lista (v1.3.5)
- [x] Compartilhar um contrato de `LazyGridState` salvo entre Biblioteca e Minha Lista.
- [x] Preservar a posição da grade durante recriação/restauração de estado, inclusive para TV e tablet.
- [x] Adicionar teste instrumentado real de restauração da posição da grade.
- [x] Suíte instrumentada da biblioteca no AVD `MulletaflixApi35`: 11/11, 0 skipped, 0 failed.
- [x] Executar `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` com código 0.
- [x] Conferir v1.3.4 antes do bump: `versionCode=305`, SHA-256 `64DEA7056D284D9B71F97D6069E9BB2850186863F796B83C03D40D9760950129`.
- [x] Gerar APK local `v1.3.5`, `versionCode=306`, 7.336.679 bytes, SHA-256 `955E703B10D7200ED5EF9799B1483EDC159931BA84A2A8D352C2A1DF155FFE97`.
- [x] Confirmar `versionName=1.3.5`/`versionCode=306` com `aapt` e encerrar o emulador (`emulator=0`, `qemu=0`).
- [ ] Publicar release remota somente mediante autorização explícita; não publicar nesta rodada.

## 245. Restauração do scroll em Home e Busca + foco TV (v1.3.6)
- [x] Salvar a posição vertical da Home durante recriação/restauração de estado.
- [x] Salvar a posição dos resultados, histórico e sugestões da Busca.
- [x] Adicionar testes instrumentados de restauração para Home e Busca.
- [x] Corrigir o alvo de foco explícito dos botões do topo no modo TV, mantendo o comportamento touch sem anel permanente.
- [x] Suíte instrumentada da Busca no AVD `MulletaflixApi35`: 10/10, 0 skipped, 0 failed.
- [x] Suíte instrumentada da Home no AVD `MulletaflixApi35`: 8/8, 0 skipped, 0 failed.
- [x] Executar `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` com código 0.
- [x] Conferir v1.3.5 antes do bump: `versionCode=306`, SHA-256 `955E703B10D7200ED5EF9799B1483EDC159931BA84A2A8D352C2A1DF155FFE97`.
- [x] Gerar APK local `v1.3.6`, `versionCode=307`, 7.336.679 bytes, SHA-256 `173CA7BF36F58A811C9C80C599383520685626CA94C05AD6D83ECE0B7550BA84`.
- [x] Confirmar `versionName=1.3.6`/`versionCode=307` com `aapt` e encerrar o emulador (`emulator=0`, `qemu=0`).
- [ ] Publicar release remota somente mediante autorização explícita; não publicar nesta rodada.

## 246. Restauração dos carrosséis horizontais (v1.3.7)
- [x] Preservar a posição dos carrosséis de conteúdo e bibliotecas da Home.
- [x] Preservar a posição dos carrosséis agrupados de resultados da Busca.
- [x] Cobrir restauração horizontal com testes instrumentados reais em Home e Busca.
- [x] Suíte instrumentada da Home no AVD `MulletaflixApi35`: 9/9, 0 skipped, 0 failed.
- [x] Suíte instrumentada da Busca no AVD `MulletaflixApi35`: 11/11, 0 skipped, 0 failed.
- [x] Executar `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` com código 0.
- [x] Conferir v1.3.6 antes do bump: `versionCode=307`, SHA-256 `173CA7BF36F58A811C9C80C599383520685626CA94C05AD6D83ECE0B7550BA84`.
- [x] Gerar APK local `v1.3.7`, `versionCode=308`, 7.336.683 bytes, SHA-256 `2EDF6972A7F98568AA3D160CBBA5470EB599E04D75C20C2C878A5F60C3A501A7`.
- [x] Confirmar `versionName=1.3.7`/`versionCode=308` com `aapt` e encerrar o emulador (`emulator=0`, `qemu=0`).
- [ ] Publicar release remota somente mediante autorização explícita; não publicar nesta rodada.

## 271. Retry focável na biblioteca (v1.3.31)
- [x] Aplicar o foco vermelho aos retries de erro inicial, erro em grade e banner offline.
- [x] Preservar a grade adaptativa e a interação por toque.
- [x] Cobrir o foco remoto do retry offline em teste instrumentado.
- [x] Executar 5 testes instrumentados na Android TV: 5/5, 0 skipped, 0 failed.
- [x] Executar `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` com código 0.
- [x] Gerar APK local `v1.3.31`, `versionCode=332`, 7.353.067 bytes, SHA-256 `8BDF480C8BEAC44DE5951E6066D9C00B34E07E175559CBF9B3EBBF8798671385`.
- [x] Confirmar que os emuladores foram encerrados após os testes.
- [ ] Publicar release remota somente mediante autorização explícita; não publicar nesta rodada.

## 272. Encerramento seguro da sessão remota ao trocar mídia (v1.3.32)
- [x] Conferir a versão anterior antes do bump: `v1.3.31`, `versionCode=332`, SHA-256 `8BDF480C8BEAC44DE5951E6066D9C00B34E07E175559CBF9B3EBBF8798671385`.
- [x] Encerrar o playback remoto anterior antes de iniciar outra mídia, aguardando o relatório de parada.
- [x] Impedir que relatórios atrasados atravessem troca de conta, sessão ou geração de carregamento.
- [x] Preservar o fluxo offline sem chamadas indevidas ao servidor e proteger a carga offline contra callbacks obsoletos.
- [x] Executar 19 testes instrumentados do player na Android TV: 18 aprovados, 1 skipped esperado, 0 falhas.
- [x] Executar `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` com código 0.
- [x] Gerar APK local `v1.3.32`, `versionCode=333`, 7.353.067 bytes, SHA-256 `60F9A6A831DB3A27544CCB127BA68C6037D877CBB9915795C66DEBC5C41B5F78`.
- [ ] Publicar release remota somente mediante autorização explícita; não publicar nesta rodada.

## 273. Mensagens acionáveis de conexão e Quick Connect (v1.3.33)
- [x] Conferir a versão anterior antes do bump: `v1.3.32`, `versionCode=333`, 7.353.067 bytes, SHA-256 `60F9A6A831DB3A27544CCB127BA68C6037D877CBB9915795C66DEBC5C41B5F78`.
- [x] Traduzir falhas de HTTP, timeout, host indisponível e bloqueio CLEARTEXT em mensagens úteis para conexão, login, cadastro e Quick Connect.
- [x] Executar 5 testes instrumentados de autenticação na Android TV: 5/5, 0 skipped, 0 falhas.
- [x] Executar `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` com código 0.
- [x] Gerar e registrar o APK local `v1.3.33`, `versionCode=334`, 7.336.683 bytes, SHA-256 `D08648413EE7BF87EA3215594F8F23691DE4CEBD7F2AC6619532B31061D0117E`.
- [ ] Publicar release remota somente mediante autorização explícita; não publicar nesta rodada.

## 274. Loop de atualização resiliente na TV (v1.3.34)
- [x] Conferir a versão anterior antes do bump: `v1.3.33`, `versionCode=334`, 7.336.683 bytes, SHA-256 `D08648413EE7BF87EA3215594F8F23691DE4CEBD7F2AC6619532B31061D0117E`.
- [x] Impedir que uma falha transitória no callback de atualização encerre o loop de foreground da Home ou da Biblioteca na Android TV.
- [x] Preservar o cancelamento normal do ciclo de vida, relançando `CancellationException` sem engolir a saída da tela.
- [x] Adicionar testes instrumentados de regressão para continuar atualizando após a primeira falha.
- [x] Executar Home na Android TV: 12/12, 0 skipped, 0 falhas.
- [x] Executar Biblioteca na Android TV: 14/14, 0 skipped, 0 falhas.
- [x] Executar `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` com código 0.
- [x] Gerar e registrar o APK local `v1.3.34`, `versionCode=335`, 7.336.679 bytes, SHA-256 `46E34B8C6BFC73FD41B9A0ED0CB42862AD1F9D231B13643C2CEC2553FFAF62B4`.
- [ ] Publicar release remota somente mediante autorização explícita; não publicar nesta rodada.

## 275. Atualização resiliente da TV ao vivo e EPG (v1.3.35)
- [x] Conferir a versão anterior antes do bump: `v1.3.34`, `versionCode=335`, 7.336.679 bytes, SHA-256 `46E34B8C6BFC73FD41B9A0ED0CB42862AD1F9D231B13643C2CEC2553FFAF62B4`.
- [x] Centralizar os timers de canais e EPG em um scheduler de foreground com tratamento de falha transitória.
- [x] Preservar cancelamento de lifecycle e impedir polling quando a tela não está resumida.
- [x] Adicionar testes instrumentados para falha transitória e pausa da tela.
- [x] Executar a suíte instrumentada da TV ao vivo: 8/8, 0 skipped, 0 falhas.
- [x] Executar `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` com código 0.
- [x] Gerar e registrar o APK local `v1.3.35`, `versionCode=336`, 7.353.067 bytes, SHA-256 `9F87A81CE26E38933C07851AC14F49E7F0CDB28A409653783C694C91353AC5AF`.
- [ ] Publicar release remota somente mediante autorização explícita; não publicar nesta rodada.

## 280. Controles básicos de reprodução no SyncPlay (v1.3.40)
- [x] Conferir a versão anterior antes do bump: `v1.3.39`, `versionCode=340`, 7.353.067 bytes, SHA-256 `3095F6E29CB528773250E0484636E73AD774281E7B0334C40C153B7CED08FD31`.
- [x] Mapear as rotas reais do servidor para `SyncPlay/Pause`, `SyncPlay/Unpause` e `SyncPlay/Stop` no cliente Android.
- [x] Expor os comandos na sala ativa com foco compatível com controle remoto/D-pad e proteção contra toques duplicados.
- [x] Adicionar cobertura de contrato da API e teste unitário do ViewModel para envio único durante submissão.
- [x] Executar a suíte instrumentada da Biblioteca na Android TV: 15/15, 0 skipped, 0 falhas; o módulo SyncPlay não possui testes instrumentados (`0 testes`).
- [x] Executar `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` com código 0 antes do empacotamento.
- [x] Gerar e registrar o APK local `v1.3.40`, `versionCode=341`, 7.353.067 bytes, SHA-256 `F693CFC81D1D4128FD5CC45E13C149AA82436A026CDADF11D6F846BB01C16767`.
- [ ] Publicar release remota somente mediante autorização explícita; não publicar nesta rodada.

## 279. Cobertura instrumentada do banner offline contextual (v1.3.39)
- [x] Conferir a versão anterior antes do bump: `v1.3.38`, `versionCode=339`, 7.353.067 bytes, SHA-256 `FAAAEDD038CF40EC7B35C1EFF3A722978A59156EFEE68E2CEC7A0F020B3442F9`.
- [x] Adicionar teste instrumentado para a mensagem contextual de Minha Lista no banner offline compartilhado.
- [x] Confirmar que a ação `Tentar novamente` continua visível e compatível com foco remoto/D-pad.
- [x] Executar a suíte instrumentada da Biblioteca na Android TV: 15/15, 0 skipped, 0 falhas.
- [x] Executar `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` com código 0 antes do bump.
- [x] Gerar e registrar o APK local `v1.3.39`, `versionCode=340`, 7.353.067 bytes, SHA-256 `3095F6E29CB528773250E0484636E73AD774281E7B0334C40C153B7CED08FD31`.
- [ ] Publicar release remota somente mediante autorização explícita; não publicar nesta rodada.

## 278. Banner offline contextual em Minha Lista (v1.3.38)
- [x] Conferir a versão anterior antes do bump: `v1.3.37`, `versionCode=338`, 7.353.067 bytes, SHA-256 `CC613BFFDD9E63F65AF8E9BDF0C861C4700EA0D804CDFF2A9A25858F44A681DC`.
- [x] Exibir em Minha Lista o estado offline enquanto a conectividade estiver indisponível.
- [x] Reutilizar o banner focável da Biblioteca com mensagem contextual e ação de retry para TV/D-pad.
- [x] Cobrir o estado offline e sua limpeza após o retorno da rede no teste unitário de reconciliação.
- [x] Executar a suíte instrumentada da Biblioteca na Android TV: 14/14, 0 skipped, 0 falhas.
- [x] Executar `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` com código 0 antes do bump.
- [x] Gerar e registrar o APK local `v1.3.38`, `versionCode=339`, 7.353.067 bytes, SHA-256 `FAAAEDD038CF40EC7B35C1EFF3A722978A59156EFEE68E2CEC7A0F020B3442F9`.
- [ ] Publicar release remota somente mediante autorização explícita; não publicar nesta rodada.

## 277. Recuperação de Minha Lista após retorno da rede (v1.3.37)
- [x] Conferir a versão anterior antes do bump: `v1.3.36`, `versionCode=337`, 7.353.067 bytes, SHA-256 `2AEF6EADBD1109FBA95BDA678CFF89B7A8DAC4F10169FA22046F152ED1D0BC8C`.
- [x] Fazer Minha Lista observar o estado de conectividade e atualizar quando a rede voltar após uma interrupção.
- [x] Reutilizar a política de refresh ocioso para não cancelar uma requisição em andamento nem duplicar chamadas.
- [x] Adicionar teste unitário de regressão para uma lista stale ser reconciliada após o retorno da rede.
- [x] Executar a suíte instrumentada da Biblioteca na Android TV: 14/14, 0 skipped, 0 falhas.
- [x] Executar `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` com código 0 antes do bump.
- [x] Gerar e registrar o APK local `v1.3.37`, `versionCode=338`, 7.353.067 bytes, SHA-256 `CC613BFFDD9E63F65AF8E9BDF0C861C4700EA0D804CDFF2A9A25858F44A681DC`.
- [ ] Publicar release remota somente mediante autorização explícita; não publicar nesta rodada.

## 276. Atualização resiliente de Minha Lista/Favoritos na TV (v1.3.36)
- [x] Conferir a versão anterior antes do bump: `v1.3.35`, `versionCode=336`, 7.353.067 bytes, SHA-256 `9F87A81CE26E38933C07851AC14F49E7F0CDB28A409653783C694C91353AC5AF`.
- [x] Reutilizar o scheduler resiliente da Biblioteca em Minha Lista/Favoritos, preservando atualização automática na Android TV após falha transitória.
- [x] Preservar cancelamento pelo lifecycle e evitar polling fora do foreground.
- [x] Executar a suíte instrumentada da Biblioteca na Android TV: 14/14, 0 skipped, 0 falhas.
- [x] Executar `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease` com código 0.
- [x] Gerar e registrar o APK local `v1.3.36`, `versionCode=337`, 7.353.067 bytes, SHA-256 `2AEF6EADBD1109FBA95BDA678CFF89B7A8DAC4F10169FA22046F152ED1D0BC8C`.
- [ ] Publicar release remota somente mediante autorização explícita; não publicar nesta rodada.

## 247. Matriz real de UX por dispositivo (validação do APK v1.3.7)
- [x] Tablet Android 15 (`MulletaflixTabletApi35`): Home 9/9 e Biblioteca 11/11, 0 skipped, 0 failed.
- [x] Android TV 14 (`MulletaflixTvApi34`): Home 9/9 e Biblioteca 11/11, 0 skipped, 0 failed.
- [x] Confirmar que a execução dos AVDs termina automaticamente pelo `with-emulator.ps1`; nenhum processo de emulador permaneceu aberto.
- [x] Nenhuma alteração de código ou nova release foi criada nesta rodada; a versão validada continua sendo `v1.3.7`.

## Correção do mapeamento de faixas de áudio e legenda no player (APK v1.3.55, sem release)
- [x] Preservar a posição dos grupos Media3 não suportados durante o mapeamento do índice de stream informado pelo servidor.
- [x] Selecionar as representações suportadas do grupo correspondente sem fixar uma única variante de qualidade.
- [x] Só atualizar a faixa exibida e preferência salva quando a seleção correspondente tiver sucesso.
- [x] Cobrir grupos sem faixas suportadas e grupo correspondente com múltiplas variantes em testes unitários.
- [x] Rodar `testDebugUnitTest`, `:app:lintDebug` e `:app:assembleRelease`: BUILD SUCCESSFUL; 1.027 testes unitários, 0 falhas/erros/ignorados.
- [x] Rodar `:feature:player:connectedDebugAndroidTest` no AVD Android TV: 20 testes, 19 passaram, 1 ignorado (Cast), 0 falhas/erros; emulador fechado automaticamente.
- [x] Gerar APK local de validação (não publicado): versão `1.3.55`, `versionCode=356`, 7.404.867 bytes, SHA-256 `8E351825218BD96A9F4F46E524873F915FDC83D27546D62D0274C089D1D91F16`.
- [ ] Risco restante: validar com mídia real de múltiplas faixas que a ordem dos streams do servidor corresponde à ordem dos grupos Media3; a ordem 1:1 não está comprovada pela suíte atual.
- [ ] Publicar release/APK e atualizar notas apenas com melhorias/correções efetivamente incluídas e validadas; nenhuma release foi publicada nesta rodada.
