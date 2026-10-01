# Matriz de Compatibilidade de Codecs, Containers e Transcodificação

Este documento consolida a matriz técnica de compatibilidade de reprodução (Direct Play, Direct Stream / Remux e Transcodificação) do MulletaFlix, validada pelos testes automatizados e pela implementação em `MediaBrowser.Controller.MediaEncoding.EncodingHelper`, `TranscodeManager` e `ServerHealthController`.

---

## 1. Modos de Reprodução

| Modo | Condições | Custo de CPU/GPU | Preservação de Qualidade |
| :--- | :--- | :--- | :--- |
| **Direct Play** | O container, codec de vídeo, codec de áudio e legendas são suportados nativamente pelo cliente (ex.: MP4 H.264/AAC no browser; MKV HEVC/TrueHD no Android TV com ExoPlayer). | Zero (apenas I/O de rede e leitura em disco) | 100% bit-exact original |
| **Direct Stream (Remux)** | Os codecs de vídeo e/ou áudio são suportados, mas o container precisa de encapsulamento compatível (ex.: MKV -> HLS/fMP4) ou áudio precisa de transcode mantendo vídeo intacto (`-c:v copy`). | Mínimo (apenas remux ou transcode leve de áudio) | Vídeo 100% bit-exact original |
| **Transcoding Completo** | Codec de vídeo incompatível, bitrate superior ao limite de banda remota, desentrelaçamento necessário ou legendas gráficas com queima forçada (`SubtitleDeliveryMethod.Encode`). | Alto (requer aceleração por hardware GPU ou fallback CPU) | Re-encoding adaptativo |

---

## 2. Matriz de Compatibilidade por Codec e Container

### Containers de Entrada Suportados
- **MKV (Matroska)**: Suporta múltiplos streams de vídeo, áudio multicanal (AAC, AC3, EAC3, DTS, TrueHD, FLAC, Opus) e legendas de texto (SRT, ASS/SSA, WebVTT) e gráficas (PGS/SUP, VobSub).
- **MP4 / M4V / MOV**: Suporta H.264, HEVC, AV1, VP9; áudio AAC, AC3, EAC3; legendas `mov_text`.
- **MPEG-TS (`.ts`)**: Fluxos de TV ao vivo e HLS legados.
- **WebM**: Suporta VP8, VP9, AV1; áudio Vorbis, Opus.
- **AVI / WMV**: Legados; requerem remux ou transcodificação conforme perfil do cliente.

### Decisão de Cópia de Stream de Vídeo (`CanStreamCopyVideo`)
O vídeo é preservado sem recompressão (`-c:v copy`) quando:
1. `AllowVideoStreamCopy == true` na requisição do cliente.
2. O codec de origem está listado em `state.SupportedVideoCodecs`.
3. O perfil (Profile) e nível (Level) do codec não excedem o limite suportado pelo cliente.
4. O vídeo não é entrelaçado quando `DeInterlace` está ativo.
5. **Legendas:** Nenhuma queima de legenda gráfica (`SubtitleDeliveryMethod.Encode`) foi solicitada. Queimar legenda no vídeo sempre exige decodificação, filtro e re-encoding.

### Decisão de Cópia de Stream de Áudio (`CanStreamCopyAudio`)
O áudio é mantido sem recompressão (`-c:a copy`) quando:
1. `AllowAudioStreamCopy == true` e `EnableAutoStreamCopy == true`.
2. O codec de áudio está presente na lista de codecs suportados pelo cliente (`supportedAudioCodecs`).
3. O número de canais não excede `MaxAudioChannels` (ex.: se o cliente suporta apenas estéreo 2.0 e o arquivo possui áudio 5.1/7.1, é acionado downmix para estéreo).
4. O sample rate e bitrate não ultrapassam as restrições negociadas.

---

## 3. Gestão e Sincronismo de Legendas

| Formato | Tipo | Estratégia Recomendada | Comportamento no Remux / Transcode |
| :--- | :--- | :--- | :--- |
| **SRT / WebVTT** | Texto | Drop / External / Embed | Entregue via VTT externo via HTTP ou muxado no fluxo HLS. Não bloqueia direct copy de vídeo. |
| **ASS / SSA** | Texto estilizado | External (Web) / Burn-in (Fallback) | Renderizado via libass / Javascript no cliente Web; se cliente não suportar estilos avançados, fallback para queima forçada via `-vf subtitles`. |
| **PGS / SUP (Blu-ray)** | Gráfico (bitmap) | Direct Play (Android TV) / Encode (Web) | Clientes Android TV renderizam nativamente; Web browsers exigem queima de legenda (`SubtitleDeliveryMethod.Encode`), forçando transcode de vídeo. |
| **VobSub (`.sub`/`.idx`)** | Gráfico (bitmap) | Encode / Direct Play | O arquivo `.idx` é localizado e passado ao FFmpeg via `-i path.idx` para sincronismo de cores e temporização de frames. |

---

## 4. Busca, Seek e Sincronismo de Timestamp

O método `GetFastSeekCommandLineParameter` garante busca rápida e precisa:
1. **Offset de Keyframe no HLS Remuxing:** Em remux HLS (`TranscodingJobType.Hls` e `OutputVideoCodec == "copy"`), um offset de `+0.5s` (5.000.000 ticks) é adicionado ao tempo de busca `-ss`. Isso força o FFmpeg a posicionar o corte exatamente no keyframe de destino, evitando descompasso entre áudio, vídeo e legenda.
2. **Clamping Próximo ao Fim (EOF):** O seek é travado em `Math.Clamp(seekTick, 0, Math.Max(maxTime - 50000000L, 0))` (Runtime - 5 segundos) para evitar que buscas no final do arquivo façam o demuxer falhar com EOF sem pacotes gerados.

---

## 5. Limitação e Visibilidade de Concorrência de Transcodes

- **Controle Centralizado:** `TranscodeManager.ActiveTranscodingJobsCount` expõe em tempo real o total de jobs ativos.
- **Configuração:** `EncodingOptions.MaxConcurrentTranscodingJobs` define a cota máxima permitida (padrão `0` = ilimitado).
- **Diagnóstico:** Exposto em `GET /ServerHealth/Encoding` e `GET /ServerHealth/Summary` para monitoramento via painel ou ferramentas externas.
- **Cancelamento Limpo:** Quando uma sessão é interrompida pelo usuário ou o buffer do cliente é preenchido, os processos FFmpeg são finalizados via `Process.Kill(entireProcessTree: true)`, liberando imediatamente handles de arquivo e instâncias de encoder GPU.

---

## 6. Evidências de Teste Automatizado

A conformidade desta matriz é validada por testes xUnit no repositório:
- `tests/Jellyfin.Controller.Tests/MediaEncoding/EncodingHelperTests.cs` (62 testes):
  - Validação de cópia de vídeo (H.264 direto, bloqueio por queima de legenda, bloqueio por entrelaçamento, bloqueio por codec não suportado).
  - Validação de cópia de áudio (codec suportado, recusa de codec incompatível, restrição de canais para downmix).
  - Cálculo de seek rápido (tempo zero, transcode progressivo, offset HLS +0.5s e clamping de EOF).
- `tests/Jellyfin.MediaEncoding.Tests/Transcoding/TranscodeManagerTests.cs` (67 testes):
  - Concorrência de jobs e isolamento de processos.
- `tests/Jellyfin.Server.Implementations.Tests/MediaEncoding/HardwareDetectionServiceTests.cs` (2 testes):
  - Fallback automático para software CPU quando hardware não está disponível.
