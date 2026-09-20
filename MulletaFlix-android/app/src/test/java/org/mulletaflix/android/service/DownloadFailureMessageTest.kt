package org.mulletaflix.android.service

import androidx.media3.exoplayer.offline.Download
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class DownloadFailureMessageTest {
    @Test
    fun `no failure does not expose an error`() {
        assertNull(downloadFailureMessage(Download.FAILURE_REASON_NONE))
    }

    @Test
    fun `unknown failure is actionable`() {
        assertEquals(
            "Falha desconhecida. Tente baixar novamente.",
            downloadFailureMessage(Download.FAILURE_REASON_UNKNOWN),
        )
    }

    @Test
    fun `unexpected failure keeps diagnostic code in an actionable message`() {
        val message = downloadFailureMessage(42)

        assertEquals(
            "Não foi possível concluir o download. Tente novamente (código 42).",
            message,
        )
    }
}
