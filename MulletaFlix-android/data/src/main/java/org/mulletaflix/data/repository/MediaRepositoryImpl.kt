package org.mulletaflix.data.repository

import org.mulletaflix.core.api.LIVE_TV_CHANNEL_PAGE_SIZE
import org.mulletaflix.core.api.MulletaFlixApiService
import org.mulletaflix.data.mapper.toDomain
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.repository.MediaRepository
import javax.inject.Inject
import javax.inject.Singleton

/**
 * MediaRepository implementation — rede pura, sem cache local.
 *
 * Havia aqui um KDoc descrevendo uma "offline-first strategy" em quatro passos (emitir do
 * cache, buscar da rede, atualizar o cache, emitir de novo) e um `MediaItemDao` injetado.
 * Nada disso era verdade: **nenhum** método de produção escrevia no banco e os únicos dois
 * que o liam (`observeFavorites` / `observeRecentlyWatched`) não tinham chamador. O cache
 * era um `mulletaflix.db` que nunca chegava a ser criado, com chave primária `id` sozinho
 * enquanto a linha guardava `userId` — ou seja, se alguém o ligasse, dois usuários com o
 * mesmo item se sobrescreveriam. Foi removido junto com o `Room` (v1.2.81); quem quiser
 * cache offline de verdade precisa desenhá-lo, não ressuscitar isto.
 */
@Singleton
class MediaRepositoryImpl @Inject constructor(
    private val api: MulletaFlixApiService,
) : MediaRepository {

    override suspend fun getResumeItems(userId: String, limit: Int): Result<List<MediaItem>> =
        suspendRunCatching {
            api.getResumeItems(userId = userId, limit = limit)
                .items
                .map { it.toDomain() }
        }

    override suspend fun getLatestItems(userId: String, parentId: String?, limit: Int): Result<List<MediaItem>> =
        suspendRunCatching {
            api.getLatestItems(userId = userId, parentId = parentId, limit = limit)
                .map { it.toDomain() }
        }

    override suspend fun getNextUp(userId: String, limit: Int): Result<List<MediaItem>> =
        suspendRunCatching {
            api.getNextUp(userId = userId, limit = limit)
                .items
                .map { it.toDomain() }
        }

    override suspend fun getLibraries(userId: String): Result<List<MediaItem>> =
        suspendRunCatching {
            api.getUserViews(userId = userId)
                .items
                .map { it.toDomain() }
        }

    override suspend fun getItems(
        userId: String,
        parentId: String?,
        includeItemTypes: String?,
        sortBy: String?,
        sortOrder: String?,
        filters: String?,
        searchTerm: String?,
        startIndex: Int,
        limit: Int,
        genres: String?,
        years: String?,
        officialRatings: String?,
        isPlayed: Boolean?,
        isFavorite: Boolean?,
    ): Result<Pair<List<MediaItem>, Int>> = suspendRunCatching {
        val result = api.getItems(
            userId = userId,
            parentId = parentId,
            includeItemTypes = includeItemTypes,
            sortBy = sortBy,
            sortOrder = sortOrder ?: "Ascending",
            filters = filters,
            searchTerm = searchTerm,
            startIndex = startIndex,
            limit = limit,
            genres = genres,
            years = years,
            officialRatings = officialRatings,
            isPlayed = isPlayed,
            isFavorite = isFavorite,
        )
        Pair(result.items.map { it.toDomain() }, result.totalRecordCount)
    }

    override suspend fun getItem(userId: String, itemId: String): Result<MediaItem> =
        suspendRunCatching { api.getItem(userId, itemId).toDomain() }

    override suspend fun getSimilarItems(userId: String, itemId: String, limit: Int): Result<List<MediaItem>> =
        suspendRunCatching { api.getSimilarItems(itemId, userId, limit).items.map { it.toDomain() } }

    override suspend fun getSeasons(userId: String, seriesId: String): Result<List<MediaItem>> =
        suspendRunCatching { api.getSeasons(seriesId, userId).items.map { it.toDomain() } }

    override suspend fun getEpisodes(userId: String, seriesId: String, seasonId: String?): Result<List<MediaItem>> =
        suspendRunCatching { api.getEpisodes(seriesId, userId, seasonId).items.map { it.toDomain() } }

    override suspend fun getSpecialFeatures(userId: String, itemId: String): Result<List<MediaItem>> =
        suspendRunCatching { api.getSpecialFeatures(itemId, userId).map { it.toDomain() } }

    override suspend fun markAsPlayed(userId: String, itemId: String): Result<Unit> =
        suspendRunCatching { api.markAsPlayed(userId, itemId); Unit }

    override suspend fun markAsUnplayed(userId: String, itemId: String): Result<Unit> =
        suspendRunCatching { api.markAsUnplayed(userId, itemId); Unit }

    override suspend fun markAsFavorite(userId: String, itemId: String): Result<Unit> =
        suspendRunCatching { api.markAsFavorite(userId, itemId); Unit }

    override suspend fun unmarkAsFavorite(userId: String, itemId: String): Result<Unit> =
        suspendRunCatching { api.unmarkAsFavorite(userId, itemId); Unit }

    /**
     * Uma página de canais ao vivo — o suficiente para o carrossel da Home.
     *
     * O `limit` é **explícito** de propósito. Antes ele vinha do default do Retrofit
     * (`@Query("Limit") limit: Int = LIVE_TV_CHANNEL_PAGE_SIZE`) e o nome do método
     * (`getLiveTvChannels`) sugeria a lista inteira; qualquer chamador futuro que quisesse a
     * lista completa receberia 100 canais sem aviso. Ver o KDoc do contrato em
     * `MediaRepository.getLiveTvChannelPreview`.
     */
    override suspend fun getLiveTvChannelPreview(userId: String): Result<List<MediaItem>> =
        suspendRunCatching {
            api.getLiveTvChannels(userId = userId, limit = LIVE_TV_CHANNEL_PAGE_SIZE)
                .items
                .map { it.toDomain() }
        }
}
