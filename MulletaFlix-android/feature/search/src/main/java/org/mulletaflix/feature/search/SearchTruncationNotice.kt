package org.mulletaflix.feature.search

/**
 * Frase que diz quantos resultados existem, ou `null` quando não há mais o que buscar.
 *
 * A busca pedia uma página de 30 itens e **descartava** o `TotalRecordCount` da mesma
 * resposta: a tela mostrava 30 em silêncio, como se fossem todos. A frase nasceu dizendo
 * "refine a busca", porque não havia outra saída. Agora há — o botão "Carregar mais" logo
 * abaixo —, então a frase só informa o tamanho do acervo e deixa a decisão com quem está
 * lendo.
 *
 * [totalMatching] nulo significa "o servidor não informou" — e aí a função não inventa um
 * número nem sugere truncamento que não pode provar.
 */
internal fun searchTruncationNotice(shownCount: Int, totalMatching: Int?): String? {
    if (totalMatching == null || totalMatching <= shownCount) return null
    return "Mostrando $shownCount de $totalMatching resultados."
}
