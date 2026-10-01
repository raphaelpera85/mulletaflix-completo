# Validação Determinística do Catálogo, Proveniência e Termos de Provedores (Fase 8)

Este documento estabelece as regras de negócio, arquitetura e garantias operacionais da **Fase 8** do roadmap do MulletaFlix, cobrindo a auditoria determinística do catálogo sem modelos de inteligência artificial, a preservação estrita de proveniência de metadados, o modo de auditoria em leitura pura, o mecanismo seguro de remediação com rollback, e os termos/direitos de provedores de metadados.

---

## 1. Princípios da Validação Determinística (Sem IA)

Toda a análise do catálogo do MulletaFlix é orientada a regras determinísticas e contratos estritos, sem depender de inferência estocástica ou modelos LLM:
1. **Regras Falsificáveis e Reprodutíveis:** Uma inconsistência só é gerada quando evidências concretas comprovam divergência (ex.: ano extraído do arquivo difere de `ProductionYear`, extensão de arquivo incompatível com o tipo de mídia, ou ausência de identificadores canônicos exigidos pelo contrato).
2. **Confiança e Severidade:** Cada inconsistência possui uma pontuação de confiança (0.0 a 1.0) e severidade (`Notice`, `Warning`, `Error`), permitindo filtragem precisa na API (`GET /Catalog/Audit`).
3. **Nenhuma Alteração em Massa Não Supervisionada:** Nenhuma correção automática é aplicada silenciosamente; toda alteração exige solicitação explícita por administrador.

---

## 2. Categorias de Inconsistências Auditadas (T8.1)

| Categoria | Descrição da Regra | Severidade | Confiança | Evidência Gerada |
| :--- | :--- | :--- | :--- | :--- |
| **`YearMismatch`** | O nome do arquivo/pasta contém um ano de 4 dígitos (`19xx`/`20xx`) que diverge de `item.ProductionYear`. | `Warning` | 0.90 | `Path filename '...' indicates release year YYYY, but item metadata records ZZZZ.` |
| **`MissingProviderId`** | O item pertence a um tipo de mídia que exige identificadores externos canônicos e nenhum está cadastrado: <br>• Filmes: falta TMDB e IMDB.<br>• Séries: falta TVDB e TMDB.<br>• Livros: falta OpenLibrary e ISBN. | `Warning` | 0.95 | `Item lacks canonical external provider identifiers.` |
| **`MissingPrimaryImage`** | O item não possui pôster ou imagem de capa primária associada. | `Notice` | 1.00 | `Item does not have a primary cover/poster image attached.` |
| **`MissingBackdrop`** | Filmes ou Séries que não possuem imagem de fundo (backdrop / fanart). | `Notice` | 0.85 | `Item does not have a background fanart/backdrop image attached.` |
| **`LockedFieldConflict`** | O item possui metadados travados (`item.IsLocked` ou `item.LockedFields`), indicando proveniência customizada intencional que deve ser protegida de sobrescritas externas. | `Notice` | 1.00 | `Item has locked metadata provenance: IsLocked=..., LockedFields=[...].` |
| **`MediaTypeMismatch`** | O container no disco é fisicamente incompatível com o tipo do item (ex.: Livro com extensão `.mkv`/`.mp4`, ou Vídeo com extensão `.epub`/`.pdf`). | `Error` | 0.99 | `Item is classified as X but path has container extension Y.` |

---

## 3. Fontes por Tipo de Catálogo e Limites de Consulta (T8.2)

- **Livros e Literatura (`OpenLibrary`):**
  - **Identificadores Suportados:** OpenLibrary Work ID (`OL...W`), Edition ID (`OL...M`), ISBN-10 e ISBN-13.
  - **Prevenção de Abuso:** O provedor `OpenLibraryProvider` realiza buscas pontuais apenas sob demanda e proíbe varredura em massa (mass harvesting/scraping). Varreduras em massa devem ser feitas via dumps oficiais da biblioteca.
- **Filmes e Produções Cinematográficas (`TheMovieDb` / `IMDB`):**
  - Identificador canônico `Tmdb` ou `Imdb`.
- **Séries e Programas de TV (`TheTVDB` / `TheMovieDb`):**
  - Identificadores de série e episódios com preservação da numeração de temporada/episódio.

---

## 4. Preservação de Proveniência e Campos Travados (T8.3)

O MulletaFlix implementa proteção de proveniência em dois níveis:
1. **Item Bloqueado Integralmente (`item.IsLocked`):** Impede que rotinas externas, plugins ou scrapes alterem qualquer campo do item.
2. **Campos Específicos Bloqueados (`item.LockedFields`):** Enumeração de `MetadataField` (como `MetadataField.Name`, `MetadataField.Overview`, `MetadataField.ProductionLocations`, etc.).
3. **Garantia de Não-Sobrescrita:** A API de remediação (`ApplyFixAsync`) rejeita alterações quando `PreserveLockedFields = true` e o campo alvo estiver presente na lista de bloqueios, retornando `400 Bad Request` com explicação amigável ao operador.

