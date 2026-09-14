package org.mulletaflix.android

/** A persisted session is usable only when all values needed by the API exist. */
internal fun hasUsableSession(serverUrl: String, accessToken: String?, userId: String?): Boolean =
    serverUrl.isNotBlank() && !accessToken.isNullOrBlank() && !userId.isNullOrBlank()
