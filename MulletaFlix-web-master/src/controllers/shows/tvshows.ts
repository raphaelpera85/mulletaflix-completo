import loading from '../../components/loading/loading';
import libraryBrowser from '../../scripts/libraryBrowser';
import imageLoader from '../../components/images/imageLoader';
import listView from '../../components/listview/listview';
import cardBuilder from '../../components/cardbuilder/cardBuilder';
import AlphaPicker from '../../components/alphaPicker/alphaPicker';
import * as userSettings from '../../scripts/settings/userSettings';
import globalize from '../../lib/globalize';
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

interface TVShowsController {
    showFilterMenu: () => void;
    getCurrentViewStyle: () => string;
    renderTab: () => void;
    alphaPicker?: AlphaPicker;
}

interface AlphaValueChangeEvent extends Event {
    detail: { value: string };
}

interface LayoutChangeEvent extends Event {
    detail: { viewStyle: string };
}

export default function (this: TVShowsController, view: HTMLElement, params: ViewParams, tabContent: HTMLElement): void {
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
                    Fields: 'PrimaryImageAspectRatio',
                    ImageTypeLimit: 1,
                    EnableImageTypes: 'Primary,Backdrop,Banner,Thumb',
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
        return `${params.topParentId}-series`;
    }

    const onViewStyleChange = (): void => {
        const viewStyle = this.getCurrentViewStyle();
        const itemsContainer = tabContent.querySelector('.itemsContainer') as HTMLElement;

        if (viewStyle == 'List') {
            itemsContainer.classList.add('vertical-list');
            itemsContainer.classList.remove('vertical-wrap');
        } else {
            itemsContainer.classList.remove('vertical-list');
            itemsContainer.classList.add('vertical-wrap');
        }

        itemsContainer.innerHTML = '';
    };

    const reloadItems = (page: HTMLElement): void => {
        loading.show();
        isLoading = true;
        const query = getQuery();
        setFilterStatus(page, query);

        ApiClient.getItems(ApiClient.getCurrentUserId(), query).then((result: ItemDtoQueryResult) => {
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
            this.alphaPicker?.updateControls(query);
            let html: string;
            const pagingHtml = libraryBrowser.getQueryPagingHtml({
                startIndex: query.StartIndex,
                limit: query.Limit ?? 0,
                totalRecordCount: result.TotalRecordCount ?? 0,
                addLayoutButton: false,
                sortButton: false,
                filterButton: false
            });
            const viewStyle = this.getCurrentViewStyle();
            if (viewStyle == 'Thumb') {
                html = cardBuilder.getCardsHtml({
                    items: result.Items ?? [],
                    shape: 'backdrop',
                    preferThumb: true,
                    context: 'tvshows',
                    overlayMoreButton: true,
                    showTitle: true,
                    centerText: true
                });
            } else if (viewStyle == 'ThumbCard') {
                html = cardBuilder.getCardsHtml({
                    items: result.Items ?? [],
                    shape: 'backdrop',
                    preferThumb: true,
                    context: 'tvshows',
                    cardLayout: true,
                    showTitle: true,
                    showYear: true,
                    centerText: true
                });
            } else if (viewStyle == 'Banner') {
                html = cardBuilder.getCardsHtml({
                    items: result.Items ?? [],
                    shape: 'banner',
                    preferBanner: true,
                    context: 'tvshows'
                });
            } else if (viewStyle == 'List') {
                html = listView.getListViewHtml({
                    items: result.Items ?? [],
                    context: 'tvshows',
                    sortBy: query.SortBy
                });
            } else if (viewStyle == 'PosterCard') {
                html = cardBuilder.getCardsHtml({
                    items: result.Items ?? [],
                    shape: 'portrait',
                    context: 'tvshows',
                    showTitle: true,
                    showYear: true,
                    centerText: true,
                    cardLayout: true
                });
            } else {
                html = cardBuilder.getCardsHtml({
                    items: result.Items,
                    shape: 'portrait',
                    context: 'tvshows',
                    centerText: true,
                    lazy: true,
                    overlayMoreButton: true,
                    showTitle: true,
                    showYear: true
                });
            }

            let elems = tabContent.querySelectorAll('.paging');

            for (const elem of elems) {
                elem.innerHTML = pagingHtml;
            }

            elems = tabContent.querySelectorAll('.btnNextPage');
            for (const elem of elems) {
                elem.addEventListener('click', onNextPageClick);
            }

            elems = tabContent.querySelectorAll('.btnPreviousPage');
            for (const elem of elems) {
                elem.addEventListener('click', onPreviousPageClick);
            }

            const itemsContainer = tabContent.querySelector('.itemsContainer') as HTMLElement;
            itemsContainer.innerHTML = html;
            imageLoader.lazyChildren(itemsContainer);
            userSettings.saveQuerySettings(getSavedQueryKey(), query);
            loading.hide();
            isLoading = false;

            void import('../../components/autoFocuser').then(({ default: autoFocuser }) => {
                autoFocuser.autoFocus(page);
            }).catch((error: unknown) => console.error('[TVShows] failed to focus page', error));
        }).catch((error: unknown) => {
            loading.hide();
            isLoading = false;
            console.error('[TVShows] failed to load shows', error);
        });
    };

    const data: Record<string, PageData> = {};
    let isLoading = false;

    this.showFilterMenu = function (): void {
        void import('../../components/filterdialog/filterdialog').then(({ default: FilterDialog }) => {
            const filterDialog = new FilterDialog({
                query: getQuery(),
                mode: 'series',
                serverId: ApiClient.serverId()
            });
            Events.on(filterDialog, 'filterchange', function () {
                getQuery().StartIndex = 0;
                reloadItems(tabContent);
            });
            void filterDialog.show().catch((error: unknown) => console.error('[TVShows] filter dialog failed', error));
        }).catch((error: unknown) => console.error('[TVShows] failed to open filter dialog', error));
    };

    this.getCurrentViewStyle = function (): string {
        return getPageData().view;
    };

    const initPage = (tabElement: HTMLElement): void => {
        const alphaPickerElement = tabElement.querySelector('.alphaPicker')!;
        const itemsContainer = tabElement.querySelector('.itemsContainer') as HTMLElement;

        alphaPickerElement.addEventListener('alphavaluechanged', function (event: Event) {
            const newValue = (event as AlphaValueChangeEvent).detail.value;
            const query = getQuery();
            if (newValue === '#') {
                query.NameLessThan = 'A';
                delete query.NameStartsWith;
            } else {
                query.NameStartsWith = newValue;
                delete query.NameLessThan;
            }
            query.StartIndex = 0;
            reloadItems(tabElement);
        });
        this.alphaPicker = new AlphaPicker({
            element: alphaPickerElement as HTMLElement,
            valueChangeEvent: 'click'
        });

        tabElement.querySelector('.alphaPicker')!.classList.add('alphabetPicker-right');
        alphaPickerElement.classList.add('alphaPicker-fixed-right');
        itemsContainer.classList.add('padded-right-withalphapicker');

        tabElement.querySelector('.btnFilter')!.addEventListener('click', () => {
            this.showFilterMenu();
        });
        tabElement.querySelector('.btnSort')!.addEventListener('click', function () {
            libraryBrowser.showSortMenu({
                items: [{
                    name: globalize.translate('Name'),
                    id: 'SortName'
                }, {
                    name: globalize.translate('OptionRandom'),
                    id: 'Random'
                }, {
                    name: globalize.translate('OptionCommunityRating'),
                    id: 'CommunityRating,SortName'
                }, {
                    name: globalize.translate('OptionDateShowAdded'),
                    id: 'DateCreated,SortName'
                }, {
                    name: globalize.translate('OptionDateEpisodeAdded'),
                    id: 'DateLastContentAdded,SortName'
                }, {
                    name: globalize.translate('OptionDatePlayed'),
                    id: 'SeriesDatePlayed,SortName'
                }, {
                    name: globalize.translate('OptionParentalRating'),
                    id: 'OfficialRating,SortName'
                }, {
                    name: globalize.translate('OptionReleaseDate'),
                    id: 'PremiereDate,SortName'
                }],
                callback: function () {
                    getQuery().StartIndex = 0;
                    reloadItems(tabElement);
                },
                query: getQuery()
            });
        });
        const btnSelectView = tabElement.querySelector('.btnSelectView') as HTMLElement;
        btnSelectView.addEventListener('click', (e: Event) => {
            libraryBrowser.showLayoutMenu(e.target as HTMLElement, this.getCurrentViewStyle(), 'Banner,List,Poster,PosterCard,Thumb,ThumbCard'.split(','));
        });
        btnSelectView.addEventListener('layoutchange', function (event: Event) {
            const viewStyle = (event as LayoutChangeEvent).detail.viewStyle;
            getPageData().view = viewStyle;
            userSettings.saveViewSetting(getSavedQueryKey(), viewStyle);
            getQuery().StartIndex = 0;
            onViewStyleChange();
            reloadItems(tabElement);
        });
    };

    initPage(tabContent);
    onViewStyleChange();

    this.renderTab = (): void => {
        reloadItems(tabContent);
        this.alphaPicker?.updateControls(getQuery());
    };
}
