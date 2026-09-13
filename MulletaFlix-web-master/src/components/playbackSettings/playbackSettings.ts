/* eslint-disable @typescript-eslint/no-explicit-any, @typescript-eslint/no-this-alias, @stylistic/indent, @stylistic/padded-blocks */
import { MediaSegmentType } from '@jellyfin/sdk/lib/generated-client/models/media-segment-type';
import escapeHTML from 'escape-html';
import type { ApiClient } from 'jellyfin-apiclient';

import { MediaSegmentAction } from 'apps/stable/features/playback/constants/mediaSegmentAction';
import { getId, getMediaSegmentAction } from 'apps/stable/features/playback/utils/mediaSegmentSettings';
import { AppFeature } from 'constants/appFeature';
import { ServerConnections } from 'lib/jellyfin-apiclient';

import appSettings from '../../scripts/settings/appSettings';
import { UserSettings } from '../../scripts/settings/userSettings';
import { appHost } from '../apphost';
import browser from '../../scripts/browser';
import focusManager from '../focusManager';
import qualityoptions from '../qualityOptions';
import globalize from '../../lib/globalize';
import loading from '../loading/loading';
import Events from '../../utils/events.ts';
import toast from '../toast/toast';
import template from './playbackSettings.template.html';

import '../../elements/emby-select/emby-select';
import '../../elements/emby-checkbox/emby-checkbox';

type LanguageOption = {
    ThreeLetterISOLanguageName?: string | null;
    DisplayName?: string | null;
};

type QualityOption = {
    bitrate?: number;
    name?: string;
};

type PlaybackApiClient = ApiClient;

function getPlaybackApiClient(serverId: string): PlaybackApiClient {
    return ServerConnections.getApiClient(serverId) as unknown as PlaybackApiClient;
}

type PlaybackUser = {
    Id?: string;
    Policy: {
        EnableVideoPlaybackTranscoding: boolean;
        EnableAudioPlaybackTranscoding: boolean;
    };
    Configuration: {
        AudioLanguagePreference?: string;
        EnableNextEpisodeAutoPlay?: boolean;
        PlayDefaultAudioTrack?: boolean;
        RememberAudioSelections?: boolean;
        RememberSubtitleSelections?: boolean;
        CastReceiverId?: string;
    };
};

type PlaybackSystemInfo = {
    CastReceiverApplications: Array<{
        Id: string;
        Name: string;
    }>;
};

type ApiPlaybackUser = Awaited<ReturnType<PlaybackApiClient['getUser']>>;
type ApiPlaybackSystemInfo = Awaited<ReturnType<PlaybackApiClient['getSystemInfo']>>;

function normalizePlaybackUser(user: ApiPlaybackUser): PlaybackUser {
    return {
        Id: user.Id,
        Policy: {
            EnableVideoPlaybackTranscoding: user.Policy?.EnableVideoPlaybackTranscoding === true,
            EnableAudioPlaybackTranscoding: user.Policy?.EnableAudioPlaybackTranscoding === true
        },
        Configuration: {
            AudioLanguagePreference: user.Configuration?.AudioLanguagePreference ?? undefined,
            EnableNextEpisodeAutoPlay: user.Configuration?.EnableNextEpisodeAutoPlay === true,
            PlayDefaultAudioTrack: user.Configuration?.PlayDefaultAudioTrack === true,
            RememberAudioSelections: user.Configuration?.RememberAudioSelections === true,
            RememberSubtitleSelections: user.Configuration?.RememberSubtitleSelections === true,
            CastReceiverId: user.Configuration?.CastReceiverId ?? undefined
        }
    };
}

function normalizePlaybackSystemInfo(systemInfo: ApiPlaybackSystemInfo): PlaybackSystemInfo {
    return {
        CastReceiverApplications: (systemInfo.CastReceiverApplications ?? []).map(application => ({
            Id: application.Id || '',
            Name: application.Name || ''
        }))
    };
}

function fillSkipLengths(select: HTMLSelectElement): void {
    const options = [5, 10, 15, 20, 25, 30];

    select.innerHTML = options.map(option => {
        return {
            name: globalize.translate('ValueSeconds', String(option)),
            value: option * 1000
        };
    }).map(o => {
        return `<option value="${escapeHTML(String(o.value))}">${escapeHTML(o.name)}</option>`;
    }).join('');
}

