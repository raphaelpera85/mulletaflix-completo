# Testes de integração MariaDB

Os testes de integração que criam ou removem schemas só usam MariaDB quando
`MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING` está configurada. Sem a variável,
os casos que dependem do banco são ignorados; nenhum endpoint padrão é sondado.

O valor é validado antes de qualquer operação destrutiva. São aceitos apenas um
host loopback (`127.0.0.1`, `::1` ou `localhost`) e uma porta diferente de `3306`.
O nome do banco é removido da conexão administrativa; cada suíte cria e remove
apenas seus próprios schemas descartáveis. O fixture web consome a instância
fornecida e não inicia nem encerra serviços MariaDB. Nunca aponte a variável para
produção nem para um serviço compartilhado.

Exemplo PowerShell para uma instância MariaDB descartável local em `33099`:

```powershell
$env:MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING = "Server=127.0.0.1;Port=33099;User ID=test;Password=<senha-local>;CharSet=utf8mb4;"
dotnet test tests/IntroSkipper.Integration.Tests/IntroSkipper.Integration.Tests.csproj --configuration Release
Remove-Item Env:MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING
```

As suítes de Intro Skipper, persistência, migração SQLite→MariaDB, restore de
backup e integração HTTP do servidor usam essa configuração. Inicie antes uma
instância descartável MariaDB local e execute, por exemplo:

```powershell
$env:MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING = "Server=127.0.0.1;Port=33099;User ID=test;Password=<senha-local>;CharSet=utf8mb4;"
dotnet test tests/Jellyfin.Server.Integration.Tests/Jellyfin.Server.Integration.Tests.csproj --configuration Release
Remove-Item Env:MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING
```

As classes `UserManagerNormalizedUsernameTests` e `BillingDatabaseTests` do
projeto `tests/Jellyfin.Server.Implementations.Tests` também exigem a mesma
instância descartável. O helper cria schemas aleatórios com prefixo reservado e
remove cada schema no descarte da fixture. Em 2026-10-19, 30 testes dessas duas
classes passaram em Release contra MariaDB 11.4.4 no loopback/33099; isso não
homologa instalação nem produção.

Na regressão ampla da solution em 2026-10-19, esses 30 casos foram ignorados
porque o processo não recebeu a variável de conexão. A execução ampla totalizou
5.044 aprovados, 64 ignorados e 0 falhas; os resultados MariaDB focais acima
foram executados separadamente com a instância descartável. Não some nem trate
os casos ignorados como aprovação.

### Collation binária de usernames e casing Linux — 2026-10-19

O modelo EF e as migrations mapeiam a entidade para a tabela `Users` (U
maiúsculo). O bootstrap MariaDB deve usar exatamente esse nome em criação,
chaves estrangeiras e consulta a `information_schema`; em Linux com
`lower_case_table_names=0`, `users` e `Users` são tabelas diferentes. O teste
`EnsureSchemaCreated_UsesEfMappedUsersTableWithBinaryUsernameCollations`
confirma a tabela mapeada e as duas collations `utf8mb4_bin` quando executado
com MariaDB. O predicado em `information_schema` usa `BINARY` nos dois lados
para não misturar eventual tabela legada com nome/casing diferente.

Após alinhar o bootstrap à tabela `Users`, a regressão Release da solution
(`FullyQualifiedName!~Integration`) terminou com 5.044 aprovados, 65
`NotExecuted` e 0 falhas em 5.109 casos. A nova regressão MariaDB ficou entre os
não executados por ausência de `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING`; os
30 testes focais anteriormente aprovados não cobriam o casing Linux. O resultado
precisa ser repetido com MariaDB Linux `lower_case_table_names=0` antes de
considerar esta compatibilidade validada. Não remova tabelas `users` legadas
automaticamente: dados e necessidade de migração precisam ser investigados
antes de qualquer operação destrutiva.

### Gate automatizado de casing em Linux — 2026-10-19

O workflow `.github/workflows/ci.yml` inclui `user-mariadb-linux-tests`, em
`ubuntu-latest`, usando MariaDB 11.4 como serviço descartável e porta 33099.
O filtro executa as classes `UserManagerNormalizedUsernameTests` e
`BillingDatabaseTests`; o teste de bootstrap verifica também
`@@lower_case_table_names = 0` antes de confirmar a tabela `Users` e as
collations. Assim, execução em MariaDB case-insensitive é marcada como skip e
não confundida com validação Linux.

