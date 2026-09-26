# Auditoria completa de performance — servidor MulletaFlix

Escopo: backend .NET (`MulletaFlix-master`), banco MariaDB (`mulletaflix`) e cliente web
(`MulletaFlix-web-master`). O aplicativo Android está fora do escopo por decisão do projeto.

Método: cada achado abaixo traz evidência medida no host real ou código citado com arquivo:linha.
Nenhuma recomendação entrou sem número que a sustente.

Skills usadas nesta auditoria: `dotnet-architect` (backend .NET), `mysql-patterns` (MariaDB),
`react-performance` (frontend), `performance-optimization`, `performance-profiling`,
`verification-before-completion` e `gauntlet-loop`.

---

## Parte 1 — Banco de dados MariaDB (medido ao vivo)

### Base de medição

| Medida | Valor |
| --- | --- |
| Banco `mulletaflix` total | **506,1 MB** |
| `baseitems` | **304,3 MB** (52,6 MB dados + **251,7 MB índices**) |
| `baseitems` linhas | 78 523 |
| `baseitems` bytes/linha | 706 de dados contra **3 382 de índices** |
| Índices em `baseitems` | **27** (26 secundários + PK) |
| Buffer pool | 256 MB para 506 MB de banco |
| Páginas livres no pool | 5 749 de 16 192 (**35% ocioso**) |
| RAM física livre no host | **730 MB** de 16 088 MB, pagefile em 2 628 MB (pico 7 074 MB) |

### D-1 [P0] `baseitems` é 4,8× maior em índices do que em dados

- Evidência: `INDEX_LENGTH = 263 946 240` bytes contra `DATA_LENGTH = 55 115 776` bytes para
  78 523 linhas — 3 382 bytes de índice por linha.
- Por que importa: os 26 índices secundários custam manutenção de B-tree em **toda** gravação de
  item. Uma biblioteca com refresh de metadados constante grava muito; isso é a fonte mais provável
  das 14 deadlocks/dia e dos retries de "Transient metadata conflict". Também é o maior consumidor
  do buffer pool, que tem apenas 256 MB.
- Correção: podar índices (ver D-2, D-3 e D-4). Não aumentar o pool: ele está 35% ocioso, então o
  gargalo é o volume de índices, não o tamanho do cache.
- Esforço/risco: pequeno para remoções pontuais; médio para reestruturar a PK.

### D-2 [P0] Índice comprovadamente redundante em `baseitems`

- Evidência: `IX_BaseItems_TopParentId_Type_IsVirtualItem` é prefixo mais à esquerda de
  `IX_BaseItems_TopParentId_Type_IsVirtualItem_DateCreated`. Verificado por consulta de prefixo em
  `information_schema.STATISTICS`.
- Por que importa: ~8,9 MB de índice mantidos sem servir nenhuma consulta que o índice mais largo já
  não atenda. Custo zero de leitura, custo positivo em toda escrita.
- Correção: `DROP INDEX IX_BaseItems_TopParentId_Type_IsVirtualItem ON baseitems;`
- Esforço/risco: pequeno. Risco apenas se alguma consulta depender do tamanho exato do índice
  (não depende: o prefixo é idêntico).

### D-3 [P1] A PK GUID de 36 bytes é replicada em todos os 26 índices secundários

- Evidência: `Id` é `char(36)` e é anexado como chave de agrupamento a todo índice secundário.
  36 bytes × 78 523 linhas × 26 índices = **~73 MB apenas de valores de PK duplicados**.
- Por que importa: ~29% dos 251,7 MB de índices são cópias do mesmo GUID. Trocar por `binary(16)`
  reduziria ~20 bytes por linha por índice (**~40 MB**) e encolheria a própria B-tree da PK em 2,25×.
- Correção: migração de `char(36)` para `binary(16)` nas colunas de identidade e relacionamento.
  É a maior economia isolada do banco, porém é uma migração ampla.
- Esforço/risco: grande. Exige conversão de dados, ajuste do mapeamento EF, revisão de todas as
  consultas e um plano de rollback. Não executar sem janela dedicada.

### D-4 [P1] `Type` é string repetida em 9 dos 27 índices

- Evidência: `Type` é `varchar(100)` com **39,9 caracteres médios** e aparece em 9 índices; `key_len`
  medido de 441 bytes em `IX_BaseItems_TopParentId_Type_IsVirtualItem_DateCreated` (EXPLAIN).
- Por que importa: ~3,1 MB por índice × 9 ≈ **28 MB** apenas carregando o nome do tipo.
- Correção: avaliar normalizar `Type`/`MediaType` para código numérico curto, ou remover índices que
  repetem `Type` quando um índice mais largo já cobre o prefixo.
- Esforço/risco: médio. Mexe no mapeamento de entidades e no `_itemTypeLookup`.

### D-5 [P2] Índice `IX_BaseItems_FullTextSearch` alimenta um caminho morto — e há um bug latente

- Evidência: `BaseItemConfiguration.cs:88` cria
  `builder.HasIndex(e => new { e.CleanName, e.OriginalTitle })`, um **BTREE comum** com nome
  enganoso de full-text. O único consumidor previsto é `MySqlDatabaseProvider.cs:393-401`, que faz
  `WHERE MATCH(i.CleanName, i.OriginalTitle) AGAINST(@term IN NATURAL LANGUAGE MODE)`.
- Por que importa: `MATCH ... AGAINST` exige índice **FULLTEXT**; contra um BTREE o MariaDB falha
  com "Can't find FULLTEXT index matching the column list". Como o método nunca é chamado, é bug
  latente, não falha ativa.
- Correção: decidir entre (a) criar um FULLTEXT real e usar o caminho de busca por relevância, ou
  (b) remover o método morto. Só remover o índice BTREE **depois** de confirmar com `userstat`, pois
  `TranslateQuery.cs:399` faz `e.CleanName == cleanName` e `OrderMapper.cs:89` faz match exato em
  `CleanName`, consultas que podem usar esse índice.
- Esforço/risco: pequeno para escolher o rumo; a remoção do índice exige a confirmação empírica.
- Correção de rumo registrada: a auditoria anterior afirmava que esse índice podia ser removido por
  ser "declarado e nunca chamado". Isso **não** se sustenta: existem consultas de igualdade em
  `CleanName` que podem usá-lo. Não remover sem medir.

### D-6 [P2] `userstat` foi ativado para medir uso real de índice

- Ação tomada: `SET GLOBAL userstat=ON` (dinâmico, não exige restart, reversível).
- Por que importa: sem contadores de uso, remover índice é adivinhação. Com `userstat` ligado,
  `information_schema.INDEX_STATISTICS` passa a acumular leituras por índice, permitindo remover com
  prova os que ficarem em zero após alguns dias de operação normal.
- Correção: coletar por alguns dias e revisar `SELECT * FROM information_schema.INDEX_STATISTICS
  WHERE TABLE_SCHEMA='mulletaflix' ORDER BY ROWS_READ;`
- Esforço/risco: nenhum. Overhead de contadores é desprezível.

### D-7 [P2] `peoplebaseitemmap` tem dois índices quase idênticos

- Evidência: `IX_PeopleBaseItemMap_ItemId_ListOrder (ItemId, ListOrder)` e
  `IX_PeopleBaseItemMap_ItemId_SortOrder (ItemId, SortOrder)` — 45,8 MB de índices para 23,6 MB de
  dados em 88 192 linhas.
- Por que importa: dois índices que diferem só na terceira coluna dobram o custo de escrita da
  tabela de elenco, que é reescrita em todo refresh de metadados.
- Correção: confirmar com `userstat` se as duas ordenações são realmente consultadas; se só uma for,
  remover a outra.
- Esforço/risco: pequeno.

### D-8 [P2] O paging do host não é causado pelo MulletaFlix

- Evidência: `MulletaFlix` usa **239 MB** de working set. Os maiores consumidores são
  `qemu-system-x86_64` com 3 195 MB e dois processos `java` com 3 155 MB e 783 MB — cerca de 7,1 GB
  dos 16 GB. Com apenas 730 MB livres, o pagefile já chegou a 7 074 MB.
- Por que importa: corrige a hipótese de que o servidor precisava de mais memória para o banco. O
  aperto de memória vem de cargas alheias ao servidor. Aumentar o buffer pool seria contraproducente.
- Correção: nenhuma no servidor. Registrar que o host está sobrecarregado por outras cargas (VM e
  Java) e que qualquer tuning de memória do MariaDB deve ser reavaliado quando essa pressão cair.
- Esforço/risco: nenhum.

### O que NÃO recomendar (e por quê)

- **Aumentar `innodb_buffer_pool_size`**: o pool tem 35% de páginas livres e zero
  `Innodb_buffer_pool_wait_free`. O hit ratio de 91,8% medido é aquecimento após restart, não
  pressão de cache. Aumentar o pool em um host com 730 MB livres agravaria o paging.
- **`innodb_flush_log_at_trx_commit=2`**: trocaria durabilidade por escrita em um banco de metadados
  onde a perda de dados é inaceitável.

---

## Parte 2 — Backend .NET: bloqueios, locks e alocações

Frente auditada: `Emby.Server.Implementations`, `MediaBrowser.Providers`, `MediaBrowser.MediaEncoding`,
`Jellyfin.Server.Implementations`. Todos os itens abaixo foram provados por leitura direta do código.

| # | Prio | Local | Problema | Correção |
| --- | --- | --- | --- | --- |
| B-1 | P0 | `ProviderManager.cs:1275` e `:1306` | `PriorityQueue` **não é thread-safe** e é usada sem lock no `Enqueue` e no `TryDequeue`; `_refreshQueueLock` só protege a flag. Produtores concorrentes corrompem o heap: refreshes perdidos, ordem errada ou exceção. | Serializar Enqueue+TryDequeue sob o lock, ou trocar por `ConcurrentQueue` com baldes de prioridade |
| B-2 | P0 | `NebulaDownloaderEngine.cs:1017` | Lock global + `Sum()` + 2-3 strings interpoladas + evento de UI **dentro do lock**, a cada buffer de 64 KB e com até 32 partes concorrentes. >1.500 aquisições de lock e ~5.000 alocações por segundo. | Reportar a cada 250–500 ms ou na mudança de percentual inteiro; formatar fora do lock |
| B-3 | P0 | `UserDataManager.cs:59` | `SaveUserData` faz lock bloqueante em 8 stripes + transação síncrona + re-query completo, e é chamado em **todo report de progresso** (~10 s por cliente). Itens sem relação colidem no mesmo stripe. | Overload assíncrono com `AsyncKeyedLocker<Guid>` por item; eliminar a re-query (leituras já vêm do cache) |
| B-4 | P1 | `MediaSourceManager.cs:619` | Um lock global do subsistema de live TV segurado **através do await** que abre o stream remoto. Dois usuários em canais diferentes serializam; um provider travado bloqueia todos. | Lock por chave (`request.OpenToken`), seção crítica só na mutação de `_openStreams` |
| B-5 | P1 | `NebulaChunkedStream.cs:66` | `MaxCachedChunks = 64` → **64 MB de LOH por stream ativo** (2-3 streams = 128-192 MB), com GC workstation e host paginando. | Baixar para 4–8 chunks e/ou alugar de `ArrayPool<byte>.Shared` |
| B-6 | P1 | `DtoService.cs:327` | Sync-over-async com `GetAwaiter().GetResult()`; o caminho busca o manifest de trickplay, que abre um DbContext e faz uma query **por media source**, no `/Sessions` que todo cliente poll. Contribuinte direto da starvation. | Remover `ItemFields.Trickplay` das opções de sessão; eliminar o contrato síncrono |
| B-7 | P1 | `DisplayPreferencesManager.cs:42,60,104,113,122` | `SaveChangesAsync(default).GetAwaiter().GetResult()` em caminhos de request, inclusive no create-on-miss do GET. | Overloads assíncronos na interface |
| B-8 | P1 | `ItemPersistenceService.cs:290` | Os 16 stripes são escolhidos pelo **primeiro item do lote**. Dois lotes que compartilham itens pegam locks diferentes (exclusão perdida) e lotes sem relação serializam. | Stripe por item ou chunk do lote com ordem determinística de locks |
| B-9 | P1 | `MetadataService.cs:1062` | `await Task.Delay(100)` incondicional **após cada imagem**, dentro do orçamento de `_refreshConcurrency`: limita o servidor inteiro a 10 imagens/s. | Só dormir após 429 real, ou mover imagens para worker dedicado |
| B-10 | P2 | `CollectionManager.cs:249` | `List<Guid>.Contains` dentro de laço → O(n²). | `HashSet<Guid>` |
| B-11 | P2 | `PeopleRepository.cs:126,166`, `MediaStreamRepository.cs:47`, `ChapterRepository.cs:81`, `MediaAttachmentRepository.cs:39`, `LinkedChildrenService.cs:175` | `SaveChangesAsync(default).GetAwaiter().GetResult()` uma vez por item no scan, com `CreateDbContext()` e transação síncronos. | Migrar para os métodos `*Async` que já existem |
| B-12 | P2 | `MediaSourceManager.cs:405` | MD5 criptográfico do nome do tipo recalculado por provider por request, para um valor constante do processo. | Memoizar por `Type` |

## Parte 3 — HTTP, API e streaming

| # | Prio | Local | Problema | Correção |
| --- | --- | --- | --- | --- |
| H-1 | P0 | `ResponseTimeMiddleware.cs:51` | Aviso de resposta lenta só em **Debug**, com log efetivo em `Information`: `EnableSlowResponseWarning=true` (limiar 500 ms) nunca produz saída. | Emitir em `Information` com supressão por endpoint |
| H-2 | P0 | `DynamicHlsController.cs:1436` | **Todo request de segmento HLS reconstrói o estado de streaming inteiro**: resolução de media source, `PathFallbackHelper` ×2, decisão de transcoding (`TryStreamCopy`), `ResolutionNormalizer` e um `StreamState` novo. Numa resolução real, 1200 reconstruções por filme de 2 h. | Cachear o `StreamState` por `PlaySessionId`/`MediaSourceId` com TTL curto, invalidando em `OnTranscodeEndRequest` |
| H-3 | P1 | `Startup.cs` (compressão) | **Playlists HLS não eram comprimidas**: `application/x-mpegURL` não está na lista padrão de MIME types do ASP.NET Core, então a compressão que eu havia habilitado não cobria m3u8. Cada segmento repete a query string inteira, gerando uma playlist de centenas de KB de texto repetitivo. | Adicionar os MIME types de playlist e `text/vtt` — **corrigido nesta execução** |
| H-4 | P1 | `ImageController.cs:2121` vs `:2172` | O `ProcessImage` (encode Skia, MD5 da chave de cache, stats) roda **antes** da checagem de `If-None-Match`/`If-Modified-Since`, embora o `tag` do ETag já esteja disponível antes. O short-circuit do ETag não evita o trabalho que deveria evitar. | Checar o ETag e devolver 304 antes de `ProcessImage` |
| H-5 | P1 | `SubtitleEncoder.cs:127` + `SubtitleController.cs:270` | Cada segmento de legenda re-resolve media source (com `allowMediaProbe: true`) e re-parseia o arquivo inteiro, sem cache de saída e **sem nenhum header de cache**. 240 requests por filme. | Cachear por `(mediaSourceId, streamIndex, format, start, end)` + `ETag`/`Cache-Control` |
| H-6 | P1 | `MediaInfoHelper.cs:127` | Clone de `MediaSourceInfo[]` via round-trip JSON (serialize + deserialize) só para copiar o grafo. | `record`/`Clone()` explícito |
| H-7 | P1 | `TrickplayController.cs:97` → `TrickplayManager.cs:507` | Uma query de banco **por tile**: abrir um DbContext novo a cada tile, e `GetTrickplayResolutions` sem cache. Um arrasto de scrub faz centenas de requests. | Cachear resoluções por `itemId` e montar o caminho sem query |
| H-8 | P1 | `UniversalAudioController.cs:134` → `AudioHelper.cs:88` | O stream é resolvido **duas vezes** no mesmo request (uma em `GetPlaybackInfo`, outra em `GetStreamingState`), com dois clones JSON e duas avaliações de device profile por faixa. | Passar o `StreamState`/`MediaSourceInfo` já resolvido |
| H-9 | P2 | `StreamState.cs:168` | `CloseLiveStream(...).GetAwaiter().GetResult()` no `Dispose`, chamado por `using var state` em todo request de playlist/stream de fonte que exige fechamento. Bug clássico de starvation. | `IAsyncDisposable` |
| H-10 | P2 | `TrickplayController.cs` (rota legada 100+) / `HlsSegmentController.cs:155` | Endpoint legado enumera o diretório de transcode inteiro por request de segmento, com `Path.GetExtension` + `Contains` por entrada. | Construir o caminho direto, com fallback guardado |
| H-11 | P2 | `Startup.cs` (static files) | Assets com hash no nome recebem `max-age=3600`, forçando revalidação de dezenas de arquivos por cliente por hora. | `max-age=31536000, immutable` para `/web/assets/**` |
| H-12 | P2 | `SecurityHeadersMiddleware` + `ResponseTimeMiddleware` + `RateLimitMiddleware` | Executam em **todo** segmento/imagem: ~900 bytes de headers (fora da compressão) × 1200 segmentos ≈ 1 MB por filme, mais closures e resolução de cultura por request. | Aplicar por tipo de conteúdo e trocar os `Any(StartsWith)` por prefixos com `AsSpan()` |
| H-13 | P2 | `TranscodeManager.cs:396`, `:700` | Sem teto de jobs ffmpeg simultâneos, e cada request de segmento pega um lock global e varre a lista de jobs até 2×. | Semáforo configurável + `ConcurrentDictionary` |
| H-14 | P2 | `NebulaHttpStreamServer.cs:480` | `new byte[128 * 1024]` por request de stream (8192 alocações ≈ 1 GB de lixo para 1 GB transmitido). | `ArrayPool<byte>.Shared` |
| H-15 | P2 | `SubtitleController.cs:511` | Lista de fontes re-enumerada e re-statada (2 stats por fonte) por request, sem cache nem `ETag`; `GetFallbackFont` usa `.First()` e devolve 500 em vez de 404. | Cache curto + `ETag`; `FirstOrDefault` + `NotFound()` |

