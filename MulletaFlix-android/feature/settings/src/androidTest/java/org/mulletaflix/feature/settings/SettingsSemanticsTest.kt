package org.mulletaflix.feature.settings

import androidx.compose.foundation.layout.Column
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.SemanticsProperties
import androidx.compose.ui.test.SemanticsMatcher
import androidx.compose.ui.test.assertCountEquals
import androidx.compose.ui.test.hasClickAction
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.onAllNodesWithText
import androidx.compose.ui.test.performClick
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.designsystem.theme.MulletaFlixThemeVariant

/**
 * Duas coisas que o leitor de tela precisa para navegar em Ajustes:
 *
 *  - uma **linha de navegação** tem que se declarar como algo que se ativa. Sem
 *    papel, ela era anunciada como um bloco de texto e o usuário não descobria que
 *    dava para tocar;
 *  - um **grupo de opções mutuamente exclusivas** tem que se declarar como grupo.
 *    As linhas usavam `selectable(role = Role.RadioButton)` uma a uma, mas o
 *    contêiner não dizia que elas formam um conjunto — o leitor de tela anunciava
 *    cada opção como um botão solto.
 */
@RunWith(AndroidJUnit4::class)
class SettingsSemanticsTest {

    @get:Rule
    val composeRule = createComposeRule()

    private fun roles(): Int = composeRule
        .onAllNodes(SemanticsMatcher.expectValue(SemanticsProperties.Role, Role.Button))
        .fetchSemanticsNodes()
        .size

    @Test
    fun aSettingsRowIsAnnouncedAsSomethingThatCanBeActivated() {
        var clicks = 0
        composeRule.setContent {
            MaterialTheme {
                SettingsItem(
                    icon = Icons.Default.Settings,
                    title = "Qualidade padrão",
                    subtitle = "Automático",
                    onClick = { clicks++ },
                )
            }
        }

        composeRule.onNodeWithText("Qualidade padrão").performClick()
        composeRule.waitForIdle()

        assertEquals(1, clicks)
        assertEquals("a linha precisa ser um botão, não um bloco de texto", 1, roles())
    }

    @Test
    fun theSubtitleLanguageDialogDeclaresOneExclusiveGroup() {
        composeRule.setContent {
            MaterialTheme {
                SubtitleLanguageDialog(
                    current = subtitleLanguageLabels.first(),
                    onSelect = {},
                    onDismiss = {},
                )
            }
        }

        composeRule.onNode(SemanticsMatcher.keyIsDefined(SemanticsProperties.SelectableGroup)).assertExists()
    }

    @Test
    fun theThemeDialogDeclaresOneExclusiveGroup() {
        composeRule.setContent {
            MaterialTheme {
                ThemePickerDialog(
                    current = MulletaFlixThemeVariant.Dark,
                    onSelect = {},
                    onDismiss = {},
                )
            }
        }

        composeRule.onNode(SemanticsMatcher.keyIsDefined(SemanticsProperties.SelectableGroup)).assertExists()
    }

    @Test
    fun theAssertionOnlyMatchesAGroupThatDeclaresItself() {
        // Controle negativo: a asserção dos dois testes acima só vale se um nó
        // **sem** `selectableGroup` não for encontrado por ela.
        composeRule.setContent {
            MaterialTheme { Column { Text("sem grupo") } }
        }

        composeRule.onAllNodes(SemanticsMatcher.keyIsDefined(SemanticsProperties.SelectableGroup)).assertCountEquals(0)
        composeRule.onAllNodes(hasClickAction()).assertCountEquals(0)
    }

    @Test
    fun downloadsSettingsAreHiddenOnTelevision() {
        composeRule.setContent {
            MaterialTheme {
                DownloadsSettingsGroup(
                    downloadPath = "/downloads",
                    storageLabel = "2 GB livres",
                    isTelevision = true,
                )
            }
        }

        composeRule.onAllNodesWithText("Downloads").assertCountEquals(0)
        composeRule.onAllNodesWithText("Pasta de Downloads").assertCountEquals(0)
        composeRule.onAllNodesWithText("Espaço livre para downloads").assertCountEquals(0)
    }

    @Test
    fun downloadsSettingsRemainOnHandheldDevices() {
        composeRule.setContent {
            MaterialTheme {
                DownloadsSettingsGroup(
                    downloadPath = "/downloads",
                    storageLabel = "2 GB livres",
                    isTelevision = false,
                )
            }
        }

        composeRule.onAllNodesWithText("Downloads").assertCountEquals(1)
        composeRule.onAllNodesWithText("Pasta de Downloads").assertCountEquals(1)
        composeRule.onAllNodesWithText("Espaço livre para downloads").assertCountEquals(1)
    }

    @Test
    fun seriesTrackPreferenceResetIsExplainedAndAccessible() {
        var clicks = 0
        composeRule.setContent {
            MaterialTheme {
                SeriesTrackPreferencesGroup(
                    statusMessage = "Preferências específicas removidas. Padrões da conta mantidos.",
                    onClear = { clicks++ },
                )
            }
        }

        composeRule.onNodeWithText("Em episódios, escolhas de áudio e legenda valem para a série. Sem escolha específica, o app usa o padrão da conta.")
            .assertExists()
        composeRule.onNodeWithText("Restaurar padrões das séries").performClick()
        composeRule.onNodeWithText("Preferências específicas removidas. Padrões da conta mantidos.").assertExists()

        assertEquals(1, clicks)
        assertEquals("a ação de redefinição precisa ser acessível como botão", 1, roles())
    }
}
