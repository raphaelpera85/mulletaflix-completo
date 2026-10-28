# Nebula STRM Downloader

## Rechecagem de falha TLS do provedor — 2026-10-27 18:19 BRT

- O log anexado registra 95 falhas de download, todas com `TLSV1_UNRECOGNIZED_NAME`; o scan iniciou e encontrou títulos, mas não concluiu esses downloads.
- Dois arquivos `.strm` correspondentes a itens do log usam o host `cn53.org`. Handshakes TLS sem caminho ou credenciais para esse host passaram nesta máquina em .NET TLS 1.3 e Python 3.13/OpenSSL 3.0.16. Isso não valida as URLs completas, redirects, nem o runtime instalado que produziu o log.
- Regressão local do downloader Python 3.10: `test_strm_downloader.py` + `test_strm_downloader_helpers.py`, 99 aprovados, 0 falhas.
- Não foi identificada causa no código do downloader: a falha pode ocorrer num redirect do provedor ou no runtime/rede de produção. Não desabilitar validação TLS nem fixar SNI sem conhecer o host final e confirmar o contrato do provedor.
- Pendente: obter do runtime afetado apenas o host final após redirects, sem caminho, query ou credenciais; então reproduzir handshake e selecionar correção compatível. Sem alteração de código, instalação ou release nesta rechecagem.

## Worker órfão mantém portas FTP/stream — 2026-10-18 13:28 (-03)

- Nova inspeção read-only do host encontrou PID Python 6080 ouvindo em 2121 e 2123 desde 16/10 21:16:16; PID pai 19764 não existe. Serviço `MulletaFlixServer` não está instalado/registrado e API 8096 não responde. A identidade binária do processo não pôde ser lida, mas o timestamp coincide com o worker mostrado no log e com os binds em conflito. Isso confirma um worker órfão; não prova que ele seja a única causa de ausência de downloads.
- O log mostra o worker em `STREAM_ONLY` desativando fila/workers de upload; tentativa posterior de modo completo falha ao vincular 127.0.0.1:2121 com WinError 10048. O runtime não chega a ativar os workers nessa tentativa.
- Correção na fonte: o host TCP local já usado por cache lease agora aceita heartbeat autenticado. `main.py` consulta o lease a cada 5 s; após 3 falhas seguidas, sinaliza shutdown e libera FTP/HTTP. O contrato usa o socket/token efêmero existente; nenhum endpoint público ou dependência nova.
- Verificações: implementação C# 1.499 aprovados/63 ignorados/0 falhas; NebulaPython 595/595. Mongo real, CI remota, instalação, Downloader no runtime e transferência seguem não verificados. O worker PID 6080 roda fonte antiga sem watchdog e permanece intocado para não interromper possíveis streams. Release não autorizada/validada nesta rodada; requer janela controlada para encerrar o órfão e instalar a candidata.
- Homologação publicada no Git do portal: commit `f223be2963bf1b6beeecf4efb88c24a54bec2db8`; Vercel Production `READY` (`dpl_6PBRGHmru4SUbj7CaFqiUzSHdEQz`). O alias público `/homologacao-status.json` confirmou HTTP 200, `generatedAt=2026-10-18T13:30:00-03:00` e o evento desta auditoria como `in_progress`. Testes do portal 105/105. O URL individual do deployment exige autenticação; confirmação foi feita pelo alias público.

## Revalidação focal pós-watchdog — 2026-10-18 13:35 (-03)

- Reexecução local: `NebulaPlaybackCacheTests` Release 59/59; `test_main_lifecycle.py` com Python 3.10 9/9. Revisão do caminho de shutdown confirmou que `stop_event` leva ao `finally` que fecha FTP/HTTP, control plane, tasks e bots.
- Snapshot read-only continua com PID Python 6080 escutando em 2121/2123; iniciado em 16/10 21:16:16, pai 19764 ausente. Serviço MulletaFlixServer e API 8096 seguem indisponíveis. PID não foi encerrado.
- Revisão cavecrew independente indisponível por limite de agentes. Mapa técnico rechecado; protocolo/fluxo não mudou nesta rodada além do watchdog já mapeado. Nenhuma produção alterada; download real, instalação, Mongo e release pendentes.
- Homologação publicada no Git do portal: commit `77d93202025d0d8b7c5f4a1f684f4e729b4b4fc5`; Vercel Production READY (`dpl_Dj3CUvw9Ej9DW1mymew6Yi8WExG8`). Alias público `/homologacao-status.json` respondeu HTTP 200, timestamp 13:35 (-03), evento `in_progress` e novos testes confirmados.

## Cobertura da rota alternativa stream_service — 2026-10-18 13:39 (-03)

- Revisão encontrou a rota de fallback `stream_service.py`: ela é lançada pelo mesmo `StartPythonStreamServiceAsync`, importa `main` para playback mas aguardava `serve_forever()` sem chamar o watchdog. Um servidor pai encerrado abruptamente poderia deixar o HTTP worker órfão.
- Correção: `stream_service.py` agora lê o lease local configurado, executa `_watch_server_owner` junto ao loop HTTP, cancela e aguarda as tasks no encerramento. Sem porta/token de lease, preserva o comportamento legado.
- Testes de lifecycle cobrem perda do supervisor, encerramento normal do servidor e compatibilidade sem lease. `test_main_lifecycle.py`: 12/12; suíte NebulaPython 598/598. C# `NebulaPlaybackCacheTests` Release 59/59.
- Sem runtime/produção alterado. PID legado 6080 continua sem watchdog e permanece preservado; serviço MulletaFlixServer ausente, API 8096 indisponível. Transferência real, instalação, Mongo integration e release pendentes.
- O mapa técnico foi atualizado para documentar ambas as rotas de execução. Homologação publicada no Git do portal no commit `ae1fa054fe1d331f087e5f3eca8af6fac8f7c12b`; Vercel Production READY (`dpl_79TCLc47WriiebBiWvz7C8pJPxss`). Alias público HTTP 200 confirmou `generatedAt=2026-10-18T13:39:00-03:00`, o evento `in_progress` e evidência dos testes/fallback.

## Processo legado tem conexões ativas — 2026-10-18 13:42 (-03)

- Novo snapshot de PID 6080 mostrou 37 conexões TCP estabelecidas, incluindo cinco para Mongo local, 28 conexões HTTPS externas e um par de sockets locais. Isso indica possível atividade real; não prova reprodução ou Downloader, mas contradiz tratá-lo como worker inativo.
- A definição segura é “processo Python sem pai conhecido, ainda com conexões ativas”. Mantido sem alteração para evitar interromper tráfego; não encerrar sem janela/confirmar que não há uso.
- O log ainda mostra `STREAM_ONLY` desativando upload workers e o modo full falhando ao vincular FTP 2121. Downloader não comprovado; API 8096 e serviço principal seguem ausentes.
- Homologação atualizada no Git do portal, commit `3c18e5231d8ddc26d54b61bfed5106770fe421dc`; Vercel Production READY (`dpl_6PdkBZd7ufVTC9FxTfxtg59KaDAj`). Alias público respondeu HTTP 200 com `generatedAt=2026-10-18T13:42:00-03:00`, status `in_progress` e evidência das conexões ativas.

## Regressão ampla do servidor — 2026-10-18 13:14 (-03)

- `dotnet test MulletaFlix.sln --configuration Release --no-restore --filter "FullyQualifiedName!~Integration"` terminou com exit 0. `Jellyfin.Server.Implementations.Tests`: 1.498 aprovados, 63 ignorados, 0 falhas; `Jellyfin.Server.Tests`: 73 aprovados, 1 ignorado; `Jellyfin.Api.Tests`: 730/730; `Jellyfin.Providers.Tests`: 777/777. Demais projetos executados sem falhas reportadas. Integrações foram excluídas pelo filtro; ignorados não contam como aprovação.
- `py -3.10 -m pytest tests -q`, dentro de `Tools/NebulaPython`, passou 592/592. Tentativa inicial no diretório pai não coletou testes por resolução de imports; execução corrigida no diretório do projeto passou.
- Sem mudanças de runtime nesta rodada. API 8096 indisponível no snapshot read-only; testes locais não validam instalação nem transferência. Integrações Mongo, CI remota, smoke test e release permanecem pendentes. Mapa técnico conferido, sem mudança estrutural.
- Homologação publicada no Git do portal, commit `9451bf5e146586c6e19b0dd2cc9828ca09805e2e`; Vercel Production `READY` (`dpl_DUU8P8dKSHWtVxgm32ZGM9Sj8ZF8`). Testes do portal 105/105; endpoint público HTTP 200 confirma evidências e status `in_progress`.

### Reauditoria do serviço instalado — 2026-10-18 13:18 (-03)

- Pasta do servidor, `MulletaFlix.exe` versão de produto 12.1.21 (mtime 16/10) e `nssm.exe` existem em `C:\Program Files\MulletaFlix\Server`; serviço `MulletaFlixServer` não está registrado. `MulletaFlixNginx` está ativo, mas API 8096 não responde. Consulta de 14 dias ao Service Control Manager encontrou evento de criação do Nginx em 05/10 e nenhum evento de criação MulletaFlixServer; isso não prova como/por que o serviço sumiu.
- O NSIS atual contém os comandos NSSM de registro e início; `Test-CleanInstaller.ps1` verifica serviço `Running` e `/ready`. Não executei o teste: ele instala serviço do Windows e requer VM/ambiente isolado. GitHub CLI não está autenticada, então CI remota não pôde ser consultada.
- Sem alteração de produção. Próximo gate é smoke test de instalação em VM isolada, diagnóstico da ausência do serviço e download STRM real. Release do servidor permanece bloqueada. Mapa técnico sem mudança estrutural.

## Gate de compatibilidade do worker no pacote — 2026-10-18 11:14 (-03)

- A instalação inspecionada contém `strm_downloader.py` de 08/10, sem `[STRM-WORKER-MONGO-READY]` nem `[STRM-SCAN-STARTED]`; a fonte atual emite o marcador Mongo que `StartPythonDownloaderAsync` exige para considerar o processo pronto. Isso torna a divergência fonte/produção uma causa provável para o botão retornar falha/timeout, mas o log anexado não registra a chamada manual nem o stdout do worker e não prova causalidade sozinho.
- O gate `Assert-StageIntegrity` agora rejeita a fonte do downloader se ela não contiver o contrato `[STRM-WORKER-MONGO-READY]`, além de já comparar o hash da cópia staged com a fonte. Regressão valida stage correto, cópia desatualizada e worker idêntico sem o marcador.
- Validação local: teste PowerShell de integridade de stage passou; `NebulaDownloaderStartupTests` + `NebulaHostedServiceConfigurationTests` Debug 47/47; `git diff --check` sem erros. Não foram executados download/scan real, instalação, endpoint manual ou restart.
- A API local está indisponível e os listeners/processos em produção foram preservados. A correção em fonte impede novo pacote incompatível, mas não atualiza o servidor já instalado; requer build/instalação em janela controlada e smoke test do botão + primeiro título STRM. Sem release nesta rodada.
- Mapa técnico conferido contra o fluxo existente `NebulaFtpManager.StartDownloaderAsync` → `StartPythonDownloaderAsync` → `strm_downloader.py` e o gate de stage; nenhum módulo/API/dependência ou topologia runtime nova. Registro de sessão: `2026-10-18-MulletaFlix-nebula-downloader-package-readiness-contract.md`.

## Propriedade do sincronizador durante falha de startup — 2026-10-18 11:05 (-03)

- `StartEnvioAsync` pode reutilizar o sincronizador Supabase já iniciado pelo modo streaming. A rotina de rollback agora descarta esse serviço somente quando foi criado pela tentativa de startup que falhou, evitando parar a sincronização de um runtime anterior.
- Testes de startup/HostedService/cleanup: 57/57; suíte completa de Implementations: 1.496 aprovados, 63 ignorados, 0 falhas. Ocorrências similares em `StopEnvioAsync` e descarte do manager permanecem intencionais.
- A alteração não resolve o runtime de produção: a API está indisponível e o listener antigo continua ativo. Sem teste de promoção em runtime, download real ou release.
- Atualização de homologação publicada no portal Git: `Mulletaflix-Portal` commit `9e1484aada74ff807ef757719b8f4d7a598be019`; Vercel Production `READY`, JSON público HTTP 200 com status `in_progress`.

## Reanálise do log e validação ampliada — 2026-10-18 10:44 (-03)

- O anexo (16–17/10) confirma runtime `Somente Streaming` em 16/10, que desativa feeder/fila/workers, e falha posterior de bind FTP `127.0.0.1:2121` (WinError 10048) ao iniciar modo Envio; há retries do mount após o helper indicar outra montagem. Não há chamada `StartDownloader`, marcador STRM, item enfileirado ou transferência no trecho.
- A interpretação permanece limitada: o log demonstra o conflito de porta e que aquela inicialização não ativou upload nesse processo; não demonstra que a ação separada do Downloader tenha sido chamada ou falhado.
- Validação da fonte: suíte Python 3.10 `Tools/NebulaPython/tests` 592/592; `.NET` Release da solução com filtro `FullyQualifiedName!~Integration` terminou com exit 0; `Jellyfin.Server.Implementations.Tests` reportou 1.496 aprovados, 63 ignorados, 0 falhas; startup Nebula focal (Downloader + HostedService) 47/47. As falhas de coleta Python na primeira tentativa foram apenas por diretório de trabalho incorreto; a execução correta passou.
- Nenhum serviço, processo, configuração, disco, mídia ou banco de produção foi alterado. Não houve início de worker nem transferência real. A homologação de T2.1 e a release continuam pendentes de runtime candidato em janela segura, readiness/scan comprovados e transferência limitada.
- Evidência publicada no portal `Mulletaflix-Portal`, commit `2218a32`; endpoint público HTTP 200 confirmou o evento `server-nebula-log-validation-2026-10-18-1044` como `in_progress`.

## Log de download sem início — 2026-10-18 10:33 (-03)

- O log anexado é histórico (16–17/10): no primeiro trecho, o runtime inicia em `Modo Somente Streaming`, desativando feeder, fila e workers; em tentativas posteriores de `Modo Envio`, o bind `127.0.0.1:2121` falha com WinError 10048 e o mount entra em retries. Não há chamada `StartDownloader`, readiness/scan STRM, título em fila ou transferência.
- Rechecagem local read-only: API MulletaFlix em `127.0.0.1:8096` recusou conexão; processo Python PID 6080 mantém 2121/2123 em LISTEN desde 16/10, com processo pai ausente. Pode ser worker órfão, mas também pode estar servindo playback; foi preservado. Nenhuma ação de produção foi executada.
- Testes Release focados C#: 54/54. Python não executou porque `pytest` não está instalado no Python 3.14 disponível. Testes não validam instalação nem transferência real.
- Conclusão: o log aponta início em modo que não baixa e conflito de runtime/porta, não falha comprovada do endpoint do Downloader. Para retestar, primeiro é necessária janela segura para resolver o listener; em seguida validar start/readiness/scan e uma transferência em raízes isoladas. T2.1 e release permanecem pendentes.
- Homologação publicada no portal: Git `Mulletaflix-Portal` commit `030b0bd`; JSON público retornou HTTP 200 e contém o evento `server-nebula-download-log-followup-2026-10-18-1033`.

## Interface em espera e versão de produção — 2026-10-18 10:26 (-03)

- A página autenticada do Nebula mostra `Download: Parado`, botão `Iniciar Downloader STRM` habilitado e `Aguardando início do downloader`. O handler da fonte envia `POST NebulaFtp/Actions/StartDownloader`; o controller retorna o resultado de `StartDownloaderAsync`. O log fornecido não contém evidência de que a ação tenha sido chamada.
- GET local somente leitura em `/System/Info/Public` respondeu HTTP 200 e confirmou a versão instalada 12.1.21, anterior à fonte candidata 12.1.24. `python.exe` PID 6080 mantém listeners 2121/2123; não foi encerrado para preservar playback potencial.
- `NebulaDownloaderStartupTests` + `NebulaHostedServiceConfigurationTests` Release: 47/47. A ação manual usa todas as raízes `MonitorPaths`, atualmente incluindo `D:\midias`; nenhum start foi disparado para evitar scan/download amplo. Não houve alteração ou restart produtivo, Mongo/Telegram ou transferência real.
- Não há correção adicional reproduzida nesta rechecagem: o caminho UI/API existente está ligado na fonte. A pendência é provar que a ação chega ao runtime atualizado e completar transferência limitada. T2.1 e release seguem abertos. Homologação publicada no portal: evento `server-nebula-download-ui-runtime-recheck-2026-10-18-1026`.
- Publicação confirmada: portal Git `Mulletaflix-Portal` commit `1bc4c163b63e82b76b66bb09cb9f0d96256a089f`; Vercel Production `READY` (`dpl_433VYMHVNvegP941AjDYza2EZymZ`). GET público do JSON HTTP 200 confirmou o evento e cobertura 329/398, com 65 pendentes e 4 em andamento.

## Handshake antes da descoberta de raízes — 2026-10-18 10:06 (-03)

- `StartPythonDownloaderAsync` agora reconhece `[STRM-WORKER-MONGO-READY]` após `MediaValidator.refresh_cache(force=True)` e antes da enumeração da biblioteca; `[STRM-SCAN-STARTED]` permanece como marcador de progresso. O timeout de 60 s não precisa aguardar a descoberta completa de pastas.
- Testes Release: `NebulaDownloaderStartupTests` 30/30; Python 3.10 `tests/test_strm_downloader.py` 17/17. Sem evidência de teste com serviços reais ou transferência completa.
- Publicação de homologação: Git `Mulletaflix-Portal` commit `7482a90ea98ca76a2cb34771be0684910084ac07`; Vercel Production `READY` (`dpl_4JqgM5VikACtE6tq6bZHWCyAJcME`); endpoint público do status HTTP 200 confirmou o evento da rodada.
- Produção local consultada por GET está em 12.1.21. Nenhum start, download, restart ou instalação foi acionado; o gate T2.1 e a release continuam pendentes de smoke test limitado em ambiente isolado/janela segura.

## Auditoria do runtime instalado — 2026-10-18 09:12 (-03)

Consulta somente leitura confirmou a instância local em `12.1.21` enquanto a fonte do servidor é candidata `12.1.24`. O `strm_downloader.py` instalado tem SHA-256 `3D41354345B79EAB19CB7A1FE0C01A02D0579130F18301D156CAF1C3221664C5` e não contém os marcadores de worker/scan; a fonte tem SHA-256 `2A671AFCA11BF06B48AAAEAA0DA49032F0A7FAD96F4963F6FEFEBD08B0B9CDBA` e os contém. Portas `2121` e `2123` estão abertas pelo mesmo processo Python; a consulta não pôde ler sua linha de comando. Não foram enviados comandos, encerrados processos, alteradas configurações ou iniciados downloads. Isso prova divergência de instalação, não causa da colisão 2121 nem sucesso/falha de transferência. Atualizar runtime e observar transferência requer janela de produção autorizada. Andamento publicado no portal, commit `4caad5d419f49a1c8697f2125c45eb8921465444`; Vercel Production `READY`, endpoint público HTTP 200 confirmou o evento como `in_progress`.

## Critério de prontidão do downloader — 2026-10-18

`[STRM-WORKER-READY]` informa apenas que o processo Python iniciou; é emitido antes da validação do índice Mongo e não pode representar sucesso de `StartDownloaderAsync`. A prontidão agora exige `[STRM-WORKER-MONGO-READY]`, emitido após a validação do índice e antes da descoberta das raízes STRM. Essa etapa pode ser demorada em bibliotecas grandes ou montadas remotamente; não deve consumir o timeout de startup nem encerrar um worker válido. `[STRM-SCAN-STARTED]` continua indicando que a varredura alcançou uma raiz e é evento de progresso, não gate de inicialização. Validação local: C# `NebulaDownloaderStartupTests` 30/30 e Python `test_strm_downloader.py` 17/17; isso valida o contrato em teste, não um download ou instalação em produção.

## Alcance da ação manual StartDownloader — 2026-10-18 07:04 (-03)

Inspeção estática da rota atual: `NebulaFtpController.StartDownloader` encaminha a chamada diretamente a `StartDownloaderAsync`; não consulta `NebulaFtpConfiguration.Enabled`. Esse flag controla o autostart no `NebulaHostedService`, não bloqueia a ação manual. A tela também deixa o botão habilitado quando `IsDownloaderRunning` é falso.

O método manual valida todas as raízes configuradas em `MonitorPaths`, prepara dependências/runtime e Mongo, pode restaurar Mongo do Supabase quando vazio e inicia o Python passando todas as raízes por `--sources`, com `--watch`. Portanto, iniciar o endpoint não equivale a baixar um único título: o worker pode indexar as raízes inteiras e manter o monitoramento; o comportamento subsequente de fila depende da implementação do worker. Não foi feita chamada HTTP nem executado worker. Para teste estritamente unitário de um título, primeiro deve-se configurar um `MonitorPaths` isolado ou usar ambiente descartável, validar que não compartilha Mongo/pool/FTP e só então executar a transferência de teste.

Testes anteriores desta rodada continuam sendo de fonte: C# 51/51 Debug, Python 3.10 23/23 e portal 82/82. Sem prova de instalação/readiness/transferência em produção; T2.1 e gate global de release continuam abertos. Portal de homologação atualizado no Git (`Mulletaflix-Portal`, commit `e32e4e9`); GET público HTTP 200 confirmou a evidência em 07:04. Mapa técnico conferido: fluxo existente, sem mudança estrutural. Sessão [[Sessões/2026-10-18-MulletaFlix-nebula-manual-route-scope-0704]].

## Revalidação do autostart e do worker — 2026-10-18 06:50 (-03)

O estado observado no host tem `Enabled=false` em `nebulaftp.xml`, e a raiz configurada de `MonitorPaths` existe. O log da inicialização do servidor em 17/10 também diz que o serviço estava desabilitado. Isso bloqueia o autostart de `NebulaHostedService`; não prova falha no endpoint manual `POST /NebulaFtp/Actions/StartDownloader`, que invoca `StartDownloaderAsync` independentemente desse gate. Não foi feita chamada manual nesta rechecagem.

