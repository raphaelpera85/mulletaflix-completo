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

## 92. Rolagem estável na biblioteca (v1.0.84)

- [x] Adicionar chaves estáveis aos itens da grade/lista da biblioteca.
- [x] Preservar a identidade visual durante paginação, filtros e atualização.
- [x] Alinhar o comportamento da biblioteca com Home, Favoritos e Live TV.

## 93. Preferência persistente de visualização da biblioteca (v1.0.85)

- [x] Persistir a escolha entre grade e lista no DataStore local.
- [x] Restaurar a preferência ao abrir qualquer biblioteca.
- [x] Cobrir restauração e gravação da preferência com teste unitário.

## 94. Ordenação persistente da biblioteca (v1.0.86)

- [x] Persistir a ordenação escolhida no DataStore local.
- [x] Restaurar a ordenação ao abrir a biblioteca.
- [x] Cobrir restauração e gravação da ordenação com teste unitário.

## 95. Filtros persistentes da biblioteca (v1.0.87)

- [x] Persistir Favoritos, Assistidos e Não assistidos no DataStore local.
- [x] Restaurar os filtros ao reabrir a biblioteca.
- [x] Cobrir restauração, gravação e limpeza dos filtros com teste unitário.

## 96. Ordem determinística dos filtros (v1.0.88)

- [x] Normalizar filtros restaurados para a ordem visual estável.
- [x] Ignorar valores desconhecidos persistidos.
- [x] Cobrir a normalização com teste unitário.

## 97. Formato de capa por tipo na biblioteca (v1.0.89)

- [x] Manter filmes e séries com capa vertical completa.
- [x] Exibir episódios e canais com arte horizontal completa também na lista.
- [x] Cobrir o mapeamento de formato com testes de apresentação.

## 98. Wordmark oficial nas telas do aplicativo (v1.0.90)

- [x] Centralizar a composição `MULLETA` vermelho + `FLIX` branco no design system.
- [x] Aplicar o wordmark na tela de login e na Home.
- [x] Cobrir as cores e os limites dos dois trechos com teste unitário.

## 99. Preservação integral das artes nos cartões (v1.0.91)

- [x] Preservar pôsteres e artes horizontais completas no componente compartilhado `MediaCard`.
- [x] Manter o preenchimento intencional em cartões quadrados e banners.
- [x] Cobrir as políticas de escala com testes unitários.

## 100. Wordmark oficial no fluxo inicial (v1.0.92)

- [x] Aplicar o wordmark oficial na seleção do servidor.
- [x] Manter o mesmo branding na seleção, login e Home.
- [x] Atualizar o smoke test instrumentado para validar `MULLETAFLIX`.

## 101. Conectividade LAN sem internet externa (v1.0.93)

- [x] Não exigir `NET_CAPABILITY_VALIDATED` para considerar uma rede local utilizável.
- [x] Preservar a recuperação automática do servidor quando o Wi-Fi não possui internet externa.
- [x] Cobrir a política de conectividade local com testes unitários.

## 102. Latência real no perfil (v1.0.94)

- [x] Remover o valor de latência fictício exibido antes da verificação do servidor.
- [x] Exibir `Conectado` enquanto ainda não houver uma medição real.
- [x] Cobrir latência válida, ausente e inválida com testes unitários.

## 103. Imagens absolutas autenticadas (v1.0.95)

- [x] Anexar o token somente às imagens absolutas do mesmo servidor selecionado.
- [x] Preservar imagens externas/CDN sem expor o token da sessão.
- [x] Evitar duplicação de `api_key` quando a URL já estiver autenticada.
- [x] Cobrir os cenários de servidor local, CDN externo e token existente.

## 104. Preferência de idioma do áudio (v1.0.96)

- [x] Adicionar a seleção persistente de Português, Inglês ou idioma original nas configurações.
- [x] Conectar a preferência ao mesmo fluxo usado pelo player para escolher a faixa de áudio.
- [x] Restaurar a escolha ao reabrir as configurações.
- [x] Cobrir restauração e gravação da preferência com teste unitário.

## 105. Qualidades avançadas na configuração (v1.0.97)

