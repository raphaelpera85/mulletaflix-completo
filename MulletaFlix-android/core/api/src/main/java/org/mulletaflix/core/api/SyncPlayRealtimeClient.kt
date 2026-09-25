package org.mulletaflix.core.api

import com.squareup.moshi.JsonClass
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import kotlinx.coroutines.runBlocking
import okhttp3.HttpUrl.Companion.toHttpUrlOrNull
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.Response
import okhttp3.WebSocket
import okhttp3.WebSocketListener
import org.mulletaflix.core.common.dispatcher.ApplicationScope
import kotlin.jvm.JvmName
import javax.inject.Inject
import javax.inject.Singleton

sealed interface SyncPlayRealtimeEvent {
    data object Connected : SyncPlayRealtimeEvent
    data object Disconnected : SyncPlayRealtimeEvent
    data class Command(
        val groupId: String,
        val playlistItemId: String,
        val command: String,
        val positionTicks: Long?,
        val whenEpochMs: Long? = null,
    ) : SyncPlayRealtimeEvent
    data class GroupUpdate(
        val groupId: String,
        val type: String,
    ) : SyncPlayRealtimeEvent
    data class QueueUpdate(
        val groupId: String,
        val itemId: String?,
        val playlistItemId: String?,
        val startPositionTicks: Long,
        val isPlaying: Boolean,
    ) : SyncPlayRealtimeEvent
}

@JsonClass(generateAdapter = true)
internal data class SyncPlayWireEnvelope(
    @param:com.squareup.moshi.Json(name = "MessageType") val messageType: String? = null,
    @param:com.squareup.moshi.Json(name = "Data") val data: Map<String, Any>? = null,
)

/** Observes SyncPlay broadcasts only while the user is inside a room. */
@Singleton
class SyncPlayRealtimeClient @Inject constructor(
    private val httpClient: OkHttpClient,
    private val sessionRepository: SessionRepository,
    @param:ApplicationScope private val applicationScope: CoroutineScope,
    moshi: com.squareup.moshi.Moshi,
) {
    private val envelopeAdapter = moshi.adapter(SyncPlayWireEnvelope::class.java)
    private val _events = MutableSharedFlow<SyncPlayRealtimeEvent>(extraBufferCapacity = 32)
    val events: Flow<SyncPlayRealtimeEvent> = _events.asSharedFlow()

    @Volatile private var socket: WebSocket? = null
    @Volatile var activeGroupId: String? = null
        private set
    @Volatile private var connectionGeneration = 0L
    @Volatile private var reconnectAttempt = 0
    private var reconnectJob: Job? = null

    fun start(groupId: String? = null) {
        stop()
        activeGroupId = groupId
        val generation = connectionGeneration
        connect(generation)
    }

    private fun connect(generation: Long) {
        if (generation != connectionGeneration || activeGroupId == null) return
        val serverUrl = runBlocking { sessionRepository.getBaseUrl().first() }
        val token = runBlocking { sessionRepository.getAccessToken().first() }
        val deviceId = runBlocking { sessionRepository.getDeviceId().first() }
        val base = serverUrl.toHttpUrlOrNull() ?: run {
            scheduleReconnect(generation)
            return
        }
        val websocketUrl = base.newBuilder()
            .scheme(if (base.scheme == "https") "wss" else "ws")
            .addPathSegment("socket")
            .addQueryParameter("api_key", token)
            .addQueryParameter("deviceId", deviceId)
            .build()
        socket = httpClient.newWebSocket(Request.Builder().url(websocketUrl).build(), listener)
    }

    fun stop() {
        connectionGeneration++
        reconnectJob?.cancel()
        reconnectJob = null
        reconnectAttempt = 0
        socket?.close(1000, "room closed")
        socket = null
        activeGroupId = null
    }

    private val listener = object : WebSocketListener() {
        override fun onOpen(webSocket: WebSocket, response: Response) {
            if (!isCurrentSyncPlaySocket(socket, webSocket)) return
            reconnectAttempt = 0
            reconnectJob?.cancel()
            reconnectJob = null
            _events.tryEmit(SyncPlayRealtimeEvent.Connected)
        }

        override fun onMessage(webSocket: WebSocket, text: String) {
            if (!isCurrentSyncPlaySocket(socket, webSocket)) return
            parse(text)?.let(_events::tryEmit)
        }

        override fun onClosed(webSocket: WebSocket, code: Int, reason: String) {
            if (isCurrentSyncPlaySocket(socket, webSocket)) {
                socket = null
                _events.tryEmit(SyncPlayRealtimeEvent.Disconnected)
                scheduleReconnect(connectionGeneration)
            }
        }

        override fun onFailure(webSocket: WebSocket, t: Throwable, response: Response?) {
            if (isCurrentSyncPlaySocket(socket, webSocket)) {
                socket = null
                _events.tryEmit(SyncPlayRealtimeEvent.Disconnected)
                scheduleReconnect(connectionGeneration)
            }
        }
    }

    private fun scheduleReconnect(generation: Long) {
        if (generation != connectionGeneration || activeGroupId == null || reconnectJob?.isActive == true) return
        val attempt = reconnectAttempt++
        reconnectJob = applicationScope.launch {
            delay(syncPlayReconnectDelayMs(attempt))
            if (generation == connectionGeneration && activeGroupId != null) connect(generation)
        }
    }

    private fun parse(text: String): SyncPlayRealtimeEvent? =
        parseSyncPlayRealtimeEvent(envelopeAdapter, text)
}

