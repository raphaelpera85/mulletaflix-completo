package org.mulletaflix.feature.syncplay

import kotlinx.coroutines.Dispatchers
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

@OptIn(kotlinx.coroutines.ExperimentalCoroutinesApi::class)
class SyncPlayViewModelTest {
    private val dispatcher = StandardTestDispatcher()

    @Before fun setUp() = Dispatchers.setMain(dispatcher)
    @After fun tearDown() = Dispatchers.resetMain()

    @Test fun `refresh exposes current groups`() = runTest {
        val groups = listOf(SyncPlayGroup("g1", "Filme", "Playing", listOf("Raphael"), "item-1", 0L))
        val repository = FakeRepository(groups)
        val viewModel = SyncPlayViewModel(repository)

        advanceUntilIdle()

        assertEquals(groups, viewModel.state.value.groups)
        assertEquals(1, repository.listCalls)
    }

    @Test fun `joining a group marks it active`() = runTest {
        val group = SyncPlayGroup("g1", "Filme", "Paused", emptyList(), null, 0L)
        val viewModel = SyncPlayViewModel(FakeRepository(listOf(group)))
        advanceUntilIdle()

        viewModel.joinGroup("g1")
        advanceUntilIdle()

        assertEquals("g1", viewModel.state.value.activeGroupId)
    }

    private class FakeRepository(private val groups: List<SyncPlayGroup>) : SyncPlayRepository {
        var listCalls = 0
        override suspend fun getGroups(): Result<List<SyncPlayGroup>> {
            listCalls++
            return Result.success(groups)
        }
        override suspend fun createGroup(name: String) = Result.success(Unit)
        override suspend fun joinGroup(groupId: String) = Result.success(Unit)
        override suspend fun leaveGroup() = Result.success(Unit)
    }
}
