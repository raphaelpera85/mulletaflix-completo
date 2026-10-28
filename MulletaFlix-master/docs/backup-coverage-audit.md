## Restore ZIP — limites de expansão e conteúdo — 2026-10-28

`RestoreBackupCoreAsync` também usa o mesmo leitor limitado a 16 MiB da consulta/listagem; a leitura acumulada aplica o limite mesmo se o tamanho anunciado no diretório ZIP não for confiável. Manifesto acima do teto falha com `InvalidDataException` antes de criar o journal de restore, criar sidecars de arquivos ou mutar config/mídia/banco, preservando arquivos existentes; o teste confirma também que o callback de sidecar não é acionado. O teste usa um `BackupManifest` válido com padding incompressível acima do limite. Uma tentativa preliminar anterior ao fix usou JSON inválido e alcançou erro de desserialização, não sendo tratada como a prova do caso válido. A validação geral da integridade ainda percorre o ZIP antes deste limite específico; o trabalho de descompressão permanece sujeito aos tetos gerais por entrada/arquivo e razão de compressão.

`GetUniqueManifestEntry` agora é compartilhado por restore, consulta e enumeração. Ele rejeita múltiplos `manifest.json` e aliases de caixa conflitantes em qualquer SO, antes de ler o conteúdo; o nome é reservado ao manifesto e a comparação sem caixa mantém consulta/listagem coerentes com a validação de raiz do restore no Windows e Linux. A regressão confirma que consulta retorna `null`, enumeração omite o ZIP ambíguo e restore lança `InvalidDataException` sem alterar o arquivo nem criar sidecars. Testa duplicado exato e alias de caixa; a execução local foi Windows, portanto o runtime Linux ainda precisa de CI.

`BackupService.ValidateArchiveIntegrityAsync` agora rejeita arquivo compactado acima de 257 GiB, diretório central acima de 32 MiB, mais de 100.000 entradas, entrada expandida acima de 64 GiB, total expandido acima de 256 GiB ou razão de compressão acima de 1.000:1. Antes de construir `ZipArchive`, um preflight lê o rodapé e percorre as entradas do diretório central sem materializar objetos; durante a leitura descompactada, o CRC usa contagem dos bytes lidos no stream ZIP e um limite dinâmico por blocos de 4 KiB. Isso limita expansão mesmo quando tamanho compactado/expandido anunciado no ZIP é adulterado. O limite dinâmico pode detectar excesso depois que até um bloco de 4 KiB foi produzido. `GetManifest`, usado por consulta e listagem, aplica os limites do arquivo/diretório central antes de construir `ZipArchive` e limita a cópia descompactada do manifesto a 16 MiB. A validação ocorre antes das mutações do restore. A assinatura CLR preexistente de `Crc32.ComputeAsync(Stream, CancellationToken)` foi preservada; o helper limitado é um método separado.

Em Windows/.NET 10 Release, o teste focal de manifesto excedente passou 1/1, o de manifesto duplicado/alias passou 2/2 e `FullyQualifiedName~FullSystemBackup` passou 150, ignorou 16 casos Linux/macOS/mount, falhou 0 (166). Os testes novos exercitam limite de bytes compactados e diretório central, contagem prévia de entradas, ZIP64 válido, teto por entrada, orçamento total expandido, razão de compressão e expansão acima do limite ao adulterar individualmente tamanho compacto e expandido do diretório ZIP; `GetBackupManifest`/`EnumerateBackups` e `RestoreBackupAsync` rejeitam manifestos excedentes ou ambíguos. `Jellyfin.Common.Tests` passou 8/8 casos `Crc32Tests`, incluindo leitura limitada.

Os limites podem recusar backups legítimos acima dos tetos indicados; não foi medido o maior backup de produção. O preflight não suporta/validou arquivos com prefixo SFX ou registro opcional de assinatura digital no diretório central. Não houve execução Linux/CI hospedada, runtime instalado nem restore em produção. Homologação formal permanece 2/7 (28,6%); esta mudança não conclui gate nem cria release.

## Reauditoria estática das operações do restore — 2026-10-28 11:34 BRT

No commit `1965ea30`, `FileSystemRestoreTransaction.ApplyEntryAsync`, rollback, limpeza de sidecars e remoção de diretórios criados executam mutações por `BackupRestoreDirectory` e nomes relativos a handles. A descoberta/abertura dos diretórios ainda usa paths para `Directory.Exists`, `Path.GetFullPath` e `BackupRestoreDirectory.OpenRoot`; a revisão estática não prova ausência de corrida do sistema operacional. Não foi identificada correção de código justificável sem reproduzir um bypass concreto.

`FullyQualifiedName~FullSystemBackup` recompilou em Windows/.NET 10 Release e passou 150, ignorou 16 casos Linux/macOS/mount e falhou 0 (166). WSL2 não inicia porque virtualização de hardware está desativada nesta máquina; Docker e Podman não estão instalados. Assim, syscalls/ACL POSIX, corrida concorrente Linux, mount cases e execução CI hospedada permanecem não verificados. Formal continua 2/7 (28,6%), sem crédito/release. O próximo gate é obter um runner Linux observável e executar o conjunto privilegiado atual, não inferir segurança Linux da suíte Windows.

## CI da branch de restore — 2026-10-28 08:58 BRT

Auditoria de `.github/workflows/ci.yml`: `push` está configurado somente para `main` e `master`; branches de trabalho dependem de `pull_request`. O endpoint público de Actions não retornou execução para o commit `80609ab92d585eb248376f2fe32ffd04fa29dfe9`, e o status combinado não listou checks. A tentativa de abrir PR draft pela integração GitHub foi recusada com HTTP 403 `Resource not accessible by integration`; nenhum PR foi criado. Isso explica a ausência de CI nessa branch, sem indicar falha do código nem sucesso dos testes hospedados. Localmente, a validação anterior segue 147/163 `FullSystemBackup`, 8/8 `Crc32Tests` e 1/1 teste focal IntroSkipper em Windows/.NET 10 Release. Linux/CI hospedada, runtime instalado e produção não foram validados. Homologação formal 2/7 (28,6%), sem crédito ou release.

## Revalidação do restore na branch isolada — 2026-10-27 23:27 BRT

Branch `codex/backup-restore-handle-relative-io`, baseada no `origin/main` então atual `8a7796c3`. A suíte `FullyQualifiedName~FullSystemBackup` em Windows/.NET 10 Release terminou com 141 aprovados, 16 ignorados por dependência de Linux/macOS/mounts e 0 falhas (157). Os dois testes de serialização da imagem e nomes de tabelas que haviam falhado na primeira compilação passaram após sincronizar as entidades/contexto do banco. `git diff --cached --check` passou antes da última sincronização; reexecutar antes do commit.

Esta execução não valida ACL/syscalls POSIX, corrida Linux, CI hospedada, runtime instalado ou produção. ZIP continua sem limite explícito de entradas e bytes expandidos/razão de compressão; manter como risco aberto. H03 e formal global permanecem 2/7 (28,6%), sem crédito ou release. A versão da branch foi alinhada a `12.1.41` para não regredir em relação a `origin/main`; não é uma versão de release.

## Restore — revalidação da política padrão de ACL — 2026-10-27 08:41 BRT

A resposta do usuário confirmou a política padrão já codificada: identidade efetiva do serviço e root/admin são confiáveis; escrita de grupos/outros, named users não autorizados e filesystem compartilhado/desconhecido são recusados. No Windows a validação usa owner/DACL e limita a NTFS/ReFS. `BackupRestoreDirectoryTests` em Windows/.NET 10 Release passou 43/54, com 11 skips detalhados exclusivamente Linux (ACL POSIX nomeada/default, symlinks, rename, bind mount e handles ancestrais); 0 falhas. O contrato `ci-test-runner-serialization.test.mjs` passou 2/2 e confirma que os dois testes ACL Linux (named user e other-write) estão no job Ubuntu privilegiado, cujo parser exige `Passed` nos 14 casos esperados. Isso valida somente o contrato estático do workflow, não execução CI/Linux. Linux nativo/CI atual, runtime e produção seguem pendentes. Homologação formal 2/7 (28,6%), sem crédito ou release.

## Restore — reexecução após morte do processo — 2026-10-26 23:18 BRT

`dotnet test MulletaFlix-master/tests/Jellyfin.Server.Implementations.Tests/Jellyfin.Server.Implementations.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~BackupServiceTests.RestoreBackupAsync_RecoversAfterRestoreProcessIsTerminated` passou 1/1 em Windows/.NET 10 Release. O teste existente cria ZIP com manifest e payload de 64 MiB, encerra abruptamente o processo filho após criar staging, confirma journal/temporário, inicia outro processo para `RecoverInterruptedRestoreAsync`, compara todo o payload restaurado e verifica limpeza do journal/sidecars. Isto prova replay real em processo filho nesse cenário Windows. Não valida ACL POSIX, inicialização integral por `Program` nem integração HTTP no mesmo ciclo. Homologação formal segue 2/7 (28,6%), sem crédito/release. Ver evento portal `server-restore-process-crash-replay-2026-10-26-2318` e sessão `2026-10-26-MulletaFlix-restore-process-crash-replay-2318`.
## Restore — rota HTTP, agendador e shutdown do TestServer — 2026-10-26 23:12 BRT

`BackupControllerAuthorizationHttpTests` passou 2/2 em Release com `BackupService` concreto. Via `POST /Backup/Restore`, autenticação Administrator, verificou 204, caminho sanitizado, `ShouldRestart`, notificação ao host e encerramento do host `TestServer` via `IHostApplicationLifetime` real. `ProgramStartupRecoveryTests` passou 8/8 separadamente e prova recovery→initialize pelo probe subprocessual. O HTTP usa um arquivo de texto dummy: não valida ZIP, replay, restart de processo nem reabertura do servidor real. Avisos do build são preexistentes em outros arquivos do projeto de testes. Homologação formal 2/7 (28,6%), sem crédito/release. Ver evento portal `server-restore-http-entrypoint-test-2026-10-26-2312` e sessão `2026-10-26-MulletaFlix-restore-http-startup-recovery-test-2312`.

## Gate privilegiado Linux restore — contrato de seleção — 2026-10-26 23:08 BRT

`node --test tools/release/tests/ci-test-runner-serialization.test.mjs` passou 2/2. O contrato verifica os 14 nomes únicos do filtro do workflow contra a lista esperada do TRX e confirma que os 14 métodos estão declarados como xUnit `[Fact]`/`[Theory]` em `BackupRestoreDirectoryTests.cs`/`BackupServiceTests.cs`. Isto valida a ligação estática, não a execução Linux. WSL não tem distribuição; Docker e Podman não estão disponíveis. O Actions público mais recente observado (CI `39608614450`, commit `d51f4f38d2b4148ab49394c001567ad273bd57aa`) falhou e não expôs passos/logs do job servidor; portanto não é possível concluir que o gate correu. O checkout local `64a33dcf5595fb6f429c65206be20dbdd4ae0bd0` está oito commits atrás e a mudança local não está no CI remoto. Também falta teste integrado HTTP→agendamento/restart→recuperação no startup. Homologação formal 2/7 (28,6%), sem crédito/release.
# Auditoria de cobertura do backup integral

## Lacuna de integração do restore no startup — 2026-10-27

Auditoria inicial: rota HTTP/agendamento, replay após morte e gate eram exercitados separadamente. `Program` usa `RunAfterBackupRecoveryAsync` e `ContinueAfterBackupRecoveryAsync`; teste integrado cria archive/journal ativo, chama ambos helpers compartilhados pelo entry point e verifica payload/journal antes da continuação; Windows/.NET 10 Release 1/1. `ProgramStartupRecoveryTests` 9/9; replay após morte 1/1. Limite: teste não inicia host real nem executa migrations/`InitializeServices`. Linux/ACL operacional, CI hospedada, runtime e produção não validados; formal permanece 2/7 (28,6%), sem crédito.

## Reexecução focal Windows — 2026-10-26 22:52 BRT

O anexo de deadlock reenviado tem o mesmo SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`; não acrescenta PID, versão do assembly nem desfecho das retentativas. `FullSystemBackup` passou 136/152 testes em Windows/.NET 10 Release, com 16 ignorados por plataforma (Linux/macOS) e zero falhas. WSL está disponível como componente do Windows, mas nenhuma distribuição está instalada. Esse resultado não executa os caminhos POSIX e não fecha o gate P0. Homologação formal 2/7 (28,6%), sem crédito/release. Ver evento portal `server-restore-windows-focused-rerun-2026-10-26-2252` e sessão `2026-10-26-MulletaFlix-restore-windows-focused-rerun-2252`.

O contrato do portal passou 318/318; parsing JSON, unicidade de IDs e `git diff --check` passaram. `git diff --check` emitiu avisos de normalização CRLF/LF, sem erro.

## Reenvio idêntico do deadlock; servidor não está ativo — 2026-10-26 20:50 BRT

O anexo tem o mesmo SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2` do incidente de 23/10. Repete MariaDB 1213 em exclusão concorrente de `BaseItemImageInfos`; o trecho mostra retries, mas não o resultado final. A inspeção local de processos e serviços não encontrou processo MulletaFlix/Jellyfin ativo; o banco não foi consultado e nenhum serviço foi iniciado ou reiniciado. Sem PID/assembly no log, não há correlação entre a falha histórica e o binário instalado ou fonte atual. Auditoria 5.465/5.493, homologação formal 2/7 (28,6%), sem crédito/release. Ver evento portal `server-image-deadlock-repeat-no-process-2026-10-26-2050`.

## Reenvio idêntico do deadlock de capas — 2026-10-26 13:49 BRT

O anexo tem SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, igual ao registro anterior: 11 deadlocks MariaDB 1213 em `DELETE BaseItemImageInfos` e 12 avisos de retry, sem desfecho, PID ou versão. A fonte de trabalho contém locks por stripe e advisory `GET_LOCK`; a regressão MariaDB descartável já passou 19/19 em execução anterior. Inspeção local anterior encontrou DLL instalada 12.1.35 sem o marcador e checkout 12.1.32 com a implementação, mas não correlacionou o processo do log. Nenhuma nova execução do banco/runtime ocorreu nesta revalidação. Formal permanece 2/7 (28,6%), sem crédito/release.

## Revalidação do gate de recuperação na inicialização — 2026-10-26 13:46 BRT

`ProgramStartupRecoveryTests` em Windows/.NET 10 Release passou 8/8, sem skips
ou falhas; TRX ficou em TEMP. A compilação emitiu avisos XML cref/analyzer não
relacionados ao foco. O teste cobre o helper por subprocesso e o encerramento
por timeout, mas não inicia `StartApp`, não chama a recuperação real do
`BackupService` e não valida banco/runtime/produção. Homologação formal permanece
2/7 (28,6%), sem crédito ou release. Ver
[[Sessões/2026-10-26-MulletaFlix-startup-recovery-revalidation-1346]].

## Gate privilegiado de restore Linux — 2026-10-26 13:27 BRT

O workflow raiz `../.github/workflows/ci.yml` agora repete dez casos de
fronteira do restore em Ubuntu sob `sudo`, após a compilação/testes normais. A
etapa salva TRX fora do checkout e exige que todos os dez métodos estejam
presentes com resultado `Passed`; `Skipped`, caso ausente ou falha encerra o job
como erro. A seleção cobre mounts/bind aliases, ACL POSIX, operações relativas
a handles, symlink, remoção relativa e proteção contra restore/recovery em mount
aninhado. Isto fecha a lacuna de observabilidade do CI, não a validação Linux:
WSL não tem distribuição instalada neste host e a nova etapa ainda não executou
remotamente. Homologação formal permanece 2/7 (28,6%), sem crédito ou release.

## Revalidação do erro de cursor Supabase — 2026-10-26 11:58 BRT

`NebulaSupabaseSyncTests` Release/.NET 10 passou 65/65 em Windows. A suíte
verifica retry de resposta HTTP 5xx do cursor, `Retry-After`, recusa de fallback
integral quando o histórico não pode ser lido, timeout/backoff do ciclo
automático e propagação de falha sem registrar sucesso vazio. Os testes usam
handlers HTTP locais e doubles; não acessam Supabase, Mongo operacional nem
runtime instalado. O erro separado de conexão recusada em `localhost:27017`
indica que o Mongo não aceitou conexão naquele instante, mas este teste não
determina se o serviço Mongo estava parado, indisponível ou mal configurado.
Homologação formal permanece 2/7 (28,6%), sem crédito ou release.

## Regressão em Windows reexecutada — 2026-10-26 10:16 BRT

`FullyQualifiedName~FullSystemBackup` foi reconstruído e executado em Windows/.NET 10 Release: 133 aprovados, 14 ignorados, 0 falhas/147. A compilação terminou e emitiu avisos XML/analisadores sem erro. Os skips incluem symlink e ACL POSIX, operações Linux relativas a handle, bind mounts e journal dependente do filesystem. Esta execução confirma apenas os casos aplicáveis no Windows; não fecha TOCTOU/mount namespace, crash real nem restore Linux. Homologação formal continua 2/7 (28,6%), sem crédito ou release. Ver [[Sessões/2026-10-26-MulletaFlix-restore-fullsystembackup-rerun-1016]].

## Regressão atual em Windows — 2026-10-26 07:28 BRT

`FullyQualifiedName~FullSystemBackup` em Release passou 133/147, com 14 casos
ignorados e zero falhas. A verificação direcionada de estilo C# também passou.
Os skips dependem de Linux/macOS: symlink/POSIX ACL, handles relativos, bind
mount e journal no filesystem. Portanto, esta rodada não fecha os gates de
TOCTOU/mount namespace, crash real ou restore Linux. Homologação formal continua
2/7 (28,6%), sem crédito ou release.

## Revalidação dos gates de restore em Windows — 2026-10-26 06:33 BRT

`FullyQualifiedName~FullSystemBackup` foi reexecutado em Windows/.NET 10 Release:
133 aprovados, 14 ignorados, 0 falhas/147. O resultado confirma as regressões
compatíveis com Windows, mas não valida o teste de bind mount aninhado, ACL POSIX,
syscalls Linux ou estabilidade concorrente de `/proc/self/mountinfo`. WSL e runner
Linux observável não estão disponíveis nesta sessão. Homologação formal segue
2/7 (28,6%), sem crédito ou release.

## Reanálise do log repetido de deadlock — 2026-10-26 06:30 BRT

O anexo tem SHA-256 idêntico ao incidente anterior. Esta cópia contém 11 deadlocks
1213 e 12 avisos de retry para o mesmo `ItemId`, além de alerta do heartbeat Kestrel
por 2,35 s. O texto termina durante uma stack trace: não mostra resultado final da
tentativa 3, PID, assembly ou versão carregada. O alerta de heartbeat não prova que
os retries causaram starvation.

Os testes unitários focais de lock por item, retry transitório e falha permanente
passaram 4/4 em Windows/.NET 10 Release. MariaDB descartável, Docker/Podman e WSL
não estão disponíveis nesta sessão; portanto o novo teste de concorrência InnoDB
permanece sem execução. Homologação formal 2/7 (28,6%), sem crédito ou release.

## Alias de ProgramData através de mount aninhado — 2026-10-26 04:42 BRT

`BackupRestoreDirectory.IsPhysicalAncestorOf` agora verifica também mounts
visíveis abaixo da raiz de restore em `/proc/self/mountinfo`. Para cada mount
aninhado, compara dispositivo e coordenadas físicas (`root`) com o diretório
ProgramData aberto; assim detecta um bind mount de origem em filesystem distinto.
O teste de serviço cria `tmpfs` dentro da raiz, bind-mounta ProgramData a partir
dele, confirma IDs de mount diferentes e exige rejeição antes do lease/journal.

Build e teste focal compilaram em Windows/.NET 10; o caso Linux foi ignorado
(0 aprovados, 1 ignorado). `FullyQualifiedName~FullSystemBackup`: 133 aprovados,
14 ignorados, 0 falhas/147. Execução Linux com privilégios de mount, CI
observável, estabilidade concorrente do namespace, runtime instalado, produção
e gates formais permanecem pendentes. Homologação formal 2/7 (28,6%), sem
crédito ou release.

## ACL dos ancestrais e alias físico do journal — 2026-10-26 03:37 BRT

`BackupRestoreDirectory.OpenRoot` já valida cada pai da raiz por handle, mas não havia teste que colocasse a raiz configurada sob um ancestral diretamente gravável por terceiros. O teste `OpenRoot_RejectsUntrustedWritableAncestor` foi adicionado e passou 1/1 focal em Windows/.NET 10 Release; `FullSystemBackup` passou 128, ignorou 11 e falhou 0/139. O caso Linux está no teste, mas foi ignorado neste host.

Revisão estática encontrou que `BackupService.EnsureRecoveryJournalIsOutsideRestoreRoots` compara apenas caminhos normalizados. Um bind mount pode dar caminhos distintos à mesma árvore e permitir que arquivo de restore coincida com o journal ativo. O backend Linux também faz verificação de tipo por nome antes de `renameat`/`unlinkat`; isso pode agir sobre entrada substituída dentro do diretório aberto se houver escritor concorrente. Não houve reprodução nem execução Linux. Próximo passo: comparar identidade e ancestralidade físicas de diretórios abertos, com teste adversarial em Linux, antes de afirmar o gate P0 fechado.

Homologação formal permanece 2/7 (28,6%), sem crédito ou release. Nenhum banco, runtime instalado ou produção foi acessado.

Registro de homologação local em `portal-site/homologacao-status.json`; contrato passou 243/243, formatter direcionado passou e JSON foi parseado. O diff geral só emitiu avisos preexistentes de normalização LF/CRLF.

## Preflight de entradas ZIP no restore — 2026-10-26 02:35 BRT

`BackupService.RestoreBackupCoreAsync` agora valida o conjunto completo de entradas antes da validação de conteúdo MariaDB, da criação do journal e de mutações de arquivos. A verificação rejeita caminhos vazios/absolutos, componentes `.`/`..`, drive/ADS, nomes duplicados segundo a semântica do filesystem, raízes não suportadas e tipos Unix que não sejam arquivo regular/diretório coerente com o nome ZIP. A comparação de caminhos e raízes respeita a semântica case-insensitive do Windows; no Linux mantém caixa sensível. O teste preserva um arquivo sentinela e confirma ausência do journal após rejeição.

`BackupServiceTests` passou 82, ignorou 3 testes específicos de plataforma/macOS e falhou 0/85 em Windows/.NET 10 Release. Seis casos verificam traversal com `/` e `\\`, caminho absoluto, caminho com drive, caminho duplicado e symlink ZIP; seis entradas verificam nomes Windows reservados (`NUL`, `COM¹`/`COM²`/`COM³`), ponto final e espaço final. Nomes específicos do Windows são recusados nesse host e permanecem aceitos segundo a branch Linux do teste. Isso prova o preflight local no Windows; Linux nativo, CI hospedada do checkout atual, runtime instalado, produção e crash/replay continuam pendentes. O gate de restore permanece aberto; homologação formal 2/7 (28,6%), sem crédito ou release.

Auditoria estática de `.github/workflows/ci.yml` confirmou que `server-build-test` roda em `ubuntu-latest` e executa `dotnet test MulletaFlix.sln --filter "FullyQualifiedName!~Integration"`; isso seleciona esta suíte de unidade. O host Windows não tem WSL, Docker ou Podman, e o checkout ainda não foi enviado, portanto não há execução Linux observável da revisão atual.

## Semântica da corrida de entrada final no restore Linux — 2026-10-26 01:12 BRT

A leitura de BackupRestoreDirectory.Rename e DeleteEntry confirma uma checagem de tipo seguida por renameat/unlinkat relativo a descritores de diretório já abertos. As man-pages do Linux especificam que renameat renomeia a própria entrada se ela for um symlink e que unlinkat remove o link; nenhuma das operações segue o destino do symlink final. Portanto, a corrida de symlink final não demonstra traversal para fora dos diretórios ancorados nem exclusão do destino apontado.

A checagem de tipo ainda pode perder uma corrida com um escritor que altere a entrada entre statx e a syscall. Isso pode fazer o restore mover/remover uma entrada diferente da verificada e comprometer a integridade ou causar falha de restore. Pela política ACL aplicada às pastas, os escritores permitidos são o serviço e root; não foi demonstrado ator não confiável capaz de explorar essa corrida. Reclassificar este caso específico como risco condicional de integridade/robustez, não como bypass P0 comprovado.

