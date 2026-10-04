(() => {
  const repository = 'raphaelpera85/mulletaflix-completo';
  const api = `https://api.github.com/repos/${repository}/releases?per_page=50`;
  const versionOf = tag => (tag.match(/(?:app-v|v)(\d+)\.(\d+)\.(\d+)/) || []).slice(1).map(Number);
  const compare = (a, b) => {
    const av = versionOf(a.tag_name), bv = versionOf(b.tag_name);
    for (let i = 0; i < 3; i += 1) if ((av[i] || 0) !== (bv[i] || 0)) return (av[i] || 0) - (bv[i] || 0);
    return 0;
  };
  const replaceText = (root, pattern, value) => {
    const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
    const nodes = [];
    while (walker.nextNode()) nodes.push(walker.currentNode);
    nodes.forEach(node => { const replaced = node.nodeValue.replace(pattern, value); if (replaced !== node.nodeValue) node.nodeValue = replaced; });
  };
  const sync = async () => {
    const releases = await (await fetch(api, { headers: { Accept: 'application/vnd.github+json' } })).json();
    const valid = releases.filter(r => !r.draft && !r.prerelease);
    const server = valid.filter(r => /^v\d+\.\d+\.\d+$/.test(r.tag_name)).sort(compare).pop();
    const android = valid.filter(r => /^app-v\d+\.\d+\.\d+$/.test(r.tag_name)).sort(compare).pop();
    if (!server || !android) return;
    const serverVersion = server.tag_name.slice(1);
    const androidVersion = android.tag_name.slice(5);
    document.querySelectorAll('a[href*="/releases/download/"], a[href*="/releases/tag/app-v"]').forEach(link => {
      if (link.href.includes('/app-v')) {
        link.href = link.href.replace(/app-v\d+\.\d+\.\d+/g, android.tag_name).replace(/mulletaflix-app-v\d+\.\d+\.\d+/, `mulletaflix-app-v${androidVersion}`);
      } else if (link.href.includes('/releases/download/v')) {
        link.href = link.href.replace(/\/v\d+\.\d+\.\d+\//, `/${server.tag_name}/`).replace(/mulletaflix_\d+\.\d+\.\d+/, `mulletaflix_${serverVersion}`);
      }
    });
    const path = location.pathname;
    if (path.endsWith('/') || path.endsWith('/index.html')) {
      document.querySelectorAll('.release').forEach(card => {
        const text = card.textContent;
        if (/Android/i.test(text)) replaceText(card, /v\d+\.\d+\.\d+/g, `v${androidVersion}`);
        else if (/Servidor|Atualização/i.test(text)) replaceText(card, /v\d+\.\d+\.\d+/g, `v${serverVersion}`);
      });
    }
    if (path.includes('downloads')) {
      document.querySelectorAll('#aplicativos .card').forEach(card => { if (/Android/i.test(card.textContent)) replaceText(card, /v\d+\.\d+\.\d+/g, `v${androidVersion}`); });
      document.querySelectorAll('#servidor .card, #servidor .section-title').forEach(card => replaceText(card, /v\d+\.\d+\.\d+/g, `v${serverVersion}`));
    }
    if (path.includes('docs')) replaceText(document.querySelector('main') || document.body, /v\d+\.\d+\.\d+/g, `v${serverVersion}`);
    document.documentElement.dataset.serverRelease = server.tag_name;
    document.documentElement.dataset.androidRelease = android.tag_name;
  };
  sync().catch(() => {});
})();
