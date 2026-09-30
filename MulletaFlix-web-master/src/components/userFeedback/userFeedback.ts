import type { ApiClient } from 'jellyfin-apiclient';
import escapeHtml from 'escape-html';

import dialogHelper from 'components/dialogHelper/dialogHelper';
import toast from 'components/toast/toast';
import globalize from 'lib/globalize';

interface FeedbackPayload {
    Title?: string;
    MediaType?: string;
    Year?: number;
    Notes?: string;
    ItemId?: string;
    Category?: string;
    Description?: string;
}

interface MediaSuggestion {
    Title: string;
    MediaType: string;
    Year?: number;
}

function openFeedbackDialog(apiClient: ApiClient, options: {
    title: string;
    endpoint: string;
    fields: string;
    payload: (form: HTMLFormElement) => FeedbackPayload;
}): void {
    const dialog = dialogHelper.createDialog({ removeOnClose: true, scrollY: true });
    dialog.classList.add('formDialog');
    dialog.innerHTML = `<div class="dialogContentInner padded-left padded-right padded-bottom">
        <h2>${escapeHtml(options.title)}</h2>
        <form class="userFeedbackForm">
            ${options.fields}
            <div class="flex justify-content-flex-end padded-top">
                <button is="emby-button" type="button" class="btnCancel button-flat">${escapeHtml(globalize.translate('ButtonCancel'))}</button>
                <button is="emby-button" type="submit" class="button-submit button-accent">${escapeHtml(globalize.translate('ButtonSend'))}</button>
            </div>
        </form>
    </div>`;

    dialog.querySelector('.btnCancel')?.addEventListener('click', () => dialogHelper.close(dialog));
    const form = dialog.querySelector<HTMLFormElement>('form');
    const titleInput = form?.querySelector<HTMLInputElement>('input[name="title"]');
    if (titleInput) {
        const mediaTypeSelect = form?.querySelector<HTMLSelectElement>('select[name="mediaType"]');
        const yearInput = form?.querySelector<HTMLInputElement>('input[name="year"]');
        const suggestions = document.createElement('div');
        suggestions.className = 'userFeedbackSuggestions';
        suggestions.id = 'userFeedbackMediaSuggestions';
        suggestions.setAttribute('role', 'listbox');
        suggestions.hidden = true;
        suggestions.style.cssText = 'position:absolute;z-index:1100;left:0;right:0;top:100%;max-height:240px;overflow:auto;background:#242424;border:1px solid #555;border-radius:4px;box-shadow:0 4px 12px #0008';
        const titleContainer = titleInput.closest<HTMLElement>('.inputContainer');
        if (titleContainer) {
            titleContainer.style.position = 'relative';
            titleContainer.appendChild(suggestions);
        }

        let debounceTimer: ReturnType<typeof setTimeout> | undefined;
        let requestSequence = 0;
        let activeIndex = -1;
        const cancelLookup = () => {
            requestSequence++;
            if (debounceTimer) clearTimeout(debounceTimer);
            debounceTimer = undefined;
        };
        const hideSuggestions = () => {
            suggestions.hidden = true;
            suggestions.textContent = '';
            activeIndex = -1;
            titleInput.removeAttribute('aria-activedescendant');
            titleInput.setAttribute('aria-expanded', 'false');
        };
        const chooseSuggestion = (suggestion: MediaSuggestion) => {
            cancelLookup();
            titleInput.value = suggestion.Title;
            if (mediaTypeSelect && Array.from(mediaTypeSelect.options).some(option => option.value === suggestion.MediaType)) {
                mediaTypeSelect.value = suggestion.MediaType;
                mediaTypeSelect.dispatchEvent(new Event('change', { bubbles: true }));
            }
            if (yearInput) yearInput.value = suggestion.Year ? String(suggestion.Year) : '';
            hideSuggestions();
        };

        titleInput.setAttribute('role', 'combobox');
        titleInput.setAttribute('aria-autocomplete', 'list');
        titleInput.setAttribute('aria-controls', suggestions.id);
        titleInput.setAttribute('aria-expanded', 'false');
        titleInput.addEventListener('input', () => {
            cancelLookup();
            hideSuggestions();
            const query = titleInput.value.trim();
            if (query.length < 2) {
                return;
            }

            const sequence = requestSequence;
            debounceTimer = setTimeout(() => {
                void apiClient.getJSON(apiClient.getUrl(`UserFeedback/MediaSuggestions?query=${encodeURIComponent(query)}&limit=10`))
                    .then((response: unknown) => {
                        const results = response as MediaSuggestion[];
                        if (sequence !== requestSequence || titleInput.value.trim() !== query || !Array.isArray(results)) return;
                        suggestions.textContent = '';
                        results.forEach((suggestion, index) => {
                            const option = document.createElement('button');
                            option.type = 'button';
                            option.id = `media-suggestion-${sequence}-${index}`;
                            option.setAttribute('role', 'option');
                            option.setAttribute('aria-selected', 'false');
                            option.style.cssText = 'display:block;width:100%;padding:10px 12px;text-align:left;color:inherit;background:transparent;border:0;cursor:pointer';
                            const details = [suggestion.MediaType, suggestion.Year].filter(Boolean).join(' · ');
                            option.textContent = details ? `${suggestion.Title} — ${details}` : suggestion.Title;
                            option.addEventListener('mouseenter', () => {
                                activeIndex = index;
                                updateActiveOption();
                            });
                            option.addEventListener('mousedown', event => event.preventDefault());
                            option.addEventListener('click', () => chooseSuggestion(suggestion));
                            suggestions.appendChild(option);
                        });
                        suggestions.hidden = results.length === 0;
                        titleInput.setAttribute('aria-expanded', String(results.length > 0));
                        if (results.length === 0) titleInput.removeAttribute('aria-activedescendant');
                    })
                    .catch((error: unknown) => {
                        if (sequence === requestSequence) hideSuggestions();
                        console.debug('[UserFeedback] suggestion lookup failed', error);
                    });
            }, 250);
        });

        const updateActiveOption = () => {
            const suggestionOptions = Array.from(suggestions.querySelectorAll<HTMLElement>('[role="option"]'));
            suggestionOptions.forEach((option, index) => {
                option.setAttribute('aria-selected', String(index === activeIndex));
                option.style.background = index === activeIndex ? '#343434' : 'transparent';
            });
            const active = suggestionOptions[activeIndex];
            if (active) {
                titleInput.setAttribute('aria-activedescendant', active.id);
                active.scrollIntoView({ block: 'nearest' });
            } else {
                titleInput.removeAttribute('aria-activedescendant');
            }
        };

        titleInput.addEventListener('keydown', event => {
            if (event.key === 'Escape') {
                cancelLookup();
                hideSuggestions();
                return;
            }
            const suggestionOptions = suggestions.querySelectorAll<HTMLElement>('[role="option"]');
            if (suggestions.hidden || suggestionOptions.length === 0) return;
            if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
                event.preventDefault();
                activeIndex = (activeIndex + (event.key === 'ArrowDown' ? 1 : -1) + suggestionOptions.length) % suggestionOptions.length;
                updateActiveOption();
            } else if (event.key === 'Enter' && activeIndex >= 0) {
                event.preventDefault();
                (suggestionOptions[activeIndex] as HTMLButtonElement).click();
            }
        });
        titleInput.addEventListener('blur', () => {
            cancelLookup();
            const sequence = requestSequence;
            window.setTimeout(() => {
                if (sequence === requestSequence) hideSuggestions();
            }, 120);
        });
        dialog.addEventListener('close', () => {
            cancelLookup();
            hideSuggestions();
        }, { once: true });
    }

    form?.addEventListener('submit', (event) => {
        event.preventDefault();
        if (!form.reportValidity()) return;

        const submitButton = form.querySelector<HTMLButtonElement>('button[type="submit"]');
        if (submitButton) submitButton.disabled = true;

        void apiClient.ajax({
            type: 'POST',
            url: apiClient.getUrl(options.endpoint),
            data: JSON.stringify(options.payload(form)),
            contentType: 'application/json'
        } as never).then(() => {
            dialogHelper.close(dialog);
            toast(globalize.translate('UserFeedbackSent'));
        }).catch((error: unknown) => {
            console.error('[UserFeedback] submit failed', error);
            toast(globalize.translate('UserFeedbackSendFailed'));
            if (submitButton) submitButton.disabled = false;
        });
    });

    void dialogHelper.open(dialog).catch((error: unknown) => {
        console.error('[UserFeedback] dialog failed to open', error);
    });
}

