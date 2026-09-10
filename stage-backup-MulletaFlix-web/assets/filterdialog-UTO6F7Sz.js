import{ej as C,ek as N,el as U,em as Y,en as L,eo as A,ep as w,eq as G,er as M,es as K,d as I,c as q,f as P,S as B,E as j,e as x}from"./index-6F8mVyyU.js";import"./emby-collapse-uEmrH5zr.js";import{stopMultiSelect as W}from"./multiSelect-CCY0hjug.js";import{g as _}from"./filterIndicator-comiHNH-.js";var O=C?C.isConcatSpreadable:void 0;function z(i){return N(i)||U(i)||!!(O&&i&&i[O])}function J(i,t,e,n,s){var a=-1,h=i.length;for(e||(e=z),s||(s=[]);++a<h;){var o=i[a];e(o)&&Y(s,o)}return s}function Z(i,t,e,n){for(var s=i.length,a=e+-1;++a<s;)if(t(i[a],a,i))return a;return-1}function Q(i){return i!==i}function X(i,t,e){for(var n=e-1,s=i.length;++n<s;)if(i[n]===t)return n;return-1}function ee(i,t,e){return t===t?X(i,t,e):Z(i,Q,e)}function te(i,t){var e=i==null?0:i.length;return!!e&&ee(i,t,0)>-1}function ie(){}var se=1/0,ne=L&&1/A(new L([,-0]))[1]==se?function(i){return new L(i)}:ie,le=200;function re(i,t,e){var n=-1,s=te,a=i.length,h=!0,o=[],d=o;if(a>=le){var f=ne(i);if(f)return A(f);h=!1,s=G,d=new w}else d=o;e:for(;++n<a;){var k=i[n],m=k;if(k=k!==0?k:0,h&&m===m){for(var g=d.length;g--;)if(d[g]===m)continue e;o.push(k)}else s(d,m,e)||(d!==o&&d.push(m),o.push(k))}return o}var ae=M(function(i){return re(J(i,1,K))});const ce=`<div style="margin: 0;padding:1.5em 2em;" class="filterDialogContent">

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
`;function T(i,t,e){return t?ae(i,t.split(e)).sort():i}function $(i,t,e,n,s){const a=i.querySelector(t);if(!a)return;n.length?a.classList.remove("hide"):a.classList.add("hide");let h="";h+='<div class="checkboxList">',h+=n.map(function(d){let f="";const k=s(d)?"checked":"";return f+="<label>",f+=`<input is="emby-checkbox" type="checkbox" ${k} data-filter="${x(d)}" class="${x(e)}"/>`,f+=`<span>${x(d)}</span>`,f+="</label>",f}).join(""),h+="</div>";const o=a.querySelector(".filterOptions");o&&(o.innerHTML=h)}function oe(i,t,e){$(i,".genreFilters","chkGenreFilter",T(t.Genres,e.Genres??null,"|"),function(n){return("|"+(e.Genres||"")+"|").includes("|"+n+"|")}),$(i,".officialRatingFilters","chkOfficialRatingFilter",T(t.OfficialRatings,e.OfficialRatings,"|"),function(n){return("|"+(e.OfficialRatings||"")+"|").includes("|"+n+"|")}),$(i,".tagFilters","chkTagFilter",T(t.Tags,e.Tags,"|"),function(n){return("|"+(e.Tags||"")+"|").includes("|"+n+"|")}),$(i,".yearFilters","chkYearFilter",T(t.Years.map(String),e.Years,","),function(n){return(","+(e.Years||"")+",").includes(","+n+",")})}function de(i,t,e,n){return t.getJSON(t.getUrl("Items/Filters",{UserId:e,ParentId:n.ParentId,IncludeItemTypes:n.IncludeItemTypes})).then(s=>{oe(i,s,n)})}function he(i,t){const e=t.query;if(t.mode==="livetvchannels"){const b=i.querySelector(".chkFavorite");b&&(b.checked=e.IsFavorite===!0)}else for(const b of i.querySelectorAll(".chkStandardFilter")){const r=`,${e.Filters||""}`,S=b.getAttribute("data-filter")??"";b.checked=r.includes(`,${S}`)}for(const b of i.querySelectorAll(".chkVideoTypeFilter")){const r=`,${e.VideoTypes||""}`,S=b.getAttribute("data-filter")??"";b.checked=r.includes(`,${S}`)}const n=i.querySelector(".chk3DFilter"),s=i.querySelector(".chkHDFilter"),a=i.querySelector(".chk4KFilter"),h=i.querySelector(".chkSDFilter"),o=i.querySelector("#chkSubtitle"),d=i.querySelector("#chkTrailer"),f=i.querySelector("#chkThemeSong"),k=i.querySelector("#chkThemeVideo"),m=i.querySelector("#chkSpecialFeature"),g=i.querySelector("#chkSpecialEpisode"),y=i.querySelector("#chkMissingEpisode"),v=i.querySelector("#chkFutureEpisode");n&&(n.checked=e.Is3D===!0),s&&(s.checked=e.IsHD===!0),a&&(a.checked=e.Is4K===!0),h&&(h.checked=e.IsHD===!1),o&&(o.checked=e.HasSubtitles===!0),d&&(d.checked=e.HasTrailer===!0),f&&(f.checked=e.HasThemeSong===!0),k&&(k.checked=e.HasThemeVideo===!0),m&&(m.checked=e.HasSpecialFeature===!0),g&&(g.checked=e.ParentIndexNumber===0),y&&(y.checked=e.IsMissing===!0),v&&(v.checked=e.IsUnaired===!0);for(const b of i.querySelectorAll(".chkStatus")){const r=`,${e.SeriesStatus||""}`,S=b.getAttribute("data-filter")??"";b.checked=r.includes(`,${S}`)}}function c(i){W(),_(i.options.query)?pe(document.body,"resetFilters"):V(document.body,"resetFilters"),j.trigger(i,"filterchange")}function ue(i,t){if((t.mode==="livetvchannels"||t.mode==="albums"||t.mode==="artists"||t.mode==="albumartists"||t.mode==="songs")&&ke(i,"videoStandard"),R(t.mode??""))for(const e of[".genreFilters",".officialRatingFilters",".tagFilters",".yearFilters"]){const n=i.querySelector(e);n&&n.classList.remove("hide")}if(t.mode==="movies"||t.mode==="series"||t.mode==="episodes"){const e=i.querySelector(".videoTypeFilters");e&&e.classList.remove("hide")}if(t.mode==="movies"||t.mode==="series"||t.mode==="episodes"){const e=i.querySelector(".features");e&&e.classList.remove("hide")}if(t.mode==="series"){const e=i.querySelector(".seriesStatus");e&&e.classList.remove("hide")}t.mode==="episodes"&&fe(i,"episodeFilter"),t.hasFilters||V(i,"resetFilters")}function pe(i,t){for(const e of i.querySelectorAll(`.${t}`))e.disabled=!1}function V(i,t){for(const e of i.querySelectorAll(`.${t}`))e.disabled=!0}function fe(i,t){for(const e of i.querySelectorAll(`.${t}`))e.classList.remove("hide")}function ke(i,t){for(const e of i.querySelectorAll(`.${t}`))e.classList.add("hide")}function R(i){return i==="movies"||i==="series"||i==="albums"||i==="albumartists"||i==="artists"||i==="songs"||i==="episodes"}class Fe{constructor(t){this.options=t}onFavoriteChange(t){const e=this.options.query;e.StartIndex=0,e.IsFavorite=!!t.checked||null,c(this)}onStandardFilterChange(t){const e=this.options.query,n=t.getAttribute("data-filter")??"";let s=e.Filters||"";s=`,${s}`.replace(`,${n}`,"").substring(1),t.checked&&(s=s?`${s},${n}`:n),e.StartIndex=0,e.Filters=s,c(this)}onVideoTypeFilterChange(t){const e=this.options.query,n=t.getAttribute("data-filter")??"";let s=e.VideoTypes||"";s=`,${s}`.replace(`,${n}`,"").substring(1),t.checked&&(s=s?`${s},${n}`:n),e.StartIndex=0,e.VideoTypes=s,c(this)}onStatusChange(t){const e=this.options.query,n=t.getAttribute("data-filter")??"";let s=e.SeriesStatus||"";s=`,${s}`.replace(`,${n}`,"").substring(1),t.checked&&(s=s?`${s},${n}`:n),e.SeriesStatus=s,e.StartIndex=0,c(this)}bindEvents(t){const e=this.options.query;if(this.options.mode==="livetvchannels")for(const r of t.querySelectorAll(".chkFavorite"))r.addEventListener("change",()=>this.onFavoriteChange(r));else for(const r of t.querySelectorAll(".chkStandardFilter"))r.addEventListener("change",()=>this.onStandardFilterChange(r));for(const r of t.querySelectorAll(".chkVideoTypeFilter"))r.addEventListener("change",()=>this.onVideoTypeFilterChange(r));t.querySelector(".resetFilters")?.addEventListener("click",()=>{for(const r of t.querySelectorAll('.filterDialogContent input[type="checkbox"]:checked'))r.checked=!1;this.resetQuery(e),c(this)});const s=t.querySelector(".chk3DFilter");s.addEventListener("change",()=>{e.StartIndex=0,e.Is3D=s.checked?!0:null,c(this)});const a=t.querySelector(".chk4KFilter");a.addEventListener("change",()=>{e.StartIndex=0,e.Is4K=a.checked?!0:null,c(this)});const h=t.querySelector(".chkHDFilter"),o=t.querySelector(".chkSDFilter");h.addEventListener("change",()=>{e.StartIndex=0,h.checked?(o.checked=!1,e.IsHD=!0):e.IsHD=null,c(this)}),o.addEventListener("change",()=>{e.StartIndex=0,o.checked?(h.checked=!1,e.IsHD=!1):e.IsHD=null,c(this)});for(const r of t.querySelectorAll(".chkStatus"))r.addEventListener("change",()=>this.onStatusChange(r));const d=t.querySelector("#chkTrailer");d.addEventListener("change",()=>{e.StartIndex=0,e.HasTrailer=d.checked?!0:null,c(this)});const f=t.querySelector("#chkThemeSong");f.addEventListener("change",()=>{e.StartIndex=0,e.HasThemeSong=f.checked?!0:null,c(this)});const k=t.querySelector("#chkSpecialFeature");k.addEventListener("change",()=>{e.StartIndex=0,e.HasSpecialFeature=k.checked?!0:null,c(this)});const m=t.querySelector("#chkThemeVideo");m.addEventListener("change",()=>{e.StartIndex=0,e.HasThemeVideo=m.checked?!0:null,c(this)});const g=t.querySelector("#chkMissingEpisode");g.addEventListener("change",()=>{e.StartIndex=0,e.IsMissing=!!g.checked,c(this)});const y=t.querySelector("#chkSpecialEpisode");y.addEventListener("change",()=>{e.StartIndex=0,e.ParentIndexNumber=y.checked?0:null,c(this)});const v=t.querySelector("#chkFutureEpisode");v.addEventListener("change",()=>{e.StartIndex=0,v.checked?(e.IsUnaired=!0,e.IsVirtualUnaired=null):(e.IsUnaired=null,e.IsVirtualUnaired=!1),c(this)});const b=t.querySelector("#chkSubtitle");b.addEventListener("change",()=>{e.StartIndex=0,e.HasSubtitles=b.checked?!0:null,c(this)}),t.addEventListener("change",r=>{const S=I.parentWithClass(r.target,"chkGenreFilter");if(S){const u=S.getAttribute("data-filter");let l=e.Genres||"";const p="|";l=l.split(p).filter(F=>F!==u).join(p),S.checked&&(l=l?l+p+u:u),e.StartIndex=0,e.Genres=l,c(this);return}const E=I.parentWithClass(r.target,"chkTagFilter");if(E){const u=E.getAttribute("data-filter");let l=e.Tags||"";const p="|";l=l.split(p).filter(F=>F!==u).join(p),E.checked&&(l=l?l+p+u:u),e.StartIndex=0,e.Tags=l,c(this);return}const H=I.parentWithClass(r.target,"chkYearFilter");if(H){const u=H.getAttribute("data-filter");let l=e.Years||"";const p=",";l=l.split(p).filter(F=>F!==u).join(p),H.checked&&(l=l?l+p+u:u),e.StartIndex=0,e.Years=l,c(this);return}const D=I.parentWithClass(r.target,"chkOfficialRatingFilter");if(D){const u=D.getAttribute("data-filter");let l=e.OfficialRatings||"";const p="|";l=l.split(p).filter(F=>F!==u).join(p),D.checked&&(l=l?l+p+u:u),e.StartIndex=0,e.OfficialRatings=l,c(this)}})}resetQuery(t){t.IsFavorite=null,t.IsHD=null,t.Is3D=null,t.Is4K=null,t.Filters="",t.SeriesStatus="",t.OfficialRatings="",t.Genres="",t.VideoTypes="",t.StartIndex=0,t.HasSpecialFeature=null,t.HasSubtitles=null,t.HasThemeSong=null,t.HasThemeVideo=null,t.HasTrailer=null,t.Tags=null,t.Years=""}show(){return new Promise(t=>{const e=q.createDialog({removeOnClose:!0,modal:!1});if(e.classList.add("ui-body-a"),e.classList.add("background-theme-a"),e.classList.add("formDialog"),e.classList.add("filterDialog"),e.innerHTML=P.translateHtml(ce),ue(e,this.options),q.open(e).catch(()=>t()),e.addEventListener("close",()=>t()),he(e,this.options),this.bindEvents(e),R(this.options.mode??"")){e.classList.add("dynamicFilterDialog");const n=B.getApiClient(this.options.serverId??"");de(e,n,n.getCurrentUserId()??"",this.options.query).catch(()=>{})}})}}export{Fe as default};
