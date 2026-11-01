## Regressão local completa e reconciliação do espelho de homologação — 2026-11-01 11:51 BRT

- `dotnet test MulletaFlix.sln --configuration Release --no-restore --filter "FullyQualifiedName!~Integration" --verbosity minimal`: exit 0 em 15 assemblies, 5.465 aprovados, 95 ignorados, 0 falhas/5.560 executados. Os casos `Integration` foram filtrados; os ignorados permanecem pendentes, incluindo pré-condições de banco e plataforma.
- Nebula Python 3.10: `pytest tests -q --tb=short`, 724/724. Focal `ImageSaverTests|RootedFileSystemTests`: 4 aprovados, 1 ignorado por plataforma em Windows/.NET 10 Release. O caso sibling `metadata-backup` segue protegido pelo `RootedFileSystem.TryDeleteFile` atual; nenhuma correção duplicada foi necessária.
- A auditoria encontrou o espelho `portal-site/homologacao-status.json` com gates formais nulos, embora o portal público e seu `latestAudit` confirmem 2/7 (28,6%). O espelho foi reconciliado para 2/7 e recebeu a regressão atual; o contrato local passou 99/99.
- O teste valida exclusão confinada, não escrita handle-relative. A lacuna de escrita por pathname permanece aberta; Linux, MariaDB/Mongo opt-in, runtime instalado e produção não foram validados. CI hospedada do PR #1 continua bloqueada por billing antes dos steps. Nenhum gate formal novo ou release.
- Lote no espelho embutido local aguarda publicação conforme batching de dez. Mapa técnico conferido; módulos, APIs, classes e dependências não mudaram.

## Mídia Storage Online: preservar erro original durante limpeza temporária — 2026-11-01 11:37 BRT

`MidiaStorageOnlineAtomicFile.WriteAtomically` já mantinha o erro de escrita/promoção quando a exclusão do temporário também falhava, guardando a exceção secundária em `Exception.Data["TemporaryFileCleanupException"]`. A revisão encontrou que a regressão da promoção aceitava um tipo também usado pela falha de limpeza; o teste agora exige o tipo da falha principal por plataforma e `UnauthorizedAccessException` na limpeza. Código de produção não mudou nesta rodada.

`MidiaStorageOnlineAtomicFileTests`: 5/5; `Jellyfin.Providers.Tests`: 914 aprovados, 14 ignorados, 0 falhas/928 em Windows/.NET 10 Release. Os 14 ignorados são integrações/precondições opcionais. O comportamento de falha de limpeza sem erro principal foi conferido estaticamente pelo `finally`, mas não recebeu teste isolado determinístico; não foi introduzido seam artificial. Linux, filesystem remoto, runtime instalado, produção e CI hospedada não foram verificados. CI do PR #1 segue bloqueada antes dos steps por cobrança GitHub. Formal 2/7, sem crédito; 12.1.46 candidata, sem release.

## Gravação de legendas: validação do token Language — 2026-11-01 08:49 BRT

`SubtitleManager.TrySaveSubtitle` agora valida no ponto compartilhado de gravação o token `SubtitleResponse.Language`, aceitando letras/números ASCII e os separadores `-`/`_`. A checagem abrange upload da API e respostas remotas de providers; rejeita `:` (ADS NTFS), separadores de path, controles e caracteres fora do formato. A exceção também fecha o stream recebido antes de sair. `SubtitleManagerPathSafetyTests` passou 13/13 e a suíte `Jellyfin.Providers.Tests` 900 aprovados, 13 ignorados, 0 falhas/913 em Windows/.NET 10 Release. Cavecrew review estática sem achados. `UploadSubtitleDto` continua com validação `[Required]`; o middleware mapeia `ArgumentException` a HTTP 400. Não foram verificados plugins instalados, endpoint HTTP real, Linux, runtime ou produção. CI do PR bloqueada por faturamento; sem release ou crédito formal, 2/7 gates.
Contrato do portal: 433/433; commit `7ac8992245420e245b4de471390ee8d290750d9f` foi publicado em `main`, Vercel Production `READY`, JSON público HTTP 200 com o novo evento. A página mantém a homologação formal em 2/7.

## Nebula: falha no startup do Control Plane reporta sucesso — 2026-11-01 08:10 BRT

Quando `CONTROL_ENABLED` estava ativo e o construtor ou `ControlPlane.start()` falhava, `main()` sinalizava shutdown, fechava FTP/HTTP, Mongo, tasks e bots, mas retornava `0`. Isso podia informar sucesso ao supervisor embora o Nebula encerrasse. `main()` agora mantém o retorno não zero após a limpeza existente. A regressão exercita falha no construtor e no start.

`test_main_lifecycle.py`: 2/2 casos focais; suíte Nebula Python em Windows/Python 3.10: 722/722. A suíte anterior à correção tinha 720 casos; estes 722 refletem os dois cenários recém-adicionados. CI hospedada permanece bloqueada antes dos steps por cobrança GitHub; Linux, runtime instalado e produção não foram verificados. Formal 2/7; sem release.

### PR #1 — rechecagem das falhas fornecidas — 2026-11-01 07:52 BRT

A execução indicada (`41005367220`, 16 jobs) e a mais recente observada para o PR (`41283703952`, head `cbc380c94e8c88278c44309f586faf9c0e1c134f`) não iniciaram steps. As anotações dos 16 checks atribuem o failure ao bloqueio de cobrança da conta GitHub. Os logs solicitados por job não existem (`BlobNotFound`/404), sem saída de testes. `gh` local continua sem autenticação; o diagnóstico foi obtido pela conexão GitHub e a API pública do workflow. Não há causa de código comprovada: sem alteração, commit/push ou crédito de homologação; formal permanece 2/7. Contrato portal atualizado e validado: 90/90. Primeira execução encontrou uma asserção textual antiga no evento; evidência foi ajustada mantendo os fatos e a repetição passou. Próxima ação: regularizar cobrança, autenticar gh e rerodar CI no HEAD atual.

## Regressão local ampla .NET — 2026-11-01 08:06 BRT

`dotnet test MulletaFlix.sln --configuration Release --no-restore --filter "FullyQualifiedName!~Integration" --verbosity minimal` terminou com exit code 0 em Windows/.NET 10 Release. Resultados visíveis: `Jellyfin.Providers.Tests` 882/882; `Jellyfin.Api.Tests` 805 aprovados, 3 ignorados; `Jellyfin.Server.Implementations.Tests` 1.683 aprovados, 90 ignorados. A saída completa não foi retida, portanto não registro total agregado da solução. Os ignorados incluem cenários MongoDB/MariaDB opt-in, Unix/Linux/macOS e outros pré-requisitos locais; eles não contam como aprovados.

A reexecução cobre o estado local atual sem validar CI hospedada, os cenários de integração excluídos pelo filtro, bancos operacionais, Linux, runtime instalado ou produção. Foram emitidos avisos de analisadores; não foi feita limpeza de avisos fora do escopo. Homologação formal permanece 2/7, sem crédito ou release.
## Prioridade de idioma de imagens — 2026-11-01

- Ajustado o idioma efetivo de consulta de imagens para preferir a configuração global do servidor sobre item/biblioteca; a preferência local continua fallback quando o servidor não define idioma. Normalização regional cobre códigos simples e tags com subtags, como `pt` + `BR` → `pt-BR` e `zh-Hant` + `TW` → `zh-Hant`.
- `ILanguageAwareRemoteImageProvider` permite que o `ProviderManager` passe o locale resolvido sem quebrar providers legados. Movie, Series, Season, Episode, BoxSet e Person do TMDb implementam essa interface. A ordenação mantém idioma do servidor primeiro, inglês como fallback, sem idioma e demais idiomas.
- `Jellyfin.Model.Tests`: 699/699 aprovados. `Jellyfin.Providers.Tests`: 887 aprovados, 13 integrações externas ignoradas, 0 falhas (900 total), Windows/.NET 10 Release. O primeiro teste amplo revelou acesso a estado global não inicializado; a resolução passou a fornecer idioma e país no mesmo caminho, eliminando a dependência de `LibraryManager` estático no teste.
- Não houve chamadas ao TMDb/Fanart, refresh de biblioteca real, substituição automática de imagens persistidas, Linux, instalação ou produção. Providers externos que não implementam locale-aware não passam a solicitar idioma; seus resultados só podem ser ordenados quando contêm a tag de idioma. Imagens já salvas não mudam automaticamente.

## Reexecução local do job de integridade de empacotamento Windows — 2026-11-01 07:00 BRT

- O workflow `.github/workflows/ci.yml` executa nove scripts PowerShell de integridade e repete `ApplyUpdateFailClosed.Tests.ps1` em Windows PowerShell 5.1. Os nove scripts passaram em PowerShell 7; a repetição em PowerShell 5.1 também passou.
- Cobertura observada: integridade do stage e ZIP, bootstrap Rclone, ACL dos dados do serviço, rollback de cópia e startup, updater fail-closed, recuperação de backup de stage e opções do stage. As fixtures foram temporárias; o teste ACL confirmou que o sentinel externo permaneceu intacto.
- Isso reproduz localmente o job `Windows packaging integrity scripts`, mas não substitui o runner hospedado. Os 16 checks atuais do PR #1 seguem falhando antes do início por bloqueio de cobrança; nenhum passo hospedado foi executado.
- Contrato do portal: 87/87. Nenhum código/artefato de produção foi alterado; não cria release nem crédito formal. Homologação formal continua 2/7. Evento 2/10 do próximo lote permanece local, sem publicação remota.

## Build, testes unitários e auditoria de dependências da solução — 2026-11-01 06:52 BRT

- Reproduzidas localmente as etapas do job Server build & unit tests em Windows/.NET 10 Release. dotnet build MulletaFlix.sln --configuration Release --no-restore terminou com 0 erros e 592 avisos de analisadores.
- dotnet test MulletaFlix.sln --configuration Release --no-build --no-restore --filter FullyQualifiedName!~Integration --verbosity minimal terminou com exit code 0. No assembly Server.Implementations foram 1.683 aprovados, 90 ignorados, 0 falhas (1.773 casos); a saída global completa não foi retida, então não se declara contagem agregada da solução. Os cenários MariaDB/MongoDB opt-in e os testes explicitamente de integração não foram executados por esse filtro.
- dotnet list MulletaFlix.sln package --vulnerable --include-transitive terminou com exit code 0 e não reportou pacotes vulneráveis nos projetos consultados.
- O diagnóstico GitHub conectado confirmou no HEAD cbc380c94e8c88278c44309f586faf9c0e1c134f que os 16 checks do run 41283703952 não iniciaram devido ao bloqueio de cobrança da conta. Os logs de job retornaram BlobNotFound; isso não comprova falha de código. gh pr view/gh pr checks continuam indisponíveis sem autenticação local.
- Nenhuma alteração de runtime, commit ou push nesta sessão. Homologação formal segue 2/7; Linux, bancos opt-in, CI hospedado, runtime instalado e produção continuam pendentes.
- A decisão de escrita segura permanece: não reutilizar o helper específico do restore em raízes de provider/perfil. BackupRestoreDirectory está no assembly Implementations; o projeto Providers declara dependências diretas de Controller e Model. A primitiva compartilhada handle-relative ainda exige desenho e testes multiplataforma.
# Auditoria local e bloqueio de CI — 2026-11-01

## Revalidação focal de persistência, provedores, Nebula e ACL — 2026-11-01 06:37 BRT

- Nesta sessão, sem alteração de runtime: `ItemPersistenceServiceTests` passou 34/34 em Release/.NET 10; regressões de path OMDb/AudioDB passaram 15/15; Nebula Python passou 720/720 pelo `.venv` versionado do projeto.
- `BackupRestoreDirectoryTests` passou 47 testes e ignorou 11 cenários exclusivos de Linux no Windows. Isso valida apenas o comportamento Windows executado; o caminho Linux/ACL segue sem runtime nesta rodada.
- MariaDB integrado não foi executado: `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING` está ausente. O processo `mysqld` ativo foi preservado e não acessado. Nenhum banco de produção foi consultado.
- O portal dedicado recebeu o lote de dez eventos em `main`, commit `21d6bd1f81e4ba3374a347b6bfca506a26b4bbe2`; contrato 433/433, Vercel Production `READY`, JSON público HTTP 200 com `generatedAt=2026-11-01T06:37:00-03:00`. Gates formais continuam 2/7, sem crédito novo.
- Mapa técnico conferido, sem alteração estrutural. A revalidação não cria crédito de homologação formal, não valida CI hospedada, instalação, produção ou release. A correção segura da exclusão de imagem por pathname permanece pendente.

## Persistência de coleções relacionadas em lotes — 2026-11-01 05:20 BRT

- `SaveBaseItemEntities` continua pré-carregando providers, imagens, campos bloqueados e tipos de trailer em lote. Agora acumula os IDs alterados durante a passagem dos itens e executa as exclusões em chunks de até `DeleteBatchSize` (1.000), antes dos `AddRange` de reposição e na mesma transação.
- O teste MariaDB focal confirmou que atualizar providers de 8 itens reduz os comandos `DELETE` de 8 para 1. `SaveItems_UpdatingProvidersAcrossDeleteBatchBoundary_RewritesEveryProvider` semeia e substitui 1.001 providers antigos e passou 1/1; a fronteira gera dois chunks e nenhum registro antigo permanece.
- `ItemPersistenceServiceTests`: 34/34 Release/.NET 10. `ServerPersistenceMariaDbTests`: 12/12 e `IntroSkipper.Integration.Tests`: 59 aprovados, 1 ignorado, 0 falhas, ambos executados antes da expansão final do fixture para 1.001 itens; o teste de fronteira final passou separadamente. Revisão independente confirmou a semeadura e substituição dos registros.
- MariaDB 11.4.4 descartável foi encerrado; a porta temporária 33129 foi confirmada fechada e o arquivo local de conexão neutralizado. Banco operacional, biblioteca real, Linux e CI hospedada não foram validados; Actions segue bloqueada por billing. Sem medição em catálogo de produção, crédito formal ou release publicada.
- Fluxo interno de persistência mudou, sem novos módulos, APIs ou dependências. Mapa técnico conferido; atualização descrita na ficha e no registro de sessão.

## Persistência de lote composto apenas por placeholders — 2026-11-01 04:41 BRT

