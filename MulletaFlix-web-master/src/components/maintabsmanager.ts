import dom from '../utils/dom';
import escapeHtml from 'escape-html';
import browser from '../scripts/browser';
import layoutManager from './layoutManager';
import Events, { type Event as EventsEvent } from '../utils/events.ts';
import '../elements/emby-tabs/emby-tabs';
import '../elements/emby-button/emby-button';

interface TabItem {
    cssClass?: string;
    enabled?: boolean;
    href?: string;
    name: string;
}

export interface TabChangeDetail {
    previousIndex?: number | string | null;
    selectedTabIndex: string;
}

type TabChangeHandler = (event: CustomEvent<TabChangeDetail>) => void;

interface TabsElement extends HTMLElement {
    readySelectedIndex?: number | null;
    selectedIndex(index?: number): void;
    selectNext(): void;
    selectPrevious(): void;
    triggerTabChange(): void;
}

interface TabsResult {
    replaced: boolean;
    tabs?: TabsElement | null;
    tabsContainer: HTMLElement | null;
}

interface ReplaceTabsOptions {
    view: HTMLElement;
    tabsContainer: HTMLElement;
    selectedIndex: number | null | undefined;
    getTabsFn: () => unknown[];
    getTabContainersFn?: () => ArrayLike<Element> | undefined;
    onBeforeTabChange?: TabChangeHandler | null;
    onTabChange?: TabChangeHandler | null;
    setSelectedIndex: boolean;
}

let tabOwnerView: HTMLElement | null = null;
let headerTabsContainer: HTMLElement | null = null;
let tabsElem: TabsElement | null = null;

function getHeaderTabsContainer(): HTMLElement | null {
    let container = document.querySelector<HTMLElement>('.skinHeader .headerTabs')
        ?? document.querySelector<HTMLElement>('.headerTabs');

    if (!container) {
        const skinHeader = document.querySelector<HTMLElement>('.skinHeader');
        if (skinHeader) {
            container = document.createElement('div');
            container.className = 'headerTabs sectionTabs hide';
            skinHeader.appendChild(container);
        }
    }

    return container;
}

function ensureElements(): void {
    if (!headerTabsContainer || !headerTabsContainer.isConnected) {
        headerTabsContainer = getHeaderTabsContainer();
    }
}

function onViewTabsReady(this: TabsElement): void {
    this.selectedIndex(this.readySelectedIndex ?? undefined);
    this.readySelectedIndex = null;
}

function allowSwipe(target: EventTarget | null): boolean {
    function allowSwipeOn(elem: HTMLElement): boolean {
        if (dom.parentWithTag(elem, 'input')) {
            return false;
        }

        const classList = elem.classList;
        if (classList) {
            return !classList.contains('scrollX') && !classList.contains('animatedScrollX');
        }

        return true;
    }

    let parent = target as HTMLElement | null;
    while (parent != null) {
        if (!allowSwipeOn(parent)) {
            return false;
        }
        parent = parent.parentNode as HTMLElement | null;
    }

    return true;
}

function configureSwipeTabs(view: HTMLElement, currentElement: TabsElement): void {
    if (!browser.touch || layoutManager.experimental) {
        return;
    }

    // implement without hammer
    const onSwipeLeft = function (_e: EventsEvent, target: EventTarget | null): void {
        if (allowSwipe(target) && view.contains(target as Node)) {
            currentElement.selectNext();
        }
    };

    const onSwipeRight = function (_e: EventsEvent, target: EventTarget | null): void {
        if (allowSwipe(target) && view.contains(target as Node)) {
            currentElement.selectPrevious();
        }
    };

    void import('../scripts/touchHelper').then(({ default: TouchHelper }) => {
        const container = view.parentNode?.parentNode as HTMLElement | null;
        if (!container) {
            return;
        }

        const touchHelper = new TouchHelper(container);

        Events.on(touchHelper, 'swipeleft', onSwipeLeft);
        Events.on(touchHelper, 'swiperight', onSwipeRight);

        view.addEventListener('viewdestroy', function () {
            touchHelper.destroy();
        });
    }).catch((error: unknown) => {
        console.error('[maintabsmanager] failed to initialize touch tabs', error);
    });
}

function updateTabContainers(event: Event, getTabContainersFn: () => ArrayLike<Element> | undefined): void {
    const tabEvent = event as CustomEvent<{ previousIndex?: number; selectedTabIndex: number }>;
    const tabContainers = getTabContainersFn();
    if (!tabContainers) return;

    if (tabEvent.detail.previousIndex != null) {
        const previousPanel = tabContainers[tabEvent.detail.previousIndex] as HTMLElement | undefined;
        previousPanel?.classList.remove('is-active');
    }

    const newPanel = tabContainers[tabEvent.detail.selectedTabIndex] as HTMLElement | undefined;
    newPanel?.classList.add('is-active');
}

