# Inventário de dados e recuperação

Inventário estático do código do servidor, atualizado em 28/09/2026. Não consulta banco, volume de mídia, arquivos de configuração reais ou credenciais de produção. Portanto, identifica estruturas e cobertura implementada, mas não estima tamanhos, retenção real nem RPO/RTO.

## Fontes de verdade e cobertura atual

| Dado | Fonte/estrutura identificada | Cobertura observada no código | Sensibilidade e lacuna |
| --- | --- | --- | --- |
| Catálogo Nebula | MongoDB `ftp.files`; documentos BSON guardam nome, tipo, pai, caminho local, estado da fila, timestamps e partes/referências Telegram (`NebulaMongoContext`) | `PerformBackupAsync` envia `nebula_files` ao Supabase em lotes e permite sincronização delta ou completa | **Corrigido (04/10):** `RestoreFilesFromSupabaseAsync` agora pagina e reidrata `nebula_files` no MongoDB; coberto por `NebulaSupabaseFileRestoreTests` (sem Mongo real) e `NebulaSupabaseRestoreMongoTests` (opt-in, requer Mongo). Ainda sem snapshot consistente entre MongoDB e Supabase (sem janela de manutenção coordenada). |
| Usuários FTP | MongoDB `ftp.users`; login, hash/senha legado e permissões | `PerformBackupAsync` envia a lista atual para `nebula_users`; a restauração tenta gravar esses documentos no MongoDB | Credenciais e permissões. Falta teste de recuperação MongoDB isolada e validação de hashes/campos legados. |
| Tokens de bots | MongoDB `ftp.bot_tokens`; token, índice e estado habilitado; fallback em `NebulaFtpConfiguration.BotTokens` | A restauração lê `nebula_bot_tokens`; configuração também permite carregar tokens do Supabase quando Mongo está vazio | **Corrigido (05/10):** `PerformBackupCoreAsync` agora grava `nebula_bot_tokens` via `BackupNebulaBotTokensAsync`/`GetAllBotTokenDocsAsync`; round-trip coberto por `NebulaSupabaseSyncTests` (unitário) e `NebulaSupabaseBackupBotTokensMongoTests` (opt-in, requer Mongo). |
| Idempotência de operações | MongoDB `ftp.operation_replays`; operação, chave idempotente e expiração | Persistência local com índice único e índice TTL | Estado transitório, não incluído no sync Supabase; pode expirar/recomeçar, mas replay durante janela ativa pode se perder após restauração. |
| Estatísticas Nebula | Coleção MongoDB `ftp.stats` acessada pelo contexto para estatísticas do feeder | Não aparece no fluxo de sincronização Supabase nem no conjunto de coleções criadas explicitamente | Dados operacionais possivelmente reconstruíveis, mas sem inventário de todos os consumidores nem política de restauração. |
| Contas e permissões MulletaFlix | `UsersDbContext`: usuários, permissões, licenças, preferências, dispositivos, agendas, logs, relatórios de reprodução, planos, pagamentos, gateways e cupons | Backup Supabase independente cobre `mulletaflix_users`; inclui usuário, permissões e licença. Backup geral serializa entidades expostas por `MulletaFlixDbContext`, não enumera explicitamente este contexto | Dados pessoais, hashes de senha, licenças e informações financeiras. A cópia remota não representa todas as tabelas de `UsersDbContext` (ex.: logs, dispositivos, preferências, pagamentos). |
| Catálogo relacional | `MulletaFlixDbContext` e contextos de domínio: `MoviesDbContext`, `SeriesDbContext`, `ChannelsDbContext`, `BooksDbContext` | O backup geral `BackupService` enumera somente as entidades do `MulletaFlixDbContext`. Migração/startup registra factories próprias para os contextos de domínio | Não há evidência de exportação/restauração completa dos contextos separados de filmes, séries, canais e livros pelo backup geral. Validar provider e bancos físicos é tarefa pendente, sem acessar produção neste inventário. |
| Configuração/segredos | Configurações do servidor, incluindo `database` e `nebulaftp`; esta última possui URI Mongo, chave Supabase/Management, hash Telegram, tokens de bot, senha FTP, token HTTP e caminhos | Backup geral inclui arquivos `.xml`/`.json` da configuração, diretórios `Config/users` e `Config/ScheduledTasks` | O backup ZIP observado não aplica criptografia. Tratar esses arquivos/arquivos de backup como material secreto; definir criptografia, permissões, rotação e exclusão segura antes de ampliar cópias remotas. Nenhum valor real foi lido. |
| Índices/schema | Índices Mongo definidos em `NebulaMongoContext.EnsureIndexesAsync`; schema EF e histórico de migrations no provider relacional | Índices Mongo são recriados/validados pelo startup; backup geral inclui `HistoryRow` e recria histórico na restauração | Índices não são exportados como dados; a recuperação depende de bootstrap/migrations compatíveis. Algumas falhas de criação de índices são apenas logadas e exigem health check/validação pós-restauração. |
| Arquivos derivados do catálogo | `.strm`, NFO/XML, imagens, legendas e metadados localizados em staging, bibliotecas e armazenamento Nebula | STRM pode ser gerado de registros Mongo concluídos; o backup geral inclui coleções/playlists/tarefas e, por opção, metadados, legendas e trickplay | Bibliotecas e roots de staging configuradas em volumes externos não são copiadas automaticamente pelo backup de banco. Mídia Telegram e arquivo original são classificados separadamente dos metadados regeneráveis. |
| Cache e temporários | Cache de playback Nebula, staging/downloads e sessões temporárias Telegram | Não fazem parte do backup Mongo→Supabase; parte do cache é descartável e sessão pode ser renovada | Não restaurar cache como fonte de verdade. Staging pode conter uploads ainda não concluídos e precisa ser classificado/recuperado separadamente para não perder trabalho pendente. |
| Mídia completa | Arquivos originais nas bibliotecas/staging e partes enviadas aos canais Telegram | O backup Mongo→Supabase guarda documentos e referências/IDs, não baixa nem arquiva novamente o payload de vídeo | Telegram/volumes de origem são dependências externas do catálogo. Proteger referências, tokens e canais; documentar o procedimento de recuperar mídia e confirmar integridade sem duplicar uploads. |

