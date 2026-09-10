import loading from '../../components/loading/loading';
import libraryBrowser from '../../scripts/libraryBrowser';
import imageLoader from '../../components/images/imageLoader';
import listView from '../../components/listview/listview';
import cardBuilder from '../../components/cardbuilder/cardBuilder';
import * as userSettings from '../../scripts/settings/userSettings';
import globalize from '../../lib/globalize';
import Dashboard from '../../utils/dashboard';
import Events from '../../utils/events';
import { setFilterStatus } from 'components/filterdialog/filterIndicator';
import type { ItemDtoQueryResult } from 'types/base/models/item-dto-query-result';

import '../../elements/emby-itemscontainer/emby-itemscontainer';

interface PageData {
    query: QueryParams;
    view: string;
}

interface QueryParams {
    [key: string]: unknown;
    SortBy: string;
    SortOrder: string;
    IncludeItemTypes: string;
    Recursive: boolean;
    Fields: string;
    IsMissing: boolean;
    ImageTypeLimit: number;
    EnableImageTypes: string;
    StartIndex: number;
    Limit?: number;
    ParentId?: string;
    NameStartsWith?: string;
    NameLessThan?: string;
}

interface ViewParams {
    topParentId: string;
}

interface EpisodesController {
    showFilterMenu: () => void;
    getCurrentViewStyle: () => string;
    renderTab: () => void;
}

interface LayoutChangeEvent extends Event {
    detail: { viewStyle: string };
}