function populateLanguages(select: HTMLSelectElement, languages: LanguageOption[]): void {
    let html = '';

    html += `<option value=''>${globalize.translate('AnyLanguage')}</option>`;
    html += `<option value='OriginalLanguage'>${globalize.translate('OriginalLanguage')}</option>`;

    for (let i = 0, length = languages.length; i < length; i++) {
        const culture = languages[i];
        html += `<option value='${escapeHTML(culture.ThreeLetterISOLanguageName || '')}'>${escapeHTML(culture.DisplayName || '')}</option>`;
    }

    select.innerHTML = html;
}

function populateMediaSegments(container: HTMLElement, userSettings: UserSettings): void {
    const selectedValues: Record<string, MediaSegmentAction> = {};
    const actionOptions = Object.values(MediaSegmentAction)
        .map(action => {
            const actionLabel = globalize.translate(`MediaSegmentAction.${action}`);
            return `<option value='${escapeHTML(action)}'>${escapeHTML(actionLabel)}</option>`;
        })
        .join('');

    const segmentSettings = [
        MediaSegmentType.Intro,
        MediaSegmentType.Preview,
        MediaSegmentType.Recap,
        MediaSegmentType.Commercial,
        MediaSegmentType.Outro
    ].map(segmentType => {
        const segmentTypeLabel = globalize.translate('LabelMediaSegmentsType', globalize.translate(`MediaSegmentType.${segmentType}`));
        const id = getId(segmentType);
        selectedValues[id] = getMediaSegmentAction(userSettings, segmentType);
        return `<div class="selectContainer">
<select is="emby-select" id="${escapeHTML(id)}" class="segmentTypeAction" label="${escapeHTML(segmentTypeLabel)}">
    ${actionOptions}
</select>
</div>`;
    }).join('');

    container.innerHTML = segmentSettings;

    Object.entries(selectedValues).forEach(([id, value]) => {
        const field = container.querySelector(`#${CSS.escape(id)}`) as HTMLSelectElement | null;
        if (field) field.value = value;
    });
}

function fillQuality(select: HTMLSelectElement, isInNetwork: boolean, mediatype: string): void {
    const options = mediatype === 'Audio' ? qualityoptions.getAudioQualityOptions({
        currentMaxBitrate: appSettings.maxStreamingBitrate(isInNetwork, mediatype),
        isAutomaticBitrateEnabled: appSettings.enableAutomaticBitrateDetection(isInNetwork, mediatype),
        enableAuto: true
    }) : qualityoptions.getVideoQualityOptions({
        currentMaxBitrate: appSettings.maxStreamingBitrate(isInNetwork, mediatype),
        isAutomaticBitrateEnabled: appSettings.enableAutomaticBitrateDetection(isInNetwork, mediatype),
        enableAuto: true
    } as any);

    select.innerHTML = options.map((i: QualityOption) => {
        return `<option value="${escapeHTML(String(i.bitrate || ''))}">${escapeHTML(String(i.name || ''))}</option>`;
    }).join('');
}

function setMaxBitrateIntoField(select: HTMLSelectElement, isInNetwork: boolean, mediatype: string): void {
    fillQuality(select, isInNetwork, mediatype);

    if (appSettings.enableAutomaticBitrateDetection(isInNetwork, mediatype)) {
        select.value = '';
    } else {
        select.value = String(appSettings.maxStreamingBitrate(isInNetwork, mediatype));
    }
}

function fillChromecastQuality(select: HTMLSelectElement): void {
    const options = qualityoptions.getVideoQualityOptions({
        currentMaxBitrate: appSettings.maxChromecastBitrate(),
        isAutomaticBitrateEnabled: !appSettings.maxChromecastBitrate(),
        enableAuto: true
    } as any);

    select.innerHTML = options.map((i: QualityOption) => {
        return `<option value="${escapeHTML(String(i.bitrate || ''))}">${escapeHTML(String(i.name || ''))}</option>`;
    }).join('');

    select.value = String(appSettings.maxChromecastBitrate() || '');
}

function setMaxBitrateFromField(select: HTMLSelectElement, isInNetwork: boolean, mediatype: string): void {
    if (select.value) {
        appSettings.maxStreamingBitrate(isInNetwork, mediatype, select.value);
        appSettings.enableAutomaticBitrateDetection(isInNetwork, mediatype, false);
    } else {
        appSettings.enableAutomaticBitrateDetection(isInNetwork, mediatype, true);
    }
}