- [x] Expor 4K e 1440p na preferência de qualidade padrão.
- [x] Manter as restrições de qualidade alinhadas ao player Media3.
- [x] Normalizar valores persistidos e substituir preferências inválidas por Automático.
- [x] Cobrir as opções e a normalização com testes unitários.

## 106. Preferências de qualidade canônicas no player (v1.0.98)

- [x] Normalizar aliases persistidos como `2160p`, `1440`, `FULL HD`, `HD` e `SD`.
- [x] Aplicar rótulos canônicos antes de criar as restrições de vídeo do Media3.
- [x] Evitar divergência entre a preferência restaurada e a opção selecionada na interface do player.
- [x] Cobrir aliases válidos e valores inválidos com testes unitários.

## 107. Qualidade disponível refletida no player (v1.0.99)

- [x] Evitar que uma preferência salva sem faixa correspondente fique sem seleção visual.
- [x] Exibir Automático quando a mídia atual não possui a resolução preferida.
- [x] Preservar o limite máximo configurado sem inventar opções de qualidade inexistentes.
- [x] Cobrir a seleção efetiva com testes unitários.

## 108. Mensagens de erro de reprodução acionáveis (v1.1.0)

- [x] Traduzir falhas de rede, decodificação, arquivo ausente e DRM para mensagens orientadas ao usuário.
- [x] Preservar o retry automático para falhas transitórias de rede.
- [x] Usar uma mensagem segura quando o Media3 não fornecer detalhes úteis.
- [x] Cobrir os grupos de erro e os fallbacks com testes unitários.

## 109. Proporção global da imagem (v1.1.1)

- [x] Expor Ajustar, Preencher/Zoom e Esticar nas configurações.
- [x] Persistir a proporção selecionada no DataStore existente.
- [x] Restaurar e normalizar valores antigos ou inválidos com segurança.
- [x] Cobrir restauração e persistência no teste do SettingsViewModel.

## 110. Feedback de limpeza de cache (v1.1.2)

- [x] Informar visualmente quando o cache de imagens terminar de ser removido.
- [x] Manter a limpeza restrita ao cache local do aplicativo.
- [x] Cobrir a mensagem de conclusão no teste do SettingsViewModel.

## 111. Confirmação para limpeza total (v1.1.3)

- [x] Exigir confirmação explícita antes de remover dados locais e encerrar a sessão.
- [x] Informar claramente o impacto e a irreversibilidade da ação.
- [x] Manter Cancelar como saída segura sem alterar preferências ou sessão.

## 112. Temporizador de suspensão consistente no player (v1.1.4)

- [x] Marcar no diálogo a duração atualmente selecionada.
- [x] Limpar o temporizador visual ao trocar de mídia ou cancelar a contagem.
- [x] Manter o estado da contagem regressiva e da duração sincronizados.
- [x] Cobrir a seleção da duração com teste unitário.

## 113. Pausa ao fim da mídia (v1.1.5)

- [x] Oferecer a opção de pausar ao fim do filme ou episódio atual.
- [x] Impedir o avanço automático para o próximo episódio quando essa opção estiver ativa.
- [x] Exibir o modo ativo no acessível do botão do temporizador.
- [x] Cobrir o rótulo do novo modo com teste unitário.

## 114. Capa completa no detalhe do título (v1.1.6)

- [x] Exibir a capa vertical completa de filmes e séries no detalhe.
- [x] Preservar a imagem inteira com `ContentScale.Fit`, sem recorte da arte.
- [x] Manter ações, título e metadados acessíveis em telas estreitas com rolagem horizontal.

## 115. Rolagem de metadados no detalhe (v1.1.7)

- [x] Permitir rolagem horizontal dos badges de ano, qualidade, HDR e áudio.
- [x] Evitar que metadados sejam cortados em telas estreitas.
- [x] Cobrir a ação de rolagem com teste Compose.

## 116. Cor configurável das legendas (v1.1.8)

- [x] Persistir a preferência de cor Branco, Amarelo ou Ciano.
- [x] Aplicar a cor selecionada no SubtitleView do Media3.
- [x] Manter contorno preto para legibilidade em cenas claras e escuras.
- [x] Cobrir normalização e mapeamento de cores com testes unitários.

