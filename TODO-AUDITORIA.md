# T6.2 — admissão concorrente e exclusão do restore — reauditoria 2026-10-27 13:45 BRT

- [x] `POST /Backup/Restore` e `POST /Backup/RestorePointInTime` agora entram no semáforo global das operações de backup pesado, compartilhado com `POST /Backup/Create` e restores Supabase.
- [x] Testes determinísticos cobrem ambos os endpoints: concorrente recebe `429` com `Retry-After: 1`; playback GET continua; capacidade retorna após liberar a requisição.
- [x] Reauditoria confirmou que `BackupService.TryBeginOperation` mantém a exclusão operacional em `_backupOperationInProgress` do início ao fim de backup/restore assíncrono. `ScheduleRestoreAndRestartServer` retém o gate até o processo ser encerrado; a recuperação de restore mantém o mesmo gate.
- [x] Regressão atual em Windows/.NET 10 Release: `ConcurrentRestoreRequests_AreRejectedWithoutBlockingPlayback` passou 2/2 e `CreateBackupAsync_RejectsConcurrentOperationAndReleasesGateAfterFailure` 1/1. O segundo teste exercita restore/scheduling rejeitados enquanto backup está ativo e liberação após falha.
- [ ] O teste não exercita restore ativo contra novo backup na direção inversa, nem o ciclo real de reinício após scheduling. Carga representativa, calibração, CI hospedada, Linux, runtime instalado e produção seguem pendentes; T6.2 parcial, formal 2/7, sem release.

# Pessoas órfãs e concorrência com atualização — correção confirmada em 2026-10-27

MariaDB 11.4.4 descartável reproduziu o defeito de limpeza: `DeleteItem` apagava `BaseItems`, a FK removia `PeopleBaseItemMap` por `ON DELETE CASCADE` e a limpeza posterior já não encontrava os `PeopleId` para remover `Peoples` sem referências. O teste `DeleteItem_RemovesPeopleRowsMadeOrphanByDeletedItem` falhou antes da correção e passou após capturar os IDs antes da exclusão.

Também foi reproduzida a corrida independente com `PeopleRepository.UpdatePeople`: o update podia inserir uma pessoa após `DeleteItem` capturar os IDs para limpeza. `UpdatePeople` agora adquire o mesmo stripe local e advisory lock MariaDB, e confirma que o item ainda existe antes de iniciar a transação. Assim, a exclusão e a atualização do mesmo item serializam; updates posteriores à remoção retornam sem gravar mapas/pessoas.

Evidência em Windows/.NET 10 Release: `DeleteItem_ConcurrentWithUpdatePeople_DoesNotLeaveOrphanPeople` passou 1/1 e `DeleteItem_RemovesPeopleRowsMadeOrphanByDeletedItem` 1/1 contra MariaDB 11.4.4 descartável; `UpdatePeople_WaitsForSameItemPersistenceLockBeforeOpeningDatabaseContext` e `ItemPersistenceServiceTests` passaram 1/1 e 39/39. A suíte completa anterior a esta última correção passou 1.738, ignorou 95 e falhou 0/1.833; ela não foi repetida depois do patch concorrente. Contrato CI MariaDB/Linux 4/4 e portal 338/338. A regressão foi adicionada ao filtro do CI. CI hospedada, runtime instalado e produção não validados; homologação formal permanece 2/7 (28,6%), sem release.

# Auditoria histórica: concorrência entre remoção e atualização de pessoas — 2026-10-27 09:49 BRT

A regressão integral `Jellyfin.Server.Implementations.Tests` passou 1.738, ignorou 95 e falhou 0/1.833 em Windows/.NET 10 Release. Auditoria estática identificou escritores distintos de `PeopleBaseItemMap`: `ItemPersistenceService.DeleteItemsUnderLocks` apaga mapas e depois remove registros `Peoples` sem referências; `PeopleRepository.UpdatePeople` altera mapas sem participar das stripes/advisory locks de exclusão. A FK `PeopleId` usa `ON DELETE CASCADE`. O teste MariaDB existente valida `UpdatePeople` sequencial, não essa interleaving.

Classificação original, superada pela evidência acima: a limpeza determinística de órfãos era defeituosa. A hipótese separada de concorrência com `UpdatePeople` continua sem reprodução e sem correção atribuída. Sem acesso a banco ativo. Formal continua 2/7; agregados preservados.

# Corrida ao reconstruir as tags herdadas — 2026-10-27 09:40 BRT

`UpdateInheritedValues` executava um `ExecuteDelete` global em `ItemValuesMap`/`InheritedTags` enquanto `SaveItems` podia inserir linhas para itens. O cleanup agora adquire as 16 stripes locais, em ordem crescente, e os advisory locks MariaDB correspondentes antes de abrir a transação. A implementação usa o protocolo de locks já existente; não cria APIs nem dependências.

Validação: regressão unitária focada 1/1; `ItemPersistenceServiceTests` 38/38 em Windows/.NET 10 Release; contrato do job CI MariaDB/Linux 4/4; formatter whitespace passou; `git diff --check` passou. A integração MariaDB adicionada ao filtro de CI foi ignorada localmente por ausência da variável de conexão descartável. CI hospedada segue sem execução recuperável; runtime instalado/produção não foram verificados. Formal permanece 2/7; não alterar o agregado nem publicar release.

# Restore — regressão completa Windows Release — 2026-10-27 09:21 BRT
- [x] `FullyQualifiedName~FullSystemBackup`: 140 aprovados, 16 ignorados, 0 falhas/156 em Windows/.NET 10 Release; inclui serviço de backup/restore, journal, replay e operações relativas de diretório.
- [ ] Os 16 ignorados exigem Linux/macOS ou mounts privilegiados e não validam esses ambientes. ACL/TOCTOU Linux, CI atual, runtime instalado e produção continuam pendentes; formal 2/7, sem crédito/release.

# Restore ACL — auditoria de disponibilidade Linux/CI — 2026-10-27 09:19 BRT
- [x] Rechecado o host: WSL não instalado; Docker e Podman indisponíveis.
- [x] GitHub Actions público: run CI 39608614450 (21/10, commit `d51f4f38`) está falho; o job “Server build & unit tests” aparece falho sem etapas e a API de logs retorna 404 `BlobNotFound`.
- [ ] Nenhuma execução Linux foi obtida. Não inferir causa do CI nem marcar ACL Linux validada. Restore P0 e homologação formal 2/7 permanecem abertos; sem crédito/release.

# Restore ACL — rename de ancestral no Windows — 2026-10-27 09:13 BRT
- [x] Novo teste `OpenRoot_PreventsRenamingAnAncestorWhileAuthorityIsOpen` confirmou que o handle de ancestral retido durante `OpenRoot` bloqueia `Directory.Move` no Windows.
- [x] Teste focal: 1/1; classe `BackupRestoreDirectoryTests` Release: 45 aprovados, 11 ignorados, 0 falhas/56. Os ignorados são Linux e não contam como validação Linux.
- [ ] Evidência limitada à semântica de handle Windows; TOCTOU entre operações relativas, ACL POSIX, CI hospedada, runtime instalado e produção seguem pendentes. Restore P0 aberto; homologação formal 2/7, sem crédito/release.

# Restore ACL — resistência a rename da raiz no Windows — 2026-10-27 09:09 BRT
- [x] Novo teste `OpenRoot_PreventsRenamingTheRootWhileAuthorityIsOpen` confirmou que o handle retido por `BackupRestoreDirectory.OpenRoot` impede `Directory.Move` enquanto aberto e permite rename após dispose.
- [x] `BackupRestoreDirectoryTests` Release: 44 aprovados, 11 ignorados, 0 falhas/55. Os skips continuam sendo cenários Linux de ACL/syscalls/mount e não contam como validação Linux.
- [ ] Isso valida a semântica de compartilhamento de handle no Windows, não a janela de TOCTOU entre operações relativas, ACL POSIX, CI hospedada, runtime/produção. Restore P0 permanece aberto; formal 2/7, sem crédito/release.

# Regressão ampla e correção do encerramento do Nebula — 2026-10-27 09:00 BRT
- [x] A regressão serial Release da solução encontrou `ObjectDisposedException` em `NebulaPythonCacheLeaseHost.AcceptLoopAsync` ao parar o listener durante `AcceptAsync`; o descarte agora absorve essa exceção somente se o cancelamento já ocorreu.
- [x] `NebulaPlaybackCacheTests`: 60/60; o teste que falhou foi repetido 10/10; suíte completa `Jellyfin.Server.Implementations.Tests`: 1.735 aprovados, 95 ignorados, 0 falhas/1.830.
- [x] Reconciliando TRX da solução (substituindo o TRX inicial da implementação pelo rerun verde): 5.456 aprovados, 98 `NotExecuted`, 0 falhas/5.554 resultados em 17 arquivos; integração marcada foi excluída pelo filtro.
- [ ] WSL/Docker não estão instalados neste host; Linux/ACL, CI hospedada, runtime instalado e produção continuam sem validação. Formal 2/7; auditoria agregada distinta 5.465/5.493 não muda; sem release.

# Revalidação do mutex e do ciclo de vida MariaDB — 2026-10-27 08:48 BRT
- [x] `MariaDbStartupMutexTests` e `MariaDbProcessManagerLifecycleTests` passaram juntos 5/5 em Windows/.NET 10 Release. O teste integrado executou o MariaDB empacotado em porta dinâmica e datadir exclusivo; start-stop-start manteve uma linha inserida.
- [x] Pós-teste: somente PID 31236 segue como `mysqld` e escuta em 127.0.0.1:3306; nenhum diretório `MfxMariaDbLifecycle-*` sob Temp. A instância operacional não foi usada pelo teste.
- [ ] Isso não valida CI hospedada, Linux, a DLL/runtime do servidor instalado nem produção; formal continua 2/7, sem crédito/release.

# Revalidação TMDB: preferência de idioma e fallback de capas — 2026-10-27 08:45 BRT
- [x] Testes focados `TmdbUtilsTests|ItemImageProviderTests|ProviderManagerImageProviderTests|MetadataServiceTests|TmdbMovieImageProviderFlowTests`: 144 aprovados, 0 ignorados/falhas em Windows/.NET 10 Release.
- [x] Suíte completa `Jellyfin.Providers.Tests`: 884 aprovados, 13 integrações externas ignoradas, 0 falhas/897.
- [ ] Não houve chamada ao TMDb/CDN, inspeção/refresh de *Invasão Zumbi 2*, nem validação de imagem persistida/interface, runtime, produção ou CI. Imagens antigas sem proveniência (`ProviderName` ausente) seguem protegidas de substituição automática por poderem ser arte manual; requerem recuperação administrativa explícita. Formal 2/7, sem crédito/release.

# Restore ACL — revalidação da política padrão escolhida — 2026-10-27 08:41 BRT
- [x] A política já implementada corresponde à escolha: dono UID/serviço e root são confiáveis; escrita por group/other, named users não confiáveis e filesystem compartilhado/desconhecido falham fechados. No Windows, a fonte valida owner/DACL e NTFS/ReFS.
- [x] `BackupRestoreDirectoryTests` Release no Windows: 43 aprovados, 11 ignorados, 0 falhas/54. Os 11 skips detalhados são casos Linux de ACL POSIX, symlink/rename e bind mount; não são aprovações de Linux.
- [x] Contrato do CI `ci-test-runner-serialization.test.mjs`: 2/2; o job Ubuntu privilegiado seleciona os dois testes ACL Linux (named user e other-write) e exige resultado `Passed` para todos os 14 casos esperados.
- [ ] Execução Linux efetiva de ACL padrão/nomeada, syscalls e filesystem/mount continua pendente, assim como CI atual, runtime e produção. Formal permanece 2/7; sem crédito/release.

# NextUp — fusão de leitura dos dados assistidos — atualização 2026-10-27 08:37 BRT
- [x] Com `includeWatchedForRewatching`, a leitura com `UserData` agora calcula numa passagem a posição assistida mais alta e a data mais recente por série. As linhas de mesmo item com `CustomDataKey` distintas são preservadas na comparação da data.
- [x] Sem rewatch, mantém-se o filtro `Any(UserData)` e a seleção anterior. Os filtros de acesso e de episódio/temporada continuam na consulta; o limite de candidatos não mudou.
- [x] `NextUpQueryOptimizationTests`: 8/8 Release. Suíte `Jellyfin.Server.Implementations.Tests`: 1.735 aprovados, 95 ignorados, 0 falhas/1.830, Windows/.NET 10 Release.
- [ ] Tradução/plano MariaDB e benchmark não foram executados. A redução de uma leitura/materialização no ramo de rewatch não prova menor latência em produção; seleção relacional de candidatos, contagem/paginação e homologação formal continuam pendentes (2/7, sem crédito).

# NextUp — requisitos para seleção relacional sem regressão — 2026-10-27 08:09 BRT
- [x] Revisados chamadores e contratos: `GetNextUpBatched` recebe todas as chaves no NextUp geral; `GetResult` calcula `TotalRecordCount` materializando a lista completa antes de aplicar `StartIndex` e `Limit`.
- [x] `GetNextUpEpisodesBatch` aplica filtros de acesso, séries, episódios especiais, virtualidade, reproduzidos e rewatch. A seleção preserva `OrderBy`/`ThenBy` estáveis; o teste diferencial existente cobre posições nulas e empates.
- [ ] Não aplicar `Take` por série ou global antes de contar/ordenar. Qualquer redução SQL precisa definir desempate estável para posições/LastPlayedDate repetidos e manter as semânticas de nullable; medir tradução, plano, linhas lidas, latência e memória em MariaDB.
- [ ] Auditoria estática apenas nesta sessão; sem SQL/MariaDB/benchmark. Homologação formal 2/7 (28,6%), sem crédito.

