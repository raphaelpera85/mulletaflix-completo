package org.mulletaflix.feature.home

import org.mulletaflix.domain.model.MediaItemType

internal fun shouldShowEmptyHomeState(state: HomeState, isTelevision: Boolean = false): Boolean {
    val hasVisibleLibrary = state.libraries.any { shouldShowLibraryOnDevice(it, isTelevision) }
    val hasVisibleRecentSection = state.libraries.any { library ->
        shouldShowLibraryOnDevice(library, isTelevision) &&
            (state.recentlyAddedByLibrary[library.id].orEmpty().isNotEmpty() ||
                state.recentlyAddedErrorsByLibrary[library.id] != null)
    }
    return !state.isLoading &&
        state.error == null &&
        (state.heroItem == null || (isTelevision && state.heroItem.type == MediaItemType.Book)) &&
        homeMediaItemsForDevice(state.resumeItems, isTelevision).isEmpty() &&
        homeMediaItemsForDevice(state.nextUpItems, isTelevision).isEmpty() &&
        homeMediaItemsForDevice(state.favoriteItems, isTelevision).isEmpty() &&
        !hasVisibleRecentSection &&
        state.liveTvChannels.isEmpty() &&
        !hasVisibleLibrary &&
        state.resumeError == null &&
        state.nextUpError == null &&
        state.favoritesError == null &&
        state.librariesError == null &&
        state.liveTvError == null
}
