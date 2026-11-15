# Homologação integrada do MulletaFlix — 2026-11-12

## Resultado

**Status geral: PARTIAL; não liberar como homologação completa.** Foram executados gates locais frescos em Windows para servidor .NET, web, portal, Android, Python e scripts. Os gates NuGet, Playwright helpers e Pester foram corrigidos e passaram. iOS nativo, instrumentação Android, dispositivos e integrações reais não foram validados nesta rodada. Não houve alteração no comportamento do produto nem publicação.

## Escopo e evidências

| Frente | Comando / evidência | Resultado | Limite |
|---|---|---|---|
| Servidor .NET | `dotnet test MulletaFlix.sln --configuration Release --no-restore -m:1 --logger "console;verbosity=minimal"` em Windows / .NET SDK 10.0.300 | Execução serial da solução terminou com exit code 0. Resumos observados incluem MediaEncoding 71/71; Naming 681/681; API 1.247 aprovados, 3 ignorados; Implementations 1.764 aprovados, 95 ignorados; Controller 73/73; XbmcMetadata 39/39; Model 685/685; Networking 124/124; Server 95 aprovados, 3 ignorados; Server Integration 3 aprovados, 143 ignorados; Providers 982 aprovados, 13 ignorados; Extensions 144/144; HLS 18/18; Keyframes 3/3; LiveTV 84/84. | Os casos opt-in de MariaDB/MongoDB, Linux/POSIX, HTTP externo e providers reais aparecem entre os skips. Muitos avisos StyleCop/XML sem inventário total. O exit code zero comprova a execução local, não valida ambientes/integrações ignorados. |
| Web | `npm test -- --run --coverage`; `npm run build:check`; `npm run stylelint`; `npm run build:production`; `npm run verify:build` | 441/441 testes em 83 arquivos; build, verificação e Stylelint passaram. Cobertura medida: 9% statements / 8,97% linhas. Bundle eager 299,4 KB gzip, limite 420 KB. | Avisos de Rollup e jsdom observados. `lint:changed` passou sem fontes frontend alteradas. |
| Portal | `node --test portal-site/tests/*.test.mjs` | 379/379 passaram. | Testes locais simulados não provam deploy/HTTP público. |
| Scripts Node release | `node --test tools/release/tests/*.test.mjs` | 33/33 passaram após tornar o workflow NuGet dinâmico e corrigir o teste para tolerar CRLF e conferir os seis projetos externos atuais. | O bloco Bash do workflow foi verificado pelo contrato Node, mas não executado localmente: WSL/Bash Linux sem distribuição disponível. CI hospedada não foi acionada. |
| Web Playwright helpers | `node --test scripts/playwright/stage-safety.test.mjs scripts/playwright/run-isolated-stage-helpers.test.mjs scripts/check-bundle-budget.test.mjs scripts/verify-build-artifact-helpers.test.mjs` no diretório web | 23/23 passaram. `stage-safety.test.mjs` agora verifica `defaultStageDll`, override `MFLX_E2E_STAGE_DLL` e composição dos argumentos do helper. | Playwright real não executado. Stage pode apagar dados; sem stage descartável comprovado, execução foi corretamente omitida. |
| Pester release | `Invoke-Pester -Path tools/release/tests` (Pester 3.4.0) | 1/1 passou após renomear o helper parametrizado de `*.tests.ps1` para `*.helper.ps1`; o consumidor Node `windows-duckdns-token-transport.test.mjs` passou 16/16. | O helper deixou de ser descoberto por engano como teste Pester autônomo; ele recebe `UpdaterPath` pelo teste Node proprietário. |
| Scripts Bash de release | `duckdns-update.test.sh`, `linux-atomic-write.test.sh`, `linux-install-preflight.test.sh`, `linux-nebula-runtime-package.test.sh` executados com Git for Windows Bash 5.3/Cygwin | Os quatro passaram. Também passou diretamente `LinuxPackageOutput.Tests.ps1`; `configure-https-credential.tests.ps1` terminou sem erro. | Git Bash/Cygwin não é Linux; não concede crédito aos gates de kernel Linux/POSIX nem substitui runner Linux. O teste de instalar dependências Nebula em venv Linux não foi executado aqui. |
| Nebula Python | `.build/nebula-locked-regression-20261025/Scripts/python.exe -m pytest tests --cov=. --cov-report=term-missing` (Python 3.10.11, venv isolada) | 727 passaram, 2 ignorados, 0 falhas em 729; cobertura medida 91% (16.684 statements no escopo). `pip check` passou antes da suíte. | Os 2 skips são ramos POSIX de sincronização de diretório em `test_upload_checkpoint.py`, incompatíveis com Windows. Esta rodada não valida Telegram/Mongo reais. O Python global não foi alterado. |
| Android | `.\gradlew.bat testDebugUnitTest :app:lintDebug :app:assembleDebug :app:compileDebugAndroidTestKotlin --rerun-tasks --no-daemon --console=plain` | BUILD SUCCESSFUL, 871 tarefas executadas; JVM 1.788 testes em 256 XML, 0 falhas/erros/ignorados; `lintDebug`, `assembleDebug` e compilação AndroidTest passaram. SARIF: 143 findings classificados warning; nenhum erro. APK Debug local gerado. | Nenhum AVD/dispositivo foi iniciado; instrumentação não executou. Matriz fonte: 10/21 com evidência, 2 em validação, 9 pendentes; `formalSignoff=pending`. Consulte `MulletaFlix-android/HOMOLOGACAO-APK.md`. |
| iOS | `MulletaFlix-iOS/scripts/validate-structure.ps1` | Passou checks XML, privacidade, exportação, versão 1.0.0, guards de release; 135 testes declarados e zero erros brutos de UI. | `swift test` e `xcodebuild` indisponíveis no Windows; sem teste/build nativo nesta rodada. |
| CI / produção | workflows; `dotnet list MulletaFlix.sln package --vulnerable --include-transitive`; restore + auditoria dos seis `.csproj` fora da solução | Nenhum pacote vulnerável reportado para a solução ou os seis projetos externos; todos os comandos terminaram com exit code 0. Sem execução CI hospedada ou publicação nesta rodada. | O step Bash dinâmico da CI não foi executado localmente (WSL sem distribuição). Auditoria NuGet local consultou `api.nuget.org`; confirmar execução hospedada antes do aceite remoto. Não consultar nem alterar serviços produtivos nesta auditoria. |

