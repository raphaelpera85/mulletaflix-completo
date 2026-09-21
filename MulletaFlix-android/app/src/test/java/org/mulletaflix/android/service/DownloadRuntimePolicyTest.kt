package org.mulletaflix.android.service

import org.junit.Assert.assertEquals
import org.junit.Test

class DownloadRuntimePolicyTest {
    @Test
    fun `keeps at least two workers on low core devices`() {
        assertEquals(2, downloadExecutorThreadCount(1))
        assertEquals(2, downloadExecutorThreadCount(2))
    }

    @Test
    fun `caps workers for high core devices`() {
        assertEquals(4, downloadExecutorThreadCount(8))
        assertEquals(4, downloadExecutorThreadCount(32))
    }
}
