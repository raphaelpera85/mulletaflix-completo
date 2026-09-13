import{s as v,S,D as b,w as y,A as h,l as f,h as u,i as $,J as r,n as C,E as L,av as q,aX as T,aD as P}from"./index-CFwqrzSZ.js";import{s as g}from"./subtitleappearancehelper-TXgtvbbf.js";/* empty css                 */import"./emby-select-BQevfB2a.js";import"./emby-slider-D1t-UXZr.js";import"./emby-checkbox-CDwDlGOj.js";import"./vendor-react-query-BiAmjtRH.js";import"./vendor-react-CeUY3tRn.js";import"./vendor-jellyfin-Bee54tkY.js";import"./vendor-axios-BCn5QfZZ.js";import"./vendor-lodash-CYyrkkBC.js";import"./vendor-date-fns-CwLI6ukS.js";import"./vendor-dompurify-Baz99PXY.js";import"./actionSheet-BM-G2WQD.js";function D(e,t){let i="";i+="<option value=''>"+v.translate("AnyLanguage")+"</option>";for(let n=0,l=t.length;n<l;n++){const s=t[n];i+="<option value='"+s.ThreeLetterISOLanguageName+"'>"+s.DisplayName+"</option>"}e.innerHTML=i}const k={populateLanguages:D},A=`<form style="margin:0 auto;">
    <div class="verticalSection">
        <h2 class="sectionTitle">
            \${Subtitles}
        </h2>

        <div class="selectContainer">
            <select is="emby-select" id="selectSubtitleLanguage" label="\${LabelPreferredSubtitleLanguage}"></select>
        </div>

        <div class="selectContainer">
            <select is="emby-select" id="selectSubtitlePlaybackMode" label="\${LabelSubtitlePlaybackMode}">
                <option value="Default">\${Default}</option>
                <option value="Smart">\${Smart}</option>
                <option value="OnlyForced">\${OnlyForcedSubtitles}</option>
                <option value="Always">\${AlwaysPlaySubtitles}</option>
                <option value="None">\${None}</option>
            </select>
            <div class="fieldDescription subtitlesDefaultHelp subtitlesHelp hide">\${DefaultSubtitlesHelp}</div>
            <div class="fieldDescription subtitlesSmartHelp subtitlesHelp hide">\${SmartSubtitlesHelp}</div>
            <div class="fieldDescription subtitlesAlwaysHelp subtitlesHelp hide">\${AlwaysPlaySubtitlesHelp}</div>
            <div class="fieldDescription subtitlesOnlyForcedHelp subtitlesHelp hide">\${OnlyForcedSubtitlesHelp}</div>
            <div class="fieldDescription subtitlesNoneHelp subtitlesHelp hide">\${NoSubtitlesHelp}</div>
        </div>

        <div class="selectContainer fldBurnIn hide">
            <select is="emby-select" id="selectSubtitleBurnIn" label="\${LabelBurnSubtitles}">
                <option value="">\${Auto}</option>
                <option value="onlyimageformats">\${OnlyImageFormats}</option>
                <option value="allcomplexformats">\${AllComplexFormats}</option>
                <option value="all">\${All}</option>
            </select>
            <div class="fieldDescription">\${BurnSubtitlesHelp}</div>
        </div>

        <div class="checkboxContainer checkboxContainer-withDescription fldRenderPgs hide">
            <label>
                <input is="emby-checkbox" type="checkbox" id="chkSubtitleRenderPgs" />
                <span>\${RenderPgsSubtitle}</span>
            </label>
            <div class="fieldDescription checkboxFieldDescription">\${RenderPgsSubtitleHelp}</div>
        </div>
    </div>

    <div class="checkboxContainer checkboxContainer-withDescription">
        <label>
            <input is="emby-checkbox" type="checkbox" id="chkAlwaysBurnInSubtitleWhenTranscoding" />
            <span>\${AlwaysBurnInSubtitleWhenTranscoding}</span>
        </label>
        <div class="fieldDescription checkboxFieldDescription">\${AlwaysBurnInSubtitleWhenTranscodingHelp}</div>
    </div>

    <div class="verticalSection subtitleAppearanceSection hide">
        <h2 class="sectionTitle">
            \${HeaderSubtitleAppearance}
        </h2>

        <div class="subtitleappearance-fullpreview subtitleappearance-fullpreview-hide">
            <div class="subtitleappearance-fullpreview-window">
                <div class="subtitleappearance-fullpreview-text">
                    \${HeaderSubtitleAppearance}
                    <br>
                    \${TheseSettingsAffectSubtitlesOnThisDevice}
                </div>
            </div>
        </div>

        <div style="margin: 2em 0 2em;">
            <div class="subtitleappearance-preview flex align-items-center justify-content-center" style="margin:2em 0;padding:1.6em;color:black;background:linear-gradient(140deg,#aa5cc3,#00a4dc);">
                <div class="subtitleappearance-preview-window flex align-items-center justify-content-center" style="width: 90%; padding: .25em;">
                    <div class="subtitleappearance-preview-text flex align-items-center justify-content-center">
                        \${TheseSettingsAffectSubtitlesOnThisDevice}
                    </div>
                </div>
            </div>
            <div class="fieldDescription">\${SubtitleAppearanceSettingsDisclaimer}</div>
            <div class="fieldDescription">\${SubtitleAppearanceSettingsAlsoPassedToCastDevices}</div>
        </div>

        <div class="selectContainer">
            <select is="emby-select" id="selectSubtitleStyling" label="\${LabelSubtitleStyling}">
                <option value="Auto">\${Auto}</option>
                <option value="Custom">\${Custom}</option>
                <option value="Native">\${Native}</option>
            </select>
            <div class="fieldDescription subtitleStylingAutoHelp subtitleStylingHelp hide">\${AutoSubtitleStylingHelp}</div>
            <div class="fieldDescription subtitleStylingCustomHelp subtitleStylingHelp hide">\${CustomSubtitleStylingHelp}</div>
            <div class="fieldDescription subtitleStylingNativeHelp subtitleStylingHelp hide">\${NativeSubtitleStylingHelp}</div>
        </div>

        <div class="selectContainer">
            <select is="emby-select" id="selectTextSize" label="\${LabelTextSize}">
                <option value="smaller">\${Smaller}</option>
                <option value="small">\${Small}</option>
                <option value="">\${Normal}</option>
                <option value="large">\${Large}</option>
                <option value="larger">\${Larger}</option>
                <option value="extralarge">\${ExtraLarge}</option>
            </select>
        </div>

        <div class="selectContainer">
            <select is="emby-select" id="selectTextWeight" label="\${LabelTextWeight}">
                <option value="normal">\${Normal}</option>
                <option value="bold">\${Bold}</option>
            </select>
        </div>

        <div class="selectContainer">
            <select is="emby-select" id="selectFont" label="\${LabelFont}">
                <option value="">\${Default}</option>
                <option value="typewriter">\${Typewriter}</option>
                <option value="print">\${Print}</option>
                <option value="console">\${Console}</option>
                <option value="cursive">\${Cursive}</option>
                <option value="casual">\${Casual}</option>
                <option value="smallcaps">\${SmallCaps}</option>
            </select>
        </div>

        <div class="inputContainer hide">
            <input is="emby-input" id="inputTextBackground" label="\${LabelTextBackgroundColor}" type="text" />
        </div>

        <div class="selectContainer">
            <input is="emby-input" id="inputTextColor" label="\${LabelTextColor}" type="color" />
        </div>

        <div class="selectContainer hide">
            <select is="emby-select" id="selectTextColor" label="\${LabelTextColor}">
                <option value="#ffffff">\${SubtitleWhite}</option>
                <option value="#d3d3d3">\${SubtitleLightGray}</option>
                <option value="#808080">\${SubtitleGray}</option>
                <option value="#ffff00">\${SubtitleYellow}</option>
                <option value="#008000">\${SubtitleGreen}</option>
                <option value="#00ffff">\${SubtitleCyan}</option>
                <option value="#0000ff">\${SubtitleBlue}</option>
                <option value="#ff00ff">\${SubtitleMagenta}</option>
                <option value="#ff0000">\${SubtitleRed}</option>
                <option value="#000000">\${SubtitleBlack}</option>
            </select>
        </div>

        <div class="selectContainer">
            <select is="emby-select" id="selectDropShadow" label="\${LabelDropShadow}">
                <option value="none">\${None}</option>
                <option value="raised">\${Raised}</option>
                <option value="depressed">\${Depressed}</option>
                <option value="uniform">\${Uniform}</option>
                <option value="">\${DropShadow}</option>
            </select>
        </div>

        <div class="sliderContainer-settings">
            <div class="sliderContainer">
                <input is="emby-slider" id="sliderVerticalPosition" label="\${LabelSubtitleVerticalPosition}" type="range" min="-16" max="16" />
            </div>
            <div class="fieldDescription">\${SubtitleVerticalPositionHelp}</div>
        </div>

        <div class="checkboxContainer">
            <label>
                <input is="emby-checkbox" type="checkbox" class="chkPreview" />
                <span>\${Preview}</span>
            </label>
        </div>
    </div>

    <button is="emby-button" type="submit" class="raised button-submit block btnSave hide">
        <span>\${Save}</span>
    </button>
</form>
`;function m(e){return{subtitleStyling:e.querySelector("#selectSubtitleStyling").value,textSize:e.querySelector("#selectTextSize").value,textWeight:e.querySelector("#selectTextWeight").value,dropShadow:e.querySelector("#selectDropShadow").value,font:e.querySelector("#selectFont").value,textBackground:e.querySelector("#inputTextBackground").value,textColor:f.tv?e.querySelector("#selectTextColor").value:e.querySelector("#inputTextColor").value,verticalPosition:e.querySelector("#sliderVerticalPosition").value}}function H(e,t,i,n,l){return b.withLoading(async()=>{const s=await l.getCultures();y.supports(h.SubtitleBurnIn)&&t.Policy.EnableVideoPlaybackTranscoding&&e.querySelector(".fldBurnIn").classList.remove("hide");const a=e.querySelector("#selectSubtitleLanguage");k.populateLanguages(a,s),a.value=t.Configuration.SubtitleLanguagePreference||"",e.querySelector("#selectSubtitlePlaybackMode").value=t.Configuration.SubtitleMode||"",e.querySelector("#selectSubtitlePlaybackMode").dispatchEvent(new CustomEvent("change",{})),e.querySelector("#selectSubtitleStyling").value=n.subtitleStyling||"Auto",e.querySelector("#selectSubtitleStyling").dispatchEvent(new CustomEvent("change",{})),e.querySelector("#selectTextSize").value=n.textSize||"",e.querySelector("#selectTextWeight").value=n.textWeight||"normal",e.querySelector("#selectDropShadow").value=n.dropShadow||"",e.querySelector("#inputTextBackground").value=n.textBackground||"transparent",e.querySelector("#selectTextColor").value=n.textColor||"#ffffff",e.querySelector("#inputTextColor").value=n.textColor||"#ffffff",e.querySelector("#selectFont").value=n.font||"",e.querySelector("#sliderVerticalPosition").value=n.verticalPosition,e.querySelector("#selectSubtitleBurnIn").value=r.get("subtitleburnin")||"",e.querySelector("#chkSubtitleRenderPgs").checked=r.get("subtitlerenderpgs")==="true",e.querySelector("#selectSubtitleBurnIn").dispatchEvent(new CustomEvent("change",{})),e.querySelector("#chkAlwaysBurnInSubtitleWhenTranscoding").checked=r.alwaysBurnInSubtitleWhenTranscoding(),o({target:e.querySelector("#selectTextSize")})})}function x(e,t,i,n,l){let s=i.getSubtitleAppearanceSettings(n);return s=Object.assign(s,m(e)),i.setSubtitleAppearanceSettings(s,n),t.Configuration.SubtitleLanguagePreference=e.querySelector("#selectSubtitleLanguage").value,t.Configuration.SubtitleMode=e.querySelector("#selectSubtitlePlaybackMode").value,l.updateUserConfiguration(t.Id,t.Configuration)}async function I(e,t,i,n,l,s){await b.withLoading(async()=>{r.set("subtitleburnin",t.querySelector("#selectSubtitleBurnIn").value),r.set("subtitlerenderpgs",String(t.querySelector("#chkSubtitleRenderPgs").checked)),r.alwaysBurnInSubtitleWhenTranscoding(t.querySelector("#chkAlwaysBurnInSubtitleWhenTranscoding").checked);const a=await l.getUser(i);await x(t,a,n,e.appearanceKey,l),s&&C(v.translate("SettingsSaved")),L.trigger(e,"saved")})}function E(e){const t=u.parentWithClass(e.target,"subtitlesettings");if(!t)return;const i=t.querySelectorAll(".subtitlesHelp");for(let n=0,l=i.length;n<l;n++)i[n].classList.add("hide");t.querySelector(".subtitles"+this.value+"Help").classList.remove("hide")}function B(e){const t=u.parentWithClass(e.target,"subtitlesettings");if(!t)return;t.querySelectorAll(".subtitleStylingHelp").forEach(n=>{n.classList.add("hide")}),t.querySelector(`.subtitleStyling${this.value}Help`).classList.remove("hide")}function F(e){const t=u.parentWithClass(e.target,"subtitlesettings");if(!t)return;t.querySelector(".fldRenderPgs").classList.toggle("hide",!!this.value)}function o(e){const t=u.parentWithClass(e.target,"subtitlesettings");if(!t)return;const i=m(t),n={window:t.querySelector(".subtitleappearance-preview-window"),text:t.querySelector(".subtitleappearance-preview-text"),preview:!0};g.applyStyles(n,i),g.applyStyles({window:t.querySelector(".subtitleappearance-fullpreview-window"),text:t.querySelector(".subtitleappearance-fullpreview-text")},i)}const W=1e3;let p;function c(e){clearTimeout(p),this._fullPreview.classList.remove("subtitleappearance-fullpreview-hide"),e&&this._refFullPreview++,this._refFullPreview===0&&(p=setTimeout(d.bind(this),W))}function d(e){clearTimeout(p),e&&this._refFullPreview--,this._refFullPreview===0&&this._fullPreview.classList.add("subtitleappearance-fullpreview-hide")}function M(e,t){if(e.element.classList.add("subtitlesettings"),e.element.innerHTML=v.translateHtml(A,"core"),e.element.querySelector("form").addEventListener("submit",t.onSubmit.bind(t)),e.element.querySelector("#selectSubtitlePlaybackMode").addEventListener("change",E),e.element.querySelector("#selectSubtitleStyling").addEventListener("change",B),e.element.querySelector("#selectSubtitleBurnIn").addEventListener("change",F),e.element.querySelector("#selectTextSize").addEventListener("change",o),e.element.querySelector("#selectTextWeight").addEventListener("change",o),e.element.querySelector("#selectDropShadow").addEventListener("change",o),e.element.querySelector("#selectFont").addEventListener("change",o),e.element.querySelector("#selectTextColor").addEventListener("change",o),e.element.querySelector("#inputTextColor").addEventListener("change",o),e.element.querySelector("#inputTextBackground").addEventListener("change",o),e.enableSaveButton&&e.element.querySelector(".btnSave").classList.remove("hide"),y.supports(h.SubtitleAppearance)){e.element.querySelector(".subtitleAppearanceSection").classList.remove("hide"),t._fullPreview=e.element.querySelector(".subtitleappearance-fullpreview"),t._refFullPreview=0;const i=e.element.querySelector("#sliderVerticalPosition");i.addEventListener("input",o),i.addEventListener("input",()=>c.call(t));const n=window.PointerEvent?"pointer":"mouse";i.addEventListener(`${n}enter`,()=>c.call(t,!0)),i.addEventListener(`${n}leave`,()=>d.call(t,!0)),f.tv&&(i.addEventListener("focus",()=>c.call(t,!0)),i.addEventListener("blur",()=>d.call(t,!0)),setTimeout(()=>{i.classList.add("focusable"),i.enableKeyboardDragging()},0),u.parentWithTag(e.element.querySelector("#inputTextColor"),"DIV").classList.add("hide"),u.parentWithTag(e.element.querySelector("#selectTextColor"),"DIV").classList.remove("hide")),e.element.querySelector(".chkPreview").addEventListener("change",l=>{l.target.checked?c.call(t,!0):d.call(t,!0)})}t.loadData(),e.autoFocus&&$.autoFocus(e.element)}class N{constructor(t){this._refFullPreview=0,this.options=t,M(t,this)}loadData(){const t=this,i=t.options.element,n=t.options.userId,l=S.getApiClient(t.options.serverId),s=t.options.userSettings;b.withLoading(async()=>{const a=await l.getUser(n);await s.setUserInfo(n,l),t.dataLoaded=!0;const w=s.getSubtitleAppearanceSettings(t.options.appearanceKey);await H(i,a,s,w,l)}).catch(a=>console.error("Failed to load subtitle settings",a))}submit(){this.onSubmit(null)}destroy(){this.options=null}onSubmit(t){const i=this,n=S.getApiClient(i.options.serverId),l=i.options.userId,s=i.options.userSettings;return s.setUserInfo(l,n).then(function(){const a=i.options.enableSaveConfirmation;return I(i,i.options.element,l,s,n,a)}).catch(a=>console.error("Failed to save subtitle settings",a)),t&&t.preventDefault(),!1}}const R=T;function te(e,t){let i;const n=t.userId||ApiClient.getCurrentUserId(),l=n===ApiClient.getCurrentUserId()?q:new R;e.addEventListener("viewshow",function(){if(i){i.loadData();return}i=new N({serverId:ApiClient.serverId(),userId:n,element:e.querySelector(".settingsContainer"),userSettings:l,enableSaveButton:!0,enableSaveConfirmation:!0,autoFocus:P.isEnabled()})}),e.addEventListener("viewdestroy",function(){i&&(i.destroy(),i=void 0)})}export{te as default};