- `ItemPersistenceService.UpdateOrInsertItemsCore` agora retorna antes de abrir contexto/transação quando todos os itens são descartados por usarem `PlaceholderId`.
- O teste `SaveItems_PlaceholderOnlyBatchDoesNotCreateDatabaseContext` falhou antes da correção ao observar a criação do contexto e passou após a mudança. Suíte focal `ItemPersistenceServiceTests`: 34/34 em Release/.NET 10.
- Nenhum item válido é ignorado por esse retorno: ele só ocorre quando a lista após o filtro está vazia. Sem alteração de API, esquema ou dependências. Não foi executado benchmark nem teste de banco nesta fatia.
- Mapa técnico conferido e sem mudança estrutural. É evidência local em andamento, sem crédito de homologação formal e sem release publicada; CI hospedada continua pendente por bloqueio de cobrança.
- Contrato do espelho no repositório servidor: 77/77. Portal dedicado `Mulletaflix-Portal` recebeu lote de 12 eventos; contrato 432/432 e Vercel Production `READY`; JSON e página pública HTTP 200. Homologação formal 2/7, sem promoção.

Na branch do PR #1 (`codex/fail-closed-inplace-updater`), a regressão local passou: solução .NET Release, 5.387 aprovados, 93 ignorados, 0 falhas/5.480; Nebula Python 714/714; contrato do portal 73/73. Separadamente, `IntroSkipper.Integration.Tests` passou 58/59, 0 falhas, em MariaDB 11.4.4 descartável; 1 cenário combinado foi ignorado por exigir MongoDB. O workflow GitHub Actions do PR (run 41280792962, SHA `bc29e779fc3f864ae3f67aa1621730d4039c048d`) não iniciou os 16 jobs: as anotações dizem que a conta está bloqueada por cobrança; não há etapas nem logs de execução. Isso não demonstra falha de código e também não substitui CI hospedada. Requer desbloqueio da conta e nova execução. MongoDB, Linux, runtime instalado, produção e homologação formal continuam sem validação; sem nova release ou crédito de homologação. O datadir MariaDB descartável não pôde ser removido pela política da ferramenta e permanece em `%TEMP%`.

## Homologação da consulta Home de séries recentes — 2026-11-01 03:21 BRT

- A classe BaseItemRepositoryLatestTvShowTests foi executada inicialmente sem conexão opt-in: 6 aprovados, 2 ignorados, 0 falhas. Isso confirmou que os cenários MariaDB ainda não tinham sido executados.
- Foi inicializada uma instância descartável MariaDB 11.4.4 em 127.0.0.1:33127, com datadir exclusivo temporário. A mesma classe passou 8/8, 0 ignorados, 0 falhas; inclui o teste com filtros reais da Home (Episode, itens não virtuais e raízes de biblioteca) e exclusão de episódios de outra biblioteca.
- O processo de teste foi encerrado, 127.0.0.1:33127 foi confirmado fechado e a porta operacional 3306 não foi acessada. A remoção recursiva do diretório temporário foi bloqueada pela política da ferramenta; dados e logs sem credenciais continuam em %TEMP%.
- Isto valida resultados funcionais no MariaDB, não plano/latência com catálogo representativo. EXPLAIN ANALYZE, volume real, CI hospedada e produção permanecem pendentes; nenhuma mudança de runtime, índice, release ou gate formal.
## Reauditoria do monitor Downloader após startup — 2026-11-01 01:42 BRT

- Revisão adversarial identificou que o monitor podia ignorar `_stopPending` após uma tentativa de shutdown incompleta. `CheckDownloaderRuntimeAsync` agora falha fechado nesse estado.
- Testes exercitam parada incompleta e duas rodadas temporizadas do monitor: todas as tentativas de uma rodada falham e a próxima rodada recupera o Downloader.
- `NebulaHostedServiceConfigurationTests`: 22/22; regressão combinada `NebulaHostedServiceConfigurationTests`, `NebulaDownloaderStartupTests` e `NebulaCleanupLifecycleTests`: 67/67 em Release/.NET 10. Runtime Python, instalação e produção não foram exercitados.

## Idempotência de upsert do catálogo Mongo — 2026-10-30 02:47 BRT

- Adicionado teste de integração que chama duas vezes, em sequência, o upsert do mesmo snapshot via `$set` e confirma um único registro, atualização de campos remotos e preservação de campo local. A primeira chamada conclui com sucesso; não representa resposta perdida.
- Teste focal passou 1/1; suíte `Category=RequiresMongo` passou 30/30 em Release/.NET 10 contra MongoDB 8.3 descartável em `127.0.0.1:27099`. Processo terminou e porta ficou fechada.
- Isto valida idempotência de chamadas sequenciais do upsert, não perda real de resposta de transporte nem replay de `PerformRestoreAsync`. Transporte/write concern ambíguo, sobrescrita snapshot-authoritative de campos presentes e semântica de `forceFullRestore` seguem pendentes. Produção 12.1.45; formal 2/7; sem release.

## Restore completo Supabase falha fechado — 2026-10-30 02:55 BRT

- `PerformRestoreAsync(forceFullRestore: true)` agora retorna falha antes de iniciar chamadas HTTP, adquirir admissão Mongo ou alterar dados. A resposta diz que nenhuma alteração foi feita.
- Reauditoria encontrou replay idempotente anterior à chamada do serviço. `RestoreSupabaseToMongoAsync` agora rejeita `forceFull=true` antes de consultar replay em memória/Mongo; teste semeia resposta incremental de sucesso com a mesma chave e confirma que ela não mascara a recusa.
- A auditoria corrigiu uma conclusão anterior: o endpoint `POST Supabase/Restore` aceita `forceFull` e encaminha o valor ao serviço, embora o restore automático e a tela web usem o padrão `false`. Antes, o serviço ignorava a flag e podia apresentar um restore incremental comum como sucesso.
- O restore completo permanece indisponível por segurança: o legado Python define remoção de registros ausentes no snapshot, mas o serviço C# restaura várias categorias, incluindo usuários do aplicativo, sem contrato que delimite quais dados podem ser removidos. Nenhuma exclusão foi implementada.
- Testes `MongoFullRestoreFailsClosedWithoutCallingSupabase` e `FullRestore_RejectsBeforeReturningIncrementalIdempotencyReplay` passaram. Suítes combinadas `NebulaSupabase*` + `NebulaBackupStatusTests`: 74 aprovados, 8 ignorados por exigirem Mongo, 0 falhas/82; Release/.NET 10. A aprovação não cobre Mongo real, Supabase real nem produção.
- Sem mudança estrutural. Produção conhecida 12.1.45; 12.1.46 segue candidata; homologação formal 2/7; sem release.

## Resultado parcial do bulk restore Mongo — 2026-10-30 02:21 BRT

- Corrigida a perda da contagem quando `BulkWriteAsync` não ordenado aplica alguns upserts e depois lança `MongoBulkWriteException`. O contexto Mongo informa ao restore somente operações reconhecidas (`MatchedCount + Upserts.Count`) quando o resultado é acknowledged e não há `WriteConcernError`; o erro original continua sendo propagado.
- Regressão real com MongoDB 8.3.7 descartável validou 1 documento confirmado antes da falha do item inválido. Categoria `RequiresMongo`: 29/29 Debug e 29/29 Release.
- Contrato local do portal passou 421/421; sétima evidência do lote local, ainda aguardando o total de dez para publicação.
- Sem mudança de APIs públicas, dependências, módulos ou fluxo macro. Não cobre exceções de transporte cujo resultado de escrita seja desconhecido. `forceFullRestore` continua inerte, sem exclusão automática. Produção 12.1.45; candidata 12.1.46 sem publicação; homologação formal 2/7.

## Restore Supabase rejeita IDs MongoDB equivalentes — 2026-10-30 01:39 BRT

- `restore_collection` valida agora duplicidade de `_id` antes de tocar no MongoDB. A verificação respeita igualdade numérica BSON (por exemplo, inteiro e double equivalentes), preserva a distinção de booleanos e compara outros IDs por BSON codificado.
- Foram cobertos restore integral e seletivo, IDs ObjectId/documento repetidos e números iguais em representações BSON diferentes. Testes focais Supabase: 63/63; suíte Nebula Python: 714/714; `git diff --check` passou.
- Não foi conectado MongoDB/Supabase real nem executado restore em produção. Falha de `bulk_write` depois de aplicar parte das operações ainda pode deixar mutação parcial; item segue pendente da auditoria. Candidata 12.1.46, sem release; produção 12.1.45; homologação formal 2/7.

## Download automático de imagens por idioma — 2026-10-30 01:15 BRT

- `ItemImageProvider` agora coleta candidatos dos provedores remotos habilitados em paralelo, classifica pelo idioma efetivo do item/biblioteca/servidor e baixa pelo provedor que forneceu cada candidato. Se o candidato preferido falha, tenta o próximo conforme a ordem resultante; mantém idioma/proveniência originais da imagem.
- Poster e backdrop passam pelo mesmo fluxo em filmes e `Series`, incluindo animações modeladas como séries. A seleção manual já usava ordenação compartilhada entre provedores.
- Revisão adversarial detectou e corrigiu isolamento insuficiente: erro de `GetSupportedImages` em um provider agora é registrado sem impedir os outros. Regressão dedicada comprova continuidade com o provider saudável.
- `Jellyfin.Providers.Tests`: 840 aprovados, 13 ignorados (integrações externas opt-in), 0 falhas/853; classe de refresh 65/65. `EnumerableExtensionsTests` passou 14/14 para prioridade regional, equivalência e fallback. Nenhuma chamada a serviço externo nem mídia existente foi alterada.
- Complemento 2026-10-30 01:31 BRT: `RefreshImages_RemoteImages_PrefersLibraryLanguageAcrossProviders` passou 8/8, cobrindo `Movie` e `Series`; animações são tratadas como `Series` pelo servidor. A suíte completa `Jellyfin.Server.Implementations.Tests` passou 1.668, ignorou 85 e teve 0 falhas (1.753 total, Windows/.NET 10). MariaDB e casos específicos de Linux não foram executados nesta suíte por pré-condição/plataforma.
- Revalidação 2026-10-30 02:11 BRT: a regressão foi ajustada para deixar de setar idioma diretamente em `BaseItem` e exercitar `LibraryOptions` (`pt` + `BR`). `RefreshImages_RemoteImages_PrefersLibraryLanguageAcrossProviders`: 8/8 em Release, cobrindo `Movie` e `Series` e fallback inglês quando o candidato localizado falha. Animações não têm entidade própria: filmes animados seguem `Movie` e séries animadas `Series`; não há ramo exclusivo por categoria.
- Ainda pendem TMDb/Fanart reais, reprocessamento de capas já persistidas, Windows instalado, Linux, hardware, CI hospedada e produção. Candidata 12.1.46 sem release; homologação formal permanece 2/7.

### Revalidação dos provedores de imagens — 2026-10-30 02:43 BRT

- A suíte completa `Jellyfin.Providers.Tests` passou 840/853 em Release/.NET 10: 840 aprovados, 13 testes de integração externa ignorados e nenhuma falha.
- A regressão focal de idioma para pôster/fundo cobre `Movie` e `Series` (8/8 nesta tarefa). Filmes animados são `Movie`; séries animadas são `Series`, sem categoria de entidade separada.
- Esta reexecução não acessou TMDb/Fanart nem mídia real. Não reprocessa imagens já salvas; atualização dessas capas ainda exige refresh/substituição explícita. Formal 2/7; produção 12.1.45; sem release.

## Concorrência entre revarredura e atualização de imagens — 2026-10-30 00:11 BRT

- Adicionado teste MariaDB determinístico para `SaveItems` e `SaveImagesAsync` sobre o mesmo item. A fábrica de contexto bloqueia a revarredura depois da aquisição do lock e comprova que a atualização de imagens não abre contexto antes da liberação.
- Build Release e teste focal passaram 1/1 contra MariaDB 11.4.4 descartável em loopback e porta dinâmica. A instância foi encerrada; MariaDB operacional/produção não foi usado.
- É incremento de cobertura, sem mudança do código de produção. A execução não valida concorrência entre processos nem substitui a regressão completa; formal permanece 2/7 e a release 12.1.46 não foi publicada.

## Cobertura da prateleira musical — 2026-10-29 22:36 BRT

- Acrescentado teste para a lista musical: inclui apenas o álbum ancestral de uma faixa que corresponde ao filtro e respeita o limite. A classe focada passou 6/6 em Release/.NET 10.
- Auditoria do ponto P1 confirmou materialização do conjunto distinto de IDs de álbuns antes do limite. Isso pode aumentar memória e parâmetros com bibliotecas grandes, mas não foi medido; mudança para subconsulta permanece pendente de validar plano/SQL em MariaDB isolado.
- Nenhuma alteração de lógica de produção nesta etapa; estrutura do projeto permanece igual. Produção 12.1.45, sem acesso; homologação formal 2/7.

## Consulta SQL para álbuns recentes — 2026-10-29 22:42 BRT

- A seleção da prateleira musical mantém os IDs dos álbuns correspondentes como subconsulta EF/SQL. Antes, todos os IDs eram materializados antes de aplicar o limite da lista, ampliando o uso de memória conforme a biblioteca crescia.
- `BaseItemRepositoryLatestTvShowTests`: 7/7 aprovados em Release, incluindo regressão InMemory e integração com MariaDB 11.4.4 descartável. A porta foi dinâmica em loopback (52761), com esquema e datadir temporários isolados; o listener encerrou após os testes. MariaDB operacional em 3306 permaneceu ativo e não foi acessado.
- A execução confirma o resultado funcional em MariaDB, mas não mede latência/uso de memória em catálogo real nem comprova ganho de desempenho. Produção 12.1.45 não foi alterada; homologação formal permanece 2/7; sem release publicada.
- Portal atualizado no lote de dez evidências: contrato 413/413; Git `main` commit `045f4c5`; Vercel Production `READY` nesse commit; a página e o JSON públicos retornaram HTTP 200.
- Mapa técnico do vault conferido: mesma classe e fluxo de consulta; sem módulo, endpoint, dependência ou alteração estrutural.

## Lote vazio de persistência de mídia — 2026-10-29 22:49 BRT

