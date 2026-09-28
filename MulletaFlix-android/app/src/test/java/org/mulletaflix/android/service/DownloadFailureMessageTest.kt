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
            "Não foi possível concluir o download. Tente novamente (código Media3 42).",
            message,
        )
    }

    @Test
    fun `insufficient storage explains how to recover`() {
        assertEquals(
            "Armazenamento insuficiente. Libere espaço no dispositivo e tente baixar novamente. (código Media3 1)",
            downloadFailureMessage(Download.FAILURE_REASON_UNKNOWN, insufficientStorage = true),
        )
    }

    @Test
    fun `storage guidance does not replace technical code for other failures`() {
        assertEquals(
            "Não foi possível concluir o download. Tente novamente (código Media3 42).",
            downloadFailureMessage(42, insufficientStorage = false),
        )
    }
}
