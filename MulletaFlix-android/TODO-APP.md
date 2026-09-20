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
  - [x] Minha Lista com favoritos sincronizados ao servidor
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

- [x] Avatar do usuário na barra superior da Home: o `HomeViewModel` carrega o perfil em paralelo ao feed,
      e a barra usa a imagem autenticada com fallback acessível.
- [x] Avatares na tela de login para usuários públicos: o endpoint de imagem pode exigir token; a tela agora
      tenta a imagem autenticada e exibe a inicial do usuário como fallback acessível quando a imagem não está
      disponível (`UserAvatarFallbackTest`).
- [x] Diagnóstico de conexão nas configurações: a ação "Testar conexão" consulta o servidor configurado e
      exibe estado, latência e versão, com cobertura para sucesso e falha no `SettingsViewModelTest`.
- [x] Proteção contra downloads duplicados: títulos já enfileirados, em andamento ou concluídos não são
      reenviados ao `DownloadManager`; itens com falha ou em remoção continuam podendo ser solicitados
      novamente (`DownloadRequestPolicyTest`).
- [x] Histórico de buscas persistente e isolado por usuário: os 10 termos mais recentes são salvos no
      DataStore local e continuam disponíveis após reabrir o aplicativo, sem misturar contas.
- [x] Biblioteca "TV ao Vivo" abre diretamente a tela dedicada de TV ao Vivo, evitando `Items?ParentId=`;
      o servidor não enumera os canais por esse endpoint e a tela dedicada usa `LiveTv/Channels`.

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

- [x] Revisar o tema escuro padrão: o preto permanece como `primary` da marca, enquanto ações, ícones e
      seleções usam `secondary` vermelho para manter contraste e acessibilidade.
- [x] Badges de episódios não assistidos e faixa de anos (ex.: "2022 - Presente") que o cliente web mostra
      nos cards: exigem mapear `RecursiveUnplayedItemCount` / `PremiereDate`+`EndDate` no DTO.

---

## 🛡️ 18. Resiliência de reprodução (v1.0.7)

- [x] **Reconexão automática do player**: falhas transitórias de rede são repetidas quando a conectividade
      volta, preservando a posição atual; mídias offline e falhas permanentes não entram nesse ciclo.
- [x] **Estado visível de conectividade**: o player exibe um aviso acessível enquanto está sem rede e tenta
      recuperar o stream automaticamente.
- [x] **Política coberta por testes**: cenários de restauração de rede, reprodução offline e erro permanente.

---

## 🎨 19. Ícone do aplicativo (v1.0.7)

- [x] Launcher usa o logo oficial com transparência e a silhueta externa original.
- [x] Fundo do adaptive icon é transparente; o APK não desenha disco ou círculo ao redor do logo.

---

## 📡 20. Identidade do servidor na descoberta LAN (v1.0.8)

- [x] A verificação do endpoint persiste o `serverId` retornado pelo servidor.
- [x] Servidores salvos mantêm a identidade para que a recuperação LAN não selecione outro servidor da rede.
- [x] Teste de autenticação cobre a persistência do identificador verificado.
## 21. Minha Lista dedicada (v1.0.10)

- [x] Criar rota dedicada `main/favorites` para favoritos completos do usuário.
- [x] Carregar favoritos usando `IsFavorite`, ordenação alfabética e paginação do servidor.
- [x] Adicionar estados de carregamento, vazio, erro recuperável e atualização manual.
- [x] Adicionar atalho com ícone de favorito na barra superior da Home.
- [x] Cobrir carregamento, filtro, paginação e falha de página no `FavoritesViewModelTest`.

## 22. Descoberta LAN segura em sessões legadas (v1.0.11)

- [x] Manter a conexão automática quando somente um servidor responde na LAN.
- [x] Evitar selecionar silenciosamente o primeiro servidor quando múltiplos servidores respondem sem `serverId` persistido.
- [x] Cobrir seleção única e ambiguidade de múltiplos servidores em `LanServerRecoveryPolicyTest`.

