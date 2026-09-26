package org.mulletaflix.feature.player

import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.width
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.ui.Modifier
import androidx.compose.ui.test.assertCountEquals
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.hasContentDescription
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onAllNodesWithText
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performTouchInput
import androidx.compose.ui.test.swipeLeft
import androidx.compose.ui.unit.dp
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

/**
 * O aviso de "sem conexão" é lido **uma vez**.
 *
 * Ele era um `Surface` com `contentDescription` **sem** `mergeDescendants`, e o
 * `Text` filho dizia a mesma frase: o nó do contêiner já anunciava a sentença e o
 * `Text` continuava sendo um segundo nó com o mesmo texto, então o usuário ouvia o
 * aviso duas vezes ao deslizar pela tela.
 *
 * O controle de transmissão também perdeu a descrição que o app publicava por cima:
 * ela competia com o rótulo visível e com a descrição localizada que o
 * `MediaRouteButton` do Media3 já traz.
 */
@RunWith(AndroidJUnit4::class)
class PlayerAnnouncementTest {

    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun theOfflineNoticeIsAnnouncedExactlyOnce() {
        composeRule.setContent {
            MaterialTheme { PlayerOfflineNotice(visible = true) }
        }

        composeRule
            .onAllNodesWithText("Sem conexão — tentando reconectar…")
            .assertCountEquals(1)

        // A asserção que discrimina: o contêiner não pode publicar uma **segunda**
        // descrição. Contar só os nós de texto não veria o defeito — o
        // `contentDescription` do `Surface` não cria nó de texto, mas o leitor de
        // tela lê a frase do contêiner e depois a do `Text`, duas vezes.
        composeRule
            .onAllNodes(hasContentDescription("Sem conexão. Tentando reconectar."))
            .assertCountEquals(0)
        composeRule
            .onAllNodes(hasContentDescription("Sem conexão — tentando reconectar…"))
            .assertCountEquals(0)
    }

    @Test
    fun aHiddenOfflineNoticeSaysNothing() {
        composeRule.setContent {
            MaterialTheme { PlayerOfflineNotice(visible = false) }
        }

        composeRule
            .onAllNodesWithText("Sem conexão — tentando reconectar…")
            .assertCountEquals(0)
    }

    @Test
    fun theTopBarRowRemainsScrollableOnNarrowWindows() {
        composeRule.setContent {
            MaterialTheme {
                Box(modifier = Modifier.width(180.dp)) {
                    PlayerTopBarActionsRow(modifier = Modifier.fillMaxWidth()) {
                        Text("Ação inicial")
                        Text("Ação final")
                    }
                }
            }
        }

        composeRule.onNodeWithTag(PLAYER_TOP_BAR_ACTIONS_TEST_TAG).performTouchInput { swipeLeft() }
        composeRule.onNodeWithText("Ação final").assertIsDisplayed()
    }
}
