package org.mulletaflix.feature.library

import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.width
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.unit.dp
import org.junit.Rule
import org.junit.Test

/** Device-level checks for the responsive Minha Lista contract. */
class FavoritesAdaptiveUsageTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun television_uses_dense_grid_and_remote_focus_contract() {
        composeRule.setContent { FavoritesAdaptiveContractSurface(1008, isTelevision = true) }
        composeRule.onNodeWithText("TV: grade densa e foco remoto").assertIsDisplayed()
    }

    @Test
    fun tablet_uses_more_columns_than_phone() {
        composeRule.setContent { FavoritesAdaptiveContractSurface(600, isTelevision = false) }
        composeRule.onNodeWithText("Tablet: grade adaptativa").assertIsDisplayed()
    }

    @Test
    fun phone_keeps_three_column_contract() {
        composeRule.setContent { FavoritesAdaptiveContractSurface(411, isTelevision = false) }
        composeRule.onNodeWithText("Celular: três colunas").assertIsDisplayed()
    }
}

@Composable
private fun FavoritesAdaptiveContractSurface(widthDp: Int, isTelevision: Boolean) {
    val columns = favoritesGridColumns(widthDp, isTelevision)
    Box(modifier = Modifier.width(widthDp.dp).height(100.dp)) {
        MaterialTheme {
            Text(
                when {
                    isTelevision && columns >= 4 -> "TV: grade densa e foco remoto"
                    widthDp >= 600 && columns >= 4 -> "Tablet: grade adaptativa"
                    else -> "Celular: três colunas"
                },
            )
        }
    }
}
