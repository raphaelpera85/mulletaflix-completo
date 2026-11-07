# MulletaFlix Playwright Summary
- Gerado em: 2026-10-30T03:50:33.700Z
- Base URL: http://127.0.0.1:18096
- Status: failed
- Especificações encontradas: 20
- Total de testes: 1
- Passou: 0
- Falhou: 1
- Instáveis: 0
- Ignorados: 0
- Duração: 235114.395 ms
## Suítes
- stage: failed (0/1 aprovados, 1 falhas, 0 ignorados) em http://127.0.0.1:18096
- viteFixtures: filtered-out (0/0 aprovados, 0 falhas, 0 ignorados) em http://127.0.0.1:18197
## Probe do Stage
- Acessível: sim
- StartupWizardCompleted: true
## Conclusão
A suíte falhou. Corrija os pontos listados no relatório antes de expandir a cobertura.
## Premissas
- Os specs devem continuar separados do código de apoio em tests/playwright/specs.
- O runner nunca acessa nem inicia stage na porta de produção 8096; usa portas isoladas 18096-18196.
- Stage inicial limpo confirmado com StartupWizardCompleted=false
- StartupWizardCompleted=true
- Specs que importam módulos React/TypeScript por /@fs ou imports Vite executam em uma instância Vite isolada, separada do stage .NET.
- Relatório JSON da suíte .NET gerado e consolidado.
- Relatório JSON da suíte Vite gerado e consolidado.
## Verificações de estado
- Wizard limpo: não
- Admin: sim
- Usuário comum: sim
- Login: sim