const __vite__mapDeps=(i,m=__vite__mapDeps,d=(m.f||(m.f=["./index-6F8mVyyU.js","./index-CQ6hRxbv.css"])))=>i.map(i=>d[i]);
import{l as a,c as l,f,_ as v}from"./index-6F8mVyyU.js";import{P as s}from"./person-kind-mlhHzv9H.js";import"./emby-select-BS-dUTXm.js";import"./actionSheet-bHfWrycr.js";const y=`<div class="formDialogHeader">
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
`;function m(t,r,i){t&&v(()=>import("./index-6F8mVyyU.js").then(o=>o.fj),__vite__mapDeps([0,1]),import.meta.url).then(o=>{(i?o.centerFocus.on:o.centerFocus.off)(t,r)}).catch(o=>console.error("[PersonEditor] failed to center focus",o))}function b(t){return new Promise(function(r,i){const o={removeOnClose:!0,scrollY:!1};a.tv?o.size="fullscreen":o.size="small";const e=l.createDialog(o);e.classList.add("formDialog");let c="",u=!1;c+=f.translateHtml(y,"core"),e.innerHTML=c,e.querySelector(".txtPersonName").value=t.Name||"",e.querySelector(".selectPersonType").value=t.Type||"",e.querySelector(".txtPersonRole").value=t.Role||"",a.tv&&m(e.querySelector(".formDialogContent"),!1,!0),l.open(e).catch(n=>{console.error("[PersonEditor] failed to open dialog",n)}),e.addEventListener("close",function(){a.tv&&m(e.querySelector(".formDialogContent"),!1,!1),u?r(t):i()});let d="";for(const n of Object.values(s)){if(n===s.Unknown)continue;const p=t.Type===n?"selected":"";d+=`<option value="${n}" ${p}>\${${n}}</option>`}e.querySelector(".selectPersonType").innerHTML=f.translateHtml(d),e.querySelector(".selectPersonType").addEventListener("change",function(){e.querySelector(".fldRole")?.classList.toggle("hide",![s.Actor,s.GuestStar].includes(this.value))}),e.querySelector(".btnCancel")?.addEventListener("click",function(){l.close(e)}),e.querySelector("form")?.addEventListener("submit",function(n){u=!0,t.Name=e.querySelector(".txtPersonName").value,t.Type=e.querySelector(".selectPersonType").value,t.Role=e.querySelector(".txtPersonRole").value||void 0,l.close(e),n.preventDefault()}),e.querySelector(".selectPersonType")?.dispatchEvent(new CustomEvent("change",{bubbles:!0}))})}const q={show:b};export{q as default};
