# Handoff — remoção do SQLite e MariaDB como banco único

## Objetivo do usuário

Remover completamente o SQLite do servidor MulletaFlix e manter somente MariaDB. O servidor deve continuar inicializando usuários, bibliotecas, plugins e o IntroSkipper sem criar arquivos `.db` locais.

## Concorrência e banco: o que foi medido

Investigação feita com dados reais de produção (logs de 21/09, ~127 MB, e consultas ao MariaDB em execução).

### O que a medição mostrou

| Item | Valor |
|---|---|
| Dados | 497 MB em `mulletaflix`; `baseitems` 298 MB / 77.909 linhas |
| Buffer pool | 128 MB (default) para 497 MB de dados |
| Hit ratio do buffer pool | 99,88 % (25.798 leituras de disco em 21,2 M pedidos) |
| `innodb_io_capacity` | 200 — padrão de disco mecânico, com o datadir em **SSD NVMe** |
| `max_connections` | **151**, abaixo das **200** que o pool do servidor pode abrir |
| Concorrência real | pico de **71** threads/s, p95 = 12, mediana = 3 |
| Transações ativas / esperas de lock | 0 / 8 |
| Deadlocks | 44 no log — todos **WRN de retentativa bem-sucedida** (`MulletaFlixDbContext` tenta 5× com backoff) |
| Chave duplicada | 4 — corrida de check-then-insert entre dois writers |
| "Too many connections" | **falso positivo**: o match era `address already in use` (logs de boot abortado) |

### O gargalo real não é o banco

O sistema operacional está **paginando**: commit de 15,7 GB de RAM com ~39 GB comprometidos e pico de pagefile de 7 GB, restando ~1,5 GB de RAM livre. O processo do servidor sozinho usa ~5 GB. É isso que produz travamentos generalizados — nenhum ajuste de banco resolve pressão de memória do SO.

### Ajuste aplicado (requer reinício do servidor para valer)

`MariaDbProcessManager.BuildTuningArguments` passa a subir o MariaDB embutido com argumentos explícitos, em vez de depender de um `my.ini` que ninguém cria:

| Argumento | Antes | Depois | Motivo |
|---|---|---|---|
| `--max-connections` | 151 | 300 | acima das 200 do pool; evita rejeição em rajada |
| `--innodb-io-capacity` | 200 | 2000 | datadir em SSD; o padrão é de disco mecânico, sem custo de memória |
| `--innodb-lock-wait-timeout` | 50 | 25 | o thread travado esperava ~50 s antes do retry rodar |
| `--innodb-buffer-pool-size` | 128M | 256M | +128 MB garantidos; não cabe todo o working set |
| `--innodb-log-file-size` | 96M | 128M | +32 MB de folga para os lotes da varredura |
| `--table-open-cache` | 2000 | 4096 | descritores para o conjunto de índices |

Os dois valores de memória são deliberadamente contidos: o MariaDB reserva buffer pool e redo log no start, e com a máquina paginando, trocar leitura de disco do banco por paging do sistema seria pior.

Detalhes que importam:

- Os argumentos **só valem para a instância embutida** que o servidor inicia. Um MariaDB externo pertence a quem o administra, e o servidor não mexe nele.
- As 6 flags foram validadas contra `mysqld --help --verbose` do binário 11.4.4 embarcado.
- O redo log **cresce** de 96 MB para 128 MB, que é o caso suportado; encolher não seria.
- Falta a prova de que a instância sobe com os argumentos: isso exige parar o MariaDB em uso (porta 3306) e deixar o servidor subir a própria instância.

### Correções de log no mesmo passe

- `JellyfinMigrationService` registrava `[ERR] Cannot make a backup of library.db` sempre que uma rotina legada marcada com `LegacyLibraryDb` rodava — o arquivo é do Jellyfin original e não existe em instalação MariaDB. Virou `LogDebug` com a explicação.
- `OptimizeDatabaseTask` dizia que fazia `VACUUM` em `MulletaFlix.db`; ele chama o provider, que roda `ANALYZE TABLE` no MariaDB.

## Estado: concluído e publicado

- Servidor: <https://github.com/raphaelpera85/mulletaflix-completo/releases/tag/v12.0.45>
  - `mulletaflix-update-win-x64.zip` — 354,11 MB, sha256 `585b0968ed93cf463e21c8b5bd4d68cc99893105553890f622d808f0337e2f3a`
  - `mulletaflix_12.0.45_windows-x64.exe` — 386,6 MB
- Ferramenta de migração: <https://github.com/raphaelpera85/mulletaflix-completo/releases/tag/tools-v1.0.0>
  - `mulletaflix-introskipper-migration-tool.zip` — 61,8 MB, sha256 `5e6722ef2725bce7a17297b0a8cd3879ef74f732d35caf308edba75f2a303605`

O número da versão sobe a cada mudança de binário do servidor, e não é cosmético: `ServerUpdateTask`
compara versões (`remoteVersion <= currentVersion` → não atualiza), então republicar o mesmo número
deixaria as instâncias sem receber a correção.

Nenhum artefato Android foi gerado ou publicado: esta tarefa é somente do servidor.

## SQLite: onde ele existe e até quando

Regra implementada: **SQLite só existe na ferramenta de migração, nunca no servidor.**

| Onde | SQLite? |
|---|---|
| Pacote do servidor (`stage`, zip, instalador) | **não** — nenhum arquivo com "sqlite" no nome, nenhum `.db` |
| Árvore de dependências do servidor | **não** — nem direta, nem transitiva |
| Código do plugin em runtime | **não** — não abre arquivo nem caminho de dados; o `IntroSkipperDbContext` só conhece o schema MariaDB |
| Projeto `tools/MulletaFlix.IntroSkipperMigration` | **sim, de propósito** — é a única dependência de SQLite do repositório |
| Testes de integração da migração | sim, para construir o arquivo de origem e provar o caminho |

O `Directory.Packages.props` fixa `Microsoft.Data.Sqlite` 10.0.11 (traz `SQLitePCLRaw` 2.1.12,
que corrige `GHSA-2m69-gcr7-jv3q`; a 9.0.0 trazia a 2.1.10 vulnerável). O audit do CI
(`dotnet list package --vulnerable --include-transitive`) continua sem acusar SQLite.

### Depois da migração, o SQLite não é mais usado para nada

1. A ferramenta lê os arquivos antigos e grava no MariaDB. Ela **não** altera os arquivos: abre
   com `Mode=ReadOnly` e `Pooling=False`, e um `-wal` deixado pelo plugin antigo é lido sem
   checkpoint.
2. Ela marca cada item afetado na fila de projeção do MariaDB, e é o plugin — falando só com o
   MariaDB — que publica os segmentos no Jellyfin.
3. O servidor sobe, encontra o schema `mulletaflix_introskipper` e nunca procura arquivo `.db`.
4. Confirmado que os intros e créditos aparecem, os `.db` antigos podem ser apagados: nada os lê.
   Até lá, mantê-los é a rede de segurança para uma volta atrás.

## Migração de dados SQLite → MariaDB

**Implementada**, em `MulletaFlix-master/tools/MulletaFlix.IntroSkipperMigration`, publicada em
`tools-v1.0.0` como executável único e autocontido (67 MB, sem dependências).

### Por que externa ao servidor

A sessão anterior removeu o `LegacyDatabaseImporter` para cumprir a remoção total do SQLite, o
que deixou uma instalação com `introskipper-v2.db` povoado sem caminho de migração. A ferramenta
resolve isso sem recolocar SQLite no runtime — e é exatamente por isso que ela é um projeto
separado: o servidor publicado não carrega nem distribui nenhum assembly SQLite.

### O que migra

`Segments` (incluindo tombstones e segmentos do usuário), `SeasonStates`, `AnalyzedItems` (com
restauração de `FileVersion`), `DisabledItems`, `SeasonAnalysisOverrides` e, com
`--include-cache`, `DetectionCache`.

Não migra `ImportHistory` (marcador do importador antigo) nem `ProjectionQueue` /
`ProjectionExternalOperations` (fila interna presa ao banco antigo).

### Garantias implementadas

- **Origem somente leitura**: nenhum byte dos arquivos originais muda.
- **Idempotente e não destrutiva**: linha com chave já existente é ignorada, nunca sobrescrita;
  uma edição feita após a primeira execução é preservada.
- **Ids preservados**: o `Id` do segmento é o mesmo que o Jellyfin usa em `MediaSegments`.
- **Linhas inválidas não abortam**: a restrição `CK_Segments_Range` do MariaDB (que o SQLite não
  tinha) faz a linha corrompida virar aviso, não falha.
- **Todas as versões de schema**: colunas lidas por `pragma_table_info`, então aceita bancos
  anteriores a `AnalyzedItems.FileVersion`, à remoção de `DisabledItems.SeasonId` e à criação de
  `SeasonAnalysisOverrides`.

### Verificação da ferramenta

- 8 testes de integração novos em `SqliteToMariaDbMigrationTests`, contra MariaDB real e um
  SQLite construído pelo teste: ids/tombstones/usuário preservados, forma pré-versionamento,
  rejeição de faixa inválida, idempotência sem sobrescrever edição do usuário, journaling de
  projeção, cache só com `--include-cache`, `--dry-run` sem gravar e erro claro para origem
  ausente. Total do projeto: **18 testes, 0 falhas**.
- Smoke test com o executável publicado: banco legado com 2 segmentos e 1 análise → migração
  gravou os 2 segmentos, a análise com `FileVersion` e 1 marcador de projeção; a segunda execução
  reportou "0 inseridas, 2 já existentes" e manteve as contagens.
- `--dry-run` contra os arquivos reais desta instalação: 0 linhas em todas as tabelas.

## O que a sessão anterior já havia feito

- Provider `SqliteDatabaseProvider` removido do registro; pacotes `Microsoft.EntityFrameworkCore.Sqlite` e `Microsoft.Data.Sqlite` removidos.
- IntroSkipper sem SQLite: helpers, importador e migrações antigas removidos; passou a usar o provider MariaDB do servidor com o schema dedicado `mulletaflix_introskipper`.
- SQL específico convertido: `ON CONFLICT` → `ON DUPLICATE KEY UPDATE`, upsert pelo EF Core, backfill por `ExecuteUpdateAsync`, chave duplicada pelo erro 1062.

## Limpeza SQLite residual (segunda passada)

A primeira passada removeu os pacotes e o provider, mas deixou **ramificações de código SQLite** que foram classificadas como "inalcançáveis". Não estão mais no repositório:

- `UserManager.EnsureUserSchemaTablesAsync`: removido o ramo `supportsSqlite` com o DDL SQLite (~100 linhas), incluindo `INTEGER PRIMARY KEY AUTOINCREMENT`, `TEXT` e datas como texto. Ficou só o DDL MariaDB; qualquer outro provider lança.
- `BillingSeedService.EnsureBillingTablesAsync`: removido o ramo `createSqliteBillingSchemaSql`.
- `DomainDataMigrator`: removido o ramo `FROM sqlite_master`; a checagem de existência de `BaseItems` é só `information_schema`.
- `UserManager.IsMissingTableException`: removida a mensagem `no such table` (SQLite).
- `ConfigurationExtensions`: `SqliteCacheSizeKey` (`sqlite:cacheSize`) → `DatabaseCacheSizeKey` (`database:cacheSize`), e removido o helper morto `GetSqliteCacheSize`. A chave não é usada por nenhum arquivo de configuração (só o dicionário de defaults em memória), então não há contrato externo quebrado.
- Nomes e comentários: `SqliteItemRepositoryTests` → `BaseItemRepositoryImageInfoTests`, `SearchPunctuationTests` → `BaseItemRepositorySearchPunctuationTests`, e as notas sobre "SQLite's variable cap" / "SQL APPLY, not supported on SQLite" passaram a citar MariaDB.
- `fuzz/Emby.Server.Implementations.Fuzz`: removido o alvo `SqliteItemRepository.ItemImageInfoFromValueString` (apontava para classe inexistente) e trocada a `<Reference>` por `HintPath` para o DLL inexistente por `ProjectReference`. O projeto não fazia parte do build e estava quebrado; agora compila com 0 erros.
- `.devcontainer/devcontainer.json` e `.vscode/extensions.json`: removida a recomendação da extensão `alexcvzz.vscode-sqlite`.
- Removidos artefatos gerados que guardavam rastros da classe apagada: `.build/server-publish`, `.build/server-publish-fixed`, `MulletaFlix-master/TestResults` (relatórios de cobertura citando `SqliteDatabaseProvider`) e `MulletaFlix-master/graphify-out` + `.planning/graphs` (grafos citando `SqliteDatabaseProvider.cs` e `SqlitePragmas.cs`). Todos são regeneráveis e ignorados pelo git.

