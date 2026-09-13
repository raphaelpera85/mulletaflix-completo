import cardBuilder from '../../components/cardbuilder/cardBuilder';
import imageLoader from '../../components/images/imageLoader';
import { withLoading } from '../../components/loading/loading';
import type { ItemDto } from '../../types/base/models/item-dto';
import '../../elements/emby-button/paper-icon-button-light';
import '../../elements/emby-button/emby-button';

declare const ApiClient: {
    getLiveTvSeriesTimers(query: QueryParams): Promise<LiveTvSeriesTimersResponse>;
};

interface QueryParams {
    SortBy: string;
    SortOrder: string;
}

interface LiveTvSeriesTimersResponse {
    Items: ItemDto[];
}

interface SeriesTimersController {
    preRender: () => void;
    renderTab: () => void;
}

function renderTimers(context: HTMLElement, timers: ItemDto[]): void {
    const html = cardBuilder.getCardsHtml({
        items: timers,
        shape: 'auto',
        defaultShape: 'portrait',
        showTitle: true,
        cardLayout: false,
        preferThumb: 'auto',
        coverImage: true,
        overlayText: false,
        showSeriesTimerTime: true,
        showSeriesTimerChannel: true,
        centerText: true,
        overlayMoreButton: true,
        lines: 3
    });
    const elem = context.querySelector('#items') as HTMLElement;
    elem.innerHTML = html;
    imageLoader.lazyChildren(elem);
}

function reload(context: HTMLElement, promise: Promise<LiveTvSeriesTimersResponse>): void {
    void withLoading(async () => {
        const result = await promise;
        renderTimers(context, result.Items);
    }).catch((error: unknown) => {
        console.error('[LiveTvSeriesTimers] failed to load timers', error);
    });
}

const query: QueryParams = {
    SortBy: 'SortName',
    SortOrder: 'Ascending'
};

export default function (this: SeriesTimersController, view: HTMLElement, params: Record<string, string>, tabContent: HTMLElement): void {
    let timersPromise: Promise<LiveTvSeriesTimersResponse>;

    this.preRender = function (): void {
        timersPromise = ApiClient.getLiveTvSeriesTimers(query);
    };

    this.renderTab = function (): void {
        reload(tabContent, timersPromise);
    };
}
