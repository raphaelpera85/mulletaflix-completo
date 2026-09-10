const __vite__mapDeps=(i,m=__vite__mapDeps,d=(m.f||(m.f=["./recordinghelper-Uwa6E3ZG.js","./index-6F8mVyyU.js","./index-CQ6hRxbv.css"])))=>i.map(i=>d[i]);
import{a2 as o,l as s,c as d,f as b,bp as h,S as u,_ as S}from"./index-6F8mVyyU.js";import"./emby-collapse-uEmrH5zr.js";/* empty css                         */const P=`<div class="formDialogHeader">
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
`;let c,a=!1,f,l,v;function y(e,n){return S(async()=>{const{default:i}=await import("./recordinghelper-Uwa6E3ZG.js");return{default:i}},__vite__mapDeps([0,1,2]),import.meta.url).then(({default:i})=>i.cancelTimerWithConfirmation(n,e.serverId()))}function C(e,n){e.querySelector("#txtPrePaddingMinutes").value=String(n.PrePaddingSeconds/60),e.querySelector("#txtPostPaddingMinutes").value=String(n.PostPaddingSeconds/60),o.hide()}function p(e){a=e,c&&d.close(c)}function D(e){const n=this,i=u.getApiClient(l);i.getLiveTvTimer(f).then(function(r){return r.PrePaddingSeconds=Number(n.querySelector("#txtPrePaddingMinutes").value)*60,r.PostPaddingSeconds=Number(n.querySelector("#txtPostPaddingMinutes").value)*60,i.updateLiveTvTimer(r)}).then(v).catch(()=>{o.hide()}),e.preventDefault()}function L(e){e.querySelector(".btnCancel").addEventListener("click",function(){p(!1)}),e.querySelector(".btnCancelRecording").addEventListener("click",function(){const n=u.getApiClient(l);y(n,f).then(function(){p(!0)}).catch(()=>o.hide())}),e.querySelector("form").addEventListener("submit",D)}function q(e,n){o.show(),f=n,u.getApiClient(l).getLiveTvTimer(n).then(function(r){C(e,r),o.hide()}).catch(()=>o.hide())}function w(e,n,i){return new Promise(function(r){a=!1,l=n,o.show(),i=i||{},v=r;const m={removeOnClose:!0,scrollY:!1};s.tv&&(m.size="fullscreen");const t=d.createDialog(m);t.classList.add("formDialog"),t.classList.add("recordingDialog"),s.tv||(t.style.minWidth="20%",t.classList.add("dialog-fullscreen-lowres"));let g="";g+=b.translateHtml(P,"core"),t.innerHTML=g,i.enableCancel===!1&&t.querySelector(".formDialogFooter").classList.add("hide"),c=t,t.addEventListener("closing",function(){a||t.querySelector(".btnSubmit").click()}),t.addEventListener("close",function(){a&&r({updated:!0,deleted:!0})}),s.tv&&h.centerFocus.on(t.querySelector(".formDialogContent"),!1),L(t),q(t,e),d.open(t).catch(()=>o.hide())})}const H={show:w};export{H as default};
