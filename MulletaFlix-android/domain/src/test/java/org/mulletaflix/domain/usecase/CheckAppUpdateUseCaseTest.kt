package org.mulletaflix.domain.usecase

import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.domain.model.AppUpdateInfo
import org.mulletaflix.domain.repository.AppUpdateRepository
import java.io.IOException

class CheckAppUpdateUseCaseTest {

    @Test
    fun `returns success when update is available`() = runTest {
        val expectedInfo = AppUpdateInfo(
            isUpdateAvailable = true,
            currentVersion = "1.0.0",
            latestVersion = "1.1.0",
            apkDownloadUrl = "https://github.com/releases/download/app-v1.1.0/mulletaflix.apk",
            apkSize = 8_000_000L,
            apkSha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            releaseNotes = "Melhorias de desempenho",
        )
        val fakeRepo = object : AppUpdateRepository {
            override suspend fun checkForUpdate(currentVersion: String): Result<AppUpdateInfo> {
                assertEquals("1.0.0", currentVersion)
                return Result.success(expectedInfo)
            }
        }
        val useCase = CheckAppUpdateUseCase(fakeRepo)

        val result = useCase("1.0.0")

        assertTrue(result.isSuccess)
        val info = result.getOrThrow()
        assertTrue(info.isUpdateAvailable)
        assertEquals("1.1.0", info.latestVersion)
        assertEquals("https://github.com/releases/download/app-v1.1.0/mulletaflix.apk", info.apkDownloadUrl)
    }

    @Test
    fun `returns success when app is already up to date`() = runTest {
        val expectedInfo = AppUpdateInfo(
            isUpdateAvailable = false,
            currentVersion = "1.2.0",
            latestVersion = "1.2.0",
        )
        val fakeRepo = object : AppUpdateRepository {
            override suspend fun checkForUpdate(currentVersion: String): Result<AppUpdateInfo> {
                assertEquals("1.2.0", currentVersion)
                return Result.success(expectedInfo)
            }
        }
        val useCase = CheckAppUpdateUseCase(fakeRepo)

        val result = useCase("1.2.0")

        assertTrue(result.isSuccess)
        val info = result.getOrThrow()
        assertFalse(info.isUpdateAvailable)
        assertEquals("1.2.0", info.latestVersion)
    }

    @Test
    fun `returns failure when repository encounters an io exception`() = runTest {
        val networkError = IOException("Falha de conexão com o GitHub")
        val fakeRepo = object : AppUpdateRepository {
            override suspend fun checkForUpdate(currentVersion: String): Result<AppUpdateInfo> {
                return Result.failure(networkError)
            }
        }
        val useCase = CheckAppUpdateUseCase(fakeRepo)

        val result = useCase("1.0.0")

        assertTrue(result.isFailure)
        val exception = result.exceptionOrNull()
        assertTrue(exception is IOException)
        assertEquals("Falha de conexão com o GitHub", exception?.message)
    }

    @Test
    fun `normalizes version string by trimming whitespace and removing v prefix before calling repository`() = runTest {
        var passedVersion: String? = null
        val fakeRepo = object : AppUpdateRepository {
            override suspend fun checkForUpdate(currentVersion: String): Result<AppUpdateInfo> {
                passedVersion = currentVersion
                return Result.success(
                    AppUpdateInfo(
                        isUpdateAvailable = false,
                        currentVersion = currentVersion,
                        latestVersion = currentVersion,
                    ),
                )
            }
        }
        val useCase = CheckAppUpdateUseCase(fakeRepo)

        val resultLowerV = useCase("  v1.3.5  ")
        assertTrue(resultLowerV.isSuccess)
        assertEquals("1.3.5", passedVersion)

        val resultUpperV = useCase(" V2.0.1\t")
        assertTrue(resultUpperV.isSuccess)
        assertEquals("2.0.1", passedVersion)
    }

    @Test
    fun `fails fast with illegal argument exception when version is empty or blank without querying repository`() = runTest {
        var repoCalled = false
        val fakeRepo = object : AppUpdateRepository {
            override suspend fun checkForUpdate(currentVersion: String): Result<AppUpdateInfo> {
                repoCalled = true
                return Result.failure(IllegalStateException("Should not be called"))
            }
        }
        val useCase = CheckAppUpdateUseCase(fakeRepo)

        val emptyResult = useCase("")
        assertTrue(emptyResult.isFailure)
        assertTrue(emptyResult.exceptionOrNull() is IllegalArgumentException)
        assertFalse(repoCalled)

        val blankResult = useCase("   \t\n ")
        assertTrue(blankResult.isFailure)
        assertTrue(blankResult.exceptionOrNull() is IllegalArgumentException)
        assertFalse(repoCalled)
    }
}