- `SaveItems` agora retorna após validar o argumento e o cancelamento quando o lote está vazio. Isso evita adquirir lock e abrir contexto/transação MariaDB em chamadas sem registros, inclusive de rotinas de biblioteca e migração.
- `ItemPersistenceServiceTests`: 30/30 aprovados em Release; teste novo confirma que lote vazio não cria contexto EF. Revisão independente sem achados; `git diff --check` passou.
- Revisão independente do diff: sem achados. Contrato do portal: 414/414; esta execução é o primeiro evento do próximo lote local de dez.
- MariaDB operacional, produção, Linux e CI hospedada não foram usados. Sem alteração de contrato; homologação formal 2/7, sem release.
## Limite zero na lista de filmes recentes — 2026-10-29 22:29 BRT

- Corrigido `GetLatestItemList` para filmes: limite `0` ou negativo retornava um título, pois a deduplicação adicionava o primeiro resultado antes de comparar com o limite. O retorno antecipado aplica-se antes da consulta e deixa inalterados os limites positivos.
- Teste de regressão na classe `BaseItemRepositoryLatestTvShowTests`: 5/5 aprovados, incluindo limite zero, negativo, limites positivos/deduplicação e séries.
- Nenhuma estrutura de módulo/API/dependência mudou. Produção 12.1.45 não acessada; candidata 12.1.46 não publicada; homologação formal 2/7.
# Auditoria Técnica do Backend MulletaFlix

## Retorno após erro de reprodução — revisão em 2026-10-30

- A supressão de navegação após `playbackerror` foi retirada após revisão: o fluxo subsequente encerra e destrói o player, então manter a rota podia deixar uma tela vazia. O retorno anterior a erro é preservado.
- A mudança de navegação relacionada ao relato webOS é apenas no evento `fullscreenchange`: sair do fullscreen forçado não deve chamar `appRouter.back()` enquanto o playback pode continuar. A parada real mantém a navegação anterior.
- Revisão atual: Vitest web 425/425 e `npm run build:check` passaram. O contador de quadros foi acrescentado ao E2E de stage, ainda não executado nesta rodada. Produção 12.1.45 intacta; candidata 12.1.46 sem release; formal 2/7. WebOS físico, codec e áudio continuam pendentes.

## Bootstrap redundante em GetFirstUser — 2026-10-29 22:13 BRT

- `StartupController` já aguarda `UserManager.InitializeAsync()` antes de consultar `GetFirstUser()`. Essa leitura síncrona não força mais uma inspeção do schema quando a inicialização foi concluída; a exceção de tabela ausente ainda força a recuperação do schema.
- Regressão MariaDB descartável 11.4.4: `UserManagerNormalizedUsernameTests` passou 26/27; o único skip é a validação Linux de `lower_case_table_names=0`. O teste novo confirmou que `GetFirstUser()` não cria outro contexto assíncrono após `InitializeAsync()`.
- O datadir foi copiado para instância isolada em `127.0.0.1:33127`; depois do teste, a porta temporária fechou e a instância de produção em 3306 permaneceu ativa e intocada.
- Isso remove uma inspeção repetida no caminho de inicialização, mas não elimina o bootstrap síncrono de outros métodos públicos de `UserManager`. Linux e produção não foram validados; homologação formal permanece 2/7, sem crédito ou release.

## Verificação incremental — player e retomada Nebula — 2026-10-29

- Hipótese webOS: a navegação de volta no evento de saída do fullscreen forçado pode deixar o elemento de mídia tocando atrás da Home. A fonte candidata remove essa navegação; ainda falta reproduzir no aparelho que apresentou o sintoma.
- Retomada do upload multipart agora reconhece partes válidas esparsas, mantém seus checkpoints e salta essas partes ao repetir o lote. Evita retransmitir dados já confirmados após falha de uma parte anterior.
- Evidência local: NebulaPython 688/688; Vitest 425/425; `npm run build:check`; build web de produção e `npm run verify:build` (1.929 artefatos). Sem acesso de escrita à produção, teste candidato em dispositivo webOS ou validação de Telegram/MongoDB operacionais.
- Revalidação 29/10 21:28 BRT: solução .NET Release passou em 17 assemblies, 5.378 aprovados, 261 ignorados, 0 falhas/5.639; `LiveIntegrationTests` foi excluído. `BackupServiceTests` focal passou 98/103, com 5 ignorados por Linux/macOS/mount. Três testes que inicialmente falharam por C: ter 4,0 GiB livres passaram novamente em diretório D: privado com ACL restrita; a regressão ampla também passou nessa configuração. `npm run build:check` e descoberta dos 3 cenários Playwright passaram. E2E e TV webOS não foram executados, e homologação formal permanece 2/7.
- Homologação formal permanece 2/7 (28,6%); nenhuma release foi criada. Ver registro da sessão de 29/10.

### Player webOS permanece na reprodução ao sair do fullscreen — 2026-10-29 20:37 BRT

- O caminho de código mais compatível com o relato é a navegação `appRouter.back()` no evento `fullscreenchange` quando `forcedFullscreen` estava ativo e a API dizia que fullscreen havia terminado. Em TVs webOS, isso pode sair da tela do player sem encerrar o elemento de vídeo, compatível com áudio ao fundo e retorno à Home; a causa ainda depende de reprodução no aparelho.
- Removida essa navegação automática; o código mantém a rota do player e atualiza o ícone. STRM real de *Moriarty the Patriot* continuou com imagem no Chrome; isso não valida saída do fullscreen no webOS, pois a TV não estava disponível.
- Verificações: Vitest 425/425; `npm run build:check` aprovado; `git diff --check` aprovado. `lint:changed` apontou três falhas preexistentes em `tests/playwright/specs/22-adsense-flow.spec.ts`; lint direto do controller acusa 100 erros históricos do arquivo, sem alteração nova de regra. Não houve build/instalação em produção.
- Produção permanece 12.1.45; formal 2/7 (28,6%); sem release. Mapa técnico conferido em 2026-10-29: sem mudança estrutural. Ver nota de sessão desta correção.

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

### Recuperação de restore após encerramento do processo — reconciliação 2026-10-30

- **Correção da lacuna anterior:** `BackupRestoreMariaDbTests.RestoreBackupAsync_ReplaysAfterProcessTerminationBetweenDatabaseAndFileCommit` já existe. O teste encerra o processo filho antes do commit relacional e após esse commit, mas antes do commit dos arquivos; em seguida, executa a recuperação e verifica estado do banco, arquivo restaurado e limpeza do journal/sidecars.
- Evidência histórica: a auditoria de 2026-10-26 registra 16/16 testes do filtro MariaDB em uma instância descartável MariaDB 11.4.13, incluindo esse cenário. A revalidação do código compilou o teste em Windows/.NET 10 Release em 2026-10-29, mas foi ignorada por falta de `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING`; isso não equivale a nova execução integrada.
- Nesta sessão não houve conexão MariaDB: a variável de teste está ausente e `127.0.0.1:3306` tem `mysqld.exe` ativo, tratado como produção. O teste exige explicitamente instância descartável em loopback e porta diferente de 3306. CI hospedada ainda precisa de execução observável para esta revisão.
- **Pendente distinto:** exportação dos bancos primário, domínio e participante usa transações separadas. A necessidade de consistência point-in-time entre stores ainda não tem contrato definido; é lacuna de requisito, não bug confirmado.
- MariaDB é o único banco relacional operacional.

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
- Métodos públicos síncronos de `UserManager` ainda podem bloquear ao inicializar o schema. `GetFirstUser()` evita a rechecagem forçada depois de `InitializeAsync()`, mantendo a recuperação forçada quando a consulta encontra tabela ausente.

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

### Consolidação E2E em configurações próprias — 2026-10-29 15:11 BRT

- Produção consultada somente por `GET /System/Info/Public`: HTTP 200, versão informada 12.1.45. Nenhum endpoint de escrita foi chamado.
- Stage isolado da fonte candidata 12.1.46: a execução ampla reportou 18/39 pass, 17 fail, 4 skipped. O runner aplicou `playwright.stage.config.ts` também aos specs 24–29, embora esses fixtures usem módulos `/@fs` do Vite e possuam configurações dedicadas. Esses resultados misturam falha de configuração do harness com falhas de cenário; não são evidência suficiente para classificar os 17 como defeitos do produto.
- Suítes web nos ambientes próprios: Playwright requests/autocomplete/Nebula 8/8 após repetir um falso negativo de inicialização fria; backup/accessibility 3/3; scrollbuttons 2/2 após repetir uma falha fria de import. As duas falhas iniciais passaram ao repetir com o Vite aquecido, caracterizando instabilidade de cold start ainda a corrigir no harness.
- `dotnet test tests/Jellyfin.Server.Implementations.Tests/Jellyfin.Server.Implementations.Tests.csproj -c Release --filter FullyQualifiedName~ItemPersistenceServiceTests --no-restore`: 29/29 aprovados. Os testes de integração MariaDB não foram apontados ao serviço em execução.
- Stage e MariaDB descartável foram encerrados; a porta de produção 8096 continuou ativa. O listener do MongoDB local não foi usado pelos testes. Artefatos/stage preexistentes foram preservados.
- Pendências: separar no runner os specs que exigem Vite dos specs de stage C#; estabilizar imports a frio; classificar e corrigir os cenários stage restantes (admin, autenticação, AdSense, logout e paginação); executar integrações MariaDB opt-in apenas em banco descartável. Homologação formal segue 2/7; 12.1.46 não publicada.
- Mapa técnico conferido nesta sessão e sem mudança estrutural; não houve alteração em módulos, APIs, classes ou dependências de produção. Portal não atualizado; acumular evidências em lote de 10 conforme orientação do usuário.

### Integração Playwright stage/Vite — 2026-10-29 15:54 BRT

- `run-suite.mjs` agora separa os seis specs que dependem dos módulos React/Vite e roda o restante contra stage C#; cada suíte usa porta loopback e JSON próprio, com status e contagens consolidados. Falha precoce respeita também o caminho `--report-dir` personalizado.
- Vite fixture: 11/11 aprovados. O stage executado usou `stage/MulletaFlix.dll`, que informa versão 12.1.44; portanto 15/28 aprovados, 6 falhas e 7 skips/não executados NÃO são evidência da fonte/candidata 12.1.46. Falhas observadas: formulário de acesso à biblioteca, rota Live TV, AdSense, logout/busca, clique sobreposto no teste acessibilidade e paginação. Homologação da candidata permanece pendente.
- Contratos Node do runner/relatório: 21/21; `npm run build:check` passou. Sem alteração no runtime do servidor ou em produção.
- Produção indicada como 12.1.45 pelo usuário; no fim da rodada 8096 recusou conexão apesar de ter sido observada ativa antes. Causa não estabelecida, servidor não reiniciado. Stage, Vite e MariaDB de teste encerrados sem listeners; diretórios temporários de stage/relatório permaneceram porque a limpeza recursiva foi bloqueada pela política.
- Portal não atualizado: aguarda o próximo lote de 10 evidências. Nenhuma release, commit ou push.
- Mapa técnico conferido; registrado split do harness, sem mudança em módulos/APIs/dependências runtime.
- Registro: [[Sessões/2026-10-29-MulletaFlix-playwright-stage-vite-integration-1554]].

### Reteste focal de acessibilidade e origem do binário — 2026-10-29 17:31 BRT

- Corrigido o teste Playwright 32: o link de detalhe fica sob o overlay de reprodução; cenário agora o ativa por teclado (`focus` + Enter). Reteste stage passou 1/1 em 8,1 s, mas o binário isolado também informa 12.1.44 e não valida a candidata 12.1.46.
- Vite fixtures executados sem filtro: 11/11 aprovados. Contratos Node do runner/relatório 21/21; `npm run build:check` aprovado.
- A tentativa focal filtrada deixou a suíte Vite vazia e o Playwright CLI retornou “No tests found”; stage passou, mas o sumário agregado ficou `failed` por essa suíte sem correspondências. Não foi alterado o runner por causa deste uso de filtro.
- Produção `localhost:8096` recusou conexão nesta verificação, não foi iniciada nem reiniciada. O log do DLL 12.1.44 registrou tentativa de iniciar o cliente DuckDNS apesar da variável de isolamento; ao fim não havia processo DuckDns, MulletaFlix, dotnet ou mysqld ativo nem listener nas portas de teste. Não foi confirmado efeito externo nem alteração DNS; o candidato atual contém a correção da variável, mas não foi o binário usado.
- Sem alteração de código de produto, release, commit, push ou portal. Portal continua aguardando lote de dez evidências. E2E da candidata atual, CI hospedada, Linux e produção continuam pendentes. Mapa conferido: estrutura/runtime permanecem iguais.

### Vinheta antes da mídia com STRM Nebula — 2026-10-29 18:28 BRT

A fonte de `v12.1.45` foi comparada com a candidata: o evento de parada da vinheta não carregava a indicação de término natural e o avanço para o título da fila dependia de `EnableNextEpisodeAutoPlay`; isso explica a falha quando a preferência está desligada. A candidata sinaliza término natural e permite a transição da vinheta sem mudar a configuração de autoplay de episódios. Playwright focal em stage isolado, usando um `.strm` real da biblioteca Nebula e a configuração padrão (vinheta habilitada, caminho customizado vazio), passou 1/1: vinheta solicitada antes do vídeo, transição natural e resposta HTTP 200 do stream principal. O relatório local é `mflx-playwright-report-real-intro-verified-20261029`. Produção consultada apenas por GET continua em 12.1.45; fix não está instalado. Homologação formal global 2/7; release candidata não publicada. Mapa técnico conferido: componentes web existentes; nenhuma API, dependência ou módulo novo.


### Nebula: retry do scanner após erro transitório — 2026-10-29 18:46 BRT

O queued_mongo_scanner agora recupera também claims gravados no Mongo cuja resposta expirou, com backoff de cinco segundos quando a liberação falha. A restauração após reinício re-enfileira arquivos em staging e limpa marcadores antigos; workers removem marcadores ao assumir. Testes de scanner/restauração 11/11, suíte Nebula Python 686/686 e compileall passaram. Um `.strm` real da biblioteca foi enfileirado em memória, sem worker de upload ou chamada ao Telegram. MongoDB real, Telegram, runtime instalado e produção não foram exercitados. Homologação formal permanece 2/7; nenhum release adicional foi publicado.

### Revisão Fable-Judge — recuperação após reinício — 2026-10-29

Claims staging de scanners anteriores agora são descobertos após reinício e liberados pelo `scanner_claim_id` persistido; falhas repetidas aguardam cinco segundos antes da próxima tentativa. Regressões de recuperação anterior ao reinício e backoff incluídas. Suíte Nebula Python 690/690. O teste permanece simulado, sem MongoDB operacional ou Telegram real. A mudança não altera a homologação formal (2/7) nem o estado da release (12.1.46 candidata, não publicada).

