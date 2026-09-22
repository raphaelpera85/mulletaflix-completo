# Migração IntroSkipper: SQLite → MariaDB

Ferramenta manual e de execução única. Copia os dados que o plugin IntroSkipper guardava em
SQLite (`introskipper-v2.db` e `introskipper-cache.db`) para o schema MariaDB
`mulletaflix_introskipper`, que passou a ser o único banco a partir da v12.0.41.

O servidor **não** faz essa migração sozinho: o provider SQLite foi removido do runtime de
propósito. Se a sua instalação ainda tem segmentos detectados no arquivo antigo, rode esta
ferramenta antes (ou depois) de atualizar o servidor — ela é independente do servidor e não
precisa que ele esteja parado.

## Uso

```powershell
.\mulletaflix-introskipper-migrate.exe `
  --segment-db "C:\Users\<você>\AppData\Local\MulletaFlix\data\introskipper\introskipper-v2.db" `
  --cache-db   "C:\Users\<você>\AppData\Local\MulletaFlix\data\introskipper\introskipper-cache.db" `
  --include-cache `
  --dry-run
```

Comece sempre com `--dry-run`: ele conta o que seria migrado sem gravar nada e sem criar o
schema. Confira os números, e só então rode sem `--dry-run`.

Numa instalação padrão do servidor os caminhos são os do exemplo acima e o destino já tem os
padrões certos (`127.0.0.1:3306`, usuário `root`, senha vazia, schema
`mulletaflix_introskipper`), então nenhuma outra opção é necessária.

`--help` lista todas as opções.

## O que é migrado

| Origem | Conteúdo |
|---|---|
| `Segments` | segmentos detectados, **tombstones** (segmentos que o usuário apagou) e segmentos do usuário |
| `SeasonStates` | ação do analisador por temporada e episódios de reanálise já resolvidos |
| `AnalyzedItems` | registro de quais itens já foram analisados, com hash de configuração e versão do arquivo |
| `DisabledItems` | itens com segmentos automáticos ocultos |
| `SeasonAnalysisOverrides` | janela de análise e preview customizados por temporada |
| `DetectionCache` | cache de detecção do FFmpeg (só com `--include-cache`; evita reanalisar o áudio) |

Os tombstones são o motivo principal para migrar em vez de deixar o plugin reanalisar:
eles registram que **o usuário apagou** um segmento, e sem eles a reanálise traria o
segmento de volta.

O que **não** é migrado, de propósito:

- `ImportHistory` — é o marcador do importador antigo, não dado do usuário;
- `ProjectionQueue` / `ProjectionExternalOperations` — fila interna presa ao banco antigo.
  Em vez de copiá-la, a ferramenta marca todos os itens afetados na fila do MariaDB, e é o
  próprio plugin que sincroniza com o Jellyfin.

## Garantias

- **Somente leitura na origem.** Os arquivos SQLite são abertos com `Mode=ReadOnly` e
  `Pooling=False`, e um `-wal` deixado pelo plugin antigo é lido sem checkpoint. Nada nos
  arquivos originais é alterado — inclusive o `-wal`, que continua intacto para uma
  eventual volta atrás.
- **Idempotente e não destrutivo.** Linha cuja chave já existe no destino é ignorada, nunca
  sobrescrita. Rodar duas vezes não duplica nada, e uma edição feita depois da primeira
  execução não é desfeita.
- **Ids preservados.** O `Id` de cada segmento é o mesmo que o Jellyfin usa em
  `MediaSegments`, então os segmentos migrados continuam casando com as linhas do Jellyfin.
- **Linhas inválidas não abortam a execução.** O schema MariaDB tem uma restrição de faixa
  (`EndTicks > StartTicks`) que o SQLite não tinha; uma linha corrompida é reportada como
  aviso e ignorada.
- **Todas as versões de schema.** A ferramenta lê as colunas via `pragma_table_info`, então
  aceita bancos anteriores à versão que adicionou `AnalyzedItems.FileVersion`, à que removeu
  `DisabledItems.SeasonId` e à que criou `SeasonAnalysisOverrides`.

## Depois de migrar

O servidor precisa estar atualizado (v12.0.41 ou superior) para que o plugin use o schema
MariaDB. Ao subir, o plugin encontra os dados migrados, processa a fila de projeção e
publica os segmentos no Jellyfin. Os arquivos `.db` antigos podem ser apagados depois de
confirmar que os intros e créditos aparecem normalmente — guarde-os até lá.

## Observação

Esta ferramenta é o único ponto do repositório que depende de SQLite, e é por isso que ela é
um projeto separado (`MulletaFlix-master/tools/MulletaFlix.IntroSkipperMigration`): o
servidor publicado não carrega nem distribui nenhum assembly SQLite.
