package org.mulletaflix.feature.home

import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.width
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.unit.dp
import org.junit.Rule
import org.junit.Test

/** Usage-level checks for the layout contracts used by phone, tablet and TV surfaces. */
class HomeAdaptiveUsageTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun tablet_surface_uses_tablet_contract() {
        composeRule.setContent { AdaptiveContractSurface(widthDp = 800, isTelevision = false) }
        composeRule.onNodeWithText("Tablet: conteúdo centralizado").assertIsDisplayed()
    }

    @Test
    fun television_surface_uses_focus_contract() {
        composeRule.setContent { AdaptiveContractSurface(widthDp = 1280, isTelevision = true) }
        composeRule.onNodeWithText("TV: espaçamento para foco").assertIsDisplayed()
    }

    @Test
    fun phone_surface_keeps_compact_contract() {
        composeRule.setContent { AdaptiveContractSurface(widthDp = 411, isTelevision = false) }
        composeRule.onNodeWithText("Celular: layout compacto").assertIsDisplayed()
    }
}

@Composable
private fun AdaptiveContractSurface(widthDp: Int, isTelevision: Boolean) {
    val spec = homeLayoutSpec(homeDeviceClass(widthDp, isTelevision))
    Box(
        modifier = Modifier
            .width(widthDp.dp)
            .height(100.dp),
    ) {
        MaterialTheme {
            val label = when (homeDeviceClass(widthDp, isTelevision)) {
                HomeDeviceClass.PHONE -> "Celular: layout compacto"
                HomeDeviceClass.TABLET -> "Tablet: conteúdo centralizado"
                HomeDeviceClass.TV -> if (spec.usesFocusFriendlySpacing) {
                    "TV: espaçamento para foco"
                } else {
                    "TV"
                }
            }
            Text(label)
        }
    }
}
