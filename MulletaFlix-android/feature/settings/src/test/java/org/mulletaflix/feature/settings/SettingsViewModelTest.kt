package org.mulletaflix.feature.settings

import android.content.Context
import io.mockk.mockk
import io.mockk.every
import io.mockk.coVerify
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.awaitCancellation
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.flow
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.test.StandardTestDispatcher
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import java.io.File
import java.nio.file.Files
import org.mulletaflix.core.common.update.AppUpdateDownloader
import org.mulletaflix.core.common.update.DownloadState
import org.mulletaflix.designsystem.theme.MulletaFlixThemeVariant
import org.mulletaflix.domain.model.AppUpdateInfo
import org.mulletaflix.domain.model.LibrarySortField
import org.mulletaflix.domain.model.UserProfile
import org.mulletaflix.domain.repository.*
import org.mulletaflix.domain.usecase.CheckAppUpdateUseCase
import org.mulletaflix.domain.usecase.LogoutUseCase
import org.mulletaflix.domain.usecase.VerifyServerUseCase

@OptIn(ExperimentalCoroutinesApi::class)
class SettingsViewModelTest {

    private val dispatcher = StandardTestDispatcher()
    private val context = mockk<Context>(relaxed = true)

    @Before
    fun setUp() {
        Dispatchers.setMain(dispatcher)
    }

    @After
    fun tearDown() {
        Dispatchers.resetMain()
    }

    @Test
    fun `initial state loads settings and user from repositories`() = runTest {
        val settingsRepo = FakeSettingsRepository()
        val authRepo = FakeAuthRepository(url = "http://localhost:8096", name = "Mulleta User")
        val logoutUseCase = LogoutUseCase(authRepo)

        val viewModel = SettingsViewModel(context, settingsRepo, authRepo, logoutUseCase)
        advanceUntilIdle()

        val state = viewModel.state.value
        assertEquals("http://localhost:8096", state.serverUrl)
        assertEquals("Mulleta User", state.username)
        assertEquals(MulletaFlixThemeVariant.Dark, state.theme)
        assertTrue(state.autoPlay)
        assertTrue(state.skipIntro)
        assertFalse(state.automaticIntroSkip)
        assertTrue(state.pictureInPicture)
        assertEquals("FIT", state.aspectRatio)
    }

    @Test
    fun `setTheme updates state and repository`() = runTest {
        val settingsRepo = FakeSettingsRepository()
        val authRepo = FakeAuthRepository()
        val logoutUseCase = LogoutUseCase(authRepo)

        val viewModel = SettingsViewModel(context, settingsRepo, authRepo, logoutUseCase)
        advanceUntilIdle()

        viewModel.setTheme(MulletaFlixThemeVariant.Netflix)
        advanceUntilIdle()

        assertEquals(MulletaFlixThemeVariant.Netflix, viewModel.state.value.theme)
        assertEquals(AppThemeSetting.Netflix, settingsRepo.currentTheme)
    }

    @Test
    fun `toggle playback options updates state and repository`() = runTest {
        val settingsRepo = FakeSettingsRepository()
        val authRepo = FakeAuthRepository()
        val logoutUseCase = LogoutUseCase(authRepo)

        val viewModel = SettingsViewModel(context, settingsRepo, authRepo, logoutUseCase)
        advanceUntilIdle()

        viewModel.setAutoPlay(false)
        viewModel.setSkipIntro(false)
        viewModel.setAutomaticIntroSkip(true)
        viewModel.setPictureInPicture(false)
        viewModel.setDefaultPlaybackSpeed(1.5f)
        advanceUntilIdle()

        assertFalse(viewModel.state.value.autoPlay)
        assertFalse(viewModel.state.value.skipIntro)
        assertTrue(viewModel.state.value.automaticIntroSkip)
        assertTrue(settingsRepo.automaticIntroSkip)
        assertFalse(viewModel.state.value.pictureInPicture)
        assertEquals(1.5f, viewModel.state.value.defaultSpeed, 0.01f)
    }

    @Test
    fun `aspect ratio preference is restored and persisted`() = runTest {
        val settingsRepo = FakeSettingsRepository().apply { aspectRatio = "ZOOM" }
        val authRepo = FakeAuthRepository()
        val viewModel = SettingsViewModel(context, settingsRepo, authRepo, LogoutUseCase(authRepo))
        advanceUntilIdle()

        assertEquals("ZOOM", viewModel.state.value.aspectRatio)
        viewModel.setDefaultAspectRatio("FILL")
        advanceUntilIdle()

        assertEquals("FILL", viewModel.state.value.aspectRatio)
        assertEquals("FILL", settingsRepo.aspectRatio)
    }

