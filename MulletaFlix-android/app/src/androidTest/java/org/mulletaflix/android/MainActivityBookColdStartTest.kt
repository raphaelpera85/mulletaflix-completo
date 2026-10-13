package org.mulletaflix.android

import android.content.Intent
import android.os.Bundle
import android.net.Uri
import android.util.Base64
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.assertTextContains
import androidx.compose.ui.test.junit4.v2.createEmptyComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performTextInput
import androidx.test.core.app.ActivityScenario
import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import org.json.JSONObject
import org.junit.Assert.assertEquals
import org.junit.Assume.assumeTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.core.common.session.FeedbackRequestSession
import org.mulletaflix.core.common.session.mediaRequestDraftScopeHash
import org.mulletaflix.data.repository.SessionRepositoryImpl
import org.mulletaflix.data.repository.MediaRequestDraftStoreImpl
import org.mulletaflix.domain.repository.MediaRequestDraft

/** Host-orchestrated phases verify the real app process is killed between session setup and UI launch. */
@RunWith(AndroidJUnit4::class)
class MainActivityBookColdStartTest {
    @get:Rule
    val composeRule = createEmptyComposeRule()

    private val context = ApplicationProvider.getApplicationContext<android.content.Context>()
    private val sessionRepository = SessionRepositoryImpl(context)

    @Test
    fun captureOriginalSessionSnapshotForColdStart() = runBlocking {
        val existing = readSession()
        assumeTrue("Cannot safely restore the current partial session", existing.isSafelyRestorable)
        val snapshot = Base64.encodeToString(
            existing.toJson().toString().toByteArray(Charsets.UTF_8),
            Base64.NO_WRAP,
        )
        InstrumentationRegistry.getInstrumentation().sendStatus(
            0,
            Bundle().apply { putString(SESSION_SNAPSHOT_STATUS_KEY, snapshot) },
        )
        Unit
    }

    @Test
    fun prepareAuthenticatedSessionForColdStart() = runBlocking {
        val existing = readSession()
        val arguments = InstrumentationRegistry.getArguments()
        sessionRepository.saveSession(
            serverUrl = requireArgument(arguments.getString(ARG_SERVER_URL), ARG_SERVER_URL),
            token = TEST_TOKEN,
            userId = TEST_USER_ID,
            userName = "Cold start fixture",
            serverId = requireArgument(arguments.getString(ARG_SERVER_ID), ARG_SERVER_ID),
            deviceId = existing.deviceId,
        )
    }

    @Test
    fun authenticatedBookDeepLinkRendersAfterMainProcessRestart() = runBlocking {
        val arguments = InstrumentationRegistry.getArguments()
        val sessionSnapshot = requireArgument(
            arguments.getString(ARG_SESSION_SNAPSHOT),
            ARG_SESSION_SNAPSHOT,
        )
        val itemId = requireArgument(arguments.getString(ARG_ITEM_ID), ARG_ITEM_ID)
        val serverId = requireArgument(arguments.getString(ARG_SERVER_ID), ARG_SERVER_ID)
        val intent = Intent(context, MainActivity::class.java).setData(
            Uri.parse("mulletaflix://details?id=$itemId&serverId=$serverId"),
        )
        var scenario: ActivityScenario<MainActivity>? = null
        try {
            scenario = ActivityScenario.launch(intent)
            composeRule.waitUntil(timeoutMillis = 30_000) {
                runCatching { composeRule.onNodeWithText("Ler livro").assertIsDisplayed() }.isSuccess
            }
            composeRule.onNodeWithText("Ler livro").performClick()
            composeRule.waitUntil(timeoutMillis = 30_000) {
                runCatching {
                    composeRule.onNodeWithText(TEST_BOOK_CONTENT).assertIsDisplayed()
                }.isSuccess
            }
            composeRule.onNodeWithText(TEST_BOOK_CONTENT).assertIsDisplayed()
            val mainProcessId = requireNotNull(scenario).let { launchedScenario ->
                var processId = -1
                launchedScenario.onActivity { processId = android.os.Process.myPid() }
                processId
            }
            check(mainProcessId > 0) { "MainActivity did not report a valid process id" }
            InstrumentationRegistry.getInstrumentation().sendStatus(
                0,
                Bundle().apply { putInt(MAIN_PROCESS_PID_STATUS_KEY, mainProcessId) },
            )
        } finally {
            scenario?.close()
            restoreSessionFromSnapshot(sessionSnapshot)
        }
        Unit
    }

