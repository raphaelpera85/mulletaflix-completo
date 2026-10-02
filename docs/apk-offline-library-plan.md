# APK: consulta offline da última biblioteca carregada

## INTENT

INTENT: `LibraryViewModel.loadLibrary` encerra a carga offline sem restaurar itens; `LibraryScreen` já mostra um banner de desconexão e `HomeFeedCacheRepositoryImpl` persiste cartões seguros isolados por servidor/conta. Implementar snapshot local limitado do resultado confirmado da biblioteca para consulta offline, sem reproduzir mídia que não foi baixada.

## Critério de aceite (Gauntlet Loop)

- Builder: snapshots das últimas bibliotecas confirmadas permitem reabrir a mesma biblioteca sem rede após reinício; conta, servidor ou biblioteca divergentes não podem reutilizar dados.
- Builder: persistir somente metadados necessários aos cards; sem `MediaSource`, streams, URLs de mídia, caminhos de arquivo, credenciais ou tokens. Limitar volume e tamanho de campos.
- Builder: estado offline identifica snapshot, horário e se a lista salva é parcial; paginação, filtros e ordenação que dependem do servidor não alteram nem descrevem falsamente o snapshot; Android TV continua excluindo Livros.
- Evaluator: testes unitários de restauração offline, biblioteca divergente, sessão divergente, cache ausente, limite do snapshot e atualização pelo primeiro carregamento/paginação. Teste de DTO/serialização garante ausência de dados sensíveis.
- Quality bar: testes Android unitários, compilação dos testes instrumentados do APK, lint e assemble; instrumentação de UI em AVD celular e TV se o ambiente suportar. Encerrar AVDs após uso.
- Sem release nesta tarefa. Se uma release APK for autorizada/criada depois, sincronizar no portal a versão, link do APK e notas correspondentes; nenhuma alteração do servidor.

## Plano

1. Criar contrato de domínio e cache persistente versionado/escopado, com DTO enxuto e tamanho limitado.
2. Integrar `LibraryViewModel` para gravar respostas e restaurar somente a biblioteca correspondente, com cancelamento e proteção contra troca de sessão.
3. Ajustar a tela para comunicar leitura offline e impedir ações de consulta remota que alterariam filtros/ordem localmente.
4. Adicionar testes adversariais em camadas, executar Quality Bar, revisar o diff APK-only e registrar evidências.

## Riscos mobile (MFRI)

MFRI aproximado: plataforma 5 + acessibilidade 4 − complexidade 3 − risco de performance 4 − dependência offline 1 = 1. Mitigação: até 8 bibliotecas recentes, máximo 200 cards por biblioteca e campos textuais truncados, sem novos gestos, banner textual acessível, operações remotas desabilitadas offline, teste de isolamento de identidade e preservação de comportamento telefone/tablet/TV.
