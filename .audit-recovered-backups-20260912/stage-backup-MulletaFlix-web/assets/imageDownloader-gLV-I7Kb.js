import{S as q,l as d,G as v,s as c,aC as T,h as b,D as B,B as s,n as w,aG as A,w as z,A as F,j as P}from"./index-CFwqrzSZ.js";import{i as M}from"./imageLoader-BC9XjmZs.js";import"./emby-checkbox-CDwDlGOj.js";/* empty css             */import"./vendor-react-query-BiAmjtRH.js";import"./vendor-react-CeUY3tRn.js";import"./vendor-jellyfin-Bee54tkY.js";import"./vendor-axios-BCn5QfZZ.js";import"./vendor-lodash-CYyrkkBC.js";import"./vendor-date-fns-CwLI6ukS.js";import"./vendor-dompurify-Baz99PXY.js";const N=`<div class="formDialogHeader">
    <button is="paper-icon-button-light" class="btnCancel autoSize" tabindex="-1" title="\${ButtonBack}"><span class="material-icons arrow_back" aria-hidden="true"></span></button>
    <h3 class="formDialogHeaderTitle">
        \${Search}
    </h3>
</div>

<div class="formDialogContent smoothScrollY">
    <div class="dialogContentInner">
        <div class="flex align-items-center justify-content-center flex-wrap-wrap" style="margin: 2em 0;">

            <div style="margin: 0;">
                <div class="selectContainer">
                    <select id="selectImageProvider" name="selectImageProvider" is="emby-select" label="\${LabelSource}">
                        <option value="">\${All}</option>
                    </select>
                </div>
            </div>

            <div style="margin-left:1em;">
                <div class="selectContainer">
                    <select id="selectBrowsableImageType" name="selectBrowsableImageType" is="emby-select" label="\${LabelType}">
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
            </div>
            <div class="availableImagesPaging" style="margin-left:1em;"></div>
            <label style="margin: 0 0 0 1em;width:auto;">
                <input id="chkAllLanguages" type="checkbox" is="emby-checkbox" />
                <span>\${AllLanguages}</span>
            </label>
            <label id="lblShowParentImages" class="hide" style="margin: 0 0 0 1em;width:auto;">
                <input id="chkShowParentImages" type="checkbox" is="emby-checkbox" />
                <span>\${ShowParentImages}</span>
            </label>
        </div>

        <div class="availableImagesList vertical-wrap centered"></div>
    </div>
</div>
`,W=!P.slow&&!P.edge;let H,p,$,D,L=!1;const S=P.slow?6:30;let m=0,u="Primary",f,h;function R(e,t=!1){const n={};return!t&&e.querySelector("#chkShowParentImages").checked&&h?n.itemId=h:n.itemId=H,n}function y(e,t){const n=R(e);n.type=u,n.startIndex=m,n.limit=S,n.IncludeAllLanguages=e.querySelector("#chkAllLanguages").checked;const a=f||"";a&&(n.ProviderName=a),B.withLoading(()=>t.getAvailableRemoteImages(n)).then(function(r){_(e,t,r,u,n.startIndex,n.limit),e.querySelector("#selectBrowsableImageType").value=u;const o=r.Providers.map(function(l){return'<option value="'+s(l)+'">'+s(l)+"</option>"}).join(""),i=e.querySelector("#selectImageProvider");i.innerHTML='<option value="">'+c.translate("All")+"</option>"+o,i.value=a}).catch(r=>{console.error("Failed to load remote images",r),w(c.translate("ErrorDefault"))})}function _(e,t,n,a,r,o){e.querySelector(".availableImagesPaging").innerHTML=j(r,o,n.TotalRecordCount);let i="";for(let I=0,E=n.Images.length;I<E;I++)i+=J(n.Images[I],a);const l=e.querySelector(".availableImagesList");l.innerHTML=i,M.lazyChildren(l);const C=e.querySelector(".btnNextPage"),k=e.querySelector(".btnPreviousPage");C&&C.addEventListener("click",function(){m+=S,y(e,t)}),k&&k.addEventListener("click",function(){m-=S,y(e,t)})}function j(e,t,n){let a="";const r=Math.min(e+t,n),o=n>t;a+='<div class="listPaging">',a+='<span style="margin-right: 10px;">';const i=n?e+1:0;return a+=c.translate("ListPaging",String(i),String(r),String(n)),a+="</span>",o&&(a+='<div data-role="controlgroup" data-type="horizontal" style="display:inline-block;">',a+=`<button is="paper-icon-button-light" title="${c.translate("Previous")}" class="btnPreviousPage autoSize" ${e?"":"disabled"}><span class="material-icons arrow_back" aria-hidden="true"></span></button>`,a+=`<button is="paper-icon-button-light" title="${c.translate("Next")}" class="btnNextPage autoSize" ${e+t>=n?"disabled":""}><span class="material-icons arrow_forward" aria-hidden="true"></span></button>`,a+="</div>"),a+="</div>",a}function x(e,t,n,a,r){const o=A(n);if(!o||!a||!r){w(c.translate("ErrorDefault"));return}const i=R(e,!0);i.Type=a,i.ImageUrl=o,i.ProviderName=r,B.withLoading(()=>t.downloadRemoteImage(i)).then(function(){L=!0;const l=b.parentWithClass(e,"dialog");v.close(l)}).catch(l=>{console.error("Failed to download remote image",l),w(c.translate("ErrorDefault"))})}function U(e){return["Backdrop","Art","Thumb","Logo"].includes(e)||p==="Episode"?"backdrop":e==="Banner"?"banner":e==="Disc"||p==="MusicAlbum"||p==="MusicArtist"?"square":"portrait"}function O(e,t){const n=d.tv||!z.supports(F.ExternalLinks)?'<div class="cardImageContainer lazy" data-src="'+s(e)+'" style="background-position:center center;background-size:contain;"></div>':'<a is="emby-linkbutton" target="_blank" rel="noopener noreferrer" href="'+s(e)+'" class="button-link cardImageContainer lazy" data-src="'+s(e)+'" style="background-position:center center;background-size:contain"></a>';return'<div class="cardBox visualCardBox"><div class="cardScalable visualCardBox-cardScalable" style="background-color:transparent;"><div class="cardPadder-'+t+'"></div><div class="cardContent">'+n+"</div></div>"}function V(e){if(!e.Width&&!e.Height&&!e.Language)return"";let t='<div class="cardText cardText-secondary cardTextCentered">';return e.Width&&e.Height?(t+=e.Width+" x "+e.Height,e.Language&&(t+=" • "+s(e.Language))):e.Language&&(t+=s(e.Language)),t+"</div>"}function G(e){if(e.CommunityRating==null)return"";let t='<div class="cardText cardText-secondary cardTextCentered">';return e.RatingType==="Likes"?t+=e.CommunityRating+(e.CommunityRating===1?" like":" likes"):e.CommunityRating?(t+=e.CommunityRating.toFixed(1),e.VoteCount&&(t+=" • "+e.VoteCount+(e.VoteCount===1?" vote":" votes"))):t+="Unrated",t+"</div>"}function Y(e,t){let n='<div class="cardFooter visualCardBox-cardFooter"><div class="cardText cardTextCentered">'+s(e.ProviderName||"")+"</div>"+V(e)+G(e);return t&&(n+=`<div class="cardText cardTextCentered"><button is="paper-icon-button-light" class="btnDownloadRemoteImage autoSize" raised" title="${s(c.translate("Download"))}"><span class="material-icons cloud_download" aria-hidden="true"></span></button></div>`),n+"</div>"}function J(e,t){const n=d.tv?"button":"div",a=U(t),r=A(e.Url);if(!r)return"";let o="card scalableCard imageEditorCard "+a+"Card "+a+"Card-scalable";n==="button"&&(o+=" btnImageCard",d.tv&&(o+=" show-focus",W&&(o+=" show-animation")));const i=' data-imageprovider="'+s(e.ProviderName||"")+'" data-imageurl="'+s(r)+'" data-imagetype="'+s(e.Type||"")+'"';return(n==="button"?'<button type="button" class="'+o+'"':'<div class="'+o+'"')+i+">"+O(r,a)+Y(e,!d.tv)+"</"+n+">"}function g(e,t){m=0,y(e,t)}function K(e,t){e.querySelector("#selectBrowsableImageType").addEventListener("change",function(){u=this.value,f=null,g(e,t)}),e.querySelector("#selectImageProvider").addEventListener("change",function(){f=this.value,g(e,t)}),e.querySelector("#chkAllLanguages").addEventListener("change",function(){g(e,t)}),e.querySelector("#chkShowParentImages").addEventListener("change",function(){g(e,t)}),e.addEventListener("click",function(n){const a=n.target,r=b.parentWithClass(a,"btnDownloadRemoteImage");if(r){const i=b.parentWithClass(r,"card");x(e,t,i.getAttribute("data-imageurl"),i.getAttribute("data-imagetype"),i.getAttribute("data-imageprovider"));return}const o=b.parentWithClass(a,"btnImageCard");o&&x(e,t,o.getAttribute("data-imageurl"),o.getAttribute("data-imagetype"),o.getAttribute("data-imageprovider"))})}function Q(e,t,n){const a=q.getApiClient(t);H=e,p=n;const r={removeOnClose:!0};d.tv?r.size="fullscreen":r.size="small";const o=v.createDialog(r);o.innerHTML=c.translateHtml(N,"core"),d.tv&&T.centerFocus.on(o,!1),h&&o.querySelector("#lblShowParentImages").classList.remove("hide"),o.addEventListener("close",X),v.open(o).catch(()=>{});const i=o.querySelector(".formDialogContent");K(i,a),o.querySelector(".btnCancel").addEventListener("click",function(){v.close(o)}),y(i,a)}function X(){const e=this;d.tv&&T.centerFocus.off(e,!1),L?$():D()}function Z(e,t,n,a,r){return new Promise(function(o,i){$=o,D=i,L=!1,m=0,u=a||"Primary",f=null,h=r||"",Q(e,t,n)})}const ue={show:Z};export{ue as default,Z as show};
