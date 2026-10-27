# Auditoria de segurança das dependências do frontend

Data: 2026-10-21
Escopo: `MulletaFlix-web-master` e os assets web empacotados pelo servidor.

## Revalidação do toolchain — 2026-10-21

- Atualizados e fixados como `devDependencies`: `vitest`/`@vitest/coverage-v8` 5.0.3, `stylelint` 17.6.0, `@stylistic/stylelint-plugin` 5.3.1, `stylelint-scss` 7.3.0 e `stylelint-no-browser-hacks` 2.0.3. O parser transitivo vulnerável deixou de aparecer no audit.
- `npm audit --omit=dev --audit-level=low`: 0 vulnerabilidades. `npm audit --audit-level=low`: 10 altas, sem moderadas/críticas, todas na árvore exclusiva de desenvolvimento e ligadas ao `braces@3.0.3` sem release corrigida upstream ([GHSA-vfj7-8cjw-p6xm](https://github.com/advisories/GHSA-vfj7-8cjw-p6xm)). Não foi aplicado override artificial nem `npm audit fix --force`.
- Corrigidos advisories de Vitest e `@vitest/mocker` via 5.0.3 ([GHSA-qxg6-89mg-rqv8](https://github.com/advisories/GHSA-qxg6-89mg-rqv8), [GHSA-xp26-3454-mjgp](https://github.com/advisories/GHSA-xp26-3454-mjgp)) e de `postcss-selector-parser` via a atualização do Stylelint ([GHSA-rj75-hqrm-r3gf](https://github.com/advisories/GHSA-rj75-hqrm-r3gf)).
- Validação posterior: Vitest 418/418 em 75 arquivos; Stylelint, typecheck e build de produção aprovados; `verify:build` aprovou 1.926 artefatos e bundle inicial de 294,3 KiB gzip (orçamento 420 KiB). `.NET` NuGet audit sem pacote vulnerável.
- Isso é validação local, não homologação formal, release ou prova de segurança do host. O restante do audit completo permanece aberto até correção upstream ou migração de ferramenta que retire `braces` sem regredir lint/build.

## Resultado

- Antes da atualização, `npm audit` reportou 12 vulnerabilidades somente no conjunto de desenvolvimento: 1 moderada e 11 altas. `npm audit --omit=dev` reportou zero vulnerabilidades em dependências de produção.
- Vite 7.3.6 era uma dependência de desenvolvimento e estava no intervalo de três advisories para `launch-editor` no Windows (GHSA-698v-4r3x-3vcv, GHSA-r825-w8mm-5pxw, GHSA-587h-7wmj-h5pm). O audit ofereceu Vite 7.3.7 como correção patch.
- `package.json` e `package-lock.json` agora fixam Vite 7.3.7. Após a atualização, o audit completo caiu para 11 vulnerabilidades de desenvolvimento (1 moderada, 10 altas); o audit de produção permanece em zero.
- Os itens restantes envolvem sobretudo ferramentas Stylelint, `es-check` e dependências transitivas. A correção automática disponível exige mudanças major em algumas ferramentas; `stylelint-no-browser-hacks` não tem correção automática indicada. Não aplicar `npm audit fix --force` sem validar compatibilidade dos comandos de lint/build.

## Validação local

- `npm run build:check`: passou.
- `npm test -- --silent --reporter=dot`: 418/418, 75 arquivos.
- `npm run build:production`: passou; gerou e copiou assets estáticos.
- `npm run verify:build`: passou; 1.926 artefatos válidos, limite por arquivo 1.536 KiB e orçamento inicial 294,3 KiB gzip (máximo 420 KiB).
- Suíte .NET Release da solução, excluindo somente `DramaFindsLiveIntegrationTests`: exit 0. Contagens observadas incluem Server.Implementations 1.509 aprovados/65 ignorados, Providers 817/817 e API 763 aprovados/3 ignorados. Ignorados incluem dependências Mongo/MariaDB e testes de plataforma; não são aprovações.

## Limites e próximos passos

- O patch Vite atualiza ferramenta de desenvolvimento, não uma dependência de runtime servida ao usuário. Esta auditoria não valida produção, CI remoto, instalação ou reprodução de mídia.
- O restante do audit é dívida de segurança de desenvolvimento. Avaliar cada major migration com seus testes de lint e build antes de atualizar.
- O audit limpo com `--omit=dev` descreve o lock atual; executar novamente no gate de release e não transformar isso em alegação de segurança absoluta.
- A v12.1.30 inclui a atualização Vite e o endurecimento do packaging: o ZIP atualizador não copia arquivos transitórios `.coverage`/`.ruff_cache` para `Tools/NebulaPython`.