    @Test
    fun `library grid density is restored and persisted`() = runTest {
        val settingsRepo = FakeSettingsRepository().apply { gridDensity = "COMPACT" }
        val authRepo = FakeAuthRepository()
        val viewModel = SettingsViewModel(context, settingsRepo, authRepo, LogoutUseCase(authRepo))
        advanceUntilIdle()

        assertEquals("Compacta", viewModel.state.value.libraryGridDensity)
        viewModel.setLibraryGridDensity("Confortável")
        advanceUntilIdle()

        assertEquals("Confortável", viewModel.state.value.libraryGridDensity)
        assertEquals("COMFORTABLE", settingsRepo.gridDensity)
    }

    @Test
    fun `library sort preference is restored and persisted`() = runTest {
        val settingsRepo = FakeSettingsRepository().apply { librarySort = "PremiereDate" }
        val authRepo = FakeAuthRepository()
        val viewModel = SettingsViewModel(context, settingsRepo, authRepo, LogoutUseCase(authRepo))
        advanceUntilIdle()

        assertEquals("Data de Lançamento", viewModel.state.value.librarySort)
        viewModel.setLibrarySort("Avaliação")
        advanceUntilIdle()

        assertEquals("Avaliação", viewModel.state.value.librarySort)
        assertEquals("CommunityRating", settingsRepo.librarySort)
        settingsRepo.librarySortOrder = "Descending"
        val directionViewModel = SettingsViewModel(context, settingsRepo, authRepo, LogoutUseCase(authRepo))
        advanceUntilIdle()
        assertEquals("Descendente", directionViewModel.state.value.librarySortOrder)
        directionViewModel.setLibrarySortOrder("Ascendente")
        advanceUntilIdle()
        assertEquals("Ascending", settingsRepo.librarySortOrder)
    }

    @Test
    fun `settings shows the stored sort for every field the library menu offers`() = runTest {
        // The library menu and this screen used to keep separate lists of labels,
        // so fields 5-8 were displayed as "Nome" even though the library was
        // ordered by them.
        LibrarySortField.entries.forEach { field ->
            val settingsRepo = FakeSettingsRepository().apply { librarySort = field.code }
            val authRepo = FakeAuthRepository()
            val viewModel = SettingsViewModel(context, settingsRepo, authRepo, LogoutUseCase(authRepo))
            advanceUntilIdle()

            assertEquals(
                "sort ${field.code} was displayed as the wrong field",
                field.label,
                viewModel.state.value.librarySort,
            )
        }
    }

    @Test
    fun `confirming the displayed sort keeps the stored field`() = runTest {
        // The defect that mattered: the settings dialog hands back the label it
        // displayed, and the ViewModel re-derived the code from that label. An
        // unrecognised label fell back to "Nome", so merely confirming the
        // dialog destroyed the user's actual choice.
        LibrarySortField.entries.forEach { field ->
            val settingsRepo = FakeSettingsRepository().apply { librarySort = field.code }
            val authRepo = FakeAuthRepository()
            val viewModel = SettingsViewModel(context, settingsRepo, authRepo, LogoutUseCase(authRepo))
            advanceUntilIdle()

            viewModel.setLibrarySort(viewModel.state.value.librarySort)
            advanceUntilIdle()

            assertEquals(
                "confirming the value shown for ${field.code} overwrote it",
                field.code,
                settingsRepo.librarySort,
            )
        }
    }

    @Test
    fun `audio language preference is restored and can be changed`() = runTest {
        val settingsRepo = FakeSettingsRepository()
        val authRepo = FakeAuthRepository()
        val viewModel = SettingsViewModel(context, settingsRepo, authRepo, LogoutUseCase(authRepo))
        advanceUntilIdle()

        assertEquals("English", viewModel.state.value.audioLanguage)
        viewModel.setAudioLanguage("Português (Brasil)")
        advanceUntilIdle()

        assertEquals("Português (Brasil)", viewModel.state.value.audioLanguage)
        // Canonical ISO 639-1 from the shared catalogue, matching what the
        // player writes and what the stream matcher compares against.
        assertEquals("pt", settingsRepo.audioLanguage)
    }

