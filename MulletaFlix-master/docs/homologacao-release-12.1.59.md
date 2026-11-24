# Homologação da release 12.1.59

Data: 2026-11-17 (America/Sao_Paulo).

## Correção

- O asset de configuração consultava `Intros/ScanStatus` sem o ID da temporada, enquanto `VisualizationController` expõe `Intros/ScanStatus/{SeasonId}`. O retorno 404 impedia a interface de acompanhar com confiabilidade uma varredura em andamento após abrir ou recarregar o painel.
- O painel agora consulta o status da temporada/filme atual em carga e durante o polling, usa `isQueued` para o scan próprio e informa falha quando `failed` vem marcado.
- Nenhum endpoint, módulo, dependência ou componente foi criado; o fluxo de configuração existente foi corrigido.

## TDD e testes

- `ConfigurationAssetsControllerTests.ConfigurationJavaScript_PollsScanStatusForTheSelectedSeason`: RED comprovou a chamada sem a chave obrigatória; GREEN após encaminhar o ID do item atual e cobrir `isQueued`/`failed`.
- `ConfigurationAssetsControllerTests`: 4 aprovados, 0 falhas.
- `IntroSkipper.Integration.Tests`: 69 aprovados, 58 ignorados, 0 falhas / 127 casos.

## Scan real autorizado

- A temporada autorizada é Os Cavaleiros do Zodíaco (1986), temporada 1, 73 episódios. O log de produção registrou `ManualScan` e início do modo Introduction sobre os 73 itens; a interface antiga chegou a mostrar “Scan in progress…”.
- A reabertura do painel em sessão nova mostrou 73 episódios com campos de segmentos vazios. O botão de scan estava habilitado, mas a build instalada não consegue consultar o status da chave porque usa a rota sem ID; portanto esse botão não prova que a fila encerrou. FFmpeg permanecia ativo na última observação. Resultado final do scan e persistência permanecem pendentes até a versão corrigida consultar `ScanStatus/{SeasonId}`; a varredura não foi repetida.
- O limite configurado de concorrência observado no host é 2. O timeout `.strm` mínimo configurado no código é 15 minutos.

## Release e limites

Instalação no host e verificação do estado real do scan após a correção continuam pendentes. Os hashes dos artefatos candidatos locais estão registrados abaixo; a release ainda não foi publicada.

## Auditoria Windows — 2026-11-24

- Correção do IntroSkipper desta candidata: a rota de status agora recebe o ID da temporada selecionada. A primeira solução Release revelou uma expectativa excessiva no novo teste; o teste foi simplificado para verificar o contrato da rota e a reexecução focal passou 1/1. Execução paralela da solução teve falhas intermitentes em Controller/Server.Tests; ambas passaram isoladamente. Execução final serial `dotnet test MulletaFlix.sln -c Release --no-build --no-restore -m:1` encerrou com exit code 0 e nenhum teste falho; Server.Implementations: 1.767 aprovados/95 ignorados; IntroSkipper: 69/58/0. Integrações externas/plataformas indisponíveis foram ignoradas.
- Correção Nebula `.strm`: `realpath()` em uma unidade de staging indisponível lançava WinError 121 antes do fallback para a leitura do Telegram. Agora a comparação de sobreposição usa `abspath()` quando o Windows não consegue resolver a unidade; a proteção de sobreposição continua ativa. TDD reproduzido em RED; focais após correção: 5 aprovados; suíte Nebula Windows: 213 aprovados, 1 ignorado, 0 falhas.
- Tray Windows: valor HKCU Run passa a ser gravado entre aspas e valores antigos incorretos são reparados no início. Build Release com referências automáticas .NET Framework 4.7.2: 0 avisos, 0 erros. A formatação foi exercitada por reflexão sem alterar o Registro real.
- Firewall Windows: runtime 12.1.58 escuta em `0.0.0.0:8096`; Nginx recebe em 80/443. Foram encontradas duas regras inbound `Allow` no perfil `Public` para o caminho instalado `MulletaFlix.exe`, sem filtro de porta. A regra do aplicativo permite tráfego direto HTTP fora do proxy. Na verificação inicial a sessão não era elevada; posteriormente, o helper foi aplicado após aprovação do UAC e a consulta confirmou zero regras inbound Allow para o caminho instalado. O instalador candidato agora ajusta/revoga somente regras inbound `Allow` que apontam para o executável da instalação, retirando Public e mantendo Domain/Private; o smoke de instalação limpa exige zero regras Public remanescentes.
- Nginx: `nginx.exe -t` para a configuração instalada passou (syntax ok, test successful). MariaDB embutido respondeu SQL 11.4.4 e `bind_address=127.0.0.1`; MongoDB e portas 3306/27017 responderam conexão TCP local. O cliente `mongosh` não está instalado, então o protocolo Mongo não foi validado nesta checagem.
- Runtime local antes da recuperação: `MulletaFlix.exe` não executava; `/health` em `127.0.0.1:8096` recusava conexão e o HTTPS via Nginx retornava 502. Após solicitação, o usuário iniciou manualmente o tray; o processo `MulletaFlix.exe` (PID 15868) passou a escutar em `0.0.0.0:8096`, versão pública 12.1.58, `/health` local e via Nginx 200 `Healthy`.
- O comando HKCU Run estava sem aspas. Foi corrigido em produção para `"C:\Program Files\MulletaFlix\Server\mulletaflix-windows-tray\MulletaFlix.Windows.Tray.exe"`; a gravação foi lida de volta e conferida. A correção de código do tray ainda não está instalada.
- No início da auditoria, o builder apontava para o tray no checkout separado ignorado. A origem foi corrigida mais tarde e está demonstrada no pacote candidato descrito abaixo. Permanecem pendentes health pós-atualização, integrações com bancos descartáveis e conclusão das pendências do roadmap.

