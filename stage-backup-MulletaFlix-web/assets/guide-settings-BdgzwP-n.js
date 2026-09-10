import{l as r,c as i,f as b,bp as d,s as h,m as s}from"./index-6F8mVyyU.js";import"./emby-radio-VFz9KEy2.js";const p=`<div class="formDialogHeader">
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
`;function u(a,o){const n=[],c=a.querySelectorAll(".chkCategory");for(const t of c){const e=t.getAttribute("data-type");e&&t.checked&&n.push(e)}n.length>=4&&n.push("series"),n.push("all"),o.categories=n}function k(a,o){const n=o.categories||[],c=a.querySelectorAll(".chkCategory");for(const t of c){const e=t.getAttribute("data-type");t.checked=!n.length||(e?n.indexOf(e)!==-1:!1)}}function y(a){const o=a.querySelectorAll(".chkIndicator");for(const c of o){const t=c.getAttribute("data-type");t&&h("guide-indicator-"+t,c.checked)}a.querySelector(".chkColorCodedBackgrounds").checked=s("guide-colorcodedbackgrounds")==="true",a.querySelector(".chkFavoriteChannelsAtTop").checked=s("livetv-favoritechannelsattop")!=="false";const n=a.querySelectorAll(".chkSortOrder");for(const c of n)if(c.checked){h("livetv-channelorder",c.value);break}}function f(a){const o=a.querySelectorAll(".chkIndicator");for(const t of o){const e=t.getAttribute("data-type");e&&(t.getAttribute("data-default")==="true"?t.checked=s("guide-indicator-"+e)!=="false":t.checked=s("guide-indicator-"+e)==="true")}a.querySelector(".chkColorCodedBackgrounds").checked=s("guide-colorcodedbackgrounds")==="true",a.querySelector(".chkFavoriteChannelsAtTop").checked=s("livetv-favoritechannelsattop")!=="false";const n=s("livetv-channelorder")||"Number",c=a.querySelectorAll(".chkSortOrder");for(const t of c)t.checked=t.value===n}function g(a){return new Promise(function(o,n){let c=!1;const t={removeOnClose:!0,scrollY:!1};r.tv?t.size="fullscreen":t.size="small";const e=i.createDialog(t);if(e.classList.add("formDialog"),e.innerHTML=b.translateHtml(p,"core"),e.addEventListener("change",function(){c=!0}),e.addEventListener("close",function(){if(r.tv){const l=e.querySelector(".formDialogContent");l&&d.centerFocus.off(l,!1)}y(e),u(e,a),c?o():n()}),e.querySelector(".btnCancel")?.addEventListener("click",function(){i.close(e)}),r.tv){const l=e.querySelector(".formDialogContent");l&&d.centerFocus.on(l,!1)}f(e),k(e,a),i.open(e).catch(()=>{})})}const C={show:g};export{C as default};
