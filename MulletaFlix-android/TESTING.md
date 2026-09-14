# Testes do MulletaFlix Android

## Comandos

```powershell
gradle test
gradle :app:connectedDebugAndroidTest
```

## Cobertura automatizada atual

| Área | Cenários | Tipo |
| --- | --- | --- |
| Mapeamento de mídia | tipo, imagens, 4K, HD, HDR, Dolby Vision, Atmos | JVM |
| Cache offline | conversão entidade/domínio e progresso de reprodução | JVM |
| Busca | debounce, filtro por filmes e histórico limitado a 10 itens | JVM |
| Seleção de servidor | logo, URL, ação de descoberta na rede | Instrumentado Compose |
| Build | variantes debug e release | Gradle |

## Validação dependente de ambiente

Os fluxos abaixo estão implementados no aplicativo, mas precisam de um servidor Mulletaflix de teste com usuário, bibliotecas e mídia para uma validação end-to-end sem dados falsos:

- autenticação por usuário e Quick Connect;
- home, bibliotecas, detalhes, temporadas e episódios;
- reprodução, retomada e envio de progresso;
- legendas, faixas de áudio, qualidade e Cast;
- Live TV, gravações, downloads e SyncPlay;
- logout, cache, preferências e troca de servidor.

Esses cenários devem ser executados com dados de teste controlados; credenciais reais não devem ser armazenadas nos testes ou relatórios.