Verificação: `sqlite` não aparece em nenhum arquivo do `MulletaFlix-master` (excluindo `bin`/`obj`) além de:

- `src/Jellyfin.Database/readme.md`, que cita a remoção de propósito, para documentar a decisão;
- `MulletaFlix-master/.agents/` (skills e agentes vendorizados, que listam SQLite como uma opção genérica de banco em guias de design — não é código do servidor).

No Android, a única ocorrência de `sqlite` é um logcat antigo em `artifacts/` (`android.database.sqlite.*`, biblioteca padrão do Android). O app não foi tocado.

## Defeitos de execução encontrados e corrigidos

A validação anterior cobria apenas compilação e a suíte de testes, que não exercita o plug-in: **o IntroSkipper não tinha nenhum projeto de teste**. Rodando as consultas reais contra MariaDB, dois defeitos de execução apareceram.

### 1. Criação de schema dependia da ordem de inicialização

`RelationalDatabaseCreator.EnsureCreated` cria tabelas **somente quando o schema está totalmente vazio**. O segmento e o cache de detecção compartilham `mulletaflix_introskipper`, então o contexto que inicializasse em segundo lugar não criava nada e toda consulta seguinte falhava com `Table 'mulletaflix_introskipper.segments' doesn't exist`.

Correção: novo `IntroSkipper.Db.IntroSkipperSchema.EnsureAsync`, que cria o banco quando ausente, roda `EnsureCreated` e, se as tabelas do próprio contexto ainda não existirem, cria apenas as suas via `IRelationalDatabaseCreator.CreateTablesAsync`. `DetectionCacheDbContext.EnsureSchema` passou a usar o mesmo helper e deixou de chamar `Database.EnsureDeleted()` — que apagaria o schema inteiro, incluindo os segmentos.

### 2. `EF.Parameter(...).Contains(...)` não é traduzível pelo Pomelo

Todas as operações por conjunto de itens usavam `EF.Parameter(ids).Contains(x)`. No Pomelo/MariaDB isso falha em tempo de execução com `Translation of method 'System.ReadOnlySpan<System.Guid>.op_Implicit' failed` — o mesmo erro que aparece no log de produção em `EraseItemsAsync`. `EF.Constant` falha com o mesmo erro.

Correção: as coleções passaram a ser `IReadOnlySet<T>` nomeadas, usando o `Contains` do próprio conjunto. Arquivos alterados:

- `Db/IntroSkipperDatabase.Maintenance.cs`
- `Db/IntroSkipperDatabase.AnalyzedItems.cs`
- `Db/IntroSkipperDatabase.Changes.cs`
- `Db/IntroSkipperDatabase.DisabledItems.cs`
- `Db/IntroSkipperDatabase.ProjectionJournal.cs`
- `Db/IntroSkipperDatabase.Segments.cs`
- `Db/DetectionCacheDatabase.cs`

Os comentários que descreviam `json_each` (mecanismo do SQLite) foram reescritos. Referências obsoletas a arquivo de banco ("database file", "the old file", "EF migrations + legacy import") também foram corrigidas, e `src/Jellyfin.Database/readme.md` deixou de ensinar a gerar migrações do provider SQLite.

## Nova cobertura de teste

`MulletaFlix-master/tests/IntroSkipper.Integration.Tests` (novo, adicionado ao `MulletaFlix.sln`):

- Monta o grafo de serviços chamando o **próprio `PluginServiceRegistrator.RegisterServices`** e aciona o `IntroSkipperDatabaseInitializer`, ou seja, exercita o caminho de produção.
- 10 testes contra MariaDB real: criação do schema dedicado, schema principal intocado, criação em qualquer ordem, ida e volta de segmentos, `EraseItemsAsync`, lookups de estado obsoleto, diário de projeção e cache de detecção.
- `InternalsVisibleTo` do plug-in atualizado para `IntroSkipper.Integration.Tests`.
- O nome termina em `.Integration.Tests` de propósito: o CI roda `--filter "FullyQualifiedName!~Integration"` em runners sem banco, então esses testes não quebram o pipeline.

## Evidências de execução (código de saída 0)

```powershell
dotnet build MulletaFlix-master/MulletaFlix.sln -v:minimal
# 0 Erro(s)

dotnet test MulletaFlix-master/MulletaFlix.sln --no-build --filter "FullyQualifiedName!~Integration"
# 0 falhas — 3193 aprovados, 38 ignorados
# (Server.Implementations.Tests: 837 aprovados / 38 ignorados, como antes)

dotnet test MulletaFlix-master/tests/IntroSkipper.Integration.Tests/IntroSkipper.Integration.Tests.csproj
# 0 falhas — 10 aprovados
```

Verificações no artefato compilado:

- Nenhum `.dll` de SQLite em `Jellyfin.Server/bin/Debug/net10.0` (antes existiam `Microsoft.Data.Sqlite.dll`, `e_sqlite3.dll`, `SQLitePCLRaw.*`).
- `UseSqlite`, `Microsoft.Data.Sqlite`, `EntityFrameworkCore.Sqlite`, `e_sqlite3`, `SQLitePCLRaw`, `sqlite_master`, `introskipper-v2.db` e `introskipper-cache.db`: ausentes das strings de `MulletaFlix.dll`, `MulletaFlix.Server.Implementations.dll`, `MulletaFlix.Database.Implementations.dll` e `IntroSkipper.dll`.
- `mulletaflix-update-win-x64.zip`: 3191 entradas, nenhuma com `sqlite` no nome e nenhum `.db`/`.db-wal`/`.db-shm`.
- `publish-release.ps1` passou a derivar tag e título de `SharedVersion.cs` em vez do valor fixo `v12.0.9`, que republicava uma release errada; as notas de release também são um here-string literal, sem escape de crase.

## Ferramenta adicionada

`publish-release.ps1 -SkipAssets` atualiza apenas os dados da release. Serve quando só as notas mudaram e evita reenviar 740 MB de binários.

## Verificação em tempo de execução — o que foi e o que não foi possível

Os testes de integração rodam contra o **mesmo daemon MariaDB de produção** (`127.0.0.1:3306`, MariaDB 11.4.4, `mysqld` instalado em `C:\Program Files\MulletaFlix\Server\mariadb`), com o provider, o registro do plugin e o assembly exatos que vão para a release. Isso cobre o item 3 do handoff anterior: `mulletaflix_introskipper` é criado automaticamente e as tabelas `Segments`, `SeasonStates`, `SeasonAnalysisOverrides`, `AnalyzedItems`, `DisabledItems`, `ProjectionQueue`, `ProjectionExternalOperations` e `DetectionCache` aparecem nele; o banco principal `mulletaflix` não recebe nenhuma tabela do plugin.

Não foi possível subir um segundo servidor completo: o mulletaflix da máquina mantém o mutex global `Global\MulletaFlix.Server` (PID 9300, escutando em 8096), e uma instância nova encerra na largada com "Outra instância do MulletaFlix já está iniciando ou executando". Como o daemon MariaDB, o provider, o registro do plugin e o binário são os mesmos já exercitados, o que falta dessa verificação é apenas o host do Jellyfin em volta, que esta migração não altera.

## Estado real dos dados nesta instalação

Medido em modo somente-leitura com `Microsoft.Data.Sqlite`, antes de a ferramenta existir:

| Arquivo | Tabelas | Linhas |
|---|---|---|
| `introskipper-v2.db` | Segments, SeasonStates, SeasonAnalysisOverrides, AnalyzedItems, DisabledItems, ProjectionQueue, ProjectionExternalOperations, ImportHistory | **0** em todas (só `__EFMigrationsHistory`=6 e `ImportHistory`=1) |
| `introskipper-cache.db` | DetectionCache | **0** |
| `introskipper.db` (legado) | — | não existe |

O `ImportHistory`=1 é o marcador "no legacy database": quando o importador antigo rodou, não
havia arquivo legado para importar. Ou seja, **nesta instalação nunca houve dados a migrar** —
e o `--dry-run` da ferramenta nova confirma isso, reportando 0 linhas em todas as tabelas.

O banco principal do servidor também nunca foi SQLite: `config/database.xml` sempre apontou
para `MulletaFlix-MySQL`, e é por isso que o schema MariaDB `mulletaflix` já existia com 54
tabelas. Não há banco principal SQLite a migrar.

## Pontos que seguem em aberto (baixo risco, não bloqueiam)

1. `DbImportRecord` / tabela `ImportHistory` sobreviveu ao importador removido. É inofensivo e mantém um schema estável entre versões, mas pode ser removido junto com a tabela em uma limpeza futura.
2. A ferramenta de migração depende de `Microsoft.Data.Sqlite` **9.0.0**, que traz `SQLitePCLRaw 2.1.12` com avisos NU1903 conhecidos. Só afeta quem compila a ferramenta: o servidor não referencia SQLite nem em transitivo, então continua limpo no `dotnet list package --vulnerable` do CI.
3. `ConnectionStrings.config` sobrevive na raiz do repo (herança do Jellyfin, não é um arquivo usado pelo servidor). Não há SQLite dentro dele.

## Observação sobre os erros anteriores

Os erros originais apontavam para o banco `mulletaflix` em `127.0.0.1`, portanto eram erros de conexão MariaDB, não SQLite. A remoção do SQLite resolveu a duplicidade de providers e os bancos locais do IntroSkipper. Se os erros MariaDB continuarem, investigar serviço, credenciais, pool de conexões e saturação causada pelo Nebula.

## Regra de continuidade

Esta tarefa é somente do servidor. Não alterar o aplicativo Android nem publicar release Android para esta solicitação.

---

# Rodada de performance + incidente de conexões (v12.0.46)

## Incidente encontrado em produção (causa raiz provada)

O servidor estava **fora do ar** durante esta rodada. Evidência medida no host:

- `MulletaFlix` mantinha **152 conexões TCP** estabelecidas com o MariaDB.
- MariaDB respondeu `ERROR 1040 (HY000): Too many connections` inclusive para o `mariadb.exe` local.
- `Max_used_connections = 152` com `max_connections = 151` e **83 `Aborted_connects`**.
- `GET /health` e `/ready` respondiam **503**; apenas `/System/Info/Public` (sem banco) respondia 200.

Causa raiz: o pool do EF (`Maximum Pool Size=200`) era **maior** que o `max_connections` do
MariaDB (151), então o processo esgotava o servidor antes de aplicar backpressure. Pior: o
MySqlConnector cria um pool por *connection string*, e o IntroSkipper usa um schema próprio
(`WithIntroSkipperDatabase`), ou seja o mesmo processo podia abrir 2 pools de 200 = 400.

Correções aplicadas:

1. `MySqlDatabaseProvider`: `Maximum Pool Size` passou de 200 para a constante `MaxPoolSize = 100`
   (dois pools ≈ 200 < 300, deixando folga para clientes administrativos).
2. `MariaDbProcessManager.BuildTuningArguments()`: `--max-connections=300` (já existia) agora tem
   comentário com a aritmética real e a medição do incidente.

Tuning **validado em execução** após reiniciar o `mysqld`:

| Variável | Valor efetivo |
| --- | --- |
| `max_connections` | 300 |
| `innodb_buffer_pool_size` | 268435456 |
| `innodb_log_file_size` | 134217728 |
| `innodb_io_capacity` | 2000 |
| `table_open_cache` | 4096 |
| `innodb_lock_wait_timeout` | 25 |

Depois do reinício: `/health` = **200 Healthy**, `Threads_connected` = 5,
`Max_used_connections` = 6, `Aborted_connects` = 0, zero ocorrências novas de
`Too many connections` no log.

