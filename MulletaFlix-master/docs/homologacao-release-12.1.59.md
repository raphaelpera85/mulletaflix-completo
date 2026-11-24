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
- Runtime local antes da recuperação: `MulletaFlix.exe` não executava; `/health` em `127.0.0.1:8096` recusava conexão e o HTTPS via Nginx retornava 502. Após solicitação, o usuário iniciou manualmente o tray; o processo `MulletaFlix.exe` (PID 15868) passou a escutar em `0.0.0.0:8096`, versão pública 12.1.58, `/health` local e via Nginx 200 `Healthy`.
- O comando HKCU Run estava sem aspas. Foi corrigido em produção para `"C:\Program Files\MulletaFlix\Server\mulletaflix-windows-tray\MulletaFlix.Windows.Tray.exe"`; a gravação foi lida de volta e conferida. A correção de código do tray ainda não está instalada.
- No início da auditoria, o builder apontava para o tray no checkout separado ignorado. A origem foi corrigida mais tarde e está demonstrada no pacote candidato descrito abaixo. Permanecem pendentes health pós-atualização, integrações com bancos descartáveis e conclusão das pendências do roadmap.

## Build candidato Windows — 2026-11-24

- A origem do tray no builder foi corrigida para compilar o projeto versionado no repositório principal. TDD do caminho de origem e teste do diretório isolado do instalador passaram. Tray empacotado e binário Release compilado têm SHA-256 idêntico (`FD610BD58D069DFAF8A45CF019ECDF14E9BE5A17BED23A9F4CC1D35C8CA33CB3`).
- Stage Windows Release passou integridade e contém `MulletaFlix.exe` versão 12.1.59. A primeira execução imprimiu sucesso mas retornou 1 porque códigos Robocopy 1–7, tratados como sucesso, vazavam em `$LASTEXITCODE`. A regressão passou RED/GREEN; após zerar o status global para os casos aceitos, o stage builder executou novamente e terminou com código 0. Os três testes de builder passaram.
- ZIP final de atualização: `.build/audit-release-12.1.59-2026-11-24/final-assets/mulletaflix-update-win-x64.zip`, 419.079.115 bytes, SHA-256 `A491C58C52606460DB1E508089A8F86FA76060161AFE6E909AD8094AA4A11CB3`. `tar -tf` leu 3.265 entradas e retornou 0; confirmou servidor, atualizador, tray e worker Python.
- Instalador: `.build/audit-release-12.1.59-2026-11-24/assets/mulletaflix_12.1.59_windows-x64.exe`, 453.916.222 bytes, SHA-256 `23293D26DC650981E15DF3AA3D00FE39004EE5483679BF806A33174A8D463637`; Authenticode `NotSigned`. Não houve instalação do candidato.
- Health local e pelo Nginx continuam `Healthy` na instalação existente 12.1.58. A homologação pós-atualização não ocorreu.
- A publicação está suspensa pelo gate obrigatório da seção 4.0 do `mulletaflix-conventions.md`: o roadmap ainda contém melhorias ativas. Além disso, `gh release view` não pôde autenticar (GitHub CLI sem sessão). A release não está criada/publicada nem aplicada à produção; os hashes acima identificam somente os artefatos candidatos locais.
