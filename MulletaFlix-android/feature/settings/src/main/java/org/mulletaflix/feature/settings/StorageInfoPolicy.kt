package org.mulletaflix.feature.settings

private const val BYTES_PER_GIB = 1024L * 1024L * 1024L

/** Floors available bytes to a stable whole-GiB value for the settings summary. */
internal fun availableStorageGb(usableBytes: Long): Int =
    (usableBytes.coerceAtLeast(0L) / BYTES_PER_GIB).coerceAtMost(Int.MAX_VALUE.toLong()).toInt()

internal fun availableStorageLabel(usableBytes: Long): String =
    "${availableStorageGb(usableBytes)} GB disponíveis"