function showHideQualityFields(context: any, user: PlaybackUser, apiClient: PlaybackApiClient): void {
    if (user.Policy.EnableVideoPlaybackTranscoding) {
        context.querySelector('.videoQualitySection').classList.remove('hide');
    } else {
        context.querySelector('.videoQualitySection').classList.add('hide');
    }

    if (appHost.supports(AppFeature.MultiServer)) {
        context.querySelector('.fldVideoInNetworkQuality').classList.remove('hide');
        context.querySelector('.fldVideoInternetQuality').classList.remove('hide');

        if (user.Policy.EnableAudioPlaybackTranscoding) {
            context.querySelector('.musicQualitySection').classList.remove('hide');
        } else {
            context.querySelector('.musicQualitySection').classList.add('hide');
        }

        return;
    }

    apiClient.getEndpointInfo().then((endpointInfo: any) => {
        if (endpointInfo.IsInNetwork) {
            context.querySelector('.fldVideoInNetworkQuality').classList.remove('hide');
            context.querySelector('.fldVideoInternetQuality').classList.add('hide');
            context.querySelector('.musicQualitySection').classList.add('hide');
        } else {
            context.querySelector('.fldVideoInNetworkQuality').classList.add('hide');
            context.querySelector('.fldVideoInternetQuality').classList.remove('hide');

            if (user.Policy.EnableAudioPlaybackTranscoding) {
                context.querySelector('.musicQualitySection').classList.remove('hide');
            } else {
                context.querySelector('.musicQualitySection').classList.add('hide');
            }
        }
    }).catch((error: unknown) => {
        console.error('[PlaybackSettings] failed to load endpoint information', error);
    });
}

function loadForm(context: any, user: PlaybackUser, userSettings: UserSettings, systemInfo: PlaybackSystemInfo, apiClient: PlaybackApiClient): void {
    const loggedInUserId = apiClient.getCurrentUserId();
    const userId = user.Id;

    showHideQualityFields(context, user, apiClient);

    if (browser.safari) {
        context.querySelector('.fldEnableHi10p').classList.remove('hide');
    }

    if (browser.web0s) {
        context.querySelector('.fldLimitSegmentLength').classList.remove('hide');
    }

    context.querySelector('#selectAllowedAudioChannels').value = userSettings.allowedAudioChannels();

    apiClient.getCultures().then((allCultures: LanguageOption[]) => {
        populateLanguages(context.querySelector('#selectAudioLanguage'), allCultures);
        context.querySelector('#selectAudioLanguage', context).value = user.Configuration.AudioLanguagePreference || '';
        context.querySelector('.chkEpisodeAutoPlay').checked = user.Configuration.EnableNextEpisodeAutoPlay || false;
    }).catch((error: unknown) => {
        console.error('[PlaybackSettings] failed to load cultures', error);
        toast(globalize.translate('ErrorDefault'));
    });

    if (appHost.supports(AppFeature.ExternalPlayerIntent) && userId === loggedInUserId) {
        context.querySelector('.fldExternalPlayer').classList.remove('hide');
    } else {
        context.querySelector('.fldExternalPlayer').classList.add('hide');
    }

    if (userId === loggedInUserId && (user.Policy.EnableVideoPlaybackTranscoding || user.Policy.EnableAudioPlaybackTranscoding)) {
        context.querySelector('.qualitySections').classList.remove('hide');

        if (appHost.supports(AppFeature.Chromecast) && user.Policy.EnableVideoPlaybackTranscoding) {
            context.querySelector('.fldChromecastQuality').classList.remove('hide');
        } else {
            context.querySelector('.fldChromecastQuality').classList.add('hide');
        }
    } else {
        context.querySelector('.qualitySections').classList.add('hide');
        context.querySelector('.fldChromecastQuality').classList.add('hide');
    }

    context.querySelector('.chkPlayDefaultAudioTrack').checked = user.Configuration.PlayDefaultAudioTrack || false;
    context.querySelector('.chkPreferFmp4HlsContainer').checked = userSettings.preferFmp4HlsContainer();
    context.querySelector('.chkLimitSegmentLength').checked = userSettings.limitSegmentLength();
    context.querySelector('.chkEnableDts').checked = appSettings.enableDts();
    context.querySelector('.chkEnableTrueHd').checked = appSettings.enableTrueHd();
    context.querySelector('.chkEnableHi10p').checked = appSettings.enableHi10p();
    context.querySelector('.chkEnableCinemaMode').checked = userSettings.enableCinemaMode();
    context.querySelector('#selectAudioNormalization').value = userSettings.selectAudioNormalization();
    context.querySelector('.chkEnableNextVideoOverlay').checked = userSettings.enableNextVideoInfoOverlay();
    context.querySelector('.chkRememberAudioSelections').checked = user.Configuration.RememberAudioSelections || false;
    context.querySelector('.chkRememberSubtitleSelections').checked = user.Configuration.RememberSubtitleSelections || false;
    context.querySelector('.chkExternalVideoPlayer').checked = appSettings.enableSystemExternalPlayers();
    context.querySelector('.chkLimitSupportedVideoResolution').checked = appSettings.limitSupportedVideoResolution();
    context.querySelector('#selectPreferredTranscodeVideoCodec').value = appSettings.preferredTranscodeVideoCodec();
    context.querySelector('#selectPreferredTranscodeVideoAudioCodec').value = appSettings.preferredTranscodeVideoAudioCodec();
    context.querySelector('.chkDisableVbrAudioEncoding').checked = appSettings.disableVbrAudio();
    context.querySelector('.chkAlwaysRemuxFlac').checked = appSettings.alwaysRemuxFlac();
    context.querySelector('.chkAlwaysRemuxMp3').checked = appSettings.alwaysRemuxMp3();

    setMaxBitrateIntoField(context.querySelector('.selectVideoInNetworkQuality'), true, 'Video');
    setMaxBitrateIntoField(context.querySelector('.selectVideoInternetQuality'), false, 'Video');
    setMaxBitrateIntoField(context.querySelector('.selectMusicInternetQuality'), false, 'Audio');

    fillChromecastQuality(context.querySelector('.selectChromecastVideoQuality'));

    const selectChromecastVersion = context.querySelector('.selectChromecastVersion');
    let ccAppsHtml = '';
    for (const app of systemInfo.CastReceiverApplications) {
        ccAppsHtml += `<option value='${escapeHTML(app.Id)}'>${escapeHTML(app.Name)}</option>`;
    }
    selectChromecastVersion.innerHTML = ccAppsHtml;
    selectChromecastVersion.value = user.Configuration.CastReceiverId;

    const selectMaxVideoWidth = context.querySelector('.selectMaxVideoWidth');
    selectMaxVideoWidth.value = appSettings.maxVideoWidth();

    const selectSkipForwardLength = context.querySelector('.selectSkipForwardLength');
    fillSkipLengths(selectSkipForwardLength);
    selectSkipForwardLength.value = userSettings.skipForwardLength();

    const selectSkipBackLength = context.querySelector('.selectSkipBackLength');
    fillSkipLengths(selectSkipBackLength);
    selectSkipBackLength.value = userSettings.skipBackLength();

    const mediaSegmentContainer = context.querySelector('.mediaSegmentActionContainer');
    populateMediaSegments(mediaSegmentContainer, userSettings);

}

