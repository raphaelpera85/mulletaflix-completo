import { AppFeature } from 'constants/appFeature';
import dialogHelper from '../dialogHelper/dialogHelper';
import loading from '../loading/loading';
import dom from '../../utils/dom';
import layoutManager from '../layoutManager';
import focusManager from '../focusManager';
import globalize from '../../lib/globalize';
import { ServerConnections } from 'lib/jellyfin-apiclient';
import scrollHelper from '../../scripts/scrollHelper';
import imageLoader from '../images/imageLoader';
import browser from '../../scripts/browser';
import { appHost } from '../apphost';
import escapeHtml from 'escape-html';
import '../cardbuilder/card.scss';
import '../formdialog.scss';
import '../../elements/emby-button/emby-button';
import '../../elements/emby-button/paper-icon-button-light';
import './imageeditor.scss';
import alert from '../alert';
import confirm from '../confirm/confirm';
import template from './imageeditor.template.html';

const enableFocusTransform: boolean = !browser.slow && !browser.edge;

interface ImageInfo {
    ImageType: string;
    ImageIndex: number;
    Width?: number;
    Height?: number;
}

interface ImageEditorItem {
    Id: string;
    ItemId?: string;
    ServerId: string;
    Type?: string;
    ParentId?: string;
    PrimaryImageTag?: string;
    ImageTags?: Record<string, string>;
    BackdropImageTags?: string[];
}

interface ImageEditorApiClient {
    serverId: () => string;
    getCurrentUserId: () => string;
    getScaledImageUrl: (itemId: string, options: ImageUrlOptions) => string;
    getRemoteImageProviders: (options: { itemId: string }) => Promise<unknown[]>;
    getItemImageInfos: (itemId: string) => Promise<ImageInfo[]>;
    getItem: (userId: string, itemId: string) => Promise<ImageEditorItem>;
    deleteItemImage: (itemId: string, type: string, index: number | null) => Promise<unknown>;
    updateItemImageIndex: (itemId: string, type: string, index: number, newIndex: number) => Promise<unknown>;
}

let currentItem: ImageEditorItem;
let hasChanges = false;

function getBaseRemoteOptions(): { itemId: string } {
    return { itemId: currentItem.Id };
}

function reload(page: HTMLElement, item?: ImageEditorItem | null, focusContext?: HTMLElement): void {
    const apiClient = ServerConnections.getApiClient(item?.ServerId || currentItem.ServerId) as unknown as ImageEditorApiClient;
    loading.withLoading(async () => {
        const itemToReload = item || await apiClient.getItem(apiClient.getCurrentUserId(), currentItem.Id);
        await reloadItem(page, itemToReload, apiClient, focusContext);
    }).catch((error: unknown) => console.error('Failed to reload image editor', error));
}

function addListeners(container: HTMLElement, className: string, eventName: string, fn: (this: HTMLElement, e: Event) => void): void {
    container.addEventListener(eventName, function (e: Event) {
        const target = e.target as HTMLElement;
        const elem = dom.parentWithClass(target, className);
        if (elem) {
            fn.call(elem, e);
        }
    });
}

async function reloadItem(page: HTMLElement, item: ImageEditorItem, apiClient: ImageEditorApiClient, focusContext?: HTMLElement): Promise<void> {
    currentItem = item;

    const providers = await apiClient.getRemoteImageProviders(getBaseRemoteOptions());
    const btnBrowseAllImages = page.querySelectorAll('.btnBrowseAllImages');
    for (let i = 0, length = btnBrowseAllImages.length; i < length; i++) {
        if (providers.length) {
            btnBrowseAllImages[i].classList.remove('hide');
        } else {
            btnBrowseAllImages[i].classList.add('hide');
        }
    }

    const imageInfos = await apiClient.getItemImageInfos(currentItem.Id);
    renderStandardImages(page, apiClient, item, imageInfos, providers);
    renderBackdrops(page, apiClient, item, imageInfos, providers);

    if (layoutManager.tv) {
        focusManager.autoFocus((focusContext || page));
    }
}

