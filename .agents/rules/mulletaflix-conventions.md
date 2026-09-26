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

### 1.4 Hierarquia de Pastas e Resolução de `parent` no MongoDB (`ftp.files`)
- **Convivência de Formatos**: O banco de dados do Nebula armazena tanto nós legados (cujo campo `parent` é uma string POSIX canônica, ex: `"/raphael/Filmes"` ou `"/Filmes"`) quanto nós novos (cujo campo `parent` é o `ObjectId` do diretório pai).
- **Resolução Obrigatória por Caminho Virtual**: Qualquer função que resolva ou crie pastas em cascata (como `EnsureDirectoryStructureAsync` ou `EnsureDirectoryStructureInMongoAsync`) deve obrigatoriamente:
  1. Rastrear o caminho virtual canônico acumulado a cada nível (`parentVirtualPath`).
  2. Passar esse caminho para `FindByNameAndParentAsync(segment, currentParent, parentVirtualPath)`.
- **Prevenção de Duplicação e Orfanato de Metadados**: Consultar apenas por `parentId` (ObjectId) falha em localizar diretórios canônicos legados, criando pastas duplicadas com o mesmo nome. Como o `NebulaFileSystem` agrupa pastas pelo nome e seleciona a primeira (`.First()`), arquivos pequenos de metadados (`.nfo`, `.jpg`, `.png`) salvos na nova pasta tornam-se invisíveis aos clientes.

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
  1. Gerar o pacote de atualização autônomo do servidor via `.\build-update-package.ps1 -Version <NovaVersao>`.
  2. Gerar o pacote do aplicativo Android via `.\build-app-package.ps1 -Version <NovaVersao>`.
  3. Publicar/atualizar a Release no GitHub anexando o zip do servidor e o APK do aplicativo (via `.\publish-release.ps1`).
  4. Publicar/atualizar a Release dedicada do aplicativo via `.\publish-app-release.ps1` (tag `app-v<NovaVersao>`).
  5. Isso garante que instâncias ativas do MulletaFlix detectem a nova versão em tempo real no **Centro de Atualizações (`/dashboard/updates`)** e usuários do aplicativo tenham o APK disponível imediatamente.

### 4.2 Autenticação Resiliente nos Scripts de Publicação (`publish-release.ps1` / `publish-app-release.ps1`)
- No Windows PowerShell, o envio de strings multiline via pipe para `git credential fill` pode falhar com `refusing to work with credential missing protocol field`.
- Os scripts de automação de release devem implementar a cadeia resiliente de obtenção do token:
  1. Variável de ambiente `$env:GITHUB_TOKEN` (caso definida em CI/CD).
  2. Execução direta de `& 'C:\Program Files\Git\mingw64\bin\git-credential-manager.exe' get` passando os pares `protocol=https` e `host=github.com`.
  3. Fallback para `git credential fill`.

---

## 5. Resiliência de Persistência no MySQL (.NET & Entity Framework)

### 5.1 Tratamento Obrigatório de Conflitos e Deadlocks Transitórios
Tarefas em segundo plano (como `StrmProbeScheduledTask`, varreduras de biblioteca e ingestão do Nebula) executam operações concorrentes com inserções e remoções de metadados (`ItemValues` e `ItemValuesMap`). Operações de salvamento no MySQL devem obrigatoriamente tratar erros transitórios via retry com backoff progressivo:
- **1062**: Entrada duplicada em índices exclusivos (`IX_ItemValues_Type_Value`).
- **1452**: Falha de chave estrangeira (`Cannot add or update a child row: a foreign key constraint fails`) decorrente de limpezas simultâneas de valores não referenciados.
- **1213**: Deadlock detectado pelo motor InnoDB (`Deadlock found when trying to get lock`).
- **1205**: Tempo limite de espera por lock excedido (`Lock wait timeout exceeded`).

