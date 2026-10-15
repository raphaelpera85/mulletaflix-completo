package org.mulletaflix.android

import android.content.Intent
import android.net.Uri
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.v2.createEmptyComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.onAllNodesWithText
import androidx.compose.ui.test.onRoot
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performScrollTo
import androidx.compose.ui.test.performTextClearance
import androidx.compose.ui.test.performTextInput
import androidx.test.core.app.ActivityScenario
import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import androidx.compose.ui.semantics.SemanticsProperties
import androidx.compose.ui.semantics.SemanticsNode
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.withTimeout
import okhttp3.mockwebserver.Dispatcher
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import okhttp3.mockwebserver.RecordedRequest
import org.junit.After
import org.junit.Assert.assertTrue
import org.junit.Assert.assertFalse
import org.junit.Assert.assertEquals
import org.junit.Assume.assumeTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.data.repository.SessionRepositoryImpl
import java.util.concurrent.ConcurrentLinkedQueue

@Retention(AnnotationRetention.RUNTIME)
@Target(AnnotationTarget.FUNCTION)
annotation class AndroidTvOnly

@RunWith(AndroidJUnit4::class)
class MainActivitySessionNavigationTest {
    @get:Rule
    val composeRule = createEmptyComposeRule()

    private val context = ApplicationProvider.getApplicationContext<android.content.Context>()
    private val sessionRepository: SessionRepository = SessionRepositoryImpl(context)
    private var originalSession: SessionSnapshot? = null
    private var sessionSnapshotIsRestorable = false
    private var activityScenario: ActivityScenario<MainActivity>? = null
    private val mockServers = mutableListOf<MockWebServer>()
    private val mockServerUrlsCreatedByThisTest = mutableSetOf<String>()
    private val temporarySavedServerUrls = mutableSetOf<String>()
    private var localNetworkPermissionGrantedForTest = false

    @Before
    fun preserveExistingSession() = runBlocking {
        val snapshot = SessionSnapshot(
            serverUrl = sessionRepository.getBaseUrl().first(),
            accessToken = sessionRepository.getAccessToken().first(),
            userId = sessionRepository.getCurrentUserId().first(),
            userName = sessionRepository.getCurrentUserName().first(),
            serverId = sessionRepository.getServerId().first(),
            deviceId = sessionRepository.getDeviceId().first(),
        )
        originalSession = snapshot
        assumeTrue(
            "Skipping: existing partial credentials cannot be restored through SessionRepository",
            snapshot.isSafelyRestorable,
        )
        sessionSnapshotIsRestorable = true
    }

    @After
    fun closeActivityAndRestoreSession() = runBlocking {
        activityScenario?.close()
        activityScenario = null
        mockServers.forEach(MockWebServer::shutdown)
        mockServers.clear()
        temporarySavedServerUrls.forEach { sessionRepository.removeSavedServer(it) }
        temporarySavedServerUrls.clear()
        val untrackedSavedMockServerUrls = sessionRepository.getSavedServers().first()
            .map { it.url.trimEnd('/') }
            .toSet()
            .intersect(mockServerUrlsCreatedByThisTest)
        mockServerUrlsCreatedByThisTest.forEach { sessionRepository.removeSavedServer(it) }
        mockServerUrlsCreatedByThisTest.clear()

        if (localNetworkPermissionGrantedForTest) {
            InstrumentationRegistry.getInstrumentation().uiAutomation
                .executeShellCommand(
                    "pm revoke ${context.packageName} android.permission.ACCESS_LOCAL_NETWORK",
                ).close()
            localNetworkPermissionGrantedForTest = false
        }

        if (!sessionSnapshotIsRestorable) return@runBlocking
        val snapshot = originalSession ?: return@runBlocking
        sessionRepository.clearSession()
        sessionRepository.setBaseUrl(snapshot.serverUrl)
        val token = snapshot.accessToken
        val userId = snapshot.userId
        if (!token.isNullOrBlank() && !userId.isNullOrBlank()) {
            sessionRepository.saveSession(
                snapshot.serverUrl,
                token,
                userId,
                snapshot.userName,
                snapshot.serverId,
                snapshot.deviceId,
            )
        } else {
            sessionRepository.setServerId(snapshot.serverId)
        }
        assertTrue(
            "Todo URL de MockWebServer persistida durante o teste deve estar registrada para limpeza: " +
                untrackedSavedMockServerUrls,
            untrackedSavedMockServerUrls.isEmpty(),
        )
    }

    @Test
    fun missingSessionStartsInAuthenticationFlow() = runBlocking {
        val server = startMockServer()
        sessionRepository.clearSession()
        sessionRepository.setBaseUrl(server.url("/").toString().trimEnd('/'))
        launchMainActivity()

        composeRule.waitUntil(timeoutMillis = 20_000) {
            serverSelectionVisible() || loginVisible()
        }
        assertTrue(
            "Sem sessão, o app deve mostrar seleção de servidor ou login, nunca a Home",
            serverSelectionVisible() || loginVisible(),
        )
    }

