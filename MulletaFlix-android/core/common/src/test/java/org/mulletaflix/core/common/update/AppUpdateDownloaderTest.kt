package org.mulletaflix.core.common.update

import java.io.File
import java.io.IOException
import java.io.ByteArrayOutputStream
import java.security.MessageDigest
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicReference
import java.util.concurrent.locks.ReentrantLock
import java.util.zip.CRC32
import java.util.zip.ZipEntry
import java.util.zip.ZipOutputStream
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.cancelAndJoin
import kotlinx.coroutines.launch
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.withTimeout
import okhttp3.Call
import okhttp3.OkHttpClient
import okhttp3.Protocol
import okhttp3.Request
import okhttp3.Response
import okhttp3.ResponseBody
import okio.Buffer
import okio.BufferedSource
import okio.Source
import okio.Timeout
import okio.buffer
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.rules.TemporaryFolder

class AppUpdateDownloaderTest {
    @get:Rule
    val temporaryFolder = TemporaryFolder()

    @Test
    fun `cancelling while response body read is blocked cancels call and removes partial apk`() = runBlocking {
        val callCreated = CountDownLatch(1)
        val activeCall = AtomicReference<StalledBodyCall>()
        val downloader = AppUpdateDownloader(
            cacheDirectory = temporaryFolder.root,
            callFactory = Call.Factory { request ->
                StalledBodyCall(request).also {
                    activeCall.set(it)
                    callCreated.countDown()
                }
            },
            isTrustedDownloadUrl = { true },
        )
        val partialFile = File(temporaryFolder.root, "updates/mulletaflix-app-vtest-cancel.apk")
        val download: Job = launch(Dispatchers.Default) {
            downloader.downloadApk("https://github.com/example/app.apk", "test-cancel").collect { }
        }

        try {
            assertTrue("Downloader must create its OkHttp call", callCreated.await(5, TimeUnit.SECONDS))
            val call = requireNotNull(activeCall.get())
            assertTrue("Downloader must reach the deliberately blocked body read", call.awaitBlockedRead())
            assertTrue("The test must create a partial file before cancellation", partialFile.length() > 0)

            withTimeout(2_000) { download.cancelAndJoin() }

            assertTrue("Cancelling the flow must cancel its active OkHttp call", call.isCanceled())
            assertFalse("Cancelled downloads must not leave a partial APK", partialFile.exists())
        } finally {
            download.cancel()
            activeCall.get()?.cancel()
        }
    }

    @Test
    fun `inconsistent response body is rejected and partial apk is removed`() = runBlocking {
        val requestBody = byteArrayOf(0x50, 0x4b, 0x03)
        val partialFile = File(temporaryFolder.root, "updates/mulletaflix-app-vtruncated.apk")
        val downloader = AppUpdateDownloader(
            cacheDirectory = temporaryFolder.root,
            callFactory = Call.Factory { request ->
                FixedResponseCall(request, responseBody(requestBody, declaredLength = 4))
            },
            isTrustedDownloadUrl = { true },
        )
        val states = mutableListOf<DownloadState>()

        downloader.downloadApk("https://github.com/example/app.apk", "truncated").collect(states::add)

        assertTrue("A response shorter than Content-Length must fail", states.last() is DownloadState.Error)
        assertFalse("A truncated APK must never be marked complete", states.any { it is DownloadState.Completed })
        assertFalse("A truncated APK must be removed", partialFile.exists())
    }

    @Test
    fun `non apk response body is rejected and partial file is removed`() = runBlocking {
        val responseBytes = "<!doctype html><title>Not an APK</title>".toByteArray()
        val partialFile = File(temporaryFolder.root, "updates/mulletaflix-app-vinvalid.apk")
        val downloader = AppUpdateDownloader(
            cacheDirectory = temporaryFolder.root,
            callFactory = Call.Factory { request ->
                FixedResponseCall(request, responseBody(responseBytes, responseBytes.size.toLong()))
            },
            isTrustedDownloadUrl = { true },
        )
        val states = mutableListOf<DownloadState>()

        downloader.downloadApk("https://github.com/example/app.apk", "invalid").collect(states::add)

        assertTrue("A non-ZIP response must report a download error", states.last() is DownloadState.Error)
        assertFalse("A non-APK response must never be marked complete", states.any { it is DownloadState.Completed })
        assertFalse("A non-APK response must be removed", partialFile.exists())
    }

