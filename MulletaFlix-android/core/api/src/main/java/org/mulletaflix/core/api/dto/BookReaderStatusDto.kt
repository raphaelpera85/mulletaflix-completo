package org.mulletaflix.core.api.dto

import com.squareup.moshi.Json
import com.squareup.moshi.JsonClass

@JsonClass(generateAdapter = true)
data class BookReaderStatusDto(
    @Json(name = "status") val status: String = "Unsupported",
    @Json(name = "itemId") val itemId: String? = null,
    @Json(name = "itemName") val itemName: String? = null,
)
