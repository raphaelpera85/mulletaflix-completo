package org.mulletaflix.feature.settings

private const val BYTES_PER_MIB = 1024L * 1024L
private const val BYTES_PER_GIB = 1024L * BYTES_PER_MIB

/** Keeps sub-GiB free space visible and avoids discarding useful precision above one GiB. */
internal fun availableStorageLabel(usableBytes: Long): String {
    if (usableBytes < 0L) return "Espaço indisponível"
    val bytes = usableBytes
    return if (bytes < BYTES_PER_GIB) {
        "${bytes / BYTES_PER_MIB} MB disponíveis"
    } else {
        val whole = bytes / BYTES_PER_GIB
        val tenths = (bytes % BYTES_PER_GIB) * 10 / BYTES_PER_GIB
        val amount = if (tenths == 0L) whole.toString() else "$whole,$tenths"
        "$amount GB disponíveis"
    }
}
