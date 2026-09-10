import{e as h}from"./index-6F8mVyyU.js";const w="mulletaFlix-adsense-script";function I(e){return e.getNamedConfiguration?e.getNamedConfiguration("branding").catch(()=>({})):Promise.resolve({})}function C(e,i){switch(i){case"login":return e.AdSenseShowOnLogin===!0;case"home":return e.AdSenseShowOnHome===!0;default:return e.AdSenseShowAfterIntro!==!1}}function v(e,i){e.parentNode&&e.parentNode.removeChild(e),i.parentNode&&i.parentNode.removeChild(i)}function N(e,i="playback"){return I(e).then(function(a){const f=a.AdSenseClientId,x=a.AdSenseSlotId;return!a.AdSenseEnabled||!f||!x||!C(a,i)||!document.body?Promise.resolve():new Promise(function(m){const d=document.createElement("div");d.className="adsenseInterstitialOverlay",d.innerHTML=`
                <div class="adsenseInterstitialCard">
                    <div class="adsenseInterstitialHeader">Propaganda</div>
                    <div class="adsenseInterstitialBody">
                        <ins class="adsbygoogle"
                            style="display:block;min-width:320px;min-height:250px"
                            data-ad-client="${h(a.AdSenseClientId)}"
                            data-ad-slot="${h(a.AdSenseSlotId)}"
                            data-ad-format="auto"
                            data-full-width-responsive="true"></ins>
                    </div>
                    <div class="adsenseInterstitialStatus"></div>
                    <button type="button" class="btnContinueAdSense" disabled>Continuar</button>
                </div>
            `;const u=document.createElement("style");u.textContent=`
                .adsenseInterstitialOverlay {
                    position: fixed;
                    inset: 0;
                    z-index: 99999;
                    background: rgba(0, 0, 0, 0.92);
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    padding: 24px;
                }

                .adsenseInterstitialCard {
                    width: min(920px, 100%);
                    background: #111;
                    color: #fff;
                    border: 1px solid rgba(255, 255, 255, 0.12);
                    border-radius: 12px;
                    padding: 20px;
                    display: flex;
                    flex-direction: column;
                    gap: 16px;
                    box-shadow: 0 30px 80px rgba(0, 0, 0, 0.5);
                }

                .adsenseInterstitialHeader {
                    font-size: 1.1rem;
                    font-weight: 700;
                }

                .adsenseInterstitialBody {
                    min-height: 280px;
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    background: #000;
                    border-radius: 8px;
                }

                .adsenseInterstitialStatus {
                    min-height: 1.2em;
                    color: rgba(255, 255, 255, 0.72);
                    font-size: 0.95rem;
                }

                .btnContinueAdSense {
                    align-self: flex-end;
                }
            `;const t=d.querySelector(".btnContinueAdSense"),g=d.querySelector(".adsenseInterstitialStatus");if(!t||!g){m();return}let n=null,s=null;const y=function(){n!==null&&(window.clearInterval(n),n=null),s!==null&&(window.clearTimeout(s),s=null),v(d,u),m()},b=function(l){g.textContent=l,n!==null&&(window.clearInterval(n),n=null),s!==null&&(window.clearTimeout(s),s=null),t.disabled=!1,t.textContent="Continuar"},p=Math.max(0,Number(a.AdSenseHoldSeconds||8));let r=p;const c=function(){t.textContent=r>0?`Continuar em ${r}s`:"Continuar"};document.head.appendChild(u),document.body.appendChild(d),c(),n=window.setInterval(function(){r-=1,r<=0&&(r=0,n!==null&&(window.clearInterval(n),n=null),t.disabled=!1),c()},1e3),p===0?(t.disabled=!1,c()):s=window.setTimeout(function(){r=0,t.disabled=!1,c()},p*1e3),t.addEventListener("click",y,{once:!0});const S=function(){try{const l=window.adsbygoogle||[];window.adsbygoogle=l,l.push({})}catch(l){console.warn("AdSense interstitial failed to render",l),b("Anuncio indisponivel. Voce pode continuar.")}};if(document.getElementById(w)){S();return}const o=document.createElement("script");o.id=w,o.async=!0,o.crossOrigin="anonymous",o.src=`https://pagead2.googlesyndication.com/pagead/js/adsbygoogle.js?client=${encodeURIComponent(f)}`,o.onload=S,o.onerror=function(){console.warn("AdSense script failed to load"),b("Falha ao carregar a propaganda. Voce pode continuar.")},document.head.appendChild(o)})})}export{N as s};
