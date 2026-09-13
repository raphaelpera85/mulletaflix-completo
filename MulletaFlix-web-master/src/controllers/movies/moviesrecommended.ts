import escapeHtml from 'escape-html';

import cardBuilder from 'components/cardbuilder/cardBuilder';
import { getBackdropShape, getPortraitShape } from 'components/cardbuilder/utils/shape';
import imageLoader from 'components/images/imageLoader';
import layoutManager from 'components/layoutManager';
import * as mainTabsManager from 'components/maintabsmanager';
import type { TabChangeDetail } from 'components/maintabsmanager';
import { playbackManager } from 'components/playback/playbackmanager';
import dom from 'utils/dom';
import globalize from 'lib/globalize';
import inputManager from 'scripts/inputManager';
import libraryMenu from 'scripts/libraryMenu';
import * as userSettings from 'scripts/settings/userSettings';
import { LibraryTab } from 'types/libraryTab';
import type { ItemDto } from 'types/base/models/item-dto';
import Dashboard from 'utils/dashboard';
import Events, { type Event as EventsEvent } from 'utils/events';

import 'elements/emby-scroller/emby-scroller';
import 'elements/emby-itemscontainer/emby-itemscontainer';
import 'elements/emby-tabs/emby-tabs';
import 'elements/emby-button/emby-button';

interface Recommendation {
    RecommendationType: string;
    BaselineItemName: string;
    Items: ItemDto[];
}

interface PlaybackState {
    NowPlayingItem?: {
        MediaType?: string;
    };
}

interface InputCommandEvent extends Event {
    detail?: {
        command?: string;
    };
}

interface TabsElementWithTrigger extends HTMLElement {
    triggerTabChange(): void;
}

function enableScrollX(): boolean {
    return !layoutManager.desktop;
}

function loadLatest(page: HTMLElement, userId: string, parentId: string): Promise<void> {
    const options = {
        IncludeItemTypes: 'Movie',
        Limit: 18,
        Fields: 'PrimaryImageAspectRatio,MediaSourceCount',
        ParentId: parentId,
        ImageTypeLimit: 1,
        EnableImageTypes: 'Primary,Backdrop,Banner,Thumb',
        EnableTotalRecordCount: false
    };
    return ApiClient.getJSON<ItemDto[]>(ApiClient.getUrl('Users/' + userId + '/Items/Latest', options)).then(function (items: ItemDto[]) {
        const allowBottomPadding = !enableScrollX();
        const container = page.querySelector('#recentlyAddedItems');
        cardBuilder.buildCards(items, {
            itemsContainer: container,
            shape: getPortraitShape(enableScrollX()),
            scalable: true,
            overlayPlayButton: true,
            allowBottomPadding: allowBottomPadding,
            showTitle: true,
            showYear: true,
            centerText: true
        });
    }).catch((error: unknown) => console.error('[MoviesRecommended] failed to load latest items', error));
}

function loadResume(page: HTMLElement, userId: string, parentId: string): Promise<void> {
    const screenWidth = dom.getWindowSize().innerWidth;
    const options = {
        SortBy: 'DatePlayed',
        SortOrder: 'Descending',
        IncludeItemTypes: 'Movie',
        Filters: 'IsResumable',
        Limit: screenWidth >= 1600 ? 5 : 3,
        Recursive: true,
        Fields: 'PrimaryImageAspectRatio,MediaSourceCount',
        CollapseBoxSetItems: false,
        ParentId: parentId,
        ImageTypeLimit: 1,
        EnableImageTypes: 'Primary,Backdrop,Banner,Thumb',
        EnableTotalRecordCount: false
    };
    return ApiClient.getItems(userId, options).then(function (result) {
        const items = result.Items || [];
        if (items.length) {
            page.querySelector('#resumableSection')!.classList.remove('hide');
        } else {
            page.querySelector('#resumableSection')!.classList.add('hide');
        }

        const allowBottomPadding = !enableScrollX();
        const container = page.querySelector('#resumableItems');
        cardBuilder.buildCards(items, {
            itemsContainer: container,
            preferThumb: true,
            shape: getBackdropShape(enableScrollX()),
            scalable: true,
            overlayPlayButton: true,
            allowBottomPadding: allowBottomPadding,
            cardLayout: false,
            showTitle: true,
            showYear: true,
            centerText: true
        });
    }).catch((error: unknown) => console.error('[MoviesRecommended] failed to load resumable items', error));
}

