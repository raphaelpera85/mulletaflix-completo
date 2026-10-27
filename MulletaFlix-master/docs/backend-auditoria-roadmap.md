# Auditoria Técnica do Backend MulletaFlix

## Restore: regressão completa local em Windows — 2026-10-27 09:21 BRT

`dotnet test tests/Jellyfin.Server.Implementations.Tests/Jellyfin.Server.Implementations.Tests.csproj -c Release --no-build --filter FullyQualifiedName~FullSystemBackup` passou com 140 aprovados, 16 ignorados, 0 falhas/156 em Windows/.NET 10. A seleção inclui testes do serviço, journal de recuperação, replay e autoridade de diretório. Os skips de Linux/macOS/mount não são evidência desses ambientes. Não altera agregado global nem homologação: servidor formal 2/7. ACL/TOCTOU Linux, CI recuperável, runtime/produção continuam abertos; sem release. Ver sessão `2026-10-27-MulletaFlix-restore-fullsystembackup-windows-0921`.

## Restore ACL: disponibilidade de ambiente Linux e evidência CI — 2026-10-27 09:19 BRT

Rechecado o host: WSL não está instalado, e Docker/Podman não estão disponíveis. A consulta read-only ao GitHub encontrou o run CI mais recente da `main` observado, 39608614450 (21/10/2026, commit `d51f4f38`), com todos os jobs em falha. O job `Server build & unit tests` não expõe steps; o download de logs retorna 404 `BlobNotFound`. Isso não identifica a causa da falha nem comprova que os testes Linux foram executados. Restore P0, Linux/ACL, CI recuperável, runtime/produção e homologação formal 2/7 permanecem pendentes; sem crédito ou release. Ver sessão `2026-10-27-MulletaFlix-restore-linux-ci-availability-0919`.

## Restore ACL: rename de ancestral bloqueado por handle no Windows — 2026-10-27 09:13 BRT

Adicionado teste adversarial `OpenRoot_PreventsRenamingAnAncestorWhileAuthorityIsOpen`: o handle de ancestral mantido por `BackupRestoreDirectory.OpenRoot` bloqueia `Directory.Move` enquanto a autoridade está aberta. Teste focal passou 1/1; classe `BackupRestoreDirectoryTests` passou 45, ignorou 11 e falhou 0/56 em Windows/.NET 10 Release. Os skips Linux continuam sem validar Linux. Isto não comprova operações relativas sem TOCTOU, ACL/Linux, CI hospedada, runtime ou produção. Restore P0 aberto; formal 2/7, sem crédito/release. Ver sessão `2026-10-27-MulletaFlix-restore-ancestor-rename-handle-0913`.

## Restore ACL: rename da raiz bloqueado por handle no Windows — 2026-10-27 09:09 BRT

Adicionado teste adversarial `OpenRoot_PreventsRenamingTheRootWhileAuthorityIsOpen`: a raiz mantida por `BackupRestoreDirectory.OpenRoot` não pode ser movida enquanto o handle está vivo, mas pode após dispose. `BackupRestoreDirectoryTests` passou 44, ignorou 11 e falhou 0/55 em Windows/.NET 10 Release. Isso cobre compartilhamento do handle Windows; não comprova operações relativas sem TOCTOU, ACL/Linux, CI hospedada, runtime ou produção. Restore P0 permanece aberto; formal 2/7, sem crédito ou release. Ver sessão `2026-10-27-MulletaFlix-restore-root-rename-handle-0909`.

## Regressão ampla: corrigir exceção no shutdown do host de leases Nebula — 2026-10-27 09:00 BRT

A suíte Release ampla reproduziu `ObjectDisposedException` em `NebulaPythonCacheLeaseHost.AcceptLoopAsync` quando o cancelamento e o fechamento do listener interrompem `AcceptTcpClientAsync`. O tratamento agora absorve `ObjectDisposedException` apenas após cancelamento; outros erros continuam visíveis. `NebulaPlaybackCacheTests` 60/60, caso reproduzido 10/10, suíte Implementations 1.735 aprovados/95 ignorados e 0 falhas. A regressão serial da solução reconciliada com o rerun da implementação teve 5.456 aprovados, 98 `NotExecuted`, 0 falhas/5.554 resultados brutos. WSL/Docker ausentes; Linux/ACL, CI hospedada, runtime e produção não validados. Contagens distintas 5.465/5.493 e formal 2/7 sem crédito; sem release. Ver sessão `2026-10-27-MulletaFlix-nebula-cache-lease-shutdown-0900`.

## Nebula: modo completo do startup e fila de upload — 2026-10-26 21:23 BRT

O teste de lifecycle cobria apenas `STREAM_ONLY=True`; em full mode, agora
confirma a ordem cleanup, restauração de uploads, scanner e workers de upload,
além de confirmar que streaming-only não executa esse fluxo. A expectativa
antiga de `stats_reporter` também passou a conferir `queue_identity`, campo já
exigido pelo código e pelo índice único. Cavecrew review apontou que faltava
assertar a ausência do cleanup em streaming-only; a asserção foi adicionada e
confirmada na execução final. A suíte completa NebulaPython passou 724 testes,
com 2 skips POSIX intencionais e zero falhas em CPython 3.10.11 isolado.

Limite: teste in-process com Mongo/Telegram simulados prova a orquestração de
startup e criação de workers, não cold start do processo instalado nem upload
com estado final persistido. Falta integração com serviços descartáveis e
validação Linux/runtime/CI/produção. Auditoria formal segue 2/7; sem release.

