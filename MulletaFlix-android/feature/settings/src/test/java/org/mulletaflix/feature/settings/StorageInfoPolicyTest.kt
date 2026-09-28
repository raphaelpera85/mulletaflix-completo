package org.mulletaflix.feature.settings

import org.junit.Assert.assertEquals
import org.junit.Test

class StorageInfoPolicyTest {
    @Test
    fun `shows available storage in megabytes below one gib`() {
        assertEquals("900 MB disponíveis", availableStorageLabel(900L * 1024 * 1024))
        assertEquals("Espaço indisponível", availableStorageLabel(-1L))
    }

    @Test
    fun `formats the storage summary`() {
        val gib = 1024L * 1024 * 1024
        assertEquals("4 GB disponíveis", availableStorageLabel(4 * gib))
        assertEquals("1,8 GB disponíveis", availableStorageLabel(gib + 900L * 1024 * 1024))
        assertEquals("1,9 GB disponíveis", availableStorageLabel((gib * 195) / 100))
        assertEquals("8589934591,9 GB disponíveis", availableStorageLabel(Long.MAX_VALUE))
    }
}
