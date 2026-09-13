import{G as s,s as m,h as u}from"./index-CFwqrzSZ.js";import"./emby-checkbox-CDwDlGOj.js";import"./emby-select-BQevfB2a.js";import"./vendor-react-query-BiAmjtRH.js";import"./vendor-react-CeUY3tRn.js";import"./vendor-jellyfin-Bee54tkY.js";import"./vendor-axios-BCn5QfZZ.js";import"./vendor-lodash-CYyrkkBC.js";import"./vendor-date-fns-CwLI6ukS.js";import"./vendor-dompurify-Baz99PXY.js";import"./actionSheet-BM-G2WQD.js";/* empty css                 */const b=`<div class="formDialogHeader">
    <button type="button" is="paper-icon-button-light" class="btnCancel autoSize" tabindex="-1" title="\${ButtonBack}">
        <span class="material-icons arrow_back" aria-hidden="true"></span>
    </button>
    <h3 class="formDialogHeaderTitle">
        \${HeaderImageOptions}
    </h3>
</div>

<div class="formDialogContent scrollY">
    <div class="dialogContentInner dialog-content-centered">
        <form style="margin:1.5em auto 0;">

            <div class="verticalSection">
                <h3 class="checkboxListLabel">\${HeaderFetchImages}</h3>
                <div class="imageSelections checkboxList">

                    <label>
                        <input type="checkbox" is="emby-checkbox" class="imageType" data-imagetype="Primary" />
                        <span>\${Primary}</span>
                    </label>

                    <label>
                        <input type="checkbox" is="emby-checkbox" class="imageType" data-imagetype="Art" />
                        <span>\${Art}</span>
                    </label>
                    <label>
                        <input type="checkbox" is="emby-checkbox" class="imageType" data-imagetype="BoxRear" />
                        <span>\${Back}</span>
                    </label>
                    <label>
                        <input type="checkbox" is="emby-checkbox" class="imageType" data-imagetype="Banner" />
                        <span>\${Banner}</span>
                    </label>
                    <label>
                        <input type="checkbox" is="emby-checkbox" class="imageType" data-imagetype="Box" />
                        <span>\${Box}</span>
                    </label>

                    <label>
                        <input type="checkbox" is="emby-checkbox" class="imageType" data-imagetype="Disc" />
                        <span>\${Disc}</span>
                    </label>
                    <label>
                        <input type="checkbox" is="emby-checkbox" class="imageType" data-imagetype="Logo" />
                        <span>\${Logo}</span>
                    </label>
                    <label>
                        <input type="checkbox" is="emby-checkbox" class="imageType" data-imagetype="Menu" />
                        <span>\${Menu}</span>
                    </label>
                    <label>
                        <input type="checkbox" is="emby-checkbox" class="imageType" data-imagetype="Thumb" />
                        <span>\${Thumb}</span>
                    </label>
                </div>
            </div>

            <div class="backdropFields">
                <div class="inputContainer">
                    <input is="emby-input" type="number" id="txtMaxBackdrops" pattern="[0-9]*" required="required" min="0" label="\${LabelMaxBackdropsPerItem}" />
                </div>
                <div class="inputContainer">
                    <input is="emby-input" type="number" id="txtMinBackdropDownloadWidth" pattern="[0-9]*" required="required" min="0" label="\${LabelMinBackdropDownloadWidth}" />
                </div>
            </div>
        </form>
    </div>
</div>
`;function y(e,t){return{Type:t,MinWidth:0,Limit:t==="Primary"?1:0}}function p(e,t){return e.filter(a=>a.Type==t)[0]}function d(e,t,a,i){return p(e.ImageOptions||[],a)||p(t.DefaultImageOptions||[],a)||y(i,a)}function g(e,t){if(t)e.classList.remove("hide"),e.querySelector("input").setAttribute("required","required");else{e.classList.add("hide");const a=e.querySelector("input");a.setAttribute("required",""),a.removeAttribute("required")}}function h(e,t,a,i){const n=i.SupportedImageTypes||[];g(e.querySelector(".backdropFields"),n.includes("Backdrop")),Array.prototype.forEach.call(e.querySelectorAll(".imageType"),r=>{const l=r.getAttribute("data-imagetype")||"",c=u.parentWithTag(r,"LABEL");n.includes(l)?c.classList.remove("hide"):c.classList.add("hide"),d(a,i,l,t).Limit?r.checked=!0:r.checked=!1});const o=d(a,i,"Backdrop",t);e.querySelector("#txtMaxBackdrops").value=String(o.Limit),e.querySelector("#txtMinBackdropDownloadWidth").value=String(o.MinWidth)}function k(e,t){t.ImageOptions=Array.prototype.map.call(e.querySelectorAll(".imageType:not(.hide)"),a=>({Type:a.getAttribute("data-imagetype")||"",Limit:a.checked?1:0,MinWidth:0})),t.ImageOptions.push({Type:"Backdrop",Limit:parseInt(e.querySelector("#txtMaxBackdrops").value,10),MinWidth:parseInt(e.querySelector("#txtMinBackdropDownloadWidth").value,10)})}class A{show(t,a,i){const n=s.createDialog({size:"small",removeOnClose:!0,scrollY:!1});n.classList.add("formDialog"),n.innerHTML=m.translateHtml(b),n.addEventListener("close",function(){k(n,a)}),h(n,t,a,i),s.open(n).then(()=>{}).catch(()=>{}),n.querySelector(".btnCancel").addEventListener("click",function(){s.close(n)})}}export{A as default};
