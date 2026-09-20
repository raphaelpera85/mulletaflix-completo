package org.mulletaflix.core.common.update

import java.io.File
import java.nio.charset.StandardCharsets
import java.security.MessageDigest
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class ApkIntegrityTest {
    @Test
    fun `matching sha256 digest is accepted with github prefix`() {
        val file = temporaryApk("mulletaflix-update")
        try {
            val digest = sha256(file)
            assertTrue(sha256Matches(file, "sha256:$digest"))
            assertTrue(sha256Matches(file, digest.uppercase()))
        } finally {
            file.delete()
        }
    }

    @Test
    fun `wrong or malformed digest is rejected`() {
        val file = temporaryApk("mulletaflix-update")
        try {
            assertFalse(sha256Matches(file, "sha256:${"0".repeat(64)}"))
            assertFalse(sha256Matches(file, "not-a-digest"))
            assertFalse(sha256Matches(file, null))
        } finally {
            file.delete()
        }
    }

    @Test
    fun `only official github release apk urls are trusted`() {
        assertTrue(isTrustedApkDownloadUrl("https://github.com/raphaelpera85/mulletaflix-completo/releases/download/app-v1.0.66/mulletaflix-app-v1.0.66.apk"))
        assertFalse(isTrustedApkDownloadUrl("http://github.com/raphaelpera85/mulletaflix-completo/releases/download/app-v1.0.66/app.apk"))
        assertFalse(isTrustedApkDownloadUrl("https://evil.example/raphaelpera85/mulletaflix-completo/releases/download/app-v1.0.66/app.apk"))
        assertFalse(isTrustedApkDownloadUrl("https://github.com/other/repository/releases/download/app-v1.0.66/app.apk"))
    }

    @Test
    fun `partial apk cleanup is idempotent`() {
        val file = temporaryApk("partial-update")

        deletePartialApk(file)
        deletePartialApk(file)
        deletePartialApk(null)

        assertFalse(file.exists())
    }

    private fun temporaryApk(content: String): File = File.createTempFile("mulletaflix-", ".apk").apply {
        writeText(content, StandardCharsets.UTF_8)
    }

    private fun sha256(file: File): String = MessageDigest.getInstance("SHA-256")
        .digest(file.readBytes())
        .joinToString("") { "%02x".format(it) }
}