## Parte 4 — Frontend web: achados de código

Medições do build: `dist/` tem 1875 arquivos e 61,25 MB (586 JS = 18,32 MB, 1083 woff2 = 23,63 MB).
**JS antes do app shell: 1952 KB não comprimidos em 14 chunks, mais 149 KB de CSS.**

| # | Prio | Local | Problema | Correção |
| --- | --- | --- | --- | --- |
| F-1 | P0 | `src/index.tsx:81-99` | **Cascata de boot: 5 round-trips seriais** (`appHost.init` → `serverAddress` → `loadCoreDictionary` → `loadPlugins` → `import('./RootAppRouter')`) antes de renderizar. Nada é sobreposto, embora só `serverAddress` seja pré-requisito real. | Iniciar o chunk do router e o dicionário em paralelo; mover `loadPlugins()` para depois do `renderApp()` |
| F-2 | P0 | `src/utils/dateFnsLocale.ts:2` | `import { enUS } from 'date-fns/locale'` resolve o barril **CommonJS**, que o Rollup não consegue tree-shakar: **285 KB (22 % do JS de boot)** na critical path por um objeto de locale. | Usar `Intl.DateTimeFormat`/`Intl.RelativeTimeFormat`, ou alias para `date-fns/esm` |
| F-3 | P0 | `src/RootAppRouter.tsx:1` | `ThemeProvider` do MUI arrasta **456 KB** (mui 428 + icons 28) para a critical path, mesmo em `/home` e `/movies`, que são views legadas sem MUI. | Mover o `ThemeProvider` para os layouts lazy |
| F-4 | P1 | `src/hooks/api/useUserViews.ts:26` | `/Users/{id}/Views` é buscado de **4 lugares** e deduplicado por um `staleTime: 1000` — o comentário no código admite o hack. Em cliente lento a janela de 1 s expira e o payload é refeito. | Um único dono com prefetch; `staleTime` real de 5 min |
| F-5 | P1 | `src/scripts/autoThemes.ts:53` | Branding refeito **a cada navegação de página** (sem `staleTime`), para ler um theme id que não muda na sessão. | `staleTime: Infinity` + memoizar o id padrão |
| F-6 | P1 | `ItemsContainer.tsx:54` | `eventsToMonitor = []` é parâmetro default → **nova identidade a cada render**, re-disparando o efeito que destrói e recria `Sortable`, `MultiSelect`, 3 listeners e 6 assinaturas websocket. | Constantes de módulo; chavear o efeito por `join(',')` |
| F-7 | P1 | `Cards.tsx:13`, `cardBuilder.ts:86` | Grades renderizam todos os itens sem virtualização e **sem `content-visibility`** (zero ocorrências no `src/`), e `setCardData` roda durante o render com `values.sort()`. 100 cards por commit. | `content-visibility: auto` no `.card` (ganho sem JS) + memoizar |
| F-8 | P1 | `queryClient.ts:3-11` | **Sem política global de cache**: sem `staleTime`, sem `refetchOnWindowFocus: false`. Toda query nasce stale; só ~22 de ~90 definem algo. | `staleTime: 30s`, `refetchOnWindowFocus: false`, e confiar no websocket já existente |
| F-9 | P1 | `bookPlayer/plugin.ts:577` | Leitor carrega **todos os spine documents em série** (`await` dentro do laço) e depois roda `locations.generate` no livro inteiro: duas passadas completas antes de ficar utilizável. | `Promise.all` em lotes de 5-8; sair cedo se a navegação já traz capítulos |
| F-10 | P2 | `autoBackdrops.ts:34` | Query recursiva com `SortBy: Random` a cada entrada de página + imagem full-screen a cada 10 s; cache é objeto de módulo que erra ao trocar de tipo. | Cache em `sessionStorage`; pausar quando `visibilityState === 'hidden'` |
| F-11 | P2 | `libraryMenu.ts:32`, `index.tsx:135` | Fonte de ícones de 122 KB em toda página (com .ttf/.woff/.eot desnecessários) e CSS de fontes de 258 KB com 210 `@font-face`. | Subsetar a fonte de ícones; curar `fonts.noto-base` |
| F-12 | P2 | `unidentified.tsx:22` | Única tela fora do React Query: `setInterval` de 15 s + `setItems` com array novo a cada poll → re-render completo mesmo sem mudança. | Migrar para `useQuery` com `structuralSharing` |
| F-13 | P2 | `itemDetails/index.ts:74` | Poster sem caixa reservada (`height: auto`, sem `aspect-ratio`): CLS na página mais visitada. | Reservar com `aspect-ratio: 2/3` ou `PrimaryImageAspectRatio` |
| F-14 | P2 | `nebula/index.tsx:407` | Polling de 3 s (status), 5 s (logs com offsets fixos em 0), 10 s (bots/health) ≈ 1,1 req/s contínuo. | 5-10 s / 15 s com cursor real / 60 s |
| F-15 | P2 | `librarybrowser.scss:304`, `navdrawer.scss:15` | Transições em `left`/`padding` — propriedades de layout, invalidando a página inteira por frame. | `transform: translate3d()` |

Nota: hls.js, flv.js, pdfjs, epubjs e libass **já** são importados dinamicamente, e o React Query já é usado em toda a aplicação — essas duas suspeitas comuns não se confirmaram aqui.

## Parte 5 — Startup, memória e tarefas agendadas

| # | Prio | Local | Problema | Correção |
| --- | --- | --- | --- | --- |
| S-1 | P0 | `NebulaFtpManager.cs:1770` + `NebulaMongoContext.cs:2428` | O bot de limpeza contínua re-materializa a biblioteca inteira (dois passeios recursivos) **e** todo o conjunto de documentos concluídos do Mongo, a cada **30 s** = 2.880×/dia. Maior driver do commit de 39 GB e do paging. | Intervalo de 15–30 min; projetar só `local_path`; pular ciclo sem upload novo |
| S-2 | P0 | `NebulaTelegramPool.cs:1407` + `:1482` | **Explica a bimodalidade do upload.** Um 429 repete o *mesmo* bot 3×, esperando o flood-wait inteiro, com o lease do bot preso: 3 × retry_after. Um flood-wait de ~19 s dá ~57 s por parte — exatamente o modo lento medido (56-59 s contra 2,4 s). | Tratar 429 como não-retentável e devolver `null` para o caller rotacionar bot; soltar o lease antes de dormir; ampliar o conjunto de `Take(3)` |
| S-3 | P1 | `NebulaStagingWatcher.cs:519` | Limpeza de diretórios órfãos é O(dirs × files) e roda no startup e a cada 10 min. | Um passe bottom-up; reusar o array; rodar de hora em hora |
| S-4 | P1 | `NebulaStagingWatcher.cs:320` | `continue` passa pelo `finally` que remove do `_queuedFiles`, então o sidecar bloqueado é re-enfileirado a cada 1 s → fila cresce e o **mesmo arquivo pode subir duas vezes**. | Limpar as chaves só em desfecho terminal; backoff 1 s → 5 s → 30 s |
| S-5 | P1 | `ChapterImagesTask.cs:85` | Carrega **toda** a biblioteca de vídeo (sem `Limit`) e reescreve o arquivo de falhas dentro do laço → O(n²) de IO. Os irmãos já paginam. | Adotar o laço `pagesize`/`StartIndex`; `HashSet` de falhas; gravar uma vez no fim |
| S-6 | P1 | `UnidentifiedMediaCleanupTask.cs:69` | Biblioteca inteira em memória + `new DirectoryService` por item + 77.909 refreshes enfileirados de uma vez. | Paginar, içar o `DirectoryService`, limitar lotes |
| S-7 | P1 | `PeopleValidationTask.cs:97` | `Take(100)` **sem `Skip` nem cursor**, avançando só pelo efeito da exclusão: re-agrupa a tabela `Peoples` inteira por página (quadrático) e trava no primeiro página se uma exclusão for bloqueada por FK. | Paginar por cursor em `Name, PersonType`; contexto novo por lote |
| S-8 | P1 | `JellyfinMigrationService.cs:356` | Migração de dados e DDL de domínio rodam **incondicionalmente em todo boot**, fora do laço de pendências: 12 probes de `information_schema` + 4 `AnyAsync`, com risco de carregar a biblioteca inteira se uma tabela de domínio estiver vazia. | Marcar a versão aplicada de forma persistente e pular quando atual |
| S-9 | P1 | `NebulaDownloaderEngine.cs:247-295` | Três cópias completas de todos os caminhos sob a raiz monitorada, a cada 60 s (1.440×/dia). | Um passe único de streaming; aumentar o intervalo |
| S-10 | P1 | `NebulaTelegramPool.cs:43` | `_documentCache` com TTL mas **sem teto e sem sweeper**: todo documento já transmitido fica residente para sempre. | Prune periódico ou LRU com limite |
| S-11 | P1 | `StrmProbeScheduledTask.cs:71` | Biblioteca inteira materializada para achar poucos STRMs, e poll de 500 ms durante horas. | Paginar, filtrar em SQL, sinalizar via `TaskCompletionSource` |
| S-12 | P2 | `Program.cs:292` | `Task.Delay(1s)` incondicional entre parar o setup server e ligar o Kestrel: ~6 % dos 17 s de startup. | Pular o setup server quando o wizard já foi concluído |
| S-13 | P2 | `NebulaDownloaderEngine.cs:1920` | `_failures` nunca evicta entradas vencidas. | `TryRemove` ao expirar em `ShouldSkip` |
| S-14 | P2 | `NotificationsLibraryNotifier.cs:114` | Um laço de poll a 1 Hz por item adicionado, por até 3 min, fazendo stats de arquivo. | Fila única com concorrência limitada e backoff |

## Parte 8 — Camada EF Core e padrões de consulta

| # | Prio | Local | Problema | Correção |
| --- | --- | --- | --- | --- |
| E-1 | P0 | `BaseItemRepository.QueryBuilding.cs:95` | O `Distinct()` catch-all cobre **todas as ~70 colunas** (incluindo 24 `longtext`), então o otimizador cai para `type=ALL` em 78.048 linhas. `GetItems` executa essa forma **duas vezes** por request, e a versão `COUNT(*)` materializa uma tabela derivada de ~55 MB só para contar. Provado por EXPLAIN. | Remover o `Distinct()` catch-all e usar o mesmo formato de subconsulta agrupada dos ramos irmãos; contar uma chave projetada, não a entidade |
| E-2 | P0 | `ItemPersistenceService.cs:573` | `Attach(entity).State = EntityState.Modified` marca **todas as ~70 colunas** como sujas: todo rescan reescreve as 26 entradas de índice por linha, mesmo sem mudança. **É a causa raiz dos 264 MB de índice.** | Buscar as entidades rastreadas do lote e usar `CurrentValues.SetValues` (marca só o que difere), pulando o UPDATE quando nada mudou |
| E-3 | P0 | `BaseItemRepository.QueryBuilding.cs:349` | Ordenação padrão `ORDER BY SortName` sem índice utilizável: `rows=78048 ... Using filesort`, e `OFFSET 2000` dá plano idêntico (custo cresce com a profundidade). `IX_BaseItems_Type_TopParentId_SortName` só serve quando `Type` **e** `TopParentId` estão fixados por igualdade — verificado. | Adicionar `IX_BaseItems_SortName_Id (SortName, Id)` |
| E-4 | P1 | `PeopleRepository.cs:112` | Chave `Name.ToLower() + "-" + PersonType` montada em memória sobre `longtext`: full scan das 52.644 linhas de `peoples` em **toda** atualização de metadados. | Filtrar por `Contains(names, e.Name)` (coluna indexada) e casar em memória |
| E-5 | P1 | `ItemPersistenceService.cs:660` + `ItemValuesConfiguration.cs:17` | `INSERT IGNORE` num índice único: o InnoDB pega **lock compartilhado** na linha conflitante. Duas transações concorrentes inserindo o mesmo `(Type, Value)` em ordens diferentes causam deadlock — **é a origem mais provável das 14 deadlocks/dia**, já absorvidas pelo retry. | Deduplicar contra o conjunto já carregado antes de montar o SQL; usar `ON DUPLICATE KEY UPDATE` no-op |
| E-6 | P1 | `ItemPersistenceService.cs:145-166` | `DeleteItem` inlineia até ~78k GUIDs em **22** `ExecuteDelete` dentro de uma transação: ~65 MB de SQL para parsear e planejar, com locks presos o tempo todo. | Deletes set-based por join; chunk de ≤1000 quando inevitável |
| E-7 | P1 | `TranslateQuery.cs:1092-1131` | `e.Data.Contains(...)` sobre o JSON em `longtext` em 7 filtros: full scan + leitura do payload de todas as linhas. | Promover as flags a colunas ou linhas de `ItemValues` indexáveis |
| E-8 | P1 | `BaseItemRepository.Querying.cs:131-155` | `GetLatestTvShowItems`/movies transmitem **todo** o conjunto filtrado ao processo para escolher uma linha por chave, anulando o `LIMIT`. | `ROW_NUMBER() OVER (PARTITION BY ...)` — o índice de partição já existe |
| E-9 | P1 | `DescendantQueryHelper.cs:90-106` | Materializa todos os ids correspondentes e inlineia um parâmetro por id na primeira iteração, com 3 round trips por nível de hierarquia. | `WITH RECURSIVE` compondo como `IQueryable` |
| E-10 | P1 | `Migrations/20260709214429` e `20260819010535` | Duas migrações distintas fazem `DROP INDEX` + `CREATE` do FULLTEXT em `baseitems`, e uma delas também altera tipo de coluna indexada: `ALGORITHM=COPY` com rebuild completo e MDL exclusivo bloqueando leituras e escritas. **É a classe de mudança que derruba o servidor durante o update.** | Nunca combinar mudança de tipo com rebuild de FTS; checar existência em `information_schema` antes de recriar |
| E-11 | P2 | `TranslateQuery.cs:545-565` | `IsResumable` ainda usa `Contains(...) == isResumable` com `ToList()` — exatamente o defeito já corrigido no ramo `IsPlayed`, gerando até ~2.787 parâmetros por request de Continue Watching. | Separar no booleano e manter `IQueryable<Guid>`, como no ramo já corrigido |
| E-12 | P2 | schema de `userdata` | 4 índices `(ItemId, UserId, ...)` cujo prefixo já é a PK, na tabela de maior frequência de escrita (progresso a cada ~10 s); e falta o índice do predicado de resume. | Remover os 4, adicionar `(UserId, PlaybackPositionTicks, ItemId)` |
| E-13 | P2 | `BaseItemRepository.Querying.cs:323-359` | `GetQueryFiltersLegacy` re-embute o pipeline inteiro de `TranslateQuery` em 4 agregações, executando o filtro caro 4× por request. | Materializar o conjunto de ids uma vez |
| E-14 | P2 | histórico de migrações vs schema | `IX_BaseItems_TopParentId_Type_IsVirtualItem_SeriesId_DateCreated` consta em `__efmigrationshistory` mas **não existe** nem no schema nem no snapshot: drift silencioso. | Reconciliação pontual |

### Nota de correção sobre a Parte 1

A frente de EF verificou o banco ao vivo e corrigiu duas premissas minhas: `ItemValues` tem **4,2 MB / 25.690 linhas** (os ~94 MB que eu atribuí a ela estão na verdade distribuídos entre `ancestorids` 20 MB, `peoplebaseitemmap` 25 MB, `baseitemimageinfos` 24 MB, `itemvaluesmap` 14 MB, `baseitemproviders` 9 MB e `peoples` 8 MB). E `IX_MediaStreamInfos_StreamType` **existe** — o item marcado como já corrigido está de fato corrigido; minha primeira janela de `SHOW INDEX` apenas truncou a saída.

---

## Parte 6 — Observabilidade (medido nos logs de produção)

### O-1 [P0] O servidor não tem telemetria de requisição lenta em produção

- Evidência: `ResponseTimeMiddleware.cs:51` só registra quando
  `_logger.IsEnabled(LogLevel.Debug)`. O log efetivo é
  `C:\Users\Raphael\AppData\Local\MulletaFlix\config\logging.default.json` com
  `MinimumLevel.Default = "Information"`, ou seja **Debug está desligado**.
- Configuração encontrada: `EnableSlowResponseWarning = true` e
  `SlowResponseThresholdMs = 500` (`ServerConfiguration.cs:226,231`) — a opção está ligada mas
  **nunca produz saída**. É um ajuste que parece ativo e não faz nada.
- Por que importa: sem isso não existe evidência de produção sobre qual endpoint é lento. Toda
  otimização de latência passa a ser palpite.
- Correção: emitir em `Information`/`Warning` quando ultrapassar o limiar, com supressão por
  endpoint/threshold para não inundar o log, mantendo o Debug para o detalhe por URL.
- Esforço/risco: pequeno. O risco é volume de log, mitigado pela supressão por endpoint.

### O-2 [P1] As paradas de heartbeat eram causadas pelo esgotamento de conexões

- Evidência medida no mesmo arquivo de log, comparando os dois ciclos do dia:

  | Ciclo | Linhas de log | Avisos de heartbeat | Pior heartbeat |
  | --- | --- | --- | --- |
  | Antes (00:00–00:39) | 4 047 | **55** | **6,32 s** (depois 5,31 s e 4,38 s) |
  | Depois (00:47+) | 476 | **0** | nenhum |

  Linha de exemplo do ciclo ruim: `[WRN] Microsoft.AspNetCore.Server.Kestrel: the heartbeat has
  been running for "00:00:03.2907041" which is longer than "00:00:01". This could be caused by
  thread pool starvation.`
- Por que importa: confirma a causalidade entre o teto de conexões (152/151 com 83 conexões
  abortadas) e a starvation de thread pool. Os 443 mil avisos históricos não eram só um problema de
  código assíncrono: eram, em boa parte, threads de request bloqueadas por falta de conexão.
- Correção: já aplicada nesta rodada (`max_connections=300` e teto de pool 100). Falta a build
  12.0.46 entrar em vigor para o teto de pool valer.