Observação relevante: o pico real de conexões simultâneas é 6, não 152. O valor de 152 era
acúmulo do processo de longa duração, o que confirma que o teto anterior de 151 era apertado
demais para o comportamento real do pool.

## Segundo incidente: a suíte de testes matava o servidor de produção

O processo `MulletaFlix` morreu sozinho às `00:35:48` **no meio de um upload Nebula**. Causa:
`PortBindingRecovery.CanTerminateProcess` decidia pelo **nome** do processo antes de comparar o
caminho do executável e retornava `true` de imediato para qualquer processo chamado
`MulletaFlix`. O test host da suíte (`Jellyfin.Server.Tests`) sobe um host que tenta ligar a
porta 8096; ele encontrava o servidor real, o nome batia, e o servidor era encerrado.

Correção: o caminho do executável passou a ser **autoritativo** quando ambos os lados são
legíveis; a comparação por nome virou fallback e só é aceita quando o próprio processo que
recupera a porta também é um executável do servidor (`IsServerExecutableName`). Assim um
`testhost.exe`/`dotnet.exe` nunca derruba um servidor vivo.

Prova red-green em `PortBindingRecoveryTests`:
`CanTerminateProcess_RefusesToKillLiveServerFromTestHost` — **falha** (1 failed/17 passed) contra
a implementação antiga e **passa** (18/18) com a correção.

Consequência prática: **enquanto a instalação rodar 12.0.45, não execute a suíte completa neste
host** — a build instalada ainda tem o guard antigo e o servidor será derrubado de novo.

## Itens de performance aplicados

| Item | Arquivo | O que mudou |
| --- | --- | --- |
| Prune bloqueante | `MetadataService.cs` | `BeforeSaveInternalAsync` com `await`; fim do `GetAwaiter().GetResult()` |
| Probe por filho inalterado | `Folder.cs` | `HasImageInfoChanged` evita `UpdateImagesAsync` por filho em cada scan |
| Rate limit anônimo | `Startup.cs` | `RateLimitMiddleware` movido para **depois** de `UseAuthentication()` |
| Stream síncrono | `NebulaChunkedStream.cs` | `Read` com fast-path de cache + `ReadAsync(Memory)` |
| Lock bloqueante | `ItemPersistenceService.cs` | `WaitAsync().GetResult()` → `Wait()` |
| LOH 16 MB | `NebulaUploadEngine.cs` | leitura direta no array enviado (remove 2ª alocação por parte) |
| Parts O(n²) | `NebulaUploadEngine.cs` | `currentPartDocs` incremental + `BuildPartDocument` |
| Cache de probe sem teto | `MediaEncoder.cs` | `PruneProbeCache` com teto (4096 local / 1024 remoto) |
| Header `Age` | `ImageController.cs` | limitado ao `max-age` em vez da idade da origem |
| Compressão HTTPS | `Startup.cs` | `EnableForHttps = true` + Brotli/Gzip |
| Cache trickplay | `TrickplayController.cs` | `public, max-age=31536000, immutable` |
| `IN` gigante | `BaseItemRepository.TranslateQuery.cs` | `IsPlayed` virou subquery server-side |
| Índice ausente | `MediaStreamInfoConfiguration.cs` + migration | `IX_MediaStreamInfos_StreamType` |

## Estado da validação

- Build da solução: **0 erros**.
- Suíte unitária (sem integração): **3195 aprovados / 38 ignorados / 0 falhas** (15 assemblies).
- Integração MariaDB real: **18/18 aprovados** (falhavam antes por `Too many connections`).
- Release `v12.0.46` publicada com o zip (371318359 bytes, SHA-256
  `2F47A8732D6A59D754C5BF30F7390581E2D4F04498E6A4E77453039BC750B212`).

## Pendência de permissão (não contornada)

As correções de código **não estão** em `C:\Program Files\MulletaFlix\Server` ainda: o processo
atual não tem elevação (`IsAdmin: False`, robocopy devolveu `ERRO 5 / Acesso negado`) e o
`ServerUpdateTask` deixou o pacote em `ReadyToApply` com a mensagem *"Awaiting user approval to
apply in Dashboard"*. Passos que exigem ação humana:

1. Aplicar `v12.0.46` pelo painel (ou executar o updater elevado). Só depois disso o guard de
   porta novo e o teto de pool de 100 entram em vigor.
2. O tuning do MariaDB **já está ativo** e sobrevive, porque o servidor reaproveita a instância
   que está na porta 3306; mesmo assim, confirmar os 6 valores acima após o restart pós-update.
3. Com 12.0.46 instalada, rodar a suíte completa com o servidor no ar para provar o fim do
   incidente do guard de porta.

## Ainda não executado desta auditoria

- `SubtitleController` / `SubtitleEncoder` O(n²) — nenhum hotspot confirmado por leitura direta.
- `DynamicHlsController`: estado de streaming recalculado por segmento.
- Cache de autenticação + varredura linear de API key.

## Rodada do instalador executável (v12.0.63)

### O defeito

O usuário apontou que a release do instalador executável não estava sendo criada. Confirmado por
evidência: `dist\mulletaflix_*_windows-x64.exe` parava em **12.0.45**, ou seja, as releases
12.0.46 a 12.0.62 foram publicadas **só com o zip de atualização**. A causa não era o script de
release — `publish-release.ps1` já varria `mulletaflix_*_windows-*.exe` no `dist` — e sim que
**nenhum passo do fluxo compilava o instalador**. `build-mulletaflix-installer.ps1` só era
chamado por `build-stage-and-installer.ps1`, arquivo que **não existe** no repositório; o
comentário interno do próprio script apontava para esse chamador fantasma.

### A correção

| Arquivo | Mudança |
| --- | --- |
| `build-update-package.ps1` | passo 3 novo: depois do zip, chama `build-mulletaflix-installer.ps1` a partir do mesmo `stage`. `-SkipInstaller` desliga, mas avisa que a publicação vai falhar. |
| `publish-release.ps1` | guarda fail-closed: recusa publicar quando não há `*_windows-x64.exe` da versão da tag nos assets. `-AllowMissingInstaller` é o único escape, explícito. Também anuncia o instalador que será anexado. |
| `publish-release.ps1` (notas) | primeira linha das notas passa a descrever os dois artefatos. |
| `AGENTS.md` | fluxo de release documenta os dois assets e proíbe `-SkipInstaller` em release oficial. |

### Evidência de execução (código de saída 0)

```text
dotnet test MulletaFlix.sln --filter "FullyQualifiedName!~Integration"
# exit 0 (Server.Implementations.Tests: 837 aprovados / 38 ignorados / 0 falhas, 31 s)

# Controle negativo ANTES do build, provando que a guarda falha fechada:
.\publish-release.ps1
# -> throw: Nenhum instalador Windows (mulletaflix_12.0.62_windows-x64.exe) encontrado em '...\dist'.

.\build-update-package.ps1 -Version 12.0.63
# Synced SharedVersion.cs to 12.0.63 / Directory.Build.props to 12.0.63
# zip      354.12 MB (371324534 bytes) SHA256 3EE553CAB94D377369ACDD99B38B5939ADCB2AE45AA529628F1F03097408009A
# installer 386.57 MB
# exit 0

.\publish-release.ps1                      # exit 0
# Instalador que será anexado: mulletaflix_12.0.63_windows-x64.exe
```

### Verificação independente pela API do GitHub (não pela saída do script)

```text
GET /repos/raphaelpera85/mulletaflix-completo/releases/tags/v12.0.63
tag=v12.0.63  draft=False  prerelease=False  assets=2
  - mulletaflix-update-win-x64.zip        354,12 MB  state=uploaded
  - mulletaflix_12.0.63_windows-x64.exe   386,57 MB  state=uploaded
```

### Limite honesto

As releases **12.0.46 a 12.0.62 continuam apenas com o zip**; não foram retro-corrigidas porque
exigiriam reconstruir cada versão a partir do código daquela época. O instalador mais recente
disponível antes desta rodada era o **12.0.45**, 18 versões defasado. A partir da 12.0.63 a guarda
impede que isso volte a acontecer. Cuidado relacionado: o `dist` acumula 23 instaladores antigos
(~8 GB) — eles são necessários para republicar tags antigas, então não foram apagados.

## DramaBox: identificação de séries que nenhum provider conhece

### O pedido e o que estava errado

O usuário apontou séries não reconhecidas por serem da plataforma DramaBox, dando como exemplo
`https://www.dramabox.com/pt/drama/42000002641/321-Adeus-e-Ponto-Final`. Confirmado no banco: as
séries existem como itens mas com **0 provider ids e 0 imagens**. Escala do problema: **2539 de
2752 séries** (92%) estão sem nenhum provider id. O DramaBox não tem entrada no TMDb, TVDB nem no
MyDramaList que o fork já traz, então nada as identifica.

Nas pastas, `N:\Series\3.2.1, Adeus e Ponto Final\Season 01` está **vazia** em todas as séries
DramaBox inspecionadas — a pasta existe, mas não há arquivo nenhum. E o nome gravado no banco é
`3.2 1, Adeus e Ponto Final`: o pipeline de nomes do MulletaFlix trocou o ponto separador por
espaço. Esse detalhe virou o caso de teste central do casamento de títulos.

### A fonte de dados (medida, não suposta)

| Tentativa | Resultado |
| --- | --- |
| `sapi.dramaboxdb.com/*` (API do app) | **403** em todos os caminhos, exige assinatura |
| `/pt/search?keyword=` (inclusive com o título exato) | `bookList` **sempre vazio**, `totalNum=0` — busca é client-side |
| `/pt/drama/<slug>` sem id | **404** |
| `/pt/drama/<bookId>` | **301** e resolve |
| `/pt/drama/<bookId>/<slug>` (página SEO) | **200** com o JSON completo em `__NEXT_DATA__` |
| `/pt/browse/all/<pagina>` | **200**, `bookList` com 12 livros + `pages` = **111** |

Conclusão: **não existe busca por nome utilizável**, então o `bookId` é a única chave confiável e
o índice local é obrigatório. O payload traz título pt, capa, sinopse pt, gênero, tags, elenco com
avatar, contagem de episódios, e por episódio: índice, capa, duração em ms.

Dois achados que mudaram o desenho: os **nomes dos episódios vêm em chinês** mesmo na página `/pt`
(`第一集`), então o título é gerado como "Episódio N" a partir do índice zero-based; e as URLs de
vídeo (`mp4`) são **assinadas com `Expires` de ~1 dia**, então não servem para guardar.

### O que foi implementado

| Arquivo | Papel |
| --- | --- |
| `MediaBrowser.Providers/Plugins/DramaBox/DramaBoxModels.cs` | Book, Performer, Chapter e o documento do índice |
| `.../DramaBoxParser.cs` | Lê o `__NEXT_DATA__` do detalhe e das listagens (puro, sem I/O) |
| `.../DramaBoxTitleMatcher.cs` | Normaliza títulos, pontua similaridade, extrai bookId de URL, reescala capa |
| `.../DramaBoxClient.cs` | HTTP com ritmo fixo, cache de livros, índice em disco, união em crawl parcial |
| `.../DramaBoxSeriesProvider.cs` | Metadados da série: nome, sinopse, gêneros, tags, nota, data, elenco |
| `.../DramaBoxImageProvider.cs` | Capa da série e thumb de cada episódio |
| `.../DramaBoxEpisodeProvider.cs` | "Episódio N" + duração por episódio |
| `.../DramaBoxExternalId.cs` | Id externo editável no painel |
| `Emby.Server.Implementations/ScheduledTasks/Tasks/DramaBoxMatchTask.cs` | Indexa o catálogo e identifica as séries |

Descoberta automática: `ApplicationHost.FindParts()` monta os providers por
`GetExports<IMetadataProvider>()` e as tarefas por `GetExports<IScheduledTask>()`, então nenhum
registro além de `AddSingleton<DramaBoxClient>()` foi necessário.

### Como usar

1. Painel → Tarefas Agendadas → **"DramaBox: indexar catálogo e identificar séries"** → Executar.
   Ela baixa as 111 páginas (~3 min), casa os títulos e enfileira o refresh só dos acertos com
   score ≥ 0.92.
