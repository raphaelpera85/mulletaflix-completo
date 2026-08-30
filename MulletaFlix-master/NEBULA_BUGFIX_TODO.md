# Nebula Bugfix TODO

Status:

- [x] Audit Nebula C# upload, download, streaming, Mongo, FTP mount, Supabase SQL, and bot API surfaces.
- [ ] Remove hardcoded source secrets and stop returning bot tokens from read APIs.
- [ ] Replace hardcoded FTP/rclone credentials with explicit configured credentials.
- [ ] Prevent filename-only collisions across different folders and staging paths.
- [ ] Make upload claiming respect worker ownership and skip unclaimed work.
- [ ] Validate completed-upload reuse against file size and part size before deleting source files.
- [ ] Validate HTTP range downloads before joining multipart files and clean partial artifacts on failure.
- [ ] Validate streamed Telegram parts against expected size and fail on inconsistent part offsets.
- [ ] Avoid killing unrelated rclone processes and serialize drive mount attempts.
- [ ] Generate Supabase SQL with RLS enabled instead of disabled.
- [ ] Add focused regression tests for the fixed Nebula failure modes.
- [ ] Run focused Nebula tests and report remaining runtime-only gates.
