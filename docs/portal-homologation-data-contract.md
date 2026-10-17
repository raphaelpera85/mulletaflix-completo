# Contrato dos dados públicos de homologação

`homologacao-status.json` é servido publicamente pelo portal. A página pode mostrar uma lista limitada de eventos, mas o endpoint entrega o histórico inteiro; trate todo campo do JSON como conteúdo público e potencialmente indexável.

## Regras de publicação

- Não inclua caminhos absolutos de máquinas, pastas de perfil, nomes de usuário do sistema, segredos, tokens, credenciais ou logs privados.
- Em `local`, use rótulos genéricos como `checkout local`, nunca um caminho de disco.
- Em `location` e `evidence`, prefira caminhos relativos ao repositório e trechos mínimos; remova qualquer caminho pessoal ou dado sensível antes de publicar.
- Preserve os fatos de homologação, contagens, limites, status e pendências ao sanitizar os registros.
- Execute os testes do portal antes de publicar: `node --test tests/homologacao.test.mjs`.

O teste `public homologation data contains no absolute Windows user-profile paths` percorre o JSON inteiro, não apenas os eventos atualmente renderizados na interface.
