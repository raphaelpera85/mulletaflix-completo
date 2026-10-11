package org.mulletaflix.core.common.util

/** Returns a usable localized error message or the supplied user-facing fallback. */
fun Throwable.userFacingMessageOr(fallback: String): String =
    localizedMessage?.takeIf(String::isNotBlank) ?: fallback
