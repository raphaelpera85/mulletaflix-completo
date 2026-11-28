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

### Pacote reconstruído contra a main local — 2026-11-24 19:49 BRT

Após consolidar a main local, recriei o manifesto de stage e executei o fluxo integrado `build-update-package.ps1 -SkipBuild -SkipWebBuild` em worktree limpa exatamente no commit `553ab19b76ace05e899328ae709e3edf97879c6d`. A stage compilada foi preservada porque o diff entre o candidato anterior e este commit era exclusivamente documental/portal (nenhum arquivo de código/build mudou). O fluxo encerrou 0 e gerou ZIP, instalador NSIS e release manifest. Validação independente: worktree limpa; stage 3.153 arquivos; release manifest 2 assets; ZIP 3.264 entradas e payloads backend/web/Tray/updater; versão EXE 12.1.59.

Hashes atuais (substituem os do candidato anterior): ZIP 419.077.730 bytes, SHA-256 `7D5888EC6BB416C396348CE7397523CBA5422420BE3D6D3D2573457B89C2DD81`; EXE 453.915.066 bytes, SHA-256 `6B56CD54EF71EC5768E74C7BE37E24B71A53BB1E60CABD74BC14E43409E6BE1A`, `NotSigned`. Artefatos locais em `.build/homologation-windows-2026-11-24/.build/homologation-release-output-main/` e `.../homologation-installer-main/`.

Sem push, tag, release ou instalação. `main` local segue dois commits à frente de `origin/main`; `stats.html` permanece como diff preexistente não commitado. O artefato é candidato limpo do commit 553ab19, mas publicação segue vetada pelo gate 4.0 (81 pendências e 1 em andamento), Authenticode ausente e homologação de instalação/upgrade em VM pendente.

### Inspeção dos logs do servidor Windows ativo — 2026-11-24 19:55 BRT

Processo MulletaFlix PID 15868 iniciou às 13:40 e segue saudável (`/health` 200); Nginx PID 5920 escuta 80/443. MariaDB 3306 e MongoDB 27017 escutam apenas em `127.0.0.1`. No log atual `log_20261124.log` (543 linhas, último write 19:49), contagem: 1 ERR, 91 WRN, 438 INF.

Evidências funcionais: `GetAvatar.AvatarValidationService` encerrou às 13:40:43 com “All avatars are valid”, 0 reparos e sem erro de validação; isso prova o estado dos arquivos/mapeamentos no boot, não renderização do avatar em toda sessão cliente. O único ERR foi `IOException` de `ImageSaver` porque outro processo mantinha um arquivo de imagem aberto (15:04); falha localizada, sem outra falha de escrita correlata no log.

Pontos de melhoria/rechecagem: 64 avisos `NotificationsLibraryNotifier` informam que capa/NFO não estabilizaram no prazo de 180 s e que a notificação foi descartada. O código exige capa+NFO locais; os logs não informam tipo de mídia de forma suficiente para concluir se são `.strm`, falta de metadados ou origem lenta. Correlacionar por tipo de item, sem expor títulos/caminhos, antes de alterar a política. Quatro avisos Kestrel disseram que o heartbeat ultrapassou o limite e citaram possível starvation, às 19:17; `/health` depois permaneceu 200. Um aviso EF Core durante startup indica `Skip/Take` sem `OrderBy`, sem indicar a consulta chamadora. O aviso é risco de paginação não determinística, mas exige atribuir a query antes de mudar código.

A senha root ausente no MariaDB é documentada pelo próprio bootstrap como aceitável apenas em localhost; listener 3306 está comprovadamente em 127.0.0.1. Nenhuma conexão/credencial foi exibida, nenhum dado de mídia foi registrado.

### Correção do notifier para mídias sem sidecars — 2026-11-24

A rotina de notificação descartava itens após 180 s quando faltavam capa ou `.nfo`, embora houvesse fallback para enviar texto sem imagem. Corrigido: dois snapshots consecutivos iguais e nome de mídia não vazio bastam para prontidão; capa e NFO locais são opcionais, e a notificação sai somente com texto quando não há imagem. Testes adicionados para prontidão sem sidecars e supressão sem texto. Suíte focal `NebulaTelegramNotifierTests`: 15/15 passou em Windows/.NET 10 Debug. O teste valida a política, não um envio real `.strm` de ponta a ponta. Runtime instalado permanece 12.1.58; o candidato deve ser reconstruído e instalado em ambiente isolado para homologação operacional.

### Build candidato Windows com correção do notifier — 2026-11-24

Build integrado `build-update-package.ps1` concluído em worktree limpa a partir do commit de código `0982af6a0e6f4e49e1e5bdac75c05c718f04c4d6`. Servidor/plugins .NET 10 Release, web (`build:check` + `build:production`) e Tray net472 compilaram; NSIS gerou instalador 12.1.59. Stage: 3.153 arquivos. Manifesto de stage e manifesto release passaram validação por hash e commit; o ZIP contém 3.264 entradas e o SHA-256 do `MulletaFlix.Server.Implementations.dll` no ZIP coincide com o DLL do stage (`FA227E920447A3962B3964445C0C28D5756CE23743E9EC59EDE4C94956AA39F4`).

Artefatos candidatos locais: ZIP 419.077.716 bytes, SHA-256 `31709989CAD705E0B8B884DAB170D8F3FF4746D71D9856265E812235ECB22F2A`; instalador 453.915.177 bytes, SHA-256 `F4612DECC44F8BC5248CF4071A2832480075D8E6C7E575F096648C76B1DA0021`, versão 12.1.59.0 e `Authenticode=NotSigned`. Artefatos em `.build/homologation-windows-2026-11-24/.build/homologation-release-output-0982b/` e `.../homologation-installer-0982b/`.

Smoke final do runtime instalado 12.1.58: API direta `/health`, Nginx HTTP `/health` e Nginx HTTPS `/health` responderam 200 `Healthy`. Nenhum candidato foi instalado. Release preflight local validou 2 assets, mas a publicação permanece bloqueada: a tag local/remota `v12.1.59` não existe, o roadmap 4.0 proíbe release intermediária com 81 pendências e 1 item em andamento, o instalador está sem assinatura de código e não há VM para validar instalação/upgrade. `main` local está três commits à frente de `origin/main`; não houve push, tag nem release. Este build cobre TDD e build de pacote, não envio real de notificação `.strm` ponta a ponta.

### Regressão final do fallback `.strm` — 2026-11-24

Teste de serviço `MetadataWorker_SendsTextForStrmWithoutLocalCoverOrNfo` percorreu o agendamento, estabilização em dois snapshots, fila e chamada de `SendNotificationAsync` com canal simulado. O item usa caminho `.strm` sem arquivo/capa/NFO; foi verificado texto contendo o nome, `imagePath=null` e exatamente uma chamada de entrega. Não acessa Telegram/Internet. Suíte focal Release `NebulaTelegramNotifierTests`: 16/16. Regressão completa Release `Jellyfin.Server.Implementations.Tests`: 1.770 aprovados, 95 ignorados, 0 falhas / 1.865 casos. O build de teste acusou 382 warnings de analyzers/documentação preexistentes, sem erro. A integração local fecha a validação de fila/texto; resta teste com `.strm` remoto real e entrega no Telegram após instalação candidata. Código de produção não mudou após a candidata; somente o teste e documentação foram adicionados, portanto os bins permanecem iguais.

## Revalidação complementar do runtime Windows — 2026-11-24 21:46 -03:00

- Revalidei `GET /health` e `/System/Info/Public` diretamente em `127.0.0.1:8096`, via Nginx HTTP e via Nginx HTTPS. Os três caminhos retornaram `200 Healthy`; os três informam a versão instalada `12.1.58`.
- A candidata `12.1.59` segue apenas local. O teste da correção de notificação `.strm` sem capa/NFO local e a suíte `Jellyfin.Server.Implementations.Tests` Release já registrados acima cobrem o comportamento de texto com transporte simulado; não equivalem a teste remoto/Telegram real. Runtime instalado não contém a candidata.
- O log atual tem aviso EF Core `RowLimitingOperationWithoutOrderByWarning` no arranque. Revisão cruzada descartou `GetUserRootFolder -> RetrieveItem(...).FirstOrDefault(e => e.Id == id)` como causa compatível com EF Core 9.0.0: esse operador com predicado não emite o evento `Skip/Take` sem ordenação. Também há um caso independente em `PeopleRepository.GetPeopleNames` que pode aplicar `Skip` sem ordenação quando `StartIndex > 0` e `Limit <= 0`; não corresponde ao filtro vazio padrão de `PeopleValidator`. A origem do aviso de arranque permanece não identificada; nenhuma consulta foi alterada por hipótese.
- Logs de runtime mostram avisos de heartbeat do Kestrel e `IOException` ao salvar imagens. A correlação temporal dos heartbeats com análise de mídia não prova causa; o processo que mantinha os arquivos bloqueados também não foi identificado. Para decidir correções é necessária captura de EventPipe/ETW/counters e reprodução controlada do lock.
- Estado funcional observado: processo do servidor e Nginx ativos e endpoints locais/proxy saudáveis. Não foi feito teste externo, instalação/upgrade limpo em VM, nova varredura destrutiva, integração real de notificação ou atualização do runtime.
- Gate de release continua impeditivo: regra 4.0 exige concluir o roadmap ativo; a candidata não está assinada Authenticode, a tag/release `v12.1.59` não existe, os runners hospedados não iniciaram steps e este host não dispõe de VM isolada para instalação limpa/upgrade. Nenhum push, tag, publicação ou instalação da candidata foi feito.
- Sem mudança de código, API, dependência ou estrutura nesta revalidação. Evidências e limitações atualizadas nesta seção e na sessão Obsidian de 2026-11-24.

### Acesso HTTPS público — 2026-11-24 21:48 -03:00

Resolução DNS A do hostname público configurado respondeu; curl.exe GET https://<hostname>/health retornou HTTP 200, TLS_VERIFY=0, conexão ao endereço público e ~0,5s. Isso comprova reachability HTTPS externa a partir do próprio host e certificado válido para o cliente curl. Não valida acesso por rede independente, autenticação da aplicação nem desempenho de mídia.

## Auditoria do fluxo de eventos do IntroSkipper — 2026-11-24

- Evidência no Windows instalado 12.1.58: as opções de biblioteca mantêm Intro Skipper desativado em Filmes e Livros e habilitado em Animações e Séries. Não alterei preferências do servidor.
- Os logs de `ChangedItems` mostram lotes sem temporadas resolvidas e nenhum início de modo FFmpeg nesses passes. Isso confirma que a fila recebia eventos que não geravam análise; a configuração observada é compatível com IDs oriundos de bibliotecas sem o provedor ativo, mas não prova a origem exata de todos os IDs históricos.
- TDD novo `EntrypointLibraryOptionsTests.ItemUpdated_OnlyQueuesItemsFromLibrariesWithIntroSkipperEnabled`: RED reproduzido (esperado 0 itens enfileirados para biblioteca desativada, observado 1). O primeiro reteste usou DLL antiga por `BuildProjectReferences=false` e não valeu como homologação. Após build Release do plugin e rebuild normal do projeto de testes, GREEN 1/1.
- Correção em `Entrypoint.OnItemChanged`: consulta as opções da biblioteca e ignora evento de item quando Intro Skipper consta em `DisabledMediaSegmentProviders`. `OnItemRemoved` permanece ativo para remover cache mesmo com provedor desativado.
- Homologação completa da suíte `IntroSkipper.Integration.Tests`: 70 aprovados, 58 ignorados, 0 falhas / 128 casos. Casos ignorados dependem de MariaDB descartável ou mídia remota configurada; não contam como validação dessas integrações.
- Nenhuma API, dependência ou módulo foi adicionado. A correção altera o filtro no fluxo já existente de evento → fila → resolução de temporada.
- Limites: plugin instalado segue 12.0.4.0 no servidor 12.1.58; comportamento corrigido ainda não foi instalado no runtime. Não executei novo scan em mídia de produção nem confirmei análise FFmpeg/persistência em `.strm` real nesta rodada. O relatório anterior do scan da temporada continua pendente de resultado final.
- Gate de release permanece fechado: o artefato local 12.1.59 não contém esta correção, runtime não atualizado, Authenticode sem assinatura e gates globais/CI/VM da documentação acima ainda pendentes. Será necessário reconstruir e repetir o preflight da próxima candidata antes de publicação.

## Diagnóstico dos exemplos STRM enviados — 2026-11-24

- Para dois `.strm` dos exemplos de log em Animações, executei `ffprobe` diretamente contra o destino resolvido, sem persistir dados nem iniciar scan no servidor. Ambos terminaram com exit 1 e HTTP 404; JSON não continha streams de áudio ou vídeo. O endereço/URL e quaisquer parâmetros não foram impressos nem registrados.
- Isso confirma que, nesses dois arquivos, `Chromaprint returned 0 points` ocorre porque o recurso remoto indicado pelo `.strm` não é encontrado. A correção de filtro por biblioteca não é a causa nem resolve esses 404; ela apenas impede enfileiramento de eventos vindos de bibliotecas onde o plugin está desativado.
- Não alterei os `.strm` nem escolhi outro destino porque a URL correta depende da fonte de mídia e não pode ser inferida com segurança. Ação operacional pendente: revisar/atualizar os destinos remotos desses arquivos no sistema que os fornece.
- A suíte E2E com mídia real remota e MariaDB descartável permaneceu ignorada na execução local; portanto não há prova de análise completa e persistência pós-correção.
- Melhoria mapeada: o runner captura stderr do FFmpeg, mas atualmente descarta seu conteúdo quando stdout contém a saída binária de Chromaprint; registra apenas zero pontos/código de saída em debug. Avaliar log de diagnóstico HTTP redigido, removendo URLs, credenciais, query strings e caminhos sensíveis.

## Revalidação operacional e firewall Windows — 2026-11-24

- API local `127.0.0.1:8096/health`, proxy Nginx HTTP e proxy Nginx HTTPS retornaram 200 `Healthy` antes e após a reconciliação. `nginx -t` da configuração usada pelo serviço passou (exit 0); serviço permaneceu `Running`, sem restart.
- Perfil de rede ativo: `Public`. O Nginx ativo estava configurado no NSSM para um executável fora da pasta instalada. Havia quatro regras `Public` amplas para os dois caminhos Nginx (TCP/UDP, porta Any). Após elevação UAC, foram mantidas apenas duas regras de entrada `Any` para o executável atualmente associado ao serviço: TCP 80 e 443; a cópia inativa na pasta instalada ficou sem permissões amplas. As regras de desenvolvimento em `D:` foram preservadas. ActiveStore confirmou apenas as duas regras web para os binários Nginx e zero regras UDP/porta Any nesses caminhos.
- MariaDB (`mysqld.exe`) foi confirmado ouvindo somente em `127.0.0.1:3306`; conexão TCP local passou. Duas regras `Public` Allow amplas TCP/UDP (Any) para o executável exato foram removidas com elevação. Depois da remoção, porta local continuou aberta, bind permaneceu loopback e as três rotas de health continuaram 200. Nenhuma consulta autenticada ao banco foi feita.
- O processo do servidor continua filho do Tray/Explorer e não foi encontrado como serviço Windows; inicialização autônoma após reboot segue sem homologação. Não houve reinício ou instalação candidata nesta checagem.
- Limites desta rodada: a checagem de hoje foi local; reachability pública já tinha evidência separada acima, mas outra rede não foi testada. As regras amplas das cópias de servidor no workspace `D:` foram deixadas intocadas por serem de desenvolvimento. O serviço ainda usa o Nginx fora da pasta instalada; a próxima execução do configurador/release precisa confirmar a migração ao binário empacotado e a manutenção das regras TCP 80/443.

## Auditoria final do servidor Windows e gate de release — 2026-11-24

- Após o usuário iniciar o Tray, `MulletaFlix.exe` permaneceu em execução. A API direta `127.0.0.1:8096/health`, o proxy HTTP (redirect seguido) e HTTPS no Nginx retornaram 200. `nginx -t`, executado no diretório real do serviço (`C:\Users\Raphael\nginx`), passou. O serviço `MulletaFlixNginx` está `Running`/`Auto` e aponta para o binário externo `C:\Users\Raphael\nginx\\nginx.exe`.
- Reconsulta ActiveStore: para o caminho Nginx ativo permanecem só duas regras inbound `Allow`, `Profile Any`, TCP 80 e 443. `mysqld.exe` não tem regra inbound `Allow`; a porta 3306 permanece ligada somente em `127.0.0.1`. Perfil atual `Public`. Regras de cópias de desenvolvimento em `D:` não foram alteradas.
- win-acme `--list` identifica uma renovação configurada. Inventário não elevado encontrou zero ações visíveis de tarefa/serviço apontando para win-acme, mas seis erros de acesso do Agendador impedem concluir que a tarefa automática está ausente. Renovação, certificado e tarefas não foram modificados; requer inspeção read-only elevada.
- Checagem final de `IntroSkipper.Integration.Tests` Release no checkout local: 80 aprovados, 58 ignorados, 0 falhas (138 casos). Os ignorados dependem de integração externa/MariaDB/mídia remota e não contam como homologação dessas integrações.
- Os dois destinos STRM do exemplo continuam com HTTP 404 e zero streams. O diagnóstico FFmpeg agora captura stderr limitado e registra status sanitizado; testes cobrem ocultação de caminhos, URI e texto Bearer. A correção não disponibiliza os arquivos remotos. A instalação de produção ainda não recebeu a mudança.
- Esta foi uma homologação operacional/read-only, sem reboot, reachability de outra rede, renovação ACME, novo scan de produção, instalação candidata ou consulta autenticada ao banco.
- **Release bloqueada:** a regra 4.0 impede release intermediária enquanto o roadmap tiver itens ativos (81 pendentes e 1 em andamento). O worktree contém código não empacotado e o candidato local anterior não contém as últimas mudanças. CI hospedada, certificado Authenticode com chave privada e VM limpa de instalação/upgrade também não têm evidência disponível. Não criar tag, enviar à `main` nem publicar assets nesta etapa.


