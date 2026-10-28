# Auditoria Tecnica do Backend MulletaFlix

## Revalidação da decisão de retry e estado entre worktrees — 2026-10-21 20:57 -03

- O usuário reafirmou que o Nebula deve manter retentativas automáticas após resultado ambíguo do Telegram ou perda do checkpoint Mongo. A política vigente reconcilia pelo journal local e histórico MTProto antes de reenviar; busca inconclusiva adia a tentativa e preserva o arquivo de stage para reprocessamento automático. Não se promete exactly-once; atraso de visibilidade no Telegram ainda pode gerar duplicata posterior.
- Na worktree gerenciada desta sessão, o caminho Python contém `UploadCheckpointJournal`, intenção SHA-256 anterior ao envio, reconciliação tipada, recuperação após reinício e retentativa automática. Revalidação focal nesta sessão: 35/35 testes passaram (`test_main_upload_part.py`, `test_staging_scanner.py`, `test_upload_checkpoint_journal.py`) em venv temporária Python 3.10 com `system-site-packages`; nenhum serviço Mongo ou Telegram real foi acessado.
- A árvore principal local não contém `upload_checkpoint.py` nem os handlers de reconciliação nesta revisão; está 8 commits atrás de `origin/main` e possui alterações locais não commitadas em arquivos Nebula, incluindo `main.py`. Não copiei nem integrei arquivos para preservar essas mudanças. A implementação testada na worktree gerenciada ainda não está comprovada na árvore principal, publicada, instalada ou em produção; reconciliação dessas árvores é pendência antes de release.
- Homologação formal permanece 2/7 (28,6%); este subconjunto de testes locais não altera gate nem autoriza release. Mapa técnico conferido: nenhum endpoint, classe pública, dependência ou módulo novo; a mudança permanece nos workers e helpers Python já existentes.

## Nebula — reconciliação tipada e retry seguro — 2026-10-21

- `NebulaTelegramPool` agora retorna `Found`, `NotFound` ou `Inconclusive`; ausência só é confirmada quando as consultas MTProto concluem sem erro e sem truncamento no limite de 50 resultados. `NebulaUploadEngine` adia a tentativa e usa a fila Mongo com backoff se a busca for inconclusiva; encontrada recupera a parte, ausência concluída permite o envio.
- Python `find_uploaded_telegram_part`/`reconcile_telegram_part_with_retries` usa os mesmos estados. `upload_part_with_retries` não reenvia naquela tentativa se a consulta falha/incompleta; falha marcada mantém o arquivo staging mesmo com partes anteriores para o scanner repetir automaticamente após o próximo intervalo. A seleção automática de retries do usuário permanece habilitada.
- Risco residual: uma busca concluída pode não expor imediatamente uma mensagem aceita ou pode atingir o limite de resultados; o caso limitado é tratado como inconclusivo. Reenvio posterior ainda pode duplicar quando a mensagem aceita demora a aparecer. Bot API não oferece idempotência; exactly-once não é garantido. O worker Python legado não chamado pelo manager foi localizado na busca de clones e não alterado; os caminhos ativos são `upload_worker_parallel` e `upload_small_file_worker`.
- Testes locais: NebulaPython completo 684/684 em venv Python 3.14 (`NEBULA_STREAM_SERVICE=1`); focal Python de upload 21/21. .NET Release: Nebula 721 aprovados, 30 ignorados opt-in, 0 falhas; teste novo de passagem de estado C# 2/2. O build/test não acessou Mongo operacional nem Telegram; a conexão de Mongo descartável não estava configurada.
- Mudança em contratos internos do módulo existente `NebulaTelegramPool` e estados Python; nenhum endpoint, pacote ou dependência nova. Mapa atualizado no vault. Sem instalação, produção, release ou consolidação formal; homologação global permanece 2/7 (28,6%). Portal requer nova atualização; o conector GitHub havia retornado 403 no último acesso.

## Nebula — identidade de conteúdo por parte e rotação segura — 2026-10-21

- Checkpoint v2 registra SHA-256 dos bytes enviados em cada parte, no journal local e documento Mongo; a legenda determinística inclui o digest para reconciliar mensagem no canal pelo cliente MTProto.
- Na retomada, partes confirmadas e intenções pendentes são verificadas por faixa de bytes. Checkpoint legado/incompleto/divergente aciona nova geração UUID e limpeza de progresso Mongo protegida pela lease atual, sem misturar conteúdo antigo e atual. Uploads novos não fazem pré-leitura integral.
- Retry automático permanece habilitado conforme decisão do usuário. Se a consulta MTProto não encontrar/não puder consultar mensagem já aceita, duplicata residual é possível; exactly-once não é garantido.
- Validação local: Python 3.10, suíte completa 681/681; .NET Release filtro `FullyQualifiedName~Nebula`: 716 aprovados, 30 ignorados opt-in, 0 falhas (746 total). O novo teste `ResetUploadGenerationAsync_RequiresCurrentLeaseAndClearsOldParts` compilou, mas foi ignorado porque não havia conexão Mongo descartável configurada. `dotnet format whitespace --verify-no-changes`, `py_compile` e `git diff --check` passaram. Sem Telegram remoto, Mongo operacional, runtime instalado, CI remota, produção, release ou homologação formal.
- O update Mongo de rotação exige atomicamente owner, lease id e `lease_until > agora`; lease ausente/expirada não pode zerar partes. O teste opt-in cobre lease antiga, expiração e lease vigente quando executado contra Mongo isolado.
- Fluxo alterado em `NebulaUploadEngine`, `NebulaMongoContext`, `NebulaTelegramPool`, journal C# e workers/checkpoint Python. Novo helper `NebulaUploadPartIntegrity`; modelo Mongo ganhou `source_sha256`; nenhum endpoint ou pacote/dependência externa novo.
- Homologação formal permanece 2/7 (28,6%); teste local não fecha gate de integração E2E.
- Publicação do andamento no portal foi tentada via GitHub API e recusada com HTTP 403 `Resource not accessible by integration`; evento remoto não foi atualizado.

## Nebula — regressão de identidade da fila Mongo — 2026-10-21

- `InsertFileDocIfAbsentAsync` preserva o `_id` fornecido pelo worker no `$setOnInsert`; o trabalho deste ciclo adiciona uma regressão opt-in para inserção, deduplicação e transição de lease/progresso, sem alterar o runtime.
- Filtro Release: 113 aprovados, 1 ignorado, 0 falhas; o novo cenário compilou mas não executou por falta de URI Mongo isolada, `mongod`, Docker e Podman. Mongo efetivo continua pendente; homologação formal 2/7 (28,6%), sem release ou produção.
- Portal atualizado em Git `main`, commit `85f03c96ab2a224ae8b8b21bc745b7c3c84926f4`; endpoint público HTTP 200 confirmou o evento. API Vercel respondeu 403; estado interno do deployment não foi verificado.
- Mapa técnico conferido e estruturalmente inalterado.

## Nebula — journal de partes Telegram após resposta confirmada — 2026-10-21

- `NebulaUploadEngine` agora grava identificadores Telegram confirmados em journal local versionado no `DataPath/nebula_upload_checkpoints`, antes da tentativa de checkpoint Mongo. Após reinício, valida UUID, tamanho e timestamp da fonte, recupera partes ausentes do Mongo e mantém o acumulado ao atualizar progresso.
- O journal é removido somente depois do estado `completed` persistir no Mongo. Registro corrompido/incompatível interrompe o envio para evitar reenvio cego. O código preserva retentativas existentes; resposta perdida antes do journal ainda pode duplicar parte, pois o Bot API não oferece idempotência nem leitura de histórico enviado.
- Testes Release: `NebulaUploadCheckpointJournalTests` 4/4; suíte Nebula 710 aprovados/25 ignorados opt-in/0 falhas. Build Release `Jellyfin.Server.csproj` 0 avisos/erros. Não houve Mongo descartável, Telegram real, instalação, produção ou release.
- Regressão da solução completa em Release (`MulletaFlix.sln --no-restore`) terminou com exit code 0 e nenhuma falha reportada. Sumários visíveis: Providers 821 aprovados/0 ignorados; Server.Implementations 1.524 aprovados/65 ignorados. A saída registrou avisos de documentação/analisadores fora deste patch; opt-ins Mongo/MariaDB e transporte Telegram integrado não foram validados.
- Fluxo atualizado: `NebulaFtpManager` fornece `CommonApplicationPaths.DataPath` ao construtor `NebulaUploadEngine`; `NebulaUploadCheckpointJournal` executa recuperação/escrita atômica; o Mongo continua como fonte primária de progresso. Sem nova API ou dependência.

## Nebula — integridade da origem e retry inconclusivo — 2026-10-21

- Auditoria estática atual: `NebulaUploadCheckpointJournal.LoadSnapshotAsync` compara UUID, tamanho e `LastWriteTimeUtc`; `Tools/NebulaPython/upload_checkpoint.py` compara UUID, tamanho e `st_mtime_ns`. Nenhum dos dois compara bytes. Conteúdo alterado com tamanho e timestamp preservados pode permitir a retomada de partes Telegram antigas junto de uma fonte nova.
- `NebulaTelegramPool` captura erro de consulta MTProto e termina sem resultado; a engine mantém retry automático. Assim, falha de pesquisa e ausência confirmada de mensagem não são estados distintos. Isso preserva a decisão do usuário, mas deixa risco residual de duplicata.
- Validação local desta rodada: `NebulaUploadCheckpointJournalTests` 5/5; filtro `FullyQualifiedName~Nebula` 711 aprovados, 29 ignorados e 0 falhas (740 total); Python journal 7/7 e suíte NebulaPython 673/673. Os cenários de Mongo apareceram como opt-in/ignorados; não houve serviço, transferência ou Telegram real.
- Próximo passo: avaliar SHA-256 por parte calculado sobre os mesmos bytes enviados, armazenar evidência junto das partes Mongo/journal e validar partes retomadas. Definir comportamento explícito para registros antigos sem digest e medir I/O somente no caminho de recuperação. Preservar retries automáticos e diferenciar reconciliação inconclusiva de “não encontrada”. Sem alteração de runtime, release, instalação ou produção nesta auditoria.
- Contrato local do portal passou 19/19; tentativa de publicar o evento em `Mulletaflix-Portal:main` recebeu HTTP 403 `Resource not accessible by integration`. Evento remoto não confirmado; gates/cobertura permanecem inalterados.

