import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { promisify } from 'node:util';
import { execFile } from 'node:child_process';
import { buildArgs, validateRequest } from '../lib/engine.mjs';
const run = promisify(execFile);
const root = fileURLToPath(new URL('../', import.meta.url));

test('yt-dlp real: prioriza AVC/AAC, respeita limite e aceita fallback', async () => {
  const folder = await fs.mkdtemp(path.join(os.tmpdir(), '7k-formats-'));
  const video = (id, codec, height) => ({ format_id: id, ext: 'mp4', vcodec: codec, acodec: 'none', height, width: height * 16 / 9, url: `http://127.0.0.1/${id}.mp4` });
  const audio = (id, codec) => ({ format_id: id, ext: codec === 'opus' ? 'webm' : 'm4a', vcodec: 'none', acodec: codec, abr: 128, url: `http://127.0.0.1/${id}.m4a` });
  async function select(formats, quality = '1080', format = 'mp4') {
    const info = path.join(folder, 'fixture.json');
    await fs.writeFile(info, JSON.stringify({ id: 'fixture', title: 'Own test', extractor: 'generic', webpage_url: 'http://127.0.0.1/video', formats }));
    const options = validateRequest({ urls: ['http://127.0.0.1/video'], quality, format });
    const args = buildArgs({ options, url: options.urls[0] }, path.join(root, 'bin'), folder);
    const selector = args[args.indexOf('-f') + 1];
    const { stdout } = await run(path.join(root, 'bin', 'yt-dlp.exe'), ['--ignore-config', '--no-plugin-dirs', '--load-info-json', info, '--simulate', '--dump-json', '--no-check-formats', '-f', selector], { windowsHide: true });
    return JSON.parse(stdout).format_id;
  }
  const formats = [video('av1-4k', 'av01.0.12M.08', 2160), video('vp9-1080', 'vp9', 1080), video('avc-720', 'avc1.64001f', 720), audio('a-opus', 'opus'), audio('aac', 'mp4a.40.2')];
  assert.equal(await select(formats), 'avc-720+aac');
  assert.equal(await select(formats, 'best'), 'avc-720+aac');
  assert.equal(await select([video('avc-1080', 'h264', 1080), video('avc-480', 'avc1.42001e', 480), audio('aac', 'aac')], '480'), 'avc-480+aac');
  assert.equal(await select([video('avc', 'h264', 1080), audio('a-opus', 'opus')]), 'avc+a-opus');
  assert.equal(await select([video('vp9', 'vp9', 1080), audio('aac', 'mp4a.40.2')]), 'vp9+aac');
  assert.equal(await select([{ ...video('combined', 'avc1.640028', 1080), acodec: 'mp4a.40.2' }]), 'combined');
  assert.equal(await select(formats, 'best', 'mkv'), 'av1-4k+a-opus');
});
