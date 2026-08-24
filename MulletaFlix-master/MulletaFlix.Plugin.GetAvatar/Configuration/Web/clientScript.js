(function () {
    'use strict';

    const apiBase = '/GetAvatar';
    let avatars = [];
    let selectedId = null;
    let targetUserId = null;

    function url(path) {
        return window.ApiClient?.getUrl ? window.ApiClient.getUrl(apiBase + path) : apiBase + path;
    }

    function headers(json) {
        const result = {};
        if (json) result['Content-Type'] = 'application/json';
        if (window.ApiClient?.accessToken) result['X-Emby-Token'] = window.ApiClient.accessToken();
        return result;
    }

    function escapeHtml(value) {
        const node = document.createElement('div');
        node.textContent = value == null ? '' : String(value);
        return node.innerHTML;
    }

    function ensureUi() {
        if (!document.getElementById('getavatar-styles')) {
            const style = document.createElement('style');
            style.id = 'getavatar-styles';
            style.textContent = '#getavatar-modal{position:fixed;inset:0;z-index:10000;display:none;align-items:center;justify-content:center;background:rgba(0,0,0,.72)}#getavatar-dialog{width:min(920px,94vw);max-height:88vh;display:flex;flex-direction:column;overflow:hidden;background:#181818;color:#fff;border-radius:8px}#getavatar-header,#getavatar-actions{display:flex;align-items:center;gap:.75rem;padding:1rem}#getavatar-header{justify-content:space-between;border-bottom:1px solid rgba(255,255,255,.15)}#getavatar-actions{justify-content:flex-end;border-top:1px solid rgba(255,255,255,.15)}#getavatar-grid,.getavatar-inline-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(105px,1fr));gap:.8rem;padding:1rem;overflow-y:auto}.getavatar-option{border:2px solid transparent;border-radius:7px;padding:.35rem;background:rgba(255,255,255,.04);color:inherit;cursor:pointer}.getavatar-option.selected{border-color:#52b54b;background:rgba(82,181,75,.18)}.getavatar-option img{width:100%;aspect-ratio:1;object-fit:cover;border-radius:5px;display:block}.getavatar-option span{display:block;margin-top:.35rem;font-size:.78rem;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}.getavatar-profile-button{margin-top:1rem}';
            document.head.appendChild(style);
        }
        if (document.getElementById('getavatar-modal')) return;
        const modal = document.createElement('div');
        modal.id = 'getavatar-modal';
        modal.innerHTML = '<div id="getavatar-dialog" role="dialog" aria-modal="true"><div id="getavatar-header"><h2>Escolha seu avatar</h2><button type="button" id="getavatar-close">Fechar</button></div><div id="getavatar-grid">Carregando...</div><div id="getavatar-actions"><button type="button" id="getavatar-random-button">Avatar aleatório</button><button type="button" id="getavatar-apply-button" disabled>Usar selecionado</button></div></div>';
        document.body.appendChild(modal);
        document.getElementById('getavatar-close').onclick = closeModal;
        modal.onclick = function (event) { if (event.target === modal) closeModal(); };
        document.getElementById('getavatar-random-button').onclick = applyRandom;
        document.getElementById('getavatar-apply-button').onclick = applySelected;
    }

    async function loadAvatars() {
        const response = await fetch(url('/Avatars'), { headers: headers(false) });
        if (!response.ok) throw new Error('HTTP ' + response.status);
        avatars = await response.json();
    }

    function renderGrid(container, applyImmediately) {
        if (!avatars.length) {
            container.innerHTML = '<p>Nenhum avatar disponível.</p>';
            return;
        }
        container.innerHTML = avatars.map(function (avatar) {
            const id = avatar.Id || avatar.id;
            const name = avatar.Name || avatar.name || 'Avatar';
            return '<button type="button" class="getavatar-option" data-id="' + escapeHtml(id) + '"><img loading="lazy" src="' + escapeHtml(url('/Image/' + encodeURIComponent(id))) + '" alt="' + escapeHtml(name) + '"><span>' + escapeHtml(name) + '</span></button>';
        }).join('');
        container.querySelectorAll('.getavatar-option').forEach(function (button) {
            button.onclick = async function () {
                container.querySelectorAll('.getavatar-option').forEach(function (item) { item.classList.remove('selected'); });
                button.classList.add('selected');
                selectedId = button.dataset.id;
                document.getElementById('getavatar-apply-button').disabled = false;
                if (applyImmediately) await setAvatar(selectedId);
            };
        });
    }

    async function setAvatar(avatarId) {
        const body = { avatarId: avatarId };
        if (targetUserId) body.userId = targetUserId;
        const response = await fetch(url('/SetAvatar'), { method: 'POST', headers: headers(true), body: JSON.stringify(body) });
        if (!response.ok) throw new Error('HTTP ' + response.status);
    }

    function closeModal() {
        document.getElementById('getavatar-modal').style.display = 'none';
        document.body.style.overflow = '';
        selectedId = null;
    }

    async function openModal() {
        ensureUi();
        const modal = document.getElementById('getavatar-modal');
        const grid = document.getElementById('getavatar-grid');
        modal.style.display = 'flex';
        document.body.style.overflow = 'hidden';
        grid.textContent = 'Carregando...';
        try {
            await loadAvatars();
            renderGrid(grid, false);
        } catch (error) {
            grid.textContent = 'Falha ao carregar avatares: ' + error.message;
        }
    }

    async function applySelected() {
        if (!selectedId) return;
        try { await setAvatar(selectedId); window.location.reload(); }
        catch (error) { window.alert('Falha ao aplicar avatar: ' + error.message); }
    }

    async function applyRandom() {
        try {
            if (!avatars.length) await loadAvatars();
            if (!avatars.length) throw new Error('Nenhum avatar disponível');
            const avatar = avatars[Math.floor(Math.random() * avatars.length)];
            await setAvatar(avatar.Id || avatar.id);
            window.location.reload();
        } catch (error) { window.alert('Falha ao aplicar avatar aleatório: ' + error.message); }
    }

    function findUserId() {
        const hash = window.location.hash || '';
        const query = hash.split('?')[1] || '';
        const hashId = new URLSearchParams(query).get('userId');
        if (hashId) return hashId;
        // React hash router: #/dashboard/users/{userId}/{tab}
        const hashPath = hash.replace(/^#\/?/, '');
        const hashMatch = hashPath.match(/dashboard\/users\/([^/?]+)/i);
        if (hashMatch && hashMatch[1]) return hashMatch[1];
        const path = window.location.pathname.match(/\/dashboard\/users\/([^/]+)/i);
        return (path && path[1]) || null;
    }

    function injectButton() {
        if (document.getElementById('getavatar-gallery-button')) return;
        const hash = window.location.hash || '';
        const hashPath = hash.replace(/^#\/?/, '');
        const pathname = window.location.pathname || '';
        const isProfilePage = hash.includes('userprofile')
            || /dashboard\/users\/[^/?]+\/profile/i.test(hashPath)
            || /\/dashboard\/users\/[^/]+\/profile/i.test(pathname);
        if (!isProfilePage) return;
        const target = document.querySelector('.selectImageContainer, #btnDeleteImage, .lnkEditUserPreferencesContainer, .editUserProfileForm');
        if (!target) return;
        targetUserId = findUserId();
        const button = document.createElement('button');
        button.type = 'button';
        button.id = 'getavatar-gallery-button';
        button.setAttribute('is', 'emby-button');
        button.className = 'raised button-alt block getavatar-profile-button';
        button.textContent = 'Escolher avatar da galeria';
        button.onclick = openModal;
        if (target.id === 'btnDeleteImage' && target.parentElement) target.parentElement.appendChild(button);
        else target.appendChild(button);
    }

    async function initStandalone() {
        const grid = document.getElementById('getavatar-user-grid');
        if (!grid) return;
        try { await loadAvatars(); renderGrid(grid, true); }
        catch (error) { grid.textContent = 'Falha ao carregar avatares: ' + error.message; }
    }

    function init() {
        ensureUi();
        injectButton();
        initStandalone();
        window.addEventListener('hashchange', injectButton);
        document.addEventListener('viewshow', injectButton);
        new MutationObserver(injectButton).observe(document.body, { childList: true, subtree: true });
    }

    function wait() { if (window.ApiClient) init(); else window.setTimeout(wait, 100); }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', wait); else wait();
}());
