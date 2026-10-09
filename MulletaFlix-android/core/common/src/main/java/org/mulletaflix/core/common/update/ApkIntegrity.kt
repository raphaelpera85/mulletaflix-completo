package org.mulletaflix.core.common.update

import java.io.File
import java.security.MessageDigest
import java.net.URI
import java.util.zip.CRC32
import java.util.zip.ZipFile

private val SHA256_PATTERN = Regex("[0-9a-f]{64}")
private const val MAX_ANDROID_MANIFEST_SIZE_BYTES = 16L * 1024 * 1024

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

/** Checks a bounded, non-empty manifest ZIP entry and its CRC, not APK signature or binary XML semantics. */
internal fun hasValidAndroidApkManifestEntry(file: File): Boolean = runCatching {
    ZipFile(file).use { archive ->
        val manifest = archive.getEntry("AndroidManifest.xml")
            ?.takeIf {
                !it.isDirectory && it.size in 1..MAX_ANDROID_MANIFEST_SIZE_BYTES
            }
            ?: return@use false
        val checksum = CRC32()
        var bytesRead = 0L
        archive.getInputStream(manifest).use { input ->
            val buffer = ByteArray(DEFAULT_BUFFER_SIZE)
            var count: Int
            while (input.read(buffer).also { count = it } != -1) {
                checksum.update(buffer, 0, count)
                bytesRead += count
            }
        }
        bytesRead == manifest.size && checksum.value == manifest.crc
    }
}.getOrDefault(false)

/** Accepts only the official GitHub release asset endpoint used by the APK channel. */
internal fun isTrustedApkDownloadUrl(downloadUrl: String): Boolean = runCatching {
    val uri = URI(downloadUrl.trim())
    val path = uri.path
    uri.scheme.equals("https", ignoreCase = true) &&
        uri.host.equals("github.com", ignoreCase = true) &&
        uri.rawUserInfo == null &&
        (uri.port == -1 || uri.port == 443) &&
        path != null &&
        uri.rawPath == path &&
        uri.normalize().path == path &&
        path.startsWith("/raphaelpera85/mulletaflix-completo/releases/download/app-v") &&
        path.endsWith(".apk", ignoreCase = true)
}.getOrDefault(false)

/** Removes an incomplete update artifact without failing cleanup itself. */
internal fun deletePartialApk(file: File?) {
    runCatching {
        if (file?.exists() == true) {
            file.delete()
        }
    }
}
