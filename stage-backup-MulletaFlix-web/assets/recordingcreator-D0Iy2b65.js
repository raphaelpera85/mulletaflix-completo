const __vite__mapDeps=(i,m=__vite__mapDeps,d=(m.f||(m.f=["./recordingeditor-v40OoPgv.js","./index-6F8mVyyU.js","./index-CQ6hRxbv.css","./emby-collapse-uEmrH5zr.js","./emby-collapse-DvyxhHqx.css","./recordingcreator-B4U_xOQx.css","./seriesrecordingeditor-Blssoufg.js","./emby-select-BS-dUTXm.js","./actionSheet-bHfWrycr.js","./actionSheet-BKZT3mVB.css","./emby-select-CyC5lbTl.css"])))=>i.map(i=>d[i]);
import{S as g,O as h,f as c,d as D,a2 as l,E as f,W as B,_ as M,l as y,c as S,bp as E,p as H,e as v,bi as I}from"./index-6F8mVyyU.js";import{m as C}from"./mediainfo-DpwbX8lX.js";import{i as w}from"./imageLoader-BuyzOWYq.js";import b from"./recordinghelper-Uwa6E3ZG.js";/* empty css                        */import"./emby-collapse-uEmrH5zr.js";/* empty css                         */import"./indicators-DOHxhk9F.js";/* empty css                   *//* empty css                 */const x=`<div class="recordingFields hide">
    <div class="recordSeriesContainer recordingFields-buttons flex align-items-center hide">
        <div>
            <button is="emby-button" type="button" class="raised recordingButton seriesRecordingButton">
                <span class="material-icons recordingIcon fiber_smart_record" aria-hidden="true"></span>
                <span class="buttonText">\${RecordSeries}</span>
            </button>
        </div>
        <button is="emby-button" type="button" class="button-flat secondaryText manageRecordingButton btnManageSeriesRecording hide">
            <span class="manageButtonText">\${SeriesSettings}</span>
        </button>
    </div>

    <div class="recordingFields-buttons flex align-items-center">
        <div>
            <button is="emby-button" type="button" class="raised recordingButton singleRecordingButton">
                <span class="material-icons recordingIcon fiber_manual_record" aria-hidden="true"></span>
                <span class="buttonText">\${Record}</span>
            </button>
        </div>
        <button is="emby-button" type="button" class="button-flat secondaryText manageRecordingButton btnManageRecording hide">
            <span class="manageButtonText">\${Settings}</span>
        </button>
    </div>
</div>
`;function _(e,n){n.IsSeries?e.querySelector(".recordSeriesContainer").classList.remove("hide"):e.querySelector(".recordSeriesContainer").classList.add("hide"),n.SeriesTimerId?(e.querySelector(".btnManageSeriesRecording").classList.remove("hide"),e.querySelector(".seriesRecordingButton .recordingIcon").classList.add("recordingIcon-active"),e.querySelector(".seriesRecordingButton .buttonText").innerHTML=c.translate("CancelSeries")):(e.querySelector(".btnManageSeriesRecording").classList.add("hide"),e.querySelector(".seriesRecordingButton .recordingIcon").classList.remove("recordingIcon-active"),e.querySelector(".seriesRecordingButton .buttonText").innerHTML=c.translate("RecordSeries")),n.TimerId&&n.Status!=="Cancelled"?(e.querySelector(".btnManageRecording").classList.remove("hide"),e.querySelector(".singleRecordingButton .recordingIcon").classList.add("recordingIcon-active"),n.Status==="InProgress"?e.querySelector(".singleRecordingButton .buttonText").innerHTML=c.translate("StopRecording"):e.querySelector(".singleRecordingButton .buttonText").innerHTML=c.translate("DoNotRecord")):(e.querySelector(".btnManageRecording").classList.add("hide"),e.querySelector(".singleRecordingButton .recordingIcon").classList.remove("recordingIcon-active"),e.querySelector(".singleRecordingButton .buttonText").innerHTML=c.translate("Record"))}function d(e){const n=e.options,t=g.getApiClient(n.serverId);return n.parent.querySelector(".recordingFields").classList.remove("hide"),t.getLiveTvProgram(n.programId,t.getCurrentUserId()).then(function(i){e.TimerId=i.TimerId,e.Status=i.Status,e.SeriesTimerId=i.SeriesTimerId,_(n.parent,i)})}function L(e,n){const t=n.options;(e?.Id&&n.TimerId===e.Id||e?.ProgramId&&t&&t.programId===e.ProgramId)&&n.refresh()}function R(e,n){const t=n.options;(e?.Id&&n.SeriesTimerId===e.Id||e?.ProgramId&&t&&t.programId===e.ProgramId)&&n.refresh()}class F{constructor(n){this.options=n,this.embed().catch(i=>console.error("Failed to embed recording fields",i));const t=g.getApiClient(n.serverId);this._unsubscribeTimers=[t?.subscribe([h.TimerCreated],({Data:i})=>L(i,this)),t?.subscribe([h.TimerCancelled],({Data:i})=>L(i,this)),t?.subscribe([h.SeriesTimerCreated],({Data:i})=>R(i,this)),t?.subscribe([h.SeriesTimerCancelled],({Data:i})=>R(i,this))].filter(Boolean)}embed(){const n=this;return new Promise(function(t,i){const r=n.options.parent;r.innerHTML=c.translateHtml(x,"core"),r.querySelector(".singleRecordingButton").addEventListener("click",U.bind(n)),r.querySelector(".seriesRecordingButton").addEventListener("click",N.bind(n)),r.querySelector(".btnManageRecording").addEventListener("click",k.bind(n)),r.querySelector(".btnManageSeriesRecording").addEventListener("click",O.bind(n)),d(n).then(t,i)})}hasChanged(){return this.changed}refresh(){d(this).catch(n=>console.error("Failed to refresh recording fields",n))}destroy(){this._unsubscribeTimers?.forEach(n=>{n()}),this._unsubscribeTimers=[]}}function k(){const e=this.options;if(!this.TimerId||this.Status==="Cancelled")return;const n=this;M(async()=>{const{default:t}=await import("./recordingeditor-v40OoPgv.js");return{default:t}},__vite__mapDeps([0,1,2,3,4,5]),import.meta.url).then(({default:t})=>t.show(n.TimerId,e.serverId,{enableCancel:!1}).then(function(){n.changed=!0})).catch(t=>console.error("Failed to open recording editor",t))}function O(){const e=this.options;if(!this.SeriesTimerId)return;const n=this;M(async()=>{const{default:t}=await import("./seriesrecordingeditor-Blssoufg.js");return{default:t}},__vite__mapDeps([6,1,2,7,8,9,10,5]),import.meta.url).then(({default:t})=>t.show(n.SeriesTimerId,e.serverId,{enableCancel:!1}).then(function(){n.changed=!0})).catch(t=>console.error("Failed to open series recording editor",t))}function U(e){this.changed=!0;const n=this,t=this.options,i=g.getApiClient(t.serverId),r=!D.parentWithTag(e.target,"BUTTON").querySelector(".material-icons").classList.contains("recordingIcon-active"),o=this.TimerId&&this.Status!=="Cancelled";r?o||(l.show(),b.createRecording(i,t.programId,!1).then(function(){return f.trigger(n,"recordingchanged"),d(n)}).catch(a=>console.error("Failed to create recording",a)).finally(()=>l.hide())):o&&(l.show(),b.cancelTimer(i,this.TimerId,!0).then(function(){return f.trigger(n,"recordingchanged"),d(n)}).catch(a=>console.error("Failed to cancel recording",a)).finally(()=>l.hide()))}function N(e){this.changed=!0;const n=this,t=this.options,i=g.getApiClient(t.serverId);!D.parentWithTag(e.target,"BUTTON").querySelector(".material-icons").classList.contains("recordingIcon-active")?(t.parent.querySelector(".recordSeriesContainer").classList.remove("hide"),this.SeriesTimerId||(this.TimerId?b.changeRecordingToSeries(i,this.TimerId,t.programId):b.createRecording(i,t.programId,!0)).then(function(){return d(n)}).catch(a=>console.error("Failed to create series recording",a))):this.SeriesTimerId&&i.cancelLiveTvSeriesTimer(this.SeriesTimerId).then(function(){return B(c.translate("RecordingCancelled")),d(n)}).catch(o=>console.error("Failed to cancel series recording",o))}const $=`<div class="formDialogHeader">
    <button is="paper-icon-button-light" class="btnCancel autoSize" tabindex="-1" title="\${ButtonBack}"><span class="material-icons arrow_back" aria-hidden="true"></span></button>
    <h3 class="formDialogHeaderTitle"></h3>
</div>
<div class="formDialogContent smoothScrollY">
    <form class="dialogContentInner dialog-content-centered">

        <div class="recordingDetailsContainer">
            <div class="recordingDialog-imageContainer">

            </div>
            <div class="recordingDetails">
                <h1 class="programDialog-itemName recordingDialog-itemName dialogContentTitle"></h1>
                <p class="itemMiscInfoPrimary recordingDetailText"></p>
                <p class="itemMiscInfoSecondary recordingDetailText secondaryText"></p>
                <p class="itemGenres secondaryText"></p>

                <div style="margin:.5em 0 1em;" class="recordingFields">
                </div>
            </div>
        </div>

        <p class="itemOverview"></p>
        <br />
        <div class="formDialogFooter hide">
            <button is="emby-button" type="button" class="raised btnPlay block formDialogFooterItem button-submit">
                <span>\${Play}</span>
            </button>
        </div>
    </form>
</div>
`,z="data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVQIW2NgYAAAAAMAAdzsfUgAAAAASUVORK5CYII=";let p,T,u;function A(){p&&S.close(p)}function V(e){e.querySelector(".btnPlay").addEventListener("click",function(){T="play",A()}),e.querySelector(".btnCancel").addEventListener("click",function(){T=null,A()})}function W(e,n,t){const i=e.ImageTags||{};return e.PrimaryImageTag&&(i.Primary=e.PrimaryImageTag),i.Primary?n.getScaledImageUrl(e.Id,{type:"Primary",maxHeight:t,tag:e.ImageTags.Primary}):i.Thumb?n.getScaledImageUrl(e.Id,{type:"Thumb",maxHeight:t,tag:e.ImageTags.Thumb}):null}function G(e,n,t,i,s){if(!s){const r=W(t,i,200),o=e.querySelector(".recordingDialog-imageContainer");r?(o.innerHTML=`<img src="${v(z)}" data-src="${v(r)}" class="recordingDialog-img lazy" />`,o.classList.remove("hide"),w.lazyChildren(o)):(o.innerHTML="",o.classList.add("hide")),e.querySelector(".recordingDialog-itemName").innerText=t.Name,e.querySelector(".formDialogHeaderTitle").innerText=t.Name,e.querySelector(".itemGenres").innerText=(t.Genres||[]).join(" / "),e.querySelector(".itemOverview").innerText=t.Overview||"";const a=e.querySelector(".formDialogFooter"),m=new Date;m>=I.parseISO8601Date(t.StartDate,!0)&&m<I.parseISO8601Date(t.EndDate,!0)?a.classList.remove("hide"):a.classList.add("hide"),e.querySelector(".itemMiscInfoPrimary").innerHTML=C.getPrimaryMediaInfoHtml(t)}e.querySelector(".itemMiscInfoSecondary").innerHTML=C.getSecondaryMediaInfoHtml(t,{}),l.hide()}function q(e,n,t,i){l.show();const s=g.getApiClient(t),r=s.getNewLiveTvTimerDefaults({programId:n}),o=s.getLiveTvProgram(n,s.getCurrentUserId());Promise.all([r,o]).then(function(a){const m=a[0],P=a[1];G(e,m,P,s,!!i)}).catch(()=>{})}function Y(e,n,t){if(e==="play"){const i=g.getApiClient(t);i.getLiveTvProgram(n,i.getCurrentUserId()).then(function(s){H.play({ids:[s.ChannelId],serverId:t})})}}function Q(e,n){return new Promise(function(t,i){T=null,l.show();const s={removeOnClose:!0,scrollY:!1};y.tv?s.size="fullscreen":s.size="small";const r=S.createDialog(s);r.classList.add("formDialog"),r.classList.add("recordingDialog");let o="";o+=c.translateHtml($,"core"),r.innerHTML=o,p=r;function a(){q(r,e,n,!0)}r.addEventListener("close",function(){u&&f.off(u,"recordingchanged",a),Y(T,e,n),u?.hasChanged()?t():i()}),y.tv&&E.centerFocus.on(r.querySelector(".formDialogContent"),!1),V(r),q(r,e,n),u=new F({parent:r.querySelector(".recordingFields"),programId:e,serverId:n}),f.on(u,"recordingchanged",a),S.open(r).catch(()=>{})})}const oe={show:Q};export{oe as default};
