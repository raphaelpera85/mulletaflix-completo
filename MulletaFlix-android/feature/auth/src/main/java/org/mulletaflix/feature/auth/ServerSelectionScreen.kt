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
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel

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
    viewModel: AuthViewModel = hiltViewModel()
) {
    val state by viewModel.state.collectAsState()

    LaunchedEffect(state.isAuthenticated) {
        if (state.isAuthenticated) onServerSelected()
    }

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(Brush.verticalGradient(colors = listOf(Color(0xFF0A1628), Color(0xFF101010))))
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
                tint = Color(0xFF00A4DC),
                modifier = Modifier.size(72.dp)
            )
            Text("MulletaFlix", style = MaterialTheme.typography.headlineMedium, color = Color.White, fontWeight = FontWeight.Bold, modifier = Modifier.padding(top = 12.dp))
            Text("The Free Software Media System", style = MaterialTheme.typography.bodySmall, color = Color.White.copy(0.5f), modifier = Modifier.padding(bottom = 32.dp))

            // Manual URL entry
            var manualUrl by remember { mutableStateOf("http://") }
            OutlinedTextField(
                value = manualUrl,
                onValueChange = { manualUrl = it },
                label = { Text("URL do Servidor") },
                leadingIcon = { Icon(Icons.Default.Dns, contentDescription = null) },
                placeholder = { Text("http://192.168.1.100:8096") },
                singleLine = true,
                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Uri),
                modifier = Modifier.fillMaxWidth(),
                colors = OutlinedTextFieldDefaults.colors(
                    focusedBorderColor = Color(0xFF00A4DC),
                    cursorColor = Color(0xFF00A4DC),
                )
            )

            Spacer(modifier = Modifier.height(12.dp))

            Button(
                onClick = { viewModel.connectToServer(manualUrl) { onServerSelected() } },
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

            state.error?.let {
                Text(it, color = MaterialTheme.colorScheme.error, style = MaterialTheme.typography.bodySmall, modifier = Modifier.padding(top = 8.dp))
            }

            // Saved servers
            if (state.savedServers.isNotEmpty()) {
                Text("Servidores Salvos", style = MaterialTheme.typography.titleSmall, color = Color.White.copy(0.7f), modifier = Modifier.padding(vertical = 16.dp).align(Alignment.Start))
                LazyColumn(modifier = Modifier.weight(1f)) {
                    items(state.savedServers) { server ->
                        SavedServerCard(
                            name = server.name,
                            url = server.url,
                            version = server.version,
                            onClick = { viewModel.connectToServer(server.url) { onServerSelected() } },
                            onRemove = { viewModel.removeServer(server.url) }
                        )
                    }
                }
            }
        }
    }
}

@Composable
private fun SavedServerCard(name: String, url: String, version: String?, onClick: () -> Unit, onRemove: () -> Unit) {
    Card(
        modifier = Modifier.fillMaxWidth().padding(vertical = 4.dp).clickable(onClick = onClick),
        colors = CardDefaults.cardColors(containerColor = Color(0xFF1A1A1A))
    ) {
        Row(
            modifier = Modifier.fillMaxWidth().padding(16.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Icon(Icons.Default.Storage, contentDescription = null, tint = Color(0xFF00A4DC))
            Column(modifier = Modifier.weight(1f).padding(horizontal = 12.dp)) {
                Text(name, style = MaterialTheme.typography.titleSmall, color = Color.White)
                Text(url, style = MaterialTheme.typography.bodySmall, color = Color.White.copy(0.5f))
                version?.let { Text("v$it", style = MaterialTheme.typography.labelSmall, color = Color(0xFF00A4DC)) }
            }
            IconButton(onClick = onRemove) {
                Icon(Icons.Default.Close, contentDescription = "Remover servidor", tint = Color.White.copy(0.5f))
            }
        }
    }
}
