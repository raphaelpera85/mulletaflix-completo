package org.mulletaflix.domain.usecase

import kotlinx.coroutines.async
import kotlinx.coroutines.coroutineScope
import org.mulletaflix.domain.model.HomeFeed
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.repository.MediaRepository
import org.mulletaflix.domain.repository.HomeFeedCache
import org.mulletaflix.domain.repository.NoOpHomeFeedCache
import javax.inject.Inject

/**
 * Use case coordinating home screen media sections in parallel.
 */
class GetHomeFeedUseCase @Inject constructor(
    private val mediaRepository: MediaRepository,
    private val homeFeedCache: HomeFeedCache,
) {
    constructor(mediaRepository: MediaRepository) : this(mediaRepository, NoOpHomeFeedCache)

    suspend operator fun invoke(userId: String): Result<HomeFeed> = runCatching {
        require(userId.isNotBlank()) { "O identificador do usuário é obrigatório." }
        coroutineScope {
            val cached = runCatching { homeFeedCache.read(userId) }.getOrNull()
            val resumeDeferred = async { mediaRepository.getResumeItems(userId) }
            val nextUpDeferred = async { mediaRepository.getNextUp(userId) }
            val librariesDeferred = async { mediaRepository.getLibraries(userId) }
            val liveTvDeferred = async { mediaRepository.getLiveTvChannelPreview(userId) }
            val favoritesDeferred = async {
                runCatching {
                    mediaRepository.getItems(
                        userId = userId,
                        filters = "IsFavorite",
                        sortBy = "SortName",
                        sortOrder = "Ascending",
                        limit = 12,
                    )
                }.getOrElse { Result.failure(it) }
            }

            val resumeResult = resumeDeferred.await()
            val nextUpResult = nextUpDeferred.await()
            val librariesResult = librariesDeferred.await()
            val liveTvResult = liveTvDeferred.await()
            val favoritesResult = favoritesDeferred.await()

            val libraries = librariesResult.getOrDefault(emptyList())

            val recentResults = libraries.map { lib ->
                async {
                    lib.id to mediaRepository
                        .getLatestItems(userId, parentId = lib.id)
                }
            }.map { it.await() }.toMap()
            val recentlyAdded = recentResults.mapValues { (_, result) -> result.getOrDefault(emptyList()) }
            val libraryNamesById = libraries.associate { it.id to it.name }
            val recentlyAddedErrors = recentResults.mapNotNull { (libraryId, result) ->
                val libraryName = libraryNamesById[libraryId] ?: return@mapNotNull null
                result.sectionError("Não foi possível carregar Adicionados Recentemente — $libraryName.")
                    ?.let { libraryId to it }
            }.toMap()

            val resumeFromCache = resumeResult.exceptionOrNull().isOfflineEligible() &&
                cached?.resumeSavedAtEpochMillis?.let { it > 0 } == true
            val favoritesFromCache = favoritesResult.exceptionOrNull().isOfflineEligible() &&
                cached?.favoritesSavedAtEpochMillis?.let { it > 0 } == true
            val resumeItems = if (resumeFromCache) cached!!.resumeItems
                else resumeResult.getOrDefault(emptyList())
            val nextUpItems = nextUpResult.getOrDefault(emptyList())
            val liveTvChannels = liveTvResult.getOrDefault(emptyList())
            val favoriteItems = if (favoritesFromCache) cached!!.favoriteItems
                else favoritesResult.getOrNull()?.first.orEmpty()

            if (resumeResult.isSuccess || favoritesResult.isSuccess) {
                runCatching {
                    homeFeedCache.write(
                        userId = userId,
                        resumeItems = resumeResult.getOrNull(),
                        favoriteItems = favoritesResult.getOrNull()?.first,
                    )
                }
            }

            if (
                libraries.isEmpty() && resumeItems.isEmpty() && nextUpItems.isEmpty() &&
                favoriteItems.isEmpty() && liveTvChannels.isEmpty() && librariesResult.isFailure
            ) {
                throw librariesResult.exceptionOrNull() ?: Exception("Não foi possível carregar o catálogo.")
            }

            val hero = resumeItems.firstOrNull()
                ?: recentlyAdded.values.flatten().firstOrNull { it.backdropImageTags.isNotEmpty() }

            HomeFeed(
                heroItem = hero,
                resumeItems = resumeItems,
                nextUpItems = nextUpItems,
                favoriteItems = favoriteItems,
                recentlyAddedByLibrary = recentlyAdded,
                recentlyAddedErrorsByLibrary = recentlyAddedErrors,
                liveTvChannels = liveTvChannels,
                libraries = libraries,
                resumeError = resumeResult.sectionError("Não foi possível carregar Continuar Assistindo."),
                nextUpError = nextUpResult.sectionError("Não foi possível carregar Próximo Episódio."),
                favoritesError = favoritesResult.sectionError("Não foi possível carregar Minha Lista."),
                // A Home sem blocos de biblioteca era indistinguível de uma conta sem
                // biblioteca nenhuma. O erro sobe como estado, não como lista vazia.
                librariesError = librariesResult.sectionError("Não foi possível carregar suas bibliotecas."),
                // Mesmo defeito, na TV ao vivo: servidor com TV desligada responde **sucesso
                // com zero canais** e aí o carrossel some em silêncio, que é o certo; uma
                // falha de rede/5xx também sumia em silêncio, e isso não é.
                liveTvError = liveTvResult.sectionError("Não foi possível carregar a TV ao vivo."),
                resumeFromCache = resumeFromCache,
                favoritesFromCache = favoritesFromCache,
                cachedAtEpochMillis = when {
                    resumeFromCache && favoritesFromCache -> listOfNotNull(
                        cached.resumeSavedAtEpochMillis.takeIf { it > 0 },
                        cached.favoritesSavedAtEpochMillis.takeIf { it > 0 },
                    ).minOrNull()
                    resumeFromCache -> cached!!.resumeSavedAtEpochMillis.takeIf { it > 0 }
                    favoritesFromCache -> cached!!.favoritesSavedAtEpochMillis.takeIf { it > 0 }
                    else -> null
                },
            )
        }
    }

    suspend fun getCachedHomeSections(userId: String) =
        runCatching { homeFeedCache.read(userId) }

    /** Refreshes one library's Home preview without querying other feed sections. */
    suspend fun getLatestItemsForLibrary(userId: String, libraryId: String): Result<List<MediaItem>> {
        require(userId.isNotBlank()) { "O identificador do usuário é obrigatório." }
        require(libraryId.isNotBlank()) { "O identificador da biblioteca é obrigatório." }
        return mediaRepository.getLatestItems(userId, parentId = libraryId)
    }
}

/**
 * Mensagem de erro de uma seção da Home, ou `null` quando a seção carregou.
 *
 * Uma seção que **falhou** tem de se anunciar; uma seção que respondeu vazia não é erro.
 * Concentrar isso aqui mantém as duas seções (bibliotecas e TV ao vivo) contando a mesma
 * história, em vez de cada uma inventar o seu texto.
 */
private fun <T> Result<T>.sectionError(fallback: String): String? =
    exceptionOrNull()?.let { error ->
        error.localizedMessage?.takeIf(String::isNotBlank) ?: fallback
    }

private fun Throwable?.isOfflineEligible(): Boolean = this is java.io.IOException
