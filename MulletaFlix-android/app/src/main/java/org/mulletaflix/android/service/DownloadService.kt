package org.mulletaflix.android.service

import android.app.Notification
import android.content.Context
import androidx.media3.common.util.UnstableApi
import androidx.media3.exoplayer.offline.Download
import androidx.media3.exoplayer.offline.DownloadManager
import androidx.media3.exoplayer.offline.DownloadNotificationHelper
import androidx.media3.exoplayer.offline.DownloadService as Media3DownloadService
import org.mulletaflix.core.api.OfflineDownloadCache
import org.mulletaflix.android.R
import org.mulletaflix.feature.player.MEDIA_CONNECT_TIMEOUT_MS
import org.mulletaflix.feature.player.MEDIA_READ_TIMEOUT_MS
import java.util.concurrent.Executors

private const val CHANNEL_ID = "mulletaflix_downloads"
private const val NOTIFICATION_ID = 101

@UnstableApi
class DownloadService : Media3DownloadService(
    NOTIFICATION_ID,
    DEFAULT_FOREGROUND_NOTIFICATION_UPDATE_INTERVAL,
    CHANNEL_ID,
    R.string.app_name,
    0
) {
    override fun getDownloadManager(): DownloadManager = DownloadManagerSingleton.get(this)

    override fun getScheduler(): androidx.media3.exoplayer.scheduler.Scheduler? = null

    override fun getForegroundNotification(
        downloads: MutableList<Download>,
        notMetRequirements: Int
    ): Notification {
        val helper = DownloadNotificationHelper(this, CHANNEL_ID)
        return helper.buildProgressNotification(
            this,
            android.R.drawable.stat_sys_download,
            null,
            null,
            downloads,
            notMetRequirements
        )
    }
}

@UnstableApi
object DownloadManagerSingleton {
    private var downloadManager: DownloadManager? = null
    private val executor = Executors.newFixedThreadPool(
        downloadExecutorThreadCount(Runtime.getRuntime().availableProcessors()),
    )

    @Synchronized
    fun get(context: Context): DownloadManager {
        return downloadManager ?: run {
            val databaseProvider = androidx.media3.database.StandaloneDatabaseProvider(context)
            val downloadCache = OfflineDownloadCache.get(context)
            val upstreamFactory = androidx.media3.datasource.DefaultHttpDataSource.Factory()
                .setConnectTimeoutMs(MEDIA_CONNECT_TIMEOUT_MS)
                .setReadTimeoutMs(MEDIA_READ_TIMEOUT_MS)
                .setAllowCrossProtocolRedirects(true)
            DownloadManager(context, databaseProvider, downloadCache, upstreamFactory, executor).also {
                downloadManager = it
            }
        }
    }
}
