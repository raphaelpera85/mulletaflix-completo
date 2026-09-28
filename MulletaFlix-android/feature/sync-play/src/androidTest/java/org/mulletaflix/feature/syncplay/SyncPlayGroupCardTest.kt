package org.mulletaflix.feature.syncplay

import androidx.compose.ui.test.assertIsNotEnabled
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import org.mulletaflix.domain.repository.SyncPlayGroup

class SyncPlayGroupCardTest {
    @get:Rule
    val compose = createComposeRule()

    @Test
    fun join_button_is_disabled_during_manual_refresh() {
        var joinCount = 0
        compose.setContent {
            SyncPlayGroupCard(
                group = SyncPlayGroup("group-1", "Sala da família", "Playing", listOf("Raphael")),
                joinEnabled = false,
                onJoin = { joinCount++ },
            )
        }

        compose.onNodeWithText("Sala da família").assertIsDisplayed()
        compose.onNodeWithText("Entrar na sala")
            .assertIsDisplayed()
            .assertIsNotEnabled()
            .performClick()
        compose.runOnIdle { assertEquals(0, joinCount) }
    }

    @Test
    fun join_button_invokes_action_when_snapshot_is_fresh() {
        var joinCount = 0
        compose.setContent {
            SyncPlayGroupCard(
                group = SyncPlayGroup("group-1", "Sala da família", "Playing", listOf("Raphael")),
                joinEnabled = true,
                onJoin = { joinCount++ },
            )
        }

        compose.onNodeWithText("Entrar na sala").performClick()
        compose.runOnIdle { assertEquals(1, joinCount) }
    }

    @Test
    fun stale_snapshot_is_explained_and_cannot_be_joined() {
        var joinCount = 0
        compose.setContent {
            SyncPlayGroupCard(
                group = SyncPlayGroup("group-1", "Sala da família", "Playing", listOf("Raphael")),
                joinEnabled = false,
                isStale = true,
                onJoin = { joinCount++ },
            )
        }

        compose.onNodeWithText("Lista desatualizada. Atualize antes de entrar.").assertIsDisplayed()
        compose.onNodeWithText("Entrar na sala").assertIsNotEnabled().performClick()
        compose.runOnIdle { assertEquals(0, joinCount) }
    }
}