---

## 5. Modo de Auditoria sem Escrita (T8.4)

O endpoint de auditoria opera estritamente em modo de leitura (read-only):
- **Rota:** `GET /Catalog/Audit` (exige `RequiresElevation`).
- **Filtros Suportados:**
  - `itemType`: filtra por tipo (ex.: `Movie`, `Series`, `Book`).
  - `category`: filtra por categoria de inconsistência (ex.: `YearMismatch`, `MissingProviderId`).
  - `severity`: filtra por severidade (`Notice`, `Warning`, `Error`).
  - `minConfidence`: threshold mínimo de confiança (ex.: `0.9`).
- **Comportamento Seguro:** O método `RunAudit()` consulta o catálogo via `ILibraryManager.GetItemList` e analisa as propriedades em memória sem invocar qualquer método de escrita (`UpdateItemAsync`, `SaveConfiguration`, etc.).

---

## 6. Remediação Determinística com Histórico e Rollback (T8.5)

Para cada correção aprovada pelo operador:
1. **Aplicação com Snapshot:** `POST /Catalog/Audit/Fix` recebe o `ItemId`, `Field` e `ApprovedValue`. Antes de persistir a alteração no repositório, o serviço captura o `PreviousValue`, gera um identificador único de histórico (`HistoryEntryId`) e registra o autor (`AppliedBy`) e timestamp UTC.
2. **Histórico de Auditoria:** `GET /Catalog/Audit/History` lista todos os snapshots de remediação ordenados cronologicamente.
3. **Rollback Seguro:** `POST /Catalog/Audit/Rollback` recebe o `HistoryEntryId`, localiza o item no banco de dados e restaura com precisão o `PreviousValue`, removendo o estado e emitindo log de auditoria.

---

## 7. Direitos e Termos de Provedores de Metadados (T8.6)

Expostos de forma estruturada via `GET /Catalog/Audit/ProviderTerms`:

| Provedor | Finalidade | Identificadores | Limites de Taxa (Rate Limits) | Atribuição e Uso Permitido |
| :--- | :--- | :--- | :--- | :--- |
| **OpenLibrary** | Livros, edições, autores e capas bibliográficas. | `OL...W`, `OL...M`, `ISBN` | 100 req/min por IP. Proibido scraping em massa; para grandes volumes deve-se usar os dumps do Internet Archive. | Domínio público (CC0 1.0 Universal). Atribuição recomendada ao Open Library. |
| **TheMovieDb (TMDB)** | Filmes, séries, elencos e sinopses. | `TMDB ID`, `IMDB ID` | Concorrência limitada por chave de cliente. | Exige logotipo oficial e texto de atribuição ("This product uses the TMDB API..."). Uso não comercial. |
| **TheTVDB** | Séries, temporadas e episódios. | `TVDB Series ID` | Token Bearer por assinatura/projeto. | Atribuição recomendada TheTVDB. Uso permitido sob termos da API. |
| **Fanart.tv** | Logos de alta definição, clearart e backgrounds. | `MusicBrainz`, `TVDB`, `TMDB` | Exige chave de projeto e **cache local mandatório** no cliente para evitar chamadas redundantes. | Atribuição à comunidade fanart.tv. |

---

## 8. Evidências de Teste Automatizado

A conformidade da Fase 8 é atestada pelas seguintes suítes de teste:
- `tests/Jellyfin.Server.Implementations.Tests/CatalogAudit/CatalogAuditServiceTests.cs` (7 testes aprovados):
  - Detecção de divergência de ano (`YearMismatch`).
  - Detecção de provedores ausentes (`MissingProviderId`) para filmes e livros.
  - Detecção de tipo de mídia conflitante com extensão (`MediaTypeMismatch`).
  - Preservação de proveniência de campos bloqueados (`LockedFieldConflict`).
  - Ciclo completo de remediação e rollback com restauração idêntica do valor original.
  - Bloqueio de alteração em itens travados quando `PreserveLockedFields = true`.
  - Conformidade das regras e termos de provedores (`GetProviderTerms`).
- `tests/Jellyfin.Api.Tests/Controllers/CatalogAuditControllerTests.cs` (4 testes aprovados):
  - Endpoint `GET /Catalog/Audit` com geração de relatório e contagem de itens.
  - Endpoint `POST /Catalog/Audit/Fix` gerando entrada de histórico.
  - Endpoint `POST /Catalog/Audit/Rollback` executando reversão com retorno tipado `CatalogAuditRollbackResponse`.
  - Endpoint `GET /Catalog/Audit/ProviderTerms` retornando provedores configurados.
