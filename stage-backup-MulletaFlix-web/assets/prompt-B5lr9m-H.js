import{l as r,c as l,f as s,bp as c,d as u}from"./index-6F8mVyyU.js";const d=`<div class="formDialogHeader">
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
`;function f(t,n){const e=t.querySelector("#txtInput");e&&(e.label?e.label(n.label||""):e.setAttribute("label",n.label||""),e.value=n.value||"")}function m(t){const n={removeOnClose:!0,scrollY:!1};r.tv&&(n.size="fullscreen");const e=l.createDialog(n);e.classList.add("formDialog"),e.innerHTML=s.translateHtml(d,"core"),r.tv?c.centerFocus.on(e.querySelector(".formDialogContent"),!1):(e.querySelector(".dialogContentInner").classList.add("dialogContentInner-mini"),e.classList.add("dialog-fullscreen-lowres")),e.querySelector(".btnCancel").addEventListener("click",()=>{l.close(e)}),e.querySelector(".formDialogHeaderTitle").innerText=t.title||"";const a=e.querySelector(".fieldDescription");t.description?a.innerText=t.description:a.classList.add("hide"),f(e,t);let i="";return e.querySelector("form").addEventListener("submit",o=>(i=e.querySelector("#txtInput").value,o.preventDefault(),o.stopPropagation(),setTimeout(()=>{l.close(e)},300),!1)),e.querySelector(".submitText").innerText=t.confirmText||s.translate("ButtonOk"),e.style.minWidth=`${Math.min(400,u.getWindowSize().innerWidth-50)}px`,l.open(e).then(()=>(r.tv&&c.centerFocus.off(e.querySelector(".formDialogContent"),!1),i||Promise.reject()))}function p(t){return typeof t=="string"&&(t={title:"",text:t}),m(t)}export{p as default};