export default function (this: EpisodesController, view: HTMLElement, params: ViewParams, tabContent: HTMLElement): void {
    function getPageData(): PageData {
        const key = getSavedQueryKey();
        let pageData = data[key];

        if (!pageData) {
            pageData = data[key] = {
                query: {
                    SortBy: 'SeriesSortName,SortName',
                    SortOrder: 'Ascending',
                    IncludeItemTypes: 'Episode',
                    Recursive: true,
                    Fields: 'PrimaryImageAspectRatio,MediaSourceCount',
                    IsMissing: false,
                    ImageTypeLimit: 1,
                    EnableImageTypes: 'Primary,Backdrop,Thumb',
                    StartIndex: 0
                },
                view: userSettings.getSavedView(key) || 'Poster'
            };

            if (userSettings.libraryPageSize() > 0) {
                pageData.query['Limit'] = userSettings.libraryPageSize();
            }

            pageData.query.ParentId = params.topParentId;
            userSettings.loadQuerySettings(key, pageData.query);
        }

        return pageData;
    }

    function getQuery(): QueryParams {
        return getPageData().query;
    }

    function getSavedQueryKey(): string {
        return `${params.topParentId}-episodes`;
    }

    const getCurrentViewStyle = (): string => getPageData().view;

    function onViewStyleChange(): void {
        const viewStyle = getCurrentViewStyle();
        const itemsContainer = tabContent.querySelector('.itemsContainer') as HTMLElement;

        if (viewStyle == 'List') {
            itemsContainer.classList.add('vertical-list');
            itemsContainer.classList.remove('vertical-wrap');
        } else {
            itemsContainer.classList.remove('vertical-list');
            itemsContainer.classList.add('vertical-wrap');
        }

        itemsContainer.innerHTML = '';
    }

    function reloadItems(page: HTMLElement): void {
        loading.show();
        isLoading = true;
        const query = getQuery();
        setFilterStatus(page, query);

        ApiClient.getItems(Dashboard.getCurrentUserId(), query).then(function (result: ItemDtoQueryResult) {
            function onNextPageClick(): void {
                if (isLoading) {
                    return;
                }

                if (userSettings.libraryPageSize() > 0) {
                    query.StartIndex += query.Limit ?? 0;
                }
                reloadItems(tabContent);
            }

            function onPreviousPageClick(): void {
                if (isLoading) {
                    return;
                }

                if (userSettings.libraryPageSize() > 0) {
                    query.StartIndex = Math.max(0, query.StartIndex - (query.Limit ?? 0));
                }
                reloadItems(tabContent);
            }

            window.scrollTo(0, 0);
            let html: string;
            const pagingHtml = libraryBrowser.getQueryPagingHtml({
                startIndex: query.StartIndex,
                limit: query.Limit ?? 0,
                totalRecordCount: result.TotalRecordCount ?? 0,
                addLayoutButton: false,
                sortButton: false,
                filterButton: false
            });
            const viewStyle = getCurrentViewStyle();
            const itemsContainer = tabContent.querySelector('.itemsContainer') as HTMLElement;
            if (viewStyle == 'List') {
                html = listView.getListViewHtml({
                    items: result.Items ?? [],
                    sortBy: query.SortBy,
                    showParentTitle: true
                });
            } else if (viewStyle == 'PosterCard') {
                html = cardBuilder.getCardsHtml({
                    items: result.Items ?? [],
                    shape: 'backdrop',
                    showTitle: true,
                    showParentTitle: true,
                    scalable: true,
                    cardLayout: true
                });
            } else {
                html = cardBuilder.getCardsHtml({
                    items: result.Items ?? [],
                    shape: 'backdrop',
                    showTitle: true,
                    showParentTitle: true,
                    overlayText: false,
                    centerText: true,
                    scalable: true,
                    overlayPlayButton: true
                });
            }
            let elems: NodeListOf<HTMLElement>;

            elems = tabContent.querySelectorAll('.paging');
            for (let i = 0, length = elems.length; i < length; i++) {
                elems[i].innerHTML = pagingHtml;
            }

            elems = tabContent.querySelectorAll('.btnNextPage');
            for (let i = 0, length = elems.length; i < length; i++) {
                elems[i].addEventListener('click', onNextPageClick);
            }

            elems = tabContent.querySelectorAll('.btnPreviousPage');
            for (let i = 0, length = elems.length; i < length; i++) {
                elems[i].addEventListener('click', onPreviousPageClick);
            }

            itemsContainer.innerHTML = html;
            imageLoader.lazyChildren(itemsContainer);
            userSettings.saveQuerySettings(getSavedQueryKey(), query);
            loading.hide();
            isLoading = false;

            void import('../../components/autoFocuser').then(({ default: autoFocuser }) => {
                autoFocuser.autoFocus(page);
            }).catch((error: unknown) => console.error('[Episodes] failed to focus page', error));
        }).catch((error: unknown) => {
            loading.hide();
            isLoading = false;
            console.error('[Episodes] failed to load episodes', error);
        });
    }

    const data: Record<string, PageData> = {};
    let isLoading = false;

    const showFilterMenu = function (): void {
        void import('../../components/filterdialog/filterdialog').then(({ default: FilterDialog }) => {
            const filterDialog = new FilterDialog({
                query: getQuery(),
                mode: 'episodes',
                serverId: ApiClient.serverId()
            });
            Events.on(filterDialog, 'filterchange', function () {
                getQuery().StartIndex = 0;
                reloadItems(tabContent);
            });
            void filterDialog.show().catch((error: unknown) => console.error('[Episodes] filter dialog failed', error));
        }).catch((error: unknown) => console.error('[Episodes] failed to open filter dialog', error));
    };

    function initPage(tabElement: HTMLElement): void {
        tabElement.querySelector('.btnFilter')!.addEventListener('click', function () {
            showFilterMenu();
        });
        tabElement.querySelector('.btnSort')!.addEventListener('click', function () {
            libraryBrowser.showSortMenu({
                items: [{
                    name: globalize.translate('Name'),
                    id: 'SeriesSortName,SortName'
                }, {
                    name: globalize.translate('OptionTvdbRating'),
                    id: 'CommunityRating,SeriesSortName,SortName'
                }, {
                    name: globalize.translate('OptionDateAdded'),
                    id: 'DateCreated,SeriesSortName,SortName'
                }, {
                    name: globalize.translate('OptionPremiereDate'),
                    id: 'PremiereDate,SeriesSortName,SortName'
                }, {
                    name: globalize.translate('OptionDatePlayed'),
                    id: 'DatePlayed,SeriesSortName,SortName'
                }, {
                    name: globalize.translate('OptionParentalRating'),
                    id: 'OfficialRating,SeriesSortName,SortName'
                }, {
                    name: globalize.translate('OptionPlayCount'),
                    id: 'PlayCount,SeriesSortName,SortName'
                }, {
                    name: globalize.translate('Runtime'),
                    id: 'Runtime,SeriesSortName,SortName'
                }],
                callback: function () {
                    reloadItems(tabElement);
                },
                query: getQuery()
            });
        });
        const btnSelectView = tabElement.querySelector('.btnSelectView') as HTMLElement;
        btnSelectView.addEventListener('click', function (e: Event) {
            libraryBrowser.showLayoutMenu(e.target as HTMLElement, getCurrentViewStyle(), 'List,Poster,PosterCard'.split(','));
        });
        btnSelectView.addEventListener('layoutchange', function (event: Event) {
            const viewStyle = (event as LayoutChangeEvent).detail.viewStyle;
            getPageData().view = viewStyle;
            userSettings.saveViewSetting(getSavedQueryKey(), viewStyle);
            onViewStyleChange();
            reloadItems(tabElement);
        });
    }

    initPage(tabContent);
    onViewStyleChange();

    const renderTab = function (): void {
        reloadItems(tabContent);
    };

    this.showFilterMenu = showFilterMenu;
    this.getCurrentViewStyle = getCurrentViewStyle;
    this.renderTab = renderTab;
}
