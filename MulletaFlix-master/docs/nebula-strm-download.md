# Nebula STRM Downloader

The STRM downloader reads only the source directories configured in `MonitorPaths` and processes one item at a time. For roots composed of direct category folders (such as `D:\midias\Animações`, `Filmes`, `Series`, `Doramas` and `Novelas`), it scans, sorts and processes one category batch at a time, in configured priority order; the first download no longer waits for lower-priority categories to be scanned. Partial downloads are prioritized within the current category so the category ordering is preserved. Flat/mixed roots use the compatible full-tree scan. During a large category scan the worker emits progress every 5,000 eligible files, and the panel displays indexed directories/media instead of looking idle.

The scanner caches category detection per source directory because category is determined by the directory hierarchy. This preserves the existing category and title ordering while avoiding repeated Unicode/path normalization for every media file in large libraries.

## Operational verification

1. Confirm `MonitorPaths` points to existing source folders containing `.strm` or supported media files.
2. Start **Download STRM** and confirm the current step changes from idle to indexing.
3. Wait for the scan count. A non-zero count should be followed by `Iniciando download` for an eligible item; zero means the configured roots contain no eligible files.
4. If indexing does not finish, inspect the Nebula downloader log for Python stderr and the last scan message. A running Python process alone does not prove that a media item has started.

### Automatic startup gate

`NebulaHostedService` starts the automatic pipeline only when `NebulaFtpConfiguration.Enabled` is true. When enabled, the hosted pipeline starts Envio before the downloader and may mount drive N: when `UseMappedDrive` is true. Disabling the global setting therefore prevents automatic downloader startup; it is not a downloader-only switch. Manual downloader startup is a separate path and must be verified independently.

In the production instance inspected on 2026-10-16, `Enabled` was false and the server log reported that NebulaFTP was disabled. A manual downloader start was then confirmed through the UI; the worker stayed in the full scan for 11 minutes 55 seconds without reaching the first download, and the panel remained at 0% / “Downloader parado.” This reproduces the delay and stale feedback in installed 12.1.21. The source now scans and drains recognized category folders sequentially and reports scan progress, but has not yet been deployed to that production instance. Do not enable the global setting solely as a downloader test while uploads may be active.

Automated validation for the scanner, downloader loop and ingestion ordering is in `Tools/NebulaPython/tests/test_strm_downloader_helpers.py`, `test_strm_downloader.py` and `test_ingestion_order.py`. The full NebulaPython test suite passed 571/571 on Python 3.10, including tests for incremental category ordering and mixed-layout fallback. `Jellyfin.Server.Implementations` built in Release with 0 errors (1,077 analyzer/style warnings). Portal tests passed 34/34 in the monorepo mirror and 21/21 in the independent portal checkout. Production download completion with the updated binary remains unverified.

Production smoke testing must be performed separately on the active server. Do not stop or restart Nebula while uploads are active merely to validate a downloader change.
