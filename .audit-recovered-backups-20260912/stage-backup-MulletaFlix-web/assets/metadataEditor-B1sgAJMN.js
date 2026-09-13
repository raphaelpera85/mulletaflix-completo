const __vite__mapDeps=(i,m=__vite__mapDeps,d=(m.f||(m.f=["./prompt-Bg9qzBZa.js","./index-CFwqrzSZ.js","./vendor-react-query-BiAmjtRH.js","./vendor-react-CeUY3tRn.js","./vendor-jellyfin-Bee54tkY.js","./vendor-axios-BCn5QfZZ.js","./vendor-lodash-CYyrkkBC.js","./vendor-date-fns-CwLI6ukS.js","./vendor-dompurify-Baz99PXY.js","./index-BAjafMOe.css","./personEditor-C4OpbPJk.js","./emby-select-BQevfB2a.js","./actionSheet-BM-G2WQD.js","./actionSheet-BKZT3mVB.css","./listview-CPZd3k-k.css","./emby-select-CyC5lbTl.css","./itemContextMenu-COj39UIH.js","./clipboard-BzsMhCfO.js"])))=>i.map(i=>d[i]);
import{_ as L}from"./vendor-react-query-BiAmjtRH.js";import{s as r,i as j,l as h,h as f,D as B,G as k,S as I,B as p,r as J,ag as w,n as Q}from"./index-CFwqrzSZ.js";import"./emby-checkbox-CDwDlGOj.js";import"./emby-select-BQevfB2a.js";/* empty css                 */import"./emby-textarea-BsVRAtiX.js";import{y as q,B as m}from"./vendor-jellyfin-Bee54tkY.js";import"./vendor-react-CeUY3tRn.js";import"./vendor-lodash-CYyrkkBC.js";import"./vendor-date-fns-CwLI6ukS.js";import"./vendor-dompurify-Baz99PXY.js";import"./vendor-axios-BCn5QfZZ.js";import"./actionSheet-BM-G2WQD.js";const V=`<div class="formDialogHeader">
    <button is="paper-icon-button-light" class="btnCancel btnBack autoSize hide" tabindex="-1" title="\${ButtonBack}"><span class="material-icons arrow_back" aria-hidden="true"></span></button>
    <h3 class="formDialogHeaderTitle">
        \${Edit}
    </h3>
    <div class="dialogHeader flex align-items-center justify-content-center">
        <button is="emby-button" type="button" class="btnHeaderSave button-accent-flat button-flat hide" tabindex="-1">
            <span class="material-icons check" aria-hidden="true"></span>
            <span>\${Save}</span>
        </button>
        <button is="paper-icon-button-light" class="btnMore autoSize" tabindex="-1" title="\${ButtonMore}">
            <span class="material-icons more_vert" aria-hidden="true"></span>
        </button>
        <button is="paper-icon-button-light" class="btnCancel btnClose autoSize" tabindex="-1" title="\${ButtonClose}">
            <span class="material-icons close" aria-hidden="true"></span>
        </button>
    </div>
</div>

<div class="formDialogContent smoothScrollY" style="padding-top:2em;">

    <form class="editItemMetadataForm editMetadataForm dialogContentInner dialog-content-centered">
        <div class="metadataFormFields">

            <div style="padding: 0 0 10px;">
                <div id="fldContentType" class="selectContainer hide">
                    <select is="emby-select" id="selectContentType" label="\${LabelContentType}"></select>
                </div>
                <div id="fldPath" class="inputContainer">
                    <div class="align-items-center flex">
                        <div class="flex-grow">
                            <input is="emby-input" id="txtPath" type="text" label="\${LabelPath}" class="flex-grow" readonly dir="ltr"/>
                        </div>
                    </div>
                </div>
                <div class="inputContainer">
                    <input is="emby-input" id="txtName" type="text" label="\${LabelTitle}" required="required" />
                </div>
                <div id="fldOriginalName" class="inputContainer">
                    <input is="emby-input" id="txtOriginalName" type="text" label="\${LabelOriginalTitle}" />
                </div>
                <div id="fldOriginalLanguage" class="hide selectContainer">
                    <select is="emby-select" id="selectOriginalLanguage" label="\${LabelOriginalLanguage}"></select>
                </div>
                <div class="inputContainer">
                    <input is="emby-input" id="txtSortName" type="text" label="\${LabelSortTitle}" />
                </div>
                <div id="fldDateAdded" class="hide inputContainer">
                    <input is="emby-input" id="txtDateAdded" type="date" label="\${LabelDateAdded}" />
                    <div class="fieldDescription">\${ConfigureDateAdded}</div>
                </div>
                <div id="fldStatus" class="hide selectContainer">
                    <select is="emby-select" id="selectStatus" label="\${LabelStatus}"></select>
                </div>
                <div id="fldArtist" class="hide inputContainer">
                    <input is="emby-input" id="txtArtist" type="text" label="\${LabelArtists}" />
                    <div class="fieldDescription">\${LabelArtistsHelp}</div>
                </div>
                <div id="fldAlbumArtist" class="hide inputContainer">
                    <input is="emby-input" id="txtAlbumArtist" type="text" label="\${LabelAlbumArtists}" />
                    <div class="fieldDescription">\${LabelArtistsHelp}</div>
                </div>
                <div id="fldAlbum" class="hide inputContainer">
                    <input is="emby-input" id="txtAlbum" type="text" label="\${LabelAlbum}" />
                </div>
                <div class="inlineForm">
                    <div id="fldParentIndexNumber" class="hide inputContainer">
                        <input is="emby-input" id="txtParentIndexNumber" type="number" />
                    </div>
                    <div id="fldIndexNumber" class="hide inputContainer">
                        <input is="emby-input" id="txtIndexNumber" type="number" pattern="[0-9]*" />
                    </div>
                </div>
                <div id="fldCommunityRating" class="hide inputContainer">
                    <input is="emby-input" id="txtCommunityRating" type="number" step="any" min="0" max="10" label="\${LabelCommunityRating}" />
                </div>
                <div id="fldCriticRating" class="hide inputContainer">
                    <input is="emby-input" id="txtCriticRating" type="number" step=".1" label="\${LabelCriticRating}" />
                </div>
                <div id="fldTagline" class="hide inputContainer">
                    <input is="emby-input" id="txtTagline" type="text" label="\${LabelTagline}" />
                </div>
                <div class="inputContainer overviewContainer hide">
                    <textarea is="emby-textarea" id="txtOverview" label="\${LabelOverview}"></textarea>
                </div>
                <div id="fldPremiereDate" class="inputContainer">
                    <input is="emby-input" id="txtPremiereDate" label="\${LabelReleaseDate}" type="date" />
                </div>
                <div id="fldYear" class="hide inputContainer">
                    <input is="emby-input" id="txtProductionYear" type="number" label="\${LabelYear}" />
                </div>
                <div id="fldPlaceOfBirth" class="hide inputContainer">
                    <input is="emby-input" id="txtPlaceOfBirth" type="text" label="\${LabelPlaceOfBirth}" />
                </div>
                <div id="fldEndDate" class="inputContainer">
                    <input is="emby-input" id="txtEndDate" label="\${LabelEndDate}" type="date" />
                </div>
                <div id="fldAirDays" class="hide">
                    <p>\${LabelAirDays}</p>

                    <div class="checkboxList">
                        <label>
                            <input type="checkbox" is="emby-checkbox" class="chkAirDay" data-day="Sunday" />
                            <span>\${Sunday}</span>
                        </label>
                        <label>
                            <input type="checkbox" is="emby-checkbox" class="chkAirDay" data-day="Monday" />
                            <span>\${Monday}</span>
                        </label>
                        <label>
                            <input type="checkbox" is="emby-checkbox" class="chkAirDay" data-day="Tuesday" />
                            <span>\${Tuesday}</span>
                        </label>
                        <label>
                            <input type="checkbox" is="emby-checkbox" class="chkAirDay" data-day="Wednesday" />
                            <span>\${Wednesday}</span>
                        </label>
                        <label>
                            <input type="checkbox" is="emby-checkbox" class="chkAirDay" data-day="Thursday" />
                            <span>\${Thursday}</span>
                        </label>
                        <label>
                            <input type="checkbox" is="emby-checkbox" class="chkAirDay" data-day="Friday" />
                            <span>\${Friday}</span>
                        </label>
                        <label>
                            <input type="checkbox" is="emby-checkbox" class="chkAirDay" data-day="Saturday" />
                            <span>\${Saturday}</span>
                        </label>
                    </div>
                    <br />

                </div>
                <div id="fldAirTime" class="inputContainer hide">
                    <input is="emby-input" id="txtAirTime" type="text" label="\${LabelAirTime}" />
                </div>
                <div id="fldSeriesRuntime" class="inputContainer hide">
                    <input is="emby-input" id="txtSeriesRuntime" type="number" label="\${LabelRuntimeMinutes}" />
                </div>
                <div class="inlineForm">
                    <div id="fldOfficialRating" class="selectContainer hide">
                        <select is="emby-select" id="selectOfficialRating" label="\${LabelParentalRating}"></select>
                    </div>
                    <div id="fldCustomRating" class="selectContainer hide">
                        <select is="emby-select" id="selectCustomRating" label="\${LabelCustomRating}"></select>
                    </div>
                </div>
                <div id="fldHeight" class="selectContainer hide">
                    <select is="emby-select" id="selectHeight" label="\${MediaInfoResolution}">
                        <option value="0"></option>
                        <option value="480">\${ChannelResolutionSD}</option>
                        <option value="576">\${ChannelResolutionSDPAL}</option>
                        <option value="720">\${ChannelResolutionHD}</option>
                        <option value="1080">\${ChannelResolutionFullHD}</option>
                        <option value="2160">\${ChannelResolutionUHD4K}</option>
                    </select>
                </div>
                <div class="inlineForm">
                    <div id="fldOriginalAspectRatio" class="inputContainer hide">
                        <input is="emby-input" id="txtOriginalAspectRatio" type="text" label="\${LabelOriginalAspectRatio}" />
                    </div>
                    <div id="fld3dFormat" class="selectContainer hide">
                        <select is="emby-select" id="select3dFormat" label="\${Label3DFormat}">
                            <option value=""></option>
                            <option value="HalfSideBySide">HSBS</option>
                            <option value="HalfTopAndBottom">HTAB</option>
                            <option value="FullSideBySide">FSBS</option>
                            <option value="FullTopAndBottom">FTAB</option>
                            <option value="MVC">MVC</option>
                        </select>
                    </div>
                </div>

                <div id="fldDisplayOrder" class="fldDisplaySetting selectContainer hide">
                    <select is="emby-select" id="selectDisplayOrder" label="\${LabelDisplayOrder}"></select>
                    <div class="fieldDescription seriesDisplayOrderDescription">\${SeriesDisplayOrderHelp}</div>
                </div>

            </div>

            <div class="detailSection hide" id="collapsibleSpecialEpisodeInfo">
                <h2>
                    \${HeaderSpecialEpisodeInfo}
                </h2>
                <div class="inlineForm">
                    <div class="inputContainer">
                        <input is="emby-input" id="txtAirsBeforeSeason" type="number" pattern="[0-9]*" label="\${LabelAirsBeforeSeason}" />
                    </div>
                    <div class="inputContainer">
                        <input is="emby-input" id="txtAirsAfterSeason" type="number" pattern="[0-9]*" label="\${LabelAirsAfterSeason}" />
                    </div>
                    <div class="inputContainer">
                        <input is="emby-input" id="txtAirsBeforeEpisode" type="number" pattern="[0-9]*" label="\${LabelAirsBeforeEpisode}" />
                    </div>
                </div>
            </div>

            <div class="detailSection externalIdsSection hide">
                <h2>
                    \${HeaderExternalIds}
                </h2>
                <div class="externalIds editorFieldset">
                </div>
            </div>

            <div id="genresCollapsible" class="editableListviewContainer hide" style="margin-top: 3em;">
                <h2 style="margin:.6em 0;vertical-align:middle;display:inline-block;">
                    \${Genres}
                </h2>
                <button is="emby-button" type="button" class="fab btnAddTextItem submit marginStart" title="\${Add}">
                    <span class="material-icons add" aria-hidden="true"></span>
                </button>
                <div class="paperList" id="listGenres"></div>
            </div>
            <div id="peopleCollapsible" style="margin-top: 3em;" class="hide">
                <h2 style="margin:.6em 0;vertical-align:middle;display:inline-block;">
                    \${People}
                </h2>
                <button is="emby-button" type="button" id="btnAddPerson" class="fab btnAddPerson marginStart" title="\${Add}">
                    <span class="material-icons add" aria-hidden="true"></span>
                </button>
                <div id="peopleList" class="paperList">
                </div>
            </div>
            <div id="studiosCollapsible" class="editableListviewContainer hide" style="margin-top: 3em;">
                <h2 style="margin:.6em 0;vertical-align:middle;display:inline-block;">
                    \${Studios}
                </h2>
                <button is="emby-button" type="button" class="fab btnAddTextItem submit marginStart" title="\${Add}">
                    <span class="material-icons add" aria-hidden="true"></span>
                </button>
                <div class="paperList" id="listStudios"></div>
            </div>
            <div id="tagsCollapsible" class="editableListviewContainer hide" style="margin-top: 3em;">
                <h2 style="margin:.6em 0;vertical-align:middle;display:inline-block;">
                    \${Tags}
                </h2>
                <button is="emby-button" type="button" class="fab btnAddTextItem submit marginStart" title="\${Add}">
                    <span class="material-icons add" aria-hidden="true"></span>
                </button>
                <div class="paperList" id="listTags"></div>
            </div>
            <div id="metadataSettingsCollapsible" style="margin-top: 3em;" class="hide">
                <h2>\${HeaderMetadataSettings}</h2>
                <div>
                    <div class="selectContainer">
                        <select is="emby-select" id="selectLanguage" label="\${LabelMetadataDownloadLanguage}"></select>
                        <div class="fieldDescription editorfieldDescription">\${MessageLeaveEmptyToInherit}</div>
                    </div>
                    <div class="selectContainer">
                        <select is="emby-select" id="selectCountry" label="\${LabelCountry}"></select>
                    </div>
                    <div class="fieldDescription editorfieldDescription">\${MessageLeaveEmptyToInherit}</div>

                    <br /><br />
                    <label class="checkboxContainer">
                        <input type="checkbox" is="emby-checkbox" id="chkLockData" />
                        <span>\${LabelLockItemToPreventChanges}</span>
                    </label>

                    <div class="providerSettingsContainer checkboxList hide">
                    </div>
                </div>
            </div>
            <br />
            <div class="formDialogFooter">
                <button is="emby-button" type="button" class="raised button-cancel block btnCancel formDialogFooterItem">
                    <span>\${ButtonCancel}</span>
                </button>
                <button is="emby-button" type="button" class="raised button-reset block btnReset formDialogFooterItem">
                    <span>\${Reset}</span>
                </button>
                <button is="emby-button" type="submit" class="raised button-submit block btnSave formDialogFooterItem">
                    <span>\${Save}</span>
                </button>
            </div>

        </div>
    </form>
</div>
`;let g,b,c;function X(){return g.classList.contains("dialog")}function _(){X()&&k.close(g)}function Z(e,t){const i=G();B.withLoading(async()=>{try{await i.updateItem(t);const n=e.querySelector("#selectContentType").value||"";(b.ContentType||"")!==n&&await i.ajax({url:i.getUrl("Items/"+t.Id+"/ContentType",{ContentType:n}),type:"POST"}),Q(r.translate("MessageItemSaved")),_()}catch(n){console.error("[MetadataEditor] failed to update item metadata",n)}})}function ee(e){const t=e.querySelectorAll(".chkAirDay:checked")||[];return Array.prototype.map.call(t,function(i){return i.getAttribute("data-day")||""})}function Y(e){return e.querySelector("#txtArtist").value.trim().split(";").filter(function(t){return t.length>0}).map(function(t){return{Name:t}})}function te(e){return Y(e)}function T(e,t,i){let n=e.querySelector(t).value;if(!n)return null;if(c[i]){const a=w.parseISO8601Date(c[i],!0).toISOString().split("T");if(a[0].startsWith(n)){const s=a[1];n+="T"+s}}return n}function O(e){const t=e.currentTarget,i={Id:c.Id,Name:t.querySelector("#txtName").value,OriginalTitle:t.querySelector("#txtOriginalName").value,OriginalLanguage:t.querySelector("#selectOriginalLanguage").value,ForcedSortName:t.querySelector("#txtSortName").value,CommunityRating:t.querySelector("#txtCommunityRating").value,CriticRating:t.querySelector("#txtCriticRating").value,IndexNumber:t.querySelector("#txtIndexNumber").value||null,AirsBeforeSeasonNumber:t.querySelector("#txtAirsBeforeSeason").value,AirsAfterSeasonNumber:t.querySelector("#txtAirsAfterSeason").value,AirsBeforeEpisodeNumber:t.querySelector("#txtAirsBeforeEpisode").value,ParentIndexNumber:t.querySelector("#txtParentIndexNumber").value||null,DisplayOrder:t.querySelector("#selectDisplayOrder").value,Album:t.querySelector("#txtAlbum").value,AlbumArtists:Y(t),ArtistItems:te(t),Overview:t.querySelector("#txtOverview").value,Status:t.querySelector("#selectStatus").value,AirDays:ee(t),AirTime:t.querySelector("#txtAirTime").value,Genres:C(t.querySelector("#listGenres")),Tags:C(t.querySelector("#listTags")),Studios:C(t.querySelector("#listStudios")).map(function(a){return{Name:a}}),PremiereDate:T(t,"#txtPremiereDate","PremiereDate")??void 0,DateCreated:T(t,"#txtDateAdded","DateCreated")??void 0,EndDate:T(t,"#txtEndDate","EndDate")??void 0,ProductionYear:t.querySelector("#txtProductionYear").value,Height:t.querySelector("#selectHeight").value,AspectRatio:t.querySelector("#txtOriginalAspectRatio").value,Video3DFormat:t.querySelector("#select3dFormat").value,OfficialRating:t.querySelector("#selectOfficialRating").value,CustomRating:t.querySelector("#selectCustomRating").value,People:c.People,LockData:t.querySelector("#chkLockData").checked,LockedFields:Array.prototype.filter.call(t.querySelectorAll(".selectLockedField"),function(a){return!a.checked}).map(function(a){return a.getAttribute("data-value")||""})};i.ProviderIds={...c.ProviderIds};const n=t.querySelectorAll(".txtExternalId");if(Array.prototype.map.call(n,function(a){const s=a.getAttribute("data-providerkey")||"";i.ProviderIds[s]=a.value}),i.PreferredMetadataLanguage=t.querySelector("#selectLanguage").value,i.PreferredMetadataCountryCode=t.querySelector("#selectCountry").value,c.Type==="Person"){const a=t.querySelector("#txtPlaceOfBirth").value;i.ProductionLocations=a?[a]:[]}if(c.Type==="Series"){const a=t.querySelector("#txtSeriesRuntime").value;i.RunTimeTicks=a?parseInt(a,10)*6e8:null}const l=t.querySelector("#txtTagline").value;i.Taglines=l?[l]:[],Z(t,i),e.preventDefault(),e.stopPropagation()}function C(e){return Array.prototype.map.call(e.querySelectorAll(".textValue"),function(t){return t.textContent||""})}function ie(e,t){L(async()=>{const{default:i}=await import("./prompt-Bg9qzBZa.js");return{default:i}},__vite__mapDeps([0,1,2,3,4,5,6,7,8,9]),import.meta.url).then(({default:i})=>{i({label:"Value:"}).then(function(n){const l=f.parentWithClass(e,"editableListviewContainer")?.querySelector(".paperList");if(!l)return;const a=C(l);a.push(n),A(l,a)}).catch(n=>console.error("[MetadataEditor] failed to prompt for list value",n))}).catch(i=>console.error("[MetadataEditor] failed to load list prompt",i))}function ne(e){const t=f.parentWithClass(e,"listItem");t&&t.parentNode?.removeChild(t)}function R(e,t,i){L(async()=>{const{default:n}=await import("./personEditor-C4OpbPJk.js");return{default:n}},__vite__mapDeps([10,2,3,4,5,1,6,7,8,9,11,12,13,14,15]),import.meta.url).then(({default:n})=>{n.show(t).then(function(l){i===-1&&c.People.push(l),P(e,c.People||[])}).catch(l=>console.error("[MetadataEditor] failed to edit person",l))}).catch(n=>console.error("[MetadataEditor] failed to load person editor",n))}function ae(e,t){const i=t.ParentId||t.SeasonId||t.SeriesId;i&&t.ServerId?D(e,i,t.ServerId):J.goHome().catch(n=>console.error("[MetadataEditor] failed to navigate home",n))}function le(e,t,i){L(async()=>{const{default:n}=await import("./itemContextMenu-COj39UIH.js");return{default:n}},__vite__mapDeps([16,2,3,4,5,1,6,7,8,9,17,12,13,14]),import.meta.url).then(({default:n})=>{const l=c;n.show({item:l,positionTo:t,edit:!1,editImages:!0,editSubtitles:!0,share:!1,play:!1,queue:!1,user:i}).then(function(a){a.deleted?ae(e,l):a.updated&&l.Id&&l.ServerId&&D(e,l.Id,l.ServerId)}).catch(()=>{})}).catch(n=>console.error("[MetadataEditor] failed to load context menu",n))}function N(e){const t=e.target,i=f.parentWithClass(t,"btnRemoveFromEditorList");if(i){ne(i);return}const n=f.parentWithClass(t,"btnAddTextItem");n&&ie(n)}function G(){return I.getApiClient(c.ServerId)}function re(e,t,i){for(let n=0,l=e.length;n<l;n++)e[n].addEventListener(t,i)}function se(){const e=["#txtName","#txtOriginalName","#selectOriginalLanguage","#txtSortName","#txtCommunityRating","#txtCriticRating","#txtIndexNumber","#txtAirsBeforeSeason","#txtAirsAfterSeason","#txtAirsBeforeEpisode","#txtParentIndexNumber","#txtAlbum","#txtAlbumArtist","#txtArtist","#txtOverview","#selectStatus","#txtAirTime","#txtPremiereDate","#txtDateAdded","#txtEndDate","#txtProductionYear","#selectHeight","#txtOriginalAspectRatio","#select3dFormat","#selectOfficialRating","#selectCustomRating","#txtSeriesRuntime","#txtTagline"],t=g?.querySelector("form");e.forEach(function(a){t.querySelector(a).value=""}),t.querySelector("#selectDisplayOrder").value="",t.querySelector("#selectLanguage").value="",t.querySelector("#selectCountry").value="",t.querySelector("#listGenres").innerHTML="",t.querySelector("#listTags").innerHTML="",t.querySelector("#listStudios").innerHTML="",t.querySelector("#peopleList").innerHTML="",c.People=[],(t.querySelectorAll(".chkAirDay:checked")||[]).forEach(function(a){a.checked=!1}),t.querySelectorAll(".txtExternalId").forEach(function(a){a.value=""}),t.querySelector("#chkLockData").checked=!1,y(".providerSettingsContainer"),t.querySelectorAll(".selectLockedField").forEach(function(a){a.checked=!0})}function U(e){h.desktop||(e.querySelector(".btnBack")?.classList.remove("hide"),e.querySelector(".btnClose")?.classList.add("hide")),re(e.querySelectorAll(".btnCancel"),"click",function(i){i.preventDefault(),_()}),e.querySelector(".btnMore")?.addEventListener("click",function(i){G().getCurrentUser().then(function(n){le(e,i.target,n)}).catch(n=>console.error("[MetadataEditor] failed to load current user",n))}),e.querySelector(".btnHeaderSave")?.addEventListener("click",function(){e.querySelector(".btnSave").click()}),e.querySelector("#chkLockData")?.addEventListener("click",function(i){i.target.checked?S(".providerSettingsContainer"):y(".providerSettingsContainer")}),e.removeEventListener("click",N),e.addEventListener("click",N);const t=e.querySelector("form");t.removeEventListener("submit",O),t.addEventListener("submit",O),e.querySelector(".btnReset")?.addEventListener("click",se),e.querySelector("#btnAddPerson")?.addEventListener("click",function(){R(e,{},-1)}),e.querySelector("#peopleList")?.addEventListener("click",function(i){let n;const l=i.target,a=f.parentWithClass(l,"btnDeletePerson");a&&(n=parseInt(a.getAttribute("data-index"),10),c.People.splice(n,1),P(e,c.People||[]));const s=f.parentWithClass(l,"btnEditPerson");s&&(n=parseInt(s.getAttribute("data-index"),10),R(e,c.People[n],n))})}function oe(e,t){const i=I.getApiClient(t);return e?i.getItem(i.getCurrentUserId(),e):i.getRootFolder(i.getCurrentUserId())}function de(e,t){const i=I.getApiClient(t);return e?i.getJSON(i.getUrl("Items/"+e+"/MetadataEditor")):Promise.resolve({})}function ue(e,t){let i="";i+="<option value=''></option>";for(let n=0,l=t.length;n<l;n++){const a=t[n];i+="<option value='"+p(a.TwoLetterISORegionName)+"'>"+p(a.DisplayName)+"</option>"}e.innerHTML=i}function $(e,t){let i="";i+="<option value=''></option>";for(let n=0,l=t.length;n<l;n++){const a=t[n];i+="<option value='"+p(a.Name)+"' data-culture-name='"+p(a.Name)+"'>"+p(a.DisplayName)+"</option>"}e.innerHTML=i}function M(e){const t=e.querySelector("#selectCountry"),i=e.querySelector("#selectLanguage");if(!t||!i)return;const n=t.value||"";if(n&&!i.value&&n.toUpperCase()==="BR"){const l=i.querySelector("option[data-culture-name='pt-BR']");if(l){i.value=l.value;return}}}function ce(e,t){t.ContentTypeOptions?.length?y("#fldContentType",e):S("#fldContentType",e);const i=(t.ContentTypeOptions||[]).map(function(l){return'<option value="'+l.Value+'">'+l.Name+"</option>"}).join(""),n=e.querySelector("#selectContentType");n.innerHTML=i,n.value=t.ContentType||""}function pe(e,t,i){let n="";const l=t.ProviderIds||{};for(let s=0,d=i.length;s<d;s++){const u=i[s],z="txt1"+u.Key;let x=u.Name;u.Type&&(x=u.Name+" "+r.translate(u.Type));const W=r.translate("LabelDynamicExternalId",p(x));n+='<div class="inputContainer">',n+='<div class="flex align-items-center">';const K=p(l[u.Key]||"");n+='<div class="flex-grow">',n+='<input is="emby-input" class="txtExternalId" value="'+K+'" data-providerkey="'+u.Key+'" id="'+z+'" label="'+W+'"/>',n+="</div>",n+="</div>",n+="</div>"}const a=e.querySelector(".externalIds");a&&(a.innerHTML=n),i.length?e.querySelector(".externalIdsSection")?.classList.remove("hide"):e.querySelector(".externalIdsSection")?.classList.add("hide")}function S(e,t,i){const n=t||document;if(typeof e=="string"){const l=[n.querySelector(e)];Array.prototype.forEach.call(l,function(a){a&&a.classList.add("hide")})}else e.classList.add("hide")}function y(e,t,i){const n=t||document;if(typeof e=="string"){const l=[n.querySelector(e)];Array.prototype.forEach.call(l,function(a){a&&a.classList.remove("hide")})}else e.classList.remove("hide")}function v(e,t,i){e.querySelector(t)?.label(i)}function o(e,t,i){t.forEach(n=>{i?y(n,e):S(n,e)})}function me(e,t){const i=e.querySelector("#selectDisplayOrder");t==="BoxSet"?(o(e,["#fldDisplayOrder"],!0),o(e,[".seriesDisplayOrderDescription"],!1),i&&(i.innerHTML='<option value="Default">'+r.translate("DateModified")+'</option><option value="SortName">'+r.translate("SortName")+'</option><option value="PremiereDate">'+r.translate("ReleaseDate")+"</option>")):t==="Series"?(o(e,["#fldDisplayOrder",".seriesDisplayOrderDescription"],!0),i&&(i.innerHTML='<option value="">'+r.translate("Aired")+'</option><option value="originalAirDate">'+r.translate("OriginalAirDate")+'</option><option value="absolute">'+r.translate("Absolute")+'</option><option value="dvd">DVD</option><option value="digital">'+r.translate("Digital")+'</option><option value="storyArc">'+r.translate("StoryArc")+'</option><option value="production">'+r.translate("Production")+'</option><option value="tv">TV</option><option value="alternate">'+r.translate("Alternate")+'</option><option value="regional">'+r.translate("Regional")+'</option><option value="altdvd">'+r.translate("AlternateDVD")+"</option>")):(o(e,["#fldDisplayOrder"],!1),i&&(i.innerHTML=""))}function ve(e,t){const i=t.Type||"",n=["Person","Genre","Studio","MusicGenre","TvChannel"].includes(i),l=i==="Series",a=t.MediaType==="Video";o(e,["#fldPath"],!!(t.Path&&t.EnableMediaSourceDisplay!==!1)),o(e,["#fldOriginalName"],[m.Series,m.Season,m.Episode,m.Movie,m.Trailer,m.Person].includes(i)),o(e,["#fldOriginalLanguage"],l||a),o(e,["#fldSeriesRuntime"],l),o(e,["#fldEndDate"],l||i==="Person"),o(e,["#albumAssociationMessage"],i==="MusicAlbum"),o(e,["#fldCriticRating"],i==="Movie"||i==="Trailer"),o(e,["#fldStatus","#fldAirDays","#fldAirTime"],l),o(e,["#fld3dFormat"],a&&i!=="TvChannel"),o(e,["#fldArtist","#fldAlbumArtist"],[m.Audio,m.MusicAlbum,m.MusicVideo].includes(i)),o(e,["#fldAlbum"],[m.Audio,m.MusicVideo].includes(i)),o(e,["#collapsibleSpecialEpisodeInfo"],i==="Episode"&&t.ParentIndexNumber===0),o(e,["#peopleCollapsible"],!n),o(e,["#fldCommunityRating","#genresCollapsible","#studiosCollapsible"],!n),o(e,["#fldOfficialRating"],!n||i==="TvChannel"),o(e,["#fldCustomRating"],!n),y("#tagsCollapsible",e),o(e,["#metadataSettingsCollapsible","#fldPremiereDate","#fldDateAdded","#fldYear",".overviewContainer"],i!=="TvChannel"),i==="Person"?(v(e,"#txtName",r.translate("LabelName")),v(e,"#txtSortName",r.translate("LabelSortName")),v(e,"#txtOriginalName",r.translate("LabelOriginalName")),v(e,"#txtProductionYear",r.translate("LabelBirthYear")),v(e,"#txtPremiereDate",r.translate("LabelBirthDate")),v(e,"#txtEndDate",r.translate("LabelDeathDate"))):(v(e,"#txtProductionYear",r.translate("LabelYear")),v(e,"#txtPremiereDate",r.translate("LabelReleaseDate")),v(e,"#txtEndDate",r.translate("LabelEndDate"))),o(e,["#fldPlaceOfBirth"],i==="Person"),o(e,["#fldHeight"],a&&i==="TvChannel"),o(e,["#fldOriginalAspectRatio"],a&&i!=="TvChannel");const s={Episode:"LabelEpisodeNumber",Season:"LabelSeasonNumber",Audio:"LabelTrackNumber"};o(e,["#fldIndexNumber"],["Audio","Episode","Season"].includes(i)),s[i]&&v(e,"#txtIndexNumber",r.translate(s[i]));const d={Episode:"LabelSeasonNumber",Audio:"LabelDiscNumber"};o(e,["#fldParentIndexNumber"],["Audio","Episode"].includes(i)),d[i]&&v(e,"#txtParentIndexNumber",r.translate(d[i])),me(e,i)}function E(e){if(!e)return"";try{return w.parseISO8601Date(e,!0).toISOString().slice(0,10)}catch{return""}}function be(e,t,i){let n=e.querySelector("#selectOfficialRating");F(i,n,t.OfficialRating),n.value=t.OfficialRating||"",n=e.querySelector("#selectCustomRating"),F(i,n,t.CustomRating),n.value=t.CustomRating||"";const l=e.querySelector("#selectStatus");ye(l),l.value=t.Status||"",e.querySelector("#select3dFormat").value=t.Video3DFormat||"",Array.prototype.forEach.call(e.querySelectorAll(".chkAirDay"),function(u){u.checked=(t.AirDays||[]).indexOf(u.getAttribute("data-day")||"")!==-1}),A(e.querySelector("#listGenres"),t.Genres),P(e,t.People||[]),A(e.querySelector("#listStudios"),(t.Studios||[]).map(function(u){return u.Name||""})),A(e.querySelector("#listTags"),t.Tags);const a=t.LockData||!1,s=e.querySelector("#chkLockData");s.checked=a,s.checked?S(".providerSettingsContainer",e):y(".providerSettingsContainer",e),ge(e,t,t.LockedFields),e.querySelector("#txtPath").value=t.Path||"",e.querySelector("#txtName").value=t.Name||"",e.querySelector("#txtOriginalName").value=t.OriginalTitle||"",e.querySelector("#selectOriginalLanguage").value=t.OriginalLanguage||"",e.querySelector("#txtOverview").value=t.Overview||"",e.querySelector("#txtTagline").value=t.Taglines?.length?t.Taglines[0]:"",e.querySelector("#txtSortName").value=t.ForcedSortName||"",e.querySelector("#txtCommunityRating").value=t.CommunityRating||"",e.querySelector("#txtCriticRating").value=t.CriticRating||"",e.querySelector("#txtIndexNumber").value=t.IndexNumber==null?"":String(t.IndexNumber),e.querySelector("#txtParentIndexNumber").value=t.ParentIndexNumber==null?"":String(t.ParentIndexNumber),e.querySelector("#txtAirsBeforeSeason").value="AirsBeforeSeasonNumber"in t?String(t.AirsBeforeSeasonNumber):"",e.querySelector("#txtAirsAfterSeason").value="AirsAfterSeasonNumber"in t?String(t.AirsAfterSeasonNumber):"",e.querySelector("#txtAirsBeforeEpisode").value="AirsBeforeEpisodeNumber"in t?String(t.AirsBeforeEpisodeNumber):"",e.querySelector("#txtAlbum").value=t.Album||"",e.querySelector("#txtAlbumArtist").value=(t.AlbumArtists||[]).map(function(u){return u.Name}).join(";"),e.querySelector("#selectDisplayOrder").value=t.DisplayOrder||"",e.querySelector("#txtArtist").value=(t.ArtistItems||[]).map(function(u){return u.Name}).join(";"),e.querySelector("#txtDateAdded").value=E(t.DateCreated),e.querySelector("#txtPremiereDate").value=E(t.PremiereDate),e.querySelector("#txtEndDate").value=E(t.EndDate),e.querySelector("#txtProductionYear").value=String(t.ProductionYear||""),e.querySelector("#txtAirTime").value=t.AirTime||"";const d=t.ProductionLocations?.length?t.ProductionLocations[0]:"";if(e.querySelector("#txtPlaceOfBirth").value=d,e.querySelector("#selectHeight").value=t.Height||"",e.querySelector("#txtOriginalAspectRatio").value=t.AspectRatio||"",e.querySelector("#selectLanguage").value=t.PreferredMetadataLanguage||"",e.querySelector("#selectCountry").value=t.PreferredMetadataCountryCode||"",t.RunTimeTicks){const u=t.RunTimeTicks/6e8;e.querySelector("#txtSeriesRuntime").value=String(Math.round(u))}else e.querySelector("#txtSeriesRuntime").value=""}function F(e,t,i){let n="";n+="<option value=''></option>";const l=[];let a,s=!1;for(let d=0,u=e.length;d<u;d++)a=e[d],l.push({Name:a.Name,Value:a.Name}),a.Name===i&&(s=!0);i&&!s&&l.push({Name:i,Value:i});for(let d=0,u=l.length;d<u;d++)a=l[d],n+="<option value='"+p(a.Value)+"'>"+p(a.Name)+"</option>";t.innerHTML=n}function ye(e){let t="";t+='<option value=""></option>',t+=`<option value="${q.Continuing}">${p(r.translate("Continuing"))}</option>`,t+=`<option value="${q.Ended}">${p(r.translate("Ended"))}</option>`,t+=`<option value="${q.Unreleased}">${p(r.translate("Unreleased"))}</option>`,e.innerHTML=t}function A(e,t,i){let n=t||[];n=n.sort(function(a,s){return a.toLowerCase().localeCompare(s.toLowerCase())});let l="";for(let a=0;a<n.length;a++)l+='<div class="listItem">',l+='<span class="material-icons listItemIcon live_tv" aria-hidden="true" style="background-color:#333;"></span>',l+='<div class="listItemBody">',l+='<div class="textValue">',l+=p(n[a]),l+="</div>",l+="</div>",l+='<button type="button" is="paper-icon-button-light" data-index="'+a+'" class="btnRemoveFromEditorList autoSize"><span class="material-icons delete" aria-hidden="true"></span></button>',l+="</div>";e.innerHTML=l}function P(e,t){let n="";const l=e.querySelector("#peopleList");for(let a=0,s=t.length;a<s;a++){const d=t[a];n+='<div class="listItem">',n+='<span class="material-icons listItemIcon person" style="background-color:#333;"></span>',n+='<div class="listItemBody">',n+='<button style="text-align:left;" type="button" class="btnEditPerson clearButton" data-index="'+a+'">',n+='<div class="textValue">',n+=p(d.Name||""),n+="</div>",d.Role&&d.Role!==""?n+='<div class="secondary">'+p(d.Role)+"</div>":n+='<div class="secondary">'+r.translate(d.Type||"")+"</div>",n+="</button>",n+="</div>",n+='<button type="button" is="paper-icon-button-light" data-index="'+a+'" class="btnDeletePerson autoSize"><span class="material-icons delete" aria-hidden="true"></span></button>',n+="</div>"}l.innerHTML=n}function fe(e,t){let i="";for(const n of e){const l=n.name,a=n.value||n.name,s=t.indexOf(a)===-1?" checked":"";i+="<label>",i+='<input type="checkbox" is="emby-checkbox" class="selectLockedField" data-value="'+a+'"'+s+"/>",i+="<span>"+l+"</span>",i+="</label>"}return i}function ge(e,t,i){const n=e.querySelector(".providerSettingsContainer"),l=i||[],a=[{name:r.translate("Name"),value:"Name"},{name:r.translate("Overview"),value:"Overview"},{name:r.translate("Genres"),value:"Genres"},{name:r.translate("ParentalRating"),value:"OfficialRating"},{name:r.translate("People"),value:"Cast"}];t.Type==="Person"?a.push({name:r.translate("BirthLocation"),value:"ProductionLocations"}):a.push({name:r.translate("ProductionLocations"),value:"ProductionLocations"}),t.Type==="Series"&&a.push({name:r.translate("Runtime"),value:"Runtime"}),a.push({name:r.translate("Studios"),value:"Studios"}),a.push({name:r.translate("Tags"),value:"Tags"});let s="";s+="<h2>"+r.translate("HeaderEnabledFields")+"</h2>",s+="<p>"+r.translate("HeaderEnabledFieldsHelp")+"</p>",s+=fe(a,l),n.innerHTML=s}function D(e,t,i){B.withLoading(()=>Promise.all([oe(t,i),de(t,i)]).then(function(n){const l=n[0];b=n[1],c=l;const a=b.Cultures||[],s=b.Countries||[];ce(e,b),pe(e,l,b.ExternalIdInfos||[]),$(e.querySelector("#selectOriginalLanguage"),a),$(e.querySelector("#selectLanguage"),a),ue(e.querySelector("#selectCountry"),s);const d=e.querySelector("#selectCountry");d&&(d.onchange=()=>{M(e)}),ve(e,l),be(e,l,b.ParentalRatingOptions||[]),M(e),l.MediaType==="Video"&&l.Type!=="Episode"&&l.Type!=="TvChannel"?y("#fldTagline",e):S("#fldTagline",e)}).catch(n=>{console.error("[MetadataEditor] failed to reload metadata",n)}))}function H(e,t,i){e&&L(()=>import("./index-CFwqrzSZ.js").then(n=>n.bu),__vite__mapDeps([1,2,3,4,5,6,7,8,9]),import.meta.url).then(n=>{const l=i?"on":"off";n.centerFocus[l](e,t)}).catch(n=>console.error("[MetadataEditor] failed to center focus",n))}function Se(e,t,i){const n={removeOnClose:!0,scrollY:!1,size:""};h.tv?n.size="fullscreen":n.size="small";const l=k.createDialog(n);l.classList.add("formDialog");let a="";a+=r.translateHtml(V,"core"),l.innerHTML=a,h.tv&&H(l.querySelector(".formDialogContent"),!1,!0),k.open(l).catch(s=>console.error("[MetadataEditor] failed to open dialog",s)),l.addEventListener("close",function(){h.tv&&H(l.querySelector(".formDialogContent"),!1,!1),i()}),g=l,U(l),D(l,e,t)}const Re={show:function(e,t){return new Promise(i=>Se(e,t,i))},embed:function(e,t,i){return new Promise(function(n){e.innerHTML=r.translateHtml(V,"core"),e.querySelector(".formDialogFooter")?.classList.remove("formDialogFooter"),e.querySelector(".btnClose")?.classList.add("hide"),e.querySelector(".btnHeaderSave")?.classList.remove("hide"),e.querySelector(".btnCancel")?.classList.add("hide"),g=e,U(e),D(e,t,i),j.autoFocus(e),n()})}};export{Re as default};
