/* eslint-disable @typescript-eslint/no-explicit-any, @typescript-eslint/no-this-alias */
import dialogHelper from '../dialogHelper/dialogHelper';
import globalize from '../../lib/globalize';
import { ServerConnections } from 'lib/jellyfin-apiclient';
import layoutManager from '../layoutManager';
import loading from '../loading/loading';
import scrollHelper from '../../scripts/scrollHelper';

import '../../styles/scrollstyles.scss';
import '../../elements/emby-button/emby-button';
import '../../elements/emby-collapse/emby-collapse';
import '../../elements/emby-input/emby-input';
import '../../elements/emby-button/paper-icon-button-light';
import '../formdialog.scss';
import './recordingcreator.scss';
import 'material-design-icons-iconfont';
import '../../styles/flexstyles.scss';
import template from './recordingeditor.template.html';

let currentDialog: HTMLDivElement | null;
let recordingDeleted = false;
let currentItemId: string;
let currentServerId: string;
let currentResolve: (value?: any) => void;

function deleteTimer(apiClient: any, timerId: string): Promise<void> {
    return import('./recordinghelper').then(({ default: recordingHelper }) => {
        return recordingHelper.cancelTimerWithConfirmation(timerId, apiClient.serverId());
    });
}

function renderTimer(context: Element, item: any): void {
    (context.querySelector('#txtPrePaddingMinutes') as HTMLInputElement).value = String(item.PrePaddingSeconds / 60);
    (context.querySelector('#txtPostPaddingMinutes') as HTMLInputElement).value = String(item.PostPaddingSeconds / 60);
}

function closeDialog(isDeleted: boolean): void {
    recordingDeleted = isDeleted;
    if (currentDialog) {
        dialogHelper.close(currentDialog);
    }
}

function onSubmit(this: any, e: Event): void {
    const form = this;

    const apiClient = ServerConnections.getApiClient(currentServerId) as any;

    void loading.withLoading(async () => {
        try {
            const item = await apiClient.getLiveTvTimer(currentItemId);
            item.PrePaddingSeconds = Number((form.querySelector('#txtPrePaddingMinutes') as HTMLInputElement).value) * 60;
            item.PostPaddingSeconds = Number((form.querySelector('#txtPostPaddingMinutes') as HTMLInputElement).value) * 60;
            await apiClient.updateLiveTvTimer(item);
            currentResolve();
        } catch (error) {
            console.error('[RecordingEditor] failed to update timer', error);
        }
    });

    e.preventDefault();

    // Disable default form submission
    return undefined as unknown as void;
}

function init(context: Element): void {
    context.querySelector('.btnCancel')!.addEventListener('click', function () {
        closeDialog(false);
    });

    context.querySelector('.btnCancelRecording')!.addEventListener('click', function () {
        const apiClient = ServerConnections.getApiClient(currentServerId) as any;

        void loading.withLoading(async () => {
            try {
                await deleteTimer(apiClient, currentItemId);
                closeDialog(true);
            } catch (error) {
                console.error('[RecordingEditor] failed to delete timer', error);
            }
        });
    });

    context.querySelector('form')!.addEventListener('submit', onSubmit);
}

function reload(context: Element, id: string): void {
    currentItemId = id;

    const apiClient = ServerConnections.getApiClient(currentServerId) as any;
    void loading.withLoading(async () => {
        try {
            const result = await apiClient.getLiveTvTimer(id);
            renderTimer(context, result);
        } catch (error) {
            console.error('[RecordingEditor] failed to load timer', error);
        }
    });
}

function showEditor(itemId: string, serverId: string, options?: { enableCancel?: boolean }): Promise<any> {
    return new Promise(function (resolve) {
        recordingDeleted = false;
        currentServerId = serverId;
        options = options || {};
        currentResolve = resolve;

        const dialogOptions: Record<string, any> = {
            removeOnClose: true,
            scrollY: false
        };

        if (layoutManager.tv) {
            dialogOptions.size = 'fullscreen';
        }

        const dlg = dialogHelper.createDialog(dialogOptions);

        dlg.classList.add('formDialog');
        dlg.classList.add('recordingDialog');

        if (!layoutManager.tv) {
            dlg.style.minWidth = '20%';
            dlg.classList.add('dialog-fullscreen-lowres');
        }

        let html = '';

        html += globalize.translateHtml(template, 'core');

        dlg.innerHTML = html;

        if (options.enableCancel === false) {
            dlg.querySelector('.formDialogFooter')!.classList.add('hide');
        }

        currentDialog = dlg;

        dlg.addEventListener('closing', function () {
            if (!recordingDeleted) {
                (dlg.querySelector('.btnSubmit') as HTMLButtonElement).click();
            }
        });

        dlg.addEventListener('close', function () {
            if (recordingDeleted) {
                resolve({
                    updated: true,
                    deleted: true
                });
            }
        });

        if (layoutManager.tv) {
            scrollHelper.centerFocus.on(dlg.querySelector('.formDialogContent') as HTMLElement, false);
        }

        init(dlg);

        reload(dlg, itemId);

        dialogHelper.open(dlg).catch((error: unknown) => {
            console.error('[RecordingEditor] failed to open dialog', error);
        });
    });
}

export default {
    show: showEditor
};

/* eslint-enable @typescript-eslint/no-explicit-any, @typescript-eslint/no-this-alias */