function getRecommendationHtml(recommendation: Recommendation): string {
    let html = '';
    let title = '';

    switch (recommendation.RecommendationType) {
        case 'SimilarToRecentlyPlayed':
            title = globalize.translate('RecommendationBecauseYouWatched', recommendation.BaselineItemName);
            break;

        case 'SimilarToLikedItem':
            title = globalize.translate('RecommendationBecauseYouLike', recommendation.BaselineItemName);
            break;

        case 'HasDirectorFromRecentlyPlayed':
        case 'HasLikedDirector':
            title = globalize.translate('RecommendationDirectedBy', recommendation.BaselineItemName);
            break;

        case 'HasActorFromRecentlyPlayed':
        case 'HasLikedActor':
            title = globalize.translate('RecommendationStarring', recommendation.BaselineItemName);
            break;
    }

    html += '<div class="verticalSection">';
    html += '<h2 class="sectionTitle sectionTitle-cards padded-left">' + escapeHtml(title) + '</h2>';
    const allowBottomPadding = true;

    if (enableScrollX()) {
        html += '<div is="emby-scroller" class="padded-top-focusscale padded-bottom-focusscale" data-mousewheel="false" data-centerfocus="true">';
        html += '<div is="emby-itemscontainer" class="itemsContainer scrollSlider focuscontainer-x">';
    } else {
        html += '<div is="emby-itemscontainer" class="itemsContainer focuscontainer-x padded-left padded-right vertical-wrap">';
    }

    html += cardBuilder.getCardsHtml(recommendation.Items, {
        shape: getPortraitShape(enableScrollX()),
        scalable: true,
        overlayPlayButton: true,
        allowBottomPadding: allowBottomPadding,
        showTitle: true,
        showYear: true,
        centerText: true
    });

    if (enableScrollX()) {
        html += '</div>';
    }
    html += '</div>';
    html += '</div>';
    return html;
}

function loadSuggestions(page: HTMLElement, userId: string): Promise<void> {
    const screenWidth = dom.getWindowSize().innerWidth;
    let itemLimit = 5;
    if (screenWidth >= 1600) {
        itemLimit = 8;
    } else if (screenWidth >= 1200) {
        itemLimit = 6;
    }

    const url = ApiClient.getUrl('Movies/Recommendations', {
        userId: userId,
        categoryLimit: 6,
        ItemLimit: itemLimit,
        Fields: 'PrimaryImageAspectRatio,MediaSourceCount',
        ImageTypeLimit: 1,
        EnableImageTypes: 'Primary,Backdrop,Banner,Thumb'
    });
    return ApiClient.getJSON<Recommendation[]>(url).then(function (recommendations: Recommendation[]) {
        if (!recommendations.length) {
            page.querySelector('.noItemsMessage')!.classList.remove('hide');
            (page.querySelector('.recommendations') as HTMLElement).innerHTML = '';
            return;
        }

        const html = recommendations.map(getRecommendationHtml).join('');
        page.querySelector('.noItemsMessage')!.classList.add('hide');
        const recs = page.querySelector('.recommendations') as HTMLElement;
        recs.innerHTML = html;
        imageLoader.lazyChildren(recs);
    }).catch((error: unknown) => console.error('[MoviesRecommended] failed to load recommendations', error));
}

