package org.mulletaflix.feature.syncplay

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.test.StandardTestDispatcher
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Before
import org.junit.Test
import org.mulletaflix.domain.repository.SyncPlayGroup
import org.mulletaflix.domain.repository.SyncPlayRepository

import org.mulletaflix.domain.usecase.ManageSyncPlayUseCase
import org.mulletaflix.core.api.SavedServerSession
import org.mulletaflix.core.api.SessionRepository

@OptIn(kotlinx.coroutines.ExperimentalCoroutinesApi::class)
class SyncPlayViewModelTest {
    private val dispatcher = StandardTestDispatcher()

    @Before fun setUp() = Dispatchers.setMain(dispatcher)
    @After fun tearDown() = Dispatchers.resetMain()

    @Test fun `refresh exposes current groups`() = runTest {
        val groups = listOf(SyncPlayGroup("g1", "Filme", "Playing", listOf("Raphael"), "item-1", 0L))
        val repository = FakeRepository(groups)
        val useCase = ManageSyncPlayUseCase(repository)
        val viewModel = SyncPlayViewModel(useCase, repository)

        advanceUntilIdle()

        assertEquals(groups, viewModel.state.value.groups)
        assertEquals(1, repository.listCalls)
    }

    @Test fun `joining a group marks it active`() = runTest {
        val group = SyncPlayGroup("g1", "Filme", "Paused", emptyList(), null, 0L)
        val repository = FakeRepository(listOf(group))
        val useCase = ManageSyncPlayUseCase(repository)
        val viewModel = SyncPlayViewModel(useCase, repository)
        advanceUntilIdle()

        viewModel.joinGroup("g1")
        advanceUntilIdle()

        assertEquals("g1", viewModel.state.value.activeGroupId)
    }

    @Test fun `late groups from a previous user cannot replace the current session`() = runTest {
        val staleGroups = CompletableDeferred<Result<List<SyncPlayGroup>>>()
        val userId = MutableStateFlow<String?>("user-1")
        val repository = FakeRepository(emptyList(), staleGroups, userId)
        val viewModel = SyncPlayViewModel(ManageSyncPlayUseCase(repository), repository)
        advanceUntilIdle()

        userId.value = "user-2"
        advanceUntilIdle()
        staleGroups.complete(Result.success(listOf(SyncPlayGroup("old", "Conta antiga", "Paused", emptyList(), null, 0L))))
        advanceUntilIdle()

        assertEquals(emptyList<SyncPlayGroup>(), viewModel.state.value.groups)
        assertEquals(2, repository.listCalls)
    }

    private class FakeRepository(
        private val groups: List<SyncPlayGroup>,
        private val firstResponse: CompletableDeferred<Result<List<SyncPlayGroup>>>? = null,
        private val userId: MutableStateFlow<String?> = MutableStateFlow("user-1"),
    ) : SyncPlayRepository, SessionRepository {
        var listCalls = 0
        override suspend fun getGroups(): Result<List<SyncPlayGroup>> {
            listCalls++
            if (listCalls == 1 && firstResponse != null) return firstResponse.await()
            return Result.success(groups)
        }
        override suspend fun createGroup(name: String) = Result.success(Unit)
        override suspend fun joinGroup(groupId: String) = Result.success(Unit)
        override suspend fun leaveGroup() = Result.success(Unit)
        override fun getAccessToken(): Flow<String?> = flowOf(null)
        override fun getDeviceId(): Flow<String> = flowOf("device")
        override fun getBaseUrl(): Flow<String> = flowOf("http://server")
        override fun getCurrentUserId(): Flow<String?> = userId
        override suspend fun saveSession(serverUrl: String, token: String, userId: String, deviceId: String) = Unit
        override suspend fun setBaseUrl(url: String) = Unit
        override suspend fun clearSession() = Unit
    }
}
