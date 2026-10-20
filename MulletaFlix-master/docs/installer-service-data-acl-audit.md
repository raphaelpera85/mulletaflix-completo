# Auditoria de ACL do diretório de dados do instalador Windows

Data: 2026-10-19 (America/Sao_Paulo)

## Problema e alteração

O instalador configurava permissões de dados sem remover concessões herdadas amplas e usava propagação recursiva (`icacls /T`). Isso podia conservar grants de escrita a grupos gerais e alcançar descendentes com ACL própria; uma raiz de volume como destino também ampliaria excessivamente o escopo.

A política atualizada em `MulletaFlix-packaging-master/MulletaFlix-ux-custom/nsis/mulletaflix.nsi` rejeita o diretório de dados se ele resolver para raiz de volume/compartilhamento ou se o próprio diretório selecionado for reparse point. Na instalação como serviço, o `icacls` atua apenas na raiz, desabilita herança nessa raiz, remove ACEs Allow conhecidas de BUILTIN Users, Everyone e Authenticated Users, e mantém administradores, SYSTEM e as identidades de serviço suportadas. A instalação básica também não percorre a árvore. `Test-CleanInstaller.ps1` ganhou uma verificação negativa para grants de escrita desses grupos na raiz e em `config/system.xml`.

## Evidência executada

- `scripts/tests/ServiceDataAcl.Tests.ps1`: PASS; fixture temporário mostrou remoção de grants amplas herdadas e explícitas na raiz, herança normal para descendentes, identidades de serviço presentes e sentinel externo inalterado.
- `Assert-StageIntegrity.Tests.ps1`, `RcloneBootstrap.Tests.ps1`, `ServiceDataAcl.Tests.ps1`, `StageBackup.Tests.ps1` e `StageBuildOptions.Tests.ps1`: PASS.
- `node --test tools/release/tests/ci-packaging-coverage.test.mjs`: PASS, 1/1; CI referencia o novo teste.
- Parser PowerShell para os dois scripts ACL e `git diff --check`: PASS.
- Testes do portal: `node --test tests/homologacao.test.mjs`, PASS, 158/158; `node --check homologacao.js` e parse JSON: PASS.
- Primeira compilação NSIS encontrou a flag não suportada `StrCmp /I`; substituída por `kernel32::lstrcmpiW` e o teste estático passou a proibir a sintaxe inválida. Compilação Release então concluiu com sucesso em `.build/mfx-acl-validation`: MulletaFlix 12.1.24, 403,74 MiB, SHA-256 `D38E6A94CFBC2CB3E45CA1A8FCEE0BFA4FA39FB287335A54D7901ACBE2B99020`; build executado com `-NoCopyToReleaseAssets`.

## Limites e pendências

O teste não substitui o smoke de instalação limpa como administrador. A sessão atual não tem token administrativo, e os diretórios padrão de `ProgramFiles` e `ProgramData` já existem; não foi tentada instalação para evitar interferência. ACLs em descendentes que tenham herança protegida ou ACEs explícitas próprias não são normalizadas por esta operação não recursiva; grants amplas por outros grupos também precisam de inventário. Reparse point no próprio diretório escolhido é recusado, mas alterações concorrentes de caminho não foram testadas. Não houve instalação, alteração de ACL efetiva de produção, teste de runtime ou release. A homologação permanece `in_progress`. Portal Git `main` commit `45f6b8092268746ae95c6887ef4ec7b72a622179`; testes do portal 158/158 e endpoint público HTTP 200 com cache-bust confirmou evento/build e o limite de permissão.
