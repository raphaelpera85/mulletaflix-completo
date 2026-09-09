import escapeHtml from 'escape-html';
import dom from '../../utils/dom';
import focusManager from '../focusManager';
import dialogHelper from '../dialogHelper/dialogHelper';
import inputManager from '../../scripts/inputManager';
import layoutManager from '../layoutManager';
import globalize from '../../lib/globalize';
import { ServerConnections } from 'lib/jellyfin-apiclient';
import * as userSettings from '../../scripts/settings/userSettings';
import '../../elements/emby-checkbox/emby-checkbox';
import '../../elements/emby-input/emby-input';
import '../../elements/emby-button/emby-button';
import '../../elements/emby-button/paper-icon-button-light';
import '../../elements/emby-select/emby-select';
import 'material-design-icons-iconfont';
import '../formdialog.scss';
import '../../styles/flexstyles.scss';
import template from './filtermenu.template.html';

type FilterItem = {
    Id?: string | null;
    Name?: string | null;
};
type FilterSettings = {
    IsPlayed: boolean;
    IsUnplayed: boolean;
    IsFavorite: boolean;
    IsResumable: boolean;
    Is4K: boolean;
    IsHD: boolean;
    IsSD: boolean;
    Is3D: boolean;
    VideoTypes: string;
    SeriesStatus: string;
    HasSubtitles: string;
    HasTrailer: string;
    HasSpecialFeature: string;
    HasThemeSong: string;
    HasThemeVideo: string;
    GenreIds: string;
};
type FilterOptions = {
    settingsKey: string;
    settings: FilterSettings;
    visibleSettings: string[];
    onChange?: () => void;
    parentId?: string;
    itemTypes: string[];
    serverId?: string;
    filterMenuOptions: Record<string, unknown>;
};
type FilterResult = {
    Genres?: FilterItem[];
};
type FilterApiClient = {
    getCurrentUserId: () => string;
    getFilters: (options: Record<string, unknown>) => Promise<FilterResult>;
};

function onSubmit(e: SubmitEvent): boolean {
    e.preventDefault();
    return false;
}

function renderOptions(context: HTMLElement, selector: string, cssClass: string, items: FilterItem[], isCheckedFn: (item: FilterItem) => boolean): void {
    const elem = context.querySelector<HTMLElement>(selector);
    const filterOptions = elem?.querySelector<HTMLElement>('.filterOptions');
    if (!elem || !filterOptions) {
        return;
    }

    if (items.length) {
        elem.classList.remove('hide');
    } else {
        elem.classList.add('hide');
    }

    let html = '';

    html += items.map(function (filter) {
        let itemHtml = '';

        const checkedHtml = isCheckedFn(filter) ? ' checked' : '';
        itemHtml += '<label>';
        itemHtml += '<input is="emby-checkbox" type="checkbox"' + checkedHtml + ' data-filter="' + escapeHtml(String(filter.Id ?? '')) + '" class="' + escapeHtml(cssClass) + '"/>';
        itemHtml += '<span>' + escapeHtml(filter.Name) + '</span>';
        itemHtml += '</label>';

        return itemHtml;
    }).join('');

    filterOptions.innerHTML = html;
}

function renderDynamicFilters(context: HTMLElement, result: FilterResult, options: FilterOptions): void {
    renderOptions(context, '.genreFilters', 'chkGenreFilter', result.Genres ?? [], function (i) {
        const delimeter = options.settings.GenreIds.indexOf('|') === -1 ? ',' : '|';
        return (delimeter + options.settings.GenreIds + delimeter).indexOf(delimeter + (i.Id || '') + delimeter) !== -1;
    });
}

function setBasicFilter(key: string, elem: HTMLInputElement): void {
    userSettings.setFilter(key, elem.checked ? 'true' : '');
}

function isSettingEnabled(settings: FilterSettings, key: string): boolean {
    const value = (settings as unknown as Record<string, unknown>)[key];
    return value === true || value === 'true';
}