    @Test
    fun `apk archive with empty manifest is rejected`() = runBlocking {
        val responseBytes = ByteArrayOutputStream().use { bytes ->
            ZipOutputStream(bytes).use { archive ->
                archive.putNextEntry(ZipEntry("AndroidManifest.xml"))
                archive.closeEntry()
            }
            bytes.toByteArray()
        }
        val partialFile = File(temporaryFolder.root, "updates/mulletaflix-app-vempty-manifest.apk")
        val downloader = AppUpdateDownloader(
            cacheDirectory = temporaryFolder.root,
            callFactory = Call.Factory { request ->
                FixedResponseCall(request, responseBody(responseBytes, responseBytes.size.toLong()))
            },
            isTrustedDownloadUrl = { true },
        )
        val states = mutableListOf<DownloadState>()

        downloader.downloadApk("https://github.com/example/app.apk", "empty-manifest").collect(states::add)

        assertTrue("An APK with an empty manifest must fail validation", states.last() is DownloadState.Error)
        assertFalse("An APK with an empty manifest must never be marked complete", states.any { it is DownloadState.Completed })
        assertFalse("An APK with an empty manifest must be removed", partialFile.exists())
    }

    @Test
    fun `apk with corrupted manifest entry is rejected and partial file is removed`() = runBlocking {
        val manifest = byteArrayOf(0x03, 0x00, 0x08, 0x00)
        val apkBytes = ByteArrayOutputStream().use { bytes ->
            ZipOutputStream(bytes).use { archive ->
                val checksum = CRC32().apply { update(manifest) }
                val entry = ZipEntry("AndroidManifest.xml").apply {
                    method = ZipEntry.STORED
                    size = manifest.size.toLong()
                    compressedSize = manifest.size.toLong()
                    crc = checksum.value
                }
                archive.putNextEntry(entry)
                archive.write(manifest)
                archive.closeEntry()
            }
            bytes.toByteArray()
        }.also { bytes ->
            val fileNameLength = bytes.readUnsignedShortLittleEndian(26)
            val extraLength = bytes.readUnsignedShortLittleEndian(28)
            val manifestDataOffset = 30 + fileNameLength + extraLength
            bytes[manifestDataOffset] = (bytes[manifestDataOffset].toInt() xor 0xff).toByte()
        }
        val partialFile = File(temporaryFolder.root, "updates/mulletaflix-app-vcorrupted-manifest.apk")
        val downloader = AppUpdateDownloader(
            cacheDirectory = temporaryFolder.root,
            callFactory = Call.Factory { request ->
                FixedResponseCall(request, responseBody(apkBytes, apkBytes.size.toLong()))
            },
            isTrustedDownloadUrl = { true },
        )
        val states = mutableListOf<DownloadState>()

        downloader.downloadApk("https://github.com/example/app.apk", "corrupted-manifest").collect(states::add)

        assertTrue("A corrupted APK manifest must report an error", states.last() is DownloadState.Error)
        assertFalse("A corrupted APK manifest must never be marked complete", states.any { it is DownloadState.Completed })
        assertFalse("A corrupted APK must be removed", partialFile.exists())
    }

