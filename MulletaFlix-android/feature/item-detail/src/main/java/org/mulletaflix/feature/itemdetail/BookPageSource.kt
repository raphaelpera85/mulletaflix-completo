package org.mulletaflix.feature.itemdetail

import android.graphics.Bitmap
import org.readium.r2.shared.publication.Locator

/** Page-oriented book source shared by PDF and comic readers. */
internal interface BookPageSource {
    val pageCount: Int

    fun locatorForPage(index: Int): Locator

    fun pageIndexFromLocator(locator: Locator?): Int?

    fun decodePage(index: Int, maxWidth: Int, maxHeight: Int): Bitmap
}
