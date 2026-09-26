package org.mulletaflix.feature.player

import android.content.Context
import androidx.media3.common.Player
import androidx.media3.common.util.UnstableApi
import androidx.media3.session.MediaSession
import com.google.android.gms.cast.framework.CastSession
import com.google.android.gms.cast.framework.CastContext
import com.google.android.gms.cast.framework.SessionManager
import com.google.android.gms.cast.framework.SessionManagerListener
import com.google.android.gms.cast.framework.media.RemoteMediaClient
import kotlinx.coroutines.flow.StateFlow

/**
 * Shares the active screen player with the app's MediaSessionService.
 * The service can still create a fallback session when it starts first.
 *
 * Ownership: the bridge **borrows** the player and **owns** only the session it
 * builds. See [shouldReleaseSession] and [shouldReleasePlayer].
 */
@UnstableApi
object PlayerMediaSessionBridge {
    @Volatile
    private var activeSession: MediaSession? = null
    private var activePlayer: Player? = null
    private var playerListener: Player.Listener? = null
    private var uiOwner = false
    private var serviceOwner = false
    private var isCasting = false
    private var castConnectionState = CastConnectionState.IDLE
    private var castReceiverName: String? = null
    private var castSessionManager: SessionManager? = null
    private var castSessionListenerRegistered = false
    private var remoteMediaClient: RemoteMediaClient? = null

    private val castMiniControllerStateStore = CastMiniControllerStateStore()
    val castMiniControllerState: StateFlow<CastMiniControllerState?> = castMiniControllerStateStore.state

    private val remoteMediaClientCallback = object : RemoteMediaClient.Callback() {
        override fun onStatusUpdated() = updateRemotePlaybackState()
        override fun onMetadataUpdated() = updateRemotePlaybackState()
        override fun onQueueStatusUpdated() = updateRemotePlaybackState()
    }