# Regressão de ordenação de candidatos NextUp — 2026-10-27
- [x] Teste diferencial compara a seleção em passagem única com `OrderBy`/`ThenBy` estáveis, cobrindo múltiplas séries, temporadas/episódios nulos e posições empatadas.
- [x] `NextUpQueryOptimizationTests`: 8 aprovados, 0 ignorados, 0 falhas em Windows/.NET 10 Release; `dotnet format style --verify-no-changes` e `git diff --check` passaram.
- [ ] MariaDB, plano SQL e benchmark não foram executados nesta verificação; seleção relacional e homologação formal permanecem pendentes.

# NextUp — requisito de contagem e paginação — auditoria 2026-10-27
- [x] `NextUpQuery.EnableTotalRecordCount` é `true` por padrão; `TVSeriesManager.GetNextUpBatched` reúne e ordena os resultados antes de `GetResult`, que materializa tudo quando a contagem está habilitada.
- [x] Um limite aplicado antes do cálculo pode mudar `TotalRecordCount`, `StartIndex`, `Limit`, a ordem com rewatch e o conjunto de episódios devolvido. A seleção relacional precisa preservar esse contrato.
- [ ] Planejar a consulta relacional sem truncar o conjunto usado para total/paginação; depois medir carga MariaDB. Esta sessão foi inspeção estática, sem alteração de código.

# NFO do dashboard com falha ao carregar usuários — validação 2026-10-27 08:06 BRT
- [x] A rota `nfo.tsx` depende apenas da configuração para sair do loading; usuários ausentes/erro não bloqueiam o formulário e a opção `Nenhum` continua disponível.
- [x] `nfo.test.tsx`: 3/3 passaram em Vitest; ESLint focal passou. O caso cobre configuração carregada, `useUsers` em erro e API autenticada pelo `ApiProvider`.
- [ ] Teste visual/manual com servidor real não executado. Item da rota web validado localmente; homologação formal do servidor permanece 2/7 (28,6%), sem crédito.

# P1 NextUp — processamento de candidatos em passagem única — atualização 2026-10-27 07:52 BRT
- [x] `NextUpService` não guarda mais listas completas de episódios assistidos/não assistidos, grupos de candidatos ou ordenações por série; mantém somente o candidato escolhido por série durante a enumeração.
- [x] Testes NextUp focais: 6/6 passaram em Windows/.NET 10 Release, incluindo seleção do episódio não assistido mais cedo e rewatch após a posição reproduzida mais recentemente. Suíte completa Implementations: 1.733 aprovados, 95 ignorados, 0 falhas/1.828. `dotnet format style --verify-no-changes` nos dois arquivos e `git diff --check` passaram.
- [ ] As consultas ainda leem as linhas candidatas de todas as chaves; seleção relacional/limite geral e medição de plano, latência e memória em MariaDB continuam pendentes. Nenhum dado de produção acessado; formal 2/7, sem crédito/release.

# Auditoria P1 do lote NextUp — atualização 2026-10-27 07:45 BRT
- [x] Revisados os chamadores: o NextUp geral consulta todas as séries com atividade recente sem limite geral; o fluxo por ID de série limita a uma chave.
- [x] `GetNextUpSeriesKeys` só projeta chaves, mas `GetNextUpEpisodesBatch` materializa todos os episódios assistidos, candidatos não assistidos e datas de rewatch das chaves recebidas; agrupamento/ordenação ocorrem em memória.
- [ ] Projetar seleção relacional dos episódios necessários sem alterar especiais/rewatch; validar tradução e resultados em MariaDB e medir plano, latência e memória. Nenhum código de produto alterado nesta auditoria.

# Deadlock MariaDB repetido em persistência de imagens — atualização 2026-10-27 07:42 BRT
- [x] Anexo SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, igual ao incidente anterior. Repete erro MariaDB 1213 no `DELETE BaseItemImageInfos` para o mesmo item, com chamadas concorrentes; HTTP 200 do pedido não confirma commit de imagens.
- [x] Fonte local serializa `SaveImagesAsync` por stripe local e advisory `GET_LOCK` por stripe de schema/item; regressão `ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests` passou 43/43 em Windows/.NET 10 Release.
- [ ] Preparação de MariaDB descartável foi bloqueada pela política da ferramenta antes da execução; nenhuma instância ou datadir foi criado. Runtime emissor não identificado, CI hospedada sem novo run, produção e Linux pendentes. Homologação formal 2/7 (28,6%) e auditoria 5.465/5.493 sem crédito; sem release.

# Regressão ampla .NET Release — atualização 2026-10-27 07:27 BRT
- [x] `dotnet test MulletaFlix.sln --configuration Release --no-restore --no-build --filter 'FullyQualifiedName!~Integration' --verbosity quiet -m:1 --results-directory <TEMP> --logger trx`: exit 0; 17 TRX, 5.450 aprovados, 98 NotExecuted, 0 falhas/5.548 resultados brutos. Os 15 assemblies com casos passaram; os 2 assemblies Integration não tiveram casos selecionados pelo filtro.
- [x] TRX classificam os 98 NotExecuted: 38 MongoDB opt-in, 32 MariaDB opt-in, 25 plataforma/SO, 2 benchmarks e 1 `/dev/shm`. Não tratados como aprovações.
- [x] Não alterar agregado distinto 5.465/5.493 nem homologação formal 2/7 (28,6%): execução bruta inclui opt-ins e não equivale à união deduplicada das execuções complementares.
- [ ] Linux/ACL, CI hospedada (bloqueio de cobrança), runtime instalado e produção continuam pendentes. Sem alteração de runtime ou release.

# Gate concorrente de imagens e diagnóstico da CI — atualização 2026-10-27 07:11 BRT
- [x] Novo teste MariaDB `SaveItems_ConcurrentWithDeleteItemForSameItem_CompletesWithoutOrphanedImages`: 1/1 em MariaDB 11.4.4 descartável, porta 49387; a classe `ServerPersistenceMariaDbTests` passou 26, ignorou 1 cenário que exige reinício externo e falhou 0/27 com `max-connections=500`. A tentativa inicial com o limite padrão falhou 7 testes por `Too many connections`; foi repetida com limite ampliado no banco temporário. O processo foi encerrado e somente PID 31236 segue em 3306; o datadir descartável ficou em `%TEMP%\mfx-audit-mariadb-20261027` porque a remoção recursiva foi bloqueada pela ferramenta.
- [x] Anotação do GitHub confirmou a causa externa: `The job was not started because your account is locked due to a billing issue.` Run CI `39608614450` não iniciou os 13 jobs; release preflight falhou e Linux/publicação foram skipped. Nenhum teste hospedado executou.
- [ ] A conta GitHub precisa ser desbloqueada pela resolução de cobrança para que CI hospedada produza evidência. Linux/ACL, runtime instalado e produção permanecem sem validação; formal 2/7, auditoria 5.465/5.493, sem crédito/release.

# Persistência MariaDB e startup após restore — atualização 2026-10-27 06:52 BRT
- [x] `StartupBackupRecoveryTests.CreateHost_ReplaysInterruptedRestoreBeforeMigrationsAndCompletesStartup`: recovery/journal, migrations reais, `InitializeServices`, startup tasks e `/System/Info/Public` no host; MariaDB 11.4.4 descartável, teste 1/1.
- [x] Isolamento do schema IntroSkipper por teste e limpeza em teardown; `IntroSkipperMariaDbTests` passou 12/12 e schema padrão de produção não foi removido.
- [ ] Executar gates Linux/ACL e CI hospedada; a investigação do workflow 39608614450 segue pendente, pois a integração não forneceu steps/logs.
- Auditoria formal permanece 2/7; execução Windows local não concede sign-off, instalação ou release.
# Reenvio do deadlock MariaDB em imagens — 2026-10-27 05:59 BRT
- [x] Anexo conferido: SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, exatamente igual ao log já auditado. Erros 1213 ocorrem em `DELETE BaseItemImageInfos` para o mesmo item; o trecho não tem PID, versão nem desfecho.
- [x] `ItemPersistenceServiceTests` passou 37/37 em Release. A fonte atual serializa imagens por stripe local e advisory lock MariaDB; mensagem de retry no log é diferente da atual, logo não comprova que veio desse checkout.
- [ ] Não validar MariaDB concorrente/produção sem banco descartável e identificação segura do runtime. Instalação e produção continuam sem confirmação; formal 2/7, auditoria 5.465/5.493, sem crédito/release.
# Reconciliar DLL instalada e fonte no deadlock de imagens — 2026-10-27 02:04 BRT
- [x] DLL instalada é 12.1.35, gravada em 21/10/2026 10:15:24, SHA-256 `6BCB074F679722F2F1D547E94DC40A81B8BAF90F4261D07235F1340B044C60BD`; saída local .NET Release é 12.1.32, gravada em 27/10/2026 01:54:20, SHA-256 `E3386F4F98F7D0D1DB3D6425BD48F77B7422B420BB27620F4953C24E4730CCE1`.
- [x] Não há processo MulletaFlix/Jellyfin ativo para correlacionar. A DLL instalada não pode ser tratada como build das alterações locais.
- [ ] O log histórico não tem PID/versão/hash; não é possível identificar seu binário emissor. Integrações exigem MariaDB descartável explicitamente configurada; CI hospedada e produção continuam pendentes. Formal 2/7, sem crédito/release.

# Reauditoria dos escritores de BaseItemImageInfos — 2026-10-27 02:03 BRT
- [x] Todos os escritores de runtime encontrados estão em ItemPersistenceService: SaveImagesCoreAsync, UpdateOrInsertItemsCore e DeleteItemsUnderLocks. Cada fluxo coordena as stripes locais e o advisory lock do banco antes das operações transacionais.
- [x] FixBaseItemImageInfos, a rotina de migração restante, adquire as 16 advisory locks antes de alterar as linhas. Testes de integração existentes cobrem refresh concorrente, SaveItems + SaveImagesAsync e DeleteItem + refresh.
- [ ] Integrações MariaDB não foram executadas nesta sessão: MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING não configurada, e os cenários devem usar um banco descartável explícito. O log repetido não identifica um writer fora do processo nem o runtime instalado; CI/runtime/produção pendentes. Formal 2/7, sem release.

# Deadlock de gravação de imagens — registrar exaustão do retry — 2026-10-27 02:00 BRT
- [x] Após a última retentativa transitória, `SaveImagesAsync` registra erro terminal com item, quantidade de tentativas, PID e versão do assembly; a exceção original continua sendo propagada.
- [x] `ItemPersistenceServiceTests`: 37/37 aprovados em Windows/.NET 10 Release; `git diff --check` passou nos dois arquivos tocados.
- [ ] A fixture simula a falha no momento de abrir o contexto; deadlock real sob concorrência MariaDB, correlação do runtime instalado, CI hospedada e produção continuam pendentes. O log anexado permanece duplicado e não prova falha da fonte atual. Formal 2/7 (28,6%), sem crédito/release.

# B-5 — validar limite LRU do stream Nebula — 2026-10-27 01:12 BRT
- [x] Corrigida documentação stale: o limite atual já é 8 chunks (~8 MiB por stream cheio), não 64 MiB.
- [x] Adicionada regressão que lê nove chunks e confirma nova busca do primeiro após eviction LRU; teste focal Release passou 1/1.
- [ ] Medir memória agregada/runtime em carga; runtime instalado/produção seguem sem prova. Homologação formal 2/7, sem crédito/release.

# B-4 — concorrência ao abrir streams Live TV — 2026-10-27 01:12 BRT
- [x] Provider fornece chave de concorrência; default usa o token, Live TV usa ID da fonte compartilhável. O lock global de ownership não cobre a chamada de rede. A revisão adversarial refutou a primeira versão só por token; a implementação final coordena tokens distintos que compartilham uma fonte.
- [x] Testes determinísticos confirmam paralelismo de chaves diferentes e que tokens distintos com a mesma chave veem/reutilizam o stream registrado; `MediaSourceManagerTests` 33/33 e mapeamento da chave Live TV 3/3.
- [ ] Validar limite de tuners por host com fontes diferentes, providers Live TV reais, carga e runtime/produção. Homologação formal segue 2/7 (28,6%), sem crédito/release.

# B-3 — reduzir contenção por item na persistência de progresso — 2026-10-27 00:51 BRT
- [x] `UserDataManager.SaveUserDataAsync` usa `AsyncKeyedLocker<Guid>` por `ItemId`; operações de itens diferentes deixam de competir pela mesma stripe.
- [x] Regressão determinística pausa uma gravação e prova que outra, com colisão na antiga stripe, termina sem esperar; regressão `UserDataManagerTests|SessionManagerTests` passou 15/15 Release.
- [x] `dotnet format style` direcionado passou nos dois projetos. Interface e semântica da transação/cache/evento/reidratação preservadas; `UserDataManager` singleton agora descarta o locker.
- [ ] B-3 parcial: medir a reconsulta de todas as linhas para `item.UserData`, executar perfil/carga MariaDB e validar runtime/produção; formal permanece 2/7, sem crédito ou release.

# Reauditoria da telemetria de respostas lentas — 2026-10-26 23:20 BRT
- [x] `ResponseTimeMiddlewareTests` passou 7/7 em Windows/.NET 10 Release; `Information` funciona com Debug desligado, com redação, supressão e teto do mapa cobertos.
- [x] O P0 O-1 está resolvido na fonte/testes. A auditoria de performance foi corrigida, sem duplicar o defeito como pendente.
- [ ] Binário/configuração/log de produção não verificados. Homologação formal segue 2/7 (28,6%), sem crédito/release. Ver vault [[Sessões/2026-10-26-MulletaFlix-slow-response-telemetry-reaudit-2320]].
## Restore — reexecução após morte do processo — 2026-10-26 23:18 BRT

