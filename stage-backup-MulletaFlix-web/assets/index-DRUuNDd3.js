import{a2 as u,W as h,f as d,a$ as P,S as x,b as A,k as v,A as S,h as t,e as s,cX as T,cY as p,cZ as H,E as w,c_ as F,bg as I,cW as R,bq as M}from"./index-6F8mVyyU.js";import{q as k}from"./qualityOptions-h92WLSdP.js";import"./emby-select-BS-dUTXm.js";import"./actionSheet-bHfWrycr.js";const Q=`<form style="margin: 0 auto;">
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
`;function q(e){return x.getApiClient(e)}function $(e){return{Id:e.Id,Policy:{EnableVideoPlaybackTranscoding:e.Policy?.EnableVideoPlaybackTranscoding===!0,EnableAudioPlaybackTranscoding:e.Policy?.EnableAudioPlaybackTranscoding===!0},Configuration:{AudioLanguagePreference:e.Configuration?.AudioLanguagePreference??void 0,EnableNextEpisodeAutoPlay:e.Configuration?.EnableNextEpisodeAutoPlay===!0,PlayDefaultAudioTrack:e.Configuration?.PlayDefaultAudioTrack===!0,RememberAudioSelections:e.Configuration?.RememberAudioSelections===!0,RememberSubtitleSelections:e.Configuration?.RememberSubtitleSelections===!0,CastReceiverId:e.Configuration?.CastReceiverId??void 0}}}function B(e){return{CastReceiverApplications:(e.CastReceiverApplications??[]).map(i=>({Id:i.Id||"",Name:i.Name||""}))}}function L(e){const i=[5,10,15,20,25,30];e.innerHTML=i.map(n=>({name:d.translate("ValueSeconds",String(n)),value:n*1e3})).map(n=>`<option value="${s(String(n.value))}">${s(n.name)}</option>`).join("")}function N(e,i){let n="";n+=`<option value=''>${d.translate("AnyLanguage")}</option>`,n+=`<option value='OriginalLanguage'>${d.translate("OriginalLanguage")}</option>`;for(let a=0,l=i.length;a<l;a++){const o=i[a];n+=`<option value='${s(o.ThreeLetterISOLanguageName||"")}'>${s(o.DisplayName||"")}</option>`}e.innerHTML=n}function O(e,i){const n={},a=Object.values(T).map(o=>{const c=d.translate(`MediaSegmentAction.${o}`);return`<option value='${s(o)}'>${s(c)}</option>`}).join(""),l=[p.Intro,p.Preview,p.Recap,p.Commercial,p.Outro].map(o=>{const c=d.translate("LabelMediaSegmentsType",d.translate(`MediaSegmentType.${o}`)),r=F(o);return n[r]=H(i,o),`<div class="selectContainer">
<select is="emby-select" id="${s(r)}" class="segmentTypeAction" label="${s(c)}">
    ${a}
</select>
</div>`}).join("");e.innerHTML=l,Object.entries(n).forEach(([o,c])=>{const r=e.querySelector(`#${CSS.escape(o)}`);r&&(r.value=c)})}function U(e,i,n){const a=n==="Audio"?k.getAudioQualityOptions({currentMaxBitrate:t.maxStreamingBitrate(i,n),isAutomaticBitrateEnabled:t.enableAutomaticBitrateDetection(i,n),enableAuto:!0}):k.getVideoQualityOptions({currentMaxBitrate:t.maxStreamingBitrate(i,n),isAutomaticBitrateEnabled:t.enableAutomaticBitrateDetection(i,n),enableAuto:!0});e.innerHTML=a.map(l=>`<option value="${s(String(l.bitrate||""))}">${s(String(l.name||""))}</option>`).join("")}function y(e,i,n){U(e,i,n),t.enableAutomaticBitrateDetection(i,n)?e.value="":e.value=String(t.maxStreamingBitrate(i,n))}function z(e){const i=k.getVideoQualityOptions({currentMaxBitrate:t.maxChromecastBitrate(),isAutomaticBitrateEnabled:!t.maxChromecastBitrate(),enableAuto:!0});e.innerHTML=i.map(n=>`<option value="${s(String(n.bitrate||""))}">${s(String(n.name||""))}</option>`).join(""),e.value=String(t.maxChromecastBitrate()||"")}function m(e,i,n){e.value?(t.maxStreamingBitrate(i,n,e.value),t.enableAutomaticBitrateDetection(i,n,!1)):t.enableAutomaticBitrateDetection(i,n,!0)}function W(e,i,n){if(i.Policy.EnableVideoPlaybackTranscoding?e.querySelector(".videoQualitySection").classList.remove("hide"):e.querySelector(".videoQualitySection").classList.add("hide"),v.supports(S.MultiServer)){e.querySelector(".fldVideoInNetworkQuality").classList.remove("hide"),e.querySelector(".fldVideoInternetQuality").classList.remove("hide"),i.Policy.EnableAudioPlaybackTranscoding?e.querySelector(".musicQualitySection").classList.remove("hide"):e.querySelector(".musicQualitySection").classList.add("hide");return}n.getEndpointInfo().then(a=>{a.IsInNetwork?(e.querySelector(".fldVideoInNetworkQuality").classList.remove("hide"),e.querySelector(".fldVideoInternetQuality").classList.add("hide"),e.querySelector(".musicQualitySection").classList.add("hide")):(e.querySelector(".fldVideoInNetworkQuality").classList.add("hide"),e.querySelector(".fldVideoInternetQuality").classList.remove("hide"),i.Policy.EnableAudioPlaybackTranscoding?e.querySelector(".musicQualitySection").classList.remove("hide"):e.querySelector(".musicQualitySection").classList.add("hide"))}).catch(a=>{console.error("[PlaybackSettings] failed to load endpoint information",a)})}function j(e,i,n,a,l){const o=l.getCurrentUserId(),c=i.Id;W(e,i,l),A.safari&&e.querySelector(".fldEnableHi10p").classList.remove("hide"),A.web0s&&e.querySelector(".fldLimitSegmentLength").classList.remove("hide"),e.querySelector("#selectAllowedAudioChannels").value=n.allowedAudioChannels(),l.getCultures().then(b=>{N(e.querySelector("#selectAudioLanguage"),b),e.querySelector("#selectAudioLanguage",e).value=i.Configuration.AudioLanguagePreference||"",e.querySelector(".chkEpisodeAutoPlay").checked=i.Configuration.EnableNextEpisodeAutoPlay||!1}).catch(b=>{console.error("[PlaybackSettings] failed to load cultures",b),h(d.translate("ErrorDefault"))}),v.supports(S.ExternalPlayerIntent)&&c===o?e.querySelector(".fldExternalPlayer").classList.remove("hide"):e.querySelector(".fldExternalPlayer").classList.add("hide"),c===o&&(i.Policy.EnableVideoPlaybackTranscoding||i.Policy.EnableAudioPlaybackTranscoding)?(e.querySelector(".qualitySections").classList.remove("hide"),v.supports(S.Chromecast)&&i.Policy.EnableVideoPlaybackTranscoding?e.querySelector(".fldChromecastQuality").classList.remove("hide"):e.querySelector(".fldChromecastQuality").classList.add("hide")):(e.querySelector(".qualitySections").classList.add("hide"),e.querySelector(".fldChromecastQuality").classList.add("hide")),e.querySelector(".chkPlayDefaultAudioTrack").checked=i.Configuration.PlayDefaultAudioTrack||!1,e.querySelector(".chkPreferFmp4HlsContainer").checked=n.preferFmp4HlsContainer(),e.querySelector(".chkLimitSegmentLength").checked=n.limitSegmentLength(),e.querySelector(".chkEnableDts").checked=t.enableDts(),e.querySelector(".chkEnableTrueHd").checked=t.enableTrueHd(),e.querySelector(".chkEnableHi10p").checked=t.enableHi10p(),e.querySelector(".chkEnableCinemaMode").checked=n.enableCinemaMode(),e.querySelector("#selectAudioNormalization").value=n.selectAudioNormalization(),e.querySelector(".chkEnableNextVideoOverlay").checked=n.enableNextVideoInfoOverlay(),e.querySelector(".chkRememberAudioSelections").checked=i.Configuration.RememberAudioSelections||!1,e.querySelector(".chkRememberSubtitleSelections").checked=i.Configuration.RememberSubtitleSelections||!1,e.querySelector(".chkExternalVideoPlayer").checked=t.enableSystemExternalPlayers(),e.querySelector(".chkLimitSupportedVideoResolution").checked=t.limitSupportedVideoResolution(),e.querySelector("#selectPreferredTranscodeVideoCodec").value=t.preferredTranscodeVideoCodec(),e.querySelector("#selectPreferredTranscodeVideoAudioCodec").value=t.preferredTranscodeVideoAudioCodec(),e.querySelector(".chkDisableVbrAudioEncoding").checked=t.disableVbrAudio(),e.querySelector(".chkAlwaysRemuxFlac").checked=t.alwaysRemuxFlac(),e.querySelector(".chkAlwaysRemuxMp3").checked=t.alwaysRemuxMp3(),y(e.querySelector(".selectVideoInNetworkQuality"),!0,"Video"),y(e.querySelector(".selectVideoInternetQuality"),!1,"Video"),y(e.querySelector(".selectMusicInternetQuality"),!1,"Audio"),z(e.querySelector(".selectChromecastVideoQuality"));const r=e.querySelector(".selectChromecastVersion");let f="";for(const b of a.CastReceiverApplications)f+=`<option value='${s(b.Id)}'>${s(b.Name)}</option>`;r.innerHTML=f,r.value=i.Configuration.CastReceiverId;const V=e.querySelector(".selectMaxVideoWidth");V.value=t.maxVideoWidth();const g=e.querySelector(".selectSkipForwardLength");L(g),g.value=n.skipForwardLength();const C=e.querySelector(".selectSkipBackLength");L(C),C.value=n.skipBackLength();const D=e.querySelector(".mediaSegmentActionContainer");O(D,n),u.hide()}function G(e,i,n,a){t.enableSystemExternalPlayers(e.querySelector(".chkExternalVideoPlayer").checked),t.maxChromecastBitrate(e.querySelector(".selectChromecastVideoQuality").value),t.maxVideoWidth(e.querySelector(".selectMaxVideoWidth").value),t.limitSupportedVideoResolution(e.querySelector(".chkLimitSupportedVideoResolution").checked),t.preferredTranscodeVideoCodec(e.querySelector("#selectPreferredTranscodeVideoCodec").value),t.preferredTranscodeVideoAudioCodec(e.querySelector("#selectPreferredTranscodeVideoAudioCodec").value),t.enableDts(e.querySelector(".chkEnableDts").checked),t.enableTrueHd(e.querySelector(".chkEnableTrueHd").checked),t.enableHi10p(e.querySelector(".chkEnableHi10p").checked),t.disableVbrAudio(e.querySelector(".chkDisableVbrAudioEncoding").checked),t.alwaysRemuxFlac(e.querySelector(".chkAlwaysRemuxFlac").checked),t.alwaysRemuxMp3(e.querySelector(".chkAlwaysRemuxMp3").checked),m(e.querySelector(".selectVideoInNetworkQuality"),!0,"Video"),m(e.querySelector(".selectVideoInternetQuality"),!1,"Video"),m(e.querySelector(".selectMusicInternetQuality"),!1,"Audio"),n.allowedAudioChannels(e.querySelector("#selectAllowedAudioChannels").value),i.Configuration.AudioLanguagePreference=e.querySelector("#selectAudioLanguage").value,i.Configuration.PlayDefaultAudioTrack=e.querySelector(".chkPlayDefaultAudioTrack").checked,i.Configuration.EnableNextEpisodeAutoPlay=e.querySelector(".chkEpisodeAutoPlay").checked,n.preferFmp4HlsContainer(e.querySelector(".chkPreferFmp4HlsContainer").checked),n.limitSegmentLength(e.querySelector(".chkLimitSegmentLength").checked),n.enableCinemaMode(e.querySelector(".chkEnableCinemaMode").checked),n.selectAudioNormalization(e.querySelector("#selectAudioNormalization").value),n.enableNextVideoInfoOverlay(e.querySelector(".chkEnableNextVideoOverlay").checked),i.Configuration.RememberAudioSelections=e.querySelector(".chkRememberAudioSelections").checked,i.Configuration.RememberSubtitleSelections=e.querySelector(".chkRememberSubtitleSelections").checked,i.Configuration.CastReceiverId=e.querySelector(".selectChromecastVersion").value,n.skipForwardLength(e.querySelector(".selectSkipForwardLength").value),n.skipBackLength(e.querySelector(".selectSkipBackLength").value);const l=e.querySelectorAll(".segmentTypeAction")||[];return Array.prototype.forEach.call(l,o=>{n.set(o.id,o.value,!1)}),a.updateUserConfiguration(i.Id||"",i.Configuration)}function K(e,i,n,a,l,o){u.show(),l.getUser(n).then(c=>G(i,$(c),a,l)).then(()=>{u.hide(),o&&h(d.translate("SettingsSaved")),w.trigger(e,"saved")}).catch(()=>{u.hide(),h(d.translate("ErrorDefault"))})}function E(e){const i=this,n=q(i.options.serverId),a=i.options.userId,l=i.options.userSettings;return l.setUserInfo(a,n).then(()=>{const o=i.options.enableSaveConfirmation;K(i,i.options.element,a,l,n,o)}).catch(()=>{u.hide(),h(d.translate("ErrorDefault"))}),e&&e.preventDefault(),!1}function X(e,i){e.element.innerHTML=d.translateHtml(Q,"core"),e.element.querySelector("form").addEventListener("submit",E.bind(i)),e.enableSaveButton&&e.element.querySelector(".btnSave").classList.remove("hide"),i.loadData(),e.autoFocus&&P.autoFocus(e.element)}class Y{constructor(i){this.options=i,this.dataLoaded=!1,X(i,this)}loadData(){const i=this,n=i.options.element;u.show();const a=i.options.userId,l=q(i.options.serverId),o=i.options.userSettings;l.getUser(a).then(c=>l.getSystemInfo().then(r=>({user:$(c),systemInfo:B(r)}))).then(({user:c,systemInfo:r})=>o.setUserInfo(a,l).then(()=>{i.dataLoaded=!0,j(n,c,o,r,l)})).catch(()=>{u.hide(),h(d.translate("ErrorDefault"))})}submit(){E.call(this,void 0)}destroy(){this.options=null}}const Z=R;function ne(e,i){let n;const a=i.userId||ApiClient.getCurrentUserId(),l=a===ApiClient.getCurrentUserId()?I:new Z;e.addEventListener("viewshow",function(){if(n){n.loadData();return}n=new Y({serverId:ApiClient.serverId(),userId:a,element:e.querySelector(".settingsContainer"),userSettings:l,enableSaveButton:!0,enableSaveConfirmation:!0,autoFocus:M.isEnabled()})}),e.addEventListener("viewdestroy",function(){n&&(n.destroy(),n=void 0)})}export{ne as default};
