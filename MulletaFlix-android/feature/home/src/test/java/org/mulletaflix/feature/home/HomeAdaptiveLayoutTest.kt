package org.mulletaflix.feature.home

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class HomeAdaptiveLayoutTest {
    @Test
    fun `phone keeps compact content and standard card scale`() {
        assertEquals(HomeDeviceClass.PHONE, homeDeviceClass(411, isTelevision = false))
        val spec = homeLayoutSpec(HomeDeviceClass.PHONE)
        assertEquals(0, spec.horizontalPaddingDp)
        assertEquals(1f, spec.cardScale)
    }

    @Test
    fun `tablet gets centered content and larger artwork`() {
        assertEquals(HomeDeviceClass.TABLET, homeDeviceClass(600, isTelevision = false))
        val spec = homeLayoutSpec(HomeDeviceClass.TABLET)
        assertEquals(1200, spec.contentMaxWidthDp)
        assertTrue(spec.cardScale > 1f)
        assertTrue(spec.heroHeightDp > homeLayoutSpec(HomeDeviceClass.PHONE).heroHeightDp)
    }

    @Test
    fun `television prioritizes focus friendly spacing even when width is small`() {
        assertEquals(HomeDeviceClass.TV, homeDeviceClass(480, isTelevision = true))
        val spec = homeLayoutSpec(HomeDeviceClass.TV)
        assertTrue(spec.usesFocusFriendlySpacing)
        assertEquals(56, spec.horizontalPaddingDp)
        assertTrue(spec.cardScale > homeLayoutSpec(HomeDeviceClass.TABLET).cardScale)
    }
}
