# Observabilidade OpenTelemetry

O MulletaFlix pode exportar traces e métricas OTLP para um OpenTelemetry Collector ou outro destino compatível. A integração é opcional: sem endpoint OTLP configurado, não inicia exporters nem envia telemetria. O endpoint Prometheus existente continua disponível independentemente desta configuração.

## Configuração

Defina as variáveis no ambiente do processo do servidor, antes de iniciá-lo. Não grave tokens ou credenciais no repositório.

Para enviar traces e métricas ao mesmo collector, defina `OTEL_EXPORTER_OTLP_ENDPOINT`. O SDK .NET usa gRPC por padrão; por exemplo, `http://127.0.0.1:4317`.

Para OTLP/HTTP, defina também `OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf` e use o endpoint HTTP do collector, normalmente `http://127.0.0.1:4318`.

É possível configurar sinais separadamente com `OTEL_EXPORTER_OTLP_TRACES_ENDPOINT` e `OTEL_EXPORTER_OTLP_METRICS_ENDPOINT`. Os endpoints e protocolo seguem as variáveis padrão do exporter OTLP .NET.

## Collector remoto autenticado

Para collector fora do próprio host, use endpoint `https://` com certificado TLS válido. O exporter aceita autenticação por cabeçalhos padrão OpenTelemetry; defina o segredo no ambiente protegido do serviço, nunca no repositório:

```text
OTEL_EXPORTER_OTLP_ENDPOINT=https://collector.example:4318
OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf
OTEL_EXPORTER_OTLP_HEADERS=Authorization=Bearer%20<token>
```

