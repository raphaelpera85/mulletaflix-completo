package org.mulletaflix.feature.player

/** Presents stream metadata without exposing server-specific field names. */
internal fun trackLabel(track: TrackInfo): String = buildList {
    add(track.displayName)
    track.codec
        ?.trim()
        ?.takeIf { it.isNotEmpty() }
        ?.uppercase()
        ?.let(::add)
    track.channels?.let { channels ->
        when {
            channels >= 8 -> add("7.1")
            channels >= 6 -> add("5.1")
            channels >= 2 -> add("Estéreo")
            channels == 1 -> add("Mono")
        }
    }
    if (track.isDefault) add("Padrão")
    if (track.isForced) add("Forçada")
}.joinToString(" • ")
