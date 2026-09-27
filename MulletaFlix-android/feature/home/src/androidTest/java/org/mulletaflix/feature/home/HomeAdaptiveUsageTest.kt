package org.mulletaflix.feature.home

import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.widthIn
import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.Modifier
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onAllNodesWithContentDescription
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onRoot
import androidx.compose.ui.unit.dp
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Assume.assumeTrue
import org.junit.Rule
import org.junit.Test
import org.mulletaflix.designsystem.components.MediaCardShape
import org.mulletaflix.designsystem.components.isTelevisionDevice
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType
import java.util.concurrent.atomic.AtomicReference
import kotlin.math.floor
import kotlin.math.roundToInt

/** Executes on real phone, tablet and TV AVD profiles and measures a production Home section. */
class HomeAdaptiveUsageTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun phone_home_section_uses_compact_cards() = assertProductionCardWidth(
        expectedDevice = HomeDeviceClass.PHONE,
        expectedWidthDp = 130f,
    )

    @Test
    fun tablet_home_section_uses_larger_cards() = assertProductionCardWidth(
        expectedDevice = HomeDeviceClass.TABLET,
        expectedWidthDp = 149.5f,
    )

    @Test
    fun television_home_section_uses_denser_cards() = assertProductionCardWidth(
        expectedDevice = HomeDeviceClass.TV,
        expectedWidthDp = 117f,
    )

    private fun assertProductionCardWidth(expectedDevice: HomeDeviceClass, expectedWidthDp: Float) {
        val renderedDevice = AtomicReference<HomeDeviceClass?>()
        composeRule.setContent {
            BoxWithConstraints(Modifier.fillMaxSize()) {
            val isTelevision = isTelevisionDevice()
            val deviceClass = homeDeviceClass(maxWidth.value.roundToInt(), isTelevision)
            val layoutSpec = homeLayoutSpec(deviceClass)
            renderedDevice.set(deviceClass)

            MaterialTheme {
                Column(
                    modifier = Modifier
                        .fillMaxWidth()
                        .widthIn(max = layoutSpec.contentMaxWidthDp.dp)
                        .padding(horizontal = layoutSpec.horizontalPaddingDp.dp)
                        .align(androidx.compose.ui.Alignment.TopCenter),
                ) {
                    MediaSection(
                        title = "Adicionados recentemente",
                        items = (0 until 20).map { index ->
                            MediaItem("adaptive-home-card-$index", "Capa de teste $index", MediaItemType.Movie)
                        },
                        cardShape = MediaCardShape.Portrait,
                        cardWidth = 130.dp,
                        layoutSpec = layoutSpec,
                        onItemClick = {},
                    )
                }
            }
            }
        }

        composeRule.waitForIdle()
        assumeTrue("Run this assertion on the $expectedDevice AVD", renderedDevice.get() == expectedDevice)

        val firstCard = composeRule
            .onNodeWithContentDescription("Abrir Capa de teste 0")
            .fetchSemanticsNode()
        val actualWidthPx = firstCard.boundsInRoot.width
        val expectedWidthPx = with(composeRule.density) { expectedWidthDp.dp.toPx() }
        assertEquals("$expectedDevice Home poster width", expectedWidthPx, actualWidthPx, 1f)

        if (expectedDevice == HomeDeviceClass.TV) {
            val rootBounds = composeRule.onRoot().fetchSemanticsNode().boundsInRoot
            val rootWidthDp = with(composeRule.density) { rootBounds.width.toDp().value }
            val layoutSpec = homeLayoutSpec(expectedDevice)
            val rowWidthDp = rootWidthDp
                .coerceAtMost(layoutSpec.contentMaxWidthDp.toFloat()) - 2 * layoutSpec.horizontalPaddingDp - 32
            val visibleAtTvScale = composeRule
                .onAllNodesWithContentDescription("Abrir Capa de teste", substring = true)
                .fetchSemanticsNodes()
                .count { node -> node.boundsInRoot.left >= rootBounds.left && node.boundsInRoot.right <= rootBounds.right }
            val visibleAtPhoneScale = floor((rowWidthDp + 8f) / (130f + 8f)).toInt()

            assertTrue(
                "TV card scale should show more complete titles than unscaled phone cards; " +
                    "visible=$visibleAtTvScale, baseline=$visibleAtPhoneScale, row=${rowWidthDp}dp",
                visibleAtTvScale > visibleAtPhoneScale,
            )
        }
    }
}
