package org.mulletaflix.feature.home

import org.mulletaflix.core.common.util.userFacingMessageOr

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.CoroutineStart
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaSuggestion
import org.mulletaflix.domain.model.UserProfile
import org.mulletaflix.domain.repository.AuthRepository
import org.mulletaflix.domain.repository.UserFeedbackRepository
import org.mulletaflix.domain.repository.MediaRequestDraft
import org.mulletaflix.domain.repository.MediaRequestDraftStore
import org.mulletaflix.domain.usecase.GetHomeFeedUseCase
import org.mulletaflix.core.api.ActiveServerEndpointChangeSignal
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.core.common.session.FeedbackRequestSession
import org.mulletaflix.core.common.session.mediaRequestDraftScopeHash
import org.mulletaflix.core.common.network.NetworkMonitor
import javax.inject.Inject

data class HomeState(
    val isLoading: Boolean = true,
    val isRefreshing: Boolean = false,
    val isOffline: Boolean = false,
    val cachedAtEpochMillis: Long? = null,
    val resumeFromCache: Boolean = false,
    val favoritesFromCache: Boolean = false,
    val heroItem: MediaItem? = null,
    val resumeItems: List<MediaItem> = emptyList(),
    val nextUpItems: List<MediaItem> = emptyList(),
    val favoriteItems: List<MediaItem> = emptyList(),
    val recentlyAddedByLibrary: Map<String, List<MediaItem>> = emptyMap(),
    val recentlyAddedErrorsByLibrary: Map<String, String> = emptyMap(),
    val retryingRecentlyAddedLibraryIds: Set<String> = emptySet(),
    val liveTvChannels: List<MediaItem> = emptyList(),
    val libraries: List<MediaItem> = emptyList(),
    val userProfile: UserProfile? = null,
    val error: String? = null,
    val resumeError: String? = null,
    val nextUpError: String? = null,
    val favoritesError: String? = null,
    /** Não nulo quando só as bibliotecas falharam; o resto da Home pode estar certo. */
    val librariesError: String? = null,
    /** Não nulo quando só a TV ao vivo falhou. Zero canais por **sucesso** não é erro. */
    val liveTvError: String? = null,
    val feedbackSessionLoaded: Boolean = false,
    val hasFeedbackSession: Boolean = false,
    val mediaSuggestions: List<MediaSuggestion> = emptyList(),
    val mediaRequestDraft: MediaRequestDraft = MediaRequestDraft(),
    val mediaRequestDraftLoaded: Boolean = false,
    val mediaRequestDraftScopeHash: String? = null,
    val mediaRequestDraftSaveState: MediaRequestDraftSaveState = MediaRequestDraftSaveState.IDLE,
)

enum class MediaRequestDraftSaveState { IDLE, SAVING, SAVED, FAILED }

