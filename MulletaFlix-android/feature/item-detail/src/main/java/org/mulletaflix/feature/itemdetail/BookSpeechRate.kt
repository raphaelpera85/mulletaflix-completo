package org.mulletaflix.feature.itemdetail

import kotlin.math.roundToInt

internal object BookSpeechRate {
    const val MIN_PERCENT = 75
    const val DEFAULT_PERCENT = 100
    const val MAX_PERCENT = 175
    private const val STEP_PERCENT = 25

    fun normalize(percent: Int): Int {
        val bounded = percent.coerceIn(MIN_PERCENT, MAX_PERCENT)
        val steps = ((bounded - MIN_PERCENT).toFloat() / STEP_PERCENT).roundToInt()
        return (MIN_PERCENT + steps * STEP_PERCENT).coerceIn(MIN_PERCENT, MAX_PERCENT)
    }

    fun decrease(percent: Int): Int = (normalize(percent) - STEP_PERCENT).coerceAtLeast(MIN_PERCENT)
    fun increase(percent: Int): Int = (normalize(percent) + STEP_PERCENT).coerceAtMost(MAX_PERCENT)
    fun canDecrease(percent: Int): Boolean = normalize(percent) > MIN_PERCENT
    fun canIncrease(percent: Int): Boolean = normalize(percent) < MAX_PERCENT
    fun multiplier(percent: Int): Double = normalize(percent) / 100.0
}
