package org.mulletaflix.feature.itemdetail

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.ui.Modifier
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onAllNodesWithContentDescription
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import java.io.File
import java.util.Base64
import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class CbrBookReaderIntegrationTest {
    @get:Rule
    val composeRule = createComposeRule()

    @Test
    fun realRarPagesRenderAndNavigateWithReaderControls() {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val file = File(context.cacheDir, "cbr-reader-${System.nanoTime()}.cbr").apply {
            writeBytes(Base64.getDecoder().decode(CC0_CBR_FIXTURE_BASE64))
        }
        var archive: CbrBookArchive? = null
        try {
            val openedArchive = runBlocking { CbrBookArchive.open(file) }.also { archive = it }
            val currentPage = mutableIntStateOf(0)
            composeRule.setContent {
                MaterialTheme {
                    Column(Modifier.fillMaxSize()) {
                        PagedBookReaderContent(
                            pageBook = openedArchive,
                            currentPage = currentPage.intValue,
                            modifier = Modifier.weight(1f),
                        )
                        ComicBookPageControls(
                            currentPage = currentPage.intValue,
                            pageCount = openedArchive.pageCount,
                            onPageSelected = { currentPage.intValue = it },
                        )
                    }
                }
            }

            composeRule.onNodeWithText("Página 1 de 2").assertIsDisplayed()
            composeRule.waitUntil(10_000) {
                composeRule.onAllNodesWithContentDescription("Página 1 de 2", substring = true)
                    .fetchSemanticsNodes().isNotEmpty()
            }
            composeRule.onNodeWithContentDescription("Próxima página").performClick()
            composeRule.onNodeWithText("Página 2 de 2").assertIsDisplayed()
            composeRule.waitUntil(10_000) {
                composeRule.onAllNodesWithContentDescription("Página 2 de 2", substring = true)
                    .fetchSemanticsNodes().isNotEmpty()
            }
            composeRule.runOnIdle { assertEquals(1, currentPage.intValue) }
        } finally {
            archive?.close()
            file.delete()
        }
    }

    private companion object {
        // Minimal test pages are CC0: https://github.com/ssokolow/rar-test-files
        const val CC0_CBR_FIXTURE_BASE64 =
            "UmFyIRoHAM+QcwAADQAAAAAAAAAMJXQggCwAtgAAANwAAAAAbLFw2gAAISodNQwAIAAAAHRlc3RmaWxlLmpwZ+cYFf7V/ydkeNQh1MKmm6OAexVPlvVHrzqPE5mVHC08ghs85LfafCcldrlGYJPnjkgNzK9t4fZEpePKwmfeq9nqNRVssG8auWw3ppmChio3P4QobbIIu+aDvAnlNhtw7eU/yPyMBuPEssZjwTehsh4DZgC5HXWGFUVxgDrn+6KnVDXP/2B26ds102b/eZa5elob/BycnuXvN92AXobBxHJ4Ebq+7rCITbK7Lz6UAAC/iGf2qf/UUW50IIAsAFQAAABXAAAAAGKssK8AACEqHTUMACAAAAB0ZXN0ZmlsZS5wbmenGIjF+7VC0fPe1feyyXAlT4G/SVtSdAyd7pMHsE3FAkIqbrYBRgyQp7m1pxzv+HOHpfwCaeA8jA41QCxmUCvsyGsqR5gONgQCwAAAAL+IZ/ap/9TEPXsAQAcA"
    }
}
