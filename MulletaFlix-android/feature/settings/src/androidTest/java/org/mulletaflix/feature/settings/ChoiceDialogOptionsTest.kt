package org.mulletaflix.feature.settings

import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.height
import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.Modifier
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.hasScrollAction
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performScrollTo
import androidx.compose.ui.unit.dp
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

/**
 * Uma lista de opções maior que a janela precisa rolar.
 *
 * O diálogo de ordenação oferece oito linhas e o de idiomas cinco. Numa janela
 * baixa — a da TV — as últimas eram compostas fora dos limites: recortadas,
 * invisíveis, e inalcançáveis até com o controle remoto, porque o recorte corta
 * o toque e o foco junto. O valor continuava guardado e exibido na linha de
 * Ajustes, mas não podia mais ser escolhido.
 *
 * A prova é `hasScrollAction()` no contêiner: sem o modificador de rolagem
 * nenhum nó da árvore tem ação de rolagem, e o teste falha. Só verificar que as
 * linhas existem não provaria nada — elas sempre foram compostas.
 */
@RunWith(AndroidJUnit4::class)
class ChoiceDialogOptionsTest {

    @get:Rule
    val composeRule = createComposeRule()

    /** Toda opção de `options` precisa caber numa janela de [viewportDp]. */
    private fun showOptions(options: List<String>, viewportDp: Int) {
        composeRule.setContent {
            MaterialTheme {
                Box(Modifier.height(viewportDp.dp)) {
                    ChoiceDialogOptions(
                        options = options,
                        selected = options.first(),
                        onSelect = {},
                    )
                }
            }
        }
    }

    @Test
    fun aListTallerThanItsWindowIsScrollable() {
        showOptions(librarySortLabels, viewportDp = 120)

        val scrollable = composeRule.onAllNodes(hasScrollAction()).fetchSemanticsNodes()
        assertTrue(
            "a lista de opções precisa ter rolagem própria; sem ela as últimas linhas " +
                "ficam recortadas e não podem ser escolhidas",
            scrollable.isNotEmpty(),
        )
    }

    @Test
    fun everyOptionCanBeBroughtIntoView() {
        showOptions(librarySortLabels, viewportDp = 120)

        librarySortLabels.forEach { label ->
            composeRule
                .onNodeWithText(label)
                .performScrollTo()
                .assertIsDisplayed()
        }
    }

    @Test
    fun aShortListStillShowsEveryOptionWithoutScrolling() {
        showOptions(librarySortLabels.take(3), viewportDp = 360)

        librarySortLabels.take(3).forEach { label ->
            composeRule.onNodeWithText(label).assertIsDisplayed()
        }
    }
}