    @Test
    fun quickConnectCodeAndPollingSurviveActivityRecreation() = runBlocking {
        if (context.checkSelfPermission(android.Manifest.permission.ACCESS_LOCAL_NETWORK) !=
            android.content.pm.PackageManager.PERMISSION_GRANTED
        ) {
            InstrumentationRegistry.getInstrumentation().uiAutomation
                .executeShellCommand(
                    "pm grant ${context.packageName} android.permission.ACCESS_LOCAL_NETWORK",
                ).close()
            localNetworkPermissionGrantedForTest = true
        }

        val receivedRequests = ConcurrentLinkedQueue<String>()
        val server = MockWebServer().apply {
            dispatcher = object : Dispatcher() {
                override fun dispatch(request: RecordedRequest): MockResponse {
                    val path = request.path.orEmpty()
                    receivedRequests.add("${request.method} $path")
                    return when (path.substringBefore('?')) {
                        "/System/Info/Public" -> MockResponse()
                            .setHeader("Content-Type", "application/json")
                            .setBody("""{"Id":"quick-connect-server","ServerName":"Quick Connect fixture","Version":"12.0.0"}""")
                        "/Users/Public" -> MockResponse()
                            .setHeader("Content-Type", "application/json")
                            .setBody("[]")
                        "/QuickConnect/Enabled" -> MockResponse()
                            .setHeader("Content-Type", "application/json")
                            .setBody("true")
                        "/QuickConnect/Initiate" -> MockResponse()
                            .setHeader("Content-Type", "application/json")
                            .setBody("""{"Code":"123456","Secret":"quick-connect-secret","Authenticated":false}""")
                        "/QuickConnect/Connect" -> MockResponse()
                            .setHeader("Content-Type", "application/json")
                            .setBody("""{"Authenticated":false}""")
                        else -> MockResponse().setResponseCode(404)
                    }
                }
            }
            start()
        }
        mockServers += server
        trackMockServerUrl(server)
        val serverUrl = server.url("/").toString().trimEnd('/')
        sessionRepository.clearSession()
        sessionRepository.setBaseUrl(serverUrl)
        launchMainActivity()

        composeRule.waitUntil(timeoutMillis = 20_000) { serverSelectionVisible() }
        composeRule.onNodeWithTag("auth.server.url").performTextClearance()
        composeRule.onNodeWithTag("auth.server.url").performTextInput(serverUrl)
        composeRule.onNodeWithText("Conectar", useUnmergedTree = true).performClick()
        composeRule.waitUntil(timeoutMillis = 20_000) { loginVisible() }
        composeRule.onNodeWithTag("auth.quick_connect.tab").performClick()
        composeRule.onNodeWithTag("auth.quick_connect.initiate").performClick()
        composeRule.waitUntil(timeoutMillis = 20_000) {
            runCatching { composeRule.onNodeWithTag("auth.quick_connect.code").assertIsDisplayed() }.isSuccess
        }
        composeRule.onNodeWithTag("auth.quick_connect.code").assertIsDisplayed()

        val pollCountBeforeRecreation = receivedRequests.count {
            it.startsWith("GET /QuickConnect/Connect?secret=quick-connect-secret")
        }
        activityScenario?.recreate()

        composeRule.waitUntil(timeoutMillis = 20_000) {
            runCatching { composeRule.onNodeWithTag("auth.quick_connect.code").assertIsDisplayed() }.isSuccess
        }
        composeRule.onNodeWithTag("auth.quick_connect.code").assertIsDisplayed()
        val pollingContinued = runCatching {
            composeRule.waitUntil(timeoutMillis = 15_000) {
                receivedRequests.count {
                    it.startsWith("GET /QuickConnect/Connect?secret=quick-connect-secret")
                } > pollCountBeforeRecreation
            }
        }.isSuccess
        assertTrue(
            "Quick Connect polling must continue after Activity recreation; requests=${receivedRequests.toList()}",
            pollingContinued,
        )
    }

    @Test
    fun quickConnectAuthorizationPersistsSessionAndOpensHome() = runBlocking {
        if (context.checkSelfPermission(android.Manifest.permission.ACCESS_LOCAL_NETWORK) !=
            android.content.pm.PackageManager.PERMISSION_GRANTED
        ) {
            InstrumentationRegistry.getInstrumentation().uiAutomation
                .executeShellCommand(
                    "pm grant ${context.packageName} android.permission.ACCESS_LOCAL_NETWORK",
                ).close()
            localNetworkPermissionGrantedForTest = true
        }

        val requests = ConcurrentLinkedQueue<String>()
        val serverId = "quick-connect-integration-server"
        val userId = "quick-connect-integration-user"
        val server = MockWebServer().apply {
            dispatcher = object : Dispatcher() {
                override fun dispatch(request: RecordedRequest): MockResponse {
                    requests.add("${request.method} ${request.path}")
                    return when (request.path?.substringBefore('?')) {
                        "/System/Info/Public" -> MockResponse()
                            .setHeader("Content-Type", "application/json")
                            .setBody("""{"Id":"$serverId","ServerName":"Quick Connect fixture","Version":"12.0.0"}""")
                        "/Users/Public" -> MockResponse()
                            .setHeader("Content-Type", "application/json")
                            .setBody("[]")
                        "/QuickConnect/Enabled" -> MockResponse()
                            .setHeader("Content-Type", "application/json")
                            .setBody("true")
                        "/QuickConnect/Initiate" -> MockResponse()
                            .setHeader("Content-Type", "application/json")
                            .setBody("""{"Code":"654321","Secret":"authorized-secret","Authenticated":false}""")
                        "/QuickConnect/Connect" -> MockResponse()
                            .setHeader("Content-Type", "application/json")
                            .setBody(
                                """{"Code":"654321","Secret":"authorized-secret","Authenticated":true}""",
                            )
                        "/Users/AuthenticateWithQuickConnect" -> MockResponse()
                            .setHeader("Content-Type", "application/json")
                            .setBody(
                                """{"AccessToken":"quick-connect-token","ServerId":"$serverId","User":{"Id":"$userId","Name":"Quick Connect user","ServerId":"$serverId"}}""",
                            )
                        else -> MockResponse().setResponseCode(404)
                    }
                }
            }
            start()
        }
        mockServers += server
        trackMockServerUrl(server)
        val serverUrl = server.url("/").toString().trimEnd('/')
        sessionRepository.clearSession()
        sessionRepository.setBaseUrl(serverUrl)
        launchMainActivity()

        composeRule.waitUntil(timeoutMillis = 20_000) { serverSelectionVisible() }
        composeRule.onNodeWithTag("auth.server.url").performTextClearance()
        composeRule.onNodeWithTag("auth.server.url").performTextInput(serverUrl)
        composeRule.onNodeWithText("Conectar", useUnmergedTree = true).performClick()
        composeRule.waitUntil(timeoutMillis = 20_000) { loginVisible() }
        composeRule.onNodeWithTag("auth.quick_connect.tab").performClick()
        composeRule.onNodeWithTag("auth.quick_connect.initiate").performClick()

        composeRule.waitUntil(timeoutMillis = 20_000) {
            requests.any { it.startsWith("POST /QuickConnect/Initiate") }
        }
        composeRule.waitUntil(timeoutMillis = 20_000) {
            requests.any { it.startsWith("POST /Users/AuthenticateWithQuickConnect") }
        }
        val homeOpened = runCatching {
            composeRule.waitUntil(timeoutMillis = 30_000) { homeVisible() }
        }.isSuccess
        assertTrue(
            "Após a autenticação Quick Connect, a sessão deve abrir a Home; " +
                "requests=${requests.toList()}, screen=${displayedTexts()}",
            homeOpened,
        )
        assertEquals("quick-connect-token", sessionRepository.getAccessToken().first())
        assertEquals(userId, sessionRepository.getCurrentUserId().first())
        assertEquals(serverId, sessionRepository.getServerId().first())
    }

    @Test
    fun incompletePersistedSessionStartsInAuthenticationFlow() = runBlocking {
        sessionRepository.saveSession(
            serverUrl = SessionRepositoryImpl.DEFAULT_MULLETAFLIX_SERVER_URL,
            token = "orphaned-instrumentation-token",
            userId = "orphaned-instrumentation-user",
            userName = null,
            serverId = null,
            deviceId = requireNotNull(originalSession).deviceId,
        )
        sessionRepository.setBaseUrl("")
        launchMainActivity()

        composeRule.waitUntil(timeoutMillis = 20_000) {
            serverSelectionVisible() || loginVisible()
        }
        assertTrue(
            "Credenciais sem URL do servidor não podem abrir a Home",
            serverSelectionVisible() || loginVisible(),
        )
    }

