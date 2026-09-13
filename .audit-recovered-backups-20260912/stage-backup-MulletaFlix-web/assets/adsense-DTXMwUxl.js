import{B as S}from"./index-CFwqrzSZ.js";const w="mulletaFlix-adsense-script";function I(e){return e.getNamedConfiguration?e.getNamedConfiguration("branding").then(n=>n&&typeof n=="object"?n:{}).catch(()=>({})):Promise.resolve({})}function C(e,n){switch(n){case"login":return e.AdSenseShowOnLogin===!0;case"home":return e.AdSenseShowOnHome===!0;default:return e.AdSenseShowAfterIntro!==!1}}function v(e,n){e.parentNode&&e.parentNode.removeChild(e),n.parentNode&&n.parentNode.removeChild(n)}function N(e,n="playback"){return I(e).then(function(o){const f=o.AdSenseClientId,x=o.AdSenseSlotId;return!o.AdSenseEnabled||!f||!x||!C(o,n)||!document.body?Promise.resolve():new Promise(function(m){const d=document.createElement("div");d.className="adsenseInterstitialOverlay",d.innerHTML=`
                <div class="adsenseInterstitialCard">
                    <div class="adsenseInterstitialHeader">Propaganda</div>
                    <div class="adsenseInterstitialBody">
                        <ins class="adsbygoogle"
                            style="display:block;min-width:320px;min-height:250px"
                            data-ad-client="${S(o.AdSenseClientId)}"
                            data-ad-slot="${S(o.AdSenseSlotId)}"
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
            `;const i=d.querySelector(".btnContinueAdSense"),g=d.querySelector(".adsenseInterstitialStatus");if(!i||!g){m();return}let t=null,s=null;const y=function(){t!==null&&(window.clearInterval(t),t=null),s!==null&&(window.clearTimeout(s),s=null),v(d,u),m()},b=function(l){g.textContent=l,t!==null&&(window.clearInterval(t),t=null),s!==null&&(window.clearTimeout(s),s=null),i.disabled=!1,i.textContent="Continuar"},p=Math.max(0,Number(o.AdSenseHoldSeconds||8));let r=p;const c=function(){i.textContent=r>0?`Continuar em ${r}s`:"Continuar"};document.head.appendChild(u),document.body.appendChild(d),c(),t=window.setInterval(function(){r-=1,r<=0&&(r=0,t!==null&&(window.clearInterval(t),t=null),i.disabled=!1),c()},1e3),p===0?(i.disabled=!1,c()):s=window.setTimeout(function(){r=0,i.disabled=!1,c()},p*1e3),i.addEventListener("click",y,{once:!0});const h=function(){try{const l=window.adsbygoogle||[];window.adsbygoogle=l,l.push({})}catch(l){console.warn("AdSense interstitial failed to render",l),b("Anuncio indisponivel. Voce pode continuar.")}};if(document.getElementById(w)){h();return}const a=document.createElement("script");a.id=w,a.async=!0,a.crossOrigin="anonymous",a.src=`https://pagead2.googlesyndication.com/pagead/js/adsbygoogle.js?client=${encodeURIComponent(f)}`,a.onload=h,a.onerror=function(){console.warn("AdSense script failed to load"),b("Falha ao carregar a propaganda. Voce pode continuar.")},document.head.appendChild(a)})})}export{N as s};
