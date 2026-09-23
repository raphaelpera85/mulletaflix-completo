package org.mulletaflix.designsystem.media

import androidx.compose.runtime.compositionLocalOf
import java.net.URI
import java.net.URLEncoder
import java.nio.charset.StandardCharsets

/** Base address of the currently selected MulletaFlix server. */
val LocalMulletaFlixServerUrl = compositionLocalOf { "" }
val LocalMulletaFlixAccessToken = compositionLocalOf<String?> { null }

/**
 * Identifier of the currently selected server.
 *
 * Shared links carry this id so a recipient on a different server can be told
 * which server the item belongs to instead of resolving the id against their
 * own library.
 */
val LocalMulletaFlixServerId = compositionLocalOf<String?> { null }

/** Converts API-relative image paths to authenticated-server-relative paths. */
fun resolveMediaUrl(baseUrl: String, path: String?, accessToken: String? = null): String? {
    if (path.isNullOrBlank()) return null
    if (path.startsWith("http://") || path.startsWith("https://")) {
        return path.withServerToken(baseUrl, accessToken)
            .also { logResolvedMediaUrl(it) }
    }
    val normalizedBase = baseUrl.trimEnd('/')
    if (normalizedBase.isBlank()) return null
    val url = "$normalizedBase/${path.trimStart('/')}"
    return accessToken?.takeIf { it.isNotBlank() }?.let {
        val encodedToken = URLEncoder.encode(it, StandardCharsets.UTF_8.name())
        url + if (url.contains('?')) "&api_key=$encodedToken" else "?api_key=$encodedToken"
    } ?: url
}

/**
 * Records the artwork URL actually handed to the image loader, with the token
 * redacted.
 *
 * A cover grid that reaches the server as *anonymous* is throttled by the
 * server's rate limiter and loads very slowly, and the only way to tell an
 * authenticated image URL from an anonymous one is to look at what was
 * requested. Debug builds only, and the token is never printed — logcat tag
 * `MulletaFlixImage`.
 */
private fun logResolvedMediaUrl(url: String) {
    if (!isDebuggableApp()) return
    android.util.Log.d(TAG_IMAGE_URL, "request ${redactToken(url)}")
}

internal const val TAG_IMAGE_URL = "MulletaFlixImage"

/** True only when the running application is a debuggable build. */
private fun isDebuggableApp(): Boolean = runCatching {
    val application = currentApplication() ?: return@runCatching false
    application.applicationInfo.flags and android.content.pm.ApplicationInfo.FLAG_DEBUGGABLE != 0
}.getOrDefault(false)

private fun currentApplication(): android.app.Application? = runCatching {
    Class.forName("android.app.ActivityThread")
        .getMethod("currentApplication")
        .invoke(null) as? android.app.Application
}.getOrNull()

/**
 * Replaces the value of every credential-bearing part of [url] with a marker.
 *
 * The previous version listed the spellings it knew — `api_key`, `ApiKey`,
 * `X-Emby-Token` — and the test enumerated the same three, so a spelling nobody
 * had thought of was printed verbatim into logcat. The server alone accepts two
 * query names and two headers
 * (`Jellyfin.Server.Implementations/Security/AuthorizationContext.cs`), and the
 * list was already coupled to a decision made on the other side of the wire.
 *
 * A deny-list of names ages badly; the rule is inverted. Any query parameter whose
 * name *contains* `key` or `token` is treated as a secret, whatever the separators
 * or the capitalisation, and a password in the `user:password@host` part is masked
 * too. This over-redacts rather than under-redacts: a diagnostic line losing the
 * value of a harmless `monkey` parameter costs nothing next to a leaked session
 * token.
 */
fun redactToken(url: String): String = url
    .replace(TOKEN_QUERY_PARAMETER) { match -> "${match.groupValues[1]}<redacted>" }
    .replace(USER_INFO_PASSWORD) { match -> "${match.groupValues[1]}<redacted>@" }

/**
 * `?`/`&`, a parameter name containing `key` or `token`, `=`, then the value.
 *
 * The value stops at the next `&` or `#`, so one redaction never eats the
 * parameters after it.
 */
private val TOKEN_QUERY_PARAMETER =
    Regex("""([?&][^=&#]*(?:key|token)[^=&#]*=)[^&#]*""", RegexOption.IGNORE_CASE)

/** The password in `scheme://user:password@host`. */
private val USER_INFO_PASSWORD = Regex("""(://[^/@:]*:)[^/@]*@""")

/** Namespace used for artwork when the server has not told the app its identity. */
internal const val UNKNOWN_SERVER_IDENTITY = "unknown-server"

/**
 * Cache key for a piece of artwork, shared by every address of the same server.
 *
 * The image loader keys its memory and disk caches on the request URL, and that URL
 * contains both the address and the session token — so the LAN address and the public
 * address of the *same* server produced two keys for the same picture. Entering and
 * leaving home threw the whole cover grid away and left two copies of every poster on
 * disk until the size evictor got around to them.
 *
 * The key keeps what identifies the picture and drops what identifies the *route* to
 * it:
 *
 *  - the server identity, so two different servers cannot share an entry. Item ids are
 *    GUIDs, so a collision is far-fetched, but the app supports several saved servers
 *    and "far-fetched" is not "impossible";
 *  - the path and the query **minus the credential**, because `?tag=` is part of the
 *    image identity (the server returns a different picture when the tag changes) and
 *    so is `?width=`/`?height=` — dropping the query would hand a 300 px thumbnail to
 *    a request for the full poster.
 *
 * Null means "leave the default key alone": a resource id or a local file is not
 * artwork served by a server and has no address to normalise.
 */
