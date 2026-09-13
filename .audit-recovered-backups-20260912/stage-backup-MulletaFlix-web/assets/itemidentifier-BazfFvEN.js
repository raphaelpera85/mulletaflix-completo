import{D as I,l as y,G as m,s as p,aC as q,S as T,n as F,B as d,i as L,ag as $,aG as P,j as S}from"./index-CFwqrzSZ.js";import"./emby-checkbox-CDwDlGOj.js";/* empty css             */import"./vendor-react-query-BiAmjtRH.js";import"./vendor-react-CeUY3tRn.js";import"./vendor-jellyfin-Bee54tkY.js";import"./vendor-axios-BCn5QfZZ.js";import"./vendor-lodash-CYyrkkBC.js";import"./vendor-date-fns-CwLI6ukS.js";import"./vendor-dompurify-Baz99PXY.js";const D=`<div class="formDialogHeader">
    <button is="paper-icon-button-light" class="btnCancel autoSize" tabindex="-1" title="\${ButtonBack}">
        <span class="material-icons arrow_back" aria-hidden="true"></span>
    </button>
    <h3 class="formDialogHeaderTitle">\${Identify}</h3>
</div>

<div class="formDialogContent smoothScrollY">
    <div class="dialogContentInner dialog-content-centered">
        <form class="popupIdentifyForm" style="margin:auto;">

            <p>\${HeaderIdentifyItemHelp}</p>
            <div class="padded-bottom fldPath hide">
                <div>\${LabelPath}</div>
                <div class="txtPath fieldDescription"></div>
            </div>
            <div class="inputContainer">
                <input is="emby-input" type="text" id="txtLookupName" class="identifyField" data-lookup="Name" label="\${LabelName}" />
            </div>
            <div class="fldLookupYear inputContainer">
                <input is="emby-input" type="number" id="txtLookupYear" class="identifyField" data-lookup="Year" pattern="[0-9]*" min="1800" label="\${LabelYear}" />
            </div>

            <div class="identifyProviderIds"></div>

            <div class="formDialogFooter">
                <button is="emby-button" type="submit" class="raised button-submit block formDialogFooterItem">
                    <span>\${Search}</span>
                </button>
            </div>
        </form>

        <div class="identificationSearchResults hide">
            <h1>\${SearchResults}</h1>
            <div class="identificationSearchResultList itemsContainer vertical-wrap"></div>
        </div>

        <form class="identifyOptionsForm hide" style="margin:auto;">
            <br />
            <div class="selectedSearchResult"></div>
            <br />
            <label class="checkboxContainer">
                <input type="checkbox" is="emby-checkbox" id="chkIdentifyReplaceImages" />
                <span>\${ReplaceExistingImages}</span>
            </label>

            <div class="formDialogFooter">
                <button is="emby-button" type="submit" class="raised button-submit block btnSubmit formDialogFooterItem">
                    <span>\${ButtonOk}</span>
                </button>
            </div>
        </form>
    </div>
</div>
`,R=!S.slow&&!S.edge;let f,u,C,x,k,v=!1,g;function b(){return T.getApiClient(C)}function w(t){let a={ProviderIds:{}},e,n;const i=t.querySelectorAll(".identifyField");let o;for(e=0,n=i.length;e<n;e++)o=i[e].value,o&&(i[e].type==="number"&&(o=parseInt(o,10)),a[i[e].getAttribute("data-lookup")]=o);let r=!1;const s=t.querySelectorAll(".txtLookupId");for(e=0,n=s.length;e<n;e++)o=s[e].value,o&&(r=!0),a.ProviderIds[s[e].getAttribute("data-providerkey")]=o;if(!r&&!a.Name){F(p.translate("PleaseEnterNameOrId"));return}a={SearchInfo:a},f?.Id?a.ItemId=f.Id:a.IncludeDisabledProviders=!0;const c=b();I.withLoading(()=>c.ajax({type:"POST",url:c.getUrl(`Items/RemoteSearch/${u}`),data:JSON.stringify(a),contentType:"application/json",dataType:"json"})).then(l=>{N(t,l)}).catch(l=>console.error("Failed to search identification results",l))}function N(t,a){const e=t.querySelector(".identificationSearchResults");t.querySelector(".popupIdentifyForm").classList.add("hide"),e.classList.remove("hide"),t.querySelector(".identifyOptionsForm").classList.add("hide"),t.querySelector(".dialogContentInner").classList.remove("dialog-content-centered");let n="",i,o;for(i=0,o=a.length;i<o;i++){const l=a[i];n+=O(l,i)}const r=t.querySelector(".identificationSearchResultList");r.innerHTML=n;function s(){const l=parseInt(this.getAttribute("data-index"),10),h=a[l];f!=null?H(t,h):A(t,h)}const c=r.querySelectorAll(".card");for(i=0,o=c.length;i<o;i++)c[i].addEventListener("click",s);y.tv&&L.autoFocus(e)}function A(t,a){g=a,v=!0,m.close(t)}function H(t,a){const e=t.querySelector(".identifyOptionsForm");t.querySelector(".popupIdentifyForm").classList.add("hide"),t.querySelector(".identificationSearchResults").classList.add("hide"),e.classList.remove("hide"),t.querySelector("#chkIdentifyReplaceImages").checked=!0,t.querySelector(".dialogContentInner").classList.add("dialog-content-centered"),g=a;const n=[];n.push(d(a.Name)),a.ProductionYear&&n.push($.toLocaleString(a.ProductionYear));let i=n.join("<br/>");const o=P(a.ImageUrl);o&&(i=`<div style="display:flex;align-items:center;"><img src="${d(o)}" style="max-height:240px;" /><div style="margin-left:1em;">${i}</div>`),t.querySelector(".selectedSearchResult").innerHTML=i,L.focus(e.querySelector(".btnSubmit"))}function O(t,a){let e="",n="card scalableCard";const i="cardBox";let o;u==="Episode"?(n+=" backdropCard backdropCard-scalable",o="cardPadder-backdrop"):u==="MusicAlbum"||u==="MusicArtist"?(n+=" squareCard squareCard-scalable",o="cardPadder-square"):(n+=" portraitCard portraitCard-scalable",o="cardPadder-portrait"),y.tv&&(n+=" show-focus",R&&(n+=" show-animation"));const r=i+" cardBox-bottompadded";e+=`<button type="button" class="${n}" data-index="${a}">`,e+=`<div class="${r}">`,e+='<div class="cardScalable">',e+=`<div class="${o}"></div>`,e+='<div class="cardContent searchImage">',t.ImageUrl?e+=`<div class="cardImageContainer coveredImage" style="background-image:url('${d(t.ImageUrl)}');"></div>`:e+=`<div class="cardImageContainer coveredImage defaultCardBackground defaultCardBackground1"><div class="cardText cardCenteredText">${d(t.Name)}</div></div>`,e+="</div>",e+="</div>";let s=3;u==="MusicAlbum"&&s++;const c=[t.Name];c.push(t.SearchProviderName),t.AlbumArtist&&c.push(t.AlbumArtist.Name),t.ProductionYear&&c.push(t.ProductionYear);for(let l=0;l<s;l++)l===0?e+='<div class="cardText cardText-first cardTextCentered">':e+='<div class="cardText cardText-secondary cardTextCentered">',e+=d(c[l]||"")||"&nbsp;",e+="</div>";return e+="</div>",e+="</button>",e}function Y(t){const a={ReplaceAllImages:t.querySelector("#chkIdentifyReplaceImages").checked},e=b();I.withLoading(()=>e.ajax({type:"POST",url:e.getUrl(`Items/RemoteSearch/Apply/${f.Id}`,a),data:JSON.stringify(g),contentType:"application/json"})).then(()=>{v=!0,m.close(t)}).catch(n=>{console.error("Failed to apply identification result",n),m.close(t)})}function B(t,a){const e=b();e.getJSON(e.getUrl(`Items/${a.Id}/ExternalIdInfos`)).then(n=>{let i="";for(let o=0,r=n.length;o<r;o++){const s=n[o],c=`txtLookup${s.Key}`;i+='<div class="inputContainer">';let l=s.Name;s.Type&&(l=`${s.Name} ${p.translate(s.Type)}`);const h=p.translate("LabelDynamicExternalId",d(l));i+=`<input is="emby-input" class="txtLookupId" data-providerkey="${d(String(s.Key))}" id="${d(c)}" label="${h}"/>`,i+="</div>"}t.querySelector("#txtLookupName").value="",a.Type==="Person"||a.Type==="BoxSet"?(t.querySelector(".fldLookupYear").classList.add("hide"),t.querySelector("#txtLookupYear").value=""):(t.querySelector(".fldLookupYear").classList.remove("hide"),t.querySelector("#txtLookupYear").value=""),t.querySelector(".identifyProviderIds").innerHTML=i,t.querySelector(".formDialogHeaderTitle").innerHTML=p.translate("Identify")})}async function E(t){const a=b();await I.withLoading(async()=>{const e=await a.getItem(a.getCurrentUserId(),t);f=e,u=f.Type;const n={size:"small",removeOnClose:!0,scrollY:!1};y.tv&&(n.size="fullscreen");const i=m.createDialog(n);i.classList.add("formDialog"),i.classList.add("recordingDialog");let o="";o+=p.translateHtml(D,"core"),i.innerHTML=o,i.addEventListener("close",M),y.tv&&q.centerFocus.on(i.querySelector(".formDialogContent"),!1),e.Path?i.querySelector(".fldPath").classList.remove("hide"):i.querySelector(".fldPath").classList.add("hide"),i.querySelector(".txtPath").innerText=e.Path||"",m.open(i).catch(r=>console.error("Failed to open item identification dialog",r)),i.querySelector(".popupIdentifyForm").addEventListener("submit",r=>(r.preventDefault(),w(i),!1)),i.querySelector(".identifyOptionsForm").addEventListener("submit",r=>(r.preventDefault(),Y(i),!1)),i.querySelector(".btnCancel").addEventListener("click",()=>{m.close(i)}),i.classList.add("identifyDialog"),B(i,e)})}function M(){v?x():k()}function U(t,a){return new Promise((e,n)=>{x=e,k=n,C=a,v=!1,E(t).catch(i=>console.error("Failed to show item identification dialog",i))})}const Z={show:U};export{Z as default,U as show};
