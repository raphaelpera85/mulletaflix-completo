package org.mulletaflix.feature.itemdetail

import android.content.res.Configuration

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.aspectRatio
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.itemsIndexed
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.PlayArrow
import androidx.compose.material.icons.filled.Download
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.semantics.selected
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.LiveRegionMode
import androidx.compose.ui.semantics.ProgressBarRangeInfo
import androidx.compose.ui.semantics.liveRegion
import androidx.compose.ui.semantics.progressBarRangeInfo
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import coil.compose.AsyncImage
import org.mulletaflix.designsystem.media.LocalMulletaFlixAccessToken
import org.mulletaflix.designsystem.media.LocalMulletaFlixServerUrl
import org.mulletaflix.designsystem.media.resolveMediaUrl
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.primaryImageUrl
import org.mulletaflix.domain.model.playbackProgressFraction
import org.mulletaflix.designsystem.components.MulletaFlixTopBarAction

/**
 * Season selector + episode list used by the item detail screen.
 *
 * Kept in its own file so the empty/loading behaviour can be covered by an
 * instrumented regression test (see SeriesSectionTest).
 */
@Composable
fun SeriesSection(
    seasons: List<MediaItem>,
    episodes: List<MediaItem>,
    selectedSeasonIndex: Int,
    onSeasonSelect: (Int) -> Unit,
    onEpisodePlay: (String) -> Unit,
    onEpisodeClick: (String) -> Unit,
    isLoading: Boolean = false,
    error: String? = null,
    onRetry: () -> Unit = {},
    seasonDownloadProgress: SeasonDownloadProgress? = null,
    onDownloadSeason: () -> Long? = { null },
    onCancelSeasonDownload: () -> Unit = {},
) {
    val configuration = LocalConfiguration.current
    val isTelevision =
        (configuration.uiMode and Configuration.UI_MODE_TYPE_MASK) == Configuration.UI_MODE_TYPE_TELEVISION
    val cancelFocusRequester = remember { FocusRequester() }

    Column(modifier = Modifier.padding(vertical = 8.dp)) {
        // Season covers, like the web client's season row. The previous tab row
        // indexed its tab positions, so measuring it with zero tabs threw
        // `IndexOutOfBoundsException: Index 0 out of bounds for length 0` and
        // killed the app (the seasons request is always in flight on the first
        // frame of a series). It is only drawn when seasons exist.
        if (seasons.isNotEmpty()) {
            SeasonCoverRow(
                seasons = seasons,
                selectedSeasonIndex = selectedSeasonIndex.coerceIn(0, seasons.lastIndex),
                onSeasonSelect = onSeasonSelect,
            )

            val selectedSeason = seasons.getOrNull(selectedSeasonIndex)
            selectedSeason?.let { season ->
                // The ViewModel permits one preparation at a time. Keep that operation
                // visible if the viewer switches seasons; otherwise the selected season
                // looks idle even though its download action cannot start another batch.
                val progress = seasonDownloadProgress?.takeIf { it.isRunning || it.seasonId == season.id }
                val wasDownloadRunning = remember(season.id) {
                    mutableStateOf(progress?.isRunning == true)
                }
                val downloadRequestedOperationId = remember { mutableStateOf<Long?>(null) }
                LaunchedEffect(
                    isTelevision,
                    progress?.seasonId,
                    progress?.isRunning,
                    progress?.operationId,
                    downloadRequestedOperationId.value,
                ) {
                    val isRunning = progress?.isRunning == true
                    val isConfirmedLocalStart = downloadRequestedOperationId.value != null &&
                        downloadRequestedOperationId.value == progress?.operationId
                    if (isTelevision && isRunning && isConfirmedLocalStart && !wasDownloadRunning.value) {
                        // The primary action becomes disabled during preparation. Move TV
                        // focus to Cancel only for a request initiated on this screen, so
                        // external progress and re-entering an active screen do not steal focus.
                        cancelFocusRequester.requestFocus()
                    }
                    if (downloadRequestedOperationId.value != null && progress != null &&
                        (!isConfirmedLocalStart || !isRunning)
                    ) {
                        downloadRequestedOperationId.value = null
                    } else if (isRunning && isConfirmedLocalStart) {
                        downloadRequestedOperationId.value = null
                    }
                    wasDownloadRunning.value = isRunning
                }
                if (!isTelevision || progress?.isRunning == true) Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(horizontal = 16.dp, vertical = 8.dp)
                        .then(
                            if (progress?.isRunning == true) {
                                Modifier.semantics {
                                    liveRegion = LiveRegionMode.Polite
                                    progressBarRangeInfo = ProgressBarRangeInfo(
                                        progress.processedEpisodes.toFloat(),
                                        0f..progress.totalEpisodes.toFloat(),
                                    )
                                }
                            } else Modifier,
                        ),
                    horizontalArrangement = Arrangement.spacedBy(8.dp),
                    verticalAlignment = Alignment.CenterVertically,
                ) {
                    if (isTelevision && progress?.isRunning == true) {
                        Text(
                            text = "Preparando ${progress.processedEpisodes}/${progress.totalEpisodes}",
                            modifier = Modifier.weight(1f),
                            maxLines = 1,
                            overflow = TextOverflow.Ellipsis,
                        )
                    }
                    if (!isTelevision) {
                        Button(
                            onClick = {
                                downloadRequestedOperationId.value = onDownloadSeason()
                            },
                            enabled = episodes.isNotEmpty() && progress?.isRunning != true,
                            modifier = Modifier.weight(1f),
                            colors = ButtonDefaults.buttonColors(
                                containerColor = MaterialTheme.colorScheme.secondary,
                                contentColor = MaterialTheme.colorScheme.onSecondary,
                            ),
                        ) {
                            if (progress?.isRunning == true) {
                                CircularProgressIndicator(modifier = Modifier.size(18.dp), strokeWidth = 2.dp)
                            } else {
                                Icon(Icons.Default.Download, contentDescription = null)
                            }
                            Text(
                                text = if (progress?.isRunning == true) {
                                    "Preparando ${progress.processedEpisodes}/${progress.totalEpisodes}"
                                } else {
                                    "Baixar temporada"
                                },
                                modifier = Modifier.padding(start = 8.dp),
                                maxLines = 1,
                                overflow = TextOverflow.Ellipsis,
                            )
                        }
                    }
                    if (progress?.isRunning == true) {
                        TextButton(
                            onClick = onCancelSeasonDownload,
                            modifier = if (isTelevision) Modifier.focusRequester(cancelFocusRequester) else Modifier,
                        ) { Text("Cancelar") }
                    }
                }
                if (progress?.isRunning == true && progress.seasonId != season.id) {
                    Text(
                        text = "Preparação em andamento para ${progress.seasonName}",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        modifier = Modifier.padding(horizontal = 16.dp, vertical = 4.dp),
                    )
                }
                if (progress != null && !progress.isRunning) {
                    val resultText = if (progress.isCancelled) {
                        "Preparação cancelada; ${progress.queuedEpisodes} episódio(s) permanecem na fila."
                    } else {
                        "${progress.queuedEpisodes} na fila · ${progress.alreadyAvailableEpisodes} já disponíveis · ${progress.alreadyPreparingEpisodes} já em preparação · ${progress.failedEpisodes} falhas"
                    }
                    Text(
                        text = resultText,
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        modifier = Modifier.padding(horizontal = 16.dp, vertical = 4.dp),
                    )
                }
            }
        }

        // Episodes list
        episodes.forEach { ep ->
            EpisodeRow(episode = ep, onPlay = { onEpisodePlay(ep.id) }, onClick = { onEpisodeClick(ep.id) })
        }

        if (episodes.isEmpty()) {
            when {
                isLoading -> CircularProgressIndicator(
                    modifier = Modifier.padding(horizontal = 16.dp, vertical = 12.dp).size(24.dp),
                    strokeWidth = 2.dp,
                )
                error != null -> Column(modifier = Modifier.padding(horizontal = 16.dp, vertical = 8.dp)) {
                    Text(error, color = MaterialTheme.colorScheme.error)
                    TextButton(onClick = onRetry) { Text("Tentar novamente") }
                }
                seasons.isEmpty() -> SeriesSectionMessage("Nenhuma temporada disponível")
                else -> SeriesSectionMessage("Nenhum episódio disponível")
            }
        }
    }
}

