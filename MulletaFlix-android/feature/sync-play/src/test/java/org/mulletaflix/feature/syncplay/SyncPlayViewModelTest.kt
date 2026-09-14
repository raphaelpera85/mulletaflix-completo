package org.mulletaflix.feature.syncplay

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.StandardTestDispatcher
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test
import org.mulletaflix.domain.repository.SyncPlayGroup
import org.mulletaflix.domain.repository.SyncPlayRepository

@OptIn(ExperimentalCoroutinesApi::class)
class SyncPlayViewModelTest {
    private val dispatcher = StandardTestDispatcher()
    private lateinit var repository: FakeSyncPlayRepository

    @Before fun setUp() { Dispatchers.setMain(dispatcher); repository = FakeSyncPlayRepository() }
    @After fun tearDown() { Dispatchers.resetMain() }

    @Test fun `loads groups from server`() = runTest {
        val expected = SyncPlayGroup("room-1", "Sessão", "Playing", listOf("Raphael"), null, 0L)
        repository.groups = listOf(expected)
        val viewModel = SyncPlayViewModel(repository)
        advanceUntilIdle()
        assertEquals(listOf(expected), viewModel.state.value.groups)
        assertTrue(viewModel.state.value.error == null)
    }

    @Test fun `creates trimmed group and refreshes list`() = runTest {
        val viewModel = SyncPlayViewModel(repository)
        advanceUntilIdle()
        var callbackCalled = false
        viewModel.createGroup("  Noite de filmes  ") { callbackCalled = true }
        advanceUntilIdle()
        assertEquals("Noite de filmes", repository.createdName)
        assertTrue(callbackCalled)
    }

    @Test fun `exposes join failure to the user`() = runTest {
        repository.joinError = IllegalStateException("Sala indisponível")
        val viewModel = SyncPlayViewModel(repository)
        advanceUntilIdle()
        viewModel.joinGroup("room-1")
        advanceUntilIdle()
        assertEquals("Sala indisponível", viewModel.state.value.error)
        assertEquals(null, viewModel.state.value.activeGroupId)
    }

    private class FakeSyncPlayRepository : SyncPlayRepository {
        var groups: List<SyncPlayGroup> = emptyList()
        var createdName: String? = null
        var joinError: Throwable? = null
        override suspend fun getGroups() = Result.success(groups)
        override suspend fun createGroup(name: String): Result<Unit> { createdName = name; return Result.success(Unit) }
        override suspend fun joinGroup(groupId: String): Result<Unit> = joinError?.let { Result.failure(it) } ?: Result.success(Unit)
        override suspend fun leaveGroup(): Result<Unit> = Result.success(Unit)
    }
}