## 117. Grade adaptativa da biblioteca (v1.1.9)

- [x] Adaptar a quantidade de colunas à largura real do dispositivo.
- [x] Preservar capas verticais inteiras em telas pequenas, tablets e orientação horizontal.
- [x] Oferecer densidades Confortável e Compacta nas configurações.
- [x] Persistir e normalizar a preferência da grade.
- [x] Cobrir a política de densidade com testes unitários.

## 118. Seletor de faixas multimídia informativo (v1.1.10)

- [x] Exibir codec e configuração de canais nas faixas de áudio.
- [x] Identificar faixa padrão e legenda forçada no seletor do player.
- [x] Preservar a seleção efetiva e os índices enviados ao servidor.
- [x] Cobrir a formatação dos rótulos com testes unitários.

## 119. Busca por voz no Android (v1.1.11)

- [x] Adicionar microfone opcional à busca, solicitando permissão somente ao tocar no botão.
- [x] Preencher e executar a busca com o primeiro resultado reconhecido.
- [x] Tratar indisponibilidade, permissão negada e falhas do reconhecimento com mensagens acionáveis.
- [x] Liberar o reconhecedor ao sair da tela.
- [x] Cobrir normalização do resultado e mensagens de erro com testes unitários.

## 120. Capas completas nos Downloads Offline (v1.1.12)

- [x] Preservar a capa inteira de filmes e séries na lista de downloads.
- [x] Evitar recorte da arte vertical no card compacto offline.
- [x] Cobrir a política de escala com teste unitário.

## 121. Paginação protegida na biblioteca (v1.1.13)

- [x] Fechar a janela de corrida entre o sentinel do Scroll e o início da requisição.
- [x] Impedir páginas duplicadas durante recomposição ou rolagem rápida.
- [x] Cobrir o bloqueio de nova página enquanto uma requisição está em andamento ou após o fim.

## 122. Recuperação de sessão durante paginação (v1.1.14)

- [x] Encerrar o carregamento quando a sessão expira durante a rolagem.
- [x] Exibir uma mensagem acionável em vez de deixar o Scroll em estado infinito.
- [x] Cobrir o caminho de sessão ausente com teste de regressão.

## 123. Repetir falhas da fila offline (v1.1.15)

- [x] Adicionar ação única para repetir todos os downloads que falharam.
- [x] Preservar a ordem e os metadados originais de cada item reenfileirado.
- [x] Exibir a quantidade de falhas no botão e cobrir a seleção com teste unitário.

## 124. Verificação visual da recuperação offline (v1.1.16)

- [x] Cobrir o botão de repetição em teste Compose instrumentado.
- [x] Confirmar que a ação fica oculta quando não há falhas.
- [x] Confirmar que o toque dispara exatamente o callback de recuperação.

## 125. Reenvio determinístico da fila offline (v1.1.17)

- [x] Repetir falhas a partir do snapshot renderizado, sem depender de coleta tardia do Flow.
- [x] Preservar a ordem dos IDs reenfileirados.
- [x] Cobrir a operação completa do ViewModel com teste unitário.

## 126. Agendamento protegido do EPG (v1.1.18)

- [x] Impedir gravações duplicadas causadas por toques rápidos no mesmo programa.
- [x] Cancelar requisições anteriores de atualização e guia quando uma nova é iniciada.
- [x] Cobrir o bloqueio de agendamento duplicado com teste unitário.

## 127. Agendamentos concorrentes do EPG (v1.1.19)

- [x] Permitir agendar programas diferentes simultaneamente sem cancelar requisições independentes.
- [x] Manter o bloqueio apenas para toques repetidos no mesmo programa.
- [x] Cobrir a preservação de dois agendamentos concorrentes com teste unitário.

## 128. Limpeza segura de downloads concluídos (v1.1.20)

- [x] Adicionar ação para remover todos os downloads concluídos em uma única operação.
- [x] Exigir confirmação explícita antes da limpeza em lote.
- [x] Preservar downloads em andamento, enfileirados e com falha.
- [x] Cobrir a seleção de concluídos e o encaminhamento ao repositório.

## 129. Descoberta LAN sem respostas obsoletas (v1.1.21)

