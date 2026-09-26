package org.mulletaflix.feature.library

import androidx.compose.foundation.lazy.grid.LazyGridState
import androidx.compose.foundation.lazy.grid.rememberLazyGridState
import androidx.compose.runtime.Composable

/**
 * Keeps catalog position in Compose's saved-state registry across Activity/process
 * recreation. Both Library and Favorites must use the same restoration contract.
 */
@Composable
internal fun rememberLibraryGridScrollState(): LazyGridState = rememberLazyGridState()
