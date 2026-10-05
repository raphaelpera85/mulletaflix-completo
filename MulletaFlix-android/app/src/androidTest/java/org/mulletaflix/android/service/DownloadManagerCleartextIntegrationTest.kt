package org.mulletaflix.android.service

import android.content.Context
import android.database.sqlite.SQLiteDatabase
import android.database.sqlite.SQLiteOpenHelper
import androidx.media3.common.util.UnstableApi
import androidx.media3.datasource.cache.SimpleCache
import androidx.media3.database.DefaultDatabaseProvider
import androidx.media3.exoplayer.offline.Download
import androidx.media3.exoplayer.offline.DownloadManager
import androidx.media3.exoplayer.offline.DownloadRequest
import androidx.media3.exoplayer.scheduler.Requirements
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import java.io.BufferedReader
import java.io.File
import java.io.InputStreamReader
import java.net.InetAddress
import java.net.ServerSocket
import java.net.Socket
import java.util.concurrent.CopyOnWriteArrayList
import java.util.concurrent.ExecutorService
import java.util.concurrent.Executors
import java.util.concurrent.TimeUnit
import java.util.UUID
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.core.api.cleartextAwareMediaDataSourceFactory

@UnstableApi
@RunWith(AndroidJUnit4::class)
class DownloadManagerCleartextIntegrationTest {
    private lateinit var context: Context
    private lateinit var tempDirectory: File
    private lateinit var databaseName: String
    private lateinit var databaseHelper: SQLiteOpenHelper
    private lateinit var databaseProvider: DefaultDatabaseProvider
    private lateinit var cache: SimpleCache
    private lateinit var executor: ExecutorService
    private lateinit var server: LocalHttpServer
    private lateinit var downloadManager: DownloadManager
    private val terminalDownloads = CopyOnWriteArrayList<Pair<String, Int>>()

    @Before
    fun setUp() {
        context = InstrumentationRegistry.getInstrumentation().targetContext
        val testId = UUID.randomUUID().toString()
        tempDirectory = File(context.cacheDir, "download-transport-tests/$testId")
        assertTrue("Unable to create isolated cache directory", tempDirectory.mkdirs())
        databaseName = "download-transport-$testId.db"
        databaseHelper = object : SQLiteOpenHelper(context, databaseName, null, 1) {
            override fun onCreate(database: SQLiteDatabase) = Unit
            override fun onUpgrade(database: SQLiteDatabase, oldVersion: Int, newVersion: Int) = Unit
        }
        databaseProvider = DefaultDatabaseProvider(databaseHelper)
        cache = SimpleCache(tempDirectory, androidx.media3.datasource.cache.NoOpCacheEvictor(), databaseProvider)
        executor = Executors.newSingleThreadExecutor()
        server = LocalHttpServer().also(LocalHttpServer::start)
        downloadManager = DownloadManager(
            context,
            databaseProvider,
            cache,
            cleartextAwareMediaDataSourceFactory(connectTimeoutMs = 5_000, readTimeoutMs = 5_000),
            executor,
        ).apply {
            setMinRetryCount(0)
            setMaxParallelDownloads(1)
            setRequirements(Requirements(Requirements.NETWORK))
            addListener(object : DownloadManager.Listener {
                override fun onDownloadChanged(
                    manager: DownloadManager,
                    download: Download,
                    finalException: Exception?,
                ) {
                    if (download.state == Download.STATE_COMPLETED || download.state == Download.STATE_FAILED) {
                        terminalDownloads += download.request.id to download.state
                    }
                }
            })
            resumeDownloads()
        }
    }

    @After
    fun tearDown() {
        downloadManager.release()
        server.close()
        cache.release()
        SimpleCache.delete(tempDirectory, databaseProvider)
        databaseHelper.close()
        context.deleteDatabase(databaseName)
        executor.shutdownNow()
    }