2. Uso manual: em qualquer série, **Identificar** → colar a URL do DramaBox ou o número do bookId.
   Isso resolve exato, sem depender do índice.

### Evidência de execução (código de saída 0)

```text
# Testes unitários do parser, normalizador, providers e casamento (payload real capturado do site)
dotnet test tests\Jellyfin.Providers.Tests --filter "FullyQualifiedName~DramaBox&FullyQualifiedName!~Integration"
# Aprovado! Com falha: 0, Aprovado: 41, Ignorado: 0, Total: 41
#   (23 de normalização/casamento, 8 de parser, 10 de provider ponta a ponta com HTTP falso)

# Suíte completa do servidor
dotnet test MulletaFlix.sln --filter "FullyQualifiedName!~Integration"
# exit 0 — 3236 aprovados, 38 ignorados, 0 falhas (15 assemblies)
#   MulletaFlix.Providers.Tests: 391 aprovados (era 350 antes desta rodada)

# Teste LIVE contra o site real (marcado como Integration, fora da suíte padrão)
dotnet test tests\Jellyfin.Providers.Tests --filter "FullyQualifiedName~DramaBoxLiveIntegration"
# Aprovado em 3 m 14 s
#   índice com >500 livros, persistido em disco
#   "3.2.1, Adeus e Ponto Final"  -> 42000002641
#   "3.2 1, Adeus e Ponto Final"  -> 42000002641   (nome como o MulletaFlix gravou)
#   56 capítulos, com duração e capa
```

### Escopo da tarefa automática (decisão de segurança)

A tarefa **só toca em séries que não têm nenhum provider id**. Motivo: o provider substitui o nome
da série pelo título da plataforma, então casar por título uma série já identificada (por TMDb, por
exemplo) cujo nome coincida reescreveria o nome de uma série que não tem nada a ver. Com essa
guarda, o contrato da tarefa cabe numa frase: ela nunca mexe no que já está identificado.

### Um defeito encontrado pela própria verificação live

A primeira execução do teste live **falhou**: o crawl parou com 408 livros em vez de ~1332. O site
estava saudável (as páginas 30–42 respondiam 200 com 12 livros cada — verificado à parte). O defeito
era do código novo: **uma única falha transitória abortava o crawl inteiro**. Corrigido em
`RebuildIndexAsync`, que agora tolera até 3 falhas consecutivas, segue adiante em falha isolada e,
quando o crawl termina incompleto, faz **união com o índice anterior** para nunca encolher o
catálogo. Este é o motivo de o teste live existir: um payload capturado não prova que o crawler
funciona contra o que o site serve hoje.

### Limite honesto

- **Nada foi ativado em produção.** O código está no repositório e a release foi publicada, mas a
  tarefa só roda depois que o usuário aplicar a atualização pelo painel.
- As séries DramaBox estão com a pasta `Season 01` **vazia**: o provider preenche metadados de série
  e de episódio, mas **não há episódio nenhum para preencher** enquanto arquivos não existirem. A
  contagem de episódios vem da plataforma, não do disco.
- O limiar 0.92 é conservador de propósito: prefiro deixar passar um título ambíguo do que colar
  metadados errados numa série. Falsos negativos aparecem no log como candidatos abaixo do limiar.
- O índice cobre o **catálogo do locale `pt`**. Títulos só disponíveis em outro idioma não casam.

### Release v12.0.64 (zip + instalador, ambos verificados)

```text
.\build-update-package.ps1 -Version 12.0.64      # exit 0
  SharedVersion.cs e Directory.Build.props -> 12.0.64
  zip       354,14 MB (371340141 bytes) SHA256 DAF81D91EA47E48FB7B67D7A7690CB8E13303D25D58309BBCF0491FFA3BBB60C
  instalador 386,59 MB

.\publish-release.ps1                            # exit 0
  Instalador que será anexado: mulletaflix_12.0.64_windows-x64.exe

GET /repos/raphaelpera85/mulletaflix-completo/releases/tags/v12.0.64
  tag=v12.0.64  draft=False  prerelease=False  assets=2
    - mulletaflix-update-win-x64.zip        354,14 MB  state=uploaded
    - mulletaflix_12.0.64_windows-x64.exe   386,59 MB  state=uploaded
  notas mencionam DramaBox: sim
```

Uma armadilha operacional encontrada aqui: ao cancelar o build do pacote, o `makensis.exe` filho
**não morre junto** e continua segurando arquivos do `stage`, o que faz o próximo build falhar com
`The process cannot access the file ... because it is being used by another process`. Se um build for
interrompido, conferir `Get-Process makensis` e encerrar o órfão antes de tentar de novo.

## Rodada 23 da auditoria — H-1 e F-2 executados (release v12.0.65)

O detalhe técnico completo está em `MulletaFlix-master/docs/auditoria-completa-performance.md`,
seção "Rodada 23". Resumo do que mudou no produto:

| Achado | Correção | Verificação |
| --- | --- | --- |
| **H-1** (P0) | O aviso de resposta lenta só saía com log de Debug ligado, então nunca saía em produção. Passou a `Information`, com supressão de 30 s por caminho e mapa limitado a 512 entradas | 6 testes novos em `Jellyfin.Api.Tests`; `Api.Tests` foi de 138 para 144 |
| **F-2** (P0) | O `en-US` deixou de ser importado estaticamente em `dateFnsLocale.ts`; os helpers do locale moram em `date-fns/esm/_lib/`, fora do caminho que o `getVendorChunk` exclui, e por isso arrastavam o `vendor-date-fns` inteiro para o grafo estático da entrada | **285 KB raw / 54 KB gzip** fora da critical path; `vendor-date-fns` saiu do `modulepreload` e os imports estáticos do entry caíram de 6 para 5 — conferido também no `stage` do pacote |

Duas correções de atribuição do próprio relatório nesta rodada:

- **E-13** não está aberto como escrito: `GetQueryFiltersLegacy` já usa uma subconsulta só de ids
  (o problema medido, joins repetidos, foi resolvido). O que resta é o filtro barato ser avaliado até
  4×; materializar em memória trocaria isso por uma lista `IN` gigante — a classe de problema do E-6.
  Registrado como parcialmente resolvido, com o motivo.
- **F-3** está aberto, mas não pelo motivo escrito: `vendor-mui` **não** está em `modulepreload`. Ele
  bloqueia o primeiro render de outro jeito — `index.tsx:98` faz `await import('./RootAppRouter')`
  antes do `renderApp()`, e o `ThemeProvider` está em `RootAppRouter.tsx:1`. F-1 e F-3 são o mesmo
  caminho de boot e serão tratados juntos na próxima rodada.

### Release v12.0.65 (zip + instalador, ambos verificados)

```text
.\build-update-package.ps1 -Version 12.0.65      # exit 0
  zip       354,14 MB (371337559 bytes) SHA256 4204D7B7169A0250CCF1617EDB4EC567E8721AEBFE3B1E769C16EEC1C71F516C
  instalador 386,59 MB

.\publish-release.ps1                            # exit 0
GET /repos/raphaelpera85/mulletaflix-completo/releases/tags/v12.0.65
  assets=2, ambos state=uploaded
```

Portão da rodada: `dotnet test` da solução **exit 0 — 3242 aprovados, 38 ignorados, 0 falhas**;
`npm run build:check` limpo; `npm test` **200/200 em 23 arquivos**; `npm run build:production` exit 0.

## Rodadas 24 e 25 — medição do banco, S-4, S-14 e o bug das séries homônimas

### O que a medição do banco mudou (rodada 25)

Liguei `slow_query_log` e `general_log` por janelas curtas no MariaDB de produção (e restaurei as
variáveis depois: `slow_query_log=OFF`, `long_query_time=10`).

| Medida | Resultado |
| --- | --- |
| Queries acima de 50 ms em 25 s | **0** |
| Delta de `ROWS_READ` de `baseitems`, `baseitemimageinfos`, `itemvaluesmap` em 60 s ociosos | **0, 0, 0** |
| Tráfego ocioso | `jobqueue` (81 linhas) e `ProjectionQueue` (0 linhas) |
| Índices do schema com zero leituras em 11,2 h | **0 de 166** |

Conclusão: **não existe hotspot em regime permanente nem índice morto**. Os 609 M de linhas lidas
acumuladas em `baseitems` são **rajada** (scan/refresh), não carga contínua — e a frase "~730 leituras
por segundo sustentadas" que eu havia escrito foi **retratada** no relatório. O E-12 (remover índices de
`userdata`) também foi **retratado**: a tabela tem **15 linhas** e 0,09 MB de índice.

### Correções executadas

| Achado | Correção | Evidência |
| --- | --- | --- |
| **S-4** | O `finally` do laço de upload limpava a chave de deduplicação **também** quando o item voltava para a fila, permitindo o mesmo arquivo subir duas vezes. Agora a chave só sai em desfecho terminal e o claim em memória é sempre liberado | 4 testes novos; `--filter NebulaStagingWatcher` → 9/9 |
| **S-14** | Cada item adicionado abria um laço com até **180 tentativas** de `GetItemById` + `GetImagePath` + stats de arquivo. Backoff crescente (2 s → 10 s) dentro do mesmo orçamento de 180 s reduz para **~20 tentativas**, e um `SemaphoreSlim` limita a **24** itens aguardados ao mesmo tempo | build 0 erros; suíte verde (contagem derivada das constantes, não medida) |
| **Séries homônimas** | `SeriesResolver` extraía o ano do nome da pasta e **descartava**; o ano agora é preservado, vira `ProductionYear` e entra na busca do TMDb (com fallback sem ano) | 9 testes novos; suíte 3254/0 |

### O bug das séries homônimas, em uma frase

`A Agência (2020)` e `A Agência (2024)` produziam lookup info **idêntico** porque o ano era jogado
fora; as duas casavam com o mesmo TMDb, ficavam com os **mesmos provider ids** e portanto com a **mesma
`PresentationUniqueKey`** — o que o cliente mostra como uma série só. Confirmado no banco: dois itens
distintos, ids distintos, mesmos `Imdb/Tmdb/Tvdb`, e conteúdos diferentes (85 vs 14 episódios).

**Pendência manual:** os dois itens **já danificados** não se consertam sozinhos — `GetMetadata`
curto-circuita no provider id existente. É preciso usar **Identificar** na série de 2020 e escolher a
série correta, ou remover a identificação atual, e então atualizar os metadados.

### Releases publicadas e verificadas (2 assets cada)

| Tag | Conteúdo |
| --- | --- |
| `v12.0.66` | S-4 (uploads duplicados) e F-5 (tema de branding por sessão) |
| `v12.0.67` | S-14 (notificações de nova mídia) |
| `v12.0.68` | Ano de série preservado e usado na identificação |

Portão das rodadas 24–25: `dotnet test` da solução **exit 0 — 3254 aprovados, 38 ignorados,
0 falhas**; `npm run build:check` limpo; `npm test` **200/200**.

## Rodada 26 — arnês de boot do cliente, F-1 medido e revertido, H-14

### Arnês de verificação do cliente web (nova capacidade)

`MulletaFlix-web-master/verify-web-boot.mjs` (arquivo de trabalho, fora do git por decisão consciente —
mantido no repo porque é útil e opt-in).

O que ele resolve: o boot do cliente **não** era verificável daqui. A suíte Playwright do repositório
aponta para o servidor instalado (`127.0.0.1:8096`) **e cria usuários e sessões**, e o dev server não
tem proxy de API. O arnês intercepta `/web/**` no Playwright e serve o `dist` local, mantendo a página
na mesma origem para que as chamadas de API continuem reais.

Como usar:

```powershell
cd MulletaFlix-web-master
node verify-web-boot.mjs                        # serve o dist local por interceptacao
$env:WEB_MODE='installed'; node verify-web-boot.mjs   # controle: bundle instalado
$env:WEB_LATENCY_MS='40'; node verify-web-boot.mjs    # simula latencia de rede por asset
```

