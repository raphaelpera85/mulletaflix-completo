import globalize from 'lib/globalize';
import loading from 'components/loading/loading';
import dom from 'utils/dom';
import 'elements/emby-input/emby-input';
import 'elements/emby-button/emby-button';
import 'elements/emby-checkbox/emby-checkbox';
import 'elements/emby-select/emby-select';
import Dashboard from 'utils/dashboard';
import { getParameterByName } from 'utils/url';
import escapeHtml from 'escape-html';

declare const ApiClient: {
    getJSON(url: string): Promise<Array<{ Id: string; Name: string }>>;
    getUrl(path: string): string;
    getNamedConfiguration(name: string): Promise<{ TunerHosts: TunerHostInfo[] }>;
    ajax(options: {
        type: string;
        url: string;
        data: string;
        contentType: string;
    }): Promise<void>;
    serverId(): string;
};

interface PageElements extends HTMLElement {
    querySelector<T extends Element>(selectors: string): T | null;
}

interface LabeledInputElement extends HTMLInputElement {
    label(text: string): void;
}

interface TunerHostInfo {
    Id?: string;
    Type?: string;
    Source?: string;
    Url?: string | null;
    FriendlyName?: string | null;
    UserAgent?: string | null;
    DeviceId?: string | null;
    TunerCount?: number | string | null;
    FallbackMaxStreamingBitrate?: number;
    ImportFavoritesOnly?: boolean;
    AllowHWTranscoding?: boolean;
    AllowFmp4TranscodingContainer?: boolean;
    AllowStreamSharing?: boolean;
    EnableStreamLooping?: boolean;
    IgnoreDts?: boolean;
    ReadAtNativeFramerate?: boolean;
}

function isM3uVariant(type?: string): boolean {
    return ['nextpvr'].includes(type || '');
}

function fillTypes(view: PageElements, currentId?: string): Promise<void> {
    return ApiClient.getJSON(ApiClient.getUrl('LiveTv/TunerHosts/Types')).then((types) => {
        const selectType = view.querySelector<HTMLSelectElement>('.selectType');
        if (!selectType) {
            return;
        }

        let html = '';
        html += types.map((tuner) => {
            return '<option value="' + escapeHtml(tuner.Id) + '">' + escapeHtml(tuner.Name) + '</option>';
        }).join('');
        html += '<option value="other">';
        html += globalize.translate('TabOther');
        html += '</option>';
        selectType.innerHTML = html;
        selectType.disabled = currentId != null;
        selectType.value = '';
        onTypeChange.call(selectType);
    });
}

async function reload(view: PageElements, providerId?: string): Promise<void> {
    const devicePath = view.querySelector<HTMLInputElement>('.txtDevicePath');
    const favorite = view.querySelector<HTMLInputElement>('.chkFavorite');
    if (devicePath) {
        devicePath.value = '';
    }
    if (favorite) {
        favorite.checked = false;
    }
    if (providerId) {
        const config = await ApiClient.getNamedConfiguration('livetv');
        const info = config.TunerHosts.find((item) => item.Id === providerId);
        if (info) {
            fillTunerHostInfo(view, info);
        }
    }
}

function fillTunerHostInfo(view: PageElements, info: TunerHostInfo): void {
    const selectType = view.querySelector<HTMLSelectElement>('.selectType');
    let type = info.Type || '';

    if (info.Source && isM3uVariant(info.Source)) {
        type = info.Source;
    }

    if (selectType) {
        selectType.value = type;
        onTypeChange.call(selectType);
    }

    (view.querySelector('.txtDevicePath') as HTMLInputElement).value = info.Url || '';
    (view.querySelector('.txtFriendlyName') as HTMLInputElement).value = info.FriendlyName || '';
    (view.querySelector('.txtUserAgent') as HTMLInputElement).value = info.UserAgent || '';
    (view.querySelector('.fldDeviceId') as HTMLInputElement).value = info.DeviceId || '';
    (view.querySelector('.chkFavorite') as HTMLInputElement).checked = !!info.ImportFavoritesOnly;
    (view.querySelector('.chkTranscode') as HTMLInputElement).checked = !!info.AllowHWTranscoding;
    (view.querySelector('.chkStreamLoop') as HTMLInputElement).checked = !!info.EnableStreamLooping;
    (view.querySelector('.chkFmp4Container') as HTMLInputElement).checked = !!info.AllowFmp4TranscodingContainer;
    (view.querySelector('.chkStreamSharing') as HTMLInputElement).checked = !!info.AllowStreamSharing;
    (view.querySelector('.chkIgnoreDts') as HTMLInputElement).checked = !!info.IgnoreDts;
    (view.querySelector('.chkReadInputAtNativeFramerate') as HTMLInputElement).checked = !!info.ReadAtNativeFramerate;
    (view.querySelector('.txtFallbackMaxStreamingBitrate') as HTMLInputElement).value = String(info.FallbackMaxStreamingBitrate ? info.FallbackMaxStreamingBitrate / 1e6 : '30');
    (view.querySelector('.txtTunerCount') as HTMLInputElement).value = String(info.TunerCount || '0');
}

