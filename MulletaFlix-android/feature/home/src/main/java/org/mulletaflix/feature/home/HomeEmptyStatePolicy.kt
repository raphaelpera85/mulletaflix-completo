package org.mulletaflix.feature.home

internal fun shouldShowEmptyHomeState(state: HomeState): Boolean =
    !state.isLoading &&
        state.error == null &&
        state.heroItem == null &&
        state.resumeItems.isEmpty() &&
        state.nextUpItems.isEmpty() &&
        state.favoriteItems.isEmpty() &&
        state.recentlyAddedByLibrary.values.all { it.isEmpty() } &&
        state.recentlyAddedErrorsByLibrary.isEmpty() &&
        state.liveTvChannels.isEmpty() &&
        state.libraries.isEmpty() &&
        state.resumeError == null &&
        state.nextUpError == null &&
        state.favoritesError == null &&
        state.librariesError == null &&
        state.liveTvError == null