## Reteste final após ampliação da redação de erros FFmpeg — 2026-11-24

- A revisão independente identificou formatos de mídia não cobertos em nomes isolados. TDD acrescentou `.wmv`, `.mpg`, `.mpeg`, `.m2ts`, `.vob` e mensagem de falha de abertura sem extensão contendo texto Bearer; os seis casos falharam antes da correção. Linhas explícitas de abertura de input são agora redigidas como unidade, mantendo linha informativa sem caminho.
- `IntroSkipper.Integration.Tests` Release executado novamente após a alteração: **87 aprovados, 58 ignorados, 0 falhas (145 total)**. Os casos ignorados continuam dependendo de MariaDB descartável e/ou mídia remota. `git diff --check` passou.
- `portal-site/homologacao-status.json` registra esta validação como parcial com `progressPercent: null`; evidência local automatizada não é sign-off formal.


## Reteste final após cobrir variantes de abertura FFmpeg — 2026-11-24

Dois testes TDD adicionais cobrem `Failed to open input file <nome sem extensão>` e `Unable to open input file <nome sem extensão>`; ambos falharam antes do ajuste e passaram depois. Verificação focal total: **31/31**. Reexecução independente da suíte Release `IntroSkipper.Integration.Tests`: **89 aprovados, 58 ignorados, 0 falhas (147 casos)**. `git diff --check` sem erros. Os casos ignorados continuam limitando homologação de integrações MariaDB/mídia remota.


## Verificação final de redação global de credenciais FFmpeg — 2026-11-24

A revisão de privacidade também cobriu stderr sem caminho: qualquer linha HTTP fica reduzida ao status; cabeçalhos `Authorization` e tokens `Bearer` são mascarados globalmente. Três regressões novas falharam antes da mudança e passaram depois, mantendo linha informativa benigna. Focais **34/34**. Reteste final Release `IntroSkipper.Integration.Tests`: **92 aprovados, 58 ignorados, 0 falhas (150 total)**; `git diff --check` limpo. Integrações ignoradas continuam não homologadas.

## Correção da tarefa de renovação TLS — 2026-11-24

`wacs.exe --list` identificou uma renovação pendente para 27/11/2026 e informou que a tarefa não estava configurada. Executei `--setuptaskscheduler` elevado; o win-acme detectou ação/diretório antigo incorreto, substituiu a tarefa para o WACS bundled em Program Files e depois reportou `Scheduled task looks healthy`. Consulta elevada: `Ready`, `SYSTEM`/Highest, `--renew`, gatilho diário e próxima execução definida. Não executei renovação forçada. A primeira execução e o reload do Nginx após renovação real permanecem pendentes.

## Reconciliação automática da tarefa win-acme no configurador — 2026-11-25

TDD adicionou asserção para que `configure-https.ps1` execute `wacs.exe --setuptaskscheduler` depois da configuração de certificado e valide exit code. RED falhou antes da mudança; GREEN passou. Suite `configure-https-credential.tests.ps1`, DuckDNS ACL PowerShell, transporte Node 16/16 e segurança shell via Git Bash passaram. No host, tarefa foi reparada e verificada `Ready`/SYSTEM/Highest/daily, WACS healthy; renovação real ainda não executou. Health HTTP/S e nginx -t continuam OK. Sem release por gate global 4.0.

## Preflight HTTPS e transação DuckDNS — 2026-11-25

O configurador agora valida Nginx, NSSM e, quando necessário, win-acme antes de qualquer recuperação de configuração, atualização DuckDNS ou migração do serviço. `-SkipCertificate` não exige WACS. Quando DuckDNS está habilitado, subdomínio, updater e arquivo de token também são verificados antes da orquestração.

A configuração DuckDNS mantém cópia protegida da configuração anterior até o update do provedor e a criação/verificação da tarefa SYSTEM terminarem. Em falha de update, registro ou verificação, o configurador restaura o JSON e a tarefa exportada; na primeira configuração, remove o JSON e a tarefa novos. Se a restauração falhar, o erro indica que o backup protegido foi mantido para recuperação. Sem segredos nos erros.

TDD RED confirmou ausência de `Assert-HttpsPrerequisites`. GREEN: `configure-https-orchestration.tests.ps1` passou em Windows PowerShell 5.1 e PowerShell 7, incluindo falha de preflight de cada binário, `SkipCertificate`, DuckDNS updater ausente, falhas de update/registro/verificação e primeiro setup. Regressões também passaram: `configure-https-credential.tests.ps1`, `configure-https-service-transaction.tests.ps1` em PS 5.1/7; `windows-duckdns-config-security.tests.ps1`; Node transporte DuckDNS 16/16; `duckdns-update.test.sh` via Git Bash; parser PowerShell e `git diff --check`.

Permanece pendente homologação em VM e a execução real de WACS. As falhas de emissão/renovação e `--setuptaskscheduler` permanecem após o commit do serviço; em bootstrap, a configuração HTTP retorna 503 fora do desafio ACME, conforme política que evita servir mídia em claro. Nenhuma alteração foi aplicada ao runtime nem publicada release.
### Falhas WACS e estado HTTPS recuperável — 2026-11-25

Adicionados wrappers testáveis para emissão ACME e reconciliação da tarefa WACS. Failure injection cobre `--source` e `--setuptaskscheduler`: em bootstrap sem certificado, o listener fica fail-closed com 503 fora do ACME challenge e não encaminha mídia em HTTP; durante renovação com chain/key existente, o listener TLS anterior permanece. Erros não imprimem o token DuckDNS. A recuperação operacional é corrigir DNS/portas/artefatos ou tarefa e reexecutar `configure-https.ps1`. Não se restaura automaticamente proxy HTTP anterior na primeira emissão, pois isso reexporia mídia em claro. Não houve WACS/ACME externo, renovação, reload real ou VM.

Validação final: `configure-https-orchestration.tests.ps1`, `configure-https-credential.tests.ps1` e `configure-https-service-transaction.tests.ps1` passaram em Windows PowerShell 5.1 e PowerShell 7; Node DuckDNS 16/16, ACL PS, shell Git Bash, parser e `git diff --check` passaram.
## Regressão Windows serial completa — 2026-11-25

`dotnet test MulletaFlix.sln --no-build --no-restore -c Release -m:1 --verbosity quiet` concluiu na segunda rodada com exit 0: 17 assemblies, 6.145 aprovados, 315 ignorados, 0 falhas (6.460 casos). A primeira rodada teve uma falha isolada em `LimitedConcurrencyLibrarySchedulerTests.DisposeAsync_WaitsForAdmittedEnqueueToDrain`; focal repetido 20/20 e assembly Controller 73/73, incluindo o caso, passaram. A fragilidade do teste é hipótese, não causa comprovada. IntroSkipper Release: 92/150 aprovados, 58 ignorados, 0 falhas; focal HTTP 404 10/10 após falha transitória na primeira execução. Casos ignorados dependem de MariaDB ou mídia remota.

Runtime corrente respondeu health direto/HTTP/HTTPS 200 e `nginx -t` passou. Instalador está no modo Basic e inicia via Tray após login; boot sem login e modo serviço não foram homologados. Nenhuma migração foi aplicada no servidor real. Gate de release permanece fechado. A contagem anterior de 54 omitia itens indentados; recontagem completa encontrou 79 checkboxes ativos abertos (82 no total, 3 no backlog futuro), além de CI hospedada sem sucesso, Authenticode e VM limpa pendentes; nenhum artefato final do HEAD atual nem publicação.
## Homologação integrada MariaDB — 2026-11-25 03:04 (-03)

- Corrigido `tests/IntroSkipper.Integration.Tests/ServerPersistenceMariaDbTests.cs`: o teste `LatestImageProvenanceMigration_PreservesExistingImageRows` agora migra do predecessor imediato da migração alvo, sem presumir que a migration alvo seja a última do assembly. A hipótese anterior deixou de ser válida depois de migrações posteriores serem adicionadas.
- RED/GREEN: a suíte integrada com MariaDB descartável havia falhado porque a asserção comparava a migration alvo ao último item. Após a correção, o focal passou 1/1; a suíte integral com MariaDB passou 149/150, com 1 skip, 0 falhas.
- `dotnet test MulletaFlix.sln --no-restore -c Release -m:1 --logger trx` terminou exit code 0: 17 assemblies, 6.376 aprovados, 84 ignorados, 0 falhas/6.460. `Jellyfin.Server.Integration.Tests` passou 146/146; `Server.Implementations` 1.801/1.865; `IntroSkipper.Integration.Tests` 149/150. Foram usados MariaDB 11.4.4 descartável, porta dinâmica loopback e aliases locais temporários `mysql`/`mysqldump`; serviço de produção não foi conectado. MariaDB temporário encerrou e porta fechou.
- Os 84 ignores permanecem explicitamente fora da aprovação: MongoDB ausente (38), providers externos ao vivo (13), requisitos Linux/macOS/Unix, mídia/instalação portátil, benchmark e casos de plataforma. Logs TRX em `%TEMP%\mflx-mariadb-full-9b45d2e6848c4b60bd40143995e3143e\trx`; credencial efêmera não foi registrada.
- Mapa técnico conferido contra módulos/manifests: mudança somente no teste, sem alteração de módulos, APIs, dependências ou fluxo runtime.
- Release preflight executado por revisão do gate: bloqueada. O gate local 4.0 proíbe release intermediária enquanto houver escopo ativo não validado; o roadmap tem 82 checkboxes abertos no total, dos quais 79 são ativos (3 estão no backlog futuro), além de integração Linux/Mongo/externa pendente. O checkout permanece em `main` com alterações não commitadas, 5 commits à frente de `origin/main`; `dist` ainda contém instalador 12.1.59 de 17/11 sem assinatura. Sem VM de instalação/upgrade, assinatura Authenticode ou CI hospedada bem-sucedida. Nenhum build novo, commit, push, tag ou publicação foi feito nesta etapa.

#### Preflight portal e release — 2026-11-25 03:06 (-03)

`node --test portal-site/tests/homologacao.test.mjs` passou 379/379; parse JSON do status e `git diff --check` passaram (apenas avisos de conversão CRLF/LF do Git). Reconsulta read-only confirmou `main` 5 commits à frente de `origin/main`, working tree com 19 entradas modificadas/não rastreadas, `v12.1.59` ausente local e remoto. O EXE em `dist` está NotSigned (SHA-256 ACA15AA4557FCB8C3AA440C24C9A9892CAC6E42EAB561DB2A9145DD2FE2418C6), sem versão embutida consultável; portanto é antigo e não foi aceito como candidato. Preflight interrompido pelo gate 4.0; nenhum pacote/commit/push/tag/release publicado.

## Auditoria live Windows complementar — 2026-11-25

Os testes de transação HTTPS passaram novamente em Windows PowerShell 5.1 e PowerShell 7: transaction, credentials e orchestration (6/6 execuções). Foi reforçada a limpeza pós-health das regras genéricas TCP/UDP Any ligadas ao executável Nginx validado, mantendo regras específicas de portas e programas não gerenciados.

**Host firewall permanece pendente:** o ActiveStore ainda contém regras públicas genéricas TCP Any e UDP Any para o Nginx instalado; regras gerenciadas 80/443 anteriores apontavam para executável antigo. A tentativa de correção live exigia UAC e foi rejeitada pela política de execução da sessão. O código é homologado por testes isolados, mas o host não foi alterado.

**Streaming da temporada investigada permanece pendente:** 73 links `.strm` retornaram HTTP 404. O ID amostrado não existe na coleção MongoDB configurada; 592 documentos concluídos com partes existem globalmente. Um preview não destrutivo gerou 489 links em outra saída e nenhum para essa temporada, indicando referências órfãs para essa mídia no catálogo atual. Nenhum link de produção foi alterado e nenhum payload foi baixado; é necessária reingestão/reindexação do item correto antes de regenerar.

Runtime/API instalado 12.1.58; registro de desinstalação reporta 12.1.5. A tarefa WACS foi reparada/verificada em contexto elevado e está saudável, mas primeira renovação/reload seguem pendentes. Health direto/proxy HTTPS e `nginx -t` estavam aprovados em evidência anterior. A solução completa tem 6.376 testes aprovados, 84 ignorados, zero falhas; 84 ignorados permanecem pendentes conforme dependências externas/plataformas. Gate de release segue aberto e nenhuma release foi publicada.

### Atualização in-place — metadados de versão Windows — 2026-11-25

A causa do registro mostrar 12.1.5 enquanto o servidor roda 12.1.58 era a cópia in-place não sincronizar DisplayVersion/DisplayName. O controlador sempre grava o updater embutido da build atual e repassa a versão preparada; após cópia robocopy bem-sucedida, o script valida ProductVersion da DLL instalada contra o alvo e atualiza os dois campos. Falha de cópia não toca no registro; falha/mismatch da validação apenas registra aviso e não mente sobre a versão. Script externo de packaging também recebe a regra.

Homologação: PowerShell updater metadata passou em Windows PowerShell 5.1 e PowerShell 7; os 7 scripts Windows do workflow de packaging passaram. UpdateInfoControllerTests 13/13; Jellyfin.Api.Tests completa: 1.252 aprovados, 3 ignorados, 0 falhas/1.255. Sem execução de update real. A instalação corrente ainda exibe 12.1.5 até receber build com o fix. Release não publicada; 79 itens ativos do roadmap continuam abertos (82 no total, 3 no backlog futuro) e demais gates formais permanecem pendentes.

### Revalidação WACS/certificado — 2026-11-25

O certificado público configurado para o hostname do servidor foi lido sem acessar a chave privada: `notBefore` 2026-10-03 22:24:14 UTC, `notAfter` 2027-01-01 22:24:13 UTC. A consulta atual da tarefa agendada win-acme não ficou disponível no contexto não elevado: `Get-ScheduledTask` não encontrou objetos e `schtasks /Query` retornou acesso negado. Portanto, a configuração elevada saudável registrada anteriormente não comprova execução de hoje nem renewal/reload; tarefa atual e última execução permanecem pendentes de verificação elevada. Nenhuma tarefa ou certificado foi alterado.

### Reconciliação do gate 4.0 — 2026-11-25

Revisão independente corrigiu a contagem anterior: uma busca rasa indicava 54 porque ignorava tarefas indentadas; a leitura completa encontrou 82 checkboxes abertos no documento, dos quais três (linhas 4330, 4337 e 4343) estão sob `Backlog futuro — fora do ciclo ativo`. Portanto o gate §4.0 exige fechar **79 itens ativos**, não 54 nem os 82 totais. Checkpoints de release DoD permanecem entre esses 79. Distribuição reportada na revisão: seis itens históricos de homologação/segurança, 37 de servidor/backend T0–T6, 20 frontend W1–W4, sete HTTPS T9 e seis checkpoints de release. A estrutura do roadmap e seus counts estão registrados com essa distinção; sem release intermediária.

## Rebuild Release limpo e suíte Windows — 2026-11-25

- A falha CS0234 em `MediaBrowser.Model/Net/MimeTypes.cs` era causada pelo preprocessamento de `contentFiles` do pacote `MimeTypes` não ocorrer durante o rebuild serial da solução. `MediaBrowser.Model.csproj` agora redefine a flag interna de limpeza imediatamente antes de `RunProduceContentAssets`; o comando Release limpo padrão passou após reproduzir RED anteriormente.
- Build limpo: `dotnet build MulletaFlix.sln -c Release --no-restore --no-incremental -m:1`; exit 0, 570 avisos, 0 erros. Rebuild de diagnóstico com `_CleaningWithoutRebuilding=false` também confirmou a causa e concluiu sem erros.
- Testes .NET em Release, binários recém-compilados, execução serial: exit 0 em 17 assemblies; 6.149 aprovados, 315 ignorados, 0 falhas (6.464 casos). A rodada não iniciou MariaDB/MongoDB; os casos dependentes desses serviços foram ignorados. Integração MariaDB descartável continua coberta pela rodada anterior registrada acima (6.376 aprovados, 84 ignorados).
- Não houve mudança de módulos/API/dependências/runtime. Mapa técnico conferido em 2026-11-25 e estrutura permaneceu igual.
- Gate §4.0: o blocker de build saiu de `[!]` para `[x]`; seguem 79 itens ativos abertos e 3 itens no backlog futuro. Sem instalador/tag/release: VM limpa, CI hospedada, Authenticode, WACS/reload e integrações externas continuam pendentes.

