package org.mulletaflix.core.api.dto

import com.squareup.moshi.Json
import com.squareup.moshi.JsonClass

@JsonClass(generateAdapter = true)
data class MediaRequestDto(
    @Json(name = "Title") val title: String,
    @Json(name = "MediaType") val mediaType: String,
    @Json(name = "Year") val year: Int? = null,
    @Json(name = "Notes") val notes: String? = null,
)

@JsonClass(generateAdapter = true)
data class PlaybackIssueDto(
    @Json(name = "ItemId") val itemId: String,
    @Json(name = "Category") val category: String,
    @Json(name = "Description") val description: String? = null,
)
