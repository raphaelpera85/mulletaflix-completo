package org.mulletaflix.android

import android.content.Intent
import android.net.Uri
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.v2.createEmptyComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.onRoot
import androidx.compose.ui.test.performClick
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
        val targetServerUrl = targetServer.url("/").toString().trimEnd('/')
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
        activityScenario = ActivityScenario.launch(launchIntent)

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
        composeRule.onNodeWithTag("auth.login.username").performTextInput("raphael")
        composeRule.onNodeWithTag("auth.login.password").performTextInput("test-password")
        composeRule.onNodeWithTag("auth.login.submit").performClick()

        composeRule.waitUntil(timeoutMillis = 30_000) {
            "GET /Users/$targetUserId/Items/$targetItemId" in targetServerRequests
        }
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
        activityScenario = ActivityScenario.launch(launchIntent)

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

    private fun launchMainActivity() {
        activityScenario = ActivityScenario.launch(MainActivity::class.java)
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
