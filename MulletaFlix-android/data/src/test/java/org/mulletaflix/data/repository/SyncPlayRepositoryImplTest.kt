package org.mulletaflix.data.repository

import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.mockk
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.core.api.MulletaFlixApiService
import org.mulletaflix.core.api.dto.GroupInfoDto
import org.mulletaflix.core.api.dto.JoinGroupRequestDto
import org.mulletaflix.core.api.dto.NewGroupRequestDto
import org.mulletaflix.core.api.dto.SyncPlayPlaybackStatusDto
import org.mulletaflix.domain.repository.SyncPlayPlaybackStatus

class SyncPlayRepositoryImplTest {

    private val api = mockk<MulletaFlixApiService>()
    private val repository = SyncPlayRepositoryImpl(api)

    @Test
    fun getGroups_returnsMappedSyncPlayGroups() = runTest {
        coEvery { api.getSyncPlayGroups() } returns listOf(
            GroupInfoDto(
                groupId = "group-42",
                groupName = "Family Movie Night",
                state = "Playing",
                participants = listOf("User1", "User2"),
            )
        )

        val result = repository.getGroups()

        assertTrue(result.isSuccess)
        val groups = result.getOrThrow()
        assertEquals(1, groups.size)
        assertEquals("group-42", groups[0].groupId)
        assertEquals("Family Movie Night", groups[0].groupName)
        assertEquals("Playing", groups[0].state)
        assertEquals(2, groups[0].participants.size)
    }

    @Test
    fun createGroup_sendsNewGroupRequest() = runTest {
        coEvery { api.createSyncPlayGroup(NewGroupRequestDto(groupName = "Cinema")) } returns Unit

        val result = repository.createGroup("Cinema")

        assertTrue(result.isSuccess)
        coVerify(exactly = 1) { api.createSyncPlayGroup(NewGroupRequestDto(groupName = "Cinema")) }
    }

    @Test
    fun joinGroup_sendsJoinRequest() = runTest {
        coEvery { api.joinSyncPlayGroup(JoinGroupRequestDto(groupId = "group-1")) } returns Unit

        val result = repository.joinGroup("group-1")

        assertTrue(result.isSuccess)
        coVerify(exactly = 1) { api.joinSyncPlayGroup(JoinGroupRequestDto(groupId = "group-1")) }
    }

    @Test
    fun leaveGroup_callsApiLeave() = runTest {
        coEvery { api.leaveSyncPlayGroup() } returns Unit

        val result = repository.leaveGroup()

        assertTrue(result.isSuccess)
        coVerify(exactly = 1) { api.leaveSyncPlayGroup() }
    }

    @Test
    fun reportBuffering_mapsAllFieldsToServerContract() = runTest {
        val status = samplePlaybackStatus()
        coEvery { api.reportSyncPlayBuffering(any()) } returns Unit

        val result = repository.reportBuffering(status)

        assertTrue(result.isSuccess)
        coVerify(exactly = 1) {
            api.reportSyncPlayBuffering(
                SyncPlayPlaybackStatusDto(
                    whenUtc = status.whenUtc,
                    positionTicks = status.positionTicks,
                    isPlaying = status.isPlaying,
                    playlistItemId = status.playlistItemId,
                ),
            )
        }
    }

    @Test
    fun reportReady_mapsAllFieldsToServerContract() = runTest {
        val status = samplePlaybackStatus()
        coEvery { api.reportSyncPlayReady(any()) } returns Unit

        val result = repository.reportReady(status)

        assertTrue(result.isSuccess)
        coVerify(exactly = 1) {
            api.reportSyncPlayReady(
                SyncPlayPlaybackStatusDto(
                    whenUtc = status.whenUtc,
                    positionTicks = status.positionTicks,
                    isPlaying = status.isPlaying,
                    playlistItemId = status.playlistItemId,
                ),
            )
        }
    }

    private fun samplePlaybackStatus() = SyncPlayPlaybackStatus(
        whenUtc = "2026-09-24T12:30:00.000Z",
        positionTicks = 42_000_000L,
        isPlaying = true,
        playlistItemId = "e7b83cbe1f782b329a2490a40252e46a",
    )
}
