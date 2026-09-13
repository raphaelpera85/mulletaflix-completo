const __vite__mapDeps=(i,m=__vite__mapDeps,d=(m.f||(m.f=["./recordinghelper-C2nBXcGY.js","./index-CFwqrzSZ.js","./vendor-react-query-BiAmjtRH.js","./vendor-react-CeUY3tRn.js","./vendor-jellyfin-Bee54tkY.js","./vendor-axios-BCn5QfZZ.js","./vendor-lodash-CYyrkkBC.js","./vendor-date-fns-CwLI6ukS.js","./vendor-dompurify-Baz99PXY.js","./index-BAjafMOe.css"])))=>i.map(i=>d[i]);
import{_ as P}from"./vendor-react-query-BiAmjtRH.js";import{s as r,l as p,G as m,aC as k,S as y,D as S,ag as f}from"./index-CFwqrzSZ.js";import"./emby-checkbox-CDwDlGOj.js";import"./emby-select-BQevfB2a.js";/* empty css                         */import"./vendor-react-CeUY3tRn.js";import"./vendor-jellyfin-Bee54tkY.js";import"./vendor-axios-BCn5QfZZ.js";import"./vendor-lodash-CYyrkkBC.js";import"./vendor-date-fns-CwLI6ukS.js";import"./vendor-dompurify-Baz99PXY.js";import"./actionSheet-BM-G2WQD.js";/* empty css                 */const C=`<div class="formDialogHeader">
    <button is="paper-icon-button-light" class="btnCancel autoSize" tabindex="-1" title="\${ButtonBack}"><span class="material-icons arrow_back" aria-hidden="true"></span></button>
    <h3 class="formDialogHeaderTitle">
        \${HeaderSeriesOptions}
    </h3>
</div>

<div class="formDialogContent smoothScrollY">
    <div class="dialogContentInner dialog-content-centered" style="padding-top:2em;">

        <form style="max-width: none;">
            <div class="selectContainer">
                <select is="emby-select" class="selectShowType" label="\${LabelRecord}">
                    <option value="new">\${NewEpisodesOnly}</option>
                    <option value="all">\${AllEpisodes}</option>
                </select>
            </div>

            <div class="checkboxContainer checkboxContainer-withDescription">
                <label>
                    <input type="checkbox" is="emby-checkbox" class="chkSkipEpisodesInLibrary" />
                    <span>\${SkipEpisodesAlreadyInMyLibrary}</span>
                </label>
                <div class="fieldDescription checkboxFieldDescription">\${SkipEpisodesAlreadyInMyLibraryHelp}</div>
            </div>

            <div class="selectContainer">
                <select is="emby-select" class="selectChannels" label="\${LabelChannels}">
                    <option class="optionChannelOnly" value="one">\${OneChannel}</option>
                    <option value="all">\${AllChannels}</option>
                </select>
            </div>

            <div class="selectContainer">
                <select is="emby-select" class="selectAirTime" label="\${LabelAirTime}">
                    <option class="optionAroundTime" value="original"></option>
                    <option value="any">\${Anytime}</option>
                </select>
            </div>

            <div class="selectContainer">
                <select is="emby-select" class="selectKeepUpTo" label="\${LabelKeepUpTo}"></select>
            </div>

            <div class="inputContainer">
                <div class="flex align-items-center">
                    <div class="flex-grow">
                        <input is="emby-input" type="number" id="txtPrePaddingMinutes" pattern="[0-9]*" required="required" min="0" step="1" label="\${LabelStartWhenPossible}" />
                    </div>
                    <div class="fieldDescription" style="margin-left:.5em;font-size:90%;margin-top:1.3em;">
                        \${MinutesBefore}
                    </div>
                </div>
            </div>
            <div class="inputContainer">
                <div class="flex align-items-center">
                    <div class="flex-grow">
                        <input is="emby-input" type="number" id="txtPostPaddingMinutes" pattern="[0-9]*" required="required" min="0" step="1" label="\${LabelStopWhenPossible}" />
                    </div>
                    <div class="fieldDescription" style="margin-left:.5em;font-size:90%;margin-top:1.3em;">
                        \${MinutesAfter}
                    </div>
                </div>
            </div>
            <br />

            <div class="formDialogFooter">
                <button is="emby-button" type="submit" class="raised btnSubmit block formDialogFooterItem button-submit hide">
                    <span>\${Save}</span>
                </button>
                <button is="emby-button" type="button" class="raised btnCancelRecording block formDialogFooterItem button-cancel" style="white-space: nowrap;">
                    <span>\${HeaderCancelRecording}</span>
                </button>
            </div>

        </form>
    </div>
</div>
`;let s,c=!1,o=!1,d,a;function A(t,e){return P(async()=>{const{default:i}=await import("./recordinghelper-C2nBXcGY.js");return{default:i}},__vite__mapDeps([0,1,2,3,4,5,6,7,8,9]),import.meta.url).then(({default:i})=>i.cancelSeriesTimerWithConfirmation(e,t.serverId()))}function b(t,e){t.querySelector("#txtPrePaddingMinutes").value=String(e.PrePaddingSeconds/60),t.querySelector("#txtPostPaddingMinutes").value=String(e.PostPaddingSeconds/60),t.querySelector(".selectChannels").value=e.RecordAnyChannel?"all":"one",t.querySelector(".selectAirTime").value=e.RecordAnyTime?"any":"original",t.querySelector(".selectShowType").value=e.RecordNewOnly?"new":"all",t.querySelector(".chkSkipEpisodesInLibrary").checked=e.SkipEpisodesInLibrary,t.querySelector(".selectKeepUpTo").value=String(e.KeepUpTo||0),e.ChannelName||e.ChannelNumber?t.querySelector(".optionChannelOnly").innerText=r.translate("ChannelNameOnly",e.ChannelName||e.ChannelNumber):t.querySelector(".optionChannelOnly").innerHTML=r.translate("OneChannel"),t.querySelector(".optionAroundTime").innerHTML=r.translate("AroundTime",f.getDisplayTime(f.parseISO8601Date(e.StartDate)))}function g(t){c=!0,o=t,s&&m.close(s)}function w(t){const e=this,i=y.getApiClient(a);S.withLoading(()=>i.getLiveTvSeriesTimer(d).then(function(n){return n.PrePaddingSeconds=Number(e.querySelector("#txtPrePaddingMinutes").value)*60,n.PostPaddingSeconds=Number(e.querySelector("#txtPostPaddingMinutes").value)*60,n.RecordAnyChannel=e.querySelector(".selectChannels").value==="all",n.RecordAnyTime=e.querySelector(".selectAirTime").value==="any",n.RecordNewOnly=e.querySelector(".selectShowType").value==="new",n.SkipEpisodesInLibrary=e.querySelector(".chkSkipEpisodesInLibrary").checked,n.KeepUpTo=Number(e.querySelector(".selectKeepUpTo").value),i.updateLiveTvSeriesTimer(n)})).catch(n=>console.error("Failed to update series recording",n)),t.preventDefault()}function L(t){E(t),t.querySelector(".btnCancel").addEventListener("click",function(){g(!1)}),t.querySelector(".btnCancelRecording").addEventListener("click",function(){const e=y.getApiClient(a);A(e,d).then(function(){g(!0)}).catch(i=>console.error("Failed to cancel series recording",i))}),t.querySelector("form").addEventListener("submit",w)}function T(t,e){const i=y.getApiClient(a);typeof e=="string"?(d=e,S.withLoading(()=>i.getLiveTvSeriesTimer(e)).then(function(n){b(t,n)}).catch(n=>console.error("Failed to load series recording",n))):e&&(d=e.Id,b(t,e))}function E(t){let e="";for(let i=0;i<=50;i++){let n;i===0?n=r.translate("AsManyAsPossible"):i===1?n=r.translate("ValueOneEpisode"):n=r.translate("ValueEpisodeCount",String(i)),e+='<option value="'+i+'">'+n+"</option>"}t.querySelector(".selectKeepUpTo").innerHTML=e}function h(){this.querySelector(".btnSubmit").click()}function $(t,e,i){c=!1,o=!1,a=e,i=i||{};const n=i.context;n.classList.add("hide"),n.innerHTML=r.translateHtml(C,"core"),n.querySelector(".formDialogHeader").classList.add("hide"),n.querySelector(".formDialogFooter").classList.add("hide"),n.querySelector(".formDialogContent").className="",n.querySelector(".dialogContentInner").className="",n.classList.remove("hide"),n.removeEventListener("change",h),n.addEventListener("change",h),s=n,L(n),T(n,t)}function I(t,e,i){return new Promise(function(n,q){c=!1,o=!1,a=e,i=i||{};const u={removeOnClose:!0,scrollY:!1};p.tv?u.size="fullscreen":u.size="small";const l=m.createDialog(u);l.classList.add("formDialog"),l.classList.add("recordingDialog"),p.tv||(l.style.minWidth="20%");let v="";v+=r.translateHtml(C,"core"),l.innerHTML=v,i.enableCancel===!1&&l.querySelector(".formDialogFooter").classList.add("hide"),s=l,l.addEventListener("closing",function(){o||l.querySelector(".btnSubmit").click()}),l.addEventListener("close",function(){c?n({updated:!0,deleted:o}):q()}),p.tv&&k.centerFocus.on(l.querySelector(".formDialogContent"),!1),L(l),T(l,t),m.open(l).catch(D=>console.error("Failed to open series recording editor",D))})}const V={show:I,embed:$};export{V as default};