Fontes primárias: [Linux man-pages: rename(2)](https://man7.org/linux/man-pages/man2/rename.2.html) e [Linux man-pages: unlink(2)/unlinkat(2)](https://man7.org/linux/man-pages/man2/unlink.2.html). Revisão estática; teste determinístico e execução Linux nativa continuam pendentes. O gate global de restore permanece aberto por validação Linux/ACL/filesystem, replay/crash e CI observável; não atribuir o status P0 global somente a esta corrida.

## Reauditoria do rollback de falha relacional — 2026-10-26 00:02 BRT

A anotação histórica de 22/10 sobre `DbContext.Add` ignorado foi superada pela fonte atual: o erro é relançado como `InvalidDataException`; purge, preparação e `SaveChangesAsync` ficam dentro da transação do contexto central. O fluxo normal de falha reverte a transação e os arquivos instalados. Não inferir rollback em banco real a partir desta leitura.

`FullyQualifiedName~FullSystemBackup` passou 115, ignorou 11 testes Linux/macOS e falhou 0/126 em Windows/.NET 10 Release. Os testes MariaDB opt-in e o cenário de crash no limite entre commit relacional/arquivos não foram executados. Linux nativo, ACL/filesystem efetivos, runtime instalado, produção e CI observável seguem pendentes. Gate P0 aberto; formal 2/7 (28,6%), sem crédito ou release.

## Revalidação da rejeição de OverlayFS — 2026-10-25 21:00 BRT

A matriz focal `BackupRestoreDirectoryTests.IsLocalLinuxFilesystem` + `IsTrustedWindowsFilesystem` foi reexecutada em Windows/.NET 10 Release e passou 6/6: ext4 e NTFS/ReFS aceitos; OverlayFS e nomes não confiáveis rejeitados. Isso confirma apenas a classificação dos tipos de filesystem. A política ampla segue incompleta: `GetDriveTypeW` + NTFS/ReFS não provam backing privado no Windows, e `fstatfs` não prova exclusividade/localidade dos dispositivos que sustentam outros filesystems Linux. P1 aberto; Linux nativo, CI hospedada, runtime instalado e produção não validados. Sem crédito formal ou release.

## Origem de volume não verificável no restore — 2026-10-25 20:28 BRT

Revisão independente apontou que `fstatfs` identificava OverlayFS como filesystem local sem revelar a origem dos diretórios de backing. A documentação do kernel confirma que OverlayFS combina camadas `lowerdir`/`upperdir`; o tipo visto pelo handle não prova que essas camadas sejam privadas. Removi OverlayFS da allowlist Linux. No Windows, `GetVolumeInformationByHandleW` agora identifica o filesystem pelo handle e só permite NTFS/ReFS; CSVFS e nomes desconhecidos falham fechado. A política Linux também segue fail-closed para tipos desconhecidos.

Foram adicionados testes de classificação: ext/NTFS/ReFS aceitos; OverlayFS/CSVFS/FS desconhecido rejeitados. `BackupRestoreDirectoryTests` passou 36, ignorou 8, falhou 0/44 em Windows/.NET 10; `FullSystemBackup` passou 114, ignorou 11, falhou 0/125. `ProgramStartupRecoveryTests` passou 6/6. `dotnet format style --verify-no-changes` passou no validador. Linux nativo não foi executado; o resultado dos testes Linux-only continua pendente.

O achado não está completamente fechado: `GetDriveTypeW == DRIVE_FIXED` e filesystem NTFS/ReFS não provam que o volume Windows não use armazenamento em bloco compartilhado. A origem de volumes Linux block-backed também não prova exclusividade entre hosts. Precisamos validar os tipos de armazenamento suportados, rejeitar origens compartilhadas/não verificáveis segundo a política aprovada e testar em ambientes nativos. Homologação formal segue 2/7 (28,6%), sem crédito; sem release/instalação/publicação.

Fontes técnicas: [OverlayFS — Linux kernel documentation](https://docs.kernel.org/filesystems/overlayfs.html).

## Reauditoria do lifecycle de restore por handles — 2026-10-25 18:15 BRT

Revisão da fonte atual e revisão independente não encontraram mutações pathname-based remanescentes dentro de `FileSystemRestoreTransaction`: staging, instalação, rollback, replay e cleanup usam `BackupRestoreDirectory` aberto e operações relativas a handles. `Directory.Exists`/`Path.GetFullPath` selecionam uma âncora; `OpenRoot` percorre os componentes sem seguir reparse/symlink e mantém os handles dos ancestrais. O ZIP é consumido pelo stream já aberto e a identidade é verificada antes da mutação.

`dotnet test tests/Jellyfin.Server.Implementations.Tests/Jellyfin.Server.Implementations.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~FullSystemBackup` passou 108, ignorou 11 e falhou 0/119 em Windows/.NET 10. Os skips de plataforma não validam Linux. A revisão independente retornou `No match` para mutação pathname-based restante dentro do lifecycle da transação.

Isso corrige descrições históricas que diziam que todo o lifecycle atual ainda era pathname-based; aquelas notas descreviam estados anteriores do checkout. O gate P0 permanece aberto: não houve teste determinístico de concorrência/race, execução Linux observável, validação da ACL/filesystem efetivos, CI hospedada ou teste em runtime instalado/produção. `BackupRestoreDirectory` ainda depende de APIs nativas Windows e syscalls Linux; compatibilidade/semântica operacional continuam sem evidência cross-platform. Formal permanece 2/7 (28,6%), sem crédito; sem release/instalação/publicação.

## ACL do arquivo de journal no recovery automático — 2026-10-25 11:27 BRT

`BackupRestoreRecoveryJournal.ReadAsync` agora valida owner/mode/ACL no handle do arquivo `active-restore.json` antes da desserialização. A checagem anterior protegia o caminho/diretório e o ZIP, mas não um arquivo de journal com ACL própria permissiva; como o recovery lê esse estado no startup, a lacuna podia permitir adulteração por identidade não confiável.

Regressão `RecoveryJournal_RejectsUntrustedJournalFileAclBeforeDeserializing` passou 1/1 em Windows/.NET 10 Release, tornando o arquivo gravável por Everyone enquanto mantém os diretórios confiáveis. A suíte `FullyQualifiedName~FullSystemBackup` passou 108, ignorou 11, falhou 0/119. `dotnet format style --verify-no-changes` passou nos dois arquivos. Linux/ACL POSIX e CI hospedada continuam sem execução observável; homologação formal não recebe crédito.

## Teste de ACL padrão nomeada no Linux — 2026-10-25 11:13 BRT

Adicionado `ValidateTrustedWriteBoundary_RejectsWritableNamedUserInLinuxDefaultAcl` em `BackupRestoreDirectoryTests`. O teste cria `system.posix_acl_default` real com `fsetxattr` no diretório isolado e verifica que uma entrada de usuário nomeado com escrita fora das identidades confiáveis é rejeitada pelo guard. O job Ubuntu existente executa essa suíte sob `FullyQualifiedName!~Integration`; não foi necessário criar job/dependência adicional.

Build da suíte Release em Windows/.NET 10 concluiu; o caso focal foi ignorado por ser Linux-only (1 skipped), logo o comportamento de xattr/ACL ainda não foi executado. `dotnet format style --verify-no-changes` passou. Portal local passou 197/197; JSON/JS válidos e `git diff --check` exit 0, com avisos de conversão LF/CRLF. Homologação formal segue 2/7 (28,6%), sem crédito; runner Linux hospedado e ACL real pendentes. Nenhum módulo, API, dependência ou comportamento de runtime mudou.

## Restore MariaDB: fixtures confiáveis e regressão Release — 2026-10-25 09:45 BRT

Os dois fixtures de restore/crash-recovery foram movidos de `%TEMP%` para diretórios exclusivos diretamente no perfil local. Neste host, `%TEMP%` herda `Modify` para `CodexSandboxUsers`, que é não confiável para o restore; o diretório de perfil não apresenta esse ACE e os testes agora passam sob a mesma política fail-closed de produção. Não houve relaxamento do `BackupRestoreAclValidator`.

Corrigido também o E2E que inicia restore num processo filho: o subprocesso exige `<root>/restore.zip`, então o archive criado pelo `BackupService` agora é copiado para esse caminho. O checkpoint injetado após o participante ocorre depois da substituição do alvo, mas antes do commit/remoção de sidecars; por isso a fixture verifica o conteúdo do backup já no destino e o original retido em `.bak`.

Validação Windows/.NET 10 Release com MariaDB 11.4.4 descartável em `127.0.0.1:33099`: dois cenários focais 2/2; `IntroSkipper.Integration.Tests` 54/54; regressão `MulletaFlix.sln` filtrando `LiveIntegrationTests`: 5.505 aprovados, 49 ignorados, 0 falhas/5.554 resultados em 17 assemblies. Os 17 TRX individuais mostram 0 falhas; ignorados restantes são casos de plataforma/opt-in Mongo. `Server.Integration.Tests` passou 145/145. MariaDB temporária encerrada, listener 33099 ausente. MariaDB operacional 3306 e MongoDB 27017 não acessados.

Portal local atualizado e contrato de homologação 192/192; JSON, JavaScript e `git diff --check` aprovados. Isso não é teste de Linux, CI hospedada, runtime instalado ou produção, não recebe crédito formal (continua 2/7, 28,6%) e não cria release/instalação/publicação.

## Regressão Release com MariaDB descartável — 2026-10-25 09:23 BRT

`dotnet test MulletaFlix.sln -c Release --no-build --no-restore --filter 'FullyQualifiedName!~LiveIntegrationTests'` foi executado com MariaDB 11.4.4 descartável, loopback porta 33099: 5.503 aprovados, 49 `NotExecuted` e 2 falhas em 5.554 resultados/17 assemblies. As duas falhas são `RestoreBackupAsync_RestoresDatabaseRowsOnDisposableMariaDb` e `RestoreBackupAsync_ReplaysAfterProcessTerminationBetweenDatabaseAndFileCommit`; o restore foi bloqueado pelo guard fail-closed porque um ancestral do caminho temporário tem ACE de escrita para identidade de sandbox não confiável. Não relaxar o guard. Tentativa de adaptar o fixture não resolveu e foi descartada; fonte dos testes não foi alterada nesta sessão. Instância temporária encerrada; nenhuma conexão ao MariaDB operacional 3306 ou MongoDB 27017. Próximo passo: executar em ambiente com ACL de ancestral apropriada ou corrigir fixture de forma segura, então repetir focal e regressão. Homologação formal 2/7 (28,6%), sem crédito; sem release, instalação, produção ou publicação remota.

## Reexecução Windows do gate de restore multi-schema — 2026-10-25 08:31 BRT

`dotnet test tests/Jellyfin.Server.Implementations.Tests/Jellyfin.Server.Implementations.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~FullSystemBackup` passou 107, ignorou 10 e falhou 0/117 em Windows/.NET 10. Os ignores incluem syscalls/symlinks específicos de Linux e casos de macOS; não validam o caminho Linux.

O teste opt-in `RestoreBackupAsync_ReplaysAfterProcessTerminationBetweenDatabaseAndFileCommit` compilou e foi selecionado, mas foi ignorado por ausência de `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING`. Preflight confirmou WSL não instalado, Docker/Podman ausentes e nenhuma configuração de MariaDB descartável. Portanto não houve replay real multi-schema nesta rodada; nenhum banco operacional foi acessado. Próximo gate continua MariaDB descartável + runner Linux observável. Homologação formal permanece 2/7 (28,6%), sem crédito; sem release ou produção.

## Gate de inicialização após recovery falho — 2026-10-25 08:19 BRT

`Jellyfin.Server/Program.cs` aguarda `RecoverInterruptedRestoreAsync` antes de migrações e `CoreAppHost.InitializeServices`. Se o recovery lança, o fluxo pula a inicialização; o `catch` de `StartServer` registra Critical, desativa restart e absorve a exceção sem atribuir `Environment.ExitCode`. Como `_setupServer.RunAsync()` precede `StartServer`, enquanto `configurationCompleted` ainda é falso durante recovery, o catch pode executar `SoftStop`, esperar 10 minutos e só então chamar `StopAsync`.

A auditoria não encontrou teste direto da ordem, do código de saída ou do atraso de shutdown. A `JellyfinApplicationFactory` inicia serviços por outro caminho sem exercitar recovery. Definir a semântica pretendida de encerramento e cobrir o processo antes de alterar esse comportamento.

`BackupServiceTests` focal passou 5 testes; 2 cenários exclusivos de macOS foram ignorados no Windows, 0 falhas/7. Isso cobre o serviço, não a orquestração de startup. Próximo gate: teste de processo com recovery falhando, prova de que `InitializeServices` não ocorre e verificação do exit code. Nenhum runtime foi alterado; formal 2/7 (28,6%), sem crédito ou release.

## Encerramento após commit do participante — 2026-10-25 08:14 BRT

O E2E opt-in `RestoreBackupAsync_ReplaysAfterProcessTerminationBetweenDatabaseAndFileCommit` encerra de fato o processo filho depois que `IntroSkipperBackupParticipant` real conclui e antes do commit dos arquivos. Um processo filho novo executa o recovery com o participante real; assertions verificam journal/sidecars, estado do banco central e do plugin, arquivos restaurados e limpeza. Isso fecha a lacuna de mesmo-processo, mas não altera runtime.

Validação local: `IntroSkipperBackupParticipantTests` 5/5; `BackupServiceTests` 68 aprovados, 3 ignorados por plataforma/ambiente, 0 falhas/71; contrato de seleção CI Linux/MariaDB 2/2; contrato do portal 189/189. O projeto de integração compilou; execução MariaDB foi ignorada por ausência de `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING`. Portanto o novo cenário de encerramento/reinício ainda não foi executado em MariaDB/Linux; restore real, TOCTOU/ACL e produção seguem gates separados. Homologação formal permanece 2/7 (28,6%), sem crédito ou release.

## Participantes de bancos de plugin — implementação local — 2026-10-25 06:47 BRT

25/10/2026 07:50 BRT: adicionada regressão SQLite `RestoreBackupAsync_RollsBackEarlierTableWritesWhenLaterTableInsertFails`, injetando falha de INSERT em `SeasonStates` após as gravações anteriores e verificando que o segmento/estado preexistentes permanecem. Classe `IntroSkipperBackupParticipantTests`: 5/5; suíte `IntroSkipper.Integration.Tests`: 19 aprovados, 35 ignorados, 0 falhas/54. Os ignores são testes opt-in MariaDB/migração e outros ambientes; não há comprovação MariaDB nesta execução. Contrato local do portal passou 185/185, JSON parseado e `node --check portal-site/homologacao.js` passou. Homologação formal 2/7, sem crédito; sem alteração de runtime, release, instalação ou produção.

25/10/2026 07:54 BRT: contrato `ci-mariadb-linux-coverage` passou 2/2 e confirma que `IntroSkipperBackupParticipantTests` está selecionado no job Linux/MariaDB. API GitHub reconsultada: último run `main` observado é 39608614450, de 21/10, sem runner provisionado (13/13 jobs falharam em 3–4 s, `runner_id=0`). Isso verifica configuração, não execução Linux nem resultado de MariaDB. Restore P0 e homologação formal 2/7 (28,6%) permanecem abertos, sem crédito ou release.

O contrato público `IBackupDatabaseParticipant` está em `MediaBrowser.Controller/SystemBackupService`. `BackupService` grava a lista `DatabaseParticipants` no manifesto de backup com banco incluído, exporta os participantes registrados e, no restore, valida identidade/conteúdo antes do journal e da mutação de arquivos. Um participante declarado e indisponível falha fechado; declarações com backup de banco desabilitado são inválidas. Manifestos legados sem a propriedade mantêm compatibilidade e não purgam bases de plugin omitidas.

`IntroSkipperBackupParticipant` está registrado pelo plugin e cobre as oito tabelas duráveis no schema `mulletaflix_introskipper`; lê em transação, valida snapshots antes da alteração, repõe os dados em transação própria e permite replay idempotente. `DetectionCache` não é incluído e é limpo após o restore. `IntroSkipperSchema.EnsureAsync` cria somente tabelas faltantes; `ValidateAsync` consulta cada entidade mapeada antes do journal para detectar colunas ausentes em tabelas preexistentes. Essa validação não inspeciona exaustivamente índices e constraints.

Evidência local: build do plugin passou sem warnings/erros; testes SQLite `IntroSkipperBackupParticipantTests` 4/4; `BackupServiceTests` 68 aprovados, 3 ignorados, 0 falhas/71 em Windows/.NET 10; build do projeto de testes passou (avisos de analisadores/documentação, 0 erros); contrato local do portal 181/181 e JSON válido. Os testes cobrem round-trip/replay idempotente e limpeza do cache, payload inválido sem apagar estado atual, schema parcial e rejeição prévia de tabela com colunas incompletas, além de rejeições fail-closed no core. O E2E `RestoreBackupAsync_RestoresDatabaseRowsOnDisposableMariaDb` cria archive pelo `BackupService`, verifica manifesto/entrada, restaura o participante real IntroSkipper e repete recovery após acknowledgement perdido. A fixture agora configura ambos os contextos IntroSkipper sobre MariaDB 11.4 numa base exclusiva por teste; o teste foi compilado, mas ignorado localmente porque a conexão descartável não foi configurada. O job CI também seleciona `IntroSkipperMariaDbTests.SchemaCreationTargetsTheDedicatedDatabase`, que usa o registro real do plugin e valida o nome fixo `mulletaflix_introskipper`; nenhum resultado remoto foi observado. O snapshot completo é materializado em memória; sem medição de volume não foi imposta cota, e streaming por tabela será necessário se o dataset crescer além de um tamanho seguro. **Ainda pendente:** executar os dois cenários em MariaDB descartável, observar falha após commit central com recuperação por journal e validar runtime Linux/produção. Não acessado banco operacional; sem release, instalação, commit, push ou publicação remota. Homologação formal permanece 2/7 (28,6%), sem crédito.

25/10/2026 07:00 BRT: `.github/workflows/ci.yml` agora inclui explicitamente `RestoreBackupAsync_RestoresDatabaseRowsOnDisposableMariaDb` no filtro do job `user-mariadb-linux-tests`, que provisiona MariaDB 11.4 descartável. O contrato `ci-mariadb-linux-coverage.test.mjs` protege a seleção. Isso agenda o E2E para execuções futuras do workflow; não equivale a execução remota. O teste ainda não foi executado localmente nesta sessão por ausência de MariaDB descartável, e a base IntroSkipper do fixture continua SQLite em memória. Homologação formal sem crédito adicional.

25/10/2026 07:07 BRT: fixture E2E passou a usar um segundo schema MariaDB efêmero, nomeado por GUID, para IntroSkipper e DetectionCache, além do schema central; ambos são removidos no `finally`. CI inclui também `IntroSkipperMariaDbTests.SchemaCreationTargetsTheDedicatedDatabase`, que valida o registro real no schema fixo do plugin em serviço descartável. Build de integração passou; execução local foi ignorada sem variável de conexão. Não há execução hospedada observável. O teste de contrato CI foi atualizado; nenhum banco operacional foi acessado.

25/10/2026 07:08 BRT: contrato CI MariaDB passou 2/2; contrato da página de homologação passou 181/181; JSON válido e `git diff --check` sem erros (avisos apenas de normalização LF/CRLF). A execução MariaDB focal segue skip local, e não foi observado run remoto; formal permanece 2/7 (28,6%), sem crédito.

25/10/2026 07:09 BRT: revisão independente apontou que cancelamento do teste poderia cancelar a remoção dos schemas de fixture. Cleanup alterado para `CancellationToken.None`; revisão não encontrou incompatibilidade do filtro CI com o MariaDB descartável nem inconsistência no portal. Build Release integração passou (0 erros/88 avisos), contratos MariaDB 2/2, portal 181/181 e `git diff --check` sem erros. Teste focal MariaDB segue sem execução real.

25/10/2026 07:15 BRT: revisão do filtro CI encontrou que `IntroSkipperBackupParticipantTests` (SQLite, snapshot inválido e schema parcial) estava fora dos jobs apesar do E2E MariaDB selecionado. O filtro Linux MariaDB agora seleciona também a classe unitária, e o contrato `ci-mariadb-linux-coverage.test.mjs` exige sua presença. Execução local focal e contrato pendentes nesta atualização; run remoto continua não observado. Sem mudança de runtime/API/dependência; formal 2/7, sem crédito.

25/10/2026 07:16 BRT: `IntroSkipperBackupParticipantTests` Release passou 4/4; contrato de cobertura MariaDB passou 2/2; homologação portal passou 181/181; JSON válido e `git diff --check` sem erros (somente avisos LF/CRLF). E2E MariaDB foi ignorado por ausência de conexão descartável e não há run remoto observado. Homologação formal 2/7, sem crédito.

## Lacuna: schema persistente do IntroSkipper fora do backup integral — 2026-10-25 06:28 BRT

Auditoria estática do checkout identificou 9 tipos de contexto EF registrados em produção. O backup atual inclui o contexto central e quatro contextos de mídia; `UsersDbContext` e `SystemDbContext` expõem tipos já incluídos pelo contexto central. Permanecem fora do backup os oito DbSets de `IntroSkipperDbContext` e o DbSet `DetectionCache` de `DetectionCacheDbContext`.

O schema do plugin é explicitamente `mulletaflix_introskipper`, separado do banco central. A restauração conecta os quatro contextos de mídia à conexão/transação primária e confirma o banco antes do commit dos arquivos. Não se pode simplesmente tratar o schema do plugin como outro contexto de mídia.

Uma extensão do backup desacoplada por participante é o candidato para não criar referência do servidor central ao assembly do plugin. A participação deve declarar nome/schema, entradas do archive e validação pré-journal; archive novo que declara um participante ausente no runtime precisa falhar fechado. Archives legados sem declaração de participante devem continuar restauráveis sem purgar estado que não estava no arquivo. A restauração do plugin precisa usar transação própria, importação idempotente e ocorrer dentro da janela de recuperação do restore.

`RecoverInterruptedRestoreAsync` invoca novamente a restauração do archive antes de `InitializeServices`, então uma falha após commit do banco central pode convergir no restart — mas apenas se cada participante puder ser reexecutado com segurança e o journal for preservado até sua conclusão. Teste obrigatório: interromper entre commit central e participant, reiniciar/replay e provar estado final completo e repetível. Não foi validado que qualquer implementação candidata satisfaz isso.

O `DetectionCache` é explicitamente otimização no código: miss retorna falso; falha de leitura é tratada como miss; falha de escrita é logada e ignorada enquanto a análise válida continua. Evidência favorece não arquivar o cache; no restore, limpá-lo é mais seguro contra valores de mídia antiga, ao custo de recomputar análise. Os oito conjuntos de segmentos, estado de temporada, overrides, histórico, opt-out e projection/outbox são dados duráveis e devem integrar o backup.

Sem teste de integração multi-schema nesta rodada nem acesso ao MariaDB operacional. O achado é cobertura estática, não prova de perda já ocorrida em produção. Portal atualizado localmente com o evento `server-introskipper-backup-gap-2026-10-25-0628`; homologação formal continua 2/7 (28,6%), sem crédito. Nenhuma alteração de runtime, release ou publicação remota.

## Restore Linux: cobertura do workflow e observabilidade — 2026-10-25 05:19 BRT

Auditoria independente do workflow e dos testes confirmou que o job existente `server-build-test` roda em `ubuntu-latest` e executa `dotnet test MulletaFlix.sln` em Release com filtro `FullyQualifiedName!~Integration`. `BackupRestoreDirectoryTests` e `BackupServiceTests` não têm `Integration` no nome e estão abrangidos; criar job duplicado não acrescentaria cobertura por si só.

A API pública do GitHub foi consultada em 25/10/2026: o CI mais recente continua sendo o run `39608614450`, de 21/10/2026. Os 13 jobs listados falharam entre 3 e 4 segundos, todos com `runner_id=0`; logs do job `Server build & unit tests` (`119251248109`) respondem 404 `BlobNotFound`. Isso comprova indisponibilidade de evidência do run, não execução nem falha dos testes. Neste host, WSL está ausente e Docker/Podman não estão disponíveis; nenhum teste Linux foi executado localmente nesta sessão.

O P0 não fecha: corridas adversariais Linux ainda não foram reproduzidas/testadas e o resultado do runner Linux permanece sem evidência observável. A política ACL aprovada confia na identidade do serviço e root/admins; não há bypass por ator não confiável demonstrado. Homologação formal permanece 2/7 (28,6%), sem crédito; sem release ou publicação.

## Origem ZIP do restore: ACL e compartilhamento validados — 2026-10-24 21:12 BRT

`RestoreBackupCoreAsync` abre a origem relativamente ao diretório pai validado, sem seguir symlink/reparse point, valida owner/ACL do handle e consome o mesmo stream usado no integrity-check. No Windows, a origem é aberta sem compartilhamento de escrita ou exclusão, impedindo alteração in-place ou troca enquanto o restore lê o arquivo. Arquivo ausente preserva `FileNotFoundException`. Testes adversariais cobrem ACL de origem antes do journal e bloqueio de writer concorrente no Windows. Release/.NET 10 em Windows: foco 2/2; `FullSystemBackup` 105 aprovados, 10 ignorados, 0 falhas/115. O modo Linux no teste de ACL foi compilado, não executado neste host.

Fechado somente o trust-boundary local do ZIP. Linux nativo/CI observável, ACL e filesystem da instalação, mount identity, races sob identidade confiável, runtime e produção continuam sem validação. Homologação formal 2/7 (28,6%), sem crédito. Nenhuma release ou publicação remota; checkout 8 commits atrás de `origin/main`, com alterações preexistentes.

## ACL de arquivos existentes no restore — 2026-10-24 18:23 BRT

Foi fechado um gap concreto da política aprovada: antes de mover o destino original para `.bak`, o restore abre o arquivo relativamente ao diretório já validado e inspeciona owner e permissões pelo handle. Durante replay, um `.bak` preexistente também é validado antes de ser reutilizado. No Windows, o handle solicita `READ_CONTROL`; no Linux, a validação considera UID, mode e ACL access. O teste adversarial que antes aceitava escrita de `Everyone` falhou antes da correção e passou depois, preservando o conteúdo original e sem deixar `.bak`. A suíte `FullSystemBackup` Release passou 103/113 no Windows/.NET 10; 10 foram ignorados por plataforma/ambiente. Linux nativo e ACL POSIX seguem sem execução nesta rodada.

Revisão independente encontrou gaps adicionais ainda abertos: validação de ACL/trust-boundary do arquivo ZIP por handle; identificação de bind mounts em Linux; corrida condicional entre verificação e syscall sob identidade confiável. Nenhum bypass por usuário não confiável foi demonstrado. O CI ativo não executa a suíte de restore no job Windows; há filtros de restore no Linux, mas a execução remota observada anteriormente falhou sem steps/log recuperável. Formal continua 2/7 (28,6%), sem crédito. A worktree principal permanece 8 commits atrás de `origin/main`, com alterações preexistentes; nada foi instalado nem publicado.

## Resultado remoto de CI e limites de observabilidade — 2026-10-24 17:35 BRT

Leitura pública do GitHub identificou como execução mais recente da branch `main` o workflow CI run `39608614450` (run 463), no commit `d51f4f38d2b4148ab49394c001567ad273bd57aa`, conclusão `failure`. A API lista 13 jobs; o job `Server build & unit tests` terminou `failure` após poucos segundos e retorna `steps: []`. A API de logs desse job respondeu `404 BlobNotFound`. Portanto o resultado remoto é falha, mas não há evidência disponível para apontar etapa ou causa; não inferir teste Linux executado, nem atribuir a falha ao restore. Não foi feito rerun.

## Escopo real do workflow — 2026-10-24 17:34 BRT

A job `server-build-test` em Ubuntu executa build e testes da solução, mas o comando de teste exclui `FullyQualifiedName!~Integration`. Há jobs Linux dedicados que executam filtros focados, incluindo MariaDB e cenários selecionados de restore/replay. A configuração desses jobs não equivale a executar a suíte de integração inteira, e só resultados remotos com steps/logs disponíveis provam execução. Para a auditoria de races ACL/namespace deste arquivo, ainda falta evidência de execução e cobertura determinística.

## Escopo das races Linux sob a política ACL aprovada — 2026-10-24 17:31 BRT

Revisão do código e segunda leitura independente refinam a classificação das janelas de namespace. `BackupRestoreRecoveryJournal.AcquireOperationLease` impede sobreposição entre processos que usam o fluxo `BackupService`; o teste multiprocesso confirma exclusividade do lease, mas não sincroniza substituição entre syscalls. As janelas `mkdirat`→`openat`, `statx`→`renameat[2]` e `statx`→`unlinkat` requerem que outro processo possa alterar o mesmo diretório-pai. Operações usam dirfd e nomes simples; a evidência não demonstra travessia para fora desse pai. Rename/unlink não seguem o symlink final.

`BackupRestoreAclValidator` valida owner service/root, rejeita bits de escrita group/other e valida ACL access/default nos diretórios consultados. Pela política aprovada pelo usuário, service e root/admins são confiáveis; logo, a race documentada é alcançável por um ator dentro dessa identidade confiável ou por mutação administrativa, não está demonstrada como bypass de ACL por usuário não confiável. A classificação mais precisa para essa parte é risco de concorrência/defesa em profundidade. Isso não fecha o gate de restore: sem execução Linux, permanecem sem evidência a semântica real de `statx`/`openat`/`renameat2`/`unlinkat`, parsing de ACL, filesystem efetivo, lease Linux e recuperação de crash. Em `.github/workflows/ci.yml`, o job Ubuntu `server-build-test` exclui testes com `FullyQualifiedName!~Integration`; há jobs Linux separados com filtros focados, incluindo testes de restore/replay e MariaDB. Nenhum resultado remoto desta revisão foi consultado, então a configuração não comprova execução nem cobertura dos casos ACL/race deste audit.

Fontes primárias consultadas: man-pages Linux [`renameat(2)`](https://man7.org/linux/man-pages/man2/renameat.2.html), [`unlinkat(2)`](https://man7.org/linux/man-pages/man2/unlinkat.2.html) e [`open(2)`](https://man7.org/linux/man-pages/man2/open.2.html). Nenhuma exploração foi executada; nenhuma mudança de runtime foi feita. Homologação formal permanece 2/7 (28,6%), sem crédito.

## Regressão integral servidor e Nebula — 2026-10-24 17:27 BRT

Reexecução build-inclusive da solução Release terminou com exit code 0 em Windows/.NET 10: 5.283 aprovados, 267 ignorados, 0 falhas/5.550 resultados em 17 projetos. A suíte Nebula Python, usando Python 3.10, `uv`, `requirements.lock` e `requirements-test.txt`, passou 694/694. Esta é evidência automatizada local, não aceite formal ou validação de runtime/produção. Testes de plataforma Linux/macOS, integrações opt-in e providers live não foram executados. Restore ACL/TOCTOU P0 continua aberto; homologação formal permanece 2/7 (28,6%), sem crédito. Checkout está 8 commits atrás de `origin/main` e contém alterações preexistentes; não criar release com base apenas neste resultado.

## Estado atual da fonte e regressão Windows — 2026-10-24 17:21 BRT

A descrição inicial de 14:50 abaixo foi superada por alterações posteriores no mesmo checkout: o código atual já integra `FileSystemRestoreTransaction` com `BackupRestoreDirectory`. A transação retém handles de diretório, usa operações relativas para staging, instalação, rollback, replay e cleanup, e aplica validação de namespace/ACL nos destinos e pais antes de mutações relevantes (`BackupService.cs`, `BackupRestoreDirectory.cs`, `BackupRestoreAclValidator.cs`, `BackupRestoreRecoveryJournal.cs`). Portanto, não é correto resumir o estado atual como lifecycle integralmente pathname-based.

Reexecutei `FullyQualifiedName~FullSystemBackup` em Release Windows/.NET 10: 102 aprovados, 10 ignorados, 0 falhas/112. Os ignorados são casos Linux/macOS. A auditoria estática ainda identifica janelas condicionais no backend Linux: `mkdirat` seguido de `openat`, `statx` seguido de `renameat`/`renameat2`, e `statx` seguido de `unlinkat`. Não houve exploração determinística nem execução em Linux nesta sessão. A segurança efetiva contra concorrência depende da fronteira ACL aprovada (identidade do serviço e root/administradores); mesmo assim, operações atômicas e validação Linux continuam gate aberto. Homologação formal permanece 2/7 (28,6%), sem crédito; runtime instalado, produção, Linux nativo e CI remota não foram validados.

Próximo passo: executar a suíte e uma corrida controlada em runner Linux real, então confirmar owner/mode/ACL efetiva e filesystem de cada raiz aceita. Não declarar P0 fechado por regressão Windows.

## Reauditoria do lifecycle por handle — 2026-10-24 14:50 BRT

Revisão independente e leitura do checkout confirmaram que `BackupRestoreDirectory` e `BackupRestoreAclValidator` ainda não governam `FileSystemRestoreTransaction`. Apply/replay, rollback, cleanup e remoção de diretórios continuam usando `File`/`Directory` por pathname, com contenção lexical e checagem de reparse point separadas das mutações. A origem ZIP e gates de journal também mantêm aberturas por nome. A ACL aprovada (identidade do serviço e root/administradores; compartilhado/inconclusivo falha fechado) ainda não está integrada ao ciclo de gravação.

Revalidação Release `FullyQualifiedName~FullSystemBackup` no Windows/.NET 10: 95 aprovados, 10 ignorados, 0 falhas/105; ignorados incluem operações exclusivas de Linux/macOS. Contrato da homologação no portal: 132/132. Estes resultados não comprovam ACL efetiva, TOCTOU, Linux nativo, runtime instalado ou produção; homologação formal permanece 2/7 (28,6%), sem crédito. Próxima fatia aprovada: migrar o lifecycle completo de uma entrada (stage/original/marker/install/rollback/replay/cleanup) para nomes relativos a diretórios abertos e validados, com handles retidos; incluir criação/remoção segura de diretórios e fixtures ACL privadas antes de portar todas as raízes. Nenhum código de runtime, banco, instalação ou produção foi alterado nesta sessão.

## Preflight de espaço do restore sem mutações — 2026-10-24 11:39 BRT

`RestoreBackupCoreAsync` agora usa `TestCommonPathsForStorageCapacityReadOnly`: quando um dos caminhos configurados ainda não existe, consulta o espaço pelo ancestral já existente mais próximo sem criar Data/Cache/ProgramData antes da validação de confiança. `Jellyfin.Server.Program` continua chamando `TestCommonPathsForStorageCapacity`, que mantém a criação de diretórios no startup. Teste Release cobre os três caminhos ausentes; StorageHelper + FullSystemBackup passou 93, ignorou 10 cenários Linux/macOS e falhou 0/103 no Windows/.NET 10.

Isso fecha somente o efeito colateral do preflight de capacidade; não é gate de ACL, não garante que o ancestral represente um mountpoint específico e não protege contra troca TOCTOU. Validação de ACL/owner por handle e migração conjunta de apply/rollback/replay/cleanup continuam P0, sem crédito formal (2/7), sem runtime/produção/release ou execução Linux nativa. Ver [[Sessões/2026-10-24-MulletaFlix-restore-readonly-capacity-preflight-1139]].

#### Retenção de ancestrais Linux — 2026-10-24 09:09 BRT

`BackupRestoreDirectory.OpenLinuxDirectoryPath` agora mantém os descritores dos ancestrais abertos até `Dispose`, alinhando o ciclo de vida dos handles ao backend Windows. O teste `OpenRoot_RetainsLinuxAncestorHandlesUntilDisposed` verifica retenção e liberação via `/proc/self/fd`. Em Windows/.NET 10, `BackupRestoreDirectoryTests` passou 23, ignorou 7 Linux/macOS e falhou 0/30; `FullSystemBackup` passou 91, ignorou 10 Linux/macOS e falhou 0/101. Os casos Linux foram ignorados no host Windows; a nova asserção ainda exige execução Linux nativa. ACL/owner continua sem validação; o helper não está integrado a apply/rollback/replay/cleanup, que seguem pathname-based. P0 continua aberto; homologação formal 2/7 (28,6%), sem crédito. Nenhum runtime, produção ou release alterado.

## Contrato de confiança do restore — 2026-10-24 08:38 BRT

A revisão da base limpa `d51f4f38` confirmou que `RestoreBackupCoreAsync` continua usando `Directory.CreateDirectory` e extração por pathname, sem ACL efetiva ou lifecycle por handles. Os caminhos de entrada incluem controller, startup e migração; todos convergem em `RestoreBackupCoreAsync`. As cinco raízes de destino, origem ZIP e diretório/lease de recuperação precisam compartilhar o mesmo gate antes de qualquer escrita, incluindo escrita relacional.

Decisão: somente a identidade efetiva do serviço e administradores/root podem ter capacidade de alterar a árvore. No Windows, validar pelo handle a identidade do owner/DACL, ACEs explícitas/herdadas e acesso efetivo do processo; permitir SID do serviço, SYSTEM e BUILTIN Administrators. No Linux, validar UID efetivo/root, modo e ACL POSIX access/default pelo descritor; permissões de grupo/outros ou ACL não interpretável que permitam escrita reprovam. Caminhos remotos/compartilhados e APIs/permissões inconclusivas falham fechado. A checagem deve permanecer vinculada aos handles usados para gravar; preflight por pathname sozinho não fecha TOCTOU.

Baseline na base limpa: `dotnet test MulletaFlix-master/tests/Jellyfin.Server.Implementations.Tests/Jellyfin.Server.Implementations.Tests.csproj -c Release --filter FullyQualifiedName~FullSystemBackup` passou 40/40 em Windows/.NET 10, sem casos Linux. O checkout principal, separado e sujo, repetiu 91 aprovados/9 ignorados/0 falhas (100 totais). Portal local antes desta atualização: 97/97. Não houve mudança de runtime, ACL, banco, instalação ou produção; homologação formal permanece 2/7 sem crédito. Implementação completa, restore adversarial, ACL real e Linux nativo seguem pendentes. Ver [[Sessões/2026-10-24-MulletaFlix-restore-acl-contract-and-clean-baseline-0838]] e [[Decisões/2026-10-24-restore-acl-trust-boundary]].

## Próxima fatia de segurança — preflight fail-closed de ACL — 2026-10-24 07:44 BRT

Revisão independente do estado atual confirmou que `BackupRestoreDirectory` assume ACL confiável, mas não verifica identidade/permissões. `RestoreBackupCoreAsync` ainda aciona `FileSystemRestoreTransaction` por caminhos; apply, rollback, cleanup e replay não estão migrados em conjunto. A origem ZIP e a existência do journal também conservam consultas por caminho.

A próxima implementação deve ser um gate fail-closed antes de criar journal/sidecars ou alterar dados: validar identidade efetiva e fronteira de confiança de todas as raízes e ancestrais afetados, diretório do journal e ZIP. A política aprovada permite escrita somente à identidade efetiva do serviço e root/administradores; caminhos compartilhados e consultas de permissão inconclusivas falham fechados. Depois desse gate, migrar a transação inteira, sem adoção parcial do helper.

Verificações desta sessão: `ItemPersistenceServiceTests` Debug passou 29/29; esse resultado não altera a homologação. A revisão do restore foi estática; nenhum teste ACL, Linux nativo, runtime instalado, produção ou banco foi executado. P0 TOCTOU/ACL permanece aberto e sem crédito formal. Ver [[Sessões/2026-10-24-MulletaFlix-restore-acl-preflight-review-0744]].

## Regressão .NET integral da solução — 2026-10-24 07:38 BRT

`dotnet test MulletaFlix.sln --configuration Release --no-restore --verbosity quiet -m:1` terminou com exit code 0 em Windows/.NET 10. Os projetos de teste executados reportaram sucesso; por exemplo, `Jellyfin.Server.Implementations.Tests` 1.607 aprovados/75 ignorados/0 falhas, `Jellyfin.Api.Tests` 772/3/0 e `Jellyfin.Providers.Tests` 865/13/0. A saída intermediária do terminal foi truncada, portanto esta sessão não consolida a soma completa por projeto. Houve avisos de analisadores/documentação; integrações opt-in e testes de plataforma aparecem como ignorados.

Esta é regressão automatizada local, não aceite formal. ACL efetiva, corrida integrada de restore, Linux nativo, runtime instalado e produção não foram testados. P0 TOCTOU/ACL segue aberto; homologação formal permanece 2/7 (28,6%), sem crédito ou release. Ver [[Sessões/2026-10-24-MulletaFlix-dotnet-solution-regression-0738]].

## Reauditoria de TOCTOU e ACL no lifecycle — 2026-10-24 07:28 BRT

Duas auditorias independentes confirmaram que `BackupRestoreDirectory` continua restrito ao journal. Apply, rollback, cleanup e remoção de diretórios em `FileSystemRestoreTransaction` ainda operam por pathname; replay reutiliza esse caminho. A origem ZIP e `JournalExists` também mantêm consultas por caminho. O helper rejeita links estáticos e oferece operações relativas a handle, mas não verifica a ACL/identidade efetiva aprovada (identidade do serviço e root/administradores); verificações Linux de tipo não tornam todas as mutações atômicas.

`FullSystemBackup` recompilado em Release passou 91/100 no Windows/.NET 10 (9 ignorados por Linux/macOS, 0 falhas). Não houve teste integrado de corrida adversarial nem validação de ACL real, Linux nativo, runtime instalado ou produção. A exploração depende de acesso concorrente às raízes; não foi reproduzida. O P0 permanece aberto, sem crédito à homologação formal (2/7) e sem release. Ver [[Sessões/2026-10-24-MulletaFlix-restore-security-boundary-audit-0728]].

## Gate de plataforma no startup — 2026-10-23

`RecoverInterruptedRestoreAsync` não adquire lease em plataformas diferentes de Windows/Linux quando não existe journal pendente; isso evita derrubar o startup normal no macOS. Se existir journal, a inicialização falha fechada com `PlatformNotSupportedException`, pois não há backend de recuperação nessa plataforma. Os endpoints de restore normal e point-in-time retornam HTTP 501 antes de agendar reinício. `BackupServiceTests` focal passou 87/95 no Windows/.NET 10 (8 ignorados, incluindo os casos exclusivos de macOS) e `BackupControllerTests` passou 7/7. `BackupRestoreRecoveryJournal.JournalExists` usa `File.GetAttributes` e só trata `FileNotFoundException`/`DirectoryNotFoundException` como ausência; erros de acesso são propagados para falhar fechado. O código não foi executado em macOS; o workflow possui runner macOS para testes/build iOS, mas não para a suíte do servidor. ACL/TOCTOU da transação continuam P0. Sem release, runtime instalado ou produção.

Data: 2026-10-21
Estado: cobertura integral ainda parcial; restore rejeita ZIP corrompido e conteúdo JSON relacional inválido/incompatível antes de sobrescrever arquivos.

## Revalidação focal — 2026-10-23

`dotnet test tests/Jellyfin.Server.Implementations.Tests/Jellyfin.Server.Implementations.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~FullSystemBackup --logger 'console;verbosity=minimal'` terminou com exit code 0 no Windows/.NET 10: 80 aprovados, 3 ignorados (todos Linux-only), 0 falhas, 83 no total. A recompilação emitiu avisos de analisadores/documentação em arquivos do servidor e testes; não impediram a execução.

Este resultado confirma a suíte unitária atual e as primitivas Windows executáveis localmente, não o teste de troca concorrente de ancestral nem as primitivas Linux ignoradas. `BackupService.FileSystemRestoreTransaction` ainda aplica, reverte e limpa arquivos por pathname após validações separadas. O journal usa `BackupRestoreDirectory` para leitura, gravação, lease e remoção do próprio journal após abrir sua raiz; o bootstrap de `_programDataPath` e a abertura da origem ZIP continuam por pathname. O helper relativo não está integrado à transação e mantém races condicionais nas primitives Linux quando um ator pode alterar o diretório-pai. A mitigação completa de TOCTOU, ACL efetiva das raízes em instalação e prova determinística Windows/Linux continuam pendentes; não há evidência de exploração remota. Nenhum arquivo de runtime foi alterado nesta revalidação.

## Escopo verificado

`Jellyfin.Server.Implementations.FullSystemBackup.BackupService` injeta somente
`IDbContextFactory<MulletaFlixDbContext>`. O backup percorre os `DbSet` públicos
desse contexto e `HistoryRow`, mas usa no manifesto e nos caminhos ZIP os nomes
físicos das tabelas do modelo EF; validação e restore verificam os mesmos nomes.
A restauração purga/restaura esse contexto. As entradas ausentes para tabelas
são ignoradas, exceto o histórico de migrations, que é obrigatório.

### Reconciliação dos contextos EF e bancos

Foram identificados nove contextos EF Core de produção: `MulletaFlixDbContext`,
`UsersDbContext`, `MoviesDbContext`, `SeriesDbContext`, `ChannelsDbContext`,
`BooksDbContext`, `SystemDbContext`, `IntroSkipperDbContext` e
`DetectionCacheDbContext`. `ServiceCollectionExtensions.AddMulletaFlixDbContext`
registra os sete primeiros com a mesma instância de `efCoreConfiguration`; o
backup geral enumera apenas propriedades do contexto central. `UsersDbContext`
e `SystemDbContext` contêm entidades cujas tabelas também estão no modelo
central, portanto não devem ser adicionados como exportações duplicadas sem
antes definir a propriedade física e a fonte de verdade.

Os quatro modelos de domínio (`MoviesDbContext`, `SeriesDbContext`,
`ChannelsDbContext` e `BooksDbContext`) mapeiam tabelas próprias que não são
enumeradas pelo backup central. `DomainSchemaInitializer` usa explicitamente
`DatabaseNames.Main` para criá-las, e a rotina de migração chama esse
inicializador com os contextos de domínio. O setup MariaDB também cria bancos
com nomes `mulletaflix_users`, `mulletaflix_movies`, `mulletaflix_series`,
`mulletaflix_channels` e `mulletaflix_books`, mas os factories observados não
selecionam esses nomes individualmente. Isso prova a configuração no código,
não a topologia física de uma instalação nem o provider ativo em produção.

IntroSkipper registra `IntroSkipperDbContext` e `DetectionCacheDbContext` com a
opção de banco `mulletaflix_introskipper`; seus nove conjuntos de tabelas não
aparecem no backup central. A confirmação de cobertura requer declarar o
ownership por banco/tabela e testar a combinação real de provider e migração;
não se deve tratar nomes dos contextos como prova de bancos independentes.

O banco MongoDB do Nebula também é externo ao `BackupService`. A fonte identifica
seis coleções usadas pelo servidor: `files`, `nebula_admission`, `users`,
`bot_tokens`, `operation_replays` e `stats` (esta última é consultada como
opcional para estatísticas do feeder). A configuração `BotTokensCollection`
também permite ler outra coleção Mongo com nome configurável. O fluxo de
sincronização Nebula/Supabase é separado e não prova cobertura integral dessas
coleções nem dos outros contextos relacionais. Nenhum banco remoto ou produção
foi acessado nesta auditoria.

Regressão encontrada nesta sessão: `BotTokensCollection` era usada pelo
carregamento operacional, mas a sincronização Supabase lia e restaurava somente
`bot_tokens`. A fonte foi alterada para propagar o nome configurado ao backup,
restore e importação de tokens. A regressão exercita coleções padrão e
customizada; os dois casos passaram com MongoDB 8.3.7 descartável limitado a
`127.0.0.1:27099`. A suíte `FullyQualifiedName~Nebula` passou 730/730 com o
mesmo fixture. Isso valida o fluxo local, não a conexão real do Supabase nem
backup integral/produção. A correção está incluída em `12.1.29`.

O ZIP também inclui arquivos XML/JSON de configuração de primeiro nível,
`Config/users`, tarefas agendadas, `Root`, coleções e playlists; legendas,
trickplay e metadados dependem das opções. Não há criptografia explícita na
criação do ZIP.

## Riscos e compatibilidade

- O manifesto valida `BackupEngineVersion` por igualdade exata (`0.2.0`). Uma
  evolução de formato precisa manter leitura explícita dos arquivos legados;
  apenas adicionar tabelas à versão atual pode resultar em incompatibilidade. A
  validação exige que `DatabaseTables` corresponda exatamente aos nomes físicos
  mapeados pelo modelo EF para os DbSets exportados pelo `MulletaFlixDbContext`
  mais `HistoryRow`, e que cada entrada JSON correspondente exista no ZIP.
- A restauração valida CRC e comprimento de todas as entradas do ZIP antes de
  verificar espaço e antes de gravar qualquer arquivo ou banco. Também
  desserializa em streaming as entidades JSON de cada tabela do contexto
  relacional central antes de copiar `Config`, `Data` ou `Root` e antes de abrir
  o banco; JSON malformado, nulo ou incompatível é rejeitado sem sobrescrever a
  configuração existente. Isso não torna a restauração transacional: falhas de
  constraints do banco, componentes não cobertos ou falhas durante a aplicação
  ainda podem deixar estado parcialmente aplicado.
- A validação semântica do componente relacional ocorre antes de copiar
  `Config`, `Data` ou `Root` e antes de abrir o banco. Declaração ausente,
  tabela não suportada ou entrada ausente produz `InvalidDataException`.
- Arquivos de configuração podem conter dados sensíveis. A ausência de
  criptografia explícita exige uma decisão de proteção e gestão de chave antes
  de incluir mais dados de usuário/operacionais no mesmo arquivo.
- O provider MariaDB tem um dump nativo usado em backups de migração; o fluxo
  `BackupService` não o incorpora. Reutilizá-lo exigiria validar disponibilidade
  de ferramentas, consistência, portabilidade e semântica de restauração.

## Evidência e próximos gates

Reconciliação estática dos contextos, factories e inicializador feita em
2026-10-21: `ServiceCollectionExtensions.cs`, `DomainDbContexts.cs`,
`UsersDbContext.cs`, `DomainSchemaInitializer.cs`,
`MariaDbProcessManager.cs` e `PluginServiceRegistrator.cs`. Nenhum banco ativo,
provider de produção ou instalação foi consultado; a inferência sobre bancos
físicos efetivos continua pendente.

Em 2026-10-21, `FullSystemBackup` passou 39/39 testes Release. Três novos casos
provam que JSON sintaticamente inválido, item nulo e entidade incompatível são
rejeitados sem sobrescrever configuração nem abrir o contexto de banco. Os
casos prévios também cobrem declaração ausente, entrada ausente e tabela
desconhecida. A suíte completa `Jellyfin.Server.Implementations.Tests` e seu
resultado desta rodada estão registrados em `docs/releases/12.1.32.md`.
O build de `Jellyfin.Server.Implementations` passou em Release sem erros; a
última execução reportou quatro avisos XML em projetos referenciados. Os
builders Windows e Linux também geraram os pacotes da release
12.1.29; os pacotes Windows, ZIP e tar.gz foram inspecionados quanto à presença
do servidor, frontend e worker Nebula, e hashes/tamanhos foram registrados em
`docs/releases/12.1.29.md`. A regressão Supabase/Mongo para coleção padrão e
customizada passou 2/2 e a suíte Nebula passou 730/730, com Mongo descartável
em `127.0.0.1:27099`. Scripts Node de release/packaging passaram 23/23. Estes
gates não comprovam cobertura de todos os contextos/coleções nem um restore
integral de ponta a ponta.

## Rejeição de reparse points durante a extração do restore — 2026-10-22

INTENT: nenhum caminho de arquivo vindo do ZIP pode redirecionar a extração
para fora da pasta de destino configurada através de symlink ou junction.

`BackupService.RestoreBackupCoreAsync` continua usando a cópia local do fluxo
`CopyDirectory`. Antes e depois de criar o diretório pai, o restore verifica
cada componente existente abaixo da raiz de destino e rejeita
`FileAttributes.ReparsePoint`; a raiz configurada continua confiável e pode ser
um link intencional. O teste com symlink real reproduziu a escrita fora da raiz
antes da correção e passou após a mudança. `FullSystemBackup` passou 40/40 em
Release. Busca de equivalentes: há um único uso de `ZipArchiveEntry.ExtractToFile`
no código C# do servidor, o ponto protegido nesta alteração.

Limites: a checagem de caminho ainda tem uma janela TOCTOU para alteração
concorrente por processo que possa modificar os diretórios; não foi feita
extração por handles de diretório nem restore transacional. A rotina de banco
ainda engole erro individual de `Add` depois da purga e pode concluir parcial;
isso permanece um gate separado de T4.3. Sem banco, instalação, Linux, produção
ou CI remota acessados.

Antes de chamar o recurso de backup integral novamente, executar nesta ordem:

1. Mapear propriedade física de cada tabela/schema e cada coleção externa,
   distinguindo fonte de verdade, dados duplicados e compatibilidade legada.
2. Evoluir o manifesto para declarar componentes relacionais, Mongo e Supabase,
   omissões e hashes; manter compatibilidade legada de `0.2.0`. A pré-validação
   semântica agora protege as entradas relacionais do backup central, mas ainda
   não cobre contextos EF independentes nem coleções Mongo.
3. Definir criptografia do arquivo, armazenamento e rotação/recuperação de
   chave; não copiar credenciais para o manifesto ou documentação.
4. Implementar snapshots consistentes e restore por componente com testes
   isolados em MariaDB/MongoDB, incluindo falha intermediária e idempotência.
5. Expor cobertura e última execução por componente na UI/API; não confundir
   sincronização Supabase de subconjuntos com backup integral.
6. Manter os testes de restore isolado e homologação em regressão contínua. A
   correção de coleção customizada está validada e publicada em 12.1.29. O ponto
   de partida desta release era 12.1.28. Esta auditoria não declara cobertura
   integral; instalação, host Linux nativo e produção não foram homologados por
   esta sessão.

## Reauditoria do lifecycle por handles — 2026-10-24 11:20 BRT

Duas revisões independentes e inspeção da fonte confirmaram que `BackupRestoreDirectory` não está integrado ao `FileSystemRestoreTransaction`: apply, rollback, replay e cleanup ainda usam operações `File`/`Directory` por pathname. A origem ZIP e verificações de journal/arquivo também reabrem nomes. A política aprovada de ACL/owner — identidade do serviço e root/administradores, falha fechada para caminhos compartilhados ou resultado inconclusivo — não está implementada. As primitivas Linux incluem pares de consulta/mutação por nome que ainda precisam de garantias atômicas ou de autoridade por handles.

`dotnet test tests/Jellyfin.Server.Implementations.Tests/Jellyfin.Server.Implementations.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~FullSystemBackup --logger "console;verbosity=minimal"` recompilou e passou 91 testes, ignorou 10 casos Linux/macOS e falhou 0/101 no Windows/.NET 10. Nenhum teste Linux nativo, ACL efetiva, corrida adversarial integrada, restore real, runtime instalado ou produção foi executado. O helper tem testes de primitivas isoladas; não provam segurança do lifecycle completo. Homologação formal permanece 2/7 (28,6%), sem crédito. Sem mudança de runtime ou release. Mapa técnico conferido; módulos, APIs, dependências e fluxo não mudaram nesta sessão.

Próximo gate P0: validar a fronteira de confiança por handle em todas as raízes, ancestrais, origem ZIP e journal; depois integrar o lifecycle completo por handles e cobrir falhas/races em Windows e Linux nativos. Até esses gates passarem, não considerar restore seguro nem promover homologação.

### ACL do instalador — teste em fixture Windows — 2026-10-24 11:25 BRT

`powershell -NoProfile -File scripts/tests/ServiceDataAcl.Tests.ps1` passou em fixture temporária: grants amplos herdados e explícitos foram removidos da raiz isolada, herança prevista dos filhos foi preservada e sentinel externo permaneceu intacto. Esse teste valida o contrato do instalador, não ACL/owner por handle nas raízes reais de restore. A decisão de ACL do restore continua sem implementação. Pesquisa da API Windows confirmou que o .NET 10 oferece `GetAccessControl` para `FileInfo`, `DirectoryInfo` e `FileStream`, não para `SafeFileHandle`; a checagem do objeto aberto requer API nativa por handle e `READ_CONTROL` desde a abertura. No Linux, UID/ACL devem ser lidos no objeto aberto (`statx`/`libacl`); política de ancestrais deve distinguir permissão de traversal da permissão de substituir componentes. As hipóteses nativas ainda precisam implementação e testes executados em cada SO. Homologação 2/7, sem crédito.

### Regressão completa de Server Implementations — 2026-10-24 11:30 BRT

`dotnet test tests/Jellyfin.Server.Implementations.Tests/Jellyfin.Server.Implementations.Tests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"` terminou com exit 0: 1.609 aprovados, 76 ignorados, 0 falhas (1.685). Build Release recompilou dependências e a suíte incluiu testes do helper de restore. Casos Linux foram ignorados neste host Windows; testes opt-in também permanecem sem infraestrutura própria. O resultado é regressão automatizada do assembly, não homologação do restore: não prova ACL efetiva, integração do helper em apply/rollback/replay/cleanup, execução Linux nativa ou segurança do runtime instalado. P0 permanece aberto e homologação formal segue 2/7 (28,6%), sem crédito; sem release ou publicação remota.

## Fontes primárias

- `Jellyfin.Server.Implementations/FullSystemBackup/BackupService.cs`
- `Jellyfin.Server.Implementations/FullSystemBackup/BackupManifest.cs`
- `Jellyfin.Server.Implementations/Extensions/ServiceCollectionExtensions.cs`
- `MulletaFlix.Plugin.IntroSkipper/PluginServiceRegistrator.cs`
- `MulletaFlix.Plugin.IntroSkipper/Db/IntroSkipperDbContext.cs`
- `MulletaFlix.Plugin.IntroSkipper/Db/DetectionCacheDbContext.cs`
- `Jellyfin.Server.Implementations/Nebula/NebulaMongoContext.cs`
- `Jellyfin.Server.Implementations/Nebula/NebulaSupabaseSyncService.cs`
- `tests/Jellyfin.Server.Implementations.Tests/FullSystemBackup/BackupServiceTests.cs`

## Deadlock em imagens: correção concorrente no processo — 2026-10-24 00:44 BRT

`ItemPersistenceService.SaveImagesAsync` agora usa a mesma faixa estática de `SemaphoreSlim` da gravação de metadados; `DeleteItem` descobre os IDs afetados antes da transação, adquire faixas em ordem crescente, revalida dentro da transação e libera/recomeça sem mutar quando descobre novas faixas. `SaveItems` também protege `ParentId` e `OwnerId` para impedir inserção concorrente de descendentes dentro do mesmo processo. Escritas da imagem, metadados e exclusão passam pelos mesmos locks de 16 faixas.

Verificações em Windows/.NET 10 Release: `ItemPersistenceServiceTests` 27/27; `ServerPersistenceMariaDbTests` 8/8 contra MariaDB 11.4.4 descartável em loopback `127.0.0.1:33099`, incluindo 12 refreshes de imagem simultâneos, refresh versus exclusão e `SaveItems` versus refresh. O servidor MariaDB foi encerrado e a porta fechou; a pasta de dados gerada em `%TEMP%` permanece porque a política da ferramenta recusou remoção recursiva. Porta 3306 nunca foi consultada.

Limites: `SemaphoreSlim` coordena apenas um processo. A revisão independente confirmou esse limite; teste multi-processo não foi executado. O log não identifica processo/assembly, então a causa é compatível com a falta desses locks em v12.1.35, não correlacionada conclusivamente. Não houve instalação, acesso ao banco de produção, atualização da versão, release ou envio remoto. Homologação formal permanece 2/7 (28,6%), sem crédito; gates de produção e restore TOCTOU/ACL P0 continuam abertos.

## Falha de restore após purga relacional — 2026-10-22

No checkout principal local baseado em `64a33dcf`, `RestoreBackupCoreAsync`
capturava exceções de `DbContext.Add`, registrava a entidade e seguia até
`SaveChanges`, permitindo que um backup inconsistente fosse tratado como restore
concluído parcialmente. A implementação local agora aborta com erro sanitizado,
usa transação relacional para histórico, purga e inserções, e envolve a instalação
de arquivos em staging/rename com rollback compensatório se qualquer etapa falhar.

Evidência: `FullSystemBackup` Release 41/41; `BackupRestoreMariaDbTests` 2
aprovados, 1 ignorado porque não há MariaDB descartável configurado; build Release
de `Jellyfin.Server` com 0 avisos e 0 erros. O cenário MariaDB de chave duplicada
foi adicionado, mas não executado em runtime. O teste de falha em instalação de
arquivo valida a reversão do arquivo anterior. Este patch está no checkout sujo
`64a33dcf` e não foi integrado, instalado nem publicado.

Limites: a compensação do filesystem é in-process e não resiste a crash abrupto;
TOCTOU de caminhos e restore multi-contexto/Mongo/Supabase permanecem fora deste
gate. T4.3 segue parcial; não declarar backup integral nem homologação concluída.

## Sincronização incremental Nebula/Supabase — 2026-10-21

A correção do cursor incremental em `NebulaSupabaseSyncService` usa um registro v2 de execução concluída como watermark e força sync integral sem checkpoint confiável. O horário inicial fica no campo existente `details`, preservando o `created_at` do banco e evitando migração remota. Isso protege o fluxo delta Mongo→Supabase contra avanço por lote parcial; não amplia a cobertura do FullSystemBackup, não copia contextos EF independentes nem coleções Mongo para o backup integral, e não comprova restore/recuperação. Os testes e limites finais constam em `docs/AUDIT-EVIDENCE.md` e na sessão do Vault.

## Escopo de contextos EF no FullSystemBackup — auditoria 2026-10-22

`BackupService` recebe apenas `IDbContextFactory<MulletaFlixDbContext>` e a criação/restauração enumera propriedades de `typeof(MulletaFlixDbContext)`. Os tipos `MoviesDbContext`, `SeriesDbContext`, `ChannelsDbContext` e `BooksDbContext` são separados; `InitializeDomainSchemasAndMigrateLegacyMedia` e `DomainDataMigrator` criam/preenchem suas tabelas a partir do legado. Esses quatro modelos não entram como snapshots independentes em `Database/` nem são restaurados pela transação EF central.

`rg -l` no código C# encontrou referências a esses modelos apenas nas definições, registro de serviços, migrações e `DomainDataMigrator`; não apareceu escritor de API/serviço além da migração. Portanto há uma lacuna comprovada de cobertura para os modelos separados, mas perda de dados ativa em produção não está demonstrada. Antes de usar esses modelos como fonte operacional, definir propriedade dos dados, adicionar captura/restauração de cada contexto e testes de round-trip/rollback.

## Manifesto relacional e relatório de reprodução — correção local 2026-10-22

Nova leitura confirmou um defeito de compatibilidade no manifesto: a criação gravava `Type.Name` do objeto `DbSet<T>` (`DbSet`1), enquanto os arquivos ZIP usam o nome físico mapeado pelo modelo EF. Um teste revelou ainda a exceção explícita `PlaybackReports` → tabela/arquivo `PlaybackReport`. Agora a geração, validação do manifesto/entradas, validação JSON e restauração usam a mesma lista de nomes físicos mapeados; `HistoryRow` é incluído. Manifestos legados que declaram `DbSet`1` são aceitos somente se todas as entradas físicas esperadas existirem.

A comparação dos `DbSet` de `UsersDbContext` contra o contexto central encontrou `PlaybackReports` como o único conjunto exclusivo. O serviço `PlaybackReportManager` o utiliza; foi adicionado `DbSet<PlaybackReport>` ao `MulletaFlixDbContext`, e o teste de modelo EF confirma que a entidade está incluída. O backup e restore continuam usando o nome físico singular `PlaybackReport`; o teste MariaDB agora insere, remove e recupera um relatório em round-trip.

Verificações em 2026-10-22: `BackupServiceTests` 34/34; `BackupRestoreMariaDbTests` 2 aprovados e 1 ignorado. O opt-in `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING` está ausente, então o novo round-trip MariaDB compila, mas não foi executado. Não houve acesso ao banco operacional, instalação ou produção. SRV-H02 continua em andamento e a homologação formal permanece 1/7 (14,3%); não há crédito parcial.

Limites: os modelos Movies, Series, Channels e Books continuam fora do snapshot relacional e precisam de decisão/cobertura antes de serem fontes operacionais. A discrepância do relatório histórico 6/6 ainda não foi rastreada. Checkout principal sujo, 8 commits atrás de `origin/main`; sem integração, CI remota, pacote ou release.

### Reconciliação da evidência MariaDB — 2026-10-22

A evidência anterior do portal atribuía round-trip/rollback e cobertura de cinco contextos a `BackupRestoreMariaDbTests 6/6`. A inspeção do código atual localizou uma única fábrica `IDbContextFactory<MulletaFlixDbContext>` em `BackupService`; o teste de integração usa essa mesma fábrica, sem registrar ou verificar os quatro contextos de domínio independentes. O resultado histórico do servidor descartável não foi reexecutado nesta sessão e não sustenta a afirmação de cobertura multi-contexto. SRV-H02 fica em andamento até recuperar o commit/relatórios que expliquem a discrepância e validar o escopo correto. A homologação formal recalculada é 1/7 (14,3%); nenhum dado operacional foi alterado.

### Implementação local de snapshot/restore multi-contexto — 2026-10-22

`BackupService` agora cria snapshots independentes para Movies, Series, Channels e Books e, no restore, associa os contextos à conexão primária e à transação relacional única. Foi mantido o contrato público `IMulletaFlixDatabaseProvider.PurgeDatabase(MulletaFlixDbContext, ...)`; a capacidade genérica foi adicionada em interface opt-in e validada antes da instalação de arquivos, apenas para restores que declaram contextos de domínio. A criação de backup não exige essa capacidade.

A validação prévia lê os registros e detecta chaves primárias e índices únicos EF não filtrados duplicados. Restrições FK são verificadas pelo banco em `SaveChanges`, não antes da alteração temporária dos arquivos. As leituras dos contextos para criação do snapshot ocorrem sequencialmente e ainda não têm isolamento temporal coordenado. O rollback de arquivos é compensatório no processo e não sobrevive a encerramento abrupto; não há journal/recuperação crash-safe. **Este parágrafo registra o estado na implementação inicial de snapshot/restore e foi superado pela seção “Journal e replay local após interrupção do restore”, abaixo.**

Verificações locais em 2026-10-22: `BackupServiceTests` 37/37; build `Jellyfin.Server` com 0 avisos e 0 erros; `BackupRestoreMariaDbTests` 2 aprovados e 1 ignorado por ausência de `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING`; `git diff --check` aprovado. O teste MariaDB que executaria round-trip/falha em domínio foi compilado, não executado. O listener operacional MariaDB local não foi usado. Alterações locais não integradas nem liberadas; formal 1/7 (14,3%), SRV-H02/H03 em andamento.

### Regressão de contexto pooled durante restore — 2026-10-22

A execução em MariaDB 11.4.4 descartável reproduziu `ObjectDisposedException`: `BackupService` reatribui temporariamente contextos de Movies/Series/Channels/Books à conexão do contexto central para compartilhar a transação. Como essas instâncias vinham de `AddPooledDbContextFactory`, o contexto reconfigurado podia voltar ao pool e ser alugado com conexão descartada após uma falha relacional. As quatro fábricas agora usam `AddDbContextFactory` sem pool; outras fábricas pooled não mudaram.

Verificação após a correção: `BackupRestoreMariaDbTests` 3/3 (inclui round-trip e rollback nos contextos relacionais); grupo unitário `FullSystemBackup` 46/46. O MariaDB de teste foi criado num datadir temporário novo, porta loopback aleatória 47251; a porta operacional 3306 não foi conectada. O processo descartável foi parado. Seu diretório temporário permanece em `%TEMP%` porque a remoção recursiva foi recusada pela política da ferramenta.

Limites: testes locais não alteram os gates formais; H02/H03 seguem abertos e homologação formal 1/7 (14,3%). Permanecem crash recovery/journal durável, pré-validação de FK, TOCTOU, consistência temporal do snapshot, CI remota e aceite formal. Código não integrado, sem release, instalação ou produção.

### Revalidação do histórico e risco de interrupção abrupta — 2026-10-22

Busca somente leitura nas branches e histórico Git locais não encontrou evidência rastreável para `BackupRestoreMariaDbTests 6/6` nem execução que comprovasse os cinco contextos. A cópia em `HEAD` contém um caso de round-trip do contexto central e dois casos de validação de connection string; o `6/6` próximo nas evidências é de `UpdateInfoControllerTests`. O teste multi-contexto atual está no working tree local e sua execução 3/3 em MariaDB descartável está documentada na sessão `2026-10-22-MulletaFlix-domain-context-pooling-restore`; isso não valida retroativamente o número 6/6. A alegação antiga permanece sem crédito até surgir o relatório original.

Revisão estática do restore confirmou um risco ainda aberto: `FileSystemRestoreTransaction` depende de rollback em memória/`Dispose`, enquanto o commit relacional ocorre antes de `fileRestore.Commit()`. Uma queda do processo no intervalo pode deixar banco e arquivos em estados diferentes; nesta data ainda não havia journal durável nem reconciliação no startup. Não houve mudança de código nem execução de testes nessa revalidação. **Estado histórico superado pela seção “Journal e replay local após interrupção do restore” abaixo:** o journal/replay já existe no working tree, mas faltam kill/restart real e MariaDB descartável. H02/H03 e homologação formal permanecem 1/7 (14,3%); sem release, instalação, produção ou alteração de banco.

### Journal e replay local após interrupção do restore — 2026-10-22

Implementação local: `BackupRestoreRecoveryJournal` grava atomicamente em ProgramData a identidade da operação e o caminho/tamanho/data/SHA-256 do ZIP, fora de todos os diretórios substituídos. `Program.StartApp` verifica e reaplica o mesmo arquivo antes de inicializar o host principal; se o arquivo estiver ausente/alterado ou o journal inválido, o startup falha fechado e preserva o journal. O restore com journal pendente bloqueia outra operação. Arquivos temporários e cópias `.bak`/marcadores `.absent` agora têm nomes determinísticos vinculados à operação; a reaplicação termina a troca de arquivos sem depender da memória do processo. Se a linha de comando solicitar o mesmo backup já recuperado, o startup não o aplica duas vezes.

Verificações locais em 2026-10-22: casos unitários de restore 41/41, incluindo conflito `.bak` + `.absent` falhando fechado e preservando alvo/sidecars/journal; conjunto `Jellyfin.Server.Implementations.Tests` 1.541 aprovados, 66 ignorados, 0 falhas (1.607 total). `dotnet format whitespace --verify-no-changes` nos quatro arquivos alterados e `git diff --check` aprovados. Os testes representam estados de arquivos/journal após interrupção, não encerram e reiniciam o processo real. Sem integração MariaDB nesta rodada, CI remota, instalação, produção, release ou aceite formal; homologação continua 1/7 (14,3%), sem crédito por teste local.

Decisão de arquitetura: para esta recuperação, replay idempotente do ZIP imutável foi escolhido em vez de adicionar marcador/tabela transacional específico a cada provedor de banco. A transação relacional existente continua atômica; reexecutar o mesmo snapshot converge tanto quando o commit anterior ocorreu como quando não ocorreu. Essa decisão revisa a proposta anterior de marcador como pré-condição, mas não elimina a necessidade de executar a matriz de crash/restart em ambiente descartável nem os gates de TOCTOU, integridade física do journal, consistência temporal e validação de FK. O recovery acontece antes do host principal; o listener de setup é criado antes desse ponto e não é coberto por esta garantia.

Portal de homologação atualizado em `Mulletaflix-Portal:main`, commit `6dfb68f`; contrato 228/228, JSON válido e endpoint público `/homologacao-status.json` HTTP 200 confirmou o evento `server-fullsystembackup-crash-recovery-replay-2026-10-22-0404`, estado `in_progress` e formal 1/7 (14,3%). Nenhum crédito formal por testes locais.

### Auditoria adversarial TOCTOU do restore — 2026-10-22

O código continua verificando contenção/reparse points e depois operando pelos nomes absolutos. `EnsureDirectoryExists` faz `Directory.CreateDirectory` entre verificações; `ApplyEntryAsync` grava staging com `FileStream` e substitui por `File.Move`/`File.Delete`. Um concorrente com permissão para renomear diretórios pode trocar um ancestral após a verificação; o teste existente cobre somente link preexistente. A segurança contra essa corrida não está comprovada.

Nesta sessão, a suíte Release filtrada `FullyQualifiedName~FullSystemBackup` recompilou e passou 50/50. O resultado é evidência funcional, não teste de corrida nem homologação formal. O próximo teste deve posicionar uma troca de namespace por um seam determinístico imediatamente antes do staging e assegurar que não haja escrita no destino externo.

Revalidação em 22/10/2026 às 09:58 (-03): o mesmo filtro Release foi executado novamente nesta sessão e passou 50/50, zero ignorados/falhas. Isso confirma apenas a regressão unitária atual; os testes de recuperação ainda constroem estados de journal/sidecar no disco e não encerram/reiniciam um processo real. Nenhum banco operacional, runtime instalado ou produção foi acessado. Homologação formal permanece 1/7 (14,3%).

Linux oferece `openat2` com resolução sob dirfd e bloqueio de symlinks como caminho candidato. No Windows, `NtCreateFile` documenta uso user-mode e nome relativo a `RootDirectory`; a combinação com `OBJ_DONT_REPARSE` não está clara na página específica, e APIs `winternl.h` são classificadas pela orientação geral como internas/sujeitas a mudanças. A opção não foi aprovada como garantia isolada; ainda é preciso desenho compatível e teste em Windows/filesystems cobertos. Não houve mudança runtime, MariaDB, instalação, produção ou release. Formal permanece 1/7 (14,3%).

### Revalidação das primitivas documentadas para TOCTOU — 2026-10-22

Pesquisa em documentação oficial refinou a decisão anterior. Linux `openat2` existe desde o kernel 5.6 e fornece `RESOLVE_BENEATH`/`RESOLVE_NO_SYMLINKS`; um desenho completo ainda precisa manter todas as operações de criação, rename e remoção relativas a descritores e definir comportamento em kernels/filesystems sem suporte. No Windows, `CreateFile` permite abrir diretórios e `SetFileInformationByHandle` permite rename relativo a um handle de destino, mas essas fontes não documentam abertura/criação genérica de filhos relativamente ao handle da raiz. `NtCreateFile` documenta `RootDirectory`, porém a Microsoft classifica APIs expostas por `winternl.h` como internas e sujeitas a mudanças. Portanto, nenhum desses fatos isolados comprova um contrato multiplataforma suportado para a transação inteira; implementação fica pendente de desenho e teste determinísticos.

A busca por padrão equivalente encontrou também `MediaBrowser.Providers/Lyric/LyricManager.cs`: o caminho de `UploadLyrics` passa por checagens de contenção/reparse point e depois grava via `Directory.CreateDirectory`/`FileStream` absolutos. Isso é uma ocorrência semelhante, não prova de impacto equivalente; requer análise própria de origem, diretório configurável e permissões antes de uma correção compartilhada.

Não houve alteração de código nem testes nesta revalidação documental. A revisão do mapa técnico confirmou os mesmos módulos e contratos; não houve mudança estrutural. Homologação formal permanece 1/7 (14,3%); sem instalação, produção, CI remota ou release.

Fontes oficiais: [Linux `openat2(2)`](https://man7.org/linux/man-pages/man2/openat2.2.html), [Microsoft — Directory Handles](https://learn.microsoft.com/en-us/windows/win32/fileio/obtaining-a-handle-to-a-directory), [Microsoft — `SetFileInformationByHandle`](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-setfileinformationbyhandle), [Microsoft — `NtCreateFile`](https://learn.microsoft.com/en-us/windows/win32/api/winternl/nf-winternl-ntcreatefile) e [Microsoft — Calling Internal APIs](https://learn.microsoft.com/en-us/windows/win32/devnotes/calling-internal-apis).

#### Correção da interpretação Windows — 2026-10-22

A frase anterior de que `NtCreateFile` seria apenas uma API interna foi ampla demais. A página Microsoft Learn de `NtCreateFile` (cabeçalho `winternl.h`) documenta a função como equivalente user-mode de `ZwCreateFile` e aceita nome relativo a um `RootDirectory` obtido por chamada anterior. Separadamente, a referência de `OBJECT_ATTRIBUTES` documenta `OBJ_DONT_REPARSE` como bloqueio de reparse durante a resolução do nome. Porém, a página específica de `NtCreateFile` restringe `Attributes` a zero ou `OBJ_CASE_INSENSITIVE`, sem explicar a combinação com `OBJ_DONT_REPARSE`; a página geral “Calling Internal APIs” também alerta que APIs expostas por `winternl.h` são internas, podem mudar e devem ser evitadas em favor de equivalentes públicos. Portanto, a classificação correta é: `NtCreateFile` está documentada para user-mode, mas a combinação e garantia de segurança requeridas não estão suficientemente claras para aprovação como contrato estável. Isso não prova que a combinação falha ou não seja suportada. A decisão de não adotá-la permanece, agora por ambiguidade documental e compatibilidade, não por ausência de documentação user-mode.

Fontes oficiais: [Microsoft — `NtCreateFile`](https://learn.microsoft.com/en-us/windows/win32/api/winternl/nf-winternl-ntcreatefile), [Microsoft — `OBJECT_ATTRIBUTES`](https://learn.microsoft.com/en-us/windows/win32/api/ntdef/ns-ntdef-_object_attributes), [Microsoft — Calling Internal APIs](https://learn.microsoft.com/en-us/windows/win32/devnotes/calling-internal-apis).

### Contrato candidato revisado — 2026-10-23

Pesquisa oficial adicional distinguiu as operações Windows: `NtCreateFile` documenta nome simples relativo a `RootDirectory`, `FILE_OPEN_REPARSE_POINT` impede o processamento normal do reparse point final, e `FILE_RENAME_INFO.RootDirectory` permite nome de destino relativo ao handle da pasta. Não usar `OBJ_DONT_REPARSE` como premissa: a documentação geral o descreve, mas a página específica de `NtCreateFile` não esclarece inequivocamente a combinação user-mode necessária. Abrir um componente por chamada, inspecionar atributos do handle e rejeitar reparse points evita depender dessa combinação para filhos; ainda há risco de compatibilidade por `winternl.h`, que precisa de validação Windows e fallback fail-closed.

No Linux, selecionar caminhada componente a componente via `openat` com `O_DIRECTORY | O_NOFOLLOW`, mais `mkdirat`, `renameat` e `unlinkat` relativos aos descritores. `openat2` não será requisito: requer kernel 5.6+, enquanto os pacotes listam Ubuntu 20.04, cujo kernel GA pode ser 5.4. Assim, compatibilidade não depende de syscall recente. Operações relativas a handles protegem contra resolução por pathname, mas não tornam restore inteiro atômico; ACLs devem impedir atores não confiáveis de renomear os ancestrais abertos. Nenhum backend foi implementado ou aprovado nesta sessão.

Fontes oficiais: [Microsoft — `NtCreateFile`](https://learn.microsoft.com/en-us/windows/win32/api/winternl/nf-winternl-ntcreatefile), [Microsoft — `OBJECT_ATTRIBUTES`](https://learn.microsoft.com/en-us/windows/win32/api/ntdef/ns-ntdef-_object_attributes), [Microsoft — `FILE_RENAME_INFO`](https://learn.microsoft.com/en-us/windows/win32/api/winbase/ns-winbase-file_rename_info), [Microsoft — `SetFileInformationByHandle`](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-setfileinformationbyhandle), [Linux man-pages — `openat2(2)`](https://man7.org/linux/man-pages/man2/openat2.2.html), [`renameat(2)`](https://man7.org/linux/man-pages/man2/renameat.2.html), [`unlinkat(2)`](https://man7.org/linux/man-pages/man2/unlinkat.2.html).

### Reavaliação do padrão handle-relative já existente — 2026-10-22

O projeto já contém `MulletaFlix.Plugin.GetAvatar.Services.AvatarFileStore`, com operações ancoradas no handle de um diretório: Windows usa `NtCreateFile` para um único nome relativo e `SetFileInformationByHandle` para exclusão; Linux usa `openat`/`unlinkat`, valida identidade e exige diretório pertencente ao UID efetivo sem escrita de grupo/outros. Isso é evidência de código existente, não uma primitiva compartilhada pronta para restore.

Limites concretos: `AvatarFileStore` aceita somente um componente de nome, enquanto restore percorre diretórios aninhados, cria diretórios, faz rename para `.tmp`/`.bak`, recupera journal após reinício e remove sidecars. O delete Linux reabre e compara identidade antes de `unlinkat`, mas a entrada ainda pode mudar após a última comparação; os próprios testes cobrem uma troca durante callback, não corrida arbitrária após a comparação. No Windows, combinação `RootDirectory`/`OBJ_DONT_REPARSE` continua sujeita à ambiguidade documental descrita acima. Não copiar ou reutilizar a classe como solução completa sem estender operações, retenção de handles e política contra renomeação dos ancestrais.

Verificação local desta auditoria: `AvatarFileStoreTests` em Windows/.NET 10 Release: 4 aprovados, 3 ignorados (cenários Linux-only); `FullSystemBackup` Release: 53/53 aprovados. Nenhuma dessas execuções testa a corrida TOCTOU do restore. Próxima implementação precisa cobrir operação completa e journal/recovery em Windows e Linux, com teste determinístico de troca de ancestral; até lá o gate continua aberto. Sem código, instalação, produção ou release nesta sessão.

### Reauditoria independente do TOCTOU do restore — 2026-10-22

Em nova inspeção do checkout, a corrida permanece aberta em todo o ciclo: extração e criação de diretórios, staging, instalação, rollback, limpeza e operações do journal continuam resolvendo caminhos absolutos após verificações separadas. `IFileSystem` trabalha por pathname e não mantém handles relativos à raiz; `AvatarFileStore` mantém um handle, mas suporta apenas nomes simples e não oferece diretórios aninhados/rename/transação. Os testes de links existentes não reproduzem troca concorrente de ancestral. Duas revisões independentes chegaram à mesma conclusão. A suíte focal `FullSystemBackup` em Windows/.NET 10 Release foi executada novamente: 62/62 aprovados; ela não cobre a corrida. Próxima etapa válida: desenhar uma abstração completa baseada em operações relativas a handles, cobrindo árvore aninhada, staging, backup, instalação, rollback, journal e limpeza; provar teste determinístico em Windows e Linux antes de fechar o gate. Nenhuma solução multiplataforma foi aprovada nesta sessão. Homologação formal permanece 1/7 (14,3%); sem instalação, produção ou release.

### Recovery após encerramento do processo durante staging — 2026-10-22

Foi adicionada regressão adversarial a `BackupServiceTests`: um processo filho inicia restore com entrada ZIP de 64 MiB; `FileSystemWatcher` observa o staging e o teste encerra a árvore do processo; um segundo processo inicia recuperação do journal e verifica os bytes exatos do arquivo instalado e a remoção do journal/sidecars temporários. O teste é isolado em diretório temporário e não usa banco operacional.

`RestoreBackupAsync_RecoversAfterRestoreProcessIsTerminated` passou 3/3 no Windows, Release (1 execução com build, 2 sem build). Isso prova o cenário do teste no filesystem local usado, não teste com MariaDB, Linux, runtime instalado, produção ou CI remota. TOCTOU, validação de FKs, consistência temporal e aceite formal permanecem pendentes; gate formal segue 1/7. Sem release.

### Reexecução focal do replay de restore — 2026-10-22

Nesta sessão, os filtros Release `RecoverInterruptedRestoreAsync_ReplaysSameBackupAndCleansPartialFileSwap` e `RestoreBackupAsync_RecoversAfterRestoreProcessIsTerminated` passaram 2/2 após recompilação. A execução confirmou replay idempotente dos arquivos e recuperação após encerramento real do processo no cenário já coberto. Não há `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING` configurada nem cliente/servidor MariaDB local detectado; portanto, o ponto exato pós-commit relacional/pré-commit filesystem segue sem teste integrado. Inspeção do fluxo confirma que o startup reaplica o mesmo arquivo de backup quando o journal persiste; não foi reproduzida inconsistência, então nenhuma correção de runtime foi feita. H02 continua 1/7; sem alteração em produção/release/portal.

### Preflight de referências FK do restore — 2026-10-22

O preflight central e os quatro preflights de domínio agora comparam as chaves estrangeiras declaradas no modelo EF com as chaves principais contidas no próprio arquivo do backup. Referência inexistente produz `InvalidDataException` antes da aplicação dos arquivos ou da mutação relacional. A semântica MATCH SIMPLE de chave composta opcional é respeitada: nulo em coluna não anulável é rejeitado; qualquer nulo em componentes anuláveis torna a relação opcional. Propriedades shadow não representadas no JSON não podem ser validadas por esse mecanismo e falham fechadas quando uma FK interna ao conjunto exportado as usa.

`ImageInfo.UserId` tem setter privado e é FK; recebeu `[JsonInclude]` para preservar o valor no round-trip. `BackupServiceTests` passou 44/44 em Release. `BackupRestoreMariaDbTests` foi atualizado para esperar `InvalidDataException` no Episode/Season inválido, mas não foi executado: `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING` não está configurada. Também não foi executada a suíte completa de Implementations após a alteração. A prova unitária exercita referência opcional central e round-trip ImageInfo; não comprova runtime de todos os relacionamentos (39 FKs auditadas). Sem banco operacional, instalação, produção, release ou homologação formal.

### Triagem de segurança do upload de letras — 2026-10-22

`LyricsController.UploadLyrics` exige `Policies.LyricManagement` (`EnableLyricManagement`) e resolve `Audio` com o usuário autenticado; `LibraryManager.GetItemById<T>(id, user)` retorna `null` para item invisível. A API rejeita separadores, `..` e extensões fora de lrc/elrc/txt. `LyricManager.TrySaveLyric` deriva o destino do caminho do item e da extensão validada; o nome fornecido pelo cliente não escolhe o caminho final. Não foi identificado escape remoto controlável via nome de arquivo.

Risco residual distinto: `IsPathWithinDirectory` valida contenção e reparse points abaixo da raiz, depois a gravação reabre paths absolutos por `Directory.CreateDirectory` e `FileStream`. Um processo local que já possa modificar a árvore pode tentar trocar um componente entre checagem e abertura. A raiz e seus ancestrais não são verificados pelo helper; uma raiz configurada como link depende de configuração local e não foi auditada em produção. Não bloquear raízes com link sem contrato de compatibilidade nem declarar a corrida resolvida por uma rechecagem de caminho.

Testes Release: `LyricManagerPathSafetyTests` 20/20 e `LyricsControllerTests` 10/10, zero ignorados/falhas. Cobrem traversal e links estáticos descendentes; não cobrem troca concorrente nem ACL/runtime instalado. Sem alteração de runtime, instalação, produção, release ou gate formal. Um teste determinístico da corrida requer seam explícito; solução efetiva requer desenho de operações relativas a handle/descritor compatível com plataformas/filesystems suportados.

### Crash/restart após commit relacional no restore — 2026-10-22

Foi adicionado um seam interno, configurável somente pelo construtor interno de testes, após `transaction.CommitAsync()` e antes de `fileRestore.Commit()`. A construção pública usada pela aplicação não injeta callback e mantém o fluxo normal. `IntroSkipper.Integration.Tests` recebeu acesso interno restrito para executar o cenário.

`RestoreBackupAsync_ReplaysAfterProcessTerminationBetweenDatabaseAndFileCommit` usa MariaDB explicitamente configurada: cria schema aleatório, gera backup central e de domínio, altera o estado vivo, inicia restore em processo filho e aguarda o sinal posterior ao commit relacional. O processo pai verifica que a linha já foi commitada, mata o filho e inicia um novo processo chamando o mesmo caminho de recovery do startup; então verifica banco, arquivo, journal e sidecars.

Verificação em Release: `BackupServiceTests` 44/44. `BackupRestoreMariaDbTests` compilou e executou 2 casos de validação de conexão; os 2 testes que exigem servidor descartável foram ignorados, pois `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING` não está configurada. Docker CLI e executável local `mariadbd`/`mysqld` não foram encontrados. Assim, a lógica do teste compila, mas a queda pós-commit e o replay contra MariaDB continuam não executados. O gate só deve ser habilitado com instância realmente descartável em loopback e porta não padrão; essa condição operacional não pode ser inferida apenas pela validação de endereço.

O mapa técnico foi atualizado para refletir o seam interno de teste e o teste de integração. Sem endpoint, dependência, mudança de DI ou alteração comportamental para a construção de produção. Sem banco operacional, instalação, produção, CI remota, release ou homologação formal; H02/H03 permanecem abertos e formal 1/7 (14,3%).

### CI MariaDB Linux para crash/replay do restore — 2026-10-22

O job `user-mariadb-linux-tests` agora restaura `IntroSkipper.Integration.Tests` e executa `RestoreBackupAsync_ReplaysAfterProcessTerminationBetweenDatabaseAndFileCommit` contra o serviço descartável MariaDB 11.4 já provisionado na porta 33099. Isso torna o cenário pós-commit relacional parte do CI, sem tocar no serviço MariaDB local/produção.

Verificações locais: contrato `ci-mariadb-linux-coverage.test.mjs` 2/2; o projeto de integração compilou e descobriu o teste, mas os dois casos que precisam de MariaDB foram ignorados sem `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING`. `git diff --check` passou. A execução remota do job ainda não foi observada; portanto, a queda/replay real e a homologação permanecem pendentes (1/7). Nenhuma release foi criada.

### Falha/ack ambíguo depois da tentativa de commit — 2026-10-22

Foi corrigida a divergência do processo atual quando `CommitAsync()` lança após o banco possivelmente ter confirmado ou uma etapa pós-commit falha. O restore preserva os arquivos instalados, sidecars e journal, marca reinício pendente e agenda parada do host. No próximo startup, o replay idempotente do mesmo ZIP converge filesystem e banco antes da inicialização principal. Se não for possível agendar a parada, o processo termina com código não zero para não continuar servindo estado potencialmente divergente.

Foi acrescentada regressão opt-in a `RestoreBackupAsync_RestoresDatabaseRowsOnDisposableMariaDb`: o seam envolve a chamada de commit, confirma a transação relacional e então lança antes de devolver o controle ao restore, simulando acknowledgement perdido. O teste verifica arquivos/journal/sidecars, sinalização de restart e aguarda/verifica `StopApplication()` antes de executar recovery subsequente. A suíte Release `FullyQualifiedName~FullSystemBackup` passou 53/53. O filtro `BackupRestoreMariaDbTests` compilou e descobriu 4 casos; 2 validações passaram e os 2 testes que exigem MariaDB descartável foram ignorados pela ausência de `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING`. Portanto, a nova regressão compila mas ainda não foi executada contra MariaDB; sem alegar validação runtime.

Sem alteração em banco operacional, instalação, produção, CI remoto ou release. Checkout principal segue sujo e 8 commits atrás de `origin/main`; mudanças preexistentes preservadas. Homologação formal segue 1/7 (14,3%), sem crédito por testes locais. O fluxo interno de `BackupService` mudou; sem nova API, dependência ou módulo.

### Identidade do ZIP aberto durante restore — 2026-10-22

Foi eliminada a divergência entre o arquivo validado/consumido e a identidade
gravada no journal. `RestoreBackupCoreAsync` abre o backup uma vez, valida sua
integridade usando esse stream e o reutiliza no `ZipArchive`; ao criar o journal,
`BackupRestoreRecoveryJournal` calcula SHA-256 e tamanho do mesmo handle. A
verificação posterior continua comparando essa identidade ao caminho usado pelo
replay e falha fechada se o caminho tiver sido substituído. Isso cobre a troca
atômica do pathname entre abertura e criação do journal; não resolve TOCTOU na
árvore de destino durante extração nem substituição após a validação final do
caminho.

Teste adversarial `RecoveryJournal_CreateAsync_UsesIdentityFromAlreadyOpenArchiveWhenPathIsReplaced` abre A, substitui o pathname por B, confirma que o journal identifica A e que a verificação do caminho rejeita B. `FullSystemBackup` Release passou 54/54. `BackupRestoreMariaDbTests` compilou: 2/2 testes de configuração passaram e 2 cenários que exigem servidor descartável foram ignorados porque `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING` não está configurada. Não houve execução MariaDB, CI remota, produção, instalação ou release. Homologação formal permanece 1/7 (14,3%).

### Regressão de fixture OTLP após journal de restore — 2026-10-22

A execução Release da solução identificou uma falha em
`ConfigureOpenTelemetry_ExportsBackgroundJobMetricsWithoutSensitiveData`: a
fixture criava `BackupService` com `IServerApplicationPaths` nulo, enquanto a
entrada do restore agora consulta o journal antes de rejeitar um caminho ZIP
ausente. A fixture passou a fornecer um `ProgramDataPath` temporário único; o
teste voltou a observar `FileNotFoundException`, sem alterar o comportamento de
produção. O teste isolado passou 1/1, `Server.Tests` 74/74 e `FullSystemBackup`
54/54. Após corrigir a fixture, a solução completa Release terminou exit 0:
5.180 aprovados, 237 ignorados e 0 falhas/5.417 casos. Os opt-ins de MariaDB e
os testes Linux-only continuam ignorados, portanto este resultado não comprova
esses ambientes nem conclui homologação formal.

### Replay não trunca staging hard-linked — 2026-10-22

No replay, o arquivo `.tmp` determinístico pode existir por causa de uma
interrupção anterior. A rotina agora remove esse nome sem abri-lo e cria novo
temporário com `FileMode.CreateNew`; não trunca inode que tenha outro nome por
hard link. O ZIP verificado pelo journal é reextraído, e o `.tmp` parcial é
descartável. Teste adversarial prepara hard link para sentinel fora da pasta,
roda `RecoverInterruptedRestoreAsync` e valida sentinel intacto e conteúdo
restaurado. Teste focal 1/1; `FullSystemBackup` Release 55/55.

Limite: operações seguem pathname e janela TOCTOU entre verificar e operar
permanece. Não valida concorrência, Linux, ACL efetiva, MariaDB ou produção.

### Symlink estático no staging de replay — 2026-10-22

Regressão adicional cria um symlink preexistente no nome determinístico `.tmp`,
aponta-o para sentinel fora da raiz e executa o replay. `RecoverInterruptedRestoreAsync`
passou 1/1; o sentinel permaneceu intacto, o destino veio do ZIP validado e o
sidecar/journal foi removido. `FullSystemBackup` Release passou 56/56 em Windows,
.NET 10. O teste pode ser ignorado em hosts sem permissão/suporte a symlink; nesta
execução foi aprovado, não ignorado.

Esta evidência só cobre symlink estático já presente. Troca concorrente entre
validação, criação, backup, rename, rollback, limpeza e journal continua sem
proteção por handles/dirfd e sem testes determinísticos; Linux, MariaDB, ACL
efetiva, CI remota, runtime instalado e produção não foram verificados. Gate
formal segue 1/7 (14,3%); sem crédito formal ou release.

### Revisão adversarial da transação completa de restore — 2026-10-22

Revisão read-only independente confirmou que os testes estáticos de hard link e
symlink não fecham a corrida TOCTOU. Achados: (1) verificações de contenção e
reparse points por pathname podem ser invalidadas antes de criação, move,
rollback ou cleanup; (2) o nome `.tmp` pode ser trocado depois de fechar o
stream e antes do install; (3) o journal permite substituição do registro ativo
sem lease single-writer entre processos; (4) `VerifyArchiveAsync` reabre o
pathname, podendo verificar outro ZIP que não o stream já usado pelo restore.

O próximo recorte seguro é uma transação fail-closed completa: ancorar roots e
executar staging, backup, install, rollback, cleanup e replay do journal por
handles/dirfd; definir a política de renomeação dos diretórios ancestrais; e
testar trocas determinísticas, substituição do ZIP e admissões concorrentes em
Windows e Linux. O lease single-writer recomendado foi implementado depois como
barreira parcial (ver seção posterior), mas não impede troca do pathname. Não
foi escolhida uma implementação multiplataforma nesta revisão. Primitivas de diretório/rename
relativo documentadas não bastam, isoladamente, para garantir que nomes não
sejam trocados; no Windows a combinação user-mode `NtCreateFile`/flags e sua
estabilidade exigem validação adicional.

Verificação desta sessão: revisão independente sem edição e sem testes; único
check informado pelo revisor foi `git diff --check` em dois arquivos, sem erro.
Não executei testes/build nesta sessão. Arquivo-fonte atual foi inspecionado;
as referências detalhadas estão nos comentários do revisor para
`BackupService.cs` e `BackupRestoreRecoveryJournal.cs`. TOCTOU permanece P0
aberto; H03 não recebe crédito novo; formal 1/7 (14,3%). Sem instalação,
produção, CI remota ou release.

## Regressão da solução e ambiente NebulaPython — 2026-10-22

`dotnet test MulletaFlix.sln --configuration Release --no-restore --logger
"console;verbosity=minimal"` terminou com exit code 0. Assemblies focais:
Providers 848/848; API 763 aprovados/3 ignorados; Server Integration 3
aprovados/142 ignorados; Server unit 74/74; Server Implementations 1.552
aprovados/66 ignorados; Model 685/685; Extensions 144/144; Networking
124/124; Controller 63/63; LiveTv 75/75; Naming 681/681; MediaEncoding
71/71; HLS 18/18; Common 31/31; XbmcMetadata 39/39. A execução manteve
avisos existentes. Os skips incluem bancos opt-in e pré-condições de
plataforma; não comprovam MariaDB, MongoDB, Linux, runtime instalado ou
produção.

A suíte `py -3.14 -m pytest tests -q`, no diretório `Tools/NebulaPython`, passou
685/685. A tentativa `py -3.10 -m pytest tests -q` abortou durante import/coleta
porque `main.py` exige runtime Nebula isolado e a venv suportada pelo instalador
não está preparada. Foram executados zero testes em 3.10; nenhum pacote foi
instalado globalmente e nenhum Mongo operacional foi acessado. Repetir em 3.10
após preparar a venv suportada. Isso não altera o status dos gates:
homologação formal permanece 1/7 (14,3%), sem crédito parcial, aceite de
produção ou release.

## Revalidação focal e primitivas de caminho relativo — 2026-10-22

`dotnet test tests/Jellyfin.Server.Implementations.Tests/Jellyfin.Server.Implementations.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~FullSystemBackup"` recompilou o projeto e passou 61/61 em Windows/.NET 10; compilação emitiu warnings existentes de documentação/analisadores. Os casos validam journal, identidade do ZIP, sidecars e lease cooperativo, mas não exercitam troca concorrente de diretório durante operações de restore.

Revisão das fontes primárias: Linux `openat2` oferece resolução restringida por `dirfd` (`RESOLVE_BENEATH`/`RESOLVE_NO_SYMLINKS`) desde Linux 5.6. A documentação alerta que proibir symlinks indiscriminadamente pode quebrar caminhos de sistema antes funcionais; `RESOLVE_NO_XDEV` também rejeita mounts/bind mounts. A política de compatibilidade para raízes de biblioteca, links e volumes ainda precisa ser definida/testada. No Windows, `OBJECT_ATTRIBUTES` documenta nome relativo a `RootDirectory` e `OBJ_DONT_REPARSE`; a página específica de `NtCreateFile` não inclui esse flag entre os valores de `Attributes`. A combinação user-mode necessária permanece ambígua e não foi aprovada como garantia de segurança.

Não foi encontrada infraestrutura local `mariadbd`, `mysqld`, Docker ou WSL/distro. Nenhum serviço de banco operacional foi acessado. Sem execução Linux ou MariaDB descartável, TOCTOU continua aberta e crash/replay pós-commit segue sem validação integrada. Homologação formal permanece 1/7 (14,3%); sem release, instalação ou produção.

Fontes primárias: [Linux man-pages — `openat2(2)`](https://www.man7.org/linux/man-pages/man2/openat2.2.html), [Microsoft Learn — `OBJECT_ATTRIBUTES`](https://learn.microsoft.com/en-us/windows/win32/api/ntdef/ns-ntdef-_object_attributes), [Microsoft Learn — `NtCreateFile`](https://learn.microsoft.com/en-us/windows/win32/api/winternl/nf-winternl-ntcreatefile).

A revisão também encontrou que o teste opt-in MariaDB limpava schema com o token
cancelável e criava diretórios antes do `try`. A limpeza agora usa
`CancellationToken.None` e a criação da raiz temporária está coberta pelo
`finally`; a limpeza do banco é tentada mesmo se a remoção local falhar. O
projeto de integração compilou e o filtro do caso terminou 0 aprovados/1
ignorado, explicitamente por falta de MariaDB descartável; format check do
arquivo e `git diff --check` passaram. Isso melhora a segurança da fixture, mas
não valida o cenário de crash contra MariaDB real.

### Restore pendente valida o stream que será reproduzido — 2026-10-22

`BackupRestoreRecoveryJournal.VerifyArchiveAsync` ganhou overload que calcula
SHA-256 e tamanho do stream seekable já aberto e restaura sua posição anterior.
`RestoreBackupCoreAsync` usa esse overload tanto para restore novo quanto para
journal pendente, antes de aplicar arquivos ou banco. No restore novo, também
confirma por pathname que o arquivo externo ainda corresponde ao journal. Isso
evita usar somente a identidade de outro arquivo que tenha ocupado o pathname
enquanto o stream original permanece aberto.

Regressões: a verificação do stream original passa mesmo após o pathname ser
substituído; um stream diferente é rejeitado embora o pathname continue correto;
mudança isolada de `LastWriteTimeUtc` não invalida conteúdo idêntico; a posição
original é preservada em sucesso e falha. O timestamp legado continua no JSON
do journal, mas não é parte da identidade de conteúdo. O filtro Release
`FullyQualifiedName~FullSystemBackup` passou 58/58 antes do ajuste final do
call-site do restore novo; a validação final está em execução. `dotnet format
whitespace --verify-no-changes` nos projetos inteiros encontrou violações de
formatação/codificação preexistentes em arquivos não relacionados; verificar os
arquivos afetados individualmente permanece pendente. `git diff --check` passou.

Limite à época desta verificação: o processo ainda depende do arquivo externo
para recovery após crash; uma alteração no mesmo inode depois de calcular o
hash, cópia privada durável, lease entre processos e corridas em
staging/rename/rollback/cleanup/journal não foram resolvidos. O lease
single-writer foi adicionado posteriormente na seção “Lease single-writer entre
processos” abaixo, sem fechar as demais corridas. O método por pathname permanece em preflight e adicionalmente
no restore novo para confirmar que o arquivo durável ainda corresponde ao
conteúdo journalado. Nenhum teste Linux/MariaDB, runtime instalado, produção ou CI remota
foi executado. TOCTOU global segue P0; formal 1/7, sem crédito ou release.

Revisão final: o call-site do restore novo também executa a validação do stream
aberto antes da validação path-only. Rebuild Release final da suíte
`FullyQualifiedName~FullSystemBackup`: 58 aprovados, 0 ignorados/falhas.
`dotnet format whitespace --verify-no-changes --no-restore --include` passou
nos dois arquivos de runtime e no arquivo de teste (execuções focadas).
A checagem ampla dos dois projetos continua falhando em arquivos não relacionados
do worktree. A busca `VerifyArchiveAsync(pendingRestore)` encontrou duas chamadas
path-only restantes: preflight de recovery e confirmação do journal recém-criado;
a validação stream agora precede esta última.
Portal: `Mulletaflix-Portal:main` commit `ef2ba98`; contrato 240/240 e JSON
válido. Endpoint público HTTP 200 confirmou o evento `server-restore-open-stream-identity-2026-10-22-1650` com `generatedAt=2026-10-22T16:50:43-03:00`.

### Lease single-writer entre processos — 2026-10-22

`BackupRestoreRecoveryJournal.AcquireOperationLease` abre `restore-operation.lock`
persistente em `ProgramData/restore-recovery` com `FileShare.None`. O restore
normal adquire o lease antes de ler o journal; a recuperação o adquire antes
de `ReadAsync` e o mantém durante verificação e replay. O arquivo não é apagado
ao liberar o handle. O gate por instância continua como proteção complementar.

`FullSystemBackup` Release passou 61/61 em Windows/.NET 10, incluindo teste
cross-process de exclusão/reabertura após fechamento e testes que confirmam que
restore e recovery falham ao tentar adquirir lease antes de ler journal
malformado. `dotnet format whitespace --verify-no-changes --no-restore --include`
passou nos dois arquivos de runtime e no teste; `git diff --check` passou.

Limites/revisão adversarial: exclusão depende dos processos cooperativos
resolverem o mesmo arquivo. Troca de diretório/arquivo ou reparse point por ator
com permissão de escrita no diretório não é mitigada por handle-relative; ACL
efetiva não foi inspecionada. A integração não prova a ordem completa do startup
real. O teste cross-process usa o filesystem Windows; WSL/Linux não está
disponível e o comportamento em mounts Linux permanece pendente. A auditoria
TOCTOU global continua P0 aberta por causa de operações path-based na árvore de
destino. Homologação formal 1/7 (14,3%), sem crédito local; sem instalação,
produção, CI remota ou release.

### Revalidação local do gate de backup — 2026-10-22

No checkout atual, `dotnet test tests/Jellyfin.Server.Implementations.Tests/Jellyfin.Server.Implementations.Tests.csproj --no-restore --configuration Release --filter FullyQualifiedName~FullSystemBackup` passou 61/61, 0 ignorados/falhas. O filtro `BackupRestoreMariaDbTests` compilou e terminou 2 aprovados e 2 ignorados: os cenários de restore/crash-replay que exigem MariaDB descartável não executaram porque `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING` não está configurada. O resultado confirma regressão unitária local, não o gate relacional nem homologação formal; TOCTOU, integração MariaDB, Linux, ACL/runtime instalado e aceite permanecem pendentes. Nenhuma release/instalação/produção foi feita.

### Revalidação do contrato de preflight FK — 2026-10-22

Foi adicionado teste de contrato que cria os cinco modelos EF em memória e compara as relações exportadas com os planos produzidos por `CreateForeignKeyValidationPlans`. Os modelos somam 39 FKs; 38 pertencem a entidades incluídas nos arquivos de backup e todas as 38 foram planejadas. `RestorePreflight_CreatesValidationPlanForEveryExportedForeignKey` passou 1/1; após recompilação Release (303 avisos existentes, 0 erros), o filtro `FullyQualifiedName~FullSystemBackup` passou 62/62.

O teste confirma escopo e cobertura do preflight, não que o banco relacional real aceite os dados nem que a transação MariaDB faça replay corretamente. MariaDB descartável não está configurado/disponível nesta sessão; Linux/WSL, CI remota, runtime instalado, produção e aceite formal não foram validados. Homologação formal permanece 1/7 (14,3%), sem release ou instalação. Portal `main` commit `ad42c884e0df2603f453b42c89a2201285174b01`; contratos 246/246, Vercel Production `READY`, endpoint público HTTP 200 confirmou o evento.

### Revalidação do risco TOCTOU no restore — 2026-10-23

No checkout atual, `dotnet test tests/Jellyfin.Server.Implementations.Tests/Jellyfin.Server.Implementations.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~FullSystemBackup" --verbosity quiet` passou 62/62, sem ignorados ou falhas. A revisão independente percorreu restore inicial, replay, staging, instalação, rollback, limpeza, journal e lease: as operações críticas ainda resolvem caminhos absolutos depois de validações separadas. A suíte não contém troca determinística de ancestral entre validação e operação.

Não existe abstração atual que cubra a transação completa com operações relativas a diretório/handles nos dois sistemas. WSL/Linux não está instalado, Docker CLI não está disponível e `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING` não está configurada; portanto, esta rodada não validou corrida adversarial em Linux nem integração MariaDB. Nenhuma mitigação parcial foi aplicada. O TOCTOU permanece P0 aberto; homologação formal segue 1/7 (14,3%), sem release, instalação ou acesso à produção.

### Auditoria de corridas de filesystem em outros fluxos — 2026-10-23

`FullSystemBackup` Release foi repetido nesta sessão e passou 62/62. A inspeção confirmou o risco em operações de restore/journal já descritas acima e ampliou o inventário: `LyricManager.TrySaveToFiles` valida caminhos e depois cria staging/move por pathname; `AvatarService.SetUserAvatarCoreAsync` e `RestoreFiles` fazem snapshot, gravação e rollback por caminho absoluto; `MidiaStorageOnlinePathGuard.WriteTextAtomicallyAsync` e o caminho de download verificam reparse points e depois movem/apagam por pathname. As checagens atuais reduzem ataques estáticos, mas não fixam a identidade dos ancestrais contra troca concorrente. Os testes encontrados cobrem links/hardlinks estáticos, não a corrida entre validação e operação. Não houve reprodução de exploração.

Pesquisa em fontes primárias: Linux `openat2` oferece restrições de resolução sob `dirfd`, enquanto `renameat2` e `unlinkat` operam relativamente a descritores de diretório ([openat2(2)](https://man7.org/linux/man-pages/man2/openat2.2.html), [renameat(2)](https://man7.org/linux/man-pages/man2/renameat.2.html), [unlinkat(2)](https://man7.org/linux/man-pages/man2/unlinkat.2.html)). Windows documenta abertura de reparse point e operações por handle, incluindo rename/delete, mas a pesquisa não confirmou uma primitiva pública Win32 para abrir/criar todo componente filho relativo ao handle pai; `NtCreateFile`/`RootDirectory` continua candidato nativo com risco de compatibilidade. Não foi escolhida uma implementação multiplataforma nem aplicada mitigação parcial. Referências e critérios estão em `01 Projetos/MulletaFlix/Decisões/2026-10-22-restore-handle-relative-primitives.md`.

Próximo gate: abstração comum com backends nativos, teste determinístico de troca de ancestral em restore, letras, avatar e sync/download, e execução Windows/Linux em filesystems suportados. O risco permanece P0; formal 1/7 (14,3%), sem release, instalação ou produção. WSL/Linux, Docker e MariaDB descartável continuam indisponíveis/não configurados nesta sessão.

### Revalidação do AvatarFileStore — 2026-10-23

`dotnet test tests/Jellyfin.Api.Tests/Jellyfin.Api.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~AvatarFileStoreTests --verbosity quiet` passou 4/4 em Windows/.NET 10; três testes Linux-only foram ignorados. O store aceita somente nomes simples e não oferece operações aninhadas/transacionais. A implementação Linux ainda revalida identidade e depois chama `unlinkat` pelo nome, portanto mantém janela concorrente. Não há evidência para generalizar a classe como mitigação do restore ou dos fluxos irmãos; nenhuma alteração de runtime foi feita. TOCTOU continua P0, formal 1/7, sem release/produção. WSL/Linux indisponível.

### Retry-After nas consultas do checkpoint Supabase — 2026-10-23

As consultas ao cursor `nebula_files` e ao histórico `nebula_backups` repetem respostas HTTP 429/5xx até três tentativas. Ambas agora respeitam `Retry-After` (delta ou data HTTP futura), limitado a 60 segundos; cabeçalho ausente/inválido/expirado usa fallback incremental de 250 ms por tentativa. Erros não transitórios continuam sem retry e o cancelamento interrompe a espera. `NebulaSupabaseSyncTests` passou 65/65 em Release, incluindo a espera real nos dois caminhos, fallback e limite. Este teste isolado não valida conectividade real, MongoDB/Supabase, Linux, instalação, produção nem homologação formal; esta permanece 1/7 (14,3%).

O mapa técnico foi conferido nesta sessão: sem novo módulo, API, classe ou dependência; o helper é interno ao serviço existente. O risco TOCTOU do restore continua aberto e não é coberto por esta alteração.

### Reauditoria de TOCTOU e permissões ancestrais — 2026-10-23

Dois exames independentes do working tree confirmaram que restore/replay/journal ainda fazem operações críticas por caminhos absolutos após validações separadas; os testes cobrem links estáticos e estados de recuperação, mas não trocam deterministicamente um ancestral entre validação e mutação. O exame de empacotamento confirmou que o instalador configura a ACL/permissão da pasta final e não audita a cadeia de ancestrais; `BackupPath` customizado e o diretório do journal não têm evidência de ACL efetiva instalada. São observações estáticas do repositório, não auditoria de uma máquina instalada.

A API candidata Linux `openat2` com `RESOLVE_BENEATH`/`RESOLVE_NO_SYMLINKS` permite desenho relativo a `dirfd`, mas não existe backend comum implementado nem validação Linux nesta sessão. A combinação Windows necessária para toda a transação continua sem contrato de compatibilidade suficientemente demonstrado; não se adotou API interna como correção. Nenhuma alteração de runtime foi feita, preservando as mudanças já presentes no checkout.

Regressão geral Release no Windows, com `FullyQualifiedName!~Integration`: 1.557 aprovados, 66 ignorados e 1 falha em 1.624 resultados. A falha `LocalizationManagerTests.GetParentalRatings_Default_Success` foi reproduzida isoladamente: esperado 56, obtido 42. A causa não foi atribuída; não a classificar como preexistente. Homologação formal permanece 1/7 (14,3%); nenhuma release/instalação/produção foi feita.

Próximo gate: corrigir/explicar a regressão de localização, desenhar operações relativas a handles para todos os passos do restore em Windows/Linux e incluir troca concorrente determinística, auditoria de ACL dos ancestrais e destinos customizados, depois executar em ambas as plataformas e com MariaDB descartável. O mapa técnico foi conferido e permanece sem mudança estrutural.

### Resolução da falha do teste de classificações — 2026-10-23

A falha anterior `LocalizationManagerTests.GetParentalRatings_Default_Success` (esperado 56, observado 42) foi explicada e corrigida no próprio teste. O caso antigo esperava as classificações dos EUA, mas configurava somente `UICulture=de-DE`; o servidor agora usa `MetadataCountryCode=BR` por padrão e corretamente retornava 42 classificações brasileiras. A regressão foi falsa para o comportamento de produção.

O teste foi renomeado para `GetParentalRatings_ConfiguredUsCountry_Success` e configura explicitamente `MetadataCountryCode="US"`; um teste adicional cobre o padrão BR e `Livre` com score 0. Reexecução Release no checkout atual: 67/67 aprovados, 0 ignorados/falhas. A suíte ampla Release não foi repetida nesta revalidação. Nenhum código de runtime mudou. O risco TOCTOU/ACL do restore segue P0 e não é resolvido por este teste; homologação formal permanece 1/7 (14,3%), sem release, instalação ou produção.

### Gate de restore relacional e crash replay em MariaDB descartável — 2026-10-23

`BackupRestoreMariaDbTests` foi executado contra MariaDB 11.4.4 inicializado em datadir temporário isolado, loopback `127.0.0.1:33099`; filtro Release passou 4/4, 0 ignorados/falhas. Os casos cobrem round-trip/rollback relacional multi-contexto, recuperação após commit confirmado mas acknowledgement perdido, crash real do processo entre commit relacional e commit dos arquivos seguido de replay, e rejeição de duas connection strings inseguras. A primeira rodada revelou que o teste de acknowledgement perdido chamava a recuperação no mesmo serviço que ainda continha o callback de falha; recuperação agora usa nova instância sem fault injection, equivalente ao restart. Também foi adicionada captura stdout/stderr no subprocesso para diagnosticar falhas de recovery. Build: 0 erros, 44 avisos de analisadores.

O banco de teste foi encerrado; não houve conexão ao MariaDB operacional porta 3306 nem MongoDB. H02 passa a concluído com evidência técnica; isto não certifica produção nem fecha H03: permanecem TOCTOU de pathname/troca concorrente, consistência dos domínios não relacionais, Linux/CI, instalação e aceite formal. Formal técnico agregado 2/7 (28,6%); nenhuma release/instalação/produção alterada. Dois diretórios temporários (~285 MiB) permanecem em `%TEMP%`, pois a remoção recursiva foi bloqueada pela política da ferramenta; não foi contornada.

### Reexecução após retomada — 2026-10-23

`dotnet test tests/Jellyfin.Server.Implementations.Tests/Jellyfin.Server.Implementations.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~FullSystemBackup` terminou com exit 0: 62 aprovados, 0 ignorados e 0 falhas em Windows/.NET 10. A reexecução confirma os testes focais do estado atual, mas não aumenta cobertura contra troca concorrente: `BackupService` e `BackupRestoreRecoveryJournal` ainda usam operações por pathname após validações separadas. Nenhum código de runtime foi alterado nesta sessão. `wsl --status` confirmou que WSL não está instalado; evidência Linux segue ausente. Homologação formal permanece 2/7 (28,6%), sem crédito novo; sem release, instalação ou produção.

### Protótipo de primitivas relativas — hardening incremental 2026-10-23

`BackupRestoreDirectory` continua sendo helper interno e não foi integrado ao restore ativo. `OpenRoot` agora percorre cada componente desde a raiz usando handle/dirfd e rejeita reparse point/symlink nos ancestrais: Windows usa `NtCreateFile` relativo a `RootDirectory` e mantém handles ancestrais sem compartilhamento de delete; Linux usa `openat(O_PATH|O_DIRECTORY|O_NOFOLLOW|O_CLOEXEC)`. Exclusão Linux consulta tipo com `statx(AT_SYMLINK_NOFOLLOW, STATX_TYPE)` e rejeita symlink, tipos especiais e tipo incompatível antes de `unlinkat`.

Validação Release Windows/.NET 10: `FullyQualifiedName~FullSystemBackup` passou 79 testes, 2 foram ignorados por serem Linux-only e 0 falharam (81 total). `dotnet format whitespace ... --verify-no-changes` e `git diff --check` passaram. WSL e Docker não estão disponíveis neste host; nenhum teste Linux nativo foi executado.

**Limites/gates:** há janela de troca entre `statx` e `unlinkat` por nome se um ator puder modificar o diretório aberto; a confiança/ACL da árvore continua sendo pré-condição não verificada. O helper não está conectado a `BackupService`, `FileSystemRestoreTransaction` nem ao journal/recovery: restore, replay, rollback e limpeza ativos ainda são baseados em pathnames e o TOCTOU P0 permanece aberto. Sem crédito novo de homologação (formal 2/7, 28,6%), release, instalação ou produção. Próximos gates: resolver/modelar a corrida de exclusão com fronteira ACL validada, integrar todas as fases e journal, testes adversariais/crash concorrentes e executar em Linux nativo e matriz Windows.

#### Auditoria do contrato de integração — 2026-10-23

Dois exames read-only confirmam que integração parcial seria insegura. `FileSystemRestoreTransaction` precisa migrar alvo, `.tmp`, `.bak`, `.absent`, rollback, commit, limpeza e diretórios criados como unidade; `BackupRestoreRecoveryJournal` precisa migrar lock, leitura, escrita atômica, substituição, exclusão e limpeza, preservando o lease durante replay. `CopyDirectoryAsync`/`RecoverInterruptedRestoreAsync` devem usar as mesmas autoridades abertas, sem fallback absoluto. O ZIP de origem é outra autoridade de leitura: o contrato atual abre e verifica o archive por pathname; definir explicitamente se fica fora da árvore de destino protegida ou também requer handle seguro.

Bloqueios adicionais do protótipo: Linux `CreateDirectory` faz `mkdirat` e reabre o nome, permitindo receber outra entrada se o pai for mutável; a verificação `statx`/exclusão `unlinkat` tem corrida de nome. `statx` foi introduzido no Linux 4.11 e o wrapper consta desde glibc 2.28; variantes musl do empacotamento não têm ABI testada. Windows depende de `NtCreateFile`/`NtSetInformationFile` e classes NT internas; compatibilidade por filesystem ainda não foi validada. A ACL confiável é pré-condição documentada, não verificada. Referência primária: [Linux `statx(2)`](https://man7.org/linux/man-pages/man2/statx.2.html).

Cobertura atual `FullSystemBackup` Windows/.NET 10 segue 79 aprovados, 2 ignorados Linux-only e 0 falhas; cobre primitivas isoladas, não o restore com helper. Formal 2/7 (28,6%), sem crédito novo. Gate antes da integração: fechar/modelar races com fronteira verificável, decidir o escopo de autoridade do ZIP, implementar todas as mutações e leitura do journal por handle, testar concorrência/crash/rollback/replay em Windows e Linux/glibc/musl. Sem release, runtime instalado ou produção.

### Varredura ampla de testes — 2026-10-23

`dotnet test MulletaFlix.sln --configuration Release --no-restore --no-build --verbosity quiet -m:1` percorreu os 17 projetos de testes no Windows/.NET 10. Todos os resumos individuais reportaram zero falhas: 5.236 aprovados, 252 ignorados, 5.488 no total. A sessão encerrou após o último projeto, mas o host não disponibilizou o código de saída agregado; portanto, o resultado é documentado pelos resumos individuais, sem declarar exit code global. Foram emitidos avisos de compilação/analisadores, não investigados nesta varredura.

A suíte Nebula Python passou 688/688 em Python 3.14 (`py -3.14 -m pytest -q`, 15,62 s); pytest já estava disponível nesse runtime, então nenhum pacote foi instalado. O Python 3.13 padrão ainda não tem pytest; a execução alternativa por unittest em 3.13 não é evidência da suíte completa. Nenhum acesso a MongoDB, Supabase ou produção foi feito. Esta evidência não altera a homologação formal 2/7 (28,6%) nem resolve os gates Linux, ABI musl/Windows filesystem ou a integração TOCTOU P0 do restore.

### Mapa do fluxo ativo e cobertura CI — 2026-10-23

Rastreio direto dos chamadores confirma três entradas para restore: API (`Jellyfin.Api.Controllers.BackupController`), recuperação no startup (`Jellyfin.Server.Program`) e rollback de migração (`Jellyfin.Server.Migrations.JellyfinMigrationService`). O startup executa `RecoverInterruptedRestoreAsync` antes de prosseguir para a solicitação de restore pendente. `BackupRestoreDirectory` não possui chamadores na implementação; portanto, seus testes isolados não exercitam a operação real.

No fluxo ativo, `BackupService.RestoreBackupCoreAsync` e `FileSystemRestoreTransaction` ainda fazem normalização/verificações de path e depois `FileStream`, `File.Move/Delete` e mutações de diretório por pathname. O journal também adquire lease e lê, grava/substitui e remove arquivos por pathname. O `server-build-test` de `.github/workflows/ci.yml` em `origin/main` roda no Ubuntu e executa `dotnet test MulletaFlix.sln` com filtro que exclui apenas `Integration`; o projeto `Jellyfin.Server.Implementations.Tests` está na solução, portanto `BackupRestoreDirectoryTests` será incluído quando seus arquivos estiverem no commit testado. Contudo, o helper e seu arquivo de testes ainda estão não rastreados no checkout e ausentes de `origin/main`; nenhum run remoto atual valida esta revisão. O job Linux existente também não testa corrida concorrente de ancestral no restore integrado.

Conclusão do gate: cobertura de crash/replay existente é evidência útil, mas não demonstra segurança contra troca concorrente de ancestrais. O P0 continua aberto; helper permanece não integrado; homologação formal 2/7 (28,6%), sem crédito novo. Próximas condições: especificar fronteira ACL e confiança do arquivo ZIP, corrigir as próprias races das primitivas, integrar restore/journal/replay/rollback/cleanup sem fallback por path, depois comprovar testes concorrentes/crash em Windows e Linux (incluindo filesystems distribuídos no pacote, glibc/musl) e CI remota. Nenhuma release/instalação/produção nesta auditoria.

#### Rename do helper rejeita symlink Linux — 2026-10-23

`BackupRestoreDirectory.Rename` agora consulta tipo da entrada de origem com `statx(AT_SYMLINK_NOFOLLOW)` e rejeita symlink no Linux, alinhando a regra existente de rejeitar reparse point no Windows. Foi adicionada regressão `Rename_RejectsSymbolicLinkSourceOnLinux`, que confirma que o link não muda de nome e o arquivo apontado permanece intacto.

Validação em Windows/.NET 10 Release: build do projeto de testes concluído; `BackupRestoreDirectoryTests` 17 aprovados/3 ignorados e `FullSystemBackup` 79 aprovados/3 ignorados, 0 falhas. Os 3 ignorados são Linux-only; portanto a nova branch P/Invoke Linux ainda não foi executada nesta máquina. `dotnet format whitespace --verify-no-changes` e `git diff --check` passaram nos arquivos rastreados; checkout tem arquivos helper/teste não rastreados. A checagem `statx` e o `renameat*` continuam separados por pathname; a corrida só pode ser tratada com fronteira de permissão/ACL confiável, ainda não verificada. O helper permanece sem caller runtime; TOCTOU P0, formal 2/7 (28,6%), sem crédito, release, instalação ou produção.

#### Rename do helper rejeita reparse point Windows — 2026-10-23

Foi adicionada regressão Windows `Rename_RejectsReparsePointSourceOnWindows`, usando a fixture de junction existente. O teste verifica que `Rename` falha com `IOException`, mantém o diretório-alvo e seu conteúdo, preserva a junction e não cria o destino. Em Windows/.NET 10 Release, `BackupRestoreDirectoryTests` passou com 18 aprovados, 3 ignorados (Linux-only) e 0 falhas; `dotnet format whitespace --verify-no-changes` passou. O código Windows já rejeitava reparse points; a mudança adiciona evidência, não corrige novo comportamento. Linux permanece não executado, o helper não possui caller runtime e a corrida TOCTOU/ACL continua P0. Formal 2/7, sem crédito/release/instalação/produção.

### Regressão completa do servidor — 2026-10-23

`dotnet test MulletaFlix.sln --configuration Release --no-restore --no-build --verbosity quiet -m:1` terminou com exit 0: 5.241 aprovados, 253 ignorados, 0 falhas em 17 projetos Windows/.NET 10. O filtro não excluiu assemblies Integration; testes condicionais/opt-in permaneceram ignorados. `FullSystemBackup` teve 80 aprovados, 3 ignorados (Linux-only), 0 falhas. Esta execução não exercitou primitivas Linux nem uma corrida de troca concorrente integrada. `BackupRestoreDirectory` continua sem caller de runtime; restore, journal, rollback e limpeza continuam baseados em pathname. Homologação formal permanece 2/7 (28,6%), sem crédito; Linux/CI remota, runtime instalado e produção não verificados. Sem release.

Revalidação build-inclusive em 2026-10-23: `dotnet test MulletaFlix-master\MulletaFlix.sln --configuration Release --no-restore --verbosity quiet -m:1` compilou o snapshot local e terminou com exit 0; todos os 17 projetos reportaram zero falhas. A saída foi capturada em buffer e não reteve os contadores agregados; a contagem exata permanece da execução `--no-build` acima. Foco repetido após o build: `FullSystemBackup` 80 aprovados, 3 ignorados (Linux-only), 0 falhas/83. Foram observados avisos xUnit durante a compilação; não foram triados nesta rodada. Nenhuma alteração de runtime, Linux, restore integrado, release ou produção.

#### Conta de serviço e limite da fronteira ACL — 2026-10-23

Auditoria somente leitura dos instaladores: `mulletaflix.service` e `linux-install.sh` executam Linux com usuário/grupo dedicado `mulletaflix`; dados, cache, logs e configuração recebem permissões de grupo específicas. O instalador NSIS oferece `NetworkService` ou `LocalSystem` para o serviço Windows (e registra essa escolha). Logo, não é seguro impor uma ACL uniforme nem presumir que somente o principal de serviço pode gravar em todos os diretórios: caminhos configurados e a identidade selecionada fazem parte do contrato ainda não testado. A política de ACL deve refletir essas opções e a proteção contra escritores não confiáveis precisa cobrir raízes de destino, journal/lease e ancestrais usados pelo restore.

Foco repetido em Windows/.NET 10 Release: `FullSystemBackup` 80 aprovados, 3 ignorados (Linux-only), 0 falhas/83. Evidência de teste não valida corrida integrada, Linux ou ACL instalada. `BackupRestoreDirectory` continua sem caller; a transação, rollback, cleanup, journal e replay continuam baseados em pathname. TOCTOU/ACL permanece P0; formal 2/7 (28,6%), sem crédito novo, release, instalação ou produção. Mapa técnico conferido: sem mudança estrutural. Próximo passo exige matriz explícita de identidade/permissões para os dois modos Windows e Linux e then integração vertical de toda a transação, sem fallback path-based.

#### Mapa de raízes e ACL do instalador efetivamente usado pela release — 2026-10-23

Rastreio dos projetos corrigiu a leitura anterior: `.github/workflows/release.yml` usa `MulletaFlix-packaging-master/build-stage-and-installer.ps1`, que seleciona `MulletaFlix-packaging-master/MulletaFlix-ux-custom/nsis/mulletaflix.nsi`; não usar a árvore semelhante `mulletaflix-server-windows-mulletaflix-installer-branding/nsis` como fonte da release atual. O NSIS de release rejeita root/junction de data antes de alterar ACL, remove herança e escrita de Users/Everyone/Authenticated Users da raiz de dados e concede Administrators/SYSTEM/NetworkService/LocalService com herança a descendentes. `ServiceDataAcl.Tests.ps1` executado neste host completou e imprimiu: `Service data ACL tests passed: broad inherited and explicit root grants removed; child inheritance preserved; outside sentinel unchanged.` É teste da política/fixure, não leitura da ACL de uma instalação real.

Topologia confirmada: `ProgramDataPath` (`--datadir`/`MulletaFlix_DATA_DIR`) contém `DataPath`, `RootFolderPath`, metadata padrão e `restore-recovery/`; `ConfigurationDirectoryPath` é destino independente configurável; `InternalMetadataPath` pode ser apontado pelo `MetadataPath` da configuração; `DefaultInternalMetadataPath` é outra raiz sob ProgramData. O restore escreve nas cinco raízes (`Config`, `Data`, `Root`, metadata ativa e metadata padrão); journal/lease ficam fora delas em `ProgramDataPath/restore-recovery`. Cache/logs não são destinos do restore.

Linux empacotado roda como usuário/grupo dedicado `mulletaflix`; instalador define ownership/mode dos diretórios padrão e a unidade systemd limita escrita via `ReadWritePaths`. Caminho `MetadataPath` externo não é automaticamente autorizado pela unidade ou corrigido pelo instalador. Windows/Linux ACLs efetivas dos caminhos personalizados e instalação em serviço não foram inspecionadas.

Decisão de implementação refinada: não aplicar uma única ACL nem reescrever permissões durante o restore. Abrir, normalizar e deduplicar cada raiz real; incluir journal e pais necessários na matriz; validar que o principal atual consegue gravar e que ancestrais/destinos não são modificáveis por atores não confiáveis segundo o contrato de cada plataforma; falhar antes de qualquer mutação se não cumprir. Em Linux empacotado, `MetadataPath` fora de `ReadWritePaths` deve falhar com diagnóstico acionável, não contornar sandbox. Em Windows, validar o token efetivo (serviço em `NetworkService`/`LocalSystem`, ou usuário interativo no modo sem serviço) e a ACL das raízes; não inferir ACL efetiva só do script do instalador.

O requisito acima só prepara a integração completa. O helper continua sem caller de runtime; `BackupService` e journal/replay seguem pathname-based. `FullSystemBackup` focal anteriormente reexecutado: 80 aprovados, 3 Linux-only ignorados, zero falhas/83. `ServiceDataAcl.Tests.ps1` é harness executável, sem contagem Pester estruturada. TOCTOU permanece P0; homologação formal 2/7, sem crédito, release ou produção. Ver sessão `2026-10-23-MulletaFlix-restore-root-acl-contract-audit` e decisão no vault.

#### Sincronização da identidade efetiva do serviço Windows — 2026-10-23

O instalador de release agora aplica ao Service Control Manager a mesma identidade lida de `ServiceAccountType`: `NetworkService`, `LocalSystem` ou `LocalService` legado. Isso corrige o upgrade em que o ACL podia refletir a conta selecionada enquanto o serviço mantinha a identidade anterior. `ServiceDataAcl.Tests.ps1` passou e NSIS compilou em `.build/service-acl-validation` (versão 12.1.24; SHA-256 `9488AAD16EB79ED5D48D87F5E074644523A0BE58AEE1708CA705C734F642C784`). A compilação não executa o instalador. Instalação/upgrade real, serviço, ACL de restore/ancestrais, Linux/Docker continuam pendentes; formal 2/7 sem crédito, TOCTOU/ACL P0 aberto, sem release.

#### Auditoria integrada e regressão ampla — 2026-10-23

Reexecução Windows/.NET 10: `dotnet test MulletaFlix.sln --configuration Release --no-restore --verbosity quiet -m:1` terminou exit 0; 5.241 aprovados, 253 ignorados, 0 falhas em 17 projetos. Nebula Python 3.14: `py -3.14 -m pytest -q`, 688/688 aprovados. Ignorados incluem 253 testes de integração/opt-in e Linux-only; nenhuma validação Linux nativa ou execução de produção ocorreu.

Revisão independente localizou o caminho mais sensível em `FileSystemRestoreTransaction.ApplyEntryAsync`: verificação de destino, exclusão do arquivo existente e `File.Move` são operações separadas por pathname (`BackupService.cs`, linhas 808–820 no snapshot desta auditoria). Rollback/cleanup repetem operações path-based; o journal e replay também abrem, substituem e removem por nome. O helper relativo segue sem caller de runtime. Não é seguro integrar apenas parte dele, pois staging, backup, marker `.absent`, rollback, cleanup, journal/lease e replay compartilham a mesma transação de recuperação.

Gaps de validação: teste determinístico de troca de ancestral integrado via `BackupService`; roots iguais/aninhados/sobrepostos e posição do journal; ACL efetiva para identidade de serviço e interativa no Windows, Linux, ancestrais, metadata e destinos customizados; replay/rollback sob swap adversarial em Windows e Linux. A menção histórica de que os testes não reiniciam processo real é datada de 22/10; o teste atual inclui processo-filho, mas isso não cobre a troca concorrente.

Sem alteração de runtime. Homologação formal continua 2/7 (28,6%), sem crédito; TOCTOU/ACL P0 aberto. Sem release ou instalação. A estrutura de módulos e fluxo permanece igual; mapa técnico conferido.

#### Abertura segura de arquivo existente no helper — 2026-10-23

`BackupRestoreDirectory.OpenExistingFile` abre arquivo relativo ao handle/dirfd sem seguir reparse point/symlink e valida tipo no handle aberto (Windows: atributos do handle; Linux: `statx(AT_EMPTY_PATH)`). Regressões verificam leitura/escrita, entrada ausente/diretório e symlink Linux. Um teste Windows adicional confirma que o handle aberto bloqueia rename/substituição do arquivo. Windows/.NET 10 Release: helper 21 aprovados/4 ignorados; `FullSystemBackup` 83 aprovados/4 ignorados, zero falhas. Linux-only não foi executado localmente.

O helper segue sem caller de runtime; restore/journal/replay/rollback/cleanup seguem pathname-based. ACL/ancestrais confiáveis e races das demais operações não foram fechadas. TOCTOU/ACL P0 aberto, homologação formal 2/7 (28,6%) sem crédito. Sem release, instalação, Linux nativo ou produção. Mapa atualizado. Sessão `2026-10-23-MulletaFlix-restore-existing-file-relative-api`.

#### Rejeição de destinos duplicados no arquivo de backup — 2026-10-23

`FileSystemRestoreTransaction` agora rejeita a segunda entrada ZIP que normalize para o mesmo destino. Antes, entradas duplicadas reutilizavam os mesmos sidecars determinísticos (`.bak`, `.tmp`, `.absent`), permitindo que o rollback de uma entrada removesse o original restaurado pela entrada seguinte. Regressão cria ZIP duplicado, exige falha sem sobrescrever o arquivo original e confirma limpeza dos sidecars. A chamada de runtime única continua sendo `BackupService.CopyDirectoryAsync` → `ApplyEntryAsync`; a proteção fica na transação compartilhada.

Teste focal Release passou 1/1; `FullSystemBackup` passou 85, ignorou 6 testes Linux-only, zero falhas (91 casos). Suíte ampla `Jellyfin.Server.Implementations.Tests` passou 1.587, ignorou 72, zero falhas (1.659 casos), Windows/.NET 10 Release, `--no-build`. A validação ZIP de integridade ainda não rejeita duplicatas antecipadamente; a transação falha com segurança quando detecta repetição. Isso não resolve o TOCTOU P0: `FileSystemRestoreTransaction` e rollback/cleanup ainda usam caminhos, ACL efetiva não verificada e Linux não testado. Homologação formal segue 2/7 sem crédito; sem release, instalação ou produção.

#### Prefixos de caminho case-insensitive no Windows — 2026-10-23

O primeiro teste de caminho com diferenças de maiúsculas revelou que `CopyDirectoryAsync` descartava entradas como `config/SERVER.json` antes de chamar a transação: os prefixos de origem e destino usavam comparação ordinal. Os três filtros de caminho no fluxo agora usam comparação ordinal sem distinção de caixa no Windows e ordinal nos demais sistemas suportados; o conjunto de destinos usa a mesma regra. O caso de teste também cobre `data/METADATA` para garantir exclusão da passagem `Data` e restauração na raiz de metadata configurada.

Testes para duplicata exata, alias de caixa e restauração em metadata passaram 3/3 em Windows/.NET 10 Release; `FullSystemBackup` 86 aprovados/6 ignorados Linux-only/0 falhas (92); suíte `Jellyfin.Server.Implementations.Tests` 1.588 aprovados/72 ignorados/0 falhas (1.660). `dotnet format whitespace --verify-no-changes` passou. A comparação segue sem sondar filesystems Linux com opção casefold; Linux nativo não foi testado. TOCTOU/ACL P0 do restore segue aberto, formal 2/7 sem crédito, sem release/produção.

#### Revisão adversarial das primitivas e transação — 2026-10-23

Dois scouts independentes, somente leitura, confirmaram que o restore segue parcialmente baseado em pathname: `FileSystemRestoreTransaction` usa caminhos em aplicação, staging, sidecars, rollback, commit/cleanup e diretórios; o bootstrap do journal e a leitura/verificação da origem ZIP também são por caminho. O journal usa handles relativos após abrir a raiz, mas essa abertura e seus ancestrais ainda dependem da fronteira de confiança. Há 3 entrypoints de restore e um caminho comum de aplicação; as gravações abrangem cinco raízes configuráveis, área separada de journal e o ZIP de origem.

A revisão adversarial encontrou no backend Linux races check-then-act em `OpenOrCreateDirectory` (`mkdirat` seguido de `openat`), `DeleteEntry` (`statx` seguido de `unlinkat`) e `Rename` (`statx` seguido de `renameat*`) se outro processo puder modificar o diretório-pai. A corrida de rename foi classificada P2; as duas primeiras P1 pelo reviewer. A condição depende de pai modificável, mas ACL efetiva e enforcement da fronteira não foram verificados. Não foi feita alteração de runtime nem executado teste nesta sessão. Não integrar parcialmente: primeiro definir e comprovar ACL/trust boundary para raízes, ancestrais, journal e ZIP; depois eliminar/mitigar races das primitivas e migrar a transação completa sem fallback pathname, com testes concorrentes/crash Windows e Linux.

Estado permanece: TOCTOU/ACL P0 aberto; homologação formal 2/7 (28,6%), sem crédito; Linux, ACL instalada, runtime e produção não verificados. Nenhuma release ou publicação remota.

#### Contrato de diretórios no Docker e permissões — 2026-10-23

Auditoria do packaging identificou que `MulletaFlix.Server.Helpers.StartupHelpers.CreateApplicationPaths` lê `MulletaFlix_DATA_DIR` e `MulletaFlix_CONFIG_DIR`, enquanto `MulletaFlix-packaging-master/docker/Dockerfile` declarava apenas `JELLYFIN_DATA_DIR=/config` e `JELLYFIN_CONFIG_DIR=/config/config`. O Dockerfile monta `/config` como volume, mas sem as variáveis consumidas pelo servidor essa persistência não estava ligada ao contrato de startup; a imagem podia cair nos diretórios de dados/configuração padrão do usuário. Foram adicionados os aliases MulletaFlix e um teste de contrato de packaging. A imagem não foi construída nem executada, Docker não está instalado neste host.

A mesma auditoria impede uma regra genérica de ACL baseada apenas em owner ou em bits de escrita: o instalador Linux empacotado usa `mulletaflix:mulletaflix`/`0750` nas raízes padrão, enquanto a imagem Docker define `/config` e `/cache` como `0777`, sem declarar `USER`; bind mounts ainda dependem das permissões do host e da configuração do container. Além disso, o caminho Docker efetivamente usado pelo servidor precisa ser confirmado em runtime. Não aplicar guarda única antes de estabelecer matriz de identidade, raízes e ambiente de execução por distribuição.

Verificação nesta sessão: teste Node de contrato de packaging e JSON do portal passaram; homologação formal permanece 2/7, sem crédito. Linux nativo, build/execução Docker, volumes montados, ACL efetiva e produção continuam pendentes. TOCTOU/ACL P0 do restore não foi resolvido.

Regressão completa build-inclusive após o teste de handle: `dotnet test MulletaFlix.sln --configuration Release --no-restore --verbosity quiet -m:1` terminou exit 0 em Windows/.NET 10, 17 projetos; soma dos resultados reportados por projeto: 5.244 aprovados, 254 ignorados e zero falhas (5.498). `FullSystemBackup` passou 83/87, quatro Linux-only ignorados. Integrações externas permanecem em opt-in/ignoradas; avisos de analisadores/documentação foram observados, não triados. Isso não valida Linux, ACL instalada, produção ou restore integrado. Formal 2/7 sem crédito; TOCTOU/ACL P0 aberto; nenhuma release.

#### Migração do journal de recuperação para handles relativos — 2026-10-23

`BackupRestoreDirectory` ganhou operações de abrir/criar diretório e arquivo relativo, além de exclusão idempotente de arquivo regular. `BackupRestoreRecoveryJournal` agora usa essas operações para acessar o diretório, adquirir lease com lock exclusivo por intervalo, ler journal, gravar temporário e substituir por rename, e excluir o journal. O lease continua exclusivo entre processos; teste existente passou. A criação inicial de `_programDataPath` ainda usa `Directory.CreateDirectory` antes de abrir a raiz; o bootstrap depende de ancestral confiável.

Verificações Windows/.NET 10 Release: `FullSystemBackup` 84 aprovados, 6 ignorados Linux-only, 0 falhas/90; `Jellyfin.Server.Implementations.Tests` 1.586 aprovados, 72 ignorados, 0 falhas/1.658; contrato do portal 72/72 e JSON válido. O novo teste Linux do lock symlink foi ignorado neste host. Revisão adversarial confirmou riscos condicionais se pais não tiverem ACL confiável: criação Linux `mkdirat`→`openat`, validação→`unlinkat` e abertura de arquivo especial antes da validação de tipo; ACL efetiva não foi verificada. A transação de arquivos ainda usa pathname em `ApplyEntryAsync`, rollback, cleanup e criação/remoção de diretórios. Portanto o TOCTOU P0 não está resolvido, homologação formal segue 2/7 sem crédito, e não houve release/instalação/produção.

#### Reauditoria da fronteira ACL Linux — 2026-10-23

`BackupRestoreDirectory` exige que raiz e ancestrais permaneçam protegidos por ACL confiável, mas não inspeciona ACL efetiva. Review read-only confirmou que `CreateDirectory`/`OpenOrCreateDirectory` fazem `mkdirat` e depois `openat`; `DeleteEntry` faz `statx(AT_SYMLINK_NOFOLLOW)` e depois `unlinkat`; `Rename` verifica o tipo por nome antes de `renameat*`. As mutações finais são relativas ao dirfd e não seguem links na operação, mas um escritor do diretório-pai pode substituir a entrada entre chamadas. Isso não prova exploração sem a pré-condição de escrita no pai; essa pré-condição não foi excluída por teste de ACL.

O pacote Linux nativo usa usuário/grupo `mulletaflix`, diretórios padrão `0750` e sandbox `ReadWritePaths`; Docker declara `/config` e `/cache` `0777`, e bind mounts dependem do host. Windows tem identidades/ACL distintas. Restore grava em cinco raízes configuráveis, enquanto journal fica separado. Não existe contrato testado que determine quais principals podem escrever cada raiz e seus ancestrais nos modos systemd, Docker e Windows. Portanto não integrar parcialmente a transação nem adicionar uma guarda genérica de owner/mode.

Verificação desta rodada: `BackupRestoreDirectoryTests` em Windows/.NET 10 Release passou 22, ignorou 5 casos Linux, falhou 0 (27 total); `.github/workflows/ci.yml` roda a suíte principal em `ubuntu-latest`, mas nenhum run remoto deste checkout foi observado. WSL não está instalado e Docker/Podman não estão disponíveis. Não houve alteração de runtime. Pendente: definir política explícita de principals autorizados para raiz, ancestrais, journal, ZIP e bind mounts; então implementar/verificar por plataforma e só depois migrar a transação inteira. TOCTOU/ACL P0 segue aberto; homologação formal 2/7 (28,6%), sem crédito.

#### Regressão completa e teste focal após recompilação — 2026-10-23 19:17 BRT

`dotnet test MulletaFlix.sln --configuration Release --no-restore --logger 'console;verbosity=quiet' -m:1` recompilou e executou 17 projetos no Windows/.NET 10: 5.247 aprovados, 256 ignorados, 0 falhas/5.503 resultados. Execução focal de `FullSystemBackup` passou 86, ignorou 6 testes Linux-only, 0 falhas/92; o projeto `Jellyfin.Server.Implementations.Tests` reportou 1.588 aprovados, 72 ignorados, 0 falhas/1.660. Avisos de documentação/analisadores apareceram durante a compilação e não foram triados. O teste comprova o snapshot Windows recompilado, não Linux, ACL efetiva ou corrida integrada. Restore transacional ainda path-based; TOCTOU/ACL P0 aberto, homologação formal 2/7 sem crédito, sem runtime/produção/release.

#### Decisão de política ACL para restore — 2026-10-23

O usuário escolheu o padrão recomendado: permitir escrita somente à identidade efetiva do serviço e a root/administradores; nenhum grupo adicional confiável é permitido por padrão. Caminhos compartilhados falham fechados quando não for possível comprovar essa fronteira. Isso resolve a decisão de política, não sua implementação: ainda é necessário identificar/verificar principals e ACL efetivas por raiz, ancestrais, journal, ZIP e modo (Windows, systemd Linux, Docker e bind mount), fechar as corridas e testar o fluxo completo. TOCTOU/ACL P0 permanece aberto; nenhuma homologação formal é concedida.

#### ACL de dados do instalador restrita à identidade selecionada — 2026-10-23 19:53 BRT

O instalador Windows agora remove grants de serviço antigos (SYSTEM, LocalService e NetworkService) na raiz de dados antes de aplicar a identidade configurada: `NetworkService` recebe Modify, `LocalService` permanece compatível somente quando já selecionado na configuração legada, e `LocalSystem` escreve pela ACE SYSTEM. Administradores e SYSTEM mantêm Full; grants amplos Users/Everyone/Authenticated Users continuam removidos. A verificação continua limitada à raiz escolhida, sem `/T`, e não inspeciona ACL efetiva dos ancestrais nem destinos de restore.

`pwsh -NoProfile -File MulletaFlix-packaging-master/scripts/tests/ServiceDataAcl.Tests.ps1` passou em Windows: grant amplo herdado removido sem percorrer descendentes; NetworkService selecionado presente; LocalService obsoleto ausente na raiz e arquivo filho; sentinel externo preservado. A primeira execução detectou asserção regex que não cruzava linhas; correção do teste e repetição passaram. Nenhum build NSIS, instalação limpa/upgrade, runtime, Linux, Docker ou ACL de produção foi testado. Isso alinha o ACL do pacote à identidade escolhida, mas não prova ACL de restore nem resolve TOCTOU: helper ainda não integrado, ACL dos paths/ancestrais e races seguem pendentes. Formal 2/7 (28,6%), sem crédito; sem release, instalação ou produção. Mapa técnico conferido; fluxo/runtime e APIs sem mudança estrutural, contrato do instalador atualizado.

#### Validação focal e bloqueios da integração segura — 2026-10-23 20:12 BRT

No Windows/.NET 10, `dotnet test tests/Jellyfin.Server.Implementations.Tests/Jellyfin.Server.Implementations.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~FullSystemBackup` recompilou o projeto e passou: 86 aprovados, 6 ignorados Linux-only, 0 falhas (92). A compilação emitiu avisos de analisadores/XML que não foram triados. O host não exercitou backend Linux nem ACL efetiva.

Leitura do código e três revisões independentes confirmam que `FileSystemRestoreTransaction` continua pathname-based em aplicação, staging, sidecars, rollback, cleanup e diretórios; `BackupRestoreDirectory` não é caller da transação. Foram observadas races Linux em verificação de tipo seguida por `unlinkat`/`renameat*`, validação de tipo depois de abrir arquivo especial, e ACL como pressuposto documentado mas não validado. A matriz CI inclui macOS, enquanto `OpenRoot` só implementa Windows/Linux; testes helper sem skip podem falhar e o caminho de recovery precisa ser avaliado no macOS. Não foi reproduzido remotamente.

Política selecionada pelo usuário: somente identidade efetiva do serviço e root/administradores; nenhuma ACL extra confiável por padrão; paths compartilhados falham fechados quando a fronteira não puder ser provada. A política ainda não está implementada no runtime. Contrato local do portal passou 74/74; `node --check`, JSON parse e `git diff --check` passaram. Próxima etapa segura: comprovar identidade e ACL em cada raiz/pai/journal/ZIP para Windows, systemd e Docker/bind mounts; corrigir primitives e então migrar o lifecycle inteiro e testes adversariais. Homologação formal permanece 2/7 (28,6%), sem crédito; TOCTOU/ACL P0 aberto; sem release ou produção.

#### Serialização de gravação de imagens por item — 2026-10-23

O log de produção anexado mostrou várias threads tentando apagar simultaneamente `BaseItemImageInfos` do mesmo `ItemId`; MariaDB retornou deadlock 1213, com retentativas repetidas e heartbeat do Kestrel atrasado. `SaveImagesAsync` agora usa o mesmo lock por faixa de `ItemId` da persistência de metadados, serializando só operações que compartilham a faixa e mantendo as retentativas transitórias limitadas. O caller segue sendo `LibraryManager.UpdateImagesAsync`; sem mudança de API/persistência de schema. Foi adicionado teste unitário para garantir que operações do mesmo item aguardem o mesmo lock. A CI ganha job `macos-14` que executa os dois testes específicos de startup macOS do restore; cobertura remota ainda depende de execução do workflow.

Homologação formal permanece 2/7, sem crédito. Correção não foi instalada em runtime nem validada contra MariaDB de produção; teste integrado de corrida/deadlock não foi executado. TOCTOU/ACL P0 do restore continua aberto. Release apenas em rascunho local e publicação bloqueada pela árvore Git preexistente suja e divergente do remoto.

#### Teste integrado de concorrência MariaDB para imagens — 2026-10-23 20:49 BRT

Adicionado `SaveImagesAsync_ConcurrentRefreshesForSameItem_PersistOneCompleteImageSet`: doze refreshes concorrentes para o mesmo `ItemId` verificam término, ausência de duplicatas e persistência das duas imagens esperadas. O teste foi ligado ao job CI com MariaDB descartável Linux. O harness agora gera schema aleatório por instância e o remove ao final; deixa de apagar schema de nome fixo preexistente. Compilação do projeto Integration passou; o teste focal foi ignorado localmente porque `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING` não está configurada e `127.0.0.1:33099` não está disponível. O CI remoto ainda não rodou. Sem mudança no runtime; homologação 2/7 sem crédito, nenhum teste/alteração em produção.

#### Regressão completa .NET após o teste MariaDB — 2026-10-23 21:04 BRT

`dotnet test MulletaFlix.sln --configuration Release --no-restore --logger 'console;verbosity=quiet' -m:1` recompilou e executou a solução Windows/.NET 10; exit code 0, 17 projetos de teste e nenhum projeto reportou falha. A contagem agregada não foi preservada no terminal, portanto não se registra soma total. Entre os resultados visíveis: `Server.Implementations.Tests` 1.590 aprovados/74 ignorados; `Api.Tests` 772/775, com 3 ignorados; `Providers.Tests` 865 aprovados/13 ignorados; `Server.Integration.Tests` 3 aprovados/142 ignorados. O novo teste MariaDB continua não validado neste host, pois depende de serviço descartável dedicado; Linux CI remoto não observado. Avisos de compilação XML/analisadores apareceram e não foram triados. Formal 2/7 sem crédito; sem instalação, produção, release ou publicação remota.

#### Revisão do log de deadlock após revalidação focal — 2026-10-23 21:46 BRT

O log recebido registra `MySqlException 1213` ao executar `DELETE BaseItemImageInfos` para o mesmo `ItemId`; várias threads repetem a falha. O trecho não identifica versão/binário carregado nem quantidade de processos. No fonte local, `SaveImagesAsync` e `UpdateOrInsertItems` compartilham o mesmo lock por faixa. A regressão unitária `SaveImagesAsync_WaitsForSameItemPersistenceLockBeforeOpeningDatabaseContext` passou 1/1 em Windows/.NET 10 Release.

Limite encontrado: `ItemPersistenceService.DeleteItem` também exclui `BaseItemImageInfos`, mas não adquire as faixas; `SemaphoreSlim` também não coordena processos distintos. Isso deixa dois escritores plausíveis, ainda não comprovados como causa deste log. A integração concorrente MariaDB não pôde ser executada: Docker, MariaDB e MySQL não estão disponíveis e `127.0.0.1:33099` recusou conexão. Não aplicar mitigação global sem medir o impacto sobre exclusões grandes. Próximo passo: executar o teste com MariaDB isolado e instrumentar a identidade/versão/processo do servidor; avaliar serialização de exclusão sem manter transação aberta enquanto espera locks.

Nenhum código de runtime alterado nesta revisão. Correção anterior segue somente em fonte local/CI pendente; release 12.1.32 é rascunho não publicado. Homologação formal 2/7 sem crédito; TOCTOU/ACL P0 do restore permanece aberto.

#### Auditoria transversal do servidor — 2026-10-23 23:35 BRT

Regressão local Windows/.NET 10 Release: `dotnet test MulletaFlix.sln --configuration Release --no-restore --no-build --filter 'FullyQualifiedName!~Integration' --logger 'console;verbosity=minimal' -m:1` terminou exit 0. Dezessete projetos foram enumerados; 15 produziram resultados e os dois projetos de integração não tinham casos compatíveis com o filtro. Soma dos resumos: 5.240 aprovados, 78 ignorados e 0 falhas/5.318. O filtro exclui testes cujo nome qualificado contém `Integration`; não valida integração real. Os ignorados incluem Mongo/MariaDB opt-in e casos Linux/macOS.

NebulaPython foi reexecutado no virtualenv isolado `.build/nebula-upload-tests-20261021`: `pytest -q` passou 694/694. Portal local passou 83/83.

Review estático independente: fluxo de imagem consulta idiomas e persiste proveniência; não foi encontrado defeito determinístico no fonte, mas provider real, migration/runtime e imagens existentes não foram verificados. Restore continua P0: `FileSystemRestoreTransaction` não usa `BackupRestoreDirectory`; apply, rollback e cleanup operam por pathname após checagens separadas, e ACL efetiva não é validada. Nebula recovery de uploads com lease/worker foi considerado hipótese; sem Mongo concorrente não se atribui defeito. Portal local atualizado, homologação formal permanece 2/7 sem crédito. Nenhuma release, instalação ou produção.

#### Regressão comportamental de locks ParentId/OwnerId — 2026-10-23 22:38 BRT

O teste `SaveItems_WaitsForParentOrOwnerPersistenceLockBeforeEvaluatingMetadata` agora cobre os dois vínculos: mantém o stripe do pai ou owner, confirma que `SaveItems` continua bloqueado e verifica que nenhum contexto EF é aberto antes da liberação. No fixture, a execução após liberar o lock para em `BaseItem.LibraryManager` global ausente; portanto o teste não afirma sucesso de persistência nem substitui integração MariaDB. `ItemPersistenceServiceTests` passou 29/29 em Windows/.NET 10 Release. O novo log ainda mostra `MySqlException` 1213 em produção, mas não inclui versão/binário carregado ou número de processos; não prova que o runtime esteja usando estas alterações. O teste concorrente MariaDB permanece ignorado por ausência de serviço descartável. Nenhuma instalação, produção ou release nesta sessão; formal 2/7 sem crédito.

#### Revalidação do deadlock de imagens e divergência entre runtime e checkout — 2026-10-23 23:42 BRT

O log de 20:35 mostra vários `DELETE BaseItemImageInfos` com MariaDB 1213 para o mesmo item e aviso posterior de thread-pool starvation; o prefixo `Status Code 200` não comprova sucesso da gravação. Auditoria dos escritores confirmou que `SaveImagesAsync`, `SaveItems`/`UpdateOrInsertItems` e `DeleteItem` usam as mesmas faixas estáticas de `SemaphoreSlim` dentro do processo; não foi encontrado escritor de runtime adicional no repositório. O teste unitário focal passou 29/29. Há dois testes MariaDB concorrentes para refreshes de imagem e exclusão, mas nenhum para concorrência de `SaveItems` ou processos separados.

No host, uma instância MulletaFlix ativa carrega `C:\Program Files\MulletaFlix\Server\MulletaFlix.Server.Implementations.dll` versão 12.1.35; o checkout/build Release local está em 12.1.32, com SHA-256 distinto. A tag `v12.1.35` de `origin/main` foi conferida e seus métodos `SaveImagesAsync` e `DeleteItem` não adquirem as travas por item presentes no checkout atual. Portanto, o código publicado na tag não inclui essa serialização local; a versão instalada coincide com a tag, embora o binário não tenha sido comparado byte a byte ao artefato de release nem o processo correlacionado formalmente ao trecho de log. A instância MariaDB local está na porta padrão 3306; a variável opt-in de testes descartáveis não está configurada e o helper rejeita 3306. Nenhum teste foi apontado ao banco em execução.

Próximo gate: executar os cenários concorrentes existentes e acrescentar concorrência de `SaveItems` somente contra MariaDB descartável isolada; comparar o binário instalado ao artefato oficial e correlacionar com `SHOW ENGINE INNODB STATUS` após novo deadlock. A correção das travas está apenas no checkout e não foi instalada/publicada. Portal local passou contrato 84/84. Homologação formal 2/7 sem crédito. O lock local não coordena processos distintos; não se provou se o deadlock ocorreu por processo concorrente, outra transação ou ambas as condições.

#### Revalidação após novo log — 2026-10-23 23:52 BRT

O novo anexo repete o deadlock 1213 no `DELETE BaseItemImageInfos` para o mesmo `ItemId`, com pelo menos 13 falhas registradas em intervalo de milissegundos e aviso posterior de atraso do heartbeat Kestrel. O `Status Code 200` que antecede os erros não demonstra commit no banco. Revalidação read-only encontrou um processo MulletaFlix ativo e assembly instalado `MulletaFlix.Server.Implementations.dll` 12.1.35; o artefato local Release compilado nesta sessão está em 12.1.32 e possui SHA-256 diferente. A tag `v12.1.35` da fonte remota não possui as travas locais em `SaveImagesAsync`/`DeleteItem`. Isso confirma divergência entre a fonte publicada da tag e o checkout, mas não prova igualdade byte a byte com o pacote instalado nem associa formalmente este trecho de log ao processo inspecionado.

`ItemPersistenceServiceTests` passou 29/29 no Windows/.NET 10 Release. A MariaDB em execução na porta padrão 3306 não foi consultada; a configuração de MariaDB descartável continua ausente, logo cenários concorrentes de integração não foram executados. Nenhum arquivo runtime foi instalado e nenhuma release publicada. Portal de homologação atualizado; formal 2/7 (28,6%), sem crédito. Riscos pendentes: deadlock pode envolver escritores/processos não coordenados; a serialização local não é distribuída; build/release precisa alinhar sua versão e validar concorrência contra banco descartável.

#### Validação concorrente em MariaDB descartável — 2026-10-24 00:05 BRT

O processo MulletaFlix ativo foi observado carregando `MulletaFlix.Server.Implementations.dll` 12.1.35; o checkout e o build Release estão em 12.1.32, com a serialização por faixa já presente no código local. `ItemPersistenceServiceTests` passou 29/29. Em MariaDB 11.4.4 isolada, dois testes de integração passaram 2/2: 12 refreshes concorrentes do mesmo item preservaram o conjunto completo de imagens; refreshes concorrentes com exclusão terminaram sem deadlock e sem registros de imagem órfãos. A instância usou datadir temporário e apenas `127.0.0.1:33099`; a porta 3306 não foi consultada. A instância temporária foi desligada normalmente e a porta 33099 fechou; a ferramenta bloqueou a remoção recursiva do diretório temporário, que permaneceu parado.

Isso valida os dois interleavings sob um único processo de teste, mas não cobre múltiplos processos, não reproduz todos os escritores externos e não associa formalmente o log ao processo observado. A correção continua sem release/instalação. Checkout e runtime seguem divergentes; formal permanece 2/7 (28,6%), sem crédito. Nenhum banco de produção foi lido ou alterado.

#### Rechecagem do log de deadlock de imagens e base de release — 2026-10-24 00:14 BRT

O novo anexo repete falhas MariaDB 1213 em `DELETE BaseItemImageInfos` para o mesmo `ItemId` em chamadas concorrentes de `SaveImagesAsync`; aparecem tentativas repetidas e, depois, heartbeat do Kestrel acima de 1 s. A resposta HTTP 200 anterior não prova que a persistência terminou. A evidência é compatível com a versão/tag 12.1.35 ainda sem os locks locais, mas o trecho não identifica PID, versão de assembly nem processo que originou cada chamada.

A mitigação já existente no checkout 12.1.32 passou anteriormente: unitários 29/29 e MariaDB 11.4.4 isolada 2/2 (12 refreshes e refresh versus delete), em `127.0.0.1:33099`; não tocou a MariaDB ativa na 3306. Isso não prova correção em processo múltiplo nem produção. Base de release está divergente: HEAD `64a33dcf` está 8 commits atrás de `origin/main` `d51f4f38` (12.1.35); o worktree possui 137 caminhos alterados e um draft 12.1.32. Não rebasear, substituir arquivos ou publicar desse estado. Nenhuma nova alteração de runtime, consulta ao banco ativo, instalação ou release nesta revisão. Portal local registra a rechecagem; remoto não publicado. Homologação formal continua 2/7 (28,6%), sem crédito.

#### Restore TOCTOU/ACL: revisão independente e regressão focal — 2026-10-23 23:58 BRT

Revisão read-only independente confirmou que `FileSystemRestoreTransaction.ApplyEntryAsync`, `EnsureDirectoryExists`, `Rollback`, `CleanupTemporaryFiles` e `RemoveCreatedDirectories` ainda consultam ou alteram a árvore por pathname. `BackupRestoreDirectory` tem operações relativas a handles/dirfd, mas nenhum caller no lifecycle ativo do restore. As cinco raízes-alvo precisam ser abertas uma vez; navegação, staging, marker `.absent`, swap, rollback, commit, cleanup e replay devem conservar os handles e usar nomes simples relativos durante todo o ciclo.

Limite de segurança: o helper não valida ACL efetiva. No Linux, `mkdirat`→`openat` e validação→`unlinkat`/`renameat*` ainda permitem corrida se o pai puder ser escrito por outro ator; a política aprovada exige somente identidade do serviço e root/administradores em raízes, ancestrais, journal e ZIP. O gate não pode ser fechado por integrar o helper parcialmente.

Testes Windows/.NET 10 Release: `FullSystemBackup` 88 aprovados, 9 ignorados (Linux/macOS), 0 falhas/97, 53s; `BackupControllerTests` 7/7. Avisos StyleCop/xUnit foram emitidos em outros controllers durante build do projeto API. Não houve teste adversarial de corrida, validação da ACL real, Linux nativo, instalação, produção ou release. Homologação formal 2/7 (28,6%), sem crédito. Próximo gate: implementar lifecycle completo sob fronteira ACL demonstrável e executar regressões adversariais em Windows e Linux nativo; definir/falhar fechado para macOS antes de executar restore nesse SO.

#### Reauditoria de restore e regressão do fluxo de backup — 2026-10-24 04:30 BRT

Na árvore principal suja `main` (`64a33dcf`), `FullSystemBackup` Release recompilou e passou 88 testes, ignorou 9 de plataforma e falhou 0/97 (35 s de execução de testes). Isso valida os casos existentes de backup/restore, não a segurança contra corrida. Leitura da implementação confirma `FileSystemRestoreTransaction.ApplyEntryAsync`, rollback, cleanup e criação de diretórios ainda usam pathname; o helper por handles só atende partes do journal. Nenhuma ACL efetiva é validada nas raízes, ancestrais, journal ou ZIP. Política aprovada continua permitindo apenas identidade do serviço + root/administradores e exige falha fechada para diretórios compartilhados.

Os testes Linux/macOS foram ignorados no host Windows; não foram executados cenário de corrida integrado, ACL real, restore real, runtime instalado ou produção. A regressão focal não fecha P0 nem gera crédito de homologação: formal continua 2/7 (28,6%). Sem alteração de runtime ou release. Próximo gate é integrar autoridade por handles a todo o lifecycle e demonstrar ACL/trust boundary antes de afirmar contenção.
#### Limpeza handle-relative de temporário/link do journal — 2026-10-23 22:51 BRT

`BackupRestoreDirectory` ganhou `DeleteNonDirectoryEntryIfExists`: remove entrada não-diretório relativamente ao diretório aberto, incluindo o próprio link, sem seguir o destino; diretórios são rejeitados. `BackupRestoreRecoveryJournal.WriteAsync` usa a operação no `finally` para remover temporários mesmo quando um staging é substituído por link. Teste Windows/.NET 10 Release: `BackupRestoreDirectoryTests` passou 23, ignorou 6 testes Linux-only e falhou 0 (29 total). A regressão Linux que comprova unlink de symlink sem tocar no alvo não executou neste host. Portal local atualizado. Esta é uma primitiva isolada, não migra a transação de restore: aplicação, rollback, replay e cleanup ainda são pathname-based; ACL efetiva não é validada; Docker `/config` permanece permissivo. TOCTOU/ACL P0 segue aberto, formal 2/7 sem crédito, sem runtime/produção/release.

#### Auditoria de identidade e volumes Docker — 2026-10-23 23:03 BRT

Leitura do `MulletaFlix-packaging-master/docker/Dockerfile` confirmou que a imagem cria `/config` e `/cache` com modo `0777`, não declara `USER` e inicia diretamente `/jellyfin/jellyfin`. O README documenta build da imagem, mas não fixa identidade de runtime nem procedimento de migração de ownership de bind mounts/volumes. Esta estação não tem comandos Docker, Podman ou Obsidian disponíveis; portanto não foi possível confirmar imagem/container implantado, usuário efetivo, mountpoints, ACL/ownership atuais ou comportamento com dados existentes. A presença de `chmod 777` no Dockerfile é evidência do contrato da imagem-fonte, não prova que produção use essa imagem/configuração.

Não foi alterado o Dockerfile: escolher uma identidade fixa ou aplicar `chown` sem inventário de mounts e regra de migração pode remover acesso a `/config` ou alterar ownership no host. Fonte oficial do Docker recomenda `USER` para serviços sem necessidade de privilégio e documenta que a identidade é aplicada ao `ENTRYPOINT`/`CMD`; documentação de deploy do Jellyfin permite configurar `user: uid:gid`, exigindo compatibilidade com os volumes. Pendente: obter compose/linha de execução e ACL/ownership anonimizados do ambiente-alvo, validar cópia descartável de volumes e definir upgrade/rollback antes de modificar a imagem. Nenhum container ou produção foi acessado.

Sem mudança de runtime ou cobertura nesta rodada; teste focal não reexecutado. TOCTOU/ACL P0 continua aberto; homologação formal 2/7 (28,6%), sem crédito; sem release/publicação.

#### Falha de limpeza mantém recuperação — 2026-10-24 04:39 BRT

`FileSystemRestoreTransaction.Commit` agora só marca commit depois que remove sidecars com sucesso. Se a limpeza falha, lança erro, preserva o journal, mantém a transação fora do rollback e solicita restart para replay do mesmo backup. Em falha de rollback/remoção de staging/diretórios no caminho anterior ao commit, `RollbackSucceeded` passa a refletir todos esses passos para não apagar o journal indevidamente.

Teste Windows bloqueia a exclusão do sidecar `.bak` via sharing, confirma erro, journal e backup preservados, libera o handle e verifica replay/limpeza. Caso focal passou 1/1 e repetiu 3/3; `FullSystemBackup` Release recompilado passou 89, ignorou 9 (Linux/macOS), 0 falhas/98 em Windows/.NET 10. Formatação focada e `git diff --check` passaram. Não foi executado Linux nativo, corrida concorrente de reparse, ACL efetiva, runtime/produção ou release. Esta correção protege recuperabilidade; não fecha TOCTOU/ACL P0. Homologação formal permanece 2/7 (28,6%), sem crédito.

#### Journal não cria ProgramData por pathname — 2026-10-24 04:49 BRT

`BackupRestoreRecoveryJournal.AcquireOperationLease` e `WriteAsync` não chamam mais `Directory.CreateDirectory` antes de abrir `_programDataPath` com `BackupRestoreDirectory.OpenRoot`. `StartupHelpers.CreateApplicationPaths` e `BaseApplicationPaths` estabelecem o diretório de dados antes de iniciar o servidor; o journal agora falha fechado se a raiz configurada estiver ausente, sem criar o caminho. Dois testes cobrem lease e gravação do journal com raiz ausente; fixtures de outros testes criam explicitamente suas raízes para corresponder ao startup.

`FullSystemBackup` Release recompilado passou 91, ignorou 9 Linux/macOS e falhou 0/100 em Windows/.NET 10. A primeira execução expôs fixtures com `ProgramData` ausente; após alinhar fixtures com o bootstrap documentado, a regressão passou. As verificações finais de formatação, portal e `git diff --check` também passaram. Nenhuma ACL efetiva, corrida TOCTOU integrada, Linux nativo, instalação, produção ou release foi validada; P0 segue aberto e homologação formal 2/7 sem crédito.

#### Reauditoria do lifecycle de restore — 2026-10-24 04:53 BRT

Revisão independente confirmou que `FileSystemRestoreTransaction` ainda usa pathname em `ApplyEntryAsync`, `EnsureDirectoryExists`, `Rollback`, `CleanupTemporaryFiles` e `RemoveCreatedDirectories`; replay reutiliza a mesma transação. `BackupRestoreDirectory` não tem caller nesse lifecycle. O helper também não valida ACL efetiva e não expõe todo o estado/operações atômicas exigidos para coordenar transação e sidecars. Foram mapeadas oito famílias de testes adversariais: troca de raiz/ancestral, concorrência no target, sidecars `.tmp`/`.bak`/`.absent`, corridas internas das primitivas Linux, rollback, limpeza e ACL permitida/proibida.

Regressão atual reexecutada: `FullSystemBackup` Release 91 aprovados, 9 ignorados por plataforma e 0 falhas/100 no Windows/.NET 10. `wsl --list --quiet` confirmou WSL não instalado; portanto Linux nativo continua sem execução. Nenhum código de runtime mudou. Homologação formal 2/7 (28,6%), sem crédito. P0 continua aberto; sem restore adversarial integrado, ACL efetiva, runtime/produção ou release.

#### Auditoria read-only do baseline limpo `d51f4f38` — 2026-10-24 09:37 BRT

O worktree limpo em `d51f4f38d2b4148ab49394c001567ad273bd57aa` não contém `FileSystemRestoreTransaction`, `BackupRestoreRecoveryJournal` nem `BackupRestoreDirectory` em nenhum arquivo C#. Portanto, alegações sobre esses componentes pertencem à árvore principal local e não descrevem este baseline. Aqui, `BackupService.RestoreBackupCoreAsync` valida a existência do ZIP por pathname, abre-o para validar CRC em `ValidateArchiveIntegrityAsync`, reabre o pathname e extrai entradas com `Directory.CreateDirectory` + `ZipArchiveEntry.ExtractToFile(overwrite: true)` para cinco raízes de aplicação. A validação lexical ignora entradas fora dos prefixos; não verifica reparse points/symlinks, ACL, identidade, nem mantém handles durante validação e escrita.

Chamadores ativos: endpoint manual `BackupController.StartRestoreBackup` sanitiza nome e rejeita reparse point no arquivo final antes de agendar; o caminho PITR agenda o caminho enumerado sem repetir essa verificação. `Program` executa o restore solicitado no startup; rollback de migração também chama `RestoreBackupAsync`. A interface pública recebe apenas `string archivePath`, então não transporta um handle validado entre agendamento e execução.

Testes no baseline cobrem corrupção/truncamento, validação de conteúdo do banco, restore isolado dos arquivos e caminhos sanitizados/reparse no controlador; não cobrem ACL service+admin, fail-closed para raiz compartilhada/inconclusiva, symlink/junction nos destinos, troca concorrente após validação, falha de escrita com restauração de arquivos, nem interrupção/replay. A validação focal posterior `FullSystemBackup` Release passou 40/40, sem ignorados ou falhas, no Windows/.NET 10; isso executa apenas os testes existentes e não acrescenta cobertura para os gaps listados. Nenhum DB, runtime instalado ou produção foi acessado.

Menor slice completa recomendada: autoridade de entrada para uma raiz de destino, do preflight à limpeza/rollback, sem atravessar pathname depois de abrir o diretório; validar todos os ancestrais e ACL efetiva antes da primeira mutação, rejeitar destinos compartilhados/inconclusivos e vincular leitura do ZIP ao mesmo handle validado. Exigir testes Windows para identidade permitida/proibida, ACL herdada/ampla, reparse em ancestral, troca concorrente, falha de escrita e rollback; adicionar os equivalentes Linux nativos antes de declarar compatibilidade multiplataforma. Isso ainda deixa quatro raízes e consistência entre raízes/banco por integrar; P0 não fecha até o lifecycle inteiro estar transacional e por handles.

Mapa técnico conferido; esta auditoria/documentação não altera runtime, API, dependências ou topologia. Homologação formal permanece 2/7 (28,6%), sem crédito; sem release.

#### Revalidação de call sites e infraestrutura — 2026-10-24 08:57 BRT

Call-site search no checkout principal confirma que `BackupRestoreDirectory` é chamado somente em `BackupRestoreRecoveryJournal` (lease/journal); `FileSystemRestoreTransaction` ainda grava, restaura e remove sidecars por pathname. Suíte focal Release recompilada de persistência passou 40/40; `BackupServiceTests` + `BackupRestoreDirectoryTests` passou 82, ignorou 9 Linux/macOS e falhou 0/91. Isso não testa integração por handles nem ACL efetiva. `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING` e Mongo equivalente ausentes; Docker/Podman/daemon MariaDB ausentes. `wsl --list --verbose` confirma que WSL não está instalado, embora o launcher exista. Listener local 3306 foi apenas observado; sem conexão. Sem runtime/banco/produção/release alterados. Homologação formal 2/7, sem crédito; próximo gate é integrar as mutações do lifecycle a handles e validar ACL/races nativamente em Windows/Linux.

#### Gap adicional do helper Linux — 2026-10-24 09:01 BRT

Releitura do código confirma que `OpenLinuxDirectoryPath` fecha o descritor anterior a cada `openat` e devolve somente o descritor da raiz final; em Windows, `OpenRoot` mantém handles dos ancestrais em `_ancestorHandles`. `BackupRestoreDirectory` não contém validador de ACL/owner em nenhuma plataforma. Portanto, antes de migrar a transação, faltam retenção/validação da cadeia Linux e consulta de ACL associada aos handles; checagem por pathname não satisfaz o contrato aprovado. Sem alteração de código; plataforma Linux não executada neste host. P0 e formal 2/7 permanecem abertos.

Correção de estado documental — 2026-10-24 13:21 BRT: a afirmação sobre fechamento dos descritores Linux acima é histórica e foi supersedida pela correção `OpenLinuxDirectoryPath` registrada às 09:09. A fonte atual mantém os handles ancestrais em `_ancestorHandles` até `Dispose`, e `BackupRestoreDirectoryTests` contém o caso `/proc/self/fd`. O teste Linux continua sem execução nativa neste host Windows. ACL/owner efetivos e integração do lifecycle apply/rollback/replay/cleanup continuam pendentes; o P0 não muda.

Revalidação Release — 2026-10-24 13:26 BRT: `BackupRestoreDirectoryTests` foi recompilado e executado no checkout atual: 23 aprovados, 7 ignorados por plataforma, 0 falhas/30 em Windows/.NET 10. O teste de retenção `/proc/self/fd` foi um dos ignorados; não é evidência Linux nativa. A ACL efetiva e o uso do helper no ciclo de restore continuam ausentes.

Regressão Release — 2026-10-24 13:26 BRT: filtro `FullyQualifiedName~FullSystemBackup` executado sem rebuild após a compilação focal: 91 aprovados, 10 ignorados por plataforma, 0 falhas/101 em Windows/.NET 10. Os ignorados incluem todos os casos Linux/macOS; sem evidência nativa dessas plataformas nem de ACL. Os testes existentes não cobrem o lifecycle inteiro por handle.

Pré-condição dos testes ACL — 2026-10-24 13:30 BRT: `icacls` confirmou que `%LOCALAPPDATA%\Temp` concede Modify herdável a `CodexSandboxUsers` e a um SID adicional não mapeado, além de SYSTEM, Administradores e usuário. Logo, roots de teste criados em `%TEMP%` devem falhar sob a política aprovada; isso não pode ser tratado como falso positivo nem corrigido afrouxando a regra. `%LOCALAPPDATA%` e o perfil do usuário não mostram o mesmo grant amplo na saída inspecionada. Antes de habilitar o gate nos testes de restore, mover apenas seus fixtures para diretório privado sob o perfil e adicionar casos que comprovem rejeição do TEMP inseguro. Sem mudança runtime nesta observação; Linux nativo permanece pendente.

Fixture do helper isolado — 2026-10-24 13:33 BRT: `BackupRestoreDirectoryTests.TemporaryDirectory` agora usa subdiretório randômico sob uma pasta dedicada no perfil em vez de `%TEMP%`. `icacls` na pasta-base mostrou escrita apenas para SYSTEM, Administradores e usuário. Suíte focal recompilada Release: 23 aprovados, 7 ignorados Linux/macOS, 0 falhas/30. Isto prepara os testes para o gate, mas ainda não testa ACL adversarial nem altera runtime; ignorados Linux continuam sem evidência nativa.

#### Reauditoria P0 do ciclo de restore — 2026-10-24 08:27 BRT

Duas revisões independentes do checkout confirmaram que `BackupRestoreDirectory` já é usado pelo journal/lease, mas não pelo `FileSystemRestoreTransaction`. `ApplyEntryAsync`, `EnsureDirectoryExists`, rollback, limpeza de sidecars e remoção de diretórios ainda fazem operações por pathname; validação de caminho/reparse point ocorre separada das mutações. ACL efetiva das cinco raízes, ancestrais, journal e ZIP não é verificada. O startup replay usa o mesmo lifecycle e não elimina esses gaps. As primitivas Linux também verificam tipo por nome antes de operações posteriores por nome; a fronteira ACL confiável continua pré-condição para qualquer integração.

`dotnet test tests/Jellyfin.Server.Implementations.Tests/Jellyfin.Server.Implementations.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~FullSystemBackup` recompilou o projeto e passou 91 testes, ignorou 9 de Linux/macOS e falhou 0/100 em Windows/.NET 10. Isso cobre regressões existentes, não corridas integradas, ACL real ou Linux nativo. O mapa técnico foi conferido e não mudou: nenhuma implementação foi alterada. TOCTOU/ACL P0 continua aberto; homologação formal 2/7 sem crédito. Sem restore real, runtime/produção, release ou publicação remota. Próxima etapa exige validar a trust boundary por raiz/ancestral e migrar apply, rollback, replay e cleanup juntos para handles, com falha fechada e testes nativos Windows/Linux.

#### Coordenação de mutações de UserData detached — 2026-10-24 08:20 BRT

`DeleteItem` inclui a stripe de `PlaceholderId` na aquisição ordenada e no advisory lock; `CleanupUserDataTask` delega a exclusão dos registros expirados ao mesmo `IItemPersistenceService`, que remove apenas UserData placeholder com `RetentionDate < cutoff` em transação. Isso coordena limpeza, reassociação e exclusão sem manter lock durante a descoberta da hierarquia. Foi adicionado teste unitário de espera pela stripe e teste MariaDB opt-in para retenção estrita e preservação de UserData anexado/na data de corte. Foco `ItemPersistenceServiceTests` + `CleanupUserDataTaskTests` Release: 34 aprovados; suíte completa `Jellyfin.Server.Implementations.Tests` Release recompilada: 1.609 aprovados, 75 ignorados, 0 falhas/1.684. O teste MariaDB novo compilou, mas foi ignorado por ausência de connection string descartável. A exclusão de UserData placeholder passa pelo contrato de persistência, sem writer direto fora dele. O log de imagens segue compatível com concorrência de refresh; as travas de imagem existentes no checkout passaram na suíte, mas produção/binário que originou o log não foi identificado. Portal local validado: 97/97 testes, JSON válido e `git diff --check` exit 0. Homologação formal permanece 2/7 (28,6%), sem crédito; banco ativo, instalação, produção e release não foram acessados. Ver [[Sessões/2026-10-24-MulletaFlix-placeholder-cleanup-coordination-0820]].

#### Falha relacional durante restore — 2026-10-24 07:02 BRT

`BackupRestoreMariaDbTests` injeta falha em `BEFORE INSERT` com trigger MariaDB durante restore e verifica rollback relacional e restauração da configuração anterior. Suíte completa `Server.Implementations` recompilada Release: 1.598 aprovados, 75 ignorados e 0 falhas/1.673 em 1m46s no Windows/.NET 10; `FullSystemBackup`: 91/100, 9 ignorados de Linux/macOS. Integração MariaDB 11.4.4 descartável: 4/4; porta 33099 encerrada. Não cobre cenários gerais de falha/crash do filesystem. `FileSystemRestoreTransaction` segue pathname-based em apply/rollback/replay/cleanup, ACL efetiva não é verificada e Linux nativo não foi executado. Homologação formal segue 2/7 (28,6%), sem crédito; sem release ou publicação remota. Ver [[Sessões/2026-10-24-MulletaFlix-restore-relational-insert-rollback-0702]].

#### Precedência de arquivo ausente e lease — 2026-10-24

A suíte de solução revelou que a ausência simultânea do arquivo solicitado e da raiz ProgramData fazia `AcquireOperationLease` expor `Win32Exception` em vez de `FileNotFoundException`. A pré-checagem do arquivo antes do lease foi descartada porque quebrava a garantia de que uma operação concorrente bloqueia antes de ler journal/arquivo. A correção final só converte erros nativos de caminho/arquivo inexistente (2/3) quando não há recuperação pendente e o backup solicitado também não existe; outros erros de abertura do lease continuam intactos.

Foco: teste de telemetria/arquivo ausente 1/1 e teste de lease ocupado 2/2. Solução Release completa: 5.260 aprovados, 261 ignorados, 0 falhas/5.521 no Windows/.NET 10. Os ignorados incluem integrações opt-in e cenários de plataforma/dependências não disponíveis. Isso é cobertura automatizada, não homologação formal: permanece 2/7 (28,6%), sem crédito. Linux nativo, runtime, ACL efetiva e produção não foram validados; o P0 TOCTOU/ACL do lifecycle de restore continua aberto. Sem release.

O evento correspondente foi gravado em `portal-site/homologacao-status.json`; `node --test portal-site/tests/homologacao.test.mjs` passou 91/91 e o teste específico confirma estado `in_progress`, percentual de evento nulo e homologação formal 2/7. O arquivo é local; publicação remota não foi feita.

#### Pesquisa de APIs nativas para ACL do restore — 2026-10-24 13:15 BRT

Pesquisa de documentação primária delimitou opções sem adicionar dependência: no Windows, `GetSecurityInfo` inspeciona DACL/owner de um objeto por handle, mas exige `READ_CONTROL`; os handles do helper precisam solicitar esse direito antes da validação. A documentação da Microsoft classifica `NtCreateFile` como API interna sujeita a mudança, então o uso atual para abertura relativa também precisa de revisão de compatibilidade antes de ser base de produção. No Linux, `acl_get_fd` requer `libacl` e pode retornar `ENOTSUP`; não se deve presumir essa biblioteca instalada nem interpretar ACL indisponível como permissiva. `fstatfs` permite identificar alguns tipos de filesystem pelo descritor, mas uma lista de tipos conhecidos não prova localidade para todos os filesystems; a decisão fail-closed para tipos compartilhados/desconhecidos permanece necessária. Fontes: Microsoft Learn (`GetSecurityInfo`, `READ_CONTROL`, guidance on internal APIs), Linux man-pages (`acl_get_fd(3)`, `fstatfs(2)`) e Linux kernel docs (`ext4 extended attributes`).

Resultado: nenhuma alteração de runtime ou dependência; não foi escolhida implementação Linux de ACL porque a alternativa sem libacl exigiria parser nativo do formato xattr e validação por ABI/filesystem, ainda sem contrato testado. A pesquisa também não valida handles, ACL efetiva, corrida, Linux nativo ou produção. O ciclo `FileSystemRestoreTransaction` segue pathname-based; P0 aberto, formal 2/7 sem crédito. Próxima etapa é selecionar e testar a inspeção ACL por descritor nas plataformas suportadas e integrar o lifecycle todo numa única mudança vertical. Ver [[Sessões/2026-10-24-MulletaFlix-restore-acl-native-api-research-1315]] e [[Decisões/2026-10-24-restore-acl-trust-boundary]].

#### Revalidação do deadlock em imagens — 2026-10-24 07:24 BRT

O anexo registra `DELETE BaseItemImageInfos` concorrentes para o mesmo item, deadlock MariaDB 1213 e atraso do heartbeat; HTTP 200 não garante commit. `ItemPersistenceService` coordena os escritores relevantes por stripes locais e locks consultivos MariaDB. Foco unitário Release: 38/38; suíte `Jellyfin.Server.Implementations.Tests` recompilada Release: 1.607 aprovados, 75 ignorados, 0 falhas/1.682. Oito testes MariaDB selecionados compilaram, mas foram ignorados nesta execução sem conexão descartável; evidência multiprocesso anterior está registrada na sessão 06:24. Não se identificou o processo/binário do log; runtime instalado, produção, instalação e release não foram acessados. Homologação formal permanece 2/7 (28,6%), sem crédito. Ver [[Sessões/2026-10-24-MulletaFlix-image-deadlock-regression-0724]].

#### Revalidação específica do anexo MariaDB — 2026-10-24 07:55 BRT

Foco unitário Release em Windows/.NET 10: `ItemPersistenceServiceTests` e `ItemPersistenceDatabaseLockTests` passaram 38/38. Os três cenários de concorrência (refresh simultâneo do mesmo item, exclusão durante refresh e bloqueio entre sessões) passaram 3/3 contra MariaDB 11.4.4 descartável, com datadir temporário e loopback na porta 33127. O servidor MariaDB ativo do MulletaFlix em `127.0.0.1:3306` foi identificado pelo PID 12056 e permaneceu intocado; a porta efêmera foi fechada ao final. Os arquivos temporários de banco/log continuam inativos no `%TEMP%`: a ferramenta bloqueou a limpeza recursiva, e nenhuma alternativa foi usada para contornar o bloqueio. `node --test portal-site/tests/*.test.mjs` passou 97/97. A regressão confirma a contenção no checkout contra MariaDB isolado, mas não identifica o binário/runtime do log nem valida produção; homologação formal 2/7 (28,6%), sem crédito. Sem instalação ou release. Ver [[Sessões/2026-10-24-MulletaFlix-image-deadlock-runtime-validation-0748]].

Reconciliação do runtime, somente leitura: assemblies em `C:\Program Files\MulletaFlix\Server` reportam 12.1.35, enquanto o `SharedVersion.cs` do checkout reporta 12.1.32; SHA-256 do `MulletaFlix.Server.Implementations.dll` instalado difere do build local 12.1.32. O endpoint público local `127.0.0.1:8096/System/Info/Public` não respondeu. Isso prova divergência de artefatos, não que o assembly instalado esteja sem locks, nem qual binário produziu o log. Nenhum runtime/serviço foi alterado.

Reconciliação com GitHub: `git ls-remote origin main` retornou `d51f4f38d2b4148ab49394c001567ad273bd57aa`, que coincide com `origin/main`, tag `v12.1.35`, e a versão dos assemblies instalados. Leitura direta do arquivo em `origin/main` confirma que `ItemPersistenceService` não usa advisory lock e que o helper `ItemPersistenceDatabaseLock.cs` não existe nesse commit. A cópia de trabalho testada continua 8 commits atrás de `origin/main`, com 139 paths modificados, incluindo alterações não relacionadas; seu `SharedVersion.cs` está em 12.1.32. Inferência sustentada: a instalação provavelmente usa o baseline sem a correção local, mas o endpoint da aplicação não respondeu e o assembly efetivamente carregado não pôde ser identificado. Não integrar/release a árvore inteira por cima do remoto sem reconciliar os 139 paths.

#### Novo anexo de deadlock e reconciliação do runtime — 2026-10-24 11:47 BRT

O anexo recebido novamente mostra múltiplas threads executando `DELETE BaseItemImageInfos` para o mesmo `ItemId` e MariaDB `1213`; depois há atraso do heartbeat do Kestrel. `Status Code 200` é de uma resposta anterior e não comprova commit da persistência.

Leitura local, sem mutação: o assembly instalado `C:\Program Files\MulletaFlix\Server\MulletaFlix.Server.Implementations.dll` tem versão 12.1.35; o `SharedVersion.cs` do checkout declara 12.1.32. Em `origin/main`, `SaveImagesAsync` ainda faz DELETE/INSERT sem `ItemPersistenceDatabaseLock`; o helper não existe no remoto. No checkout sujo, `SaveImagesAsync` já adquire stripe intra-processo e advisory lock MariaDB por item; `ItemPersistenceServiceTests` Release recompilado nesta sessão passou 31/31. Evidência anterior da branch local inclui MariaDB descartável com refresh concorrente e refresh-vs-delete, mas este anexo não identifica PID/assembly e não se correlaciona conclusivamente ao processo instalado.

Conclusão: a correção de serialização está no checkout, não na versão instalada nem em `origin/main`, portanto não se pode afirmar corrigido em produção. O worktree tem 144 paths modificados/não rastreados e está 8 commits atrás; não é seguro publicar a árvore inteira como release. Próximo gate: reconciliar mudanças por escopo, integrar a correção de persistência com testes MariaDB descartáveis e preparar artefato versionado; não tocar na MariaDB ativa/instalação sem autorização de implantação. Teste desta sessão: `dotnet test tests/Jellyfin.Server.Implementations.Tests/Jellyfin.Server.Implementations.Tests.csproj --configuration Release --filter FullyQualifiedName~ItemPersistenceServiceTests --no-restore` — 31/31 aprovados, 0 ignorados/falhas. Formal 2/7 (28,6%), sem crédito; portal apenas local, sem release/publicação remota.

#### Regressão de solução e reauditoria independente do restore — 2026-10-24 11:56 BRT

`dotnet test MulletaFlix.sln --configuration Release --no-restore --logger 'console;verbosity=minimal' -m:1` recompilou e passou em Windows/.NET 10: 17 projetos, 5.272 aprovados, 265 ignorados, 0 falhas/5.537 resultados. `ItemPersistenceServiceTests` recompilado Release passou 31/31. `IntroSkipper.Integration.Tests` passou 14 e ignorou 31 testes opt-in; não acessou a MariaDB ativa. Os ignores também incluem Linux/macOS e provedores live.

Dois rastreamentos read-only independentes confirmaram: o helper `BackupRestoreDirectory` não valida ACL/owner e não participa de `FileSystemRestoreTransaction`; apply/rollback/cleanup e replay ainda usam pathname. O journal tem operações parcialmente relativas a handles, mas `JournalExists` e algumas verificações/identificação do ZIP resolvem pathname. `AcquireOperationLease` pode criar sidecars antes da validação do arquivo de backup. Linux tem janelas check-then-act em criação, rename e delete. Restore P0 não está fechado. Formal 2/7 (28,6%), sem crédito; nenhum runtime, produção, instalação, release ou portal remoto alterado. Worktree permanece 144 paths sujos e 8 commits atrás; precisa reconciliação antes de release.
`FullSystemBackup` Release recompilado passou 91, ignorou 9 Linux/macOS e falhou 0/100 em Windows/.NET 10. A primeira execução expôs fixtures com `ProgramData` ausente; após alinhar fixtures com o bootstrap documentado, a regressão passou. As verificações finais de formatação, testes do portal e `git diff --check` também passaram. Nenhuma ACL efetiva, corrida TOCTOU integrada, Linux nativo, instalação, produção ou release foi validada; P0 segue aberto e homologação formal 2/7 sem crédito.

#### Regressão focal de backup/restore — 2026-10-24 12:50 BRT

`dotnet test tests/Jellyfin.Server.Implementations.Tests/Jellyfin.Server.Implementations.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~FullSystemBackup --logger "console;verbosity=minimal"` recompilou em Windows/.NET 10 e terminou com 91 aprovados, 10 ignorados e 0 falhas (101). Os ignores são cenários Linux/macOS; logo esta rodada não executou o backend Linux nativo. A auditoria da fonte e o teste não demonstram que `FileSystemRestoreTransaction` esteja vinculada a handles nem que ACL/owner sejam validados: apply, rollback, replay e cleanup permanecem pathname-based no checkout observado. Não há evidência nova de fechamento do TOCTOU/ACL P0. Homologação formal permanece 2/7 (28,6%), sem crédito. Nenhum runtime, instalação, produção, release ou portal remoto foi alterado.
#### Reauditoria do lifecycle de restore — 2026-10-24 04:53 BRT

Revisão independente confirmou que `FileSystemRestoreTransaction` ainda usa pathname em `ApplyEntryAsync`, `EnsureDirectoryExists`, `Rollback`, `CleanupTemporaryFiles` e `RemoveCreatedDirectories`; replay reutiliza a mesma transação. `BackupRestoreDirectory` não tem caller nesse lifecycle. O helper também não valida ACL efetiva e não expõe todo o estado/operações atômicas exigidos para coordenar transação e sidecars. Foram mapeadas oito famílias de testes adversariais: troca de raiz/ancestral, concorrência no target, sidecars `.tmp`/`.bak`/`.absent`, corridas internas das primitivas Linux, rollback, limpeza e ACL permitida/proibida.

Regressão atual reexecutada: `FullSystemBackup` Release 91 aprovados, 9 ignorados por plataforma e 0 falhas/100 no Windows/.NET 10. `wsl --list --quiet` confirmou WSL não instalado; portanto Linux nativo continua sem execução. Nenhum código de runtime mudou. Homologação formal 2/7 (28,6%), sem crédito. P0 continua aberto; sem restore adversarial integrado, ACL efetiva, runtime/produção ou release.
#### Reauditoria após confirmação independente — 2026-10-24 12:30 BRT

Duas inspeções independentes e a leitura atual de `BackupService.cs` confirmam que `BackupRestoreDirectory` ainda não governa a transação de arquivos. `FileSystemRestoreTransaction` mantém operações por pathname em aplicação, rollback, replay e limpeza; os métodos públicos de restore convergem nesse fluxo. A política decidida requer ACL/owner efetivos validados por handle, permissões apenas para identidade do serviço e root/administradores, e falha fechada para paths compartilhados ou inconclusivos. Nenhuma dessas garantias está implementada para o lifecycle completo. Não integrar somente a fase apply: isso deixaria rollback, replay e cleanup sujeitos à mesma corrida.

O anexo de 23/10/2026 também foi revalidado: contém deadlocks MariaDB 1213 durante `DELETE BaseItemImageInfos`, mas não identifica PID/assembly; o código de trabalho possui locks local e consultivo, o que não comprova a versão instalada. Nesta sessão, os testes focados `ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests` passaram 40/40; não são testes de restore. Não foram executados testes de restore nesta rodada, Linux nativo, ACL real, runtime instalado, banco operacional ou produção. A homologação formal permanece 2/7 (28,6%), sem crédito.

Próxima ação técnica: implementar autoridade/ACL e operações por handle como um lifecycle único para as cinco raízes de destino, o arquivo de backup e a área de recovery; acrescentar cobertura fail-closed e concorrente; validar nativamente em Windows e Linux. Até então o risco P0 continua aberto e não há base para release integral.

#### Validação local da fronteira ACL do helper — 2026-10-24 13:42 BRT

`BackupRestoreAclValidator` agora lê owner/DACL do handle do destino Windows e owner, modo, ACL POSIX e tipo de filesystem pelo descritor Linux; desconhecido, ACL ausente/inconclusiva e filesystem fora da allowlist falham fechados. A regra verifica o diretório de restore aberto, não os ancestrais: eles não concedem escrita sobre a autoridade já aberta e validá-los rejeitava o volume `C:\` do Windows por owner de sistema e permissões de criação de diretórios. Isso não altera a política de escrita no destino. Dois testes locais cobrem aceitação de fixture privada e rejeição de writer amplo no Windows.

Verificações nesta sessão: foco dos dois testes 2/2; `BackupRestoreDirectoryTests` 25 aprovados, 7 ignorados, 0 falhas/32, Windows/.NET 10. Os sete ignores são testes Linux; parser ACL Linux, montagem/localidade real, integração no `FileSystemRestoreTransaction`, ACL de staging/journal/origem ZIP, produção e restore completo ainda não foram validados. A validação existe no helper mas nenhum caller de restore a invoca; portanto não é gate operacional. P0 aberto; homologação formal 2/7 (28,6%), sem crédito. Sem instalação, release ou publicação remota.

#### Regressão Release do componente — 2026-10-24 13:48 BRT

Após a implementação do helper ACL, a suíte `FullyQualifiedName~FullSystemBackup` foi recompilada em Release no Windows/.NET 10: 93 aprovados, 10 ignorados, 0 falhas/103. Os ignorados incluem cenários Linux/macOS; logo não há validação nativa do parser ACL Linux ou filesystem. O teste não fecha a integração: `FileSystemRestoreTransaction` ainda altera arquivos por pathname e não chama `ValidateTrustedWriteBoundary`. Journal/lease e origem ZIP também precisam participar de uma única autoridade validada. P0 aberto, formal 2/7 (28,6%), sem crédito ou release.

#### Regressão ACE herdada só para subdiretórios — 2026-10-24 13:56 BRT

O parser Windows agora considera `CONTAINER_INHERIT` junto de `OBJECT_INHERIT` ao avaliar ACE `INHERIT_ONLY`, evitando liberar um writer não confiável que só herda a permissão nos diretórios descendentes. O teste adversarial aplica `Modify` para `WorldSid` com `ContainerInherit | InheritOnly` e exige `UnauthorizedAccessException`. Foco `ValidateTrustedWriteBoundary`: 3/3; suíte `FullyQualifiedName~FullSystemBackup` Release recompilada: 94 aprovados, 10 ignorados, 0 falhas/104 em Windows/.NET 10.

Isso só corrige um falso negativo do helper. Não há caller no `FileSystemRestoreTransaction`, ACL de cada descendente/arquivo não é validada e apply/rollback/replay/cleanup seguem pathname-based; restore P0 continua aberto. O novo anexo de deadlock repete erros MariaDB 1213 na tabela de imagens. Revisão de escritores confirmou locks compartilhados no working tree, porém ausentes em `HEAD`; hipótese principal é binário/processo antigo ou misto, não conclusiva pois o log não fornece PID/assembly. A mudança ACL não corrige esse incidente. Formal 2/7 (28,6%), sem crédito, produção, release ou publicação remota. Mapa técnico conferido e estruturalmente igual. Ver [[Sessões/2026-10-24-MulletaFlix-restore-acl-inherit-only-and-db-log-1356]].

#### Teste de limpeza do restore com sincronização determinística — 2026-10-24 14:10 BRT

O teste `RestoreBackupAsync_PreservesJournalWhenSidecarCleanupFails` não usa mais `FileSystemWatcher` para tentar abrir um `.bak` transitório: um callback interno opcional é chamado imediatamente após o rename original→backup, e a fixture mantém o handle aberto até a falha de limpeza e replay. O callback é fornecido apenas pelo construtor interno de teste; a chamada normal do servidor permanece nula. Foco repetido 10/10; suíte `FullSystemBackup` Release recompilada: 94 aprovados, 10 ignorados, 0 falhas/104 em Windows/.NET 10. Os ignorados incluem cenários Linux/macOS. Isto melhora apenas determinismo do teste: apply/rollback/replay/cleanup ainda operam por pathname, ACL não está integrada e P0 permanece. Nenhum runtime, banco, instalação, produção ou release foi alterado. Portal local atualizado; formal 2/7 (28,6%), sem crédito.

#### Regressão build-inclusive de Server Implementations — 2026-10-24 14:15 BRT

`dotnet test tests/Jellyfin.Server.Implementations.Tests/Jellyfin.Server.Implementations.Tests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal" -m:1` recompilou o projeto e terminou exit 0 no Windows/.NET 10: **1.613 aprovados, 76 ignorados, 0 falhas (1.689)**. Os ignorados incluem MariaDB/MongoDB opt-in, casos Linux/macOS e benchmarks; nenhum banco foi acessado nesta rodada. A suíte não fecha cobertura do restore inseguro: `FileSystemRestoreTransaction` ainda usa pathname e não invoca o helper ACL. Restore P0 permanece, formal 2/7 (28,6%) sem crédito. Sem runtime, instalação, produção, release ou publicação remota.

#### Reauditoria da máscara ACL Windows e contrato do helper — 2026-10-24 14:23 BRT

A hipótese de que `FILE_ADD_SUBDIRECTORY` não estava na máscara era falsa: no Windows, o bit `0x4` representa `FILE_ADD_SUBDIRECTORY` em diretório e `FILE_APPEND_DATA` em arquivo; `WindowsWriteRights` já inclui `FileAppendData`. `ValidateTrustedWriteBoundary_RejectsUntrustedDirectoryCreationOnWindows` confirma que um baseline privado passa e uma ACE `Everyone/CreateDirectories` é rejeitada. `BackupRestoreDirectoryTests` Release: 27 aprovados, 7 ignorados por Linux, 0 falhas/34; `FullSystemBackup`: 95 aprovados, 10 ignorados por Linux/macOS, 0 falhas/105, Windows/.NET 10. Comentários corrigidos: o helper inspeciona somente o handle atual; ancestrais ficam retidos, não validados, conforme a decisão registrada. Não houve mudança de runtime. A transação ainda não chama o helper e segue pathname-based em apply/rollback/replay/cleanup; restore P0 aberto, formal 2/7 sem crédito. Linux nativo, produção, instalação, release e portal remoto não validados/publicados.

#### Integração do restore por handles retidos — 2026-10-24 15:01 BRT

`FileSystemRestoreTransaction` passou a abrir e reter autoridades de diretório, validar o diretório final antes de mutar e fazer apply/replay, marker, staging, troca original, rollback, limpeza de sidecars e remoção de diretórios por nomes relativos aos handles. Falha de cleanup preserva journal para replay. Fixtures de restore usam raiz privada com ACL explícita. `FullSystemBackup` Release recompilado no Windows/.NET 10: 95 aprovados, 10 ignorados por plataforma/ambiente, 0 falhas/105. Linux nativo, ACL adversarial integrada, concorrência de namespace, runtime instalado e produção não foram validados; P0 permanece aberto e homologação formal continua 2/7 (28,6%), sem crédito.

O anexo MariaDB mostra `DELETE BaseItemImageInfos` com deadlock 1213 repetido. A fonte local contém coordenação por stripe e advisory `GET_LOCK`, mas o log não identifica PID/assembly; auditoria anterior encontrou `origin/main`/runtime observado 12.1.35 sem a proteção no baseline remoto. Isso não confirma que o processo com erro contém a correção. Nenhuma conexão à base operacional foi feita. Sem release ou instalação.

#### Gate integrado de ACL no restore — 2026-10-24 15:05 BRT

Novo teste `RestoreBackupAsync_RejectsUntrustedDestinationAclBeforeChangingExistingFile` aplica um grant de escrita não confiável à pasta de configuração e confirma que o restore lança `UnauthorizedAccessException` sem substituir o arquivo anterior. Em Windows/.NET 10, `FullSystemBackup` Release passou 96, ignorou 10 Linux/macOS/ambiente e falhou 0/106. Isso prova enforcement ACL integrado no cenário Windows testado, não fecha o P0: Linux nativo não disponível, corrida de namespace e ACLs adversariais adicionais ainda pendentes. Homologação formal 2/7 (28,6%), sem crédito.
#### Consulta de existência falha fechada em acesso negado — 2026-10-24 15:26 BRT

`BackupRestoreDirectory.FileExists` não trata mais Win32 `ERROR_ACCESS_DENIED` (5) como arquivo ausente: propaga `IOException` com a exceção nativa como inner exception. Isso evita que `ApplyEntryAsync`/rollback suponham ausência e deixem de preservar um arquivo original quando não conseguem inspecioná-lo. O teste adversarial Windows `FileExists_DoesNotTreatAccessDeniedAsMissingFileOnWindows` passou 1/1; `FullSystemBackup` Release recompilado passou 97, ignorou 10 e falhou 0/107 em Windows/.NET 10. Os ignorados incluem cenários Linux/macOS/ambiente. A mudança é local e não valida filesystem/ACL Linux, corrida de namespace, instalação ou produção. O ciclo completo do restore ainda exige gates P0 pendentes; homologação formal permanece 2/7 (28,6%), sem crédito ou release.
#### ACL obrigatória para journal e lease de restore — 2026-10-24 15:32 BRT

`BackupRestoreRecoveryJournal` agora centraliza a abertura do estado de recuperação em `OpenRecoveryDirectory`. O método valida por handle a fronteira de confiança de `ProgramDataPath` e `restore-recovery` antes de lease, leitura, escrita, exclusão ou consulta. `JournalExists` não usa mais `File.GetAttributes` por pathname; usa `FileExists` relativo ao handle validado. Testes Windows adversariais com `WorldSid/Modify` em cada diretório confirmaram que consulta, lease e mutações falham fechados e que o journal original permanece intacto. Foco 2/2; `FullSystemBackup` Release recompilado: 99 aprovados, 10 ignorados, 0 falhas/109 em Windows/.NET 10. Contrato do portal local passou 132/132; JSON, JavaScript e `git diff --check` passaram. Linux nativo, ACL real de instalação, corridas de namespace, runtime e produção não foram validados. Races Linux `statx`→`renameat/unlinkat` persistem; restore P0 permanece aberto, formal 2/7 (28,6%), sem crédito ou release.

#### Rollback incompleto ao não remover diretório criado — 2026-10-24 15:45 BRT

O teste `RestoreBackupAsync_PreservesJournalWhenCreatedDirectoryParentBecomesUntrusted` reproduziu uma falha concreta: o callback adiciona `WorldSid/Modify` ao diretório pai depois do primeiro arquivo aplicado e força uma falha de restore. O rollback restaura os arquivos, mas `RemoveCreatedDirectories` não consegue validar a fronteira do pai. Antes da correção, o método registrava a pasta remanescente, mantinha `RollbackSucceeded` verdadeiro e o serviço apagava o journal; o teste falhou em `Assert.NotNull`. Agora os caminhos de `UnauthorizedAccessException` e de `IOException`/`UnauthorizedAccessException` também definem `succeeded = false`, fazendo o serviço preservar o journal para replay.

O teste focal Release passou 1/1; `FullSystemBackup` Release recompilado em Windows/.NET 10 passou com 100 aprovados, 10 ignorados por plataforma/ambiente e 0 falhas/110. Busca de código confirmou um único caller de `RemoveCreatedDirectories` (`Dispose`) e nenhum outro caminho correspondente de limpeza do restore que omita a falha. Linux nativo, ACL operacional, outras races, runtime instalado e produção continuam pendentes; o restore P0 não fecha e homologação permanece 2/7 (28,6%), sem crédito ou release.

#### Reauditoria adversarial de namespace Linux — 2026-10-24 16:11 BRT

Leitura da implementação confirmou três janelas condicionais de troca de entrada em `BackupRestoreDirectory`: `OpenOrCreateDirectory` chama `mkdirat` e depois abre pelo nome; `Rename` consulta tipo sem seguir symlink e depois chama `renameat`/`renameat2`; `DeleteEntry` consulta tipo e depois chama `unlinkat`. Se outro processo puder alterar o diretório-pai aberto nesse intervalo, o nome pode apontar para entrada diferente. O rename não segue symlink, mas pode mover a entrada symlink substituta para o destino; o unlink pode remover a entrada substituta. Não foi construída nem executada uma exploração determinística.

`ValidateTrustedWriteBoundary` valida o diretório-alvo aberto, não os ancestrais retidos, e `OpenTargetDirectory` valida o handle final após percorrer/criar componentes. A documentação existente adverte que validar ancestrais integralmente rejeita caminhos Windows comuns, incluindo a raiz de volume; portanto não basta ampliar cegamente a mesma validação para cada ancestral. O gate precisa proteger a autoridade dos pais e entradas mutáveis sem quebrar caminhos suportados, usando o contrato aprovado de service + root/admins e falhando fechados para diretórios compartilhados sem garantia.

`FullSystemBackup` Release passou 100 aprovados, 10 ignorados por plataforma/ambiente, 0 falhas/110 em Windows/.NET 10; os 10 ignorados impedem afirmar validação Linux. O host não tem WSL. As corridas seguem como achado condicional não reproduzido; restore P0 aberto, homologação formal 2/7 (28,6%), sem crédito, sem runtime/produção/release.

#### ACL do namespace dos pais antes de mutações — 2026-10-24 16:24 BRT

`BackupRestoreDirectory.OpenRoot` agora valida por handle cada ancestral não-raiz antes de atravessar para o próximo componente: Windows ignora somente a âncora da raiz do volume; Linux ignora somente `/`. Em Windows, a ACL de namespace permite a terceiros apenas `FILE_ADD_SUBDIRECTORY` em pais, necessário para raízes comuns como `C:\Users`; grants que permitam remover/substituir entradas, alterar ACL/owner, owner não confiável, DACL ausente ou compartilhamento são recusados. O handle final de destino mantém a regra mais restrita de escrita. No Linux, pais usam owner UID do serviço/root, modo, ACL access/default e allowlist de filesystem local; compartilhados/inconclusivos falham fechados.

O restore valida o pai imediatamente antes de criar componente ausente, o journal faz o mesmo antes de criar `restore-recovery`, e rollback exige gate do pai antes de remover diretório criado. Novo teste Windows exercita “criar filho permitido, delete-child negado”; teste integrado exige falha antes de criar destino com pai gravável por terceiros. `FullSystemBackup` Release passou 102, ignorou 10 (Linux/macOS/ambiente), falhou 0/112 em Windows/.NET 10. Uma execução anterior falhou inicialmente por fixture sem caminhos de storage configurados; a fixture foi corrigida e a repetição completa passou. Linux nativo e corrida adversarial concorrente não foram executados; janelas de rename/unlink por nome seguem abertas, portanto P0 não fecha. Formal permanece 2/7 (28,6%), sem crédito; nenhuma instalação, produção, release ou publicação remota.

#### ACL da âncora do filesystem antes da travessia — 2026-10-24 16:32 BRT

Revisão adversarial detectou que `OpenRoot` validava ancestrais somente depois do primeiro componente: a raiz do volume Windows e `/` Linux eram puladas. Agora a âncora também passa pelo gate por handle antes da abertura do primeiro componente. Windows mantém a verificação de DACL/grants; somente o owner TrustedInstaller é adicionalmente aceito para a âncora do volume, porque `C:\` nesta máquina é de `NT SERVICE\TrustedInstaller`. Linux usa o mesmo owner/mode/ACL/type gate em `/`. `BackupRestoreDirectoryTests` Release Windows/.NET 10: 29 aprovados, 7 ignorados por plataforma, 0 falhas/36; execução inicial expôs corretamente a incompatibilidade do owner da raiz, corrigida com exceção limitada à âncora.

Pesquisa em Linux man-pages confirma que `STATX_MNT_ID` identifica qual mount contém um descritor e corresponde a `/proc/self/mountinfo`; isso não classifica sozinho origem local/remota nem prova segurança de overlay/bind. A allowlist `fstatfs` ainda pode aceitar overlay cujo backing seja remoto; essa fronteira segue pendente, assim como Linux nativo e races de mutação por nome. Teste Windows Release do helper: 29/36, sete Linux/macOS ignorados; `FullSystemBackup` completo: 102 aprovados, 10 ignorados, 0 falhas/112. Testes do portal: 138/138; JSON válido; `git diff --check` passou com avisos de normalização CRLF/LF. O gate é progresso local, não crédito de homologação: formal permanece 2/7 (28,6%), restore P0 aberto, sem runtime, instalação, produção ou release. Portal atualizado e teste de contrato adicionado.

#### Regressão Windows e auditoria de CI Linux — 2026-10-24 16:42 BRT

Reexecutei `dotnet test tests/Jellyfin.Server.Implementations.Tests/Jellyfin.Server.Implementations.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~FullSystemBackup" --logger "console;verbosity=minimal"`: **102 aprovados, 10 ignorados, 0 falhas (112)** em Windows/.NET 10. Os ignorados cobrem Linux/macOS; isto não valida restore Linux.

O workflow `.github/workflows/ci.yml` roda `server-build-test` em `ubuntu-latest`, incluindo testes unitários fora de `Integration`, portanto os cenários Linux de `FullSystemBackup` estão configurados para CI. A execução remota mais recente observada foi CI run `39608614450`, SHA `d51f4f38d2b4148ab49394c001567ad273bd57aa`, conclusão `failure`; jobs terminaram sem steps observáveis e download do log do job `Server build & unit tests` respondeu 404 `BlobNotFound`. Não se pode inferir que os testes Linux passaram ou falharam, nem a causa do run. WSL e Docker não estão instalados neste host.

Restore P0 permanece aberto: validar CI Linux em execução observável, resolver risco de backing overlay/bind remoto e concluir análise/testes de corrida. Homologação formal permanece 2/7 (28,6%), sem crédito; sem release/instalação/produção. O workflow e a arquitetura não foram alterados nesta sessão.

#### Reexecução da regressão no checkout principal — 2026-10-24 18:01 BRT

No checkout principal atual, `dotnet test tests/Jellyfin.Server.Implementations.Tests/Jellyfin.Server.Implementations.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~FullSystemBackup --verbosity quiet -m:1` terminou exit 0: **102 aprovados, 10 ignorados, 0 falhas (112)** em Windows/.NET 10. Os ignorados incluem testes Linux/macOS e dependentes de ambiente; não constituem evidência de execução Linux. `wsl --status` confirmou que WSL não está instalado e `docker` não está disponível. Nenhum runtime Linux foi instalado; não houve acesso a dados de produção. O checkout principal está oito commits atrás de `origin/main` e contém alterações locais preexistentes, preservadas.

O teste confirma somente a regressão Windows. Não valida syscalls Linux, ABI, ACL/filesystem efetivos ou races de namespace em runtime Linux; o gate P0, formal 2/7 (28,6%) sem crédito, instalação, produção e release seguem pendentes. O portal local foi atualizado e o contrato cobre que os dez skips não sejam apresentados como validação Linux.

## Regressão global da solução — 2026-10-24 21:50 BRT

`dotnet test MulletaFlix-master\MulletaFlix.sln --configuration Release --no-build --no-restore --filter 'FullyQualifiedName!~LiveIntegrationTests' --verbosity quiet -m:1` terminou com exit code 0 em Windows/.NET 10: 5.286 aprovados, 255 ignorados e 0 falhas em 17 projetos, 5.541 resultados. Integrações live foram excluídas por filtro explícito; skips incluem testes condicionados a MariaDB/MongoDB e plataforma. A primeira execução build-inclusive exibiu warnings de documentação XML/analisadores, sem erro de compilação observado; contagens/exit code finais vêm da reexecução sem rebuild. Isso não demonstra runtime, serviços reais, Linux/ACL nem provedores live. Homologação formal permanece 2/7 (28,6%), sem crédito.

#### Reexecução Windows e disponibilidade de runtime Linux — 2026-10-24 21:31 BRT

Nova execução do filtro `FullyQualifiedName~FullSystemBackup` passou 105, ignorou 10 e falhou 0 (115) em Windows/.NET 10 Release. Os skips incluem testes Linux/macOS. Nesta sessão, `wsl --status` confirmou que o WSL não está instalado; `Get-Command docker,podman,bash` não encontrou alternativas locais. Resultado registra regressão Windows, não valida ACL POSIX, syscalls ou races Linux. Nenhuma alteração de código/produção; restore P0 continua aberto e não recebe crédito formal.

#### Reauditoria do fluxo atual de restore e handles — 2026-10-24 22:03 BRT

A conferência do checkout atual confirma integração do `BackupRestoreDirectory` em `FileSystemRestoreTransaction` e `BackupRestoreRecoveryJournal`: apply, rollback, replay, staging/sidecars e journal usam operações relativas a handles; há validação da fronteira de escrita e arquivos existentes. Portanto, registros históricos anteriores dizendo que o helper não tinha callers são superados pelo estado atual do código.

Regressão recompilada `dotnet test tests/Jellyfin.Server.Implementations.Tests/Jellyfin.Server.Implementations.Tests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~FullSystemBackup' --logger 'console;verbosity=minimal' -m:1`: exit 0, 105 aprovados, 10 ignorados, 0 falhas/115 em Windows/.NET 10. O build exibiu warnings de analisador/estilo; não foram corrigidos neste escopo.

Contratos locais do portal passaram 161/161; `homologacao-status.json` parseou, `node --check portal-site/homologacao.js` passou e `git diff --check` não apontou whitespace error.

O backend Linux ainda contém janelas condicionais entre inspeção de entrada por nome e mutação: `Rename` verifica tipo antes de `renameat`/`renameat2`, e `DeleteEntry` verifica antes de `unlinkat`. Nenhuma race foi reproduzida nesta sessão; os casos Linux/macOS foram ignorados e não há runtime Linux observado. Restore P0 permanece aberto até validação Linux e teste adversarial determinístico; homologação formal 2/7 (28,6%), sem crédito. Sem runtime instalado, produção, release ou publicação remota.

#### Revalidação da origem ZIP e reconciliação de achados antigos — 2026-10-24 23:10 BRT

Nova leitura de `RestoreBackupCoreAsync` e `OpenTrustedRestoreArchive` confirmou que o ZIP é aberto relativamente ao pai validado, sem seguir reparse point/symlink; `ValidateTrustedFile` consulta a ACL/owner do mesmo handle usado para validar e consumir o arquivo. O caller passa `FileShare.Read`, bloqueando novas aberturas compatíveis para escrita/remoção no Windows enquanto o restore mantém o handle. Linux abre por `openat` sem seguir link e valida tipo/owner/mode/ACL POSIX pelo descritor. O teste integrado `RestoreBackupAsync_RejectsUntrustedArchiveAclBeforeCreatingRecoveryJournal` passou 1/1 em Windows/.NET 10 Release. A regressão completa `FullyQualifiedName~FullSystemBackup` passou 105, ignorou 10 e falhou 0 (115), usando binários já compilados; os skips cobrem casos Linux/macOS/ambiente e não são evidência de execução Linux.

Isso supersede achados anteriores que descreviam o ZIP atual como pathname-only, sem ACL de origem ou compartilhamento de escrita no Windows; essas afirmações não descrevem a fonte atual. Revisão independente confirmou que no Linux um processo com o UID confiável do serviço ainda poderia alterar o mesmo arquivo em paralelo; não há teste de concorrência por mesmo UID e não foi reproduzido. Isso é limite entre identidades consideradas confiáveis pela política, não bypass demonstrado por usuário não confiável. O compartilhamento Windows não prova concorrência Linux, nem ACL efetiva no serviço instalado. Races condicionais do namespace Linux, CI observável e runtime/produção continuam pendentes; P0 e homologação formal 2/7 (28,6%) permanecem abertos, sem crédito.

#### Reauditoria de gravação de letras — 2026-10-24 23:07 BRT

`LyricManager.IsPathWithinDirectory` rejeitava reparse points abaixo da raiz, mas não inspecionava a própria raiz canonicalizada. Uma pasta configurada como symlink podia, portanto, passar no gate lexical/descendente antes de `TrySaveToFiles` gravar por pathname. A verificação agora recusa a raiz quando ela própria é reparse point; `IsPathWithinDirectory_RejectsSymbolicLinkAsConfiguredRoot` reproduz o caso. `LyricManagerPathSafetyTests` passou 22/22 em Windows/.NET 10 Release, sem ignorados/falhas. O build da suíte emitiu warnings StyleCop/xUnit preexistentes em outros testes.

Limite: isto fecha o caso estático da raiz-link; não torna validação e gravação atômicas e não elimina TOCTOU por troca concorrente, nem valida ACL/runtime Linux. A árvore ancestral da raiz configurada não foi revisada nesta correção. Nenhuma release, instalação, produção ou validação Linux ocorreu. Homologação formal permanece 2/7 (28,6%), sem crédito.

#### Persistência de namespace do restore — 2026-10-25 06:07 BRT

O helper `BackupRestoreDirectory` agora força persistência das mudanças de namespace que sustentam o journal e os sidecars: novos arquivos/diretórios, rename e remoções usam `FILE_WRITE_THROUGH` nos handles mutáveis do Windows; em Linux, o diretório-pai é reaberto com `openat` a partir do handle de autoridade e sincronizado com `fsync` após cada mutação. Isso inclui a publicação atômica do journal e a ordem dos renames de backup/staging/rollback. O teste ACL de pai também confirma que terceiros podem criar apenas o diretório do namespace, não arquivos nem substituir/remover entradas.

Validação Windows/.NET 10 Release: `BackupRestoreDirectoryTests` 30 aprovados, 7 ignorados, 0 falhas; `FullSystemBackup` 105 aprovados, 10 ignorados, 0 falhas. Ignorados são casos de plataforma/ambiente; WSL não está instalado, portanto o caminho Linux (`openat`/`fsync`) não foi executado. Sem teste de queda de energia em filesystem/hardware real; write-through/fsync solicitam durabilidade, não provam que controladora/dispositivo honrem flush. Restore P0 e homologação formal 2/7 (28,6%) seguem abertos, sem crédito; nenhuma release, instalação ou produção.

#### Revalidação Windows e disponibilidade Linux — 2026-10-25 07:43 BRT

No checkout local atual, `dotnet test tests/Jellyfin.Server.Implementations.Tests/Jellyfin.Server.Implementations.Tests.csproj -c Release --filter FullyQualifiedName~FullSystemBackup --no-restore` recompilou e passou: **107 aprovados, 10 ignorados, 0 falhas (117)** em Windows/.NET 10. Os ignorados incluem testes de syscalls/ACL Linux e macOS; não são evidência Linux. `wsl.exe` está presente, mas `wsl --status` informa que o Subsistema Windows para Linux não está instalado; Docker e Podman não foram encontrados. A leitura do helper confirma que `OpenOrCreateDirectory`, `Rename` e `DeleteEntry` ainda fazem verificação por nome antes da mutação Linux (`mkdirat/openat`, `statx/renameat*`, `statx/unlinkat`), sob a fronteira de ACL validada. Não houve execução nem reprodução determinística das corridas.

Resultado: regressão Windows verde; não é validação da ABI, ACL efetiva ou concorrência Linux. O risco P0 de restore permanece aberto, homologação formal permanece 2/7 (28,6%) sem crédito. Sem alteração de runtime, instalação, produção, release ou publicação remota. Próximo gate: executar suíte em runner Linux observável e testar concorrência/adversários dentro da fronteira de confiança, preservando o fail-closed para diretórios compartilhados/inconclusivos.

O teste de contrato do portal foi atualizado para exigir que o evento atual apareça na lista recente enquanto mantém o registro histórico consultável; a suíte local passou 184/184.

## Revalidação Windows do restore — 2026-10-25 20:18 BRT

- `dotnet test MulletaFlix-master/tests/Jellyfin.Server.Implementations.Tests/Jellyfin.Server.Implementations.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~BackupServiceTests|FullyQualifiedName~BackupRestoreDirectoryTests" --verbosity minimal`: 99 aprovados, 11 ignorados, 0 falhas/110 em Windows/.NET 10 Release.
- `dotnet test MulletaFlix-master/tests/IntroSkipper.Integration.Tests/IntroSkipper.Integration.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~BackupRestoreMariaDbTests" --verbosity minimal`: 2 aprovados, 2 ignorados, 0 falhas/4. A variável de conexão descartável não está definida; `127.0.0.1:33099` não tem listener. Nenhum banco operacional foi acessado.
- `wsl --status`/`wsl --list --quiet`: WSL não instalado. Docker e Podman não estão disponíveis; nenhuma instalação foi tentada.
- Resultado valida apenas os cenários Windows executados. Os skips de plataforma, MariaDB, runner Linux, CI remota e produção não recebem crédito; o gate P0 de restore continua aberto. O build de integração emitiu EF1002 em duas instruções SQL de trigger dos testes; os identificadores são gerados/escapados pela fixture, mas o aviso permanece para revisão.
- Portal: a primeira execução terminou 197/198 porque a nova assertion esperava contadores históricos de cobertura (328/65); o estado atual da fonte era 347/425, com 73 pendentes. Assertion alinhada ao estado já publicado, sem alterar os contadores; repetição passou 198/198.
- Sem mudança de código de runtime, release, instalação ou publicação. Homologação formal permanece 2/7, sem crédito.
## Falha explícita no startup após recovery de restore — 2026-10-25 08:26 BRT

`Program.StartServer` marca como falha de restore a resolução/validação de `IBackupService`, `RecoverInterruptedRestoreAsync` e o restore explícito solicitado. Se qualquer uma dessas etapas falhar, o catch define `Environment.ExitCode=1` e não aguarda o setup server por até 10 minutos. No caminho normal, o marcador é removido antes das migrações; outras falhas iniciais em modo MediaServer continuam com a espera anterior.

`ProgramStartupRecoveryTests` Release/.NET 10 passou 4/4; compilação emitiu 2 avisos SA1201. A revisão independente confirmou o lifetime do marcador, inclusive para serviço incompatível e restore explícito. Limite: o teste verifica apenas `ShouldWaitForSetupServerAfterStartupFailure`; não executa processo, não valida o valor observado do exit code nem espiona `InitializeServices`. A falha impede essa chamada pelo fluxo sequencial, ainda sem teste de integração. Runtime Windows/Linux, CI hospedado observável, produção e homologação formal seguem pendentes; formal permanece 2/7 (28,6%), sem crédito ou release. Nenhum banco operacional foi acessado.

Revalidação em 25/10/2026 08:33 BRT: `ProgramStartupRecoveryTests` Release passou novamente 4/4, sem skips/falhas. Mantém-se o limite: teste de predicado não prova exit code de processo nem ausência de chamada de inicialização por instrumentação.

Reavaliação em 25/10/2026 09:58 BRT: `ProgramStartupRecoveryTests` Release/.NET 10 recompilado passou 4/4, sem skips/falhas. A inspeção de `Program.StartApp` confirmou que MariaDB embutido, proxy HTTPS e migrações iniciam antes de `StartServer`; executar o processo completo como teste neste host não isolaria o restore e poderia conflitar com serviços locais. Nenhum executável/runtime instalado foi iniciado. O harness processual continua pendente: deve injetar falha de recovery em ambiente isolado, observar exit code não-zero e provar ausência de `InitializeServices`. CI remota, Windows/Linux em runtime instalado, produção e homologação formal não foram validados; formal 2/7 (28,6%), sem crédito ou release.

#### Rechecagem do log de deadlock repetido — 2026-10-25 08:35 BRT

O anexo recebido tem SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, igual ao log auditado anteriormente: múltiplos `DELETE BaseItemImageInfos` falham com deadlock MariaDB 1213 durante `SaveImagesAsync` no mesmo item; a mensagem inicial de HTTP 200 não comprova commit. Não há PID, assembly carregado ou resultado final adicional.

`ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests` recompilou e passou 40/40 em Windows/.NET 10 Release. GitHub Actions continua sem nova evidência: CI `39608614450` e release `39608677913` permanecem falhos, iniciados em 21/10/2026; nenhum runner Linux/teste hospedado posterior ficou observável. Portanto o resultado local não confirma que o runtime instalado contém o lock nem valida concorrência MariaDB nesta rodada. Não houve acesso ao banco operacional, instalação, produção, release ou publicação. Formal 2/7 (28,6%), sem crédito.
#### Rollback de restore no lifecycle das migrações — 2026-10-25 10:05 BRT

Auditoria de todas as anotações `MulletaFlixMigrationBackupAttribute` em `Jellyfin.Server/Migrations/Routines` encontrou somente `MulletaFlixDb=true` ou `LegacyLibraryDb=true`; nenhuma migração atual pede backup de `Metadata`, `Subtitles` ou `Trickplay`. `JellyfinMigrationService` só preenche `_backupKey.FullBackup` quando um desses recursos é solicitado; portanto seu fallback `IBackupService.RestoreBackupAsync` não é alcançável pela configuração atual. Rollback do banco MulletaFlix usa `RestoreBackupFast` e não o restore integral.

`Program.StartServer` chama recovery/restore solicitado antes das migrações e antes de `InitializeServices`. A etapa `AppInitialisation` executa depois de `InitializeServices`, mas antes de iniciar o `IHost` e `RunStartupTasksAsync`. Isso reduz a evidência de writers normais iniciados pelo host, porém não exclui efeitos laterais durante discovery de plugins; não houve execução de processo nem teste de concorrência. Não se atribui o deadlock 1213 a este caminho. Harness processual, runtime Windows/Linux, CI observável, instalação e produção permanecem pendentes; homologação formal 2/7 (28,6%), sem crédito ou release.

#### Gate de recovery isolado em subprocesso — 2026-10-25 10:28 BRT

`Program.StartServer` agora delega o limite entre recovery e inicialização de serviços ao gate `RunAfterStartupRecoveryAsync`; o callback de recovery conserva a recuperação interrompida e o restore solicitado, e o callback de inicialização conserva as migrações e `InitializeServices`. O novo probe console chama esse mesmo gate em processo filho. `ProgramStartupRecoveryTests` executa dois cenários: recovery falho encerra o processo com código 1 e sem criar o marcador de inicialização; recovery bem-sucedido encerra com código 0 e cria o marcador. Os quatro casos existentes do predicado de espera continuam cobertos.

Validação local Windows/.NET 10 Debug: `dotnet test tests/Jellyfin.Server.Tests/Jellyfin.Server.Tests.csproj --no-restore --filter FullyQualifiedName~ProgramStartupRecoveryTests` passou **6/6**, sem ignorados ou falhas. Isso verifica o gate de produção e o contrato observado de subprocesso, mas não inicia `StartApp`/runtime completo; MariaDB, proxy HTTPS, migrações, instalação e produção não são parte do probe. O caminho Linux ainda não foi executado. Homologação formal permanece 2/7 (28,6%), sem crédito; nenhuma release ou publicação remota.

## Arquivo de lease do restore — validação fail-closed — 2026-10-25 21:34 BRT

A auditoria encontrou que `AcquireOperationLease` abria/criava `restore-operation.lock` e chamava `FileStream.Lock` sem validar owner e ACL do próprio arquivo. O diretório de recovery validado não garante a segurança de um arquivo preexistente. Agora a lease passa por `ValidateTrustedFile` antes do lock. A regressão injeta escrita para `Everyone` no Windows ou `other` no Linux, confirma que a aquisição é negada antes do lock e que os bytes existentes não são alterados. O primeiro teste Windows revelou que `OpenOrCreateFile` não pedia `READ_CONTROL`; esse acesso foi adicionado para leitura da DACL pelo handle.

Validação focal Windows/.NET 10 Release: 1/1. `FullSystemBackup`: 115 aprovados, 11 ignorados por plataforma/ambiente, 0 falhas/126. `dotnet format style --verify-no-changes` passou nos três arquivos C# tocados. Isso comprova o cenário Windows e a compilação dos casos Linux, não execução Linux ou produção. Homologação formal 2/7 (28,6%), sem crédito; sem release ou publicação remota.
## Replay de restore com dois bancos participantes independentes — 2026-10-25 22:12 BRT

O E2E opt-in `RestoreBackupAsync_RestoresDatabaseRowsOnDisposableMariaDb` agora grava no archive o participante real `IntroSkipper` e um segundo participante de fixture em schema MariaDB independente. O processo filho interrompe o restore depois do commit do primeiro participante, antes do segundo; o teste verifica que o segundo ainda contém estado pós-backup. Um processo novo executa `RecoverInterruptedRestoreAsync` e as assertions exigem ambos os participantes no estado do backup, journal removido e sidecars limpos. O filtro existente do job Linux/MariaDB já seleciona o cenário.

Validação local: build Release do projeto de integração passou; `BackupRestoreMariaDbTests` reportou 2 aprovados e 2 ignorados. Os dois E2Es MariaDB foram ignorados porque `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING` não está configurada. Contrato do portal passou 211/211; contrato do filtro CI MariaDB, 2/2; JSON e `homologacao.js` passaram validação sintática. Revisão Cavecrew não encontrou defeitos estáticos no isolamento, ordenação, ponto de crash, replay transacional ou limpeza. A execução MariaDB real continua pendente; não há resultado novo de CI nem acesso a produção. O cenário amplia cobertura de teste, sem mudança de runtime, API, dependência ou pontuação de homologação formal (2/7, 28,6%).

O log recebido nesta sessão é o recorte já auditado de 23/10: deadlock 1213 em `BaseItemImageInfos`, sem PID/assembly/versão. A nota da release 12.1.32 registra a mitigação local por lock, mas o log não comprova qual binário estava em execução. Não se inferiu instalação ou correção em produção.

## Revisão estática da fronteira ACL de restore — 2026-10-25 22:20 BRT

Revisão independente de `FileSystemRestoreTransaction`, `BackupRestoreDirectory` e `BackupRestoreAclValidator` não confirmou bypass explorável por identidade não confiável. Apply, rollback, replay e cleanup usam handles de diretório e nomes relativos; a ACL valida o namespace antes do uso e na reutilização do handle. As primitivas Linux ainda verificam tipo/nome antes de `renameat`/`unlinkat`, deixando uma janela check-then-act se alguém puder escrever no diretório aberto. Sob a política aprovada (serviço + root/admins), a revisão não encontrou identidade não confiável capaz de fazer essa escrita. Isso é conclusão estática; não é prova por exploração.

Reexecução Windows/.NET 10 Release: `FullyQualifiedName~FullSystemBackup` passou 115, ignorou 11 por plataforma/ambiente e falhou 0/126. Os casos Linux ACL, symlink e operações `openat` foram ignorados neste host. Consulta read-only à API pública GitHub em 25/10 encontrou os workflows mais recentes de CI e Release em 21/10/2026, ambos falhos, sem run Linux observável posterior. Não há evidência de execução hospedada, runtime instalado ou produção.

Conclusão: descrições pathname-only não representam o lifecycle atual e devem permanecer apenas como histórico datado. Restore P0 segue aberto até execução Linux nativa observável e teste determinístico da concorrência dentro do modelo de confiança. A suíte local revalidada e a revisão estática não alteram homologação formal (2/7; 28,6%) e não autorizam release.

## Falha explícita ao persistir letras — 2026-10-25 22:32 BRT

Quando nenhum destino seguro existe ou o formato é recusado, `TrySaveLyric` agora devolve falha e libera o stream. Os fluxos de upload e download remoto convertem falha de persistência em `IOException`; assim a API não enfileira refresh como sucesso e a tarefa agendada recebe a falha para contabilização. Controllers documentam a resposta HTTP 500. `LyricManagerPathSafetyTests` passou 25/25 e `LyricsControllerTests` 12/12 em Windows/.NET 10 Release. O runtime instalado, providers externos, produção e caminho Linux não foram validados. Homologação formal permanece 2/7, sem crédito.

\n
## Revalidação do gate Linux de restore — 2026-10-25 23:44 BRT

`dotnet test tests/Jellyfin.Server.Implementations.Tests/Jellyfin.Server.Implementations.Tests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~FullSystemBackup' --logger 'console;verbosity=minimal' -m:1` recompilou e passou: **115 aprovados, 11 ignorados, 0 falhas (126)** em Windows/.NET 10. Os 11 skips são casos de plataforma/ambiente; operações `openat`, ACL POSIX e links Linux não foram executados.

O host ainda não tem distribuição WSL; `gh run list` não pôde consultar Actions porque GitHub CLI pede `gh auth login`. Nenhuma alteração de código ou serviço foi feita. Revisão Cavecrew independente apontou limite já aberto: `fstatfs` identifica tipo de filesystem, não prova exclusividade do backing; ext4/XFS em armazenamento de bloco compartilhado pode passar na allowlist. As corridas `statx`/`unlinkat` e `statx`/`renameat*` só têm escritor concorrente se alguém puder alterar o namespace aberto; não foram reproduzidas. Não inventar detecção genérica de backing local sem evidência Linux, pois isso pode dar falsa garantia.

O anexo MariaDB recebido tem SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, idêntico ao incidente já documentado; não acrescenta PID/assembly nem confirma qual runtime emitiu o deadlock.

Restore P0/P1 permanece aberto até execução Linux observável, teste determinístico no runtime Linux e decisão/evidência de proveniência do armazenamento. Homologação formal permanece 2/7 (28,6%), sem crédito; sem release, instalação ou publicação remota. Módulos, APIs, dependências e fluxo de produção não mudaram.

## Rechecagem do anexo e do CI remoto — 2026-10-25 23:49 BRT

O anexo recebido tem SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, igual ao registro previamente auditado: deadlock MariaDB 1213 em `DELETE BaseItemImageInfos`. A resposta HTTP 200 inicial não confirma commit; o recorte não inclui PID, assembly, versão do servidor nem resultado final.

Busca de callers: `Program.StartServer` é o único caller de produção de `RecoverInterruptedRestoreAsync`. A recuperação lê journal, verifica o archive por caminho e, no core, valida novamente o stream aberto que será consumido pelo `ZipArchive`. Destinos e sidecars seguem pela transação relativa a handles; sem mudança estrutural nesta revisão.

O GitHub Actions API read-only mostra CI `39608614450`, SHA `d51f4f38d2b4148ab49394c001567ad273bd57aa`: 13 jobs encerrados em 3–4 segundos, todos falhos, `runner_id=0` e `steps=[]`. Release `39608677913` (v12.1.35) falhou no gate de validação da tag sem runner; empacotamento Linux e publicação foram ignorados. A consulta dos logs de job retornou `404 BlobNotFound`; a causa interna do provisionamento não está disponível. Isso demonstra que esses workflows não chegaram a executar comandos de teste, não que os testes de código falharam.

Regressão local `FullyQualifiedName~FullSystemBackup`, Windows/.NET 10 Release: 115 aprovados, 11 ignorados e 0 falhas/126. Os ignorados incluem cenários Linux. WSL não tem distribuição instalada. Não houve mudança de runtime; restore Linux/ACL, replay em MariaDB descartável, instalação e produção continuam sem validação. Homologação formal permanece 2/7 (28,6%), sem crédito ou release.
## Revalidação Release do probe fail-closed — 2026-10-26 00:08 BRT

`ProgramStartupRecoveryTests` foi recompilado e executado em Windows/.NET 10 Release: 6 aprovados, 0 ignorados/falhas. O subprocesso executa `Program.RunAfterStartupRecoveryAsync`, o mesmo gate chamado por `Program.StartServer`. Recovery simulado falho encerra com código 1 sem criar o marcador de inicialização; sucesso encerra com código 0 e cria o marcador.

Limite: o probe não inicia `StartApp`, não exercita `BackupService.RecoverInterruptedRestoreAsync` real e não espiona diretamente `InitializeServices`; só valida a ordem/resultado do gate compartilhado. Linux nativo, CI hospedada, runtime instalado e produção continuam pendentes. Homologação formal 2/7 (28,6%), sem crédito ou release.

## Cobertura durável do IntroSkipper em MariaDB — 2026-10-26 02:07 BRT

`IntroSkipperBackupParticipantTests` valida restore idempotente das oito tabelas duráveis (`Segments`, `SeasonStates`, `SeasonAnalysisOverrides`, `AnalyzedItems`, `ImportHistory`, `DisabledItems`, `ProjectionQueue`, `ProjectionExternalOperations`), confirma que `DetectionCache` é descartado/recalculável e verifica rollback quando uma inserção tardia falha. A verificação de schema inclui `ImportHistory`. O teste de composição resolve o `BackupService` e inspeciona o participante injetado pelo registro de plugin.

`IntroSkipper.Integration.Tests` passou **58/58** em Windows/.NET 10 Release contra MariaDB 11.4.13 descartável, isolada em `127.0.0.1:33099`; o formatter passou nos três arquivos alterados. Processo de teste encerrado e porta fechada. Nenhuma lógica de runtime mudou. CI Linux, runtime instalado, produção e cobertura de índices/constraints da auditoria de schema continuam pendentes. Homologação formal permanece 2/7 (28,6%), sem crédito ou release.

## Identidade física e mount do journal de restore — 2026-10-26 03:57 BRT

`BackupRestoreDirectory` consulta identidade de diretório por handle: `FILE_ID_INFO` (volume + ID de 128 bits) no Windows; device/inode com `statx(AT_EMPTY_PATH)` no Linux. O preflight de `BackupService` roda antes de criar/adquirir o lock; mantém a comparação lexical para raízes inexistentes, compara ancestrais físicos e rejeita `restore-recovery` em mount diferente de ProgramData. Isso detecta um bind mount aplicado diretamente ao diretório de recuperação. O fluxo valida novamente depois da validação do archive, antes de criar o journal.

`FullyQualifiedName~FullSystemBackup` passou **132, ignorou 12, falhou 0/144** em Windows/.NET 10 Release. Inclui identidade same/ancestor, caminho padrão com raiz abaixo de ProgramData e preservação de `FileNotFoundException` com ProgramData ausente. `IsOnSameMountAs_DetectsBindMountedDirectory` compilou, mas foi ignorado porque esta execução foi Windows. `dotnet format style --verify-no-changes` passou nos quatro arquivos C# do escopo.

Limite P1: o mesmo método ainda pode não detectar se o próprio ProgramData for bind mount de uma subpasta de uma raiz de restore; Linux/bind mount e corrida de namespace não foram executados. CI deste checkout, runtime instalado, MariaDB crash/replay e produção pendentes. Homologação formal permanece 2/7 (28,6%), sem crédito; nenhuma release.

### Rechecagem do alias físico de ProgramData — 2026-10-26 04:14 BRT

`BackupRestoreDirectory.IsSameOrAncestorOf` agora mantém a comparação por identidade de handle e, no Linux, resolve o caminho atual do descritor via `/proc/self/fd`, associa seu mount ID a `/proc/self/mountinfo` e compara dispositivo e coordenadas físicas (`root` + caminho relativo ao mountpoint). O caminho é lido novamente para falhar fechado se o diretório se mover durante a resolução. O parser de escapes octais passou; a regressão monta `restore/program-data-source` em `program-data` e exige reconhecer a ancestralidade.

`FullyQualifiedName~FullSystemBackup`: Windows/.NET 10 Release, 133 aprovados, 13 ignorados, 0 falhas/146. O caso do bind mount foi ignorado. `dotnet format style --verify-no-changes` passou para os dois arquivos C# alterados. Revisão adversarial estática não encontrou finding restante. WSL não está instalado; Linux nativo/CI observável, MariaDB crash/replay, runtime instalado e produção não foram validados. Homologação formal permanece 2/7 (28,6%), sem crédito ou release.
## Gate de recuperação antes da inicialização — 2026-10-26

O helper subprocessual `Program.RunAfterStartupRecoveryAsync`, usado por `Program.StartServer`, agora também tem caso para o retorno `false` produzido após restore solicitado. O probe verifica três contratos: exceção de recovery encerra filho com exit 1 sem inicializar; retorno `false` encerra caminho com exit 0 sem inicializar; recovery bem-sucedido executa callback e grava marcador. `ProgramStartupRecoveryTests` Release Windows/.NET 10 passou 7/7; `dotnet format style --verify-no-changes` direcionado passou.

Esse teste cobre o helper e um processo filho, não chama `StartApp`, `BackupService.RecoverInterruptedRestoreAsync` ou `InitializeServices` real. Linux/CI hospedada, runtime instalado e produção permanecem pendentes; homologação formal 2/7 (28,6%), sem crédito.

Reexecução em 26/10/2026 07:10 BRT: `dotnet test tests/Jellyfin.Server.Tests/Jellyfin.Server.Tests.csproj --configuration Release --filter FullyQualifiedName~ProgramStartupRecoveryTests` passou 7/7, sem ignorados ou falhas. A compilação em Release também completou; emitiu avisos XML preexistentes fora do teste. A evidência revalida somente o probe/gate subprocessual; não amplia o escopo de runtime nem de produção.

### Limite de tempo do subprocesso de teste — 2026-10-26 07:18 BRT

`ProgramStartupRecoveryTests` agora limita cada probe a 30 segundos e encerra a árvore de processos quando o teste expira ou é cancelado. O cenário `hang` testa o caminho de timeout em 250 ms e confirma que o processo filho terminou sem gravar o marcador. Reexecução Release Windows/.NET 10: 8/8, sem skips/falhas; `dotnet format style --verify-no-changes --no-restore --include ...` passou para os dois arquivos C# alterados. Isso protege a suíte de teste, não muda o runtime de produção nem valida inicialização completa.

## Reconciliação do gate MariaDB de restore — 2026-10-26 06:11 BRT

A execução autenticada em MariaDB 11.4.13 descartável, documentada em `server-mariadb-authenticated-integration-2026-10-26-0604`, executou o filtro selecionado pelo workflow e passou 16/16. O filtro inclui `RestoreBackupAsync_ReplaysAfterProcessTerminationBetweenDatabaseAndFileCommit` e `RestoreBackupAsync_RestoresDatabaseRowsOnDisposableMariaDb`; os cenários validam replay após encerramento do processo entre commits e restauração de registros em banco descartável. Grants permaneceram ativos; o teste usou root e não valida privilégios mínimos da aplicação.

O gate local de restore MariaDB deixa de estar pendente. Linux nativo e CI hospedada para este checkout, runtime instalado, produção e aceite formal continuam sem evidência. A regressão focal de persistência de imagens passou 49/49 em Windows/.NET 10 Release; ela não comprova que o binário do log de 23/10 continha o lock. Nenhuma alteração de runtime foi feita nesta sessão. Mapa técnico conferido e sem mudança estrutural. Homologação formal permanece 2/7 (28,6%), sem crédito ou release.

## Rechecagem de CI Linux para o checkout atual — 2026-10-26 06:16 BRT

Consulta GitHub read-only em 26/10/2026 confirma que o último workflow CI de `main` é o run `39608614450`, SHA `d51f4f38d2b4148ab49394c001567ad273bd57aa`, de 21/10. Os 13 jobs falharam em 3–4 segundos; cada job tem `runner_id=0` e lista `steps` vazia. Não houve execução de build ou testes, e a causa interna de provisionamento não foi recuperada.

O SHA remoto coincide com `origin/main`, mas o checkout local está no `64a33dcf5595fb6f429c65206be20dbdd4ae0bd0`, oito commits atrás, com 205 paths modificados. Assim, o run não valida a fonte atual. WSL não está instalado e Docker/Podman não estão disponíveis. Restore Linux/ACL, CI hospedada do checkout atual, runtime instalado e produção permanecem sem validação; homologação formal 2/7 (28,6%), sem crédito. Mapa técnico conferido; nenhuma estrutura foi alterada nesta rechecagem.

## Concorrência de capas por faixas MariaDB — 2026-10-26 06:27 BRT

- [x] Anexo SHA-256 idêntico ao incidente anterior; repete `DELETE BaseItemImageInfos` e deadlock 1213, sem PID/assembly/versão nem resultado final dos retries.
- [x] Auditoria dos escritores MariaDB (`SaveImagesAsync`, `SaveItems`, `DeleteItem`, rotina `FixDates`) não encontrou bypass dos locks cooperativos intra-processo/advisory.
- [x] Novo teste de integração cobre `SaveItems` em lote concorrendo com 24 `SaveImagesAsync` em várias faixas; compilou em Windows/.NET 10 Release e foi incluído no filtro CI MariaDB/Linux. Formatter passou.
- [x] Contrato portal: 255/255; JSON atualizado. Formal permanece 2/7 (28,6%), sem crédito.
- [ ] Teste MariaDB foi ignorado localmente porque endpoint descartável não está configurado. Executar em MariaDB descartável; testar deadlock InnoDB real e correlação PID/assembly ficam pendentes. Sem validação de runtime/produção ou publicação remota.

## Revalidação do anexo repetido de deadlock — 2026-10-26 12:08 BRT

O arquivo recebido tem SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`, idêntico ao anexo auditado em 25/10. O trecho mostra concorrência no `DELETE BaseItemImageInfos` e deadlock MariaDB 1213; a resposta HTTP 200 não atesta commit. Não inclui PID, assembly, versão instalada nem desfecho dos retries. A fonte local já serializa `SaveImagesAsync` com a faixa compartilhada do item e `ItemPersistenceDatabaseLock` consultivo MariaDB.

Nesta revalidação, `ItemPersistenceServiceTests|ItemPersistenceDatabaseLockTests` Release/.NET 10 passou 49/49. Três cenários de integração MariaDB (refresh simultâneo, metadata+imagem e lote multi-faixa) foram selecionados, mas todos ignorados: `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING` não está configurada. Assim, o erro permanece sem confirmação de correção em runtime: processo/binário causador não identificado e locks consultivos só coordenam participantes que usam a mesma convenção/banco. Nenhum banco operacional ou runtime instalado foi acessado; nenhuma release/instalação foi feita. Formal 2/7 (28,6%), sem crédito.

## Bloqueio de mountpoints internos durante restore — 2026-10-26 12:17 BRT

Revisão adversarial identificou que o restore verificava contenção lexical e recusava reparse/symlinks, mas não comparava o mount dos diretórios abertos abaixo da raiz configurada. `FileSystemRestoreTransaction` agora ancora cada destino à autoridade de mount da raiz configurada e falha antes de criar sidecars ou gravar quando um descendente está em outro mount. Foi adicionado `RestoreBackupAsync_RejectsNestedMountBeforeWritingIntoIt`, que prepara tmpfs sob a raiz Config e exige recusa sem arquivo/journal ativo.

`FullSystemBackup|ItemPersistenceServiceTests` Release/Windows passou 167, ignorou 15, falhou 0/182. O teste de mount novo foi ignorado por ser Linux-only; WSL, Linux nativo e permissões de mount não estão disponíveis aqui. Format verify e contrato do portal (274/274) passaram. A correção está somente no checkout local. A falha de imagens já tem lock por stripe compartilhado entre escritores e cobertura MariaDB, mas os testes de carga não executaram sem `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING`; log não identifica o binário/PID nem o desfecho dos retries. Formal 2/7 (28,6%), sem crédito; sem produção, release ou publicação remota.

## Reconciliação documental do estado do helper — 2026-10-26 12:24 BRT

As observações datadas de 23/10 que dizem “helper sem caller” registram o estado naquela revisão e não descrevem a fonte atual. `BackupService.FileSystemRestoreTransaction` e `BackupRestoreRecoveryJournal` usam operações relativas a handles para restore, replay, journal, rollback e cleanup. A comparação de mountpoints descendentes com a raiz configurada também está implementada; a regressão tmpfs ainda não foi executada em Linux. Permanecem abertos Linux nativo/CI observável, casefold, ACL por identidade/raiz, races e crash integrados, instalação/runtime e produção. Nenhuma evidência adicional foi gerada nesta reconciliação; homologação formal 2/7 (28,6%), sem crédito.

## Deadlock de imagens: lacuna na fonte da tag 12.1.35 — 2026-10-26 12:34 BRT

A DLL da instalação local reporta 12.1.35 e data de arquivo 21/10/2026. Inspecionando a fonte Git da tag `v12.1.35`, a rotina `SaveImagesAsync` faz replace em transação, mas não inclui o lock por stripe em memória nem `ItemPersistenceDatabaseLock` que existem no checkout de desenvolvimento. A correspondência do binário instalado com a tag não foi provada por hash de artefato oficial; o log não fornece PID, assembly nem versão. Portanto, o descompasso é uma causa plausível para as gravações simultâneas, não uma atribuição conclusiva do incidente.

A integração MariaDB foi reforçada para produzir substituições concorrentes de fato: 32 refreshes simultâneos, cada um com conjunto distinto, readiness barrier e verificação de conjunto final íntegro. Windows/.NET 10 Release compilou o projeto e selecionou o caso, mas ele foi ignorado por falta de `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING`; regressão unitária focal 49/49 e format verify passaram. MariaDB real, runtime/produção e CI hospedada continuam sem validação. Formal 2/7 (28,6%), sem crédito/release.


## Regressão Windows do restore — 2026-10-26 16:32 BRT

dotnet test FullSystemBackup em Windows/.NET 10 Release recompilou e passou 133, ignorou 15, falhou 0/148. Os skips abrangem syscalls/Linux (links, ACL, mount) e cenários macOS. WSL não instalado; Docker/Podman indisponíveis. Resultado confirma regressão Windows do checkout, sem validar Linux, CI hospedada atual, ACL real ou produção. Homologação formal permanece 2/7 (28,6%), sem crédito. Ver [[Sessões/2026-10-26-MulletaFlix-restore-windows-regression-1632]].

Contrato do portal atualizado com esta evidência passou 294/294; JSON parseável. git diff --check inicialmente apontou linha em branco extra no EOF de TODO-AUDITORIA.md; removida antes do fechamento.

## Auditoria do exercício de recuperação no painel — 2026-10-26 18:25 BRT

`BackupCoverageSummary` mostra separadamente ZIP local, catálogo Nebula no Supabase, backup de usuários e os dados relatados pela última restauração operacional. Também declara que esses fatos não comprovam um exercício isolado ponta a ponta. O teste focado `BackupCoverageSummary.test.tsx` passou 6/6 com Vitest. A interface está correta em não transformar restauração operacional em homologação completa, porém nenhuma API/execução registrada comprova um exercício isolado cobrindo MongoDB e arquivos; T4.4.15 permanece pendente. Nenhum runtime foi alterado ou instalado. Formal 2/7, sem crédito. Ver [[Sessões/2026-10-26-MulletaFlix-backup-exercise-ui-audit-1825]].

## Auditoria da fronteira ACL do restore — 2026-10-26 18:28 BRT

Revisão das fontes atuais confirmou que `OpenLinuxDirectoryPath` valida cada diretório ancestral aberto antes de avançar; `FileSystemRestoreTransaction` abre destinos relativamente a handles, confere a raiz/mount e valida a ACL antes de gravar. A política Linux aceita escrita para UID do serviço/root e rejeita escrita de grupo/outros; a política Windows deriva identidades confiáveis do token do processo e valida owner/DACL. Não foi encontrada uma correção de código justificada nesta revisão estática.

`BackupRestoreDirectoryTests` passou 39, ignorou 10 e falhou 0/49 em Windows/.NET 10 Release. Os ignorados são específicos de Linux/macOS e deixam ABI, ACL POSIX, symlink e bind mount sem validação nesta máquina. O contrato portal passou 297/297. Duas aplicações sob o mesmo UID confiável continuam indistinguíveis pela ACL POSIX; qualquer ameaça que inclua processos co-residentes desse UID exige isolamento operacional da identidade do serviço, não uma ACL que aceite o mesmo UID. Linux nativo/CI observável, runtime instalado e produção permanecem pendentes. Formal 2/7 (28,6%), sem crédito ou release. Mapa conferido; estrutura inalterada. Ver [[Sessões/2026-10-26-MulletaFlix-restore-acl-static-audit-1828]].
# Reauditoria da ACL de pais do restore — 2026-10-26 21:29 BRT

Uma revisão independente apontou `FILE_APPEND_DATA`/`FILE_ADD_SUBDIRECTORY` concedido a `Everyone` nos pais Windows como possível divergência da política. A exceção já está explicitada na emenda da decisão `2026-10-24-restore-acl-trust-boundary`: o handle do pai é validado contra criação de arquivos, deleção/substituição, alteração de ACL/owner e principals não confiáveis; permitir criação isolada de subdiretórios não concede escrita no conteúdo existente. O alvo final usa a regra estrita. A exceção não foi revertida nesta sessão; o risco de negação de serviço por criação de nomes permanece distinto do risco de adulterar arquivos.

`BackupRestoreDirectoryTests` Windows/.NET 10 Release: 39 aprovados, 10 ignorados Linux/macOS, zero falhas. Isso valida os contratos Windows do checkout, não ACL/namespace concorrente em produção nem Linux. Homologação formal 2/7 (28,6%), sem crédito. Ver sessão vault `2026-10-26-MulletaFlix-deadlock-and-restore-acl-reaudit-2129`.
# Contrato do gate Linux privilegiado — 2026-10-26 21:40 BRT

O workflow já executa os dez cenários Linux de restore com `sudo` e confere no
TRX que todos passaram. Adicionado teste contratual para proteger a lista de
seleção, privilégios e rejeição de resultados ausentes/Skipped. Teste focal
2/2; seis contratos CI 15/15. A máquina local não tem WSL nem Docker e a CI
hospedada não foi executada; portanto o gate Linux real continua aberto. Formal
2/7 (28,6%), sem crédito.
## Gate Linux ampliado e ACL de owner separada — 2026-10-26 22:25 BRT

Revisão independente encontrou três testes de symlink Linux omitidos do filtro privilegiado da CI: `OpenOrCreateFileAndDeleteIfExists_RejectSymbolicLinkOnLinux`, `Rename_RejectsSymbolicLinkSourceOnLinux` e `RecoveryJournal_OperationLeaseRejectsSymbolicLinkOnLinux`. Os três foram adicionados ao filtro e ao conjunto TRX obrigatório. A etapa agora exige 13 resultados `Passed`; caso ausente, `Skipped` ou qualquer resultado diferente falha.

A exceção Windows para TrustedInstaller foi separada da lista de principals autorizados a receber grants de escrita: TrustedInstaller pode ser owner da âncora do volume, mas não é inserido no conjunto usado para validar ACEs de escrita. Testes cobrem a distinção do owner e uma ACL real que concede escrita a TrustedInstaller, validando que essa ACE é rejeitada.

`FullSystemBackup` Windows/.NET 10 Release passou 135, ignorou 15 por plataforma e falhou 0/150; contrato do portal passou 316/316 após registrar esta atualização. WSL não está instalado e Docker/Podman não estão disponíveis. Linux nativo/CI hospedada, ACL efetiva em instalação/runtime e produção continuam pendentes; restore P0 não fecha. Homologação formal segue 2/7 (28,6%), sem crédito ou release.

## Rejeitar escrita efetiva em `other::` na ACL POSIX — 2026-10-26 22:38 BRT

Revisão adversarial descobriu que a validação aplicava `mask::` a todas as entradas, incluindo `other::`. POSIX ACL não aplica a máscara a `other::`; uma ACL padrão com `other::write` podia passar se a máscara removesse essa permissão. Agora apenas `user::` e `other::` usam permissões diretas; grupos e usuários nomeados continuam sujeitos à máscara. Adicionados teste unitário multiplataforma do parser e teste Linux com ACL padrão real; o gate privilegiado exige ambos os casos Linux e valida exatamente 14 resultados Passed.

`BackupRestoreDirectoryTests` Windows/.NET 10 Release: 42 aprovados, 11 ignorados por plataforma, 0 falhas/53. `FullSystemBackup`: 136 aprovados, 16 ignorados por plataforma, 0 falhas/152. Contrato CI 2/2; portal 316/316. Revisão final identificou também que o parser TRX antigo podia sobrescrever resultados de mesmo nome curto; agora valida cada resultado, rejeita duplicatas e itens extras e exige exatamente um Passed por teste esperado. O teste real de xattr ACL e os 14 testes privilegiados não executaram neste host Windows. WSL/Docker/Podman ausentes, CI hospedada, ACL de instalação/runtime e produção pendentes; restore P0 e homologação formal 2/7 (28,6%) permanecem abertos, sem crédito ou release.

## Supabase delta: remover ordenação remota redundante — 2026-10-26 23:28 BRT

`GetLastRemoteFileTimestampAsync` consultava `nebula_files` ordenando `updated_at DESC`, mas descartava esse valor; a decisão delta usa o checkpoint `sync_started_at_utc` do último backup concluído. A consulta agora lê apenas `id` com `limit=1`, preservando a distinção entre catálogo vazio/não vazio e o bloqueio seguro quando a consulta falha. O script de provisionamento não cria mais o índice usado somente pela ordenação removida; índice já existente no Supabase não é removido por esta mudança.

`NebulaSupabaseSyncTests`: 65/65 em Release; `SupabaseSqlScript_ContainsRlsPolicies_ForAllNebulaTablesWithServiceRole`: 1/1. Os testes usam HTTP simulado; não reproduzem o HTTP 500 no Supabase real. Homologação formal continua 2/7 (28,6%), sem crédito.

## Regressão ampla local do servidor — 2026-10-26 23:37 BRT

Após a otimização da consulta delta, a solução .NET em Release terminou com 5.410 aprovados, 98 NotExecuted e zero falhas em 5.508 resultados, coletados em 17 TRX. Os ignorados não são aprovados: incluem testes opt-in sem MariaDB/MongoDB, casos exclusivos de Linux/macOS e restrições locais como `/dev/shm`. Nebula Python 3.14 passou 724 e ignorou 2; testes web passaram 418 em 75 arquivos; `tsc --noEmit` terminou sem erros. A suíte web imprimiu avisos de ambiente jsdom/React, sem falhas de teste.

Esta é uma rodada Windows local e não reconcilia o denominador histórico do portal. Não foram executados serviços reais MongoDB/MariaDB, CI hospedada, Linux/macOS, instalação de produção ou build web de produção. Homologação formal permanece 2/7 (28,6%), sem crédito ou release.

## Revalidação Windows e revisão Cavecrew do restore — 2026-10-26 23:42 BRT

Recompilação da fonte atual e execução focal `dotnet test ...Jellyfin.Server.Implementations.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~FullSystemBackup`: 136 aprovados, 16 ignorados por plataforma, 0 falhas/152. Os ignorados foram enumerados no output; incluem ACL/syscalls/symlinks/mounts Linux e dois cenários macOS. Revisão independente de `FileSystemRestoreTransaction`, `BackupRestoreDirectory` e `BackupRestoreAclValidator` não confirmou vulnerabilidade ou quebra de invariante adicional: o fluxo implementa autoridade por handles, staging, rollback/recovery sidecars e validação ACL. Isso é revisão estática mais regressão Windows, não prova comportamento Linux/ACL efetiva no runtime instalado. CI hospedada segue sem logs, Linux local indisponível, restore P0 não recebe fechamento; formal 2/7 (28,6%), sem crédito/release.

## Rechecagem da CI remota e disponibilidade de Linux — 2026-10-26 23:39 BRT

GitHub API ainda mostra `main` em `d51f4f38d2b4148ab49394c001567ad273bd57aa`; checkout local está em `64a33dcf5595fb6f429c65206be20dbdd4ae0bd0`, oito commits atrás e sem commits locais à frente. CI run `39608614450` (`main`) tem 13 jobs com conclusão `failure`, porém os resumos não incluem steps/logs; download de logs do job de servidor `119251247983` retorna HTTP 404 `BlobNotFound`. Release run `39608677913` falhou em `Validate source tag before packaging`; os jobs Linux e publicação ficaram `skipped`, e o download do log do preflight `119251465001` também retorna `BlobNotFound`. Isso confirma ausência de evidência diagnóstica armazenada, não a causa das falhas.

No host Windows, `wsl --list --quiet` informa que WSL não está instalado e o comando `docker` não existe. O checkout contém alterações locais numerosas; não foi limpo, alterado ou enviado ao remoto. Sem runner Linux disponível e sem mecanismo de dispatch/retry de workflow no conector desta sessão, o gate Linux do restore não pode ser validado aqui. Homologação formal permanece 2/7 (28,6%), sem crédito ou release.
## Revalidação Windows e revisão do contrato Linux — 2026-10-27 04:27 BRT

`dotnet test tests/Jellyfin.Server.Implementations.Tests/Jellyfin.Server.Implementations.Tests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~FullSystemBackup'` passou com **137 aprovados, 16 ignorados por plataforma, 0 falhas/153** em Windows/.NET 10. Os skips incluem ACL/POSIX, symlinks e mounts Linux, e casos macOS; não são validação Linux.

Revisão adversarial alegou que TrustedInstaller poderia receber grant de escrita na âncora. Conferência da fonte e do teste `ValidateTrustedWriteBoundary_RejectsTrustedInstallerWriteGrantOnWindows` refutou a alegação: a exceção do SID só entra em `IsTrustedWindowsOwnerSid` para o owner da âncora; grants continuam comparados somente com a lista service/SYSTEM/Administrators. O contrato dedicado `ci-test-runner-serialization.test.mjs` passou 2/2 e verifica os 14 nomes únicos entre filtro Linux, expectativa TRX e métodos xUnit. O teste do portal mantém verificações representativas de três casos.

`node --test tools/release/tests/ci-test-runner-serialization.test.mjs` passou 2/2; `node --test --test-reporter=spec portal-site/tests/homologacao.test.mjs` passou 332/332. Nenhuma lógica de restore/runtime foi alterada. Linux nativo/CI hospedada, ACL efetiva do runtime, corridas adversariais e produção continuam pendentes. Restore P0 permanece aberto; homologação formal 2/7 (28,6%), auditoria agregada 5.465/5.493, sem crédito ou release.

## Recovery do journal antes de migrações pré-iniciais — 2026-10-27 05:15 BRT

A ordem de startup foi corrigida: recovery do journal e continuação de restore solicitado antecedem `ApplyStartupMigrationAsync`. A migração pré-inicial continua sendo executada uma única vez entre reinícios; `GetTranscodePath`, que cria diretório quando ausente, também só é chamado após recovery.

Evidência Windows/.NET 10 Release: `ProgramStartupRecoveryTests` passou 4/4; `RecoverInterruptedRestoreAsync_ProgramStartupGateReplaysJournalBeforeInitialization` passou 1/1 com `BackupService`, archive e journal reais, confirmando o payload restaurado no callback de migração e a inicialização posterior. O build emitiu avisos StyleCop/xUnit em outros testes, sem falhas. Não foram executados host integral, migrações concretas, Linux/ACL, CI hospedada, runtime instalado ou produção. Restore P0 e homologação formal 2/7 (28,6%) seguem abertos, sem crédito ou release.

### Reenvio idêntico do log — 2026-10-27 05:31 BRT

O anexo repetido mantém o SHA-256 `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`. Sem PID/AssemblyVersion, desfecho de retry ou evidência InnoDB, não atribui a falha ao binário atual. A fonte de trabalho serializa as gravações de imagens por stripe e usa advisory lock, mas não comprova correção em produção.

## Classificação da corrida de rename no restore Linux — 2026-10-27 05:35 BRT

A política de ACL escolhida pelo usuário confia no UID efetivo do serviço e em root. A validação Linux na fonte rejeita escrita para group/other e named users não autorizados; portanto rename executado por essas identidades não constitui bypass de traversal por ator não confiável. Ainda pode desviar o destino nomeado e causar falha/integridade, classificado como risco operacional condicional. Decisão registrada no vault em `01 Projetos/MulletaFlix/Decisões/2026-10-27-restore-linux-trusted-rename-threat-model.md`.

O gate permanece aberto para ACL/syscalls Linux observáveis, teste determinístico de rename/namespace, CI do checkout atual, runtime instalado e produção. `FullSystemBackup` local Windows/.NET 10 Release: 138 aprovados, 16 ignorados por plataforma, 0 falhas/154; não valida comportamento Linux.

## Limites de leitura e expansão ZIP do restore — 2026-10-28 08:51 BRT

`BackupService` agora limita tamanho do arquivo (257 GiB), diretório central (32 MiB), entradas (100.000), expansão por entrada (64 GiB), expansão total (256 GiB), manifesto (16 MiB) e razão de compressão (1.000:1). O preflight conta entradas antes de criar `ZipArchive`; CRC lê a expansão em blocos limitados e conta bytes efetivamente entregues pelo descompressor. A listagem de backups passa pelo preflight e limita também o manifesto. O participante IntroSkipper limita o snapshot a 64 MiB declarados e a 32 Mi caracteres UTF-16 acumulados durante a leitura. `Directory.Packages.props` ganhou a versão central EFCore.Sqlite 9.0.0 necessária para restaurar o projeto de teste IntroSkipper.

Validação Windows/.NET 10 Release: `FullSystemBackup` 147 aprovados, 16 ignorados por plataforma e 0 falhas (163); `Crc32Tests` 8/8; teste focal de rejeição do snapshot IntroSkipper 1/1; `git diff --check` passou. O contrato do portal passou 286/286. Revisão independente não encontrou P1. O limite do ratio usa leitura antecipada do descompressor em blocos de até 4 KiB; os limites absolutos de expansão continuam rígidos. O preflight não valida ZIPs com prefixo SFX nem assinatura digital opcional do diretório central. Não foram medidos backups reais de maior porte. Linux/CI hospedada, runtime instalado, produção e aceite formal não foram validados; os 16 skips não contam como aprovados. Homologação formal permanece 2/7 (28,6%), sem crédito ou release.

## Retentativas do Supabase e refresh de diretório Nebula — 2026-10-28 09:03 BRT

Auditoria estática identificou que o POST idempotente de `mulletaflix_users` falhava no primeiro HTTP 5xx/timeout, sem usar a política de até três tentativas que já existia para lotes Supabase. O método agora usa o helper compartilhado de retry, preserva `on_conflict=normalized_username`/merge-duplicates e mantém a mensagem específica para `PGRST205`/migração ausente. O teste `PerformUsersBackupAsync_RetriesTransientMulletaFlixUserBatch` valida HTTP 500 seguido de sucesso e reconciliação GET subsequente.

`NebulaDirectoryRefreshQueue` removia o diretório antes de executar o refresh e descartava a falha; agora agenda o mesmo diretório novamente após 30 s, sem impedir o processamento dos demais. `RefreshFailure_RetriesDirectoryUntilRefreshSucceeds` usa delay curto determinístico e confirma falha transitória seguida de refresh bem-sucedido. Focal `NebulaDirectoryRefreshQueueTests` + regressão do backup de usuários: 7/7 aprovados, 0 falhas; build Debug/net10.0 incluiu warnings XML/StyleCop preexistentes. `git diff --check` passou.

Os testes HTTP são simulados; Supabase real, timeout de rede real, Mongo, runtime instalado e produção não foram verificados. Retry da fila é em memória e não sobrevive ao encerramento do processo; a recuperação periódica atual ainda consulta apenas a janela de 30 minutos. CI hospedada não executou porque branches recebem CI por pull request e o GitHub recusou a criação do PR via integração (HTTP 403 `Resource not accessible by integration`). Branch `codex/backup-restore-handle-relative-io`, sem PR/merge/release; homologação formal segue 2/7 (28,6%), sem crédito.

- Revalidação ampliada: `NebulaSupabaseSyncTests` passou 54/54 no Windows/.NET 10 Debug; o resultado 7/7 acima é a soma dos testes focais de fila e retry do backup. Não altera o escopo dos limites/pedências acima.

## Recuperação durável do refresh VFS Nebula — 2026-10-28 09:16 BRT

Correção da nota de 09:03: o retry apenas em memória não cobria restart, e o callback de `RefreshRcloneDirectoryAsync` capturava exceções; assim a fila podia interpretar refresh falho como sucesso. Agora os caminhos pendentes são registrados por `_id` canônico em `nebula_directory_refreshes` antes da fila em memória, rehidratados no scan de startup e removidos somente após `rclone /vfs/refresh` (ou refresh dos ancestrais) confirmar sucesso. Erros de rede, resposta negativa e rclone ainda não pronto propagam à fila, que aplica retry de 30 s. Caminhos persistidos precisam pertencer a uma raiz de mídia visível e não podem conter segmentos `.`/`..`.

Teste Release Windows/.NET 10 de todos os testes com `FullyQualifiedName~Nebula`: 716 aprovados, 26 ignorados, 0 falhas (742). `NebulaDirectoryRefreshMongoTests` foi incluído, mas ignorado porque Mongo descartável em `127.0.0.1:27099` não estava disponível; portanto a persistência Mongo real ainda não foi executada. CI hospedada, backup/restore Supabase com a nova coleção, runtime instalado e produção também pendentes. Formal continua 2/7 (28,6%), sem crédito ou release.

## Backup/restauração da fila de refresh VFS — 2026-10-28

A fila persistente `nebula_directory_refreshes` agora participa do backup/restore nativo C# do Supabase. O backup grava o conjunto atual de `directory_path` por upsert e remove do espelho caminhos que já não estão pendentes; o restore regrava os paths no Mongo por `PersistDirectoryRefreshAsync`, que aplica validação de raiz visível e traversal. Isso evita perder markers quando uma restauração do Mongo ocorre antes do refresh do rclone.

O script de configuração cria `nebula_directory_refreshes` e RLS/policy apenas para `service_role`. `nebula_admission` e `operation_replays` seguem fora do backup porque são leases e deduplicação operacional; restaurar esses registros poderia reinstalar locks/replays vencidos. O schema Supabase real ainda não foi aplicado nem validado nesta sessão. Verificação focal Release: 57 aprovados, 4 ignorados, 0 falhas; suíte ampla `FullyQualifiedName~Nebula`: 718 aprovados, 26 ignorados, 0 falhas/744. Mongo opt-in, Supabase real, CI hospedada, runtime e produção permanecem pendentes.

## Validação Mongo opt-in e instalação do schema — 2026-10-28

Após habilitar MongoDB 8.3.7 descartável apenas em `127.0.0.1:27099` (sem usar o listener `27017`), a suíte `FullyQualifiedName~Nebula` passou 744/744 em Release, sem skips. Isso executou os testes reais de persistência/recriação de contexto e backup/restore Mongo com HTTP Supabase mockado. Dois fixtures antes ignorados tinham cursor GUID esperado com aspas e quantidade insuficiente de respostas; ambos foram corrigidos. Migration `20261028125319_nebula_directory_refreshes` foi aplicada ao projeto Supabase `Mulletaflix-Nebula`: tabela existe vazia, RLS está habilitado, policy permite somente `service_role` e ACL foi verificada sem privilégios para `anon`/`authenticated`. O teste não fez backup ou restore com chave real, nem validou runtime/CI/produção. A migration versionada está em `supabase/migrations/20261028125319_nebula_directory_refreshes.sql`.

## Remediação dos advisors Supabase — 2026-10-28

Os advisors reportaram `function_search_path_mutable` em `public.set_nebula_updated_at`, índice duplicado em `mulletaflix_users.normalized_username` e `idx_nebula_files_parent` sem scans. A função trigger só atribui `NEW.updated_at = NOW()`, então o `search_path` vazio não altera resolução de relações. A migration remota `20261028130514_supabase_advisor_cleanup` foi aplicada; catálogos confirmaram `proconfig=search_path=""`, remoção dos dois índices, permanência do índice único canônico de `normalized_username`, e advisors security/performance vazios. C# e setup Python foram alinhados para não recriar o índice `parent`; C# garante unicidade por um único índice nomeado. Testes focais: C# schema 1/1 e Python schema CLI 1/1. CI hospedada, runtime e produção não foram validados.

## Reauditoria da corrida Linux no rename de rollback — 2026-10-28

Uma revisão independente apontou a sequência potencial em `BackupRestoreDirectory.Rename`: `GetLinuxEntryType` chama `statx(..., AT_SYMLINK_NOFOLLOW)` e, em seguida, `RenameAt`/`RenameAt2` opera pelo nome relativo. Um escritor concorrente com a mesma identidade do serviço ou root poderia trocar a entrada entre essas chamadas; `renameat` moveria a symlink sem segui-la. No restore, `Rollback` pode então remover o destino instalado e falhar ao renomear o sidecar `.bak`, pois a própria API de `Rename` rejeita symlink.

Confirmação nesta sessão: inspeção estática dos pontos acima e do teste `Rename_RejectsSymbolicLinkSourceOnLinux`; o teste cobre apenas symlink estática, não injeta a corrida entre inspeção e rename. Nenhuma exploração nem perda de arquivo foi reproduzida. WSL não tem distribuição Linux instalada e Docker não está disponível, então não houve execução Linux. Tratar como risco residual condicionado a escrita concorrente por identidade já confiável, não como bypass de usuário não confiável confirmado. H03 permanece em andamento; próxima evidência necessária é teste Linux determinístico e decisão sobre endurecimento/garantia de exclusão concorrente. Sem mudança de runtime nesta reauditoria.

## Falha do rollback deve parar o servidor — 2026-10-28

Quando uma restauração de arquivos falha antes do commit relacional e o rollback também não consegue devolver um sidecar, `BackupService` agora solicita reinício e mantém o journal. Isso evita atender requisições com um conjunto parcialmente restaurado; no próximo startup, o replay reaplica o backup até convergir. O teste Windows `RestoreBackupAsync_RequestsRestartWhenRollbackCannotRestoreBackupSidecar` confirmou o pedido de restart, preservação do journal/sidecar e replay concluído. `FullSystemBackup` faz parte da suíte ampla: 1.649 aprovados, 82 ignorados, 0 falhas. Linux, CI hospedada e runtime instalado continuam pendentes.
