package org.mulletaflix.feature.player

/** User-facing labels for the Cast action, kept consistent across TalkBack and the OSD. */
internal fun castActionLabel(isCasting: Boolean): String =
    if (isCasting) "Transmitindo" else "Transmitir"

internal fun castActionContentDescription(isCasting: Boolean): String =
    if (isCasting) "Transmitindo para dispositivo compatível" else "Transmitir para dispositivo compatível"
