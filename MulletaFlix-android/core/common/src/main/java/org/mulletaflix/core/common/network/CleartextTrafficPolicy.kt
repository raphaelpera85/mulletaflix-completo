package org.mulletaflix.core.common.network

import java.io.IOException
import java.net.InetAddress
import java.net.InterfaceAddress
import java.net.NetworkInterface
import java.net.Proxy
import java.util.Locale
import okhttp3.HttpUrl
import okhttp3.HttpUrl.Companion.toHttpUrlOrNull
import okhttp3.OkHttpClient
import okhttp3.Interceptor
import okhttp3.Response

/** Allows unencrypted traffic only to addresses that are local to the device or LAN. */
object CleartextTrafficPolicy {
    const val BLOCKED_MESSAGE = "HTTP sem criptografia só é permitido para servidores locais."

    fun isAllowed(url: HttpUrl): Boolean =
        url.scheme == "https" ||
            (url.scheme == "http" && isLocalNetworkHost(url.host, LocalNetworkSubnetProvider.activeSubnets()))

    fun isAllowed(url: String): Boolean {
        val parsed = url.toHttpUrlOrNull() ?: return false
        return isAllowed(parsed)
    }

    internal fun isAllowed(url: String, localSubnets: List<LocalNetworkSubnet>): Boolean {
        val parsed = url.toHttpUrlOrNull() ?: return false
        return parsed.scheme == "https" ||
            (parsed.scheme == "http" && isLocalNetworkHost(parsed.host, localSubnets))
    }

    internal fun isAllowedOnConnectedRoute(
        url: HttpUrl,
        localAddress: InetAddress,
        remoteAddress: InetAddress,
        localSubnets: List<LocalNetworkSubnet>,
    ): Boolean {
        if (url.scheme == "https") return true
        if (url.scheme != "http") return false
        if (localAddress.isLoopbackAddress && remoteAddress.isLoopbackAddress) return true

        val localBytes = localAddress.address
        val localIsOnInterface = localSubnets.any { it.contains(localBytes) }
        return localIsOnInterface && isLocalAddress(remoteAddress, localSubnets)
    }

    fun requireAllowed(url: HttpUrl) {
        if (!isAllowed(url)) throw IOException(BLOCKED_MESSAGE)
    }

    /** Classifies private/local endpoints without checking the current connection. */
    fun isLocalNetworkHost(host: String): Boolean {
        val normalized = host.trim().trim('[', ']').substringBefore('%').lowercase()
        if (normalized == "localhost" || normalized.endsWith(".local") ||
            normalized.endsWith(".home.arpa")
        ) return true

        val address = parseIpLiteral(normalized) ?: return false
        return address.isLoopbackAddress || when (address.address.size) {
            4 -> isLocalIpv4(address.address)
            16 -> isLocalIpv6(address.address)
            else -> false
        }
    }

    internal fun isLocalNetworkHost(host: String, localSubnets: List<LocalNetworkSubnet>): Boolean {
        val normalized = host.trim().trim('[', ']').substringBefore('%').lowercase()
        if (normalized == "localhost" || normalized.endsWith(".local") ||
            normalized.endsWith(".home.arpa")
        ) return true

        return isLocalAddress(normalized, localSubnets)
    }

    private fun isLocalAddress(host: String, localSubnets: List<LocalNetworkSubnet>): Boolean {
        val address = parseIpLiteral(host) ?: return false
        return isLocalAddress(address, localSubnets)
    }

    private fun isLocalAddress(address: InetAddress, localSubnets: List<LocalNetworkSubnet>): Boolean {
        if (address.isLoopbackAddress) return true

        val bytes = address.address
        val isLocalRange = when (bytes.size) {
            4 -> isLocalIpv4(bytes)
            16 -> isLocalIpv6(bytes)
            else -> false
        }
        return isLocalRange && localSubnets.any { it.contains(bytes) }
    }

    private fun parseIpLiteral(host: String): InetAddress? {
        if (host.contains(':')) {
            return runCatching { InetAddress.getByName(host) }.getOrNull()
        }

        val octets = host.split('.')
        if (octets.size != 4) return null
        val bytes = octets.map {
            it.toIntOrNull()?.takeIf { value -> value in 0..255 }?.toByte() ?: return null
        }.toByteArray()
        return runCatching { InetAddress.getByAddress(bytes) }.getOrNull()
    }

    private fun isLocalIpv4(address: ByteArray): Boolean {
        val first = address[0].toInt() and 0xff
        val second = address[1].toInt() and 0xff
        return first == 10 ||
            first == 127 ||
            (first == 169 && second == 254) ||
            (first == 172 && second in 16..31) ||
            (first == 192 && second == 168)
    }

