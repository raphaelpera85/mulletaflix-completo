package org.mulletaflix.core.common.update

import java.io.File
import java.security.MessageDigest
import java.net.URI

private val SHA256_PATTERN = Regex("[0-9a-f]{64}")

/** Verifies an APK against the digest returned by the GitHub asset API. */
internal fun sha256Matches(file: File, expectedDigest: String?): Boolean {
    val expected = expectedDigest
        ?.removePrefix("sha256:")
        ?.trim()
        ?.lowercase()
        ?.takeIf { SHA256_PATTERN.matches(it) }
        ?: return false

    val actual = MessageDigest.getInstance("SHA-256").let { digest ->
        file.inputStream().use { input ->
            val buffer = ByteArray(DEFAULT_BUFFER_SIZE)
            var read: Int
            while (input.read(buffer).also { read = it } != -1) {
                digest.update(buffer, 0, read)
            }
        }
        digest.digest().joinToString("") { "%02x".format(it) }
    }
    return actual == expected
}

/** Accepts only the official GitHub release asset endpoint used by the APK channel. */
internal fun isTrustedApkDownloadUrl(downloadUrl: String): Boolean = runCatching {
    val uri = URI(downloadUrl.trim())
    uri.scheme.equals("https", ignoreCase = true) &&
        uri.host.equals("github.com", ignoreCase = true) &&
        uri.path.startsWith("/raphaelpera85/mulletaflix-completo/releases/download/app-v") &&
        uri.path.endsWith(".apk", ignoreCase = true)
}.getOrDefault(false)

/** Removes an incomplete update artifact without failing cleanup itself. */
internal fun deletePartialApk(file: File?) {
    runCatching {
        if (file?.exists() == true) {
            file.delete()
        }
    }
}