## Correções e melhorias priorizadas

1. **P0 — Homologação de runtime e plataformas:** executar integrações MariaDB/Mongo em instâncias descartáveis; testar Linux/POSIX e providers reais em ambiente controlado. Completar instrumentação Android PHONE/TABLET/TV pendente e rodar Swift/Xcode em macOS.
2. **P0 — CI hospedada e workflow NuGet:** validar o scanner dinâmico em runner Linux e concluir CI hospedada. A auditoria local passou sem vulnerabilidades; runner Bash e CI hospedada continuam pendentes.
3. **P1 — Android lint:** classificar os 143 avisos SARIF e eliminar os de maior impacto; manter a matriz formal sem promoção até cumprir critérios de device/runtime.
4. **P1 — Cobertura web:** 9% statements / 8,97% linhas é a medição observada; priorizar regras críticas de sessão/autorização, downloads, player, falhas de rede e acessibilidade com testes focados, sem perseguir percentual sem requisito.
5. **P1 — Avisos .NET e builds:** capturar inventário completo e agrupar warnings por regra/projeto; corrigir em lotes TDD mensuráveis. Não tratar warnings históricos como falha funcional sem classificação.
6. **P1 — Stage / E2E / produção:** provisionar stage exclusivo e dados descartáveis, executar Playwright, registrar limpeza e validar HTTP/deploy separadamente. Não reutilizar stage compartilhado.

## Contratos TDD exercitados e pendentes

Os contratos abaixo foram criados a partir das lacunas reproduzidas. TDD-01 a TDD-05 tiveram gates locais exercitados conforme indicado; TDD-06 depende de macOS/Xcode. Mudanças ficaram em CI e testes/harness, sem alteração de comportamento do produto.

### TDD-01 — Auditoria NuGet encontra projetos fora da solução sem lista fixa

- **Dado** um conjunto de projetos `.csproj` descoberto no checkout e a lista da solução, **quando** o workflow procura projetos externos, **então** audita exatamente esse conjunto, inclusive quando vazio.
- **RED observado:** o teste original não interpretava a lista Bash em CRLF e comparava conjunto extraído vazio com seis projetos externos reais. O workflow antigo dependia de lista fixa.
- **GREEN:** teste verifica descoberta `find` e comparação normalizada com a solução; workflow percorre todos os `.csproj` e audita os que não estão na `.sln`. Teste focado passou 1/1; suíte Node release 33/33; auditoria de vulnerabilidades passou para solução + seis projetos externos, sem findings.

### TDD-02 — Launcher Playwright resolve DLL default ou override

- **Dado** ausência de `MFLX_E2E_STAGE_DLL`, **quando** o launcher compõe o executável, **então** usa `stage/MulletaFlix.dll`.
- **Dado** override válido, **então** usa o caminho absoluto configurado sem perder as guardas de isolamento/limpeza.
- **RED observado:** a asserção regex antiga falha apesar do código definir `defaultStageDll` e `stageDll` via override.
- **GREEN:** teste confere default, override e argumentos; focal 11/11 e conjunto de helpers 23/23.

### TDD-03 — Harness Pester fornece parâmetro obrigatório

