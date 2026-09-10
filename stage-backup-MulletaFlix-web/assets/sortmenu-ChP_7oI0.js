const __vite__mapDeps=(i,m=__vite__mapDeps,d=(m.f||(m.f=["./index-6F8mVyyU.js","./index-CQ6hRxbv.css"])))=>i.map(i=>d[i]);
import{l as i,c,f as m,e as v,_ as g,cd as S}from"./index-6F8mVyyU.js";import"./emby-select-BS-dUTXm.js";import"./actionSheet-bHfWrycr.js";const p=`<div class="formDialogContent smoothScrollY">
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
`;function b(t){return t.preventDefault(),!1}function h(t,o){const n=t.querySelector("form");n&&n.addEventListener("submit",b);const e=t.querySelector(".selectSortOrder");e&&(e.value=o.sortOrder);const s=t.querySelector(".selectSortBy");s&&(s.value=o.sortBy)}function y(t,o,n){g(()=>import("./index-6F8mVyyU.js").then(e=>e.fj),__vite__mapDeps([0,1]),import.meta.url).then(e=>{const s=n?"on":"off";e.centerFocus[s](t,o)}).catch(e=>console.error("[SortMenu] failed to center focus",e))}function B(t,o){const n=t.querySelector(".selectSortBy");n&&(n.innerHTML=o.map(e=>'<option value="'+v(e.value)+'">'+v(e.name)+"</option>").join(""))}function D(t,o){const n=t.querySelector(".selectSortOrder"),e=t.querySelector(".selectSortBy");n&&S(o+"-sortorder",n.value),e&&S(o+"-sortby",e.value)}class L{show(o){return new Promise((n,e)=>{const s={removeOnClose:!0,scrollY:!1};s.size=i.tv?"fullscreen":"small";const r=c.createDialog(s);r.classList.add("formDialog");let a="";a+='<div class="formDialogHeader">',a+=`<button is="paper-icon-button-light" class="btnCancel hide-mouse-idle-tv" tabindex="-1" title="${m.translate("ButtonBack")}"><span class="material-icons arrow_back" aria-hidden="true"></span></button>`,a+='<h3 class="formDialogHeaderTitle">${Sort}</h3>',a+="</div>",a+=p,r.innerHTML=m.translateHtml(a,"core"),B(r,o.sortOptions),h(r,o.settings);const d=r.querySelector(".btnCancel");if(d&&d.addEventListener("click",()=>{c.close(r)}),i.tv){const l=r.querySelector(".formDialogContent");l&&y(l,!1,!0)}let u=!1;const f=r.querySelector("form");f&&f.addEventListener("change",()=>{u=!0},!0),c.open(r).then(()=>{if(i.tv){const l=r.querySelector(".formDialogContent");l&&y(l,!1,!1)}if(u){D(r,o.settingsKey),n();return}e()}).catch(l=>{console.error("[SortMenu] failed to open dialog",l),e(l)})})}}export{L as default};