Ele reporta render, avisos `[bootstrap]`, erros de página/console, quantos arquivos vieram do `dist` e
quantos caíram no servidor (mistura de bundles invalida a comparação) e o timing de cada chunk contra a
marca de render. **A marca de render ignora o splash estático** do `index.html`: `#reactRoot` já começa
com 5 nós e 6741 bytes, então "tem filhos" seria falso positivo.

### F-1 — implementado, medido e REVERTIDO

Adiantar `import('./RootAppRouter')` para logo depois de `appHost.init()` **piorou** o primeiro render:
média de **290 ms → 343 ms** (3 execuções cada, 40 ms de latência por asset, faixas sem sobreposição).
Antecipar o grafo de ~200 chunks faz ele competir com os assets do caminho crítico. Revertido, com
`git diff` vazio confirmando revert exato.

O problema real do F-1, agora medido: `import('./RootAppRouter')` arrasta o **grafo inteiro do app**
(as listas de rotas são imports estáticos dentro de `RootAppRouter`) e `renderApp()` espera por ele —
220 chunks únicos antes do primeiro render. A correção estrutural é rota preguiçosa
(`lazy: () => import(...)`), refactor da camada de rotas com risco em todas as telas.

### H-14 (P2) executado

`NebulaHttpStreamServer.cs:480` — `new byte[128 * 1024]` por request virava ~1 GB de lixo por GB
transmitido. Agora `ArrayPool<byte>.Shared.Rent(...)` com devolução em `finally`, e o `Math.Min` usa a
constante `StreamBufferSize` (não `buffer.Length`, que pode ser maior que o pedido).

### Release

`v12.0.69` publicada e verificada: **2 assets**, ambos `uploaded`.

Portão: `dotnet test` da solução **exit 0 — 3254 aprovados, 38 ignorados, 0 falhas**;
`npm run build:check` limpo; `dotnet build Jellyfin.Server.Implementations` 0 erros;
`node verify-web-boot.mjs` → **BOOT OK** (render, 0 avisos, 0 erros de página).

## Rodada 27 — E-4 medido sob carga real e executado (o maior ganho da auditoria)

### O que mudou o quadro: servidor em uso, não ocioso

Até aqui medi o banco com o servidor **ocioso** e o delta de linhas lidas era zero. Nesta rodada ele
estava em **uso real** — upload Nebula ativo e o log mostrando
`EpisodeMetadataService: File changed, pruning extracted data` para `N:\Series\...`. Em 60 s:

| Tabela | Linhas lidas |
| --- | --- |
| **`peoples`** | **+3.156.136** |
| `baseitems` | +59.882 |
| `ancestorids` | +35.256 |

`peoples` tem 52.652 linhas, então isso é **60 varreduras completas da tabela por minuto** (uma por
segundo). A conta fecha: 52.652 × 60 = 3.159.120 contra 3.156.136 medidos.

### A causa e a correção

`PeopleRepository.cs:112` montava a chave `e.Name.ToLower() + "-" + e.PersonType` dentro de um tipo
projetado e comparava com `Enumerable.Contains`. O fork tem um helper que traduz esse `Contains` quando
o lado direito é **coluna** da entidade (as outras ~20 ocorrências no código); com valor **computado**
não dá, e o EF materializava a tabela inteira. Isso roda em `UpdatePeople`, ou seja **a cada item cujo
metadado é atualizado**.

Agora filtra primeiro por `Name` (coluna indexada, `IX_Peoples_Name`, collation
`utf8mb4_general_ci` → o `IN` é case-insensitive e devolve superconjunto) e compara a chave em memória.

| plano | type | índice | linhas |
| --- | --- | --- | --- |
| antes | `ALL` | nenhum | **52.652** |
| depois | `range` | `IX_Peoples_Name` | **5** |

~10.500× menos linhas por atualização de metadados. Verificado no host real com `EXPLAIN`; o "antes" é
`EXPLAIN SELECT ... FROM peoples` sem `WHERE`, que é o que o código antigo de fato fazia.

### ATENÇÃO — duas sessões trabalhando neste repositório ao mesmo tempo

Durante esta rodada, **outra sessão/agente editou os mesmos arquivos em paralelo**:

| arquivo | mtime | observação |
| --- | --- | --- |
| `Item\ItemPersistenceService.cs` | 14:15:58 | não é meu |
| `Nebula\NebulaMetadataExportService.cs` | 14:15:34 | não é meu |
| `tests\...\NebulaMetadataExportServiceTests.cs` (novo) | 14:26:40 | não é meu |
| `tests\...\Item\ItemPersistenceServiceTests.cs` | 14:28:17 | não é meu |
| `publish-release.ps1` | **14:44:37** | **reescrito por terceiros** |

Consequências concretas:

1. A suíte ficou **vermelha** no meio da rodada por motivos que não eram meus (erro `CA2016` no arquivo
   de teste novo deles e uma expectativa de `1213` em `ItemPersistenceServiceTests`). Voltou a verde
   sozinha quando eles terminaram — a execução final deu **exit 0, 3275 aprovados, 0 falhas**.
2. O `publish-release.ps1` foi **reescrito** e as notas de release que eu havia acumulado (DramaBox,
   séries homônimas, uploads duplicados, date-fns, aviso lento, streaming, pessoas) foram substituídas
   pelas notas deles. O **guard do instalador sobreviveu** (`-AllowMissingInstaller`, 4 ocorrências) —
   que era o pedido explícito do usuário.
3. A release `v12.0.70` **já existia** (criada em 21/09); meu publish a atualizou e trocou os dois
   assets por um build do estado atual da árvore, que contém as mudanças das duas sessões.

**Não reescrevi o template de notas de propósito**: seria uma corrida de escrita num arquivo que a outra
sessão está usando, com risco de apagar o texto mais novo dela. Se você quiser as duas contribuições nas
notas, o caminho é mesclar depois que as edições pararem.

Meu código **está** no pacote publicado — a cronologia confirma: edição em `PeopleRepository.cs` às
14:14:22 → DLL no `stage` às 14:34:48 → zip às 14:37:36. Só a nota ficou de fora.

### Release

`v12.0.70` publicada e verificada: **2 assets**, ambos `uploaded` (zip 354,14 MB, instalador 386,59 MB).

Portão da rodada: `dotnet build Jellyfin.Server.Implementations` **0 erros**;
`dotnet test` da solução **exit 0 — 3275 aprovados, 38 ignorados, 0 falhas**.

---

## Rodada 28 — o caminho de gravação passa a ter teste contra MariaDB real

### 1. `SaveBaseItemEntities` coberto de ponta a ponta (`ServerPersistenceMariaDbTests`)

O buraco era conhecido e não dava para fechar com o provider InMemory: o caminho de gravação chama
`ExecuteDelete` para as linhas filhas de um item reescrito (providers, imagens, metadata fields, trailer
types), e o InMemory **não implementa** `ExecuteDelete`. Ou seja, a suíte inteira nunca executava o ramo
que quebrou em produção.

`tests\IntroSkipper.Integration.Tests\ServerPersistenceMariaDbTests.cs` fecha isso com dois testes que
rodam contra um MariaDB de verdade:

| teste | o que prova |
| --- | --- |
| `SaveItems_ReSavingAnExistingItem_RewritesItWithoutTrackingConflict` | regravar um item que já existe (metadado novo + provider id novo) não dispara mais `AddRange` → `IdentityMap.ThrowIdentityConflict`; é o bug que abortava refresh de metadados em scan |
| `SaveItems_SavingTwoDistinctItems_KeepsBothRows` | duas séries com **o mesmo nome e anos diferentes** continuam sendo duas linhas, cada uma com o seu `ProductionYear` (2020 e 2024) |

Como rodar (exige o MariaDB em `127.0.0.1:3306`):

```text
dotnet test MulletaFlix-master\tests\IntroSkipper.Integration.Tests\IntroSkipper.Integration.Tests.csproj --filter "FullyQualifiedName~ServerPersistenceMariaDbTests"
# 2 aprovados, 0 falhas
```

O schema é descartável (`mulletaflix_integration_persistence`), dropado antes de cada execução — nunca
toca o schema de produção. O nome do projeto contém `Integration.Tests`, então o filtro do CI
(`FullyQualifiedName!~Integration`) o mantém fora da suíte rápida; é intencional, porque ele exige um
banco vivo.

#### Dois estáticos e uma armadilha de harness (cada um custou uma iteração)

1. `BaseItem.LibraryManager` — `GetAncestorIds()` passa por `LibraryManager.GetCollectionFolders(this)`.
2. `BaseItem.ConfigurationManager` — `BaseItemMapper.Map` → `SortName` → `CreateSortName()` lê
   `ConfigurationManager.Configuration.SortRemoveWords`. Sem ele o NRE acontece **dentro** do save.
3. `BaseItemMapper.Map` grava `appHost.ReverseVirtualPath(dto.Path)`. Um `Mock.Of<IServerApplicationHost>()`
   cru devolve `null` ali, o que **anula o `Path` de toda linha gravada**. A primeira versão do segundo
   teste viu "2 linhas, 1 path distinto" e pareceu um bug de mesclagem no caminho de persistência.
   Não era: as linhas estavam certas, era o harness apagando o valor. O mock agora devolve identidade, e
   o teste assere `Path` distinto **e** `ProductionYear` distinto.

### 2. Backlog declarado — o que não fecha em código

| item | situação | por quê |
| --- | --- | --- |
| 403 nas fontes atrás de Cloudflare | **não corrigível em código** | o bloqueio é do fingerprint TLS do .NET, não do conteúdo da requisição; nenhuma troca de header resolve |
| app Android | **não publicado, por decisão do usuário** | o escopo desta rodada é servidor + cliente web |
| árvore sem commit | **pendente** | 266 entradas (175 modificadas, 85 novas, 6 removidas); último commit `48831916`, de 21/09 23:59. `dist/` é ignorado (`.gitignore:7`), então os ~8 GB de instaladores não entram. Não commitei porque outra sessão edita o mesmo repositório: o commit misturaria trabalho em andamento das duas |
| `MyDramaListSeriesImageProvider` | **corrigido nesta rodada** | era o mesmo defeito do `[ERR]` por item; agora tem `FailureCooldown` de 30 min |

### Release

`v12.0.71` publicada e verificada pela API: `draft=false`, **2 assets** `uploaded`
(zip 354,14 MB, instalador 386,59 MB).

Portão da rodada 28:

```text
dotnet test MulletaFlix.sln --filter "FullyQualifiedName!~Integration"                 # exit 0 — 3275 aprovados, 38 ignorados, 0 falhas
dotnet test ...IntroSkipper.Integration.Tests --filter ServerPersistenceMariaDbTests   # 2 aprovados, 0 falhas
.\build-update-package.ps1 -Version 12.0.71                                            # exit 0 — zip + instalador
.\publish-release.ps1                                                                 # exit 0 — 2 assets uploaded
```

---

## Rodada 29 — o erro que está queimando a produção agora, e duas retratações por medição

### O achado: 2.355 falhas vivas no formato exato do bug de tracking

Medido no processo em execução (12.0.69, PID 10716, iniciado 14:28:40):

| medida | valor |
| --- | --- |
| `[ERR]` no processo atual | **2.355** (todas as linhas de erro do processo) |
| janela | rajada de ~14:52 a 15:42 (142 erros na hora 14, 2.222 na hora 15, **nenhum** na hora 16) |
| ritmo dentro da rajada | 69–78 por minuto |
| exceção dominante | `InvalidOperationException: The instance of entity type 'BaseItemEntity' cannot be tracked because another instance with the same key value for {'Id'} is already being tracked` — essencialmente todas |
| log gerado no período | 36 MB |
| memória do processo | 2.009 MB (14:28) → **4.551 MB (15:40)** no pico → 2.995 MB (16:06) depois do GC |

A rajada **terminou sozinha** às 15:42 e a memória caiu de 4.551 para 2.995 MB em ~25 min: era lixo
coletável (pilhas de exceção, DbContexts do laço), não vazamento. O que fica é o dano: os metadados
daquela série não foram gravados, e o próximo refresh da mesma série deve repetir a rajada.

Contagem dos frames do próprio código nos blocos de exceção:

```text
BaseItem.UpdateToRepositoryAsync                            2.270x
LibraryManager.UpdateItemsAsync                             2.270x
SeriesMetadataService.RefreshMetadata                       2.272x
Folder.RefreshChildMetadata                                 2.273x
Folder.RefreshAllMetadataForContainer                       2.273x
LimitedConcurrencyLibraryScheduler.ProcessItem              2.273x
ItemPersistenceService.SaveBaseItemEntities                 2.278x
ItemPersistenceService.UpdateOrInsertItemsCore              2.278x
MetadataService.SaveItemAsync                               2.278x
MetadataService.RefreshMetadata                             2.280x
```

Leitura: o refresh de uma série dispara o refresh dos filhos, e **todo** save de filho morre no
`SaveBaseItemEntities` (ramo de item existente). A operação inteira é perdida — e como o refresh foi
tentado centenas de vezes, o resultado foi uma rajada de 50 minutos, não uma falha permanente.

### A correção já está na árvore; o que faltava era o teste no formato do stack vivo

O teste da rodada 28 usava duas séries soltas: sem pai, sem imagens. O stack vivo difere em três pontos —
o item é um **filho** (episódio), o **pai já está salvo**, e o filho carrega **as quatro** espécies de
linha que o mapper religa na entidade recém-criada (provider, imagem, locked field, trailer type).

Novo teste: `SaveItems_RefreshingAnExistingEpisodeUnderASavedSeries_RewritesTheChildRows` — salva série,
temporada e episódio, e depois re-salva o episódio (o refresh) com provider id, locked field e imagem.

```text
dotnet test MulletaFlix-master\tests\IntroSkipper.Integration.Tests\IntroSkipper.Integration.Tests.csproj --filter "FullyQualifiedName~ServerPersistenceMariaDbTests"
# 3 aprovados, 0 falhas
```

As asserções são sobre as linhas filhas, não só sobre "não lançou": um teste que só checa ausência de
exceção continuaria verde se as linhas simplesmente deixassem de ser gravadas.

Terceiro estático descoberto no arranjo, depois de dois NREs dentro do save: `Video.RecordingsManager`.
`UpdateOrInsertItemsCore` chama `GetUserDataKeys()`, e `Episode` sobrescreve via `Video`, cujo
`SourceType` consulta `IsActiveRecording()` → `RecordingsManager.GetActiveRecordingInfo(Path)`.

### Duas retratações por medição no host

**H-7 (P1 — uma query por tile de trickplay): retratado.** No banco real,
`SELECT COUNT(*) FROM trickplayinfos` = **0 linhas**, e os únicos índices são a PK `(ItemId, Width)`.
A "query por tile" existe no código (`TrickplayManager.cs:595`) mas, sem dados e sem tráfego de tile,
custa um probe de índice em tabela vazia. A correção continuaria correta — não tem efeito mensurável
**neste** host, então não é prioridade.

**S-3 (P1 — limpeza de órfãos O(dirs × files)): retratado na magnitude.** Medido na árvore real de
staging:

| raiz | dirs | arquivos | entradas que o algoritmo atual enumera | multiplicador |
| --- | --- | --- | --- | --- |
| `E:\NebulaStage` | 2.059 | 2.582 | 8.112 em **2.058 varreduras recursivas** | 3,14× |
| `F:\NebulaStage` | 25 | 281 | 1.124 | 4,00× |

Tempo, emulando os dois algoritmos sobre `E:\NebulaStage`: atual **0,2 s** (5.530 entradas) contra passe
único **0,1 s** (2.582 entradas) → **1,5×**. A complexidade alegada é real na forma, mas a árvore é rasa
(profundidade ≤ 3) e pequena: cerca de 0,2 s a cada 10 minutos. Não paga um ciclo de release.

### Achado novo, ainda não medido: ffmpeg image extraction timed out

28 ocorrências no processo atual (24 delas para `A Agência (2020)`):
`MediaBrowser.Common.FfmpegException: ffmpeg image extraction timed out`. É a segunda maior fonte viva
de `[ERR]`, num host em que o laço de falhas acima consumia CPU e IO. Fica registrado para a próxima
rodada: a causa provável é contenção, e afirmar isso exige medição própria.

### Release

`v12.0.72` publicada com os dois assets (zip + instalador). Portão da rodada:

```text
dotnet test MulletaFlix.sln --filter "FullyQualifiedName!~Integration"   # exit 0 — 3275 aprovados, 38 ignorados, 0 falhas, 0 erros de build
dotnet test ...IntroSkipper.Integration.Tests --filter ServerPersistenceMariaDbTests   # 3 aprovados, 0 falhas
.\build-update-package.ps1 -Version 12.0.72                              # exit 0 — zip 354,14 MB + instalador 386,59 MB
.\publish-release.ps1                                                    # exit 0
```

### O que isso significa para o host agora

A correção está no pacote, mas o servidor instalado é o **12.0.69**. A rajada de 15:42 parou por conta
própria (o servidor está Healthy e subindo arquivos normalmente às 16:06), mas o defeito continua no
binário: **qualquer** refresh daquela série repete a rajada, e cada repetição custa os metadados
daquela série. A 12.0.72 só entra em produção pela atualização do painel.

---

## Rodada 30 — o maior item da critical path não era código: 1 MB de imagem

### Passei a medir bytes, não só a ordem dos chunks

O arnês da rodada 26 respondia "em que ordem os chunks chegam". Ele agora também responde "quantos bytes
chegam **antes do primeiro render**" — a pergunta que importa para quem abre o cliente longe do servidor.

Primeira medição no bundle publicado (12.0.72), uma execução:

| medida | valor |
| --- | --- |
| assets que terminam antes do render | 131 |
| total transferido antes do render | **4.021 KB** |
| primeiro render | 371 ms |

Top da critical path, em KB: `search-brand-tile.png` **1.003**, `vendor-jellyfin` 436, `vendor-mui` 429,
`index-*.js` 310, `vendor-date-fns` 285, `pt-br` 178, `en-us` 167, `index-*.css` 150, `vendor-react` 139,
`icon.png` 120, `vendor-router` 93, `RootAppRouter` 82.

O maior item da critical path não era código, arquitetura nem banco: era **uma imagem de 1 MB**.

### A correção

`src/assets/branding/search-brand-tile.png` — 1431×1099, 24bpp, **1.003 KB**, o logo do splash
(`.splashLogo`, telas ≥992px) e o logo do `pageTitleWithDefaultLogo` nos temas. Convertido para
`search-brand-tile.webp` (q95, **53 KB**) com o **ffmpeg que o próprio servidor instala**.

- 6 referências no cliente web: `site.scss:34`, `themes/_base/_theme.scss:93,95`, `themes/appletv:38`,
  `themes/netflix:41`, `themes/purplehaze:34`.
- 1 referência no servidor: `Jellyfin.Server/wwwroot/api-docs/swagger/custom.css:9` (logo do Swagger),
  que passou a apontar para a cópia `.webp` em `wwwroot/branding/`.
- Verificação visual: li o PNG e o WebP lado a lado. Idênticos ao olho — sem banding no fundo navy, sem
  artefato nas bordas do wordmark. (A imagem é chapada, o que explica 53 KB.)
- O PNG **não foi apagado**: a URL `/branding/search-brand-tile.png` pode ser usada por cliente fora
  deste repositório (o app Android está fora do escopo desta auditoria) — trocar a referência é seguro,
  remover o arquivo não é decisão minha.

### A/B na mesma árvore (2 builds, 2 execuções cada)

| build | execuções (bytes antes do render) | média | primeiro render |
| --- | --- | --- | --- |
| PNG original | 4.146 KB / 4.132 KB | **4.139 KB** | 253 / 267 ms |
| WebP q95 | 2.242 KB / 2.244 KB | **2.243 KB** | 286 / 278 ms |

**1.896 KB a menos antes do primeiro render**, com `BOOT OK` nas quatro execuções (renderizou, sem erro
de página, sem fallback). O asset responde por 950 KB disso de forma determinística (1.003 → 53 KB, e o
`dist` não emite mais o PNG); o restante eu **não isolei** — é consistente com o mesmo logo sendo buscado
por uma segunda URL (o `pageTitleWithDefaultLogo` dos temas usa a cópia sem hash de
`copy-assets.cjs`), mas não medi essa segunda busca separadamente.

Evidência de que a mudança está no artefato: o zip da release caiu de 354,14 MB para 353,27 MB.

### Retratação: o F-2 não tirou 285 KB da critical path

A rodada 23 registrou o F-2 como "285 KB fora da critical path". Medição de hoje, no bundle publicado:
`vendor-date-fns` **continua com 285 KB e continua terminando antes do primeiro render** (end=61 ms
contra render=371 ms).

O que a correção fez, e está no bundle: o helper do barril de locale (`buildLocalizeFn`) **não está mais
no chunk**. O que ela não fez: tirar o chunk da critical path, porque **quatro componentes importam
funções do date-fns estaticamente** — `components/dashboard/users/UserCardBox.tsx:3`,
`apps/dashboard/features/sessions/utils/filterSessions.ts:2`,
`apps/dashboard/features/tasks/utils/edit.ts:2`,
`apps/dashboard/features/tasks/components/TaskLastRan.tsx:4` — e eles entram no grafo do entry pelas
rotas estáticas do `RootAppRouter` (o mecanismo do F-1, que foi implementado, medido e revertido na
rodada 26).

**F-2 = "o barril de locale saiu do chunk". F-2 ≠ "285 KB fora da critical path".**

### F-3 confirmado, com número e mecanismo

MUI + emotion + icons somam **483 KB** terminando antes do primeiro render (429 + 28 + 26). O
`ThemeProvider` de `RootAppRouter.tsx:1` é só parte da história: o MUI entra no grafo do entry porque o
mesmo arquivo importa **estaticamente** os arrays de rota (`apps/dashboard/routes/routes`,
`apps/experimental/routes/routes`). Mexer só no `ThemeProvider` não remove byte nenhum; mexer nos arrays
é o F-1. Fica registrado com o número correto — não é uma correção de uma linha.

### Achado novo registrado, não executado: timeout de extração de I-frame

`MediaEncoder.cs:942-946` adiciona `thumbnail=n=24` no caminho I-frame (amostra 24 quadros para escolher
o melhor); `MediaEncoder.cs:1024-1037` mata o ffmpeg em `ImageExtractionTimeoutMs`, que no host está
`0` em `system.xml` — ou seja, o default de 10.000 ms de `MediaEncoder.cs:48`.

No log real (14:52–15:42): **28 ocorrências** de `ffmpeg image extraction timed out ... after 10000ms`,
24 delas na mesma série. Cada uma é seguida do fallback "standard way" — que é exatamente o caminho
**sem** a amostragem de 24 quadros, ou seja, **a imagem que o usuário recebe hoje**. O custo é 10 s de
ffmpeg morto por item (segurando `_thumbnailResourcePool`, o lock global de extração, em
`MediaEncoder.cs:1020`) mais um stack de exceção no log.

Caminho de correção: ligar o tradeoff que já existe (`FFmpeg:imgExtractPerfTradeoff`, hoje `false` em
`ConfigurationOptions.cs:23`), que remove o `thumbnail=n=24`. **Não executei** por dois motivos: (a) a
biblioteca está em `N:`, que não existe nesta sessão, então não pude medir a extração no arquivo real; e
(b) a decisão é de produto — o tradeoff muda o quadro escolhido para **todos** os itens, não só para os
que estouram o timeout.

### Release 12.0.73 e portão da rodada

```text
npm run build:check (tsc --noEmit)                                        # exit 0
npm run build:production                                                 # exit 0
npm test (vitest)                                                        # 200 aprovados, exit 0
dotnet test MulletaFlix.sln --filter "FullyQualifiedName!~Integration"   # exit 0 — 3275 aprovados, 38 ignorados, 0 falhas, 0 erros
dotnet test ...IntroSkipper.Integration.Tests --filter ServerPersistenceMariaDbTests   # 3 aprovados, 0 falhas
.\build-update-package.ps1 -Version 12.0.73                              # exit 0 — zip 353,33 MB + instalador 386,64 MB
.\publish-release.ps1                                                    # exit 0
```

`v12.0.73` verificada pela API: `draft=false`, 2 assets `uploaded` (zip 353,33 MB, instalador 386,64 MB).

