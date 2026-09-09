import escapeHtml from 'escape-html';
import type { VirtualFolderInfo } from '@jellyfin/sdk/lib/generated-client/models/virtual-folder-info';

import { getDefaultBackgroundClass } from 'components/cardbuilder/utils/builder';
import confirm from 'components/confirm/confirm';
import loading from 'components/loading/loading';
import globalize from 'lib/globalize';
import { ServerConnections } from 'lib/jellyfin-apiclient';
import dom from 'utils/dom';
import taskButton from 'scripts/taskbutton';
import Dashboard, { pageClassOn, pageIdOn } from 'utils/dashboard';
import imageHelper from 'utils/image';

import 'components/cardbuilder/card.scss';
import 'elements/emby-itemrefreshindicator/emby-itemrefreshindicator';

type WizardLibraryApiClient = NonNullable<ReturnType<typeof ServerConnections.currentApiClient>> & {
    getVirtualFolders: () => Promise<VirtualFolderInfo[]>;
    removeVirtualFolder: (name: string, refreshLibrary?: boolean) => Promise<void>;
    renameVirtualFolder: (name: string, newName: string, refreshLibrary?: boolean) => Promise<void>;
    serverId: () => string;
    getScaledImageUrl: (itemId: string, options?: Record<string, unknown>) => string;
};
type LibraryPage = HTMLElement & { id: string };
type WizardVirtualFolder = VirtualFolderInfo & {
    icon?: string;
    showType?: boolean;
    showLocations?: boolean;
    showMenu?: boolean;
    showNameWithIcon?: boolean;
    elementId?: string;
};
type CollectionTypeOption = {
    name: string;
    value: string;
    message?: string;
    hidden?: boolean;
};

function logLibraryError(action: string, error: unknown): void {
    console.error(`[Wizard > Library] ${action}`, error);
}

function addVirtualFolder(page: LibraryPage): void {
    import('components/mediaLibraryCreator/mediaLibraryCreator').then(({ default: MediaLibraryCreator }) => {
        const creator = new MediaLibraryCreator({
            collectionTypeOptions: getCollectionTypeOptions().filter(function (f) {
                return !f.hidden;
            }),
            refresh: shouldRefreshLibraryAfterChanges(page)
        }) as unknown as Promise<boolean>;
        creator.then(function (hasChanges: boolean) {
            if (hasChanges) {
                reloadLibrary(page);
            }
        }).catch((error: unknown) => logLibraryError('failed to create media library', error));
    }).catch((error: unknown) => logLibraryError('failed to load media library creator', error));
}

function editVirtualFolder(page: LibraryPage, virtualFolder: WizardVirtualFolder): void {
    import('components/mediaLibraryEditor/mediaLibraryEditor').then(({ default: MediaLibraryEditor }) => {
        const library = {
            ...virtualFolder,
            Name: virtualFolder.Name ?? '',
            ItemId: virtualFolder.ItemId ?? undefined,
            Locations: virtualFolder.Locations ?? []
        } as unknown as ConstructorParameters<typeof MediaLibraryEditor>[0]['library'];
        const editor = new MediaLibraryEditor({
            refresh: shouldRefreshLibraryAfterChanges(page),
            library
        }) as unknown as Promise<boolean>;
        editor.then(function (hasChanges: boolean) {
            if (hasChanges) {
                reloadLibrary(page);
            }
        }).catch((error: unknown) => logLibraryError('failed to edit media library', error));
    }).catch((error: unknown) => logLibraryError('failed to load media library editor', error));
}