O contrato do workflow está em
`tools/release/tests/ci-mariadb-linux-coverage.test.mjs`. O contrato local e o
contrato Mongo passaram 2/2, e o YAML foi parseado com sucesso em 19/10/2026.
O teste real C# continua pendente até a execução do job Ubuntu; nesta máquina,
sem a variável de conexão, os 31 casos ficam `NotExecuted`. Não publicar Release
nem declarar homologação Linux concluída até o resultado remoto verde.

### Cobertura MariaDB dos testes HTTP — 2026-10-20

O assembly `Jellyfin.Server.Integration.Tests` lista 145 casos. Em MariaDB
11.4.4 temporário no Windows, a agregação dos TRX por nome de teste confirmou
145/145 aprovados, zero ignorados, zero falhas e nenhum teste listado sem
resultado aprovado. Os resultados vieram de filtros focais e uma tentativa
integral do assembly; a tentativa integral foi cancelada após 78 aprovações em
17m03s, e os filtros posteriores cobriram os demais nomes. Filtros finais:
`VideosControllerTests` 13/13, `DashboardControllerTests` 12/12,
`MediaStructureControllerTests|PlaystateControllerTests` 13/13 e
`EncodedQueryStringTest|RobotsRedirectionMiddlewareTests|WebSocketTests|ActivityLogControllerTests`
11/11. Testes focais anteriores cobriram upload de imagem, usuários e
biblioteca. Todos os bancos usaram datadir exclusivo, `--no-defaults`, bind
`127.0.0.1`, porta 33099 e foram encerrados; o listener de produção observado
no PID 20736/3306 permaneceu intacto.

Dos 198 casos MariaDB opt-in identificados na regressão ampla, 197 agora
passaram em Windows: os 142 casos `Server.Integration`, 25 casos
`IntroSkipper.Integration` e 30 casos de `UserManagerNormalizedUsernameTests` /
`BillingDatabaseTests`. Um caso permanece ignorado porque exige
`@@lower_case_table_names=0` em filesystem Linux case-sensitive. A compatibilidade
Linux ainda depende do job Ubuntu do CI; estes testes locais não homologam CI,
runtime instalado, produção ou reprodução de mídia real. Não conte a tentativa
integral cancelada como suite green; use a agregação explícita dos TRX focais.

Publicação da evidência: `Mulletaflix-Portal:main` commit
`98041b5b8f7be028e02b8f4ee25dc7a90bf99c83`; a suíte do contrato do portal
passou 168/168. O deployment Vercel Production `dpl_9pRUoKrUEXW9c3Xph5Rsm3MVJRz9`
está READY para esse SHA e o JSON público respondeu HTTP 200 com o evento
145/145. Isso documenta resultados locais, sem afirmar homologação de runtime,
produção, CI Linux ou playback real.

Em 2026-10-19, os testes positivos `GetItem_UserIdAndItemId_Valid`,
`GetIntros_UserIdAndItemId_Valid` e `LocalTrailersAndSpecialFeatures_UserIdAndItemId_Valid`
voltaram a executar na suíte HTTP. A classe passou 16/16 e a suíte completa passou
144/144 contra a instância MariaDB descartável; os resultados não homologam o
runtime instalado ou produção.

A senha deve ser fornecida somente no ambiente local do processo; não a grave
em arquivos versionados ou no vault.

### Regressão inclusiva no Windows — 2026-10-20

`dotnet test MulletaFlix.sln --configuration Release --no-restore --filter
'FullyQualifiedName!~DramaFindsLiveIntegrationTests' --logger trx --results-directory
<scratch> --verbosity quiet` produziu 17 TRX completos. A agregação pelos
resultados individuais (`UnitTestResult`) registrou 5.099 aprovados, 235
`NotExecuted` e 0 falhas em 5.334 casos selecionados. Diferente do gate filtrado
anterior, os assemblies de integração foram executados: `Server.Integration`
passou 3 e ignorou 142; `IntroSkipper.Integration` passou 14 e ignorou 25. Os
resultados TRX não expõem o código final do processo; ele não foi recuperado
após a sessão de execução desaparecer, então não se afirma exit code 0. Os
skips com MariaDB foram explicados pela ausência de
`MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING`; nenhum banco real foi sondado.
`Server.Implementations.Tests` teve 1.508 aprovados/64 ignorados e também incluiu
testes opt-in MariaDB/Mongo. Não foram executados 198 casos MariaDB opt-in e 24
casos Mongo opt-in. A classe `DramaFindsLiveIntegrationTests` foi excluída para
evitar chamada à API externa ao vivo; casos Unix/Linux também ficaram ignorados
no Windows. Esses resultados não comprovam MariaDB/Mongo reais, compatibilidade
Linux nem homologação de instalação/produção. Resultados publicados no portal
em `Mulletaflix-Portal:main`, commit `d1ed0f97f773c570902855182ff9ee464656923b`;
endpoint público confirmou a homologação em 2026-10-20 02:03 (-03).