    @Test
    fun enterMediaRequestDraftBeforeMainProcessRestart() = runBlocking {
        val serverUrl = requireArgument(
            InstrumentationRegistry.getArguments().getString(ARG_SERVER_URL),
            ARG_SERVER_URL,
        )
        val serverId = requireArgument(
            InstrumentationRegistry.getArguments().getString(ARG_SERVER_ID),
            ARG_SERVER_ID,
        )
        val scopeHash = requestDraftScopeHash(serverUrl, serverId)
        val expectedDraft = MediaRequestDraft(
            title = REQUEST_DRAFT_TITLE,
            mediaType = "Filme",
            year = REQUEST_DRAFT_YEAR,
            notes = REQUEST_DRAFT_NOTES,
        )
        val store = MediaRequestDraftStoreImpl(context)
        store.save(scopeHash, expectedDraft)
        assertEquals(expectedDraft, store.load(scopeHash))
        Unit
    }

    @Test
    fun mediaRequestDraftIsRestoredAfterMainProcessRestart() = runBlocking {
        val expectedDraft = MediaRequestDraft(
            title = REQUEST_DRAFT_TITLE,
            mediaType = "Filme",
            year = REQUEST_DRAFT_YEAR,
            notes = REQUEST_DRAFT_NOTES,
        )
        val scenario = ActivityScenario.launch(MainActivity::class.java)
        try {
            composeRule.waitUntil(timeoutMillis = 30_000) {
                runCatching {
                    composeRule.onNodeWithContentDescription("Solicitar mídia").assertIsDisplayed()
                }.isSuccess
            }
            composeRule.onNodeWithContentDescription("Solicitar mídia").performClick()
            composeRule.waitUntil(timeoutMillis = 10_000) {
                runCatching {
                    composeRule.onNodeWithTag("media-request-title")
                        .assertTextContains(expectedDraft.title)
                    composeRule.onNodeWithTag("media-request-year")
                        .assertTextContains(expectedDraft.year)
                    composeRule.onNodeWithTag("media-request-notes")
                        .assertTextContains(expectedDraft.notes)
                }.isSuccess
            }
            composeRule.onNodeWithText("Filme").assertIsDisplayed()
            scenario.onActivity { activity ->
                InstrumentationRegistry.getInstrumentation().sendStatus(
                    0,
                    Bundle().apply { putInt(MEDIA_REQUEST_DRAFT_MAIN_PID_STATUS_KEY, android.os.Process.myPid()) },
                )
            }
        } finally {
            scenario.close()
        }
        Unit
    }

    @Test
    fun cleanupOnlyTheHostProcessRestartDraft() = runBlocking {
        val serverUrl = requireArgument(
            InstrumentationRegistry.getArguments().getString(ARG_SERVER_URL),
            ARG_SERVER_URL,
        )
        val serverId = requireArgument(
            InstrumentationRegistry.getArguments().getString(ARG_SERVER_ID),
            ARG_SERVER_ID,
        )
        val store = MediaRequestDraftStoreImpl(context)
        val scopeHash = requestDraftScopeHash(serverUrl, serverId)
        store.clear(scopeHash)
        assertEquals(null, store.load(scopeHash))
        Unit
    }

    @Test
    fun restoreOriginalSessionAfterColdStartFixture() = runBlocking {
        val snapshot = requireArgument(
            InstrumentationRegistry.getArguments().getString(ARG_SESSION_SNAPSHOT),
            ARG_SESSION_SNAPSHOT,
        )
        restoreSessionFromSnapshot(snapshot)
    }

