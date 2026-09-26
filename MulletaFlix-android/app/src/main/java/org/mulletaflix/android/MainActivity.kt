package org.mulletaflix.android

import android.os.Bundle
import android.os.Build
import android.app.PictureInPictureUiState
import android.content.Intent
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
import androidx.compose.foundation.layout.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.painterResource
import androidx.compose.material3.Icon
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.compose.LocalLifecycleOwner
import androidx.core.splashscreen.SplashScreen.Companion.installSplashScreen
import dagger.hilt.android.AndroidEntryPoint
import org.mulletaflix.android.navigation.MulletaFlixNavHost
import org.mulletaflix.designsystem.theme.MulletaFlixTheme
import org.mulletaflix.designsystem.media.LocalMulletaFlixServerUrl
import org.mulletaflix.designsystem.media.LocalMulletaFlixAccessToken
import org.mulletaflix.designsystem.media.LocalMulletaFlixServerId
import org.mulletaflix.designsystem.components.ReleaseNotesText
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.android.network.LanServerRecovery
import org.mulletaflix.feature.player.PlayerPictureInPictureController
import androidx.media3.common.util.UnstableApi
import javax.inject.Inject
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.first

import androidx.compose.material3.*
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.CloudDownload
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalConfiguration
import androidx.hilt.navigation.compose.hiltViewModel
import org.mulletaflix.android.update.AppUpdateViewModel
import org.mulletaflix.android.update.AppUpdateCheckEffect
import org.mulletaflix.android.update.appUpdateCheckIntervalMillis
import org.mulletaflix.core.common.update.AppUpdateInstaller
import org.mulletaflix.domain.repository.AppThemeSetting
import org.mulletaflix.domain.repository.SettingsRepository
import org.mulletaflix.feature.settings.toThemeVariant

/**
 * Single-Activity entry point for the MulletaFlix Android app.
 *
 * - Installs the splash screen
 * - Enables edge-to-edge display
 * - Hosts the Compose navigation graph
 * - Verifies in-app updates from GitHub Releases
 */
@AndroidEntryPoint
@UnstableApi
class MainActivity : ComponentActivity() {

    private var incomingDeepLink by mutableStateOf<MediaDeepLinkRequest?>(null)
    private var deepLinkSequence = 0L

    @Inject lateinit var sessionRepository: SessionRepository
    @Inject lateinit var settingsRepository: SettingsRepository
    @Inject lateinit var lanServerRecovery: LanServerRecovery