function saveUser(context: any, user: PlaybackUser, userSettingsInstance: UserSettings, apiClient: PlaybackApiClient): Promise<unknown> {
    appSettings.enableSystemExternalPlayers(context.querySelector('.chkExternalVideoPlayer').checked);
    appSettings.maxChromecastBitrate(context.querySelector('.selectChromecastVideoQuality').value);
    appSettings.maxVideoWidth(context.querySelector('.selectMaxVideoWidth').value);
    appSettings.limitSupportedVideoResolution(context.querySelector('.chkLimitSupportedVideoResolution').checked);
    appSettings.preferredTranscodeVideoCodec(context.querySelector('#selectPreferredTranscodeVideoCodec').value);
    appSettings.preferredTranscodeVideoAudioCodec(context.querySelector('#selectPreferredTranscodeVideoAudioCodec').value);
    appSettings.enableDts(context.querySelector('.chkEnableDts').checked);
    appSettings.enableTrueHd(context.querySelector('.chkEnableTrueHd').checked);
    appSettings.enableHi10p(context.querySelector('.chkEnableHi10p').checked);
    appSettings.disableVbrAudio(context.querySelector('.chkDisableVbrAudioEncoding').checked);
    appSettings.alwaysRemuxFlac(context.querySelector('.chkAlwaysRemuxFlac').checked);
    appSettings.alwaysRemuxMp3(context.querySelector('.chkAlwaysRemuxMp3').checked);

    setMaxBitrateFromField(context.querySelector('.selectVideoInNetworkQuality'), true, 'Video');
    setMaxBitrateFromField(context.querySelector('.selectVideoInternetQuality'), false, 'Video');
    setMaxBitrateFromField(context.querySelector('.selectMusicInternetQuality'), false, 'Audio');

    userSettingsInstance.allowedAudioChannels(context.querySelector('#selectAllowedAudioChannels').value);
    user.Configuration.AudioLanguagePreference = context.querySelector('#selectAudioLanguage').value;
    user.Configuration.PlayDefaultAudioTrack = context.querySelector('.chkPlayDefaultAudioTrack').checked;
    user.Configuration.EnableNextEpisodeAutoPlay = context.querySelector('.chkEpisodeAutoPlay').checked;
    userSettingsInstance.preferFmp4HlsContainer(context.querySelector('.chkPreferFmp4HlsContainer').checked);
    userSettingsInstance.limitSegmentLength(context.querySelector('.chkLimitSegmentLength').checked);
    userSettingsInstance.enableCinemaMode(context.querySelector('.chkEnableCinemaMode').checked);
    userSettingsInstance.selectAudioNormalization(context.querySelector('#selectAudioNormalization').value);
    userSettingsInstance.enableNextVideoInfoOverlay(context.querySelector('.chkEnableNextVideoOverlay').checked);
    user.Configuration.RememberAudioSelections = context.querySelector('.chkRememberAudioSelections').checked;
    user.Configuration.RememberSubtitleSelections = context.querySelector('.chkRememberSubtitleSelections').checked;
    user.Configuration.CastReceiverId = context.querySelector('.selectChromecastVersion').value;
    userSettingsInstance.skipForwardLength(context.querySelector('.selectSkipForwardLength').value);
    userSettingsInstance.skipBackLength(context.querySelector('.selectSkipBackLength').value);

    const segmentTypeActions = context.querySelectorAll('.segmentTypeAction') || [];
    Array.prototype.forEach.call(segmentTypeActions, (actionEl: any) => {
        userSettingsInstance.set(actionEl.id, actionEl.value, false);
    });

    return apiClient.updateUserConfiguration(user.Id || '', user.Configuration);
}

