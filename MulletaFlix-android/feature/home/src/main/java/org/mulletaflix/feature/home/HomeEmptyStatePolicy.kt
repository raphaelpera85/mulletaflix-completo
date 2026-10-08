package org.mulletaflix.feature.home

import org.mulletaflix.domain.model.isBookContent

internal fun shouldShowEmptyHomeState(state: HomeState, isTelevision: Boolean = false): Boolean {
    val hasVisibleLibrary = state.libraries.any { shouldShowLibraryOnDevice(it, isTelevision) }
    val hasVisibleRecentSection = homeRecentLibrarySections(
        libraries = state.libraries,
        recentItemsByLibraryId = state.recentlyAddedByLibrary,
        errorsByLibraryId = state.recentlyAddedErrorsByLibrary,
        isTelevision = isTelevision,
    ).isNotEmpty()
    return !state.isLoading &&
        state.error == null &&
        (state.heroItem == null || (isTelevision && state.heroItem.type.isBookContent())) &&
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
