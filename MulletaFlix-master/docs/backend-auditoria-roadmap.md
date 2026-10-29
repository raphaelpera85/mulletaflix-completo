# Auditoria Técnica do Backend MulletaFlix

Escopo: backend `MulletaFlix-master`, com foco em desempenho, manutenção, bootstrap, persistência, consultas e cobertura de testes.

### Opt-in MariaDB reexecutado — 2026-10-29

- Classificação do TRX amplo atual: 85 ignorados visíveis; 31 são testes opt-in MariaDB, 26 exigem MongoDB descartável, 25 são condicionados pelo sistema operacional/recursos Unix/Linux/macOS e 3 são benchmarks. As categorias de ambiente podem sobrepor-se.
- `UserManagerNormalizedUsernameTests|BillingDatabaseTests` passou **30/31** em Release contra MariaDB 11.4.4 descartável, em loopback `127.0.0.1:53319`; 0 falhas. O caso restante requer `lower_case_table_names=0` em Linux. TRX: `%TEMP%/mflx-mariadb-20261029-audit/results/mariadb-focused-20261029.trx`.
- A instância usou datadir exclusivo e foi encerrada após os testes; porta 53319 confirmada fechada. Produção permaneceu read-only e respondeu HTTP 200 na versão 12.1.45. Nenhuma conexão ao banco operacional foi feita.
- Esta execução focal complementa, sem somar ou substituir, a regressão ampla de 5.377 aprovados/274 ignorados/0 falhas. O opt-in MongoDB foi executado depois e está registrado na seção seguinte; MariaDB Linux, CI hospedada, runtime/instalação e E2E real continuam pendentes. Homologação formal segue 2/7.

### Testes MongoDB opt-in — 2026-10-29

- Os 26 testes `Category=RequiresMongo` passaram **26/26**, sem skips nem falhas, contra MongoDB 8.3.7 temporário em `127.0.0.1:27099`, com datadir novo e isolado. TRX: `%TEMP%/mflx-mongodb-20261029-audit/results/mongodb-optin-20261029.trx` (o runner gerou um TRX por projeto; arquivo final pertence a `Jellyfin.Server.Implementations.Tests`).
- O daemon temporário foi encerrado e a porta 27099 ficou sem listener. Serviço MongoDB existente não foi usado nem alterado; produção do MulletaFlix continuou read-only.
- O resultado reduz a lacuna opt-in local, mas não altera os 274 skips do snapshot amplo, que não foi reexecutado. A validação MongoDB Linux/CI e os gates de runtime/instalação e E2E real seguem pendentes; homologação formal permanece 2/7.

### Correção das rotas de logs do painel web — 2026-10-29

Rotas já declaradas `/dashboard/logs` e `/dashboard/logs/:file` estavam sem tela de lista e com o componente de detalhe ausente. Foram adicionadas telas React com estados de carregamento/erro/retry e chamadas ao SDK. A leitura solicita a cauda do log, com teto de 1 MiB no endpoint existente; clientes legados sem `tailBytes` ainda recebem o arquivo completo. Playwright/Selenium e plano foram alinhados. Revalidação em 29/10: build atual do servidor Release passou sem avisos/erros; regressão de 17 projetos .NET passou 5.377/5.651 (274 ignorados, 0 falhas); Nebula Python 682/682; Vitest web 425/425; `npm run build:check`, build web production e `verify:build` (1.929 artefatos, payload inicial 298,2 KB gzip); `SystemControllerTests` 3/3 e Playwright administrativo/logs 1/1 com build atual em stage isolado; contrato portal 394/394. A tentativa anterior com o stage empacotado 12.1.44 expirou porque seus assets não continham `#logFilesPage`; não foi falha do código atual. Produção, sem escrita, respondeu HTTP 200 em `/System/Info/Public` e `/web/` e informou versão 12.1.45. Ignorados não são aprovados; MariaDB/Mongo opt-in, Selenium, E2E amplo, CI/Linux e integrações/instalação seguem pendentes. Sem crédito formal ou release; homologação formal permanece 2/7.

### Gate de artefatos Windows da release — 2026-10-29

- Foi encontrado um ponto cego entre o staging e a publicação: o ZIP final não era comparado ao stage, e o smoke `Test-CleanInstaller.ps1` existente não fazia parte do workflow antes de publicar.
- O novo `Assert-UpdatePackageIntegrity.ps1` verifica ZIP válido, entradas obrigatórias do servidor/web/tray/Nebula/MariaDB, ausência de duplicados/traversal/caminhos absolutos ou com `:`, e igualdade SHA-256 de todos os arquivos elegíveis contra o stage. O teste PowerShell cobre seis cenários.
- O workflow de release agora executa a validação ZIP e smoke de instalação limpa com diretórios isolados antes do passo de publicação. Testes locais: verificador PowerShell 6 cenários, suíte Windows de packaging 8/8 e contrato Node 7/7; `git diff --check` passou.
- Limite: não foram produzidos ZIP/EXE reais nem executado NSIS/instalação nesta sessão; apenas o runner Windows da CI de release poderá validar esse gate com artefatos reais. Nenhuma release foi criada. MariaDB é o único banco relacional operacional.

### Cobertura do backup MariaDB — estado reconciliado, 2026-10-29

- **Correção do resumo anterior:** a contagem `15/38` e a afirmação de 23 tabelas pendentes ficaram obsoletas após a sequência de fixtures adicionadas entre 05:17 e 05:35 BRT. A auditoria atual da fixture registra linhas semeadas e asserções pós-restore para **38/38 tabelas centrais** declaradas no `JellyfinDbContext`.
- As 7 tabelas auxiliares de domínio (`MovieMetadata`, `MovieUserData`, `SeriesUserData`, `BookUserData`, `Seasons`, `Episodes`, `Programs`) e as 8 tabelas do participante durável IntroSkipper são escopos separados da contagem central.
- `BackupRestoreMariaDbTests` passou 4/5 em Windows/.NET 10 Release contra MariaDB 11.4.4 descartável; o caso restante foi ignorado porque requer MongoDB. Esse resultado valida apenas os cenários executados no fixture. Não prova equivalência com todos os dados de produção, todos os participantes de backup, restore completo multi-backend, Linux ou implantação.
- Próxima validação: executar o exercício completo MariaDB + MongoDB + arquivos em ambiente isolado e obter execução CI hospedada observável. Não reabrir o inventário de 23 tabelas centrais: o registro de 38/38 supersede essa contagem.
- Nenhuma consulta ao banco operacional foi feita para esta auditoria. MariaDB é o único banco relacional operacional; skips continuam pendentes, não aprovados.

### Trickplay: invalidação do manifesto — 2026-10-29

- A implementação de `DeleteTrickplayDataAsync` remove tanto o cache de resoluções quanto o cache do manifesto. A regressão MariaDB foi estendida para buscar o manifesto antes/depois do delete, confirmar que o item excluído some e que outro item fica preservado.
- `TrickplayManagerDeleteMariaDbTests` passou 1/1 contra MariaDB 11.4.13 descartável em `127.0.0.1:60307`; confirmou a remoção do item excluído e a retenção do item preservado em cache/DB. Processo encerrado e porta fechada; o datadir de teste permanece em `%TEMP%` após bloqueio da política à exclusão recursiva. Banco operacional não foi acessado.

### Segurança do atualizador — 2026-10-29

- `Jellyfin.Api.Controllers.UpdateInfoController` agora aceita somente assets HTTPS da release oficial `raphaelpera85/mulletaflix-completo`, rejeita URL arbitrária/traversal e valida o host final após redirecionamentos.
- O token `MulletaFlix_GITHUB_TOKEN` continua restrito à consulta da API GitHub; o cliente de download remove Authorization antes de buscar o arquivo.
- Verificação Windows/.NET 10 Release: `Jellyfin.Api.Tests` 793 aprovados, 3 ignorados, 0 falhas/796; formatação dos arquivos alterados e `git diff --check` passaram. Os 3 ignorados não são aprovados.
- Não executados rede externa, CI hospedada, empacotamento, instalação, Linux ou produção. Homologação formal permanece 2/7; nenhuma release criada.

### Atualizador fail-closed — 2026-10-29

- Regressões confirmadas no helper: após 30 s, a cópia prosseguia mesmo com o PID antigo vivo; `Running` do SCM ou processo vivo eram aceitos sem comprovar `/ready`; exceções durante a preparação/abertura do helper podiam deixar a API em `Applying`.
- Agora o helper aborta antes da primeira cópia se o PID ainda existir; testa `/ready` até 180 s usando porta/prefixo e configuração HTTP/HTTPS; confirma que a porta pertence ao processo iniciado ou ao serviço (incluindo filhos); e mantém snapshot/pacote até a prontidão. HTTPS usa pin do certificado configurado apenas para conexão loopback, inclusive quando o SAN não inclui o IP. Falha após substituição restaura o snapshot e deixa os dados de update disponíveis. Falha ao preparar/iniciar o helper devolve a API para `ReadyToApply`.
- Dez cenários cobrem PID persistente, serviço `Running`/processo vivo sem prontidão, listener conflitante, listener próprio pronto e incapacidade de parar o novo serviço/processo após falha. Dez cenários passam em Windows PowerShell 5.1 e PowerShell 7; testes existentes de cópia e startup passaram; `Jellyfin.Api.Tests` completo 794 aprovados, 3 ignorados, 0 falhas/797, incluindo o retry após falha de lançamento.
- `ApplyUpdateFailClosed.Tests.ps1` foi incluído no CI de packaging e no gate pré-publicação; ambos exercitam também Windows PowerShell 5.1, que o serviço usa em produção.
- Pendências: smoke em instalação Windows isolada, execução do workflow hospedado, runtime Linux e produção. Nenhuma release publicada nem crédito à homologação formal; esta segue 2/7 (28,6%). MariaDB permanece o único banco relacional operacional.

### Lacunas de teste do restore MariaDB — revisão read-only — 2026-10-29

- A suíte atual cobre falha de insert/rollback e recuperação após commit, mas não encerra um processo durante a transação de restore antes do commit. Adicionar exercício isolado com filho morto nessa janela e validar que não sobrevivem linhas parciais nem sidecars/journal indevidos.
- A exportação de bancos primário, domínio e participante ocorre em transações distintas. Falta especificar se backups precisam de consistência point-in-time entre stores; sem esse requisito decidido, isso é lacuna de contrato/teste, não bug confirmado.
- Não houve acesso a banco operacional; análise usa somente fonte e testes versionados. MariaDB é o único banco relacional operacional.

## Skills Utilizadas

### Atualização incremental — 2026-10-29

- Reauditoria do helper Windows `apply-update.ps1` encontrou parada global por nome de processo, em conflito com o escopo já documentado do instalador: somente a instância desta instalação e o PID standalone iniciado pelo helper podem ser encerrados. O bloco global foi removido. O teste de falha de startup agora registra qualquer chamada a `Stop-Process`; antes, o mock não vinculava o processo recebido pelo pipeline e deixava passar o defeito. Windows PowerShell 5.1 e PowerShell 7 passaram os dois testes do helper; contrato de empacotamento Node passou 6/6. Sem build NSIS, CI hospedada ou instalação/produção nesta rodada.
- Esta correção altera somente o fluxo do helper de atualização Windows; não adiciona classe, módulo, API, schema ou dependência. MariaDB permanece o único banco relacional operacional.
- Avançou-se na revisão das classes grandes de mídia/Nebula com um lote verificável de integridade de download, prevenção de duplicidade por identidade e prontidão do feeder. Ver `nebula-strm-download.md` e registro da sessão.
- Evidência local nesta rodada: NebulaPython 672/672 (Python 3.10.11), suíte C# filtrada Nebula 726 aprovados/26 ignorados/0 falhas e `NebulaMediaSuggestionTests` 11/11 (Release/.NET 10). Isso não conclui a revisão completa de providers/classes grandes, integração Mongo/Telegram, runtime, Linux, CI hospedada, medição de carga ou produção.
- MariaDB permanece o único banco relacional operacional; testes desta rodada não conectaram a banco.

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
4. `MariaDbProcessManager` faz bootstrap bloqueante com polling síncrono e inicialização de esquema dentro do caminho de startup.
5. `UserManager` combina inicialização de esquema, resolução de provedores e operações de usuário numa classe muito extensa.