## Atualizador Windows em modo serviço — 2026-11-25

- TDD RED confirmou duas lacunas no updater externo e embutido: serviço não era interrompido antes de copiar os binários e o código tentava reiniciar mesmo após Robocopy indicar falha.
- GREEN: ambos os fluxos agora param `MulletaFlixServer`, aguardam até 30 s pelo estado `Stopped` e abortam antes da cópia se isso falhar. Restart do serviço/runtime ocorre dentro do ramo de cópia bem-sucedida. Em erro de Robocopy, o serviço fica parado e o pacote extraído é mantido para diagnóstico. A bandeja só reinicia em modo interativo; em modo serviço o log explica que ela deve ser iniciada pela sessão do usuário.
- `UpdateVersionMetadata.Tests.ps1` passou no Windows PowerShell 5.1. Os testes focais `EmbeddedUpdater` passaram 3/3 em Release, incluindo parse do script embutido pelo Windows PowerShell; Jellyfin.Api e os testes focais compilaram sem erros, com avisos existentes de documentação/analisadores.
- `git diff --check` passou; Git emitiu avisos de conversão de finais de linha CRLF/LF.
- Atualizador real, upgrade e modo serviço não foram exercitados, pois o host é Basic/Tray em uso; execução destrutiva foi evitada e o host não fornece VM limpa disponível. A homologação operacional do service mode segue pendente.
- Módulos, APIs, componentes e dependências não mudaram; o fluxo de atualização in-place mudou e o mapa técnico foi atualizado. Release continua bloqueada pelo §4.0 (79 itens ativos), VM, CI hospedada, assinatura Authenticode e validações externas. Nenhuma tag, release ou push foi realizado.

## Inicialização Windows presa na detecção de GPU — 2026-11-25

- Na inicialização iniciada pelo operador, o log parou após `=== INICIANDO DETECCAO AUTOMATICA DE HARDWARE ===`; `ApplicationHost` chama essa rotina sincronamente antes de registrar `Core startup complete`/`CoreStartupHasCompleted=true`. Durante a espera, `MulletaFlix.exe` permaneceu vivo, mas `/health` respondeu que o servidor ainda estava carregando.
- A causa no código era a enumeração WMI via `powershell.exe` sem timeout, usando `StandardOutput.ReadToEnd()` e `WaitForExit()` ilimitado. Isso também era consistente com o PowerShell do host travando em comandos simples. A relação é forte e localizada; ainda precisa de confirmação após instalar uma build corrigida.
- `HardwareDetectionService` agora limita a chamada a 5 s, drena stdout/stderr em paralelo, mata a árvore do processo em timeout e mantém as configurações de encoding atuais quando não consegue ler inventário da GPU. A consulta retorna somente nomes de GPU e não repete uma segunda chamada PowerShell sem limite.
- TDD: a primeira execução falhou na compilação porque `TryReadProcessOutput` ainda não existia; após a implementação, `HardwareDetectionServiceTests` passou 4/4 em Release, incluindo encerramento de processo lento e preservação da configuração quando o inventário falha. A build focal passou com avisos preexistentes de XML/StyleCop e sem erros.
- O runtime instalado 12.1.58 não foi substituído. Na última observação ele ainda estava em estado de carregamento; o código corrigido só pode ser validado operacionalmente após build/instalação em ambiente isolado.
- Módulos, APIs externas e dependências não mudaram. O fluxo síncrono de inicialização e a classe `HardwareDetectionService` foram alterados; mapa e ficha do vault devem refletir isso.

## Retomada da auditoria Windows — 2026-11-25

- A suíte `Jellyfin.Server.Implementations.Tests` em Release passou 1.772/1.772, com 95 ignorados por plataforma/serviços indisponíveis. O teste de prioridade reproduziu travamento em `File.Exists` para mídia num volume `E:` indisponível; a lógica agora reconhece primeiro extensões de payload de mídia. O teste passou isolado em 29 ms.
- Os testes de seleção de staging passaram 4/4 após o método evitar `Directory.CreateDirectory` quando `driveInspector` é fornecido pela fixture. O fluxo de produção continua criando o diretório quando consulta o `DriveInfo` real.
- Build Release do servidor passou (exit code 0) para output isolado. A solução completa executou os demais projetos; a API test suite ficou sem concluir `EmbeddedUpdaterScript_ParsesInWindowsPowerShell` por cinco minutos ao iniciar `powershell.exe`, e o runner abortou esse caso. Na mesma execução o teste `CacheEvictsLeastRecentlyUsedInactiveMediaToRespectLimit` falhou uma vez, mas passou no teste isolado e na repetição serial completa da assembly. Portanto, a homologação ampla não fica marcada como integralmente verde.
- A instalação atualmente iniciada segue em 12.1.58: `GET /health` respondeu 503 e o log termina em `INICIANDO DETECCAO AUTOMATICA DE HARDWARE`. A correção de inicialização ainda não foi instalada/observada no runtime.
- Gate final não passou: 79 itens ativos do roadmap, worktree sujo, cinco commits à frente de `origin/main`, sem Authenticode, CI hospedada verde ou VM limpa. A regra `.agents/rules/mulletaflix-conventions.md` §4.0 impede publicar release intermediária; nenhum pacote, tag ou release foi criado.
- Mapa técnico conferido; não houve mudança de módulo, API, dependência ou classe pública. A sessão foi registrada no vault.

### Smoke isolado corrigido e revalidação do runtime instalado — 2026-11-25

- Build Release do servidor em diretório isolado: exit 0, 7 avisos e 0 erros. Foi anexada somente uma cópia read-only do runtime MariaDB instalado ao output isolado; o candidato usou diretórios temporários sob `C:\ProgramData\MulletaFlix\Server\_codex-smoke-20261125`, portas HTTP 28096 e MariaDB 43306, sem importar dados/configuração da instalação.
- O candidato concluiu migrações, detectou que o PowerShell não entregou o inventário de GPU em 5 segundos, encerrou a árvore desse subprocesso, preservou a configuração de encoding existente e seguiu até `Core startup complete` / `Startup complete` em aproximadamente 24 segundos. `GET /health` respondeu HTTP 200 `Healthy`. Isso homologa o caminho de falha da detecção corrigida em Windows com MariaDB real isolado.
- A instância isolada e seu MariaDB foram encerrados; HTTP 28096 e MariaDB 43306 não permaneceram ouvindo. O processo iniciado pelo operador não foi parado, atualizado ou alterado.
- Revalidação do runtime do operador: a instalação continua em 12.1.58, `/health` em 8096 respondeu 503 “carregando” e o log de 25/11 termina em `INICIANDO DETECCAO AUTOMATICA DE HARDWARE`, sem `Core startup complete`. Evidência confirma que a correção ainda não chegou ao runtime instalado; upgrade live permanece pendente.
- Mapa técnico conferido nesta etapa; sem alteração de módulos, APIs, classes públicas, dependências ou topologia. A mudança de timeout em `HardwareDetectionService` já está registrada na ficha/mapa da sessão anterior.
- Preflight permanece bloqueado pelo gate `.agents/rules/mulletaflix-conventions.md` §4.0: 79 itens ativos abertos. Sem criação/assinatura/publicação de instalador, tag, release, push ou alteração no servidor de produção.

### Correção final da detecção de GPU e smoke Windows — 2026-11-25 21:19 BRT

- O primeiro smoke da alteração com PowerShell demonstrou que o servidor conseguia continuar após 5 s, mas a tentativa de matar o processo da consulta WMI deixou um processo auxiliar órfão e manteve o output temporário bloqueado. Essa estratégia foi descartada; não considerar o encerramento do filho homologado.
- Implementação final: `HardwareDetectionService` consulta os adaptadores pelo `EnumDisplayDevicesW` (`user32.dll`) e deixa de executar PowerShell/WMI. Falha/ausência de adaptador físico mantém a configuração existente. Não foi adicionada dependência.
- Build Release focal do servidor passou (exit 0). `HardwareDetectionServiceTests`: 4/4 após a normalização de nomes. Suíte completa `Jellyfin.Server.Implementations.Tests`: 1.772 aprovados, 95 ignorados, 0 falhas. Smoke real isolado: dois adaptadores enumerados (Intel UHD e NVIDIA RTX 3050), decisão NVENC, startup completo em 16,03 s, health 200 `Healthy`, nenhum descendente PowerShell. Processo candidato e MariaDB isolado foram encerrados; dados temporários e outputs de teste foram removidos.
- A instalação do operador não recebeu alteração. Durante a sessão, `/health` da porta 8096 respondeu 503 enquanto o processo 12.1.58 estava iniciando; na última observação a porta recusava conexão e não havia processo `MulletaFlix.exe`. Motivo dessa transição não foi determinado; validar novamente quando a instalação estiver iniciada.
- Estrutura técnica conferida e atualizada: `HardwareDetectionService`/fluxo de detecção mudou de subprocesso WMI para API User32, sem mudança de classe pública, endpoint, módulo, dependência ou topologia.
- Preflight de release refeito: `main` está cinco commits à frente de `origin/main`, worktree ainda tem alterações não commitadas, tag local `v12.1.59` não existe e o gate 4.0 tem 79 itens ativos abertos. Sem pacote instalador, assinatura, tag, push ou publicação; o gate do roadmap impede release parcial.

### Rechecagem do runtime Windows instalado — 2026-11-25 21:22 BRT

- Consulta read-only: `sc query` e `sc qc MulletaFlixServer` retornaram erro 1060 (serviço não registrado); `netstat` não mostrou listener na 8096 e `GET /health` recusou conexão. O uso observado anteriormente foi pela bandeja interativa; sem confirmar o modo de instalação pretendido, a ausência do serviço não é classificada como defeito.
- O log instalado foi modificado por último às 20:03:21 BRT e a última linha relevante visível indica parada de monitoramento de diretório; não há evidência suficiente para atribuir a causa da saída do processo. Nenhum processo foi iniciado/parado nesta rechecagem e nenhum dado de biblioteca foi incluído no registro.
- Homologação da instalação/boot permanece pendente até observar o modo de execução pretendido com o runtime corrigido. O smoke isolado do build atual continua sendo a evidência de inicialização disponível.

### Quoting do script de renovação Nginx em caminhos Windows — 2026-11-25 21:33 BRT

- TDD RED reproduziu um defeito em `Write-ReloadScript`: um caminho instalado contendo apóstrofo era interpolado sem escaping em literal PowerShell, alterando a interpretação da linha que chama o Nginx.
- GREEN adicionou `ConvertTo-PowerShellSingleQuotedLiteral`, que duplica apóstrofos em executável/raiz antes de gerar o script de renovação. Nenhum binário é invocado pelo teste; o conteúdo é analisado pelo parser AST e validado quanto a statements extras.
- Novo `configure-https-reload-script.tests.ps1` passou em Windows PowerShell 5.1 e PowerShell 7. As regressões de credenciais, orquestração e transação de serviço/firewall também passaram nas duas versões (8 invocações no total, todas exit 0). A integração WACS real, reload pós-renovação em VM e modo serviço continuam pendentes.
- O subitem de escaping foi marcado concluído em T9.1; T9.1 e os gates globais permanecem abertos. Release não executada: roadmap com 79 itens ativos, `main` cinco commits à frente de `origin/main`, worktree sujo e sem tag/manifesto candidato.
- Mapa técnico conferido/atualizado: o fluxo de renovação continua usando o mesmo script e Nginx; somente a serialização segura dos caminhos mudou. Sem endpoint, módulo, classe pública ou dependência nova.


## Auditoria runtime Windows e DataFolder canônico — 2026-11-25 22:15 BRT

- A instância instalada respondeu `/System/Info/Public` com versão 12.1.58. `/health` respondeu inicialmente HTTP 503 enquanto carregava e depois HTTP 200 `Healthy`; o listener permaneceu ativo.
- Processo e atalho `MulletaFlix (View Console)` usavam `--datadir C:\ProgramData\MulletaFlix\Server`, mas `HKLM\SOFTWARE\WOW6432Node\MulletaFlix\Server\DataFolder` apontava para `%LOCALAPPDATA%\MulletaFlix`. Os dois diretórios têm `data\device.txt` distintos; o arquivo sob ProgramData corresponde ao ID público atual.
- O usuário escolheu ProgramData como fonte oficial desta instalação. Após UAC, `DataFolder` foi alinhado para `C:\ProgramData\MulletaFlix\Server`; leitura posterior confirmou e `/health` continuou `Healthy`. Nenhum processo reiniciado; diretório antigo preservado sem cópia ou remoção.
- A interface mostrou divergência de ID salvo pelo navegador. O usuário confirmou mudança esperada; a conexão abriu a tela de login. Login e fluxos autenticados de avatar, IntroSkipper e reprodução seguem pendentes.
- Oito testes Windows em PowerShell 5.1 passaram: `UpdateVersionMetadata`, `StageBuildOptions`, `StageBackup`, `ServiceDataAcl`, `RcloneBootstrap`, `PublicServerFirewallRules`, `InstallerLocalization` e `Assert-StageIntegrity`.
- A instalação permanece 12.1.58; release bloqueada pelo gate local (79 itens ativos, árvore suja, cinco commits ahead, VM/CI/Authenticode pendentes). Nenhum pacote, tag ou release foi criado.

### Homologação autenticada Windows — avatar, IntroSkipper e reprodução STRM — 2026-11-25 22:31 BRT

- Login local concluído manualmente pelo operador. O avatar de perfil aparece como imagem no menu superior e a página de perfil oferece `Excluir Imagem` e `Escolher avatar da galeria`; A renderização de um avatar existente passou; a autoatribuição GetAvatar não foi exercitada nesta conta.
- A configuração live do Intro Skipper mostra concorrência `2`, limitada a `1–2` arquivos; timeout configurado `300 s`, com mínimo de `900 s` aplicado a probes/scans de `.strm` remoto. A análise automática de mídia nova e a reanálise de temporadas estabilizadas estão habilitadas.
- O player iniciou `mulletaflix_intro` antes da mídia. Log registrou `NativeIntroProvider` encontrando 1 intro e o início/fim da intro após aproximadamente 10 s. `NativeIntroProvider` chama `StrmPrebufferManager.PrepareAsync(item)` antes de liberar a intro, conforme código-fonte.
- Resultado de reprodução STRM: reprovado para o item testado. `StrmPrebufferManager` recebeu HTTP 404 na origem antes da intro; após a intro, os três processos FFmpeg de HLS também receberam HTTP 404 e o web player exibiu erro fatal. O `.strm` tem 56 bytes e foi identificado como remoto. Não houve cache de conteúdo útil nem confirmação de início da mídia principal. URLs e tokens foram omitidos.
- A origem HTTP 404 é causa imediata comprovada para esta amostra; não prova defeito no prebuffer ou no FFmpeg. A correção operacional depende de reingestão/reindexação do registro upstream correto e regeneração do `.strm`, depois repetição do teste. Os logs também mostram análise automática de muitas temporadas já ativa antes do teste de playback; a causa do disparo e seu escopo precisam ser verificados. Não foi iniciado scan manual nem alterada configuração do plugin.
- Evidência: `C:\ProgramData\MulletaFlix\Server\log\log_20261125.log` e três `FFmpeg.Transcode-...` correspondentes, com endereços/tokens removidos; UI local autenticada. Runtime instalado 12.1.58, não a build-fonte 12.1.59. Mapa técnico conferido; o fluxo existente é `NativeIntroProvider → IStrmPrebufferManager/StrmPrebufferManager → MediaSourceManager`.
- Gate de release permanece bloqueado por 79 itens ativos do roadmap e demais gates registrados acima. Nenhuma release/tag/publicação foi criada.

### Complemento GetAvatar — fluxo e limites do teste live — 2026-11-25 22:34 BRT

- A UI live do GetAvatar respondeu com `Avatar aleatório para novos usuários` habilitado e catálogo de 1.671 avatares. O avatar do perfil atual está renderizado; isso valida imagem persistida/servida, não a criação automática para usuário recém-criado.
- O código registra `UserCreatedAvatarConsumer` para `UserCreatedEventArgs`, e `AvatarValidationService` atribui imagem a contas sem avatar quando a opção está ligada. Não foi criada conta real nesta instalação para não alterar a lista de usuários.
- Testes direcionados de API/avatar: `AvatarControllerUserAvatarAuthorizationTests`, `AvatarFileStoreTests` e `GetAvatarAdminAuthorizationTests`: 46 aprovados, 3 ignorados (casos específicos de filesystem Linux), 0 falhas. Esses casos não exercitam diretamente o consumidor de `UserCreatedEventArgs` nem o validador de usuários sem avatar; essa cobertura permanece pendente.

### Retomada da homologação Windows — updater e preflight — 2026-11-25 22:46 BRT

