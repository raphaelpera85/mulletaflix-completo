package org.mulletaflix.feature.player

import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.width
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performTouchInput
import androidx.compose.ui.test.swipeLeft
import androidx.compose.ui.unit.dp
import org.junit.Rule
import org.junit.Test

/**
 * O controle de transmissão e a fileira de ações do player.
 *
 * O teste anterior (`castActionExposesAUnifiedDescription`) **reconstruía a
 * semântica dentro do próprio teste** — ele montava um `Row` com a mesma
 * `contentDescription` que o app publicava e verificava que ela existia. Não
 * tocava em código de produção: passaria com o defeito no lugar. A descrição
 * duplicada saiu do app (ver `PlayerAnnouncementTest` e `castActionLabel`), então
 * o que resta aqui é a rolagem da fileira.
 */
class PlayerCastAccessibilityTest {

    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun topBarActionsCanBeScrolledHorizontallyOnNarrowWindows() {
        composeRule.setContent {
            MaterialTheme {
                Box(modifier = androidx.compose.ui.Modifier.width(180.dp)) {
                    PlayerTopBarActionsRow(modifier = androidx.compose.ui.Modifier.fillMaxWidth()) {
                        Text("Ação inicial")
                        Text("Ação final")
                    }
                }
            }
        }

        composeRule.onNodeWithTag(PLAYER_TOP_BAR_ACTIONS_TEST_TAG).performTouchInput { swipeLeft() }
        composeRule.onNodeWithText("Ação final").assertIsDisplayed()
    }

    /**
     * O alvo de toque do controle de transmissão foi para
     * `PlayerCastControlTouchTargetTest`, que precisa de uma regra `AndroidComposeRule`:
     * com a variante `v2` o serviço de media router recusa a composição.
     */
}
