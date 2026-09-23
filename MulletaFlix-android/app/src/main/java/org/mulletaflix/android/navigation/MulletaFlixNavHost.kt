package org.mulletaflix.android.navigation

import androidx.compose.animation.AnimatedContentTransitionScope
import androidx.compose.animation.core.tween
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.navigation.NavHostController
import androidx.navigation.NavType
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.currentBackStackEntryAsState
import androidx.navigation.compose.rememberNavController
import androidx.navigation.navArgument
import androidx.media3.common.util.UnstableApi
import android.net.Uri
import org.mulletaflix.android.MediaDeepLinkRequest
import org.mulletaflix.feature.auth.LoginScreen
import org.mulletaflix.feature.auth.ServerSelectionScreen
import org.mulletaflix.feature.home.HomeScreen
import org.mulletaflix.feature.library.LibraryScreen
import org.mulletaflix.feature.library.FavoritesScreen
import org.mulletaflix.feature.itemdetail.ItemDetailScreen
import org.mulletaflix.feature.player.VideoPlayerScreen
import org.mulletaflix.feature.search.SearchScreen
import org.mulletaflix.feature.settings.SettingsScreen
import org.mulletaflix.feature.user.ProfileScreen
import org.mulletaflix.feature.livetv.LiveTvScreen
import org.mulletaflix.feature.downloads.DownloadsScreen
import org.mulletaflix.feature.syncplay.SyncPlayScreen

/**
 * Root navigation host for MulletaFlix.
 *
 * Routes:
 *   auth/server-selection   → Pick/add a server
 *   auth/login              → Login screen
 *   main/home               → Home (start destination after auth)
 *   main/library/{libId}    → Library browser
 *   main/search             → Universal search
 *   main/downloads          → Offline downloads
 *   main/live-tv            → Live TV / EPG
 *   main/settings           → Settings
 *   main/profile            → User profile
 *   detail/{itemId}         → Item detail (movie/series/music/book)
 *   player/video/{itemId}   → Full-screen video player
 */