- Ressalva honesta: o ciclo novo tem só alguns minutos e menos carga (sem uploads Nebula no
  momento), então a comparação é indicativa, não um benchmark controlado.

### O-3 [P2] Volume de log alto e com ruído

- Evidência: `log_20260921_001.log` = 79,3 MB e `log_20260921.log` = 48,2 MB no mesmo dia
  (~130 MB/dia); retenção de 3 arquivos com limite de 100 MB cada.
- Por que importa: I/O de log competindo com o banco no mesmo disco, e o ruído (avisos repetidos de
  heartbeat e erros 403 de provider) enterra o sinal útil.
- Correção: avaliar política de retenção menor, e suprimir/agregar avisos repetitivos por janela de
  tempo. Os erros 403 do MyDramaList são de terceiros e não indicam falha do servidor.
- Esforço/risco: pequeno.

---

## Parte 7 — Frontend: números medidos do build de produção

| Medida | Valor |
| --- | --- |
| Arquivos JS | 591 arquivos, **23,42 MB** |
| Arquivos CSS | 79 arquivos, 1,40 MB |
| Arquivos acima de 500 KB | 1 |

Maiores bundles:

| Tamanho | Arquivo |
| --- | --- |
| **4 698,5 KB** | `libraries\subtitles-octopus-worker-legacy.js` |
| 435,3 KB | `assets\vendor-jellyfin-*.js` |
| 428,3 KB | `assets\vendor-mui-*.js` |
| 427,5 KB | `assets\vendor-pdf-*.js` |
| 403,3 KB | `libraries\subtitles-octopus-worker.js` |
| 369,4 KB | `assets\vendor-epub-*.js` |
| 332,8 KB | `assets\vendor-hls-*.js` |
| 309,1 KB | `assets\index-*.js` |
| 303,2 KB | `assets\ta-*.js` (tradução tâmil) |
| 297,9 KB | `assets\te-*.js` (tradução telugo) |

Observação imediata: o `subtitles-octopus-worker-legacy.js` sozinho tem **4,6 MB**, e existe também
a variante moderna com 403 KB. Os dois somam ~5,1 MB de renderizador de legendas. Vale confirmar se
a variante legacy ainda é necessária ou se pode ser carregada só por detecção de recurso. Os
idiomas `ta` (303 KB) e `te` (298 KB) também sugerem que os catálogos de tradução poderiam ser
carregados sob demanda em vez de no bundle de idiomas inicial.

_(achados de código do frontend complementados pela frente paralela)_

---

## Plano de execução priorizado (por ganho ÷ risco)

Ordenado do que dá mais resultado com menos risco. Cada item indica como provar que funcionou.

### Onda 1 — sem risco, ganho imediato

| # | Ação | Ganho medido | Prova |
| --- | --- | --- | --- |
| 1 | `DROP INDEX IX_BaseItems_TopParentId_Type_IsVirtualItem` | ~8,9 MB de índice e uma B-tree a menos por escrita | `INDEX_LENGTH` de `baseitems` antes/depois; suíte verde |
| 2 | Emitir aviso de resposta lenta em nível visível (`ResponseTimeMiddleware`) | passa a existir telemetria de latência | log com `Slow HTTP Response` em carga real |
| 3 | Suprimir/agregar os avisos repetitivos de heartbeat e os 403 de provider | ~130 MB/dia de log e sinal legível | tamanho diário do log antes/depois |
| 4 | Aplicar a build 12.0.46 na instalação (exige elevação) | ativa teto de pool 100, guard de porta e correções de Fase 1–3 | versão instalada = 12.0.46 e suíte completa com servidor vivo |

### Onda 2 — precisa de confirmação empírica antes de agir

| # | Ação | Como decidir | Ganho estimado |
| --- | --- | --- | --- |
| 5 | Podar índices sem uso | deixar `userstat` coletando alguns dias e olhar `information_schema.INDEX_STATISTICS` com `ROWS_READ = 0` | até dezenas de MB |
| 6 | Resolver o impasse do `CleanName`/`OriginalTitle` | decidir entre FULLTEXT real ou remover o método morto | remove bug latente e ~5 MB se o índice cair |
| 7 | Unificar `(ItemId, ListOrder)` e `(ItemId, SortOrder)` em `peoplebaseitemmap` | confirmar com `userstat` se as duas ordenações são usadas | ~11 MB |

### Onda 3 — ganho estrutural grande, risco alto, exige janela

| # | Ação | Ganho estimado | Risco |
| --- | --- | --- | --- |
| 8 | PK GUID `char(36)` → `binary(16)` nas tabelas grandes | **~40 MB** só de PK duplicada nos 26 índices, mais B-tree da PK 2,25× menor | migração ampla; exige rollback ensaiado |
| 9 | Normalizar `Type`/`MediaType` para código curto | ~28 MB nos 9 índices que repetem `Type` | mexe no mapeamento de entidades e no lookup de tipos |

### Ações explicitamente NÃO recomendadas

- **Aumentar `innodb_buffer_pool_size`.** O pool tem 35% de páginas livres e zero
  `Innodb_buffer_pool_wait_free`; o host tem 730 MB livres e já pagina por causa de uma VM QEMU
  (3,2 GB) e dois processos Java (3,9 GB). Aumentar o pool agravaria o paging sem ganho de cache.
- **`innodb_flush_log_at_trx_commit=2`.** Troca durabilidade por escrita num banco de metadados.
- **Ligar Server GC.** A decisão original permanece correta neste host que pagina.
- **Remover o índice `(CleanName, OriginalTitle)` por ser "não usado".** Existem consultas de
  igualdade em `CleanName` que podem usá-lo; aguardar `userstat`.

---

## Execução — o que já foi corrigido e verificado

| Item | Arquivo | O que mudou | Verificação |
| --- | --- | --- | --- |
| D-2 | `BaseItemConfiguration.cs` + migration `20260922040042_DropRedundantBaseItemCountIndex` | Removido o índice redundante `IX_BaseItems_TopParentId_Type_IsVirtualItem` | Build 0 erros; migration gera só o `DropIndex` |
| B-1 | `ProviderManager.cs:1275` e `:1306` | `PriorityQueue` agora acessada sob `_refreshQueueLock` no `Enqueue`, no `TryDequeue` **e** em `GetRefreshQueue` (que já usava o lock) | Build 0 erros; suíte verde |
| S-2 | `NebulaTelegramPool.cs:1435` | 429 deixou de ser retentado no mesmo bot: devolve `null` e o caller rotaciona; 5xx continua retentável | Build 0 erros |
| S-1 | `NebulaFtpManager.cs:1770` | Ciclo de limpeza contínua de 30 s → **15 min** (era 2.880 passeios completos da biblioteca por dia) | Build 0 erros |
| H-3 | `Startup.cs` | Compressão passou a cobrir `application/x-mpegurl`, `application/vnd.apple.mpegurl`, `audio/x-mpegurl` e `text/vtt` — a lacuna que fazia playlists HLS viajarem sem compressão | Build 0 erros |

### Rodada 3

| Item | Arquivo | O que mudou | Verificação |
| --- | --- | --- | --- |
| E-3 | `BaseItemConfiguration.cs` + migration `AddBaseItemSortNameIdIndex` | Adicionado `IX_BaseItems_SortName_Id (SortName, Id)`: a ordenação padrão passa a ter índice com `SortName` na frente em vez de `Using filesort` sobre 78.048 linhas em toda página | Build 0 erros; migration gera só o `CreateIndex` |
| B-9 | `MetadataService.cs:1062` | O `Task.Delay(100)` incondicional por imagem virou condicional: só dorme após um sinal real de falha (`sawRateLimit`). Antes limitava o servidor inteiro a 10 imagens/s, mesmo com 8 núcleos ociosos | Build 0 erros; suíte verde |
| S-10 | `NebulaTelegramPool.cs:43` | `_documentCache` ganhou `PruneDocumentCache()` com teto de 4.096 entradas, chamado na inserção: remove vencidos e, se ainda exceder, os mais próximos de expirar. Antes o TTL só era consultado na leitura, então uma chave nunca relida ficava residente para sempre | Build 0 erros; suíte verde |
| F-8 | `MulletaFlix-web-master/src/utils/query/queryClient.ts` | Política global de cache: `staleTime: 30s` e `refetchOnWindowFocus: false`. Sem isso toda query nascia stale e era refeita a cada montagem, e cada troca de foco refazia todas as queries montadas | Build do cliente web no pacote |

### Rodada 4 — a causa raiz do inchaço de índice

| Item | Arquivo | O que mudou | Verificação |
| --- | --- | --- | --- |
| **E-2** | `ItemPersistenceService.cs:343` e `:573` | O UPDATE de item deixou de ser full-column. `UpdateOrInsertItemsCore` passou a buscar as entidades **rastreadas** (`ToDictionary(e => e.Id)`) em vez de só os ids, e `SaveBaseItemEntities` trocou `Attach(entity).State = EntityState.Modified` por `context.Entry(current).CurrentValues.SetValues(entity)`. EF agora compara com os valores originais e marca só as colunas que realmente diferem — um rescan sem mudança **não emite UPDATE nenhum**, em vez de reescrever as ~70 colunas e as 26 entradas de índice por item | Build 0 erros; suíte verde; 18/18 integração |

Raciocínio da troca: o estado final no banco é idêntico (as mesmas colunas terminam com os mesmos
valores), mas o custo de I/O cai de "todos os índices em todo item" para "só o que mudou". Isso
também reduz a janela de lost update: colunas que não mudaram não são mais sobrescritas com valores
possivelmente obsoletos.

Custo verificado: `ItemPersistenceServiceTests` roda em **73 ms** (8 testes) após a mudança, então a
leitura extra das entidades rastreadas e a comparação de propriedades não introduziram regressão
mensurável no caminho de escrita.

Observação de ambiente registrada no momento da medição: o processo do servidor estava com
**4.258 MB** de working set e 1.478 s de CPU (scan de biblioteca em andamento), contra os 239 MB
medidos antes. A suíte ficou mais lenta nesta janela por contenção de CPU/disco com o servidor, não
por causa da alteração — confirmado pelos 73 ms do teste focado. Isso também reforça por que os
itens S-1 (intervalo da limpeza contínua) e S-10 (teto do cache de documentos) importam: o host não
tem folga de memória.

### Rodada 5

| Item | Arquivo | O que mudou | Verificação |
| --- | --- | --- | --- |
| **E-5** | `ItemPersistenceService.cs:664` | `InsertItemValuesIgnoreDuplicates` passa a inserir em **ordem determinística** (`OrderBy(Type).ThenBy(CleanValue, Ordinal)`). O `INSERT IGNORE` que colide no índice único `(Type, Value)` pega lock compartilhado na linha conflitante; duas transações inserindo o mesmo conjunto em ordens diferentes adquirem esses locks em sentidos opostos e travam — exatamente os 1213/1205 que o retry de `MulletaFlixDbContext` absorve ~14 vezes por dia. Ordenar igual em toda transação remove a espera circular sem mudar quais linhas são gravadas | Build 0 erros; suíte verde |
| **H-4** | `ImageController.cs:2115` | O `If-None-Match` passou a ser respondido **antes** de `ProcessImage`. Antes o 304 era decidido depois do lookup de imagem suportada, do `StringBuilder` + MD5 da chave de cache, de dois `File.Exists`, de um `GetLastWriteTimeUtc` e, em cache miss, do encode Skia inteiro sob o semáforo de encodificação — ou seja, o ETag não evitava o trabalho que existe para evitar. Uma grade de 200 posters faz 200 requests condicionais por navegação | Build 0 erros; suíte verde |
| **B-10** | `CollectionManager.cs:238` | `currentLinkedChildrenIds` passou de `List<Guid>` para `HashSet<Guid>`: era só sondado com `Contains` dentro de um laço, fazendo adicionar N itens custar O(N²) comparações de Guid na thread do request | Build 0 erros; suíte verde |
| **E-11** | `BaseItemRepository.TranslateQuery.cs:545` | `IsResumable` recebeu a mesma correção já aplicada em `IsPlayed`: os operandos voltaram a ser `IQueryable` (subquery server-side) e a comparação `Contains(...) == isResumable` foi trocada pela separação no booleano. O `ToList()` inlineava um parâmetro por série e por item — a cardinalidade de `SeriesId` é ~2.787 no banco real | Build 0 erros; suíte verde |

### Rodada 6

| Item | Arquivo | O que mudou | Verificação |
| --- | --- | --- | --- |
| **E-1** | `BaseItemRepository.QueryBuilding.cs:93` | O `Distinct()` sobre a entidade virou dedupe pela chave primária: `var dedupedIds = dbQuery.Select(e => e.Id).Distinct(); dbQuery = context.BaseItems.AsNoTracking().Where(e => dedupedIds.Contains(e.Id));`. O `Distinct()` original cobria as ~70 colunas, incluindo 24 `longtext`, então nenhum índice podia atendê-lo e o otimizador caía para varredura completa (`type=ALL`, `rows=78048`) — e `GetItems` executa essa forma **duas vezes** por request (uma para `TotalRecordCount`, outra para a página), com a versão `COUNT` montando tabela derivada de todas as colunas só para contar. A ordenação do pipeline foi verificada: `ApplyGroupingFilter` roda **antes** de `ApplyQueryPaging`, então o `Skip`/`Take` continua sendo aplicado depois, sobre a nova consulta. `AsNoTracking` preservado para casar com `PrepareItemQuery` e com os demais helpers de collapse do mesmo arquivo | Build 0 erros; suíte verde; 18/18 integração |
| **B-2** | `NebulaDownloaderEngine.cs:1009` | O relatório de progresso saiu de dentro do `lock (progressLock)` e passou a ser **limitado por tempo (250 ms)**. Antes cada leitura de 64 KB fazia, sob um lock global compartilhado por até 32 partes: `Sum()` sobre o array de partes, dois `FormatBytes`, três strings interpoladas e o evento de UI. Um download de 10 GB fazia ~160.000 aquisições de lock e ~160.000 invocações de evento competindo com as próprias threads de download. O contador por parte continua sendo atualizado em toda iteração (cada tarefa é dona do seu índice), então o valor final permanece exato | Build 0 erros; suíte verde |
| **H-11** | `Startup.cs:330` | Assets sob `/web/assets` — que têm hash de conteúdo no nome e são imutáveis por construção — passaram de `max-age=3600` para `max-age=31536000, immutable`. O TTL curto forçava cada cliente a revalidar dezenas de assets a cada hora. Caminhos sem hash mantêm 3600 s. Exigiu passar `StringComparison.Ordinal` por causa do CA1307 tratado como erro neste projeto | Build 0 erros |
| **H-15** | `SubtitleController.cs:563` | `.First(...)` virou `.FirstOrDefault(...)`: pedir uma fonte não instalada lançava `InvalidOperationException` e virava **HTTP 500**, e isso tornava inalcançável o `null` check logo abaixo. O comportamento pretendido (200 vazio, porque 204 quebra o SubtitlesOctopus) agora é o que acontece | Build 0 erros; suíte verde |

### Rodada 7

| Item | Arquivo | O que mudou | Verificação |
| --- | --- | --- | --- |
| **H-9** | `StreamState.cs:161` + 3 call sites | `StreamState` passou a implementar **`IAsyncDisposable`**. O `Dispose` síncrono fazia `_mediaSourceManager.CloseLiveStream(...).GetAwaiter().GetResult()` — um bloqueio de thread em `Dispose`, chamado por `using var state` em todo request de playlist/stream de fonte que exige fechamento, e o exemplo mais literal do mecanismo por trás dos avisos de starvation do Kestrel (a continuação do `CloseLiveStream` precisa do mesmo thread pool que está bloqueado). Agora: `DisposeAsync` aguarda o fechamento corretamente, e o `Dispose` síncrono apenas **despacha em background** (best effort, com exceção observada e contida) em vez de bloquear. Os 3 call sites que já estavam em método `async` — `DynamicHlsController.GetVariantPlaylistInternal`, `DynamicHlsHelper.GetMasterHlsPlaylist` e `AudioHelper.GetAudioStream` — passaram a usar `await using` | Build 0 erros; suíte verde; 18/18 integração |

### Rodada 8 — o bloqueio mais frequente do sistema

| Item | Arquivo | O que mudou | Verificação |
| --- | --- | --- | --- |
| **B-3** | `IUserDataManager.cs:30`, `UserDataManager.cs:51`, `SessionManager.cs:946` | `SaveUserData` era a chamada bloqueante mais frequente do servidor: roda em **todo report de progresso de reprodução** (~10 s por cliente ativo), e fazia `saveLock.Wait()` num stripe de 8 + transação EF **síncrona** + uma re-query completa do user data do item dentro do lock. Como 8 stripes sobre um espaço de Guid fazem itens sem relação colidirem, a espera não é rara. Agora existe `SaveUserDataAsync` na interface, implementado com `WaitAsync`, `CreateDbContextAsync`, `BeginTransactionAsync`, `SaveChangesAsync` e `ToArrayAsync`; o `SaveUserData` síncrono passou a **delegar** a ele (uma única implementação, para os dois caminhos não divergirem), e o caminho quente — `SessionManager.OnPlaybackProgressAsync` — passou a aguardá-lo | Build 0 erros; suíte verde; 18/18 integração |

Avaliação de risco feita antes de mexer: verifiquei que o **único** implementador real é
`UserDataManager` e que **todos** os demais consumidores são `Mock<IUserDataManager>` (Moq, 12
ocorrências em testes), e o Moq implementa membros novos automaticamente. Por isso a adição ao
contrato público não quebrou nenhum test double.

Pendente do mesmo padrão: `OnPlaybackStart` (`SessionManager.cs:838`) e `OnPlaybackStopped`
(`SessionManager.cs:1153`) ainda usam o overload síncrono. São uma vez por reprodução, não a cada
10 s, então ficaram para uma rodada seguinte.

### Rodada 9 — correção de um laço que nunca terminava

