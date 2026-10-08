# Plano de TDD do MulletaFlix Android

## Escopo e situação

Este plano cobre todo o aplicativo Android: `:app`, `:core:api`, `:core:common`, `:core:testing`, `:data`, `:design-system`, `:domain` e todos os `:feature:*` registrados em `settings.gradle.kts`.

O projeto já tem suítes JVM e instrumentadas em todos os módulos de produto. A existência de testes ou a aprovação da suíte não prova que testes legados foram escritos antes da implementação. Não há evidência histórica RED-GREEN consolidada para o legado. A partir da adoção deste plano, cada mudança executável deve registrar e demonstrar o ciclo TDD; os comportamentos legados serão protegidos quando forem alterados e as lacunas abaixo serão fechadas por prioridade.

Não reportar percentual de cobertura sem relatório gerado por ferramenta e escopo explícito. Não converter quantidade de testes em cobertura ou homologação.

## Ciclo obrigatório por mudança

Antes de editar código de produção:

1. Declare o comportamento observável e a regressão que o teste detectará (`INTENT`).
2. Escreva um teste pequeno na camada proprietária do comportamento, com fixture determinística e expectativa independente da implementação.
3. Execute o teste isolado. Registre o comando e confirme falha pela razão esperada. Erro de compilação, fixture ou ambiente não é `RED` válido.
4. Implemente a menor mudança que torne o teste verde.
5. Execute o mesmo teste e a suíte do módulo; refatore somente após o verde.
6. Execute a suíte JVM do projeto, lint dos módulos afetados, compilação Debug e instrumentação nos perfis afetados.
7. Registre limitações e confirme que o relatório contém testes executados, sem falhas, erros ou execução totalmente ignorada.

Formato obrigatório do registro na descrição da mudança ou handoff:

```text
INTENT: <regressão observável e requisito relacionado>
RED: <comando> — falha esperada: <assertion/resultado>
GREEN: <comando> — <testes aprovados/falhas/erros/ignorados>
QUALITY GATE: <comandos e resultados>
DEVICE: <PHONE/TABLET/TV ou não aplicável, com evidência>
LIMITAÇÕES: <lacunas reais ou nenhuma>
```

Teste que passa antes da implementação, falha por setup ou verifica apenas chamada de mock não comprova RED. Use fakes para dependências externas lentas; mantenha a lógica e os efeitos sob teste reais.

## Seleção de testes

| Camada/comportamento | Primeiro teste | Integração complementar |
| --- | --- | --- |
| Política, parser, mapper, regra de domínio | JVM no módulo proprietário | Casos limite e entradas inválidas |
| Coroutines, Flow, ViewModel | JVM com scheduler/dispatcher determinístico | Persistência Android instrumentada quando relevante |
| Retrofit, autenticação, headers, retries | JVM + MockWebServer | Servidor de teste autorizado; nunca credencial real |
| DataStore, Room, WorkManager, FileProvider, intents | JVM para decisões puras | Instrumentação Android para integração de plataforma |
| Compose, navegação, rolagem, acessibilidade, D-pad | Teste de estado/política JVM quando separável | Compose instrumentado no PHONE, TABLET ou TV afetado |
| Player, PiP, lifecycle, TTS, codecs, downloads offline | JVM para políticas e estados | Instrumentação; aparelho físico quando hardware/saída for parte do requisito |
| Empacotamento/assinatura | Pester ou teste de script com arquivos temporários controlados | Inspecionar metadados, versão, assinatura e hash do artefato |

## Lacunas auditadas e ordem de fechamento

Prioridades abaixo resultam de auditoria estática dos módulos e são tarefas de teste, não afirmações de que a funcionalidade esteja quebrada. Criar cada teste primeiro e confirmar `RED` antes de qualquer correção.

### P1 — segurança, sessão e fluxos críticos

1. **Atualização do APK:** `core/common/.../AppUpdateDownloader.kt` — sucesso, progresso, HTTP inválido, corpo vazio, checksum incorreto, cancelamento e remoção do parcial. Depois, `AppUpdateInstaller.kt` — permissão, intent de configurações, URI FileProvider e abertura do instalador. Evidência parcial em 2026-10-08: RED-GREEN comprovado para rejeitar user-info, porta diferente de 443, caminhos com segmentos `.`/`..` literais ou percent-encoded e redirects HTTPS para hosts fora da allowlist; redirect para `release-assets.githubusercontent.com` preservado. Teste instrumentado `AppUpdateHttpsRedirectIntegrationTest` passou 5/5 no AVD PHONE API 35 usando NVIDIA. Fluxo completo do downloader e instalador continuam pendentes.
2. **Sessão persistida:** `data/.../SessionRepositoryImpl.kt` — salvar/reabrir/limpar sessão, troca de servidor/conta, servidores salvos e isolamento no DataStore.
3. **SyncPlay realtime:** `core/api/.../SyncPlayRealtimeClient.kt` — start/stop, troca de grupo, eventos, falha, cancelamento e reconexão obsoleta com WebSocket controlado.
4. **Navegação de produção:** `app/.../MulletaFlixNavHost.kt` — sessão ausente/válida/incompleta, restauração, logout, deep link frio e `onNewIntent` no grafo real. Os testes de rota simplificados continuam úteis, mas não substituem esta integração.
5. **Build/package APK:** `build-app-package.ps1` — `-SkipBuild` não pode aceitar APK antigo/versão incompatível; artefato ausente ou assinatura divergente deve falhar sem copiar/publicar.
6. **Fluxos visíveis de features:** `LiveTvScreen` deve enviar o ID correto ao iniciar canal/gravação; `ProfileScreen` deve refletir confirmação de logout/troca de conta; Downloads devem sobreviver a reinício/retomar e abrir arquivo offline; Login/registro devem demonstrar validação, erro e sucesso; SyncPlay deve conectar ações da tela, eventos e estado final.