Não havia listener nas portas FTP 2121 e HTTP 2123 no snapshot de processos/conexões. O arquivo colado continua sendo o mesmo log histórico de 16–17/10 e relata WinError 10048 e retries de mount; sem evento atual, readiness/scan STRM, item em fila ou transferência. A fonte passou novamente 51 testes focados C# (Debug) e 23 testes Python 3.10 (lifecycle + downloader). Isso não valida produção.

Nenhuma configuração, serviço, processo, mount, banco ou mídia foi alterado; nenhum download real foi iniciado. Para homologar a ação manual, obter autorização para testar um único item e registrar o booleano de `StartDownloader`, status e logs do worker. Para autostart, reativar NebulaFTP pela configuração administrativa. Não confundir as duas ações; a habilitação global também inicia componentes de Envio/FTP. T2.1 e o gate de release permanecem abertos.

## Rechecagem do mesmo log e dos gates focados — 2026-10-18 06:32 (-03)

O arquivo recebido novamente tem 54.444 bytes, 516 linhas e timestamp de 17/10/2026 19:52:55; é o mesmo log histórico já analisado, não uma nova amostra do runtime. Repete modo Somente Streaming, depois conflito de bind `127.0.0.1:2121` (WinError 10048) e retries do helper de montagem. Não contém chamada `StartDownloader`, readiness/scan STRM, título em fila ou transferência. Nenhuma correção adicional pode ser inferida desse anexo.

Revalidação da fonte atual: build Release do projeto de testes de Implementations concluído com 0 avisos/erros; testes `NebulaDownloaderStartupTests` 27/27 e `NebulaHostedServiceConfigurationTests` + `NebulaMountRuntimeHealthTests` 24/24; Python 3.10 `test_mount_drive_n.py` 3/3; `git diff --check` sem erros (apenas avisos preexistentes de CRLF). Um primeiro comando .NET que combinava filtros ficou sem progresso visível e foi interrompido; a repetição com classes separadas passou. Nada foi instalado, reiniciado ou executado no runtime de produção; nenhum download real foi iniciado. O teste valida fonte, não operação produtiva. Sem nova release.

O mapa técnico foi conferido e permanece estruturalmente igual: `NebulaHostedService`/`NebulaFtpManager` e `mount_drive_n.py`, sem módulo, endpoint ou dependência novos. T2.1 continua pendente até uma janela segura, identificação do listener e smoke test real limitado a um título.

O andamento foi publicado no Git do portal (`Mulletaflix-Portal`, commit `7ef835c`, `main`); endpoint público confirmou `generatedAt`/`updatedAt` em 06:32 e o evento `in_progress`.

## Rechecagem do log de download — 2026-10-18 06:18 (-03)

O anexo recebido contém 516 linhas da operação de 16–17/10. Ele mostra `main.py` em “Somente Streaming” (que desativa feeder, fila e workers de upload), depois uma tentativa de modo Envio que falha ao abrir FTP `127.0.0.1:2121` com WinError 10048, seguida por retries de montagem. Não há chamada registrada de `NebulaFtp/Actions/StartDownloader`, `[STRM-WORKER-READY]`, `[STRM-SCAN-STARTED]`, título na fila ou transferência. Portanto, o log não permite afirmar que o Downloader STRM foi acionado nem localizar uma falha interna dele. Também há 248 adiamentos do backup Supabase por uploads Telegram ativos; o trecho não prova se essas leases correspondem a transferências efetivamente em andamento.

Na fonte atual, `NebulaHostedService.StartComponentsAsync` tenta Downloader mesmo quando Envio falha, e o fluxo de startup valida readiness do worker. Regressões focadas recompiladas nesta rechecagem passaram: C# Release `NebulaDownloaderStartupTests`, `NebulaHostedServiceConfigurationTests` e `NebulaMountRuntimeHealthTests`, 51/51; Python 3.10 `test_mount_drive_n.py`, 3/3. Isso valida a fonte, não o runtime instalado.

Inspeção somente leitura em 18/10: `/System/Info/Public` HTTP 200 reporta produção 12.1.21. `python.exe` antigo continua escutando HTTP 2123; FTP 2121 não aparece em estado Listening nesta coleta. Processo preservado. Nenhum POST, worker, instalação, restart, mount ou transferência foi iniciado. Não há release nem smoke test produtivo; T2.1 continua pendente até janela segura, instalação aprovada e transferência controlada de um item. O mapa técnico foi conferido e permanece estruturalmente correto; não houve mudança de fluxo nesta rechecagem. Portal de homologação atualizado no Git `Mulletaflix-Portal`, commit `886c253`; endpoint público HTTP 200 confirmou evento `in_progress`.

## Estado real do runtime e retries da montagem N: — 2026-10-18 06:08 (-03)

O novo log registra que o helper Python de montagem encerrou repetidamente com código 0 enquanto o serviço não considerava N: acessível. Na fonte anterior, `mount_drive_n.py` retornava 0 apenas porque `Path('N:\\')` existia na sessão do helper; isso não provava que o serviço MulletaFlix podia usar a montagem. O fluxo de `MountDriveNAsync` também confiava em `_isEnvioRunning` para decidir se deveria iniciar FTP, embora essa flag possa sobreviver à saída inesperada do worker Python. O log ainda mostra tentativas de bind em `127.0.0.1:2121` com WinError 10048.

Correção: a decisão de recuperar o runtime verifica somente `NebulaFtpServerHost.IsRunning` ou o processo Python ainda vivo. Se a unidade preexistir antes do helper, ele retorna código 4 em vez de alegar sucesso; o manager pausa retries automáticos nos códigos 0 e 4 quando N: continua inacessível, evitando um ciclo de tentativas contra uma unidade visível em outra sessão. Saída 3 significa que outro helper já possui o lock global de montagem; retries automáticos também são pausados nesse caso, pois reiniciar a cada três segundos apenas cria novas tentativas que falham no mesmo lock. Após a tentativa proprietária terminar, a montagem pode ser solicitada novamente explicitamente. Falhas reais continuam sujeitas ao retry existente.

### Retry concorrente pausado após lock pertencente a outro helper — 2026-10-18 08:02 (-03)

O log de 17/10 mostra repetidas chamadas de montagem N: retornando imediatamente porque outro helper detinha o lock global. A política anterior classificava o código de saída 3 como retryável, então cada nova tentativa relançava um helper que falhava no mesmo lock. Agora código 3 pausa retries automáticos, assim como saídas 0 e 4; falhas reais continuam podendo ser repetidas. O log é antigo e não prova estado atual do host nem download STRM.

`NebulaMountRuntimeHealthTests` passou 7/7 após recompilação Release; as classes `NebulaMount*` passaram 18/18 sem recompilar. `git diff --check` passou. Não houve operação em produção, identificação do processo externo, restart, mount, instalação ou transferência. O owner do lock precisa terminar antes de um novo mount explícito; homologação e release permanecem pendentes.

Regressões: `NebulaMountRuntimeHealthTests`, startup e ownership C# Release compilaram e passaram 29/29 após corrigir o namespace do teste. `Tools/NebulaPython/tests/test_mount_drive_n.py` passou 3/3 com Python 3.10. O teste `--no-build` anterior executou um assembly stale e não conta como evidência; a repetição com recompilação validou a fonte atual. O log continua sem `[STRM-WORKER-READY]`, `[STRM-SCAN-STARTED]`, título na fila ou transferência; não comprova que a função de download tenha sido iniciada. Inspeção somente leitura do host confirma versão 12.1.21 e o mesmo processo Python iniciado em 16/10 ainda escutando 2121/2123; ele foi preservado, não encerrado. Nenhum processo, serviço, configuração, mount, instalação, banco ou mídia de produção foi alterado. Identificação/resolução operacional do listener, instalação e smoke test real continuam pendentes.

## Proteção do estado Enabled contra autosave — 2026-10-18 04:03 (-03)

Os salvamentos silenciosos de campos secundários no painel Nebula agora preservam o valor `Enabled` já carregado do servidor. Apenas o salvamento explícito da configuração usa o checkbox e aciona a confirmação existente para habilitar/desabilitar o serviço. Isso evita que um autosave de outro controle inicie ou interrompa Envio/Downloader sem consentimento explícito.

Regressões: `tests/Jellyfin.Api.Tests/NebulaConfigPage.test.mjs` passou 4/4; sintaxe de `configPage.js` validada com Node 26.7.0; `test_mount_drive_n.py` passou 3/3 com Python 3.10; testes Release `NebulaDownloaderStartupTests` + `NebulaHostedServiceConfigurationTests` passaram 39/39. O teste foi localizado na árvore de testes versionada fora das pastas ignoradas pela regra genérica `web/`. A tentativa Python 3.12 não foi executada porque o runtime não está instalado. GET público local `/System/Info/Public` respondeu HTTP 200 e confirmou servidor 12.1.21; nenhum estado foi alterado. O log enviado nesta sessão é de 16–17/10 e não contém `[STRM-WORKER-READY]`, `[STRM-SCAN-STARTED]`, item em fila ou transferência. O fluxo de download real segue pendente de homologação.

## Rechecagem do log: runtime antigo e colisão FTP repetida — 2026-10-18 03:52 (-03)

O log anexado cobre 16–17/10. Às 21:14, o fluxo inicia `main.py` em Somente Streaming e o mount N: informa sucesso. Às 19:49 e 19:50, novas inicializações em modo Envio falham no bind `127.0.0.1:2121` com WinError 10048. Depois, o helper informa que outra montagem está em execução; o código 0 do helper não comprova mount pronto. O trecho não contém marcadores `[STRM-WORKER-READY]`/`[STRM-SCAN-STARTED]`, título em fila ou transferência concluída.

A leitura somente GET de `/System/Info/Public` respondeu HTTP 200 e confirmou produção 12.1.21. A fonte atual tem readiness Python vinculada ao PID recém-criado, tentativa independente do Downloader após falha do Envio e serialização do mount. `NebulaDownloaderStartupTests` + `NebulaHostedServiceConfigurationTests` Release passaram 39/39 nesta rechecagem. Isso valida a fonte, não o runtime 12.1.21 nem uma transferência real. Nenhum processo, configuração, serviço, mount, mídia ou download de produção foi alterado; nenhum release foi criado. T2.1 permanece aberto até publicar/instalar pacote aprovado e resolver a porta em janela controlada, preservando possíveis sessões ativas.

## Rechecagem do acionamento: serviço desabilitado — 2026-10-18 03:40 (-03)

O log local de produção mostra o servidor 12.1.21 iniciando às 20:35:46 de 17/10 com `[NEBULA-STARTUP] NebulaFTP está desabilitado na configuração`. A página administrativa consultada somente para leitura também mostra Envio/Downloader parados, `Aguardando início do Downloader` e contexto Mongo não inicializado. As duas chamadas históricas `POST /NebulaFtp/Actions/StartDownloader` (00:24 e 19:51 de 17/10) são anteriores ao início desse processo; não demonstram tentativa após o reinício. O serviço Windows MongoDB está `Running`, mas isso não confirma conectividade do banco.

`D:\midias` e as pastas configuradas de stage existem no host; não foi verificada a permissão da conta do serviço. O log anexado também registra colisão FTP `127.0.0.1:2121`, sem marcador `[STRM-WORKER-READY]` ou `[STRM-SCAN-STARTED]`. A causa confirmada para o autostart ausente é `Enabled=false`; ainda não foi reproduzida falha na ação manual atual. Nenhum start, mudança de configuração, serviço, processo, MongoDB, Telegram ou transferência foi executado. Ativar o serviço ou iniciar manualmente pode varrer e baixar toda a pasta configurada; isso permanece pendente de autorização operacional.

Não houve mudança de código nesta rechecagem, portanto nenhum teste de implementação foi executado. O item T2.1 continua em homologação; não declarar corrigido nem criar release.

## Rechecagem do runtime e do acionamento web — 2026-10-18 03:29 (-03)

O log anexado permanece sem chamada da ação `POST /NebulaFtp/Actions/StartDownloader` e sem marcadores `[STRM-WORKER-READY]` / `[STRM-SCAN-STARTED]`, fila ou transferência. A página `Jellyfin.Api/Web/NebulaFTP/configPage.js` está ligada à ação correta e trata retorno `false`/erro; isto confirma o caminho da interface na fonte, não que a solicitação tenha chegado à produção.

Rechecagem read-only às 03:29: `/System/Info/Public` retornou 12.1.21; `python.exe` PID 6080 (iniciado em 16/10 21:16) continua escutando 2121/2123. Testes repetidos: startup/host C# Release **39/39**; `test_strm_downloader.py` Python 3.10 **17/17**. Sem POST, Mongo/Telegram, instalação, parada/reinício de processo ou transferência real. Não foi identificada correção adicional reproduzível na fonte 12.1.24; produção permanece não homologada.

Próximo gate segue sendo janela operacional controlada: liberar as portas sem interromper reprodução, instalar pacote aprovado, registrar resposta do endpoint/status e logs STRM, e completar uma mídia de teste. Nenhuma release do servidor foi criada.

Andamento de homologação publicado no Git do portal: commit `77e043af9cf1b803d22d31d708d83a887e0b6e55`; status Vercel `success`. Endpoint público `/homologacao-status.json` respondeu HTTP 200 e confirmou o evento atualizado às 03:29 (-03). Testes do portal: checkout publicado **75/75**, espelho do servidor **60/60**.

## Rechecagem do runtime após solicitação de correção — 2026-10-18 03:22 (-03)

O log fornecido é de 16–17/10 e não mostra chamada do Downloader STRM: mostra `main.py` em “Somente Streaming”, depois falha de bind FTP `127.0.0.1:2121` (WinError 10048), sem `[STRM-WORKER-READY]`, `[STRM-SCAN-STARTED]`, item de fila ou transferência. A verificação read-only atual confirmou servidor 12.1.21. O PID 6080 (`python.exe`, iniciado em 16/10 21:16) continua escutando em 2121 e 2123; o PID pai 19764 não existe no snapshot. O processo não foi encerrado para evitar interromper streams.

A fonte 12.1.24 contém a tentativa de `StartDownloaderAsync` independente do Envio em `NebulaHostedService.StartComponentsAsync`; o endpoint manual devolve o booleano de startup. Testes repetidos nesta sessão: `NebulaDownloaderStartupTests` + `NebulaHostedServiceConfigurationTests` Release **39/39** e `test_strm_downloader.py` com Python 3.10 **17/17**. Isso comprova os testes/código local, não o runtime instalado nem download real. O anexo não contém retorno do endpoint manual nem logs do processo STRM; portanto não demonstra que o clique de início ocorreu. Não foi feita chamada POST, operação Mongo/Telegram, instalação ou restart.

Conclusão: runtime defasado e listener legado nas portas 2121/2123 confirmados; o PID pai não foi encontrado no snapshot. Não há evidência suficiente para atribuir a ausência de download a defeito adicional na fonte 12.1.24. Próximo gate é janela controlada: resolver o worker legado sem afetar streams, instalar pacote aprovado, capturar retorno do `StartDownloader`, `/NebulaFtp/Status`, logs STRM e concluir transferência de uma mídia. T2.1 permanece aberto e não se declara produção corrigida.

## Revalidação de startup após log operacional — 2026-10-18 03:14 (-03)

O anexo de 16/10 inicia `main.py` em modo “Somente Streaming”; a própria saída informa que feeder, fila e workers de upload estão desativados. Também há colisão de bind FTP em `127.0.0.1:2121` em uma execução posterior. Não aparecem marcadores do processo separado `strm_downloader.py`, inclusão de item na fila nem transferência. Portanto, o log não permite atribuir a falha ao downloader nem comprova que ele recebeu uma solicitação de início.

A fonte atual inicia o Downloader separadamente do Envio no `NebulaHostedService`, aguarda readiness e registra falha de startup; o endpoint manual também retorna um booleano de resultado. Testes locais desta rodada: API Release **718/718**, `NebulaDownloaderStartupTests` + `NebulaHostedServiceConfigurationTests` Release **39/39**, `test_strm_downloader.py` **17/17**. Nenhum processo, banco, Telegram, instalação ou download real foi acionado nesta revalidação.

Leitura somente de `http://localhost:8096/System/Info/Public` nesta rechecagem respondeu HTTP 200 e confirmou produção 12.1.21, enquanto a fonte registrada é 12.1.24. Próximo gate: em janela controlada após publicação/instalação autorizada, capturar o booleano de `POST /NebulaFtp/Actions/StartDownloader`, `/NebulaFtp/Status`, logs próprios `[STRM-*]`, primeiro título/fila e progresso de uma transferência. T2.1 e homologação continuam abertos; nenhuma release foi criada.

## Distinção entre worker de streaming e downloader STRM — 2026-10-18

O log operacional recebido começa com um worker `main.py` iniciado em **Modo Somente Streaming**. Essa instância confirma FTP/HTTP de streaming e desativa feeder/fila/workers de upload; ela não é o processo `strm_downloader.py`. Mais tarde, o modo Envio tenta abrir `127.0.0.1:2121` e falha com WinError 10048. O trecho não contém `[STRM-WORKER-READY]`, `[STRM-SCAN-STARTED]`, item na fila ou transferência, então não comprova que o downloader foi chamado.

Na fonte atual, o serviço hospedado tenta `StartDownloaderAsync` mesmo após falha de Envio; o downloader executa em processo separado e aguarda readiness próprio. Testes locais confirmam esse contrato, não a atividade da instalação 12.1.21. A causa exata de ausência do processo STRM neste log permanece indeterminada: verificar logs dedicados do Downloader, valor booleano da ação manual e estado `IsDownloaderRunning` numa homologação controlada. Não inferir que desativar os workers de upload no modo streaming desativa o processo de download STRM.

## Rechecagem do log anterior — 2026-10-17 21:51 (-03)

O anexo mais recente registra o worker iniciado como **Somente Streaming**, que explicitamente desativa feeder, fila e workers de upload. Mais tarde, tentativa de Envio completo falha ao vincular `127.0.0.1:2121` com WinError 10048, seguida por tentativas repetidas do mount. O trecho não contém marcadores de startup do Downloader (`[STRM-WORKER-READY]`/`[STRM-SCAN-STARTED]`), nem item ou transferência; portanto não demonstra que o Downloader foi acionado. Os backups Supabase concluídos não indicam falha de download.

Na fonte atual, `NebulaHostedService.StartComponentsAsync` tenta `StartDownloaderAsync` mesmo se Envio falhar; `StartDownloaderAsync` inicia um processo independente e aguarda marcador de readiness. Testes focados executados nesta rechecagem: `NebulaHostedServiceConfigurationTests` + `NebulaDownloaderStartupTests` Release **39/39**. Isso confirma o comportamento da fonte, não o runtime instalado nem uma transferência real. O log não identifica o PID que ocupa a porta; não iniciar/parar produção sem janela controlada. Sem alteração de código nesta rechecagem; release e smoke test continuam pendentes.

Mapa técnico conferido em 2026-10-17: sem mudança de módulo, API, classe, dependência ou fluxo de runtime nesta rechecagem. Sessão: `2026-10-17-MulletaFlix-nebula-download-log-recheck-2151.md`.

## Transição de streaming para modo completo — 2026-10-17

`NebulaFtpManager.StartEnvioAsync` serializa as inicializações e compara o modo solicitado com o ativo. Um worker iniciado para `Somente Streaming` não satisfaz uma solicitação de Envio completo. Na engine nativa, listeners FTP/HTTP existentes são liberados antes da recriação; a instância Supabase é mantida. O host de lease do cache Python é descartado mesmo se o processo do worker já não existir.

Validação local: testes focados de startup em Release passaram 39/39. O log operacional registra WinError 10048 em `127.0.0.1:2121` e retries de mount, sem determinar a identidade do listener. Sem encerramento de processos, instalação, restart ou transferência real; publicação e smoke test controlado permanecem pendentes.

## Nota da auditoria de API — 2026-10-17

Auditoria paralela do backend encontrou que `GET /Branding/Configuration` incluía caminho local da intro para qualquer cliente. O endpoint mantém as opções visuais públicas; a projeção agora inclui `IntroPath` somente para administradores. Testes focados verificam as duas classes de usuário. Isso não altera o fluxo interno `NativeIntroProvider` nem a configuração persistida.

## Correção do resultado de mount duplicado — 2026-10-17 20:31 (-03)

O helper Python agora reconhece `N:` já acessível antes de tentar adquirir o lock de instância e retorna sucesso nesse caso. Se outro helper ainda possuir o lock e `N:` não estiver acessível, retorna código distinto (`3`), não sucesso falso. O C# verifica primeiro se a unidade ficou acessível e distingue o helper concorrente de uma falha prematura real, mantendo a tentativa externa intacta e agendando nova verificação.

Testes executados: `test_mount_drive_n.py` 3/3; suíte NebulaPython 591/591; testes .NET `NebulaMountOwnershipTests`, `NebulaMountRetryTests` e `NebulaDownloaderStartupTests` 26/26. Revisão encontrou e corrigiu a classificação genérica de erros ao abrir/adquirir o lock: somente conflito de lock retorna 3; falha operacional retorna 2 com log explícito. A colisão FTP `127.0.0.1:2121` segue confirmada pelo log e exige identificar/parar o worker concorrente em janela controlada; não foi resolvida por encerrar processos. Nenhum processo produtivo ou montagem foi tocado. A mudança não prova que uploads ou downloads funcionem no runtime instalado.

Mapa conferido: fluxo de montagem existente em `NebulaFtpManager.MountDriveNAsync` → `mount_drive_n.py`; sem novo módulo de runtime, endpoint ou dependência. Registro: `2026-10-17-MulletaFlix-nebula-mount-duplicate-result-fix.md`.

## Log novo: porta FTP ocupada e tentativas repetidas — 2026-10-17 20:03 (-03)