## Bancos relacionais registrados no código

O registro de serviços cria factories separadas para `MulletaFlixDbContext`, `UsersDbContext`, `MoviesDbContext`, `SeriesDbContext`, `ChannelsDbContext`, `BooksDbContext` e `SystemDbContext`. `SystemDbContext` contém `DeviceOptions` e `ApiKeys`, portanto o inventário de segredos não termina na configuração XML. Contextos de domínio expõem filmes e metadados/dados do usuário, séries/temporadas/episódios, canais/programas e livros/dados do usuário.

O backup geral usa uma transação relacional para o contexto principal, inclui o histórico de migrations e pruna arquivos locais para manter dois ZIPs. O arquivo agora é gerado como `.partial`, relido entrada por entrada e promovido ao nome final somente após validação estrutural; isso não equivale a checksum autenticado nem a criptografia. Inclui configurações, usuários agendados, diretórios `Root`, collections/playlists/tarefas; legendas, trickplay e metadata dependem das opções escolhidas. Ele não é um backup universal de todos os contextos independentes nem dos volumes das bibliotecas.

## Supabase e limites da restauração atual

O schema de backup define `nebula_files`, `nebula_users`, `nebula_bot_tokens`, `mulletaflix_users` e `nebula_backups`. O sync Mongo→Supabase observado escreve arquivos e usuários FTP; o fluxo independente de usuários escreve contas MulletaFlix. A leitura de tokens existe, mas não foi encontrado o correspondente writer nesse fluxo. A restauração MongoDB atual recupera usuários FTP, contas MulletaFlix e tokens, mas não reidrata o catálogo `nebula_files`.

O registro em `nebula_backups` não validava a resposta HTTP: falha de histórico podia ser ignorada enquanto o backup era reportado como bem-sucedido. A falha agora invalida o resultado e é coberta por testes de rejeição e aceite; os demais itens de consistência/segurança de T4.2 continuam abertos.

## Próximas tarefas derivadas

1. Fazer T4.2 cobrir snapshots/consistência, criptografia em repouso e trânsito, retenção/rotação, integridade e exclusões remotas; resposta HTTP do histórico já foi validada.
2. Fazer T4.3 exercitar restauração isolada de `nebula_files`, FTP users, tokens e entidades relacionais, com contagens e consultas, usando armazenamento temporário.
3. Fazer T4.4 mostrar por componente o que entrou no backup, o que foi omitido, última/ próxima execução, integridade e erros.
4. Fazer T4.5 medir RPO/RTO e realizar recuperação isolada Windows/Linux antes de alegar recuperação completa.
5. Separar payload volumoso de vídeo, bibliotecas externas, staging pendente e cache descartável; não inferir que referência Telegram equivale a backup do arquivo.

## Evidências no código

- MongoDB Nebula e índices: `MulletaFlix-master/Jellyfin.Server.Implementations/Nebula/NebulaMongoContext.cs`.
- Backup/restauração Supabase: `MulletaFlix-master/Jellyfin.Server.Implementations/Nebula/NebulaSupabaseSyncService.cs`.
- Schema Supabase e ingestão de tokens: `MulletaFlix-master/Jellyfin.Server.Implementations/Nebula/NebulaFtpManager.cs`.
- Configuração Nebula e campos sensíveis: `MulletaFlix-master/MediaBrowser.Model/Configuration/NebulaFtpConfiguration.cs`.
- Contextos e entidades relacionais: `MulletaFlix-master/src/Jellyfin.Database/Jellyfin.Database.Implementations/Contexts/UsersDbContext.cs`, `Contexts/DomainDbContexts.cs` e `MulletaFlix-master/src/Jellyfin.Database/Jellyfin.Database.Implementations/JellyfinDbContext.cs`.
- Registro dos sete contextos: `MulletaFlix-master/Jellyfin.Server.Implementations/Extensions/ServiceCollectionExtensions.cs`.
- Cobertura do ZIP geral e opções: `MulletaFlix-master/Jellyfin.Server.Implementations/FullSystemBackup/BackupService.cs`.
