package org.mulletaflix.feature.itemdetail

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.unit.dp
import androidx.compose.foundation.layout.size
import kotlinx.coroutines.CancellationException

@Composable
internal fun PlaybackIssueReportDialog(
    hasFeedbackSession: Boolean,
    feedbackSessionLoaded: Boolean,
    onDismiss: () -> Unit,
    onSubmit: (category: String, description: String, complete: (Result<Unit>) -> Unit) -> Unit,
    onQueue: (category: String, description: String, complete: (Result<Unit>) -> Unit) -> Unit,
) {
    var category by remember { mutableStateOf("Não reproduz") }
    var description by remember { mutableStateOf("") }
    var submitting by remember { mutableStateOf(false) }
    var errorMessage by remember { mutableStateOf<String?>(null) }
    var categoryMenuExpanded by remember { mutableStateOf(false) }

    fun submit(action: (String, String, (Result<Unit>) -> Unit) -> Unit) {
        if (submitting || !hasFeedbackSession) return
        submitting = true
        errorMessage = null
        action(category, description) { result ->
            submitting = false
            result.onSuccess {
                onDismiss()
            }.onFailure { failure ->
                errorMessage = if (failure is CancellationException) null else
                    failure.localizedMessage ?: "Não foi possível enviar o relato. Tente novamente."
            }
        }
    }

    AlertDialog(
        onDismissRequest = { if (!submitting) onDismiss() },
        title = { Text("Reportar problema") },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                Box {
                    OutlinedButton(onClick = { categoryMenuExpanded = true }, enabled = !submitting) {
                        Text(category)
                    }
                    DropdownMenu(
                        expanded = categoryMenuExpanded,
                        onDismissRequest = { categoryMenuExpanded = false },
                    ) {
                        listOf("Não reproduz", "Travamentos", "Sem áudio", "Áudio/legenda", "Qualidade", "Outro")
                            .forEach { option ->
                                DropdownMenuItem(
                                    text = { Text(option) },
                                    onClick = { category = option; categoryMenuExpanded = false },
                                )
                            }
                    }
                }
                OutlinedTextField(
                    value = description,
                    onValueChange = { description = it.take(1_000) },
                    label = { Text("Descreva o problema (opcional)") },
                    minLines = 2,
                    enabled = !submitting,
                )
                if (!hasFeedbackSession) {
                    Text(
                        text = if (feedbackSessionLoaded) "Conecte-se ao servidor para enviar." else "Verificando sessão…",
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        style = MaterialTheme.typography.bodySmall,
                    )
                }
                errorMessage?.let { Text(it, color = MaterialTheme.colorScheme.error) }
                if (errorMessage != null && hasFeedbackSession) {
                    TextButton(
                        modifier = Modifier.testTag("playback-issue-queue"),
                        enabled = !submitting,
                        onClick = { submit(onQueue) },
                    ) {
                        Text("Salvar para enviar depois")
                    }
                }
            }
        },
        confirmButton = {
            TextButton(enabled = hasFeedbackSession && !submitting, onClick = { submit(onSubmit) }) {
                if (submitting) CircularProgressIndicator(modifier = Modifier.size(18.dp), strokeWidth = 2.dp)
                else Text("Enviar")
            }
        },
        dismissButton = {
            TextButton(enabled = !submitting, onClick = onDismiss) { Text("Cancelar") }
        },
    )
}