## 23. Atualizador Android somente com releases estáveis (v1.0.12)

- [x] Ignorar releases GitHub marcadas como `draft` ou `prerelease`.
- [x] Evitar oferecer APKs de teste pelo atualizador integrado.
- [x] Cobrir o filtro no `AppUpdateRepositoryTest`.

## 24. Regressão do cadastro na tela de login (v1.0.13)

- [x] Garantir no teste instrumentado que o fluxo de login exibe a ação `Cadastrar`.
- [x] Manter a verificação do branding, URL padrão e ação `Entrar` no mesmo smoke test.

## 25. Versão do cliente no cabeçalho de autenticação (v1.0.14)

- [x] Centralizar a versão do APK no Version Catalog do Gradle.
- [x] Remover a versão fixa `12.0.2` do cabeçalho `MediaBrowser`.
- [x] Cobrir os cabeçalhos anônimo e autenticado em `AuthHeaderTest`.

## 26. Preferências manuais de idioma no player (v1.0.15)
- [x] Persistir o idioma técnico da faixa quando o usuário troca o áudio.
- [x] Persistir o idioma técnico da legenda quando o usuário troca a legenda.
- [x] Persistir a opção de legendas desativadas para as próximas reproduções.
- [x] Manter o rótulo visual separado do idioma usado pela seleção automática.

## 27. Qualidade persistente e plataforma atualizada (v1.0.16)
- [x] Persistir a qualidade escolhida manualmente no player para as próximas reproduções.
- [x] Atualizar Media3 para a versão estável mais recente disponível no ciclo atual.
- [x] Atualizar Core KTX e DataStore para as versões estáveis atuais.
- [x] Preparar o APK para Android 16 (targetSdk 36) mantendo minSdk 24.

## 28. Progresso de reprodução completo (v1.0.17)

- [x] Enviar os índices atuais de áudio e legenda também nos relatórios periódicos de progresso.
- [x] Evitar que o servidor perca a preferência de faixa durante uma sessão longa de reprodução.

## 29. Notas de atualização legíveis (v1.0.18)

- [x] Renderizar títulos, listas e negrito das notas de release no APK.
- [x] Tornar o bloco de novidades rolável em diálogos pequenos.
- [x] Cobrir o parser e a formatação de negrito com testes unitários.

## 30. Deep links de mídia (v1.0.19)

- [x] Abrir links `mulletaflix://details?id=...` diretamente no detalhe da mídia.
- [x] Aceitar links oficiais `http(s)://mulletaflix.duckdns.org:8096/web/#/details?id=...`.
- [x] Preservar o destino durante a seleção do servidor e concluir a navegação após o login.
- [x] Cobrir parsing, validação de host e rejeição de links externos com testes unitários.

## 31. Deep links com o APK em execução (v1.0.20)

- [x] Entregar novos links à `MainActivity` existente com `singleTop` e `onNewIntent`.
- [x] Navegar para outro detalhe sem reiniciar o processo nem duplicar a atividade.
- [x] Não ultrapassar autenticação quando o link chega antes do login.
- [x] Cobrir a política de navegação com testes unitários e validar a entrega real no AVD.

## 32. Controles de mídia externos (v1.0.21)

- [x] Encaminhar eventos `MEDIA_BUTTON` para o `MediaSessionService` via receiver oficial do Media3.
- [x] Remover o receiver inerte que recebia comandos Bluetooth/fone sem despachá-los para a sessão.
- [x] Validar a resolução do receiver no APK instalado no AVD.

## 33. Metadados de reprodução externos (v1.0.22)

- [x] Exibir título contextual de série/temporada/episódio nos controles Media3.
- [x] Propagar descrição e capa autenticada para a sessão de mídia e destinos Cast.
- [x] Manter título correto também na reprodução offline.
- [x] Cobrir a formatação de filmes e episódios com testes unitários.

