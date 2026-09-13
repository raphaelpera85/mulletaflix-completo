import{h as I,G as q,s as O,S as A,E as V,B as L}from"./index-CFwqrzSZ.js";import{u as R}from"./vendor-lodash-CYyrkkBC.js";import"./emby-checkbox-CDwDlGOj.js";import"./emby-collapse-Jnj7ypJU.js";import{stopMultiSelect as N}from"./multiSelect-D8x3r-bh.js";import{g as U}from"./filterIndicator-UULZ9d1m.js";import"./vendor-react-query-BiAmjtRH.js";import"./vendor-react-CeUY3tRn.js";import"./vendor-jellyfin-Bee54tkY.js";import"./vendor-axios-BCn5QfZZ.js";import"./vendor-date-fns-CwLI6ukS.js";import"./vendor-dompurify-Baz99PXY.js";const G=`<div style="margin: 0;padding:1.5em 2em;" class="filterDialogContent">

    <div is="emby-collapse" title="\${Filters}">
        <div class="collapseContent">
            <div class="checkboxList">
                <label class="videoStandard">
                    <input type="checkbox" is="emby-checkbox" class="chkStandardFilter"
                        data-filter="IsPlayed" />
                    <span>\${Played}</span>
                </label>
                <label class="videoStandard">
                    <input type="checkbox" is="emby-checkbox" class="chkStandardFilter"
                        data-filter="IsUnPlayed" />
                    <span>\${Unplayed}</span>
                </label>
                <label class="videoStandard">
                    <input type="checkbox" is="emby-checkbox" class="chkStandardFilter"
                        data-filter="IsResumable" />
                    <span>\${OptionResumable}</span>
                </label>
                <label>
                    <input type="checkbox" is="emby-checkbox" class="chkStandardFilter chkFavorite"
                        data-filter="IsFavorite" />
                    <span>\${Favorites}</span>
                </label>
                <label class="episodeFilter hide">
                    <input type="checkbox" is="emby-checkbox" id="chkSpecialEpisode" />
                    <span>\${OptionSpecialEpisode}</span>
                </label>
                <label class="episodeFilter hide">
                    <input type="checkbox" is="emby-checkbox" id="chkMissingEpisode" />
                    <span>\${OptionMissingEpisode}</span>
                </label>
                <label class="episodeFilter hide">
                    <input type="checkbox" is="emby-checkbox" id="chkFutureEpisode" />
                    <span>\${OptionUnairedEpisode}</span>
                </label>
            </div>
        </div>
    </div>

    <div is="emby-collapse" title="\${HeaderStatus}" class="seriesStatus hide">
        <div class="collapseContent">
            <div class="checkboxList">
                <label>
                    <input type="checkbox" is="emby-checkbox" class="chkStatus" data-filter="Continuing" />
                    <span>\${Continuing}</span>
                </label>
                <label>
                    <input type="checkbox" is="emby-checkbox" class="chkStatus" data-filter="Ended" />
                    <span>\${Ended}</span>
                </label>
                <label>
                    <input type="checkbox" is="emby-checkbox" class="chkStatus" data-filter="Unreleased" />
                    <span>\${Unreleased}</span>
                </label>
            </div>
        </div>
    </div>

    <div is="emby-collapse" title="\${Features}" class="features hide">
        <div class="collapseContent">
            <div class="checkboxList">
                <label>
                    <input type="checkbox" is="emby-checkbox" class="chkFeatureFilter" id="chkSubtitle" />
                    <span>\${Subtitles}</span>
                </label>
                <label>
                    <input type="checkbox" is="emby-checkbox" class="chkFeatureFilter" id="chkTrailer" />
                    <span>\${ButtonTrailer}</span>
                </label>
                <label>
                    <input type="checkbox" is="emby-checkbox" class="chkFeatureFilter" id="chkSpecialFeature" />
                    <span>\${SpecialFeatures}</span>
                </label>
                <label>
                    <input type="checkbox" is="emby-checkbox" class="chkFeatureFilter" id="chkThemeSong" />
                    <span>\${OptionHasThemeSong}</span>
                </label>
                <label>
                    <input type="checkbox" is="emby-checkbox" class="chkFeatureFilter" id="chkThemeVideo" />
                    <span>\${OptionHasThemeVideo}</span>
                </label>
            </div>
        </div>
    </div>

    <div is="emby-collapse" title="\${Genres}" class="genreFilters hide">
        <div class="collapseContent filterOptions">
        </div>
    </div>

    <div is="emby-collapse" title="\${HeaderParentalRatings}" class="officialRatingFilters hide">
        <div class="collapseContent filterOptions">
        </div>
    </div>

    <div is="emby-collapse" title="\${Tags}" class="tagFilters hide">
        <div class="collapseContent filterOptions">
        </div>
    </div>

    <div is="emby-collapse" title="\${HeaderVideoTypes}" class="videoTypeFilters hide">
        <div class="collapseContent">
            <div class="checkboxList">
                <label>
                    <input type="checkbox" is="emby-checkbox" class="chkVideoTypeFilter chkBluray"
                        data-filter="Bluray" />
                    <span>\${OptionBluray}</span>
                </label>
                <label>
                    <input type="checkbox" is="emby-checkbox" class="chkVideoTypeFilter chkDvd" data-filter="Dvd" />
                    <span>\${OptionDvd}</span>
                </label>

                <label>
                    <input type="checkbox" is="emby-checkbox" class="chkHDFilter IsHD" />
                    <span>\${OptionIsHD}</span>
                </label>

                <label>
                    <input type="checkbox" is="emby-checkbox" class="chk4KFilter Is4K" />
                    <span>4K</span>
                </label>

                <label>
                    <input type="checkbox" is="emby-checkbox" class="chkSDFilter IsHD" />
                    <span>\${OptionIsSD}</span>
                </label>

                <label>
                    <input type="checkbox" is="emby-checkbox" class="chk3DFilter chk3D" />
                    <span>\${Option3D}</span>
                </label>
            </div>
        </div>
    </div>

    <div is="emby-collapse" title="\${HeaderYears}" class="yearFilters hide">
        <div class="collapseContent filterOptions">
        </div>
    </div>

    <button is="emby-button" type="submit" class="resetFilters raised">
        <span class="material-icons clear" aria-hidden="true"></span>
        <span>\${ResetFilters}</span>
    </button>
</div>
`;function T(i,t,e){return t?R(i,t.split(e)).sort():i}function $(i,t,e,s,l){const p=i.querySelector(t);if(!p)return;s.length?p.classList.remove("hide"):p.classList.add("hide");let h="";h+='<div class="checkboxList">',h+=s.map(function(f){let u="";const m=l(f)?"checked":"";return u+="<label>",u+=`<input is="emby-checkbox" type="checkbox" ${m} data-filter="${L(f)}" class="${L(e)}"/>`,u+=`<span>${L(f)}</span>`,u+="</label>",u}).join(""),h+="</div>";const k=p.querySelector(".filterOptions");k&&(k.innerHTML=h)}function M(i,t,e){$(i,".genreFilters","chkGenreFilter",T(t.Genres,e.Genres??null,"|"),function(s){return("|"+(e.Genres||"")+"|").includes("|"+s+"|")}),$(i,".officialRatingFilters","chkOfficialRatingFilter",T(t.OfficialRatings,e.OfficialRatings,"|"),function(s){return("|"+(e.OfficialRatings||"")+"|").includes("|"+s+"|")}),$(i,".tagFilters","chkTagFilter",T(t.Tags,e.Tags,"|"),function(s){return("|"+(e.Tags||"")+"|").includes("|"+s+"|")}),$(i,".yearFilters","chkYearFilter",T(t.Years.map(String),e.Years,","),function(s){return(","+(e.Years||"")+",").includes(","+s+",")})}function K(i,t,e,s){return t.getJSON(t.getUrl("Items/Filters",{UserId:e,ParentId:s.ParentId,IncludeItemTypes:s.IncludeItemTypes})).then(l=>{M(i,l,s)})}function Y(i,t){const e=t.query;if(t.mode==="livetvchannels"){const d=i.querySelector(".chkFavorite");d&&(d.checked=e.IsFavorite===!0)}else for(const d of i.querySelectorAll(".chkStandardFilter")){const a=`,${e.Filters||""}`,b=d.getAttribute("data-filter")??"";d.checked=a.includes(`,${b}`)}for(const d of i.querySelectorAll(".chkVideoTypeFilter")){const a=`,${e.VideoTypes||""}`,b=d.getAttribute("data-filter")??"";d.checked=a.includes(`,${b}`)}const s=i.querySelector(".chk3DFilter"),l=i.querySelector(".chkHDFilter"),p=i.querySelector(".chk4KFilter"),h=i.querySelector(".chkSDFilter"),k=i.querySelector("#chkSubtitle"),f=i.querySelector("#chkTrailer"),u=i.querySelector("#chkThemeSong"),m=i.querySelector("#chkThemeVideo"),y=i.querySelector("#chkSpecialFeature"),F=i.querySelector("#chkSpecialEpisode"),g=i.querySelector("#chkMissingEpisode"),v=i.querySelector("#chkFutureEpisode");s&&(s.checked=e.Is3D===!0),l&&(l.checked=e.IsHD===!0),p&&(p.checked=e.Is4K===!0),h&&(h.checked=e.IsHD===!1),k&&(k.checked=e.HasSubtitles===!0),f&&(f.checked=e.HasTrailer===!0),u&&(u.checked=e.HasThemeSong===!0),m&&(m.checked=e.HasThemeVideo===!0),y&&(y.checked=e.HasSpecialFeature===!0),F&&(F.checked=e.ParentIndexNumber===0),g&&(g.checked=e.IsMissing===!0),v&&(v.checked=e.IsUnaired===!0);for(const d of i.querySelectorAll(".chkStatus")){const a=`,${e.SeriesStatus||""}`,b=d.getAttribute("data-filter")??"";d.checked=a.includes(`,${b}`)}}function r(i){N(),U(i.options.query)?P(document.body,"resetFilters"):C(document.body,"resetFilters"),V.trigger(i,"filterchange")}function B(i,t){if((t.mode==="livetvchannels"||t.mode==="albums"||t.mode==="artists"||t.mode==="albumartists"||t.mode==="songs")&&W(i,"videoStandard"),x(t.mode??""))for(const e of[".genreFilters",".officialRatingFilters",".tagFilters",".yearFilters"]){const s=i.querySelector(e);s&&s.classList.remove("hide")}if(t.mode==="movies"||t.mode==="series"||t.mode==="episodes"){const e=i.querySelector(".videoTypeFilters");e&&e.classList.remove("hide")}if(t.mode==="movies"||t.mode==="series"||t.mode==="episodes"){const e=i.querySelector(".features");e&&e.classList.remove("hide")}if(t.mode==="series"){const e=i.querySelector(".seriesStatus");e&&e.classList.remove("hide")}t.mode==="episodes"&&j(i,"episodeFilter"),t.hasFilters||C(i,"resetFilters")}function P(i,t){for(const e of i.querySelectorAll(`.${t}`))e.disabled=!1}function C(i,t){for(const e of i.querySelectorAll(`.${t}`))e.disabled=!0}function j(i,t){for(const e of i.querySelectorAll(`.${t}`))e.classList.remove("hide")}function W(i,t){for(const e of i.querySelectorAll(`.${t}`))e.classList.add("hide")}function x(i){return i==="movies"||i==="series"||i==="albums"||i==="albumartists"||i==="artists"||i==="songs"||i==="episodes"}class ne{constructor(t){this.options=t}onFavoriteChange(t){const e=this.options.query;e.StartIndex=0,e.IsFavorite=!!t.checked||null,r(this)}onStandardFilterChange(t){const e=this.options.query,s=t.getAttribute("data-filter")??"";let l=e.Filters||"";l=`,${l}`.replace(`,${s}`,"").substring(1),t.checked&&(l=l?`${l},${s}`:s),e.StartIndex=0,e.Filters=l,r(this)}onVideoTypeFilterChange(t){const e=this.options.query,s=t.getAttribute("data-filter")??"";let l=e.VideoTypes||"";l=`,${l}`.replace(`,${s}`,"").substring(1),t.checked&&(l=l?`${l},${s}`:s),e.StartIndex=0,e.VideoTypes=l,r(this)}onStatusChange(t){const e=this.options.query,s=t.getAttribute("data-filter")??"";let l=e.SeriesStatus||"";l=`,${l}`.replace(`,${s}`,"").substring(1),t.checked&&(l=l?`${l},${s}`:s),e.SeriesStatus=l,e.StartIndex=0,r(this)}bindEvents(t){const e=this.options.query;if(this.options.mode==="livetvchannels")for(const a of t.querySelectorAll(".chkFavorite"))a.addEventListener("change",()=>this.onFavoriteChange(a));else for(const a of t.querySelectorAll(".chkStandardFilter"))a.addEventListener("change",()=>this.onStandardFilterChange(a));for(const a of t.querySelectorAll(".chkVideoTypeFilter"))a.addEventListener("change",()=>this.onVideoTypeFilterChange(a));t.querySelector(".resetFilters")?.addEventListener("click",()=>{for(const a of t.querySelectorAll('.filterDialogContent input[type="checkbox"]:checked'))a.checked=!1;this.resetQuery(e),r(this)});const l=t.querySelector(".chk3DFilter");l.addEventListener("change",()=>{e.StartIndex=0,e.Is3D=l.checked?!0:null,r(this)});const p=t.querySelector(".chk4KFilter");p.addEventListener("change",()=>{e.StartIndex=0,e.Is4K=p.checked?!0:null,r(this)});const h=t.querySelector(".chkHDFilter"),k=t.querySelector(".chkSDFilter");h.addEventListener("change",()=>{e.StartIndex=0,h.checked?(k.checked=!1,e.IsHD=!0):e.IsHD=null,r(this)}),k.addEventListener("change",()=>{e.StartIndex=0,k.checked?(h.checked=!1,e.IsHD=!1):e.IsHD=null,r(this)});for(const a of t.querySelectorAll(".chkStatus"))a.addEventListener("change",()=>this.onStatusChange(a));const f=t.querySelector("#chkTrailer");f.addEventListener("change",()=>{e.StartIndex=0,e.HasTrailer=f.checked?!0:null,r(this)});const u=t.querySelector("#chkThemeSong");u.addEventListener("change",()=>{e.StartIndex=0,e.HasThemeSong=u.checked?!0:null,r(this)});const m=t.querySelector("#chkSpecialFeature");m.addEventListener("change",()=>{e.StartIndex=0,e.HasSpecialFeature=m.checked?!0:null,r(this)});const y=t.querySelector("#chkThemeVideo");y.addEventListener("change",()=>{e.StartIndex=0,e.HasThemeVideo=y.checked?!0:null,r(this)});const F=t.querySelector("#chkMissingEpisode");F.addEventListener("change",()=>{e.StartIndex=0,e.IsMissing=!!F.checked,r(this)});const g=t.querySelector("#chkSpecialEpisode");g.addEventListener("change",()=>{e.StartIndex=0,e.ParentIndexNumber=g.checked?0:null,r(this)});const v=t.querySelector("#chkFutureEpisode");v.addEventListener("change",()=>{e.StartIndex=0,v.checked?(e.IsUnaired=!0,e.IsVirtualUnaired=null):(e.IsUnaired=null,e.IsVirtualUnaired=!1),r(this)});const d=t.querySelector("#chkSubtitle");d.addEventListener("change",()=>{e.StartIndex=0,e.HasSubtitles=d.checked?!0:null,r(this)}),t.addEventListener("change",a=>{const b=I.parentWithClass(a.target,"chkGenreFilter");if(b){const c=b.getAttribute("data-filter");let n=e.Genres||"";const o="|";n=n.split(o).filter(S=>S!==c).join(o),b.checked&&(n=n?n+o+c:c),e.StartIndex=0,e.Genres=n,r(this);return}const D=I.parentWithClass(a.target,"chkTagFilter");if(D){const c=D.getAttribute("data-filter");let n=e.Tags||"";const o="|";n=n.split(o).filter(S=>S!==c).join(o),D.checked&&(n=n?n+o+c:c),e.StartIndex=0,e.Tags=n,r(this);return}const E=I.parentWithClass(a.target,"chkYearFilter");if(E){const c=E.getAttribute("data-filter");let n=e.Years||"";const o=",";n=n.split(o).filter(S=>S!==c).join(o),E.checked&&(n=n?n+o+c:c),e.StartIndex=0,e.Years=n,r(this);return}const H=I.parentWithClass(a.target,"chkOfficialRatingFilter");if(H){const c=H.getAttribute("data-filter");let n=e.OfficialRatings||"";const o="|";n=n.split(o).filter(S=>S!==c).join(o),H.checked&&(n=n?n+o+c:c),e.StartIndex=0,e.OfficialRatings=n,r(this)}})}resetQuery(t){t.IsFavorite=null,t.IsHD=null,t.Is3D=null,t.Is4K=null,t.Filters="",t.SeriesStatus="",t.OfficialRatings="",t.Genres="",t.VideoTypes="",t.StartIndex=0,t.HasSpecialFeature=null,t.HasSubtitles=null,t.HasThemeSong=null,t.HasThemeVideo=null,t.HasTrailer=null,t.Tags=null,t.Years=""}show(){return new Promise(t=>{const e=q.createDialog({removeOnClose:!0,modal:!1});if(e.classList.add("ui-body-a"),e.classList.add("background-theme-a"),e.classList.add("formDialog"),e.classList.add("filterDialog"),e.innerHTML=O.translateHtml(G),B(e,this.options),q.open(e).catch(()=>t()),e.addEventListener("close",()=>t()),Y(e,this.options),this.bindEvents(e),x(this.options.mode??"")){e.classList.add("dynamicFilterDialog");const s=A.getApiClient(this.options.serverId??"");K(e,s,s.getCurrentUserId()??"",this.options.query).catch(()=>{})}})}}export{ne as default};
