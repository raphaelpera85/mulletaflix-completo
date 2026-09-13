import{j as t}from"./vendor-react-CeUY3tRn.js";import{B as r,s as d,u,q as y}from"./index-CFwqrzSZ.js";import{g as b,N as x,c as $}from"./vendor-jellyfin-Bee54tkY.js";import{u as h,a as C}from"./vendor-react-query-BiAmjtRH.js";import{Q as g}from"./useUser-BxlYo_zZ.js";const j=({labelClassName:s,className:e,id:a,dataFilter:n,dataItemType:c,dataId:i,checkedAttribute:o,renderContent:l})=>({__html:`<label ${s}>
        <input
            is="emby-checkbox"
            type="checkbox"
            class="${r(e||"")}"
            ${a?`id='${r(a.replace(/^id=['"]|['"]$/g,""))}'`:""}
            ${n?`data-filter='${r(n.replace(/^data-filter=['"]|['"]$/g,""))}'`:""}
            ${c?`data-itemtype='${r(c.replace(/^data-itemtype=['"]|['"]$/g,""))}'`:""}
            ${i}
            ${o}
        />
        ${l}
    </label>`}),f=({labelClassName:s,className:e,elementId:a,dataFilter:n,itemType:c,itemId:i,itemCheckedAttribute:o,itemName:l,title:p})=>{const m=l?`<span>${r(l)}</span>`:`<span>${d.translate(p??"")}</span>`;return t.jsx("div",{className:"sectioncheckbox",dangerouslySetInnerHTML:j({labelClassName:s?`class='${r(s)}'`:"",className:e,id:a,dataFilter:n,dataItemType:c,dataId:i?`data-id='${r(i)}'`:"",checkedAttribute:o||"",renderContent:m})})},M=({containerClassName:s,headerTitle:e,checkBoxClassName:a,checkBoxTitle:n,listContainerClassName:c,accessClassName:i,listTitle:o,description:l,children:p})=>t.jsxs("div",{className:s,children:[t.jsx("h2",{children:d.translate(e||"")}),t.jsx(f,{labelClassName:"checkboxContainer",className:a,title:n}),t.jsxs("div",{className:c,children:[t.jsxs("div",{className:i,children:[t.jsx("h3",{className:"checkboxListLabel",children:d.translate(o||"")}),t.jsx("div",{className:"checkboxList paperList",style:{padding:".5em 1em"},children:p})]}),t.jsx("div",{className:"fieldDescription",children:d.translate(l||"")})]})]}),k=async(s,e,a)=>(await b(s).getMediaFolders(e,a)).data,E=s=>{const{api:e}=u();return h({queryKey:["LibraryMediaFolders",e?.basePath],queryFn:({signal:a})=>k(e,s,{signal:a}),enabled:!!e})},L=async(s,e,a)=>(await x(s).getChannels(e,a)).data,P=s=>{const{api:e}=u();return h({queryKey:["Channels",e?.basePath],queryFn:({signal:a})=>L(e,s,{signal:a}),enabled:!!e})},U=()=>{const{api:s}=u();return C({mutationFn:e=>$(s).updateUserPolicy(e),onSuccess:(e,a)=>{y.invalidateQueries({queryKey:[g,s?.basePath,a.userId]})}})};export{M as A,f as C,P as a,U as b,E as u};
