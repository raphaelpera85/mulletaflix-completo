package org.mulletaflix.android.service

import android.content.Intent
import androidx.media3.common.util.UnstableApi
import androidx.media3.common.AudioAttributes
import androidx.media3.common.C
import androidx.media3.exoplayer.ExoPlayer
import androidx.media3.session.MediaSession
import androidx.media3.session.MediaSessionService
import dagger.hilt.android.AndroidEntryPoint
import org.mulletaflix.feature.player.PlayerMediaSessionBridge

@UnstableApi
@AndroidEntryPoint
class MulletaFlixPlaybackService : MediaSessionService() {

    private var mediaSession: MediaSession? = null
    private var fallbackPlayer: ExoPlayer? = null
    private var ownsFallbackSession = false

    override fun onCreate() {
        super.onCreate()
        PlayerMediaSessionBridge.current()?.takeIf { PlayerMediaSessionBridge.retainForService(it) }?.let {
            mediaSession = it
            return
        }

        fallbackPlayer = ExoPlayer.Builder(this)
            .setAudioAttributes(
                AudioAttributes.Builder()
                    .setUsage(C.USAGE_MEDIA)
                    .setContentType(C.AUDIO_CONTENT_TYPE_MOVIE)
                    .build(),
                true,
            )
            .setHandleAudioBecomingNoisy(true)
            .build()
        mediaSession = MediaSession.Builder(this, fallbackPlayer!!)
            .setId("mulletaflix-service-fallback")
            .build()
        ownsFallbackSession = true
    }

    override fun onGetSession(controllerInfo: MediaSession.ControllerInfo): MediaSession? {
        return PlayerMediaSessionBridge.current() ?: mediaSession
    }

    override fun onDestroy() {
        if (ownsFallbackSession) {
            mediaSession?.release()
            fallbackPlayer?.release()
        } else {
            mediaSession?.let(PlayerMediaSessionBridge::detachService)
        }
        mediaSession = null
        fallbackPlayer = null
        ownsFallbackSession = false
        super.onDestroy()
    }
}
