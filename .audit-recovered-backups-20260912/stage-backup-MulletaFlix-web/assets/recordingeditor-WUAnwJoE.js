const __vite__mapDeps=(i,m=__vite__mapDeps,d=(m.f||(m.f=["./recordinghelper-C2nBXcGY.js","./index-CFwqrzSZ.js","./vendor-react-query-BiAmjtRH.js","./vendor-react-CeUY3tRn.js","./vendor-jellyfin-Bee54tkY.js","./vendor-axios-BCn5QfZZ.js","./vendor-lodash-CYyrkkBC.js","./vendor-date-fns-CwLI6ukS.js","./vendor-dompurify-Baz99PXY.js","./index-BAjafMOe.css"])))=>i.map(i=>d[i]);
import{_ as y}from"./vendor-react-query-BiAmjtRH.js";import{l,G as s,s as S,aC as P,S as c,D as u}from"./index-CFwqrzSZ.js";import"./emby-collapse-Jnj7ypJU.js";/* empty css                         */import"./vendor-react-CeUY3tRn.js";import"./vendor-jellyfin-Bee54tkY.js";import"./vendor-axios-BCn5QfZZ.js";import"./vendor-lodash-CYyrkkBC.js";import"./vendor-date-fns-CwLI6ukS.js";import"./vendor-dompurify-Baz99PXY.js";const h=`<div class="formDialogHeader">
    <button is="paper-icon-button-light" class="btnCancel autoSize" tabindex="-1" title="\${ButtonBack}"><span class="material-icons arrow_back" aria-hidden="true"></span></button>
    <h3 class="formDialogHeaderTitle">
        \${HeaderRecordingOptions}
    </h3>
</div>

<div class="formDialogContent smoothScrollY">
    <div class="dialogContentInner dialog-content-centered">

        <form>
            <br />
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
                <button is="emby-button" type="button" class="raised btnCancelRecording block formDialogFooterItem button-cancel" style="white-space:nowrap;">
                    <span>\${HeaderCancelRecording}</span>
                </button>
            </div>

        </form>
    </div>
</div>
`;let d,o=!1,m,a,v;function C(e,t){return y(async()=>{const{default:n}=await import("./recordinghelper-C2nBXcGY.js");return{default:n}},__vite__mapDeps([0,1,2,3,4,5,6,7,8,9]),import.meta.url).then(({default:n})=>n.cancelTimerWithConfirmation(t,e.serverId()))}function D(e,t){e.querySelector("#txtPrePaddingMinutes").value=String(t.PrePaddingSeconds/60),e.querySelector("#txtPostPaddingMinutes").value=String(t.PostPaddingSeconds/60)}function p(e){o=e,d&&s.close(d)}function L(e){const t=this,n=c.getApiClient(a);u.withLoading(async()=>{try{const r=await n.getLiveTvTimer(m);r.PrePaddingSeconds=Number(t.querySelector("#txtPrePaddingMinutes").value)*60,r.PostPaddingSeconds=Number(t.querySelector("#txtPostPaddingMinutes").value)*60,await n.updateLiveTvTimer(r),v()}catch(r){console.error("[RecordingEditor] failed to update timer",r)}}),e.preventDefault()}function w(e){e.querySelector(".btnCancel").addEventListener("click",function(){p(!1)}),e.querySelector(".btnCancelRecording").addEventListener("click",function(){const t=c.getApiClient(a);u.withLoading(async()=>{try{await C(t,m),p(!0)}catch(n){console.error("[RecordingEditor] failed to delete timer",n)}})}),e.querySelector("form").addEventListener("submit",L)}function q(e,t){m=t;const n=c.getApiClient(a);u.withLoading(async()=>{try{const r=await n.getLiveTvTimer(t);D(e,r)}catch(r){console.error("[RecordingEditor] failed to load timer",r)}})}function E(e,t,n){return new Promise(function(r){o=!1,a=t,n=n||{},v=r;const g={removeOnClose:!0,scrollY:!1};l.tv&&(g.size="fullscreen");const i=s.createDialog(g);i.classList.add("formDialog"),i.classList.add("recordingDialog"),l.tv||(i.style.minWidth="20%",i.classList.add("dialog-fullscreen-lowres"));let f="";f+=S.translateHtml(h,"core"),i.innerHTML=f,n.enableCancel===!1&&i.querySelector(".formDialogFooter").classList.add("hide"),d=i,i.addEventListener("closing",function(){o||i.querySelector(".btnSubmit").click()}),i.addEventListener("close",function(){o&&r({updated:!0,deleted:!0})}),l.tv&&P.centerFocus.on(i.querySelector(".formDialogContent"),!1),w(i),q(i,e),s.open(i).catch(b=>{console.error("[RecordingEditor] failed to open dialog",b)})})}const A={show:E};export{A as default};
