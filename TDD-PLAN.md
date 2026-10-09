# Plano TDD integrado — MulletaFlix

## Objetivo e limite

Este documento define como aplicar Test-Driven Development nas frentes mantidas neste repositório: servidor Mulletaflix/.NET e Nebula/Python, cliente web, aplicativo Android, cliente iOS, ferramentas de build/release e portal público. É um plano de adoção contínua, não uma declaração de que todo o código legado já foi desenvolvido por TDD nem de que todas as funcionalidades estão homologadas.

O trabalho deve permanecer na frente solicitada. A existência de testes do servidor ou do portal não autoriza alterar essas frentes numa tarefa limitada ao APK. O plano detalhado, matriz de funcionalidades Android, AVDs e registros recentes ficam em [`MulletaFlix-android/TDD-PLAN.md`](MulletaFlix-android/TDD-PLAN.md) e [`MulletaFlix-android/TESTING.md`](MulletaFlix-android/TESTING.md).

## Protocolo obrigatório por mudança executável

1. Escrever o resultado observável e o defeito/regressão que o teste detecta.
2. Criar o teste focal de comportamento na camada proprietária, antes do código de produção. Um ciclo RED-GREEN começa com um teste; o conjunto final deve cobrir todos os casos exigidos pelo contrato.
3. Rodar o teste isolado e guardar evidência de RED: falha de assertion pela razão pretendida. Erro de compilação, fixture ou ambiente não é RED válido.
4. Implementar a menor mudança para torná-lo verde; repetir o mesmo comando.
5. Refatorar só depois do verde e reexecutar o teste focado.
6. Executar a suíte completa da frente, os validadores estáticos/build e integrações/dispositivos exigidos pela superfície alterada.
7. Fazer revisão adversarial Builder vs Evaluator (Gauntlet Loop), reportar evidência, falhas, limitações e testes não executados.

Não escrever produção primeiro e “cobrir depois”. Mudanças puramente documentais, geradas ou de configuração sem comportamento executável não exigem RED; validar sintaxe, links e diff. Testes usam dados fictícios. Testes contra servidor real exigem ambiente autorizado e não podem incluir credenciais em fixtures/logs.

Registro obrigatório no PR/handoff:

```text
INTENT: <comportamento observável e risco coberto>
RED: <comando> — <assertion esperada e motivo>
GREEN: <comando> — <aprovados/falhas/erros/ignorados>
QUALITY GATE: <suíte, lint, build e resultados reais>
DEVICE/E2E: <perfil/ambiente e evidência, ou não aplicável>
LIMITAÇÕES: <lacunas restantes; nunca converter testes em percentual de cobertura>
```

## Mapa das frentes e quality gates

