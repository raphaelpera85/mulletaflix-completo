package org.mulletaflix.feature.search

import androidx.compose.foundation.lazy.LazyListState
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.runtime.Composable

/** Saved vertical position for Search lists across Activity/process recreation. */
@Composable
internal fun rememberSearchScrollState(): LazyListState = rememberLazyListState()

/** Saved horizontal position for grouped Search result carousels. */
@Composable
internal fun rememberSearchCarouselScrollState(): LazyListState = rememberLazyListState()