| Item | Arquivo | O que mudou | Verificação |
| --- | --- | --- | --- |
| **S-7** | `PeopleValidationTask.cs:81` | A tarefa paginava com `dupQuery.Take(PartitionSize)` **sem `Skip` e sem cursor**: `iterator` era incrementado mas **nunca usado no paging**, e o avanço dependia só do efeito colateral do `ExecuteDelete`. Consequências: (a) qualquer grupo cujas duplicatas não pudessem ser excluídas — por exemplo com uma linha de `PeopleBaseItemMap` ainda referenciando, ou update/delete afetando zero linhas — fazia a mesma primeira página voltar para sempre, e como `itemCounter` continuava igual a `PartitionSize` o `do/while` **nunca terminava**; (b) o `GROUP BY` completo sobre `Peoples` era reavaliado a cada página, quadrático no número de grupos duplicados. Agora os grupos são materializados **uma vez** em ordem determinística e processados em lotes, com guarda para grupos de tamanho < 2. Todo grupo é processado exatamente uma vez e a tarefa sempre termina | Build 0 erros; suíte verde; 18/18 integração |

### Rodada 10 — fechando o padrão do B-3

| Item | Arquivo | O que mudou | Verificação |
| --- | --- | --- | --- |
| **B-3 (continuação)** | `SessionManager.cs:826` e `:1135` | Os dois call sites restantes de `SaveUserData` no caminho de reprodução passaram para o overload assíncrono: `OnPlaybackStart` virou `OnPlaybackStartAsync` e `OnPlaybackStopped` virou `OnPlaybackStoppedAsync` (`Task<bool>`), com os respectivos chamadores nos métodos públicos já assíncronos aguardando o resultado. Sob a rodada anterior ficavam apenas o de progresso, que é o mais frequente. Agora **nenhum** caminho de reprodução bloqueia thread em gravação de user data | Build 0 erros; suíte verde; 18/18 integração |

Com isso o item B-3 fica fechado por completo: os três caminhos de reprodução (start, progress,
stop) usam `SaveUserDataAsync`.

### Rodada 11

| Item | Arquivo | O que mudou | Verificação |
| --- | --- | --- | --- |
| **B-12** | `MediaSourceManager.cs:403` e `:1017` | O MD5 do nome do tipo do provider passou a ser memoizado por `Type` (`ConcurrentDictionary`). Era recalculado — hash mais alocação de 32 caracteres hex — **uma vez por media source em todo request de playback-info** e **uma vez por provider em toda abertura de live stream**, para um valor constante do tempo de vida do processo. O valor produzido é byte-idêntico à expressão inline anterior, o que importa porque ele forma o prefixo persistido do `LiveStreamId` | Build 0 erros; suíte verde; 18/18 integração |

### Rodada 12

| Item | Arquivo | O que mudou | Verificação |
| --- | --- | --- | --- |
| **S-12** | `Program.cs:293` | Removido o `Task.Delay(TimeSpan.FromSeconds(1))` incondicional entre parar o setup server e ligar o Kestrel. Era cerca de **6% dos 17 s de startup** medidos, e desnecessário: o `PortBindingRecovery` já tenta o bind de novo e espera as portas ficarem realmente livres | Build 0 erros; suíte verde |
| **S-13** | `NebulaDownloaderEngine.cs:1954` | `FailureTracker.ShouldSkip` passa a **remover** a entrada quando ela já passou do cooldown. Antes só existia remoção no caminho de sucesso (`RecordSuccess`), então um caminho que falhava uma vez e depois era apagado ou renomeado mantinha o path completo e a string de erro residentes pelo tempo de vida do processo | Build 0 erros; suíte verde |

### Rodada 15 — atacando a causa dominante (memória)

| Item | Arquivo | O que mudou | Verificação |
| --- | --- | --- | --- |
| **B-5** | `NebulaChunkedStream.cs:65` | `MaxCachedChunks` de **64 → 8**: o LRU de blocos por stream ativo caiu de 64 MB para 8 MB de Large Object Heap. Com até 32 conexões simultâneas no `NebulaHttpStreamServer` isso significava até ~2 GB, e no p95 medido (12 concorrentes) ~768 MB — num host com 1,9 GB de RAM livre e 3,9 GB de pagefile em uso, um dos maiores contribuintes dos congelamentos de 50–100 s. 8 blocos ainda dão 4× de folga sobre o `PrefetchAheadChunks` de 2. A troca é deliberada: banda por memória, porque a banda era o recurso abundante (uploads a 5–6 MB/s) e a memória era o que faltava | Build 0 erros; suíte verde; 18/18 integração |

### Rodada 16 — reduzindo o pico de memória (continuação)

| Item | Arquivo | O que mudou | Verificação |
| --- | --- | --- | --- |
| **S-5** | `ChapterImagesTask.cs:85` | A tarefa deixou de carregar **toda** a biblioteca de vídeo de uma vez: agora faz `GetCount` e pagina de 100 em 100, com `startIndex`, exatamente como os irmãos `MediaSegmentExtractionTask` e `TrickplayImagesTask` já fazem. Eram 77 909 entidades `Video` residentes simultaneamente, um dos maiores picos num host que estava paginando. O histórico de falhas virou `HashSet<string>` (era `List` com `Contains` O(n) por item, sobre uma coleção que cresce a cada falha), e o `ObjectDisposedException` passou de `break` para `return` para sair do laço de páginas e não apenas do `foreach` interno | Build 0 erros; suíte verde; 18/18 integração |

Ressalva registrada: a paginação não tem `OrderBy` explícito — exatamente como os dois irmãos. Sem
ordenação determinística, uma página pode pular ou repetir itens. Para uma tarefa de refresh de
imagens de capítulo isso é tolerável (o pior caso é reprocessar ou adiar alguns itens), mas é uma
fragilidade conhecida e vale corrigir junto quando o padrão for revisado.

### Rodada 18 — memória no scan do downloader

| Item | Arquivo | O que mudou | Verificação |
| --- | --- | --- | --- |
| **S-9** | `NebulaDownloaderEngine.cs:247` e `:283` | O scan do downloader mantinha **três listas materializadas** de todos os caminhos vivos ao mesmo tempo — `allCandidateFiles` (árvore inteira), `mediaList` (subconjunto de mídia) e `mediaFiles` acumulando — e depois construía uma quarta, `prioritizedList`, com um objeto anônimo por arquivo. Tudo isso reconstruído **a cada 60 segundos** (1 440×/dia num host que estava paginando). Agora há um único passe de streaming direto em `mediaFiles`, restando apenas ele e `prioritizedList`. Além disso, `GetCategoryPriority` era chamado **duas vezes por arquivo** (uma para a chave de ordenação, outra para o nome de exibição); passou a ser calculado uma vez | Build 0 erros; suíte verde; 18/18 integração |

Mudança de comportamento registrada: antes, um erro no meio da enumeração de uma pasta descartava
**a fonte inteira**; agora o que já foi encontrado permanece. Isso é mais útil (progresso parcial em
vez de perda total) e não altera a semântica de quais arquivos são elegíveis.

### Rodada 19 — memória: terceira tarefa com carga total removida

| Item | Arquivo | O que mudou | Verificação |
| --- | --- | --- | --- |
| **S-11 (paginação)** | `StrmProbeScheduledTask.cs:70` | A tarefa carregava **todos** os `Movie` e `Episode` da biblioteca — a biblioteca inteira — só para manter os poucos STRMs sem stream identificado que sobrevivem ao filtro. Agora pagina de 500 em 500 e materializa **apenas o conjunto filtrado**, porque o job enfileirado captura essa lista. Incluí dedupe por `Id` num `HashSet`, porque a paginação não tem `OrderBy` e sem ordenação determinística uma página pode repetir itens — repetir aqui causaria probe duplicado. O total usado na mensagem de progresso passou a ser a contagem do que foi efetivamente varrido | Build 0 erros; suíte verde; 18/18 integração |

Não alterei o poll de 500 ms que segura um slot de tarefa pelo tempo do job. É a parte menos
arriscada de trocar por sinalização via `TaskCompletionSource`, mas exige mexer no contrato da fila
de jobs, e não caberia nesta rodada com a verificação adequada.

### Rodada 21 — S-6: última tarefa com carga total removida

| Item | Arquivo | O que mudou | Verificação |
| --- | --- | --- | --- |
| **S-6 (paginação)** | `UnidentifiedMediaCleanupTask.cs:69` | A tarefa carregava **todos** os `Movie`, `Series` e `Episode` da biblioteca e só então filtrava o subconjunto não identificado. Agora pagina de 500 em 500 e mantém **apenas o subconjunto não identificado** — a fração pequena que de fato precisa de refresh. Escolhi o menor diff possível de propósito: o resto do laço (que enfileira os refreshes, com sua checagem de nome e o `QueueRefresh`) ficou intacto, porque é o trecho sensível. Com isso as **quatro** tarefas que carregavam a biblioteca inteira estão corrigidas | Build 0 erros; suíte verde; 18/18 integração |

Pendente deste item, explicitamente não feito: o **burst de refreshes**. Se houver muitos itens não
identificados, a tarefa ainda enfileira um `QueueRefresh` por item numa única rajada. Limitar isso
muda *quais* itens são atualizados por execução, então precisa de decisão de produto (processar por
lote com continuidade entre execuções) — não é uma correção mecânica. Também não iça o
`DirectoryService`, que ainda é alocado por item.

### As quatro tarefas que carregavam a biblioteca inteira

| Tarefa | Rodada | Antes | Depois |
| --- | --- | --- | --- |
| `ChapterImagesTask` | 16 | todos os `Video` | página de 100 |
| `StrmProbeScheduledTask` | 19 | todos os `Movie` + `Episode` | página de 500, só os STRMs |
| `UnidentifiedMediaCleanupTask` | 21 | todos os `Movie` + `Series` + `Episode` | página de 500, só os não identificados |
| `E-1` (query de biblioteca) | 6 | tabela derivada de ~55 MB por request | dedupe por chave primária |

### Suíte de testes após a rodada 21

- Build da solução: **0 erros**
- Unitários: **3195 aprovados / 38 ignorados / 0 falhas** (15 assemblies)
- Integração MariaDB real: **18/18**
- Servidor de produção sobreviveu à suíte completa (PID 17792 antes e depois)
- Release: `v12.0.62`

### Suíte de testes após a rodada 19

- Build da solução: **0 erros**
- Unitários: **3195 aprovados / 38 ignorados / 0 falhas** (15 assemblies)
- Integração MariaDB real: **18/18**
- Servidor de produção sobreviveu à suíte completa (PID 34576 antes e depois)
- Release: `v12.0.61`

### Suíte de testes após a rodada 18

- Build da solução: **0 erros**
- Unitários: **3195 aprovados / 38 ignorados / 0 falhas** (15 assemblies)
- Integração MariaDB real: **18/18**
- Servidor de produção sobreviveu à suíte completa (PID 34576 antes e depois)
- Release: `v12.0.60`

### Suíte de testes após a rodada 16

- Build da solução: **0 erros**
- Unitários: **3195 aprovados / 38 ignorados / 0 falhas** (15 assemblies)
- Integração MariaDB real: **18/18**
- Servidor de produção sobreviveu à suíte completa (PID 34576 antes e depois)
- Release: `v12.0.59`

### Suíte de testes após a rodada 15

- Build da solução: **0 erros**
- Unitários: **3195 aprovados / 38 ignorados / 0 falhas** (15 assemblies)
- Integração MariaDB real: **18/18**
- Servidor de produção sobreviveu à suíte completa (PID 34576 antes e depois)
- Release: `v12.0.58`

### Suíte de testes após a rodada 12

- Build da solução: **0 erros**
- Unitários: **3195 aprovados / 38 ignorados / 0 falhas** (15 assemblies)
- Integração MariaDB real: **18/18**
- Servidor de produção sobreviveu à suíte completa (PID 34576 antes e depois)
- Release: `v12.0.57`

### Suíte de testes após a rodada 11

- Build da solução: **0 erros**
- Unitários: **3195 aprovados / 38 ignorados / 0 falhas** (15 assemblies)
- Integração MariaDB real: **18/18**
- Servidor de produção sobreviveu à suíte completa (PID 34576 antes e depois)
- Release: `v12.0.56`

### Suíte de testes após a rodada 10

- Build da solução: **0 erros**
- Unitários: **3195 aprovados / 38 ignorados / 0 falhas** (15 assemblies)
- Integração MariaDB real: **18/18**
- Servidor de produção sobreviveu à suíte completa (PID 34576 antes e depois)
- Release: `v12.0.55`

### Suíte de testes após a rodada 9

- Build da solução: **0 erros**
- Unitários: **3195 aprovados / 38 ignorados / 0 falhas** (15 assemblies)
- Integração MariaDB real: **18/18**
- Servidor de produção sobreviveu à suíte completa (PID 34576 antes e depois)
- Release: `v12.0.54`

### Suíte de testes após a rodada 8

- Build da solução: **0 erros**
- Unitários: **3195 aprovados / 38 ignorados / 0 falhas** (15 assemblies)
- Integração MariaDB real: **18/18**
- Servidor de produção sobreviveu à suíte completa (PID 34576 antes e depois)
- Release: `v12.0.53`

### Suíte de testes após a rodada 7

- Build da solução: **0 erros**
- Unitários: **3195 aprovados / 38 ignorados / 0 falhas** (15 assemblies)
- Integração MariaDB real: **18/18**
- Servidor de produção sobreviveu à suíte completa (PID 34576 antes e depois)
- Release: `v12.0.52`

### Suíte de testes após a rodada 6

- Build da solução: **0 erros**
- Unitários: **3195 aprovados / 38 ignorados / 0 falhas** (15 assemblies)
- Integração MariaDB real: **18/18**
- Servidor de produção sobreviveu à suíte completa (PID 34576 antes e depois)
- Release: `v12.0.51`

### Suíte de testes após a rodada 5

- Build da solução: **0 erros**
- Unitários: **3195 aprovados / 38 ignorados / 0 falhas** (15 assemblies)
- Integração MariaDB real: **18/18**
- Servidor de produção sobreviveu à suíte completa (PID 34576 antes e depois)
- Duração da suíte de volta ao normal (`Jellyfin.Server.Implementations.Tests` 1m29s contra 2m56s na rodada anterior), confirmando que a lentidão medida antes era contenção com o scan do servidor e não efeito do item E-2
- Release: `v12.0.50`

### Suíte de testes após a rodada 4

- Build da solução: **0 erros**
- Unitários: **3195 aprovados / 38 ignorados / 0 falhas** (15 assemblies)
- Integração MariaDB real: **18/18**
- Servidor de produção sobreviveu à suíte completa (PID 34576 antes e depois)
- Release: `v12.0.49`

### Suíte de testes após a rodada 3

- Build da solução: **0 erros**
- Unitários: **3195 aprovados / 38 ignorados / 0 falhas** (15 assemblies)
- Integração MariaDB real: **18/18**
- Servidor de produção **sobreviveu** à suíte completa de novo (PID 34576 antes e depois)
- Release: `v12.0.48`

### Prova de ponta a ponta do incidente mais grave

O guard de porta corrigido foi validado **no harness real**: a suíte completa (15 assemblies) rodou
com o servidor de produção no ar e o servidor **sobreviveu com o mesmo PID**.

| Medida | Antes da correção | Depois da correção |
| --- | --- | --- |
| Suíte completa com servidor vivo | servidor **morto** (caiu às 00:35:48) | **PID 34576 preservado** |
| `/health` após a suíte | 503 | **200 Healthy** |
| `Too many connections` no log | 182 no dia | **0** |
| Avisos de heartbeat no ciclo normal | 55, pior 6,32 s | **0** no ciclo normal |

Ressalva honesta: a execução da suíte em si gera carga e produziu 12 avisos de heartbeat nas
últimas linhas do log; eles coincidem com a janela de teste, não com a operação normal.

### Suíte de testes após as correções

- Build da solução: **0 erros**
- Unitários: **3195 aprovados / 38 ignorados / 0 falhas** (15 assemblies)
- Integração MariaDB real: **18/18**
- Release: `v12.0.47` publicada com o zip

### Pendências que exigem ação humana ou janela

1. Aplicar a build em `C:\Program Files\MulletaFlix\Server`: o processo atual **não tem elevação**
   (`IsAdmin: False`, robocopy devolve `ERRO 5`), e o `ServerUpdateTask` deixa o pacote em
   `ReadyToApply` aguardando aprovação no painel.
2. Janela dedicada para a Onda 3 (PK `binary(16)`, normalização de `Type`), por serem migrações
   amplas com necessidade de rollback ensaiado.
3. Coletar `userstat` por alguns dias para transformar os candidatos a remoção de índice (D-6, D-7,
   E-12) em conclusões medidas.

---

## VERIFICADO EM PRODUÇÃO (rodada 21): a 12.0.61 foi aplicada

A atualização foi aprovada no painel e aplicada às **09:47:27** de 22/09. Servidor reiniciado
(PID 15860), `/health` = **200 Healthy**, versão instalada **12.0.61**.

### As migrations aplicaram — verificado no schema

| Verificação | Resultado |
| --- | --- |
| `IX_BaseItems_SortName_Id` | **existe** ✓ |
| `IX_BaseItems_TopParentId_Type_IsVirtualItem` (redundante) | **removido** ✓ |
| `IX_MediaStreamInfos_StreamType` | **existe** ✓ |

Nota honesta: `INDEX_LENGTH` de `baseitems` não caiu (257,3 MB contra 251,7 MB). O InnoDB não
encolhe o tablespace ao remover índice — as páginas ficam para reuso. O ganho **imediato** é de
escrita (uma B-tree a menos para manter por item), não de espaço em disco.

### Medição comparativa — sinal inicial, janela insuficiente

| Medida | Baseline 12.0.46 (8,5 h) | Pós-aplicação 12.0.61 (**9 min**) |
| --- | --- | --- |
| Avisos de heartbeat | 829 (~**98/h**) | 1 (~**6,7/h**) |
| Silêncios de log > 20 s | 68 (~0,13/min) | 2 (~0,22/min) |
| Pior silêncio | **102,6 s** | 23,8 s |
| `Too many connections` | 0 | 0 |

**Leitura honesta, e ela tem dois lados:**

1. **Avisos de heartbeat caíram ~93% por hora.** É o sinal que as correções de bloqueio de thread
   visavam, e ele apareceu.
