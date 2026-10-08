package org.mulletaflix.feature.itemdetail

import android.graphics.Bitmap
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.async
import kotlinx.coroutines.launch
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import kotlinx.coroutines.test.runCurrent
import kotlinx.coroutines.test.runTest
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicInteger
import java.util.concurrent.atomic.AtomicReference
import org.json.JSONObject
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import org.readium.r2.shared.publication.Locator
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [35])
@OptIn(ExperimentalCoroutinesApi::class)
class BookPageDecodeGateTest {
    @Test
    fun serializesPageAndZoomDecodes() = runTest {
        val gate = BookPageDecodeGate()
        val firstStarted = CompletableDeferred<Unit>()
        val releaseFirst = CompletableDeferred<Unit>()
        val secondStarted = CompletableDeferred<Unit>()
        var activeDecodes = 0
        var maximumActiveDecodes = 0

        val first = launch {
            gate.run {
                activeDecodes++
                maximumActiveDecodes = maxOf(maximumActiveDecodes, activeDecodes)
                firstStarted.complete(Unit)
                try {
                    releaseFirst.await()
                } finally {
                    activeDecodes--
                }
            }
        }
        firstStarted.await()

        val second = async {
            gate.run {
                activeDecodes++
                maximumActiveDecodes = maxOf(maximumActiveDecodes, activeDecodes)
                secondStarted.complete(Unit)
                activeDecodes--
            }
        }
        runCurrent()

        assertFalse(secondStarted.isCompleted)
        releaseFirst.complete(Unit)
        second.await()
        first.join()

        assertEquals(1, maximumActiveDecodes)
    }

    @Test
    fun canceledQueuedDecodeDoesNotRunAfterActiveDecodeFinishes() = runTest {
        val gate = BookPageDecodeGate()
        val firstStarted = CompletableDeferred<Unit>()
        val releaseFirst = CompletableDeferred<Unit>()
        var staleDecodeStarted = false

        val first = launch {
            gate.run {
                firstStarted.complete(Unit)
                releaseFirst.await()
            }
        }
        firstStarted.await()

        val stale = launch {
            gate.run { staleDecodeStarted = true }
        }
        runCurrent()
        stale.cancel()
        releaseFirst.complete(Unit)

        first.join()
        stale.join()

        assertFalse(staleDecodeStarted)
        assertEquals("latest", gate.run { "latest" })
    }

    @Test
    fun canceledActiveDecodeRecyclesBitmapBeforeNextPageStarts() = runTest {
        val firstDecodeStarted = CountDownLatch(1)
        val releaseFirstDecode = CountDownLatch(1)
        val secondDecodeStarted = CountDownLatch(1)
        val firstBitmap = AtomicReference<Bitmap?>()
        val activeDecodes = AtomicInteger()
        val maximumActiveDecodes = AtomicInteger()
        val source = object : BookPageSource {
            override val pageCount = 2

            override fun locatorForPage(index: Int) =
                requireNotNull(Locator.fromJSON(JSONObject("""{"href":"page-$index"}""")))

            override fun pageIndexFromLocator(locator: Locator?) = null

            override fun decodePage(index: Int, maxWidth: Int, maxHeight: Int): Bitmap {
                val active = activeDecodes.incrementAndGet()
                maximumActiveDecodes.updateAndGet { maxOf(it, active) }
                try {
                    if (index == 0) {
                        firstDecodeStarted.countDown()
                        try {
                            check(releaseFirstDecode.await(5, TimeUnit.SECONDS))
                        } catch (_: InterruptedException) {
                            // Model a decoder that returns a native bitmap even as cancellation wins.
                        }
                    } else {
                        secondDecodeStarted.countDown()
                    }
                    return Bitmap.createBitmap(2, 2, Bitmap.Config.ARGB_8888).also {
                        if (index == 0) firstBitmap.set(it)
                    }
                } finally {
                    activeDecodes.decrementAndGet()
                }
            }
        }
        val gate = BookPageDecodeGate()
        val first = async { decodeBookPage(gate, source, 0, 32, 32) }
        assertTrue(withContext(Dispatchers.IO) { firstDecodeStarted.await(5, TimeUnit.SECONDS) })
        val next = async { decodeBookPage(gate, source, 1, 32, 32) }
        runCurrent()

        first.cancel()
        assertTrue(withContext(Dispatchers.IO) { secondDecodeStarted.await(5, TimeUnit.SECONDS) })
        val nextBitmap = next.await()
        runCatching { first.await() }

        assertNotNull(firstBitmap.get())
        assertTrue(requireNotNull(firstBitmap.get()).isRecycled)
        assertEquals(0L, secondDecodeStarted.count)
        assertEquals(1, maximumActiveDecodes.get())
        nextBitmap.recycle()
    }
}