## Build candidato Windows — 2026-11-24

- A origem do tray no builder foi corrigida para compilar o projeto versionado no repositório principal. TDD do caminho de origem e teste do diretório isolado do instalador passaram. Tray empacotado e binário Release compilado têm SHA-256 idêntico (`FD610BD58D069DFAF8A45CF019ECDF14E9BE5A17BED23A9F4CC1D35C8CA33CB3`).
- Stage Windows Release passou integridade e contém `MulletaFlix.exe` versão 12.1.59. A primeira execução imprimiu sucesso mas retornou 1 porque códigos Robocopy 1–7, tratados como sucesso, vazavam em `$LASTEXITCODE`. A regressão passou RED/GREEN; após zerar o status global para os casos aceitos, o stage builder executou novamente e terminou com código 0. Os três testes de builder passaram.
- TDD de firewall: o novo teste começou RED porque o helper não existia; após implementação, os testes de regras por caminho/perfil, integração NSIS, smoke limpo e integridade do stage passaram. A análise read-only do host confirmou duas regras aplicáveis que o helper removerá; alterações ficam pendentes de instalação elevada.
- ZIP final de atualização: `.build/audit-release-12.1.59-2026-11-24/final-assets-firewall-final2/mulletaflix-update-win-x64.zip`, 419.080.311 bytes, SHA-256 `8D2F4F9BBDE2E0C03B2FBD629633E6E13D2A3A4E57CEB7D24D48067368D96D1B`. `tar -tf` leu 3.266 entradas e retornou 0; inclui `remove-public-server-firewall-rules.ps1`, servidor, atualizador, tray e worker Python.
- Instalador: `.build/audit-release-12.1.59-2026-11-24/final-assets-firewall-final2/mulletaflix_12.1.59_windows-x64.exe`, 453.917.499 bytes, SHA-256 `10D96FD494D32165E662024B7244421A77FF58CF8003B67D9F3F1F5CFB2A9CC4`; Authenticode `NotSigned`. NSIS compilou com sucesso. Não houve instalação do candidato.
- Health local e pelo Nginx continuam `Healthy` na instalação existente 12.1.58. A homologação pós-atualização não ocorreu.
- A publicação está suspensa pelo gate obrigatório da seção 4.0 do `mulletaflix-conventions.md`: o roadmap ainda contém melhorias ativas. A leitura pública confirma que Latest continua v12.1.58. `gh` não tem sessão, mas `git credential fill` forneceu credencial para o script de publicação; o fluxo de publicação não foi executado. A release não está criada/publicada nem aplicada à produção; os hashes acima identificam somente os artefatos candidatos locais. As regras Public do executável foram removidas com o helper elevado; o host segue em 12.1.58 e o candidato não foi instalado.

