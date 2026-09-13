const __vite__mapDeps=(i,m=__vite__mapDeps,d=(m.f||(m.f=["./index-CFwqrzSZ.js","./vendor-react-query-BiAmjtRH.js","./vendor-react-CeUY3tRn.js","./vendor-jellyfin-Bee54tkY.js","./vendor-axios-BCn5QfZZ.js","./vendor-lodash-CYyrkkBC.js","./vendor-date-fns-CwLI6ukS.js","./vendor-dompurify-Baz99PXY.js","./index-BAjafMOe.css"])))=>i.map(i=>d[i]);
import{_ as y}from"./vendor-react-query-BiAmjtRH.js";import{l as a,G as c,s as f,B as v,aM as S}from"./index-CFwqrzSZ.js";import"./emby-select-BQevfB2a.js";import"./vendor-react-CeUY3tRn.js";import"./vendor-jellyfin-Bee54tkY.js";import"./vendor-axios-BCn5QfZZ.js";import"./vendor-lodash-CYyrkkBC.js";import"./vendor-date-fns-CwLI6ukS.js";import"./vendor-dompurify-Baz99PXY.js";import"./actionSheet-BM-G2WQD.js";/* empty css                 */const g=`<div class="formDialogContent smoothScrollY">
    <div class="dialogContentInner dialog-content-centered">
        <form style="margin:auto;">

            <div class="verticalSection verticalSection-extrabottompadding" style="margin-top:2em;">

                <div class="selectContainer">
                    <select is="emby-select" class="selectSortBy" label="\${LabelSortBy}">

                    </select>
                </div>

                <div class="selectContainer">
                    <select is="emby-select" class="selectSortOrder" label="\${LabelSortOrder}">
                        <option value="Ascending">\${Ascending}</option>
                        <option value="Descending">\${Descending}</option>
                    </select>
                </div>
            </div>
        </form>
    </div>
</div>
`;function b(t){return t.preventDefault(),!1}function h(t,o){const r=t.querySelector("form");r&&r.addEventListener("submit",b);const e=t.querySelector(".selectSortOrder");e&&(e.value=o.sortOrder);const s=t.querySelector(".selectSortBy");s&&(s.value=o.sortBy)}function p(t,o,r){y(()=>import("./index-CFwqrzSZ.js").then(e=>e.bu),__vite__mapDeps([0,1,2,3,4,5,6,7,8]),import.meta.url).then(e=>{const s=r?"on":"off";e.centerFocus[s](t,o)}).catch(e=>console.error("[SortMenu] failed to center focus",e))}function B(t,o){const r=t.querySelector(".selectSortBy");r&&(r.innerHTML=o.map(e=>'<option value="'+v(e.value)+'">'+v(e.name)+"</option>").join(""))}function D(t,o){const r=t.querySelector(".selectSortOrder"),e=t.querySelector(".selectSortBy");r&&S(o+"-sortorder",r.value),e&&S(o+"-sortby",e.value)}class w{show(o){return new Promise((r,e)=>{const s={removeOnClose:!0,scrollY:!1};s.size=a.tv?"fullscreen":"small";const n=c.createDialog(s);n.classList.add("formDialog");let i="";i+='<div class="formDialogHeader">',i+=`<button is="paper-icon-button-light" class="btnCancel hide-mouse-idle-tv" tabindex="-1" title="${f.translate("ButtonBack")}"><span class="material-icons arrow_back" aria-hidden="true"></span></button>`,i+='<h3 class="formDialogHeaderTitle">${Sort}</h3>',i+="</div>",i+=g,n.innerHTML=f.translateHtml(i,"core"),B(n,o.sortOptions),h(n,o.settings);const d=n.querySelector(".btnCancel");if(d&&d.addEventListener("click",()=>{c.close(n)}),a.tv){const l=n.querySelector(".formDialogContent");l&&p(l,!1,!0)}let u=!1;const m=n.querySelector("form");m&&m.addEventListener("change",()=>{u=!0},!0),c.open(n).then(()=>{if(a.tv){const l=n.querySelector(".formDialogContent");l&&p(l,!1,!1)}if(u){D(n,o.settingsKey),r();return}e()}).catch(l=>{console.error("[SortMenu] failed to open dialog",l),e(l)})})}}export{w as default};
