package org.mulletaflix.core.api.dto

import com.squareup.moshi.Json
import com.squareup.moshi.JsonClass

/** Minimal TimerInfoDto payload accepted by the server's LiveTv/Timers endpoint. */
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
    @param:Json(name = "PrePaddingSeconds") val prePaddingSeconds: Int = 0,
    @param:Json(name = "PostPaddingSeconds") val postPaddingSeconds: Int = 0,
)