## 34. Metadados preservados no fallback de transcodificação (v1.0.23)

- [x] Manter título, descrição e capa quando o player troca do Direct Play para transcodificação após uma falha.
- [x] Limpar o estado de metadados ao alternar entre mídia remota e download offline.

## 35. Foco de áudio e desconexão de fones (v1.0.24)

- [x] Solicitar foco de áudio automaticamente para reprodução de mídia.
- [x] Pausar o player quando fones ou dispositivos Bluetooth forem desconectados.
- [x] Aplicar a mesma política ao player de fallback do serviço em segundo plano.

## 36. Retomada de downloads offline (v1.0.25)

- [x] Persistir a posição local de mídias baixadas entre sessões do aplicativo.
- [x] Retomar a reprodução offline a partir da última posição salva.
- [x] Remover a posição persistida quando a mídia chega ao fim.
- [x] Evitar que a chave local exponha URL ou token de acesso.

## 37. Player widescreen responsivo (v1.0.26)

- [x] Entrar no player em orientação paisagem sensorizada para mídias widescreen.
- [x] Restaurar a orientação anterior ao sair do player, sem alterar a navegação do restante do aplicativo.
- [x] Cobrir a política de orientação para entradas retrato, indefinida e paisagem.

## 38. Retomada remota resiliente (v1.0.27)

- [x] Persistir localmente a última posição remota por usuário e mídia enquanto o servidor sincroniza.
- [x] Priorizar o progresso oficial do servidor quando ele estiver disponível.
- [x] Usar o fallback local somente quando o progresso do servidor estiver ausente ou zerado.
- [x] Remover a posição local ao concluir a mídia e manter chaves sem URL, token ou identificador em texto.
- [x] Cobrir prioridade, fallback, isolamento por usuário/mídia e valores inválidos com testes unitários.

## 39. Velocidade de reprodução persistente (v1.0.28)

- [x] Persistir a velocidade escolhida no OSD para as próximas reproduções.
- [x] Normalizar valores finitos entre 0,5x e 2x, com fallback seguro para 1x.
- [x] Cobrir limites, valores inválidos e o valor padrão com testes unitários.

## 40. Backoff de reconexão do player (v1.0.29)

- [x] Repetir falhas transitórias de rede até três vezes, sem repetir erros permanentes.
- [x] Aplicar backoff limitado de 750 ms, 1,5 s e 3 s preservando a posição atual.
- [x] Cobrir limite de tentativas e atrasos com testes unitários.

## 41. MIME explícito para reprodução e Cast (v1.0.30)

- [x] Informar o MIME type conhecido no `MediaItem` para HLS, MP4, WebM e Matroska.
- [x] Permitir que Media3 faça sniffing quando o container não for reconhecido.
- [x] Cobrir URL HLS, container informado e fallback de formatos desconhecidos.

## 42. Cards de mídia acessíveis (v1.0.31)

- [x] Agrupar imagem, badges e título do `MediaCard` em um único alvo semântico.
- [x] Anunciar o card como botão com a ação "Abrir <título>".
- [x] Informar explicitamente quando o conteúdo estiver ao vivo.
- [x] Cobrir o anúncio e a ação de toque com teste instrumentado no AVD.

## 43. Preferência de idioma por rótulo regional (v1.0.32)

- [x] Normalizar rótulos humanizados como `Português (Brasil)` e `English (US)`.
- [x] Preservar a compatibilidade com códigos ISO e nomes simples.
- [x] Cobrir áudio/legenda com nomes regionais no teste unitário do player.

## 44. Menu de faixas selecionável e acessível (v1.0.33)

- [x] Transformar cada linha de áudio/legenda em uma opção de rádio semântica.
- [x] Tornar a linha inteira acionável e remover o clique duplicado do `RadioButton`.
- [x] Cobrir a seleção de faixa com teste instrumentado no AVD.

