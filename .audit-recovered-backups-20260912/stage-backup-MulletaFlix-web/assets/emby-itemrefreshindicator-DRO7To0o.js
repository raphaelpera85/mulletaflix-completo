import{b8 as o,b9 as l,S as c,h as d}from"./index-CFwqrzSZ.js";import{O as n}from"./vendor-jellyfin-Bee54tkY.js";import"./vendor-react-query-BiAmjtRH.js";import"./vendor-react-CeUY3tRn.js";import"./vendor-lodash-CYyrkkBC.js";import"./vendor-date-fns-CwLI6ukS.js";import"./vendor-dompurify-Baz99PXY.js";import"./vendor-axios-BCn5QfZZ.js";const m=`<div class="progressring-bg">
    <div class="progressring-text"></div>
</div>
<div class="spiner-holder-one animate-0-25-a">
    <div class="spiner-holder-two animate-0-25-b">
        <div class="progressring-spiner"></div>
    </div>
</div>
<div class="spiner-holder-one animate-25-50-a">
    <div class="spiner-holder-two animate-25-50-b">
        <div class="progressring-spiner"></div>
    </div>
</div>
<div class="spiner-holder-one animate-50-75-a">
    <div class="spiner-holder-two animate-50-75-b">
        <div class="progressring-spiner"></div>
    </div>
</div>
<div class="spiner-holder-one animate-75-100-a">
    <div class="spiner-holder-two animate-75-100-b">
        <div class="progressring-spiner"></div>
    </div>
</div>
`,s=Object.create(HTMLDivElement.prototype);s.createdCallback=function(){this.classList.add("progressring"),this.setAttribute("dir","ltr");const e=this;if(e.innerHTML=m,window.MutationObserver){const t=new MutationObserver(function(a){a.forEach(function(){e.setProgress(parseFloat(e.getAttribute("data-progress")||"0"))})}),r={attributes:!0,childList:!1,characterData:!1};t.observe(e,r),e.observer=t}e.setProgress(parseFloat(e.getAttribute("data-progress")||"0"))};s.setProgress=function(e){e=Math.floor(e);let t;e<25?(t=-90+e/100*360,this.querySelector(".animate-0-25-b").style.transform="rotate("+t+"deg)",this.querySelector(".animate-25-50-b").style.transform="rotate(-90deg)",this.querySelector(".animate-50-75-b").style.transform="rotate(-90deg)",this.querySelector(".animate-75-100-b").style.transform="rotate(-90deg)"):e>=25&&e<50?(t=-90+(e-25)/100*360,this.querySelector(".animate-0-25-b").style.transform="none",this.querySelector(".animate-25-50-b").style.transform="rotate("+t+"deg)",this.querySelector(".animate-50-75-b").style.transform="rotate(-90deg)",this.querySelector(".animate-75-100-b").style.transform="rotate(-90deg)"):e>=50&&e<75?(t=-90+(e-50)/100*360,this.querySelector(".animate-0-25-b").style.transform="none",this.querySelector(".animate-25-50-b").style.transform="none",this.querySelector(".animate-50-75-b").style.transform="rotate("+t+"deg)",this.querySelector(".animate-75-100-b").style.transform="rotate(-90deg)"):e>=75&&e<=100&&(t=-90+(e-75)/100*360,this.querySelector(".animate-0-25-b").style.transform="none",this.querySelector(".animate-25-50-b").style.transform="none",this.querySelector(".animate-50-75-b").style.transform="none",this.querySelector(".animate-75-100-b").style.transform="rotate("+t+"deg)"),this.querySelector(".progressring-text").innerHTML=o(e/100,l())};s.attachedCallback=function(){};s.detachedCallback=function(){const e=this.observer;e&&(e.disconnect(),this.observer=null)};document.registerElement("emby-progressring",{prototype:s,extends:"div"});function b(e,t){if(e.itemId||(e.itemId=d.parentWithAttribute(e,"data-id").getAttribute("data-id")),t?.ItemId===e.itemId){const r=parseFloat(String(t.Progress));r&&r<100?e.classList.remove("hide"):e.classList.add("hide"),e.dataset.progress=String(r)}}const i=Object.create(s);i.createdCallback=function(){s.createdCallback&&s.createdCallback.call(this);const e=({Data:t})=>b(this,t);this._wsApiClientCreatedHandler=(t,r)=>{const a=r.subscribe([n.RefreshProgress],e);a&&this._wsUnsubscribers.push(a)},this._wsUnsubscribers=c.getApiClients().map(t=>t.subscribe([n.RefreshProgress],e)).filter(Boolean)};i.attachedCallback=function(){s.attachedCallback&&s.attachedCallback.call(this)};i.detachedCallback=function(){s.detachedCallback&&s.detachedCallback.call(this),this._wsUnsubscribers?.forEach(e=>{e()}),this._wsUnsubscribers=[],this._wsApiClientCreatedHandler&&(this._wsApiClientCreatedHandler=null),this.itemId=null};document.registerElement("emby-itemrefreshindicator",{prototype:i,extends:"div"});
