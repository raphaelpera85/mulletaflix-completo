package org.mulletaflix.feature.itemdetail

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.selected
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp

@Composable
internal fun BookSpeechVoiceDialog(
    voices: List<BookSpeechVoiceOption>,
    selectedVoiceName: String?,
    onSelect: (String?) -> Unit,
    onDismiss: () -> Unit,
) {
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text(stringResource(R.string.book_reader_speech_voice)) },
        text = {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .heightIn(max = 360.dp)
                    .verticalScroll(rememberScrollState()),
            ) {
                VoiceOption(
                    label = stringResource(R.string.book_reader_speech_voice_default),
                    name = "default",
                    selected = selectedVoiceName == null,
                    networkRequired = false,
                    onClick = { onSelect(null) },
                )
                if (voices.isEmpty()) {
                    Text(
                        text = stringResource(R.string.book_reader_speech_voices_unavailable),
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        style = MaterialTheme.typography.bodySmall,
                    )
                }
                voices.forEach { voice ->
                    VoiceOption(
                        label = voice.displayName,
                        name = voice.name,
                        selected = voice.name == selectedVoiceName,
                        networkRequired = voice.networkRequired,
                        onClick = { onSelect(voice.name) },
                    )
                }
            }
        },
        confirmButton = {
            TextButton(onClick = onDismiss) {
                Text(stringResource(R.string.book_reader_speech_voice_close))
            }
        },
    )
}

@Composable
private fun VoiceOption(
    label: String,
    name: String,
    selected: Boolean,
    networkRequired: Boolean,
    onClick: () -> Unit,
) {
    TextButton(
        modifier = Modifier
            .fillMaxWidth()
            .testTag("speech-voice-$name")
            .semantics { this.selected = selected },
        onClick = onClick,
    ) {
        Column(modifier = Modifier.fillMaxWidth()) {
            Text(label)
            Text(
                text = stringResource(
                    if (networkRequired) R.string.book_reader_speech_voice_online
                    else R.string.book_reader_speech_voice_offline,
                ),
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                style = MaterialTheme.typography.bodySmall,
            )
        }
    }
}
