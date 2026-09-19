# MulletaFlix Android - Plano de Desenvolvimento & Checklist de Funcionalidades (TODO)

Este documento rastreia o status de implementação de todas as funcionalidades, módulos, telas e componentes do aplicativo oficial **MulletaFlix Android**.

---

## 🏛️ 1. Arquitetura & Infraestrutura (Clean Architecture + Multi-module)

- [x] **Configuração Gradle & Version Catalog (`libs.versions.toml`)**
  - [x] AGP 8.7.3, Kotlin 2.0.21, Compose BOM 2024.12.01, Media3 1.5.0, Hilt 2.53.1, Room 2.6.1, Retrofit 2.11.0, Moshi 1.15.2, Ktor Client
- [x] **Divisão Modular**
  - [x] `:app` (Orquestração, Navigation, Services, Splash, Cast Options)
  - [x] `:core:common` (Result/Resource, Coroutine Dispatchers Hilt Module, FormatUtils, NetworkMonitor)
  - [x] `:core:api` (Retrofit, Ktor, DTOs, Interceptors, WebSocket)
  - [x] `:domain` (Modelos puros, Interfaces de Repositórios, UseCases: `GetNextEpisodeUseCase`, `GetHomeFeedUseCase`, `SearchMediaUseCase`, `ManageDownloadsUseCase`, `GetLiveTvChannelsUseCase`, `GetLibraryItemsUseCase`, `GetItemDetailUseCase`, `ToggleFavoriteUseCase`, `TogglePlayedUseCase`, `ManagePlaylistUseCase`, `ManageSyncPlayUseCase`, `LoginUseCase`, `RegisterUseCase`, `VerifyServerUseCase`, `GetUserProfileUseCase`, `LogoutUseCase`, `SwitchUserUseCase`)
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
  - [x] Liberação imediata de decodificadores de hardware e codecs no `onCleared` (sem vazamento de memória)
  - [x] Tratamento de navegação no `BackHandler` retornando à tela anterior sem finalizar a Activity
- [x] **Interface OSD (On-Screen Display)**
  - [x] Controles modernos de play/pause, avançar/retroceder 10s
  - [x] Barra de progresso com visualização de capítulos e thumbnails de busca
  - [x] Botões "Pular Introdução" (Skip Intro) e "Pular Créditos" (Skip Credits)
  - [x] **Próximo Episódio Automático**: Descoberta inteligente do próximo episódio de séries (`GetNextEpisodeUseCase`), contagem regressiva visual de 5s, botão "Assistir Agora" e avanço automático ao fim do episódio
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
  - [x] Busca instantânea com debounce de digitação (350ms)
  - [x] Chips de filtro rápido por tipo de mídia (Filmes, Séries, Músicas, Pessoas)
  - [x] Histórico de buscas recentes com remoção individual por item e limpeza total
  - [x] Tratamento robusto de erros com card explicativo e botão "Tentar novamente"
  - [x] Navegação de retorno (`onBack`) com ícone na SearchBar
  - [x] Resultados agrupados por categoria com navegação direta

---

## 💾 8. Downloads & Reprodução Offline (`:feature:downloads`)

- [x] **Gerenciador de Downloads**
  - [x] Integração com `Media3 DownloadService` para downloads estáveis em segundo plano
  - [x] Notificação de progresso persistente com pausa e cancelamento
  - [x] Fila de downloads priorizada com controle de Wi-Fi apenas
- [x] **Experiência do Usuário & Armazenamento Local**
  - [x] Navegação com TopAppBar e botão de voltar (`onBack`)
  - [x] Estado vazio intuitivo com chamada para ação ("Explorar Catálogo")
  - [x] Diálogo de confirmação de exclusão prevenindo perda acidental de mídias baixadas
  - [x] Registro Room para itens baixados e reprodução offline direta no Player

---

## 📺 9. Live TV & Guia de Programação (EPG) (`:feature:live-tv`)

- [x] **Canais Ao Vivo**
  - [x] Lista de canais com logos oficiais e programas atuais
  - [x] Sintonização instantânea de stream de TV
- [x] **Guia Eletrônico de Programação (EPG)**
  - [x] Grade de horários por canal com navegação temporal
  - [x] Detalhes do programa ao vivo e sinopse
  - [x] Ação para agendar gravações (DVR) no servidor