O log fornecido mostra que uma execução do worker subiu em modo somente streaming e montou `N:`, mas uma inicialização posterior (modo de envio, 19:49:05) falhou ao abrir `127.0.0.1:2121` com WinError 10048. Depois, o servidor repetiu tentativas de mount, recebeu `Outro processo de montagem de N: já está em execução` e `rclone encerrou prematuramente (código: 0)`. Portanto, esse trecho não demonstra que o Downloader iniciou nem que uma mídia foi baixada; demonstra colisão com outro listener e retries de montagem sem recuperação.

A fonte atual valida que o endpoint `/health` pertence ao PID Python recém-criado; a instalação que gerou o log não deve ser considerada corrigida, pois a evidência de produção existente identifica 12.1.21, anterior a essa alteração. Os testes de fonte executados nesta rechecagem foram `NebulaDownloaderStartupTests` Release (**15/15**) e `test_http_stream_security.py` + `test_main_lifecycle.py` (**12/12**). Nenhum processo foi encerrado, mount alterado, download iniciado ou serviço reiniciado. A próxima validação produtiva requer publicar/instalar a fonte corrigida em janela controlada e confirmar que só o worker proprietário atende 2121/2123 e que o primeiro item entra em transferência.

O mapa técnico foi conferido: não houve mudança de módulos, endpoints ou dependências nesta rechecagem; o PID é acrescentado ao health já existente. Registro: `2026-10-17-MulletaFlix-nebula-download-port-collision-log.md`.

## Revalidação: download não inicia na instalação ativa — 2026-10-17 19:43 (-03)

O painel autenticado mostra a instalação **12.1.21**, downloader parado, raiz `D:\midias` e dois diretórios Stage disponíveis. O histórico visível contém inícios antigos que entraram na varredura global, mas não registraram título nem transferência antes do encerramento. A API pública local também retorna 12.1.21. O `strm_downloader.py` instalado continua com SHA-256 `3D41354345B79EAB19CB7A1FE0C01A02D0579130F18301D156CAF1C3221664C5`, diferente da fonte 12.1.24 (`2A671AFCA11BF06B48AAAEAA0DA49032F0A7FAD96F4963F6FEFEBD08B0B9CDBA`), que contém o handshake e a varredura incremental por título. A causa comprovada é runtime desatualizado; a transferência não foi reproduzida.

Nesta revalidação, NebulaPython passou **587/587** e `NebulaDownloaderStartupTests` Release passou **11/11**. A release pública segue `v12.1.21`; o run GitHub Actions `39037932557` terminou com falha e jobs sem runner atribuído/etapas executadas. O bloqueio de cobrança registrado anteriormente não foi confirmado diretamente nesta consulta. `gh auth status` continua sem sessão. Os pacotes candidatos Windows/Linux 12.1.24 existem localmente, mas não foram instalados. Não houve start de worker, download, alteração de configuração ou restart. Produção continua sem correção/homologação; publicar 12.1.24 e instalar em janela controlada são gates pendentes.

O mapa técnico foi conferido contra `NebulaFtpManager.StartDownloaderAsync` → `StartPythonDownloaderAsync` → `strm_downloader.py`; módulos, APIs, classes, dependências e topologia não mudaram. Registro: `2026-10-17-MulletaFlix-nebula-download-runtime-recheck-1943.md`.

## Revalidação da execução atual — 2026-10-17 19:35 (-03)

A API local confirmou MulletaFlix **12.1.21**. Comparação direta dos arquivos comprovou que `strm_downloader.py` instalado não é o mesmo runtime corrigido da fonte: SHA-256 instalado `3D41354345B79EAB19CB7A1FE0C01A02D0579130F18301D156CAF1C3221664C5`; fonte 12.1.24 `2A671AFCA11BF06B48AAAEAA0DA49032F0A7FAD96F4963F6FEFEBD08B0B9CDBA`. O instalado não contém os marcadores `[STRM-WORKER-READY]` / `[STRM-WORKER-MONGO-READY]` nem os helpers `get_categorized_scan_batches` / `get_title_scan_roots` existentes na fonte. Isso confirma que a correção de startup e varredura ainda não chegou à instância ativa; não prova um defeito de transferência MongoDB/Telegram.

Validação executada nesta sessão: `py -3.10 -m pytest tests -q` (**587/587**) e `NebulaDownloaderStartupTests` em Release (**11/11**). GitHub latest release continua `v12.1.21`, `v12.1.24` ainda sem tag, e `gh auth status` não tem sessão. O workflow de release também está bloqueado por cobrança, conforme anotação do run já registrada. Nenhum worker, download real, instalação ou restart foi executado; produção segue pendente. Não alterei código de produto nesta revalidação. O mapa técnico foi conferido e permanece igual.

## Revalidação solicitada: download continua sem iniciar — 2026-10-17 12:47 (-03)

Inspeção somente leitura do painel local: servidor **12.1.21**, `Download: Parado`, botão de iniciar habilitado e botão de parar desabilitado. O histórico visível registra workers antigos iniciando e chegando a `Escaneando e priorizando arquivos .strm...`, sem título ou transferência registrados antes do encerramento. A fonte atual 12.1.24 contém handshake e scan incremental por título; o runtime instalado não contém esses marcadores/helpers (hashes da revalidação anterior abaixo). Isso confirma divergência de runtime e explica a ausência de progresso observável; não prova que o worker atual consiga baixar do MongoDB/Telegram.

Testes repetidos nesta sessão: `py -3.10 -m pytest tests -q` em `Tools/NebulaPython`: **587/587**; `NebulaDownloaderStartupTests` Release: **11/11**. A API pública local confirmou versão 12.1.21. GitHub informa latest release `v12.1.21`; a tag `v12.1.24` não existe. `gh auth status` sem sessão. Não houve alteração no código de produto, início da fila completa, download real, instalação ou reinício. Portanto, a correção da fonte ainda não está ativa em produção; release e smoke test de um item continuam pendentes.

O andamento foi publicado no portal Git `Mulletaflix-Portal`, commit `40dd2169bd33a1bdae17566b990477e43eff1563`; Vercel Production deployment `dpl_5WZGKVFWyYAHzjHqV12mxZ97R6Yn` está `READY`. O endpoint público `/homologacao-status.json` respondeu com evento atualizado e status `in_progress`. Testes do portal: **65/65** no checkout de publicação e **51/51** no espelho do servidor; JSON e diff check passaram. Isto atualiza a homologação, não instala nem publica o servidor.

## Revalidação solicitada: download Nebula não inicia — 2026-10-17 11:37 (-03)

GET `http://localhost:8096/System/Info/Public` respondeu **12.1.21**. O `strm_downloader.py` instalado conserva SHA-256 `3D41354345B79EAB19CB7A1FE0C01A02D0579130F18301D156CAF1C3221664C5`; a fonte 12.1.24 tem SHA-256 `2A671AFCA11BF06B48AAAEAA0DA49032F0A7FAD96F4963F6FEFEBD08B0B9CDBA`. A instalação não contém `[STRM-WORKER-READY]`; nenhum processo downloader estava ativo.

Fonte revalidada: `NebulaDownloaderStartupTests` Release **11/11** e NebulaPython **587/587**. O hand-shake já presente na fonte informa ao manager que o worker iniciou antes do refresh potencialmente lento do índice Mongo. O runtime 12.1.21 é anterior a essa correção. Não houve download, instalação, configuração nem restart; nenhum teste atingiu MongoDB/Telegram reais. Portanto, o problema de produção continua aberto até publicar/instalar runtime atualizado em janela controlada e confirmar um arquivo real em transferência.

Tentativa preliminar `python -m pytest tests -q` com Python 3.14 falhou na coleta porque esse interpretador não tem `pytest`; não conta como teste. A suíte foi executada com Python 3.10 e passou 587/587.

Homologação atualizada no repositório do portal pelo commit `915f49dfe782b3ae1db93ba42a3eb385a2a88a3b`; os testes do portal passaram 61/61 no checkout de publicação e 47/47 no espelho do servidor. O endpoint público `/homologacao-status.json` respondeu HTTP 200 e mostrou o evento `server-nebula-download-runtime-revalidation-2026-10-17-1137` como `in_progress`. Esta atualização não representa release do servidor nem smoke test.

## Revalidação: worker de produção ainda é anterior à correção — 2026-10-17 11:23 (-03)

A API local respondeu **12.1.21**. O `strm_downloader.py` instalado tem SHA-256 `3D41354345B79EAB19CB7A1FE0C01A02D0579130F18301D156CAF1C3221664C5`; a fonte 12.1.24 tem SHA-256 `2A671AFCA11BF06B48AAAEAA0DA49032F0A7FAD96F4963F6FEFEBD08B0B9CDBA` e inclui o marcador `[STRM-WORKER-READY]` antes da atualização do índice Mongo e os helpers de varredura por título. O arquivo instalado não contém esses marcadores/helpers.

No momento da checagem, nenhum processo `strm_downloader.py` estava ativo. O arquivo `C:\ProgramData\MulletaFlix\nebula\nebula.log` é do processo de upload e não comprova tentativa do downloader; sua última atualização observada foi em 11/10/2026. Portanto, a causa confirmada é runtime de produção desatualizado; não há evidência suficiente para diagnosticar uma tentativa de transferência atual.

Validação da fonte nesta rodada: `NebulaDownloaderStartupTests` Release **11/11**; testes Python do downloader (scanner, helpers, fila Mongo e processamento) **105/105**. Isso não valida MongoDB/Telegram nem um download real. Nenhum worker foi iniciado, e não houve instalação, mudança de configuração ou reinício. A correção continua pendente de release/instalação em janela operacional e smoke test de um arquivo real.

O estado de homologação foi publicado no portal Git `Mulletaflix-Portal` pelo commit `62655eb`; as duas suítes de portal passaram (47/47 no espelho do projeto e 61/61 no checkout de publicação). O endpoint público retornou HTTP 200 com o evento mais recente visível. Isso publica o andamento, não conclui o gate de produção.

## Nova confirmação: worker de produção desatualizado — 2026-10-17 11:14 (-03)

A checagem read-only de `http://localhost:8096/System/Info/Public` retornou **12.1.21**. O worker instalado tem SHA-256 `3D41354345B79EAB19CB7A1FE0C01A02D0579130F18301D156CAF1C3221664C5`; o arquivo-fonte 12.1.24 tem SHA-256 `2A671AFCA11BF06B48AAAEAA0DA49032F0A7FAD96F4963F6FEFEBD08B0B9CDBA`. Busca por `[STRM-WORKER-READY]`, `[STRM-WORKER-MONGO-READY]` e helpers do scan incremental não encontrou os marcadores no worker instalado, mas encontrou todos na fonte.

Revalidação executada: `NebulaDownloaderStartupTests` Release **11/11**; testes Python 3.10 (`test_strm_downloader.py` e `test_strm_downloader_helpers.py`) **89/89**. Isso confirma os testes da fonte, não uma transferência MongoDB/Telegram. Nenhum worker/download foi iniciado, e não houve instalação, configuração ou restart. `gh auth status` informa que não há sessão GitHub. Causa atual: runtime de produção está anterior à correção já presente na fonte; portanto, a correção ainda não está ativa em produção. Publicação/instalação e smoke test de um item seguem pendentes.

O mapa técnico foi conferido: o fluxo `NebulaFtpManager` → worker Python → MongoDB/Telegram não mudou. Registro: `2026-10-17-MulletaFlix-nebula-download-runtime-stale-1114.md`.

## Revalidação: correção ainda não instalada — 2026-10-17 10:56 (-03)

O endpoint local `/System/Info/Public` respondeu **12.1.21**. O `Tools/NebulaPython/tools/strm_downloader.py` instalado tem SHA-256 `3D41354345B79EAB19CB7A1FE0C01A02D0579130F18301D156CAF1C3221664C5`; não contém os marcadores de worker pronto nem os helpers de varredura incremental presentes na fonte 12.1.24 (SHA-256 atual `2A671AFCA11BF06B48AAAEAA0DA49032F0A7FAD96F4963F6FEFEBD08B0B9CDBA`). O log instalado está sem atualização desde 11/10/2026. Isso confirma runtime desatualizado; não identifica falha de transferência de um item específico.

Revalidação da fonte: `NebulaDownloaderStartupTests` Release **11/11** e NebulaPython **587/587**. Nenhum worker/download foi iniciado; não houve alteração de configuração, instalação ou reinicialização. Portanto, download real não foi comprovado e a produção não está corrigida. Próximo passo permanece publicar e instalar 12.1.24 após liberar o CI GitHub bloqueado por cobrança; então observar um item sair da fila e concluir. Portal Git atualizado no commit `d418da35004ce197791b730accd9f75ff12975bc`; Vercel deployment Production `READY` (`dpl_7NkeUDuyS6LGj6iZ5fXq4v8sdidb`), e o endpoint público respondeu HTTP 200 com este evento `in_progress`.

## Candidato de release 12.1.24 construído — 2026-10-17 10:19 (-03)

O pacote Windows e o pacote Linux foram reconstruídos para a versão 12.1.24. Os dois builders Linux (Bash usado no workflow e PowerShell cross-publish) incluem `Tools/NebulaPython` (scripts, módulos FTP/tools e `requirements.txt`); o instalador adiciona `python3`/`python3-venv`, instala os requisitos em venv próprio e grava `python.path`, que o manager usa para localizar o interpretador. A checagem do arquivo Linux confirmou o `strm_downloader.py` dentro do tarball e o mesmo SHA-256 da fonte (`2A671AFCA11BF06B48AAAEAA0DA49032F0A7FAD96F4963F6FEFEBD08B0B9CDBA`). O workflow de release verifica esses arquivos antes de publicar.

### Bloqueio de compatibilidade Linux CPython 3.12 — identificado e corrigido na fonte (2026-10-17)

Revisão adversarial do instalador identificou que `tgcrypto>=1.2.5,<2` não possui wheel CPython 3.12 Linux. O instalador do Ubuntu 24.04 não instala compilador nem headers Python; então o `pip install -r requirements.txt` tentaria compilar uma extensão C e poderia abortar antes de instalar o serviço. Reprodução por `pip download --only-binary=:all:` para `cp312/manylinux_2_17_x86_64` falhou para `tgcrypto` e passou com wheel `tgcrypto-pyrofork` 1.2.8. Esse wheel exporta o módulo `tgcrypto` esperado pelo Pyrofork.

Atualização do fluxo em 2026-10-20: builds atuais carregam `requirements.in`, `requirements.txt` e `requirements.lock`; instalações Windows/Linux usam `pip install --require-hashes -r requirements.lock`, não a faixa aberta no `requirements.txt`. A instalação antiga descrita acima é evidência histórica do fluxo 12.1.24; gates locais de cópia/sintaxe não substituem a execução do instalador Linux em Ubuntu.

A fonte troca o requisito para `tgcrypto-pyrofork>=1.2.8,<1.3`, atualiza o mapa do `check_deps.py` e acrescenta teste de regressão. O workflow de release agora cria venv limpo em `ubuntu-24.04`, exige wheel para TgCrypto, instala os requisitos com `CC=false`/`CXX=false` e importa `pyrogram` e `tgcrypto`. Este gate remoto ainda não foi executado, pois a conta GitHub está bloqueada por cobrança; homologação de download real segue pendente.

Revalidação em 2026-10-17: Python **587/587**, `NebulaDownloaderStartupTests` Release **11/11**, suíte `Jellyfin.Server.Implementations.Tests` sem integração **1.464 aprovados, 62 ignorados, 0 falhas**; Web TypeScript/build e testes **407/407**; portal independente **57/57**, espelho **43/43**. Inspeção do payload Linux e `bash -n` passaram; verificação de artefatos web validou 1.927 arquivos e bundle inicial de 294,3 KB gzip. Resolução cruzada confirmou wheel `cp312/manylinux` para Pyrofork 1.2.8; instalação isolada em Windows Python 3.10 importou `pyrogram`/`tgcrypto` e concluiu round-trip AES-IGE. O script Ubuntu não foi executado neste Windows. Actions run `39036753498` foi bloqueado antes de iniciar jobs: conta bloqueada por cobrança; `gh auth status` sem sessão. **Release/tag não publicada e produção não corrigida**: serviço segue 12.1.21, sem instalação, restart, worker ou transferência real.

Verificações: teste do empacotamento Linux passou; Pester helper PowerShell 1/1; `bash -n` passou nos scripts alterados; Python Nebula 89/89; `NebulaDownloaderStartupTests` Release 11/11; build Windows/installer e build Linux concluídos. Build web incluiu os avisos existentes (15 dependências reportadas pelo `npm audit`, além de avisos Vite); não foram tratados neste hotfix. Commit `3716b04c` chegou ao `main`; CI run `39036436583` terminou falha em ~9 s, sem etapas executadas nos jobs, então não conta como validação. Isso não é homologação da integração real: MongoDB/Telegram não foram usados e o servidor de produção continua em 12.1.21. Release ainda não publicada. Próximos gates: destravar CI/publicação dos artefatos e instalar/reiniciar em janela controlada, então comprovar um download real de início a conclusão.

O fluxo de distribuição passou a ter uma etapa de inclusão do runtime Nebula e bootstrap de venv somente no instalador Linux; topologia do worker e APIs do servidor permaneceram iguais. Ver mapa técnico no vault e registro desta sessão.

## Revalidação visual: runtime de download antigo e sem progresso por título — 2026-10-17 10:00 (-03)

O painel autenticado do servidor confirma MulletaFlix **12.1.21**, Downloader parado, uma raiz de monitoramento configurada e dois stages com espaço disponível. Os registros de execuções anteriores mostram a inicialização do worker e a mensagem genérica de varredura, mas não mostram título indexado nem início de transferência antes de o processo parar. Não foi disparado um novo scan/transferência para evitar gravar arquivos de mídia e registros no servidor.

Comparação de runtime: `strm_downloader.py` instalado em 08/10/2026, SHA-256 `3D41354345B79EAB19CB7A1FE0C01A02D0579130F18301D156CAF1C3221664C5`; a fonte contém `get_categorized_scan_batches` e `get_title_scan_roots`, ausentes na instalação, SHA-256 `D812F36ED102085A21D826EA684ED95A270C0E3903F0EE474C6E395D9F054938`. A ausência dos helpers faz o runtime antigo percorrer a árvore inteira antes de expor progresso granular e pode explicar a demora observada; não prova, por si só, que um título específico seja baixável.

Validação da fonte: suíte NebulaPython **586/586**; `NebulaDownloaderStartupTests` Release **11/11**. O runtime de produção não foi iniciado, parado, instalado nem reiniciado nesta rodada; nenhum download real foi confirmado. A fonte está na versão 12.1.23, porém o candidato local 12.1.23 documentado anteriormente contém outro hash e não inclui esta revisão da varredura; reconstruir/publicar pacote **12.1.24** é o próximo passo, seguido de instalação controlada e verificação de um primeiro item. O portal foi publicado no Git no commit `5749a6b06559891e791c33142f527246af187775`; deployment Production Vercel `READY` (`dpl_6npaAu9dLkv1dvgzEVXp4WsRYF12`), e o endpoint público retornou HTTP 200 com o evento `in_progress`. A homologação permanece aberta.

## Downloader STRM bloqueado por runtime instalado desatualizado — 2026-10-17 09:34 (-03)

Revalidação da reclamação atual: a API local respondeu 12.1.21 e a tela Nebula mostrou o Downloader parado. O histórico registra que o worker iniciou às 00:24:21 e começou a varrer às 00:24:26; não há registro visível de primeiro download antes do encerramento às 00:30:19. O status HTTP 200 da ação não prova transferência.

O `strm_downloader.py` instalado não contém `get_categorized_scan_batches`, `get_title_scan_roots`, nem as mensagens de progresso por categoria que existem na fonte atual. A raiz monitorada tinha cinco entradas imediatas, mas o worker instalado permaneceu na varredura integral sem indicar progresso por título. A fonte atual separa a varredura por categoria e título para iniciar trabalho antes de percorrer categorias inferiores. Isso explica a espera observada com maior probabilidade, mas não prova o primeiro download: o worker foi encerrado antes de uma transferência observável.

Retificação de escopo: a ausência de `tools.mongo_admission.py` no runtime também é real, mas `main.py` importa esse módulo para o processo Python de upload. Essa ausência, isoladamente, **não explica a falha do Downloader STRM**. O runtime ativo precisa receber um pacote completo compatível; não foi instalado nem reiniciado nesta sessão, e nenhum download real foi disparado.

Testes da fonte executados nesta sessão: suíte `Tools/NebulaPython/tests` **586/586**, `NebulaDownloaderStartupTests` Release **11/11**. Não houve teste ponta a ponta com MongoDB/Telegram. A fonte e os testes estão prontos; a correção ainda não está ativa em produção. A instalação/release permanece bloqueada pelo gate global do roadmap. Próximo passo: autorizar uma release hotfix fora do gate global ou aguardar a conclusão do roadmap; depois instalar em janela operacional e observar o primeiro arquivo avançar.

## Runtime instalado ainda sem o módulo de admissão Mongo — 2026-10-17 09:22 (-03)

Rechecagem somente leitura do servidor local: `/System/Info/Public` retorna 12.1.21. O `main.py` instalado importa `tools.mongo_admission.acquire_upload_admission`, mas `Tools/NebulaPython/tools/mongo_admission.py` está ausente no diretório instalado; Python 3.10 confirmou `find_spec("tools.mongo_admission") == None`. A fonte inclui o módulo, `build-stage-and-installer.ps1` o copia e `Assert-StageIntegrity.ps1` exige sua presença. **Retificação de 09:34:** isso é um defeito do runtime de upload Python; não atribuir a ele a falha do Downloader STRM.

Validação da fonte desta sessão: `NebulaDownloaderStartupTests` Release **11/11**; `test_strm_downloader.py` + `test_strm_downloader_helpers.py` em Python 3.10 **89/89**. Não houve início do worker, download, instalação, restart nem alteração de configuração. O teste não inclui Mongo/Telegram real. Correção em produção continua pendente: empacotar/instalar o runtime íntegro em janela operacional e observar um item real avançar para download.

## Correção da confirmação de início do worker — 2026-10-17 08:59 (-03)

