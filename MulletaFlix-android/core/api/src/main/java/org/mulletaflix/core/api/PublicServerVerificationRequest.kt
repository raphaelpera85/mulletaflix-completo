package org.mulletaflix.core.api

/** Routes public server discovery to an unverified endpoint without forwarding session credentials. */
data class PublicServerVerificationRequest(val serverUrl: String)
