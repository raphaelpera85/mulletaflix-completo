package org.mulletaflix.feature.downloads

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.domain.repository.DownloadEntry
import org.mulletaflix.domain.repository.DownloadState

class DownloadSelectionPolicyTest {
    private val completed = DownloadEntry("done", "Concluído", "https://server/done", DownloadState.Completed, 100)
    private val queued = DownloadEntry("queued", "Na fila", "https://server/queued", DownloadState.Queued, 0)

    @Test
    fun `only completed downloads can be selected`() {
        val completedIds = completedDownloadIds(listOf(completed, queued))

        assertEquals(setOf("done"), completedIds)
        assertEquals(setOf("done"), toggleCompletedDownloadSelection(emptySet(), "done", completedIds))
        assertTrue(toggleCompletedDownloadSelection(emptySet(), "queued", completedIds).isEmpty())
        assertTrue(toggleCompletedDownloadSelection(emptySet(), "missing", completedIds).isEmpty())
    }

    @Test
    fun `toggling selected completed download removes only its id`() {
        val selected = toggleCompletedDownloadSelection(setOf("done", "other"), "done", setOf("done", "other"))

        assertEquals(setOf("other"), selected)
    }

    @Test
    fun `selection reconciliation drops removed or non-completed ids`() {
        assertEquals(
            setOf("done"),
            reconcileCompletedDownloadSelection(setOf("done", "queued", "removed"), setOf("done")),
        )
    }

    @Test
    fun `selected removal preserves visible order and excludes active entries`() {
        val entries = listOf(queued, completed)

        assertEquals(listOf(completed), selectedCompletedDownloads(entries, setOf("queued", "done")))
        assertFalse(selectedCompletedDownloads(entries, setOf("queued")).any { it.state != DownloadState.Completed })
    }

    @Test
    fun `same public media id on two servers remains independently selectable`() {
        val serverA = completed.copy(downloadId = "queue-server-a", serverId = "server-a")
        val serverB = completed.copy(downloadId = "queue-server-b", serverId = "server-b")
        val entries = listOf(serverA, serverB)

        assertEquals(setOf("queue-server-a", "queue-server-b"), completedDownloadIds(entries))
        assertEquals(
            listOf(serverB),
            selectedCompletedDownloads(entries, setOf("queue-server-b")),
        )
    }
}
