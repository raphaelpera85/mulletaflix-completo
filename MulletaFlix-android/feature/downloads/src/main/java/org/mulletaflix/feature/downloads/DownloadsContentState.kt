package org.mulletaflix.feature.downloads

internal enum class DownloadsContentState {
    Loading,
    Empty,
    Content,
}

internal fun downloadsContentState(isLoaded: Boolean, itemCount: Int): DownloadsContentState = when {
    !isLoaded -> DownloadsContentState.Loading
    itemCount == 0 -> DownloadsContentState.Empty
    else -> DownloadsContentState.Content
}