    private fun isLocalIpv6(address: ByteArray): Boolean {
        val first = address[0].toInt() and 0xff
        val second = address[1].toInt() and 0xff
        val uniqueLocal = (first and 0xfe) == 0xfc
        val linkLocal = first == 0xfe && (second and 0xc0) == 0x80
        val deprecatedSiteLocal = first == 0xfe && (second and 0xc0) == 0xc0

        if (uniqueLocal || linkLocal || deprecatedSiteLocal) return true

        val ipv4Mapped = address.take(10).all { it.toInt() == 0 } &&
            address[10] == 0xff.toByte() && address[11] == 0xff.toByte()
        if (!ipv4Mapped) return false
        return isLocalIpv4(address.takeLast(4).toByteArray())
    }
}

internal data class LocalNetworkSubnet(val address: ByteArray, val prefixLength: Int) {
    fun contains(candidate: ByteArray): Boolean {
        if (address.size != candidate.size || prefixLength !in 1..(address.size * 8)) return false

        val wholeBytes = prefixLength / 8
        val remainingBits = prefixLength % 8
        for (index in 0 until wholeBytes) {
            if (address[index] != candidate[index]) return false
        }
        if (remainingBits == 0) return true

        val mask = (0xff shl (8 - remainingBits)) and 0xff
        return ((address[wholeBytes].toInt() and 0xff) and mask) ==
            ((candidate[wholeBytes].toInt() and 0xff) and mask)
    }
}

private object LocalNetworkSubnetProvider {
    fun activeSubnets(): List<LocalNetworkSubnet> = runCatching {
        val interfaces = NetworkInterface.getNetworkInterfaces() ?: return emptyList()
        buildList {
            while (interfaces.hasMoreElements()) {
                val networkInterface = interfaces.nextElement()
                val eligible = runCatching {
                    networkInterface.isUp && !networkInterface.isLoopback && !networkInterface.isPointToPoint
                }.getOrDefault(false)
                if (!eligible) continue

                val addresses: List<InterfaceAddress> = runCatching {
                    networkInterface.interfaceAddresses
                }.getOrDefault(emptyList())
                addresses.forEach { interfaceAddress ->
                    val address = interfaceAddress.address.address
                    val prefixLength = interfaceAddress.networkPrefixLength.toInt()
                    if (prefixLength in 1..(address.size * 8)) {
                        add(LocalNetworkSubnet(address, prefixLength))
                    }
                }
            }
        }
    }.getOrDefault(emptyList())

    fun subnetsFor(localAddress: InetAddress): List<LocalNetworkSubnet> {
        if (localAddress.isLoopbackAddress) return emptyList()
        val networkInterface = runCatching { NetworkInterface.getByInetAddress(localAddress) }.getOrNull()
            ?: return emptyList()
        val eligible = runCatching {
            networkInterface.isUp && !networkInterface.isLoopback && !networkInterface.isPointToPoint
        }.getOrDefault(false)
        if (!eligible) return emptyList()

        return runCatching {
            networkInterface.interfaceAddresses.mapNotNull { interfaceAddress ->
                val address = interfaceAddress.address.address
                val prefixLength = interfaceAddress.networkPrefixLength.toInt()
                if (prefixLength in 1..(address.size * 8)) {
                    LocalNetworkSubnet(address, prefixLength)
                } else {
                    null
                }
            }
        }.getOrDefault(emptyList())
    }
}

