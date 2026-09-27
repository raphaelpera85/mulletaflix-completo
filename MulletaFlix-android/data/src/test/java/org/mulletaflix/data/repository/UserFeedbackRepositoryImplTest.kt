package org.mulletaflix.data.repository

import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.mockk
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertSame
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.core.api.MulletaFlixApiService
import org.mulletaflix.core.common.session.FeedbackRequestSession
import org.mulletaflix.core.api.dto.MediaRequestDto
import org.mulletaflix.core.api.dto.PlaybackIssueDto

class UserFeedbackRepositoryImplTest {

    private val api = mockk<MulletaFlixApiService>()
    private val session = FeedbackRequestSession("http://server.test:8096", "token-1", "user-1", "device-1")
    private val repository = UserFeedbackRepositoryImpl(api)

    @Test
    fun requestMedia_sendsAllFieldsAndReturnsSuccess() = runTest {
        val request = MediaRequestDto(
            title = "Duna",
            mediaType = "Filme",
            year = 2024,
            notes = "Áudio em português",
        )
        coEvery { api.requestMedia(request, session) } returns Unit

        val result = repository.requestMedia(session, request.title, request.mediaType, request.year, request.notes)

        assertTrue(result.isSuccess)
        coVerify(exactly = 1) { api.requestMedia(request, session) }
    }

    @Test
    fun requestMedia_convertsApiExceptionToFailure() = runTest {
        val failure = IllegalStateException("Servidor indisponível")
        coEvery { api.requestMedia(any(), any()) } throws failure

        val result = repository.requestMedia(session, "Duna", "Filme", null, null)

        assertSame(failure, result.exceptionOrNull())
    }

    @Test
    fun reportPlaybackIssue_sendsItemCategoryAndDescriptionAndReturnsSuccess() = runTest {
        val issue = PlaybackIssueDto(
            itemId = "item-42",
            category = "Sem áudio",
            description = "A faixa em português fica muda",
        )
        coEvery { api.reportPlaybackIssue(issue, session) } returns Unit

        val result = repository.reportPlaybackIssue(session, issue.itemId, issue.category, issue.description)

        assertTrue(result.isSuccess)
        coVerify(exactly = 1) { api.reportPlaybackIssue(issue, session) }
    }

    @Test
    fun reportPlaybackIssue_convertsApiExceptionToFailure() = runTest {
        val failure = IllegalStateException("Servidor indisponível")
        coEvery { api.reportPlaybackIssue(any(), any()) } throws failure

        val result = repository.reportPlaybackIssue(session, "item-42", "Não reproduz", null)

        assertSame(failure, result.exceptionOrNull())
    }

    @Test
    fun requestMedia_rethrowsCoroutineCancellation() = runTest {
        val cancellation = CancellationException("Solicitação cancelada")
        coEvery { api.requestMedia(any(), any()) } throws cancellation

        val observedCancellation = try {
            repository.requestMedia(session, "Duna", "Filme", null, null)
            null
        } catch (error: CancellationException) {
            error
        }

        assertSame(cancellation, observedCancellation)
        coVerify(exactly = 1) { api.requestMedia(any(), session) }
    }

    @Test
    fun reportPlaybackIssue_rethrowsCoroutineCancellation() = runTest {
        val cancellation = CancellationException("Relato cancelado")
        coEvery { api.reportPlaybackIssue(any(), any()) } throws cancellation

        val observedCancellation = try {
            repository.reportPlaybackIssue(session, "item-42", "Não reproduz", null)
            null
        } catch (error: CancellationException) {
            error
        }

        assertSame(cancellation, observedCancellation)
        coVerify(exactly = 1) { api.reportPlaybackIssue(any(), session) }
    }

}
