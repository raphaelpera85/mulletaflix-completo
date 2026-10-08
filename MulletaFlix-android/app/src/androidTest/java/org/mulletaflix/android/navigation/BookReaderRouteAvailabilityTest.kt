package org.mulletaflix.android.navigation

import android.content.res.Configuration
import androidx.activity.compose.setContent
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.test.assertDoesNotExist
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.v2.createAndroidComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import org.mulletaflix.android.MainActivity
import java.util.concurrent.atomic.AtomicReference

class BookReaderRouteAvailabilityTest {
    @get:Rule
    val composeRule = createAndroidComposeRule<MainActivity>()

    @Test
    fun directBookReaderRouteIsBlockedOnTelevisionAndAvailableOnHandheld() {
        val expectedProfile = requireNotNull(
            InstrumentationRegistry.getArguments().getString("expectedDeviceProfile"),
        ) { "Set expectedDeviceProfile to PHONE, TABLET or TV for this AVD run" }
        val actualProfile = AtomicReference<String?>()

        composeRule.activity.setContent {
            val isTelevision =
                (LocalConfiguration.current.uiMode and Configuration.UI_MODE_TYPE_MASK) ==
                    Configuration.UI_MODE_TYPE_TELEVISION
            actualProfile.set(if (isTelevision) "TV" else "PHONE")
            MaterialTheme {
                BookReaderDestination(
                    isTelevision = isTelevision,
                    onBack = {},
                ) {
                    Text("Book reader destination")
                }
            }
        }

        composeRule.waitForIdle()
        assertEquals("The route test must run against the requested device profile", expectedProfile, actualProfile.get())
        if (expectedProfile == "TV") {
            composeRule.onNodeWithText("Leitura de livros indisponível na TV").assertIsDisplayed()
            composeRule.onNodeWithText("Book reader destination").assertDoesNotExist()
            composeRule.onNodeWithText("Voltar").assertIsDisplayed()
        } else {
            composeRule.onNodeWithText("Book reader destination").assertIsDisplayed()
            composeRule.onNodeWithText("Leitura de livros indisponível na TV").assertDoesNotExist()
        }
    }
}
