package org.mulletaflix.feature.player

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
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
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.unit.dp

internal val PLAYBACK_ISSUE_CATEGORIES = listOf(
    "Não reproduz",
    "Travamentos",
    "Sem áudio",
    "Áudio/legenda",
    "Qualidade",
    "Outro",
)

@Composable
internal fun PlayerIssueReportDialog(
    initialDescription: String?,
    isSubmitting: Boolean,
    isSent: Boolean,
    isQueued: Boolean = false,
    message: String?,
    onDismiss: () -> Unit,
    onSubmit: (category: String, description: String?) -> Unit,
) {
    var category by remember { mutableStateOf(PLAYBACK_ISSUE_CATEGORIES.first()) }
    var description by remember(initialDescription) { mutableStateOf(initialDescription.orEmpty().take(1_000)) }
    var categoryMenuExpanded by remember { mutableStateOf(false) }
    val screenHeightDp = LocalConfiguration.current.screenHeightDp
    val dialogContentMaxHeight = (screenHeightDp - 220).coerceIn(180, 440).dp

    AlertDialog(
        onDismissRequest = { if (!isSubmitting) onDismiss() },
        title = { Text("Reportar problema") },
        text = {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .heightIn(max = dialogContentMaxHeight)
                    .verticalScroll(rememberScrollState())
                    .testTag("playback-issue-content"),
                verticalArrangement = Arrangement.spacedBy(12.dp),
            ) {
                Box {
                    OutlinedButton(
                        onClick = { categoryMenuExpanded = true },
                        enabled = !isSubmitting && !isSent && !isQueued,
                        modifier = Modifier.fillMaxWidth(),
                    ) { Text(category) }
                    DropdownMenu(
                        expanded = categoryMenuExpanded,
                        onDismissRequest = { categoryMenuExpanded = false },
                    ) {
                        PLAYBACK_ISSUE_CATEGORIES.forEach { option ->
                            DropdownMenuItem(
                                text = { Text(option) },
                                onClick = {
                                    category = option
                                    categoryMenuExpanded = false
                                },
                            )
                        }
                    }
                }
                OutlinedTextField(
                    value = description,
                    onValueChange = { description = it.take(1_000) },
                    label = { Text("Descreva o problema (opcional)") },
                    minLines = 2,
                    maxLines = 4,
                    enabled = !isSubmitting && !isSent && !isQueued,
                )
                message?.let {
                    Text(
                        text = it,
                        color = if (isSent || isQueued) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.error,
                        style = MaterialTheme.typography.bodySmall,
                    )
                }
            }
        },
        confirmButton = {
            TextButton(
                enabled = !isSubmitting && !isSent && !isQueued,
                onClick = { onSubmit(category, description.trim().ifBlank { null }) },
            ) {
                if (isSubmitting) CircularProgressIndicator(Modifier.padding(end = 8.dp), strokeWidth = 2.dp)
                Text(when { isQueued -> "Na fila"; isSent -> "Enviado"; else -> "Enviar" })
            }
        },
        dismissButton = {
            TextButton(enabled = !isSubmitting, onClick = onDismiss) { Text("Fechar") }
        },
    )
}
