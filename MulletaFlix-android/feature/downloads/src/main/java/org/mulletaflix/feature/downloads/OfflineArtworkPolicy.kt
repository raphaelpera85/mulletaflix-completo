package org.mulletaflix.feature.downloads

import androidx.compose.ui.layout.ContentScale

/** Offline movie/series posters must remain fully visible in the compact row. */
internal fun offlineArtworkContentScale(): ContentScale = ContentScale.Fit