### Reprodução real de intro nativa em produção — 2026-10-29 19:25 BRT

- Teste somente de reprodução no servidor local informado como produção v12.1.45, com episódio STRM real `O Crime do Conde` e Nebula original ativo. O servidor retornou uma intro nativa; o console do web registrou `intro playback failed, falling back to main item` duas vezes. A reprodução principal então mostrou quadro H.264 1280x720 no Chrome; saída pelo botão Voltar encerrou o player e voltou à Home.
- O log local confirmou `Resolved 1 intro(s)` e `Providing native intro`; também registrou falha de extração de legenda com desafio de autenticação, sem evidência de que essa falha cause a rejeição da intro. A causa HTTP exata da falha da intro não foi capturada.
- Resultado: intro padrão continua sem homologação em produção. A execução não reproduziu áudio sem vídeo nem retorno automático à Home; webOS não testado. O teste candidato em stage documentado acima continua separado e não valida a instalação 12.1.45. Nenhuma configuração/arquivo de biblioteca foi alterado; playback foi parado ao final.
- Mapa técnico conferido em 2026-10-29: sem mudança estrutural. Homologação formal 2/7; sem release. Portal aguarda o próximo lote de dez evidências, conforme preferência do usuário.

### Teste de quadro de vídeo com STRM real — 2026-10-29 19:50 BRT

- O teste Playwright focal da vinheta agora exige que o elemento de vídeo esteja visível, tocando, com `HAVE_CURRENT_DATA` e dimensões maiores que zero após a resposta do stream principal. Fonte STRM real usada: episódio de *Moriarty the Patriot* na biblioteca Nebula; link e parâmetros não são registrados.
- `npm run build:check` passou. A compilação temporária do cliente e servidor concluiu. A primeira partida do stage isolado foi interrompida por falta do runtime MariaDB; após copiar somente o runtime MariaDB já disponível em stage, a segunda iniciou o banco descartável e falhou fechada na validação ACL do diretório de restauração. Playwright não chegou à reprodução; nenhuma política ACL foi relaxada.
- Produção respondeu HTTP 200 à consulta de saúde e informa 12.1.45. Nenhum dado de produção foi alterado. A reprodução Chrome anterior com STRM real mostrou vídeo 1280x720, portanto o sintoma áudio-sem-vídeo/retorno à Home continua sem reprodução confiável.
- Sem alteração de runtime ou release. Homologação formal segue 2/7. Mapa técnico conferido, sem mudança estrutural. Próximo passo: executar stage com identidade aprovada pela política ACL e obter título/aparelho exatos que apresentaram a falha.

### Integridade dos snapshots Supabase — 2026-10-29 20:05 BRT

- `backup_collection` compara a contagem Mongo com os documentos lidos e não grava snapshot quando o limite truncaria a coleção. Novos snapshots usam versão 2 e guardam a lista de documentos sob `data.documents`.
- Restore integral valida formato, versão e contagem antes de qualquer escrita; backup filtrado não pode apagar o restante da coleção. Restore por `backup_id` aceita o formato legado apenas como upsert seletivo, sem exclusão de documentos extras. Escritas em lote devem terminar antes da limpeza de excedentes.
- Testes Supabase focais: 50/50; suíte Nebula Python: 701/701; `git diff --check` passou. Inclui snapshot truncado, versão inválida, consulta filtrada, snapshot vazio, erro parcial de escrita e compatibilidade do backup legado seletivo.
- MongoDB/Supabase reais, backup/restauração de produção e limite real de payload não foram exercitados. Nenhuma chamada a banco operacional. Homologação formal permanece 2/7; produção 12.1.45; sem release. Mapa técnico conferido: módulos/APIs/dependências/fluxo de dados sem alteração estrutural.

### Reprodução da intro nativa com STRM real e diagnóstico de resposta parcial — 2026-10-29 20:12 BRT

- Em produção local 12.1.45, Chrome reproduziu `Moriarty the Patriot — S1:E1: O Crime do Conde` a partir de STRM real, com Nebula original ativo. O servidor resolveu o asset nativo e retornou uma intro; o cliente registrou falha de reprodução da intro e fez fallback para o episódio principal. A mídia principal exibiu vídeo 1280×720 por 24 s e permaneceu no player; o sintoma de vídeo invisível/retorno à Home não foi reproduzido neste Chrome. A faixa de áudio não foi medida instrumentalmente.
- O log do servidor mostra `ExceptionMiddleware` recebendo exceção após início da resposta, mas descartava o objeto da exceção. A correção candidata agora registra a exceção original no warning, preservando o rethrow e sem alterar resposta/status após headers. `ExceptionMiddlewareTests`: 3/3; o teste novo verifica que a exceção permanece no log. `ffprobe` confirmou o asset instalado (MP4/H.264 High, 1280×720, AAC-LC estéreo, 10 s).
- Os erros de prebuffer do principal e extração de legenda foram observados separadamente; o stream principal conseguiu iniciar por nova requisição. Ainda não foi identificado o status/cause exato da resposta da intro nem reproduzido o relato de webOS. A mudança de logging está apenas na fonte candidata; não instalada na produção. Formal 2/7; sem release.
- Mapa técnico conferido; sem mudança estrutural de módulos, APIs ou dependências. Reteste de webOS exige modelo/ano, versão do navegador e título/episódio exatos, ainda não informados.
### Revalidação com STRM real e regressões do servidor — 2026-10-29 20:28 BRT

- Em produção local 12.1.45, o Chrome mostrou quadros diferentes de *Moriarty the Patriot — S1:E1: O Crime do Conde* a partir do STRM real e permaneceu em `#/video`. O OSD avançou de 1:11 para 1:14 com vídeo visível; Space pausou e mostrou `Reproduzir`. Não houve retorno automático à Home; o áudio não foi medido. Após Escape, uma captura ficou preta por um instante, mas os controles voltaram a mostrar vídeo ativo; a mídia foi deixada pausada em 1:14.
- A biblioteca continha 38.513 STRMs na contagem read-only. A proteção candidata do evento `viewshow` ainda não foi acionada por uma exceção real. WebOS/modelo/versão do navegador continuam sem confirmação.
- Regressões desta sessão: Nebula Python 701/701; `ExceptionMiddlewareTests` 3/3; `verify:build` 1.929 artefatos válidos e bundle inicial 298,2 KB gzip; `git diff --check` aprovado. Vitest 425/425 e `build:check` são resultados da sessão imediatamente anterior.
- Produção permaneceu em 12.1.45, sem alteração de dados/configuração e sem instalação ou release. 12.1.46 continua candidata; homologação formal 2/7.
- O snapshot público do portal é de 20:07 BRT. A nota de 20:12 e este reteste são duas evidências posteriores; a cópia local do evento foi atualizada, mas permanece sem publicação até o próximo lote de dez.
- O `generatedAt` da cópia local foi alinhado a 20:30 BRT após a primeira validação detectar timestamp do evento mais recente que o snapshot. A suíte do contrato do portal passou 403/403 após o ajuste; o snapshot público continua em 20:07.
- Mapa técnico conferido em 2026-10-29: sem mudança estrutural em módulos, contratos, dependências ou fluxo.

### Preservação de tipos BSON no backup Supabase — 2026-10-29 20:43 BRT

- A revisão do fluxo `Tools/NebulaPython/tools/supabase_sync.py` encontrou que restore v2 interpretava qualquer string ISO como data e que backup não codificava valores BSON aninhados. Snapshots novos agora usam Extended JSON canônico v3; ObjectId/datetime aninhados são preservados e strings ISO continuam strings. Restore mantém compatibilidade com v2 e listas legadas.
- Testes Supabase focais: 51/51; suíte Nebula Python: 702/702; `compileall` e `git diff --check` passaram. A primeira rodada focal falhou em três asserções durante o ajuste do formato; após corrigir a codificação e validar datas BSON como UTC sem fuso, a suíte focal e a suíte completa passaram.
- MongoDB/Supabase reais, backup/restauração de produção e teste de limite de payload não foram executados. Nenhum banco operacional foi acessado. Produção 12.1.45; homologação formal 2/7; 12.1.46 continua candidata, sem publicação.
- Mapa técnico do vault foi conferido e atualizado nesta sessão para registrar o formato BSON versionado e a compatibilidade do restore; módulos/APIs/dependências não mudaram.

### Revalidação focal e player webOS — 2026-10-29 20:52 BRT

- Reexecução focal em Release: `ExceptionMiddlewareTests` 3/3; `NativeIntroProviderTests`, `BackupServiceTests` e `BackupRestoreDirectoryTests` no assembly de implementações: 153 aprovados, 16 ignorados, 0 falhas. Os ignorados não são contabilizados como homologados.
- `npm run build:check` no web passou. A alteração candidata em `onFullscreenChanged` deixa de navegar para trás quando o player sai do fullscreen forçado; esta execução validou apenas compilação TypeScript, sem reproduzir o defeito em webOS.
- Stage permanece bloqueado pela validação fail-closed de ACL da pasta de restauração. Não houve reprodução em TV webOS; o modelo/ano, versão do navegador e título exato continuam indisponíveis. Portanto não há confirmação de correção no aparelho.
- Produção continua em 12.1.45, sem alteração de configuração/biblioteca nesta sessão. Nenhuma release ou publicação foi feita; homologação formal permanece 2/7. Mapa técnico do vault conferido: sem mudança de módulos, APIs, dependências ou fluxo de dados.

### Portal: nova evidência no lote local — 2026-10-29 20:56 BRT

- A página pública estava no snapshot 20:07 BRT (`origin/main` 7cf6ddf). Esta revalidação foi registrada no rascunho local do portal; contrato passou 404/404, incluindo o teste que rejeita caminhos absolutos de perfis Windows.
- Há seis evidências novas desde o snapshot público: cinco anteriores, ainda não integradas ao JSON publicado, e esta revalidação adicionada ao rascunho. Conforme preferência de lote de dez, não houve commit ou push do portal nesta sessão. Quatro evidências novas faltam para publicar o próximo lote.
- Percentuais agregados e homologação formal não foram alterados: 2/7. O mapa técnico foi conferido e permanece igual.

### Fechamento da auditoria de snapshots BSON — 2026-10-29 21:03 BRT

- Snapshots novos usam BSON codificado em Base64 v4. Restore aceita v2 e listas legadas; v3 experimental é rejeitado antes de mutação porque tags EJSON e documentos literais podem ser ambíguos. Restore integral de legado exige snapshot completo não filtrado; restore seletivo permanece sem exclusão de extras.
- Testes focais Supabase: 56/56; suíte Nebula Python: 707/707; `compileall` e `git diff --check` passaram. Teste adicional garante rejeição de v3 inválido sem gravação. Chamadores encontrados limitam-se ao método de lote existente e testes; sem alteração da API pública ou dependências.
- Limitação histórica: v1/v2/legado não distinguem string ISO de data BSON e conservam conversão compatível, potencialmente ambígua. Nenhum MongoDB/Supabase operacional, restore real, produção ou limite real de payload foi exercitado. Homologação formal 2/7; sem release.
- Portal local atualizado com esta evidência: sete novas evidências pendentes desde o snapshot público 20:07 BRT; três faltam para o lote de dez. Sem commit/push. Mapa técnico do vault conferido; módulos, APIs, dependências e fluxo estrutural permanecem iguais.

### Revalidação webOS — 2026-10-29 21:36 BRT

O usuário confirmou que o sintoma ocorreu no navegador webOS de uma TV. Reexecução local: Vitest 425/425, `npm run build:check` e `git diff --check` passaram.
pm run build:check e git diff --check passaram. O controlador evita a navegação automática ao sair do fullscreen forçado; a causa do sintoma permanece hipótese sem teste E2E, TV real, modelo/versão do browser ou título exato. Produção 12.1.45 não foi alterada; homologação formal 2/7; sem release. Ver sessão no vault Sessões/2026-10-29-MulletaFlix-player-webos-revalidation-2136.

### E2E do player em stage com STRM real — 2026-10-29 21:54 BRT

Playwright 1/1 passou em stage Windows Release 12.1.46 em 173,8 s, com o STRM real `Invasão Zumbi 2 - Península (2020)`. Chromium usou marcador de user agent web0s e confirmou vinheta antes da mídia, vídeo com largura/altura e rota do player após saída do fullscreen. O STRM acessou o serviço local Nebula em 127.0.0.1:2124; MariaDB do stage ficou em 127.0.0.1:33100. O stage foi encerrado, sem listener na faixa Playwright; produção 8096 não foi usada. Isto não comprova navegação/codec/áudio em TV webOS física. Portal local atualizado, contrato 408/408; lote pendente 5/10, não publicado. Produção 12.1.45, homologação formal 2/7, sem release. Ver sessão no vault `Sessões/2026-10-29-MulletaFlix-player-webos-revalidation-2136`.

Análise dos três logs FFmpeg do teste: fonte Matroska contém H.264 High 1920×1080 e AAC estéreo português; saída HLS contém faixas H.264/AAC e quadros/segmentos. O processo registra `[q] command received. Exiting.` antes do status `FFmpeg exited with code -1`; no E2E, vídeo apresentou dimensões e 1/1 passou. A sequência indica encerramento solicitado do job depois de gerar saída, sem provar causa do sintoma na TV real.

### Revalidação local de testes — 2026-10-29 22:01 BRT

- Nebula Python completo: 707/707 aprovados; foco `test_supabase_sync.py`: 56/56. O Python global não tem pytest, então foi usada a `.venv` do projeto.
- `ExceptionMiddlewareTests` em .NET Release: 3/3 aprovados. O teste novo confirma que a exceção original é registrada quando a resposta HTTP já começou.
- `npm run build:check` e `git diff --check` passaram. A compilação .NET emitiu avisos de documentação/analisadores já existentes em arquivos sem relação com a mudança.
- Não houve alteração adicional no código nesta revalidação, acesso/escrita na produção, release ou publicação do portal. O E2E webOS validado anteriormente é Chromium simulado; TV real e saída de áudio seguem pendentes. Homologação formal 2/7.
### Consultas em lotes durante exclusões extensas — 2026-10-29 23:01 BRT