    @Test
    fun completePersistedSessionStartsAtHome() = runBlocking {
        val server = startMockServer()
        sessionRepository.saveSession(
            server.url("/").toString().trimEnd('/'),
            token = "instrumentation-session-token",
            userId = "instrumentation-user-id",
            userName = "Usuário de teste",
            serverId = "instrumentation-server-id",
            deviceId = requireNotNull(originalSession).deviceId,
        )
        launchMainActivity()

        composeRule.waitUntil(timeoutMillis = 20_000) { homeVisible() }
        composeRule.onNodeWithContentDescription("Buscar", useUnmergedTree = true).assertIsDisplayed()
        Unit
    }

    @Test
    fun settingsLogoutClearsPersistedSessionBeforeShowingServerSelection() = runBlocking {
        val server = startMockServer()
        val serverUrl = server.url("/").toString().trimEnd('/')
        sessionRepository.setBaseUrl(serverUrl)
        sessionRepository.saveSession(
            serverUrl = serverUrl,
            token = "settings-logout-instrumentation-token",
            userId = "settings-logout-instrumentation-user",
            userName = "Usuário de teste",
            serverId = "settings-logout-instrumentation-server",
            deviceId = requireNotNull(originalSession).deviceId,
        )
        launchMainActivity()

        composeRule.waitUntil(timeoutMillis = 20_000) { homeVisible() }
        openSettingsFromHome()
        composeRule.onNodeWithText("Sair", useUnmergedTree = true).performClick()

        composeRule.waitUntil(timeoutMillis = 20_000) { serverSelectionVisible() }
        assertTrue("Logout deve limpar o token persistido antes de sair de Ajustes", sessionRepository.getAccessToken().first().isNullOrBlank())
        assertTrue("Logout deve limpar o usuário persistido", sessionRepository.getCurrentUserId().first().isNullOrBlank())
        assertEquals("Logout deve manter o servidor para reconexão", serverUrl, sessionRepository.getBaseUrl().first())
    }

    @Test
    fun profileQuickSwitchPersistsSelectedAccountAndKeepsServerAndDevice() = runBlocking {
        if (context.checkSelfPermission(android.Manifest.permission.ACCESS_LOCAL_NETWORK) !=
            android.content.pm.PackageManager.PERMISSION_GRANTED
        ) {
            InstrumentationRegistry.getInstrumentation().uiAutomation
                .executeShellCommand(
                    "pm grant ${context.packageName} android.permission.ACCESS_LOCAL_NETWORK",
                ).close()
            localNetworkPermissionGrantedForTest = true
        }
        val requests = ConcurrentLinkedQueue<String>()
        val server = MockWebServer().apply {
            dispatcher = object : Dispatcher() {
                override fun dispatch(request: RecordedRequest): MockResponse {
                    val path = request.path.orEmpty().substringBefore('?')
                    requests.add("${request.method} $path")
                    return when (path) {
                        "/System/Info/Public" -> MockResponse()
                            .setHeader("Content-Type", "application/json")
                            .setBody("""{"Id":"profile-switch-server","ServerName":"Profile fixture","Version":"12.0.0"}""")
                        "/Users/Public" -> MockResponse()
                            .setHeader("Content-Type", "application/json")
                            .setBody(
                                """[{"Id":"old-user","Name":"Conta atual"},{"Id":"guest-user","Name":"Conta secundária"}]""",
                            )
                        "/QuickConnect/Enabled" -> MockResponse()
                            .setHeader("Content-Type", "application/json")
                            .setBody("false")
                        "/Users/Me" -> {
                            val user = if (request.getHeader("Authorization").orEmpty().contains("guest-token")) {
                                "guest-user" to "Conta secundária"
                            } else {
                                "old-user" to "Conta atual"
                            }
                            MockResponse()
                                .setHeader("Content-Type", "application/json")
                                .setBody("""{"Id":"${user.first}","Name":"${user.second}"}""")
                        }
                        "/Users/AuthenticateByName" -> {
                            val body = request.body.readUtf8()
                            requests.add("AUTH $body")
                            if (!body.contains("\"Username\":\"Conta secundária\"")) {
                                MockResponse().setResponseCode(401)
                            } else {
                                MockResponse()
                                    .setHeader("Content-Type", "application/json")
                                    .setBody(
                                        """{"AccessToken":"guest-token","ServerId":"profile-switch-server","User":{"Id":"guest-user","Name":"Conta secundária"}}""",
                                    )
                            }
                        }
                        else -> MockResponse().setResponseCode(404)
                    }
                }
            }
            start()
        }
        mockServers += server
        trackMockServerUrl(server)
        val serverUrl = server.url("/").toString().trimEnd('/')
        val deviceId = requireNotNull(originalSession).deviceId
        sessionRepository.setBaseUrl(serverUrl)
        sessionRepository.saveSession(
            serverUrl = serverUrl,
            token = "old-token",
            userId = "old-user",
            userName = "Conta atual",
            serverId = "profile-switch-server",
            deviceId = deviceId,
        )
        launchMainActivity()

        composeRule.waitUntil(timeoutMillis = 20_000) { homeVisible() }
        composeRule.waitUntil(timeoutMillis = 20_000) {
            runCatching {
                composeRule.onNodeWithContentDescription("Perfil de Conta atual", useUnmergedTree = true)
                    .assertIsDisplayed()
            }.isSuccess
        }
        openProfileFromHome()
        val switcherLoaded = runCatching {
            composeRule.waitUntil(timeoutMillis = 20_000) {
                runCatching {
                    composeRule.onNodeWithText("Conta secundária").assertIsDisplayed()
                }.isSuccess
            }
        }.isSuccess
        assertTrue(
            "O Perfil deve carregar os outros usuários públicos antes de permitir a troca; " +
                "requests=$requests, text=${displayedTexts()}",
            switcherLoaded,
        )
        composeRule.onNodeWithText("Conta secundária").performScrollTo().performClick()
        composeRule.onNodeWithText("Entrar como Conta secundária").assertIsDisplayed()
        composeRule.onNodeWithText("Alternar").performClick()

        composeRule.waitUntil(timeoutMillis = 20_000) {
            requests.any {
                it.startsWith("AUTH ") && it.contains("\"Username\":\"Conta secundária\"")
            }
        }
        withTimeout(20_000) {
            sessionRepository.getCurrentUserId().first { it == "guest-user" }
        }
        val reopenedSession = SessionRepositoryImpl(context)
        assertEquals(serverUrl, reopenedSession.getBaseUrl().first())
        assertEquals("profile-switch-server", reopenedSession.getServerId().first())
        assertEquals("guest-user", reopenedSession.getCurrentUserId().first())
        assertEquals("Conta secundária", reopenedSession.getCurrentUserName().first())
        assertEquals("guest-token", reopenedSession.getAccessToken().first())
        assertEquals(deviceId, reopenedSession.getDeviceId().first())
        assertTrue("A troca deve autenticar a conta escolhida", requests.any { it.startsWith("AUTH ") })

        composeRule.waitUntil(timeoutMillis = 20_000) {
            composeRule.onAllNodesWithText("Conta atual", useUnmergedTree = true)
                .fetchSemanticsNodes().isNotEmpty()
        }
        assertEquals(
            "Após a troca, o cabeçalho do Perfil deve refletir a conta autenticada, " +
                "e a conta anterior deve voltar à lista de alternância",
            1,
            composeRule.onAllNodesWithText("Conta secundária", useUnmergedTree = true)
                .fetchSemanticsNodes().size,
        )
    }

