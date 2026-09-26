package org.mulletaflix.domain.model

import java.io.File
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The accepted subtitle colours and the size range must be defined **once**.
 *
 * This is the class of bug that has recurred in this project more than any other: the
 * same value list kept by hand in two modules. It happened with theme enums, library
 * sort labels, language codes, download ids, settings option lists — and with subtitle
 * colours, which `SettingsRepositoryImpl` re-declared as a bare `setOf("WHITE",
 * "YELLOW", "CYAN")` and a literal `50..200`, so widening the range in the UI would
 * have left the storage layer silently truncating what was written.
 *
 * A source scan, because no runtime assertion can prove the absence of a second copy.
 * The working directory of a `:domain` test is that module, so the sibling modules are
 * reached through `..`.
 */
class SubtitleStyleSingleDefinitionTest {

    /** The Android project root: the test's working directory is this module. */
    private val projectRoot = File("..")

    private val storage = File(
        projectRoot,
        "data/src/main/java/org/mulletaflix/data/repository/SettingsRepositoryImpl.kt",
    )

    @Test
    fun `the storage layer does not re-declare the accepted colours`() {
        assertTrue(
            "SettingsRepositoryImpl.kt not found at ${storage.absolutePath}; " +
                "working dir is ${File(".").absolutePath}",
            storage.isFile,
        )

        val source = storage.readText()
        val offenders = subtitleColorCodes.filter { source.contains("\"$it\"") }

        assertTrue(
            "the storage layer must not list the subtitle colours a second time; use " +
                "normalizeSubtitleColor from this module instead. Found listed: $offenders",
            offenders.isEmpty(),
        )
    }

    @Test
    fun `the storage layer does not re-declare the size range`() {
        val source = storage.readText()

        assertTrue(
            "the storage layer must not repeat the subtitle size bounds; use " +
                "normalizeSubtitleSizePercent from this module instead. " +
                "Found coerceIn(50, 200): ${source.contains("coerceIn(50, 200)")}",
            !source.contains("coerceIn(50, 200)"),
        )
    }
}