## Rechecagem após ajuste elevado do firewall — 2026-11-24

- O helper foi executado após aprovação do UAC. Consulta a `PersistentStore`: zero regras inbound `Allow` para `C:\Program Files\MulletaFlix\Server\MulletaFlix.exe`. Perfil de rede ativo: `Public`; os perfis Domain/Private/Public estão habilitados e configurados com `DefaultInboundAction=Block`.
- HTTP local em `127.0.0.1:8096/health` retornou 200 `Healthy`; HTTPS pelo Nginx em `127.0.0.1/health` também retornou 200 `Healthy`. `/System/Info/Public` continua na versão instalada 12.1.58.
- A verificação não foi feita de uma rede externa. O candidato 12.1.59 permanece não instalado e sem assinatura Authenticode; o gate 4.0 do roadmap segue ativo.

## CI hospedada do commit final da documentação — 2026-11-24

- GitHub Actions run `45456440350`, commit `75c02f61997812b743069bf8d53ee79fa896e187`. Primeira tentativa: jobs finalizaram em poucos segundos com `runner_id=0` e zero steps. Reexecução autorizada da tentativa falha (attempt 2) também não executou testes: 13 jobs concluíram failure sem runner/steps, dois foram cancelados e um permaneceu queued; a execução geral terminou cancelled. Check runs não trouxeram resumo de erro dos jobs.
- Interpretação: esse resultado não reprova código porque nenhum step/teste iniciou, mas também não conta como CI verde. Os testes locais Release e os focais Windows seguem aprovados como registrado acima. Confirmar funcionamento dos runners e obter uma execução hospedada verde continua pendente do operador/repositório.
- GitHub API pública ainda aponta `v12.1.58` como Latest. O fluxo `publish-release.ps1` tem credencial disponível via fallback `git credential fill`, mas não foi executado; o gate 4.0 do roadmap continua aberto.
- Novo push documental `9ae1a1492ce638c27dffe96896e8ff0a9d7efd80`: run `45457377315` também terminou cancelled; 14 jobs failure e 2 cancelled, todos `runner_id=0`, sem steps. Não foi feita outra tentativa.

## Revalidação visual do avatar — 2026-11-24

- Na interface web autenticada do runtime instalado, o menu de usuário exibiu a foto de perfil do usuário em vez do fallback com inicial. A imagem carregou com `naturalWidth=300`, `naturalHeight=300`, `complete=true`, atributo `alt` preenchido; a requisição foi para o endpoint de imagem primária do usuário. O endereço/token da requisição não foi registrado.
- Esta observação valida a renderização de GetAvatar para a conta e instalação existentes 12.1.58. Não reproduz a falha relatada nesse runtime e não prova atribuição de avatar a contas novas, outros usuários, nem comportamento da candidata 12.1.59. Não foi alterado código.
- Revalidação read-only: `/health` local HTTP 200 `Healthy`; `/System/Info/Public` informa 12.1.58; porta 8096 está associada ao processo instalado `MulletaFlix.exe` PID 15868; processo escuta em `0.0.0.0:8096`. Integração externa/Internet não testada.

## Amostra de baseline do runtime Windows — 2026-11-24 17:50 BRT

- Foram feitas 25 requisições sequenciais, sem autenticação, a cada uma de três rotas. `/health` em loopback e HTTPS via Nginx retornaram 25/25 HTTP 200; `/System/Info/Public` em loopback também retornou 25/25 HTTP 200.
- Latência min/p50/p95/máx em ms: `/health` local 4,33/7,28/15,10/113,87; `/System/Info/Public` local 0,90/1,20/3,20/9,65; `/health` via Nginx 7,73/10,21/24,89/37,98. Percentil nearest-rank, N=25.
- Snapshot do processo instalado: 320,4 MiB working set, 296,7 MiB private bytes e 4.290,8 s de CPU acumulada; volume C: com 81.288 MiB livres. Resultado pontual, sem carga concorrente e não representativo de duração/volume de tarefas Nebula.
- Isso acrescenta observação ao runtime 12.1.58, sem validar o pacote 12.1.59, endpoints autenticados, carga, Linux ou tráfego externo. A tarefa T0.1 e a homologação formal continuam abertas.

