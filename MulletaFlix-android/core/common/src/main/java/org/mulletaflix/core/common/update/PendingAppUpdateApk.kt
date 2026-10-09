package org.mulletaflix.core.common.update

import java.io.File
import java.security.MessageDigest
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.CoroutineDispatcher
import kotlinx.coroutines.withContext

/** Keeps a completed APK available for an install retry in the current ViewModel session. */
class PendingAppUpdateApk(private val ioDispatcher: CoroutineDispatcher = Dispatchers.IO) {

    private data class Entry(
        val file: File,
        val versionName: String,
        val downloadUrl: String,
        val expectedSha256: String?,
        val verifiedSha256: String,
    )

    private var entry: Entry? = null

    suspend fun remember(file: File, versionName: String, downloadUrl: String, expectedSha256: String?) {
        val verifiedSha256 = withContext(ioDispatcher) { file.sha256OrNull() } ?: run {
            entry = null
            return
        }
        if (expectedSha256 != null && !verifiedSha256.equals(expectedSha256, ignoreCase = true)) {
            entry = null
            return
        }
        entry = Entry(file, versionName, downloadUrl, expectedSha256, verifiedSha256)
    }

    suspend fun findFor(versionName: String, downloadUrl: String, expectedSha256: String?): File? {
        val cached = entry ?: return null
        val matchesUpdate = cached.versionName == versionName &&
            cached.downloadUrl == downloadUrl &&
            cached.expectedSha256 == expectedSha256
        if (!matchesUpdate) {
            entry = null
            return null
        }
        val actualSha256 = withContext(ioDispatcher) { cached.file.sha256OrNull() }
        if (actualSha256 == null || !actualSha256.equals(cached.verifiedSha256, ignoreCase = true)) {
            entry = null
            return null
        }
        return cached.file
    }

    fun clear() {
        entry = null
    }
}

private fun File.sha256OrNull(): String? = runCatching {
    if (!isFile) return null
    val digest = MessageDigest.getInstance("SHA-256")
    inputStream().buffered().use { stream ->
        val buffer = ByteArray(DEFAULT_BUFFER_SIZE)
        while (true) {
            val read = stream.read(buffer)
            if (read < 0) break
            digest.update(buffer, 0, read)
        }
    }
    digest.digest().joinToString("") { byte -> "%02x".format(byte) }
}.getOrNull()