`dotnet test MulletaFlix-master/tests/Jellyfin.Server.Implementations.Tests/Jellyfin.Server.Implementations.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~BackupServiceTests.RestoreBackupAsync_RecoversAfterRestoreProcessIsTerminated` passou 1/1 em Windows/.NET 10 Release. O teste existente cria ZIP com manifest e payload de 64 MiB, encerra abruptamente o processo filho após criar staging, confirma journal/temporário, inicia outro processo para `RecoverInterruptedRestoreAsync`, compara todo o payload restaurado e verifica limpeza do journal/sidecars. Isto prova replay real em processo filho nesse cenário Windows. Não valida ACL POSIX, inicialização integral por `Program` nem integração HTTP no mesmo ciclo. Homologação formal segue 2/7 (28,6%), sem crédito/release. Ver evento portal `server-restore-process-crash-replay-2026-10-26-2318` e sessão `2026-10-26-MulletaFlix-restore-process-crash-replay-2318`.
## Restore — rota HTTP, agendador e shutdown do TestServer — 2026-10-26 23:12 BRT

`BackupControllerAuthorizationHttpTests` passou 2/2 em Release com `BackupService` concreto. Via `POST /Backup/Restore`, autenticação Administrator, verificou 204, caminho sanitizado, `ShouldRestart`, notificação ao host e encerramento do host `TestServer` via `IHostApplicationLifetime` real. `ProgramStartupRecoveryTests` passou 8/8 separadamente e prova recovery→initialize pelo probe subprocessual. O HTTP usa um arquivo de texto dummy: não valida ZIP, replay, restart de processo nem reabertura do servidor real. Avisos do build são preexistentes em outros arquivos do projeto de testes. Homologação formal 2/7 (28,6%), sem crédito/release. Ver evento portal `server-restore-http-entrypoint-test-2026-10-26-2312` e sessão `2026-10-26-MulletaFlix-restore-http-startup-recovery-test-2312`.

## Gate privilegiado Linux restore — contrato de seleção — 2026-10-26 23:08 BRT

`node --test tools/release/tests/ci-test-runner-serialization.test.mjs` passou 2/2. O contrato verifica os 14 nomes únicos do filtro do workflow contra a lista esperada do TRX e confirma que os 14 métodos estão declarados como xUnit `[Fact]`/`[Theory]` em `BackupRestoreDirectoryTests.cs`/`BackupServiceTests.cs`. Isto valida a ligação estática, não a execução Linux. WSL não tem distribuição; Docker e Podman não estão disponíveis. O Actions público mais recente observado (CI `39608614450`, commit `d51f4f38d2b4148ab49394c001567ad273bd57aa`) falhou e não expôs passos/logs do job servidor; portanto não é possível concluir que o gate correu. O checkout local `64a33dcf5595fb6f429c65206be20dbdd4ae0bd0` está oito commits atrás e a mudança local não está no CI remoto. Também falta teste integrado HTTP→agendamento/restart→recuperação no startup. Homologação formal 2/7 (28,6%), sem crédito/release.
# Revalidação do deadlock repetido em imagens — 2026-10-26 23:02 BRT

- [x] SHA-256 53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2 confirma que o anexo repete log anterior; não traz identidade do processo, resultado final do retry nem relatório InnoDB.
- [x] Fonte atual mantém stripe estática por ItemId e advisory MariaDB antes da transação; testes unitários focais passaram 52/52 em Windows/.NET 10 Release.
- [ ] Integração MariaDB nesta sessão, correlação do runtime emissor, CI hospedada e produção pendentes. Não alterar os agregados: auditoria 5.465/5.493; formal 2/7 (28,6%). Sem release/publicação remota. Ver vault [[Sessões/2026-10-26-MulletaFlix-image-deadlock-duplicate-rerun-2302]].

# Nebula STRM — revalidação do pacote Linux — 2026-10-26 22:56 BRT
- [x] O log de deadlock repetido não é o escopo desta verificação. Auditoria confirma que `control_plane._generate_strm()` importa `generate_strm.py`, já incluído pelo copiador e verificado pelo integrador do stage.
- [x] `tools/release/tests/linux-nebula-runtime-package.test.sh`: passou; pacote copiado contém `generate_strm.py`.
- [x] Python 3.10, em `MulletaFlix-master/Tools/NebulaPython`: `test_control_plane_operations.py` + `test_generate_strm.py`, 27 aprovados.
- [ ] Sem teste de pacote instalado, MongoDB real, mídia real ou CI Linux hospedada. Homologação formal continua 2/7 (28,6%), sem crédito/release.
- Portal atualizado com evento `server-nebula-strm-linux-package-audit-2026-10-26-2256`; contrato `node --test portal-site/tests/homologacao.test.mjs` passou 318/318 após atualização. Mapa conferido, sem mudança estrutural. Ver sessão `2026-10-26-MulletaFlix-nebula-strm-package-audit-2256`.

# Restore P0 — reexecução focal Windows — 2026-10-26 22:52 BRT
- [x] Anexo de deadlock comparado por SHA-256: `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, idêntico ao incidente já auditado.
- [x] `dotnet test MulletaFlix-master/tests/Jellyfin.Server.Implementations.Tests/Jellyfin.Server.Implementations.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~FullSystemBackup' --logger 'console;verbosity=minimal'`: 136 aprovados, 16 ignorados, 0 falhas/152 em Windows/.NET 10 Release.
- [ ] Os 16 ignorados incluem cenários Linux/macOS; WSL não tem distribuição instalada. Gate Linux privilegiado hospedado, runtime emissor do log e produção continuam pendentes. Formal 2/7 (28,6%), sem crédito/release.
- Portal atualizado com evento `server-restore-windows-focused-rerun-2026-10-26-2252`; contrato `node --test portal-site/tests/homologacao.test.mjs` passou 318/318; JSON e IDs únicos validados; `git diff --check` passou com avisos apenas de normalização de fim de linha. Mapa técnico conferido, estrutura inalterada. Ver sessão `2026-10-26-MulletaFlix-restore-windows-focused-rerun-2252`.

# T5.6 — teste permanente do ciclo MariaDB embarcado — 2026-10-26 21:52 BRT
- [x] Adicionado teste de regressão start-stop-second-start com porta dinâmica, schema aleatório exclusivo e datadir temporário; a linha de controle foi confirmada após o segundo start; a inicialização falha fechada se o processo da porta não pertencer ao teste; cobre o MariaDbProcessManager real em Windows.
- [x] CI Windows baixa/stageia MariaDB 11.4.4 ao lado do assembly e exige exatamente um resultado Passed no TRX; contrato Node verifica job, runtime, filtro e fixture.
- [x] Validação local Windows/.NET 10 Release: focal 1/1; suíte Jellyfin.Server.Tests com runtime stageado: 87/87 Passed, 0 NotExecuted, 0 Failed; sete contratos CI 16/16; portal 312/312; git diff --check aprovado.
- [ ] CI hospedada, runtime instalado, upgrade de schema sob carga e produção pendentes. Evidência suplementar: auditoria agregada preservada em 5.465/5.493; homologação formal segue 2/7 (28,6%), sem crédito/release.
# T5.6 — smoke do lifecycle real do MariaDB embarcado — 2026-10-26 21:45 BRT
- [x] Start a frio, stop gracioso e segundo start concluíram 1/1 usando os binários MariaDB locais, porta efêmera e datadir exclusivo temporário; processo e dados temporários foram removidos/verificados.
- [x] `MariaDbStartupMutexTests` passou 4/4 em Windows/.NET 10 Release. O smoke C# era temporário e foi removido; nenhuma fonte de produção foi modificada.
- [ ] Não valida upgrade de schema sob carga, CI hospedada, pacote/runtime instalado completo nem produção; formal continua 2/7 (28,6%), sem crédito ou release.

# Reauditoria do deadlock de imagens e ACL do restore — 2026-10-26 21:29 BRT
- [x] SHA-256 do anexo `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2` confirma que é o mesmo log histórico; sem PID/assembly nem desfecho dos retries. A fonte atual mantém lock por item e advisory `GET_LOCK` antes da transação; isso não identifica o runtime que emitiu o log.
- [x] Cavecrew revisou a exceção Windows para pais do namespace. Ela já está documentada na decisão de 24/10: permite somente criar subdiretórios sob handles validados; nega criação de arquivos e direitos de substituição. O diretório final segue a regra estrita. Não reverti essa exceção sem evidência de violação do contrato aprovado.
- [x] `BackupRestoreDirectoryTests` Windows/.NET 10 Release: 39 aprovados, 10 ignorados por plataforma, 0 falhas.
- [ ] Linux nativo, CI hospedada, runtime emissor do log e produção continuam sem evidência; homologação formal 2/7 (28,6%), sem crédito ou release.
- Portal atualizado; contrato de homologação executado após o registro. Mapa técnico conferido, sem mudança estrutural. Ver sessão `2026-10-26-MulletaFlix-deadlock-and-restore-acl-reaudit-2129`.

# T5.6 — auditoria de writers e locks das imagens — 2026-10-26 20:53 BRT
- [x] Revisados todos os writers de produção de `BaseItemImageInfos`: `SaveImagesAsync`, `SaveBaseItemEntities` dentro de `UpdateOrInsertItemsCore`, deleção de itens e migração `FixBaseItemImageInfos`. Nenhum writer de runtime foi encontrado sem sincronização local e advisory MariaDB.
- [x] A migração `FixDates` não usa stripes locais, mas toma os 16 advisory locks MariaDB. Em providers sem advisory locks, seu estágio `CoreInitialisation` ocorre antes de `InitializeServices`, portanto a inicialização normal precede os writers da biblioteca.
- [ ] Runtime instalado, correlação com log novo, CI hospedada e produção continuam pendentes; esta auditoria estática não prova o comportamento do binário instalado.
- Sem alteração de código; contrato do portal passou 304/304. Agregados permanecem 5.465/5.493 e formal 2/7 (28,6%), sem crédito ou release. Ver sessão `2026-10-26-MulletaFlix-image-persistence-writer-lock-audit-2053`.

# T5.6 — reenvio de deadlock sem processo servidor ativo — 2026-10-26 20:50 BRT
- [x] SHA-256 do anexo `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2` confirma que é o mesmo log de 23/10, sem evidência nova; erro MariaDB 1213 continua em DELETE concorrente de `BaseItemImageInfos`, e tentativas não têm desfecho no trecho.
- [x] Inspeção local de processos/serviços não encontrou processo MulletaFlix/Jellyfin ativo. Nenhum banco foi acessado e nenhum serviço foi iniciado ou reiniciado.
- [ ] Sem PID/assembly histórico, não é possível atribuir o log à DLL instalada 12.1.35 ou à fonte local. Runtime, correlação com log novo, CI hospedada e produção seguem pendentes.
- Agregado técnico permanece 5.465/5.493; homologação formal 2/7 (28,6%), sem crédito. Nenhuma release ou instalação.

# Regressão ampla e testhost da API — 2026-10-26 20:33 BRT
- [x] A mesma regressão, serializada com `-m:1`, terminou exit 0: 5.401 aprovados, 97 ignorados, 0 falhas/5.498 em 15 assemblies com testes; dois assemblies Integration foram filtrados sem casos selecionados.
- [x] TRX classifica ignorados: 38 exigem Mongo opt-in, 32 MariaDB opt-in, 25 plataforma/SO, 2 benchmarks. CI define jobs dedicados para Mongo e MariaDB; último run público não chegou a executar steps.
- [x] O crash CLR `0x80131506` apareceu na execução sem serialização; API passou isolada 792/795. A causa não foi determinada. CI unit-test passa a usar `-m:1`, modo que passou localmente; teste de contrato da configuração passou 1/1.
- [x] Contrato portal 304/304. Auditoria agregada 5.465/5.493 e homologação formal 2/7 (28,6%) mantidas.
- [ ] CI hospedada no checkout atualizado segue pendente: último run disponível falhou antes dos steps. Runtime DLL 12.1.35 ainda difere do checkout 12.1.32; log não tem versão/hash emissor.
- Nenhum banco operacional/runtime foi alterado; sem release ou publicação remota. Mapa técnico conferido, estrutura inalterada.

# T5.6 — divergência do runtime instalado para persistência de imagens — 2026-10-26 20:16 BRT
- [x] Inspecionada somente leitura a DLL em `C:\Program Files\MulletaFlix\Server`: 12.1.35, SHA-256 `0D9465B7EC1549CB70C063A2B7B199E8A6B645F6E454CDBFD008A8D0B3F9D30D`; não contém marcadores de advisory lock nem da nova mensagem de identidade de runtime. Checkout declara 12.1.32 e a fonte contém os dois.
- [x] Sem processo MulletaFlix/Jellyfin ativo; serviço Nginx do MulletaFlix ativo. Nenhum banco acessado ou serviço reiniciado.
- [x] Portal atualizado; contrato `node --test portal-site/tests/homologacao.test.mjs` passou 303/303; JSON válido, sem IDs de evento duplicados; diff check passou.
- [ ] Divergência não associa essa DLL ao processo que emitiu o log e não prova que o retry histórico falhou. Precisamos de build/release compatível e identificação do assembly emissor antes da validação de instalação.
- Evidência publicada no contrato local do portal. Auditoria técnica 5.465/5.493 e formal 2/7 (28,6%) permanecem iguais; sem crédito ou release.

