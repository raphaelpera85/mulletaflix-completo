package org.mulletaflix.core.api.dto

import com.squareup.moshi.Json
import com.squareup.moshi.JsonClass

/**
 * Minimal TimerInfoDto payload accepted by the server's `LiveTv/Timers` endpoint.
 *
 * [serviceName] is not optional on the server: `LiveTvManager.CreateTimer` calls
 * `GetService(timer.ServiceName)` before anything else, and a null name throws
 * `KeyNotFoundException`, which the API maps to HTTP 500. Always take it from
 * `LiveTv/Timers/Defaults?programId=`.
 */
@JsonClass(generateAdapter = true)
data class CreateLiveTvTimerDto(
    @param:Json(name = "Type") val type: String = "Timer",
    @param:Json(name = "ProgramId") val programId: String,
    @param:Json(name = "ChannelId") val channelId: String,
    @param:Json(name = "ChannelName") val channelName: String? = null,
    @param:Json(name = "Name") val name: String,
    @param:Json(name = "Overview") val overview: String? = null,
    @param:Json(name = "StartDate") val startDate: String,
    @param:Json(name = "EndDate") val endDate: String,
    @param:Json(name = "ServiceName") val serviceName: String,
    @param:Json(name = "PrePaddingSeconds") val prePaddingSeconds: Int = 0,
    @param:Json(name = "PostPaddingSeconds") val postPaddingSeconds: Int = 0,
)

/**
 * Subset of the server's `SeriesTimerInfoDto`, as returned by
 * `LiveTv/Timers/Defaults`.
 *
 * Unknown JSON fields are ignored, which is what lets the server add fields
 * without breaking this client. Every field stays nullable so a missing one
 * falls back to the programme being scheduled instead of crashing.
 */
@JsonClass(generateAdapter = true)
data class TimerDefaultsDto(
    @param:Json(name = "ServiceName") val serviceName: String? = null,
    @param:Json(name = "ChannelId") val channelId: String? = null,
    @param:Json(name = "Name") val name: String? = null,
    @param:Json(name = "Overview") val overview: String? = null,
    @param:Json(name = "StartDate") val startDate: String? = null,
    @param:Json(name = "EndDate") val endDate: String? = null,
    @param:Json(name = "PrePaddingSeconds") val prePaddingSeconds: Int? = null,
    @param:Json(name = "PostPaddingSeconds") val postPaddingSeconds: Int? = null,
)

/**
 * Subset of the server's `TimerInfoDto`, as returned by `LiveTv/Timers`.
 *
 * Only [programId] is used: it is what lets the guide mark a programme as already
 * scheduled. Everything else is optional so the server can extend the payload
 * without breaking parsing.
 */
@JsonClass(generateAdapter = true)
data class LiveTvTimerDto(
    @param:Json(name = "Id") val id: String? = null,
    @param:Json(name = "ProgramId") val programId: String? = null,
    @param:Json(name = "ChannelId") val channelId: String? = null,
    @param:Json(name = "Name") val name: String? = null,
    @param:Json(name = "StartDate") val startDate: String? = null,
    @param:Json(name = "EndDate") val endDate: String? = null,
)

/** Envelope for `LiveTv/Timers`; the page fields are ignored for now. */
@JsonClass(generateAdapter = true)
data class LiveTvTimerQueryResultDto(
    @param:Json(name = "Items") val items: List<LiveTvTimerDto>? = null,
    @param:Json(name = "TotalRecordCount") val totalRecordCount: Int? = null,
)
