package org.mulletaflix.domain.usecase

import kotlinx.coroutines.async
import kotlinx.coroutines.coroutineScope
import org.mulletaflix.domain.model.HomeFeed
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.repository.MediaRepository
import javax.inject.Inject

/**
 * Use case coordinating home screen media sections in parallel.
 */
class GetHomeFeedUseCase @Inject constructor(
    private val mediaRepository: MediaRepository,
) {
    suspend operator fun invoke(userId: String): Result<HomeFeed> = runCatching {
        require(userId.isNotBlank()) { "O identificador do usuário é obrigatório." }
        coroutineScope {
            val resumeDeferred = async { mediaRepository.getResumeItems(userId) }
            val nextUpDeferred = async { mediaRepository.getNextUp(userId) }
            val librariesDeferred = async { mediaRepository.getLibraries(userId) }
            val liveTvDeferred = async { mediaRepository.getLiveTvChannels(userId) }
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

            val recentlyAdded = libraries.map { lib ->
                async {
                    lib.name to mediaRepository
                        .getLatestItems(userId, parentId = lib.id)
                        .getOrDefault(emptyList())
                }
            }.map { it.await() }.toMap()

            val resumeItems = resumeResult.getOrDefault(emptyList())
            val nextUpItems = nextUpResult.getOrDefault(emptyList())
            val liveTvChannels = liveTvResult.getOrDefault(emptyList())
            val favoriteItems = favoritesResult.getOrNull()?.first.orEmpty()

            if (libraries.isEmpty() && resumeItems.isEmpty() && nextUpItems.isEmpty() && librariesResult.isFailure) {
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
                liveTvChannels = liveTvChannels,
                libraries = libraries,
            )
        }
    }
}