function replaceTabs({
    view,
    tabsContainer,
    selectedIndex,
    getTabsFn,
    getTabContainersFn,
    onBeforeTabChange,
    onTabChange,
    setSelectedIndex
}: ReplaceTabsOptions): TabsResult {
    const tabs = getTabsFn() as TabItem[];
    tabsContainer.innerHTML = getTabsHtml(tabs, selectedIndex);
    window.customElements.upgrade(tabsContainer);

    document.body.classList.add('withSectionTabs');
    tabOwnerView = view;

    const currentTabsElem = tabsContainer.querySelector('[is="emby-tabs"]') as TabsElement | null;
    if (!currentTabsElem) {
        return { tabsContainer, replaced: true };
    }

    tabsElem = currentTabsElem;
    configureSwipeTabs(view, currentTabsElem);

    if (getTabContainersFn) {
        currentTabsElem.addEventListener('beforetabchange', event => updateTabContainers(event, getTabContainersFn));
    }
    if (onBeforeTabChange) {
        currentTabsElem.addEventListener('beforetabchange', onBeforeTabChange as EventListener);
    }
    if (onTabChange) {
        currentTabsElem.addEventListener('tabchange', onTabChange as EventListener);
    }

    if (setSelectedIndex) {
        if (currentTabsElem.selectedIndex) {
            currentTabsElem.selectedIndex(selectedIndex ?? undefined);
        } else {
            currentTabsElem.readySelectedIndex = selectedIndex ?? null;
            currentTabsElem.addEventListener('ready', onViewTabsReady);
        }
    }

    return { tabsContainer, tabs: currentTabsElem, replaced: true };
}

function getTabsHtml(tabs: TabItem[], selectedIndex: number | null | undefined): string {
    const indexAttribute = selectedIndex == null ? '' : (' data-index="' + escapeHtml(String(selectedIndex)) + '"');
    const tabsHtml = tabs.map((tab, index) => {
        let tabClass = 'emby-tab-button';

        if (tab.enabled === false) {
            tabClass += ' hide';
        }

        if (tab.cssClass) {
            tabClass += ' ' + escapeHtml(tab.cssClass);
        }

        const safeClass = escapeHtml(tabClass);
        const safeName = escapeHtml(tab.name);
        const dataIndex = String(index);

        if (tab.href) {
            return '<a href="' + escapeHtml(tab.href) + '" is="emby-linkbutton" class="' + safeClass + '" data-index="' + dataIndex + '"><div class="emby-button-foreground">' + safeName + '</div></a>';
        }

        return '<button type="button" is="emby-button" class="' + safeClass + '" data-index="' + dataIndex + '"><div class="emby-button-foreground">' + safeName + '</div></button>';
    }).join('');

    return '<div is="emby-tabs"' + indexAttribute + ' class="tabs-viewmenubar"><div class="emby-tabs-slider" style="white-space:nowrap;">' + tabsHtml + '</div></div>';
}

export function setTabs(
    view: HTMLElement | null,
    selectedIndex: number | null | undefined = 0,
    getTabsFn: () => unknown[] = () => [],
    getTabContainersFn?: () => ArrayLike<Element> | undefined,
    onBeforeTabChange?: TabChangeHandler | null,
    onTabChange?: TabChangeHandler | null,
    setSelectedIndex = true
): TabsResult {
    ensureElements();

    if (!headerTabsContainer) {
        return {
            tabsContainer: null,
            replaced: false
        };
    }

    if (!view) {
        if (tabOwnerView) {
            document.body.classList.remove('withSectionTabs');

            headerTabsContainer.innerHTML = '';
            headerTabsContainer.classList.add('hide');

            tabOwnerView = null;
        }

        return {
            tabsContainer: headerTabsContainer,
            replaced: false
        };
    }

    const tabsContainerElem = headerTabsContainer;

    if (!tabOwnerView) {
        tabsContainerElem.classList.remove('hide');
    }

    if (tabOwnerView !== view) {
        return replaceTabs({
            view,
            tabsContainer: tabsContainerElem,
            selectedIndex,
            getTabsFn,
            getTabContainersFn,
            onBeforeTabChange,
            onTabChange,
            setSelectedIndex: setSelectedIndex !== false
        });
    }

    if (!tabsElem) {
        return {
            tabsContainer: tabsContainerElem,
            replaced: false
        };
    }

    tabsElem.selectedIndex(selectedIndex ?? undefined);

    return {
        tabsContainer: tabsContainerElem,
        tabs: tabsElem,
        replaced: false
    };
}

export function selectedTabIndex(index?: number | null): void {
    const currentTabsElem = tabsElem;
    if (!currentTabsElem) {
        return;
    }

    if (index != null) {
        currentTabsElem.selectedIndex(index);
    } else {
        currentTabsElem.triggerTabChange();
    }
}

export function getTabsElement(): HTMLElement | null {
    return document.querySelector('.tabs-viewmenubar');
}