    @Test
    fun `valid apk reports byte progress and completes only after checksum validation`() = runBlocking {
        val apkBytes = validApkBytes()
        val expectedSha256 = MessageDigest.getInstance("SHA-256")
            .digest(apkBytes)
            .joinToString("") { "%02x".format(it) }
        val downloader = AppUpdateDownloader(
            cacheDirectory = temporaryFolder.root,
            callFactory = Call.Factory { request ->
                FixedResponseCall(request, responseBody(apkBytes, apkBytes.size.toLong()))
            },
            isTrustedDownloadUrl = { true },
        )
        val states = mutableListOf<DownloadState>()

        downloader.downloadApk("https://github.com/example/app.apk", "success", expectedSha256)
            .collect(states::add)

        val progress = states.filterIsInstance<DownloadState.Downloading>()
            .filter { it.bytesDownloaded > 0L }
        assertTrue("A successful download must report byte progress", progress.isNotEmpty())
        assertTrue("Reported progress must be monotonic", progress.zipWithNext().all { (a, b) ->
            b.bytesDownloaded >= a.bytesDownloaded && b.progress >= a.progress
        })
        assertTrue("The completed state must contain the validated APK", states.last() is DownloadState.Completed)
        val completedFile = (states.last() as DownloadState.Completed).file
        assertTrue(completedFile.isFile)
        assertTrue(sha256Matches(completedFile, expectedSha256))
        assertTrue(hasValidAndroidApkManifestEntry(completedFile))
    }

    @Test
    fun `checksum mismatch reports error and removes downloaded apk`() = runBlocking {
        val apkBytes = validApkBytes()
        val partialFile = File(temporaryFolder.root, "updates/mulletaflix-app-vwrong-checksum.apk")
        val downloader = AppUpdateDownloader(
            cacheDirectory = temporaryFolder.root,
            callFactory = Call.Factory { request ->
                FixedResponseCall(request, responseBody(apkBytes, apkBytes.size.toLong()))
            },
            isTrustedDownloadUrl = { true },
        )
        val states = mutableListOf<DownloadState>()

        downloader.downloadApk(
            "https://github.com/example/app.apk",
            "wrong-checksum",
            "0".repeat(64),
        ).collect(states::add)

        assertTrue("A mismatched digest must report an error", states.last() is DownloadState.Error)
        assertFalse("A mismatched APK must never be marked complete", states.any { it is DownloadState.Completed })
        assertFalse("A mismatched APK must be deleted", partialFile.exists())
    }

    @Test
    fun `empty successful response reports error and removes the empty apk`() = runBlocking {
        val partialFile = File(temporaryFolder.root, "updates/mulletaflix-app-vempty-response.apk")
        val downloader = AppUpdateDownloader(
            cacheDirectory = temporaryFolder.root,
            callFactory = Call.Factory { request ->
                FixedResponseCall(request, responseBody(byteArrayOf(), declaredLength = 0L))
            },
            isTrustedDownloadUrl = { true },
        )
        val states = mutableListOf<DownloadState>()

        downloader.downloadApk("https://github.com/example/app.apk", "empty-response").collect(states::add)

        assertTrue("An empty HTTP 200 response must report an error", states.last() is DownloadState.Error)
        assertFalse("An empty response must never be marked complete", states.any { it is DownloadState.Completed })
        assertFalse("The empty APK placeholder must be removed", partialFile.exists())
    }

    @Test
    fun `http failure reports status and removes response target`() = runBlocking {
        val partialFile = File(temporaryFolder.root, "updates/mulletaflix-app-vhttp-failure.apk")
        val downloader = AppUpdateDownloader(
            cacheDirectory = temporaryFolder.root,
            callFactory = Call.Factory { request ->
                FixedResponseCall(
                    request,
                    responseBody("unavailable".toByteArray(), 11L),
                    responseCode = 503,
                )
            },
            isTrustedDownloadUrl = { true },
        )
        val states = mutableListOf<DownloadState>()

        downloader.downloadApk("https://github.com/example/app.apk", "http-failure").collect(states::add)

        assertEquals(DownloadState.Error("Falha no download do APK: HTTP 503"), states.last())
        assertFalse("An HTTP error must never be marked complete", states.any { it is DownloadState.Completed })
        assertFalse("An HTTP error must not leave a target file", partialFile.exists())
    }

