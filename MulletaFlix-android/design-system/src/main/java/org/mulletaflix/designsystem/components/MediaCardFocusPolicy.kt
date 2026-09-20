package org.mulletaflix.designsystem.components

/** Returns the small focus lift used by remote-friendly media cards. */
internal fun mediaCardFocusScale(isFocusFriendly: Boolean, isFocused: Boolean): Float =
    if (isFocusFriendly && isFocused) 1.04f else 1f

/** Keeps the TV focus ring visible without changing phone/tablet card geometry. */
internal fun mediaCardFocusBorderWidthDp(isFocusFriendly: Boolean, isFocused: Boolean): Float =
    if (isFocusFriendly && isFocused) 2f else 0f