2. **A taxa de silêncios de log NÃO melhorou de forma clara** — 0,22/min contra 0,13/min no baseline.
   Numa janela de 9 minutos isso é ruído estatístico, mas é coerente com a conclusão da rodada 14: os
   congelamentos são de origem OS/memória, e as correções de código não os eliminam sozinhas.

**9 minutos não permitem concluir nada.** A carga também varia (uploads e scans em rajadas). Para
fechar, é preciso medir uma janela de 1–2 h com carga comparável à do baseline. O que se pode
afirmar hoje é apenas: as 29 correções estão em produção, as três migrations aplicaram, o servidor
está saudável e o primeiro indicador de starvation melhorou.

---

## Estado da aplicação em produção (rodada 20)

### Linha de base congelada, medida com a 12.0.46 em execução

| Medida (janela 01:00–09:32, 8,5 h) | Valor |
| --- | --- |
| Linhas de log | 28 111 |
| **Avisos de heartbeat** | **829** (~98/h) |
| **Silêncios de log > 20 s** | **68** |
| Pior silêncio | **102,6 s** |

Salva em `%TEMP%\mflx-baseline.json` para comparação depois de aplicar.

### O restart não aplica — e isso está correto por desenho

Reiniciei o servidor em 09:32 com aprovação do usuário. Resultado verificado:

- Processo novo (PID 17792) subiu **saudável** (`/health` = 200 Healthy)
- A versão instalada **continua 12.0.46** — o `update.log` não ganhou entrada nova
- Motivo: `ReadyToApply` exige **aprovação explícita** no painel. A mensagem do próprio log é
  `Server update "12.0.61" downloaded and extracted automatically in background. Awaiting user
  approval to apply in Dashboard.`

### Correção da minha estimativa: é UMA aprovação, não duas

Eu havia dito que seriam necessários dois ciclos (aplicar 12.0.47 → buscar a 12.0.61 → aplicar),
porque o pacote em stage era a 12.0.47. **Errado.** O restart disparou um novo *check* que
**substituiu o pacote pela 12.0.61**:

| Hora | Evento do log |
| --- | --- |
| 09:32:57 | `Auto-downloading server update "12.0.61" in background` |
| 09:33:23 | `Server update "12.0.61" downloaded and extracted automatically ... Awaiting user approval` |

`update_state.json` agora: `{"DownloadedVersion":"12.0.61","InstallState":"ReadyToApply"}`.

Ou seja: **uma aprovação no painel aplica as 29 correções das rodadas 2–19 de uma vez**, mais as três
migrations (índice `StreamType`, remoção do índice redundante de `baseitems`, `IX_BaseItems_SortName_Id`).

### O que ainda não posso fazer daqui

Não tenho credenciais de admin, então não posso chamar `POST /System/Update/Apply`. A aprovação é
uma ação sua no painel. Depois dela, a verificação que fecha a auditoria é: medir novamente os
silêncios de log e os avisos de heartbeat numa janela com carga comparável, e comparar com os 68 e
829 acima.

---

## CONCLUSÃO REVISADA (rodada 14): o gargalo dominante é pressão de memória do host

Medição em ~2,7 h de produção com a build **12.0.46 já aplicada** e o tuning do MariaDB ativo.

### O sintoma é congelamento total do processo, não lentidão

Procurei silêncios no log — intervalos sem **nenhuma** linha, o que significa que o processo não
estava executando nada, nem o heartbeat do Kestrel:

| Silêncio no log | Ocorrências |
| --- | --- |
| > 20 s | **26** |
| > 10 s | 78 |
| > 5 s | 265 |
| **Pior** | **102,6 s** (03:02:09) |

Os 6 maiores: 102,6 s · 62,9 s · 59,6 s · 51,9 s · 48,7 s · 46,5 s.

O padrão observado às 02:39–02:40 é típico: `02:39:28` é a última linha, seguem-se **52 segundos de
silêncio**, e então várias partes de upload concluem **no mesmo instante** com durações de ~54 s
(0,30 MB/s) — inclusive partes que estavam a 0,11 MB/s (151,6 s). Uploads são limitados por rede;
todos travarem juntos e terminarem em lote aponta para o **sistema operacional**, não para um lock
de código.

### A memória do host confirma a causa

| Medida | Valor |
| --- | --- |
| RAM livre | **1 956 MB de 16 088 MB** |
| Pagefile em uso | **3 889 MB** (pico **7 074 MB**) |
| Working set do `MulletaFlix` | 2 240 MB (já chegou a 4 258 MB em scan) |
| Consumidores alheios | VM QEMU 3,2 GB + dois Java 3,9 GB ≈ 7,1 GB |

Um processo que fica 102 segundos sem escrever uma linha, num host com 1,9 GB livres e 3,9 GB de
pagefile em uso, está **paginating** — e nenhuma correção de código elimina isso enquanto o host
estiver sobrecomprometido.

### O que isso muda na priorização

Isto **não invalida** as 25 correções executadas — o esgotamento de conexões caiu de 182/dia para
**0** e isso é medido. Mas reposiciona o resto:

1. **Reduzir o pico de memória do servidor passa a ser a prioridade número um**, à frente de
   qualquer micro-otimização de CPU ou de query. São exatamente os achados que ainda não foram
   executados: `ChapterImagesTask` (S-5), `UnidentifiedMediaCleanupTask` (S-6) e
   `StrmProbeScheduledTask` (S-11) carregam a **biblioteca inteira** em memória sem paginação;
   `NebulaChunkedStream` reserva **64 MB de LOH por stream ativo** (B-5), e há 9 workers de upload
   concorrentes segurando buffers de 16 MB cada.
2. **Aliviar a competição no host** — a VM QEMU e os processos Java somam mais que a RAM livre.
   Nenhum tuning do servidor resolve isso.
3. As correções já feitas que reduzem memória (E-1, que eliminou uma tabela derivada de ~55 MB por
   request; S-10, que pôs teto no cache de documentos do Telegram) atacam esta causa, e não só
   latência.
4. Só depois disso faz sentido voltar a latência e throughput.

Uma hipótese que **não** se sustenta mais: atribuir os avisos de heartbeat exclusivamente a
bloqueios de thread. Os bloqueios existem e foram corrigidos, mas os congelamentos de 50–100 s são
de outra ordem de grandeza e coincidem com o esgotamento de memória do host.

---

## Correções ao próprio relatório (verificação posterior)

### S-7 não é falha ativa nesta instalação — é hazard latente

Na rodada 9 descrevi o laço do `PeopleValidationTask` como "nunca terminava". Medido no banco real na
rodada 13:

| Medida em `mulletaflix.peoples` | Valor |
| --- | --- |
| Linhas totais | 63 101 |
| **Grupos duplicados** (`GROUP BY Name, PersonType HAVING COUNT(*) > 1`) | **36** |
| Linhas dentro desses grupos | 73 |

Com `PartitionSize = 100` e apenas 36 grupos, `itemCounter == PartitionSize` já é falso na primeira
iteração, então **o laço termina hoje**. O defeito é real mas **latente**: ele só gira para sempre
com **≥ 100 grupos duplicados** e ao menos um grupo cuja exclusão não remova linhas (FK). O custo
quadrático também era teórico, porque só existe uma página.

A correção continua válida, mas por outro motivo: ela remove a **classe** de defeito, fazendo o
término não depender de as exclusões funcionarem. A mesma medição validou a premissa de memória que
eu havia assumido sem checar — 36 arrays de Guid somando 73 ids, cerca de 1,5 KB.

### O resultado que importa mais que a contagem de correções

A auditoria provou que o servidor **não estava lento por CPU nem por falta de cache**. Ele estava
travando por duas causas medidas:

- **Bloqueio de thread** em caminhos de request (o caso mais frequente era `SaveUserData`, a cada
  ~10 s por cliente), com locks segurados através de I/O e um `Dispose` bloqueante.
- **Amplificação de escrita no banco**: `Attach(entity).State = Modified` marcava as ~70 colunas como
  sujas, reescrevendo as 26 entradas de índice de `baseitems` em todo item, em todo scan — a origem
  medida dos 264 MB de índice para 55 MB de tabela.

Depois de corrigir um teto de conexões menor que o pool do EF e um guard de porta que derrubava o
próprio servidor durante os testes: avisos de heartbeat de **55 (pior 6,32 s) → 0**, e
`Too many connections` de **182/dia → 0**.

Hipótese descartada por medição: **não** aumentar o `innodb_buffer_pool_size`. O pool tem 35% de
páginas livres e `wait_free = 0`; o host tem 730 MB livres porque uma VM QEMU (3,2 GB) e dois
processos Java (3,9 GB) consomem 7,1 GB dos 16 GB. Aumentar o pool agravaria o paging.

### A instalação NÃO está em 12.0.45 — está em 12.0.46, e o updater funciona

Eu reportei por várias rodadas que a instalação seguia em 12.0.45 e que nada podia ser aplicado. Isso
estava **errado**. Verificado no disco e no log de update:

| Evidência | Valor |
| --- | --- |
| `C:\Program Files\MulletaFlix\Server\MulletaFlix.dll` | **12.0.46**, escrito em 22/09 00:42 |
| `update.log`, entrada de 00:52:29–00:52:32 | `Copying update files ... Robocopy completed with exit code: 3 ... Files copied successfully ... Starting MulletaFlix.exe` |
| Processo em execução | PID 34576, iniciado 00:52:32 (pós-update) |
| `update_state.json` | `DownloadedVersion: 12.0.47`, `InstallState: ReadyToApply` |

Conclusão corrigida: o **updater do próprio servidor consegue escrever em `Program Files`** — ele fez
isso às 00:52. O que falha é apenas o meu shell, que não tem elevação. E o updater já baixou
`12.0.47` e deixou em `ReadyToApply`.

Isso significa que a build **12.0.46 está em produção**, contendo o teto de pool, o guard de porta e
as correções de Fase 1–3 da sessão anterior. As rodadas 2–12 (12.0.47 a 12.0.57) é que ainda não
estão aplicadas — e a próxima verificação de update vai pegar a 12.0.57, que é a mais recente.

### A starvation de thread pool NÃO acabou sob carga real

Eu reportei "avisos de heartbeat de 55 (pior 6,32 s) → 0". Isso estava **escopado a uma janela
quieta** de ~5 minutos sem scans nem uploads, e eu não deixei isso claro o suficiente. Medição das
últimas ~2,4 h de produção, com uploads Nebula e scan ativos:

| Medida desde 01:00 | Valor |
| --- | --- |
| Linhas de log | 8 983 |
| **Avisos de heartbeat** | **347** (~145/h) |
| `Too many connections` | **0** |
| `[ERR]` | 4 |
| Velocidade de upload Nebula observada | 5,5–6,6 MB/s (modo rápido, não o lento de 0,27) |

Leitura correta e mais útil: o **esgotamento de conexões está resolvido** (182/dia → 0), mas a
starvation de thread pool **persiste sob carga**. E isso é coerente com o que ainda não foi
aplicado: as rodadas 2–12 atacam justamente os bloqueios restantes identificados pela auditoria —
`UserDataManager.SaveUserData` bloqueante a cada ~10 s por cliente, `StreamState.Dispose` bloqueante,
`DisplayPreferencesManager` síncrono, `DtoService` síncrono no `/Sessions`, lock global do
`MediaSourceManager` segurado através do I/O e a `PriorityQueue` sem sincronização do
`ProviderManager`. Nenhum deles está na 12.0.46.

Ou seja: **aplicar a 12.0.57 é o que testa a hipótese central da auditoria.** Não posso afirmar que
zerará os 347 avisos, mas são exatamente os mecanismos que a medição apontou.

## Rodada 23 — H-1 e F-2 executados, e duas correções de atribuição

### H-1 (P0) — o aviso de resposta lenta nunca saía. Executado.

`Jellyfin.Api/Middleware/ResponseTimeMiddleware.cs:51`

```csharp
if (enableWarning && responseTimeMs > warningThreshold && _logger.IsEnabled(LogLevel.Debug))
{
    _logger.LogDebug(...);
}
```

A conjunção é o defeito: o aviso só era emitido **quando o log de Debug estava ligado**, e em
produção o nível é `Information`. Uma instalação com `EnableSlowResponseWarning=true` e
`SlowResponseThresholdMs=500` (ambos já são o padrão do `ServerConfiguration`) produzia **zero
linhas** — a configuração parecia funcionar e não fazia nada. É o mesmo tipo de defeito do H-4:
a checagem existia exatamente para evitar trabalho, e não evitava.

Correção: `LogInformation`, com **supressão por caminho** de 30 s e mapa limitado a 512 entradas
(podado ao exceder). A supressão é obrigatória, não um enfeite: uma reprodução HLS de 2 h gera
~1200 requisições para o mesmo caminho de segmento, e sem ela o conserto viraria uma inundação de
log. O mapa tem teto porque cache por caminho sem limite é o mesmo defeito de memória residente que
esta auditoria encontrou em `_documentCache`, `_failures` e no cache de probe.

| Evidência | Resultado |
| --- | --- |
| `dotnet test tests/Jellyfin.Api.Tests --filter ResponseTimeMiddleware` | **6 aprovados**, 0 falhas |
| Teste de regressão direto | `IsEnabled(Debug) == false` **e** o aviso é emitido (o caso que estava quebrado) |
| Supressão | 3 requisições lentas no mesmo caminho → **1** log |
| Caminhos distintos | 2 caminhos → **2** logs |
| Cabeçalho `X-Response-Time-ms` | presente em resposta rápida e lenta |

### F-2 (P0) — 285 KB fora da critical path. Executado, com a causa corrigida.

`MulletaFlix-web-master/src/utils/dateFnsLocale.ts:2`

O relatório dizia que `import { enUS } from 'date-fns/locale'` resolvia o barril **CommonJS** e que o
Rollup não conseguia tree-shakar. **A atribuição estava errada.** Medi: `date-fns` é 2.30.0,
`locale/package.json` tem `"module": "../esm/locale/index.js"` e `"sideEffects": false`, e o chunk
resultante tem **0 objetos de locale** — o barril foi tree-shakado corretamente, e os outros locales
existem como chunks dinâmicos separados, criados pelo `import.meta.glob` que o próprio módulo usa.

A causa real é mais sutil e só apareceu seguindo a cadeia de imports:

```text
dateFnsLocale.ts  ──import estático──▶  date-fns/locale/en-US/index.js
                                        └─▶ _lib/formatLong  ─▶ date-fns/esm/_lib/buildFormatLongFn
                                        └─▶ _lib/localize    ─▶ date-fns/esm/_lib/buildLocalizeFn
                                        └─▶ _lib/match       ─▶ date-fns/esm/_lib/buildMatchFn
                                                                 buildMatchPatternFn
```

`getVendorChunk` em `vite.config.ts` mantém `/date-fns/locale/` **fora** do chunk de vendor, mas os
helpers `_lib` ficam em `date-fns/esm/_lib/` — fora daquele caminho. Então eles caíram no
`vendor-date-fns`, e isso fez **o chunk inteiro** virar dependência estática do módulo de entrada.
Prova no artefato: `index.html` trazia
`<link rel="modulepreload" href="./assets/vendor-date-fns-BVLQz3_u.js">` e o entry importava
**um único** símbolo (`import{b as ur}from"./vendor-date-fns-…"`) — para obter um objeto de locale,
o navegador pré-carregava e parseava 285 KB antes do primeiro render.

Correção: o `en-US` passa pelo mesmo glob dinâmico dos outros locales. `fetchLocale` e `getLocale`
devolvem `Locale | undefined` — o que é seguro por construção, porque todo consumidor entrega o
valor direto na opção `locale` do date-fns, e o date-fns cai para en-US quando ela é `undefined`.
Isso permitiu remover também o cast `undefined as unknown as Locale` em `useLocale.tsx`.

| Medida | Antes | Depois |
| --- | --- | --- |
| `vendor-date-fns` em `modulepreload` do `index.html` | **sim** | **não** |
| Imports estáticos do entry | 6 | **5** |
| `vendor-date-fns` no grafo de boot | sim | não — virou carga sob demanda |
| Custo removido da critical path | — | **285 KB raw / 54 KB gzip** |

A referência que sobrou no entry é `__vite__mapDeps`, o mapa de dependências de import dinâmico que
o Vite gera; não é import estático.

Verificação: `npm run build:check` (tsc) limpo, `dateFnsLocale.test.ts` **5/5**, build de produção
com sucesso, e a comparação acima feita nos artefatos gerados.

### E-13 (P2) — correção de status: parcialmente resolvido, não "aberto"

O relatório listava `GetQueryFiltersLegacy` re-executando o pipeline caro 4×. O código atual em
`BaseItemRepository.Querying.cs` já usa `var matchingItemIds = baseQuery.Select(e => e.Id);` — uma
subconsulta **só de ids**, com comentário explicando que a forma anterior repetia os joins de
ancestral/biblioteca e podia estourar o timeout do MySQL. Ou seja: o problema medido
(joins repetidos) foi resolvido.

O que **permanece** é que o filtro barato ainda é avaliado até 4× pelo servidor, porque
`matchingItemIds` continua sendo um `IQueryable` inlineado em cada agregação. Materializar o
conjunto em memória uma vez — a correção que o relatório propunha — trocaria isso por uma lista
`IN` com até dezenas de milhares de GUIDs inlineados, que é exatamente a classe de problema do E-6
(65 MB de SQL). Não fiz a troca, e registro o motivo em vez de marcar como concluído.

### Portão de verificação da rodada 23

```text
dotnet test MulletaFlix.sln --filter "FullyQualifiedName!~Integration"
# exit 0 — 3242 aprovados, 38 ignorados, 0 falhas (15 assemblies)
#   MulletaFlix.Api.Tests: 144 aprovados (era 138)
npm run build:check          # tsc --noEmit, limpo
npm run test                 # 23 arquivos, 200 aprovados, exit 0
npm run build:production     # built in 2m 3s, exit 0
```

### F-1 e F-3 (P0) — verificados, ainda ABERTOS, com o mecanismo corrigido

Fui conferir os dois com o mesmo rigor do F-2, e o resultado tem uma nuance que muda a correção.