function moveCheckboxFocus(elem: HTMLElement, offset: number): void {
    const parent = dom.parentWithClass(elem, 'checkboxList-verticalwrap');
    const elems = focusManager.getFocusableElements(parent);

    let index = -1;
    for (let i = 0, length = elems.length; i < length; i++) {
        if (elems[i] === elem) {
            index = i;
            break;
        }
    }

    index += offset;
    index = Math.min(elems.length - 1, index);
    index = Math.max(0, index);

    const newElem = elems[index];
    if (newElem) {
        focusManager.focus(newElem);
    }
}

function centerFocus(elem: Element, horiz: boolean, on: boolean): void {
    import('../../scripts/scrollHelper').then((scrollHelper) => {
        const fn = on ? 'on' : 'off';
        scrollHelper.centerFocus[fn](elem, horiz);
    }).catch(error => console.error('[FilterMenu] failed to center focus', error));
}

function onInputCommand(e: Event): void {
    const commandEvent = e as CustomEvent<{ command?: string }>;
    switch (commandEvent.detail?.command) {
        case 'left':
            moveCheckboxFocus(commandEvent.target as HTMLElement, -1);
            e.preventDefault();
            break;
        case 'right':
            moveCheckboxFocus(commandEvent.target as HTMLElement, 1);
            e.preventDefault();
            break;
        default:
            break;
    }
}

function saveValues(context: HTMLElement, settingsKey: string): void {
    context.querySelectorAll<HTMLElement>('.simpleFilter').forEach(elem => {
        const input = elem instanceof HTMLInputElement ? elem : elem.querySelector<HTMLInputElement>('input');
        const settingName = elem.getAttribute('data-settingname');
        if (input && settingName) {
            setBasicFilter(settingsKey + '-filter-' + settingName, input);
        }
    });

    const videoTypes: string[] = [];
    context.querySelectorAll<HTMLInputElement>('.chkVideoTypeFilter').forEach(elem => {
        if (elem.checked && elem.getAttribute('data-filter')) {
            videoTypes.push(elem.getAttribute('data-filter') as string);
        }
    });
    userSettings.setFilter(settingsKey + '-filter-VideoTypes', videoTypes.join(','));

    const seriesStatuses: string[] = [];
    context.querySelectorAll<HTMLInputElement>('.chkSeriesStatus').forEach(elem => {
        if (elem.checked && elem.getAttribute('data-filter')) {
            seriesStatuses.push(elem.getAttribute('data-filter') as string);
        }
    });
    userSettings.setFilter(`${settingsKey}-filter-SeriesStatus`, seriesStatuses.join(','));

    const genres: string[] = [];
    context.querySelectorAll<HTMLInputElement>('.chkGenreFilter').forEach(elem => {
        if (elem.checked && elem.getAttribute('data-filter')) {
            genres.push(elem.getAttribute('data-filter') as string);
        }
    });
    userSettings.setFilter(settingsKey + '-filter-GenreIds', genres.join(','));
}

function bindCheckboxInput(context: HTMLElement, on: boolean): void {
    const elems = context.querySelectorAll<HTMLElement>('.checkboxList-verticalwrap');
    for (let i = 0, length = elems.length; i < length; i++) {
        if (on) {
            inputManager.on(elems[i], onInputCommand);
        } else {
            inputManager.off(elems[i], onInputCommand);
        }
    }
}

function initEditor(context: HTMLElement, settings: FilterSettings): void {
    context.querySelector('form')?.addEventListener('submit', onSubmit);

    let elems = context.querySelectorAll<HTMLElement>('.simpleFilter');
    let i: number;
    let length: number;

    for (i = 0, length = elems.length; i < length; i++) {
        if (elems[i].tagName === 'INPUT') {
            const input = elems[i] as HTMLInputElement;
            input.checked = isSettingEnabled(settings, elems[i].getAttribute('data-settingname') || '');
        } else {
            const input = elems[i].querySelector<HTMLInputElement>('input');
            if (input) {
                input.checked = isSettingEnabled(settings, elems[i].getAttribute('data-settingname') || '');
            }
        }
    }

    const videoTypes = settings.VideoTypes ? settings.VideoTypes.split(',') : [];
    elems = context.querySelectorAll<HTMLElement>('.chkVideoTypeFilter');
    for (i = 0, length = elems.length; i < length; i++) {
        (elems[i] as HTMLInputElement).checked = videoTypes.indexOf(elems[i].getAttribute('data-filter') || '') !== -1;
    }

    const seriesStatuses = settings.SeriesStatus ? settings.SeriesStatus.split(',') : [];
    elems = context.querySelectorAll<HTMLElement>('.chkSeriesStatus');
    for (i = 0, length = elems.length; i < length; i++) {
        (elems[i] as HTMLInputElement).checked = seriesStatuses.indexOf(elems[i].getAttribute('data-filter') || '') !== -1;
    }

    if (context.querySelector('.basicFilterSection .viewSetting:not(.hide)')) {
        context.querySelector<HTMLElement>('.basicFilterSection')?.classList.remove('hide');
    } else {
        context.querySelector<HTMLElement>('.basicFilterSection')?.classList.add('hide');
    }

    if (context.querySelector('.featureSection .viewSetting:not(.hide)')) {
        context.querySelector<HTMLElement>('.featureSection')?.classList.remove('hide');
    } else {
        context.querySelector<HTMLElement>('.featureSection')?.classList.add('hide');
    }
}

