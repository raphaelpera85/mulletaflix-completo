# MulletaFlix Server v12.1.17

Release de produção do servidor Windows/Linux e interface web incluída.

## Correções

- O backup automático do MongoDB para o Supabase agora aguarda a fila de uploads Telegram ficar ociosa, verificando novamente a cada 30 segundos. O backup manual permanece disponível.
- A sincronização delta do Supabase passa a usar uma janela de sobreposição de 15 minutos e atualiza `updated_at` no upsert, reduzindo o risco de perder alterações próximas ao cursor.
- Se o catálogo remoto estiver vazio apesar de existir histórico de backup, o servidor inicia uma sincronização integral de recuperação. Erros ao consultar o histórico/cursor interrompem o delta em vez de provocar um backup integral inesperado.
- O backup só é registrado como concluído depois de confirmar a contagem remota de arquivos.
- A listagem Nebula/Mongo inclui as categorias sob o diretório raiz e percorre descendentes usando IDs `ObjectId` e strings hexadecimais, sem carregar payloads de mídia para verificar se há arquivos publicados.

## Validação

- Suíte `Jellyfin.Server.Implementations` em Release: 1.408 aprovados, 39 ignorados e 0 falhas.
- Integrações Mongo isoladas: 20/20 aprovadas.
- Os testes automatizados não substituem validação de upload real no Telegram. Nesta publicação não foi possível fazer o A/B de velocidade com upload real nem conferir a unidade N:, que não está mapeada neste ambiente.
- Auditoria read-only das tabelas Supabase encontrou zero duplicatas; nenhum registro foi removido.

## Artefatos

- `mulletaflix-update-win-x64.zip` — atualização Windows x64.
- `mulletaflix_12.1.17_windows-x64.exe` — instalador Windows x64.
- `mulletaflix_12.1.17_linux-x64.tar.gz` — pacote Linux x64.