    @Test
    fun `untrusted url is rejected before creating an okhttp call`() = runBlocking {
        var callsCreated = 0
        val downloader = AppUpdateDownloader(
            cacheDirectory = temporaryFolder.root,
            callFactory = Call.Factory { request ->
                callsCreated++
                FixedResponseCall(request, responseBody(byteArrayOf(), 0L))
            },
            isTrustedDownloadUrl = { false },
        )
        val states = mutableListOf<DownloadState>()

        downloader.downloadApk("https://evil.example/app.apk", "untrusted").collect(states::add)

        assertEquals(0, callsCreated)
        assertTrue(states.last() is DownloadState.Error)
        assertFalse(File(temporaryFolder.root, "updates/mulletaflix-app-vuntrusted.apk").exists())
    }

    private class StalledBodyCall(private val requestValue: Request) : Call by OkHttpClient().newCall(requestValue) {
        private val blockedRead = CountDownLatch(1)
        private val readLock = ReentrantLock()
        private val readChanged = readLock.newCondition()
        private var isCanceledUnderLock = false
        @Volatile private var canceled = false
        private val body = object : ResponseBody() {
            private var readCount = 0
            private val bufferedSource: BufferedSource = object : Source {
                override fun read(sink: Buffer, byteCount: Long): Long {
                    if (readCount++ == 0) {
                        sink.writeByte('x'.code)
                        return 1
                    }
                    readLock.lock()
                    try {
                        blockedRead.countDown()
                        val deadline = System.nanoTime() + TimeUnit.SECONDS.toNanos(10)
                        while (!isCanceledUnderLock) {
                            val remaining = deadline - System.nanoTime()
                            if (remaining <= 0) {
                                throw IOException("Test call was not cancelled while reading the stalled body")
                            }
                            readChanged.awaitNanos(remaining)
                        }
                    } finally {
                        readLock.unlock()
                    }
                    throw IOException("Call cancelled")
                }

                override fun timeout(): Timeout = Timeout.NONE

                override fun close() {
                    cancel()
                }
            }.buffer()

            override fun contentType() = null
            override fun contentLength(): Long = 128
            override fun source(): BufferedSource = bufferedSource
        }

        override fun request(): Request = requestValue

        override fun execute(): Response {
            return Response.Builder()
                .request(requestValue)
                .protocol(Protocol.HTTP_1_1)
                .code(200)
                .message("OK")
                .body(body)
                .build()
        }

        override fun cancel() {
            canceled = true
            readLock.lock()
            try {
                isCanceledUnderLock = true
                readChanged.signalAll()
            } finally {
                readLock.unlock()
            }
        }

        override fun isCanceled(): Boolean = canceled

        fun awaitBlockedRead(): Boolean {
            if (!blockedRead.await(5, TimeUnit.SECONDS)) return false
            readLock.lock()
            try {
                // Acquiring this lock proves the reader has entered timedWait and released it.
                return true
            } finally {
                readLock.unlock()
            }
        }
    }

    private class FixedResponseCall(
        private val requestValue: Request,
        private val responseBody: ResponseBody,
        private val responseCode: Int = 200,
    ) : Call by OkHttpClient().newCall(requestValue) {
        override fun request(): Request = requestValue

        override fun execute(): Response = Response.Builder()
            .request(requestValue)
            .protocol(Protocol.HTTP_1_1)
            .code(responseCode)
            .message(if (responseCode == 200) "OK" else "HTTP error")
            .body(responseBody)
            .build()
    }

    private fun responseBody(bytes: ByteArray, declaredLength: Long): ResponseBody = object : ResponseBody() {
        private val bufferedSource = Buffer().write(bytes)

        override fun contentType() = null
        override fun contentLength(): Long = declaredLength
        override fun source(): BufferedSource = bufferedSource
    }

    private fun ByteArray.readUnsignedShortLittleEndian(offset: Int): Int =
        (this[offset].toInt() and 0xff) or ((this[offset + 1].toInt() and 0xff) shl 8)

    private fun validApkBytes(): ByteArray = ByteArrayOutputStream().use { bytes ->
        ZipOutputStream(bytes).use { archive ->
            archive.putNextEntry(ZipEntry("AndroidManifest.xml"))
            archive.write(byteArrayOf(0x03, 0x00, 0x08, 0x00))
            archive.closeEntry()
        }
        bytes.toByteArray()
    }
}
