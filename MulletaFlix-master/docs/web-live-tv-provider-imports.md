# Carregamento de provedores do guia de TV ao vivo

O controlador `MulletaFlix-web-master/src/apps/dashboard/controllers/livetvguideprovider.ts` carrega os formulários dos provedores XMLTV e Schedules Direct. O Vite não transforma `import()` construído a partir de um alias variável em um mapa de módulos, então a distribuição deixava o identificador `components/tvproviders/*.template.html` no bundle. O navegador falhava ao resolver esse identificador e a página apresentava o alerta genérico.

O controlador usa `import.meta.glob` para gerar os mapas de módulos TypeScript e templates HTML suportados. A verificação `MulletaFlix-web-master/scripts/verify-build-artifacts.mjs` confirma que os templates de ambos os provedores entram no bundle e que o padrão de import variável não reaparece.

## Evidência de 2026-10-27

- A captura de produção mostrou a falha para XMLTV e Schedules Direct.
- `npm run build:check`, `npm run build:production` e `npm run verify:build` passaram localmente após a correção.
- Os assets compilados incluem os módulos `xmltv` e `schedulesdirect` e seus templates.
- A instalação no servidor e a verificação visual após atualizar permanecem pendentes.