    private suspend fun readSession() = SessionSnapshot(
        serverUrl = sessionRepository.getBaseUrl().first(),
        accessToken = sessionRepository.getAccessToken().first(),
        userId = sessionRepository.getCurrentUserId().first(),
        userName = sessionRepository.getCurrentUserName().first(),
        serverId = sessionRepository.getServerId().first(),
        deviceId = sessionRepository.getDeviceId().first(),
    )

    private suspend fun restoreSessionFromSnapshot(encodedSnapshot: String) {
        val jsonBytes = Base64.decode(encodedSnapshot, Base64.NO_WRAP)
        val snapshot = SessionSnapshot.fromJson(JSONObject(String(jsonBytes, Charsets.UTF_8)))
        sessionRepository.clearSession()
        sessionRepository.setBaseUrl(snapshot.serverUrl)
        if (!snapshot.accessToken.isNullOrBlank() && !snapshot.userId.isNullOrBlank()) {
            sessionRepository.saveSession(
                serverUrl = snapshot.serverUrl,
                token = snapshot.accessToken,
                userId = snapshot.userId,
                userName = snapshot.userName,
                serverId = snapshot.serverId,
                deviceId = snapshot.deviceId,
            )
        } else {
            sessionRepository.setServerId(snapshot.serverId)
        }
        assertEquals(snapshot.serverUrl, sessionRepository.getBaseUrl().first())
        assertEquals(snapshot.userId, sessionRepository.getCurrentUserId().first())
        assertEquals(snapshot.serverId, sessionRepository.getServerId().first())
    }

    private fun requestDraftScopeHash(serverUrl: String, serverId: String) =
        FeedbackRequestSession(
            serverUrl = serverUrl,
            accessToken = TEST_TOKEN,
            userId = TEST_USER_ID,
            deviceId = TEST_DEVICE_ID,
            serverId = serverId,
        ).mediaRequestDraftScopeHash()

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
                    (userName == null || userName.isNotBlank()))

        fun toJson() = JSONObject()
            .put("serverUrl", serverUrl)
            .put("accessToken", accessToken ?: JSONObject.NULL)
            .put("userId", userId ?: JSONObject.NULL)
            .put("userName", userName ?: JSONObject.NULL)
            .put("serverId", serverId ?: JSONObject.NULL)
            .put("deviceId", deviceId)

        companion object {
            fun fromJson(json: JSONObject) = SessionSnapshot(
                serverUrl = json.getString("serverUrl"),
                accessToken = json.optNullableString("accessToken"),
                userId = json.optNullableString("userId"),
                userName = json.optNullableString("userName"),
                serverId = json.optNullableString("serverId"),
                deviceId = json.getString("deviceId"),
            )
        }
    }

    private fun requireArgument(value: String?, name: String): String =
        requireNotNull(value?.takeIf(String::isNotBlank)) { "Missing instrumentation argument: $name" }

    private companion object {
        const val SESSION_SNAPSHOT_STATUS_KEY = "BOOK_COLD_START_SESSION_SNAPSHOT"
        const val MAIN_PROCESS_PID_STATUS_KEY = "BOOK_COLD_START_MAIN_PID"
        const val MEDIA_REQUEST_DRAFT_MAIN_PID_STATUS_KEY = "MEDIA_REQUEST_DRAFT_MAIN_PID"
        const val ARG_SESSION_SNAPSHOT = "bookColdStartSessionSnapshot"
        const val ARG_SERVER_URL = "bookColdStartServerUrl"
        const val ARG_ITEM_ID = "bookColdStartItemId"
        const val ARG_SERVER_ID = "bookColdStartServerId"
        const val TEST_USER_ID = "book-cold-start-user"
        const val TEST_TOKEN = "book-cold-start-token"
        const val TEST_BOOK_CONTENT = "Livro reaberto após cold start do processo principal."
        const val TEST_DEVICE_ID = "media-request-draft-test-device"
        const val REQUEST_DRAFT_TITLE = "Duna — processo reiniciado"
        const val REQUEST_DRAFT_YEAR = "2024"
        const val REQUEST_DRAFT_NOTES = "Rascunho persistido antes do encerramento"
    }
}

private fun JSONObject.optNullableString(name: String): String? =
    if (isNull(name)) null else getString(name)
