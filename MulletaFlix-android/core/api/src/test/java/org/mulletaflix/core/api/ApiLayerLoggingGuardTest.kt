package org.mulletaflix.core.api

import java.io.File
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Guard for the requirement "URLs, tokens, PINs and credentials must never
 * appear in production logs".
 *
 * Today the app has no logging surface at all, and the only OkHttp logging
 * interceptor is pinned to `Level.NONE`. Both facts are one commit away from
 * regressing, so this test fails the build if a logging call appears in the
 * API layer.
 *
 * A source scan is used deliberately: there is no runtime assertion that can
 * prove the *absence* of a call site.
 */
class ApiLayerLoggingGuardTest {

    /** Directory of this module's production sources, resolved from the test's working dir. */
    private val sourceRoots = listOf(
        File("src/main/java"),
        File("src/main/kotlin"),
    )

    private val forbiddenPatterns = listOf(
        Regex("""\bandroid\.util\.Log\b"""),
        Regex("""(?<![\w.])Log\.[dviwe]\s*\("""),
        Regex("""(?<![\w.])println\s*\("""),
        Regex("""\bprintStackTrace\s*\("""),
        Regex("""\bTimber\.""") ,
        Regex("""\bSystem\.(out|err)\b"""),
    )

    private fun productionSources(): List<File> = sourceRoots
        .filter { it.isDirectory }
        .flatMap { root -> root.walkTopDown().filter { it.isFile && it.extension == "kt" }.toList() }

    @Test
    fun `production sources expose no logging surface`() {
        val sources = productionSources()
        assertTrue(
            "expected to find production sources under ${sourceRoots.map(File::getPath)}; " +
                "working dir is ${File(".").absolutePath}",
            sources.isNotEmpty(),
        )

        val offenders = sources.flatMap { file ->
            file.readLines().withIndex().flatMap { (index, line) ->
                forbiddenPatterns
                    .filter { it.containsMatchIn(line) }
                    .map { "${file.path}:${index + 1}: ${line.trim()}" }
            }
        }

        assertTrue(
            "logging calls found in production sources; URLs, tokens and PINs could leak:\n" +
                offenders.joinToString("\n"),
            offenders.isEmpty(),
        )
    }

    @Test
    fun `the okhttp logger stays disabled for every build type`() {
        val networkModule = productionSources()
            .firstOrNull { it.name == "NetworkModule.kt" }
            ?: error("NetworkModule.kt not found under ${sourceRoots.map(File::getPath)}")

        val source = networkModule.readText()
        assertTrue(
            "NetworkModule must still configure HttpLoggingInterceptor",
            source.contains("HttpLoggingInterceptor"),
        )
        assertTrue(
            "the OkHttp logger must be Level.NONE; BODY or HEADERS would print the " +
                "Authorization header and api_key query strings",
            source.contains("HttpLoggingInterceptor.Level.NONE"),
        )
        assertTrue(
            "the logger must not be gated on BuildConfig.DEBUG, because media URLs " +
                "carry api_key tokens even in debug builds",
            !source.contains("BuildConfig.DEBUG"),
        )
    }
}
