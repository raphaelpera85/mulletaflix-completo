package org.mulletaflix.core.common.update

import android.content.Context
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.flow
import kotlinx.coroutines.flow.flowOn
import okhttp3.OkHttpClient
import okhttp3.Request
import java.io.File
import java.io.FileOutputStream
import java.util.concurrent.TimeUnit
import javax.inject.Inject
import javax.inject.Singleton

sealed interface DownloadState {
    data object Idle : DownloadState
    data class Downloading(
        val progress: Float,
        val bytesDownloaded: Long,
        val totalBytes: Long
    ) : DownloadState
    data class Completed(val file: File) : DownloadState
    data class Error(val message: String) : DownloadState
}

@Singleton
class AppUpdateDownloader @Inject constructor(
    @ApplicationContext private val context: Context,
) {
    private val httpClient = OkHttpClient.Builder()
        .connectTimeout(30, TimeUnit.SECONDS)
        .readTimeout(180, TimeUnit.SECONDS)
        .build()

    /**
     * Downloads an APK from [downloadUrl] and streams progress updates.
     */
    fun downloadApk(
        downloadUrl: String,
        versionName: String,
        expectedSha256: String? = null,
    ): Flow<DownloadState> = flow {
        emit(DownloadState.Downloading(0f, 0L, -1L))

        try {
            val updatesDir = File(context.cacheDir, "updates").apply { mkdirs() }
            val destinationFile = File(updatesDir, "mulletaflix-app-v$versionName.apk")

            if (destinationFile.exists()) {
                destinationFile.delete()
            }

            val request = Request.Builder()
                .url(downloadUrl)
                .header("User-Agent", "MulletaFlix-Android-Downloader")
                .get()
                .build()

            val response = httpClient.newCall(request).execute()
            if (!response.isSuccessful) {
                emit(DownloadState.Error("Falha no download do APK: HTTP ${response.code}"))
                return@flow
            }

            val body = response.body
            if (body == null) {
                emit(DownloadState.Error("Resposta vazia ao baixar o APK."))
                return@flow
            }

            val totalBytes = body.contentLength()
            var bytesDownloaded = 0L

            body.byteStream().use { input ->
                FileOutputStream(destinationFile).use { output ->
                    val buffer = ByteArray(64 * 1024)
                    var bytesRead: Int

                    while (input.read(buffer).also { bytesRead = it } != -1) {
                        output.write(buffer, 0, bytesRead)
                        bytesDownloaded += bytesRead

                        val progress = if (totalBytes > 0) {
                            (bytesDownloaded.toFloat() / totalBytes.toFloat()).coerceIn(0f, 1f)
                        } else {
                            0f
                        }

                        emit(DownloadState.Downloading(progress, bytesDownloaded, totalBytes))
                    }
                    output.flush()
                }
            }

            if (destinationFile.exists() && destinationFile.length() > 0) {
                if (expectedSha256 != null && !sha256Matches(destinationFile, expectedSha256)) {
                    destinationFile.delete()
                    emit(DownloadState.Error("A assinatura SHA-256 do APK não confere."))
                    return@flow
                }
                emit(DownloadState.Completed(destinationFile))
            } else {
                emit(DownloadState.Error("Arquivo baixado está corrompido ou vazio."))
            }
        } catch (e: Exception) {
            emit(DownloadState.Error("Erro durante o download: ${e.localizedMessage ?: e.message}"))
        }
    }.flowOn(Dispatchers.IO)
}
