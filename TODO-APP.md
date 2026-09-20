# MulletaFlix Android - Plano de Desenvolvimento & Checklist de Funcionalidades (TODO)

Este documento rastreia o status de implementação de todas as funcionalidades, módulos, telas e componentes do aplicativo oficial **MulletaFlix Android**.
O código completo do app está localizado em: [`MulletaFlix-android/`](file:///d:/Users/Raphael/Documents/Projetos/mulletaflix/MulletaFlix-android)

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