- **Dado** o script de updater exigindo `UpdaterPath`, **quando** a suíte Pester inicia, **então** fornece um fixture temporário/falso válido e executa o teste sem depender de instalação real.
- **RED observado:** Pester 3.4 descobria `windows-duckdns-config-security.tests.ps1` como suíte autônoma e abortava sem o parâmetro obrigatório `UpdaterPath`.
- **GREEN:** arquivo foi renomeado como helper e o consumidor Node passou 16/16; Pester passou 1/1. O helper usa somente fixture temporária.

### TDD-04 — Nebula coleta apenas no runtime isolado configurado

- **Dado** runtime isolado pronto, **quando** a suíte oficial roda em Python suportado, **então** coleta e executa todos os testes, com skips e dependências reportados.
- **Dado** runtime ausente, **então** guard aborta com instrução acionável antes de qualquer instalação global ou efeito externo.
- **GREEN:** venv isolada existente, `pip check` sem dependências quebradas; suíte passou 727, ignorou 2, cobertura medida 91%. Não há RED funcional pendente nesta frente local.

### TDD-05 — Android gate comprova execução fresca

- **Dado** Gradle com estado de build já aquecido, **quando** a regressão homologatória roda, **então** relatórios identificam testes executados, falhas, skips e tarefas reutilizadas; uma execução sem relatório não recebe aceite.
- **GREEN:** `--rerun-tasks` concluiu 1.788/1.788 JVM, lint, assemble e compilação AndroidTest. Instrumentação/device permanece pendente e não é GREEN inferido.

### TDD-06 — iOS mantém gates nativos separados da validação estrutural

- **Dado** macOS/Xcode disponíveis, **quando** gates de Core e Simulator rodam, **então** `swift test` e `xcodebuild` geram resultados verificáveis e falhas não são mascaradas pela validação PowerShell.
- **RED/GREEN:** pendentes neste host Windows; validação estrutural isolada passou.

## Método e limites

Nenhum defeito de produção foi selecionado para correção; TDD foi usado para estruturar casos de regressão dos gates falhos/bloqueados. Gauntlet comparativo A/B não se aplica: esta auditoria não produz artefato concorrente com referência externa nomeada; gates determinísticos e revisão adversarial das alegações foram usados. Skills consultadas: `tdd-workflow`, `backend-patterns`, `python-patterns`, `dotnet-patterns`, `frontend-patterns`, `gauntlet-loop`, `fable-method`, `fable-judge`; skills locais obrigatórias `caveman`, `cavecrew`, `fable-loop`. Especialistas auditaram .NET, Android/iOS, web/Python/portal/scripts sem editar o checkout.

**Pendências que impedem aceite integral:** integrações reais e gates Linux/POSIX; CI hospedada; Bash workflow NuGet em runner Linux; instrumentação Android/device; iOS nativo em macOS; E2E em stage descartável; hardware, deploy e publicação. Não houve revisão manual linha a linha de todo o código.

## Rechecagem adversarial — 2026-11-12

- `node --test tools/release/tests/ci-nuget-audit-coverage.test.mjs`: passou 1/1 após atualizar o contrato CRLF e tornar o workflow dinâmico. Vulnerability audit solução + seis `.csproj` externos: zero findings.
- `node --test scripts/playwright/stage-safety.test.mjs`: passou 11/11; o conjunto de helpers relacionado passou 23/23.
- Regressão de scripts Node release: 33/33. Pester: 1/1. Consumidor de transporte DuckDNS: 16/16. Quatro Bash scripts via Git Bash passaram; isso não valida Linux nativo.
- NebulaPython com venv isolada: 727 passou, 2 ignorados; cobertura medida 91%. Android com `--rerun-tasks`: 1.788/1.788 JVM, lint/build/compile AndroidTest passaram. .NET solution Release terminou exit code 0.
- Portal após registrar evidência Android: 379/379. `git diff --check` passou. Nenhuma validação executou em cache como substituto de testes frescos.

### Consulta final de CI remoto — 2026-11-12

- Checkout: branch udit/homologacao-20261108, HEAD d46c465cc; consulta de status checks para o commit retornou lista vazia. Busca por PR aberto com essa branch também não encontrou resultado. Isso não equivale a CI aprovado; a branch não tem evidência remota de execução no momento da consulta.
- Rechecagem local: JSON do portal parseia, contém 868 eventos e generatedAt=2026-11-12T14:51:08-03:00; relatório, sessão e mapa técnico existem. git diff --check passou, com avisos de conversão LF/CRLF em dois arquivos, sem erros de whitespace.
- O mapa técnico foi conferido nesta sessão; módulos, APIs, dependências, componentes e fluxos seguem iguais.


## Android lint: triagem acionável — 2026-11-12