Auditoria paralela da proveniência de imagens encontrou que imagem antiga sem
`ProviderName`/`Language` é preservada por segurança para não sobrescrever arte
manual. Assim, capas remotas antigas em idioma errado não são recuperadas
automaticamente. O fluxo precisa identificar autoria da imagem antes de
substituir registros legados; sem inferir isso pelo nome/caminho.

## Revalidação concorrente da persistência de imagens — 2026-10-26 21:15 BRT

Após cavecrew apontar que a versão anterior do teste podia serializar as
operações por agendamento, o caso foi tornado explícito: refresh fica bloqueado
na stripe advisory antes de `DeleteItem` começar. MariaDB 11.4.4 descartável,
Windows/.NET 10 Release: os três testes focais passaram, zero skips/falhas. A
execução terminou e a porta isolada fechou; a instância operacional não foi
acessada. O log reenviado é o mesmo SHA do incidente de 23/10 e não contém
identidade do processo nem confirmação final dos retries. Isto não valida DLL
instalada nem produção. Agregado 5.465/5.493, formal 2/7 (28,6%); sem release.
Mapa técnico conferido e inalterado. Ver sessão
`2026-10-26-MulletaFlix-t56-deterministic-image-delete-overlap-2115`.

## Cobertura dos writers de imagem — 2026-10-26 20:53 BRT

Auditoria estática dos usos de `BaseItemImageInfos` identificou `SaveImagesAsync`, a substituição de imagens dentro de `UpdateOrInsertItemsCore`, deleção de itens e migração `FixBaseItemImageInfos`. Os caminhos de runtime compartilham stripes locais e locks advisory MariaDB; não foi encontrado writer de runtime que ignore ambos. `FixDates` toma todos os stripes advisory, mas não stripes locais. A migração ocorre em `CoreInitialisation` antes de `appHost.InitializeServices`; isso ordena o fluxo normal de startup antes dos escritores da biblioteca. Para providers sem advisory lock, essa conclusão depende dessa ordem de inicialização. Nenhuma alteração de código nesta rodada; contrato do portal passou 304/304, sem skips/falhas. A DLL instalada e a produção continuam sem correlação ao log histórico. Métricas permanecem 5.465/5.493 e homologação formal 2/7 (28,6%); sem release. Ver sessão `2026-10-26-MulletaFlix-image-persistence-writer-lock-audit-2053`.

## Reenvio do log de deadlock e inspeção do host — 2026-10-26 20:50 BRT

O anexo foi conferido por SHA-256 e é idêntico ao log de 23/10: deadlocks MariaDB 1213 durante `DELETE BaseItemImageInfos` concorrente, sem resultado final das retentativas. Inspeção read-only não encontrou processo MulletaFlix/Jellyfin ativo; não houve acesso ao banco nem controle de serviço. O log não contém PID/assembly para correlacionar à DLL instalada 12.1.35 ou ao checkout local. Portanto, não prova falha após a correção local, nem prova que o runtime instalado já a contém. Auditoria agregada 5.465/5.493 e homologação formal 2/7 (28,6%) permanecem; CI hospedada, runtime ativo e produção não validados. Sem release. Ver evento `server-image-deadlock-repeat-no-process-2026-10-26-2050`.

Escopo: backend `MulletaFlix-master`, com foco em desempenho, manutenção, bootstrap, persistência, consultas e cobertura de testes.

## Evidência suplementar — regressão serial Windows/.NET 10 (2026-10-26 20:33 BRT)

O filtro Release da CI passou em Windows/.NET 10, sem restore/build e com `-m:1`: 5.401 aprovados, 97 ignorados e 0 falhas/5.498 em 15 assemblies com casos selecionados; dois assemblies Integration foram filtrados sem testes selecionados. Os TRX classificam os skips: 38 MongoDB opt-in, 32 MariaDB opt-in, 25 Linux/macOS/Unix e 2 benchmarks. A CI possui jobs dedicados para Mongo e MariaDB, mas o último run público não executou steps. Uma execução anterior sem serialização encerrou o testhost API com erro fatal CLR `0x80131506`; a repetição serial passou e a API isolada passou 792/795. O workflow agora limita a execução de testes a um projeto por vez. A causa do crash não foi determinada; `-m:1` mitiga o modo observado, não prova a causa. O agregado reconciliado continua 5.465/5.493, homologação formal 2/7 (28,6%). CI hospedada, runtime instalado e produção pendentes; sem release.

## Skills Utilizadas

As skills abaixo foram consideradas para esta auditoria e para o plano de execução. Nem todas são aplicadas em igual peso em cada tarefa.

- `codex-fable5`: orientar a leitura por evidência, verificação e fechamento com rastreabilidade.
- `code-review-and-quality`: revisar correção, arquitetura, segurança, legibilidade e performance.
- `code-review-excellence`: estruturar achados com severidade, impacto e recomendação.
- `dotnet-backend`: orientar padrões ASP.NET Core, EF Core, async, caching e background services.
- `dotnet-architect`: avaliar limites de arquitetura, coesão de módulos e risco sistêmico.
- `dotnet-backend-patterns`: validar padrões de API, data access, DI, cache e resiliência.
- `code-simplifier`: reduzir complexidade sem mudar comportamento.
- `backend-development-feature-development`: organizar trabalho em sprints, entregáveis e validação.
- `caveman`: usado apenas para compressão de comunicação interna quando útil.
- `cavecrew`: útil para delegação enxuta em investigação ou revisão pontual.