- [x] **Arquitetura & Navegação**
  - [x] TopAppBar com navegação de retorno
  - [x] `GetLiveTvChannelsUseCase` orquestrando canais e gravações com isolamento de camada de apresentação

---

## 👥 10. SyncPlay (Sessões Sincronizadas) (`:feature:sync-play`)

- [x] **Salas de Sincronização**
  - [x] Criação e entrada em salas existentes via WebSocket do MulletaFlix
  - [x] Controle sincronizado de Play, Pause e Seek entre múltiplos participantes
  - [x] Lista de usuários conectados na sala com seus status de buffer
  - [x] Notificações em tela de ações de outros usuários

---

## 🎨 11. Design System, Temas & Visual (`:design-system`)

- [x] **Paleta Oficial MulletaFlix**
  - [x] Cores primárias `#00A4DC`, fundos cinematográficos `#101010`
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

---

## 📺 13. Suporte a Android TV & Dispositivos de Sala

- [x] **Compatibilidade com Leanback & Smart TVs**
  - [x] Suporte a `android.software.leanback` com `required="false"` (híbrido TV + Mobile)
  - [x] Suporte a dispositivos sem touchscreen (`android.hardware.touchscreen` required="false")
  - [x] Banner oficial 16:9 (`@drawable/ic_tv_banner`) com proporções corretas para o Launcher da Android TV / Google TV
  - [x] Categoria de inicialização `android.intent.category.LEANBACK_LAUNCHER` no manifesto

---

## 📦 14. Otimização de Release, Assinatura & CI/CD

- [x] **Minificação & Otimização ProGuard / R8**
  - [x] Regras personalizadas em `app/proguard-rules.pro` para Moshi, Retrofit, Room, Media3 ExoPlayer, Hilt e Cast SDK
  - [x] Redução do tamanho final do APK de Release para **7.28 MB** com encolhimento agressivo de recursos
- [x] **Assinatura Automatizada**
  - [x] Bloco `signingConfigs` configurado com suporte a variáveis de ambiente (`KEYSTORE_PATH`, `KEYSTORE_PASSWORD`, etc.) e fallback transparente para debug, gerando APKs imediatamente instaláveis via sideload
- [x] **Integração com GitHub Actions CI**
  - [x] Job automatizado `android-build-test` configurado em `.github/workflows/ci.yml` com Java 17 Temurin, cache de dependências Gradle, execução de testes unitários e build de artefatos Debug e Release

---

## 🧪 15. Validação E2E em Dispositivo Real / Emulador (Gauntlet Loop)

- [x] **Autenticação Real com Servidor Remoto**: Login executado com credenciais reais (`Raphael`), token persistido e recuperação de sessão.
- [x] **Carregamento de Catálogo em Tempo Real**: Hero Banner dinâmico, carrosséis de Filmes e Séries alimentados pela API Jellyfin/MulletaFlix.
- [x] **Navegação & Detalhes**: Backdrop, pôster, metadados, classificação, elenco/equipe técnica e títulos similares renderizados.
- [x] **Player Media3 ExoPlayer**: Inicialização do player com controles de transporte, scrubbing, seletores de faixas de áudio, legendas e resolução.
- [x] **Busca Global**: Query com debounce (`"dias"`), filtros categorizados e resultados divididos entre filmes e outras mídias.
- [x] **TV Ao Vivo & EPG**: Listagem de canais remotos e guia de programação.
- [x] **Downloads Offline**: Interface de mídias baixadas e navegação rápida para o catálogo.
- [x] **Configurações & Temas**: Preferências de reprodução, legendas, tema escuro e informações de servidor.
- [x] **SyncPlay**: Lobbies de reprodução em grupo sincronizados.
- [x] **Perfil de Usuário & Limpeza de Cache**: Diagnóstico de latência (33 ms), limpeza de cache em tempo real (17.2 MB -> 0.0 MB) e encerramento de sessão com diálogo de confirmação.

---

## 🩹 16. Correções v1.0.5 (app Android)

Relatado em uso real: a biblioteca de Séries listava temporadas e episódios como cartões soltos
e abrir uma série fechava o aplicativo. Verificado no emulador (API 35) contra o servidor MulletaFlix real.

