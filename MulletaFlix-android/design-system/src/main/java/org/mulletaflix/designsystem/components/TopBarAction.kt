package org.mulletaflix.designsystem.components

import android.content.res.Configuration
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.tween
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.IconButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.alpha
import androidx.compose.ui.draw.scale
import androidx.compose.ui.focus.onFocusChanged
import androidx.compose.ui.graphics.Shape
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.disabled
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import org.mulletaflix.designsystem.theme.MulletaFlixRed

/**
 * Whether the current layout is an Android TV (10-foot) surface.
 *
 * Seven screens used to repeat this expression verbatim. One copy means a screen
 * cannot silently disagree about what a TV is.
 */
@Composable
fun isTelevisionDevice(): Boolean =
    (LocalConfiguration.current.uiMode and Configuration.UI_MODE_TYPE_MASK) ==
        Configuration.UI_MODE_TYPE_TELEVISION

/**
 * Focus treatment for top-bar actions on a remote.
 *
 * Reported by the user as "a navegacao na tv nos menus superiores nao mostra em qual
 * icone esta", answered with "todos": a plain `IconButton` is focusable, so the D-pad
 * already moved across every top bar in the app, but each icon kept a fixed `tint`
 * and nothing was drawn around it. Focus was invisible everywhere, not just on Home.
 *
 * Measured on the TV emulator before this existed: the focused action and an
 * unfocused one were byte-identical in a pixel capture.
 */

/** Small focus lift, matching the one media cards use. */
internal fun topBarActionFocusScale(focusFriendly: Boolean, isFocused: Boolean): Float =
    if (focusFriendly && isFocused) 1.15f else 1f

/** Width of the focus ring; zero on touch devices, where it would be permanent noise. */
internal fun topBarActionFocusRingWidthDp(focusFriendly: Boolean, isFocused: Boolean): Float =
    if (focusFriendly && isFocused) 2f else 0f

/**
 * Alpha of the disc drawn behind a focused action.
 *
 * A top bar sits directly on the artwork or a gradient, so a ring alone can vanish
 * against a bright backdrop. The tinted disc guarantees contrast.
 */
internal fun topBarActionFocusBackgroundAlpha(focusFriendly: Boolean, isFocused: Boolean): Float =
    if (focusFriendly && isFocused) 0.20f else 0f

/**
 * Whether a top-bar action may act right now.
 *
 * `enabled` is the caller's own gate (nothing to refresh); `busy` is a request
 * already running. Both must be respected, but they must not be expressed the same
 * way — see [MulletaFlixTopBarAction].
 */
internal fun topBarActionAcceptsInput(enabled: Boolean, busy: Boolean): Boolean = enabled && !busy

/**
 * How opaque a top-bar action draws.
 *
 * `enabled` fades the action because there is nothing to act on. `busy` deliberately
 * does **not**: the action is working, and a faded icon is what the user reported as
 * "um simbolo de atualizar o tempo todo" — a refresh button that looks dead.
 *
 * The decision is a function, rather than an inline `if`, because the render check for
 * it is not stable: faded white over the theme's surface sits close enough to a usual
 * brightness threshold that counting "bright pixels" could not separate the two states
 * reliably. The composable's own enabled/disabled fade is covered by a pixel assertion;
 * this covers which input causes it.
 */
internal fun topBarActionContentAlpha(enabled: Boolean): Float =
    if (enabled) 1f else TOP_BAR_ACTION_DISABLED_ALPHA

/**
 * A ring and disc that appear when a remote's focus lands on this element.
 *
 * For surfaces that are not top-bar actions — a tab, a settings row, a list
 * entry. Material's default focus indication is a translucent state layer, which
 * measured on the TV emulator is a barely visible dark wash: the login screen's
 * "Entrar"/"Quick Connect" tabs were indistinguishable from the unfocused one.
 *
 * Callers that also want the focus lift add `.scale(...)` themselves.
 */
@Composable
fun Modifier.remoteFocusRing(
    shape: Shape = CircleShape,
    focusFriendly: Boolean = isTelevisionDevice(),
): Modifier {
    var isFocused by remember { mutableStateOf(false) }
    val ringWidthDp = topBarActionFocusRingWidthDp(focusFriendly, isFocused)
    val discAlpha = topBarActionFocusBackgroundAlpha(focusFriendly, isFocused)

    return this
        .onFocusChanged { isFocused = it.isFocused }
        .then(
            if (ringWidthDp > 0f) {
                Modifier
                    .background(MulletaFlixRed.copy(alpha = discAlpha), shape)
                    .border(ringWidthDp.dp, MulletaFlixRed, shape)
            } else {
                Modifier
            },
        )
}

/**
 * A top-bar action (navigation icon or action) that shows where the remote's focus
 * is and stays reachable while it is working.
 *
 * Built from a clickable `Box` rather than `IconButton` on purpose. Measured on the
 * TV emulator: any composable whose `clickable` is disabled drops the `RequestFocus`
 * action, so there is no way to keep a *busy* action in the D-pad sequence while
 * using a disabled `IconButton` — and a busy action has to stay reachable, because
 * these screens refresh on a foreground timer while the user is standing on the icon.
 * The two gates are therefore kept apart: `enabled` removes the action from the
 * sequence (there is nothing to act on), `busy` keeps it there and only swallows the
 * click.
 *
 * @param focusFriendly defaults to whether this is a TV, so no call site has to
 *   remember to pass it and none can silently opt out of the focus ring.
 * @param busy a request for this action is already running. Reported by the user as
 *   "enquanto em um dos icones fica um simbolo de atualizar o tempo todo no meio da
 *   tela": Live TV, Biblioteca, Minha Lista and SyncPlay all passed
 *   `enabled = !state.isLoading` to their refresh action. That both dimmed the icon
 *   into something that reads as a broken, frozen button (it is a static glyph, so it
 *   never animates) and removed it from the focus order mid-navigation.
 *
 * @param busyContentDescription label the spinner carries, so the action keeps the
 *   same name for a screen reader while it works instead of losing its description.
 */