- Auditoria read-only do runtime: `MulletaFlix.exe` 12.1.58 ativo com `--datadir C:\ProgramData\MulletaFlix\Server`; `/health` HTTP 200 `Healthy`; a pasta de dados coincide com a decisão do operador. O runtime instalado não contém as mudanças 12.1.59 do checkout.
- O updater externo e o embutido iniciavam `MulletaFlixServer` com `-ErrorAction SilentlyContinue` e não aguardavam `Running`; uma falha era ocultada. TDD RED reproduziu as duas lacunas. GREEN passou após ambos os scripts exigirem start sem supressão de erro e esperarem até 30 s pelo estado `Running`.
- `UpdateInfoController` focal EmbeddedUpdater: 3/3; `Jellyfin.Api.Tests` completo: 1.253 aprovados, 3 ignorados por filesystem Linux, 0 falhas (1.256 total). `UpdateVersionMetadata.Tests.ps1`: passou em Windows PowerShell 5.1 e PowerShell 7. Os scripts externo e embutido foram analisados pelo parser do Windows PowerShell 5.1; o externo também pelo PowerShell 7. Quatro suítes HTTPS existentes passaram em PowerShell 5.1.
- `git diff --check` passou. Não houve alteração do runtime nem execução do updater real. A árvore continua suja e `main` cinco commits à frente de `origin/main`.
- Risco aberto: Robocopy >=8 mantém o servidor parado e não há rollback dos binários parcialmente copiados. Reiniciar uma instalação parcialmente substituída pode agravar a falha; precisa de recuperação transacional/backup e teste isolado em VM antes de ser corrigido.
- Homologação ainda parcial: logs instalados acumulam 58.120 avisos `Chromaprint returned 0 points`; a causa ainda não foi separada entre fontes inválidas, metadados/duração e mídia sem áudio. A amostra STRM reproduzida anteriormente retornou 404 da origem. Live IntroSkipper está configurado com concorrência 2; uma amostra de processos observou 1 FFmpeg, sem medida do pico de concorrência. Avatar existente renderiza, mas autoatribuição em conta nova não foi exercitada.
- Gate §4.0 segue aberto com 79 itens ativos. Sem VM limpa, CI hospedada verde ou Authenticode; instalador em `dist` é antigo e `NotSigned`. Nenhuma tag, pacote, push, instalação ou publicação foi feita.
### Complemento Tray mode — verificação do processo inicializado — 2026-11-25 22:51 BRT

A revisão do runtime confirmou que esta instalação usa Basic/Tray, por isso a correção foi ampliada para o ramo interativo nos dois updaters. O servidor é iniciado com `-PassThru -ErrorAction Stop` e precisa continuar vivo após 2 s; ausência, erro de start ou saída imediata gera log explícito e código 1. A bandeja também é iniciada com `-ErrorAction Stop`/`-PassThru` e falha explicitamente se não iniciar ou sair imediatamente. Isso verifica apenas o processo inicial, não o endpoint `/health`.

Homologação atualizada: suíte `Jellyfin.Api.Tests` passou 1.253, 3 skips Linux, 0 falhas; `UpdateVersionMetadata.Tests.ps1` passou em Windows PowerShell 5.1 e PowerShell 7; parser externo passou nas duas versões e o parser embutido está coberto pelo teste C#. O runtime live permaneceu fora do escopo de alteração.
### TDD GetAvatar — evento de criação de usuário — 2026-11-25 22:57 BRT

- Foi identificada lacuna de cobertura: os testes anteriores validavam API/arquivo/persistência do avatar existente, sem executar `UserCreatedAvatarConsumer`.
- Novo teste `UserCreatedAvatarConsumer_AssignsCatalogAvatarWhenAutoAssignIsEnabled` cria usuário e catálogo somente em fixture temporária, dispara `UserCreatedEventArgs` diretamente pelo consumidor e verifica arquivo de perfil e mapeamento. Passou 1/1 em Release.
- Essa verificação valida consumidor → `AvatarService` → persistência local em fixture. Não testa a publicação de evento por `UserManager` nem `AvatarValidationService` no host; ainda não houve criação de usuário em produção.
### Reteste de IntroSkipper, prebuffer e concorrência — 2026-11-25 23:01 BRT

- `IntroSkipper.Integration.Tests` completo: 92 aprovados, 58 ignorados, 0 falhas/150 casos. Os skips incluem E2E STRM e testes MariaDB, porque não há conexão para banco descartável configurada; não foi usado MariaDB operacional.
- Testes focados `StrmPrebufferManagerTests`, `NativeIntroProviderTests` e `MediaSourceManagerTests`: 41/41 aprovados em Release. Isso verifica contratos locais com fixtures; não substitui a reprodução real com URL externa funcional.
- Durante 20 amostras de 1 s, o host observou sempre 2 processos `ffmpeg.exe`; máximo 2. Configuração live do IntroSkipper também está em 2. É uma janela observada, não garantia sobre todos os períodos ou usos de FFmpeg.
- `Jellyfin.Api.Tests` atualizado após teste de evento GetAvatar: 1.254 aprovados, 3 ignorados por Linux, 0 falhas/1.257 casos. Runtime continuou HTTP 200 `Healthy`, 12.1.58, no diretório `C:\ProgramData\MulletaFlix\Server`.
- O log instalado tem 58.120 avisos `Chromaprint returned 0 points`; o binário 12.1.58 não registra o exit code/diagnóstico que foi adicionado ao código 12.1.59, então a causa do conjunto não está determinada. A amostra STRM reproduzida retornou 404.
### Complemento de homologação autenticada e FFmpeg Windows — 2026-11-25 23:08 BRT

Login local foi retomado e confirmado na página inicial; o cabeçalho expôs uma imagem de perfil identificada como Raphael. O controle do Chrome perdeu resposta ao abrir a configuração GetAvatar, então a configuração live não foi relida nesta continuação. Código confirma registro do `UserCreatedAvatarConsumer` no DI e publicação síncrona de `UserCreatedEventArgs` após persistência por `UserManager.CreateUserAsync`; log instalado mostra `Avatar validation completed. All avatars are valid`. O teste fixture do consumidor passou 1/1 e regressão `Jellyfin.Api.Tests` passou 1.254/1.257 (3 skips Linux). Sem criar usuário de produção; evento de criação real no host segue pendente.

FFmpeg instalado tem muxer Chromaprint e gerou 140 pontos (560 bytes) com áudio sintético de 20 s, exit 0. Isso verifica o recurso básico do binário, sem identificar a causa dos 58.120 avisos em mídias reais. Runtime `/health` 200, processo 23184, versão 12.1.58, DataFolder ProgramData; não reiniciado. Concorrência live configurada em 2 e observada com máximo 2 no intervalo anterior de 20 s. A amostra real `.strm` anterior retorna 404; falta origem válida e nova execução diagnóstica da build corrigida. O mapa técnico foi conferido e não houve mudança estrutural nesta continuação. Gate de release permanece fechado (79 itens ativos e requisitos externos sem evidência); nenhuma publicação.

### Revisão adversarial final da rodada — 2026-11-25 23:10 BRT

Fable-Judge reexecutou no Windows o teste focal `UserCreatedAvatarConsumer` + EmbeddedUpdater: 4/4; PowerShell `UpdateVersionMetadata.Tests.ps1` passou. Diff não removeu nem enfraqueceu as assertions preexistentes do teste de avatar. `git diff --check` exit 0 com avisos informativos LF/CRLF. Roadmap: 82 caixas abertas, das quais 3 em Backlog futuro, restando 79 ativas sob o gate §4.0. `main` cinco commits ahead, worktree 33 entradas modificadas/não rastreadas, sem tag local v12.1.59 e instalador dist de 17/11 sem assinatura/correspondência com HEAD. Veredito desta rodada: VERIFIED WITH CAVEATS para os testes automatizados, saúde read-only, avatar de perfil existente e muxer Chromaprint sintético; origem STRM real funcional, conta recém-criada live, update aplicado, CI/VM/Authenticode e roadmap ativo não homologados.

### E2E MariaDB Windows e STRM HTTP local preparado — 2026-11-25 23:35 BRT

MariaDB 11.4.4 de teste em datadir temporário exclusivo e loopback `127.0.0.1:43307`; o listener operacional 3306 não foi usado. `ServerPersistenceMariaDbTests`: rodada integral 30 pass/1 skip/4 falhas `Too many connections` durante setup, sem assertion falhando; quatro falhas repetidas serialmente passaram 4/4. Foram removidos exclusivamente os 32 schemas/users temporários com prefixo de teste da instância descartável; após o reteste há 0/0 pendentes. E2E de STRM preparado com dois `.strm` apontando para WAV sintético servido por HTTP local: HEAD 200, `audio/wav`, 1.764.078 bytes. Ainda não executado porque o scan live mantém dois FFmpeg ativos. Uma tentativa anterior de FFmpeg direto foi cancelada ao perceber que criaria o terceiro processo global; sem resultado de fingerprint. O scanner live segue na 12.1.58; últimas 1.000 linhas do log incluíram 787–789 avisos de fingerprint vazio e nenhum candidato de intro. Não atribuir causa até rodar a build diagnóstica com uma fonte válida.

### Encerramento de fixtures temporárias — 2026-11-25 23:37 BRT

MariaDB isolado recebeu shutdown gracioso e a fixture HTTP foi encerrada. Portas de teste 43307/43877 fechadas; o scanner live ainda mantém 2 FFmpeg. E2E STRM remoto permanece pendente para não exceder o teto. Os artefatos sintéticos e o datadir inativo ficam em %TEMP%; nenhum dado operacional foi acessado.

### Continuação da homologação Windows — 2026-11-25 23:40 BRT

- Scanner live continua avançando e ocupa exatamente 2 processos FFmpeg; o log de `C:\ProgramData\MulletaFlix\Server\log\log_20261125.log` estava em 19.823.773 bytes às 23:39. O teste E2E com `.strm` HTTP não foi iniciado para não passar do limite configurado. Os dois processos observados pertencem ao scanner do operador.
- Teste Release focal `EntrypointLibraryOptionsTests`: 1/1 aprovado. `UpdateVersionMetadata.Tests.ps1` no Windows PowerShell 5.1 passou. `git diff --check` passou após remover uma linha em branco excedente no fim de `MulletaFlix-master/docs/homologacao-release-12.1.59.md`; permanecem apenas avisos informativos de normalização LF/CRLF.
- Rechecados os pré-requisitos de release: a regra local §4.0 ainda impede release intermediária com 79 itens ativos abertos; árvore segue suja, `main` cinco commits à frente e sem evidência nova de assinatura Authenticode, CI verde ou VM limpa. Nenhuma tag, push ou publicação executados.
- Mapa técnico rechecado: esta continuação mudou apenas o registro de evidências; módulos, APIs, classes, dependências e fluxo não mudaram.
- Pendências: executar E2E `.strm` remoto quando o scan liberar slots sem ultrapassar dois FFmpeg; causa dos fingerprints vazios em mídia real ainda requer build com os novos diagnósticos e fonte válida. Homologação live e release final continuam abertas.

### Regressão Windows sem FFmpeg adicional — 2026-11-25 23:46 BRT

- `HardwareDetectionServiceTests`: 4/4 aprovados; `UpdateInfoControllerTests`: 14/14; `NebulaDownloaderEngineTests` + `NebulaStagingWatcherTests`: 56/56. `EntrypointLibraryOptionsTests`: 1/1; `UpdateVersionMetadata.Tests.ps1`: passou em Windows PowerShell 5.1. Nenhum desses testes precisou criar outro `ffmpeg.exe`; o scan live ficou nos 2 processos observados.
- Fixture do E2E remoto foi preparada em diretório temporário: MariaDB 11.4.4 isolado em `127.0.0.1:43307` (200 conexões, datadir temporário) e servidor HTTP de áudio sintético em `127.0.0.1:43877`. HEAD respondeu 200, `audio/wav`, 1.764.078 bytes; dois `.strm` apontam para essa origem válida. O teste ainda não foi iniciado porque há dois FFmpeg do scan live.
- `/health` continuou `Healthy`, processo servidor PID 23184, instalação em ProgramData; o log foi visto em 19.825.965 bytes às 23:45. Nenhum dado da instância MariaDB operacional em 3306 foi usado.
- `git diff --check` passou depois de remover a linha em branco excedente; avisos remanescentes são normalização CRLF/LF. Mapa técnico conferido; sem alteração estrutural.
- Release permanece sem execução/publicação conforme gate local §4.0 (79 pendências ativas); worktree suja, cinco commits à frente, instalador sem assinatura. Nenhum commit, push, tag ou publicação nesta etapa.
- Pendência imediata: aguardar slots do scan, executar `AnalyzeStrmEpisodes_PersistsSharedIntrosAndCapsConcurrentFingerprints` com a origem real `.strm` HTTP, confirmar persistência e máximo de concorrência <=2, e encerrar as duas fixtures temporárias após o teste.

### Infra E2E temporária reativada — 2026-11-25 23:49 BRT

- Reaberto apenas o MariaDB descartável 11.4.4 em `127.0.0.1:43307` e HTTP de fixture em `127.0.0.1:43877`; ambos respondem. O MariaDB operacional do servidor continua PID 21700 em `--datadir=C:\ProgramData\MulletaFlix\Server\data\mariadb_data --port=3306`; nenhuma conexão de teste foi direcionada a ele.
- Health consultado novamente: `/health` = `Healthy`. O scanner ainda ocupa 2 FFmpeg; o E2E não foi iniciado. O log ficou sem alteração desde 23:46:47 apesar dos processos continuarem ativos, então a evolução do scan precisa ser confirmada antes do reteste.
- Solicitada decisão ao operador para pausar temporariamente o scan e executar o E2E sem ultrapassar 2 FFmpeg, ou aguardar seu término. Nenhuma ação de cancelamento foi enviada.
- Mapa técnico rechecado; sem alteração de classes, APIs, dependências ou fluxo.

### Regressão HTTPS/DuckDNS e estágio Windows — 2026-11-25 23:53 BRT

- Quatro scripts PowerShell de HTTPS passaram (credenciais, orquestração, reload e transação de serviço). `windows-duckdns-config-security.tests.ps1` passou quando apontado para `tools/release/duckdns-update.ps1`; `windows-stage-robocopy-exit-code.tests.ps1` passou.
- Testes Node de transporte de token DuckDNS e saída de pacote: 18/18 aprovados. `copy-nebula-python-runtime.tests.ps1` via Pester 3.4 passou 1/1; `build-update-package-installer-output.tests.ps1` e `windows-tray-build-source.tests.ps1` também passaram.
- Uma invocação inicial de `windows-duckdns-config-security.tests.ps1` não informou `-UpdaterPath` correto e falhou antes das assertions; corrigida a invocação e retestada com o arquivo real `tools/release/duckdns-update.ps1`, resultado passou. Uma primeira chamada de `Invoke-Pester` usou opções indisponíveis no Pester 3.4; repetida com sintaxe compatível e passou.
- Runtime `/health` Healthy; scanner segue ocupando 2 FFmpeg com novos PIDs, e log ainda avança. HTTP/MariaDB de fixture continuam isolados e prontos. O E2E remoto ainda aguarda liberação do scan ou autorização para pausa temporária.
- Mapa técnico conferido; sem mudança estrutural. Nenhum release/commit/push/tag iniciado.

### E2E STRM remoto homologado e scan retomado — 2026-11-26 00:44 BRT

- Com autorização do operador, pausei no painel a tarefa `Detect and Analyze Media Segments` após 43 minutos (25%) e confirmei estado `Cancelado`; antes de testar, não havia nenhum `ffmpeg.exe` ativo.
- Executei `AnalyzeStrmEpisodes_PersistsSharedIntrosAndCapsConcurrentFingerprints` no projeto `IntroSkipper.Integration.Tests` (Release, sem rebuild): 1/1 aprovado. O cenário usou dois arquivos `.strm` para uma origem HTTP local sintética, fixture MariaDB 11.4.4 isolada na porta 43307; validou análise não vazia, persistência de intros compartilhadas e máximo de concorrência <=2.
- Encerrei a fixture HTTP e o MariaDB de teste de forma graciosa. Consulta anterior ao shutdown não encontrou schemas nem usuários de teste; a porta 43307 ficou fechada. O banco operacional em 3306 não foi usado. Uma tentativa inicial de chamar `mysqladmin` pelo nome falhou pois não está no PATH; repetida com caminho absoluto do MariaDB instalado e concluída com sucesso.
- Retomei a tarefa pelo botão de execução do painel; a UI saiu do estado cancelado e mostrou a barra de progresso. Checagem imediata depois da retomada: `/health` Healthy e zero `ffmpeg.exe` naquele instante, portanto o teste não excedeu dois workers. O recomeço da atividade foi confirmado pela UI, mas workers ainda não haviam reaparecido até a consulta instantânea.
- A guia de tarefas foi deixada em segundo plano e a guia do YouTube do operador restaurada em primeiro plano.
- `git diff --check` passou; somente avisos informativos LF/CRLF. Mapa técnico conferido: sem mudanças em módulos, APIs, classes, dependências ou fluxo; apenas registro de homologação. Nenhuma release, commit, push ou tag executados. Gate §4.0 continua impedindo release intermediária (79 pendências ativas, conforme auditoria anterior).
- Pendência restante: acompanhar se o scan retomado abre processos FFmpeg (limite configurado <=2) e se progride normalmente; E2E STRM sintético passou, mas isto não prova por si só fingerprints válidos em mídia real nem a execução de intro antes de playback real.

#### Rechecagem do scan retomado — 2026-11-26 00:50 BRT

