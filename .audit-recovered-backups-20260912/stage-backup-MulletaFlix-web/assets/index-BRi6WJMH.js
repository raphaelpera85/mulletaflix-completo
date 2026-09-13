import{w as p}from"./vendor-jellyfin-Bee54tkY.js";import{D as L,n as b,s as r,i as P,S as x,j as C,w as y,A as v,J as t,B as c,aY as T,aZ as H,E as w,a_ as F,av as I,aX as R,aD as M}from"./index-CFwqrzSZ.js";import{q as S}from"./qualityOptions-C-iPmz-O.js";import"./emby-select-BQevfB2a.js";import"./emby-checkbox-CDwDlGOj.js";/* empty css                 */import"./vendor-axios-BCn5QfZZ.js";import"./vendor-react-query-BiAmjtRH.js";import"./vendor-react-CeUY3tRn.js";import"./vendor-lodash-CYyrkkBC.js";import"./vendor-date-fns-CwLI6ukS.js";import"./vendor-dompurify-Baz99PXY.js";import"./actionSheet-BM-G2WQD.js";const Q=`<form style="margin: 0 auto;">
    <div class="verticalSection verticalSection-extrabottompadding">
        <h2 class="sectionTitle">
            \${HeaderAudioSettings}
        </h2>

        <div class="selectContainer">
            <select is="emby-select" id="selectAllowedAudioChannels" label="\${LabelAllowedAudioChannels}">
                <option value="-1">\${Auto}</option>
                <option value="1">\${LabelSelectMono}</option>
                <option value="2">\${LabelSelectStereo}</option>
                <option value="6">5.1 \${LabelSelectAudioChannels}</option>
                <option value="8">7.1 \${LabelSelectAudioChannels}</option>
            </select>
        </div>

        <div class="selectContainer">
            <select is="emby-select" id="selectAudioLanguage" label="\${LabelAudioLanguagePreference}"></select>
        </div>

        <label class="checkboxContainer">
            <input type="checkbox" is="emby-checkbox" class="chkPlayDefaultAudioTrack" />
            <span>\${LabelPlayDefaultAudioTrack}</span>
        </label>

        <div class="checkboxContainer checkboxContainer-withDescription">
            <label>
                <input type="checkbox" is="emby-checkbox" class="chkDisableVbrAudioEncoding" />
                <span>\${LabelDisableVbrAudioEncoding}</span>
            </label>
            <div class="fieldDescription checkboxFieldDescription">\${DisableVbrAudioEncodingHelp}</div>
        </div>
    </div>

    <div class="qualitySections hide">
        <div class="verticalSection verticalSection-extrabottompadding videoQualitySection hide">
            <h2 class="sectionTitle">
                \${HeaderVideoQuality}
            </h2>

            <div class="selectContainer fldVideoInNetworkQuality hide">
                <select is="emby-select" class="selectVideoInNetworkQuality" label="\${LabelHomeNetworkQuality}"></select>
            </div>

            <div class="selectContainer fldVideoInternetQuality hide">
                <select is="emby-select" class="selectVideoInternetQuality" label="\${LabelInternetQuality}"></select>
            </div>

            <div class="selectContainer fldChromecastQuality hide">
                <select is="emby-select" class="selectChromecastVideoQuality" label="\${LabelMaxChromecastBitrate}"></select>
            </div>

            <div class="selectContainer">
                <select is="emby-select" class="selectMaxVideoWidth" label="\${LabelMaxVideoResolution}">
                    <option value="0">\${Auto}</option>
                    <option value="-1">\${ScreenResolution}</option>
                    <option value="640">360p</option>
                    <option value="852">480p</option>
                    <option value="1280">720p</option>
                    <option value="1920">1080p</option>
                    <option value="3840">4K</option>
                    <option value="7680">8K</option>
                </select>
            </div>

            <div class="checkboxContainer checkboxContainer-withDescription">
                <label>
                    <input type="checkbox" is="emby-checkbox" class="chkLimitSupportedVideoResolution" />
                    <span>\${LimitSupportedVideoResolution}</span>
                </label>
                <div class="fieldDescription checkboxFieldDescription">\${LimitSupportedVideoResolutionHelp}</div>
            </div>
        </div>

        <div class="verticalSection verticalSection-extrabottompadding musicQualitySection hide">
            <h2>
                \${HeaderMusicQuality}
            </h2>

            <div class="selectContainer">
                <select is="emby-select" class="selectMusicInternetQuality" label="\${LabelInternetQuality}"></select>
            </div>
        </div>
    </div>

    <div class="verticalSection verticalSection-extrabottompadding">
        <h2 class="sectionTitle">
            \${TabAdvanced}
        </h2>

        <div class="checkboxContainer checkboxContainer-withDescription">
            <label>
                <input type="checkbox" is="emby-checkbox" class="chkPreferFmp4HlsContainer" />
                <span>\${PreferFmp4HlsContainer}</span>
            </label>
            <div class="fieldDescription checkboxFieldDescription">\${PreferFmp4HlsContainerHelp}</div>
        </div>

        <div class="checkboxContainer checkboxContainer-withDescription cinemaModeOptions">
            <label>
                <input type="checkbox" is="emby-checkbox" class="chkEnableCinemaMode" />
                <span>\${EnableCinemaMode}</span>
            </label>
            <div class="fieldDescription checkboxFieldDescription">\${CinemaModeConfigurationHelp}</div>
        </div>

        <div class="checkboxContainer fldEpisodeAutoPlay">
            <label>
                <input type="checkbox" is="emby-checkbox" class="chkEpisodeAutoPlay" />
                <span>\${PlayNextEpisodeAutomatically}</span>
            </label>
        </div>

        <div class="checkboxContainer checkboxContainer-withDescription">
            <label>
                <input type="checkbox" is="emby-checkbox" class="chkRememberAudioSelections" />
                <span>\${RememberAudioSelections}</span>
            </label>
            <div class="fieldDescription checkboxFieldDescription">\${RememberAudioSelectionsHelp}</div>
        </div>

        <div class="checkboxContainer checkboxContainer-withDescription">
            <label>
                <input type="checkbox" is="emby-checkbox" class="chkRememberSubtitleSelections" />
                <span>\${RememberSubtitleSelections}</span>
            </label>
            <div class="fieldDescription checkboxFieldDescription">\${RememberSubtitleSelectionsHelp}</div>
        </div>

        <div class="checkboxContainer checkboxContainer-withDescription fldEnableNextVideoOverlay">
            <label>
                <input type="checkbox" is="emby-checkbox" class="chkEnableNextVideoOverlay" />
                <span>\${EnableNextVideoInfoOverlay}</span>
            </label>
            <div class="fieldDescription checkboxFieldDescription">\${EnableNextVideoInfoOverlayHelp}</div>
        </div>

        <div class="checkboxContainer fldExternalPlayer checkboxContainer-withDescription hide">
            <label>
                <input type="checkbox" is="emby-checkbox" class="chkExternalVideoPlayer" />
                <span>\${EnableExternalVideoPlayers}</span>
            </label>
            <div class="fieldDescription checkboxFieldDescription">
                <div class="labelNativeExternalPlayers">\${EnableExternalVideoPlayersHelp}</div>
            </div>
        </div>

        <div class="selectContainer">
            <select is="emby-select" class="selectChromecastVersion" label="\${LabelChromecastVersion}"></select>
        </div>

        <div class="selectContainer">
            <select is="emby-select" class="selectSkipForwardLength" label="\${LabelSkipForwardLength}"></select>
        </div>

        <div class="selectContainer">
            <select is="emby-select" class="selectSkipBackLength" label="\${LabelSkipBackLength}"></select>
        </div>

        <h3 class="sectionTitle">\${HeaderMediaSegmentActions}</h3>
        <div class="mediaSegmentActionContainer"></div>
    </div>

    <div class="verticalSection verticalSection-extrabottompadding">
        <h2 class="sectionTitle">
            \${HeaderVideoAdvanced}
        </h2>

        <div class="checkboxContainer checkboxContainer-withDescription fldEnableDts">
            <label>
                <input type="checkbox" is="emby-checkbox" class="chkEnableDts" />
                <span>\${EnableDts}</span>
            </label>
            <div class="fieldDescription checkboxFieldDescription">\${EnableDtsHelp}</div>
        </div>

        <div class="checkboxContainer checkboxContainer-withDescription fldEnableTrueHd">
            <label>
                <input type="checkbox" is="emby-checkbox" class="chkEnableTrueHd" />
                <span>\${EnableTrueHd}</span>
            </label>
            <div class="fieldDescription checkboxFieldDescription">\${EnableTrueHdHelp}</div>
        </div>

        <div class="checkboxContainer checkboxContainer-withDescription fldEnableHi10p hide">
             <label>
                 <input type="checkbox" is="emby-checkbox" class="chkEnableHi10p" />
                 <span>\${EnableHi10p}</span>
             </label>
             <div class="fieldDescription checkboxFieldDescription">\${EnableHi10pHelp}</div>
        </div>

        <div class="checkboxContainer checkboxContainer-withDescription fldLimitSegmentLength hide">
            <label>
                <input type="checkbox" is="emby-checkbox" class="chkLimitSegmentLength" />
                <span>\${LimitSegmentLength}</span>
            </label>
            <div class="fieldDescription checkboxFieldDescription">\${LimitSegmentLengthHelp}</div>
        </div>

        <div class="selectContainer">
            <select is="emby-select" id="selectPreferredTranscodeVideoCodec" label="\${LabelSelectPreferredTranscodeVideoCodec}">
                <option value="">\${Auto}</option>
                <option value="h264">H264</option>
                <option value="hevc">HEVC</option>
                <option value="av1">AV1</option>
            </select>
            <div class="fieldDescription">\${SelectPreferredTranscodeVideoCodecHelp}</div>
        </div>

        <div class="selectContainer">
            <select is="emby-select" id="selectPreferredTranscodeVideoAudioCodec" label="\${LabelSelectPreferredTranscodeVideoAudioCodec}">
                <option value="">\${Auto}</option>
                <option value="aac">AAC</option>
                <option value="ac3">AC3</option>
                <option value="alac">ALAC</option>
                <option value="dts">DTS</option>
                <option value="flac">FLAC</option>
                <option value="opus">Opus</option>
            </select>
            <div class="fieldDescription">\${SelectPreferredTranscodeVideoAudioCodecHelp}</div>
        </div>
    </div>

    <div class="verticalSection verticalSection-extrabottompadding">
        <h2 class="sectionTitle">
            \${HeaderAudioAdvanced}
        </h2>

        <div class="selectContainer">
            <select is="emby-select" id="selectAudioNormalization" label="\${LabelSelectAudioNormalization}">
                <option value="Off">\${Off}</option>
                <option value="TrackGain">\${LabelTrackGain}</option>
                <option value="AlbumGain">\${LabelAlbumGain}</option>
            </select>
            <div class="fieldDescription">\${SelectAudioNormalizationHelp}</div>
        </div>

        <div class="checkboxContainer checkboxContainer-withDescription">
            <label>
                <input type="checkbox" is="emby-checkbox" class="chkAlwaysRemuxFlac" />
                <span>\${LabelAlwaysRemuxFlacAudioFiles}</span>
            </label>
            <div class="fieldDescription checkboxFieldDescription">\${AlwaysRemuxFlacAudioFilesHelp}</div>
        </div>

        <div class="checkboxContainer checkboxContainer-withDescription">
            <label>
                <input type="checkbox" is="emby-checkbox" class="chkAlwaysRemuxMp3" />
                <span>\${LabelAlwaysRemuxMp3AudioFiles}</span>
            </label>
            <div class="fieldDescription checkboxFieldDescription">\${AlwaysRemuxMp3AudioFilesHelp}</div>
        </div>
    </div>

    <button is="emby-button" type="submit" class="raised button-submit block btnSave hide">
        <span>\${Save}</span>
    </button>
</form>
`;function q(e){return x.getApiClient(e)}function V(e){return{Id:e.Id,Policy:{EnableVideoPlaybackTranscoding:e.Policy?.EnableVideoPlaybackTranscoding===!0,EnableAudioPlaybackTranscoding:e.Policy?.EnableAudioPlaybackTranscoding===!0},Configuration:{AudioLanguagePreference:e.Configuration?.AudioLanguagePreference??void 0,EnableNextEpisodeAutoPlay:e.Configuration?.EnableNextEpisodeAutoPlay===!0,PlayDefaultAudioTrack:e.Configuration?.PlayDefaultAudioTrack===!0,RememberAudioSelections:e.Configuration?.RememberAudioSelections===!0,RememberSubtitleSelections:e.Configuration?.RememberSubtitleSelections===!0,CastReceiverId:e.Configuration?.CastReceiverId??void 0}}}function B(e){return{CastReceiverApplications:(e.CastReceiverApplications??[]).map(i=>({Id:i.Id||"",Name:i.Name||""}))}}function A(e){const i=[5,10,15,20,25,30];e.innerHTML=i.map(n=>({name:r.translate("ValueSeconds",String(n)),value:n*1e3})).map(n=>`<option value="${c(String(n.value))}">${c(n.name)}</option>`).join("")}function N(e,i){let n="";n+=`<option value=''>${r.translate("AnyLanguage")}</option>`,n+=`<option value='OriginalLanguage'>${r.translate("OriginalLanguage")}</option>`;for(let a=0,l=i.length;a<l;a++){const o=i[a];n+=`<option value='${c(o.ThreeLetterISOLanguageName||"")}'>${c(o.DisplayName||"")}</option>`}e.innerHTML=n}function O(e,i){const n={},a=Object.values(T).map(o=>{const s=r.translate(`MediaSegmentAction.${o}`);return`<option value='${c(o)}'>${c(s)}</option>`}).join(""),l=[p.Intro,p.Preview,p.Recap,p.Commercial,p.Outro].map(o=>{const s=r.translate("LabelMediaSegmentsType",r.translate(`MediaSegmentType.${o}`)),d=F(o);return n[d]=H(i,o),`<div class="selectContainer">
<select is="emby-select" id="${c(d)}" class="segmentTypeAction" label="${c(s)}">
    ${a}
</select>
</div>`}).join("");e.innerHTML=l,Object.entries(n).forEach(([o,s])=>{const d=e.querySelector(`#${CSS.escape(o)}`);d&&(d.value=s)})}function U(e,i,n){const a=n==="Audio"?S.getAudioQualityOptions({currentMaxBitrate:t.maxStreamingBitrate(i,n),isAutomaticBitrateEnabled:t.enableAutomaticBitrateDetection(i,n),enableAuto:!0}):S.getVideoQualityOptions({currentMaxBitrate:t.maxStreamingBitrate(i,n),isAutomaticBitrateEnabled:t.enableAutomaticBitrateDetection(i,n),enableAuto:!0});e.innerHTML=a.map(l=>`<option value="${c(String(l.bitrate||""))}">${c(String(l.name||""))}</option>`).join("")}function h(e,i,n){U(e,i,n),t.enableAutomaticBitrateDetection(i,n)?e.value="":e.value=String(t.maxStreamingBitrate(i,n))}function z(e){const i=S.getVideoQualityOptions({currentMaxBitrate:t.maxChromecastBitrate(),isAutomaticBitrateEnabled:!t.maxChromecastBitrate(),enableAuto:!0});e.innerHTML=i.map(n=>`<option value="${c(String(n.bitrate||""))}">${c(String(n.name||""))}</option>`).join(""),e.value=String(t.maxChromecastBitrate()||"")}function m(e,i,n){e.value?(t.maxStreamingBitrate(i,n,e.value),t.enableAutomaticBitrateDetection(i,n,!1)):t.enableAutomaticBitrateDetection(i,n,!0)}function j(e,i,n){if(i.Policy.EnableVideoPlaybackTranscoding?e.querySelector(".videoQualitySection").classList.remove("hide"):e.querySelector(".videoQualitySection").classList.add("hide"),y.supports(v.MultiServer)){e.querySelector(".fldVideoInNetworkQuality").classList.remove("hide"),e.querySelector(".fldVideoInternetQuality").classList.remove("hide"),i.Policy.EnableAudioPlaybackTranscoding?e.querySelector(".musicQualitySection").classList.remove("hide"):e.querySelector(".musicQualitySection").classList.add("hide");return}n.getEndpointInfo().then(a=>{a.IsInNetwork?(e.querySelector(".fldVideoInNetworkQuality").classList.remove("hide"),e.querySelector(".fldVideoInternetQuality").classList.add("hide"),e.querySelector(".musicQualitySection").classList.add("hide")):(e.querySelector(".fldVideoInNetworkQuality").classList.add("hide"),e.querySelector(".fldVideoInternetQuality").classList.remove("hide"),i.Policy.EnableAudioPlaybackTranscoding?e.querySelector(".musicQualitySection").classList.remove("hide"):e.querySelector(".musicQualitySection").classList.add("hide"))}).catch(a=>{console.error("[PlaybackSettings] failed to load endpoint information",a)})}function W(e,i,n,a,l){const o=l.getCurrentUserId(),s=i.Id;j(e,i,l),C.safari&&e.querySelector(".fldEnableHi10p").classList.remove("hide"),C.web0s&&e.querySelector(".fldLimitSegmentLength").classList.remove("hide"),e.querySelector("#selectAllowedAudioChannels").value=n.allowedAudioChannels(),l.getCultures().then(u=>{N(e.querySelector("#selectAudioLanguage"),u),e.querySelector("#selectAudioLanguage",e).value=i.Configuration.AudioLanguagePreference||"",e.querySelector(".chkEpisodeAutoPlay").checked=i.Configuration.EnableNextEpisodeAutoPlay||!1}).catch(u=>{console.error("[PlaybackSettings] failed to load cultures",u),b(r.translate("ErrorDefault"))}),y.supports(v.ExternalPlayerIntent)&&s===o?e.querySelector(".fldExternalPlayer").classList.remove("hide"):e.querySelector(".fldExternalPlayer").classList.add("hide"),s===o&&(i.Policy.EnableVideoPlaybackTranscoding||i.Policy.EnableAudioPlaybackTranscoding)?(e.querySelector(".qualitySections").classList.remove("hide"),y.supports(v.Chromecast)&&i.Policy.EnableVideoPlaybackTranscoding?e.querySelector(".fldChromecastQuality").classList.remove("hide"):e.querySelector(".fldChromecastQuality").classList.add("hide")):(e.querySelector(".qualitySections").classList.add("hide"),e.querySelector(".fldChromecastQuality").classList.add("hide")),e.querySelector(".chkPlayDefaultAudioTrack").checked=i.Configuration.PlayDefaultAudioTrack||!1,e.querySelector(".chkPreferFmp4HlsContainer").checked=n.preferFmp4HlsContainer(),e.querySelector(".chkLimitSegmentLength").checked=n.limitSegmentLength(),e.querySelector(".chkEnableDts").checked=t.enableDts(),e.querySelector(".chkEnableTrueHd").checked=t.enableTrueHd(),e.querySelector(".chkEnableHi10p").checked=t.enableHi10p(),e.querySelector(".chkEnableCinemaMode").checked=n.enableCinemaMode(),e.querySelector("#selectAudioNormalization").value=n.selectAudioNormalization(),e.querySelector(".chkEnableNextVideoOverlay").checked=n.enableNextVideoInfoOverlay(),e.querySelector(".chkRememberAudioSelections").checked=i.Configuration.RememberAudioSelections||!1,e.querySelector(".chkRememberSubtitleSelections").checked=i.Configuration.RememberSubtitleSelections||!1,e.querySelector(".chkExternalVideoPlayer").checked=t.enableSystemExternalPlayers(),e.querySelector(".chkLimitSupportedVideoResolution").checked=t.limitSupportedVideoResolution(),e.querySelector("#selectPreferredTranscodeVideoCodec").value=t.preferredTranscodeVideoCodec(),e.querySelector("#selectPreferredTranscodeVideoAudioCodec").value=t.preferredTranscodeVideoAudioCodec(),e.querySelector(".chkDisableVbrAudioEncoding").checked=t.disableVbrAudio(),e.querySelector(".chkAlwaysRemuxFlac").checked=t.alwaysRemuxFlac(),e.querySelector(".chkAlwaysRemuxMp3").checked=t.alwaysRemuxMp3(),h(e.querySelector(".selectVideoInNetworkQuality"),!0,"Video"),h(e.querySelector(".selectVideoInternetQuality"),!1,"Video"),h(e.querySelector(".selectMusicInternetQuality"),!1,"Audio"),z(e.querySelector(".selectChromecastVideoQuality"));const d=e.querySelector(".selectChromecastVersion");let k="";for(const u of a.CastReceiverApplications)k+=`<option value='${c(u.Id)}'>${c(u.Name)}</option>`;d.innerHTML=k,d.value=i.Configuration.CastReceiverId;const E=e.querySelector(".selectMaxVideoWidth");E.value=t.maxVideoWidth();const f=e.querySelector(".selectSkipForwardLength");A(f),f.value=n.skipForwardLength();const g=e.querySelector(".selectSkipBackLength");A(g),g.value=n.skipBackLength();const D=e.querySelector(".mediaSegmentActionContainer");O(D,n)}function G(e,i,n,a){t.enableSystemExternalPlayers(e.querySelector(".chkExternalVideoPlayer").checked),t.maxChromecastBitrate(e.querySelector(".selectChromecastVideoQuality").value),t.maxVideoWidth(e.querySelector(".selectMaxVideoWidth").value),t.limitSupportedVideoResolution(e.querySelector(".chkLimitSupportedVideoResolution").checked),t.preferredTranscodeVideoCodec(e.querySelector("#selectPreferredTranscodeVideoCodec").value),t.preferredTranscodeVideoAudioCodec(e.querySelector("#selectPreferredTranscodeVideoAudioCodec").value),t.enableDts(e.querySelector(".chkEnableDts").checked),t.enableTrueHd(e.querySelector(".chkEnableTrueHd").checked),t.enableHi10p(e.querySelector(".chkEnableHi10p").checked),t.disableVbrAudio(e.querySelector(".chkDisableVbrAudioEncoding").checked),t.alwaysRemuxFlac(e.querySelector(".chkAlwaysRemuxFlac").checked),t.alwaysRemuxMp3(e.querySelector(".chkAlwaysRemuxMp3").checked),m(e.querySelector(".selectVideoInNetworkQuality"),!0,"Video"),m(e.querySelector(".selectVideoInternetQuality"),!1,"Video"),m(e.querySelector(".selectMusicInternetQuality"),!1,"Audio"),n.allowedAudioChannels(e.querySelector("#selectAllowedAudioChannels").value),i.Configuration.AudioLanguagePreference=e.querySelector("#selectAudioLanguage").value,i.Configuration.PlayDefaultAudioTrack=e.querySelector(".chkPlayDefaultAudioTrack").checked,i.Configuration.EnableNextEpisodeAutoPlay=e.querySelector(".chkEpisodeAutoPlay").checked,n.preferFmp4HlsContainer(e.querySelector(".chkPreferFmp4HlsContainer").checked),n.limitSegmentLength(e.querySelector(".chkLimitSegmentLength").checked),n.enableCinemaMode(e.querySelector(".chkEnableCinemaMode").checked),n.selectAudioNormalization(e.querySelector("#selectAudioNormalization").value),n.enableNextVideoInfoOverlay(e.querySelector(".chkEnableNextVideoOverlay").checked),i.Configuration.RememberAudioSelections=e.querySelector(".chkRememberAudioSelections").checked,i.Configuration.RememberSubtitleSelections=e.querySelector(".chkRememberSubtitleSelections").checked,i.Configuration.CastReceiverId=e.querySelector(".selectChromecastVersion").value,n.skipForwardLength(e.querySelector(".selectSkipForwardLength").value),n.skipBackLength(e.querySelector(".selectSkipBackLength").value);const l=e.querySelectorAll(".segmentTypeAction")||[];return Array.prototype.forEach.call(l,o=>{n.set(o.id,o.value,!1)}),a.updateUserConfiguration(i.Id||"",i.Configuration)}async function K(e,i,n,a,l,o){const s=await l.getUser(n);await G(i,V(s),a,l),o&&b(r.translate("SettingsSaved")),w.trigger(e,"saved")}function $(e){const i=this,n=q(i.options.serverId),a=i.options.userId,l=i.options.userSettings;return L.withLoading(async()=>{await l.setUserInfo(a,n);const o=i.options.enableSaveConfirmation;await K(i,i.options.element,a,l,n,o)}).catch(()=>{b(r.translate("ErrorDefault"))}),e&&e.preventDefault(),!1}function J(e,i){e.element.innerHTML=r.translateHtml(Q,"core"),e.element.querySelector("form").addEventListener("submit",$.bind(i)),e.enableSaveButton&&e.element.querySelector(".btnSave").classList.remove("hide"),i.loadData(),e.autoFocus&&P.autoFocus(e.element)}class X{constructor(i){this.options=i,this.dataLoaded=!1,J(i,this)}loadData(){const i=this,n=i.options.element,a=i.options.userId,l=q(i.options.serverId),o=i.options.userSettings;L.withLoading(async()=>{const s=V(await l.getUser(a)),d=B(await l.getSystemInfo());await o.setUserInfo(a,l),i.dataLoaded=!0,W(n,s,o,d,l)}).catch(()=>{b(r.translate("ErrorDefault"))})}submit(){$.call(this,void 0)}destroy(){this.options=null}}const Y=R;function ue(e,i){let n;const a=i.userId||ApiClient.getCurrentUserId(),l=a===ApiClient.getCurrentUserId()?I:new Y;e.addEventListener("viewshow",function(){if(n){n.loadData();return}n=new X({serverId:ApiClient.serverId(),userId:a,element:e.querySelector(".settingsContainer"),userSettings:l,enableSaveButton:!0,enableSaveConfirmation:!0,autoFocus:M.isEnabled()})}),e.addEventListener("viewdestroy",function(){n&&(n.destroy(),n=void 0)})}export{ue as default};