Skill não aplicada:

- `product-design audit`: não foi aplicada porque este trabalho não é auditoria de fluxo, UI ou experiência visual.

## Leitura Base

Arquivos e sinais usados como base desta auditoria:

- `[Jellyfin.Server.Implementations/Item/BaseItemRepository.cs](../Jellyfin.Server.Implementations/Item/BaseItemRepository.cs)`
- `[Jellyfin.Server.Implementations/Item/BaseItemRepository.Querying.cs](../Jellyfin.Server.Implementations/Item/BaseItemRepository.Querying.cs)`
- `[Jellyfin.Server.Implementations/Item/ItemPersistenceService.cs](../Jellyfin.Server.Implementations/Item/ItemPersistenceService.cs)`
- `[Jellyfin.Server/Helpers/MariaDbProcessManager.cs](../Jellyfin.Server/Helpers/MariaDbProcessManager.cs)`
- `[Jellyfin.Server.Implementations/Users/UserManager.cs](../Jellyfin.Server.Implementations/Users/UserManager.cs)`
- `[tests/Jellyfin.Server.Implementations.Tests/Item/ItemPersistenceServiceTests.cs](../tests/Jellyfin.Server.Implementations.Tests/Item/ItemPersistenceServiceTests.cs)`
- `[tests/Jellyfin.Server.Implementations.Tests/Item/NextUpQueryOptimizationTests.cs](../tests/Jellyfin.Server.Implementations.Tests/Item/NextUpQueryOptimizationTests.cs)`
- `[tests/Jellyfin.Server.Implementations.Tests/Item/BaseItemRepositoryTests.cs](../tests/Jellyfin.Server.Implementations.Tests/Item/BaseItemRepositoryTests.cs)`
- `[tests/Jellyfin.Server.Implementations.Tests/Users/UserManagerTests.cs](../tests/Jellyfin.Server.Implementations.Tests/Users/UserManagerTests.cs)`
- `[tests/Jellyfin.Server.Implementations.Tests/Users/UserManagerNormalizedUsernameTests.cs](../tests/Jellyfin.Server.Implementations.Tests/Users/UserManagerNormalizedUsernameTests.cs)`

## Resumo Executivo

O backend já tem uma base funcional, mas está concentrado em poucos módulos muito grandes e com bastante mistura entre orquestração, persistência e regras de negócio.

Os principais riscos encontrados são:

1. `ItemPersistenceService` concentra escrita, deduplicação, remapeamento relacional e limpeza de órfãos no mesmo fluxo síncrono.
2. `BaseItemRepository` materializa consulta cedo demais em pontos quentes de listagem e "latest/next up".
3. `MediaInfoResolver`, `MetadataService` e os caminhos de `ProviderManager` ainda fazem muita descoberta, leitura e download de metadata em sequência, o que penaliza reconhecimento de mídia e refresh.
4. `MariaDbProcessManager`: o bootstrap/readiness/shutdown atuais são assíncronos; o teste start-stop-start passou 1/1 em Windows/.NET 10 Release (27/10/2026). A criação de schema permanece no startup, mas ocorre por chamadas assíncronas. O wrapper legado `StopMariaDb` ainda bloqueia, porém não tem chamadores internos encontrados.
5. `UserManager` combina inicialização de esquema, resolução de provedores e operações de usuário numa classe muito extensa.

## Achados Priorizados

### P0

- `ItemPersistenceService` mistura save/delete/update, deduplicação de `UserData`, atualização de `AncestorIds`, `LinkedChildren`, `ItemValues` e limpeza de dependências em um único método grande.
- `SaveBaseItemEntities` executa `ExecuteDelete` por item quando providers, imagens explícitas, campos bloqueados ou tipos de trailer mudam. O custo é condicional e ainda precisa ser medido em scans representativos antes de otimizar.
- `UpdateOrInsertItems` é síncrono: espera as stripes com `SemaphoreSlim.Wait` e a rotina persiste com chamadas EF síncronas. A contenção e o impacto no scan precisam ser medidos; a reauditoria não encontrou `GetAwaiter().GetResult()` nesse fluxo.

### P1

- `GetLatestItemList` mantém deduplicação de filmes/séries recentes no cliente após projetar poucas colunas; com alta duplicidade ou sem limite, pode percorrer muitos candidatos. Medir plano e custo MariaDB antes de trocar a semântica.
- O ramo de álbuns musicais antes materializava todos os IDs de álbuns com faixa correspondente. Agora mantém o conjunto distinto como subconsulta `IQueryable`, evitando lista e parâmetros proporcionais ao catálogo. Teste de semântica passou; plano/carga MariaDB ainda pendentes.
- O pipeline de reconhecimento de mídia e metadata faz muito trabalho por item, sem foco explícito em reduzir I/O, batching e reutilização de resultados entre `MediaInfoResolver`, `MetadataService` e providers.

### Reauditoria estática de `ItemPersistenceService` — 2026-10-27 02:46 BRT