A fonte tinha uma regressão: o manager só aceitava `[STRM-SCAN-STARTED]`, emitido depois de `MediaValidator.refresh_cache(force=True)`. Como essa atualização lê o índice Mongo inteiro, se demorasse mais que o timeout de startup de 60 segundos o manager encerrava um worker ainda ativo antes de iniciar o scan/download. Agora o worker emite `[STRM-WORKER-READY]` após preparar argumentos e raízes e antes do refresh; o manager aceita esse marcador e mantém `[STRM-SCAN-STARTED]` como fallback. A interface mostra “preparando índice Mongo” nessa fase. O marcador não significa scan iniciado nem mídia baixada; erros de Mongo continuam fail-closed e aparecem depois no log do worker.

Verificação local desta correção: `NebulaDownloaderStartupTests` Release **11/11**; suíte NebulaPython **586/586**. O grupo .NET `FullyQualifiedName~Nebula` teve **660 aprovados, 23 ignorados, 0 falhas (683 total)**. Nenhum teste foi enfraquecido. Isso não substitui integração Mongo/Telegram nem prova de transferência real.

Produção continua sem a correção: GET `/System/Info/Public` respondeu **12.1.21**; o downloader instalado de 08/10/2026 não tem os markers de readiness/scan e não contém `tools/mongo_admission.py`, que `main.py` importa. Nenhuma instalação, reinicialização, alteração de configuração, start de worker ou transferência foi feita. O candidato 12.1.23 anterior não contém esta correção; o próximo pacote precisa ser reconstruído como **12.1.24**. CI GitHub segue bloqueada por cobrança; ainda não há release nem smoke test real. Registro da sessão no segundo cérebro e andamento de homologação no portal ficam ligados a esta correção.

## Candidato Windows/Linux 12.1.23 e bloqueio de publicação — 2026-10-17 08:47 (-03)

O stage isolado e o instalador Windows 12.1.23 foram gerados com sucesso. A versão dos assemblies no stage é 12.1.23, a do instalador é 12.1.23.0, e o pacote contém `Tools/NebulaPython/tools/mongo_admission.py`, ausente na instalação ativa 12.1.21. O hash do `strm_downloader.py` staged é `B82A43390A1F13ABEC76C7DBB563422839070DC3B474F311F5DD77F62B7F7D4D`. Instalador: `.build/release-12.1.23-20261017/build/installer-output/mulletaflix_12.1.23_windows-x64.exe` (423.339.038 bytes; SHA-256 `D4B97A545C103A7C07AD6872DC3BFB74E80DDC0EBB4D9DADF5D219EC1541FC62`). Também foi gerado o arquivo Linux `dist/mulletaflix_12.1.23_linux-x64.tar.gz` (134.577.717 bytes; SHA-256 `e03e352a0dc40d39c3b6316503eb186a4b2507f0118e83e7647549811bd00065`); `bash -n` do instalador Linux passou e a inspeção do tar confirmou servidor, `install.sh` e cliente web.

Os scripts `StageBuildOptions.Tests.ps1`, `Assert-StageIntegrity.Tests.ps1`, `StageBackup.Tests.ps1` e `RcloneBootstrap.Tests.ps1` passaram. Um teste de colisão confirmou que o builder recusa sobrescrever o mesmo instalador e preserva o hash existente. A suíte .NET Release filtrada (sem FQNs contendo `Integration`), Python 586/586, `NebulaDownloaderStartupTests` 9/9 e web Vitest 407/407 também passaram; portal 51/51.

Limites: não houve instalação, reinicialização, worker ativo, transferência real Mongo/Telegram ou confirmação de download ponta a ponta. A produção segue em 12.1.21. O commit servidor `074607e9a87c881934ba4e61a994cb1b5d112af8` está no `main`, mas CI `39029908311` e tentativas em commits documentais `39030289779`/`39030347425` falharam antes de iniciar jobs: GitHub bloqueou as execuções por problema de cobrança. Linux e Windows foram construídos localmente, porém `systemd-analyze`/smoke em Linux e o pipeline Windows não foram verificados no runner; CLI `gh` não autenticado impede a publicação manual da release. Nenhuma tag foi criada. Portal Git atualizado em `8184bf3`, testes 52/52; Vercel lista deployment Production `READY` (`dpl_8VK8NR2ksFGdWWEmgKK94oPxzqLy`) e GET público com cache-buster respondeu HTTP 200, `generatedAt=08:54`, evento final encontrado.

Incidente de empacotamento a não repetir: a primeira geração local foi executada enquanto `SharedVersion.cs` ainda indicava 12.1.22. Isso sobrescreveu os três instaladores locais 12.1.22 em `dist/` e nos dois diretórios NSIS com um build local. A colisão foi detectada; consulta ao remoto não encontrou release v12.1.22 nem cópia versionada dos binários originais, portanto não foi possível restaurá-los. O instalador produzido agora está em pasta isolada de 12.1.23, o builder verifica a versão staged e recusa colisões. Não publicar os binários locais 12.1.22 como release válida.

O mapa técnico foi conferido; o fluxo de runtime permanece `NebulaFtpManager` → worker Python → Mongo/Telegram, sem nova API ou dependência de runtime nesta etapa. Registro: [[Sessões/2026-10-17-MulletaFlix-nebula-download-release-candidate]].

## Causa confirmada na instalação ativa — 2026-10-17 08:03 (-03)

O downloader não inicia porque o `main.py` instalado importa `tools.mongo_admission`, mas `C:\Program Files\MulletaFlix\Server\Tools\NebulaPython\tools\mongo_admission.py` não existe. Reproduzi `ModuleNotFoundError: No module named 'tools.mongo_admission'` no diretório instalado; o mesmo import passou na fonte. A API somente leitura confirmou servidor **12.1.21**. O script instalado é de 08/10 e seu SHA-256 difere da fonte atual. O log Nebula não é atualizado desde 11/10; portanto, não há evidência de tentativa de download atual.

A fonte do empacotador já inclui `mongo_admission.py` e o validador rejeita stage sem esse módulo. O stage isolado `.build/candidate-nebula-download-20261017/stage` passou `validate-stage.ps1`; testes desta investigação: NebulaPython **586/586**, `NebulaDownloaderStartupTests` **9/9**, integridade do stage e builder **2/2 scripts**. Isso confirma a correção do pacote na fonte/stage, não na instalação ativa.

Não instalei arquivos nem reiniciei o servidor. NSIS está disponível, porém o asset local `mulletaflix_12.1.22_windows-x64.exe` já existe; a tag `v12.1.22` aponta para fonte com `VersionPrefix` 12.1.21, então não gerei nem sobrescrevi um pacote com versão conflitante. O push Git direto do portal funcionou, embora `gh auth status` não esteja autenticado. Próximos gates: escolher uma nova versão/tag, gerar pacote isolado, publicar e instalar em janela operacional para testar início e conclusão de download.

## Revalidação completa local — 2026-10-17 08:23 (-03)

- `dotnet test MulletaFlix.sln --configuration Release --no-restore --filter FullyQualifiedName!~Integration` terminou com exit 0. O filtro exclui testes cujo nome contém `Integration`; não é cobertura de integração Mongo/Telegram. Complementarmente, `DramaFindsLiveIntegration` isolado passou 1/1 após ajustar uma asserção externa para candidatos fuzzy de baixa confiança.
- NebulaPython 586/586; `NebulaDownloaderStartupTests` Release 9/9; quatro scripts de teste de packaging passaram; frontend web Vitest 74 arquivos/407 testes; portal Node test 48/48.
- Runtime permanece 12.1.21; source/stage do downloader coincidem no hash `B82A43390A1F13ABEC76C7DBB563422839070DC3B474F311F5DD77F62B7F7D4D`. Nenhum worker, configuração, instalação, reinício ou download foi executado. Release, integração real e primeiro download continuam pendentes.
- Portal atualizado no commit `21fd6f3af557c1bd87ea99551a37de96a30d6925`; Vercel Production `READY` (`dpl_Ab982TtNPAMbiyf26i4MSPSDRk1e`); GET público com cache-buster respondeu HTTP 200, `generatedAt=08:23`, evento encontrado com status `in_progress`.

O mapa técnico foi conferido: sem mudança estrutural nesta rodada; a alteração no teste DramaFinds não muda runtime, API ou dependências.

## Revalidação após revisão adversarial — 2026-10-17 (America/Sao_Paulo)

A revisão encontrou dois casos que ainda podiam atrasar o primeiro download: uma pasta de nível superior não reconhecida forçava fallback para scan global, e o handshake de início podia confundir preparação e scan. Categorias conhecidas mantêm a prioridade configurada; pastas desconhecidas e mídias soltas vão para um lote A–Z final; dentro da categoria, títulos e arquivos diretos são caminhados separadamente. O worker emite `[STRM-WORKER-READY]` antes da atualização completa do índice Mongo, e `[STRM-SCAN-STARTED]` quando a travessia começa; `[STRM-WORKER-MONGO-READY]` atualiza o progresso sem substituir nenhum desses marcos.

RED/green: os novos testes para layout misto e raiz de arquivo falharam antes da implementação; depois passaram. Python focado downloader/helpers: **89/89**; suíte Python completa: **586/586**; `NebulaDownloaderStartupTests` Release: **9/9**; `Jellyfin.Server.Implementations.Tests` Release: **1.458 aprovados, 62 ignorados, 0 falhas (1.520 total)**. Revisão adversarial retornou “No issues”. Nenhum worker, instalação, reinicialização ou transferência de mídia foi executado.

Estado atual: `/System/Info/Public` continua em **12.1.21**. A fonte tem SHA-256 `B82A43390A1F13ABEC76C7DBB563422839070DC3B474F311F5DD77F62B7F7D4D`; o stage é diferente e precisa ser reconstruído. A tentativa de build isolado parou antes de compilar porque o builder exige stage preexistente; inspeção também revelou backup temporário com destino no root do projeto, risco que precisa ser corrigido/testado antes de novo build. A tag `v12.1.22` aponta para commit com `VersionPrefix` 12.1.21; API não encontra GitHub Release `v12.1.22`. `gh auth status` sem autenticação. Release, instalação e primeiro download real continuam pendentes.

## Handshake Mongo fail-closed — 2026-10-17 06:52 (-03)

A verificação somente leitura do servidor local retornou versão **12.1.21**. O `strm_downloader.py` instalado, datado de 08/10/2026, tem SHA-256 `3D41354345B79EAB19CB7A1FE0C01A02D0579130F18301D156CAF1C3221664C5`; a fonte atual tem SHA-256 `E0B9A8F3461E78B9CBC9F4F5434DA7C65E38B1518D5041E50A44671C29128980`. Portanto, a instalação ativa não contém a alteração validada nesta sessão.

A fonte emitia `[STRM-WORKER-READY]` antes de `MediaValidator.refresh_cache(force=True)`. Como o manager trata esse marcador como início bem-sucedido, a interface podia informar execução enquanto o worker ainda aguardava o índice Mongo e não iniciava a varredura. Agora `refresh_cache()` informa sucesso/falha; o worker emite `[STRM-WORKER-READY]` somente após refresh válido e, em falha, emite `[STRM-WORKER-STARTUP-FAILED]` e encerra sem iniciar scan/download. `[STRM-SCAN-STARTED]` continua indicando o início seguinte da varredura. Não houve mudança de endpoint, DTO, dependência ou topologia.

Validação desta sessão: `tests/test_strm_downloader.py` **17/17**; subconjunto downloader/helpers **88/88**; suíte NebulaPython **585/585**; `NebulaDownloaderStartupTests` Release **10/10**. A suíte .NET completa, executada antes deste ajuste Python, passou com **4.919 aprovados, 63 ignorados, 0 falhas**. SHA-256 atual da fonte: `0E9393C23ABAEE9625ED7387499F9F6798A6B90954948CC8E0DC8F2CCA13F728`. Nenhum worker foi iniciado/parado, nenhuma mídia transferida e nenhuma instalação/reinicialização foi feita. A correção está apenas na fonte; release, instalação e smoke test do primeiro download permanecem pendentes.

O mapa foi conferido: fluxo existente `NebulaFtpManager` → `strm_downloader.py` → Mongo/Telegram; só mudou o momento do marcador de prontidão dentro de `run_downloader`. Sessão: [[Sessões/2026-10-17-MulletaFlix-nebula-download-handshake-mongo]].

## Rechecagem do erro reportado em 2026-10-17 05:58 (-03)

A reprodução read-only de `http://localhost:8096/System/Info/Public` confirmou que a instância local continua em **12.1.21**. O downloader instalado em `C:\Program Files\MulletaFlix\Server\Tools\NebulaPython\tools\strm_downloader.py` tem data 2026-10-08, SHA-256 `3D41354345B79EAB19CB7A1FE0C01A02D0579130F18301D156CAF1C3221664C5` e não contém `get_title_scan_roots` nem `[STRM-WORKER-READY]`. A fonte atual 12.1.22 e o downloader no candidato isolado têm o mesmo SHA-256 `EC7ABF827990C3992A7E3F926D09C189161BF6DFCCA6457E8C78899081DFAD79`; portanto, o sintoma da instalação é explicado por runtime desatualizado, não por ausência da correção na fonte.

Validação repetida nesta sessão: testes de startup C# **10/10**; testes focados Python **103/103**; suíte completa NebulaPython **584/584**; suíte `Jellyfin.Server.Implementations.Tests` **1.458 aprovados, 62 ignorados, 0 falhas (1.520 total)**. A validação de integridade do candidato falhou corretamente porque `MulletaFlix.Server.Implementations.dll` difere do publish atual; o candidato não é um pacote publicável. A primeira tentativa do comando passou `MulletaFlix-master` como raiz indevida e falhou por caminho duplicado; repetida com a raiz do repositório, encontrou a divergência real do assembly.

Nenhum worker foi iniciado/parado, nenhuma mídia transferida e produção não foi instalada ou reiniciada. Correção ativa em produção, primeiro download e release continuam pendentes. Para prosseguir é preciso reconstruir o pacote completo em destino isolado e homologá-lo, depois realizar a instalação/smoke test em janela operacional aprovada. A tentativa de pytest com Python 3.14 não tinha `pytest`; os testes foram executados com o Python 3.10 instalado.

O mapa técnico foi conferido: o fluxo continua `NebulaFtpManager` → processo Python `strm_downloader.py` → Mongo/Telegram. Nenhum módulo, endpoint, dependência ou contrato mudou nesta sessão.

## Stage isolado reconstruído — 2026-10-17 06:04 (-03)

Após o candidato anterior falhar por DLL desatualizada, o builder foi executado em `.build/nebula-download-build-20261017`, com stage e publish separados dos diretórios usuais. `build-stage-and-installer.ps1 -SkipWebBuild -SkipTrayBuild -SkipInstaller` concluiu; `validate-stage.ps1` passou com o publish novo e os testes de integridade aceitaram o downloader igual à fonte/rejeitaram a cópia antiga. SHA-256 fonte/stage: `EC7ABF827990C3992A7E3F926D09C189161BF6DFCCA6457E8C78899081DFAD79`.

O build reutilizou web/tray do stage-base e omitiu o instalador; não é uma release completa. Testes de startup C# 10/10, suíte NebulaPython 584/584 e `Jellyfin.Server.Implementations.Tests` 1.458 aprovados, 62 ignorados, 0 falhas. A produção continua em 12.1.21 e não foi tocada; nenhum worker/download foi iniciado. Primeiro download real continua pendente.

Status e resultado foram publicados no portal Git (commit `69b5cc71850ff6368c3f6f030b81a7c46fb33265`); Vercel reportou `READY` (`dpl_6bp4fPHTfyQe9eV235JXCChczckA`) e o endpoint público retornou o evento novo em `in_progress`. Testes do portal 42/42. Nenhuma mudança de topologia; mapa conferido.

## Backup Supabase e uploads Nebula — revalidação de cancelamento e concorrência (2026-10-17)

`NebulaSupabaseSyncService` propaga cancelamento solicitado nas rotinas HTTP e nos fluxos de backup/restauração, sem converter cancelamento em resultado de falha. O backup reserva a admissão compartilhada no Mongo antes de ler os dados; a reserva bloqueia novos uploads e drena os leases ativos. O gate cobre os workers C# e Python que usam `nebula_admission`.

Validação executada em 2026-10-17: testes focados `NebulaQueueConcurrencyMongoTests` e `SupabaseHttpHelpers_PropagateCallerCancellation` passaram **21/21** contra Mongo descartável em `127.0.0.1:27099`. A suíte completa `Jellyfin.Server.Implementations.Tests` passou **1.481 aprovados, 39 ignorados, 0 falhas (1.520 total)**. Mongo de produção em `127.0.0.1:27017` não foi tocado. O processo Mongo de teste foi encerrado e a porta fechou; os arquivos temporários permanecem em `%TEMP%\mfx-nebula-mongo-tests-20261017`, pois a remoção recursiva foi bloqueada pela política local.

Esses testes validam cancelamento HTTP e a admissão concorrente real no Mongo, não backup contra Supabase remoto nem transferência Telegram em produção. O downloader instalado segue sem smoke test do primeiro item; esta rodada não iniciou/parou worker, não modificou configuração ou instalação, e não executou transferência.

The STRM downloader reads only the source directories configured in `MonitorPaths` and processes one item at a time. For roots composed of direct category folders (such as `Animações`, `Filmes`, `Series`, `Doramas` and `Novelas`), it scans in configured category priority and splits category folders into alphabetically sorted title and direct-media roots. Unknown top-level folders and loose files are kept in a final A–Z batch instead of forcing a global scan before higher-priority categories. Each title/root is indexed and processed before the next one is scanned. Partial downloads are prioritized within the current root; a partial file in a later title does not jump ahead of earlier A–Z titles. Large scans report progress every 5,000 eligible files.

The scanner caches category detection per source directory because category is determined by the directory hierarchy. This preserves the existing category and title ordering while avoiding repeated Unicode/path normalization for every media file in large libraries.

## Operational verification

1. Confirm `MonitorPaths` points to existing source folders containing `.strm` or supported media files.
2. Start **Download STRM** and confirm the current step changes from idle to indexing.
3. Wait for the scan count. A non-zero count should be followed by `Iniciando download` for an eligible item; zero means the configured roots contain no eligible files.
4. If indexing does not finish, inspect the Nebula downloader log for Python stderr and the last scan message. A running Python process alone does not prove that a media item has started.

Manual start reports process readiness before Mongo refresh so a slow full-index read cannot trigger the 60-second startup timeout and terminate a live worker. `[STRM-WORKER-MONGO-READY]` remains a progress marker; `[STRM-SCAN-STARTED]` confirms traversal began, not that a file was downloaded. The UI reports worker startup, Mongo index preparation, scan start, per-title indexing and empty titles separately; an empty title does not imply the whole queue is empty. If the process exits before signaling readiness, or fails to do so within 60 seconds, the action fails and the child process is stopped. The manager keeps tracking the child if shutdown cannot be confirmed, preventing a duplicate worker on retry.

### Automatic startup gate

`NebulaHostedService` starts the automatic pipeline only when `NebulaFtpConfiguration.Enabled` is true. When enabled, the hosted pipeline starts Envio before the downloader and may mount drive N: when `UseMappedDrive` is true. Disabling the global setting therefore prevents automatic downloader startup; it is not a downloader-only switch. Manual downloader startup is a separate path and must be verified independently.

In the production instance inspected on 2026-10-16, `Enabled` was false and the server log reported that NebulaFTP was disabled. A manual downloader start did launch the Python process, but the UI log showed it remained at “Escaneando e priorizando arquivos .strm...” and never reached a media download before it was stopped. The source previously scanned categories sequentially, but still scanned an entire category before starting its first title; it now processes one alphabetically ordered title subtree at a time. The updated source has **not** been installed in production. Do not enable the global setting or start/stop the downloader solely as a test while upload/streaming workers may be active.

Validation update (2026-10-16): the Release solution test command completed successfully with `--filter 'FullyQualifiedName!~Integration'`: Model.Tests 684/684; Server.Tests 73 passed, 1 skipped; Server.Implementations.Tests 1,437 passed, 60 skipped; no failures in the reported projects. Mongo/service-dependent integration coverage was excluded or skipped, so this is not an end-to-end download result. The NebulaPython suite was rerun from its required working directory and passed 574/574 in 18.27 seconds; an initial invocation from the solution root failed test collection because the package import root was wrong, then succeeded from `Tools/NebulaPython`. Portal status tests passed 35/35 in the monorepo and 22/22 in the independent portal checkout. The status was pushed to the portal Git repository at commit `1376ca9`; the public `/homologacao-status.json` endpoint returned HTTP 200 with the updated evidence. Production remains on 12.1.21 and the fix remains uninstalled; a controlled download smoke test and a downloadable release are still pending.

Automated validation for the scanner, downloader loop and ingestion ordering is in `Tools/NebulaPython/tests/test_strm_downloader_helpers.py`, `test_strm_downloader.py` and `test_ingestion_order.py`. The full NebulaPython suite passed **574/574** on Python 3.10, including title-level incremental ordering, category priority and mixed-layout fallback. The status was published in the portal (commit `5049490d`, Production deployment `READY`; public status JSON HTTP 200). Production download completion with the updated source/binary remains unverified.

Production smoke testing must be performed separately on the active server. Do not stop or restart Nebula while uploads are active merely to validate a downloader change.

## Full local revalidation (2026-10-17)

After the report that Nebula download does not start, the focused startup tests passed 10/10, the scanner/helper subset passed 86/86, the complete NebulaPython suite passed **581/581**, and `Jellyfin.Server.Implementations.Tests` passed **1,452/1,512** (60 skipped, 0 failed; Release, no build). `validate-stage.ps1` passed and the stage downloader hash still matches source at `C588DD2006A182440A57AC02D33730531E1FD0575FB3B971B6441D459FDAA88F`. The first pytest invocation from the solution root failed collection because `tools` was not importable there; rerunning from `Tools/NebulaPython` passed. These are local source/package checks only.

