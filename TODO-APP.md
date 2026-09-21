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