# Reenvio repetido do deadlock de imagens — 2026-10-26 20:14 BRT
- [x] Anexo SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2` é o mesmo log de 23/10: MariaDB 1213 ao apagar `BaseItemImageInfos` sob concorrência; sem PID/assembly ou desfecho das retentativas.
- [x] Reexecução `ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests`: 50 aprovados, 0 ignorados, 0 falhas/50 em Windows/.NET 10 Release. Não acessou banco operacional.
- [x] Portal homologação atualizado; `node --test portal-site/tests/homologacao.test.mjs`: 302 aprovados, 0 falhas.
- [ ] Correlação com versão/hash do runtime instalado, CI hospedada atual e produção pendentes. Não há evidência nova de falha após a correção local.
- Evidência suplementar; não somar ao agregado 5.465/5.493. Homologação formal 2/7 (28,6%), sem crédito nem release.

# T5.6 — ownership do mutex no lifecycle async do MariaDB — 2026-10-26 20:05 BRT
- [x] Corrigida a liberação cross-thread: uma thread proprietária dedicada mantém e libera o Mutex nomeado após o shutdown; aquisição mantém suporte a mutex abandonado, fallback e timeout limitado.
- [x] Testes direcionados: 4/4 (ownership após continuação async, chamadas concorrentes de dispose, timeout, cancelamento e abandono); projeto `Jellyfin.Server.Tests` Release: 86/86.
- [x] Contrato do portal 301/301; `dotnet format whitespace` direcionado nos dois projetos e `git diff --check` nos arquivos alterados passaram.
- [ ] Sem runtime instalado, CI hospedada ou produção nesta verificação. Formal 2/7 (28,6%); sem release; auditoria agregada não muda.

# T5.6 — upgrade de schema e persistência MariaDB — 2026-10-26 15:42 BRT
- [x] Novo teste `LatestImageProvenanceMigration_PreservesExistingImageRows`: migrou de `20260926050701_DropUnusedFullTextSearchIndex` para `20261022212236_AddRemoteImageProvenance` e confirmou a preservação do registro de imagem. Passou 1/1 em MariaDB 11.4.4 descartável.
- [x] `ServerPersistenceMariaDbTests`: 24 aprovados, 1 ignorado, 0 falhas (25 total). O skip é o restart sem marcadores nesta execução; restart real continua validado separadamente 1/1.
- [x] MariaDB de teste encerrado; porta 33218 fechada e instância 3306/PID 31236 preservada. O fluxo `Program` aguarda `CoreInitialisation` antes de `InitializeServices`; tráfego concorrente durante schema upgrade não é um gate representativo do startup normal.
- [ ] CI hospedada do checkout atual, carga de escritas/DELETE durante o runtime, runtime instalado e produção seguem pendentes. Formal 2/7 (28,6%), sem release.

# T5.6 — restart MariaDB durante persistência de imagens — 2026-10-26 15:29 BRT
- [x] Retry de `SaveImagesAsync` ampliado para dez tentativas em falhas de conexão; deadlock e lock timeout (1205/1213) continuam com três.
- [x] Teste integrado exige sinal durante persistência, reinício do mesmo MariaDB 11.4 isolado, readiness e confirmação da imagem final; gate CI valida container exclusivo na porta 33099.
- [x] Execução real local aprovada 1/1 com MariaDB 11.4.4 em datadir descartável e processo próprio na porta 33159; dotnet Release terminou em 38,7 s. O processo foi reiniciado durante o trigger `DELETE`; porta 3306 permaneceu no PID 31236 e não foi acessada.
- [x] Unitários focados 35/35; integração de restart 1/1; contrato CI 3/3; contrato do portal 288/288. A integração compilou e passou ligada à instância descartável.
- [ ] CI hospedada, runtime instalado, escrita/DELETE sob carga concorrente e upgrade do schema sob carga seguem pendentes. Produção não validada. Sem release; homologação formal 2/7 (28,6%).

# T5.6 — benchmark sintético do índice de imagens — 2026-10-26 15:00 BRT
- [x] MariaDB 11.4.4 descartável: consulta por `ItemId` em 100.000 linhas, quatro correspondentes; `EXPLAIN` usou índice composto (ref, estimativa 4) e sem índice fez scan (ALL, estimativa 99.881).
- [x] `mysqlslap`, 5 rodadas de 1.000 consultas sequenciais: índice média 0,068 s (0,062–0,079); sem índice 28,809 s (27,547–31,547), ~423x neste cenário sintético.
- [ ] Medir operações de escrita/DELETE, concorrência representativa, restart do MariaDB durante operação e upgrade do schema sob carga. Este número não representa produção; T5.6 aberto, homologação formal 2/7, sem release.

# T5.6 — comportamento na ausência do índice de imagens — 2026-10-26 14:48 BRT
- [x] Teste integrado remove FK e índice composto somente no schema MariaDB descartável; com 2.048 linhas não relacionadas, `EXPLAIN` confirmou `type=ALL` e `key` nula.
- [x] `SaveImagesAsync` persistiu as duas imagens após a remoção do índice; `ServerPersistenceMariaDbTests` passou 23/23.
- [x] MariaDB 11.4.4 de teste em 127.0.0.1:33159 encerrado e porta fechada; 3306 operacional não acessada.
- [ ] Estimativa do otimizador não atingiu o limite arbitrário de 2.048; removido esse limite e mantida a checagem determinística `type=ALL`/chave nula. Medição de latência com/sem índice, restart do banco durante operação e upgrade sob carga seguem pendentes. Formal 2/7, sem release.
# T5.6 — validação do plano seletivo de imagens — 2026-10-26 14:36 BRT
- [x] `SaveImagesAsync_UsesCompositeItemImageIndexForSelectiveLookup` passou 1/1 em MariaDB 11.4.4 descartável. Com 2.048 registros irrelevantes, `EXPLAIN` escolheu `IX_BaseItemImageInfos_ItemId_ImageType` e estimou no máximo quatro linhas.
- [x] `ServerPersistenceMariaDbTests` passou 22/22 com `max_connections=256`; instância descartável na porta 33159 encerrada e porta fechada. Instância operacional na 3306 não foi conectada.
- [x] `dotnet format ... --verify-no-changes` passou. Contrato do portal passou 284/284 após atualização do evento.
- [ ] Primeira execução completa teve 16 aprovações e 6 falhas por limite de conexões da instância de teste; a repetição configurada passou 22/22. Isso não representa falha do código, mas requisito de capacidade para esta suíte.
- [ ] Benchmark de carga, restart MariaDB durante operação, degradação sem índice, upgrade sob carga e CI hospedada seguem pendentes; T5.6 aberto. Formal 2/7 (28,6%), sem crédito/release.

# T5.6 — revalidação de concorrência da persistência de imagens — 2026-10-26 15:57 BRT
- [x] `ItemPersistenceServiceTests` passou 35/35 em Windows/.NET 10 Release.
- [x] `ServerPersistenceMariaDbTests` passou 24/25: 24 aprovados, 1 ignorado, 0 falhas, MariaDB 11.4.4 descartável na porta 33219. Concorrência SaveImages/SaveItems, múltiplos stripes e DeleteItem executada. Restart requer marcadores e já passou separado 1/1.
- [x] Processo/diretório de teste verificados e encerrados; porta 33219 fechada. Banco operacional 3306 não acessado. Anexo repetido; SHA-256 coincide com incidentes anteriores.
- [ ] Execução CI hospedada atual, runtime instalado e produção permanecem pendentes. Formal 2/7 (28,6%); nenhum crédito/release.

# Regressão ampla do servidor — 2026-10-26 16:10 BRT
- [x] Repetição Release sem build/restore: 5.379 aprovados, 96 ignorados, 0 falhas/5.475; 15 resumos de projetos.
- [x] Auditoria de writers: BaseItemImageInfos mutado em ItemPersistenceService; FixDates usa as 16 locks. Sem novo writer desbloqueado encontrado.
- [ ] Fresh run exit 1 sem diagnóstico preservado; projetos Integration, MariaDB/Mongo opt-in, CI hospedada, runtime instalado e produção continuam sem validação nesta rodada.
- Homologação formal 2/7 (28,6%); sem alteração de runtime ou release.

- [ ] Portal fonte atualizado e contrato 292/292; porta local 4173 sem listener, validar visualização renderizada quando o portal local estiver ativo.

# Regressão compilada do servidor — 2026-10-26 16:23 BRT
- [x] Repetição Release com build terminou exit 0; Server.Implementations.Tests 1.693/1.786 (93 ignorados); API 789/792 (3 ignorados).
- [ ] Diagnosticar primeiro exit 1 não foi possível porque a saída completa não foi preservada; teste opt-in MariaDB/Mongo, projetos Integration, Linux/macOS, CI hospedada, runtime e produção seguem pendentes.


# Restore P0 — regressão Windows atual — 2026-10-26 16:32 BRT
- [x] FullSystemBackup recompilado e executado em Windows/.NET 10 Release: 133 aprovados, 15 ignorados, 0 falhas/148.
- [ ] 15 skips incluem Linux/macOS; WSL não instalado, Docker/Podman indisponíveis. Linux/syscalls, ACL/mount efetivos e CI hospedada do checkout seguem pendentes.
- Sem alteração de runtime, sem crédito formal (2/7), sem release. Ver sessão do vault.
- [x] Portal `homologacao-status.json` parseia; `node --test portal-site/tests/homologacao.test.mjs` passou 294/294.

# Regressão completa Nebula — 2026-10-26 16:36 BRT
- [x] Suíte inteira Python passou em Windows/Python 3.10.11 isolado: 723 aprovados, 2 ignorados, 0 falhas/725. Os skips são ramos POSIX de durabilidade cobertos pela implementação Windows `MoveFileExW`.
- [x] Repetição com motivos de skip confirmou os dois casos; venv preexistente reutilizada sem instalar/atualizar dependências.
- [ ] Linux nativo, runtime/serviço instalado, CI hospedada atual e produção seguem sem evidência. A tentativa global foi bloqueada pelo guard. Homologação formal 2/7 (28,6%), sem release.
- [x] Portal atualizado e contrato validado após registrar essa execução.

# Revalidação do log de deadlock — 2026-10-26 16:38 BRT
- [x] O novo anexo é byte a byte o mesmo incidente já registrado (SHA-256 53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2); reexecutados `ItemPersistenceServiceTests` + `ItemPersistenceDatabaseLockTests`: 50/50 em Windows/.NET 10 Release.
- [ ] Log não identifica PID/assembly/versão; não prova que a fonte atual está instalada nem se a gravação final persistiu. MariaDB de produção não acessada. Homologação formal 2/7, sem release.

# T5.6 — regressão MariaDB isolada — 2026-10-26 16:43 BRT
- [x] Classe `ServerPersistenceMariaDbTests` em MariaDB 11.4.4 descartável: 24 aprovados, 1 skip, 0 falhas/25. Cobriu escrita concorrente, DELETE, advisory locks, recuperação de conexão e operações relacionadas.
- [x] Skip: teste de restart do processo exige marcadores próprios. Processo MariaDB 11.4.4 de teste encerrado, porta 33167 fechada; serviço operacional 3306/PID 31236 preservado sem acesso.
- [ ] Carga prolongada/representativa, CI hospedada atual, runtime instalado e produção seguem pendentes. Diretórios de teste permanecem em `%TEMP%`; a ferramenta bloqueou sua remoção recursiva. Formal 2/7, sem release.
- Observação de harness: execução inicial com `--skip-grant-tables` impediu CREATE USER; execução seguinte bateu no limite default de conexões. Repetição com grants ativos e `max_connections=256` passou.

# Auditoria P1 — propriedade do mutex de startup MariaDB — 2026-10-26 19:57 BRT
- [x] Regressão e correção concluídas em 2026-10-26: thread proprietária dedicada mantém/cria/libera o mutex; preserva abandono, cancelamento e timeout no fallback.
- [x] Testes direcionados 4/4; projeto `Jellyfin.Server.Tests` 86/86; contrato portal 301/301.
- [x] Smoke isolado cold start/stop/second start 1/1 registrado em T5.6 (21:52 BRT); não acessou MariaDB operacional.
- [x] Revalidação 26/10 22:43 BRT: MariaDbStartupMutexTests 4/4; teste end-to-end foi ignorado porque o runtime portable não estava staged.
- [ ] Runtime instalado, CI hospedada e produção seguem pendentes; o smoke e unitários locais não dão crédito formal.
- Formal 2/7; sem validação do runtime instalado/produção e sem release.

## CI remota continua sem chegar aos steps — 2026-10-26 20:39 BRT
- [x] Consulta somente leitura à API pública confirmou que o run mais recente observado continua sendo CI `39608614450`, no commit `d51f4f38` de 21/10: 13 jobs terminaram failure em 3–4 segundos, sem steps reportados.
- [x] Release run `39608677913` falhou no preflight de tag, sem steps; empacotamento Linux e publicação ficaram skipped. Nenhum dos cinco runs recentes é posterior a 21/10.
- [x] Branch local `main` está 8 commits atrás de `origin/main`; working tree contém 224 caminhos alterados/não rastreados. O CI remoto não cobre o checkout local atual.
- [ ] Causa da recusa/encerramento anterior aos steps e validação hospedada do checkout atual continuam pendentes. Não atribuir a falha ao código dos jobs; sem release.
- Auditoria agregada permanece 5.465/5.493; homologação formal 2/7 (28,6%), sem crédito. Nenhuma mudança em runtime, banco ou produção.

## Logs dos jobs remotos sem artefato disponível — 2026-10-26 20:42 BRT
- [x] GitHub API de logs consultada para o job de servidor `119251247983` e o preflight `119251465001`; ambos retornaram `404 BlobNotFound`.
- [x] As respostas confirmam indisponibilidade dos logs solicitados; não revelam causa e não provam que build/teste iniciou ou falhou.
- [ ] CI hospedada no checkout atual ainda não executou steps. Não recuperar diagnóstico por API nesta rodada; requer novo run que grave logs e steps.
- Agregados mantidos: 5.465/5.493 automáticos, formal 2/7 (28,6%). Sem crédito, sem release.

## Regressão Release compilada e serial — 2026-10-26 20:46 BRT
- [x] `dotnet test MulletaFlix-master/MulletaFlix.sln --configuration Release --no-restore --filter 'FullyQualifiedName!~Integration' --results-directory %TEMP%/mfx-server-build-regression-20261026-2045 --verbosity minimal --logger trx -m:1`: exit 0 com build.
- [x] 17 TRX: 15 assemblies com casos, 5.401 aprovados, 97 ignorados, 0 falhas/5.498. Os dois assemblies Integration foram filtrados sem casos selecionados.
- [x] Skip reasons: 38 Mongo opt-in, 32 MariaDB opt-in, 25 plataforma/filesystem, 2 benchmarks. Skips continuam fora dos aprovados.
- [ ] Linux, os opt-ins (sem serviços na suíte geral), CI hospedada sobre o checkout atual, runtime instalado e produção pendentes. O run local repete casos já registrados; agregado 5.465/5.493 e formal 2/7 permanecem. Sem release.
# T5.6 — revalidação do anexo e da CI remota — 2026-10-26 20:57 BRT
- [x] SHA-256 do anexo `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2` é idêntico ao log de 23/10; continua mostrando MariaDB 1213 em `DELETE BaseItemImageInfos`, sem desfecho das retentativas.
- [x] GitHub Actions run `39608614450`, commit `d51f4f38` de 21/10: 13 jobs failure, nenhum step reportado. Evidência não identifica a causa nem valida o checkout local.
- [x] Contrato do portal `node --test portal-site/tests/homologacao.test.mjs`: 304/304 aprovados, 0 ignorados/falhas.
- [ ] Checkout local segue 8 commits atrás e 224 caminhos modificados/não rastreados. Correlação do log novo, CI atualizada, runtime e produção pendentes. Sem mudança de código, banco ou serviço; sem release. Agregado 5.465/5.493 e formal 2/7 (28,6%) inalterados.

# T5.6 — comparação do log com o runtime instalado — 2026-10-26 20:59 BRT
- [x] Fonte atual de `SaveImagesAsync` registra PID e versão do assembly nas falhas; o anexo antigo não contém esses campos.
- [x] DLL instalada permanece 12.1.35, sem os marcadores textuais presentes na fonte; SHA-256 `0D9465B7EC1549CB70C063A2B7B199E8A6B645F6E454CDBFD008A8D0B3F9D30D`. Nenhum processo MulletaFlix/Jellyfin ativo; Nginx segue ativo.
- [ ] Essa diferença prova que o runtime instalado não contém o diagnóstico da fonte local, mas não associa o log histórico a essa DLL nem comprova o resultado das tentativas. Não instalar árvore local atrasada/suja. Checkout: 8 commits atrás e 224 caminhos modificados/não rastreados.
- Sem alteração de código, serviço, banco, instalação ou release. CI, runtime atualizado, log correlacionável e produção seguem pendentes; formal 2/7 (28,6%).
- [x] Após a atualização do portal, `node --test portal-site/tests/homologacao.test.mjs`: 304 aprovados, 0 ignorados/falhas.
- [x] Releitura da DLL instalada confirmou também ausência do literal `GET_LOCK` usado pelo `ItemPersistenceDatabaseLock` da fonte atual. O binário instalado não contém esse mecanismo de sincronização; isso ainda não prova que ele emitiu o log antigo sem PID/versão.

# T5.6 — regressão unitária focal no estado atual — 2026-10-26 21:03 BRT
- [x] `dotnet test` Release, `--no-restore`, filtro `FullyQualifiedName~ItemPersistence`: recompilou fonte e passou 50/50, 0 ignorados/falhas. Build emitiu avisos de analisadores/documentação, sem erros.
- [ ] Isso não executa testes integrados MariaDB nem valida concorrência de banco, runtime instalado ou produção. Agregado 5.465/5.493 e formal 2/7 (28,6%) sem mudança; teste é suplementar, sem crédito/release.

# T5.6 — concorrência integrada em MariaDB descartável — 2026-10-26 21:07 BRT
- [x] MariaDB 11.4.4 isolado, datadir temporário, loopback/porta 33259. `DeleteItem_ConcurrentWithImageRefreshes_CompletesWithoutDeadlock`, `SaveImagesAsync_WaitsForDatabaseAdvisoryLockHeldByAnotherSession` e `DeleteItem_WaitsForDatabaseAdvisoryLockHeldByAnotherSession`: 3/3 aprovados.
- [x] MariaDB operacional em 3306/PID 31236 não foi acessado; instância descartável encerrada, porta de teste fechada e variável de conexão removida.
- [ ] Remoção recursiva dos dados temporários foi bloqueada; restam ~150 MB de dados sintéticos sob `%TEMP%` em `mfx-t56-mariadb-20261026-2108`. Carga longa, CI atual, runtime e produção pendentes. Sem crédito agregado/formal, sem release.
# T5.6 — reteste determinístico após reenvio do deadlock — 2026-10-26 21:15 BRT
- [x] O log anexado tem SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, idêntico ao de 23/10; mostra deadlock 1213 em DELETE de `BaseItemImageInfos` e retries sem desfecho, não comprova commit nem versão emissora.
- [x] Recompilação e testes focais em MariaDB 11.4.4 isolado passaram 3/3, zero skips: refresh de imagem mantido bloqueado antes de iniciar `DeleteItem`, espera por advisory lock em SaveImagesAsync e DeleteItem. Processo temporário encerrou e porta 33259 fechou; banco operacional 3306 não acessado.
- [x] Contrato do portal e memória do projeto atualizados; fonte/mapa conferidos sem alteração estrutural.
- [ ] DLL instalada, correlação por PID/versão/hash, CI hospedada, produção e carga prolongada permanecem pendentes. Agregado 5.465/5.493; homologação formal 2/7 (28,6%), sem crédito ou release.
- Ver sessão `2026-10-26-MulletaFlix-t56-deterministic-image-delete-overlap-2115`.
# Auditoria Nebula — inicialização normal e regressão de fila — 2026-10-26 21:23 BRT
- [x] Lifecycle test passou a cobrir `STREAM_ONLY=True/False`; modo full valida cleanup, restauração de uploads, scanner Mongo e workers grandes/pequenos. `stats_reporter` agora tem expectativa explícita para `queue_identity` já gravada pelo código.
- [x] Regressão completa Python 3.10.11 isolada: 724 aprovados, 2 skips POSIX intencionais, 0 falhas. Cavecrew review encontrou e a execução final confirmou asserção de ausência do cleanup no modo streaming-only.
- [ ] Processo de serviço real, Mongo/Telegram descartáveis integrados até estado final do upload, Linux, runtime instalado, CI atual e produção continuam pendentes. A regressão não dá crédito formal nem ao agregado do servidor.
- [ ] Imagens antigas sem proveniência ainda não podem ser distinguidas com segurança de capas manuais; requer fluxo explícito para recuperação sem sobrescrever arte manual.
- Portal local atualizado; sem release/publicação remota. Ver sessão `2026-10-26-MulletaFlix-nebula-full-mode-startup-audit-2123`.
# T5.6 — atualizar imagens remotas sem idioma de origem — 2026-10-26 21:35 BRT
- [x] Refresh agora detecta imagens com provedor remoto conhecido e idioma ausente; seleciona idioma preferido e usa inglês como fallback. Capa sem provedor segue preservada.
- [x] Testes direcionados 4/4; conjunto `ItemImageProviderTests.RefreshImages` 47/47 em Windows/.NET 10 Release.
- [ ] Imagens antigas sem provedor e sem idioma não podem ser distinguidas de arte manual; permanecem sem substituição automática. Necessário fluxo explícito de reparo administrativo.
- [ ] Nenhuma integração TMDb/CDN, banco, runtime instalado, CI hospedada ou produção foi testada. Formal 2/7 (28,6%), sem crédito/release.
# CI do servidor — incluir contrato de serialização na suíte de contratos — 2026-10-26 21:38 BRT
- [x] O workflow CI chamava cinco contratos Node e omitia `ci-test-runner-serialization.test.mjs`, embora este verifique `-m:1` no comando de regressão do servidor. Incluído no comando de contratos existente.
- [x] Seis contratos CI: 14/14 aprovados em Node local.
- [ ] CI hospedada ainda não validou este checkout; o job de restore Linux privilegiado também não foi executado nesta sessão. Sem crédito formal/release; homologação 2/7 (28,6%).
# Restore P0 — proteger os testes privilegiados de Linux no contrato da CI — 2026-10-26 21:40 BRT
- [x] Adicionado contrato que exige os dez testes de fronteira no job Linux com `sudo`, verifica inclusão no filtro e falha se qualquer caso faltar ou não terminar Passed; a verificação TRX já configurada trata Skip como não aprovado.
- [x] O teste novo passou; seis contratos da CI 15/15. Contrato do portal atualizado para 310/310.
- [ ] WSL e Docker indisponíveis neste host; os dez cenários Linux reais e o runner hospedado ainda não foram executados. Restore P0 e formal 2/7 (28,6%) permanecem abertos, sem crédito/release.
# Regressão Release ampla local — 2026-10-26 22:06 BRT
- [x] Solução .NET 10 Release serial, sem rebuild, filtro `FullyQualifiedName!~Integration`: 15 assemblies, 5.403 aprovados, 98 ignorados, 0 falhas/5.501 casos selecionados; exit code 0.
- [x] Dois assemblies de integração não tiveram casos correspondentes ao filtro; skips mantidos como não executados. O teste lifecycle MariaDB foi ignorado sem runtime stageado nesta suíte e já possui evidência focal separada 1/1.
- [x] Anexo recebido nesta sessão comparado por SHA-256: idêntico ao deadlock já registrado; sem PID/assembly ou resultado final novo.
- [ ] Linux nativo, CI hospedada, runtime instalado/produção e formalização 2/7 continuam pendentes. Esta execução não altera o agregado 5.465/5.493 nem gera release.
# MariaDB descartável: regressão de usuário e billing — 2026-10-26 22:11 BRT
- [x] `UserManagerNormalizedUsernameTests|BillingDatabaseTests` passou 31/32 em Windows/.NET 10 Release; 1 caso ignorado porque exige `lower_case_table_names=0`, não disponível no MariaDB Windows.
- [x] MariaDB 11.4.4 isolada em porta dinâmica 60327, bind 127.0.0.1, datadir temporário; processo terminou e porta fechou. MariaDB operacional PID 31236/3306 não acessada.
- [ ] O skip exige Linux; CI hospedada, runtime instalado, produção e homologação formal 2/7 (28,6%) continuam pendentes. Não somar ao agregado auditável 5.465/5.493; sem release.
# Persistência de metadados com lookup O(1) — 2026-10-26 22:19 BRT
- [x] `UpdateOrInsertItemsCore` compara providers por `(ProviderId, ProviderValue)` e campos bloqueados por `Id` usando `HashSet`, removendo buscas `Any` aninhadas sem mudar a semântica de cardinalidade/pertencimento.
- [x] Testes direcionados 2/2; suíte `Jellyfin.Server.Implementations.Tests` Windows/.NET 10 Release: 1.707 aprovados, 94 ignorados, 0 falhas/1.801. Formatter `--verify-no-changes` aprovado. Cavecrew não encontrou regressão; duplicatas não têm caso dedicado.
- [ ] Benchmark antes/depois, CI hospedada, Linux e runtime/produção não validados. Auditoria agregada segue 5.465/5.493; homologação formal 2/7 (28,6%), sem crédito ou release.
# Restore: ampliar gate Linux e endurecer validação ACL — 2026-10-26 22:35 BRT
- [x] Gate privilegiado Linux exige 14 testes de fronteira e parser TRX reprova ausentes, Skip e outcomes diferentes de Passed.
- [x] Corrigida a máscara POSIX aplicada indevidamente a `other::`; agora `user::` e `other::` usam permissões diretas. Testes unitário e Linux real cobrem o caso `other::write` com máscara sem escrita.
- [x] TrustedInstaller segue permitido apenas como owner da âncora Windows; teste real de ACE de escrita continua rejeitando essa concessão. `FullSystemBackup` Release: 136 aprovados, 16 ignorados, 0 falhas/152; `BackupRestoreDirectoryTests`: 42 aprovados, 11 ignorados, 0 falhas/53.
- [ ] Linux real, CI hospedada do checkout atual, ACL de instalação/runtime e produção pendentes; gate restore P0 e homologação formal 2/7 (28,6%) continuam abertos, sem crédito ou release.

# Rechecagem CI hospedada e ambiente Linux — 2026-10-26 23:39 BRT
- [x] GitHub API: `main` permanece em `d51f4f38`; checkout está em `64a33dcf`, 8 commits atrás, sem commits à frente.
- [x] CI run `39608614450`: 13 jobs concluídos como failure sem steps/logs disponíveis; release run `39608677913`: preflight falhou e build Linux/publicação foram skipped. Logs para jobs `119251247983` e `119251465001` retornaram 404 `BlobNotFound`; causa desconhecida.
- [ ] WSL ausente e Docker não instalado neste host. Linux gate, nova CI do checkout e restore integrado ficam pendentes; não atribuir falhas ao código sem logs. Formal continua 2/7 (28,6%).

# Restore — revalidação Windows e revisão independente — 2026-10-26 23:42 BRT
- [x] Rebuild Release atual + `FullyQualifiedName~FullSystemBackup`: 136 Passed, 16 NotExecuted, 0 Failed/152.
- [x] Revisão Cavecrew da transação, diretórios por handle e ACL não encontrou vulnerabilidade adicional confirmada; confirma staging/rollback/sidecars e limites da validação estática.
- [ ] 16 casos de plataforma, incluindo Linux ACL/syscalls/symlink/mount, não executados aqui. CI hospedada sem logs; runtime/produção não conferidos. Restore P0/formal 2/7 permanecem abertos, sem release.

# Restore: impedir resultados TRX duplicados no gate — 2026-10-26 22:38 BRT
- [x] Verificador Linux deixou de converter os resultados em dicionário por nome curto. Agora avalia cada linha TRX, rejeita nomes esperados duplicados, itens inesperados e todos os outcomes diferentes de Passed; quantidade deve ser exatamente 14.
- [x] Contrato Node exige a sincronização exata dos 14 nomes de filtro e TRX e verifica as condições fail-closed do parser.
- [ ] Execução Linux privilegiada e CI hospedada continuam pendentes. Sem crédito formal ou release.

# Revalidação do mutex MariaDB — 2026-10-26 22:43 BRT
- [x] Fonte usa thread proprietária dedicada para adquirir/liberar mutex; cobre timeout, cancelamento e abandono.
- [x] MariaDbStartupMutexTests: 4/4 Passed em Windows/.NET 10 Release.
- [x] Smoke start-stop-start 1/1 executado anteriormente em T5.6; esta sessão não o reexecutou.
- [ ] Runtime instalado, CI hospedada e produção pendentes; sem crédito formal/release. Ver evento server-mariadb-startup-mutex-revalidation-2026-10-26-2243.

- [x] Atualização do portal e git diff --check; contratos portal/CI 319/319 após correção da asserção do evento.

# Smoke start-stop-start reexecutado — 2026-10-26 22:47 BRT
- [x] `MariaDbProcessManagerLifecycleTests.StartStopStart_UsesTheSameIsolatedDataDirectory`: 1/1 Passed com MariaDB 11.4.4 reportado pelo runtime instalado, porta dinâmica e datadir temporário; SQL sobreviveu ao segundo start.
- [x] Processo operacional existente permaneceu intocado; teste confirmou processo próprio obrigatório e usou datadir distinto.
- [ ] Não comparado hash do runtime com ZIP da CI; runtime empacotado, CI hospedada e produção pendentes. Formal 2/7; sem crédito/release.

- [x] Portal local e contratos CI 320/320 após corrigir asserções de texto; git diff --check e JSON válidos.
# Runtime instalado — telemetria O-1 — 2026-10-26 23:23 BRT
- [x] `AppData\Local\MulletaFlix\config\logging.default.json`: `Serilog.MinimumLevel.Default=Information`.
- [x] Binário instalado reporta 12.1.35; fonte local declara 12.1.32. Só `MulletaFlixNginx` estava ativo; processo/serviço principal e logs do servidor não encontrados nesta checagem.
- [ ] Comparar o binário instalado com artefato de build identificável, iniciar/verificar o serviço em runtime e confirmar `Slow HTTP Response` sob carga controlada.
- [ ] Homologação formal segue 2/7 (28,6%); sem crédito ou release.

# Supabase delta sem sort redundante — 2026-10-26 23:28 BRT
- [x] Consulta de presença do catálogo usa `select=id&limit=1`; timestamp usado como watermark continua vindo do checkpoint de backup concluído.
- [x] Removida criação do índice que só atendia `ORDER BY updated_at`; índice remoto pré-existente não é removido.
- [x] `NebulaSupabaseSyncTests` 65/65 e contrato SQL/RLS 1/1, Release.
- [ ] Supabase real não foi consultado; ainda não prova a causa específica do HTTP 500 nem valida o backup end-to-end. Formal 2/7 (28,6%), sem crédito ou release.

# Regressão ampla local — 2026-10-26 23:37 BRT
- [x] Solução .NET Release, 17 TRX: 5.410 Passed, 98 NotExecuted, 0 Failed/5.508.
- [x] Nebula Python 3.14: 724 Passed, 2 Skipped; frontend web: 418 Passed em 75 arquivos; `tsc --noEmit` passou.
- [ ] Os ignorados requerem bancos reais/opt-in, Linux/macOS ou recursos locais; não executar CI hospedada, serviço instalado, produção nem build de produção web nesta rodada. Não alterar agregados do portal nem conceder crédito formal: 2/7 (28,6%).

# B-6 — DTO de playback sem sync-over-async nos fluxos internos — 2026-10-27 01:34 BRT
- [x] `SessionManager` e `SessionController.ReportViewing` aguardam o caminho `GetBaseItemDtoAsync`; o campo Trickplay permanece para as miniaturas do player web.
- [x] Implementação default de `ISessionManager.ReportNowViewingItemAsync` preserva compatibilidade com implementadores existentes; `SessionManager` sobrescreve com fluxo assíncrono.
- [x] Release tests: `SessionManagerTests` 14/14 e `SessionControllerAuthorizationTests` 12/12; formatter direcionado e `git diff --check` aprovados.
- [ ] CI hospedada, runtime instalado e produção não validados; contrato síncrono legado segue disponível. Formal 2/7 (28,6%), sem crédito/release.
- [x] Log anexado tem SHA-256 idêntico ao incidente registrado anteriormente; não inclui PID/versão e não permite vincular o deadlock ao runtime atual.

# B-7 a B-12 — reconciliação de achados da auditoria — 2026-10-27 01:42 BRT
- [x] Fonte conferida: B-7 e B-11 usam `SaveChanges()` em métodos síncronos; busca nos cinco repositórios não encontrou `.GetAwaiter().GetResult()` nem `SaveChangesAsync(default)`.
- [x] B-8 locks de todos os itens em ordem determinística; B-9 atraso só após falha transitória; B-10 membership com `HashSet<Guid>`; B-12 hash memoizado por `Type`.
- [x] `UserDataManagerTests`: 3/3 Passed em Windows/.NET 10 Release. B-3 permanece parcial: cobertura funcional não mede custo da reidratação nem contenção MariaDB.
- [ ] Runtime/produção e carga permanecem pendentes para B-7 a B-12. Homologação formal 2/7 (28,6%), sem crédito/release.
- [x] Auditoria e evento local do portal atualizados; portal ainda não publicado remotamente; mapa técnico conferido, sem mudança estrutural. Ver sessão `2026-10-27-MulletaFlix-performance-audit-reconcile-b7-b12-0142`.

# Reenvio do log de deadlock de imagens — 2026-10-27 01:42 BRT
- [x] O anexo `14e0dd32-7aa9-4278-a883-b4acaa4ced72/Texto colado.txt` tem SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, idêntico aos anexos anteriores. Mostra MariaDB 1213 em `DELETE BaseItemImageInfos` para o mesmo ItemId em múltiplas threads e retries, mas não o desfecho nem PID/versão/assembly.
- [x] Não é evidência nova de regressão após a mitigação no checkout. A fonte protege gravações por item e advisory lock antes da transação; o trecho repetido não identifica qual runtime o produziu.
- [ ] Para concluir causa em execução: coletar log novo com versão/identidade do processo e resultado de cada retry, além do deadlock report do InnoDB; confirmar o binário realmente carregado. Não consultar nem alterar produção sem essa evidência.

# H-2 — estado mutável e chave incompleta no cache HLS — 2026-10-27 01:55 BRT
- [x] Removida a cópia dos timestamps de cada segmento para o `StreamState` cacheado; o callback da resposta usa posição final calculada do request próprio.
- [x] Cache hit aplica seek do request ao estado compartilhado somente sob lock da playlist, imediatamente antes de iniciar o ffmpeg.
- [x] Chave inclui as opções de encode omitidas (incluindo `MaxRefFrames`, `MaxVideoBitDepth`, sample rate, stream copy, segment options e `StreamOptions` determinísticas); valores nullable distinguem ausência de zero.
- [x] Campos textuais da chave usam framing comprimento+valor, evitando colisão quando `Container`/`SegmentContainer` ou opções textuais contêm `|`; regressão cobre dois pares distintos com chave antiga idêntica.
- [x] Release local Windows/.NET 10: `DynamicHlsControllerTests` 7/7 e `StreamStateCacheTests` 18/18; formatter passou. A revisão paralela ficou indisponível por dispositivo offline e não foi contabilizada como aprovação.
- [x] Teste do contrato do portal passou 351/351; JSON válido e `git diff --check` sem erros.
- [ ] Não executado teste HTTP end-to-end com requests HLS simultâneos, cliente de reprodução real, runtime instalado, produção ou carga. Homologação formal 2/7 (28,6%), sem crédito/release.
- [x] Portal e memória do projeto atualizados no checkout; mapa técnico conferido, sem dependência/módulo novo. Ver sessões `2026-10-27-MulletaFlix-hls-cache-state-safety-0155` e `2026-10-27-MulletaFlix-hls-cache-key-delimiter-collision-1340`.

# Validação adicional do deadlock BaseItemImageInfos — 2026-10-27 02:10 BRT
- [x] Log repetido: SHA-256 53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2; registra pelo menos 11 retries simultâneos no mesmo ItemId e heartbeat atrasado 2,35 s, mas sem PID/versão/caller/desfecho.
- [x] Integração Release contra MariaDB 11.4.4 descartável (127.0.0.1:33564): 24 Passed, 1 Skipped, 0 Failed/25; duração 1m14s. O cenário de reinício do processo DB foi ignorado fora de CI.
- [x] MariaDB temporária encerrada e processo instalado em 3306 permaneceu ativo. Nenhum dado operacional foi consultado ou alterado.
- [ ] O teste não reproduz a rajada observada, não valida runtime/produção nem CI remota. Necessário log novo com PID, versão do assembly, resultado terminal e relatório InnoDB para identificar a execução.
- [ ] Homologação formal permanece 2/7 (28,6%); sem crédito/release. Portal atualizado apenas localmente.
- [x] Mapa técnico conferido; sem alteração de estrutura. Sessão: 2026-10-27-MulletaFlix-image-deadlock-mariadb-validation-0210.

# Regressão Release ampla do servidor — 2026-10-27 02:18 BRT
- [x] Solução .NET Release, `-m:1`, filtro `FullyQualifiedName!~Integration`: 15 assemblies, 5.433 Passed, 98 Skipped, 0 Failed/5.531; exit code 0. Skips mantidos como não executados.
- [x] Nebula Python via `uv run --isolated` com lock de produção/teste: 724 Passed, 2 Skipped, 0 Failed em 35,91 s. Tentativa inicial no Python global foi bloqueada pelo guard antes da coleta; o runtime global não recebeu instalação.
- [x] CI local/portal recebeu evidência; resultados TRX por projeto foram sobrescritos por nome repetido, então contagem .NET foi agregada a partir dos 15 resumos console. Saída bruta fica em `%TEMP%` desta sessão.
- [ ] Filtro omite integrações .NET e não valida Linux/CI remota, runtime instalado, serviços reais ou produção. Integração de persistência MariaDB foi executada à parte na sessão anterior. Formal 2/7 (28,6%), sem crédito/release.
- [x] Mapa técnico conferido, sem mudança estrutural. Ver sessão `2026-10-27-MulletaFlix-server-regression-release-0218`.
# Revalidação do anexo MariaDB — 2026-10-27 02:22 BRT
- [x] SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`: log idêntico aos anexos já auditados. Confirma DELETE concorrente em `BaseItemImageInfos`, deadlock 1213 e retry 2; não contém PID/versão nem desfecho terminal.
- [x] Estado local conferido: sem processo MulletaFlix/Jellyfin; apenas o `mysqld` instalado em 3306 está ativo. DLL instalada segue 12.1.35; checkout/build local 12.1.32. Isto demonstra divergência, não identifica o emissor histórico do log.
- [x] A correção local serializa `SaveImagesAsync` por `ItemId` e advisory lock MariaDB; a integração MariaDB descartável foi aprovada 24/25 na sessão 02:10, com 1 teste CI-only ignorado. Regressão ampla .NET e Nebula Python passou na sessão 02:18.
- [ ] Runtime instalado/produção, CI remota e relatório InnoDB para uma ocorrência nova continuam pendentes. Homologação formal 2/7 (28,6%); sem crédito/release. O portal local recebeu evento de revalidação; sem publicação remota.

