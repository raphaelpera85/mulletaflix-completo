package org.mulletaflix.feature.itemdetail

import android.graphics.Bitmap
import com.google.android.gms.tasks.Task
import com.google.mlkit.vision.common.InputImage
import com.google.mlkit.vision.text.TextRecognition
import com.google.mlkit.vision.text.TextRecognizer
import com.google.mlkit.vision.text.latin.TextRecognizerOptions
import java.util.LinkedHashMap
import java.util.concurrent.atomic.AtomicBoolean
import kotlinx.coroutines.sync.Semaphore

internal interface ComicPageTextRecognizer : AutoCloseable {
    fun process(bitmap: Bitmap): Task<String>
}

internal fun interface ComicPageTextRecognizerFactory {
    fun create(): ComicPageTextRecognizer
}

/** On-demand offline OCR for comic pages. Recognition is serialized and the bitmap stays alive until ML Kit finishes. */
internal class ComicBookOcr(
    private val source: BookPageSource,
    private val recognizerFactory: ComicPageTextRecognizerFactory = ComicPageTextRecognizerFactory {
        MlKitComicPageTextRecognizer()
    },
) : AutoCloseable {
    private val cache = object : LinkedHashMap<Int, String>(CACHE_PAGE_COUNT, 0.75f, true) {
        override fun removeEldestEntry(eldest: MutableMap.MutableEntry<Int, String>?): Boolean =
            size > CACHE_PAGE_COUNT
    }
    private val lifecycleLock = Any()
    private val recognitionSemaphore = Semaphore(1)
    private var recognizer: ComicPageTextRecognizer? = null
    private var activeRecognitions = 0
    private var closeRequested = false

    suspend fun extractPageText(pageIndex: Int): String {
        require(pageIndex in 0 until source.pageCount)
        synchronized(cache) { cache[pageIndex] }?.let { return it }
        recognitionSemaphore.acquire()
        var client: ComicPageTextRecognizer? = null
        var bitmap: Bitmap? = null
        var resourcesTransferredToTask = false
        try {
            synchronized(cache) { cache[pageIndex] }?.let { return it }
            client = beginRecognition()
            val decodedBitmap = source.decodePage(pageIndex, MAX_BITMAP_SIDE, MAX_BITMAP_SIDE)
            bitmap = decodedBitmap
            val recognitionTask = requireNotNull(client).process(decodedBitmap)
            val cleanupStarted = AtomicBoolean(false)
            val cleanup = {
                if (cleanupStarted.compareAndSet(false, true)) {
                    try {
                        decodedBitmap.recycle()
                    } finally {
                        try {
                            recognitionSemaphore.release()
                        } finally {
                            endRecognition()
                        }
                    }
                }
            }
            resourcesTransferredToTask = true
            try {
                recognitionTask.addOnCompleteListener(BOOK_OCR_TASK_EXECUTOR) { cleanup() }
            } catch (failure: Throwable) {
                cleanup()
                throw failure
            }

            val text = recognitionTask.awaitCancellable()
            synchronized(lifecycleLock) {
                if (!closeRequested) synchronized(cache) { cache[pageIndex] = text }
            }
            return text
        } finally {
            if (!resourcesTransferredToTask) {
                try {
                    bitmap?.recycle()
                } finally {
                    try {
                        if (client != null) endRecognition()
                    } finally {
                        recognitionSemaphore.release()
                    }
                }
            }
        }
    }

    override fun close() {
        synchronized(lifecycleLock) {
            closeRequested = true
            synchronized(cache) { cache.clear() }
            if (activeRecognitions == 0) closeRecognizerLocked()
        }
    }

    private fun beginRecognition(): ComicPageTextRecognizer = synchronized(lifecycleLock) {
        check(!closeRequested) { "Comic OCR is closed." }
        val client = recognizer ?: recognizerFactory.create().also { recognizer = it }
        activeRecognitions++
        client
    }

    private fun endRecognition() = synchronized(lifecycleLock) {
        activeRecognitions--
        if (closeRequested && activeRecognitions == 0) closeRecognizerLocked()
    }

    private fun closeRecognizerLocked() {
        recognizer?.close()
        recognizer = null
    }

    private companion object {
        const val CACHE_PAGE_COUNT = 3
        const val MAX_BITMAP_SIDE = 2048
    }
}

private class MlKitComicPageTextRecognizer : ComicPageTextRecognizer {
    private val client: TextRecognizer = TextRecognition.getClient(TextRecognizerOptions.DEFAULT_OPTIONS)

    override fun process(bitmap: Bitmap): Task<String> =
        client.process(InputImage.fromBitmap(bitmap, 0)).continueWith(BOOK_OCR_TASK_EXECUTOR) { completed ->
            if (!completed.isSuccessful) {
                throw completed.exception ?: IllegalStateException("ML Kit text recognition failed.")
            }
            PdfBookDocument.normalizeSpeechSegments(
                requireNotNull(completed.result).textBlocks.asSequence().map { it.text },
            )
        }

    override fun close() = client.close()
}
