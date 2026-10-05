package org.mulletaflix.core.api

import android.content.Context
import android.database.sqlite.SQLiteDatabase
import android.database.sqlite.SQLiteOpenHelper
import android.net.Uri
import androidx.media3.common.util.UnstableApi
import androidx.media3.datasource.cache.NoOpCacheEvictor
import androidx.media3.datasource.cache.SimpleCache
import androidx.media3.database.DefaultDatabaseProvider
import androidx.media3.exoplayer.offline.Download
import androidx.media3.exoplayer.offline.DownloadManager
import androidx.media3.exoplayer.offline.DownloadRequest
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import java.io.File
import java.net.InetAddress
import java.util.UUID
import java.util.concurrent.CopyOnWriteArrayList
import java.util.concurrent.Executors
import java.util.concurrent.TimeUnit
import okhttp3.Dns
import okhttp3.OkHttpClient
import okhttp3.tls.HandshakeCertificates
import okhttp3.tls.HeldCertificate
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import okio.Buffer
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.core.common.network.enforceLocalNetworkCleartextPolicy

@UnstableApi
@RunWith(AndroidJUnit4::class)
class DownloadManagerHttpsIntegrationTest {
    private lateinit var context: Context
    private lateinit var cacheDirectory: File
    private lateinit var databaseName: String
    private lateinit var databaseHelper: SQLiteOpenHelper
    private lateinit var databaseProvider: DefaultDatabaseProvider
    private lateinit var cache: SimpleCache
    private lateinit var server: MockWebServer
    private lateinit var downloadManager: DownloadManager
    private val executor = Executors.newSingleThreadExecutor()
    private val terminalStates = CopyOnWriteArrayList<Pair<String, Int>>()

    @Before
    fun setUp() {
        context = InstrumentationRegistry.getInstrumentation().targetContext
        val testId = UUID.randomUUID().toString()
        cacheDirectory = File(context.cacheDir, "https-download-tests/$testId")
        assertTrue("Unable to create isolated cache directory", cacheDirectory.mkdirs())
        databaseName = "https-download-$testId.db"
        databaseHelper = object : SQLiteOpenHelper(context, databaseName, null, 1) {
            override fun onCreate(database: SQLiteDatabase) = Unit
            override fun onUpgrade(database: SQLiteDatabase, oldVersion: Int, newVersion: Int) = Unit
        }
        databaseProvider = DefaultDatabaseProvider(databaseHelper)
        cache = SimpleCache(cacheDirectory, NoOpCacheEvictor(), databaseProvider)

        val certificate = HeldCertificate.Builder()
            .addSubjectAlternativeName(HOST)
            .build()
        val serverTls = HandshakeCertificates.Builder()
            .heldCertificate(certificate)
            .build()
        val clientTls = HandshakeCertificates.Builder()
            .addTrustedCertificate(certificate.certificate)
            .build()
        server = MockWebServer().apply {
            useHttps(serverTls.sslSocketFactory(), false)
            enqueue(MockResponse().setBody(Buffer().write(PAYLOAD)))
            start(InetAddress.getByName("127.0.0.1"), 0)
        }
        val client = OkHttpClient.Builder()
            .sslSocketFactory(clientTls.sslSocketFactory(), clientTls.trustManager)
            .dns(object : Dns {
                override fun lookup(hostname: String): List<InetAddress> =
                    if (hostname == HOST) listOf(InetAddress.getByName("127.0.0.1"))
                    else Dns.SYSTEM.lookup(hostname)
            })
            .enforceLocalNetworkCleartextPolicy()
            .build()
        downloadManager = DownloadManager(
            context,
            databaseProvider,
            cache,
            cleartextAwareMediaDataSourceFactory(client),
            executor,
        ).apply {
            setMinRetryCount(0)
            setMaxParallelDownloads(1)
            addListener(object : DownloadManager.Listener {
                override fun onDownloadChanged(
                    manager: DownloadManager,
                    download: Download,
                    finalException: Exception?,
                ) {
                    if (download.state == Download.STATE_COMPLETED || download.state == Download.STATE_FAILED) {
                        terminalStates += download.request.id to download.state
                    }
                }
            })
            resumeDownloads()
        }
    }

    @After
    fun tearDown() {
        downloadManager.release()
        server.shutdown()
        cache.release()
        SimpleCache.delete(cacheDirectory, databaseProvider)
        databaseHelper.close()
        context.deleteDatabase(databaseName)
        executor.shutdownNow()
    }

    @Test
    fun remoteHttpsDownloadCompletesAndPersistsBytesInMedia3Cache() {
        val uri = server.url("/media/movie.mp4")
            .newBuilder()
            .host(HOST)
            .build()
        val request = DownloadRequest.Builder("remote-https", Uri.parse(uri.toString()))
            .setMimeType("video/mp4")
            .build()
        downloadManager.addDownload(request)

        val deadline = System.nanoTime() + TimeUnit.SECONDS.toNanos(15)
        while (System.nanoTime() < deadline && terminalStates.none { it.first == request.id }) {
            Thread.sleep(25)
        }
        assertEquals("HTTPS download did not complete", Download.STATE_COMPLETED,
            terminalStates.lastOrNull { it.first == request.id }?.second)
        val download = downloadManager.downloadIndex.getDownload(request.id)
        assertEquals(PAYLOAD.size.toLong(), download?.bytesDownloaded)
        assertTrue("Expected downloaded HTTPS bytes in isolated cache", cache.cacheSpace >= PAYLOAD.size)
        assertEquals(1, server.requestCount)
        assertEquals("/media/movie.mp4", server.takeRequest(1, TimeUnit.SECONDS)?.path)
    }

    private companion object {
        const val HOST = "media.example.org"
        val PAYLOAD = ByteArray(64 * 1024) { (it % 251).toByte() }
    }
}
