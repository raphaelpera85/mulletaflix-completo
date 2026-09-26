package org.mulletaflix.domain.model

/**
 * Domain model representing aggregated sections for the home screen.
 *
 * [librariesError] is not null when the rest of the feed loaded but `/Views` did not.
 * The failure used to be swallowed unless *every* other section was empty too, so a
 * viewer with a working "Continuar Assistindo" saw a Home with no library tiles — the
 * exact screen a user with no libraries sees, with no error and no way to retry.
 *
 * [liveTvError] é o mesmo defeito, na seção de TV ao vivo, e sobreviveu à correção das
 * bibliotecas: a falha ao buscar `LiveTv/Channels` era achatada em lista vazia e a Home
 * simplesmente não desenhava o carrossel. Servidor com TV ao vivo desligada e servidor
 * que respondeu 500 davam a mesma tela. Os dois casos são distinguíveis: desligada é
 * **sucesso com zero canais**, falha é **falha**.
 */
data class HomeFeed(
    val heroItem: MediaItem? = null,
    val resumeItems: List<MediaItem> = emptyList(),
    val nextUpItems: List<MediaItem> = emptyList(),
    val favoriteItems: List<MediaItem> = emptyList(),
    val recentlyAddedByLibrary: Map<String, List<MediaItem>> = emptyMap(),
    val recentlyAddedErrorsByLibrary: Map<String, String> = emptyMap(),
    val liveTvChannels: List<MediaItem> = emptyList(),
    val libraries: List<MediaItem> = emptyList(),
    val resumeError: String? = null,
    val nextUpError: String? = null,
    val favoritesError: String? = null,
    val librariesError: String? = null,
    val liveTvError: String? = null,
)
