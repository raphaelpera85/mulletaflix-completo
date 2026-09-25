package org.mulletaflix.feature.player

import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow

/** Playback details needed by the app-wide Cast mini controller. */
data class CastMiniControllerState(
    val itemId: String,
    val title: String,
    val receiverName: String,
    val isPlaying: Boolean,
    val connectionState: CastConnectionState,
)

enum class CastConnectionState {
    IDLE,
    CONNECTING,
    CONNECTED,
    SUSPENDED,
}

fun canToggleCastPlayback(connectionState: CastConnectionState): Boolean =
    connectionState == CastConnectionState.CONNECTED

fun castMiniControllerStatus(
    connectionState: CastConnectionState,
    isPlaying: Boolean,
): String = when (connectionState) {
    CastConnectionState.CONNECTING -> "Conectando"
    CastConnectionState.SUSPENDED -> "Reconectando"
    CastConnectionState.CONNECTED -> if (isPlaying) "Reproduzindo" else "Pausado"
    CastConnectionState.IDLE -> "Desconectado"
}

internal fun castMiniControllerState(
    isCasting: Boolean,
    itemId: String?,
    title: String?,
    receiverName: String?,
    isPlaying: Boolean,
    connectionState: CastConnectionState = if (isCasting) CastConnectionState.CONNECTED else CastConnectionState.IDLE,
): CastMiniControllerState? {
    val validItemId = itemId?.takeIf(String::isNotBlank) ?: return null
    if (!isCasting) return null

    return CastMiniControllerState(
        itemId = validItemId,
        title = title?.trim()?.takeIf(String::isNotEmpty) ?: "Mídia em reprodução",
        receiverName = receiverName?.trim()?.takeIf(String::isNotEmpty) ?: "Dispositivo Cast",
        isPlaying = isPlaying && connectionState == CastConnectionState.CONNECTED,
        connectionState = connectionState,
    )
}

/** Keeps Cast UI state alive independently of the player screen lifecycle. */
internal class CastMiniControllerStateStore {
    private var isCasting = false
    private var itemId: String? = null
    private var title: String? = null
    private var receiverName: String? = null
    private var isPlaying = false
    private var connectionState = CastConnectionState.IDLE

    private val mutableState = MutableStateFlow<CastMiniControllerState?>(null)
    val state: StateFlow<CastMiniControllerState?> = mutableState.asStateFlow()

    @Synchronized
    fun updateMedia(itemId: String?, title: String?, isPlaying: Boolean) {
        this.itemId = itemId
        this.title = title
        this.isPlaying = isPlaying && connectionState == CastConnectionState.CONNECTED
        publish()
    }

    @Synchronized
    fun updateSession(
        isCasting: Boolean,
        receiverName: String?,
        connectionState: CastConnectionState = if (isCasting) CastConnectionState.CONNECTED else CastConnectionState.IDLE,
    ) {
        this.isCasting = isCasting
        this.receiverName = receiverName
        this.connectionState = if (isCasting) connectionState else CastConnectionState.IDLE
        if (this.connectionState != CastConnectionState.CONNECTED) isPlaying = false
        publish()
    }

    @Synchronized
    fun updateRemotePlayback(isPlaying: Boolean) {
        this.isPlaying = isPlaying
        publish()
    }

    @Synchronized
    fun clear() {
        isCasting = false
        itemId = null
        title = null
        receiverName = null
        isPlaying = false
        connectionState = CastConnectionState.IDLE
        publish()
    }

    private fun publish() {
        mutableState.value = castMiniControllerState(
            isCasting = isCasting,
            itemId = itemId,
            title = title,
            receiverName = receiverName,
            isPlaying = isPlaying,
            connectionState = connectionState,
        )
    }
}