/**
 * Horizontally scrollable season posters (the media cover the server has for the
 * season, falling back to the series poster) with the selected season outlined.
 */
@Composable
private fun SeasonCoverRow(
    seasons: List<MediaItem>,
    selectedSeasonIndex: Int,
    onSeasonSelect: (Int) -> Unit,
) {
    val serverUrl = LocalMulletaFlixServerUrl.current
    val accessToken = LocalMulletaFlixAccessToken.current

    LazyRow(
        contentPadding = PaddingValues(horizontal = 16.dp, vertical = 4.dp),
        horizontalArrangement = Arrangement.spacedBy(10.dp),
    ) {
        itemsIndexed(seasons, key = { _, season -> season.id }) { index, season ->
            val selected = index == selectedSeasonIndex
            val shape = RoundedCornerShape(8.dp)

            Column(
                horizontalAlignment = Alignment.CenterHorizontally,
                modifier = Modifier
                    .width(110.dp)
                    .clip(shape)
                    .clickable { onSeasonSelect(index) }
                    .semantics { this.selected = selected }
                    .padding(bottom = 4.dp),
            ) {
                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .aspectRatio(2f / 3f)
                        .clip(shape)
                        .background(MaterialTheme.colorScheme.surfaceVariant)
                        .then(
                            if (selected) {
                                // `colorScheme.primary` is the brand black in the default
                                // dark scheme (invisible on dark surfaces), so the accent
                                // of this app — secondary/red — marks the selection.
                                Modifier.border(2.dp, MaterialTheme.colorScheme.secondary, shape)
                            } else {
                                Modifier
                            }
                        ),
                ) {
                    AsyncImage(
                        model = resolveMediaUrl(serverUrl, season.primaryImageUrl, accessToken),
                        contentDescription = season.name,
                        contentScale = ContentScale.Crop,
                        modifier = Modifier.fillMaxSize(),
                    )
                }
                Text(
                    text = season.name,
                    style = MaterialTheme.typography.titleSmall,
                    color = if (selected) MaterialTheme.colorScheme.secondary else MaterialTheme.colorScheme.onSurface,
                    fontWeight = if (selected) FontWeight.SemiBold else FontWeight.Normal,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis,
                    modifier = Modifier.padding(top = 6.dp),
                )
            }
        }
    }
}

