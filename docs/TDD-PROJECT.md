# TDD do projeto MulletaFlix

Este documento define como aplicar Test-Driven Development (TDD) em todos os produtos e ferramentas mantidos neste repositório. Ele complementa os guias específicos, não substitui seus critérios locais:

- Android: [`MulletaFlix-android/TDD-PLAN.md`](../MulletaFlix-android/TDD-PLAN.md) e [`MulletaFlix-android/TESTING.md`](../MulletaFlix-android/TESTING.md).
- iOS: [`MulletaFlix-iOS/MACOS-VALIDATION.md`](../MulletaFlix-iOS/MACOS-VALIDATION.md).
- Web: scripts em [`MulletaFlix-web-master/package.json`](../MulletaFlix-web-master/package.json), testes unitários em `src/**`, Playwright e Cucumber.
- Servidor e integrações: `MulletaFlix-master/MulletaFlix.sln` e `MulletaFlix-master/Tools/NebulaPython/`.
- Portal e ferramentas de release: `portal-site/`, `tools/` e scripts de build/publicação na raiz.

## Princípio e limites

Toda mudança de comportamento deve começar com um teste que expresse o resultado observável esperado. O teste deve falhar antes da implementação pela regressão correta, passar após a menor correção e continuar passando após refatoração. Testes existentes, cobertura de linhas e build verde não provam, por si só, que o ciclo RED-GREEN foi seguido.

Mudanças exclusivamente documentais, assets gerados e arquivos de configuração sem comportamento executável podem dispensar um teste RED; ainda exigem validação estrutural apropriada. Não invente percentuais de cobertura. Informe comandos, resultados, testes ignorados e limitações reais.

O escopo da mudança determina a suíte obrigatória. Não execute serviços reais, contas, tokens, keystores ou publicação externa em testes. Use fixtures temporárias, fake servers e credenciais fictícias. Testes E2E contra servidor real exigem ambiente de teste autorizado e dados controlados.

## Ciclo obrigatório RED-GREEN-REFACTOR

1. **Defina intenção:** descreva o comportamento, o limite do componente e a regressão que o teste detectará.
2. **RED:** escreva um teste pequeno na camada proprietária e execute-o antes de alterar o código de produção. Confirme falha por assertion/resultado esperado. Falha de compilação, fixture ou infraestrutura não conta.
3. **GREEN:** implemente somente o necessário e rode exatamente o teste novamente.
4. **REFACTOR:** simplifique após o verde, preservando o teste; execute novamente a suíte afetada.
5. **GAUNTLET:** execute os gates da tabela abaixo, mais os requisitos específicos do módulo. Um teste inteiramente ignorado não prova aprovação.
6. **EVIDÊNCIA:** registre comandos e contagens reais de aprovados, falhas, erros e ignorados. Identifique o perfil/dispositivo quando relevante.

Evite testes que afirmam apenas que um mock foi chamado. Prefira testar saída, estado persistido, evento, arquivo, requisição HTTP ou interação que o usuário/consumidor observa. Use mocks apenas quando a dependência não for controlável de outro modo; prefira fakes determinísticos para rede, relógio, armazenamento e sistema externo.

Modelo de evidência por mudança:

```text
INTENT: <comportamento e regressão observável>
RED: <comando> — falha esperada: <assertion/resultado>
GREEN: <comando> — <aprovados/falhas/erros/ignorados>
GAUNTLET: <gates executados e resultados>
DEVICE/ENVIRONMENT: <PHONE/TABLET/TV, SO, browser ou não aplicável>
LIMITAÇÕES: <lacunas reais ou nenhuma>
```

## Propriedade de testes e quality gates

Execute comandos no diretório indicado. Rode gate focal primeiro e suíte integral do produto afetado antes de encerrar. Para mudanças cross-product, execute todos os produtos tocados; não há comando único portátil porque iOS exige macOS/Xcode.

