package org.mulletaflix.feature.itemdetail

import android.graphics.Bitmap
import com.google.android.gms.tasks.Task
import com.google.android.gms.tasks.TaskCompletionSource
import java.io.File
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicReference
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.async
import kotlinx.coroutines.delay
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.withTimeout
import kotlinx.coroutines.withTimeoutOrNull
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [34])
class PdfBookDocumentOcrTest {
    @Test
    fun cancelingRecognitionReturnsPromptlyAndKeepsBitmapAliveUntilTaskCompletes() = runBlocking {
        val bitmap = AtomicReference<Bitmap?>()
        val recognizer = ControlledPdfPageTextRecognizer()
        val document = PdfBookDocument(
            file = File("unused-test-pdf"),
            pageCount = 1,
            recognizerFactory = PdfPageTextRecognizerFactory { recognizer },
            pageBitmapDecoder = { _, _, _ ->
                Bitmap.createBitmap(8, 8, Bitmap.Config.ARGB_8888).also(bitmap::set)
            },
        )
        val extraction = async(Dispatchers.IO) { document.extractPageText(0) }

        try {
            assertTrue(recognizer.started.await(5, TimeUnit.SECONDS))
            extraction.cancel()
            val returnedPromptly = withTimeoutOrNull(CANCEL_WAIT_MS) {
                extraction.join()
                true
            }
            val bitmapStayedAlive = !requireNotNull(bitmap.get()).isRecycled
            document.close()
            val recognizerStayedOpen = !recognizer.closed

            recognizer.completion.setResult("recognized text")
            withTimeout(CLEANUP_TIMEOUT_MS) {
                while (!requireNotNull(bitmap.get()).isRecycled || !recognizer.closed) delay(10)
            }

            assertNotNull("canceling PDF OCR must not wait for ML Kit", returnedPromptly)
            assertTrue("ML Kit still owns the bitmap until its task completes", bitmapStayedAlive)
            assertTrue("the recognizer must remain open while ML Kit is using it", recognizerStayedOpen)
            assertTrue(requireNotNull(bitmap.get()).isRecycled)
            assertTrue(recognizer.closed)
        } finally {
            recognizer.completion.trySetResult("cleanup")
            extraction.cancel()
            withTimeoutOrNull(CLEANUP_TIMEOUT_MS) { extraction.join() }
            document.close()
            bitmap.get()?.takeUnless(Bitmap::isRecycled)?.recycle()
        }
    }

    private class ControlledPdfPageTextRecognizer : PdfPageTextRecognizer {
        val completion = TaskCompletionSource<String>()
        val started = CountDownLatch(1)

        @Volatile
        var closed = false
            private set

        override fun process(bitmap: Bitmap): Task<String> = completion.task.also {
            started.countDown()
        }

        override fun close() {
            closed = true
        }
    }

    private companion object {
        const val CANCEL_WAIT_MS = 500L
        const val CLEANUP_TIMEOUT_MS = 5_000L
    }
}
