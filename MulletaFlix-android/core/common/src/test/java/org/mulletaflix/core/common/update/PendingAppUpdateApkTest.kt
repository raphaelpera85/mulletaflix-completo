package org.mulletaflix.core.common.update

import java.io.File
import org.junit.Assert.assertNull
import org.junit.Assert.assertSame
import org.junit.Test
import kotlinx.coroutines.runBlocking

class PendingAppUpdateApkTest {

    @Test
    fun `returns existing APK only for the same update identity`() = runBlocking {
        val apk = File.createTempFile("mulletaflix-pending-update", ".apk")
        try {
            val pending = PendingAppUpdateApk()
            pending.remember(apk, "1.3.90", "https://example.test/app.apk", null)

            assertSame(apk, pending.findFor("1.3.90", "https://example.test/app.apk", null))
            listOf(
                Triple("1.3.91", "https://example.test/app.apk", "abc123"),
                Triple("1.3.90", "https://example.test/other.apk", "abc123"),
                Triple("1.3.90", "https://example.test/app.apk", "different-hash"),
            ).forEach { (version, url, hash) ->
                val mismatched = PendingAppUpdateApk().apply {
                    remember(apk, "1.3.90", "https://example.test/app.apk", null)
                }
                assertNull(mismatched.findFor(version, url, hash))
            }
        } finally {
            apk.delete()
        }
    }

    @Test
    fun `does not return an APK that was removed before retry`() = runBlocking {
        val apk = File.createTempFile("mulletaflix-pending-update", ".apk")
        val pending = PendingAppUpdateApk()
        pending.remember(apk, "1.3.90", "https://example.test/app.apk", null)
        apk.delete()

        assertNull(pending.findFor("1.3.90", "https://example.test/app.apk", null))
    }

    @Test
    fun `does not return a cached APK whose bytes changed after download`() = runBlocking {
        val apk = File.createTempFile("mulletaflix-pending-update", ".apk").apply {
            writeText("verified apk")
        }
        try {
            val pending = PendingAppUpdateApk()
            pending.remember(apk, "1.3.90", "https://example.test/app.apk", null)
            apk.writeText("tampered apk")

            assertNull(pending.findFor("1.3.90", "https://example.test/app.apk", null))
        } finally {
            apk.delete()
        }
    }
}
