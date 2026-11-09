import { spawn } from 'node:child_process';
import path from 'node:path';

export function validateRequest(input) {
  if (!input || typeof input !== 'object') throw new Error('Pedido inválido.');
  const urls = Array.isArray(input.urls) ? input.urls : [];
  if (!urls.length || urls.length > 30) throw new Error('Cole entre 1 e 30 links.');
  const clean = [...new Set(urls.map(raw => {
    if (typeof raw !== 'string' || raw.length > 4096) throw new Error('Link inválido.');
    let url; try { url = new URL(raw.trim()); } catch { throw new Error('Link inválido: ' + raw); }
    if (!['http:', 'https:'].includes(url.protocol) || url.username || url.password) throw new Error('Use links HTTP ou HTTPS sem credenciais.');
    return url.href;
  }))];
  const mode = input.mode ?? 'video';
  if (!['video', 'audio'].includes(mode)) throw new Error('Tipo de download inválido.');
  const format = input.format ?? (mode === 'video' ? 'mp4' : 'mp3');
  if (!(mode === 'video' ? ['mp4', 'mkv'] : ['mp3', 'm4a', 'opus']).includes(format)) throw new Error('Formato inválido.');
  const quality = String(input.quality ?? '1080');
  if (!['best', '2160', '1440', '1080', '720', '480'].includes(quality)) throw new Error('Qualidade inválida.');
  const bitrate = String(input.bitrate ?? '192');
  if (!['128', '192', '256', '320'].includes(bitrate)) throw new Error('Taxa de áudio inválida.');
  const destination = input.destination ?? '';
  if (typeof destination !== 'string' || destination.length > 4096 || (destination && !path.isAbsolute(destination))) throw new Error('Selecione uma pasta de destino válida.');
  return { urls: clean, mode, format, quality, bitrate, destination, playlist: input.playlist === true, subtitles: input.subtitles === true };
}

export function baseArgs(binDir, settings = {}) {
  return ['--ignore-config', '--no-plugin-dirs', '--no-colors', '--encoding', 'utf-8',
    '--ffmpeg-location', binDir, '--js-runtimes', `node:${process.execPath}`,
    '--socket-timeout', String(settings.timeout ?? 25), '--retries', String(settings.retries ?? 3), '--fragment-retries', String(settings.retries ?? 3),
    ...(settings.proxyUrl ? ['--proxy', settings.proxyUrl] : []),
    ...(settings.cookiesFile ? ['--cookies', settings.cookiesFile] : [])];
}

export function buildArgs(job, binDir, destination, settings = {}) {
  const o = job.options;
  const args = [...baseArgs(binDir, settings), '--newline', '--progress', '--progress-delta', '0.5',
    '--progress-template', 'download:7K_PROGRESS:%(progress)j',
    '--print', 'before_dl:7K_TITLE:%(title)s', '--print', 'after_move:7K_FILE:%(filepath)s',
    '--windows-filenames', '--no-overwrites', settings.resume === false ? '--no-continue' : '--continue', '--no-mtime',
    '--concurrent-fragments', String(settings.fragmentThreads ?? 1),
    '-P', destination, '-o', '%(id)s/%(title).150B.%(ext)s'];
  if (o.playlist) args.push('--yes-playlist', '--playlist-end', '100');
  else args.push('--no-playlist');
  if (o.mode === 'audio') args.push('-f', 'bestaudio/best', '-x', '--audio-format', o.format, '--audio-quality', `${o.bitrate}K`, '--embed-metadata');
  else {
    const limit = o.quality === 'best' ? '' : `[height<=?${o.quality}]`;
    // A container named MP4 can contain AV1/VP9. Prefer the actual AVC/AAC
    // streams, even if the compatible rendition has a lower resolution.
    const avc = '[vcodec~="^(avc1|avc3|h264)"]';
    const aac = '[acodec~="^(mp4a|aac)"]';
    const premiere = `bv${limit}${avc}+ba${aac}/b${limit}${avc}${aac}/bv${limit}${avc}+ba/b${limit}${avc}/bv${limit}+ba/b${limit}`;
    args.push('-f', o.format === 'mp4' ? premiere : `bv${limit}+ba/b${limit}`,
      '--merge-output-format', o.format, '--remux-video', o.format);
  }
  if (o.subtitles) args.push('--write-subs', '--write-auto-subs', '--sub-langs', settings.subLanguages || 'pt.*,en.*', '--sub-format', 'srt/best', '--convert-subs', 'srt');
  if (settings.speedLimitKB > 0) args.push('--limit-rate', `${settings.speedLimitKB}K`);
  args.push('--', job.url);
  return args;
}

export function friendlyError(raw) {
  if (/sign in to confirm|not a bot|login required|authentication required/i.test(raw)) return {
    message: 'O site exige uma sessão autenticada. Selecione cookies.txt em Ferramentas → Preferências e tente novamente.',
    requiresAuth: true
  };
  return { message: raw, requiresAuth: false };
}

export function youtubeVideoId(raw) {
  let url; try { url = new URL(raw); } catch { return null; }
  const host = url.hostname.toLowerCase();
  const id = host === 'youtu.be' ? url.pathname.split('/')[1] : ['youtube.com', 'www.youtube.com', 'm.youtube.com', 'music.youtube.com'].includes(host)
    ? (url.searchParams.get('v') || (/^\/(shorts|live|embed)\//.test(url.pathname) ? url.pathname.split('/')[2] : null)) : null;
  return /^[\w-]{11}$/.test(id || '') ? id : null;
}

export function startProcess(engine, args) {
  return spawn(engine.file, [...(engine.prefix ?? []), ...args], { windowsHide: true, shell: false, stdio: ['ignore', 'pipe', 'pipe'] });
}

export function attachLines(stream, callback) {
  stream.setEncoding('utf8'); let buffer = '';
  stream.on('data', chunk => {
    buffer += chunk;
    const lines = buffer.split(/\r?\n/); buffer = lines.pop();
    for (const line of lines) callback(line.slice(0, 8192));
    if (buffer.length > 65536) buffer = buffer.slice(-8192);
  });
  stream.on('end', () => { if (buffer) callback(buffer.slice(0, 8192)); });
}

export function safeMediaPath(file, destination) {
  const absolute = path.resolve(file); const relative = path.relative(destination, absolute);
  if (!relative || relative.startsWith('..') || path.isAbsolute(relative) || !isAllowedMediaFile(absolute)) return null;
  return absolute;
}

export function isAllowedMediaFile(file) {
  return /\.(mp4|mkv|mp3|m4a|opus|webm|mov|avi|flv|ogg|wav|aac|srt|vtt|ass)$/i.test(file)
    && !path.basename(file).includes(':');
}

export function terminateProcess(child) {
  if (!child?.pid) return;
  if (process.platform === 'win32') {
    const killer = spawn('taskkill.exe', ['/PID', String(child.pid), '/T', '/F'], { windowsHide: true, stdio: 'ignore', shell: false });
    killer.on('error', () => child.kill());
    killer.on('exit', code => { if (code !== 0) child.kill(); });
  } else child.kill('SIGTERM');
}
