package org.mulletaflix.feature.user

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.automirrored.filled.Logout
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.semantics.stateDescription
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import coil.compose.AsyncImage
import org.mulletaflix.designsystem.media.LocalMulletaFlixAccessToken
import org.mulletaflix.designsystem.media.LocalMulletaFlixServerUrl
import org.mulletaflix.designsystem.media.resolveMediaUrl
import org.mulletaflix.designsystem.media.userAvatarPath
import org.mulletaflix.designsystem.theme.readableTextOn
import org.mulletaflix.domain.repository.AvailableUser
import android.content.ClipData
import kotlinx.coroutines.launch
import org.mulletaflix.designsystem.components.MulletaFlixTopBarAction

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun ProfileScreen(
    onBack: (() -> Unit)? = null,
    onLogout: () -> Unit = {},
    onSwitchServer: () -> Unit = {},
    viewModel: UserProfileViewModel = hiltViewModel(),
) {
    val state by viewModel.uiState.collectAsStateWithLifecycle()
    val serverBaseUrl = LocalMulletaFlixServerUrl.current
    val activeUrl = activeServerUrl(state.serverUrl, serverBaseUrl)
    val clipboardManager = LocalContext.current.getSystemService(android.content.ClipboardManager::class.java)
    val snackbarHostState = remember { SnackbarHostState() }
    val coroutineScope = rememberCoroutineScope()
    var showLogoutConfirmDialog by remember { mutableStateOf(false) }

    Scaffold(
        snackbarHost = { SnackbarHost(snackbarHostState) },
        topBar = {
            TopAppBar(
                title = { Text("Meu Perfil", fontWeight = FontWeight.Bold) },
                navigationIcon = {
                    if (onBack != null) {
                        MulletaFlixTopBarAction(onClick = onBack) {
                            Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Voltar")
                        }
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = MaterialTheme.colorScheme.background,
                )
            )
        }
    ) { padding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .background(MaterialTheme.colorScheme.background)
                .verticalScroll(rememberScrollState())
                .padding(horizontal = 20.dp, vertical = 12.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
        ) {
            // Feedback messages
            AnimatedVisibility(visible = state.message != null) {
                Card(
                    modifier = Modifier.fillMaxWidth().padding(bottom = 16.dp),
                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.primaryContainer),
                ) {
                    Row(
                        modifier = Modifier.padding(14.dp),
                        verticalAlignment = Alignment.CenterVertically,
                    ) {
                        Icon(Icons.Default.CheckCircle, contentDescription = null, tint = MaterialTheme.colorScheme.secondary)
                        Spacer(modifier = Modifier.width(10.dp))
                        Text(state.message ?: "", style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onPrimaryContainer)
                    }
                }
            }

            AnimatedVisibility(visible = state.error != null) {
                Card(
                    modifier = Modifier.fillMaxWidth().padding(bottom = 16.dp),
                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.errorContainer),
                ) {
                    Row(
                        modifier = Modifier.padding(14.dp),
                        verticalAlignment = Alignment.CenterVertically,
                    ) {
                        Icon(Icons.Default.Error, contentDescription = null, tint = MaterialTheme.colorScheme.error)
                        Spacer(modifier = Modifier.width(10.dp))
                        Text(state.error ?: "", style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onErrorContainer)
                    }
                }
            }

            // User Header
            val userName = state.userProfile?.name ?: state.fallbackUserName ?: "Usuário MulletaFlix"
            val isAdmin = state.userProfile?.isAdministrator ?: false
            val initial = userName.firstOrNull()?.uppercase() ?: "M"
            // Avatar uploaded/created on the server (PrimaryImageTag). The initial
            // letter stays behind the image so it doubles as the placeholder.
            val avatarUrl = resolveMediaUrl(
                serverBaseUrl,
                userAvatarPath(state.userProfile?.id, state.userProfile?.primaryImageTag),
                LocalMulletaFlixAccessToken.current,
            )

            Box(
                modifier = Modifier
                    .size(100.dp)
                    .clip(CircleShape)
                    .background(
                        Brush.linearGradient(
                            listOf(
                                MaterialTheme.colorScheme.secondary,
                                MaterialTheme.colorScheme.secondary,
                            )
                        )
                    ),
                contentAlignment = Alignment.Center,
            ) {
                Text(
                    text = initial,
                    style = MaterialTheme.typography.headlineLarge,
                    color = Color.White,
                    fontWeight = FontWeight.Black,
                )
                if (avatarUrl != null) {
                    AsyncImage(
                        model = avatarUrl,
                        contentDescription = userName,
                        contentScale = ContentScale.Crop,
                        modifier = Modifier.fillMaxSize(),
                    )
                }
            }

            Spacer(modifier = Modifier.height(14.dp))
            Text(
                text = userName,
                style = MaterialTheme.typography.headlineSmall,
                fontWeight = FontWeight.Bold,
            )

            Spacer(modifier = Modifier.height(6.dp))
            Surface(
                shape = RoundedCornerShape(12.dp),
                color = if (isAdmin) MaterialTheme.colorScheme.tertiaryContainer else MaterialTheme.colorScheme.surfaceVariant,
                modifier = Modifier.padding(bottom = 20.dp),
            ) {
                Text(
                    text = if (isAdmin) "Administrador do Servidor" else "Membro MulletaFlix",
                    style = MaterialTheme.typography.labelMedium,
                    color = if (isAdmin) MaterialTheme.colorScheme.onTertiaryContainer else MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.padding(horizontal = 12.dp, vertical = 4.dp),
                    fontWeight = FontWeight.SemiBold,
                )
            }

            // Server Info Card
            Card(
                modifier = Modifier.fillMaxWidth(),
                shape = RoundedCornerShape(16.dp),
                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
            ) {
                Column(modifier = Modifier.padding(16.dp)) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Icon(Icons.Default.Dns, contentDescription = null, tint = MaterialTheme.colorScheme.secondary, modifier = Modifier.size(22.dp))
                        Spacer(modifier = Modifier.width(10.dp))
                        Text("Servidor Conectado", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.SemiBold)
                    }
                    Spacer(modifier = Modifier.height(12.dp))
                    Text(
                        text = state.serverVerification?.name ?: "MulletaFlix Server",
                        style = MaterialTheme.typography.bodyLarge,
                        fontWeight = FontWeight.Medium,
                    )
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Text(
                            text = activeUrl,
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurface.copy(alpha = 0.7f),
                            maxLines = 1,
                            overflow = TextOverflow.Ellipsis,
                            modifier = Modifier.weight(1f),
                        )
                        MulletaFlixTopBarAction(
                            onClick = {
                                if (activeUrl.isNotBlank()) {
                                    clipboardManager?.setPrimaryClip(
                                        ClipData.newPlainText("URL do servidor", activeUrl),
                                    )
                                    coroutineScope.launch {
                                        snackbarHostState.showSnackbar("URL do servidor copiada")
                                    }
                                }
                            },
                            enabled = activeUrl.isNotBlank(),
                        ) {
                            Icon(Icons.Default.ContentCopy, contentDescription = "Copiar URL do servidor")
                        }
                    }
                    Spacer(modifier = Modifier.height(8.dp))
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Box(
                            modifier = Modifier
                                .size(8.dp)
                                .clip(CircleShape)
                                .background(Color(0xFF4CAF50))
                        )
                        Spacer(modifier = Modifier.width(6.dp))
                        Text(
                            text = formatServerConnectionStatus(state.serverVerification?.latencyMs),
                            style = MaterialTheme.typography.bodySmall,
                            color = Color(0xFF4CAF50),
                            fontWeight = FontWeight.Medium,
                        )
                        state.serverVerification?.version?.let { ver ->
                            Text(
                                text = " • v$ver",
                                style = MaterialTheme.typography.bodySmall,
                                color = readableTextOn(
                                    MaterialTheme.colorScheme.onSurface.copy(alpha = 0.5f),
                                    MaterialTheme.colorScheme.surface,
                                ),
                            )
                        }
                    }
                    Text(
                        text = "Rota: ${serverConnectionModeLabel(classifyServerConnection(activeUrl))}",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        modifier = Modifier.padding(top = 4.dp),
                    )
                }
            }

            Spacer(modifier = Modifier.height(16.dp))

            // Privileges & Resources
            Card(
                modifier = Modifier.fillMaxWidth(),
                shape = RoundedCornerShape(16.dp),
                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
            ) {
                Column(modifier = Modifier.padding(16.dp)) {
                    Text("Recursos & Permissões", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.SemiBold)
                    Spacer(modifier = Modifier.height(12.dp))
                    ProfilePrivilegeRow("Transmissão 4K HDR / Dolby Vision", state.userProfile?.canPlayMedia ?: true)
                    ProfilePrivilegeRow("TV Ao Vivo & Gravações (Live TV)", state.userProfile?.canAccessLiveTv ?: true)
                    ProfilePrivilegeRow("Downloads para Reprodução Offline", state.userProfile?.canDownload ?: true)
                    ProfilePrivilegeRow("Sessões Compartilhadas (SyncPlay)", true)
                }
            }

            // Quick User Switcher (if there are other public accounts on server)
            if (state.availableUsers.isNotEmpty()) {
                Spacer(modifier = Modifier.height(16.dp))
                Card(
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(16.dp),
                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
                ) {
                    Column(modifier = Modifier.padding(16.dp)) {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Icon(Icons.Default.Group, contentDescription = null, tint = MaterialTheme.colorScheme.secondary, modifier = Modifier.size(20.dp))
                            Spacer(modifier = Modifier.width(8.dp))
                            Text("Alternar Perfil Rapidamente", style = MaterialTheme.typography.titleSmall, fontWeight = FontWeight.SemiBold)
                        }
                        Spacer(modifier = Modifier.height(12.dp))
                        LazyRow(
                            horizontalArrangement = Arrangement.spacedBy(14.dp),
                            modifier = Modifier.fillMaxWidth(),
                        ) {
                            items(state.availableUsers) { user ->
                                val userAvatarUrl = resolveMediaUrl(
                                    serverBaseUrl,
                                    userAvatarPath(user.id, user.primaryImageTag),
                                    LocalMulletaFlixAccessToken.current,
                                )
                                ProfileSwitcherItem(
                                    user = user,
                                    avatarUrl = userAvatarUrl,
                                    onClick = viewModel::selectUserToSwitch,
                                )
                            }
                        }
                    }
                }
            }

            Spacer(modifier = Modifier.height(16.dp))

            // Actions (Clear Cache, Switch Server, Logout)
            Card(
                modifier = Modifier.fillMaxWidth(),
                shape = RoundedCornerShape(16.dp),
                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
            ) {
                Column(modifier = Modifier.padding(16.dp)) {
                    Text("Gerenciamento do Aplicativo", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.SemiBold)
                    Spacer(modifier = Modifier.height(8.dp))

                    ProfileOptionItem(
                        icon = Icons.Default.CleaningServices,
                        title = "Limpar Cache Local",
                        subtitle = "Armazenamento em cache: ${state.cacheSizeFormatted}",
                        onClick = { viewModel.clearCache() },
                    )

                    HorizontalDivider(modifier = Modifier.padding(vertical = 8.dp), color = MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.5f))

                    ProfileOptionItem(
                        icon = Icons.Default.SwapHoriz,
                        title = "Trocar de Servidor",
                        subtitle = "Conectar a outro servidor MulletaFlix",
                        onClick = onSwitchServer,
                    )

                    HorizontalDivider(modifier = Modifier.padding(vertical = 8.dp), color = MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.5f))

                    ProfileOptionItem(
                        icon = Icons.AutoMirrored.Filled.Logout,
                        title = "Sair da Conta",
                        subtitle = "Finaliza a sessão neste dispositivo",
                        tint = MaterialTheme.colorScheme.error,
                        onClick = { showLogoutConfirmDialog = true },
                    )
                }
            }

            Spacer(modifier = Modifier.height(32.dp))
        }
    }

    // Switch User Dialog
    if (state.isSwitchDialogOpen && state.selectedUserForSwitch != null) {
        val target = state.selectedUserForSwitch!!
        AlertDialog(
            onDismissRequest = { viewModel.selectUserToSwitch(null) },
            title = { Text("Entrar como ${target.name}") },
            text = {
                Column {
                    Text("Digite a senha do usuário caso o servidor exija:")
                    Spacer(modifier = Modifier.height(12.dp))
                    OutlinedTextField(
                        value = state.switchPasswordInput,
                        onValueChange = { viewModel.onSwitchPasswordChanged(it) },
                        label = { Text("Senha (opcional se livre)") },
                        visualTransformation = PasswordVisualTransformation(),
                        singleLine = true,
                        modifier = Modifier.fillMaxWidth(),
                    )
                }
            },
            confirmButton = {
                Button(
                    onClick = {
                        viewModel.confirmSwitchUser {
                            // User switched successfully
                        }
                    },
                    enabled = !state.isSwitchingUser,
                ) {
                    if (state.isSwitchingUser) {
                        CircularProgressIndicator(modifier = Modifier.size(18.dp), color = MaterialTheme.colorScheme.onPrimary)
                    } else {
                        Text("Alternar")
                    }
                }
            },
            dismissButton = {
                TextButton(onClick = { viewModel.selectUserToSwitch(null) }) {
                    Text("Cancelar")
                }
            }
        )
    }

    // Logout Confirmation Dialog
    if (showLogoutConfirmDialog) {
        AlertDialog(
            onDismissRequest = { showLogoutConfirmDialog = false },
            title = { Text("Encerrar Sessão") },
            text = { Text("Deseja realmente sair da sua conta MulletaFlix? Suas preferências salvas permanecerão intactas no servidor.") },
            confirmButton = {
                Button(
                    onClick = {
                        showLogoutConfirmDialog = false
                        viewModel.logout(onLogout)
                    },
                    colors = ButtonDefaults.buttonColors(containerColor = MaterialTheme.colorScheme.error)
                ) {
                    Text("Sair")
                }
            },
            dismissButton = {
                TextButton(onClick = { showLogoutConfirmDialog = false }) {
                    Text("Cancelar")
                }
            }
        )
    }
}

