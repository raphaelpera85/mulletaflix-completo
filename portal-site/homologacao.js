(() => {
  const repository = 'raphaelpera85/mulletaflix-completo';
  const apiRoot = `https://api.github.com/repos/${repository}`;
  const refreshMs = 60000;
  const byId = id => document.getElementById(id);
  const escapeHtml = value => String(value ?? '').replace(/[&<>'"]/g, character => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[character]));
  const date = value => value ? new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value)) : '—';
  const statusText = { completed: 'Concluído', success: 'Aprovado', pending: 'Pendente', queued: 'Na fila', in_progress: 'Em execução', failure: 'Falhou', monitoring: 'Monitorando', unknown: 'Sem evidência' };
  const statusClass = value => ({ completed: 'is-good', success: 'is-good', pending: 'is-warn', queued: 'is-warn', in_progress: 'is-live', failure: 'is-bad', monitoring: 'is-live', unknown: 'is-muted' }[value] || 'is-muted');
  const renderCoverage = data => {
    const coverage = data.coverage || {};
    const total = Number(coverage.total) || 0;
    const completed = Number(coverage.completed) || 0;
    const percent = total ? Math.round((completed / total) * 100) : 0;
    byId('coverage-completed').textContent = completed;
    byId('coverage-pending').textContent = Number(coverage.pending) || 0;
    byId('coverage-total').textContent = total;
    document.querySelectorAll('[data-coverage-percent]').forEach(element => { element.textContent = `${percent}%`; });
    byId('coverage-bar').style.width = `${Math.min(100, percent)}%`;
    byId('coverage-source').textContent = `${coverage.definition || 'Fonte não informada'} Atualizado em ${date(data.generatedAt)}.`;
  };
  const renderFeatures = data => {
    const features = Array.isArray(data.features) ? data.features : [];
    byId('feature-count').textContent = `${features.length} itens acompanhados`;
    byId('feature-table').innerHTML = features.length ? features.map(feature => `<tr><td><strong>${escapeHtml(feature.id)}</strong></td><td>${escapeHtml(feature.name)}</td><td>${escapeHtml(feature.area)}</td><td><span class="status-chip ${statusClass(feature.status)}"><i></i>${escapeHtml(statusText[feature.status] || feature.status)}</span></td></tr>`).join('') : '<tr><td colspan="4" class="empty-cell">Nenhuma funcionalidade foi publicada no contrato de homologação.</td></tr>';
  };
  const renderTracks = (data, runs, releases) => {
    const latest = {};
    (runs || []).forEach(run => {
      const key = /android|apk|mobile/i.test(`${run.name} ${run.workflow?.name || ''}`) ? 'android' : 'server';
      if (!latest[key] || new Date(run.created_at) > new Date(latest[key].created_at)) latest[key] = run;
    });
    const stable = (releases || []).filter(release => !release.draft && !release.prerelease);
    const serverRelease = stable.find(release => /^v\d+\.\d+\.\d+$/.test(release.tag_name));
    const apkRelease = stable.find(release => /^app-v\d+\.\d+\.\d+$/.test(release.tag_name));
    byId('track-grid').innerHTML = (data.tracks || []).map(track => {
      const run = latest[track.id];
      const release = track.id === 'android' ? apkRelease : track.id === 'server' ? serverRelease : null;
      const state = run ? (run.status === 'completed' ? (run.conclusion === 'success' ? 'success' : 'failure') : 'in_progress') : track.status || 'unknown';
      const label = run ? `${statusText[state]} · ${run.name}` : statusText[state];
      const detail = run ? `Último workflow: ${date(run.updated_at)}` : release ? `Release estável: ${escapeHtml(release.tag_name)}` : 'Aguardando evidência pública.';
      return `<article class="track-card"><div class="track-top"><span class="status-chip ${statusClass(state)}"><i></i>${escapeHtml(label)}</span><span class="track-kind">${escapeHtml(track.kind)}</span></div><h3>${escapeHtml(track.name)}</h3><p>${escapeHtml(detail)}</p><div class="track-source">Fonte: ${escapeHtml(track.source)}</div></article>`;
    }).join('');
    const lastRun = (runs || []).slice().sort((a, b) => new Date(b.updated_at) - new Date(a.updated_at))[0];
    byId('live-updated').textContent = `Última leitura pública: ${date(lastRun?.updated_at || new Date())}`;
  };
  const renderEvents = data => {
    const events = Array.isArray(data.events) ? data.events : [];
    byId('event-list').innerHTML = events.length ? events.slice(0, 12).map(event => `<li><span class="event-dot ${statusClass(event.status)}"></span><div><strong>${escapeHtml(event.title)}</strong><p>${escapeHtml(event.location || 'Local não informado')} · ${escapeHtml(statusText[event.status] || event.status)} · ${date(event.updatedAt)}</p></div></li>`).join('') : '<li class="empty-event"><strong>Nenhum evento de homologação publicado</strong><p>Quando um agente iniciar um teste, registre-o em <code>portal-site/homologacao-status.json</code> com local, status e evidência.</p></li>';
  };
  const load = async () => {
    const [data, runs, releases] = await Promise.all([
      fetch('homologacao-status.json', { cache: 'no-store' }).then(response => response.json()),
      fetch(`${apiRoot}/actions/runs?per_page=20`, { headers: { Accept: 'application/vnd.github+json' } }).then(response => response.ok ? response.json() : { workflow_runs: [] }).catch(() => ({ workflow_runs: [] })),
      fetch(`${apiRoot}/releases?per_page=20`, { headers: { Accept: 'application/vnd.github+json' } }).then(response => response.ok ? response.json() : []).catch(() => [])
    ]);
    renderCoverage(data); renderFeatures(data); renderTracks(data, runs.workflow_runs, releases); renderEvents(data);
    byId('dashboard-state').textContent = 'Atualizado'; byId('dashboard-state').className = 'status-chip is-good';
  };
  byId('refresh-dashboard').addEventListener('click', () => { byId('dashboard-state').textContent = 'Atualizando…'; load().catch(() => { byId('dashboard-state').textContent = 'Falha ao atualizar'; byId('dashboard-state').className = 'status-chip is-bad'; }); });
  load().catch(() => { byId('dashboard-state').textContent = 'Fonte indisponível'; byId('dashboard-state').className = 'status-chip is-bad'; });
  window.setInterval(() => load().catch(() => {}), refreshMs);
})();
