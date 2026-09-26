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
        fields: `<div class="inputContainer"><input class="emby-input" name="title" maxlength="200" required placeholder="${escapeHtml(globalize.translate('MediaRequestName'))}" /></div>
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
