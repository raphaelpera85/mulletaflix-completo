package org.mulletaflix.android

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.core.splashscreen.SplashScreen.Companion.installSplashScreen
import dagger.hilt.android.AndroidEntryPoint
import org.mulletaflix.android.navigation.MulletaFlixNavHost
import org.mulletaflix.designsystem.theme.MulletaFlixTheme

/**
 * Single-Activity entry point for the MulletaFlix Android app.
 *
 * - Installs the splash screen
 * - Enables edge-to-edge display
 * - Hosts the Compose navigation graph
 */
@AndroidEntryPoint
class MainActivity : ComponentActivity() {

    override fun onCreate(savedInstanceState: Bundle?) {
        installSplashScreen()
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()

        setContent {
            MulletaFlixTheme {
                MulletaFlixNavHost()
            }
        }
    }
}