function submitForm(page: PageElements): void {
    const type = (page.querySelector('.selectType') as HTMLSelectElement).value;
    const info: TunerHostInfo & { Id?: string; Source?: string } = {
        Type: type,
        Url: (page.querySelector('.txtDevicePath') as HTMLInputElement).value || null,
        UserAgent: (page.querySelector('.txtUserAgent') as HTMLInputElement).value || null,
        FriendlyName: (page.querySelector('.txtFriendlyName') as HTMLInputElement).value || null,
        DeviceId: (page.querySelector('.fldDeviceId') as HTMLInputElement).value || null,
        TunerCount: (page.querySelector('.txtTunerCount') as HTMLInputElement).value || 0,
        FallbackMaxStreamingBitrate: parseInt(String(1e6 * parseFloat((page.querySelector('.txtFallbackMaxStreamingBitrate') as HTMLInputElement).value || '30')), 10),
        ImportFavoritesOnly: (page.querySelector('.chkFavorite') as HTMLInputElement).checked,
        AllowHWTranscoding: (page.querySelector('.chkTranscode') as HTMLInputElement).checked,
        AllowFmp4TranscodingContainer: (page.querySelector('.chkFmp4Container') as HTMLInputElement).checked,
        AllowStreamSharing: (page.querySelector('.chkStreamSharing') as HTMLInputElement).checked,
        EnableStreamLooping: (page.querySelector('.chkStreamLoop') as HTMLInputElement).checked,
        IgnoreDts: (page.querySelector('.chkIgnoreDts') as HTMLInputElement).checked,
        ReadAtNativeFramerate: (page.querySelector('.chkReadInputAtNativeFramerate') as HTMLInputElement).checked
    };

    if (isM3uVariant(info.Type)) {
        info.Source = info.Type;
        info.Type = 'm3u';
    }

    const id = getParameterByName('id');
    if (id) {
        info.Id = id;
    }

    void loading.withLoading(() => ApiClient.ajax({
        type: 'POST',
        url: ApiClient.getUrl('LiveTv/TunerHosts'),
        data: JSON.stringify(info),
        contentType: 'application/json'
    })).then(() => {
        Dashboard.processServerConfigurationUpdateResult();
        void Dashboard.navigate('dashboard/livetv');
    }).catch(() => {
        Dashboard.alert({
            message: globalize.translate('ErrorSavingTvProvider')
        });
    });
}

function getDetectedDevice(): Promise<TunerHostInfo> {
    return import('components/tunerPicker').then(({ default: TunerPicker }) => {
        return new TunerPicker().show();
    });
}

function setFieldVisibility(view: PageElements, selector: string, visible: boolean): void {
    view.querySelector(selector)?.classList.toggle('hide', !visible);
}

