import escapeHtml from 'escape-html';

import { AppFeature } from 'constants/appFeature';
import { appHost } from '../apphost';
import dialogHelper from '../dialogHelper/dialogHelper';
import layoutManager from '../layoutManager';
import globalize from '../../lib/globalize';
import { ServerConnections } from 'lib/jellyfin-apiclient';
import * as userSettings from '../../scripts/settings/userSettings';
import loading from '../loading/loading';
import focusManager from '../focusManager';
import dom from '../../utils/dom';

import '../../elements/emby-select/emby-select';
import '../listview/listview.scss';
import '../../elements/emby-button/paper-icon-button-light';
import '../formdialog.scss';
import 'material-design-icons-iconfont';
import './subtitleeditor.scss';
import '../../elements/emby-button/emby-button';
import '../../styles/flexstyles.scss';
import toast from '../toast/toast';
import confirm from '../confirm/confirm';
import template from './subtitleeditor.template.html';

interface SubtitleStream {
    Type?: string;
    Index?: number;
    DisplayTitle?: string;
    Path?: string;
}

interface SubtitleEditorItem {
    Id: string;
    ServerId: string;
    Path?: string;
    MediaStreams?: SubtitleStream[];
}

interface SubtitleSearchResult {
    Id?: string;
    ProviderName?: string;
    Name?: string;
    Format?: string;
    DownloadCount?: number;
    FrameRate?: number;
    Comment?: string;
    IsHashMatch?: boolean;
    AiTranslated?: boolean;
    MachineTranslated?: boolean;
    Forced?: boolean;
    HearingImpaired?: boolean;
}

interface SubtitleLanguage {
    ThreeLetterISOLanguageName?: string;
    DisplayName?: string;
}

interface SubtitleEditorApiClient {
    ajax: (options: { type: string; url: string }) => Promise<unknown>;
    getUrl: (url: string) => string;
    getJSON: (url: string) => Promise<SubtitleSearchResult[]>;
    getItem: (userId: string, itemId: string) => Promise<SubtitleEditorItem>;
    getCurrentUserId: () => string;
    getCurrentUser: () => Promise<{ Configuration?: { SubtitleLanguagePreference?: string } }>;
    getCultures: () => Promise<SubtitleLanguage[]>;
}

let currentItem: SubtitleEditorItem;
let hasChanges: boolean;

function downloadRemoteSubtitles(context: Element, id: string): void {
    const url = 'Items/' + currentItem.Id + '/RemoteSearch/Subtitles/' + id;

    const apiClient = ServerConnections.getApiClient(currentItem.ServerId) as unknown as SubtitleEditorApiClient;
    void apiClient.ajax({

        type: 'POST',
        url: apiClient.getUrl(url)

    }).then(function () {
        hasChanges = true;

        toast(globalize.translate('MessageDownloadQueued'));

        focusManager.autoFocus(context);
    }).catch(() => toast(globalize.translate('ErrorDefault')));
}

function deleteLocalSubtitle(context: Element, index: string): void {
    const msg = globalize.translate('MessageAreYouSureDeleteSubtitles');

    void confirm({

        title: globalize.translate('ConfirmDeletion'),
        text: msg,
        confirmText: globalize.translate('Delete'),
        primary: 'delete'

    }).then(function () {
        loading.show();

        const itemId = currentItem.Id;
        const url = 'Videos/' + itemId + '/Subtitles/' + index;

        const apiClient = ServerConnections.getApiClient(currentItem.ServerId) as unknown as SubtitleEditorApiClient;

        apiClient.ajax({

            type: 'DELETE',
            url: apiClient.getUrl(url)

        }).then(function () {
            hasChanges = true;
            reload(context, apiClient, itemId);
        }, function () {
            toast(globalize.translate('ErrorDefault'));
        });
    }).catch(() => undefined);
}