fun canonicalImageCacheKey(url: String, serverId: String?): String? {
    if (url.isBlank()) return null
    val parsed = runCatching { URI(url) }.getOrNull() ?: return null
    val scheme = parsed.scheme ?: return null
    if (!scheme.equals("http", ignoreCase = true) && !scheme.equals("https", ignoreCase = true)) return null
    if (parsed.host.isNullOrBlank()) return null

    val identity = serverId?.takeIf { it.isNotBlank() } ?: UNKNOWN_SERVER_IDENTITY
    return buildString {
        append("mulletaflix|").append(identity).append('|')
        append(parsed.rawPath.orEmpty().ifBlank { "/" })
        withoutCredentialParameters(parsed.rawQuery)?.let { append('|').append(it) }
    }
}

/**
 * The query without the parameters that carry a credential.
 *
 * It is fed to the same [TOKEN_QUERY_PARAMETER] rule as [redactToken] and
 * [retargetMediaUrl], so "which parameter is a secret" still has one definition. A
 * value is emptied rather than removed by that rule, so parameters left without a
 * value are dropped here.
 */
private fun withoutCredentialParameters(rawQuery: String?): String? {
    if (rawQuery.isNullOrBlank()) return null
    return "?$rawQuery"
        .replace(TOKEN_QUERY_PARAMETER) { match -> match.groupValues[1] }
        .removePrefix("?")
        .split('&')
        .filter { it.substringAfter('=', "").isNotEmpty() }
        .joinToString("&")
        .ifBlank { null }
}

/**
 * Points an already-built media URL at the server address that is in use now.
 *
 * The address a URL was built with is baked into the string, and a playback URL also
 * carries the session token. Two places held on to one for a long time:
 *
 *  - the *download queue*: the URL is written into the Media3 index, and "Tentar
 *    novamente" re-added exactly that URI, so a download failed at home and kept
 *    being retried against the LAN address after the app had already switched to the
 *    public one;
 *  - a *player already prepared*, which only fetches a playback URL again when the
 *    user reopens the title.
 *
 * Only scheme, host and port are taken from [baseUrl]; the path and query of
 * [storedUrl] are kept as they are. Both addresses are the same installation, so the
 * absolute path stays valid — and rebuilding it from the base path would double a
 * `/jellyfin` prefix when one side carries it and the other does not.
 *
 * A credential already in the URL is **replaced**, not appended, using the same
 * "a parameter whose name contains `key` or `token`" rule as [redactToken].
 *
 * Anything that cannot be parsed is returned untouched: guessing at a URL is worse
 * than retrying the one that was stored. A blank [baseUrl] means the session has not
 * been read yet, and is also left untouched.
 */
fun retargetMediaUrl(storedUrl: String, baseUrl: String, accessToken: String?): String {
    if (storedUrl.isBlank() || baseUrl.isBlank()) return storedUrl
    val stored = runCatching { URI(storedUrl) }.getOrNull() ?: return storedUrl
    if (stored.host.isNullOrBlank()) return storedUrl

    val base = runCatching { URI(baseUrl.trimEnd('/')) }.getOrNull() ?: return storedUrl
    if (base.host.isNullOrBlank()) return storedUrl

    val address = buildString {
        append(base.scheme ?: stored.scheme ?: "http")
        append("://")
        append(base.host)
        if (base.port >= 0) append(':').append(base.port)
    }
    val retargeted = buildString {
        append(address)
        append(stored.rawPath.orEmpty().ifBlank { "/" })
        stored.rawQuery?.let { append('?').append(it) }
        stored.rawFragment?.let { append('#').append(it) }
    }

    val token = accessToken?.takeIf { it.isNotBlank() } ?: return retargeted
    val encodedToken = URLEncoder.encode(token, StandardCharsets.UTF_8.name())
    return if (TOKEN_QUERY_PARAMETER.containsMatchIn(retargeted)) {
        retargeted.replace(TOKEN_QUERY_PARAMETER) { match -> "${match.groupValues[1]}$encodedToken" }
    } else {
        retargeted + if (retargeted.contains('?')) "&api_key=$encodedToken" else "?api_key=$encodedToken"
    }
}

private fun String.withServerToken(baseUrl: String, accessToken: String?): String {
    val token = accessToken?.takeIf { it.isNotBlank() } ?: return this
    val isSameOrigin = runCatching {
        val server = URI(baseUrl.trimEnd('/'))
        val media = URI(this)
        server.scheme.equals(media.scheme, ignoreCase = true) &&
            server.host.equals(media.host, ignoreCase = true) &&
            effectivePort(server) == effectivePort(media)
    }.getOrDefault(false)
    if (!isSameOrigin) return this

    // Same rule as the redaction: this used to look only for `api_key`, so a URL the
    // server had already authenticated as `ApiKey` or `X-Emby-Token` came back with a
    // second credential appended.
    if (TOKEN_QUERY_PARAMETER.containsMatchIn(this)) return this

    val encodedToken = URLEncoder.encode(token, StandardCharsets.UTF_8.name())
    return this + if (contains('?')) "&api_key=$encodedToken" else "?api_key=$encodedToken"
}

private fun effectivePort(uri: URI): Int = when {
    uri.port >= 0 -> uri.port
    uri.scheme.equals("https", ignoreCase = true) -> 443
    else -> 80
}

/**
 * Path of a server-side user profile image (avatar), or null when the server
 * has no image for that user.
 *
 * The `Tag` parameter is required: without it the image endpoint answers 200
 * with an empty body. The tag comes from `PrimaryImageTag` on the user payload.
 */
fun userAvatarPath(userId: String?, primaryImageTag: String?): String? {
    if (userId.isNullOrBlank() || primaryImageTag.isNullOrBlank()) return null
    return "Users/$userId/Images/Primary?tag=$primaryImageTag"
}
