package org.mulletaflix.feature.settings

import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.FastForward
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.test.assertIsOff
import androidx.compose.ui.test.assertIsOn
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.performClick
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class AutomaticIntroSkipToggleTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun automaticIntroSkipIsOptInAndSwitchExposesAccessibleState() {
        composeRule.setContent {
            MaterialTheme {
                var enabled by remember { mutableStateOf(false) }
                SettingsToggle(
                    icon = Icons.Default.FastForward,
                    title = "Pular abertura automaticamente",
                    subtitle = "Avançar sem confirmação durante uma introdução detectada",
                    checked = enabled,
                    onCheckedChange = { enabled = it },
                )
            }
        }

        val toggle = composeRule.onNodeWithContentDescription("Pular abertura automaticamente")
        toggle.assertIsOff()
        toggle.performClick()
        toggle.assertIsOn()
    }
}