- `DeleteItem` divide consultas de hierarquia e `OwnerId` em lotes de até 1.000 IDs, expande proprietários recursivamente e limpa referências `OwnerId` internas antes da remoção. Isso evita violação da FK `BaseItems.OwnerId` (`NoAction`) quando pai e filho caem em lotes diferentes. A travessia das pastas raiz também usa lotes.
- `ItemPersistenceServiceTests`: 33/33 aprovados em Windows/.NET 10 Release; o teste MariaDB opt-in foi ignorado nessa rodada porque a conexão descartável não estava configurada. MariaDB 11.4.4 isolado em `127.0.0.1:33251`: integração 1/1 aprovada, removendo 2.005 descendentes e itens com `OwnerId` encadeado, preservando Placeholder e mídia não relacionada. A primeira asserção de integração não contou o Placeholder criado pelo esquema; expectativa corrigida sem afrouxar a verificação dos itens removidos.
- O teste confirma SQL, FK e travessia no MariaDB isolado; biblioteca de produção, Linux hospedado e carga real não foram usados. Latência e memória em catálogo real seguem sem medição. Produção 12.1.45; homologação formal 2/7; sem release.
- Portal recebeu evidência local no lote seguinte ao commit `045f4c5`; não publicar até completar dez. Mapa técnico conferido: mesmas APIs, dependências e fluxo de persistência, com consultas internas agora paginadas. Ver sessão no vault `Sessões/2026-10-29-MulletaFlix-delete-query-batching-2301`.

#### Addendo — retenção antes de cascatas e desempates — 2026-10-29 23:49 BRT

- Revisão independente encontrou casos entre lotes e empate: cascata `ParentId` podia remover UserData antes da migração, Placeholder vencedor podia manter `RetentionDate` expirado, e itens empatados não tinham ordenação definida. A rotina agora migra todos os UserData antes de apagar qualquer mídia, renova o prazo quando Placeholder vence, e usa a mesma comparação em candidatos e Placeholder: data, contagem, Favorito, assistido, gostei, progresso, avaliação, faixas de áudio/legenda configuradas (menor índice primeiro); candidatos com empate completo usam `ItemId` crescente.
- Integração em MariaDB 11.4.4 descartável validou 2.005 descendentes com cascata `ParentId`, cadeia `OwnerId`, retenção do UserData mais recente, empates entre candidatos e Placeholder preservando Favorito/progresso/avaliação/faixas e prazo expirado renovado: 1/1; regressão focal completa: 34/34. Servidor de teste na porta 33251 foi encerrado; a porta MariaDB de produção 3306 permaneceu intocada.
- Sem medição em catálogo real, validação Linux/CI hospedada, acesso a dados de produção ou aceite em hardware. Produção 12.1.45; homologação formal 2/7; sem release.

### Contagem parcial no restore Supabase — 2026-10-30 01:49–01:56 BRT

- `PerformRestoreCoreAsync` atualiza `FilesRestored` depois de cada página de catálogo que `BulkUpsertRawDocsAsync` confirma. O mesmo progresso agora é refletido em usuários FTP, tokens de bot e refreshes VFS, após cada upsert Mongo confirmado. Linhas FTP sem `_id` ou login válido não contam como persistidas. `UsersRestored` acompanha os usuários FTP confirmados; usuários do app só entram na soma após `SaveChangesAsync` bem-sucedido.
- `NebulaSupabaseFileRestoreTests`: 9/9 aprovados em Debug e 9/9 em Release. Foram adicionados testes de integração para falha depois de persistir token/refresh, mas os 4 testes de `NebulaSupabaseRestoreMongoTests` foram ignorados nesta sessão porque a instância Mongo descartável em `127.0.0.1:27099` não está disponível; os testes compilam.
- Limite: se `BulkWriteAsync` lançar após aplicar parte de uma página não ordenada, o callback não recebe o número parcial dessa página. `forceFullRestore` é recebido mas atualmente ignorado; sem especificação segura para restore destrutivo, comportamento permanece pendente de decisão.
- Sem MongoDB/Supabase operacionais, dados de produção, runtime instalado ou homologação formal. Produção conhecida 12.1.45; candidata 12.1.46 não publicada; homologação formal 2/7.

### Navegação no fullscreen forçado do webOS — 2026-10-30 00:00 BRT

- O encerramento do fullscreen forçado podia disparar appRouter.back() sem que a reprodução terminasse, compatível com o player saindo da tela enquanto a mídia continuava.
- onFullscreenChanged agora apenas atualiza o ícone. O retorno à tela anterior segue no evento de parada. Retirada a supressão experimental após erro, que podia deixar a rota do player vazia quando o player era destruído.
- Verificação desta sessão: Vitest web 425/425; npm run build:check; git diff --check. O E2E com STRM real e webOS simulado tem novas asserções de avanço de quadros antes/depois do fullscreen, ainda sem execução nesta sessão. Stage/TV física não foram testados; produção 12.1.45 não foi alterada.
- Causa do retorno à Home sustentada pelo fluxo do código; a ausência de vídeo/continuidade de áudio na TV não foi reproduzida. Homologação formal segue 2/7; candidata 12.1.46 sem release.
- Arquivos: MulletaFlix-web-master/src/controllers/playback/video/index.ts, MulletaFlix-web-master/tests/playwright/specs/22-adsense-flow.spec.ts, MulletaFlix-master/docs/releases/12.1.46.md, MulletaFlix-master/docs/backend-auditoria-roadmap.md.

## Reauditoria das consultas quentes — 2026-10-30 01:27 BRT

- Conferidos `BaseItemRepository.Querying.cs` e `NextUpService.cs`. A seleção de filmes/séries recentes mantém streaming até o limite de itens distintos; `AsEnumerable` posterior é necessário para deduplicação/serialização e `NextUp` usa projeção leve depois dos filtros do banco. Nesta revisão não foi encontrado um limite prematuro que pudesse truncar duplicatas antes da deduplicação.
- Testes focais: `BaseItemRepositoryLatestTvShowTests` e `NextUpQueryOptimizationTests` passaram 8/9; 1 integração MariaDB foi ignorada porque o opt-in de conexão não está configurado. Sem benchmark de catálogo real, SQL/EXPLAIN MariaDB ou produção.
- Resultado limitado à inspeção dos métodos e testes locais; não conclui a auditoria de consultas nem valida desempenho. O bloco permanece em andamento.
- Suíte ampla `Jellyfin.Server.Implementations.Tests`: 1.668 aprovados, 85 ignorados e 0 falhas/1.753 em Windows/.NET 10; os ignorados incluem MariaDB opt-in e casos exclusivos de Linux. O resultado não mede desempenho real.

## Restore Supabase — contadores por etapa com Mongo descartável — 2026-10-30 02:08 BRT

- `NebulaSupabaseSyncService` mantém os totais confirmados de catálogo, usuários FTP, tokens de bot e refreshes VFS quando uma etapa posterior falha. Linhas FTP sem `_id` ou login não incrementam o subtotal. Sem mudança de API pública, módulo ou dependência.
- `NebulaSupabaseRestoreMongoTests`: 4/4 aprovados em Debug e Release usando MongoDB 8.3.7 descartável em loopback. Todos os testes `Category=RequiresMongo` de `Jellyfin.Server.Implementations.Tests`: 28/28 aprovados em Release. A primeira tentativa da regressão VFS encontrou resposta vazia sobrando no fixture; o fixture foi corrigido e repetido com sucesso.
- O processo de teste foi encerrado e a porta `27099` fechada. Os arquivos do banco de teste continuam em `%TEMP%`; remoção foi recusada pela política do shell. Sem Supabase real, MongoDB 7.0, produção ou runtime instalado.
- Riscos restantes: falha parcial dentro de `BulkWriteAsync` sem contagem das operações aplicadas; `forceFullRestore` ainda é ignorado até existir contrato seguro para exclusão. Produção conhecida 12.1.45; candidata 12.1.46 sem release; homologação formal 2/7.
- Reauditoria dos callers 2026-10-30 02:13 BRT: todos os chamadores de produção encontrados passam `forceFullRestore: false`; o caminho público que não recebe o parâmetro também fixa `false`. Nenhum caller com `true` foi encontrado. O parâmetro é atualmente inerte, e removê-lo ou atribuir semântica continua dependendo do contrato de restore; nenhuma deleção foi introduzida.

### Regressão local atualizada — 2026-10-30 02:35 BRT

- Solução .NET Release: 17 assemblies, 5.395 aprovados, 267 ignorados, 0 falhas/5.662; `LiveIntegrationTests` excluídos. Um teste inicialmente falhou por esperar `UsersRestored=null` apesar de o subtotal FTP conhecido ser zero; a asserção foi alinhada ao contrato existente (`UsersRestored=0`, etapa do aplicativo ainda nula). Teste focal e regressão completa repetidos; saída final da solução 0.
- Nebula Python: 714/714 aprovados. Esses resultados não executam CI hospedada nem provam runtime instalado/produção. Integrações opt-in ignoradas continuam pendentes; MongoDB operacional em 3306 não foi acessado.
- Mudança desta rodada limitada a uma asserção de teste; nenhum código de runtime, módulo, API, dependência ou fluxo mudou. Mapa técnico conferido e permanece igual. Produção conhecida 12.1.45; candidata 12.1.46 sem release; homologação formal permanece 2/7.
## Revalidação de imagens para séries e animações — 2026-11-01 01:01 BRT

- `ItemImageProvider` aplica a ordenação compartilhada para `Movie` e `Series`. A cobertura focal agora verifica Primary, Backdrop, Logo e Thumb e confirma fallback inglês quando o candidato localizado falha.
- `ProviderManager` confirma prioridade `pt-BR` entre provedores também para `Series`; animações são representadas por `Movie` ou `Series`, sem entidade independente.
- Testes focais Release/.NET 10: 18 aprovados, 0 ignorados/falhas. Testes locais simulam provedores; não confirmam TMDb/Fanart reais, atualização de imagens persistidas ou produção.

## Ordenação determinística de “Continuar assistindo” — 2026-11-01 01:05 BRT

- `NextUpService.GetNextUpSeriesKeys` agora desempata séries com a mesma data de reprodução pela chave da série, antes de aplicar `Limit`; resultados não dependem da ordem acidental do banco.
- Regressão insere três séries com data igual, limita a duas e confirma as chaves `SeriesA`, `SeriesB`. `NextUpQueryOptimizationTests`: 3/3 em Release/.NET 10.
- O teste usa EF InMemory; tradução SQL, plano MariaDB e ordenação de catálogo real não foram medidos. Sem mudança de esquema, API ou dependência.

## Lacuna de paginação de Next Up — 2026-11-01 01:07 BRT

- Inspeção estática confirmou que `TvSeriesManager.GetNextUp` recebe `request.Limit`, mas passa `Limit = null` para `GetNextUpSeriesKeys` no fluxo global. O serviço materializa todas as chaves elegíveis; a resolução dos episódios e a página ocorrem depois.
- Isso comprova trabalho sem limite de séries antes da paginação, não uma regressão de latência medida. Não apliquei um `Take` antecipado: séries sem próximo episódio podem consumir a janela e excluir resultados válidos.
- Próximo passo seguro: paginação progressiva de chaves ordenadas até preencher a página requisitada ou esgotar os candidatos; validar ordenação/contagem em MariaDB descartável e medir `EXPLAIN ANALYZE` antes de alterar índice ou consulta.

### Revalidação focal — 2026-11-01 01:49 BRT

- `NextUpQueryOptimizationTests` e `BaseItemRepositoryLatestTvShowTests`: 9 aprovados, 2 ignorados, 0 falhas em Release/.NET 10. Os ignorados são integrações MariaDB opt-in sem conexão descartável configurada.
- A reexecução valida os testes de desempate e deduplicação existentes, mas não mede desempenho, tradução SQL MariaDB ou catálogo de produção. O fluxo global ainda materializa as chaves antes da paginação final; a correção segue pendente até haver paginação progressiva que preserve a contagem exata e não perca séries sem episódio elegível.
- Não houve alteração de runtime nem de estrutura do servidor.

### Contagem de NextUp encaminhada pelo UserView — 2026-11-01 01:52 BRT

- `UserViewBuilder.GetTvNextUp` agora encaminha `InternalItemsQuery.EnableTotalRecordCount` à `NextUpQuery`. O caminho de filhos da `UserView` desativa a contagem total; antes, a query nova voltava ao padrão `true` e fazia `TVSeriesManager.GetResult` materializar novamente todos os resultados para contar.
- O teste `UserViewBuilderTests.GetUserItems_TvNextUp_PropagatesDisabledTotalCount` reproduziu a regressão antes da correção e passou depois. `Jellyfin.Controller.Tests`: 74/74 em Release/.NET 10.
- Isso remove uma materialização duplicada quando a contagem está desativada; não limita a busca de chaves/episódios do fluxo global. Paginação progressiva, SQL/EXPLAIN MariaDB e desempenho com biblioteca real seguem pendentes.
- Mudança localizada ao encaminhamento de uma opção existente; sem alteração de API pública, esquema ou dependência.

## Consulta de séries recentes — cobertura MariaDB — 2026-11-01 01:11 BRT

- `GetLatestTvShowItems` ordena episódios no banco, depois percorre o resultado para reter a primeira ocorrência de cada `SeriesId` e aplicar o limite. A consulta pode percorrer mais episódios que a quantidade de cards retornados; ainda não há medição do custo em catálogo real.
- Adicionada regressão MariaDB opt-in com episódios repetidos por série, ordenação por data e limite após deduplicação. Em MariaDB 11.4.4 descartável, os testes de séries recentes e subconsulta de álbuns passaram 2/2; as seis regressões sem banco passaram na suíte Release.
- A tradução e o resultado funcional foram validados no provider MariaDB. Nenhuma consulta ou índice foi alterado. `EXPLAIN ANALYZE` com volume representativo e comparação de desempenho seguem pendentes antes de redesenhar a deduplicação.

## Seleção manual de imagens preserva idioma e provider — 2026-11-01 01:22 BRT

- `RemoteImageController.DownloadRemoteImage` agora aceita `providerName` opcional, que já é enviado pelo seletor web. O servidor reconsulta esse provider para a URL selecionada entre todos os idiomas e persiste `ProviderName`/`Language` no registro da imagem.
- Cobertura para `Movie` e `Series` (inclui animação representada por qualquer tipo), locales `pt-br`, `en-us`, `zh-hant`; clientes antigos sem `providerName` continuam compatíveis. Se a URL não for retornada, a gravação segue sem preencher proveniência desconhecida.
- `RemoteImageControllerTests`: 8/8; suíte `Jellyfin.Api.Tests`: 805 aprovados, 3 ignorados, 0 falhas/808 em Windows/.NET 10 Release. Portal local: 427/427; nono evento do lote 9/10.
- Provedores reais, arquivos MariaDB já persistidos, ambiente instalado e produção não foram validados. Nenhuma release publicada; produção conhecida 12.1.45, candidata 12.1.46, homologação formal 2/7.
## Supervisão do processo Downloader Nebula — 2026-11-01 01:34 BRT

