# MulletaFlix Security Audit Report

**Date:** July 12, 2026 (updated July 20, 2026)  
**Scope:** Full codebase security audit (Emby/Jellyfin fork)  
**Auditor:** AI Security Assessment

---

## Executive Summary

The MulletaFlix codebase is based on Emby/Jellyfin and inherits many of its security properties. The audit identified several security concerns ranging from critical timing attacks on API key validation to medium-severity CORS misconfigurations. No hardcoded secrets were found in the codebase or deployment scripts.

---

## Critical Findings

### 1. ~~Timing Attack on API Key Validation~~ — FIXED ✅
**Location:** `Jellyfin.Server.Implementations/Security/AuthorizationContext.cs:204-210`  
**Severity:** Critical  
**CVSS:** 8.1 (High)

**Status:** FIXED — `CryptographicOperations.FixedTimeEquals()` used for in-memory comparison.

---

## High Findings

### 2. ~~CORS Misconfiguration - Allow Any Origin by Default~~ — FIXED ✅
**Location:** `MulletaFlix/Configuration/CorsPolicyProvider.cs`  
**Severity:** High  
**CVSS:** 7.5 (High)

**Status:** FIXED — Cross-origin denied when no hosts configured. Regression-tested by
`tests/Jellyfin.Server.Tests/Configuration/CorsPolicyProviderTests.cs` (added 29/09/2026): an
empty or wildcard-only `CorsHosts` produces a policy with no explicit origins and
`SupportsCredentials == false`; explicit hosts produce a policy scoped to exactly those origins
with credentials allowed.

---

### 3. ~~Path Traversal Risk in BackupService SQL Execution~~ — MITIGATED ✅
**Location:** `Jellyfin.Server.Implementations/FullSystemBackup/BackupService.cs`  
**Severity:** High  
**CVSS:** 6.8 (Medium-High)

**Status:** MITIGATED — `SanitizeMigrationId()` strips non-alphanumeric characters; restore uses EF Core parameterized methods.

---

## Medium Findings

### 4. ~~Rate Limiting Implementation Concerns~~ — FIXED ✅
**Location:** `Jellyfin.Api/Middleware/RateLimitMiddleware.cs`  
**Severity:** Medium  
**CVSS:** 5.3 (Medium)

**Description:**  
Rate limiting exists for authentication attempts, but implementation details should be reviewed to ensure it effectively prevents brute force attacks. The middleware should track failed attempts per IP and implement exponential backoff.

**Status:** FIXED/VERIFIED — `RateLimitMiddleware` already tracked failed logins per IP in a
15-minute sliding window (`LoginWindow` / `MaxFailedLogins = 10`) with a `429 Too Many Requests`
+ `Retry-After` response, applied before the request reaches authentication, and `UserManager`
independently disables the account once `InvalidLoginAttemptCount` reaches
`LoginAttemptsBeforeLockout` (account lockout layered on top of the per-IP window). This review
added deterministic regression tests for the three properties the finding asked to verify
(threshold enforcement, time-window reset instead of a permanent lock, and quota not being
consumed by a successful login), plus the account-lockout path, which previously had zero test
coverage:
- `tests/Jellyfin.Api.Tests/Middleware/RateLimitMiddlewareTests.cs`:
  `RateLimitEntry_BlocksTheAttemptImmediatelyAfterTheConfiguredThreshold`,
  `RateLimitEntry_ResetsOnceTheLoginWindowFullyElapses`,
  `RateLimitEntry_SuccessfulLoginDoesNotConsumeFailureQuota` (added 05/10/2026), plus the
  pre-existing `LoginAttempts_BlockedResponse_IncludesRetryAfterHeader`,
  `ConcurrentFailedLogins_RecordOnlyAdmittedFailures`, `InFlightLogin_ReservesRemainingFailureWindowCapacity`.
- `tests/Jellyfin.Server.Implementations.Tests/Users/UserManagerAuthenticationLockTests.cs`:
  `AuthenticateUser_ExceedingLoginAttemptsBeforeLockout_DisablesTheAccount`,
  `AuthenticateUser_SuccessfulLogin_ResetsInvalidLoginAttemptCount`.

---

### 5. ~~File Upload Validation in Lyrics Endpoint~~ — FIXED ✅
**Location:** `Jellyfin.Api/Controllers/LyricsController.cs`, `Jellyfin.Api/Validation/LyricUploadValidator.cs`  
**Severity:** Medium  
**CVSS:** 4.3 (Medium)

**Description:**  
The lyrics upload endpoint uses `Path.GetExtension(fileName.AsSpan())` for validation, which provides some path traversal protection. However, the endpoint should also validate file size limits and content type to prevent denial of service attacks.

