package org.mulletaflix.feature.player

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class CastMiniControllerStateTest {
    @Test
    fun `publishes active media details for a connected Cast session`() {
        assertEquals(
            CastMiniControllerState(
                itemId = "media-42",
                title = "Filme de teste",
                receiverName = "Sala",
                isPlaying = true,
                connectionState = CastConnectionState.CONNECTED,
            ),
            castMiniControllerState(
                isCasting = true,
                itemId = "media-42",
                title = " Filme de teste ",
                receiverName = " Sala ",
                isPlaying = true,
                connectionState = CastConnectionState.CONNECTED,
            ),
        )
    }

    @Test
    fun `hides the controller when Cast is inactive or item identity is missing`() {
        assertNull(castMiniControllerState(false, "media-42", "Filme", "Sala", false))
        assertNull(castMiniControllerState(false, "media-42", "Filme", "Sala", true))
        assertNull(castMiniControllerState(true, " ", "Filme", "Sala", true))
        assertNull(castMiniControllerState(true, null, "Filme", "Sala", true))
    }

    @Test
    fun `uses readable fallback labels when remote metadata is incomplete`() {
        assertEquals(
            CastMiniControllerState("media-42", "Mídia em reprodução", "Dispositivo Cast", false, CastConnectionState.CONNECTED),
            castMiniControllerState(true, "media-42", "  ", null, false),
        )
    }

    @Test
    fun `remote playback updates remain available after player metadata stops changing`() {
        val store = CastMiniControllerStateStore()
        store.updateMedia(itemId = "media-42", title = "Filme", isPlaying = true)
        store.updateSession(isCasting = true, receiverName = "Sala")

        store.updateRemotePlayback(isPlaying = false)

        assertEquals(
            CastMiniControllerState("media-42", "Filme", "Sala", false, CastConnectionState.CONNECTED),
            store.state.value,
        )
    }

    @Test
    fun `ending the remote session hides the mini controller`() {
        val store = CastMiniControllerStateStore()
        store.updateMedia(itemId = "media-42", title = "Filme", isPlaying = true)
        store.updateSession(isCasting = true, receiverName = "Sala")

        store.updateSession(isCasting = false, receiverName = null)

        assertNull(store.state.value)
    }

    @Test
    fun `suspending the remote session keeps the controller visible and playback actions disabled`() {
        val store = CastMiniControllerStateStore()
        store.updateMedia(itemId = "media-42", title = "Filme", isPlaying = true)
        store.updateSession(isCasting = true, receiverName = "Sala")

        store.updateSession(
            isCasting = true,
            receiverName = "Sala",
            connectionState = CastConnectionState.SUSPENDED,
        )

        assertEquals(
            CastMiniControllerState("media-42", "Filme", "Sala", false, CastConnectionState.SUSPENDED),
            store.state.value,
        )
        val state = requireNotNull(store.state.value)
        assertEquals("Reconectando", castMiniControllerStatus(state.connectionState, state.isPlaying))
        assertFalse(canToggleCastPlayback(state.connectionState))
    }

    @Test
    fun `resuming the remote session restores connected playback controls`() {
        val store = CastMiniControllerStateStore()
        store.updateMedia(itemId = "media-42", title = "Filme", isPlaying = false)
        store.updateSession(true, "Sala", CastConnectionState.SUSPENDED)

        store.updateSession(true, "Sala", CastConnectionState.CONNECTED)
        store.updateRemotePlayback(isPlaying = true)

        val state = requireNotNull(store.state.value)
        assertEquals(
            CastMiniControllerState("media-42", "Filme", "Sala", true, CastConnectionState.CONNECTED),
            state,
        )
        assertTrue(canToggleCastPlayback(state.connectionState))
    }
}
