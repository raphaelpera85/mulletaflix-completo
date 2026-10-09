package org.mulletaflix.core.common.update

import android.content.ActivityNotFoundException
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.net.Uri
import android.os.Build
import android.provider.Settings
import androidx.core.content.FileProvider
import androidx.core.net.toUri
import java.io.File

object AppUpdateInstaller {

    private const val GOOGLE_PLAY_INSTALLER_PACKAGE = "com.android.vending"

    fun isGooglePlayInstallerPackageName(installerPackageName: String?): Boolean =
        installerPackageName == GOOGLE_PLAY_INSTALLER_PACKAGE

    /** Returns true when this installed package's recorded installer is Google Play. */
    fun isInstalledFromGooglePlay(context: Context): Boolean {
        val installerPackageName = try {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
                context.packageManager.getInstallSourceInfo(context.packageName).installingPackageName
            } else {
                @Suppress("DEPRECATION")
                context.packageManager.getInstallerPackageName(context.packageName)
            }
        } catch (_: PackageManager.NameNotFoundException) {
            null
        } catch (_: SecurityException) {
            null
        }
        return isGooglePlayInstallerPackageName(installerPackageName)
    }

    /**
     * Checks whether the app can request package installations (Android 8.0+).
     */
    fun canRequestPackageInstalls(context: Context): Boolean {
        return if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            try {
                context.packageManager.canRequestPackageInstalls()
            } catch (_: SecurityException) {
                // The API throws when REQUEST_INSTALL_PACKAGES is not declared. Keep
                // store-distributed builds from crashing when external self-updates are disallowed.
                false
            }
        } else {
            true
        }
    }

    /**
     * Opens the system settings screen to allow installing unknown apps.
     */
    fun openUnknownAppSourcesSettings(context: Context) {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            val intent = Intent(Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES).apply {
                data = "package:${context.packageName}".toUri()
                addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
            }
            context.startActivity(intent)
        }
    }

    /** Opens this app's Google Play listing, falling back to the browser URL. */
    fun openGooglePlayListing(context: Context): Boolean {
        val packageId = Uri.encode(context.packageName)
        val marketIntent = Intent(
            Intent.ACTION_VIEW,
            Uri.parse("market://details?id=$packageId"),
        ).apply {
            setPackage("com.android.vending")
            addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
        }
        try {
            context.startActivity(marketIntent)
            return true
        } catch (_: ActivityNotFoundException) {
            val browserIntent = Intent(
                Intent.ACTION_VIEW,
                Uri.parse("https://play.google.com/store/apps/details?id=$packageId"),
            ).apply { addFlags(Intent.FLAG_ACTIVITY_NEW_TASK) }
            return try {
                context.startActivity(browserIntent)
                true
            } catch (_: ActivityNotFoundException) {
                false
            }
        }
    }

    private fun declaresPackageInstallPermission(context: Context): Boolean {
        val requestedPermissions = try {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
                context.packageManager.getPackageInfo(
                    context.packageName,
                    PackageManager.PackageInfoFlags.of(PackageManager.GET_PERMISSIONS.toLong()),
                ).requestedPermissions
            } else {
                @Suppress("DEPRECATION")
                context.packageManager.getPackageInfo(
                    context.packageName,
                    PackageManager.GET_PERMISSIONS,
                ).requestedPermissions
            }
        } catch (_: PackageManager.NameNotFoundException) {
            null
        }
        return requestedPermissions?.contains(android.Manifest.permission.REQUEST_INSTALL_PACKAGES) == true
    }

    /**
     * Triggers the Android package installer for the given APK file.
     * Returns true when the system installer or the supported store handoff was opened.
     */
    fun installApk(context: Context, apkFile: File): Boolean {
        if (!canRequestPackageInstalls(context)) {
            if (declaresPackageInstallPermission(context)) {
                openUnknownAppSourcesSettings(context)
                return false
            }
            // Google Play policy does not permit self-updating a Play-distributed app
            // through REQUEST_INSTALL_PACKAGES. This manifest intentionally omits it.
            return openGooglePlayListing(context)
        }

        val contentUri = FileProvider.getUriForFile(
            context,
            "${context.packageName}.fileprovider",
            apkFile
        )

        val installIntent = Intent(Intent.ACTION_VIEW).apply {
            setDataAndType(contentUri, "application/vnd.android.package-archive")
            addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
            addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
        }

        context.startActivity(installIntent)
        return true
    }
}
