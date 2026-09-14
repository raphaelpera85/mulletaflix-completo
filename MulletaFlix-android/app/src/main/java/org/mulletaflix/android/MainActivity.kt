package org.mulletaflix.android

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.CompositionLocalProvider
import androidx.core.splashscreen.SplashScreen.Companion.installSplashScreen
import dagger.hilt.android.AndroidEntryPoint
import org.mulletaflix.android.navigation.MulletaFlixNavHost
import org.mulletaflix.designsystem.theme.MulletaFlixTheme
import org.mulletaflix.designsystem.media.LocalMulletaFlixServerUrl
import org.mulletaflix.designsystem.media.LocalMulletaFlixAccessToken
import org.mulletaflix.core.api.SessionRepository
import javax.inject.Inject

/**
 * Single-Activity entry point for the MulletaFlix Android app.
 *
 * - Installs the splash screen
 * - Enables edge-to-edge display
 * - Hosts the Compose navigation graph
 */
@AndroidEntryPoint
class MainActivity : ComponentActivity() {

    @Inject lateinit var sessionRepository: SessionRepository

    override fun onCreate(savedInstanceState: Bundle?) {
        installSplashScreen()
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()

        setContent {
            val serverUrl by sessionRepository.getBaseUrl().collectAsState(initial = "")
            val accessToken by sessionRepository.getAccessToken().collectAsState(initial = null)
            CompositionLocalProvider(
                LocalMulletaFlixServerUrl provides serverUrl,
                LocalMulletaFlixAccessToken provides accessToken,
            ) {
                MulletaFlixTheme {
                    MulletaFlixNavHost()
                }
            }
        }
    }
}
