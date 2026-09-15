package org.mulletaflix.android

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.runtime.getValue
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.setValue
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.ui.Alignment
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.painterResource
import androidx.compose.foundation.layout.size
import androidx.compose.material3.Icon
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.core.splashscreen.SplashScreen.Companion.installSplashScreen
import dagger.hilt.android.AndroidEntryPoint
import org.mulletaflix.android.navigation.MulletaFlixNavHost
import org.mulletaflix.designsystem.theme.MulletaFlixTheme
import org.mulletaflix.designsystem.media.LocalMulletaFlixServerUrl
import org.mulletaflix.designsystem.media.LocalMulletaFlixAccessToken
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.android.network.LanServerRecovery
import org.mulletaflix.feature.player.PlayerPictureInPictureController
import androidx.media3.common.util.UnstableApi
import javax.inject.Inject
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.first

/**
 * Single-Activity entry point for the MulletaFlix Android app.
 *
 * - Installs the splash screen
 * - Enables edge-to-edge display
 * - Hosts the Compose navigation graph
 */
@AndroidEntryPoint
@UnstableApi
class MainActivity : ComponentActivity() {

    @Inject lateinit var sessionRepository: SessionRepository
    @Inject lateinit var lanServerRecovery: LanServerRecovery

    override fun onCreate(savedInstanceState: Bundle?) {
        installSplashScreen()
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        lanServerRecovery.start()

        setContent {
            val serverUrl by sessionRepository.getBaseUrl().collectAsStateWithLifecycle(initialValue = "")
            val accessToken by sessionRepository.getAccessToken().collectAsStateWithLifecycle(initialValue = null)
            var sessionResolved by remember { mutableStateOf(false) }
            var hasValidSession by remember { mutableStateOf(false) }

            LaunchedEffect(Unit) {
                hasValidSession = combine(
                    sessionRepository.getBaseUrl(),
                    sessionRepository.getAccessToken(),
                    sessionRepository.getCurrentUserId(),
                ) { url, token, userId -> hasUsableSession(url, token, userId) }.first()
                sessionResolved = true
            }

            CompositionLocalProvider(
                LocalMulletaFlixServerUrl provides serverUrl,
                LocalMulletaFlixAccessToken provides accessToken,
            ) {
                MulletaFlixTheme {
                    if (!sessionResolved) {
                        Box(
                            modifier = Modifier.fillMaxSize().background(Color.Black),
                            contentAlignment = Alignment.Center,
                        ) {
                            Icon(
                                painter = painterResource(org.mulletaflix.designsystem.R.drawable.ic_mulletaflix_logo),
                                contentDescription = "MulletaFlix",
                                tint = Color.Unspecified,
                                modifier = Modifier.size(88.dp),
                            )
                        }
                    } else {
                        MulletaFlixNavHost(
                            startDestination = if (hasValidSession) {
                                org.mulletaflix.android.navigation.MulletaFlixRoute.HOME
                            } else {
                                org.mulletaflix.android.navigation.MulletaFlixRoute.SERVER_SELECTION
                            },
                        )
                    }
                }
            }
        }
    }

    override fun onStart() {
        super.onStart()
        // The Wi-Fi network may have changed while the activity was paused;
        // refresh the LAN endpoint before the next playback request.
        if (::lanServerRecovery.isInitialized) {
            lanServerRecovery.refresh()
        }
    }

    override fun onUserLeaveHint() {
        super.onUserLeaveHint()
        PlayerPictureInPictureController.dispatchUserLeaveHint()
    }

    override fun onDestroy() {
        lanServerRecovery.stop()
        super.onDestroy()
    }
}
