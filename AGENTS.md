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
     1. Gere o pacote de atualização do servidor via `.\build-update-package.ps1`. Este passo também compila o instalador executável (`stage` -> `.\build-mulletaflix-installer.ps1`), então a release do servidor sempre leva **dois** assets: `mulletaflix-update-win-x64.zip` e `mulletaflix_<versao>_windows-x64.exe`. Nunca use `-SkipInstaller` numa release oficial.
     2. Gere o pacote do aplicativo Android via `.\build-app-package.ps1`.
     3. Atualize as releases oficiais no GitHub Releases separadamente para cada sistema: via `.\publish-release.ps1` (release do servidor com o zip **e** o instalador executável) e via `.\publish-app-release.ps1` (release do aplicativo Android com o APK) antes de finalizar a tarefa. `publish-release.ps1` recusa publicar sem o instalador; `-AllowMissingInstaller` só existe para releases deliberadamente sem instalador.
     4. Confirme o resultado consultando a API de releases e verificando que os dois assets estão anexados antes de declarar a tarefa concluída.
