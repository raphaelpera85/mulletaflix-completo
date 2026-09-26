package org.mulletaflix.android.service

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class DownloadIdentityTest {
    @Test
    fun `scopes Media3 request ids by account`() {
        assertEquals("user-1::movie-1", scopedDownloadRequestId("user-1", "movie-1"))
        assertEquals("user-2::movie-1", scopedDownloadRequestId("user-2", "movie-1"))
    }

    @Test
    fun `only the owning account can see a download`() {
        assertTrue(downloadBelongsToUser("user-1", "user-1"))
        assertFalse(downloadBelongsToUser("user-1", "user-2"))
        assertFalse(downloadBelongsToUser(null, "user-1"))
    }

    @Test
    fun `recovers public item id from scoped request id`() {
        assertEquals("movie-1", publicDownloadItemId("user-1::movie-1", "user-1"))
        assertEquals("legacy-id", publicDownloadItemId("legacy-id", "user-1"))
    }

    @Test
    fun `a media id survives a round trip through the account scope`() {
        val itemId = "episode-202"
        val scoped = scopedDownloadRequestId("user-1", itemId)
        assertEquals(itemId, publicDownloadItemId(scoped, "user-1"))
    }

    @Test
    fun `the scope contract trims the account id on both sides`() {
        // `scopedDownloadRequestId` trims. If `publicDownloadItemId` did not, an
        // account id carrying whitespace would leave the prefix in place and
        // return the whole scoped id as if it were a media id.
        val scoped = scopedDownloadRequestId(" user-1 ", "movie-1")
        assertEquals("user-1::movie-1", scoped)
        assertEquals("movie-1", publicDownloadItemId(scoped, " user-1 "))
        assertEquals("movie-1", publicDownloadItemId(scoped, "user-1"))
    }

    @Test
    fun `another account never resolves someone else's scoped id`() {
        // Must not strip a prefix belonging to a different account: the caller
        // then sees the raw scoped id instead of a wrong media id.
        assertEquals(
            "user-2::movie-1",
            publicDownloadItemId("user-2::movie-1", "user-1"),
        )
    }

    @Test
    fun `an unscoped entry with no owner is recognised as legacy`() {
        // Releases up to 1.0.6 stored the raw media id and wrote no owner. Such
        // an entry is otherwise filtered out forever: invisible, unremovable and
        // unreclaimable, so it has to be adopted.
        assertTrue(isLegacyUnscopedDownload("movie-1", ownerUserId = null))
        assertTrue(isLegacyUnscopedDownload("movie-1", ownerUserId = ""))
        assertTrue(isLegacyUnscopedDownload("episode 202", ownerUserId = "  "))
    }

    @Test
    fun `a scoped entry is never treated as legacy`() {
        assertFalse(isLegacyUnscopedDownload("user-1::movie-1", ownerUserId = null))
        assertFalse(isLegacyUnscopedDownload("user-2::movie-1", ownerUserId = "user-1"))
    }

    @Test
    fun `an entry that already has an owner is never treated as legacy`() {
        assertFalse(isLegacyUnscopedDownload("movie-1", ownerUserId = "user-1"))
    }

    @Test
    fun `the adopted id is the media id, not the scoped one`() {
        // Adoption declares the legacy request id to be the media id, which is
        // exactly what the unscoped format meant.
        val legacyRequestId = "movie-1"
        assertTrue(isLegacyUnscopedDownload(legacyRequestId, null))
        assertEquals(legacyRequestId, publicDownloadItemId(legacyRequestId, "user-1"))
    }

    @Test
    fun `a legacy entry is never adopted before the session is known`() {
        // `observeDownloads` emits its first snapshot before the account has
        // been read. Adopting there would persist an EMPTY owner, and since the
        // only legacy test is "owner is blank", every later snapshot would see
        // it as legacy again and keep rewriting nothing — the entry would stay
        // unreachable for every account instead of being recovered.
        assertFalse(shouldAdoptLegacyDownload("movie-1", ownerUserId = null, currentUserId = null))
        assertFalse(shouldAdoptLegacyDownload("movie-1", ownerUserId = null, currentUserId = ""))
        assertFalse(shouldAdoptLegacyDownload("movie-1", ownerUserId = null, currentUserId = "   "))
    }

    @Test
    fun `a legacy entry is adopted once a real account is known`() {
        assertTrue(shouldAdoptLegacyDownload("movie-1", ownerUserId = null, currentUserId = "user-1"))
    }

    @Test
    fun `an entry that already has an owner is never adopted`() {
        assertFalse(
            shouldAdoptLegacyDownload("movie-1", ownerUserId = "user-1", currentUserId = "user-2"),
        )
    }

    @Test
    fun `a scoped entry is never adopted`() {
        assertFalse(
            shouldAdoptLegacyDownload("user-1::movie-1", ownerUserId = null, currentUserId = "user-1"),
        )
    }

    @Test
    fun `an existing entry resolves to whichever id shape it is stored under`() {
        val index = setOf("user-1::movie-1")
        assertEquals(
            "user-1::movie-1",
            existingDownloadRequestId("user-1", "movie-1", index::contains),
        )
    }

    @Test
    fun `a legacy entry resolves to its raw id, the same one remove uses`() {
        // O defeito: "Tentar novamente" montava o id *scoped* para um download
        // criado até a 1.0.6, cujo id é o media id cru. O slot scoped não existia,
        // então nasciam DOIS slots para a mesma mídia — o que falhou continuava
        // listado e o arquivo era baixado de novo. "Remover" já resolvia o formato
        // certo e o retry não; agora os dois passam por esta função.
        val legacyIndex = setOf("movie-1")
        assertEquals(
            "movie-1",
            existingDownloadRequestId("user-1", "movie-1", legacyIndex::contains),
        )
    }

    @Test
    fun `an entry that is no longer in the index resolves to nothing`() {
        // Não cair para o id cru: um id cru que não está no índice seria
        // adicionado como download novo e sem dono, exatamente o formato que
        // `shouldAdoptLegacyDownload` entrega para quem entrar primeiro.
        assertNull(existingDownloadRequestId("user-1", "movie-1", { false }))
    }

    @Test
    fun `both id shapes present picks the scoped one`() {
        // A ordem importa: o slot scoped é o atual e é o único que
        // `belongsToCurrentUser` reconhece. Escolher o cru aqui faria a operação
        // agir sobre o slot antigo e deixar o novo intacto.
        val both = setOf("user-1::movie-1", "movie-1")
        assertEquals(
            "user-1::movie-1",
            existingDownloadRequestId("user-1", "movie-1", both::contains),
        )
    }

    @Test
    fun `the resolved id is always one the index actually holds`() {
        // O invariante que os dois call sites compartilham: a operação precisa
        // mirar um slot que existe. Um id que não está no índice é ou um no-op
        // silencioso (remover) ou um download novo e sem dono (tentar novamente).
        val shapes = listOf(
            setOf("user-1::movie-1"),
            setOf("movie-1"),
            setOf("user-1::movie-1", "movie-1"),
        )

        shapes.forEach { index ->
            val resolved = existingDownloadRequestId("user-1", "movie-1", index::contains)
            assertTrue("o id resolvido precisa existir no índice $index", resolved in index)
        }
    }
}
