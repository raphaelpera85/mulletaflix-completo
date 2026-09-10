const __vite__mapDeps=(i,m=__vite__mapDeps,d=(m.f||(m.f=["./index-6F8mVyyU.js","./index-CQ6hRxbv.css"])))=>i.map(i=>d[i]);
import{c as u,f as m,d as s,e as y,_ as C,j as h,a2 as c,W as w}from"./index-6F8mVyyU.js";import{l as p}from"./emby-toggle-VTx5lffU.js";import"./emby-select-BS-dUTXm.js";import"./emby-textarea-BTRMqmjp.js";import"./actionSheet-bHfWrycr.js";const T=`<div class="formDialogHeader">
    <button type="button" is="paper-icon-button-light" class="btnCancel autoSize" tabindex="-1" title="\${ButtonBack}"><span class="material-icons arrow_back" aria-hidden="true"></span></button>
    <h3 class="formDialogHeaderTitle">\${ButtonAddMediaLibrary}</h3>
</div>

<div class="formDialogContent scrollY" style="padding-top:2em;">
    <div class="dialogContentInner dialog-content-centered">
        <form class="addLibraryForm" style="max-width:100%;">

            <div id="fldCollectionType" class="selectContainer">
                <select is="emby-select" id="selectCollectionType" data-mini="true" required="required" label="\${LabelContentType}"></select>
                <div class="collectionTypeFieldDescription fieldDescription">
                </div>
            </div>

            <div class="inputContainer">
                <input is="emby-input" type="text" id="txtValue" required="required" label="\${LabelDisplayName}" />
            </div>

            <div class="folders">
                <div style="display: flex; align-items: center;">
                    <h1 style="margin: .5em 0;">\${Folders}</h1>
                    <button is="emby-button" type="button" class="fab btnAddFolder submit" title="\${Add}">
                        <span class="material-icons add" aria-hidden="true"></span>
                    </button>
                </div>
                <div class="paperList folderList hide" style="margin-bottom:2em;"></div>
            </div>

            <div class="libraryOptions"></div>

            <div class="formDialogFooter">
                <button is="emby-button" type="submit" class="raised btnSubmit button-submit block formDialogFooterItem">
                    <span>\${ButtonOk}</span>
                </button>
            </div>
        </form>
    </div>
</div>
`;let a=[],v,L,b=!1,l=!1;function q(r){if(r.preventDefault(),l)return;if(a.length==0){h({text:m.translate("PleaseAddAtLeastOneFolder"),type:"error"}).catch(o=>console.error("[MediaLibraryCreator] failed to show validation alert",o));return}l=!0,c.show();const e=s.parentWithClass(this,"dlg-librarycreator"),t=e.querySelector("#txtValue").value.trim();let i=e.querySelector("#selectCollectionType").value;if(t.length===0){h({text:m.translate("LibraryNameInvalid"),type:"error"}).catch(o=>console.error("[MediaLibraryCreator] failed to show validation alert",o)),l=!1,c.hide();return}i=="mixed"&&(i=null);const n={...p.getLibraryOptions(e.querySelector(".libraryOptions")),PathInfos:a};window.ApiClient.addVirtualFolder(t,i||void 0,L.refresh,n).then(()=>{b=!0,l=!1,c.hide(),u.close(e)},()=>{w(m.translate("ErrorAddingMediaPathToVirtualFolder")),l=!1,c.hide()})}function P(r){return r.map(e=>`<option value="${y(e.value)}">${y(e.name)}</option>`).join("")}function x(r,e){const t=r.querySelector("#selectCollectionType");t.innerHTML=P(e),t.value="",t.addEventListener("change",function(){const i=this.value,n=s.parentWithClass(this,"dialog");if(p.setContentType(n.querySelector(".libraryOptions"),i),i?n.querySelector(".libraryOptions").classList.remove("hide"):n.querySelector(".libraryOptions").classList.add("hide"),i!="mixed"){const d=this.selectedIndex;if(d!=-1){const g=this.options[d].innerHTML.replace(/\*/g,"").replace(/&amp;/g,"&");n.querySelector("#txtValue").value=g}}const o=e.find(d=>d.value===i);n.querySelector(".collectionTypeFieldDescription").innerHTML=o?.message||""}),r.querySelector(".btnAddFolder").addEventListener("click",S),r.querySelector(".addLibraryForm").addEventListener("submit",q),r.querySelector(".folderList").addEventListener("click",D)}function S(){const r=s.parentWithClass(this,"dlg-librarycreator");C(async()=>{const{default:e}=await import("./index-6F8mVyyU.js").then(t=>t.fk);return{default:e}},__vite__mapDeps([0,1]),import.meta.url).then(({default:e})=>{const t=new e;t.show({callback:function(i){i&&O(r,i),t.close()}})}).catch(e=>{console.error("[MediaLibraryCreator] failed to load directory browser",e)})}function k(r,e){let t="";return t+='<div class="listItem listItem-border lnkPath">',t+=`<div class="${r.NetworkPath?"listItemBody two-line":"listItemBody"}">`,t+=`<div class="listItemBodyText" dir="ltr">${y(r.Path)}</div>`,r.NetworkPath&&(t+=`<div class="listItemBodyText secondary" dir="ltr">${y(r.NetworkPath)}</div>`),t+="</div>",t+=`<button type="button" is="paper-icon-button-light" class="listItemButton btnRemovePath" data-index="${e}"><span class="material-icons remove_circle" aria-hidden="true"></span></button>`,t+="</div>",t}function f(r){const e=a.map(k).join(""),t=r.querySelector(".folderList");t.innerHTML=e,e?t.classList.remove("hide"):t.classList.add("hide")}function O(r,e){a.some(i=>i.Path===e)||(a.push({Path:e}),f(r))}function D(r){const e=s.parentWithClass(r.target,"btnRemovePath"),t=Number.parseInt(e.getAttribute("data-index")||"",10);if(!Number.isInteger(t)||t<0||t>=a.length)return;const n=a[t].Path.toLowerCase();a=a.filter(o=>o.Path.toLowerCase()!=n),f(s.parentWithClass(e,"dlg-librarycreator"))}function I(){v(b)}function M(r){p.embed(r.querySelector(".libraryOptions"),null,null).then(()=>{r.querySelector("#selectCollectionType").dispatchEvent(new Event("change"))}).catch(e=>{console.error("[MediaLibraryCreator] failed to initialize library options",e)})}class B{constructor(e){return new Promise(t=>{L=e,v=t,b=!1;const i=u.createDialog({size:"small",modal:!1,removeOnClose:!0,scrollY:!1});i.classList.add("ui-body-a"),i.classList.add("background-theme-a"),i.classList.add("dlg-librarycreator"),i.classList.add("formDialog"),i.innerHTML=m.translateHtml(T),x(i,e.collectionTypeOptions),i.addEventListener("close",I),u.open(i).catch(n=>{console.error("[MediaLibraryCreator] failed to open dialog",n)}),i.querySelector(".btnCancel").addEventListener("click",()=>{u.close(i)}),a=[],f(i),M(i)})}}export{B as MediaLibraryCreator,B as default};