- `NebulaHostedService` agora verifica a cada 30 segundos se o processo downloader iniciado continua ativo. Se encerrou, tenta recuperá-lo pelo `INebulaFtpManager` sob o lock de transição existente; Envio e montagem do disco não são reiniciados por essa rotina.
- O sinal de vida foi extraído para `NebulaFtpManager.IsDownloaderRunning` e reutilizado pelo DTO de status. A verificação evita `GetStatusAsync`, que também consulta filas MongoDB.
- `NebulaHostedServiceConfigurationTests`: 21/21 aprovados em Release/.NET 10, incluindo o monitor periódico real sob espera controlada, retry após falha e shutdown durante recuperação. A regressão combinada `NebulaHostedServiceConfigurationTests`, `NebulaDownloaderStartupTests` e `NebulaCleanupLifecycleTests` passou 66/66. Build mostrou avisos XML preexistentes em `NebulaSupabaseSyncService` e avisos de análise já distribuídos pela suíte.
- Não executado com processo Python real, runtime instalado, disco de produção, Telegram, MongoDB operacional ou produção. 12.1.46 segue candidata; produção conhecida 12.1.45; homologação formal 2/7, sem crédito.

## Paginação progressiva de Next Up — 2026-11-01

- O caminho global de `TVSeriesManager.GetNextUp` agora busca séries candidatas em blocos de 50, mantendo a ordenação por `LastPlayedDate` e chave. Continua consultando até preencher a página e comprovar que os candidatos seguintes não podem alterar a ordem; empate na fronteira continua a busca. `EnableTotalRecordCount`, rewatching e solicitações sem limite continuam usando o caminho completo.
- Regressões cobrem início da próxima página quando necessário, lotes sem episódio elegível, empate na fronteira, parada somente quando datas candidatas são estritamente anteriores e `StartIndex` aplicado após desempate estável.
- Suíte `Jellyfin.Server.Implementations.Tests`: 1.680 aprovados, 90 ignorados, 0 falhas/1.770 em Release/.NET 10. Testes MariaDB do provider seguem opt-in e não rodaram; sem medição com biblioteca real, produção ou benchmark.
- Mudança candidata 12.1.46, sem release publicada; produção conhecida 12.1.45; homologação formal permanece 2/7. O contrato de `GetNextUpSeriesKeys` agora retorna também a data máxima candidata. Mapa técnico conferido: interfaces/callers existentes alterados, sem módulo ou dependência nova. Risco de offset sob alteração concorrente do histórico está documentado no código.

## Next Up validado pelo provider MariaDB — 2026-11-01 02:21:40 BRT

- Adicionado teste opt-in NextUpServiceMariaDbTests para validar a query real de candidatos com o provider Pomelo/MariaDB: agrupamento por série, MAX(LastPlayedDate), desempate alfabético, Skip/Take e retorno da data persistida.
- Executado em MariaDB 11.4.4 descartável, loopback 127.0.0.1:33127, schema aleatório com prefixo mfx_nextup_test_: 1/1 aprovado. O provider traduziu e executou a consulta. A diferença de precisão sub-microssegundo foi normalizada na asserção (DATETIME(6)).
- Regressão focal sem conexão opt-in: 7 aprovados, 1 ignorado/8 (o teste MariaDB); com MariaDB, o teste de integração passou 1/1. A instância temporária foi encerrada, porta fechada e diretório de dados descartável removido. O PID 19840 de produção permaneceu em execução e não recebeu conexão.
- Sem EXPLAIN ANALYZE, benchmark ou catálogo real. Produção conhecida 12.1.45; candidata 12.1.46 sem release; homologação formal 2/7.

## Paginação progressiva Next Up por cursor — 2026-11-01 02:32 BRT

- `TVSeriesManager.GetNextUp` agora continua os lotes com o par `(LastPlayedDate, SeriesKey)` em vez de `StartIndex`; inserções/remoções antes do lote já lido não deslocam o próximo lote e evitam repetição/pulo causado por offset.
- `NextUpService` aplica `LastPlayedDate < cursorDate OR (LastPlayedDate = cursorDate AND SeriesKey > cursorKey)` depois do agrupamento; ordem, predicado e paginação foram executados pelo Pomelo/MariaDB.
- Testes focais Release/.NET 10: `TVSeriesManagerNextUpPaginationTests` + `NextUpQueryOptimizationTests` + integração opt-in: 8 aprovados, 1 ignorado sem conexão; com MariaDB 11.4.4 descartável em `127.0.0.1:33127`, `NextUpServiceMariaDbTests` passou 1/1 e verificou cursor no empate. O teste do manager insere uma chave anterior ao cursor entre páginas.
- Reexecução da suíte completa `Jellyfin.Server.Implementations.Tests`: 1.681 aprovados, 91 ignorados por opt-in/plataforma, 0 falhas/1.772 em Release/.NET 10.
- Limite: `LastPlayedDate` pode mudar através do cursor durante a mesma requisição; a paginação não cria snapshot transacional. Não houve EXPLAIN, benchmark, biblioteca real ou produção. O teste temporário foi encerrado e seus dados removidos; MariaDB de produção PID 19840 não recebeu conexão.
- Candidata 12.1.46; produção 12.1.45; homologação formal 2/7; sem release. Ver sessão `2026-11-01-MulletaFlix-nextup-keyset-pagination-0232`.
## Plano de execução de séries recentes — 2026-11-01 02:50 BRT

- O SQL foi capturado do teste MariaDB real de `GetLatestItemList_TvShows_UsesMariaDbAndDeduplicatesBeforeApplyingLimit`: filtra `SeriesId IS NOT NULL` e versões/owners elegíveis, ordena por `DateCreated DESC, Id DESC` e transmite episódios para deduplicação em C#.
- Em MariaDB 11.4.4 descartável, com 100.000 linhas sintéticas (80.000 episódios e 5.001 séries, uma série longa com 65.000 episódios), `ANALYZE FORMAT=JSON` observou full scan + filesort: 45,7 ms, 100.000 linhas lidas e 80.000 ordenadas. Forçar índice isolado `DateCreated` levou 127,8 ms e 300.315 páginas acessadas. A forma equivalente com `ROW_NUMBER()` leu 80.000 episódios, materializou a janela e levou 63,8 ms.
- Mantida a consulta atual: as alternativas medidas pioraram; não há evidência que justifique mudança de código ou índice. Os números são sintéticos e não predizem latência da biblioteca real. O teste do plano executa a consulta inteira; o consumidor de produção pode parar a enumeração depois de atingir seu limite.
- A instância e o datadir descartáveis foram removidos; PID operacional 19840 continuou ativo em `127.0.0.1:3306` e não recebeu conexão. Produção, tamanho/distribuição do catálogo real e impacto no tempo da primeira linha seguem pendentes.

## Filtros reais da faixa de séries recentes — 2026-11-01 02:58 BRT

- Revisão do caminho `UserViewManager` → `LibraryManager.GetLatestItemList` → `BaseItemRepository` confirmou filtros da Home ausentes da medição anterior: `IncludeItemTypes=Episode`, `IsVirtualItem=false` e `TopParentIds` derivados das bibliotecas visíveis. A captura/benchmark de 02:50 representa o método do repositório sem esses filtros e não é evidência do plano efetivo da Home.
- A integração MariaDB foi ampliada para aplicar os mesmos três filtros e incluir um episódio mais recente pertencente a outra biblioteca; a asserção garante que o conteúdo de fora da biblioteca não entre no resultado. A compilação Release passou, mas a execução do teste MariaDB foi ignorada porque `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING` não está configurada nesta rodada.
- Portanto, SQL final com filtros de biblioteca, plano `ANALYZE FORMAT=JSON`, volume real e latência até a primeira série continuam pendentes. Nenhuma mudança de runtime/índice ou conclusão de desempenho foi feita; nenhuma conexão ao banco operacional foi aberta.

## Gate .NET Release do servidor — 2026-11-01 03:15 BRT

- `dotnet test MulletaFlix.sln --configuration Release --no-restore --no-build --filter "FullyQualifiedName!~Integration"` terminou com exit 0 em 15 assemblies: 5.387 aprovados, 93 ignorados, 0 falhas (5.480 casos). Os 93 ignorados incluem testes opt-in, Linux/macOS e a suíte de integração removida pelo filtro; não equivalem a validação dessas plataformas/integrações.
- Separadamente, o contrato do portal passou 73/73 depois de remover os asserts de mês fixo e isolar o evento de teste antigo da janela de eventos recentes.
- A compilação reportou warnings XML/StyleCop/xUnit já presentes, sem warnings tratados como falha. Nenhum serviço, runtime, banco operacional, provedor externo ou mídia real foi usado; sem prova de produção, instalação, Linux nativo, smoke de reprodução ou homologação formal.

## PR #1 — recuperação isolada no CI — 2026-11-01 03:55 BRT
- Diagnóstico do teste reproduziu que o helper invocava restore completo, que o serviço rejeita antes de modificar dados; o cenário é opt-in/integrado e sua intenção é recuperar fontes isoladas.
- Alteração mínima em `BackupRestoreMariaDbTests.cs`: helper passa a solicitar restore incremental. Contrato de produção fail-closed e teste que prova a rejeição do restore completo foram preservados.
- Verificação local: teste focal 1/1; regressões de fail-closed 2/2; suíte `IntroSkipper.Integration.Tests` 59/59 em Release/.NET 10 contra MariaDB 11.4.4 e MongoDB 8.3.7 descartáveis.
- Commit `de1f25a0` enviado à branch `codex/fail-closed-inplace-updater`. Novo run GitHub `41282646578` no SHA `de1f25a0df3fe94b2864570f836b62288e75a3a7`: 16 jobs em failure sem execução. Anotação consultada para `Full recovery exercise` confirma bloqueio da conta por billing; não há log de teste remoto. Regularizar cobrança e executar novo run continuam pendentes.
- Sem mudança de runtime, release, produção ou crédito de homologação formal. Dados temporários do teste mantidos em `%TEMP%` após rejeição da política de remoção.

## Gate Release amplo da solução — 2026-11-01 04:01 BRT
dotnet test MulletaFlix.sln --configuration Release --no-restore --filter "FullyQualifiedName!~LiveIntegrationTests" --verbosity quiet terminou com exit 0 e zero falhas nos assemblies que reportaram resultado. Exemplos confirmados no console: Server.Implementations 1.681 aprovados/91 ignorados; IntroSkipper.Integration 26 aprovados/33 ignorados; API 805/3; Providers 849/0; Server.Tests 104/0. Vários projetos adicionais também passaram. O log desta execução não forneceu agregado global auditável, então não atribuo um total geral. Avisos XML/analisadores foram exibidos, sem erro de build. O filtro exclui testes live; opt-ins de banco/plataforma permanecem ignorados. GitHub Actions no SHA atual continua sem iniciar por bloqueio de billing (run 41282646578); produção, release e homologação formal não mudaram. Mapa técnico conferido, sem alteração estrutural. Ver [[Sessões/2026-11-01-MulletaFlix-release-solution-regression-0401]].

## Regressão .NET e Nebula Python — 2026-11-01 04:02 BRT
- `dotnet test MulletaFlix.sln --configuration Release --no-restore --filter "FullyQualifiedName!~LiveIntegrationTests" --verbosity quiet`: exit 0 e zero falhas nos projetos que reportaram resultados. Server.Implementations 1.681 aprovados/91 ignorados; IntroSkipper.Integration 26/33; API 805/3; Providers 849/0; Server.Tests 104/0. Sem agregado global auditável; não inferido. Avisos XML/analisadores sem erro de build.
- Nebula `.venv` Python 3.10: `python -m pytest -q --tb=short`: 714/714 em 9,12 s.
- WSL está habilitado sem distribuição Linux; testes Linux nativos não executados. GitHub Actions run 41282646578 continua sem iniciar por bloqueio de billing. `LiveIntegrationTests` excluídos; skips MariaDB/Mongo/plataforma pendentes. Sem mudança runtime/release/gate formal. Mapa técnico conferido e inalterado. Ver [[Sessões/2026-11-01-MulletaFlix-nebula-python-regression-0402]].

## PR #1 — updater copia conteúdo mesmo com metadados coincidentes — 2026-11-01 04:08 BRT
- Reprodução red: o teste de falha demonstrou que arquivos antigos e novos com mesmo tamanho e timestamp podiam não ser copiados pelo Robocopy, e que esse caminho também não possuía snapshot do conteúdo antigo.
- Correção mínima em `apply-update.ps1`: comparar SHA-256 apenas quando tamanho e timestamp coincidem; se os bytes diferirem, salvar snapshot, avançar temporariamente o timestamp de origem para acionar Robocopy e restaurar os timestamps da origem e do pacote. Falhas de restauração de timestamp entram no rollback.
- Testes `ApplyUpdateFailure`, `ApplyUpdateFailClosed` e `ApplyUpdateStartupFailure` passaram no PowerShell 5.1 e PowerShell 7.6.9; nove scripts Windows de integridade/empacotamento passaram. O caminho `process-ready` usou o Robocopy real. `UpdateInfoControllerTests`: 30/30 em Release e confirma o recurso embutido.
- `git diff --check` passou antes da edição documental. CI GitHub, runtime instalado, instalação e produção ainda pendentes; Actions estava bloqueado antes dos runners por cobrança. Sem crédito de homologação formal e sem release publicada.
- Mapa técnico conferido; fluxo e módulos existentes, sem mudança estrutural ou dependência.

- Atualização após push: commit `cbc380c9` enviado ao PR #1. Actions run `41283703952` falhou antes de iniciar todos os jobs por bloqueio de billing; anotações de servidor, empacotamento Windows e recuperação integral confirmam a causa. Não há log de execução para aferir o CI. CLI `gh` sem login; consulta via conexão GitHub Codex.

- Revalidação adicional em 04:13 BRT: os nove scripts Windows de integridade/empacotamento passaram também em Windows PowerShell 5.1; o cenário de update com Robocopy e as regressões de rollback passaram. Reviewer read-only do commit cbc380c9 não encontrou problemas. Docker não está instalado e não há distribuição WSL ativa; Linux permanece pendente. CI hospedada bloqueada por cobrança antes dos runners.

