# Observabilidade OpenTelemetry

O MulletaFlix pode exportar traces e métricas OTLP para um OpenTelemetry Collector ou outro destino compatível. A integração é opcional: sem endpoint OTLP configurado, não inicia exporters nem envia telemetria. O endpoint Prometheus existente continua disponível independentemente desta configuração.

## Configuração

Defina as variáveis no ambiente do processo do servidor, antes de iniciá-lo. Não grave tokens ou credenciais no repositório.

Para enviar traces e métricas ao mesmo collector, defina `OTEL_EXPORTER_OTLP_ENDPOINT`. O SDK .NET usa gRPC por padrão; por exemplo, `http://127.0.0.1:4317`.

Para OTLP/HTTP, defina também `OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf` e use o endpoint HTTP do collector, normalmente `http://127.0.0.1:4318`.

É possível configurar sinais separadamente com `OTEL_EXPORTER_OTLP_TRACES_ENDPOINT` e `OTEL_EXPORTER_OTLP_METRICS_ENDPOINT`. Os endpoints e protocolo seguem as variáveis padrão do exporter OTLP .NET.

## Dados coletados

- Traces de requisições ASP.NET Core, listener HTTP Nebula e ciclos de upload/download Nebula. Os spans Nebula correlacionam pelo `traceparent`; `tracestate` recebido é descartado para não exportar conteúdo opaco do chamador.
- Métricas de requisições ASP.NET Core e transferências Nebula: total e duração, com resultado de baixa cardinalidade (`success`, `failure` ou `cancelled`). As métricas Prometheus existentes do listener Nebula continuam no endpoint `/metrics`.
- Traces não incluem caminho, query string ou user-agent da requisição. Exceções não são anexadas como eventos; o tipo do erro e o status HTTP permanecem disponíveis.
- Títulos, nomes de mídia, tokens, credenciais e IDs de usuários não são adicionados como tags desta instrumentação.
- O span e as métricas do upload não incluem filename, caminho local, pasta, ID Telegram ou ID MongoDB.
- O span e as métricas do download não incluem URL, query, filename ou caminho local.

## Limites atuais

Esta etapa não adiciona spans automáticos para MariaDB, MongoDB, varreduras, chamadas Telegram ou sessões de reprodução. Métricas operacionais do cache e das filas ainda não são exportadas pelo OpenTelemetry. Validar entrega e retenção requer um collector ativo; validar o endpoint local não prova disponibilidade nem política de retenção do destino remoto.
