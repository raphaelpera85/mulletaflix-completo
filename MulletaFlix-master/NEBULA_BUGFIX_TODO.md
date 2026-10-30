# Nebula Bugfix TODO

Status:

- [x] Audit Nebula C# upload, download, streaming, Mongo, FTP mount, Supabase SQL, and bot API surfaces.
- [x] Remove hardcoded source secrets and stop returning bot tokens from read APIs.
- [x] Replace hardcoded FTP/rclone credentials with explicit configured or per-installation generated credentials.
- [x] Prevent filename-only collisions across different folders and staging paths.
- [x] Make upload claiming respect worker ownership and skip unclaimed work.
- [x] Validate completed-upload reuse against file size and part size before deleting source files.
- [x] Validate HTTP range downloads before joining multipart files and clean partial artifacts on failure.
- [x] Validate streamed Telegram parts against expected size and fail on inconsistent part offsets.
- [x] Avoid killing unrelated rclone processes and serialize drive mount attempts.
- [x] Generate Supabase SQL with RLS enabled instead of disabled.
- [x] Add focused regression tests for the fixed Nebula failure modes.

## Historical validation — 2026-09-10

The original focused suite result was 144/144. The same historical note recorded three environment-dependent integration failures when the disposable MariaDB test service was unavailable. These figures describe the 2026-09-10 validation and are not current run results.

## Current local validation — 2026-10-29

- C# tests with `FullyQualifiedName~Nebula`: 726 passed, 26 skipped, 0 failed/752, 8 s. Windows, .NET 10 Release, `--no-build --no-restore`. This name filter is broader than the historical focused suite.
- Nebula Python `python -m pytest tests -q`: 681 passed in 10.33 s using the project virtual environment.
- The repository root now ignores nested `.venv` directories so local Python test environments are not staged as project content; verified with `git check-ignore`.

## Remaining validation

These local unit suites did not connect to MariaDB, MongoDB, Supabase, Telegram, FTP, an installed server, or production. The current CI workflow defines separate jobs for Mongo-backed Nebula, MariaDB user schema, server MariaDB integration, and full-system recovery with MariaDB/MongoDB. GitHub Actions run 40795733017 for the current revision marked all 13 jobs failed before runner execution. Check annotations for the server and Nebula jobs confirm: account locked due to billing. No remote test ran; this is an infrastructure blocker, not a product-test failure. Do not interpret the historical three failures as current product failures or as evidence that integration currently passes.

MariaDB is the only relational database in use; no MySQL service is used.

### Validação ampla e regressões de download/persistência — 2026-10-29

- HTTP 206 sem `Content-Range` válido não pode alimentar um arquivo final. O feeder agora falha fechado; o fallback sequencial só aceita HTTP 200. Teste RED/green confirma preservação do destino anterior.
- Escrita de imagens usa a mesma faixa de lock por ID do `SaveItems`.
- Suíte inteira da solução .NET Release: 5.363 aprovados, 273 ignorados, 0 falhas/5.636 resultados em 17 assemblies; exit 0. Suíte Nebula Python 682/682 e compileall aprovado.
- Ignorados permanecem pendentes: testes opt-in/de plataforma, incluindo integração MariaDB sem conexão descartável configurada. CI hospedada, Linux nativo, runtime instalado e produção não validados.
- Homologação formal 2/7 (28,6%). MariaDB é o único banco relacional operacional. Nenhuma release publicada.

### Revalidação da concorrência de imagens — 2026-10-29 08:57 BRT

- Foi incluído `SaveImagesAsync_ConcurrentWritesForSameItem_DoNotMixImageSets` na integração MariaDB; cobre vinte escritas concorrentes de dois conjuntos e exige que o conjunto final esteja íntegro.
- Teste focal compilou e foi ignorado, 1/1, porque não havia conexão MariaDB descartável configurada.
- Solução .NET repetida depois da inclusão: 5.363 aprovados, 274 ignorados, 0 falhas/5.637 em 17 assemblies, exit 0. Testes Nebula Python seguem 682/682 e compileall aprovado.
- Sem homologação do banco em execução: MariaDB é o único banco relacional operacional. CI, Linux nativo, runtime instalado e produção pendentes; formal 2/7 e sem release.
