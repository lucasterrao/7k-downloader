const $ = id => document.getElementById(id);
let token = location.hash.slice(1) || sessionStorage.getItem('7k-token') || '';
if (token) sessionStorage.setItem('7k-token', token);
history.replaceState(null, '', '/');
let mode = 'video'; let filter = 'all'; let latest = null; let stopped = false; let busy = false;
const expanded = new Set();
const active = ['queued', 'running', 'processing'];
const labels = { queued: 'Na fila', running: 'Baixando', processing: 'Finalizando', completed: 'Concluído', canceled: 'Cancelado', failed: 'Falhou', interrupted: 'Interrompido' };
let toastTimer;
function toast(message) { $('toast').textContent = message; $('toast').hidden = false; clearTimeout(toastTimer); toastTimer = setTimeout(() => $('toast').hidden = true, 6500); }
async function api(route, data) {
  const response = await fetch('/api/' + route, { method: data === undefined ? 'GET' : 'POST', headers: { 'X-7K-Token': token, ...(data === undefined ? {} : { 'Content-Type': 'application/json' }) }, body: data === undefined ? undefined : JSON.stringify(data) });
  const result = await response.json(); if (!response.ok) throw new Error(result.error || 'Erro no aplicativo.'); return result;
}
function links() { return $('urls').value.split(/\r?\n/).map(v => v.trim()).filter(Boolean); }
$('urls').addEventListener('input', () => { $('link-count').textContent = `${links().length} links`; $('preview-result').hidden = true; });
$('paste').addEventListener('click', async () => { try { const text = await navigator.clipboard.readText(); $('urls').value += ($('urls').value.trim() ? '\n' : '') + text; $('urls').dispatchEvent(new Event('input')); } catch { toast('Use Ctrl + V no campo de links.'); $('urls').focus(); } });
document.querySelectorAll('[data-mode]').forEach(button => button.addEventListener('click', () => {
  mode = button.dataset.mode;
  document.querySelectorAll('[data-mode]').forEach(b => { b.classList.toggle('selected', b === button); b.setAttribute('aria-pressed', String(b === button)); });
  $('quality-field').hidden = mode === 'audio'; $('bitrate-field').hidden = mode === 'video';
  $('format').replaceChildren();
  const choices = mode === 'video' ? [['mp4', 'MP4 · ampla compatibilidade'], ['mkv', 'MKV · qualidade e flexibilidade']] : [['mp3', 'MP3 · ampla compatibilidade'], ['m4a', 'M4A · áudio AAC'], ['opus', 'Opus · áudio eficiente']];
  for (const [value, text] of choices) { const option = document.createElement('option'); option.value = value; option.textContent = text; $('format').append(option); }
}));
$('preview').addEventListener('click', async () => {
  if (!links().length) return toast('Cole um link primeiro.');
  $('preview').disabled = true; $('preview').textContent = 'Consultando…';
  try {
    const info = await api('preview', { urls: [links()[0]] }); const block = $('preview-result'); block.replaceChildren();
    const title = document.createElement('strong'); title.textContent = info.title || 'Conteúdo encontrado';
    const sub = document.createElement('small'); sub.textContent = [info.site, info.uploader, info.duration ? `${Math.round(info.duration / 60)} min` : '', info.heights?.length ? info.heights.map(h => `${h}p`).join(' · ') : ''].filter(Boolean).join(' · ');
    block.append(title, sub); block.hidden = false;
  } catch (error) { toast(error.message); } finally { $('preview').disabled = false; $('preview').textContent = 'Consultar primeiro link ↗'; }
});
$('download-form').addEventListener('submit', async event => {
  event.preventDefault(); if (busy) return; busy = true; $('add').disabled = true;
  try {
    const result = await api('jobs', { urls: links(), mode, format: $('format').value, quality: $('quality').value, bitrate: $('bitrate').value, playlist: $('playlist').checked, subtitles: $('subtitles').checked });
    $('urls').value = ''; $('urls').dispatchEvent(new Event('input')); toast(`${result.ids.length} tarefa(s) adicionada(s).`); await refresh();
  } catch (error) { toast(error.message); } finally { busy = false; $('add').disabled = !latest?.ready; }
});
document.querySelectorAll('[data-filter]').forEach(button => button.addEventListener('click', () => { filter = button.dataset.filter; document.querySelectorAll('[data-filter]').forEach(b => b.classList.toggle('selected', b === button)); render(); }));
$('open-folder').addEventListener('click', () => api('open-folder', {}).catch(e => toast(e.message)));
$('clear').addEventListener('click', async () => { try { await api('clear', {}); await refresh(); } catch (e) { toast(e.message); } });
$('shutdown').addEventListener('click', async () => {
  if (latest?.jobs.some(j => active.includes(j.status)) && !confirm('Encerrar e interromper os downloads em andamento? Você poderá retomá-los depois.')) return;
  try { await api('shutdown', {}); stopped = true; $('download-form').querySelectorAll('button').forEach(b => b.disabled = true); $('shutdown').disabled = true; toast('Aplicativo encerrado. Você pode fechar esta aba.'); } catch (e) { toast(e.message); }
});
function element(tag, className, text) { const el = document.createElement(tag); if (className) el.className = className; if (text !== undefined) el.textContent = text; return el; }
function action(text, handler) { const b = element('button', 'quiet', text); b.type = 'button'; b.addEventListener('click', handler); return b; }
function render() {
  if (!latest) return;
  $('dependency').hidden = latest.ready; $('add').disabled = busy || !latest.ready;
  $('destination').textContent = latest.destination; $('total').textContent = latest.jobs.length;
  $('running-count').textContent = latest.jobs.filter(j => ['running', 'processing'].includes(j.status)).length;
  $('done-count').textContent = latest.jobs.filter(j => j.status === 'completed').length;
  $('waiting-count').textContent = latest.jobs.filter(j => j.status === 'queued').length;
  const shown = latest.jobs.filter(j => filter === 'all' || (filter === 'active' ? active.includes(j.status) : !active.includes(j.status))).slice().reverse();
  const container = $('jobs'); const scroll = container.scrollTop; container.replaceChildren(); $('empty').hidden = shown.length > 0;
  for (const job of shown) {
    const card = element('article', 'job'); const head = element('div', 'job-head');
    head.append(element('div', 'job-icon', job.options.mode === 'audio' ? '♫' : '▸'));
    const info = element('div', 'job-info'); info.append(element('p', 'job-title', job.title || job.url));
    const meta = element('div', 'job-meta'); let site = ''; try { site = new URL(job.url).hostname; } catch { site = 'Link'; }
    meta.append(element('span', '', site), element('span', '', job.options.format.toUpperCase()), element('span', '', job.options.mode === 'audio' ? `${job.options.bitrate} kbps` : job.options.quality === 'best' ? 'Melhor disponível' : `${job.options.quality}p`));
    info.append(meta); head.append(info, element('span', `badge ${job.status}`, labels[job.status] || job.status)); card.append(head);
    if (['running', 'processing'].includes(job.status)) { const bar = document.createElement('progress'); bar.max = 100; bar.value = job.progress || 0; bar.setAttribute('aria-label', 'Progresso do download'); card.append(bar); }
    const progress = job.status === 'running' ? `${(job.progress || 0).toFixed(1)}%${job.speed ? ' · ' + (job.speed / 1048576).toFixed(1) + ' MB/s' : ''}${job.eta ? ' · ' + Math.ceil(job.eta) + 's restantes' : ''}` : '';
    card.append(element('p', 'job-message', progress || job.message));
    const actions = element('div', 'job-actions');
    if (active.includes(job.status)) actions.append(action('Cancelar', async () => { try { await api(`jobs/${job.id}/cancel`, {}); await refresh(); } catch (e) { toast(e.message); } }));
    else if (job.status !== 'completed') actions.append(action('Tentar novamente', async () => { try { await api(`jobs/${job.id}/retry`, {}); await refresh(); } catch (e) { toast(e.message); } }));
    if (job.status === 'completed') actions.append(action('Abrir pasta', () => api('open-folder', {}).catch(e => toast(e.message))));
    card.append(actions);
    if (job.files?.length) card.append(element('div', 'job-files', `${job.files.length} arquivo(s) · ${job.files.map(f => f.split(/[\\/]/).pop()).join(' · ')}`));
    if (job.logs?.length) {
      const details = document.createElement('details'); details.open = expanded.has(job.id);
      details.append(element('summary', '', 'Detalhes técnicos'), element('pre', '', job.logs.join('\n')));
      details.addEventListener('toggle', () => { if (details.isConnected) details.open ? expanded.add(job.id) : expanded.delete(job.id); }); card.append(details);
    }
    container.append(card);
  }
  container.scrollTop = scroll;
}
let failures = 0;
async function refresh() {
  if (stopped) return;
  try { latest = await api('status'); failures = 0; render(); } catch (error) { if (++failures === 1) toast(error.message + ' Se o aplicativo foi fechado, abra o executável novamente.'); }
}
async function poll() { await refresh(); if (!stopped) setTimeout(poll, 1500); }
poll();
