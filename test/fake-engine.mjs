import path from 'node:path';
import fs from 'node:fs';
const args = process.argv.slice(2); const url = args.at(-1);
if (args.includes('--dump-single-json')) {
  console.log(JSON.stringify({ title: 'Vídeo de teste', duration: 42, extractor_key: 'Fixture', formats: [{ height: 720 }, { height: 1080 }] }));
} else {
  if (url.includes('/needs-session') && !args.includes('--cookies')) {
    console.error("ERROR: [youtube] fixture: Sign in to confirm you're not a bot. Use --cookies"); process.exit(1);
  }
  console.log('7K_TITLE:Vídeo de teste');
  console.log('7K_PROGRESS:' + JSON.stringify({ status: 'downloading', downloaded_bytes: 50, total_bytes: 100, speed: 128, eta: 1 }));
  const directory = args[args.indexOf('-P') + 1];
  if (url.includes('/fail')) { console.error('ERROR: falha de teste'); process.exitCode = 1; }
  else if (url.includes('/slow')) { setTimeout(() => {}, 30000); }
  else {
    setTimeout(() => {
      fs.mkdirSync(directory, { recursive: true }); fs.writeFileSync(path.join(directory, 'Vídeo de teste.mp4'), 'TEST_MEDIA');
      console.log('7K_FILE:' + path.join(directory, 'Vídeo de teste.mp4'));
      console.log('7K_FILE:' + path.resolve(directory, '..', 'outside.mp4'));
    }, 200);
  }
}
