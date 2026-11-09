import fs from 'node:fs/promises';
import path from 'node:path';
import http from 'node:http';
import { fileURLToPath } from 'node:url';
import { promisify } from 'node:util';
import { execFile } from 'node:child_process';
import { createApp } from '../server.mjs';
const run = promisify(execFile);
const project = path.dirname(path.dirname(fileURLToPath(import.meta.url)));
const root = path.join(project, '.qa', 'native'); const binDir = path.join(project, 'bin');
await fs.mkdir(root, { recursive: true });
const compiler = path.join(process.env.WINDIR, 'Microsoft.NET', 'Framework64', 'v4.0.30319', 'csc.exe');
const tester = path.join(root, 'NativeSmoke.exe');
await run(compiler, ['/nologo', '/codepage:65001', '/target:exe', '/platform:x64', '/main:NativeSmoke', '/reference:System.Windows.Forms.dll', '/reference:System.Drawing.dll', '/reference:System.Web.Extensions.dll', `/reference:${path.join(project,'Microsoft.Web.WebView2.Core.dll')}`, `/reference:${path.join(project,'Microsoft.Web.WebView2.WinForms.dll')}`, `/out:${tester}`, path.join(project, 'scripts', 'NativeApp.cs'), path.join(project, 'scripts', 'DesktopComponents.cs'), path.join(project, 'test', 'NativeSmoke.cs')], { windowsHide: true });
const source = path.join(root, 'amostra-nativa.mp4');
await run(path.join(binDir, 'ffmpeg.exe'), ['-hide_banner', '-loglevel', 'error', '-y', '-f', 'lavfi', '-i', 'testsrc2=size=320x180:rate=24', '-f', 'lavfi', '-i', 'sine=frequency=440:sample_rate=44100', '-t', '2', '-c:v', 'libx264', '-pix_fmt', 'yuv420p', '-c:a', 'aac', source], { windowsHide: true });
const bytes = await fs.readFile(source);
const fixture = http.createServer((req, res) => { if (req.url !== '/amostra-nativa.mp4') { res.writeHead(404); res.end(); return; } res.writeHead(200, { 'Content-Type': 'video/mp4', 'Content-Length': bytes.length }); res.end(bytes); });
await new Promise(resolve => fixture.listen(0, '127.0.0.1', resolve));
const app = await createApp({ root, binDir, testing: true, noOpen: true }); const address = await app.listen();
await fs.writeFile(path.join(root, 'data', 'session.json'), JSON.stringify({ url: address }));
try {
  const { stdout, stderr } = await run(tester, [root, `http://127.0.0.1:${fixture.address().port}/amostra-nativa.mp4`], { windowsHide: true, timeout: 100000 });
  console.log(stdout.trim()); if (stderr) console.error(stderr.trim());
} finally { await app.close(); fixture.close(); fixture.closeAllConnections(); }
