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

interface MediaSuggestionIndexStatus {
    State: string;
    IsIndexing: boolean;
    FailedRootCount: number;
}

type FeedbackFormField = HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement;

let feedbackDialogSequence = 0;

function announceFeedbackStatus(message: string): void {
    let region = document.getElementById('userFeedbackLiveStatus');
    if (!region) {
        region = document.createElement('div');
        region.id = 'userFeedbackLiveStatus';
        region.setAttribute('role', 'status');
        region.setAttribute('aria-live', 'polite');
        region.setAttribute('aria-atomic', 'true');
        region.style.cssText = 'position:absolute;width:1px;height:1px;padding:0;margin:-1px;overflow:hidden;clip:rect(0,0,0,0);white-space:nowrap;border:0';
        document.body.appendChild(region);
    }

    region.textContent = '';
    window.setTimeout(() => {
        region!.textContent = message;
    }, 0);
}

function openFeedbackDialog(apiClient: ApiClient, options: {
    title: string;
    endpoint: string;
    fields: (idPrefix: string) => string;
    payload: (form: HTMLFormElement) => FeedbackPayload;
}): void {
    const dialog = dialogHelper.createDialog({ removeOnClose: true, scrollY: true });
    dialog.classList.add('formDialog');
    const idPrefix = `userFeedback-${++feedbackDialogSequence}`;
    dialog.innerHTML = `<div class="dialogContentInner padded-left padded-right padded-bottom">
        <h2>${escapeHtml(options.title)}</h2>
        <form class="userFeedbackForm" novalidate>
            ${options.fields(idPrefix)}
            <div class="flex justify-content-flex-end padded-top">
                <button is="emby-button" type="button" class="btnCancel button-flat">${escapeHtml(globalize.translate('ButtonCancel'))}</button>
                <button is="emby-button" type="submit" class="button-submit button-accent">${escapeHtml(globalize.translate('ButtonSend'))}</button>
            </div>
        </form>
    </div>`;

    dialog.querySelector('.btnCancel')?.addEventListener('click', () => dialogHelper.close(dialog));
    const form = dialog.querySelector<HTMLFormElement>('form');
    const formError = document.createElement('div');
    formError.className = 'fieldDescription userFeedbackFormError';
    formError.setAttribute('role', 'alert');
    formError.setAttribute('aria-atomic', 'true');
    formError.hidden = true;
    formError.id = `${idPrefix}-form-error`;
    const validationErrors = new Map<FeedbackFormField, HTMLElement>();
    if (form) {
        form.setAttribute('aria-describedby', formError.id);
        const actions = form.querySelector('.flex');
        form.insertBefore(formError, actions);
        form.addEventListener('input', () => {
            formError.hidden = true;
            formError.textContent = '';
        });

        const fields = Array.from(form.querySelectorAll<FeedbackFormField>('input, select, textarea'));
        fields.forEach((field, index) => {
            const error = document.createElement('div');
            error.className = 'fieldDescription userFeedbackFieldError';
            error.id = `${idPrefix}-field-error-${index}`;
            error.setAttribute('role', 'status');
            error.setAttribute('aria-live', 'polite');
            error.setAttribute('aria-atomic', 'true');
            error.hidden = true;
            field.insertAdjacentElement('afterend', error);
            const describedBy = field.getAttribute('aria-describedby');
            field.setAttribute('aria-describedby', [describedBy, error.id].filter(Boolean).join(' '));
            validationErrors.set(field, error);
        });
    }

    const getValidationMessage = (field: FeedbackFormField): string => {
        const titleIsBlank = field.name === 'title' && !field.value.trim();
        if (field.validity.valueMissing || titleIsBlank) {
            return globalize.translate(field.dataset.requiredMessageKey || 'UserFeedbackFieldRequired');
        }
        if (field.validity.rangeUnderflow || field.validity.rangeOverflow || field.validity.stepMismatch) {
            return globalize.translate(field.dataset.rangeMessageKey || 'UserFeedbackFieldInvalid');
        }
        return globalize.translate('UserFeedbackFieldInvalid');
    };
    const updateFieldValidation = (field: FeedbackFormField, force: boolean): boolean => {
        const error = validationErrors.get(field);
        if (!error) return true;

        const titleIsBlank = field.name === 'title' && !field.value.trim();
        const isValid = field.validity.valid && !titleIsBlank;
        if (isValid) {
            error.hidden = true;
            error.textContent = '';
            field.removeAttribute('aria-invalid');
        } else if (force || !error.hidden) {
            error.textContent = getValidationMessage(field);
            error.hidden = false;
            field.setAttribute('aria-invalid', 'true');
        }
        return isValid;
    };
    form?.addEventListener('input', event => {
        const field = event.target;
        if (field instanceof HTMLInputElement || field instanceof HTMLSelectElement || field instanceof HTMLTextAreaElement) {
            updateFieldValidation(field, false);
        }
    });
    form?.addEventListener('change', event => {
        const field = event.target;
        if (field instanceof HTMLInputElement || field instanceof HTMLSelectElement || field instanceof HTMLTextAreaElement) {
            updateFieldValidation(field, false);
        }
    });
    const titleInput = form?.querySelector<HTMLInputElement>('input[name="title"]');
    if (titleInput) {
        const mediaTitleInput = titleInput;
        const mediaTypeSelect = form?.querySelector<HTMLSelectElement>('select[name="mediaType"]');
        const yearInput = form?.querySelector<HTMLInputElement>('input[name="year"]');
        const suggestions = document.createElement('div');
        suggestions.className = 'userFeedbackSuggestions';
        suggestions.hidden = true;
        suggestions.style.cssText = 'position:absolute;z-index:1100;left:0;right:0;top:100%;max-height:240px;overflow:auto;background:#242424;border:1px solid #555;border-radius:4px;box-shadow:0 4px 12px #0008';
        const suggestionList = document.createElement('div');
        suggestionList.id = 'userFeedbackMediaSuggestions';
        suggestionList.setAttribute('role', 'listbox');
        const suggestionStatus = document.createElement('div');
        suggestionStatus.setAttribute('role', 'status');
        suggestionStatus.setAttribute('aria-live', 'polite');
        suggestionStatus.style.cssText = 'padding:10px 12px;color:#bbb';
        suggestions.append(suggestionList, suggestionStatus);
        const titleContainer = titleInput.closest<HTMLElement>('.inputContainer');
        if (titleContainer) {
            titleContainer.style.position = 'relative';
            titleContainer.appendChild(suggestions);
        }

        let debounceTimer: ReturnType<typeof setTimeout> | undefined;
        let indexPollTimer: ReturnType<typeof setTimeout> | undefined;
        let requestSequence = 0;
        let activeIndex = -1;
        const cancelLookup = () => {
            requestSequence++;
            if (debounceTimer) clearTimeout(debounceTimer);
            if (indexPollTimer) clearTimeout(indexPollTimer);
            debounceTimer = undefined;
            indexPollTimer = undefined;
        };
        const hideSuggestions = () => {
            suggestions.hidden = true;
            activeIndex = -1;
            suggestionList.textContent = '';
            suggestionStatus.textContent = '';
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

        const showRetry = (query: string) => {
            suggestionStatus.textContent = '';
            const errorMessage = document.createElement('span');
            errorMessage.textContent = globalize.translate('MediaRequestSuggestionsFailed');
            const retryButton = document.createElement('button');
            retryButton.type = 'button';
            retryButton.className = 'button-flat';
            retryButton.textContent = globalize.translate('Retry');
            retryButton.style.cssText = 'margin-left:8px;color:inherit;background:transparent;border:0;text-decoration:underline;cursor:pointer';
            retryButton.addEventListener('mousedown', event => event.preventDefault());
            retryButton.addEventListener('click', () => {
                cancelLookup();
                titleInput.focus();
                lookupSuggestions(query, requestSequence).catch(error => console.debug('[UserFeedback] suggestion retry failed', error));
            });
            suggestionStatus.append(errorMessage, retryButton);
            suggestions.hidden = false;
            titleInput.setAttribute('aria-expanded', 'true');
        };

        async function pollIndexStatus(query: string, sequence: number): Promise<void> {
            try {
                const status = await apiClient.getJSON(apiClient.getUrl('UserFeedback/MediaSuggestions/Status')) as MediaSuggestionIndexStatus;
                if (sequence !== requestSequence || mediaTitleInput.value.trim() !== query) return;
                if (status.IsIndexing) {
                    suggestionStatus.textContent = globalize.translate('MediaRequestSuggestionsIndexing');
                    indexPollTimer = window.setTimeout(() => {
                        pollIndexStatus(query, sequence).catch(error => console.debug('[UserFeedback] index status retry failed', error));
                    }, 1000);
                    return;
                }
                if (status.State === 'Error') {
                    showRetry(query);
                    return;
                }

                lookupSuggestions(query, sequence, false, status.FailedRootCount).catch(error => console.debug('[UserFeedback] refreshed suggestion lookup failed', error));
            } catch (error: unknown) {
                if (sequence === requestSequence && mediaTitleInput.value.trim() === query) {
                    console.debug('[UserFeedback] STRM index status failed', error);
                    showRetry(query);
                }
            }
        }

        titleInput.setAttribute('role', 'combobox');
        titleInput.setAttribute('aria-autocomplete', 'list');
        titleInput.setAttribute('aria-controls', suggestionList.id);
        titleInput.setAttribute('aria-expanded', 'false');

        async function lookupSuggestions(query: string, sequence: number, checkIndex = true, failedRootCount = 0): Promise<void> {
            suggestions.hidden = false;
            suggestionList.textContent = '';
            suggestionStatus.textContent = globalize.translate('MediaRequestSuggestionsLoading');
            mediaTitleInput.setAttribute('aria-expanded', 'true');

            try {
                const response = await apiClient.getJSON(apiClient.getUrl(`UserFeedback/MediaSuggestions?query=${encodeURIComponent(query)}&limit=10`));
                const results = response as MediaSuggestion[];
                if (sequence !== requestSequence || mediaTitleInput.value.trim() !== query) return;
                if (!Array.isArray(results)) throw new Error('Invalid media suggestions response');

                if (results.length === 0) {
                    if (checkIndex) {
                        pollIndexStatus(query, sequence).catch(error => console.debug('[UserFeedback] index status lookup failed', error));
                    } else {
                        suggestionStatus.textContent = failedRootCount > 0 ?
                            globalize.translate('MediaRequestSuggestionsPartialIndex') :
                            globalize.translate('MediaRequestSuggestionsEmpty');
                    }
                } else {
                    suggestionStatus.textContent = '';
                }
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
                    suggestionList.appendChild(option);
                });
            } catch (error: unknown) {
                if (sequence === requestSequence && mediaTitleInput.value.trim() === query) {
                    showRetry(query);
                }
                console.debug('[UserFeedback] suggestion lookup failed', error);
            }
        }

        titleInput.addEventListener('input', () => {
            cancelLookup();
            hideSuggestions();
            const query = titleInput.value.trim();
            if (query.length < 2) {
                return;
            }

            const sequence = requestSequence;
            debounceTimer = setTimeout(() => {
                void lookupSuggestions(query, sequence);
            }, 250);
        });

        const updateActiveOption = () => {
            const suggestionOptions = Array.from(suggestionList.querySelectorAll<HTMLElement>('[role="option"]'));
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
            const suggestionOptions = suggestionList.querySelectorAll<HTMLElement>('[role="option"]');
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
        const scheduleHideIfFocusLeavesPopup = () => {
            const sequence = requestSequence;
            window.setTimeout(() => {
                const activeElement = document.activeElement;
                if (sequence === requestSequence && activeElement !== titleInput && !suggestions.contains(activeElement)) {
                    hideSuggestions();
                }
            }, 120);
        };
        titleInput.addEventListener('blur', event => {
            const nextFocus = event.relatedTarget;
            if (nextFocus instanceof Node && suggestions.contains(nextFocus)) return;
            cancelLookup();
            scheduleHideIfFocusLeavesPopup();
        });
        suggestions.addEventListener('focusout', event => {
            const nextFocus = event.relatedTarget;
            if (nextFocus instanceof Node && suggestions.contains(nextFocus)) return;
            cancelLookup();
            scheduleHideIfFocusLeavesPopup();
        });
        dialog.addEventListener('close', () => {
            cancelLookup();
            hideSuggestions();
        }, { once: true });
    }

    form?.addEventListener('submit', (event) => {
        event.preventDefault();
        formError.hidden = true;
        formError.textContent = '';
        const invalidFields = Array.from(form.querySelectorAll<FeedbackFormField>('input, select, textarea'))
            .filter(field => !updateFieldValidation(field, true));
        if (invalidFields.length > 0) {
            invalidFields[0].focus();
            return;
        }

        const submitButton = form.querySelector<HTMLButtonElement>('button[type="submit"]');
        if (submitButton) submitButton.disabled = true;

        void apiClient.ajax({
            type: 'POST',
            url: apiClient.getUrl(options.endpoint),
            data: JSON.stringify(options.payload(form)),
            contentType: 'application/json'
        } as never).then(() => {
            dialogHelper.close(dialog);
            const successMessage = globalize.translate('UserFeedbackSent');
            announceFeedbackStatus(successMessage);
            toast(successMessage);
        }).catch((error: unknown) => {
            console.error('[UserFeedback] submit failed', error);
            formError.textContent = globalize.translate('UserFeedbackSendFailed');
            formError.hidden = false;
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
        fields: (idPrefix) => `<div class="inputContainer"><label class="inputLabel" for="${idPrefix}-title">${escapeHtml(globalize.translate('MediaRequestName'))}</label><input id="${idPrefix}-title" class="emby-input" name="title" maxlength="200" required data-required-message-key="MediaRequestTitleRequired" autocomplete="off" aria-label="${escapeHtml(globalize.translate('MediaRequestName'))}" placeholder="${escapeHtml(globalize.translate('MediaRequestName'))}" /></div>
            <div class="selectContainer"><label class="selectLabel" for="${idPrefix}-media-type">${escapeHtml(globalize.translate('MediaType'))}</label><select id="${idPrefix}-media-type" is="emby-select" name="mediaType" class="emby-select" required data-required-message-key="MediaRequestTypeRequired" aria-label="${escapeHtml(globalize.translate('MediaType'))}">
                <option value="" selected disabled>${escapeHtml(globalize.translate('MediaRequestTypeSelect'))}</option>
                <option value="Movie">${escapeHtml(globalize.translate('MediaRequestTypeMovie'))}</option>
                <option value="Series">${escapeHtml(globalize.translate('MediaRequestTypeSeries'))}</option>
                <option value="Animation">${escapeHtml(globalize.translate('MediaRequestTypeAnimation'))}</option>
                <option value="Novel">${escapeHtml(globalize.translate('MediaRequestTypeNovel'))}</option>
                <option value="Dorama">${escapeHtml(globalize.translate('MediaRequestTypeDorama'))}</option>
                <option value="Other">${escapeHtml(globalize.translate('Other'))}</option>
            </select></div>
            <div class="inputContainer"><label class="inputLabel" for="${idPrefix}-year">${escapeHtml(globalize.translate('LabelYear'))}</label><input id="${idPrefix}-year" class="emby-input" name="year" type="number" min="1888" max="2200" data-range-message-key="MediaRequestYearRangeInvalid" aria-label="${escapeHtml(globalize.translate('LabelYear'))}" placeholder="${escapeHtml(globalize.translate('LabelYear'))}" /></div>
            <div class="inputContainer"><label class="inputLabel" for="${idPrefix}-notes">${escapeHtml(globalize.translate('MediaRequestNotes'))}</label><textarea id="${idPrefix}-notes" is="emby-textarea" class="emby-textarea" name="notes" maxlength="1000" aria-label="${escapeHtml(globalize.translate('MediaRequestNotes'))}" placeholder="${escapeHtml(globalize.translate('MediaRequestNotes'))}"></textarea></div>`,
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
        fields: (idPrefix) => `<p>${escapeHtml(item.Name || '')}</p>
            <div class="selectContainer"><label class="selectLabel" for="${idPrefix}-category">${escapeHtml(globalize.translate('LabelType'))}</label><select id="${idPrefix}-category" is="emby-select" name="category" class="emby-select" required data-required-message-key="PlaybackIssueCategoryRequired" aria-label="${escapeHtml(globalize.translate('LabelType'))}">
                <option value="" selected disabled>${escapeHtml(globalize.translate('PlaybackIssueCategorySelect'))}</option>
                <option value="Playback">${escapeHtml(globalize.translate('PlaybackIssueCategoryPlayback'))}</option>
                <option value="MissingMedia">${escapeHtml(globalize.translate('PlaybackIssueCategoryMedia'))}</option>
                <option value="WrongMetadata">${escapeHtml(globalize.translate('PlaybackIssueCategoryMetadata'))}</option>
                <option value="Other">${escapeHtml(globalize.translate('Other'))}</option>
            </select></div>
            <div class="inputContainer"><label class="inputLabel" for="${idPrefix}-details">${escapeHtml(globalize.translate('PlaybackIssueDetails'))}</label><textarea id="${idPrefix}-details" is="emby-textarea" class="emby-textarea" name="description" maxlength="1000" aria-label="${escapeHtml(globalize.translate('PlaybackIssueDetails'))}" placeholder="${escapeHtml(globalize.translate('PlaybackIssueDetails'))}"></textarea></div>`,
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