@Composable
internal fun ProfileSwitcherItem(
    user: AvailableUser,
    avatarUrl: String?,
    modifier: Modifier = Modifier,
    onClick: (AvailableUser) -> Unit,
) {
    Column(
        horizontalAlignment = Alignment.CenterHorizontally,
        modifier = modifier
            .clip(RoundedCornerShape(10.dp))
            .clickable(
                role = Role.Button,
                onClickLabel = "Alternar para ${user.name}",
                onClick = { onClick(user) },
            )
            .padding(4.dp),
    ) {
        Surface(
            shape = CircleShape,
            color = MaterialTheme.colorScheme.primaryContainer,
            modifier = Modifier.size(48.dp),
        ) {
            Box(contentAlignment = Alignment.Center) {
                Text(
                    text = user.name.firstOrNull()?.uppercase() ?: "U",
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.Bold,
                )
                if (avatarUrl != null) {
                    AsyncImage(
                        model = avatarUrl,
                        contentDescription = null,
                        contentScale = ContentScale.Crop,
                        modifier = Modifier.fillMaxSize().clip(CircleShape),
                    )
                }
            }
        }
        Spacer(modifier = Modifier.height(6.dp))
        Text(
            text = user.name,
            style = MaterialTheme.typography.bodySmall,
            maxLines = 1,
            overflow = TextOverflow.Ellipsis,
        )
    }
}

