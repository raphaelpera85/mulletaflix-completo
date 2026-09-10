const __vite__mapDeps=(i,m=__vite__mapDeps,d=(m.f||(m.f=["./prompt-B5lr9m-H.js","./index-6F8mVyyU.js","./index-CQ6hRxbv.css","./personEditor-zcx-m8R_.js","./person-kind-mlhHzv9H.js","./emby-select-BS-dUTXm.js","./actionSheet-bHfWrycr.js","./actionSheet-BKZT3mVB.css","./emby-select-CyC5lbTl.css","./itemContextMenu-CWSvsVZa.js","./clipboard-CMo43-4Q.js"])))=>i.map(i=>d[i]);
import{a2 as b,f as r,a$ as K,l as C,d as g,c as I,S as P,_ as D,e as p,U as J,bi as w,W as Q,Q as m}from"./index-6F8mVyyU.js";import"./emby-select-BS-dUTXm.js";import"./emby-textarea-BTRMqmjp.js";import{S as T}from"./series-status-BpoYpEBS.js";import"./actionSheet-bHfWrycr.js";const V=`<div class="formDialogHeader">
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
`;let h,y,c;function X(){return h.classList.contains("dialog")}function _(){X()&&I.close(h)}function Z(e,t){function n(){Q(r.translate("MessageItemSaved")),b.hide(),_()}const i=U();i.updateItem(t).then(function(){const l=e.querySelector("#selectContentType").value||"";(y.ContentType||"")!==l?i.ajax({url:i.getUrl("Items/"+t.Id+"/ContentType",{ContentType:l}),type:"POST"}).then(function(){n()}).catch(a=>{b.hide(),console.error("[MetadataEditor] failed to update content type",a)}):n()}).catch(l=>{b.hide(),console.error("[MetadataEditor] failed to update item",l)})}function ee(e){const t=e.querySelectorAll(".chkAirDay:checked")||[];return Array.prototype.map.call(t,function(n){return n.getAttribute("data-day")||""})}function Y(e){return e.querySelector("#txtArtist").value.trim().split(";").filter(function(t){return t.length>0}).map(function(t){return{Name:t}})}function te(e){return Y(e)}function E(e,t,n){let i=e.querySelector(t).value;if(!i)return null;if(c[n]){const a=w.parseISO8601Date(c[n],!0).toISOString().split("T");if(a[0].startsWith(i)){const s=a[1];i+="T"+s}}return i}function R(e){b.show();const t=e.currentTarget,n={Id:c.Id,Name:t.querySelector("#txtName").value,OriginalTitle:t.querySelector("#txtOriginalName").value,OriginalLanguage:t.querySelector("#selectOriginalLanguage").value,ForcedSortName:t.querySelector("#txtSortName").value,CommunityRating:t.querySelector("#txtCommunityRating").value,CriticRating:t.querySelector("#txtCriticRating").value,IndexNumber:t.querySelector("#txtIndexNumber").value||null,AirsBeforeSeasonNumber:t.querySelector("#txtAirsBeforeSeason").value,AirsAfterSeasonNumber:t.querySelector("#txtAirsAfterSeason").value,AirsBeforeEpisodeNumber:t.querySelector("#txtAirsBeforeEpisode").value,ParentIndexNumber:t.querySelector("#txtParentIndexNumber").value||null,DisplayOrder:t.querySelector("#selectDisplayOrder").value,Album:t.querySelector("#txtAlbum").value,AlbumArtists:Y(t),ArtistItems:te(t),Overview:t.querySelector("#txtOverview").value,Status:t.querySelector("#selectStatus").value,AirDays:ee(t),AirTime:t.querySelector("#txtAirTime").value,Genres:A(t.querySelector("#listGenres")),Tags:A(t.querySelector("#listTags")),Studios:A(t.querySelector("#listStudios")).map(function(a){return{Name:a}}),PremiereDate:E(t,"#txtPremiereDate","PremiereDate")??void 0,DateCreated:E(t,"#txtDateAdded","DateCreated")??void 0,EndDate:E(t,"#txtEndDate","EndDate")??void 0,ProductionYear:t.querySelector("#txtProductionYear").value,Height:t.querySelector("#selectHeight").value,AspectRatio:t.querySelector("#txtOriginalAspectRatio").value,Video3DFormat:t.querySelector("#select3dFormat").value,OfficialRating:t.querySelector("#selectOfficialRating").value,CustomRating:t.querySelector("#selectCustomRating").value,People:c.People,LockData:t.querySelector("#chkLockData").checked,LockedFields:Array.prototype.filter.call(t.querySelectorAll(".selectLockedField"),function(a){return!a.checked}).map(function(a){return a.getAttribute("data-value")||""})};n.ProviderIds={...c.ProviderIds};const i=t.querySelectorAll(".txtExternalId");if(Array.prototype.map.call(i,function(a){const s=a.getAttribute("data-providerkey")||"";n.ProviderIds[s]=a.value}),n.PreferredMetadataLanguage=t.querySelector("#selectLanguage").value,n.PreferredMetadataCountryCode=t.querySelector("#selectCountry").value,c.Type==="Person"){const a=t.querySelector("#txtPlaceOfBirth").value;n.ProductionLocations=a?[a]:[]}if(c.Type==="Series"){const a=t.querySelector("#txtSeriesRuntime").value;n.RunTimeTicks=a?parseInt(a,10)*6e8:null}const l=t.querySelector("#txtTagline").value;n.Taglines=l?[l]:[],Z(t,n),e.preventDefault(),e.stopPropagation()}function A(e){return Array.prototype.map.call(e.querySelectorAll(".textValue"),function(t){return t.textContent||""})}function ne(e,t){D(async()=>{const{default:n}=await import("./prompt-B5lr9m-H.js");return{default:n}},__vite__mapDeps([0,1,2]),import.meta.url).then(({default:n})=>{n({label:"Value:"}).then(function(i){const l=g.parentWithClass(e,"editableListviewContainer")?.querySelector(".paperList");if(!l)return;const a=A(l);a.push(i),L(l,a)}).catch(i=>console.error("[MetadataEditor] failed to prompt for list value",i))}).catch(n=>console.error("[MetadataEditor] failed to load list prompt",n))}function ie(e){const t=g.parentWithClass(e,"listItem");t&&t.parentNode?.removeChild(t)}function N(e,t,n){D(async()=>{const{default:i}=await import("./personEditor-zcx-m8R_.js");return{default:i}},__vite__mapDeps([3,1,2,4,5,6,7,8]),import.meta.url).then(({default:i})=>{i.show(t).then(function(l){n===-1&&c.People.push(l),x(e,c.People||[])}).catch(l=>console.error("[MetadataEditor] failed to edit person",l))}).catch(i=>console.error("[MetadataEditor] failed to load person editor",i))}function ae(e,t){const n=t.ParentId||t.SeasonId||t.SeriesId;n&&t.ServerId?q(e,n,t.ServerId):J.goHome().catch(i=>console.error("[MetadataEditor] failed to navigate home",i))}function le(e,t,n){D(async()=>{const{default:i}=await import("./itemContextMenu-CWSvsVZa.js");return{default:i}},__vite__mapDeps([9,1,2,10,6,7]),import.meta.url).then(({default:i})=>{const l=c;i.show({item:l,positionTo:t,edit:!1,editImages:!0,editSubtitles:!0,share:!1,play:!1,queue:!1,user:n}).then(function(a){a.deleted?ae(e,l):a.updated&&l.Id&&l.ServerId&&q(e,l.Id,l.ServerId)}).catch(()=>{})}).catch(i=>console.error("[MetadataEditor] failed to load context menu",i))}function $(e){const t=e.target,n=g.parentWithClass(t,"btnRemoveFromEditorList");if(n){ie(n);return}const i=g.parentWithClass(t,"btnAddTextItem");i&&ne(i)}function U(){return P.getApiClient(c.ServerId)}function re(e,t,n){for(let i=0,l=e.length;i<l;i++)e[i].addEventListener(t,n)}function se(){const e=["#txtName","#txtOriginalName","#selectOriginalLanguage","#txtSortName","#txtCommunityRating","#txtCriticRating","#txtIndexNumber","#txtAirsBeforeSeason","#txtAirsAfterSeason","#txtAirsBeforeEpisode","#txtParentIndexNumber","#txtAlbum","#txtAlbumArtist","#txtArtist","#txtOverview","#selectStatus","#txtAirTime","#txtPremiereDate","#txtDateAdded","#txtEndDate","#txtProductionYear","#selectHeight","#txtOriginalAspectRatio","#select3dFormat","#selectOfficialRating","#selectCustomRating","#txtSeriesRuntime","#txtTagline"],t=h?.querySelector("form");e.forEach(function(a){t.querySelector(a).value=""}),t.querySelector("#selectDisplayOrder").value="",t.querySelector("#selectLanguage").value="",t.querySelector("#selectCountry").value="",t.querySelector("#listGenres").innerHTML="",t.querySelector("#listTags").innerHTML="",t.querySelector("#listStudios").innerHTML="",t.querySelector("#peopleList").innerHTML="",c.People=[],(t.querySelectorAll(".chkAirDay:checked")||[]).forEach(function(a){a.checked=!1}),t.querySelectorAll(".txtExternalId").forEach(function(a){a.value=""}),t.querySelector("#chkLockData").checked=!1,f(".providerSettingsContainer"),t.querySelectorAll(".selectLockedField").forEach(function(a){a.checked=!0})}function G(e){C.desktop||(e.querySelector(".btnBack")?.classList.remove("hide"),e.querySelector(".btnClose")?.classList.add("hide")),re(e.querySelectorAll(".btnCancel"),"click",function(n){n.preventDefault(),_()}),e.querySelector(".btnMore")?.addEventListener("click",function(n){U().getCurrentUser().then(function(i){le(e,n.target,i)}).catch(i=>console.error("[MetadataEditor] failed to load current user",i))}),e.querySelector(".btnHeaderSave")?.addEventListener("click",function(){e.querySelector(".btnSave").click()}),e.querySelector("#chkLockData")?.addEventListener("click",function(n){n.target.checked?S(".providerSettingsContainer"):f(".providerSettingsContainer")}),e.removeEventListener("click",$),e.addEventListener("click",$);const t=e.querySelector("form");t.removeEventListener("submit",R),t.addEventListener("submit",R),e.querySelector(".btnReset")?.addEventListener("click",se),e.querySelector("#btnAddPerson")?.addEventListener("click",function(){N(e,{},-1)}),e.querySelector("#peopleList")?.addEventListener("click",function(n){let i;const l=n.target,a=g.parentWithClass(l,"btnDeletePerson");a&&(i=parseInt(a.getAttribute("data-index"),10),c.People.splice(i,1),x(e,c.People||[]));const s=g.parentWithClass(l,"btnEditPerson");s&&(i=parseInt(s.getAttribute("data-index"),10),N(e,c.People[i],i))})}function oe(e,t){const n=P.getApiClient(t);return e?n.getItem(n.getCurrentUserId(),e):n.getRootFolder(n.getCurrentUserId())}function de(e,t){const n=P.getApiClient(t);return e?n.getJSON(n.getUrl("Items/"+e+"/MetadataEditor")):Promise.resolve({})}function ue(e,t){let n="";n+="<option value=''></option>";for(let i=0,l=t.length;i<l;i++){const a=t[i];n+="<option value='"+p(a.TwoLetterISORegionName)+"'>"+p(a.DisplayName)+"</option>"}e.innerHTML=n}function M(e,t){let n="";n+="<option value=''></option>";for(let i=0,l=t.length;i<l;i++){const a=t[i];n+="<option value='"+p(a.Name)+"' data-culture-name='"+p(a.Name)+"'>"+p(a.DisplayName)+"</option>"}e.innerHTML=n}function F(e){const t=e.querySelector("#selectCountry"),n=e.querySelector("#selectLanguage");if(!t||!n)return;const i=t.value||"";if(i&&!n.value&&i.toUpperCase()==="BR"){const l=n.querySelector("option[data-culture-name='pt-BR']");if(l){n.value=l.value;return}}}function ce(e,t){t.ContentTypeOptions?.length?f("#fldContentType",e):S("#fldContentType",e);const n=(t.ContentTypeOptions||[]).map(function(l){return'<option value="'+l.Value+'">'+l.Name+"</option>"}).join(""),i=e.querySelector("#selectContentType");i.innerHTML=n,i.value=t.ContentType||""}function pe(e,t,n){let i="";const l=t.ProviderIds||{};for(let s=0,d=n.length;s<d;s++){const u=n[s],W="txt1"+u.Key;let O=u.Name;u.Type&&(O=u.Name+" "+r.translate(u.Type));const z=r.translate("LabelDynamicExternalId",p(O));i+='<div class="inputContainer">',i+='<div class="flex align-items-center">';const j=p(l[u.Key]||"");i+='<div class="flex-grow">',i+='<input is="emby-input" class="txtExternalId" value="'+j+'" data-providerkey="'+u.Key+'" id="'+W+'" label="'+z+'"/>',i+="</div>",i+="</div>",i+="</div>"}const a=e.querySelector(".externalIds");a&&(a.innerHTML=i),n.length?e.querySelector(".externalIdsSection")?.classList.remove("hide"):e.querySelector(".externalIdsSection")?.classList.add("hide")}function S(e,t,n){const i=t||document;if(typeof e=="string"){const l=[i.querySelector(e)];Array.prototype.forEach.call(l,function(a){a&&a.classList.add("hide")})}else e.classList.add("hide")}function f(e,t,n){const i=t||document;if(typeof e=="string"){const l=[i.querySelector(e)];Array.prototype.forEach.call(l,function(a){a&&a.classList.remove("hide")})}else e.classList.remove("hide")}function v(e,t,n){e.querySelector(t)?.label(n)}function o(e,t,n){t.forEach(i=>{n?f(i,e):S(i,e)})}function me(e,t){const n=e.querySelector("#selectDisplayOrder");t==="BoxSet"?(o(e,["#fldDisplayOrder"],!0),o(e,[".seriesDisplayOrderDescription"],!1),n&&(n.innerHTML='<option value="Default">'+r.translate("DateModified")+'</option><option value="SortName">'+r.translate("SortName")+'</option><option value="PremiereDate">'+r.translate("ReleaseDate")+"</option>")):t==="Series"?(o(e,["#fldDisplayOrder",".seriesDisplayOrderDescription"],!0),n&&(n.innerHTML='<option value="">'+r.translate("Aired")+'</option><option value="originalAirDate">'+r.translate("OriginalAirDate")+'</option><option value="absolute">'+r.translate("Absolute")+'</option><option value="dvd">DVD</option><option value="digital">'+r.translate("Digital")+'</option><option value="storyArc">'+r.translate("StoryArc")+'</option><option value="production">'+r.translate("Production")+'</option><option value="tv">TV</option><option value="alternate">'+r.translate("Alternate")+'</option><option value="regional">'+r.translate("Regional")+'</option><option value="altdvd">'+r.translate("AlternateDVD")+"</option>")):(o(e,["#fldDisplayOrder"],!1),n&&(n.innerHTML=""))}function ve(e,t){const n=t.Type||"",i=["Person","Genre","Studio","MusicGenre","TvChannel"].includes(n),l=n==="Series",a=t.MediaType==="Video";o(e,["#fldPath"],!!(t.Path&&t.EnableMediaSourceDisplay!==!1)),o(e,["#fldOriginalName"],[m.Series,m.Season,m.Episode,m.Movie,m.Trailer,m.Person].includes(n)),o(e,["#fldOriginalLanguage"],l||a),o(e,["#fldSeriesRuntime"],l),o(e,["#fldEndDate"],l||n==="Person"),o(e,["#albumAssociationMessage"],n==="MusicAlbum"),o(e,["#fldCriticRating"],n==="Movie"||n==="Trailer"),o(e,["#fldStatus","#fldAirDays","#fldAirTime"],l),o(e,["#fld3dFormat"],a&&n!=="TvChannel"),o(e,["#fldArtist","#fldAlbumArtist"],[m.Audio,m.MusicAlbum,m.MusicVideo].includes(n)),o(e,["#fldAlbum"],[m.Audio,m.MusicVideo].includes(n)),o(e,["#collapsibleSpecialEpisodeInfo"],n==="Episode"&&t.ParentIndexNumber===0),o(e,["#peopleCollapsible"],!i),o(e,["#fldCommunityRating","#genresCollapsible","#studiosCollapsible"],!i),o(e,["#fldOfficialRating"],!i||n==="TvChannel"),o(e,["#fldCustomRating"],!i),f("#tagsCollapsible",e),o(e,["#metadataSettingsCollapsible","#fldPremiereDate","#fldDateAdded","#fldYear",".overviewContainer"],n!=="TvChannel"),n==="Person"?(v(e,"#txtName",r.translate("LabelName")),v(e,"#txtSortName",r.translate("LabelSortName")),v(e,"#txtOriginalName",r.translate("LabelOriginalName")),v(e,"#txtProductionYear",r.translate("LabelBirthYear")),v(e,"#txtPremiereDate",r.translate("LabelBirthDate")),v(e,"#txtEndDate",r.translate("LabelDeathDate"))):(v(e,"#txtProductionYear",r.translate("LabelYear")),v(e,"#txtPremiereDate",r.translate("LabelReleaseDate")),v(e,"#txtEndDate",r.translate("LabelEndDate"))),o(e,["#fldPlaceOfBirth"],n==="Person"),o(e,["#fldHeight"],a&&n==="TvChannel"),o(e,["#fldOriginalAspectRatio"],a&&n!=="TvChannel");const s={Episode:"LabelEpisodeNumber",Season:"LabelSeasonNumber",Audio:"LabelTrackNumber"};o(e,["#fldIndexNumber"],["Audio","Episode","Season"].includes(n)),s[n]&&v(e,"#txtIndexNumber",r.translate(s[n]));const d={Episode:"LabelSeasonNumber",Audio:"LabelDiscNumber"};o(e,["#fldParentIndexNumber"],["Audio","Episode"].includes(n)),d[n]&&v(e,"#txtParentIndexNumber",r.translate(d[n])),me(e,n)}function k(e){if(!e)return"";try{return w.parseISO8601Date(e,!0).toISOString().slice(0,10)}catch{return""}}function be(e,t,n){let i=e.querySelector("#selectOfficialRating");H(n,i,t.OfficialRating),i.value=t.OfficialRating||"",i=e.querySelector("#selectCustomRating"),H(n,i,t.CustomRating),i.value=t.CustomRating||"";const l=e.querySelector("#selectStatus");ye(l),l.value=t.Status||"",e.querySelector("#select3dFormat").value=t.Video3DFormat||"",Array.prototype.forEach.call(e.querySelectorAll(".chkAirDay"),function(u){u.checked=(t.AirDays||[]).indexOf(u.getAttribute("data-day")||"")!==-1}),L(e.querySelector("#listGenres"),t.Genres),x(e,t.People||[]),L(e.querySelector("#listStudios"),(t.Studios||[]).map(function(u){return u.Name||""})),L(e.querySelector("#listTags"),t.Tags);const a=t.LockData||!1,s=e.querySelector("#chkLockData");s.checked=a,s.checked?S(".providerSettingsContainer",e):f(".providerSettingsContainer",e),ge(e,t,t.LockedFields),e.querySelector("#txtPath").value=t.Path||"",e.querySelector("#txtName").value=t.Name||"",e.querySelector("#txtOriginalName").value=t.OriginalTitle||"",e.querySelector("#selectOriginalLanguage").value=t.OriginalLanguage||"",e.querySelector("#txtOverview").value=t.Overview||"",e.querySelector("#txtTagline").value=t.Taglines?.length?t.Taglines[0]:"",e.querySelector("#txtSortName").value=t.ForcedSortName||"",e.querySelector("#txtCommunityRating").value=t.CommunityRating||"",e.querySelector("#txtCriticRating").value=t.CriticRating||"",e.querySelector("#txtIndexNumber").value=t.IndexNumber==null?"":String(t.IndexNumber),e.querySelector("#txtParentIndexNumber").value=t.ParentIndexNumber==null?"":String(t.ParentIndexNumber),e.querySelector("#txtAirsBeforeSeason").value="AirsBeforeSeasonNumber"in t?String(t.AirsBeforeSeasonNumber):"",e.querySelector("#txtAirsAfterSeason").value="AirsAfterSeasonNumber"in t?String(t.AirsAfterSeasonNumber):"",e.querySelector("#txtAirsBeforeEpisode").value="AirsBeforeEpisodeNumber"in t?String(t.AirsBeforeEpisodeNumber):"",e.querySelector("#txtAlbum").value=t.Album||"",e.querySelector("#txtAlbumArtist").value=(t.AlbumArtists||[]).map(function(u){return u.Name}).join(";"),e.querySelector("#selectDisplayOrder").value=t.DisplayOrder||"",e.querySelector("#txtArtist").value=(t.ArtistItems||[]).map(function(u){return u.Name}).join(";"),e.querySelector("#txtDateAdded").value=k(t.DateCreated),e.querySelector("#txtPremiereDate").value=k(t.PremiereDate),e.querySelector("#txtEndDate").value=k(t.EndDate),e.querySelector("#txtProductionYear").value=String(t.ProductionYear||""),e.querySelector("#txtAirTime").value=t.AirTime||"";const d=t.ProductionLocations?.length?t.ProductionLocations[0]:"";if(e.querySelector("#txtPlaceOfBirth").value=d,e.querySelector("#selectHeight").value=t.Height||"",e.querySelector("#txtOriginalAspectRatio").value=t.AspectRatio||"",e.querySelector("#selectLanguage").value=t.PreferredMetadataLanguage||"",e.querySelector("#selectCountry").value=t.PreferredMetadataCountryCode||"",t.RunTimeTicks){const u=t.RunTimeTicks/6e8;e.querySelector("#txtSeriesRuntime").value=String(Math.round(u))}else e.querySelector("#txtSeriesRuntime").value=""}function H(e,t,n){let i="";i+="<option value=''></option>";const l=[];let a,s=!1;for(let d=0,u=e.length;d<u;d++)a=e[d],l.push({Name:a.Name,Value:a.Name}),a.Name===n&&(s=!0);n&&!s&&l.push({Name:n,Value:n});for(let d=0,u=l.length;d<u;d++)a=l[d],i+="<option value='"+p(a.Value)+"'>"+p(a.Name)+"</option>";t.innerHTML=i}function ye(e){let t="";t+='<option value=""></option>',t+=`<option value="${T.Continuing}">${p(r.translate("Continuing"))}</option>`,t+=`<option value="${T.Ended}">${p(r.translate("Ended"))}</option>`,t+=`<option value="${T.Unreleased}">${p(r.translate("Unreleased"))}</option>`,e.innerHTML=t}function L(e,t,n){let i=t||[];i=i.sort(function(a,s){return a.toLowerCase().localeCompare(s.toLowerCase())});let l="";for(let a=0;a<i.length;a++)l+='<div class="listItem">',l+='<span class="material-icons listItemIcon live_tv" aria-hidden="true" style="background-color:#333;"></span>',l+='<div class="listItemBody">',l+='<div class="textValue">',l+=p(i[a]),l+="</div>",l+="</div>",l+='<button type="button" is="paper-icon-button-light" data-index="'+a+'" class="btnRemoveFromEditorList autoSize"><span class="material-icons delete" aria-hidden="true"></span></button>',l+="</div>";e.innerHTML=l}function x(e,t){let i="";const l=e.querySelector("#peopleList");for(let a=0,s=t.length;a<s;a++){const d=t[a];i+='<div class="listItem">',i+='<span class="material-icons listItemIcon person" style="background-color:#333;"></span>',i+='<div class="listItemBody">',i+='<button style="text-align:left;" type="button" class="btnEditPerson clearButton" data-index="'+a+'">',i+='<div class="textValue">',i+=p(d.Name||""),i+="</div>",d.Role&&d.Role!==""?i+='<div class="secondary">'+p(d.Role)+"</div>":i+='<div class="secondary">'+r.translate(d.Type||"")+"</div>",i+="</button>",i+="</div>",i+='<button type="button" is="paper-icon-button-light" data-index="'+a+'" class="btnDeletePerson autoSize"><span class="material-icons delete" aria-hidden="true"></span></button>',i+="</div>"}l.innerHTML=i}function fe(e,t){let n="";for(const i of e){const l=i.name,a=i.value||i.name,s=t.indexOf(a)===-1?" checked":"";n+="<label>",n+='<input type="checkbox" is="emby-checkbox" class="selectLockedField" data-value="'+a+'"'+s+"/>",n+="<span>"+l+"</span>",n+="</label>"}return n}function ge(e,t,n){const i=e.querySelector(".providerSettingsContainer"),l=n||[],a=[{name:r.translate("Name"),value:"Name"},{name:r.translate("Overview"),value:"Overview"},{name:r.translate("Genres"),value:"Genres"},{name:r.translate("ParentalRating"),value:"OfficialRating"},{name:r.translate("People"),value:"Cast"}];t.Type==="Person"?a.push({name:r.translate("BirthLocation"),value:"ProductionLocations"}):a.push({name:r.translate("ProductionLocations"),value:"ProductionLocations"}),t.Type==="Series"&&a.push({name:r.translate("Runtime"),value:"Runtime"}),a.push({name:r.translate("Studios"),value:"Studios"}),a.push({name:r.translate("Tags"),value:"Tags"});let s="";s+="<h2>"+r.translate("HeaderEnabledFields")+"</h2>",s+="<p>"+r.translate("HeaderEnabledFieldsHelp")+"</p>",s+=fe(a,l),i.innerHTML=s}function q(e,t,n){b.show(),Promise.all([oe(t,n),de(t,n)]).then(function(i){const l=i[0];y=i[1],c=l;const a=y.Cultures||[],s=y.Countries||[];ce(e,y),pe(e,l,y.ExternalIdInfos||[]),M(e.querySelector("#selectOriginalLanguage"),a),M(e.querySelector("#selectLanguage"),a),ue(e.querySelector("#selectCountry"),s);const d=e.querySelector("#selectCountry");d&&(d.onchange=()=>{F(e)}),ve(e,l),be(e,l,y.ParentalRatingOptions||[]),F(e),l.MediaType==="Video"&&l.Type!=="Episode"&&l.Type!=="TvChannel"?f("#fldTagline",e):S("#fldTagline",e),b.hide()}).catch(i=>{b.hide(),console.error("[MetadataEditor] failed to reload metadata",i)})}function B(e,t,n){e&&D(()=>import("./index-6F8mVyyU.js").then(i=>i.fj),__vite__mapDeps([1,2]),import.meta.url).then(i=>{const l=n?"on":"off";i.centerFocus[l](e,t)}).catch(i=>console.error("[MetadataEditor] failed to center focus",i))}function he(e,t,n){b.show();const i={removeOnClose:!0,scrollY:!1,size:""};C.tv?i.size="fullscreen":i.size="small";const l=I.createDialog(i);l.classList.add("formDialog");let a="";a+=r.translateHtml(V,"core"),l.innerHTML=a,C.tv&&B(l.querySelector(".formDialogContent"),!1,!0),I.open(l).catch(s=>console.error("[MetadataEditor] failed to open dialog",s)),l.addEventListener("close",function(){C.tv&&B(l.querySelector(".formDialogContent"),!1,!1),n()}),h=l,G(l),q(l,e,t)}const qe={show:function(e,t){return new Promise(n=>he(e,t,n))},embed:function(e,t,n){return new Promise(function(i){b.show(),e.innerHTML=r.translateHtml(V,"core"),e.querySelector(".formDialogFooter")?.classList.remove("formDialogFooter"),e.querySelector(".btnClose")?.classList.add("hide"),e.querySelector(".btnHeaderSave")?.classList.remove("hide"),e.querySelector(".btnCancel")?.classList.add("hide"),h=e,G(e),q(e,t,n),K.autoFocus(e),i()})}};export{qe as default};
