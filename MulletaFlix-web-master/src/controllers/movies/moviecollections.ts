import loading from '../../components/loading/loading';
import libraryBrowser from '../../scripts/libraryBrowser';
import { AlphaPicker } from '../../components/alphaPicker/alphaPicker';
import imageLoader from '../../components/images/imageLoader';
import listView from '../../components/listview/listview';
import cardBuilder from '../../components/cardbuilder/cardBuilder';
import * as userSettings from '../../scripts/settings/userSettings';
import globalize from '../../lib/globalize';
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
    StartIndex: number;
    ImageTypeLimit: number;
    EnableImageTypes: string;
    ParentId?: string;
    Limit?: number;
    NameStartsWith?: string;
    NameLessThan?: string;
}

interface ViewParams {
    topParentId: string;
}

interface MovieCollectionsController {
    getCurrentViewStyle: () => string;
    renderTab: () => void;
    alphaPicker?: AlphaPicker;
}

interface LayoutChangeEvent extends Event {
    detail: { viewStyle: string };
}

interface AlphaValueChangeEvent extends Event {
    detail: { value: string };
}

export default function (this: MovieCollectionsController, view: HTMLElement, params: ViewParams, tabContent: HTMLElement): void {
    function getPageData(): PageData {
        const key = getSavedQueryKey();
        let pageData = data[key];

        if (!pageData) {
            pageData = data[key] = {
                query: {
                    SortBy: 'SortName',
                    SortOrder: 'Ascending',
                    IncludeItemTypes: 'BoxSet',
                    Recursive: true,
                    Fields: 'PrimaryImageAspectRatio,SortName',
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
        return params.topParentId + '-' + 'moviecollections';
    }

    const onViewStyleChange = (): void => {
        const viewStyle = this.getCurrentViewStyle();
        const itemsContainer = tabContent.querySelector('.itemsContainer');
        if (!(itemsContainer instanceof HTMLElement)) {
            return;
        }

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
        isLoading = true;
        const query = getQuery();
        this.alphaPicker?.updateControls(query);
        loading.withLoading(() => ApiClient.getItems(ApiClient.getCurrentUserId(), query)).then((result: ItemDtoQueryResult) => {
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
            const viewStyle = this.getCurrentViewStyle();
            if (viewStyle == 'Thumb') {
                html = cardBuilder.getCardsHtml({
                    items: result.Items ?? [],
                    shape: 'backdrop',
                    preferThumb: true,
                    context: 'movies',
                    overlayPlayButton: true,
                    centerText: true,
                    showTitle: true
                });
            } else if (viewStyle == 'ThumbCard') {
                html = cardBuilder.getCardsHtml({
                    items: result.Items ?? [],
                    shape: 'backdrop',
                    preferThumb: true,
                    context: 'movies',
                    lazy: true,
                    cardLayout: true,
                    showTitle: true
                });
            } else if (viewStyle == 'Banner') {
                html = cardBuilder.getCardsHtml({
                    items: result.Items ?? [],
                    shape: 'banner',
                    preferBanner: true,
                    context: 'movies',
                    lazy: true
                });
            } else if (viewStyle == 'List') {
                html = listView.getListViewHtml({
                    items: result.Items ?? [],
                    context: 'movies',
                    sortBy: query.SortBy
                });
            } else if (viewStyle == 'PosterCard') {
                html = cardBuilder.getCardsHtml({
                    items: result.Items ?? [],
                    shape: 'auto',
                    context: 'movies',
                    showTitle: true,
                    centerText: false,
                    cardLayout: true
                });
            } else {
                html = cardBuilder.getCardsHtml({
                    items: result.Items ?? [],
                    shape: 'auto',
                    context: 'movies',
                    centerText: true,
                    overlayPlayButton: true,
                    showTitle: true
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

            if (!(result.Items ?? []).length) {
                html = '';

                html += '<div class="noItemsMessage centerMessage">';
                html += '<h1>' + globalize.translate('MessageNothingHere') + '</h1>';
                html += '<p>' + globalize.translate('MessageNoCollectionsAvailable') + '</p>';
                html += '</div>';
            }

            const itemsContainer = tabContent.querySelector('.itemsContainer') as HTMLElement;
            itemsContainer.innerHTML = html;
            imageLoader.lazyChildren(itemsContainer);
            userSettings.saveQuerySettings(getSavedQueryKey(), query);
            isLoading = false;

            void import('../../components/autoFocuser').then(({ default: autoFocuser }) => {
                autoFocuser.autoFocus(page);
            }).catch((error: unknown) => console.error('[MovieCollections] failed to focus page', error));
        }).catch((error: unknown) => {
            isLoading = false;
            console.error('[MovieCollections] failed to load collections', error);
        });
    };

    const data: Record<string, PageData> = {};
    let isLoading = false;

    this.getCurrentViewStyle = function (): string {
        return getPageData().view;
    };

    const initPage = (tabElement: HTMLElement): void => {
        tabElement.querySelector('.btnSort')!.addEventListener('click', function () {
            libraryBrowser.showSortMenu({
                items: [{
                    name: globalize.translate('Name'),
                    id: 'SortName'
                }, {
                    name: globalize.translate('OptionCommunityRating'),
                    id: 'CommunityRating,SortName'
                }, {
                    name: globalize.translate('OptionDateAdded'),
                    id: 'DateCreated,SortName'
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
            libraryBrowser.showLayoutMenu(e.target as HTMLElement, this.getCurrentViewStyle(), 'List,Poster,PosterCard,Thumb,ThumbCard'.split(','));
        });
        btnSelectView.addEventListener('layoutchange', ((event: Event) => {
            const viewStyle = (event as LayoutChangeEvent).detail.viewStyle;
            getPageData().view = viewStyle;
            userSettings.saveViewSetting(getSavedQueryKey(), viewStyle);
            getQuery().StartIndex = 0;
            onViewStyleChange();
            reloadItems(tabElement);
        }) as EventListener);
        tabElement.querySelector('.btnNewCollection')!.addEventListener('click', () => {
            void import('../../components/collectionEditor/collectionEditor').then(({ default: CollectionEditor }) => {
                const serverId = ApiClient.serverInfo().Id;
                if (!serverId) {
                    console.error('[MovieCollections] Cannot open collection editor without a server id');
                    return;
                }
                const collectionEditor = new CollectionEditor();
                void collectionEditor.show({
                    items: [],
                    serverId: serverId
                }).catch((error: unknown) => console.error('[MovieCollections] failed to open collection editor', error));
            }).catch((error: unknown) => console.error('[MovieCollections] failed to open collection editor', error));
        });

        const alphaPickerElement = tabElement.querySelector('.alphaPicker');

        if (alphaPickerElement) {
            alphaPickerElement.addEventListener('alphavaluechanged', ((event: Event) => {
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
            }) as EventListener);
            this.alphaPicker = new AlphaPicker({
                element: alphaPickerElement as HTMLElement,
                valueChangeEvent: 'click'
            });

            tabElement.querySelector('.alphaPicker')!.classList.add('alphabetPicker-right');
            alphaPickerElement.classList.add('alphaPicker-fixed-right');
            tabElement.querySelector('.itemsContainer')!.classList.add('padded-right-withalphapicker');
        }
    };

    initPage(tabContent);
    onViewStyleChange();

    this.renderTab = function (): void {
        reloadItems(tabContent);
        this.alphaPicker?.updateControls(getQuery());
    };
}