@Composable
internal fun ProfilePrivilegeRow(title: String, enabled: Boolean) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(vertical = 6.dp)
            // O visto verde e o X vermelho são a única indicação de que a conta pode
            // ou não fazer isto. Sem estado, o leitor de tela anunciava só o nome do
            // recurso ("Transmissão 4K HDR / Dolby Vision") e o usuário não descobria
            // se tinha a permissão — a cor não chega a quem não enxerga.
            .semantics { stateDescription = if (enabled) "Concedido" else "Negado" },
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically
    ) {
        Text(title, style = MaterialTheme.typography.bodyMedium)
        Icon(
            imageVector = if (enabled) Icons.Default.CheckCircle else Icons.Default.Cancel,
            contentDescription = null,
            tint = if (enabled) Color(0xFF4CAF50) else MaterialTheme.colorScheme.error,
            modifier = Modifier.size(20.dp)
        )
    }
}

@Composable
private fun ProfileOptionItem(
    icon: ImageVector,
    title: String,
    subtitle: String,
    tint: Color = MaterialTheme.colorScheme.onSurface,
    onClick: () -> Unit,
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(8.dp))
            .clickable(role = Role.Button, onClick = onClick)
            .padding(vertical = 8.dp, horizontal = 4.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Icon(imageVector = icon, contentDescription = null, tint = tint, modifier = Modifier.size(24.dp))
        Spacer(modifier = Modifier.width(14.dp))
        Column(modifier = Modifier.weight(1f)) {
            Text(text = title, style = MaterialTheme.typography.bodyLarge, fontWeight = FontWeight.Medium, color = tint)
            Text(
                text = subtitle,
                style = MaterialTheme.typography.bodySmall,
                color = readableTextOn(
                    MaterialTheme.colorScheme.onSurface.copy(alpha = 0.6f),
                    MaterialTheme.colorScheme.surface,
                ),
            )
        }
        Icon(
            Icons.Default.ChevronRight,
            contentDescription = null,
            tint = readableTextOn(
                foreground = MaterialTheme.colorScheme.onSurface.copy(alpha = 0.4f),
                background = MaterialTheme.colorScheme.surface,
                minimum = 3.0,
            ),
            modifier = Modifier.size(20.dp),
        )
    }
}
