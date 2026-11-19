// Spot reklamowy → plik MP4 (1920×1080, 30 kl./s, H.264 + AAC).
// Klatki liczone dokładnie (nie nagranie ekranu): Chromium z Playwright ustawia każdą chwilę filmu (window.__spot.renderAt),
// robi zrzut i przekazuje go do ffmpeg. Dźwięk liczy OfflineAudioContext w tej samej stronie (window.__spot.renderAudio).
// Uruchomienie: marketing\spot\render-spot.cmd  (wymaga ffmpeg – np. `winget install Gyan.FFmpeg`).
import { spawn, execFileSync } from 'node:child_process';
import { existsSync, readdirSync, writeFileSync, mkdirSync } from 'node:fs';
import { createRequire } from 'node:module';
import { dirname, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const root = join(here, '..', '..');
const require = createRequire(join(root, 'ICanHelp.Web', 'package.json'));
const { chromium } = require('@playwright/test');

const FPS = 30;
const WIDTH = 1920;
const HEIGHT = 1080;
const outDir = join(here, 'out');
const outFile = join(outDir, 'icanhelp-spot.mp4');
const wavFile = join(outDir, 'icanhelp-spot.wav');

/** ffmpeg: zmienna FFMPEG, PATH albo instalacja z winget (PATH odświeża się dopiero w nowym oknie). */
function findFfmpeg() {
  if (process.env.FFMPEG && existsSync(process.env.FFMPEG)) return process.env.FFMPEG;
  try {
    execFileSync('ffmpeg', ['-version'], { stdio: 'ignore' });
    return 'ffmpeg';
  } catch { /* nie ma w PATH */ }
  const winget = join(process.env.LOCALAPPDATA ?? '', 'Microsoft', 'WinGet');
  const link = join(winget, 'Links', 'ffmpeg.exe');
  if (existsSync(link)) return link;
  const packages = join(winget, 'Packages');
  if (existsSync(packages)) {
    for (const pkg of readdirSync(packages).filter((p) => p.toLowerCase().includes('ffmpeg'))) {
      for (const build of readdirSync(join(packages, pkg))) {
        const exe = join(packages, pkg, build, 'bin', 'ffmpeg.exe');
        if (existsSync(exe)) return exe;
      }
    }
  }
  return null;
}

const ffmpeg = findFfmpeg();
if (!ffmpeg) {
  console.error('Brak ffmpeg. Zainstaluj go poleceniem:  winget install Gyan.FFmpeg  i uruchom skrypt ponownie.');
  process.exit(1);
}

mkdirSync(outDir, { recursive: true });
const browser = await chromium.launch();
try {
  const page = await browser.newPage({ viewport: { width: WIDTH, height: HEIGHT }, deviceScaleFactor: 1 });
  await page.goto(pathToFileURL(join(here, 'icanhelp-spot.html')).href, { waitUntil: 'networkidle' });
  await page.evaluate(() => document.fonts.ready);
  await page.evaluate(() => window.__spot.enterExport());
  const total = await page.evaluate(() => window.__spot.total);

  console.log('Dźwięk: liczę ścieżkę offline…');
  const wav = await page.evaluate(() => window.__spot.renderAudio());
  writeFileSync(wavFile, Buffer.from(wav, 'base64'));

  const frames = Math.round(total * FPS);
  console.log(`Obraz: ${frames} klatek (${total.toFixed(1)} s) → ${outFile}`);
  const encoder = spawn(ffmpeg, [
    '-y', '-hide_banner', '-loglevel', 'error',
    '-f', 'image2pipe', '-framerate', String(FPS), '-c:v', 'mjpeg', '-i', '-',
    '-i', wavFile,
    '-c:v', 'libx264', '-preset', 'medium', '-crf', '18', '-pix_fmt', 'yuv420p',
    '-c:a', 'aac', '-b:a', '192k', '-shortest', '-movflags', '+faststart',
    outFile,
  ], { stdio: ['pipe', 'inherit', 'inherit'] });
  const finished = new Promise((resolve, reject) => {
    encoder.on('error', reject);
    encoder.on('close', (code) => (code === 0 ? resolve() : reject(new Error(`ffmpeg zakończył się kodem ${code}`))));
  });

  for (let f = 0; f < frames; f++) {
    await page.evaluate((t) => window.__spot.renderAt(t), f / FPS);
    const jpg = await page.screenshot({ type: 'jpeg', quality: 92 });
    if (!encoder.stdin.write(jpg)) await new Promise((r) => encoder.stdin.once('drain', r));
    if (f % (FPS * 5) === 0) console.log(`  ${(f / FPS).toFixed(0)} s / ${total.toFixed(0)} s`);
  }
  encoder.stdin.end();
  await finished;
  console.log(`Gotowe: ${outFile}`);
} finally {
  await browser.close();
}
