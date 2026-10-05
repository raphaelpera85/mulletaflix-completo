package org.mulletaflix.feature.player

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.selection.selectableGroup
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.RadioButton
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import org.mulletaflix.domain.model.SUBTITLE_BACKGROUND_BLACK_50
import org.mulletaflix.domain.model.SUBTITLE_BACKGROUND_BLACK_80
import org.mulletaflix.domain.model.SUBTITLE_BACKGROUND_NONE
import org.mulletaflix.domain.model.SUBTITLE_COLOR_CYAN
import org.mulletaflix.domain.model.SUBTITLE_COLOR_WHITE
import org.mulletaflix.domain.model.SUBTITLE_COLOR_YELLOW

private val subtitleAppearanceSizes = listOf(50, 75, 100, 125, 150, 200)
private val subtitleAppearanceColors = listOf(
    "Branco" to SUBTITLE_COLOR_WHITE,
    "Amarelo" to SUBTITLE_COLOR_YELLOW,
    "Ciano" to SUBTITLE_COLOR_CYAN,
)
private val subtitleAppearanceBackgrounds = listOf(
    "Sem fundo" to SUBTITLE_BACKGROUND_NONE,
    "Preto 50%" to SUBTITLE_BACKGROUND_BLACK_50,
    "Preto 80%" to SUBTITLE_BACKGROUND_BLACK_80,
)

/** Appearance controls stay beside playback, so users can see each change immediately. */
@Composable
internal fun PlayerSubtitleAppearanceMenu(
    state: PlayerState,
    onSubtitleFontSizeSelect: (Int) -> Unit,
    onSubtitleColorSelect: (String) -> Unit,
    onSubtitleBackgroundSelect: (String) -> Unit,
    onDismiss: () -> Unit,
) {
    val useColumns = androidx.compose.ui.platform.LocalConfiguration.current.screenWidthDp >= 600
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Aparência das legendas") },
        text = {
            if (useColumns) {
                Row(
                    modifier = Modifier.fillMaxWidth().heightIn(max = 380.dp),
                    horizontalArrangement = Arrangement.spacedBy(8.dp),
                ) {
                    SubtitleAppearanceGroup(modifier = Modifier.weight(1f), title = "Tamanho") {
                        subtitleAppearanceSizes.forEach { size ->
                            SubtitleAppearanceOption(
                                label = "$size%",
                                selected = state.subtitleFontSize == size,
                                onClick = { onSubtitleFontSizeSelect(size) },
                            )
                        }
                    }
                    SubtitleAppearanceGroup(modifier = Modifier.weight(1f), title = "Cor") {
                        subtitleAppearanceColors.forEach { (label, code) ->
                            SubtitleAppearanceOption(
                                label = label,
                                selected = state.subtitleColor == code,
                                onClick = { onSubtitleColorSelect(code) },
                            )
                        }
                    }
                    SubtitleAppearanceGroup(modifier = Modifier.weight(1f), title = "Fundo") {
                        subtitleAppearanceBackgrounds.forEach { (label, code) ->
                            SubtitleAppearanceOption(
                                label = label,
                                selected = state.subtitleBackground == code,
                                onClick = { onSubtitleBackgroundSelect(code) },
                            )
                        }
                    }
                }
            } else {
                Column(
                    modifier = Modifier
                        .fillMaxWidth()
                        .heightIn(max = 380.dp)
                        .verticalScroll(rememberScrollState()),
                ) {
                    SubtitleAppearanceGroup(title = "Tamanho") {
                        subtitleAppearanceSizes.forEach { size ->
                            SubtitleAppearanceOption(
                                label = "$size%",
                                selected = state.subtitleFontSize == size,
                                onClick = { onSubtitleFontSizeSelect(size) },
                            )
                        }
                    }
                    SubtitleAppearanceGroup(title = "Cor") {
                        subtitleAppearanceColors.forEach { (label, code) ->
                            SubtitleAppearanceOption(
                                label = label,
                                selected = state.subtitleColor == code,
                                onClick = { onSubtitleColorSelect(code) },
                            )
                        }
                    }
                    SubtitleAppearanceGroup(title = "Fundo") {
                        subtitleAppearanceBackgrounds.forEach { (label, code) ->
                            SubtitleAppearanceOption(
                                label = label,
                                selected = state.subtitleBackground == code,
                                onClick = { onSubtitleBackgroundSelect(code) },
                            )
                        }
                    }
                }
            }
        },
        confirmButton = {
            TextButton(onClick = onDismiss) { Text("Fechar") }
        },
    )
}

@Composable
private fun SubtitleAppearanceGroup(
    title: String,
    modifier: Modifier = Modifier,
    content: @Composable () -> Unit,
) {
    Column(modifier = modifier.selectableGroup()) {
        Text(title, modifier = Modifier.padding(start = 8.dp, top = 8.dp, bottom = 4.dp))
        content()
    }
}

@Composable
private fun SubtitleAppearanceOption(
    label: String,
    selected: Boolean,
    onClick: () -> Unit,
) {
    PlayerOptionRow(
        selected = selected,
        onClick = onClick,
        verticalAlignment = Alignment.CenterVertically,
    ) {
        RadioButton(selected = selected, onClick = null)
        Text(label, modifier = Modifier.padding(start = 8.dp))
    }
}