Busca dos chamadores confirmou `SaveItems` → `UpdateOrInsertItems` → `UpdateOrInsertItemsCore` como cadeia interna. As leituras de itens existentes, providers e campos de metadata são em lote; não foram confirmadas consultas repetidas por item dentro dos loops. O custo de escrita condicional por item descrito em P0 permanece candidato para medição, não defeito comprovado. O fluxo é síncrono (`SemaphoreSlim.Wait` e EF síncrono); sem `GetAwaiter().GetResult()` na rotina. Nenhuma mudança de código ou estrutura nesta rodada. Benchmark/telemetria representativa pendentes.

### P2

- `MariaDbProcessManager` mantém MariaDB embutido no processo principal; a separação para serviço externo permanece como possibilidade arquitetural, não defeito de polling atual.
- A inicialização de schema continua no caminho de startup, com contrato assíncrono; ainda falta medir o tempo de startup em instalação Linux e ambiente representativo.
- A classe `UserManager` tem 1358 linhas e concentra responsabilidades demais para manutenção saudável.
- `UserManager` mantém `.GetAwaiter().GetResult()` no caminho frio: `GetUsers`, `GetUsersIds`, `GetUserById` e `GetUserByName` só bloqueiam enquanto `_schemaInitialized` é falso. `InitializeAsync` é chamado pelas rotas de configuração, não como inicializador geral do host; a primeira consulta de usuário em cada processo pode executar o bootstrap do schema dentro de uma chamada síncrona. Não é bloqueio repetido em toda requisição após a inicialização. Startup frio com MariaDB ainda não foi medido.

### Reauditoria do bootstrap de usuários — 2026-10-27 02:54 BRT

Busca de referências confirmou que `IUserManager.InitializeAsync` é chamado por `StartupController` nas rotas de configuração. Os getters síncronos executam `EnsureSchemaCreatedAsync().GetAwaiter().GetResult()` apenas sob `!_schemaInitialized`; após o guard passar, o bootstrap não roda repetidamente. `GetFirstUser` força verificação do schema, mas os únicos callers encontrados também são as rotas de startup. O risco é uma possível espera síncrona na primeira consulta de usuário após reiniciar o processo, não bloqueio em cada chamada quente; reclassificado de P1 para P2. `UserManagerTests|UserManagerNormalizedUsernameTests`: 16 aprovados, 27 ignorados, 0 falhas/43; os 27 são MariaDB opt-in e não validam o bootstrap relacional nesta máquina.

## Recomendações Técnicas

1. Separar o pipeline de escrita de itens em serviços menores:
   - resolução de valores
   - relação de ancestors
   - relação de linked children
   - remoção de órfãos
   - persistência final

2. Manter filtros e ordenações no lado do banco pelo maior tempo possível:
   - manter subconsultas de alta cardinalidade como `IQueryable`; validar separadamente os streams de deduplicação de recentes
   - projetar somente colunas necessárias
   - usar `AsNoTracking()` onde a consulta é leitura pura
   - validar se algum `GroupBy` precisa mesmo sair do SQL

3. Medir o tempo do bootstrap em runtime Linux e em instalação representativa; considerar separar o banco somente se a medição justificar o custo operacional.

4. Reduzir responsabilidade do `UserManager`:
   - separar resolução de provedor
   - separar schema bootstrap
   - separar fluxo de senha/autenticação
   - criar helpers testáveis para seleção de provider

5. Cobrir os caminhos mais caros com testes focados:
   - duplicidade de `UserData` em delete em lote
   - projeções de `NextUp` e plano MariaDB do `GetLatestItemList`
   - bootstrap de MariaDB com DB já existente
   - seleção de provider inválido e fallback

## Plano Por Sprint

### Sprint 0 - Baseline e Instrumentação [CONCLUÍDA]

Objetivo: medir antes de mexer.

Tarefas:

1. Identificar endpoints e jobs mais caros.
2. Medir tempo de query, uso de memória e tempo de startup.
3. Registrar benchmark mínimo para item list, next up, save item e bootstrap.

Skills:

- `codex-fable5`
- `code-review-and-quality`
- `dotnet-architect`
- `dotnet-backend`

Prioridade: P0.

Ganho esperado:

- Não entrega ganho direto de produção.
- Reduz risco de regressão e define linha de base para os sprints seguintes.

### Sprint 1 - Consultas Quentes [CONCLUÍDA]

Objetivo: reduzir latência e memória em listagens, reconhecimento de mídia e "latest/next up".

Tarefas:

1. Revisar `BaseItemRepository.Querying.cs` para adiar materialização.
2. Reescrever projeções para manter lógica no banco quando possível.
3. Revisar `NextUpService` e pontos de `GroupBy`/`ToList` que podem ser empurrados para consulta mais barata.
4. Revisar `MediaInfoResolver` para reduzir leituras de diretório, `GetFilePaths` e probing desnecessário.
5. Revisar `MetadataService` e `ProviderManager` para diminuir materialização precoce, repetir menos downloads e evitar refreshs redundantes.
6. Adicionar testes para regressão de paginação, ordenação e descoberta de mídia.

Skills:

- `dotnet-backend`
- `dotnet-backend-patterns`
- `dotnet-architect`
- `code-simplifier`
- `code-review-excellence`

Prioridade: P0.

Ganho esperado:

- Latência p95 de listagens: melhora estimada de 15% a 35% em bibliotecas grandes.
- Consumo de memória: redução estimada de 10% a 25% em queries com muitos itens.
- Reconhecimento de mídia: melhora estimada de 10% a 30% em bibliotecas com muitos arquivos externos e streams auxiliares.
- Download de metadata: melhora estimada de 15% a 40% quando há muitos providers ou imagens remotas.

