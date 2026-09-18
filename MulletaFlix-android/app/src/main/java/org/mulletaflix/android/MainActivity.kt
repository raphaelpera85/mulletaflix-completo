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
import androidx.compose.foundation.layout.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.painterResource
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

    @Inject lateinit var sessionRepository: SessionRepository
    @Inject lateinit var lanServerRecovery: LanServerRecovery
    @Inject lateinit var checkAppUpdateUseCase: CheckAppUpdateUseCase
    @Inject lateinit var appUpdateDownloader: AppUpdateDownloader

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

            var availableUpdate by remember { mutableStateOf<AppUpdateInfo?>(null) }
            var isDownloadingUpdate by remember { mutableStateOf(false) }
            var updateProgress by remember { mutableStateOf(0f) }
            var showUpdateDialog by remember { mutableStateOf(false) }

            val coroutineScope = rememberCoroutineScope()
            val context = LocalContext.current

            LaunchedEffect(Unit) {
                hasValidSession = combine(
                    sessionRepository.getBaseUrl(),
                    sessionRepository.getAccessToken(),
                    sessionRepository.getCurrentUserId(),
                ) { url, token, userId -> hasUsableSession(url, token, userId) }.first()
                sessionResolved = true

                // Check for updates against GitHub Releases
                try {
                    val result = checkAppUpdateUseCase(BuildConfig.VERSION_NAME)
                    result.onSuccess { info ->
                        if (info.isUpdateAvailable && !info.apkDownloadUrl.isNullOrBlank()) {
                            availableUpdate = info
                            showUpdateDialog = true
                        }
                    }
                } catch (_: Exception) {
                    // Non-blocking background check
                }
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

                        if (showUpdateDialog && availableUpdate != null) {
                            val update = availableUpdate!!
                            AlertDialog(
                                onDismissRequest = {
                                    if (!isDownloadingUpdate) {
                                        showUpdateDialog = false
                                    }
                                },
                                icon = {
                                    Icon(
                                        Icons.Default.CloudDownload,
                                        contentDescription = null,
                                        tint = MaterialTheme.colorScheme.primary,
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
                                            Text(
                                                text = notes,
                                                style = MaterialTheme.typography.bodySmall,
                                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                                                maxLines = 6,
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
                                    }
                                },
                                confirmButton = {
                                    Button(
                                        onClick = {
                                            val downloadUrl = update.apkDownloadUrl ?: return@Button
                                            coroutineScope.launch {
                                                isDownloadingUpdate = true
                                                appUpdateDownloader.downloadApk(downloadUrl, update.latestVersion).collect { downloadState ->
                                                    when (downloadState) {
                                                        is DownloadState.Downloading -> {
                                                            updateProgress = downloadState.progress
                                                        }
                                                        is DownloadState.Completed -> {
                                                            isDownloadingUpdate = false
                                                            showUpdateDialog = false
                                                            AppUpdateInstaller.installApk(context, downloadState.file)
                                                        }
                                                        is DownloadState.Error -> {
                                                            isDownloadingUpdate = false
                                                        }
                                                        DownloadState.Idle -> Unit
                                                    }
                                                }
                                            }
                                        },
                                        enabled = !isDownloadingUpdate && !update.apkDownloadUrl.isNullOrBlank(),
                                    ) {
                                        Text(if (isDownloadingUpdate) "Baixando..." else "Atualizar Agora")
                                    }
                                },
                                dismissButton = {
                                    if (!isDownloadingUpdate) {
                                        TextButton(onClick = { showUpdateDialog = false }) {
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
