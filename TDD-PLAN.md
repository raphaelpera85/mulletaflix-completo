# Plano TDD integrado — MulletaFlix

## Regressão do playback de intro em `.strm` — 2026-11-17

- RED: `UserLibraryControllerTests.GetItem_ReturnsRegisteredTransientIntro` falhou (1 teste, 0 aprovados): a rota de detalhes retornou `NotFound` para a intro recém-registrada, pois só consultava a biblioteca persistida.
- GREEN: o endpoint existente agora recorre ao registro transitório quando o item não existe na biblioteca, sem priorização Nebula nem refresh de metadata para essa intro. `UserLibraryControllerTests`: 27/27.
- E2E Windows: em 12.1.57, a abertura tocou por 24 s; `Main media prebuffer successful` apareceu ainda durante esse trecho e a reprodução avançou para o STRM. O item foi restaurado ao estado não assistido. O teste também revelou HTTP 500 separado ao buscar segmentos.
- 12.1.57 foi aplicado localmente para validar a rota transitória. A correção adicional do filtro de provedores será a release 12.1.58.

## Regressão de consulta de segmentos — 2026-11-17

- RED: `MediaSegmentManagerTests.GetSegmentsAsync_FiltersByProviderId` reproduziu `InvalidOperationException`/`TypeLoadException` para `ReadOnlySpan<string>` em `EFCore.ExpressionTreeFuncletizer`; o endpoint de segmentos retornava 500.
- GREEN: qualificar `Enumerable.Contains(providerIds, e.SegmentProviderId)` evita que o compilador selecione `MemoryExtensions.Contains(ReadOnlySpan<T>)`. O teste focal passou com EF InMemory.
- Release alvo: 12.1.58; suíte completa, pacote e E2E pós-fix pendentes.

## Regressões da release 12.1.55 — 2026-11-16

- `StrmInputResolverTests.FingerprintAsync_ReportsMissingStrmAsFingerprintFailure`: RED reproduzido com `DirectoryNotFoundException` escapando do resolver; GREEN após converter falhas de leitura/validação de STRM em `FingerprintException`.
- `StrmInputResolverTests.DetectBlackFramesAsync_ReportsMissingStrmAsFingerprintFailure`: RED reproduzido no caminho compartilhado de argumentos FFmpeg; GREEN com o mesmo tratamento por item, sem encerrar o passe da biblioteca.
- E2E MariaDB descartável + FFmpeg real: `AnalyzeStrmEpisodes_PersistsSharedIntrosAndCapsConcurrentFingerprints` passou; valida fingerprint, persistência e concorrência máxima de 2.
- Verificação completa, builds, pacote e publicação registrados em `docs/homologacao-release-12.1.55.md` quando concluídos.

## Cobertura Windows do instalador — 2026-11-16

- RED: `node --test tools/release/tests/ci-packaging-coverage.test.mjs` falhou porque `InstallerLocalization.Tests.ps1` não estava na lista do job Windows.
- GREEN: o script foi adicionado à lista `.github/workflows/ci.yml`; teste de contrato focal passou 7/7, suíte Node de release passou 34/34 e execução direta do script imprimiu `Installer localization and DuckDNS controls passed.`
- Limite: o runner hospedado não iniciou devido ao bloqueio de cobrança da conta GitHub; nenhuma validação remota foi creditada.

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

## Regressões da release 12.1.52 — 2026-11-12

