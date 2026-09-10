const __vite__mapDeps=(i,m=__vite__mapDeps,d=(m.f||(m.f=["./index-6F8mVyyU.js","./index-CQ6hRxbv.css"])))=>i.map(i=>d[i]);
import{l,c,f as d,_ as v,s as u}from"./index-6F8mVyyU.js";import"./emby-select-BS-dUTXm.js";import"./actionSheet-bHfWrycr.js";const p=`<div class="formDialogContent smoothScrollY" style="padding-top:2em;">
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
`;function f(n){n.preventDefault()}function b(n,i){n.querySelector("form")?.addEventListener("submit",f);const o=n.querySelectorAll(".viewSetting-checkboxContainer");for(const t of o){const e=t.querySelector("input"),a=t.getAttribute("data-settingname");e.checked=i[a||""]||!1}n.querySelector(".selectImageType").value=i.imageType||"primary"}function S(n,i,o){const t=n.querySelectorAll(".viewSetting-checkboxContainer");for(const e of t){const a=e.getAttribute("data-settingname");u(o+"-"+a,e.querySelector("input").checked)}u(o+"-imageType",n.querySelector(".selectImageType").value)}function m(n,i,o){v(()=>import("./index-6F8mVyyU.js").then(t=>t.fj),__vite__mapDeps([0,1]),import.meta.url).then(t=>{const e=o?"on":"off";n&&t.centerFocus[e](n,i)}).catch(t=>{console.error("[ViewSettings] failed to load scroll helper",t)})}function g(n,i,o){const t=n.querySelector(i);t&&(o&&!t.classList.contains("hiddenFromViewSettings")?t.classList.remove("hide"):t.classList.add("hide"))}class C{show(i){return new Promise(function(o){const t={removeOnClose:!0,scrollY:!1,size:""};l.tv?t.size="fullscreen":t.size="small";const e=c.createDialog(t);e.classList.add("formDialog");let a="";a+='<div class="formDialogHeader">',a+=`<button is="paper-icon-button-light" class="btnCancel hide-mouse-idle-tv" tabindex="-1" title="${d.translate("ButtonBack")}"><span class="material-icons arrow_back" aria-hidden="true"></span></button>`,a+='<h3 class="formDialogHeaderTitle">${Settings}</h3>',a+="</div>",a+=p,e.innerHTML=d.translateHtml(a,"core");const h=e.querySelectorAll(".viewSetting");for(const s of h)i.visibleSettings.indexOf(s.getAttribute("data-settingname")||"")===-1?(s.classList.add("hide"),s.classList.add("hiddenFromViewSettings")):(s.classList.remove("hide"),s.classList.remove("hiddenFromViewSettings"));b(e,i.settings),e.querySelector(".selectImageType")?.addEventListener("change",function(){g(e,".chkTitleContainer",this.value!=="list"&&this.value!=="banner"),g(e,".chkYearContainer",this.value!=="list"&&this.value!=="banner")}),e.querySelector(".btnCancel")?.addEventListener("click",function(){c.close(e)}),l.tv&&m(e.querySelector(".formDialogContent"),!1,!0);let r;e.querySelector(".selectImageType")?.dispatchEvent(new CustomEvent("change",{})),e.querySelector("form")?.addEventListener("change",function(){r=!0},!0),c.open(e).then(function(){return l.tv&&m(e.querySelector(".formDialogContent"),!1,!1),r&&S(e,i.settings,i.settingsKey),o()}).catch(s=>{console.error("[ViewSettings] failed to open dialog",s),o()})})}}export{C as default};
