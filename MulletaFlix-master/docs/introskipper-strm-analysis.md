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
de timeout/concorrência não substitui a disponibilidade do relay. Não iniciar
o launcher de produção para homologar: ele também pode iniciar manutenção e
limpeza de arquivos. Requer relay de staging isolado antes de executar análise
de mídia remota. A release protege falhas por item, mas não pode fingerprintar
um endpoint indisponível.