function fillSubtitleList(context: Element, item: SubtitleEditorItem): void {
    const streams = item.MediaStreams || [];

    const subs = streams.filter(function (s: SubtitleStream) {
        return s.Type === 'Subtitle';
    });

    let html = '';

    if (subs.length) {
        html += '<h2>' + globalize.translate('MySubtitles') + '</h2>';

        html += '<div>';

        html += subs.map(function (s: SubtitleStream) {
            let itemHtml = '';

            const tagName = layoutManager.tv ? 'button' : 'div';
            let className = layoutManager.tv && s.Path ? 'listItem listItem-border btnDelete' : 'listItem listItem-border';

            if (layoutManager.tv) {
                className += ' listItem-focusscale listItem-button';
            }

            className += ' listItem-noborder';

            itemHtml += '<' + tagName + ' class="' + className + '" data-index="' + escapeHtml(String(s.Index ?? '')) + '">';

            itemHtml += '<span class="listItemIcon material-icons closed_caption" aria-hidden="true"></span>';

            itemHtml += '<div class="listItemBody two-line">';

            itemHtml += '<div>';
            itemHtml += escapeHtml(s.DisplayTitle || '');
            itemHtml += '</div>';

            if (s.Path) {
                itemHtml += '<div class="secondary listItemBodyText">' + escapeHtml(s.Path) + '</div>';
            }

            itemHtml += '</div>';

            if (!layoutManager.tv && s.Path) {
                itemHtml += '<button is="paper-icon-button-light" data-index="' + escapeHtml(String(s.Index ?? '')) + '" title="' + globalize.translate('Delete') + '" class="btnDelete listItemButton"><span class="material-icons delete" aria-hidden="true"></span></button>';
            }

            itemHtml += '</' + tagName + '>';

            return itemHtml;
        }).join('');

        html += '</div>';
    }

    const elem = context.querySelector('.subtitleList')!;

    if (subs.length) {
        elem.classList.remove('hide');
    } else {
        elem.classList.add('hide');
    }
    elem.innerHTML = html;
}

function fillLanguages(context: Element, apiClient: SubtitleEditorApiClient, languages: SubtitleLanguage[]): void {
    const selectLanguage = context.querySelector('#selectLanguage') as HTMLSelectElement;

    selectLanguage.innerHTML = languages.map(function (l: SubtitleLanguage) {
        return '<option value="' + escapeHtml(l.ThreeLetterISOLanguageName || '') + '">' + escapeHtml(l.DisplayName || '') + '</option>';
    }).join('');

    const lastLanguage = userSettings.get('subtitleeditor-language');
    if (lastLanguage) {
        selectLanguage.value = lastLanguage;
    } else {
        void apiClient.getCurrentUser().then(function (user) {
            const lang = user.Configuration?.SubtitleLanguagePreference;

            if (lang) {
                selectLanguage.value = lang;
            }
        }).catch(() => undefined);
    }
}

function renderSubtitleFlags(result: SubtitleSearchResult): string {
    const flags: Array<[boolean | undefined, string]> = [
        [result.IsHashMatch, 'PerfectMatch'],
        [result.AiTranslated, 'AiTranslated'],
        [result.MachineTranslated, 'MachineTranslated'],
        [result.Forced, 'ForeignPartsOnly'],
        [result.HearingImpaired, 'HearingImpairedShort']
    ];
    const spanOpen = '<span class="inline-flex align-items-center justify-content-center subtitleFeaturePillow">';
    const renderedFlags = flags
        .filter(([enabled]) => enabled)
        .map(([, label]) => spanOpen + globalize.translate(label) + '</span>')
        .join('');

    return renderedFlags ? '<div class="secondary listItemBodyText">' + renderedFlags + '</div>' : '';
}

function renderSubtitleResult(result: SubtitleSearchResult): string {
    const tagName = layoutManager.tv ? 'button' : 'div';
    let className = layoutManager.tv ? 'listItem listItem-border btnOptions' : 'listItem listItem-border';
    if (layoutManager.tv) {
        className += ' listItem-focusscale listItem-button';
    }

    const hasAnyFlags = result.IsHashMatch || result.AiTranslated || result.MachineTranslated || result.Forced || result.HearingImpaired;
    const bodyClass = result.Comment || hasAnyFlags ? 'three-line' : 'two-line';
    let html = '<' + tagName + ' class="' + className + '" data-subid="' + escapeHtml(String(result.Id ?? '')) + '">';

    html += '<span class="listItemIcon material-icons closed_caption" aria-hidden="true"></span>';
    html += '<div class="listItemBody ' + bodyClass + '">';
    html += '<div>' + escapeHtml(result.Name || '') + '</div>';
    html += '<div class="secondary listItemBodyText">';

    if (result.Format) {
        html += '<span style="margin-right:1em;">' + globalize.translate('FormatValue', result.Format) + '</span>';
    }
    if (result.DownloadCount != null) {
        html += '<span style="margin-right:1em;">' + globalize.translate('DownloadsValue', String(result.DownloadCount)) + '</span>';
    }
    if (result.FrameRate) {
        html += '<span>' + globalize.translate('Framerate') + ': ' + escapeHtml(String(result.FrameRate)) + '</span>';
    }

    html += '</div>';
    if (result.Comment) {
        html += '<div class="secondary listItemBodyText" style="white-space:pre-line;">' + escapeHtml(result.Comment) + '</div>';
    }
    html += renderSubtitleFlags(result);
    html += '</div>';

    if (!layoutManager.tv) {
        html += '<button type="button" is="paper-icon-button-light" data-subid="' + escapeHtml(String(result.Id ?? '')) + '" class="btnDownload listItemButton"><span class="material-icons file_download" aria-hidden="true"></span></button>';
    }

    return html + '</' + tagName + '>';
}

