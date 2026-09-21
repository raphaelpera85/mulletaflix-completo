package org.mulletaflix.android.navigation

import android.net.Uri
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Assert.assertEquals
import org.junit.Test
import org.junit.runner.RunWith

/**
 * The decoding half of the offline-player route contract.
 *
 * Navigation matches query arguments with `Uri.getQueryParameters`, which
 * follows RFC 3986 and does **not** convert `+` into a space. The route builder
 * used to encode with `URLEncoder`, so every offline title containing a space
 * reached the player OSD verbatim as `O+Retorno+de+Jedi`.
 *
 * This lives in `androidTest` because `android.net.Uri` is stubbed in JVM unit
 * tests (`unitTests.isReturnDefaultValues = true`), where `Uri.encode` returns
 * null and the assertion would prove nothing.
 */
@RunWith(AndroidJUnit4::class)
class OfflineRouteDecodingTest {

    private fun decode(route: String, key: String): List<String> =
        Uri.parse(route).getQueryParameters(key)

    @Test
    fun offlineTitleWithSpacesDecodesBackToTheOriginal() {
        val title = "O Retorno de Jedi"
        val route = MulletaFlixRoute.offlinePlayer(
            itemId = "movie 1",
            uri = "content://media/external/video/42",
            title = title,
        )

        assertEquals(listOf(title), decode(route, "title"))
    }

    @Test
    fun offlineItemIdWithSpacesDecodesBackToTheOriginal() {
        val route = MulletaFlixRoute.offlinePlayer(
            itemId = "downloaded 1",
            uri = "content://media/external/video/42",
            title = "Filme",
        )

        assertEquals(listOf("downloaded 1"), decode(route, "itemId").ifEmpty {
            // The id is a path argument, so read it from the path instead.
            listOf(Uri.parse(route).pathSegments.last())
        })
    }

    @Test
    fun offlineUriWithItsOwnQueryAndSpacesDecodesBackToTheOriginal() {
        val uri = "content://media/external/video/42?token=a b&other=1"
        val route = MulletaFlixRoute.offlinePlayer(
            itemId = "movie-1",
            uri = uri,
            title = "Filme",
        )

        assertEquals(listOf(uri), decode(route, "uri"))
    }

    @Test
    fun offlineTitleWithSpecialCharactersDecodesBackToTheOriginal() {
        listOf(
            "Movie & Show",
            "50% de desconto",
            "Pergunta? Sim!",
            "A/B: o retorno",
            "Ação & Aventura",
        ).forEach { title ->
            val route = MulletaFlixRoute.offlinePlayer(
                itemId = "movie-1",
                uri = "content://media/external/video/42",
                title = title,
            )
            assertEquals(
                "title <$title> did not survive the route",
                listOf(title),
                decode(route, "title"),
            )
        }
    }
}