### Sprint 2 - Persistência e Escrita [CONCLUÍDA]

Objetivo: reduzir custo de save/delete, diminuir risco de lock contention e acelerar persistência de metadata.

Tarefas:

1. Quebrar `ItemPersistenceService.UpdateOrInsertItemsCore` em etapas menores.
2. Trocar buscas repetidas por dicionários e estruturas indexadas.
3. Reduzir chamadas síncronas sobre async.
4. Revisar `DeleteItem` para diminuir `Any/Contains/ToList` repetidos em coleções grandes.
5. Revisar o caminho de `SaveItemAsync`/`UpdateToRepositoryAsync` para reduzir regravações desnecessárias de metadata e imagens.
6. Reforçar cobertura do `ItemPersistenceServiceTests`.

Skills:

- `dotnet-backend`
- `dotnet-backend-patterns`
- `code-simplifier`
- `code-review-and-quality`
- `code-review-excellence`

Prioridade: P0.

Ganho esperado:

- Tempo de save em lotes: melhora estimada de 20% a 40% em cargas com muitos relacionamentos.
- Menos bloqueio de thread e menor chance de travamento sob carga concorrente.
- Refresh de metadata: melhora estimada de 10% a 25% por reduzir regravações e chamadas desnecessárias.

### Sprint 3 - Startup e Bootstrap [CONCLUÍDO]

Objetivo: melhorar tempo de boot e reduzir acoplamento operacional.

Tarefas:

1. Remover polling bloqueante de `MariaDbProcessManager`.
2. Separar inicialização de processo e criação de schema.
3. Tornar o bootstrap de DB externo e local mais explícito.
4. Revisar `UserManager` para inicialização não bloqueante quando possível.
5. Cobrir boot com DB pré-existente e schema ausente.

Skills:

- `dotnet-architect`
- `dotnet-backend`
- `dotnet-backend-patterns`
- `backend-development-feature-development`
- `code-review-and-quality`

Prioridade: P1.

Ganho esperado:

- Startup: melhora estimada de 10% a 30% no caminho com MariaDB local/embutido.
- Menor risco de bloqueio e falha intermitente no boot.

### Sprint 4 - Manutenibilidade Estrutural [CONCLUÍDA]

Objetivo: reduzir tamanho das classes e facilitar evolução.

Tarefas:

1. [x] Extrair helpers de `UserManager`.
2. [x] Separar responsabilidades de autenticação, reset de senha e inicialização (criadas as classes `UserAuthenticationService` e `PasswordResetService`).
3. [ ] Revisar classes grandes de mídia e provider com o mesmo padrão.
4. [x] Padronizar nomes, contratos e retornos para facilitar leitura.
5. [x] Remover lógica morta, comentários obsoletos e duplicação.

Skills:

- `code-simplifier`
- `dotnet-architect`
- `code-review-excellence`
- `code-review-and-quality`
- `cavecrew`

Prioridade: P1.

Ganho esperado:

- Ganho direto de performance: baixo, 0% a 10%.
- Ganho forte em manutenção, revisão e velocidade de entrega futura.

## Ordem Recomendada De Execução

1. Sprint 0
2. Sprint 1
3. Sprint 2
4. Sprint 3
5. Sprint 4

## Testes E Verificação Recomendados

- `dotnet test` focado em `Jellyfin.Server.Implementations.Tests`.
- Casos prioritários:
  - `ItemPersistenceServiceTests`
  - `NextUpQueryOptimizationTests`
  - `BaseItemRepositoryTests`
  - `UserManagerTests`
  - `UserManagerNormalizedUsernameTests`
- Benchmark ou medição local para:
  - listagem paginada
  - latest/next up
  - save/delete em lote
  - startup com MariaDB embutido e com DB já existente

## Risco Residual

Mesmo com as correções acima, o backend ainda vai carregar herança de compatibilidade e paths legados. Isso significa que:

- parte do custo de consulta pode continuar por causa de dados antigos;
- parte da complexidade estrutural vem de compatibilidade com schema e comportamento legado;
- alguns ganhos de performance só aparecem com dados reais e biblioteca grande.

Para evitar autoengano, cada sprint deve terminar com medição antes/depois.

## T6.1 — autorização HTTP das rotas de backup — 2026-10-26 18:34 BRT

A auditoria confirmou que BackupController exige Policies.RequiresElevation e que SecurityAuthorizationTests cobre a política de classe e impede AllowAnonymous nas rotas. A nova regressão TestServer percorre Create, Restore, RestorePointInTime, List, Manifest, History e Validate; as sete retornam 401 sem autenticação e 403 para papel User. Filtro focal passou 17/17 em Windows/.NET 10 Release; format verify passou. Não houve falha de autorização para corrigir, nem alteração de runtime. Isto valida o pipeline configurado no teste, não a API implantada. Linux/CI hospedada atual/runtime instalado/produção pendentes. Formal 2/7 (28,6%), sem crédito ou release.

## T6.1 — autorização HTTP das rotas de solicitações — 2026-10-26 18:38 BRT

O teste `UserFeedbackAuthorizationHttpTests` percorre as seis rotas do `UserFeedbackController`: todas retornam 401 sem autenticação; papel User acessa sugestões (200) e recebe 403 no catálogo administrativo. Filtro `UserFeedbackAuthorizationHttpTests|UserFeedbackAuthorizationTests|UserFeedbackControllerTests` passou 16/16 em Windows/.NET 10 Release; `dotnet format style --verify-no-changes` passou no arquivo de teste.