## 45. Temporizador de suspensão do player (v1.0.34)

- [x] Oferecer pausas automáticas de 15, 30, 45, 60 e 90 minutos.
- [x] Exibir a contagem regressiva no conteúdo do menu e no estado semântico do ícone.
- [x] Cancelar o temporizador com segurança ao trocar de mídia ou sair do player.
- [x] Cobrir limites e formatação do temporizador com testes unitários.

## 46. Busca acessível de 10 segundos no player (v1.0.35)

- [x] Expor botões explícitos para voltar e avançar 10 segundos.
- [x] Limitar o alvo de busca ao intervalo válido da mídia.
- [x] Manter os gestos existentes e cobrir os novos limites com teste unitário.

## 47. Visibilidade de senha no cadastro (v1.0.36)

- [x] Permitir revelar/ocultar a senha do cadastro sem alterar o valor digitado.
- [x] Permitir revelar/ocultar a confirmação de forma independente.
- [x] Cobrir os dois controles de visibilidade com teste instrumentado.

## 48. Estado de pausa no progresso remoto (v1.0.37)

- [x] Reportar ao servidor quando a mídia estiver pausada durante a sincronização periódica.
- [x] Manter o estado ativo quando o player estiver reproduzindo.
- [x] Cobrir os dois estados com teste unitário.

## 49. Atualizador Android resiliente a releases do servidor (v1.0.38)

- [x] Consultar até 100 releases do GitHub para não perder releases do APK atrás das releases do servidor.
- [x] Manter o filtro exclusivo de tags `app-*`, sem oferecer pacotes do servidor no APK.
- [x] Cobrir a separação entre canais Android e servidor com teste unitário.

## 50. Recuperação automática do Home após conexão (v1.0.39)

- [x] Detectar a transição de offline para online sem repetir atualizações em estados estáveis.
- [x] Recarregar o feed do Home automaticamente quando a rede voltar.
- [x] Cobrir a transição com teste unitário.

## 51. Ação Cast acessível no player (v1.0.40)

- [x] Agrupar ícone e rótulo de transmissão em um único alvo semântico.
- [x] Informar que a ação transmite para um dispositivo compatível.
- [x] Cobrir a descrição unificada com teste instrumentado.

## 52. Proporção do player persistente (v1.0.41)

- [x] Persistir a proporção escolhida no player entre mídias e sessões.
- [x] Restaurar a preferência com fallback seguro para Ajustar (Original).
- [x] Cobrir valores válidos, antigos e inválidos com teste unitário.

## 53. Busca de downloads offline (v1.0.42)

- [x] Filtrar downloads localmente por título sem alterar a fila original.
- [x] Exibir estado vazio específico para buscas sem resultado.
- [x] Cobrir busca sem distinção de maiúsculas/minúsculas e consulta vazia.

## 54. Menus do player roláveis e sem clique duplicado (v1.0.46)

- [x] Permitir rolagem vertical nos menus de qualidade, velocidade, temporizador e proporção.
- [x] Usar uma única ação semântica de rádio por opção, incluindo o alvo de toque da linha inteira.
- [x] Cobrir uma lista longa de qualidades no teste instrumentado do player.

## 55. Barra de ações do player responsiva (v1.0.47)

- [x] Permitir rolagem horizontal das ações superiores em telas estreitas e fontes ampliadas.
- [x] Manter o título e o botão de voltar fora da área rolável.
- [x] Cobrir a navegação horizontal da faixa de ações no teste instrumentado.

## 56. Cadastro rolável em telas pequenas (v1.0.48)

- [x] Limitar e permitir rolagem vertical do conteúdo do diálogo de cadastro.
- [x] Manter mensagens de erro extensas alcançáveis com o teclado e fontes ampliadas.
- [x] Cobrir a rolagem do diálogo com teste instrumentado.

