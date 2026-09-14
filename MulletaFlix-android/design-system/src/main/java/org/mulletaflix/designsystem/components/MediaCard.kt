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
import coil.compose.AsyncImage
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
    Column(modifier = modifier.clickable(onClick = onClick)) {
      Box(
        modifier = Modifier
            .fillMaxWidth()
            .aspectRatio(aspectRatio)
            .clip(RoundedCornerShape(cornerRadius))
            .background(MaterialTheme.colorScheme.surfaceVariant)
      ) {
        // Poster image
        AsyncImage(
            model = resolvedImageUrl,
            contentDescription = title,
            contentScale = ContentScale.Crop,
            modifier = Modifier.fillMaxSize()
        )

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
        if (progress > 0f && !isWatched) {
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
                        .fillMaxWidth(progress)
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

        // Unplayed episode count badge (top-end)
        if (unplayedCount > 0) {
            Box(
                modifier = Modifier
                    .align(Alignment.TopEnd)
                    .padding(4.dp)
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
