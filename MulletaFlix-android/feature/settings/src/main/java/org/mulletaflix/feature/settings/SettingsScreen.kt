package org.mulletaflix.feature.settings

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import org.mulletaflix.designsystem.theme.MulletaFlixThemeVariant

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
    viewModel: SettingsViewModel = hiltViewModel()
) {
    val state by viewModel.state.collectAsState()

    Scaffold(
        topBar = {
            TopAppBar(title = { Text("Configurações") })
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
                SettingsItem(icon = Icons.Default.Dns, title = "Servidor", subtitle = state.serverUrl ?: "Não configurado") {}
                SettingsItem(icon = Icons.Default.Person, title = "Conta", subtitle = state.username ?: "") {}
                SettingsItem(icon = Icons.Default.Group, title = "Salas SyncPlay", subtitle = "Assistir sincronizado com amigos", onClick = onSyncPlay)
                SettingsItem(icon = Icons.Default.Logout, title = "Sair", subtitle = "Desconectar da conta atual", onClick = {
                    viewModel.logout()
                    onLogout()
                })
            }

            // ── Aparência ────────────────────────────────────────────────────
            SettingsGroup(title = "Aparência") {
                var showThemeDialog by remember { mutableStateOf(false) }
                SettingsItem(
                    icon = Icons.Default.Palette,
                    title = "Tema",
                    subtitle = state.theme.displayName,
                    onClick = { showThemeDialog = true }
                )
                if (showThemeDialog) {
                    ThemePickerDialog(
                        current = state.theme,
                        onSelect = { viewModel.setTheme(it); showThemeDialog = false },
                        onDismiss = { showThemeDialog = false }
                    )
                }
            }

            // ── Reprodução ───────────────────────────────────────────────────
            SettingsGroup(title = "Reprodução") {
                SettingsItem(icon = Icons.Default.Hd, title = "Qualidade Padrão", subtitle = state.defaultQuality) {}
                SettingsItem(icon = Icons.Default.Speed, title = "Velocidade Padrão", subtitle = "${state.defaultSpeed}x") {}
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
                SettingsItem(icon = Icons.Default.TextFields, title = "Tamanho da Fonte", subtitle = "${state.subtitleFontSize}%") {}
            }

            // ── Downloads ────────────────────────────────────────────────────
            SettingsGroup(title = "Downloads") {
                SettingsItem(icon = Icons.Default.Folder, title = "Pasta de Downloads", subtitle = state.downloadPath) {}
                SettingsItem(icon = Icons.Default.Storage, title = "Limite de Armazenamento", subtitle = "${state.downloadStorageGb} GB") {}
                SettingsItem(icon = Icons.Default.Hd, title = "Qualidade de Download", subtitle = state.downloadQuality) {}
            }

            // ── Cache ────────────────────────────────────────────────────────
            SettingsGroup(title = "Cache") {
                SettingsItem(icon = Icons.Default.DeleteOutline, title = "Limpar Cache de Imagens", subtitle = "Libera espaço removendo imagens em cache", onClick = { viewModel.clearImageCache() })
                SettingsItem(icon = Icons.Default.Delete, title = "Limpar Todos os Dados Locais", subtitle = "Remove cache e preferências locais", onClick = { viewModel.clearAllCache() })
            }

            // ── Sobre ────────────────────────────────────────────────────────
            SettingsGroup(title = "Sobre") {
                SettingsItem(icon = Icons.Default.Info, title = "Versão", subtitle = "MulletaFlix Android 12.0.0") {}
                SettingsItem(icon = Icons.Default.OpenInBrowser, title = "GitHub", subtitle = "github.com/raphaelpera85/MulletaFlix") {}
                SettingsItem(icon = Icons.Default.Gavel, title = "Licenças", subtitle = "GPL-2.0 e licenças de terceiros") {}
            }
        }
    }
}

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
private fun SettingsItem(icon: ImageVector, title: String, subtitle: String, onClick: () -> Unit = {}) {
    Row(
        modifier = Modifier.fillMaxWidth().clickable(onClick = onClick).padding(horizontal = 16.dp, vertical = 12.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Icon(icon, contentDescription = null, tint = MaterialTheme.colorScheme.onSurfaceVariant)
        Column(modifier = Modifier.weight(1f).padding(horizontal = 12.dp)) {
            Text(title, style = MaterialTheme.typography.bodyMedium)
            if (subtitle.isNotBlank()) Text(subtitle, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
        Icon(Icons.Default.ChevronRight, contentDescription = null, tint = MaterialTheme.colorScheme.onSurfaceVariant)
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
private fun ThemePickerDialog(
    current: MulletaFlixThemeVariant,
    onSelect: (MulletaFlixThemeVariant) -> Unit,
    onDismiss: () -> Unit,
) {
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Escolher Tema") },
        text = {
            Column {
                MulletaFlixThemeVariant.values().forEach { theme ->
                    Row(
                        verticalAlignment = Alignment.CenterVertically,
                        modifier = Modifier.fillMaxWidth().clickable { onSelect(theme) }.padding(vertical = 8.dp)
                    ) {
                        RadioButton(selected = current == theme, onClick = { onSelect(theme) })
                        Text(theme.displayName, modifier = Modifier.padding(start = 8.dp))
                    }
                }
            }
        },
        confirmButton = { TextButton(onClick = onDismiss) { Text("Cancelar") } }
    )
}

@Composable
private fun SubtitleLanguageDialog(
    current: String,
    onSelect: (String) -> Unit,
    onDismiss: () -> Unit,
) {
    val options = listOf("Português (Brasil)", "English", "Idioma original", "Desativadas")
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Idioma das legendas") },
        text = {
            Column {
                options.forEach { option ->
                    Row(
                        verticalAlignment = Alignment.CenterVertically,
                        modifier = Modifier.fillMaxWidth().clickable { onSelect(option) }.padding(vertical = 8.dp),
                    ) {
                        RadioButton(selected = current == option, onClick = { onSelect(option) })
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