## Achados Priorizados

### P0

- `ItemPersistenceService` mistura save/delete/update, deduplicação de `UserData`, atualização de `AncestorIds`, `LinkedChildren`, `ItemValues` e limpeza de dependências em um único método grande.
- O método `UpdateOrInsertItemsCore` faz vários `ToList`, `GroupBy`, `Distinct`, `First` e consultas repetidas dentro de loops, o que cria custo algorítmico desnecessário em bibliotecas grandes.
- Há `GetAwaiter().GetResult()` em fluxo de persistência, o que aumenta risco de bloqueio de thread e dificulta escalabilidade sob carga.

### P1

- `BaseItemRepository.Querying.cs` usa `AsEnumerable()` em caminhos de consulta que deveriam permanecer server-side por mais tempo.
- Há materialização repetida em `GetLatestItemList`, `GetLatestMusicAlbums` e `GetRecentlyAddedItemIds`, com potencial de ampliar latência e consumo de memória quando a biblioteca cresce.
- O pipeline de reconhecimento de mídia e metadata faz muito trabalho por item, sem foco explícito em reduzir I/O, batching e reutilização de resultados entre `MediaInfoResolver`, `MetadataService` e providers.
- `UserManager` faz inicialização síncrona via `EnsureSchemaCreatedAsync().GetAwaiter().GetResult()` em métodos públicos que podem ser chamados com frequência.

### P2

- `MariaDbProcessManager` mantém bootstrap de MariaDB embutido no processo principal e usa polling bloqueante para aguardar porta.
- A lógica de inicialização de schemas está acoplada à disponibilidade do servidor e não tem contrato assíncrono claro.
- A classe `UserManager` tem 1358 linhas e concentra responsabilidades demais para manutenção saudável.

## Recomendações Técnicas

1. Separar o pipeline de escrita de itens em serviços menores:
   - resolução de valores
   - relação de ancestors
   - relação de linked children
   - remoção de órfãos
   - persistência final

2. Manter filtros e ordenações no lado do banco pelo maior tempo possível:
   - remover `AsEnumerable()` precoce
   - projetar somente colunas necessárias
   - usar `AsNoTracking()` onde a consulta é leitura pura
   - validar se algum `GroupBy` precisa mesmo sair do SQL

3. Trocar bootstrap bloqueante por inicialização assíncrona e isolada:
   - startup da base não deve travar o host se o DB externo já estiver pronto
   - polling deve virar espera com cancelamento
   - criação de schema deve ser idempotente e testável por unidade

4. Reduzir responsabilidade do `UserManager`:
   - separar resolução de provedor
   - separar schema bootstrap
   - separar fluxo de senha/autenticação
   - criar helpers testáveis para seleção de provider

5. Cobrir os caminhos mais caros com testes focados:
   - duplicidade de `UserData` em delete em lote
   - `AsEnumerable()` e projeções de `NextUp`
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

## Auditoria Nebula — 2026-10-28

- Corrigido um risco de integridade em `NebulaDownloaderEngine.DownloadMultipartCoreAsync`: GET sequencial HTTP 200 com corpo vazio e tamanho desconhecido era aceito como download concluído; o caller então podia enfileirar um arquivo de 0 bytes e remover o `.strm` de origem.
- Agora o fluxo lança `IOException` antes da publicação do arquivo. O caller existente registra a falha e retorna antes do enfileiramento e da remoção do `.strm`.
- Evidência: `NebulaDownloaderEngineTests` passou 45/45 em Windows/.NET 10 Release. Dois casos novos exercitam resposta vazia e resposta não vazia sem tamanho conhecido. O teste do fluxo completo que envolve MongoDB e `.strm` não foi executado.

### Startup e integridade da retomada de upload — 2026-10-28

- O log da instalação 12.1.45 encerra com timeout do supervisor em 45 segundos, embora o worker tenha confirmado 28 bots, aberto FTP/HTTP e iniciado uploads. Na candidata 12.1.46, o watchdog aguarda 120 segundos e a prontidão exige que `/health` retorne o PID do processo iniciado. A falha original pode ser explicada pelo limite anterior; o log não contém resposta da sonda para confirmar eventual incompatibilidade de PID.
- O timeout agora registra porta, PID e a última falha de `/health` (conexão, HTTP, payload ou PID), para diferenciar lentidão de processo/sonda incorretos.
- A retomada de uploads grava SHA-256 por parte e só reutiliza uma parte persistida se os bytes locais no mesmo intervalo corresponderem. Partes antigas sem hash são reenviadas. `NebulaDownloaderStartupTests` passou 35/35 e `NebulaUploadEngineTests` 225/225 em Windows/.NET 10 Release.
- Limites: testes locais não validam Telegram/Mongo reais, pacote instalado, Linux ou produção. A candidata não tem assets reconstruídos; CI hospedada do PR #1 está bloqueada por cobrança e a homologação formal continua 2/7 (28,6%).
- A correção entra no candidato 12.1.46 ainda não publicado. Os hashes anteriores dos pacotes foram invalidados; reconstruir após desbloqueio do CI. CI bloqueada por cobrança GitHub; homologação formal permanece 2/7.

## Auditoria de migrações e schema de imagem — 2026-10-28
- Seleção de IDs de migrações ausentes corrigida em `JellyfinMigrationService`; testes focais 5/5.
- A migration `AddImageProviderLanguage` do schema MariaDB alinha colunas de idioma/provedor de imagem ao modelo; teste técnico `CheckForUnappliedMigrations_MySql` 1/1. `MySql` no identificador de teste é legado do provider, não outro banco operacional.
- Testes executados localmente em Windows/.NET 10 Release. Actions run 40752861156 iniciou runners, mas teve falhas de configuração em Linux, venv Python, profundidade do checkout e versão Swift; correções aguardam nova execução. Sem validação de banco em produção; homologação formal 2/7. Ver [[Sessões/2026-10-28-MulletaFlix-update-center-prerelease-1601]].
- Reexecução CI 40752861156 destravou os runners e revelou erros da configuração multiplataforma: caminho `tools/` divergia da pasta `Tools/` e falhava em Linux; testes Python importavam o runtime sem virtualenv; lint web/scan Gitleaks precisavam do histórico/base completa; Swift 5.10 no runner macOS 14 não atende ao manifest Swift 6. Workflows foram ajustados, mas aguardam nova execução para validar.

- Retificação: execução 40753434665 falhou antes de criar jobs por erro de aspas na sintaxe YAML do passo Python; workflow corrigido, parser `js-yaml` e `git diff --check` aprovados localmente. A nova execução ainda precisa validar os gates CI.

### Proveniência de imagens e startup parcial Nebula — 2026-10-28
- `ItemImageProvider` agora copia provider e locale da imagem remota em todos os caminhos de gravação, incluindo arquivos, streams e stubs; o locale é normalizado para minúsculas. `BaseItemMapper` preserva os valores nas duas direções e `SaveImagesAsync` reconhece uma alteração apenas de idioma/provider.
- `main.py` encerra FTP, cliente Mongo e bots e retorna erro se o HTTP de streaming falhar ao vincular a porta, antes da criação da fila de upload.
- Evidência local Release: `ItemPersistenceServiceTests` 26/26 e `ItemImageProviderTests` 54/54; Nebula Python lifecycle 14/14 e `compileall` aprovado. Esse escopo não valida refresh em TMDb ao vivo, todas as capas existentes ou upload em produção.
- Mapa técnico atualizado nesta sessão: o fluxo de proveniência de imagem agora persiste do provider à tabela. Mapa estrutural de módulos, APIs e dependências permanece igual. Homologação formal 2/7; CI remota bloqueada por cobrança; v12.1.46 ainda não publicada.

## Alinhamento do runtime MariaDB — 2026-10-28

- O servidor usa exclusivamente MariaDB. `MySqlConnector` e os nomes `mysql_install_db.exe`, `mysqld.exe` e `mysql.exe` pertencem ao driver/distribuição MariaDB; não configuram nem instalam um servidor MySQL separado.
- Corrigido o descompasso de patch: instalador Windows e pacote portátil agora obtêm e extraem MariaDB 11.4.13; CI usa a tag exata `mariadb:11.4.13`. O artefato oficial Windows foi baixado e seu SHA-256 conferido com o manifesto publicado.
- Evidência: servidor descartável Windows iniciado somente em `127.0.0.1:33159`, `SELECT VERSION()` retornou `11.4.13-MariaDB`; `IntroSkipper.Integration.Tests` passou 47/47, sem skips/falhas. Sintaxe do script PowerShell e `bash -n` via Git Bash passaram.
- O metadado EF `MariaDbServerVersion(11.4.2)` permanece como base de compatibilidade de recursos da série, não como versão do runtime empacotado. O smoke de teste foi encerrado e a porta 33159 fechou.
- Limites: build real dos pacotes, CI hospedada, Linux, runtime instalado e produção não foram validados. Pester 3.4.0 reportou zero testes descobertos e não é contado. Homologação formal permanece 2/7 (28,6%); não houve release.

## Regressão de duplicidade na prateleira de filmes recentes — 2026-10-29

- `BaseItemRepositoryLatestTvShowTests` agora cobre 12 variantes recentes do mesmo filme antes de dois filmes distintos, com limite de três resultados. A consulta deve eliminar duplicatas por `PresentationUniqueKey` e continuar preenchendo o limite com os próximos títulos.
- Evidência Windows/.NET 10 Release: regressão focal 3/3; `Jellyfin.Server.Implementations.Tests` 1.654 aprovados, 82 ignorados, 0 falhas; suíte .NET filtrada 15 assemblies, 5.289 aprovados, 85 ignorados, 0 falhas/5.374; Nebula Python 672/672 e `compileall` passou. Portal 322/322.
- A execução inicial .NET `--no-build` encontrou DLL incorporada desatualizada no teste de empacotamento do updater. Após rebuild Release do projeto API, o teste passou; o resultado inicial não indica falha funcional do código.
- Nenhuma implementação de produção foi alterada nesta fatia. Não valida integração, Linux nativo, CI hospedada, runtime instalado ou produção. Homologação formal segue 2/7 (28,6%); sem release.
- Mapa técnico conferido em 2026-10-29: módulo, API, dependências e fluxo de produção permanecem iguais; a cobertura foi ampliada no teste existente. Ver [[Sessões/2026-10-29-MulletaFlix-recent-movie-dedup-regression-0031]].
### Trickplay: coalescência do cache frio — 29/10/2026

- `TrickplayManager` serializa cache misses por título para resoluções e manifestos, com double-check dentro dos locks.
- Gravação e exclusão de dados mantêm a mesma ordem de locks e invalidam ambos os caches sem permitir repopulação antiga concorrente.
- Focal 2/2; suite Implementations Release 1.656 aprovados/82 ignorados; solução Release 5.291 aprovados/85 ignorados/0 falhas em 15 assemblies. MariaDB-only permanece a premissa operacional.
- Limites: exclusão ainda precisa de integração MariaDB porque EF InMemory não implementa `ExecuteDeleteAsync`; sem medição de carga, CI hospedada, Linux, runtime ou produção. Sem crédito formal ou release.

### Trickplay: exclusão seletiva integrada ao MariaDB — 29/10/2026

- `TrickplayManagerDeleteMariaDbTests` cria schema aleatório com prefixo fixo, testa escrita, aquecimento de cache e `ExecuteDeleteAsync`; confirma exclusão no banco/cache e preservação de outro título.
- A fixture é opt-in por `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING` e recusa host não-loopback ou porta padrão. Execução em MariaDB 11.4.4 descartável: 1/1; schema e datadir temporários limpos. A instância do usuário em 3306 não foi acessada.
- Unidade de cache: 2/2. Sem mudança no runtime nesta rodada, benchmark, CI hospedada, Linux, produção, crédito formal ou release.

### Busca remota: anos e separadores de nomes de arquivo — 2026-10-29

