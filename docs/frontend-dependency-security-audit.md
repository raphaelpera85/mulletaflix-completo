# Auditoria de segurança das dependências do frontend

Data: 2026-10-21
Escopo: `MulletaFlix-web-master` e os assets web empacotados pelo servidor.

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