The installed service remains 12.1.21 with the old downloader hash; no installation, restart, worker start/stop, or media transfer was performed. The public Vercel portal JSON still reports `in_progress` and carries the 12.1.22 stage/installer validation event, but does not contain the newer monorepo mirror event; Vercel is wired to the separate `Mulletaflix-Portal` repository. No GitHub Release `v12.1.22` exists. Keep the operational fix and release pending until publication, a controlled install, and a first-item download smoke test; do not claim the production symptom fixed based on tests alone.

The technical map was checked against `NebulaFtpManager`, `strm_downloader.py`, configuration and tests. No module, API, dependency, class or runtime flow changed in this revalidation.

Additional package/portal checks on 2026-10-17: `scripts/tests/RcloneBootstrap.Tests.ps1` and `scripts/tests/Assert-StageIntegrity.Tests.ps1` passed. Review of the Python readiness emission and C# accepted markers found them aligned. The independently hosted portal was updated with the same limits and test evidence: commit `88c87c490003c1c0bebb147c344612ff4e0a5c47`, Vercel deployment `dpl_7Nqp5jcNnkSjzjYoFciMxJ476iTL` `READY`, public JSON HTTP 200 at `generatedAt=2026-10-17T02:34:00-03:00`, status `in_progress`. Portal tests passed 32/32. The production installation remains unchanged; no release or first-item download is confirmed.

## Revalidation of the installed download path (2026-10-16)

The running server reports **12.1.21**. Its installed `Tools/NebulaPython/tools/strm_downloader.py` is dated 2026-10-08 and does not contain title-level incremental scanning or the `[STRM-SCAN-STARTED]` readiness marker present in the current source checkout. The active configuration has `Enabled=false`, `UsePythonEngine=true`, and `MonitorPaths` set; `Enabled=false` prevents automatic pipeline startup, while the manual `StartDownloader` endpoint is independent. The production logs show manual POSTs before the latest server process start, but HTTP 200 alone does not expose the returned boolean or prove a media download began.

The source correction is validated locally: it scans one A–Z title subtree and processes it before scanning the next title, and the manager waits for scan readiness instead of treating process creation as success. Targeted validation passed 4/4 C# startup tests, 99/99 Python scanner/order tests, and the deterministic Release solution suite passed 4,898 tests with 61 skipped and 0 failures. No real download, Mongo/Telegram transfer, install, or restart was performed. The source correction is therefore not yet active in production; the current tag v12.1.22 does not include the title-level/readiness changes, and the GitHub Actions billing blocker still prevents the downloadable server package. Keep homologation `in_progress`.

## Recheck of the download-start failure (2026-10-16)

The local `System/Info/Public` endpoint still reports **12.1.21**. The installed `C:\Program Files\MulletaFlix\Server\Tools\NebulaPython\tools\strm_downloader.py` is dated **2026-10-08**, and read-only content checks confirm it lacks `get_title_scan_roots`, `[STRM-SCAN-STARTED]`, and the reparse-point guard. The current source contains all three. This establishes that the validated source correction is not running in the installed server; it does not prove a new manual click or media transfer occurred during this check.

Current local validation: NebulaPython **580/580**; `Jellyfin.Server.Implementations.Tests` **1,445 passed/60 skipped**; `Jellyfin.Api.Tests` **674/674**; `NebulaDownloaderStartupTests` **4/4**; API rate-limit tests **92/92**; web tests **407/407**; production web build and artifact/bundle verification passed (1,927 artifacts; initial payload 294.3 KiB gzip under the 420 KiB budget); portal status tests **26/26**; `git diff --check` passed. A first C# test build exposed `CA1307` in the new startup test; its assertion was made explicit with `StringComparison.Ordinal`, and the rebuilt startup tests passed. The portal update was pushed as `bfb5103`; Vercel Production reported `READY` and the public status JSON returned HTTP 200. These checks do not install or exercise the downloader against MongoDB, Telegram, or production media.

The active production release remains stale. A read-only recheck on 2026-10-17 returned server **12.1.21**; the installed downloader timestamp is **2026-10-08 02:36:06** and SHA-256 is `3D41354345B79EAB19CB7A1FE0C01A02D0579130F18301D156CAF1C3221664C5`. It lacks `get_title_scan_roots`, `[STRM-WORKER-READY]` and `[STRM-SCAN-STARTED]`, which are in source. The current local build `stage` has SHA-256 `C588DD2006A182440A57AC02D33730531E1FD0575FB3B971B6441D459FDAA88F`, matching source; `validate-stage.ps1 -StageDirectory .\\stage -ProjectRoot .` passed on the current artifact. The remote `v12.1.22` tag points to `86dd41257cc60152307c7f44ee0cf79f823a42ed`, whose downloader source does not contain `[STRM-WORKER-READY]`; the GitHub Release API returns 404 for that tag. Source/stage version is already 12.1.22, so the corrected build requires a new patch version rather than reusing the tag. No worker, download, upload, installation, configuration, or server restart was initiated. `gh auth status` reports no authenticated GitHub host, and no GitHub release-write tool is available in this environment. Keep T2.1 and homologation `in_progress` until a correctly versioned package is published, installed in an approved quiet window, and a manual run reaches `Iniciando download` and enqueues a real item. The technical map was checked; no module, API, dependency, class or runtime flow changed in this revalidation.

## Package integrity guard (2026-10-17)

The production installer stage previously checked only that `Tools/NebulaPython/tools/strm_downloader.py` existed. The packaging gate now compares its SHA-256 with the server source-tree copy and rejects stale/mixed stage content. An earlier validation recorded the stage as stale; this recheck found the current stage matches source at SHA-256 `C588DD2006A182440A57AC02D33730531E1FD0575FB3B971B6441D459FDAA88F`, and `validate-stage.ps1 -StageDirectory stage -ProjectRoot .` passes. `MulletaFlix-packaging-master/scripts/tests/Assert-StageIntegrity.Tests.ps1` validates matching acceptance and mismatching rejection. The current stage does not update the installed server, whose version remains 12.1.21, and does not prove a real download. The remote `v12.1.22` tag lacks the latest readiness marker and has no GitHub Release; a correctly versioned release remains gated. Homologation remains `in_progress`.

## Worker readiness timeout correction (2026-10-17)

Code review found that the manager's 60-second startup acknowledgement depended on `[STRM-SCAN-STARTED]`, emitted only after the downloader had synchronously refreshed its full MongoDB duplicate index. Thus a live worker loading a large or slow index could be treated as failed and terminated before scanning. The worker now emits `[STRM-WORKER-READY]` after argument/source initialization and before Mongo index refresh; the manager accepts that marker (while retaining the scan marker as a fallback) and logs the distinct “preparing the index and queue” phase. This makes manual start acknowledgement track process readiness, not first media transfer.

Validation on 2026-10-17: Python downloader module tests 16/16; C# `NebulaDownloaderStartupTests` 7/7; tests include both readiness markers and the ordering of the Python marker before Mongo refresh. Production service was not running in this environment, and no process, download, MongoDB, Telegram, installation, or restart was exercised. Therefore the code-level startup-timeout defect is corrected and unit-tested, but production first-download success remains pending an approved smoke test and release.

## Windows installer rclone bootstrap (2026-10-17)

The Windows NSIS installer now requires and invokes `install-rclone-if-missing.ps1` before the WinFsp setup step. The helper uses a pinned rclone version and verifies the downloaded archive SHA-256 before placing `rclone.exe` in the server tools directory; a missing helper or non-zero result aborts installation. Stage integrity also requires this helper. This closes the gap where the stage builder warned that rclone would be downloaded by the installer, but NSIS did not invoke it.

The bootstrap is covered by static PowerShell contract tests and the stage-integrity fixture. Those checks do not execute an actual rclone download or mount. Production remains 12.1.21, and no install or runtime transfer was performed in this validation.

## Production reproduction and status reporting (2026-10-17)

A manual start was triggered from the authenticated production Nebula page. `POST /NebulaFtp/Actions/StartDownloader` returned HTTP 200 after **15.74 s**; dependency checks found Python 3.14, MongoDB, rclone and WinFsp. The worker was marked running, then logged `Escaneando e priorizando arquivos .strm...`; after **5m52s** it was still scanning, with no first item or download progress. UI continued showing “Aguardando início do Downloader...” despite the running badge. This is consistent with the installed legacy scanner traversing the whole configured root before processing an item, and proves the active installation does not contain the incremental title scan. The diagnostic worker was then stopped; Envio remained stopped and the panel showed drive N: mounted. The old runtime script hash is `3D41354345B79EAB19CB7A1FE0C01A02D0579130F18301D156CAF1C3221664C5`, while the corrected source is `C588DD2006A182440A57AC02D33730531E1FD0575FB3B971B6441D459FDAA88F`.

The source UI status parser now reports the worker-ready/Mongo-index phase, current title indexing, and a title with zero eligible files as “seguindo para o próximo” rather than falsely labeling the entire queue empty. Added three C# tests for these cases; focused startup suite passes **10/10**. The NebulaPython suite passes **581/581**. The installed server remains **12.1.21** and its staged downloader remains stale; the smoke attempt was not observed through a completed transfer. No media file was moved or enqueued during the observed interval. Do not report production fixed until a rebuilt/published package is installed and a controlled run reaches the first download.

## Revalidation of the first-title scan (2026-10-16)

The authenticated production page showed version 12.1.21 and `MonitorPaths` rooted at `D:\midias`. Its log showed two Python starts followed by “Escaneando e priorizando arquivos .strm...”: one from 21:14:19 until the downloader ended at 21:22:49, and another from 21:38:11 until it ended at 21:50:04. Neither excerpt contained scan counts or “Iniciando download”. These observations confirm a long scan before the first item in the installed version; they do not identify who stopped either run.

Read-only inspection of the actual source root found the expected direct category folders. Running the current scanner's directory-classification functions against that root found five category batches, 120 animation title folders, and one eligible item in the first alphabetic title. This did not download media, contact Telegram/Mongo, or change production. Four focused ordering/fallback tests passed, the full NebulaPython suite passed **574/574**, and `Jellyfin.Server.Implementations` built in Release with **0 warnings and 0 errors**. The incremental title-level scan remains in the source checkout and is not installed in production; a real download smoke test and release remain unverified. GitHub CLI is not authenticated in this environment.

## Rebuilt local server stage (2026-10-17)

The Windows server stage was rebuilt from the current checkout with `build-stage-and-installer.ps1 -SkipWebBuild -SkipTrayBuild -SkipInstaller -NoPause`. The script published the server in Release, copied the current Nebula Python runtime, and restored the stage's preserved web, tray, MariaDB, media tools, and HTTPS payloads. `validate-stage.ps1` passed afterward. The staged downloader SHA-256 matches the source (`C588DD2006A182440A57AC02D33730531E1FD0575FB3B971B6441D459FDAA88F`). `VersionPrefix`, assembly version, file version, and product version are now aligned at **12.1.22**. NebulaPython passed **581/581** and `NebulaDownloaderStartupTests` passed **10/10** after the server rebuild.

This is a local stage build only: the installer step was skipped, no release or installation was performed, and no production worker was started. The active installation remains 12.1.21 with the old script, so the first real download is still unverified. The package build also reported that `rclone.exe` was absent from stage and is expected to be downloaded by the installer; that path remains a packaging/runtime prerequisite to validate. The builder's stage-only summary was also corrected so it no longer labels a stage-only build as a release or prints a stale, pre-existing installer. Homologation evidence was published at portal commit `cb046989`; Vercel Production deployment `dpl_HDmaRmuH1h3TjQ61hWgzsMSAs93G` is `READY`, and the public status endpoint contains the event. Keep T2.1 and homologation `in_progress` until the final Windows/Linux package gates, release policy, controlled installation, and first-item smoke test pass.

## Revalidation after repeated report that download does not start (2026-10-17)

Read-only request to `http://localhost:8096/System/Info/Public` returned HTTP 200 and version **12.1.21**. The listener is `MulletaFlix.exe` from `C:\Program Files\MulletaFlix\Server`; its installed downloader remains dated 2026-10-08 with SHA-256 `3D41354345B79EAB19CB7A1FE0C01A02D0579130F18301D156CAF1C3221664C5`. The source SHA-256 is `C588DD2006A182440A57AC02D33730531E1FD0575FB3B971B6441D459FDAA88F`; `stage` passes `validate-stage.ps1` with that corrected script. Repeated validation passed: NebulaPython **581/581**, `NebulaDownloaderStartupTests` **10/10**, and `Jellyfin.Server.Implementations.Tests` **1,452 passed/60 skipped** (Integration excluded). This proves the running installation is stale relative to validated source and stage; it does not prove that a real media download now succeeds. No install, restart, worker action, configuration change, or real download was performed. Keep T2.1 and homologation `in_progress` pending an authorized install and a controlled first-item smoke test.

## Revalidation after report that download still does not start (2026-10-17)

Read-only request to `http://localhost:8096/System/Info/Public` returned HTTP 200 and server version **12.1.21**. The existing local stage has the source-matching downloader SHA-256 `C588DD2006A182440A57AC02D33730531E1FD0575FB3B971B6441D459FDAA88F`; `validate-stage.ps1 -StageDirectory ./stage -ProjectRoot .` passed. Repeated tests: NebulaPython **581/581**; `NebulaDownloaderStartupTests` **10/10**. This confirms the tested stage is not installed in the active server. No production config, files, workers, or transfers were changed. Do not claim the production symptom fixed until the server runs the updated package and a controlled test observes `Iniciando download` and a completed/queued item. Installer/release and the rclone bootstrap check remain pending.

## Recheck of user report: Nebula download does not start (2026-10-17)

The active listener is `C:\Program Files\MulletaFlix\Server\MulletaFlix.exe`, PID 16928 (started 2026-10-16 23:09:23); `/System/Info/Public` returned HTTP 200 and version **12.1.21**. No Nebula/Python/rclone worker process was present at the time of the read-only process snapshot. The installed downloader hash remains the legacy `3D41354345B79EAB19CB7A1FE0C01A02D0579130F18301D156CAF1C3221664C5`, while the corrected source/stage hash is `C588DD2006A182440A57AC02D33730531E1FD0575FB3B971B6441D459FDAA88F`; the running server therefore does not contain the validated incremental scanner/readiness handshake. Current checkout validation in this session: `NebulaDownloaderStartupTests` **10/10**; Python scanner/helper focused tests **86/86** on Python 3.10. An initial attempt with default Python 3.14 could not collect because pytest is not installed there; no dependency was installed. These results validate source behavior, not an actual production transfer. No source-code change, worker start/stop, configuration change, install or restart was performed. Root cause established to date: production still runs the older package and has no worker active in the snapshot; whether its configured queue has eligible files was not queried. To finish the operational correction, an approved maintenance window is required to install the built current package, restart the server, then verify worker startup and a real first queued/downloaded item. The technical map was checked against `NebulaFtpManager`, `strm_downloader.py`, configuration and tests; no modules, APIs, dependencies or runtime flow were changed in this diagnostic session.

Follow-up validation in the same session: `validate-stage.ps1 -StageDirectory D:\Users\Raphael\Documents\Projetos\mulletaflix\stage -ProjectRoot D:\Users\Raphael\Documents\Projetos\mulletaflix` passed; explicit hashes confirmed installed != source == stage. This verifies package consistency, not installation.

### Isolated stage candidate for the Nebula download correction (2026-10-17)

The packaging builder now accepts optional `-StageDirectory` and `-BuildDirectory` outputs, constrained to remain under the repository root, and validates the selected publish directory rather than assuming the default `.build/server-publish`. `validate-stage.ps1` forwards the optional publish path. This allowed rebuilding and validating an isolated candidate under `.build/nebula-download-candidate-20261017` without deleting or replacing the repository's active `stage` directory.

Validation evidence: candidate stage integrity passed; source and candidate `strm_downloader.py` SHA-256 both `EC7ABF827990C3992A7E3F926D09C189161BF6DFCCA6457E8C78899081DFAD79`; staged/published `MulletaFlix.dll` hashes both `A913AD77DEC4761184DE3F2B8357492195F025536A81C8CDFB9B13ADACC27A3A`; staged/published `MulletaFlix.Server.Implementations.dll` hashes both `08163374138ECCBEFD72127EB9D5C3A2B5586CFDF2293E2EB6FC2B1F32114F2F`. Packaging script tests passed. Candidate contains 3,224 files (~1.15 GB), copied web/tray artifacts from the prior stage and therefore is not a complete freshly built release. Production remains 12.1.21; no worker, installation, restart or transfer was performed. Release and first real download remain unverified.

## Recheck: active server still runs the stale downloader (2026-10-17)

The current read-only `GET http://127.0.0.1:8096/System/Info/Public` returned HTTP 200 and **12.1.21**. The installed `Tools/NebulaPython/tools/strm_downloader.py` is dated 2026-10-08 and lacks `[STRM-WORKER-READY]`, `[STRM-SCAN-STARTED]`, and `get_title_scan_roots`; the source and local `stage` both have SHA-256 `C588DD2006A182440A57AC02D33730531E1FD0575FB3B971B6441D459FDAA88F`. The startup test group passed **10/10** (Release), Python scanner/helper tests **86/86** (Python 3.10), and the stage integrity validator passed. Thus, the start/readiness and incremental-scan correction is already present and validated in source/stage, but is not deployed to the active server. `gh auth status` reports no authenticated GitHub host; the GitHub Contents API attempt to publish the portal status returned HTTP 403 `Resource not accessible by integration`, so the status update remains local. No downloader start/stop, install, restart, configuration mutation, or media transfer was performed. Production correction and first-item smoke test remain pending release publication and a controlled maintenance window; do not report the runtime fixed yet.

## Expansão da auditoria MongoDB isolada (2026-10-17)

Para avançar nos testes de integração sem tocar no Mongo da produção (listener `127.0.0.1:27017`), foi iniciado um `mongod` temporário 8.3.7 somente em loopback `127.0.0.1:27099`, com diretório de dados exclusivo sob `.build/nebula-test-mongo-20261017-a`. A variável opt-in `MULLETAFLIX_TEST_MONGODB_CONNECTION_STRING` foi definida apenas no processo do `dotnet test`; os testes aceitam exclusivamente host/porta fixos e usam nomes de bancos isolados/aleatórios. `Category=RequiresMongo` em Release passou **21/21, 0 falhas, 0 skips**. O processo temporário foi encerrado e a porta 27099 fechou; o diretório de dados de teste permaneceu em `.build` após a política bloquear a remoção recursiva. Nenhum teste usou a porta 27017. Isso fecha uma lacuna dos testes de integração Mongo, mas não valida o download real via Telegram nem corrige a instalação 12.1.21; primeiro download em produção segue pendente.

Na mesma instância descartável, a suíte completa `Jellyfin.Server.Implementations.Tests` Release passou **1.473 aprovados, 39 ignorados, 0 falhas (1.512 total)** em 1m42s. Os 21 testes `RequiresMongo` passaram; os demais skips ainda incluem dependências de MySQL/plataforma e benchmarks. O processo de teste Mongo foi encerrado e a porta 27099 fechou. Isso não executa integração end-to-end com Telegram nem comprova primeira transferência.

## Recheck de início reportado pelo usuário — 2026-10-17 02:42 (-03)

O processo ativo é `MulletaFlix.exe` versão 12.1.21. O log geral registra POST `/NebulaFtp/Actions/StartDownloader` com HTTP 200 em 15,74 s às 00:24 e uma chamada StopDownloader às 00:30. HTTP 200 não expõe o valor bool de retorno e não prova que um item foi iniciado; não há confirmação de primeiro item/transferência no log geral. `MonitorPaths` aponta para `D:\midias`, diretório existente com 197.082 `.strm`; isso descarta apenas raiz ausente, não fila elegível. Downloader instalado tem hash antigo `3D41354345B79EAB19CB7A1FE0C01A02D0579130F18301D156CAF1C3221664C5`, fonte corrigida `C588DD2006A182440A57AC02D33730531E1FD0575FB3B971B6441D459FDAA88F`. Testes repetidos: `NebulaDownloaderStartupTests` 10/10 e scanner/helpers Python 86/86. Nenhum worker/API mutável, instalação, alteração de configuração, restart ou transferência foi executado nesta rechecagem. A atualização de runtime e um smoke test do primeiro item continuam pendentes.

The same read-only configuration check found `Enabled=false`. `NebulaHostedService` explicitly skips automatic startup when this global gate is false, while the manual `StartDownloader` API route separately calls the manager. Thus, this setting explains lack of automatic start, but it does not explain the incomplete result of the manual request. The flag was not changed: enabling Nebula may also start FTP/upload components. A separate approved change is required if automatic start is desired.

The API controller regression suite was also run in Release: `NebulaFtpControllerTests` **36/36** passed, including boolean true/false responses for manual start. This is controller-level coverage only; it does not spawn the worker or prove production transfer.

## Revalidação do sintoma de download não iniciado (2026-10-17)

Repeti a suíte específica do startup do downloader em Release: `NebulaDownloaderStartupTests` e `NebulaHostedServiceConfigurationTests`, **25/25 aprovados**. Em Python 3.10, `test_strm_downloader.py` e `test_strm_downloader_helpers.py` passaram **86/86**. A tentativa com Python 3.14 não foi válida porque esse runtime não tem `pytest`; foi substituída pela execução completa em Python 3.10. O executável servidor ativo continua sendo a versão **12.1.21**; o código/stage corrigido é **12.1.22**, e não foi iniciado worker nem feita transferência real nesta validação. O pacote Linux não pode ser construído neste ambiente: Docker e WSL não estão instalados. Portanto, nenhum release foi publicado e não se afirma correção em produção. Permanecem pendentes uma release com pacotes Windows e Linux e o smoke test controlado do primeiro download.

## Revalidação complementar (2026-10-17)

Após nova auditoria, a API ativa ainda responde **12.1.21** e não havia worker do downloader no snapshot. O código/stage 12.1.22 seguem sem implantação, portanto o sintoma do Nebula não está corrigido em produção nem foi possível confirmar a primeira mídia baixada. `NebulaDownloaderStartupTests` passou **10/10**, mas cobre startup/mocks, não uma transferência Telegram real. A suíte `Jellyfin.Api.Tests` passou **679/679** após adicionar regressão para deleção de imagem de usuário. Nenhum worker ou transferência foi iniciado. A tentativa de validar o stage apontou a pasta incorreta e falhou; revalidação do stage permanece pendente, assim como pacote Linux, publicação da release e smoke test em janela de manutenção.

