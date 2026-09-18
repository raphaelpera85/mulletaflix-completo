# Antigravity Global Project Directives

Este projeto e todas as suas sessões de desenvolvimento são governados estritamente pela metodologia **Gauntlet Loop**.

## Diretrizes Obrigatórias do Agente

1. **Metodologia Gauntlet Loop**:
   - Para qualquer solicitação de implementação, correção de bug ou refatoração, o agente deve operar no ciclo **Builder vs. Evaluator**.
   - A barra de qualidade ("Quality Bar") deve ser testada e comprovada através dos comandos reais de terminal do projeto (`dotnet test`, `npm run build:check`, `./gradlew testDebugUnitTest`).
   - Nenhuma tarefa pode ser finalizada baseada em suposição. Somente evidências reais de execução com código de saída 0 são aceitas.

2. **Regras e Convenções do Repositório**:
   - Siga sempre as regras descritas em `.agents/rules/mulletaflix-conventions.md` e `.agents/rules/gauntlet-loop.md`.
   - Mantenha a integridade da arquitetura, sem quebra de convenções estabelecidas.

3. **Atualização Mandatória de Release (Servidor e Aplicativo)**:
   - Sempre que qualquer alteração, correção ou funcionalidade for concluída e testada:
     1. Gere o pacote de atualização do servidor via `.\build-update-package.ps1`.
     2. Gere o pacote do aplicativo Android via `.\build-app-package.ps1`.
     3. Atualize as releases oficiais no GitHub Releases via `.\publish-release.ps1` (anexando o zip do servidor e o APK) e via `.\publish-app-release.ps1` (release dedicada do app) antes de finalizar a tarefa.
