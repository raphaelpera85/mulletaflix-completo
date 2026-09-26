package org.mulletaflix.feature.livetv

import java.util.Locale
import java.util.TimeZone
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

/**
 * O rótulo de horário de uma gravação.
 *
 * O defeito: a tela removia o `Z` e trocava o `T` por espaço, o que *parece* hora local
 * mas é UTC. Uma gravação das 20:00 no Brasil (UTC-3) aparecia como "23:00".
 */
class RecordingStartLabelTest {

    private val saoPaulo = TimeZone.getTimeZone("America/Sao_Paulo")
    private val utc = TimeZone.getTimeZone("UTC")

    @Test
    fun `a utc recording is shown in the viewer's time zone`() {
        assertEquals(
            "2026-09-14 20:00",
            recordingStartLabel("2026-09-14T23:00:00.0000000Z", saoPaulo, Locale.US),
        )
    }

    @Test
    fun `the same instant is rendered per zone`() {
        val raw = "2026-09-14T23:00:00.0000000Z"
        assertEquals("2026-09-14 23:00", recordingStartLabel(raw, utc, Locale.US))
        assertEquals("2026-09-14 20:00", recordingStartLabel(raw, saoPaulo, Locale.US))
    }

    @Test
    fun `every fractional precision the server emits is accepted`() {
        // Jellyfin writes seven fractional digits in most payloads, three in others, and
        // sometimes none at all.
        listOf(
            "2026-09-14T23:00:00.0000000Z",
            "2026-09-14T23:00:00.000Z",
            "2026-09-14T23:00:00Z",
            "2026-09-14T23:00:00.0000000",
            "2026-09-14T23:00:00",
        ).forEach { raw ->
            assertEquals(
                "não consegui ler $raw",
                "2026-09-14 20:00",
                recordingStartLabel(raw, saoPaulo, Locale.US),
            )
        }
    }

    @Test
    fun `a date the server did not send draws no line at all`() {
        // Melhor não mostrar nada do que mostrar um horário errado.
        assertNull(recordingStartLabel(null, saoPaulo, Locale.US))
        assertNull(recordingStartLabel("", saoPaulo, Locale.US))
        assertNull(recordingStartLabel("   ", saoPaulo, Locale.US))
        assertNull(recordingStartLabel("ontem à noite", saoPaulo, Locale.US))
        assertNull(recordingStartLabel("2026-13-45T99:00:00Z", saoPaulo, Locale.US))
    }
}
