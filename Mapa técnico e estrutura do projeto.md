
### Conferência 2026-10-29 19:14 BRT — revisão do scanner

Fluxo existente em `Tools/NebulaPython/main.py`: recuperação de claims `staging` agora alcança identificadores persistidos por processo anterior e libera o claim guardado; retry de recuperação aplica cinco segundos de backoff. Sem API, módulo, dependência ou schema novo. Cobertura: suíte Nebula Python 690/690. Estrutura conferida e permaneceu igual.

### Conferência 2026-11-02 23:10 BRT — redação de falhas Nebula

`NebulaDownloaderEngine` mantém a mesma responsabilidade/API externa. O helper interno `RedactSensitiveData` sanitiza URL credentials em logs de erro visíveis e nos motivos do `FailureTracker`; nenhuma dependência ou fluxo público novo. Cobertura focal: downloader 50/50 em Windows/.NET 10 Release.

## 02/11/2026 23:26 BRT — Live TV/IPTV: redação de URLs nos logs

`LiveTvUrlRedactor` agora oferece `RedactText`, helper interno que redige URLs HTTP(S) em mensagens de exceção. `TunerHostManager`, `IptvOrgEpgSynchronizer` e `XmlTvListingsProvider` usam o fluxo antes do logger; nenhuma API pública, dependência ou módulo novo. Testes focais: 19/19 Live TV e 11/11 Providers em Windows/.NET 10 Release. A auditoria de outros sinks, Linux e produção permanece aberta. Ver [[Sessões/2026-11-02-MulletaFlix-livetv-url-log-redaction-2326]].

## 2026-11-02 — redação de URLs em logs de provedores

O redator interno de MidiaStorageOnline também é usado por NartoDramaClient, GoodShortClient e provedores YouTube para retirar URLs e URLs embutidas em exceções dos logs. GuideManager usa LiveTvUrlRedactor. O fluxo HTTP e os metadados não mudam. Testes de provedores focal: 62/62; Live TV focal: 15/15. Sem APIs, módulos ou dependências novos. Outros sinks seguem em auditoria. Ver sessão 2026-11-02-MulletaFlix-livetv-url-log-redaction-2326.
### 2026-11-02 — Continuação da redação de URLs remotas

`MediaBrowser.Providers/Helpers/RemoteUrlLogRedactor` é helper compartilhado pelos providers e pelo `ItemImageProvider`; testes ficam em `tests/Jellyfin.Providers.Tests/Manager` e nos testes de ListenBrainz. As chamadas em MidiaStorageOnline, NartoDrama, GoodShort, YouTube, DramaBox, ListenBrainz e ItemImageProvider redigem valores de URL/exceção antes de logar ou devolver erro de refresh. Módulos e dependências existentes; novo helper interno substitui o redator específico anterior. Evidência focal 150/150 e contrato portal 361/361 em Windows. Busca ampliada encontrou exceções ainda a revisar em ShortMax e no tratamento de erro de imagens do YouTube; cobertura completa dos sinks não foi demonstrada. Linux/runtime/produção pendentes. `last_verified: 2026-11-02`.
### 2026-11-02 — redação de exceções de transporte nos providers

`RemoteUrlLogRedactor` é reutilizado por ShortMax, YouTube metadata/imagens, ListenBrainz Similar, TMDb Similar Movies/TV e GoodShort JSON parsing, além dos callers registrados acima. Providers focal acumulativo 152/152; formatter aprovado. A busca por `_logger.Log*(ex, ...)` nos plugins deixou dois logs com contexto local, mas não audita sinks indiretos ou outras camadas. Nenhuma dependência ou API foi adicionada. Microsoft Learn descreve pipeline classificado com `Microsoft.Extensions.Telemetry`; sua compatibilidade permanece pendente. `last_verified: 2026-11-02`.
### 2026-11-02 — ampliação da redação de URLs em logs

Os limites de exceção em MediaBrowser.Providers/Manager/ProviderManager.cs, MetadataService.cs, provedores OpenLibrary, letras, legendas, src/Jellyfin.LiveTv/Guide/GuideManager.cs e Schedules Direct sanitizam URLs remotas antes do logger. Schedules Direct deixou de registrar resposta HTTP bruta. Verificações: Providers 893/906 (13 ignorados), Live TV 81/81; formatter parcial por avisos de codificação CHARSET em dois arquivos legados. T6.4 parcial, restante dos logs ainda em triagem. last_verified: 2026-11-02.