    @Test
    fun switchingServerFromProfileDoesNotRestorePreviousProfileOnBackAfterLogin() = runBlocking {
        if (context.checkSelfPermission(android.Manifest.permission.ACCESS_LOCAL_NETWORK) !=
            android.content.pm.PackageManager.PERMISSION_GRANTED
        ) {
            InstrumentationRegistry.getInstrumentation().uiAutomation
                .executeShellCommand(
                    "pm grant ${context.packageName} android.permission.ACCESS_LOCAL_NETWORK",
                ).close()
            localNetworkPermissionGrantedForTest = true
        }
        val oldServer = startMockServer()
        val oldServerUrl = oldServer.url("/").toString().trimEnd('/')
        val newServerRequests = ConcurrentLinkedQueue<String>()
        val newServer = MockWebServer().apply {
            dispatcher = object : Dispatcher() {
                override fun dispatch(request: RecordedRequest): MockResponse {
                    newServerRequests.add("${request.method} ${request.path}")
                    return when (request.path) {
                    "/System/Info/Public" -> MockResponse()
                        .setHeader("Content-Type", "application/json")
                        .setBody("""{"Id":"new-server","ServerName":"New server","Version":"12.0.0"}""")
                    "/Users/Public" -> MockResponse()
                        .setHeader("Content-Type", "application/json")
                        .setBody("[]")
                    "/QuickConnect/Enabled" -> MockResponse()
                        .setHeader("Content-Type", "application/json")
                        .setBody("false")
                    "/Users/AuthenticateByName" -> MockResponse()
                        .setHeader("Content-Type", "application/json")
                        .setBody("""{"AccessToken":"new-token","ServerId":"new-server","User":{"Id":"new-user","Name":"New user","ServerId":"new-server"}}""")
                    else -> MockResponse().setResponseCode(404)
                }
                }
            }
            start()
        }
        mockServers += newServer
        trackMockServerUrl(newServer)
        val newServerUrl = newServer.url("/").toString().trimEnd('/')
        sessionRepository.setBaseUrl(oldServerUrl)
        sessionRepository.saveSession(
            oldServerUrl,
            "old-token",
            "old-user",
            "Old user",
            "old-server",
            requireNotNull(originalSession).deviceId,
        )
        launchMainActivity()

        composeRule.waitUntil(timeoutMillis = 20_000) { homeVisible() }
        openProfileFromHome()
        composeRule.onNodeWithText("Trocar de Servidor", useUnmergedTree = true).performScrollTo().performClick()
        composeRule.waitUntil(timeoutMillis = 20_000) { serverSelectionVisible() }
        composeRule.onNodeWithTag("auth.server.url").performTextClearance()
        composeRule.onNodeWithTag("auth.server.url").performTextInput(newServerUrl)
        composeRule.onNodeWithText("Conectar", useUnmergedTree = true).performClick()
        val connectedToNewServer = runCatching {
            composeRule.waitUntil(timeoutMillis = 20_000) { loginVisible() }
        }.isSuccess
        assertTrue(
            "A seleção do novo servidor deve abrir login; tela=${displayedTexts()}, " +
                "url=${sessionRepository.getBaseUrl().first()}, requests=$newServerRequests",
            connectedToNewServer,
        )
        composeRule.onNodeWithTag("auth.login.username").performTextInput("new-user")
        composeRule.onNodeWithTag("auth.login.password").performTextInput("test-password")
        composeRule.onNodeWithTag("auth.login.submit").performClick()
        val authenticationRequestSent = runCatching {
            composeRule.waitUntil(timeoutMillis = 10_000) {
                "POST /Users/AuthenticateByName" in newServerRequests
            }
        }.isSuccess
        assertTrue(
            "Login deve enviar autenticação ao servidor novo; requests=$newServerRequests, " +
                "visibleText=${displayedTexts()}, savedUser=${sessionRepository.getCurrentUserId().first()}, " +
                "savedServerId=${sessionRepository.getServerId().first()}",
            authenticationRequestSent,
        )
        val homeAfterLogin = runCatching {
            composeRule.waitUntil(timeoutMillis = 20_000) { homeVisible() }
        }.isSuccess
        assertTrue(
            "Autenticação enviada não levou ao catálogo; requests=$newServerRequests, " +
                "visibleText=${displayedTexts()}, savedUser=${sessionRepository.getCurrentUserId().first()}, " +
                "savedServerId=${sessionRepository.getServerId().first()}, " +
                "hasAccessToken=${!sessionRepository.getAccessToken().first().isNullOrBlank()}",
            homeAfterLogin,
        )
        assertEquals(newServerUrl, sessionRepository.getBaseUrl().first())
        assertEquals("new-user", sessionRepository.getCurrentUserId().first())

        activityScenario?.onActivity { it.onBackPressedDispatcher.onBackPressed() }
        val leftAuthenticatedTask = runCatching {
            composeRule.waitUntil(timeoutMillis = 20_000) {
                requireNotNull(activityScenario).state != androidx.lifecycle.Lifecycle.State.RESUMED
            }
        }.isSuccess
        assertTrue(
            "Voltar da Home após trocar servidor deve sair da Activity, sem reabrir telas da sessão anterior",
            leftAuthenticatedTask,
        )
    }