A leitura seguinte do painel mostrou `Detect and Analyze Media Segments` com controle de parada (ativo) e progresso visual que avançou de aproximadamente 25% para aproximadamente 60%; portanto a tarefa retomou e continua progredindo. A amostragem posterior ainda encontrou zero `ffmpeg.exe` e `/health` Healthy. Registro progresso visual, sem afirmar execução de workers FFmpeg nessa amostragem. Guia do YouTube restaurada em primeiro plano. `git diff --check` permanece sem erros; só avisos de normalização de fim de linha.

## Auditoria live do servidor Windows — 2026-11-26 01:04 BRT

- Fonte local: main em 795adef6a, cinco commits à frente de origin/main, worktree ainda com alterações. Versão de fonte 12.1.59; runtime instalado 12.1.58 em C:\Program Files\MulletaFlix\Server, dados oficiais em C:\ProgramData\MulletaFlix\Server.
- Saúde: /health retornou 200 Healthy no app :8096, no proxy HTTP e no proxy HTTPS; o hostname público mulletaflix.duckdns.org/health também respondeu 200 sem ignorar validação TLS.
Nginx -t passou. Nginx e MongoDB estão Running/Automatic; MariaDB escuta em loopback 127.0.0.1:3306; proxy STRM em 127.0.0.1:2124; MongoDB em 127.0.0.1:27017.
- Intro Skipper: tarefa retomada e em progresso; amostragem encontrou exatamente 2 processos FFmpeg, limite configurado. O log de 25/11 tinha 73.507 linhas e terminou às 23:59:59 com avisos Chromaprint returned 0 points, inclusive janela [0, 0]; o log de 26/11 permanece vazio nesta amostra. A causa em mídia real não está determinada. O E2E sintético remoto registrado nesta sessão passou 1/1.
- Firewall: encontrei duas regras ativas MulletaFlix.Server para o executável instalado, perfil Public, TCP/UDP, portas/endereço Any. O teste PublicServerFirewallRules.Tests.ps1 passou; executei o helper do release candidate para o caminho instalado. Pós-checagem: zero regras inbound allow para o executável instalado, regras Nginx 80/443 intactas e as três rotas health continuaram 200. Ainda há quatro regras Public de executáveis de stage/build/smoke: duas apontam para alvos de teste ausentes e duas para binários de desenvolvimento existentes. A tentativa de limpar esse conjunto via shell foi rejeitada pela revisão automática; não houve tentativa de contornar a rejeição. Nenhuma outra regra ou binário foi alterado.
- Inicialização: registro HKCU Run aponta para o Tray com caminho entre aspas; o servidor ativo está em modo Basic/Tray e não há serviço MulletaFlixServer instalado. Boot/login limpo e execução pós-logoff ainda não foram homologados. A tarefa WACS não apareceu na enumeração não elevada nesta amostra.
- Release: API oficial do GitHub ainda retorna v12.1.58; source aponta 12.1.59. Get-AuthenticodeSignature no executável instalado reporta NotSigned; não há certificado válido com chave privada de Code Signing nos stores CurrentUser/LocalMachine e signtool.exe não está no PATH. GitHub Actions: os três últimos runs consultados terminaram cancelled. Gate §4.0: 79 itens ativos abertos (82 no total, três backlog futuro). Sem package/tag/push/publicação nesta etapa.
- Verificações: consulta das rotas locais/públicas;
Nginx -t; contagem FFmpeg; bind addresses; firewall ActiveStore; PublicServerFirewallRules.Tests.ps1; leitura redigida do log; consulta release/CI públicas; git diff --check.
- Mapa técnico conferido e permaneceu estruturalmente igual. Arquivos de documentação/status atualizados nesta sessão; nenhum código do produto foi alterado aqui.
- Próximos passos: analisar zero fingerprints em mídia real com diagnóstico do build 12.1.59 e fonte válida; tratar/revalidar os quatro allow rules residuais com a ferramenta apropriada; validar boot/service e WACS em ambiente isolado; concluir os 79 itens ativos e gates de CI, assinatura e upgrade limpo antes da release final.

### Revisão adversarial — 2026-11-26 01:06 BRT

INTENT: o runtime permitia TCP/UDP público Any para o executável instalado; a expectativa operacional é expor somente Nginx nas portas 80/443 e remover/restringir exceções públicas do servidor; o helper remove-public-server-firewall-rules.ps1 implementa e o instalador o executa no fluxo de upgrade.

TWINS: searched (New-NetFirewallRule|advfirewall firewall add rule|firewall add allowedprogram).*MulletaFlix - found 1 other site: mulletaflix-server-windows-mulletaflix-installer-branding/nsis/mulletaflix.nsi adds only a MariaDB rule; no second creator for the broad server executable rule was found.

PublicServerFirewallRules.Tests.ps1: passou. portal-site/tests/homologacao.test.mjs: 379/379; o teste verifica a contagem total do roadmap sem promover progresso parcial. git diff --check: passou depois da remoção de trailing whitespace desta seção. Mídia real continua sem causa conclusiva para zero points.

## Homologação integrada STRM/Nebula — 2026-11-26

- A consulta `GET /api/files?q=...` do Nebula falhava com HTTP 500 porque faltava o índice Mongo `type_1_status_1_search_name_1_uploaded_at_-1`. O bootstrap foi corrigido para garantir o índice e propagar falhas de permissão/criação; conflitos de especificação 85/86 permanecem idempotentes.
- O índice foi aplicado na instância operacional Windows. Busca `Equipe de Resgate` retornou HTTP 200 com 9 resultados; 9/9 STRMs pertenciam a IDs `completed`. `HEAD` de um STRM real retornou 200 e `video/x-matroska`. Um STRM antigo consultado isoladamente permaneceu 404 por ID inválido.
- Suíte Nebula completa: 326 aprovados, 1 ignorado e 5 subtestes aprovados; teste focal 2/2 e `py_compile` passaram. `ruff` indisponível (`No module named ruff`).
- No MulletaFlix, painel autenticado confirmou a tarefa `Detect and Analyze Media Segments` retomada. Dois processos FFmpeg ligados ao servidor foram amostrados; sem argumento Chromaprint naquela amostra. Portanto o scan está executando, mas fingerprint real bem-sucedida segue pendente.
- Rechecagem às 01:39 BRT: health 200 `Healthy`, exatamente 2 FFmpeg (um com execução Chromaprint), e busca Nebula ainda retorna 9 resultados. O log do dia tinha 32.181 linhas, 30.314 entradas relacionadas a Chromaprint, 30.305 avisos `returned 0 points` e nenhuma linha reconhecida como retorno não vazio; execução do processo, portanto, não equivale a fingerprint válida.
- `FFmpegAudioDurationTests`: 10/10 Release aprovados; cobre fallback de duração quando a duração do catálogo está ausente. Isso valida a correção na fonte/candidato, não a instalação nem o resultado no runtime 12.1.58.
- Mapa MFX conferido: sem mudança estrutural no próprio servidor/API; correção ocorreu no bootstrap Mongo do componente Nebula integrado. Mapa Nebula atualizado.
- Nenhum commit, tag, artefato ou publicação foi criado. Permanecem os bloqueios registrados: gate §4.0 com 79 pendências ativas, Authenticode sem chave/certificado, CI hospedada cancelada e validação de upgrade em VM limpa pendentes.

## Reavaliação de fingerprint STRM sem duração — 2026-11-26

- Correção da conclusão anterior: a tag pública `v12.1.58` já contém `ResolveDurationAsync` e o fallback de consulta de duração de áudio; o commit dessa implementação é ancestral da tag. Portanto, a ausência desse fallback na versão instalada não explica, por si só, os avisos `[0,0]`.
- A implementação ainda tinha uma lacuna: se tanto Jellyfin quanto `ffprobe` não conseguirem resolver duração, `IntroFingerprintEnd` permanecia zero. Agora `QueuedEpisode.CalculateIntroFingerprintEnd` atribui uma janela positiva limitada por `AnalysisLengthLimit` (padrão de 10 minutos), preservando a regra percentual para durações conhecidas. `FFmpegService.FingerprintAsync` rejeita janelas não finitas ou vazias antes de consultar streams ou iniciar FFmpeg.
- `FFmpegAudioDurationTests`: 20/20 aprovados em Release, incluindo duração ausente, limite mínimo e ranges inválidos. `git diff --check` passou; houve apenas avisos Git de conversão CRLF/LF. A suíte focada não iniciou FFmpeg.
- A captura desta verificação ainda encontrou dois FFmpeg do servidor em execução. Nenhum `ffprobe` adicional foi iniciado para não ultrapassar o teto operacional de dois workers. Duração reportada pelo relay, retorno não vazio e gravação real de segmentos permanecem pendentes; a correção está validada em teste, mas não foi instalada.
- A primeira amostra real escolhida para `ffprobe` retornou exit code 1 sem duração; uma verificação HTTP `HEAD` subsequente mostrou que aquele `.strm` específico está em 404. Em 20 arquivos da mesma temporada, 8 responderam 200 e 12 responderam 404. Portanto, a falha desse `ffprobe` não serve como evidência de duração ausente em um stream saudável; uma das 8 amostras 200 ainda precisa de probe sob o teto de concorrência.
- As opções de runtime `Automatically Analyze New Media` e `Update Missing Segments During Scan` foram desligadas e salvas para drenar slots conforme autorização anterior; antes de encerrar a tentativa, ambos os valores originais foram restaurados e salvos. A tarefa programada segue marcada como cancelada; retomada manual da varredura global depende de confirmação/validação da janela operacional.
- O painel de suporte do plugin reportou `Scan running: yes`, fila com 254 itens alterados e 1 scan manual. O scan de `Equipe de Resgate`, temporada 1, foi iniciado pelo controle de temporada e a UI confirmou “Scan in progress”; o endpoint enfileira uma passagem própria e limpa/recalcula apenas essa temporada. Até a última checagem, a passagem aguardava a atividade já em execução: não havia log de “Analyzing” após o enfileiramento nem FFmpeg com muxer Chromaprint. Portanto, aceite da requisição não é conclusão; manter a homologação dessa temporada pendente até status terminal e segmentos verificados.
- Hipótese causal agora delimitada, ainda sem confirmação em mídia real: a origem STRM pode omitir duração tanto na biblioteca quanto na consulta de áudio; a janela de fingerprint zero foi corrigida na fonte. A hipótese anterior que atribuía a causa à ausência de fallback em 12.1.58 está revogada.
- A estrutura do plugin foi alterada por um método estático em `QueuedEpisode` e uma validação de intervalo em `FFmpegService`; mapa técnico atualizado. Não houve módulo, dependência, endpoint ou componente novo.
- Sem commit, pacote, tag, push ou publicação. Gate §4.0 permanece aberto com 79 itens ativos e seguem pendentes assinatura Authenticode, CI verde e VM limpa de upgrade.

## Rechecagem live do scan da temporada — 2026-11-26 02:29 BRT

- O painel autenticado `Timestamps` exibiu 9/9 episódios de `Equipe de Resgate (2022)`, Temporada 1, com segmentos Intro e Credits. Essa tela, porém, não prova gravação pelo scan manual: os segmentos também existiam antes do pedido e o painel de suporte ainda reportava `Scan running: yes`, `254 changed items` e `1 manual scan`.
- O log operacional continuou analisando temporadas fora do escopo autorizado (incluindo ThunderCats Roar, temporada 1, 50 arquivos) depois do cancelamento da tarefa global. Às 02:29 BRT havia 1 FFmpeg ativo; amostras anteriores desta rodada encontraram 2, nunca acima do limite configurado. `/health` respondeu 200 `Healthy`.
- O log segue mostrando vários `Chromaprint returned 0 points` com janela `[0, 0]` no runtime instalado (Jellyfin 12.1.58, plugin IntroSkipper 12.0.4.0). Algumas janelas de introdução tinham duração positiva; a maioria dos arquivos da temporada amostrada permaneceu em `[0,0]`. A correção da fonte local 12.1.59 evita intervalos vazios/sem duração, mas não está instalada e ainda não foi homologada em origem `.strm` HTTP 200.
- O scan manual permanece PENDENTE: o pedido foi aceito às 02:14 BRT, mas não há confirmação de que a passagem própria de `Equipe de Resgate` terminou nem de que os segmentos visíveis foram recalculados. A tarefa global `Detect and Analyze Media Segments` continua marcada como Cancelled, porém análises automáticas de outras temporadas prosseguem; a causa do trabalho em curso não foi isolada entre passe ativo e fila de itens alterados.
- Não iniciar uma terceira análise nem declarar E2E live aprovado. Próximo passo técnico é identificar a origem do passe residual/254 itens e, após drenar ou cancelar de modo suportado, aguardar `ScanStatus` terminal para a temporada e verificar pontos não vazios e persistência. O limite operacional observado permanece no máximo dois FFmpeg.
- Release não executada: §4.0 mantém 79 itens ativos; continuam faltando CI hospedada verde, VM limpa de upgrade e certificado/chave Authenticode. Nenhum pacote, tag, commit, push ou publicação foi criado.

#### Recheck após documentação — 2026-11-26 02:31 BRT

`/health` retornou 200 `Healthy`; havia 2 processos FFmpeg, dentro do máximo configurado. O log continuou avançando em outra temporada e manteve aviso `[0,0]` (Dragon Ball Super - Beerus S1, Credits). `portal-site/tests/homologacao.test.mjs` passou 379/379 após a atualização do roadmap; `git diff --check` passou, somente avisos de normalização CRLF/LF. Nada disso conclui o scan manual nem homologa o candidato instalado.

### STRM real e timeout — 2026-11-26 02:38 BRT

- Validação sem FFmpeg da temporada autorizada `Equipe de Resgate`, S1: 9/9 `.strm` responderam `HEAD 200`; uma amostra `GET Range: bytes=0-0` respondeu `206`, `video/x-matroska`, bytes `0-0/421024776` e foi lido 1 byte. Os streams reais desta temporada estão disponíveis para análise.
- Controle causal: 3 amostras de `Bungo Stray Dogs`, S3, responderam `HEAD 404`; a busca do endpoint Nebula local retornou 0 arquivos concluídos para Bungo e 9 para Equipe de Resgate. Isso explica warnings de zero points em parte da biblioteca, sem provar que explique todos os casos.
- A configuração instalada `IntroSkipper.xml` informa `MaxParallelism=2`, `ScanTimeoutSeconds=300`, `AutoDetectIntros=true` e `UpdateMediaSegments=true`. A tag `v12.1.58` não contém o piso de timeout STRM de 15 minutos; a fonte 12.1.59 tem `MinimumStrmScanTimeoutSeconds=900`, e `FFmpegAudioDurationTests` cobre 300 s configurados → 900.000 ms em `.strm`. Esse ajuste ainda não está no runtime.
- `/health` continuou 200 `Healthy`; dois FFmpeg ativos no último snapshot. Não iniciei FFmpeg/ffprobe adicional. O scan manual de Equipe de Resgate continua sem confirmação terminal; o painel anterior mostrava 254 itens alterados e 1 scan manual, enquanto o log avançava em outras temporadas.
- Próxima prova live: aguardar um slot livre/scan terminal e executar fingerprint em um dos 9 streams HTTP 200, respeitando o teto total de dois workers; em seguida verificar pontos e persistência novos na temporada. O runtime antigo também impede validar o piso de timeout de 15 minutos.

#### Revalidação de testes e diff — 2026-11-26 02:43 BRT

- `FFmpegAudioDurationTests` recompilado e reexecutado: 20/20 aprovados, 0 falhas. `portal-site/tests/homologacao.test.mjs`: 379/379 aprovados, inclusive contrato de contagem do roadmap. `git diff --check`: exit code 0; só avisos existentes de conversão CRLF/LF.
- Esses testes validam código e documentação, não instalam o candidato nem comprovam fingerprint real. O runtime ainda usa a configuração/tag antiga e o scan live da temporada permanece sem resultado terminal confirmado.

### Processo FFmpeg observado — 2026-11-26 02:44 BRT

Dois `ffmpeg.exe` filhos de MulletaFlix estavam ativos com entrada HTTP, filtros de análise de vídeo e saída null. Isso confirma leitura de mídia remota por FFmpeg no scan atual, dentro do limite 2. Não era fingerprint Chromaprint no instante da amostra e os processos analisavam outras temporadas, não a temporada autorizada. Health estava 200 `Healthy`; ainda não há conclusão do scan manual.

#### Rechecagem do worker — 2026-11-26 02:46 BRT

Os dois PIDs FFmpeg observados às 02:44 saíram antes da rechecagem 30 s depois; o log avançou para outra temporada e o contador atual era 1. A fila, portanto, recicla workers e segue avançando, mas não ficou ociosa o bastante para uma prova externa sem risco de exceder o teto. Nenhuma linha de scan manual concluído foi observada.

### Origem da fila que atrasa o scan manual — 2026-11-26 02:51 BRT

