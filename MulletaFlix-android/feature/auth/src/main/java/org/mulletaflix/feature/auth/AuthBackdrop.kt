package org.mulletaflix.feature.auth

import androidx.compose.ui.graphics.Color

/**
 * The cinematic backdrop shared by the server-selection and login screens.
 *
 * It is a vertical gradient, so [AuthBackdropTop] is the *lightest* end and
 * therefore the worst case for contrast measurement: dim text has less
 * contrast on a lighter background. Pass [AuthBackdropTop] as the background
 * when lifting dim text drawn directly over the backdrop.
 */
internal val AuthBackdropTop = Color(0xFF1A0507)
internal val AuthBackdropBottom = Color(0xFF080808)
