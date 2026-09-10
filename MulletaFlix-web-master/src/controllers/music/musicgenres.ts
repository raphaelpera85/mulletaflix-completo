import * as userSettings from '../../scripts/settings/userSettings';
import cardBuilder from '../../components/cardbuilder/cardBuilder';
import imageLoader from '../../components/images/imageLoader';
import loading from '../../components/loading/loading';
import type { ItemDtoQueryResult } from 'types/base/models/item-dto-query-result';

interface QueryParams {
    SortBy: string;
    SortOrder: string;
    Recursive: boolean;
    Fields: string;
    StartIndex: number;
    ParentId: string;
}

interface PageData {
    query: QueryParams;
    view: string;
}

export default function (this: { getViewStyles: () => string[]; getCurrentViewStyle: () => string; setCurrentViewStyle: (viewStyle: string) => void; enableViewSelection: boolean; preRender: () => void; renderTab: () => void }, view: HTMLElement, params: { topParentId: string }, tabContent: HTMLElement) {
    function getPageData(): PageData {
        const key = getSavedQueryKey();
        let pageData = data[key] as PageData | undefined;

        if (!pageData) {
            pageData = data[key] = {
                query: {
                    SortBy: 'SortName',
                    SortOrder: 'Ascending',
                    Recursive: true,
                    Fields: 'PrimaryImageAspectRatio,ItemCounts',
                    StartIndex: 0,
                    ParentId: params.topParentId
                },
                view: userSettings.getSavedView(key) || 'Poster'
            };
            userSettings.loadQuerySettings(key, pageData.query as unknown as Record<string, unknown>);
        }

        return pageData!;
    }

    function getQuery(): QueryParams {
        return getPageData().query;
    }

    function getSavedQueryKey(): string {
        return `${params.topParentId}-genres`;
    }

    function getPromise(): Promise<ItemDtoQueryResult> {
        loading.show();
        const query = getQuery();
        return ApiClient.getGenres(ApiClient.getCurrentUserId(), query as unknown as Record<string, unknown>);
    }

    const reloadItems = (context: HTMLElement, promise: Promise<ItemDtoQueryResult>): void => {
        const query = getQuery();
        void promise.then((result: ItemDtoQueryResult) => {
            let html = '';
            const viewStyle = this.getCurrentViewStyle();
            const items = result.Items ?? [];

            if (viewStyle == 'Thumb') {
                html = cardBuilder.getCardsHtml({
                    items: items,
                    shape: 'backdrop',
                    preferThumb: true,
                    context: 'music',
                    centerText: true,
                    overlayMoreButton: true,
                    showTitle: true
                });
            } else if (viewStyle == 'ThumbCard') {
                html = cardBuilder.getCardsHtml({
                    items: items,
                    shape: 'backdrop',
                    preferThumb: true,
                    context: 'music',
                    cardLayout: true,
                    showTitle: true
                });
            } else if (viewStyle == 'PosterCard') {
                html = cardBuilder.getCardsHtml({
                    items: items,
                    shape: 'auto',
                    context: 'music',
                    cardLayout: true,
                    showTitle: true
                });
            } else if (viewStyle == 'Poster') {
                html = cardBuilder.getCardsHtml({
                    items: items,
                    shape: 'auto',
                    context: 'music',
                    centerText: true,
                    overlayMoreButton: true,
                    showTitle: true
                });
            }

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
            }).catch((error: unknown) => console.error('[MusicGenres] failed to focus page', error));
        }).catch((error: unknown) => {
            loading.hide();
            console.error('[MusicGenres] failed to load genres', error);
        });
    };

    const fullyReload = (): void => {
        this.preRender();
        this.renderTab();
    };

    const data: Record<string, PageData> = {};

    this.getViewStyles = function () {
        return 'Poster,PosterCard,Thumb,ThumbCard'.split(',');
    };

    this.getCurrentViewStyle = function () {
        return getPageData().view;
    };

    this.setCurrentViewStyle = function (viewStyle: string) {
        getPageData().view = viewStyle;
        userSettings.saveViewSetting(getSavedQueryKey(), viewStyle);
        fullyReload();
    };

    this.enableViewSelection = true;
    let promise: Promise<ItemDtoQueryResult> = Promise.resolve({ Items: [] });

    this.preRender = function () {
        promise = getPromise();
    };

    this.renderTab = function () {
        reloadItems(tabContent, promise);
    };
}