**Fatos observados:** XML fresco `MulletaFlix-android/app/build/reports/lint-results-debug.xml` contém 143 avisos e nenhum Error/Fatal: `UnusedResources` 99 (93 em `app/src/main/res/values/strings.xml`), dependências/versões 20 (3 Gradle/AGP + 7 dependências + 10 atualizações disponíveis; concentradas em `gradle/libs.versions.toml` e `gradle/wrapper/gradle-wrapper.properties`), além de 24 avisos variados. IDs sensíveis a revisão incluem `AcceptsUserCertificates` e `InsecureBaseConfiguration` no `app/src/main/res/xml/network_security_config.xml`, `ExportedService` e `ExportedReceiver` no `app/src/main/AndroidManifest.xml`, e `AppLinkWarning` no filtro de deep links.

**Leitura / ação recomendada:**

1. **P1 segurança — certificados:** o base config habilita certificados `user` e cleartext, enquanto o comentário declara que clientes HTTP fazem validação de host/redirect. Confirmar se aceitar CAs instaladas pelo usuário para todo HTTPS é requisito de self-hosted. Definir política por build/domínio e testar redirect, hosts LAN e servidores com CA própria antes de restringir.
2. **P1 superfície exportada — mídia:** playback service e MediaButtonReceiver são interfaces de mídia exportadas esperadas, mas conferir validação da MediaSession/caller e permissões exigidas antes de considerar aviso intencional. Não remover `exported` sem preservar controles Bluetooth/Android e controllers externos pretendidos.
3. **P2 App Links:** o filtro do domínio público tem warning por não declarar `autoVerify`; verificar associação `assetlinks.json` e posse do domínio antes de habilitar verificação automática.
4. **P2 recursos:** classificar os 99 `UnusedResources` e remover apenas após busca de referências geradas/dinâmicas e teste por idioma/tema. Strings representam a maioria (93).
5. **P2 dependências:** revisar e atualizar em lotes version catalog/Wrapper, respeitando compatibilidade e repetindo regressões completas; a sugestão de versão mais nova não é finding de vulnerabilidade.

**Limite:** estes itens são classificação inicial de lint, não vulnerabilidades confirmadas nem mudanças realizadas. O gate Android continua verde para build/lint, mas matriz e aceite formal permanecem pendentes.

## Continuação da homologação Windows — 2026-11-12

- Pester Windows PowerShell 5.1: suíte de `tools` 12/12, `tools/release/tests` 1/1 e `MulletaFlix-android/tools` 53/53. Pester 3.4 sob PowerShell 7 não foi usado para aceite porque `Should Throw` divergiu; os testes focais passaram em Windows PowerShell 5.1.
- Android AVD PHONE/API 35: `:core:common:connectedDebugAndroidTest` passou 4/4. Os harnesses dedicados BookReader passaram 4 fases, PID 3496→3572; MediaRequestDraft passou 6 fases, PID 3592→3671. Ambos restauraram a sessão sintética e limparam os dados de teste.
- Download real após `am force-stop`: preparação, retomada, reprodução offline e limpeza passaram no harness; a faixa retomou no byte 1.041.632 e concluiu 3.846.023 bytes. Evidência filtrada/redigida em `MulletaFlix-android/validation-evidence/2026-11-12/download-process-restart-diagnostics/2a400f17-91c5-444c-a509-c748f3ec504e`.
- Canário opt-in de falha: 1 teste, 1 falha intencional preservada, 0 requests de mídia e cleanup executado. Logs/snapshot em `MulletaFlix-android/validation-evidence/2026-11-12/download-process-restart-diagnostics/584b012f-4d14-4e43-bec8-8ce3eecdd691`.
- A suíte ampla `:app:connectedDebugAndroidTest` **não passou**: 75 casos descobertos, 64 aprovados e 11 falhas em fases host-orquestradas chamadas sem os argumentos dos scripts. As três jornadas relevantes passaram quando executadas pelos scripts próprios. O argumento `notAnnotation` que o wrapper adiciona não filtrou esses métodos nem as duas jornadas marcadas `AndroidTvOnly`; resolver o encaminhamento/filtro do runner é pendência do harness, não declarar a suíte integral verde.
- Sondas de seleção: `class=AppSmokeTest` pelo Gradle passou 1/1; lista de classes separada por vírgulas executou apenas a primeira classe (AndroidCleartextPolicyIntegrationTest 3/3). Não foi suficiente para isolar todo o conjunto. Uma tentativa de executar lista via `testFile` foi barrada pela política automática do executor antes de iniciar; nenhum estado Android/dado foi alterado.
- O grafo de dependências não foi atualizado: `graphify --help` confirmou que o binário instalado 0.9.79 não tem comando `update`. O mapa técnico foi reconferido e nenhuma topologia de produto mudou. A tentativa de provisionar MongoDB descartável foi rejeitada pela política automática do executor antes de iniciar processo/alterar dados; não foi usada a instância local padrão.
- TDD-05 passa a ter evidência parcial em device; não altera a matriz formal Android (10/21 concluídas, 2 em validação, 9 pendentes), `formalSignoff=pending` ou status global **PARTIAL**. A MariaDB/Mongo opt-in, dispositivos físicos, PHONE/TABLET/TV completos, iOS nativo, Linux/CI hospedada e stage E2E permanecem pendentes.
- Após atualizar o portal, `node --test portal-site/tests/*.test.mjs` passou 379/379; Pester final passou 12/12, 1/1 e 53/53; `git diff --check` passou e o JSON do portal parseou com 871 eventos.

