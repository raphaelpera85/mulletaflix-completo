package org.mulletaflix.android.service

import android.content.Context
import android.system.ErrnoException
import android.system.OsConstants
import androidx.media3.common.util.UnstableApi
import androidx.media3.exoplayer.offline.Download
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import java.io.IOException
import org.junit.After
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith

@UnstableApi
@RunWith(AndroidJUnit4::class)
class DownloadFailureClassificationTest {
    private val preferences = InstrumentationRegistry.getInstrumentation().targetContext
        .getSharedPreferences("download_failure_classification_test", Context.MODE_PRIVATE)

    @Before
    fun clearTestPreferences() {
        preferences.edit().clear().commit()
    }

    @After
    fun removeTestPreferences() {
        preferences.edit().clear().commit()
    }

    @Test
    fun recognizesNoSpaceByErrnoThroughWrappedCauseOnly() {
        val noSpace = ErrnoException("write", OsConstants.ENOSPC)
        val otherIoError = ErrnoException("write", OsConstants.EIO)

        assertTrue(isInsufficientStorageFailure(noSpace))
        assertTrue(isInsufficientStorageFailure(IOException("wrapper", IOException("inner", noSpace))))
        assertFalse(isInsufficientStorageFailure(otherIoError))
        assertFalse(isInsufficientStorageFailure(IOException("No space left on device")))
        assertFalse(isInsufficientStorageFailure(null))
    }

    @Test
    fun persistsClassificationAcrossSnapshotsAndClearsWhenDownloadRetries() {
        val requestId = "user:item"
        val failure = IOException("write failed", ErrnoException("write", OsConstants.ENOSPC))

        recordDownloadFailure(preferences, requestId, Download.STATE_FAILED, failure)
        assertTrue(isDownloadFailureDueToInsufficientStorage(preferences, requestId))

        // A restored failed record may have no transient exception; retain saved diagnosis.
        recordDownloadFailure(preferences, requestId, Download.STATE_FAILED, null)
        assertTrue(isDownloadFailureDueToInsufficientStorage(preferences, requestId))

        // Media3 moves it out of FAILED before running a retry. Do not carry stale advice forward.
        recordDownloadFailure(preferences, requestId, Download.STATE_QUEUED, null)
        assertFalse(isDownloadFailureDueToInsufficientStorage(preferences, requestId))

        recordDownloadFailure(
            preferences,
            requestId,
            Download.STATE_FAILED,
            IOException("write failed", ErrnoException("write", OsConstants.EIO)),
        )
        assertFalse(isDownloadFailureDueToInsufficientStorage(preferences, requestId))

        clearDownloadFailure(preferences, requestId)
        assertFalse(isDownloadFailureDueToInsufficientStorage(preferences, requestId))
    }
}