## Backup — snapshot relacional consistente entre contextos — 2026-10-21

- A criação do backup agora compartilha connection e transação `RepeatableRead` entre contexto central, Movies, Series, Channels e Books, incluindo a leitura do histórico antes da exportação das tabelas. O teste injeta alterações concorrentes nos quatro contextos de domínio e comprova que o arquivo conserva uma única versão temporal.
- Evidência Release: `BackupRestoreMariaDbTests` 6/6 em MariaDB 11.4.4 descartável; regressão `FullSystemBackup` 42/42; build `Jellyfin.Server.csproj` 0 avisos/0 erros. MariaDB operacional 3306 intocado; integração descartável desligada.
- Snapshot validado somente para os cinco contextos relacionais. Filesystem, IntroSkipper/DetectionCache, MongoDB, Supabase, instalação, runtime produtivo e homologação formal continuam pendentes. Nenhuma release foi criada.
- Mapa técnico conferido e atualizado. O fluxo interno de exportação passou a coordenar contexts; não houve novo endpoint nem dependência.

## Backup — rollback único entre contextos relacionais — 2026-10-21

- Falha injetada em `Series` provou que a versão anterior confirmava o contexto central antes dos contextos de domínio, apagando linhas existentes no erro intermediário. O restore agora usa uma conexão e transação compartilhadas para o contexto central e Movies, Series, Channels e Books; contextos não agrupados são instanciados apenas para esse ciclo e restaurados à conexão original antes do descarte.
- Validação Release: MariaDB 11.4.4 descartável, testes `BackupRestoreMariaDbTests` 6/6, com falhas injetadas em ActivityLogs, Series e Books e preservação dos cinco contextos; regressões `FullSystemBackup` 42/42; `Jellyfin.Server.csproj` compilado sem avisos/erros. A integração gerou alertas de analisadores preexistentes. Listener operacional `127.0.0.1:3306` permaneceu intocado; o listener descartável `127.0.0.1:33099` foi encerrado.
- Limites nesta etapa: a consistência temporal entre factories foi validada posteriormente na seção atual acima; permanece sem commit atômico entre banco e filesystem. IntroSkipper/DetectionCache, MongoDB e Supabase continuam fora do backup abrangido aqui. Mudança só local, sem release, instalação ou produção.
- Mapa técnico conferido e atualizado; o fluxo interno do `BackupService` mudou, sem endpoint ou dependência nova.

## Backup integral v0.3 — inclusão local dos contextos de mídia — 2026-10-21

- O patch local no checkout v12.1.35 inclui os DbSets de Movies, Series, Channels e Books na exportação e restauração do backup integral; mantém leitura legada de `0.2.0` sem limpar as tabelas de domínio que esse formato não contém.
- Evidência MariaDB e rollback intercontextos atualizada na seção acima; as anotações de validação desta etapa anterior foram substituídas.
- Estado desta etapa anterior: snapshots entre contexts foram coordenados na seção mais recente acima. O serviço MariaDB local em 3306 não foi acessado. Nenhuma release, instalação ou alteração de produção foi feita.
- Arquivos desta extensão: `Jellyfin.Server.Implementations/FullSystemBackup/BackupService.cs`, `tests/Jellyfin.Server.Implementations.Tests/FullSystemBackup/BackupServiceTests.cs`, `tests/IntroSkipper.Integration.Tests/BackupRestoreMariaDbTests.cs`, `tests/Jellyfin.Server.Tests/MulletaFlixOpenTelemetryExportTests.cs`.

## Restauração integral: manifesto e rollback MariaDB — 2026-10-21

Nota histórica: o rollback descrito nesta entrada cobria somente o contexto
central e foi posteriormente ampliado para todos os contextos relacionais na
seção de validação mais recente acima.

