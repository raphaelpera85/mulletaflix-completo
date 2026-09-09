import escapeHtml from 'escape-html';

import { ItemAction } from 'constants/itemAction';
import { ServerConnections } from 'lib/jellyfin-apiclient';
import { OutboundWebSocketMessageType } from '@jellyfin/sdk/lib/websocket';

import inputManager from '../../scripts/inputManager';
import browser from '../../scripts/browser';
import globalize from '../../lib/globalize';
import Events from '../../utils/events.ts';
import scrollHelper from '../../scripts/scrollHelper';
import loading from '../loading/loading';
import datetime from '../../scripts/datetime';
import focusManager from '../focusManager';
import { playbackManager } from '../playback/playbackmanager';
import * as userSettings from '../../scripts/settings/userSettings';
import imageLoader from '../images/imageLoader';
import layoutManager from '../layoutManager';
import itemShortcuts from '../shortcuts';
import dom from '../../utils/dom';

import './guide.scss';
import './programs.scss';
import 'material-design-icons-iconfont';
import '../../styles/scrollstyles.scss';
import '../../elements/emby-programcell/emby-programcell';
import '../../elements/emby-button/emby-button';
import '../../elements/emby-button/paper-icon-button-light';
import '../../elements/emby-tabs/emby-tabs';
import '../../elements/emby-scroller/emby-scroller';
import '../../styles/flexstyles.scss';
import 'webcomponents.js/webcomponents-lite';

import template from './tvguide.template.html';

interface GuideSettingsInstance {
    categoryOptions: { categories: string[] };
    refresh: () => void;
}

export interface GuideOptions {
    element: HTMLElement;
    serverId: string;
}

export interface GuideInstance extends GuideSettingsInstance {
    options: GuideOptions;
    pause: () => void;
    resume: (refreshData?: unknown) => void;
    destroy: () => void;
    _wsUnsubscribers?: Array<() => void>;
}

interface ProgramCellElement extends HTMLElement {
    posLeft?: number;
    posWidth?: number;
    guideProgramName?: HTMLElement | null;
    caret?: HTMLElement | null;
}

type GuideIdentifier = string | number;

interface GuideTimerEvent {
    Id?: GuideIdentifier | null;
    ProgramId?: GuideIdentifier | null;
}

interface TimerIndicatorItem {
    Type?: string;
    TimerId?: GuideIdentifier | null;
    SeriesTimerId?: GuideIdentifier | null;
    Status?: string;
}

interface GuideProgramDates {
    StartDate: string;
    EndDate: string;
    StartDateLocal?: Date;
    EndDateLocal?: Date;
}

interface GuideSortableProgram {
    ChannelId?: string;
    StartDate: string;
}

interface GuideChannelIdentity {
    Id?: string;
}

interface GuideChannel extends GuideChannelIdentity {
    Id: string;
    Name?: string;
    ChannelName?: string;
    ChannelNumber?: string | number;
    IsFolder?: boolean;
    ServerId?: string;
    Type?: string;
    ImageTags: {
        Primary?: string;
    };
}

interface GuideProgram extends TimerIndicatorItem, GuideSortableProgram {
    Id: string;
    ChannelId: string;
    StartDate: string;
    EndDate: string;
    StartDateLocal: Date;
    EndDateLocal: Date;
    ServerId?: string;
    Name?: string;
    EpisodeTitle?: string;
    IsHD?: boolean;
    IsKids?: boolean;
    IsLive?: boolean;
    IsMovie?: boolean;
    IsNews?: boolean;
    IsPremiere?: boolean;
    IsRepeat?: boolean;
    IsSeries?: boolean;
    IsSports?: boolean;
}

interface GuideProgramOptions {
    showHdIcon: boolean;
    showLiveIndicator: boolean;
    showPremiereIndicator: boolean;
    showNewIndicator: boolean;
    showRepeatIndicator: boolean;
    showEpisodeTitle: boolean;
}

interface GuideProgramListInfo {
    startIndex: number;
}

interface GuideApiClient {
    getScaledImageUrl: (id: string, options: { maxHeight: number; tag: string; type: string }) => string;
    getCurrentUserId: () => string;
    getLiveTvGuideInfo: () => Promise<{ StartDate: string; EndDate: string }>;
    getLiveTvChannels: (query: Record<string, unknown>) => Promise<GuideChannelResult>;
    getLiveTvPrograms: (query: Record<string, unknown>) => Promise<GuideProgramResult>;
    subscribe?: (messages: OutboundWebSocketMessageType[], callback: (message: { Data: GuideTimerEvent }) => void) => (() => void) | undefined;
}

interface GuideChannelResult {
    TotalRecordCount: number;
    Items: GuideChannel[];
}

interface GuideProgramResult {
    Items: GuideProgram[];
}

type GuideFocusDirection = 'left' | 'right' | 'up' | 'down';

interface GuideCommandEvent extends Event {
    detail: {
        command: GuideFocusDirection;
    };
    target: HTMLElement;
}

interface GuideTabChangeEvent extends Event {
    target: HTMLElement;
    detail: {
        selectedTabIndex: string | number;
        previousIndex?: string | number | null;
    };
}

interface GuideScrollableElement extends HTMLElement {
    toCenter: (element: HTMLElement, immediate: boolean) => void;
}

function showViewSettings(instance: GuideSettingsInstance) {
    void import('./guide-settings')
        .then(({ default: guideSettingsDialog }) => guideSettingsDialog.show(instance.categoryOptions))
        .then(() => instance.refresh())
        .catch((error: unknown) => console.error('Failed to open TV guide settings', error));
}

