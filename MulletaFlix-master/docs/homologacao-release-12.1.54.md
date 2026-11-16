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
- `git diff --check`: executado antes da publicação.

## Limites

- A leitura da imagem não foi repetida no navegador/runtime de produção nesta validação.
- Os assets do instalador Windows e do atualizador ainda dependem do workflow de release acionado pela tag.
- A instalação limpa `npm ci` reportou 8 vulnerabilidades moderadas no conjunto de dependências; não foram aplicadas atualizações fora do escopo da correção.