- `ProviderManager.NormalizeRemoteSearchName` agora extrai o ano em nomes com separadores `.` e `_`, normaliza separadores antes de remover tags de qualidade e preserva títulos numéricos puros como `1984` sem inferir ano.
- Testes novos: 5/5; suíte `Jellyfin.Providers.Tests` Release/.NET 10: 836/836 em 3m16s; format check passou.
- Não altera score, API pública, schema ou dependências. Regressão filtrada de solução, busca em provider externo, Linux, CI hospedada, runtime instalado, produção e benchmark permanecem não verificados. MariaDB é o único banco relacional operacional. Sem release e sem avanço formal (2/7).

### Persistência idempotente de imagens durante varredura — 2026-10-29

- `SaveBaseItemEntities` agora busca em lote as imagens dos itens existentes e compara todos os campos persistidos antes de substituir as linhas. Revarreduras com imagens equivalentes preservam os IDs e evitam `DELETE`/`INSERT` e reconstrução desnecessária do índice `(ItemId, ImageType)`.
- A igualdade considera a coleção sem depender da ordem e consome cada correspondência uma única vez; duplicatas de `(ImageType, Path)` com idiomas, providers ou outros metadados distintos não podem ser confundidas. `SaveImagesAsync` usa o mesmo comparador.
- Evidência Windows/.NET 10: `ItemPersistenceServiceTests` passou 28/28. A primeira tentativa MariaDB foi corretamente ignorada sem fixture; depois, o teste de revarredura e alteração de idioma passou 1/1 contra MariaDB 11.4.4 descartável em loopback/porta dinâmica, com IDs preservados no rescan idêntico e idioma atualizado no segundo save. Processo e datadir temporários foram encerrados/removidos.
- Contrato do portal: `node --test tests/homologacao.test.mjs` passou 327/327; evento publicado no `Mulletaflix-Portal/main` em `1daf605c` e será atualizado com a validação MariaDB desta etapa. Agregado amplo do servidor não foi reexecutado; homologação formal segue 2/7 (28,6%).
- Não altera API pública, schema ou dependências. MariaDB é o único banco relacional operacional. Confirmação SQL em instância descartável, medição de carga, CI hospedada, Linux, runtime instalado, produção e release permanecem pendentes; homologação formal segue 2/7 (28,6%).

### Persistência idempotente dos tipos de trailer — 2026-10-29

- `SaveBaseItemEntities` agora carrega em lote os tipos de trailer dos itens existentes e compara a coleção como multiconjunto, sem depender da ordem e respeitando ocorrências duplicadas. Revarreduras equivalentes deixam de executar `DELETE`/`INSERT` nessa tabela.
- `TrailerTypeCollectionsMatch` passou 1/1; classe `ItemPersistenceServiceTests` passou 29/29 em Release/.NET 10. `SaveItems_RescanningTrailerWithSameTypes_PreservesTheStoredTypeSet` passou 1/1 contra MariaDB 11.4.4 descartável; triggers de auditoria provaram zero `INSERT`/`DELETE` no rescan com ordem invertida e gravações presentes quando a coleção mudou.
- `dotnet format whitespace --verify-no-changes` para o projeto de integração e `git diff --check` passaram. O processo MariaDB de teste foi encerrado e a porta dinâmica ficou fechada. Datadirs temporários de teste permanecem em Temp porque a remoção recursiva foi bloqueada pela política local; nenhum processo segue em execução. A base da instalação não foi acessada.
- Progresso publicado no `Mulletaflix-Portal/main`, commit `512a719205aedc5498391d136030cc200fd96e55`; contrato Node 328/328, Vercel Production READY e JSON público HTTP 200 confirmaram este evento concluído sem release/crédito formal.
- Medição/interceptação de comandos SQL, suíte ampliada da solução, Linux, CI hospedada, instalação, produção e release continuam pendentes. MariaDB é o único banco relacional operacional; homologação formal segue 2/7 (28,6%), sem crédito.

### Regressão ampla local e execução por fatias — 2026-10-29

- `dotnet test MulletaFlix.sln --configuration Release --no-restore` concluiu com exit code 0 e nenhuma falha. Entre os assemblies executados: Providers 836/836; Server.Implementations 1.659 aprovados, 83 ignorados, 0 falhas; API 774 aprovados/3 ignorados; IntroSkipper Integration 21 aprovados/29 ignorados.
- `NebulaPython\.venv\Scripts\python.exe -m pytest tests -q`, executado a partir de `Tools/NebulaPython`, passou 672/672 em Python 3.10. Uma tentativa inicial na raiz do repositório falhou na coleta por import path (`ModuleNotFoundError: tools`); a chamada documentada no diretório correto passou.
- Os testes MariaDB/Mongo opt-in e testes de sistema Linux/macOS que foram ignorados não contam como aprovados nem como homologação. A rodada não conectou aos bancos instalados, não executou runtime/produção e não altera os gates formais.
- Ciclo rápido durante correção: usar o assembly e filtro da área; exemplos: `dotnet test tests/Jellyfin.Server.Implementations.Tests/Jellyfin.Server.Implementations.Tests.csproj --no-restore -c Release --filter FullyQualifiedName~ItemPersistenceServiceTests` (29/29 em ~8s, com build) e `python -m pytest tests -q` a partir de `Tools/NebulaPython` (672/672 em 12s). Rodar solução completa como gate de integração antes da homologação, não a cada edição pequena.
- A solução completa levou vários minutos; Providers consumiu 3m27s, enquanto as fatias focais foram muito mais rápidas. Investigar testes de Providers que fazem I/O/rede e introduzir categorização/opt-in de testes externos é pendência de otimização; não alteramos expectativas funcionais apenas para reduzir tempo.
- Tentativa de reexecutar todo `IntroSkipper.Integration.Tests` com MariaDB opt-in foi bloqueada pela política local antes do início de `dotnet test`; não houve resultado e os casos permanecem não executados. A instância MariaDB descartável foi encerrada e a porta ficou fechada.
- Contrato do portal passou 328/328 e a página recebeu a evidência deste snapshot, sem crédito formal. MariaDB é o único banco relacional operacional. Homologação formal segue 2/7; sem release.

### Diagnóstico de lentidão da fatia Providers — 2026-10-29

- A execução lenta anterior usava o filtro parcial `FullyQualifiedName!~DramaFindsLiveIntegration`, que ainda incluía quatro classes de testes `*LiveIntegrationTests` com acesso externo/timeouts. A tentativa foi interrompida ao identificar isso; não foi contabilizada como resultado.
- Repetindo com o filtro amplo adotado pela CI, `FullyQualifiedName!~Integration`, Providers Release/.NET 10 passou 823/823 em 6 segundos (TRX `providers-no-integration-20261029.trx`). Os testes externos ficaram fora; isso não os valida.
- A execução completa anterior continua exit 0; skips opt-in/plataforma seguem sem crédito. A causa do atraso no comando filtrado foi a inclusão acidental de integrações externas, não lentidão dos testes locais da fatia.

### Nebula: sondagem de espaço fail-closed — 29/10/2026

A fila do feeder deixa de assumir que há espaço quando `disk_usage` falha. Ambos os workers aguardam nova sondagem em erro de I/O/permissão. O downloader sequencial sem `Range` agora valida capacidade antes de iniciar e a cada bloco, reservando o tamanho conhecido quando `Content-Length` existe. Python 3.10: 674/674, `compileall` e `git diff --check` aprovados. Não executados rede real, runtime instalado ou produção; sem crédito formal. MariaDB permanece o único banco relacional operacional.

### Regressão segmentada do servidor — 29/10/2026

- `Jellyfin.Server.Implementations.Tests`: 1.659 aprovados, 82 ignorados, 0 falhas; `Jellyfin.Api.Tests`: 774 aprovados, 3 ignorados; `Jellyfin.Server.Tests`: 91/91; `Jellyfin.Providers.Tests`: 823/823; `Jellyfin.MediaEncoding.Tests`: 71/71. Todos em Windows/.NET 10 Release, sem filtro externo além de `FullyQualifiedName!~Integration` quando aplicável.
- `IntroSkipper.Integration.Tests`: 21 aprovados, 29 ignorados, 0 falhas; sem endpoint MariaDB opt-in, os casos integrados reais não foram executados.
- Nebula Python 3.10: 674/674; contrato do portal: 331/331 após incluir esta evidência; contratos de release/packaging: 26/26.
- Builds .NET disparados em paralelo disputaram assemblies intermediários e falharam com `CS2012`; a repetição serial dos projetos alterados passou. A tentativa de solução completa ficou sem progresso visível e foi interrompida; não há resultado agregado novo. O assembly MediaEncoding passou isoladamente 71/71 em 1,8 s.
- Skips por Linux, MongoDB/MariaDB opt-in e plataforma continuam pendentes, não aprovados. Não executados neste ciclo: CI hospedada, Linux nativo, runtime instalado, instalação, produção, rede real ou gate formal. O agregado publicado continua 5.291/5.376 e a homologação formal 2/7 (28,6%); sem release.
- MariaDB é a única base relacional operacional.

### Pessoas duplicadas: cobertura de leitura única — 29/10/2026

- `PeopleValidationTask` já materializa os grupos duplicados uma vez para evitar repetir o `GROUP BY` em cada lote. Foi acrescentado teste opt-in MariaDB com 101 grupos (cruza o limite de 100), valida remoção das duplicatas e conta comandos de leitura do contexto de catálogo; a expectativa é uma consulta agrupada.
- O filtro focal `PeopleValidationTask_QueriesDuplicateGroupsOnceAcrossBatchBoundary` passou 1/1 em MariaDB 11.4.13 descartável, Windows/.NET 10 Release, loopback `127.0.0.1:60307`. Confirmou uma linha por cada grupo, uma única leitura agrupada do catálogo e uma chamada de validação; os registros criados foram removidos pelo próprio teste.
- A limpeza usa token independente limitado a 10 segundos para não deixar registros no schema de teste quando o token da tarefa é cancelado. O teste não simula uma corrida em que outra conexão impede exclusão; cobre o custo da releitura e o limite do lote.
- O processo MariaDB foi encerrado e a porta fechada; o datadir descartável permanece em `%TEMP%` porque a política bloqueou a remoção recursiva. Não houve acesso à base operacional. MariaDB é a única base relacional operacional. Sem mudança no código de produção, arquitetura ou gates de homologação; formal segue 2/7 e não há release.

### Regressão segmentada adicional — 2026-10-29

- `Jellyfin.Server.Implementations.Tests`: 1.659 aprovados, 82 ignorados, 0 falhas/1.741 em 1m13s (`FullyQualifiedName!~Integration`).
- `Jellyfin.Server.Tests`: 91/91; `Jellyfin.Providers.Tests`: 823/823; `Jellyfin.MediaEncoding.Tests`: 71/71. Essas três suítes totalizaram 985 aprovados, 0 ignorados, 0 falhas.
- Esses resultados são execuções locais por assembly e não atualizam a matriz agregada já publicada. MariaDB/MongoDB integrados, Linux nativo, CI hospedada, runtime instalado e produção continuam fora do escopo validado.

### Testes HTTP de servidor com MariaDB isolada — 2026-10-29

- A execução opt-in revelou que o fixture criava um schema exclusivo do IntroSkipper, mas o plugin mantinha o nome operacional fixo, causando negação de acesso. O plugin passou a aceitar o nome de teste somente no ambiente `Testing`, com prefixo e caracteres validados; em outros ambientes segue usando `mulletaflix_introskipper`.
- Foi acrescentada à CI principal uma job Ubuntu com MariaDB 11.4.13 descartável para executar a suíte inteira `Jellyfin.Server.Integration.Tests`; um contrato Node verifica que a job e a suíte permanecem ligadas ao fluxo.
- Windows/.NET 10, MariaDB 11.4.13 isolada: testes focais 2/2 e suíte completa 146/146 em 1m54s. Build focal e contrato Node 2/2 aprovados. MariaDB foi encerrada e a porta 60307 fechada; datadir temporário preservado.
- `dotnet format --verify-no-changes` no projeto integral reporta problemas de charset/imports em diversos arquivos; a factory alterada foi formatada focalmente. CI Linux hospedada, homologação formal, runtime instalado, produção e release seguem pendentes. MariaDB é a única base relacional operacional; nomes `MySql*` permanecem por compatibilidade do driver/provedor.

