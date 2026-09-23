package org.mulletaflix.designsystem.components

import androidx.compose.animation.animateColorAsState
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.tween
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.focusable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.scale
import androidx.compose.ui.focus.onFocusChanged
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.LiveTv
import androidx.compose.material.icons.filled.Favorite
import androidx.compose.material.icons.filled.Movie
import androidx.compose.material.icons.filled.MusicNote
import androidx.compose.material.icons.filled.Tv
import androidx.compose.material3.Icon
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.invisibleToUser
import androidx.compose.ui.semantics.role
import androidx.compose.ui.semantics.semantics
import coil.compose.AsyncImagePainter
import coil.compose.SubcomposeAsyncImage
import coil.compose.SubcomposeAsyncImageContent
import org.mulletaflix.designsystem.theme.MulletaFlixRed
import org.mulletaflix.designsystem.theme.WatchedBadge
import org.mulletaflix.designsystem.media.LocalMulletaFlixServerUrl
import org.mulletaflix.designsystem.media.LocalMulletaFlixAccessToken
import org.mulletaflix.designsystem.media.resolveMediaUrl

/**
 * Supported card shapes for different content types:
 *  - Portrait (2:3)  → Movies, Books, Albums
 *  - Landscape (16:9) → Episodes, Channels, TV Shows
 *  - Square (1:1)   → Artists, Music
 *  - Banner (7:1)   → Libraries
 */
enum class MediaCardShape { Portrait, Landscape, Square, Banner }

/** Keeps poster and landscape artwork fully visible instead of cropping its edges. */
internal fun mediaCardContentScale(shape: MediaCardShape): ContentScale = when (shape) {
    MediaCardShape.Portrait, MediaCardShape.Landscape -> ContentScale.Fit
    MediaCardShape.Square, MediaCardShape.Banner -> ContentScale.Crop
}

/** Keeps server-provided resume values safe for Compose's fraction modifiers. */
internal fun normalizedCardProgress(progress: Float): Float =
    if (progress.isFinite()) progress.coerceIn(0f, 1f) else 0f

/** Builds one concise TalkBack label from the card's visible playback state. */
internal fun mediaCardAccessibilityLabel(
    title: String,
    isLive: Boolean,
    isWatched: Boolean,
    isFavorite: Boolean,
    qualityBadge: String?,
    unplayedCount: Int,
    progress: Float,
): String {
    val details = buildList {
        if (isLive) add("ao vivo")
        if (isWatched) add("assistido")
        if (isFavorite) add("na Minha Lista")
        qualityBadge?.takeIf { it.isNotBlank() }?.let { add(it) }
        if (unplayedCount > 0) add("$unplayedCount episódios não assistidos")
        val normalized = normalizedCardProgress(progress)
        if (!isWatched && normalized > 0f) {
            add("${(normalized * 100).toInt()}% reproduzido")
        }
    }
    return buildString {
        append("Abrir ")
        append(title)
        if (details.isNotEmpty()) {
            append(", ")
            append(details.joinToString(", "))
        }
    }
}

/**
 * Reusable media card composable used across Home, Library, Search and Detail screens.
 *
 * Features:
 *  - Async image with shimmer placeholder
 *  - Progress bar overlay (resume progress)
 *  - Watched badge
 *  - HD / 4K quality badge
 *  - Unplayed episode count badge
 *  - Gradient overlay for readability of text on top of image
 *  - Favourite heart icon (animated)
 */