- [x] Cancelar uma busca LAN anterior quando uma nova busca começa.
- [x] Impedir que uma resposta atrasada substitua o resultado da busca mais recente.
- [x] Cobrir a corrida entre buscas com teste unitário.

## 130. Consultas de busca normalizadas (v1.1.22)

- [x] Aparar espaços nas buscas manuais antes de atualizar estado e histórico.
- [x] Não criar histórico nem requisição para consultas vazias.
- [x] Cobrir normalização e rejeição de entradas somente com espaços.

## 131. Confirmação do histórico de buscas (v1.1.23)

- [x] Exigir confirmação antes de apagar todo o histórico do usuário.
- [x] Manter Cancelar como saída segura sem modificar o histórico.
- [x] Cobrir o diálogo e a ação de confirmação em teste Compose.

## 132. Limpeza de downloads com falha (v1.1.24)

- [x] Expor limpeza exclusiva de downloads falhos, sem afetar ativos ou concluídos.
- [x] Exigir confirmação antes de remover falhas da fila offline.
- [x] Cobrir a política de filtragem e delegação no ViewModel.

## 133. Regressão instrumentada da limpeza de falhas (v1.1.25)

- [x] Atualizar o teste Compose de `OfflineSummary` para os callbacks de limpeza.
- [x] Validar que “Limpar falhas” aparece somente quando há falhas.
- [x] Executar a suíte instrumentada do módulo no emulador API 35.

## 134. Proteção contra respostas obsoletas da biblioteca (v1.1.26)

- [x] Associar cada carregamento de biblioteca a uma geração de requisição.
- [x] Ignorar sucesso ou falha de respostas que já não pertencem à tela atual.
- [x] Cobrir resposta atrasada que ignora cancelamento com teste unitário.

## 135. Confirmação para limpar cache de imagens (v1.1.27)

- [x] Exigir confirmação antes de remover o cache visual local.
- [x] Manter Cancelar como saída segura sem chamar a limpeza.
- [x] Cobrir confirmação e cancelamento em teste Compose instrumentado.

## 136. Limpeza de cache fora da thread principal (v1.1.28)

- [x] Executar remoção e cálculo de diretórios de cache no dispatcher de I/O.
- [x] Reutilizar o qualifier `@IoDispatcher` nas ViewModels de Settings e Perfil.
- [x] Manter testes determinísticos com dispatcher controlado e preservar o cache de downloads offline.

## 137. Proteção contra consultas duplicadas de atualização (v1.1.29)

- [x] Bloquear uma nova verificação enquanto a consulta atual ou a instalação estiver em andamento.
- [x] Desabilitar a ação de atualização na tela durante esses estados.
- [x] Cobrir dois acionamentos consecutivos com teste unitário, garantindo uma única requisição.

## 138. Verificação de atualização resiliente (v1.1.30)

- [x] Exigir a versão instalada explicitamente, sem fallback silencioso para `1.0.0`.
- [x] Normalizar espaços da versão antes de consultar o canal de releases do APK.
- [x] Informar indisponibilidade ou versão ausente sem iniciar uma requisição inválida.
- [x] Garantir que exceções inesperadas do repositório liberem o estado de verificação.
- [x] Cobrir normalização, entrada vazia e exceção de rede com testes unitários.

## 139. Ordenação padrão da biblioteca nas configurações (v1.1.31)

- [x] Expor a ordenação persistida da biblioteca na tela de Configurações.
- [x] Oferecer nome, data de adição, data de lançamento, duração e avaliação.
- [x] Reutilizar os valores de ordenação compatíveis com a API do servidor.
- [x] Restaurar a preferência salva e cobrir sua persistência com teste unitário.

## 140. Proteção contra episódios obsoletos ao trocar de temporada (v1.1.32)

- [x] Cancelar o carregamento anterior de episódios quando uma nova temporada é selecionada.
- [x] Ignorar respostas atrasadas de temporadas que já não estão selecionadas.
- [x] Cobrir a corrida entre temporadas com teste unitário usando resposta suspensa.

## 141. Proteção contra detalhes obsoletos ao trocar de título (v1.1.33)

