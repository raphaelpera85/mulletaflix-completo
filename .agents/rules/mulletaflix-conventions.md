# Convenções e Diretrizes de Arquitetura do MulletaFlix

Este documento formaliza as regras arquiteturais, padrões de resiliência e diretrizes de interface do MulletaFlix.

---

## 1. Desduplicação e Integridade de Mídias (Nebula & Telegram)

### 1.1 Regra de Ouro: Nunca Reenviar Mídias Já Concluídas
- Antes de qualquer processamento ou consumo de banda (seja no `SyncStagingDirectoryAsync`, no `NebulaUploadEngine` ou no `NebulaDownloaderEngine`), deve-se consultar se o item já existe com status `completed` e partes válidas no Telegram (`tg_file_id`).

### 1.2 Resolução por Identidade Canônica
A correspondência não pode depender unicamente do caminho completo local no disco (letras de unidade como `D:`, `E:`, `F:` ou subpastas de staging temporárias variam).
A verificação deve sempre considerar:
1. **Nome exato** ou nome base sem extensão (`stem`).
2. **Identidade de Filme**: Título normalizado + Ano de lançamento (ex: `Invasão Bolchevique` + `2022`).
3. **Identidade de Série / Episódio**: Nome normalizado da série + Temporada + Episódio (padrão `SxxExx` / `1x02`).

### 1.3 Travas Mandatórias nos Motores
- **No Escaneamento do Staging**: Se a mídia já constar no banco com partes Telegram, não gerar documento com status `queued`. Se `deleteCompletedFromStaging` estiver habilitado, limpar a cópia duplicada local para economizar armazenamento.
- **No Upload Engine**: Antes de abrir sockets com a API do Telegram, revalidar se a mídia já foi completada. Se sim, abortar imediatamente o upload, liberar o lock da fila e marcar como limpo.
- **No Downloader de .strm**: Antes de iniciar downloads HTTP/multipart, verificar se a mídia já existe no Telegram para não desperdiçar tráfego nem CPU.

---

## 2. Resiliência de Sockets, Streams e Buffers (.NET Backend)

### 2.1 Watchdogs em Leitura de Streams HTTP
- O uso de `HttpClient` com `HttpCompletionOption.ResponseHeadersRead` **não** aplica o `HttpClient.Timeout` nas operações subsequentes de `stream.ReadAsync()`.
- Toda leitura sequencial ou multipart de download deve possuir watchdog ou `CancellationTokenSource` com cancelamento por inatividade (ex: 45 segundos) a cada bloco lido, garantindo que conexões congeladas pelo TCP do Windows sejam abortadas e reexecutadas com retry exponencial.

### 2.2 Buffers Circulares e Paginação de Logs
- Em buffers de log em memória com capacidade limitada (onde os itens mais antigos são removidos via `RemoveRange(0, n)`), **nunca** utilizar `List.Count` como cursor de paginação incremental para a interface web.
- O controle deve ser feito obrigatoriamente por **contadores monotônicos (`long _seq`)**, garantindo que clientes que consultam offsets continuem recebendo novos logs mesmo após o buffer atingir a capacidade máxima.

---

## 3. Padrões de Interface e Experiência do Usuário (MulletaFlix Web)

### 3.1 Gestão de Entidades e Credenciais
- Painéis de administração com listagens de entidades funcionais (ex: Bots do Telegram, chaves de API, webhooks) devem ser isolados em **Abas dedicadas (`Tabs`)**, evitando telas sobrecarregadas de rolagem única vertical.
- As coleções devem ser apresentadas em **Cards responsivos em Grid** (`repeat(auto-fill, minmax(320px, 1fr))`) e não em listas contínuas sem destaque.

### 3.2 Anatomia dos Cards de Entidades
Cada card deve apresentar claramente:
- Ícone visual da entidade com destaque de cor conforme o estado.
- Nome amigável e indicador de índice ou identificador único.
- Valores sensíveis (tokens/senhas) sempre mascarados em fonte monoespaçada com opção segura de cópia/substituição.
- Chips de status explícitos (ex: `Sessão ativa` em verde, `Sessão pendente` em amarelo).
- Ações contextuais rápidas no próprio card (ex: botão de exclusão com confirmação).
- Formulário de adição/rotação contextualizado e posicionado no topo da respectiva aba.

---

## 4. Ciclo de Lançamentos e Releases Obrigatórios

### 4.1 Regra de Ouro: Modificação no MulletaFlix Exige Release no Git
- Sempre que qualquer alteração, correção de bug ou nova funcionalidade for finalizada e aprovada na suíte de testes (Quality Bar), deve-se:
  1. Gerar o pacote de atualização autônomo via `.\build-update-package.ps1 -Version <NovaVersao>`.
  2. Publicar a Release correspondente no GitHub anexando o asset `dist/mulletaflix-update-win-x64.zip` (via `.\publish-release.ps1` ou tag do Git).
  3. Isso garante que instâncias ativas do MulletaFlix detectem a nova versão em tempo real no **Centro de Atualizações (`/dashboard/updates`)** e possam atualizar com 1 clique.