- O log comprova a sequência: 00:02 `Starting analysis pass: Library`; 00:42:51 cancelamento da tarefa e `Analysis pass cancelled: Library`; imediatamente depois, `Starting analysis pass: ChangedItems`. Novo pedido de tarefa em 00:43:59 ficou separado. Cancelamentos posteriores da tarefa Library não cancelaram o passe ChangedItems.
- Às 02:14:37 o painel aceitou scan manual de Equipe de Resgate enquanto o passe ChangedItems ainda percorria a biblioteca. Às 02:47–02:50 o log ainda analisava outras temporadas. A fila da fonte processa manual scans após a passagem ativa terminar; essa dependência explica por que cancelar a tarefa programada não libera os workers.
- `/Intros/ScanStatus/{seasonId}` sem token administrativo responde 401; não consultarei nem extrairei credenciais. A guia autenticada não está aberta e o Chrome desta sessão rejeitou a criação de guia oculta. Não reiniciar o servidor de produção para limpar fila em memória sem janela de manutenção explícita.
- Próximas opções seguras: aguardar o passe ChangedItems concluir e observar o scan manual; ou planejar janela autorizada de manutenção para parar/reiniciar o servidor e enfileirar somente o scan manual. O teto de 2 FFmpeg deve permanecer. Uma melhoria candidata é dividir passes ChangedItems em lotes para que pedidos manuais tenham latência limitada; exige TDD e avaliação antes de integrar.
- Estado a 02:51: health 200, `FFmpegCount=2`; a fila continua ativa. Nenhuma alteração de dados de mídia ou processo foi feita.

### Auditoria Windows — 2026-11-26 03:04 BRT

Revalidação em host real: health `/health` 200; painel IntroSkipper reporta 260 itens ChangedItems e 1 scan manual, que ainda não iniciou. FFmpeg executou Chromaprint em entradas HTTP; máximo observado 2 processos. Opções AutoDetect e UpdateMissing foram salvas como false para pausar novos itens, preservando a fila ativa; restauração para true pendente após o E2E autorizado.

Homologação offline Windows passou: `dotnet test MulletaFlix.sln -c Release --filter FullyQualifiedName!~Integration` (exit 0, sem falhas; `Server.Implementations.Tests` registrou 95 skips, com outros skips em diferentes projetos), scripts do instalador/HTTPS 19/19, transporte DuckDNS 16/16, `dotnet publish` self-contained `win-x64` e build do Tray sem erros. Isso não substitui scan real, instalação do candidato ou upgrade em VM limpa. O runtime continua 12.1.58; as correções da fonte 12.1.59 ainda não estão ativas.

Release não executada: §4.0 ainda tem 79 itens ativos; main diverge cinco commits de origin/main, worktree suja; CI verde, VM de upgrade e assinatura Authenticode continuam pendentes. Nenhuma tag/artefato/publicação foi criada nesta continuação. Ver sessão Obsidian [[Sessões/2026-11-26 - Auditoria completa do servidor Windows]].

03:09 BRT: os testes focados de IntroSkipper `FFmpegProcessRunnerTests`, `StrmInputResolverTests`, `ChromaprintAnalyzerTests` e `EntrypointLibraryOptionsTests` passaram 35/35 em Release. Não acessaram o banco de produção nem abriram novo stream. O scan manual real permanece pendente.

## Resultado final da varredura manual autorizada — 2026-11-26 03:29 BRT

- O passe `ManualScan` da temporada autorizada `Equipe de Resgate`, S1, iniciou às 03:07:27 e percorreu Introduction, Credits, Recap, Preview e Commercial. Às 03:25:58 o log iniciou o passe ChangedItems seguinte, evidência de que o passe manual terminou e a fila anterior retomou.
- No painel Timestamps, os 9 episódios exibem Intro (~0–1m01s) e Credits (~23m01s–23m33s); os mesmos segmentos já existiam antes do scan, então não afirmo que foram recém-gravados. Recap, Preview e Commercial continuam sem timestamps visíveis. Não houve warning `Chromaprint returned 0 points` para esta temporada durante a passagem observada. Persistência de resultados novos por episódio não pôde ser diferenciada dos valores preexistentes.
- Restauradas e salvas as configurações temporariamente pausadas: `AutoDetectIntros=true` e `UpdateMediaSegments=true`. A configuração do host ativo confirma `MaxParallelism=2`, `ScanTimeoutSeconds=300`; `/health` respondeu 200 e havia 2 processos FFmpeg no snapshot final, sem exceder o teto.
- O teste live ocorreu no servidor instalado Jellyfin 12.1.58 / IntroSkipper 12.0.4.0. Não homologou as correções da fonte 12.1.59 (piso de 900 s para STRM) porque o candidato não foi instalado. O mapa técnico do servidor foi conferido nesta continuação e permaneceu estruturalmente igual.
- Não foi criado commit, tag, pacote ou publicação. Release continua bloqueada pelos gates §4.0 (79 itens ativos abertos), CI hospedada verde, VM limpa para upgrade e certificado/chave privada Authenticode. Também permanecem pendentes Recap/Preview/Commercial e comprovação de persistência nova; preservar a concorrência máxima 2.


Rechecagem documental 03:29 BRT: node --test portal-site/tests/homologacao.test.mjs passou 379/379 (0 falhas); git diff --check foi repetido após corrigir esta linha e passou, com avisos apenas de normalização CRLF/LF. Worktree permanece main cinco commits à frente de origin/main, com alterações preexistentes/locais; não foram criados commits ou tags. Release permanece bloqueada conforme gates acima.

### Updater transacional Windows — 2026-11-26 04:02 BRT

A auditoria especializada identificou que `apply-update.ps1` copiava no diretório ativo depois de parar o serviço, podia deixar atualização parcial sem reinício e encerrava por nome processos de outras instalações. O script agora valida a versão no stage antes do stop, monta stage mantendo arquivos existentes (`Uninstall.exe`), aplica ACL do install root, restringe a parada a executáveis dentro da instalação selecionada, troca pastas no mesmo volume com backup e reverte se startup/health falhar. O timeout do PID agora aborta; a limpeza de backup aguarda HTTP 200 em `/health` na porta interna de `network.xml`.

TDD RED/GREEN: teste C# `UpdateInfoControllerTests` 14/14; PowerShell `UpdateVersionMetadata.Tests.ps1` passou cenários de falha de stage, preservação de arquivos gerenciados, swap/rollback, health 503→200 e timeout, porta configurada e isolamento de processos; scripts Windows de packaging 7/7; `ci-packaging-coverage.test.mjs` 7/7. `dotnet publish Jellyfin.Server.csproj -c Release -r win-x64 --self-contained true` passou; reflexão no assembly publicado confirmou o resource updater. Isso não exercitou SCM, instalação ou rollback real. O `.build/audit-updater-final` contém o artefato local de inspeção; não foi instalado.

A alteração estrutural foi mapeada: C# embute a fonte física do packaging como `MulletaFlix.Api.Updater.apply-update.ps1`; o controller grava esse resource e o empacotador sincroniza a mesma fonte. Mapa atualizado. Não houve mudança no runtime, reinício, commit, tag, push ou release. A release segue bloqueada pelo §4.0, CI hospedada, VM limpa para upgrade e certificado/chave privada Authenticode.

#### Rechecagem de prontidão do updater — 2026-11-26 04:12 BRT

A revisão especialista encontrou casos de rollback falso: binding apenas em `LocalNetworkAddresses` e HTTP redirecionado para HTTPS obrigatório. O probe foi refinado para usar endereços bind configurados, respeitar `BaseUrl`, validar a porta atual/legada (default 8096 apenas quando ausente) e evitar seguir redirects. Aceita HTTP 200 ou redirect HTTPS absoluto sem enfraquecer validação TLS. O endpoint `/System/Ping` prova que a API está respondendo, não que MariaDB/Nebula estejam prontas; esta é a semântica intencional do rollback para não reverter por indisponibilidade de integrações.

`UpdateVersionMetadata.Tests.ps1` passou incluindo bind local, BaseUrl, portas, porta inválida, retry 503→200, redirect HTTPS e rejeição de redirect HTTP. `UpdateInfoControllerTests` passou 14/14; os 8 scripts Windows passaram (incluindo updater); `ci-packaging-coverage.test.mjs` 7/7. `dotnet publish` Release self-contained `win-x64` terminou com exit 0 e reflexão confirmou o resource atualizado no assembly. `node --test portal-site/tests/homologacao.test.mjs` passou 379/379; `git diff --check` exit 0 (avisos apenas de CRLF/LF). Runtime instalado continua 12.1.58; `/health` retornou Healthy, `MaxParallelism=2` e foram observados 2 FFmpeg, sem reinício ou alteração do servidor. Instalação/rollback real em VM permanece pendente e release não foi publicada.

Reteste final 04:15 BRT, após a última revisão de liveness: os 8 scripts Windows e `UpdateInfoControllerTests` 14/14 passaram novamente; contrato do portal 379/379 e `git diff --check` também passaram. A pesquisa de padrões duplicados/encerramento global de processos e cópia direta para o install root retornou zero ocorrências adicionais.

### Correções finais do probe de liveness do updater — 2026-11-26 05:00 BRT