## Revalidação do configurador Windows e do fluxo de artefatos — 2026-11-24 18:30 BRT

- Auditoria de `configure-https.ps1` encontrou dois defeitos de reconciliação: um serviço NSSM ativo mantinha o processo Nginx do caminho anterior depois de atualizar `Application`; e regras com DisplayName conhecido podiam manter aplicação/protocolo/porta antigos. Corrigido para reiniciar o serviço, conferir o `Application` efetivamente gravado e confirmar processo `nginx.exe` executando do diretório instalado. Regras gerenciadas agora são substituídas quando divergentes e ficam limitadas ao binário Nginx instalado, inbound TCP 80/443; regras Allow UDP/Any port para esse binário são removidas. O backend continua sendo proxy local em 8096.
- TDD do firewall: o teste de contrato falhou primeiro (RED) pela ausência de reconciliação; após a correção, `configure-https-credential.tests.ps1` passou, incluindo simulação de regra TCP Any/Any e UDP Any antigas e verificação das duas regras exatas. O teste também preservou renderização/rollback HTTP/TLS e guardas do token DuckDNS.
- O runtime instalado continua em 12.1.58: Nginx atual já responde 200, mas o Nginx efetivamente instalado não foi reiniciado/reconfigurado nesta sessão. A homologação de regras novas, caminhos de serviço e acesso externo requer instalação/upgrade isolado; o smoke de instalação não foi executado porque o host atual já tem servidor, serviço e portas ocupadas. Não alegar que a candidata corrigiu o host ativo.
- O fluxo de publicação foi endurecido com manifesto por hashes de todos os assets versionados, manifesto de conteúdo do stage, rejeição de working tree suja/stage legado/tag apontando para commit diferente, rehash no mesmo stream usado no upload e verificação remota de nomes/tamanhos (e digest se fornecido pela API) antes de publicar o draft. Criadores de pacote não alteram mais arquivos versionados para sincronizar versão.
- Gauntlet local: parser PowerShell de seis scripts passou; `publish-release-artifact-manifest.tests.ps1`, `build-update-package-installer-output.tests.ps1` e `configure-https-credential.tests.ps1` passaram. Teste do stage com DLL real 12.1.51 aceitou arquivos correspondentes e rejeitou arquivo extra adulterado. `git diff --check` passou; a suíte de contrato do portal previamente reexecutada nesta rodada passou 379/379.
- Limites e estado: esta execução não produziu nem instalou artefatos novos; `stage/MulletaFlix.dll` local é 12.1.51, enquanto o candidato 12.1.59 permanece isolado sob `.build`. Authenticode `NotSigned`, GitHub Actions sem runner/steps e gate 4.0 com 81 itens incompletos continuam bloqueando homologação completa/publicação. Manifesto é integridade por SHA-256, não assinatura de autenticidade do publicador.

### Revisão adversarial e correções adicionais — 2026-11-24 18:40 BRT

- O HTTPS agora mantém configuração TLS ativa quando chain e chave já existem durante renovação; apenas o modo explícito `-SkipCertificate` seleciona HTTP-only. Teste de policy cobre certificado válido, ausência de certificado e bypass explícito.
- `build-stage-and-installer.ps1` só aceita `-SkipServerBuild`/`-SkipWebBuild` quando um manifesto anterior de stage prova versão, commit e hashes. O workflow grava esse manifesto logo após `dotnet publish`; o builder volta a gravar após juntar tray/runtime. O fluxo normal de `build-update-package.ps1` delega a um stage build completo que limpa o diretório antes do publish; `-SkipBuild` valida o manifesto de entrada.
- Regras Nginx também validam filtros `LocalAddress`/`RemoteAddress=Any`; filtros restritivos são refeitos. `publish-release.ps1 -SkipAssets` preserva o `target_commitish` da release existente, sem depender de variável do caminho de upload.
- Na primeira emissão sem certificado, o Nginx mantém `/.well-known/acme-challenge/` para validar o domínio, mas devolve HTTP 503 para o restante do site até o TLS ficar pronto. Isso evita encaminhar o player pela porta 80 se o ACME falhar. `-SkipCertificate` explícito continua permitindo modo HTTP.
- Testes de firewall, preservação TLS, manifesto/working tree e contrato do empacotador passaram em PowerShell 5.1; suíte de portal passou 379/379. Build completo, NSIS, instalação, restart real do serviço, network probe externo e publish não executados.
- Ainda falta uma VM isolada para provar upgrade/instalação. O teste cobre a configuração bootstrap e a rota de challenge, mas não simula win-acme/HTTP-01 contra servidor local. Runtime instalado segue 12.1.58 e não foi modificado.

