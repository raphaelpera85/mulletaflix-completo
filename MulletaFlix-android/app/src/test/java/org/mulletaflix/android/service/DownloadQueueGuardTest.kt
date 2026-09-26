package org.mulletaflix.android.service

import java.io.File
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Guard for the download queue being driven by its foreground service.
 *
 * `DownloadService` was declared in the manifest and implemented its
 * notification, but nothing ever started it: the repository talked to the
 * `DownloadManager` directly. Media3 only drives the queue while the service
 * runs, so no ongoing notification was ever posted and a queued or partially
 * downloaded item stopped as soon as the app process died.
 *
 * The invariant: every queue mutation in the repository goes through
 * `DownloadService.send*`, which starts the service. A direct
 * `manager.addDownload`/`manager.removeDownload` bypasses it and silently
 * reintroduces the defect. A source scan is used because no runtime assertion can
 * prove the absence of a call site.
 */
class DownloadQueueGuardTest {

    private val repository = File(
        "src/main/java/org/mulletaflix/android/service/Media3DownloadRepository.kt",
    )

    @Test
    fun `the repository never mutates the queue without the download service`() {
        assertTrue(
            "Media3DownloadRepository.kt not found at ${repository.absolutePath}; " +
                "working dir is ${File(".").absolutePath}",
            repository.isFile,
        )

        val source = repository.readText()
        val directMutations = listOf("manager.addDownload(", "manager.removeDownload(")
            .filter { source.contains(it) }

        assertTrue(
            "the queue must be mutated through DownloadService so the foreground " +
                "service runs and keeps a download alive off-screen; found: $directMutations",
            directMutations.isEmpty(),
        )
    }

    @Test
    fun `every service entry point the queue needs is used`() {
        val source = repository.readText()

        listOf(
            "sendAddDownload",
            "sendRemoveDownload",
            "sendResumeDownloads",
        ).forEach { entryPoint ->
            assertTrue(
                "the repository must call $entryPoint so the download service is started",
                source.contains(entryPoint),
            )
        }

        assertTrue(
            "the service class must be the app's DownloadService",
            source.contains("DownloadService::class.java"),
        )
    }
}
