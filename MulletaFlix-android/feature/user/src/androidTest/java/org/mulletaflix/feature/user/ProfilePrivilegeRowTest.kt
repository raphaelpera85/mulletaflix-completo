package org.mulletaflix.feature.user

import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.semantics.SemanticsProperties
import androidx.compose.ui.test.SemanticsMatcher
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

/**
 * A permissão da conta precisa existir em texto, não só em cor.
 *
 * A linha "Transmissão 4K HDR / Dolby Vision" mostra um visto verde quando a conta
 * pode e um X vermelho quando não pode — e nada mais. Para quem usa leitor de tela
 * a informação simplesmente não existia: o serviço anunciava o nome do recurso e o
 * usuário não tinha como saber se a permissão estava concedida ou negada.
 */
@RunWith(AndroidJUnit4::class)
class ProfilePrivilegeRowTest {

    @get:Rule
    val composeRule = createComposeRule()

    private fun stateNode(value: String) = composeRule.onNode(
        SemanticsMatcher.expectValue(SemanticsProperties.StateDescription, value),
    )

    private fun show(enabled: Boolean) {
        composeRule.setContent {
            MaterialTheme {
                ProfilePrivilegeRow(title = "Transmissão 4K HDR / Dolby Vision", enabled = enabled)
            }
        }
    }

    @Test
    fun aGrantedPrivilegeIsAnnouncedAsGranted() {
        show(enabled = true)

        stateNode("Concedido").assertExists()
    }

    @Test
    fun aDeniedPrivilegeIsAnnouncedAsDenied() {
        show(enabled = false)

        stateNode("Negado").assertExists()
    }

    @Test
    fun theTwoStatesCannotBeConfused() {
        // A cor é a única diferença visual. Se os dois estados anunciassem a mesma
        // coisa, os testes acima passariam por acidente em um dos casos.
        var enabled by mutableStateOf(true)
        composeRule.setContent {
            MaterialTheme {
                ProfilePrivilegeRow(title = "Transmissão 4K HDR / Dolby Vision", enabled = enabled)
            }
        }

        stateNode("Concedido").assertExists()
        stateNode("Negado").assertDoesNotExist()

        enabled = false
        composeRule.waitForIdle()

        stateNode("Negado").assertExists()
        stateNode("Concedido").assertDoesNotExist()
    }
}
