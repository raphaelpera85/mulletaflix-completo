package org.mulletaflix.android

import java.io.File
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Guard for the requirement "URLs, tokens, PINs and credentials must never appear
 * in production logs" — across the **whole** app, not one module.
 *
 * `ApiLayerLoggingGuardTest` already scans `:core:api`, which was where the OkHttp
 * logger lived. That left every other module unguarded, and one was already using
 * the logging surface: `:design-system` prints the resolved artwork URL, which is
 * the one URL in the app that carries a session token as a query parameter. The
 * narrow guard could not see it and passed.
 *
 * The exception is kept, because the artwork URL is the only way to tell an
 * authenticated image request from an anonymous one, which is the diagnosed cause
 * of the slow covers. It is pinned instead: exactly one file may log, it must be
 * gated on a debuggable build, and it must pass the URL through `redactToken`.
 *
 * A source scan is used deliberately: no runtime assertion can prove the *absence*
 * of a call site.
 */
class ProductionLoggingSurfaceGuardTest {

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

    /**
     * The single file allowed to write to logcat, relative to the project root.
     *
     * Adding a second entry is a decision, not a formality: it has to be paired with
     * a debuggable check and a redaction, and the tests below enforce both for this
     * one.
     */
    private val allowedLoggingFile =
        "design-system/src/main/java/org/mulletaflix/designsystem/media/MediaImageUrl.kt"

    private val forbiddenPatterns = listOf(
        Regex("""\bandroid\.util\.Log\b"""),
        Regex("""(?<![\w.])Log\.[dviwe]\s*\("""),
        Regex("""(?<![\w.])println\s*\("""),
        Regex("""\bprintStackTrace\s*\("""),
        Regex("""\bTimber\."""),
        Regex("""\bSystem\.(out|err)\b"""),
    )

    private fun mainSources(module: String): List<File> =
        File(projectRoot, "$module/src/main/java")
            .takeIf { it.isDirectory }
            ?.walkTopDown()
            ?.filter { it.isFile && it.extension == "kt" }
            ?.toList()
            .orEmpty()

    /** Path relative to the Android project root, with `/` separators. */
    private fun fileNameOf(file: File): String =
        file.canonicalFile
            .toRelativeString(projectRoot.canonicalFile)
            .replace('\\', '/')

    @Test
    fun `no module outside the single exception writes to a log`() {
        val scanned = modules.flatMap { mainSources(it) }
        assertTrue(
            "expected to find production sources under ${projectRoot.absolutePath}; " +
                "working dir is ${File(".").absolutePath}",
            scanned.size > 50,
        )

        val offenders = scanned
            .filterNot { fileNameOf(it) == allowedLoggingFile }
            .flatMap { file ->
                file.readLines().withIndex().flatMap { (index, line) ->
                    forbiddenPatterns
                        .filter { it.containsMatchIn(line) }
                        .map { "${file.path}:${index + 1}: ${line.trim()}" }
                }
            }

        assertTrue(
            "a logging call appeared outside the single reviewed exception; URLs, " +
                "tokens and PINs could reach logcat or a bug report. Either remove it " +
                "or make it a deliberate, redacted, debug-only exception and add it to " +
                "`allowedLoggingFile`:\n" + offenders.joinToString("\n"),
            offenders.isEmpty(),
        )
    }

    @Test
    fun `the one file allowed to log does so only behind a debuggable check and redaction`() {
        val exception = File(projectRoot, allowedLoggingFile)
        assertTrue(
            "the allow-listed file moved; update `allowedLoggingFile`: " +
                exception.absolutePath,
            exception.isFile,
        )

        val source = exception.readText()
        val logLine = Regex("""\bLog\.[dviwe]\s*\(""")
        val logLines = source.lines().filter { logLine.containsMatchIn(it) }

        assertTrue(
            "the allow-list is stale: $allowedLoggingFile no longer writes to logcat. " +
                "Remove it from `allowedLoggingFile` instead of leaving a hole.",
            logLines.isNotEmpty(),
        )
        assertTrue(
            "the artwork diagnostic must stay gated on a debuggable build, or a release " +
                "APK writes the resolved URL to logcat",
            source.contains("if (!isDebuggableApp()) return"),
        )
        logLines.forEach { line ->
            assertTrue(
                "every log line must pass its URL through `redactToken`, because the " +
                    "artwork URL carries the session token as `api_key`: $line",
                line.contains("redactToken("),
            )
        }
    }

    @Test
    fun `every okhttp logger is pinned to none`() {
        val usages = modules.flatMap { mainSources(it) }.flatMap { file ->
            file.readLines().withIndex()
                .filter { (_, line) -> line.contains("HttpLoggingInterceptor.Level.") }
                .map { (index, line) -> "${file.path}:${index + 1}: ${line.trim()}" }
        }

        assertTrue(
            "expected to find the OkHttp logger configuration",
            usages.isNotEmpty(),
        )
        val permissive = usages.filterNot { it.contains("HttpLoggingInterceptor.Level.NONE") }
        assertTrue(
            "BODY or HEADERS prints the Authorization header and every `api_key` query " +
                "string, and BASIC prints the URL of every request — including the " +
                "artwork and playback URLs, which carry the session token. Every " +
                "HttpLoggingInterceptor must be Level.NONE:\n" +
                permissive.joinToString("\n"),
            permissive.isEmpty(),
        )
    }
}
