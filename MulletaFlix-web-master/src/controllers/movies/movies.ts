import loading from '../../components/loading/loading';
import * as userSettings from '../../scripts/settings/userSettings';
import libraryBrowser from '../../scripts/libraryBrowser';
import { AlphaPicker } from '../../components/alphaPicker/alphaPicker';
import listView from '../../components/listview/listview';
import cardBuilder from '../../components/cardbuilder/cardBuilder';
import globalize from '../../lib/globalize';
import Events from '../../utils/events';
import { playbackManager } from '../../components/playback/playbackmanager';
import { getFilterStatus, setFilterStatus } from 'components/filterdialog/filterIndicator';
import type { ItemDto } from 'types/base/models/item-dto';
import type { ItemDtoQueryResult } from 'types/base/models/item-dto-query-result';

import '../../elements/emby-itemscontainer/emby-itemscontainer';

interface QueryOptions {
    [key: string]: unknown;
    SortBy: string;
    SortOrder: string;
    IncludeItemTypes?: string;
    Recursive?: boolean;
    Fields?: string;
    ImageTypeLimit?: number;
    EnableImageTypes?: string;
    StartIndex: number;
    ParentId?: string;
    Limit?: number;
    NameLessThan?: string;
    NameStartsWith?: string;
    IsFavorite?: boolean;
}

interface ViewParams {
    topParentId: string;
    tab?: string;
}

interface TabOptions {
    mode?: string;
}

interface MoviesController {
    getCurrentViewStyle: () => string;
    showFilterMenu: () => void;
    renderTab: () => void;
    initTab: () => void;
    destroy: () => void;
    alphaPicker?: AlphaPicker;
}

interface ItemsContainerElement extends HTMLElement {
    fetchData?: (() => Promise<ItemDtoQueryResult>) | null;
    getItemsHtml?: ((items: ItemDto[]) => string) | null;
    afterRefresh?: (result: ItemDtoQueryResult) => void;
    refreshItems(): Promise<void>;
}

interface AlphaValueChangeEvent extends Event {
    detail: { value: string };
}

interface LayoutChangeEvent extends Event {
    detail: { viewStyle: string };
}