### P2 — persistência, conectividade e UX adaptativa

1. `SearchHistoryRepositoryImpl`: persistência, isolamento por usuário, limite/ordem, duplicatas, remoção, limpeza e JSON corrompido.
2. `HomeFeedCacheRepositoryImpl`: DataStore concreto, reconstrução do repositório e estado corrompido.
3. `ConnectivityNetworkMonitor`: estado inicial, mudanças em múltiplas redes, rede tarifada e desregistro do callback ao cancelar Flow.
4. `CheckAppUpdateUseCase`: atualização disponível/indisponível e erro do repositório.
5. Acessibilidade integrada em telas e diálogos, com ordem TalkBack/foco, escala de fonte e estados visuais; complementar a cobertura existente de componentes e contraste.
6. Matriz de layout/interação para PHONE, TABLET e TV: alvo touch, clipping, escala, rolagem e foco D-pad. Um teste portátil genérico não comprova os três perfis.

### P3 — suporte de testes

`:core:testing` contém fixtures compartilhadas. Não precisa de teste próprio enquanto permanecer apenas como dados constantes; se fixtures ganharem lógica, cobrir determinismo, valores padrão e casos representativos no próprio módulo.

## Frentes do projeto e propriedade

| Módulo | Foco TDD e limite de validação |
| --- | --- |
| `:core:common` | Políticas comuns, conectividade, atualização/instalação e utilitários; instrumentar APIs Android que não podem ser demonstradas na JVM. |
| `:core:api` | Contratos HTTP/WebSocket, autenticação, redirecionamento, erros, identidade e cancelamento. |
| `:domain` | Use cases, decisões de negócio, paginação e estados independentes de Android. |
| `:data` | Repositórios, mapeamento, isolamento por conta/servidor e persistência concreta. |
| `:design-system` | Semântica acessível, foco remoto, dimensões e políticas de dispositivo. |
| `:feature:auth` | Login, cadastro, Quick Connect, validação, erros e transição de sessão. |
| `:feature:home` | Descoberta, refresh, solicitações, navegação por toque/D-pad e ocultação de Livros na TV. |
| `:feature:library` | Filtros, ordenação ascendente/descendente, paginação, refresh e exclusão de livros na TV. |
| `:feature:item-detail` | Tipos de mídia, capas, leitores, progresso, controles de downloads por dispositivo. |
| `:feature:player` | Playback, áudio/legendas, lifecycle, OSD, PiP, Cast e pausa por perfil. |
| `:feature:search` | Debounce, filtros, histórico, resultados e diferenças TV/portáteis. |
| `:feature:downloads` | Fila, estados, ações remotas/touch, retomar e abrir mídia offline. |
| `:feature:live-tv` | Guia, canais, gravações, timers e início de playback. |
| `:feature:settings` | Persistência de preferências, tema, cache e opções específicas por dispositivo. |
| `:feature:user` | Troca de conta, perfil, logout e isolamento de preferências. |
| `:feature:sync-play` | Estado de sala, eventos realtime, refresh, entrada, saída e falhas. |
| `:app` | Inicialização, DI, navegação de produção, intents/deep links, serviços e integração de módulos. |

## Quality gate

Execute da raiz `MulletaFlix-android`:

```powershell
.\gradlew.bat testDebugUnitTest --no-daemon --console=plain
.\gradlew.bat :<modulo-afetado>:lintDebug :app:lintDebug :app:assembleDebug --no-daemon --console=plain
```

Use `tools\with-emulator.ps1` para instrumentação. Informe `expectedDeviceProfile=PHONE`, `TABLET` ou `TV`, execute os casos afetados em cada perfil e confira XML/log para testes executados, ignorados, erros e falhas. Consulte `TESTING.md` para comandos, emuladores e cenários existentes.

Baseline executado em 2026-10-08: `testDebugUnitTest --rerun-tasks` executou 235 suítes, 1.515 testes, 0 falhas, 0 erros e 0 ignorados. Isso não mede cobertura e não comprova RED-GREEN histórico.

## Critério de conclusão do plano

- TDD RED-GREEN demonstrado em toda mudança executável nova ou alterada.
- Lacunas P1 encerradas por testes primeiro, com resultados registrados.
- Lacunas P2 e P3 fechadas na sequência, sem ignorar perfis de dispositivo afetados.
- Suite completa, lint e build passam; instrumentação executa casos nos perfis pertinentes.
- Histórico antigo sem evidência permanece explicitamente “não auditado”; nunca reclassificar testes retroativamente como TDD sem prova.
