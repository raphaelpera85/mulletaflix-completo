# Homologação da release 12.1.54

Data: 2026-11-16

## Escopo

Corrigir a imagem de perfil que não carregava para o usuário autenticado. O cliente `getUserImageUrl` cria uma URL de imagem, mas o elemento `<img>` não envia o cabeçalho de autorização usado pelas chamadas feitas pelo cliente HTTP. O componente agora anexa a chave de sessão como `api_key` na URL autenticada e preserva a URL original quando não há chave.

## TDD e evidências

- RED: o teste novo falhou porque a URL esperada precisava incluir `api_key` e o helper devolvia somente `tag`.
- GREEN: `UserAvatar.test.tsx` cobre a URL autenticada, ausência de tag principal e sessão sem chave.
- Suíte web da árvore limpa: 84 arquivos, 444 testes aprovados, 0 falhas.
- TypeScript (`npm run build:check`): exit 0.
- ESLint dos dois arquivos alterados: exit 0.
- Build de produção e `verify:build`: exit 0; 1.926 arquivos válidos; payload inicial 299,8 KiB gzip (orçamento 420 KiB).
- Publicação do servidor Windows Release: concluída; versão do executável em stage 12.1.54.
- `Assert-StageIntegrity`: aprovado; pacote inclui os componentes obrigatórios. Instalador NSIS produzido com 423,8 MB.
- ZIP de atualização: 390,73 MB, 3.262 entradas, hashes verificados e executável, DLL, script de atualização e webroot presentes.
- Pacote Linux x64 cross-publish: exit 0; 2.464 entradas e executável, instalador e webroot presentes.
- Release API: `v12.1.54` publicada em 2026-11-16, marcada Latest, com Linux tarball, instalador Windows e ZIP de atualização.
- Workflow Release: falhou antes de iniciar o primeiro job porque a conta está bloqueada por cobrança; os três pacotes foram montados e publicados localmente.
- `git diff --check`: executado antes da publicação.

## Limites

- A leitura da imagem não foi repetida no navegador/runtime de produção nesta validação.
- O instalador está unsigned e não foi executado em instalação limpa; Windows SmartScreen/Authenticode não foram validados.
- A instalação limpa `npm ci` reportou 8 vulnerabilidades moderadas no conjunto de dependências; não foram aplicadas atualizações fora do escopo da correção.
