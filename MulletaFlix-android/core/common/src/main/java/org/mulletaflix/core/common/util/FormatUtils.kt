package org.mulletaflix.core.common.util

import java.util.Locale

/**
 * Common formatting utilities for ticks, duration, and file sizes.
 */
object FormatUtils {

    /** Converts video/audio playback ticks (100ns per tick) to milliseconds */
    fun ticksToMillis(ticks: Long): Long = ticks / 10_000L

    /** Converts milliseconds to video/audio ticks */
    fun millisToTicks(millis: Long): Long = millis * 10_000L

    /** Formats milliseconds into "H:MM:SS" or "M:SS" string */
    fun formatDuration(durationMs: Long): String {
        val totalSeconds = (durationMs.coerceAtLeast(0L)) / 1000L
        val hours = totalSeconds / 3600L
        val minutes = (totalSeconds % 3600L) / 60L
        val seconds = totalSeconds % 60L
        return if (hours > 0) {
            String.format(Locale.US, "%d:%02d:%02d", hours, minutes, seconds)
        } else {
            String.format(Locale.US, "%d:%02d", minutes, seconds)
        }
    }

    /** Formats runtime ticks into human friendly "Xh Ymin" or "Y min" string */
    fun formatRuntimeTicks(ticks: Long?): String? {
        if (ticks == null || ticks <= 0) return null
        val totalMinutes = (ticks / 600_000_000L).toInt()
        if (totalMinutes <= 0) return null
        val hours = totalMinutes / 60
        val minutes = totalMinutes % 60
        return when {
            hours > 0 && minutes > 0 -> "${hours}h ${minutes}min"
            hours > 0 -> "${hours}h"
            else -> "${minutes} min"
        }
    }

    /** Formats byte size into human readable string (e.g., "1.5 GB", "320.0 MB") */
    fun formatFileSize(bytes: Long): String {
        if (bytes <= 0) return "0 B"
        val units = arrayOf("B", "KB", "MB", "GB", "TB")
        val digitGroups = (Math.log10(bytes.toDouble()) / Math.log10(1024.0)).toInt().coerceIn(0, units.size - 1)
        val size = bytes / Math.pow(1024.0, digitGroups.toDouble())
        return String.format(Locale.US, "%.1f %s", size, units[digitGroups])
    }

    /** Formats progress percentage into "X%" */
    fun formatPercentage(percentage: Double?): String? {
        if (percentage == null || percentage <= 0.0) return null
        return "${percentage.coerceIn(0.0, 100.0).toInt()}%"
    }
}