function deleteVirtualFolder(page: LibraryPage, virtualFolder: WizardVirtualFolder): void {
    const name = virtualFolder.Name;
    if (!name) {
        return;
    }
    let msg = globalize.translate('MessageAreYouSureYouWishToRemoveMediaFolder');
    const locations: string[] = virtualFolder.Locations ?? [];

    if (locations.length) {
        msg += '<br/><br/>' + globalize.translate('MessageTheFollowingLocationWillBeRemovedFromLibrary') + '<br/><br/>';
        msg += locations.map((location: string) => escapeHtml(location)).join('<br/>');
    }

    confirm({
        text: msg,
        title: globalize.translate('HeaderRemoveMediaFolder'),
        confirmText: globalize.translate('Delete'),
        primary: 'delete'
    }).then(function () {
        const refreshAfterChange = shouldRefreshLibraryAfterChanges(page);
        const apiClient = ServerConnections.currentApiClient() as WizardLibraryApiClient | undefined;
        if (!apiClient) {
            return;
        }
        apiClient
            .removeVirtualFolder(name, refreshAfterChange)
            .then(function () {
                reloadLibrary(page);
            })
            .catch((error: unknown) => logLibraryError('failed to remove media library', error));
    }).catch((error: unknown) => logLibraryError('failed to confirm media library removal', error));
}

function refreshVirtualFolder(page: LibraryPage, virtualFolder: WizardVirtualFolder): void {
    const itemId = virtualFolder.ItemId;
    if (!itemId) {
        return;
    }
    import('components/refreshdialog/refreshdialog').then(({ default: RefreshDialog }) => {
        const apiClient = ServerConnections.currentApiClient() as WizardLibraryApiClient | undefined;
        if (!apiClient) {
            return;
        }
        void Promise.resolve(new RefreshDialog({
            itemIds: [itemId],
            serverId: apiClient.serverId(),
            mode: 'scan'
        }).show()).catch((error: unknown) => logLibraryError('failed to refresh media library', error));
    }).catch((error: unknown) => logLibraryError('failed to load refresh dialog', error));
}

function renameVirtualFolder(page: LibraryPage, virtualFolder: WizardVirtualFolder): void {
    const name = virtualFolder.Name;
    if (!name) {
        return;
    }
    import('components/prompt/prompt').then(({ default: prompt }) => {
        prompt({
            label: globalize.translate('LabelNewName'),
            description: globalize.translate('MessageRenameMediaFolder'),
            confirmText: globalize.translate('ButtonRename')
        }).then(function (newName: string) {
            if (newName && newName != name) {
                const refreshAfterChange = shouldRefreshLibraryAfterChanges(page);
                const apiClient = ServerConnections.currentApiClient() as WizardLibraryApiClient | undefined;
                if (!apiClient) {
                    return;
                }
                apiClient
                    .renameVirtualFolder(name, newName, refreshAfterChange)
                    .then(function () {
                        reloadLibrary(page);
                    })
                    .catch((error: unknown) => logLibraryError('failed to rename media library', error));
            }
        }).catch((error: unknown) => logLibraryError('failed to process media library rename', error));
    }).catch((error: unknown) => logLibraryError('failed to load media library prompt', error));
}

function showCardMenu(page: LibraryPage, elem: HTMLElement, virtualFolders: WizardVirtualFolder[]): void {
    const card = dom.parentWithClass(elem, 'card');
    if (!card) {
        return;
    }
    const index = parseInt(card.getAttribute('data-index') ?? '-1', 10);
    const virtualFolder = virtualFolders[index];
    if (!virtualFolder) {
        return;
    }
    const menuItems = [
        {
            name: globalize.translate('EditImages'),
            id: 'editimages',
            icon: 'photo'
        },
        {
            name: globalize.translate('ManageLibrary'),
            id: 'edit',
            icon: 'folder'
        },
        {
            name: globalize.translate('ButtonRename'),
            id: 'rename',
            icon: 'mode_edit'
        },
        {
            name: globalize.translate('ScanLibrary'),
            id: 'refresh',
            icon: 'refresh'
        },
        {
            name: globalize.translate('ButtonRemove'),
            id: 'delete',
            icon: 'delete'
        }
    ];

    import('components/actionSheet/actionSheet').then((actionsheet) => {
        void Promise.resolve(actionsheet.show({
            items: menuItems,
            positionTo: elem,
            callback: function (resultId: string) {
                switch (resultId) {
                    case 'edit':
                        editVirtualFolder(page, virtualFolder);
                        break;
                    case 'editimages':
                        editImages(page, virtualFolder);
                        break;
                    case 'rename':
                        renameVirtualFolder(page, virtualFolder);
                        break;
                    case 'delete':
                        deleteVirtualFolder(page, virtualFolder);
                        break;
                    case 'refresh':
                        refreshVirtualFolder(page, virtualFolder);
                }
            }
        })).catch((error: unknown) => logLibraryError('failed to display media library actions', error));
    }).catch((error: unknown) => logLibraryError('failed to load media library actions', error));
}

