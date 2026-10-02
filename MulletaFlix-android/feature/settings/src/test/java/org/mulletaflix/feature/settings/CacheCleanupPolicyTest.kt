package org.mulletaflix.feature.settings

import org.junit.Assert.assertEquals
import org.junit.Test

class CacheCleanupPolicyTest {
    @Test
    fun `preserves offline download cache`() {
        assertEquals(listOf("code_cache"), cacheEntriesToRemove(listOf("downloads", "image_cache", "code_cache")))
    }

    @Test
    fun `preserves downloads regardless of directory casing`() {
        assertEquals(listOf("tmp"), cacheEntriesToRemove(listOf("DOWNLOADS", "tmp")))
    }

    @Test
    fun `preserves active Coil disk cache directory for cache API cleanup`() {
        assertEquals(listOf("tmp"), cacheEntriesToRemove(listOf("IMAGE_CACHE", "tmp")))
    }
}