function autoFocus(page: HTMLElement): void {
    void import('../../components/autoFocuser').then(({ default: autoFocuser }) => {
        autoFocuser.autoFocus(page);
    }).catch((error: unknown) => console.error('[MoviesRecommended] failed to focus page', error));
}

function setScrollClasses(elem: HTMLElement, scrollX: boolean): void {
    if (scrollX) {
        elem.classList.add('hiddenScrollX');

        if (layoutManager.tv) {
            elem.classList.add('smoothScrollX');
            elem.classList.add('padded-top-focusscale');
            elem.classList.add('padded-bottom-focusscale');
        }

        elem.classList.add('scrollX');
        elem.classList.remove('vertical-wrap');
    } else {
        elem.classList.remove('hiddenScrollX');
        elem.classList.remove('smoothScrollX');
        elem.classList.remove('scrollX');
        elem.classList.add('vertical-wrap');
    }
}

function initSuggestedTab(page: HTMLElement, tabContent: HTMLElement): void {
    const containers = tabContent.querySelectorAll('.itemsContainer');

    for (const container of containers) {
        setScrollClasses(container as HTMLElement, enableScrollX());
    }
}

function loadSuggestionsTab(view: HTMLElement, params: ViewParams, tabContent: HTMLElement): void {
    const parentId = params.topParentId;
    const userId = ApiClient.getCurrentUserId();
    Promise.all([
        loadResume(tabContent, userId, parentId),
        loadLatest(tabContent, userId, parentId),
        loadSuggestions(tabContent, userId)
    ]).then(() => autoFocus(tabContent)).catch((error: unknown) => console.error('[MoviesRecommended] failed to initialize suggestions tab', error));
}

function getTabs(): { name: string }[] {
    return [{
        name: globalize.translate('Movies')
    }, {
        name: globalize.translate('Suggestions')
    }, {
        name: globalize.translate('Favorites')
    }, {
        name: globalize.translate('Collections')
    }, {
        name: globalize.translate('Genres')
    }];
}

function getDefaultTabIndex(folderId: string): number {
    switch (userSettings.get('landing-' + folderId)) {
        case LibraryTab.Suggestions:
            return 1;

        case LibraryTab.Favorites:
            return 2;

        case LibraryTab.Collections:
            return 3;

        case LibraryTab.Genres:
            return 4;

        default:
            return 0;
    }
}

interface ViewParams {
    topParentId: string;
    tab?: string;
}

interface MovieTabController {
    preRender?(): void;
    renderTab?(): void;
    initTab?(): void;
    destroy?(): void;
}

interface MoviesRecommendedController extends MovieTabController {
    tabContent?: HTMLElement;
}

interface ItemTitleResult {
    Name?: string | null;
}

