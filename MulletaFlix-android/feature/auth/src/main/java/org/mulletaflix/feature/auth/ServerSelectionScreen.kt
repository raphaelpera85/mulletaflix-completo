package org.mulletaflix.feature.auth

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.foundation.*
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import android.content.pm.PackageManager
import android.widget.Toast
import com.google.mlkit.vision.codescanner.GmsBarcodeScanning
import org.mulletaflix.designsystem.components.MulletaFlixWordmark
import org.mulletaflix.designsystem.components.MulletaFlixTopBarAction
import org.mulletaflix.designsystem.theme.readableTextOn

/**
 * Server selection screen — shown before login.
 *
 * Features:
 *  - List of previously connected servers
 *  - Auto-discovery of servers on local network
 *  - Manual URL entry with validation
 *  - Connection test with health check
 */
@Composable
fun ServerSelectionScreen(
    onServerSelected: () -> Unit,
    switchingServer: Boolean = false,
    viewModel: AuthViewModel = hiltViewModel()
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    val context = LocalContext.current
    val hasCamera = remember {
        context.packageManager.hasSystemFeature(PackageManager.FEATURE_CAMERA_ANY)
    }
    var manualUrl by remember { mutableStateOf(DEFAULT_MULLETAFLIX_SERVER_URL) }
    var manuallyEdited by remember { mutableStateOf(false) }
    var automaticConnectionStarted by remember { mutableStateOf(false) }
    var isScanningQr by remember { mutableStateOf(false) }

    // A discovered LAN server has priority over the public fallback. Do not
    // overwrite an address while the user is actively editing the field.
    LaunchedEffect(state.serverUrl) {
        if (!manuallyEdited && !state.serverUrl.isNullOrBlank()) {
            manualUrl = state.serverUrl.orEmpty()
        }
    }

    LaunchedEffect(state.isAuthenticated, switchingServer) {
        if (shouldAutoAdvanceAuthScreen(state.isAuthenticated, switchingServer)) onServerSelected()
    }

    // Verify LAN first; when discovery finds nothing, verify the saved/public
    // endpoint as a fallback. The callback only advances to login; credentials
    // are still required by the user.
    LaunchedEffect(state.isDiscovering, state.discoveredServers, state.serverUrl, manuallyEdited) {
        automaticServerCandidate(state, manuallyEdited, automaticConnectionStarted)?.let { endpoint ->
            automaticConnectionStarted = true
            viewModel.connectToServer(
                url = endpoint,
                onSuccess = { onServerSelected() },
                onFailure = {
                    // A stale LAN advertisement must not prevent access through
                    // the saved/public endpoint.
                    if (state.discoveredServers.firstOrNull()?.url == endpoint) {
                        viewModel.connectToServer(
                            fallbackServerCandidate(state, endpoint),
                            onSuccess = { onServerSelected() },
                        )
                    }
                },
            )
        }
    }

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(Brush.verticalGradient(colors = listOf(AuthBackdropTop, AuthBackdropBottom)))
    ) {
        Column(
            modifier = Modifier.fillMaxSize().padding(24.dp),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            Spacer(modifier = Modifier.height(48.dp))

            // Logo
            Icon(
                painter = androidx.compose.ui.res.painterResource(org.mulletaflix.designsystem.R.drawable.ic_mulletaflix_logo),
                contentDescription = "MulletaFlix",
                tint = Color.Unspecified,
                modifier = Modifier.size(72.dp)
            )
            MulletaFlixWordmark(
                style = MaterialTheme.typography.headlineMedium,
                fontWeight = FontWeight.Bold,
                modifier = Modifier.padding(top = 12.dp),
            )
            Text(
                "Player conectado ao servidor remoto",
                style = MaterialTheme.typography.bodySmall,
                color = readableTextOn(Color.White.copy(0.5f), AuthBackdropTop),
                modifier = Modifier.padding(bottom = 32.dp),
            )

            // Manual URL entry
            OutlinedTextField(
                value = manualUrl,
                onValueChange = { manuallyEdited = true; manualUrl = it },
                label = { Text("URL do Servidor") },
                leadingIcon = { Icon(Icons.Default.Dns, contentDescription = null) },
                placeholder = { Text(DEFAULT_MULLETAFLIX_SERVER_URL) },
                singleLine = true,
                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Uri),
                modifier = Modifier.fillMaxWidth(),
                colors = OutlinedTextFieldDefaults.colors(
                    focusedBorderColor = MaterialTheme.colorScheme.secondary,
                    cursorColor = MaterialTheme.colorScheme.secondary,
                )
            )

            Spacer(modifier = Modifier.height(12.dp))

            Button(
                onClick = { viewModel.connectToServer(manualUrl, onSuccess = { onServerSelected() }) },
                enabled = !state.isLoading && manualUrl.length > 10,
                modifier = Modifier.fillMaxWidth().height(52.dp),
                shape = RoundedCornerShape(12.dp)
            ) {
                if (state.isLoading) {
                    CircularProgressIndicator(modifier = Modifier.size(20.dp), color = Color.White, strokeWidth = 2.dp)
                } else {
                    Icon(Icons.Default.Link, contentDescription = null, modifier = Modifier.padding(end = 8.dp))
                    Text("Conectar")
                }
            }

            if (hasCamera) {
                OutlinedButton(
                    onClick = {
                        isScanningQr = true
                        GmsBarcodeScanning.getClient(context).startScan()
                            .addOnSuccessListener { barcode ->
                                val scannedUrl = serverUrlFromQrPayload(barcode.rawValue)
                                if (scannedUrl != null) {
                                    manualUrl = scannedUrl
                                    manuallyEdited = true
                                    Toast.makeText(context, "URL do servidor preenchida", Toast.LENGTH_SHORT).show()
                                } else {
                                    Toast.makeText(context, "QR inválido: informe uma URL HTTP ou HTTPS", Toast.LENGTH_LONG).show()
                                }
                            }
                            .addOnFailureListener {
                                Toast.makeText(context, "Não foi possível ler o QR", Toast.LENGTH_SHORT).show()
                            }
                            .addOnCompleteListener { isScanningQr = false }
                    },
                    enabled = !state.isLoading && !isScanningQr,
                    modifier = Modifier.fillMaxWidth().height(48.dp),
                    shape = RoundedCornerShape(12.dp),
                    colors = ButtonDefaults.outlinedButtonColors(contentColor = MaterialTheme.colorScheme.secondary),
                ) {
                    if (isScanningQr) {
                        CircularProgressIndicator(modifier = Modifier.size(18.dp), strokeWidth = 2.dp)
                    } else {
                        Icon(Icons.Default.QrCodeScanner, contentDescription = null)
                    }
                    Spacer(Modifier.width(8.dp))
                    Text(if (isScanningQr) "Lendo QR…" else "Ler QR do servidor")
                }
            }

            state.error?.let {
                Text(it, color = MaterialTheme.colorScheme.error, style = MaterialTheme.typography.bodySmall, modifier = Modifier.padding(top = 8.dp))
            }

            OutlinedButton(
                onClick = {
                    // A manual refresh starts a new automatic selection cycle.
                    automaticConnectionStarted = false
                    viewModel.discoverLocalServers()
                },
                enabled = !state.isDiscovering && !state.isLoading,
                modifier = Modifier.fillMaxWidth().height(48.dp),
                shape = RoundedCornerShape(12.dp),
                border = BorderStroke(1.dp, MaterialTheme.colorScheme.secondary.copy(alpha = 0.7f)),
                colors = ButtonDefaults.outlinedButtonColors(contentColor = MaterialTheme.colorScheme.secondary),
            ) {
                if (state.isDiscovering) {
                    CircularProgressIndicator(modifier = Modifier.size(18.dp), strokeWidth = 2.dp)
                } else {
                    Icon(Icons.Default.Refresh, contentDescription = null)
                }
                Spacer(modifier = Modifier.width(8.dp))
                Text(if (state.isDiscovering) "Procurando na rede…" else "Procurar na rede")
            }

            Spacer(modifier = Modifier.height(8.dp))

            OutlinedButton(
                onClick = {
                    viewModel.connectToServer(DEFAULT_MULLETAFLIX_SERVER_URL, onSuccess = { onServerSelected() })
                },
                enabled = !state.isLoading,
                modifier = Modifier.fillMaxWidth().height(48.dp),
                shape = RoundedCornerShape(12.dp),
                border = BorderStroke(1.dp, MaterialTheme.colorScheme.secondary.copy(alpha = 0.8f)),
                colors = ButtonDefaults.outlinedButtonColors(contentColor = MaterialTheme.colorScheme.secondary),
            ) {
                Icon(Icons.Default.Cloud, contentDescription = null)
                Spacer(modifier = Modifier.width(8.dp))
                Text("Conectar ao Servidor Oficial (Nuvem)")
            }

            // Servers found automatically or saved from previous connections.
            if (state.discoveredServers.isNotEmpty() || state.savedServers.isNotEmpty()) {
                LazyColumn(
                    modifier = Modifier.weight(1f).fillMaxWidth(),
                    contentPadding = PaddingValues(top = 12.dp, bottom = 16.dp),
                    verticalArrangement = Arrangement.spacedBy(4.dp),
                ) {
                    if (state.discoveredServers.isNotEmpty()) {
                        item {
                            Text("Encontrados nesta rede", style = MaterialTheme.typography.titleSmall, color = MaterialTheme.colorScheme.secondary, modifier = Modifier.padding(vertical = 8.dp))
                        }
                        items(state.discoveredServers, key = { it.url }) { server ->
                            SavedServerCard(
                                name = server.name,
                                url = server.url,
                                latencyMs = server.latencyMs,
                                version = server.version,
                                onClick = { viewModel.connectToServer(server.url, onSuccess = { onServerSelected() }) },
                                onRemove = {},
                                showRemove = false,
                                origin = ServerCardOrigin.Discovered,
                            )
                        }
                    }
                    if (state.savedServers.isNotEmpty()) {
                        item {
                            Text("Servidores salvos / disponíveis", style = MaterialTheme.typography.titleSmall, color = Color.White.copy(0.7f), modifier = Modifier.padding(vertical = 8.dp))
                        }
                        items(state.savedServers, key = { it.url }) { server ->
                            SavedServerCard(
                                name = server.name,
                                url = server.url,
                                latencyMs = server.latencyMs,
                                version = server.version,
                                onClick = { viewModel.connectToServer(server.url, onSuccess = { onServerSelected() }) },
                                onRemove = { viewModel.removeServer(server.url) },
                                showRemove = server.url.trimEnd('/') != DEFAULT_MULLETAFLIX_SERVER_URL.trimEnd('/'),
                            )
                        }
                    }
                }
            }
        }
    }
}

