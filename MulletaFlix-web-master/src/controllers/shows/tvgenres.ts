import escapeHtml from 'escape-html';

import cardBuilder from 'components/cardbuilder/cardBuilder';
import { getBackdropShape, getPortraitShape } from 'components/cardbuilder/utils/shape';
import lazyLoader from 'components/lazyLoader/lazyLoaderIntersectionObserver';
import layoutManager from 'components/layoutManager';
import { withLoading } from 'components/loading/loading';
import { appRouter } from 'components/router/appRouter';
import globalize from 'lib/globalize';
import * as userSettings from 'scripts/settings/userSettings';
import type { ItemDtoQueryResult } from 'types/base/models/item-dto-query-result';

import 'elements/emby-button/emby-button';

interface PageData {
    query: Record<string, unknown>;
    view: string;
}

interface ViewParams {
    topParentId: string;
}

interface TvGenresController {
    getViewStyles: () => string[];
    getCurrentViewStyle: () => string;
    setCurrentViewStyle: (viewStyle: string) => void;
    enableViewSelection: boolean;
    preRender: () => void;
    renderTab: () => void;
}

export default function (this: TvGenresController, view: HTMLElement, params: ViewParams, tabContent: HTMLElement): void {
    function getPageData(): PageData {
        const key = getSavedQueryKey();
        let pageData = data[key];

        if (!pageData) {
            pageData = data[key] = {
                query: {
                    SortBy: 'SortName',
                    SortOrder: 'Ascending',
                    IncludeItemTypes: 'Series',
                    Recursive: true,
                    EnableTotalRecordCount: false
                },
                view: 'Poster'
            };
            pageData.query.ParentId = params.topParentId;
            userSettings.loadQuerySettings(key, pageData.query);
        }

        return pageData;
    }

    function getQuery(): Record<string, unknown> {
        return getPageData().query;
    }

    function getSavedQueryKey(): string {
        return `${params.topParentId}-seriesgenres`;
    }

    function getPromise(): Promise<ItemDtoQueryResult> {
        const query = getQuery();
        return ApiClient.getGenres(ApiClient.getCurrentUserId(), query);
    }

    function enableScrollX(): boolean {
        return !layoutManager.desktop;
    }

    const fillItemsContainer = (entry: IntersectionObserverEntry, observer: IntersectionObserver): void => {
        if (!entry.isIntersecting) {
            return;
        }

        const elem = entry.target as HTMLElement;
        observer.unobserve(elem);

        const id = elem.getAttribute('data-id')!;
        const viewStyle = this.getCurrentViewStyle();
        let limit = viewStyle == 'Thumb' || viewStyle == 'ThumbCard' ? 5 : 9;

        if (enableScrollX()) {
            limit = 10;
        }

        const enableImageTypes = viewStyle == 'Thumb' || viewStyle == 'ThumbCard' ? 'Primary,Backdrop,Thumb' : 'Primary';
        const query = {
            SortBy: 'Random',
            SortOrder: 'Ascending',
            IncludeItemTypes: 'Series',
            Recursive: true,
            Fields: 'PrimaryImageAspectRatio,MediaSourceCount',
            ImageTypeLimit: 1,
            EnableImageTypes: enableImageTypes,
            Limit: limit,
            GenreIds: id,
            EnableTotalRecordCount: false,
            ParentId: params.topParentId
        };
        ApiClient.getItems(ApiClient.getCurrentUserId(), query).then(function (result: ItemDtoQueryResult) {
            const items = result.Items ?? [];
            if (viewStyle == 'Thumb') {
                cardBuilder.buildCards(items, {
                    itemsContainer: elem,
                    shape: getBackdropShape(enableScrollX()),
                    preferThumb: true,
                    showTitle: true,
                    scalable: true,
                    centerText: true,
                    overlayMoreButton: true,
                    allowBottomPadding: false
                });
            } else if (viewStyle == 'ThumbCard') {
                cardBuilder.buildCards(items, {
                    itemsContainer: elem,
                    shape: getBackdropShape(enableScrollX()),
                    preferThumb: true,
                    showTitle: true,
                    scalable: true,
                    centerText: false,
                    cardLayout: true,
                    showYear: true
                });
            } else if (viewStyle == 'PosterCard') {
                cardBuilder.buildCards(items, {
                    itemsContainer: elem,
                    shape: getPortraitShape(enableScrollX()),
                    showTitle: true,
                    scalable: true,
                    centerText: false,
                    cardLayout: true,
                    showYear: true
                });
            } else if (viewStyle == 'Poster') {
                cardBuilder.buildCards(items, {
                    itemsContainer: elem,
                    shape: getPortraitShape(enableScrollX()),
                    scalable: true,
                    showTitle: true,
                    centerText: true,
                    showYear: true,
                    overlayMoreButton: true,
                    allowBottomPadding: false
                });
            }
            if (items.length >= (query.Limit as number)) {
                tabContent.querySelector(`.btnMoreFromGenre${id} .material-icons`)?.classList.remove('hide');
            }
        }).catch((error: unknown) => console.error('[TvGenres] failed to load genre items', error));
    };

    function reloadItems(context: HTMLElement, promise: Promise<ItemDtoQueryResult>): void {
        const query = getQuery();
        void withLoading(async () => {
            const result = await promise;
            const elem = context.querySelector('#items');
            if (!elem) {
                return;
            }
            let html = '';
            const items = result.Items ?? [];

            for (const item of items) {
                html += '<div class="verticalSection">';
                html += '<div class="sectionTitleContainer sectionTitleContainer-cards padded-left">';
                html += '<a is="emby-linkbutton" href="' + escapeHtml(appRouter.getRouteUrl(item, {
                    context: 'tvshows',
                    parentId: params.topParentId
                })) + '" class="more button-flat button-flat-mini sectionTitleTextButton btnMoreFromGenre' + escapeHtml(item.Id || '') + '">';
                html += '<h2 class="sectionTitle sectionTitle-cards">';
                html += escapeHtml(item.Name);
                html += '</h2>';
                html += '<span class="material-icons hide chevron_right" aria-hidden="true"></span>';
                html += '</a>';
                html += '</div>';
                if (enableScrollX()) {
                    let scrollXClass = 'scrollX hiddenScrollX';
                    if (layoutManager.tv) {
                        scrollXClass += 'smoothScrollX padded-top-focusscale padded-bottom-focusscale';
                    }
                    html += '<div is="emby-itemscontainer" class="itemsContainer ' + scrollXClass + ' lazy padded-left padded-right" data-id="' + escapeHtml(item.Id || '') + '">';
                } else {
                    html += '<div is="emby-itemscontainer" class="itemsContainer vertical-wrap lazy padded-left padded-right" data-id="' + escapeHtml(item.Id || '') + '">';
                }
                html += '</div>';
                html += '</div>';
            }

            if (!items.length) {
                html = '';

                html += '<div class="noItemsMessage centerMessage">';
                html += '<h1>' + globalize.translate('MessageNothingHere') + '</h1>';
                html += '<p>' + globalize.translate('MessageNoGenresAvailable') + '</p>';
                html += '</div>';
            }

            elem.innerHTML = html;
            lazyLoader.lazyChildren(elem, fillItemsContainer);
            userSettings.saveQuerySettings(getSavedQueryKey(), query);
        }).catch((error: unknown) => {
            console.error('[TvGenres] failed to load genres', error);
        });
    }

    const fullyReload = (): void => {
        this.preRender();
        this.renderTab();
    };

    const data: Record<string, PageData> = {};

    this.getViewStyles = function (): string[] {
        return 'Poster,PosterCard,Thumb,ThumbCard'.split(',');
    };

    this.getCurrentViewStyle = function (): string {
        return getPageData().view;
    };

    this.setCurrentViewStyle = function (viewStyle: string): void {
        getPageData().view = viewStyle;
        userSettings.saveViewSetting(getSavedQueryKey(), viewStyle);
        fullyReload();
    };

    this.enableViewSelection = true;
    let promise: Promise<ItemDtoQueryResult> = Promise.resolve({ Items: [] });

    this.preRender = function (): void {
        promise = getPromise();
    };

    this.renderTab = function (): void {
        reloadItems(tabContent, promise);
    };
}
