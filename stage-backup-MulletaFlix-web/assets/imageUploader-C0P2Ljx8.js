import{l as d,c as l,f as o,bp as m,a2 as i,e as p,W as r,d as I,S}from"./index-6F8mVyyU.js";import"./emby-select-BS-dUTXm.js";import"./actionSheet-bHfWrycr.js";const L=`<div class="formDialogHeader">
    <button is="paper-icon-button-light" class="btnCancel autoSize" tabindex="-1" title="\${ButtonBack}"><span class="material-icons arrow_back" aria-hidden="true"></span></button>
    <h3 class="formDialogHeaderTitle">
        \${HeaderUploadImage}
    </h3>
</div>

<div class="formDialogContent smoothScrollY">
    <div class="dialogContentInner">

        <form class="uploadItemImageForm" style="max-width: 100%;">

            <div class="flex align-items-center" style="margin:1.5em 0;">
                <h2 style="margin:0;">\${HeaderAddUpdateImage}</h2>

                <button is="emby-button" type="button" class="raised raised-mini btnBrowse">
                    <span class="material-icons folder" aria-hidden="true"></span>
                    <span>\${Browse}</span>
                </button>
            </div>
            <div>
                <div class="imageEditor-dropZone fieldDescription">
                    <div id="dropImageText">\${LabelDropImageHere}</div>
                    <output id="imageOutput" class="flex align-items-center justify-content-center" style="position: absolute;top:0;left:0;right:0;bottom:0;width:100%;"></output>
                    <input type="file" accept="image/*" id="uploadImage" name="uploadImage" style="position: absolute;top:0;left:0;right:0;bottom:0;width:100%;opacity:0;" />
                </div>
                <div id="fldUpload" class="hide">
                    <br />
                    <div class="selectContainer">
                        <select is="emby-select" id="selectImageType" name="selectImageType" label="\${LabelImageType}">
                            <option value="None"></option>
                            <option value="Primary">\${Primary}</option>
                            <option value="Art">\${Art}</option>
                            <option value="Backdrop">\${Backdrop}</option>
                            <option value="Banner">\${Banner}</option>
                            <option value="Box">\${Box}</option>
                            <option value="BoxRear">\${BoxRear}</option>
                            <option value="Disc">\${Disc}</option>
                            <option value="Logo">\${Logo}</option>
                            <option value="Menu">\${Menu}</option>
                            <option value="Thumb">\${Thumb}</option>
                        </select>
                    </div>
                    <button is="emby-button" type="submit" class="raised button-submit block">
                        <span>\${Upload}</span>
                    </button>
                </div>
            </div>
        </form>
    </div>
</div>
`;let g,f,c,u=!1;function T(e){i.hide();const a=e.target.error;if(a)switch(a.name){case"NotFoundError":r(o.translate("MessageFileReadError"));break;case"AbortError":break;default:r(o.translate("MessageFileReadError"));break}}function $(e,a){if(!a||a.length===0)return;const n=a[0];if(!n?.type.startsWith("image/")){e.querySelector("#imageOutput").innerHTML="",e.querySelector("#fldUpload").classList.add("hide"),c=null;return}c=n;const t=new FileReader;t.onerror=T,t.onloadstart=()=>{e.querySelector("#fldUpload").classList.add("hide")},t.onabort=()=>{i.hide(),console.debug("File read cancelled")},t.onload=(v=>b=>{const s=b.target.result,h=typeof s=="string"&&/^data:image\/(?:jpeg|png|gif|webp|bmp);base64,/i.test(s)?s:"",y=['<img style="max-width:100%;max-height:100%;" src="',p(h),'" title="',p(v.name),'"/>'].join("");e.querySelector("#imageOutput").innerHTML=y,e.querySelector("#dropImageText").classList.add("hide"),e.querySelector("#fldUpload").classList.remove("hide")})(n),t.readAsDataURL(n)}function w(e){const a=c;if(!a)return!1;if(!a.type.startsWith("image/"))return r(o.translate("MessageImageFileTypeAllowed")),e.preventDefault(),!1;i.show();const n=I.parentWithClass(this,"dialog"),t=n.querySelector("#selectImageType").value;return t==="None"?(r(o.translate("MessageImageTypeNotSelected")),e.preventDefault(),!1):(S.getApiClient(f).uploadItemImage(g,t,a).then(()=>{n.querySelector("#uploadImage").value="",i.hide(),u=!0,l.close(n)}).catch(()=>{i.hide(),r(o.translate("ImageUploadFailed"))}),e.preventDefault(),!1)}function D(e){e.querySelector("form").addEventListener("submit",w),e.querySelector("#uploadImage").addEventListener("change",function(){$(e,this.files)}),e.querySelector(".btnBrowse").addEventListener("click",()=>{e.querySelector("#uploadImage").click()})}function q(e,a){e=e||{},g=e.itemId||"",f=e.serverId||"";const n={removeOnClose:!0};d.tv?n.size="fullscreen":n.size="small";const t=l.createDialog(n);t.classList.add("formDialog"),t.innerHTML=o.translateHtml(L,"core"),d.tv&&m.centerFocus.on(t,!1),t.addEventListener("close",()=>{d.tv&&m.centerFocus.off(t,!1),i.hide(),a(u)}),l.open(t).catch(()=>{}),D(t),t.querySelector("#selectImageType").value=e.imageType||"Primary",t.querySelector(".btnCancel").addEventListener("click",()=>{l.close(t)})}function x(e){return new Promise(a=>{u=!1,q(e,a)})}const H={show:x};export{H as default,x as show};
