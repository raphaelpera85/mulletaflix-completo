package org.mulletaflix.feature.itemdetail

import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.mutableStateListOf
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performTextInput
import androidx.compose.ui.test.performTextClearance
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.json.JSONObject
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.readium.r2.shared.publication.Locator

@RunWith(AndroidJUnit4::class)
class BookReaderBookmarkActionsTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun canSaveBookmarkAndRestoreSavedLocation() {
        var saveRequests = 0
        var savedLabel: String? = null
        var restoredBookmark: BookReaderBookmark? = null
        showActions(
            bookmarks = listOf(bookmark()),
            onSaveBookmark = { label ->
                saveRequests++
                savedLabel = label
            },
            onOpenBookmark = { restoredBookmark = it },
        )

        composeRule.onNodeWithContentDescription("Mais opções de leitura").performClick()
        composeRule.onNodeWithText("Salvar posição atual").performClick()
        composeRule.onNodeWithTag(BOOK_READER_BOOKMARK_NAME_TEST_TAG).performTextInput("Minha nota")
        composeRule.onNodeWithText("Salvar").performClick()
        composeRule.onNodeWithContentDescription("Mais opções de leitura").performClick()
        composeRule.onNodeWithText("Marcadores salvos (1)").performClick()
        composeRule.onNodeWithText("Capítulo 2 · 42%").performClick()

        composeRule.runOnIdle {
            assertEquals(1, saveRequests)
            assertEquals("Minha nota", savedLabel)
            assertEquals("bookmark-1", restoredBookmark?.id)
        }
    }

    @Test
    fun blankCustomNameUsesSuggestedPageLabel() {
        var savedLabel: String? = null
        showActions(onSaveBookmark = { label -> savedLabel = label })

        composeRule.onNodeWithContentDescription("Mais opções de leitura").performClick()
        composeRule.onNodeWithText("Salvar posição atual").performClick()
        composeRule.onNodeWithText("Salvar").performClick()

        composeRule.runOnIdle { assertEquals("Página 3", savedLabel) }
    }

    @Test
    fun savedBookmarkCanBeRemoved() {
        var removedId: String? = null
        showActions(
            bookmarks = listOf(bookmark()),
            onDeleteBookmark = { removedId = it },
        )

        composeRule.onNodeWithContentDescription("Mais opções de leitura").performClick()
        composeRule.onNodeWithText("Marcadores salvos (1)").performClick()
        composeRule.onNodeWithContentDescription("Excluir marcador Capítulo 2 · 42%").performClick()

        composeRule.runOnIdle { assertEquals("bookmark-1", removedId) }
    }

    @Test
    fun savedBookmarkCanBeRenamedWithoutChangingItsLocation() {
        val bookmarks = mutableStateListOf(bookmark())
        var renamedId: String? = null
        var renamedLabel: String? = null
        showActions(
            bookmarks = bookmarks,
            onRenameBookmark = { id, label ->
                renamedId = id
                renamedLabel = label
                val index = bookmarks.indexOfFirst { it.id == id }
                if (index >= 0) bookmarks[index] = bookmarks[index].copy(label = label)
            },
        )

        composeRule.onNodeWithContentDescription("Mais opções de leitura").performClick()
        composeRule.onNodeWithText("Marcadores salvos (1)").performClick()
        composeRule.onNodeWithContentDescription("Renomear marcador Capítulo 2 · 42%").performClick()
        val nameField = composeRule.onNodeWithTag(BOOK_READER_BOOKMARK_NAME_TEST_TAG)
        nameField.performTextClearance()
        nameField.performTextInput("Minha nota")
        composeRule.onNodeWithText("Salvar").performClick()

        composeRule.onNodeWithText("Minha nota").assertIsDisplayed()
        composeRule.runOnIdle {
            assertEquals("bookmark-1", renamedId)
            assertEquals("Minha nota", renamedLabel)
            assertEquals("OPS/chapter-2.xhtml", bookmarks.single().locator.href.toString())
        }
    }

    private fun showActions(
        bookmarks: List<BookReaderBookmark> = emptyList(),
        onRestart: () -> Unit = {},
        onSaveBookmark: (String) -> Unit = {},
        onOpenBookmark: (BookReaderBookmark) -> Unit = {},
        onDeleteBookmark: (String) -> Unit = {},
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
                    onOpenBookmark = onOpenBookmark,
                    onDeleteBookmark = onDeleteBookmark,
                    onRenameBookmark = onRenameBookmark,
                )
            }
        }
    }

    private fun bookmark() = BookReaderBookmark(
        id = "bookmark-1",
        label = "Capítulo 2 · 42%",
        locator = requireNotNull(
            Locator.fromJSON(
                JSONObject(
                    """{"href":"OPS/chapter-2.xhtml","type":"application/xhtml+xml","locations":{"progression":0.42}}""",
                ),
            ),
        ),
    )
}
