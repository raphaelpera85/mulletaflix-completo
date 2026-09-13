import cardBuilder from 'components/cardbuilder/cardBuilder';
import { getBackdropShape } from 'components/cardbuilder/utils/shape';
import imageLoader from 'components/images/imageLoader';
import layoutManager from 'components/layoutManager';
import { withLoading } from 'components/loading/loading';
import { getTimersHtml, type TimerItem, type TimerOptions } from 'scripts/livetvcomponents';
import Dashboard from 'utils/dashboard';

import 'elements/emby-button/emby-button';
import 'elements/emby-itemscontainer/emby-itemscontainer';

interface LiveTvItemsResult {
    Items: TimerItem[];
}

interface LiveTvApiClient {
    getLiveTvRecordings(options: Record<string, unknown>): Promise<LiveTvItemsResult>;
    getLiveTvTimers(options: Record<string, unknown>): Promise<LiveTvItemsResult>;
}

interface ScheduleController {
    preRender: () => void;
    renderTab: () => void;
}

declare const ApiClient: LiveTvApiClient;

function enableScrollX(): boolean {
    return !layoutManager.desktop;
}

function renderRecordings(elem: HTMLElement | null, recordings: TimerItem[], cardOptions?: Record<string, unknown>): void {
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

    if (enableScrollX()) {
        recordingItems.classList.add('scrollX');

        if (layoutManager.tv) {
            recordingItems.classList.add('smoothScrollX');
        }

        recordingItems.classList.add('hiddenScrollX');
        recordingItems.classList.remove('vertical-wrap');
    } else {
        recordingItems.classList.remove('scrollX');
        recordingItems.classList.remove('smoothScrollX');
        recordingItems.classList.remove('hiddenScrollX');
        recordingItems.classList.add('vertical-wrap');
    }

    recordingItems.innerHTML = cardBuilder.getCardsHtml(Object.assign({
        items: recordings,
        shape: enableScrollX() ? 'autooverflow' : 'auto',
        showTitle: true,
        showParentTitle: true,
        coverImage: true,
        cardLayout: false,
        centerText: true,
        allowBottomPadding: !enableScrollX(),
        preferThumb: 'auto'
    }, cardOptions || {}));
    imageLoader.lazyChildren(recordingItems);
}

async function renderActiveRecordings(context: HTMLElement, promise: Promise<LiveTvItemsResult>): Promise<void> {
    const result = await promise;
    renderRecordings(context.querySelector('#activeRecordings') as HTMLElement, result.Items, {
        shape: enableScrollX() ? 'autooverflow' : 'auto',
        defaultShape: getBackdropShape(enableScrollX()),
        showParentTitle: false,
        showParentTitleOrTitle: true,
        showTitle: true,
        showAirTime: true,
        showAirEndTime: true,
        showChannelName: true,
        coverImage: true,
        overlayText: false,
        overlayMoreButton: true
    });
}

async function renderTimers(context: HTMLElement | null, timers: TimerItem[], options?: TimerOptions): Promise<void> {
    if (!context) {
        return;
    }

    const html = await getTimersHtml(timers, options);
    const elem = context;

    if (html) {
        elem.classList.remove('hide');
    } else {
        elem.classList.add('hide');
    }

    const recordingItems = elem.querySelector('.recordingItems');
    if (recordingItems instanceof HTMLElement) {
        recordingItems.innerHTML = html;
        imageLoader.lazyChildren(elem);
    }
}

async function renderUpcomingRecordings(context: HTMLElement, promise: Promise<LiveTvItemsResult>): Promise<void> {
    const result = await promise;
    await renderTimers(context.querySelector('#upcomingRecordings') as HTMLElement, result.Items);
}

export default function (this: ScheduleController, view: HTMLElement, params: Record<string, unknown>, tabContent: HTMLElement): void {
    let activeRecordingsPromise: Promise<LiveTvItemsResult>;
    let upcomingRecordingsPromise: Promise<LiveTvItemsResult>;
    const recordingItems = tabContent.querySelector('#upcomingRecordings .recordingItems');
    recordingItems?.addEventListener('timercancelled', () => {
        this.preRender();
        this.renderTab();
    });

    this.preRender = function () {
        activeRecordingsPromise = ApiClient.getLiveTvRecordings({
            UserId: Dashboard.getCurrentUserId(),
            IsInProgress: true,
            Fields: 'CanDelete,PrimaryImageAspectRatio',
            EnableTotalRecordCount: false,
            EnableImageTypes: 'Primary,Thumb,Backdrop'
        });
        upcomingRecordingsPromise = ApiClient.getLiveTvTimers({
            IsActive: false,
            IsScheduled: true
        });
    };

    this.renderTab = function () {
        void withLoading(() => Promise.all([
            renderActiveRecordings(tabContent, activeRecordingsPromise),
            renderUpcomingRecordings(tabContent, upcomingRecordingsPromise)
        ])).catch((error: unknown) => console.error('[LiveTvSchedule] failed to load schedule', error));
    };
}
