package org.mulletaflix.feature.syncplay

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun SyncPlayScreen(onJoinGroup: (String?) -> Unit = {}, onBack: () -> Unit = {}, viewModel: SyncPlayViewModel = hiltViewModel()) {
    val state by viewModel.state.collectAsStateWithLifecycle(); var showCreateDialog by remember { mutableStateOf(false) }
    LaunchedEffect(Unit) {
        while (isActive) {
            delay(5_000)
            viewModel.refresh()
        }
    }
    Scaffold(topBar = { TopAppBar(title = { Text("Salas SyncPlay") }, navigationIcon = { IconButton(onClick = onBack) { Icon(Icons.AutoMirrored.Filled.ArrowBack, "Voltar") } }, actions = { IconButton(onClick = viewModel::refresh, enabled = !state.isLoading) { Icon(Icons.Default.Refresh, "Atualizar salas") }; IconButton(onClick = { showCreateDialog = true }, enabled = !state.isSubmitting) { Icon(Icons.Default.Add, "Criar sala") } }) }) { padding ->
        LazyColumn(Modifier.fillMaxSize().padding(padding).background(MaterialTheme.colorScheme.background), contentPadding = PaddingValues(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
            item { Card(colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.primaryContainer), modifier = Modifier.fillMaxWidth()) { Row(Modifier.padding(16.dp), verticalAlignment = Alignment.CenterVertically) { Icon(Icons.Default.Group, null, tint = MaterialTheme.colorScheme.onPrimaryContainer); Spacer(Modifier.width(12.dp)); Column { Text("Assistir em grupo", style = MaterialTheme.typography.titleMedium, color = MaterialTheme.colorScheme.onPrimaryContainer); Text("Sincronize a reprodução em tempo real com seus amigos.", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onPrimaryContainer) } } } }
            state.error?.let { message -> item { Card(colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.errorContainer), modifier = Modifier.fillMaxWidth()) { Row(Modifier.padding(12.dp), verticalAlignment = Alignment.CenterVertically) { Text(message, Modifier.weight(1f), color = MaterialTheme.colorScheme.onErrorContainer); TextButton(onClick = viewModel::refresh) { Text("Tentar novamente") } } } } }
            if (state.isLoading && state.groups.isEmpty()) item { Box(Modifier.fillMaxWidth().padding(32.dp), contentAlignment = Alignment.Center) { CircularProgressIndicator() } }
            if (!state.isLoading && state.groups.isEmpty() && state.error == null) item { EmptyGroupsState { showCreateDialog = true } }
            items(state.groups, key = { it.groupId }) { group -> Card(Modifier.fillMaxWidth(), colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)) { Column(Modifier.padding(16.dp)) { Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) { Text(group.groupName, style = MaterialTheme.typography.titleMedium); AssistChip(onClick = {}, label = { Text(group.state ?: "Pronto") }) }; Text("Participantes: ${group.participants.joinToString(", ").ifBlank { "Ninguém ainda" }}", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant); Spacer(Modifier.height(12.dp)); Button(onClick = { viewModel.joinGroup(group.groupId) { joined -> onJoinGroup(joined?.playingItemId) } }, enabled = !state.isSubmitting, modifier = Modifier.fillMaxWidth()) { Icon(Icons.Default.PlayArrow, null); Spacer(Modifier.width(8.dp)); Text("Entrar na sessão") } } } }
            if (state.activeGroupId != null) item { OutlinedButton(onClick = viewModel::leaveGroup, enabled = !state.isSubmitting, modifier = Modifier.fillMaxWidth()) { Text("Sair da sala atual") } }
        }
    }
    if (showCreateDialog) { var name by remember { mutableStateOf("") }; AlertDialog(onDismissRequest = { if (!state.isSubmitting) showCreateDialog = false }, title = { Text("Criar sala SyncPlay") }, text = { OutlinedTextField(value = name, onValueChange = { name = it }, singleLine = true, label = { Text("Nome da sala") }, modifier = Modifier.fillMaxWidth()) }, dismissButton = { TextButton(onClick = { showCreateDialog = false }, enabled = !state.isSubmitting) { Text("Cancelar") } }, confirmButton = { Button(onClick = { viewModel.createGroup(name) { showCreateDialog = false } }, enabled = name.isNotBlank() && !state.isSubmitting) { Text("Criar") } }) }
}

@Composable
private fun EmptyGroupsState(onCreate: () -> Unit) { Column(Modifier.fillMaxWidth().padding(vertical = 32.dp), horizontalAlignment = Alignment.CenterHorizontally) { Icon(Icons.Default.GroupOff, null, modifier = Modifier.size(48.dp), tint = MaterialTheme.colorScheme.onSurfaceVariant); Spacer(Modifier.height(8.dp)); Text("Nenhuma sala disponível", style = MaterialTheme.typography.titleMedium); Text("Crie uma sala para começar a assistir em grupo.", color = MaterialTheme.colorScheme.onSurfaceVariant); Spacer(Modifier.height(12.dp)); Button(onClick = onCreate) { Icon(Icons.Default.Add, null); Spacer(Modifier.width(8.dp)); Text("Criar sala") } } }
