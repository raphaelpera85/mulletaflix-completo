package org.mulletaflix.feature.syncplay

import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableLongStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class SyncPlayRealtimeSnackbarTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun repeated_event_sequence_shows_the_same_message_again() {
        composeRule.setContent {
            val hostState = remember { SnackbarHostState() }
            var sequence by remember { mutableLongStateOf(0L) }
            var message by remember { mutableStateOf<String?>(null) }

            MaterialTheme {
                SyncPlayRealtimeSnackbarEffect(sequence, message, hostState)
                SnackbarHost(hostState)
                Button(onClick = {
                    message = "Mídia da sala atualizada"
                    sequence++
                }) {
                    Text("Simular evento")
                }
            }
        }

        val trigger = composeRule.onNodeWithText("Simular evento")
        trigger.performClick()
        composeRule.onNodeWithText("Mídia da sala atualizada").assertIsDisplayed()
        trigger.performClick()
        composeRule.waitForIdle()
        composeRule.onNodeWithText("Mídia da sala atualizada").assertIsDisplayed()
    }
}