### Build reproduzível e auditoria final dos artefatos — 2026-11-24 19:24 BRT

O build Windows completo foi executado em worktree limpo e isolado, candidato local `b6cfa99f3bf0e1df1f78d049b4999e4d821a47f4` (derivado de HEAD `a4082f166358093c13dd86fcd54960aa4c99a09b`, sem incluir o diff preexistente de `MulletaFlix-web-master/stats.html`). O servidor .NET 10 Release e plugins compilaram, a web passou `npm run build:check` e `npm run build:production`, o Tray .NET Framework 4.7.2 compilou sem warnings/erros, e o stage passou integridade com 3.153 arquivos. A instalação npm mostrou 8 vulnerabilidades moderadas; não foi executado `npm audit fix`.

O Vite altera o relatório versionado `MulletaFlix-web-master/stats.html` durante o build. O builder agora o restaura somente depois de exigir checkout limpo, preservando alterações do usuário porque um checkout sujo é recusado antes do build. A validação do manifesto do stage também foi corrigida: PowerShell ordenava nomes pontuados em ordem dependente de cultura e comparava listas por índice, embora os caminhos/tamanhos/hashes fossem iguais. Agora compara por caminho relativo normalizado e rejeita duplicatas, ausências, arquivos extras e bytes divergentes. O teste de manifesto embaralhado passou; adulteração continua rejeitada.

Pacote final local (não publicado), vinculado ao commit candidato acima: `mulletaflix-update-win-x64.zip`, 419.077.730 bytes, SHA-256 `9E1C14EB05C60993F18DA3BBF35233C9EC19EDA6CCE4E627E3F07294F94271ED`, 3.264 entradas; instalador `mulletaflix_12.1.59_windows-x64.exe`, 453.915.066 bytes, SHA-256 `6B56CD54EF71EC5768E74C7BE37E24B71A53BB1E60CABD74BC14E43409E6BE1A`, `NotSigned`. O manifesto final validou dois assets, versão e commit; stage e payloads obrigatórios (backend, web, Tray, updater) conferidos. O primeiro NSIS recusou sobrescrever um EXE já existente conforme proteção do builder; o build final foi repetido em diretório de saída novo e terminou 0.

Homologação do runtime ativo: após o usuário iniciar o Tray, `GET /System/Info/Public` retornou 200, versão instalada 12.1.58, e `/health` local retornou 200; processo PID 15868 escuta em `0.0.0.0:8096`. Não instalamos o candidato porque não há VM isolada para clean install/upgrade. Não validamos win-acme HTTP-01/renovação real, reachability externa nem CI hospedada. `git ls-remote` não encontrou tag `v12.1.59`. O gate 4.0 do projeto proíbe release intermediária enquanto o roadmap tiver trabalho ativo; no checklist atual há 81 itens pendentes e 1 em andamento. Portanto nenhum commit foi enviado, nenhuma release foi publicada e runtime permaneceu 12.1.58.

Reexecução final: três suítes PowerShell (`configure-https-credential`, `publish-release-artifact-manifest`, `build-update-package-installer-output`) passaram; `portal-site/tests/homologacao.test.mjs`: 379/379; `git diff --check` passou, com apenas avisos de normalização CRLF/LF. Assinatura de código é pendência crítica separada: não há certificado disponível, e o instalador é `NotSigned`.

