const __vite__mapDeps=(i,m=__vite__mapDeps,d=(m.f||(m.f=["./index-CFwqrzSZ.js","./vendor-react-query-BiAmjtRH.js","./vendor-react-CeUY3tRn.js","./vendor-jellyfin-Bee54tkY.js","./vendor-axios-BCn5QfZZ.js","./vendor-lodash-CYyrkkBC.js","./vendor-date-fns-CwLI6ukS.js","./vendor-dompurify-Baz99PXY.js","./index-BAjafMOe.css"])))=>i.map(i=>d[i]);
import{_ as v}from"./vendor-react-query-BiAmjtRH.js";import{t as l}from"./vendor-jellyfin-Bee54tkY.js";import{l as a,G as s,s as m}from"./index-CFwqrzSZ.js";import"./emby-select-BQevfB2a.js";import"./vendor-react-CeUY3tRn.js";import"./vendor-axios-BCn5QfZZ.js";import"./vendor-lodash-CYyrkkBC.js";import"./vendor-date-fns-CwLI6ukS.js";import"./vendor-dompurify-Baz99PXY.js";import"./actionSheet-BM-G2WQD.js";/* empty css                 */const y=`<div class="formDialogHeader">
    <button is="paper-icon-button-light" class="btnCancel autoSize" tabindex="-1" title="\${ButtonBack}"><span class="material-icons arrow_back" aria-hidden="true"></span></button>
    <h3 class="formDialogHeaderTitle">
        \${Edit}
    </h3>
</div>

<div class="formDialogContent smoothScrollY" style="padding-top:2em;">
    <form class="popupEditPersonForm dialogContentInner dialog-content-centered">
        <div class="inputContainer">
            <input type="text" is="emby-input" class="txtPersonName" required="required" label="\${LabelName}" />
        </div>

        <div class="selectContainer">
            <select is="emby-select" id="selectPersonType" class="selectPersonType" label="\${LabelType}"></select>
        </div>

        <div class="inputContainer fldRole hide">
            <input is="emby-input" type="text" class="txtPersonRole" label="\${LabelPersonRole}" />
            <div class="fieldDescription">\${LabelPersonRoleHelp}</div>
        </div>

        <div class="formDialogFooter">
            <button is="emby-button" type="submit" class="raised button-submit block formDialogFooterItem">
                <span>\${Save}</span>
            </button>
        </div>
    </form>
</div>
`;function p(t,r,i){t&&v(()=>import("./index-CFwqrzSZ.js").then(o=>o.bu),__vite__mapDeps([0,1,2,3,4,5,6,7,8]),import.meta.url).then(o=>{(i?o.centerFocus.on:o.centerFocus.off)(t,r)}).catch(o=>console.error("[PersonEditor] failed to center focus",o))}function b(t){return new Promise(function(r,i){const o={removeOnClose:!0,scrollY:!1};a.tv?o.size="fullscreen":o.size="small";const e=s.createDialog(o);e.classList.add("formDialog");let c="",u=!1;c+=m.translateHtml(y,"core"),e.innerHTML=c,e.querySelector(".txtPersonName").value=t.Name||"",e.querySelector(".selectPersonType").value=t.Type||"",e.querySelector(".txtPersonRole").value=t.Role||"",a.tv&&p(e.querySelector(".formDialogContent"),!1,!0),s.open(e).catch(n=>{console.error("[PersonEditor] failed to open dialog",n)}),e.addEventListener("close",function(){a.tv&&p(e.querySelector(".formDialogContent"),!1,!1),u?r(t):i()});let d="";for(const n of Object.values(l)){if(n===l.Unknown)continue;const f=t.Type===n?"selected":"";d+=`<option value="${n}" ${f}>\${${n}}</option>`}e.querySelector(".selectPersonType").innerHTML=m.translateHtml(d),e.querySelector(".selectPersonType").addEventListener("change",function(){e.querySelector(".fldRole")?.classList.toggle("hide",![l.Actor,l.GuestStar].includes(this.value))}),e.querySelector(".btnCancel")?.addEventListener("click",function(){s.close(e)}),e.querySelector("form")?.addEventListener("submit",function(n){u=!0,t.Name=e.querySelector(".txtPersonName").value,t.Type=e.querySelector(".selectPersonType").value,t.Role=e.querySelector(".txtPersonRole").value||void 0,s.close(e),n.preventDefault()}),e.querySelector(".selectPersonType")?.dispatchEvent(new CustomEvent("change",{bubbles:!0}))})}const x={show:b};export{x as default};
