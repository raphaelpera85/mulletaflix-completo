import cardBuilder from '../../components/cardbuilder/cardBuilder';
import imageLoader from '../../components/images/imageLoader';
import libraryBrowser from '../../scripts/libraryBrowser';
import { withLoading } from '../../components/loading/loading';
import * as userSettings from '../../scripts/settings/userSettings';
import Events from '../../utils/events.ts';
import { setFilterStatus } from 'components/filterdialog/filterIndicator';

import '../../elements/emby-itemscontainer/emby-itemscontainer';

declare const ApiClient: {
    serverId(): string;
    getCurrentUserId(): string;
    getLiveTvChannels(query: Query): Promise<{ Items: Channel[]; TotalRecordCount: number }>;
};

interface Query {
    StartIndex: number;
    Fields: string;
    Limit?: number;
    UserId?: string;
    [key: string]: unknown;
}

interface PageData {
    query: Query;
}

interface Channel {
    [key: string]: unknown;
}

interface LiveTvChannelsController {
    renderTab: () => void;
}

export default function (
    this: LiveTvChannelsController,
    view: HTMLElement,
    params: Record<string, unknown>,
    tabContent: HTMLElement
): void {
    let pageData: PageData | undefined;
    let isLoading = false;

    function getPageData(): PageData {
        if (!pageData) {
            pageData = {
                query: {
                    StartIndex: 0,
                    Fields: 'PrimaryImageAspectRatio'
                }
            };
        }

        if (userSettings.libraryPageSize() > 0) {
            pageData.query['Limit'] = userSettings.libraryPageSize();
        }

        return pageData;
    }

    function getQuery(): Query {
        return getPageData().query;
    }

    function getChannelsHtml(channels: Channel[]): string {
        return cardBuilder.getCardsHtml({
            items: channels,
            shape: 'square',
            showTitle: true,
            lazy: true,
            cardLayout: true,
            showDetailsMenu: true,
            showCurrentProgram: true,
            showCurrentProgramTime: true
        });
    }

    function renderChannels(context: HTMLElement, result: { Items: Channel[]; TotalRecordCount: number }): void {
        const query = getQuery();

        function onNextPageClick(): void {
            if (isLoading) {
                return;
            }

            if (userSettings.libraryPageSize() > 0) {
                query.StartIndex += query.Limit!;
            }
            void reloadItems(context).then(() => {
                window.scrollTo(0, 0);
            }).catch((error: unknown) => console.error('Failed to load next TV channel page', error));
        }

        function onPreviousPageClick(): void {
            if (isLoading) {
                return;
            }

            if (userSettings.libraryPageSize() > 0) {
                query.StartIndex = Math.max(0, query.StartIndex - query.Limit!);
            }
            void reloadItems(context).then(() => {
                window.scrollTo(0, 0);
            }).catch((error: unknown) => console.error('Failed to load previous TV channel page', error));
        }

        for (const elem of context.querySelectorAll<HTMLElement>('.paging')) {
            elem.innerHTML = libraryBrowser.getQueryPagingHtml({
                startIndex: query.StartIndex,
                limit: query.Limit ?? 0,
                totalRecordCount: result.TotalRecordCount,
                filterButton: false
            });
        }

        const html = getChannelsHtml(result.Items);
        const elem = context.querySelector<HTMLElement>('#items');
        if (!elem) {
            return;
        }

        elem.innerHTML = html;
        imageLoader.lazyChildren(elem);

        let elems: NodeListOf<HTMLElement>;
        let i: number;
        let length: number;

        for (elems = context.querySelectorAll<HTMLElement>('.btnNextPage'), i = 0, length = elems.length; i < length; i++) {
            elems[i].addEventListener('click', onNextPageClick);
        }

        for (elems = context.querySelectorAll<HTMLElement>('.btnPreviousPage'), i = 0, length = elems.length; i < length; i++) {
            elems[i].addEventListener('click', onPreviousPageClick);
        }
    }

    function showFilterMenu(context: HTMLElement): void {
        void import('../../components/filterdialog/filterdialog').then(({ default: FilterDialog }) => {
            const filterDialog = new FilterDialog({
                query: getQuery(),
                mode: 'livetvchannels',
                serverId: ApiClient.serverId()
            });
            Events.on(filterDialog, 'filterchange', applyFilter.bind(null, context));
            filterDialog.show().catch((error: unknown) => console.error('Failed to show TV channel filter', error));
        }).catch((error: unknown) => console.error('Failed to open TV channel filter', error));
    }

    function applyFilter(context: HTMLElement): void {
        void reloadItems(context).catch((error: unknown) => console.error('Failed to apply TV channel filter', error));
    }

    function reloadItems(context: HTMLElement): Promise<void> {
        isLoading = true;
        const query = getQuery();
        setFilterStatus(context, query);

        const apiClient = ApiClient;
        query.UserId = apiClient.getCurrentUserId();
        return withLoading(async () => {
            const result = await apiClient.getLiveTvChannels(query);
            renderChannels(context, result);
            isLoading = false;

            void import('../../components/autoFocuser').then(({ default: autoFocuser }) => {
                autoFocuser.autoFocus(context);
            }).catch((error: unknown) => console.error('Failed to focus TV channels', error));
        }).catch((error: unknown) => {
            isLoading = false;
            console.error('Failed to load TV channels', error);
        });
    }

    const filterButton = tabContent.querySelector<HTMLElement>('.btnFilter');
    filterButton?.addEventListener('click', function () {
        showFilterMenu(tabContent);
    });

    this.renderTab = function (): void {
        void reloadItems(tabContent).catch((error: unknown) => console.error('Failed to render TV channels tab', error));
    };
}
