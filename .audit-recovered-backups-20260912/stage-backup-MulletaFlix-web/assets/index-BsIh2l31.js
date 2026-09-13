import{S as g,D as h,i as b,s as l,l as $,q as H,t as f,h as v,B as u,n as C,E as L,av as M,aX as I,aD as k}from"./index-CFwqrzSZ.js";import{g as w}from"./useUserViews-Dc3u5LYz.js";import{h as V}from"./homesections-neP-mCG6.js";/* empty css                 */import"./emby-select-BQevfB2a.js";import"./emby-checkbox-CDwDlGOj.js";import{L as s}from"./libraryTab-BxTq0WIP.js";import"./vendor-react-query-BiAmjtRH.js";import"./vendor-react-CeUY3tRn.js";import"./vendor-jellyfin-Bee54tkY.js";import"./vendor-axios-BCn5QfZZ.js";import"./vendor-lodash-CYyrkkBC.js";import"./vendor-date-fns-CwLI6ukS.js";import"./vendor-dompurify-Baz99PXY.js";import"./dashboard-BemBAZF-.js";import"./itemidentifier-BazfFvEN.js";/* empty css             */import"./cardBuilder-B53dLFYf.js";import"./itemAction-CSkKDKc6.js";import"./image-DKSnMQ9f.js";import"./imageLoader-BC9XjmZs.js";import"./indicators-C1UKDgBM.js";/* empty css                   */import"./shortcuts-BGFBOjpp.js";import"./recordinghelper-C2nBXcGY.js";/* empty css                 */import"./builder-Cs32oike.js";import"./LegacyRoute-DZ-BvRbg.js";import"./viewManager-CMbsf_aR.js";import"./RootAppRouter-Bsmk6BFS.js";import"./vendor-router-DMpXb7aY.js";import"./vendor-mui-CstY5C4d.js";import"./vendor-emotion-Cnm98Hm5.js";import"./emby-itemscontainer-H956OHat.js";import"./vendor-admin-BHN7v2qD.js";import"./emby-scroller-Bektmivt.js";import"./index-TnpU5rua.js";/* empty css                      */import"./actionSheet-BM-G2WQD.js";const T=`<form style="margin:0 auto;">
    <div class="verticalSection verticalSection-extrabottompadding">
        <h2 class="sectionTitle">\${Home}</h2>

        <div class="selectContainer hide selectTVHomeScreenContainer">
            <select is="emby-select" class="selectTVHomeScreen" label="\${LabelTVHomeScreen}">
                <option value="horizontal">\${Horizontal}</option>
                <option value="vertical">\${Vertical}</option>
            </select>
            <div class="fieldDescription">\${LabelPleaseRestart}</div>
        </div>

        <div class="verticalSection verticalSection-extrabottompadding">
            <label class="checkboxContainer">
                <input class="chkHidePlayedFromLatest" type="checkbox" is="emby-checkbox" />
                <span>\${HideWatchedContentFromLatestMedia}</span>
            </label>
        </div>

        <div class="selectContainer">
            <select is="emby-select" id="selectHomeSection1" label="{section1label}">
                <option value="smalllibrarytiles">\${HeaderMyMedia}</option>
                <option value="librarybuttons">\${HeaderMyMediaSmall}</option>
                <option value="activerecordings">\${HeaderActiveRecordings}</option>
                <option value="resume">\${HeaderContinueWatching}</option>
                <option value="resumeaudio">\${HeaderContinueListening}</option>
                <option value="resumebook">\${HeaderContinueReading}</option>
                <option value="latestmedia">\${HeaderLatestMedia}</option>
                <option value="nextup">\${NextUp}</option>
                <option value="livetv">\${LiveTV}</option>
                <option value="none">\${None}</option>
            </select>
        </div>
        <div class="selectContainer">
            <select is="emby-select" id="selectHomeSection2" label="{section2label}">
                <option value="smalllibrarytiles">\${HeaderMyMedia}</option>
                <option value="librarybuttons">\${HeaderMyMediaSmall}</option>
                <option value="activerecordings">\${HeaderActiveRecordings}</option>
                <option value="resume">\${HeaderContinueWatching}</option>
                <option value="resumeaudio">\${HeaderContinueListening}</option>
                <option value="resumebook">\${HeaderContinueReading}</option>
                <option value="latestmedia">\${HeaderLatestMedia}</option>
                <option value="nextup">\${NextUp}</option>
                <option value="livetv">\${LiveTV}</option>
                <option value="none">\${None}</option>
            </select>
        </div>
        <div class="selectContainer">
            <select is="emby-select" id="selectHomeSection3" label="{section3label}">
                <option value="smalllibrarytiles">\${HeaderMyMedia}</option>
                <option value="librarybuttons">\${HeaderMyMediaSmall}</option>
                <option value="activerecordings">\${HeaderActiveRecordings}</option>
                <option value="resume">\${HeaderContinueWatching}</option>
                <option value="resumeaudio">\${HeaderContinueListening}</option>
                <option value="resumebook">\${HeaderContinueReading}</option>
                <option value="latestmedia">\${HeaderLatestMedia}</option>
                <option value="nextup">\${NextUp}</option>
                <option value="livetv">\${LiveTV}</option>
                <option value="none">\${None}</option>
            </select>
        </div>
        <div class="selectContainer">
            <select is="emby-select" id="selectHomeSection4" label="{section4label}">
                <option value="smalllibrarytiles">\${HeaderMyMedia}</option>
                <option value="librarybuttons">\${HeaderMyMediaSmall}</option>
                <option value="activerecordings">\${HeaderActiveRecordings}</option>
                <option value="resume">\${HeaderContinueWatching}</option>
                <option value="resumeaudio">\${HeaderContinueListening}</option>
                <option value="resumebook">\${HeaderContinueReading}</option>
                <option value="latestmedia">\${HeaderLatestMedia}</option>
                <option value="nextup">\${NextUp}</option>
                <option value="livetv">\${LiveTV}</option>
                <option value="none">\${None}</option>
            </select>
        </div>
        <div class="selectContainer">
            <select is="emby-select" id="selectHomeSection5" label="{section5label}">
                <option value="smalllibrarytiles">\${HeaderMyMedia}</option>
                <option value="librarybuttons">\${HeaderMyMediaSmall}</option>
                <option value="activerecordings">\${HeaderActiveRecordings}</option>
                <option value="resume">\${HeaderContinueWatching}</option>
                <option value="resumeaudio">\${HeaderContinueListening}</option>
                <option value="resumebook">\${HeaderContinueReading}</option>
                <option value="latestmedia">\${HeaderLatestMedia}</option>
                <option value="nextup">\${NextUp}</option>
                <option value="livetv">\${LiveTV}</option>
                <option value="none">\${None}</option>
            </select>
        </div>
        <div class="selectContainer">
            <select is="emby-select" id="selectHomeSection6" label="{section6label}">
                <option value="smalllibrarytiles">\${HeaderMyMedia}</option>
                <option value="librarybuttons">\${HeaderMyMediaSmall}</option>
                <option value="activerecordings">\${HeaderActiveRecordings}</option>
                <option value="resume">\${HeaderContinueWatching}</option>
                <option value="resumeaudio">\${HeaderContinueListening}</option>
                <option value="resumebook">\${HeaderContinueReading}</option>
                <option value="latestmedia">\${HeaderLatestMedia}</option>
                <option value="nextup">\${NextUp}</option>
                <option value="livetv">\${LiveTV}</option>
                <option value="none">\${None}</option>
            </select>
        </div>
        <div class="selectContainer">
            <select is="emby-select" id="selectHomeSection7" label="{section7label}">
                <option value="smalllibrarytiles">\${HeaderMyMedia}</option>
                <option value="librarybuttons">\${HeaderMyMediaSmall}</option>
                <option value="activerecordings">\${HeaderActiveRecordings}</option>
                <option value="resume">\${HeaderContinueWatching}</option>
                <option value="resumeaudio">\${HeaderContinueListening}</option>
                <option value="resumebook">\${HeaderContinueReading}</option>
                <option value="latestmedia">\${HeaderLatestMedia}</option>
                <option value="nextup">\${NextUp}</option>
                <option value="livetv">\${LiveTV}</option>
                <option value="none">\${None}</option>
            </select>
        </div>
        <div class="selectContainer">
            <select is="emby-select" id="selectHomeSection8" label="{section8label}">
                <option value="smalllibrarytiles">\${HeaderMyMedia}</option>
                <option value="librarybuttons">\${HeaderMyMediaSmall}</option>
                <option value="activerecordings">\${HeaderActiveRecordings}</option>
                <option value="resume">\${HeaderContinueWatching}</option>
                <option value="resumeaudio">\${HeaderContinueListening}</option>
                <option value="resumebook">\${HeaderContinueReading}</option>
                <option value="latestmedia">\${HeaderLatestMedia}</option>
                <option value="nextup">\${NextUp}</option>
                <option value="livetv">\${LiveTV}</option>
                <option value="none">\${None}</option>
            </select>
        </div>
        <div class="selectContainer">
            <select is="emby-select" id="selectHomeSection9" label="{section9label}">
                <option value="smalllibrarytiles">\${HeaderMyMedia}</option>
                <option value="librarybuttons">\${HeaderMyMediaSmall}</option>
                <option value="activerecordings">\${HeaderActiveRecordings}</option>
                <option value="resume">\${HeaderContinueWatching}</option>
                <option value="resumeaudio">\${HeaderContinueListening}</option>
                <option value="resumebook">\${HeaderContinueReading}</option>
                <option value="latestmedia">\${HeaderLatestMedia}</option>
                <option value="nextup">\${NextUp}</option>
                <option value="livetv">\${LiveTV}</option>
                <option value="none">\${None}</option>
            </select>
        </div>
        <div class="selectContainer">
            <select is="emby-select" id="selectHomeSection10" label="{section10label}">
                <option value="smalllibrarytiles">\${HeaderMyMedia}</option>
                <option value="librarybuttons">\${HeaderMyMediaSmall}</option>
                <option value="activerecordings">\${HeaderActiveRecordings}</option>
                <option value="resume">\${HeaderContinueWatching}</option>
                <option value="resumeaudio">\${HeaderContinueListening}</option>
                <option value="resumebook">\${HeaderContinueReading}</option>
                <option value="latestmedia">\${HeaderLatestMedia}</option>
                <option value="nextup">\${NextUp}</option>
                <option value="livetv">\${LiveTV}</option>
                <option value="none">\${None}</option>
            </select>
        </div>
    </div>

    <div class="verticalSection verticalSection-extrabottompadding">
        <h2 class="sectionTitle">\${HeaderLibraryOrder}</h2>
        <div class="paperList viewOrderList"></div>
    </div>

    <div class="perLibrarySettings"></div>

    <div class="verticalSection verticalSection-extrabottompadding">
        <h2 class="sectionTitle">\${HeaderLibraryFolders}</h2>
        <div>
            <p>\${LabelSelectFolderGroups}</p>
            <div class="folderGroupList"></div>
            <div class="fieldDescription checkboxFieldDescription">\${LabelSelectFolderGroupsHelp}</div>
        </div>
    </div>

    <button is="emby-button" type="submit" class="raised button-submit block btnSave hide">
        <span>\${Save}</span>
    </button>
</form>
`,y=10;function x(e,n,t){let o="";o+='<div class="checkboxList">',o+=t.map(i=>{let a="";const r=`chkGroupFolder${i.Id}`,c=n.Configuration.GroupedFolders?.includes(i.Id)?' checked="checked"':"";return a+="<label>",a+=`<input type="checkbox" is="emby-checkbox" class="chkGroupFolder" data-folderid="${u(i.Id||"")}" id="${u(r)}"${c}/>`,a+=`<span>${u(i.Name||"")}</span>`,a+="</label>",a}).join(""),o+="</div>",e.querySelector(".folderGroupList").innerHTML=o}function A(e){const n=[];return e==="movies"?n.push({name:l.translate("Movies"),value:s.Movies,isDefault:!0},{name:l.translate("Suggestions"),value:s.Suggestions},{name:l.translate("Favorites"),value:s.Favorites},{name:l.translate("Collections"),value:s.Collections},{name:l.translate("Genres"),value:s.Genres},{name:l.translate("Studios"),value:s.Studios},{name:l.translate("Playlists"),value:s.Playlists}):e==="tvshows"?n.push({name:l.translate("Shows"),value:s.Series,isDefault:!0},{name:l.translate("Suggestions"),value:s.Suggestions},{name:l.translate("TabUpcoming"),value:s.Upcoming},{name:l.translate("Genres"),value:s.Genres},{name:l.translate("Studios"),value:s.Studios},{name:l.translate("Episodes"),value:s.Episodes},{name:l.translate("Collections"),value:s.Collections},{name:l.translate("Playlists"),value:s.Playlists}):e==="music"?n.push({name:l.translate("Albums"),value:s.Albums,isDefault:!0},{name:l.translate("Suggestions"),value:s.Suggestions},{name:l.translate("HeaderAlbumArtists"),value:s.AlbumArtists},{name:l.translate("Artists"),value:s.Artists},{name:l.translate("Playlists"),value:s.Playlists},{name:l.translate("Songs"),value:s.Songs},{name:l.translate("Genres"),value:s.Genres},{name:l.translate("Collections"),value:s.Collections}):e==="livetv"?n.push({name:l.translate("Programs"),value:s.Programs,isDefault:!0},{name:l.translate("Guide"),value:s.Guide},{name:l.translate("Channels"),value:s.Channels},{name:l.translate("Recordings"),value:s.Recordings},{name:l.translate("Schedule"),value:s.Schedule},{name:l.translate("Series"),value:s.SeriesTimers}):e==="homevideos"?n.push({name:l.translate("Folders"),value:s.Folders,isDefault:!0},{name:l.translate("Photos"),value:s.Photos},{name:l.translate("HeaderPhotoAlbums"),value:s.PhotoAlbums},{name:l.translate("HeaderVideos"),value:s.Videos}):e==="musicvideos"?n.push({name:l.translate("Folders"),value:s.Folders,isDefault:!0},{name:l.translate("Suggestions"),value:s.Suggestions},{name:l.translate("HeaderVideos"),value:s.MusicVideos},{name:l.translate("Playlists"),value:s.Playlists}):e==="mixed"&&n.push({name:l.translate("Folders"),value:s.Folders,isDefault:!0},{name:l.translate("Suggestions"),value:s.Suggestions},{name:l.translate("HeaderMedia"),value:s.Mixed},{name:l.translate("Collections"),value:s.Collections},{name:l.translate("Playlists"),value:s.Playlists}),n}function q(e,n){return A(e).map(t=>{const i=n===t.value||t.isDefault&&!n?" selected":"",a=t.isDefault?"":t.value;return`<option value="${u(a)}"${i}>${u(t.name)}</option>`}).join("")}function F(e,n,t){let o="";o+=t.Items.map(i=>{let a="";return a+=`<div class="listItem viewItem" data-viewid="${u(i.Id||"")}">`,a+='<span class="material-icons listItemIcon folder_open" aria-hidden="true"></span>',a+='<div class="listItemBody">',a+="<div>",a+=u(i.Name||""),a+="</div>",a+="</div>",a+=`<button type="button" is="paper-icon-button-light" class="btnViewItemUp btnViewItemMove autoSize" title="${l.translate("Up")}"><span class="material-icons keyboard_arrow_up" aria-hidden="true"></span></button>`,a+=`<button type="button" is="paper-icon-button-light" class="btnViewItemDown btnViewItemMove autoSize" title="${l.translate("Down")}"><span class="material-icons keyboard_arrow_down" aria-hidden="true"></span></button>`,a+="</div>",a}).join(""),e.querySelector(".viewOrderList").innerHTML=o}function N(e,n){for(let t=1;t<=y;t++){const o=e.querySelector(`#selectHomeSection${t}`),i=V.getDefaultSection(t-1),a=o.querySelector(`option[value="${i}"]`)||o.querySelector('option[value=""]'),r=n.get(`homesection${t-1}`);a&&(a.value=""),r===i||!r?o.value="":o.value=r}e.querySelector(".selectTVHomeScreen").value=n.get("tvhome")||""}function U(e,n,t){const o=e.Type==="CollectionFolder"&&e.CollectionType==null?"mixed":e.CollectionType;let i="",a;if((e.Type==="Channel"||o==="boxsets"||o==="playlists")&&(a=!(n.Configuration.MyMediaExcludes||[]).includes(e.Id),i+="<div>",i+="<label>",i+=`<input type="checkbox" is="emby-checkbox" class="chkIncludeInMyMedia" data-folderid="${u(e.Id||"")}"${a?' checked="checked"':""}/>`,i+=`<span>${l.translate("DisplayInMyMedia")}</span>`,i+="</label>",i+="</div>"),["playlists","livetv","boxsets","channels"].includes(o||"")||(a=!n.Configuration.LatestItemsExcludes?.includes(e.Id),i+='<label class="fldIncludeInLatest">',i+=`<input type="checkbox" is="emby-checkbox" class="chkIncludeInLatest" data-folderid="${u(e.Id||"")}"${a?' checked="checked"':""}/>`,i+=`<span>${l.translate("DisplayInOtherHomeScreenSections")}</span>`,i+="</label>"),i&&(i=`<div class="checkboxListContainer">${i}</div>`),["movies","tvshows","music","livetv","homevideos","musicvideos","mixed"].includes(o||"")){const c=o==="livetv"?o:e.Id;i+='<div class="selectContainer">',i+=`<select is="emby-select" class="selectLanding" data-folderid="${u(c||"")}" label="${u(l.translate("LabelDefaultScreen"))}">`;const p=t.get(`landing-${c}`);i+=q(o||"",p),i+="</select>",i+="</div>"}if(i){let c="";c+='<div class="verticalSection">',c+='<h2 class="sectionTitle">',c+=u(e.Name||""),c+="</h2>",i=c+i,i+="</div>"}return i}function D(e,n,t,o){const i=e.querySelector(".perLibrarySettings");let a="";for(let r=0,d=t.length;r<d;r++)a+=U(t[r],n,o);i.innerHTML=a}async function P(e,n,t,o){e.querySelector(".chkHidePlayedFromLatest").checked=n.Configuration.HidePlayedInLatest||!1,N(e,t);const i=H.fetchQuery(w(f(o),{userId:n.Id,includeHidden:!0})),a=o.getJSON(o.getUrl(`Users/${n.Id}/GroupingOptions`)),r=await Promise.all([i,a]);F(e,n,r[0]),D(e,n,r[0].Items,t),x(e,n,r[1])}function R(e){const n=v.parentWithClass(e.target,"btnViewItemMove");if(n){const t=v.parentWithClass(n,"viewItem");if(t)if(n.classList.contains("btnViewItemDown")){const o=t.nextSibling;o&&(t.parentNode?.removeChild(t),o.parentNode?.insertBefore(t,o.nextSibling),b.focus(e.target))}else{const o=t.previousSibling;o&&(t.parentNode?.removeChild(t),o.parentNode?.insertBefore(t,o),b.focus(e.target))}}}function m(e,n,t){const o=n.querySelectorAll(e),i=[];for(let a=0,r=o.length;a<r;a++)o[a].checked===t&&i.push(o[a]);return i}function G(e,n,t,o){n.Configuration.HidePlayedInLatest=e.querySelector(".chkHidePlayedFromLatest").checked,n.Configuration.LatestItemsExcludes=m(".chkIncludeInLatest",e,!1).map(p=>p.getAttribute("data-folderid")||""),n.Configuration.MyMediaExcludes=m(".chkIncludeInMyMedia",e,!1).map(p=>p.getAttribute("data-folderid")||""),n.Configuration.GroupedFolders=m(".chkGroupFolder",e,!0).map(p=>p.getAttribute("data-folderid")||"");const i=e.querySelectorAll(".viewItem"),a=[];let r,d;for(r=0,d=i.length;r<d;r++)a.push(i[r].getAttribute("data-viewid")||"");n.Configuration.OrderedViews=a,t.set("tvhome",e.querySelector(".selectTVHomeScreen").value),t.set("homesection0",e.querySelector("#selectHomeSection1").value),t.set("homesection1",e.querySelector("#selectHomeSection2").value),t.set("homesection2",e.querySelector("#selectHomeSection3").value),t.set("homesection3",e.querySelector("#selectHomeSection4").value),t.set("homesection4",e.querySelector("#selectHomeSection5").value),t.set("homesection5",e.querySelector("#selectHomeSection6").value),t.set("homesection6",e.querySelector("#selectHomeSection7").value),t.set("homesection7",e.querySelector("#selectHomeSection8").value),t.set("homesection8",e.querySelector("#selectHomeSection9").value),t.set("homesection9",e.querySelector("#selectHomeSection10").value);const c=e.querySelectorAll(".selectLanding");for(r=0,d=c.length;r<d;r++){const p=c[r];t.set(`landing-${p.getAttribute("data-folderid")}`,p.value)}return o.updateUserConfiguration(n.Id,n.Configuration)}async function W(e,n,t,o,i,a){const r=await i.getUser(t);await G(n,r,o,i),a&&C(l.translate("SettingsSaved")),L.trigger(e,"saved")}function S(e){const n=this,t=g.getApiClient(n.options.serverId),o=n.options.userId,i=n.options.userSettings;h.withLoading(async()=>{try{await i.setUserInfo(o,t);const a=n.options.enableSaveConfirmation;await W(n,n.options.element,o,i,t,a)}catch(a){console.error("[homeScreenSettings] failed to save settings",a)}}),e&&e.preventDefault()}function E(e){const n=v.parentWithClass(e.target,"chkIncludeInMyMedia");if(!n)return;const o=v.parentWithClass(n,"verticalSection")?.querySelector(".fldIncludeInLatest");o&&(n.querySelector("input").checked?o.classList.remove("hide"):o.classList.add("hide"))}function O(e,n){let t=T;for(let o=1;o<=y;o++)t=t.replace(`{section${o}label}`,l.translate("LabelHomeScreenSectionValue",String(o)));e.element.innerHTML=l.translateHtml(t,"core"),e.element.querySelector(".viewOrderList")?.addEventListener("click",R),e.element.querySelector("form")?.addEventListener("submit",S.bind(n)),e.element.addEventListener("change",E),e.enableSaveButton&&e.element.querySelector(".btnSave")?.classList.remove("hide"),$.tv?e.element.querySelector(".selectTVHomeScreenContainer")?.classList.remove("hide"):e.element.querySelector(".selectTVHomeScreenContainer")?.classList.add("hide"),n.loadData(e.autoFocus)}class B{constructor(n){this.dataLoaded=!1,this.options=n,O(n,this)}loadData(n){const t=this,o=t.options.element,i=t.options.userId,a=g.getApiClient(t.options.serverId),r=t.options.userSettings;h.withLoading(async()=>{try{const d=await a.getUser(i);await r.setUserInfo(i,a),t.dataLoaded=!0,await P(o,d,r,a),n&&b.autoFocus(o)}catch(d){console.error("[homeScreenSettings] failed to load settings",d)}})}submit(){S.call(this)}destroy(){this.options=null}}const z=I;function xe(e,n){let t;const o=n.userId||ApiClient.getCurrentUserId(),i=o===ApiClient.getCurrentUserId()?M:new z;e.addEventListener("viewshow",function(){if(t){t.loadData();return}t=new B({serverId:ApiClient.serverId(),userId:o,element:e.querySelector(".homeScreenSettingsContainer"),userSettings:i,enableSaveButton:!0,enableSaveConfirmation:!0,autoFocus:k.isEnabled()})}),e.addEventListener("viewdestroy",function(){t&&(t.destroy(),t=void 0)})}export{xe as default};