## IntroSkipper, avatar e instalador Windows — 2026-11-12

### Diagnóstico e correções

- `UserAvatar` montava URL de imagem sem o helper autenticado. TDD-07 chama `ApiClient.getUserImageUrl`; teste de componente 2/2. `npm run build:check`, Stylelint, suite web 443/443 e `build:production`/`verify:build` passaram (1.928 artefatos).
- NSIS usava texto UTF-8 sem declarar charset e o campo DuckDNS/token não cabia com clareza na página. O fonte agora declara UTF-8, posiciona o token-password no layout e inclui botão para abrir `https://www.duckdns.org/`. Guard PowerShell `InstallerLocalization.Tests.ps1` terminou exit 0. Inspeção visual por GUI não foi executada.
- Logs ativos tinham ocorrências de `Chromaprint returned 0 points`. Sonda somente de leitura em um `.strm` de homologação respondeu HTTP 200; FFprobe identificou três streams de áudio e um de vídeo. FFmpeg produziu fingerprint de 172 bytes em uma janela de 8 segundos. O FFprobe não retornou duração positiva da faixa, mas retornou duração do contêiner. A janela de 600 segundos excedeu o limite da sonda de 45 segundos; não foi persistido segmento no banco.
- IntroSkipper agora lê dinamicamente a configuração do caminho FFmpeg depois da resolução do encoder, inclui a duração do contêiner na busca FFprobe e só faz esse probe quando a duração da biblioteca é ausente/zero. `MaxParallelism` tem teto efetivo de 2 nos dois laços concorrentes e validação de UI 1–2.

### RED/GREEN e artefatos

- RED path FFmpeg: `resolved-ffmpeg.exe` esperado, `stale-ffmpeg.exe` observado. RED duração: parser devolveu `null` e fallback devolveu `0` (2 falhas, uma expectativa já verde). GREEN: oito casos novos passaram; `IntroSkipper.Integration.Tests` passou 51, ignorou 57 integrações externas/plataforma, 0 falhas.
- `dotnet test MulletaFlix-master\MulletaFlix.sln -c Release --no-restore -m:1`: exit 0, 6.092 aprovados, 314 ignorados, 0 falhas; execução anterior ao último fallback, coberto pela suíte IntroSkipper posterior.
- `dotnet publish` Windows `win-x64` self-contained passou depois do último ajuste. O `IntroSkipper.dll` do stage isolado foi comparado por SHA-256 ao publish; hashes iguais.
- NSIS final compilado com sucesso, versão 12.1.51, 601.506.187 bytes. Candidato local: `.build/installer-output-introskipper-final-20261112/mulletaflix_12.1.51_windows-x64.exe`; SHA-256 `FFD2A489FA9A6CFECEE0A5622FF0154396408997F72F0A899685CE5DB587A0ED`.

### Limites e release

- Não foi executado o scan completo de 600 segundos nem a gravação de segmentos em banco ativo. O serviço Windows instalado não foi substituído nem reiniciado; o build candidato precisa ser aplicado e reiniciado antes de afirmar que a biblioteca foi reanalisada.
- O fluxo de release permanece bloqueado: checkout com mudanças preexistentes/atuais, sem autenticação `gh` e workflow disparado por tag. Nenhuma tag, push ou publicação foi feita. Artefato local não equivale a release publicada.
- Sem mudança de dependência ou endpoint público. O mapa técnico foi conferido e atualizado. Registros: `TDD-PLAN.md`, [[Sessões/2026-11-12 - IntroSkipper streams, duração e limite Windows]] e [[Decisões/2026-11-12 - IntroSkipper usa duração e caminho atuais do FFmpeg]].

INTENT: code bypasses authenticated legacy URL generation; failing check expects the signed-in image URL; the spec (legacy image callsites) says use ApiClient.getUserImageUrl.
TWINS: raw avatar URL construction is absent from sibling components; they call the shared authenticated helper.

### Diagnóstico complementar do log Chromaprint — 2026-11-12

