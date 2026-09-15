---
name: gauntlet-loop
description: >-
  Executa tarefas de desenvolvimento sob a metodologia Gauntlet Loop: separação estrita entre Construtor (Builder) e Crítico Adversário (Critic/Evaluator). Use para qualquer tarefa de implementação, correção de bugs, refatoração ou novas features onde seja mandatório provar a conclusão com testes reais, builds e zero tolerância a falhas.
---

# Metodologia Gauntlet Loop

A metodologia **Gauntlet Loop** garante que nenhuma alteração de código seja aceita sem passar por uma bateria impiedosa de testes e verificações ("The Gauntlet").

---

## 🔁 Fluxo de Execução Obrigatório

### 1. Definição do Gauntlet (Critérios de Aceite)
Antes de editar qualquer arquivo, defina os comandos de terminal que constituem a barra de qualidade (Quality Bar):
- **Tipagem / Compilação**: Ex: `tsc --noEmit`, `dotnet build --no-restore`, etc.
- **Testes Unitários / Regressão**: Ex: `dotnet test`, `./gradlew testDebugUnitTest`, `npm test`.
- **Build / Integridade**: Ex: `npm run build:production`, `./gradlew assembleDebug`.

### 2. Fase Builder (Implementação)
- Realize as alterações no código de maneira atômica e limpa.
- Respeite as convenções do repositório (`mulletaflix-conventions.md`).
- Não interrompa o raciocínio no meio da solução; finalize a hipótese de código antes de testar.

### 3. Fase Gauntlet (O Crítico Implacável)
Execute as validações definidas no Passo 1:
1. Dispare os comandos reais no terminal.
2. Analise a saída com rigidez:
   - Houve algum erro de compilação ou tipo?
   - Algum teste quebrou (mesmo não relacionado diretamente)?
   - O código de saída foi diferente de 0?

### 4. O Loop de Convergência (Iteração de Correção)
- Se **qualquer falha** for detectada pelo Gauntlet:
  1. Capture a mensagem exata de erro e a causa raiz (sem tentar adivinhar).
  2. Retorne imediatamente ao papel de **Builder** para aplicar a correção.
  3. Reenvie a nova versão para o **Gauntlet**.
- **Critério de Saída do Loop**: 100% dos testes e comandos de validação aprovados com evidência real no terminal.
