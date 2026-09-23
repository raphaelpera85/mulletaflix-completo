package org.mulletaflix.android

import java.io.File
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Guard for the reported defect "usando na tv tenho que clicar 2x para entrar em
 * qualquer biblioteca ou midia".
 *
 * A Compose node that had **both** `Modifier.focusable()` and `Modifier.clickable`
 * carried two focus targets. On a remote the centre key went to the target without
 * the activation handler, so the first press was swallowed. Measured on the TV
 * emulator with one centre press on a focused `MediaCard`: 0 activations before the
 * fix, 1 after.
 *
 * Five screens had hand-rolled the pair next to their own `clickable` — the library
 * list rows, the Live TV channel rows, the Live TV recording rows, the search
 * history rows and the offline download rows — so the whole app needed two clicks.
 *
 * The invariant that prevents a repeat: a focus target is declared **only** in
 * `:design-system`, which owns the components that get this right. A screen that
 * needs a remote-activatable element uses `clickable` (which brings its own focus
 * target) or a design-system component. This is a source scan because no runtime
 * assertion can prove the absence of a call site.
 */
class RemoteFocusTargetGuardTest {

    /** The Android project root: the test's working directory is this module. */
    private val projectRoot = File("..")

    /** Modules whose `src/main` is scanned. */
    private val modules = listOf(
        "app",
        "core/api",
        "core/common",
        "data",
        "domain",
        "design-system",
        "feature/auth",
        "feature/downloads",
        "feature/home",
        "feature/item-detail",
        "feature/library",
        "feature/live-tv",
        "feature/player",
        "feature/search",
        "feature/settings",
        "feature/sync-play",
        "feature/user",
    )

    /** `:design-system` is allowed to declare focus targets; nothing else is. */
    private val focusTargetOwners = setOf("design-system")

    private fun mainSources(module: String): List<File> =
        File(projectRoot, "$module/src/main/java")
            .takeIf { it.isDirectory }
            ?.walkTopDown()
            ?.filter { it.isFile && it.extension == "kt" }
            ?.toList()
            .orEmpty()

    @Test
    fun `only the design system declares its own focus targets`() {
        val scanned = modules.flatMap { mainSources(it) }
        assertTrue(
            "expected to find production sources under ${projectRoot.absolutePath}; " +
                "working dir is ${File(".").absolutePath}",
            scanned.size > 50,
        )

        val offenders = modules
            .filterNot { it in focusTargetOwners }
            .flatMap { module ->
                mainSources(module).flatMap { file ->
                    file.readLines().withIndex()
                        .filter { (_, line) -> line.contains(".focusable()") }
                        .map { (index, line) ->
                            "${file.path}:${index + 1}: ${line.trim()}"
                        }
                }
            }

        assertTrue(
            "a screen must not declare its own focus target: placed next to a " +
                "`clickable` on the same node it creates a second focus target and " +
                "the remote needs two presses to activate the element. Use " +
                "`clickable` or a :design-system component instead:\n" +
                offenders.joinToString("\n"),
            offenders.isEmpty(),
        )
    }

    @Test
    fun `a clickable card is not given a second focus target`() {
        // Pins the exact combination, in case the invariant above is ever relaxed.
        val mediaCard = File(
            projectRoot,
            "design-system/src/main/java/org/mulletaflix/designsystem/components/MediaCard.kt",
        )
        assertTrue("MediaCard.kt not found at ${mediaCard.absolutePath}", mediaCard.isFile)

        val source = mediaCard.readText()
        assertTrue(
            "a clickable MediaCard must rely on `clickable` for focus; the explicit " +
                "`focusable()` must stay conditional on the card not being clickable",
            source.contains("if (isClickable) Modifier else Modifier.focusable()"),
        )
    }

    /**
     * The same defect's second half, measured in v1.2.58.
     *
     * A `RadioButton` that carries its own `onClick` is a second focus target when it
     * sits inside a row that is itself clickable or selectable. Measured on the TV
     * emulator (`RadioOptionFocusTargetTest`, `:design-system`): that shape exposes
     * **2** focus targets per option, while `RadioButton(onClick = null)` inside a
     * `selectable` row exposes **1**. Two targets is the reported "clicar 2x" — the
     * remote lands on the row, and only the second press reaches the activation.
     *
     * The scan is deliberately narrow: `onClick = null` — the correct form, used by
     * every menu in `:feature:player` — does not contain `onClick = ` at all on the
     * same line as `RadioButton(`, so it cannot trip this.
     */
    @Test
    fun `no radio button carries its own activation`() {
        val offenders = modules.flatMap { module ->
            mainSources(module).flatMap { file ->
                file.readLines().withIndex()
                    .filter { (_, line) -> line.contains("RadioButton(") && line.contains("onClick") }
                    .filterNot { (_, line) -> line.contains("onClick = null") }
                    .map { (index, line) -> "${file.path}:${index + 1}: ${line.trim()}" }
            }
        }

        assertTrue(
            "a `RadioButton` with its own `onClick` adds a second focus target when it " +
                "sits inside a clickable or selectable row, so the remote needs two " +
                "presses to pick the option. Put the action on the row " +
                "(`selectable(role = Role.RadioButton)`) and pass `onClick = null` to the " +
                "button:\n" + offenders.joinToString("\n"),
            offenders.isEmpty(),
        )
    }
}
