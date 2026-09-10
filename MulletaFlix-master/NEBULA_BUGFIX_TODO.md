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
- [x] Run focused Nebula tests and report remaining runtime-only gates.

The focused Nebula suite is green (144/144). The full integration suite still has
three environment-dependent failures when the test MySQL/MariaDB service is not
available; these are not product assertion failures.