export default function (this: MoviesController, view: HTMLElement, params: ViewParams, tabContent: HTMLElement, options: TabOptions): void {
    const onViewStyleChange = (): void => {
        if (this.getCurrentViewStyle() == 'List') {
            itemsContainer.classList.add('vertical-list');
            itemsContainer.classList.remove('vertical-wrap');
        } else {
            itemsContainer.classList.remove('vertical-list');
            itemsContainer.classList.add('vertical-wrap');
        }

        itemsContainer.innerHTML = '';
    };

    function fetchData(): Promise<ItemDtoQueryResult> {
        isLoading = true;
        return loading.withLoading(() => ApiClient.getItems(ApiClient.getCurrentUserId(), query)).finally(() => {
            isLoading = false;
        });
    }

    function playAll(): void {
        ApiClient.getItem(ApiClient.getCurrentUserId(), params.topParentId).then(function (item: ItemDto) {
            playbackManager.play({
                items: [item]
            });
        }).catch((error: unknown) => console.error('[Movies] failed to load item for play all', error));
    }

    function shuffle(): Promise<void> {
        isLoading = true;
        const newQuery = { ...query, SortBy: 'Random', StartIndex: 0, Limit: 300, Fields: 'PrimaryImageAspectRatio,MediaSourceCount,Chapters,Trickplay' };
        return loading.withLoading(() => ApiClient.getItems(ApiClient.getCurrentUserId(), newQuery)).then(({ Items }: ItemDtoQueryResult) => {
            playbackManager.play({
                items: Items,
                autoplay: true
            });
        }).finally(() => {
            isLoading = false;
        });
    }

    const afterRefresh = (result: ItemDtoQueryResult): void => {
        setFilterStatus(tabContent, query);

        function onNextPageClick(): void {
            if (isLoading) {
                return;
            }

            if (userSettings.libraryPageSize() > 0) {
                query.StartIndex! += query.Limit!;
            }
            void itemsContainer.refreshItems();
        }

        function onPreviousPageClick(): void {
            if (isLoading) {
                return;
            }

            if (userSettings.libraryPageSize() > 0) {
                query.StartIndex = Math.max(0, query.StartIndex! - query.Limit!);
            }
            void itemsContainer.refreshItems();
        }

        window.scrollTo(0, 0);
        this.alphaPicker?.updateControls(query);
        const pagingHtml = libraryBrowser.getQueryPagingHtml({
            startIndex: query.StartIndex ?? 0,
            limit: query.Limit ?? 0,
            totalRecordCount: result.TotalRecordCount ?? 0,
            addLayoutButton: false,
            sortButton: false,
            filterButton: false
        });

        for (const elem of tabContent.querySelectorAll('.paging')) {
            elem.innerHTML = pagingHtml;
        }

        for (const elem of tabContent.querySelectorAll('.btnNextPage')) {
            elem.addEventListener('click', onNextPageClick);
        }

        for (const elem of tabContent.querySelectorAll('.btnPreviousPage')) {
            elem.addEventListener('click', onPreviousPageClick);
        }

        tabContent.querySelector('.btnPlayAll')?.classList.toggle('hide', (result.TotalRecordCount ?? 0) < 1);
        tabContent.querySelector('.btnShuffle')?.classList.toggle('hide', (result.TotalRecordCount ?? 0) < 1);

        isLoading = false;

        void import('../../components/autoFocuser').then(({ default: autoFocuser }) => {
            autoFocuser.autoFocus(tabContent);
        }).catch((error: unknown) => console.error('[Movies] failed to focus page', error));
    };

    const getItemsHtml = (items: ItemDto[]): string => {
        let html: string;
        const viewStyle = this.getCurrentViewStyle();

        if (viewStyle == 'Thumb') {
            html = cardBuilder.getCardsHtml({
                items: items,
                shape: 'backdrop',
                preferThumb: true,
                context: 'movies',
                lazy: true,
                overlayPlayButton: true,
                showTitle: true,
                showYear: true,
                centerText: true
            });
        } else if (viewStyle == 'ThumbCard') {
            html = cardBuilder.getCardsHtml({
                items: items,
                shape: 'backdrop',
                preferThumb: true,
                context: 'movies',
                lazy: true,
                cardLayout: true,
                showTitle: true,
                showYear: true,
                centerText: true
            });
        } else if (viewStyle == 'Banner') {
            html = cardBuilder.getCardsHtml({
                items: items,
                shape: 'banner',
                preferBanner: true,
                context: 'movies',
                lazy: true
            });
        } else if (viewStyle == 'List') {
            html = listView.getListViewHtml({
                items: items,
                context: 'movies',
                sortBy: query.SortBy
            });
        } else if (viewStyle == 'PosterCard') {
            html = cardBuilder.getCardsHtml({
                items: items,
                shape: 'portrait',
                context: 'movies',
                showTitle: true,
                showYear: true,
                centerText: true,
                lazy: true,
                cardLayout: true
            });
        } else {
            html = cardBuilder.getCardsHtml({
                items: items,
                shape: 'portrait',
                context: 'movies',
                overlayPlayButton: true,
                showTitle: true,
                showYear: true,
                centerText: true
            });
        }

        return html;
    };

    const initPage = (tabElement: HTMLElement): void => {
        itemsContainer.fetchData = fetchData;
        itemsContainer.getItemsHtml = getItemsHtml;
        itemsContainer.afterRefresh = afterRefresh;
        const alphaPickerElement = tabElement.querySelector('.alphaPicker');

        if (alphaPickerElement) {
            alphaPickerElement.addEventListener('alphavaluechanged', ((event: Event) => {
                const newValue = (event as AlphaValueChangeEvent).detail.value;
                if (newValue === '#') {
                    query.NameLessThan = 'A';
                    delete query.NameStartsWith;
                } else {
                    query.NameStartsWith = newValue;
                    delete query.NameLessThan;
                }
                query.StartIndex = 0;
                void itemsContainer.refreshItems();
            }) as EventListener);
            this.alphaPicker = new AlphaPicker({
                element: alphaPickerElement as HTMLElement,
                valueChangeEvent: 'click'
            });

            (tabElement.querySelector('.alphaPicker') as HTMLElement)!.classList.add('alphabetPicker-right');
            (alphaPickerElement as HTMLElement).classList.add('alphaPicker-fixed-right');
            itemsContainer.classList.add('padded-right-withalphapicker');
        }

        const btnFilter = tabElement.querySelector('.btnFilter');

        if (btnFilter) {
            btnFilter.addEventListener('click', () => {
                this.showFilterMenu();
            });
        }
        const btnSort = tabElement.querySelector('.btnSort');

        if (btnSort) {
            btnSort.addEventListener('click', function () {
                libraryBrowser.showSortMenu({
                    items: [{
                        name: globalize.translate('Name'),
                        id: 'SortName,ProductionYear'
                    }, {
                        name: globalize.translate('OptionRandom'),
                        id: 'Random'
                    }, {
                        name: globalize.translate('OptionCommunityRating'),
                        id: 'CommunityRating,SortName,ProductionYear'
                    }, {
                        name: globalize.translate('OptionCriticRating'),
                        id: 'CriticRating,SortName,ProductionYear'
                    }, {
                        name: globalize.translate('OptionDateAdded'),
                        id: 'DateCreated,SortName,ProductionYear'
                    }, {
                        name: globalize.translate('OptionDatePlayed'),
                        id: 'DatePlayed,SortName,ProductionYear'
                    }, {
                        name: globalize.translate('OptionParentalRating'),
                        id: 'OfficialRating,SortName,ProductionYear'
                    }, {
                        name: globalize.translate('OptionPlayCount'),
                        id: 'PlayCount,SortName,ProductionYear'
                    }, {
                        name: globalize.translate('OptionReleaseDate'),
                        id: 'PremiereDate,SortName,ProductionYear'
                    }, {
                        name: globalize.translate('Runtime'),
                        id: 'Runtime,SortName,ProductionYear'
                    }],
                    callback: function () {
                        query.StartIndex = 0;
                        userSettings.saveQuerySettings(savedQueryKey, query as Record<string, unknown>);
                        void itemsContainer.refreshItems();
                    },
                    query
                });
            });
        }
        const btnSelectView = tabElement.querySelector<HTMLElement>('.btnSelectView');
        if (btnSelectView) {
            btnSelectView.addEventListener('click', (e: Event) => {
                libraryBrowser.showLayoutMenu(e.target as HTMLElement, this.getCurrentViewStyle(), 'Banner,List,Poster,PosterCard,Thumb,ThumbCard'.split(','));
            });
            btnSelectView.addEventListener('layoutchange', ((event: Event) => {
                const viewStyle = (event as LayoutChangeEvent).detail.viewStyle;
                userSettings.set(savedViewKey, viewStyle);
                query.StartIndex = 0;
                onViewStyleChange();
                void itemsContainer.refreshItems();
            }) as EventListener);
        }

        tabElement.querySelector('.btnPlayAll')!.addEventListener('click', playAll);
        tabElement.querySelector('.btnShuffle')?.addEventListener('click', shuffle);
    };

    const itemsContainerElement = tabContent.querySelector('.itemsContainer');
    if (!(itemsContainerElement instanceof HTMLElement)) {
        return;
    }

    const itemsContainer = itemsContainerElement as ItemsContainerElement;
    const savedQueryKey = params.topParentId + '-' + options.mode;
    const savedViewKey = savedQueryKey + '-view';
    const query: QueryOptions = {
        SortBy: 'SortName,ProductionYear',
        SortOrder: 'Ascending',
        IncludeItemTypes: 'Movie',
        Recursive: true,
        Fields: 'PrimaryImageAspectRatio,MediaSourceCount',
        ImageTypeLimit: 1,
        EnableImageTypes: 'Primary,Backdrop,Banner,Thumb',
        StartIndex: 0,
        ParentId: params.topParentId
    };

    if (userSettings.libraryPageSize() > 0) {
        query['Limit'] = userSettings.libraryPageSize();
    }

    let isLoading = false;

    if (options.mode === 'favorites') {
        query.IsFavorite = true;
    }

    userSettings.loadQuerySettings(savedQueryKey, query);

    this.showFilterMenu = function (): void {
        void import('../../components/filterdialog/filterdialog').then(({ default: FilterDialog }) => {
            const filterDialog = new FilterDialog({
                query: query as unknown as Record<string, unknown>,
                mode: 'movies',
                serverId: ApiClient.serverId(),
                hasFilters: getFilterStatus(query)
            });
            Events.on(filterDialog, 'filterchange', () => {
                query.StartIndex = 0;
                userSettings.saveQuerySettings(savedQueryKey, query as Record<string, unknown>);
                void itemsContainer.refreshItems();
            });
            void filterDialog.show().catch((error: unknown) => console.error('[Movies] filter dialog failed', error));
        }).catch((error: unknown) => console.error('[Movies] failed to open filter dialog', error));
    };

    this.getCurrentViewStyle = function (): string {
        return userSettings.get(savedViewKey) || 'Poster';
    };

    this.initTab = function (): void {
        initPage(tabContent);
        onViewStyleChange();
    };

    this.renderTab = (): void => {
        void itemsContainer.refreshItems();
        this.alphaPicker?.updateControls(query);
    };

    this.destroy = function (): void {
        itemsContainer.fetchData = null;
        itemsContainer.getItemsHtml = null;
        itemsContainer.afterRefresh = undefined;
    };
}