    @Test
    fun localHttpDownloadCompletesAndPublicHttpRedirectFailsWithoutCachingBytes() {
        val localRequest = request("local", server.url("/movie"))
        downloadManager.addDownload(localRequest)

        awaitTerminalState(localRequest.id, Download.STATE_COMPLETED)
        val cacheDeadline = System.nanoTime() + TimeUnit.SECONDS.toNanos(5)
        while (cache.cacheSpace < PAYLOAD.size && System.nanoTime() < cacheDeadline) Thread.sleep(25)
        val cachedBytesAfterLocalDownload = cache.cacheSpace
        val localDownload = downloadManager.downloadIndex.getDownload(localRequest.id)
        assertTrue(
            "Expected downloaded bytes in isolated Media3 cache; cacheSpace=$cachedBytesAfterLocalDownload, bytesDownloaded=${localDownload?.bytesDownloaded}, files=${tempDirectory.listFiles()?.map { it.name to it.length() }}, requests=${server.requestPaths}",
            cachedBytesAfterLocalDownload >= PAYLOAD.size,
        )

        val redirectRequest = request("redirect", server.url("/redirect-public"))
        downloadManager.addDownload(redirectRequest)

        awaitTerminalState(redirectRequest.id, Download.STATE_FAILED)
        assertEquals("Only the local resource and redirect endpoint should reach the LAN server", listOf("/movie", "/redirect-public"), server.requestPaths.toList())
        assertTrue(
            "The blocked public redirect must not leave bytes in cache",
            cache.cacheSpace == cachedBytesAfterLocalDownload,
        )
    }

    private fun request(id: String, uri: String): DownloadRequest =
        DownloadRequest.Builder(id, android.net.Uri.parse(uri))
            .setMimeType("video/mp4")
            .setCustomCacheKey("transport-test-$id")
            .build()

    private fun awaitTerminalState(id: String, expected: Int) {
        val deadline = System.nanoTime() + TimeUnit.SECONDS.toNanos(30)
        while (System.nanoTime() < deadline) {
            val download = downloadManager.downloadIndex.getDownload(id)
            if (download?.state == expected) return
            Thread.sleep(50)
        }
        val current = downloadManager.downloadIndex.getDownload(id)
        throw AssertionError("Timed out waiting for download $id state=$expected; last=${current?.state}, terminal=$terminalDownloads")
    }

    private class LocalHttpServer : AutoCloseable {
        private val socket = ServerSocket(0, 4, InetAddress.getByName("127.0.0.1"))
        private val serverThread = Thread({ serve() }, "download-test-http-server").apply { isDaemon = true }
        val requestPaths = CopyOnWriteArrayList<String>()

        fun start() = serverThread.start()
        fun url(path: String) = "http://127.0.0.1:${socket.localPort}$path"

        private fun serve() {
            while (!socket.isClosed) {
                try {
                    socket.accept().use(::respond)
                } catch (error: Exception) {
                    if (!socket.isClosed) throw AssertionError("Local test server failed", error)
                }
            }
        }

        private fun respond(client: Socket) {
            val reader = BufferedReader(InputStreamReader(client.getInputStream(), Charsets.US_ASCII))
            val requestLine = reader.readLine() ?: return
            val path = requestLine.split(' ').getOrElse(1) { "/" }
            requestPaths += path
            while (reader.readLine()?.isNotEmpty() == true) { }

            val output = client.getOutputStream()
            if (path == "/redirect-public") {
                output.write("HTTP/1.1 302 Found\r\nLocation: http://203.0.113.66:8096/external/movie\r\nContent-Length: 0\r\nConnection: close\r\n\r\n".toByteArray(Charsets.US_ASCII))
            } else {
                output.write("HTTP/1.1 200 OK\r\nContent-Type: video/mp4\r\nContent-Length: ${PAYLOAD.size}\r\nConnection: close\r\n\r\n".toByteArray(Charsets.US_ASCII))
                output.write(PAYLOAD)
            }
            output.flush()
        }

        override fun close() {
            socket.close()
            serverThread.join(1_000)
        }
    }

    private companion object {
        val PAYLOAD = ByteArray(64 * 1024) { index -> (index % 251).toByte() }
    }
}
