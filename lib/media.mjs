import { spawn } from 'node:child_process';
import fs from 'node:fs/promises';
import path from 'node:path';
import { randomUUID } from 'node:crypto';
import { constants } from 'node:fs';

function execute(file, args, options = {}) {
  return new Promise((resolve, reject) => {
    let child; try { child = spawn(file, args, { windowsHide: true, shell: false, signal: options.signal, stdio: ['ignore', 'pipe', 'pipe'] }); } catch (e) { reject(e); return; }
    options.onProcess?.(child);
    let output = '', errors = '', pending = '';
    child.stdout.setEncoding('utf8'); child.stderr.setEncoding('utf8');
    child.stdout.on('data', chunk => {
      output = (output + chunk).slice(-2_000_000); pending += chunk;
      const lines = pending.split(/\r?\n/); pending = lines.pop();
      for (const line of lines) options.onLine?.(line);
    });
    child.stderr.on('data', chunk => { errors = (errors + chunk).slice(-8000); });
    child.on('error', reject);
    child.on('close', code => code === 0 ? resolve(output) : reject(new Error(errors.trim() || `Conversão interrompida (código ${code}).`)));
  });
}

export async function probeMedia(file, binDir, options = {}) {
  return JSON.parse(await execute(path.join(binDir, 'ffprobe.exe'), ['-v', 'error', '-show_streams', '-show_format', '-of', 'json', file], options));
}
export function isPremiereCompatible(info) {
  const video = info.streams?.find(s => s.codec_type === 'video');
  return video?.codec_name === 'h264' && video.pix_fmt === 'yuv420p' && (info.streams?.filter(s => s.codec_type === 'audio') || []).every(s => s.codec_name === 'aac');
}
export async function ensurePremiereMp4(source, options) {
  const info = await probeMedia(source, options.binDir, options);
  const video = info.streams?.find(s => s.codec_type === 'video');
  if (!video) throw new Error('O arquivo baixado não contém uma faixa de vídeo.');
  if (isPremiereCompatible(info) && !options.output) return source;
  const output = options.output || path.join(path.dirname(source), 'premiere', path.basename(source, path.extname(source)) + '.mp4');
  if (path.resolve(source) === path.resolve(output)) throw new Error('A conversão deve gerar uma cópia, sem substituir o original.');
  await fs.mkdir(path.dirname(output), { recursive: true });
  if (await fs.stat(output).catch(() => null)) {
    if (!options.output) {
      const existing = await probeMedia(output, options.binDir, options).catch(() => null);
      if (existing && isPremiereCompatible(existing) && Math.abs(Number(existing.format?.duration) - Number(info.format?.duration)) < 2) return output;
    }
    throw new Error('A cópia de destino já existe. Escolha outro nome para preservar o arquivo existente.');
  }
  const temporary = output + '.' + randomUUID() + '.converting.mp4';
  const rate = video.avg_frame_rate || video.r_frame_rate;
  const parts = String(rate).split('/').map(Number); const fps = parts.length === 2 ? parts[0] / parts[1] : parts[0];
  const duration = Number(info.format?.duration) || 0;
  const audio = info.streams?.find(s => s.codec_type === 'audio');
  const copyVideo = video.codec_name === 'h264' && video.pix_fmt === 'yuv420p';
  const args = ['-hide_banner', '-loglevel', 'error', '-nostdin', '-n', '-i', source,
    '-map', '0:v:0', '-map', '0:a:0?', '-map_metadata', '0',
    ...(copyVideo ? ['-c:v', 'copy'] : ['-c:v', 'libx264', '-preset', 'fast', '-crf', '18', '-threads', '4',
      '-vf', 'pad=ceil(iw/2)*2:ceil(ih/2)*2', '-pix_fmt', 'yuv420p',
      ...(Number.isFinite(fps) && fps > 0 && fps <= 120 ? ['-r', rate, '-fps_mode', 'cfr'] : [])]),
    ...(audio?.codec_name === 'aac' ? ['-c:a', 'copy'] : ['-c:a', 'aac', '-b:a', '192k', '-ar', '48000']),
    '-movflags', '+faststart', '-progress', 'pipe:1', temporary];
  await execute(path.join(options.binDir, 'ffmpeg.exe'), args, { ...options,
    onLine(line) { const time = line.startsWith('out_time_us=') ? Number(line.slice(12)) : NaN; if (duration && Number.isFinite(time) && time >= 0) options.onProgress?.(Math.min(99, time / 1_000_000 / duration * 100)); }
  });
  const verified = await probeMedia(temporary, options.binDir, options);
  if (!isPremiereCompatible(verified)) throw new Error('A validação do arquivo H.264/AAC falhou.');
  const actualDuration = Number(verified.format?.duration);
  if (duration && (!Number.isFinite(actualDuration) || Math.abs(actualDuration - duration) > 2)) throw new Error('A duração da cópia convertida não corresponde ao original.');
  if (options.signal?.aborted) throw new Error('Conversão cancelada.');
  await fs.copyFile(temporary, output, constants.COPYFILE_EXCL);
  await fs.unlink(temporary);
  return output;
}
