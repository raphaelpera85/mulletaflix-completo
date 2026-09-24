package org.mulletaflix.feature.home

import androidx.compose.foundation.lazy.LazyListState
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.runtime.Composable

/** Saved vertical position for Home across Activity/process recreation. */
@Composable
internal fun rememberHomeScrollState(): LazyListState = rememberLazyListState()

/** Saved horizontal position for Home carousels across Activity/process recreation. */
@Composable
internal fun rememberHomeCarouselScrollState(): LazyListState = rememberLazyListState()
