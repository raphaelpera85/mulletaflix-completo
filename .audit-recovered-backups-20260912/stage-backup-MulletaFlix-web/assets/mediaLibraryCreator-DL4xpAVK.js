const __vite__mapDeps=(i,m=__vite__mapDeps,d=(m.f||(m.f=["./dashboard-BemBAZF-.js","./index-CFwqrzSZ.js","./vendor-react-query-BiAmjtRH.js","./vendor-react-CeUY3tRn.js","./vendor-jellyfin-Bee54tkY.js","./vendor-axios-BCn5QfZZ.js","./vendor-lodash-CYyrkkBC.js","./vendor-date-fns-CwLI6ukS.js","./vendor-dompurify-Baz99PXY.js","./index-BAjafMOe.css","./itemidentifier-BazfFvEN.js","./emby-checkbox-CDwDlGOj.js","./emby-checkbox-O4Gt6THi.css","./card-Cnu_ZzkL.css","./dashboard-CAGhfN5N.css","./listview-CPZd3k-k.css"])))=>i.map(i=>d[i]);
import{_ as g}from"./vendor-react-query-BiAmjtRH.js";import{G as c,s as u,h as l,B as m,o as f,D as C,n as w}from"./index-CFwqrzSZ.js";import{l as p}from"./emby-toggle-BUHpYdbV.js";import"./emby-select-BQevfB2a.js";/* empty css                 */import"./vendor-react-CeUY3tRn.js";import"./vendor-jellyfin-Bee54tkY.js";import"./vendor-axios-BCn5QfZZ.js";import"./vendor-lodash-CYyrkkBC.js";import"./vendor-date-fns-CwLI6ukS.js";import"./vendor-dompurify-Baz99PXY.js";import"./emby-checkbox-CDwDlGOj.js";import"./emby-textarea-BsVRAtiX.js";import"./actionSheet-BM-G2WQD.js";const T=`<div class="formDialogHeader">
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
`;let n=[],h,v,y=!1,d=!1;function q(r){if(r.preventDefault(),d)return;if(n.length==0){f({text:u.translate("PleaseAddAtLeastOneFolder"),type:"error"}).catch(a=>console.error("[MediaLibraryCreator] failed to show validation alert",a));return}d=!0;const e=l.parentWithClass(this,"dlg-librarycreator"),t=e.querySelector("#txtValue").value.trim();let i=e.querySelector("#selectCollectionType").value;if(t.length===0){f({text:u.translate("LibraryNameInvalid"),type:"error"}).catch(a=>console.error("[MediaLibraryCreator] failed to show validation alert",a)),d=!1;return}i=="mixed"&&(i=null);const o={...p.getLibraryOptions(e.querySelector(".libraryOptions")),PathInfos:n};C.withLoading(()=>window.ApiClient.addVirtualFolder(t,i||void 0,v.refresh,o)).then(()=>{y=!0,c.close(e)}).catch(a=>{console.error("[MediaLibraryCreator] failed to add virtual folder",a),w(u.translate("ErrorAddingMediaPathToVirtualFolder"))}).finally(()=>{d=!1})}function P(r){return r.map(e=>`<option value="${m(e.value)}">${m(e.name)}</option>`).join("")}function x(r,e){const t=r.querySelector("#selectCollectionType");t.innerHTML=P(e),t.value="",t.addEventListener("change",function(){const i=this.value,o=l.parentWithClass(this,"dialog");if(p.setContentType(o.querySelector(".libraryOptions"),i),i?o.querySelector(".libraryOptions").classList.remove("hide"):o.querySelector(".libraryOptions").classList.add("hide"),i!="mixed"){const s=this.selectedIndex;if(s!=-1){const L=this.options[s].innerHTML.replace(/\*/g,"").replace(/&amp;/g,"&");o.querySelector("#txtValue").value=L}}const a=e.find(s=>s.value===i);o.querySelector(".collectionTypeFieldDescription").innerHTML=a?.message||""}),r.querySelector(".btnAddFolder").addEventListener("click",S),r.querySelector(".addLibraryForm").addEventListener("submit",q),r.querySelector(".folderList").addEventListener("click",O)}function S(){const r=l.parentWithClass(this,"dlg-librarycreator");g(async()=>{const{default:e}=await import("./dashboard-BemBAZF-.js").then(t=>t.d);return{default:e}},__vite__mapDeps([0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15]),import.meta.url).then(({default:e})=>{const t=new e;t.show({callback:function(i){i&&D(r,i),t.close()}})}).catch(e=>{console.error("[MediaLibraryCreator] failed to load directory browser",e)})}function k(r,e){let t="";return t+='<div class="listItem listItem-border lnkPath">',t+=`<div class="${r.NetworkPath?"listItemBody two-line":"listItemBody"}">`,t+=`<div class="listItemBodyText" dir="ltr">${m(r.Path)}</div>`,r.NetworkPath&&(t+=`<div class="listItemBodyText secondary" dir="ltr">${m(r.NetworkPath)}</div>`),t+="</div>",t+=`<button type="button" is="paper-icon-button-light" class="listItemButton btnRemovePath" data-index="${e}"><span class="material-icons remove_circle" aria-hidden="true"></span></button>`,t+="</div>",t}function b(r){const e=n.map(k).join(""),t=r.querySelector(".folderList");t.innerHTML=e,e?t.classList.remove("hide"):t.classList.add("hide")}function D(r,e){n.some(i=>i.Path===e)||(n.push({Path:e}),b(r))}function O(r){const e=l.parentWithClass(r.target,"btnRemovePath"),t=Number.parseInt(e.getAttribute("data-index")||"",10);if(!Number.isInteger(t)||t<0||t>=n.length)return;const o=n[t].Path.toLowerCase();n=n.filter(a=>a.Path.toLowerCase()!=o),b(l.parentWithClass(e,"dlg-librarycreator"))}function I(){h(y)}function M(r){p.embed(r.querySelector(".libraryOptions"),null,null).then(()=>{r.querySelector("#selectCollectionType").dispatchEvent(new Event("change"))}).catch(e=>{console.error("[MediaLibraryCreator] failed to initialize library options",e)})}class G{constructor(e){return new Promise(t=>{v=e,h=t,y=!1;const i=c.createDialog({size:"small",modal:!1,removeOnClose:!0,scrollY:!1});i.classList.add("ui-body-a"),i.classList.add("background-theme-a"),i.classList.add("dlg-librarycreator"),i.classList.add("formDialog"),i.innerHTML=u.translateHtml(T),x(i,e.collectionTypeOptions),i.addEventListener("close",I),c.open(i).catch(o=>{console.error("[MediaLibraryCreator] failed to open dialog",o)}),i.querySelector(".btnCancel").addEventListener("click",()=>{c.close(i)}),n=[],b(i),M(i)})}}export{G as MediaLibraryCreator,G as default};