### Fixture MariaDB: teardown fail-loud — 2026-10-29

A auditoria adversarial da fixture de integração HTTP encontrou que falhas em `DROP DATABASE`/`DROP USER` eram engolidas, podendo deixar recursos de teste e ainda reportar suíte verde. A limpeza tenta agora cada instrução e falha explicitamente se algo não for removido. Dispose do host e cleanup do banco são independentes, e a factory restaura variáveis de ambiente em `finally`. O teste de startup também preserva a falha original da operação quando Dispose falha.

Validação: `Jellyfin.Server.Integration.Tests` 151/151 em Windows/.NET 10 Release com MariaDB 11.4.4 descartável (1m55s); zero schemas e contas de teste ao final; servidor de banco parado, porta 60416 fechada. CI contract Node MariaDB: 2/2. A CI usa MariaDB 11.4.13; paridade exata de versão e execução Linux hospedada continuam pendentes. Nenhum crédito formal, release ou teste de produção; homologação formal 2/7. MariaDB permanece o único banco relacional operacional.

### Fixture MariaDB IntroSkipper — 2026-10-29

- A fixture agora usa schema único por instância, aguarda a inicialização assíncrona compartilhada antes do teardown e tenta independentemente Dispose, DROP e restauração do override de ambiente.
- Evidência final: `IntroSkipper.Integration.Tests` 55/56, 1 ignorado, 0 falhas, com MariaDB 11.4.4 descartável; nenhum schema `mfx_intro_test_*` nem conta `mfx_test_*` restou. Processo encerrado e porta 60419 fechada. Linux hospedado e paridade com a MariaDB 11.4.13 da CI seguem pendentes.
- Mudança restrita a fixture e testes. Sem alteração em produção, release, gates formais ou auditoria agregada.

### Preferência global de idioma para imagens e inicialização do datadir MariaDB — 2026-10-29

- Refresh de imagens agora resolve idioma nesta ordem: item, pais, pasta da coleção, opções da biblioteca e configuração global do servidor. O fallback de configuração tolera ConfigurationManager ainda não inicializado em testes e ferramentas.
- Teste de regressão prova preferência global pt-BR com item sem idioma e resposta do provider listada em inglês primeiro. Jellyfin.Providers.Tests passou 824/824 em Release/.NET 10 com filtro FullyQualifiedName!~Integration.
- Start do MariaDB embarcado agora falha claramente se datadir novo/vazio não tiver mysql_install_db.exe; falha também se o processo inicializador não puder ser iniciado. Datadir existente mantém o caminho atual. Jellyfin.Server.Tests passou 95/95. git diff --check e format focais passaram.

### Confirmação MariaDB-only e referências técnicas — 2026-10-29

- A premissa operacional é MariaDB como único banco relacional. CI e testes opt-in usam imagens/variáveis MariaDB; não há serviço ou endpoint de servidor MySQL separado configurado.
- Corrigidas três descrições que chamavam a fixture de “MySQL/MariaDB” ou sugeriam fallback operacional MySQL. O código mantém os nomes de compatibilidade `MySqlConnector`, `MySqlDatabaseProvider` e `MulletaFlix-MySQL`; esses identificadores não indicam outro servidor.
- Busca direcionada por referências operacionais ambíguas não encontrou mais casos. Nenhuma lógica de runtime ou dependência mudou; testes .NET não foram reexecutados por serem alterações de comentário/documentação.
- Reexecução da suíte Nebula após as clarificações: 681/681 em 7,37 s; `compileall -q main.py tools tests` passou. O ambiente usa mocks e fixtures locais.
- Limites: refresh de capas em fonte real/TMDb, integração MariaDB de SaveImagesAsync, CI/Linux hospedada, pacote instalado e produção ainda não validados. A job CI IntroSkipper foi ampliada para rodar o projeto inteiro com MariaDB e MongoDB; o round-trip de SaveImagesAsync foi acrescentado. Teste local do projeto passou 26/57 e ignorou 31 cenários dependentes de serviços; o contrato CI passou 3/3. Pendem execução hospedada Linux, matriz completa das tabelas do manifesto de backup, startup aguardando MariaDB existente sem prontidão SQL e smoke do ZIP/instalador gerado. MariaDB é o único banco relacional operacional. Formal 2/7; sem release.

Pendências da varredura desta sessão (sem alterar a contagem de checklists do portal): executar a suíte IntroSkipper ampliada no CI Linux; ampliar round-trip para todos os participantes/tabelas do manifesto MariaDB; executar o novo round-trip SaveImagesAsync em MariaDB descartável; cobrir espera por instância MariaDB existente e ainda não pronta; validar o ZIP gerado pelo workflow e decidir se NSIS é artefato obrigatório da release.

### Falha transitória no CREATE TRIGGER e reexecução — 2026-10-29 05:12 BRT

Uma execução isolada falhou antes das asserções quando `CREATE TRIGGER` tentou gravar um arquivo temporário `.TRN~` e MariaDB retornou Errcode 2 (“No such file or directory”). O log do mesmo processo também registra “ready for connections”; portanto, a causa não foi estabelecida e não é atribuída à prontidão SQL. Uma reexecução após `mariadb-admin ping` passou 1/1 e execuções posteriores da classe também passaram. A ocorrência inicial permanece registrada sem crédito de aprovação. Nenhum banco operacional foi acessado. Uma auditoria adicional do próprio fixture confirmou `UserLicense`, `TrickplayInfo` e `MidiaStorageOnlineMediaMetadata`, corrigindo a cobertura para 13/38, 25 pendentes. Formal 2/7; sem release. Portal Git `main`: commit `fdce0608418844d06ae9fbb52c5b8fd0da2239ba`, contrato 346/346.

### Round-trip de imagem e segmento de mídia — 2026-10-29 05:17 BRT

ImageInfo e MediaSegment foram adicionadas ao round-trip do fixture; após restaurar, o teste compara caminho/data da imagem e item/tipo/limites/provedor do segmento. O teste focal passou 1/1 e BackupRestoreMariaDbTests passou 4/5, 1 ignorado (MongoDB), 0 falhas contra MariaDB 11.4.4 descartável na porta 51432; processo encerrado. Cobertura central: 15/38, com 23 pendentes. Portal Git main: fdce0608418844d06ae9fbb52c5b8fd0da2239ba, contrato 346/346. Sem produção ou release; formal 2/7.

### Round-trip de tabelas de cobrança — 2026-10-29 05:25 BRT

`BackupRestoreMariaDbTests` foi ampliado para semear e validar `PricingPlans`, `DiscountCoupons`, `PaymentGatewayConfigs` e `PaymentTransactions` após restauração. As transações cobrem relação obrigatória com usuário/plano e os cenários `CouponId` preenchido e nulo; a configuração de gateway usa apenas valores sintéticos. A classe passou 4/5, 1 ignorado (caso opt-in MongoDB), 0 falhas em Windows/.NET 10 Release contra MariaDB 11.4.4 descartável em `127.0.0.1:55871`; o processo encerrou normalmente. `dotnet format whitespace --verify-no-changes` no arquivo e `git diff --check` passaram. Nenhum banco operacional foi acessado.

A contagem central reconciliada agora é 22/38 tabelas com linha semeada e assertion pós-restore; 16 seguem sem round-trip de linha. As 7 tabelas auxiliares de domínio e as tabelas persistentes do participante IntroSkipper são acompanhamentos separados. A matriz ampla 5.291/5.376 não foi recalculada; homologação formal permanece 2/7, sem release. MariaDB é o único banco relacional operacional.

### Round-trip de item central e dados de usuário — 2026-10-29 05:28 BRT

O fixture agora também semeia `BaseItems` e `UserData` relacionados a um usuário e valida os campos do item e os dados de reprodução/favorito após o restore. `BackupRestoreMariaDbTests` passou 4/5, 1 ignorado por requerer MongoDB, 0 falhas em Windows/.NET 10 Release contra MariaDB 11.4.4 descartável em `127.0.0.1:60104`; processo encerrou normalmente. `dotnet format whitespace --verify-no-changes` focal e `git diff --check` passaram. Nenhum banco operacional foi acessado.

A cobertura central reconciliada agora é 24/38 tabelas, com 14 pendentes; as seis novas desde o inventário anterior são as quatro tabelas de cobrança, `BaseItems` e `UserData`. A matriz ampla 5.291/5.376 não foi recalculada; homologação formal segue 2/7, sem release. MariaDB é o único banco relacional operacional.

### Round-trip de AttachmentStreamInfo — 2026-10-29 05:30 BRT

Acrescentada uma attachment de stream associada ao `BaseItemEntity` da fixture. Após restore, as assertions conferem chave composta (`ItemId`, `Index`), codec, codec tag, comentário, filename e MIME type; isso também exercita a relação por convenção EF, não explicitada no fluent config. `BackupRestoreMariaDbTests` passou 4/5, 1 ignorado (MongoDB), 0 falhas em Windows/.NET 10 Release contra MariaDB 11.4.4 descartável em `127.0.0.1:60837`; processo encerrou normalmente. Format whitespace focal e `git diff --check` passaram. Nenhum banco operacional foi acessado.

Cobertura central agora 25/38, 13 pendentes. Esta rodada confirma o vínculo por convention no modelo configurado; não amplia homologação formal (2/7) nem recalcula matriz ampla (5.291/5.376). Sem release. MariaDB é o único banco relacional operacional.

### Round-trip de relações de metadados base do item — 2026-10-29 05:32 BRT

A fixture inclui `BaseItemProviders`, `BaseItemImageInfos`, `BaseItemMetadataFields` e `BaseItemTrailerTypes`, todas relacionadas ao `BaseItemEntity`. O restore é verificado por identificadores, provedor/valor, imagem/idioma/dimensões/blurhash, e tipos de metadado/trailer. `BackupRestoreMariaDbTests` passou 4/5, 1 ignorado por depender de MongoDB, 0 falhas em Windows/.NET 10 Release contra MariaDB 11.4.4 descartável em `127.0.0.1:50413`; encerramento normal. Format whitespace focal e diff check passaram; nenhum banco operacional foi acessado.

Cobertura central: 29/38 tabelas com linhas e assertions pós-restore; 9 pendentes. Formal 2/7 e matriz ampla 5.291/5.376 permanecem inalterados; sem release. MariaDB é o único banco relacional operacional.

### Round-trip de capítulos, streams e keyframes — 2026-10-29 05:34 BRT

Adicionadas linhas dependentes de `BaseItems` para `Chapters`, `MediaStreamInfos` e `KeyframeData`. As assertions pós-restore cobrem chave composta/índice e imagem do capítulo; codec, idioma, canais, bitrate, resolução, perfil e cores do stream; e duração/lista de keyframes. `BackupRestoreMariaDbTests` passou 4/5, 1 ignorado por requerer MongoDB, 0 falhas em Windows/.NET 10 Release contra MariaDB 11.4.4 descartável em `127.0.0.1:53605`; processo encerrou normalmente. Format whitespace focal e diff check passaram. Nenhum banco operacional foi acessado.

Cobertura central 32/38; 6 tabelas pendentes. Homologação formal 2/7, agregado 5.291/5.376 sem recálculo e sem release. MariaDB é o único banco relacional operacional.

### Cobertura de linha completa das 38 tabelas centrais — 2026-10-29 05:35 BRT

A fixture adiciona dados e asserções de restore para `AncestorIds`, `LinkedChildren`, `ItemValues`, `ItemValuesMap`, `Peoples` e `PeopleBaseItemMap`, com dois BaseItems e vínculos pai/filho, pessoa/obra e valor/obra. `BackupRestoreMariaDbTests` passou 4/5, 1 ignorado (caso que exige MongoDB), 0 falhas em Windows/.NET 10 Release contra MariaDB 11.4.4 descartável em `127.0.0.1:57572`; instância encerrada normalmente. `dotnet format whitespace --verify-no-changes` focal e `git diff --check` passaram. Nenhum banco operacional foi acessado.

