# Limites do conjunto de alterações

Este arquivo registra a separação operacional da auditoria no workspace. Ele não
cria commits nem presume que todo arquivo modificado tenha sido criado nesta
rodada; serve para revisão antes de qualquer integração.

## Grupos

- `backend`: correções do servidor, migrações, health/readiness, segurança e
  testes em `MulletaFlix-master`.
- `frontend`: correções de wizard, loading/erro/retry, navegação, playback e
  testes em `MulletaFlix-web-master`.
- `packaging`: NSIS, scripts de stage/smoke test, README de packaging e build do
  instalador.
- `audit-docs`: `TODO-AUDITORIA.md`, evidências e este mapa de alterações.
- `skills`: arquivos auxiliares locais em `.agents/skills`; não fazem parte do
  runtime nem do instalador.
- `other`: arquivos fora das fronteiras acima; exigem revisão manual do
  proprietário antes de commit.

## Estado observado em 2026-09-11

O workspace possui alterações não commitadas distribuídas assim:

| Grupo | Arquivos | Regra de integração |
| --- | ---: | --- |
| backend | 38 | integrar junto com testes .NET correspondentes |
| frontend | 211 | integrar junto com build, lint e testes frontend |
| packaging | 9 | integrar somente após hash e `validate-stage.ps1` |
| audit-docs | 3 | integrar com o relatório da auditoria |
| skills | 1 | manter separado do produto, salvo decisão explícita |
| other | 2 | revisar individualmente; não assumir autoria |

Os números são um snapshot gerado por `git status --short`; devem ser
recalculados antes de criar commits. A ausência de commits nesta rodada é
intencional para preservar alterações locais pré-existentes.

## Gates por grupo

- Backend: build Release e testes focados/afetados.
- Frontend: `npm run build:check`, lint incremental e suíte Vitest.
- Packaging: parser PowerShell, compilação NSIS, `validate-stage.ps1` e hash do
  instalador.
- Auditoria: atualizar `TODO-AUDITORIA.md` e `docs/AUDIT-EVIDENCE.md` somente
  com evidência reproduzível.
