package org.mulletaflix.feature.livetv

/**
 * What the EPG dialog body shows.
 *
 * Split out from the composable so the decision can be asserted without a device,
 * because it encodes two behaviours that were wrong:
 *
 *  - an error replaced the programme list entirely (`guideError != null -> Text(...)`),
 *    so a failed *refresh* threw away a guide that was already on screen;
 *  - a reload in flight had no programmes to show, so a transient failure left the
 *    dialog reading "Nenhum programa encontrado para as próximas 24 horas." and the
 *    only way out was to close and reopen it.
 */
internal enum class GuideBody {
    /** Nothing to show and a request is running. */
    LOADING,

    /** Nothing to show and nothing running: the guide really is empty. */
    EMPTY,

    /** There are programmes; they stay visible through reloads and errors. */
    PROGRAMMES,
}

/**
 * Programmes already on screen always win.
 *
 * A refresh or a failed retry must never blank content the user can already read.
 */
internal fun guideBody(isLoadingGuide: Boolean, programmeCount: Int): GuideBody = when {
    programmeCount > 0 -> GuideBody.PROGRAMMES
    isLoadingGuide -> GuideBody.LOADING
    else -> GuideBody.EMPTY
}
