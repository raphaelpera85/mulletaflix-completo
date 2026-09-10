const __vite__mapDeps=(i,m=__vite__mapDeps,d=(m.f||(m.f=["./recordinghelper-Uwa6E3ZG.js","./index-6F8mVyyU.js","./index-CQ6hRxbv.css"])))=>i.map(i=>d[i]);
import{a2 as r,f as a,l as m,c as y,bp as D,S as v,_ as P,bi as f}from"./index-6F8mVyyU.js";import"./emby-select-BS-dUTXm.js";/* empty css                         */import"./actionSheet-bHfWrycr.js";const C=`<div class="formDialogHeader">
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
`;let c,d=!1,s=!1,u,o;function k(n,e){return P(async()=>{const{default:i}=await import("./recordinghelper-Uwa6E3ZG.js");return{default:i}},__vite__mapDeps([0,1,2]),import.meta.url).then(({default:i})=>i.cancelSeriesTimerWithConfirmation(e,n.serverId()))}function h(n,e){n.querySelector("#txtPrePaddingMinutes").value=String(e.PrePaddingSeconds/60),n.querySelector("#txtPostPaddingMinutes").value=String(e.PostPaddingSeconds/60),n.querySelector(".selectChannels").value=e.RecordAnyChannel?"all":"one",n.querySelector(".selectAirTime").value=e.RecordAnyTime?"any":"original",n.querySelector(".selectShowType").value=e.RecordNewOnly?"new":"all",n.querySelector(".chkSkipEpisodesInLibrary").checked=e.SkipEpisodesInLibrary,n.querySelector(".selectKeepUpTo").value=String(e.KeepUpTo||0),e.ChannelName||e.ChannelNumber?n.querySelector(".optionChannelOnly").innerText=a.translate("ChannelNameOnly",e.ChannelName||e.ChannelNumber):n.querySelector(".optionChannelOnly").innerHTML=a.translate("OneChannel"),n.querySelector(".optionAroundTime").innerHTML=a.translate("AroundTime",f.getDisplayTime(f.parseISO8601Date(e.StartDate))),r.hide()}function g(n){d=!0,s=n,c&&y.close(c)}function w(n){const e=this,i=v.getApiClient(o);i.getLiveTvSeriesTimer(u).then(function(t){return t.PrePaddingSeconds=Number(e.querySelector("#txtPrePaddingMinutes").value)*60,t.PostPaddingSeconds=Number(e.querySelector("#txtPostPaddingMinutes").value)*60,t.RecordAnyChannel=e.querySelector(".selectChannels").value==="all",t.RecordAnyTime=e.querySelector(".selectAirTime").value==="any",t.RecordNewOnly=e.querySelector(".selectShowType").value==="new",t.SkipEpisodesInLibrary=e.querySelector(".chkSkipEpisodesInLibrary").checked,t.KeepUpTo=Number(e.querySelector(".selectKeepUpTo").value),i.updateLiveTvSeriesTimer(t)}).catch(()=>r.hide()),n.preventDefault()}function T(n){A(n),n.querySelector(".btnCancel").addEventListener("click",function(){g(!1)}),n.querySelector(".btnCancelRecording").addEventListener("click",function(){const e=v.getApiClient(o);k(e,u).then(function(){g(!0)}).catch(()=>r.hide())}),n.querySelector("form").addEventListener("submit",w)}function L(n,e){const i=v.getApiClient(o);r.show(),typeof e=="string"?(u=e,i.getLiveTvSeriesTimer(e).then(function(t){h(n,t),r.hide()}).catch(()=>r.hide())):e&&(u=e.Id,h(n,e),r.hide())}function A(n){let e="";for(let i=0;i<=50;i++){let t;i===0?t=a.translate("AsManyAsPossible"):i===1?t=a.translate("ValueOneEpisode"):t=a.translate("ValueEpisodeCount",String(i)),e+='<option value="'+i+'">'+t+"</option>"}n.querySelector(".selectKeepUpTo").innerHTML=e}function S(){this.querySelector(".btnSubmit").click()}function E(n,e,i){d=!1,s=!1,o=e,r.show(),i=i||{};const t=i.context;t.classList.add("hide"),t.innerHTML=a.translateHtml(C,"core"),t.querySelector(".formDialogHeader").classList.add("hide"),t.querySelector(".formDialogFooter").classList.add("hide"),t.querySelector(".formDialogContent").className="",t.querySelector(".dialogContentInner").className="",t.classList.remove("hide"),t.removeEventListener("change",S),t.addEventListener("change",S),c=t,T(t),L(t,n)}function $(n,e,i){return new Promise(function(t,q){d=!1,s=!1,o=e,r.show(),i=i||{};const p={removeOnClose:!0,scrollY:!1};m.tv?p.size="fullscreen":p.size="small";const l=y.createDialog(p);l.classList.add("formDialog"),l.classList.add("recordingDialog"),m.tv||(l.style.minWidth="20%");let b="";b+=a.translateHtml(C,"core"),l.innerHTML=b,i.enableCancel===!1&&l.querySelector(".formDialogFooter").classList.add("hide"),c=l,l.addEventListener("closing",function(){s||l.querySelector(".btnSubmit").click()}),l.addEventListener("close",function(){d?t({updated:!0,deleted:s}):q()}),m.tv&&D.centerFocus.on(l.querySelector(".formDialogContent"),!1),T(l),L(l,n),y.open(l).catch(()=>r.hide())})}const N={show:$,embed:E};export{N as default};