# CI hospedada bloqueada por cobrança — 2026-10-27 02:25 BRT
- [x] API pública do GitHub consultada: CI `39608614450` e Release `39608677913`, ambas no commit remoto `d51f4f38d2b4148ab49394c001567ad273bd57aa` (21/10/2026). O endpoint de annotations informa: “The job was not started because your account is locked due to a billing issue.” Os jobs falharam em 3–4 segundos, runner ID 0, steps vazios; logs de job não estão disponíveis.
- [x] `origin/main` aponta para `d51f4f38d2b4148ab49394c001567ad273bd57aa`; checkout HEAD `64a33dcf5595fb6f429c65206be20dbdd4ae0bd0`, oito commits atrás. Regressões locais aprovadas não certificam o commit remoto nem o estado atual da branch.
- [ ] Restaurar a capacidade de execução do GitHub Actions resolvendo a pendência de cobrança na conta e disparar CI/release preflight no commit de produção atualizado. Até lá, CI hospedada, artefato e release não homologados; formal do servidor continua 2/7 (28,6%). Portal local atualizado, sem publicação remota.

# Reauditoria do ciclo MariaDB do servidor — 2026-10-27 02:27 BRT
- [x] `Program` aguarda `StartMariaDbAsync` e `StopMariaDbAsync`; `WaitForDatabaseAsync` usa espera `Task.Delay(250, cancellationToken)`, sem polling bloqueante no caminho de prontidão.
- [x] Busca de chamadores: `StopMariaDb` síncrono chama `.GetAwaiter().GetResult()`, mas não tem uso interno encontrado. É wrapper legado público; não removido sem confirmar compatibilidade externa.
- [x] `MariaDbProcessManagerLifecycleTests.StartStopStart_UsesTheSameIsolatedDataDirectory`: Windows/.NET 10 Release, 1 Passed, 0 Skipped/Failed; executou start-stop-start e conferiu dado persistido em diretório efêmero, posteriormente removido pelo teste.
- [ ] Linux/CI hospedada e runtime instalado seguem pendentes; a CI remota está bloqueada pela cobrança da conta. Não atribuir o resultado local à branch remota. Homologação formal 2/7 (28,6%), sem crédito/release. Portal local atualizado.