- [x] Cancelar o carregamento anterior ao abrir outro filme, série ou episódio.
- [x] Limpar os dados dependentes do título anterior enquanto o novo detalhe carrega.
- [x] Ignorar respostas atrasadas de detalhes, capas, extras e faixas do título anterior.
- [x] Cobrir a troca rápida de títulos com teste unitário concorrente.

## 142. Proteção contra downloads duplicados por toque repetido (v1.1.34)

- [x] Bloquear nova preparação enquanto o servidor ainda resolve a fonte do download.
- [x] Mostrar progresso no botão de download durante a preparação.
- [x] Ignorar atualizações de uma preparação antiga quando o título muda.
- [x] Cobrir dois toques consecutivos com teste unitário concorrente.

## 143. Proteção contra respostas obsoletas da Home (v1.1.35)

- [x] Associar cada carregamento ou atualização da Home a uma geração monotônica.
- [x] Ignorar respostas antigas mesmo quando a requisição não respeita cancelamento.
- [x] Preservar o conteúdo mais recente durante atualizações concorrentes.
- [x] Cobrir a corrida entre uma resposta antiga e uma atualização nova com teste unitário.

## 144. Mutação protegida de favoritos e status assistido (v1.1.36)

- [x] Bloquear toques repetidos enquanto o servidor atualiza favorito ou status assistido.
- [x] Mostrar progresso visual nas ações de detalhe durante a mutação.
- [x] Cancelar e invalidar mutações antigas ao abrir outro título.
- [x] Cobrir a proteção contra duas solicitações de favorito com teste unitário concorrente.

## 145. Paginação protegida de Minha Lista (v1.1.37)

- [x] Bloquear duas solicitações da mesma página causadas por toques rápidos.
- [x] Associar cada carga e refresh a uma geração monotônica.
- [x] Ignorar respostas atrasadas de refresh que já não representam a lista atual.
- [x] Cobrir paginação duplicada e refresh obsoleto com testes unitários concorrentes.

## 146. Guia da TV ao vivo protegido contra respostas obsoletas (v1.1.38)

- [x] Associar cada atualização de canais e guia a uma geração monotônica.
- [x] Invalidar o guia anterior quando uma nova lista de canais é carregada.
- [x] Ignorar respostas atrasadas do guia mesmo quando o transporte não respeita cancelamento.
- [x] Cobrir duas solicitações concorrentes do guia com teste unitário.

## 147. Retry offline sem IDs duplicados (v1.1.39)

- [x] Deduplicar entradas com o mesmo ID antes de reenfileirar downloads com falha.
- [x] Preservar a primeira ocorrência e a ordem apresentada na fila.
- [x] Cobrir entradas duplicadas com teste unitário.

## 148. Histórico de busca isolado por sessão (v1.1.40)

- [x] Associar cada observador de histórico a uma geração monotônica.
- [x] Confirmar também o usuário atual antes de atualizar a UI.
- [x] Ignorar emissões atrasadas de uma sessão anterior.
- [x] Cobrir troca de usuário com emissão tardia em teste concorrente.

## 149. Recuperação do player protegida por geração (v1.1.41)

- [x] Associar cada carga remota ou offline a uma geração de reprodução.
- [x] Impedir que erros de uma carga antiga apareçam na mídia atual.
- [x] Invalidar retries atrasados ao recarregar o mesmo título ou trocar de mídia.
- [x] Cobrir geração obsoleta e título divergente com teste unitário.

## 150. Relatórios de reprodução protegidos contra sessões obsoletas (v1.1.42)

- [x] Capturar item, sessão, fonte e posição antes de iniciar relatórios assíncronos.
- [x] Ignorar progresso e parada atrasados quando a mídia ou a sessão atuais mudarem.
- [x] Evitar que uma troca rápida de mídia envie o progresso para o título errado.
- [x] Cobrir geração, sessão e fonte divergentes com teste unitário.

## 151. Relatório de seek protegido contra sessão obsoleta (v1.1.43)

- [x] Aplicar a mesma captura de geração, item, sessão e fonte ao seek manual.
- [x] Impedir que um seek antigo atualize o título aberto depois da troca de mídia.
- [x] Revalidar o APK v1.1.42 antes de publicar a correção residual.

## 152. Descoberta LAN vinculada à identidade do servidor (v1.1.44)