    override fun onCreate(savedInstanceState: Bundle?) {
        installSplashScreen()
        super.onCreate(savedInstanceState)
        PlayerPictureInPictureController.onPictureInPictureModeChanged(isInPictureInPictureMode)
        incomingDeepLink = mediaDeepLinkRequest(intent, ++deepLinkSequence)
        enableEdgeToEdge()

        setContent {
            val serverUrl by sessionRepository.getBaseUrl().collectAsStateWithLifecycle(initialValue = "")
            val accessToken by sessionRepository.getAccessToken().collectAsStateWithLifecycle(initialValue = null)
            val serverId by sessionRepository.getServerId().collectAsStateWithLifecycle(initialValue = null)
            var sessionResolved by remember { mutableStateOf(false) }
            var hasValidSession by remember { mutableStateOf(false) }

            // O aviso de atualização — estado **e** download — vive no ViewModel, e não
            // em `remember`: ver `AppUpdateViewModel` para o defeito que isso corrigia
            // (uma recriação da Activity matava o download em silêncio).
            val appUpdateViewModel: AppUpdateViewModel = hiltViewModel()
            val updateState by appUpdateViewModel.state.collectAsStateWithLifecycle()
            val context = LocalContext.current
            val lifecycleOwner = LocalLifecycleOwner.current
            val isTelevision = LocalConfiguration.current.uiMode and
                android.content.res.Configuration.UI_MODE_TYPE_MASK ==
                android.content.res.Configuration.UI_MODE_TYPE_TELEVISION

            LaunchedEffect(Unit) {
                hasValidSession = combine(
                    sessionRepository.getBaseUrl(),
                    sessionRepository.getAccessToken(),
                    sessionRepository.getCurrentUserId(),
                ) { url, token, userId -> hasUsableSession(url, token, userId) }.first()
                sessionResolved = true
            }

            if (sessionResolved) {
                AppUpdateCheckEffect(
                    lifecycleOwner = lifecycleOwner,
                    intervalMillis = appUpdateCheckIntervalMillis(isTelevision),
                    checkForUpdate = { appUpdateViewModel.checkForUpdate(BuildConfig.VERSION_NAME) },
                )
            }

            CompositionLocalProvider(
                LocalMulletaFlixServerUrl provides serverUrl,
                LocalMulletaFlixAccessToken provides accessToken,
                LocalMulletaFlixServerId provides serverId,
            ) {
                // The theme is read here, at the root, so the choice made in
                // Settings actually paints the app. It used to be saved and
                // displayed but never applied, because this call omitted the
                // variant and therefore always used the Dark default.
                val theme by settingsRepository.getTheme()
                    .collectAsStateWithLifecycle(initialValue = AppThemeSetting.Dark)
                MulletaFlixTheme(variant = theme.toThemeVariant()) {
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
                                incomingDeepLink?.detailRoute
                                    ?: org.mulletaflix.android.navigation.MulletaFlixRoute.HOME
                            } else {
                                org.mulletaflix.android.navigation.MulletaFlixRoute.SERVER_SELECTION
                            },
                            deepLinkRequest = incomingDeepLink,
                            // The request is cleared once its destination is on
                            // screen: otherwise a later logout and login would
                            // reopen a detail page the user had already left.
                            onDeepLinkConsumed = { incomingDeepLink = null },
                        )

                        if (updateState.isDialogVisible && updateState.available != null) {
                            val update = updateState.available!!
                            val isDownloadingUpdate = updateState.isDownloading
                            AlertDialog(
                                onDismissRequest = {
                                    if (!isDownloadingUpdate) {
                                        appUpdateViewModel.dismissDialog()
                                    }
                                },
                                icon = {
                                    Icon(
                                        Icons.Default.CloudDownload,
                                        contentDescription = null,
                                        tint = MaterialTheme.colorScheme.secondary,
                                    )
                                },
                                title = { Text("Nova Versão Disponível: v${update.latestVersion}") },
                                text = {
                                    Column(
                                        modifier = Modifier
                                            .fillMaxWidth()
                                            .padding(vertical = 4.dp),
                                    ) {
                                        Text(
                                            text = "Uma nova versão do MulletaFlix Android está disponível!",
                                            style = MaterialTheme.typography.bodyMedium,
                                        )
                                        if (update.apkSize > 0) {
                                            Text(
                                                text = "Tamanho: ${String.format(java.util.Locale.US, "%.1f", update.apkSize / (1024f * 1024f))} MB",
                                                style = MaterialTheme.typography.bodySmall,
                                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                                                modifier = Modifier.padding(top = 4.dp),
                                            )
                                        }
                                        val notes = update.releaseNotes
                                        if (!notes.isNullOrBlank()) {
                                            Spacer(modifier = Modifier.height(8.dp))
                                            Text(
                                                text = "Novidades:",
                                                style = MaterialTheme.typography.titleSmall,
                                            )
                                            ReleaseNotesText(
                                                markdown = notes,
                                                modifier = Modifier.padding(top = 4.dp),
                                            )
                                        }
                                        if (isDownloadingUpdate) {
                                            Spacer(modifier = Modifier.height(16.dp))
                                            LinearProgressIndicator(
                                                progress = { updateState.progress },
                                                modifier = Modifier.fillMaxWidth(),
                                            )
                                            Text(
                                                text = "Baixando: ${(updateState.progress * 100).toInt()}%",
                                                style = MaterialTheme.typography.bodySmall,
                                                modifier = Modifier.padding(top = 4.dp),
                                            )
                                        }
                                        updateState.error?.let { error ->
                                            Spacer(modifier = Modifier.height(12.dp))
                                            Text(
                                                text = error,
                                                color = MaterialTheme.colorScheme.error,
                                                style = MaterialTheme.typography.bodySmall,
                                            )
                                        }
                                    }
                                },
                                confirmButton = {
                                    Button(
                                        onClick = {
                                            // O download roda em `viewModelScope`: uma
                                            // recriação da Activity não o interrompe mais.
                                            appUpdateViewModel.downloadUpdate { file ->
                                                AppUpdateInstaller.installApk(context, file)
                                            }
                                        },
                                        enabled = !isDownloadingUpdate && !update.apkDownloadUrl.isNullOrBlank(),
                                    ) {
                                        Text(
                                            when {
                                                isDownloadingUpdate -> "Baixando..."
                                                updateState.error != null -> "Tentar novamente"
                                                else -> "Atualizar Agora"
                                            },
                                        )
                                    }
                                },
                                dismissButton = {
                                    if (!isDownloadingUpdate) {
                                        TextButton(onClick = { appUpdateViewModel.dismissDialog() }) {
                                            Text("Depois")
                                        }
                                    }
                                },
                            )
                        }
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
            lanServerRecovery.start()
            lanServerRecovery.refresh()
        }
    }

    override fun onStop() {
        // LAN discovery is useful only while the player UI is visible. Stop
        // callbacks and scans in the background to avoid unnecessary network
        // work and Wi-Fi wakeups while another app is in the foreground.
        if (::lanServerRecovery.isInitialized) {
            lanServerRecovery.stop()
        }
        super.onStop()
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        setIntent(intent)
        // Every delivered intent gets a fresh sequence, so tapping the same
        // link again is a new request instead of a silently ignored one.
        mediaDeepLinkRequest(intent, ++deepLinkSequence)?.let { request ->
            incomingDeepLink = request
        }
    }

    override fun onUserLeaveHint() {
        super.onUserLeaveHint()
        PlayerPictureInPictureController.dispatchUserLeaveHint()
    }

    override fun onPictureInPictureModeChanged(
        isInPictureInPictureMode: Boolean,
        newConfig: android.content.res.Configuration,
    ) {
        super.onPictureInPictureModeChanged(isInPictureInPictureMode, newConfig)
        PlayerPictureInPictureController.onPictureInPictureModeChanged(isInPictureInPictureMode)
    }

    override fun onPictureInPictureUiStateChanged(pipState: PictureInPictureUiState) {
        super.onPictureInPictureUiStateChanged(pipState)
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.VANILLA_ICE_CREAM && pipState.isTransitioningToPip) {
            PlayerPictureInPictureController.onPictureInPictureModeChanged(true)
        }
    }

    override fun onDestroy() {
        lanServerRecovery.stop()
        super.onDestroy()
    }
}
