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
internal fun BookSpeechLanguageDialog(
    languages: List<BookSpeechLanguageOption>,
    selectedLanguageTag: String?,
    onSelect: (String?) -> Unit,
    onOpenVoices: () -> Unit,
    voicesLoading: Boolean,
    onDismiss: () -> Unit,
) {
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text(stringResource(R.string.book_reader_speech_language)) },
        text = {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .heightIn(max = 360.dp)
                    .verticalScroll(rememberScrollState()),
            ) {
                LanguageOption(
                    label = stringResource(R.string.book_reader_speech_language_default),
                    tag = "default",
                    selected = selectedLanguageTag == null,
                    onClick = { onSelect(null) },
                )
                if (languages.isEmpty()) {
                    Text(
                        text = stringResource(R.string.book_reader_speech_languages_unavailable),
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        style = MaterialTheme.typography.bodySmall,
                    )
                }
                languages.forEach { language ->
                    LanguageOption(
                        label = language.displayName,
                        tag = language.languageTag,
                        selected = language.languageTag == selectedLanguageTag,
                        onClick = { onSelect(language.languageTag) },
                    )
                }
                TextButton(
                    modifier = Modifier
                        .fillMaxWidth()
                        .testTag("speech-language-open-voices"),
                    enabled = !voicesLoading,
                    onClick = onOpenVoices,
                ) {
                    Text(stringResource(R.string.book_reader_speech_voice_open))
                }
            }
        },
        confirmButton = {
            TextButton(onClick = onDismiss) {
                Text(stringResource(R.string.book_reader_speech_language_close))
            }
        },
    )
}

@Composable
private fun LanguageOption(
    label: String,
    tag: String,
    selected: Boolean,
    onClick: () -> Unit,
) {
    TextButton(
        modifier = Modifier
            .fillMaxWidth()
            .testTag("speech-language-$tag")
            .semantics { this.selected = selected },
        onClick = onClick,
    ) {
        Text(label, modifier = Modifier.fillMaxWidth())
    }
}