@Composable
@UnstableApi
fun MulletaFlixNavHost(
    navController: NavHostController = rememberNavController(),
    startDestination: String = MulletaFlixRoute.SERVER_SELECTION,
    deepLinkRequest: MediaDeepLinkRequest? = null,
    onDeepLinkConsumed: () -> Unit = {},
) {
    val currentBackStackEntry by navController.currentBackStackEntryAsState()
    val currentRoute = currentBackStackEntry?.destination?.route
    val currentItemId = currentBackStackEntry?.arguments?.getString("itemId")
    val currentServerId = org.mulletaflix.designsystem.media.LocalMulletaFlixServerId.current
    // Keyed by request sequence rather than by item id, so a second intent for
    // the same media is a new delivery instead of a duplicate.
    var handledDeepLinkSequence by remember { mutableStateOf<Long?>(null) }
    val deepLinkItemId = deepLinkRequest?.itemId
    // True while the user is deliberately changing servers from the profile
    // screen. Both auth screens auto-advance past themselves when a session
    // already exists, which is right for the launch destination and wrong here:
    // the server list sent the user to login, login sent them to Home, and
    // "Trocar de Servidor" looked like a dead button.
    var switchingServer by remember { mutableStateOf(false) }

    LaunchedEffect(deepLinkRequest, currentRoute, currentItemId, currentServerId) {
        if (!shouldDeliverMediaDeepLink(deepLinkRequest?.sequence, handledDeepLinkSequence, deepLinkItemId)) {
            return@LaunchedEffect
        }
        val request = deepLinkRequest ?: return@LaunchedEffect
        val targetItemId = request.itemId
        if (shouldRedirectToServerSelectionForDeepLinkMismatch(currentRoute, request.serverId, currentServerId)) {
            // Keep the request alive. Server selection verifies the endpoint,
            // clears the old session when its server id differs, and the login
            // callback then resumes this exact item id.
            switchingServer = true
            navController.navigate(MulletaFlixRoute.SERVER_SELECTION) {
                popUpTo(MulletaFlixRoute.HOME) { inclusive = true }
                launchSingleTop = true
            }
            return@LaunchedEffect
        }
        if (shouldMarkMediaDeepLinkHandled(currentItemId, targetItemId)) {
            // The destination is already visible; the link is satisfied.
            handledDeepLinkSequence = request.sequence
            onDeepLinkConsumed()
            return@LaunchedEffect
        }
        val targetRoute = MulletaFlixRoute.itemDetail(targetItemId)
        if (shouldNavigateToMediaDeepLink(currentRoute, currentItemId, targetItemId)) {
            navController.navigate(targetRoute) {
                launchSingleTop = true
            }
            handledDeepLinkSequence = request.sequence
            onDeepLinkConsumed()
        }
    }

    NavHost(
        navController = navController,
        startDestination = startDestination,
        enterTransition = {
            slideIntoContainer(
                AnimatedContentTransitionScope.SlideDirection.Start,
                tween(300)
            )
        },
        exitTransition = {
            slideOutOfContainer(
                AnimatedContentTransitionScope.SlideDirection.Start,
                tween(300)
            )
        },
        popEnterTransition = {
            slideIntoContainer(
                AnimatedContentTransitionScope.SlideDirection.End,
                tween(300)
            )
        },
        popExitTransition = {
            slideOutOfContainer(
                AnimatedContentTransitionScope.SlideDirection.End,
                tween(300)
            )
        }
    ) {
        // Auth flow
        composable(MulletaFlixRoute.SERVER_SELECTION) {
            ServerSelectionScreen(
                onServerSelected = { navController.navigate(MulletaFlixRoute.LOGIN) },
                switchingServer = switchingServer,
            )
        }

        composable(MulletaFlixRoute.LOGIN) {
            LoginScreen(
                switchingServer = switchingServer,
                onLoginSuccess = {
                    // The pending link survives the login detour and is cleared
                    // by the LaunchedEffect once its destination is reached.
                    val destination = deepLinkItemId?.let(MulletaFlixRoute::itemDetail)
                        ?: MulletaFlixRoute.HOME
                    switchingServer = false
                    navController.navigate(destination) {
                        popUpTo(MulletaFlixRoute.SERVER_SELECTION) { inclusive = true }
                    }
                }
            )
        }

        // Main app
        composable(MulletaFlixRoute.HOME) {
            HomeScreen(
                onItemClick = { itemId ->
                    navController.navigate(MulletaFlixRoute.itemDetail(itemId))
                },
                onLibraryClick = { libId ->
                    navController.navigate(MulletaFlixRoute.library(libId))
                },
                onLiveTvClick = { navController.navigate(MulletaFlixRoute.LIVE_TV) },
                navController = navController
            )
        }

        composable(MulletaFlixRoute.FAVORITES) {
            FavoritesScreen(
                onItemClick = { itemId -> navController.navigate(MulletaFlixRoute.itemDetail(itemId)) },
                onBack = { navController.popBackStack() },
            )
        }

        composable(
            route = MulletaFlixRoute.LIBRARY,
            arguments = listOf(navArgument("libId") { type = NavType.StringType })
        ) { backStack ->
            val libId = backStack.arguments?.getString("libId") ?: ""
            LibraryScreen(
                libraryId = libId,
                onItemClick = { itemId ->
                    navController.navigate(MulletaFlixRoute.itemDetail(itemId))
                },
                onBack = { navController.popBackStack() }
            )
        }

        composable(MulletaFlixRoute.SEARCH) {
            SearchScreen(
                onItemClick = { itemId ->
                    navController.navigate(MulletaFlixRoute.itemDetail(itemId))
                },
                onBack = { navController.popBackStack() },
            )
        }

        composable(MulletaFlixRoute.DOWNLOADS) {
            DownloadsScreen(
                onItemClick = { entry ->
                    navController.navigate(MulletaFlixRoute.offlinePlayer(entry.id, entry.uri, entry.title))
                },
                onBack = { navController.popBackStack() },
                onExploreClick = {
                    navController.navigate(MulletaFlixRoute.HOME) {
                        popUpTo(MulletaFlixRoute.HOME) { inclusive = true }
                    }
                },
            )
        }

        composable(MulletaFlixRoute.LIVE_TV) {
            LiveTvScreen(
                onChannelPlay = { channelId ->
                    navController.navigate(MulletaFlixRoute.videoPlayer(channelId))
                },
                onBack = { navController.popBackStack() },
            )
        }

        composable(MulletaFlixRoute.SETTINGS) {
            SettingsScreen(
                onProfile = { navController.navigate(MulletaFlixRoute.PROFILE) },
                onSyncPlay = { navController.navigate(MulletaFlixRoute.SYNC_PLAY) },
                onBack = { navController.popBackStack() },
                onLogout = {
                    navController.navigate(MulletaFlixRoute.SERVER_SELECTION) {
                        popUpTo(MulletaFlixRoute.HOME) { inclusive = true }
                    }
                }
            )
        }

        composable(MulletaFlixRoute.SYNC_PLAY) {
            // Sem `onJoinGroup`: o servidor não informa o item da sala em
            // `SyncPlay/List`, então a navegação que existia aqui levava um id
            // sempre nulo e nunca abria nada.
            SyncPlayScreen(
                onBack = { navController.popBackStack() }
            )
        }

        composable(MulletaFlixRoute.PROFILE) {
            ProfileScreen(
                onBack = { navController.popBackStack() },
                onLogout = {
                    navController.navigate(MulletaFlixRoute.SERVER_SELECTION) {
                        popUpTo(MulletaFlixRoute.HOME) { inclusive = true }
                    }
                },
                onSwitchServer = {
                    switchingServer = true
                    navController.navigate(MulletaFlixRoute.SERVER_SELECTION)
                },
            )
        }

        // Detail
        composable(
            route = MulletaFlixRoute.ITEM_DETAIL,
            arguments = listOf(navArgument("itemId") { type = NavType.StringType })
        ) { backStack ->
            val itemId = backStack.arguments?.getString("itemId") ?: ""
            ItemDetailScreen(
                itemId = itemId,
                onPlay = { id -> navController.navigate(MulletaFlixRoute.videoPlayer(id)) },
                onItemClick = { id -> navController.navigate(MulletaFlixRoute.itemDetail(id)) },
                onBack = { navController.popBackStack() }
            )
        }

        // Player
        composable(
            route = MulletaFlixRoute.VIDEO_PLAYER,
            arguments = listOf(navArgument("itemId") { type = NavType.StringType })
        ) { backStack ->
            val itemId = backStack.arguments?.getString("itemId") ?: ""
            VideoPlayerScreen(
                itemId = itemId,
                onBack = { navController.popBackStack() }
            )
        }

        composable(
            route = MulletaFlixRoute.OFFLINE_PLAYER,
            arguments = listOf(
                navArgument("itemId") { type = NavType.StringType },
                navArgument("uri") {
                    type = NavType.StringType
                    nullable = true
                    defaultValue = null
                },
                navArgument("title") {
                    type = NavType.StringType
                    nullable = true
                    defaultValue = null
                },
            ),
        ) { backStack ->
            VideoPlayerScreen(
                itemId = backStack.arguments?.getString("itemId") ?: "offline",
                offlineUri = backStack.arguments?.getString("uri"),
                offlineTitle = backStack.arguments?.getString("title"),
                onBack = { navController.popBackStack() },
            )
        }
    }
}