function loadDynamicFilters(context: HTMLElement, options: FilterOptions): void {
    if (!options.serverId) {
        return;
    }
    const apiClient = ServerConnections.getApiClient(options.serverId) as unknown as FilterApiClient;

    const filterMenuOptions = Object.assign({}, options.filterMenuOptions, {
        UserId: apiClient.getCurrentUserId(),
        ParentId: options.parentId,
        IncludeItemTypes: options.itemTypes.join(',')
    });

    apiClient.getFilters(filterMenuOptions).then((result: FilterResult) => {
        renderDynamicFilters(context, result, options);
    }).catch((error: unknown) => console.error('[FilterMenu] failed to load dynamic filters', error));
}

class FilterMenu {
    show(options: FilterOptions): Promise<void> {
        return new Promise<void>((resolve) => {
            const dialogOptions: { removeOnClose: boolean; scrollY: boolean; size?: string } = {
                removeOnClose: true,
                scrollY: false
            };
            if (layoutManager.tv) {
                dialogOptions.size = 'fullscreen';
            } else {
                dialogOptions.size = 'small';
            }

            const dlg = dialogHelper.createDialog(dialogOptions) as HTMLElement;
            dlg.classList.add('formDialog');

            let html = '';
            html += '<div class="formDialogHeader">';
            html += `<button is="paper-icon-button-light" class="btnCancel hide-mouse-idle-tv" tabindex="-1" title="${globalize.translate('ButtonBack')}"><span class="material-icons arrow_back" aria-hidden="true"></span></button>`;
            html += '<h3 class="formDialogHeaderTitle">${Filters}</h3>';
            html += '</div>';
            html += template;

            dlg.innerHTML = globalize.translateHtml(html, 'core');

            const settingElements = dlg.querySelectorAll('.viewSetting');
            for (let i = 0, length = settingElements.length; i < length; i++) {
                if (options.visibleSettings.indexOf(settingElements[i].getAttribute('data-settingname') || '') === -1) {
                    settingElements[i].classList.add('hide');
                } else {
                    settingElements[i].classList.remove('hide');
                }
            }

            initEditor(dlg, options.settings);
            loadDynamicFilters(dlg, options);

            bindCheckboxInput(dlg, true);
            dlg.querySelector<HTMLElement>('.btnCancel')?.addEventListener('click', function () {
                dialogHelper.close(dlg);
            });

            if (layoutManager.tv) {
                const content = dlg.querySelector('.formDialogContent');
                if (content) {
                    centerFocus(content, false, true);
                }
            }

            let submitted: boolean | undefined;
            dlg.querySelector('form')?.addEventListener('change', function () {
                submitted = true;
            }, true);

            dialogHelper.open(dlg).then(function () {
                bindCheckboxInput(dlg, false);

                if (layoutManager.tv) {
                    const content = dlg.querySelector('.formDialogContent');
                    if (content) {
                        centerFocus(content, false, false);
                    }
                }

                if (submitted) {
                    saveValues(dlg, options.settingsKey);
                    return resolve();
                }
                return resolve();
            }).catch((error: unknown) => {
                bindCheckboxInput(dlg, false);
                console.error('[FilterMenu] failed to open dialog', error);
                resolve();
            });
        });
    }
}

export default FilterMenu;
