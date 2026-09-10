const __vite__mapDeps=(i,m=__vite__mapDeps,d=(m.f||(m.f=["./index-6F8mVyyU.js","./index-CQ6hRxbv.css"])))=>i.map(i=>d[i]);
import{c as s,f as n,W as m,d,a2 as u,j as h,e as p,_ as C,a1 as w}from"./index-6F8mVyyU.js";import{$ as P}from"./jquery-PwHOrod8.js";import{l as v}from"./emby-toggle-VTx5lffU.js";import"./emby-select-BS-dUTXm.js";import"./actionSheet-bHfWrycr.js";import"./emby-textarea-BTRMqmjp.js";const k=`<div class="formDialogHeader">
    <button type="button" is="paper-icon-button-light" class="btnCancel autoSize" tabindex="-1" title="\${ButtonBack}"><span class="material-icons arrow_back" aria-hidden="true"></span></button>
    <h3 class="formDialogHeaderTitle"></h3>
</div>

<div class="formDialogContent scrollY" style="padding-top:2em;">
    <div class="dialogContentInner dialog-content-centered">
        <div class="infoBanner" style="margin-bottom:1.8em;">
            \${ChangingMetadataImageSettingsNewContent}
        </div>

        <div class="folders hide">
            <div style="display: flex; align-items: center;">
                <h1 style="margin: .5em 0;">\${Folders}</h1>
                <button is="emby-button" type="button" class="fab btnAddFolder submit" title="\${Add}">
                    <span class="material-icons add" aria-hidden="true"></span>
                </button>
            </div>
            <div class="paperList folderList" style="margin-bottom:2em;"></div>
        </div>

        <div class="libraryOptions"></div>
    </div>
</div>

<div class="formDialogFooter">
    <button is="emby-button" type="button" class="raised btnSubmit button-submit block formDialogFooterItem">
        <span>\${ButtonOk}</span>
    </button>
</div>
`;let g,a,c=!1,b=!1;function D(){if(b)return!1;b=!0,u.show();const r=d.parentWithClass(this,"dlg-libraryeditor"),i=t=>{let e=v.getLibraryOptions(r.querySelector(".libraryOptions"));e=Object.assign(a.library.LibraryOptions||{},e),window.ApiClient.updateVirtualFolderOptions(t,e).then(()=>{c=!0,b=!1,u.hide(),s.close(r)},()=>{b=!1,u.hide()})};return a.library.ItemId?(i(a.library.ItemId),!1):(window.ApiClient.getVirtualFolders().then(t=>{const e=(t||[]).find(o=>o.Name===a.library.Name);e&&e.ItemId?(a.library=e,i(e.ItemId)):(u.hide(),s.close(r),h({text:n.translate("LibraryInvalidItemIdError")}))},()=>{u.hide(),s.close(r),h({text:n.translate("LibraryInvalidItemIdError")})}),!1)}function E(r,i){const t=a.library,e=a.refresh;t.Locations.some(l=>i===l)||window.ApiClient.addMediaPath(t.Name,i,null,e).then(()=>{c=!0,f(r)},()=>{m(n.translate("ErrorAddingMediaPathToVirtualFolder"))})}function A(r,i){const t=a.library;window.ApiClient.updateMediaPath(t.Name,{Path:i}).then(()=>{c=!0,f(r)},()=>{m(n.translate("ErrorAddingMediaPathToVirtualFolder"))})}function O(r,i){const t=r,e=a.library;w({title:n.translate("HeaderRemoveMediaLocation"),text:n.translate("MessageConfirmRemoveMediaLocation"),confirmText:n.translate("Delete"),primary:"delete"}).then(()=>{const o=a.refresh;window.ApiClient.removeMediaPath(e.Name,i,o).then(()=>{c=!0,f(d.parentWithClass(t,"dlg-libraryeditor"))},()=>{m(n.translate("ErrorDefault"))})}).catch(()=>{})}function F(r){const i=d.parentWithClass(r.target,"listItem");if(i){const t=Number.parseInt(i.getAttribute("data-index")||"",10),e=a.library.LibraryOptions?.PathInfos||[],l=(Number.isInteger(t)&&t>=0?e[t]:void 0)?.Path||(Number.isInteger(t)&&t>=0?a.library.Locations[t]:void 0),y=d.parentWithClass(r.target,"btnRemovePath");if(y&&l){O(y,l);return}l&&I(d.parentWithClass(i,"dlg-libraryeditor"),l)}}function N(r,i){let t="";return t+=`<div class="listItem listItem-border lnkPath" data-index="${i}">`,t+=`<div class="${r.NetworkPath?"listItemBody two-line":"listItemBody"}">`,t+='<h3 class="listItemBodyText">',t+=p(r.Path),t+="</h3>",r.NetworkPath&&(t+=`<div class="listItemBodyText secondary">${p(r.NetworkPath)}</div>`),t+="</div>",t+=`<button type="button" is="paper-icon-button-light" class="listItemButton btnRemovePath" data-index="${i}"><span class="material-icons remove_circle" aria-hidden="true"></span></button>`,t+="</div>",t}function f(r){window.ApiClient.getVirtualFolders().then(i=>{const t=(i||[]).filter(e=>e.Name===a.library.Name)[0];t&&(a.library=t,L(r,a))},()=>{m(n.translate("ErrorDefault"))})}function L(r,i){let t=i.library.LibraryOptions?.PathInfos||[];t.length||(t=i.library.Locations.map(e=>({Path:e}))),i.library.CollectionType==="boxsets"?r.querySelector(".folders").classList.add("hide"):r.querySelector(".folders").classList.remove("hide"),r.querySelector(".folderList").innerHTML=t.map(N).join("")}function S(){I(d.parentWithClass(this,"dlg-libraryeditor"))}function I(r,i){C(async()=>{const{default:t}=await import("./index-6F8mVyyU.js").then(e=>e.fk);return{default:t}},__vite__mapDeps([0,1]),import.meta.url).then(({default:t})=>{const e=new t;e.show({pathReadOnly:i!=null,path:i,callback:function(o){o&&(i?A(r,o):E(r,o)),e.close()}})}).catch(()=>{m(n.translate("ErrorDefault"))})}function M(r,i){L(r,i),r.querySelector(".btnAddFolder").addEventListener("click",S),r.querySelector(".folderList").addEventListener("click",F),r.querySelector(".btnSubmit").addEventListener("click",D),v.embed(r.querySelector(".libraryOptions"),i.library.CollectionType,i.library.LibraryOptions??null)}function x(){g.resolveWith(null,[c])}class R{constructor(i){const t=P.Deferred();a=i,g=t,c=!1;const e=s.createDialog({size:"small",modal:!1,removeOnClose:!0,scrollY:!1});return e.classList.add("dlg-libraryeditor"),e.classList.add("ui-body-a"),e.classList.add("background-theme-a"),e.classList.add("formDialog"),e.innerHTML=n.translateHtml(k),e.querySelector(".formDialogHeaderTitle").innerText=i.library.Name,M(e,i),e.addEventListener("close",x),s.open(e),e.querySelector(".btnCancel").addEventListener("click",()=>{s.close(e)}),f(e),t.promise()}}export{R as MediaLibraryEditor,R as default};
