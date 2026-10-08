package org.mulletaflix.feature.itemdetail

import android.graphics.Bitmap
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.ensureActive
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import org.readium.r2.shared.publication.Locator

/** Page-oriented book source shared by PDF and comic readers. */
internal interface BookPageSource : AutoCloseable {
    val pageCount: Int

    override fun close() = Unit

    fun locatorForPage(index: Int): Locator

    fun pageIndexFromLocator(locator: Locator?): Int?

    fun decodePage(index: Int, maxWidth: Int, maxHeight: Int): Bitmap
}

/** Archive image streams use this check to stop synchronous decodes after runInterruptible cancels them. */
internal fun ensurePageDecodeNotInterrupted() {
    if (Thread.currentThread().isInterrupted) {
        throw CancellationException("Book page decoding was canceled.")
    }
}

/** Serializes page and zoom decodes; canceled requests waiting here never start. */
internal class BookPageDecodeGate {
    private val mutex = Mutex()

    suspend fun <T> run(block: suspend () -> T): T = mutex.withLock {
        currentCoroutineContext().ensureActive()
        block()
    }
}
