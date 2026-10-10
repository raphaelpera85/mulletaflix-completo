package org.mulletaflix.feature.itemdetail

import android.content.Context
import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.ViewModelStore
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.withTimeout
import okhttp3.OkHttpClient
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.core.api.HomeFeedCacheScope
import org.mulletaflix.core.api.MulletaFlixApiService
import org.mulletaflix.core.api.SavedServerSession
import org.mulletaflix.core.api.SessionRepository
import org.readium.r2.shared.publication.Locator
import retrofit2.Retrofit

/** Host-orchestrated phases prove reader progress survives a real target-process force-stop. */
@RunWith(AndroidJUnit4::class)
class BookReaderProgressProcessRestartTest {
    private val context: Context
        get() = InstrumentationRegistry.getInstrumentation().targetContext

    @Test
    fun saveProgressBeforeHostProcessRestart() = runBlocking {
        assertTestTargetPackage()
        val testId = requireTestId()
        val itemId = testItemId(testId)
        val scope = testScope()
        val store = BookReaderProgressStore(context)
        val viewModelStore = ViewModelStore()
        val fixture = startTextFixture()
        val viewModel = createViewModel(viewModelStore, fixture, scope)

        try {
            viewModel.load(itemId)
            val loadedState = withTimeout(20_000) { viewModel.state.first { !it.isLoading } }
            assertNull("A local text-book fixture should open without error.", loadedState.error)
            val document = requireNotNull(loadedState.textBook)
            assertTrue("The fixture must provide enough text chunks.", document.chunkCount > SAVED_CHUNK_INDEX)

            val expectedLocator = document.locatorForChunk(SAVED_CHUNK_INDEX)
            viewModel.saveReadingProgression(itemId, expectedLocator)
            withTimeout(10_000) {
                while (store.read(scope, itemId)?.toJSON()?.toString() != expectedLocator.toJSON().toString()) {
                    delay(50)
                }
            }
            assertEquals(expectedLocator.toJSON().toString(), store.read(scope, itemId)?.toJSON()?.toString())
        } finally {
            viewModelStore.clear()
            fixture.shutdown()
        }
    }

    @Test
    fun freshViewModelRestoresProgressAfterHostProcessRestart() = runBlocking {
        assertTestTargetPackage()
        val testId = requireTestId()
        val itemId = testItemId(testId)
        val scope = testScope()
        val store = BookReaderProgressStore(context)
        val savedLocator = store.read(scope, itemId)
        assertNotNull("The progress entry must survive the preceding Android process death.", savedLocator)
        assertEquals("mulletaflix-text-chunk-$SAVED_CHUNK_INDEX", savedLocator?.href?.toString())
        assertNull(store.read(scope.copy(userId = "other-user"), itemId))
        assertNull(store.read(scope.copy(serverId = "other-server"), itemId))

        val viewModelStore = ViewModelStore()
        val fixture = startTextFixture()
        val viewModel = createViewModel(viewModelStore, fixture, scope)
        try {
            viewModel.load(itemId)
            val restoredState = withTimeout(20_000) { viewModel.state.first { !it.isLoading } }
            assertNull("The same local book fixture should reopen without error.", restoredState.error)
            assertNotNull(restoredState.textBook)
            assertEquals(savedLocator?.toJSON()?.toString(), restoredState.initialLocator?.toJSON()?.toString())
            assertEquals(SAVED_CHUNK_INDEX, restoredState.textBook?.chunkIndexFromLocator(restoredState.initialLocator))
        } finally {
            viewModelStore.clear()
            fixture.shutdown()
        }
    }

    @Test
    fun cleanupOnlyTheHostProcessRestartEntry() = runBlocking {
        assertTestTargetPackage()
        val testId = requireTestId()
        val itemId = testItemId(testId)
        BookReaderProgressStore(context).remove(testScope(), itemId)
        assertNull(BookReaderProgressStore(context).read(testScope(), itemId))
    }

    private fun createViewModel(
        viewModelStore: ViewModelStore,
        fixture: MockWebServer,
        scope: HomeFeedCacheScope,
    ): BookReaderViewModel {
        val api = Retrofit.Builder()
            .baseUrl(fixture.url("/"))
            .client(OkHttpClient())
            .build()
            .create(MulletaFlixApiService::class.java)
        return ViewModelProvider(
            viewModelStore,
            object : ViewModelProvider.Factory {
                @Suppress("UNCHECKED_CAST")
                override fun <T : ViewModel> create(modelClass: Class<T>): T =
                    BookReaderViewModel(api, RestartSessionRepository(scope), context) as T
            },
        )[BookReaderViewModel::class.java]
    }

    private fun startTextFixture(): MockWebServer = MockWebServer().apply {
        start()
        enqueue(
            MockResponse()
                .setResponseCode(200)
                .addHeader("Content-Type", "text/plain; charset=utf-8")
                .setBody(TEXT_FIXTURE),
        )
    }

    private fun assertTestTargetPackage() {
        assertEquals(
            "Process-restart tests must run in the isolated item-detail instrumentation target.",
            TEST_TARGET_PACKAGE,
            context.packageName,
        )
        val args = InstrumentationRegistry.getArguments()
        assertEquals("PHONE", args.getString("expectedDeviceProfile"))
        assertTrue(
            "The host harness must pass the expected emulator serial.",
            args.getString("deviceSerial")?.matches(Regex("emulator-\\d+")) == true,
        )
    }

    private fun requireTestId(): String = requireNotNull(
        InstrumentationRegistry.getArguments().getString("bookReaderProgressRestartTestId"),
    ).also { require(it.matches(Regex("[a-f0-9-]{36}"))) { "Expected a lowercase UUID test id." } }

    private fun testItemId(testId: String) = "book-reader-process-restart-$testId"

    private fun testScope() = HomeFeedCacheScope(
        serverId = "book-reader-process-restart-server",
        serverUrl = "https://book-reader-process-restart.invalid",
        userId = "book-reader-process-restart-user",
    )

    private class RestartSessionRepository(
        private val scope: HomeFeedCacheScope,
    ) : SessionRepository {
        override fun getAccessToken(): Flow<String?> = flowOf(null)
        override fun getDeviceId(): Flow<String> = flowOf("book-reader-process-restart-device")
        override fun getBaseUrl(): Flow<String> = flowOf(scope.serverUrl)
        override fun getCurrentUserId(): Flow<String?> = flowOf(scope.userId)
        override fun getHomeFeedCacheScope(): Flow<HomeFeedCacheScope?> = flowOf(scope)
        override suspend fun saveSession(serverUrl: String, token: String, userId: String, deviceId: String) = Unit
        override suspend fun setBaseUrl(url: String) = Unit
        override suspend fun clearSession() = Unit
        override fun getSavedServers(): Flow<List<SavedServerSession>> = flowOf(emptyList())
    }

    private companion object {
        const val TEST_TARGET_PACKAGE = "org.mulletaflix.feature.itemdetail.test"
        const val SAVED_CHUNK_INDEX = 3
        val TEXT_FIXTURE = "Process restart reader fixture. ".repeat(1_000)
    }
}
