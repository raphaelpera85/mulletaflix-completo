export default function (view, params) {
    'use strict';

    var pollInterval = null;
    var queueSummaryInterval = null;
    var serverLogOffset = 0;
    var downloaderLogOffset = 0;
    var currentConfig = null;
    var currentBots = [];
    var mongoPendingUploadCount = null;
    var currentActiveUploads = [];

    function apiRequest(method, path, body) {
        var url = ApiClient.getUrl(path);
        var headers = {
            'Accept': 'application/json'
        };
        var token = (ApiClient.accessToken && ApiClient.accessToken()) || '';
        if (token) {
            headers['X-Emby-Token'] = token;
            headers['Authorization'] = 'MediaBrowser Client="MulletaFlix Web", Device="Browser", DeviceId="' + (ApiClient.deviceId ? ApiClient.deviceId() : '') + '", Version="' + (ApiClient.appVersion ? ApiClient.appVersion() : '1.0.0') + '", Token="' + token + '"';
        }
        var options = {
            method: method,
            headers: headers
        };
        if (body !== undefined && body !== null) {
            headers['Content-Type'] = 'application/json';
            options.body = JSON.stringify(body);
        }
        return fetch(url, options).then(function (res) {
            if (!res.ok) {
                return res.text().then(function (txt) {
                    throw new Error(txt || res.statusText);
                });
            }
            var contentType = res.headers.get('content-type');
            if (contentType && contentType.indexOf('application/json') !== -1) {
                return res.json();
            }
            return res.text();
        });
    }

    // Tab switching
    view.querySelectorAll('.nebula-tab-btn').forEach(function (btn) {
        btn.onclick = function (e) {
            e.preventDefault();
            view.querySelectorAll('.nebula-tab-btn').forEach(function (b) { b.classList.remove('active'); });
            view.querySelectorAll('.nebula-tab-content').forEach(function (c) { c.classList.remove('active'); });
            btn.classList.add('active');
            var targetId = btn.getAttribute('data-tab');
            var target = view.querySelector('#' + targetId);
            if (target) {
                target.classList.add('active');
            }
        };
    });

    var quickBots = view.querySelector('#lblQuickBotsLink');
    if (quickBots) {
        quickBots.onclick = function (e) {
            e.preventDefault();
            var tabBtn = view.querySelector('.nebula-tab-btn[data-tab="tabBots"]');
            if (tabBtn) tabBtn.click();
        };
    }

    // Settings drawer
    var btnToggle = view.querySelector('#btnToggleSettings');
    var drawer = view.querySelector('#settingsDrawer');
    var btnClose = view.querySelector('#btnCloseSettings');

    if (btnToggle && drawer) {
        btnToggle.onclick = function (e) { e.preventDefault(); drawer.classList.toggle('open'); };
    }
    if (btnClose && drawer) {
        btnClose.onclick = function (e) { e.preventDefault(); drawer.classList.remove('open'); };
    }

    // Save settings button
    var btnSave = view.querySelector('#btnSaveConfig');
    if (btnSave) {
        btnSave.onclick = function (e) { e.preventDefault(); saveConfiguration(false); };
    }

    // Fast save on change
    ['#chkTurboMode', '#spinTurboMinutes', '#spinDownloadParts'].forEach(function (sel) {
        var el = view.querySelector(sel);
        if (el) el.onchange = function () { saveConfiguration(true); };
    });

    // Add/Remove Monitor Paths
    var btnAddMon = view.querySelector('#btnAddMonitorPath');
    if (btnAddMon) {
        btnAddMon.onclick = function (e) {
            e.preventDefault();
            var input = view.querySelector('#inputMonitorPath');
            var val = input ? input.value.trim() : '';
            if (val) {
                var sel = view.querySelector('#listMonitorPaths');
                if (sel) {
                    var opt = document.createElement('option');
                    opt.value = val;
                    opt.textContent = val;
                    sel.appendChild(opt);
                    input.value = '';
                    saveConfiguration(true);
                }
            }
        };
    }

    var btnRemMon = view.querySelector('#btnRemoveMonitorPath');
    if (btnRemMon) {
        btnRemMon.onclick = function (e) {
            e.preventDefault();
            var sel = view.querySelector('#listMonitorPaths');
            if (sel && sel.selectedIndex >= 0) {
                sel.remove(sel.selectedIndex);
                saveConfiguration(true);
            }
        };
    }

    // Directory Browser Helper
    function openFolderPicker(targetInput, headerTitle) {
        if (window.Dashboard && window.Dashboard.DirectoryBrowser) {
            try {
                var picker = new window.Dashboard.DirectoryBrowser();
                picker.show({
                    path: targetInput ? targetInput.value : '',
                    validateWriteable: false,
                    header: headerTitle || 'Selecionar Pasta',
                    instruction: 'Navegue pelas unidades do sistema e selecione a pasta:',
                    callback: function (path) {
                        if (path && targetInput) {
                            targetInput.value = path;
                        }
                        picker.close();
                    }
                });
                return;
            } catch (err) {
                console.warn('Dashboard.DirectoryBrowser falhou:', err);
            }
        }

        var current = targetInput ? targetInput.value : '';
        var chosen = prompt(headerTitle || 'Digite ou cole o caminho completo da pasta:', current);
        if (chosen && targetInput) {
            targetInput.value = chosen.trim();
        }
    }

    var btnBrowseStg = view.querySelector('#btnBrowseStagePath');
    if (btnBrowseStg) {
        btnBrowseStg.onclick = function (e) {
            e.preventDefault();
            var input = view.querySelector('#inputStagePath');
            openFolderPicker(input, 'Selecionar Pasta de Stage (Download)');
        };
    }

    var btnBrowseMon = view.querySelector('#btnBrowseMonitorPath');
    if (btnBrowseMon) {
        btnBrowseMon.onclick = function (e) {
            e.preventDefault();
            var input = view.querySelector('#inputMonitorPath');
            openFolderPicker(input, 'Selecionar Pasta de Monitoramento (.strm)');
        };
    }


    // Add/Remove Stage Paths
    var btnAddStg = view.querySelector('#btnAddStagePath');
    if (btnAddStg) {
        btnAddStg.onclick = function (e) {
            e.preventDefault();
            var input = view.querySelector('#inputStagePath');
            var val = input ? input.value.trim() : '';
            if (val) {
                var sel = view.querySelector('#listStagePaths');
                if (sel) {
                    var opt = document.createElement('option');
                    opt.value = val;
                    opt.textContent = val;
                    sel.appendChild(opt);
                    input.value = '';
                    saveConfiguration(true);
                }
            }
        };
    }

    var btnRemStg = view.querySelector('#btnRemoveStagePath');
    if (btnRemStg) {
        btnRemStg.onclick = function (e) {
            e.preventDefault();
            var sel = view.querySelector('#listStagePaths');
            if (sel && sel.selectedIndex >= 0) {
                sel.remove(sel.selectedIndex);
                saveConfiguration(true);
            }
        };
    }

    // Action buttons
    var btnStartEnv = view.querySelector('#btnStartEnvio');
    if (btnStartEnv) {
        btnStartEnv.onclick = function (e) {
            e.preventDefault();
            var badge = view.querySelector('#globalStatusBadge');
            if (badge) badge.textContent = 'Iniciando servidor de envio...';
            apiRequest('POST', 'NebulaFtp/Actions/StartEnvio', { streamOnly: false })
                .then(function () { pollStatusAndLogs(); })
                .catch(function (err) { alert('Erro ao iniciar envio: ' + (err.message || err)); });
        };
    }

    var btnStartStream = view.querySelector('#btnStartStreamOnly');
    if (btnStartStream) {
        btnStartStream.onclick = function (e) {
            e.preventDefault();
            var badge = view.querySelector('#globalStatusBadge');
            if (badge) badge.textContent = 'Iniciando streaming...';
            apiRequest('POST', 'NebulaFtp/Actions/StartEnvio', { streamOnly: true })
                .then(function () { pollStatusAndLogs(); })
                .catch(function (err) { alert('Erro ao iniciar streaming: ' + (err.message || err)); });
        };
    }

    var btnStopEnv = view.querySelector('#btnStopEnvio');
    if (btnStopEnv) {
        btnStopEnv.onclick = function (e) {
            e.preventDefault();
            apiRequest('POST', 'NebulaFtp/Actions/StopEnvio')
                .then(function () { pollStatusAndLogs(); })
                .catch(function (err) { alert('Erro ao parar envio: ' + (err.message || err)); });
        };
    }

    var btnMountN = view.querySelector('#btnMountDriveN');
    if (btnMountN) {
        btnMountN.onclick = function (e) {
            e.preventDefault();
            btnMountN.disabled = true;
            apiRequest('POST', 'NebulaFtp/Actions/MountDriveN')
                .then(function () { pollStatusAndLogs(); })
                .catch(function (err) { alert('Erro ao montar disco N: ' + (err.message || err)); })
                .finally(function () { btnMountN.disabled = false; });
        };
    }

    var btnUnmountN = view.querySelector('#btnUnmountDriveN');
    if (btnUnmountN) {
        btnUnmountN.onclick = function (e) {
            e.preventDefault();
            btnUnmountN.disabled = true;
            apiRequest('POST', 'NebulaFtp/Actions/UnmountDriveN')
                .then(function () { pollStatusAndLogs(); })
                .catch(function (err) { alert('Erro ao desmontar disco N: ' + (err.message || err)); })
                .finally(function () { btnUnmountN.disabled = false; });
        };
    }

    var btnGenStrm = view.querySelector('#btnGenerateStrm');
    if (btnGenStrm) {
        btnGenStrm.onclick = function (e) {
            e.preventDefault();
            apiRequest('POST', 'NebulaFtp/Actions/GenerateStrm')
                .then(function () {
                    if (typeof Dashboard !== 'undefined' && Dashboard.alert) {
                        Dashboard.alert('Geração de biblioteca STRM solicitada com sucesso!');
                    } else {
                        alert('Geração de biblioteca STRM solicitada com sucesso!');
                    }
                })
                .catch(function (err) { alert('Erro ao gerar STRM: ' + (err.message || err)); });
        };
    }

    var btnStartDl = view.querySelector('#btnStartDownloader');
    if (btnStartDl) {
        btnStartDl.onclick = function (e) {
            e.preventDefault();
            apiRequest('POST', 'NebulaFtp/Actions/StartDownloader')
                .then(function () { pollStatusAndLogs(); })
                .catch(function (err) { alert('Erro ao iniciar downloader: ' + (err.message || err)); });
        };
    }

    var btnStopDl = view.querySelector('#btnStopDownloader');
    if (btnStopDl) {
        btnStopDl.onclick = function (e) {
            e.preventDefault();
            apiRequest('POST', 'NebulaFtp/Actions/StopDownloader')
                .then(function () { pollStatusAndLogs(); })
                .catch(function (err) { alert('Erro ao parar downloader: ' + (err.message || err)); });
        };
    }

    var btnPrune = view.querySelector('#btnPruneCompleted');
    if (btnPrune) {
        btnPrune.onclick = function (e) {
            e.preventDefault();
            apiRequest('POST', 'NebulaFtp/Actions/PruneCompleted')
                .then(function () {
                    if (typeof Dashboard !== 'undefined' && Dashboard.alert) {
                        Dashboard.alert('Limpeza de STRMs concluídos executada.');
                    } else {
                        alert('Limpeza de STRMs concluídos executada.');
                    }
                })
                .catch(function (err) { alert('Erro ao limpar STRMs: ' + (err.message || err)); });
        };
    }

    var btnClearSrv = view.querySelector('#btnClearServerLogs');
    if (btnClearSrv) {
        btnClearSrv.onclick = function (e) {
            e.preventDefault();
            var sTerm = view.querySelector('#serverLogTerminal');
            if (sTerm) sTerm.textContent = '';
        };
    }

    var btnClearDl = view.querySelector('#btnClearDownloaderLogs');
    if (btnClearDl) {
        btnClearDl.onclick = function (e) {
            e.preventDefault();
            var dTerm = view.querySelector('#downloaderLogTerminal');
            if (dTerm) dTerm.textContent = '';
        };
    }

    // Bots buttons
    var btnOpenAddBot = view.querySelector('#btnOpenAddBotModal');
    if (btnOpenAddBot) {
        btnOpenAddBot.onclick = function (e) { e.preventDefault(); openBotModal(null); };
    }

    var btnCancelBot = view.querySelector('#btnCancelBotModal');
    if (btnCancelBot) {
        btnCancelBot.onclick = function (e) { e.preventDefault(); closeBotModal(); };
    }

    var btnSaveBot = view.querySelector('#btnSaveBotModal');
    if (btnSaveBot) {
        btnSaveBot.onclick = function (e) {
            e.preventDefault();
            var indexVal = (view.querySelector('#modalBotIndex') || {}).value;
            var inputToken = view.querySelector('#inputModalBotToken');
            var tokenVal = inputToken ? inputToken.value.trim() : '';
            if (!tokenVal) { alert('Por favor, informe o Token do Bot.'); return; }

            var req = { Index: indexVal ? parseInt(indexVal, 10) : null, Token: tokenVal };
            apiRequest('POST', 'NebulaFtp/Bots', req)
                .then(function (updatedBots) {
                    currentBots = updatedBots || [];
                    renderBotsGrid(currentBots);
                    closeBotModal();
                    loadConfiguration();
                })
                .catch(function (err) { alert('Erro ao salvar bot: ' + (err.message || err)); });
        };
    }

    var btnSyncBots = view.querySelector('#btnSyncBotsEnv');
    if (btnSyncBots) {
        btnSyncBots.onclick = function (e) {
            e.preventDefault();
            apiRequest('POST', 'NebulaFtp/Bots/Sync')
                .then(function (updatedBots) {
                    currentBots = updatedBots || [];
                    renderBotsGrid(currentBots);
                    loadConfiguration();
                    if (typeof Dashboard !== 'undefined' && Dashboard.alert) {
                        Dashboard.alert('Sincronizados ' + currentBots.length + ' bots com sucesso a partir do .env!');
                    } else {
                        alert('Sincronizados ' + currentBots.length + ' bots com sucesso!');
                    }
                })
                .catch(function (err) { alert('Erro ao sincronizar bots: ' + (err.message || err)); });
        };
    }

    // =========================================================================
    // SUPABASE HANDLERS
    // =========================================================================
    var btnToggleKey = view.querySelector('#btnToggleKeyVisibility');
    if (btnToggleKey) {
        btnToggleKey.onclick = function (e) {
            e.preventDefault();
            var input = view.querySelector('#inputSupabaseKey');
            if (input) {
                input.type = input.type === 'password' ? 'text' : 'password';
            }
        };
    }

    var btnSaveSb = view.querySelector('#btnSaveSupabaseConfig');
    if (btnSaveSb) {
        btnSaveSb.onclick = function (e) {
            e.preventDefault();
            saveConfiguration(false);
        };
    }

    var btnTestSb = view.querySelector('#btnTestSupabase');
    if (btnTestSb) {
        btnTestSb.onclick = function (e) {
            e.preventDefault();
            var url = (view.querySelector('#inputSupabaseUrl') || {}).value || '';
            var key = (view.querySelector('#inputSupabaseKey') || {}).value || '';

            if (!url || !key) {
                alert('Informe a URL e a Secret key do Supabase para testar.');
                return;
            }

            var badge = view.querySelector('#lblSupabaseConnBadge');
            if (badge) {
                badge.textContent = 'Testando conexão...';
                badge.style.color = '#f9e2af';
            }

            apiRequest('POST', 'NebulaFtp/Supabase/Test', { Url: url, Key: key })
                .then(function (res) {
                    if (res && res.Success) {
                        if (badge) {
                            badge.textContent = '🟢 Conectado';
                            badge.style.color = '#a6e3a1';
                        }
                        if (typeof Dashboard !== 'undefined' && Dashboard.alert) {
                            Dashboard.alert('Sucesso: ' + (res.Message || 'Conexão validada!'));
                        } else {
                            alert('Sucesso: ' + (res.Message || 'Conexão validada!'));
                        }
                    } else {
                        if (badge) {
                            badge.textContent = '🔴 Falha';
                            badge.style.color = '#f38ba8';
                        }
                        alert('Falha na conexão: ' + (res ? res.Message : 'Erro desconhecido'));
                    }
                })
                .catch(function (err) {
                    if (badge) {
                        badge.textContent = '🔴 Erro';
                        badge.style.color = '#f38ba8';
                    }
                    alert('Erro ao testar Supabase: ' + (err.message || err));
                });
        };
    }

    var btnRunBackup = view.querySelector('#btnRunSupabaseBackup');
    if (btnRunBackup) {
        btnRunBackup.onclick = function (e) {
            e.preventDefault();
            apiRequest('POST', 'NebulaFtp/Supabase/Backup')
                .then(function (res) {
                    if (res && res.Success) {
                        var term = view.querySelector('#supabaseLogTerminal');
                        if (term) term.textContent = '[SUPABASE] Backup concluído.\n' + (res.Message || '') + '\n';
                        pollStatusAndLogs();
                        if (typeof Dashboard !== 'undefined' && Dashboard.alert) {
                            Dashboard.alert(res.Message || 'Backup concluído!');
                        } else {
                            alert(res.Message || 'Backup concluído!');
                        }
                    } else {
                        alert('Erro ao iniciar backup: ' + (res ? res.Message : 'Erro desconhecido'));
                    }
                })
                .catch(function (err) {
                    alert('Erro ao solicitar backup: ' + (err.message || err));
                });
        };
    }

    var btnRunRestore = view.querySelector('#btnRunSupabaseRestore');
    if (btnRunRestore) {
        btnRunRestore.onclick = function (e) {
            e.preventDefault();
            if (!confirm('ATENÇÃO: A restauração irá carregar os arquivos e os usuários salvos no Supabase para o MongoDB local. As senhas serão restauradas pelos hashes protegidos. Deseja continuar?')) {
                return;
            }

            apiRequest('POST', 'NebulaFtp/Supabase/Restore')
                .then(function (res) {
                    if (res && res.Success) {
                        var term = view.querySelector('#supabaseLogTerminal');
                        if (term) term.textContent = '[SUPABASE-RESTORE] Restauração concluída.\n' + (res.Message || '') + '\n';
                        pollStatusAndLogs();
                        if (typeof Dashboard !== 'undefined' && Dashboard.alert) {
                            Dashboard.alert(res.Message || 'Restauração concluída!');
                        } else {
                            alert(res.Message || 'Restauração concluída!');
                        }
                    } else {
                        alert('Erro ao iniciar restauração: ' + (res ? res.Message : 'Erro desconhecido'));
                    }
                })
                .catch(function (err) {
                    alert('Erro ao solicitar restauração: ' + (err.message || err));
                });
        };
    }

    var btnOpenSql = view.querySelector('#btnOpenSqlModal');
    var sqlModal = view.querySelector('#sqlModalOverlay');
    var btnCloseSql = view.querySelector('#btnCloseSqlModal');
    var btnCopySql = view.querySelector('#btnCopySqlScript');

    if (btnOpenSql && sqlModal) {
        btnOpenSql.onclick = function (e) {
            e.preventDefault();
            apiRequest('GET', 'NebulaFtp/Supabase/SqlScript')
                .then(function (sql) {
                    var txt = view.querySelector('#txtSqlScript');
                    if (txt) txt.value = typeof sql === 'string' ? sql : JSON.stringify(sql);
                    sqlModal.classList.add('open');
                })
                .catch(function (err) {
                    alert('Erro ao carregar script SQL: ' + (err.message || err));
                });
        };
    }

    if (btnCloseSql && sqlModal) {
        btnCloseSql.onclick = function (e) {
            e.preventDefault();
            sqlModal.classList.remove('open');
        };
    }

    if (btnCopySql) {
        btnCopySql.onclick = function (e) {
            e.preventDefault();
            var txt = view.querySelector('#txtSqlScript');
            if (txt && txt.value) {
                navigator.clipboard.writeText(txt.value).then(function () {
                    alert('Script SQL copiado para a área de transferência! Cole no SQL Editor do Supabase.');
                }).catch(function () {
                    txt.select();
                    document.execCommand('copy');
                    alert('Script SQL copiado!');
                });
            }
        };
    }

    var btnClearSbLogs = view.querySelector('#btnClearSupabaseLogs');
    if (btnClearSbLogs) {
        btnClearSbLogs.onclick = function (e) {
            e.preventDefault();
            var term = view.querySelector('#supabaseLogTerminal');
            if (term) term.textContent = 'Logs limpos.\n';
        };
    }

    function updateSupabaseStatusDisplay(config) {
        var badge = view.querySelector('#lblSupabaseConnBadge');
        var lastBackupLbl = view.querySelector('#lblSupabaseLastBackup');
        var cardStep = view.querySelector('#sbCardStep');

        if (config && config.SupabaseUrl && config.SupabaseKey) {
            if (badge) {
                badge.textContent = 'Configurado';
                badge.style.color = '#89b4fa';
            }
        } else {
            if (badge) {
                badge.textContent = '⚪ Não configurado';
                badge.style.color = '#a6adc8';
            }
        }

        if (config && config.SupabaseLastBackupStatus) {
            if (lastBackupLbl) lastBackupLbl.textContent = 'Status: ' + config.SupabaseLastBackupStatus;
            if (cardStep) cardStep.textContent = config.SupabaseLastBackupStatus;
        }
    }

    function loadConfiguration() {
        apiRequest('GET', 'NebulaFtp/Config').then(function (config) {
            currentConfig = config;
            var setVal = function (id, val) {
                var el = view.querySelector('#' + id);
                if (el) el.value = val;
            };

            // The API intentionally redacts the MongoDB URI. Keep the field
            // blank so saving unrelated settings lets the server preserve the
            // existing secret instead of replacing it with localhost.
            setVal('cfgMongoUri', config.MongoDbConnectionString || '');
            setVal('cfgServerPort', config.ServerPort || 2121);
            setVal('cfgUsername', config.Username || '');
            setVal('cfgPassword', '');
            setVal('cfgApiId', config.ApiId || '');
            setVal('cfgApiHash', config.ApiHash || '');
            setVal('cfgChatId', config.NotificationsChannelIds || config.TelegramNotificationChatIds || config.ChatId || '');
            setVal('cfgPublicServerUrl', config.PublicServerUrl || 'http://mulletaflix.duckdns.org:8096');
            setVal('cfgBotTokens', config.BotTokens || '');
            var chkAllowInsecureRemoteFtp = view.querySelector('#cfgAllowInsecureRemoteFtp');
            if (chkAllowInsecureRemoteFtp) chkAllowInsecureRemoteFtp.checked = config.AllowInsecureRemoteFtp === true;
            var chkEmbedFtpCredentialsInStrmUrls = view.querySelector('#cfgEmbedFtpCredentialsInStrmUrls');
            if (chkEmbedFtpCredentialsInStrmUrls) chkEmbedFtpCredentialsInStrmUrls.checked = config.EmbedFtpCredentialsInStrmUrls === true;

            var chkTurbo = view.querySelector('#chkTurboMode');
            if (chkTurbo) chkTurbo.checked = config.TurboEnabled !== false;

            setVal('spinTurboMinutes', config.TurboIdleMinutes || 10);
            setVal('spinDownloadParts', config.DownloadParts || 32);

            // Supabase fields
            setVal('inputSupabaseUrl', config.SupabaseUrl || '');
            setVal('inputSupabaseKey', config.SupabaseKey || '');
            var chkSbAuto = view.querySelector('#chkSupabaseAutoBackup');
            if (chkSbAuto) chkSbAuto.checked = config.SupabaseAutoBackup !== false;
            updateSupabaseStatusDisplay(config);

            renderList('listMonitorPaths', config.MonitorPaths || ['D:\\midias']);
            renderList('listStagePaths', config.StagePaths || ['E:\\NebulaStage', 'F:\\NebulaStage', 'I:\\NebulaStage']);
        }).catch(function (err) {
            console.error('Erro ao carregar configuração NebulaFTP', err);
        });
    }

    function saveConfiguration(silent) {
        var getVal = function (id) {
            var el = view.querySelector('#' + id);
            return el ? el.value : '';
        };
        var baseConfig = currentConfig || {};

        var config = {
            Enabled: baseConfig.Enabled !== false,
            RaiDriveDownloadUrl: baseConfig.RaiDriveDownloadUrl || 'https://www.raidrive.com/download',
            ServerHost: baseConfig.ServerHost || '0.0.0.0',
            MongoDbConnectionString: getVal('cfgMongoUri'),
            ServerPort: parseInt(getVal('cfgServerPort') || '2121', 10),
            HttpStreamPort: baseConfig.HttpStreamPort || 2123,
            PassivePorts: baseConfig.PassivePorts || '60000-60009',
            Username: getVal('cfgUsername'),
            Password: getVal('cfgPassword'),
            ApiId: getVal('cfgApiId'),
            ApiHash: getVal('cfgApiHash'),
            ChatId: getVal('cfgChatId').split(',')[0].trim(),
            NotificationsEnabled: baseConfig.NotificationsEnabled !== false,
            NotificationsChannelIds: getVal('cfgChatId'),
            NotificationsIntervalSeconds: baseConfig.NotificationsIntervalSeconds || 3,
            PublicServerUrl: getVal('cfgPublicServerUrl') || 'http://mulletaflix.duckdns.org:8096',
            BotTokens: getVal('cfgBotTokens'),
            AllowInsecureRemoteFtp: (view.querySelector('#cfgAllowInsecureRemoteFtp') || {}).checked === true,
            EmbedFtpCredentialsInStrmUrls: (view.querySelector('#cfgEmbedFtpCredentialsInStrmUrls') || {}).checked === true,
            BotTokensCollection: baseConfig.BotTokensCollection || 'bot_tokens',
            BotTokensTable: baseConfig.BotTokensTable || 'nebula_bot_tokens',
            MaxWorkers: baseConfig.MaxWorkers || 10,
            ChunkSizeMb: baseConfig.ChunkSizeMb || 64,
            DeleteSourceAfterUpload: baseConfig.DeleteSourceAfterUpload !== false,
            DriveLetter: baseConfig.DriveLetter || 'N:',
            RemotePath: baseConfig.RemotePath || '/',
            UseMappedDrive: baseConfig.UseMappedDrive !== false,
            WatchFolderPath: baseConfig.WatchFolderPath || '',
            SetupNotes: baseConfig.SetupNotes || '',
            NebulaFolderPath: baseConfig.NebulaFolderPath || '',
            TurboEnabled: (view.querySelector('#chkTurboMode') || {}).checked !== false,
            TurboIdleMinutes: parseInt(getVal('spinTurboMinutes') || '10', 10),
            DownloadParts: parseInt(getVal('spinDownloadParts') || '32', 10),
            SupabaseUrl: getVal('inputSupabaseUrl'),
            SupabaseKey: getVal('inputSupabaseKey'),
            SupabaseAutoBackup: (view.querySelector('#chkSupabaseAutoBackup') || {}).checked !== false,
            SupabaseAutoBackupIntervalHours: 1,
            SupabaseLastBackupTime: baseConfig.SupabaseLastBackupTime || null,
            SupabaseLastBackupStatus: baseConfig.SupabaseLastBackupStatus || 'Nenhum backup realizado ainda',
            SupabaseLastBackupFilesCount: baseConfig.SupabaseLastBackupFilesCount || 0,
            MonitorPaths: getListValues('listMonitorPaths'),
            StagePaths: getListValues('listStagePaths')
        };

        apiRequest('POST', 'NebulaFtp/Config', config).then(function () {
            if (!silent) {
                var drawer = view.querySelector('#settingsDrawer');
                if (drawer) drawer.classList.remove('open');
                if (typeof Dashboard !== 'undefined' && Dashboard.alert) {
                    Dashboard.alert('Configurações salvas com sucesso!');
                } else {
                    alert('Configurações salvas com sucesso!');
                }
            }
            updateSupabaseStatusDisplay(config);
        }).catch(function (err) {
            if (!silent) alert('Erro ao salvar: ' + (err.message || err));
        });
    }

    function renderList(elementId, items) {
        var select = view.querySelector('#' + elementId);
        if (!select) return;
        select.innerHTML = '';
        (items || []).forEach(function (item) {
            if (item) {
                var opt = document.createElement('option');
                opt.value = item;
                opt.textContent = item;
                select.appendChild(opt);
            }
        });
    }

    function getListValues(elementId) {
        var select = view.querySelector('#' + elementId);
        if (!select) return [];
        var values = [];
        for (var i = 0; i < select.options.length; i++) {
            values.push(select.options[i].value);
        }
        return values;
    }

    function loadBots() {
        apiRequest('GET', 'NebulaFtp/Bots').then(function (bots) {
            currentBots = bots || [];
            renderBotsGrid(currentBots);
        }).catch(function (err) {
            console.error('Erro ao carregar bots do Nebula', err);
        });
    }

    function renderBotsGrid(bots) {
        var container = view.querySelector('#botsGridContainer');
        var countHeader = view.querySelector('#lblTotalBotsCount');
        var countTab = view.querySelector('#tabBotCount');
        var countEnvio = view.querySelector('#lblEnvioBotCount');

        var total = bots.length;
        if (countHeader) countHeader.textContent = total;
        if (countTab) countTab.textContent = total;
        if (countEnvio) countEnvio.textContent = total;

        if (!container) return;

        if (total === 0) {
            container.innerHTML = '<div class="nebula-empty-text">Nenhum bot cadastrado. Clique em "+ Adicionar Novo Bot" ou sincronize com o .env.</div>';
            return;
        }

        container.innerHTML = '';
        bots.forEach(function (bot) {
            var card = document.createElement('div');
            card.className = 'nebula-bot-card';

            var topRow = document.createElement('div');
            topRow.className = 'nebula-bot-card-top';

            var nameSpan = document.createElement('span');
            nameSpan.className = 'nebula-bot-name';
            nameSpan.textContent = 'Bot #' + bot.Index + ' (' + bot.Name + ')';
            topRow.appendChild(nameSpan);

            var badge = document.createElement('span');
        badge.className = 'nebula-bot-session-badge ' + (bot.SessionExists ? 'active' : 'new');
        badge.textContent = bot.SessionExists ? 'Sessão Ativa' : 'Nova Sessão';
            topRow.appendChild(badge);
            card.appendChild(topRow);

            var tokenBox = document.createElement('div');
            tokenBox.className = 'nebula-bot-token-box';

            var tokenTxt = document.createElement('span');
            tokenTxt.className = 'nebula-bot-token-text';
            tokenTxt.textContent = bot.MaskedToken || bot.Token;
            tokenBox.appendChild(tokenTxt);

            var copyBtn = document.createElement('button');
            copyBtn.type = 'button';
            copyBtn.className = 'nebula-log-mini-btn';
            if (bot.Token) {
                copyBtn.textContent = 'Copiar';
                copyBtn.onclick = function (e) {
                    e.preventDefault();
                    navigator.clipboard.writeText(bot.Token).then(function () {
                        copyBtn.textContent = 'Copiado!';
                        setTimeout(function () { copyBtn.textContent = 'Copiar'; }, 2000);
                    }).catch(function () {
                        copyBtn.textContent = 'Falha ao copiar';
                    });
                };
            } else {
                copyBtn.textContent = 'Token oculto';
                copyBtn.disabled = true;
                copyBtn.title = 'O token completo não é retornado por segurança. Edite o bot para informar um novo token.';
            }
            tokenBox.appendChild(copyBtn);
            card.appendChild(tokenBox);

            var actionsRow = document.createElement('div');
            actionsRow.className = 'nebula-bot-card-actions';

            var editBtn = document.createElement('button');
            editBtn.type = 'button';
            editBtn.className = 'nebula-btn btn-secondary';
            editBtn.style.padding = '4px 10px';
            editBtn.style.fontSize = '0.8rem';
            editBtn.textContent = '✏ Editar';
            editBtn.onclick = function (e) {
                e.preventDefault();
                openBotModal(bot);
            };
            actionsRow.appendChild(editBtn);

            var delBtn = document.createElement('button');
            delBtn.type = 'button';
            delBtn.className = 'nebula-btn btn-stop';
            delBtn.style.padding = '4px 10px';
            delBtn.style.fontSize = '0.8rem';
            delBtn.textContent = '🗑 Excluir';
            delBtn.onclick = function (e) {
                e.preventDefault();
                if (confirm('Tem certeza que deseja remover o Bot #' + bot.Index + '?')) {
                    apiRequest('DELETE', 'NebulaFtp/Bots/' + bot.Index).then(function (updated) {
                        currentBots = updated || [];
                        renderBotsGrid(currentBots);
                        loadConfiguration();
                    }).catch(function (err) {
                        alert('Erro ao excluir bot: ' + (err.message || err));
                    });
                }
            };
            actionsRow.appendChild(delBtn);

            card.appendChild(actionsRow);
            container.appendChild(card);
        });
    }

    function openBotModal(bot) {
        var modal = view.querySelector('#botModalOverlay');
        var title = view.querySelector('#botModalTitle');
        var indexInput = view.querySelector('#modalBotIndex');
        var tokenInput = view.querySelector('#inputModalBotToken');

        if (!modal) return;

        if (bot) {
            title.textContent = 'Editar Bot #' + bot.Index;
            indexInput.value = bot.Index;
            tokenInput.value = bot.Token;
        } else {
            title.textContent = 'Adicionar Novo Bot do Telegram';
            indexInput.value = '';
            tokenInput.value = '';
        }

        modal.classList.add('open');
    }

    function closeBotModal() {
        var modal = view.querySelector('#botModalOverlay');
        if (modal) modal.classList.remove('open');
    }

    function pollStatusAndLogs() {
        apiRequest('GET', 'NebulaFtp/Status').then(function (status) {
            var badge = view.querySelector('#globalStatusBadge');
            if (badge) {
                var rawText = status.GlobalStatusText || '';
                if (rawText.indexOf('|') !== -1) {
                    badge.innerHTML = '';
                    var parts = rawText.split('|');
                    parts.forEach(function (part) {
                        var trimmed = part.trim();
                        if (!trimmed) return;
                        var chip = document.createElement('span');
                        chip.className = 'nebula-status-chip';
                        var dot = document.createElement('span');
                        dot.className = 'nebula-status-dot';
                        var lower = trimmed.toLowerCase();
                        if (lower.indexOf('executando') !== -1 || lower.indexOf('montado') !== -1 || lower.indexOf('ativo') !== -1 || lower.indexOf('conectado') !== -1) {
                            chip.classList.add('chip-active');
                        } else if (lower.indexOf('parado') !== -1 || lower.indexOf('desmontado') !== -1 || lower.indexOf('inativo') !== -1 || lower.indexOf('desconectado') !== -1) {
                            chip.classList.add('chip-idle');
                        } else {
                            chip.classList.add('chip-warn');
                        }
                        chip.appendChild(dot);
                        var textNode = document.createTextNode(trimmed);
                        chip.appendChild(textNode);
                        badge.appendChild(chip);
                    });
                } else {
                    badge.textContent = rawText;
                }
            }

            var btnStartEnv = view.querySelector('#btnStartEnvio');
            var btnStartStream = view.querySelector('#btnStartStreamOnly');
            var btnStopEnv = view.querySelector('#btnStopEnvio');

            if (btnStartEnv) btnStartEnv.disabled = status.IsEnvioRunning;
            if (btnStartStream) btnStartStream.disabled = status.IsEnvioRunning;
            if (btnStopEnv) btnStopEnv.disabled = !status.IsEnvioRunning;

            var btnMountN = view.querySelector('#btnMountDriveN');
            var btnUnmountN = view.querySelector('#btnUnmountDriveN');
            if (btnMountN && btnUnmountN) {
                if (status.IsDriveNMounted) {
                    btnMountN.style.display = 'none';
                    btnUnmountN.style.display = 'inline-block';
                } else {
                    btnMountN.style.display = 'inline-block';
                    btnUnmountN.style.display = 'none';
                }
            }

            var btnStartDl = view.querySelector('#btnStartDownloader');
            var btnStopDl = view.querySelector('#btnStopDownloader');

            if (btnStartDl) btnStartDl.disabled = status.IsDownloaderRunning;
            if (btnStopDl) btnStopDl.disabled = !status.IsDownloaderRunning;

            var diskSpace = view.querySelector('#lblStageDiskSpace');
            if (diskSpace && status.StageDisksFormatted) {
                diskSpace.textContent = status.StageDisksFormatted;
            }

            if (status.CurrentDownload) {
                var cur = status.CurrentDownload;
                var dlTitle = view.querySelector('#dlCurrentTitle');
                var dlStep = view.querySelector('#dlCurrentStep');
                var dlFill = view.querySelector('#dlPbarFill');
                var dlPct = view.querySelector('#dlPbarPct');
                var dlStats = view.querySelector('#dlDetailStats');

                if (dlTitle) dlTitle.textContent = cur.Name || 'Nenhum download em andamento';
                if (dlStep) dlStep.textContent = cur.StageStep || '';
                if (dlFill) dlFill.style.width = (cur.Percentage || 0) + '%';
                if (dlPct) dlPct.textContent = (cur.Percentage || 0).toFixed(1) + '%';
                if (dlStats) dlStats.textContent = cur.DetailText || '';
            }

            window._nebulaUploadQueueCount = status.UploadQueueCount !== undefined && status.UploadQueueCount !== null
                ? status.UploadQueueCount
                : (status.QueuedUploads ? status.QueuedUploads.length : 0);
            renderActiveUploads(status.ActiveUploads || []);
        }).catch(function () {});

        apiRequest('GET', 'NebulaFtp/Logs?serverOffset=' + serverLogOffset + '&downloaderOffset=' + downloaderLogOffset).then(function (logs) {
            if (logs.ServerLogs && logs.ServerLogs.length > 0) {
                var sTerm = view.querySelector('#serverLogTerminal');
                var sbTerm = view.querySelector('#supabaseLogTerminal');

                if (sTerm) {
                    if (sTerm.textContent === 'Aguardando início do servidor...') sTerm.textContent = '';
                    logs.ServerLogs.forEach(function (line) {
                        sTerm.textContent += line + '\n';
                        if (line.indexOf('[SUPABASE') !== -1 && sbTerm) {
                            if (sbTerm.textContent === 'Aguardando operações do Supabase...') sbTerm.textContent = '';
                            sbTerm.textContent += line + '\n';
                            sbTerm.scrollTop = sbTerm.scrollHeight;
                        }
                    });
                    var sLines = sTerm.textContent.split('\n');
                    if (sLines.length > 800) {
                        sTerm.textContent = sLines.slice(sLines.length - 800).join('\n');
                    }
                    sTerm.scrollTop = sTerm.scrollHeight;
                }
            }
            serverLogOffset = (logs.ServerLogTotal !== undefined) ? logs.ServerLogTotal : (serverLogOffset + (logs.ServerLogs ? logs.ServerLogs.length : 0));

            if (logs.DownloaderLogs && logs.DownloaderLogs.length > 0) {
                var dTerm = view.querySelector('#downloaderLogTerminal');
                if (dTerm) {
                    if (dTerm.textContent === 'Aguardando início do downloader...') dTerm.textContent = '';
                    logs.DownloaderLogs.forEach(function (line) { dTerm.textContent += line + '\n'; });
                    var dLines = dTerm.textContent.split('\n');
                    if (dLines.length > 800) {
                        dTerm.textContent = dLines.slice(dLines.length - 800).join('\n');
                    }
                    dTerm.scrollTop = dTerm.scrollHeight;
                }
            }
            downloaderLogOffset = (logs.DownloaderLogTotal !== undefined) ? logs.DownloaderLogTotal : (downloaderLogOffset + (logs.DownloaderLogs ? logs.DownloaderLogs.length : 0));
        }).catch(function () {});
    }

    function refreshUploadQueueSummary() {
        apiRequest('GET', 'NebulaFtp/UploadQueueSummary').then(function (summary) {
            if (!summary || summary.IsAvailable === false || summary.isAvailable === false) {
                mongoPendingUploadCount = null;
            } else {
                var pendingCount = summary.PendingCount !== undefined ? summary.PendingCount : summary.pendingCount;
                mongoPendingUploadCount = Number.isFinite(Number(pendingCount)) ? Number(pendingCount) : null;
            }
            renderActiveUploads(null);
        }).catch(function () {
            // Keep the last Mongo snapshot on transient errors; status polling still shows loaded items.
        });
    }

    function renderActiveUploads(uploads) {
        if (uploads !== null) {
            currentActiveUploads = uploads || [];
        }
        uploads = currentActiveUploads;
        var container = view.querySelector('#uploadsListContainer');
        var countLbl = view.querySelector('#lblUploadCount');
        if (countLbl) {
            var queueCount = Number(window._nebulaUploadQueueCount || 0);
            var pendingText = mongoPendingUploadCount === null
                ? 'Total pendente no Mongo: indisponível (carregados: ' + queueCount + ')'
                : 'Aguardando no Mongo: ' + Math.max(mongoPendingUploadCount, queueCount) + ' (carregados: ' + queueCount + ')';
            countLbl.textContent = (uploads ? uploads.length : 0) + ' enviando agora  |  ' + pendingText;
        }
        if (!container) return;

        if (!uploads || uploads.length === 0) {
            container.innerHTML = '<div class="nebula-empty-text">Nenhum upload em andamento no momento.</div>';
            return;
        }

        container.innerHTML = '';
        uploads.forEach(function (item) {
            var card = document.createElement('div');
            card.className = 'nebula-upload-card' + (item.Percentage >= 100 ? ' completed' : '');

            var header = document.createElement('div');
            header.className = 'nebula-upload-card-header';

            var title = document.createElement('div');
            title.className = 'nebula-upload-card-title';

            var tagsContainer = document.createElement('div');
            tagsContainer.className = 'nebula-upload-tags';

            var rawInfo = item.InfoText || item.DisplayName || item.Name || '';
            if (rawInfo.indexOf('|') !== -1) {
                var parts = rawInfo.split('|').map(function (s) { return s.trim(); });
                var filename = parts[parts.length - 1];
                title.textContent = filename;
                title.title = filename;

                for (var i = 0; i < parts.length - 1; i++) {
                    var p = parts[i];
                    var tag = document.createElement('span');
                    tag.className = 'nebula-tag';
                    var pLower = p.toLowerCase();
                    if (pLower.indexOf('worker') !== -1) {
                        tag.classList.add('tag-worker');
                    } else if (pLower.indexOf('bot') !== -1) {
                        tag.classList.add('tag-bots');
                    } else if (pLower.indexOf('upload') !== -1 || pLower.indexOf('enviando') !== -1) {
                        tag.classList.add('tag-uploading');
                    }
                    tag.textContent = p;
                    tagsContainer.appendChild(tag);
                }
            } else {
                title.textContent = rawInfo;
                title.title = rawInfo;
            }

            header.appendChild(title);
            header.appendChild(tagsContainer);
            card.appendChild(header);

            var pbarRow = document.createElement('div');
            pbarRow.className = 'nebula-pbar-container';
            var pbarBg = document.createElement('div');
            pbarBg.className = 'nebula-pbar-bg';
            var pbarFill = document.createElement('div');
            pbarFill.className = 'nebula-pbar-fill' + (item.Percentage >= 100 ? ' success' : '');
            pbarFill.style.width = (item.Percentage || 0) + '%';
            pbarBg.appendChild(pbarFill);

            var pbarPct = document.createElement('div');
            pbarPct.className = 'nebula-pbar-pct';
            pbarPct.textContent = (item.Percentage || 0).toFixed(1) + '%';
            pbarRow.appendChild(pbarBg);
            pbarRow.appendChild(pbarPct);

            card.appendChild(pbarRow);
            container.appendChild(card);
        });
    }

    function startPolling() {
        if (!pollInterval) {
            pollStatusAndLogs();
            pollInterval = setInterval(pollStatusAndLogs, 1000);
        }
        if (!queueSummaryInterval) {
            refreshUploadQueueSummary();
            queueSummaryInterval = setInterval(refreshUploadQueueSummary, 30000);
        }
    }

    function stopPolling() {
        if (pollInterval) {
            clearInterval(pollInterval);
            pollInterval = null;
        }
        if (queueSummaryInterval) {
            clearInterval(queueSummaryInterval);
            queueSummaryInterval = null;
        }
    }

    view.addEventListener('viewshow', startPolling);
    view.addEventListener('viewdestroy', stopPolling);
    view.addEventListener('viewhide', stopPolling);

    // Initial load
    loadConfiguration();
    loadBots();
    serverLogOffset = 0;
    downloaderLogOffset = 0;
    startPolling();
}