    @Test
    fun `every language the player can store is displayed and preserved`() = runTest {
        // The player saves whatever code the server reported. Previously only
        // por/pt/eng/en were recognised, so Spanish, French and German were all
        // shown as "Idioma original" — and confirming that value wrote
        // `original`, discarding the real choice.
        listOf(
            "por" to "Português (Brasil)",
            "pt-br" to "Português (Brasil)",
            "eng" to "English",
            "en-US" to "English",
            "spa" to "Español",
            "es" to "Español",
            "fra" to "Français",
            "deu" to "Deutsch",
            "original" to "Idioma original",
        ).forEach { (stored, expectedLabel) ->
            val settingsRepo = FakeSettingsRepository().apply { audioLanguage = stored }
            val authRepo = FakeAuthRepository()
            val viewModel = SettingsViewModel(context, settingsRepo, authRepo, LogoutUseCase(authRepo))
            advanceUntilIdle()

            assertEquals("stored <$stored> was displayed wrongly", expectedLabel, viewModel.state.value.audioLanguage)

            // Confirming the displayed value must keep the same language.
            viewModel.setAudioLanguage(viewModel.state.value.audioLanguage)
            advanceUntilIdle()
            assertEquals(
                "confirming the value shown for <$stored> changed the stored language",
                expectedLabel,
                viewModel.state.value.audioLanguage,
            )
        }
    }

    @Test
    fun `subtitle language keeps its own off state`() = runTest {
        val settingsRepo = FakeSettingsRepository()
        val authRepo = FakeAuthRepository()
        val viewModel = SettingsViewModel(context, settingsRepo, authRepo, LogoutUseCase(authRepo))
        advanceUntilIdle()

        viewModel.setSubtitleLanguage("Desativadas")
        advanceUntilIdle()

        assertEquals("Desativadas", viewModel.state.value.subtitleLanguage)
        assertEquals("off", settingsRepo.subtitleLanguage)
    }

    @Test
    fun `logout calls logoutUseCase`() = runTest {
        var loggedOut = false
        val authRepo = object : FakeAuthRepository() {
            override suspend fun logout(): Result<Unit> {
                loggedOut = true
                return Result.success(Unit)
            }
        }
        val settingsRepo = FakeSettingsRepository()
        val logoutUseCase = LogoutUseCase(authRepo)

        val viewModel = SettingsViewModel(context, settingsRepo, authRepo, logoutUseCase)
        advanceUntilIdle()

        viewModel.logout()
        advanceUntilIdle()

        assertTrue(loggedOut)
    }

    @Test
    fun `clearing image cache exposes a completion message`() = runTest {
        val cacheRoot = Files.createTempDirectory("mulletaflix-settings-test").toFile()
        every { context.cacheDir } returns cacheRoot
        val authRepo = FakeAuthRepository()
        val viewModel = SettingsViewModel(
            context,
            FakeSettingsRepository(),
            authRepo,
            LogoutUseCase(authRepo),
            ioDispatcher = dispatcher,
        )
        advanceUntilIdle()

        viewModel.clearImageCache()
        advanceUntilIdle()

        assertEquals("Cache de imagens limpo.", viewModel.state.value.cacheStatusMessage)
        cacheRoot.deleteRecursively()
    }

    @Test
    fun `clearing all local data reports completion and preserves downloads`() = runTest {
        val cacheRoot = Files.createTempDirectory("mulletaflix-settings-all-test").toFile()
        val downloads = cacheRoot.resolve("downloads").apply { mkdirs() }
        cacheRoot.resolve("image_cache").apply { mkdirs() }
        cacheRoot.resolve("temporary").apply { mkdirs() }
        every { context.cacheDir } returns cacheRoot
        var loggedOut = false
        val authRepo = object : FakeAuthRepository() {
            override suspend fun logout(): Result<Unit> {
                loggedOut = true
                return Result.success(Unit)
            }
        }
        val settingsRepo = FakeSettingsRepository()
        val viewModel = SettingsViewModel(
            context,
            settingsRepo,
            authRepo,
            LogoutUseCase(authRepo),
            ioDispatcher = dispatcher,
        )
        advanceUntilIdle()

        viewModel.clearAllCache()
        advanceUntilIdle()

        assertEquals("Dados locais limpos. Você saiu da conta.", viewModel.state.value.cacheStatusMessage)
        assertTrue(loggedOut)
        assertTrue(settingsRepo.localPreferencesCleared)
        assertTrue(downloads.exists())
        assertFalse(cacheRoot.resolve("image_cache").exists())
        assertFalse(cacheRoot.resolve("temporary").exists())
        cacheRoot.deleteRecursively()
    }