    @Test
    fun canceledProfileServerSwitchDoesNotAffectLoginAfterLogout() = runBlocking {
        if (context.checkSelfPermission(android.Manifest.permission.ACCESS_LOCAL_NETWORK) !=
            android.content.pm.PackageManager.PERMISSION_GRANTED
        ) {
            InstrumentationRegistry.getInstrumentation().uiAutomation
                .executeShellCommand(
                    "pm grant ${context.packageName} android.permission.ACCESS_LOCAL_NETWORK",
                ).close()
            localNetworkPermissionGrantedForTest = true
        }
        val oldServer = startMockServer()
        val oldServerUrl = oldServer.url("/").toString().trimEnd('/')
        val targetServer = MockWebServer().apply {
            dispatcher = object : Dispatcher() {
                override fun dispatch(request: RecordedRequest): MockResponse = when (request.path?.substringBefore('?')) {
                    "/System/Info/Public" -> MockResponse()
                        .setHeader("Content-Type", "application/json")
                        .setBody("""{"Id":"logout-target","ServerName":"Logout target","Version":"12.0.0"}""")
                    "/Users/Public" -> MockResponse().setHeader("Content-Type", "application/json").setBody("[]")
                    "/QuickConnect/Enabled" -> MockResponse().setHeader("Content-Type", "application/json").setBody("false")
                    "/Users/AuthenticateByName" -> MockResponse()
                        .setHeader("Content-Type", "application/json")
                        .setBody("""{"AccessToken":"target-token","ServerId":"logout-target","User":{"Id":"target-user","Name":"Target user","ServerId":"logout-target"}}""")
                    else -> MockResponse().setResponseCode(404)
                }
            }
            start()
        }
        mockServers += targetServer
        trackMockServerUrl(targetServer)
        val targetServerUrl = targetServer.url("/").toString().trimEnd('/')
        sessionRepository.setBaseUrl(oldServerUrl)
        sessionRepository.saveSession(
            oldServerUrl,
            "old-token",
            "old-user",
            "Old user",
            "old-server",
            requireNotNull(originalSession).deviceId,
        )
        launchMainActivity()

        composeRule.waitUntil(timeoutMillis = 20_000) { homeVisible() }
        openProfileFromHome()
        composeRule.onNodeWithText("Trocar de Servidor", useUnmergedTree = true).performScrollTo().performClick()
        composeRule.waitUntil(timeoutMillis = 20_000) { serverSelectionVisible() }
        activityScenario?.onActivity { it.onBackPressedDispatcher.onBackPressed() }
        composeRule.waitUntil(timeoutMillis = 20_000) {
            runCatching { composeRule.onNodeWithText("Sair da Conta", useUnmergedTree = true).assertExists() }.isSuccess
        }
        composeRule.onNodeWithText("Sair da Conta", useUnmergedTree = true).performScrollTo().performClick()
        composeRule.onNodeWithText("Sair", useUnmergedTree = true).performClick()
        composeRule.waitUntil(timeoutMillis = 20_000) { serverSelectionVisible() }

        composeRule.onNodeWithTag("auth.server.url").performTextClearance()
        composeRule.onNodeWithTag("auth.server.url").performTextInput(targetServerUrl)
        composeRule.onNodeWithText("Conectar", useUnmergedTree = true).performClick()
        composeRule.waitUntil(timeoutMillis = 20_000) { loginVisible() }
        composeRule.onNodeWithTag("auth.login.username").performTextInput("target-user")
        composeRule.onNodeWithTag("auth.login.password").performTextInput("test-password")
        composeRule.onNodeWithTag("auth.login.submit").performClick()
        val homeOpened = runCatching {
            composeRule.waitUntil(timeoutMillis = 20_000) { homeVisible() }
        }.isSuccess
        assertTrue(
            "Após cancelar a troca e encerrar a sessão, novo login independente deve abrir Home; " +
                "visibleText=${displayedTexts()}, baseUrl=${sessionRepository.getBaseUrl().first()}",
            homeOpened,
        )
        activityScenario?.onActivity { it.onBackPressedDispatcher.onBackPressed() }
        composeRule.waitUntil(timeoutMillis = 20_000) {
            requireNotNull(activityScenario).state != androidx.lifecycle.Lifecycle.State.RESUMED
        }
    }

