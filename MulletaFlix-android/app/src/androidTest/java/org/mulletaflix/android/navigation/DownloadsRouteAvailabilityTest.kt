package org.mulletaflix.android.navigation

import androidx.activity.compose.setContent
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.v2.createAndroidComposeRule
import androidx.compose.ui.test.onAllNodesWithText
import androidx.compose.ui.test.onNodeWithText
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.rememberNavController
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.junit.Assert.assertTrue
import org.mulletaflix.android.MainActivity

@RunWith(AndroidJUnit4::class)
class DownloadsRouteAvailabilityTest {
    @get:Rule
    val composeRule = createAndroidComposeRule<MainActivity>()

    @Test
    fun directDownloadsRouteRedirectsToHomeOnTelevision() {
        showDownloadsNavigation(isTelevision = true)

        composeRule.waitUntil(timeoutMillis = 5_000) {
            composeRule.onAllNodesWithText(HOME_CONTENT).fetchSemanticsNodes().isNotEmpty()
        }
        composeRule.onNodeWithText(HOME_CONTENT).assertIsDisplayed()
        assertTrue(composeRule.onAllNodesWithText(DOWNLOADS_CONTENT).fetchSemanticsNodes().isEmpty())
    }

    @Test
    fun directDownloadsRouteRemainsAvailableOnHandheldDevices() {
        showDownloadsNavigation(isTelevision = false)

        composeRule.onNodeWithText(DOWNLOADS_CONTENT).assertIsDisplayed()
        assertTrue(composeRule.onAllNodesWithText(HOME_CONTENT).fetchSemanticsNodes().isEmpty())
    }

    private fun showDownloadsNavigation(isTelevision: Boolean) {
        composeRule.activity.setContent {
            MaterialTheme {
                val navController = rememberNavController()
                NavHost(
                    navController = navController,
                    startDestination = MulletaFlixRoute.DOWNLOADS,
                ) {
                    composable(MulletaFlixRoute.DOWNLOADS) {
                        DownloadsDestination(navController, isTelevision) {
                            Text(DOWNLOADS_CONTENT)
                        }
                    }
                    composable(MulletaFlixRoute.HOME) {
                        Text(HOME_CONTENT)
                    }
                }
            }
        }
    }

    private companion object {
        const val DOWNLOADS_CONTENT = "Downloads destination"
        const val HOME_CONTENT = "Home destination"
    }
}