function renderSearchResults(context: Element, results: SubtitleSearchResult[]): void {
    let lastProvider = '';
    let html = '';

    if (!results.length) {
        context.querySelector('.noSearchResults')!.classList.remove('hide');
        (context.querySelector('.subtitleResults') as Element).innerHTML = '';
        loading.hide();
        return;
    }

    context.querySelector('.noSearchResults')!.classList.add('hide');

    for (let i = 0, length = results.length; i < length; i++) {
        const result = results[i];

        const provider = result.ProviderName || '';

        if (provider !== lastProvider) {
            if (i > 0) {
                html += '</div>';
            }
            html += '<h2>' + escapeHtml(provider || '') + '</h2>';
            html += '<div>';
            lastProvider = provider;
        }

        html += renderSubtitleResult(result);
    }

    if (results.length) {
        html += '</div>';
    }

    const elem = context.querySelector('.subtitleResults')!;
    elem.innerHTML = html;

    loading.hide();
}

function searchForSubtitles(context: Element, language: string): void {
    userSettings.set('subtitleeditor-language', language);

    loading.show();

    const apiClient = ServerConnections.getApiClient(currentItem.ServerId) as unknown as SubtitleEditorApiClient;
    const url = apiClient.getUrl('Items/' + currentItem.Id + '/RemoteSearch/Subtitles/' + language);

    void apiClient.getJSON(url).then(function (results: SubtitleSearchResult[]) {
        renderSearchResults(context, results);
    }).catch(() => {
        loading.hide();
        toast(globalize.translate('ErrorDefault'));
    });
}

function reload(context: Element, apiClient: SubtitleEditorApiClient, itemId: string | SubtitleEditorItem): void {
    context.querySelector('.noSearchResults')!.classList.add('hide');

    function onGetItem(item: SubtitleEditorItem): void {
        currentItem = item;

        fillSubtitleList(context, item);
        let file = item.Path || '';
        const index = Math.max(file.lastIndexOf('/'), file.lastIndexOf('\\'));
        if (index > -1) {
            file = file.substring(index + 1);
        }

        if (file) {
            (context.querySelector('.pathValue') as HTMLElement).innerText = file;
            context.querySelector('.originalFile')!.classList.remove('hide');
        } else {
            context.querySelector('.pathValue')!.innerHTML = '';
            context.querySelector('.originalFile')!.classList.add('hide');
        }

        loading.hide();
    }

    if (typeof itemId === 'string') {
        void apiClient.getItem(apiClient.getCurrentUserId(), itemId).then(onGetItem).catch(() => {
            loading.hide();
            toast(globalize.translate('ErrorDefault'));
        });
    } else {
        onGetItem(itemId);
    }
}

function onSearchSubmit(this: HTMLFormElement, e: Event): void {
    const lang = (this.querySelector('#selectLanguage') as HTMLSelectElement).value;

    searchForSubtitles(dom.parentWithClass(this, 'formDialogContent')!, lang);

    e.preventDefault();
    return undefined as unknown as void;
}

function onSubtitleListClick(e: Event): void {
    const btnDelete = dom.parentWithClass(e.target as HTMLElement, 'btnDelete');
    if (btnDelete) {
        const index = btnDelete.getAttribute('data-index')!;
        const context = dom.parentWithClass(btnDelete, 'subtitleEditorDialog')!;
        deleteLocalSubtitle(context, index);
    }
}

function onSubtitleResultsClick(e: Event): void {
    let subtitleId: string | null;
    let context: Element;

    const btnOptions = dom.parentWithClass(e.target as HTMLElement, 'btnOptions');
    if (btnOptions) {
        subtitleId = btnOptions.getAttribute('data-subid');
        context = dom.parentWithClass(btnOptions, 'subtitleEditorDialog')!;
        showDownloadOptions(btnOptions, context, subtitleId!);
    }

    const btnDownload = dom.parentWithClass(e.target as HTMLElement, 'btnDownload');
    if (btnDownload) {
        subtitleId = btnDownload.getAttribute('data-subid');
        context = dom.parentWithClass(btnDownload, 'subtitleEditorDialog')!;
        downloadRemoteSubtitles(context, subtitleId!);
    }
}

