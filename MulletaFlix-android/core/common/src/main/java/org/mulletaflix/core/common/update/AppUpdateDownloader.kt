package org.mulletaflix.core.common.update

import android.content.Context
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.InternalCoroutinesApi
import kotlinx.coroutines.Job
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.flow
import kotlinx.coroutines.flow.flowOn
import okhttp3.Call
import okhttp3.OkHttpClient
import okhttp3.Request
import org.mulletaflix.core.common.network.enforceLocalNetworkCleartextPolicy
import java.io.File
import java.io.FileOutputStream
import java.util.concurrent.TimeUnit
import kotlinx.coroutines.DisposableHandle
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
class AppUpdateDownloader private constructor(
    private val cacheDirectory: () -> File,
    private val callFactory: Call.Factory,
    private val isTrustedDownloadUrl: (String) -> Boolean,
) {
    @Inject
    constructor(@ApplicationContext context: Context) : this(
        cacheDirectory = { context.cacheDir },
        callFactory = newHttpClient(),
        isTrustedDownloadUrl = ::isTrustedApkDownloadUrl,
    )

    internal constructor(
        cacheDirectory: File,
        callFactory: Call.Factory,
        isTrustedDownloadUrl: (String) -> Boolean,
    ) : this({ cacheDirectory }, callFactory, isTrustedDownloadUrl)

    /**
     * Downloads an APK from [downloadUrl] and streams progress updates.
     */
    @OptIn(InternalCoroutinesApi::class)
    fun downloadApk(
        downloadUrl: String,
        versionName: String,
        expectedSha256: String? = null,
    ): Flow<DownloadState> = flow {
        emit(DownloadState.Downloading(0f, 0L, -1L))

        var destinationFile: File? = null
        var completed = false
        var activeCall: Call? = null
        var cancellationHandle: DisposableHandle? = null
        var declaredContentLength = -1L
        var bytesDownloaded = 0L
        try {
            if (!isTrustedDownloadUrl(downloadUrl)) {
                emit(DownloadState.Error("A origem do APK não é confiável."))
                return@flow
            }

            val updatesDir = File(cacheDirectory(), "updates").apply { mkdirs() }
            val safeVersionName = versionName
                .trim()
                .replace(Regex("[^0-9A-Za-z._-]"), "_")
                .ifBlank { "unknown" }
            destinationFile = File(updatesDir, "mulletaflix-app-v$safeVersionName.apk")

            if (destinationFile.exists()) {
                destinationFile.delete()
            }

            val request = Request.Builder()
                .url(downloadUrl)
                .header("User-Agent", "MulletaFlix-Android-Downloader")
                .get()
                .build()

            val call = callFactory.newCall(request)
            activeCall = call
            cancellationHandle = currentCoroutineContext()[Job]?.invokeOnCompletion(
                onCancelling = true,
                invokeImmediately = true,
            ) { cause ->
                if (cause is CancellationException) call.cancel()
            }

            call.execute().use { response ->
                if (!response.isSuccessful) {
                    emit(DownloadState.Error("Falha no download do APK: HTTP ${response.code}"))
                    return@flow
                }

                val body = response.body
                declaredContentLength = body.contentLength()

                body.byteStream().use { input ->
                    FileOutputStream(destinationFile).use { output ->
                        val buffer = ByteArray(64 * 1024)
                        var bytesRead: Int

                        while (input.read(buffer).also { bytesRead = it } != -1) {
                            output.write(buffer, 0, bytesRead)
                            bytesDownloaded += bytesRead

                            val progress = if (declaredContentLength > 0) {
                                (bytesDownloaded.toFloat() / declaredContentLength.toFloat()).coerceIn(0f, 1f)
                            } else {
                                0f
                            }

                            emit(DownloadState.Downloading(progress, bytesDownloaded, declaredContentLength))
                        }
                        output.flush()
                    }
                }
            }

            if (declaredContentLength >= 0 && bytesDownloaded != declaredContentLength) {
                emit(DownloadState.Error("O APK baixado está incompleto."))
                return@flow
            }

            val downloadedFile = destinationFile
            if (downloadedFile.exists() && downloadedFile.length() > 0) {
                if (expectedSha256 != null && !sha256Matches(downloadedFile, expectedSha256)) {
                    deletePartialApk(downloadedFile)
                    emit(DownloadState.Error("A assinatura SHA-256 do APK não confere."))
                    return@flow
                }
                if (!hasValidAndroidApkManifestEntry(downloadedFile)) {
                    emit(DownloadState.Error("O manifesto do APK está ausente, vazio ou corrompido."))
                    return@flow
                }
                emit(DownloadState.Completed(downloadedFile))
                completed = true
            } else {
                emit(DownloadState.Error("Arquivo baixado está corrompido ou vazio."))
            }
        } catch (e: CancellationException) {
            throw e
        } catch (e: Exception) {
            emit(DownloadState.Error("Erro durante o download: ${e.localizedMessage ?: e.message}"))
        } finally {
            cancellationHandle?.dispose()
            if (!completed) {
                activeCall?.cancel()
                deletePartialApk(destinationFile)
            }
        }
    }.flowOn(Dispatchers.IO)

    private companion object {
        fun newHttpClient(): OkHttpClient = OkHttpClient.Builder()
            .enforceLocalNetworkCleartextPolicy(
                requireHttpsRedirects = true,
                allowedHttpsRedirectHosts = setOf("github.com", "release-assets.githubusercontent.com"),
            )
            .connectTimeout(30, TimeUnit.SECONDS)
            .readTimeout(180, TimeUnit.SECONDS)
            .build()
    }
}
