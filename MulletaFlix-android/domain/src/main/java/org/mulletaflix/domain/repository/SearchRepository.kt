package org.mulletaflix.domain.repository

import org.mulletaflix.domain.model.MediaItem

data class SearchHintItem(
    val id: String,
    val name: String,
    val type: String?,
    val year: Int?,
    val imageTag: String?,
)

/**
 * O que a busca devolveu.
 *
 * A busca ia ao servidor com `limit = 30` e **descartava** o `TotalRecordCount` que
 * vem na mesma resposta: procurar "a" numa biblioteca de 4 000 itens mostrava 30 e
 * nada na tela dizia que existiam mais. O total agora viaja junto com os itens, e
 * [isTruncated] é o que a tela usa para saber que ainda há páginas.
 *
 * [totalMatching] é nulo quando o servidor não informa — e nesse caso a tela não
 * afirma nada, porque não sabe.
 */
data class SearchResults(
    val items: List<MediaItem>,
    val totalMatching: Int? = null,
) {
    val isTruncated: Boolean
        get() = totalMatching != null && totalMatching > items.size
}

/** Quantos itens a busca pede por página. */
const val SEARCH_PAGE_SIZE = 30

interface SearchRepository {
    suspend fun searchHints(term: String, userId: String?): Result<List<SearchHintItem>>

    /**
     * Uma **página** de resultados, a partir de [startIndex].
     *
     * O `startIndex` existe para a tela poder pedir a página seguinte: antes a busca era
     * uma página só, e a única saída era dizer "refine a busca" para quem tinha 412
     * resultados e via 30.
     */
    suspend fun searchItems(
        term: String,
        userId: String,
        itemTypes: String? = null,
        startIndex: Int = 0,
    ): Result<SearchResults>
}