## Nebula main.py — auditoria de cobertura — 2026-11-01 04:15 BRT
- `python -m pytest -q --cov=main --cov-report=term-missing:skip-covered`: 714/714 em Python 3.10.11. Cobertura focal `main.py` 77%, 1.780 statements, 404 ausentes; não representa cobertura global.
- Lacunas incluem handlers/compactação de logs, fila e ramos de `main()` para startup/control-plane/shutdown. Os testes existentes de lifecycle cobrem algumas falhas e modo streaming; ampliar com mocks e sem serviços reais antes de mudar runtime.
- Linux/MariaDB case-sensitive não validado: Docker ausente e WSL sem distribuição ativa. CI Actions continua bloqueada antes dos runners por billing. Nenhuma alteração de runtime.

### Nebula Python: falha de startup e encerramento de MongoClient — 2026-11-01

- A auditoria encontrou que os retornos de erro após criar `AsyncIOMotorClient` não fechavam a conexão, e o shutdown normal também não fechava o cliente. `main()` agora fecha o cliente nas falhas de índice Mongo, configuração FTP/TLS, bind FTP/HTTP e no encerramento normal; erro de fechamento não mascara o erro original.
- Testes primeiro falharam nos três retornos de erro cobertos, reproduzindo o vazamento. Após correção, os cenários focalizados de erro e shutdown passaram 5/5. Suíte Nebula Python passou 719/719; `main.py` 80% (1.794 statements, 364 ausentes). `compileall` e `git diff --check` passaram.
- Também corrigidos nesta fatia os handlers de log: fallback de encoding substitui caracteres que o terminal não suporta; falha ao gravar contexto de compactação mantém o log ativo intacto. Testes focais de logging 5/5.
- Testes usam doubles e arquivos temporários; não validam runtime instalado, MongoDB de produção ou serviço em produção. CI GitHub Actions do PR #1 continua bloqueada antes dos runners por cobrança da conta; não houve release nem crédito de homologação formal.

### Nebula: teste de falha ao carregar TLS — 2026-11-01

- Caminho de `main()` sem teste agora verifica o comportamento de certificado TLS ausente: startup retorna erro, não chama `FTPServer.start` em texto puro, fecha Mongo e para os bots já iniciados.
- Teste focal passou 1/1. Suíte Nebula Python passou 720/720; `main.py` ficou em 80% (1.794 statements, 356 sem cobertura). `compileall` aprovado.
- Só teste e documentação mudaram nesta fatia; a proteção TLS já existia. Sem validação de certificado/runtime instalado, Linux, produção ou CI hospedada. Não há crédito de homologação formal nem release publicada.


## Regressão ampla local do servidor — 2026-11-01 05:27 BRT

dotnet test MulletaFlix.sln --configuration Release --no-restore --filter 'FullyQualifiedName!~LiveIntegrationTests' --verbosity minimal encerrou com exit 0. O assembly MulletaFlix.Server.Implementations.Tests reportou 1.682 aprovados, 91 ignorados e zero falhas (1.773 total). Nebula Python 3.10.11: pytest tests -q, 720/720 aprovados. Os testes .NET ignorados incluem dependências opt-in de MariaDB/MongoDB e condições exclusivas de plataforma; LiveIntegrationTests foram explicitamente excluídos. Isso é regressão local, não homologação formal, CI hospedada ou validação em produção; homologação permanece 2/7 e candidata 12.1.46 não foi publicada. Mapa técnico conferido sem alteração estrutural. Ver [[Sessões/2026-11-01-MulletaFlix-server-full-regression-0527]].

- Contrato do espelho local do portal: 79/79 em 2026-11-01; o novo evento não altera coverage.completed=328 nem coverage.pending=65.


## Backup restore: caminho da raiz trocado após abrir handle — 2026-11-01 05:32 BRT

Adicionado teste adversarial de troca do caminho Config durante o restore, após a transação já ter aberto o diretório e movido o original para backup. No Windows, exige que a renomeação seja bloqueada pelo handle; no Linux, tenta renomear o diretório e colocar symlink para pasta externa. O arquivo externo sentinela precisa permanecer intacto; conteúdo restaurado deve permanecer no diretório originalmente aberto. Focal passou 1/1 em Windows/.NET 10 Release; FullSystemBackup passou 155, ignorou 16, falhou 0. O ramo Linux ainda não foi executado; a lacuna TOCTOU global em outros fluxos permanece P0. Sem mudança de runtime. Formal 2/7, sem release/produção. Mapa estrutural inalterado. Ver [[Sessões/2026-11-01-MulletaFlix-restore-root-path-replacement-0532]].

- Contrato do espelho local do portal passou 80/80; evento não altera coverage.completed=328 nem coverage.pending=65. Dois eventos novos estão no lote local pendente.
### 2026-11-01 — OMDb cache path traversal guard

Validated every OMDb cache path boundary (`GetDataFilePath`, `GetSeasonFilePath`) and normalized incoming IMDb IDs to `tt` plus ASCII digits before path composition and HTTP lookup. New path-safety tests cover valid prefixed/unprefixed IDs and slash, backslash, traversal, alphabetic, and empty-suffix cases. Focused OMDb suite: 20/20 Release on Windows/.NET 10. This does not close cache-directory symlink/TOCTOU risks or grant formal homologation; formal remains 2/7. No production or external provider request was made.
### 2026-11-01 — AudioDB MBID cache path validation

Artist and album cache directories now require canonical MusicBrainz UUIDs (`Guid.TryParseExact`, format D) before path composition. Direct download methods validate before any HTTP request. Focused safety suite: 7/7 Release on Windows/.NET 10, including traversal rejection and no request for invalid IDs. Official MBID format is a 36-character UUID. Cache-directory symlink/TOCTOU remains separate; no formal homologation credit.

## Revalidação da PR #1 — bloqueio de cobrança do GitHub Actions — 2026-11-01 06:03 BRT

O diagnóstico conectado do GitHub confirmou o PR #1 no HEAD `cbc380c94e8c88278c44309f586faf9c0e1c134f` e o run mais recente observado, [41283703952](https://github.com/raphaelpera85/mulletaflix-completo/actions/runs/41283703952). Os 16 jobs falharam antes de iniciar com a anotação `The job was not started because your account is locked due to a billing issue.` O endpoint de detalhe do job retornou `log: null`; não existem steps executados para diagnosticar como falha de código. `gh pr view`/`gh pr checks` continuam sem sessão CLI autenticada. Nenhuma alteração de código, commit, push ou reexecução ocorreu. Próximo passo depende de resolver a cobrança/desbloquear a conta e autenticar `gh`; depois, reexecutar os checks antes de qualquer correção de código.

## Fechamento da auditoria estática de gravadores — 2026-11-01 06:03 BRT

O escopo de callers de legendas e imagens foi ampliado. `SubtitleManager.TrySaveToFiles` valida destino antes de criar diretório/abrir arquivo por pathname; `FileMode.CreateNew` evita sobrescrever o arquivo final, mas não ancora os diretórios validados. `ImageSaver.SaveImageToLocation` cria o diretório e abre com `FileMode.Create`, sem proteção observada no ponto de escrita contra symlink no arquivo final. Callers incluem uploads em `SubtitleController`/`ImageController`, providers e `ProviderManager`. São achados de revisão estática; nenhuma exploração de corrida foi reproduzida e as raízes/capacidade de alteração não foram validadas em produção.

Não há primitiva handle-relative reutilizável nas camadas `MediaBrowser.Model`/`MediaBrowser.Common`; `BackupRestoreDirectory` contém operações seguras por handle, mas é interno a `Jellyfin.Server.Implementations`, acima de `MediaBrowser.Providers`. `AsyncFile` e `IConfigurationManager` permanecem baseados em pathname. A auditoria foi concluída; o reparo da escrita segura permanece pendente e requer uma primitiva compartilhada em camada inferior, compatível com Windows/Linux, mais testes adversariais em ambas as plataformas. Nenhuma API, dependência ou fluxo runtime mudou nesta sessão; mapa técnico conferido.

### Proveniência de raízes em ImageSaver — 2026-11-01 06:12 BRT

Revisão dos destinos confirmou três classes de raiz: pasta da biblioteca (`ContainingFolderPath`, `SeriesPath` ou pasta do arquivo), metadata interno (`GetInternalMetadataPath`) e configuração do servidor para upload de perfil (`UserConfigurationDirectoryPath`). Em erro de escrita local, `ImageSaver` tenta metadata interno como fallback. `CollectionFolder` não impõe uma política global de rejeição de symlink/junction; logo, reutilizar `BackupRestoreDirectory.OpenRoot` sem política por raiz pode recusar caminhos hoje válidos. O plugin `AvatarFileStore` tem implementação nativa Windows/Linux, mas valida propriedade/permissões próprias de um diretório privado e não é apropriado como helper genérico.

Decisão: não aplicar rejeição global de links nem política ACL de avatar a bibliotecas, cache e perfil. A implementação handle-relative precisa aceitar raiz configurada como autoridade de confiança, preservar as diferenças `CreateNew` (legendas) e substituição (imagem/letra), e avaliar fallback de imagem sem transformar erro de segurança em gravação alternativa. Não há evidência sobre uso de symlink/junction nos caminhos de produção. Sem alteração runtime ou crédito formal; coverage do portal permanece 328/397 e homologação formal 2/7.

### Reavaliação da exclusão de imagem nativa de perfil — 2026-11-01

Uma tentativa local conectou `ImageController` a `BackupRestoreDirectory` e exigiu `ValidateTrustedWriteBoundary`. O teste de exclusão bem-sucedida recebeu `ObjectResult` em vez de 204. O logger nulo não capturou a exceção exata; não atribuir falha a ACL sem observar a exceção. Como a política do helper pertence ao restore e a compatibilidade com raízes de perfil não está demonstrada, a chamada, `InternalsVisibleTo` e os testes específicos da tentativa foram retirados. Os testes existentes `ImageControllerDeleteUserImageTests` + `ImageControllerUserImageAuthorizationTests` passaram 20/20 após a reversão. Estado final: exclusões nativas continuam pathname-based, TOCTOU aberta. Próximo passo é desenhar e testar uma primitiva handle-relative compartilhada em camada adequada; esta sessão não alterou runtime, produção, release nem contagem formal.
## Mídia Storage Online: promoção de download preserva o arquivo final — 2026-11-01

`MidiaStorageOnlineSyncTask.DownloadMediaFileAsync` grava a resposta em um temporário irmão e agora o promove com `File.Move(tempPath, filePath, overwrite: true)`. Foi removida a exclusão explícita do arquivo final antes do move; se a promoção falhar, o arquivo existente não é removido primeiro. O endpoint equivalente já usava a sobrecarga com `overwrite: true`. Teste focal `MidiaStorageOnlineSyncTaskTests`: 9/9 em Windows/.NET 10 Release; o cenário de destino aberto exclusivamente é específico de Windows e agora é explicitamente ignorado em outras plataformas. Contrato do portal: 87/87. O evento desta sessão é o 3º de 10 do lote local, ainda sem publicação. `git diff --check` passou. CI hospedada não iniciou os jobs por bloqueio de cobrança; runtime/produção não foram alterados nem validados. Sem release ou crédito formal de homologação; formal permanece 2/7. Mapa conferido, sem alteração estrutural. Ver [[Sessões/2026-11-01-0713-MulletaFlix-midia-storage-download-promotion]].

## Mídia Storage Online: colisão e gravação concorrente de logos — 2026-11-01 07:24 BRT

`MidiaStorageOnlineLogoLibrary.BuildLogoFileName` agora incorpora 64 bits do SHA-256 da URL no nome local, mantendo a extensão e limitando o filename a 120 caracteres. As coleções de URL usam comparação ordinal para respeitar diferenças de caixa em paths. O downloader grava em temporário exclusivo e promove com `File.Move(..., overwrite: true)`, sem expor um arquivo parcial nem competir por `FileMode.Create` no destino. `MidiaStorageOnlineLogoLibraryTests`: 2/2; suíte `Jellyfin.Providers.Tests`: 867 aprovados, 13 ignorados por integrações externas, 0 falhas; contrato do portal 88/88. Teste de nomes não é HTTP end-to-end. Linux, produção e CI hospedada não validados; os 16 jobs do PR #1 não iniciaram por bloqueio de cobrança. Arquivos antigos sem hash ficam no cache e serão baixados novamente ao serem requisitados; limpeza de órfãos pendente. Homologação formal 2/7, sem crédito ou release. Mapa técnico conferido, sem mudança de módulos/APIs/dependências. Ver [[Sessões/2026-11-01-0724-MulletaFlix-logo-cache-collision]].

## Mídia Storage Online: confinamento das chaves do manifesto na limpeza — 2026-11-01 07:29 BRT

`MidiaStorageOnlineController` agora valida cada chave legada antes de compor o destino de exclusão. A validação rejeita caminhos absolutos, drive/UNC, componentes `.`/`..` nas sintaxes `/` e `\\`, e confirma que o caminho normalizado permanece sob a raiz STRM. Chaves inválidas são registradas e ignoradas; a gravação do manifesto novo continua removendo entradas que não pertencem à sincronização atual. `MidiaStorageOnlineManifestPathTests`: 13/13 em Windows/.NET 10 Release; suíte `Jellyfin.Providers.Tests`: 880 aprovados, 13 ignorados por integrações externas, 0 falhas (893 total); contrato local do portal 89/89. A validação é lexical: symlinks/junctions e TOCTOU permanecem fora deste fix; Linux, runtime instalado e produção não foram testados. Homologação formal 2/7, sem crédito ou release publicada. Mapa conferido; helper interno na mesma classe, sem novo módulo, API pública ou dependência. Ver [[Sessões/2026-11-01-0729-MulletaFlix-manifest-path-confinement]].
### 2026-11-01 07:42 BRT — escopo da auditoria de escritas por pathname confirmado

Auditoria estática ampliou a seção das gravações em `MediaBrowser.Providers`: `ImageSaver.SaveImageToLocation` é o ponto compartilhado dos três overloads de `ProviderManager.SaveImage`; `LyricManager` e `SubtitleManager` validam/confinam por pathname antes de abrir e ainda têm janela de troca concorrente. `LyricManager` usa `FileMode.Create`; `SubtitleManager` usa `CreateNew`, mas exclusão de legendas remove `stream.Path` diretamente. Foram rastreados também os dois escritores STRM/download do Mídia Storage Online (endpoint manual e tarefa agendada), além da limpeza por manifesto: a verificação existente é lexical e não ancora operações a um diretório aberto.

