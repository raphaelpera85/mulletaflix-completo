package org.mulletaflix.feature.livetv

import java.text.ParseException
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale
import java.util.TimeZone

/**
 * When a recording starts, in the viewer's own time zone.
 *
 * The server sends ISO-8601 UTC (`2026-09-14T23:00:00.0000000Z`). The previous label
 * removed the `Z` and replaced the `T` with a space, which reads as a local time but is
 * UTC: a recording made at 20:00 in Brazil appeared as "23:00".
 *
 * Null means "the server did not send a date we can read", and the caller draws no line
 * at all rather than a wrong one. A date without a zone marker is treated as UTC, which
 * is what the server means.
 *
 * `java.time` is not used on purpose: `minSdk` is 24 and core library desugaring is not
 * enabled, so `DateTimeFormatter` would crash below API 26.
 */
internal fun recordingStartLabel(
    startDateUtc: String?,
    timeZone: TimeZone = TimeZone.getDefault(),
    locale: Locale = Locale.getDefault(),
): String? {
    val raw = startDateUtc?.takeIf { it.isNotBlank() } ?: return null
    val parsed = parseServerDate(raw) ?: return null
    val formatter = SimpleDateFormat("yyyy-MM-dd HH:mm", locale).apply {
        this.timeZone = timeZone
    }
    return formatter.format(parsed)
}

/**
 * Parses the shapes the server actually emits.
 *
 * Jellyfin writes variable fractional digits — seven in most payloads
 * (`...:00.0000000Z`), three in others — and no fractional part at all in some. One
 * `SimpleDateFormat` pattern with `SSS` reads the first three digits and ignores the
 * rest, so a single tolerant pattern covers all of them; the fallbacks exist for a date
 * without a zone marker.
 */
private fun parseServerDate(raw: String): Date? {
    val patterns = listOf(
        "yyyy-MM-dd'T'HH:mm:ss.SSSSSSS'Z'",
        "yyyy-MM-dd'T'HH:mm:ss.SSS'Z'",
        "yyyy-MM-dd'T'HH:mm:ss'Z'",
        "yyyy-MM-dd'T'HH:mm:ss.SSSSSSS",
        "yyyy-MM-dd'T'HH:mm:ss",
    )
    patterns.forEach { pattern ->
        val formatter = SimpleDateFormat(pattern, Locale.US).apply {
            timeZone = TimeZone.getTimeZone("UTC")
            isLenient = false
        }
        try {
            return formatter.parse(raw)
        } catch (_: ParseException) {
            // tenta o próximo formato
        }
    }
    return null
}
