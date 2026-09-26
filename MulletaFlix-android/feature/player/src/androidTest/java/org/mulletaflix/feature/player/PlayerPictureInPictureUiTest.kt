package org.mulletaflix.feature.player

import androidx.compose.foundation.layout.Box
import androidx.compose.material3.Text
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.test.assertCountEquals
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onAllNodesWithTag
import androidx.compose.ui.test.onNodeWithTag
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class PlayerPictureInPictureUiTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun playerOverlaysHideInPipAndReturnAfterExit() {
        PlayerPictureInPictureController.onPictureInPictureModeChanged(false)
        composeRule.setContent {
            val isInPip = PlayerPictureInPictureController.isInPictureInPictureMode
                .collectAsStateWithLifecycle().value
            Box {
                PlayerOverlayContent(isInPictureInPictureMode = isInPip) {
                    Text("Player controls", Modifier.testTag("player-overlay"))
                }
            }
        }

        composeRule.onNodeWithTag("player-overlay").assertIsDisplayed()
        composeRule.runOnIdle {
            PlayerPictureInPictureController.onPictureInPictureModeChanged(true)
        }
        composeRule.onAllNodesWithTag("player-overlay").assertCountEquals(0)
        composeRule.runOnIdle {
            PlayerPictureInPictureController.onPictureInPictureModeChanged(false)
        }
        composeRule.onNodeWithTag("player-overlay").assertIsDisplayed()
    }
}
