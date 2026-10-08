package org.mulletaflix.feature.itemdetail

/** Sorts archive entries naturally while tokenizing each page name only once per sort. */
internal object NaturalPageOrder {
    private val tokenPattern = Regex("\\d+|\\D+")

    fun <T> sort(items: Iterable<T>, nameOf: (T) -> String): List<T> =
        items.map { item ->
            val name = nameOf(item)
            SortEntry(item, name, tokenPattern.findAll(name).map { it.value }.toList())
        }.sortedWith(::compareEntries).map { it.item }

    private fun <T> compareEntries(left: SortEntry<T>, right: SortEntry<T>): Int {
        for (index in 0 until minOf(left.tokens.size, right.tokens.size)) {
            val leftToken = left.tokens[index]
            val rightToken = right.tokens[index]
            val comparison = if (leftToken.firstOrNull()?.isDigit() == true && rightToken.firstOrNull()?.isDigit() == true) {
                compareDigitTokens(leftToken, rightToken)
            } else {
                leftToken.compareTo(rightToken, ignoreCase = true)
            }
            if (comparison != 0) return comparison
        }
        if (left.tokens.size != right.tokens.size) return left.tokens.size.compareTo(right.tokens.size)
        return left.name.compareTo(right.name, ignoreCase = true).takeIf { it != 0 }
            ?: left.name.compareTo(right.name)
    }

    private fun compareDigitTokens(left: String, right: String): Int {
        val leftSignificant = left.dropWhile { it == '0' }.ifEmpty { "0" }
        val rightSignificant = right.dropWhile { it == '0' }.ifEmpty { "0" }
        return leftSignificant.length.compareTo(rightSignificant.length).takeIf { it != 0 }
            ?: leftSignificant.compareTo(rightSignificant).takeIf { it != 0 }
            ?: left.length.compareTo(right.length)
    }

    private data class SortEntry<T>(
        val item: T,
        val name: String,
        val tokens: List<String>,
    )
}
