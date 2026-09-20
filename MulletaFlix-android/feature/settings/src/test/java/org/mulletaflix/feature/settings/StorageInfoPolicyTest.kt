package org.mulletaflix.feature.settings

import org.junit.Assert.assertEquals
import org.junit.Test

class StorageInfoPolicyTest {
    @Test
    fun `floors usable storage to whole gib`() {
        assertEquals(10, availableStorageGb(10L * 1024 * 1024 * 1024 + 900L))
        assertEquals(0, availableStorageGb(-1L))
    }

    @Test
    fun `formats the storage summary`() {
        assertEquals("4 GB disponíveis", availableStorageLabel(4L * 1024 * 1024 * 1024))
    }
}
