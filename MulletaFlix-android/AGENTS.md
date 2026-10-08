# Regras de desenvolvimento Android

## TDD obrigatório

- Para correção, funcionalidade ou mudança de comportamento, escreva primeiro um teste que descreva o resultado observável.
- Execute o teste antes da implementação. Registre o comando e confirme falha pela razão esperada, não por erro de compilação ou configuração.
- Implemente a menor mudança que torna o teste verde. Execute o mesmo teste e a suíte da área afetada.
- Refatore somente depois do verde e repita as verificações.
- Não altere expectativas para esconder regressão. Se não for possível escrever um teste, registre a limitação e peça decisão antes de tratar o comportamento como validado.
- Mudanças apenas documentais ou em recursos/configuração sem comportamento executável não exigem teste RED; valide links, sintaxe e diff.

## Quality Gate Android

1. Execute os testes do módulo afetado durante o ciclo RED/GREEN.
2. Antes de concluir, execute a suíte JVM completa: `./gradlew testDebugUnitTest` (PowerShell: `gradlew.bat testDebugUnitTest`).
3. Para alterações Android, execute lint e compile/package Debug. Release oficial exige o fluxo de produção separado e nunca substitui esses testes.
4. Para UI, ciclo de vida, navegação, persistência Android ou recursos do sistema, execute instrumentação no perfil necessário. Use os AVDs documentados em `TESTING.md` e `tools/with-emulator.ps1`.
5. TV, tablet e celular são perfis distintos. Escolha os perfis conforme a superfície afetada; não infira validação de um perfil a partir de outro.
6. Revise o diff adversarialmente. `BUILD SUCCESSFUL` sozinho não prova que instrumentação executou; confirme testes executados, falhas, erros e ignorados no relatório.

## Evidências e escopo

- Registre no handoff o teste RED observado, o teste GREEN, os comandos de Quality Gate, os resultados e limitações reais.
- Não transforme quantidade de testes executados em percentual de cobertura. Só informe cobertura quando medida por ferramenta e escopo identificados.
- Testes usam fixtures e credenciais fictícias. Testes contra servidor real exigem ambiente de teste autorizado.
- Mantenha o escopo de implementação no aplicativo Android quando a tarefa estiver limitada ao APK; este arquivo não autoriza mudanças no servidor nem no portal. Obrigações globais de release, homologação e publicação definidas por instruções de nível superior continuam prevalecendo e não são substituídas por esta política.
- Consulte `TESTING.md` para cenários, comandos e perfis de emulador mantidos pelo projeto.
