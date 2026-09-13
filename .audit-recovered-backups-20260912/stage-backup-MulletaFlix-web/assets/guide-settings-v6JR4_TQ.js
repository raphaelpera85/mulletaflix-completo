import{l as r,G as i,s as p,aC as d,M as h,L as s}from"./index-CFwqrzSZ.js";import"./emby-checkbox-CDwDlGOj.js";import"./emby-radio-CF4xFnhF.js";import"./vendor-react-query-BiAmjtRH.js";import"./vendor-react-CeUY3tRn.js";import"./vendor-jellyfin-Bee54tkY.js";import"./vendor-axios-BCn5QfZZ.js";import"./vendor-lodash-CYyrkkBC.js";import"./vendor-date-fns-CwLI6ukS.js";import"./vendor-dompurify-Baz99PXY.js";const b=`<div class="formDialogHeader">
    <button is="paper-icon-button-light" class="btnCancel autoSize" title="\${Previous}" tabindex="-1">
        <span class="material-icons arrow_back" aria-hidden="true"></span>
    </button>
    <h3 class="formDialogHeaderTitle">
        \${Settings}
    </h3>
</div>
<div class="formDialogContent smoothScrollY">
    <form class="dialogContentInner dialog-content-centered" style="padding-top:2em;">

        <h3 class="checkboxListLabel">\${SortChannelsBy}</h3>
        <label class="radio-label-block"><input type="radio" is="emby-radio" name="ChannelSortOrder" value="Number" class="chkSortOrder" /><span>\${ChannelNumber}</span></label>
        <label class="radio-label-block"><input type="radio" is="emby-radio" name="ChannelSortOrder" value="DatePlayed" class="chkSortOrder" /><span>\${RecentlyWatched}</span></label>
        <br />
        <label class="checkboxContainer">
            <input type="checkbox" is="emby-checkbox" class="chkFavoriteChannelsAtTop" />
            <span>\${PlaceFavoriteChannelsAtBeginning}</span>
        </label>
        <h3 class="checkboxListLabel">\${ShowIndicatorsFor}</h3>
        <div class="checkboxList">
            <label>
                <input type="checkbox" is="emby-checkbox" class="chkIndicator" data-type="hd" />
                <span>\${HDPrograms}</span>
            </label>
            <label>
                <input type="checkbox" is="emby-checkbox" class="chkIndicator" data-type="live" data-default="true" />
                <span>\${LiveBroadcasts}</span>
            </label>
            <label>
                <input type="checkbox" is="emby-checkbox" class="chkIndicator" data-type="new" />
                <span>\${NewEpisodes}</span>
            </label>
            <label>
                <input type="checkbox" is="emby-checkbox" class="chkIndicator" data-type="premiere" data-default="true" />
                <span>\${Premieres}</span>
            </label>
            <label>
                <input type="checkbox" is="emby-checkbox" class="chkIndicator" data-type="repeat" />
                <span>\${RepeatEpisodes}</span>
            </label>
        </div>
        <br />
        <label class="checkboxContainer">
            <input type="checkbox" is="emby-checkbox" class="chkColorCodedBackgrounds"/>
            <span>\${EnableColorCodedBackgrounds}</span>
        </label>

        <h3 class="checkboxListLabel">\${Categories}</h3>
       <div class="checkboxList">
            <label>
                <input type="checkbox" is="emby-checkbox" class="chkCategory" data-type="movies" />
                <span>\${Movies}</span>
            </label>
            <label>
                <input type="checkbox" is="emby-checkbox" class="chkCategory" data-type="sports" />
                <span>\${Sports}</span>
            </label>
            <label>
                <input type="checkbox" is="emby-checkbox" class="chkCategory" data-type="kids" />
                <span>\${Kids}</span>
            </label>
            <label>
                <input type="checkbox" is="emby-checkbox" class="chkCategory" data-type="news" />
                <span>\${News}</span>
            </label>
        </div>

    </form>
</div>
`;function u(a,c){const n=[],o=a.querySelectorAll(".chkCategory");for(const t of o){const e=t.getAttribute("data-type");e&&t.checked&&n.push(e)}n.length>=4&&n.push("series"),n.push("all"),c.categories=n}function k(a,c){const n=c.categories||[],o=a.querySelectorAll(".chkCategory");for(const t of o){const e=t.getAttribute("data-type");t.checked=!n.length||(e?n.indexOf(e)!==-1:!1)}}function y(a){const c=a.querySelectorAll(".chkIndicator");for(const o of c){const t=o.getAttribute("data-type");t&&h("guide-indicator-"+t,o.checked)}a.querySelector(".chkColorCodedBackgrounds").checked=s("guide-colorcodedbackgrounds")==="true",a.querySelector(".chkFavoriteChannelsAtTop").checked=s("livetv-favoritechannelsattop")!=="false";const n=a.querySelectorAll(".chkSortOrder");for(const o of n)if(o.checked){h("livetv-channelorder",o.value);break}}function g(a){const c=a.querySelectorAll(".chkIndicator");for(const t of c){const e=t.getAttribute("data-type");e&&(t.getAttribute("data-default")==="true"?t.checked=s("guide-indicator-"+e)!=="false":t.checked=s("guide-indicator-"+e)==="true")}a.querySelector(".chkColorCodedBackgrounds").checked=s("guide-colorcodedbackgrounds")==="true",a.querySelector(".chkFavoriteChannelsAtTop").checked=s("livetv-favoritechannelsattop")!=="false";const n=s("livetv-channelorder")||"Number",o=a.querySelectorAll(".chkSortOrder");for(const t of o)t.checked=t.value===n}function f(a){return new Promise(function(c,n){let o=!1;const t={removeOnClose:!0,scrollY:!1};r.tv?t.size="fullscreen":t.size="small";const e=i.createDialog(t);if(e.classList.add("formDialog"),e.innerHTML=p.translateHtml(b,"core"),e.addEventListener("change",function(){o=!0}),e.addEventListener("close",function(){if(r.tv){const l=e.querySelector(".formDialogContent");l&&d.centerFocus.off(l,!1)}y(e),u(e,a),o?c():n()}),e.querySelector(".btnCancel")?.addEventListener("click",function(){i.close(e)}),r.tv){const l=e.querySelector(".formDialogContent");l&&d.centerFocus.on(l,!1)}g(e),k(e,a),i.open(e).catch(()=>{})})}const B={show:f};export{B as default};
