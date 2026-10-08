import java.security.KeyStore
import java.security.MessageDigest

plugins {
    alias(libs.plugins.android.application)
    alias(libs.plugins.kotlin.compose)
    alias(libs.plugins.kotlin.serialization)
    alias(libs.plugins.hilt)
    alias(libs.plugins.ksp)
}

// Chave de produção adotada após a rotação registrada em signing/SIGNING-KEY-ROTATION.md.
// A keystore privada nunca deve ser versionada; somente o certificado público é mantido no Git.
val officialReleaseSignerSha256 = "4890D80B87FE27C804FF875B549ACD57B946FE6F9BBA0B9D7718989EC5A0A24C"

android {
    namespace = "org.mulletaflix.android"
    compileSdk = libs.versions.compileSdk.get().toInt()

    sourceSets.getByName("androidTest").assets.srcDir(
        project(":feature:player").projectDir.resolve("src/androidTest/assets"),
    )

    defaultConfig {
        applicationId = "org.mulletaflix.android"
        minSdk = libs.versions.minSdk.get().toInt()
        targetSdk = libs.versions.targetSdk.get().toInt()
        versionCode = 382
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
        isCoreLibraryDesugaringEnabled = true
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

// Regressão: prova, com um processo Gradle real e código de saída real, que um build de
// variante release sem as credenciais de produção FALHA de forma explícita em vez de cair
// silenciosamente para a assinatura de debug. Não entra no grafo de build padrão (é lento,
// pois sobe um sub-build Gradle completo) — execute manualmente ou em um gate de CI dedicado:
//   ./gradlew :app:verifySigningGuardFailsWithoutCredentials
val verifySigningGuardFailsWithoutCredentials = tasks.register("verifySigningGuardFailsWithoutCredentials") {
    group = "verification"
    description = "Regression check: assembling/bundling a release variant without the " +
        "production KEYSTORE_PATH/KEYSTORE_PASSWORD/KEY_ALIAS/KEY_PASSWORD env vars must fail " +
        "loudly (non-zero exit, clear message) instead of silently falling back to debug signing."

    doLast {
        val isWindows = System.getProperty("os.name").lowercase().contains("windows")
        val wrapperName = if (isWindows) "gradlew.bat" else "gradlew"
        val wrapper = File(rootDir, wrapperName)
        if (!wrapper.isFile) {
            throw GradleException("Gradle wrapper não encontrado em ${wrapper.absolutePath}.")
        }

        val command = if (isWindows) {
            listOf(
                "cmd", "/c", wrapper.absolutePath,
                ":app:verifyProductionSigningCertificate", "--console=plain", "--no-daemon",
            )
        } else {
            listOf(
                wrapper.absolutePath,
                ":app:verifyProductionSigningCertificate", "--console=plain", "--no-daemon",
            )
        }

        val processBuilder = ProcessBuilder(command)
            .directory(rootDir)
            .redirectErrorStream(true)
        val environment = processBuilder.environment()
        // Força a ausência das credenciais de release, independentemente do ambiente do
        // chamador, para que este teste seja determinístico.
        listOf("KEYSTORE_PATH", "KEYSTORE_PASSWORD", "KEY_ALIAS", "KEY_PASSWORD").forEach {
            environment.remove(it)
        }

        val process = processBuilder.start()
        val output = process.inputStream.bufferedReader().readText()
        val exitCode = process.waitFor()

        if (exitCode == 0) {
            throw GradleException(
                "REGRESSÃO DE SEGURANÇA: ':app:verifyProductionSigningCertificate' concluiu com " +
                    "sucesso (exit 0) mesmo sem nenhuma credencial de release configurada. Isso " +
                    "significa que um build de release pode estar caindo silenciosamente para a " +
                    "assinatura de debug. Saída do sub-build:\n$output"
            )
        }
        // Fragmento puramente ASCII: a saída do sub-processo pode chegar em um charset da
        // console nativa do SO (ex.: cp1252 no Windows) que corrompe acentos, então evitamos
        // comparar com texto acentuado aqui.
        val expectedMessageFragment = "Ausentes: KEYSTORE_PATH, KEYSTORE_PASSWORD, KEY_ALIAS, KEY_PASSWORD"
        if (!output.contains(expectedMessageFragment)) {
            throw GradleException(
                "O build de release sem credenciais falhou (exit $exitCode) mas não com a " +
                    "mensagem esperada ('$expectedMessageFragment'). Saída do sub-build:\n$output"
            )
        }
    }
}

dependencies {
    coreLibraryDesugaring(libs.android.desugar.jdk.libs)
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
    androidTestImplementation(libs.androidx.work.testing)
    androidTestImplementation(libs.androidx.espresso.core)
    androidTestImplementation(libs.mockwebserver)
    androidTestImplementation(libs.moshi.kotlin)
    androidTestImplementation(libs.media3.cast)
    androidTestImplementation(libs.okhttp.tls)
    androidTestImplementation(platform(libs.androidx.compose.bom))
    androidTestImplementation(libs.androidx.ui.test.junit4)
    debugImplementation(libs.androidx.ui.tooling)
    debugImplementation(libs.androidx.ui.test.manifest)

}