- O reteste em Windows PowerShell 7 e Windows PowerShell 5.1 detectou e cobriu configuração XML sem VirtualInterfaceNames: valores nulos/vazios agora são descartados e o padrão veth é aplicado corretamente, evitando ignorar por engano todas as interfaces.
- A revisão especializada encontrou falso sucesso possível para LocalNetworkAddresses=:: com IPv4 desabilitado. O updater agora não transforma IPv6Any em ::1 nesse modo; como o servidor não tem endereço local conhecido para provar, a configuração falha fechada. No modo dual-stack o probe IPv4 continua disponível.
- UpdateVersionMetadata.Tests.ps1 passou no PowerShell 7 e Windows PowerShell 5.1 após essas alterações. Os scripts Windows scripts/tests/*.Tests.ps1 passaram 8/8; UpdateInfoControllerTests passou 14/14. Testes de manifesto e saída do instalador passaram. portal-site/tests/homologacao.test.mjs passou 379/379.
- dotnet publish Jellyfin.Server.csproj -c Release -r win-x64 --self-contained true --output .build/audit-updater-final --no-restore terminou com exit 0. Inspeção reflexiva de MulletaFlix.Api.dll confirmou MulletaFlix.Api.Updater.apply-update.ps1 e as lógicas atuais de filtragem de prefixos vazios e IPv6Any. git diff --check passou (avisos só de normalização LF/CRLF). A pesquisa de twins de encerramento global/cópia direta retornou zero ocorrências.
- A revisão especialista considerou alinhado o tratamento de IgnoreVirtualInterfaces para aliases, IP explícito e fallback IPv6 sem binding; após a última alteração, os dois runtimes PowerShell locais passaram. Não houve instalação, reinício de serviço, manipulação de mídia ou acesso ao banco de produção.
- O mapa técnico foi conferido: permanece o fluxo apply-update.ps1 como fonte única → resource embutido em Jellyfin.Api → controller extrai durante atualização; mudaram apenas regras internas do cálculo de liveness e seus testes, sem novo endpoint, módulo ou dependência.
- O servidor ativo segue 12.1.58 e não recebeu esta correção. A release continua bloqueada: gate §4.0 com 79 itens ativos, árvore main com alterações e cinco commits adiante do upstream, ausência de CI hospedada verde, certificado/chave Authenticode de produção e homologação de upgrade em VM limpa. Nenhum commit, tag, push ou release foi criado.

## Rechecagem Windows e baseline passivo — 2026-11-26 12:32 BRT

- Runtime instalado: MulletaFlix.exe 12.1.58, /health Healthy; /System/Info/Public informou 12.1.58. Em amostragem sequencial passiva de ~150 s, ambas as rotas responderam 11/11 HTTP 200. p50/p95 nearest-rank: /health 21,1/156,4 ms e info 12,4/24,4 ms. Working set variou de 1.219,2 a 1.304,7 MiB; CPU acumulada aumentou 321,6 s; data 3,271 GiB/377 arquivos, metadata 2,029 GiB/10.702 arquivos, cache 0,103 GiB/9.083 arquivos (0 erros; reparse points ignorados); C: tinha 81,75 GiB livres. A amostra é curta, sem carga representativa e sem rotas autenticadas; Linux e métricas de volume/duração Nebula seguem sem medição; o footprint recursivo de data/cache/metadata foi medido separadamente.
- Snapshot de execução: 0 FFmpeg do servidor ativos. Processo iniciou às 11:03:02, filho do explorer.exe, sem --service; no SCM existe MulletaFlixNginx (Running/Automatic), mas MulletaFlixServer está ausente. O Tray pode iniciar em modo Executable quando o serviço não existe e o instalador tem modo Basic sem serviço; o operador havia iniciado o Tray/servidor manualmente. Logo, isso não prova instalação defeituosa, mas também não comprova boot/logoff. Reboot/instalação Advanced em VM descartável continua pendente.
- O log disponível, anterior ao início do processo atual, registra workers IntroSkipper encerrados após 900.000 ms e Chromaprint com 0 points para janelas [0,0] em STRM. Isso confirma um sintoma histórico no runtime antigo, não um novo scan nesta amostra: nenhum FFmpeg estava ativo. O candidato fonte 12.1.59 continua sem instalação/validação live.
- Auditoria de release atual: 80 checkboxes abertos no roadmap (77 ativos, três futuros); main está cinco commits à frente de origin/main com 38 paths modificados/não rastreados; CI pública #700 cancelada sem steps úteis; versão pública v12.1.58; zero certificados Authenticode utilizáveis no store; sem VM de upgrade disponível. Não publiquei, criei tag/commit ou atualizei o runtime.
- Uma tentativa de criar guia oculta no Chrome falhou porque o provider não oferece suporte à opção visible=false; não alterei as guias existentes do usuário. A inspeção subsequente foi somente do inventário de janelas.
- Mapa técnico revisado, sem mudança de módulos/APIs/dependências; nova evidência anexada ao T0.1, que permanece parcialmente aberto. Portal status atualizado e contrato será repetido.

### Limite da medição Nebula — 26/11/2026

Leitura read-only limitada aos últimos 3.000 registros do log operacional: 383 linhas no snapshot, sem agregados `Fila:` ou `Stats runtime` para fila, transferências ou duração. O log de montagem de 1,78 GB foi deixado intacto e não lido; nenhuma consulta MongoDB foi feita. As métricas de volume/duração Nebula exigem uma fonte agregada autenticada ou uma janela futura com eventos de transferência; T0.1 permanece parcial.

## Fechamento da rechecagem Windows e release — 2026-11-26 12:48 BRT

- Contrato do portal repetido após reconciliação documental: 379/379; roadmap: 398 concluídos, 80 pendentes de 478 (77 ativos e 3 futuros). A revisão especializada não achou caixa adicional que possa ser fechada integralmente com as evidências atuais.
- Log Nebula atual em `MulletaFlix-master/Tools/NebulaPython/nebula.log` (tail limitado a 5.000 linhas): 359 ocorrências de marcadores pending/retry e 198 de falha/timeout; não representam contagem de jobs porque retries se repetem. Nenhuma duração numérica com unidade foi interpretável; 244 tokens numéricos de tamanho em linhas de transferência somaram 240.000 bytes, mas não podem ser deduplicados com segurança. O log do servidor em ProgramData também não expôs agregados reconhecíveis. `rclone-mount.log` (1,78 GB) não foi lido; sem MongoDB ou tarefa iniciada. Volume, duração e tamanho real da fila seguem não demonstrados.
- Executei apenas a etapa local de preflight de `publish-release.ps1`; ela abortou antes de obter credenciais/chamar GitHub porque a tag `v12.1.59` não existe/aponta para HEAD. Testes locais `publish-release-artifact-manifest.tests.ps1` e `build-update-package-installer-output.tests.ps1` passaram. O fluxo completo não pode avançar: 80 pendências, árvore de trabalho modificada, CI pública cancelada, sem certificado Authenticode com chave privada e sem VM limpa. Nenhum artefato, commit, tag, push, publicação ou atualização de produção foi executado.

### Exercício isolado do updater — 2026-11-26 12:53 BRT

O script de packaging `apply-update.ps1` foi executado contra uma instalação simulada sob `%TEMP%`, com um payload que não contém DLL com ProductVersion compatível. Resultado esperado: exit 1 durante validação do stage; hash do arquivo sentinela instalado permaneceu igual; nenhum diretório `mfx-update-*` ficou; log foi criado. Isso comprova apenas rejeição segura de payload inválido no fluxo real do script; não exercita swap/health/rollback ou serviço. `UpdateVersionMetadata.Tests.ps1` foi reexecutado em Windows PowerShell 5.1 e PowerShell 7, ambos passaram. Uma tentativa de remover a fixture temporária foi rejeitada pela revisão automática de comandos destrutivos; a fixture permanece isolada em `%TEMP%` e não contém dados do servidor.

### Renovação HTTPS e reload WACS em produção — 2026-11-26 13:31 BRT

- A auditoria encontrou uma renovação WACS sem tarefa agendada e sem etapa de instalação/reload. Ao editar a renovação pelo gerenciador interativo, o WACS iniciou uma emissão real. A validação ACME HTTP-01 foi aprovada e o certificado foi escrito no store configurado. A instalação era `none`; foi configurada a etapa de script para `C:\ProgramData\MulletaFlix\Server\nginx-reload.ps1`.
- A primeira execução do script, no contexto interativo sem elevação, falhou ao sinalizar `Global\ngx_reload_6920` (Access denied). A execução administrativa interativa também falhou, pois o objeto global do Nginx é protegido para o serviço `SYSTEM`. O reload gracioso foi então executado por uma tarefa temporária como `SYSTEM`/Highest: `LastTaskResult=0`; essa tarefa temporária foi removida após o sucesso.
- Configurei o agendamento oficial do WACS com `--setuptaskscheduler` em contexto elevado. A tarefa `win-acme renew (acme-v02.api.letsencrypt.org)` está `Ready`, principal `SYSTEM`, `Highest`, ação `wacs.exe --renew --baseuri ...`; WACS `--list`, executado elevado, reporta “Scheduled task looks healthy”. A tarefa executa diariamente às 09:00 com atraso aleatório configurado de até quatro horas. Em 26/11 às 13:33, iniciei a tarefa real uma vez: estado final Ready, `LastTaskResult=0`; WACS informa que a próxima renovação de certificado só vence em 20/01/2027. Na reconsulta elevada, `Get-ScheduledTaskInfo` e `schtasks /Query` retornaram valores diferentes para NextRunTime; observar o próximo disparo real antes de registrar uma hora única.
- Homologação pós-reload: `http://127.0.0.1:8096/health` e `https://mulletaflix.duckdns.org/health` retornaram HTTP 200 `Healthy`; Nginx reportou `nginx/1.30.5`. Handshake público negociou TLS 1.3, `PolicyErrors=None`, certificado CN correspondente ao hostname, validade 26/11/2026–24/02/2027.
- O teste prova uma emissão ACME real e execução bem-sucedida do script de reload no host. A tarefa real `--renew` também foi iniciada manualmente pelo Task Scheduler sob SYSTEM e terminou com código 0, sem emissão adicional porque a próxima renovação vence em 20/01/2027. A execução automática no horário marcado e a persistência após reboot continuam pendentes. O serviço Nginx também continua usando binários/configuração sob o perfil do usuário enquanto roda como SYSTEM; sem migração neste trabalho.
- Fonte/runtime MulletaFlix permaneceu na versão instalada 12.1.58; não houve troca de binários, interrupção prolongada, commit, tag ou publicação. A release candidata 12.1.59 continua bloqueada pelo gate §4.0 (77 itens ativos abertos e 3 futuros), worktree com mudanças, CI hospedada verde, assinatura Authenticode com chave privada e VM limpa para upgrade.

### Fechamento da auditoria Windows e do fluxo de release — 2026-11-26 13:35 BRT

- Depois da correção WACS, repeti `node --test portal-site/tests/homologacao.test.mjs`: 379/379 aprovados. `git diff --check` terminou com exit 0; apenas avisos de normalização CRLF/LF preexistentes, sem erro de whitespace. Health local e público permaneceram HTTP 200; a tarefa WACS testada manualmente segue `Ready`/SYSTEM, `LastTaskResult=0`.
- Preflight do fluxo de release já executado nesta sessão abortou antes de GitHub por falta da tag `v12.1.59` ligada ao commit candidato. No fechamento, a tag continua ausente local e remotamente; `main` está cinco commits à frente de `origin/main` e há 38 paths modificados/não rastreados. Roadmap mantém 80 pendências ativas (399 concluídas/479; a seção explícita Backlog futuro não contém checkboxes abertos); CI hospedada verde, VM limpa de upgrade e certificado Authenticode com chave privada continuam sem evidência.
- Nenhum commit, tag, push, asset ou release foi criado. Nenhum binário do servidor foi atualizado. A conclusão operacional do HTTPS não remove os gates globais da release 12.1.59.

### Correção da classificação do gate e revalidação de boot — 2026-11-26 15:16 BRT

- TDD da contagem do roadmap: RED reproduziu a diferença (portal esperava 398 concluídos, a fonte já tinha 399); GREEN atualizou `roadmapChecklistCoverage` e adicionou contrato para contagem literal e classificação por fronteira explícita. A fonte tem 399 `[x]`, 80 `[ ]`, total 479. A única seção `## Backlog futuro` não contém checkboxes abertos; por isso, para §4.0, as 80 pendências são ativas até que o roadmap as classifique explicitamente como futuras. Os três itens históricos em 4332/4339/4345 não estão sob essa seção; o antigo split 77/3 não tinha base estrutural.
- `portal-site/homologacao-status.json` agora registra 399/80/479, `activePending=80` e `futurePending=0`. `portal-site/tests/homologacao.test.mjs` confirma os totais literais e que apenas checkboxes dentro da seção explícita de backlog ficam fora do gate.
- Revalidação elevada WACS: tarefa `win-acme renew (acme-v02.api.letsencrypt.org)` confirmada habilitada, `Ready`, principal `SYSTEM`/Highest, ação `wacs.exe --renew`; última execução manual em 26/11 às 13:33:11, resultado 0. `wacs.exe --list` elevado reportou a tarefa saudável; consultas não elevadas não a listam. Gatilho diário 09:00, atraso aleatório até quatro horas; `Get-ScheduledTaskInfo` e `schtasks` deram NextRunTime diferentes, então o primeiro disparo automático real continua pendente de observação.
- Evidência de reboot do host: Windows reiniciou às 10:56:24; serviço Nginx começou 10:56:54, antes do Explorer às 10:56:56. O processo MulletaFlix iniciou por volta de 11:03 como filho do Explorer, sem serviço próprio. O boot do proxy foi observado; boot do app antes do login/independente do Tray não foi comprovado.
- Release continua bloqueada pelo gate §4.0 com 80 pendências ativas. A branch local tem cinco commits à frente, 38 paths pendentes, tag v12.1.59 ausente, CI #700 cancelada, sem chave Authenticode disponível e sem VM limpa de upgrade. Nenhum commit/tag/push/release ocorreu.

### Modo de inicialização do instalador Windows — 2026-11-26 15:26 BRT

- Auditoria do boot mostrou Nginx iniciando como serviço antes do login, mas `MulletaFlix.exe` iniciando sob Explorer sem serviço próprio. O diálogo NSIS Setup Type rotulava Basic como “Recommended” e Service como “not recommended”, sem informar que Basic depende de sign-in/logoff.
- Agora `ShowSetupTypePage` substitui os rótulos herdados após criar os controles: Basic é explicitamente para desktop/usuário atual, inicia após sign-in e termina com a sessão; Service é para servidor always-on, roda sem usuário logado e informa a configuração adicional de network shares. A cor de advertência inadequada foi removida; Service é recomendado para servidor headless/always-on. A tela mantém a escolha padrão Basic, sem mudar instalação/runtime existente.
- TDD: `InstallerLocalization.Tests.ps1` falhou antes da alteração e passou depois em Windows PowerShell 5.1 e PowerShell 7; todos os scripts Windows de packaging passaram 8/8; `makensis /PPO` com fonte NSIS atual passou (exit 0). Nenhum instalador foi compilado ou instalado; a tela não foi renderizada em VM nesta rodada.
- A instalação ativa permanece no modo observado (MulletaFlix iniciado sob Explorer); a correção orienta futuras instalações. Mudança só de texto/estilo em Setup Type; sem módulos/APIs/classes/dependências novos. Release continua bloqueada por §4.0 e 80 itens ativos.

### Rechecagem técnica do servidor Windows e preflight final — 2026-11-26 15:39 BRT

- Health local atual: `/health` respondeu 200 `Healthy` e `/System/Info/Public` respondeu 200 na versão instalada 12.1.58. O processo MulletaFlix está presente; não há processo FFmpeg ativo. O log mais recente em ProgramData foi gravado às 13:04 e o snapshot limitado de 3.000 linhas contém 444 menções IntroSkipper e 241 avisos de Chromaprint/FFmpeg; estes registros são históricos e não comprovam scan em andamento.
- Homologação automatizada: `dotnet test tests/IntroSkipper.Integration.Tests/IntroSkipper.Integration.Tests.csproj -c Release --no-restore` passou com 102 aprovados, 58 ignorados e 0 falhas (160 total). O E2E `AnalyzeStrmEpisodes_PersistsSharedIntrosAndCapsConcurrentFingerprints` foi ignorado porque não há conexão MariaDB descartável configurada; este run não valida processamento de STRM contra MariaDB real. O worker de teste tem FFmpeg/ffprobe disponíveis. O teste de localização NSIS passou em Windows PowerShell 5.1 e PowerShell 7, os oito scripts PowerShell de packaging passaram, o contrato do portal passou 381/381, `makensis /PPO` exit 0 e `git diff --check` exit 0 (avisos apenas de normalização LF/CRLF).
- A guia Chrome oculta não pôde ser criada porque o provider desta sessão não oferece a opção `visible=false`; as guias existentes permaneceram sob controle do usuário. Não houve scan ativo a pausar. Nenhuma requisição autenticada, E2E de mídia externa, instalação/upgrade ou renderização visual do NSIS foi realizada nesta rodada.
- Preflight final: `publish-release.ps1 -Tag v12.1.59` encerrou antes do acesso ao GitHub porque a tag não existe/aponta para o commit validado. O gate §4.0 continua com 80 checkboxes ativos abertos (roadmap: 399 concluídos/479; backlog futuro explicitamente demarcado: zero pendentes). `main` está cinco commits à frente, a árvore tem 40 paths modificados/não rastreados, CI pública #700 está cancelada, não há chave privada Authenticode disponível nem VM limpa de upgrade. Não houve commit, tag, push, upload, publicação ou atualização do servidor.
- A revisão estrutural do mapa técnico foi repetida: nenhuma API, módulo, dependência ou fluxo de aplicação novo. A mudança de UI continua limitada a textos runtime do Setup Type e seu teste estrutural; o instalador não foi compilado/instalado. Pendências de release e homologação real estão abertas.

### E2E com MariaDB descartável e STRM real — 2026-11-26 16:09 BRT

- Preparei MariaDB 11.4.13 portátil do ZIP oficial, validei SHA-256, inicializei datadir isolado em `.build`, bind loopback `127.0.0.1:33319`, sem serviço Windows, e confirmei que o listener pertencia somente ao PID de teste. Nenhuma conexão foi feita à instância operacional 3306. Ao final, `mariadb-admin shutdown` terminou exit 0 e o processo/porta de teste desapareceram; o diretório temporário `.build/audit-mariadb-e2e-20261126` permaneceu porque o controle automático bloqueou duas tentativas de limpeza.
- Com o banco isolado configurado, a suíte `IntroSkipper.Integration.Tests` passou 159/160 (159 aprovados, 1 ignorado, 0 falhas). O E2E sintético `AnalyzeStrmEpisodes_PersistsSharedIntrosAndCapsConcurrentFingerprints` passou 1/1 e confirmou fingerprints persistidos e concorrência exata de 2 para os quatro STRM sintéticos.
- Repeti o mesmo E2E com os dois primeiros STRM reais da temporada autorizada de Os Cavaleiros do Zodíaco. As duas chamadas FFmpeg ocorreram e a assertion do limite máximo de concorrência (2) passou, mas `EmptyFingerprints` fez o teste falhar antes da confirmação de segmentos. O endpoint HTTP Range da origem local `127.0.0.1:2124` retornou 404 para o primeiro ID. A busca `/api/files?q=Os%20Cavaleiros&limit=100` retornou um único arquivo concluído; nenhum dos 73 IDs dos STRM correspondeu a esse resultado único. Isso não comprova ausência desses IDs em todo o banco da origem. Assim, esta execução não prova defeito no Chromaprint sobre mídia válida: a origem não reconheceu os IDs testados e não entregou mídia fingerprintável. O log de origem registra outros trabalhos da fila, mas o `rclone-mount.log` de 1,78 GB permaneceu intacto.
- Essa diferença de IDs fica pendente de reconciliação no Nebula/origem antes de repetir análise real. Não regenerei nem sobrescrevi os STRM do usuário. Nenhuma API, módulo, classe, dependência ou fluxo de aplicação foi alterado nesta etapa; o mapa técnico segue válido. A release continua bloqueada pelo §4.0 e gates de CI/assinatura/VM já registrados.

### Encerramento da janela de auditoria — 2026-11-26 16:13 BRT

- Rechecagem local final: `/health` HTTP 200 `Healthy`, `/System/Info/Public` HTTP 200 versão 12.1.58; serviço `MulletaFlixNginx` Running/Automatic. Nenhum servidor 12.1.59 foi instalado ou iniciado nesta janela.
- Revi a evidência STRM no relatório, ficha e sessão para declarar com precisão que a busca por nome retornou apenas um arquivo e não permite inferir ausência de todos os 73 IDs no banco. O Range do primeiro ID testado continua confirmado como 404.
- `git diff --check` passou (somente avisos LF/CRLF); `main` permanece cinco commits à frente de `origin/main`, tag `v12.1.59` ausente e 40 paths modificados/não rastreados. A release segue sem preflight publicável e sem commit/tag/push/upload; nenhum gate foi contornado.
- Cleanup dos artefatos portáteis MariaDB foi tentado duas vezes, mas o revisor automático bloqueou a remoção recursiva. A instância foi encerrada e porta 33319 está fechada; conteúdo residual está confinado a `.build/audit-mariadb-e2e-20261126`.
- Mapa técnico conferido: nesta janela apenas documentação e evidência mudaram; nenhuma API, módulo, classe, dependência ou fluxo de runtime foi acrescentado.
### Rechecagem read-only da origem STRM — 2026-11-26 16:18 BRT

- Validei o endpoint de busca com um controle positivo: `/api/files?limit=5` retornou `Elektra (2005).mkv`, ID `6b073179ff310039b81f745a`; busca pelo nome retornou o mesmo item e `HEAD /stream?id=6b073179ff310039b81f745a` respondeu 200 sem transferir conteúdo.
- Seis buscas por `Os Cavaleiros`, `Cavaleiros`, `Zodiaco`, `Zodíaco`, `S01E01` e o nome do episódio retornaram zero registros. `HEAD /stream` para os IDs dos dois STRM `6ac502866057bbddc7469ac6` e `6ac502de6057bbddc7469ac7` continuou retornando 404. O STRM S01E01 tem timestamp 12/10/2026; evidência consistente com links antigos sem registros correspondentes no catálogo que o servidor expõe hoje. Não consultei diretamente o MongoDB nem concluí que todos os 73 itens da temporada estão ausentes.
- O endpoint `GET /health` em 2124 retornou HTTP 200 com HTML do índice do player (100 itens), não um payload de saúde; essa rota cai no handler de índice no código observado e não serve como health check válido para Nebula. A busca e o stream foram testados separadamente.
- Não alterei Nebula, MongoDB ou `.strm`; a correção requer reconciliar IDs/registro-fonte antes de reexecutar FFmpeg em mídia válida.
### Regressão do gerador STRM — 2026-11-26 16:19 BRT

`pytest tests/test_generate_strm.py -q` passou 20/20 com `PYTHONPATH` apontando para `Tools/NebulaPython`, conforme a topologia do módulo. A primeira invocação com Python 3.13 não tinha pytest instalado; o executável pytest do Python 3.10 sem PYTHONPATH também falhou na coleta (`ModuleNotFoundError: generate_strm`); a execução corrigida passou. Nenhum arquivo de biblioteca ou banco foi tocado.
### Revisão crítica e compatibilidade do health check — 2026-11-26 16:27 BRT

- A revisão adversarial confirmou que não há correção segura somente no MulletaFlix para IDs STRM que não resolvem: o gerador usa ID/path atual ao criar o link e a rota busca por caminho/nome ou ID; ela não escolhe outra mídia por título quando a identidade não existe. Os IDs aparecem em dois STRM locais de 12/10; o ID usado como controle corresponde a outro arquivo `.strm` Elektra atualizado em 26/11. Isso não demonstra que os IDs antigos sejam ausentes do MongoDB nem que pertençam às mídias alegadas. Não consultei MongoDB; a identidade requer reconciliação operacional antes de regenerar arquivos.
- O serviço na porta 2124 respondeu 200 para uma busca por um item de controle e o stream atual, enquanto `/health` retornou o índice HTML. A cópia MulletaFlix de `main.py` tem rota e teste explícitos para `/health`, mas o código operacional Nebula está em árvore separada; PID 9520 tem pai usando `D:\Users\Raphael\Documents\Projetos\nebula\.venv\Scripts\python.exe`, distinta da venv nested usada pela tentativa de teste. A divergência de health confirma que `/health` não deve ser usado como prova de saúde do Nebula.
- Teste do health do código MulletaFlix não chegou à coleta: o guard detectou 30 versões de pacotes fora do lock e tentou corrigir automaticamente; depois abortou com divergência de `pymongo`. As gravações identificadas ficaram na venv de desenvolvimento `NebulaFTP-master\.venv`, não na venv pai do processo ativo. Após a tentativa, PID 9520 continuava escutando 2124, `/api/files?limit=1` respondeu 200 e o servidor MFlix respondeu `/health` 200 Healthy. Não repeti o teste para evitar novas alterações no ambiente.
### Preflight explícito da publicação — 2026-11-26 16:28 BRT

Reexecutei `publish-release.ps1 -Tag v12.1.59 -Title "MulletaFlix Server v12.1.59" -NotesPath MulletaFlix-master\docs\releases\12.1.59.md -TargetCommitish HEAD`. O script terminou exit 1 no `git rev-parse v12.1.59^{commit}`: a tag não existe/não aponta para o commit validado. Parou antes de obter credenciais e antes de qualquer chamada GitHub; não houve efeito remoto.
### Build isolado do instalador Windows — 2026-11-26 16:40 BRT

- Pré-condição: stage local `.build/release-12.1.59-stage` contém `MulletaFlix.exe`, `MulletaFlix.dll`, tray e ícone; assembly informa 12.1.59. Compilei o NSIS por `build-mulletaflix-installer.ps1 -StageDirectory .build\release-12.1.59-stage -OutputDirectory .build\audit-installer-output-20261126 -NoCopyToReleaseAssets`; exit 0, executável de 444.470.556 bytes em `.build`.
- SHA-256: `9F3EAE52314FBC803049C075C0026B29F3622E016FE24E15D908BDA82D27B1FE`. Assinatura Authenticode: `NotSigned`. O arquivo preexistente em `dist` foi preservado: timestamp 17/11/2026, SHA-256 diferente (`ACA15AA4557FCB8C3AA440C24C9A9892CAC6E42EAB561DB2A9145DD2FE2418C6`). A opção `-NoCopyToReleaseAssets` impediu cópia para assets da release.
- Isso valida compilação/empacotamento NSIS do stage local; setup não foi executado e não houve validação visual, instalação limpa, upgrade ou rollback em VM. A build não satisfaz assinatura nem gates de publicação.
### Correção de proveniência do stage e build atual da fonte — 2026-11-26 16:47 BRT

Ao comparar os assemblies, confirmei que o stage usado no NSIS tinha `MulletaFlix.Api.dll` de 17/11 (SHA-256 `F6672559740E04238FDC7EB2AB9EC4283A05AE2C25F2C01AAD1EBB49F7ECF520`), diferente da publicação recém-feita da fonte atual. Portanto, o executável NSIS compilado nessa etapa é somente evidência do compilador/script e **não deve ser tratado como instalador candidato das correções atuais**.

Build atualizado da fonte: `dotnet publish .\Jellyfin.Server\Jellyfin.Server.csproj -c Release -r win-x64 --self-contained true --output .build\audit-server-publish-current-20261126 --no-restore -v:minimal` terminou exit 0. Saída isolada contém 563 arquivos (305.987.809 bytes); `MulletaFlix.dll` File/ProductVersion 12.1.59, SHA-256 `E4E7534BE6273107C0411A3653875D881F39C5387B97BD27305CE563DDC579D7`. API atual SHA-256 `A393EBB11C9E7B774FC0157A3BC3415A84F4991C9F9A45191C61BE9B597404BD` (cópia do stage antigo é diferente). Não executei essa publicação nem a empacotei em um instalador; a árvore de trabalho suja impede o builder oficial de certificar um stage proveniente de commit limpo.
### Estado remoto de refs após o build atual — 2026-11-26 16:48 BRT

`git ls-remote --tags origin refs/tags/v12.1.59` não retornou tag; `origin/main` aponta `a4082f166358093c13dd86fcd54960aa4c99a09b`, enquanto HEAD local é `795adef6a2cc3d191118dc01fe10cd6f2f036f7b` (cinco commits à frente; 40 paths modificados/não rastreados). `git diff --check` segue exit 0, com alertas de normalização LF/CRLF. Runtime foi rechecado depois dos builds: MulletaFlix 12.1.58 `/health` 200 Healthy e Nebula ainda escuta em 2124. Nenhuma tag/push/release foi criada.
### NSIS recompilado com payload atual — 2026-11-26 16:59 BRT

- Criei stage temporário `.build/audit-stage-current-source-20261126` a partir dos recursos já empacotados; sobrepus a publicação atual 12.1.59 e os scripts `apply-update.ps1`, `configure-https.ps1` e `duckdns-update.ps1` atuais. Verificação por SHA-256 confirmou os 563 arquivos publicados presentes no stage, sem ausências/divergências; `MulletaFlix.Api.dll` no stage iguala o publish atual (`A393EBB11C9E7B774FC0157A3BC3415A84F4991C9F9A45191C61BE9B597404BD`). O stage contém 1.223.862.390 bytes, incluindo dependências e recursos externos reaproveitados.
- `build-mulletaflix-installer.ps1 -StageDirectory .build\audit-stage-current-source-20261126 -OutputDirectory .build\audit-installer-current-source-20261126 -NoCopyToReleaseAssets` compilou exit 0. EXE: 447.161.093 bytes, SHA-256 `17A3D1A303A0342047C36F0094DA24BA1A1287297E38254DE635F81250047210`; stage version 12.1.59; Authenticode `NotSigned`.
- O EXE está somente em `.build`; não executei instalação nem rodei VM/visual. O arquivo preexistente em `dist` segue com timestamp 17/11/2026 e hash anterior, sem ser sobrescrito. Artefato local sem manifesto de commit limpo, certificado e VM não pode ser publicado como release.
### NSIS do stage íntegro — 2026-11-26 17:05 BRT

Após sincronizar o runtime Python do stage, executei `Assert-StageIntegrity` do packaging contra o stage temporário e a publicação atual: passou. Recompilei o instalador com esse stage: `build-mulletaflix-installer.ps1 -StageDirectory .build\audit-stage-current-source-20261126 -OutputDirectory .build\audit-installer-validated-stage-20261126 -NoCopyToReleaseAssets`, exit 0. EXE 447.162.251 bytes, SHA-256 `33FC7672FB6B50A5099FAA98B06545DA0756CB99B15774E8017917D4C8996140`, Authenticode `NotSigned`, stage 12.1.59, API hash igual ao publish. Runtime permaneceu saudável em 12.1.58; MariaDB de teste encerrado/porta 33319 fechada; `dist` inalterado. Setup segue sem teste visual/VM e o artefato não está assinado nem certificado por manifesto de commit limpo.
### Regressão dos scripts Windows de packaging — 2026-11-26 17:06 BRT

Todos os oito `MulletaFlix-packaging-master/scripts/tests/*.Tests.ps1` passaram em Windows PowerShell 5.1: `Assert-StageIntegrity`, `InstallerLocalization`, regras de firewall, bootstrap rclone, ACL de dados de serviço, backup do stage, opções de build/stage e staging/rollback/health do updater. Localization/DuckDNS passou também separadamente em PowerShell 7. Não executa instalação real; os cenários de updater usam diretórios temporários.

### Auditoria final Windows e preflight de release — 2026-11-26 17:13 BRT

- Revisão read-only confirmou que `.build/audit-server-publish-current-20261126` ainda não foi iniciado; o processo ativo vem de `C:\Program Files\MulletaFlix\Server`. O smoke test disponível instala/inicia serviço e executa uninstall no host. Diretórios temporários não isolam serviços ou bancos locais de produção. Não executei o candidato nem o instalador.
- Pré-condição para o smoke do payload atual: VM Windows descartável sem rota/VPN para produção, sem configuração/credencial/dados do servidor ativo, bancos dedicados dentro da VM e portas livres. A validação realizada permanece build/publish, hash, integridade do stage, compilação NSIS e testes locais registrados acima; startup operacional do payload atual e instalação/upgrade/rollback em VM continuam pendentes.
- Preflight oficial `publish-release.ps1 -Tag v12.1.59 ... -TargetCommitish HEAD` terminou exit 1 em `git rev-parse v12.1.59^{commit}` porque a tag não existe. O script parou antes de obter credenciais ou acessar GitHub; nenhuma operação remota ocorreu.
- Estado rechecado: branch `main`, HEAD `795adef6a2cc3d191118dc01fe10cd6f2f036f7b`, `origin/main` `a4082f166358093c13dd86fcd54960aa4c99a09b`, cinco commits de diferença, 40 paths modificados/não rastreados, tag local/remota ausente. O gate `.agents/rules/mulletaflix-conventions.md` §4.0 veda release intermediária enquanto houver melhorias ativas; o relatório registra 77 itens ativos abertos. CI #700 cancelada, sem chave privada Authenticode disponível e sem VM limpa.
- Nenhum código, serviço, banco, mídia, configuração do host, tag, commit, push ou release foi alterado/criado nesta etapa. `Mapa técnico e estrutura do projeto.md` conferido e permaneceu válido; sem mudança estrutural. Próximo passo: concluir o roadmap ativo e, para homologação operacional, fornecer VM isolada Windows com bancos descartáveis; só então gerar/validar manifesto vinculado a commit limpo, assinar artefato e executar o fluxo oficial.

## Retomada da auditoria Windows — 2026-11-28 04:07 BRT

- Fonte confirmada em `main` e `origin/main`, commit `4fe6b98a8e8d4edb15bb2aef631a18c9cd49370b`, árvore limpa, versão 12.1.59. Runtime instalado segue na 12.1.58; `/health` HTTP 200 e Nginx Running/Automatic. Correções estão no código sincronizado e ainda não foram aplicadas ao servidor instalado.
- `dotnet test MulletaFlix.sln --no-restore -c Release -m:1`: exit 0 sem falhas. Integrações que requerem bancos/serviços indisponíveis foram ignoradas; a saída não fornece agregado global confiável. Portal: 381/381; scripts Windows packaging: 8/8; `dotnet publish` self-contained win-x64 12.1.59: exit 0; `git diff --check`: exit 0 (avisos LF/CRLF).
- Smoke isolado do publish não iniciado: execução do comando foi rejeitada pela política antes do PowerShell, nenhum processo candidato foi criado e as portas de teste permaneceram fechadas. Não houve instalação/upgrade/rollback nem alteração no runtime de produção.
- Release não publicada: a convenção `.agents/rules/mulletaflix-conventions.md` §4.0 mantém o gate fechado com 80 itens ativos abertos. A versão pública verificada segue 12.1.58; não há tag/artefato 12.1.59 elegível nesta etapa. Pendentes: homologação operacional em VM limpa, CI verde e assinatura Authenticode utilizável, além da conclusão do gate ativo.
- O mapa técnico foi conferido e permaneceu estruturalmente válido; sem módulos, APIs, classes ou dependências novas nesta etapa. Ver sessão no vault de 28/11 para limites e evidências detalhadas.

## Regressões focadas após retomada — 2026-11-28 04:13 BRT

- `IntroSkipper.Integration.Tests` com filtro `FFmpegAudioDurationTests|PluginRuntimeConfigurationTests`: 27/27 passaram, sem skips. Cobre o limite de concorrência configurável efetiva entre 1 e 2 e o timeout maior para STRM (mínimo 900 s) com preservação do timeout local. `Jellyfin.Api.Tests` com filtro `AvatarControllerUserAvatarAuthorizationTests|AvatarFileStoreTests`: 41 aprovados, 3 ignorados por cenários específicos de Linux, 0 falhas.
- A primeira tentativa de executar as duas compilações ao mesmo tempo colidiu nos diretórios `obj` compartilhados (CS2012); repetição serial passou. Não executei o E2E que inicia FFmpeg enquanto os dois processos FFmpeg do servidor estavam ativos. A amostra confirmou contagem máxima atual 2; os processos não foram interrompidos.
- Logs em `C:\ProgramData\MulletaFlix\Server\log`: arquivo de 28/11 criado no boot ainda vazio; último log com conteúdo, de 27/11, registra Chromaprint com janela `[0, 0]` em STRM. Isso é evidência histórica do runtime 12.1.58, não prova de falha atual nem valida a correção 12.1.59. Testes unitários confirmam o cálculo de timeout/concurrency; scan de mídia válida pela fonte corrigida continua sem E2E runtime.
- Mapa técnico conferido e mantido: sem mudança de estrutura; registra os fluxos FFmpeg/STRM já existentes. Nenhum processo, configuração, dado ou mídia de produção foi alterado.

## Observação do scan ao vivo — 2026-11-28 04:17 BRT

- Rechecagem read-only confirmou a fila `ChangedItems` em execução desde 04:08; o log registra análise de introduções e créditos. Dois processos `ffmpeg.exe` são filhos do PID do servidor e os comandos contêm a saída Chromaprint. Em uma amostra de 12 s os PIDs mudaram e o uso de CPU avançou, consistente com subprocessos FFmpeg sendo concluídos/substituídos na fila. A contagem ficou em 2.
- Desde o boot de 28/11, a busca no log não encontrou `Chromaprint returned 0 points`; isso é sinal melhor que os avisos históricos de 27/11, mas ainda não prova que segmentos foram identificados e persistidos. Runtime observado continua 12.1.58; não valida a fonte 12.1.59.
- Não pausei nem iniciei tarefas e não consultei/alterei banco ou biblioteca. Próxima evidência necessária para fechar o E2E: observar a conclusão da fila autorizada e confirmar segmentos persistidos via UI/API autenticada ou banco de teste, mantendo concorrência em 2; em seguida, repetir no candidato isolado.

## Correção do runner macOS no CI — 2026-11-28 04:23 BRT

- Evidência pública: CI #701 no commit `4fe6b98` cancelada em 5 s. Anotações do GitHub mostram jobs não iniciados por bloqueio de cobrança da conta e dois erros de configuração `macos-14-arm64-us-default` nos jobs macOS. A fonte `.github/workflows/ci.yml` usava `macos-14` em `server-restore-macos-startup` e `ios-build-test`.
- Consulta à documentação oficial atual de runners do GitHub confirmou `macos-15` como label macOS suportado e não listou `macos-14`. RED: o teste novo `uses currently supported GitHub-hosted macOS runners for macOS jobs` falhou com as labels antigas. GREEN: ambas as jobs passaram a usar `macos-15`; teste focado 1/1 e suíte portal completa 382/382. `git diff --check` exit 0. Não foi possível confirmar execução hosted porque a conta está bloqueada por cobrança; ajuste de YAML não remove esse bloqueio.
- Arquivos: `.github/workflows/ci.yml`, `portal-site/tests/homologacao.test.mjs`, relatório desta homologação, status do portal, ficha e sessão no vault. Mapa técnico conferido: nenhuma alteração em módulos, APIs, classes, dependências ou fluxos do servidor; somente labels de runner do CI.
- Referências: run `https://github.com/raphaelpera85/mulletaflix-completo/actions/runs/46119130817`; documentação de runners `https://docs.github.com/en/actions/reference/runners/github-hosted-runners`.
- O release workflow segue pendente: gate §4.0 com 80 ativos; CI deve ser reexecutada após regularizar a cobrança e a troca das labels chegar à branch remota. Nenhuma release foi criada.

#### Configuração observada no runtime — 2026-11-28 04:23 BRT

Leitura restrita de `C:\ProgramData\MulletaFlix\Server\plugins\configurations\IntroSkipper.xml` confirmou `MaxParallelism=2` e `ScanTimeoutSeconds=300` no runtime 12.1.58; a fonte 12.1.59 impõe mínimo 900 s apenas para `.strm` mesmo quando o valor salvo fica em 300. Não alterei XML do runtime durante scan. Dois FFmpeg ativos e zero avisos de fingerprint vazio no log corrente até 04:17, mas resultado de segmentos ainda não verificado.

## CI após sincronizar a correção do runner — 2026-11-28 04:26 BRT

- O commit `034e14e8` foi enviado para `origin/main`, corrigindo as duas labels macOS e incluindo o teste de regressão/documentação. O run público de CI `#702` (`46121187512`) foi iniciado por esse push e terminou em failure após 5 s; os 16 jobs não iniciaram porque a conta GitHub permanece bloqueada por cobrança. As anotações agora mostram somente aviso de fila/capacidade para macOS ARM e nenhum erro da label inválida. Portanto a correção de configuração foi confirmada no run, mas não houve execução de testes hospedados.
- Após o push, `HEAD` e `origin/main` foram confirmados em `034e14e8`; nenhuma alteração de código do servidor ou update de produção ocorreu. Release segue impedida pelo §4.0 e por falta de CI verde, assinatura e homologação em VM.

## Persistência confirmada durante o scan — 2026-11-28 04:34 BRT

- Leitura SQL `START TRANSACTION READ ONLY` no banco `mulletaflix_introskipper` (sem exibir ou gravar credenciais) mostrou linhas `Segments` criadas em 28/11: Mode 0/Introduction: 10; Mode 1/Credits: 24; Mode 3/Recap: 2. Totais por modo no momento: 1.335 intros, 876 créditos, 33 previews, 121 recaps. O maior `CreatedAt` observado foi 07:34:25 UTC (04:34:25 BRT). O transaction read-only foi fechado com COMMIT. Não acessei nomes/IDs de mídia via SQL.
- Correlacionado com o log da fila `ChangedItems` desde 04:08, análise das temporadas observadas até 04:27 e dois processos FFmpeg filhos do servidor com saída Chromaprint, limite de concorrência 2. O banco recebeu novas intros, créditos e recaps enquanto a análise seguia. Isso prova atividade e persistência no runtime instalado 12.1.58, sem atribuir os novos segmentos a um episódio específico.
- O resultado melhora a evidência do scan real, mas não homologa a candidata 12.1.59: a configuração ativa ainda está em timeout 300 s; o candidato aplica piso 900 s para STRM. Não reiniciei/instalei candidato, não alterei configuração, segmentos, catálogo ou biblioteca. E2E autenticado por item e playback da intro continuam sem verificação.
- CI #703 (`46121436641`, commit `a366be6`) terminou failure em 5 s; os jobs novamente não iniciaram sob bloqueio de cobrança da conta GitHub. Release pública mais recente verificada continua 12.1.58; gate §4.0 conserva 80 itens ativos.