| Frente | Escopo de teste | Gate local/CI identificado | Limite conhecido |
| --- | --- | --- | --- |
| Servidor .NET (`MulletaFlix-master`) | contratos HTTP/autorização, modelo, persistência, rede, providers, transcodificação, tarefas e integração do servidor | Local: `dotnet test MulletaFlix-master\MulletaFlix.sln`. CI: build Release da solução, testes Release filtrados para excluir nomes `Integration`, cobertura Cobertura; além disso, suites Nebula representativas em Windows/Linux e integração Mongo em serviço isolado | o gate local da solução e os cenários de integração selecionados no CI são distintos; inspecionar filtros e resultados ignorados |
| Nebula Python (`MulletaFlix-master/Tools/NebulaPython`) | parsers, deduplicação, identidade canônica, sync/upload/download, retry, cancelamento e persistência | `python -m pytest tests --cov=. --cov-report=term-missing --cov-report=xml:coverage.xml` (executar no diretório do projeto) | coverage só reportar com denominador/escopo; não confundir integração simulada com Telegram/Mongo real |
| Cliente web (`MulletaFlix-web-master`) | TypeScript, componentes, estado, acessibilidade, fluxos Playwright e compatibilidade de build | CI: `npm run build:check`, `npm run lint:changed`, `npm run stylelint`, `npm run build:production`, `npm test -- --run --coverage`, `npm run verify:build`; lint global é informativo por baseline legado. Usar Playwright conforme escopo | E2E de stage deve usar ambiente isolado; não chamar build de produção de teste funcional |
| Android (`MulletaFlix-android`) | módulos Gradle, contratos, ViewModels, persistência, Compose, lifecycle, player e UX PHONE/TABLET/TV | Local: `./gradlew.bat testDebugUnitTest`; módulos tocados + app: `./gradlew.bat :<modulo>:lintDebug :app:lintDebug :app:assembleDebug`. CI também executa `./gradlew test` e `./gradlew assembleDebug`; instrumentação com `tools/with-emulator.ps1` nos perfis afetados | seguir a matriz detalhada em `MulletaFlix-android/TDD-PLAN.md`; sucesso JVM não prova UX em device |
| iOS (`MulletaFlix-iOS`) | regras Core Swift, contratos, estado, persistência e integração com APIs/sistema | macOS: `swift test --enable-code-coverage`; CI compila para iOS Simulator com `xcodebuild ... build`. Testes de UI exigem `xcodebuild test` em destino Simulator separado | requer macOS/Xcode; build de Simulator no CI não equivale a execução de testes UI |
| Scripts de build/release/validação | argumentos, arquivos ausentes/corrompidos, versão, assinatura, cópia/publicação segura e limpeza | PowerShell: `Invoke-Pester` nos testes `tools/**/*.tests.ps1`/`*.Tests.ps1`; Bash: `bash tools/release/tests/linux-atomic-write.test.sh`, `linux-install-preflight.test.sh`, `duckdns-update.test.sh`; Node: `node --test tools/release/tests/windows-duckdns-token-transport.test.mjs` | publicação real é etapa separada; nunca usar chave debug nem alegar publicação por build local |
| Portal estático (`portal-site`) | links, sincronização de release, metadados, rotas, sitemap e conteúdo de homologação | `node --test portal-site/tests/*.test.mjs`; para páginas públicas, `tools/Validate-PortalSitemap.ps1` e validação HTTP/deploy | validação local não prova estado READY nem publicação; não alterar na trilha APK |

Os comandos acima foram identificados em documentação/workflows; este plano não afirma que todos foram executados nesta tarefa. Antes de usar um gate, conferir o diretório e os scripts vigentes da branch.

## Inventário verificado das suítes e lacunas

Snapshot de 2026-10-08. As quantidades abaixo contam arquivos/specs, não casos executados, cobertura ou homologação. O legado não recebe classificação TDD retroativa: só se registra RED-GREEN quando a falha esperada foi observada antes da implementação.

| Frente | Inventário observado | Lacuna operacional a fechar |
| --- | --- | --- |
| Android | 18 módulos Gradle; inventário estático de 239 arquivos JVM em 17 módulos e 143 arquivos instrumentados em 15 módulos. Ver `MulletaFlix-android/TDD-PLAN.md` e `TESTING.md`. | Executar instrumentação somente nos perfis afetados e manter relatórios novos. A existência de testes não valida automaticamente todos os contratos nem os três perfis. |
| iOS | Dois arquivos XCTest em `Tests/MulletaFlixCoreTests`; `swift test --enable-code-coverage` é gate macOS. | Não há UI tests identificados no inventário; build de Simulator não substitui `xcodebuild test`. Este ambiente Windows não executa XCTest/Xcode. |
| Servidor .NET | 17 projetos de teste sob `MulletaFlix-master/tests`; CI principal filtra nomes `Integration` e executa grupos de integração Nebula/Mongo em jobs separados. | Auditar filtros, skips e cobertura entre todos os projetos de integração; não interpretar suíte filtrada como suíte completa. |
| Nebula Python | 37 arquivos sob `MulletaFlix-master/Tools/NebulaPython/tests`; CI roda Windows/Linux com Python 3.10 e cobertura. | Usar `python -m pytest tests ...` no diretório do componente. Integrações externas ficam simuladas em unit tests e isoladas nos jobs de serviço. |
| Web | 74 arquivos unitários/componentes; 20 specs Playwright na pasta padrão; 3 specs em `e2e/` fora do padrão; 4 features Cucumber e 4 arquivos de steps. | CI cobre smoke Playwright selecionado, não todos os specs; declarar owner/gate para `e2e/` e Cucumber. O runner Cucumber pode limpar dados de stage: executar só em ambiente descartável. |
| Portal | Um arquivo `portal-site/tests/homologacao.test.mjs` com 4 casos que usam dados/fetch simulados. | Criar contratos para sincronização de release, links, canonical/sitemap e páginas restantes; teste local não prova publicação ou HTTP 200 remoto. |
| Scripts/release | Suítes Bash, Node e Pester existem em `tools/`; os workflows atuais não executam diretamente todo esse conjunto. | Integrar suites seguras de scripts na CI. Usar temporários/ferramentas falsas; nunca chamar publicação real nem expor segredos durante testes. |

