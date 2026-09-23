package org.mulletaflix.designsystem.components

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.selection.selectable
import androidx.compose.material3.RadioButton
import androidx.compose.material3.Text
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.test.isFocusable
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.requestFocus
import androidx.compose.ui.unit.dp
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.designsystem.theme.MulletaFlixTheme

/**
 * How many focus targets one radio option carries — the count behind the reported
 * defect "usando na tv tenho que clicar 2x para entrar em qualquer biblioteca ou
 * midia".
 *
 * The settings dialogs build an option as a `clickable` row containing a
 * `RadioButton` that has its own `onClick`. The player's own menus instead put
 * `selectable(role = Role.RadioButton)` on the row and pass `onClick = null` to the
 * button. Both look identical; if the `RadioButton` contributes a second focus target
 * the remote has to travel onto it before the option can be activated, which is the
 * two-press report. This measures the shapes against each other so the settings
 * dialogs can be brought onto whichever one is honest.
 */
@RunWith(AndroidJUnit4::class)
class RadioOptionFocusTargetTest {

    @get:Rule
    val composeRule = createComposeRule()

    private fun focusTargetCount(): Int =
        composeRule.onAllNodes(isFocusable()).fetchSemanticsNodes().size
    @Test
    fun aClickableRowWithItsOwnRadioClickIsTheShapeThatLosesAFocusTarget() {
        // Pinned rather than merely observed: this is the shape the settings dialogs
        // used, and the count is why they needed a second press.
        composeRule.setContent {
            MulletaFlixTheme {
                Row(
                    verticalAlignment = Alignment.CenterVertically,
                    modifier = Modifier
                        .fillMaxWidth()
                        .clickable { }
                        .padding(vertical = 8.dp),
                ) {
                    RadioButton(selected = false, onClick = { })
                    Text("Opção")
                }
            }
        }

        assertEquals(
            "a `clickable` row holding an active `RadioButton` carries two focus " +
                "targets; a test that asserts 1 here documents the defect, not the fix",
            2,
            focusTargetCount(),
        )
    }

    @Test
    fun thePlayerMenuShapeCarriesASingleFocusTarget() {
        composeRule.setContent {
            MulletaFlixTheme {
                Row(
                    verticalAlignment = Alignment.CenterVertically,
                    modifier = Modifier
                        .fillMaxWidth()
                        .selectable(selected = false, role = Role.RadioButton, onClick = { })
                        .padding(vertical = 8.dp),
                ) {
                    RadioButton(selected = false, onClick = null)
                    Text("Opção")
                }
            }
        }

        assertEquals(
            "the shape the app's dialogs must use: one press reaches the option",
            1,
            focusTargetCount(),
        )
    }

    @Test
    fun aSingleRowPressSelectsTheOptionOnce() {
        var selections = 0
        composeRule.setContent {
            MulletaFlixTheme {
                Column {
                    Row(
                        verticalAlignment = Alignment.CenterVertically,
                        modifier = Modifier
                            .fillMaxWidth()
                            .testTag("option")
                            .selectable(selected = false, role = Role.RadioButton, onClick = { selections++ })
                            .padding(vertical = 8.dp),
                    ) {
                        RadioButton(selected = false, onClick = null)
                        Text("Opção")
                    }
                }
            }
        }

        composeRule.onNodeWithTag("option").requestFocus()
        composeRule.onNodeWithTag("option").performClick()
        composeRule.waitForIdle()

        assertEquals("one press must select once", 1, selections)
    }
}
