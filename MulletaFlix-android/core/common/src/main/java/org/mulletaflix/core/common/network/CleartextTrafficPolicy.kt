package org.mulletaflix.core.common.network

import java.io.IOException
import java.net.InetAddress
import okhttp3.HttpUrl
import okhttp3.HttpUrl.Companion.toHttpUrlOrNull
import okhttp3.OkHttpClient
import okhttp3.Interceptor
import okhttp3.Response

/** Allows unencrypted traffic only to addresses that are local to the device or LAN. */
object CleartextTrafficPolicy {
    const val BLOCKED_MESSAGE = "HTTP sem criptografia só é permitido para servidores locais."

    fun isAllowed(url: HttpUrl): Boolean =
        url.scheme == "https" || (url.scheme == "http" && isLocalNetworkHost(url.host))

    fun isAllowed(url: String): Boolean {
        val parsed = url.toHttpUrlOrNull() ?: return false
        return isAllowed(parsed)
    }

    fun requireAllowed(url: HttpUrl) {
        if (!isAllowed(url)) throw IOException(BLOCKED_MESSAGE)
    }

    /** Does not resolve hostnames, avoiding DNS work and rebinding-based allow decisions. */
    fun isLocalNetworkHost(host: String): Boolean {
        val normalized = host.trim().trim('[', ']').substringBefore('%').lowercase()
        if (normalized == "localhost" || normalized.endsWith(".local") ||
            normalized.endsWith(".home.arpa")
        ) return true

        return isLocalIpv4(normalized) || isLocalIpv6(normalized)
    }

    private fun isLocalIpv4(host: String): Boolean {
        val octets = host.split('.')
        if (octets.size != 4) return false
        val address = octets.map { it.toIntOrNull()?.takeIf { value -> value in 0..255 } ?: return false }
        val first = address[0]
        val second = address[1]
        return first == 10 ||
            first == 127 ||
            (first == 169 && second == 254) ||
            (first == 172 && second in 16..31) ||
            (first == 192 && second == 168)
    }

    private fun isLocalIpv6(host: String): Boolean {
        if (!host.contains(':')) return false
        val address = runCatching { InetAddress.getByName(host).address }.getOrNull() ?: return false
        if (address.size != 16) return false

        val first = address[0].toInt() and 0xff
        val second = address[1].toInt() and 0xff
        val loopback = address.dropLast(1).all { it.toInt() == 0 } && address.last().toInt() == 1
        val uniqueLocal = (first and 0xfe) == 0xfc
        val linkLocal = first == 0xfe && (second and 0xc0) == 0x80
        val deprecatedSiteLocal = first == 0xfe && (second and 0xc0) == 0xc0

        if (loopback || uniqueLocal || linkLocal || deprecatedSiteLocal) return true

        val ipv4Mapped = address.take(10).all { it.toInt() == 0 } &&
            address[10] == 0xff.toByte() && address[11] == 0xff.toByte()
        if (!ipv4Mapped) return false
        return isLocalIpv4(address.takeLast(4).joinToString(".") { (it.toInt() and 0xff).toString() })
    }
}

/** Checks every exchange, including redirect destinations, before it reaches the wire. */
object LocalNetworkCleartextInterceptor : Interceptor {
    private const val MAX_REDIRECTS = 20
    private val REDIRECT_CODES = setOf(300, 301, 302, 303, 307, 308)

    override fun intercept(chain: Interceptor.Chain): Response {
        var request = chain.request()
        var redirects = 0

        while (true) {
            CleartextTrafficPolicy.requireAllowed(request.url)
            val response = chain.proceed(request)
            val location = response.header("Location") ?: return response
            val redirectCode = response.code in REDIRECT_CODES
            val redirectUrl = if (redirectCode) request.url.resolve(location) else null
            if (redirectUrl == null) return response

            val method = request.method
            val redirectToGet = (response.code == 303 && method != "HEAD") ||
                (response.code in setOf(301, 302) && method == "POST")
            if (!redirectToGet && method !in setOf("GET", "HEAD")) return response
            if (redirects == MAX_REDIRECTS) {
                response.close()
                throw IOException("Too many redirects: $MAX_REDIRECTS")
            }

            try {
                CleartextTrafficPolicy.requireAllowed(redirectUrl)
            } catch (failure: IOException) {
                response.close()
                throw failure
            }

            val sameOrigin = request.url.scheme == redirectUrl.scheme &&
                request.url.host == redirectUrl.host && request.url.port == redirectUrl.port
            val builder = request.newBuilder().url(redirectUrl)
            if (!sameOrigin) {
                builder.removeHeader("Authorization")
                    .removeHeader("Cookie")
                    .removeHeader("Proxy-Authorization")
            }
            if (redirectToGet) {
                builder.method("GET", null)
                    .removeHeader("Transfer-Encoding")
                    .removeHeader("Content-Length")
                    .removeHeader("Content-Type")
            }

            response.close()
            request = builder.build()
            redirects++
        }
    }

}

/** Controls redirects in-app, then checks every request again at the network boundary. */
fun OkHttpClient.Builder.enforceLocalNetworkCleartextPolicy(): OkHttpClient.Builder =
    followRedirects(false)
        .followSslRedirects(false)
        .addInterceptor(LocalNetworkCleartextInterceptor)
        .addNetworkInterceptor(LocalNetworkCleartextInterceptor)
