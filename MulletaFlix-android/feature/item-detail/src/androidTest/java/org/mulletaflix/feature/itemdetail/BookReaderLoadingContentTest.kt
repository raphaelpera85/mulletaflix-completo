package org.mulletaflix.feature.itemdetail

import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.assertTextEquals
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.semantics.SemanticsProperties
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import org.mulletaflix.designsystem.theme.MulletaFlixTheme

class BookReaderLoadingContentTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun conversionMessageIsVisibleAndAnnouncedAsPoliteLiveUpdate() {
        val message = "O servidor está convertendo este livro para leitura. Aguarde um momento."
        composeRule.setContent {
            MulletaFlixTheme {
                BookReaderLoadingContent(message)
            }
        }

        val messageNode = composeRule.onNodeWithTag(BOOK_READER_LOADING_MESSAGE_TEST_TAG)
        messageNode.assertIsDisplayed().assertTextEquals(message)
        val liveRegion = messageNode.fetchSemanticsNode().config[SemanticsProperties.LiveRegion]
        assertEquals(androidx.compose.ui.semantics.LiveRegionMode.Polite, liveRegion)
    }

    @Test
    fun genericLoadingMessageRemainsVisibleBeforeConversionIsDetected() {
        composeRule.setContent {
            MulletaFlixTheme {
                BookReaderLoadingContent(message = null)
            }
        }

        composeRule.onNodeWithTag(BOOK_READER_LOADING_MESSAGE_TEST_TAG)
            .assertIsDisplayed()
            .assertTextEquals("Carregando livro…")
    }
}
