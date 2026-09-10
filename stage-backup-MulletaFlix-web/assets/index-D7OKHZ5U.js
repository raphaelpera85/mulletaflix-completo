import{a2 as t,S as b,aC as C,b2 as D,T as f,a$ as g,f as c,k as s,A as p,bi as L,l as m,cV as y,e as u,a8 as q,P as T,b as v,W as x,E as $,bg as E,cW as w,bq as B}from"./index-6F8mVyyU.js";import"./emby-select-BS-dUTXm.js";import"./emby-textarea-BTRMqmjp.js";import"./actionSheet-bHfWrycr.js";const I=`\uFEFF<form style="margin: 0 auto;">
    <h2 class="sectionTitle">
        \${Localization}
    </h2>

    <div class="selectContainer languageSection hide">
        <select id="selectLanguage" is="emby-select" label="\${LabelDisplayLanguage}">
            <option value="">\${Auto}</option>
            <option value="af">Afrikaans</option>
            <option value="ar">Ø§Ù„Ø¹Ø±Ø¨ÙŠØ©</option>
            <option value="be-BY">Ð‘ÐµÐ»Ð°Ñ€ÑƒÑÐºÐ°Ñ</option>
            <option value="bg-BG">Ð‘ÑŠÐ»Ð³Ð°Ñ€ÑÐºÐ¸</option>
            <option value="bn_BD">à¦¬à¦¾à¦‚à¦²à¦¾ (à¦¬à¦¾à¦‚à¦²à¦¾à¦¦à§‡à¦¶)</option>
            <option value="ca">CatalÃ </option>
            <option value="cs">ÄŒeÅ¡tina</option>
            <option value="cy">Cymraeg</option>
            <option value="da">Dansk</option>
            <option value="de">Deutsch</option>
            <option value="el">Î•Î»Î»Î·Î½Î¹ÎºÎ¬</option>
            <option value="en-GB">English (United Kingdom)</option>
            <option value="en-US">English</option>
            <option value="eo">Esperanto</option>
            <option value="es">EspaÃ±ol</option>
            <option value="es_419">EspaÃ±ol americano</option>
            <option value="es-AR">EspaÃ±ol (Argentina)</option>
            <option value="es_DO">EspaÃ±ol (Dominicana)</option>
            <option value="es-MX">EspaÃ±ol (MÃ©xico)</option>
            <option value="et">Eesti</option>
            <option value="eu">Euskara</option>
            <option value="fa">ÙØ§Ø±Ø³ÛŒ</option>
            <option value="fi">Suomi</option>
            <option value="fil">Filipino</option>
            <option value="fr">FranÃ§ais</option>
            <option value="fr-CA">FranÃ§ais (Canada)</option>
            <option value="gl">Galego</option>
            <option value="gsw">SchwiizerdÃ¼tsch</option>
            <option value="he">×¢Ö´×‘Ö°×¨Ö´×™×ª</option>
            <option value="hi-IN">à¤¹à¤¿à¤¨à¥à¤¦à¥€</option>
            <option value="hr">Hrvatski </option>
            <option value="hu">Magyar</option>
            <option value="id">Bahasa Indonesia</option>
            <option value="is-IS">Ãslenska</option>
            <option value="it">Italiano</option>
            <option value="ja">æ—¥æœ¬èªž</option>
            <option value="kk">QazaqÅŸa</option>
            <option value="ko">í•œêµ­ì–´</option>
            <option value="lt-LT">LietuviÅ³</option>
            <option value="lv">LatvieÅ¡u</option>
            <option value="mk">ÐœÐ°ÐºÐµÐ´Ð¾Ð½ÑÐºÐ¸</option>
            <option value="ml">à´®à´²à´¯à´¾à´³à´‚</option>
            <option value="mr">à¤®à¤°à¤¾à¤ à¥€</option>
            <option value="ms">Bahasa Melayu</option>
            <option value="nb">Norsk bokmÃ¥l</option>
            <option value="ne">à¤¨à¥‡à¤ªà¤¾à¤²à¥€</option>
            <option value="nl">Nederlands</option>
            <option value="nn">Norsk nynorsk</option>
            <option value="pa">à¨ªà©°à¨œà¨¾à¨¬à©€</option>
            <option value="pl">Polski</option>
            <option value="pr">Pirate</option>
            <option value="pt">PortuguÃªs</option>
            <option value="pt-BR">PortuguÃªs (Brasil)</option>
            <option value="pt-PT">PortuguÃªs (Portugal)</option>
            <option value="ro">RomÃ¢neÈ™te</option>
            <option value="ru">Ð ÑƒÑÑÐºÐ¸Ð¹</option>
            <option value="sk">SlovenÄina</option>
            <option value="sl-SI">SlovenÅ¡Äina</option>
            <option value="sq">Shqip</option>
            <option value="sr">Ð¡Ñ€Ð¿ÑÐºÐ¸</option>
            <option value="sv">Svenska</option>
            <option value="ta">à®¤à®®à®¿à®´à¯</option>
            <option value="te">à°¤à±†à°²à±à°—à±</option>
            <option value="th">à¸ à¸²à¸©à¸²à¹„à¸—à¸¢</option>
            <option value="tr">TÃ¼rkÃ§e</option>
            <option value="uk">Ð£ÐºÑ€Ð°Ñ—Ð½ÑÑŒÐºÐ°</option>
            <option value="ur_PK">Ø§ÙØ±Ø¯ÙÙˆ</option>
            <option value="vi">Tiáº¿ng Viá»‡t</option>
            <option value="zh-CN">æ±‰è¯­ (ç®€åŒ–å­—)</option>
            <option value="zh-TW">æ¼¢èªž (ç¹ä½“å­—)</option>
            <option value="zh-HK">å»£æ±è©± (é¦™æ¸¯)</option>
        </select>
        <div class="fieldDescription">
            <div>\${LabelDisplayLanguageHelp}</div>
            <div class="learnHowToContributeContainer hide" style="margin-top: .25em;">
                <a is="emby-linkbutton" rel="noopener noreferrer" class="button-link" href="https://github.com/MulletaFlix/MulletaFlix" target="_blank">\${LearnHowYouCanContribute}</a>
            </div>
        </div>
    </div>

    <div class="selectContainer fldDateTimeLocale hide">
        <select is="emby-select" class="selectDateTimeLocale" label="\${LabelDateTimeLocale}">
            <option value="">\${Auto}</option>
            <option value="af">Afrikaans</option>
            <option value="ar">Ø§Ù„Ø¹Ø±Ø¨ÙŠØ©</option>
            <option value="be-BY">Ð‘ÐµÐ»Ð°Ñ€ÑƒÑÐºÐ°Ñ</option>
            <option value="bg-BG">Ð‘ÑŠÐ»Ð³Ð°Ñ€ÑÐºÐ¸</option>
            <option value="bn_BD">à¦¬à¦¾à¦‚à¦²à¦¾ (à¦¬à¦¾à¦‚à¦²à¦¾à¦¦à§‡à¦¶)</option>
            <option value="ca">CatalÃ </option>
            <option value="cs">ÄŒeÅ¡tina</option>
            <option value="cy">Cymraeg</option>
            <option value="da">Dansk</option>
            <option value="de">Deutsch</option>
            <option value="el">Î•Î»Î»Î·Î½Î¹ÎºÎ¬</option>
            <option value="en-GB">English (United Kingdom)</option>
            <option value="en-US">English</option>
            <option value="eo">Esperanto</option>
            <option value="es">EspaÃ±ol</option>
            <option value="es_419">EspaÃ±ol americano</option>
            <option value="es-AR">EspaÃ±ol (Argentina)</option>
            <option value="es_DO">EspaÃ±ol (Dominicana)</option>
            <option value="es-MX">EspaÃ±ol (MÃ©xico)</option>
            <option value="et">Eesti</option>
            <option value="fa">ÙØ§Ø±Ø³ÛŒ</option>
            <option value="fi">Suomi</option>
            <option value="fil">Filipino</option>
            <option value="fr">FranÃ§ais</option>
            <option value="fr-CA">FranÃ§ais (Canada)</option>
            <option value="gl">Galego</option>
            <option value="gsw">SchwiizerdÃ¼tsch</option>
            <option value="he">×¢Ö´×‘Ö°×¨Ö´×™×ª</option>
            <option value="hi-IN">à¤¹à¤¿à¤¨à¥à¤¦à¥€</option>
            <option value="hr">Hrvatski </option>
            <option value="hu">Magyar</option>
            <option value="id">Bahasa Indonesia</option>
            <option value="is-IS">Ãslenska</option>
            <option value="it">Italiano</option>
            <option value="ja">æ—¥æœ¬èªž</option>
            <option value="kk">QazaqÅŸa</option>
            <option value="ko">í•œêµ­ì–´</option>
            <option value="lt-LT">LietuviÅ³</option>
            <option value="lv">LatvieÅ¡u</option>
            <option value="mk">ÐœÐ°ÐºÐµÐ´Ð¾Ð½ÑÐºÐ¸</option>
            <option value="ml">à´®à´²à´¯à´¾à´³à´‚</option>
            <option value="mr">à¤®à¤°à¤¾à¤ à¥€</option>
            <option value="ms">Bahasa Melayu</option>
            <option value="nb">Norsk bokmÃ¥l</option>
            <option value="ne">à¤¨à¥‡à¤ªà¤¾à¤²à¥€</option>
            <option value="nl">Nederlands</option>
            <option value="nn">Norsk nynorsk</option>
            <option value="pa">à¨ªà©°à¨œà¨¾à¨¬à©€</option>
            <option value="pl">Polski</option>
            <option value="pr">Pirate</option>
            <option value="pt">PortuguÃªs</option>
            <option value="pt-BR">PortuguÃªs (Brasil)</option>
            <option value="pt-PT">PortuguÃªs (Portugal)</option>
            <option value="ro">RomÃ¢neÈ™te</option>
            <option value="ru">Ð ÑƒÑÑÐºÐ¸Ð¹</option>
            <option value="sk">SlovenÄina</option>
            <option value="sl-SI">SlovenÅ¡Äina</option>
            <option value="sq">Shqip</option>
            <option value="sr">Ð¡Ñ€Ð¿ÑÐºÐ¸</option>
            <option value="sv">Svenska</option>
            <option value="ta">à®¤à®®à®¿à®´à¯</option>
            <option value="te">à°¤à±†à°²à±à°—à±</option>
            <option value="th">à¸ à¸²à¸©à¸²à¹„à¸—à¸¢</option>
            <option value="tr">TÃ¼rkÃ§e</option>
            <option value="uk">Ð£ÐºÑ€Ð°Ñ—Ð½ÑÑŒÐºÐ°</option>
            <option value="ur_PK">Ø§ÙØ±Ø¯ÙÙˆ</option>
            <option value="vi">Tiáº¿ng Viá»‡t</option>
            <option value="zh-CN">æ±‰è¯­ (ç®€åŒ–å­—)</option>
            <option value="zh-TW">æ¼¢èªž (ç¹ä½“å­—)</option>
            <option value="zh-HK">å»£æ±è©± (é¦™æ¸¯)</option>
        </select>
    </div>

    <h2 class="sectionTitle">
        \${Display}
    </h2>

    <div class="selectContainer fldDisplayMode hide">
        <select is="emby-select" class="selectLayout" label="\${LabelDisplayMode}">
            <option value="">\${Auto}</option>
            <option value="desktop">\${Desktop}</option>
            <option value="mobile">\${Mobile}</option>
            <option value="tv">\${TV}</option>
        </select>
        <div class="fieldDescription">\${DisplayModeHelp}</div>
        <div class="fieldDescription">\${LabelPleaseRestart}</div>
    </div>

    <div class="selectContainer">
        <select id="selectTheme" is="emby-select" label="\${LabelTheme}"></select>
    </div>

    <div class="checkboxContainer checkboxContainer-withDescription">
        <label>
            <input type="checkbox" is="emby-checkbox" id="chkDisableCustomCss" />
            <span>\${DisableCustomCss}</span>
        </label>
        <div class="fieldDescription checkboxFieldDescription">\${LabelDisableCustomCss}</div>
    </div>

    <div class="inputContainer customCssContainer">
        <textarea is="emby-textarea" id="txtLocalCustomCss" label="\${LabelCustomCss}" class="textarea-mono"></textarea>
        <div class="fieldDescription">\${LabelLocalCustomCss}</div>
    </div>

    <div class="selectContainer selectDashboardThemeContainer hide">
        <select id="selectDashboardTheme" is="emby-select" label="\${LabelDashboardTheme}"></select>
    </div>

    <div class="selectContainer hide selectScreensaverContainer">
        <select is="emby-select" class="selectScreensaver" label="\${LabelScreensaver}"></select>
    </div>

    <div class="inputContainer hide txtScreensaverTimeContainer inputContainer-withDescription">
        <input is="emby-input" type="number" id="txtScreensaverTime" pattern="[0-9]*" required="required" min="5" max="86400" step="1"
            label="\${LabelScreensaverTime}" />
        <div class="fieldDescription">\${LabelScreensaverTimeHelp}</div>
    </div>

    <div class="inputContainer hide txtBackdropScreensaverIntervalContainer inputContainer-withDescription">
        <input is="emby-input" type="number" id="txtBackdropScreensaverInterval" pattern="[0-9]*" required="required" min="1" max="3600" step="1" label="\${LabelBackdropScreensaverInterval}" />
        <div class="fieldDescription">\${LabelBackdropScreensaverIntervalHelp}</div>
    </div>

    <div class="inputContainer hide txtSlideshowIntervalContainer inputContainer-withDescription">
        <input is="emby-input" type="number" id="txtSlideshowInterval" pattern="[0-9]*" required="required" min="1" max="3600" step="1" label="\${LabelSlideshowInterval}" />
        <div class="fieldDescription">\${LabelSlideshowIntervalHelp}</div>
    </div>

    <div class="checkboxContainer checkboxContainer-withDescription">
        <label>
            <input type="checkbox" is="emby-checkbox" id="chkFadein" />
            <span>\${EnableFasterAnimations}</span>
        </label>
        <div class="fieldDescription checkboxFieldDescription">\${EnableFasterAnimationsHelp}</div>
    </div>

    <div class="checkboxContainer checkboxContainer-withDescription">
        <label>
            <input type="checkbox" is="emby-checkbox" id="chkBlurhash" />
            <span>\${EnableBlurHash}</span>
        </label>
        <div class="fieldDescription checkboxFieldDescription">\${EnableBlurHashHelp}</div>
    </div>

    <h2 class="sectionTitle">
        \${HeaderLibraries}
    </h2>

    <div class="inputContainer inputContainer-withDescription">
        <input is="emby-input" type="number" id="txtLibraryPageSize" pattern="[0-9]*" required="required" min="0" max="1000" step="1" label="\${LabelLibraryPageSize}" />
        <div class="fieldDescription">\${LabelLibraryPageSizeHelp}</div>
    </div>

    <div class="checkboxContainer checkboxContainer-withDescription fldBackdrops">
        <label>
            <input type="checkbox" is="emby-checkbox" id="chkBackdrops" />
            <span>\${Backdrops}</span>
        </label>
        <div class="fieldDescription checkboxFieldDescription">\${EnableBackdropsHelp}</div>
    </div>

    <div class="checkboxContainer checkboxContainer-withDescription fldThemeSong">
        <label>
            <input type="checkbox" is="emby-checkbox" id="chkThemeSong" />
            <span>\${ThemeSongs}</span>
        </label>
        <div class="fieldDescription checkboxFieldDescription">\${EnableThemeSongsHelp}</div>
    </div>

    <div class="checkboxContainer checkboxContainer-withDescription fldThemeVideo">
        <label>
            <input type="checkbox" is="emby-checkbox" id="chkThemeVideo" />
            <span>\${ThemeVideos}</span>
        </label>
        <div class="fieldDescription checkboxFieldDescription">\${EnableThemeVideosHelp}</div>
    </div>

    <div class="checkboxContainer checkboxContainer-withDescription fldDisplayMissingEpisodes hide">
        <label>
            <input type="checkbox" is="emby-checkbox" class="chkDisplayMissingEpisodes" />
            <span>\${DisplayMissingEpisodesWithinSeasons}</span>
        </label>
        <div class="fieldDescription checkboxFieldDescription">\${DisplayMissingEpisodesWithinSeasonsHelp}</div>
    </div>

    <h2 class="sectionTitle">
        \${NextUp}
    </h2>

    <div class="inputContainer inputContainer-withDescription">
        <input is="emby-input" type="number" id="txtMaxDaysForNextUp" pattern="[0-9]*" required="required" min="0" max="1000" step="1" label="\${LabelMaxDaysForNextUp}" />
        <div class="fieldDescription">\${LabelMaxDaysForNextUpHelp}</div>
    </div>

    <div class="checkboxContainer checkboxContainer-withDescription">
        <label>
            <input type="checkbox" is="emby-checkbox" id="chkRewatchingNextUp" />
            <span>\${EnableRewatchingNextUp}</span>
        </label>
        <div class="fieldDescription checkboxFieldDescription">\${EnableRewatchingNextUpHelp}</div>
    </div>

    <div class="checkboxContainer checkboxContainer-withDescription fldUseEpisodeImagesInNextUp">
        <label>
            <input type="checkbox" is="emby-checkbox" id="chkUseEpisodeImagesInNextUp" />
            <span>\${UseEpisodeImagesInNextUp}</span>
        </label>
        <div class="fieldDescription checkboxFieldDescription">\${UseEpisodeImagesInNextUpHelp}</div>
    </div>

    <h2 class="sectionTitle">
        \${ItemDetails}
    </h2>

    <div class="checkboxContainer checkboxContainer-withDescription">
        <label>
            <input type="checkbox" is="emby-checkbox" id="chkDetailsBanner" />
            <span>\${EnableDetailsBanner}</span>
        </label>
        <div class="fieldDescription checkboxFieldDescription">\${EnableDetailsBannerHelp}</div>
    </div>

    <button is="emby-button" type="submit" class="raised button-submit block btnSave hide">
        <span>\${Save}</span>
    </button>
</form>

`;function h(e,o){y.getThemes().then(n=>{e.innerHTML=n.map(i=>`<option value="${u(i.id)}">${u(i.name)}</option>`).join("");const a=n.find(i=>i.default);e.value=o||a.id}).catch(()=>{e.innerHTML=""})}function U(e,o){const n=e.querySelector(".selectScreensaver"),a=q.ofType(T.Screensaver).map(i=>({name:c.translate(i.name),value:i.id}));a.unshift({name:c.translate("None"),value:"none"}),n.innerHTML=a.map(i=>`<option value="${u(i.value)}">${u(i.name)}</option>`).join(""),n.value=o.screensaver(),n.value||(n.value="none")}function F(e){if(v.tizen||v.web0s){e.querySelector(".fldDisplayMissingEpisodes").classList.add("hide");return}e.querySelector(".fldDisplayMissingEpisodes").classList.remove("hide")}function M(e,o,n){s.supports(p.DisplayLanguage)?e.querySelector(".languageSection").classList.remove("hide"):e.querySelector(".languageSection").classList.add("hide"),s.supports(p.DisplayMode)?e.querySelector(".fldDisplayMode").classList.remove("hide"):e.querySelector(".fldDisplayMode").classList.add("hide"),s.supports(p.ExternalLinks)?e.querySelector(".learnHowToContributeContainer").classList.remove("hide"):e.querySelector(".learnHowToContributeContainer").classList.add("hide"),e.querySelector(".selectDashboardThemeContainer").classList.toggle("hide",!o.Policy.IsAdministrator),e.querySelector(".txtSlideshowIntervalContainer").classList.remove("hide"),s.supports(p.Screensaver)?(e.querySelector(".selectScreensaverContainer").classList.remove("hide"),e.querySelector(".txtBackdropScreensaverIntervalContainer").classList.remove("hide"),e.querySelector(".txtScreensaverTimeContainer").classList.remove("hide")):(e.querySelector(".selectScreensaverContainer").classList.add("hide"),e.querySelector(".txtBackdropScreensaverIntervalContainer").classList.add("hide"),e.querySelector(".txtScreensaverTimeContainer").classList.add("hide")),L.supportsLocalization()?e.querySelector(".fldDateTimeLocale").classList.remove("hide"):e.querySelector(".fldDateTimeLocale").classList.add("hide"),h(e.querySelector("#selectTheme"),n.theme()),h(e.querySelector("#selectDashboardTheme"),n.dashboardTheme()),U(e,n),e.querySelector("#txtBackdropScreensaverInterval").value=n.backdropScreensaverInterval(),e.querySelector("#txtSlideshowInterval").value=n.slideshowInterval(),e.querySelector("#txtScreensaverTime").value=n.screensaverTime(),e.querySelector(".chkDisplayMissingEpisodes").checked=o.Configuration.DisplayMissingEpisodes||!1,e.querySelector("#chkThemeSong").checked=n.enableThemeSongs(),e.querySelector("#chkThemeVideo").checked=n.enableThemeVideos(),e.querySelector("#chkFadein").checked=n.enableFastFadein(),e.querySelector("#chkBlurhash").checked=n.enableBlurhash(),e.querySelector("#chkBackdrops").checked=n.enableBackdrops(),e.querySelector("#chkDetailsBanner").checked=n.detailsBanner(),e.querySelector("#chkDisableCustomCss").checked=n.disableCustomCss(),e.querySelector("#txtLocalCustomCss").value=n.customCss(),e.querySelector("#selectLanguage").value=n.language()||"",e.querySelector(".selectDateTimeLocale").value=n.dateTimeLocale()||"",e.querySelector("#txtLibraryPageSize").value=n.libraryPageSize(),e.querySelector("#txtMaxDaysForNextUp").value=n.maxDaysForNextUp(),e.querySelector("#chkRewatchingNextUp").checked=n.enableRewatchingInNextUp(),e.querySelector("#chkUseEpisodeImagesInNextUp").checked=n.useEpisodeImagesInNextUpAndResume(),e.querySelector(".selectLayout").value=m.getSavedLayout()||"",F(e),t.hide()}function H(e,o,n,a){return o.Configuration.DisplayMissingEpisodes=e.querySelector(".chkDisplayMissingEpisodes").checked,s.supports(p.DisplayLanguage)&&n.language(e.querySelector("#selectLanguage").value),n.dateTimeLocale(e.querySelector(".selectDateTimeLocale").value),n.enableThemeSongs(e.querySelector("#chkThemeSong").checked),n.enableThemeVideos(e.querySelector("#chkThemeVideo").checked),n.theme(e.querySelector("#selectTheme").value),n.dashboardTheme(e.querySelector("#selectDashboardTheme").value),n.screensaver(e.querySelector(".selectScreensaver").value),n.backdropScreensaverInterval(e.querySelector("#txtBackdropScreensaverInterval").value),n.slideshowInterval(e.querySelector("#txtSlideshowInterval").value),n.screensaverTime(e.querySelector("#txtScreensaverTime").value),n.libraryPageSize(e.querySelector("#txtLibraryPageSize").value),n.maxDaysForNextUp(e.querySelector("#txtMaxDaysForNextUp").value),n.enableRewatchingInNextUp(e.querySelector("#chkRewatchingNextUp").checked),n.useEpisodeImagesInNextUpAndResume(e.querySelector("#chkUseEpisodeImagesInNextUp").checked),n.enableFastFadein(e.querySelector("#chkFadein").checked),n.enableBlurhash(e.querySelector("#chkBlurhash").checked),n.enableBackdrops(e.querySelector("#chkBackdrops").checked),n.detailsBanner(e.querySelector("#chkDetailsBanner").checked),n.disableCustomCss(e.querySelector("#chkDisableCustomCss").checked),n.customCss(e.querySelector("#txtLocalCustomCss").value),o.Id===a.getCurrentUserId()&&y.setTheme(n.theme()).catch(()=>{}),m.setLayout(e.querySelector(".selectLayout").value),a.updateUserConfiguration(o.Id,o.Configuration)}function N(e,o,n,a,i,l){t.show(),i.getUser(n).then(r=>{H(o,r,a,i).then(()=>{t.hide(),l&&x(c.translate("SettingsSaved")),$.trigger(e,"saved")},()=>{t.hide()})}).catch(()=>{t.hide()})}function k(e){const o=this,n=b.getApiClient(o.options.serverId),a=o.options.userId,i=o.options.userSettings;i.setUserInfo(a,n).then(()=>{const l=o.options.enableSaveConfirmation;N(o,o.options.element,a,i,n,l)}).catch(()=>{t.hide()}),e&&e.preventDefault()}function P(e,o){e.element.innerHTML=c.translateHtml(I,"core"),e.element.querySelector("form").addEventListener("submit",k.bind(o)),e.enableSaveButton&&e.element.querySelector(".btnSave").classList.remove("hide"),o.loadData(e.autoFocus).catch(()=>{t.hide()})}class A{constructor(o){this.dataLoaded=!1,this.options=o,P(o,this)}async loadData(o){const n=this,a=n.options.element;t.show();const i=n.options.userId,l=b.getApiClient(n.options.serverId),r=n.options.userSettings;let d;try{d=await C.fetchQuery(D(f(l),{userId:i}))}catch(S){console.warn("Error fetching user with React Query, falling back to direct API call:",S),d=await l.getUser(i)}await r.setUserInfo(i,l),n.dataLoaded=!0,M(a,d,r),o&&g.autoFocus(a)}submit(){k.call(this,new Event("submit"))}destroy(){this.options=null}}const z=w;function K(e,o){let n;const a=o.userId||ApiClient.getCurrentUserId(),i=a===ApiClient.getCurrentUserId()?E:new z;e.addEventListener("viewshow",function(){if(n){n.loadData();return}n=new A({serverId:ApiClient.serverId(),userId:a,element:e.querySelector(".settingsContainer"),userSettings:i,enableSaveButton:!0,enableSaveConfirmation:!0,autoFocus:B.isEnabled()})}),e.addEventListener("viewdestroy",function(){n&&(n.destroy(),n=void 0)})}export{K as default};