export default function (this: MoviesRecommendedController, view: HTMLElement, params: ViewParams): void {
    function onBeforeTabChange(e: CustomEvent<TabChangeDetail>): void {
        preLoadTab(view, parseInt(e.detail.selectedTabIndex, 10));
    }

    function onTabChange(e: CustomEvent<TabChangeDetail>): void {
        const newIndex = parseInt(e.detail.selectedTabIndex, 10);
        loadTab(view, newIndex);
    }

    function getTabContainers(): NodeListOf<HTMLElement> {
        return view.querySelectorAll('.pageTabContent');
    }

    function initTabs(): void {
        mainTabsManager.setTabs(view, currentTabIndex, getTabs, getTabContainers, onBeforeTabChange, onTabChange);
    }

    const getTabController = (page: HTMLElement, index: number, callback: (controller: MovieTabController) => void): void => {
        let depends = 'movies';

        switch (index) {
            case 1:
                depends = 'moviesrecommended';
                break;

            case 3:
                depends = 'moviecollections';
                break;

            case 4:
                depends = 'moviegenres';
                break;
        }

        void import(`../movies/${depends}.ts`).then(({ default: ControllerFactory }) => {
            let tabContent: HTMLElement;

            if (index === suggestionsTabIndex) {
                tabContent = view.querySelector(`.pageTabContent[data-index='${index}']`) as HTMLElement;
                this.tabContent = tabContent;
            }

            let controller = tabControllers[index];

            if (!controller) {
                tabContent = view.querySelector(`.pageTabContent[data-index='${index}']`) as HTMLElement;

                if (index === suggestionsTabIndex) {
                    callback(this);
                    return;
                }

                if (index == 0 || index == 2) {
                    controller = new ControllerFactory(view, params, tabContent, {
                        mode: index ? 'favorites' : 'movies'
                    });
                } else {
                    controller = new ControllerFactory(view, params, tabContent);
                }

                tabControllers[index] = controller;

                if (controller.initTab) {
                    controller.initTab();
                }
            }

            callback(controller);
        }).catch((error: unknown) => console.error('[MoviesRecommended] failed to load tab controller', error));
    };

    function preLoadTab(page: HTMLElement, index: number): void {
        getTabController(page, index, function (controller: MovieTabController) {
            if (renderedTabs.indexOf(index) == -1 && controller.preRender) {
                controller.preRender();
            }
        });
    }

    function loadTab(page: HTMLElement, index: number): void {
        currentTabIndex = index;
        getTabController(page, index, ((controller: MovieTabController) => {
            if (renderedTabs.indexOf(index) == -1) {
                renderedTabs.push(index);
                controller.renderTab?.();
            }
        }));
    }

    function onPlaybackStop(_event: EventsEvent, state: PlaybackState): void {
        if (state.NowPlayingItem && state.NowPlayingItem.MediaType == 'Video') {
            renderedTabs = [];
            const tabsElement = mainTabsManager.getTabsElement() as TabsElementWithTrigger | null;
            tabsElement?.triggerTabChange();
        }
    }

    function onInputCommand(e: InputCommandEvent): void {
        if (e.detail?.command === 'search') {
            e.preventDefault();
            void Dashboard.navigate('search?collectionType=movies&parentId=' + params.topParentId).catch((error: unknown) => console.error('[MoviesRecommended] failed to open search', error));
        }
    }

    let currentTabIndex = parseInt(String(params.tab || getDefaultTabIndex(params.topParentId)), 10);
    const suggestionsTabIndex = 1;

    this.initTab = function (): void {
        const tabContent = view.querySelector(`.pageTabContent[data-index='${suggestionsTabIndex}']`) as HTMLElement;
        initSuggestedTab(view, tabContent);
    };

    this.renderTab = function (): void {
        const tabContent = view.querySelector(`.pageTabContent[data-index='${suggestionsTabIndex}']`) as HTMLElement;
        loadSuggestionsTab(view, params, tabContent);
    };

    const tabControllers: MovieTabController[] = [];
    let renderedTabs: number[] = [];
    view.addEventListener('viewshow', function () {
        initTabs();
        if (!view.getAttribute('data-title')) {
            const parentId = params.topParentId;

            if (parentId) {
                ApiClient.getItem(ApiClient.getCurrentUserId(), parentId).then(function (item: ItemTitleResult) {
                    const title = item.Name || globalize.translate('Movies');
                    view.setAttribute('data-title', title);
                    libraryMenu.setTitle(title);
                }).catch((error: unknown) => console.error('[MoviesRecommended] failed to load parent title', error));
            } else {
                view.setAttribute('data-title', globalize.translate('Movies'));
                libraryMenu.setTitle(globalize.translate('Movies'));
            }
        }

        Events.on(playbackManager, 'playbackstop', onPlaybackStop);
        inputManager.on(window, onInputCommand);
    });
    view.addEventListener('viewbeforehide', function () {
        inputManager.off(window, onInputCommand);
    });
    for (const tabController of tabControllers) {
        if (tabController.destroy) {
            tabController.destroy();
        }
    }
}
