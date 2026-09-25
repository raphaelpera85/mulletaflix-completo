package org.mulletaflix.core.api

import android.content.Context
import androidx.media3.common.util.UnstableApi
import androidx.media3.database.StandaloneDatabaseProvider
import androidx.media3.datasource.cache.Cache
import androidx.media3.datasource.cache.NoOpCacheEvictor
import androidx.media3.datasource.cache.SimpleCache
import java.io.File

/** Durable cache shared by DownloadManager and the offline player. */
@UnstableApi
object OfflineDownloadCache {
    @Volatile
    private var cache: SimpleCache? = null

    @Synchronized
    fun get(context: Context): Cache = cache ?: run {
        val appContext = context.applicationContext
        val cacheDirectory = resolveOfflineDownloadCacheDirectory(
            cacheDirectory = appContext.cacheDir,
            noBackupFilesDirectory = appContext.noBackupFilesDir,
        )
        SimpleCache(
            cacheDirectory,
            NoOpCacheEvictor(),
            StandaloneDatabaseProvider(appContext),
        ).also { cache = it }
    }
}

/** Resolves the durable cache while preserving files from older app versions. */
internal fun resolveOfflineDownloadCacheDirectory(
    cacheDirectory: File,
    noBackupFilesDirectory: File,
): File {
    val legacyDirectory = File(cacheDirectory, "downloads")
    val persistentDirectory = File(noBackupFilesDirectory, "downloads")

    if (persistentDirectory.isDirectory) {
        if (!legacyDirectory.isDirectory) return persistentDirectory
        return if (mergeLegacyDownloadCacheFiles(legacyDirectory, persistentDirectory)) {
            persistentDirectory
        } else {
            // A same-name/different-content cache span is ambiguous; keep the old cache active.
            legacyDirectory
        }
    }
    if (persistentDirectory.exists()) {
        return legacyDirectory.takeIf { it.isDirectory } ?: persistentDirectory
    }
    if (!legacyDirectory.exists()) return persistentDirectory
    if (!legacyDirectory.isDirectory) return legacyDirectory

    if (!noBackupFilesDirectory.isDirectory && !noBackupFilesDirectory.mkdirs()) {
        return legacyDirectory
    }
    return if (legacyDirectory.renameTo(persistentDirectory)) persistentDirectory else legacyDirectory
}

/** Moves non-conflicting cache spans; returns false without deleting either conflicting file. */
private fun mergeLegacyDownloadCacheFiles(legacyDirectory: File, persistentDirectory: File): Boolean {
    val legacyFiles = legacyDirectory.listFiles() ?: return false
    for (legacyFile in legacyFiles) {
        val persistentFile = File(persistentDirectory, legacyFile.name)
        if (persistentFile.exists()) {
            if (!legacyFile.isFile || !persistentFile.isFile || !legacyFile.contentEquals(persistentFile)) {
                return false
            }
            continue
        }
        if (!legacyFile.renameTo(persistentFile)) return false
    }
    return true
}

private fun File.contentEquals(other: File): Boolean {
    if (length() != other.length()) return false
    var equal = true
    inputStream().use { left ->
        other.inputStream().use { right ->
            val leftBuffer = ByteArray(DEFAULT_BUFFER_SIZE)
            val rightBuffer = ByteArray(DEFAULT_BUFFER_SIZE)
            while (true) {
                val leftCount = left.read(leftBuffer)
                val rightCount = right.read(rightBuffer)
                if (leftCount != rightCount) {
                    equal = false
                    break
                }
                if (leftCount < 0) break
                for (index in 0 until leftCount) {
                    if (leftBuffer[index] != rightBuffer[index]) {
                        equal = false
                        break
                    }
                }
                if (!equal) break
            }
        }
    }
    return equal
}
