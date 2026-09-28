package org.mulletaflix.feature.player

import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import org.junit.Rule
import org.junit.Test

class PlayerCastTvVisibilityTest {
    @get:Rule
    val compose = createComposeRule()

    @Test
    fun sender_control_is_not_composed_for_television_profile() {
        compose.setContent {
            MaterialTheme {
                PlayerCastAction(isTelevision = true, isCasting = false)
            }
        }

        compose.onNodeWithTag(PLAYER_CAST_CONTROL_TEST_TAG).assertDoesNotExist()
        compose.onNodeWithText("Transmitir").assertDoesNotExist()
    }
}