function reloadLibrary(page: LibraryPage): void {
    loading.show();
    const apiClient = ServerConnections.currentApiClient() as WizardLibraryApiClient | undefined;
    if (!apiClient) {
        loading.hide();
        return;
    }
    apiClient
        .getVirtualFolders()
        .then(function (result: VirtualFolderInfo[]) {
            reloadVirtualFolders(page, result);
        })
        .catch((error: unknown) => {
            loading.hide();
            logLibraryError('failed to load media libraries', error);
        });
}

function shouldRefreshLibraryAfterChanges(page: LibraryPage): boolean {
    return page.id === 'mediaLibraryPage';
}

function reloadVirtualFolders(page: LibraryPage, virtualFolders: VirtualFolderInfo[]): void {
    let html = '';
    const folders: WizardVirtualFolder[] = [...virtualFolders, {
        Name: globalize.translate('ButtonAddMediaLibrary'),
        icon: 'add_circle',
        Locations: [],
        showType: false,
        showLocations: false,
        showMenu: false,
        showNameWithIcon: false,
        elementId: 'addLibrary'
    }];

    for (let i = 0; i < folders.length; i++) {
        const virtualFolder = folders[i];
        html += getVirtualFolderHtml(virtualFolder, i);
    }

    const divVirtualFolders = page.querySelector<HTMLElement>('#divVirtualFolders');
    if (!divVirtualFolders) {
        loading.hide();
        return;
    }
    divVirtualFolders.innerHTML = html;
    divVirtualFolders.classList.add('itemsContainer');
    divVirtualFolders.classList.add('vertical-wrap');
    const btnCardMenuElements = divVirtualFolders.querySelectorAll<HTMLElement>('.btnCardMenu');
    btnCardMenuElements.forEach(function (btn) {
        btn.addEventListener('click', function () {
            showCardMenu(page, btn, folders);
        });
    });
    const addLibraryButton = divVirtualFolders.querySelector<HTMLElement>('#addLibrary');
    addLibraryButton?.addEventListener('click', function () {
        addVirtualFolder(page);
    });

    const libraryEditElements = divVirtualFolders.querySelectorAll<HTMLElement>('.editLibrary');
    libraryEditElements.forEach(function (btn) {
        btn.addEventListener('click', function () {
            const card = dom.parentWithClass(btn, 'card');
            if (!card) {
                return;
            }
            const index = parseInt(card.getAttribute('data-index') ?? '-1', 10);
            const virtualFolder = folders[index];

            if (virtualFolder.ItemId) {
                editVirtualFolder(page, virtualFolder);
            }
        });
    });
    loading.hide();
}

function editImages(page: LibraryPage, virtualFolder: WizardVirtualFolder): void {
    const itemId = virtualFolder.ItemId;
    if (!itemId) {
        return;
    }
    import('components/imageeditor/imageeditor').then((imageEditor) => {
        const apiClient = ServerConnections.currentApiClient() as WizardLibraryApiClient | undefined;
        if (!apiClient) {
            return;
        }
        imageEditor.show({
            itemId,
            serverId: apiClient.serverId()
        }).then(function () {
            reloadLibrary(page);
        }).catch((error: unknown) => logLibraryError('failed to edit media library images', error));
    }).catch((error: unknown) => logLibraryError('failed to load image editor', error));
}

