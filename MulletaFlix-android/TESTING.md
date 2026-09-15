# Testes do MulletaFlix Android

## Comandos

```powershell
.\gradlew.bat test
.\gradlew.bat assembleDebug
.\gradlew.bat :app:connectedDebugAndroidTest
```

## Cobertura automatizada atual

| Área | Cenários | Tipo |
| --- | --- | --- |
| UseCases de Domínio (`:domain`) | Obtenção de perfil, logout, detalhes de mídia, toggle favorito, toggle assistido | JVM |
| Perfil de Usuário (`:feature:user`) | Carregamento de perfil, fallback de sessão offline, alternância rápida de usuário, limpeza de cache e logout | JVM |
| Detalhes do Item (`:feature:item-detail`) | Filmes, séries, temporadas/episódios, faixas de álbuns, favoritos, assistidos, playlists e erros | JVM |
| Mapeamento de mídia (`:data`) | Tipo, imagens, 4K, HD, HDR, Dolby Vision, Atmos | JVM |
| Cache offline & Downloads | Conversão entidade/domínio, progresso de reprodução, fila de download | JVM |
| Busca (`:feature:search`) | Debounce, filtro por filmes/mídias e histórico limitado a 10 itens | JVM |
| Home & Descoberta (`:feature:home`) | Carregamento assíncrono, expiração de sessão e seções dinâmicas | JVM |
| Biblioteca (`:feature:library`) | Filtros, ordenação e navegação paginada | JVM |
| Player (`:feature:player`) | Pular capítulos, gestos de volume/brilho, PiP, recuperação e seleção de faixas | JVM |
| Seleção de servidor | Logo, URL, ação de descoberta na rede | Instrumentado Compose |
| Build & Packaging | Variantes debug (APK gerado com sucesso) e release | Gradle |

## Validação dependente de ambiente

Os fluxos abaixo estão implementados no aplicativo, mas precisam de um servidor Mulletaflix de teste com usuário, bibliotecas e mídia para uma validação end-to-end sem dados falsos:

- autenticação por usuário e Quick Connect;
- home, bibliotecas, detalhes, temporadas e episódios;
- reprodução, retomada e envio de progresso;
- legendas, faixas de áudio, qualidade e Cast;
- Live TV, gravações, downloads e SyncPlay;
- logout, cache, preferências e troca de servidor.

Esses cenários devem ser executados com dados de teste controlados; credenciais reais não devem ser armazenadas nos testes ou relatórios.
