package org.mulletaflix.feature.itemdetail

import java.util.concurrent.CancellationException
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertSame
import org.junit.Assert.assertTrue
import org.junit.Test

class BookReaderRequestGenerationTest {
    @Test
    fun newerLoadOrProgressUpdateInvalidatesOlderOperation() {
        val generation = BookReaderRequestGeneration()

        val first = generation.begin()
        val second = generation.begin()

        assertTrue(generation.isCurrent(second))
        assertEquals(false, generation.isCurrent(first))
    }

    @Test
    fun recoverableStorageErrorsReturnNull() = runTest {
        assertNull(recoverBookReaderStorageFailure<String> { error("disk unavailable") })
    }

    @Test
    fun storageCancellationIsPropagated() = runTest {
        val cancellation = CancellationException("reader closed")
        val caught = runCatching {
            recoverBookReaderStorageFailure<String> { throw cancellation }
        }.exceptionOrNull()

        assertSame(cancellation, caught)
    }
}