object MulletaFlixRoute {
    const val SERVER_SELECTION = "auth/server-selection"
    const val LOGIN = "auth/login"
    const val HOME = "main/home"
    const val FAVORITES = "main/favorites"
    const val SEARCH = "main/search"
    const val DOWNLOADS = "main/downloads"
    const val LIVE_TV = "main/live-tv"
    const val SETTINGS = "main/settings"
    const val PROFILE = "main/profile"
    const val SYNC_PLAY = "main/sync-play"

    const val LIBRARY = "main/library/{libId}"
    const val ITEM_DETAIL = "detail/{itemId}"
    const val VIDEO_PLAYER = "player/video/{itemId}"
    const val OFFLINE_PLAYER = "player/offline/{itemId}?uri={uri}&title={title}"

    fun library(libId: String) = "main/library/$libId"
    fun itemDetail(itemId: String) = "detail/$itemId"
    fun videoPlayer(itemId: String) = "player/video/$itemId"
    fun offlinePlayer(itemId: String, uri: String, title: String): String {
        // `encodeRouteQueryArgument`, not `URLEncoder`. Navigation decodes query
        // arguments with `Uri.getQueryParameters`, which follows RFC 3986 and
        // does **not** turn `+` back into a space, so `URLEncoder` made every
        // offline title containing a space render as `O+Retorno+de+Jedi`.
        val encodedId = encodeRouteQueryArgument(itemId)
        val encodedUri = encodeRouteQueryArgument(uri)
        val encodedTitle = encodeRouteQueryArgument(title)
        return "player/offline/$encodedId?uri=$encodedUri&title=$encodedTitle"
    }
}
