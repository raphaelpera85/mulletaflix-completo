import{e as t,f as $,ai as l,bh as m}from"./index-6F8mVyyU.js";const b=({is:i,id:n,className:o,title:s,icon:r,dataIndex:e,dataTag:a,dataProfileid:c})=>({__html:m.sanitize(`<button
        is="${t(i||"")}"
        type="button"
        ${n}
        class="${t(o||"")}"
        ${s}
        ${e}
        ${a}
        ${c}
    >
        <span class="material-icons ${t(r||"")}" aria-hidden="true"></span>
    </button>`)}),p=({is:i,id:n,className:o,title:s,icon:r,dataIndex:e,dataTag:a,dataProfileid:c,onClick:u})=>{const d=b({is:i,id:n?`id="${t(n)}"`:"",className:o,title:s?`title="${t($.translate(s))}"`:"",icon:r,dataIndex:e||e===0?`data-index="${t(String(e))}"`:"",dataTag:a?`data-tag="${t(String(a))}"`:"",dataProfileid:c?`data-profileid="${t(String(c))}"`:""});return u!==void 0?l.jsx("button",{style:{all:"unset"},dangerouslySetInnerHTML:d,onClick:u}):l.jsx("div",{dangerouslySetInnerHTML:d})},g=({SectionClassName:i,title:n,isBtnVisible:o=!1,btnId:s,btnClassName:r,btnTitle:e,btnIcon:a})=>l.jsxs("div",{className:`${i} sectionTitleContainer flex align-items-center`,children:[l.jsx("h2",{className:"sectionTitle",children:n}),o&&l.jsx(p,{is:"emby-button",id:s,className:r,title:e,icon:a})]});export{p as I,g as S};