    @Test
    fun crossServerDeepLinkAuthenticatesOnTargetServerAndLoadsItemWithoutRequestingOldServer() = runBlocking {
        if (context.checkSelfPermission(android.Manifest.permission.ACCESS_LOCAL_NETWORK) !=
            android.content.pm.PackageManager.PERMISSION_GRANTED
        ) {
            InstrumentationRegistry.getInstrumentation().uiAutomation
                .executeShellCommand(
                    "pm grant ${context.packageName} android.permission.ACCESS_LOCAL_NETWORK",
                ).close()
            localNetworkPermissionGrantedForTest = true
        }
        val oldServerRequests = ConcurrentLinkedQueue<String>()
        val oldServer = MockWebServer().apply {
            dispatcher = object : Dispatcher() {
                override fun dispatch(request: RecordedRequest): MockResponse {
                    oldServerRequests.add("${request.method} ${request.path}")
                    return MockResponse().setResponseCode(404)
                }
            }
            start()
        }
        mockServers += oldServer
        trackMockServerUrl(oldServer)
        val oldServerUrl = oldServer.url("/").toString().trimEnd('/')
        val targetItemId = "server-b-item"
        val targetUserId = "server-b-user"
        val targetServerRequests = ConcurrentLinkedQueue<String>()
        val targetAuthenticationBodies = ConcurrentLinkedQueue<String>()
        val targetServer = MockWebServer().apply {
            dispatcher = object : Dispatcher() {
                override fun dispatch(request: RecordedRequest): MockResponse {
                    targetServerRequests.add("${request.method} ${request.path}")
                    if (request.path == "/Users/AuthenticateByName") {
                        val body = request.body.readUtf8()
                        targetAuthenticationBodies.add(body)
                        if (!body.contains("\"Username\":\"raphael\"") ||
                            !body.contains("\"Pw\":\"test-password\"")
                        ) return MockResponse().setResponseCode(401)
                    }
                    return when (request.path) {
                        "/System/Info/Public" -> MockResponse()
                            .setHeader("Content-Type", "application/json")
                            .setBody("""{"Id":"server-b","ServerName":"Server B","Version":"12.0.0"}""")
                        "/Users/Public" -> MockResponse()
                            .setHeader("Content-Type", "application/json")
                            .setBody("[]")
                        "/QuickConnect/Enabled" -> MockResponse()
                            .setHeader("Content-Type", "application/json")
                            .setBody("false")
                        "/Users/AuthenticateByName" -> MockResponse()
                            .setHeader("Content-Type", "application/json")
                            .setBody(
                                """{"AccessToken":"server-b-token","ServerId":"server-b","User":{"Id":"$targetUserId","Name":"Server B user","ServerId":"server-b"}}""",
                            )
                        "/Users/$targetUserId/Items/$targetItemId" -> MockResponse()
                            .setHeader("Content-Type", "application/json")
                            .setBody(
                                """{"Id":"$targetItemId","Name":"Server B media","ServerId":"server-b","Type":"Movie"}""",
                            )
                        else -> MockResponse().setResponseCode(404)
                    }
                }
            }
            start()
        }
        mockServers += targetServer
        trackMockServerUrl(targetServer)
        val targetServerUrl = targetServer.url("/").toString().trimEnd('/')
        sessionRepository.setBaseUrl(oldServerUrl)
        sessionRepository.saveSession(
            serverUrl = oldServerUrl,
            token = "old-server-token",
            userId = "old-server-user",
            userName = "Old user",
            serverId = "server-a",
            deviceId = requireNotNull(originalSession).deviceId,
        )
        val launchIntent = Intent(context, MainActivity::class.java).setData(
            Uri.parse("mulletaflix://details?id=server-b-item&serverId=server-b"),
        )
        launchMainActivity(launchIntent)

        composeRule.waitUntil(timeoutMillis = 20_000) { serverSelectionVisible() }
        Thread.sleep(750)
        composeRule.onNodeWithText("URL do Servidor", useUnmergedTree = true).assertIsDisplayed()
        assertFalse(
            "O fluxo não deve avançar para login usando o servidor antigo",
            loginVisible(),
        )
        assertTrue(
            "A troca para o servidor do deep link não deve consultar a sessão anterior: $oldServerRequests",
            oldServerRequests.isEmpty(),
        )

        composeRule.onNodeWithTag("auth.server.url").performTextClearance()
        composeRule.onNodeWithTag("auth.server.url").performTextInput(targetServerUrl)
        composeRule.onNodeWithText("Conectar", useUnmergedTree = true).performClick()

        val targetServerVerified = runCatching {
            composeRule.waitUntil(timeoutMillis = 20_000) {
                targetServerRequests.contains("GET /Users/Public")
            }
        }.isSuccess
        assertTrue(
            "B precisa validar e carregar usuários antes do login; requests=$targetServerRequests, " +
                "savedUrl=${sessionRepository.getBaseUrl().first()}, " +
                "savedServerId=${sessionRepository.getServerId().first()}, " +
                "visibleText=${displayedTexts()}",
            targetServerVerified,
        )
        assertTrue(
            "A validação pública precisa ter ocorrido no destino antes da consulta dos usuários",
            "GET /System/Info/Public" in targetServerRequests,
        )
        assertTrue("A tela de login deve abrir após validar B", loginVisible())
        assertEquals(
            "A URL persistida precisa continuar no servidor B depois da conexão e antes do login",
            targetServerUrl,
            sessionRepository.getBaseUrl().first(),
        )
        composeRule.onNodeWithTag("auth.login.username").performTextInput("raphael")
        composeRule.onNodeWithTag("auth.login.password").performTextInput("test-password")
        assertTrue(
            "A tela de login precisa continuar exibindo o servidor B imediatamente antes do envio",
            runCatching { composeRule.onNodeWithText(targetServerUrl).assertIsDisplayed() }.isSuccess,
        )
        assertEquals(
            "O endpoint ativo precisa continuar em B imediatamente antes do envio",
            targetServerUrl,
            sessionRepository.getBaseUrl().first(),
        )
        composeRule.onNodeWithTag("auth.login.submit").performClick()

        val targetItemRequested = runCatching {
            composeRule.waitUntil(timeoutMillis = 30_000) {
                "GET /Users/$targetUserId/Items/$targetItemId" in targetServerRequests
            }
        }.isSuccess
        assertTrue(
            "Após autenticação em B, o app deve solicitar o item do link. " +
                "requests=$targetServerRequests, authBodies=$targetAuthenticationBodies, " +
                "savedUrl=${sessionRepository.getBaseUrl().first()}, " +
                "savedServerId=${sessionRepository.getServerId().first()}, " +
                "savedUser=${sessionRepository.getCurrentUserId().first()}, " +
                "loginVisible=${loginVisible()}, visibleText=${displayedTexts()}",
            targetItemRequested,
        )
        assertTrue(
            "O login deve usar o endpoint de autenticação do servidor indicado pelo link",
            "POST /Users/AuthenticateByName" in targetServerRequests,
        )
        assertTrue(
            "As credenciais escolhidas devem ser enviadas a B",
            targetAuthenticationBodies.singleOrNull()?.let {
                it.contains("\"Username\":\"raphael\"") && it.contains("\"Pw\":\"test-password\"")
            } == true,
        )
        assertTrue(
            "O item do link deve ser solicitado ao servidor B após o login",
            "GET /Users/$targetUserId/Items/$targetItemId" in targetServerRequests,
        )
        val reopenedSession = SessionRepositoryImpl(context)
        assertEquals(targetServerUrl, reopenedSession.getBaseUrl().first())
        assertEquals("server-b", reopenedSession.getServerId().first())
        assertEquals("server-b-user", reopenedSession.getCurrentUserId().first())
        assertEquals("Server B user", reopenedSession.getCurrentUserName().first())
        assertEquals("server-b-token", reopenedSession.getAccessToken().first())

        activityScenario?.onActivity { activity ->
            activity.onBackPressedDispatcher.onBackPressed()
        }
        composeRule.waitUntil(timeoutMillis = 20_000) { homeVisible() }
        assertEquals(
            "Voltar de um detalhe aberto por deep link deve levar à Home do servidor autenticado",
            targetServerUrl,
            sessionRepository.getBaseUrl().first(),
        )
        assertTrue(
            "O fluxo cross-server não pode consultar o servidor A antes nem depois do login",
            oldServerRequests.isEmpty(),
        )
    }

    @Test
    fun authenticatedMediaDeepLinkOpensDetailsAndRequestsLinkedItem() = runBlocking {
        val itemId = "instrumentation-deep-link-item"
        val serverId = "instrumentation-deep-link-server"
        val receivedRequests = ConcurrentLinkedQueue<String>()
        val server = MockWebServer().apply {
            dispatcher = object : Dispatcher() {
                override fun dispatch(request: RecordedRequest): MockResponse {
                    receivedRequests.add("${request.method} ${request.path}")
                    return if (request.path == "/Users/instrumentation-deep-link-user/Items/$itemId") {
                    MockResponse()
                        .setResponseCode(200)
                        .setHeader("Content-Type", "application/json")
                        .setBody(
                            """{"Id":"$itemId","Name":"Linked media fixture","ServerId":"$serverId","Type":"Movie"}""",
                        )
                } else {
                    MockResponse().setResponseCode(404)
                }
                }
            }
            start()
        }
        mockServers += server
        trackMockServerUrl(server)
        sessionRepository.saveSession(
            server.url("/").toString().trimEnd('/'),
            token = "instrumentation-deep-link-token",
            userId = "instrumentation-deep-link-user",
            userName = "Usuário de teste",
            serverId = serverId,
            deviceId = requireNotNull(originalSession).deviceId,
        )
        val launchIntent = Intent(context, MainActivity::class.java).setData(
            Uri.parse("mulletaflix://details?id=$itemId&serverId=$serverId"),
        )
        launchMainActivity(launchIntent)

        composeRule.waitUntil(timeoutMillis = 20_000) {
            receivedRequests.any { it == "GET /Users/instrumentation-deep-link-user/Items/$itemId" }
        }
        assertTrue(
            "O deep link autenticado deve disparar GET ao endpoint exato do item solicitado",
            "GET /Users/instrumentation-deep-link-user/Items/$itemId" in receivedRequests,
        )

        activityScenario?.onActivity { activity ->
            activity.onBackPressedDispatcher.onBackPressed()
        }
        composeRule.waitForIdle()
        assertTrue(
            "Voltar do detalhe aberto por deep link deve restaurar a Home, não encerrar a Activity. " +
                "Tela: ${displayedTexts()}",
            homeVisible(),
        )
    }