function updateProgramCellOnScroll(cell: ProgramCellElement, scrollPct: number) {
    let left = cell.posLeft;
    if (!left) {
        left = parseFloat(cell.style.left.replace('%', ''));
        cell.posLeft = left;
    }
    let width = cell.posWidth;
    if (!width) {
        width = parseFloat(cell.style.width.replace('%', ''));
        cell.posWidth = width;
    }

    const right = left + width;
    const newPct = Math.max(Math.min(scrollPct, right), left);

    const offset = newPct - left;
    const pctOfWidth = (offset / width) * 100;

    let guideProgramName = cell.guideProgramName;
    if (!guideProgramName) {
        guideProgramName = cell.querySelector('.guideProgramName') as HTMLElement | null;
        cell.guideProgramName = guideProgramName;
    }

    let caret = cell.caret;
    if (!caret) {
        caret = cell.querySelector('.guide-programNameCaret') as HTMLElement | null;
        cell.caret = caret;
    }

    if (guideProgramName) {
        if (pctOfWidth > 0 && pctOfWidth <= 100) {
            guideProgramName.style.transform = 'translateX(' + pctOfWidth + '%)';
            caret?.classList.remove('hide');
        } else {
            guideProgramName.style.transform = 'none';
            caret?.classList.add('hide');
        }
    }
}

let isUpdatingProgramCellScroll = false;
function updateProgramCellsOnScroll(programGrid: HTMLElement, programCells: Iterable<ProgramCellElement>) {
    if (isUpdatingProgramCellScroll) {
        return;
    }

    isUpdatingProgramCellScroll = true;

    requestAnimationFrame(function () {
        const scrollLeft = programGrid.scrollLeft;

        const scrollPct = scrollLeft ? (scrollLeft / programGrid.scrollWidth) * 100 : 0;

        for (const programCell of programCells) {
            updateProgramCellOnScroll(programCell, scrollPct);
        }

        isUpdatingProgramCellScroll = false;
    });
}

function onProgramGridClick(e: Event) {
    if (!layoutManager.tv) {
        return;
    }

    const programCell = dom.parentWithClass(e.target as HTMLElement, 'programCell') as ProgramCellElement | null;
    if (programCell) {
        const startDate = datetime.parseISO8601Date(programCell.getAttribute('data-startdate') || '', true).getTime();
        const endDate = datetime.parseISO8601Date(programCell.getAttribute('data-enddate') || '', true).getTime();

        const now = new Date().getTime();
        if (now >= startDate && now < endDate) {
            const channelId = programCell.getAttribute('data-channelid');
            const serverId = programCell.getAttribute('data-serverid');

            if (!channelId) {
                return;
            }

            e.preventDefault();
            e.stopPropagation();

            playbackManager.play({
                ids: [channelId],
                serverId: serverId
            });
        }
    }
}