A auditoria da fixture agora encontrou dados e assertions para as 38/38 tabelas centrais declaradas no `JellyfinDbContext`; não há tabela central pendente neste inventário. Esse resultado não significa que todas as linhas de produção foram exercitadas, nem cobre outros participantes/bancos/arquivos. As tabelas auxiliares de domínio e IntroSkipper permanecem contagens independentes já registradas. Agregado 5.291/5.376 não recalculado; homologação formal 2/7 e sem release. MariaDB é o único banco relacional operacional.

### Regressão unitária do subsistema de backup/restore — 2026-10-29 05:37 BRT

`dotnet test tests/Jellyfin.Server.Implementations.Tests/Jellyfin.Server.Implementations.Tests.csproj --configuration Release --no-build --no-restore --filter FullyQualifiedName~FullSystemBackup --verbosity quiet` passou 154, ignorou 16 por plataforma/ambiente, falhou 0 (170 casos) em Windows/.NET 10 Release. Os ignorados não contam como aprovados; a bateria não acessou banco operacional nem valida restore de todas as linhas de produção. Combinada ao round-trip em MariaDB descartável, esta é uma verificação unitária adjacente, não homologação total. Homologação formal segue 2/7, matriz 5.291/5.376 sem recálculo, sem release. MariaDB é o único banco relacional operacional.

### Suíte completa Server Implementations — 2026-10-29 05:39 BRT

`dotnet test tests/Jellyfin.Server.Implementations.Tests/Jellyfin.Server.Implementations.Tests.csproj --configuration Release --no-build --no-restore --verbosity quiet` passou 1.659, ignorou 83 por requisitos/platforma/opt-in, falhou 0 (1.742 casos) em Windows/.NET 10 Release, 1m12s. Isto amplia a verificação do backup para o projeto inteiro, mas skips continuam pendentes e a suíte não valida todos os serviços, Linux, instalação ou produção. Nenhum banco operacional foi acessado nesta execução. Homologação formal 2/7, agregado 5.291/5.376 sem recálculo, sem release. MariaDB é o único banco relacional operacional.

### Matriz de testes da solução recompilada — 2026-10-29 05:44 BRT

`dotnet test MulletaFlix.sln --configuration Release --no-restore --filter FullyQualifiedName!~LiveIntegrationTests --verbosity quiet` recompilou os projetos e terminou com exit code 0; nenhum teste falhou. Vários projetos foram reportados individualmente pelo runner. Exemplos: Server.Tests 95/95; Server.Integration.Tests 8 aprovados/143 ignorados; IntroSkipper.Integration.Tests 26/57, 31 ignorados; Providers 824/824; API 793 aprovados/3 ignorados; Server Implementations 1.659 aprovados/83 ignorados. O runner não publicou soma global nesta execução e não será inferida; os skips opt-in/ambiente permanecem pendentes. O filtro excluiu `LiveIntegrationTests`; build reportou avisos de documentação/analisadores, sem erros. Sem acesso a bancos de produção ou runtime. Formal 2/7, matriz 5.291/5.376 não recalculada, sem release. MariaDB é o único banco relacional operacional.

### Nebula Python: cobertura medida e loop de fixtures explícito — 2026-10-29

- Suíte completa: 674/674 em 10,36 s; compileall passou. Relatório de cobertura local via JSON temporário: 80,5% agregado (3.940/4.894 statements); `main.py` 75,6%, `feed_ftp.py` 81,0%, `strm_downloader.py` 80,4%, `supabase_sync.py` 90,5%, `check_deps.py` 92,9% e `mongo_admission.py` 100%.
- Corrigido aviso de futuro do `pytest-asyncio` com `asyncio_default_fixture_loop_scope = function` em `Tools/NebulaPython/pytest.ini`; suíte repetida 674/674 em 13,23 s, sem aviso de depreciação; compileall aprovado.
- Não definido limite de cobertura automatizado: o agregado está somente 0,5 ponto percentual acima de 80%, sem folga para variação entre plataformas/versões. Prioridade seguinte é adicionar cobertura a fluxos relevantes de `main.py` (75,6%) e manter execuções curtas por módulo.
- Cobertura usa doubles e não comprova Telegram/FTP/MongoDB reais, falha física, Linux, concorrência, runtime instalado ou produção. Sem release e sem crédito formal; homologação segue 2/7. MariaDB é a única base relacional operacional.

### Resiliência de stage_paths.json — 2026-10-29 05:53 BRT

A leitura de raízes de stage ignorava qualquer erro na lista JSON em bloco: um único item que não fosse string fazia descartar todas as raízes vindas do arquivo. Agora entradas não-string/vazias são ignoradas uma a uma, preservando as válidas e a deduplicação com `STAGING_DIRS`/`STAGING_DIR`; fallback padrão só entra se nenhuma raiz válida existir. Testes: focais `test_strm_downloader_helpers.py` 76/76 em 0,42 s; suíte Nebula 676/676 em 7,22 s; compileall passou. Fixture usa `tmp_path`; sem leitura de biblioteca real. Portal Git `main` recebeu a evidência no commit `131b5867335a06f6a93c851608b629459142fb96`; contrato 358/358 e SHA remoto conferido. MariaDB é o único banco relacional operacional. Homologação formal 2/7, sem release/produção.

### Stage fallback test coverage — 2026-10-29 05:54 BRT

Incluído caso de teste que prova que o fallback `staging/` só é escolhido quando não há raiz válida em ambiente nem em `stage_paths.json`; também confirma que raiz de ambiente configurada não recebe raiz padrão adicional. Teste focal `test_strm_downloader_helpers.py`: 77/77 em 0,35 s. Suíte Python Nebula: 677/677 em 7,18 s; compileall passou. Portal Git `main` commit `5adcf608d08c44138273d9215443ecc129bbd242`, contrato 359/359 e SHA remoto confirmado. Sem mídia/disco operacional, Linux/FTP/produção ou gates formais. Homologação 2/7; sem release. MariaDB é o único banco relacional operacional.

### Nebula resolve_media_parent — 2026-10-29 05:58 BRT

A medição de cobertura indicou lacunas em `resolve_media_parent` (`main.py`). Foram adicionados testes para preferir a pasta existente exata de menor profundidade, reutilizar uma pasta existente por correspondência aproximada e criar a pasta somente quando nenhuma correspondência existe. `test_main_helpers.py`: 57/57 em 7,94 s; Nebula completo: 680/680 em 7,40 s; compileall aprovado. Cobertura JSON: 3.980/4.899 statements (81,24%); `main.py` 76,83%, `feed_ftp.py` 81,01%, `strm_downloader.py` 81,54%, `supabase_sync.py` 90,47%. Os doubles não validam MongoDB real nem biblioteca de produção. Portal `main` commit `13f45589a754b7fea3f64304ff67bea9c41997ef`, contrato 360/360 e SHA remoto confirmado. Mapa técnico conferido; nenhum módulo, API, dependência ou fluxo de produção alterado nesta fatia. Homologação formal 2/7; sem release.

### Regressão Release recompilada da solução .NET — 2026-10-29 06:07 BRT

`dotnet test MulletaFlix.sln --configuration Release --no-restore --filter 'FullyQualifiedName!~LiveIntegrationTests' --verbosity quiet` recompilou e terminou com exit code 0, sem falhas. Resultados observados: Providers 824/824; API 793 aprovados/3 ignorados; Server.Tests 95/95; Server.Implementations 1.659 aprovados/83 ignorados; Server.Integration 8 aprovados/143 ignorados; IntroSkipper.Integration 26 aprovados/31 ignorados. A saída não foi retida como soma global confiável; nenhum total agregado é inferido. `LiveIntegrationTests` foi excluído e os skips não contam como aprovados.

Os cenários MariaDB opt-in permaneceram ignorados: não havia `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING` configurada para um serviço descartável. CI hospedada, execução Linux, runtime instalado e produção não foram validados. MariaDB é o único banco relacional operacional; sem release e homologação formal permanece 2/7.

### Scheduler: remover espera fixa do teste de limpeza — 2026-10-29 06:21 BRT

`EnqueueDuringCleanupStartsNewWorkersAndCompletes` aguardava 65 segundos para alcançar o callback de limpeza, apesar de precisar apenas sincronizar com o evento. Foi adicionado um construtor interno com período configurável para testes; o construtor público mantém os 60 segundos de produção. O teste usa 100 ms e aguarda o `TaskCompletionSource` do callback. Duração observada: 65,116 s antes; 200 ms na revalidação focal. A suíte `Jellyfin.Controller.Tests` passou 73/73 em 230 ms após a mudança; o teste foi repetido 5 vezes e passou em 180–193 ms. A solução Release recompilada passou sem falhas, com skips ambientais/plataforma mantidos como pendências. `git diff --check` passou. Sem acesso a runtime ou banco de produção. MariaDB segue como único banco relacional operacional; homologação formal 2/7, sem release.

Limite de timeout de 60 s no teste separado de recuperação de backup foi preservado: é guarda de falha, não espera fixa. Mapa técnico conferido; nenhum módulo, API pública, dependência ou fluxo de produção mudou. Registro da sessão: [[Sessões/2026-10-29-MulletaFlix-controller-test-cleanup-speedup-0621]].

### Nebula: remover espera real nos testes de timeout e retry — 2026-10-29 06:29 BRT

TRX anteriores mostraram cerca de 15 s no teste de falha MongoDB e 15 s no teste de retry de startup. `NebulaMongoContext` ganhou construtor interno para encurtar o timeout somente no teste; o construtor público preserva 15 s. `NebulaHostedService` ganhou delegate interno de atraso; o teste valida os retries de 5 s e 10 s sem dormir, enquanto o construtor público continua usando `Task.Delay`. Focais 2/2 em 426 ms; classes Nebula 86/86 em 6 s; projeto `Jellyfin.Server.Implementations.Tests` passou 1.659, ignorou 83, falhou 0 (1.742 casos), duração 42 s. Avisos de analisadores/documentação preexistentes foram observados no rebuild focal. Revisão independente: nenhum achado. Portal contrato 363/363; Git `main` commit `635dfaa5bdb7ffa235c8891f1248780c42de957c`; deployment Vercel da nova revisão ainda não confirmado nesta anotação. Sem banco real. MariaDB segue como único banco relacional operacional; homologação formal 2/7, sem release.

Mapa técnico conferido: classes/runtime e sequência de produção iguais; construtores internos e delegate servem só para controle determinístico dos testes. Sem API pública ou dependência nova. Ver [[Sessões/2026-10-29-MulletaFlix-nebula-test-timeouts-retries-0629]].

Atualização de publicação — 2026-10-29 06:34 BRT: portal Git `main` atualizado no commit `d24bcbe7e466bb165d20d0f1a5166e54dc4b5505`, contrato 364/364. Vercel não lista deployment para esse SHA nem para o commit de evidência inicial. A criação explícita do primeiro deployment respondeu HTTP 402 por exceder o limite gratuito de 100 deployments/dia; CLI Vercel não instalada. Git atualizado; deployment público ainda não confirmado.

### Startup Nebula: acelerar retry transitório no teste — 2026-10-29 06:36 BRT

TRX identificou `TransientStartupTimeout_IsRetriedBeforeStartingDownloader` consumindo 5,014 s por atraso real. O teste usa a seam interna de delay para validar exatamente 5 s sem aguardar; serviço público preserva `Task.Delay` e a política de retry. Focal 1/1 em 155 ms; projeto Implementations reexecutado: 1.659 aprovados/83 ignorados/0 falhas, 43 s. Outros waits de sincronização/recuperação permanecem pendentes de revisão sem enfraquecer suas garantias. Sem Mongo real/produção; MariaDB único relacional. Formal 2/7; sem release.

Publicação parcial — 2026-10-29 06:40 BRT: portal Git `main` atual `7a997a86bc36b77a260db4f53b82d5a406a1d276`. O deployment `dpl_FEB8txzNbYwY9VhLk2xBbxqCB3bW` segue READY para o SHA anterior `9be0169471e625ff32469a521398894fa3403320`; snapshot público HTTP 200 inclui os resultados do teste, mas conserva texto de bloqueio anterior. Dois lookups do SHA `7a997a8` e a lista recente não encontraram deployment. Git atualizado; revisão nova ainda não visível publicamente.

### Supabase: acelerar teste de recusa a ciclo concorrente — 2026-10-29 06:46 BRT