function showDownloadOptions(button: Element, context: Element, subtitleId: string): void {
    const items: Array<{ name: string; id: string }> = [];

    items.push({
        name: globalize.translate('Download'),
        id: 'download'
    });

    void import('../actionSheet/actionSheet').then((actionsheet) => {
        return actionsheet.show({
            items: items,
            positionTo: button

        });
    }).then((id: unknown) => {
        if (id === 'download') {
            downloadRemoteSubtitles(context, subtitleId);
        }
    }).catch(() => undefined);
}

function centerFocus(elem: Element | null, horiz: boolean, on: boolean): void {
    void import('../../scripts/scrollHelper').then(({ default: scrollHelper }) => {
        const fn = on ? 'on' : 'off';
        if (elem) {
            scrollHelper.centerFocus[fn](elem, horiz);
        }
    }).catch(() => undefined);
}

function onOpenUploadMenu(e: Event): void {
    const dialog = dom.parentWithClass(e.target as HTMLElement, 'subtitleEditorDialog');
    const selectLanguage = dialog!.querySelector('#selectLanguage') as HTMLSelectElement;
    const apiClient = ServerConnections.getApiClient(currentItem.ServerId) as unknown as SubtitleEditorApiClient;

    void import('../subtitleuploader/subtitleuploader').then(({ default: subtitleUploader }) => {
        return subtitleUploader.show({
            languages: {
                list: selectLanguage.innerHTML,
                value: selectLanguage.value
            },
            itemId: currentItem.Id,
            serverId: currentItem.ServerId
        });
    }).then(function (hasChanged: boolean) {
        if (hasChanged) {
            hasChanges = true;
            reload(dialog!, apiClient, currentItem.Id);
        }
    }).catch(() => undefined);
}

function showEditorInternal(itemId: string, serverId: string): Promise<void> {
    hasChanges = false;

    const apiClient = ServerConnections.getApiClient(serverId) as unknown as SubtitleEditorApiClient;
    return apiClient.getItem(apiClient.getCurrentUserId(), itemId).then(function (item: SubtitleEditorItem) {
        const dialogOptions: Record<string, boolean | string> = {
            removeOnClose: true,
            scrollY: false
        };

        if (layoutManager.tv) {
            dialogOptions.size = 'fullscreen';
        } else {
            dialogOptions.size = 'small';
        }

        const dlg = dialogHelper.createDialog(dialogOptions);

        dlg.classList.add('formDialog');
        dlg.classList.add('subtitleEditorDialog');

        dlg.innerHTML = globalize.translateHtml(template, 'core');

        dlg.querySelector('.originalSubtitleFileLabel')!.innerHTML = globalize.translate('File');

        dlg.querySelector('.subtitleSearchForm')!.addEventListener('submit', onSearchSubmit);

        dlg.querySelector('.btnOpenUploadMenu')!.addEventListener('click', onOpenUploadMenu);

        const btnSubmit = dlg.querySelector('.btnSubmit') as HTMLElement;

        if (layoutManager.tv) {
            centerFocus(dlg.querySelector('.formDialogContent'), false, true);
            dlg.querySelector('.btnSearchSubtitles')!.classList.add('hide');
        } else {
            btnSubmit.classList.add('hide');
        }

        if (layoutManager.tv || !appHost.supports(AppFeature.ExternalLinks)) {
            dlg.querySelector('.btnHelp')!.remove();
        }

        const editorContent = dlg.querySelector('.formDialogContent')!;

        dlg.querySelector('.subtitleList')!.addEventListener('click', onSubtitleListClick);
        dlg.querySelector('.subtitleResults')!.addEventListener('click', onSubtitleResultsClick);

        apiClient.getCultures().then(function (languages: SubtitleLanguage[]) {
            fillLanguages(editorContent, apiClient, languages);
        }).catch(() => toast(globalize.translate('ErrorDefault')));

        dlg.querySelector('.btnCancel')!.addEventListener('click', function () {
            dialogHelper.close(dlg);
        });

        return new Promise<void>(function (resolve, reject) {
            dlg.addEventListener('close', function () {
                if (layoutManager.tv) {
                    centerFocus(dlg.querySelector('.formDialogContent'), false, false);
                }

                if (hasChanges) {
                    resolve();
                } else {
                    reject();
                }
            });

            void dialogHelper.open(dlg);

            reload(editorContent, apiClient, item);
        });
    });
}

function showEditor(itemId: string, serverId: string): Promise<void> {
    loading.show();

    return showEditorInternal(itemId, serverId);
}

export default {
    show: showEditor
};