/** Checks every exchange, including redirect destinations, before it reaches the wire. */
class LocalNetworkCleartextInterceptor(
    internal val requireHttpsRedirects: Boolean = false,
    private val allowedHttpsRedirectHosts: Set<String>? = null,
    private val allowedHttpsRedirectPorts: Set<Int> = setOf(443),
) : Interceptor {
    private companion object {
        const val HTTPS_REDIRECT_BLOCKED_MESSAGE = "HTTPS requests must not redirect to HTTP."
        const val UNTRUSTED_HTTPS_REDIRECT_BLOCKED_MESSAGE = "HTTPS redirect host is not trusted."
        const val NON_DEFAULT_HTTPS_REDIRECT_PORT_BLOCKED_MESSAGE = "HTTPS redirects must use the default port."
        const val MAX_REDIRECTS = 20
        val REDIRECT_CODES = setOf(300, 301, 302, 303, 307, 308)
        val CREDENTIAL_QUERY_PARAMETER_NAMES = setOf(
            "apikey",
            "xapikey",
            "key",
            "accesskey",
            "awsaccesskeyid",
            "consumerkey",
            "token",
            "accesstoken",
            "refreshtoken",
            "idtoken",
            "auth",
            "authorization",
            "password",
            "passwd",
            "passphrase",
            "pwd",
            "secret",
            "clientsecret",
            "clientassertion",
            "code",
            "codeverifier",
            "credential",
            "credentials",
            "session",
            "sessionid",
            "sid",
            "phpsessid",
            "jsessionid",
            "oauthverifier",
            "jwt",
            "bearer",
        )
        val REDIRECT_TARGET_QUERY_PARAMETER_NAMES = setOf(
            "paginationtoken",
            "continuationtoken",
            "nextpagetoken",
            "nexttoken",
            "pagetoken",
            "cursor",
            "signature",
            "sig",
            "xgoogcredential",
            "xgoogsignature",
            "xamzcredential",
            "xamzsignature",
            "xamzsecuritytoken",
        )
    }

    private fun isCredentialQueryParameter(name: String): Boolean {
        val normalized = name.lowercase(Locale.ROOT).filter(Char::isLetterOrDigit)
        // Keep pagination cursors and destination-issued signed URLs working across redirects.
        if (normalized in REDIRECT_TARGET_QUERY_PARAMETER_NAMES) return false
        return normalized in CREDENTIAL_QUERY_PARAMETER_NAMES ||
            normalized.endsWith("token") ||
            normalized.endsWith("password") ||
            normalized.endsWith("passwd") ||
            normalized.endsWith("secret") ||
            normalized.endsWith("credential")
    }

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
            val sameOrigin = request.url.scheme == redirectUrl.scheme &&
                request.url.host == redirectUrl.host && request.url.port == redirectUrl.port

            val method = request.method
            val redirectToGet = (response.code == 303 && method != "HEAD") ||
                (response.code in setOf(301, 302) && method == "POST")
            if (!redirectToGet && method !in setOf("GET", "HEAD")) return response
            if (redirects == MAX_REDIRECTS) {
                response.close()
                throw IOException("Too many redirects: $MAX_REDIRECTS")
            }

            try {
                if (requireHttpsRedirects && !redirectUrl.isHttps) {
                    throw IOException(HTTPS_REDIRECT_BLOCKED_MESSAGE)
                }
                if (requireHttpsRedirects &&
                    allowedHttpsRedirectHosts != null &&
                    redirectUrl.host !in allowedHttpsRedirectHosts
                ) {
                    throw IOException(UNTRUSTED_HTTPS_REDIRECT_BLOCKED_MESSAGE)
                }
                if (requireHttpsRedirects &&
                    allowedHttpsRedirectHosts != null &&
                    !sameOrigin &&
                    redirectUrl.port !in allowedHttpsRedirectPorts
                ) {
                    throw IOException(NON_DEFAULT_HTTPS_REDIRECT_PORT_BLOCKED_MESSAGE)
                }
                CleartextTrafficPolicy.requireAllowed(redirectUrl)
            } catch (failure: IOException) {
                response.close()
                throw failure
            }

            val safeRedirectUrl = if (sameOrigin) {
                redirectUrl
            } else {
                val redirectBuilder = redirectUrl.newBuilder()
                    .username("")
                    .password("")
                redirectUrl.queryParameterNames
                    .filter(::isCredentialQueryParameter)
                    .forEach(redirectBuilder::removeAllQueryParameters)
                redirectBuilder.build()
            }
            val builder = request.newBuilder().url(safeRedirectUrl)
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

/** Last-resort guard for each physical exchange; network interceptors proceed once. */
object LocalNetworkCleartextNetworkInterceptor : Interceptor {
    override fun intercept(chain: Interceptor.Chain): Response {
        val request = chain.request()
        if (request.url.scheme == "http") {
            val connection = chain.connection()
                ?: throw IOException(CleartextTrafficPolicy.BLOCKED_MESSAGE)
            if (connection.route().proxy.type() != Proxy.Type.DIRECT) {
                throw IOException(CleartextTrafficPolicy.BLOCKED_MESSAGE)
            }

            val localAddress = connection.socket().localAddress
            val remoteAddress = connection.route().socketAddress.address
                ?: throw IOException(CleartextTrafficPolicy.BLOCKED_MESSAGE)
            val allowed = CleartextTrafficPolicy.isAllowedOnConnectedRoute(
                request.url,
                localAddress,
                remoteAddress,
                LocalNetworkSubnetProvider.subnetsFor(localAddress),
            )
            if (!allowed) throw IOException(CleartextTrafficPolicy.BLOCKED_MESSAGE)
        }
        return chain.proceed(chain.request())
    }
}

/** Controls redirects in-app, then checks every request again at the network boundary. */
fun OkHttpClient.Builder.enforceLocalNetworkCleartextPolicy(
    requireHttpsRedirects: Boolean = false,
    allowedHttpsRedirectHosts: Set<String>? = null,
    allowedHttpsRedirectPorts: Set<Int> = setOf(443),
): OkHttpClient.Builder =
    followRedirects(false)
        .followSslRedirects(false)
        .addInterceptor(
            LocalNetworkCleartextInterceptor(requireHttpsRedirects, allowedHttpsRedirectHosts, allowedHttpsRedirectPorts),
        )
        .addNetworkInterceptor(LocalNetworkCleartextNetworkInterceptor)