## 57. Dados técnicos do player roláveis (v1.0.49)

- [x] Limitar o diálogo de informações técnicas para não ultrapassar telas pequenas.
- [x] Permitir rolagem vertical para codecs, resolução, bitrate e método de reprodução.
- [x] Cobrir informações extensas com teste instrumentado.

## 58. Compartilhamento de diagnóstico do player (v1.0.50)

- [x] Formatar método, codecs, resolução e bitrate em texto estável.
- [x] Permitir copiar os dados técnicos para a área de transferência.
- [x] Cobrir a ação de copiar no teste instrumentado e o formato no teste unitário.

## 59. Confirmação de diagnóstico copiado (v1.0.51)

- [x] Confirmar visualmente a cópia dos dados técnicos com o estado “Copiado”.
- [x] Evitar que a ação pareça não executada em telas com feedback transitório.
- [x] Cobrir a confirmação com teste instrumentado.

## 60. Compartilhamento nativo de diagnóstico (v1.0.52)

- [x] Expor os dados técnicos pela folha nativa de compartilhamento do Android.
- [x] Usar texto simples compatível com mensageiros, e-mail e ferramentas de suporte.
- [x] Cobrir a ação de compartilhamento no teste instrumentado.

## 61. Diagnóstico identificado por mídia (v1.0.53)

- [x] Incluir o título da mídia no texto copiado e compartilhado.
- [x] Preservar o formato técnico para suporte e reprodução do problema.
- [x] Cobrir a identificação no teste unitário e no diálogo instrumentado.

## 62. Integridade do atualizador Android (próxima release)

- [x] Ler o digest SHA-256 publicado pelo GitHub para o asset APK.
- [x] Validar o APK baixado antes de disponibilizá-lo para instalação.
- [x] Rejeitar digest inválido ou divergente e manter compatibilidade com releases antigas sem digest.
- [x] Cobrir digest correto, incorreto e malformado com teste unitário.
- [x] Ignorar releases com campo de digest presente, mas malformado, antes do download.

## 63. Capas nos downloads offline (próxima release)

- [x] Persistir a referência da capa junto ao item da fila de downloads.
- [x] Exibir a capa autenticada na lista de downloads quando o servidor estiver acessível.
- [x] Manter o fallback por ícone e título para entradas antigas ou sem imagem.
- [x] Cobrir a propagação da referência de capa no caso de uso e no detalhe da mídia.

## 64. Capas verticais completas para filmes e séries (próxima release)

- [x] Renderizar filmes e séries em cards de pôster vertical `2:3` nas grades e favoritos.
- [x] Preservar a capa inteira com `ContentScale.Fit` nos cards de pôster.
- [x] Aplicar a mesma proporção vertical aos resultados de busca.

## 65. Regra única para apresentação de capas (próxima release)

- [x] Centralizar no domínio a classificação de mídias que usam arte de pôster.
- [x] Reutilizar a regra em biblioteca, favoritos e busca.
- [x] Cobrir filmes, séries, episódios e TV ao vivo com testes unitários.

## 66. Carrossel adaptativo da Home (próxima release)

- [x] Exibir filmes e séries retomados com cards verticais no “Continuar Assistindo”.
- [x] Manter episódios e canais em cards paisagem nos carrosséis apropriados.
- [x] Cobrir a política de formato da Home com testes unitários.

## 67. Identificação de episódios nos cards (v1.0.60)

- [x] Exibir temporada e episódio em cards de episódios quando a numeração estiver disponível.
- [x] Manter o metadado opcional para não alterar cards de filmes, séries e canais.
- [x] Cobrir numeração completa e incompleta com testes unitários.

## 68. Metadados de episódios consistentes (v1.0.61)

- [x] Centralizar a regra de temporada/episódio no domínio.
- [x] Reutilizar o metadado em Home, busca, biblioteca, favoritos e detalhe.
- [x] Cobrir temporadas, episódios e numeração incompleta com testes unitários.

