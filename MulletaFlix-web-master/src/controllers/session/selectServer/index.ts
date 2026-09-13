import escapeHtml from 'escape-html';
import loading from '../../../components/loading/loading';
import { appRouter } from '../../../components/router/appRouter';
import layoutManager from '../../../components/layoutManager';
import libraryMenu from '../../../scripts/libraryMenu';
import appSettings from '../../../scripts/settings/appSettings';
import focusManager from '../../../components/focusManager';
import globalize from '../../../lib/globalize';
import actionSheet from '../../../components/actionSheet/actionSheet';
import confirm from '../../../components/confirm/confirm';
import dom from '../../../utils/dom';
import browser from '../../../scripts/browser';
import 'material-design-icons-iconfont';
import '../../../styles/flexstyles.scss';
import '../../../elements/emby-scroller/emby-scroller';
import '../../../elements/emby-itemscontainer/emby-itemscontainer';
import '../../../components/cardbuilder/card.scss';
import '../../../elements/emby-button/emby-button';
import Dashboard from '../../../utils/dashboard';
import alert from '../../../components/alert';
import { getDefaultBackgroundClass } from '../../../components/cardbuilder/utils/builder';
import { ConnectionState, ServerConnections } from 'lib/jellyfin-apiclient';
import type { ConnectResult } from 'lib/jellyfin-apiclient/connectionManager';

const enableFocusTransform: boolean = !browser.slow && !browser.edge;

interface ServerItem {
    Name?: string;
    Id: string;
    [key: string]: unknown;
}

interface SelectServerItem {
    name: string | undefined;
    icon: string;
    cardType: string;
    id: string;
    server: ServerItem;
    url?: string;
}

interface SelectServerViewParams {
    showuser?: string;
}

function renderSelectServerItems(view: HTMLElement, servers: ServerItem[]): void {
    const items: SelectServerItem[] = servers.map(function (server: ServerItem) {
        return {
            name: server.Name,
            icon: 'storage',
            cardType: '',
            id: server.Id,
            server: server
        };
    });
    let html: string = items.map(function (item: SelectServerItem) {
        const cardImageContainer: string = '<span class="cardImageIcon material-icons ' + item.icon + '" aria-hidden="true"></span>';
        let cssClass: string = 'card overflowSquareCard loginSquareCard scalableCard overflowSquareCard-scalable';

        if (layoutManager.tv) {
            cssClass += ' show-focus';

            if (enableFocusTransform) {
                cssClass += ' show-animation';
            }
        }

        const cardBoxCssClass: string = 'cardBox';

        const innerOpening: string = '<div class="' + cardBoxCssClass + '">';
        let cardContainer: string = '';
        cardContainer += '<button raised class="' + cssClass + '" style="display:inline-block;" data-id="' + escapeHtml(item.id) + '" data-url="' + escapeHtml(item.url || '') + '" data-cardtype="' + escapeHtml(item.cardType) + '">';
        cardContainer += innerOpening;
        cardContainer += '<div class="cardScalable">';
        cardContainer += '<div class="cardPadder cardPadder-square">';
        cardContainer += '</div>';
        cardContainer += '<div class="cardContent">';
        cardContainer += `<div class="cardImageContainer coveredImage ${getDefaultBackgroundClass()}">`;
        cardContainer += cardImageContainer;
        cardContainer += '</div>';
        cardContainer += '</div>';
        cardContainer += '</div>';
        cardContainer += '<div class="cardFooter">';
        cardContainer += '<div class="cardText cardTextCentered">' + escapeHtml(item.name || '') + '</div>';
        cardContainer += '</div></div></button>';
        return cardContainer;
    }).join('');
    const itemsContainer = view.querySelector('.servers') as HTMLElement;

    if (!items.length) {
        html = '<p>' + globalize.translate('MessageNoServersAvailable') + '</p>';
    }

    itemsContainer.innerHTML = html;
}

function updatePageStyle(view: HTMLElement, params: SelectServerViewParams): void {
    if (params.showuser == '1') {
        view.classList.add('libraryPage');
        view.classList.remove('standalonePage');
        view.classList.add('noSecondaryNavPage');
    } else {
        view.classList.add('standalonePage');
        view.classList.remove('libraryPage');
        view.classList.remove('noSecondaryNavPage');
    }
}

interface AlertOptions {
    text?: string;
    html?: string;
}

function alertText(text: string): void {
    alertTextWithOptions({
        text: text
    });
}

function alertTextWithOptions(options: AlertOptions): void {
    alert(options).catch((error: unknown) => console.error('[selectServer] failed to show alert', error));
}

