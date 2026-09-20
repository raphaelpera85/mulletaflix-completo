package org.mulletaflix.designsystem.components

import androidx.compose.animation.animateColorAsState
import androidx.compose.animation.core.tween
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
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
import androidx.compose.ui.semantics.contentDescription
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
    onClick: () -> Unit = {}
) {
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
    val accessibilityLabel = if (isLive) {
        "Abrir $title, ao vivo"
    } else {
        "Abrir $title"
    }

    Column(
        modifier = modifier
            .clickable(onClick = onClick)
            .semantics(mergeDescendants = true) {
                role = Role.Button
                contentDescription = accessibilityLabel
            },
    ) {
      Box(
        modifier = Modifier
            .fillMaxWidth()
            .aspectRatio(aspectRatio)
            .clip(RoundedCornerShape(cornerRadius))
            .background(MaterialTheme.colorScheme.surfaceVariant)
      ) {
        // Poster image with loading placeholder & error fallback
        SubcomposeAsyncImage(
            model = resolvedImageUrl,
            contentDescription = title,
            contentScale = mediaCardContentScale(shape),
            modifier = Modifier.fillMaxSize()
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
                        modifier = Modifier.padding(6.dp)
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

        // Badges row (top-start)
        Row(
            modifier = Modifier
                .align(Alignment.TopStart)
                .padding(4.dp),
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
                    .padding(4.dp),
                horizontalArrangement = Arrangement.spacedBy(4.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                if (isFavorite) {
                    androidx.compose.material3.Icon(
                        imageVector = Icons.Default.Favorite,
                        contentDescription = "Favorito",
                        tint = MulletaFlixRed,
                        modifier = Modifier
                            .size(24.dp)
                            .semantics { contentDescription = "Adicionado à Minha Lista" },
                    )
                }
                if (unplayedCount > 0) {
                    Box(
                        modifier = Modifier
                            .semantics { contentDescription = "$unplayedCount episódios não assistidos" }
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