    @Test
    fun `server connection check exposes latency and version`() = runTest {
        val settingsRepo = FakeSettingsRepository()
        val authRepo = FakeAuthRepository(
            url = "http://mulletaflix.duckdns.org:8096",
            verification = Result.success(ServerVerification("MulletaFlix", "12.0.11", 42L)),
        )
        val viewModel = SettingsViewModel(
            context,
            settingsRepo,
            authRepo,
            LogoutUseCase(authRepo),
            verifyServerUseCase = VerifyServerUseCase(authRepo),
        )
        advanceUntilIdle()

        viewModel.checkServerConnection()
        advanceUntilIdle()

        assertFalse(viewModel.state.value.isCheckingConnection)
        assertEquals("Conectado • 42 ms • v12.0.11", viewModel.state.value.connectionStatus)
    }

    @Test
    fun `server connection check exposes a friendly failure`() = runTest {
        val settingsRepo = FakeSettingsRepository()
        val authRepo = FakeAuthRepository(
            verification = Result.failure(IllegalStateException("Servidor indisponível")),
        )
        val viewModel = SettingsViewModel(
            context,
            settingsRepo,
            authRepo,
            LogoutUseCase(authRepo),
            verifyServerUseCase = VerifyServerUseCase(authRepo),
        )
        advanceUntilIdle()

        viewModel.checkServerConnection()
        advanceUntilIdle()

        assertFalse(viewModel.state.value.isCheckingConnection)
        assertEquals("Servidor indisponível", viewModel.state.value.connectionStatus)
    }

    @Test
    fun `duplicate update checks are ignored while the first request is active`() = runTest {
        var requestCount = 0
        val updateRepository = object : AppUpdateRepository {
            override suspend fun checkForUpdate(currentVersion: String): Result<AppUpdateInfo> {
                requestCount++
                return Result.success(
                    AppUpdateInfo(
                        isUpdateAvailable = false,
                        currentVersion = currentVersion,
                        latestVersion = currentVersion,
                    ),
                )
            }
        }
        val authRepo = FakeAuthRepository()
        val viewModel = SettingsViewModel(
            context,
            FakeSettingsRepository(),
            authRepo,
            LogoutUseCase(authRepo),
            checkAppUpdateUseCase = CheckAppUpdateUseCase(updateRepository),
            ioDispatcher = dispatcher,
        )
        advanceUntilIdle()

        viewModel.checkForUpdates("1.1.28")
        viewModel.checkForUpdates("1.1.28")
        advanceUntilIdle()

        assertEquals(1, requestCount)
        assertFalse(viewModel.state.value.isCheckingUpdate)
    }

    @Test
    fun `update check trims installed version before requesting`() = runTest {
        var requestedVersion: String? = null
        val updateRepository = object : AppUpdateRepository {
            override suspend fun checkForUpdate(currentVersion: String): Result<AppUpdateInfo> {
                requestedVersion = currentVersion
                return Result.success(
                    AppUpdateInfo(
                        isUpdateAvailable = false,
                        currentVersion = currentVersion,
                        latestVersion = currentVersion,
                    )
                )
            }
        }
        val viewModel = newViewModel(
            checkAppUpdateUseCase = CheckAppUpdateUseCase(updateRepository),
        )

        viewModel.checkForUpdates(" 1.1.29 ")
        advanceUntilIdle()

        assertEquals("1.1.29", requestedVersion)
        assertFalse(viewModel.state.value.isCheckingUpdate)
    }

    @Test
    fun `blank installed version is rejected without a request`() = runTest {
        var requestCount = 0
        val updateRepository = object : AppUpdateRepository {
            override suspend fun checkForUpdate(currentVersion: String): Result<AppUpdateInfo> {
                requestCount++
                return Result.failure(AssertionError("request must not be made"))
            }
        }
        val viewModel = newViewModel(
            checkAppUpdateUseCase = CheckAppUpdateUseCase(updateRepository),
        )

        viewModel.checkForUpdates("   ")
        advanceUntilIdle()

        assertEquals(0, requestCount)
        assertEquals("Não foi possível identificar a versão instalada.", viewModel.state.value.updateErrorMessage)
    }