- A auditoria em MariaDB isolado encontrou duas falhas reproduzíveis: o manifesto declarava todas as tabelas como ``DbSet`1``, não os nomes das entradas JSON; e a transação iniciada antes de `IHistoryRepository.CreateIfNotExistsAsync` era incompatível com o comando de histórico que suprime transações.
- Corrigido para gravar os nomes reais das tabelas e preparar a tabela de histórico antes de iniciar a transação que abrange as alterações relacionais. O restore falha ao primeiro erro de persistência e só copia arquivos depois do commit.
- `BackupRestoreMariaDbTests`: 4/4 aprovados contra MariaDB 11.4.4 descartável em `127.0.0.1:13316`, incluindo restore e rollback de falha SQL com arquivos preservados. A instância MulletaFlix em `127.0.0.1:3306` não foi acessada nem alterada. `FullSystemBackup`: 40/40; Nebula suggestions: 14/14; `NebulaUploadEngine`: 224/224.
- Build Release focado de `Jellyfin.Server.Implementations.csproj`: 0 avisos e 0 erros. A compilação do projeto de integração ainda mostra avisos preexistentes de XML/analisadores. Sem commit ou release. Permanecem fora do escopo atomicidade entre banco e filesystem, outros contextos EF e homologação no runtime instalado.
- Arquivos alterados: `BackupService.cs`, `BackupRestoreMariaDbTests.cs`, `backup-coverage-audit.md`; módulos/contratos/APIs/dependências não mudaram.

## Indexação STRM informa raízes parcialmente legíveis — 2026-10-21

- O índice de autocomplete percorria subpastas com `IgnoreInaccessible = true`; subpastas sem acesso eram omitidas sem aumentar `FailedRootCount`, fazendo um catálogo incompleto parecer `Ready`.
- `NebulaStrmDirectoryEnumerator` agora percorre cada diretório separadamente, ignora junctions/reparse points pela configuração de enumeração e continua nos diretórios acessíveis após falha em outro. A raiz é contabilizada como parcial, o estado passa a `ReadyWithWarnings` e o log registra quantos diretórios tiveram falha.
- Teste de regressão simula `UnauthorizedAccessException` numa subpasta, confirma que títulos de pastas acessíveis permanecem disponíveis e que o estado sinaliza índice parcial. Suíte Nebula de sugestões: 14/14 aprovados. Isso não valida ACLs do servidor instalado nem bibliotecas em produção.
- Módulos: `NebulaStrmDirectoryEnumerator`, `NebulaFtpManager`, DTO de status do catálogo; procedimento atualizado em `docs/NEBULA-RUNBOOK.md`.

## Intro Skipper — reanálise de STRM alterado e parâmetro EF — 2026-10-18

- O log de `A Era de Miyu - S01E06.strm` registra `AnalysisScheduler` falhando no passe `ChangedItems`: EF Core não conseguiu avaliar `ReadOnlySpan<AnalysisMode>` no predicado de `ResetItemsForReanalysisAsync`. Os avisos Chromaprint `returned 0 points` são mensagens separadas e não causam essa exceção.
- A consulta materializava os modos em array e usava `Contains` dentro de duas expressões EF. O código foi alterado para usar um `IReadOnlySet<AnalysisMode>` capturado como parâmetro local em ambos os predicados; os IDs dos itens já usam conjunto.
- Regressão MariaDB em `tests/IntroSkipper.Integration.Tests/IntroSkipperMariaDbTests.cs` cobre dois resets seletivos: `Credits` remove somente segmento/registro daquele modo; `Introduction` preserva o segmento manual e o automático correspondente e limpa apenas o marcador analisado desse modo. Outro item permanece inalterado. Suíte completa `IntroSkipper.Integration.Tests` Release recompilada passou 38/38, 0 ignorados, contra MariaDB descartável em loopback; revisão adversarial final sem achados.
- Sem endpoint, módulo, dependência ou componente novo; mudança no fluxo de manutenção/consulta existente do `IntroSkipperDatabase`. Não houve alteração em instalação ou banco produtivo. A versão do plugin/runtime que gerou o log ainda precisa ser comparada com a fonte e validada após uma release.
- Revalidação em 18/10/2026 15:32 (-03): solução Release recompilada e suíte repetida contra MariaDB descartável 11.4.4 em `127.0.0.1:33681`: 38/38 aprovados, 0 ignorados. O novo log recebido às 14:54 contém a mesma exceção; sem versão do plugin no log, não prova se é binário antigo nem se correção foi instalada. Nenhuma produção ou banco operacional foi acessado.

## Gate de compatibilidade do worker STRM no pacote — 2026-10-18 11:14 (-03)

- Evidência: runtime instalado tem `strm_downloader.py` datado de 08/10/2026 e sem `[STRM-WORKER-MONGO-READY]`/`[STRM-SCAN-STARTED]`; fonte atual possui o marcador exigido pelo manager. A divergência é compatível com falha/timeout ao iniciar o Downloader, mas não há no log anexado chamada manual nem stdout para confirmar causa exata.
- `Assert-StageIntegrity.ps1` agora verifica o contrato de prontidão Mongo na fonte antes de aceitar o artefato; hash da fonte e do staged continua sendo comparado. Teste PowerShell cobre pacote válido, cópia obsoleta e ausência do contrato. C# focal `NebulaDownloaderStartupTests` + `NebulaHostedServiceConfigurationTests`: 47/47; teste de integridade de stage passou; `git diff --check` limpo.
- Limite operacional: produção permanece sem alteração; processo/listeners atuais preservados. A API local indisponível impede observação via endpoint; instalação e smoke test controlados ainda necessários para confirmar a execução real. Sem release até o gate global de homologação e primeiro download controlado.
- Mapa conferido: fluxo existente `NebulaFtpManager` → worker Python e pipeline de empacotamento; nenhum componente, API, dependência ou topologia runtime nova.

## Rollback preserva sincronizador Supabase preexistente no Nebula — 2026-10-18

- Em falha do `StartEnvioAsync`, o catch descartava `_supabaseSyncService` mesmo quando a promoção do runtime reutilizava um serviço iniciado anteriormente. A propriedade agora é rastreada por tentativa; só recurso criado pela tentativa atual é limpo.
- Testes C# focados startup/HostedService/cleanup 57/57; suíte completa de Implementations 1.496 aprovados, 63 ignorados por dependências externas, 0 falhas. A suíte não cobre o runtime Python/Telegram real.
- Mapa técnico: componentes e endpoints inalterados; apenas semântica de rollback interno do manager. Produção e release não validadas.
- Homologação publicada no portal Git commit `9e1484aada74ff807ef757719b8f4d7a598be019`; deployment Vercel `READY`, endpoint público HTTP 200.

Escopo: backend `MulletaFlix-master`, com foco em performance, manutencao, bootstrap, persistencia, consultas e cobertura de testes.

## Autorização de endpoints administrativos do GetAvatar — 2026-10-17

- Corrigido controle de acesso em seis endpoints que já documentavam operação “admin only”: upload, delete, remover avatar de usuário, validar, limpar órfãos e consultar status. Antes, `LocalAccessOrRequiresElevation` permitia papel `User` em IP local; agora exigem `Administrator`.
- Regressão HTTP em `TestServer`: todos os seis endpoints retornam 401 sem autenticação e 403 para papel `User` (12/12). Suíte API Release: 697/697. Build Release do plugin: 0 avisos/0 erros.
- Ainda pendente: verificar ação bem-sucedida com admin por endpoint em ambiente controlado, incluir na release final e confirmar instalação. Não houve chamada privilegiada nem mudança de produção.
- Fontes: `MulletaFlix.Plugin.GetAvatar/Controllers/AvatarController.cs`, `tests/Jellyfin.Api.Tests/Controllers/NebulaAuthorizationHttpTests.cs`, `docs/avatar-admin-authorization.md`.

## Privacidade da configuração de branding — 2026-10-17

- `GET /Branding/Configuration` continua público para opções visuais, mas só inclui `IntroPath` para papel `Administrator`; anônimos e usuários comuns recebem `null`, impedindo divulgação do caminho físico do servidor.
- O DTO mantém a propriedade e o endpoint administrativo continua podendo ler o caminho. Testes unitários de controller cobrem retorno público sem caminho e leitura por administrador; o teste de integração público existente foi ignorado pelo runner nesta execução, então não conta como validação HTTP.
- Fonte: `Jellyfin.Api/Controllers/BrandingController.cs`; regressão: `tests/Jellyfin.Api.Tests/Controllers/BrandingControllerPrivacyTests.cs`.

## Persistência de imagens sob timeout MariaDB — 2026-10-17

- O log observado em produção mostrou `SaveImagesAsync` falhando após `Connect Timeout expired`, com `IOException` no stream de leitura. O classificador de falhas transitórias agora reconhece mensagens explícitas de timeout, `IOException` e continua percorrendo a cadeia interna quando um `MySqlException` não for transitório por si só.
- `SaveImagesAsync` já possui até três tentativas com backoff curto; esta mudança faz o erro de conexão do log alcançar esse retry, evitando perder a persistência das referências de imagem por uma falha de rede temporária.
- Testes de regressão cobrem o texto `Connect Timeout expired` e exceções de I/O. Isso não prova download de arte/capa do provider nem validação contra MariaDB de produção; após a release será necessário confirmar as imagens na interface e revisar o log.
- Fontes: `Jellyfin.Server.Implementations/Item/ItemPersistenceService.cs` e `tests/Jellyfin.Server.Implementations.Tests/Item/ItemPersistenceServiceTests.cs`.

## Padrao De Codificacao

- Toda documentacao e todo codigo devem ser mantidos em `utf-8`.
- O repositório ja reforca isso em [`.editorconfig`](D:/Users/Raphael/Documents/Projetos/mulletaflix/MulletaFlix-master/.editorconfig) com `charset = utf-8`.
- Novos arquivos de texto, markdown e codigo devem seguir o mesmo padrao.

## Handoff Entre IAs

Se esta conversa ficar sem tokens e for necessario continuar em outra IA, a proxima instancia deve receber este contexto minimo:

- O projeto principal e o backend do `MulletaFlix-master`.
- A sprint atual e `Sprint 5 - Home, detalhe e refresh sob demanda`.
- O que ja foi concluido inclui `UserManager`, `ItemPersistenceService`, `BaseItemRepository.Querying`, `MetadataService`, `ProviderManager`, `MediaInfoResolver`, `FFProbeVideoInfo` e partes de `ProbeProvider`.
- O que ja foi corrigido nesta rodada inclui o erro de `requestIdleCallback` no `imageLoader`, que estava bloqueando a renderizacao da home e da pagina de detalhe.
- O que ainda falta nesta rodada inclui medir o impacto do refresh sob demanda em background, validar no navegador o carregamento de metadados/imagens, revisar a pagina de dispositivos do dashboard sob carga e continuar os cortes de performance em reconhecimento de midia e download de metadata.
- Toda a documentacao e todo o codigo devem continuar em `utf-8`.
- Os testes de providers já foram executados com sucesso e devem ser mantidos como referencia de regressao.
- A cada tarefa concluida, a documentacao deve ser atualizada com:
  - a tarefa realizada;
  - as proximas tarefas da sprint;
  - o status atual da sprint;
  - qualquer risco ou bloqueio novo.
- Esse update de documentacao deve acontecer antes de trocar de IA, para que outra instancia possa continuar sem perder contexto.
- Se houver troca de IA de ida e volta, a documentacao deve servir como fonte unica do estado atual do trabalho.
- Sempre marcar o que foi concluido com `[x]` e o que falta com `[ ]`, para que outra IA saiba exatamente onde continuar.

## Sprint Atual

- Sprint atual: `Sprint 5 - Home, detalhe e refresh sob demanda`
- Status da sprint atual: `em andamento`
- Sprints concluídas: `Sprint 0 - Baseline e instrumentacao`, `Sprint 1 - Consultas quentes e midia`, `Sprint 2 - Persistencia e escrita`, `Sprint 3 - Startup e bootstrap` e `Sprint 4 - Manutenibilidade estrutural`
- Sprints pendentes: `Sprint 5 - Home, detalhe e refresh sob demanda`

## Status Atual

- [x] Auditoria tecnica inicial concluida.
- [x] Otimizacoes de `UserManager`, `ItemPersistenceService`, `BaseItemRepository.Querying`, `MetadataService`, `ProviderManager`, `MediaInfoResolver` e `FFProbeVideoInfo` aplicadas.
- [x] Suíte de testes de providers validada com sucesso.
- [x] Sprint 1 concluída com sucesso (incluindo fechamento de `ProbeProvider.HasChanged` e cortes de performance).
- [x] Sprint 2 concluída com sucesso (refatorações de batching, remoção de queries N+1, otimização de `DeleteItem` e `SaveImagesAsync` no `ItemPersistenceService`).
- [x] Sprint 3 concluída com sucesso (portabilidade e bootstrap do MariaDB assíncrono, remoção de polling de porta bloqueante e fast-path de schema bypass no `UserManager`).
- [x] Sprint 4 concluída com sucesso (refatoração e decomposição de `UserManager.cs` em `UserAuthenticationService.cs` e `PasswordResetService.cs`).
- [x] Auditoria de segurança completa (skill `find-bugs`) executada com sucesso.
  - Resultado: 1 vulnerabilidade corrigida (injeção de parâmetros URL no `OpenLibraryProvider.cs`).
  - Correção: aplicação de `Uri.EscapeDataString` nos parâmetros ISBN e OLID interpolados nas URLs de consulta à API externa do OpenLibrary.
  - Build verde (0 erros) e testes unitários/providers aprovados (4/4 OpenLibrary, 560/560 Implementations).
  - Testes de integração com falhas pré-existentes (401 Unauthorized no `AuthHelper`) não relacionadas à alteração.

- [x] Corrigido o fluxo de detalhe para revalidar metadados e imagens sob demanda antes de montar o DTO em `GetItem`.
- [x] Criado `OnDemandMetadataRefreshPolicy` para centralizar a decisao de refresh por completude e recencia.
- [x] Ajustado `GetLatestMedia` para tentar completar itens incompletos ou desatualizados antes de montar os cards da home.
- [x] Teste unitario da politica de refresh aprovado com 3 casos de regressao.
- [x] Revisao do fluxo de frontend da home e da pagina de detalhe feita; nao apareceu bug obvio de fetch, entao o ajuste principal ficou no backend.
- [x] Corrigido o `GetLatestTvShowItems` para agrupar por `SeriesId`, nao por `SeriesName`, e adicionado indice composto `Type, SeriesId, DateCreated`.
- [x] Adicionado indice em `UserData` para o `NextUp` (`UserId, Played, LastPlayedDate`).
- [x] Evitado leitura de arquivo para `PrimaryImageAspectRatio` quando `ItemImageInfo.Width/Height` ja estao persistidos.
- [ ] Medir o impacto real de latencia do refresh sob demanda na home e no detalhe.
- [ ] Validar no navegador se as artes, overview e metadados reaparecem nas midias recentemente adicionadas.

## Todo Prioritario Por Ganho

1. P0 - Medir a home real com banco grande:
   - comparar `LatestMedia` de filmes e series com `EXPLAIN ANALYZE`;
   - confirmar se o custo saiu de SQL e ficou em DTO/imagem.
   - ganho esperado: muito alto se ainda existir scan ou sort caro.
2. P1 - Fechar `NextUp`:
   - validar o impacto do novo indice em `UserData`;
   - revisar se `GetNextUpSeriesKeys` ainda materializa mais do que precisa.
   - ganho esperado: alto em bibliotecas com muitas series.
3. P2 - Cortar custo de DTO/imagem:
   - revisar `DtoService` e `UpdateImagesAsync` para evitar trabalho repetido em items recentes;
   - medir se o gargalo restante esta em blurhash, dimensoes ou carga de imagem.
   - ganho esperado: medio.
4. P3 - Refino de home no cliente:
   - manter as imagens fora da viewport em lazy load;
   - evitar refetch desnecessario em navegacao entre paginas.
   - ganho esperado: baixo a medio, mas barato de manter.

## Skills utilizadas nesta auditoria

Estas skills foram selecionadas porque cobrem revisao tecnica, arquitetura, simplificacao e execucao orientada a sprint.

- `codex-fable5`: leitura orientada a evidencias, validacao e fechamento rastreavel.
- `code-review-and-quality`: revisao de corretude, arquitetura, seguranca, legibilidade e performance.
- `code-review-excellence`: estruturar achados por severidade, impacto e recomendacao.
- `dotnet-backend`: orientar ASP.NET Core, EF Core, async, cache e background services.
- `dotnet-architect`: avaliar limites de arquitetura, coesao de modulos e risco sistico.
- `dotnet-backend-patterns`: validar padroes de API, data access, DI, cache e resiliencia.
- `code-simplifier`: reduzir complexidade sem mudar comportamento.
- `backend-development-feature-development`: organizar trabalho em sprints, entregaveis e validacao.
- `caveman`: sintetizar e manter foco em execucao pragmatica.
- `cavecrew`: delegacao curta e objetiva para investigacao pontual.

## Entregas ja validadas

As alteracoes abaixo ja foram aplicadas e validadas com `dotnet test` no backend:

- [x] `Jellyfin.Server.Implementations/Users/UserManager.cs`
  - lock por username para evitar serializacao indevida entre logins diferentes;
  - lock por user id quando o usuario ja existe.
- [x] `Jellyfin.Server.Implementations/Item/ItemPersistenceService.cs`
  - uso de `Dictionary` e `HashSet` para reduzir buscas repetidas;
  - menor custo em atualizacao de itens com muitos relacionamentos.
- [x] `Jellyfin.Server.Implementations/Item/BaseItemRepository.Querying.cs`
  - hot path de `latest TV shows` reduzido para materializar menos dados;
  - ajuste para evitar falha de compatibilidade com EF Core no filtro recente.
- [x] `MediaBrowser.Providers/Manager/MetadataService.cs`
  - menos enumeracoes repetidas em selecao de providers.
- [x] `MediaBrowser.Providers/Manager/ProviderManager.cs`
  - materializacao e particionamento simplificados no caminho quente.
- [x] `MediaBrowser.Providers/MediaInfo/MediaInfoResolver.cs`
  - parse e filtragem dos sidecars em um unico passe.
- [x] `MediaBrowser.Providers/MediaInfo/FFProbeVideoInfo.cs`
  - cache de sidecars reaproveitado e probes de DVD executados em paralelo.
- [x] `MediaBrowser.Providers/MediaInfo/ProbeProvider.cs`
  - comparacao de sidecars reduzida com helper mais barato.
- [x] Testes de regressao adicionados e aprovados para esses caminhos.

## Achados priorizados

### P0

1. `ItemPersistenceService` concentra save, delete, deduplicacao, relacoes e limpeza em um fluxo grande.
2. `UpdateOrInsertItemsCore` ainda precisa manter o padrao de lookup O(1) em todas as estruturas internas.
3. O pipeline de reconhecimento de midia e download de metadata ainda faz trabalho demais por item e precisa de mais batching, cache e menos I/O.
4. `BaseItemRepository` ainda possui trechos com materializacao antecipada em consultas quentes.

### P1

1. `MariaDbProcessManager` faz bootstrap com polling bloqueante.
2. `UserManager` ainda concentra responsabilidades demais para manutencao sustentavel.
3. Os caminhos de refresh e provider ainda podem evitar downloads repetidos e refreshs redundantes.

### P2

1. Classes grandes precisam de extracao gradual de helpers.
2. Rotinas auxiliares de consulta e bootstrap precisam de contratos mais claros.

## Plano tecnico por sprint

### Sprint 0 - Baseline e instrumentacao

Objetivo: medir antes de otimizar.

Tarefas:

- [x] Identificar endpoints e jobs mais caros.
- [x] Medir tempo de query, memoria e startup.
- [x] Registrar baseline para listagem, next up, save item e bootstrap.

Concluido:

- [x] Sprint 0 fechado como baseline documental e de medicao.

Pendente:

- [ ] Repetir as mediciones apos as proximas sprints para comparar ganhos reais.

Skills:

- `codex-fable5`
- `code-review-and-quality`
- `code-review-excellence`
- `dotnet-architect`
- `dotnet-backend`

Prioridade: P0.

Ganho esperado:

- Nao entrega ganho direto de producao.
- Reduz risco de regressao e define linha de base.

### Sprint 1 - Consultas quentes e midia

Objetivo: reduzir latencia e memoria em listagens, reconhecimento de midia e metadata.

Tarefas:

- [x] Revisar `BaseItemRepository.Querying.cs` para adiar materializacao.
- [x] Manter filtros e ordenacao no banco pelo maior tempo possivel.
- [x] Revisar `MediaInfoResolver` para reduzir probes e leituras de caminho.
- [x] Revisar `MetadataService` e `ProviderManager` para diminuir downloads repetidos.
- [x] Adicionar testes para regressao de paginacao, ordenacao e descoberta de midia.
- [x] Reduzir tempo de download de metadata com cache por item e deduplicacao de requests.
- [x] Reduzir o custo de reconhecimento de midia com menos probes redundantes.

Concluido:

- [x] `MediaInfoResolver`, `MetadataService`, `ProviderManager`, `FFProbeVideoInfo` e `ProbeProvider` receberam o primeiro corte de performance.
- [x] Testes focados de providers fecharam em verde.
- [x] `ProbeProvider.HasChanged` recebeu o corte seguro e foi validado com teste dedicado.

Pendente:

- [x] Reduzir ainda mais o scan de sidecars em refresh de video e audio (resolvido e consolidado com o corte de performance principal da Sprint 1).

Proxima tarefa:

- [x] Iniciar a Sprint 2 - Persistencia e escrita (concluída!).

Skills:

- `dotnet-backend`
- `dotnet-backend-patterns`
- `dotnet-architect`
- `code-simplifier`
- `code-review-excellence`
- `backend-development-feature-development`

Prioridade: P0.

Ganho esperado:

- Latencia p95 de listagens: melhora estimada de 15% a 35%.
- Consumo de memoria: reducao estimada de 10% a 25%.
- Reconhecimento de midia: melhora estimada de 10% a 30%.
- Download de metadata: melhora estimada de 15% a 40%.

### Sprint 2 - Persistencia e escrita

Objetivo: reduzir custo de save/delete e melhorar persistencia em lote.

Tarefas:

- [x] Quebrar `UpdateOrInsertItemsCore` em etapas menores.
- [x] Trocar buscas repetidas por dicionarios e estruturas indexadas.
- [x] Reduzir chamadas sincrono-sobre-async.
- [x] Revisar `DeleteItem` para diminuir `Any`, `Contains` e `ToList` repetidos.
- [x] Reduzir regravacao desnecessaria de metadata e imagens.
- [x] Reforcar cobertura de `ItemPersistenceServiceTests`.

Concluido:

- [x] Refatorações e batching do `ItemPersistenceService` aplicados e validados com testes (remoção de queries N+1, otimização de `DeleteItem` e `UpdateOrInsertItemsCore`, e conversão de `.SaveChangesAsync().GetAwaiter().GetResult()` em `SaveChanges()`).
- [x] Implementação de verificação de alterações para Providers, Imagens (em `SaveImagesAsync` e `SaveBaseItemEntities`) e LockedFields, eliminando regravações redundantes no banco de dados.

Pendente:

- [ ] Nenhuma (Sprint 2 totalmente concluída).

Skills:

- `dotnet-backend`
- `dotnet-backend-patterns`
- `code-simplifier`
- `code-review-and-quality`
- `code-review-excellence`

Prioridade: P0.

Ganho esperado:

- Tempo de save em lotes: melhora estimada de 20% a 40%.
- Menos lock contention e menor chance de travamento sob carga concorrente.
- Refresh de metadata: melhora estimada de 10% a 25%.

### Sprint 3 - Startup e bootstrap

Objetivo: melhorar boot e reduzir acoplamento operacional.

Tarefas:

- [x] Remover polling bloqueante de `MariaDbProcessManager`.
- [x] Separar inicializacao de processo e criacao de schema.
- [x] Tornar bootstrap de DB externo e local mais explicito.
- [x] Revisar `UserManager` para nao bloquear caminhos frequentes.
- [x] Cobrir boot com DB pre-existente e schema ausente.

Concluido:

- [x] Conversão da inicialização do MariaDB portátil para fluxos totalmente assíncronos (`StartMariaDbAsync`, `InitializeDatabaseSchemasAsync`, `WaitForPortAsync`) e await real.
- [x] Otimização da concorrência de logins de usuários inexistentes via hashing MD5 de username para a chave de lock, evitando serialização de requests.
- [x] Implementação do fast-path bypass de validação de schemas em consultas síncronas (`GetUsers`, `GetUsersIds`, `GetUserById`, `GetUserByName`) no `UserManager`.
- [x] Validação completa de toda a suíte de testes (560/560 testes aprovados, incluindo os testes de concorrência de autenticação que anteriormente expiravam).

Pendente:

- [ ] Nenhuma (Sprint 3 totalmente concluída).

Skills:

- `dotnet-architect`
- `dotnet-backend`
- `dotnet-backend-patterns`
- `backend-development-feature-development`
- `code-review-and-quality`
- `code-review-excellence`

Prioridade: P1.

Ganho esperado:

- Startup: melhora estimada de 10% a 30%.
- Menor risco de falha intermitente no boot.

### Sprint 4 - Manutenibilidade estrutural

Objetivo: reduzir tamanho de classes e facilitar evolucao.

- [x] Extrair helpers de `UserManager`.
- [x] Separar autenticacao, reset de senha e inicializacao.
- [ ] Revisar classes grandes de midia e provider.
- [x] Padronizar nomes, contratos e retornos.
- [x] Remover logica morta, comentarios obsoletos e duplicacao.

Concluido:

- [x] Extração de `UserAuthenticationService` contendo a lógica de validação de licenças, login local/provedores e restrição de acesso.
- [x] Extração de `PasswordResetService` contendo a lógica de esquecimento de senha e verificação de PIN de redefinição.
- [x] Refatoração de `UserManager.cs` delegando responsabilidades a esses novos serviços internos, reduzindo o arquivo original.
- [x] Compilação e testes validados com sucesso (560/560 testes passando).

Pendente:

- [ ] Revisar classes grandes de midia e provider (planejado para sprints futuras de manutenção de código).

Skills:

- `code-simplifier`
- `dotnet-architect`
- `code-review-excellence`
- `code-review-and-quality`
- `cavecrew`
- `caveman`

Prioridade: P2.

Ganho esperado:

- Ganho direto de performance: baixo, 0% a 10%.
- Ganho forte em manutencao e velocidade de entrega futura.

### Sprint 5 - Home, detalhe e refresh sob demanda

Objetivo: corrigir a exibicao de midias recentes, metadados e imagens sem perder o controle de performance.

Tarefas:

- [x] Auditar o fluxo da home e da pagina de detalhe no backend e no frontend.
- [x] Centralizar a decisao de refresh sob demanda em `OnDemandMetadataRefreshPolicy`.
- [x] Revalidar metadados e imagens no `GetItem` antes de montar o DTO.
- [x] Revalidar itens incompletos ou desatualizados no `GetLatestMedia`.
- [x] Criar teste unitario da politica de refresh.
- [x] Enfileirar o refresh sob demanda em background com `MulletaFlixJobQueue` para remover custo da request.
- [x] Reduzir o custo do `GetLatestTvShowItems` trocando o carregamento final de entidades para `AsSplitQuery` por ids materializados.

Concluido:

- [x] O backend agora tenta completar itens sem overview ou imagem primaria antes de responder o detalhe.
- [x] O fluxo de cards recentes tambem ganhou refresh sob demanda para melhorar a chance de mostrar arte e metadados atualizados.
- [x] A validacao do frontend nao mostrou bug obvio no fetch; o problema principal ficou concentrado no backend.
- [x] O refresh sob demanda saiu da request path e passou a ser processado em background com deduplicacao curta por item.
- [x] Corrigido o bug de `requestIdleCallback` em `imageLoader`, que interrompia `lazyChildren()` e impedia home e detalhe de terminar de renderizar.
- [x] Blindado o `reload()` da pagina de detalhe com `finally` para liberar o loading mesmo se alguma etapa falhar.
- [x] Otimizado o carrossel da home para pedir imagens menos pesadas em listas `overflow`, reduzindo o custo de download e decode das thumbs.
- [x] Corrigido o carregamento da pagina de dispositivos do dashboard para nao bloquear a renderizacao em `useUsersDetails`.
- [x] Otimizado o caminho de series recentemente adicionadas no backend (`GetLatestTvShowItems`) para evitar `SingleQuery` com multiplas colecoes e reduzir a latencia da home.
- [x] Ajustado o frontend da home para `tvshows` pedir `Backdrop` e voltar a priorizar `Thumb`, reduzindo fallback lento de imagem nas series recentemente adicionadas.

Proximas tarefas:

- [ ] Validar no servidor instalado se a secao de series recentemente adicionadas voltou ao mesmo patamar de filmes.
- [ ] Medir a diferenca real de tempo da home com e sem varredura/refresh de metadata em andamento.
- [x] O backend de devices passou a tolerar referencia de usuario ausente sem derrubar a lista inteira.
- [x] Teste de regressao `DeviceManagerTests` aprovado.
- [x] Build de producao do frontend validado com sucesso.
- [x] Corrigido o carregamento da pagina `Atividade` do dashboard para nao bloquear a tabela em `useUsersDetails`.
- [x] Build de producao do frontend validado novamente apos o ajuste de `Atividade`.
- [x] Corrigida a pagina `NFO` do dashboard para nao depender de usuarios como bloqueio de carregamento.
- [x] Corrigida a persistencia de `BaseItemProvider`, `LockedFields`, `Images` e `TrailerTypes` para nao anexar o grafo completo com entidades duplicadas durante refresh de metadados.
- [x] Teste de regressao `ItemPersistenceServiceTests` aprovado.
- [x] A gravacao de `ItemValues` passou a usar `INSERT IGNORE` para eliminar conflitos recorrentes de indice unico durante refresh concorrente.
- [x] Validacao da frente `ItemPersistenceService` aprovada em `Jellyfin.Server.Implementations.Tests`.

Pendente:

- [ ] Medir o impacto de tempo de resposta da home e do detalhe com o refresh novo.
- [ ] Validar a experiencia final no navegador com midias novas, metadados e imagens.
- [ ] Medir o impacto do ajuste da pagina de dispositivos apos remover a dependencia de usuarios.
- [ ] Medir o impacto do ajuste da pagina de `Atividade` apos remover a dependencia de usuarios.
- [ ] Validar se a pagina `NFO` segue funcional mesmo quando a carga de usuarios falha.
- [ ] Confirmar nos logs que o refresh de metadados nao volta a acusar `BaseItemProvider` duplicado nem repetir erro ao persistir `TrailerTypes`.
- [ ] Confirmar em stage que o erro `Duplicate entry '3-Sky Atlantic' for key 'IX_ItemValues_Type_Value'` nao volta a aparecer sob carga.
- [ ] Medir o impacto da troca para `INSERT IGNORE` na taxa de sucesso do refresh concorrente.
- [ ] Se a home continuar vazia, instrumentar `itemsContainer.refreshItems()` para registrar falhas de query e diferenciar erro de API de lista realmente vazia.
- [ ] Fechar a proxima sprint apenas depois de confirmar no navegador que `Minha midia`, `recentemente adicionadas` e a tela de detalhe carregam sem erro de console.

Skills:

- `dotnet-backend`
- `dotnet-architect`
- `dotnet-backend-patterns`
- `code-review-and-quality`
- `code-review-excellence`
- `backend-development-feature-development`
- `caveman`
- `cavecrew`

Prioridade: P0.

Ganho esperado:

- Melhor chance de carregar arte, overview e metadados nas primeiras respostas.
- Reducao de chamados de suporte por item sem imagem ou sem metadata na tela.
- Impacto de performance agora tende a ser menor na request e maior no processamento em segundo plano.
- A fila existente foi reutilizada para evitar duplicar infraestrutura de background.
- A pagina de dispositivos deve aparecer mais cedo no dashboard e continuar funcional mesmo com usuarios antigos removidos do banco.

## Ordem recomendada

1. Sprint 0
2. Sprint 1
3. Sprint 2
4. Sprint 3
5. Sprint 4
6. Sprint 5

## Prioridade consolidada

1. `P0`: consultas quentes, reconhecimento de midia, download de metadata, refresh sob demanda da home/detalhe e persistencia em lote.
2. `P1`: startup, bootstrap e reducao de bloqueios.
3. `P2`: refatoracao estrutural e simplificacao de classes grandes.

## Validacao recomendada

- `dotnet test` focado em `Jellyfin.Server.Implementations.Tests`.
- Casos prioritarios:
  - `ItemPersistenceServiceTests`
  - `NextUpQueryOptimizationTests`
  - `BaseItemRepositoryTests`
  - `BaseItemRepositoryLatestTvShowTests`
  - `UserManagerTests`
  - `UserManagerNormalizedUsernameTests`
  - `UserManagerAuthenticationLockTests`
- Benchmark local para:
  - listagem paginada
  - latest/next up
  - save/delete em lote
  - startup com MariaDB embutido e com DB ja existente
  - reconhecimento de midia
  - download de metadata

## Risco residual

Mesmo com as correcoes aplicadas, parte do custo pode continuar por causa de dados legados, schema historico e compatibilidade com comportamento antigo.

Para evitar falso ganho, cada sprint deve fechar com medicao antes/depois.

### 13/10/2026 — Persistência resiliente das referências de imagens (v12.1.20)

- O log de produção registrou deadlock MariaDB `1213` durante `ItemPersistenceService.SaveImagesAsync`, além de timeouts posteriores do banco. A mídia e os metadados apareciam, mas a gravação de `BaseItemImageInfos` falhava.
- Antes da correção, `SaveImagesAsync` apagava as referências antigas e inseria as novas sem uma transação explícita. Uma falha na inserção podia deixar o item sem referências de imagem.
- A operação agora exclui e insere dentro da mesma transação e repete até três vezes deadlocks/timeout de lock e falhas de conexão transitórias, cada tentativa usando um novo contexto e atraso progressivo. Erros de duplicidade e chave estrangeira não entram no retry.
- A busca por gravações de `BaseItemImageInfos` encontrou uma rotina análoga em `UpdateOrInsertItemsCore`; ela já é executada dentro de uma transação. A rotina de exclusão de itens não reinsere imagens.
- Build Release de `Jellyfin.Server.Implementations` aprovado. `ItemPersistenceServiceTests`: 23 aprovados. Suíte completa: 1.419 aprovados, 60 ignorados e 16 falhas não relacionadas em `NebulaDownloaderEngineTests`, devido a mocks de `NebulaMongoContext` sealed.
- Limite: o log não confirma se o provedor remoto chegou a baixar a imagem; o ID do erro de persistência não coincide com o ID do item mostrado no screenshot. A correção protege a persistência das referências; é necessário revalidar uma mídia afetada depois de atualizar e atualizar seus metadados. Falhas de geração FFmpeg do Trickplay são outro fluxo.

### 16/10/2026 — Monitoramento Nebula restrito às pastas configuradas (v12.1.21)

- O downloader agora deriva suas fontes exclusivamente de `MonitorPaths`; caminhos inexistentes, vazios ou raízes do filesystem são rejeitados e caminhos equivalentes são normalizados/deduplicados. Não injeta mais `D:\midias` nem `D:\midias2` automaticamente.
- A seleção de staging não aceita a raiz de um volume e falha explicitamente se nenhum candidato válido restar, em vez de tentar gravar nela ou silenciosamente usar o primeiro caminho inválido.
- `NebulaFtpManager` valida as fontes antes de inicializar dependências e só informa downloader ativo quando o motor realmente iniciou.
- Validação: suíte final Implementations 1.437 aprovados, 60 ignorados, 0 falhas; `NebulaDownloaderEngineTests` 43/43; portal 31/31; build Release `Jellyfin.Server` 0 avisos/erros. Gate agregado anterior passou 4.384/4.449 (65 ignorados), excluindo `LiveIntegration`; repetição agregada final ficou sem progresso no testhost ASP.NET por mais de cinco minutos e foi interrompida (inconclusiva). Nenhum teste de reprodução/upload em produção foi executado.

### 17/10/2026 — Falha de deleção de imagem de usuário não pode retornar sucesso

- `ImageController.DeleteUserImage` usava `System.IO.File.Delete` diretamente e, ao capturar `IOException`, apenas registrava o erro; ainda limpava a referência persistida e retornava 204. Isso podia deixar arquivo e banco inconsistentes e informar sucesso indevido.
- A operação agora usa o `IFileSystem` injetado. Em `IOException`, retorna 500 e mantém a referência; em sucesso continua limpando-a e retornando 204. O contrato da API declara também a resposta 500.
- Regressão TDD: 2 testes focados passaram (falha mantém referência/retorna 500; sucesso chama exclusão e limpa referência). `Jellyfin.Api.Tests` Release: 679/679. A busca global por `catch (IOException)` com log seguido de `ClearProfileImageAsync`/`NoContent()` encontrou apenas este local e foi corrigida.
- Essa correção não altera módulos, endpoints, dependências ou fluxo de runtime, mas ainda precisa ser incluída em pacote/release. Isso é separado do download Nebula: a instalação ativa segue 12.1.21, não havia worker no snapshot e o primeiro download real permanece sem homologação.

### 17/10/2026 — Corrida entre sync Supabase (backup/restore) e uploads Nebula

- `NebulaSupabaseSyncService` serializa backup e restore no `_backupGate`; ambos agora adquirem também `NebulaMongoContext.AcquireExclusiveSyncAdmissionAsync`, que reserva `nebula_admission`, bloqueia novas admissões C#/Python e drena leases ativos antes de ler ou substituir estado Mongo. A API foi renomeada para refletir o escopo de sincronização exclusiva, não somente backup.
- A regressão Mongo `Restore_ReservesExclusiveAdmissionUntilActiveUploadCompletes` mantém um upload lease ativo e comprova que o restore não envia requisições ao Supabase até o lease ser liberado; após a liberação, o restore termina. Executada contra MongoDB descartável em `127.0.0.1:27099`; testes focados restore + concorrência passaram 20/20, sem ignorados/falhas.
- Limite: o gate só coordena processos que compartilham o mesmo Mongo/database `ftp` e usam versões que implementam o protocolo; isso não valida backup/restore remoto real nem instalação produtiva. A instância 12.1.21 continua sem essa evidência.
- Evidências: `Jellyfin.Server.Implementations/Nebula/NebulaSupabaseSyncService.cs`, `NebulaMongoContext.cs`, `tests/Jellyfin.Server.Implementations.Tests/Nebula/NebulaSupabaseRestoreMongoTests.cs` e `NebulaQueueConcurrencyMongoTests.cs`. Mapa técnico atualizado e release 12.1.24 ainda não publicada.

### 17/10/2026 — Revalidação do gate de backup e cobertura Mongo real

- Reinspeção confirmou que `_backupGate` em `NebulaSupabaseSyncService` serializa chamadas de backup/restauração entre si; não coordena os quatro caminhos de admissão de upload: três `find_one_and_update` nos workers large/parallel/small de `Tools/NebulaPython/main.py` e a criação/claim em `NebulaUploadEngine`/`NebulaMongoContext`.
- `NebulaQueueConcurrencyMongoTests` Release foi primeiro executado sem opt-in Mongo e todos os 16 casos foram ignorados; depois, com MongoDB 8.3.7 exclusivo em `127.0.0.1:27099` e banco de teste isolado, passou 16/16, 0 falhas, 0 ignorados. A instância temporária foi encerrada e a porta deixou de escutar; o processo Mongo alheio em outras portas não foi tocado.
- A integração demonstra que as regressões existentes funcionam contra Mongo real, mas não contém caso de interleaving backup-ocioso → claim de upload; não é validação da barreira ainda ausente. Sem evidência de perda permanente e sem modificação de produção.

### 17/10/2026 — Revalidação do download Nebula que não inicia

- Confirmado novamente: runtime local responde 12.1.21 e executa script antigo (SHA-256 `3D41354345B79EAB19CB7A1FE0C01A02D0579130F18301D156CAF1C3221664C5`); fonte e stage 12.1.22 têm hash `C588DD2006A182440A57AC02D33730531E1FD0575FB3B971B6441D459FDAA88F` e contêm correção de readiness/varredura incremental por título.
- `NebulaDownloaderStartupTests`: 10/10; suíte Python: 581/581; integridade de stage passou. Esses testes não comprovam transferência Telegram real. Nenhum worker, instalação ou configuração de produção foi alterado. `gh auth status` não encontra sessão autenticada; release não publicada.
- Próximo gate: autenticar publicação GitHub, reconstruir/publicar pacote suportado, instalar em janela controlada e observar primeiro item real. Não declarar correção em produção antes dessa evidência.

### 17/10/2026 — Homologação ampla da solução do servidor

- `dotnet test MulletaFlix.sln --configuration Release --no-build --no-restore --filter "FullyQualifiedName!~Integration"` concluiu sem falhas nos projetos determinísticos reportados. `Jellyfin.Server.Implementations.Tests`: 1.452 aprovados, 60 ignorados (1.512); `Jellyfin.Providers.Tests`: 775/775; `Jellyfin.Api.Tests`: 679/679; `Jellyfin.Server.Tests`: 73 aprovados/1 ignorado; Controller 63/63; Naming 681/681; Model 684/684; Networking 124/124; MediaEncoding 71/71; LiveTv 75/75; XbmcMetadata 39/39; Common 31/31; HLS 18/18; Extensions 144/144; Keyframes 3/3. O filtro excluiu os projetos cujo assembly é `*.Integration.Tests`; testes de banco sem serviços opt-in também foram ignorados.
- A primeira execução usou `Category!=LiveIntegration` e incluiu indevidamente `DramaFindsLiveIntegrationTests`, que acessa a internet e falhou porque a API agora retorna resultados à consulta que o teste esperava vazia. Reexecução com o filtro por `FullyQualifiedName` passou na suíte isolada de Providers. A checagem live permanece não-gating e sua expectativa de payload precisa ser atualizada antes de executá-la como homologação externa.
- NebulaPython com Python 3.10: 581/581. Portal: 35/35. Uma chamada inicial de pytest a partir da raiz do monorepo falhou na coleta por `sys.path`; repetida no diretório `Tools/NebulaPython` passou. Isso é erro de invocação, não falha de teste. Status publicado no portal em `main`, commit final `770bf8b455ee3c9de737478045e1628b869f39e3`; Vercel Production `dpl_CPufMKYbJ6SHR7JLSxFLrboFU3aV` `READY`, JSON público HTTP 200 confirma evento `server-wide-validation-2026-10-17-0420` e evidência 35/35.
- Limites: validação local, sem instalação/restart, sem worker Nebula, transferência Telegram, dados de produção, testes de integração completos ou homologação visual. A instalação permanece em 12.1.21 enquanto fonte/stage estão em 12.1.22; não produzir nem anunciar release até completar os gates ainda abertos do roadmap e o smoke test controlado.
- Mapa técnico consultado; não houve mudança de módulos, APIs, classes, dependências ou fluxo produtivo nesta sessão.

### 17/10/2026 — Fila de download Nebula e admissão compartilhada Mongo

- `MediaValidator.refresh_cache` não limitava tentativas com falha: uma falha durante o cursor deixava `last_cache_time` antigo, e a checagem de cada próximo título repetia a consulta completa. Foi adicionado `last_cache_attempt_time` separado para respeitar o intervalo de 120 s sem descartar o último snapshot válido; cursor com `max_time_ms(15000)` limita consulta quando suportado pelo driver.
- Criada a barreira `nebula_admission` no Mongo, com `NebulaMongoAdmissionLease` no C# e `tools/mongo_admission.py` no Python. O backup Supabase reserva atomicamente o gate, drena leases de upload existentes e mantém novos uploads aguardando; upload workers (legacy, parallel e small) e `NebulaUploadEngine` adquirem/renovam/liberam leases. O resultado exige que os dois runtimes implantados compartilhem Mongo/configuração.
- Testes: NebulaPython completo 584/584; testes focados de startup e Supabase .NET Release 52/52; os dois novos testes de concorrência `RequiresMongo` compilam, mas foram ignorados nesta sessão por falta de Mongo de teste. Sem garantia de integração até executar os casos com Mongo real.
- Evidência operacional limitada: `C:\ProgramData\MulletaFlix\nebula\nebula.log` não é atualizado desde 11/10/2026; não permite diagnóstico atual do worker. A instalação responde 12.1.21 e a fonte é 12.1.22. Nenhum worker, configuração, instalação, restart ou mídia alterados; produção e primeiro download não foram validados.
- Mapa técnico atualizado na sessão correspondente; release e implantação permanecem pendentes.

### 17/10/2026 — Promoção Nebula de streaming para modo completo

- `StartEnvioAsync` agora serializa o startup e revalida o modo existente: um worker `Somente Streaming` não é aceito como Envio completo. Para engine nativa, listeners anteriores são liberados antes da recriação; o serviço Supabase é reutilizado durante a transição.
- O host de lease do cache Python também é descartado se o processo não existir. Testes Release focados de startup: 39 aprovados, 0 falhas/ignorados.
- `pytest` do helper de mount indisponível neste Python (módulo não instalado), portanto não contado como aprovação. O log confirma WinError 10048 em 2121 e retries de mount, mas não identifica o processo proprietário. Nenhum processo foi encerrado; sem instalação/restart nem transferência real. Homologação de produção pendente.
- Mapa técnico conferido em 17/10/2026: fluxo interno de startup em `NebulaFtpManager` atualizado; sem nova API, dependência ou classe runtime.

### 18/10/2026 — Reuso baseado em runtime vivo no Nebula

- O log histórico confirma modo somente streaming e falha posterior de bind `127.0.0.1:2121` (WinError 10048), mas não confirma chamada/readiness do Downloader STRM. `StartEnvioAsync` agora decide reuso/promoção pela saúde do host/processo sob `_envioLock`, não pela flag lógica isolada; isso evita tentar criar um segundo runtime quando o handle permanece vivo e corrige a flag após reuso.
- Regressão testa a decisão de reuso usando runtime vivo, modo atual e modo pedido. Testes Release de `NebulaDownloaderStartupTests`, `NebulaHostedServiceConfigurationTests` e `NebulaSupabaseSyncTests`: 95/95, 0 ignorados/falhas; build incluiu warnings preexistentes em outros testes/analyzers. Retry do cursor Supabase tem cobertura de 500→200, 503 após três tentativas e 401 sem retry.
- Mapa: mesmos módulos, classes públicas, APIs e dependências; alterada a condição interna do fluxo de startup `NebulaFtpManager.StartEnvioAsync`. Runtime de serviço completo concorrente não foi reproduzido; instalação atual, resolução de listener 2121 e transferência real permanecem pendentes. Sem acesso/alteração de produção e sem release.

### 17/10/2026 — Cobertura unitária da lease Mongo de admissão

- Adicionado `NebulaMongoAdmissionLeaseTests.cs` cobrindo cancelamento quando o heartbeat perde a lease ou lança exceção, mais descarte idempotente que interrompe o heartbeat e libera a lease uma vez.
- Evidência .NET Release: teste focado 3/3; projeto `Jellyfin.Server.Implementations.Tests` completo 1.455 aprovados, 62 ignorados, 0 falhas. NebulaPython 3.10: 584/584.
- Os casos não exercitam Mongo real nem transferência Telegram. Runtime continua 12.1.21 e candidato/stage completo, instalação e primeiro download seguem pendentes. Não é aprovação para release.
- Mapa técnico consultado em 17/10/2026; a alteração desta rodada é somente teste, sem mudança adicional em módulos, APIs, classes runtime, dependências ou fluxo produtivo.

### 18/10/2026 04:42 (-03) — Promoção Python do streaming para Envio mantinha porta FTP ocupada (T2.1)

- Evidência do log: worker `main.py` iniciou em Somente Streaming; uma inicialização posterior de Envio falhou ao vincular FTP `127.0.0.1:2121` com WinError 10048. No código, a rotina de transição só parava o worker anterior quando `UsePythonEngine=false`; na configuração Python, ele continuava ocupando 2121 enquanto um novo processo era iniciado.
- Correção: parar o worker Python existente também na promoção stream-only → Envio completo, antes de iniciar o modo full; no runtime nativo, continuar liberando os listeners anteriores. Adicionado teste de condição da transição.
- Evidência: testes Nebula de startup/hosted/mount C# `Release` 49/49; ciclo de vida/health/mount Python 3.10 16/16; portal independente 80/80 e espelho 65/65; `git diff --check` aprovado. Uma coleta Python inicial falhou por não incluir o diretório de runtime no `PYTHONPATH`, repetida corretamente e aprovada.
- Limite: produção continua em 12.1.21; PID que possui listeners foi preservado. Não houve restart, instalação, mount ou download real. O código fonte corrige a causa de transição, mas operação e release continuam sem homologação. Sem alteração estrutural de módulos, APIs, classes ou dependências.
- Homologação do portal publicada em `Mulletaflix-Portal`, commit `e2ce7ee663372d2e0635d57f29ac6f307c280c41`; GET público HTTP 200 confirmou o evento `in_progress`.

### 18/10/2026 08:12 (-03) — Admissão de backup aguarda upload, integração cruzada pendente

- Revisão atual da fonte: `NebulaSupabaseSyncService` reserva admissão exclusiva antes do backup; `NebulaMongoContext` bloqueia novos uploads e drena leases com `expires_at > now`. Python renova lease em heartbeat de 30 s e a expiração configurada é de 5 min. Isso torna plausível uma espera recorrente durante upload ativo, mas o log anexado não permite saber se a lease tem proprietário ativo.
- NebulaPython 3.10 completo 592/592. Três testes C# reais de Mongo foram compilados e ignorados porque o endpoint de teste `127.0.0.1:27099` não está disponível. Existe cobertura separada C#/fake, mas não interleaving C#↔Python no mesmo Mongo descartável. Evitar usar Mongo de produção; falta ambiente isolado para fechar T4.2.
- Estrutura revisada em `NebulaSupabaseSyncService`, `NebulaMongoContext`, `NebulaMongoAdmissionLease` e `Tools/NebulaPython/tools/mongo_admission.py`: sem novos módulos, APIs, classes ou dependências nesta auditoria.

### 18/10/2026 11:26 (-03) — Revalidação ampla de testes, gates operacionais continuam abertos

- Execuções Release concluídas nesta rodada: `Jellyfin.Server.Implementations.Tests` 1.498 aprovados, 63 ignorados, 0 falhas; `Jellyfin.Api.Tests` 730/730; `Jellyfin.Providers.Tests` 790/790; NebulaPython 3.10 592/592. Gate de integridade do pacote PowerShell aprovado; portal homologação 102/102.
- Os 63 testes ignorados em Implementations incluem cenários dependentes de serviços externos/ambiente e não contam como validação. Não há Mongo isolado disponível em `127.0.0.1:27099`; nenhum banco de produção foi usado. Não foram iniciados worker, upload/download, instalação ou restart.
- Fato operacional ainda pendente: API local `8096` estava indisponível na última leitura; listeners antigos `2121/2123` foram preservados. O log anexado é histórico e não comprova StartDownloader, readiness, scan ou transferência.
- Resultado: suites determinísticas locais aprovadas; homologação global e release não aprovadas. Permanecem pendentes CI remoto, integrações com Mongo/Supabase em ambiente isolado e smoke test controlado do downloader instalado.
- Mapa técnico consultado nesta rodada; não houve alteração de módulos, APIs, classes, dependências ou fluxo runtime nesta atualização de evidências.

### 18/10/2026 11:29 (-03) — Anexo de startup confirma adiamentos e conflitos, não scan/download STRM

- O log anexado tem 516 linhas. Backup inicial Supabase completou: 255 arquivos, 7 usuários FTP e 28 tokens; usuários do aplicativo não incluídos. Depois aparecem 248 mensagens de adiamento do backup automático por uploads Telegram ativos, embora o processo Python registre “somente streaming” e feeder/fila/workers de upload desativados. Isso sugere leases/atividade de outro processo ou runtime antigo; o log não identifica proprietário nem lease Mongo.
- O mesmo anexo registra duas colisões WinError 10048 em `127.0.0.1:2121` e 14 saídas do helper de montagem porque outro processo já tinha o lock; não confirma N: saudável. Só existe mensagem de scanner de pastas de stage; não há `StartDownloader`, readiness Mongo ou scan STRM de biblioteca.
- Busca na fonte atual não encontra a mensagem literal de adiamento de 30 s. O log é de versão/build anterior ou de componente distinto da fonte atual. Não é seguro inferir qual lease ou processo causou a espera nem encerrar o worker, pois pode servir streams.
- Testes locais desta sessão: Implementations 1.498 aprovados/63 ignorados; API 730/730; Providers 790/790; NebulaPython 592/592. Eles não reproduzem a situação operacional do anexo. Sem mudança de código ou produção, sem download real e sem release; próximo passo é coletar build/versão instalada e telemetria de leases em janela controlada.
- Mapa técnico conferido; arquitetura inalterada nesta análise.

### 21/10/2026 11:16 (-03) — Janela de duplicação entre Telegram e checkpoint Mongo

- Evidência na fonte: `NebulaTelegramPool.UploadDocumentAsync` envia `sendDocument` e repete exceções de transporte até três tentativas (`NebulaTelegramPool.cs`, linhas 1537–1603). Depois da resposta de sucesso, `NebulaUploadEngine` monta a parte e persiste `parts`/`uploaded_bytes` em `UpdateUploadProgressAsync` (`NebulaUploadEngine.cs`, linha 1233; `NebulaMongoContext.cs`, linhas 3277–3322).
- Se o Telegram aceitar a parte e a resposta se perder, ou se a gravação Mongo falhar após a resposta, a execução pode não guardar `tg_message_id`/`tg_file_id`. Retomada confia nas partes persistidas (`NebulaUploadEngine.cs`, linhas 914–936 e 1067); a busca no código Nebula não encontrou reconciliação por nome/caption da parte antes de reenviar. Uma parte aceita sem checkpoint pode, portanto, ser publicada novamente numa retomada. Isso identifica risco no código, não comprova duplicata em produção.
- `NebulaUploadEngineTests` não injeta o cenário Telegram aceito + checkpoint Mongo com falha. Os testes Mongo reais são opt-in e exigem instância isolada; nenhum banco foi acessado nesta auditoria.
- Próximo passo: homologar em ambiente instalado a reconciliação de resultado ambíguo; o fallback de retry automático ainda pode reenviar quando o canal/conta MTProto não permite localizar a parte.
- 2026-10-21: resultado ambíguo em `sendDocument` agora persiste a intenção local antes da chamada, busca a mensagem do canal pelo UUID e marcador de parte via MTProto, grava a parte recuperada no journal e segue a retomada sem novo POST quando localizada. Resposta/aceite ambíguos mantêm retentativas automáticas conforme decisão do usuário; sem acesso à busca ou resultado encontrado, o motor volta à fila de retry, portanto entrega exactly-once não é garantida. Testes Release: 7/7 focados com Mongo 8.3.7 isolado; suíte Nebula 738/738. A busca de histórico foi simulada no teste de reinício, não validada com Telegram real nem produção. Runtime instalado, release e homologação formal permanecem pendentes.
- Nenhuma alteração de código, teste, banco, produção ou release nesta auditoria. O mapa técnico foi conferido; fluxo de classes/APIs permanece inalterado.
- Revalidação em 2026-10-21: `NebulaUploadEngineTests` 224/224 e `NebulaSupabaseSyncTests` 50/50 passaram; suíte ampla sem `Integration`: 1.517 aprovados, 65 ignorados, 0 falhas. Os opt-ins de MongoDB/MariaDB e o cenário Telegram aceitou + checkpoint Mongo falhou continuam não comprovados por esses testes.
- Evidência da homologação publicada no portal `main`, commit `34f64c001e4489899a2686aed2704fda00a94b2d`; Vercel Production `READY`, deployment `dpl_ELvQjaQE9xKcMrARYHshfLCfUNoi`. Endpoint público confirmou evidência e ressalva; auditoria segue `in_progress`. Sem nova release do servidor.
- Revalidação Mongo isolada em 21/10/2026 11:52 (-03): instância efêmera MongoDB 8.3.7 limitada a `127.0.0.1:27099`; `Category=RequiresMongo` passou 25/25 (0 ignorados/falhas). O processo recebeu shutdown administrativo, porta fechada; o serviço MongoDB de produção permaneceu `Running` em sua porta padrão. Nenhum banco ou coleção de produção foi consultado.
- Os testes comprovam os cinco grupos de integração opt-in na build local desta árvore; não cobrem runtime instalado, Supabase remoto, Telegram ao vivo nem topologia/credenciais reais. A pasta temporária de dados ainda existe sob `%TEMP%` e sua remoção automatizada foi bloqueada, sem manter mongod em execução.
- Homologação pública atualizada em `main`, commit `a5b6d7d5d3cb3447f91471b7afc714767a9017a2`; portal 209/209; Vercel Production `READY`, deployment `dpl_3jb65EwTr3tWQu7oZExPrQtdwkhP`. JSON público confirma `25/25`, isolamento `127.0.0.1:27099` e que o teste não cobre a falha Telegram-aceite/Mongo-checkpoint.
- Regressão adicional (21/10/2026 16:17 -03): o primeiro upsert filtrava `_id` de `$setOnInsert`, então Mongo gerava um ID diferente do ID mantido pelo worker; transições de falha/retry não encontravam o documento recém-criado. O upsert agora preserva o `_id` fornecido. Teste da resposta ambígua + reinício passou 1/1 no Mongo descartável `127.0.0.1:27099`; `NebulaQueueConcurrencyMongoTests` + `NebulaUploadCheckpointJournalTests`: 25/25. Retentativas automáticas permanecem conforme decisão do usuário. Sem Telegram real, runtime/produção, homologação formal ou release; exatamente-uma-vez não é garantido. Instância isolada finalizada; serviço operacional não foi acessado.

### 21/10/2026 — Paridade de reconciliação de upload no motor Python

- `Tools/NebulaPython/upload_checkpoint.py` acrescenta journal JSON atômico, sem caminho de origem nem credenciais. `main.py` inicializa-o sob `SESSIONS_DIR/upload-checkpoints` (ou `NEBULA_UPLOAD_CHECKPOINT_DIR`), grava UUID/intenção antes do envio, salva cada parte confirmada antes do checkpoint Mongo, combina journal/Mongo ao retomar e remove o journal apenas após conclusão Mongo.
- `upload_worker_parallel` e `upload_small_file_worker` usam a mesma reconciliação. Em resposta/transporte ambíguo e intenção pendente após reinício, buscam no histórico MTProto por UUID + marcadores exatos `PARTE` antes de reenviar; ausência/indisponibilidade não desativa retries automáticos. Não é exactly-once.
- O marcador de legenda preserva UUID/parte dentro do limite de 1.024 caracteres. O empacotamento de atualização Windows, instalação Windows e pacote Linux, mais integridade do stage, agora exigem/copiam `upload_checkpoint.py`.
- Evidência local Python 3.10: suíte completa 671/671, sem falhas; `py_compile` e `git diff --check` aprovados. PowerShell: cópia do runtime Pester 1/1; `Assert-StageIntegrity.Tests.ps1` aceitou o stage correspondente. Não foram executados .NET tests nesta rodada.
- Cobertura simulada inclui aceite Telegram + resposta perdida sem segundo envio, retorno de parte em journal após reinício, checkpoint Mongo de progresso falho retomado sem reenviar partes confirmadas, preservação e skip de parte confirmada fora de ordem, e fallback que conserva retry quando o histórico não encontra mensagem.
- Sem chamadas Telegram/Mongo externas, instalação, produção, CI remota ou release. A API de busca MTProto e permissões reais ainda exigem homologação; checksum/fingerprint local não equivale a uma assinatura imutável da origem. A homologação formal/global permanece 2/7 (28,6%) e `in_progress`.

### 21/10/2026 18:20 (-03) — Auditoria de paridade do retry Telegram entre motores Nebula

- Revalidação do motor nativo C#: `NebulaQueueConcurrencyMongoTests` + `NebulaUploadCheckpointJournalTests`, Release, 26/26 aprovados, sem falhas/ignorados, contra MongoDB 8.3.7 efêmero restrito a `127.0.0.1:27099`. A instância foi encerrada e a porta fechada; não houve acesso ao Mongo operacional.
- Auditoria estática confirmou que `NebulaFtpManager.StartPythonStreamServiceAsync` ainda é alcançável quando `UsePythonEngine` está ativo. Nessa configuração, `main.py` inicia `upload_worker_parallel` e `upload_small_file_worker`; ambos usam `upload_part_with_retries`, que repete `TimeoutError`/`ConnectionError`. O journal/reconciliação durável validado em C# não está integrado a esse caminho Python. Isso é uma lacuna de paridade identificada na fonte, não evidência de uso atual da configuração nem de duplicata em produção.
- Baseline Python 3.10 isolada: `tests/test_main_upload_worker.py` + `tests/test_main_upload_worker_legacy.py`, 14/14. Essa suíte não injeta aceitação Telegram seguida de resposta perdida/checkpoint Mongo falho e, portanto, não fecha o risco no motor Python. A preferência do usuário por retries automáticos continua; reconciliação no caminho Python antes de novo envio permanece como próximo trabalho.
- Sem alteração de código/release, sem envio real ao Telegram, sem instalação ou produção. Homologação formal/global permanece 2/7 (28,6%); os testes focados não recebem crédito de aceite operacional.
- Mapa técnico conferido: mesmos módulos, classes, APIs e dependências; a auditoria apenas confirmou o caminho alternativo de runtime já existente. [[Sessões/2026-10-21-MulletaFlix-nebula-python-ambiguous-retry-audit]]

### Fencing por geração de lease no upload Nebula — 2026-10-21 17:05 (-03)

- Correção local: o `lease_id` criado por `ClaimFileForUploadAsync` agora atravessa `NebulaStagingWatcher` até `NebulaUploadEngine`; todas as mutações do ciclo (progresso, falha, cancelamento, conclusão normal/duplicada e liberação para retry) filtram worker + lease. `TryAcquireUploadOwnershipAsync` só adota registros sem lease, ou renova a posse do mesmo worker com o mesmo token; um worker reutilizado não é a mesma geração.
- O estado legado sem `lease_id` aceita apenas chamadas sem token, evitando que uma escrita tokenizada obsoleta corresponda a lease ausente. O retry automático Telegram permanece: fencing Mongo não reverte um envio que o destino já aceitou; reconciliação via journal/MTProto continua essencial, com risco residual at-least-once.
- Testes: `NebulaStreamEngineTests` 69/69 Release; `RequiresMongo` focados (fila + recuperação playback) 21 aprovados, 1 ignorado por depender de failpoint; cenário `ReclaimedLease_FencesEveryMutationWhenWorkerIdIsReused` 1/1 em MongoDB 8.3.7 descartável, loopback `127.0.0.1:27099`. O processo de teste foi encerrado e a porta ficou fechada. Não houve acesso a Mongo 27017, Telegram, instalação ou produção.
- A build Release da implementação compilou; os avisos XML dos parâmetros novos foram removidos após essa execução e requerem revalidação incremental. O workspace já continha mudanças não relacionadas; todas foram preservadas. Sem release do servidor. Homologação global/formal permanece aberta e não recebeu crédito parcial por testes focados.
### 21/10/2026 — Identidade Mongo ao registrar item novo da fila Nebula

- Revisão independente apontou como risco que o upsert inicial pudesse gerar um `_id` diferente do usado pelo worker. Conferência da fonte atual mostrou que `InsertFileDocIfAbsentAsync` inclui o `_id` de origem em `$setOnInsert`; a implementação estava corrigida, mas faltava uma regressão direta para o caminho novo.
- Teste opt-in adicionado para preservar o `_id` após inserir, recusar duplicidade pelo mesmo `local_path`, obter lease pelo ID e persistir progresso com essa geração.
- Verificação focal Release: 113 aprovados/1 ignorado/0 falhas. O novo cenário Mongo não executou por ausência de Mongo descartável e runtimes locais de container. O resultado prova compilação e suites não-Mongo selecionadas, não a integração real desta regressão. Homologação e release permanecem sem alteração.

### Nebula — resumo de erros operacionais — 2026-10-21

- Novo helper interno `NebulaErrorMessageFormatter` é usado pelos retornos/status/logs de `NebulaSupabaseSyncService` e `NebulaFtpManager`; normaliza whitespace, limita a mensagem visível a 512 caracteres e prefere a exceção-base se o texto externo do driver for longo. Os loggers existentes continuam recebendo a exceção completa.
- Cobertos backup/restore banco e usuários, teste/provisionamento Supabase, dependências, streaming Python, startup/shutdown, Downloader, montagem N:, geração STRM e limpeza de concluídos.
- Filtro Release `FullyQualifiedName~Nebula`: 719 aprovados, 30 ignorados opt-in, 0 falhas/749 total. Três regressões validam causa-base curta, HTTP 503 e truncamento/multilinha. Não houve acesso a serviços operacionais.
- Módulo Nebula existente ganhou uma classe interna; sem endpoint, API pública, dependência externa ou alteração de DI. Formal 2/7 (28,6%); produção e runtime instalado não foram testados. Atualização GitHub do portal bloqueada por HTTP 403.

### Regressão do journal: falha de persistência atômica — 2026-10-21

- Teste Python parametrizado força falha em `fsync` ou `os.replace` depois de existir checkpoint válido e confirma que os dados prévios seguem recuperáveis e que não sobra arquivo temporário.
- `test_upload_checkpoint_journal.py`: 7/7; suíte completa NebulaPython 3.10 com requisitos travados: 673/673. Nenhuma mudança na implementação nesta etapa. Sem acesso a Mongo/Telegram reais, runtime instalado ou produção; homologação formal global permanece 2/7 (28,6%); nenhuma release criada.

### 21/10/2026 — claim fencing Python, runtime Linux e atomicidade de restore

- Risco de geração antiga: `_prepare_upload_resume` limpava progresso por `_id/status` somente. Cada worker Python agora persiste token `upload_claim_id` exclusivo durante claim; checkpoint, progresso, erro e conclusão usam filtro `_id + worker_id + upload_claim_id + status=uploading`. `matched_count=0` aborta sem limpar o staging; conclusões removem o token.
- Pacote Linux agora copia e exige `upload_checkpoint.py`, dependência direta de import em `main.py`; teste compara pacote contra fonte. Stage Windows rejeita cópia stale pelo hash SHA-256.
- `BackupService` rejeita restore de manifesto 0.3.0 se não consegue abrir contextos relacionais ligados à mesma transação, antes de materializar banco/arquivos. Manifestos 0.2.0 seguem aceitos.
- Testes desta auditoria: pytest workers 15/15, suíte NebulaPython 685/685, package Linux passou, validação de stage PowerShell passou, restore atômico focal passou; suíte `Jellyfin.Server.Implementations.Tests` completa: 1.537 aprovados, 70 ignorados opt-in, zero falhas. Sem teste Telegram real, Mongo/MariaDB de produção, runtime instalado, CI remota ou instalação. Homologação formal 2/7 (28,6%); sem release. Andamento publicado no portal em `267e03689ae76e11c19e035a69b24c8961acc2fc`; Vercel `READY`, endpoint público HTTP 200.
- Estrutura externa não mudou: mesmos módulos, endpoints, dependências e classes públicas. Fluxo interno do worker Python e barreira de restore foram atualizados; mapa técnico e runbook refletem o contrato.
