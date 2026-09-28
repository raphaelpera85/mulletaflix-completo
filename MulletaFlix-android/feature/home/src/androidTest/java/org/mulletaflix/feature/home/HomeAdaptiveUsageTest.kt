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
import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
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
    fun home_section_uses_card_width_for_current_device_profile() {
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
        val deviceClass = checkNotNull(renderedDevice.get()) {
            "Home should resolve the current device profile"
        }
        val expectedProfile = checkNotNull(
            InstrumentationRegistry.getArguments().getString("expectedDeviceProfile"),
        ) { "Set expectedDeviceProfile to PHONE, TABLET or TV for this AVD run" }
        assertEquals("Home should detect the requested AVD profile", expectedProfile, deviceClass.name)
        val expectedWidthDp = when (deviceClass) {
            HomeDeviceClass.PHONE -> 130f
            HomeDeviceClass.TABLET -> 149.5f
            HomeDeviceClass.TV -> 117f
        }

        val firstCard = composeRule
            .onNodeWithContentDescription("Abrir Capa de teste 0")
            .fetchSemanticsNode()
        val actualWidthPx = firstCard.boundsInRoot.width
        val expectedWidthPx = with(composeRule.density) { expectedWidthDp.dp.toPx() }
        assertEquals("$deviceClass Home poster width", expectedWidthPx, actualWidthPx, 1f)

        if (deviceClass == HomeDeviceClass.TV) {
            val rootBounds = composeRule.onRoot().fetchSemanticsNode().boundsInRoot
            val rootWidthDp = with(composeRule.density) { rootBounds.width.toDp().value }
            val layoutSpec = homeLayoutSpec(deviceClass)
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
