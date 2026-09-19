package org.mulletaflix.domain.repository

import kotlinx.coroutines.flow.Flow

data class DownloadEntry(
    val id: String,
    val title: String,
    val uri: String,
    val state: DownloadState,
    val percent: Int,
    val error: String? = null,
)

enum class DownloadState { Queued, Downloading, Completed, Failed, Removing }

interface DownloadRepository {
    fun observeDownloads(): Flow<List<DownloadEntry>>
    fun observeWifiOnly(): Flow<Boolean> = kotlinx.coroutines.flow.flowOf(false)
    fun setWifiOnly(enabled: Boolean): Result<Unit> = Result.success(Unit)
    fun enqueue(id: String, title: String, uri: String): Result<Unit>
    fun retry(id: String, title: String, uri: String): Result<Unit>
    fun remove(id: String): Result<Unit>
    fun pauseAll(): Result<Unit>
    fun resumeAll(): Result<Unit>
}
