### MulletaFlix Server v12.1.18 — hotfix emergencial

#### Correção

- Corrige os timestamps dos arquivos e diretórios virtuais do Nebula. Antes, cada leitura gerava uma data/hora nova, fazendo o Jellyfin considerar os itens modificados continuamente e reprocessar metadados e Intro Skipper; isso também aumentava a atividade de leitura no disco virtual N:.
- Os timestamps agora são estáveis e derivados dos dados MongoDB disponíveis (`mtime`, `created_at` e `modified_at`), com fallback determinístico para datas ausentes ou inválidas.

#### Validação

- `NebulaUploadEngineTests`: 224 aprovados, 0 falhas.
- `Jellyfin.Server.Implementations` compilado em `Release`, 0 avisos e 0 erros.
- A correção operacional na instância de produção será confirmada após a instalação e verificação pós-reinício deste hotfix.

#### Artefatos

- Pacote de atualização Windows x64: `mulletaflix-update-win-x64.zip`.
- Instalador Windows x64: `mulletaflix_12.1.18_windows-x64.exe`.

Esta publicação é um hotfix emergencial autorizado pelo usuário enquanto o roadmap geral continua em andamento; não representa a conclusão das demais melhorias planejadas.
