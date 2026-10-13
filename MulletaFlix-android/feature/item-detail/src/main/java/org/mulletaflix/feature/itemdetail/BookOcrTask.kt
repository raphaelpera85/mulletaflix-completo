package org.mulletaflix.feature.itemdetail

import com.google.android.gms.tasks.Task
import java.util.concurrent.Executor
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.suspendCancellableCoroutine

internal val BOOK_OCR_TASK_EXECUTOR = Executor { command -> command.run() }

internal suspend fun <T> Task<T>.awaitCancellable(): T = suspendCancellableCoroutine { continuation ->
    addOnCompleteListener(BOOK_OCR_TASK_EXECUTOR) { completed ->
        when {
            completed.isCanceled -> continuation.resumeWith(
                Result.failure(CancellationException("ML Kit text recognition was canceled.")),
            )
            completed.isSuccessful -> continuation.resumeWith(Result.success(completed.result))
            else -> continuation.resumeWith(
                Result.failure(completed.exception ?: IllegalStateException("ML Kit text recognition failed.")),
            )
        }
    }
}