## 69. Marca com contraste MULLETA/FLIX (v1.0.62)

- [x] Exibir “MULLETA” em vermelho e “FLIX” em branco no cabeçalho da Home.
- [x] Preservar um único texto semântico “MULLETAFLIX” para acessibilidade e testes.
- [x] Manter contraste adequado sobre o fundo preto.

## 70. Progresso seguro nos cards (v1.0.63)

- [x] Limitar o progresso recebido do servidor ao intervalo aceito pelo Compose.
- [x] Tratar valores `NaN` e infinitos como progresso inexistente.
- [x] Cobrir valores negativos, normais, excedentes e não finitos com testes unitários.

## 71. Progresso unificado em toda a UI (v1.0.64)

- [x] Centralizar a conversão percentual do servidor em uma fração segura no domínio.
- [x] Aplicar a mesma normalização nos cards da Home, biblioteca, favoritos e episódios.
- [x] Cobrir valores ausentes, negativos, excedentes e não finitos com testes unitários.

## 72. Faixas de áudio e legendas sem opções inválidas (v1.0.65)

- [x] Representar ausência de faixa de áudio com índice `-1`, sem selecionar uma posição inexistente.
- [x] Desabilitar os controles de áudio e legendas quando o servidor não retornar faixas.
- [x] Reutilizar a cobertura existente de listas vazias na política de seleção de faixas.

## 73. Política de versionamento semântico

- [x] Manter `1.0.x` até `1.0.99`.
- [x] Usar `1.1.0` após `1.0.99`, sem criar `1.0.100`.
- [x] Continuar o incremento semântico (`1.1.1`…`1.1.99`, depois `1.2.0`).

## 74. Atualizador alinhado ao versionamento (v1.0.66)

- [x] Ordenar corretamente a transição `1.0.99` para `1.1.0`.
- [x] Ignorar releases Android inválidas com patch `100` ou superior.
- [x] Cobrir a transição e a filtragem com testes do parser de releases.

## 75. Download de atualização com origem confiável (v1.0.67)

- [x] Aceitar downloads somente do endpoint oficial de assets APK do GitHub.
- [x] Rejeitar HTTP, hosts externos e repositórios diferentes antes do download.
- [x] Sanitizar o nome local do APK e cobrir URLs confiáveis e maliciosas com testes.

## 76. Limpeza segura do updater (v1.0.68)

- [x] Fechar sempre a resposta HTTP após o download ou falha de rede.
- [x] Remover APKs parciais em erro HTTP, resposta vazia, hash inválido ou cancelamento.
- [x] Preservar o cancelamento da coroutine e cobrir a limpeza idempotente com teste unitário.

## 77. Feedback e nova tentativa do updater (v1.0.69)

- [x] Exibir no diálogo o motivo retornado quando o download da atualização falhar.
- [x] Manter o diálogo aberto após falha, sem exigir reinicialização do aplicativo.
- [x] Oferecer uma nova tentativa explícita e limpar o erro ao iniciar o novo download.
- [x] Preservar cancelamento de coroutine sem convertê-lo em erro visual.

## 78. Instalação de atualização com feedback (v1.0.70)

- [x] Não fechar o diálogo antes de confirmar que o instalador Android foi aberto.
- [x] Informar bloqueios de permissão ou falhas ao iniciar o instalador.
- [x] Permitir nova tentativa após uma instalação que não pôde ser iniciada.

## 79. Estabilidade da recuperação LAN (v1.0.71)

- [x] Comparar URLs descobertas ignorando espaços e barras finais equivalentes.
- [x] Evitar alternância repetida entre o mesmo endpoint LAN durante a recuperação.
- [x] Cobrir aliases de endpoint com teste unitário de regressão.

## 80. Atualizador consistente nas Configurações (v1.0.72)