function Guide(this: GuideInstance, options: GuideOptions) {
    // The instance is intentionally captured because the legacy guide uses nested callbacks.
    // eslint-disable-next-line @typescript-eslint/no-this-alias
    const self = this;
    let items: Record<string, unknown> = {};

    self.options = options;
    self.categoryOptions = { categories: [] };

    // 30 mins
    const cellCurationMinutes = 30;
    const cellDurationMs = cellCurationMinutes * 60 * 1000;
    const msPerDay = 86400000;

    let currentDate: Date | null = null;
    let currentStartIndex = 0;
    let currentChannelLimit = 0;
    let autoRefreshInterval: ReturnType<typeof setInterval> | null = null;
    let programCells: NodeListOf<ProgramCellElement> | null = null;
    let lastFocusDirection: GuideFocusDirection | null = null;

    self.refresh = function () {
        currentDate = null;
        reloadPage(options.element);
        restartAutoRefresh();
    };

    self.pause = function () {
        stopAutoRefresh();
    };

    self.resume = function (refreshData?: unknown) {
        if (refreshData) {
            self.refresh();
        } else {
            restartAutoRefresh();
        }
    };

    self.destroy = function () {
        stopAutoRefresh();

        if (self._wsUnsubscribers) {
            self._wsUnsubscribers.forEach((unsub) => {
                unsub();
            });
            self._wsUnsubscribers = [];
        }

        setScrollEvents(options.element, false);
        itemShortcuts.off(options.element, {});
        items = {};
    };

    function restartAutoRefresh() {
        stopAutoRefresh();

        const intervalMs = 60000 * 15; // (minutes)

        autoRefreshInterval = setInterval(function () {
            self.refresh();
        }, intervalMs);
    }

    function stopAutoRefresh() {
        if (autoRefreshInterval) {
            clearInterval(autoRefreshInterval);
            autoRefreshInterval = null;
        }
    }

    function normalizeDateToTimeslot(date: Date): Date {
        const minutesOffset = date.getMinutes() - cellCurationMinutes;

        if (minutesOffset >= 0) {
            date.setHours(date.getHours(), cellCurationMinutes, 0, 0);
        } else {
            date.setHours(date.getHours(), 0, 0, 0);
        }

        return date;
    }

    function showLoading() {
        loading.show();
    }

    function hideLoading() {
        loading.hide();
    }

    function getGuideChannelQuery(apiClient: GuideApiClient): Record<string, unknown> & { StartIndex: number; Limit: number } {
        const categories = self.categoryOptions.categories || [];
        const enabled = (category: string) => !categories.length || categories.indexOf(category) !== -1;
        const query: Record<string, unknown> & { StartIndex: number; Limit: number } = {
            StartIndex: currentStartIndex,
            Limit: 500,
            UserId: apiClient.getCurrentUserId(),
            EnableFavoriteSorting: userSettings.get('livetv-favoritechannelsattop') !== 'false',
            AddCurrentProgram: false,
            EnableUserData: false,
            EnableImageTypes: 'Primary'
        };

        const allPrimaryCategories = ['movies', 'sports', 'news', 'kids'].every(enabled);
        if (allPrimaryCategories) {
            Object.assign(query, { IsMovie: null, IsSports: null, IsKids: null, IsNews: null, IsSeries: null });
        } else {
            const categoryFilters: Record<string, string> = {
                news: 'IsNews',
                sports: 'IsSports',
                kids: 'IsKids',
                movies: 'IsMovie',
                series: 'IsSeries'
            };
            for (const [category, filter] of Object.entries(categoryFilters)) {
                if (enabled(category)) {
                    query[filter] = true;
                }
            }
        }

        if (userSettings.get('livetv-channelorder') === 'DatePlayed') {
            query.SortBy = 'DatePlayed';
            query.SortOrder = 'Descending';
        } else {
            query.SortBy = null;
            query.SortOrder = null;
        }

        return query;
    }

    function getGuideRenderOptions(): GuideProgramOptions {
        const allowIndicators = dom.getWindowSize().innerWidth >= 600;
        return {
            showHdIcon: allowIndicators && userSettings.get('guide-indicator-hd') === 'true',
            showLiveIndicator: allowIndicators && userSettings.get('guide-indicator-live') !== 'false',
            showPremiereIndicator: allowIndicators && userSettings.get('guide-indicator-premiere') !== 'false',
            showNewIndicator: allowIndicators && userSettings.get('guide-indicator-new') !== 'false',
            showRepeatIndicator: allowIndicators && userSettings.get('guide-indicator-repeat') === 'true',
            showEpisodeTitle: !layoutManager.tv
        };
    }

    function updateGuidePagination(context: HTMLElement, channelQuery: Record<string, unknown> & { StartIndex: number; Limit: number }, totalRecordCount: number) {
        const btnPreviousPage = context.querySelector('.btnPreviousPage') as HTMLButtonElement;
        const btnNextPage = context.querySelector('.btnNextPage') as HTMLButtonElement;
        const guideOptions = context.querySelector('.guideOptions') as HTMLElement;
        const hasMultiplePages = totalRecordCount > channelQuery.Limit;

        guideOptions.classList.toggle('hide', !hasMultiplePages);
        if (!hasMultiplePages) {
            return;
        }

        btnPreviousPage.classList.remove('hide');
        btnNextPage.classList.remove('hide');
        btnPreviousPage.disabled = channelQuery.StartIndex === 0;
        btnNextPage.disabled = channelQuery.StartIndex + channelQuery.Limit >= totalRecordCount;
    }

    function getGuideProgramQuery(apiClient: GuideApiClient, date: Date, nextDay: Date, channelIds: string[], showHdIcon: boolean): Record<string, unknown> {
        const query: Record<string, unknown> = {
            UserId: apiClient.getCurrentUserId(),
            MaxStartDate: nextDay.toISOString(),
            MinEndDate: date.toISOString(),
            channelIds: channelIds.join(','),
            ImageTypeLimit: 1,
            EnableImages: false,
            SortBy: 'StartDate',
            EnableTotalRecordCount: false,
            EnableUserData: false
        };

        if (showHdIcon) {
            query.Fields = 'IsHD';
        }

        return query;
    }

    function reloadGuide(
        context: HTMLElement,
        newStartDate: Date,
        scrollToTimeMs: number,
        focusToTimeMs: number,
        startTimeOfDayMs: number,
        focusProgramOnRender: boolean
    ) {
        const apiClient = ServerConnections.getApiClient(options.serverId) as unknown as GuideApiClient;

        const channelLimit = 500;
        currentChannelLimit = channelLimit;
        showLoading();
        const channelQuery = getGuideChannelQuery(apiClient);

        let date = newStartDate;
        // Add one second to avoid getting programs that are just ending
        date = new Date(date.getTime() + 1000);

        // Subtract to avoid getting programs that are starting when the grid ends
        const nextDay = new Date(date.getTime() + msPerDay - 2000);

        // Normally we'd want to just let responsive css handle this,
        // but since mobile browsers are often underpowered,
        // it can help performance to get them out of the markup
        const renderOptions = getGuideRenderOptions();

        apiClient.getLiveTvChannels(channelQuery).then(function (channelsResult) {
            updateGuidePagination(context, channelQuery, channelsResult.TotalRecordCount);
            const programQuery = getGuideProgramQuery(apiClient, date, nextDay, channelsResult.Items.map(c => c.Id), renderOptions.showHdIcon);

            return apiClient.getLiveTvPrograms(programQuery).then(function (programsResult) {
                const focusOptions = { focusProgramOnRender, scrollToTimeMs, focusToTimeMs, startTimeOfDayMs };

                renderGuide(context, date, channelsResult.Items, programsResult.Items, renderOptions, focusOptions, apiClient);

                hideLoading();
            });
        }).catch((error: unknown) => {
            hideLoading();
            console.error('Failed to load TV guide data', error);
        });
    }

    function getDisplayTime(date: Date | string): string {
        if (typeof date === 'string') {
            try {
                return datetime.getDisplayTime(datetime.parseISO8601Date(date, true)).toLowerCase();
            } catch {
                return date;
            }
        }

        return datetime.getDisplayTime(date).toLowerCase();
    }

    function getTimeslotHeadersHtml(startDate: Date, endDateTime: Date): string {
        let html = '';

        // clone
        startDate = new Date(startDate.getTime());

        html += '<div class="timeslotHeadersInner">';

        while (startDate.getTime() < endDateTime.getTime()) {
            html += '<div class="timeslotHeader">';

            html += getDisplayTime(startDate);
            html += '</div>';

            // Add 30 mins
            startDate.setTime(startDate.getTime() + cellDurationMs);
        }

        return html;
    }

    function parseDates(program: GuideProgramDates): null {
        if (!program.StartDateLocal) {
            try {
                program.StartDateLocal = datetime.parseISO8601Date(program.StartDate, true);
            } catch (err) {
                console.error('error parsing timestamp for start date', err);
            }
        }

        if (!program.EndDateLocal) {
            try {
                program.EndDateLocal = datetime.parseISO8601Date(program.EndDate, true);
            } catch (err) {
                console.error('error parsing timestamp for end date', err);
            }
        }

        return null;
    }

    function getTimerIndicator(item: TimerIndicatorItem): string {
        let status;

        if (item.Type === 'SeriesTimer') {
            return '<span class="material-icons programIcon seriesTimerIcon fiber_smart_record" aria-hidden="true"></span>';
        } else if (item.TimerId || item.SeriesTimerId) {
            status = item.Status || 'Cancelled';
        } else if (item.Type === 'Timer') {
            status = item.Status;
        } else {
            return '';
        }

        if (item.SeriesTimerId) {
            if (status !== 'Cancelled') {
                return '<span class="material-icons programIcon seriesTimerIcon fiber_smart_record" aria-hidden="true"></span>';
            }

            return '<span class="material-icons programIcon seriesTimerIcon seriesTimerIcon-inactive fiber_smart_record" aria-hidden="true"></span>';
        }

        return '<span class="material-icons programIcon timerIcon fiber_manual_record" aria-hidden="true"></span>';
    }

    function getChannelProgramsHtml(
        context: HTMLElement,
        date: Date,
        channel: GuideChannel,
        programs: GuideProgram[],
        programOptions: GuideProgramOptions,
        listInfo: GuideProgramListInfo
    ): string {
        let html = '';

        const startMs = date.getTime();
        const endMs = startMs + msPerDay - 1;

        const outerCssClass = layoutManager.tv ? 'channelPrograms channelPrograms-tv' : 'channelPrograms';

        html += '<div class="' + escapeHtml(outerCssClass) + '" data-channelid="' + escapeHtml(String(channel.Id || '')) + '">';

        const clickAction = layoutManager.tv ? ItemAction.Link : ItemAction.ProgramDialog;

        const categories = self.categoryOptions.categories || [];
        const displayMovieContent = !categories.length || categories.indexOf('movies') !== -1;
        const displaySportsContent = !categories.length || categories.indexOf('sports') !== -1;
        const displayNewsContent = !categories.length || categories.indexOf('news') !== -1;
        const displayKidsContent = !categories.length || categories.indexOf('kids') !== -1;
        const displaySeriesContent = !categories.length || categories.indexOf('series') !== -1;
        const enableColorCodedBackgrounds = userSettings.get('guide-colorcodedbackgrounds') === 'true';

        let programsFound;
        const now = new Date().getTime();

        for (let i = listInfo.startIndex, length = programs.length; i < length; i++) {
            const program = programs[i];

            if (program.ChannelId !== channel.Id) {
                if (programsFound) {
                    break;
                }

                continue;
            }

            programsFound = true;
            listInfo.startIndex++;

            parseDates(program);

            const startDateLocalMs = program.StartDateLocal.getTime();
            const endDateLocalMs = program.EndDateLocal.getTime();

            if (endDateLocalMs < startMs) {
                continue;
            }

            if (startDateLocalMs > endMs) {
                break;
            }

            items[program.Id] = program;

            const renderStartMs = Math.max(startDateLocalMs, startMs);
            let startPercent = (startDateLocalMs - startMs) / msPerDay;
            startPercent *= 100;
            startPercent = Math.max(startPercent, 0);

            const renderEndMs = Math.min(endDateLocalMs, endMs);
            let endPercent = (renderEndMs - renderStartMs) / msPerDay;
            endPercent *= 100;

            let cssClass = 'programCell itemAction';
            let accentCssClass = null;
            let displayInnerContent = true;

            if (program.IsKids) {
                displayInnerContent = displayKidsContent;
                accentCssClass = 'kids';
            } else if (program.IsSports) {
                displayInnerContent = displaySportsContent;
                accentCssClass = 'sports';
            } else if (program.IsNews) {
                displayInnerContent = displayNewsContent;
                accentCssClass = 'news';
            } else if (program.IsMovie) {
                displayInnerContent = displayMovieContent;
                accentCssClass = 'movie';
            } else if (program.IsSeries) {
                displayInnerContent = displaySeriesContent;
            } else {
                displayInnerContent = displayMovieContent && displayNewsContent && displaySportsContent && displayKidsContent && displaySeriesContent;
            }

            if (displayInnerContent && enableColorCodedBackgrounds && accentCssClass) {
                cssClass += ' programCell-' + accentCssClass;
            }

            if (now >= startDateLocalMs && now < endDateLocalMs) {
                cssClass += ' programCell-active';
            }

            let timerAttributes = '';
            if (program.TimerId) {
                timerAttributes += ' data-timerid="' + escapeHtml(String(program.TimerId)) + '"';
            }
            if (program.SeriesTimerId) {
                timerAttributes += ' data-seriestimerid="' + escapeHtml(String(program.SeriesTimerId)) + '"';
            }

            const isAttribute = endPercent >= 2 ? ' is="emby-programcell"' : '';

            html += '<button' + isAttribute
                + ' data-action="' + escapeHtml(String(clickAction)) + '"'
                + timerAttributes
                + ' data-channelid="' + escapeHtml(String(program.ChannelId || '')) + '"'
                + ' data-id="' + escapeHtml(String(program.Id || '')) + '"'
                + ' data-serverid="' + escapeHtml(String(program.ServerId || '')) + '"'
                + ' data-startdate="' + escapeHtml(String(program.StartDate || '')) + '"'
                + ' data-enddate="' + escapeHtml(String(program.EndDate || '')) + '"'
                + ' data-type="' + escapeHtml(String(program.Type || '')) + '"'
                + ' class="' + escapeHtml(cssClass) + '"'
                + ' style="left:' + startPercent + '%;width:' + endPercent + '%;">';

            if (displayInnerContent) {
                const guideProgramNameClass = 'guideProgramName';

                html += '<div class="' + guideProgramNameClass + '">';

                html += '<div class="guide-programNameCaret hide"><span class="guideProgramNameCaretIcon material-icons keyboard_arrow_left" aria-hidden="true"></span></div>';

                html += '<div class="guideProgramNameText">' + escapeHtml(program.Name);

                let indicatorHtml = null;
                if (program.IsLive && programOptions.showLiveIndicator) {
                    indicatorHtml = '<span class="liveTvProgram guideProgramIndicator">' + globalize.translate('Live') + '</span>';
                } else if (program.IsPremiere && programOptions.showPremiereIndicator) {
                    indicatorHtml = '<span class="premiereTvProgram guideProgramIndicator">' + globalize.translate('Premiere') + '</span>';
                } else if (program.IsSeries && !program.IsRepeat && programOptions.showNewIndicator) {
                    indicatorHtml = '<span class="newTvProgram guideProgramIndicator">' + globalize.translate('New') + '</span>';
                } else if (program.IsSeries && program.IsRepeat && programOptions.showRepeatIndicator) {
                    indicatorHtml = '<span class="repeatTvProgram guideProgramIndicator">' + globalize.translate('Repeat') + '</span>';
                }
                html += indicatorHtml || '';

                if ((program.EpisodeTitle && programOptions.showEpisodeTitle)) {
                    html += '<div class="guideProgramSecondaryInfo">';

                    if (program.EpisodeTitle && programOptions.showEpisodeTitle) {
                        html += '<span class="programSecondaryTitle">' + escapeHtml(program.EpisodeTitle) + '</span>';
                    }
                    html += '</div>';
                }

                html += '</div>';

                if (program.IsHD && programOptions.showHdIcon) {
                    if (layoutManager.tv) {
                        html += '<div class="programIcon guide-programTextIcon guide-programTextIcon-tv">HD</div>';
                    } else {
                        html += '<div class="programIcon guide-programTextIcon">HD</div>';
                    }
                }

                html += getTimerIndicator(program);

                html += '</div>';
            }

            html += '</button>';
        }

        html += '</div>';

        return html;
    }

    function renderChannelHeaders(context: HTMLElement, channels: GuideChannel[], apiClient: GuideApiClient) {
        let html = '';

        for (const channel of channels) {
            const imageTag = channel.ImageTags.Primary;

            let cssClass = 'guide-channelHeaderCell itemAction';

            if (layoutManager.tv) {
                cssClass += ' guide-channelHeaderCell-tv';
            }

            const title = String(channel.Name || channel.ChannelName || channel.ChannelNumber || '').replace(/^\s*\d+\s+/, '').trim();

            html += `<button title="${escapeHtml(title)}" type="button" class="${escapeHtml(cssClass)}" data-action="${ItemAction.Link}" data-isfolder="${String(channel.IsFolder)}" data-id="${escapeHtml(String(channel.Id || ''))}" data-serverid="${escapeHtml(String(channel.ServerId || ''))}" data-type="${escapeHtml(String(channel.Type || ''))}">`;

            if (imageTag) {
                const url = apiClient.getScaledImageUrl(channel.Id, {
                    maxHeight: 220,
                    tag: imageTag,
                    type: 'Primary'
                });

                html += '<div class="guideChannelImage lazy" data-src="' + escapeHtml(url) + '"></div>';
            }

            const channelName = (channel.Name || channel.ChannelName || '').replace(/^\s*\d+\s+/, '').trim();

            if (channelName) {
                html += '<div class="guideChannelName">' + escapeHtml(channelName) + '</div>';
            } else if (channel.ChannelNumber) {
                html += '<h3 class="guideChannelNumber">' + escapeHtml(String(channel.ChannelNumber)) + '</h3>';
            }

            html += '</button>';
        }

        const channelList = context.querySelector('.channelsContainer') as HTMLElement;
        channelList.innerHTML = html;
        imageLoader.lazyChildren(channelList);
    }

    function renderPrograms(context: HTMLElement, date: Date, channels: GuideChannel[], programs: GuideProgram[], programOptions: GuideProgramOptions) {
        const listInfo: GuideProgramListInfo = {
            startIndex: 0
        };

        const html: string[] = [];

        for (const channel of channels) {
            html.push(getChannelProgramsHtml(context, date, channel, programs, programOptions, listInfo));
        }

        programGrid.innerHTML = html.join('');

        programCells = programGrid.querySelectorAll('[is=emby-programcell]') as NodeListOf<ProgramCellElement>;

        updateProgramCellsOnScroll(programGrid, programCells);
    }

    function getProgramSortOrder(program: GuideSortableProgram, channels: GuideChannelIdentity[]): number {
        const channelId = program.ChannelId;
        let channelIndex = -1;

        for (let i = 0, length = channels.length; i < length; i++) {
            if (channelId === channels[i].Id) {
                channelIndex = i;
                break;
            }
        }

        const start = datetime.parseISO8601Date(program.StartDate, true);

        return (channelIndex * 10000000) + (start.getTime() / 60000);
    }

    function renderGuide(
        context: HTMLElement,
        date: Date,
        channels: GuideChannel[],
        programs: GuideProgram[],
        renderOptions: GuideProgramOptions,
        guideOptions: { focusProgramOnRender: boolean; scrollToTimeMs: number; focusToTimeMs: number; startTimeOfDayMs: number },
        apiClient: GuideApiClient
    ) {
        programs.sort(function (a: GuideProgram, b: GuideProgram) {
            return getProgramSortOrder(a, channels) - getProgramSortOrder(b, channels);
        });

        const activeElement = document.activeElement;
        const itemId = activeElement?.getAttribute ? activeElement.getAttribute('data-id') : null;
        let channelRowId = null;

        if (activeElement) {
            channelRowId = dom.parentWithClass(activeElement as HTMLElement, 'channelPrograms');
            channelRowId = channelRowId?.getAttribute ? channelRowId.getAttribute('data-channelid') : null;
        }

        renderChannelHeaders(context, channels, apiClient);

        const startDate = date;
        const endDate = new Date(startDate.getTime() + msPerDay);
        (context.querySelector('.timeslotHeaders') as HTMLElement).innerHTML = getTimeslotHeadersHtml(startDate, endDate);
        items = {};
        renderPrograms(context, date, channels, programs, renderOptions);

        if (guideOptions.focusProgramOnRender) {
            focusProgram(context, itemId, channelRowId, guideOptions.focusToTimeMs, guideOptions.startTimeOfDayMs);
        }

        scrollProgramGridToTimeMs(context, guideOptions.scrollToTimeMs, guideOptions.startTimeOfDayMs);
    }

    function scrollProgramGridToTimeMs(_context: HTMLElement, scrollToTimeMs: number, startTimeOfDayMs: number) {
        scrollToTimeMs -= startTimeOfDayMs;

        const pct = scrollToTimeMs / msPerDay;

        programGrid.scrollTop = 0;

        const scrollPos = pct * programGrid.scrollWidth;

        nativeScrollTo(programGrid, scrollPos, true);
    }

    function findProgramCellForTime(parent: HTMLElement, pct: number): HTMLElement | null {
        let programCell = parent.querySelector('.programCell') as HTMLElement | null;

        while (programCell) {
            const left = parseFloat((programCell.style.left || '0').replace('%', '')) || 0;
            const width = parseFloat((programCell.style.width || '0').replace('%', '')) || 0;

            if (left >= pct || (left + width) >= pct) {
                return programCell;
            }

            programCell = programCell.nextElementSibling as HTMLElement | null;
        }

        return null;
    }

    function focusProgram(context: HTMLElement, itemId: GuideIdentifier | null, channelRowId: string | null, focusToTimeMs: number, startTimeOfDayMs: number) {
        let focusElem: HTMLElement | null = null;
        if (itemId) {
            focusElem = context.querySelector('[data-id="' + itemId + '"]') as HTMLElement | null;
        }

        if (focusElem) {
            focusManager.focus(focusElem as HTMLElement);
        } else {
            let autoFocusParent: HTMLElement = programGrid;

            if (channelRowId) {
                autoFocusParent = context.querySelector('[data-channelid="' + channelRowId + '"]') as HTMLElement || programGrid;
            }

            if (!autoFocusParent) {
                autoFocusParent = programGrid;
            }

            focusToTimeMs -= startTimeOfDayMs;

            const pct = (focusToTimeMs / msPerDay) * 100;

            const programCell = findProgramCellForTime(autoFocusParent, pct);

            if (programCell) {
                focusManager.focus(programCell as HTMLElement);
            } else {
                focusManager.autoFocus(autoFocusParent, true);
            }
        }
    }

    function nativeScrollTo(container: HTMLElement, pos: number, horizontal: boolean) {
        if (container.scrollTo) {
            if (horizontal) {
                container.scrollTo(pos, 0);
            } else {
                container.scrollTo(0, pos);
            }
        } else if (horizontal) {
            container.scrollLeft = Math.round(pos);
        } else {
            container.scrollTop = Math.round(pos);
        }
    }

    let lastGridScroll = 0;
    let lastHeaderScroll = 0;
    let scrollXPct = 0;
    function onProgramGridScroll(_context: HTMLElement, elem: HTMLElement, headers: HTMLElement) {
        if ((new Date().getTime() - lastHeaderScroll) >= 1000) {
            lastGridScroll = new Date().getTime();

            const scrollLeft = elem.scrollLeft;
            scrollXPct = (scrollLeft * 100) / elem.scrollWidth;
            nativeScrollTo(headers, scrollLeft, true);
        }

        if (programCells) {
            updateProgramCellsOnScroll(elem, programCells);
        }
    }

    function onTimeslotHeadersScroll(_context: HTMLElement, elem: HTMLElement) {
        if ((new Date().getTime() - lastGridScroll) >= 1000) {
            lastHeaderScroll = new Date().getTime();
            nativeScrollTo(programGrid, elem.scrollLeft, true);
        }
    }

    function changeDate(page: HTMLElement, date: Date, scrollToTimeMs: number, focusToTimeMs: number, startTimeOfDayMs: number, focusProgramOnRender: boolean) {
        const newStartDate = normalizeDateToTimeslot(date);
        currentDate = newStartDate;

        reloadGuide(page, newStartDate, scrollToTimeMs, focusToTimeMs, startTimeOfDayMs, focusProgramOnRender);
    }

    function getDateTabText(date: Date, isActive: boolean, tabIndex: number): string {
        const cssClass = isActive ? 'emby-tab-button guide-date-tab-button emby-tab-button-active' : 'emby-tab-button guide-date-tab-button';

        let html = '<button is="emby-button" class="' + cssClass + '" data-index="' + tabIndex + '" data-date="' + date.getTime() + '">';
        let tabText = datetime.toLocaleDateString(date, { weekday: 'short' });

        tabText += '<br/>';
        tabText += date.getDate();
        html += '<div class="emby-button-foreground">' + tabText + '</div>';
        html += '</button>';

        return html;
    }

    function setDateRange(page: HTMLElement, guideInfo: { StartDate: string; EndDate: string }) {
        const today = new Date();
        const nowHours = today.getHours();
        today.setHours(nowHours, 0, 0, 0);

        let start = datetime.parseISO8601Date(guideInfo.StartDate, true);
        const end = datetime.parseISO8601Date(guideInfo.EndDate, true);

        start.setHours(nowHours, 0, 0, 0);
        end.setHours(0, 0, 0, 0);

        if (start.getTime() >= end.getTime()) {
            end.setDate(start.getDate() + 1);
        }

        start = new Date(Math.max(today.getTime(), start.getTime()));

        let dateTabsHtml = '';
        let tabIndex = 0;

        // TODO: Use date-fns
        const date = new Date();

        if (currentDate) {
            date.setTime(currentDate.getTime());
        }

        date.setHours(nowHours, 0, 0, 0);

        let startTimeOfDayMs = (start.getHours() * 60 * 60 * 1000);
        startTimeOfDayMs += start.getMinutes() * 60 * 1000;

        while (start <= end) {
            const isActive = date.getDate() === start.getDate() && date.getMonth() === start.getMonth() && date.getFullYear() === start.getFullYear();

            dateTabsHtml += getDateTabText(start, isActive, tabIndex);

            start.setDate(start.getDate() + 1);
            start.setHours(0, 0, 0, 0);
            tabIndex++;
        }

        const tabsSlider = page.querySelector('.emby-tabs-slider') as HTMLElement;
        const dateTabs = page.querySelector('.guideDateTabs') as HTMLElement & { refresh: () => void };
        tabsSlider.innerHTML = dateTabsHtml;
        dateTabs.refresh();

        const newDate = new Date();
        const newDateHours = newDate.getHours();
        let scrollToTimeMs = newDateHours * 60 * 60 * 1000;

        const minutes = newDate.getMinutes();
        if (minutes >= 30) {
            scrollToTimeMs += 30 * 60 * 1000;
        }

        const focusToTimeMs = ((newDateHours * 60) + minutes) * 60 * 1000;
        changeDate(page, date, scrollToTimeMs, focusToTimeMs, startTimeOfDayMs, layoutManager.tv);
    }

    function reloadPage(page: HTMLElement) {
        showLoading();

        const apiClient = ServerConnections.getApiClient(options.serverId) as unknown as GuideApiClient;

        void apiClient.getLiveTvGuideInfo().then(function (guideInfo) {
            setDateRange(page, guideInfo);
        }).catch((error: unknown) => {
            hideLoading();
            console.error('Failed to load TV guide range', error);
        });
    }

    function getChannelProgramsFocusableElements(container: HTMLElement): HTMLElement[] {
        const elements = container.querySelectorAll('.programCell') as NodeListOf<HTMLElement>;

        const list = [];
        // add 1 to avoid programs that are out of view to the left
        const currentScrollXPct = scrollXPct + 1;

        for (const elem of elements) {
            const leftStyle = (elem.style.left || '').replace('%', '');
            const left = leftStyle ? parseFloat(leftStyle) : 0;

            const widthStyle = (elem.style.width || '').replace('%', '');
            const width = widthStyle ? parseFloat(widthStyle) : 0;

            if ((left + width) >= currentScrollXPct) {
                list.push(elem);
            }
        }

        return list;
    }

    function moveVertically(target: HTMLElement, programCell: HTMLElement | null, direction: 'up' | 'down'): boolean {
        let container: HTMLElement | null = null;
        let focusableElements: HTMLElement[] | undefined;

        if (programCell) {
            container = programGrid;
            const channelPrograms = dom.parentWithClass(programCell, 'channelPrograms');
            if (!channelPrograms) {
                return false;
            }

            const newRow = (direction === 'up' ? channelPrograms.previousSibling : channelPrograms.nextSibling) as HTMLElement | null;
            if (newRow) {
                focusableElements = getChannelProgramsFocusableElements(newRow);
                if (focusableElements.length) {
                    container = newRow;
                } else {
                    focusableElements = undefined;
                }
            } else {
                container = null;
            }
        }

        lastFocusDirection = direction;
        const focusOptions = { container, focusableElements };
        if (direction === 'up') {
            focusManager.moveUp(target, focusOptions);
        } else {
            focusManager.moveDown(target, focusOptions);
        }

        return true;
    }

    function moveHorizontally(target: HTMLElement, programCell: HTMLElement | null, direction: 'left' | 'right') {
        let container = programCell ? dom.parentWithClass(programCell, 'channelPrograms') : null;
        if (direction === 'left' && container && programCell && !programCell.previousSibling) {
            container = null;
        }

        lastFocusDirection = direction;
        if (direction === 'left') {
            focusManager.moveLeft(target, { container });
        } else {
            focusManager.moveRight(target, { container });
        }
    }

    function onInputCommand(event: Event) {
        const e = event as GuideCommandEvent;
        const target = e.target;
        const programCell = dom.parentWithClass(target, 'programCell') as HTMLElement | null;

        switch (e.detail.command) {
            case 'up':
                if (!moveVertically(target, programCell, 'up')) return;
                break;
            case 'down':
                if (!moveVertically(target, programCell, 'down')) return;
                break;
            case 'left':
                moveHorizontally(target, programCell, 'left');
                break;
            case 'right':
                moveHorizontally(target, programCell, 'right');
                break;
            default:
                return;
        }

        e.preventDefault();
        e.stopPropagation();
    }

    function centerFocusedElement(target: HTMLElement, programCell: Element | null) {
        if (lastFocusDirection === 'left' && programCell) {
            scrollHelper.toStart(programGrid, programCell as HTMLElement, true, true);
            return;
        }

        if (lastFocusDirection === 'right' && programCell) {
            scrollHelper.toCenter(programGrid, programCell as HTMLElement, true, true);
            return;
        }

        if (lastFocusDirection === 'up' || lastFocusDirection === 'down') {
            const verticalScroller = dom.parentWithClass(target, 'guideVerticalScroller') as GuideScrollableElement | null;
            const focusedElement = (programCell || dom.parentWithTag(target, 'BUTTON')) as HTMLElement | null;
            if (verticalScroller && focusedElement) {
                verticalScroller.toCenter(focusedElement, true);
            }
        }
    }

    function onScrollerFocus(event: Event) {
        const e = event as FocusEvent;
        const target = e.target;
        const targetElement = target as HTMLElement;
        const programCell = dom.parentWithClass(targetElement, 'programCell');

        if (programCell) {
            const focused = targetElement;

            const id = focused.getAttribute('data-id');
            const item = id ? items[id] : undefined;

            if (item) {
                Events.trigger(self, 'focus', [
                    {
                        item: item
                    }]);
            }
        }

        centerFocusedElement(targetElement, programCell);
    }

    function setScrollEvents(view: HTMLElement, enabled: boolean) {
        if (layoutManager.tv) {
            const guideVerticalScroller = view.querySelector('.guideVerticalScroller') as HTMLElement | null;

            if (!guideVerticalScroller) {
                return;
            }

            if (enabled) {
                inputManager.on(guideVerticalScroller, onInputCommand);
            } else {
                inputManager.off(guideVerticalScroller, onInputCommand);
            }
        }
    }

    function findProgramCellsByAttribute(attribute: string, value: unknown) {
        if (value == null) {
            return [] as Element[];
        }

        const expectedValue = String(value);
        const programCellNodes = options.element.querySelectorAll('.programCell') as NodeListOf<Element>;
        return Array.from(programCellNodes)
            .filter((cell) => cell.getAttribute(attribute) === expectedValue);
    }

    function onTimerCreated(data: GuideTimerEvent) {
        const programId = data?.ProgramId;
        // This could be null, not supported by all tv providers
        const newTimerId = data?.Id;

        // find guide cells by program id, ensure timer icon
        const cells = findProgramCellsByAttribute('data-id', programId);
        for (const cell of cells) {
            const icon = cell.querySelector('.timerIcon');
            if (!icon) {
                cell.querySelector('.guideProgramName')?.insertAdjacentHTML('beforeend', '<span class="timerIcon material-icons programIcon fiber_manual_record"></span>');
            }

            if (newTimerId) {
                cell.setAttribute('data-timerid', String(newTimerId));
            }
        }
    }

    function onTimerCancelled(data: GuideTimerEvent) {
        const id = data?.Id;
        // find guide cells by timer id, remove timer icon
        const cells = findProgramCellsByAttribute('data-timerid', id);

        for (const cell of cells) {
            const icon = cell.querySelector('.timerIcon');

            if (icon) {
                icon.parentNode?.removeChild(icon);
            }

            cell.removeAttribute('data-timerid');
        }
    }

    function onSeriesTimerCancelled(data: GuideTimerEvent) {
        const id = data?.Id;
        // find guide cells by timer id, remove timer icon
        const cells = findProgramCellsByAttribute('data-seriestimerid', id);

        for (const cell of cells) {
            const icon = cell.querySelector('.seriesTimerIcon');

            if (icon) {
                icon.parentNode?.removeChild(icon);
            }

            cell.removeAttribute('data-seriestimerid');
        }
    }

    const guideContext = options.element;

    guideContext.classList.add('tvguide');

    guideContext.innerHTML = globalize.translateHtml(template, 'core');

    const programGrid = guideContext.querySelector('.programGrid') as HTMLElement;
    const timeslotHeaders = guideContext.querySelector('.timeslotHeaders') as HTMLElement;

    if (layoutManager.tv) {
        const guideVerticalScroller = guideContext.querySelector('.guideVerticalScroller') as HTMLElement;
        dom.addEventListener(guideVerticalScroller, 'focus', onScrollerFocus, {
            capture: true,
            passive: true
        });
    } else if (layoutManager.desktop) {
        timeslotHeaders.classList.add('timeslotHeaders-desktop');
    }

    if (browser.iOS || browser.osx) {
        const channelsContainer = guideContext.querySelector('.channelsContainer') as HTMLElement;
        channelsContainer.classList.add('noRubberBanding');

        programGrid.classList.add('noRubberBanding');
    }

    dom.addEventListener(programGrid, 'scroll', function (this: HTMLElement) {
        onProgramGridScroll(guideContext, this, timeslotHeaders);
    }, {
        passive: true
    });

    dom.addEventListener(timeslotHeaders, 'scroll', function (this: HTMLElement) {
        onTimeslotHeadersScroll(guideContext, this);
    }, {
        passive: true
    });

    programGrid.addEventListener('click', onProgramGridClick);

    const nextPageButton = guideContext.querySelector('.btnNextPage') as HTMLElement;
    nextPageButton.addEventListener('click', function () {
        currentStartIndex += currentChannelLimit;
        reloadPage(guideContext);
        restartAutoRefresh();
    });

    const previousPageButton = guideContext.querySelector('.btnPreviousPage') as HTMLElement;
    previousPageButton.addEventListener('click', function () {
        currentStartIndex = Math.max(currentStartIndex - currentChannelLimit, 0);
        reloadPage(guideContext);
        restartAutoRefresh();
    });

    const guideViewSettingsButton = guideContext.querySelector('.btnGuideViewSettings') as HTMLElement;
    guideViewSettingsButton.addEventListener('click', function () {
        showViewSettings(self);
        restartAutoRefresh();
    });

    const guideDateTabs = guideContext.querySelector('.guideDateTabs') as HTMLElement;
    guideDateTabs.addEventListener('tabchange', function (event: Event) {
        const e = event as GuideTabChangeEvent;
        const allTabButtons = e.target.querySelectorAll('.guide-date-tab-button');

        const tabButton = allTabButtons[parseInt(String(e.detail.selectedTabIndex), 10)];
        if (tabButton) {
            const previousButton = e.detail.previousIndex == null ? null : allTabButtons[parseInt(String(e.detail.previousIndex), 10)];

            const date = new Date();
            date.setTime(parseInt(tabButton.getAttribute('data-date') || '0', 10));

            const scrollWidth = programGrid.scrollWidth;
            let scrollToTimeMs;
            if (scrollWidth) {
                scrollToTimeMs = (programGrid.scrollLeft / scrollWidth) * msPerDay;
            } else {
                scrollToTimeMs = 0;
            }

            if (previousButton) {
                const previousDate = new Date();
                previousDate.setTime(parseInt(previousButton.getAttribute('data-date') || '0', 10));

                scrollToTimeMs += (previousDate.getHours() * 60 * 60 * 1000);
                scrollToTimeMs += (previousDate.getMinutes() * 60 * 1000);
            }

            let startTimeOfDayMs = (date.getHours() * 60 * 60 * 1000);
            startTimeOfDayMs += (date.getMinutes() * 60 * 1000);

            changeDate(guideContext, date, scrollToTimeMs, scrollToTimeMs, startTimeOfDayMs, false);
        }
    });

    setScrollEvents(guideContext, true);
    itemShortcuts.on(guideContext, {});

    Events.trigger(self, 'load');

    const _guideApiClient = ServerConnections.getApiClient(options.serverId) as unknown as GuideApiClient;
    self._wsUnsubscribers = [
        _guideApiClient.subscribe?.([OutboundWebSocketMessageType.TimerCreated], ({ Data }) => onTimerCreated(Data)),
        _guideApiClient.subscribe?.([OutboundWebSocketMessageType.TimerCancelled], ({ Data }) => onTimerCancelled(Data)),
        _guideApiClient.subscribe?.([OutboundWebSocketMessageType.SeriesTimerCancelled], ({ Data }) => onSeriesTimerCancelled(Data))
    ].filter((unsubscribe): unsubscribe is () => void => Boolean(unsubscribe));

    self.refresh();
}

export function createGuide(options: GuideOptions): GuideInstance {
    const instance = Object.create(null) as GuideInstance;
    Guide.call(instance, options);
    return instance;
}

export default Guide;
