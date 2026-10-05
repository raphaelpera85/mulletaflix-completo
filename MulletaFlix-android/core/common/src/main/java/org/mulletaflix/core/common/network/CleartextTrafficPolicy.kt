package org.mulletaflix.core.common.network

import java.io.IOException
import java.net.InetAddress
import okhttp3.HttpUrl
import okhttp3.HttpUrl.Companion.toHttpUrlOrNull
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
            normalized.endsWith(".lan") || normalized.endsWith(".home.arpa")
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
            address[10].toInt() == 0xff.toByte() && address[11].toInt() == 0xff.toByte()
        if (!ipv4Mapped) return false
        return isLocalIpv4(address.takeLast(4).joinToString(".") { (it.toInt() and 0xff).toString() })
    }
}

/** Checks every exchange, including redirect destinations, before it reaches the wire. */
object LocalNetworkCleartextInterceptor : Interceptor {
    override fun intercept(chain: Interceptor.Chain): Response {
        CleartextTrafficPolicy.requireAllowed(chain.request().url)
        return chain.proceed(chain.request())
    }
}