## Revalidação do pedido de download que não inicia (2026-10-17, 03:35 -03)

- A API local continua respondendo **12.1.21**. O script instalado, datado de 2026-10-08, tem SHA-256 `3D41354345B79EAB19CB7A1FE0C01A02D0579130F18301D156CAF1C3221664C5`; a fonte e o stage têm `C588DD2006A182440A57AC02D33730531E1FD0575FB3B971B6441D459FDAA88F` e contêm handshake de worker pronto, scan por título e prioridade A–Z. Isso confirma que a correção existente não está no runtime ativo.
- Verificação repetida nesta sessão: `NebulaDownloaderStartupTests` **10/10**; suíte Python completa, no diretório `Tools/NebulaPython` com Python 3.10, **581/581**; `validate-stage.ps1 -StageDirectory .\stage -ProjectRoot .` passou. Executável no stage identifica versão **12.1.22**.
- Nenhum worker foi iniciado/parado, nenhuma configuração/instalação foi alterada e nenhum arquivo foi transferido. O snapshot não mostrou processos `python`, `pythonw` ou `rclone`. Não houve alteração de código nesta sessão: testes validam a correção já presente na fonte/stage, não um download real.
- GitHub CLI não está autenticado (`gh auth status`); não foi criada nem publicada release. O runtime produtivo e o primeiro download continuam pendentes de publicação/autorização e instalação controlada. A busca de padrão confirma uma única implementação do fluxo Python em `run_downloader`; não há segunda rotina de varredura por categoria/título equivalente.
- O mapa técnico foi conferido; nenhum módulo, API, classe, dependência ou fluxo mudou nesta revalidação.

## Rechecagem solicitada — downloader não inicia (2026-10-17 03:37 -03)

- O endpoint local `/System/Info/Public` ainda identifica produção como **12.1.21**. SHA-256 do `strm_downloader.py`: produção `3D41354345B79EAB19CB7A1FE0C01A02D0579130F18301D156CAF1C3221664C5`; checkout e stage corrigidos `C588DD2006A182440A57AC02D33730531E1FD0575FB3B971B6441D459FDAA88F`.
- Logs locais registram POST manual `StartDownloader` com HTTP 200 e duração de **15,74 s**. Isso não demonstra `true` no payload, início de item ou transferência; a versão em execução não tem o handshake/scan por título atualmente validado.
- Snapshot de processos não mostrou worker Python do servidor nem processo `rclone` do servidor. Processos com nomes semelhantes pertencem a outros diretórios/projetos e não foram tocados.
- Revalidação da fonte/stage: `NebulaDownloaderStartupTests` + `NebulaHostedServiceConfigurationTests` **25/25**; Python 3.10 scanner/helpers **86/86**. Não iniciou/parou worker, não chamou ação HTTP mutável, não mudou config/instalação nem fez transferência.
- Diagnóstico: a correção já está na fonte e no stage, mas não na instância ativa. Portanto o sintoma permanece em produção até publicar/instalar o pacote atualizado e executar smoke test controlado do primeiro item. A causa secundária de não autostart (`Enabled=false`) foi registrada anteriormente; o botão manual é independente e esse flag não foi alterado.
- Mapa técnico conferido: nenhum módulo, API, classe, dependência ou fluxo foi alterado nesta rechecagem. Release e homologação real permanecem pendentes.
- Portal sincronizado no commit `77a82ca6cc0c413bcbab38827b9d7c33406de311`; testes do portal **35/35** e deployment Vercel `dpl_Cq9jXS2HC3Gf2KHbfx3TiPaAufqA` `READY`; endpoint público HTTP 200 confirmou `in_progress`.

### Revalidação adicional — 2026-10-17 03:51 -03

- GET read-only `/System/Info/Public`: versão ativa **12.1.21**. Downloader instalado (08/10/2026) SHA-256 `3D41354345B79EAB19CB7A1FE0C01A02D0579130F18301D156CAF1C3221664C5`, sem marcadores de prontidão. Fonte/stage 12.1.22 SHA-256 `C588DD2006A182440A57AC02D33730531E1FD0575FB3B971B6441D459FDAA88F`, com `[STRM-WORKER-READY]` e `[STRM-SCAN-STARTED]`.
- Testes desta rodada: `NebulaDownloaderStartupTests` **10/10** em Release; Python 3.10 `test_strm_downloader.py` + `test_strm_downloader_helpers.py` **86/86**; `validate-stage.ps1 -StageDirectory .\stage -ProjectRoot .` passou. A tentativa com Python 3.14 não contou porque pytest não está instalado nesse runtime.
- Conclusão: source/stage continuam corrigidos; o runtime ativo continua antigo. Nenhum worker, configuração, instalação, reinício ou mídia foi alterado, e não houve smoke test real. A correção em produção e a primeira mídia baixada seguem pendentes.
- `gh auth status` não encontra autenticação; não houve publicação de release nesta rodada. Mapa técnico conferido; estrutura sem alterações. Sessão: [[Sessões/2026-10-17-MulletaFlix-nebula-download-source-validated-runtime-old]].
- Portal de homologação atualizado: commit `f1e5808911208fcd2caef75ca296518fa2b91191`; testes do portal 35/35; Vercel Production `dpl_A1YyhX1bzxSpLcsY3rPEFUzMrDuM` `READY`; endpoint público HTTP 200 contém o status `in_progress`.

### Rechecagem posterior e publicação do estado — 2026-10-17 03:58 -03

- Reteste focal: `NebulaDownloaderStartupTests` 10/10; NebulaPython 581/581; `validate-stage.ps1` passou. O stage atual bate com a fonte; isso corrige a anotação anterior de que o stage ainda era stale. A instalação permanece em 12.1.21 e não recebeu restart nem transferência.
- Tag Git `v12.1.22` existe, mas o downloader do commit da tag não tem `[STRM-WORKER-READY]`; GitHub Release API retorna 404. Não usar a tag para publicar o stage atual. Próxima patch version é necessária.
- Estado de homologação publicado em `Mulletaflix-Portal`, commit `06cca32319a37cf8e9174ce853b934e76cc1b6c1`; portal testado 35/35; endpoint `/homologacao-status.json` HTTP 200 confirma evento `server-nebula-download-recheck-2026-10-17-0358`. Homologação continua `in_progress`.

### Rechecagem solicitada — download Nebula não inicia — 2026-10-17 04:29 -03

- Evidência read-only: `/System/Info/Public` responde versão 12.1.21. O `strm_downloader.py` instalado tem SHA-256 `3D41354345B79EAB19CB7A1FE0C01A02D0579130F18301D156CAF1C3221664C5`; fonte e stage 12.1.22 têm `C588DD2006A182440A57AC02D33730531E1FD0575FB3B971B6441D459FDAA88F`.
- Configuração ativa tem `Enabled=false`, `UsePythonEngine=true`, `UseMappedDrive=true`, uma raiz configurada existente. `Enabled=false` impede autoinício do pipeline; o endpoint manual é caminho separado. Log antigo registra POST manual `StartDownloader` HTTP 200 após 15,74 s e `StopDownloader` aproximadamente seis minutos depois, mas não registra confirmação de item/transferência. Não é possível atribuir o não início manual somente a `Enabled=false`.
- Process snapshot não mostrou downloader Python filho do servidor; executáveis Python/rclone observados pertencem a outro projeto/uso e não foram tocados. A fonte já contém handshake e scan incremental por título, porém essa correção não está no runtime instalado.
- Revalidação: `NebulaDownloaderStartupTests` 10/10; Python 3.10, `tests` completo 581/581; `validate-stage.ps1 -StageDirectory .\stage -ProjectRoot .` passou; SHA do downloader source/stage coincide. Nenhum arquivo de código foi alterado nesta sessão.
- Não iniciou/parou worker, alterou configuração, instalou pacote, reiniciou serviço ou transferiu mídia. Não declarar produção corrigida. Para corrigir a instância e validar primeiro download, falta publicar pacote com a fonte atual e instalar em janela aprovada; autostart também exige escolha explícita, pois o gate global inicia outros componentes Nebula.

### Investigação complementar — repetição do refresh Mongo trava a fila — 2026-10-17

- `MediaValidator.refresh_cache` é executado no início e na checagem de cada mídia. Quando a leitura do cursor falhava, o timestamp do último intento não era atualizado, fazendo a mídia seguinte refazer o scan completo. Essa repetição pode atrasar toda a fila durante falhas/transientes do Mongo, mesmo com worker vivo.
- Corrigido na fonte: `last_cache_attempt_time` limita novas tentativas a 120 s sem substituir o timestamp do último snapshot bem-sucedido; cursor usa `max_time_ms(15000)` quando o driver oferece esse método. Erros continuam logados e o snapshot anterior é preservado.
- Foi iniciada também a implementação do gate Mongo compartilhado entre backup Supabase e upload C#/Python. Leases renováveis bloqueiam novas admissões enquanto o backup drena uploads existentes; alteração não está implantada nem validada contra Mongo real nesta sessão.
- Validação: NebulaPython completo em Python 3.10 **584/584**; .NET Release focado (`NebulaDownloaderStartupTests` e `NebulaSupabaseSyncTests`) **52/52**. Dois novos testes `RequiresMongo` para a barreira foram compilados, mas ignorados porque não havia Mongo de teste nesta sessão. Log local de Nebula mais recente é de 11/10; não há evidência atual de worker/primeiro download. Instalação permanece 12.1.21; fonte 12.1.22.

### Correção da janela claim→admissão — 2026-10-17

- Auditoria do fluxo mostrou que `NebulaStagingWatcher` atualizava o documento para `uploading` antes de `NebulaUploadEngine` obter o lease. O watcher agora reserva a admissão antes do claim Mongo e passa o mesmo lease ao motor, cobrindo preparação, claim e transferência sem adquirir lease duplicado.
- Se o lease compartilhado se perder, o token cancela o trabalho; o watcher tenta liberar condicionalmente o claim para `queued`, preservando partes já confirmadas, e devolve o item à fila. A liberação não sobrescreve cancelamento administrativo.
- Regressões .NET Release para `NebulaUploadEngineTests` e `NebulaStagingWatcherTests`: **230 aprovados, 2 RequiresMongo ignorados, 0 falhas**. Mongo real descartável não foi iniciado nesta rodada; os novos casos de admissão compilam, mas sua concorrência cross-process ainda requer execução Mongo.
- Isso fecha a corrida no código-fonte, não comprova que o downloader está operacional no runtime. Processo ativo observado anteriormente 12.1.21, fonte 12.1.22; log de produção local continua antigo e não há smoke test de mídia. Sem alteração de produção nem release.
- Não houve restart, instalação nem transferência. Não declarar correção em produção; primeiro download real, Mongo descartável para a barreira e publicação/instalação permanecem pendentes.

### Rechecagem do download que não inicia — 2026-10-17 05:10 -03

- GET read-only em `/System/Info/Public` continua identificando produção como `12.1.21`. O downloader instalado é de 08/10 e seu SHA-256 é `3D41354345B79EAB19CB7A1FE0C01A02D0579130F18301D156CAF1C3221664C5`; não contém `[STRM-WORKER-READY]`, `[STRM-SCAN-STARTED]` nem `get_title_scan_roots`. A fonte `12.1.22` tem SHA-256 `EC7ABF827990C3992A7E3F926D09C189161BF6DFCCA6457E8C78899081DFAD79` e contém as correções de readiness e scan incremental.
- A validação de integridade do `stage` falhou porque `stage/Tools/NebulaPython/tools/strm_downloader.py` ainda tem o hash anterior `C588DD2006A182440A57AC02D33730531E1FD0575FB3B971B6441D459FDAA88F`, diferente da fonte atual. Os assemblies do stage também diferem dos assemblies recém-compilados em `bin/Release`, portanto esse stage não é um pacote consistente desta fonte.
- Validações: `NebulaDownloaderStartupTests` 10/10; suíte completa `Jellyfin.Server.Implementations.Tests` 1.452 aprovados, 62 ignorados, 0 falhas; NebulaPython em Python 3.10 584/584. `pytest` em Python 3.14 não executou porque o módulo não está instalado nesse runtime; a suíte válida foi executada em Python 3.10. `validate-stage.ps1` reprovou pelo hash divergente, não passou.
- O log local `C:\ProgramData\MulletaFlix\nebula\nebula.log` não recebe atualização desde 11/10/2026 23:22; não existe evidência atual de worker nem transferência. Nenhum processo/worker foi iniciado ou parado; não houve alteração de instalação/configuração, restart ou mídia transferida.
- Diagnóstico: a fonte contém correções já testadas, mas a instalação ativa e o stage estão atrasados. Isso explica por que esses testes locais não corrigem a instância observada. Não foi identificada nem demonstrada uma nova falha de lógica na fonte nesta sessão; a correção em produção exige pacote consistente, publicação e instalação controlada, seguidos de smoke test do primeiro item. A homologação permanece `in_progress`.
- O mapa técnico foi conferido contra manager, downloader, testes e manifests; nenhum módulo, API, classe, dependência ou fluxo mudou. Sessão registrada no segundo cérebro; estado publicado no portal.

### Cobertura unitária adicional da lease Mongo — 2026-10-17 05:26 -03

- `NebulaMongoAdmissionLeaseTests`: 3/3 passaram; cenários cobrem renovação rejeitada, exceção no heartbeat e descarte idempotente/liberação única.
- Suíte completa de `Jellyfin.Server.Implementations.Tests`: 1.455 aprovados, 62 ignorados, 0 falhas. NebulaPython 3.10: 584/584.
- Testes que dependem de Mongo real continuam sem execução nesta rodada (runtime `mongod` indisponível); não há prova de integração cross-process nem transferência Telegram. Produção continua 12.1.21; sem instalação, restart, worker ou primeiro download controlado.
- Esta rodada só acrescentou testes. Stage/candidato de release não foi reconstruído como pacote completo; release não autorizada pelos gates ainda abertos.

### Rechecagem do download Nebula solicitado — 2026-10-17 05:44 -03

- `GET /System/Info/Public` retornou `12.1.21`. O script instalado (`08/10/2026`) tem SHA-256 `3D41354345B79EAB19CB7A1FE0C01A02D0579130F18301D156CAF1C3221664C5`; a fonte atual tem `EC7ABF827990C3992A7E3F926D09C189161BF6DFCCA6457E8C78899081DFAD79`. A instalação continua sem as correções atuais de readiness/scan incremental.
- Snapshot de processos não mostrou filho Python/rclone do processo MulletaFlix; só `conhost.exe`. `nebula.log` não recebe atualização desde 11/10/2026 23:22. Nenhum worker foi iniciado/parado e nenhuma configuração, instalação, mídia ou processo de produção foi alterado.
- Revalidação local: `NebulaDownloaderStartupTests` + `NebulaHostedServiceConfigurationTests` **25/25**; Python 3.10 (`test_strm_downloader.py` + `test_strm_downloader_helpers.py`) **87/87**. Isso confirma a fonte; não comprova download Telegram real.
- Diagnóstico: a instalação ativa está desatualizada em relação à fonte. Configuração `Enabled=false` foi observada em 04:29 e bloqueia o autostart global; não foi relida nesta verificação. Não alterá-la sem decisão explícita, pois o gate também inicia Envio e pode montar N:. T2.1 e homologação continuam `in_progress`; pacote integral, instalação em janela aprovada e primeiro item baixado seguem pendentes.
- Mapa técnico conferido em 17/10; módulos, APIs, classes, dependências e fluxo estrutural não mudaram. Registro: [[Sessões/2026-10-17-MulletaFlix-nebula-download-runtime-recheck-0544]].
### Revalidação da falha de início do download — 2026-10-17 06:09 (-03)

- Repetidos os gates locais: suíte Nebula C# em Release **659 aprovados, 23 ignorados, 0 falhas (682 total)**; NebulaPython 3.10 **584/584**; `validate-stage.ps1` com `PublishDirectory` passou para `.build/nebula-download-build-20261017`.
- O endpoint read-only `/System/Info/Public` ainda informa **12.1.21**. O downloader instalado continua com SHA-256 `3D41354345B79EAB19CB7A1FE0C01A02D0579130F18301D156CAF1C3221664C5`; source e stage usam `EC7ABF827990C3992A7E3F926D09C189161BF6DFCCA6457E8C78899081DFAD79`.
- A causa confirmada continua sendo o runtime desatualizado; não foi reproduzido novo defeito na fonte. Não houve instalação, restart, worker, alteração de configuração ou transferência real. O stage continua sem instalador e reaproveita web/tray; portanto não é release. `gh auth status` informa que nenhuma conta GitHub está autenticada.
- Resultado: correção validada na fonte/stage, ainda não aplicada nem homologada em produção. Próximos gates são pacote/release integral com credencial GitHub disponível e janela controlada para instalar/reiniciar e provar o primeiro download. Mapa técnico conferido; nenhum módulo, API, classe, dependência ou fluxo mudou.

### Correção do caminho de stage passado ao instalador — 2026-10-17 06:15 (-03)

- Causa confirmada no fluxo de build: `build-stage-and-installer.ps1` permitia compilar/validar um `-StageDirectory` isolado, mas chamava `build-mulletaflix-installer.ps1` sem parâmetro; o helper então voltava silenciosamente ao `stage` padrão. Isso permitia validar um stage e empacotar outro, potencialmente desatualizado.
- Correção: o builder encaminha o caminho selecionado ao instalador; o instalador normaliza e limita o stage ao root do projeto antes de procurar arquivos ou NSIS. Regressão `StageBuildOptions.Tests.ps1` cobre a propagação, aceitação do parâmetro, normalização e confinamento.
- Testes: `StageBuildOptions.Tests.ps1`, `Assert-StageIntegrity.Tests.ps1` e `RcloneBootstrap.Tests.ps1` passaram; `NebulaDownloaderStartupTests` passou **10/10**; suíte NebulaPython em Python 3.10 passou **584/584**; `git diff --check` passou. Exercício direto com um caminho temporário fora do projeto foi rejeitado pela guarda antes de qualquer compilação do instalador. Uma tentativa inicial de coletar pytest a partir da raiz falhou por cwd incorreto; a execução pelo diretório `Tools/NebulaPython` completou normalmente.
- Estado real read-only: `/System/Info/Public` continua em 12.1.21; downloader instalado continua com SHA-256 `3D41354345B79EAB19CB7A1FE0C01A02D0579130F18301D156CAF1C3221664C5`. Não foi instalado, reiniciado, nem iniciado worker ou transferência. A correção de packaging remove uma causa de stage/instalador incoerentes; não prova nem corrige runtime de produção por si só. Pacote integral, instalação em janela controlada e primeiro download continuam pendentes; não há release concluída.
- O mapa técnico foi atualizado para registrar a passagem do stage no fluxo de packaging; sem mudança de módulos, APIs públicas, dependências ou runtime. Portal de homologação atualizado e publicado no Git commit `a0d0966b3b8dfc2c29c1ce728c51fa7785d31c4a`; Vercel Production `READY` (`dpl_5e73eYhec4wW1DJy9bzoSWZczFFo`) e endpoint público confirmou o evento `in_progress`. Sessão: [[Sessões/2026-10-17-MulletaFlix-nebula-download-installer-stage-path]].
## 2026-10-17 — Pacote do worker Python e diagnóstico do download que não inicia

- A instalação observada continua em MulletaFlix **12.1.21**; seu downloader instalado (`strm_downloader.py`) difere da fonte atual pelo SHA-256. A configuração lida sem exibir credenciais tem `UsePythonEngine=true`, `Enabled=false`, uma pasta `MonitorPaths` existente. `Enabled=false` desabilita o autostart do serviço hospedado, não o botão de início manual.
- O checkout atual adiciona `from tools.mongo_admission import acquire_upload_admission` em `Tools/NebulaPython/main.py`. O script de packaging copiava `main.py`, mas não copiava `tools/mongo_admission.py`; portanto, um pacote criado a partir dessa fonte poderia iniciar o worker FTP com `ModuleNotFoundError`. A lista de cópia agora inclui o módulo, e o validador exige sua presença no stage.
- `Assert-StageIntegrity.Tests.ps1` agora comprova que um stage sem `mongo_admission.py` é rejeitado; `StageBuildOptions.Tests.ps1` verifica que o builder inclui o módulo. Isso corrige um defeito confirmado na cadeia de build, mas não aplica a correção à instalação 12.1.21 nem prova um download real.
- Próximos gates: gerar e validar um stage/package completo com os dois workers Python, obter CI de release aprovado, instalar em janela operacional controlada e observar uma mídia iniciando e concluindo. Não reiniciar o servidor nem iniciar transferência nesta sessão.
### Stage isolado, lock do pipeline e gate de release — 2026-10-17 07:53 (-03)

- Stage Windows isolado compilado em `.build/candidate-nebula-download-20261017`: servidor .NET Release, frontend, tray, workers/módulos Python, MariaDB e ferramentas incluídos. O validador integrado aceitou os assemblies e scripts do stage. NSSM 2.24 foi obtido do arquivo oficial e validado por SHA-256 `F689EE9AF94B00E9E3F0BB072B34CAAF207F32DCB4F5782FC9CA351DF9A06C97`.
- Revisão encontrou que o lock do empacotamento era liberado após somente a etapa de servidor. O lock agora envolve o pipeline inteiro até integridade/instalador/fim; teste estático e de concorrência conferem essa garantia. Revisão final não encontrou novos apontamentos.
- Validação: NebulaPython **586/586**; `NebulaDownloaderStartupTests` **9/9**; `Jellyfin.Server.Implementations.Tests` **1.458 aprovados, 62 ignorados, 0 falhas (1.520 total)**; quatro testes PowerShell do packaging passaram; parser do builder passou.
- Gate de release permanece aberto: o builder localiza `makensis.exe` na instalação padrão `C:\Program Files (x86)\NSIS\makensis.exe`, embora não esteja no `PATH`; nenhum instalador foi gerado naquela validação. Produção continua 12.1.21; não houve instalação/restart, início de worker nem download real. Portanto, fonte e stage passaram seus testes, mas não se afirma correção em produção nem release publicada. Status atualizado no portal, commit `b5284fd`.