- [x] **Fechamento do app ao abrir série**: o seletor de temporadas (`PrimaryScrollableTabRow`) era medido
      com zero abas enquanto a consulta de temporadas ainda estava em andamento, lançando
      `IndexOutOfBoundsException: Index 0 out of bounds for length 0` (`TabRow.kt`) e derrubando o processo.
      O seletor agora só é desenhado quando existem temporadas; antes disso aparece indicador de carga e,
      sem temporadas, uma mensagem. Regressão coberta por teste instrumentado (`SeriesSectionTest`).
- [x] **Temporadas separadas da série**: a listagem de biblioteca consultava
      `Users/{userId}/Items?Recursive=true` sem `IncludeItemTypes`, devolvendo séries, temporadas e episódios
      no mesmo nível (270 itens na biblioteca de Séries do servidor, contra 10 séries reais). A consulta agora
      declara os tipos de navegação da biblioteca (`LibraryBrowseTypes`), seguindo a mesma regra do cliente web
      (`src/controllers/list.ts`).
- [x] **Temporada ou episódio abrem dentro da série**: entrar em uma temporada/episódio carrega a série pai,
      seleciona a temporada correspondente e exibe a lista de episódios.
- [x] **Botão Reproduzir em série**: tocava a série (item não reproduzível) e exibia
      "O servidor não conseguiu preparar esta média". Agora reproduz o primeiro episódio carregado e fica
      desabilitado quando não há episódio disponível.
- [x] **Faixas de álbum de música**: `Shows/{albumId}/Episodes` responde 404 para álbuns; as faixas agora são
      obtidas de `Items?ParentId={albumId}&IncludeItemTypes=Audio`.
- [x] **Avatar do usuário criado no servidor**: a imagem (`Users/{userId}/Images/Primary?tag=...`) passou a ser
      exibida no perfil e na troca rápida de usuários, com a letra inicial como fallback.
- [x] **Rótulo de episódio**: episódios sem numeração (extras) exibiam `nullx00`; agora mostram apenas o nome.
- [x] **Paginação da biblioteca**: `loadMore` usa a quantidade de itens já carregados como cursor, em vez de
      assumir que cada página veio cheia.

Pendências registradas nesta correção:

- [ ] Avatar do usuário na barra superior da Home (requer carregar o perfil no `HomeViewModel`).
- [ ] Avatares na tela de login para usuários públicos: o endpoint de imagem exige token, portanto só é
      possível exibir após autenticar.
- [ ] Biblioteca "TV ao Vivo" aberta pelo cartão de biblioteca retorna 0 itens em `Items?ParentId=` — o
      servidor não enumera os canais por esse endpoint (mesmo resultado antes desta correção); o caminho
      correto é a tela de TV Ao Vivo.

---

## 🖼️ 17. Capas das mídias iguais ao acesso web (v1.0.6)

- [x] **Capas das temporadas na série**: o seletor de temporadas era apenas texto. Agora cada temporada é um
      card com a capa vinda do servidor (`Items/{seasonId}/Images/Primary?tag=...`, com fallback para a capa
      da série via `SeriesPrimaryImageTag`), igual ao cliente web. Coberto por teste instrumentado
      (`SeriesSectionTest`, incluindo o estado "selecionada").
- [x] **Destaque da temporada selecionada**: contorno e nome na cor de acento (`secondary`). O destaque
      anterior usava `colorScheme.primary`, que no tema escuro padrão é o preto da marca
      (`DarkColorScheme.primary = MulletaFlixBlack`) e ficava invisível sobre superfícies escuras.
- [x] **Conferência das capas**: grades de Filmes/Séries, cartões de biblioteca da Home e lista de episódios
      carregam a capa de todo item que possui imagem no servidor — conferido item a item contra a API
      (`ImageTags`). Itens sem nenhuma imagem cadastrada (ex.: `BoJack Horseman`, `Series`) exibem o mesmo
      espaço reservado do cliente web.

Pendências relacionadas:

- [ ] Revisar o tema escuro padrão: `primary` é o preto da marca, então todo componente que usa
      `colorScheme.primary` como acento (botões preenchidos, chips, abas selecionadas) fica sem contraste.
      Trocar a cor primária muda a aparência de várias telas e deve ser decidido com o cliente.
- [ ] Badges de episódios não assistidos e faixa de anos (ex.: "2022 - Presente") que o cliente web mostra
      nos cards: exigem mapear `RecursiveUnplayedItemCount` / `PremiereDate`+`EndDate` no DTO.




