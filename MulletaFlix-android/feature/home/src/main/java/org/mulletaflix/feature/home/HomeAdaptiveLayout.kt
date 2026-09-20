package org.mulletaflix.feature.home

/** Device families used to tune the home experience without relying on a preview-only assumption. */
internal enum class HomeDeviceClass {
    PHONE,
    TABLET,
    TV,
}

internal data class HomeLayoutSpec(
    val contentMaxWidthDp: Int,
    val horizontalPaddingDp: Int,
    val heroHeightDp: Int,
    val cardScale: Float,
    val usesFocusFriendlySpacing: Boolean,
)

internal fun homeDeviceClass(widthDp: Int, isTelevision: Boolean): HomeDeviceClass = when {
    isTelevision -> HomeDeviceClass.TV
    widthDp >= 600 -> HomeDeviceClass.TABLET
    else -> HomeDeviceClass.PHONE
}

internal fun homeLayoutSpec(deviceClass: HomeDeviceClass): HomeLayoutSpec = when (deviceClass) {
    HomeDeviceClass.PHONE -> HomeLayoutSpec(
        contentMaxWidthDp = 600,
        horizontalPaddingDp = 0,
        heroHeightDp = 500,
        cardScale = 1f,
        usesFocusFriendlySpacing = false,
    )
    HomeDeviceClass.TABLET -> HomeLayoutSpec(
        contentMaxWidthDp = 1200,
        horizontalPaddingDp = 24,
        heroHeightDp = 560,
        cardScale = 1.15f,
        usesFocusFriendlySpacing = false,
    )
    HomeDeviceClass.TV -> HomeLayoutSpec(
        contentMaxWidthDp = 1600,
        horizontalPaddingDp = 56,
        heroHeightDp = 620,
        cardScale = 1.35f,
        usesFocusFriendlySpacing = true,
    )
}
