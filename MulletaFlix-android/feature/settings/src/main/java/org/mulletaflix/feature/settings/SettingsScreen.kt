package org.mulletaflix.feature.settings

import android.content.Context
import android.content.Intent
import android.net.Uri
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.selection.selectable
import androidx.compose.foundation.selection.selectableGroup
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.automirrored.filled.Logout
import androidx.compose.material.icons.automirrored.filled.List
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.compose.LocalLifecycleOwner
import androidx.lifecycle.repeatOnLifecycle
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import org.mulletaflix.core.common.update.AppUpdateInstaller
import org.mulletaflix.designsystem.theme.MulletaFlixThemeVariant
import org.mulletaflix.designsystem.components.ReleaseNotesText
import org.mulletaflix.designsystem.components.MulletaFlixTopBarAction

/**
 * Settings screen with categorized preferences.
 *
 * Categories:
 *  - Servidor: URL, usuário, logout, múltiplos servidores
 *  - Reprodução: qualidade padrão, auto, velocidade padrão
 *  - Subtítulos: idioma, tamanho, cor
 *  - Aparência: tema (7 opções), idioma do app
 *  - Downloads: localização, limite de armazenamento
 *  - Cache: limpar
 *  - Notificações: novos episódios, gravações
 *  - Sobre: versão, licenças, GitHub
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun SettingsScreen(
    onLogout: () -> Unit,
    onSyncPlay: () -> Unit = {},
    onProfile: () -> Unit = {},
    onBack: (() -> Unit)? = null,
    viewModel: SettingsViewModel = hiltViewModel()
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    var showLicensesDialog by remember { mutableStateOf(false) }
    var showClearAllDataDialog by remember { mutableStateOf(false) }
    var showClearImageCacheDialog by remember { mutableStateOf(false) }
    val context = androidx.compose.ui.platform.LocalContext.current
    val lifecycleOwner = LocalLifecycleOwner.current

    LaunchedEffect(lifecycleOwner) {
        // Poll only while the screen is actually visible: a plain
        // `LaunchedEffect(Unit)` keeps ticking after the user navigates away,
        // waking the device for a storage figure nobody is looking at.
        lifecycleOwner.lifecycle.repeatOnLifecycle(Lifecycle.State.RESUMED) {
            while (isActive) {
                viewModel.refreshStorageInfo()
                delay(30_000L)
            }
        }
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Configurações") },
                navigationIcon = {
                    if (onBack != null) {
                        MulletaFlixTopBarAction(onClick = onBack) {
                            Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Voltar")
                        }
                    }
                }
            )
        }
    ) { padding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .verticalScroll(rememberScrollState())
        ) {

            // ── Servidor ─────────────────────────────────────────────────────
            SettingsGroup(title = "Servidor") {
                SettingsItem(
                    icon = Icons.Default.Dns,
                    title = if (state.isCheckingConnection) "Testando servidor…" else "Testar conexão",
                    subtitle = state.connectionStatus ?: state.serverUrl ?: "Não configurado",
                    onClick = viewModel::checkServerConnection,
                    enabled = !state.isCheckingConnection,
                )
                SettingsItem(icon = Icons.Default.Person, title = "Meu Perfil", subtitle = state.username ?: "Ver perfil, permissões e alternar usuário", onClick = onProfile)
                SettingsItem(icon = Icons.Default.Group, title = "Salas SyncPlay", subtitle = "Assistir sincronizado com amigos", onClick = onSyncPlay)
                SettingsItem(icon = Icons.AutoMirrored.Filled.Logout, title = "Sair", subtitle = "Desconectar da conta atual", onClick = {
                    viewModel.logout()
                    onLogout()
                })
            }

            // ── Aparência ────────────────────────────────────────────────────
            SettingsGroup(title = "Aparência") {
                var showThemeDialog by remember { mutableStateOf(false) }
                var showGridDensityDialog by remember { mutableStateOf(false) }
                var showLibrarySortDialog by remember { mutableStateOf(false) }
                var showLibrarySortOrderDialog by remember { mutableStateOf(false) }
                SettingsItem(
                    icon = Icons.Default.Palette,
                    title = "Tema",
                    subtitle = state.theme.displayName,
                    onClick = { showThemeDialog = true }
                )
                SettingsItem(
                    icon = Icons.Default.GridView,
                    title = "Densidade da Grade",
                    subtitle = state.libraryGridDensity,
                    onClick = { showGridDensityDialog = true },
                )
                SettingsItem(
                    icon = Icons.AutoMirrored.Filled.List,
                    title = "Ordenação da Biblioteca",
                    subtitle = "${state.librarySort} • ${state.librarySortOrder}",
                    onClick = { showLibrarySortDialog = true },
                )
                SettingsItem(
                    icon = Icons.Default.SwapVert,
                    title = "Direção da Ordenação",
                    subtitle = state.librarySortOrder,
                    onClick = { showLibrarySortOrderDialog = true },
                )
                if (showThemeDialog) {
                    ThemePickerDialog(
                        current = state.theme,
                        onSelect = { viewModel.setTheme(it); showThemeDialog = false },
                        onDismiss = { showThemeDialog = false }
                    )
                }
                if (showGridDensityDialog) {
                    ChoiceDialog(
                        title = "Densidade da grade",
                        options = libraryGridDensityChoices.map { it.label },
                        selected = state.libraryGridDensity,
                        onSelect = { viewModel.setLibraryGridDensity(it); showGridDensityDialog = false },
                        onDismiss = { showGridDensityDialog = false },
                    )
                }
                if (showLibrarySortDialog) {
                    ChoiceDialog(
                        title = "Ordenação padrão da biblioteca",
                        options = librarySortLabels,
                        selected = state.librarySort,
                        onSelect = { viewModel.setLibrarySort(it); showLibrarySortDialog = false },
                        onDismiss = { showLibrarySortDialog = false },
                    )
                }
                if (showLibrarySortOrderDialog) {
                    ChoiceDialog(
                        title = "Direção padrão da biblioteca",
                        options = librarySortOrderChoices.map { it.label },
                        selected = state.librarySortOrder,
                        onSelect = { viewModel.setLibrarySortOrder(it); showLibrarySortOrderDialog = false },
                        onDismiss = { showLibrarySortOrderDialog = false },
                    )
                }
            }

            // ── Reprodução ───────────────────────────────────────────────────
            SettingsGroup(title = "Reprodução") {
                var showQualityDialog by remember { mutableStateOf(false) }
                var showAspectRatioDialog by remember { mutableStateOf(false) }
                var showSpeedDialog by remember { mutableStateOf(false) }
                SettingsItem(icon = Icons.Default.Hd, title = "Qualidade Padrão", subtitle = qualityLabel(state.defaultQuality), onClick = { showQualityDialog = true })
                SettingsItem(
                    icon = Icons.Default.AspectRatio,
                    title = "Proporção da Imagem",
                    subtitle = aspectRatioTitle(state.aspectRatio),
                    onClick = { showAspectRatioDialog = true },
                )
                SettingsItem(icon = Icons.Default.Speed, title = "Velocidade Padrão", subtitle = "${state.defaultSpeed}x", onClick = { showSpeedDialog = true })
                if (showQualityDialog) {
                    ChoiceDialog(
                        title = "Qualidade padrão",
                        options = choicesIncludingCurrent(defaultQualityChoices, state.defaultQuality),
                        selected = state.defaultQuality,
                        onSelect = { viewModel.setDefaultQuality(it); showQualityDialog = false },
                        onDismiss = { showQualityDialog = false },
                    )
                }
                if (showAspectRatioDialog) {
                    ChoiceDialog(
                        title = "Proporção da imagem",
                        options = aspectRatioChoices.map { it.second },
                        selected = aspectRatioTitle(state.aspectRatio),
                        onSelect = { selected ->
                            aspectRatioChoices.firstOrNull { it.second == selected }?.first?.let(viewModel::setDefaultAspectRatio)
                            showAspectRatioDialog = false
                        },
                        onDismiss = { showAspectRatioDialog = false },
                    )
                }
                if (showSpeedDialog) {
                    ChoiceDialog(
                        title = "Velocidade padrão",
                        options = playbackSpeedChoices.map(::playbackSpeedLabel),
                        selected = playbackSpeedLabel(state.defaultSpeed),
                        onSelect = { viewModel.setDefaultPlaybackSpeed(it.toFloat()); showSpeedDialog = false },
                        onDismiss = { showSpeedDialog = false },
                    )
                }
                SettingsToggle(
                    icon = Icons.Default.PlayCircle,
                    title = "Reprodução Automática",
                    subtitle = "Reproduzir próximo episódio automaticamente",
                    checked = state.autoPlay,
                    onCheckedChange = viewModel::setAutoPlay
                )
                SettingsToggle(
                    icon = Icons.Default.SkipNext,
                    title = "Pular Introdução",
                    subtitle = "Mostrar botão para pular abertura",
                    checked = state.skipIntro,
                    onCheckedChange = viewModel::setSkipIntro
                )
                SettingsToggle(
                    icon = Icons.Default.PictureInPicture,
                    title = "Picture-in-Picture",
                    subtitle = "Continuar assistindo ao sair do player",
                    checked = state.pictureInPicture,
                    onCheckedChange = viewModel::setPictureInPicture,
                )
            }

            // ── Áudio ───────────────────────────────────────────────────────
            SettingsGroup(title = "Áudio") {
                var showAudioDialog by remember { mutableStateOf(false) }
                SettingsItem(
                    icon = Icons.Default.Audiotrack,
                    title = "Idioma do Áudio",
                    subtitle = state.audioLanguage,
                    onClick = { showAudioDialog = true },
                )
                if (showAudioDialog) {
                    AudioLanguageDialog(
                        current = state.audioLanguage,
                        onSelect = { viewModel.setAudioLanguage(it); showAudioDialog = false },
                        onDismiss = { showAudioDialog = false },
                    )
                }
            }

            // ── Subtítulos ───────────────────────────────────────────────────
            SettingsGroup(title = "Legendas") {
                var showSubtitleDialog by remember { mutableStateOf(false) }
                SettingsItem(icon = Icons.Default.ClosedCaption, title = "Idioma Padrão", subtitle = state.subtitleLanguage, onClick = { showSubtitleDialog = true })
                if (showSubtitleDialog) {
                    SubtitleLanguageDialog(
                        current = state.subtitleLanguage,
                        onSelect = { viewModel.setSubtitleLanguage(it); showSubtitleDialog = false },
                        onDismiss = { showSubtitleDialog = false },
                    )
                }
                var showFontSizeDialog by remember { mutableStateOf(false) }
                SettingsItem(icon = Icons.Default.TextFields, title = "Tamanho da Fonte", subtitle = "${state.subtitleFontSize}%", onClick = { showFontSizeDialog = true })
                if (showFontSizeDialog) {
                    ChoiceDialog(
                        title = "Tamanho da legenda",
                        options = subtitleFontSizeChoices.map(::subtitleFontSizeLabel),
                        selected = subtitleFontSizeLabel(state.subtitleFontSize),
                        onSelect = { viewModel.setSubtitleFontSize(it.toInt()); showFontSizeDialog = false },
                        onDismiss = { showFontSizeDialog = false },
                    )
                }
                var showSubtitleColorDialog by remember { mutableStateOf(false) }
                SettingsItem(
                    icon = Icons.Default.FormatColorText,
                    title = "Cor da Legenda",
                    subtitle = state.subtitleColor,
                    onClick = { showSubtitleColorDialog = true },
                )
                if (showSubtitleColorDialog) {
                    ChoiceDialog(
                        title = "Cor da legenda",
                        options = subtitleColorChoices.map { it.label },
                        selected = state.subtitleColor,
                        onSelect = { viewModel.setSubtitleColor(it); showSubtitleColorDialog = false },
                        onDismiss = { showSubtitleColorDialog = false },
                    )
                }
                // Neither setting had any feedback until a video was playing.
                SubtitlePreview(
                    sizePercent = state.subtitleFontSize,
                    colorCode = subtitleColorCode(state.subtitleColor),
                    modifier = Modifier.padding(horizontal = 16.dp, vertical = 4.dp),
                )
            }

            // ── Downloads ────────────────────────────────────────────────────
            SettingsGroup(title = "Downloads") {
                SettingsItem(icon = Icons.Default.Folder, title = "Pasta de Downloads", subtitle = state.downloadPath, enabled = false)
                SettingsItem(icon = Icons.Default.Storage, title = "Espaço livre para downloads", subtitle = "${state.downloadStorageGb} GB disponíveis", enabled = false)
                // A linha "Qualidade de Download: 1080p (Original)" saiu daqui: era um
                // literal fabricado, sem chave no repositório e sem ninguém que o
                // lesse — o download usa a URL que o servidor devolve. Uma tela não
                // pode afirmar uma preferência que não existe.
            }

            // ── Cache ────────────────────────────────────────────────────────
            SettingsGroup(title = "Cache") {
                SettingsItem(icon = Icons.Default.DeleteOutline, title = "Limpar Cache de Imagens", subtitle = "Libera espaço removendo imagens em cache", onClick = { showClearImageCacheDialog = true })
                state.cacheStatusMessage?.let { message ->
                    Text(
                        text = message,
                        color = MaterialTheme.colorScheme.primary,
                        style = MaterialTheme.typography.bodySmall,
                        modifier = Modifier.padding(horizontal = 20.dp, vertical = 4.dp),
                    )
                }
                SettingsItem(
                    icon = Icons.Default.Delete,
                    title = "Limpar Todos os Dados Locais",
                    subtitle = "Remove cache e preferências locais",
                    onClick = { showClearAllDataDialog = true },
                )
            }

            // ── Sobre ────────────────────────────────────────────────────────
            SettingsGroup(title = "Sobre") {
                val currentAppVersion = remember {
                    try {
                        val pInfo = context.packageManager.getPackageInfo(context.packageName, 0)
                        pInfo.versionName ?: "1.0.0"
                    } catch (_: Exception) {
                        "1.0.0"
                    }
                }
                SettingsItem(icon = Icons.Default.Info, title = "Versão", subtitle = "MulletaFlix Android $currentAppVersion", enabled = false)
                SettingsItem(
                    icon = Icons.Default.SystemUpdate,
                    title = "Verificar Atualizações do Aplicativo",
                    subtitle = when {
                        state.isCheckingUpdate -> "Buscando novas versões no GitHub..."
                        state.isDownloadingUpdate -> "Baixando atualização (${(state.updateDownloadProgress * 100).toInt()}%)..."
                        state.updateStatusMessage != null -> state.updateStatusMessage ?: ""
                        state.updateErrorMessage != null -> state.updateErrorMessage ?: ""
                        else -> "Tocar para verificar atualizações"
                    },
                    onClick = { viewModel.checkForUpdates(currentAppVersion) },
                    enabled = !state.isCheckingUpdate && !state.isDownloadingUpdate,
                )
                SettingsItem(
                    icon = Icons.Default.OpenInBrowser,
                    title = "GitHub",
                    subtitle = "github.com/raphaelpera85/MulletaFlix",
                    onClick = { openExternalUrl(context, "https://github.com/raphaelpera85/MulletaFlix") },
                )
                SettingsItem(
                    icon = Icons.Default.Gavel,
                    title = "Licenças",
                    subtitle = "GPL-2.0 e licenças de terceiros",
                    onClick = { showLicensesDialog = true },
                )
            }

            if (showLicensesDialog) {
                AlertDialog(
                    onDismissRequest = { showLicensesDialog = false },
                    icon = { Icon(Icons.Default.Gavel, contentDescription = null, tint = MaterialTheme.colorScheme.secondary) },
                    title = { Text("Licenças") },
                    text = {
                        Text(
                            "MulletaFlix Android é distribuído sob GPL-2.0. " +
                                "O aplicativo utiliza AndroidX, Jetpack Compose, Media3, Coil, Retrofit, OkHttp, Hilt e Room.",
                        )
                    },
                    confirmButton = {
                        TextButton(onClick = { showLicensesDialog = false }) { Text("Fechar") }
                    },
                )
            }

            if (showClearAllDataDialog) {
                AlertDialog(
                    onDismissRequest = { showClearAllDataDialog = false },
                    icon = { Icon(Icons.Default.Warning, contentDescription = null, tint = MaterialTheme.colorScheme.error) },
                    title = { Text("Limpar dados locais?") },
                    text = { Text("Isso removerá preferências, cache e a sessão atual. A ação não pode ser desfeita.") },
                    confirmButton = {
                        TextButton(
                            onClick = {
                                showClearAllDataDialog = false
                                viewModel.clearAllCache()
                            },
                            colors = ButtonDefaults.textButtonColors(contentColor = MaterialTheme.colorScheme.error),
                        ) { Text("Limpar e sair") }
                    },
                    dismissButton = {
                        TextButton(onClick = { showClearAllDataDialog = false }) { Text("Cancelar") }
                    },
                )
            }

            if (showClearImageCacheDialog) {
                ClearImageCacheDialog(
                    onConfirm = {
                        showClearImageCacheDialog = false
                        viewModel.clearImageCache()
                    },
                    onDismiss = { showClearImageCacheDialog = false },
                )
            }

            if (state.showUpdateDialog && state.updateInfo != null) {
                val update = state.updateInfo!!
                val context = androidx.compose.ui.platform.LocalContext.current
                AlertDialog(
                    onDismissRequest = { if (!state.isDownloadingUpdate) viewModel.dismissUpdateDialog() },
                    icon = { Icon(Icons.Default.CloudDownload, contentDescription = null, tint = MaterialTheme.colorScheme.secondary) },
                    title = { Text("Nova Versão Disponível: v${update.latestVersion}") },
                    text = {
                        Column(
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(vertical = 4.dp)
                        ) {
                            Text(
                                text = "Uma nova versão do MulletaFlix Android está disponível para instalação!",
                                style = MaterialTheme.typography.bodyMedium
                            )
                            if (update.apkSize > 0) {
                                Text(
                                    text = "Tamanho: ${String.format(java.util.Locale.US, "%.1f", update.apkSize / (1024f * 1024f))} MB",
                                    style = MaterialTheme.typography.bodySmall,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                                    modifier = Modifier.padding(top = 4.dp)
                                )
                            }
                            val notes = update.releaseNotes
                            if (!notes.isNullOrBlank()) {
                                Spacer(modifier = Modifier.height(8.dp))
                                Text(
                                    text = "Novidades:",
                                    style = MaterialTheme.typography.titleSmall
                                )
                                ReleaseNotesText(
                                    markdown = notes,
                                    modifier = Modifier.padding(top = 4.dp),
                                )
                            }
                            if (state.isDownloadingUpdate) {
                                Spacer(modifier = Modifier.height(16.dp))
                                LinearProgressIndicator(
                                    progress = { state.updateDownloadProgress },
                                    modifier = Modifier.fillMaxWidth(),
                                )
                                Text(
                                    text = "Baixando: ${(state.updateDownloadProgress * 100).toInt()}%",
                                    style = MaterialTheme.typography.bodySmall,
                                    modifier = Modifier.padding(top = 4.dp)
                                )
                            }
                            state.updateErrorMessage?.let { error ->
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
                                viewModel.downloadAndInstallUpdate { file ->
                                    AppUpdateInstaller.installApk(context, file)
                                }
                            },
                            enabled = !state.isDownloadingUpdate && !update.apkDownloadUrl.isNullOrBlank()
                        ) {
                            Text(
                                when {
                                    state.isDownloadingUpdate -> "Baixando..."
                                    state.updateErrorMessage != null -> "Tentar novamente"
                                    else -> "Atualizar Agora"
                                },
                            )
                        }
                    },
                    dismissButton = {
                        if (!state.isDownloadingUpdate) {
                            TextButton(onClick = viewModel::dismissUpdateDialog) {
                                Text("Depois")
                            }
                        }
                    }
                )
            }
        }
    }
}

@Composable
internal fun ClearImageCacheDialog(
    onConfirm: () -> Unit,
    onDismiss: () -> Unit,
) {
    AlertDialog(
        onDismissRequest = onDismiss,
        icon = { Icon(Icons.Default.DeleteOutline, contentDescription = null, tint = MaterialTheme.colorScheme.error) },
        title = { Text("Limpar cache de imagens?") },
        text = { Text("As capas e imagens serão baixadas novamente quando necessário.") },
        confirmButton = {
            TextButton(
                onClick = onConfirm,
                colors = ButtonDefaults.textButtonColors(contentColor = MaterialTheme.colorScheme.error),
            ) { Text("Limpar") }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) { Text("Cancelar") }
        },
    )
}

internal fun openExternalUrl(context: Context, url: String) {
    runCatching {
        context.startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(url)))
    }
}

@Composable
internal fun ChoiceDialog(
    title: String,
    options: List<String>,
    selected: String,
    onSelect: (String) -> Unit,
    onDismiss: () -> Unit,
) {
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text(title) },
        text = { ChoiceDialogOptions(options, selected, onSelect) },
        confirmButton = { TextButton(onClick = onDismiss) { Text("Fechar") } },
    )
}

/**
 * A altura máxima da lista de opções de um diálogo.
 *
 * A lista de ordenação tem oito linhas e a de idiomas cinco. Numa janela de
 * diálogo baixa — a de TV, onde ela ainda divide espaço com a barra de botões —
 * as últimas linhas ficavam compostas fora dos limites da janela: recortadas,
 * invisíveis e inalcançáveis até com o controle remoto, porque o toque e o foco
 * também são recortados. O teto é explícito para que a rolagem tenha um viewport
 * definido mesmo quando a janela oferece altura infinita.
 */
