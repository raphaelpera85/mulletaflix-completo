package org.mulletaflix.feature.library

import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.test.assertIsNotSelected
import androidx.compose.ui.test.assertIsSelected
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

/**
 * O menu de ordenação diz qual opção está escolhida.
 *
 * A única marca do campo ativo era um `Icon(Check)` com `contentDescription = null`,
 * e o `DropdownMenuItem` do material3 1.4.0 não tem parâmetro `selected` — então o
 * menu publicava três nós sem estado nenhum: o usuário ouvia os nomes das opções e
 * nunca descobria qual delas estava aplicada, nem em que direção.
 */
@RunWith(AndroidJUnit4::class)
class SortDropdownSemanticsTest {

    @get:Rule
    val composeRule = createComposeRule()

    private fun show(current: SortOption, currentOrder: SortOrder) {
        composeRule.setContent {
            MaterialTheme {
                SortDropdown(
                    current = current,
                    currentOrder = currentOrder,
                    onApply = { _, _ -> },
                    onDismiss = {},
                )
            }
        }
    }

    @Test
    fun theActiveSortFieldIsSelected() {
        show(current = SortOption.Name, currentOrder = SortOrder.Ascending)

        composeRule.onNodeWithText(SortOption.Name.label).assertIsSelected()
    }

    @Test
    fun onlyTheActiveSortFieldIsSelected() {
        show(current = SortOption.Name, currentOrder = SortOrder.Ascending)

        SortOption.values()
            .filter { it != SortOption.Name }
            .forEach { other ->
                composeRule.onNodeWithText(other.label).assertIsNotSelected()
            }
    }

    @Test
    fun theActiveDirectionIsSelected() {
        show(current = SortOption.Name, currentOrder = SortOrder.Descending)

        composeRule.onNodeWithText(SortOrder.Descending.label).assertIsSelected()
        composeRule.onNodeWithText(SortOrder.Ascending.label).assertIsNotSelected()
    }
}
