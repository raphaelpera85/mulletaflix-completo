import{l as c,c as s,f as a,bp as p,a2 as i,W as d,e as h,d as v,T as L,S as g}from"./index-6F8mVyyU.js";import{g as S}from"./lyrics-api-CpHKX-n4.js";import"./emby-select-BS-dUTXm.js";import{r as w}from"./file-CnsaP6k6.js";import"./actionSheet-bHfWrycr.js";const D=`<div class="formDialogHeader">
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
`;let y,m,u,f=!1;function q(e){i.hide();const n=e.target.error;n&&n.name!=="AbortError"&&d(a.translate("MessageFileReadError"))}function b(e){return!!e&&[".lrc",".txt"].some(function(n){return e.name.endsWith(n)})}function F(e,n){if(!n||n.length===0)return;const r=n[0];if(!b(r)){e.querySelector("#lyricsOutput").innerHTML="",e.querySelector("#fldUpload").classList.add("hide"),e.querySelector("#labelDropLyrics").classList.remove("hide"),u=null;return}u=r;const t=new FileReader;t.onerror=q,t.onloadstart=function(){e.querySelector("#fldUpload").classList.add("hide")},t.onabort=function(){i.hide(),console.debug("File read cancelled")},t.onload=(function(o){return function(){const l=`<div><span class="material-icons lyrics" aria-hidden="true" style="transform: translateY(25%);"></span><span>${h(o.name)}</span></div>`;e.querySelector("#lyricsOutput").innerHTML=l,e.querySelector("#fldUpload").classList.remove("hide"),e.querySelector("#labelDropLyrics").classList.add("hide")}})(r),t.readAsDataURL(r)}async function E(e){e.preventDefault();const n=u;if(!b(n)){d(a.translate("MessageLyricsFileTypeAllowed"));return}i.show();const r=v.parentWithClass(this,"dialog"),t=L(g.getApiClient(m)),o=S(t);try{const l=await w(n);await o.uploadLyrics({itemId:y,fileName:n.name,body:l}),r.querySelector("#uploadLyrics").value="",f=!0,s.close(r)}catch{d(a.translate("ErrorDefault"))}finally{i.hide()}}function H(e){e.querySelector(".uploadLyricsForm").addEventListener("submit",E),e.querySelector("#uploadLyrics").addEventListener("change",function(){F(e,this.files)}),e.querySelector(".btnBrowse").addEventListener("click",function(){e.querySelector("#uploadLyrics").click()})}function A(e,n){e=e||{},y=e.itemId||"",m=e.serverId||"";const r={removeOnClose:!0,scrollY:!1};c.tv?r.size="fullscreen":r.size="small";const t=s.createDialog(r);t.classList.add("formDialog"),t.classList.add("lyricsUploaderDialog"),t.innerHTML=a.translateHtml(D,"core"),c.tv&&p.centerFocus.on(t,!1),t.addEventListener("close",function(){c.tv&&p.centerFocus.off(t,!1),i.hide(),n(f)}),s.open(t).catch(()=>i.hide()),H(t),t.querySelector(".btnCancel").addEventListener("click",function(){s.close(t)})}function C(e){return new Promise(function(n){f=!1,A(e,n)})}const k={show:C};export{k as default,C as show};
