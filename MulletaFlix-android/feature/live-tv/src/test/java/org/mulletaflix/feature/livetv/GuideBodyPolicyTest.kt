package org.mulletaflix.feature.livetv

import org.junit.Assert.assertEquals
import org.junit.Test

/**
 * The EPG dialog body must never blank content the user is already reading.
 *
 * Both halves were wrong before: `guideError` was rendered *instead of* the
 * programme list, so a single failed refresh discarded a guide already on screen,
 * and an in-flight reload with no programmes yet fell through to "Nenhum programa
 * encontrado para as próximas 24 horas." with no request behind it.
 */
class GuideBodyPolicyTest {

    @Test
    fun `programmes on screen survive a reload`() {
        assertEquals(GuideBody.PROGRAMMES, guideBody(isLoadingGuide = true, programmeCount = 12))
    }

    @Test
    fun `programmes on screen survive an error`() {
        // The error is drawn as a banner above the list, never as a replacement.
        assertEquals(GuideBody.PROGRAMMES, guideBody(isLoadingGuide = false, programmeCount = 1))
    }

    @Test
    fun `an empty guide shows progress only while something is running`() {
        assertEquals(GuideBody.LOADING, guideBody(isLoadingGuide = true, programmeCount = 0))
        assertEquals(GuideBody.EMPTY, guideBody(isLoadingGuide = false, programmeCount = 0))
    }
}
