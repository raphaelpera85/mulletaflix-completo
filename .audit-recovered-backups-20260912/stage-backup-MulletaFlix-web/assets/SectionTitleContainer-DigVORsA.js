import{j as l}from"./vendor-react-CeUY3tRn.js";import{p as d}from"./vendor-dompurify-Baz99PXY.js";import{B as t,s as $}from"./index-CFwqrzSZ.js";const p=({is:a,id:n,className:o,title:s,icon:r,dataIndex:e,dataTag:i,dataProfileid:c})=>({__html:d.sanitize(`<button
        is="${t(a||"")}"
        type="button"
        ${n}
        class="${t(o||"")}"
        ${s}
        ${e}
        ${i}
        ${c}
    >
        <span class="material-icons ${t(r||"")}" aria-hidden="true"></span>
    </button>`)}),x=({is:a,id:n,className:o,title:s,icon:r,dataIndex:e,dataTag:i,dataProfileid:c,onClick:m})=>{const u=p({is:a,id:n?`id="${t(n)}"`:"",className:o,title:s?`title="${t($.translate(s))}"`:"",icon:r,dataIndex:e||e===0?`data-index="${t(String(e))}"`:"",dataTag:i?`data-tag="${t(String(i))}"`:"",dataProfileid:c?`data-profileid="${t(String(c))}"`:""});return m!==void 0?l.jsx("button",{style:{all:"unset"},dangerouslySetInnerHTML:u,onClick:m}):l.jsx("div",{dangerouslySetInnerHTML:u})},j=({SectionClassName:a,title:n,isBtnVisible:o=!1,btnId:s,btnClassName:r,btnTitle:e,btnIcon:i})=>l.jsxs("div",{className:`${a} sectionTitleContainer flex align-items-center`,children:[l.jsx("h2",{className:"sectionTitle",children:n}),o&&l.jsx(x,{is:"emby-button",id:s,className:r,title:e,icon:i})]});export{x as I,j as S};
