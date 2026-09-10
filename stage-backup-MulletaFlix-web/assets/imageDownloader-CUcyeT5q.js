import{a2 as d,S as q,l as u,c as b,f as c,bp as B,d as p,e as s,W as P,cc as A,k as z,A as F,b as S}from"./index-6F8mVyyU.js";import{i as M}from"./imageLoader-BuyzOWYq.js";const N=`<div class="formDialogHeader">
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
`,W=!S.slow&&!S.edge;let H,f,$,R,C=!1;const k=S.slow?6:30;let m=0,g="Primary",h,y;function D(e,t=!1){const n={};return!t&&e.querySelector("#chkShowParentImages").checked&&y?n.itemId=y:n.itemId=H,n}function I(e,t){d.show();const n=D(e);n.type=g,n.startIndex=m,n.limit=k,n.IncludeAllLanguages=e.querySelector("#chkAllLanguages").checked;const a=h||"";a&&(n.ProviderName=a),t.getAvailableRemoteImages(n).then(function(r){_(e,t,r,g,n.startIndex,n.limit),e.querySelector("#selectBrowsableImageType").value=g;const o=r.Providers.map(function(l){return'<option value="'+s(l)+'">'+s(l)+"</option>"}).join(""),i=e.querySelector("#selectImageProvider");i.innerHTML='<option value="">'+c.translate("All")+"</option>"+o,i.value=a,d.hide()}).catch(r=>{d.hide(),console.error("Failed to load remote images",r),P(c.translate("ErrorDefault"))})}function _(e,t,n,a,r,o){e.querySelector(".availableImagesPaging").innerHTML=U(r,o,n.TotalRecordCount);let i="";for(let w=0,E=n.Images.length;w<E;w++)i+=J(n.Images[w],a);const l=e.querySelector(".availableImagesList");l.innerHTML=i,M.lazyChildren(l);const L=e.querySelector(".btnNextPage"),x=e.querySelector(".btnPreviousPage");L&&L.addEventListener("click",function(){m+=k,I(e,t)}),x&&x.addEventListener("click",function(){m-=k,I(e,t)})}function U(e,t,n){let a="";const r=Math.min(e+t,n),o=n>t;a+='<div class="listPaging">',a+='<span style="margin-right: 10px;">';const i=n?e+1:0;return a+=c.translate("ListPaging",String(i),String(r),String(n)),a+="</span>",o&&(a+='<div data-role="controlgroup" data-type="horizontal" style="display:inline-block;">',a+=`<button is="paper-icon-button-light" title="${c.translate("Previous")}" class="btnPreviousPage autoSize" ${e?"":"disabled"}><span class="material-icons arrow_back" aria-hidden="true"></span></button>`,a+=`<button is="paper-icon-button-light" title="${c.translate("Next")}" class="btnNextPage autoSize" ${e+t>=n?"disabled":""}><span class="material-icons arrow_forward" aria-hidden="true"></span></button>`,a+="</div>"),a+="</div>",a}function T(e,t,n,a,r){const o=A(n);if(!o||!a||!r){P(c.translate("ErrorDefault"));return}const i=D(e,!0);i.Type=a,i.ImageUrl=o,i.ProviderName=r,d.show(),t.downloadRemoteImage(i).then(function(){C=!0;const l=p.parentWithClass(e,"dialog");b.close(l)}).catch(l=>{d.hide(),console.error("Failed to download remote image",l),P(c.translate("ErrorDefault"))})}function j(e){return["Backdrop","Art","Thumb","Logo"].includes(e)||f==="Episode"?"backdrop":e==="Banner"?"banner":e==="Disc"||f==="MusicAlbum"||f==="MusicArtist"?"square":"portrait"}function O(e,t){const n=u.tv||!z.supports(F.ExternalLinks)?'<div class="cardImageContainer lazy" data-src="'+s(e)+'" style="background-position:center center;background-size:contain;"></div>':'<a is="emby-linkbutton" target="_blank" rel="noopener noreferrer" href="'+s(e)+'" class="button-link cardImageContainer lazy" data-src="'+s(e)+'" style="background-position:center center;background-size:contain"></a>';return'<div class="cardBox visualCardBox"><div class="cardScalable visualCardBox-cardScalable" style="background-color:transparent;"><div class="cardPadder-'+t+'"></div><div class="cardContent">'+n+"</div></div>"}function V(e){if(!e.Width&&!e.Height&&!e.Language)return"";let t='<div class="cardText cardText-secondary cardTextCentered">';return e.Width&&e.Height?(t+=e.Width+" x "+e.Height,e.Language&&(t+=" • "+s(e.Language))):e.Language&&(t+=s(e.Language)),t+"</div>"}function Y(e){if(e.CommunityRating==null)return"";let t='<div class="cardText cardText-secondary cardTextCentered">';return e.RatingType==="Likes"?t+=e.CommunityRating+(e.CommunityRating===1?" like":" likes"):e.CommunityRating?(t+=e.CommunityRating.toFixed(1),e.VoteCount&&(t+=" • "+e.VoteCount+(e.VoteCount===1?" vote":" votes"))):t+="Unrated",t+"</div>"}function G(e,t){let n='<div class="cardFooter visualCardBox-cardFooter"><div class="cardText cardTextCentered">'+s(e.ProviderName||"")+"</div>"+V(e)+Y(e);return t&&(n+=`<div class="cardText cardTextCentered"><button is="paper-icon-button-light" class="btnDownloadRemoteImage autoSize" raised" title="${s(c.translate("Download"))}"><span class="material-icons cloud_download" aria-hidden="true"></span></button></div>`),n+"</div>"}function J(e,t){const n=u.tv?"button":"div",a=j(t),r=A(e.Url);if(!r)return"";let o="card scalableCard imageEditorCard "+a+"Card "+a+"Card-scalable";n==="button"&&(o+=" btnImageCard",u.tv&&(o+=" show-focus",W&&(o+=" show-animation")));const i=' data-imageprovider="'+s(e.ProviderName||"")+'" data-imageurl="'+s(r)+'" data-imagetype="'+s(e.Type||"")+'"';return(n==="button"?'<button type="button" class="'+o+'"':'<div class="'+o+'"')+i+">"+O(r,a)+G(e,!u.tv)+"</"+n+">"}function v(e,t){m=0,I(e,t)}function K(e,t){e.querySelector("#selectBrowsableImageType").addEventListener("change",function(){g=this.value,h=null,v(e,t)}),e.querySelector("#selectImageProvider").addEventListener("change",function(){h=this.value,v(e,t)}),e.querySelector("#chkAllLanguages").addEventListener("change",function(){v(e,t)}),e.querySelector("#chkShowParentImages").addEventListener("change",function(){v(e,t)}),e.addEventListener("click",function(n){const a=n.target,r=p.parentWithClass(a,"btnDownloadRemoteImage");if(r){const i=p.parentWithClass(r,"card");T(e,t,i.getAttribute("data-imageurl"),i.getAttribute("data-imagetype"),i.getAttribute("data-imageprovider"));return}const o=p.parentWithClass(a,"btnImageCard");o&&T(e,t,o.getAttribute("data-imageurl"),o.getAttribute("data-imagetype"),o.getAttribute("data-imageprovider"))})}function Q(e,t,n){d.show();const a=q.getApiClient(t);H=e,f=n;const r={removeOnClose:!0};u.tv?r.size="fullscreen":r.size="small";const o=b.createDialog(r);o.innerHTML=c.translateHtml(N,"core"),u.tv&&B.centerFocus.on(o,!1),y&&o.querySelector("#lblShowParentImages").classList.remove("hide"),o.addEventListener("close",X),b.open(o).catch(()=>{});const i=o.querySelector(".formDialogContent");K(i,a),o.querySelector(".btnCancel").addEventListener("click",function(){b.close(o)}),I(i,a)}function X(){const e=this;u.tv&&B.centerFocus.off(e,!1),d.hide(),C?$():R()}function Z(e,t,n,a,r){return new Promise(function(o,i){$=o,R=i,C=!1,m=0,g=a||"Primary",h=null,y=r||"",Q(e,t,n)})}const ne={show:Z};export{ne as default,Z as show};