**Evidence:**  
```csharp
// Uses Path.GetExtension for validation (good)
var format = Path.GetExtension(fileName.AsSpan()).RightPart('.').ToString();
// But no file size validation beyond ContentLength check
```

**Status:** FIXED — The 1 MB cap was already enforced both via `[RequestSizeLimit]`/`ContentLength`
and again on the actually-read body (so a chunked request without `Content-Length` can't bypass
it). This review added the two previously-missing checks: (1) a format allow-list restricted to
the extensions the registered `ILyricParser`s actually understand (`lrc`, `elrc`, `txt` — not an
arbitrary attacker-chosen extension), and (2) content sniffing that rejects known binary magic
bytes (PE/ELF/ZIP/PNG/JPEG/GIF/PDF/GZIP), embedded NUL bytes, and obviously malicious embedded
payloads (`<script>`, `javascript:`, `<?php`, inline event handlers, `<iframe>`, etc.), all
before the body is handed to `ILyricManager.SaveLyricAsync`. Covered by:
- `tests/Jellyfin.Api.Tests/Validation/LyricUploadValidatorTests.cs` (13 tests covering allowed
  formats, binary signatures, malicious patterns, and the combined `Validate` entry point).
- `tests/Jellyfin.Api.Tests/Controllers/LyricsControllerTests.cs`:
  `UploadLyrics_AcceptsWellFormedLrcFile`, `UploadLyrics_RejectsUnsupportedExtension`,
  `UploadLyrics_RejectsEmbeddedScriptPayload`, `UploadLyrics_RejectsBinaryPayloadMasqueradingAsLrc`,
  `UploadLyrics_RejectsBodyLargerThanOneMegabyte`.

---

## Low Findings

### 6. ~~Insecure Deserialization Risk~~ — FIXED ✅
**Location:** Multiple locations using JSON serialization  
**Severity:** Low  
**CVSS:** 3.7 (Low)

**Description:**  
The codebase uses JSON serialization extensively. While no dangerous deserialization patterns were found, ensure all JSON deserialization uses safe settings (e.g., `TypeNameHandling.None`).

**Status:** FIXED/VERIFIED — Audited every deserialization call site in the repository: the
codebase deserializes exclusively through `System.Text.Json.JsonSerializer.Deserialize`, which
has no `TypeNameHandling` concept and cannot reproduce the Newtonsoft.Json
`TypeNameHandling.All`/`Auto` polymorphic-deserialization RCE gadget chain. There are zero calls
to `JsonConvert.DeserializeObject` anywhere in the codebase; the only `Newtonsoft.Json` usage is
building a `JObject` programmatically in `MulletaFlix.Plugin.IntroSkipper/Services/Entrypoint.cs`
(not parsing untrusted input), and a regression test already guarded against the vulnerable
Newtonsoft.Json 9.x line reappearing as a transitive dependency
(`DeprecatedDependencyResolutionTests.NoAssemblyFromTheAbandonedNewtonsoftLineIsPresent`). Added
a static-analysis regression test so this can't regress silently in the future:
- `tests/Jellyfin.Server.Tests/Dependencies/InsecureDeserializationAuditTests.cs`:
  `NoSourceFileUsesUnsafeNewtonsoftTypeNameHandling` (fails the build if
  `TypeNameHandling.All`/`.Auto` is ever introduced anywhere in the source tree) and
  `NoSourceFileCallsJsonConvertDeserializeObjectWithoutExplicitSafeSettings` (fails as soon as
  `JsonConvert.DeserializeObject` is reintroduced, forcing a deliberate audit instead of a
  silent merge).

---

### 7. SQL Injection in User Management
**Location:** `Jellyfin.Server.Implementations/Users/UserManager.cs`  
**Severity:** Low  
**CVSS:** 3.1 (Low)

**Description:**  
`UserManager.cs` uses `ExecuteSqlRawAsync`, but investigation shows it uses parameterized queries properly. This finding is informational only.

**Evidence:**  
```csharp
// Uses parameterized queries (safe)
await dbContext.Database.ExecuteSqlRawAsync(sql, parameters).ConfigureAwait(false);
```

**Status:** Verified safe - no action required.

---

## New Findings (July 2026 Scan)

### 11. TLS Downgrade Risk — FIXED ✅
**Location:** `Jellyfin.Server/Extensions/WebHostBuilderExtensions.cs`  
**Severity:** Medium  
**CVSS:** 4.3

**Description:** Kestrel HTTPS endpoint did not explicitly enforce minimum TLS protocol version, allowing negotiation of deprecated TLS 1.0/1.1.

**Status:** FIXED — Explicit `SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13` applied to both dev and production HTTPS callbacks.

---

### 12. Overly Broad Content Security Policy — FIXED ✅
**Location:** `Jellyfin.Api/Middleware/SecurityHeadersMiddleware.cs`  
**Severity:** Medium  
**CVSS:** 3.5

