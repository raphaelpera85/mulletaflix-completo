package org.mulletaflix.android

import android.app.Application
import android.content.pm.ActivityInfo
import android.content.res.Configuration
import androidx.activity.compose.setContent
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.assertIsEnabled
import androidx.compose.ui.test.hasSetTextAction
import androidx.compose.ui.test.junit4.v2.createAndroidComposeRule
import androidx.compose.ui.test.onAllNodesWithTag
import androidx.compose.ui.test.onAllNodesWithText
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performTextInput
import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import dagger.hilt.EntryPoint
import dagger.hilt.InstallIn
import dagger.hilt.android.EntryPointAccessors
import dagger.hilt.components.SingletonComponent
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.core.api.MulletaFlixApiService
import org.mulletaflix.designsystem.theme.MulletaFlixTheme
import org.mulletaflix.feature.itemdetail.BookReaderScreen

@EntryPoint
@InstallIn(SingletonComponent::class)
internal interface RemoteBookReaderQaEntryPoint {
    fun apiService(): MulletaFlixApiService
}

@RunWith(AndroidJUnit4::class)
class RemoteBookReaderE2eTest {
    @get:Rule
    val composeRule = createAndroidComposeRule<MainActivity>()

    @Test
    fun authenticatedCatalogBookOpensPagesAndSurvivesRotation() = runBlocking {
        val session = composeRule.activity.sessionRepository
        var userId = session.getCurrentUserId().first()
        var token = session.getAccessToken().first()
        var serverUrl = session.getBaseUrl().first()

        if (userId.isNullOrBlank() || token.isNullOrBlank() || serverUrl.isBlank()) {
            composeRule.waitUntil(timeoutMillis = 30_000) {
                composeRule.onAllNodesWithTag("auth.login.username", useUnmergedTree = true)
                    .fetchSemanticsNodes().isNotEmpty()
            }

            val qaUsername = "qa_reader_e2e_${System.currentTimeMillis()}"
            val qaPassword = "QaReader381!"
            println("QA registration user: $qaUsername")

            composeRule.onNodeWithText("Cadastrar").performClick()
            composeRule.onNodeWithText("Criar Conta").assertIsDisplayed()

            val editableFields = composeRule.onAllNodes(hasSetTextAction())
            val editableFieldCount = editableFields.fetchSemanticsNodes().size
            check(editableFieldCount >= 3) {
                "O diálogo de cadastro não expôs os três campos editáveis"
            }
            editableFields[editableFieldCount - 3].performTextInput(qaUsername)
            editableFields[editableFieldCount - 2].performTextInput(qaPassword)
            editableFields[editableFieldCount - 1].performTextInput(qaPassword)

            val registerActions = composeRule.onAllNodesWithText("Cadastrar")
            val registerActionCount = registerActions.fetchSemanticsNodes().size
            check(registerActionCount > 0) { "A ação Cadastrar não foi encontrada" }
            registerActions[registerActionCount - 1].performClick()

            composeRule.waitUntil(timeoutMillis = 30_000) {
                composeRule.onAllNodesWithTag("auth.login.username", useUnmergedTree = true)
                    .fetchSemanticsNodes().isEmpty()
            }
            userId = session.getCurrentUserId().first()
            token = session.getAccessToken().first()
            serverUrl = session.getBaseUrl().first()
        }
        check(!userId.isNullOrBlank() && !token.isNullOrBlank() && serverUrl.isNotBlank()) {
            "O cadastro QA não resultou em uma sessão autenticada"
        }

        val application = ApplicationProvider.getApplicationContext<Application>()
        val api = EntryPointAccessors.fromApplication(
            application,
            RemoteBookReaderQaEntryPoint::class.java,
        ).apiService()
        val books = api.getItems(
            userId = userId,
            includeItemTypes = "Book",
            fields = "MediaSources",
            limit = 50,
        ).items
        check(books.isNotEmpty()) { "A conta QA não enxerga nenhum livro no servidor configurado" }

        val book = books.firstOrNull { item ->
            item.mediaSources.orEmpty().any { it.container.equals("epub", ignoreCase = true) }
        } ?: books.first()
        val container = book.mediaSources.orEmpty().firstOrNull()?.container.orEmpty()
        println("QA remote book: ${book.name.orEmpty()} id=${book.id} container=$container total=${books.size}")

        var backInvoked = false
        composeRule.activityRule.scenario.onActivity { activity ->
            activity.setContent {
                MulletaFlixTheme {
                    BookReaderScreen(
                        itemId = book.id,
                        onBack = { backInvoked = true },
                    )
                }
            }
        }

        composeRule.waitUntil(timeoutMillis = 60_000) {
            runCatching {
                composeRule.onNodeWithTag("book-reader-screen").assertIsDisplayed()
                true
            }.getOrDefault(false)
        }
        composeRule.waitUntil(timeoutMillis = 20_000) {
            runCatching {
                composeRule.onNodeWithText("Próximo").assertIsEnabled()
                true
            }.getOrDefault(false)
        }
        composeRule.onNodeWithText("Próximo").performClick()
        composeRule.waitUntil(timeoutMillis = 20_000) {
            runCatching {
                composeRule.onNodeWithText("Anterior").assertIsEnabled()
                true
            }.getOrDefault(false)
        }

        composeRule.activityRule.scenario.onActivity {
            it.requestedOrientation = ActivityInfo.SCREEN_ORIENTATION_LANDSCAPE
        }
        composeRule.waitUntil(timeoutMillis = 15_000) {
            composeRule.activity.resources.configuration.orientation == Configuration.ORIENTATION_LANDSCAPE
        }
        composeRule.onNodeWithTag("book-reader-screen").assertIsDisplayed()
        composeRule.onNodeWithText("Anterior").assertIsEnabled()

        composeRule.onNodeWithContentDescription("Voltar").performClick()
        composeRule.runOnIdle { check(backInvoked) { "A ação Voltar do leitor não foi entregue" } }
    }
}
