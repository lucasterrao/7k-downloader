// Teste real, sem acessar sites externos: cria mídia própria e a serve por HTTP local.
import fs from 'node:fs/promises';
import path from 'node:path';
import http from 'node:http';
import assert from 'node:assert/strict';
import { promisify } from 'node:util';
import { execFile } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { createApp } from '../server.mjs';
const run = promisify(execFile);
const project = path.dirname(path.dirname(fileURLToPath(import.meta.url)));
const root = path.join(project, '.qa'); const binDir = path.join(project, 'bin');
await fs.mkdir(root, { recursive: true });
const source = path.join(root, 'amostra-propria.mp4');
await run(path.join(binDir, 'ffmpeg.exe'), ['-hide_banner', '-loglevel', 'error', '-y', '-f', 'lavfi', '-i', 'testsrc2=size=320x180:rate=24', '-f', 'lavfi', '-i', 'sine=frequency=440:sample_rate=44100', '-t', '2', '-c:v', 'libvpx-vp9', '-pix_fmt', 'yuv420p', '-c:a', 'aac', source], { windowsHide: true });
const bytes = await fs.readFile(source);
const compatibleSource = path.join(root, 'amostra-h264.mp4');
await run(path.join(binDir, 'ffmpeg.exe'), ['-v', 'error', '-y', '-i', source, '-c:v', 'libx264', '-pix_fmt', 'yuv420p', '-c:a', 'copy', compatibleSource], { windowsHide: true });
const compatibleBytes = await fs.readFile(compatibleSource);
const fixture = http.createServer((req, res) => {
  const body = req.url === '/amostra-propria.mp4' ? bytes : req.url === '/amostra-h264.mp4' ? compatibleBytes : null;
  if (!body) { res.writeHead(404); res.end(); return; }
  res.writeHead(200, { 'Content-Type': 'video/mp4', 'Content-Length': body.length }); res.end(body);
});
await new Promise(resolve => fixture.listen(0, '127.0.0.1', resolve));
const app = await createApp({ root, binDir, testing: true, noOpen: true });
const url = new URL(await app.listen()); const headers = { 'x-7k-token': app.token, 'Content-Type': 'application/json' };
const request = async (route, data) => {
  const res = await fetch(url.origin + '/api/' + route, { method: data === undefined ? 'GET' : 'POST', headers, body: data === undefined ? undefined : JSON.stringify(data) });
  const result = await res.json(); if (!res.ok) throw new Error(result.error); return result;
};
try {
  await request('clear', {});
  const link = `http://127.0.0.1:${fixture.address().port}/amostra-propria.mp4`;
  const preview = await request('preview', { urls: [link] }); console.log('Consulta real:', preview.title);
  const ids = [];
  for (const options of [{ mode: 'video', format: 'mp4', quality: '1080' }, { mode: 'audio', format: 'mp3', bitrate: '192' }, { mode: 'video', format: 'mp4', quality: '1080', urls: [link.replace('amostra-propria', 'amostra-h264')] }]) {
    const result = await request('jobs', { urls: [link], ...options }); ids.push(...result.ids);
  }
  let done;
  for (let i = 0; i < 180; i++) {
    const state = await request('status'); const selected = state.jobs.filter(j => ids.includes(j.id));
    if (selected.some(j => j.status === 'failed')) throw new Error(selected.filter(j => j.status === 'failed').map(j => j.logs.join('\n')).join('\n'));
    if (selected.length === 3 && selected.every(j => j.status === 'completed')) { done = selected; break; }
    await new Promise(resolve => setTimeout(resolve, 500));
  }
  assert.ok(done, 'Os downloads devem concluir em até 90 segundos.');
  for (const job of done) {
    assert.ok(job.files.length);
    for (const file of job.files) {
      assert.ok((await fs.stat(file)).size > 0);
      if (job.url.endsWith('/amostra-h264.mp4')) assert.deepEqual(await fs.readFile(file), compatibleBytes, 'MP4 compatível deve ser entregue sem conversão, byte a byte.');
      const { stdout } = await run(path.join(binDir, 'ffprobe.exe'), ['-v', 'error', '-show_entries', 'format=duration:stream=codec_type,codec_name,pix_fmt,width,height,avg_frame_rate', '-of', 'json', file], { windowsHide: true });
      const info = JSON.parse(stdout); assert.ok(Number(info.format.duration) > 1);
      if (job.options.mode === 'audio') { assert.equal(info.streams.length, 1); assert.equal(info.streams[0].codec_name, 'mp3'); }
      else { const video=info.streams.find(s=>s.codec_type==='video');assert.equal(video.codec_name,'h264');assert.equal(video.pix_fmt,'yuv420p');assert.equal(video.width,320);assert.equal(video.height,180);assert.equal(video.avg_frame_rate,'24/1');assert.ok(info.streams.some(s => s.codec_type === 'audio' && s.codec_name === 'aac')); }
      console.log('Arquivo validado:', path.basename(file));
    }
  }
  console.log('Download HTTP real, MP4 e conversão MP3: OK.');
} finally { await app.close(); fixture.close(); fixture.closeAllConnections(); }