### Rechecagem de runtime, stage e suíte — 2026-10-17 08:10 (-03)

- Runtime read-only continua em 12.1.21 e o script instalado difere da fonte. O stage candidato foi confirmado byte-a-byte alinhado ao `strm_downloader.py` de fonte (SHA-256 `B82A43390A1F13ABEC76C7DBB563422839070DC3B474F311F5DD77F62B7F7D4D`).
- Testes repetidos: `NebulaDownloaderStartupTests` Release 9/9; suíte Python Nebula 586/586; quatro verificações do empacotador concluíram sem erro. A suíte do portal passou 47/47.
- A execução inicial da solução encontrou uma asserção frágil no teste externo `DramaFindsLiveIntegrationTests`: a API pública devolveu candidatos fuzzy para uma consulta que o teste esperava vazia. O teste foi ajustado para validar ausência de candidatos fortes (score ≥ 0,92) e ausência de cooldown; essa alteração ainda precisa de execução de regressão.
- `makensis.exe` está instalado no caminho padrão, mas não no `PATH`. A saída 12.1.22 e a tag remota v12.1.22 já existem; o commit da tag contém `VersionPrefix` 12.1.21. A tag v12.1.23 ainda não existe. Sem novo instalador, instalação, worker ou mídia; produção e primeiro download real continuam não homologados.
### Rechecagem do download não iniciado — 2026-10-17 09:08 (-03)

- A instância local de produção está ativa e `/System/Info/Public` retorna MulletaFlix **12.1.21**. O `strm_downloader.py` instalado tem SHA-256 `3D41354345B79EAB19CB7A1FE0C01A02D0579130F18301D156CAF1C3221664C5`, diferente da fonte em HEAD (`2A671AFCA11BF06B48AAAEAA0DA49032F0A7FAD96F4963F6FEFEBD08B0B9CDBA`), que já inclui scan incremental por categoria/título e marcadores de progresso.
- O log de produção registra `POST /NebulaFtp/Actions/StartDownloader` com HTTP 200 em 15,74 s às 00:24 e `StopDownloader` às 00:30. HTTP 200 não revela o booleano de início e não há no log geral evidência de primeiro item ou transferência concluída. A verificação read-only do endpoint autenticado de status retornou 401; portanto não foi possível observar o estado interno pela API. Não confundir resposta HTTP com download iniciado.
- Validação local repetida: `NebulaDownloaderStartupTests` **11/11**; `test_strm_downloader.py` e `test_strm_downloader_helpers.py` **89/89**. São testes de regressão de fonte, não smoke test de Telegram/Mongo em produção.
- Causa do sintoma atual: a instalação produtiva não contém o worker corrigido da fonte. Não alterei nem reiniciei o servidor; iniciar o worker pode escrever arquivos e registros Mongo/Telegram. O endpoint de status foi consultado somente por GET. A correção ainda não está aplicada em produção.
- Pendências: reconstruir pacote integral a partir do HEAD atual (o candidato anterior é anterior à última correção), superar o bloqueio de cobrança da CI GitHub, publicar a release conforme os gates do projeto e agendar instalação/restart controlados; depois verificar primeiro item e transferência real. Homologação permanece `in_progress`.
- Mapa técnico conferido em 2026-10-17; sem alteração em módulos, APIs, classes, dependências ou fluxo estrutural. Registro: `2026-10-17-MulletaFlix-nebula-download-runtime-stale-0908.md`.
- Homologação publicada no portal pelo commit `8f34d05`; testes do portal 52/52 e GET público HTTP 200 confirmou `generatedAt=2026-10-17T09:08:00-03:00`.

### Revalidação solicitada — download Nebula sem início — 2026-10-17 11:02 (-03)

- Comparação read-only confirma produção 12.1.21 e worker instalado sem os marcadores `[STRM-WORKER-READY]`/`[STRM-SCAN-STARTED]`; SHA-256 instalado `3D41354345B79EAB19CB7A1FE0C01A02D0579130F18301D156CAF1C3221664C5`. Fonte 12.1.24 SHA-256 `2A671AFCA11BF06B48AAAEAA0DA49032F0A7FAD96F4963F6FEFEBD08B0B9CDBA` contém o handshake e helpers correspondentes. A divergência runtime/fonte permanece causa confirmada; a instalação não foi alterada.
- Testes repetidos nesta sessão: `NebulaDownloaderStartupTests` Release 11/11; Python 3.10 (`test_strm_downloader.py` + `test_strm_downloader_helpers.py`) 89/89. Os testes confirmam fonte, não uma transferência real.
- `gh auth status` continua sem sessão; não foi possível publicar uma release. Nenhum worker foi iniciado, nenhuma configuração alterada e nenhum serviço reiniciado; portanto o download não está confirmado como corrigido em produção. Próximo passo: autenticar/publicar o candidato e, em janela operacional controlada, instalar/reiniciar e verificar o primeiro item.
- Mapa técnico conferido contra `NebulaFtpManager`, worker STRM e testes; módulos, contratos, dependências e topologia sem mudança.
- Teste do portal independente 58/58 e espelho local 44/44; `git diff --check` passou. Portal externo atualizado no Git (`Mulletaflix-Portal` commit `79c8f60`); GET público HTTP 200 confirmou o evento. Release do servidor permanece bloqueada por `gh auth status` sem sessão; nenhuma release ou homologação de download real.

### Pacotes candidatos reconstruídos e validados — 2026-10-17 12:06 (-03)

- A fonte da versão 12.1.24 já contém o handshake de prontidão do worker e a varredura incremental; esta rodada não encontrou nova falha no código de execução. O runtime ativo ainda é 12.1.21 e não foi modificado.
- Reconstruídos em diretório isolado o pacote Linux (`.build/candidate-nebula-download-20261017-1200/dist/mulletaflix_12.1.24_linux-x64.tar.gz`, SHA-256 `142D3B9EF33FBABDD189A1D11665970F0B84FE877BFD543695793F2F3119464F`) e o instalador Windows (`.build/candidate-nebula-download-20261017-1200/build/installer-output/mulletaflix_12.1.24_windows-x64.exe`, SHA-256 `F100E72EC8761DD87B972975F521AC411F30F553A2AD51C748A5111E1E7C7B66`). O tar contém 2.544 entradas e o `strm_downloader.py` empacotado coincide byte a byte com a fonte (SHA-256 `2A671AFCA11BF06B48AAAEAA0DA49032F0A7FAD96F4963F6FEFEBD08B0B9CDBA`). O instalador foi compilado, mas não foi executado para instalação.
- O teste de packaging que estava fixado em 12.1.23 foi atualizado para verificar a versão definida pelos metadados atuais sem relaxar a igualdade das versões. O builder Linux agora aceita saída isolada sob `.build`, mantendo `dist` como padrão e recusando sobrescrita/caminho reanalisado; testes estáticos e guarda de não sobrescrita passaram. O artefato anterior em `dist` permaneceu inalterado.
- Verificações de fonte: NebulaDownloaderStartupTests 11/11; NebulaPython 587/587; Implementations sem integrações 1.464 aprovados, 62 ignorados, 0 falhas; testes PowerShell de packaging e verificações Bash passaram. Nenhum download real foi iniciado ou observado.
- Gate de release continua bloqueado: a execução Actions 39037932557 falhou antes de iniciar steps; `gh auth status` não possui sessão. Não existe release publicada nesta rodada. Produção 12.1.21 não foi instalada/reiniciada e a correção ainda não foi homologada pelo primeiro download.
- Homologação/testes publicados no Git do portal (`Mulletaflix-Portal` commits `2871dc5` e `a927815`); portal: 61/61 testes, espelho do projeto: 47/47; GET público HTTP 200 confirmou `generatedAt=2026-10-17T12:06:00-03:00` e evento `in_progress`.
- Mapa técnico conferido em 2026-10-17: nenhum módulo, API, classe, dependência do runtime ou fluxo Nebula alterado nesta rodada; alterações restritas a scripts/testes/documentação de packaging.

### Revalidação atual do download Nebula — 2026-10-17 12:22 (-03)

- Leitura HTTP pública local de `/System/Info/Public` confirma servidor **12.1.21**. A tag `v12.1.24` não existe localmente nem em `origin`; `gh auth status` informa sessão ausente. Não há publicação de release confirmada.
- O stage isolado atual contém `strm_downloader.py` idêntico à fonte: SHA-256 `2A671AFCA11BF06B48AAAEAA0DA49032F0A7FAD96F4963F6FEFEBD08B0B9CDBA`. Os candidatos já construídos existem: instalador Windows SHA-256 `F100E72EC8761DD87B972975F521AC411F30F553A2AD51C748A5111E1E7C7B66`; pacote Linux SHA-256 `142D3B9EF33FBABDD189A1D11665970F0B84FE877BFD543695793F2F3119464F`.
- Regressão repetida agora: `NebulaDownloaderStartupTests` Release **11/11**; Python 3.10, `test_strm_downloader.py` + `test_strm_downloader_helpers.py`, **89/89**. Esses resultados validam a fonte e os testes, não uma transferência real.
- Nenhum worker/download foi iniciado, nem configuração, instalação ou serviço alterado. Causa atual continua sendo divergência entre runtime e fonte; a correção não está comprovada na produção. Gates pendentes: autenticação/publicação da release, janela aprovada para instalação/restart e smoke test com o primeiro item.

### Revalidação do processo ativo e log Nebula — 2026-10-17 12:27 (-03)

- Reconsulta read-only: `/System/Info/Public` respondeu 12.1.21; processo MulletaFlix.exe PID 16928. O `nebula.log` tem 82.510 bytes e última gravação em 11/10/2026 23:22:32 (-03), sem atividade recente registrada.
- Snapshot de processos por ancestralidade não encontrou `python`, `strm_downloader` ou `rclone` descendente do processo MulletaFlix. Isso confirma ausência de worker filho naquele instante, não prova por si só por que o comando de início falha; o estado autenticado do downloader continua indisponível.
- Pacotes candidatos, hashes e testes de fonte permanecem conforme a validação de 12:22. Não houve tentativa de iniciar worker, download, instalação ou restart. Release e primeiro download real continuam não homologados; GitHub não autenticado e gate global de release ainda aberto.
- O mapa técnico foi conferido; nenhuma alteração de módulos, APIs, classes, dependências ou topologia nesta revalidação. Registro: `2026-10-17-MulletaFlix-nebula-live-process-log-recheck-1227.md`.

### Auditoria do gerador STRM no pacote — 2026-10-17 12:34 (-03)

- Confirmação no checkout atual: `Tools/NebulaPython/generate_strm.py` existe; control plane chama essa implementação real e os testes cobrem pais Mongo legados/ObjectId e saída STRM. O builder inclui o arquivo e `Assert-StageIntegrity.ps1` já o declara obrigatório.
- Adicionada regressão explícita ao teste de integridade para remover `generate_strm.py` do stage e exigir rejeição. Testes do validador PowerShell, builder PowerShell e Python (gerador + control plane) passaram; detalhes na sessão `2026-10-17-MulletaFlix-nebula-strm-generator-stage-integrity-1234.md`.
- O stage candidato Windows tem SHA-256 igual à fonte (`A123B90D083DD39B964795E0A6DA70E4F26723046FBA4FFD8E8360BB216B8CFD`). Isso cobre o candidato local, não o artefato instalado nem geração E2E com Mongo/Telegram. T0.3 e homologação de runtime permanecem parciais.

### Reteste visual do início do download — 2026-10-17 12:40 (-03)

- O painel local autenticado mostra versão 12.1.21 e estado atual `Envio: Parado`, `Download: Parado`. Os logs visíveis registram três inicializações do worker Python (21:14, 21:38 e 00:24), cada uma chegando a “Escaneando e priorizando arquivos .strm...”; depois disso, o trecho não mostra progresso de mídia antes do encerramento. Isso indica demora/falta de progresso observável na varredura, não falha do clique/API: as sessões chegaram a iniciar o worker.
- O script instalado (SHA-256 `3D41354345B79EAB19CB7A1FE0C01A02D0579130F18301D156CAF1C3221664C5`) não contém `get_title_scan_roots` nem os marcadores de progresso incremental. A fonte 12.1.24 (SHA-256 `2A671AFCA11BF06B48AAAEAA0DA49032F0A7FAD96F4963F6FEFEBD08B0B9CDBA`) divide a varredura por categoria/título e processa cada lote antes de avançar.
- Revalidação local: `test_strm_downloader.py` + `test_strm_downloader_helpers.py` passaram 89/89; `NebulaDownloaderStartupTests` + `NebulaHostedServiceConfigurationTests` passaram 26/26. Não foi iniciada a fila de produção, porque ela pode baixar toda a biblioteca; sem release/instalação não há comprovação de correção ativa.
- `/System/Info/Public` confirma produção 12.1.21. `gh auth status` está sem sessão e a tag remota `v12.1.24` não foi encontrada. A causa operacional segue sendo runtime instalado antigo; nenhuma nova falha na lógica da fonte foi reproduzida. Pendem autenticação GitHub, execução de CI/publicação, instalação em janela controlada e smoke test limitado a um item.
- Homologação publicada no Git `Mulletaflix-Portal` (commits `fadadb3` e revisão `44180ab`); deploy Vercel Production mais recente `dpl_DWtqX8T5zH5HsPwPwwPk65zFSCd9` ficou `READY`; endpoint público `/homologacao-status.json` retornou HTTP 200 com o evento/texto revisado.
- Mapa conferido: sem mudança de módulo, API, dependência ou topologia. Registro: `2026-10-17-MulletaFlix-nebula-download-start-investigation-1240.md`.

### Prontidão do runtime e conflito de portas — 2026-10-17

- O log recebido mostra que o Python novo falha ao abrir FTP `:2121` (WinError 10048), mas o C# considerava o runtime pronto ao receber qualquer resposta saudável em HTTP `:2123`. Se um processo antigo ainda responder nessa porta, a sonda confundia esse processo com o filho recém-criado.
- O `/health` agora informa o PID do processo servidor; o C# só aceita prontidão quando o PID corresponde ao `Process.Id` lançado por ele. A resposta de um worker alheio deixa de mascarar o erro de bind do processo novo.
- Verificações: NebulaDownloaderStartupTests 15/15; NebulaPython 588/588; teste de segurança HTTP 7/7; falha de download remoto de imagem 2/2. A suíte completa Providers foi interrompida após permanecer sem saída por mais de um minuto; resultado inconclusivo, não contado como aprovação.
- Consulta read-only do host identificou `python.exe` PID 6080 escutando simultaneamente em 2121 e 2123 desde 2026-10-16 21:16; PID pai 19764 já não está presente. O processo mantém 37 conexões TCP estabelecidas, então foi preservado para evitar interromper sessões. Ainda é necessária janela de manutenção para substituir o worker e confirmar um download real.

### Rechecagem do log: porta FTP ocupada e montagem repetida — 2026-10-17

- O log de 17/10 comprova que o novo worker de modo Envio não consegue vincular `127.0.0.1:2121` (WinError 10048), depois de uma sessão anterior em modo Somente Streaming ter iniciado outro processo. Em seguida, chamadas repetidas de montagem N: reportam montagem já em execução e retorno do helper rclone com código 0; isso não comprova que o worker de envio nem o downloader estejam ativos.
- A fonte atual recusa marcar o worker como pronto usando somente um `/health` de `:2123`: exige que `pid` corresponda ao processo Python recém-criado. Se o worker próprio termina durante startup, `StartEnvioAsync` retorna falso; o `NebulaHostedService` atual ainda tenta iniciar o Downloader de forma independente e pula a montagem automática se o Envio/FTP falhar. A instalação vista nos logs ainda pode aceitar um serviço antigo na porta e, portanto, precisa receber a release antes de validar esse comportamento.
- Validação local desta sessão: `NebulaDownloaderStartupTests` + `NebulaHostedServiceConfigurationTests` Release 39/39; Python 3.10, suíte `Tools/NebulaPython/tests` 592/592, incluindo regressão de bind FTP ocupado. `/System/Info/Public` respondeu HTTP 200 e confirmou produção 12.1.21. Não foram iniciados worker, transferência nem processo de produção. Release, janela de manutenção e primeiro download real permanecem pendentes.
- A causa operacional observada não autoriza encerrar arbitrariamente o listener: a porta pertence a processo com sessões ativas. Só substituir/liberar o worker numa janela controlada e depois validar que Envio, Downloader e N: sobem uma única vez.

### Rechecagem de produção e regressão do bind FTP — 2026-10-18

- No log anexado, `/health` foi reportado como pronto em `:2123` antes de aparecer nova inicialização de bots; logo depois, o worker registra WinError 10048 ao abrir `127.0.0.1:2121`. A sequência é compatível com a sonda antiga aceitar um serviço HTTP que não era o processo recém-criado, mas o proprietário do listener não está identificado e isso segue como hipótese, não certeza.
- A leitura HTTP de `/System/Info/Public` confirmou produção 12.1.21; portanto a alteração de prontidão por PID da fonte não está demonstrada como instalada. Não há no log marcador de início do Downloader STRM, título ou transferência.
- Adicionada regressão Python: bind FTP ocupado deve retornar falha, parar bots já iniciados e não iniciar HTTP. Suíte Python completa 592/592; testes C# de hosted service/startup Release 39/39. Sem alteração no serviço, processos, configuração, mount ou biblioteca de produção.
- Mapa técnico consultado: fluxo e módulos existentes, sem alteração de arquitetura; esta sessão apenas acrescenta cobertura do tratamento de erro já implementado. Homologação de runtime e primeiro download continuam pendentes. Registro: [[Sessões/2026-10-18-MulletaFlix-nebula-download-port-recheck]].

### Validação do log de startup e regressões de fonte — 2026-10-18 04:29 (-03)

- Revisão integral do log anexado: `main.py` inicia em Somente Streaming, modo que explicitamente desativa feeder, fila e workers de upload. Mais tarde, inicialização em modo Envio não consegue abrir `127.0.0.1:2121` (WinError 10048); tentativas subsequentes encontram outro helper de montagem. O retorno 0 do helper antigo não confirma que N: esteja acessível. O trecho não contém `[STRM-WORKER-READY]`, `[STRM-SCAN-STARTED]`, título na fila ou transferência. Isso não prova que o processo STRM separado tenha sido chamado.
- A fonte atual inicia o Downloader independentemente do Envio, aguarda readiness do processo Python recém-criado e trata lock de mount ocupado como concorrência (código 3). Isso não altera retroativamente a instalação antiga. A verificação read-only realizada nesta auditoria confirmou servidor 12.1.21 e worker Python instalado diferente da fonte; o processo ativo manteve conexões e foi preservado.
- Testes: `NebulaDownloaderStartupTests` + `NebulaHostedServiceConfigurationTests` + `NebulaMountRetryTests` Release 45/45; ciclo de vida/mount Python 9/9; suíte NebulaPython 592/592; portal independente 79/79 e espelho 64/64. A suíte .NET completa passou pelos projetos iniciais sem falhas observadas, mas parou sem progresso em Providers por mais de cinco minutos; encerrada manualmente e classificada inconclusiva, não como aprovação global.
- Nenhuma mudança funcional de código nesta rechecagem; nenhum serviço, processo, mount, configuração, banco, Telegram, instalação ou mídia de produção foi alterado. Não houve transferência real. Nenhuma release criada; validação produtiva requer publicação/instalação do worker atual e janela segura para resolução do listener, seguida de smoke test de um item. Mapa técnico conferido e inalterado. Portal atualizado no Git `Mulletaflix-Portal`, commit `bd3434e`; GET público do JSON retornou HTTP 200 e evento `in_progress`. Testes do portal: independente 79/79, espelho 64/64. Registro: `2026-10-18-MulletaFlix-nebula-download-log-runtime-recheck-0429.md`.

### Correção da promoção Python de Somente Streaming para Envio — 2026-10-18 04:42 (-03)

- O log mostra inicialização em Somente Streaming seguida por tentativa de Envio que colide no FTP `127.0.0.1:2121` (WinError 10048). Causa encontrada na fonte: a transição encerrava o worker anterior apenas quando a engine C# estava configurada; com engine Python, o processo stream-only permanecia vivo enquanto outro `main.py` tentava abrir a mesma porta.
- `StartEnvioAsync` agora encerra o worker Python anterior ao promover o modo; a engine nativa também libera seus listeners antes de recriar o serviço. Foi acrescentado teste da condição de reinício da promoção.
- Testes locais: `NebulaDownloaderStartupTests` + `NebulaHostedServiceConfigurationTests` + `NebulaMountRetryTests` Release 49/49; Python 3.10 — casos de ciclo de vida, HTTP health e mount 16/16. A primeira chamada Python não coletou testes por `PYTHONPATH` ausente; repetição no diretório correto passou. `git diff --check` aprovado.
- Produção consultada somente por GET: versão 12.1.21; listener atual em 2121/2123 preservado. Sem restart, instalação ou transferência real. Promoção corrigida na fonte, mas não homologada em produção; release 12.1.24 continua bloqueada pelos gates pendentes do roadmap e do primeiro download controlado.
- Mapa técnico conferido: fluxo interno existente em `NebulaFtpManager`, sem módulo/API/dependência nova. Portal atualizado no commit `e2ce7ee663372d2e0635d57f29ac6f307c280c41`; GET público HTTP 200 confirmou o evento `in_progress`, portal 80/80 e espelho 65/65. Registro: [[Sessões/2026-10-18-MulletaFlix-nebula-streaming-to-full-python-promotion]].

### Parada segura do worker Python antes de liberar as portas — 2026-10-18 05:27 (-03)