@Composable
private fun SeriesSectionMessage(text: String) {
    Text(
        text = text,
        style = MaterialTheme.typography.bodyMedium,
        color = MaterialTheme.colorScheme.onSurfaceVariant,
        modifier = Modifier.padding(horizontal = 16.dp, vertical = 12.dp),
    )
}

@Composable
private fun EpisodeRow(episode: MediaItem, onPlay: () -> Unit, onClick: () -> Unit) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clickable(role = Role.Button, onClick = onClick)
            .padding(horizontal = 16.dp, vertical = 8.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Box(modifier = Modifier.width(160.dp).aspectRatio(16f / 9f).clip(RoundedCornerShape(6.dp)).background(MaterialTheme.colorScheme.surfaceVariant)) {
            AsyncImage(model = resolveMediaUrl(LocalMulletaFlixServerUrl.current, episode.primaryImageUrl, LocalMulletaFlixAccessToken.current), contentDescription = episode.name, contentScale = ContentScale.Crop, modifier = Modifier.fillMaxSize())
            episode.playbackProgressFraction().takeIf { it > 0f }?.let { progress ->
                Box(modifier = Modifier.fillMaxWidth().height(3.dp).align(Alignment.BottomCenter).background(MaterialTheme.colorScheme.surface)) {
                    Box(modifier = Modifier.fillMaxHeight().fillMaxWidth(progress).background(MaterialTheme.colorScheme.secondary))
                }
            }
            // O alvo interativo tem o mínimo do Material (48 dp). Medido no aparelho: a
            // área que aceita o toque é a do `clickable` que o componente aplica, então
            // um `padding` no modificador do chamador encolheria *o alvo* de volta para
            // 40 dp mesmo com o nó externo maior — o disco tem o mesmo tamanho do alvo.
            MulletaFlixTopBarAction(
                onClick = onPlay,
                modifier = Modifier
                    .align(Alignment.Center)
                    .size(EPISODE_PLAY_TARGET_DP.dp)
                    .background(Color.Black.copy(0.5f), CircleShape),
            ) {
                Icon(Icons.Default.PlayArrow, contentDescription = "Reproduzir", tint = Color.White)
            }
        }
        Column(modifier = Modifier.weight(1f).padding(start = 12.dp)) {
            Text(episodeLabel(episode.parentIndexNumber, episode.indexNumber, episode.name), style = MaterialTheme.typography.titleSmall, color = MaterialTheme.colorScheme.onSurface, maxLines = 2, overflow = TextOverflow.Ellipsis)
            episode.runtimeMinutes?.let { Text("$it min", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant) }
            episode.overview?.let { Text(it, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant, maxLines = 2, overflow = TextOverflow.Ellipsis, modifier = Modifier.padding(top = 4.dp)) }
        }
    }
}

private val MediaItem.runtimeMinutes: Int? get() = runtimeTicks?.div(600_000_000L)?.toInt()?.takeIf { it > 0 }

/**
 * Interactive size of the play button over an episode thumbnail.
 *
 * Material's minimum: a 40 dp target is harder to hit with a thumb, and this button sits
 * on top of artwork that is itself tappable to open the episode.
 */
internal const val EPISODE_PLAY_TARGET_DP = 48

/**
 * Episode row title (`1x01 Nome`).
 *
 * Extras and specials have no season/episode numbers; they show only their name
 * instead of a `nullx00` placeholder.
 */
internal fun episodeLabel(seasonNumber: Int?, episodeNumber: Int?, name: String): String =
    if (seasonNumber == null || episodeNumber == null) {
        name
    } else {
        "${seasonNumber}x${String.format("%02d", episodeNumber)} $name"
    }