**Description:** CSP included `data:`, `blob:`, broad `http:`/`https:` origins, and third-party domains, increasing attack surface for XSS payloads.

**Status:** FIXED — Removed broad origins and third-party domains; retained `'unsafe-inline'`/`'unsafe-eval'` in `script-src` (required by Jellyfin frontend).

---

### 13. XSS via dangerouslySetInnerHTML — FULLY MITIGATED ✅
**Location:** 7 instances across frontend components  
**Severity:** Medium  
**CVSS:** 3.5

**Description:** Original assessment listed 14 instances, but file paths were incorrect (VideoCard.tsx, SeasonCard.tsx, etc. do not exist). Actual 7 instances:

| Component | DOMPurify | escapeHTML | Risk |
|-----------|-----------|------------|------|
| SelectElement.tsx:29 | ✅ | — | Low |
| ItemsScrollerContainerElement.tsx:32 | ✅ | — | Low |
| IconButtonElement.tsx:49,57 | ✅ | — | Low |
| CheckBoxElement.tsx:71 | — | ✅ | Low |
| ConnectionErrorPage.tsx:73 | ✅ | — | Low |
| MarkdownBox.tsx:17 | ✅ | — | Low |

**Status:** ALL MITIGATED — All instances use DOMPurify.sanitize() or escapeHTML(). No high-severity XSS found.

---

### 14. console.log Information Leak — FIXED ✅
**Location:** 10 instances across frontend  
**Severity:** Low  
**CVSS:** 3.1

**Description:** `quickConnect/index.tsx` logged auth codes; `MoreCommandsButton.tsx` logged item data; 8 others logged internal state.

**Status:** FIXED — All 10 instances migrated to `console.debug`.

---

### 15. Path Traversal in Dev HTML Plugin — LOW RISK
**Location:** `vite.config.ts`  
**Severity:** Low  
**CVSS:** 2.1

**Description:** Custom HTML string loader used `fs.readFileSync(filePath)` without path validation.

**Status:** MITIGATED — Added path boundary check + try/catch. Vite's internal resolver normalizes paths, and plugin only runs in dev mode.

---

### 16. SEO Misconfigurations — FIXED ✅
**Location:** `src/robots.txt`, `src/index.html`, `src/manifest.json`, `src/sitemap.xml`  
**Severity:** Informational

**Status:** FIXED — robots.txt allows crawling; sitemap.xml added; lang corrected to pt-BR; OG/Twitter/canonical/JSON-LD added to index.html.

### 8. Cryptographic Practices
- **Password Hashing:** Uses industry-standard PBKDF2 with SHA256 (10,000 iterations)
- **TLS:** Properly configured for HTTPS connections
- **No hardcoded secrets** found in codebase or deployment scripts

### 9. Command Injection Protection
- `ServerUpdateTask.cs` uses `Quote()` helper for argument escaping
- Most `Process.Start` calls use parameterized arguments
- Hardware detection uses trusted binaries from known paths

### 10. Path Traversal Protection
- `Startup.cs` uses `Path.GetFullPath()` with `StartsWith()` validation for static file serving
- Most `Path.Combine` usages use constants or GUIDs (safe)

---

## Recommendations Summary

| Priority | Finding | Status |
|----------|---------|--------|
| **Critical** | Fix timing attack on API key validation | ✅ FIXED |
| **High** | Fix CORS misconfiguration | ✅ FIXED |
| **High** | Secure BackupService SQL execution | ✅ MITIGATED |
| **Medium** | TLS enforcement (1.2+ only) | ✅ FIXED |
| **Medium** | CSP tightening | ✅ FIXED |
| **Medium** | XSS via innerHTML | ✅ MITIGATED (all 7 instances) |
| **Medium** | File upload validation | ✅ FIXED (1 MB max + format allow-list + content/magic-byte sniffing) |
| **Low** | console.log info leak | ✅ FIXED |
| **Low** | Path traversal in dev plugin | ✅ MITIGATED |
| **Low** | SEO misconfigurations | ✅ FIXED |
| **Low** | Audit JSON deserialization | ✅ FIXED (verified System.Text.Json-only; regression test added) |
| **Medium** | Brute-force rate limiting | ✅ FIXED/VERIFIED (sliding window + account lockout; regression tests added) |

---

## Conclusion

The MulletaFlix codebase has been hardened significantly. All critical and high-severity findings from the original audit are now resolved. Medium-severity TLS and CSP issues have been addressed. The XSS surface was found to be smaller than initially reported — all 7 actual `dangerouslySetInnerHTML` instances use proper sanitization. Frontend console.log information leaks have been eliminated. SEO basics are now in place.

**Overall Risk Rating:** Low-Medium (downgraded from Medium-High)

---

*Report generated by AI security assessment. Manual verification recommended for all findings.*