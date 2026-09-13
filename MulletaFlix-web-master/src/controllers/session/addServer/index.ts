import loading from 'components/loading/loading';
import globalize from 'lib/globalize';
import { ConnectionState, ServerConnections } from 'lib/jellyfin-apiclient';
import type { ConnectResult } from 'lib/jellyfin-apiclient/connectionManager';
import appSettings from 'scripts/settings/appSettings';
import Dashboard from 'utils/dashboard';

import 'elements/emby-button/emby-button';

function observeDashboardOperation(operation: unknown, label: string): void {
    void Promise.resolve(operation).catch((error: unknown) => {
        console.error(`[AddServer] failed to ${label}`, error);
    });
}

function handleConnectionResult(page: HTMLElement, result: ConnectResult): void {
    switch (result.State) {
        case ConnectionState.SignedIn: {
            const apiClient = result.ApiClient!;
            Dashboard.onServerChanged(apiClient.getCurrentUserId()!, apiClient.accessToken()!, apiClient as never);
            observeDashboardOperation(Dashboard.navigate('home'), 'navigate home');
            break;
        }
        case ConnectionState.ServerSignIn:
            if (result.SystemInfo?.StartupWizardCompleted) {
                observeDashboardOperation(Dashboard.navigate('login?serverid=' + result.Servers![0].Id), 'navigate to login');
            } else {
                observeDashboardOperation(Dashboard.navigate('/wizard/start'), 'navigate to wizard');
            }
            break;
        case ConnectionState.ServerSelection:
            observeDashboardOperation(Dashboard.navigate('selectserver'), 'navigate to server selection');
            break;
        case ConnectionState.ServerUpdateNeeded:
            observeDashboardOperation(Dashboard.alert({
                message: globalize.translate('ServerUpdateNeeded', '<a href="https://github.com/MulletaFlix/MulletaFlix">https://github.com/MulletaFlix/MulletaFlix</a>')
            }), 'show update alert');
            break;
        case ConnectionState.Unavailable:
            observeDashboardOperation(Dashboard.alert({
                message: globalize.translate('MessageUnableToConnectToServer'),
                title: globalize.translate('HeaderConnectionFailure')
            }), 'show connection alert');
    }
}

function submitServer(page: HTMLElement): void {
    let host = (page.querySelector('#txtServerHost') as HTMLInputElement).value;
    while (host.endsWith('/')) {
        host = host.slice(0, -1);
    }
    void loading.withLoading(() => ServerConnections.connectToAddress(host, {
        enableAutoLogin: appSettings.enableAutoLogin()
    })).then(function(result: ConnectResult) {
        handleConnectionResult(page, result);
    }).catch(function() {
        handleConnectionResult(page, {
            State: ConnectionState.Unavailable
        });
    });
}

export default function(view: HTMLElement): void {
    view.querySelector('.addServerForm')!.addEventListener('submit', onServerSubmit);
    view.querySelector('.btnCancel')!.addEventListener('click', goBack);

    void import('../../../components/autoFocuser').then(({ default: autoFocuser }) => {
        autoFocuser.autoFocus(view);
    }).catch((error: unknown) => console.error('[AddServer] failed to focus server form', error));

    function onServerSubmit(e: Event): void {
        submitServer(view);
        e.preventDefault();
    }

    function goBack(): void {
        void import('../../../components/router/appRouter').then(({ appRouter }) => {
            return appRouter.back();
        }).catch((error: unknown) => console.error('[AddServer] failed to navigate back', error));
    }
}
