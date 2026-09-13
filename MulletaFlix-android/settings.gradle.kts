pluginManagement {
    repositories {
        google {
            content {
                includeGroupByRegex("com\\.android.*")
                includeGroupByRegex("com\\.google.*")
                includeGroupByRegex("androidx.*")
            }
        }
        mavenCentral()
        gradlePluginPortal()
    }
}

dependencyResolutionManagement {
    repositoriesMode.set(RepositoriesMode.FAIL_ON_PROJECT_REPOS)
    repositories {
        google()
        mavenCentral()
    }
}

rootProject.name = "MulletaFlix"

include(":app")
include(":core:common")
include(":core:api")
include(":core:testing")
include(":domain")
include(":data")
include(":design-system")
include(":feature:auth")
include(":feature:home")
include(":feature:library")
include(":feature:item-detail")
include(":feature:player")
include(":feature:search")
include(":feature:downloads")
include(":feature:live-tv")
include(":feature:settings")
include(":feature:user")
include(":feature:sync-play")