Padrão exigido:
```csharp
for (var attempt = 1; attempt <= 3; attempt++)
{
    try
    {
        UpdateOrInsertItemsCore(items);
        return;
    }
    catch (DbUpdateException ex) when (attempt < 3 && IsTransientMetadataConflict(ex))
    {
        _logger.LogWarning(ex, "Transient metadata conflict on attempt {Attempt}. Retrying...", attempt);
        Thread.Sleep(attempt * 75);
    }
}
```

### 5.2 Lookup Seguro em Caches de Persistência
- Em mapeamentos e caches em memória de valores de itens (`ItemValuesMap`), **nunca** acessar o dicionário via indexador direto `lookup[key]`, pois itens excluídos ou alterados concorrentemente lançam `KeyNotFoundException`.
- Utilizar sempre `lookup.TryGetValue(key, out var val)` filtrando valores nulos antes da geração da transação.

---

## 6. Execução de Mídia e FFmpeg/FFprobe no Windows

### 6.1 Proibição do Prefixo `file:` para Caminhos Locais no Windows
- Ao invocar `ffmpeg.exe` ou `ffprobe.exe` no Windows, **nunca** prefixar caminhos locais ou unidades mapeadas com `file:` (ex: `file:N:\Filmes\...` ou `file:C:\...`).
- O runtime do FFmpeg no Windows falha ao interpretar letras de unidade acompanhadas de `file:`, abortando com `Invalid argument`. Caminhos do Windows devem ser fornecidos como caminhos literais normalizados (ex: `N:\Filmes\...`).

### 6.2 Isolamento de Subtitles em Transcodificações Rápidas
- Em operações de transcodificação de vídeo com busca rápida (`-ss`), containers como `.mkv` contendo faixas de legenda embutidas (ex: `subrip`) fazem o FFmpeg varrer todo o arquivo se legendas não forem explicitamente excluídas ou mapeadas em pipeline separado.
- Para transcodificações de áudio/vídeo imediatas, incluir sempre `-sn` ou `-map -0:s` no comando.

