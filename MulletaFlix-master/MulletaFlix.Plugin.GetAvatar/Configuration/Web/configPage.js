(function () {
    'use strict';
    const page = document.getElementById('GetAvatarConfigPage');
    if (!page) return;

    function getClient() {
        return window.ApiClient || (typeof ApiClient !== 'undefined' ? ApiClient : null);
    }

    function headers(json) {
        const value = { 'Accept': 'application/json' };
        if (json) value['Content-Type'] = 'application/json';
        const client = getClient();
        if (client && client.accessToken && client.accessToken()) {
            const token = client.accessToken();
            value['X-Emby-Token'] = token;
            value['X-MediaBrowser-Token'] = token;
            const deviceId = client.deviceId ? client.deviceId() : '';
            const version = client.appVersion ? client.appVersion() : '1.0.0';
            const appName = client.appName ? client.appName() : 'MulletaFlix Web';
            value['Authorization'] = 'MediaBrowser Client="' + appName + '", Device="Browser", DeviceId="' + deviceId + '", Version="' + version + '", Token="' + token + '"';
        }
        return value;
    }

    function url(path) {
        const client = getClient();
        let fullPath = '/GetAvatar' + path;
        const token = (client && client.accessToken && client.accessToken()) || '';
        if (token) {
            const sep = fullPath.indexOf('?') === -1 ? '?' : '&';
            fullPath += sep + 'api_key=' + encodeURIComponent(token);
        }
        return client && client.getUrl ? client.getUrl(fullPath) : fullPath;
    }

    function escapeHtml(value) {
        const node = document.createElement('div');
        node.textContent = value == null ? '' : String(value);
        return node.innerHTML;
    }

    async function loadSettings() {
        const response = await fetch(url('/Settings'), { headers: headers(false) });
        if (!response.ok) throw new Error('HTTP ' + response.status);
        const settings = await response.json();
        page.querySelector('#enableAutoAssign').checked = Boolean(settings.enableAutoAssign);
    }

    async function saveSettings() {
        const response = await fetch(url('/Settings'), {
            method: 'POST',
            headers: headers(true),
            body: JSON.stringify({ enableAutoAssign: page.querySelector('#enableAutoAssign').checked })
        });
        if (!response.ok) throw new Error('HTTP ' + response.status);
    }

    async function loadAvatars() {
        const response = await fetch(url('/Avatars'), { headers: headers(false) });
        if (!response.ok) throw new Error('HTTP ' + response.status);
        const avatars = await response.json();
        page.querySelector('#avatarCount').textContent = '(' + avatars.length + ')';
        if (!avatars.length) {
            page.querySelector('#avatarList').innerHTML = '<div style="color:#a6adc8;font-style:italic">Nenhum avatar cadastrado na coleção.</div>';
            return;
        }
        page.querySelector('#avatarList').innerHTML = '<div style="display:grid;grid-template-columns:repeat(auto-fill,minmax(110px,1fr));gap:1em">' + avatars.map(function (avatar) {
            const id = avatar.Id || avatar.id;
            const name = avatar.Name || avatar.name || 'Avatar';
            return '<div style="text-align:center"><img loading="lazy" style="width:96px;height:96px;object-fit:cover;border-radius:6px;border:1px solid rgba(255,255,255,0.1)" src="' + escapeHtml(url('/Image/' + encodeURIComponent(id))) + '" alt="' + escapeHtml(name) + '"><div style="margin-top:0.35em;font-size:0.85em;overflow:hidden;text-overflow:ellipsis;white-space:nowrap">' + escapeHtml(name) + '</div><button type="button" class="getavatar-delete raised" style="margin-top:0.35em;padding:0.2em 0.6em;font-size:0.75em" data-id="' + escapeHtml(id) + '">Remover</button></div>';
        }).join('') + '</div>';

        page.querySelectorAll('.getavatar-delete').forEach(function (button) {
            button.onclick = async function () {
                if (!window.confirm('Remover este avatar?')) return;
                const result = await fetch(url('/Delete/' + encodeURIComponent(button.dataset.id)), {
                    method: 'DELETE',
                    headers: headers(false)
                });
                if (!result.ok) throw new Error('HTTP ' + result.status);
                await loadAvatars();
            };
        });
    }

    async function upload() {
        const input = page.querySelector('#avatarFileInput');
        const category = page.querySelector('#avatarCategory').value.trim();
        if (!input.files || input.files.length === 0) {
            window.alert('Selecione pelo menos um arquivo de imagem para enviar.');
            return;
        }
        for (const file of input.files) {
            const form = new FormData();
            form.append('file', file);
            const uploadHeaders = headers(false);
            delete uploadHeaders['Content-Type']; // Let browser set multipart boundary
            const response = await fetch(url('/Upload?category=' + encodeURIComponent(category)), {
                method: 'POST',
                headers: uploadHeaders,
                body: form
            });
            if (!response.ok) throw new Error('HTTP ' + response.status + ': ' + file.name);
        }
        input.value = '';
        await loadAvatars();
    }

    page.querySelector('#enableAutoAssign').onchange = function () {
        saveSettings().catch(function (error) { window.alert('Falha ao salvar: ' + error.message); });
    };

    page.querySelector('#uploadButton').onclick = function () {
        upload().catch(function (error) { window.alert('Falha ao enviar: ' + error.message); });
    };

    Promise.all([loadSettings(), loadAvatars()]).catch(function (error) {
        page.querySelector('#avatarList').textContent = 'Falha ao carregar: ' + error.message;
    });
}());