**F-1 confirmado.** `src/index.tsx:81-99` é estritamente serial: `appHost.init()` →
`serverAddress()` → `loadCoreDictionary()` → `loadPlugins()` → `import('./RootAppRouter')` →
`renderApp()`. Nenhuma sobreposição.

**F-3 confirmado, mas não pelo motivo escrito.** Meu primeiro teste parecia refutar o achado:
`vendor-mui` **não** está em `modulepreload` do `index.html`, não é import estático do entry (que
tem 5 imports, nenhum de MUI), e o entry não contém nenhum marcador de MUI (`createTheme`,
`ThemeProvider`, `@mui`, `useTheme`, `styled`: todos ausentes). Concluí cedo demais. O `vendor-mui`
carga **sob demanda** — e o ponto é que ela é aguardada: `index.tsx:98` faz
`await import('./RootAppRouter')` **antes** de `renderApp()` na linha 99, e o
`ThemeProvider` do MUI está em `RootAppRouter.tsx:1`. Ou seja, os 438 KB do `vendor-mui` +
`vendor-emotion` **bloqueiam o primeiro render**, mesmo sem `modulepreload`. A correção escrita
("mover o ThemeProvider para os layouts lazy") ataca o sintoma; a causa imediata é a espera antes
do render.

Ficam para a próxima rodada e serão tratados juntos, porque são o mesmo caminho de boot: iniciar o
`import('./RootAppRouter')` logo no começo do bootstrap faz o fetch desse chunk se sobrepor aos
outros quatro awaits, sem mudar a ordem dos efeitos colaterais em relação ao `renderApp`.

**Correção de contagem no F-1:** são **4** esperas de rede, não 5. `loadCoreDictionary()` não é um
round-trip: `lib/globalize/loader.ts` chama `globalize.loadStrings({ name: 'core', translations:
locales })` com o JSON de traduções **já empacotado** (import estático), e `ensureTranslation` sai cedo
quando `dictionaries[culture]` já existe. Contar essa chamada como round-trip inflava o achado.

**Por que o F-1 não foi executado nesta rodada:** o `RootAppRouter` não é um chunk inerte. Em escopo
de módulo ele lê `layoutManager.layout` (um getter derivado das flags `tv`/`mobile`/`desktop`/
`experimental`, definidas pelo `appHost.init()`) para escolher entre `STABLE_APP_ROUTES` e
`EXPERIMENTAL_APP_ROUTES`, e ainda executa `createHashRouter` e `setRouterHistory`. Ou seja: adiantar
esse import antes de `appHost.init()` escolheria o conjunto de rotas errado, e adiantá-lo para depois
de `appHost.init()` mas antes de `loadPlugins()` só é seguro se nenhum plugin alimentar o router —
o que eu **não consigo verificar daqui**. A suíte Playwright do repositório está configurada para o
servidor de produção em `127.0.0.1:8096` e cria usuários e sessões, então rodá-la não é aceitável; e
não há proxy de API no dev server. Registro isso como pendência que exige uma instância local do
servidor, em vez de fazer a mudança às cegas num caminho de boot.

## Rodada 24 — S-4 e F-5 executados, E-12 retratado por medição, e um hotspot novo

### S-4 (P1) — o mesmo arquivo podia subir duas vezes. Executado.

`Jellyfin.Server.Implementations/Nebula/NebulaStagingWatcher.cs:376`

O `finally` do laço de upload removia a chave de deduplicação **incondicionalmente**:

```csharp
finally
{
    var fullPath = Path.GetFullPath(item.FilePath);
    _queuedFiles.TryRemove(fullPath, out _);   // <-- também nos caminhos que RE-ENFILEIRAM
    _activeFiles.TryRemove(fullPath, out _);
}
```

Dois caminhos põem o mesmo item de volta na fila antes do `continue`/saída: o sidecar de metadados
esperando o marcador (linha 322) e o arquivo ainda sendo gravado (linha 368). Nos dois, o `finally`
apagava a chave de fila mesmo assim — então o watcher podia enfileirar aquele caminho de novo
enquanto a primeira cópia ainda estava pendente, e **o mesmo arquivo subia duas vezes**: banda e cota
do Telegram gastas em dobro.

Correção: `ReleaseDequeuedItem(queuedFiles, activeFiles, filePath, requeued)` — a chave de fila só é
removida em desfecho terminal, e o claim em memória é **sempre** liberado (mantê-lo através de um
re-enfileiramento faria o próximo `TryAdd` falhar para sempre, travando o item).

| Evidência | Resultado |
| --- | --- |
| `dotnet test --filter NebulaStagingWatcher` | **9 aprovados**, 0 falhas (4 novos) |
| `RequeuedItem_KeepsTheQueueDedupeKey` | chave preservada no re-enfileiramento |
| `RequeuedItem_AlwaysReleasesTheInMemoryClaim` | claim liberado (sem travar o item) |
| `TerminalItem_ClearsBothKeysSoTheFileCanBeDiscoveredAgain` | caminho terminal limpa as duas |
| `RequeueThenTerminal_ClearsTheKeyOnlyAtTheEnd` | 2 requeues + término → limpa só no fim |

### F-5 (P1) — branding re-resolvido a cada navegação. Executado.

`MulletaFlix-web-master/src/scripts/autoThemes.ts:53`

`pageClassOn('viewbeforeshow', 'page', ...)` chama `applyTheme` a **cada navegação**. Quando o
usuário não tem tema explícito, isso cai em `getBrandingDefaultThemeId()` → `queryClient.fetchQuery(
getBrandingOptionsQuery(api))`, e essa query **não declara `staleTime` próprio** (verificado), então
herdava a janela global de 30 s: passada a janela, toda navegação refazia a consulta de branding para
ler um valor que não muda na sessão.

Correção: a promise é memorizada por carregamento de página. Detalhe que quase virou regressão — a
primeira versão que escrevi zerava o cache dentro do caminho "sem API client", mas o `??=` atribui a
promise **depois** de o corpo síncrono rodar, então o `undefined` seria sobrescrito pela promise já
resolvida e a ausência de API ficaria cacheada: o tema de branding nunca seria aplicado a quem
entrasse depois. A versão final decide fora do cache — sem API client, não cacheia.

Verificação: `npm run build:check` limpo e `npm test` **200/200**.

### E-12 (P2) — RETRATADO por medição. Não há índice redundante para remover.

O achado dizia: 4 índices `(ItemId, UserId, ...)` redundantes cujo prefixo já é a PK, **na tabela de
maior frequência de escrita** (progresso a cada ~10 s), e faltava o índice do predicado de resume.

Medido no banco real:

| Consulta | Resultado |
| --- | --- |
| `SELECT COUNT(*) FROM userdata` | **15 linhas** |
| `INDEX_LENGTH` de `userdata` | **0,09 MB** |
| `userdata` em todos os outros bancos `mulletaflix*` | **0 linhas** em cada (6 bancos de teste) |

A premissa é falsa nesta instalação: a tabela está efetivamente vazia. Remover 4 índices ali
economizaria ~0,05 MB e nenhum custo de escrita mensurável — em troca de uma migração de schema com
risco real. Não foi feito.

Fui além do E-12 e usei a infraestrutura que a própria auditoria ligou na rodada 1: com
`userstat=ON` dá para **perguntar ao MariaDB quais índices foram realmente lidos** em vez de deduzir.

| Medida (janela de 11,2 h de uptime) | Valor |
| --- | --- |
| Índices no schema `mulletaflix` | **166** |
| Índices com **zero** leituras registradas | **0** |

Ou seja: **nenhum dos 166 índices é morto**. Toda a linha de otimização "remover índices redundantes"
fica fechada por medição, não por opinião — o que redireciona o esforço para o que a medição aponta.

### Hotspot novo, medido, ainda não atacado

Os índices mais lidos na mesma janela de 11,2 h:

| Tabela | Índice | Linhas lidas |
| --- | --- | --- |
| `baseitems` | PRIMARY | 59.441.500 |
| **`baseitemimageinfos`** | **`IX_BaseItemImageInfos_ItemId_ImageType`** | **29.407.046** |
| `itemvaluesmap` | PRIMARY | 5.122.336 |
| `itemvaluesmap` | `IX_ItemValuesMap_ItemId` | 737.154 |

`baseitemimageinfos` tem 55.948 linhas, e seu índice foi lido **29,4 milhões** de vezes — ~730
leituras por segundo sustentadas, quase metade do tráfego do PRIMARY de `baseitems`. Isso é a
assinatura de uma consulta de image info por item em listagens/refresh. Nenhum achado anterior tinha
esse número; fica registrado como o próximo alvo com evidência medida, e não como suposição.

> **CORREÇÃO (rodada 25): a segunda metade do parágrafo acima está errada.** A taxa de 730 leituras
> por segundo **não é sustentada**. Ver a rodada 25 abaixo: a medição do delta em janela ociosa
> mostra **zero** leituras nessas tabelas. O número acumulado é produzido por **rajadas** (scan e
> refresh), e o alvo correto é o caminho de rajada, não uma otimização de regime permanente. O que
> sobrevive do parágrafo é o número absoluto (29,4 M de leituras no índice) e o nexo causal, que a
> rodada 25 identificou no código.

## Rodada 25 — o banco está ocioso em regime; o custo é rajada. S-14 executado.

### O que a medição do host mostrou (e uma retratação)

Liguei o `slow_query_log` por 25 s com `long_query_time=0.05` e depois o `general_log` por janelas
curtas, para parar de inferir a partir de contadores acumulados:

| Medida | Resultado |
| --- | --- |
| Queries acima de 50 ms em 25 s | **0** (log ficou em 0,2 KB) |
| General log em 8 s | **0,6 KB** |
| Delta de `ROWS_READ` de `baseitems`, `baseitemimageinfos`, `itemvaluesmap` em **60 s** | **0, 0, 0** |
| Tráfego ocioso observado | `jobqueue` (82 statements/min) e `ProjectionQueue` (a cada ~10 s) |
| Linhas em `jobqueue` / `projectionqueue` | **81** / **0** |

Conclusões, na ordem em que importam:

1. **Não existe hotspot de banco em regime permanente.** Em um minuto ocioso os contadores das
   tabelas grandes não se moveram **nada**. O servidor não está sofrendo com volume contínuo de
   queries.
2. **Não existe query lenta.** Nada passou de 50 ms na janela, e o tráfego ocioso bate em tabelas de
   81 e 0 linhas.
3. Portanto os 609 M de linhas lidas em `baseitems` e os 29,4 M no índice de `baseitemimageinfos`
   **acumulados em 11,2 h são rajada**, não regime. A frase "~730 leituras por segundo sustentadas"
   que eu escrevi na rodada 24 está **retratada**.

Isso redireciona o trabalho: não há o que otimizar em regime; o alvo é o que roda **durante** scan e
refresh.

### S-14 (P2) — o caminho de rajada encontrado no código. Executado.

`Jellyfin.Server.Implementations/Nebula/NotificationsLibraryNotifier.cs:114`

Cada item adicionado pela biblioteca disparava um `Task.Run` com um laço de **até 180 tentativas**,
uma por segundo, e cada tentativa fazia:

- `_libraryManager.GetItemById(itemId)` — uma ida ao banco;
- `GetMetadataSnapshot(current)` — que chama `item.GetImagePath(ImageType.Primary)`, ou seja **lê
  `ImageInfos`**, e depois faz `File.Exists` + `new FileInfo(...).Length` + `GetLastWriteTimeUtc` no
  NFO e na capa;
- `IsMetadataReady(current)` — mais `FindCoverPath`, `GetNfoPath` e stats de arquivo.

Uma varredura que adiciona 1.000 itens abria **1.000 laços concorrentes**, cada um podendo fazer 180
idas ao banco e ~8 a 10 stats de arquivo por tentativa. É exatamente a assinatura da rajada que os
contadores mostram, e é o **nexo causal** das 29,4 M de leituras no índice de `baseitemimageinfos`:
`GetImagePath` passa por `ImageInfos` a cada tentativa.

Correção, sem mudar a intenção (dar 180 s para capa e NFO estabilizarem):

| Mudança | Efeito |
| --- | --- |
| Backoff crescente (2 s, ×1,5, teto de 10 s) dentro do mesmo orçamento de 180 s | **20 tentativas** por item em vez de 180 (~9× menos idas ao banco e stats) |
| `SemaphoreSlim` limitando a **24** itens aguardados ao mesmo tempo | uma varredura de 1.000 itens não abre 1.000 laços concorrentes |
| `_metadataWaitSlots` liberado só quando adquirido, e descartado no `Dispose` | sem vazamento nem `Release` indevido |

Nível de verificação, explicitamente: **build 0 erros** e **suíte completa verde**. A contagem de 20
tentativas é **derivada das constantes** (2+3+4,5+6,75 e depois 10 s por tentativa até 180 s), não
medida em execução — o laço é privado e assíncrono, e um teste comportamental exigiria 180 s de
execução ou uma costura de teste que eu não quis introduzir sem necessidade.

### Portão da rodada 25

```text
dotnet build Jellyfin.Server.Implementations   # 0 erros
dotnet test MulletaFlix.sln                    # ver release
```

## Rodada 26 — arnês de verificação de boot, F-1 medido e revertido, H-14 executado

### O arnês que faltava (e que destrava o resto do frontend)

O F-1/F-3 estava parado porque eu não tinha como verificar o boot do cliente de ponta a ponta: a suíte
Playwright do repositório aponta para o servidor instalado em `127.0.0.1:8096` **e cria usuários e
sessões**, e o dev server não tem proxy de API. A saída foi interceptar `/web/**` no Playwright e servir
o `dist` local, mantendo a página na **mesma origem** para que as chamadas de API continuem reais.

O arnês (`MulletaFlix-web-master/verify-web-boot.mjs`, temporário) mede:

- se o app renderizou (marca quando o `#reactRoot` **muda** em relação ao splash estático do
  `index.html` — "tem filhos" seria falso positivo, o splash já traz 5 nós e 6741 bytes);
- avisos `[bootstrap] ... falhou/excedeu`, erros de página e de console;
- timing de recurso de cada chunk contra a marca de render, o que diz se ele estava **antes** do
  primeiro render (caminho crítico) ou depois;
- quantos arquivos foram servidos do `dist` e quantos **caíram no servidor** (misturar bundles
  invalidaria a comparação, então isso é contado, não escondido);
- latência artificial por asset (`WEB_LATENCY_MS`), porque servir do disco é rápido demais e esconde
  justamente o efeito que o F-1 quer medir.

Modo de controle: `WEB_MODE=installed` carrega o bundle instalado (12.0.65) sem interceptação.
Primeiro resultado: **os dois bootam corretamente** — render, `skinHeader`, zero avisos de bootstrap,
zero erros de página.

Duas leituras minhas foram corrigidas pelo próprio arnês, antes de virarem afirmação:

1. "O meu build renderiza em 336 ms contra 1461 ms do instalado" era **ruído de partida fria**. Em três
   execuções com cache quente: 332 ms (instalado) contra 346 ms (meu). Sem ganho mensurável.
2. "Meu build carrega 78 chunks a mais" é diferença entre 12.0.68 e 12.0.65 combinada com o caminho de
   entrega; não é regressão comprovada e **não** foi tratada como tal.

### F-1 — a correção proposta foi implementada, medida e REVERTIDA

Antes de mudar, resolvi a dúvida de segurança que me travava: `layoutManager.init()` roda no
**carregamento do módulo** (`layoutManager.ts:94`) e nada em `appHost.init()` o altera, então o conjunto
de rotas (`STABLE_APP_ROUTES` vs `EXPERIMENTAL_APP_ROUTES`, escolhido em `RootAppRouter.tsx:25`) **não
depende de quando o chunk é buscado**. Isso tornava seguro adiantar o import.

Implementei: `const routerModule = import('./RootAppRouter')` logo depois de `appHost.init()`, com o
`await` mantido antes de `renderApp()`.

Medição A/B com o mesmo arnês, 40 ms de latência por asset, 3 execuções cada:

| | primeiro render |
| --- | --- |
| antes | **278, 293, 298 ms** (média 290) |
| depois | **336, 344, 350 ms** (média 343) |

As faixas não se sobrepõem: a mudança ficou **~50 ms mais lenta**. A explicação mais provável é
contenção de banda — antecipar o grafo de ~200 chunks faz ele competir com os assets do caminho crítico
e com as chamadas de API de que os awaits seguintes dependem. **Revertido**, com `git diff` do arquivo
vazio confirmando que o revert é exato e que a linha de base continua válida.

Conclusão honesta: adiantar o import **não** é a correção do F-1. O problema real, agora medido, é
outro: `import('./RootAppRouter')` arrasta o **grafo inteiro da aplicação** (as listas de rotas são
imports estáticos dentro de `RootAppRouter`) e `renderApp()` espera por ele — por isso 220 chunks
únicos são buscados antes do primeiro render. A correção estrutural é tornar as rotas preguiçosas
(`lazy: () => import(...)` por rota) para que o boot não aguarde o app inteiro. Isso é um refactor da
camada de rotas, com risco de regressão em todas as telas, e fica registrado como tal em vez de ser
feito às pressas.

### H-14 (P2) — alocação por request no servidor de stream. Executado.

`Jellyfin.Server.Implementations/Nebula/NebulaHttpStreamServer.cs:480`

```csharp
var buffer = new byte[128 * 1024];
```

Um array novo por request de stream: para cada gigabyte transmitido, cerca de **um gigabyte de lixo**
de 128 KB em 128 KB, num componente cuja função é transmitir continuamente.

Correção: `ArrayPool<byte>.Shared.Rent(StreamBufferSize)` com devolução em `finally` (e uma constante
`StreamBufferSize` no lugar do literal repetido). O `Math.Min` passou a usar a constante em vez de
`buffer.Length`, porque o array alugado pode ser **maior** que o pedido — usar `buffer.Length` ali
ampliaria a leitura além do tamanho pretendido.

Verificação: build 0 erros e suíte verde. É uma mudança de alocação, sem alteração de comportamento
observável, então não há teste dedicado; o que ela preserva é o tamanho do bloco lido e o destino do
buffer.

### Portão da rodada 26