    @Test
    fun `clearing all local data also clears the search history`() = runTest {
        // "Limpar Todos os Dados Locais" limpava preferências e caches, mas o histórico
        // de busca vive em outro armazenamento e sobrevivia: os termos buscados
        // reapareciam no próximo login do mesmo usuário.
        val cacheRoot = Files.createTempDirectory("mulletaflix-settings-search-test").toFile()
        val downloads = cacheRoot.resolve("downloads").apply { mkdirs() }
        every { context.cacheDir } returns cacheRoot
        val history = RecordingSearchHistoryRepository()
        val authRepo = FakeAuthRepository()
        val viewModel = SettingsViewModel(
            context,
            FakeSettingsRepository(),
            authRepo,
            LogoutUseCase(authRepo),
            searchHistoryRepository = history,
            ioDispatcher = dispatcher,
        )
        advanceUntilIdle()

        viewModel.clearAllCache()
        advanceUntilIdle()

        assertEquals("Dados locais limpos. Você saiu da conta.", viewModel.state.value.cacheStatusMessage)
        assertEquals(
            "o histórico do usuário que estava na sessão precisa ser apagado",
            listOf("u1"),
            history.clearedFor,
        )
        assertTrue("os downloads não fazem parte da limpeza", downloads.exists())
        cacheRoot.deleteRecursively()
    }

    @Test
    fun `a second update download in the same frame is ignored`() = runTest {
        // O botão fica desabilitado durante o download, mas isso é lido na composição:
        // dois toques no mesmo frame passavam os dois, e o segundo download apaga o
        // arquivo que o primeiro já abriu.
        val updateRepository = object : AppUpdateRepository {
            override suspend fun checkForUpdate(currentVersion: String): Result<AppUpdateInfo> =
                Result.success(
                    AppUpdateInfo(
                        isUpdateAvailable = true,
                        currentVersion = currentVersion,
                        latestVersion = "9.9.9",
                        apkDownloadUrl = "https://example.invalid/app.apk",
                    ),
                )
        }
        val downloader = mockk<AppUpdateDownloader>()
        every { downloader.downloadApk(any(), any(), any()) } returns flow {
            // Um download que nunca termina mantém a operação em andamento.
            emit(DownloadState.Downloading(progress = 0.1f, bytesDownloaded = 1, totalBytes = 10))
            awaitCancellation()
        }
        val authRepo = FakeAuthRepository()
        val viewModel = SettingsViewModel(
            context,
            FakeSettingsRepository(),
            authRepo,
            LogoutUseCase(authRepo),
            checkAppUpdateUseCase = CheckAppUpdateUseCase(updateRepository),
            appUpdateDownloader = downloader,
            ioDispatcher = dispatcher,
        )
        advanceUntilIdle()
        viewModel.checkForUpdates("1.0.0")
        advanceUntilIdle()
        viewModel.downloadAndInstallUpdate { true }
        viewModel.downloadAndInstallUpdate { true }
        advanceUntilIdle()

        coVerify(exactly = 1) { downloader.downloadApk(any(), any(), any()) }
        assertTrue(viewModel.state.value.isDownloadingUpdate)
    }

    @Test
    fun `a completed download closes the dialog and reports the install start`() = runTest {
        val viewModel = viewModelWithCompletedDownload()
        viewModel.checkForUpdates("1.0.0")
        advanceUntilIdle()

        viewModel.downloadAndInstallUpdate { true }
        advanceUntilIdle()

        val state = viewModel.state.value
        assertFalse(state.isDownloadingUpdate)
        assertFalse(state.showUpdateDialog)
        assertEquals("Download concluído. Iniciando instalação...", state.updateStatusMessage)
        assertEquals(null, state.updateErrorMessage)
    }

    @Test
    fun `a refused install keeps the dialog and explains the permission step`() = runTest {
        // `false` não é falha: é a resposta do sistema quando falta a permissão de
        // fontes desconhecidas — a tela precisa dizer isso, e não fingir sucesso.
        val viewModel = viewModelWithCompletedDownload()
        viewModel.checkForUpdates("1.0.0")
        advanceUntilIdle()

        viewModel.downloadAndInstallUpdate { false }
        advanceUntilIdle()

        val state = viewModel.state.value
        assertFalse(state.isDownloadingUpdate)
        assertTrue(state.showUpdateDialog)
        assertEquals(
            "Permita a instalação de fontes desconhecidas e tente novamente.",
            state.updateErrorMessage,
        )
    }

