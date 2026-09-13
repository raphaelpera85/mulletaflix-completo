import{l as o,G as s,s as r,aC as b,n as c,B as S,D as y,h as L,t as I,S as k}from"./index-CFwqrzSZ.js";import{x as q}from"./vendor-jellyfin-Bee54tkY.js";import"./emby-select-BQevfB2a.js";import{r as F}from"./file-B2HG98yY.js";import"./vendor-react-query-BiAmjtRH.js";import"./vendor-react-CeUY3tRn.js";import"./vendor-lodash-CYyrkkBC.js";import"./vendor-date-fns-CwLI6ukS.js";import"./vendor-dompurify-Baz99PXY.js";import"./vendor-axios-BCn5QfZZ.js";import"./actionSheet-BM-G2WQD.js";/* empty css                 */const H=`<div class="formDialogHeader">
    <button is="paper-icon-button-light" class="btnCancel autoSize" tabindex="-1" title="\${ButtonBack}"><span class="material-icons arrow_back" aria-hidden="true"></span></button>
    <h3 class="formDialogHeaderTitle">
        \${HeaderUploadSubtitle}
    </h3>
</div>

<div class="formDialogContent">
    <div class="dialogContentInner">

        <form class="uploadSubtitleForm">

            <div class="flex align-items-center" style="margin:1.5em 0;">
                <h2 style="margin:0;">\${HeaderAddUpdateSubtitle}</h2>

                <button is="emby-button" type="button" class="raised raised-mini btnBrowse">
                    <span class="material-icons folder" aria-hidden="true"></span>
                    <span>\${Browse}</span>
                </button>
            </div>
            <div>
                <div class="subtitleEditor-dropZone fieldDescription">
                    <div id="labelDropSubtitle">\${LabelDropSubtitleHere}</div>
                    <output id="subtitleOutput" class="flex align-items-center justify-content-center" style="position: absolute;top:0;left:0;right:0;bottom:0;width:100%;"></output>
                    <input type="file" accept=".sub,.srt,.vtt,.ass,.ssa,.mks" id="uploadSubtitle" name="uploadSubtitle" style="position: absolute;top:0;left:0;right:0;bottom:0;width:100%;opacity:0;"/>
                </div>
                <div id="fldUpload" class="hide">
                    <br />
                    <div class="checkboxContainer">
                        <label>
                            <input type="checkbox" is="emby-checkbox" id="chkIsForced" />
                            <span>\${LabelIsForced}</span>
                        </label>
                        <label>
                            <input type="checkbox" is="emby-checkbox" id="chkIsHearingImpaired" />
                            <span>\${LabelIsHearingImpaired}</span>
                        </label>
                    </div>
                    <div class="selectContainer flex-grow">
                        <select is="emby-select" id="selectLanguage" required="required" label="\${LabelLanguage}"></select>
                    </div>
                    <button is="emby-button" type="submit" class="raised button-submit block">
                        <span>\${Upload}</span>
                    </button>
                </div>
            </div>
        </form>
    </div>
</div>
`;let p,m,d,u=!1;function w(e){e.target.error.name!=="AbortError"&&c(r.translate("MessageFileReadError"))}function f(e){return!!e&&[".sub",".srt",".vtt",".ass",".ssa",".mks"].some(function(a){return e.name.endsWith(a)})}function D(e,a){const n=a[0];if(!f(n)){e.querySelector("#subtitleOutput").innerHTML="",e.querySelector("#fldUpload").classList.add("hide"),e.querySelector("#labelDropSubtitle").classList.remove("hide"),d=null;return}d=n;const t=new FileReader;t.onerror=w,t.onloadstart=function(){e.querySelector("#fldUpload").classList.add("hide")},t.onabort=function(){console.debug("File read cancelled")},t.onload=(function(l){return function(){const i=`<div><span class="material-icons subtitles" aria-hidden="true" style="transform: translateY(25%);"></span><span>${S(l.name)}</span></div>`;e.querySelector("#subtitleOutput").innerHTML=i,e.querySelector("#fldUpload").classList.remove("hide"),e.querySelector("#labelDropSubtitle").classList.add("hide")}})(n),t.readAsDataURL(n)}async function C(e){e.preventDefault();const a=d;if(!f(a)){c(r.translate("MessageSubtitleFileTypeAllowed"));return}await y.withLoading(async()=>{const n=L.parentWithClass(this,"dialog"),t=n.querySelector("#selectLanguage").value,l=n.querySelector("#chkIsForced").checked,i=n.querySelector("#chkIsHearingImpaired").checked,g=q(I(k.getApiClient(m)));try{const h=await F(a),v=a.name.substring(a.name.lastIndexOf(".")+1).toLowerCase();await g.uploadSubtitle({itemId:p,uploadSubtitleDto:{Data:h,Language:t,IsForced:l,Format:v,IsHearingImpaired:i}}),n.querySelector("#uploadSubtitle").value="",u=!0,s.close(n)}catch{c(r.translate("ErrorDefault"))}})}function x(e){e.querySelector(".uploadSubtitleForm").addEventListener("submit",C),e.querySelector("#uploadSubtitle").addEventListener("change",function(){D(e,this.files)}),e.querySelector(".btnBrowse").addEventListener("click",function(){e.querySelector("#uploadSubtitle").click()})}function E(e,a){e=e||{},p=e.itemId,m=e.serverId;const n={removeOnClose:!0,scrollY:!1};o.tv?n.size="fullscreen":n.size="small";const t=s.createDialog(n);t.classList.add("formDialog"),t.classList.add("subtitleUploaderDialog"),t.innerHTML=r.translateHtml(H,"core"),o.tv&&b.centerFocus.on(t,!1),t.addEventListener("close",function(){o.tv&&b.centerFocus.off(t,!1),a(u)}),s.open(t).catch(i=>console.error("[SubtitleUploader] failed to open dialog",i)),x(t);const l=t.querySelector("#selectLanguage");e.languages&&(l.innerHTML=e.languages.list||null,l.value=e.languages.value||null),t.querySelector(".btnCancel").addEventListener("click",function(){s.close(t)})}function U(e){return new Promise(function(a){u=!1,E(e,a)})}const P={show:U};export{P as default,U as show};