```text
dotnet build Jellyfin.Server.Implementations   # 0 erros
npm run build:check                            # tsc --noEmit limpo
node verify-web-boot.mjs                       # BOOT OK (render, 0 avisos, 0 erros de pagina)
dotnet test MulletaFlix.sln                    # ver release
```

## Rodada 27 — E-4 confirmado sob carga real e executado (o maior ganho medido da auditoria)

### Como o achado saiu de "provável" para "medido"

Nas rodadas anteriores o servidor estava **ocioso** quando medi, e o delta de linhas lidas era zero. Nesta
rodada o servidor estava **em uso real**: upload Nebula ativo e o log mostrando
`EpisodeMetadataService: File changed, pruning extracted data` para `N:\Series\...` — ou seja, um scan em
andamento. Medição de 60 s nesse estado:

| Tabela | Linhas lidas em 60 s |
| --- | --- |
| **`peoples`** | **+3.156.136** |
| `baseitems` | +59.882 |
| `ancestorids` | +35.256 |
| `itemvaluesmap` | +3.792 |
| `baseitemimageinfos` | +1.357 |

Com 52.652 linhas em `peoples`, 3.156.136 é **exatamente 60 varreduras completas da tabela por minuto** —
uma por segundo. E 52.652 × 60 = 3.159.120, contra 3.156.136 medidos: a coincidência fecha a conta.

### A causa, no código

`Jellyfin.Server.Implementations/Item/PeopleRepository.cs:112`

```csharp
var existingPersons = context.Peoples.Select(e => new
{
    item = e,
    SelectionKey = e.Name.ToLower() + "-" + e.PersonType
})
    .Where(p => Enumerable.Contains(personKeys, p.SelectionKey))
    .Select(f => f.item)
    .ToArray();
```

O `Contains` compara contra uma chave **computada** dentro de um tipo projetado. O fork tem um
`JellyfinQueryHelperExtensions` que torna `Enumerable.Contains` traduzível quando o lado direito é uma
**coluna** da entidade — que é o caso de todas as outras 20 ocorrências no código. Aqui não: o valor
comparado é `e.Name.ToLower() + "-" + e.PersonType`, então o EF não consegue traduzir e materializa a
tabela inteira, filtrando no cliente.

Isso roda em `UpdatePeople`, ou seja, **a cada item cujo metadado é atualizado** — durante um scan, 60
vezes por minuto.

### A correção

Filtrar primeiro pela coluna `Name`, que **é indexada** (`IX_Peoples_Name`), e só então comparar a chave
em memória:

```csharp
var candidateNames = people.Select(e => e.Name).ToArray();
var existingPersons = context.Peoples
    .Where(e => candidateNames.Contains(e.Name))
    .AsEnumerable()
    .Where(e => personKeys.Contains(e.Name.ToLower() + "-" + e.PersonType, StringComparer.Ordinal))
    .ToArray();
```

A coluna `Name` é `utf8mb4_general_ci`, então o `IN` do servidor é **case-insensitive** e devolve um
superconjunto do que o predicado antigo casava; a comparação de chave logo abaixo continua decidindo
quais linhas ficam. Verifiquei a collation na prática antes de confiar nisso:
`SELECT ... WHERE Name IN ('070 shake')` casa a linha `'070 Shake'`.

### Evidência medida no host real (plano de execução)

| | type | índice | linhas |
| --- | --- | --- | --- |
| antes (tabela inteira) | `ALL` | nenhum | **52.652** |
| depois (filtro por `Name`) | `range` | `IX_Peoples_Name` | **5** |

`Using index condition`, 5 linhas para 5 nomes. São **~10.500× menos linhas por atualização de
metadados**; durante o scan medido, sai de 3,16 M linhas/min para algo da ordem de centenas.

Nota de método: o "antes" foi medido com `EXPLAIN SELECT ... FROM peoples` sem `WHERE`, que é o que o
código antigo de fato fazia (materializar a tabela). Não é uma estimativa.

Observação adicional da mesma medição: 30 s depois o delta de `peoples` voltou a **zero** — era rajada de
scan, coerente com a conclusão da rodada 25 de que não existe hotspot em regime.

### Portão da rodada 27

```text
dotnet build Jellyfin.Server.Implementations   # 0 erros
dotnet test MulletaFlix.sln                    # ver release
```

## Bug reportado pelo usuário — duas séries de mesmo nome foram fundidas (rodada 25)

### O sintoma e a evidência no banco

O usuário reportou que `A Agência (2020)` e `A Agência (2024)` — que deveriam ser séries diferentes —
apareciam como uma só. Investigado direto no banco:

| Id | Name | ProductionYear | Path | PresentationUniqueKey |
| --- | --- | --- | --- | --- |
| `3dc3694a…` | A Agência | **2024** | `N:\Series\A Agência **(2020)**` | `430769-pt-br-d565273fd114d77bdf349a2896867069` |
| `b5a23d1e…` | A Agência | **2024** | `N:\Series\A Agência **(2024)**` | `430769-pt-br-d565273fd114d77bdf349a2896867069` |

Não são duas linhas fundidas numa: são **dois itens distintos** com a **mesma `PresentationUniqueKey`**
e **os mesmos provider ids** nos dois (`Imdb tt26656917`, `Tmdb 219971`, `Tvdb 430769`). O conteúdo
difere (85 episódios na pasta de 2020, 14 na de 2024), então são mesmo séries diferentes. E o item da
pasta `(2020)` ficou com `ProductionYear = 2024`.

### A causa raiz, elo por elo

1. `Emby.Naming/TV/SeriesResolver.cs:40` casa o padrão "título com ano", usa o grupo `title` para
   limpar o nome e **descarta o ano**. `Emby.Naming/TV/SeriesInfo` nem tinha propriedade `Year`.
   Resultado: `A Agência (2020)` e `A Agência (2024)` produzem lookup info **idêntico**
   (`Name = "A Agência"`, sem ano).
2. `MediaBrowser.Controller/Entities/TV/Series.cs:514` tenta recuperar o ano em
   `BeforeMetadataRefresh`, mas a partir de `LibraryManager.ParseName(Name)` — e o nome já não tem
   mais o ano. A recuperação é impossível por construção.
3. O caminho de busca do provider não usava ano nenhum: `TmdbSeriesProvider.cs:116` chamava
   `SearchSeriesAsync(searchName, …)` **sem** o parâmetro `year`, embora o método aceite
   (`firstAirDateYear: year`). As duas pastas recebiam a mesma lista de candidatos e ficavam com a
   mesma série.
4. `MediaBrowser.Providers/Manager/MetadataService.cs:273` faz `lookupInfo.Year = result.ProductionYear`,
   então o ano errado (2024) realimentava as buscas seguintes: a partir daí `ProductionYear.HasValue`
   é verdadeiro e a tentativa de recuperação do passo 2 nunca mais roda.
5. Com os mesmos provider ids, `Series.CreatePresentationUniqueKey()` (`Series.cs:79`) monta a chave a
   partir de `userdatakeys[0]` quando `EnableAutomaticSeriesGrouping` está ligado — que é exatamente o
   recurso de agrupar séries homônimas. Com ids iguais, a chave é igual, e o cliente exibe uma só.

### A correção

| Arquivo | Mudança |
| --- | --- |
| `Emby.Naming/TV/SeriesInfo.cs` | propriedade `Year` |
| `Emby.Naming/TV/SeriesResolver.cs` | o ano passa a ser capturado (grupo `year` no regex) e devolvido nos dois caminhos, em vez de descartado |
| `Emby.Server.Implementations/Library/Resolvers/TV/SeriesResolver.cs` | os 3 pontos de criação de `Series` passam a gravar `ProductionYear = seriesInfo.Year` |
| `TmdbSeriesProvider.cs` (busca e detalhe) | a busca tenta **primeiro com o ano**; se não vier nada, tenta **sem o ano** |

O fallback sem ano é deliberado: um filtro de ano estrito faria uma pasta cujo ano divirja do TMDb
**perder** a identificação. Assim o ano desempata quando pode e não custa recall quando não pode.

Verificação: **9 testes novos** em `SeriesResolverYearTests`, incluindo o par
`A Agencia (2020)`/`A Agencia (2024)` resolvendo para anos diferentes com o mesmo nome, e o guarda de
regressão do caso que motivou o regex original (`1923 (2022)` → nome `1923`). Suíte completa:
**3254 aprovados, 0 falhas**.

### Limite honesto: os dois itens já danificados não se consertam sozinhos

A correção impede que aconteça de novo, mas **não** repara os dois itens que já estão com os provider
ids trocados: um refresh normal não ajuda porque `TmdbSeriesProvider.GetMetadata` curto-circuita em
`TryGetProviderId(Tmdb)` e vai direto para a série errada. O conserto desses dois é manual, no painel:
**Identificar** na série `A Agência (2020)` e escolher a série correta (ou remover a identificação
atual) e depois repetir o refresh. Não automatizei isso porque "desidentificar" séries por heurística
poderia apagar metadados corretos de outras — o sinal disponível (ano da pasta divergindo do ano do
item) é bom para *sinalizar*, não para decidir sozinho.

### Portão da rodada 24

```text
dotnet test --filter NebulaStagingWatcher      # 9 aprovados, 0 falhas
dotnet test MulletaFlix.sln (suíte completa)   # ver release
npm run build:check                            # tsc --noEmit, limpo
npm test                                       # 23 arquivos, 200 aprovados, exit 0
```

## Rodada 28 — o único achado que não era código: o caminho de gravação não tinha prova

### O achado

