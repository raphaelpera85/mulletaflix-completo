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