O caso `StartContinuousSync_DoesNotStartAnotherCycleWhilePreviousCallbackIsStillRunning` caía no timeout público de encerramento de 5 s e levava 10,318 s devido ao ciclo bloqueado. Somente a instância de teste configura 100 ms; o teste mantém o callback bloqueado e afirma que o segundo ciclo não é iniciado. Serviço público mantém 5 s. Focal 1/1 em 505 ms; regressão completa 1.659 aprovados/83 ignorados/0 falhas em 33 s. Portal `main` commit `129aa1111a3144100f1199b54af25490f942156b`, contrato 365/365; deployment Vercel ausente para esse SHA, último READY é `9be0169`. Sem validação MariaDB real, Mongo ou produção; formal 2/7, sem release.


### Nebula: acelerar teste de timeout da limpeza — 2026-10-29 06:49 BRT

O teste `CleanupTimeout_PreservesResourcesUntilTheTaskCompletes` aguardava o timeout de produção de 5 s. O método privado de produção continua usando 5 s; uma sobrecarga interna aceita timeout somente para o teste. O teste usa 100 ms e verifica que a tarefa e o token continuam vivos após timeout, depois conclui a limpeza. Focal passou 1/1 em 183 ms; a suíte `Jellyfin.Server.Implementations.Tests` passou 1.659, ignorou 83, falhou 0/1.742 em 29 s. O portal Git `main` está no commit `61cbf6dff921eeb5ad7379240fef6b3a3eb88974`, SHA remoto confirmado, contratos 365/365. Vercel não tem deployment confirmado para esse SHA; último READY conhecido ainda aponta para `9be0169`, e o snapshot público não contém esta evidência. A tentativa explícita anterior de deployment retornou HTTP 402 por limite diário. Nenhum Mongo operacional, MariaDB de produção ou servidor foi acessado. Formal 2/7; sem release.

Premissa de banco confirmada: MariaDB é o único banco relacional operacional. Nomes internos `MySql*` identificam provider/driver compatível, não uso de servidor MySQL.

Mapa técnico conferido: sem mudança de módulo, API, dependência ou fluxo de produção; somente seam interna de teste.

### Recuperação de restore após encerramento do processo — 2026-10-29 06:58 BRT

O teste existente `RestoreBackupAsync_RecoversAfterRestoreProcessIsTerminated` passou quatro execuções consecutivas: 4/4 em 4 s por execução. Ele encerra o processo filho durante a escrita do staging, reinicia a restauração e verifica 64 MiB recuperados, journal removido e ausência de temporários/sidecars. Os limites de 60 s/30 s são guards de falha, não sleeps. `BackupServiceTests`: 98 aprovados, 5 ignorados, 0 falhas/103 em 8 s. Fatia `FullSystemBackup`: 154 aprovados, 16 ignorados, 0 falhas/170 em 8 s, Windows/.NET 10 Release. Os skips Linux/macOS de filesystem, ACL e mount permanecem pendentes.

Portal Git `main` atualizado no commit `348ac4e5f2f9bd991d995f640bb6e583a3a3627a`; contrato local 366/366. Vercel Production está READY para esse mesmo SHA. Leitura autenticada do endpoint `homologacao-status.json` confirmou HTTP 200, evento `server-backup-restore-process-recovery-repeat-2026-10-29-0653`, evidência dos testes e homologação formal 2/7 (28,6%).

Nenhum banco operacional foi acessado. MongoDB estava Running, mas sem conexão; não havia endpoint MariaDB descartável configurado. MariaDB continua sendo o único banco relacional operacional. CI hospedada, Linux nativo, runtime instalado e produção permanecem sem validação. Sem alteração de código do servidor e sem release.

### Nebula: revalidar caminhos de falha lentos — 2026-10-29 07:01 BRT

TRX histórico da regressão apontava 15,05 s para falha de consulta Mongo e 15,04 s para retentativas de startup do Nebula. Os dois testes focais foram reexecutados na revisão atual e passaram 2/2 em 412 ms. A injeção de timeout/atraso é interna ao teste; defaults públicos permanecem Mongo 15 s e retry 5 s/10 s. As assertions de falha, telemetria sem dados sensíveis, startup independente do downloader e ausência de montagem do disco permanecem ativas. Nenhum código de servidor mudou nesta revalidação.

Portal Git main atualizado em 01cb029ce3549bace53850f09ec61da2fd07895c; contrato 367/367. Vercel ainda não mostra deployment para esse SHA; último Production READY observado é 348ac4e5f2f9bd991d995f640bb6e583a3a3627a, então esta evidência nova está no Git e publicação pública no Vercel segue pendente. MariaDB é o único banco relacional operacional; nenhum banco foi acessado. Formal 2/7, sem release.

### Catálogo de séries: revalidar duração anômala — 2026-10-29 07:04 BRT

TRX histórico registrava 12,71 s no teste GetLatestItemList_TvShows_DoesNotMergeDifferentSeriesWithTheSameName. A classe foi reexecutada no assembly Release existente (--no-build --no-restore): 3/3 em 1 s. Novo TRX focal: 0,055 s, 0,075 s e 1,037 s. A duração alta não se reproduziu e não foi encontrada espera fixa nos três testes; variação de ambiente/JIT é hipótese, não causa provada. Não alterei query, teste ou código de produção sem reprodução.

Portal Git main atualizado no commit 16566bbd023a537be9ee02becf0d1cb429dfdca6; contrato 368/368. Vercel não mostra deployment deste SHA; o último Production READY confirmado permanece no commit 348ac4e5f2f9bd991d995f640bb6e583a3a3627a. A nova evidência está no Git, não confirmada no site.

Sem banco operacional ou produção. MariaDB é o único banco relacional operacional. Homologação formal 2/7; sem release. Mapa técnico conferido, módulos/APIs/dependências/fluxos inalterados nesta fatia.

### Restore MariaDB: interrupção antes do commit — 2026-10-29 08:31 BRT

O cenário de encerramento abrupto em `BackupRestoreMariaDbTests` agora cobre dois pontos: antes de `CommitAsync` e depois do commit relacional, ambos antes da conclusão do commit de arquivos. No ponto pré-commit, a conexão independente deve continuar vendo somente o estado MariaDB anteriormente confirmado; após encerrar o processo, o exercício de recuperação deve convergir banco e arquivo para o backup e limpar journal/sidecars. O teste recompilou em Windows/.NET 10 Release. Execução foi ignorada porque `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING` não está configurada; nenhuma instância operacional foi consultada. CI possui serviço MariaDB descartável configurado, mas não foi executada nesta rodada. Formal 2/7; sem release. A estrutura técnica foi conferida e não mudou: apenas cobertura no teste de integração existente.

### Teste determinístico de cache `.ignore` sem espera fixa — 2026-10-29 08:37 BRT

`DotIgnoreIgnoreRuleCacheTests.CacheInvalidation_ModifyIgnoreFile_Reparses` esperava 50 ms somente para forçar a atualização de `LastWriteTimeUtc`; isso acrescentava tempo e continuava dependente da resolução temporal do filesystem. O teste agora atualiza o conteúdo de mesmo comprimento e ajusta o timestamp explicitamente, verificando o mesmo comportamento sem sleep. `DotIgnoreIgnoreRuleTest` em Release/.NET 10 passou 35/35 em 270 ms. Uma primeira compilação apontou que `Parallel` ainda precisa do using `System.Threading.Tasks`; o using foi restaurado e a validação final passou. `git diff --check` limpo; avisos de analisadores não relacionados seguem existentes. Nenhum código de produção, API, schema ou dependência mudou. Homologação formal 2/7 (28,6%), sem release. MariaDB é o único banco relacional operacional.

### Nebula: remover atraso redundante após descarte da lease — 2026-10-29 08:41 BRT

`DisposeAsync_StopsHeartbeatAndReleasesOnlyOnce` aguardava mais 50 ms depois de `DisposeAsync`, embora a implementação aguarde `_heartbeatTask` encerrar antes de retornar. A espera foi removida; a contagem de renovações e a liberação única continuam verificadas após a barreira real de término. `NebulaMongoAdmissionLeaseTests` passou 3/3 em 94 ms em Release/.NET 10; formatter e `git diff --check` passaram. Nenhuma mudança de produção, banco, API, dependência ou ciclo de heartbeat. Homologação formal 2/7, sem release. MariaDB é o único banco relacional operacional.

### Regressão de HTTP 206 incompleto e persistência concorrente de imagens — 2026-10-29 08:51 BRT

- Teste novo reproduziu que o feeder aceitava resposta HTTP 206 sem `Content-Range` e gravava o corpo parcial como arquivo completo. Agora 206 exige intervalo numérico e compatível; resposta 200 segue como fallback sequencial; status inesperado falha. O temporário é removido e o arquivo anterior permanece íntegro.
- `SaveImagesAsync` agora usa a mesma faixa de exclusão por ID que `SaveItems`, serializando gravações de imagem e metadados do mesmo item. Isso fecha uma divergência observada na fonte; integração concorrente com MariaDB ainda não foi executada.
- Teste feeder focado reproduziu RED antes da alteração e passou depois; arquivo STRM 87/87. Suíte Nebula Python 682/682; `compileall` passou.
- Solução .NET Release: 17 assemblies, 5.363 aprovados, 273 ignorados, 0 falhas/5.636 resultados, exit 0. Ignorados incluem integração opt-in/ambiente; não contam como aprovados. Avisos de analisadores e estilo existentes apareceram na compilação.
- Sem CI hospedada, integração MariaDB local, Linux nativo, runtime instalado ou produção. MariaDB é o único banco relacional operacional. Homologação formal segue 2/7 (28,6%); sem release.
- Mapa técnico conferido: mudanças ficam nos módulos existentes `ItemPersistenceService` e feeder Python; sem endpoint, dependência ou componente novo.

### Revalidação após incluir teste MariaDB concorrente — 2026-10-29 08:57 BRT

Foi adicionado `SaveImagesAsync_ConcurrentWritesForSameItem_DoNotMixImageSets`, que disputa 20 escritas de dois conjuntos de imagens para o mesmo item e verifica que a persistência final contém um único conjunto completo. O projeto de integração recompilou; execução focada foi ignorada (1/1) porque não há conexão MariaDB descartável configurada. A suíte completa .NET foi repetida após a inclusão: exit 0; 17 assemblies, 5.363 aprovados, 274 ignorados, 0 falhas/5.637 resultados. Os ignorados incluem o novo caso de integração e permanecem fora do percentual de homologação. Formal 2/7; sem release. MariaDB é o único banco relacional operacional.

Publicação da homologação — 2026-10-29 08:58 BRT: `Mulletaflix-Portal/main` no commit `ba195f1d16e91caf4c586f3890074ed01105ba68`; 377/377 testes Node e SHA remoto conferido. O servidor permanece sem commit/release; a versão não foi instalada nem testada em produção.

### Isolamento do MariaDB, locale de imagens e restore direcionado do CI — 2026-10-29 09:37 BRT

- `MariaDbProcessManager` coordena inicializações tanto pelo diretório quanto pela porta configurada. Antes de reutilizar MariaDB local, consulta `@@datadir` e falha fechado se a instância responder pela porta, mas usar outro diretório. MariaDB continua sendo o único banco relacional operacional.
- Rechecagem do código e teste já existentes de `ItemImageProvider` confirmou que preferência explícita `pt-PT` é preservada; somente `pt` com país `BR` é normalizado para `pt-BR`. Nenhuma alteração de código de imagem foi necessária nesta sessão.
- Job Mongo CI restaura o projeto `Jellyfin.Server.Implementations.Tests` diretamente em vez de avaliar a solução inteira. Restore local passou em 6,7 s e resolveu 29 projetos transitivos; não há medição comparativa hospedada para quantificar o ganho de execução.
- Testes focados Release/.NET 10: mutex/diretório 9/9; preferência de idioma 2/2. O teste de compatibilidade do nome histórico do mutex reproduziu a falha antes de restaurar o nome antigo e passou depois. Regressão da solução Release, sem nomes Integration: 15 assemblies, 5.329 aprovados, 85 ignorados, 0 falhas, exit 0. Restore direcionado do projeto Mongo passou. Nenhum serviço MariaDB/Mongo nem produção foi acessado nesta validação.
- Ainda pendentes nesta fatia: integração com MariaDB descartável, CI hospedada, execução Linux, runtime instalado e produção. Homologação formal não avança pelos testes unitários; permanece em 2/7 (28,6%). 12.1.46 continua candidata, sem release.
- Mapa técnico conferido: mudança de fluxo no bootstrap MariaDB existente; preferência de idioma revalidada no código já presente. Sem módulos, APIs ou dependências novas.
- Portal Git `main` atualizado no commit `7bff25b7260b8b30ee7aa08d314643a009d3ff1b`; contrato 378/378. Deployment Vercel e conteúdo público não foram verificados.