### 6.3 Elevação de Privilégios em Atualizações In-Place (Windows)
- Modificações ou cópias de binários para `C:\Program Files\MulletaFlix\Server\` exigem privilégios elevados de Administrador (UAC).
- O disparador de in-place update deve sempre invocar o PowerShell com `Verb = "runas"` para garantir que o Robocopy conclua a substituição sem erro 5 (`Acesso negado`).

---

## 7. Integridade de DOM em Templates de Cartões e Listas (MulletaFlix Web)

### 7.1 Fechamento Estrito e Simetria de Tags Estruturais
- Funções TypeScript/JavaScript que geram HTML através de concatenação de strings para cartões repetidos (como `imageDownloader.ts`, `imageeditor.ts`, etc.) devem obrigatoriamente manter paridade exata entre tags de abertura e fechamento (`<div class="cardBox visualCardBox">`, `<div class="cardScalable">`, `<div class="cardFooter">`).
- **Sintoma de Violação**: Abertura de container de cartão sem o `</div>` de fechamento provoca o aninhamento recursivo dos itens pelo parser do navegador: o primeiro cartão se torna pai do segundo, que se torna pai do terceiro, fazendo com que apenas 1 cartão apareça visível na tela e todos os demais colapsem internamente.
- Todo cartão de listagem ou grade deve ser gerado como um elemento irmão autocontido e fechado.

---

## 8. Arquitetura de Cache de Reprodução Nebula (.NET & Web)

### 8.1 Proteção de Mídias em Execução (Active Leases)
- Os arquivos temporários e partes de vídeo baixadas sob demanda pelo `NebulaHttpStreamServer` ou montagem STRM ficam sob custódia do `NebulaPlaybackCache`.
- Arquivos com leituras ativas mantêm uma trava lógica (`ActiveLeasesCount`). Qualquer rotina de limpeza (periódica de 5 minutos, expiração por inatividade de 1 hora ou invocação via `POST /Nebula/Ftp/PlaybackCache/Clear`) deve obrigatoriamente ignorar e preservar arquivos com lease ativo.

### 8.2 Configuração Centralizada de Armazenamento de Cache
- O caminho do cache do Nebula deve ser dinamicamente configurável via `POST /Nebula/Ftp/PlaybackCache/Path`.
- Na interface Web do Painel de Administração, a tela de configuração deve residir no submenu **Reprodução > Cache Nebula** (`/dashboard/playback/nebulacache`), disponibilizando:
  1. Indicadores de espaço ocupado, arquivos em cache e leases ativos.
  2. Barra de porcentagem de utilização do volume de armazenamento.
  3. Seletor de diretórios nativo (`DirectoryBrowser`).
  4. Ação de limpeza imediata segura para arquivos não vinculados a reproduções em andamento.

---

## 9. Preservação de Codificação UTF-8 em Scripts de Automação e Release (.ps1)

### 9.1 Assinatura Mandatória UTF-8 BOM em Scripts com Texto
- Todos os scripts PowerShell (`publish-release.ps1`, `publish-app-release.ps1`, `build-update-package.ps1`, etc.) que contenham strings literais em português (com acentuação ou caracteres especiais) devem ser obrigatoriamente salvos com a marca de ordem de bytes **UTF-8 BOM** (`0xEF, 0xBB, 0xBF`).
- **Causa Raiz & Sintoma**: O Windows PowerShell 5.1 (`powershell.exe`) decodifica scripts sem BOM através da página de código ANSI local (`Windows-1252`). Quando caracteres UTF-8 de 2 bytes (como `ç` `0xC3 0xA7` e `ã` `0xC3 0xA3`) são lidos em ANSI, tornam-se `Ã§` e `Ã£`. Ao transmitir payloads JSON para a API do GitHub via `[System.Text.Encoding]::UTF8.GetBytes()`, esses caracteres são codificados uma segunda vez, provocando o fenômeno de **mojibake** na interface de atualizações.
- Todo script de release deve declarar no topo:
  ```powershell
  [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
  $OutputEncoding = [System.Text.Encoding]::UTF8
  ```

---

## 10. Isolamento de Layout do Dashboard React MUI (.dashboardDocument vs .skinBody)

### 10.1 Proibição de Transform no Container .skinBody do Dashboard
- No cliente Web, páginas do Painel de Administração (`.dashboardDocument`) utilizam a estrutura React MUI onde o recuo da navegação lateral é provido por `.mainAnimatedPage` (`left: $drawer-width` = 240px).
- A classe legada de páginas do Jellyfin `.dashboardDocument .skinBody` **nunca deve utilizar `transform: translateX(...)`** para representar abertura/fechamento de menu. No CSS, a propriedade `transform` continua atuando sobre elementos mesmo quando `position: unset !important` é aplicado.
- **Sintoma de Violação**: Aplicar `transform: translateX(20em)` em `.skinBody` gera um deslocamento cumulativo de 320px + 240px = 560px. Isso empurra o conteúdo da tela para a direita, gerando um vazio escuro desnecessário entre o botão de voltar do cabeçalho e os cartões, além de forçar o corte do conteúdo à direita fora dos limites visíveis da viewport.
- O arquivo `AppOverrides.scss` deve manter neutralização explícita:
  ```scss
  .dashboardDocument .skinBody {
      position: unset !important;
      transform: none !important;
      left: unset !important;
      right: unset !important;
  }
  ```

### 10.2 Prevenção de Overflow Horizontal em Textos e Markdown
- Elementos que renderizam conteúdo dinâmico externo (ex: `MarkdownBox`, tabelas de changelog, blocos `<pre>`) devem conter `overflowWrap: 'break-word'`, `wordBreak: 'break-word'` e rolagem horizontal contida em blocos tabulares (`overflowX: 'auto'`), garantindo que nenhuma tabela de changelog estoure a largura máxima da tela.



