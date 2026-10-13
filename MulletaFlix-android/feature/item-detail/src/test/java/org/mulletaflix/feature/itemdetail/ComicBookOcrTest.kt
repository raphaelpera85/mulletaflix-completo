package org.mulletaflix.feature.itemdetail

import android.graphics.Bitmap
import com.google.android.gms.tasks.Task
import com.google.android.gms.tasks.TaskCompletionSource
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicReference
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.async
import kotlinx.coroutines.delay
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.withTimeout
import kotlinx.coroutines.withTimeoutOrNull
import org.json.JSONObject
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import org.readium.r2.shared.publication.Locator
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [35])
class ComicBookOcrTest {
    @Test
    fun cancelingRecognitionReturnsPromptlyAndKeepsBitmapAliveUntilTaskCompletes() = runBlocking {
        val sourceBitmap = AtomicReference<Bitmap?>()
        val source = object : BookPageSource {
            override val pageCount = 1

            override fun locatorForPage(index: Int) =
                requireNotNull(Locator.fromJSON(JSONObject("""{"href":"page-$index"}""")))

            override fun pageIndexFromLocator(locator: Locator?) = null

            override fun decodePage(index: Int, maxWidth: Int, maxHeight: Int): Bitmap =
                Bitmap.createBitmap(8, 8, Bitmap.Config.ARGB_8888).also(sourceBitmap::set)
        }
        val recognizer = ControlledComicPageTextRecognizer()
        val ocr = ComicBookOcr(source, ComicPageTextRecognizerFactory { recognizer })
        val search = async(Dispatchers.IO) { ocr.extractPageText(0) }

        try {
            assertTrue(recognizer.started.await(5, TimeUnit.SECONDS))
            search.cancel()
            val canceledPromptly = withTimeoutOrNull(CANCEL_WAIT_MS) {
                search.join()
                true
            }
            val bitmapStayedAlive = !requireNotNull(sourceBitmap.get()).isRecycled
            ocr.close()
            val recognizerStayedOpen = !recognizer.closed

            recognizer.completion.setResult("recognized text")
            withTimeout(CLEANUP_TIMEOUT_MS) {
                while (!requireNotNull(sourceBitmap.get()).isRecycled || !recognizer.closed) delay(10)
            }

            assertNotNull("canceling search must not wait for ML Kit", canceledPromptly)
            assertTrue("ML Kit still owns the bitmap until its task completes", bitmapStayedAlive)
            assertTrue("the recognizer must remain open while ML Kit is using it", recognizerStayedOpen)
            assertTrue(requireNotNull(sourceBitmap.get()).isRecycled)
            assertTrue(recognizer.closed)
        } finally {
            recognizer.completion.trySetResult("cleanup")
            search.cancel()
            withTimeoutOrNull(CLEANUP_TIMEOUT_MS) { search.join() }
            ocr.close()
            sourceBitmap.get()?.takeUnless(Bitmap::isRecycled)?.recycle()
        }
    }

    private class ControlledComicPageTextRecognizer : ComicPageTextRecognizer {
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
