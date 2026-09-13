import cardBuilder from 'components/cardbuilder/cardBuilder';
import { getBackdropShape } from 'components/cardbuilder/utils/shape';
import imageLoader from 'components/images/imageLoader';
import layoutManager from 'components/layoutManager';
import { withLoading } from 'components/loading/loading';
import datetime from 'scripts/datetime';
import globalize from 'lib/globalize';
import type { ItemDto } from 'types/base/models/item-dto';
import type { ItemDtoQueryResult } from 'types/base/models/item-dto-query-result';

import 'elements/emby-itemscontainer/emby-itemscontainer';

import 'styles/scrollstyles.scss';

interface ViewParams {
    topParentId: string;
}

function getUpcomingPromise(params: ViewParams): Promise<ItemDtoQueryResult> {
    const query: Record<string, unknown> = {
        Limit: 48,
        Fields: 'AirTime',
        UserId: ApiClient.getCurrentUserId(),
        ImageTypeLimit: 1,
        EnableImageTypes: 'Primary,Backdrop,Banner,Thumb',
        EnableTotalRecordCount: false
    };
    query.ParentId = params.topParentId;
    return ApiClient.getJSON(ApiClient.getUrl('Shows/Upcoming', query));
}

function loadUpcoming(context: HTMLElement, promise: Promise<ItemDtoQueryResult>): void {
    void withLoading(async () => {
        const result = await promise;
        const items = result.Items ?? [];
        const noItemsMessage = context.querySelector('.noItemsMessage');

        if (noItemsMessage instanceof HTMLElement) {
            noItemsMessage.style.display = items.length ? 'none' : 'block';
        }

        const upcomingItems = context.querySelector('#upcomingItems');
        if (upcomingItems instanceof HTMLElement) {
            renderUpcoming(upcomingItems, items);
        }
    }).catch((error: unknown) => {
        console.error('[TvUpcoming] failed to load upcoming shows', error);
    });
}

function enableScrollX(): boolean {
    return !layoutManager.desktop;
}

function getUpcomingDateText(item: ItemDto): string {
    if (!item.PremiereDate) {
        return '';
    }

    try {
        const premiereDate = datetime.parseISO8601Date(item.PremiereDate, true);
        return datetime.isRelativeDay(premiereDate, -1) ? globalize.translate('Yesterday') : datetime.toLocaleDateString(premiereDate, {
            weekday: 'long',
            month: 'short',
            day: 'numeric'
        });
    } catch (error) {
        console.error('error parsing timestamp for upcoming tv shows', error);
        return '';
    }
}

function groupUpcomingItems(items: ItemDto[]): { name: string; items: ItemDto[] }[] {
    const groups: { name: string; items: ItemDto[] }[] = [];
    let currentGroup: { name: string; items: ItemDto[] } | undefined;

    for (const item of items) {
        const name = getUpcomingDateText(item);
        if (!currentGroup || currentGroup.name !== name) {
            currentGroup = { name, items: [] };
            groups.push(currentGroup);
        }
        currentGroup.items.push(item);
    }

    return groups;
}

function renderUpcomingGroup(group: { name: string; items: ItemDto[] }): string {
    const horizontal = enableScrollX();
    const allowBottomPadding = !horizontal;
    let containerClass = 'vertical-wrap';
    if (horizontal) {
        containerClass = 'scrollX hiddenScrollX';
        if (layoutManager.tv) {
            containerClass += ' smoothScrollX';
        }
    }

    let html = '<div class="verticalSection">';
    html += '<h2 class="sectionTitle sectionTitle-cards padded-left">' + group.name + '</h2>';
    html += '<div is="emby-itemscontainer" class="itemsContainer ' + containerClass + ' padded-left padded-right">';
    html += cardBuilder.getCardsHtml({
        items: group.items,
        showLocationTypeIndicator: false,
        shape: getBackdropShape(horizontal),
        showTitle: true,
        preferThumb: true,
        lazy: true,
        showDetailsMenu: true,
        centerText: true,
        showParentTitle: true,
        overlayText: false,
        allowBottomPadding: allowBottomPadding,
        cardLayout: false,
        overlayMoreButton: true,
        missingIndicator: false
    });
    return html + '</div></div>';
}

function renderUpcoming(elem: HTMLElement, items: ItemDto[]): void {
    elem.innerHTML = groupUpcomingItems(items).map(renderUpcomingGroup).join('');
    imageLoader.lazyChildren(elem);
}

interface TvUpcomingController {
    preRender: () => void;
    renderTab: () => void;
}

export default function (this: TvUpcomingController, view: HTMLElement, params: ViewParams, tabContent: HTMLElement): void {
    let upcomingPromise: Promise<ItemDtoQueryResult> = Promise.resolve({ Items: [] });

    this.preRender = function (): void {
        upcomingPromise = getUpcomingPromise(params);
    };

    this.renderTab = function (): void {
        loadUpcoming(tabContent, upcomingPromise);
    };
}
