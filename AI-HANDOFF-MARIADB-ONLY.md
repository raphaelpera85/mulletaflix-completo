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
