package org.mulletaflix.domain.repository

import org.mulletaflix.domain.model.MediaItem

data class LiveTvChannel(
    val id: String,
    val name: String,
    val number: String?,
    val imageTag: String?,
    val currentProgram: String?,
)

interface LiveTvRepository {
    suspend fun getChannels(userId: String): Result<List<MediaItem>>

    /**
     * Programmes that **overlap** the window `[windowStartUtc, windowEndUtc]`.
     *
     * Overlap, not "starts inside": the server applies `MinEndDate` and
     * `MaxStartDate` for this, so a film that began before the window and is still
     * on the air is included. Filtering by start date alone hid exactly the
     * programme the viewer is watching right now.
     */
    suspend fun getPrograms(
        channelIds: List<String>,
        windowStartUtc: String?,
        windowEndUtc: String?,
    ): Result<List<MediaItem>>

    suspend fun getRecordings(userId: String): Result<List<MediaItem>>
    suspend fun scheduleRecording(program: MediaItem): Result<Unit>

    /**
     * Identifiers of the programmes that already have a pending recording on the
     * server.
     *
     * Without this the guide had no idea what was already scheduled: every time
     * the screen was reopened, a programme that was already set to record showed
     * "Gravar" again, and tapping it created a **second** timer on the server.
     */
    suspend fun getScheduledProgramIds(): Result<Set<String>>
}
