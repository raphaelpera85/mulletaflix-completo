
import globalize from 'lib/globalize';
import { ServerConnections } from 'lib/jellyfin-apiclient';
import { OutboundWebSocketMessageType } from '@jellyfin/sdk/lib/websocket';
import escapeHtml from 'escape-html';

import 'elements/emby-button/emby-button';

interface ScheduledTask {
    Key: string;
    State: string;
    Id: string;
    CurrentProgressPercentage?: number;
    LastExecutionResult?: {
        Status: string;
    };
}

interface TaskApiClient {
    serverId(): string;
    startScheduledTask(id: string): void | Promise<void>;
    subscribe(
        messageTypes: unknown[],
        onMessage: (message: { Data?: ScheduledTask[] }) => void
    ): { close: () => void };
}

interface HtmlResultElement {
    html(content: string): void;
}

interface TaskButtonOptions {
    button: HTMLElement;
    taskKey: string;
    panel?: HTMLElement;
    progressElem?: HTMLProgressElement;
    lastResultElem?: HtmlResultElement;
    mode?: string;
}

function taskbutton(options: TaskButtonOptions): void {
    const button = options.button;
    const currentApiClient = ServerConnections.currentApiClient() as unknown as { serverId?: () => string } | undefined;
    const serverId = currentApiClient?.serverId?.() || '';
    let subscription: { close: () => void } | null = null;

    function getApiClient(): TaskApiClient {
        return ServerConnections.getApiClient(serverId) as unknown as TaskApiClient;
    }

    function updatePanel(task: ScheduledTask | undefined): void {
        options.panel?.classList.toggle('hide', !task);
    }

    function updateButtonState(task: ScheduledTask): void {
        button.toggleAttribute('disabled', task.State !== 'Idle');
        button.setAttribute('data-taskid', task.Id);
        options.progressElem?.classList.toggle('hide', task.State !== 'Running');
        if (options.progressElem) {
            options.progressElem.value = Number((task.CurrentProgressPercentage || 0).toFixed(1));
        }
    }

    function renderLastResult(task: ScheduledTask): void {
        if (!options.lastResultElem) {
            return;
        }

        const lastResult = task.LastExecutionResult?.Status || '';
        const translatedResults: Record<string, string> = {
            Failed: `<span style="color:#FF0000;">(${globalize.translate('LabelFailed')})</span>`,
            Cancelled: `<span style="color:#0026FF;">(${globalize.translate('LabelCancelled')})</span>`,
            Aborted: `<span style="color:#FF0000;">${globalize.translate('LabelAbortedByServerShutdown')}</span>`
        };

        options.lastResultElem.html(translatedResults[lastResult] || escapeHtml(lastResult));
    }

    function updateTasks(tasks: ScheduledTask[]): void {
        const task = tasks.find((candidate) => candidate.Key === options.taskKey);
        updatePanel(task);

        if (!task) {
            return;
        }

        updateButtonState(task);
        renderLastResult(task);
    }

    function onScheduledTaskMessageConfirmed(id: string): void {
        Promise.resolve(getApiClient().startScheduledTask(id)).catch((error: unknown) => {
            console.error('Unable to start scheduled task', error);
        });
    }

    function onButtonClick(this: HTMLElement): void {
        onScheduledTaskMessageConfirmed(this.getAttribute('data-taskid')!);
    }

    function onScheduledTasksUpdate({ Data }: { Data?: ScheduledTask[] }): void {
        if (getApiClient().serverId() === serverId) {
            updateTasks(Data ?? []);
        }
    }

    function subscribe(): { close: () => void } {
        return getApiClient().subscribe([OutboundWebSocketMessageType.ScheduledTasksInfo], onScheduledTasksUpdate);
    }

    function startSubscription(): void {
        if (subscription) {
            subscription.close();
        }
        subscription = subscribe();
    }

    function stopSubscription(): void {
        if (subscription) {
            subscription.close();
            subscription = null;
        }
    }

    if (options.panel) {
        options.panel.classList.add('hide');
    }

    if (options.mode == 'off') {
        button.removeEventListener('click', onButtonClick);
        stopSubscription();
    } else {
        button.addEventListener('click', onButtonClick);
        startSubscription();
    }
}

export default taskbutton;
