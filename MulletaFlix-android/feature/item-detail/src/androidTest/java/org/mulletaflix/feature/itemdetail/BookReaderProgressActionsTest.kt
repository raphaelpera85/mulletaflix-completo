package org.mulletaflix.feature.itemdetail

import android.content.res.Configuration
import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.assertIsFocused
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performKeyInput
import androidx.compose.ui.test.performTextInput
import androidx.compose.ui.test.performTextClearance
import androidx.compose.ui.test.pressKey
import androidx.compose.ui.test.requestFocus
import androidx.compose.ui.input.key.Key
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Assume.assumeTrue
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class BookReaderProgressActionsTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun restartRequiresConfirmationAndCanBeCancelled() {
        var restartRequests = 0
        showActions(onRestart = { restartRequests++ })

        composeRule.onNodeWithContentDescription("Mais opções de leitura").performClick()
        composeRule.onNodeWithText("Reiniciar do começo").performClick()
        composeRule.onNodeWithText("Reiniciar leitura?").assertIsDisplayed()
        composeRule.onNodeWithText("Cancelar").performClick()

        assertEquals(0, restartRequests)
    }

    @Test
    fun tvRemoteCanConfirmRestartFromTheMenu() {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val isTelevision = (context.resources.configuration.uiMode and Configuration.UI_MODE_TYPE_MASK) ==
            Configuration.UI_MODE_TYPE_TELEVISION
        assumeTrue("D-pad restart behavior is specific to Android TV", isTelevision)

        var restartRequests = 0
        showActions(onRestart = { restartRequests++ })

        val menuButton = composeRule.onNodeWithContentDescription("Mais opções de leitura")
        menuButton.requestFocus().assertIsFocused()
        menuButton.performKeyInput { pressKey(Key.DirectionCenter) }

        val restartOption = composeRule.onNodeWithText("Reiniciar do começo")
        restartOption.assertIsDisplayed().requestFocus().assertIsFocused()
        restartOption.performKeyInput { pressKey(Key.DirectionCenter) }

        composeRule.onNodeWithText("Reiniciar leitura?").assertIsDisplayed()
        val confirmButton = composeRule.onNodeWithText("Reiniciar")
        confirmButton.requestFocus().assertIsFocused()
        confirmButton.performKeyInput { pressKey(Key.DirectionCenter) }
        composeRule.runOnIdle { assertEquals(1, restartRequests) }
    }

    @Test
    fun tvRemoteCanSaveBookmarkAndOpenBookmarkList() {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val isTelevision = (context.resources.configuration.uiMode and Configuration.UI_MODE_TYPE_MASK) ==
            Configuration.UI_MODE_TYPE_TELEVISION
        assumeTrue("Bookmark D-pad behavior is specific to Android TV", isTelevision)

        var saveRequests = 0
        showActions(onSaveBookmark = { saveRequests++ })

        val menuButton = composeRule.onNodeWithContentDescription("Mais opções de leitura")
        menuButton.requestFocus().assertIsFocused()
        menuButton.performKeyInput { pressKey(Key.DirectionCenter) }
        val saveItem = composeRule.onNodeWithText("Salvar posição atual")
        saveItem.assertIsDisplayed().requestFocus().assertIsFocused()
        saveItem.performKeyInput { pressKey(Key.DirectionCenter) }
        val nameField = composeRule.onNodeWithTag(BOOK_READER_BOOKMARK_NAME_TEST_TAG)
        nameField.assertIsDisplayed().requestFocus().assertIsFocused()
        nameField.performTextInput(" — Minha parte favorita")
        val saveName = composeRule.onNodeWithText("Salvar")
        saveName.requestFocus().assertIsFocused()
        saveName.performKeyInput { pressKey(Key.DirectionCenter) }
        val listButton = composeRule.onNodeWithContentDescription("Mais opções de leitura")
        listButton.requestFocus().assertIsFocused()
        listButton.performKeyInput { pressKey(Key.DirectionCenter) }
        val listItem = composeRule.onNodeWithText("Marcadores salvos (0)")
        listItem.assertIsDisplayed().requestFocus().assertIsFocused()
        listItem.performKeyInput { pressKey(Key.DirectionCenter) }

        composeRule.onNodeWithText("Nenhum marcador salvo neste livro.").assertIsDisplayed()
        composeRule.runOnIdle { assertEquals(1, saveRequests) }
    }

    @Test
    fun tvRemoteCanRenameSavedBookmark() {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val isTelevision = (context.resources.configuration.uiMode and Configuration.UI_MODE_TYPE_MASK) ==
            Configuration.UI_MODE_TYPE_TELEVISION
        assumeTrue("Bookmark D-pad behavior is specific to Android TV", isTelevision)

        var renamed: Pair<String, String>? = null
        showActions(
            bookmarks = listOf(bookmark()),
            onRenameBookmark = { id, label -> renamed = id to label },
        )

        composeRule.onNodeWithContentDescription("Mais opções de leitura")
            .requestFocus().performKeyInput { pressKey(Key.DirectionCenter) }
        val listOption = composeRule.onNodeWithText("Marcadores salvos (1)")
        listOption.assertIsDisplayed().requestFocus().performKeyInput { pressKey(Key.DirectionCenter) }
        val renameButton = composeRule.onNodeWithContentDescription("Renomear marcador Capítulo 2 · 42%")
        renameButton.assertIsDisplayed().requestFocus().performKeyInput { pressKey(Key.DirectionCenter) }
        val nameField = composeRule.onNodeWithTag(BOOK_READER_BOOKMARK_NAME_TEST_TAG)
        nameField.assertIsDisplayed().requestFocus()
        nameField.performTextClearance()
        nameField.performTextInput("Revisar depois")
        val saveButton = composeRule.onNodeWithText("Salvar")
        saveButton.requestFocus().performKeyInput { pressKey(Key.DirectionCenter) }

        composeRule.onNodeWithText("Capítulo 2 · 42%").assertIsDisplayed()
        composeRule.runOnIdle { assertEquals("bookmark-1" to "Revisar depois", renamed) }
    }

    private fun showActions(
        bookmarks: List<BookReaderBookmark> = emptyList(),
        onRestart: () -> Unit = {},
        onSaveBookmark: (String) -> Unit = {},
        onRenameBookmark: (String, String) -> Unit = { _, _ -> },
    ) {
        composeRule.setContent {
            MaterialTheme {
                BookReaderProgressActions(
                    enabled = true,
                    bookmarks = bookmarks,
                    bookmarkLabelSuggestion = "Página 3",
                    onRestart = onRestart,
                    onSaveBookmark = onSaveBookmark,
                    onOpenBookmark = {},
                    onDeleteBookmark = {},
                    onRenameBookmark = onRenameBookmark,
                )
            }
        }
    }

    private fun bookmark() = BookReaderBookmark(
        id = "bookmark-1",
        label = "Capítulo 2 · 42%",
        locator = org.readium.r2.shared.publication.Locator.fromJSON(
            org.json.JSONObject(
                """{"href":"OPS/chapter-2.xhtml","type":"application/xhtml+xml","locations":{"progression":0.42}}""",
            ),
        )!!,
    )
}
