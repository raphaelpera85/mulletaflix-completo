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

#### Identidade de serviço selecionada — 2026-10-23 19:53 BRT

A concessão explícita agora deriva de `ServiceAccountType`: NetworkService recebe Modify; LocalSystem escreve por sua ACE SYSTEM; LocalService é aceito somente como identidade já configurada em instalações legadas. O instalador remove ACEs antigas para os três SIDs de serviço antes de criar a concessão atual, evitando acesso residual para contas não selecionadas. Administradores e SYSTEM preservam Full. `pwsh -NoProfile -File scripts/tests/ServiceDataAcl.Tests.ps1` passou com fixture Windows e sentinel externo. Não foi compilado NSIS nem executado instalador/serviço real. O ajuste cobre apenas o diretório de dados do instalador e não comprova ACL efetiva dos ancestrais, raízes de restore, journal/ZIP, Linux, Docker ou produção; o runtime de restore permanece inalterado e TOCTOU P0 continua aberto.

#### Sincronização da identidade efetiva do serviço — 2026-10-23 20:05 BRT

O upgrade agora aplica no Service Control Manager a conta selecionada em cada ramo (`NetworkService`, `LocalSystem` ou `LocalService` legado), em vez de atualizar somente NetworkService e deixar a conta anterior ativa. Usa `sc.exe config` com o tratamento de erro/retry existente; os grants ACL permanecem alinhados ao mesmo valor de `ServiceAccountType`. `ServiceDataAcl.Tests.ps1` passou. Compilação NSIS com o stage existente passou e gerou somente artefato isolado em `.build/service-acl-validation/mulletaflix_12.1.24_windows-x64.exe` (423.349.215 bytes; SHA-256 `9488AAD16EB79ED5D48D87F5E074644523A0BE58AEE1708CA705C734F642C784`); não foi copiado à pasta `dist` nem ao diretório de assets da release. Não foram executados instalador, upgrade, serviço real ou smoke administrativo. Homologação formal permanece 2/7, sem crédito. ACL efetiva de restore/ancestrais, Linux/Docker e TOCTOU P0 seguem pendentes.