function onTypeChange(this: HTMLSelectElement): void {
    const value = this.value;
    const view = dom.parentWithClass(this, 'page') as PageElements;
    const mayIncludeUnsupportedDrmChannels = value === 'hdhomerun';
    const supportsTranscoding = value === 'hdhomerun';
    const supportsFavorites = value === 'hdhomerun';
    const supportsTunerIpAddress = value === 'hdhomerun';
    const supportsTunerFileOrUrl = value === 'm3u';
    const supportsStreamLooping = value === 'm3u';
    const supportsIgnoreDts = value === 'm3u';
    const supportsReadInputAtNativeFramerate = value === 'm3u';
    const supportsTunerCount = value === 'm3u';
    const supportsUserAgent = value === 'm3u';
    const supportsFmp4Container = value === 'm3u';
    const supportsStreamSharing = value === 'm3u';
    const supportsFallbackBitrate = value === 'm3u' || value === 'hdhomerun';
    const suppportsSubmit = value !== 'other';
    const supportsSelectablePath = supportsTunerFileOrUrl;
    const txtDevicePath = view.querySelector('.txtDevicePath') as LabeledInputElement;

    let pathLabel: string | undefined;
    if (supportsTunerIpAddress) {
        pathLabel = 'LabelTunerIpAddress';
    } else if (supportsTunerFileOrUrl) {
        pathLabel = 'LabelFileOrUrl';
    }
    if (pathLabel) {
        txtDevicePath.label(globalize.translate(pathLabel));
    }
    setFieldVisibility(view, '.fldPath', pathLabel !== undefined);
    setFieldVisibility(view, '.btnSelectPath', supportsSelectablePath);
    txtDevicePath.toggleAttribute('required', supportsSelectablePath);
    setFieldVisibility(view, '.fldUserAgent', supportsUserAgent);
    setFieldVisibility(view, '.fldFavorites', supportsFavorites);
    setFieldVisibility(view, '.fldTranscode', supportsTranscoding);
    setFieldVisibility(view, '.fldFmp4Container', supportsFmp4Container);
    setFieldVisibility(view, '.fldStreamSharing', supportsStreamSharing);
    setFieldVisibility(view, '.fldFallbackMaxStreamingBitrate', supportsFallbackBitrate);
    setFieldVisibility(view, '.fldStreamLoop', supportsStreamLooping);
    setFieldVisibility(view, '.fldIgnoreDts', supportsIgnoreDts);
    setFieldVisibility(view, '.fldReadInputAtNativeFramerate', supportsReadInputAtNativeFramerate);
    setFieldVisibility(view, '.fldTunerCount', supportsTunerCount);
    (view.querySelector('.txtTunerCount') as HTMLInputElement)?.toggleAttribute('required', supportsTunerCount);
    setFieldVisibility(view, '.drmMessage', mayIncludeUnsupportedDrmChannels);
    setFieldVisibility(view, '.button-submit', suppportsSubmit);
}

export default function (view: PageElements, params: { id?: string }): void {
    if (!params.id) {
        view.querySelector('.btnDetect')?.classList.remove('hide');
    }

    view.addEventListener('viewshow', () => {
        const currentId = params.id;
        void loading.withLoading(async () => {
            await fillTypes(view, currentId);
            await reload(view, currentId);
        })
            .catch((error: unknown) => {
                console.error('Failed to load Live TV tuner types', error);
                Dashboard.alert({ message: globalize.translate('ErrorDefault') });
            });
    });
    (view.querySelector('form') as HTMLFormElement)?.addEventListener('submit', (e) => {
        submitForm(view);
        e.preventDefault();
        e.stopPropagation();
        return false;
    });
    (view.querySelector('.selectType') as HTMLSelectElement)?.addEventListener('change', onTypeChange);
    (view.querySelector('.btnDetect') as HTMLElement)?.addEventListener('click', () => {
        void getDetectedDevice()
            .then((info) => {
                fillTunerHostInfo(view, info);
            })
            .catch((error: unknown) => {
                console.error('Failed to detect Live TV tuner', error);
                Dashboard.alert({ message: globalize.translate('ErrorDefault') });
            });
    });
    (view.querySelector('.btnSelectPath') as HTMLElement)?.addEventListener('click', () => {
        void import('components/directorybrowser/directorybrowser')
            .then(({ default: DirectoryBrowser }) => {
                const picker = new DirectoryBrowser();
                picker.show({
                    includeFiles: true,
                    callback: (path: string) => {
                        if (path) {
                            (view.querySelector('.txtDevicePath') as HTMLInputElement).value = path;
                        }

                        picker.close();
                    }
                });
            })
            .catch((error: unknown) => {
                console.error('Failed to open directory browser', error);
                Dashboard.alert({ message: globalize.translate('ErrorDefault') });
            });
    });
}
