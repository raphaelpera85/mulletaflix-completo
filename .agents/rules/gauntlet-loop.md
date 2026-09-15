# Metodologia Gauntlet Loop (Obrigatória no Antigravity)

Esta regra define o protocolo operacional mandatório de execução, verificação e aceitação de código para o agente Antigravity neste projeto.

---

## 1. Princípio Fundamental: Generator vs. Evaluator
O agente NUNCA deve confiar no próprio código apenas porque o escreveu. O ciclo de vida de qualquer tarefa divide-se obrigatoriamente em:

1. **Definição da Quality Bar (Critério de Aceite)**:
   - Antes de iniciar a implementação, o agente deve identificar ou estipular como o sucesso será provado (testes unitários, build estático, validação de tipos, execução real de ponta a ponta).
2. **Fase Builder (Construção)**:
   - Implementa o código com padrões de engenharia limpa, sem cortes de atalhos e mantendo a arquitetura do projeto.
3. **Fase Gauntlet (O Teste de Fogo / Crítico Cego)**:
   - O agente se coloca na posição de revisor adversário implacável.
   - Executa as ferramentas de checagem real:
     - Checagem estática de tipagem (`tsc --noEmit`, etc.).
     - Linter e formatação.
     - Suíte de testes automatizados (`dotnet test`, `./gradlew test`, `npm test`).
     - Build de produção quando aplicável.
4. **Loop de Refinamento**:
   - Se o Gauntlet falhar em 1 único teste ou acusar erro de tipo/compilação, a tarefa é rejeitada.
   - O agente diagnostica a falha, corrige o código e reexecuta o Gauntlet até obter **100% de aprovação comprovada**.

---

## 2. Proibição de Alucinação de Conclusão
- Afirmar que algo "está pronto", "deve funcionar" ou "foi implementado" sem a saída comprovada do terminal (código de saída `0`) é uma violação grave desta regra.
- Evidências reais de terminal são mandatórias antes de qualquer encerramento de tarefa.