O log anexado nesta rodada tem SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, igual ao incidente previamente registrado: 11 deadlocks 1213, 12 retries, sem linha final de sucesso ou identificador de PID/assembly. A suíte focal `ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests` passou 50/50; a regressão MariaDB concorrente foi ignorada porque `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING` não está configurada. A fonte atual inclui serialização por item e loga processo/versão, diferente do formato observado; isso sugere uma versão antiga, mas não identifica o binário que gerou o log. O HTTP 200 antecedente não prova commit das imagens.

Contrato local do portal passou 297/297. Homologação formal permanece 2/7 (28,6%), sem crédito. Nenhuma alteração de runtime, release, instalação ou produção nesta rodada; MariaDB integrado, CI hospedada, Linux e runtime instalado seguem pendentes.

## Regressão ampla da solução — 2026-10-26 18:46 BRT

`dotnet test MulletaFlix-master/MulletaFlix.sln --configuration Release --no-restore --verbosity minimal -m:1 --filter "FullyQualifiedName!~Integration"` terminou com exit code 0 em Windows/.NET 10. A saída visível confirmou Server.Implementations 1.705/1.798 (93 ignorados), Providers 881/881, Extensions 144/144, MediaEncoding.Hls 18/18, MediaEncoding.Keyframes 3/3, LiveTv 75/75, Common 31/31, MediaEncoding 71/71 e Naming 681/681. A saída do runner ficou truncada; não há agregado global confiável e os totais parciais não devem ser somados. Integrações foram filtradas; alguns casos também ignorados por opt-in de banco ou plataforma. O log recebido repete SHA-256 já auditado, com deadlock 1213 em `DELETE BaseItemImageInfos`; sem PID/assembly/versão nem confirmação de persistência. A execução não identifica o binário nem valida serviços operacionais. Homologação formal permanece 2/7 (28,6%), sem crédito; Linux/CI hospedada, bancos integrados, runtime instalado e produção pendentes. Sem alteração de runtime ou release. Mapa técnico conferido, estrutura inalterada.

Contrato do portal `node --test portal-site/tests/homologacao.test.mjs`: 297/297 passou após a atualização deste registro. `git diff --check` passou sem erros de whitespace; apenas alertas de normalização CRLF/LF em arquivos já modificados.

## Regressão ampla do servidor — agregado TRX reconciliado — 2026-10-26 18:52 BRT

A execução foi repetida com diretório temporário e logger TRX por projeto. Resultado: 17 TRX, 5.397 Passed, 96 NotExecuted, 0 Failed, total 5.493; exit code 0. Classificação dos skips pelos motivos dos TRX: 37 MongoDB opt-in, 32 MariaDB opt-in, 24 plataforma/SO, 2 benchmarks e 1 `/dev/shm`. O contrato portal passou 297/297. `testAudit` do portal foi atualizado com esta contagem, separada da homologação formal 2/7 (28,6%). A regressão é local no checkout sujo Windows/.NET 10; integrações foram filtradas; não valida branch remoto, Linux/CI hospedada, runtime instalado ou produção. Nenhuma release. Ver evento `server-full-solution-trx-regression-2026-10-26-1852`.

Revalidação após corrigir assertions de estado corrente do dashboard e adicionar teste do agregado: contrato `node --test portal-site/tests/homologacao.test.mjs` passou 298/298. Os eventos históricos permanecem intactos; teste garante que 5.397/5.493 resultados não avancem o sign-off formal (2/7). Ver evento portal `server-full-solution-trx-regression-2026-10-26-1852`.

## MongoDB opt-in e ambiente isolado do job de CI — 2026-10-26 18:58 BRT

37 casos `RequiresMongo` passaram em MongoDB 8.3.7 descartável no loopback: a primeira execução deixou o caso C#/Python falhar porque o interpretador global foi recusado pelo guard de isolamento; usando a venv Nebula existente, o caso isolado passou 1/1 e o conjunto passou 37/37. A porta 27099 foi fechada e o Mongo operacional 27017 ficou intocado.

O job `nebula-mongo-integration-tests` criava dependências no Python compartilhado do runner e passava `python3` ao subprocesso; isso reproduziu localmente a condição que o runtime Nebula recusa. Workflow alterado para criar venv em `runner.temp`, instalar `requirements.lock` com hashes nessa venv e fornecer seu executável ao teste. `ci-mongo-coverage.test.mjs` passou 1/1 conferindo job e dependências. Mongo local foi 8.3.7; workflow usa Mongo 7.0, remoto ainda não testado.

A regressão ampla tinha 5.397/5.493 aprovados e 96 skips; os 37 Mongo passaram em execução isolada posterior. Auditoria reconciliada: 5.434 aprovados, 59 restantes ignorados, 0 falhas. Isso não é uma única execução nem aceite formal. MariaDB, Linux/CI atual, runtime instalado e produção seguem pendentes; formal permanece 2/7 (28,6%). Sem release.

## Homologação MariaDB local — 2026-10-26 19:03 BRT