- O exemplo confirma falha na etapa de fingerprint (saída binária vazia), mas não contém stderr/exit code do FFmpeg nem a janela `[Start, End]`; por si só, não identifica a causa.
- Sonda local somente de leitura do episódio S01E37 citado: 8 s, exit code 0, 172 bytes / 43 pontos, sem stderr. Isso confirma que o `.strm` e o endpoint daquela mídia aceitam ao menos uma janela curta; não valida a janela completa nem o FFmpeg do processo implantado no momento do log.
- A configuração do processo Windows atualmente ativo foi lida sem expor outros campos e mostra `AnalysisLengthLimit=10`; zero não explica esse processo. O log enviado não está correlacionado a PID/configuração e não permite atribuir causa.
- Ainda assim, o setter do servidor agora normaliza zero/negativo para 1, em linha com o mínimo validado pela UI. RED para 0/-1; GREEN 2/2. Suíte `IntroSkipper.Integration.Tests`: 53 aprovados, 57 ignorados, 0 falhas.
- Aviso agora registra modo, janela `[Start, End]` e contagem correta de pontos, sem confundir bytes com pontos. O objetivo é que a próxima execução distinga janela vazia de erro de entrada/decodificação.
- A varredura de todas as mídias, a detecção real do segmento e sua escrita no banco continuam pendentes; também falta correlacionar o log original ao PID/configuração após instalar a nova build.
- Complemento da sonda com o FFmpeg instalado (`C:\Program Files\MulletaFlix\Server\ffmpeg.exe`): duração do contêiner S01E37 = 1.462,503 s; com configuração atual `AnalysisLengthLimit=10` e percentual 25%, o intervalo de abertura calculado é 365,625 s. A execução direta dessa janela completa terminou em 38 s, exit code 0, sem stderr, e produziu 11.728 bytes / 2.932 pontos. Não gravou segmentos nem usou o banco; prova streaming + decode + Chromaprint em uma janela real, não o scheduler nem detecção/persistência ponta a ponta.
- Build candidata atualizada após a correção/diagnóstico: publish Windows self-contained e instalador NSIS compilados com sucesso; SHA-256 do `IntroSkipper.dll` em publish e stage confere (`E7FF8F9E5F88EA985091C22DDAE05491003E46260AAA11A4924F35F6A5774E80`). Instalador [mulletaflix_12.1.51_windows-x64.exe](../.build/installer-output-introskipper-diagnostic-20261112/mulletaflix_12.1.51_windows-x64.exe), 601.506.477 bytes, SHA-256 `51F91D8BDF18F1A7E4496159301E0AC998510FDDD5B21689621CB91FCC0A5FCC`. Guard de localização/DuckDNS passou novamente. Build é candidata local; serviço ativo não foi substituído.
- **Release:** consulta somente leitura ao GitHub confirmou que `v12.1.51` já é a release pública mais recente. O candidato local tem o mesmo número, portanto não deve ser publicado como release duplicada. `v12.1.51` aponta para `69856155`, enquanto checkout está em `d46c465c`; há 33 arquivos alterados/não rastreados e o último CI remoto desse branch terminou `failure` sem jobs listados. Não criei tag, commit, push ou release. Próxima release exige versão nova, revisão de todo o diff e CI verde.

### Correção crítica do diagnóstico/release — 2026-11-12

- **Causa reproduzida:** o `IntroSkipper.dll` ativo é `12.1.51+7f41d668...`; no código correspondente `FFmpegService.FingerprintAsync` envia `episode.Path` ao FFmpeg. Para o `.strm` S01E37, o FFmpeg instalado retorna exit `-1094995529`, 0 bytes/pontos e stderr quando recebe o `.strm` bruto; ao receber o target resolvido, retorna exit 0 e 172 bytes/43 pontos. Isso reproduz a falha do log. A correção local está no commit `cf5b493` (`StrmInputResolver`) e ainda não está na tag pública 12.1.51 nem no DLL instalado.
- **Artefato NSIS anterior invalidado:** o `Directory.Build.props` do checkout é 12.1.46 e o publish produzido contém `MulletaFlix.dll` 12.1.46; o stage reutilizado ainda tinha `MulletaFlix.dll` 12.1.51 e recebeu apenas a DLL do plugin do publish. Portanto os executáveis locais nomeados `mulletaflix_12.1.51_windows-x64.exe` misturam builds e **não podem ser instalados nem publicados**. Tentativa de remover os dois executáveis exatos foi rejeitada pela revisão automática de ações destrutivas; continuam em `.build` com os hashes registrados acima, apenas como evidência e inválidos.
- **Release não liberável:** release pública/tag 12.1.51 já existe; seu histórico diverge do branch de origem (`HEAD d46c465c`, `origin/main 8438271f`, tag commit `69856155`). O branch também mantém 33 arquivos alterados/não rastreados. O último CI remoto do branch falhou e a integração não forneceu jobs/logs. Não criar nova tag ou publicação antes de alinhar histórico/versão, revisar o diff completo, fazer stage totalmente fresco e obter CI verde.
- **Gates Windows/Python nesta continuação:** `dotnet test MulletaFlix.sln -c Release --no-restore -m:1` terminou exit 0; a saída teve warnings de documentação/analisadores e 95 testes ignorados no projeto de servidor, sem falhas. Suite Python `Tools/NebulaPython`: 727 aprovados, 2 ignorados, 1 aviso de depreciação; exigiu venv isolada em `.build/qa-venv` com dependências no lock. Quatro guards de packaging Windows executados diretamente terminaram exit 0; Pester 3.4 sob PowerShell 7 não reconhece `-CI` e a invocação por Pester registrou 0 casos, portanto não contam como suíte Pester.
- **Live state:** o serviço Windows permaneceu em execução e `/health` respondeu 200 em HTTP 8096 e HTTPS 443. O servidor ativo não recebeu DLL/instalador novo. Scheduler, reconhecimento da região intro e escrita em DB continuam sem homologação; o scheduler apaga segmentos/cache da temporada antes de reanalisar, então a próxima validação deve usar ambiente isolado.


