package org.mulletaflix.feature.player

/**
 * Who the media-session bridge is allowed to tear down.
 *
 * The bridge exists to *lend* the screen's player to the playback service. It never
 * creates a player, so it must never destroy one: the bridge used to call
 * `player.release()` on whatever it was holding. Every caller already owns its own
 * player — `PlayerViewModel.onCleared` releases `player` and `localPlayer`, and
 * `MulletaFlixPlaybackService` releases the fallback it built — so the bridge's
 * release was redundant in the ordinary path and destructive when `attach` ran with a
 * new player while the previous owner was still alive, because it destroyed a player
 * another component still held and would still hand to Media3.
 *
 * The session is different: the bridge is the only thing that builds it, so it is the
 * bridge's to release.
 *
 * Kept as a separate decision from the state machine so the rule can be asserted
 * without a device: `MediaSession` is final and the bridge is a singleton, so the
 * behaviour itself cannot be reached from a JVM test.
 */
internal fun shouldReleaseSession(uiOwner: Boolean, serviceOwner: Boolean): Boolean =
    !uiOwner && !serviceOwner

/**
 * Whether the bridge may release the player it is holding.
 *
 * Always `false`. It is a function rather than a comment so that a future change that
 * decides the bridge should own the player has to say so here, where a test reads it,
 * instead of adding `player.release()` back into `releaseIfUnused`.
 */
internal fun shouldReleasePlayer(): Boolean = false