### Regressão multipart e harness PowerShell 5.1 — 2026-10-29

Na execução completa Nebula sob concorrência com outras suítes, o teste de retomada multipart falhou porque uma futura pendente foi cancelada quando executor.map propagou a primeira exceção. O downloader agora envia as partes e aguarda todos os futures antes de propagar o primeiro erro; partes já concluídas ficam disponíveis para retomada. Após a mudança, Nebula Python passou 682/682; o caso de retomada passou 20/20 repetições; compileall passou.

O harness ApplyUpdateFailClosed.Tests.ps1 não carregava System.ServiceProcess porque Get-Service é mockado. O teste em Windows PowerShell 5.1 falhava ao resolver o enum, enquanto PowerShell 7 passava. O harness agora carrega a assembly explicitamente; passou em ambas as versões. Os nove scripts de empacotamento Windows passaram em PowerShell 7.

Validação geral local: .NET Release, solução completa com filtro contra testes live, exit 0; teste web 425/425; build:check, build de produção e verify:build passaram, artefatos 1.927 e payload inicial 298,1 KB gzip/420 KB. Contratos Node de CI/release passaram 15/15. Vite emitiu avisos sobre diretivas use client ignoradas e imports mistos; a build e o limite de payload permaneceram aprovados.

Não foi executado E2E local porque a porta 8096 está ocupada e o diretório stage já contém artefatos; não foi tocado. Integração descartável MariaDB/Mongo, CI hospedada, runtime instalado e produção permanecem pendentes. MariaDB é o único banco relacional operacional. Homologação formal permanece 2/7; nenhuma release criada.

### Cancelamento da espera de startup MariaDB — 2026-10-29 09:49 BRT

`MariaDbStartupMutexLease` agora recebe o token de cancelamento do startup e aguarda simultaneamente a posse do mutex ou o cancelamento. O cancelamento só interrompe a aquisição; após obter o mutex, a thread proprietária continua segurando-o até o descarte explícito, preservando sincronização de processo.

O novo `Constructor_CancelsWhileWaitingForAnotherOwner` valida cancelamento enquanto outro thread mantém o mutex. `MariaDbStartupMutexLeaseTests` passou 10/10. Regressão local da solução Release com `FullyQualifiedName!~Integration`: 15 assemblies, 5.330 aprovados, 85 ignorados e 0 falhas observadas. Os ignorados não contam como aprovados; o processo agregado encerrou antes de eu capturar seu exit code, portanto ele não é declarado aqui. MariaDB descartável, CI hospedada, Linux nativo, runtime instalado e produção não foram acessados.

Formal permanece 2/7; 12.1.46 continua candidata sem publicação. MariaDB é o único banco relacional operacional. Mapa técnico conferido e atualizado: mudou o fluxo de aquisição dos locks existentes, sem classe pública, API ou dependência nova.

### Integração MariaDB descartável — 2026-10-29 10:06 BRT

Executadas as suítes contra MariaDB 11.4.4 efêmero em porta loopback exclusiva: `Jellyfin.Server.Integration.Tests` 151/151; `IntroSkipper.Integration.Tests` 57 aprovados, 1 ignorado; filtro MariaDB de `Jellyfin.Server.Implementations.Tests` 30 aprovados, 1 ignorado. Total 238 aprovados, 2 ignorados, 0 falhas, exit 0 em cada comando. O skip IntroSkipper depende do exercício MongoDB e o skip de Implementations depende de Linux `lower_case_table_names=0`. Instância encerrada e porta fechada; credencial temporária removida do arquivo de conexão. A exclusão recursiva do diretório temporário foi bloqueada e os dados/logs sem credencial permanecem em `%TEMP%`. Resultado local Windows não fecha os gates Linux/CI/instalação/produção nem altera homologação formal (2/7). Sem release.

Portal Git `main` recebeu a evidência no commit `2d65b700b3034420ab80bfae3dcbe74b5d91f557`; contrato 380/380 e SHA remoto conferido. Deployment Vercel não verificado.
### Serialização do ciclo de vida MariaDB — 2026-10-29 10:16 BRT

Auditoria confirmou que chamadas simultâneas a `StartMariaDbAsync` compartilhavam processo e leases estáticas: se a segunda chamada falhasse ao aguardar lock, sua rotina de erro poderia executar `StopMariaDbAsync` e encerrar o processo iniciado pela primeira. Start e stop agora passam por uma trava assíncrona dentro do processo; falha de startup limpa estado dentro da operação já protegida, sem reentrância na trava. A lease também observa cancelamento após adquirir o mutex, liberando-o pela thread proprietária.

`MariaDbStartupMutexLeaseTests`: 13/13; `Jellyfin.Server.Tests`: 103/103, Windows/.NET 10. Não acessou MariaDB, MongoDB, serviço instalado ou produção. `RecoveryExercise_RestoresMariaDbMongoAndFilesFromIsolatedSources` já passou 1/1 em 28/10/2026, 22:59 BRT, conforme `docs/backup-coverage-audit.md`; o resultado foi obtido com MariaDB 11.4.4 e MongoDB 8.3.7 descartáveis. Ainda falta execução CI com as versões configuradas (MariaDB 11.4.13 e MongoDB 7.0), validação Linux/runtime/instalação e consolidação do exercício no painel. Formal 2/7; versão 12.1.46 continua candidata, sem release.

Portal `Mulletaflix-Portal/main` atualizado com esta evidência, commit `57e0ed7903e4ce09a3fee717177f531537344e20`; contrato 381/381, SHA remoto confirmado. Deployment Vercel não verificado.

### CI sem steps executados — 2026-10-29 10:21 BRT

A integração GitHub reporta run 40872202840 do commit-base `e3425131826a8d0573267878b0766f910588b34c` concluído como failure em 16 jobs. O job de recuperação completa não contém steps; os logs consultados retornam 404. Amostragem também confirmou ausência de steps/logs recuperáveis nos jobs Server Integration, Server Build e Nebula Mongo. Assim, nenhum teste foi comprovado como executado e a causa do runner permanece desconhecida; não classificar como falha de código nem como aprovação. Reexecução CI com steps e exercício MariaDB+Mongo+arquivos permanecem pendentes. Formal 2/7; sem release.

Estado desta verificacao publicado no portal Git `main`, commit `71642809d9c482e27b66027734f5af82a541d81a`; contrato 382/382 e SHA remoto confirmado. Vercel nao foi verificado.

Rechecagem das anotações do GitHub Actions confirmou a causa: os jobs foram bloqueados antes de iniciar porque a conta GitHub está bloqueada por cobrança. Nenhum teste rodou nesse run; o estado é bloqueio de infraestrutura, não reprovação do código.

### Regressão da solução .NET após serialização MariaDB — 2026-10-29 10:28 BRT

`dotnet test MulletaFlix.sln --configuration Release --no-build --no-restore --filter FullyQualifiedName!~LiveIntegrationTests --verbosity quiet` terminou com exit 0. Agregação dos resumos de 17 assemblies: 5.372 aprovados, 261 ignorados, 0 falhas/5.633. Destaques: `Jellyfin.Server.Tests` 103/103; `Jellyfin.Server.Implementations.Tests` 1.659 aprovados/83 ignorados; `Jellyfin.Server.Integration.Tests` 8 aprovados/143 ignorados; `IntroSkipper.Integration.Tests` 26 aprovados/32 ignorados. O filtro excluiu testes live; integrações MariaDB/Mongo continuam ignoradas sem serviços opt-in. Sem conexão a banco/serviço operacional. Esta regressão não comprova Linux, instalação ou produção e não concede gate formal. Homologação 2/7; sem release.