    @Test
    fun authenticatedBookDeepLinkOpensReaderAndSurvivesActivityRecreation() = runBlocking {
        val itemId = "activity-book-reader-item"
        val serverId = "activity-book-reader-server"
        val userId = "activity-book-reader-user"
        val receivedRequests = ConcurrentLinkedQueue<String>()
        val server = MockWebServer().apply {
            dispatcher = object : Dispatcher() {
                override fun dispatch(request: RecordedRequest): MockResponse {
                    receivedRequests.add("${request.method} ${request.path}")
                    return when (request.path?.substringBefore('?')) {
                        "/Users/$userId/Items/$itemId" -> MockResponse()
                            .setHeader("Content-Type", "application/json")
                            .setBody(
                                """{"Id":"$itemId","Name":"Livro da Activity","ServerId":"$serverId","Type":"Book"}""",
                            )
                        "/BookReader/Items/$itemId/BookReader/Status" -> MockResponse()
                            .setHeader("Content-Type", "application/json")
                            .setBody("""{"Status":"Ready"}""")
                        "/BookReader/Items/$itemId/BookReader/Epub" -> MockResponse()
                            .setHeader("Content-Type", "text/plain; charset=utf-8")
                            .setBody("Conteúdo do livro na Activity principal.")
                        else -> MockResponse().setResponseCode(404)
                    }
                }
            }
            start()
        }
        mockServers += server
        trackMockServerUrl(server)
        sessionRepository.saveSession(
            server.url("/").toString().trimEnd('/'),
            token = "activity-book-reader-token",
            userId = userId,
            userName = "Leitor de teste",
            serverId = serverId,
            deviceId = requireNotNull(originalSession).deviceId,
        )
        val launchIntent = Intent(context, MainActivity::class.java).setData(
            Uri.parse("mulletaflix://details?id=$itemId&serverId=$serverId"),
        )
        launchMainActivity(launchIntent)

        composeRule.waitUntil(timeoutMillis = 20_000) {
            receivedRequests.contains("GET /Users/$userId/Items/$itemId") &&
                runCatching { composeRule.onNodeWithText("Ler livro").assertIsDisplayed() }.isSuccess
        }
        composeRule.onNodeWithText("Ler livro").performClick()
        composeRule.waitUntil(timeoutMillis = 20_000) {
            "GET /BookReader/Items/$itemId/BookReader/Epub" in receivedRequests &&
                runCatching {
                    composeRule.onNodeWithText("Conteúdo do livro na Activity principal.").assertIsDisplayed()
                }.isSuccess
        }

        activityScenario?.recreate()

        composeRule.waitUntil(timeoutMillis = 20_000) {
            runCatching {
                composeRule.onNodeWithText("Conteúdo do livro na Activity principal.").assertIsDisplayed()
            }.isSuccess
        }
        assertTrue(
            "A recriação da Activity deve manter o destino do leitor e o conteúdo visível",
            receivedRequests.contains("GET /BookReader/Items/$itemId/BookReader/Epub"),
        )
    }

    @Test
    @AndroidTvOnly
    fun liveTvChannelSelectionReachesPlaybackAndOpenStreamWithTheChannelId() {
        assumeTrue(
            "Este fluxo de foco e navegação exige um dispositivo Android TV",
            context.packageManager.hasSystemFeature(android.content.pm.PackageManager.FEATURE_LEANBACK),
        )
        runBlocking {
        journeyRequests.clear()
        openedLiveStreamItemIds.clear()
        val channelId = "live-channel-journey-42"
        val server = startLiveTvJourneyServer(channelId, "Canal de jornada", "LiveTvChannel", requiresOpening = true)
        val serverUrl = server.url("/").toString().trimEnd('/')
        trackMockServerUrl(server)
        sessionRepository.saveSession(
            serverUrl = serverUrl,
            token = "live-tv-journey-token",
            userId = "live-tv-journey-user",
            userName = "Conta de teste",
            serverId = "live-tv-journey-server",
            deviceId = requireNotNull(originalSession).deviceId,
        )
        launchMainActivity()

        composeRule.waitUntil(timeoutMillis = 20_000) { homeVisible() }
        composeRule.onNodeWithContentDescription("TV Ao Vivo", useUnmergedTree = true).performClick()
        val channelVisible = runCatching {
            composeRule.waitUntil(timeoutMillis = 20_000) {
                runCatching { composeRule.onNodeWithText("Canal de jornada").assertIsDisplayed() }.isSuccess
            }
        }.isSuccess
        assertTrue("Canal não carregou; requests=$journeyRequests, tela=${displayedTexts()}", channelVisible)
        composeRule.onNodeWithContentDescription("Assistir Canal de jornada").performClick()

        composeRule.waitUntil(timeoutMillis = 30_000) {
            journeyRequests.contains("POST /Items/$channelId/PlaybackInfo") &&
                journeyRequests.contains("POST /LiveStreams/Open")
        }
        assertTrue("PlaybackInfo deve receber o ID exato do canal: $journeyRequests", journeyRequests.contains("POST /Items/$channelId/PlaybackInfo"))
        val recordedRequests = journeyRequests.toList()
        assertTrue("A abertura do tuner deve acontecer depois de PlaybackInfo: $recordedRequests", recordedRequests.indexOf("POST /LiveStreams/Open") > recordedRequests.indexOf("POST /Items/$channelId/PlaybackInfo"))
        assertEquals("O tuner deve receber o mesmo ID selecionado", channelId, openedLiveStreamItemIds.peek())
        }
    }

    @Test
    @AndroidTvOnly
    fun liveTvRecordingSelectionReachesPlaybackInfoWithTheRecordingId() {
        assumeTrue(
            "Este fluxo de foco e navegação exige um dispositivo Android TV",
            context.packageManager.hasSystemFeature(android.content.pm.PackageManager.FEATURE_LEANBACK),
        )
        runBlocking {
        journeyRequests.clear()
        openedLiveStreamItemIds.clear()
        val recordingId = "live-recording-journey-73"
        val server = startLiveTvJourneyServer(recordingId, "Gravação de jornada", "Recording", requiresOpening = false)
        val serverUrl = server.url("/").toString().trimEnd('/')
        trackMockServerUrl(server)
        sessionRepository.saveSession(
            serverUrl = serverUrl,
            token = "live-tv-journey-token",
            userId = "live-tv-journey-user",
            userName = "Conta de teste",
            serverId = "live-tv-journey-server",
            deviceId = requireNotNull(originalSession).deviceId,
        )
        launchMainActivity()

        composeRule.waitUntil(timeoutMillis = 20_000) { homeVisible() }
        composeRule.onNodeWithContentDescription("TV Ao Vivo", useUnmergedTree = true).performClick()
        composeRule.waitUntil(timeoutMillis = 20_000) {
            runCatching { composeRule.onNodeWithText("Gravações").assertIsDisplayed() }.isSuccess
        }
        composeRule.onAllNodesWithText("Gravação de jornada")[1].performClick()

        composeRule.waitUntil(timeoutMillis = 30_000) {
            journeyRequests.contains("POST /Items/$recordingId/PlaybackInfo")
        }
        assertTrue("PlaybackInfo deve receber o ID exato da gravação: $journeyRequests", journeyRequests.contains("POST /Items/$recordingId/PlaybackInfo"))
        assertFalse("Gravação já disponível não deve abrir como tuner: $journeyRequests", journeyRequests.contains("POST /LiveStreams/Open"))
        }
    }