function showServerConnectionFailure(): void {
    alertText(globalize.translate('MessageUnableToConnectToServer'));
}

export default function (view: HTMLElement, params: SelectServerViewParams): void {
    let servers: ServerItem[] = [];

    function connectToServer(server: ServerItem): void {
        void loading.withLoading(() => ServerConnections.connectToServer(server as never, {
            enableAutoLogin: appSettings.enableAutoLogin()
        })).then(function (result: ConnectResult) {
            switch (result.State) {
                case ConnectionState.SignedIn: {
                    const apiClient = result.ApiClient;
                    if (!apiClient) {
                        showServerConnectionFailure();
                        return;
                    }

                    Dashboard.onServerChanged(apiClient.getCurrentUserId()!, apiClient.accessToken()!, apiClient as never);
                    void Dashboard.navigate('home').catch((error: unknown) => console.error('[selectServer] failed to open home', error));
                    break;
                }
                case ConnectionState.ServerSignIn: {
                    const apiClient = result.ApiClient;
                    const serverId = result.Servers?.[0]?.Id;
                    if (!apiClient || !serverId) {
                        showServerConnectionFailure();
                        return;
                    }

                    Dashboard.onServerChanged('', '', apiClient as never);
                    void Dashboard.navigate('login?serverid=' + serverId).catch((error: unknown) => console.error('[selectServer] failed to open login', error));
                    break;
                }

                case ConnectionState.ServerUpdateNeeded:
                    alertTextWithOptions({
                        text: globalize.translate('core#ServerUpdateNeeded', 'https://github.com/MulletaFlix/MulletaFlix'),
                        html: globalize.translate('core#ServerUpdateNeeded', '<a href="https://github.com/MulletaFlix/MulletaFlix">https://github.com/MulletaFlix/MulletaFlix</a>')
                    });
                    break;

                default:
                    showServerConnectionFailure();
            }
        }).catch((error: unknown) => {
            console.error('[selectServer] failed to connect to server', error);
            showServerConnectionFailure();
        });
    }

    function deleteServer(server: ServerItem): void {
        confirm({
            title: globalize.translate('DeleteName', server.Name || ''),
            text: globalize.translate('DeleteServerConfirmation'),
            confirmText: globalize.translate('Delete'),
            primary: 'delete'
        }).then(function () {
            void loading.withLoading(() => ServerConnections.deleteServer(server.Id)).then(function () {
                loadServers();
            }).catch((err: unknown) => {
                console.error('[selectServer] failed to delete server', err);
            });
        }).catch(() => {
            // confirm dialog closed
        });
    }

    function onServerClick(server: ServerItem): void {
        const menuItems: Array<{ name: string; id: string }> = [];
        menuItems.push({
            name: globalize.translate('Connect'),
            id: 'connect'
        });
        menuItems.push({
            name: globalize.translate('Delete'),
            id: 'delete'
        });
        actionSheet.show({
            items: menuItems,
            title: server.Name
        }).then(function (id: unknown) {
            switch (id as string) {
                case 'connect':
                    connectToServer(server);
                    break;

                case 'delete':
                    deleteServer(server);
                    break;
            }
        }).catch(() => { /* no-op */ });
    }

    function onServersRetrieved(result: ServerItem[]): void {
        servers = result;
        renderSelectServerItems(view, result);

        if (layoutManager.tv) {
            focusManager.autoFocus(view);
        }
    }

    function loadServers(): void {
        void loading.withLoading(() => ServerConnections.getAvailableServers()).then(onServersRetrieved as never).catch((error: unknown) => {
            console.error('[selectServer] failed to load servers', error);
            showServerConnectionFailure();
        });
    }

    updatePageStyle(view, params);
    view.addEventListener('viewshow', ((e: CustomEvent) => {
        const isRestored: boolean = e.detail.isRestored;
        libraryMenu.setTitle(null);
        libraryMenu.setTransparentMenu(true);

        if (!isRestored) {
            loadServers();
        }
    }) as EventListener);
    view.querySelector('.servers')!.addEventListener('click', (e: Event) => {
        const target = e.target as HTMLElement;
        const card = dom.parentWithClass(target, 'card');

        if (card) {
            const url = card.getAttribute('data-url');

            if (url) {
                void appRouter.show(url).catch((error: unknown) => console.error('[selectServer] failed to open server URL', error));
            } else {
                const id = card.getAttribute('data-id');
                onServerClick(servers.filter(function (s: ServerItem) {
                    return s.Id === id;
                })[0]);
            }
        }
    });
}