### Reexecução focal MariaDB de usuários e cobrança — 2026-10-20 07:23 (-03)

`UserManagerNormalizedUsernameTests|BillingDatabaseTests` foi reexecutado em
Release contra MariaDB 11.4.4 em instância temporária exclusiva, com datadir
novo, bind `127.0.0.1` e porta 33099: 30 aprovados, 1 `NotExecuted`, 0 falhas
(31 total; exit 0). O único caso não executado foi
`EnsureSchemaCreated_UsesEfMappedUsersTableWithBinaryUsernameCollations`, que
exige MariaDB Linux com `@@lower_case_table_names=0`. O listener descartável foi
encerrado e a porta 33099 ficou fechada; o listener de produção permaneceu no
PID 20736/3306. A conexão do teste apontou exclusivamente para o loopback/33099.

Esta rodada focal é evidência suplementar: não altera nem se soma ao snapshot
amplo de TRX. Os casos continuam `NotExecuted` naquele snapshot por ausência da
variável opt-in no processo da rodada ampla. Compatibilidade Linux/CI, runtime
instalado, produção e reprodução real não foram homologados; não criar release
com base apenas nesta rodada. Evidência publicada no portal via `main`, commit
`674c799fc0cb7a599e2dac6ac064217d614d1f18`; contrato do portal 174/174. Vercel
Production `dpl_A2iQsE9yjnV8tr4LYqEiqxBcLnUv` ficou `READY` para o SHA publicado,
e o JSON público respondeu HTTP 200 com o evento.

### Classificação dos skips restantes da regressão Release — 2026-10-20 07:29 (-03)

A recontagem dos 17 TRX por `System.IO.File.ReadAllText` (evitando interpretação
de `[` e `]` nos nomes dos arquivos) confirmou 5.071 `Passed`, 67 `NotExecuted`
e 0 `Failed` em 5.138 casos. A partição exata dos 67 é: 31 MariaDB opt-in, 24
MongoDB opt-in, 10 testes específicos de plataforma/filesystem e 2 benchmarks
explicitamente ignorados. Execuções focais separadas já passaram 30/31 MariaDB
(um caso exige Linux) e 24/24 MongoDB; os casos continuam `NotExecuted` no
snapshot amplo, sem soma ou reclassificação.

Os 10 skips de plataforma são: três `AvatarFileStoreTests` (identidade de
entrada Linux, FIFO via O_PATH/procfs e permissões POSIX), um teste de modos
Unix em `ServerConfigurationManagerFilePermissionsTests` e seis casos
`ManagedFileSystemTests` (symlink quebrado, quatro cenários de caminhos Unix e
movimentação cross-filesystem que exige `/dev/shm`). Os dois métodos de
`QueryPerformanceBenchmark` têm `[Fact(Skip = "Benchmark")]` na fonte e não são
testes de regressão executáveis na suíte padrão.

`wsl.exe --status` confirmou que WSL não está instalado; Docker e Podman não
estão disponíveis. A API do GitHub revalidou os runs CI `39037932557` e Release
`39212993752`: falhas sem steps executados; o registro anterior atribui o
bloqueio à alocação de runners/billing. Assim, Linux, a execução desses casos
Unix e o gate remoto seguem pendentes. Nenhum dado de produção foi usado e
nenhuma release foi criada. Homologação publicada em `Mulletaflix-Portal:main`
commit `3b45fb5ecf849d71fe0807d8688559e89cd9ff17`; contrato 175/175. Vercel
Production `dpl_DVwkcQ12uqQCA32nxUcqkPgEsroM` está `READY`; endpoint público
HTTP 200 confirmou o evento e preservou o snapshot amplo 5.099/5.334.