- A leitura do log confirma colisões repetidas de bind FTP em `127.0.0.1:2121` ao tentar ativar Envio completo. Não há nele marcador `[STRM-WORKER-READY]`, `[STRM-SCAN-STARTED]`, título baixando ou transferência; a ausência do Downloader nesse trecho não pode ser concluída.
- Revisão da rotina de promoção achou falha de lifecycle: `StopPythonStreamServiceAsync` apagava o handle e descartava o processo mesmo quando `Kill`/espera falhava, podendo perder controle de um worker que ainda segurava a porta. A rotina agora preserva o handle e aborta a transição enquanto o processo continuar ativo; só libera as referências e o lease depois da saída confirmada.
- Testes de Nebula startup/host Release: **43/43**. Testes direcionados de autorização de avatar: **17/17**. A execução integral de `Jellyfin.Api.Tests` está registrada na sessão, incluindo qualquer falha remanescente; warnings de analyzer já existentes não são tratados como aprovação.
- Não houve interação com os endpoints de produção, encerramento de processo alheio, instalação, mudança de configuração, mount ou download. A alteração é fonte local, sem homologação de runtime e sem release. Para comprovar o início real do Downloader ainda são necessários status/logs dele e um download controlado.

### Falha de Envio não deve bloquear o Downloader — 2026-10-18 05:42 (-03)

- O log anexado mostra tentativa do modo Envio encerrada por WinError 10048 na porta FTP `127.0.0.1:2121`. O `NebulaHostedService.StartComponentsAsync` chama Envio e Downloader em sequência, mas `StartWithRetryAsync` só trata `TimeoutException` e `IOException`; uma `InvalidOperationException` de worker que encerra no bind escapava e interrompia o fluxo antes da chamada do Downloader.
- O startup agora captura falhas não relacionadas a cancelamento no componente Envio, registra que Downloader será tentado e continua para `StartDownloaderAsync`. Cancelamento segue propagado. Teste regressivo `EnvioStartupException_DoesNotPreventDownloaderFromStarting` verifica a exceção, chamada única do Downloader e ausência de mount quando Envio não ficou ativo.
- Verificação: testes Release `NebulaHostedServiceConfigurationTests` + `NebulaDownloaderStartupTests`, 44/44; build compilou os projetos envolvidos. A API local `/System/Info/Public` respondeu HTTP 200 com servidor 12.1.21, anterior à fonte atual. O log antigo não prova que a versão instalada contenha a correção nem que houve início do processo STRM ou transferência.
- Nenhum processo, listener, serviço, configuração, mount ou mídia de produção foi alterado. Primeiro download real, instalação e release permanecem pendentes; requer janela controlada de manutenção e smoke test de um item.
- Mapa técnico conferido e atualizado porque o fluxo de falha entre Envio e Downloader mudou. Registro: `2026-10-18-MulletaFlix-nebula-downloader-survives-envio-start-failure.md`.

### Reanálise do log anexado e estado atual — 2026-10-18 07:34 (-03)

- O anexo recebido repete o log histórico de 16–17/10: inicia explicitamente em Somente Streaming (fila e workers de upload desligados), depois tenta modo Envio e falha ao vincular FTP `127.0.0.1:2121` com WinError 10048. Os retries subsequentes do helper de montagem dizem que já existe outro processo e retornam antes de confirmar N:. Não há nele marcador de chamada do Downloader STRM, readiness, varredura, título na fila ou transferência.
- Reconsulta read-only atual: servidor 12.1.21; listeners 2121/2123 ativos em processo preexistente; worker separado `strm_downloader.py --watch` presente. Isso confirma processos/listeners, não progresso de scan nem download concluído. Preservados serviços/processos e conexões; sem POST, restart, mount, escrita em banco ou mídia.
- As regressões locais já registradas para promoção de modo e falha de Envio continuam na fonte, mas não são prova de instalação: produção está na 12.1.21. Não houve nova correção de código nem release nesta reanálise. T2.1 permanece em andamento até versão candidata instalada e transferência real observada com escopo seguro.
- Testes da fonte não foram repetidos nesta reanálise (nenhuma alteração de código); os resultados históricos são 592 testes Python, 729 testes API e 689 aprovados/24 ignorados na bateria de Implementations focada em Nebula. Portal de homologação commit `3f201786139f89ca4a3cf46b70faf61c9743b5c6`, GET público HTTP 200 confirmou evento T2.1 `in_progress`; testes 83/83 no portal independente e 69/69 no espelho. Mapa técnico conferido contra o fluxo existente; estrutura inalterada. Registro: `2026-10-18-MulletaFlix-nebula-log-runtime-recheck-0734.md`.

### Reanálise do log e estado de runtime no startup — 2026-10-18 08:27 (-03)

- O log anexado é histórico (16–17/10): `main.py` anuncia `Modo somente streaming`, que explicitamente desativa fila e workers de upload; numa tentativa posterior o modo Envio falha ao abrir `127.0.0.1:2121` com WinError 10048. Depois, o helper de N: relata outro processo de montagem. Não há marcadores `[STRM-WORKER-READY]`, `[STRM-SCAN-STARTED]`, mídia em fila ou transferência; o log não prova se `strm_downloader.py` foi acionado.
- Na fonte, `StartEnvioAsync` promovia o modo considerando `_isEnvioRunning`. Se a flag estivesse atrasada enquanto o host/processo ainda vivia, a chamada podia tentar criar uma segunda instância. A decisão agora usa o runtime realmente vivo sob o lock, restaura a flag lógica para um runtime reutilizável e promove stream-only antes de recriar o serviço completo.
- Testes Release focados `NebulaDownloaderStartupTests` + `NebulaHostedServiceConfigurationTests` + `NebulaSupabaseSyncTests`: 95/95, 0 ignorados/falhas. Cobertos runtime vivo/reuso/promoção, `500→200`, 503 persistente e 401 sem retry. `git diff --check` limpo. A revisão independente não identificou defeito demonstrado, mas apontou que a corrida em serviço completo não foi executada com runtime real.
- Nenhum processo, listener, serviço, configuração, mount, Mongo/Telegram, instalação ou mídia de produção foi alterado. Não houve transferência real; a colisão de porta continua exigindo identificar/solucionar o listener numa janela segura. T2.1 permanece aberto; sem release nesta rodada.

### Reauditoria da admissão de backup Supabase e uploads Nebula — 2026-10-18 08:12 (-03)

- O anexo desta rodada é a mesma amostra histórica de 516 linhas de 16–17/10. A mensagem de adiamento a cada 30 segundos não demonstra lease órfã: a fonte atual reserva o gate e aguarda leases Mongo válidas; o heartbeat Python renova a cada 30 s e a lease expira em 5 min. O log não contém IDs/donos/vencimentos para distinguir upload real de lease antiga.
- Revalidação: suíte NebulaPython 3.10 completa **592/592**; os testes C# focados em admissão compilaram, porém **3/3 foram ignorados** porque não há Mongo isolado em `127.0.0.1:27099`. Não foi usado banco de produção. A coordenação cruzada C#/Python no mesmo Mongo continua sem teste end-to-end.
- Sem alteração de código runtime, acesso ao Supabase/Telegram, produção, instalação ou release. Pendente: executar os testes de interleaving com Mongo descartável e validar uma transferência real em janela controlada. Registro: `2026-10-18-MulletaFlix-nebula-supabase-admission-audit.md`.

### Gate Mongo dos testes de release — 2026-10-18 08:54 (-03)

- O workflow de tag agora executa a validação .NET/Python/frontend em `validate-release-source` com MongoDB 7 efêmero na porta isolada 27099. O job de pacote Linux depende da validação e a publicação Windows depende do pacote Linux.
- O ambiente local não tinha serviço Mongo em 27099; por isso, embora a suíte .NET tenha encerrado com exit 0, os testes `RequiresMongo` foram ignorados. A validação completa contra Mongo é pendente do GitHub Actions; não considerar os testes de concorrência/backup Mongo comprovados por esta rodada.
- NebulaPython 592/592 e os testes web 418/418 passaram; `npm run build:check` passou. Nenhuma alteração de runtime, produção ou transferência real. O log histórico segue insuficiente para comprovar início do Downloader STRM.

### Revalidação ampla do Downloader e mount — 2026-10-18 12:00 (-03)

- Releitura do anexo de 16–17/10: `main.py` iniciou em `STREAM_ONLY`, desativando fila e workers de upload; depois houve conflito no bind FTP `127.0.0.1:2121` e repetição de mount com outro helper ativo. O trecho não inclui chamada do endpoint `StartDownloader`, saída do `strm_downloader.py`, readiness, scan da biblioteca, item enfileirado ou transferência.
- A fonte atual mantém o Downloader STRM como processo separado do serviço FTP. `mount_drive_n.py` retorna código 3 quando outro helper possui o lock; `NebulaFtpManager` pausa retry nesse caso. A discrepância do log antigo com esse contrato reforça que o runtime/log não corresponde à fonte atual, mas não prova a causa do botão de download.
- Validação Release: suíte completa da solução .NET, filtro `FullyQualifiedName!~Integration`, exit 0. Implementations 1.498 aprovados/63 ignorados; API 730/730; Providers 777/777; Server 73 aprovados/1 ignorado. NebulaPython completo 592/592. Focal Nebula mount/Downloader C# 48/48; Python (`test_mount_drive_n.py`, downloader e helpers) 92/92. Integrações Mongo sem banco descartável não foram validadas.
- Estado live read-only nesta sessão: `http://127.0.0.1:2123/health` retornou `ready`; API Jellyfin em 8096 recusou conexão. Health do stream não comprova Downloader. Nenhum serviço, processo, banco, configuração, mount ou mídia produtiva foi alterado; nenhum clique/transferência real foi observado. Instalação candidata, confirmação da versão atual e smoke test controlado seguem pendentes; não criar release antes dos gates.
- Mapa técnico conferido contra manager, controller, worker e helper: nenhum módulo, rota, classe, dependência ou fluxo mudou nesta sessão.

### Compatibilidade com worker STRM legado — 2026-10-18 12:05 (-03)

- Inspeção read-only do runtime encontrou `C:\Program Files\MulletaFlix\Server\Tools\NebulaPython\tools\strm_downloader.py`, datado de 08/10/2026, SHA-256 `3D41354345B79EAB19CB7A1FE0C01A02D0579130F18301D156CAF1C3221664C5`; a cópia da fonte tem SHA-256 `557DFF0CD6D5D1C3D1AB5806C9C86DA764690FB9378A02D17697DCBBB8942300`. A instalada não emite os marcadores atuais, mas emite `Escaneando e priorizando arquivos .strm...` após `validator.refresh_cache(force=True)`. O manager procura primeiro a cópia instalada e aguardava somente marcadores novos: incompatibilidade compatível com timeout, mas não causa comprovada pelo log sem chamada/saída do Downloader.
- `IsPythonDownloaderReadyOutput` agora aceita a mensagem legada além dos marcadores atuais. O teste regressivo falhou antes da mudança e passou depois. Suíte Release `Jellyfin.Server.Implementations.Tests`: 1.498 aprovados, 63 ignorados, 0 falhas. NebulaPython completo: 592/592. Suíte .NET da solução (pré-alteração pontual) terminou com exit 0; integrações Mongo não foram executadas localmente.
- API 8096 indisponível; health de streaming 2123 respondeu `ready`, sem provar Downloader. Nenhuma instalação, reinicialização ou transferência produtiva foi feita. Smoke test e release seguem pendentes.

### Backend indisponível no host — 2026-10-18 12:20 (-03)

- `sc.exe query MulletaFlixServer` respondeu erro 1060; instalador NSIS registra o serviço via NSSM e o inicia (`mulletaflix.nsi`, linhas 417 e 487). Não havia processo Jellyfin/MulletaFlix nem listener 8096. O processo Python mantém 2121/2123; `2123/health` HTTP 200 verifica apenas a engine.
- Motivo da ausência do serviço não determinado. Nenhum start/reparo foi executado. Downloader e release aguardam restauração do backend em janela segura e smoke test real.
- Homologação documentada no portal em `Mulletaflix-Portal:main`, commit `453da6d4ca4ea9d94818dfba155cfa43e07b7cf8`; Vercel Production `READY` (`dpl_A9YSDUkFpRHfADr9qpJe3o2tU4ce`), testes do portal 105/105 e endpoint público HTTP 200. Essa publicação não constitui release do servidor.

### Revalidação do mesmo log e dos testes de mount — 2026-10-18 13:06 (-03)

- O arquivo recebido tem 54.444 bytes, 516 linhas, timestamp 17/10/2026 19:52:55 e SHA-256 `68137C7A9B1846B15486441DC8435AF944874B4FEDB13DAD4EABFEDDD1188079`, igual ao anexo histórico já documentado. Não contém evidência nova de `StartDownloader`, readiness/scan STRM ou transferência.
- Reexecutados os testes focados atuais: `NebulaMountRuntimeHealthTests`, `NebulaDownloaderStartupTests` e `NebulaHostedServiceConfigurationTests` Release passaram 54/54; `Tools/NebulaPython/tests/test_mount_drive_n.py` Python 3.10 passou 3/3. Isso valida a fonte, não o runtime instalado.
- Observação local somente leitura: serviço `MulletaFlixServer` ausente, `MulletaFlixNginx` em execução, sem listener/API em 8096; Python escuta 2121/2123 e `/health` em 2123 retorna HTTP 200 `ready`. A saúde do stream não confirma o backend ou Downloader. Nenhum serviço/processo foi iniciado, encerrado, instalado ou configurado.
- Mapa técnico conferido contra `NebulaHostedService`, `NebulaFtpManager`, `mount_drive_n.py` e testes: sem mudança de módulos, rotas, classes, dependências ou fluxo. Próxima ação segue sendo recuperar backend e validar transferência isolada em janela operacional autorizada; sem release nesta rodada.
### Regressão ampla do servidor — 2026-10-18 13:14 (-03)

- `dotnet test MulletaFlix.sln --configuration Release --no-restore --filter "FullyQualifiedName!~Integration"` terminou com exit 0. `Jellyfin.Server.Implementations.Tests`: 1.498 aprovados, 63 ignorados, 0 falhas; `Jellyfin.Server.Tests`: 73 aprovados, 1 ignorado; `Jellyfin.Api.Tests`: 730/730; `Jellyfin.Providers.Tests`: 777/777. Demais projetos executados sem falhas reportadas. Integrações foram excluídas pelo filtro; ignorados não contam como aprovação.
- `py -3.10 -m pytest tests -q`, dentro de `Tools/NebulaPython`, passou 592/592. Tentativa inicial no diretório pai não coletou testes por resolução de imports; execução corrigida no diretório do projeto passou.
- Sem mudanças de runtime nesta rodada. API 8096 indisponível no snapshot read-only; testes locais não validam instalação nem transferência. Integrações Mongo, CI remota, smoke test e release permanecem pendentes. Mapa técnico conferido, sem mudança estrutural.
## Revalidação do log de download anexado — 2026-10-18 13:47 (-03)

- O anexo recebido nesta rodada tem 54.444 bytes, 516 linhas e o mesmo SHA-256 `68137C7A9B1846B15486441DC8435AF944874B4FEDB13DAD4EABFEDDD1188079` do log de 16–17/10 já registrado. Não acrescenta evidência de clique/endpoint `StartDownloader`, readiness, scan STRM, item na fila ou transferência.
- O código da interface aponta para `POST NebulaFtp/Actions/StartDownloader`; `NebulaHostedService.StartComponentsAsync` também tenta `StartDownloaderAsync` independentemente de falha do Envio. Isso verifica apenas o contrato da fonte, não o runtime instalado nem se a requisição ocorreu.
- Testes focados atuais: `NebulaDownloaderStartupTests` + `NebulaHostedServiceConfigurationTests` Release 47/47; `test_main_lifecycle.py` com Python 3.10 12/12. Uma tentativa inicial de pytest a partir da raiz do servidor falhou na coleta por diretório de import incorreto; repetida no diretório `Tools/NebulaPython`, passou.
- O registro histórico de `STREAM_ONLY` e colisão `WinError 10048` permanece; nenhuma causa nova pode ser atribuída ao clique sem log atual da ação. Nenhum código de produto, processo, serviço, configuração, montagem, banco ou mídia foi alterado. Release e smoke test real seguem pendentes.

### Regressão ampla do servidor — 2026-10-18 14:04 (-03)

- Solução .NET Release com filtro `FullyQualifiedName!~Integration` terminou com exit 0. Suíte `MulletaFlix.Server.Implementations.Tests`: 1.499 aprovados, 63 ignorados, 0 falhas (1.562 total). NebulaPython 3.10: `py -3.10 -m pytest tests -q`, 598/598.
- A suíte confirma somente os testes automatizados executados. Testes Mongo ignorados e projetos de integração excluídos não contam como validação integrada; ainda é necessário CI com Mongo descartável.
- Nenhum runtime/serviço foi alterado, e não foi observada chamada do Downloader ou transferência real nesta rodada. Produção e release permanecem não homologadas; smoke test controlado continua pendente.
- Mapa técnico conferido: sem alteração de módulos, APIs, classes, dependências ou fluxo nesta sessão. Sessão: `2026-10-18-MulletaFlix-server-broad-regression-1404.md`.

### Auditoria do fluxo de imagens — 2026-10-18 14:04 (-03)

- Rastreamento da fonte: provedores de metadata retornam `RemoteImages`; `MetadataService.SaveRemoteResultImages` grava essas imagens em série, e o `ImageSaver` persiste caminho e atualiza `ImageInfos`. Metadados e imagens percorrem caminhos de resultado distintos, portanto campos textuais podem persistir mesmo quando a arte falha.
- Defeito potencial de sobrescrita confirmado: o filtro seleciona todas as imagens remotas antes de iniciar gravações e não limita imagens singulares por tipo. Para `Primary`, o `ImageSaver` resolve índice 0; portanto múltiplas capas `Primary` no mesmo resultado do provedor gravam repetidamente o mesmo arquivo lógico e a última substitui as anteriores. Há provedores de livros que podem retornar múltiplas capas `Primary`.
- Defeito de observabilidade confirmado: após até três tentativas, a falha do download da imagem é capturada e apenas registrada como warning; não incrementa `RefreshResult.Failures`. Isso permite atualizar metadados e concluir a atualização sem sinalizar a falha de imagem neste estágio.
- Limite da confirmação: não havia log de produção atual nem resposta de imagem/API para os títulos do screenshot. Assim, os defeitos de fonte explicam casos compatíveis, mas não atribuo a eles todos os posters azuis nem confirmo que cada item afetado tenha recebido múltiplas imagens. Nenhuma produção, item ou imagem foi alterado.
- Verificações: fluxo percorrido em `MetadataService`, `ProviderManager`, `ImageSaver`, `ItemImageProvider` e `BaseItem.ValidateImages`; suítes servidor/Python já concluídas nesta sessão. Testes de regressão específicos de múltiplas imagens Primary e falha remota ainda não existem. Mapa técnico conferido: sem mudança de estrutura. Detalhes: `docs/image-metadata-flow-audit.md`.

## Retry durável do refresh VFS

Após upload concluído, o caminho virtual é normalizado e persistido na coleção Mongo `nebula_directory_refreshes` antes de entrar na fila de refresh do rclone. A fila remove o registro persistido somente após a confirmação do refresh do diretório ou dos ancestrais; exceções de rede, recusas do rclone e falta de readiness são erros para a fila, que retenta após 30 segundos. No próximo ciclo imediato/periódico, registros ainda pendentes são reidratados, incluindo os deixados por uma parada do servidor. Diretórios são deduplicados por `_id` canônico.

O caminho persistido deve estar sob uma raiz visível (Animações, Filmes, Séries, Doramas, Novelas ou Porno) e rejeita segmentos de traversal. A coleção é criada por `EnsureSchemaAsync`; não requer instalação manual. A varredura de `completed_at` de 30 minutos continua como reconciliação secundária para uploads cujo marker não pôde ser persistido. Se Mongo estiver indisponível ao gravar, refresh atual segue em memória e a janela de 30 minutos pode recuperar a conclusão quando Mongo voltar; para falha além da janela, esse marker ainda não existe.

Validação local Release Windows/.NET 10: testes Nebula 716 aprovados, 26 ignorados, 0 falhas/742. O teste de persistência entre contextos exige Mongo descartável e foi ignorado nesta execução; CI, runtime instalado e produção pendentes.

### Complemento — snapshot da fila persistente no Supabase

A tabela `nebula_directory_refreshes` foi adicionada ao SQL de instalação Supabase com RLS apenas para `service_role`. Backup sincroniza o conjunto atual por caminho canônico e remove rows remotas concluídas; restore reidrata paths usando o mesmo guard de raiz visível. Resultado de backup/restore expõe contagens de refreshes sincronizados/restaurados. Suíte ampla `FullyQualifiedName~Nebula`: 718 aprovados, 26 ignorados, 0 falhas/744; os testes Mongo opt-in não executaram sem Mongo disponível. Schema remoto real, CI hospedada, runtime instalado e produção seguem pendentes.

### Revalidação com Mongo descartável e schema instalado — 2026-10-28

MongoDB 8.3.7 isolado em `127.0.0.1:27099` habilitou todos os testes da suíte Nebula: 744 aprovados, 0 ignorados, 0 falhas. A migration `20261028125319` foi aplicada ao Supabase `Mulletaflix-Nebula`; tabela vazia e ACL/RLS conferidos por catálogo: apenas `service_role` tem SELECT/INSERT/UPDATE/DELETE. HTTP de sync/restore continuou mockado; CI, runtime instalado e execução de backup de produção não foram validados.

### 28/10/2026 — Remediação dos advisors Supabase

A migration `20261028130514_supabase_advisor_cleanup` fixa `search_path` vazio na trigger `set_nebula_updated_at`, remove o índice duplicado `idx_mulletaflix_users_username` e elimina `idx_nebula_files_parent`, sem scans e sem callers do campo no código. A uniqueness canônica `mulletaflix_users_normalized_username_key` permanece; C# setup cria/garante essa chave sem a duplicata e C#/Python setup não recriam `parent`. Advisors security e performance retornaram sem findings. Testes C# e Python focais passaram 1/1 cada. CI hospedada, runtime instalado e produção do servidor seguem pendentes.
