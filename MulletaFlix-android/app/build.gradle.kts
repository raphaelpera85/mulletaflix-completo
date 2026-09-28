import java.security.KeyStore
import java.security.MessageDigest

plugins {
    alias(libs.plugins.android.application)
    alias(libs.plugins.kotlin.compose)
    alias(libs.plugins.kotlin.serialization)
    alias(libs.plugins.hilt)
    alias(libs.plugins.ksp)
}

val officialReleaseSignerSha256 = "224F9A6BD12690E1114ACE649BBFA778D3E7E99DAE608FF711DDF9131E036273"

android {
    namespace = "org.mulletaflix.android"
    compileSdk = libs.versions.compileSdk.get().toInt()

    defaultConfig {
        applicationId = "org.mulletaflix.android"
        minSdk = libs.versions.minSdk.get().toInt()
        targetSdk = libs.versions.targetSdk.get().toInt()
        versionCode = 381
        versionName = libs.versions.appVersion.get()

        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"
        vectorDrawables {
            useSupportLibrary = true
        }
    }

    signingConfigs {
        create("release") {
            val keystorePath = System.getenv("KEYSTORE_PATH")
            if (!keystorePath.isNullOrBlank() && file(keystorePath).exists()) {
                storeFile = file(keystorePath)
                storePassword = System.getenv("KEYSTORE_PASSWORD")
                keyAlias = System.getenv("KEY_ALIAS")
                keyPassword = System.getenv("KEY_PASSWORD")
            } else {
                // Testes e builds Debug não precisam de uma credencial de distribuição.
                // A validação ligada ao empacotamento bloqueia release sem a keystore oficial.
                initWith(getByName("debug"))
            }
        }
    }

    buildTypes {
        release {
            isMinifyEnabled = true
            isShrinkResources = true
            signingConfig = signingConfigs.getByName("release")
            proguardFiles(
                getDefaultProguardFile("proguard-android-optimize.txt"),
                "proguard-rules.pro"
            )
        }
        debug {
            isDebuggable = true
            applicationIdSuffix = ".debug"
            versionNameSuffix = "-debug"
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }


    buildFeatures {
        compose = true
        buildConfig = true
    }

    testOptions {
        unitTests.isReturnDefaultValues = true
        animationsDisabled = true
    }

    packaging {
        resources {
            excludes += "/META-INF/{AL2.0,LGPL2.1}"
        }
    }
}

val verifyProductionSigningCertificate = tasks.register("verifyProductionSigningCertificate") {
    group = "verification"
    description = "Confirms the release keystore matches the official MulletaFlix signing certificate."

    doLast {
        val requiredValues = listOf(
            "KEYSTORE_PATH" to System.getenv("KEYSTORE_PATH"),
            "KEYSTORE_PASSWORD" to System.getenv("KEYSTORE_PASSWORD"),
            "KEY_ALIAS" to System.getenv("KEY_ALIAS"),
            "KEY_PASSWORD" to System.getenv("KEY_PASSWORD"),
        )
        val missingVariables = requiredValues.filter { it.second.isNullOrBlank() }.map { it.first }
        if (missingVariables.isNotEmpty()) {
            throw GradleException(
                "Release Android exige todas as credenciais de produção. Ausentes: " +
                    missingVariables.joinToString(", ")
            )
        }

        val keystoreFile = file(System.getenv("KEYSTORE_PATH")!!)
        if (!keystoreFile.isFile) {
            throw GradleException("O arquivo definido em KEYSTORE_PATH não existe.")
        }

        val storePassword = System.getenv("KEYSTORE_PASSWORD")!!
        val keyAlias = System.getenv("KEY_ALIAS")!!
        var certificate: java.security.cert.Certificate? = null
        var lastLoadFailure: Exception? = null
        for (storeType in listOf("JKS", "PKCS12")) {
            try {
                val keyStore = KeyStore.getInstance(storeType)
                keystoreFile.inputStream().use { keyStore.load(it, storePassword.toCharArray()) }
                certificate = keyStore.getCertificate(keyAlias)
                if (certificate != null) break
            } catch (error: Exception) {
                lastLoadFailure = error
            }
        }
        val signingCertificate = certificate ?: throw GradleException(
            "Não foi possível ler um certificado no alias configurado da keystore de produção.",
            lastLoadFailure
        )

        val actualSignerSha256 = MessageDigest.getInstance("SHA-256")
            .digest(signingCertificate.encoded)
            .joinToString("") { "%02X".format(it.toInt() and 0xFF) }
        if (!actualSignerSha256.equals(officialReleaseSignerSha256, ignoreCase = true)) {
            throw GradleException(
                "Certificado da keystore de produção incompatível. " +
                    "Esperado $officialReleaseSignerSha256; obtido $actualSignerSha256."
            )
        }
    }
}

tasks.configureEach {
    if (name in setOf(
            "validateSigningRelease",
            "packageRelease",
            "packageReleaseBundle",
            "packageReleaseUniversalApk",
        )
    ) {
        dependsOn(verifyProductionSigningCertificate)
    }
}

dependencies {
    implementation(project(":core:common"))
    implementation(project(":core:api"))
    implementation(project(":domain"))
    implementation(project(":data"))
    implementation(project(":design-system"))
    implementation(project(":feature:auth"))
    implementation(project(":feature:home"))
    implementation(project(":feature:library"))
    implementation(project(":feature:item-detail"))
    implementation(project(":feature:player"))
    implementation(project(":feature:search"))
    implementation(project(":feature:downloads"))
    implementation(project(":feature:live-tv"))
    implementation(project(":feature:settings"))
    implementation(project(":feature:user"))
    implementation(project(":feature:sync-play"))

    // Core
    implementation(libs.androidx.core.ktx)
    implementation(libs.androidx.work.runtime.ktx)
    implementation(libs.androidx.lifecycle.runtime.ktx)
    implementation(libs.androidx.lifecycle.runtime.compose)
    implementation(libs.androidx.activity.compose)
    implementation(libs.core.splashscreen)

    // Compose
    implementation(platform(libs.androidx.compose.bom))
    implementation(libs.androidx.ui)
    implementation(libs.androidx.ui.graphics)
    implementation(libs.androidx.ui.tooling.preview)
    implementation(libs.androidx.material3)
    implementation(libs.androidx.material.icons.extended)
    implementation(libs.androidx.navigation.compose)
    // Artwork goes through the same authenticated client as the API, so the
    // server can identify the device and does not throttle the poster grid.
    implementation(libs.coil.compose)
    implementation(libs.okhttp)

    // Hilt
    implementation(libs.hilt.android)
    ksp(libs.hilt.android.compiler)
    implementation(libs.hilt.navigation.compose)

    // Media3 & Cast
    implementation(libs.media3.exoplayer)
    implementation(libs.media3.session)
    implementation(libs.play.services.cast.framework)

    // Tests
    testImplementation(libs.junit)
    testImplementation(libs.kotlinx.coroutines.test)
    // `AppUpdateDownloader` é uma classe final com `Context` no construtor: para testar o
    // fluxo do aviso de atualização sem Android, ela entra como mock.
    testImplementation(libs.mockk)
    androidTestImplementation(libs.androidx.junit)
    androidTestImplementation(libs.androidx.test.runner)
    androidTestImplementation(libs.androidx.espresso.core)
    androidTestImplementation(platform(libs.androidx.compose.bom))
    androidTestImplementation(libs.androidx.ui.test.junit4)
    debugImplementation(libs.androidx.ui.tooling)
    debugImplementation(libs.androidx.ui.test.manifest)

}
