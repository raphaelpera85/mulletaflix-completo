package org.mulletaflix.domain.repository

import org.mulletaflix.domain.model.MediaItem

/**
 * Repository interface for media items — defined in domain, implemented in data.
 *
 * All methods return Result<T> to propagate network/db errors without exceptions
 * crossing layer boundaries.
 */
interface MediaRepository {

    // ── Home sections ────────────────────────────────────────────────────────

    suspend fun getResumeItems(userId: String, limit: Int = 12): Result<List<MediaItem>>

    suspend fun getLatestItems(userId: String, parentId: String? = null, limit: Int = 16): Result<List<MediaItem>>

    suspend fun getNextUp(userId: String, limit: Int = 12): Result<List<MediaItem>>

    // ── Library browsing ─────────────────────────────────────────────────────

    suspend fun getLibraries(userId: String): Result<List<MediaItem>>

    suspend fun getItems(
        userId: String,
        parentId: String? = null,
        includeItemTypes: String? = null,
        sortBy: String? = null,
        sortOrder: String? = null,
        filters: String? = null,
        searchTerm: String? = null,
        startIndex: Int = 0,
        limit: Int = 40,
        genres: String? = null,
        years: String? = null,
        isPlayed: Boolean? = null,
        isFavorite: Boolean? = null,
    ): Result<Pair<List<MediaItem>, Int>>   // items + total count

    // ── Item detail ──────────────────────────────────────────────────────────

    suspend fun getItem(userId: String, itemId: String): Result<MediaItem>

    suspend fun getSimilarItems(userId: String, itemId: String, limit: Int = 12): Result<List<MediaItem>>

    suspend fun getSeasons(userId: String, seriesId: String): Result<List<MediaItem>>

    suspend fun getEpisodes(userId: String, seriesId: String, seasonId: String? = null): Result<List<MediaItem>>

    suspend fun getSpecialFeatures(userId: String, itemId: String): Result<List<MediaItem>>

    // ── Playstate ────────────────────────────────────────────────────────────

    suspend fun markAsPlayed(userId: String, itemId: String): Result<Unit>

    suspend fun markAsUnplayed(userId: String, itemId: String): Result<Unit>

    suspend fun markAsFavorite(userId: String, itemId: String): Result<Unit>

    suspend fun unmarkAsFavorite(userId: String, itemId: String): Result<Unit>

    // ── Live TV ──────────────────────────────────────────────────────────────

    /**
     * Uma **prévia** dos canais ao vivo para o carrossel da Home — uma página limitada, não o
     * catálogo.
     *
     * Este método se chamava `getLiveTvChannels`, e sob esse nome era uma armadilha: o
     * `LiveTvRepository.getChannels` pagina até o fim (a lista da tela de TV ao vivo precisa de
     * todos os canais), mas quem precisasse da lista inteira e chamasse *este* aqui receberia
     * `LIVE_TV_CHANNEL_PAGE_SIZE` canais em silêncio, sem nada dizendo que faltava o resto.
     *
     * O carrossel da Home quer o oposto — uma amostra barata, que não dispare uma dúzia de
     * requisições ao abrir o app. Então o contrato virou o nome, e o limite deixou de ser herdado
     * do default da API para ficar explícito aqui.
     *
     * **Para a lista completa, use `LiveTvRepository.getChannels`.**
     */
    suspend fun getLiveTvChannelPreview(userId: String): Result<List<MediaItem>>
}