@HiltViewModel
class HomeViewModel @Inject constructor(
    private val getHomeFeedUseCase: GetHomeFeedUseCase,
    private val sessionRepository: SessionRepository,
    private val networkMonitor: NetworkMonitor,
    private val authRepository: AuthRepository,
    private val userFeedbackRepository: UserFeedbackRepository = object : UserFeedbackRepository {
        override suspend fun requestMedia(session: FeedbackRequestSession, title: String, mediaType: String, year: Int?, notes: String?) = Result.failure<Unit>(UnsupportedOperationException())
        override suspend fun reportPlaybackIssue(session: FeedbackRequestSession, itemId: String, category: String, description: String?) = Result.failure<Unit>(UnsupportedOperationException())
    },
    private val activeServerEndpointChangeSignal: ActiveServerEndpointChangeSignal = ActiveServerEndpointChangeSignal(),
    private val mediaRequestDraftStore: MediaRequestDraftStore = EmptyMediaRequestDraftStore,
) : ViewModel() {

    private var mediaRequestSubmitting = false
    private var feedbackRequestSession: FeedbackRequestSession? = null
    private var mediaSuggestionJob: Job? = null
    private var mediaSuggestionGeneration = 0L
    private var mediaRequestDraftScope: String? = null
    private var mediaRequestDraftScopeObserved = false
    private var mediaRequestDraftGeneration = 0L
    private var mediaRequestDraftLoadJob: Job? = null
    private var mediaRequestDraftSaveJob: Job? = null
    private var mediaRequestDraftSaveScope: String? = null
    private val pendingMediaRequestDrafts = linkedMapOf<String, MediaRequestDraft>()
    private val mediaRequestDraftWriteMutex = Mutex()

    // INTENT: Keep request drafts private to the authenticated account/server and restore them without resubmitting.
    fun updateMediaRequestDraft(draft: MediaRequestDraft) {
        val current = _state.value
        val scopeHash = current.mediaRequestDraftScopeHash ?: return
        if (!current.mediaRequestDraftLoaded) return
        _state.update {
            it.copy(mediaRequestDraft = draft, mediaRequestDraftSaveState = MediaRequestDraftSaveState.SAVING)
        }
        pendingMediaRequestDrafts[scopeHash] = draft
        mediaRequestDraftSaveJob?.cancel()
        lateinit var saveJob: Job
        saveJob = viewModelScope.launch(start = CoroutineStart.LAZY) {
            try {
                delay(MEDIA_REQUEST_DRAFT_SAVE_DEBOUNCE_MILLIS)
                persistPendingMediaRequestDraft(scopeHash, draft)
            } finally {
                if (mediaRequestDraftSaveJob === saveJob) {
                    mediaRequestDraftSaveJob = null
                    mediaRequestDraftSaveScope = null
                }
            }
        }
        mediaRequestDraftSaveScope = scopeHash
        mediaRequestDraftSaveJob = saveJob
        saveJob.start()
    }

    /** Persist pending input before the app is backgrounded or the user leaves Home. */
    fun flushMediaRequestDraft() {
        val pending = pendingMediaRequestDrafts.toMap()
        if (pending.isEmpty()) return
        mediaRequestDraftSaveJob?.cancel()
        mediaRequestDraftSaveScope = null
        lateinit var flushJob: Job
        flushJob = viewModelScope.launch(start = CoroutineStart.LAZY) {
            try {
                for ((scopeHash, draft) in pending) {
                    persistPendingMediaRequestDraft(scopeHash, draft)
                }
            } finally {
                if (mediaRequestDraftSaveJob === flushJob) {
                    mediaRequestDraftSaveJob = null
                    mediaRequestDraftSaveScope = null
                }
            }
        }
        mediaRequestDraftSaveJob = flushJob
        flushJob.start()
    }

    /** Retry only the visible account/server's local draft; this never resubmits a media request. */
    fun retryMediaRequestDraftSave() {
        val scopeHash = _state.value.mediaRequestDraftScopeHash ?: return
        val draft = pendingMediaRequestDrafts[scopeHash] ?: return
        mediaRequestDraftSaveJob?.cancel()
        mediaRequestDraftSaveScope = null
        lateinit var retryJob: Job
        retryJob = viewModelScope.launch(start = CoroutineStart.LAZY) {
            try {
                persistPendingMediaRequestDraft(scopeHash, draft)
            } finally {
                if (mediaRequestDraftSaveJob === retryJob) {
                    mediaRequestDraftSaveJob = null
                    mediaRequestDraftSaveScope = null
                }
            }
        }
        mediaRequestDraftSaveScope = scopeHash
        mediaRequestDraftSaveJob = retryJob
        retryJob.start()
    }

    private suspend fun persistPendingMediaRequestDraft(scopeHash: String, draft: MediaRequestDraft) {
        if (pendingMediaRequestDrafts[scopeHash] != draft) return
        updateDraftSaveState(scopeHash, draft, MediaRequestDraftSaveState.SAVING)
        val saved = persistMediaRequestDraft(scopeHash, draft)
        if (pendingMediaRequestDrafts[scopeHash] == draft) {
            if (saved) {
                pendingMediaRequestDrafts.remove(scopeHash)
                updateDraftSaveState(scopeHash, draft, MediaRequestDraftSaveState.SAVED)
            } else {
                updateDraftSaveState(scopeHash, draft, MediaRequestDraftSaveState.FAILED)
            }
        }
    }

    private fun updateDraftSaveState(
        scopeHash: String,
        draft: MediaRequestDraft,
        saveState: MediaRequestDraftSaveState,
    ) {
        _state.update { current ->
            if (current.mediaRequestDraftScopeHash == scopeHash && current.mediaRequestDraft == draft) {
                current.copy(mediaRequestDraftSaveState = saveState)
            } else {
                current
            }
        }
    }

    private suspend fun persistMediaRequestDraft(scopeHash: String, draft: MediaRequestDraft): Boolean =
        mediaRequestDraftWriteMutex.withLock {
            if (pendingMediaRequestDrafts[scopeHash] != draft) return@withLock false
            // Per-scope keys prevent a late save from one account overwriting another account's draft.
            try {
                mediaRequestDraftStore.save(scopeHash, draft)
                true
            } catch (cancelled: CancellationException) {
                throw cancelled
            } catch (_: Throwable) {
                // A local persistence error must not crash Home or turn a successful network request into failure.
                false
            }
        }

    private fun cancelPendingMediaRequestDraftFor(scopeHash: String) {
        if (mediaRequestDraftSaveScope == scopeHash) {
            mediaRequestDraftSaveJob?.cancel()
            mediaRequestDraftSaveJob = null
            mediaRequestDraftSaveScope = null
        }
        pendingMediaRequestDrafts.remove(scopeHash)
    }

    fun searchMediaSuggestions(query: String) {
        val normalizedQuery = query.trim()
        val generation = ++mediaSuggestionGeneration
        mediaSuggestionJob?.cancel()
        if (normalizedQuery.length < 2) {
            _state.update { it.copy(mediaSuggestions = emptyList()) }
            return
        }

        val requestSession = feedbackRequestSession
        if (requestSession == null) {
            _state.update { it.copy(mediaSuggestions = emptyList()) }
            return
        }

        mediaSuggestionJob = viewModelScope.launch {
            kotlinx.coroutines.delay(250)
            val result = userFeedbackRepository.getMediaSuggestions(requestSession, normalizedQuery)
            if (generation == mediaSuggestionGeneration
                && feedbackRequestSession == requestSession
                && _state.value.hasFeedbackSession) {
                _state.update { it.copy(mediaSuggestions = result.getOrDefault(emptyList())) }
            }
        }
    }

    fun clearMediaSuggestions() {
        mediaSuggestionGeneration++
        mediaSuggestionJob?.cancel()
        _state.update { it.copy(mediaSuggestions = emptyList()) }
    }

    fun requestMedia(title: String, mediaType: String, year: Int?, notes: String, onComplete: (Result<Unit>) -> Unit) {
        if (mediaRequestSubmitting) return
        val requestSession = feedbackRequestSession
        if (requestSession == null) {
            onComplete(Result.failure(IllegalStateException("Sessão indisponível. Conecte-se novamente.")))
            return
        }
        mediaRequestSubmitting = true
        viewModelScope.launch {
            val result = try {
                check(sessionRepository.getFeedbackRequestSession().first() == requestSession) {
                    "A sessão mudou. Revise sua conexão antes de tentar novamente."
                }
                userFeedbackRepository.requestMedia(requestSession, title.trim(), mediaType, year, notes.trim())
            } catch (cancelled: CancellationException) {
                onComplete(Result.failure(cancelled))
                throw cancelled
            } catch (error: Throwable) {
                Result.failure(error)
            } finally {
                mediaRequestSubmitting = false
            }
            if (result.isSuccess) {
                val scopeHash = requestSession.mediaRequestDraftScopeHash()
                val cleanDraft = MediaRequestDraft(mediaType = mediaType)
                cancelPendingMediaRequestDraftFor(scopeHash)
                if (_state.value.mediaRequestDraftScopeHash == scopeHash) {
                    _state.update { it.copy(mediaRequestDraft = cleanDraft) }
                }
                pendingMediaRequestDrafts[scopeHash] = cleanDraft
                persistPendingMediaRequestDraft(scopeHash, cleanDraft)
            }
            onComplete(result)
        }
    }

    private val _state = MutableStateFlow(HomeState())
    val state: StateFlow<HomeState> = _state.asStateFlow()
    private var loadJob: Job? = null
    private val recentlyAddedRetryJobs = mutableMapOf<String, Job>()
    private var profileJob: Job? = null
    private var loadGeneration = 0L
    private var profileGeneration = 0L
    private var currentUserId: String? = null
    private var hasObservedSession = false

    init {
        viewModelScope.launch {
            sessionRepository.getFeedbackRequestSession().distinctUntilChanged().collect { session ->
                feedbackRequestSession = session
                val draftScope = session?.mediaRequestDraftScopeHash()
                _state.update {
                    it.copy(feedbackSessionLoaded = true, hasFeedbackSession = session != null,
                        mediaSuggestions = if (session == null) emptyList() else it.mediaSuggestions)
                }
                if (!mediaRequestDraftScopeObserved || draftScope != mediaRequestDraftScope) {
                    mediaRequestDraftScopeObserved = true
                    val previousScope = mediaRequestDraftScope
                    if (previousScope != null) {
                        val pending = pendingMediaRequestDrafts[previousScope]
                        if (mediaRequestDraftSaveScope == previousScope) {
                            mediaRequestDraftSaveJob?.cancel()
                            mediaRequestDraftSaveJob = null
                            mediaRequestDraftSaveScope = null
                        }
                        if (pending != null) launch { persistPendingMediaRequestDraft(previousScope, pending) }
                    }
                    mediaRequestDraftScope = draftScope
                    val generation = ++mediaRequestDraftGeneration
                    mediaRequestDraftLoadJob?.cancel()
                    _state.update {
                        it.copy(
                            mediaRequestDraft = MediaRequestDraft(),
                            mediaRequestDraftLoaded = draftScope == null,
                            mediaRequestDraftScopeHash = draftScope,
                            mediaRequestDraftSaveState = MediaRequestDraftSaveState.IDLE,
                        )
                    }
                    if (draftScope != null) {
                        mediaRequestDraftLoadJob = launch {
                            val pendingDraft = pendingMediaRequestDrafts[draftScope]
                            val draft = pendingDraft ?: try {
                                mediaRequestDraftStore.load(draftScope) ?: MediaRequestDraft()
                            } catch (cancelled: CancellationException) {
                                throw cancelled
                            } catch (_: Throwable) {
                                MediaRequestDraft()
                            }
                            if (generation == mediaRequestDraftGeneration && mediaRequestDraftScope == draftScope) {
                                _state.update {
                                    it.copy(mediaRequestDraft = draft, mediaRequestDraftLoaded = true)
                                }
                                if (pendingDraft != null) persistPendingMediaRequestDraft(draftScope, pendingDraft)
                            }
                        }
                    }
                }
                if (session == null) clearMediaSuggestions()
            }
        }
        viewModelScope.launch {
            activeServerEndpointChangeSignal.changes.collect { serverUrl ->
                if (serverUrl.isNotBlank() && !currentUserId.isNullOrBlank()) {
                    // Only confirmed automatic recovery emits this signal. Server
                    // verification temporarily changes the shared URL too, but
                    // must not trigger authenticated catalog requests to that host.
                    refresh()
                }
            }
        }
        viewModelScope.launch {
            var previousOnline: Boolean? = null
            networkMonitor.isOnline.distinctUntilChanged().collect { online ->
                val recovered = shouldRefreshHomeOnNetworkReturn(previousOnline, online)
                previousOnline = online
                _state.update { it.copy(isOffline = !online) }
                if (recovered) refresh()
            }
        }
        viewModelScope.launch {
            sessionRepository.getCurrentUserId().distinctUntilChanged().collect { userId ->
                val userChanged = hasObservedSession && currentUserId != userId
                currentUserId = userId
                hasObservedSession = true
                if (userChanged) {
                    loadJob?.cancel()
                    ++loadGeneration
                    profileJob?.cancel()
                    profileJob = null
                    ++profileGeneration
                    _state.update {
                        it.copy(
                            heroItem = null,
                            resumeItems = emptyList(),
                            nextUpItems = emptyList(),
                            favoriteItems = emptyList(),
                            cachedAtEpochMillis = null,
                            resumeFromCache = false,
                            favoritesFromCache = false,
                            recentlyAddedByLibrary = emptyMap(),
                            recentlyAddedErrorsByLibrary = emptyMap(),
                            liveTvChannels = emptyList(),
                            libraries = emptyList(),
                            userProfile = null,
                            isLoading = false,
                            isRefreshing = false,
                            error = null,
                            resumeError = null,
                            nextUpError = null,
                            favoritesError = null,
                            librariesError = null,
                            liveTvError = null,
                        )
                    }
                }
                loadHome()
            }
        }
    }

    fun refresh() {
        loadJob?.cancel()
        _state.update { it.copy(isRefreshing = true) }
        loadHome(refresh = true)
    }

    fun retryRecentlyAdded(libraryId: String) {
        val current = _state.value
        val library = current.libraries.firstOrNull { it.id == libraryId } ?: return
        if (current.isLoading || current.isRefreshing || libraryId in current.retryingRecentlyAddedLibraryIds) return
        val userId = currentUserId?.takeIf(String::isNotBlank) ?: return
        val generation = loadGeneration

        _state.update { it.copy(retryingRecentlyAddedLibraryIds = it.retryingRecentlyAddedLibraryIds + libraryId) }
        val retryJob = viewModelScope.launch(start = CoroutineStart.LAZY) {
            var serverUnchanged = false
            try {
                val serverUrl = sessionRepository.getBaseUrl().first()
                val result = try {
                    if (!networkMonitor.isOnline.first()) {
                        Result.failure(IllegalStateException("Sem conexão. Verifique sua rede e tente novamente."))
                    } else {
                        getHomeFeedUseCase.getLatestItemsForLibrary(userId, libraryId)
                    }
                } catch (cancelled: CancellationException) {
                    throw cancelled
                } catch (error: Throwable) {
                    Result.failure(error)
                }

                serverUnchanged = sessionRepository.getBaseUrl().first() == serverUrl
                if (generation == loadGeneration && currentUserId == userId && serverUnchanged) {
                    result.onSuccess { items ->
                        _state.update { state ->
                            if (state.libraries.none { it.id == libraryId }) state
                            else state.copy(
                                recentlyAddedByLibrary = state.recentlyAddedByLibrary + (libraryId to items),
                                recentlyAddedErrorsByLibrary = state.recentlyAddedErrorsByLibrary - libraryId,
                            )
                        }
                    }.onFailure { error ->
                        val message = error.userFacingMessageOr(
                            "Não foi possível carregar Adicionados Recentemente — ${library.name}.",
                        )
                        _state.update { state ->
                            if (state.libraries.none { it.id == libraryId }) state
                            else state.copy(
                                recentlyAddedErrorsByLibrary = state.recentlyAddedErrorsByLibrary + (libraryId to message),
                            )
                        }
                    }
                }
            } finally {
                if (recentlyAddedRetryJobs[libraryId] === coroutineContext[Job]) {
                    recentlyAddedRetryJobs.remove(libraryId)
                    if (generation == loadGeneration && currentUserId == userId) {
                        _state.update {
                            it.copy(retryingRecentlyAddedLibraryIds = it.retryingRecentlyAddedLibraryIds - libraryId)
                        }
                    }
                }
            }
        }
        recentlyAddedRetryJobs[libraryId] = retryJob
        retryJob.start()
    }

    /**
     * Reconciles the TV home feed without interrupting the initial load or a
     * refresh already in flight. The foreground timer calls this method so a
     * slow server response cannot be cancelled by the next tick.
     */
    fun refreshIfIdle() {
        val current = _state.value
        // A foreground refresh can arrive immediately after the initial
        // coroutine is created, before that coroutine publishes isLoading.
        // Treat the active Job as authoritative so TV never replaces the
        // first Home request with a duplicate one.
        if (loadJob?.isActive == true || current.isLoading || current.isRefreshing) return
        refresh()
    }

    /**
     * A resume-triggered TV refresh waits for an in-flight Home load instead
     * of dropping the refresh or cancelling that load. If another refresh or
     * session change supersedes the load while waiting, its newer generation
     * already owns reconciliation and this request becomes a no-op.
     */
    suspend fun refreshAfterActiveLoadOnResume() {
        val resumeGeneration = loadGeneration
        while (resumeGeneration == loadGeneration) {
            val activeLoad = loadJob?.takeIf { it.isActive } ?: break
            activeLoad.join()
        }
        if (resumeGeneration == loadGeneration) refreshIfIdle()
    }

    private fun loadHome(refresh: Boolean = false) {
        val generation = ++loadGeneration
        recentlyAddedRetryJobs.values.toList().forEach { it.cancel() }
        recentlyAddedRetryJobs.clear()
        loadJob = viewModelScope.launch {
            // Um aviso de seção pertence à carga que o produziu. Sem esta limpeza, uma
            // falha da TV ao vivo sobrevivia à carga seguinte e aparecia **ao lado** do
            // erro do feed inteiro — os dois cartões juntos, que é exatamente o que a
            // `HomeScreen` documenta como impossível. Vale para as três saídas daqui
            // para baixo: offline, sessão expirada e falha total.
            _state.update {
                it.copy(
                    resumeError = null,
                    nextUpError = null,
                    favoritesError = null,
                    recentlyAddedErrorsByLibrary = emptyMap(),
                    retryingRecentlyAddedLibraryIds = emptySet(),
                    librariesError = null,
                    liveTvError = null,
                )
            }
            // Do not enqueue a request while the monitor already reports the
            // device offline. Reading the current value here also closes the
            // small startup race between the session collector and the
            // connectivity collector. The network transition collector will
            // trigger a fresh load when connectivity returns.
            val userId = currentUserId ?: sessionRepository.getCurrentUserId().first()
            val online = networkMonitor.isOnline.first()
            if (!online) {
                if (isCurrentLoad(generation)) {
                    val cached = userId?.takeIf(String::isNotBlank)
                        ?.let { getHomeFeedUseCase.getCachedHomeSections(it).getOrNull() }
                    _state.update {
                        it.copy(
                            isLoading = false,
                            isRefreshing = false,
                            isOffline = true,
                            heroItem = cached?.resumeItems?.firstOrNull(),
                            resumeItems = cached?.resumeItems.orEmpty(),
                            favoriteItems = cached?.favoriteItems.orEmpty(),
                            nextUpItems = emptyList(),
                            recentlyAddedByLibrary = emptyMap(),
                            recentlyAddedErrorsByLibrary = emptyMap(),
                            liveTvChannels = emptyList(),
                            libraries = emptyList(),
                            cachedAtEpochMillis = listOfNotNull(
                                cached?.resumeSavedAtEpochMillis?.takeIf { it > 0 },
                                cached?.favoritesSavedAtEpochMillis?.takeIf { it > 0 },
                            ).minOrNull(),
                            resumeFromCache = cached?.resumeSavedAtEpochMillis?.let { it > 0 } == true,
                            favoritesFromCache = cached?.favoritesSavedAtEpochMillis?.let { it > 0 } == true,
                            error = null,
                        )
                    }
                }
                return@launch
            }
            if (userId.isNullOrBlank()) {
                if (isCurrentLoad(generation)) {
                    _state.update {
                        it.copy(
                            isLoading = false,
                            isRefreshing = false,
                            error = "Sessão expirada. Entre novamente para carregar sua biblioteca.",
                        )
                    }
                }
                return@launch
            }
            if (isCurrentLoad(generation)) {
                _state.update { it.copy(isLoading = !refresh, error = null) }
            }

            // The profile is secondary to the catalog. Keep it independent from
            // the feed job so a slow avatar request cannot block TV auto-refresh.
            loadUserProfile(userId)
            val result = getHomeFeedUseCase(userId)

            if (!isCurrentLoad(generation)) {
                return@launch
            }
            result.onFailure { error ->
                if (isCurrentLoad(generation)) {
                    _state.update {
                        it.copy(
                            isLoading = false,
                            isRefreshing = false,
                            error = error.userMessage(),
                        )
                    }
                }
            }.onSuccess { feed ->
                if (isCurrentLoad(generation)) {
                    _state.update {
                        it.copy(
                            isLoading = false,
                            isRefreshing = false,
                            heroItem = feed.heroItem,
                            resumeItems = feed.resumeItems,
                            nextUpItems = feed.nextUpItems,
                            favoriteItems = feed.favoriteItems,
                            cachedAtEpochMillis = feed.cachedAtEpochMillis,
                            resumeFromCache = feed.resumeFromCache,
                            favoritesFromCache = feed.favoritesFromCache,
                            recentlyAddedByLibrary = feed.recentlyAddedByLibrary,
                            recentlyAddedErrorsByLibrary = feed.recentlyAddedErrorsByLibrary,
                            liveTvChannels = feed.liveTvChannels,
                            libraries = feed.libraries,
                            error = null,
                            resumeError = feed.resumeError,
                            nextUpError = feed.nextUpError,
                            favoritesError = feed.favoritesError,
                            librariesError = feed.librariesError,
                            liveTvError = feed.liveTvError,
                        )
                    }
                }
            }
        }
    }

    private fun loadUserProfile(userId: String) {
        if (profileJob?.isActive == true) return
        val generation = profileGeneration
        profileJob = viewModelScope.launch {
            val profile = try {
                authRepository.getCurrentUserProfile().getOrNull()
            } catch (cancelled: CancellationException) {
                throw cancelled
            } catch (_: Throwable) {
                null
            }
            if (profile != null && generation == profileGeneration && currentUserId == userId) {
                _state.update { it.copy(userProfile = profile) }
            }
        }
    }

    private fun isCurrentLoad(generation: Long): Boolean = generation == loadGeneration

    private companion object {
        const val MEDIA_REQUEST_DRAFT_SAVE_DEBOUNCE_MILLIS = 250L
    }
}

private object EmptyMediaRequestDraftStore : MediaRequestDraftStore {
    override suspend fun load(scopeHash: String): MediaRequestDraft? = null
    override suspend fun save(scopeHash: String, draft: MediaRequestDraft) = Unit
    override suspend fun clear(scopeHash: String) = Unit
}

private fun Throwable.userMessage(): String = when (this) {
    is java.net.UnknownHostException -> "Servidor indisponível. Verifique a conexão com a rede."
    is java.net.ConnectException -> "Não foi possível conectar ao servidor."
    else -> userFacingMessageOr("Não foi possível carregar o conteúdo do servidor.")
}
