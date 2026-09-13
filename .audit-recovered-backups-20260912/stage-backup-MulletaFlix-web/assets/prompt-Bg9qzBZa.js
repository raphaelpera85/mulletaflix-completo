import{l as r,G as i,s,aC as c,h as u}from"./index-CFwqrzSZ.js";import"./vendor-react-query-BiAmjtRH.js";import"./vendor-react-CeUY3tRn.js";import"./vendor-jellyfin-Bee54tkY.js";import"./vendor-axios-BCn5QfZZ.js";import"./vendor-lodash-CYyrkkBC.js";import"./vendor-date-fns-CwLI6ukS.js";import"./vendor-dompurify-Baz99PXY.js";const d=`<div class="formDialogHeader">
    <button is="paper-icon-button-light" class="btnCancel autoSize" tabindex="-1" title="\${ButtonBack}">
        <span class="material-icons arrow_back" aria-hidden="true"></span>
    </button>

    <h3 class="formDialogHeaderTitle"></h3>
</div>

<div class="formDialogContent smoothScrollY">
    <div class="dialogContentInner dialog-content-centered" style="padding-top:2em;">
        <form>
            <div class="inputContainer">
                <input is="emby-input" type="text" id="txtInput" label="" />
                <div class="fieldDescription"></div>
            </div>

            <div class="formDialogFooter">
                <button is="emby-button" type="submit" class="raised btnSubmit block formDialogFooterItem button-submit">
                    <span class="submitText"></span>
                </button>
            </div>
        </form>
    </div>
</div>
`;function m(t,n){const e=t.querySelector("#txtInput");e&&(e.label?e.label(n.label||""):e.setAttribute("label",n.label||""),e.value=n.value||"")}function f(t){const n={removeOnClose:!0,scrollY:!1};r.tv&&(n.size="fullscreen");const e=i.createDialog(n);e.classList.add("formDialog"),e.innerHTML=s.translateHtml(d,"core"),r.tv?c.centerFocus.on(e.querySelector(".formDialogContent"),!1):(e.querySelector(".dialogContentInner").classList.add("dialogContentInner-mini"),e.classList.add("dialog-fullscreen-lowres")),e.querySelector(".btnCancel").addEventListener("click",()=>{i.close(e)}),e.querySelector(".formDialogHeaderTitle").innerText=t.title||"";const o=e.querySelector(".fieldDescription");t.description?o.innerText=t.description:o.classList.add("hide"),m(e,t);let l="";return e.querySelector("form").addEventListener("submit",a=>(l=e.querySelector("#txtInput").value,a.preventDefault(),a.stopPropagation(),setTimeout(()=>{i.close(e)},300),!1)),e.querySelector(".submitText").innerText=t.confirmText||s.translate("ButtonOk"),e.style.minWidth=`${Math.min(400,u.getWindowSize().innerWidth-50)}px`,i.open(e).then(()=>{if(r.tv&&c.centerFocus.off(e.querySelector(".formDialogContent"),!1),l)return l;throw new Error("Prompt cancelled.")})}function h(t){return typeof t=="string"&&(t={title:"",text:t}),f(t)}export{h as default};