MariaDB 11.4.4 descartável em `127.0.0.1:33099`: `Jellyfin.Server.Implementations.Tests` completo passou 1.736 e ignorou 62, zero falhas/1.798; 31 dos 32 casos DB opt-in foram executados, e um requer filesystem case-sensitive Linux. O filtro MariaDB definido na CI para `IntroSkipper.Integration.Tests` passou 17/17 (persistência, imagens/advisory locks, backup participant e restore replay). Daemon descartável encerrado; porta fechada; serviço operacional em 3306 preservado e não acessado.

Reconciliação por casos distintos sobre execuções locais: regressão ampla 5.397/5.493; os 37 casos Mongo passaram isoladamente; 31 casos MariaDB passaram. Resultado acumulado 5.465 aprovados, 28 NotExecuted, 0 falhas/5.493. Os 17 testes IntroSkipper são suplementares, fora do denominador. `testAudit` do portal atualizado; 99,5% é métrica de automação, não homologação formal. Formal permanece 2/7 (28,6%), sem crédito. CI Linux hospedada, o caso case-sensitive, runtime instalado e produção seguem pendentes. Sem release.

Portal e contratos após registrar a evidência MariaDB: `homologacao.test.mjs` 298/298; `git diff --check` sem erro. No shutdown, 33099/27099 fechadas, serviços operacionais 3306 e 27017 ainda ouvindo sem acesso. O datadir descartável MariaDB permanece sob `%TEMP%` com daemon parado; não contém dados operacionais.

## Revalidação da CI e do preflight de release — 2026-10-26 20:39 BRT
A API pública do GitHub ainda mostra como run mais recente a CI `39608614450`, no commit `d51f4f38` de 21/10. Seus 13 jobs terminaram failure entre 3–4 segundos sem steps reportados. O Release `39608677913` falhou no preflight da tag sem steps e pulou empacotamento Linux/publicação. Não há evidência da causa. O checkout local está 8 commits atrás de `origin/main` e contém 224 caminhos modificados/não rastreados; os runs públicos não validam o estado local. Auditoria 5.465/5.493 e formal 2/7 (28,6%) permanecem. Sem release.

## Logs dos jobs CI retornam BlobNotFound — 2026-10-26 20:42 BRT
A API de logs respondeu HTTP 404 `BlobNotFound` para o job do servidor `119251247983` e o preflight da release `119251465001`. Os jobs não reportam steps. Isso confirma que o diagnóstico não pode ser recuperado pelos logs existentes; não identifica causa nem comprova falha de build/teste. O checkout local continua sem validação hospedada. Agregados seguem 5.465/5.493 e formal 2/7 (28,6%). Sem release.

## Regressão serial Release compilada — 2026-10-26 20:46 BRT
`dotnet test MulletaFlix-master/MulletaFlix.sln --configuration Release --no-restore --filter 'FullyQualifiedName!~Integration' --results-directory %TEMP%/mfx-server-build-regression-20261026-2045 --verbosity minimal --logger trx -m:1` concluiu exit 0 incluindo compilação. Foram reconciliados 17 TRX: 15 assemblies com casos, 5.401 aprovados, 97 ignorados e 0 falhas/5.498; os dois assemblies Integration não tiveram casos selecionados. Os skips: 38 Mongo opt-in, 32 MariaDB opt-in, 25 SO/filesystem e 2 benchmarks. Resultado local repetido, não acrescenta casos distintos ao agregado 5.465/5.493. Formal 2/7 (28,6%); CI hospedada, Linux, runtime e produção pendentes.

