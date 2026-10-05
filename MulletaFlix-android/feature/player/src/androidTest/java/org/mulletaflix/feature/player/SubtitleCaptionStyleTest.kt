package org.mulletaflix.feature.player

import android.graphics.Bitmap
import android.graphics.Canvas
import android.graphics.Color
import android.text.SpannableString
import android.text.Spanned
import android.text.style.BackgroundColorSpan
import android.text.style.ForegroundColorSpan
import android.text.style.StyleSpan
import android.text.style.UnderlineSpan
import android.graphics.Typeface
import android.content.res.ColorStateList
import android.text.style.TextAppearanceSpan
import android.text.style.TypefaceSpan
import androidx.media3.common.text.Cue
import androidx.media3.ui.SubtitleView
import androidx.test.platform.app.InstrumentationRegistry
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.domain.model.SUBTITLE_BACKGROUND_BLACK_50
import org.mulletaflix.domain.model.SUBTITLE_BACKGROUND_BLACK_80
import org.mulletaflix.domain.model.SUBTITLE_BACKGROUND_NONE

@RunWith(AndroidJUnit4::class)
class SubtitleCaptionStyleTest {

    @Test
    fun selectedBackgroundIsPassedToMedia3WithoutChangingTextOrOutline() {
        val foreground = 0xFFFFFFFF.toInt()
        val edge = 0xFF000000.toInt()

        listOf(
            SUBTITLE_BACKGROUND_NONE to 0x00000000,
            SUBTITLE_BACKGROUND_BLACK_50 to 0x80000000.toInt(),
            SUBTITLE_BACKGROUND_BLACK_80 to 0xCC000000.toInt(),
        ).forEach { (mode, expectedBackground) ->
            val style = subtitleCaptionStyle(foreground, edge, mode)

            assertEquals(expectedBackground, style.backgroundColor)
            assertEquals(foreground, style.foregroundColor)
            assertEquals(edge, style.edgeColor)
        }
    }

    @Test
    fun userBackgroundOverridesFormattingEmbeddedInSubtitleCue() {
        val bitmap = Bitmap.createBitmap(640, 360, Bitmap.Config.ARGB_8888)
        InstrumentationRegistry.getInstrumentation().runOnMainSync {
            val context = InstrumentationRegistry.getInstrumentation().targetContext
            val cueText = SpannableString("Embedded style")
            cueText.setSpan(
                ForegroundColorSpan(Color.RED),
                0,
                cueText.length,
                SpannableString.SPAN_EXCLUSIVE_EXCLUSIVE,
            )
            cueText.setSpan(
                BackgroundColorSpan(Color.YELLOW),
                0,
                cueText.length,
                SpannableString.SPAN_EXCLUSIVE_EXCLUSIVE,
            )

            val view = SubtitleView(context)
            applyUserSubtitlePreferences(
                subtitleView = view,
                fontSizePercent = 100,
                style = subtitleCaptionStyle(
                    foregroundColor = Color.WHITE,
                    edgeColor = Color.BLACK,
                    backgroundCode = SUBTITLE_BACKGROUND_BLACK_80,
                ),
            )
            view.setCues(listOf(applyUserSubtitleColors(Cue.Builder().setText(cueText).build())))
            view.measure(
                android.view.View.MeasureSpec.makeMeasureSpec(bitmap.width, android.view.View.MeasureSpec.EXACTLY),
                android.view.View.MeasureSpec.makeMeasureSpec(bitmap.height, android.view.View.MeasureSpec.EXACTLY),
            )
            view.layout(0, 0, bitmap.width, bitmap.height)
            view.draw(Canvas(bitmap))
        }

        val pixels = IntArray(bitmap.width * bitmap.height)
        bitmap.getPixels(pixels, 0, bitmap.width, 0, 0, bitmap.width, bitmap.height)
        assertTrue("subtitle cue must render", pixels.any { Color.alpha(it) > 0 })
        assertFalse("embedded red text style must not override the user's text color", pixels.any { it == Color.RED })
        assertFalse("embedded yellow background must not override the user's backing", pixels.any { it == Color.YELLOW })
        assertTrue(
            "the selected 80% black background must render behind the cue",
            pixels.any { Color.alpha(it) == 0xCC && Color.red(it) == 0 && Color.green(it) == 0 && Color.blue(it) == 0 },
        )
        bitmap.recycle()
    }

    @Test
    fun filteringEmbeddedColorsPreservesEmphasisAndCueLayout() {
        val text = SpannableString("Keep bold and underline")
        text.setSpan(StyleSpan(Typeface.BOLD_ITALIC), 0, 9, Spanned.SPAN_EXCLUSIVE_EXCLUSIVE)
        text.setSpan(UnderlineSpan(), 10, text.length, Spanned.SPAN_EXCLUSIVE_EXCLUSIVE)
        text.setSpan(
            TextAppearanceSpan("serif", Typeface.BOLD, 18, ColorStateList.valueOf(Color.RED), null),
            10,
            text.length,
            Spanned.SPAN_EXCLUSIVE_EXCLUSIVE,
        )
        text.setSpan(ForegroundColorSpan(Color.RED), 0, text.length, Spanned.SPAN_EXCLUSIVE_EXCLUSIVE)
        text.setSpan(BackgroundColorSpan(Color.YELLOW), 0, text.length, Spanned.SPAN_EXCLUSIVE_EXCLUSIVE)
        val cue = Cue.Builder()
            .setText(text)
            .setPosition(0.35f)
            .setWindowColor(Color.YELLOW)
            .build()

        val sanitized = applyUserSubtitleColors(cue)
        val sanitizedText = sanitized.text as Spanned

        assertEquals(0.35f, sanitized.position)
        assertFalse(sanitized.windowColorSet)
        assertEquals(Typeface.BOLD_ITALIC, sanitizedText.getSpans(0, 9, StyleSpan::class.java).single().style)
        assertTrue(sanitizedText.getSpans(10, sanitizedText.length, UnderlineSpan::class.java).isNotEmpty())
        assertTrue(sanitizedText.getSpans(10, sanitizedText.length, TextAppearanceSpan::class.java).isEmpty())
        assertTrue(sanitizedText.getSpans(10, sanitizedText.length, TypefaceSpan::class.java).any { it.family == "serif" })
        assertTrue(sanitizedText.getSpans(10, sanitizedText.length, StyleSpan::class.java).any { it.style == Typeface.BOLD })
        assertTrue(sanitizedText.getSpans(0, sanitizedText.length, ForegroundColorSpan::class.java).isEmpty())
        assertTrue(sanitizedText.getSpans(0, sanitizedText.length, BackgroundColorSpan::class.java).isEmpty())

        val image = Bitmap.createBitmap(1, 1, Bitmap.Config.ARGB_8888)
        val bitmapCue = Cue.Builder().setBitmap(image)
            .setWindowColor(Color.YELLOW)
            .build()
        assertEquals(bitmapCue, applyUserSubtitleColors(bitmapCue))
        image.recycle()
    }
}