/** Drops callbacks from a socket superseded by a reconnect or room switch. */
internal fun isCurrentSyncPlaySocket(activeSocket: Any?, callbackSocket: Any): Boolean =
    activeSocket === callbackSocket

/** Bounded backoff keeps a transient Wi-Fi loss from creating a reconnect storm. */
internal fun syncPlayReconnectDelayMs(attempt: Int): Long = when (attempt.coerceAtLeast(0)) {
    0 -> 1_000L
    1 -> 2_000L
    2 -> 4_000L
    else -> 8_000L
}

internal fun parseSyncPlayRealtimeEvent(
    adapter: com.squareup.moshi.JsonAdapter<SyncPlayWireEnvelope>,
    text: String,
): SyncPlayRealtimeEvent? = runCatching {
    val envelope = adapter.fromJson(text) ?: return null
    val data = envelope.data ?: return null
    when (envelope.messageType) {
        "SyncPlayCommand" -> SyncPlayRealtimeEvent.Command(
            groupId = data.string("GroupId") ?: return null,
            playlistItemId = data.string("PlaylistItemId").orEmpty(),
            command = data.string("Command") ?: return null,
            positionTicks = data.long("PositionTicks"),
            whenEpochMs = data.string("When")?.let(::parseSyncPlayTimestamp),
        )
        "SyncPlayPlayQueueUpdate" -> parseQueueUpdate(data)
        "SyncPlayGroupUpdate" -> SyncPlayRealtimeEvent.GroupUpdate(
            groupId = data.string("GroupId") ?: return null,
            type = data.string("Type") ?: return null,
        )
        else -> null
    }
}.getOrNull()

@JvmName("typedString")
private fun Map<String, Any>.string(key: String): String? = get(key)?.toString()

private fun parseQueueUpdate(data: Map<String, Any>): SyncPlayRealtimeEvent.QueueUpdate? {
    val update = data["Data"] as? Map<*, *> ?: return null
    val playlist = update["Playlist"] as? List<*> ?: return null
    val index = update.long("PlayingItemIndex")?.toInt() ?: return null
    val item = playlist.getOrNull(index) as? Map<*, *> ?: return null
    return SyncPlayRealtimeEvent.QueueUpdate(
        groupId = data.string("GroupId") ?: return null,
        itemId = item.string("ItemId"),
        playlistItemId = item.string("PlaylistItemId"),
        startPositionTicks = update.long("StartPositionTicks") ?: 0L,
        isPlaying = update["IsPlaying"] as? Boolean ?: false,
    )
}

@JvmName("genericString")
private fun Map<*, *>.string(key: String): String? = this[key]?.toString()

@JvmName("genericLong")
private fun Map<*, *>.long(key: String): Long? = when (val value = this[key]) {
    is Number -> value.toLong()
    is String -> value.toLongOrNull()
    else -> null
}

@JvmName("typedLong")
private fun Map<String, Any>.long(key: String): Long? = when (val value = get(key)) {
    is Number -> value.toLong()
    is String -> value.toLongOrNull()
    else -> null
}

private fun parseSyncPlayTimestamp(value: String): Long? = runCatching {
    val normalized = value.replace(Regex("(\\.\\d{3})\\d+"), "$1")
    listOf(
        "yyyy-MM-dd'T'HH:mm:ss.SSSX",
        "yyyy-MM-dd'T'HH:mm:ssX",
    ).firstNotNullOfOrNull { pattern ->
        java.text.SimpleDateFormat(pattern, java.util.Locale.US).runCatching {
            parse(normalized)?.time
        }.getOrNull()
    }
}.getOrNull()
