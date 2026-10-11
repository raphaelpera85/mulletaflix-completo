package org.mulletaflix.feature.itemdetail

import androidx.compose.ui.graphics.Color
import android.graphics.Color as AndroidColor
import org.readium.r2.navigator.preferences.Color as ReadiumColor

internal enum class BookReaderAppearance(
    val label: String,
    val backgroundHex: String?,
    val textHex: String?,
) {
    ORIGINAL("Original", null, null),
    SEPIA("Sépia", "#F4ECD8", "#332B1D"),
    DARK("Escuro", "#121212", "#E6E1D8");

    val backgroundColor: Color?
        get() = backgroundHex?.let { Color(AndroidColor.parseColor(it)) }

    val textColor: Color?
        get() = textHex?.let { Color(AndroidColor.parseColor(it)) }

    val readiumBackgroundColor: ReadiumColor?
        get() = backgroundHex?.let { ReadiumColor(AndroidColor.parseColor(it)) }

    val readiumTextColor: ReadiumColor?
        get() = textHex?.let { ReadiumColor(AndroidColor.parseColor(it)) }

    companion object {
        fun fromStoredValue(value: String?): BookReaderAppearance =
            entries.firstOrNull { it.name == value } ?: ORIGINAL
    }
}
