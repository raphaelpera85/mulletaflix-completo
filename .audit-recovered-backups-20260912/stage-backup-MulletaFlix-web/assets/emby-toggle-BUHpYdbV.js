const __vite__mapDeps=(i,m=__vite__mapDeps,d=(m.f||(m.f=["./imageOptionsEditor-CgrWwRoV.js","./index-CFwqrzSZ.js","./vendor-react-query-BiAmjtRH.js","./vendor-react-CeUY3tRn.js","./vendor-jellyfin-Bee54tkY.js","./vendor-axios-BCn5QfZZ.js","./vendor-lodash-CYyrkkBC.js","./vendor-date-fns-CwLI6ukS.js","./vendor-dompurify-Baz99PXY.js","./index-BAjafMOe.css","./emby-checkbox-CDwDlGOj.js","./emby-checkbox-O4Gt6THi.css","./emby-select-BQevfB2a.js","./actionSheet-BM-G2WQD.js","./actionSheet-BKZT3mVB.css","./listview-CPZd3k-k.css","./emby-select-CyC5lbTl.css"])))=>i.map(i=>d[i]);
import{_ as q}from"./vendor-react-query-BiAmjtRH.js";import{C as S}from"./vendor-jellyfin-Bee54tkY.js";import{s as c,B as n,h as m}from"./index-CFwqrzSZ.js";import"./emby-checkbox-CDwDlGOj.js";import"./emby-select-BQevfB2a.js";import"./emby-textarea-BsVRAtiX.js";const M=`<h2>\${HeaderLibrarySettings}</h2>
<div class="checkboxContainer checkboxContainer-withDescription chkEnabledContainer">
    <label>
        <input type="checkbox" is="emby-checkbox" class="chkEnabled" checked />
        <span>\${EnableLibrary}</span>
    </label>
    <div class="fieldDescription checkboxFieldDescription">\${EnableLibraryHelp}</div>
</div>

<div class="selectContainer fldMetadataLanguage hide">
    <select is="emby-select" id="selectLanguage" label="\${LabelMetadataDownloadLanguage}"></select>
</div>
<div class="selectContainer fldMetadataCountry hide">
    <select is="emby-select" id="selectCountry" label="\${LabelCountry}"></select>
</div>
<div class="checkboxContainer checkboxContainer-withDescription chkEnablePhotosContainer">
    <label>
        <input type="checkbox" is="emby-checkbox" class="chkEnablePhotos" checked />
        <span>\${EnablePhotos}</span>
    </label>
    <div class="fieldDescription checkboxFieldDescription">\${EnablePhotosHelp}</div>
</div>

<div class="inputContainer fldSeasonZeroDisplayName hide advanced">
    <input is="emby-input" type="text" id="txtSeasonZeroName" label="\${LabelSpecialSeasonsDisplayName}" value="Specials" required />
</div>
<div class="checkboxContainer checkboxContainer-withDescription chkEnableEmbeddedTitlesContainer hide advanced">
    <label>
        <input is="emby-checkbox" type="checkbox" id="chkEnableEmbeddedTitles" />
        <span>\${PreferEmbeddedTitlesOverFileNames}</span>
    </label>
    <div class="fieldDescription checkboxFieldDescription">\${PreferEmbeddedTitlesOverFileNamesHelp}</div>
</div>
<div class="checkboxContainer checkboxContainer-withDescription chkEnableEmbeddedExtrasTitlesContainer hide advanced">
    <label>
        <input is="emby-checkbox" type="checkbox" id="chkEnableEmbeddedExtrasTitles" />
        <span>\${PreferEmbeddedExtrasTitlesOverFileNames}</span>
    </label>
    <div class="fieldDescription checkboxFieldDescription">\${PreferEmbeddedExtrasTitlesOverFileNamesHelp}</div>
</div>
<div class="checkboxContainer checkboxContainer-withDescription chkEnableEmbeddedEpisodeInfosContainer hide advanced">
    <label>
        <input is="emby-checkbox" type="checkbox" id="chkEnableEmbeddedEpisodeInfos" />
        <span>\${PreferEmbeddedEpisodeInfosOverFileNames}</span>
    </label>
    <div class="fieldDescription checkboxFieldDescription">\${PreferEmbeddedEpisodeInfosOverFileNamesHelp}</div>
</div>
<div class="selectContainer fldAllowEmbeddedSubtitlesContainer hide advanced" style="margin: 2em 0;">
    <select is="emby-select" id="selectAllowEmbeddedSubtitles" label="\${AllowEmbeddedSubtitles}">
        <option value="AllowAll">\${AllowEmbeddedSubtitlesAllowAllOption}</option>
        <option value="AllowText">\${AllowEmbeddedSubtitlesAllowTextOption}</option>
        <option value="AllowImage">\${AllowEmbeddedSubtitlesAllowImageOption}</option>
        <option value="AllowNone">\${AllowEmbeddedSubtitlesAllowNoneOption}</option>
    </select>
    <div class="fieldDescription">\${AllowEmbeddedSubtitlesHelp}</div>
</div>

<div class="checkboxContainer checkboxContainer-withDescription advanced">
    <label>
        <input type="checkbox" is="emby-checkbox" class="chkEnableRealtimeMonitor" checked />
        <span>\${LabelEnableRealtimeMonitor}</span>
    </label>
    <div class="fieldDescription checkboxFieldDescription">\${LabelEnableRealtimeMonitorHelp}</div>
</div>

<div class="checkboxContainer checkboxContainer-withDescription chkEnableLUFSScanContainer advanced">
    <label>
        <input type="checkbox" is="emby-checkbox" class="chkEnableLUFSScan" checked />
        <span>\${LabelEnableLUFSScan}</span>
    </label>
    <div class="fieldDescription checkboxFieldDescription">\${LabelEnableLUFSScanHelp}</div>
</div>

<div class="checkboxContainer checkboxContainer-withDescription chkAutomaticallyAddToCollectionContainer hide advanced">
    <label>
        <input is="emby-checkbox" type="checkbox" id="chkAutomaticallyAddToCollection" />
        <span>\${LabelAutomaticallyAddToCollection}</span>
    </label>
    <div class="fieldDescription checkboxFieldDescription">\${LabelAutomaticallyAddToCollectionHelp}</div>
</div>

<div class="metadataReaders hide advanced" style="margin-bottom: 2em;">
</div>

<div class="metadataFetchers hide" style="margin-bottom: 2em;">
</div>

<div class="selectContainer fldAutoRefreshInterval hide advanced" style="margin: 2em 0;">
    <select is="emby-select" id="selectAutoRefreshInterval" label="\${LabelAutomaticallyRefreshInternetMetadataEvery}"></select>
    <div class="fieldDescription">\${MessageEnablingOptionLongerScans}</div>
</div>

<div class="metadataSavers hide" style="margin-bottom: 2em;">
</div>

<div class="imageFetchers hide advanced" style="margin-bottom: 2em;">
</div>

<div class="similarItemProviders hide advanced" style="margin-bottom: 2em;">
</div>

<div class="checkboxContainer checkboxContainer-withDescription chkSaveLocalContainer hide">
    <label>
        <input is="emby-checkbox" type="checkbox" id="chkSaveLocal" />
        <span>\${LabelSaveLocalMetadata}</span>
    </label>
    <div class="fieldDescription checkboxFieldDescription">\${LabelSaveLocalMetadataHelp}</div>
</div>

<div class="checkboxContainer checkboxContainer-withDescription chkAutomaticallyGroupSeriesContainer hide advanced">
    <label>
        <input type="checkbox" is="emby-checkbox" class="chkAutomaticallyGroupSeries" />
        <span>\${OptionAutomaticallyGroupSeries}</span>
    </label>
    <div class="fieldDescription checkboxFieldDescription">\${OptionAutomaticallyGroupSeriesHelp}</div>
</div>

<div class="mediaSegmentProviders advanced hide" style="margin-bottom: 2em;">
</div>

<div class="trickplaySettingsSection hide">
    <h2>\${Trickplay}</h2>
    <div class="checkboxContainer checkboxContainer-withDescription fldExtractTrickplayImages">
        <label>
            <input type="checkbox" is="emby-checkbox" class="chkExtractTrickplayImages" />
            <span>\${OptionExtractTrickplayImage}</span>
        </label>
        <div class="fieldDescription checkboxFieldDescription">\${ExtractTrickplayImagesHelp}</div>
    </div>

    <div class="checkboxContainer checkboxContainer-withDescription fldExtractTrickplayDuringLibraryScan advanced">
        <label>
            <input type="checkbox" is="emby-checkbox" class="chkExtractTrickplayDuringLibraryScan" />
            <span>\${LabelExtractTrickplayDuringLibraryScan}</span>
        </label>
        <div class="fieldDescription checkboxFieldDescription">\${LabelExtractTrickplayDuringLibraryScanHelp}</div>
    </div>

    <div class="checkboxContainer checkboxContainer-withDescription fldSaveTrickplayLocally advanced">
        <label>
            <input type="checkbox" is="emby-checkbox" class="chkSaveTrickplayLocally" />
            <span>\${LabelSaveTrickplayLocally}</span>
        </label>
        <div class="fieldDescription checkboxFieldDescription">\${LabelSaveTrickplayLocallyHelp}</div>
    </div>
</div>

<div class="chapterSettingsSection hide">
    <h2>\${HeaderChapterImages}</h2>
    <div class="checkboxContainer checkboxContainer-withDescription fldExtractChapterImages">
        <label>
            <input type="checkbox" is="emby-checkbox" class="chkExtractChapterImages" />
            <span>\${OptionExtractChapterImage}</span>
        </label>
        <div class="fieldDescription checkboxFieldDescription">\${ExtractChapterImagesHelp}</div>
    </div>

    <div class="checkboxContainer checkboxContainer-withDescription fldExtractChaptersDuringLibraryScan advanced">
        <label>
            <input type="checkbox" is="emby-checkbox" class="chkExtractChaptersDuringLibraryScan" />
            <span>\${LabelExtractChaptersDuringLibraryScan}</span>
        </label>
        <div class="fieldDescription checkboxFieldDescription">\${LabelExtractChaptersDuringLibraryScanHelp}</div>
    </div>
</div>

<div class="subtitleDownloadSettings hide">
    <h2>\${HeaderSubtitleDownloads}</h2>

    <div>
        <h3 class="checkboxListLabel">\${LabelDownloadLanguages}</h3>
        <div class="subtitleDownloadLanguages paperList checkboxList" style="max-height: 10.5em; overflow-y: auto; padding: .5em 1em;">
        </div>
    </div>
    <br />

    <div class="subtitleFetchers advanced" style="margin-bottom: 2em;">
    </div>

    <div class="checkboxContainer checkboxContainer-withDescription">
        <label>
            <input is="emby-checkbox" type="checkbox" id="chkRequirePerfectMatch" checked />
            <span>\${OptionRequirePerfectSubtitleMatch}</span>
        </label>
        <div class="fieldDescription checkboxFieldDescription">\${OptionRequirePerfectSubtitleMatchHelp}</div>
    </div>
    <div class="checkboxContainer checkboxContainer-withDescription advanced">
        <label>
            <input is="emby-checkbox" type="checkbox" id="chkSkipIfAudioTrackPresent" />
            <span>\${LabelSkipIfAudioTrackPresent}</span>
        </label>
        <div class="fieldDescription checkboxFieldDescription">\${LabelSkipIfAudioTrackPresentHelp}</div>
    </div>
    <div class="checkboxContainer checkboxContainer-withDescription advanced">
        <label>
            <input is="emby-checkbox" type="checkbox" id="chkSkipIfGraphicalSubsPresent" />
            <span>\${LabelSkipIfGraphicalSubsPresent}</span>
        </label>
        <div class="fieldDescription checkboxFieldDescription">\${LabelSkipIfGraphicalSubsPresentHelp}</div>
    </div>

    <div class="checkboxContainer checkboxContainer-withDescription advanced">
        <label>
            <input type="checkbox" is="emby-checkbox" id="chkSaveSubtitlesLocally" checked />
            <span>\${SaveSubtitlesIntoMediaFolders}</span>
        </label>
        <div class="fieldDescription checkboxFieldDescription">\${SaveSubtitlesIntoMediaFoldersHelp}</div>
    </div>
</div>

<div class="lyricSettingsSection hide">
    <h2>\${Lyrics}</h2>

    <div class="lyricFetchers advanced" style="margin-bottom: 2em;">
    </div>

    <div class="checkboxContainer checkboxContainer-withDescription advanced">
        <label>
            <input type="checkbox" is="emby-checkbox" id="chkSaveLyricsLocally" />
            <span>\${SaveLyricsIntoMediaFolders}</span>
        </label>
        <div class="fieldDescription checkboxFieldDescription">\${SaveLyricsIntoMediaFoldersHelp}</div>
    </div>
</div>

<div class="audioTagSettingsSection hide">
    <h2>\${LabelAudioTagSettings}</h2>
    <div class="checkboxContainer checkboxContainer-withDescription advanced">
        <label>
            <input type="checkbox" is="emby-checkbox" id="chkPreferNonstandardArtistsTag" />
            <span>\${PreferNonstandardArtistsTag}</span>
        </label>
        <div class="fieldDescription checkboxFieldDescription">\${PreferNonstandardArtistsTagHelp}</div>
    </div>
    <div class="checkboxContainer checkboxContainer-withDescription advanced">
        <label>
            <input type="checkbox" is="emby-checkbox" id="chkUseCustomTagDelimiters" />
            <span>\${UseCustomTagDelimiters}</span>
        </label>
        <div class="fieldDescription checkboxFieldDescription">\${UseCustomTagDelimitersHelp}</div>
    </div>
    <div class="inputContainer">
        <input type="text" is="emby-input" id="customTagDelimitersInput" label="\${LabelCustomTagDelimiters}" value="/|;\\"/>
        <div class="fieldDescription">\${LabelCustomTagDelimitersHelp}</div>
    </div>
    <div class="inputContainer">
        <textarea is="emby-textarea" id="tagDelimiterWhitelist" label="\${LabelDelimiterWhitelist}" class="textarea-mono" style="resize: none;min-height:2.5em"></textarea>
        <div class="fieldDescription">\${LabelDelimiterWhitelistHelp}</div>
    </div>
</div>
`;function w(e){return ApiClient.getCultures().then(a=>{const t=e.querySelector("#selectLanguage"),i=e.querySelector(".subtitleDownloadLanguages");t&&F(t,a),i&&$(i,a)})}function F(e,a){let t="";t+="<option value=''></option>";for(const i of a)t+=`<option value='${n(i.Name||"")}' data-culture-name='${n(i.Name||"")}'>${n(i.DisplayName||"")}</option>`;e.innerHTML=t}function $(e,a){let t="";for(const i of a)t+=`<label><input type="checkbox" is="emby-checkbox" class="chkSubtitleLanguage" data-lang="${n((i.ThreeLetterISOLanguageName??"").toLowerCase())}" /><span>${n(i.DisplayName||"")}</span></label>`;e.innerHTML=t}function P(e){return ApiClient.getCountries().then(a=>{let t="";t+="<option value=''></option>";for(const i of a)t+=`<option value='${i.TwoLetterISORegionName}'>${i.DisplayName}</option>`;e.innerHTML=t})}function O(e){return e?ApiClient.getJSON(ApiClient.getUrl("Localization/DefaultMetadataLanguage",{countryCode:e})).then(a=>a||"").catch(()=>""):Promise.resolve("")}function k(e){const a=e.querySelector("#selectCountry").value,t=e.querySelector("#selectLanguage");return!a||!t?Promise.resolve(""):t.value?Promise.resolve(t.value):O(a).then(i=>{if(a.toUpperCase()==="BR"){const r=Array.prototype.find.call(t.options,l=>(l.textContent||l.innerText||"").trim()==="Portuguese (Brazil)");if(r)return t.value=r.value,r.value}return i&&(t.value=i),t.value||i})}function N(e){e=e||{};const a=e.MetadataCountryCode||"",t=e.PreferredMetadataLanguage||"";return{Enabled:!0,EnablePhotos:!0,EnableRealtimeMonitor:!0,EnableLUFSScan:!1,EnableChapterImageExtraction:!1,ExtractChapterImagesDuringLibraryScan:!1,EnableTrickplayImageExtraction:!1,ExtractTrickplayImagesDuringLibraryScan:!1,PathInfos:[],SaveLocalMetadata:!1,EnableInternetProviders:!0,EnableAutomaticSeriesGrouping:!0,EnableEmbeddedTitles:!1,EnableEmbeddedExtrasTitles:!1,EnableEmbeddedEpisodeInfos:!1,AutomaticRefreshIntervalDays:0,PreferredMetadataLanguage:t||"",MetadataCountryCode:a,SeasonZeroDisplayName:"Specials",MetadataSavers:[],DisabledLocalMetadataReaders:[],LocalMetadataReaderOrder:[],DisabledSubtitleFetchers:[],SubtitleFetcherOrder:[],DisabledMediaSegmentProviders:[],MediaSegmentProviderOrder:[],SkipSubtitlesIfEmbeddedSubtitlesPresent:!1,SkipSubtitlesIfAudioTrackMatches:!0,SubtitleDownloadLanguages:[],RequirePerfectSubtitleMatch:!0,SaveSubtitlesWithMedia:!0,SaveLyricsWithMedia:!1,SaveTrickplayWithMedia:!1,DisabledLyricFetchers:[],LyricFetcherOrder:[],PreferNonstandardArtistsTag:!1,UseCustomTagDelimiters:!1,CustomTagDelimiters:["/","|",";","\\"],DelimiterWhitelist:[],AutomaticallyAddToCollection:!1,AllowEmbeddedSubtitles:"AllowAll",TypeOptions:[]}}function H(e){let a="";a+=`<option value='0'>${c.translate("Never")}</option>`,a+=[30,60,90].map(t=>`<option value='${t}'>${c.translate("EveryNDays",String(t))}</option>`).join(""),e.innerHTML=a}function g(e,a){let t="";const i=e.querySelector(".metadataReaders");if(!i)return!1;if(a.length<1)return i.innerHTML="",i.classList.add("hide"),!1;t+=`<h3 class="checkboxListLabel">${c.translate("LabelMetadataReaders")}</h3>`,t+='<div class="checkboxList paperList checkboxList-paperList">';for(let r=0;r<a.length;r++){const l=a[r];t+=`<div class="listItem localReaderOption sortableOption" data-pluginname="${n(l.Name)}">`,t+='<span class="listItemIcon material-icons live_tv" aria-hidden="true"></span>',t+='<div class="listItemBody">',t+='<h3 class="listItemBodyText">',t+=n(l.Name),t+="</h3>",t+="</div>",r>0?t+=`<button type="button" is="paper-icon-button-light" title="${c.translate("Up")}" class="btnSortableMoveUp btnSortable" data-pluginindex="${r}"><span class="material-icons keyboard_arrow_up" aria-hidden="true"></span></button>`:a.length>1&&(t+=`<button type="button" is="paper-icon-button-light" title="${c.translate("Down")}" class="btnSortableMoveDown btnSortable" data-pluginindex="${r}"><span class="material-icons keyboard_arrow_down" aria-hidden="true"></span></button>`),t+="</div>"}return t+="</div>",t+=`<div class="fieldDescription">${c.translate("LabelMetadataReadersHelp")}</div>`,a.length<2?i.classList.add("hide"):i.classList.remove("hide"),i.innerHTML=t,!0}function _(e,a){let t="";const i=e.querySelector(".metadataSavers");if(!i)return!1;if(!a.length)return i.innerHTML="",i.classList.add("hide"),!1;t+=`<h3 class="checkboxListLabel">${c.translate("LabelMetadataSavers")}</h3>`,t+='<div class="checkboxList paperList checkboxList-paperList">';for(const r of a)t+=`<label><input type="checkbox" data-defaultenabled="${r.DefaultEnabled}" is="emby-checkbox" class="chkMetadataSaver" data-pluginname="${n(r.Name)}" false><span>${n(r.Name)}</span></label>`;return t+="</div>",t+=`<div class="fieldDescription" style="margin-top:.25em;">${c.translate("LabelMetadataSaversHelp")}</div>`,i.innerHTML=t,i.classList.remove("hide"),!0}function R(e,a){let t="",i=e.MetadataFetchers??[];return i=h(i,a.MetadataFetcherOrder??[]),i.length&&(t+='<div class="metadataFetcher" data-type="'+e.Type+'">',t+='<h3 class="checkboxListLabel">'+c.translate("LabelTypeMetadataDownloaders",c.translate("TypeOptionPlural"+(e.Type??"")))+"</h3>",t+='<div class="checkboxList paperList checkboxList-paperList">',i.forEach((r,l)=>{t+='<div class="listItem metadataFetcherItem sortableOption" data-pluginname="'+n(r.Name)+'">';const o=(a.MetadataFetchers?a.MetadataFetchers.includes(r.Name??""):r.DefaultEnabled)?' checked="checked"':"";t+='<label class="listItemCheckboxContainer"><input type="checkbox" is="emby-checkbox" class="chkMetadataFetcher" data-pluginname="'+n(r.Name)+'" '+o+"><span></span></label>",t+='<div class="listItemBody">',t+='<h3 class="listItemBodyText">',t+=n(r.Name),t+="</h3>",t+="</div>",l>0?t+='<button type="button" is="paper-icon-button-light" title="'+c.translate("Up")+'" class="btnSortableMoveUp btnSortable" data-pluginindex="'+l+'"><span class="material-icons keyboard_arrow_up" aria-hidden="true"></span></button>':i.length>1&&(t+='<button type="button" is="paper-icon-button-light" title="'+c.translate("Down")+'" class="btnSortableMoveDown btnSortable" data-pluginindex="'+l+'"><span class="material-icons keyboard_arrow_down" aria-hidden="true"></span></button>'),t+="</div>"}),t+="</div>",t+='<div class="fieldDescription">'+c.translate("LabelMetadataDownloadersHelp")+"</div>",t+="</div>"),t}function d(e,a){const t=e.TypeOptions||[];for(const i of t)if(i.Type===a)return i;return null}function f(e,a,t){let i="";const r=e.querySelector(".metadataFetchers");if(!r)return!0;for(const l of a.TypeOptions??[])i+=R(l,d(t,l.Type)||{});return r.innerHTML=i,i?(r.classList.remove("hide"),e.querySelector(".fldAutoRefreshInterval").classList.remove("hide"),e.querySelector(".fldMetadataLanguage").classList.remove("hide"),e.querySelector(".fldMetadataCountry").classList.remove("hide")):(r.classList.add("hide"),e.querySelector(".fldAutoRefreshInterval").classList.add("hide"),e.querySelector(".fldMetadataLanguage").classList.add("hide"),e.querySelector(".fldMetadataCountry").classList.add("hide")),!0}function L(e,a,t){let i="";const r=e.querySelector(".subtitleFetchers");if(!r)return i;let l=a.SubtitleFetchers??[];if(l=h(l,t.SubtitleFetcherOrder??[]),!l.length)return i;i+=`<h3 class="checkboxListLabel">${c.translate("LabelSubtitleDownloaders")}</h3>`,i+='<div class="checkboxList paperList checkboxList-paperList">';for(let s=0;s<l.length;s++){const o=l[s];i+=`<div class="listItem subtitleFetcherItem sortableOption" data-pluginname="${n(o.Name)}">`;const b=(t.DisabledSubtitleFetchers?!t.DisabledSubtitleFetchers.includes(o.Name??""):o.DefaultEnabled)?' checked="checked"':"";i+=`<label class="listItemCheckboxContainer"><input type="checkbox" is="emby-checkbox" class="chkSubtitleFetcher" data-pluginname="${n(o.Name)}" ${b}><span></span></label>`,i+='<div class="listItemBody">',i+='<h3 class="listItemBodyText">',i+=n(o.Name),i+="</h3>",i+="</div>",s>0?i+=`<button type="button" is="paper-icon-button-light" title="${c.translate("Up")}" class="btnSortableMoveUp btnSortable" data-pluginindex="${s}"><span class="material-icons keyboard_arrow_up" aria-hidden="true"></span></button>`:l.length>1&&(i+=`<button type="button" is="paper-icon-button-light" title="${c.translate("Down")}" class="btnSortableMoveDown btnSortable" data-pluginindex="${s}"><span class="material-icons keyboard_arrow_down" aria-hidden="true"></span></button>`),i+="</div>"}i+="</div>",i+=`<div class="fieldDescription">${c.translate("SubtitleDownloadersHelp")}</div>`,r.innerHTML=i}function x(e,a,t){let i="";const r=e.querySelector(".lyricFetchers");if(!r)return i;let l=a.LyricFetchers??[];if(l=h(l,t.LyricFetcherOrder),!l.length)return i;i+=`<h3 class="checkboxListLabel">${c.translate("LabelLyricDownloaders")}</h3>`,i+='<div class="checkboxList paperList checkboxList-paperList">';for(let s=0;s<l.length;s++){const o=l[s];i+=`<div class="listItem lyricFetcherItem sortableOption" data-pluginname="${n(o.Name)}">`;const b=(t.DisabledLyricFetchers?!t.DisabledLyricFetchers.includes(o.Name??""):o.DefaultEnabled)?' checked="checked"':"";i+=`<label class="listItemCheckboxContainer"><input type="checkbox" is="emby-checkbox" class="chkLyricFetcher" data-pluginname="${n(o.Name)}" ${b}><span></span></label>`,i+='<div class="listItemBody">',i+='<h3 class="listItemBodyText">',i+=n(o.Name),i+="</h3>",i+="</div>",s>0?i+=`<button type="button" is="paper-icon-button-light" title="${c.translate("Up")}" class="btnSortableMoveUp btnSortable" data-pluginindex="${s}"><span class="material-icons keyboard_arrow_up" aria-hidden="true"></span></button>`:l.length>1&&(i+=`<button type="button" is="paper-icon-button-light" title="${c.translate("Down")}" class="btnSortableMoveDown btnSortable" data-pluginindex="${s}"><span class="material-icons keyboard_arrow_down" aria-hidden="true"></span></button>`),i+="</div>"}i+="</div>",i+=`<div class="fieldDescription">${c.translate("LyricDownloadersHelp")}</div>`,r.innerHTML=i}function E(e,a,t){let i="";const r=e.querySelector(".mediaSegmentProviders");if(!r)return i;let l=a.MediaSegmentProviders??[];if(l=h(l,t.MediaSegmentProviderOrder),r.classList.toggle("hide",!l.length),!l.length)return i;i+=`<h3 class="checkboxListLabel">${c.translate("LabelMediaSegmentProviders")}</h3>`,i+='<div class="checkboxList paperList checkboxList-paperList">';for(let s=0;s<l.length;s++){const o=l[s];i+=`<div class="listItem mediaSegmentProviderItem sortableOption" data-pluginname="${n(o.Name)}">`;const b=(t.DisabledMediaSegmentProviders?!t.DisabledMediaSegmentProviders.includes(o.Name??""):o.DefaultEnabled)?' checked="checked"':"";i+=`<label class="listItemCheckboxContainer"><input type="checkbox" is="emby-checkbox" class="chkMediaSegmentProvider" data-pluginname="${n(o.Name)}" ${b}><span></span></label>`,i+='<div class="listItemBody">',i+='<h3 class="listItemBodyText">',i+=n(o.Name),i+="</h3>",i+="</div>",s>0?i+=`<button type="button" is="paper-icon-button-light" title="${c.translate("Up")}" class="btnSortableMoveUp btnSortable" data-pluginindex="${s}"><span class="material-icons keyboard_arrow_up" aria-hidden="true"></span></button>`:l.length>1&&(i+=`<button type="button" is="paper-icon-button-light" title="${c.translate("Down")}" class="btnSortableMoveDown btnSortable" data-pluginindex="${s}"><span class="material-icons keyboard_arrow_down" aria-hidden="true"></span></button>`),i+="</div>"}i+="</div>",i+=`<div class="fieldDescription">${c.translate("MediaSegmentProvidersHelp")}</div>`,r.innerHTML=i}function U(e,a){let t="",i=e.ImageFetchers??[];if(i=h(i,a.ImageFetcherOrder??[]),!i.length)return t;t+='<div class="imageFetcher" data-type="'+e.Type+'">',t+='<div class="flex align-items-center" style="margin:1.5em 0 .5em;">',t+='<h3 class="checkboxListLabel" style="margin:0;">'+c.translate("HeaderTypeImageFetchers",c.translate("TypeOptionPlural"+(e.Type??"")))+"</h3>";const r=e.SupportedImageTypes||[];(r.length>1||r.length===1&&r[0]!=="Primary")&&(t+='<button is="emby-button" class="raised btnImageOptionsForType" type="button" style="font-size:90%;"><span>'+c.translate("HeaderFetcherSettings")+"</span></button>"),t+="</div>",t+='<div class="checkboxList paperList checkboxList-paperList">';for(let l=0;l<i.length;l++){const s=i[l];t+='<div class="listItem imageFetcherItem sortableOption" data-pluginname="'+n(s.Name)+'">';const v=(a.ImageFetchers?a.ImageFetchers.includes(s.Name??""):s.DefaultEnabled)?' checked="checked"':"";t+='<label class="listItemCheckboxContainer"><input type="checkbox" is="emby-checkbox" class="chkImageFetcher" data-pluginname="'+n(s.Name)+'" '+v+"><span></span></label>",t+='<div class="listItemBody">',t+='<h3 class="listItemBodyText">',t+=n(s.Name),t+="</h3>",t+="</div>",l>0?t+='<button type="button" is="paper-icon-button-light" title="'+c.translate("Up")+'" class="btnSortableMoveUp btnSortable" data-pluginindex="'+l+'"><span class="material-icons keyboard_arrow_up" aria-hidden="true"></span></button>':i.length>1&&(t+='<button type="button" is="paper-icon-button-light" title="'+c.translate("Down")+'" class="btnSortableMoveDown btnSortable" data-pluginindex="'+l+'"><span class="material-icons keyboard_arrow_down" aria-hidden="true"></span></button>'),t+="</div>"}return t+="</div>",t+='<div class="fieldDescription">'+c.translate("LabelImageFetchersHelp")+"</div>",t+="</div>",t}function W(e,a){let t="",i=e.SimilarItemProviders??[];return i=h(i,a.SimilarItemProviderOrder??[]),i.length&&(t+='<div class="similarItemProvider" data-type="'+e.Type+'">',t+='<h3 class="checkboxListLabel">'+c.translate("HeaderTypeSimilarItemProviders",c.translate("TypeOptionPlural"+(e.Type??"")))+"</h3>",t+='<div class="checkboxList paperList checkboxList-paperList">',i.forEach((r,l)=>{t+='<div class="listItem similarItemProviderItem sortableOption" data-pluginname="'+n(r.Name)+'">';const o=(a.SimilarItemProviders?a.SimilarItemProviders.includes(r.Name??""):r.DefaultEnabled)?' checked="checked"':"";t+='<label class="listItemCheckboxContainer"><input type="checkbox" is="emby-checkbox" class="chkSimilarItemProvider" data-pluginname="'+n(r.Name)+'" '+o+"><span></span></label>",t+='<div class="listItemBody">',t+='<h3 class="listItemBodyText">',t+=n(r.Name),t+="</h3>",t+="</div>",l>0?t+='<button type="button" is="paper-icon-button-light" title="'+c.translate("Up")+'" class="btnSortableMoveUp btnSortable" data-pluginindex="'+l+'"><span class="material-icons keyboard_arrow_up" aria-hidden="true"></span></button>':i.length>1&&(t+='<button type="button" is="paper-icon-button-light" title="'+c.translate("Down")+'" class="btnSortableMoveDown btnSortable" data-pluginindex="'+l+'"><span class="material-icons keyboard_arrow_down" aria-hidden="true"></span></button>'),t+="</div>"}),t+="</div>",t+='<div class="fieldDescription">'+c.translate("LabelSimilarItemProvidersHelp")+"</div>",t+="</div>"),t}function C(e,a,t){let i="";const r=e.querySelector(".imageFetchers");if(!r)return!0;for(const l of a.TypeOptions??[])i+=U(l,d(t,l.Type)||{});return r.innerHTML=i,i?(r.classList.remove("hide"),e.querySelector(".chkSaveLocalContainer").classList.remove("hide")):(r.classList.add("hide"),e.querySelector(".chkSaveLocalContainer").classList.add("hide")),!0}function D(e,a,t){let i="";const r=e.querySelector(".similarItemProviders");if(!r)return!0;for(const l of a.TypeOptions??[])i+=W(l,d(t,l.Type)||{});return r.innerHTML=i,i?r.classList.remove("hide"):r.classList.add("hide"),!0}function B(e,a){const t=e.classList.contains("newlibrary");return ApiClient.getJSON(ApiClient.getUrl("Libraries/AvailableOptions",{LibraryContentType:a,IsNewLibrary:t})).then(i=>{y=i,e.availableOptions=i,_(e,i.MetadataSavers),g(e,i.MetadataReaders),f(e,i,{}),L(e,i,{}),x(e,i,{}),E(e,i,{}),C(e,i,{}),D(e,i,{}),(i.SubtitleFetchers??[]).length?e.querySelector(".subtitleDownloadSettings").classList.remove("hide"):e.querySelector(".subtitleDownloadSettings").classList.add("hide")}).catch(()=>Promise.resolve())}function G(e){const a=e.querySelector(".btnSortable");if(!a)return;const t=a.querySelector(".material-icons");t&&(e.previousSibling?(a.title=c.translate("Up"),a.classList.add("btnSortableMoveUp"),a.classList.remove("btnSortableMoveDown"),t.classList.remove("keyboard_arrow_down"),t.classList.add("keyboard_arrow_up")):(a.title=c.translate("Down"),a.classList.remove("btnSortableMoveUp"),a.classList.add("btnSortableMoveDown"),t.classList.remove("keyboard_arrow_up"),t.classList.add("keyboard_arrow_down")))}function Z(e){q(async()=>{const{default:a}=await import("./imageOptionsEditor-CgrWwRoV.js");return{default:a}},__vite__mapDeps([0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16]),import.meta.url).then(({default:a})=>{let t=d(p??{TypeOptions:[]},e);t||(t={Type:e},(p.TypeOptions??=[]).push(t));const i=y?d(y,e):null;new a().show(e,t,i)}).catch(()=>{})}function j(e){const a=m.parentWithClass(e.target,"btnImageOptionsForType");if(a){const t=m.parentWithClass(a,"imageFetcher");if(!t)return;Z(t.getAttribute("data-type")??"");return}u.call(this,e)}function u(e){const a=m.parentWithClass(e.target,"btnSortable");if(a){const t=m.parentWithClass(a,"sortableOption");if(!t)return;const i=m.parentWithClass(t,"paperList");if(!i)return;if(a.classList.contains("btnSortableMoveDown")){const r=t.nextSibling;if(r){const l=t.parentNode,s=r.parentNode;if(!l||!s)return;l.removeChild(t),s.insertBefore(t,r.nextSibling)}}else{const r=t.previousSibling;if(r){const l=t.parentNode,s=r.parentNode;if(!l||!s)return;l.removeChild(t),s.insertBefore(t,r)}}Array.prototype.forEach.call(i.querySelectorAll(".sortableOption"),G)}}function z(e){e.querySelector(".metadataReaders").addEventListener("click",u),e.querySelector(".subtitleFetchers").addEventListener("click",u),e.querySelector(".metadataFetchers").addEventListener("click",u),e.querySelector(".lyricFetchers").addEventListener("click",u),e.querySelector(".mediaSegmentProviders").addEventListener("click",u),e.querySelector(".imageFetchers").addEventListener("click",j),e.querySelector(".similarItemProviders").addEventListener("click",u),e.querySelector("#selectCountry").addEventListener("change",()=>{k(e)}),e.querySelector("#chkEnableEmbeddedTitles").addEventListener("change",a=>{e.querySelector(".chkEnableEmbeddedExtrasTitlesContainer").classList.toggle("hide",!a.currentTarget.checked)})}async function J(e,a,t){p={TypeOptions:[]},y=null;const i=t==null;i&&e.classList.add("newlibrary"),e.innerHTML=c.translateHtml(M),H(e.querySelector("#selectAutoRefreshInterval"));const r=[w(e),P(e.querySelector("#selectCountry"))];i&&r.push(ApiClient.getJSON(ApiClient.getUrl("Startup/Configuration")).catch(l=>(console.warn("[libraryoptionseditor] Failed to fetch Startup/Configuration:",l),null))),Promise.all(r).then(function(l){const s=i?l[2]||{}:null;return I(e,a).then(function(){A(e,t||N(s)),z(e)})}).catch(()=>{})}const K=[S.Homevideos,S.Movies,S.Musicvideos,S.Tvshows];function I(e,a){a==="homevideos"||a==="photos"?e.querySelector(".chkEnablePhotosContainer").classList.remove("hide"):e.querySelector(".chkEnablePhotosContainer").classList.add("hide");const t=!a||K.includes(a??"");return e.querySelector(".trickplaySettingsSection").classList.toggle("hide",!t),e.querySelector(".chapterSettingsSection").classList.toggle("hide",!t),a==="tvshows"?(e.querySelector(".chkAutomaticallyGroupSeriesContainer").classList.remove("hide"),e.querySelector(".fldSeasonZeroDisplayName").classList.remove("hide"),e.querySelector("#txtSeasonZeroName").setAttribute("required","required")):(e.querySelector(".chkAutomaticallyGroupSeriesContainer").classList.add("hide"),e.querySelector(".fldSeasonZeroDisplayName").classList.add("hide"),e.querySelector("#txtSeasonZeroName").removeAttribute("required")),a==="books"||a==="boxsets"||a==="playlists"||a==="music"?(e.querySelector(".chkEnableEmbeddedTitlesContainer").classList.add("hide"),e.querySelector(".chkEnableEmbeddedExtrasTitlesContainer").classList.add("hide")):(e.querySelector(".chkEnableEmbeddedTitlesContainer").classList.remove("hide"),e.querySelector("#chkEnableEmbeddedTitles").checked&&e.querySelector(".chkEnableEmbeddedExtrasTitlesContainer").classList.remove("hide")),e.querySelector(".chkEnableLUFSScanContainer").classList.toggle("hide",a!=="music"),a==="tvshows"?e.querySelector(".chkEnableEmbeddedEpisodeInfosContainer").classList.remove("hide"):e.querySelector(".chkEnableEmbeddedEpisodeInfosContainer").classList.add("hide"),a==="tvshows"||a==="movies"||a==="musicvideos"||a==="mixed"?e.querySelector(".fldAllowEmbeddedSubtitlesContainer").classList.remove("hide"):e.querySelector(".fldAllowEmbeddedSubtitlesContainer").classList.add("hide"),a==="music"?(e.querySelector(".lyricSettingsSection").classList.remove("hide"),e.querySelector(".audioTagSettingsSection").classList.remove("hide")):(e.querySelector(".lyricSettingsSection").classList.add("hide"),e.querySelector(".audioTagSettingsSection").classList.add("hide")),e.querySelector(".chkAutomaticallyAddToCollectionContainer").classList.toggle("hide",a!=="movies"&&a!=="mixed"),B(e,a??"")}function V(e,a){a.DisabledSubtitleFetchers=Array.prototype.map.call(Array.prototype.filter.call(e.querySelectorAll(".chkSubtitleFetcher"),t=>!t.checked),t=>t.getAttribute("data-pluginname")),a.SubtitleFetcherOrder=Array.prototype.map.call(e.querySelectorAll(".subtitleFetcherItem"),t=>t.getAttribute("data-pluginname"))}function Y(e,a){a.DisabledLyricFetchers=Array.prototype.map.call(Array.prototype.filter.call(e.querySelectorAll(".chkLyricFetcher"),t=>!t.checked),t=>t.getAttribute("data-pluginname")),a.LyricFetcherOrder=Array.prototype.map.call(e.querySelectorAll(".lyricFetcherItem"),t=>t.getAttribute("data-pluginname"))}function Q(e,a){a.DisabledMediaSegmentProviders=Array.prototype.map.call(Array.prototype.filter.call(e.querySelectorAll(".chkMediaSegmentProvider"),t=>!t.checked),t=>t.getAttribute("data-pluginname")),a.MediaSegmentProviderOrder=Array.prototype.map.call(e.querySelectorAll(".mediaSegmentProviderItem"),t=>t.getAttribute("data-pluginname"))}function X(e,a){const t=e.querySelectorAll(".metadataFetcher");for(const i of t){const r=i.getAttribute("data-type");let l=d(a,r);l||(l={Type:r},(a.TypeOptions??=[]).push(l)),l.MetadataFetchers=Array.prototype.map.call(Array.prototype.filter.call(i.querySelectorAll(".chkMetadataFetcher"),s=>s.checked),s=>s.getAttribute("data-pluginname")),l.MetadataFetcherOrder=Array.prototype.map.call(i.querySelectorAll(".metadataFetcherItem"),s=>s.getAttribute("data-pluginname"))}}function ee(e,a){const t=e.querySelectorAll(".imageFetcher");for(const i of t){const r=i.getAttribute("data-type");let l=d(a,r);l||(l={Type:r},(a.TypeOptions??=[]).push(l)),l.ImageFetchers=Array.prototype.map.call(Array.prototype.filter.call(i.querySelectorAll(".chkImageFetcher"),s=>s.checked),s=>s.getAttribute("data-pluginname")),l.ImageFetcherOrder=Array.prototype.map.call(i.querySelectorAll(".imageFetcherItem"),s=>s.getAttribute("data-pluginname"))}}function te(e,a){const t=e.querySelectorAll(".similarItemProvider");for(const i of t){const r=i.getAttribute("data-type");let l=d(a,r);l||(l={Type:r},(a.TypeOptions??=[]).push(l)),l.SimilarItemProviders=Array.prototype.map.call(Array.prototype.filter.call(i.querySelectorAll(".chkSimilarItemProvider"),s=>s.checked),s=>s.getAttribute("data-pluginname")),l.SimilarItemProviderOrder=Array.prototype.map.call(i.querySelectorAll(".similarItemProviderItem"),s=>s.getAttribute("data-pluginname"))}}function ae(e){const a=p?.TypeOptions||[];for(const t of a){let i=d(e,t.Type);i||(i={Type:t.Type},(e.TypeOptions??=[]).push(i)),t.ImageOptions&&(i.ImageOptions=t.ImageOptions)}}function ie(e){const a={Enabled:e.querySelector(".chkEnabled").checked,EnableArchiveMediaFiles:!1,EnablePhotos:e.querySelector(".chkEnablePhotos").checked,EnableRealtimeMonitor:!0,EnableLUFSScan:e.querySelector(".chkEnableLUFSScan").checked,ExtractTrickplayImagesDuringLibraryScan:e.querySelector(".chkExtractTrickplayDuringLibraryScan").checked,SaveTrickplayWithMedia:e.querySelector(".chkSaveTrickplayLocally").checked,EnableTrickplayImageExtraction:e.querySelector(".chkExtractTrickplayImages").checked,ExtractChapterImagesDuringLibraryScan:e.querySelector(".chkExtractChaptersDuringLibraryScan").checked,EnableChapterImageExtraction:e.querySelector(".chkExtractChapterImages").checked,EnableInternetProviders:!0,SaveLocalMetadata:e.querySelector("#chkSaveLocal").checked,EnableAutomaticSeriesGrouping:e.querySelector(".chkAutomaticallyGroupSeries").checked,PreferredMetadataLanguage:e.querySelector("#selectLanguage").value,MetadataCountryCode:e.querySelector("#selectCountry").value,SeasonZeroDisplayName:e.querySelector("#txtSeasonZeroName").value,AutomaticRefreshIntervalDays:parseInt(e.querySelector("#selectAutoRefreshInterval").value,10),EnableEmbeddedTitles:e.querySelector("#chkEnableEmbeddedTitles").checked,EnableEmbeddedExtrasTitles:e.querySelector("#chkEnableEmbeddedExtrasTitles").checked,EnableEmbeddedEpisodeInfos:e.querySelector("#chkEnableEmbeddedEpisodeInfos").checked,AllowEmbeddedSubtitles:e.querySelector("#selectAllowEmbeddedSubtitles").value,SkipSubtitlesIfEmbeddedSubtitlesPresent:e.querySelector("#chkSkipIfGraphicalSubsPresent").checked,SkipSubtitlesIfAudioTrackMatches:e.querySelector("#chkSkipIfAudioTrackPresent").checked,SaveSubtitlesWithMedia:e.querySelector("#chkSaveSubtitlesLocally").checked,SaveLyricsWithMedia:e.querySelector("#chkSaveLyricsLocally").checked,RequirePerfectSubtitleMatch:e.querySelector("#chkRequirePerfectMatch").checked,AutomaticallyAddToCollection:e.querySelector("#chkAutomaticallyAddToCollection").checked,PreferNonstandardArtistsTag:e.querySelector("#chkPreferNonstandardArtistsTag").checked,UseCustomTagDelimiters:e.querySelector("#chkUseCustomTagDelimiters").checked,MetadataSavers:Array.prototype.map.call(Array.prototype.filter.call(e.querySelectorAll(".chkMetadataSaver"),t=>t.checked),t=>t.getAttribute("data-pluginname")),TypeOptions:[]};return a.LocalMetadataReaderOrder=Array.prototype.map.call(e.querySelectorAll(".localReaderOption"),t=>t.getAttribute("data-pluginname")),a.SubtitleDownloadLanguages=Array.prototype.map.call(Array.prototype.filter.call(e.querySelectorAll(".chkSubtitleLanguage"),t=>t.checked),t=>t.getAttribute("data-lang")),a.CustomTagDelimiters=e.querySelector("#customTagDelimitersInput").value.split(""),a.DelimiterWhitelist=e.querySelector("#tagDelimiterWhitelist").value.split(`
`).filter(t=>t.trim()),V(e,a),Y(e,a),Q(e,a),X(e,a),ee(e,a),te(e,a),ae(a),a}function h(e=[],a=[]){return e=e.slice(0),e.sort((t,i)=>(t=a.indexOf(t.Name),i=a.indexOf(i.Name),t-i)),e}function A(e,a){p=a,y=e.availableOptions??null,e.querySelector("#selectLanguage").value=a.PreferredMetadataLanguage||"",e.querySelector("#selectCountry").value=a.MetadataCountryCode||"",e.querySelector("#selectAutoRefreshInterval").value=String(a.AutomaticRefreshIntervalDays??0),e.querySelector("#txtSeasonZeroName").value=a.SeasonZeroDisplayName||"Specials",e.querySelector(".chkEnabled").checked=a.Enabled??!1,e.querySelector(".chkEnablePhotos").checked=a.EnablePhotos??!1,e.querySelector(".chkEnableRealtimeMonitor").checked=!0,e.querySelector(".chkEnableRealtimeMonitor").disabled=!0,e.querySelector(".chkEnableLUFSScan").checked=a.EnableLUFSScan??!1,e.querySelector(".chkExtractTrickplayDuringLibraryScan").checked=a.ExtractTrickplayImagesDuringLibraryScan??!1,e.querySelector(".chkExtractTrickplayImages").checked=a.EnableTrickplayImageExtraction??!1,e.querySelector(".chkSaveTrickplayLocally").checked=a.SaveTrickplayWithMedia??!1,e.querySelector(".chkExtractChaptersDuringLibraryScan").checked=a.ExtractChapterImagesDuringLibraryScan??!1,e.querySelector(".chkExtractChapterImages").checked=a.EnableChapterImageExtraction??!1,e.querySelector("#chkSaveLocal").checked=a.SaveLocalMetadata??!1,e.querySelector(".chkAutomaticallyGroupSeries").checked=a.EnableAutomaticSeriesGrouping??!1,e.querySelector("#chkEnableEmbeddedTitles").checked=a.EnableEmbeddedTitles??!1,e.querySelector(".chkEnableEmbeddedExtrasTitlesContainer").classList.toggle("hide",!a.EnableEmbeddedTitles),e.querySelector("#chkEnableEmbeddedExtrasTitles").checked=a.EnableEmbeddedExtrasTitles??!1,e.querySelector("#chkEnableEmbeddedEpisodeInfos").checked=a.EnableEmbeddedEpisodeInfos??!1,e.querySelector("#selectAllowEmbeddedSubtitles").value=a.AllowEmbeddedSubtitles??"",e.querySelector("#chkSkipIfGraphicalSubsPresent").checked=a.SkipSubtitlesIfEmbeddedSubtitlesPresent??!1,e.querySelector("#chkSaveSubtitlesLocally").checked=a.SaveSubtitlesWithMedia??!1,e.querySelector("#chkSaveLyricsLocally").checked=a.SaveLyricsWithMedia??!1,e.querySelector("#chkSkipIfAudioTrackPresent").checked=a.SkipSubtitlesIfAudioTrackMatches??!1,e.querySelector("#chkRequirePerfectMatch").checked=a.RequirePerfectSubtitleMatch??!1,e.querySelector("#chkAutomaticallyAddToCollection").checked=a.AutomaticallyAddToCollection??!1,e.querySelector("#chkPreferNonstandardArtistsTag").checked=a.PreferNonstandardArtistsTag??!1,e.querySelector("#chkUseCustomTagDelimiters").checked=a.UseCustomTagDelimiters??!1,Array.prototype.forEach.call(e.querySelectorAll(".chkMetadataSaver"),i=>{i.checked=a.MetadataSavers?a.MetadataSavers.includes(i.getAttribute("data-pluginname")):i.getAttribute("data-defaultenabled")==="true"}),Array.prototype.forEach.call(e.querySelectorAll(".chkSubtitleLanguage"),i=>{i.checked=!!a.SubtitleDownloadLanguages&&a.SubtitleDownloadLanguages.includes(i.getAttribute("data-lang"))}),e.querySelector("#customTagDelimitersInput").value=a.CustomTagDelimiters?.join("")??"",e.querySelector("#tagDelimiterWhitelist").value=a.DelimiterWhitelist?.filter(i=>i.trim()).join(`
`)??"";const t=e.availableOptions;t&&(g(e,h(t.MetadataReaders,a.LocalMetadataReaderOrder||[])),f(e,t,a),C(e,t,a),D(e,t,a),L(e,t,a),x(e,t,a),E(e,t,a)),a.MetadataCountryCode&&k(e)}let p,y;const ue={embed:J,setContentType:I,getLibraryOptions:ie,setLibraryOptions:A},T=Object.create(HTMLInputElement.prototype);function le(e){e.key==="Enter"&&(e.preventDefault(),this.checked=!this.checked,this.dispatchEvent(new CustomEvent("change",{bubbles:!0})))}T.attachedCallback=function(){if(this.getAttribute("data-embytoggle")==="true")return;this.setAttribute("data-embytoggle","true"),this.classList.add("mdl-switch__input");const e=this.parentNode;e.classList.add("mdl-switch"),e.classList.add("mdl-js-switch");const a=e.querySelector("span");e.insertAdjacentHTML("beforeend",'<div class="mdl-switch__trackContainer"><div class="mdl-switch__track"></div><div class="mdl-switch__thumb"><span class="mdl-switch__focus-helper"></span></div></div>'),a.classList.add("toggleButtonLabel"),a.classList.add("mdl-switch__label"),this.addEventListener("keydown",le)};document.registerElement("emby-toggle",{prototype:T,extends:"input"});export{ue as l};