function getLink(text: string, url: string): string {
    return globalize.translate(text, '<a is="emby-linkbutton" class="button-link" href="' + escapeHtml(url) + '" target="_blank" data-autohide="true">', '</a>');
}

function getCollectionTypeOptions(): CollectionTypeOption[] {
    return [{
        name: '',
        value: ''
    }, {
        name: globalize.translate('Movies'),
        value: 'movies',
        message: getLink('MovieLibraryHelp', 'https://MulletaFlix.org/docs/general/server/media/movies')
    }, {
        name: globalize.translate('TabMusic'),
        value: 'music',
        message: getLink('MusicLibraryHelp', 'https://MulletaFlix.org/docs/general/server/media/music')
    }, {
        name: globalize.translate('Shows'),
        value: 'tvshows',
        message: getLink('TvLibraryHelp', 'https://MulletaFlix.org/docs/general/server/media/shows')
    }, {
        name: globalize.translate('Books'),
        value: 'books',
        message: getLink('BookLibraryHelp', 'https://MulletaFlix.org/docs/general/server/media/books')
    }, {
        name: globalize.translate('HomeVideosPhotos'),
        value: 'homevideos'
    }, {
        name: globalize.translate('MusicVideos'),
        value: 'musicvideos'
    }, {
        name: globalize.translate('MixedMoviesShows'),
        value: 'mixed',
        message: globalize.translate('MessageUnsetContentHelp')
    }];
}

function getVirtualFolderImageHtml(virtualFolder: WizardVirtualFolder): string {
    let html = '';
    let imgUrl = '';

    if (virtualFolder.PrimaryImageItemId) {
        const apiClient = ServerConnections.currentApiClient() as WizardLibraryApiClient | undefined;
        if (apiClient) {
            imgUrl = apiClient.getScaledImageUrl(virtualFolder.PrimaryImageItemId, {
                maxWidth: Math.round(dom.getScreenWidth() * 0.40),
                type: 'Primary'
            });
        }
    }

    let hasCardImageContainer = false;
    if (imgUrl) {
        html += `<div class="cardImageContainer editLibrary" style="cursor:pointer"><img src="${escapeHtml(imgUrl)}" style="width:100%" />`;
        hasCardImageContainer = true;
    } else if (!virtualFolder.showNameWithIcon) {
        html += `<div class="cardImageContainer editLibrary ${getDefaultBackgroundClass()}" style="cursor:pointer;">`;
        html += `<span class="cardImageIcon material-icons ${escapeHtml(virtualFolder.icon || imageHelper.getLibraryIcon(virtualFolder.CollectionType))}" aria-hidden="true"></span>`;
        hasCardImageContainer = true;
    }

    if (hasCardImageContainer) {
        html += '<div class="cardIndicators backdropCardIndicators">';
        html += '<div is="emby-itemrefreshindicator"' + (virtualFolder.RefreshProgress || virtualFolder.RefreshStatus && virtualFolder.RefreshStatus !== 'Idle' ? '' : ' class="hide"') + ' data-progress="' + escapeHtml(String(virtualFolder.RefreshProgress || 0)) + '" data-status="' + escapeHtml(virtualFolder.RefreshStatus || '') + '"></div>';
        html += '</div></div>';
    }

    if (!imgUrl && virtualFolder.showNameWithIcon) {
        html += '<h3 class="cardImageContainer addLibrary" style="position:absolute;top:0;left:0;right:0;bottom:0;cursor:pointer;flex-direction:column;">';
        html += `<span class="cardImageIcon material-icons ${escapeHtml(virtualFolder.icon || imageHelper.getLibraryIcon(virtualFolder.CollectionType))}" aria-hidden="true"></span>`;
        html += '<div style="margin:1em 0;position:width:100%;">' + escapeHtml(String(virtualFolder.Name ?? '')) + '</div></h3>';
    }

    return html;
}