`OTEL_EXPORTER_OTLP_TRACES_HEADERS` e `OTEL_EXPORTER_OTLP_METRICS_HEADERS` permitem fornecer credenciais diferentes por sinal. O formato dos cabeçalhos é uma lista `chave=valor` separada por vírgulas, conforme a especificação OpenTelemetry. Não use HTTP remoto para enviar credenciais. Para autenticação por certificado de cliente, o exporter também reconhece `OTEL_EXPORTER_OTLP_CERTIFICATE`, `OTEL_EXPORTER_OTLP_CLIENT_CERTIFICATE` e `OTEL_EXPORTER_OTLP_CLIENT_KEY`; proteja o arquivo de chave privada com permissões restritas. Consulte a [configuração oficial do exporter OTLP](https://opentelemetry.io/docs/languages/sdk-configuration/otlp-exporter/) e a [documentação do exporter OpenTelemetry .NET](https://opentelemetry.io/docs/languages/dotnet/exporters/).

O teste `MulletaFlixOpenTelemetryExportTests` envia um trace de teste a um receptor local que recusa requisições sem `X-API-Key`; isso comprova o encaminhamento de headers pelo exporter, não valida TLS, política ou retenção de um collector de produção.

## Fontes de trace registradas

| Fonte (`ActivitySource`) | Cobertura |
| --- | --- |
| `MulletaFlix.Nebula.HttpStreamServer` | Requisições do listener HTTP Nebula, com propagação W3C `traceparent`/`tracestate`. |
| `MulletaFlix.Nebula.UploadEngine` | Ciclo de upload de mídia para o Telegram. |
| `MulletaFlix.Nebula.DownloaderEngine` | Downloads multipart de STRM e arquivos físicos. |
| `MulletaFlix.Nebula.TelegramPool` | Download de chunks e envios (`sendDocument`, `sendPhoto`, `sendMessage`). |
| `MulletaFlix.Nebula.MongoContext` | Consultas e escritas MongoDB, transições da fila de upload e varredura de staging. |
| `MulletaFlix.Nebula.PlaybackSession` | Início, troca e parada de sessões de reprodução. |

Além destas, a instrumentação automática do ASP.NET Core cobre as requisições da API.

## Dados coletados

- Traces de requisições ASP.NET Core, listener HTTP Nebula, ciclos de upload/download Nebula, operações MongoDB, varredura de staging, chamadas Telegram e sessões de reprodução. Os spans Nebula correlacionam pelo `traceparent`; o `tracestate` recebido é preservado quando válido.
- Métricas de requisições ASP.NET Core e transferências Nebula: total e duração, com resultado de baixa cardinalidade (`success`, `failure` ou `cancelled`). As métricas Prometheus existentes do listener Nebula continuam no endpoint `/metrics`.
- Admissão HTTP exporta `mulletaflix.http.admission.rejected` por categoria fixa; operações de backup/restauração exportam `mulletaflix.backup.operations`, duração e operações ativas, com dimensões limitadas a tipo da operação e resultado. O sync Nebula MongoDB↔Supabase exporta `mulletaflix.supabase.operations`, duração e operações ativas para sync delta/completo e restauração; as dimensões são apenas `operation` e `result`. Nenhuma dessas métricas registra IP, URL, chave, usuário, caminho ou nome de arquivo.
- Traces não incluem caminho, query string ou user-agent da requisição. Exceções não são anexadas como eventos; o tipo do erro e o status HTTP permanecem disponíveis.
- Títulos, nomes de mídia, tokens, credenciais e IDs de usuários não são adicionados como tags desta instrumentação.
- O span e as métricas do upload não incluem filename, caminho local, pasta, ID Telegram ou ID MongoDB.
- O span e as métricas do download não incluem URL, query, filename ou caminho local.
- Os spans MongoDB usam apenas o nome da operação (`mongodb.*`) e um resultado de baixa cardinalidade; string de conexão, nome de banco, coleção, caminho virtual, nome de mídia, login e identificadores de documento não são atributos.
- As transições da fila de upload registram o estado canônico da máquina de estados em `nebula.queue.state` e a etapa normalizada da falha em `nebula.upload.failure_stage`; o motivo textual da falha, que pode conter caminho ou nome de mídia, nunca é exportado.
- A varredura de staging registra apenas `nebula.staging.root_count`; as raízes e os arquivos varridos não são atributos.
- Os spans de sessão de reprodução registram resultado, se o pré-cache foi cancelado e a contagem de sessões ativas; caminho da mídia, `sessionId` e `playSessionId` não são atributos.
- As métricas do sync Supabase usam somente os tipos fixos `mongodb_delta_sync`, `mongodb_full_sync`, `restore_users`, `backup_app_users` e `restore_app_users`, e os resultados `success`, `failure` ou `cancelled`; credenciais e endpoints não são atributos.
- Os follow-ups de exportação de metadados reconhecidos pelo Nebula exportam `mulletaflix.metadata_export.operations`, duração e operações ativas, com apenas `result=success|failure|cancelled`; título, caminho e ID de mídia não são tags.
- O ciclo periódico de limpeza do Nebula exporta `mulletaflix.nebula.cleanup.cycles`, duração e ciclos ativos; `result` é `success`, `partial_failure`, `cancelled`, `failure` ou `skipped`. Raízes, caminhos e nomes de arquivos não são tags.

## Limites atuais

Não há spans automáticos para MariaDB. Métricas operacionais do cache e das filas ainda não são exportadas pelo OpenTelemetry — o painel continua sendo a fonte desses indicadores.

A instrumentação descrita acima é verificada por testes automatizados que inspecionam os spans emitidos e afirmam a ausência de dados sensíveis nos atributos. O **pipeline de exportação** também é validado por teste de integração (`MulletaFlixOpenTelemetryExportTests`): o SDK OpenTelemetry real é construído com exporter OTLP/HTTP contra um receptor em processo, que confirma `POST /v1/traces` com `application/x-protobuf`, os nomes de operação no payload codificado e a ausência de dados sensíveis. Um teste complementar garante que, sem `OTEL_*`, nenhum exporter é registrado e `StartActivity` devolve `null`.

O que esses testes **não** cobrem é a política de retenção e a experiência de consulta no backend de observabilidade: confirmar TLS, aceitação/rotação de credencial, amostragem efetiva, retenção e correlação entre serviços exige apontar o servidor para um collector real de produção e inspecionar os traces lá. Validar o endpoint local não prova disponibilidade nem política de retenção do destino remoto.
