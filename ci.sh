#!/usr/bin/env bash
# MulletaFlix local CI — same pipeline as .github/workflows/ci.yml
# Usage: bash ci.sh   (from workspace root)
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$ROOT"

PASS=0
FAIL=0

step() { echo ""; echo "=== $1 ==="; }
ok()   { echo "  ✅ $1"; PASS=$((PASS+1)); }
fail() { echo "  ❌ $1"; FAIL=$((FAIL+1)); }

# ── Server: restore + build (Release) ────────────────────────────────
step "Server: restore"
if (cd MulletaFlix-master && dotnet restore MulletaFlix.sln > /tmp/mf-restore.log 2>&1); then
  ok "restore"
else
  fail "restore — see /tmp/mf-restore.log"; tail -20 /tmp/mf-restore.log
fi

step "Server: build Release"
if (cd MulletaFlix-master && dotnet build MulletaFlix.sln -c Release --no-restore > /tmp/mf-build.log 2>&1); then
  ok "build"
else
  fail "build — see /tmp/mf-build.log"; tail -30 /tmp/mf-build.log
fi

step "Server: audit NuGet dependencies"
if (cd MulletaFlix-master && dotnet list MulletaFlix.sln package --vulnerable --include-transitive > /tmp/mf-nuget-audit.log 2>&1); then
  ok "NuGet audit"
else
  fail "NuGet audit — see /tmp/mf-nuget-audit.log"; tail -30 /tmp/mf-nuget-audit.log
fi

step "Server: unit tests (integration excluded)"
if (cd MulletaFlix-master && dotnet test MulletaFlix.sln -c Release --no-build \
      --filter "FullyQualifiedName!~Integration" \
      --logger "console;verbosity=minimal" > /tmp/mf-test.log 2>&1); then
  ok "tests"
  grep -E "Aprovado!|Passed!|Failed!|Falha" /tmp/mf-test.log | tail -3
else
  fail "tests — see /tmp/mf-test.log"; grep -E "Falha|Failed|error" /tmp/mf-test.log | head -20
fi

# ── Web: npm ci + production build ───────────────────────────────────
step "Web: npm ci"
if (cd MulletaFlix-web-master && npm ci > /tmp/mf-npm.log 2>&1); then
  ok "npm ci"
else
  fail "npm ci — see /tmp/mf-npm.log"; tail -20 /tmp/mf-npm.log
fi

step "Web: production build"
if (cd MulletaFlix-web-master && npm run build:production > /tmp/mf-web.log 2>&1); then
  ok "web build"
else
  fail "web build — see /tmp/mf-web.log"; tail -30 /tmp/mf-web.log
fi

step "Web: unit tests"
if (cd MulletaFlix-web-master && npm test -- --run > /tmp/mf-web-test.log 2>&1); then
  ok "unit tests"
else
  fail "unit tests — see /tmp/mf-web-test.log"; tail -30 /tmp/mf-web-test.log
fi

step "Web: verify build artifacts"
if (cd MulletaFlix-web-master && npm run verify:build > /tmp/mf-web-artifacts.log 2>&1); then
  ok "build artifacts"
else
  fail "build artifacts — see /tmp/mf-web-artifacts.log"; tail -30 /tmp/mf-web-artifacts.log
fi

step "Web: audit production dependencies"
if (cd MulletaFlix-web-master && npm audit --omit=dev --audit-level=high > /tmp/mf-npm-audit.log 2>&1); then
  ok "npm audit"
else
  fail "npm audit — see /tmp/mf-npm-audit.log"; tail -30 /tmp/mf-npm-audit.log
fi

echo ""
echo "════════════════════════════════════════"
echo "  CI LOCAL: $PASS ok, $FAIL falhas"
echo "════════════════════════════════════════"
[ "$FAIL" -eq 0 ]