- [x] Priorizar na LAN o servidor cujo `serverId` coincide com um servidor salvo.
- [x] Manter o primeiro anúncio como fallback para a primeira configuração.
- [x] Passar os servidores salvos à seleção automática durante a inicialização.
- [x] Cobrir servidor correto, servidor desconhecido e primeiro uso com testes unitários.

## 153. Snapshot consistente de faixas no relatório periódico (v1.1.45)

- [x] Capturar áudio, legenda, posição e estado no mesmo evento periódico.
- [x] Evitar misturar uma faixa escolhida depois da captura com a posição anterior.
- [x] Cobrir a imutabilidade dos dados do snapshot com teste unitário.

## 154. Usuários disponíveis sincronizados com o endpoint conectado (v1.1.46)

- [x] Recarregar a lista de usuários após verificar uma URL LAN ou pública.
- [x] Evitar que o login mostre usuários do servidor anterior após trocar de endpoint.
- [x] Cobrir conexão sem URL salva e atualização do seletor de usuários.

## 155. Seletor de usuários resiliente à troca e falha de endpoint (v1.1.47)

- [x] Limpar usuários do servidor anterior assim que uma nova URL válida começa a ser verificada.
- [x] Ignorar respostas tardias de carregamentos cancelados ou de endpoints que já não estão ativos.
- [x] Permitir nova tentativa quando o carregamento de usuários falhar temporariamente.
- [x] Cobrir retry no mesmo endpoint com teste unitário.

## 156. Seleção de servidor protegida contra verificações atrasadas (v1.1.48)

- [x] Cancelar a verificação anterior quando o usuário escolhe outro servidor.
- [x] Associar cada verificação à geração da última seleção.
- [x] Ignorar sucesso ou falha atrasados de uma conexão que já não é a escolhida.
- [x] Cobrir resposta antiga não cooperativa com teste concorrente.

## 157. Recuperação LAN protegida contra varredura obsoleta (v1.1.49)

- [x] Associar cada descoberta LAN a uma geração monotônica.
- [x] Ignorar o resultado de uma varredura cancelada após uma nova tentativa.
- [x] Invalidar varreduras pendentes ao parar a recuperação de rede.
- [x] Cobrir geração antiga e recuperação parada com teste unitário.

## 158. Home não bloqueada por perfil lento (v1.1.50)

- [x] Renderizar o catálogo assim que o feed de mídia terminar.
- [x] Carregar avatar e perfil em paralelo sem bloquear a Home.
- [x] Atualizar o perfil depois sem substituir um carregamento mais recente.
- [x] Cobrir resposta de perfil suspensa com teste unitário.

## 159. Busca isolada por sessão de usuário (v1.1.51)

- [x] Invalidar buscas pendentes quando a conta ativa mudar.
- [x] Limpar resultados da sessão anterior ao trocar de usuário.
- [x] Ignorar respostas tardias de uma busca iniciada por outra conta.
- [x] Cobrir transporte não cooperativo com teste unitário concorrente.

## 160. Biblioteca isolada por sessão de usuário (v1.1.52)

- [x] Invalidar carregamentos e paginações quando a conta ativa mudar.
- [x] Limpar itens da biblioteca anterior antes de exibir a nova sessão.
- [x] Validar geração, usuário e biblioteca antes de aplicar respostas.
- [x] Cobrir resposta tardia de biblioteca com teste unitário concorrente.

## 161. Minha Lista isolada por sessão de usuário (v1.1.53)

- [x] Observar troca de conta enquanto Minha Lista está aberta.
- [x] Limpar favoritos da conta anterior e recarregar a nova sessão.
- [x] Validar usuário e geração antes de aplicar respostas de favoritos.
- [x] Cobrir resposta tardia não cooperativa com teste unitário concorrente.

## 162. Home sincronizada com a sessão ativa (v1.1.54)

- [x] Observar troca de usuário enquanto a Home permanece aberta.
- [x] Limpar conteúdo e perfil da conta anterior antes do novo carregamento.
- [x] Recarregar automaticamente o feed da nova sessão.
- [x] Ignorar resposta tardia não cooperativa da conta anterior.

