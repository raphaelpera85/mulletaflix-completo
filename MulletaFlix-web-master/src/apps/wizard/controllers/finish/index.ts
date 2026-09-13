import { withLoading } from 'components/loading/loading';
import { ServerConnections } from 'lib/jellyfin-apiclient';

interface WizardApiClient {
    ajax(options: { url: string; type: 'POST' }): Promise<void>;
    getUrl(path: string): string;
}

async function onFinish(): Promise<void> {
    const apiClient = ServerConnections.currentApiClient() as (ReturnType<typeof ServerConnections.currentApiClient> & WizardApiClient) | undefined;
    if (!apiClient) {
        return;
    }

    try {
        await withLoading(() => apiClient.ajax({
            url: apiClient.getUrl('Startup/Complete'),
            type: 'POST'
        }));
        window.location.href = '';
    } catch (error) {
        console.error('[Wizard > Finish] failed to complete startup', error);
    }
}

export default function (view: HTMLElement): void {
    const nextButton = view.querySelector('.btnWizardNext');
    if (nextButton) {
        nextButton.addEventListener('click', () => {
            onFinish().catch((error: unknown) => console.error('[Wizard > Finish] unexpected completion error', error));
        });
    }
}
