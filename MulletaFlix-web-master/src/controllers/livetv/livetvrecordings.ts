import cardBuilder from 'components/cardbuilder/cardBuilder';
import { getBackdropShape } from 'components/cardbuilder/utils/shape';
import imageLoader from 'components/images/imageLoader';
import loading from 'components/loading/loading';
import type { ItemDto } from 'types/base/models/item-dto';
import Dashboard from 'utils/dashboard';

import 'scripts/livetvcomponents';
import 'components/listview/listview.scss';
import 'elements/emby-itemscontainer/emby-itemscontainer';

declare const ApiClient: {
    serverId(): string;
    getLiveTvRecordings(options: {
        UserId: string;
        Limit: number;
        Fields: string;
        EnableTotalRecordCount: boolean;
        EnableImageTypes: string;
    }): Promise<LiveTvItemsResult>;
    getRecordingFolders(userId: string): Promise<LiveTvItemsResult>;
};

interface LiveTvItemsResult {
    Items: ItemDto[];
}

function renderRecordings(
    elem: HTMLElement | null,
    recordings: ItemDto[],
    cardOptions?: Record<string, unknown>,
    scrollX?: boolean
): void {
    if (!elem) {
        return;
    }

    if (recordings.length) {
        elem.classList.remove('hide');
    } else {
        elem.classList.add('hide');
    }

    const recordingItems = elem.querySelector('.recordingItems');
    if (!(recordingItems instanceof HTMLElement)) {
        return;
    }

    if (scrollX) {
        recordingItems.classList.add('scrollX');
        recordingItems.classList.add('hiddenScrollX');
        recordingItems.classList.remove('vertical-wrap');
    } else {
        recordingItems.classList.remove('scrollX');
        recordingItems.classList.remove('hiddenScrollX');
        recordingItems.classList.add('vertical-wrap');
    }

    recordingItems.innerHTML = cardBuilder.getCardsHtml(Object.assign({
        items: recordings,
        shape: scrollX ? 'autooverflow' : 'auto',
        defaultShape: getBackdropShape(scrollX),
        showTitle: true,
        showParentTitle: true,
        coverImage: true,
        cardLayout: false,
        centerText: true,
        allowBottomPadding: !scrollX,
        preferThumb: 'auto',
        overlayText: false
    }, cardOptions || {}));
    imageLoader.lazyChildren(recordingItems);
}

function renderLatestRecordings(
    context: HTMLElement,
    promise: Promise<LiveTvItemsResult>
): void {
    promise.then(function (result) {
        renderRecordings(context.querySelector('#latestRecordings'), result.Items, {
            showYear: true,
            lines: 2
        }, false);
        loading.hide();
    }).catch((error: unknown) => {
        loading.hide();
        console.error('[LiveTvRecordings] failed to load latest recordings', error);
    });
}

function renderRecordingFolders(
    context: HTMLElement,
    promise: Promise<LiveTvItemsResult>
): void {
    promise.then(function (result) {
        renderRecordings(context.querySelector('#recordingFolders'), result.Items, {
            showYear: false,
            showParentTitle: false
        }, false);
    }).catch((error: unknown) => {
        loading.hide();
        console.error('[LiveTvRecordings] failed to load recording folders', error);
    });
}

function onMoreClick(this: HTMLElement): void {
    const type = this.getAttribute('data-type');

    if (type === 'latest') {
        Dashboard.navigate('list?type=Recordings&serverId=' + ApiClient.serverId()).catch((error: unknown) => console.error('[LiveTvRecordings] failed to open recordings list', error));
    }
}

interface LiveTvRecordingsController {
    preRender: () => void;
    renderTab: () => void;
}

export default function (this: LiveTvRecordingsController, view: HTMLElement, params: Record<string, string>, tabContent: HTMLElement): void {
    function enableFullRender(): boolean {
        return new Date().getTime() - lastFullRender > 300000;
    }

    let foldersPromise: Promise<LiveTvItemsResult>;
    let latestPromise: Promise<LiveTvItemsResult>;
    let lastFullRender = 0;
    const moreButtons = tabContent.querySelectorAll('.more');

    for (let i = 0, length = moreButtons.length; i < length; i++) {
        moreButtons[i].addEventListener('click', function (this: HTMLElement) {
            onMoreClick.call(this);
        });
    }

    this.preRender = function (): void {
        const userId = Dashboard.getCurrentUserId() ?? '';
        if (enableFullRender()) {
            latestPromise = ApiClient.getLiveTvRecordings({
                UserId: userId,
                Limit: 12,
                Fields: 'CanDelete,PrimaryImageAspectRatio',
                EnableTotalRecordCount: false,
                EnableImageTypes: 'Primary,Thumb,Backdrop'
            });
            foldersPromise = ApiClient.getRecordingFolders(userId);
        }
    };

    this.renderTab = function (): void {
        if (enableFullRender()) {
            loading.show();
            renderLatestRecordings(tabContent, latestPromise);
            renderRecordingFolders(tabContent, foldersPromise);
            lastFullRender = new Date().getTime();
        }
    };
}
