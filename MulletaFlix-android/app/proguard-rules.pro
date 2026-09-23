# MulletaFlix Android - ProGuard & R8 Configuration Rules

# ── Kotlin & Coroutines ───────────────────────────────────────────────────
-dontwarn kotlinx.coroutines.**
-keepattributes *Annotation*, Signature, InnerClasses, EnclosingMethod

# ── Moshi (JSON Serialization) ────────────────────────────────────────────
-keepclasseswithmembers class * {
    @com.squareup.moshi.Json <fields>;
}
-keepclasseswithmembers class * {
    @com.squareup.moshi.JsonClass class *;
}
-keep class * extends com.squareup.moshi.JsonAdapter {
    public <init>(com.squareup.moshi.Moshi);
    public <init>(com.squareup.moshi.Moshi, java.lang.reflect.Type[]);
}
-keep class org.mulletaflix.core.api.dto.** { *; }

# ── Retrofit & OkHttp ─────────────────────────────────────────────────────
-dontwarn retrofit2.**
-dontwarn okhttp3.**
-keep class retrofit2.** { *; }
-keepclasseswithmembers interface * {
    @retrofit2.http.* <methods>;
}

# ── Hilt / Dagger ─────────────────────────────────────────────────────────
-keep class * extends dagger.hilt.internal.GeneratedComponent
-keep class dagger.hilt.** { *; }

# ── Media3 / ExoPlayer ────────────────────────────────────────────────────
-keep class androidx.media3.** { *; }
-dontwarn androidx.media3.**

# ── Google Cast SDK ───────────────────────────────────────────────────────
-keep class com.google.android.gms.cast.** { *; }
-dontwarn com.google.android.gms.cast.**
