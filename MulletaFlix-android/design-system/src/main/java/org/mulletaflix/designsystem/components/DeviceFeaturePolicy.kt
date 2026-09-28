package org.mulletaflix.designsystem.components

/** Downloads are an offline-storage feature for handheld devices, not Android TV. */
fun downloadsAvailableOnDevice(isTelevision: Boolean): Boolean = !isTelevision