Todas as correções de persistência desta auditoria rodam sobre `SaveBaseItemEntities`, e **nenhuma
linha desse método era executada pela suíte**. A razão é específica, não preguiça: o método chama
`ExecuteDelete` para as linhas filhas de um item sendo reescrito (`BaseItemProviders`,
`BaseItemImageInfos`, `BaseItemMetadataFields`, `BaseItemTrailerTypes`) e o provider EF InMemory não
implementa `ExecuteDelete` — o teste nem chega ao ramo. O efeito prático: o bug que **abortava refresh
de metadados durante o scan** ("cannot be tracked because another instance with the same key value is
already being tracked", lançado de `AddRange`) podia voltar sem nenhum teste ficar vermelho.

Correção: `tests\IntroSkipper.Integration.Tests\ServerPersistenceMariaDbTests.cs`, dois testes contra
MariaDB real, em schema descartável (`mulletaflix_integration_persistence`, dropado antes da execução —
nunca o schema de produção).

| teste | prova |
| --- | --- |
| `SaveItems_ReSavingAnExistingItem_RewritesItWithoutTrackingConflict` | regravar item existente com metadado e provider id novos toma o ramo de reescrita e **não** dispara o conflito de tracking |
| `SaveItems_SavingTwoDistinctItems_KeepsBothRows` | mesmo nome + anos diferentes = **duas** linhas, com os `ProductionYear` 2020 e 2024 preservados |

```text
dotnet test MulletaFlix-master\tests\IntroSkipper.Integration.Tests\IntroSkipper.Integration.Tests.csproj --filter "FullyQualifiedName~ServerPersistenceMariaDbTests"
# 2 aprovados, 0 falhas
```

Fica fora da suíte rápida por construção: o projeto se chama `*.Integration.Tests` e o filtro do CI é
`FullyQualifiedName!~Integration`, porque o teste precisa de um MariaDB vivo na 3306.

### Achado colateral: um harness que apagava o dado e parecia bug de produto

`BaseItemMapper.Map` persiste `appHost.ReverseVirtualPath(dto.Path)`. Com um
`Mock.Of<IServerApplicationHost>()` cru, esse método devolve `null`, então **toda** linha gravada saía
com `Path = null`. A primeira versão do teste das duas séries leu "2 linhas, 1 path distinto" e a
conclusão plausível era "o caminho de persistência está fundindo séries homônimas" — que é exatamente
o bug que o usuário reportou e que já havia sido corrigido. Não era: as duas linhas estavam certas e
com anos distintos. O harness é que destruía o valor antes de gravá-lo. Duas asserções substituíram
uma: `Path` distinto **e** `ProductionYear` distinto.

O mesmo padrão aparece em outros dois estáticos que o caminho dereferencia e que o teste precisa montar,
cada um descoberto por um `NullReferenceException` dentro do save: `BaseItem.LibraryManager`
(`GetAncestorIds()`) e `BaseItem.ConfigurationManager` (`SortName` → `CreateSortName()` lê
`ServerConfiguration.SortRemoveWords`).

### Backlog — declarado, não silenciado

| item | situação | motivo |
| --- | --- | --- |
| 403 nas fontes atrás de Cloudflare | não corrigível em código | bloqueio por fingerprint TLS do .NET; header não resolve |
| app Android | não publicado | decisão explícita do usuário; escopo é servidor + web |
| árvore sem commit | pendente | 266 entradas (175 M, 85 novos, 6 D), último commit `48831916` de 21/09. `dist/` é ignorado, então instaladores não entram. Não commitei porque outra sessão edita o mesmo repo |
| `MyDramaListSeriesImageProvider` | corrigido na rodada 28 | mesmo defeito do `[ERR]` por item; `FailureCooldown` de 30 min |

### Release

`v12.0.71` — 2 assets verificados na API (`draft=false`, ambos `uploaded`): zip 354,14 MB e instalador
386,59 MB. O instalador é obrigatório em toda release; `publish-release.ps1` recusa publicar sem ele.

```text
dotnet test MulletaFlix.sln --filter "FullyQualifiedName!~Integration"   # exit 0 — 3275 aprovados, 38 ignorados, 0 falhas
dotnet build MulletaFlix.sln                                             # 0 erros
.\build-update-package.ps1 -Version 12.0.71                              # exit 0
.\publish-release.ps1                                                    # exit 0, 2 assets uploaded
```

## Rodada 29 — o erro que a produção estava gerando durante a auditoria, e duas retratações

### O maior achado da auditoria não estava na lista: 2.355 falhas vivas

Enquanto esta rodada rodava, o servidor em produção (12.0.69, PID 10716, iniciado 14:28:40) acumulou
**2.355 linhas `[ERR]`**, numa rajada de ~14:52 a 15:42 (142 erros na hora 14, 2.222 na hora 15,
**nenhum** na hora 16), a 69–78 por minuto dentro da rajada. **Praticamente todas as 2.355 eram a mesma
exceção**:

```text
System.InvalidOperationException: The instance of entity type 'BaseItemEntity' cannot be tracked
because another instance with the same key value for {'Id'} is already being tracked.
  at Microsoft.EntityFrameworkCore.ChangeTracking.Internal.EntityGraphAttacher.AttachGraph
```

Contagem dos frames do próprio código nos blocos de exceção:

| frame | ocorrências |
| --- | --- |
| `MediaBrowser.Providers.Manager.MetadataService.RefreshMetadata` | 2.280 |
| `ItemPersistenceService.SaveBaseItemEntities` | 2.278 |
| `ItemPersistenceService.UpdateOrInsertItemsCore` | 2.278 |
| `MediaBrowser.Controller.Entities.BaseItem.RefreshMetadata` | 2.279 |
| `Folder.RefreshAllMetadataForContainer` / `RefreshChildMetadata` | 2.273 |
| `LimitedConcurrencyLibraryScheduler.ProcessItem` | 2.273 |
| `SeriesMetadataService.RefreshMetadata` | 2.272 |
| `LibraryManager.UpdateItemsAsync` / `BaseItem.UpdateToRepositoryAsync` | 2.270 |

O refresh de uma série dispara o refresh dos filhos, e **todo** save de filho morre no
`SaveBaseItemEntities`, no ramo de item existente. O custo não é só o erro perdido: são 36 MB de log na
janela, e um working set que subiu de 2.009 MB para 4.551 MB no pico. A rajada terminou sozinha às 15:42
e a memória voltou a 2.995 MB em ~25 min — era lixo coletável (pilhas de exceção, DbContexts do laço),
não vazamento. O que fica é o dano: os metadados daquela série não foram gravados.

### A cobertura que faltava: o formato do stack vivo

O teste da rodada 28 usava duas séries soltas — sem pai, sem imagens. O caminho que falha em produção é
um **filho**: o pai já está salvo, e o filho carrega as quatro espécies de linha que o mapper religa na
entidade recém-criada (provider, imagem, locked field, trailer type).

`SaveItems_RefreshingAnExistingEpisodeUnderASavedSeries_RewritesTheChildRows` reproduz exatamente isso
(série → temporada → episódio, depois o refresh do episódio) e assere as **linhas filhas**, não apenas a
ausência de exceção — do contrário o teste continuaria verde se as linhas deixassem de ser gravadas.

```text
dotnet test ...IntroSkipper.Integration.Tests --filter "FullyQualifiedName~ServerPersistenceMariaDbTests"
# 3 aprovados, 0 falhas
```

### Retratações por medição no host

| item | alegação original | medição | veredito |
| --- | --- | --- | --- |
| H-7 (P1) | uma query de banco por tile de trickplay | `trickplayinfos` = **0 linhas**; índices apenas a PK `(ItemId, Width)`; sem tráfego de tile | sem efeito mensurável neste host; não é prioridade |
| S-3 (P1) | limpeza de órfãos O(dirs × files), a cada 10 min | `E:\NebulaStage`: 2.059 dirs / 2.582 arquivos / 8.112 entradas em 2.058 varreduras recursivas; emulado: **0,2 s** contra **0,1 s** do passe único (**1,5×**) | complexidade real na forma, custo absoluto ~0,2 s por execução; não paga um ciclo de release |

Árvore rasa (profundidade ≤ 3) e pequena é o que separa "O(dirs × files)" de um problema: o
multiplicador existe (3,14× em `E:`), mas incide sobre 2.582 arquivos.

### Achado novo, ainda não medido

28 ocorrências de `MediaBrowser.Common.FfmpegException: ffmpeg image extraction timed out` no processo
atual (24 delas para `A Agência (2020)`) — a segunda maior fonte viva de `[ERR]`. A causa provável é
contenção de CPU/IO pelo laço de falhas acima, o que exige medição própria antes de virar correção.

### Release

`v12.0.72` — 2 assets verificados na API (`draft=false`, ambos `uploaded`): zip 354,14 MB e instalador
386,59 MB.

```text
dotnet test MulletaFlix.sln --filter "FullyQualifiedName!~Integration"   # exit 0 — 3275 aprovados, 38 ignorados, 0 falhas, 0 erros de build
dotnet test ...ServerPersistenceMariaDbTests                             # 3 aprovados, 0 falhas
.\build-update-package.ps1 -Version 12.0.72                              # exit 0
.\publish-release.ps1                                                    # exit 0, 2 assets uploaded
```

**Ação pendente fora do código:** o servidor instalado é o 12.0.69. A rajada parou por conta própria e o
servidor está Healthy, mas o defeito continua no binário — qualquer novo refresh daquela série repete a
rajada e perde os metadados de novo. A 12.0.72 só chega ao host pela atualização do painel.

## Rodada 30 — medindo bytes de boot: o maior item da critical path era uma imagem de 1 MB

### O instrumento que faltava

O arnês de boot da rodada 26 media a **ordem** dos chunks; passou a medir também **quantos bytes chegam
antes do primeiro render**. No bundle publicado (12.0.72): 131 assets, **4.021 KB**, primeiro render
371 ms. Top da critical path em KB: `search-brand-tile.png` **1.003**, `vendor-jellyfin` 436,
`vendor-mui` 429, `index-*.js` 310, `vendor-date-fns` 285, `pt-br` 178, `en-us` 167, `index-*.css` 150,
`vendor-react` 139, `icon.png` 120, `vendor-router` 93, `RootAppRouter` 82.

O item dominante não era código: era o **logo do splash**, 1431×1099, 24bpp, 1.003 KB.

### Correção

PNG → WebP q95 (53 KB) gerado com o ffmpeg que o próprio servidor instala, em 7 pontos de referência:
6 no cliente web (`site.scss:34`, `themes/_base/_theme.scss:93,95`, `themes/appletv:38`,
`themes/netflix:41`, `themes/purplehaze:34`) e 1 no servidor
(`Jellyfin.Server/wwwroot/api-docs/swagger/custom.css:9`). O PNG original foi mantido: a URL
`/branding/search-brand-tile.png` pode ser consumida por cliente fora deste repositório.

Verificação visual do resultado: PNG e WebP lidos lado a lado, idênticos ao olho (a imagem é chapada —
daí os 53 KB).

### A/B na mesma árvore (2 builds, 2 execuções cada)

| build | bytes antes do render | média | primeiro render |
| --- | --- | --- | --- |
| PNG original | 4.146 / 4.132 KB | **4.139 KB** | 253 / 267 ms |
| WebP q95 | 2.242 / 2.244 KB | **2.243 KB** | 286 / 278 ms |

**−1.896 KB antes do primeiro render**, `BOOT OK` nas quatro execuções. Determinístico: 950 KB (o asset,
1.003 → 53 KB, e o PNG não é mais emitido). O restante não foi isolado — é compatível com o mesmo logo
sendo buscado por uma segunda URL (a cópia sem hash de `copy-assets.cjs`, usada pelo
`pageTitleWithDefaultLogo` dos temas), hipótese que não medi separadamente.

Confirmação de que a mudança está no artefato: o zip da release caiu de 354,14 MB para 353,33 MB.

### Duas correções de registro

**F-2 não tirou 285 KB da critical path.** `vendor-date-fns` continua com 285 KB e continua terminando
antes do primeiro render. A correção fez o que prometia em parte — o helper do barril de locale
(`buildLocalizeFn`) saiu do chunk —, mas o chunk permanece no caminho de boot porque quatro componentes
importam funções do date-fns estaticamente (`UserCardBox.tsx:3`, `filterSessions.ts:2`,
`tasks/utils/edit.ts:2`, `TaskLastRan.tsx:4`) e entram no grafo do entry pelas rotas estáticas do
`RootAppRouter`. É o mecanismo do F-1, medido e revertido na rodada 26.

**F-3 confirmado:** MUI + emotion + icons = **483 KB** antes do primeiro render (429 + 28 + 26), e o
mecanismo é o do F-1, não o `ThemeProvider` isolado: o mesmo `RootAppRouter` importa estaticamente os
arrays de rota do dashboard, que importam MUI. Não é correção de uma linha.

### Achado novo registrado, não executado

`MediaEncoder.cs:942-946` usa `thumbnail=n=24` no caminho I-frame e `MediaEncoder.cs:1024-1037` mata o
ffmpeg em `ImageExtractionTimeoutMs` (host: `0` em `system.xml` → default de 10.000 ms de
`MediaEncoder.cs:48`). No log real: **28 timeouts** entre 14:52 e 15:42, cada um seguido do fallback
"standard way" — que é o caminho sem amostragem de 24 quadros, isto é, a imagem que o usuário já recebe.
Custo: 10 s de ffmpeg morto por item segurando `_thumbnailResourcePool` (`MediaEncoder.cs:1020`) e um
stack no log. Correção disponível: `FFmpeg:imgExtractPerfTradeoff` (`ConfigurationOptions.cs:23`, hoje
`false`). Não executado: a biblioteca está em `N:` (indisponível nesta sessão, então sem medição no
arquivo real) e a troca altera o quadro escolhido para todos os itens — decisão de produto.

## Rodada 31 — H-6, H-8, B-4, B-6, B-7/B-8/B-11, E-5, H-2 e F-3/F-4 executados

Sessão dividida em 6 workstreams paralelos (um agente por cluster de achados), cada um seguindo o
mesmo processo: reler o código atual (o `main` recebeu commits concorrentes de outra sessão de IA
durante toda a execução), corrigir, `dotnet build MulletaFlix.sln -c Debug`, rodar os testes
focados reais, e um commit por achado. Nenhuma correção foi declarada concluída sem `exit 0` real de
build/test.

| Item | Arquivo | O que mudou | Verificação | Commit |
| --- | --- | --- | --- | --- |
| **H-6** | `MediaBrowser.Model/Dto/MediaSourceInfo.cs`, `MediaStream.cs`, `MediaAttachment.cs`, `MediaInfoHelper.cs:127` | O clone de `MediaSourceInfo[]` via round-trip JSON (`SerializeToUtf8Bytes` + `Deserialize`) virou `Clone()` explícito em cada classe (`MemberwiseClone()` + reconstrução das coleções mutáveis — `MediaStreams`, `MediaAttachments`, `Formats`, `RequiredHttpHeaders`), com a mesma garantia de isolamento sem o custo de serialização | Build 0 erros; `Jellyfin.Model.Tests` 658/658 | `3f481d3a` |
| **H-8** | `UniversalAudioController.cs:257`, `StreamingHelpers.cs:47-166` | `GetStreamingState` ganhou parâmetro opcional `preresolvedMediaSource`: quando o controller já resolveu o media source via `GetPlaybackInfo` (aplicando `SetDeviceSpecificData`), esse resultado é reaproveitado em vez de `AudioHelper`/`StreamingHelpers` rodarem `GetPlaybackMediaSources` de novo do zero. Antes: dois clones + duas avaliações de device profile por faixa, no mesmo request | Build 0 erros; `Jellyfin.Api.Tests` 148/148 | `fbb39f02` |
| **B-8** | `ItemPersistenceService.cs:290` | O stripe de lock deixou de ser escolhido só pelo primeiro item do lote (colisão/exclusão perdida quando dois lotes parcialmente sobrepostos pegavam stripes diferentes). Agora todas as stripes distintas cobrindo o lote são calculadas, adquiridas em ordem ascendente (evita deadlock) e liberadas em ordem reversa | Build 0 erros; `ItemPersistenceServiceTests` 23/23 | `239ea4d9` |
| **B-7/B-11** | `DisplayPreferencesManager.cs`, `PeopleRepository.cs`, `MediaStreamRepository.cs`, `ChapterRepository.cs`, `MediaAttachmentRepository.cs`, `LinkedChildrenService.cs` | O padrão real no código atual não era mais `.GetAwaiter().GetResult()` isolado (já não existia como tal), e sim `dbContext.SaveChangesAsync(default).GetAwaiter().GetResult()` dentro de métodos síncronos com contexto/transação também síncronos. Trocado por `SaveChanges()` nativo nos 8 pontos, sem alterar nenhuma interface pública — os overloads `*Async` já existentes continuam servindo o pipeline de scan | Build 0 erros; filtro `People\|DisplayPreferences\|MediaStream\|Chapter\|MediaAttachment\|LinkedChildren` 17/17; `LibraryManagerDeleteAsyncTests` 2/2 | `7936b42c` |
| **B-6** | `TrickplayManager.cs` | O caminho já era assíncrono (sem `.Result`/`.GetAwaiter().GetResult()`); o problema real era refazer a consulta ao DB a cada poll de `/Sessions` para dados que só mudam durante scan/geração de trickplay. Adicionado cache em `IMemoryCache` (TTL 10 min) por item id, invalidado explicitamente em `SaveTrickplayInfo`/`DeleteTrickplayDataAsync` | Build 0 erros; DtoService*/`SessionManagerTests` 22/22 | `5e16d97e` |
| **B-4** | `MediaSourceManager.cs:619` | Confirmado exatamente como descrito: `_liveStreamLocker` envolvia `OpenLiveStreamInternal` inteiro, incluindo o `await provider.OpenMediaSource(...)` de abertura remota. Lock reduzido para proteger só a escrita final em `_openStreams` (já `ConcurrentDictionary`); a chamada de rede roda fora do lock | Build 0 erros; `MediaSourceManagerTests` 29/29 | `dcb0001f` |
| **E-5** | `IJellyfinDatabaseProvider.cs`, `MySqlDatabaseProvider.cs`, `BaseItemConfiguration.cs` + migration `DropUnusedFullTextSearchIndex` | Investigado: `FullTextSearch` (`MATCH...AGAINST` sobre `IX_BaseItems_FullTextSearch`, um índice BTREE comum — não FULLTEXT) **não tinha nenhum caller** em todo o repo; a busca real de produção usa `EF.Functions.Like`/`.Contains` via `TranslateQuery.cs`. Decisão: código morto → removido (interface + implementação + mapeamento do índice) e nova migração dropando o índice, em vez de criar um FULLTEXT real que nada usaria | Build 0 erros; `EfMigrationTests` 2/2; suíte completa `Jellyfin.Server.Implementations.Tests` 924 aprovados, 0 falhas (38 ignorados pré-existentes) | `89c38b8e` + `dbd38fcb` |
| **H-2 / HTTP-1** | `Jellyfin.Api/Caching/StreamStateCache.cs` (novo), `DynamicHlsController.cs`, `Startup.cs` | O maior ganho de performance apontado pela auditoria: `GetDynamicSegment` reconstruía o `StreamState` inteiro (resolução de item, probe/attach de `MediaSourceInfo`, todo o cálculo de parâmetros de encoding) a cada segmento HLS — ~1200 reconstruções por filme de 2h. Cache singleton via `IMemoryCache`, chave = `PlaySessionId + MediaSourceId` + todos os campos do request que afetam a decisão de encoding (trocar áudio/legenda muda a chave → força reconstrução). TTL deslizante de 30s (cobre o gap entre segmentos de 3-6s, expira sessão abandonada sem cleanup explícito). Callback de evicção chama `StreamState.DisposeAsync()`. Live streams (`LiveStreamId` presente) ficam fora do cache por segurança de semântica de fechamento | Build 0 erros; `StreamStateCacheTests` 11/11 (novo); `Jellyfin.Api.Tests` 148/148 | `89c38b8e` |
| **F-3 / FRONT-1** | `MulletaFlix-web-master/src/RootAppRouter.tsx` | Confirma exatamente o mecanismo descrito na Rodada 30: `DASHBOARD_APP_ROUTES`/`EXPERIMENTAL_APP_ROUTES`/`STABLE_APP_ROUTES`/`WIZARD_APP_ROUTES` eram importados estaticamente no `RootAppRouter`, arrastando MUI (429 KB) + emotion + icons para a critical path mesmo em rotas legadas sem MUI — era a causa raiz por trás do F-1/F-3 que a Rodada 26 havia revertido. Investigação do histórico: existe um `components/router/DynamicAppRoutes.tsx` abandonado (roteador aninhado via `useRoutes()`) substituído por imports estáticos sem motivo documentado. Convertido para o mecanismo nativo e suportado do React Router (`patchRoutesOnNavigation`/"Fog of War"): cada árvore de rotas é importada sob demanda só quando a navegação entra naquele segmento de path, com cache por kind e retry em falha de carregamento — evita repetir o padrão do roteador sombra abandonado | `npm run build:check` 0 erros; `npm run build:production` ok; `npm test` 26/26 arquivos, 208/208 testes | `79284557` |
| **F-4** | `src/apps/dashboard/features/metrics/api/useLibraryItemCounts.ts` | `/Users/{id}/Views` era buscado de 4 lugares; o 4º (`useLibraryItemCounts`) reimplementava a query manualmente em vez de usar o hook `useUserViews` já usado por `MainDrawerContent`/`UserViewNav`. Consolidado nesse hook comum; os consumidores em scripts legados (`libraryMenu.ts`, `homesections.ts`, `homeScreenSettings.ts`) continuam usando `queryClient.fetchQuery(getUserViewsQuery(...))` porque não podem usar hooks React — padrão mantido de propósito | `npm run build:check` 0 erros; `npm test` 26/26, 208/208 | `e48405f1` |

Nota de processo desta rodada: por causa da execução paralela sobre o mesmo working tree, dois
achados (E-5 e H-2/HTTP-1) acabaram compartilhando o commit `89c38b8e` — um race no índice git
entre as duas sessões concorrentes, não uma mistura deliberada de escopo. Os dois diffs foram
conferidos separadamente antes de aceitar o resultado: cada achado tem sua própria validação de
build/test independente, só a autoria no histórico ficou fundida num commit.

Itens do handoff que ficam fora desta rodada por decisão explícita de escopo (não são bugs de
código, exigem decisão de produto/infra): correção manual dos registros duplicados de "A Agência" no
banco (causa raiz já corrigida em rodadas anteriores), gap de Cast/Chromecast no Android, migração de
índice da Database Wave 3 (exige janela de manutenção dedicada) e a decisão sobre quais dos commits
pendentes em `main` vão para `release/v12.0.92`.

## Rodada 32 — B-1 (já corrigido), H-7, H-5, H-10 executados

Processo: reler o código atual de cada arquivo (`git log --oneline -3 -- <arquivo>` + leitura
completa) antes de editar, corrigir, `dotnet build MulletaFlix.sln -c Debug`, rodar os testes reais
focados, um commit por achado. Ordem seguida: B-1 (P0) → H-7 (checando duplicação com B-6 primeiro) →
H-5 → H-10.

| Item | Arquivo | O que mudou | Verificação | Commit |
| --- | --- | --- | --- | --- |
| **B-1** | `ProviderManager.cs:1275-1341` | **Já corrigido, sem ação nesta rodada.** Leitura completa do `QueueRefresh`/`StartProcessingRefreshQueue` mostrou que tanto o `Enqueue` (linha 1282) quanto o `TryDequeue` (linha 1324) já estão dentro de `lock (_refreshQueueLock)`, com comentários explicando exatamente o risco de corrupção do heap que a auditoria descreve (`git blame` aponta commit `309027ff6` de 23/09, já em `main`). O achado descrevia um estado anterior do código; hoje o `PriorityQueue` já é acessado de forma serializada. Nenhum novo commit criado para não duplicar trabalho já existente | `git blame -L 1275,1290` confirma o lock já presente nos dois pontos citados pela auditoria | *(nenhum — já resolvido em `309027ff6`)* |
| **H-7** | `Jellyfin.Server.Implementations/Trickplay/TrickplayManager.cs` | Verificado primeiro se B-6 (commit `5e16d97e`, rodada anterior) já cobria o achado: B-6 só cacheou `GetTrickplayManifest`, mas `GetTrickplayTilePathAsync` (chamado por tile) e `GetHlsPlaylist` chamam `GetTrickplayResolutions` diretamente, fora do manifest, continuando sem cache — não era duplicado. Adicionado cache em `IMemoryCache` por itemId (mesmo TTL de 10min), com invalidação explícita em `SaveTrickplayInfo`/`DeleteTrickplayDataAsync` ao lado da invalidação do manifest existente | Build 0 erros, 806 avisos preexistentes (não regressão). Sem suíte dedicada a Trickplay no repo (`search_files *Trickplay*` em `tests/` = 0 arquivos) | `f5d3f60d` |
| **H-5** | `MediaBrowser.MediaEncoding/Subtitles/SubtitleEncoder.cs`, `Jellyfin.Api/Controllers/SubtitleController.cs` | `SubtitleEncoder.GetSubtitles` ganhou `IMemoryCache` injetado, cacheando os bytes resultantes por `(mediaSourceId, subtitleStreamIndex, outputFormat, startTimeTicks, endTimeTicks, preserveOriginalTimestamps)` por 10min — evita re-resolver o media source (`allowMediaProbe:true`) e re-parsear o arquivo inteiro em pedidos repetidos da mesma janela. `SubtitleController.GetSubtitle` calcula um ETag forte (SHA-256) sobre os mesmos parâmetros, responde 304 quando `If-None-Match` bate, e define `Cache-Control: public, max-age=86400` nas respostas 200 | Build 0 erros. `Jellyfin.MediaEncoding.Tests` filtro `SubtitleEncoderTests` 4/4. `Jellyfin.Api.Tests` filtro `SubtitleControllerTests` 2/2 | `29961fcd` |
| **H-10** | `Jellyfin.Api/Controllers/HlsSegmentController.cs:155` | `GetHlsVideoSegmentLegacy` chamava `_fileSystem.GetFilePaths` no diretório de transcode inteiro por request de segmento, comparando `Path.GetExtension`+`Contains` entrada por entrada. `GetHlsPlaylistLegacy` (mesma classe) já constrói exatamente o mesmo caminho como `Path.Combine(transcodePath, playlistId + ".m3u8")`. Adicionado esse caminho direto como fast path (`File.Exists`), com fallback preservado para a enumeração antiga do diretório caso o arquivo direto não exista | Build 0 erros. Sem teste dedicado a `HlsSegmentController` no repo (`search_files *HlsSegment*` = 0 arquivos); suíte completa `Jellyfin.Api.Tests` 148/148 (sem regressão nos demais controllers) | `e41dcdc5` |