function getVirtualFolderFooterHtml(virtualFolder: WizardVirtualFolder, locations: string[]): string {
    let html = '<div class="cardFooter visualCardBox-cardFooter">';

    if (virtualFolder.showMenu !== false) {
        const dirTextAlign = globalize.getIsRTL() ? 'left' : 'right';
        html += '<div style="text-align:' + dirTextAlign + '; float:' + dirTextAlign + ';padding-top:5px;">';
        html += '<button type="button" is="paper-icon-button-light" class="btnCardMenu autoSize"><span class="material-icons more_vert" aria-hidden="true"></span></button></div>';
    }

    html += "<div class='cardText'>" + (virtualFolder.showNameWithIcon ? '&nbsp;' : escapeHtml(String(virtualFolder.Name ?? ''))) + '</div>';
    const typeName = getCollectionTypeOptions().find(t => t.value === virtualFolder.CollectionType)?.name || globalize.translate('Other');
    html += "<div class='cardText cardText-secondary'>" + (virtualFolder.showType === false ? '&nbsp;' : typeName) + '</div>';

    if (virtualFolder.showLocations === false) {
        html += "<div class='cardText cardText-secondary'>&nbsp;</div>";
    } else if (locations.length === 1) {
        html += "<div class='cardText cardText-secondary' dir='ltr' style='text-align:left;'>" + escapeHtml(locations[0]) + '</div>';
    } else {
        html += "<div class='cardText cardText-secondary'>" + globalize.translate('NumLocationsValue', String(locations.length)) + '</div>';
    }

    return html + '</div>';
}

function getVirtualFolderHtml(virtualFolder: WizardVirtualFolder, index: number): string {
    let html = '';
    const locations: string[] = virtualFolder.Locations ?? [];

    const elementId = virtualFolder.elementId ? `id="${escapeHtml(virtualFolder.elementId)}" ` : '';
    html += '<div ' + elementId + 'class="card backdropCard scalableCard backdropCard-scalable" style="min-width:33.3%;" data-index="' + index + '" data-id="' + escapeHtml(virtualFolder.ItemId || '') + '">';

    html += '<div class="cardBox visualCardBox">';
    html += '<div class="cardScalable visualCardBox-cardScalable">';
    html += '<div class="cardPadder cardPadder-backdrop"></div>';
    html += '<div class="cardContent">' + getVirtualFolderImageHtml(virtualFolder) + '</div>';
    html += '</div>';
    html += getVirtualFolderFooterHtml(virtualFolder, locations);
    html += '</div>';
    html += '</div>';
    return html;
}

const win = window as typeof window & {
    WizardLibraryPage?: {
        next: () => void;
    };
};
win.WizardLibraryPage = {
    next: function () {
        void Promise.resolve(Dashboard.navigate('wizard/settings')).catch(error => logLibraryError('failed to navigate to wizard settings', error));
    }
};
pageClassOn('pageshow', 'mediaLibraryPage', function (this: LibraryPage) {
    reloadLibrary(this);
});
pageIdOn('pageshow', 'mediaLibraryPage', function (this: LibraryPage) {
    const button = this.querySelector<HTMLElement>('.btnRefresh');
    if (!button) {
        return;
    }
    taskButton({
        mode: 'on',
        progressElem: this.querySelector<HTMLProgressElement>('.refreshProgress') ?? undefined,
        taskKey: 'RefreshLibrary',
        button
    });
});
pageIdOn('pagebeforehide', 'mediaLibraryPage', function (this: LibraryPage) {
    const button = this.querySelector<HTMLElement>('.btnRefresh');
    if (!button) {
        return;
    }
    taskButton({
        mode: 'off',
        progressElem: this.querySelector<HTMLProgressElement>('.refreshProgress') ?? undefined,
        taskKey: 'RefreshLibrary',
        button
    });
});