- `tests/IntroSkipper.Integration.Tests/StrmInputResolverTests.cs`: cobertura de URI `file:` com espaço no caminho, além de preservação de URI de rede.
- `ChromaprintAnalyzerTests.cs`, `FFmpegAudioDurationTests.cs` e `PluginRuntimeConfigurationTests.cs`: regressões para fingerprint/offset, duração reportada pelo FFprobe e configuração corrente do FFmpeg.
- `IntroSkipperMediaAnalysisE2ETests.cs`: fluxo completo com quatro arquivos `.strm` sintéticos, FFmpeg real, MariaDB descartável, segmentos persistidos e concorrência limitada a duas análises.
- `MulletaFlix-web-master/src/components/UserAvatar.test.tsx`: contrato de URL autenticada e imagem primária, 2/2.
- `MulletaFlix-packaging-master/scripts/tests/InstallerLocalization.Tests.ps1` e `tools/release/tests/windows-duckdns-token-transport.test.mjs`: localização/controles do instalador aprovados e transporte do token DuckDNS 16/16.
- Gates observados: solução .NET 5.713 aprovados/305 ignorados/0 falhas; IntroSkipper com banco descartável 109 aprovados/1 ignorado/0 falhas; web 427/427; build Windows e NSIS passaram. O ciclo RED não foi reexecutado nesta worktree de release, então estes resultados comprovam regressão GREEN, não um RED-GREEN novo nesta base.
- Relatório e limites: [`MulletaFlix-master/docs/homologacao-release-12.1.52.md`](MulletaFlix-master/docs/homologacao-release-12.1.52.md). Instalação/upgrade em VM, CI hospedada e Android/iOS permanecem pendentes.

### Correções da revisão adversarial — 2026-11-12

- `tests/IntroSkipper.Integration.Tests/FFmpegProcessRunnerTests.cs`: 4/4 casos garantem que URLs com userinfo, query e fragmento sejam removidas dos logs Debug, sem alterar argumentos não URI nem caminhos `file:`. Ambos os caminhos de log usam o redator.
- `OnlinePackService` mantém limites de download ZIP em 1 GiB e expansão de imagens em 2 GiB para não rejeitar de imediato o asset oficial Steam.zip de 958.257.135 bytes. O asset não foi baixado/importado, então seu tamanho expandido segue pendente.
- Regressão final atualizada: solução **5.717/305/0/6.022**; IntroSkipper com MariaDB **113 aprovados/1 ignorado/0 falhas**. Stage refeito e íntegro; NSIS recompilado, artefato atualizado registrado no relatório fonte.
- A revisão também identificou que URI `file:` não está confinada às raízes de biblioteca; controlar a origem dos `.strm` até validar/implementar esse limite.

### Revalidação do port para `main` — 2026-11-13

Decisão posterior do usuário: manter a `main` atual como base e portar somente as correções 12.1.52, sem migrar os 103 commits de `v12.1.51`. Na árvore portável, a solução .NET passou **6.103/315/0/6.418** em 17 projetos; IntroSkipper com MariaDB descartável passou **116/1/0/117**, incluindo E2E de quatro STRM, persistência e concorrência 2; Vitest passou **443/443**; build/typecheck web, produção, verify:build, auditoria de produção, localização NSIS, transporte DuckDNS e build do instalador passaram. A auditoria geral mantém oito vulnerabilidades moderadas apenas em ferramentas dev. Instalação em VM e CI hospedada seguem pendentes.

## Regressões da release 12.1.56 — 2026-11-16

- `StrmPrebufferManagerTests` reproduziu RED com origem remota lenta: a reprodução da introdução ficava aguardando o download terminar. GREEN: o prebuffer disponibiliza e descarrega o primeiro bloco ao cliente, continua a transferência em segundo plano e retoma a origem por Range; o teste verifica o início antes do fim da origem. `NativeIntroProviderTests` garante que a sessão de preparação exista antes de a introdução ser retornada.
- Redação dos logs FFmpeg coberta por testes focais para credenciais, query strings, JSON e cabeçalhos sensíveis. Pesquisa de usos equivalentes aplicada aos pontos de logging revisados.
- `CleanName` expandido para 512 com migração reversível; teste confirma alteração e preservação no `Up`. SQL EF gerado e conferido; DDL equivalente exercitado em MariaDB descartável.
- Quality gates locais: solução .NET **6.116 aprovados, 315 ignorados, 0 falhas / 6.431**; Python **727 aprovados, 2 ignorados**; Web **444 aprovados em 84 arquivos** e `npm run build:check`; pacote Windows, integridade do stage, controles/localização NSIS e transporte DuckDNS passaram. Linux x64 cross-publish concluído e arquivo/script inspecionados.
- Limites iniciais desta rodada: reprodução real de `.strm` com intro pelo player ainda não foi exercitada; Linux não foi executado em host Linux; homologação UI/upgrade em VM e jobs CI hospedados estão pendentes.
- Publicação final: tag `v12.1.56` e commit `e76a2a7cba21f006404db21000710955f150e409` enviados à `main`; release Latest publicada com assets Windows installer/update e Linux. API pública confirma tamanhos e hashes locais. O run hospedado `44003569145` falhou antes de executar steps; nenhum gate remoto foi creditado.
- Windows produção atualizado de 12.1.55 para 12.1.56. `/health` respondeu `Healthy` e `/System/Info/Public` retornou 12.1.56; pasta de dados preservada. Não havia serviço `MulletaFlixServer`; há entrada `HKCU Run` da bandeja e o código inicia o servidor como executável quando o serviço está ausente. Disponibilidade antes de login interativo não foi verificada.

