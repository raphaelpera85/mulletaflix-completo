package org.mulletaflix.feature.itemdetail

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.unit.dp

/** Uses a supporting pane on wide tablets while preserving the phone and TV flow. */
@Composable
internal fun AdaptiveItemDetailLayout(
    isTelevision: Boolean,
    modifier: Modifier = Modifier,
    hero: @Composable ColumnScope.() -> Unit,
    details: @Composable ColumnScope.() -> Unit,
) {
    val compactScrollState = rememberScrollState()
    val detailsScrollState = rememberScrollState()

    BoxWithConstraints(modifier = modifier) {
        if (!isTelevision && maxWidth >= 840.dp) {
            Row(
                modifier = Modifier.fillMaxSize().padding(horizontal = 24.dp, vertical = 16.dp),
                horizontalArrangement = Arrangement.spacedBy(24.dp),
            ) {
                Column(modifier = Modifier.weight(0.9f).fillMaxHeight(), content = hero)
                Column(
                    modifier = Modifier
                        .weight(1.1f)
                        .fillMaxHeight()
                        .verticalScroll(detailsScrollState)
                        .testTag("item-detail-details-scroll-pane"),
                    content = details,
                )
            }
        } else {
            Column(
                modifier = Modifier.fillMaxSize().verticalScroll(compactScrollState),
            ) {
                hero()
                details()
            }
        }
    }
}