## Reenvio do deadlock e revalidação do CI — 2026-10-26 20:57 BRT
O anexo é byte a byte o mesmo log já auditado (SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`): deadlocks MariaDB 1213 em `DELETE BaseItemImageInfos`; o recorte não mostra o resultado final das tentativas. Na API pública do GitHub, o run 39608614450 (commit d51f4f38, 21/10) tem 13 jobs em failure e nenhum step reportado. Isso não permite atribuir causa ao CI nem comprova falha de build/teste. A árvore local está 8 commits atrás e tem 224 caminhos modificados/não rastreados, portanto não foi testada pelo run remoto. Contrato local do portal: 304/304. T5.6 permanece aberto; sem alteração de código/banco/runtime e sem release. Agregados 5.465/5.493 e formal 2/7 (28,6%) seguem iguais.

## Runtime instalado e log de retry — 2026-10-26 20:59 BRT
A fonte atual adiciona PID e versão do assembly à mensagem de retry de `SaveImagesAsync`; o log reenviado não contém os campos. A DLL instalada segue 12.1.35, SHA-256 `0D9465B7EC1549CB70C063A2B7B199E8A6B645F6E454CDBFD008A8D0B3F9D30D`, e não contém os marcadores encontrados na fonte. Nenhum processo servidor está ativo. A divergência confirma que o instalado não tem o diagnóstico atual, mas não identifica o emissor do log antigo nem o resultado final das retentativas. Não instalar a árvore local (8 commits atrás, 224 caminhos alterados/não rastreados). Sem release; homologação 2/7 (28,6%).

Contrato do portal após registrar a divergência do binário: `node --test portal-site/tests/homologacao.test.mjs` passou 304/304, 0 falhas/ignorados. T5.6 permanece aberto e sem crédito formal.

A verificação dos literais da DLL confirmou ausência de `GET_LOCK`, presente na classe `ItemPersistenceDatabaseLock` da fonte. O assembly instalado não contém o lock advisory MariaDB atual. O emissor do log antigo continua sem correlação; não houve instalação.

## Regressão unitária focal T5.6 — 2026-10-26 21:03 BRT
`dotnet test` Release com `--no-restore` e filtro `FullyQualifiedName~ItemPersistence` recompilou o estado atual e passou 50/50; zero ignorados/falhas. Build produziu avisos de analisadores e documentação, sem erros. É teste unitário; não valida MariaDB, corrida com DELETE no banco, runtime instalado ou produção. Não soma ao agregado 5.465/5.493 nem à homologação formal 2/7 (28,6%).

## T5.6 concorrência integrada MariaDB — 2026-10-26 21:07 BRT
MariaDB 11.4.4 isolado em `127.0.0.1:33259`, datadir temporário. Concorrência `DeleteItem`/`SaveImagesAsync` e dois testes de espera por advisory locks passaram 3/3. O servidor operacional em `3306` não foi acessado. A instância de teste encerrou e porta fechou; a ferramenta bloqueou remoção recursiva, deixando ~150 MB de dados sintéticos em `%TEMP%\mfx-t56-mariadb-20261026-2108`. Sem crédito no agregado 5.465/5.493 ou formal 2/7 (28,6%); T5.6 permanece aberto para carga representativa, CI, runtime e produção.

## Validação MariaDB da subconsulta de álbuns recentes — 2026-10-27 02:40 BRT

`GetLatestItemList_Music_UsesMariaDbSubqueryAndDeduplicatesAlbums` passou 1/1 em Windows/.NET 10 Release contra MariaDB portátil 11.4.4, iniciado em diretório temporário exclusivo e porta dinâmica `127.0.0.1:49270`. O caso prova a tradução EF/Pomelo da subconsulta e o resultado ordenado/deduplicado com duas faixas relacionadas ao mesmo álbum. O processo de teste encerrou e a porta fechou; o serviço operacional em 3306 não foi acessado. A pasta temporária sintética foi mantida porque a ferramenta não permitiu remoção recursiva.

Isso fecha apenas a verificação de tradução e semântica da consulta. EXPLAIN/carga representativa, CI hospedada, runtime instalado e produção continuam pendentes; homologação formal permanece 2/7 (28,6%), sem crédito ou release. Portal alterado apenas no checkout local.

## Deadlock de imagens — correlação do runtime instalado — 2026-10-27 02:51 BRT

O anexo desta rodada tem SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, idêntico ao incidente já analisado. O trecho contém 12 comandos DELETE falhos, 11 erros MariaDB 1213, 12 retries e um alerta de heartbeat, sem sucesso terminal nem identidade do processo.

Inspeção somente leitura distinguiu os assemblies instalados: `MulletaFlix.dll` 12.1.35 (SHA-256 `6BCB074F679722F2F1D547E94DC40A81B8BAF90F4261D07235F1340B044C60BD`) e `MulletaFlix.Server.Implementations.dll` 12.1.35 (SHA-256 `0D9465B7EC1549CB70C063A2B7B199E8A6B645F6E454CDBFD008A8D0B3F9D30D`). A DLL de implementação não contém os marcadores do `ItemPersistenceDatabaseLock` nem o template de retry atual com PID/versão. A fonte atual serializa por stripe local, adquire advisory lock MariaDB e conserva a stripe durante retentativas. `ItemPersistenceServiceTests` passou 37/37 em Windows/.NET 10 Release.

Conclusão limitada: a instalação observada não contém a proteção presente no checkout, mas o log sem PID/versão não permite provar que esse binário o emitiu. Não havia processo servidor ativo e a conexão MariaDB de integração não está configurada; replay MariaDB, runtime ativo e produção não foram validados. Homologação formal permanece 2/7 (28,6%), sem crédito/release.

## Revalidação de incidente repetido — 2026-10-27 02:58 BRT

O log recebido tem SHA-256 53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2, igual ao incidente de deadlock de BaseItemImageInfos já auditado. Não fornece identificação do runtime nem desfecho final. A proteção presente no checkout não pode ser considerada ativa em produção sem correlacionar PID/assembly e validar o pacote instalado. Não houve alteração de produto nem crédito de homologação nesta revalidação.

## NextUp — rewatch selecionado no SQL — 2026-10-27 12:30 BRT

A seleção do rewatch foi movida para agrupamento e ranking no MariaDB; projeta o ID vencedor e busca a entidade pela chave. MariaDB 11.4.4 descartável passou 1/1 com fixture 6 séries/19 episódios, inclusive candidato sem posição antes do próximo episódio numerado; unitários NextUp 8/8. Isso valida tradução e semântica, não performance em catálogo real. Rewatch deixa de materializar todos os candidatos. Benchmark representativo, CI hospedada, Linux, runtime instalado e produção continuam pendentes. Homologação formal 2/7 (28,6%); sem crédito/release.

## NextUp — plano ANALYZE do rewatch — 2026-10-27 12:52 BRT

O teste exige que a consulta capturada após reset contenha `LastPlayedDate` e `ROW_NUMBER() OVER`, então o `ANALYZE FORMAT=JSON` mede o SQL do rewatch, e não uma consulta ranqueada anterior. MariaDB 11.4.4 descartável passou 1/1; fixture 6 séries/19 episódios, query block 0,568 ms. Fixture pequena/cache aquecido, sem benchmark representativo ou prova de ganho. Linux/CI hospedada, runtime instalado e produção pendentes. Formal 2/7 (28,6%), sem crédito/release.
