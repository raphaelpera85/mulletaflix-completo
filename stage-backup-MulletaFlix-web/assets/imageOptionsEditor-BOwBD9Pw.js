import{c as r,f as u,d as m}from"./index-6F8mVyyU.js";import"./emby-select-BS-dUTXm.js";import"./actionSheet-bHfWrycr.js";const b=`<div class="formDialogHeader">
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
`;function y(e,n){return{Type:n,MinWidth:0,Limit:n==="Primary"?1:0}}function p(e,n){return e.filter(a=>a.Type==n)[0]}function d(e,n,a,i){return p(e.ImageOptions||[],a)||p(n.DefaultImageOptions||[],a)||y(i,a)}function g(e,n){if(n)e.classList.remove("hide"),e.querySelector("input").setAttribute("required","required");else{e.classList.add("hide");const a=e.querySelector("input");a.setAttribute("required",""),a.removeAttribute("required")}}function h(e,n,a,i){const t=i.SupportedImageTypes||[];g(e.querySelector(".backdropFields"),t.includes("Backdrop")),Array.prototype.forEach.call(e.querySelectorAll(".imageType"),s=>{const c=s.getAttribute("data-imagetype")||"",o=m.parentWithTag(s,"LABEL");t.includes(c)?o.classList.remove("hide"):o.classList.add("hide"),d(a,i,c,n).Limit?s.checked=!0:s.checked=!1});const l=d(a,i,"Backdrop",n);e.querySelector("#txtMaxBackdrops").value=String(l.Limit),e.querySelector("#txtMinBackdropDownloadWidth").value=String(l.MinWidth)}function k(e,n){n.ImageOptions=Array.prototype.map.call(e.querySelectorAll(".imageType:not(.hide)"),a=>({Type:a.getAttribute("data-imagetype")||"",Limit:a.checked?1:0,MinWidth:0})),n.ImageOptions.push({Type:"Backdrop",Limit:parseInt(e.querySelector("#txtMaxBackdrops").value,10),MinWidth:parseInt(e.querySelector("#txtMinBackdropDownloadWidth").value,10)})}class L{show(n,a,i){const t=r.createDialog({size:"small",removeOnClose:!0,scrollY:!1});t.classList.add("formDialog"),t.innerHTML=u.translateHtml(b),t.addEventListener("close",function(){k(t,a)}),h(t,n,a,i),r.open(t).then(()=>{}).catch(()=>{}),t.querySelector(".btnCancel").addEventListener("click",function(){r.close(t)})}}export{L as default};