private val ChoiceDialogOptionsMaxHeight = 360.dp

@Composable
internal fun ChoiceDialogOptions(
    options: List<String>,
    selected: String,
    onSelect: (String) -> Unit,
) {
    Column(
        modifier = Modifier
            .heightIn(max = ChoiceDialogOptionsMaxHeight)
            // `heightIn` vem antes de `verticalScroll` de propósito: na ordem
            // inversa o modificador de rolagem mede o filho com altura infinita e
            // devolve o próprio tamanho do filho, então nada rola.
            .verticalScroll(rememberScrollState()),
    ) {
        options.forEach { option ->
            TextButton(onClick = { onSelect(option) }, modifier = Modifier.fillMaxWidth()) {
                Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
                    Text(option)
                    if (option == selected) Icon(Icons.Default.Check, contentDescription = "Selecionado")
                }
            }
        }
    }
}

private fun qualityLabel(value: String): String = if (value == "Auto") "Automático" else value

@Composable
private fun SettingsGroup(title: String, content: @Composable ColumnScope.() -> Unit) {
    Column(modifier = Modifier.padding(vertical = 4.dp)) {
        Text(
            text = title,
            style = MaterialTheme.typography.labelMedium,
            color = MaterialTheme.colorScheme.secondary,
            modifier = Modifier.padding(horizontal = 16.dp, vertical = 8.dp)
        )
        Card(
            modifier = Modifier.fillMaxWidth().padding(horizontal = 16.dp),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
        ) {
            Column { content() }
        }
    }
}