# Subconsulta de álbuns recentes em MariaDB — 2026-10-27 02:40 BRT
- [x] `GetLatestItemList_Music_UsesMariaDbSubqueryAndDeduplicatesAlbums` passou 1/1 em MariaDB portátil 11.4.4, Windows/.NET 10 Release, porta dinâmica loopback 49270. Tradução SQL, ordenação e deduplicação verificadas; banco operacional 3306 não acessado.
- [ ] EXPLAIN/carga de catálogo representativo, CI hospedada, runtime instalado e produção pendentes. Homologação formal 2/7 (28,6%), sem crédito/release.
- [x] Portal de homologação atualizado localmente; publicação remota não feita. Datadir temporário sintético foi mantido após bloqueio da ferramenta à remoção recursiva.

# Consulta de álbuns recentes — 2026-10-27 02:29 BRT
- [x] `BaseItemRepository.GetLatestItemList(CollectionType.music)` mantém IDs distintos dos álbuns em subconsulta `IQueryable`, sem materializar lista de tamanho catálogo.
- [x] Novo teste de resultado/limite/filtro passou; `BaseItemRepositoryLatestTvShowTests`: 3 Passed, 0 Skipped/Failed em Release.
- [x] Reconciliado E-8: o stream de filmes/séries para no limite de cards distintos, mas não limita linhas SQL; duplicidade alta/ausência de limite pode ampliar leitura. Benchmark e EXPLAIN MariaDB pendentes.
- [x] Log de deadlock nesta conversa tem SHA idêntico ao incidente registrado; sem PID/versão/desfecho e não atribui falha ao checkout atual.
- [ ] Plano/carga MariaDB, CI hospedada, runtime instalado e produção não validados. Formal permanece 2/7 (28,6%), sem crédito/release.
- [x] Portal de homologação atualizado no checkout local; publicação remota não realizada.
# Reauditoria estática da persistência de itens — 2026-10-27 02:46 BRT
- [x] Chamadores internos conferidos: `SaveItems` → `UpdateOrInsertItems` → `UpdateOrInsertItemsCore`. Leituras de itens, providers e campos de metadata são agrupadas; a afirmação de consultas repetidas em loops não foi confirmada e foi retirada do resumo P0.
- [x] Escritas relacionadas podem gerar `ExecuteDelete` por item quando há mudanças em providers, imagens explicitamente informadas, campos bloqueados ou tipos de trailer. É custo condicional, ainda sem perfil representativo que prove gargalo.
- [x] O fluxo usa `SemaphoreSlim.Wait` e persistência EF síncrona; a busca não encontrou `GetAwaiter().GetResult()` nesse caminho. A contenção é risco a medir, não falha demonstrada.
- [ ] EXPLAIN/carga representativa e telemetria de scan pendentes. Nenhum código de produto alterado; homologação formal continua 2/7 (28,6%), sem crédito/release. Ver evento portal `server-item-persistence-static-reaudit-2026-10-27-0246` e sessão `2026-10-27-MulletaFlix-item-persistence-static-reaudit-0246`.
# Correlação do deadlock de imagens — binário instalado e fonte — 2026-10-27 02:51 BRT
- [x] O anexo atual tem SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2` (mesmo log já conhecido): 12 comandos DELETE falhos, 11 deadlocks 1213, 12 retries e um alerta de heartbeat; não há sucesso final nem PID/assembly no trecho.
- [x] Inspeção read-only distinguiu os dois arquivos instalados: `MulletaFlix.dll` 12.1.35, SHA-256 `6BCB074F679722F2F1D547E94DC40A81B8BAF90F4261D07235F1340B044C60BD`; `MulletaFlix.Server.Implementations.dll` 12.1.35, SHA-256 `0D9465B7EC1549CB70C063A2B7B199E8A6B645F6E454CDBFD008A8D0B3F9D30D`. A segunda não contém `ItemPersistenceDatabaseLock`, `mfx:item:` nem o novo template com PID/versão.
- [x] A fonte atual adquire stripe local e lock advisory MariaDB antes da transação; `SaveImagesAsync` mantém a stripe durante retries. `ItemPersistenceServiceTests`: 37 aprovados, 0 ignorados/falhas em Windows/.NET 10 Release.
- [ ] Nenhum processo servidor estava ativo; conexão de integração MariaDB não está configurada. Não é possível atribuir o log ao binário nem validar replay MariaDB, runtime ou produção. Homologação formal 2/7 (28,6%), sem crédito/release. Portal atualizado localmente, sem publicação remota. Ver evento `server-image-deadlock-installed-runtime-correlation-2026-10-27-0251` e sessão `2026-10-27-MulletaFlix-image-deadlock-runtime-correlation-0251`.
# Reauditoria do bootstrap de schema do UserManager — 2026-10-27 02:54 BRT
- [x] `IUserManager.InitializeAsync` tem callers de produção apenas nas rotas `StartupController`; os getters síncronos usam `.GetAwaiter().GetResult()` somente sob `!_schemaInitialized`. Após o guard, o bootstrap não se repete por request. `GetFirstUser` força bootstrap, mas só foi encontrado nas mesmas rotas.
- [x] Corrigida a descrição P1 no roadmap: risco reclassificado para P2 e limitado à primeira consulta síncrona de usuário em cada processo quando o schema ainda não foi inicializado.
- [x] `UserManagerTests|UserManagerNormalizedUsernameTests`: 16 Passed, 27 NotExecuted/Skipped, 0 Failed/43 em Windows/.NET 10 Release.
- [ ] Os 27 skips exigem MariaDB opt-in; não validam schema bootstrap relacional. Startup do host, MariaDB/runtime/produção seguem pendentes. Sem mudança de código; formal 2/7 (28,6%), sem crédito/release. Ver evento portal `server-user-manager-schema-cold-path-audit-2026-10-27-0254` e sessão `2026-10-27-MulletaFlix-user-manager-schema-cold-path-0254`.

# Restore P0 — ancora os diretórios de destino ao handle da raiz — 2026-10-27 04:18 BRT
- [x] `OpenTargetDirectoryCore` agora atravessa os componentes a partir da raiz configurada aberta; caminhos em cache são reabertos sob o pai autorizado e comparados pela identidade do diretório.
- [x] `BackupRestoreDirectoryTests` + `BackupServiceTests` Release: 128 Passed, 16 Skipped por plataforma/ambiente, 0 Failed/144.
- [ ] Linux nativo, ACL/mount reais, races de rename/unlink, CI hospedada, runtime instalado e produção continuam pendentes. Restore P0 e homologação formal 2/7 (28,6%) seguem abertos, sem crédito/release.
- [x] Portal de homologação atualizado localmente com resultado/limites; remoto não publicado. Ver `server-restore-root-anchored-path-traversal-2026-10-27-0418` e sessão `2026-10-27-MulletaFlix-restore-root-anchored-path-traversal-0418`.
# Restore: regressão Windows e contrato integral do gate Linux — 2026-10-27 04:27 BRT
- [x] `FullSystemBackup` Release Windows/.NET 10: 137 Passed, 16 Skipped por plataforma, 0 Failed/153.
- [x] Revisão ACL: TrustedInstaller é exceção somente para owner da âncora; grant de escrita continua negado e já tem teste Windows.
- [x] Contrato dedicado da CI compara os 14 casos entre o filtro Linux privilegiado, expectativa TRX e métodos xUnit; teste focal 2/2.
- [x] Contrato do portal reexecutado: 332/332, com verificações representativas dos três casos já cobertos. Linux nativo/CI hospedada e ACL operacional continuam sem evidência; restore P0 e homologação formal 2/7 (28,6%) permanecem abertos.

# Restore: journal antes de migrações pré-iniciais — 2026-10-27 05:15 BRT
- [x] Recovery do journal e restore solicitado movidos para antes das migrações pré-iniciais; a migração permanece única entre reinícios.
- [x] `GetTranscodePath` (cria diretório se necessário) também ocorre somente após recovery.
- [x] `ProgramStartupRecoveryTests` Release 4/4; teste com archive/journal real Release 1/1; payload verificado antes do callback de migração.
- [ ] Host real/migrações concretas, Linux/ACL, CI hospedada, runtime instalado e produção pendentes; homologação formal segue 2/7, sem crédito/release.
# Auditoria 2026-10-27 — ciclo de vida Nebula e integração restore

- [x] Corrigir shutdown Nebula que podia pular a espera quando `UPLOAD_QUEUE.empty()` ocultava upload já retirado pelo worker; regressão de ciclo de vida 3/3 e suíte isolada 725 aprovados, 2 ignorados.
- [x] Extrair a composição usada pelo startup para `Program.RunAfterBackupRecoveryAsync` e testar o mesmo helper com `BackupService` concreto e sinal explícito de conclusão antes da inicialização. `ProgramStartupRecoveryTests` passou 9/9.
- [x] Integrar journal ativo aos helpers de recovery/continuação usados pelo entry point; provar payload antes da continuação (`RecoverInterruptedRestoreAsync_ProgramStartupGateReplaysJournalBeforeInitialization`, Windows Release 1/1).
- [ ] Validar boot integral do host após restore (migrations/`InitializeServices`), Linux/ACL operacional, CI hospedada, runtime instalado e produção.
- [ ] Manter validação Linux/ACL, runtime instalado e produção pendente; homologação formal permanece 2/7.

## Restore Linux — aceite condicional do modelo de confiança — 2026-10-27 05:35 BRT

- [x] A política escolhida pelo usuário trata UID do serviço e root como principals confiáveis; a fonte ACL Linux nega escrita a group/other e named users não autorizados. Rename por essas identidades não é bypass de traversal por ator não confiável.
- [x] Risco residual explicitado: principal confiável pode renomear a árvore e causar divergência do destino configurado ou falha/integridade. Decisão `2026-10-27-restore-linux-trusted-rename-threat-model` registrada no vault.
- [ ] ACL Linux observável, teste de rename adversarial, CI no checkout atual, runtime instalado e produção continuam pendentes; não fecha homologação nem gate de release.

# Rechecagem read-only do runtime instalado — 2026-10-27 05:37 BRT
- Nenhum processo MulletaFlix/Jellyfin em execução; `MulletaFlixNginx` está ativo.
- DLL instalada `MulletaFlix.Server.Implementations.dll`: versão 12.1.35, 1.774.080 bytes, last-write 21/10/2026 10:15:18 -03. Nenhum banco/serviço foi tocado.
- Isso não correlaciona o log histórico nem valida checkout 12.1.32; nenhuma mudança de runtime ou release.

## Regressão focal do deadlock de imagens — 2026-10-27 05:41 BRT
- [x] `ItemPersistenceServiceTests` + `ItemPersistenceDatabaseLockTests`: 52/52 em Windows/.NET 10 Release, exit code 0.
- [x] Log anexado confirmado como duplicata por SHA-256; MariaDB 1213 durante `DELETE BaseItemImageInfos`, sem PID/versão nem desfecho do retry.
- [ ] Não executar MariaDB contra instância ativa não identificada. Integração descartável, Linux/CI hospedada, runtime instalado e produção seguem pendentes. Homologação formal 2/7 (28,6%), auditoria 5.465/5.493 sem crédito ou release.

## Compilação de integração e regressão ampla — 2026-10-27 05:55 BRT
- [x] Corrigido CS1061 no teste `FixDatesImageMigration_WaitsForAllImageAdvisoryLocks`: adicionado `InternalsVisibleTo("IntroSkipper.Integration.Tests")` ao assembly MulletaFlix. `FixBaseItemImageInfos` permanece internal.
- [x] `dotnet test MulletaFlix.sln --configuration Release --no-restore`: exit code 0. `Jellyfin.Server.Implementations.Tests` 1.729 aprovados / 95 ignorados / 0 falhas.
- [x] `Jellyfin.Api.Tests` 809/3/0; `Jellyfin.Providers.Tests` 884/13/0; execução focada `IntroSkipper.Integration.Tests` 20 aprovados / 48 ignorados / 0 falhas.
- [ ] 48 integrações MariaDB omitidas sem conexão descartável; Linux, Mongo opt-in, CI hospedada, runtime instalado e produção continuam pendentes. Homologação formal 2/7 (28,6%); auditoria 5.465/5.493 sem crédito ou release.

## Restore seguido de boot integral do servidor — 2026-10-27 06:32 BRT
- [x] Gate de integração opt-in prepara journal/arquivos de restore interrompido e verifica conteúdo restaurado e journal removido antes da migração pré-inicial.
- [x] `MariaDbTestEnvironment` agora exige opt-in explícito de instância descartável antes de qualquer teste MariaDB destrutivo; a CI só confirma no job que inicia serviço MariaDB próprio. Testes de validação do endpoint passaram 10/10.
- [x] Com MariaDB 11.4.4 descartável: build de `Jellyfin.Server.Integration.Tests` sem avisos/erros; `StartupBackupRecoveryTests.CreateHost_ReplaysInterruptedRestoreBeforeMigrationsAndCompletesStartup` passou 1/1, incluindo assert explícita do callback pré-migração. O host executou migrations, `InitializeServices`, startup tasks e respondeu com sucesso a `/System/Info/Public`.
- [x] Contrato estático de cobertura MariaDB na CI passou 4/4; portal local atualizado com `server-full-startup-after-restore-mariadb-2026-10-27-0626`.
- [x] Review final: schema de plugin agora é exclusivo por host de teste e removido em teardown; build final 0 warnings/0 errors, startup 1/1, IntroSkipper MariaDB 12/12 e schema fixo ausente ao final.
- [x] CI contract 4/4; portal 334/334; diff check sem whitespace errors. Instância de teste 49374 encerrada e produção 3306/PID 31236 preservada.
- [ ] Linux/ACL, CI hospedada, runtime instalado e produção pendentes. Datadirs descartáveis ficaram em `%TEMP%`; formal 2/7 (28,6%), auditoria 5.465/5.493, sem crédito/release.
# Divergência da correção de persistência de imagens com origin/main — 2026-10-27 07:32 BRT
- [x] `git fetch origin main` atualizou a referência sem tocar a árvore de trabalho: `origin/main` = `d51f4f38` / v12.1.35, 8 commits à frente do HEAD `64a33dcf`.
- [x] O release remoto ainda apaga `BaseItemImageInfos` sem `ItemPersistenceDatabaseLock` e registra retry sem PID/AssemblyVersion. A correção local está em alterações não commitadas no helper, serviço e testes; não foi mesclada/publicada.
- [x] Anexo repetido tem SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`; sem identidade do processo ou desfecho final.
- [x] Contrato local `node --test portal-site/tests/homologacao.test.mjs`: 337/337 aprovados, sem falhas.
- [x] Worktree de correção na base `d51f4f38`/v12.1.35: filtro `ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests` passou 43/43, 0 ignorados/falhas. Build teve avisos de estilo/documentação em outros arquivos.
- [ ] Reconciliar a correção sobre v12.1.35 numa árvore isolada, revisar o diff sobreposto e executar unitários + integração MariaDB descartável; depois requer CI desbloqueada, Linux/ACL, runtime e produção. Formal 2/7, auditoria 5.465/5.493, sem release.

