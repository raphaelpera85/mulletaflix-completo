package org.mulletaflix.android.navigation

import java.nio.charset.StandardCharsets

/**
 * Percent-encodes a value for a navigation query argument.
 *
 * Deliberately not `java.net.URLEncoder`: that applies
 * `application/x-www-form-urlencoded`, where a space becomes `+`. Navigation
 * matches query arguments with `android.net.Uri.getQueryParameters`, which
 * follows RFC 3986 and does **not** convert `+` back into a space, so an offline
 * title reached the player OSD verbatim as `O+Retorno+de+Jedi`.
 *
 * The implementation is kept free of `android.net.Uri` so the encoding can be
 * asserted in JVM unit tests. There, `Uri.encode` is stubbed and returns null
 * because the app sets `unitTests.isReturnDefaultValues = true`, which is what
 * previously hid this defect.
 *
 * Everything outside the RFC 3986 unreserved set is escaped, so the result is
 * safe in both a path segment and a query value.
 */
internal fun encodeRouteQueryArgument(value: String): String {
    val bytes = value.toByteArray(StandardCharsets.UTF_8)
    return buildString(bytes.size) {
        bytes.forEach { byte ->
            val unsigned = byte.toInt() and 0xFF
            val char = unsigned.toChar()
            if (char.isRouteArgumentSafe()) {
                append(char)
            } else {
                append('%')
                append(HEX_DIGITS[unsigned shr 4])
                append(HEX_DIGITS[unsigned and 0x0F])
            }
        }
    }
}

/** RFC 3986 unreserved characters. */
private fun Char.isRouteArgumentSafe(): Boolean =
    this in 'A'..'Z' || this in 'a'..'z' || this in '0'..'9' ||
        this == '-' || this == '_' || this == '.' || this == '~'

private const val HEX_DIGITS = "0123456789ABCDEF"