| Frente | Suíte focal/integral | Verificações adicionais e observações |
| --- | --- | --- |
| Android APK | Em `MulletaFlix-android`: `./gradlew.bat testDebugUnitTest --no-daemon --console=plain` | Módulo alterado e app: `./gradlew.bat :<modulo>:lintDebug :app:lintDebug :app:assembleDebug --no-daemon --console=plain`. Para UI/plataforma: `tools/with-emulator.ps1` com perfil explícito `PHONE`, `TABLET` ou `TV`; valide relatório instrumentado não vazio. Guia detalhado em `TESTING.md`. |
| iOS Core/app | Em `MulletaFlix-iOS` no macOS: `swift test --enable-code-coverage` | `bash scripts/run-simulator-tests.sh` para Core + build de simulador. Para integração UI, execute `xcodebuild test` com destination de Simulator apropriado; o script atual não substitui cobertura UI. Windows: `./scripts/validate-structure.ps1` é apenas checagem estrutural, não teste Swift. |
| Web | Em `MulletaFlix-web-master`: `npm test -- --run` | `npm run build:check`, `npm run lint`, `npm run stylelint`, `npm run build:production`, `npm run verify:build`. Mudanças de interação também exigem Playwright apropriado; o E2E completo depende de fixtures/serviços descritos na configuração da suíte. |
| Servidor .NET | Na raiz: `dotnet test MulletaFlix-master/MulletaFlix.sln` | Para APIs/DB/processos/instaladores, execute os projetos de integração relevantes. Use `dotnet test ... --no-restore` somente se assets de restore estiverem atualizados. Não confunda testes unitários com validação de banco/FFmpeg/OS real. |
| Ferramentas Nebula Python | Em `MulletaFlix-master/Tools/NebulaPython`: `python -m pytest` | Instale dependências de teste do `requirements-test.txt` no ambiente virtual isolado. Para regressão focal: `python -m pytest tests/<arquivo>.py`. Cobertura opcional: `python -m pytest --cov --cov-report=term-missing`; só reportar percentuais deste relatório e escopo. |
| Portal | Na raiz: `node --test portal-site/tests/*.test.mjs` | Validar HTML/JS alterado e `portal-site/homologacao-status.json`. Para mudanças de rotas públicas, validar HTTP 200 e conteúdo publicado após deploy autorizado; teste local não prova deploy. |
| Shell/instaladores Linux | Na raiz, Bash/Git Bash/WSL conforme ambiente: `bash tools/release/tests/linux-atomic-write.test.sh`, `bash tools/release/tests/linux-install-preflight.test.sh`, `bash tools/release/tests/duckdns-update.test.sh` | Execute os testes relacionados ao script alterado. Requer ambiente Bash e utilitários POSIX; execução no PowerShell nativo não é equivalente. |
| Scripts Windows PowerShell | Na raiz: `Invoke-Pester` nos arquivos `*.tests.ps1` e `*.test.ps1` em `tools/` | Scripts de release/build exigem fixtures temporárias; nunca executar publicação ou usar segredos em Pester. Verifique `Get-Command Invoke-Pester` e versão antes do gate. |
| Packaging/build | Scripts de validação do pacote afetado e testes das plataformas acima | Validar conteúdo, versão, assinatura/hash e ausência de artefato obsoleto por metadados. Não publicar como parte de testes. |

Os comandos acima são referência de gates por componente. Se um script ou CI declarar comando mais específico, consulte-o e inclua-o; não reduza o gate apenas porque um alvo caro está indisponível. Registre indisponibilidade e deixe validação pendente.

## Matriz de design dos testes

| Tipo de mudança | Teste primário | Complementos |
| --- | --- | --- |
| Regra pura, parser, normalização ou política | Unitário determinístico na camada dona | Limites, entradas inválidas, Unicode, valores vazios e propriedade quando cabível |
| Estado, concorrência, retries ou cancelamento | Teste com relógio/scheduler controlado | Corridas, cancelamento, retry e resposta obsoleta |
| Persistência/migração | Repositório com banco/armazenamento isolado | Inicialização a partir de estado legado, rollback, corrupção e isolamento entre contas |
| HTTP/WebSocket | Servidor local/fake que verifica contrato real | Status, headers, auth fictícia, timeout, cancelamento, retry e payload inválido |
| UI/navegação/acessibilidade | Teste de componente/instrumentado | Teclado, TalkBack/VoiceOver, foco D-pad, escala de fonte, rolagem e estados vazios/erro |
| Integração de plataforma ou mídia | Instrumentação/simulador | Dispositivo físico para hardware, codecs, áudio, DRM ou comportamento de sistema não reproduzido |
| Script/installer/release | Processo isolado com diretório temporário e ferramentas falsas | Caminho com espaços, falha parcial, rollback, reexecução, versões e validação do artefato |
| Portal/deploy | Teste de dados/rotas antes de publicar | Verificação pública pós-deploy; apenas com autorização de publicação |

## Ordem de adoção para legado

1. Não alegue TDD histórico sem evidência RED-GREEN registrada. Marque testes existentes como legado com histórico não auditado.
2. Em toda alteração de uma área legada, escreva primeiro teste de regressão para o comportamento tocado. O restante da área pode continuar não auditado.
3. Priorize falhas de segurança/assinatura, autenticação/sessão, playback/download, persistência/migração e release; depois navegação/UI/acessibilidade, sincronização/integrações e utilitários.
4. Para cada módulo, mantenha uma lista de comportamentos críticos, casos cobertos, ambiente necessário e bloqueios. “Tem testes” não significa “homologado”.
5. Feche uma lacuna somente após evidência executável; cobertura total exige inventário de requisitos e relatório mensurável, não estimativa visual.

## Critério de aceite do projeto

Uma mudança está pronta quando RED foi observado pela razão correta, GREEN foi confirmado, gates pertinentes terminaram com código 0 e evidências são rastreáveis ao diff. Uma frente inteira só pode ser chamada de homologada quando seus requisitos e perfis de ambiente foram inventariados e cada um possui resultado atual. Deploy, release, upload e push são ações separadas e não são autorizadas por este documento.