    private val journeyRequests = ConcurrentLinkedQueue<String>()
    private val openedLiveStreamItemIds = ConcurrentLinkedQueue<String>()

    private fun startLiveTvJourneyServer(
        itemId: String,
        itemName: String,
        itemType: String,
        requiresOpening: Boolean,
    ): MockWebServer = MockWebServer().apply {
        dispatcher = object : Dispatcher() {
            override fun dispatch(request: RecordedRequest): MockResponse {
                val path = request.path.orEmpty().substringBefore('?')
                journeyRequests += "${request.method} $path"
                return when {
                    path == "/LiveTv/Channels" -> jsonResponse(
                        """{"Items":[{"Id":"$itemId","Name":"$itemName","Type":"LiveTvChannel"}],"TotalRecordCount":1}""",
                    )
                    path == "/LiveTv/Recordings" -> jsonResponse(
                        if (itemType == "Recording") {
                            """{"Items":[{"Id":"$itemId","Name":"$itemName","Type":"Recording"}],"TotalRecordCount":1}"""
                        } else {
                            """{"Items":[],"TotalRecordCount":0}"""
                        },
                    )
                    path == "/Users/live-tv-journey-user/Items/$itemId" -> jsonResponse(
                        """{"Id":"$itemId","Name":"$itemName","Type":"$itemType","MediaSources":[]}""",
                    )
                    path == "/Items/$itemId/PlaybackInfo" -> jsonResponse(
                        """{"PlaySessionId":"journey-play-session","MediaSources":[{"Id":"journey-source","Path":"http://10.0.2.2:${port}/stream.ts","Protocol":"Http","Container":"ts","RequiresOpening":$requiresOpening,"OpenToken":"journey-open-token"}]}""",
                    )
                    path == "/LiveStreams/Open" -> {
                        openedLiveStreamItemIds += request.requestUrl?.queryParameter("ItemId").orEmpty()
                        jsonResponse(
                            """{"MediaSource":{"Id":"opened-journey-source","Path":"http://10.0.2.2:${port}/stream.ts","Protocol":"Http","Container":"ts","LiveStreamId":"journey-live-feed"}}""",
                        )
                    }
                    else -> jsonResponse("""{"Items":[],"TotalRecordCount":0}""")
                }
            }
        }
        start()
        mockServers += this
    }

    private fun jsonResponse(body: String): MockResponse = MockResponse()
        .setHeader("Content-Type", "application/json")
        .setBody(body)

    private fun launchMainActivity(
        intent: Intent = Intent(context, MainActivity::class.java),
    ) {
        intent.putExtra(EXTRA_SUPPRESS_APP_UPDATE_CHECK_FOR_TEST, true)
        activityScenario = ActivityScenario.launch(intent)
    }

    private fun serverSelectionVisible(): Boolean =
        runCatching {
            composeRule.onNodeWithText("URL do Servidor", useUnmergedTree = true).assertIsDisplayed()
        }.isSuccess

    private fun loginVisible(): Boolean =
        runCatching { composeRule.onNodeWithTag("auth.login.username").assertIsDisplayed() }.isSuccess

    private fun homeVisible(): Boolean =
        runCatching {
            composeRule.onNodeWithContentDescription("Buscar", useUnmergedTree = true).assertIsDisplayed()
        }.isSuccess

    private fun openProfileFromHome() {
        val moreActions = composeRule.onNodeWithContentDescription("Mais ações", useUnmergedTree = true)
        if (runCatching { moreActions.assertIsDisplayed() }.isSuccess) {
            moreActions.performClick()
            val menuLabel = listOf("Meu Perfil", "Perfil de Conta atual", "Perfil de Old user")
                .firstOrNull { label ->
                    runCatching {
                        composeRule.onNodeWithText(label, useUnmergedTree = true).assertIsDisplayed()
                    }.isSuccess
                }
            composeRule.onNodeWithText(requireNotNull(menuLabel) { "A ação de Perfil não apareceu no menu compacto." }, useUnmergedTree = true)
                .performClick()
            return
        }

        val directProfileDescription = listOf("Meu Perfil", "Perfil de Conta atual", "Perfil de Old user")
            .firstOrNull { description ->
                runCatching {
                    composeRule.onNodeWithContentDescription(description, useUnmergedTree = true).assertIsDisplayed()
                }.isSuccess
            }
        composeRule.onNodeWithContentDescription(
            requireNotNull(directProfileDescription) { "A ação direta de Perfil não apareceu no Home amplo." },
            useUnmergedTree = true,
        ).performClick()
    }

    private fun openSettingsFromHome() {
        val moreActions = composeRule.onNodeWithContentDescription("Mais ações", useUnmergedTree = true)
        if (runCatching { moreActions.assertIsDisplayed() }.isSuccess) {
            moreActions.performClick()
            composeRule.onNodeWithText("Configurações", useUnmergedTree = true).performClick()
        } else {
            composeRule.onNodeWithContentDescription("Configurações", useUnmergedTree = true).performClick()
        }
    }

    private fun displayedTexts(): String =
        semanticsTree(composeRule.onRoot(useUnmergedTree = true).fetchSemanticsNode())
            .flatMap { node ->
                if (SemanticsProperties.Text in node.config) node.config[SemanticsProperties.Text].asSequence()
                else emptySequence()
            }
            .joinToString(" | ") { it.text }

    private fun semanticsTree(root: SemanticsNode): Sequence<SemanticsNode> =
        sequenceOf(root) + root.children.asSequence().flatMap(::semanticsTree)

    private fun startMockServer(): MockWebServer = MockWebServer().apply {
        dispatcher = object : Dispatcher() {
            override fun dispatch(request: RecordedRequest): MockResponse =
                MockResponse().setResponseCode(404)
        }
        start()
        mockServers += this
        trackMockServerUrl(this)
    }

    private fun trackMockServerUrl(server: MockWebServer) {
        val url = server.url("/").toString().trimEnd('/')
        mockServerUrlsCreatedByThisTest += url
        temporarySavedServerUrls += url
    }


    private data class SessionSnapshot(
        val serverUrl: String,
        val accessToken: String?,
        val userId: String?,
        val userName: String?,
        val serverId: String?,
        val deviceId: String,
    ) {
        val isSafelyRestorable: Boolean
            get() = (accessToken == null && userId == null && userName == null) ||
                (!accessToken.isNullOrBlank() && !userId.isNullOrBlank() &&
                    (userName == null || !userName.isBlank()))
    }
}
