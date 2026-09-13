import { withLoading } from 'components/loading/loading';
import toast from 'components/toast/toast';
import globalize from 'lib/globalize';
import { ServerConnections } from 'lib/jellyfin-apiclient';
import Dashboard from 'utils/dashboard';

import 'styles/dashboard.scss';
import 'elements/emby-input/emby-input';
import 'elements/emby-button/emby-button';

interface WizardUserPage extends HTMLElement {
    querySelector<T extends Element = Element>(selectors: string): T | null;
}

interface WizardUserApiClient {
    ajax(request: {
        type: string;
        data: string;
        url: string;
        contentType: string;
    }): Promise<void>;
    getUrl(path: string): string;
    getJSON(url: string): Promise<{ Name?: string; Password?: string }>;
}

function nextWizardPage(): void {
    Dashboard.navigate('wizard/library')
        .catch(err => {
            console.error('[Wizard > User] error navigating to library setup', err);
        });
}

function onUpdateUserComplete(result: unknown): void {
    console.debug('[Wizard > User] user update complete:', result);
    nextWizardPage();
}

async function onUpdateUserError(error: unknown): Promise<void> {
    let message = 'unknown error';

    if (typeof Response !== 'undefined' && error instanceof Response) {
        message = await error.text();
    } else if (error instanceof Error) {
        message = error.message;
    } else if (typeof error === 'string') {
        message = error;
    }

    console.warn('[Wizard > User] user update failed:', message);
    toast(globalize.translate('ErrorDefault'));
}

function submit(form: WizardUserPage): void {
    const apiClient = ServerConnections.currentApiClient() as unknown as WizardUserApiClient;
    const usernameInput = form.querySelector<HTMLInputElement>('#txtUsername');
    const passwordInput = form.querySelector<HTMLInputElement>('#txtManualPassword');

    void withLoading(() => apiClient.ajax({
        type: 'POST',
        data: JSON.stringify({
            Name: usernameInput?.value.trim() || '',
            Password: passwordInput?.value || ''
        }),
        url: apiClient.getUrl('Startup/User'),
        contentType: 'application/json'
    }))
        .then(onUpdateUserComplete)
        .catch(onUpdateUserError);
}

function onSubmit(this: HTMLFormElement, e: SubmitEvent): boolean {
    const password = this.querySelector<HTMLInputElement>('#txtManualPassword')?.value || '';
    const confirmPassword = this.querySelector<HTMLInputElement>('#txtPasswordConfirm')?.value || '';

    if (password != confirmPassword) {
        toast(globalize.translate('PasswordMatchError'));
    } else {
        submit(this.parentElement as WizardUserPage);
    }

    e.preventDefault();
    return false;
}

function onViewShow(this: WizardUserPage): void {
    const apiClient = ServerConnections.currentApiClient() as unknown as WizardUserApiClient;
    void withLoading(() => apiClient.getJSON(apiClient.getUrl('Startup/User'))).then((user) => {
        const usernameInput = this.querySelector<HTMLInputElement>('#txtUsername');
        const manualPasswordInput = this.querySelector<HTMLInputElement>('#txtManualPassword');

        if (usernameInput) {
            usernameInput.value = user.Name || '';
        }
        if (manualPasswordInput) {
            manualPasswordInput.value = user.Password || '';
        }
    }).catch((error: unknown) => {
        console.error('[Wizard > User] failed to load user settings', error);
    });
}

export default function (view: WizardUserPage): void {
    view.querySelector<HTMLFormElement>('.wizardUserForm')?.addEventListener('submit', onSubmit);
    view.addEventListener('viewshow', function () {
        document.querySelector('.skinHeader')?.classList.add('noHomeButtonHeader');
    });
    view.addEventListener('viewhide', function () {
        document.querySelector('.skinHeader')?.classList.remove('noHomeButtonHeader');
    });
    view.addEventListener('viewshow', onViewShow);
}
