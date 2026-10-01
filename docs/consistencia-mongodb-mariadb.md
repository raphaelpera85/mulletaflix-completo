# Arquitetura e Consistência entre MongoDB e MariaDB

Este documento formaliza o modelo de persistência híbrido do MulletaFlix, detalhando as fontes de verdade por entidade, os mecanismos de sincronização, a reconciliação e o comportamento do sistema diante de falhas parciais (cumprindo a meta **T5.3** do roadmap).

---

## 1. Visão Geral da Separação de Responsabilidades

O MulletaFlix adota uma arquitetura de dados poliglota especializada:
1. **MariaDB (Instância Única / Embutida ou Externa)**:
   - Banco de dados relacional oficial do núcleo do servidor.
   - Substituiu integralmente o SQLite histórico (comprovado em `AI-HANDOFF-MARIADB-ONLY.md`).
   - Gerenciado via Entity Framework Core (`MulletaFlixDbContext`, `JellyfinDbContext`).
2. **MongoDB (Standalone na porta 27017)**:
   - Banco de dados NoSQL orientado a documentos utilizado pela integração de nuvem **Nebula** (`NebulaFtpManager`).
   - Especializado em controle de fila de upload distribuída, identificadores de arquivos no Telegram (`tg_file_id`), chunks multipartes e leases de execução.

---

## 2. Matriz de Entidades e Fonte de Verdade

| Entidade | Banco Oficial | Fonte da Verdade | Descrição e Ciclo de Vida |
|---|---|---|---|
| **Usuários e Permissões** | MariaDB | **MariaDB** | `Users`, `Permissions`, autenticação, hashes de senha, restrições de LAN e elevação. |
| **Metadados do Catálogo** | MariaDB | **MariaDB** | `BaseItems` (filmes, séries, episódios, músicas, livros), `ItemValues`, `People`, `Chapters`. |
| **Progresso e Playback** | MariaDB | **MariaDB** | `UserItemData` (tempo assistido, contagem de reproduções, favoritos, status "jogado/visto"). |
| **Streams e Anexos** | MariaDB | **MariaDB** | `MediaStreams` (áudio, vídeo, legendas detectadas), `MediaAttachments` (fontes de legenda). |
| **Segmentos IntroSkipper** | MariaDB | **MariaDB** | `mulletaflix_introskipper` (tabelas de intro, créditos, fingerprints acústicos e filas). |
| **Fila de Upload Nebula** | MongoDB | **MongoDB** | Coleção `files` com estados `staging`, `queued`, `uploading`, `failed`, `completed`, `cancelled`. |
| **Partes e Telegram IDs** | MongoDB | **MongoDB** | Detalhes de chunks, `tg_file_id`, tamanho de cada parte, hashes e confirmação do Telegram. |
| **Arquivo `.strm`** | Disco Local | **Ponte Física** | Arquivo texto contendo a URL canônica de streaming (`/Videos/{id}/stream.mkv`). Conecta o catálogo MariaDB aos dados do MongoDB. |

---

## 3. Fluxo de Sincronização entre Bancos

```
[Upload / Nebula Worker]
          │
          ▼
   (MongoDB: `files`)
   - tg_file_id persistido
   - status: completed
          │
          ▼
 [Nebula STRM Generator]
   - Gera arquivo `.strm` na biblioteca
          │
          ▼
 [Jellyfin Library Scanner]
   - Detecta `.strm` no disco
   - Cria/atualiza `BaseItem` no MariaDB
          │
          ▼
  (MariaDB: `baseitems`)
```

1. **Geração de STRM (`GenerateStrmAsync`)**:
   - Quando um documento na coleção `files` do MongoDB alcança o estado terminal `completed`, o worker gera o arquivo `.strm` correspondente na pasta da biblioteca de mídia.
   - O identificador do documento MongoDB compõe a URL de reprodução gravada no `.strm`.
2. **Indexação no MariaDB (`LibraryManager`)**:
   - A tarefa agendada de varredura ou o monitor de sistema de arquivos detecta o `.strm`.
   - Um registro `BaseItem` é criado ou atualizado no MariaDB contendo o caminho físico do `.strm`.
   - Metadados externos (TheMovieDb, TheAudioDb, OpenLibrary) são baixados e persistidos no MariaDB.
3. **Reprodução**:
   - O cliente Jellyfin solicita a reprodução do `BaseItem` ao MariaDB (validando permissões de usuário).
   - O servidor lê o `.strm`, extrai a rota e aciona o `NebulaStreamEngine`, que busca no MongoDB as partes e `tg_file_id` necessários para streaming ou prefetch em cache.

---

## 4. Reconciliação e Recuperação de Inconsistências

### Cenário A: Documento `completed` no MongoDB, mas sem item no MariaDB
- **Causa**: Exclusão acidental da biblioteca, perda de `.strm` ou falha durante varredura.
- **Resolução Automática**: A rotina administrativa `NebulaFtpManager.GenerateStrmAsync` reexecuta uma varredura idempotente sobre o MongoDB e recria os arquivos `.strm` faltantes. Em seguida, a varredura ordinária do Jellyfin sincroniza as entidades no MariaDB.

### Cenário B: Item `.strm` no MariaDB aponta para MongoDB ID inexistente ou cancelado
- **Causa**: Documento expurgado do MongoDB ou restauração assíncrona parcial.
- **Comportamento**: O motor de streaming detecta código HTTP 404 / registro ausente no MongoDB e falha limpa com mensagem operacional no log. A integridade do MariaDB permanece preservada. Ao rodar "Remover itens excluídos" na varredura de biblioteca, o Jellyfin detecta que o arquivo físico `.strm` foi removido e remove o `BaseItem` do MariaDB.

---

## 5. Comportamento Diante de Falha Parcial

1. **MongoDB Indisponível ou Reiniciando**:
   - O servidor MulletaFlix continua plenamente funcional para todas as operações centrais (login, navegação no catálogo, gerenciamento de usuários, execução de tarefas e reprodução de itens locais em disco).
   - Apenas o streaming de itens remotos Nebula falha, retornando HTTP 503 com log descritivo.
   - As tabelas relacionais do MariaDB nunca entram em deadlock nem sofrem corrupção por ausência do MongoDB.
2. **MariaDB sob Carga ou Transações Concorrentes**:
   - `MulletaFlixDbContext` implementa política de retentativa com backoff exponencial (até 5 tentativas) contra `MySqlException` (deadlocks ou timeouts transitórios de conexão).
   - O tuning do MariaDB embutido (`--max-connections 300`, `--innodb-io-capacity 2000`, `--innodb-buffer-pool-size 256M`) garante que rajadas de conexão não saturem o pool de conexões.
3. **Isolamento de Backups**:
   - Backups do MariaDB (`mysqldump` ou extração do banco embutido) garantem recuperação completa das contas e do histórico de reprodução sem depender do MongoDB.
   - Backups do MongoDB (`mongodump` ou snapshot de coleções) preservam a chave dos arquivos remotos e leases de fila independentemente do catálogo relacional.
