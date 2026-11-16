# IntroSkipper: análise de mídia `.strm`

O limite `ScanTimeoutSeconds`, disponível na configuração FFmpeg do IntroSkipper,
é aplicado aos scans de áudio/vídeo. Para caminhos `.strm`, scans e probes FFprobe
têm piso de 900 segundos para permitir que servidores remotos iniciem a
transmissão; valores configurados acima disso continuam valendo. Zero ou valor
negativo mantém os scans `.strm` ilimitados. Probes FFprobe de mídia local
continuam com o limite de 10 segundos.

`MaxParallelism` controla quantos arquivos de mídia são analisados ao mesmo
tempo. O valor efetivo fica entre 1 e 2, inclusive. O campo da aba FFmpeg é
rotulado como “Concurrent media files”.

Implementação em `MulletaFlix.Plugin.IntroSkipper/FFmpeg/FFmpegService.cs` e
`MulletaFlix.Plugin.IntroSkipper/Configuration/PluginConfiguration.cs`; a UI é
`MulletaFlix.Plugin.IntroSkipper/Configuration/introskipper.js`. Os testes de
conversão do timeout estão em
`tests/IntroSkipper.Integration.Tests/FFmpegAudioDurationTests.cs`.

## Evidência de runtime Windows — 2026-11-16

Uma probe de um `.strm` real retornou conexão recusada ao relay HTTP local em
`127.0.0.1:2124`; não havia listener. A configuração integrada do MulletaFlix
estava desabilitada e configurada para `HttpStreamPort=2123`, enquanto esse
arquivo foi gerado pela ferramenta Nebula separada com porta 2124. A correção
de timeout/concorrência não substitui a disponibilidade do relay. Na primeira
sondagem, o launcher não foi iniciado porque também pode iniciar manutenção e
limpeza de arquivos; depois, o usuário autorizou seu uso e o relay já estava
ativo quando a validação foi retomada. A release protege falhas por item, mas
não pode fingerprintar um endpoint indisponível.

Após autorização para iniciar o Nebula de produção, o relay 2124 foi observado
ativo. FFprobe leu um `.strm` real e dois episódios reais, fingerprintados
simultaneamente pelo FFmpeg, retornaram 705 pontos cada; uma comparação local
encontrou sequência compartilhada aproximada de 123 pontos (distância Hamming
≤4). Isso verifica o transporte HTTP e fingerprints com FFmpeg; não executa o
comparador/persistência do IntroSkipper nem altera o catálogo de produção. O
launcher já estava ativo quando a probe começou; não foi iniciada outra cópia.

### E2E opt-in com STRM real

O teste `IntroSkipperMediaAnalysisE2ETests.AnalyzeStrmEpisodes_PersistsSharedIntrosAndCapsConcurrentFingerprints`
usa dois arquivos locais `.strm` apontados por `MULLETAFLIX_TEST_REAL_STRM_PATHS`
quando a variável está definida. Ele usa somente o MariaDB descartável loopback
exigido por `MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING` e
`MULLETAFLIX_TEST_MARIADB_CONFIRM_DISPOSABLE`; não use conexão da instância do
servidor. Sem a variável de STRM real, o teste conserva os fixtures sintéticos.

Evidência de 2026-11-16: dois STRMs reais processados pelo caminho completo
`BaseItemAnalyzerTask -> ChromaprintAnalyzer -> FFmpegService ->
StrmInputResolver -> FFmpeg`; fingerprints não vazios e segmento Chromaprint
persistido por item no banco descartável. Concorrência observada 1, dentro do
teto 2. O cenário sintético continua exercitando duas análises simultâneas.
Suíte completa IntroSkipper passou 125/125, sem ignorados, com capacidade do
MariaDB de teste configurada em 512 conexões. O banco foi encerrado após o teste.
