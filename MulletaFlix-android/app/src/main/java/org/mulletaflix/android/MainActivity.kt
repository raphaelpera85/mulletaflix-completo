package org.mulletaflix.android

import android.os.Bundle
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
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.repeatOnLifecycle
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
import kotlinx.coroutines.CancellationException

import androidx.compose.material3.*
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.CloudDownload
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.ui.platform.LocalContext
import kotlinx.coroutines.launch
import org.mulletaflix.core.common.update.AppUpdateDownloader
import org.mulletaflix.core.common.update.AppUpdateInstaller
import org.mulletaflix.core.common.update.DownloadState
import org.mulletaflix.domain.model.AppUpdateInfo
import org.mulletaflix.domain.usecase.CheckAppUpdateUseCase

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
    @Inject lateinit var lanServerRecovery: LanServerRecovery
    @Inject lateinit var checkAppUpdateUseCase: CheckAppUpdateUseCase
    @Inject lateinit var appUpdateDownloader: AppUpdateDownloader

    override fun onCreate(savedInstanceState: Bundle?) {
        installSplashScreen()
        super.onCreate(savedInstanceState)
        incomingDeepLink = mediaDeepLinkRequest(intent, ++deepLinkSequence)
        enableEdgeToEdge()

        setContent {
            val serverUrl by sessionRepository.getBaseUrl().collectAsStateWithLifecycle(initialValue = "")
            val accessToken by sessionRepository.getAccessToken().collectAsStateWithLifecycle(initialValue = null)
            val serverId by sessionRepository.getServerId().collectAsStateWithLifecycle(initialValue = null)
            var sessionResolved by remember { mutableStateOf(false) }
            var hasValidSession by remember { mutableStateOf(false) }

            var availableUpdate by remember { mutableStateOf<AppUpdateInfo?>(null) }
            var isDownloadingUpdate by remember { mutableStateOf(false) }
            var updateProgress by remember { mutableStateOf(0f) }
            var showUpdateDialog by remember { mutableStateOf(false) }
            var updateError by remember { mutableStateOf<String?>(null) }
            var dismissedUpdateVersion by remember { mutableStateOf<String?>(null) }

            val coroutineScope = rememberCoroutineScope()
            val context = LocalContext.current
            val lifecycleOwner = LocalLifecycleOwner.current

            LaunchedEffect(Unit) {
                hasValidSession = combine(
                    sessionRepository.getBaseUrl(),
                    sessionRepository.getAccessToken(),
                    sessionRepository.getCurrentUserId(),
                ) { url, token, userId -> hasUsableSession(url, token, userId) }.first()
                sessionResolved = true
            }

            LaunchedEffect(lifecycleOwner, sessionResolved) {
                if (!sessionResolved) return@LaunchedEffect
                lifecycleOwner.lifecycle.repeatOnLifecycle(Lifecycle.State.RESUMED) {
                    // Re-check on every foreground transition so TV devices
                    // can notice a new APK without being force-stopped.
                    try {
                        checkAppUpdateUseCase(BuildConfig.VERSION_NAME).onSuccess { info ->
                            availableUpdate = info
                            if (shouldShowAppUpdateDialog(info, dismissedUpdateVersion)) {
                                showUpdateDialog = true
                            }
                        }
                    } catch (_: Exception) {
                        // Update checks are intentionally non-blocking.
                    }
                }
            }

            CompositionLocalProvider(
                LocalMulletaFlixServerUrl provides serverUrl,
                LocalMulletaFlixAccessToken provides accessToken,
                LocalMulletaFlixServerId provides serverId,
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

                        if (showUpdateDialog && availableUpdate != null) {
                            val update = availableUpdate!!
                            AlertDialog(
                                onDismissRequest = {
                                    if (!isDownloadingUpdate) {
                                        dismissedUpdateVersion = update.latestVersion
                                        showUpdateDialog = false
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
                                                progress = { updateProgress },
                                                modifier = Modifier.fillMaxWidth(),
                                            )
                                            Text(
                                                text = "Baixando: ${(updateProgress * 100).toInt()}%",
                                                style = MaterialTheme.typography.bodySmall,
                                                modifier = Modifier.padding(top = 4.dp),
                                            )
                                        }
                                        updateError?.let { error ->
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
                                            val downloadUrl = update.apkDownloadUrl ?: return@Button
                                            coroutineScope.launch {
                                                updateError = null
                                                updateProgress = 0f
                                                isDownloadingUpdate = true
                                                try {
                                                    appUpdateDownloader.downloadApk(
                                                        downloadUrl = downloadUrl,
                                                        versionName = update.latestVersion,
                                                        expectedSha256 = update.apkSha256,
                                                    ).collect { downloadState ->
                                                        when (downloadState) {
                                                            is DownloadState.Downloading -> {
                                                                updateProgress = downloadState.progress
                                                            }
                                                            is DownloadState.Completed -> {
                                                                isDownloadingUpdate = false
                                                                val installationStarted = runCatching {
                                                                    AppUpdateInstaller.installApk(context, downloadState.file)
                                                                }.getOrElse { error ->
                                                                    updateError = error.localizedMessage
                                                                        ?: "Não foi possível abrir o instalador do APK."
                                                                    false
                                                                }
                                                                if (installationStarted) {
                                                                    showUpdateDialog = false
                                                                } else if (updateError == null) {
                                                                    updateError = "Permita a instalação de fontes desconhecidas e tente novamente."
                                                                }
                                                            }
                                                            is DownloadState.Error -> {
                                                                isDownloadingUpdate = false
                                                                updateError = downloadState.message
                                                            }
                                                            DownloadState.Idle -> Unit
                                                        }
                                                    }
                                                } catch (error: CancellationException) {
                                                    throw error
                                                } catch (error: Exception) {
                                                    isDownloadingUpdate = false
                                                    updateError = error.localizedMessage
                                                        ?: "Não foi possível baixar a atualização."
                                                }
                                            }
                                        },
                                        enabled = !isDownloadingUpdate && !update.apkDownloadUrl.isNullOrBlank(),
                                    ) {
                                        Text(
                                            when {
                                                isDownloadingUpdate -> "Baixando..."
                                                updateError != null -> "Tentar novamente"
                                                else -> "Atualizar Agora"
                                            },
                                        )
                                    }
                                },
                                dismissButton = {
                                    if (!isDownloadingUpdate) {
                                        TextButton(onClick = {
                                            dismissedUpdateVersion = update.latestVersion
                                            showUpdateDialog = false
                                        }) {
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

    override fun onDestroy() {
        lanServerRecovery.stop()
        super.onDestroy()
    }
}
