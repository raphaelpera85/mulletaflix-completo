package org.mulletaflix.feature.player

import java.net.URI
import org.mulletaflix.designsystem.media.retargetMediaUrl

/**
 * Whether a prepared stream has to move at all — the decision on its own.
 *
 * Split out from [retargetPreparedStreamUrl] so a caller can ask the question *before*
 * paying for the answer: reading the session credential is a storage access, and the
 * address flow emits on every preference write. It is also the single definition of the
 * comparison, so "nothing to move" and "what it would move to" cannot disagree.
 *
 * False is returned, deliberately, when:
 *
 *  - the URL is not `http`/`https` — an offline download plays from a local file, and
 *    re-pointing it at a server would be wrong;
 *  - the address is blank or unparseable — the session has not been read yet, and
 *    guessing is worse than the URL already in use;
 *  - the URL already points at that host and port — `getBaseUrl()` emits on every
 *    preference write, and re-preparing a healthy stream would only stutter it.
 */
internal fun shouldRetargetPreparedStream(preparedUrl: String, baseUrl: String): Boolean {
    if (preparedUrl.isBlank() || baseUrl.isBlank()) return false

    val prepared = runCatching { URI(preparedUrl) }.getOrNull() ?: return false
    val scheme = prepared.scheme ?: return false
    if (!scheme.equals("http", ignoreCase = true) && !scheme.equals("https", ignoreCase = true)) return false
    if (prepared.host.isNullOrBlank()) return false

    val base = runCatching { URI(baseUrl.trimEnd('/')) }.getOrNull() ?: return false
    if (base.host.isNullOrBlank()) return false

    return !(prepared.host.equals(base.host, ignoreCase = true) && prepared.port == base.port)
}

/**
 * The URL a prepared stream should move to after the server address changed, or null
 * when nothing has to move.
 *
 * The player is handed an absolute URL once, in `setUri`, and only fetches a playback
 * URL again when the title is reopened. The app meanwhile switches between the LAN
 * address and the public one on its own when the network disappears
 * (`LanServerRecovery`), so a stream that started at home keeps opening connections to
 * an address the device has already left: playback dies in the middle of an episode
 * and only comes back if the user exits and reopens it.
 *
 * The conditions are [shouldRetargetPreparedStream]; this adds where to move to.
 */
internal fun retargetPreparedStreamUrl(
    preparedUrl: String,
    baseUrl: String,
    accessToken: String?,
): String? {
    if (!shouldRetargetPreparedStream(preparedUrl, baseUrl)) return null

    return retargetMediaUrl(preparedUrl, baseUrl, accessToken)
}
