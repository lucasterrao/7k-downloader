import http from 'node:http';
import fs from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { constants } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { randomBytes, randomUUID, timingSafeEqual } from 'node:crypto';
import { spawn } from 'node:child_process';
import { ensurePremiereMp4 } from './lib/media.mjs';
import { validateRequest, buildArgs, baseArgs, startProcess, attachLines, safeMediaPath, isAllowedMediaFile, terminateProcess, friendlyError, youtubeVideoId } from './lib/engine.mjs';

const ROOT = path.dirname(fileURLToPath(import.meta.url));
const activeStates = new Set(['queued', 'running', 'processing']);

export async function createApp(config = {}) {
  const root = config.root ?? ROOT;
  const binDir = config.binDir ?? path.join(root, 'bin'); const destination = path.join(root, 'downloads');
  const dataDir = path.join(root, 'data'); const historyFile = path.join(dataDir, 'history.json');
  const token = config.token ?? randomBytes(32).toString('hex');
  const engine = config.engine ?? { file: path.join(binDir, 'yt-dlp.exe') };
  await fs.mkdir(destination, { recursive: true }); await fs.mkdir(dataDir, { recursive: true });
  const settingsFile = path.join(dataDir, 'auth-settings.json');
  const defaults = { cookiesFile: '', concurrency: 2, retries: 3, timeout: 25, speedLimitKB: 0, fragmentThreads: 1, proxyUrl: '', resume: true, subLanguages: 'pt.*,en.*' };
  let settings = { ...defaults };
  try { const saved = JSON.parse(await fs.readFile(settingsFile, 'utf8')); settings = { ...defaults, ...saved }; } catch {}
  async function saveSettings(input) {
    const file = input.cookiesFile ?? '';
    if (typeof file !== 'string' || file.length > 4096 || (file && !path.isAbsolute(file))) throw new Error('Selecione um arquivo cookies.txt válido.');
    if (file) {
      const stat = await fs.stat(file).catch(() => null);
      if (!stat?.isFile() || stat.size > 20_000_000) throw new Error('Arquivo de cookies inexistente ou muito grande.');
      const handle = await fs.open(file, 'r'); const buffer = Buffer.alloc(512);
      try { await handle.read(buffer, 0, buffer.length, 0); } finally { await handle.close(); }
      if (!/^# (Netscape HTTP Cookie File|HTTP Cookie File)/.test(buffer.toString('utf8').replace(/^\uFEFF/, ''))) throw new Error('Use um arquivo de cookies exportado no formato Netscape.');
    }
    const updated = { ...settings, cookiesFile: file ? path.resolve(file) : '' };
    for (const [name, min, max] of [['concurrency',1,8],['retries',0,10],['timeout',10,120],['speedLimitKB',0,100000],['fragmentThreads',1,8]]) {
      const value = input[name] ?? settings[name];
      if (!Number.isInteger(value) || value < min || value > max) throw new Error('Valor inválido para ' + name + '.'); updated[name] = value;
    }
    const proxy = input.proxyUrl ?? settings.proxyUrl;
    if (typeof proxy !== 'string' || proxy.length > 4096) throw new Error('Proxy inválido.');
    if (proxy) { let u; try { u = new URL(proxy); } catch { throw new Error('Use uma URL de proxy válida.'); } if (!['http:','https:','socks5:','socks4:'].includes(u.protocol) || u.username || u.password) throw new Error('Use proxy HTTP/HTTPS/SOCKS sem credenciais na URL.'); }
    const langs = input.subLanguages ?? settings.subLanguages;
    if (!['pt.*,en.*','pt.*','en.*','all'].includes(langs)) throw new Error('Idioma de legendas inválido.');
    updated.proxyUrl = proxy; updated.subLanguages = langs; updated.resume = input.resume ?? settings.resume;
    if (typeof updated.resume !== 'boolean') throw new Error('Opção de retomada inválida.');
    settings = updated;
    await fs.writeFile(settingsFile, JSON.stringify(settings, null, 2)); schedule(); return settings;
  }
  let jobs = []; let closing = false; const children = new Map(); const probes = new Set(); const conversionSignals = new Map();
  try {
    jobs = JSON.parse(await fs.readFile(historyFile, 'utf8'));
    if (!Array.isArray(jobs)) jobs = [];
    jobs = jobs.slice(-200).filter(j => j && typeof j.id === 'string' && j.options);
    for (const job of jobs) {
      if (activeStates.has(job.status)) { job.status = 'interrupted'; job.message = 'Interrompido ao fechar o aplicativo. Use Tentar novamente.'; }
      else if (job.status === 'failed') Object.assign(job, friendlyError(job.message || 'Download não concluído.'));
    }
  } catch { jobs = []; }
  let persistChain = Promise.resolve();
  function persist() {
    const snapshot = JSON.stringify(jobs, null, 2);
    persistChain = persistChain.then(async () => {
      await fs.writeFile(historyFile + '.tmp', snapshot); await fs.rename(historyFile + '.tmp', historyFile);
    }).catch(error => console.error('Não foi possível salvar o histórico:', error.message));
    return persistChain;
  }
  function ready() { return config.engine ? true : existsSync(engine.file) && existsSync(path.join(binDir, 'ffmpeg.exe')) && existsSync(path.join(binDir, 'ffprobe.exe')); }
  function schedule() {
    if (closing) return;
    while (children.size < settings.concurrency) {
      const job = jobs.find(j => j.status === 'queued'); if (!job) break;
      run(job);
    }
  }
  function run(job) {
    job.status = 'running'; job.message = 'Consultando o site…'; job.progress = 0; job.startedAt = new Date().toISOString();
    const jobDirectory = path.join(dataDir, 'work', job.id);
    job.destination = job.options.destination || destination;
    const producedFiles = [];
    const conversion = new AbortController(); conversionSignals.set(job.id, conversion);
    const child = startProcess(engine, buildArgs(job, binDir, jobDirectory, settings)); children.set(job.id, child); persist();
    let lastError = ''; let finished = false;
    const lineHandler = line => {
      if (job.status === 'canceled') return;
      if (line.startsWith('7K_TITLE:')) { job.title = line.slice(9); job.titleSource = 'extractor'; }
      else if (line.startsWith('7K_FILE:')) {
        const file = safeMediaPath(line.slice(8).trim(), jobDirectory);
        if (file && !producedFiles.includes(file)) producedFiles.push(file);
      } else if (line.startsWith('7K_PROGRESS:')) {
        try {
          const p = JSON.parse(line.slice(12)); const total = p.total_bytes || p.total_bytes_estimate;
          job.progress = total ? Math.min(100, Math.max(0, p.downloaded_bytes / total * 100)) : 0;
          job.speed = Number(p.speed) || 0; job.eta = Number(p.eta) || 0;
          job.status = p.status === 'finished' ? 'processing' : 'running';
          job.message = p.status === 'finished' ? 'Preparando o arquivo final…' : 'Baixando…';
        } catch { /* Non-JSON progress is ignored. */ }
      } else if (line.startsWith('ERROR:')) { lastError = line.slice(6).trim(); Object.assign(job, friendlyError(lastError)); }
      else if (/^\[(Merger|ExtractAudio|VideoRemuxer|Metadata|SubtitlesConvertor)\]/.test(line)) { job.status = 'processing'; job.message = 'Juntando as faixas e finalizando…'; }
      if (line.trim()) { job.logs.push(line); if (job.logs.length > 60) job.logs.shift(); }
    };
    attachLines(child.stdout, lineHandler); attachLines(child.stderr, lineHandler);
    async function finish(code, error) {
      if (finished) return; finished = true;
      if (code === 0 && job.status !== 'canceled' && job.status !== 'interrupted') {
        job.status = 'processing'; job.message = 'Salvando na pasta escolhida…';
        try {
          await fs.mkdir(job.destination, { recursive: true });
          if (!producedFiles.length) throw new Error('O motor não informou o arquivo final. Consulte os detalhes.');
          for (const file of producedFiles) {
            if (job.status === 'canceled' || job.status === 'interrupted') break;
            let compatible = file;
            if (job.options.mode === 'video' && job.options.format === 'mp4' && !config.engine) {
              job.message = 'Verificando compatibilidade com Premiere…';
              compatible = await ensurePremiereMp4(file, { binDir, signal: conversion.signal,
                onProcess(process) { children.set(job.id, process); },
                onProgress(percent) { job.message = `Convertendo para H.264/AAC: ${percent.toFixed(0)}%`; job.progress = percent; }
              });
            }
            if (job.status === 'canceled' || job.status === 'interrupted') break;
            const media = await saveFlat(compatible, job.destination); if (!job.files.includes(media)) job.files.push(media);
            if (job.options.subtitles) {
              const stem = path.basename(file, path.extname(file)); const directory = path.dirname(file);
              for (const entry of await fs.readdir(directory)) if (entry.startsWith(stem + '.') && /\.(srt|vtt|ass)$/.test(entry)) await saveFlat(path.join(directory, entry), job.destination);
            }
          }
          if (job.status === 'processing' && path.dirname(path.resolve(jobDirectory)) === path.resolve(dataDir, 'work')) await fs.rm(jobDirectory, { recursive: true, force: true });
        } catch (e) { code = -1; error = e; }
      }
      children.delete(job.id);
      conversionSignals.delete(job.id);
      if (job.status !== 'canceled' && job.status !== 'interrupted') {
        job.status = code === 0 ? 'completed' : 'failed';
        if (code === 0) { job.message = 'Download concluído.'; job.requiresAuth = false; }
        else Object.assign(job, friendlyError(error?.message || lastError || 'O site não forneceu o arquivo. Consulte os detalhes e tente novamente.'));
        if (code === 0) job.progress = 100;
      }
      job.speed = 0; job.finishedAt = new Date().toISOString(); persist(); schedule();
    }
    child.on('error', error => finish(-1, error)); child.on('close', code => finish(code));
  }
  async function saveFlat(source, folder) {
    if (!isAllowedMediaFile(source)) throw new Error('Tipo de arquivo bloqueado: somente mídia e legendas são permitidas.');
    const sourceInfo = await fs.lstat(source);
    if (!sourceInfo.isFile() || sourceInfo.isSymbolicLink()) throw new Error('Arquivo de mídia inválido.');
    const name = path.basename(source); const extension = path.extname(name); const stem = path.basename(name, extension);
    for (let n = 0; n < 10000; n++) {
      const target = path.join(folder, n ? `${stem} (${n + 1})${extension}` : name);
      try { await fs.copyFile(source, target, constants.COPYFILE_EXCL); return target; }
      catch (e) { if (e.code !== 'EEXIST') throw e; }
    }
    throw new Error('Não foi possível escolher um nome livre na pasta de destino.');
  }
  async function metadata(url) {
    const child = startProcess(engine, [...baseArgs(binDir, settings), '--no-playlist', '--skip-download', '--dump-single-json', '--', url]);
    probes.add(child);
    return new Promise((resolve, reject) => {
      let output = ''; let error = ''; let settled = false;
      const done = (err, value) => { if (settled) return; settled = true; clearTimeout(timer); probes.delete(child); err ? reject(err) : resolve(value); };
      const timer = setTimeout(() => { terminateProcess(child); done(new Error('A consulta demorou demais. Você pode tentar baixar diretamente.')); }, 60000);
      child.stdout.setEncoding('utf8'); child.stderr.setEncoding('utf8');
      child.stdout.on('data', chunk => { output += chunk; if (output.length > 6_000_000) { terminateProcess(child); done(new Error('Resposta do site muito grande.')); } });
      child.stderr.on('data', chunk => { error = (error + chunk).slice(-3000); });
      child.on('error', err => done(err));
      child.on('close', code => {
        if (settled) return;
        if (code !== 0) return done(new Error(friendlyError(error.trim() || 'Não foi possível consultar esse link.').message));
        try {
          const info = JSON.parse(output);
          done(null, { title: info.title, duration: info.duration, uploader: info.uploader || info.channel,
            site: info.extractor_key, heights: [...new Set((info.formats || []).map(f => f.height).filter(Number.isFinite))].sort((a, b) => b - a) });
        } catch { done(new Error('Resposta de metadados inválida.')); }
      });
    });
  }
  const titleQueue = []; let titleWorkers = 0;
  const titleAbort = new AbortController(); const titleCache = new Map(); const pendingTitles = new Map();
  function lookupTitle(link) {
    if (titleCache.has(link)) return Promise.resolve(titleCache.get(link));
    if (pendingTitles.has(link)) return pendingTitles.get(link);
    const pending = fetchTitle(link).finally(() => pendingTitles.delete(link)); pendingTitles.set(link, pending); return pending;
  }
  async function fetchTitle(link) {
    if (titleCache.has(link)) return titleCache.get(link);
    let title = '';
    if (youtubeVideoId(link) && !config.engine) {
      try {
        const endpoint = new URL('https://www.youtube.com/oembed');
        endpoint.searchParams.set('url', `https://www.youtube.com/watch?v=${youtubeVideoId(link)}`); endpoint.searchParams.set('format', 'json');
        const response = await fetch(endpoint, { signal: AbortSignal.any([titleAbort.signal, AbortSignal.timeout(10000)]) });
        if (response.ok) { const info = await response.json(); if (typeof info.title === 'string') title = info.title; }
      } catch {}
    }
    if (!title && !closing) try { title = (await metadata(link)).title || ''; } catch {}
    if (title) titleCache.set(link, title); return title;
  }
  function queueTitle(job) {
    if (job.title && !['pending', 'unavailable'].includes(job.titleSource)) return;
    job.title = 'Consultando título…'; job.titleSource = 'pending'; titleQueue.push(job); drainTitles();
  }
  function drainTitles() {
    while (!closing && titleWorkers < 2 && titleQueue.length) {
      const job = titleQueue.shift(); titleWorkers++;
      lookupTitle(job.url).then(title => {
        if (closing || job.titleSource === 'extractor') return;
        job.title = title || (job.options.playlist ? 'Playlist — título indisponível' : 'Vídeo ou áudio — título indisponível');
        job.titleSource = title ? 'metadata' : 'unavailable'; persist();
      }).finally(() => { titleWorkers--; drainTitles(); });
    }
  }
  let port = 0;
  const json = (res, status, body) => { res.writeHead(status, { 'Content-Type': 'application/json; charset=utf-8', 'Cache-Control': 'no-store' }); res.end(JSON.stringify(body)); };
  async function body(req) {
    let text = ''; for await (const chunk of req) { text += chunk; if (text.length > 150000) throw new Error('Pedido muito grande.'); }
    try { return JSON.parse(text); } catch { throw new Error('JSON inválido.'); }
  }
  const server = http.createServer(async (req, res) => {
    try {
      if (req.headers.host !== `127.0.0.1:${port}`) return json(res, 403, { error: 'Host inválido.' });
      const origin = req.headers.origin;
      if (origin && origin !== `http://127.0.0.1:${port}`) return json(res, 403, { error: 'Origem inválida.' });
      res.setHeader('X-Content-Type-Options', 'nosniff'); res.setHeader('Referrer-Policy', 'no-referrer');
      res.setHeader('Content-Security-Policy', "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'");
      const url = new URL(req.url, `http://127.0.0.1:${port}`);
      if (url.pathname.startsWith('/api/')) {
        const supplied = Buffer.from(req.headers['x-7k-token'] || ''); const expected = Buffer.from(token);
        if (supplied.length !== expected.length || !timingSafeEqual(supplied, expected)) return json(res, 403, { error: 'Sessão inválida. Abra pelo executável 7K Downloader.' });
        if (req.method === 'GET' && url.pathname === '/api/status') return json(res, 200, { ready: ready(), destination, jobs, concurrency: settings.concurrency, apiVersion: 6 });
        if (req.method === 'GET' && url.pathname === '/api/settings') return json(res, 200, settings);
        if (req.method === 'POST' && url.pathname === '/api/settings') return json(res, 200, await saveSettings(await body(req)));
        if (req.method === 'POST' && url.pathname === '/api/jobs') {
          if (!ready()) return json(res, 503, { error: 'Instale os componentes executando Instalar componentes.cmd.' });
          const options = validateRequest(await body(req));
          if (jobs.filter(j => activeStates.has(j.status)).length + options.urls.length > 100) throw new Error('A fila aceita até 100 tarefas.');
          const created = options.urls.map(link => ({ id: randomUUID(), url: link, title: '', options, status: 'queued', progress: 0, message: 'Na fila', files: [], logs: [], createdAt: new Date().toISOString() }));
          jobs.push(...created); for (const job of created) queueTitle(job); await persist(); schedule(); return json(res, 201, { ids: created.map(j => j.id) });
        }
        if (req.method === 'POST' && url.pathname === '/api/preview') {
          if (!ready()) return json(res, 503, { error: 'Instale os componentes primeiro.' });
          if (probes.size >= 2) throw new Error('Aguarde as consultas em andamento.');
          const options = validateRequest({ ...(await body(req)), mode: 'video', format: 'mp4' });
          return json(res, 200, await metadata(options.urls[0]));
        }
        if (req.method === 'POST' && url.pathname === '/api/clear') { jobs = jobs.filter(j => activeStates.has(j.status)); await persist(); return json(res, 200, { ok: true }); }
        if (req.method === 'POST' && url.pathname === '/api/open-folder') {
          if (process.platform === 'win32' && !config.noOpen) spawn('explorer.exe', [destination], { windowsHide: true, stdio: 'ignore' }).on('error', () => {});
          return json(res, 200, { ok: true, destination });
        }
        if (req.method === 'POST' && url.pathname === '/api/shutdown') { json(res, 200, { ok: true }); setTimeout(() => app.close(), 100); return; }
        const match = url.pathname.match(/^\/api\/jobs\/([\w-]+)\/(cancel|retry|remove)$/);
        if (req.method === 'POST' && match) {
          const job = jobs.find(j => j.id === match[1]); if (!job) return json(res, 404, { error: 'Tarefa não encontrada.' });
          if (match[2] === 'remove') {
            if (activeStates.has(job.status) || children.has(job.id)) throw new Error('Cancele e aguarde a tarefa parar antes de removê-la.');
            jobs = jobs.filter(j => j.id !== job.id); await persist(); return json(res, 200, { ok: true });
          } else if (match[2] === 'cancel') {
            if (activeStates.has(job.status)) { job.status = 'canceled'; job.message = 'Cancelado. Arquivos parciais podem ser retomados ao tentar novamente.'; conversionSignals.get(job.id)?.abort(); terminateProcess(children.get(job.id)); }
          } else {
            if (children.has(job.id) || activeStates.has(job.status)) throw new Error('Aguarde a tarefa parar antes de tentar novamente.');
            if (!ready()) throw new Error('Componentes indisponíveis.');
            const retry = await body(req);
            if (retry.destination !== undefined) job.options.destination = validateRequest({ ...job.options, urls: [job.url], destination: retry.destination }).destination;
            job.status = 'queued'; job.progress = 0; job.logs = []; job.files = []; job.message = 'Na fila'; job.finishedAt = null; job.requiresAuth = false;
            queueTitle(job);
          }
          await persist(); schedule(); return json(res, 200, { ok: true });
        }
        return json(res, 404, { error: 'Operação não encontrada.' });
      }
      if (req.method !== 'GET') return json(res, 405, { error: 'Método inválido.' });
      const files = { '/': ['index.html', 'text/html'], '/app.js': ['app.js', 'text/javascript'], '/style.css': ['style.css', 'text/css'] };
      const target = files[url.pathname]; if (!target) return json(res, 404, { error: 'Página não encontrada.' });
      res.writeHead(200, { 'Content-Type': target[1] + '; charset=utf-8', 'Cache-Control': 'no-store' });
      res.end(await fs.readFile(path.join(ROOT, 'public', target[0])));
    } catch (error) { if (!res.headersSent) json(res, 400, { error: error.message }); else res.end(); }
  });
  server.requestTimeout = 30000;
  server.headersTimeout = 10000;
  server.maxHeadersCount = 50;
  const app = { server, token, jobs: () => jobs, destination,
    async listen() { await new Promise((resolve, reject) => { server.once('error', reject); server.listen(config.port ?? 0, '127.0.0.1', resolve); }); port = server.address().port; for (const job of jobs) if (!job.title || ['pending', 'unavailable'].includes(job.titleSource)) queueTitle(job); return `http://127.0.0.1:${port}/#${token}`; },
    async close() {
      if (closing) return; closing = true;
      titleAbort.abort(); titleQueue.length = 0;
      for (const signal of conversionSignals.values()) signal.abort();
      for (const job of jobs) if (activeStates.has(job.status)) { job.status = 'interrupted'; job.message = 'Aplicativo encerrado. Use Tentar novamente para retomar.'; }
      for (const child of [...children.values(), ...probes]) terminateProcess(child);
      await persist(); server.close(); server.closeAllConnections();
      if (!config.testing) await fs.unlink(path.join(dataDir, 'session.json')).catch(() => {});
    }
  };
  return app;
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const app = await createApp(); const address = await app.listen();
  await fs.writeFile(path.join(ROOT, 'data', 'session.json'), JSON.stringify({ pid: process.pid, url: address }));
  console.log('Motor do 7K Downloader iniciado. Abra 7K Downloader.exe para usar a janela do Windows.');
  process.on('SIGINT', () => app.close()); process.on('SIGTERM', () => app.close());
}