interface ImageUrlOptions {
    type?: string;
    index?: number;
    tag?: string;
    maxWidth?: number;
}

function getImageUrl(item: ImageEditorItem, apiClient: ImageEditorApiClient, type: string, index: number, options?: ImageUrlOptions): string {
    options = options || {};
    options.type = type;
    options.index = index;

    if (type === 'Backdrop') {
        options.tag = item.BackdropImageTags?.[index] || '';
    } else if (type === 'Primary') {
        options.tag = item.PrimaryImageTag || item.ImageTags?.[type] || '';
    } else {
        options.tag = item.ImageTags?.[type] || '';
    }

    // For search hints
    return apiClient.getScaledImageUrl(item.Id || item.ItemId || '', options);
}

interface CardOptions {
    index: number;
    numImages: number;
    imageProviders: unknown[];
    imageSize: number;
    tagName: string;
    enableFooterButtons: boolean;
}

function renderImageFooter(image: ImageInfo, options: CardOptions): string {
    if (!options.enableFooterButtons) {
        return '';
    }

    let html = '<div class="cardText cardTextCentered">';
    if (image.ImageType === 'Backdrop') {
        if (options.index > 0) {
            html += '<button type="button" is="paper-icon-button-light" class="btnMoveImage autoSize" data-imagetype="' + escapeHtml(String(image.ImageType || '')) + '" data-index="' + escapeHtml(String(image.ImageIndex)) + '" data-newindex="' + escapeHtml(String(image.ImageIndex - 1)) + '" title="' + escapeHtml(globalize.translate('MoveLeft')) + '"><span class="material-icons chevron_left"></span></button>';
        } else {
            html += '<button type="button" is="paper-icon-button-light" class="autoSize" disabled title="' + escapeHtml(globalize.translate('MoveLeft')) + '"><span class="material-icons chevron_left" aria-hidden="true"></span></button>';
        }

        if (options.index < options.numImages - 1) {
            html += '<button type="button" is="paper-icon-button-light" class="btnMoveImage autoSize" data-imagetype="' + escapeHtml(String(image.ImageType || '')) + '" data-index="' + escapeHtml(String(image.ImageIndex)) + '" data-newindex="' + escapeHtml(String(image.ImageIndex + 1)) + '" title="' + escapeHtml(globalize.translate('MoveRight')) + '"><span class="material-icons chevron_right" aria-hidden="true"></span></button>';
        } else {
            html += '<button type="button" is="paper-icon-button-light" class="autoSize" disabled title="' + escapeHtml(globalize.translate('MoveRight')) + '"><span class="material-icons chevron_right" aria-hidden="true"></span></button>';
        }
    } else if (options.imageProviders.length) {
        html += '<button type="button" is="paper-icon-button-light" data-imagetype="' + escapeHtml(String(image.ImageType || '')) + '" class="btnSearchImages autoSize" title="' + escapeHtml(globalize.translate('Search')) + '"><span class="material-icons search" aria-hidden="true"></span></button>';
    }

    html += '<button type="button" is="paper-icon-button-light" data-imagetype="' + escapeHtml(String(image.ImageType || '')) + '" data-index="' + escapeHtml(String(image.ImageIndex != null ? image.ImageIndex : 'null')) + '" class="btnDeleteImage autoSize" title="' + escapeHtml(globalize.translate('Delete')) + '"><span class="material-icons delete" aria-hidden="true"></span></button>';
    return html + '</div>';
}