export function showMediaRequestDialog(apiClient: ApiClient): void {
    openFeedbackDialog(apiClient, {
        title: globalize.translate('MediaRequestTitle'),
        endpoint: 'UserFeedback/MediaRequests',
        fields: `<div class="inputContainer"><input class="emby-input" name="title" maxlength="200" required autocomplete="off" placeholder="${escapeHtml(globalize.translate('MediaRequestName'))}" /></div>
            <div class="selectContainer"><select is="emby-select" name="mediaType" class="emby-select" required aria-label="${escapeHtml(globalize.translate('MediaType'))}">
                <option value="Movie">${escapeHtml(globalize.translate('MediaRequestTypeMovie'))}</option>
                <option value="Series">${escapeHtml(globalize.translate('MediaRequestTypeSeries'))}</option>
                <option value="Animation">${escapeHtml(globalize.translate('MediaRequestTypeAnimation'))}</option>
                <option value="Novel">${escapeHtml(globalize.translate('MediaRequestTypeNovel'))}</option>
                <option value="Dorama">${escapeHtml(globalize.translate('MediaRequestTypeDorama'))}</option>
                <option value="Other">${escapeHtml(globalize.translate('Other'))}</option>
            </select></div>
            <div class="inputContainer"><input class="emby-input" name="year" type="number" min="1888" max="2200" placeholder="${escapeHtml(globalize.translate('LabelYear'))}" /></div>
            <div class="inputContainer"><textarea is="emby-textarea" class="emby-textarea" name="notes" maxlength="1000" placeholder="${escapeHtml(globalize.translate('LabelOverview'))}"></textarea></div>`,
        payload: (form) => {
            const data = new FormData(form);
            const rawYear = String(data.get('year') || '').trim();
            return {
                Title: String(data.get('title') || '').trim(),
                MediaType: String(data.get('mediaType') || ''),
                Year: rawYear ? Number(rawYear) : undefined,
                Notes: String(data.get('notes') || '').trim() || undefined
            };
        }
    });
}