- [x] Aplicar na tela de Configurações o mesmo feedback de instalação usado na Home.
- [x] Manter o diálogo aberto quando o instalador Android não puder ser iniciado.
- [x] Exibir a falha e oferecer “Tentar novamente” sem reiniciar o aplicativo.

## 81. Permissão de notificações no player (v1.0.73)

- [x] Detectar a permissão runtime de notificações em Android 13 ou superior.
- [x] Exibir aviso não bloqueante para ativar controles de mídia e notificações de downloads.
- [x] Permitir dispensar o aviso sem interromper a reprodução.
- [x] Cobrir a política por versão do Android e estado da permissão com testes unitários.

## 82. Lembrança da preferência de notificações (v1.0.74)

- [x] Persistir a dispensa do aviso de notificações no armazenamento local do APK.
- [x] Não reapresentar o aviso após “Agora não” ou recusa da permissão.
- [x] Manter o aviso disponível para usuários que ainda não decidiram.
- [x] Cobrir a política de apresentação para permissão ausente e aviso dispensado.

## 83. Descoberta LAN resiliente (v1.0.75)

- [x] Repetir sondagens UDP durante a janela de descoberta sem aumentar o timeout total.
- [x] Manter a descoberta responsiva com intervalos curtos de recepção.
- [x] Cobrir a agenda de tentativas e intervalos inválidos com testes unitários.

## 84. Pausa persistente da fila offline (v1.0.76)

- [x] Persistir o estado de pausa da fila de downloads no armazenamento local.
- [x] Restaurar a pausa ao recriar o processo do aplicativo.
- [x] Expor o estado persistido por `Flow` para manter a tela sincronizada.
- [x] Cobrir pausa, retomada e falha da operação nos testes do módulo.

## 85. Atualização por gesto na biblioteca (v1.0.77)

- [x] Adicionar pull-to-refresh à tela de biblioteca.
- [x] Separar o estado de atualização da paginação para não exibir feedback incorreto.
- [x] Manter o botão de atualização manual e o retry de erro compatíveis com o gesto.

## 86. Atualização por gesto em Minha Lista (v1.0.78)

- [x] Conectar o estado `isRefreshing` existente ao indicador visual da tela.
- [x] Adicionar pull-to-refresh aos favoritos sem interferir na paginação.
- [x] Preservar o botão manual, retry e estados vazio/erro.

## 87. EPG rolável no Live TV (v1.0.79)

- [x] Renderizar a programação em uma lista virtualizada dentro do diálogo.
- [x] Limitar a altura do EPG e permitir rolagem vertical para muitos programas.
- [x] Preservar o agendamento individual e o estado “Agendado”.

## 88. Histórico de busca rolável (v1.0.80)

- [x] Renderizar o histórico em `LazyColumn` para suportar vários itens.
- [x] Preservar seleção, remoção individual e limpeza completa.
- [x] Exibir estado vazio orientando o usuário sem conteúdo oculto.

## 89. Busca sem resultados obsoletos (v1.0.81)

- [x] Cancelar a consulta anterior ao iniciar uma nova busca, troca de filtro ou nova tentativa.
- [x] Identificar cada requisição com uma geração para bloquear respostas atrasadas.
- [x] Cobrir a corrida entre consultas com teste unitário controlado.

## 90. Atualização por gesto na busca (v1.0.82)

- [x] Adicionar pull-to-refresh aos resultados sem sair da consulta atual.
- [x] Manter os resultados visíveis enquanto a atualização está em andamento.
- [x] Cobrir o estado de atualização e a substituição dos resultados após o refresh.

## 91. Rolagem estável na Home (v1.0.83)

- [x] Adicionar chaves estáveis aos cards de mídia nas fileiras horizontais.
- [x] Adicionar chaves estáveis aos cards de bibliotecas.
- [x] Evitar que atualizações do servidor troquem a identidade visual dos itens e desloquem a rolagem.