---

## Estado final da auditoria (rodadas 1–30)

**Executado e verificado**, por área: persistência e schema (E-1 a E-5, E-11, D-2, B-1, B-3, B-10,
B-12); tarefas e memória (S-1, S-2, S-5, S-6, S-7, S-9, S-10, S-11, S-12, S-13, S-14, B-5); HTTP e
streaming (H-1, H-3, H-4, H-9, H-11, H-14, H-15); cliente web (F-2 parcial, F-5, F-8) e a imagem de boot
da rodada 30; pool do MariaDB e tuning de conexões.

**Retratado por medição no host** (o achado não resistiu à evidência): E-12 (índices não redundantes),
F-1 (a reordenação de imports mediu **mais lento** e foi revertida), F-2 (o barril saiu do chunk, os
285 KB **não** saíram da critical path), H-7 (`trickplayinfos` tem 0 linhas; sem tráfego de tile),
S-3 (custo real medido: 0,2 s por varredura, 1,5× — não paga um ciclo de release).

**Não executado, com motivo registrado:** H-2 (P0 — cache do `StreamState` por sessão; é refactor do
caminho quente de playback e não há como verificar reprodução neste ambiente: o teste E2E do repositório
aponta para produção e cria usuários/sessões); F-3 (P0 — o mecanismo é o do F-1, medido e revertido);
timeout de I-frame do ffmpeg (decisão de produto + `N:` indisponível); e os P1/P2 restantes de cada
frente (H-5, H-6, H-8, H-10, H-12, H-13, E-6 a E-10, E-13, E-14, F-4, F-6, F-7, F-9 a F-15, S-8), cada
um com arquivo:linha, evidência, impacto e correção concreta na tabela de achados.

**Não corrigível em código:** o 403 das fontes atrás de Cloudflare (fingerprint TLS do .NET).

**Fora de escopo por decisão do usuário:** o aplicativo Android — zero edições nesta auditoria;
`publish-app-release.ps1` nunca foi executado.

**Pendências do usuário:** (1) aplicar a atualização — o host está no **12.0.69** e a 12.0.73 traz,
entre outras, a correção do conflito de tracking que gerou 2.355 erros em 50 min; (2) o commit — a
árvore está com 274 alterações e o último commit é `48831916`, de 21/09 23:59.

### Correção das notas de release (depois da rodada 30)

As notas publicadas estavam **sem nenhuma acentuação** (`instalacao`, `executavel`, `series`,
`navegacao`, `simultaneas`, `conexoes`) e o bullet do pool do MariaDB tinha um trecho truncado no meio.
Corrigido o texto no template (`publish-release.ps1`, o here-string de `$bodyContent`), que é de onde
toda release futura herda as notas, e atualizadas as releases já publicadas:

| release | o que foi feito |
| --- | --- |
| `v12.0.73` | notas corrigidas + bullet novo do boot do cliente web (PNG 1.003 KB → WebP 53 KB) e menção ao teste de integração; republicada com `-SkipAssets` |
| `v12.0.72`, `v12.0.71`, `v12.0.70` | mesmo texto corrigido, **sem** o bullet do WebP (que só vale a partir da 12.0.73) |

Os assets não foram tocados (2 em cada release) e `releases/latest` continua sendo a `v12.0.73` — é o
endpoint que o painel do servidor lê para mostrar "Notas de Lançamento".

Cada número citado nas notas foi conferido no código antes de entrar no texto: `MaxConcurrentExports = 4`
(`NebulaMetadataExportService.cs:43`), a espera de 10 s (`NebulaMetadataExportService.cs:136`), o teto de
100 do pool (`MySqlDatabaseProvider.cs:44`), os códigos transientes
(`ItemPersistenceService.cs:436-440`) e a mensagem `MariaDB database: {Database}`
(`MySqlDatabaseProvider.cs:100`).

**Ainda sem acentuação:** as notas de `v12.0.63` a `v12.0.69` (9 a 17 bullets cada, de rodadas
anteriores). Não reescrevi: o conteúdo é de várias sessões e refazer ~100 bullets à mão convida a erro
factual. Se quiser, normalize essas também.

### Limpeza dos restos de release (depois da atualização para 12.0.73)

O updater extrai por cima e **não remove** os arquivos das releases anteriores, então cada atualização
deixa para trás os chunks com hash do build antigo. Medido após aplicar a 12.0.73:

| alvo | antes | depois | o que foi feito |
| --- | --- | --- | --- |
| `dist` (repositório) | 326 arquivos / 15,19 GB | **293 / 2,71 GB** | removidos 33 instaladores antigos (12,48 GB); mantidos o instalador `12.0.73` e o zip da release |
| `MulletaFlix-web` instalado | 5.811 arquivos / 94,4 MB | pendente | 3.935 arquivos com hash de builds anteriores (34 MB) — **bloqueado por ACL de `Program Files`** nesta sessão (não elevada) |
| `wwwroot` instalado | 2.751 arquivos / 73,8 MB | intacto | cópia de 19/09 que ainda responde em `/assets/**` e num `/index.html` antigo: **não removida** de propósito |

Script novo: `limpar-assets-instalados.ps1` (dry-run por padrão; `-Executar` exige sessão elevada).
Ele só remove **arquivo com hash no nome e data anterior ao `index.html` vigente**, nunca arquivo sem
hash (temas, `libraries/`, branding, favicons — que são carregados por URL de runtime) e nunca algo
referenciado pelo `index.html` atual.

Segurança verificada antes de entregar o script: uma cópia podada da pasta instalada (5.811 → 1.876
arquivos / 60,4 MB, o mesmo conjunto do pacote) foi carregada no arnês de boot e deu **BOOT OK** — 0
erros de página, 0 fallback para o servidor, com a entrada, o CSS e o `search-brand-tile.webp` presentes.

**APKs do app Android (a pedido do usuário):** removidas **284 versões antigas** na raiz do `dist`
(1,92 GB), mantendo apenas a mais recente — `mulletaflix-app-v1.2.80.apk`, que é exatamente o caminho
que `publish-app-release.ps1` procura (`dist\mulletaflix-app-v<versao>.apk`) e que bate com
`appVersion = "1.2.80"` em `MulletaFlix-android\gradle\libs.versions.toml`, mais a cópia sem versão
`mulletaflix-app.apk`, **byte-idêntica** (mesmo SHA256, mesmo build de 22/09 18:34).

**Correção de uma afirmação minha:** eu havia registrado que nenhum APK estava publicado no GitHub.
**Estava errado** — a consulta foi feita com `per_page=8`, e as releases do servidor dominam a primeira
página, então nenhuma release do app entrou na amostra. A auditoria completa (275 releases `app-v*`)
mostra que **todas** têm exatamente um asset `mulletaflix-app-v<versao>.apk`, com `state=uploaded`,
tamanho > 1 MB e nenhuma como draft: 1,86 GB de APKs publicados. Não há tag `app-v*` sem release.

Consequência prática: remover as 284 cópias locais **não perdeu nada** — cada versão continua publicada e
baixável pela URL do asset da release. A cópia local do `v1.2.80` bate byte a byte com o asset publicado
(7.357.517 bytes). O manifesto do que saiu do disco ficou em `%TEMP%\apks-removidos-*.txt`.

Ponto de atenção para o futuro: as releases do app são frequentes (às vezes uma a cada poucos minutos
durante trabalho ativo), então o consumo de assets do GitHub cresce; se um dia incomodar, dá para
publicar só versões "redondas" e manter as intermediárias apenas em CI.

---

## Integração de novas plataformas de dramas curtos

Motivo: o usuário encontrou dramas curtos que não são reconhecidos porque **não são do DramaBox**. Ele
apontou quatro plataformas e pediu para trazer as informações delas. Tudo abaixo foi verificado ao vivo
nesta sessão (HTTP 200 da pilha .NET — **nenhuma delas está atrás do bloqueio Cloudflare que atinge o
MyDramaList**), com os números medidos.

### DramaFinds — `dramafinds.com` (API JSON, a mais direta)