Não há exploit reproduzido e esta rodada não executou testes. O helper `BackupRestoreDirectory` não pode ser reutilizado diretamente: pertence à camada `Jellyfin.Server.Implementations` e impõe ACL de restore. `FileSystemHelper` só valida pathname e não fecha TOCTOU. Decisão: não adicionar outra checagem lexical nem aplicar ACL de restore a bibliotecas; a próxima correção deve introduzir I/O relativo a handle numa camada abaixo de Providers, preservar permissões atuais dos caminhos confiáveis e integrar os escritores compartilhados com testes de symlink/troca em Windows e Linux. Sem mudança de runtime, release ou produção; homologação formal 2/7. Mapa técnico conferido e estrutura inalterada.
## Nebula — limite da fila prioritária validado localmente — 2026-11-01

`queued_mongo_scanner` agora consulta `large_qsize()` imediatamente antes de cada seleção. Regressão RED confirmou que com cinco arquivos grandes na faixa, a lógica anterior selecionava dois itens pela consulta grande antes de alternar para a geral; outro teste cobre consumo concorrente durante confirmação Mongo. Focal 9/9, suíte Nebula Python 724/724, cobertura 93%. CI do PR #1 não iniciou os jobs por bloqueio de cobrança; Linux, MongoDB real, runtime instalado e produção não verificados. Candidata 12.1.46; sem release nem crédito de homologação formal (2/7).
## Auditoria de exclusão de imagens do ImageSaver — 2026-11-01 09:06 BRT

- **Achado reproduzido na lógica:** `ImageSaver` decide se um caminho está sob `InternalMetadataPath` usando `string.Contains`. Quando `saveLocally` é falso, para a raiz `C:\ProgramData\Mulletaflix\metadata` o caminho irmão `C:\ProgramData\Mulletaflix\metadata-backup\poster.jpg` também satisfaz o teste textual, embora esteja fora da raiz (`Path.GetRelativePath` começa com `..`). Com `saveLocally=true`, o `||` curto-circuita e essa chamada a `Contains` não decide a remoção. O único call site do predicado foi encontrado em `MediaBrowser.Providers/Manager/ImageSaver.cs:180`.
- **Estado/risco:** isso pode classificar incorretamente caminhos irmãos durante a limpeza da imagem anterior. Não foi executada exclusão real nem validado impacto em produção. Uma troca lexical isolada não resolve symlink/TOCTOU; manter correção runtime pendente até haver operação de arquivo ancorada por handle na raiz apropriada, sem reutilizar a política específica de restore.
- **Validação:** demonstração local do predicado e do caminho relativo; nenhuma alteração runtime, teste de servidor, release ou homologação formal. Gate formal continua 2/7.

### Correção da exclusão da imagem anterior — 2026-11-01

`ImageSaver` deixou de decidir remoção por `string.Contains` e passou a chamar `MediaBrowser.Controller.IO.RootedFileSystem.TryDeleteFile` sob raízes de escrita permitidas. No Windows, a primitiva ancora a operação em handle da raiz, mantém todos os diretórios ancestrais abertos sem compartilhamento de exclusão até o término e não segue reparse points descendentes. Linux/macOS lançam `PlatformNotSupportedException`, capturada no caller e registrada como aviso; a imagem anterior permanece até haver primitiva validada contra renomeação concorrente. Caso sem contenção ou alvo ausente também preserva o arquivo. A limpeza de diretórios vazios de episódios foi removida porque ainda usaria pathname.

Verificado em Windows/.NET 10 Release: `RootedFileSystemTests` 4 aprovados, 1 ignorado; foram adicionados testes diretos do caller em `ImageSaverTests`: 2 executados e 1 ignorado (cenário não-Windows); suíte `Jellyfin.Providers.Tests` 902 aprovados, 14 ignorados, 0 falhas/916. Os testes diretos confirmam que a imagem sob a raiz é removida após salvar a substituta e a pasta irmã `metadata-backup` permanece intacta. Sem execução Linux/macOS do caller, runtime instalado, mídia/produção ou CI hospedada; a limpeza antiga continua desabilitada fora do Windows. Falhas de exclusão Windows por compartilhamento/ACL são registradas, sem reverter nova referência e sem rotina de reconciliação. A escrita da imagem segue pathname-based. Sem release publicada nem crédito formal (2/7). O mapa técnico foi conferido e não houve mudança estrutural.
## TMDb — idioma regional real das imagens — 2026-11-01

- **Achado:** o fork usava `Iso_639_1` sozinho e promovia `pt` para a região pedida (`pt-BR`), apagando a região real `Iso_3166_1`. Isso permite confundir arte portuguesa com brasileira e também impacta outras variantes regionais.
- **Correção:** `TmdbClientManager` passa idioma e região ao novo `TmdbUtils.GetImageLanguage`, portado do upstream Jellyfin PR #18150. Região divergente é conservada; região correspondente usa o locale pedido; consulta sem região mantém idioma genérico. O fluxo central atende imagens de filmes, séries, temporadas, episódios e coleções, incluindo animações tratadas como `Series`.
- **Verificação:** `TmdbUtilsTests` 32/32; ordenação `Jellyfin.Model.Tests` 8/8; `Jellyfin.Providers.Tests` 907 aprovados, 14 ignorados e 0 falhas/921, todos em Windows/.NET 10 Release.
- **Limites:** sem chamada à API TMDb, refresh de arte persistida, Linux/runtime instalado ou produção; providers externos como Fanart/TVDB não usam esse conversor. Homologação formal 2/7; sem release/crédito.


## Revalidação local TMDb e CI PR #1 — 2026-11-01 10:17 BRT

A suíte `Jellyfin.Providers.Tests` passou 907, ignorou 14 e falhou 0/921; `Jellyfin.Model.Tests` passou 699/699 em Windows/.NET 10 Release. O run mais recente do PR #1 (`41283703952`, HEAD `cbc380c94e8c88278c44309f586faf9c0e1c134f`) bloqueou todos os 16 jobs antes de iniciar por problema de cobrança do GitHub; logs não foram gerados. Isso não demonstra falha de código. Homologação formal permanece 2/7; 12.1.46 continua candidata sem publicação. TMDb ao vivo, imagens já persistidas, Linux e produção permanecem pendentes.
## Mídia Storage Online: inventário dos writers de filesystem — 2026-11-01 10:48 BRT

Reauditoria estática dos dois caminhos de sincronização (`MidiaStorageOnlineSyncTask` agendada e `MidiaStorageOnlineController` manual) confirmou estes acessos por pathname sob a raiz STRM ou diretório de dados configurado:

- STRM de filmes e episódios usa `File.WriteAllTextAsync` diretamente; downloads usam temporário irmão e `File.Move(..., overwrite: true)`. Na tarefa agendada o temporário fixo usa `FileMode.Create`; no controller ocorre o mesmo. A promoção preserva o destino se falhar, mas não ancora a árvore de diretórios.
- Os dois writers de `sync_manifest.json` agora usam temporário irmão exclusivo e promoção por rename; cache `guides.json` e M3U de canais continuam em escrita direta. Os locks do manifesto são distintos e ainda permitem snapshots concorrentes perdidos.
- XMLTV cria `.new` e tenta `File.Replace`/`File.Move`, mas há fallback de `File.Copy(..., overwrite: true)` que volta à escrita por pathname.
- Migração de STRM legado move/apaga por pathname. Limpeza manual usa `TryGetConfinedManifestPath` para bloquear traversal lexical, depois `File.Delete`, enumeração e remoção de diretórios por pathname. Isso não resolve symlinks/junctions nem corrida entre validação e uso.
- `MidiaStorageOnlineLogoLibrary` é melhor isolado: temporário aleatório exclusivo (`CreateNew`) e promoção por move com overwrite. Ainda não prova confinamento handle-relative da raiz.

`RootedFileSystem.TryDeleteFile` existente não fecha essa lacuna: fornece apenas exclusão, e lança `PlatformNotSupportedException` fora do Windows. Não pode ser adotado para writes nem como solução multiplataforma. A implementação interna de restore também não é compartilhável diretamente; sua validação ACL atende outro domínio.

É inventário estático, não exploit reproduzido. Os testes históricos `MidiaStorageOnlineSyncTaskTests` 8/8 não cobrem raiz com link/troca concorrente nem endpoint manual; nenhum teste de código foi executado nesta sessão. Próxima ação: primitiva handle-relative abaixo de Providers, Windows e Linux, com criação, substituição, remoção e regressões adversariais para ambos os fluxos antes de integrar. Não atribuir crédito formal nem release; gates formais seguem 2/7.

### Revisão independente do helper existente — 2026-11-01 10:50 BRT

A revisão Cavecrew confirmou que `RootedFileSystem.TryDeleteFile` não é chamado pelo Mídia Storage Online; o único caller de produção encontrado é `ImageSaver`. No Windows, a deleção abre raiz e descendentes relativamente a handles com share mask `0x3`, sem `FILE_SHARE_DELETE`, bloqueando renames desses diretórios enquanto os handles ficam abertos. Fora do Windows, lança `PlatformNotSupportedException`. Portanto, a lacuna do plugin continua sendo escrita/alterações por pathname; não se deve descrever o helper como pathname-only para deleção Windows. Os testes de `RootedFileSystem` verificam traversal, links e fail-closed fora do Windows, mas não foram executados nesta sessão e não cobrem writers do plugin.

### Persistência atômica do manifesto Mídia Storage Online — 2026-11-01 11:01 BRT

Os dois `SaveManifest` (tarefa agendada e endpoint manual) agora gravam JSON em um temporário exclusivo no mesmo diretório e promovem com `File.Move(..., overwrite: true)`. Uma falha durante gravação ou substituição não trunca primeiro o manifesto anterior. Teste focal: 3/3; filtro Mídia Storage Online: 40/40; suíte `Jellyfin.Providers.Tests` em Windows/.NET 10 Release: 912 aprovados, 14 ignorados, 0 falhas/926. A revisão Cavecrew confirmou o limite de concorrência.

Essa alteração não é confinamento handle-relative: caminhos de diretório continuam por pathname, temporários podem permanecer se a limpeza falhar, e atomicidade/durabilidade em filesystems de rede ou após queda de energia não foi verificada. Os locks continuam separados e cobrem apenas Load/Save; execuções concorrentes ainda podem sobrescrever snapshots e perder entradas. Outros escritores STRM/M3U/XMLTV seguem na auditoria. Não há crédito formal, mudança de release ou validação Linux/produção.

### Mídia Storage Online: serialização por manifesto — 2026-11-01 12:32 BRT

O endpoint manual e a tarefa agendada agora adquirem o mesmo lease assíncrono identificado pelo caminho canonicalizado de `sync_manifest.json` antes de iniciar a sincronização completa; o lease cobre leitura, materialização/limpeza dos arquivos e gravação do manifesto. Bibliotecas com manifestos diferentes continuam independentes. Espera cancelável limitada a 15 minutos; o caller em espera recebe timeout sem cancelar o dono do lease. Os locks locais de Load/Save permanecem dentro das rotinas.

Windows/.NET 10 Release: `MidiaStorageOnlineSyncGateTests` 4/4; filtro `Plugins.MidiaStorageOnline` 46/46. Testes exercitam os delegates dos dois tipos de caller usando caminhos equivalentes, isolamento de manifests distintos, cancelamento da espera e liberação após exceção. Não validam sync ponta a ponta contra mídia real. Coordenação é intra-processo; múltiplas instâncias, Linux, filesystem de rede, runtime instalado e produção não foram verificados. Homologação formal 2/7; sem crédito formal ou release. CI hospedada do PR #1 continua bloqueada antes dos runners por billing.

## ImageSaver: escritores por pathname seguem sem proteção contra troca concorrente — 2026-11-01 11:18 BRT

Call graph revisado: destinos de item e retries convergem em `ImageSaver.SaveImageToLocation`; o overload `ProviderManager.SaveImage(Stream, mimeType, path)` chega ao mesmo writer e, no checkout, é usado por `AvatarService` para imagem de perfil. Todos ainda usam operações por pathname. `RootedFileSystem.TryDeleteFile` não participa das gravações.

Testes focais Release no host Windows passaram (`ImageSaverTests`: 4 aprovados/1 ignorado; `RootedFileSystemTests`: 4/1), mas não cobrem symlink ou troca concorrente durante escrita. Nenhuma mudança de comportamento nem crédito de homologação. Próximo passo: projetar gravação handle-relative que preserve substituição atômica e permissões atuais, testar Windows/Linux, depois migrar ImageSaver e AvatarService.

## Comparação do fluxo de imagens com Jellyfin — 2026-11-01

A auditoria de `ProviderManager`/`ItemImageProvider` upstream confirma a separação entre descoberta, ordenação por `RemoteImageInfo.Language` e salvamento da primeira imagem elegível. No fork, o controller remoto preserva a ordem já calculada pelo servidor; os providers TMDb language-aware também solicitam locale/região e fallback. A revalidação local Windows/.NET 10 Release passou: ProviderManager 3/3, ItemImageProvider locale/fallback 26/26, TMDb utils 32/32 e controller 8/8. O legado `MetadataService` carrega URL/tipo sem proveniência linguística, e imagens singulares já existentes não são trocadas automaticamente. Sem runtime, produção, providers reais ou renderização; homologação formal inalterada. Detalhes e fontes: `image-metadata-flow-audit.md` e sessão [[Sessões/2026-11-01-MulletaFlix-image-language-jellyfin-comparison-1208]].

### Proveniência ausente no contrato legado — 2026-11-01 12:18 BRT

O caminho `MetadataService.SaveRemoteResultImages` recebe `MetadataResult.RemoteImages` como pares URL/tipo. NFO e OpenLibrary produzem esses pares; OpenLibrary os converte em `RemoteImageInfo` sem idioma. `ResultLanguage` se refere aos metadados textuais e não prova o idioma da capa. Não alterar o contrato público ou atribuir esse valor à imagem sem sinal por arte na fonte. Testes locais focados: Model 21/21 e Providers 23/23 em Windows/.NET 10 Release. Próximo passo: só estender o fluxo legado quando uma fonte fornecer idioma por imagem; homologação formal 2/7, sem crédito/release.