@Composable
internal fun SettingsItem(
    icon: ImageVector,
    title: String,
    subtitle: String,
    onClick: () -> Unit = {},
    enabled: Boolean = true,
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            // Linha de navegação: sem papel, o leitor de tela a anuncia como texto
            // e não como algo que se ativa.
            .clickable(enabled = enabled, role = Role.Button, onClick = onClick)
            .padding(horizontal = 16.dp, vertical = 12.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        val contentAlpha = if (enabled) 1f else 0.55f
        Icon(icon, contentDescription = null, tint = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = contentAlpha))
        Column(modifier = Modifier.weight(1f).padding(horizontal = 12.dp)) {
            Text(title, style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurface.copy(alpha = contentAlpha))
            if (subtitle.isNotBlank()) Text(subtitle, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = contentAlpha))
        }
        Icon(Icons.Default.ChevronRight, contentDescription = null, tint = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = contentAlpha))
    }
    HorizontalDivider(modifier = Modifier.padding(start = 56.dp), color = MaterialTheme.colorScheme.outlineVariant)
}

@Composable
private fun SettingsToggle(icon: ImageVector, title: String, subtitle: String, checked: Boolean, onCheckedChange: (Boolean) -> Unit) {
    Row(
        modifier = Modifier.fillMaxWidth().padding(horizontal = 16.dp, vertical = 8.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Icon(icon, contentDescription = null, tint = MaterialTheme.colorScheme.onSurfaceVariant)
        Column(modifier = Modifier.weight(1f).padding(horizontal = 12.dp)) {
            Text(title, style = MaterialTheme.typography.bodyMedium)
            Text(subtitle, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
        Switch(checked = checked, onCheckedChange = onCheckedChange)
    }
    HorizontalDivider(modifier = Modifier.padding(start = 56.dp), color = MaterialTheme.colorScheme.outlineVariant)
}

@Composable
internal fun ThemePickerDialog(
    current: MulletaFlixThemeVariant,
    onSelect: (MulletaFlixThemeVariant) -> Unit,
    onDismiss: () -> Unit,
) {
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Escolher Tema") },
        text = {
            Column(modifier = Modifier.selectableGroup()) {
                MulletaFlixThemeVariant.values().forEach { theme ->
                    Row(
                        verticalAlignment = Alignment.CenterVertically,
                        // One focus target per option. A `clickable` row holding a
                        // `RadioButton` with its own `onClick` is two, which measured on
                        // the TV emulator is exactly the "clicar 2x" defect: the remote
                        // lands on the row first and only the second press activates it.
                        // The player's own menus already use this shape.
                        modifier = Modifier
                            .fillMaxWidth()
                            .selectable(
                                selected = current == theme,
                                role = Role.RadioButton,
                                onClick = { onSelect(theme) },
                            )
                            .padding(vertical = 8.dp),
                    ) {
                        RadioButton(selected = current == theme, onClick = null)
                        Text(theme.displayName, modifier = Modifier.padding(start = 8.dp))
                    }
                }
            }
        },
        confirmButton = { TextButton(onClick = onDismiss) { Text("Cancelar") } }
    )
}

@Composable
internal fun SubtitleLanguageDialog(
    current: String,
    onSelect: (String) -> Unit,
    onDismiss: () -> Unit,
) {
    val options = choicesIncludingCurrent(subtitleLanguageLabels, current)
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Idioma das legendas") },
        text = {
            Column(modifier = Modifier.selectableGroup()) {
                options.forEach { option ->
                    // One focus target per option; see ThemePickerDialog.
                    Row(
                        verticalAlignment = Alignment.CenterVertically,
                        modifier = Modifier
                            .fillMaxWidth()
                            .selectable(
                                selected = current == option,
                                role = Role.RadioButton,
                                onClick = { onSelect(option) },
                            )
                            .padding(vertical = 8.dp),
                    ) {
                        RadioButton(selected = current == option, onClick = null)
                        Text(option, modifier = Modifier.padding(start = 8.dp))
                    }
                }
            }
        },
        confirmButton = { TextButton(onClick = onDismiss) { Text("Cancelar") } },
    )
}

@Composable
private fun AudioLanguageDialog(
    current: String,
    onSelect: (String) -> Unit,
    onDismiss: () -> Unit,
) {
    val options = choicesIncludingCurrent(audioLanguageLabels, current)
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Idioma do áudio") },
        text = {
            Column(modifier = Modifier.selectableGroup()) {
                options.forEach { option ->
                    // One focus target per option; see ThemePickerDialog.
                    Row(
                        verticalAlignment = Alignment.CenterVertically,
                        modifier = Modifier
                            .fillMaxWidth()
                            .selectable(
                                selected = current == option,
                                role = Role.RadioButton,
                                onClick = { onSelect(option) },
                            )
                            .padding(vertical = 8.dp),
                    ) {
                        RadioButton(selected = current == option, onClick = null)
                        Text(option, modifier = Modifier.padding(start = 8.dp))
                    }
                }
            }
        },
        confirmButton = { TextButton(onClick = onDismiss) { Text("Cancelar") } },
    )
}

private val MulletaFlixThemeVariant.displayName: String get() = when (this) {
    MulletaFlixThemeVariant.System -> "Sistema (Automático)"
    MulletaFlixThemeVariant.Dark -> "Escuro (Padrão)"
    MulletaFlixThemeVariant.Light -> "Claro"
    MulletaFlixThemeVariant.Netflix -> "Netflix"
    MulletaFlixThemeVariant.PurpleHaze -> "Purple Haze"
    MulletaFlixThemeVariant.BlueRadiance -> "Blue Radiance"
    MulletaFlixThemeVariant.WMC -> "WMC"
    MulletaFlixThemeVariant.AppleTV -> "Apple TV"
}