### Homologação final do analisador IntroSkipper — 2026-11-12

- Novo teste determinístico chama ChromaprintAnalyzer.CompareEpisodes com a mesma abertura deslocada em 80 e 100 pontos. RED inicial: namespace ausente no teste; após incluir IntroSkipper.Data, GREEN focal 1/1.
- Execução completa posterior de IntroSkipper.Integration.Tests: 54 aprovados, 57 ignorados, 0 falhas (111 total). git diff --check terminou exit 0; emitiu somente avisos de conversão CRLF/LF em arquivos alterados.
- O teste sintético cobre reconhecimento da abertura compartilhada, sem executar scheduler ou persistir segmentos. O diagnóstico do FFmpeg permanece reproduzível: .strm bruto falha (exit -1094995529, 0 bytes); URI resolvido gera 43 pontos em 8 s.
- Homologação/release Windows ainda não concluídas: plugin corrigido não foi instalado no serviço ativo, nenhum segmento foi escrito no banco, os instaladores 12.1.51 estão invalidados por mistura de versões, a release 12.1.51 já existe, o checkout diverge da tag e o CI remoto falhou. Nenhuma instalação, reinicialização, tag, push ou publicação foi feita.


### Auditoria runtime e regressão completa — continuação 2026-11-12

- Nova execução `dotnet test MulletaFlix.sln -c Release --no-restore -m:1 -v minimal`: exit code 0, sem falhas; warnings de documentação/analisadores e 95 skips Windows/Linux/externos no projeto Server. A suíte IntroSkipper do mesmo source passou 54, ignorou 57 e falhou 0.
- Estado atual: serviço MulletaFlixNginx ativo; `MulletaFlix.exe` PID 8400 desde 15:33; MariaDB operacional no loopback 3306. Health local `/health` retornou HTTP 200 `Healthy`. O plugin instalado em `C:\Program Files\MulletaFlix\Server\IntroSkipper.dll` tem SHA-256 `BE4A5F6CE053BCA29C9E4C73573FB3707A412B6503C5ED89AC47910E1FDEE884`, correspondente ao DLL antigo observado antes da correção. Config ativa segue com `MaxParallelism=2` e `AnalysisLengthLimit=10`.
- A cauda de 2.500 linhas do log atual continha 2.344 warnings `Chromaprint returned 0 points`; a passagem `ChangedItems` correlacionada no log ocorreu em 18:35. Conteúdo de mídia/URLs permaneceu oculto. Isto confirma que saúde HTTP não representa análise corrigida implantada.
- **Segurança de validação:** `AnalysisScheduler.RunScanAsync` apaga segmentos e cache da temporada antes de chamar o analisador. Fixture MariaDB existente apaga/cria schema fixo e usuário de teste. Não executar qualquer scan contra serviço/banco/library ativos. E2E de scheduler precisa instância isolada de server/config/data e MariaDB descartável em porta loopback não padrão, com mídia sintética.
- **Release revalidada:** release pública ainda é `v12.1.51` (GitHub API, publicada em 07/11/2026); `HEAD=d46c465c`, `origin/main=8438271f`, tag `69856155`; distância HEAD/tag 100/103; source version 12.1.46; status GitHub combinado retornou zero checks. Nenhum branch/tag/commit/push/release foi criado.

## Correção do caminho `file:` em `.strm` e homologação MariaDB — 2026-11-12