/** De onde o cartão de servidor veio; decide o nome anunciado pelo ícone. */
internal enum class ServerCardOrigin { Saved, Discovered }

/**
 * O nome acessível do ícone do cartão.
 *
 * Um servidor apenas descoberto não é "salvo": ele aparece sob "Encontrados nesta
 * rede", e o nome precisa dizer o mesmo que a seção.
 */
internal fun serverCardIconDescription(isOfficial: Boolean, origin: ServerCardOrigin): String = when {
    isOfficial -> "Servidor oficial na nuvem"
    origin == ServerCardOrigin.Discovered -> "Servidor encontrado nesta rede"
    else -> "Servidor salvo"
}

@Composable
private fun SavedServerCard(
    name: String,
    url: String,
    latencyMs: Long? = null,
    version: String?,
    onClick: () -> Unit,
    onRemove: () -> Unit,
    showRemove: Boolean = true,
    /**
     * De onde o cartão veio: um servidor guardado ou um encontrado na rede agora.
     *
     * O cartão era reusado para os dois casos com o mesmo rótulo de ícone, então um
     * servidor apenas **descoberto** era anunciado como "Servidor salvo" logo abaixo
     * do título "Encontrados nesta rede" — o nome contradizia a seção.
     */
    origin: ServerCardOrigin = ServerCardOrigin.Saved,
) {
    val isOfficial = url.trimEnd('/') == DEFAULT_MULLETAFLIX_SERVER_URL.trimEnd('/')
    Card(
        modifier = Modifier.fillMaxWidth().padding(vertical = 4.dp).clickable(role = Role.Button, onClick = onClick),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
    ) {
        Row(
            modifier = Modifier.fillMaxWidth().padding(16.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Icon(
                if (isOfficial) Icons.Default.Cloud else Icons.Default.Storage,
                contentDescription = serverCardIconDescription(isOfficial, origin),
                tint = MaterialTheme.colorScheme.secondary
            )
            Column(modifier = Modifier.weight(1f).padding(horizontal = 12.dp)) {
                Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text(name, style = MaterialTheme.typography.titleSmall, color = Color.White)
                    if (isOfficial) {
                        Surface(
                            shape = RoundedCornerShape(4.dp),
                            color = MaterialTheme.colorScheme.secondary.copy(alpha = 0.2f),
                            border = BorderStroke(1.dp, MaterialTheme.colorScheme.secondary.copy(alpha = 0.6f))
                        ) {
                            Text(
                                "OFICIAL",
                                style = MaterialTheme.typography.labelSmall,
                                color = MaterialTheme.colorScheme.secondary,
                                modifier = Modifier.padding(horizontal = 6.dp, vertical = 2.dp)
                            )
                        }
                    }
                }
                Text(url, style = MaterialTheme.typography.bodySmall, color = Color.White.copy(0.5f))
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    version?.let { Text("v$it", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.secondary) }
                    latencyMs?.let { Text("${it} ms", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.secondary) }
                }
            }
            if (showRemove && !isOfficial) {
                MulletaFlixTopBarAction(onClick = onRemove) {
                    Icon(Icons.Default.Close, contentDescription = "Remover servidor", tint = Color.White.copy(0.5f))
                }
            }
        }
    }
}