### E2E Windows adicional — 2026-11-16

- FFprobe leu o arquivo de introdução instalado (H.264 1280×720) e um `.strm` real após a atualização (HTTP 200, Matroska, 1420,074 s, exit 0); Range de 4 KiB respondeu 206. Nenhum endereço ou token foi registrado.
- MariaDB 11.4.4 descartável em loopback porta 33189: migração `CleanName` para varchar(512) preservou valor anterior de 250 caracteres e aceitou valor de 400. Processo encerrado sem tocar a MariaDB de produção em 3306; datadir temporário ficou em `.build` porque remoção recursiva foi bloqueada pelo ambiente.
- Trilha de código confirmada: o player envia todo item `Video` ao endpoint de intros; o provider nativo é descoberto pelo `ApplicationHost.GetExports<IIntroProvider>()`; com intro, o player inicia o prebuffer principal em paralelo. O teste em player real ainda requer sessão autenticada.
- O host usa a bandeja em `HKCU Run` para iniciar o servidor quando não existe serviço; não foi demonstrado boot antes de login. Reprodução real pelo player com sessão autenticada continua pendente.

## Regressões da release 12.1.58 — 2026-11-17

- `UserLibraryControllerTests.GetItem_ReturnsRegisteredTransientIntro`: RED/ GREEN para obter o DTO de intro transitória que não existe na biblioteca persistente.
- `MediaSegmentManagerTests.GetSegmentsAsync_FiltersByProviderId`: RED reproduziu erro de tradução/avaliação EF ao selecionar `MemoryExtensions.Contains(ReadOnlySpan<T>)`; GREEN com `Enumerable.Contains`.
- Suíte API: 1.248 aprovados, 3 ignorados, 0 falhas / 1.251. Solução .NET Release: 6.118 aprovados, 315 ignorados, 0 falhas / 6.433 em 17 projetos.
- Web: 5 testes focais passaram. Windows stage/NSIS, integridade do stage, localização e controles DuckDNS/token passaram; validação do player real confirmou intro, pré-cache e transição para `.strm` na 12.1.58. Um `bufferStalledError` HLS não fatal ocorreu, e a reprodução continuou.
- Relatório: `MulletaFlix-master/docs/homologacao-release-12.1.58.md`. Release pública Latest `v12.1.58` publicada no commit `d40f76a9255b8529de17ed3f26443cc6127a909b`; API remota confirmou três assets e hashes. CI hospedada run `44018912372` falhou antes de steps (causa não confirmada).

## Regressão do status de scan — 2026-11-17

- `ConfigurationAssetsControllerTests.ConfigurationJavaScript_PollsScanStatusForTheSelectedSeason`: RED confirmou que o painel chamava `Intros/ScanStatus` sem a chave exigida pelo controlador (`ScanStatus/{SeasonId}`).
- GREEN: o asset consulta a temporada/movie atual nos dois pontos (poll e carga), observa `isQueued` em vez do estado global de qualquer análise e sinaliza o campo `failed` ao concluir.
- Suíte `IntroSkipper.Integration.Tests`: 69 aprovados, 58 ignorados, 0 falhas. Relatório de homologação 12.1.59 documentará a validação runtime e os limites do scan real autorizado.