async function save(instance: any, context: any, userId: string, userSettings: UserSettings, apiClient: PlaybackApiClient, enableSaveConfirmation: boolean): Promise<void> {
    const user = await apiClient.getUser(userId);
    await saveUser(context, normalizePlaybackUser(user), userSettings, apiClient);
        if (enableSaveConfirmation) {
            toast(globalize.translate('SettingsSaved'));
        }

        Events.trigger(instance, 'saved');
}

function onSubmit(this: any, e: any): boolean {
    const self: any = this;
    const apiClient = getPlaybackApiClient(self.options.serverId);
    const userId = self.options.userId;
    const userSettings = self.options.userSettings;

    void loading.withLoading(async () => {
        await userSettings.setUserInfo(userId, apiClient);
        const enableSaveConfirmation = self.options.enableSaveConfirmation;
        await save(self, self.options.element, userId, userSettings, apiClient, enableSaveConfirmation);
    }).catch(() => {
        toast(globalize.translate('ErrorDefault'));
    });

    if (e) {
        e.preventDefault();
    }
    return false;
}

function embed(options: any, self: any): void {
    options.element.innerHTML = globalize.translateHtml(template, 'core');
    options.element.querySelector('form').addEventListener('submit', onSubmit.bind(self));

    if (options.enableSaveButton) {
        options.element.querySelector('.btnSave').classList.remove('hide');
    }

    self.loadData();

    if (options.autoFocus) {
        focusManager.autoFocus(options.element);
    }
}

class PlaybackSettings {
    private options: any;

    private dataLoaded: boolean;

    constructor(options: any) {
        this.options = options;
        this.dataLoaded = false;
        embed(options, this);
    }

    loadData(): void {
        const self = this;
        const context = self.options.element;

        const userId = self.options.userId;
        const apiClient = getPlaybackApiClient(self.options.serverId);
        const userSettings = self.options.userSettings;

        void loading.withLoading(async () => {
            const user = normalizePlaybackUser(await apiClient.getUser(userId));
            const systemInfo = normalizePlaybackSystemInfo(await apiClient.getSystemInfo());
            await userSettings.setUserInfo(userId, apiClient);
            self.dataLoaded = true;
            loadForm(context, user, userSettings, systemInfo, apiClient);
        }).catch(() => {
            toast(globalize.translate('ErrorDefault'));
        });
    }

    submit(): void {
        onSubmit.call(this, undefined);
    }

    destroy(): void {
        this.options = null;
    }
}

export default PlaybackSettings;

/* eslint-enable @typescript-eslint/no-explicit-any, @typescript-eslint/no-this-alias, @stylistic/indent, @stylistic/padded-blocks */
