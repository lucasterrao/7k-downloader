import test from 'node:test';
import assert from 'node:assert/strict';
import path from 'node:path';
import fs from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { promisify } from 'node:util';
import { execFile } from 'node:child_process';
import { ensurePremiereMp4, isPremiereCompatible, probeMedia } from '../lib/media.mjs';
const run = promisify(execFile);
const root = path.dirname(path.dirname(fileURLToPath(import.meta.url)));
const binDir = path.join(root, 'bin');

test('conversão VP9, cancelamento e preservação de saída existente', async () => {
  const folder = await fs.mkdtemp(path.join(root, '.qa', 'codec-test-'));
  const source = path.join(folder, 'source.mp4');
  await run(path.join(binDir, 'ffmpeg.exe'), ['-v', 'error', '-f', 'lavfi', '-i', 'testsrc2=size=160x90:rate=30', '-t', '1', '-c:v', 'libvpx-vp9', source], { windowsHide: true });
  assert.equal(isPremiereCompatible(await probeMedia(source, binDir)), false);
  const canceled = path.join(folder, 'canceled.mp4'); const controller = new AbortController();
  await assert.rejects(ensurePremiereMp4(source, { binDir, output: canceled, signal: controller.signal,
    onProcess(child) { if (path.basename(child.spawnfile).toLowerCase() === 'ffmpeg.exe') child.once('spawn', () => controller.abort()); }
  }));
  assert.equal(await fs.stat(canceled).catch(() => null), null);
  const progress = [];
  const output = await ensurePremiereMp4(source, { binDir, output: canceled, onProgress(value) { progress.push(value); } });
  assert.ok(progress.length); assert.ok(progress.every(Number.isFinite));
  const info = await probeMedia(output, binDir); assert.equal(isPremiereCompatible(info), true);
  assert.equal(info.streams[0].width, 160); assert.equal(info.streams[0].height, 90); assert.equal(info.streams[0].avg_frame_rate, '30/1');
  assert.ok(await fs.stat(source));
  const size = (await fs.stat(output)).size;
  await assert.rejects(ensurePremiereMp4(source, { binDir, output }), /já existe/);
  assert.equal((await fs.stat(output)).size, size);
  const processes = [];
  assert.equal(await ensurePremiereMp4(output, { binDir, onProcess(child) { processes.push(path.basename(child.spawnfile)); } }), output);
  assert.deepEqual(processes, ['ffprobe.exe'], 'Arquivo compatível não deve iniciar FFmpeg.');
});

test('H.264 com Opus: converte somente áudio e copia o vídeo intacto', async () => {
  const folder = await fs.mkdtemp(path.join(root, '.qa', 'audio-only-'));
  const source = path.join(folder, 'source.mp4');
  await run(path.join(binDir, 'ffmpeg.exe'), ['-v', 'error', '-f', 'lavfi', '-i', 'testsrc2=size=160x90:rate=30', '-f', 'lavfi', '-i', 'sine=frequency=440', '-t', '1', '-c:v', 'libx264', '-pix_fmt', 'yuv420p', '-c:a', 'libopus', source], { windowsHide: true });
  const output = await ensurePremiereMp4(source, { binDir });
  assert.ok(isPremiereCompatible(await probeMedia(output, binDir)));
  async function hash(file) {
    return (await run(path.join(binDir, 'ffmpeg.exe'), ['-v', 'error', '-i', file, '-map', '0:v:0', '-c:v', 'copy', '-f', 'hash', '-hash', 'sha256', '-'], { windowsHide: true })).stdout.trim();
  }
  assert.equal(await hash(source), await hash(output), 'Os pacotes H.264 devem permanecer idênticos.');
});