export function showPlaybackIssueDialog(apiClient: ApiClient, item: { Id: string; Name?: string }): void {
    openFeedbackDialog(apiClient, {
        title: globalize.translate('PlaybackIssueTitle'),
        endpoint: 'UserFeedback/PlaybackIssues',
        fields: `<p>${escapeHtml(item.Name || '')}</p>
            <div class="selectContainer"><select is="emby-select" name="category" class="emby-select" required aria-label="${escapeHtml(globalize.translate('LabelType'))}">
                <option value="Playback">${escapeHtml(globalize.translate('PlaybackIssueCategoryPlayback'))}</option>
                <option value="MissingMedia">${escapeHtml(globalize.translate('PlaybackIssueCategoryMedia'))}</option>
                <option value="WrongMetadata">${escapeHtml(globalize.translate('PlaybackIssueCategoryMetadata'))}</option>
                <option value="Other">${escapeHtml(globalize.translate('Other'))}</option>
            </select></div>
            <div class="inputContainer"><textarea is="emby-textarea" class="emby-textarea" name="description" maxlength="1000" placeholder="${escapeHtml(globalize.translate('PlaybackIssueDetails'))}"></textarea></div>`,
        payload: (form) => {
            const data = new FormData(form);
            return {
                ItemId: item.Id,
                Category: String(data.get('category') || ''),
                Description: String(data.get('description') || '').trim() || undefined
            };
        }
    });
}
