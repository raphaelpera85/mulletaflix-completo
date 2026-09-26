package org.mulletaflix.feature.itemdetail

import org.junit.Assert.assertEquals
import org.junit.Test

class DetailRefreshPolicyTest {
    @Test
    fun `refresh action describes idle and loading states`() {
        assertEquals("Atualizar detalhes", detailRefreshContentDescription(false))
        assertEquals("Atualizando detalhes", detailRefreshContentDescription(true))
    }
}
