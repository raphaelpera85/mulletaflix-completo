(function () {
    'use strict';
    const page = document.getElementById('GetAvatarConfigPage');
    if (!page) return;
    function headers(json) { const value = { 'X-Emby-Token': ApiClient.accessToken() }; if (json) value['Content-Type'] = 'application/json'; return value; }
    function url(path) { return ApiClient.getUrl('/GetAvatar' + path); }
    function escapeHtml(value) { const node = document.createElement('div'); node.textContent = value == null ? '' : String(value); return node.innerHTML; }
    async function loadSettings() {
        const response = await fetch(url('/Settings'), { headers: headers(false) });
        if (!response.ok) throw new Error('HTTP ' + response.status);
        const settings = await response.json();
        page.querySelector('#enableAutoAssign').checked = Boolean(settings.enableAutoAssign);
    }
    async function saveSettings() {
        const response = await fetch(url('/Settings'), { method: 'POST', headers: headers(true), body: JSON.stringify({ enableAutoAssign: page.querySelector('#enableAutoAssign').checked }) });
        if (!response.ok) throw new Error('HTTP ' + response.status);
    }
    async function loadAvatars() {
        const response = await fetch(url('/Avatars'), { headers: headers(false) });
        if (!response.ok) throw new Error('HTTP ' + response.status);
        const avatars = await response.json();
        page.querySelector('#avatarCount').textContent = '(' + avatars.length + ')';
        page.querySelector('#avatarList').innerHTML = '<div style="display:grid;grid-template-columns:repeat(auto-fill,minmax(110px,1fr));gap:1em">' + avatars.map(function (avatar) {
            const id = avatar.Id || avatar.id; const name = avatar.Name || avatar.name || 'Avatar';
            return '<div style="text-align:center"><img loading="lazy" style="width:96px;height:96px;object-fit:cover;border-radius:6px" src="' + escapeHtml(url('/Image/' + encodeURIComponent(id))) + '" alt="' + escapeHtml(name) + '"><div>' + escapeHtml(name) + '</div><button type="button" class="getavatar-delete" data-id="' + escapeHtml(id) + '">Remover</button></div>';
        }).join('') + '</div>';
        page.querySelectorAll('.getavatar-delete').forEach(function (button) { button.onclick = async function () { if (!window.confirm('Remover este avatar?')) return; const result = await fetch(url('/Delete/' + encodeURIComponent(button.dataset.id)), { method: 'DELETE', headers: headers(false) }); if (!result.ok) throw new Error('HTTP ' + result.status); await loadAvatars(); }; });
    }
    async function upload() {
        const input = page.querySelector('#avatarFileInput'); const category = page.querySelector('#avatarCategory').value.trim();
        for (const file of input.files) { const form = new FormData(); form.append('file', file); const response = await fetch(url('/Upload?category=' + encodeURIComponent(category)), { method: 'POST', headers: headers(false), body: form }); if (!response.ok) throw new Error('HTTP ' + response.status + ': ' + file.name); }
        input.value = ''; await loadAvatars();
    }
    page.querySelector('#enableAutoAssign').onchange = function () { saveSettings().catch(function (error) { window.alert('Falha ao salvar: ' + error.message); }); };
    page.querySelector('#uploadButton').onclick = function () { upload().catch(function (error) { window.alert('Falha ao enviar: ' + error.message); }); };
    Promise.all([loadSettings(), loadAvatars()]).catch(function (error) { page.querySelector('#avatarList').textContent = 'Falha ao carregar: ' + error.message; });
}());
