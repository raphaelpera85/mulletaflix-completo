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
                playingItemId = "item-99",
                positionTicks = 1200000000L
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
}