@Composable
fun MediaCard(
    title: String,
    imageUrl: String?,
    metadata: String? = null,
    modifier: Modifier = Modifier,
    shape: MediaCardShape = MediaCardShape.Portrait,
    progress: Float = 0f,            // 0..1, 0 = not shown
    isWatched: Boolean = false,
    isFavorite: Boolean = false,
    qualityBadge: String? = null,    // "HD", "4K", "SDR"
    unplayedCount: Int = 0,
    isLive: Boolean = false,
    focusFriendly: Boolean = false,
    isClickable: Boolean = true,
    onClick: () -> Unit = {}
) {
    var isFocused by remember { mutableStateOf(false) }
    val focusScale by animateFloatAsState(
        targetValue = mediaCardFocusScale(focusFriendly, isFocused),
        animationSpec = tween(durationMillis = 120),
        label = "media-card-focus-scale",
    )
    val focusBorderWidth = mediaCardFocusBorderWidthDp(focusFriendly, isFocused)
    val normalizedProgress = normalizedCardProgress(progress)
    val aspectRatio = when (shape) {
        MediaCardShape.Portrait -> 2f / 3f
        MediaCardShape.Landscape -> 16f / 9f
        MediaCardShape.Square -> 1f
        MediaCardShape.Banner -> 7f / 1f
    }

    val cornerRadius = when (shape) {
        MediaCardShape.Banner -> 4.dp
        else -> 8.dp
    }

    val resolvedImageUrl = resolveMediaUrl(LocalMulletaFlixServerUrl.current, imageUrl, LocalMulletaFlixAccessToken.current)
    val accessibilityLabel = mediaCardAccessibilityLabel(
        title = title,
        isLive = isLive,
        isWatched = isWatched,
        isFavorite = isFavorite,
        qualityBadge = qualityBadge,
        unplayedCount = unplayedCount,
        progress = normalizedProgress,
    )

    Column(
        modifier = modifier
            .scale(focusScale)
            .then(
                if (focusFriendly) {
                    Modifier.onFocusChanged { isFocused = it.isFocused }.then(
                        // Only a card that cannot be clicked needs an explicit
                        // focus target. A clickable one already has its own, and
                        // adding `focusable()` on top put **two focus targets on
                        // the same node**: the remote's centre key went to the
                        // target without the activation handler, so the first
                        // press was swallowed and the card needed two clicks to
                        // open. Measured with one centre press on a focused card:
                        // 0 activations before, 1 after.
                        if (isClickable) Modifier else Modifier.focusable(),
                    )
                } else {
                    Modifier
                },
            )
            .then(
                if (focusBorderWidth > 0f) {
                    Modifier.border(
                        width = focusBorderWidth.dp,
                        color = MulletaFlixRed,
                        shape = RoundedCornerShape(10.dp),
                    )
                } else {
                    Modifier
                },
            )
            .then(if (isClickable) Modifier.clickable(onClick = onClick) else Modifier)
            .then(
                if (isClickable) {
                    Modifier.semantics(mergeDescendants = true) {
                        role = Role.Button
                        contentDescription = accessibilityLabel
                    }
                } else {
                    // Um card **não clicável** publicava `role = Role.Button` sem
                    // ação nenhuma e com o mesmo rótulo da linha clicável que o
                    // contém (`LibraryScreen`), então o leitor de tela encontrava o
                    // mesmo item duas vezes — uma delas um botão que não fazia nada
                    // ao ser ativado. Sem ação, o card não fala: quem fala é a linha.
                    Modifier.clearAndSetSemantics { }
                },
            ),
    ) {
      Box(
        modifier = Modifier
            .fillMaxWidth()
            .aspectRatio(aspectRatio)
            .clip(RoundedCornerShape(cornerRadius))
            .background(MaterialTheme.colorScheme.surfaceVariant)
      ) {
        // Single TalkBack announcement (see mediaCardAccessibilityLabel).
        //
        // The column sets `mergeDescendants = true` with an explicit
        // `contentDescription`, and Compose concatenates every descendant
        // description into that merged node. So each visual layer below is
        // marked decorative: the artwork (whose description was the raw title,
        // announced a second time) and the overlays, whose visible text is
        // already folded into the label above.
        SubcomposeAsyncImage(
            model = resolvedImageUrl,
            contentDescription = null,
            contentScale = mediaCardContentScale(shape),
            modifier = Modifier.fillMaxSize().semantics { invisibleToUser() },
        ) {
            val state = painter.state
            if (state is AsyncImagePainter.State.Loading) {
                Box(
                    modifier = Modifier
                        .fillMaxSize()
                        .background(MaterialTheme.colorScheme.surfaceVariant)
                )
            } else if (state is AsyncImagePainter.State.Error || resolvedImageUrl.isNullOrBlank()) {
                Box(
                    modifier = Modifier
                        .fillMaxSize()
                        .background(
                            Brush.verticalGradient(
                                colors = listOf(
                                    Color(0xFF282A36),
                                    Color(0xFF14151C)
                                )
                            )
                        ),
                    contentAlignment = Alignment.Center
                ) {
                    Column(
                        horizontalAlignment = Alignment.CenterHorizontally,
                        verticalArrangement = Arrangement.Center,
                        // This fallback draws the title inside the artwork area,
                        // and the card draws it again underneath. Both labels end
                        // up in the merged node's `Text` property, so an
                        // accessibility service read the title twice.
                        //
                        // `invisibleToUser()` was not enough: it only marks the
                        // node invisible to services while its text still reaches
                        // the merged property (measured on device). The fallback is
                        // purely decorative — the card's own `contentDescription`
                        // already carries the title and the playback state — so its
                        // semantics are cleared entirely, which keeps the visible
                        // drawing identical.
                        modifier = Modifier
                            .padding(6.dp)
                            .clearAndSetSemantics { }
                    ) {
                        val icon = if (isLive) {
                            Icons.Default.LiveTv
                        } else {
                            when (shape) {
                                MediaCardShape.Square -> Icons.Default.MusicNote
                                MediaCardShape.Landscape -> Icons.Default.Movie
                                MediaCardShape.Banner -> Icons.Default.Tv
                                MediaCardShape.Portrait -> Icons.Default.Movie
                            }
                        }
                        Icon(
                            imageVector = icon,
                            contentDescription = null,
                            tint = Color.White.copy(alpha = 0.35f),
                            modifier = Modifier.size(if (shape == MediaCardShape.Banner) 20.dp else 32.dp)
                        )
                        Spacer(modifier = Modifier.height(4.dp))
                        Text(
                            text = title,
                            style = MaterialTheme.typography.labelSmall,
                            color = Color.White.copy(alpha = 0.75f),
                            maxLines = 2,
                            overflow = TextOverflow.Ellipsis,
                            textAlign = TextAlign.Center
                        )
                    }
                }
            } else {
                SubcomposeAsyncImageContent()
            }
        }

        // Gradient overlay (bottom → top, 40%)
        Box(
            modifier = Modifier
                .fillMaxWidth()
                .fillMaxHeight(0.5f)
                .align(Alignment.BottomCenter)
                .background(
                    Brush.verticalGradient(
                        colors = listOf(Color.Transparent, Color.Black.copy(alpha = 0.75f))
                    )
                )
        )

        // Progress bar
        if (normalizedProgress > 0f && !isWatched) {
            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .height(3.dp)
                    .align(Alignment.BottomCenter)
                    .background(MaterialTheme.colorScheme.surfaceVariant)
                    .semantics { invisibleToUser() }
            ) {
                Box(
                    modifier = Modifier
                        .fillMaxHeight()
                        .fillMaxWidth(normalizedProgress)
                        .background(MulletaFlixRed)
                )
            }
        }

        // Watched overlay
        if (isWatched) {
            Box(
                modifier = Modifier
                    .fillMaxSize()
                    .background(Color.Black.copy(alpha = 0.4f))
            )
            Box(
                modifier = Modifier
                    .size(24.dp)
                    .align(Alignment.TopEnd)
                    .padding(4.dp)
                    .background(WatchedBadge, RoundedCornerShape(12.dp))
            )
        }

        // Badges row (top-start); their text is part of the merged label.
        Row(
            modifier = Modifier
                .align(Alignment.TopStart)
                .padding(4.dp)
                .semantics { invisibleToUser() },
            horizontalArrangement = Arrangement.spacedBy(4.dp)
        ) {
            if (isLive) {
                MediaBadge(text = "AO VIVO", color = Color(0xFFE53935))
            }
            qualityBadge?.let { MediaBadge(text = it, color = Color(0xFF2196F3)) }
        }

        // User state badges share one touch-free, readable overlay.
        if (isFavorite || unplayedCount > 0) {
            Row(
                modifier = Modifier
                    .align(Alignment.TopEnd)
                    .padding(4.dp)
                    .semantics { invisibleToUser() },
                horizontalArrangement = Arrangement.spacedBy(4.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                if (isFavorite) {
                    androidx.compose.material3.Icon(
                        imageVector = Icons.Default.Favorite,
                        contentDescription = null,
                        tint = MulletaFlixRed,
                        modifier = Modifier.size(24.dp),
                    )
                }
                if (unplayedCount > 0) {
                    Box(
                        modifier = Modifier
                            .background(MulletaFlixRed, RoundedCornerShape(12.dp))
                            .padding(horizontal = 6.dp, vertical = 2.dp)
                    ) {
                        Text(
                            text = unplayedCount.toString(),
                            style = MaterialTheme.typography.labelSmall,
                            color = Color.White
                        )
                    }
                }
            }
        }

        // Title at bottom
      }
      Text(
          text = title,
          style = MaterialTheme.typography.labelMedium,
          color = MaterialTheme.colorScheme.onSurface,
          maxLines = 2,
          overflow = TextOverflow.Ellipsis,
          modifier = Modifier.padding(top = 6.dp, start = 2.dp, end = 2.dp)
      )
      metadata?.takeIf { it.isNotBlank() }?.let { label ->
          Text(
              text = label,
              style = MaterialTheme.typography.labelSmall,
              color = MaterialTheme.colorScheme.onSurfaceVariant,
              maxLines = 1,
              overflow = TextOverflow.Ellipsis,
              modifier = Modifier.padding(top = 2.dp, start = 2.dp, end = 2.dp),
          )
      }
    }
}

@Composable
private fun MediaBadge(text: String, color: Color) {
    Box(
        modifier = Modifier
            .background(color, RoundedCornerShape(4.dp))
            .padding(horizontal = 5.dp, vertical = 2.dp)
    ) {
        Text(
            text = text,
            style = MaterialTheme.typography.labelSmall,
            color = Color.White
        )
    }
}
