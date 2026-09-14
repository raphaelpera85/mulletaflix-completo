package org.mulletaflix.feature.player

import android.content.Context
import androidx.media3.common.Player
import androidx.media3.common.util.UnstableApi
import androidx.media3.session.MediaSession

/**
 * Shares the active screen player with the app's MediaSessionService.
 * The service can still create a fallback session when it starts first.
 */
@UnstableApi
object PlayerMediaSessionBridge {
    @Volatile
    private var activeSession: MediaSession? = null
    private var activePlayer: Player? = null
    private var uiOwner = false
    private var serviceOwner = false

    @Synchronized
    fun attach(context: Context, player: Player): MediaSession {
        if (activeSession != null && activePlayer === player) {
            uiOwner = true
            return activeSession!!
        }
        releaseIfUnused(force = true)
        return MediaSession.Builder(context.applicationContext, player)
            .setId("mulletaflix-player")
            .build()
            .also {
                activeSession = it
                activePlayer = player
                uiOwner = true
                serviceOwner = false
            }
    }

    fun current(): MediaSession? = activeSession

    @Synchronized
    fun retainForService(session: MediaSession): Boolean {
        if (activeSession !== session) return false
        serviceOwner = true
        return true
    }

    @Synchronized
    fun detach(session: MediaSession) {
        if (activeSession === session) {
            uiOwner = false
            releaseIfUnused()
        }
    }

    @Synchronized
    fun detachService(session: MediaSession) {
        if (activeSession === session) {
            serviceOwner = false
            releaseIfUnused()
        }
    }

    @Synchronized
    private fun releaseIfUnused(force: Boolean = false) {
        if (!force && (uiOwner || serviceOwner)) return
        activeSession?.release()
        activePlayer?.release()
        activeSession = null
        activePlayer = null
        uiOwner = false
        serviceOwner = false
    }
}