Contagens são fotografia do repositório e mudam com inclusão/remoção de testes. A auditoria não executou as suítes .NET, Python, web, iOS, Bash, Pester ou Cucumber. Android `testDebugUnitTest`, lint e build registrados nesta tarefa referem-se somente ao gate Android indicado no handoff; não provam cobertura integral do projeto.

## Ordem de adoção e backlog

O legado recebe cobertura incremental, sem selo retroativo de TDD. Priorizar mudanças futuras e regressões reais; para cada item, primeiro escrever RED, então corrigir. Não abrir uma alteração gigantesca para “testar tudo” sem critério observável.

### P0 — limites críticos compartilhados

- Autenticação, autorização, isolamento por usuário/servidor e persistência de sessão nos clientes e APIs.
- Identidade e integridade de mídia: metadados, imagens, reprodução, progresso, solicitações/relatos, cache e operações concorrentes.
- Contratos entre servidor, web, Android e iOS: formatos, erros, paginação, idiomas/legendas e compatibilidade regressiva.
- Cancelamento, timeout, retry e resposta obsoleta em rede; sanitização de logs e segredos.
- Empacotamento: versão correta, artefato fresco, hash/assinatura esperada e falha segura antes de publicação.

### P1 — fluxos de produto e dispositivos

- Bibliotecas, busca, ordenação, detalhes, leitura de livros, downloads, TV ao vivo e SyncPlay.
- Player: seleção de faixa, legendas, lifecycle, PiP/Cast e ações de controle remoto.
- UX adaptativa Android PHONE/TABLET/TV e iOS; foco D-pad, toque, rolagem, acessibilidade e retomada.
- Portal: sincronização de versão/links/notas e consistência do sitemap após mudança pública.
- Validar integração com serviços apenas em ambiente de teste isolado; registrar pendências reais se indisponível.

### P2 — qualidade transversal

- Matriz de compatibilidade, acessibilidade, desempenho de catálogos grandes, consumo de memória/rede e falhas intermitentes.
- Aumentar observabilidade de testes: relatórios preservados em CI, contagem de ignorados explícita e reexecução reproduzível.
- Adicionar testes de lógica aos scripts e validadores que hoje só tenham smoke/build; não introduzir testes redundantes para constantes/dados declarativos.

## Regra para definir um bom teste

- Nomear uma única expectativa em linguagem de comportamento; fixtures determinísticas e pequenas.
- Preferir lógica real com fake apenas na fronteira externa. Não afirmar comportamento pelo número de chamadas a mock se não houver efeito observável.
- Testar casos válido, inválido, vazio, erro, cancelamento e concorrência quando fizerem parte do contrato.
- Teste de contrato HTTP confirma método, rota, payload, identidade e tratamento de resposta; não prova que uma implantação remota está ativa.
- UI: teste de unidade/semântica primeiro quando possível, depois integração no perfil de device afetado. PHONE, TABLET e TV são perfis distintos.
- Manter teste de regressão junto à camada dona da regra e evitar duplicar a mesma regra em cada app, salvo para verificar paridade do contrato.

## Critérios de conclusão

Uma mudança só é concluída quando o RED esperado foi observado, GREEN e suíte da frente passaram, builds/lint aplicáveis passaram e não há falhas ocultas como testes ignorados. Para evitar artefato stale, valide diretório de saída limpo/identificado, timestamp de geração, versão e hash/metadados do artefato comparados ao build recém-executado; para APK valide também assinatura esperada. Registre relatório e ambiente. Não inferir homologação total, cobertura percentual ou validação ponta-a-ponta de uma suíte parcial.

Uma release não faz parte automaticamente de cada execução de TDD: seguir autorização, escopo e fluxo de release vigente da frente. APK, servidor e portal continuam releases independentes quando a alteração é exclusiva de um deles.
