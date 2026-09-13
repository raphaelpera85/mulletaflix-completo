import{l,G as s,s as o,aC as p,n as c,B as b,D as L,h as v,t as h,S as g}from"./index-CFwqrzSZ.js";import{v as S}from"./vendor-jellyfin-Bee54tkY.js";import"./emby-select-BQevfB2a.js";import{a as w}from"./file-B2HG98yY.js";import"./vendor-react-query-BiAmjtRH.js";import"./vendor-react-CeUY3tRn.js";import"./vendor-lodash-CYyrkkBC.js";import"./vendor-date-fns-CwLI6ukS.js";import"./vendor-dompurify-Baz99PXY.js";import"./vendor-axios-BCn5QfZZ.js";import"./actionSheet-BM-G2WQD.js";/* empty css                 */const D=`<div class="formDialogHeader">
    <button is="paper-icon-button-light" class="btnCancel autoSize" tabindex="-1" title="\${ButtonBack}"><span class="material-icons arrow_back" aria-hidden="true"></span></button>
    <h3 class="formDialogHeaderTitle">
        \${HeaderUploadLyrics}
    </h3>
</div>

<div class="formDialogContent">
    <div class="dialogContentInner">

        <form class="uploadLyricsForm">

            <div class="flex align-items-center" style="margin:1.5em 0;">
                <h2 style="margin:0;">\${HeaderAddLyrics}</h2>

                <button is="emby-button" type="button" class="raised raised-mini btnBrowse">
                    <span class="material-icons folder" aria-hidden="true"></span>
                    <span>\${Browse}</span>
                </button>
            </div>
            <div>
                <div class="lyricsEditor-dropZone fieldDescription">
                    <div id="labelDropLyrics">\${LabelDropLyricsHere}</div>
                    <output id="lyricsOutput" class="flex align-items-center justify-content-center" style="position: absolute;top:0;left:0;right:0;bottom:0;width:100%;"></output>
                    <input type="file" accept=".lrc,.txt" id="uploadLyrics" name="uploadLyrics" style="position: absolute;top:0;left:0;right:0;bottom:0;width:100%;opacity:0;"/>
                </div>
                <div id="fldUpload" class="hide">
                    <br />
                    <button is="emby-button" type="submit" class="raised button-submit block">
                        <span>\${Upload}</span>
                    </button>
                </div>
            </div>
        </form>
    </div>
</div>
`;let m,f,d,u=!1;function q(e){const r=e.target.error;r&&r.name!=="AbortError"&&c(o.translate("MessageFileReadError"))}function y(e){return!!e&&[".lrc",".txt"].some(function(r){return e.name.endsWith(r)})}function F(e,r){if(!r||r.length===0)return;const n=r[0];if(!y(n)){e.querySelector("#lyricsOutput").innerHTML="",e.querySelector("#fldUpload").classList.add("hide"),e.querySelector("#labelDropLyrics").classList.remove("hide"),d=null;return}d=n;const t=new FileReader;t.onerror=q,t.onloadstart=function(){e.querySelector("#fldUpload").classList.add("hide")},t.onabort=function(){console.debug("File read cancelled")},t.onload=(function(i){return function(){const a=`<div><span class="material-icons lyrics" aria-hidden="true" style="transform: translateY(25%);"></span><span>${b(i.name)}</span></div>`;e.querySelector("#lyricsOutput").innerHTML=a,e.querySelector("#fldUpload").classList.remove("hide"),e.querySelector("#labelDropLyrics").classList.add("hide")}})(n),t.readAsDataURL(n)}async function E(e){e.preventDefault();const r=d;if(!y(r)){c(o.translate("MessageLyricsFileTypeAllowed"));return}await L.withLoading(async()=>{const n=v.parentWithClass(this,"dialog"),t=h(g.getApiClient(f)),i=S(t);try{const a=await w(r);await i.uploadLyrics({itemId:m,fileName:r.name,body:a}),n.querySelector("#uploadLyrics").value="",u=!0,s.close(n)}catch{c(o.translate("ErrorDefault"))}})}function H(e){e.querySelector(".uploadLyricsForm").addEventListener("submit",E),e.querySelector("#uploadLyrics").addEventListener("change",function(){F(e,this.files)}),e.querySelector(".btnBrowse").addEventListener("click",function(){e.querySelector("#uploadLyrics").click()})}function C(e,r){e=e||{},m=e.itemId||"",f=e.serverId||"";const n={removeOnClose:!0,scrollY:!1};l.tv?n.size="fullscreen":n.size="small";const t=s.createDialog(n);t.classList.add("formDialog"),t.classList.add("lyricsUploaderDialog"),t.innerHTML=o.translateHtml(D,"core"),l.tv&&p.centerFocus.on(t,!1),t.addEventListener("close",function(){l.tv&&p.centerFocus.off(t,!1),r(u)}),s.open(t).catch(i=>console.error("[LyricsUploader] failed to open dialog",i)),H(t),t.querySelector(".btnCancel").addEventListener("click",function(){s.close(t)})}function A(e){return new Promise(function(r){u=!1,C(e,r)})}const Y={show:A};export{Y as default,A as show};