## 163. TV ao vivo isolada por sessão de usuário (v1.1.55)

- [x] Invalidar canais, gravações e guia ao trocar de conta.
- [x] Recarregar a programação da nova sessão automaticamente.
- [x] Impedir callbacks atrasados de agendamentos de alterar a conta atual.
- [x] Cobrir resposta tardia do guia com teste unitário concorrente.

## 164. Estado da transmissão Cast refletido no player (v1.1.56)

- [x] Observar a disponibilidade da sessão Cast pelo `CastPlayer` do Media3.
- [x] Atualizar o rótulo e a descrição de acessibilidade para “Transmitindo” durante o espelhamento.
- [x] Limpar o observador ao destruir o `PlayerViewModel`.
- [x] Cobrir os rótulos de conexão e desconexão com teste unitário.

## 165. SyncPlay isolado por sessão de usuário (v1.1.57)

- [x] Invalidar salas e sala ativa ao trocar de conta.
- [x] Recarregar as salas da nova sessão automaticamente.
- [x] Impedir callbacks atrasados de criação, entrada, saída e listagem de alterar a conta atual.
- [x] Cobrir resposta tardia de salas com teste unitário concorrente.

## 166. Detalhes de mídia isolados por sessão de usuário (v1.1.58)

- [x] Invalidar detalhes, temporadas, favoritos, estado assistido e downloads ao trocar de conta.
- [x] Ignorar respostas atrasadas de detalhes, temporadas, playlists e preparação de download da conta anterior.
- [x] Recomeçar a tela de detalhes limpa para a nova sessão ativa.
- [x] Cobrir resposta tardia de detalhes com teste unitário concorrente.

## 167. Perfil do usuário isolado por sessão (v1.1.59)

- [x] Invalidar carregamentos de perfil, usuários disponíveis e verificação do servidor ao trocar de conta.
- [x] Limpar o perfil anterior enquanto a nova sessão é carregada.
- [x] Recarregar automaticamente o perfil após a troca de usuário.
- [x] Cobrir resposta tardia do perfil com teste unitário concorrente.

## 168. Player invalidado ao trocar de sessão (v1.1.60)

- [x] Observar a sessão ativa enquanto o player está aberto.
- [x] Interromper a reprodução e limpar a mídia da conta anterior ao trocar de usuário.
- [x] Invalidar carregamentos, retries e relatórios de progresso atrasados.
- [x] Cobrir a invalidação por geração de sessão com teste unitário.

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
## 208. Ordenação ascendente e descendente (v1.1.98)
- [x] Adicionar direção Ascendente/Descendente ao menu de ordenação da biblioteca.
- [x] Enviar `SortOrder` ao servidor e persistir a preferência localmente.
- [x] Cobrir restauração, persistência e requisição com teste unitário.

## 209. Direção da ordenação nas Configurações (v1.1.99)
- [x] Exibir a direção atual junto da ordenação padrão.
- [x] Permitir escolher Ascendente ou Descendente nas Configurações.
- [x] Persistir e restaurar a direção com teste unitário.

## 210. Rótulo neutro para o critério de nome (v1.2.0)
- [x] Separar o critério “Nome” da direção Ascendente/Descendente.
- [x] Evitar o texto contraditório “Nome A-Z” quando Descendente estiver ativo.
- [x] Cobrir a preferência de direção nas Configurações.

## 211. Refresh de foreground resiliente na TV (v1.2.1)
- [x] Evitar que o timer de atualização cancele uma carga de Home ou Biblioteca ainda em andamento.
- [x] Atualizar imediatamente ao retornar ao foreground sem duplicar requisições.
- [x] Cobrir a proteção contra refresh concorrente em testes de ViewModel.
- [x] Remover o uso de ícone Compose obsoleto na tela de Configurações.

## 212. Quick Connect alinhado à disponibilidade do servidor (v1.2.2)
- [x] Consultar `QuickConnect/Enabled` antes de oferecer a ação de autenticação.
- [x] Exibir uma mensagem clara quando o recurso estiver desativado no servidor.
- [x] Reconsultar a disponibilidade ao trocar de endpoint LAN/internet.
- [x] Cobrir o contrato Retrofit e o bloqueio no ViewModel.
