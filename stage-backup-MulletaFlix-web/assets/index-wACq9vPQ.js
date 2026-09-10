import{f as b,a2 as c,S,k as y,A as f,l as h,d as u,a$ as $,h as r,W as C,E as L,bg as q,cW as T,bq as P}from"./index-6F8mVyyU.js";import{s as g}from"./subtitleappearancehelper-voXRn8Ac.js";import"./emby-select-BS-dUTXm.js";import"./emby-slider-CuP6dmVC.js";import"./actionSheet-bHfWrycr.js";function k(e,t){let n="";n+="<option value=''>"+b.translate("AnyLanguage")+"</option>";for(let i=0,l=t.length;i<l;i++){const s=t[i];n+="<option value='"+s.ThreeLetterISOLanguageName+"'>"+s.DisplayName+"</option>"}e.innerHTML=n}const A={populateLanguages:k},D=`<form style="margin:0 auto;">
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
`;function m(e){return{subtitleStyling:e.querySelector("#selectSubtitleStyling").value,textSize:e.querySelector("#selectTextSize").value,textWeight:e.querySelector("#selectTextWeight").value,dropShadow:e.querySelector("#selectDropShadow").value,font:e.querySelector("#selectFont").value,textBackground:e.querySelector("#inputTextBackground").value,textColor:h.tv?e.querySelector("#selectTextColor").value:e.querySelector("#inputTextColor").value,verticalPosition:e.querySelector("#sliderVerticalPosition").value}}function H(e,t,n,i,l){l.getCultures().then(function(s){y.supports(f.SubtitleBurnIn)&&t.Policy.EnableVideoPlaybackTranscoding&&e.querySelector(".fldBurnIn").classList.remove("hide");const o=e.querySelector("#selectSubtitleLanguage");A.populateLanguages(o,s),o.value=t.Configuration.SubtitleLanguagePreference||"",e.querySelector("#selectSubtitlePlaybackMode").value=t.Configuration.SubtitleMode||"",e.querySelector("#selectSubtitlePlaybackMode").dispatchEvent(new CustomEvent("change",{})),e.querySelector("#selectSubtitleStyling").value=i.subtitleStyling||"Auto",e.querySelector("#selectSubtitleStyling").dispatchEvent(new CustomEvent("change",{})),e.querySelector("#selectTextSize").value=i.textSize||"",e.querySelector("#selectTextWeight").value=i.textWeight||"normal",e.querySelector("#selectDropShadow").value=i.dropShadow||"",e.querySelector("#inputTextBackground").value=i.textBackground||"transparent",e.querySelector("#selectTextColor").value=i.textColor||"#ffffff",e.querySelector("#inputTextColor").value=i.textColor||"#ffffff",e.querySelector("#selectFont").value=i.font||"",e.querySelector("#sliderVerticalPosition").value=i.verticalPosition,e.querySelector("#selectSubtitleBurnIn").value=r.get("subtitleburnin")||"",e.querySelector("#chkSubtitleRenderPgs").checked=r.get("subtitlerenderpgs")==="true",e.querySelector("#selectSubtitleBurnIn").dispatchEvent(new CustomEvent("change",{})),e.querySelector("#chkAlwaysBurnInSubtitleWhenTranscoding").checked=r.alwaysBurnInSubtitleWhenTranscoding(),a({target:e.querySelector("#selectTextSize")}),c.hide()})}function x(e,t,n,i,l){let s=n.getSubtitleAppearanceSettings(i);return s=Object.assign(s,m(e)),n.setSubtitleAppearanceSettings(s,i),t.Configuration.SubtitleLanguagePreference=e.querySelector("#selectSubtitleLanguage").value,t.Configuration.SubtitleMode=e.querySelector("#selectSubtitlePlaybackMode").value,l.updateUserConfiguration(t.Id,t.Configuration)}function I(e,t,n,i,l,s){c.show(),r.set("subtitleburnin",t.querySelector("#selectSubtitleBurnIn").value),r.set("subtitlerenderpgs",String(t.querySelector("#chkSubtitleRenderPgs").checked)),r.alwaysBurnInSubtitleWhenTranscoding(t.querySelector("#chkAlwaysBurnInSubtitleWhenTranscoding").checked),l.getUser(n).then(function(o){x(t,o,i,e.appearanceKey,l).then(function(){c.hide(),s&&C(b.translate("SettingsSaved")),L.trigger(e,"saved")},function(){c.hide()})})}function E(e){const t=u.parentWithClass(e.target,"subtitlesettings");if(!t)return;const n=t.querySelectorAll(".subtitlesHelp");for(let i=0,l=n.length;i<l;i++)n[i].classList.add("hide");t.querySelector(".subtitles"+this.value+"Help").classList.remove("hide")}function B(e){const t=u.parentWithClass(e.target,"subtitlesettings");if(!t)return;t.querySelectorAll(".subtitleStylingHelp").forEach(i=>{i.classList.add("hide")}),t.querySelector(`.subtitleStyling${this.value}Help`).classList.remove("hide")}function F(e){const t=u.parentWithClass(e.target,"subtitlesettings");if(!t)return;t.querySelector(".fldRenderPgs").classList.toggle("hide",!!this.value)}function a(e){const t=u.parentWithClass(e.target,"subtitlesettings");if(!t)return;const n=m(t),i={window:t.querySelector(".subtitleappearance-preview-window"),text:t.querySelector(".subtitleappearance-preview-text"),preview:!0};g.applyStyles(i,n),g.applyStyles({window:t.querySelector(".subtitleappearance-fullpreview-window"),text:t.querySelector(".subtitleappearance-fullpreview-text")},n)}const W=1e3;let v;function d(e){clearTimeout(v),this._fullPreview.classList.remove("subtitleappearance-fullpreview-hide"),e&&this._refFullPreview++,this._refFullPreview===0&&(v=setTimeout(p.bind(this),W))}function p(e){clearTimeout(v),e&&this._refFullPreview--,this._refFullPreview===0&&this._fullPreview.classList.add("subtitleappearance-fullpreview-hide")}function M(e,t){if(e.element.classList.add("subtitlesettings"),e.element.innerHTML=b.translateHtml(D,"core"),e.element.querySelector("form").addEventListener("submit",t.onSubmit.bind(t)),e.element.querySelector("#selectSubtitlePlaybackMode").addEventListener("change",E),e.element.querySelector("#selectSubtitleStyling").addEventListener("change",B),e.element.querySelector("#selectSubtitleBurnIn").addEventListener("change",F),e.element.querySelector("#selectTextSize").addEventListener("change",a),e.element.querySelector("#selectTextWeight").addEventListener("change",a),e.element.querySelector("#selectDropShadow").addEventListener("change",a),e.element.querySelector("#selectFont").addEventListener("change",a),e.element.querySelector("#selectTextColor").addEventListener("change",a),e.element.querySelector("#inputTextColor").addEventListener("change",a),e.element.querySelector("#inputTextBackground").addEventListener("change",a),e.enableSaveButton&&e.element.querySelector(".btnSave").classList.remove("hide"),y.supports(f.SubtitleAppearance)){e.element.querySelector(".subtitleAppearanceSection").classList.remove("hide"),t._fullPreview=e.element.querySelector(".subtitleappearance-fullpreview"),t._refFullPreview=0;const n=e.element.querySelector("#sliderVerticalPosition");n.addEventListener("input",a),n.addEventListener("input",()=>d.call(t));const i=window.PointerEvent?"pointer":"mouse";n.addEventListener(`${i}enter`,()=>d.call(t,!0)),n.addEventListener(`${i}leave`,()=>p.call(t,!0)),h.tv&&(n.addEventListener("focus",()=>d.call(t,!0)),n.addEventListener("blur",()=>p.call(t,!0)),setTimeout(()=>{n.classList.add("focusable"),n.enableKeyboardDragging()},0),u.parentWithTag(e.element.querySelector("#inputTextColor"),"DIV").classList.add("hide"),u.parentWithTag(e.element.querySelector("#selectTextColor"),"DIV").classList.remove("hide")),e.element.querySelector(".chkPreview").addEventListener("change",l=>{l.target.checked?d.call(t,!0):p.call(t,!0)})}t.loadData(),e.autoFocus&&$.autoFocus(e.element)}class N{constructor(t){this._refFullPreview=0,this.options=t,M(t,this)}loadData(){const t=this,n=t.options.element;c.show();const i=t.options.userId,l=S.getApiClient(t.options.serverId),s=t.options.userSettings;l.getUser(i).then(function(o){s.setUserInfo(i,l).then(function(){t.dataLoaded=!0;const w=s.getSubtitleAppearanceSettings(t.options.appearanceKey);H(n,o,s,w,l)})})}submit(){this.onSubmit(null)}destroy(){this.options=null}onSubmit(t){const n=this,i=S.getApiClient(n.options.serverId),l=n.options.userId,s=n.options.userSettings;return s.setUserInfo(l,i).then(function(){const o=n.options.enableSaveConfirmation;I(n,n.options.element,l,s,i,o)}),t&&t.preventDefault(),!1}}const R=T;function j(e,t){let n;const i=t.userId||ApiClient.getCurrentUserId(),l=i===ApiClient.getCurrentUserId()?q:new R;e.addEventListener("viewshow",function(){if(n){n.loadData();return}n=new N({serverId:ApiClient.serverId(),userId:i,element:e.querySelector(".settingsContainer"),userSettings:l,enableSaveButton:!0,enableSaveConfirmation:!0,autoFocus:P.isEnabled()})}),e.addEventListener("viewdestroy",function(){n&&(n.destroy(),n=void 0)})}export{j as default};
