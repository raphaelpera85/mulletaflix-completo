# Diretrizes de Naming de Mídia e Resolução de Metadados (MulletaFlix)

## 1. Tratamento de Tags de Release e Extração de Ano
- **Limpeza de Tags de Release**:
  - Tags de release/áudio/legenda (`(LEG)`, `[LEG]`, `(DUB)`, `[Multi-Subs]`, `PT-BR`, `DUAL`, etc.) podem aparecer em qualquer posição do nome (início, meio ou fim).
  - Devem ser limpas antes da busca e normalizadas via `TitleNormalization.CleanReleaseTags`.
  - Padrões onde tags e anos vêm colados (ex: `(LEG)(2024)`, `(LEG) 2024` ou `[LEG] [2024]`) devem ser desobstruídos mantendo o ano intacto.

- **Proteção de Extração de Ano**:
  - A extração de ano deve aceitar tanto parênteses quanto colchetes (ex: `(2024)`, `[2024]`).
  - É mandatório usar lookaheads de proteção para **não** classificar datas diárias completas (ex.: `2013-12-09`, `2013.12.09`) como ano de lançamento de filme.
  - Caso o objeto de mídia (`Movie`) ainda não possua o ano definido no momento do refresh de metadados, o sistema deve inspecionar tanto o nome do arquivo quanto a pasta pai via `TitleNormalization.ExtractYear`.

## 2. Resolução e Scoring de Metadados (TMDb e Outros Provedores)
- **Busca em Duas Fases**:
  - Sempre que um ano for identificado no nome ou pasta da mídia, a busca primária na API de metadados DEVE filtrar estritamente por esse ano.
  - Somente se a busca primária com ano retornar vazia é que deve ser feito fallback para busca aberta (`year = 0`).

- **Proibição de Seleção Cega**:
  - Nunca assumir `searchResults[0]` como o resultado correto.
  - Sempre aplicar algoritmo de correspondência ponderada (`FindBestMatch` / `ScoreCandidate`):
    - **Nome Completo**: Priorizar correspondência total dos tokens (F1 Score / Coeficiente Dice). Penalizar severamente candidatos que correspondam apenas a um prefixo de uma única palavra quando o usuário possui um título composto (ex.: `"Deadpool"` não pode vencer `"Deadpool & Wolverine"`).
    - **Peso Decisivo do Ano**: Bônus expressivo para correspondência exata de ano (+60), tolerância de 1 ano (+20 para diferenças de lançamento internacional/festivais), e penalização severa para discrepâncias maiores que 2 anos (-60 a -100) para evitar que clássicos homônimos vençam sequências ou lançamentos modernos.
