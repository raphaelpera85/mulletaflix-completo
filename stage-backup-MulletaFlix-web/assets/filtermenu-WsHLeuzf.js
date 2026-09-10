const __vite__mapDeps=(i,m=__vite__mapDeps,d=(m.f||(m.f=["./index-6F8mVyyU.js","./index-CQ6hRxbv.css"])))=>i.map(i=>d[i]);
import{l as u,c as h,f as m,S as F,$ as f,_ as L,cd as d,e as b,d as T,a$ as g}from"./index-6F8mVyyU.js";import"./emby-select-BS-dUTXm.js";import"./actionSheet-bHfWrycr.js";const I=`<div class="formDialogContent smoothScrollY">
    <div class="dialogContentInner dialog-content-centered">
        <form style="margin:auto;">

            <div class="verticalSection verticalSection-extrabottompadding basicFilterSection focuscontainer-x" style="margin-top:2em;">
                <div class="checkboxList checkboxList-verticalwrap">
                    <label class="viewSetting simpleFilter" data-settingname="IsUnplayed">
                        <input type="checkbox" is="emby-checkbox" class="chkUnplayed" />
                        <span>\${Unplayed}</span>
                    </label>
                    <label class="viewSetting simpleFilter" data-settingname="IsPlayed">
                        <input type="checkbox" is="emby-checkbox" class="chkPlayed" />
                        <span>\${Played}</span>
                    </label>
                    <label class="viewSetting simpleFilter" data-settingname="IsFavorite">
                        <input type="checkbox" is="emby-checkbox" class="chkFavorite" />
                        <span>\${Favorite}</span>
                    </label>
                    <label class="viewSetting simpleFilter" data-settingname="IsResumable">
                        <input type="checkbox" is="emby-checkbox" class="chkResumable" />
                        <span>\${ContinueWatching}</span>
                    </label>
                </div>
            </div>

            <div class="verticalSection verticalSection-extrabottompadding viewSetting focuscontainer-x" data-settingname="SeriesStatus">
                <h2 class="checkboxListLabel">\${HeaderSeriesStatus}</h2>
                <div class="checkboxList checkboxList-verticalwrap">

                    <label>
                        <input type="checkbox" is="emby-checkbox" class="chkSeriesStatus" data-filter="Continuing" />
                        <span>\${Continuing}</span>
                    </label>
                    <label>
                        <input type="checkbox" is="emby-checkbox" class="chkSeriesStatus" data-filter="Ended" />
                        <span>\${Ended}</span>
                    </label>
                    <label>
                        <input type="checkbox" is="emby-checkbox" class="chkSeriesStatus" data-filter="Unreleased" />
                        <span>\${Unreleased}</span>
                    </label>
                </div>
            </div>

            <div class="verticalSection verticalSection-extrabottompadding hide genreFilters focuscontainer-x">
                <h2 class="checkboxListLabel">\${Genres}</h2>
                <div class="checkboxList checkboxList-verticalwrap filterOptions">
                </div>
            </div>

            <div class="verticalSection verticalSection-extrabottompadding viewSetting focuscontainer-x" data-settingname="VideoType">
                <h2 class="checkboxListLabel">\${HeaderVideoType}</h2>
                <div class="checkboxList checkboxList-verticalwrap">

                    <label>
                        <input type="checkbox" is="emby-checkbox" class="simpleFilter chkHDFilter" data-settingname="IsHD" />
                        <span>HD</span>
                    </label>

                    <label>
                        <input type="checkbox" is="emby-checkbox" class="simpleFilter chk4KFilter" data-settingname="Is4K" />
                        <span>4K</span>
                    </label>

                    <label>
                        <input type="checkbox" is="emby-checkbox" class="simpleFilter chkSDFilter" data-settingname="IsSD" />
                        <span>SD</span>
                    </label>

                    <label>
                        <input type="checkbox" is="emby-checkbox" class="simpleFilter chk3DFilter" data-settingname="Is3D" />
                        <span>3D</span>
                    </label>
                    <label>
                        <input type="checkbox" is="emby-checkbox" class="chkVideoTypeFilter" data-filter="Bluray" />
                        <span>Blu-ray</span>
                    </label>
                    <label>
                        <input type="checkbox" is="emby-checkbox" class="chkVideoTypeFilter" data-filter="Dvd" />
                        <span>DVD</span>
                    </label>
                </div>
            </div>

            <div class="verticalSection verticalSection-extrabottompadding featureSection hide focuscontainer-x">
                <h2 class="checkboxListLabel">\${Features}</h2>
                <div class="checkboxList checkboxList-verticalwrap">
                    <label class="viewSetting simpleFilter" data-settingname="HasSubtitles">
                        <input type="checkbox" is="emby-checkbox" class="chkFeatureFilter chkSubtitle" />
                        <span>\${Subtitles}</span>
                    </label>
                    <label class="viewSetting simpleFilter" data-settingname="HasTrailer">
                        <input type="checkbox" is="emby-checkbox" class="chkFeatureFilter chkTrailer" />
                        <span>\${Trailers}</span>
                    </label>
                    <label class="viewSetting simpleFilter" data-settingname="HasSpecialFeature">
                        <input type="checkbox" is="emby-checkbox" class="chkFeatureFilter chkSpecialFeature" />
                        <span>\${Extras}</span>
                    </label>
                    <label class="viewSetting simpleFilter" data-settingname="HasThemeSong">
                        <input type="checkbox" is="emby-checkbox" class="chkFeatureFilter chkThemeSong" />
                        <span>\${ThemeSongs}</span>
                    </label>
                    <label class="viewSetting simpleFilter" data-settingname="HasThemeVideo">
                        <input type="checkbox" is="emby-checkbox" class="chkFeatureFilter chkThemeVideo" />
                        <span>\${ThemeVideos}</span>
                    </label>
                </div>
            </div>
        </form>
    </div>
</div>
`;function w(a){return a.preventDefault(),!1}function D(a,n,i,e,t){const s=a.querySelector(n),l=s?.querySelector(".filterOptions");if(!s||!l)return;e.length?s.classList.remove("hide"):s.classList.add("hide");let r="";r+=e.map(function(c){let o="";const x=t(c)?" checked":"";return o+="<label>",o+='<input is="emby-checkbox" type="checkbox"'+x+' data-filter="'+b(String(c.Id??""))+'" class="'+b(i)+'"/>',o+="<span>"+b(c.Name)+"</span>",o+="</label>",o}).join(""),l.innerHTML=r}function q(a,n,i){D(a,".genreFilters","chkGenreFilter",n.Genres??[],function(e){const t=i.settings.GenreIds.indexOf("|")===-1?",":"|";return(t+i.settings.GenreIds+t).indexOf(t+(e.Id||"")+t)!==-1})}function A(a,n){d(a,n.checked?"true":"")}function k(a,n){const i=a[n];return i===!0||i==="true"}function S(a,n){const i=T.parentWithClass(a,"checkboxList-verticalwrap"),e=g.getFocusableElements(i);let t=-1;for(let l=0,r=e.length;l<r;l++)if(e[l]===a){t=l;break}t+=n,t=Math.min(e.length-1,t),t=Math.max(0,t);const s=e[t];s&&g.focus(s)}function y(a,n,i){L(()=>import("./index-6F8mVyyU.js").then(e=>e.fj),__vite__mapDeps([0,1]),import.meta.url).then(e=>{const t=i?"on":"off";e.centerFocus[t](a,n)}).catch(e=>console.error("[FilterMenu] failed to center focus",e))}function v(a){const n=a;switch(n.detail?.command){case"left":S(n.target,-1),a.preventDefault();break;case"right":S(n.target,1),a.preventDefault();break}}function $(a,n){a.querySelectorAll(".simpleFilter").forEach(s=>{const l=s instanceof HTMLInputElement?s:s.querySelector("input"),r=s.getAttribute("data-settingname");l&&r&&A(n+"-filter-"+r,l)});const i=[];a.querySelectorAll(".chkVideoTypeFilter").forEach(s=>{s.checked&&s.getAttribute("data-filter")&&i.push(s.getAttribute("data-filter"))}),d(n+"-filter-VideoTypes",i.join(","));const e=[];a.querySelectorAll(".chkSeriesStatus").forEach(s=>{s.checked&&s.getAttribute("data-filter")&&e.push(s.getAttribute("data-filter"))}),d(`${n}-filter-SeriesStatus`,e.join(","));const t=[];a.querySelectorAll(".chkGenreFilter").forEach(s=>{s.checked&&s.getAttribute("data-filter")&&t.push(s.getAttribute("data-filter"))}),d(n+"-filter-GenreIds",t.join(","))}function p(a,n){const i=a.querySelectorAll(".checkboxList-verticalwrap");for(let e=0,t=i.length;e<t;e++)n?f.on(i[e],v):f.off(i[e],v)}function H(a,n){a.querySelector("form")?.addEventListener("submit",w);let i=a.querySelectorAll(".simpleFilter"),e,t;for(e=0,t=i.length;e<t;e++)if(i[e].tagName==="INPUT"){const r=i[e];r.checked=k(n,i[e].getAttribute("data-settingname")||"")}else{const r=i[e].querySelector("input");r&&(r.checked=k(n,i[e].getAttribute("data-settingname")||""))}const s=n.VideoTypes?n.VideoTypes.split(","):[];for(i=a.querySelectorAll(".chkVideoTypeFilter"),e=0,t=i.length;e<t;e++)i[e].checked=s.indexOf(i[e].getAttribute("data-filter")||"")!==-1;const l=n.SeriesStatus?n.SeriesStatus.split(","):[];for(i=a.querySelectorAll(".chkSeriesStatus"),e=0,t=i.length;e<t;e++)i[e].checked=l.indexOf(i[e].getAttribute("data-filter")||"")!==-1;a.querySelector(".basicFilterSection .viewSetting:not(.hide)")?a.querySelector(".basicFilterSection")?.classList.remove("hide"):a.querySelector(".basicFilterSection")?.classList.add("hide"),a.querySelector(".featureSection .viewSetting:not(.hide)")?a.querySelector(".featureSection")?.classList.remove("hide"):a.querySelector(".featureSection")?.classList.add("hide")}function E(a,n){if(!n.serverId)return;const i=F.getApiClient(n.serverId),e=Object.assign({},n.filterMenuOptions,{UserId:i.getCurrentUserId(),ParentId:n.parentId,IncludeItemTypes:n.itemTypes.join(",")});i.getFilters(e).then(t=>{q(a,t,n)}).catch(t=>console.error("[FilterMenu] failed to load dynamic filters",t))}class M{show(n){return new Promise(i=>{const e={removeOnClose:!0,scrollY:!1};u.tv?e.size="fullscreen":e.size="small";const t=h.createDialog(e);t.classList.add("formDialog");let s="";s+='<div class="formDialogHeader">',s+=`<button is="paper-icon-button-light" class="btnCancel hide-mouse-idle-tv" tabindex="-1" title="${m.translate("ButtonBack")}"><span class="material-icons arrow_back" aria-hidden="true"></span></button>`,s+='<h3 class="formDialogHeaderTitle">${Filters}</h3>',s+="</div>",s+=I,t.innerHTML=m.translateHtml(s,"core");const l=t.querySelectorAll(".viewSetting");for(let c=0,o=l.length;c<o;c++)n.visibleSettings.indexOf(l[c].getAttribute("data-settingname")||"")===-1?l[c].classList.add("hide"):l[c].classList.remove("hide");if(H(t,n.settings),E(t,n),p(t,!0),t.querySelector(".btnCancel")?.addEventListener("click",function(){h.close(t)}),u.tv){const c=t.querySelector(".formDialogContent");c&&y(c,!1,!0)}let r;t.querySelector("form")?.addEventListener("change",function(){r=!0},!0),h.open(t).then(function(){if(p(t,!1),u.tv){const c=t.querySelector(".formDialogContent");c&&y(c,!1,!1)}return r&&$(t,n.settingsKey),i()}).catch(c=>{p(t,!1),console.error("[FilterMenu] failed to open dialog",c),i()})})}}export{M as default};
