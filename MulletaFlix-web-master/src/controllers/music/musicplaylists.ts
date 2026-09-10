import * as userSettings from '../../scripts/settings/userSettings';
import cardBuilder from '../../components/cardbuilder/cardBuilder';
import imageLoader from '../../components/images/imageLoader';
import loading from '../../components/loading/loading';
import type { ItemDtoQueryResult } from 'types/base/models/item-dto-query-result';

interface QueryParams {
    SortBy: string;
    SortOrder: string;
    IncludeItemTypes: string;
    Recursive: boolean;
    Fields: string;
    StartIndex: number;
    ParentId?: string;
}

interface PageData {
    query: QueryParams;
    view: string;
}

export default function (this: { getCurrentViewStyle: () => string; preRender: () => void; renderTab: () => void }, view: HTMLElement, params: { topParentId: string }, tabContent: HTMLElement) {
    function getPageData(): PageData {
        const key = getSavedQueryKey();
        let pageData = data[key] as PageData | undefined;

        if (!pageData) {
            pageData = data[key] = {
                query: {
                    SortBy: 'SortName',
                    SortOrder: 'Ascending',
                    IncludeItemTypes: 'Playlist',
                    Recursive: true,
                    Fields: 'PrimaryImageAspectRatio,SortName,CanDelete',
                    StartIndex: 0,
                    ParentId: params.topParentId
                },
                view: userSettings.getSavedView(key) || 'Poster'
            };
            userSettings.loadQuerySettings(key, pageData.query as unknown as Record<string, unknown>);
        }

        return pageData;
    }

    function getQuery(): QueryParams {
        return getPageData().query;
    }

    function getSavedQueryKey(): string {
        return `${params.topParentId}-musicplaylists`;
    }

    function getPromise(): Promise<ItemDtoQueryResult> {
        loading.show();
        const query = getQuery();
        return ApiClient.getItems(ApiClient.getCurrentUserId(), query as unknown as Record<string, unknown>);
    }

    function reloadItems(context: HTMLElement, promise: Promise<ItemDtoQueryResult>): void {
        const query = getQuery();
        promise.then(function (result: ItemDtoQueryResult) {
            let html = '';
            html = cardBuilder.getCardsHtml({
                items: result.Items ?? [],
                shape: 'square',
                showTitle: true,
                coverImage: true,
                centerText: true,
                overlayPlayButton: true,
                allowBottomPadding: true,
                cardLayout: false
            });
            const elem = context.querySelector('#items');
            if (!elem) {
                loading.hide();
                return;
            }
            elem.innerHTML = html;
            imageLoader.lazyChildren(elem);
            userSettings.saveQuerySettings(getSavedQueryKey(), query as unknown as Record<string, unknown>);
            loading.hide();

            void import('../../components/autoFocuser').then(({ default: autoFocuser }) => {
                autoFocuser.autoFocus(context);
            }).catch((error: unknown) => console.error('[MusicPlaylists] failed to focus page', error));
        }).catch((error: unknown) => {
            console.error('[MusicPlaylists] failed to load playlists', error);
            loading.hide();
        });
    }

    const data: Record<string, PageData> = {};

    this.getCurrentViewStyle = function () {
        return getPageData().view;
    };

    let promise: Promise<ItemDtoQueryResult> = Promise.resolve({ Items: [] });

    this.preRender = function () {
        promise = getPromise();
    };

    this.renderTab = function () {
        reloadItems(tabContent, promise);
    };
}
