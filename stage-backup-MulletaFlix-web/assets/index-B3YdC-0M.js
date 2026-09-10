import{a2 as v,S as h,a$ as g,f as a,l as $,aC as H,T as f,d as m,e as d,W as C,E as L,bg as M,cW as I,bq as k}from"./index-6F8mVyyU.js";import{g as T}from"./useUserViews--1mvwwNw.js";import{h as V}from"./homesections-BfXPhkp9.js";import"./emby-select-BS-dUTXm.js";import{L as s}from"./libraryTab-BxTq0WIP.js";import"./item-fields-Bl5Ld9l1.js";import"./live-tv-api-tZ-oNikT.js";import"./cardBuilder-D1eLvnBe.js";import"./person-kind-mlhHzv9H.js";import"./itemAction-CSkKDKc6.js";import"./image-BEa7LKsb.js";import"./imageLoader-BuyzOWYq.js";import"./indicators-DOHxhk9F.js";/* empty css                   */import"./shortcuts-drWo9b_R.js";import"./recordinghelper-Uwa6E3ZG.js";/* empty css                 */import"./builder-gN4Aqr_k.js";import"./image-api-BSd5pBND.js";import"./tv-shows-api--VPbiJmq.js";import"./items-api-Dw3jobvc.js";import"./emby-itemscontainer-B8ZyPudq.js";import"./sortable.esm-BzJVkfZx.js";import"./emby-scroller-CHov0ueH.js";import"./index-DHiDgy7M.js";/* empty css                      */import"./actionSheet-bHfWrycr.js";const x=`<form style="margin:0 auto;">
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
`,S=10;function w(e,n,t){let o="";o+='<div class="checkboxList">',o+=t.map(i=>{let l="";const r=`chkGroupFolder${i.Id}`,c=n.Configuration.GroupedFolders?.includes(i.Id)?' checked="checked"':"";return l+="<label>",l+=`<input type="checkbox" is="emby-checkbox" class="chkGroupFolder" data-folderid="${d(i.Id||"")}" id="${d(r)}"${c}/>`,l+=`<span>${d(i.Name||"")}</span>`,l+="</label>",l}).join(""),o+="</div>",e.querySelector(".folderGroupList").innerHTML=o}function A(e){const n=[];return e==="movies"?n.push({name:a.translate("Movies"),value:s.Movies,isDefault:!0},{name:a.translate("Suggestions"),value:s.Suggestions},{name:a.translate("Favorites"),value:s.Favorites},{name:a.translate("Collections"),value:s.Collections},{name:a.translate("Genres"),value:s.Genres},{name:a.translate("Studios"),value:s.Studios},{name:a.translate("Playlists"),value:s.Playlists}):e==="tvshows"?n.push({name:a.translate("Shows"),value:s.Series,isDefault:!0},{name:a.translate("Suggestions"),value:s.Suggestions},{name:a.translate("TabUpcoming"),value:s.Upcoming},{name:a.translate("Genres"),value:s.Genres},{name:a.translate("Studios"),value:s.Studios},{name:a.translate("Episodes"),value:s.Episodes},{name:a.translate("Collections"),value:s.Collections},{name:a.translate("Playlists"),value:s.Playlists}):e==="music"?n.push({name:a.translate("Albums"),value:s.Albums,isDefault:!0},{name:a.translate("Suggestions"),value:s.Suggestions},{name:a.translate("HeaderAlbumArtists"),value:s.AlbumArtists},{name:a.translate("Artists"),value:s.Artists},{name:a.translate("Playlists"),value:s.Playlists},{name:a.translate("Songs"),value:s.Songs},{name:a.translate("Genres"),value:s.Genres},{name:a.translate("Collections"),value:s.Collections}):e==="livetv"?n.push({name:a.translate("Programs"),value:s.Programs,isDefault:!0},{name:a.translate("Guide"),value:s.Guide},{name:a.translate("Channels"),value:s.Channels},{name:a.translate("Recordings"),value:s.Recordings},{name:a.translate("Schedule"),value:s.Schedule},{name:a.translate("Series"),value:s.SeriesTimers}):e==="homevideos"?n.push({name:a.translate("Folders"),value:s.Folders,isDefault:!0},{name:a.translate("Photos"),value:s.Photos},{name:a.translate("HeaderPhotoAlbums"),value:s.PhotoAlbums},{name:a.translate("HeaderVideos"),value:s.Videos}):e==="musicvideos"?n.push({name:a.translate("Folders"),value:s.Folders,isDefault:!0},{name:a.translate("Suggestions"),value:s.Suggestions},{name:a.translate("HeaderVideos"),value:s.MusicVideos},{name:a.translate("Playlists"),value:s.Playlists}):e==="mixed"&&n.push({name:a.translate("Folders"),value:s.Folders,isDefault:!0},{name:a.translate("Suggestions"),value:s.Suggestions},{name:a.translate("HeaderMedia"),value:s.Mixed},{name:a.translate("Collections"),value:s.Collections},{name:a.translate("Playlists"),value:s.Playlists}),n}function q(e,n){return A(e).map(t=>{const i=n===t.value||t.isDefault&&!n?" selected":"",l=t.isDefault?"":t.value;return`<option value="${d(l)}"${i}>${d(t.name)}</option>`}).join("")}function F(e,n,t){let o="";o+=t.Items.map(i=>{let l="";return l+=`<div class="listItem viewItem" data-viewid="${d(i.Id||"")}">`,l+='<span class="material-icons listItemIcon folder_open" aria-hidden="true"></span>',l+='<div class="listItemBody">',l+="<div>",l+=d(i.Name||""),l+="</div>",l+="</div>",l+=`<button type="button" is="paper-icon-button-light" class="btnViewItemUp btnViewItemMove autoSize" title="${a.translate("Up")}"><span class="material-icons keyboard_arrow_up" aria-hidden="true"></span></button>`,l+=`<button type="button" is="paper-icon-button-light" class="btnViewItemDown btnViewItemMove autoSize" title="${a.translate("Down")}"><span class="material-icons keyboard_arrow_down" aria-hidden="true"></span></button>`,l+="</div>",l}).join(""),e.querySelector(".viewOrderList").innerHTML=o}function N(e,n){for(let t=1;t<=S;t++){const o=e.querySelector(`#selectHomeSection${t}`),i=V.getDefaultSection(t-1),l=o.querySelector(`option[value="${i}"]`)||o.querySelector('option[value=""]'),r=n.get(`homesection${t-1}`);l&&(l.value=""),r===i||!r?o.value="":o.value=r}e.querySelector(".selectTVHomeScreen").value=n.get("tvhome")||""}function U(e,n,t){const o=e.Type==="CollectionFolder"&&e.CollectionType==null?"mixed":e.CollectionType;let i="",l;if((e.Type==="Channel"||o==="boxsets"||o==="playlists")&&(l=!(n.Configuration.MyMediaExcludes||[]).includes(e.Id),i+="<div>",i+="<label>",i+=`<input type="checkbox" is="emby-checkbox" class="chkIncludeInMyMedia" data-folderid="${d(e.Id||"")}"${l?' checked="checked"':""}/>`,i+=`<span>${a.translate("DisplayInMyMedia")}</span>`,i+="</label>",i+="</div>"),["playlists","livetv","boxsets","channels"].includes(o||"")||(l=!n.Configuration.LatestItemsExcludes?.includes(e.Id),i+='<label class="fldIncludeInLatest">',i+=`<input type="checkbox" is="emby-checkbox" class="chkIncludeInLatest" data-folderid="${d(e.Id||"")}"${l?' checked="checked"':""}/>`,i+=`<span>${a.translate("DisplayInOtherHomeScreenSections")}</span>`,i+="</label>"),i&&(i=`<div class="checkboxListContainer">${i}</div>`),["movies","tvshows","music","livetv","homevideos","musicvideos","mixed"].includes(o||"")){const c=o==="livetv"?o:e.Id;i+='<div class="selectContainer">',i+=`<select is="emby-select" class="selectLanding" data-folderid="${d(c||"")}" label="${d(a.translate("LabelDefaultScreen"))}">`;const p=t.get(`landing-${c}`);i+=q(o||"",p),i+="</select>",i+="</div>"}if(i){let c="";c+='<div class="verticalSection">',c+='<h2 class="sectionTitle">',c+=d(e.Name||""),c+="</h2>",i=c+i,i+="</div>"}return i}function P(e,n,t,o){const i=e.querySelector(".perLibrarySettings");let l="";for(let r=0,u=t.length;r<u;r++)l+=U(t[r],n,o);i.innerHTML=l}function D(e,n,t,o){e.querySelector(".chkHidePlayedFromLatest").checked=n.Configuration.HidePlayedInLatest||!1,N(e,t);const i=H.fetchQuery(T(f(o),{userId:n.Id,includeHidden:!0})),l=o.getJSON(o.getUrl(`Users/${n.Id}/GroupingOptions`));Promise.all([i,l]).then(r=>{F(e,n,r[0]),P(e,n,r[0].Items,t),w(e,n,r[1]),v.hide()}).catch(()=>v.hide())}function R(e){const n=m.parentWithClass(e.target,"btnViewItemMove");if(n){const t=m.parentWithClass(n,"viewItem");if(t)if(n.classList.contains("btnViewItemDown")){const o=t.nextSibling;o&&(t.parentNode?.removeChild(t),o.parentNode?.insertBefore(t,o.nextSibling),g.focus(e.target))}else{const o=t.previousSibling;o&&(t.parentNode?.removeChild(t),o.parentNode?.insertBefore(t,o),g.focus(e.target))}}}function b(e,n,t){const o=n.querySelectorAll(e),i=[];for(let l=0,r=o.length;l<r;l++)o[l].checked===t&&i.push(o[l]);return i}function G(e,n,t,o){n.Configuration.HidePlayedInLatest=e.querySelector(".chkHidePlayedFromLatest").checked,n.Configuration.LatestItemsExcludes=b(".chkIncludeInLatest",e,!1).map(p=>p.getAttribute("data-folderid")||""),n.Configuration.MyMediaExcludes=b(".chkIncludeInMyMedia",e,!1).map(p=>p.getAttribute("data-folderid")||""),n.Configuration.GroupedFolders=b(".chkGroupFolder",e,!0).map(p=>p.getAttribute("data-folderid")||"");const i=e.querySelectorAll(".viewItem"),l=[];let r,u;for(r=0,u=i.length;r<u;r++)l.push(i[r].getAttribute("data-viewid")||"");n.Configuration.OrderedViews=l,t.set("tvhome",e.querySelector(".selectTVHomeScreen").value),t.set("homesection0",e.querySelector("#selectHomeSection1").value),t.set("homesection1",e.querySelector("#selectHomeSection2").value),t.set("homesection2",e.querySelector("#selectHomeSection3").value),t.set("homesection3",e.querySelector("#selectHomeSection4").value),t.set("homesection4",e.querySelector("#selectHomeSection5").value),t.set("homesection5",e.querySelector("#selectHomeSection6").value),t.set("homesection6",e.querySelector("#selectHomeSection7").value),t.set("homesection7",e.querySelector("#selectHomeSection8").value),t.set("homesection8",e.querySelector("#selectHomeSection9").value),t.set("homesection9",e.querySelector("#selectHomeSection10").value);const c=e.querySelectorAll(".selectLanding");for(r=0,u=c.length;r<u;r++){const p=c[r];t.set(`landing-${p.getAttribute("data-folderid")}`,p.value)}return o.updateUserConfiguration(n.Id,n.Configuration)}function W(e,n,t,o,i,l){v.show(),i.getUser(t).then(r=>G(n,r,o,i)).then(()=>{v.hide(),l&&C(a.translate("SettingsSaved")),L.trigger(e,"saved")}).catch(()=>{v.hide()})}function y(e){const n=this,t=h.getApiClient(n.options.serverId),o=n.options.userId,i=n.options.userSettings;i.setUserInfo(o,t).then(()=>{const l=n.options.enableSaveConfirmation;W(n,n.options.element,o,i,t,l)}).catch(()=>v.hide()),e&&e.preventDefault()}function E(e){const n=m.parentWithClass(e.target,"chkIncludeInMyMedia");if(!n)return;const o=m.parentWithClass(n,"verticalSection")?.querySelector(".fldIncludeInLatest");o&&(n.querySelector("input").checked?o.classList.remove("hide"):o.classList.add("hide"))}function O(e,n){let t=x;for(let o=1;o<=S;o++)t=t.replace(`{section${o}label}`,a.translate("LabelHomeScreenSectionValue",String(o)));e.element.innerHTML=a.translateHtml(t,"core"),e.element.querySelector(".viewOrderList")?.addEventListener("click",R),e.element.querySelector("form")?.addEventListener("submit",y.bind(n)),e.element.addEventListener("change",E),e.enableSaveButton&&e.element.querySelector(".btnSave")?.classList.remove("hide"),$.tv?e.element.querySelector(".selectTVHomeScreenContainer")?.classList.remove("hide"):e.element.querySelector(".selectTVHomeScreenContainer")?.classList.add("hide"),n.loadData(e.autoFocus)}class z{constructor(n){this.dataLoaded=!1,this.options=n,O(n,this)}loadData(n){const t=this,o=t.options.element;v.show();const i=t.options.userId,l=h.getApiClient(t.options.serverId),r=t.options.userSettings;l.getUser(i).then(u=>r.setUserInfo(i,l).then(()=>{t.dataLoaded=!0,D(o,u,r,l),n&&g.autoFocus(o)})).catch(()=>v.hide())}submit(){y.call(this)}destroy(){this.options=null}}const B=I;function ye(e,n){let t;const o=n.userId||ApiClient.getCurrentUserId(),i=o===ApiClient.getCurrentUserId()?M:new B;e.addEventListener("viewshow",function(){if(t){t.loadData();return}t=new z({serverId:ApiClient.serverId(),userId:o,element:e.querySelector(".homeScreenSettingsContainer"),userSettings:i,enableSaveButton:!0,enableSaveConfirmation:!0,autoFocus:k.isEnabled()})}),e.addEventListener("viewdestroy",function(){t&&(t.destroy(),t=void 0)})}export{ye as default};