    @Test
    fun `an installer exception surfaces its detail as the error`() = runTest {
        val viewModel = viewModelWithCompletedDownload()
        viewModel.checkForUpdates("1.0.0")
        advanceUntilIdle()

        viewModel.downloadAndInstallUpdate { throw IllegalStateException("sem fileprovider") }
        advanceUntilIdle()

        val state = viewModel.state.value
        assertFalse(state.isDownloadingUpdate)
        assertTrue(state.showUpdateDialog)
        assertEquals("sem fileprovider", state.updateErrorMessage)
    }

    /** ViewModel com uma atualização disponível e um download que já terminou. */
    @Suppress("SameParameterValue")
    private fun viewModelWithCompletedDownload(): SettingsViewModel {
        val updateRepository = object : AppUpdateRepository {
            override suspend fun checkForUpdate(currentVersion: String): Result<AppUpdateInfo> =
                Result.success(
                    AppUpdateInfo(
                        isUpdateAvailable = true,
                        currentVersion = currentVersion,
                        latestVersion = "9.9.9",
                        apkDownloadUrl = "https://example.invalid/app.apk",
                    ),
                )
        }
        val downloader = mockk<AppUpdateDownloader>().apply {
            every { downloadApk(any(), any(), any()) } returns
                flowOf(DownloadState.Completed(File("mulletaflix-app-v9.9.9.apk")))
        }
        val authRepo = FakeAuthRepository()
        return SettingsViewModel(
            context,
            FakeSettingsRepository(),
            authRepo,
            LogoutUseCase(authRepo),
            checkAppUpdateUseCase = CheckAppUpdateUseCase(updateRepository),
            appUpdateDownloader = downloader,
            ioDispatcher = dispatcher,
        )
    }

    @Test
    fun `repository exception resets checking state with a friendly error`() = runTest {
        val updateRepository = object : AppUpdateRepository {
            override suspend fun checkForUpdate(currentVersion: String): Result<AppUpdateInfo> {
                error("network exploded")
            }
        }
        val viewModel = newViewModel(
            checkAppUpdateUseCase = CheckAppUpdateUseCase(updateRepository),
        )

        viewModel.checkForUpdates("1.1.29")
        advanceUntilIdle()

        assertFalse(viewModel.state.value.isCheckingUpdate)
        assertEquals("network exploded", viewModel.state.value.updateErrorMessage)
    }

    private fun newViewModel(
        checkAppUpdateUseCase: CheckAppUpdateUseCase? = null,
    ): SettingsViewModel {
        val authRepo = FakeAuthRepository()
        return SettingsViewModel(
            context = context,
            settingsRepository = FakeSettingsRepository(),
            authRepository = authRepo,
            logoutUseCase = LogoutUseCase(authRepo),
            checkAppUpdateUseCase = checkAppUpdateUseCase,
            ioDispatcher = dispatcher,
        )
    }

    /** Registra qual usuário teve o histórico de busca apagado. */
    private class RecordingSearchHistoryRepository : SearchHistoryRepository {
        val clearedFor = mutableListOf<String?>()

        override fun observeHistory(userId: String?): Flow<List<String>> = flowOf(emptyList())

        override suspend fun add(userId: String?, query: String) = Unit

        override suspend fun remove(userId: String?, query: String) = Unit

        override suspend fun clear(userId: String?) {
            clearedFor += userId
        }
    }

    private class FakeSettingsRepository : SettingsRepository {
        var localPreferencesCleared: Boolean = false
        var currentTheme: AppThemeSetting = AppThemeSetting.Dark
        var autoPlay: Boolean = true
        var skipIntro: Boolean = true
        var automaticIntroSkip: Boolean = false
        var pip: Boolean = true
        var quality: String = "Auto"
        var speed: Float = 1.0f
        var aspectRatio: String = "FIT"
        var subtitleSize: Int = 100
        var audioLanguage: String? = "eng"
        var gridDensity: String = "COMFORTABLE"
        var librarySort: String = "SortName"

        override fun getTheme(): Flow<AppThemeSetting> = MutableStateFlow(currentTheme)
        override suspend fun setTheme(theme: AppThemeSetting) { currentTheme = theme }


        override fun isPiPEnabled(): Flow<Boolean> = MutableStateFlow(pip)
        override suspend fun setPiPEnabled(enabled: Boolean) { pip = enabled }