function getCardHtml(image: ImageInfo, apiClient: ImageEditorApiClient, options: CardOptions): string {
    let html = '';

    let cssClass = 'card scalableCard imageEditorCard';
    const cardBoxCssClass = 'cardBox visualCardBox';

    cssClass += ' backdropCard backdropCard-scalable';

    if (options.tagName === 'button') {
        cssClass += ' btnImageCard';

        if (layoutManager.tv) {
            cssClass += ' show-focus';

            if (enableFocusTransform) {
                cssClass += ' show-animation';
            }
        }

        html += '<button type="button" class="' + cssClass + '"';
    } else {
        html += '<div class="' + cssClass + '"';
    }

    html += ' data-id="' + escapeHtml(String(currentItem.Id || '')) + '" data-serverid="' + escapeHtml(String(apiClient.serverId() || '')) + '" data-index="' + escapeHtml(String(options.index)) + '" data-numimages="' + escapeHtml(String(options.numImages)) + '" data-imagetype="' + escapeHtml(String(image.ImageType || '')) + '" data-providers="' + escapeHtml(String(options.imageProviders.length)) + '"';

    html += '>';

    html += '<div class="' + cardBoxCssClass + '">';
    html += '<div class="cardScalable visualCardBox-cardScalable" style="background-color:transparent;">';
    html += '<div class="cardPadder-backdrop"></div>';

    html += '<div class="cardContent">';

    const imageUrl = getImageUrl(currentItem, apiClient, image.ImageType, image.ImageIndex, { maxWidth: options.imageSize });
    const safeImageUrl = imageUrl.replace(/\\/g, '\\\\').replace(/'/g, "\\'").replace(/[\r\n]/g, '');

    html += '<div class="cardImageContainer" style="background-image:url(\'' + safeImageUrl + '\');background-position:center center;background-size:contain;"></div>';

    html += '</div>';
    html += '</div>';

    html += '<div class="cardFooter visualCardBox-cardFooter">';

    html += '<h3 class="cardText cardTextCentered" style="margin:0;">' + escapeHtml(globalize.translate('' + image.ImageType)) + '</h3>';

    html += '<div class="cardText cardText-secondary cardTextCentered">';
    if (image.Width && image.Height) {
        html += image.Width + ' X ' + image.Height;
    } else {
        html += '&nbsp;';
    }
    html += '</div>';

    html += renderImageFooter(image, options);

    html += '</div>';
    html += '</div>';
    html += '</' + options.tagName + '>';

    return html;
}

function deleteImage(context: HTMLElement, itemId: string, type: string, index: number | null, apiClient: ImageEditorApiClient, enableConfirmation: boolean): void {
    const afterConfirm = function () {
        void apiClient.deleteItemImage(itemId, type, index).then(function () {
            hasChanges = true;
            reload(context);
        }).catch((error: unknown) => console.error('Failed to delete image', error));
    };

    if (!enableConfirmation) {
        afterConfirm();
        return;
    }

    void confirm({
        text: globalize.translate('ConfirmDeleteImage'),
        confirmText: globalize.translate('Delete'),
        primary: 'delete'
    }).then(afterConfirm).catch((error: unknown) => console.error('Failed to confirm image deletion', error));
}

function moveImage(context: HTMLElement, apiClient: ImageEditorApiClient, itemId: string, type: string, index: number, newIndex: number, focusContext: HTMLElement | null): void {
    apiClient.updateItemImageIndex(itemId, type, index, newIndex).then(function () {
        hasChanges = true;
        reload(context, null, focusContext || undefined);
    }, function () {
        void alert(globalize.translate('ErrorDefault')).catch((error: unknown) => console.error('Failed to show image error', error));
    });
}

function renderImages(page: HTMLElement, item: ImageEditorItem, apiClient: ImageEditorApiClient, images: ImageInfo[], imageProviders: unknown[], elem: HTMLElement): void {
    let html = '';

    let imageSize = 300;
    const windowSize = dom.getWindowSize();
    if (windowSize.innerWidth >= 1280) {
        imageSize = Math.round(windowSize.innerWidth / 4);
    }

    const tagName = layoutManager.tv ? 'button' : 'div';
    const enableFooterButtons = !layoutManager.tv;

    for (let i = 0, length = images.length; i < length; i++) {
        const image = images[i];
        const options: CardOptions = { index: i, numImages: length, imageProviders, imageSize, tagName, enableFooterButtons };
        html += getCardHtml(image, apiClient, options);
    }

    elem.innerHTML = html;
    imageLoader.lazyChildren(elem);
}

function renderStandardImages(page: HTMLElement, apiClient: ImageEditorApiClient, item: ImageEditorItem, imageInfos: ImageInfo[], imageProviders: unknown[]): void {
    const images = imageInfos.filter(function (i: ImageInfo) {
        return i.ImageType !== 'Backdrop' && i.ImageType !== 'Chapter';
    });

    renderImages(page, item, apiClient, images, imageProviders, page.querySelector('#images') as HTMLElement);
}

function renderBackdrops(page: HTMLElement, apiClient: ImageEditorApiClient, item: ImageEditorItem, imageInfos: ImageInfo[], imageProviders: unknown[]): void {
    const images = imageInfos.filter(function (i: ImageInfo) {
        return i.ImageType === 'Backdrop';
    }).sort(function (a: ImageInfo, b: ImageInfo) {
        return a.ImageIndex - b.ImageIndex;
    });

    if (images.length) {
        (page.querySelector('#backdropsContainer') as HTMLElement).classList.remove('hide');
        renderImages(page, item, apiClient, images, imageProviders, page.querySelector('#backdrops') as HTMLElement);
    } else {
        (page.querySelector('#backdropsContainer') as HTMLElement).classList.add('hide');
    }
}

function showImageDownloader(page: HTMLElement, imageType: string): void {
    void import('../imageDownloader/imageDownloader').then((ImageDownloader) => {
        return ImageDownloader.show(
            currentItem.Id,
            currentItem.ServerId,
            currentItem.Type || '',
            imageType,
            currentItem.Type == 'Season' ? currentItem.ParentId || undefined : undefined
        ).then(function () {
            hasChanges = true;
            reload(page);
        }).catch(function () {
            // image downloader closed
        });
    }).catch((error: unknown) => console.error('Failed to open image downloader', error));
}

function showActionSheet(context: HTMLElement, imageCard: HTMLElement): void {
    const itemId = imageCard.getAttribute('data-id')!;
    const serverId = imageCard.getAttribute('data-serverid')!;
    const apiClient = ServerConnections.getApiClient(serverId) as unknown as ImageEditorApiClient;

    const type = imageCard.getAttribute('data-imagetype')!;
    const index = parseInt(imageCard.getAttribute('data-index')!, 10);
    const providerCount = parseInt(imageCard.getAttribute('data-providers')!, 10);
    const numImages = parseInt(imageCard.getAttribute('data-numimages')!, 10);

    void import('../actionSheet/actionSheet').then(({ default: actionSheet }) => {
        const commands: { name: string; id: string }[] = [];

        commands.push({
            name: globalize.translate('Delete'),
            id: 'delete'
        });

        if (type === 'Backdrop') {
            if (index > 0) {
                commands.push({
                    name: globalize.translate('MoveLeft'),
                    id: 'moveleft'
                });
            }

            if (index < numImages - 1) {
                commands.push({
                    name: globalize.translate('MoveRight'),
                    id: 'moveright'
                });
            }
        }

        if (providerCount) {
            commands.push({
                name: globalize.translate('Search'),
                id: 'search'
            });
        }

        return actionSheet.show({

            items: commands,
            positionTo: imageCard

        }).then(function (id: unknown) {
            switch (id) {
                case 'delete':
                    deleteImage(context, itemId, type, index, apiClient, false);
                    break;
                case 'search':
                    showImageDownloader(context, type);
                    break;
                case 'moveleft':
                    moveImage(context, apiClient, itemId, type, index, index - 1, dom.parentWithClass(imageCard, 'itemsContainer') as HTMLElement);
                    break;
                case 'moveright':
                    moveImage(context, apiClient, itemId, type, index, index + 1, dom.parentWithClass(imageCard, 'itemsContainer') as HTMLElement);
                    break;
                default:
                    break;
            }
        });
    }).catch((error: unknown) => console.error('Failed to open image actions', error));
}

interface EditorOptions {
    theme?: string;
    itemId?: string;
    serverId?: string;
}

function initEditor(context: HTMLElement): void {
    const uploadButtons = context.querySelectorAll('.btnOpenUploadMenu');
    const isFileInputSupported = appHost.supports(AppFeature.FileInput);
    for (let i = 0, length = uploadButtons.length; i < length; i++) {
        if (isFileInputSupported) {
            uploadButtons[i].classList.remove('hide');
        } else {
            uploadButtons[i].classList.add('hide');
        }
    }

    addListeners(context, 'btnOpenUploadMenu', 'click', function (this: HTMLElement) {
        const imageType = this.getAttribute('data-imagetype')!;

        void import('../imageUploader/imageUploader').then(({ default: imageUploader }) => {
            return imageUploader.show({

                imageType: imageType,
                itemId: currentItem.Id,
                serverId: currentItem.ServerId

            }).then(function (hasChanged: boolean) {
                if (hasChanged) {
                    hasChanges = true;
                    reload(context);
                }
            });
        }).catch((error: unknown) => console.error('Failed to open image uploader', error));
    });

    addListeners(context, 'btnSearchImages', 'click', function (this: HTMLElement) {
        showImageDownloader(context, this.getAttribute('data-imagetype')!);
    });

    addListeners(context, 'btnBrowseAllImages', 'click', function (this: HTMLElement) {
        showImageDownloader(context, this.getAttribute('data-imagetype') || 'Primary');
    });

    addListeners(context, 'btnImageCard', 'click', function (this: HTMLElement) {
        showActionSheet(context, this);
    });

    addListeners(context, 'btnDeleteImage', 'click', function (this: HTMLElement) {
        const type = this.getAttribute('data-imagetype')!;
        let index: number | string | null = this.getAttribute('data-index');
        index = index === 'null' ? null : parseInt(index!, 10);
        const apiClient = ServerConnections.getApiClient(currentItem.ServerId) as unknown as ImageEditorApiClient;
        deleteImage(context, currentItem.Id, type, index, apiClient, true);
    });

    addListeners(context, 'btnMoveImage', 'click', function (this: HTMLElement) {
        const type = this.getAttribute('data-imagetype')!;
        const index = parseInt(this.getAttribute('data-index')!, 10);
        const newIndex = parseInt(this.getAttribute('data-newindex')!, 10);
        const apiClient = ServerConnections.getApiClient(currentItem.ServerId) as unknown as ImageEditorApiClient;
        moveImage(context, apiClient, currentItem.Id, type, index, newIndex, dom.parentWithClass(this, 'itemsContainer') as HTMLElement);
    });
}

function showEditor(options: EditorOptions, resolve: () => void, reject: () => void): void {
    const itemId = options.itemId!;
    const serverId = options.serverId!;

    const apiClient = ServerConnections.getApiClient(serverId) as unknown as ImageEditorApiClient;
    loading.withLoading(() => apiClient.getItem(apiClient.getCurrentUserId(), itemId)).then(function (item: ImageEditorItem) {
        const dialogOptions: Record<string, boolean | string> = {
            removeOnClose: true
        };

        if (layoutManager.tv) {
            dialogOptions.size = 'fullscreen';
        } else {
            dialogOptions.size = 'small';
        }

        const dlg = dialogHelper.createDialog(dialogOptions);

        dlg.classList.add('formDialog');

        dlg.innerHTML = globalize.translateHtml(template, 'core');

        if (layoutManager.tv) {
            scrollHelper.centerFocus.on(dlg, false);
        }

        initEditor(dlg);

        // Has to be assigned a z-index after the call to .open()
        dlg.addEventListener('close', function () {
            if (layoutManager.tv) {
                scrollHelper.centerFocus.off(dlg, false);
            }

            if (hasChanges) {
                resolve();
            } else {
                reject();
            }
        });

        void dialogHelper.open(dlg).catch((error: unknown) => console.error('Failed to open image editor dialog', error));

        reload(dlg, item);

        dlg.querySelector('.btnCancel')!.addEventListener('click', function () {
            dialogHelper.close(dlg);
        });
    }).catch((error: unknown) => {
        console.error('Failed to load image editor item', error);
    });
}

export function show(options: EditorOptions): Promise<void> {
    return new Promise(function (resolve, reject) {
        hasChanges = false;
        showEditor(options, resolve, reject);
    });
}

export default {
    show
};
