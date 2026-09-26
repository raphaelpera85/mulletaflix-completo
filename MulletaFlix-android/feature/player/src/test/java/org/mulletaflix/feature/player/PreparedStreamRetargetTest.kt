package org.mulletaflix.feature.player

import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Prova a ligação do repontamento de playback: a decisão existe em
 * `StreamRetargetPolicy`, e aqui se prova que ela é **aplicada ao player**.
 *
 * O cenário: um episódio começa a ser assistido em casa e o app troca sozinho para o
 * endereço público quando o Wi-Fi desaparece. O player recebeu a URL absoluta uma
 * única vez, então sem isto o vídeo morre no meio e só volta reabrindo o título.
 *
 * `PlayerViewModel` constrói o próprio `ExoPlayer` e não pode ser montado fora de um
 * app com grafo Hilt; por isso o que se exercita é a peça que decide e manda, com um
 * jogador falso de cinco membros.
 */
class PreparedStreamRetargetTest {

    /** Registra o que o coordenador pediu ao player. */
    private class RecordingStream(
        private var url: String? = "http://192.168.15.9:8096/Videos/item-1/stream?api_key=OLD",
        var fallbackUrl: String? = "http://192.168.15.9:8096/Videos/item-1/transcode?api_key=OLD",
        private val offline: Boolean = false,
        private val position: Long = 42_000L,
        private val playing: Boolean = true,
    ) : RetargetableStream {
        var replacedUrl: String? = null
        var replacedPosition: Long? = null
        var replacedResume: Boolean? = null
        var replacements = 0

        override fun preparedUrl(): String? = url
        override fun isOffline(): Boolean = offline
        override fun positionMs(): Long = position
        override fun isPlaying(): Boolean = playing

        override fun replaceSource(url: String, positionMs: Long, resumePlayback: Boolean) {
            replacedUrl = url
            replacedPosition = positionMs
            replacedResume = resumePlayback
            replacements++
            this.url = url
        }
    }

    private fun coordinator(
        stream: RecordingStream,
        token: String? = "NEW",
        tokenReads: MutableList<String> = mutableListOf(),
    ) = PreparedStreamRetarget(
        stream = stream,
        accessToken = { tokenReads += "read"; token },
        transcodeFallbackUrl = { stream.fallbackUrl },
        updateTranscodeFallbackUrl = { stream.fallbackUrl = it },
    )

    @Test
    fun `a stream prepared at home moves to the address in use`() = runBlocking {
        val stream = RecordingStream()
        val moved = coordinator(stream).onBaseUrlChanged("http://mulletaflix.duckdns.org:8096")

        assertTrue("o stream deveria ter sido repontado", moved)
        assertEquals(
            "http://mulletaflix.duckdns.org:8096/Videos/item-1/stream?api_key=NEW",
            stream.replacedUrl,
        )
        assertEquals("a posição assistida precisa ser preservada", 42_000L, stream.replacedPosition)
        assertEquals("a intenção de reproduzir precisa ser preservada", true, stream.replacedResume)
    }

    @Test
    fun `transcode fallback follows LAN to internet host changes`() = runBlocking {
        val stream = RecordingStream()

        coordinator(stream).onBaseUrlChanged("http://mulletaflix.duckdns.org:8096")

        assertEquals(
            "http://mulletaflix.duckdns.org:8096/Videos/item-1/transcode?api_key=NEW",
            stream.fallbackUrl,
        )
    }

    @Test
    fun `transcode fallback follows internet to LAN host changes`() = runBlocking {
        val stream = RecordingStream(
            url = "http://mulletaflix.duckdns.org:8096/Videos/item-1/stream?api_key=OLD",
            fallbackUrl = "http://mulletaflix.duckdns.org:8096/Videos/item-1/transcode?api_key=OLD",
        )

        coordinator(stream).onBaseUrlChanged("http://192.168.15.9:8096")

        assertEquals(
            "http://192.168.15.9:8096/Videos/item-1/transcode?api_key=NEW",
            stream.fallbackUrl,
        )
    }

    @Test
    fun `a paused stream stays paused`() = runBlocking {
        val stream = RecordingStream(playing = false)
        coordinator(stream).onBaseUrlChanged("http://mulletaflix.duckdns.org:8096")
        assertEquals(false, stream.replacedResume)
    }

    @Test
    fun `a stream already on the current address is not touched`() = runBlocking {
        // O fluxo de endereço emite a cada gravação de preferência; repontar um stream
        // saudável só o faria engasgar.
        val stream = RecordingStream(
            url = "http://192.168.15.9:8096/Videos/item-1/stream?api_key=OLD",
        )
        val moved = coordinator(stream).onBaseUrlChanged("http://192.168.15.9:8096")

        assertFalse(moved)
        assertNull(stream.replacedUrl)
        assertEquals(0, stream.replacements)
    }

    @Test
    fun `an offline download is never re-pointed at a server`() = runBlocking {
        // A URL de um download é a URL **http** original: o Media3 a reproduz através do
        // cache compartilhado, que é indexado por ela. Repontar essa URL para outro
        // host faria a busca no cache errar e a reprodução offline virar streaming (ou
        // falhar), então o guarda de offline é o que impede a correção de virar defeito.
        val stream = RecordingStream(
            url = "http://192.168.15.9:8096/Videos/item-1/stream?api_key=OLD&Static=true",
            offline = true,
        )
        val moved = coordinator(stream).onBaseUrlChanged("http://mulletaflix.duckdns.org:8096")

        assertFalse("download offline não se reponta para servidor", moved)
        assertNull(stream.replacedUrl)
        assertEquals(0, stream.replacements)
    }

    @Test
    fun `nothing loaded means nothing to move`() = runBlocking {
        val stream = RecordingStream(url = null)
        assertFalse(coordinator(stream).onBaseUrlChanged("http://mulletaflix.duckdns.org:8096"))
        assertEquals(0, stream.replacements)
    }

    @Test
    fun `the credential is only read when a move is going to happen`() = runBlocking {
        // Cada leitura de token é um acesso ao armazenamento da sessão; o fluxo de
        // endereço emite muito mais vezes do que o endereço muda.
        val untouched = RecordingStream(url = "http://192.168.15.9:8096/Videos/item-1/stream")
        val readsWhenIdle = mutableListOf<String>()
        coordinator(untouched, tokenReads = readsWhenIdle)
            .onBaseUrlChanged("http://192.168.15.9:8096")
        assertEquals("não deveria ler o token sem precisar mover", 0, readsWhenIdle.size)

        val stream = RecordingStream()
        val readsWhenMoving = mutableListOf<String>()
        coordinator(stream, tokenReads = readsWhenMoving)
            .onBaseUrlChanged("http://mulletaflix.duckdns.org:8096")
        assertEquals(1, readsWhenMoving.size)
    }

    @Test
    fun `a session that has not been read yet leaves the prepared stream alone`() = runBlocking {
        val stream = RecordingStream()
        val moved = coordinator(stream).onBaseUrlChanged(baseUrl = "")
        assertFalse("sem endereço lido, chutar é pior do que o stream em uso", moved)
        assertNull(stream.replacedUrl)
    }

    @Test
    fun `a negative position is clamped instead of passed on`() = runBlocking {
        // `currentPosition` é `C.TIME_UNSET` (negativo) antes de o player saber onde
        // está; passar isso adiante seria um seek inválido.
        val stream = RecordingStream(position = -1L)
        coordinator(stream).onBaseUrlChanged("http://mulletaflix.duckdns.org:8096")
        assertEquals(0L, stream.replacedPosition)
    }
}
