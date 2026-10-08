package org.mulletaflix.android.navigation

import android.content.res.Configuration
import androidx.activity.compose.setContent
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.v2.createAndroidComposeRule
import androidx.compose.ui.test.onAllNodesWithText
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.rememberNavController
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
            actualProfile.set(
                when {
                    isTelevision -> "TV"
                    LocalConfiguration.current.screenWidthDp >= 600 -> "TABLET"
                    else -> "PHONE"
                },
            )
            MaterialTheme {
                val navController = rememberNavController()
                NavHost(navController = navController, startDestination = PREVIOUS_ROUTE) {
                    composable(PREVIOUS_ROUTE) {
                        Text("Previous screen")
                        Button(onClick = { navController.navigate(MulletaFlixRoute.bookReader("test-book")) }) {
                            Text("Open book")
                        }
                    }
                    bookReaderRoute(
                        navController = navController,
                        isTelevision = isTelevision,
                    ) { itemId, _ ->
                        Text("Book reader destination: $itemId")
                    }
                }
            }
        }

        composeRule.waitForIdle()
        assertEquals("The route test must run against the requested device profile", expectedProfile, actualProfile.get())
        composeRule.onNodeWithText("Open book").assertIsDisplayed().performClick()
        composeRule.waitForIdle()
        if (expectedProfile == "TV") {
            composeRule.onNodeWithText("Leitura de livros indisponível na TV").assertIsDisplayed()
            assertEquals(0, composeRule.onAllNodesWithText("Book reader destination: test-book").fetchSemanticsNodes().size)
            composeRule.onNodeWithText("Voltar").assertIsDisplayed().performClick()
            composeRule.onNodeWithText("Previous screen").assertIsDisplayed()
        } else {
            composeRule.onNodeWithText("Book reader destination: test-book").assertIsDisplayed()
            assertEquals(
                0,
                composeRule.onAllNodesWithText("Leitura de livros indisponível na TV").fetchSemanticsNodes().size,
            )
        }
    }

    private companion object {
        const val PREVIOUS_ROUTE = "previous"
    }
}