        override fun getPreferredAudioLanguage(): Flow<String?> = flowOf(audioLanguage)
        override suspend fun setPreferredAudioLanguage(language: String?) { audioLanguage = language }

        var subtitleLanguage: String? = "pt-br"
        override fun getPreferredSubtitleLanguage(): Flow<String?> = flowOf(subtitleLanguage)
        override suspend fun setPreferredSubtitleLanguage(language: String?) { subtitleLanguage = language }

        override fun isAutoPlayEnabled(): Flow<Boolean> = MutableStateFlow(autoPlay)
        override suspend fun setAutoPlayEnabled(enabled: Boolean) { autoPlay = enabled }

        override fun isSkipIntroEnabled(): Flow<Boolean> = MutableStateFlow(skipIntro)
        override suspend fun setSkipIntroEnabled(enabled: Boolean) { skipIntro = enabled }
        override fun isAutomaticIntroSkipEnabled(): Flow<Boolean> = MutableStateFlow(automaticIntroSkip)
        override suspend fun setAutomaticIntroSkipEnabled(enabled: Boolean) { automaticIntroSkip = enabled }

        override fun getDefaultQuality(): Flow<String> = MutableStateFlow(quality)
        override suspend fun setDefaultQuality(quality: String) { this.quality = quality }

        override fun getDefaultPlaybackSpeed(): Flow<Float> = MutableStateFlow(speed)
        override suspend fun setDefaultPlaybackSpeed(speed: Float) { this.speed = speed }

        override suspend fun clearLocalPreferences() {
            localPreferencesCleared = true
        }

        override fun getSubtitleFontSize(): Flow<Int> = MutableStateFlow(subtitleSize)
        override suspend fun setSubtitleFontSize(size: Int) { subtitleSize = size }
        override fun getDefaultAspectRatio(): Flow<String> = MutableStateFlow(aspectRatio)
        override suspend fun setDefaultAspectRatio(aspectRatio: String) { this.aspectRatio = aspectRatio }
        override fun isLibraryGridViewEnabled(): Flow<Boolean> = MutableStateFlow(true)
        override suspend fun setLibraryGridViewEnabled(enabled: Boolean) = Unit
        override fun getLibraryGridDensity(): Flow<String> = MutableStateFlow(gridDensity)
        override suspend fun setLibraryGridDensity(density: String) { gridDensity = density }
        override fun getDefaultLibrarySort(): Flow<String> = MutableStateFlow(librarySort)
        override suspend fun setDefaultLibrarySort(sortBy: String) { librarySort = sortBy }
        var librarySortOrder: String = "Ascending"
        override fun getDefaultLibrarySortOrder(): Flow<String> = MutableStateFlow(librarySortOrder)
        override suspend fun setDefaultLibrarySortOrder(sortOrder: String) { librarySortOrder = sortOrder }
        override fun getDefaultLibraryFilters(): Flow<Set<String>> = MutableStateFlow(emptySet())
        override suspend fun setDefaultLibraryFilters(filters: Set<String>) = Unit
    }

    private open class FakeAuthRepository(
        private val url: String = "http://localhost:8096",
        private val name: String = "User",
        private val verification: Result<ServerVerification> = Result.failure(NotImplementedError()),
    ) : AuthRepository {
        override suspend fun verifyServer(url: String): Result<ServerVerification> = verification
        override suspend fun register(username: String, password: String): Result<RegistrationResult> = Result.failure(NotImplementedError())
        override suspend fun login(username: String, password: String): Result<UserSession> = Result.failure(NotImplementedError())
        override suspend fun getAvailableUsers(): Result<List<AvailableUser>> = Result.success(emptyList())
        override suspend fun initiateQuickConnect(): Result<QuickConnectState> = Result.failure(NotImplementedError())
        override suspend fun checkQuickConnect(secret: String): Result<UserSession?> = Result.success(null)
        override suspend fun logout(): Result<Unit> = Result.success(Unit)
        override suspend fun getCurrentUserProfile(): Result<UserProfile> = Result.failure(NotImplementedError())
        override fun getSavedServerUrl(): Flow<String> = flowOf(url)
        override suspend fun setServerUrl(url: String) = Unit
        override fun getSavedUserId(): Flow<String?> = flowOf("u1")
        override fun getSavedUserName(): Flow<String?> = flowOf(name)
        override fun getSavedToken(): Flow<String?> = flowOf("token")
    }
}