Portal Git `main`: commit `8227cc94d769ff4f832178497166fdc115302eff; teste contratual 383/383 e SHA remoto confirmado. CI hospedada segue bloqueada antes dos steps pela cobrança; deployment Vercel não verificado.

### Nebula Python e regressão .NET — 2026-10-29 10:30 BRT

A regressão .NET filtrada da solução passou em 17 assemblies: 5.372 aprovados, 261 ignorados, 0 falhas/5.633, exit 0; `LiveIntegrationTests` excluídos. O `Jellyfin.Server.Tests` passou 103/103. `Tools/NebulaPython`: Python 3.10.11, `pytest -q` 682/682 e `compileall -q .` exit 0. Integrações opt-in de MariaDB/Mongo continuam ignoradas; CI GitHub está bloqueada por billing antes dos steps. Formal 2/7, sem release.

Portal Git `main` atualizado com a suíte Nebula, commit `439bea42b5402844ef5d2c52f1d5a546b5bb887c`; contrato 384/384 e SHA remoto confirmado. Deployment Vercel não verificado.

### Auditoria ampliada e E2E bloqueado — 2026-10-29 10:55 BRT

- Regressão .NET Release com MariaDB 11.4.13 e MongoDB 7.0.45 descartáveis: 17 assemblies, 5.604 aprovados, 29 ignorados, 0 falhas/5.633; exit 0. `IntroSkipper.Integration.Tests` 58/58, `Jellyfin.Server.Integration.Tests` 151/151. Os serviços temporários foram encerrados; nenhum banco operacional foi usado.
- Nebula Python 3.10 em venv temporária com hashes: 682/682 aprovados; `compileall` passou. Nove verificações de empacotamento Windows passaram em PowerShell 7, `ApplyUpdateFailClosed.Tests.ps1` passou em PowerShell 5.1 e os quatro contratos Node de release/CI passaram 15/15.
- Web: 425/425 testes passaram; cobertura global de linhas 8,25% (statements 8,28%, branches 7,17%, functions 6,45%). Typecheck, stylelint, build de produção, verificação de build e auditoria de dependências de produção passaram; bundle inicial 298,1 KB gzip/420 KB. Cobertura Web é insuficiente para declarar homologação completa.
- Smoke E2E Wizard/listas não executou casos. O runner recusou corretamente a porta ocupada 8096; em stage separado na 18096, a recuperação falhou fechada porque a ACL herdada de `%TEMP%` concede escrita a identidade não confiável. Não foi enfraquecida a validação. Startup também tentou iniciar o cliente DuckDNS instalado; investigar e isolar esse bootstrap antes de nova tentativa.
- Incidente de execução: a primeira invocação do runner partiu do checkout Web original; o reset do harness removeu `stage/data` ignorado nesse worktree. A pasta está ausente; não há evidência registrada de seu conteúdo anterior. Nenhum acesso à porta/serviço de produção 8096 ocorreu.
- Homologação formal permanece 2/7 (28,6%); os resultados locais não concedem gate formal. CI hospedada segue sem runners por bloqueio de faturamento; Linux nativo, runtime instalado e produção pendentes. 12.1.46 continua candidata, não publicada. MariaDB é o único banco relacional operacional.
- Mapa técnico conferido: nenhuma API, módulo ou dependência foi adicionada nesta rodada. A sessão detalha comandos, resultados, bloqueios e limpeza pendente.
- Página de homologação sincronizada com as evidências deste lote no Git `main` do portal: commit `29c0f21ce6577f6efe3b4416211c3b7adcc9112a`; SHA remoto conferido; contrato local 386/386. Deployment Vercel não verificado.

### Bootstrap externo isolável e smoke E2E concluído — 2026-10-29 11:14 BRT

- `SecureConnectionProcessManager.EnsureSecureConnectionStartedAsync` respeita `MFLX_DISABLE_EXTERNAL_BOOTSTRAP` antes de qualquer caminho específico do Windows e não inicia HTTPS externo/DuckDNS quando o runner pede isolamento. O teste `EnsureSecureConnectionStartedAsync_ExternalBootstrapDisabled_SkipsExternalServices` passou dentro de `Jellyfin.Server.Tests` 104/104; compilação da solução passou.
- A regressão .NET posterior em 17 assemblies terminou exit 0: 5.373 aprovados, 261 ignorados, 0 falhas/5.634. Rodada MariaDB 11.4.13 + MongoDB 7.0.45 descartáveis anterior continua registrada separadamente como 5.604 aprovados, 29 ignorados, 0 falhas/5.633. Ignorados não são aprovações.
- Playwright filtrado para Wizard/listas passou 3/3, sem falhas ou skips. Stage 12.1.46 acessível em 127.0.0.1:18096; inicialização registrada em 16,2 s; MariaDB embutido descartável na porta 33100. Raw report confirma os fluxos de stage limpo, conclusão do assistente e persistência de lista. A porta 8096 não foi acessada. Isso não cobre toda a suíte E2E ou a operação real de mídia/Nebula.
- O fix e esses testes são evidência candidata, não nova homologação formal. Gates continuam 2/7 (28,6%); Web tem 8,25% de cobertura de linhas; CI hospedada bloqueada por billing; Linux nativo, runtime/instalação e produção pendentes. 12.1.46 não publicada.
- O worktree de produção permaneceu sem acesso. Um reset anterior do runner removeu `stage/data` ignorado; conteúdo prévio não foi inventariado e não há cópia recuperável confirmada. Diretórios temporários da execução permanecem sujeitos à limpeza segura.
- Estrutura revisada: mesmo `SecureConnectionProcessManager` e teste existente; startup agora encerra cedo quando o sinalizador de isolamento está ativo. Sem API, dependência ou componente novo. MariaDB é o único banco relacional operacional.
- Portal de homologação atualizado em `Mulletaflix-Portal/main`, commit `fc19dc92caa7dd4d4e92713900a9148015cca0d6`; contrato portal 387/387 e SHA remoto conferido. Vercel não foi verificado.

### Publicação do resultado E2E — 2026-10-29 11:39 BRT

Portal do Mulletaflix atualizado em Git `main`, commit `93edcb4df6b28bc170914a8d5e933ebc0f45fbda`; `git ls-remote origin refs/heads/main` confirmou o mesmo SHA. Contrato do portal passou 389/389 antes do commit. Deployment Vercel não foi verificado.

### Caminhos padrão da vinheta nativa e triagem Playwright — 29/10/2026 11:47 BRT

- Removida a busca automática da vinheta em `Videos` do perfil do usuário e em um caminho pessoal fixo. Os candidatos padrão agora ficam limitados ao pacote instalado e aos diretórios `ProgramDataPath`/`DataPath` do servidor; caminhos configurados pelo administrador continuam aceitos.
- `NativeIntroProviderTests`: 8/8; regressão `Jellyfin.Server.Implementations.Tests`, excluindo Integration: 1.661 aprovados, 82 ignorados, 0 falhas/1.743, exit 0. Não foi executada regressão completa da solução nesta rodada.
- Revisão das falhas Playwright classificou preliminarmente 6 grupos como contrato/seletores possivelmente desatualizados, 12 como provável insuficiência/forma de fixture e nenhum defeito de produto confirmado só por esse relatório. Continua necessária triagem individual, correção de fixtures e repetição. Isso é classificação preliminar, não evidência de que as falhas são apenas testes.
- Linux nativo, CI hospedada bloqueada por cobrança, instalação/upgrade e produção não foram validados. Homologação formal permanece 2/7; 12.1.46 continua candidata, sem release.


### Playwright: fixtures locais e varredura da biblioteca — 2026-10-29

- Fixtures locais com quatro filmes, um episódio e NFO foram adicionadas; harness sem diretórios pessoais, com ACL restrita e bootstrap externo desativado.
- Testes Node de suporte: 13/13. O stage Windows 12.1.46 com MariaDB descartável continua sem itens consultáveis: teste focal de acessibilidade/detalhes 0/2, mesmo após a preparação inicial e a tentativa de consolidar a varredura em uma execução.
- Logs demonstram deadlock MariaDB ao inserir BaseItemImageInfos; as cinco tentativas de retry registradas não foram suficientes para produzir itens reconhecidos nessa execução. Investigar o fluxo concorrente de biblioteca/metadata e validar com uma nova execução antes de classificar como defeito de produto.
- Produção 8096 não acessada; a homologação formal segue 2/7 (28,6%). 12.1.46 continua candidata, sem release. CI hospedada, Linux, runtime/instalação, regressão Web/E2E ampla e produção permanecem pendentes.

### E2E focado aprovado; suíte ampla ainda bloqueada — 2026-10-29 12:41 BRT

- Ajustado o contrato de consulta a bibliotecas para usar `VirtualFolderInfo.ItemId` e filtrar mídia `Movie/Video`; helper também retorna `Type`. A seção de metadata secundária é própria de programa ao vivo e deve permanecer oculta em filmes.
- Corrigido rótulo acessível dos selects de fonte, vídeo, áudio e legendas com placeholders localizados e correção do escopo do backbutton no E2E.
- Runner bloqueia 8096 e aceita stage apenas em loopback 18096–18196. Testes auxiliares passaram 15/15; `npm run build:check` passou; build web de produção concluiu; E2E acessibilidade e detalhes passou 2/2.
- A suíte de 39 cenários foi iniciada, mas interrompida antes de relatório final diante de esperas longas (até 90–120 s) e múltiplas falhas observadas: acesso administrativo espera `.channelAccessContainer` mesmo sem canais, dashboard espera `#logPage`, reprodução multiusuário esperava 2 sessões e recebeu 0. A rodada isolada também observou deadlock transitório ao persistir `BaseItemImageInfos` no MariaDB; a varredura continuou e detectou os itens.
- Histórico de porta: uma execução prévia iniciou stage em `127.0.0.1:8096` quando sem listener instalado; não há evidência de que tenha atingido o servidor instalado. O runner atual rejeita essa porta. Teste focado: listener 8096 = 0 antes e depois; processos do stage foram encerrados.
- Homologação formal continua 2/7 (28,6%), CI hospedada bloqueada por faturamento, sem evidência Linux/instalador/produção e nenhuma release desta etapa.
- Próximos passos: corrigir seletores/contratos E2E, fornecer fixture longa para teste simultâneo, investigar a transação de imagens MariaDB, limitar waits conforme cenário e repetir até obter relatório completo.

Estado parcial publicado no portal Git main, commit c2f19535f7bf7e87862570e6e2101e7670b7bbf2; 392/392 testes contratuais do portal passaram. Cobertura declarada não alterada (338/412 checklist, gates servidor 2/7).

### Reprodução simultânea em stage — 2026-10-29 12:56 BRT

- Fixture H.264/AAC de 300,106689 s gerada localmente e validada por ffprobe; testes Node de segurança/fixture 16/16. E2E focado de reprodução simultânea passou 1/1, com duas sessões ativas de usuários distintos, usando MariaDB descartável e stage loopback 18096. Relatório JSON isolado: `%TEMP%/mflx-e2e-report-f20a1025b6324bbfac442dd5e98625c2/playwright-summary.json`.
- A execução anterior não observou a segunda sessão porque o Chromium headless escolheu transcodificação HEVC. O cenário temporário agora desativa remux/transcodificação nas contas E2E e confirma direct play H.264/AAC. Isso valida o teste de sessão concorrente em direct play, não transcodificação HEVC nem a suíte E2E ampla.
- Uma tentativa inicial em `%TEMP%` foi rejeitada pela ACL herdada que concedia escrita a identidade não confiável; nenhum relaxamento foi feito. A repetição em stage sob ACL restrita passou. Nenhum banco operacional ou porta 8096 foi usado; após a execução não restou listener de stage.
- Este resultado corrige apenas o cenário/harness e não comprova defeito de produto ou resolução do deadlock MariaDB observado em outra varredura. Formal segue 2/7 (28,6%), sem crédito; 12.1.46 não publicada. Permanecem pendentes a suíte ampla Playwright, transcodificação, CI hospedada, Linux nativo, runtime/instalação e produção.
- Mapa técnico conferido: nenhuma API, classe de produto, dependência ou fluxo de produção mudou. Portal Git `main` sincronizado no commit `aa832d42aeaf5ee71d5897fb71c5dd8c9182542b`; contrato completo passou 393/393 e SHA remoto foi confirmado. Deployment Vercel não verificado.

### Regressão focal servidor e reconciliação da auditoria — 2026-10-29 14:00 BRT

- `dotnet test MulletaFlix.sln --configuration Release --no-build --no-restore --filter "FullyQualifiedName~SystemControllerTests|FullyQualifiedName~NativeIntroProviderTests|FullyQualifiedName~MariaDbStartupMutexLeaseTests|FullyQualifiedName~SecureConnectionProcessManagerTests" --verbosity quiet`: exit 0; 30/30 aprovados, 0 ignorados/falhas (API 3, Implementations 8, Server 19). Log: `%TEMP%/mflx-server-current-focals-confirm-20261029.log`.
- Produção confirmou 12.1.45 via GET somente leitura; serviço MongoDB operacional ficou ativo, Mongo descartável encerrado e porta 27099 fechada.
- WSL só está disponível como versão 1, sem distribuição Linux; Docker Engine não está instalado. Não foi possível executar validação Linux nesta máquina.
- Reconciliada a afirmação obsoleta no roadmap sobre o teste `RecoveryExercise_RestoresMariaDbMongoAndFilesFromIsolatedSources`: `docs/backup-coverage-audit.md` registra execução histórica 1/1 em 28/10 contra MariaDB 11.4.4 e MongoDB 8.3.7 descartáveis. A versão CI configurada e consolidação no painel continuam pendentes.
- Regressão ampla permanece 5.377/5.651, 274 ignorados/0 falhas; teste focal não somado. Homologação formal 2/7 (28,6%); sem release.
- Portal atualizado e enviado para Git `main`, commit `08036752f69241cf1d74394f7c1f389e73f6a4c2`; contrato final 397/397 e SHA remoto conferido. Vercel Production permanece READY no commit anterior `cb7bc604`; lista e endpoint público confirmaram que o foco 30/30 ainda não está publicado no deployment. Uma tentativa anterior de criar deployment recebeu HTTP 402 pelo limite diário.
- Mapa técnico conferido em 2026-10-29; nenhuma mudança de módulo, API, classe, dependência ou fluxo de produção.

### Regressão ampla da fonte — 2026-10-29 14:17 BRT

- .NET 10 Release: solução com filtro `FullyQualifiedName!~LiveIntegrationTests`, 17 assemblies, 5.377 aprovados, 261 ignorados, 0 falhas (5.638 casos contabilizados; exit code 0). Casos ignorados não são aprovados.
- NebulaPython: pytest no `.venv` do projeto, 682/682; `compileall` dos módulos, testes e ferramentas do projeto passou.
- Frontend web: Vitest 425/425 em 78 arquivos; testes Node de segurança/fixtures do runner 16/16; `npm run build:check` passou. Build de produção e `verify:build` passaram: 1.929 artefatos válidos, payload inicial 298,2 KB gzip (limite 420 KB). O Vite emitiu avisos de dependências sobre diretivas `use client` e imports mistos, sem falha de build.
- A suíte E2E de navegador não foi executada contra esta fonte: o binário isolado disponível em `stage/MulletaFlix.dll` tem data 28/10 e não representa o código atual. CI hospedada, Linux nativo, instalação/upgrade e integrações externas permanecem pendentes. Produção segue informada pelo usuário como 12.1.45; esta rodada não fez escrita nela nem publicou release.
- Auditoria de versionamento local encontrou 14 diretórios temporários Playwright (aprox. 2,87 GiB) que foram preservados no disco e excluídos do índice Git por regra de ignore. Nenhum foi removido.
- Homologação formal continua 2/7 (28,6%). Evidência local, não equivale a release nem a homologação formal. Após o lote de dez evidências, o contrato do portal passou 397/397; commit 0281b4f foi enviado ao Git main e o JSON público da Vercel respondeu HTTP 200, generatedAt 2026-10-29T14:17:00-03:00.