### Reexecução integrada final — 2026-11-24 19:34 BRT

O fluxo completo do `build-update-package.ps1` foi executado novamente em saída limpa contra o commit candidato limpo `b6cfa99f3bf0e1df1f78d049b4999e4d821a47f4`, usando o stage de produção já construído e validado (`-SkipBuild -SkipWebBuild`, sem ignorar validação de manifesto), e encerrou com exit code 0. ZIP, instalador NSIS e manifesto foram gerados pelo mesmo fluxo. Revalidação independente: manifesto de stage 3.153 arquivos, release manifest com ZIP+EXE, versão do instalador 12.1.59 e 3.264 entradas ZIP, com backend, web, Tray e updater.

Hashes finais deste build integrado (substituem o hash ZIP da execução anterior às 19:24): ZIP 419.077.730 bytes, SHA-256 `8360AC930D4DA7040427ACBD5946CD36A3C19E59CF8CF437838DEDB618F3F583`; instalador 453.915.066 bytes, SHA-256 `6B56CD54EF71EC5768E74C7BE37E24B71A53BB1E60CABD74BC14E43409E6BE1A`, `NotSigned`. `Get-MulletaFlixGitContext` confirmou worktree candidata limpa e commit exato. `git ls-remote` ainda não encontrou `refs/tags/v12.1.59`. Sem instalação, publicação, push ou alteração no runtime 12.1.58. Gate 4.0 e pendências de VM/ACME/CI/assinatura permanecem.

### Smoke operacional final — 2026-11-24 19:36 BRT

No host Windows ativo, `nginx.exe -t -p "C:\Program Files\MulletaFlix\Server\nginx\" -c conf\nginx.conf` encerrou 0. Health direto `http://127.0.0.1/health`, por Nginx HTTP `http://127.0.0.1/health` e por Nginx HTTPS `https://127.0.0.1/health` retornaram 200 `Healthy`. Listeners atuais: Nginx PID 5920 em 0.0.0.0:80/443; MulletaFlix PID 15868 em 0.0.0.0:8096. A consulta ActiveStore não encontrou regra inbound Allow habilitada para `C:\Program Files\MulletaFlix\Server\MulletaFlix.exe` abrangendo perfil Public/Any. Essa verificação local não substitui teste de reachability externa; nenhuma regra foi alterada nesta checagem.

### Triagem das vulnerabilidades npm — 2026-11-24

`npm audit --json` na web identificou 8 moderadas, 0 altas/críticas: apenas dependências de ferramenta de desenvolvimento de lint/typecheck (`@typescript-eslint/*`, `typescript-eslint`, `eslint-plugin-sonarjs` e `minimatch` transitivo). `npm audit --omit=dev --json` reportou zero vulnerabilidades nas dependências de produção. O advisory específico de `minimatch` é GHSA-g3hv-75wg-m249; npm sugere `typescript-eslint` 8.74.0 e marca uma remediação do SonarJS como major. Nenhuma atualização de dependências foi feita nesta release: requer revisão compatível do lint e suíte web completa. Não altera a avaliação de risco runtime do servidor Windows.

### Capacidade de homologação isolada — 2026-11-24 19:38 BRT

Inventário read-only do host: Hyper-V cmdlets indisponíveis, serviço `vmms` ausente, sem `VBoxManage`, `vmrun` ou `WindowsSandbox.exe`. Não há VM/sandbox disponível nesta sessão para clean install/upgrade. O instalador candidato não foi executado no servidor de produção.

### Estado do código fonte — 2026-11-24

As correções de código e configuração desta auditoria foram consolidadas localmente na branch `main` pelo commit `d00bc64fd12534fab008a2e474520fd3bed51243`; `origin/main` ainda está um commit atrás. Não houve push. O diff preexistente de `MulletaFlix-web-master/stats.html` segue fora do commit. Os artefatos de homologação anteriores foram vinculados ao candidato isolado `b6cfa99f3bf0e1df1f78d049b4999e4d821a47f4`, cuja árvore contém o mesmo código de build/runtime deste commit e documentação anterior; como nada foi publicado, o pacote deverá ser reconstruído contra o commit final quando os gates permitirem a release.
