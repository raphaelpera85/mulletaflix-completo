import{c as l,f as i,bi as c}from"./index-6F8mVyyU.js";import"./emby-select-BS-dUTXm.js";import"./actionSheet-bHfWrycr.js";const d=`<div class="formDialogHeader">
    <button is="paper-icon-button-light" class="btnCancel autoSize" title="\${Previous}" tabindex="-1">
        <span class="material-icons arrow_back" aria-hidden="true"></span>
    </button>
    <h3 class="formDialogHeaderTitle">
        \${HeaderAccessSchedule}
    </h3>
</div>
<div class="formDialogContent scrollY" style="padding-top:2em;">
    <div class="dialogContentInner dialog-content-centered">
        <form class="scheduleForm" style="margin:auto;">

            <div class="selectContainer">
                <select is="emby-select" id="selectDay" label="\${LabelAccessDay}">
                    <option value="Sunday">\${Sunday}</option>
                    <option value="Monday">\${Monday}</option>
                    <option value="Tuesday">\${Tuesday}</option>
                    <option value="Wednesday">\${Wednesday}</option>
                    <option value="Thursday">\${Thursday}</option>
                    <option value="Friday">\${Friday}</option>
                    <option value="Saturday">\${Saturday}</option>
                    <option value="Everyday">\${OptionEveryday}</option>
                    <option value="Weekday">\${OptionWeekdays}</option>
                    <option value="Weekend">\${OptionWeekends}</option>
                </select>
            </div>
            <div class="selectContainer">
                <select is="emby-select" class="selectHour" id="selectStart" label="\${LabelAccessStart}"></select>
            </div>
            <div class="selectContainer">
                <select is="emby-select" class="selectHour" id="selectEnd" label="\${LabelAccessEnd}"></select>
            </div>

            <div class="formDialogFooter">
                <button is="emby-button" type="submit" class="raised button-submit block formDialogFooterItem">
                    <span>\${Add}</span>
                </button>
            </div>
        </form>
    </div>
</div>
`;function s(e){let n=0;const t=e%1;return t&&(n=parseInt(String(60*t),10)),c.getDisplayTime(new Date(2e3,1,1,e,n,0,0))}function u(e){let n="";for(let t=0;t<24;t+=.5)n+=`<option value="${t}">${s(t)}</option>`;n+=`<option value="24">${s(0)}</option>`,e.querySelector("#selectStart").innerHTML=n,e.querySelector("#selectEnd").innerHTML=n}function p(e,{DayOfWeek:n,StartHour:t,EndHour:a}){e.querySelector("#selectDay").value=n||"Sunday",e.querySelector("#selectStart").value=String(t||0),e.querySelector("#selectEnd").value=String(a||0)}function y(e,n){const t={DayOfWeek:e.querySelector("#selectDay").value,StartHour:e.querySelector("#selectStart").value,EndHour:e.querySelector("#selectEnd").value};if(parseFloat(t.StartHour)>=parseFloat(t.EndHour)){alert(i.translate("ErrorStartHourGreaterThanEnd"));return}e.submitted=!0,n.schedule=Object.assign(n.schedule,t),l.close(e)}function v(e){return new Promise((n,t)=>{const a=l.createDialog({removeOnClose:!0,size:"small"});a.classList.add("formDialog");let o="";o+=i.translateHtml(d),a.innerHTML=o,u(a),p(a,e.schedule),l.open(a).catch(t),a.addEventListener("close",()=>{a.submitted?n(e.schedule):t()}),a.querySelector(".btnCancel").addEventListener("click",()=>{l.close(a)}),a.querySelector("form").addEventListener("submit",r=>(y(a,e),r.preventDefault(),!1))})}const f={show:v};export{f as default,v as show};
