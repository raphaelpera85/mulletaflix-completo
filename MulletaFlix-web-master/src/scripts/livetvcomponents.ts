import cardBuilder from 'components/cardbuilder/cardBuilder';
import { getBackdropShape } from 'components/cardbuilder/utils/shape';
import layoutManager from 'components/layoutManager';

import datetime from './datetime';

function enableScrollX(): boolean {
    return !layoutManager.desktop;
}

export interface TimerItem {
    Type?: string;
    StartDate?: string;
    Id?: string;
}

interface TimerGroup {
    name: string;
    items: TimerItem[];
}

export interface TimerOptions {
    indexByDate?: boolean;
}

function getTimerDateText(item: TimerItem, indexByDate: boolean): string {
    if (!indexByDate || !item.StartDate) {
        return '';
    }

    try {
        const premiereDate = datetime.parseISO8601Date(item.StartDate, true);
        return datetime.toLocaleDateString(premiereDate, {
            weekday: 'long',
            month: 'short',
            day: 'numeric'
        });
    } catch (err) {
        console.error('error parsing premiereDate:' + item.StartDate + '; error: ' + err);
        return '';
    }
}

function groupTimers(items: TimerItem[], indexByDate: boolean): TimerGroup[] {
    const groups: TimerGroup[] = [];
    let currentGroupName = '';
    let currentGroup: TimerItem[] = [];

    for (const item of items) {
        const dateText = getTimerDateText(item, indexByDate);

        if (dateText != currentGroupName) {
            if (currentGroup.length) {
                groups.push({
                    name: currentGroupName,
                    items: currentGroup
                });
            }

            currentGroupName = dateText;
            currentGroup = [item];
        } else {
            currentGroup.push(item);
        }
    }

    if (currentGroup.length) {
        groups.push({
            name: currentGroupName,
            items: currentGroup
        });
    }
    return groups;
}

function renderTimerGroup(group: TimerGroup): string {
    let heading = '';
    if (group.name) {
        heading = '<div class="verticalSection"><h2 class="sectionTitle sectionTitle-cards padded-left">' + group.name + '</h2>';
    }
    let containerClass = 'itemsContainer vertical-wrap padded-left padded-right';
    if (enableScrollX()) {
        let scrollClass = 'scrollX hiddenScrollX';
        if (layoutManager.tv) {
            scrollClass += ' smoothScrollX';
        }

        containerClass = 'itemsContainer ' + scrollClass + ' padded-left padded-right';
    }

    return heading
        + '<div is="emby-itemscontainer" class="' + containerClass + '">'
        + cardBuilder.getCardsHtml({
            items: group.items,
            shape: getBackdropShape(enableScrollX()),
            showTitle: true,
            showParentTitleOrTitle: true,
            showAirTime: true,
            showAirEndTime: true,
            showChannelName: false,
            cardLayout: true,
            centerText: false,
            action: 'edit',
            cardFooterAside: 'none',
            preferThumb: true,
            defaultShape: null,
            coverImage: true,
            allowBottomPadding: false,
            overlayText: false,
            showChannelLogo: true
        })
        + '</div>'
        + (group.name ? '</div>' : '');
}

export function getTimersHtml(timers: TimerItem[], options: TimerOptions = {}): Promise<string> {
    const items = timers.map((timer) => ({ ...timer, Type: 'Timer' }));
    const groups = groupTimers(items, options.indexByDate !== false);
    return Promise.resolve(groups.map(renderTimerGroup).join(''));
}