@Composable
fun MulletaFlixTopBarAction(
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    enabled: Boolean = true,
    busy: Boolean = false,
    focusFriendly: Boolean = isTelevisionDevice(),
    busyContentDescription: String? = null,
    content: @Composable () -> Unit,
) {
    var isFocused by remember { mutableStateOf(false) }
    val focusScale by animateFloatAsState(
        targetValue = topBarActionFocusScale(focusFriendly, isFocused),
        animationSpec = tween(durationMillis = 120),
        label = "top-bar-action-focus-scale",
    )
    val ringWidthDp = topBarActionFocusRingWidthDp(focusFriendly, isFocused)
    val discAlpha = topBarActionFocusBackgroundAlpha(focusFriendly, isFocused)
    val acceptsInput = topBarActionAcceptsInput(enabled, busy)

    // No wrapper: the action *is* an `IconButton`. Every wrapper tried here changed its
    // size. A fixed 48 dp box made a 48 dp action where the platform's own measures
    // 40 dp, and with `propagateMinConstraints = true` it also drew the glyph at 32 dp
    // instead of 24 dp — the user's "os icones do menu superior estao muito grandes no
    // celular". The focus ring is drawn inside the button's own box, so it adds no
    // size; `scale` is visual only. `modifier` is applied *after* the tag so a caller's
    // tag wins, and the component's own tag survives for the default case.
    IconButton(
        onClick = { if (acceptsInput) onClick() },
        // Always enabled, with the gate in `onClick`: `clickable(enabled = false)`
        // *removes* the `RequestFocus` action, which measured on the TV emulator drops
        // the action out of the D-pad sequence. A busy action has to stay reachable,
        // because these screens refresh on a foreground timer while the user is
        // standing on the icon.
        enabled = true,
        modifier = Modifier
            .testTag(TOP_BAR_ACTION_TEST_TAG)
            .scale(focusScale)
            .onFocusChanged { isFocused = it.isFocused }
            .then(
                if (ringWidthDp > 0f) {
                    Modifier
                        .background(MulletaFlixRed.copy(alpha = discAlpha), CircleShape)
                        .border(ringWidthDp.dp, MulletaFlixRed, CircleShape)
                } else {
                    Modifier
                },
            )
            // `enabled` has to *look* disabled, or "there is nothing to act on" becomes
            // indistinguishable from an available action — measured: enabled and disabled
            // states were pixel-identical after the wrapper box was removed. This does
            // **not** apply to a busy action, which must stay at full strength and show
            // its spinner; dimming it is the defect the user reported as "um simbolo de
            // atualizar o tempo todo".
            .alpha(topBarActionContentAlpha(enabled))
            // `enabled = true` é deliberado (o D-pad precisa do nó na sequência), mas o
            // alpha é um sinal puramente visual: o leitor de tela anunciava "botão" e a
            // ativação não fazia nada, sem explicação. `disabled()` diz aos serviços o
            // que a cor já diz a quem enxerga. Um pedido em andamento não é
            // indisponibilidade — ali o spinner e a descrição de "ocupado" é que falam.
            .then(
                if (!acceptsInput && !busy) {
                    Modifier.semantics { disabled() }
                } else {
                    Modifier
                },
            )
            .then(modifier),
        content = {
            if (busy) {
                CircularProgressIndicator(
                    modifier = Modifier
                        .size(TOP_BAR_ACTION_ICON_SIZE_DP.dp)
                        .then(
                            if (busyContentDescription == null) {
                                Modifier
                            } else {
                                Modifier.semantics { contentDescription = busyContentDescription }
                            },
                        ),
                    strokeWidth = 2.dp,
                )
            } else {
                IconSlot(content)
            }
        },
    )
}

/**
 * Holds the caller's icon at the Material icon size.
 *
 * Measured: without this the glyph is whatever the caller asked for *stretched to the
 * interactive target* — a `MulletaFlixTopBarAction` drew a 48 dp glyph where a bare
 * Material `Icon` draws 24 dp.
 */
@Composable
private fun IconSlot(content: @Composable () -> Unit) {
    Box(
        modifier = Modifier.size(TOP_BAR_ACTION_ICON_SIZE_DP.dp),
        contentAlignment = Alignment.Center,
        propagateMinConstraints = true,
    ) {
        content()
    }
}

/**
 * Size of the icon a top-bar action draws.
 *
 * Material's icon size. Kept as a named constant so the measurement in
 * `TopBarActionIconSizeTest` and the component cannot drift apart.
 */
internal const val TOP_BAR_ACTION_ICON_SIZE_DP = 24

/** Material's minimum interactive size. */
internal const val TOP_BAR_ACTION_SIZE_DP = 48

/**
 * How much a genuinely disabled action fades.
 *
 * Material's disabled content alpha. Only `enabled` uses it: a *busy* action stays at
 * full strength, because it is working rather than unavailable.
 */
internal const val TOP_BAR_ACTION_DISABLED_ALPHA = 0.38f

/**
 * Identifies the *action* rather than its artwork.
 *
 * The content description belongs to the icon inside, and the spinner carries the
 * same label while busy, so a test that selected by description resolved to a child
 * and measured the wrong node.
 */
internal const val TOP_BAR_ACTION_TEST_TAG = "mulletaflix-top-bar-action"
