package org.mulletaflix.android.navigation

import androidx.compose.animation.AnimatedContentTransitionScope
import androidx.compose.animation.core.tween
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.navigation.NavHostController
import androidx.navigation.NavType
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.currentBackStackEntryAsState
import androidx.navigation.compose.rememberNavController
import androidx.navigation.navArgument
import org.mulletaflix.feature.auth.LoginScreen
import org.mulletaflix.feature.auth.ServerSelectionScreen
import org.mulletaflix.feature.home.HomeScreen
import org.mulletaflix.feature.library.LibraryScreen
import org.mulletaflix.feature.itemdetail.ItemDetailScreen
import org.mulletaflix.feature.player.VideoPlayerScreen
import org.mulletaflix.feature.search.SearchScreen
import org.mulletaflix.feature.settings.SettingsScreen
import org.mulletaflix.feature.user.ProfileScreen
import org.mulletaflix.feature.livetv.LiveTvScreen
import org.mulletaflix.feature.downloads.DownloadsScreen

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
fun MulletaFlixNavHost(
    navController: NavHostController = rememberNavController(),
    startDestination: String = MulletaFlixRoute.SERVER_SELECTION
) {
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
                onServerSelected = { navController.navigate(MulletaFlixRoute.LOGIN) }
            )
        }

        composable(MulletaFlixRoute.LOGIN) {
            LoginScreen(
                onLoginSuccess = {
                    navController.navigate(MulletaFlixRoute.HOME) {
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
                navController = navController
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
                }
            )
        }

        composable(MulletaFlixRoute.DOWNLOADS) {
            DownloadsScreen(
                onItemClick = { itemId ->
                    navController.navigate(MulletaFlixRoute.itemDetail(itemId))
                }
            )
        }

        composable(MulletaFlixRoute.LIVE_TV) {
            LiveTvScreen(
                onChannelPlay = { channelId ->
                    navController.navigate(MulletaFlixRoute.videoPlayer(channelId))
                }
            )
        }

        composable(MulletaFlixRoute.SETTINGS) {
            SettingsScreen(
                onLogout = {
                    navController.navigate(MulletaFlixRoute.SERVER_SELECTION) {
                        popUpTo(MulletaFlixRoute.HOME) { inclusive = true }
                    }
                }
            )
        }

        composable(MulletaFlixRoute.PROFILE) {
            ProfileScreen()
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
    }
}

object MulletaFlixRoute {
    const val SERVER_SELECTION = "auth/server-selection"
    const val LOGIN = "auth/login"
    const val HOME = "main/home"
    const val SEARCH = "main/search"
    const val DOWNLOADS = "main/downloads"
    const val LIVE_TV = "main/live-tv"
    const val SETTINGS = "main/settings"
    const val PROFILE = "main/profile"

    const val LIBRARY = "main/library/{libId}"
    const val ITEM_DETAIL = "detail/{itemId}"
    const val VIDEO_PLAYER = "player/video/{itemId}"

    fun library(libId: String) = "main/library/$libId"
    fun itemDetail(itemId: String) = "detail/$itemId"
    fun videoPlayer(itemId: String) = "player/video/$itemId"
}