    private val castSessionListener = object : SessionManagerListener<CastSession> {
        override fun onSessionStarting(session: CastSession) = markCastSessionActive(session, CastConnectionState.CONNECTING)
        override fun onSessionStarted(session: CastSession, sessionId: String) = bindCastSession(session)
        override fun onSessionStartFailed(session: CastSession, error: Int) = clearCastSession()
        override fun onSessionEnding(session: CastSession) = clearCastSession()
        override fun onSessionEnded(session: CastSession, error: Int) = clearCastSession()
        override fun onSessionResuming(session: CastSession, sessionId: String) = markCastSessionActive(session, CastConnectionState.CONNECTING)
        override fun onSessionResumed(session: CastSession, wasSuspended: Boolean) = bindCastSession(session)
        override fun onSessionResumeFailed(session: CastSession, error: Int) = clearCastSession()
        override fun onSessionSuspended(session: CastSession, reason: Int) = suspendCastSession(session)
    }

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
                playerListener = object : Player.Listener {
                    override fun onEvents(player: Player, events: Player.Events) {
                        updateMiniControllerState(player)
                    }
                }.also(player::addListener)
                updateMiniControllerState(player)
                observeCastSession(context)
            }
    }

    fun current(): MediaSession? = activeSession

    @Synchronized
    fun updateCastState(
        isCasting: Boolean,
        receiverName: String?,
        connectionState: CastConnectionState = if (isCasting) CastConnectionState.CONNECTED else CastConnectionState.IDLE,
    ) {
        this.isCasting = isCasting
        castConnectionState = if (isCasting) connectionState else CastConnectionState.IDLE
        castReceiverName = receiverName
        castMiniControllerStateStore.updateSession(isCasting, receiverName, castConnectionState)
    }

    @Synchronized
    fun toggleCastPlayback() {
        if (!isCasting || !canToggleCastPlayback(castConnectionState)) return
        remoteMediaClient?.let { client ->
            if (client.isPlaying) client.pause() else client.play()
            return
        }
        if (!uiOwner) return
        val player = activePlayer ?: return
        if (player.isPlaying) player.pause() else player.play()
    }

    fun stopCasting(context: Context) {
        runCatching {
            CastContext.getSharedInstance(context.applicationContext)
                .sessionManager
                .endCurrentSession(true)
        }
    }

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
            activePlayer?.let { player -> playerListener?.let(player::removeListener) }
            playerListener = null
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
        if (!force && !shouldReleaseSession(uiOwner, serviceOwner)) return
        activePlayer?.let { player -> playerListener?.let(player::removeListener) }
        playerListener = null
        if (!isCasting) {
            stopObservingCastSession()
            castMiniControllerStateStore.clear()
        }
        activeSession?.release()
        // The player is deliberately *not* released. [shouldReleasePlayer] carries the
        // rule and the reasoning; it is always false because the bridge never builds a
        // player, only borrows one.
        if (shouldReleasePlayer()) activePlayer?.release()
        activeSession = null
        activePlayer = null
        uiOwner = false
        serviceOwner = false
    }

    private fun updateMiniControllerState(player: Player?) {
        if (player == null || !uiOwner) return
        val item = player.currentMediaItem
        castMiniControllerStateStore.updateMedia(
            itemId = item?.mediaId,
            title = item?.mediaMetadata?.title?.toString(),
            isPlaying = player.isPlaying,
        )
    }

    @Synchronized
    private fun observeCastSession(context: Context) {
        val manager = runCatching {
            CastContext.getSharedInstance(context.applicationContext).sessionManager
        }.getOrNull() ?: return
        castSessionManager = manager
        if (!castSessionListenerRegistered) {
            manager.addSessionManagerListener(castSessionListener, CastSession::class.java)
            castSessionListenerRegistered = true
        }
        manager.currentCastSession?.let(::bindCastSession)
    }

    @Synchronized
    private fun markCastSessionActive(session: CastSession, connectionState: CastConnectionState) {
        isCasting = true
        castConnectionState = connectionState
        castReceiverName = session.castDevice?.friendlyName ?: castReceiverName
        castMiniControllerStateStore.updateSession(
            isCasting = true,
            receiverName = castReceiverName,
            connectionState = connectionState,
        )
    }

    @Synchronized
    private fun bindCastSession(session: CastSession) {
        markCastSessionActive(session, CastConnectionState.CONNECTED)
        val client = session.remoteMediaClient
        if (remoteMediaClient !== client) {
            remoteMediaClient?.unregisterCallback(remoteMediaClientCallback)
            remoteMediaClient = client
            client?.registerCallback(remoteMediaClientCallback)
        }
        updateRemotePlaybackState()
    }

    @Synchronized
    private fun updateRemotePlaybackState() {
        remoteMediaClient?.let { client ->
            castMiniControllerStateStore.updateRemotePlayback(client.isPlaying)
        }
    }

    @Synchronized
    private fun clearCastSession() {
        remoteMediaClient?.unregisterCallback(remoteMediaClientCallback)
        remoteMediaClient = null
        isCasting = false
        castConnectionState = CastConnectionState.IDLE
        castReceiverName = null
        castMiniControllerStateStore.updateSession(
            isCasting = false,
            receiverName = null,
            connectionState = CastConnectionState.IDLE,
        )
        if (activeSession == null && !uiOwner && !serviceOwner) {
            unregisterCastSessionListener()
        }
    }

    @Synchronized
    private fun suspendCastSession(session: CastSession) {
        remoteMediaClient?.unregisterCallback(remoteMediaClientCallback)
        remoteMediaClient = null
        markCastSessionActive(session, CastConnectionState.SUSPENDED)
        // Keep the SessionManagerListener registered: this session may resume.
    }

    @Synchronized
    private fun stopObservingCastSession() {
        clearCastSession()
        unregisterCastSessionListener()
    }

    private fun unregisterCastSessionListener() {
        castSessionManager?.let { manager ->
            if (castSessionListenerRegistered) {
                manager.removeSessionManagerListener(castSessionListener, CastSession::class.java)
            }
        }
        castSessionManager = null
        castSessionListenerRegistered = false
    }
}