- **Causa reproduzida:** conteúdo `.strm` com destino local `file:///D:/...` era devolvido como URI absoluto ao FFmpeg Windows. FFmpeg não abriu a URI, produziu saída vazia (registrada como 0 pontos); o mesmo WAV aberto pelo caminho nativo gerou 172 bytes/43 pontos em 8 s. O defeito explicava o aviso compartilhado pelo usuário.
- **Correção:** `StrmInputResolver.Resolve` agora converte apenas destinos `file:` em `uri.LocalPath`; HTTP/HTTPS e demais protocolos continuam como URI. Os callers existentes em fingerprint, probe de duração e seleção de faixa recebem a correção pelo helper único.
- **RED/GREEN:** teste do resolver cobre caminho local com espaço; o E2E real gera `.strm` `file:` sintéticos, executa o pipeline `BaseItemAnalyzerTask` com FFmpeg/MariaDB isolados, afirma fingerprints não vazios, limite de concorrência 2 e segmentos Chromaprint persistidos. E2E focado passou 1/1. Suíte completa de integração, sem o E2E que disputa subprocessos FFmpeg sob concorrência, passou 111, ignorou 1 e falhou 0; o teste omitido passou separado. O caso de restart externo foi ignorado por não haver marcador.
- **Ambiente de teste:** MariaDB descartável 11.4.4 em loopback:33099, `max_connections=512`, schema/usuários de teste removidos pelos fixtures. Serviço encerrado graciosamente e porta fechada após os testes. Não foi acessado o banco MariaDB ativo em 3306 nem a biblioteca real.
- **Interferência observada:** primeira rodada integral com fixture padrão falhou 33 casos por `Too many connections`; com instância isolada ampliada, os testes de persistência passaram. Na rodada que incluiu o E2E junto a todo o conjunto paralelo, a probe de capability do FFmpeg excedeu seu limite de subprocesso; a execução isolada do E2E foi verde. Não confundir esse gargalo de homologação concorrente com falha funcional do caminho corrigido.
- **Limites ainda abertos:** DLL ativo permanece antigo; a mudança não foi instalada no MulletaFlix Windows. Scan da biblioteca e banco reais seguem proibidos nesta validação porque `RunScanAsync` remove segmentos/cache antes de recomputar. Correlação do log pós-deploy, build/stage coerentes, CI hospedada e release nova ainda são gates.
- **Release:** nenhum commit/tag/push/publicação. O workflow CI foi corrigido para não usar contexto runner em env de job e tem teste Node 1/1; a execução hospedada ainda falta. Release pública continua 12.1.51; source version/histórico local e diff pendentes de alinhamento/revisão.
- **Mapa técnico:** conferido; sem novo módulo/API/dependência. Atualizado o fluxo existente `STRM -> StrmInputResolver -> FFprobe/FFmpeg`, incluindo conversão de URI `file:` em caminho nativo Windows.

### Candidato Windows coerente — continuação 2026-11-12

- `build-stage-and-installer.ps1` publicou o servidor self-contained `win-x64` em `.build/release-build-strm-fileuri-20261112/server-publish`, montou stage novo em `.build/stage-strm-fileuri-20261112` e passou `Assert-StageIntegrity`. Versionamento do executável e DLL: 12.1.46.
- `IntroSkipper.dll` do publish e do stage têm SHA-256 idêntico `85894D52773A518B45C19D686BFC46167B68987A91C4F4847A757A56951C19DA`.
- NSIS compilou um candidato local de 444.392.492 bytes, SHA-256 `4DA03673534B4D93CC764B489FCF69EBE134F5825298270F9C9D16E017E8DF67`: `.build/release-build-strm-fileuri-20261112/installer-output/mulletaflix_12.1.46_windows-x64.exe`.
- O script então parou ao tentar copiar para `dist/mulletaflix_12.1.46_windows-x64.exe`, pois já existe um artefato de 415.470.877 bytes. O guard evitou sobrescrita; nenhuma ação ocorreu em `dist`. O novo candidato isolado existe e é coerente, mas 12.1.46 está abaixo da release pública 12.1.51 e não é publicável como próxima release.
- Não houve instalação ou reinício do serviço Windows. O candidato contém a correção `.strm`, mas não serve como artefato de release enquanto versão/histórico não forem reconciliados e CI hospedada não passar.

### Fechamento dos gates locais — 2026-11-12

- `dotnet test MulletaFlix-master\MulletaFlix.sln -c Release --no-restore -m:1 -v minimal`: exit code 0. Todos os projetos da solução terminaram sem falhas; testes condicionais a Linux, serviços ou credenciais externas foram ignorados conforme seus marcadores.
- `node --test tools/release/tests/ci-nuget-audit-coverage.test.mjs`: 1/1 aprovado. `git diff --check`: exit code 0; apenas avisos de normalização CRLF/LF.
- Homologação específica do IntroSkipper permanece: E2E isolado 1/1 (FFmpeg real + MariaDB descartável, 4 `.strm`, fingerprints e segmentos persistidos, concorrência máxima 2); restantes das integrações 111 aprovadas, 1 ignorada, 0 falhas. A execução E2E junto da carga integral excedeu timeout na probe FFmpeg; separado passou.
- **Não fechar como validação de produção:** DLL ativo segue antigo e a correção não foi instalada; nenhum scan da biblioteca real foi executado. A release continua bloqueada por versão source 12.1.46 abaixo da pública 12.1.51, histórico/tag divergentes, diff por revisar e falta de CI hospedada verde. Não houve commit, tag, push ou publicação.