# Persistência concorrente de imagens — validação MariaDB descartável — 2026-10-27 08:19 BRT
- [x] Log SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2` confirma MariaDB 1213 em `DELETE BaseItemImageInfos`; HTTP 200 não confirma commit e o recorte não contém PID/versão nem resultado final.
- [x] `SaveImagesAsync_ConcurrentRefreshesForSameItem_PersistOneCompleteImageSet` passou 1/1 em Windows/.NET 10 Release contra MariaDB 11.4.4 descartável em loopback/porta 51588. A instância isolada foi encerrada; MariaDB operacional 3306/PID 31236 não foi acessado.
- [x] Fonte do checkout serializa gravações por stripe local e `GET_LOCK` MariaDB por schema/stripe antes da transação. O formato do retry no anexo é anterior à mensagem de diagnóstico atual e não identifica o assembly emissor.
- [ ] DLL/runtime do log, CI hospedada, instalação e produção não foram correlacionados; nenhuma release nesta sessão. Formal 2/7 (28,6%) e auditoria 5.465/5.493 permanecem sem crédito.
# Revalidação unitária do deadlock de imagens — 2026-10-27 08:27 BRT
- [x] O anexo recebido tem SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, igual aos registros anteriores; não acrescenta PID/AssemblyVersion nem resultado final das retentativas.
- [x] `dotnet test MulletaFlix-master/tests/Jellyfin.Server.Implementations.Tests/Jellyfin.Server.Implementations.Tests.csproj --no-restore --filter 'FullyQualifiedName~ItemPersistenceServiceTests|FullyQualifiedName~ItemPersistenceDatabaseLockTests'`: 52/52 passaram em Windows/.NET 10 Debug. O build emitiu avisos StyleCop/xUnit em outros arquivos do projeto.
- [x] `node --test portal-site/tests/homologacao.test.mjs`: 337/337; `git diff --check` nos arquivos atualizados: exit 0.
- [x] Revisão NextUp corrigiu a hipótese anterior: o `limit + 10` em `TVSeriesManager` não alcança o fluxo geral; a consulta por `SeriesId` retorna antes. Mantém-se o risco real documentado de materialização de todos os episódios candidatos e a exigência de preservar contagem/paginação.
- [ ] Teste unitário não identifica o runtime do log nem valida produção. O teste MariaDB concorrente 1/1 já consta no evento portal de 08:19; instalação/runtime, CI hospedada e produção pendentes. Não acessar o MariaDB operacional. Formal 2/7 (28,6%), auditoria 5.465/5.493 sem crédito; sem release.
# Nebula — serialização do estado e reanálise de deadlock repetido — 2026-10-27 09:28 BRT
- [x] eed_ftp.mark_seen agora serializa alteração do conjunto e persistência; tomic_write_json protege o arquivo temporário por processo contra escritas concorrentes. Regressão determinística adicionada.
- [x] Suíte NebulaPython: 726 aprovados, 2 ignorados, 0 falhas; teste concorrente repetido 10/10; py_compile e git diff --check passaram. Ruff indisponível; execução pelo Python global foi corretamente bloqueada pelo guard do runtime isolado.
- [x] Anexo MariaDB SHA-256 53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2 é idêntico ao incidente já registrado. Fonte local contém stripe por item, advisory lock MariaDB e retries limitados; o log não identifica PID/assembly nem resultado terminal, logo não permite atribuir a emissão ou confirmar persistência no runtime em uso.
- [ ] Não houve novo teste MariaDB para este anexo duplicado, CI remota, Linux, runtime instalado ou produção. Auditoria agregada não muda; homologação formal 2/7, sem crédito. Nenhuma release/publicação remota.

# NextUp — desempate estável antes do limite — 2026-10-27 10:24 BRT
- [x] `GetNextUpSeriesKeys` ordena agora por `SeriesPresentationUniqueKey` quando `LastPlayedDate` empata, antes de aplicar `Limit`.
- [x] O teste `GetNextUpSeriesKeys_AppliesLimit` usa datas iguais; `NextUpQueryOptimizationTests` passou 8/8 em Windows/.NET 10 Release.
- [x] Revisão adversarial não encontrou erro no diff; identificou limite: suíte usa EF InMemory e não testa tradução Pomelo.
- [ ] Adicionar/verificar integração MariaDB para confirmar tradução/ordenação SQL e medir a seleção relacional pendente dos candidatos de episódio. CI hospedada, runtime e produção sem validação; homologação formal permanece 2/7 (28,6%), sem release.

# NextUp — integrar teste de desempate MariaDB no CI — 2026-10-27 10:30 BRT
- [x] Adicionado teste real de `NextUpService.GetNextUpSeriesKeys` com duas séries, `LastPlayedDate` igual e `Limit=1`; deve confirmar `SeriesA` em MariaDB.
- [x] O job `user-mariadb-linux-tests` e o contrato estático do workflow incluem o novo caso.
- [ ] Execução local compilou e reconheceu o teste, mas foi ignorada sem MariaDB descartável; Docker indisponível. A execução hospedada ainda não foi observada, então tradução Pomelo permanece sem resultado. Sem crédito formal (2/7) ou release.

# NextUp — auditoria da seleção SQL — 2026-10-27 10:58 BRT
- [x] Reexecutado `NextUpQueryOptimizationTests` no Windows/.NET 10 Release: 8 aprovados, 0 ignorados/falhas.
- [x] Cavecrew revisou o query shape sem escrita nem acesso a banco: episódios não assistidos, assistidos e linhas de rewatch continuam enumerados em lote; o índice por tipo/série/temporada/episódio já existe; `GetResult` precisa continuar vendo o conjunto completo antes da paginação/contagem.
- [x] `GetNextUpEpisodesBatch_SelectsEarliestUnplayedEpisodeOnMariaDb` valida a seleção de até um candidato não assistido por série, inclusive especiais, virtuais e posições nulas, e a tradução MariaDB `ROW_NUMBER() ... <= 1`; passou 1/1 na instância portátil descartável.
- [ ] EXPLAIN/ANALYZE, volume/latência/memória em catálogo representativo, rewatch relacional, CI hospedada, runtime e produção seguem pendentes. Cavecrew identificou e o teste agora cobre a semântica do último episódio assistido com posição parcial. Formal 2/7 (28,6%), sem crédito/release.

# MariaDB descartável — prontidão de ciclo local — 2026-10-27 11:03 BRT
- [x] `MariaDbProcessManagerLifecycleTests.StartStopStart_UsesTheSameIsolatedDataDirectory` passou 1/1 em Windows/.NET 10 Release: start, stop, segundo start, leitura do marcador persistido, shutdown e limpeza do diretório temporário.
- [x] Após a execução, a lista de processos contém somente o `mysqld` operacional preexistente; o teste não deixou instância descartável ativa.
- [ ] A integração NextUp continua sem executar Pomelo/MariaDB: o teste de ciclo não valida consulta de candidatos. Próximo passo é provisionar a conexão descartável via fixture sem tocar o processo operacional e executar o teste relacional/EXPLAIN. Formal 2/7, sem crédito/release.

# NextUp — seleção SQL em MariaDB — 2026-10-27 11:24 BRT
- [x] Implementada seleção relacional do primeiro episódio não assistido por série; MariaDB confirma SQL `ROW_NUMBER()` e filtro de ranking `<= 1`.
- [x] Regressão encontrada na revisão Cavecrew foi corrigida: posições de candidatos continuam respeitando o último episódio assistido na ordenação existente; se ele tiver temporada ou episódio nulo, não se aplica corte por posição. Integração descartável passou 1/1.
- [x] Suíte `Jellyfin.Server.Implementations.Tests`: 1.739 aprovados, 95 ignorados de plataforma/ambiente, 0 falhas/1.834 em Windows/.NET 10 Release.
- [ ] Medir plano, latência e memória em catálogo representativo; o `ANALYZE` do fixture sintético da seleção não assistida está registrado na seção 11:45. Rewatch agora tem seleção relacional; plano medido e benchmark representativo continuam pendentes. CI hospedada, runtime instalado e produção pendentes. Formal 2/7; sem release.

# NextUp — ANALYZE do seletor MariaDB — 2026-10-27 11:45–11:48 BRT
- [x] Teste descartável executou `ANALYZE FORMAT=JSON` sobre o SQL e parâmetros capturados do serviço; no fixture de cinco séries/16 episódios, o plano observou contagens incluindo 4 e 16 linhas.
- [x] Rerun passou 1/1 e registrou 0,429 ms no query block. Após revisão, teste não depende de aliases internos do plano e ignora métricas `r_rows` nulas.
- [ ] Fixture pequeno e aquecido: não extrapolar para biblioteca real. Gerar volume representativo, comparar antes/depois com planos e métricas equivalentes, e avaliar o ramo de rewatch. CI hospedada/runtime/produção pendentes; formal 2/7, sem release.

# HTTP condicional — ETag em listas, weak validators e wildcard — 2026-10-27 10:37 BRT
- [x] Compartilhado helper `ConditionalRequestExtensions.IfNoneMatchMatches` entre imagem e legenda; lista, comparação fraca e `*` cobertos por testes.
- [x] A validação de imagem usa `If-Modified-Since` apenas se `If-None-Match` estiver ausente, conforme precedência.
- [x] Regressão Release: `ConditionalRequestExtensionsTests|ImageControllerSplashscreenTests|ImageControllerUserImageAuthorizationTests|SubtitleControllerTests` passou 44/44, incluindo precedência validada por ação pública do controller.
- [ ] Runtime, CI hospedada e produção pendentes; homologação formal continua 2/7, sem crédito/release.

# NextUp — desempate determinístico do rewatch — 2026-10-27 12:01 BRT
- [x] No empate de `LastPlayedDate`, a seleção escolhe maior temporada/episódio; empate completo usa maior `Guid` estável.
- [x] MariaDB descartável validou a seleção por posição, Guid e data nula contra data válida: 1/1. `NextUpQueryOptimizationTests`: 8/8.
- [ ] O rewatch já seleciona no SQL e passou teste de semântica no MariaDB descartável; ainda falta medir seu plano e comparar desempenho com carga representativa. CI hospedada, runtime instalado e produção pendentes. Formal 2/7 (28,6%); sem release.

# NextUp — SQL para seleção de rewatch — 2026-10-27 12:30 BRT
- [x] Rewatch ranqueado no SQL por data, posição e ID; projeção escalar do ID vencedor evita `EmptyProjectionMember`, com carga de entidades pela chave.
- [x] MariaDB descartável 1/1, fixture 6 séries/19 episódios, incluindo episódio sem posição antes do próximo numerado; NextUp unitário 8/8; formatter e diff-check aprovados.
- [ ] Benchmark representativo, plano/análise do rewatch, CI hospedada, Linux, runtime instalado e produção pendentes. Formal 2/7, sem crédito/release.

# NextUp — ANALYZE do rewatch — 2026-10-27 12:52 BRT
- [x] Teste exige que a consulta capturada após reset contenha `LastPlayedDate` e `ROW_NUMBER() OVER`, então o ANALYZE não reutiliza uma consulta ranqueada anterior.
- [x] MariaDB 11.4.4 descartável passou 1/1; fixture 6 séries/19 episódios, query block 0,568 ms. `dotnet format whitespace` passou.
- [ ] Medir antes/depois com volume representativo, cache controlado e memória; esse fixture pequeno não demonstra ganho. Linux/CI hospedada/runtime/produção pendentes. Formal 2/7, sem release.
# Revalidação do log duplicado de deadlock em imagens — 2026-10-27 13:32 BRT
- [x] O anexo tem SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, idêntico ao incidente anterior. Repete MariaDB 1213 em `DELETE BaseItemImageInfos`; não contém PID, AssemblyVersion ou desfecho das retentativas.
- [x] Filtro Release `ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests`: 54/54 aprovados no checkout atual. O checkout usa stripe local por item, advisory lock MariaDB e retries limitados.
- [ ] O teste confirma a fonte local, não o binário que emitiu o log nem produção. A integração MariaDB descartável anterior 1/1 não foi repetida nesta sessão; runtime instalado, CI hospedada e produção seguem sem correlação. Formal 2/7 (28,6%), sem crédito/release.