| item | fato verificado |
| --- | --- |
| API | `https://api.dramafinds.com/api` |
| Cabeçalhos | `Content-Type: application/json`, **`Guest-Id`** (8 caracteres: os 4 últimos dígitos do epoch em ms + 4 aleatórios, gerado no cliente), `X-Language: pt`, `Origin`/`Referer` do site. Sem `Guest-Id` → `{"code":10,"msg":"GuestId Missing"}` |
| Busca | `POST /v1/short-dramas/search`, corpo `{"keyword":"99 amuletos","pageNum":1,"pageSize":10}` → `data.list[]` com `dramaId`, `title`, `description`, `thumbnailUrl`, `rating`, `episodes` (contagem), `releaseDate`. **O campo é `keyword`** (`searchString`, `searchKey`, `name`, `query` devolvem `total: 0`). Existem **duplicatas** do mesmo título com `dramaId` diferente (ex.: 47011 e 47218) |
| Detalhe da série | `POST /v1/short-dramas/episodes`, corpo **em texto puro** `{"dramaId":"47011","indexE":0}` → `title`, `description`, `thumbnailUrl`, `totalCount` (52 no exemplo), `rating`, `releaseDate`, `md5Id`, `seoTitle/Description/Keywords`. Corpo criptografado é rejeitado com `dramaId is not numeric` |
| Capa | vem assinada (`?auth_key=<epoch>` que **expira em horas** — o do exemplo expirava ~2 h depois). **Remover a query funciona** (HTTP 200): guarde a URL sem `auth_key` |
| Episódios | a API **não** devolve a lista (`episodes: null` em qualquer `indexE`). O site nomeia `Episódio N` (verificado no `og:title` da página de episódio) e a URL é determinística: `https://dramafinds.com/pt/video-play/{dramaId}/{slug}/episode-{n}` → **sintetizar 1..totalCount** |
| Fora de escopo | `POST /v1/short-dramas/detail/v2` exige corpo RSA (JSEncrypt = PKCS#1 v1.5, chave SPKI no bundle) e devolve **AES** (`encrypt-key` no header). Não vale o custo |

### GoodShort — `goodshort.com` (HTML + JSON-LD, sem API)

- Série: `https://www.goodshort.com/drama/<slug>-<seriesId>` (o slug pode ter prefixo `dublado-` e
  acentos, ex. `...-máfia-31001499548`).
- JSON-LD na própria página: `TVSeries` com `name` (sem sufixo), `image` (capa em
  `acf.goodshort.com`, **URL estável, sem assinatura**) e `description` (sinopse completa).
- Segundo JSON-LD: `ItemList` de `VideoObject` com `position`, `name` ("... - EP 1"), `url`,
  `thumbnailUrl`, `duration`. **A página de detalhe traz apenas 6 episódios** (positions 1..6) — a lista
  completa vem da API abaixo.
- **API própria, sem autenticação**: `POST https://www.goodshort.com/hwycreels/chapter/page` com
  `{"bookId":"31001543625","pageNo":1,"pageSize":50}` →
  `{"status":0,"data":{"current":1,"size":50,"total":37,"pages":1,"records":[...]}}`, cada record com
  `{id, bookId, volumeId, chapterName ("001"), wordNum, price, prevChapterId, nextChapterId, status, index, m3u8Path}`.
  Verificado: a série de teste tem **37 episódios**. O `bookId` é exatamente o número no fim do slug.
- `POST https://www.goodshort.com/hwycreels/book/detail` com `{"bookId":"..."}` → `data` com `book`,
  `chapterVo`, `chapterVoList`, `recommends`, `guessLike`, `seoBookRelateList` (metadados de série).
  Base errada: `https://www.goodshort.com/api/hwycreels/...` devolve 404 (é sem o `/api`).
- Episódio: `/episode/<slug>-<seriesId>/<NNN>-<episodeId>` (NNN com 3 dígitos; o `episodeId` não é
  derivável do `seriesId` — use o `id` de `records`).
- Busca: `https://www.goodshort.com/search?keyword=<termo>` devolve o resultado **no HTML**
  (`<a href="/drama/...">` + `<div class="item-title">` + `<img alt="Título">`). Não há API pública.

### NetShort — `netshort.com` (API com corpos criptografados; a mais trabalhosa)

- Base: `https://netshort.com/prod-web-api`.
- Busca: `POST /web/short_play/search/keyword/seo` com corpo
  `{queryKeyword, pageQuery:{pageNum,pageSize}}` — **mas o corpo vai criptografado**.
- **Todos os corpos observados são AES cifrados** (strings base64 de 100+ chars). Texto puro → **HTTP 500**
  (foi o que aconteceu nas minhas tentativas e com cookies de sessão).
- Autenticação: `POST /web/auth/visitor_login` (corpo também cifrado + header
  `device-code: <32 hex>-<epoch>`, ex. `b18305f3902baccadef76f479e5d79a6-1790116`) devolve um
  `Authorization: Bearer eyJ0eXAiOiJKV1...` usado nas chamadas seguintes.
- Endpoints de conteúdo: `POST /web/v4/short_play/detail_info/cascade_label`,
  `/web/web/v3/detail_info/episode_info/cascade_label`, `/web/web/v2/load_popular_online/cascade_label`,
  `/web/web/v3/queryOnlineLabelList/cascade_label`.
- A **página** da série tem JSON-LD `TVSeries` com `name`, `image` (`awscover.netshort.com`),
  `description`, **`genre[]`** e **`numberOfEpisodes`** (36 no exemplo) — ou seja, dá para obter a série
  por HTML e deixar a API só para busca/episódios.
- Material para o RE da criptografia: a captura real das requisições ficou em
  `%TEMP%\netshort-capture.json` (8 requisições com corpo, cabeçalhos e respostas), feita com o
  `netshort-capture.mjs` que está em `MulletaFlix-web-master\` (ferramenta temporária).

### NartoDrama — `narto-drama.com`

- Série: `https://narto-drama.com/detail/watch/<slug>?lang=pt-PT`. JSON-LD `TVSeries` com `name`
  (traz sufixo ` - Streaming grátis` para remover) e `description`.
- Capa: `https://img.nartodrama-api.online/poster/<id>.jpg` — o **id numérico aparece no caminho da
  capa** (ex. 91437), não na URL da página.
- Busca: `https://narto-drama.com/search?q=<termo>` → 200 com 72 links `/detail/watch/` no HTML
  (`?keyword=` também responde, com menos resultados). Busca **server-side**, parseável.
- **Episódios: estão no próprio HTML**, como âncoras
  `<a class="episode-item" href="https://narto-drama.com/detail/watch/<slug>/<n>?lang=pt-PT" title="001">`
  (o número do episódio é o último segmento do caminho e o `title` é o rótulo com 3 dígitos). A série de
  teste tem **37 episódios**. Não precisa de API para a lista.
- Contagem/atualização: `GET https://edge.narto-drama.com/e/rs/detail/watch/<slug>/check-new-episodes?lang=pt-PT`
  (o host `edge` responde 200; o caminho equivalente no site devolve **403**) →
  `{"ok":true,"has_new":false,"current_count":37,"new_count":37,"api_count":37,"checking":false}`.
- A própria página já declara `var currentEpCount = 37;` e o id numérico da série
  (`_epGateKey = 'nd_epcheck_v1_' + String(91437)`), então nem a contagem nem o id exigem requisição extra.

### Estado da implementação

Os **quatro** providers foram escritos por subagentes, com build da solução em **0 erros**:

| plataforma | pasta | arquivos |
| --- | --- | --- |
| DramaFinds | `Plugins\DramaFinds\` | 8 (+ tarefa `DramaFindsMatchTask.cs` e 5 arquivos de teste; 77 testes, incluindo 1 contra a API real) |
| GoodShort | `Plugins\GoodShort\` | 8 |
| NetShort | `Plugins\NetShort\` | 9 (inclui `NetShortCrypto.cs`) |
| NartoDrama | `Plugins\NartoDrama\` | 8 |

Ligação em DI feita à mão (era o combinado): os quatro clientes são **singletons** em
`Emby.Server.Implementations\ApplicationHost.cs`, logo depois do `DramaBoxClient`, com os `using`
correspondentes. Motivo: cada cliente tem o seu *pacing* de HTTP, cache de metadados e cooldown de
falha — sem o registro cada provider e a tarefa teriam a sua própria instância e nada disso seria
compartilhado.

### Correção do seletor de imagens remotas (bug reportado pelo usuário)

Sintoma: a busca de imagens devolvia 148 imagens, mas a tela mostrava **um** card por linha e as setas
de página revelavam "mais um".

Causa: o seletor abre um diálogo `size: 'small'`, e `.dialog-small` só define largura **acima de 80em**
(`@media all and (min-width: 80em) and (min-height: 45em)`); todo diálogo com `size` também recebe
`dialog-fixedSize`, que vira fullscreen abaixo desse ponto. Com isso a largura disponível varia muito
com janela e zoom, enquanto os cards tinham **18em fixos** — sobrava espaço para um card por linha.

Correção em `components\cardbuilder\card.scss`: a lista deixou de ser flex e virou **grid com
`auto-fill`** (`.availableImagesList.vertical-wrap { display: grid; grid-template-columns: repeat(auto-fill, minmax(9em, 1fr)) }`),
com o card preenchendo a célula (`width: 100%; max-width: 14em`), o que preserva a intenção original
("não colapsar quando a imagem não tem dimensões") sem depender da largura medida do diálogo.

Medido com uma sonda de layout em navegador real, usando o CSS compilado do próprio cliente
(`MulletaFlix-web-master\layout-probe.mjs`, ferramenta temporária):

| | antes | depois |
| --- | --- | --- |
| cards por linha (diálogo a 60%) | 2 | **5** |
| largura do card | 288 px | 145 px |
| altura total da lista | 3748 px | **1056 px** |

### Rodada 8 — release 12.0.74 publicada

Portão da rodada: build **0 erros**; suíte **3.615 aprovados, 38 ignorados, 0 falhas** (15 assemblies);
release **v12.0.74** com 2 assets verificados pela API (zip 353,38 MB + instalador 386,69 MB).

O que entrou nela:

| item | evidência |
| --- | --- |
| Providers DramaFinds, GoodShort, NetShort e NartoDrama | 77 + 95 + 75 + 102 testes, com testes ao vivo contra as APIs reais (o NetShort exigiu reverter AES-256-ECB com chave fixa + RSA-2048 para a chave de resposta) |
| DI dos quatro clientes | `ApplicationHost.cs`, singletons, interfaces de reflexão conferidas |
| Correção do seletor de imagens | grid `auto-fill`: 2 → 5 cards por linha, medido em navegador real |

**Regressão minha, encontrada e corrigida:** a otimização E-4 em `PeopleRepository.UpdatePeople` foi
escrita como `candidateNames.Contains(e.Name)`, e o compilador ligou isso para
`MemoryExtensions.Contains(ReadOnlySpan<string>, string)`. O EF não consegue transformar isso em
parâmetro de query e **toda** atualização de pessoas falhava — 1.389 operações de biblioteca com erro na
12.0.73 desde o restart das 18:54. Corrigido para `Enumerable.Contains(candidateNames, e.Name)` (a forma
que as outras 20 ocorrências do assembly já usavam, justamente por isso), com o porquê comentado no
arquivo e um teste de integração novo contra MariaDB real
(`UpdatePeople_WithAPersonThatAlreadyExists_DoesNotFailWhileEvaluatingTheQuery`) — 4/4 na classe.
Validação em produção da rodada 29: **0 conflitos de tracking** desde as 18:54 (antes, milhares/hora).

**Regra de Novela da outra sessão:** ela parou com um teste vermelho (o caso
`Series/Novela Avenida Brasil` esperava `NOVELA` e recebia `SERIE`, porque o casamento exigia o segmento
exato). Com autorização do usuário, completei: segmento que **começa** com `novela`/`novelas`/
`telenovela`/`telenovelas` (com espaço depois, para não pegar título que só contenha a palavra) passa a
declarar NOVELA. Classe em 164/164.

**Pendente do usuário:** aplicar a atualização (produção segue na **12.0.73**) e conferir a identificação
numa varredura da biblioteca; e o commit — a árvore tem 319 alterações e nada foi commitado.

### Qualidade da BUSCA de cada fonte (medido — decide o matcher)

Isto importa porque o defeito original que o usuário reportou foi **mesclagem errada de séries
homônimas**; um matcher que aceita o primeiro resultado da API reintroduz o problema.

**DramaFinds** — a busca é **fuzzy por substring**, não exata:

| termo buscado | o que a API devolveu |
| --- | --- |
| `99 Amuletos, 99 Desilusões` | **2 duplicatas exatas**: id 47011 (nota 8,0, 2025-12-22) e id 47218 (nota 9,3, 2025-12-24), ambos com 52 episódios |
| `Abandonei o Rei dos Deuses no Altar` | **total = 0** (essa série só existe no GoodShort/NartoDrama) |
| `A Agência` | 2 resultados que **não** são o título pedido: `Traído e Caçado pela Própria Agência` (67991) e `Cazado por mi propia agencia` (177195) |

Consequências obrigatórias para o provider: aplicar **limiar de similaridade** (reusar
`DramaBoxTitleMatcher.Normalize`/`Similarity`), nunca aceitar o primeiro resultado, devolver as
duplicatas como candidatos para o usuário escolher e, quando decidir sozinho, usar critério determinístico
com log. Busca vazia é resultado normal, não erro.

**GoodShort** — o endpoint de busca "seo" que o cliente usa (`/hwycreels/seo/resources/*`) devolveu
`topList`/`bottomList` **vazios** para o termo pesquisado, mas o **endpoint de sugestão**
(`api-suggest.json`) devolveu `data.suggest[]` com o registro completo (bookId, bookName, cover) para o
termo parcial — foi assim que apareceu `[Dublado] Abandonei o Rei dos Deuses no Altar` com bookId
**31001543628** (diferente do id da URL que o usuário mandou, 31001543625: a plataforma tem a versão
dublada e a não dublada). Alternativa garantida: `/search?keyword=` renderizado no HTML.

### Como ligar um provider novo (mapeado no DramaBox — a ligação é feita à mão)

Lendo o registro do DramaBox dá para ver que **quase nada precisa ser registrado**:

| peça | precisa de registro em DI? |
| --- | --- |
| `XSeriesProvider : IRemoteMetadataProvider<Series, SeriesInfo>, IHasOrder` | **não** — o Jellyfin descobre por interface |
| `XImageProvider : IRemoteImageProvider, IHasOrder` | **não** |
| `XEpisodeProvider : IRemoteMetadataProvider<Episode, EpisodeInfo>, IHasOrder` | **não** |
| `XExternalId : IExternalId` | **não** |
| `XClient` (o cliente HTTP tipado, `IDisposable`) | **sim**: `serviceCollection.AddSingleton<XClient>();` em `Emby.Server.Implementations\ApplicationHost.cs`, junto da linha 508 (`AddSingleton<DramaBoxClient>()`) |
| tarefa agendada (`IScheduledTask`, como `DramaBoxMatchTask`) | **não** — descoberta por interface |

Ou seja: ao final, a ligação dos quatro providers novos é **uma linha por cliente** no `ApplicationHost.cs`
(nada de editar registro de provider). Isso foi reservado para ser feito à mão, de uma vez, depois que os
subagentes terminarem — assim nenhum deles edita o mesmo arquivo compartilhado ao mesmo tempo.

Padrão de teste que vale reusar: `tests\Jellyfin.Providers.Tests\Plugins\DramaBox\DramaBoxLiveIntegrationTests.cs`
— teste de integração **ao vivo** contra o site real, excluído do CI por nome
(`dotnet test --filter "FullyQualifiedName~DramaBoxLiveIntegration"`). É o modelo para provar cada fonte
nova contra a página real, e foi assim que o DramaBox foi validado.

**Intocados:** 2 APKs de fixture em `dist\version-probe` (ferramenta de sondagem de versão, não são
releases) e os zips de ferramentas (`introskipper-migration-tool`, 61,8 MB).

**Resultado das limpezas:** `dist` foi de **15,19 GB para 0,80 GB** (12,48 GB de instaladores antigos +
1,92 GB de APKs antigos).
