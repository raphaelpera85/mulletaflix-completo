package org.mulletaflix.android.update

import android.content.Context
import android.content.ContextWrapper
import android.content.Intent
import android.net.Uri
import android.os.Build
import android.provider.Settings
import androidx.core.content.FileProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.core.common.update.AppUpdateInstaller

@RunWith(AndroidJUnit4::class)
class AppUpdateInstallerIntegrationTest {
    private val context: Context
        get() = InstrumentationRegistry.getInstrumentation().targetContext

    @Test
    fun unknownAppSourcesIntentTargetsThisApplicationAndDoesNotLaunchSettingsInTest() {
        val recordingContext = RecordingContext(context)

        AppUpdateInstaller.openUnknownAppSourcesSettings(recordingContext)

        val intent = requireNotNull(recordingContext.startedIntent)
        assertEquals(Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES, intent.action)
        assertEquals(Uri.parse("package:${context.packageName}"), intent.data)
        assertTrue(intent.flags and Intent.FLAG_ACTIVITY_NEW_TASK != 0)
    }

    @Test
    fun installFallsBackToGooglePlayWhenTheManifestCannotRequestPackageInstalls() {
        val file = File(context.cacheDir, "updates/store-fallback-integration.apk").apply {
            parentFile?.mkdirs()
            writeBytes(byteArrayOf(0x50, 0x4b, 0x03, 0x04))
        }
        val recordingContext = RecordingContext(context)

        try {
            val permissionGranted = AppUpdateInstaller.canRequestPackageInstalls(context)
            assertFalse("o APK de produção não declara REQUEST_INSTALL_PACKAGES", permissionGranted)

            assertTrue(
                "quando instalação lateral não é permitida, deve abrir a página da loja",
                AppUpdateInstaller.installApk(recordingContext, file),
            )

            val intent = requireNotNull(recordingContext.startedIntent)
            assertEquals(Intent.ACTION_VIEW, intent.action)
            assertEquals("market", intent.data?.scheme)
            assertEquals("com.android.vending", intent.`package`)
            assertEquals(context.packageName, intent.data?.getQueryParameter("id"))
            assertTrue(intent.flags and Intent.FLAG_ACTIVITY_NEW_TASK != 0)
        } finally {
            file.delete()
        }
    }

    @Test
    fun googlePlayListingFallsBackToHttpsWhenPlayStoreIsUnavailable() {
        val recordingContext = RecordingContext(
            context,
            failMarketIntents = true,
        )

        assertTrue(AppUpdateInstaller.openGooglePlayListing(recordingContext))

        assertEquals(1, recordingContext.startedIntents.size)
        val browserIntent = recordingContext.startedIntents.last()
        assertEquals(Intent.ACTION_VIEW, browserIntent.action)
        assertEquals("https", browserIntent.data?.scheme)
        assertEquals("play.google.com", browserIntent.data?.host)
        assertEquals(context.packageName, browserIntent.data?.getQueryParameter("id"))
    }

    @Test
    fun googlePlayInstallDetectionMatchesAndroidInstallSourceMetadata() {
        val installerPackageName = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
            context.packageManager.getInstallSourceInfo(context.packageName).installingPackageName
        } else {
            @Suppress("DEPRECATION")
            context.packageManager.getInstallerPackageName(context.packageName)
        }

        assertEquals(
            AppUpdateInstaller.isGooglePlayInstallerPackageName(installerPackageName),
            AppUpdateInstaller.isInstalledFromGooglePlay(context),
        )
    }

    @Test
    fun cachedUpdateApkIsReadableThroughTheDeclaredFileProviderPath() {
        val expectedBytes = byteArrayOf(0x50, 0x4b, 0x03, 0x04, 0x01)
        val file = File(context.cacheDir, "updates/file-provider-integration.apk").apply {
            parentFile?.mkdirs()
            writeBytes(expectedBytes)
        }

        try {
            val uri = FileProvider.getUriForFile(
                context,
                "${context.packageName}.fileprovider",
                file,
            )
            val deliveredBytes = context.contentResolver.openInputStream(uri).use { input ->
                requireNotNull(input).readBytes()
            }

            assertEquals("content", uri.scheme)
            assertEquals("${context.packageName}.fileprovider", uri.authority)
            assertTrue(deliveredBytes.contentEquals(expectedBytes))
        } finally {
            file.delete()
        }
    }

    @Test
    fun installUsesTheRealFileProviderButNeverOpensTheExternalInstallerInTest() {
        val file = File(context.cacheDir, "updates/installer-integration.apk").apply {
            parentFile?.mkdirs()
            writeBytes(byteArrayOf(0x50, 0x4b, 0x03, 0x04))
        }
        val recordingContext = RecordingContext(context)

        try {
            val permissionGranted = AppUpdateInstaller.canRequestPackageInstalls(context)
            val started = AppUpdateInstaller.installApk(recordingContext, file)
            val intent = requireNotNull(recordingContext.startedIntent)

            if (permissionGranted) {
                assertTrue(started)
                assertEquals(Intent.ACTION_VIEW, intent.action)
                assertEquals("application/vnd.android.package-archive", intent.type)
                assertEquals("content", intent.data?.scheme)
                assertEquals("${context.packageName}.fileprovider", intent.data?.authority)
                assertTrue(intent.flags and Intent.FLAG_GRANT_READ_URI_PERMISSION != 0)
                assertTrue(intent.flags and Intent.FLAG_ACTIVITY_NEW_TASK != 0)
                val deliveredBytes = context.contentResolver.openInputStream(intent.data!!).use { input ->
                    requireNotNull(input).readBytes()
                }
                assertTrue(deliveredBytes.contentEquals(file.readBytes()))
            } else {
                assertTrue(started)
                assertEquals(Intent.ACTION_VIEW, intent.action)
                assertEquals("market", intent.data?.scheme)
                assertEquals("com.android.vending", intent.`package`)
                assertEquals(context.packageName, intent.data?.getQueryParameter("id"))
            }
        } finally {
            file.delete()
        }
    }

    private class RecordingContext(
        base: Context,
        private val failMarketIntents: Boolean = false,
    ) : ContextWrapper(base) {
        private val _startedIntents = mutableListOf<Intent>()
        val startedIntents: List<Intent> get() = _startedIntents.toList()
        val startedIntent: Intent? get() = _startedIntents.lastOrNull()

        override fun startActivity(intent: Intent) {
            if (failMarketIntents && intent.data?.scheme == "market") {
                throw android.content.ActivityNotFoundException("Play Store indisponível no teste")
            }
            _startedIntents += intent
        }

    }
}
