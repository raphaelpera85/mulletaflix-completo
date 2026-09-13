const __vite__mapDeps=(i,m=__vite__mapDeps,d=(m.f||(m.f=["./index-CFwqrzSZ.js","./vendor-react-query-BiAmjtRH.js","./vendor-react-CeUY3tRn.js","./vendor-jellyfin-Bee54tkY.js","./vendor-axios-BCn5QfZZ.js","./vendor-lodash-CYyrkkBC.js","./vendor-date-fns-CwLI6ukS.js","./vendor-dompurify-Baz99PXY.js","./index-BAjafMOe.css"])))=>i.map(i=>d[i]);
import{_ as p}from"./vendor-react-query-BiAmjtRH.js";import{l,G as r,s as d,M as m}from"./index-CFwqrzSZ.js";import"./emby-checkbox-CDwDlGOj.js";import"./emby-select-BQevfB2a.js";import"./vendor-react-CeUY3tRn.js";import"./vendor-jellyfin-Bee54tkY.js";import"./vendor-axios-BCn5QfZZ.js";import"./vendor-lodash-CYyrkkBC.js";import"./vendor-date-fns-CwLI6ukS.js";import"./vendor-dompurify-Baz99PXY.js";import"./actionSheet-BM-G2WQD.js";/* empty css                 */const v=`<div class="formDialogContent smoothScrollY" style="padding-top:2em;">
    <div class="dialogContentInner dialog-content-centered">
        <form style="margin:auto;">

            <div class="verticalSection">

                <div class="selectContainer viewSetting hide" data-settingname="imageType">
                    <select is="emby-select" label="\${LabelImageType}" class="selectImageType">
                        <option value="primary">\${Primary}</option>
                        <option value="banner">\${Banner}</option>
                        <option value="disc">\${Disc}</option>
                        <option value="logo">\${Logo}</option>
                        <option value="thumb">\${Thumb}</option>
                        <option value="list">\${List}</option>
                    </select>
                </div>

                <div class="checkboxContainer viewSetting viewSetting-checkboxContainer hide chkTitleContainer" data-settingname="showTitle">
                    <label>
                        <input is="emby-checkbox" type="checkbox" class="chkShowTitle" />
                        <span>\${ShowTitle}</span>
                    </label>
                </div>

                <div class="checkboxContainer viewSetting viewSetting-checkboxContainer hide chkYearContainer" data-settingname="showYear">
                    <label>
                        <input is="emby-checkbox" type="checkbox" class="chkShowYear" />
                        <span>\${ShowYear}</span>
                    </label>
                </div>

                <div class="checkboxContainer viewSetting viewSetting-checkboxContainer hide" data-settingname="groupBySeries">
                    <label>
                        <input is="emby-checkbox" type="checkbox" class="chkGroupBySeries" />
                        <span>\${GroupBySeries}</span>
                    </label>
                </div>
            </div>
        </form>
    </div>
</div>
`;function f(n){n.preventDefault()}function b(n,i){n.querySelector("form")?.addEventListener("submit",f);const o=n.querySelectorAll(".viewSetting-checkboxContainer");for(const t of o){const e=t.querySelector("input"),a=t.getAttribute("data-settingname");e.checked=i[a||""]||!1}n.querySelector(".selectImageType").value=i.imageType||"primary"}function S(n,i,o){const t=n.querySelectorAll(".viewSetting-checkboxContainer");for(const e of t){const a=e.getAttribute("data-settingname");m(o+"-"+a,e.querySelector("input").checked)}m(o+"-imageType",n.querySelector(".selectImageType").value)}function u(n,i,o){p(()=>import("./index-CFwqrzSZ.js").then(t=>t.bu),__vite__mapDeps([0,1,2,3,4,5,6,7,8]),import.meta.url).then(t=>{const e=o?"on":"off";n&&t.centerFocus[e](n,i)}).catch(t=>{console.error("[ViewSettings] failed to load scroll helper",t)})}function g(n,i,o){const t=n.querySelector(i);t&&(o&&!t.classList.contains("hiddenFromViewSettings")?t.classList.remove("hide"):t.classList.add("hide"))}class _{show(i){return new Promise(function(o){const t={removeOnClose:!0,scrollY:!1,size:""};l.tv?t.size="fullscreen":t.size="small";const e=r.createDialog(t);e.classList.add("formDialog");let a="";a+='<div class="formDialogHeader">',a+=`<button is="paper-icon-button-light" class="btnCancel hide-mouse-idle-tv" tabindex="-1" title="${d.translate("ButtonBack")}"><span class="material-icons arrow_back" aria-hidden="true"></span></button>`,a+='<h3 class="formDialogHeaderTitle">${Settings}</h3>',a+="</div>",a+=v,e.innerHTML=d.translateHtml(a,"core");const h=e.querySelectorAll(".viewSetting");for(const s of h)i.visibleSettings.indexOf(s.getAttribute("data-settingname")||"")===-1?(s.classList.add("hide"),s.classList.add("hiddenFromViewSettings")):(s.classList.remove("hide"),s.classList.remove("hiddenFromViewSettings"));b(e,i.settings),e.querySelector(".selectImageType")?.addEventListener("change",function(){g(e,".chkTitleContainer",this.value!=="list"&&this.value!=="banner"),g(e,".chkYearContainer",this.value!=="list"&&this.value!=="banner")}),e.querySelector(".btnCancel")?.addEventListener("click",function(){r.close(e)}),l.tv&&u(e.querySelector(".formDialogContent"),!1,!0);let c;e.querySelector(".selectImageType")?.dispatchEvent(new CustomEvent("change",{})),e.querySelector("form")?.addEventListener("change",function(){c=!0},!0),r.open(e).then(function(){return l.tv&&u(e.querySelector(".formDialogContent"),!1,!1),c&&S(e,i.settings,i.settingsKey),o()}).catch(s=>{console.error("[ViewSettings] failed to open dialog",s),o()})})}}export{_ as default};
