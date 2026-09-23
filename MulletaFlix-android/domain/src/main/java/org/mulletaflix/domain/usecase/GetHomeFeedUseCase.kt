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
                // A Home sem blocos de biblioteca era indistinguível de uma conta sem
                // biblioteca nenhuma. O erro sobe como estado, não como lista vazia.
                librariesError = librariesResult.sectionError("Não foi possível carregar suas bibliotecas."),
                // Mesmo defeito, na TV ao vivo: servidor com TV desligada responde **sucesso
                // com zero canais** e aí o carrossel some em silêncio, que é o certo; uma
                // falha de rede/5xx também sumia em silêncio, e isso não é.
                liveTvError = liveTvResult.sectionError("Não foi possível carregar a TV ao vivo."),
            )
        }
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
