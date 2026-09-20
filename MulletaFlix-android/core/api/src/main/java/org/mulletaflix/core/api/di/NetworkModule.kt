package org.mulletaflix.core.api.di

import com.squareup.moshi.Moshi
import com.squareup.moshi.kotlin.reflect.KotlinJsonAdapterFactory
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import okhttp3.OkHttpClient
import okhttp3.logging.HttpLoggingInterceptor
import org.mulletaflix.core.api.AuthInterceptor
import org.mulletaflix.core.api.ApiRetryInterceptor
import org.mulletaflix.core.api.MulletaFlixApiService
import org.mulletaflix.core.api.ServerUrlInterceptor
import retrofit2.Retrofit
import retrofit2.converter.moshi.MoshiConverterFactory
import java.util.concurrent.TimeUnit
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
object NetworkModule {

    @Provides
    @Singleton
    fun provideMoshi(): Moshi = Moshi.Builder()
        .addLast(KotlinJsonAdapterFactory())
        .build()

    @Provides
    @Singleton
    fun provideOkHttpClient(
        authInterceptor: AuthInterceptor,
        apiRetryInterceptor: ApiRetryInterceptor,
        serverUrlInterceptor: ServerUrlInterceptor,
    ): OkHttpClient = OkHttpClient.Builder()
        .addInterceptor(serverUrlInterceptor)   // replaces base URL dynamically
        .addInterceptor(authInterceptor)         // injects Bearer token
        .addInterceptor(apiRetryInterceptor)     // recovers safe transient API failures
        // Do not log bodies, Authorization headers, or playback URLs. Media
        // URLs may contain api_key tokens even when the app is a debug build.
        .addInterceptor(HttpLoggingInterceptor().apply {
            level = HttpLoggingInterceptor.Level.NONE
        })
        .connectTimeout(30, TimeUnit.SECONDS)
        .readTimeout(120, TimeUnit.SECONDS)      // 2h for streams handled at OkHttp level; per API contract
        .writeTimeout(30, TimeUnit.SECONDS)
        .build()

    @Provides
    @Singleton
    fun provideRetrofit(okHttpClient: OkHttpClient, moshi: Moshi): Retrofit =
        Retrofit.Builder()
            // Placeholder — real URL injected per-request by ServerUrlInterceptor
            .baseUrl("http://localhost:8096/")
            .client(okHttpClient)
            .addConverterFactory(MoshiConverterFactory.create(moshi))
            .build()

    @Provides
    @Singleton
    fun provideApiService(retrofit: Retrofit): MulletaFlixApiService =
        retrofit.create(MulletaFlixApiService::class.java)
}
