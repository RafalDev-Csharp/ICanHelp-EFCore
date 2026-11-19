// Montaż reklamy z klipów AI (E:\reklama) i nagrań aplikacji (out/app) → out/reklama-16x9.mp4 i out/reklama-pion.mp4.
// Uruchom: node marketing/spot/montage.mjs [folder-z-klipami-AI]
// Napisy i plansze rysuje Chromium (Playwright z ICanHelp.Web), muzykę syntezuje Web Audio (własna, bez licencji),
// a ffmpeg skleja wszystko. Gdy w folderze z klipami AI leży muzyka.mp3, zastępuje ona syntezowaną.
import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { dirname, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const require = createRequire(join(here, '../../ICanHelp.Web/package.json'));
const { chromium } = require('@playwright/test');

const aiDir = process.argv[2] ?? 'E:/reklama';
const appDir = join(here, 'out/app');
const outDir = join(here, 'out');
const tmp = join(outDir, 'montage');
mkdirSync(tmp, { recursive: true });
const logo = readFileSync(join(here, '../../ICanHelp.Web/public/icons/logo.svg'), 'utf8');

// Klip AI po fragmencie nazwy pliku (nazwy nadaje generator).
const aiFiles = readdirSync(aiDir).filter((f) => f.toLowerCase().endsWith('.mp4'));
function ai(part) {
  const name = aiFiles.find((f) => f.includes(part));
  if (!name) throw new Error(`Brak klipu z „${part}” w ${aiDir}`);
  return join(aiDir, name);
}

// Kadrowanie: Hailuo daje 21:9 ze znakiem wodnym w prawym dolnym rogu – bierzemy lewe 16:9 (znak zostaje poza kadrem);
// Dreamina – 16:9, znaki wodne zamazane (delogo).
const HAILUO = 'crop=2218:1248:0:0';
const DREAMINA = 'delogo=x=936:y=636:w=336:h=78,delogo=x=6:y=6:w=52:h=42';

// Oś czasu. kind: ai (klip AI), still (zdjęcie z powolnym przybliżeniem + dźwięk z klipu), app (nagranie aplikacji),
// card (plansza). Sceny z ludźmi bez napisów. Do planszy „Znasz to?” słychać dźwięk klipów (reakcje ludzi), od niej
// do końca tylko muzyka (`mute` – klipy AI mają własne podkłady, które gryzłyby się z naszą muzyką).
// `money` – plakietka z kwotą i dźwięk kasy przy fachowcu (sekunda w ujęciu, pozycja w kadrze 1920×1080).
const segments = [
  { kind: 'ai', src: ai('Extreme close-up'), ss: 0, dur: 2.6, fix: HAILUO, gain: 14 },
  { kind: 'ai', src: ai('Extreme close-up'), ss: 2.6, dur: 2.55, fix: HAILUO, gain: 14 },
  { kind: 'ai', src: ai('A man in his 40'), ss: 0, dur: 5.15, fix: HAILUO, gain: 0 },
  { kind: 'ai', src: ai('scraping_door'), ss: 0, dur: 4.4, fix: '', gain: 0 },
  { kind: 'ai', src: ai('overflowing_gutter'), ss: 0, dur: 4.1, fix: '', gain: 2 },
  { kind: 'ai', src: ai('printer_jam'), ss: 1.2, dur: 5.9, fix: '', gain: 4 },
  { kind: 'card', card: 'turn', dur: 3.4, musicStart: true },
  { kind: 'app', src: '2-zlecam', speed: 2.5, tag: 'Zlecam', text: 'Opisz, co się zepsuło, podaj cenę i miejsce' },
  { kind: 'app', src: '1-alert', speed: 2.5, tag: 'Pomagam', text: 'Fachowiec ustawia alert na swoją okolicę' },
  { kind: 'app', src: '1b-trasa', speed: 2.6, tag: 'Pomagam', text: 'Albo na trasę, którą i tak jeździ – np. z pracy do domu' },
  { kind: 'app', src: '3-oferta', speed: 2.5, tag: 'Pomagam', text: 'Dostaje powiadomienie i składa ofertę' },
  { kind: 'app', src: '4-przyjecie', speed: 1.6, tag: 'Zlecam', text: 'Przyjmujesz ofertę jednym kliknięciem' },
  { kind: 'app', src: '5-realizacja', speed: 2.5, tag: 'Pomagam', text: 'Przyjeżdża i zaczyna pracę' },
  { kind: 'ai', src: ai('scene A, about 2 sec'), ss: 0, dur: 5.15, fix: HAILUO, mute: true, money: { text: '+35 zł', at: 3.3, x: 470, y: 150 } },
  { kind: 'app', src: '6-potwierdzenie', speed: 2.5, tag: 'Zlecam', text: 'Potwierdzasz wykonanie i wystawiasz ocenę' },
  // animacja ze spotu (icanhelp-spot.html?reklama): pomocnik po drodze zgarnia trzy zlecenia, przy każdym dźwięk kasy
  { kind: 'ai', src: join(tmp, 'po-drodze.mp4'), spotScene: 'Pomagam: zlecenia po drodze', ss: 0, dur: 7.5, fix: '', mute: true, chings: [1.75, 3.4, 4.55] },
  { kind: 'ai', src: ai('dreamina'), ss: 0, dur: 5.05, fix: DREAMINA, mute: true },
  { kind: 'card', card: 'end', dur: 4.6 },
];

function probeDuration(file) {
  const r = spawnSync('ffprobe', ['-v', 'error', '-show_entries', 'format=duration', '-of', 'csv=p=0', file], { encoding: 'utf8' });
  return Number(r.stdout.trim());
}
for (const s of segments) {
  if (s.kind === 'app') {
    s.dur = probeDuration(join(appDir, `${s.src}-16x9.mp4`)) - 0.05;
    s.ss = 0;
  }
  s.out = Math.round(((s.kind === 'app' ? s.dur / s.speed : s.dur) * 30)) / 30; // pełne klatki
}
let t = 0;
for (const s of segments) {
  s.at = t;
  t += s.out;
}
const total = t;
const musicAt = segments.find((s) => s.musicStart).at;

function ffmpeg(args) {
  const r = spawnSync('ffmpeg', ['-hide_banner', '-loglevel', 'error', '-y', ...args], { encoding: 'utf8' });
  if (r.status !== 0) throw new Error(`ffmpeg: ${r.stderr}`);
}

// ---------- Napisy i plansze ----------
const css = `
  * { box-sizing: border-box; margin: 0; }
  body { width: var(--w); height: var(--h); font-family: 'Segoe UI', Arial, sans-serif; background: transparent; overflow: hidden; }
  .pill { position: absolute; left: 50%; transform: translateX(-50%); width: max-content; max-width: 88%; padding: .38em .9em; border-radius: .5em;
    background: rgba(12,12,12,.68); color: #fff; font-weight: 700; text-align: center; line-height: 1.2;
    text-shadow: 0 2px 6px rgba(0,0,0,.4); }
  .tag { display: inline-block; padding: .25em .8em; border-radius: 999px; background: #fff; color: #e35d0a; font-weight: 800;
    text-transform: uppercase; letter-spacing: .06em; }
  .line { color: #fff; font-weight: 800; line-height: 1.12; text-shadow: 0 3px 10px rgba(120,40,0,.35); }
  .brand { display: flex; align-items: center; gap: .35em; color: #fff; font-weight: 900; }
  .brand svg { width: 1.6em; height: 1.6em; border-radius: .3em; box-shadow: 0 .1em .4em rgba(0,0,0,.25); }
  .card { position: absolute; inset: 0; background: radial-gradient(circle at 50% 40%, #ff9a3c, #f07a12 55%, #d9620a);
    display: flex; flex-direction: column; align-items: center; justify-content: center; text-align: center; color: #fff; }
  .chips { display: flex; gap: .6em; }
  .chips span { padding: .3em .9em; border-radius: 999px; background: rgba(255,255,255,.95); color: #d9620a; font-weight: 800; }
`;
/** Plakietka z kwotą przy fachowcu (zarobek pomocnika). */
function moneyHtml(money, f) {
  const [x, y, size] = f === 'h' ? [money.x, money.y, 84] : [money.x * 0.5625, 656 + money.y * 0.5625, 60];
  return `<div style="position:absolute;left:${x}px;top:${y}px;padding:.14em .5em;border-radius:.4em;background:#1f9d4c;color:#fff;
    font-size:${size}px;font-weight:900;letter-spacing:.01em;box-shadow:0 .08em .35em rgba(0,0,0,.35);border:.05em solid rgba(255,255,255,.9)">${money.text}</div>`;
}
function pillHtml(text, f) {
  if (!text) return '';
  return f === 'h'
    ? `<div class="pill" style="bottom:64px;font-size:52px">${text}</div>`
    : `<div class="pill" style="top:1300px;font-size:58px">${text}</div>`;
}
function sideHtml(tag, text, f) {
  if (f === 'h') {
    return `<div style="position:absolute;left:70px;top:0;bottom:0;width:600px;display:flex;flex-direction:column;justify-content:center;gap:28px">
        <div><span class="tag" style="font-size:34px">${tag}</span></div>
        <div class="line" style="font-size:64px">${text}</div></div>
      <div class="brand" style="position:absolute;right:90px;top:50%;transform:translateY(-50%);font-size:48px">${logo}<span>I can help U</span></div>`;
  }
  return `<div style="position:absolute;left:60px;right:60px;top:40px;height:270px;display:flex;flex-direction:column;justify-content:center;align-items:center;gap:18px;text-align:center">
      <span class="tag" style="font-size:34px">${tag}</span>
      <div class="line" style="font-size:56px">${text}</div></div>`;
}
function cardHtml(card, f) {
  const big = f === 'h' ? 1 : 0.9;
  if (card === 'turn') {
    return `<div class="card" style="gap:${44 * big}px">
      <div class="line" style="font-size:${84 * big}px">Znasz to?</div>
      <div class="line" style="font-size:${58 * big}px;max-width:${f === 'h' ? 1400 : 900}px;font-weight:700">W okolicy jest ktoś, kto umie pomóc.</div>
      <div class="brand" style="font-size:${76 * big}px;margin-top:${20 * big}px">${logo}<span>I can help U</span></div></div>`;
  }
  return `<div class="card" style="gap:${40 * big}px">
      <div style="width:${230 * big}px;height:${230 * big}px">${logo.replace('<svg', '<svg style="width:100%;height:100%;border-radius:22%;box-shadow:0 10px 40px rgba(0,0,0,.25)"')}</div>
      <div class="line" style="font-size:${110 * big}px;font-weight:900">I can help U</div>
      <div class="line" style="font-size:${50 * big}px;font-weight:600;max-width:${f === 'h' ? 1400 : 900}px">Drobna pomoc sąsiedzka – blisko Ciebie</div>
      <div class="chips" style="font-size:${40 * big}px"><span>Zlecam</span><span>Pomagam</span></div></div>`;
}

async function renderOverlays(browser) {
  for (const f of ['h', 'v']) {
    const [w, h] = f === 'h' ? [1920, 1080] : [1080, 1920];
    const page = await browser.newPage({ viewport: { width: w, height: h }, deviceScaleFactor: 1 });
    for (const [i, s] of segments.entries()) {
      const inner =
        s.kind === 'card' ? cardHtml(s.card, f) : s.kind === 'app' ? sideHtml(s.tag, s.text, f) : pillHtml(s.text, f);
      await page.setContent(`<html><head><style>${css}</style></head><body style="--w:${w}px;--h:${h}px">${inner}</body></html>`);
      await page.screenshot({ path: join(tmp, `ov-${f}-${i}.png`), omitBackground: s.kind !== 'card' });
      if (s.money) {
        await page.setContent(`<html><head><style>${css}</style></head><body style="--w:${w}px;--h:${h}px">${moneyHtml(s.money, f)}</body></html>`);
        await page.screenshot({ path: join(tmp, `money-${f}-${i}.png`), omitBackground: true });
      }
    }
    await page.close();
  }
}

// ---------- Muzyka (Web Audio, offline) ----------
async function renderMusic(browser) {
  const page = await browser.newPage();
  const ducks = segments.filter((s) => s.duck).map((s) => [s.at - musicAt, s.out]);
  const chings = segments.flatMap((s) => [...(s.money ? [s.money.at] : []), ...(s.chings ?? [])].map((c) => s.at + c - musicAt));
  const b64 = await page.evaluate(
    async ({ dur, ducks, chings }) => {
      const sr = 48000;
      const ctx = new OfflineAudioContext(2, Math.ceil(sr * dur), sr);
      const master = ctx.createGain();
      master.connect(ctx.destination);
      // głośność: wejście, przyciszenie pod scenami z dźwiękiem, wyciszenie na końcu
      const g = master.gain;
      g.setValueAtTime(0, 0);
      g.linearRampToValueAtTime(0.8, 0.35);
      for (const [at, len] of ducks) {
        g.setValueAtTime(0.8, at - 0.3);
        g.linearRampToValueAtTime(0.38, at);
        g.setValueAtTime(0.38, at + len - 0.25);
        g.linearRampToValueAtTime(0.8, at + len + 0.1);
      }
      g.setValueAtTime(0.8, dur - 2.6);
      g.linearRampToValueAtTime(0, dur);

      const comp = ctx.createDynamicsCompressor();
      comp.threshold.value = -14;
      comp.ratio.value = 3;
      comp.connect(master);
      const rev = ctx.createConvolver(); // krótki pogłos z szumu
      const ir = ctx.createBuffer(2, sr * 1.4, sr);
      for (let c = 0; c < 2; c++) {
        const d = ir.getChannelData(c);
        for (let i = 0; i < d.length; i++) d[i] = (Math.random() * 2 - 1) * Math.pow(1 - i / d.length, 3);
      }
      rev.buffer = ir;
      const revGain = ctx.createGain();
      revGain.gain.value = 0.18;
      rev.connect(revGain).connect(comp);

      const noise = ctx.createBuffer(1, sr, sr);
      const nd = noise.getChannelData(0);
      for (let i = 0; i < nd.length; i++) nd[i] = Math.random() * 2 - 1;

      const bpm = 112;
      const beat = 60 / bpm;
      const bar = beat * 4;
      const hz = (m) => 440 * Math.pow(2, (m - 69) / 12);
      // C – G – Am – F
      const chords = [
        [60, 64, 67],
        [59, 62, 67],
        [57, 60, 64],
        [57, 60, 65],
      ];
      const roots = [48, 43, 45, 41];

      function pluck(t, midi, vol, pan) {
        const o1 = ctx.createOscillator();
        const o2 = ctx.createOscillator();
        o1.type = 'triangle';
        o2.type = 'sine';
        o1.frequency.value = hz(midi);
        o2.frequency.value = hz(midi + 12) * 1.002;
        const env = ctx.createGain();
        env.gain.setValueAtTime(0.0001, t);
        env.gain.exponentialRampToValueAtTime(vol, t + 0.006);
        env.gain.exponentialRampToValueAtTime(0.0001, t + 0.55);
        const lp = ctx.createBiquadFilter();
        lp.type = 'lowpass';
        lp.frequency.setValueAtTime(5200, t);
        lp.frequency.exponentialRampToValueAtTime(900, t + 0.4);
        const p = ctx.createStereoPanner();
        p.pan.value = pan;
        const o2g = ctx.createGain();
        o2g.gain.value = 0.35;
        o1.connect(lp);
        o2.connect(o2g).connect(lp);
        lp.connect(env).connect(p);
        p.connect(comp);
        p.connect(rev);
        o1.start(t);
        o2.start(t);
        o1.stop(t + 0.6);
        o2.stop(t + 0.6);
      }
      function bass(t, midi, len) {
        const o = ctx.createOscillator();
        o.type = 'sine';
        o.frequency.value = hz(midi);
        const env = ctx.createGain();
        env.gain.setValueAtTime(0.0001, t);
        env.gain.exponentialRampToValueAtTime(0.42, t + 0.01);
        env.gain.exponentialRampToValueAtTime(0.0001, t + len);
        o.connect(env).connect(comp);
        o.start(t);
        o.stop(t + len + 0.05);
      }
      function kick(t) {
        const o = ctx.createOscillator();
        o.frequency.setValueAtTime(130, t);
        o.frequency.exponentialRampToValueAtTime(42, t + 0.12);
        const env = ctx.createGain();
        env.gain.setValueAtTime(0.75, t);
        env.gain.exponentialRampToValueAtTime(0.0001, t + 0.28);
        o.connect(env).connect(comp);
        o.start(t);
        o.stop(t + 0.3);
      }
      function noiseHit(t, freq, q, vol, len, toRev) {
        const s = ctx.createBufferSource();
        s.buffer = noise;
        const f = ctx.createBiquadFilter();
        f.type = 'bandpass';
        f.frequency.value = freq;
        f.Q.value = q;
        const env = ctx.createGain();
        env.gain.setValueAtTime(vol, t);
        env.gain.exponentialRampToValueAtTime(0.0001, t + len);
        s.connect(f).connect(env).connect(comp);
        if (toRev) env.connect(rev);
        s.start(t, Math.random() * 0.5);
        s.stop(t + len + 0.02);
      }
      function chime(t) {
        [
          [88, 0],
          [96, 0.12],
        ].forEach(([m, d]) => {
          const o = ctx.createOscillator();
          o.type = 'sine';
          o.frequency.value = hz(m);
          const env = ctx.createGain();
          env.gain.setValueAtTime(0.0001, t + d);
          env.gain.exponentialRampToValueAtTime(0.22, t + d + 0.01);
          env.gain.exponentialRampToValueAtTime(0.0001, t + d + 1.2);
          o.connect(env);
          env.connect(comp);
          env.connect(rev);
          o.start(t + d);
          o.stop(t + d + 1.3);
        });
      }

      // „Ka-ching” kasy: stuk szuflady + dwa jasne dzwonki
      function cash(t) {
        noiseHit(t, 2600, 2, 0.5, 0.06, false);
        noiseHit(t + 0.03, 900, 1.5, 0.35, 0.08, false);
        [
          [0.07, [2637, 3951, 5274]],
          [0.19, [3136, 4699, 6272]],
        ].forEach(([d, freqs]) =>
          freqs.forEach((fr, k) => {
            const o = ctx.createOscillator();
            o.type = 'sine';
            o.frequency.value = fr;
            const env = ctx.createGain();
            env.gain.setValueAtTime(0.0001, t + d);
            env.gain.exponentialRampToValueAtTime(0.3 / (k + 1), t + d + 0.004);
            env.gain.exponentialRampToValueAtTime(0.0001, t + d + 0.9);
            o.connect(env);
            env.connect(comp);
            env.connect(rev);
            o.start(t + d);
            o.stop(t + d + 1);
          }),
        );
      }
      chings.forEach(cash);

      chime(0.05);
      const bars = Math.ceil((dur - 2) / bar);
      const arp = [0, 1, 2, 1, 0, 2, 1, 2];
      for (let b = 0; b < bars; b++) {
        const t0 = 0.4 + b * bar;
        const ch = chords[b % 4];
        const full = b >= 1; // pierwszy takt – sama gitara
        for (let i = 0; i < 8; i++) {
          const t = t0 + i * (beat / 2) + (i % 2 ? 0.012 : 0); // lekki swing
          pluck(t, ch[arp[i]] + 12, i % 2 ? 0.16 : 0.22, i % 2 ? 0.35 : -0.25);
        }
        if (b % 2 === 1) pluck(t0 + beat * 3.5, ch[2] + 24, 0.1, 0.5);
        if (!full) continue;
        for (let q = 0; q < 4; q++) {
          const t = t0 + q * beat;
          bass(t, roots[b % 4], beat * 0.9);
          if (q === 0 || q === 2) kick(t);
          if (q === 1 || q === 3) noiseHit(t, 1800, 0.8, 0.32, 0.16, true);
          noiseHit(t + beat / 2, 9000, 1.2, 0.08, 0.05, false);
          noiseHit(t, 9000, 1.2, 0.05, 0.04, false);
        }
      }
      // ostatni akord wybrzmiewa
      const tEnd = 0.4 + bars * bar;
      if (tEnd < dur) [60, 64, 67, 72].forEach((m, i) => pluck(tEnd + i * 0.03, m + 12, 0.18, i % 2 ? 0.3 : -0.3));

      const buf = await ctx.startRendering();
      const L = buf.getChannelData(0);
      const R = buf.getChannelData(1);
      const n = L.length;
      const out = new DataView(new ArrayBuffer(44 + n * 4));
      const str = (o, s) => [...s].forEach((c, i) => out.setUint8(o + i, c.charCodeAt(0)));
      str(0, 'RIFF');
      out.setUint32(4, 36 + n * 4, true);
      str(8, 'WAVEfmt ');
      out.setUint32(16, 16, true);
      out.setUint16(20, 1, true);
      out.setUint16(22, 2, true);
      out.setUint32(24, sr, true);
      out.setUint32(28, sr * 4, true);
      out.setUint16(32, 4, true);
      out.setUint16(34, 16, true);
      str(36, 'data');
      out.setUint32(40, n * 4, true);
      for (let i = 0; i < n; i++) {
        out.setInt16(44 + i * 4, Math.max(-1, Math.min(1, L[i])) * 32767, true);
        out.setInt16(46 + i * 4, Math.max(-1, Math.min(1, R[i])) * 32767, true);
      }
      const bytes = new Uint8Array(out.buffer);
      let bin = '';
      for (let i = 0; i < bytes.length; i += 0x8000) bin += String.fromCharCode(...bytes.subarray(i, i + 0x8000));
      return btoa(bin);
    },
    { dur: total - musicAt, ducks, chings },
  );
  writeFileSync(join(tmp, 'muzyka.wav'), Buffer.from(b64, 'base64'));
  await page.close();
}

// ---------- Segmenty ----------
function encodeSegment(s, i, f) {
  const [W, H] = f === 'h' ? [1920, 1080] : [1080, 1920];
  const outFile = join(tmp, `seg-${f}-${String(i).padStart(2, '0')}.mp4`);
  const ov = join(tmp, `ov-${f}-${i}.png`);
  const d = s.out.toFixed(3);
  const vEnd = `fps=30,format=yuv420p,trim=duration=${d}`;
  const silent = ['-f', 'lavfi', '-t', d, '-i', 'anullsrc=r=48000:cl=stereo'];
  let args;
  if (s.kind === 'card') {
    const fade = s.card === 'turn' ? `fade=t=in:st=0:d=0.35,` : `fade=t=in:st=0:d=0.3,`;
    args = ['-loop', '1', '-t', d, '-i', ov, ...silent, '-filter_complex', `[0:v]scale=${W}:${H},${fade}fade=t=out:st=${(s.out - 0.3).toFixed(3)}:d=0.3,${vEnd}[v]`, '-map', '[v]', '-map', '1:a'];
  } else if (s.kind === 'app') {
    const src = join(appDir, `${s.src}-${f === 'h' ? '16x9' : 'pion'}.mp4`);
    const base =
      f === 'h'
        ? `[0:v]setpts=(PTS-STARTPTS)/${s.speed}[b]`
        : `color=c=0xf07a12:s=${W}x${H}:r=30:d=${d}[bg];[0:v]setpts=(PTS-STARTPTS)/${s.speed},scale=-2:1560[ph];[bg][ph]overlay=(W-w)/2:340[b]`;
    args = ['-i', src, '-loop', '1', '-t', d, '-i', ov, ...silent, '-filter_complex', `${base};[1:v]format=rgba,fade=t=in:st=0.15:d=0.3:alpha=1[o];[b][o]overlay=0:0,${vEnd}[v]`, '-map', '[v]', '-map', '2:a'];
  } else {
    // Obraz 1920×1080: klip AI (kadrowanie / zamazanie znaków wodnych) albo zdjęcie z powolnym przybliżeniem.
    const frames = Math.round(s.out * 30);
    const src =
      s.kind === 'still'
        ? `[0:v]crop=iw:iw*9/16,scale=3840:2160:flags=lanczos,zoompan=z='1+0.07*on/${frames}':x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':d=1:s=1920x1080:fps=30,${s.look ?? 'null'},setsar=1[s0]`
        : `[0:v]${s.fix ? `${s.fix},` : ''}scale=1920:1080:flags=lanczos,setsar=1[s0]`;
    const base =
      f === 'h'
        ? `${src};[s0]null[b]`
        : `${src};[s0]split[x][y];[x]scale=-2:${H},crop=${W}:${H},boxblur=28:2,eq=brightness=-0.12[bg];[y]scale=${W}:-2:flags=lanczos[fg];[bg][fg]overlay=0:(H-h)/2[b]`;
    const video = s.kind === 'still' ? ['-loop', '1', '-t', d, '-i', s.img] : ['-ss', String(s.ss), '-t', d, '-i', s.src];
    // dźwięk: z klipu (zdjęcie – z oryginalnego klipu kranu), po planszy „Znasz to?” – cisza (gra tylko muzyka)
    const sound = s.kind === 'still' ? ['-t', d, '-i', s.audio] : s.mute ? silent : [];
    const aIn = s.kind === 'still' ? '[2:a]' : '[0:a]';
    const audio = s.mute
      ? `[2:a]anull[a]`
      : `${aIn}aresample=48000,aformat=channel_layouts=stereo,volume=${s.gain}dB,apad,atrim=duration=${d},afade=t=in:d=0.06,afade=t=out:st=${(s.out - 0.08).toFixed(3)}:d=0.08[a]`;
    const money = s.money ? ['-loop', '1', '-t', d, '-i', join(tmp, `money-${f}-${i}.png`)] : [];
    const mIdx = s.kind === 'still' || s.mute ? 3 : 2;
    // plakietka z kwotą: pojawia się i lekko unosi
    const moneyFx = s.money
      ? `;[${mIdx}:v]format=rgba,fade=t=in:st=${s.money.at}:d=0.25:alpha=1[m];[c][m]overlay=x=0:y='if(lt(t,${s.money.at}),0,-36*min(1,(t-${s.money.at})/0.5))'[cm]`
      : '';
    args = [
      ...video, '-loop', '1', '-t', d, '-i', ov, ...sound, ...money,
      '-filter_complex',
      `${base};[1:v]format=rgba,fade=t=in:st=0.25:d=0.3:alpha=1[o];[b][o]overlay=0:0[c]${moneyFx};[${s.money ? 'cm' : 'c'}]${vEnd}[v];${audio}`,
      '-map', '[v]', '-map', '[a]',
    ];
  }
  ffmpeg([...args, '-t', d, '-c:v', 'libx264', '-preset', 'medium', '-crf', '18', '-c:a', 'pcm_s16le', outFile.replace('.mp4', '.mov')]);
  return outFile.replace('.mp4', '.mov');
}

function finalMix(files, f, music) {
  const list = join(tmp, `list-${f}.txt`);
  writeFileSync(list, files.map((p) => `file '${p.replace(/\\/g, '/')}'`).join('\n'));
  const out = join(outDir, `reklama-${f === 'h' ? '16x9' : 'pion'}.mp4`);
  const ms = Math.round(musicAt * 1000);
  // muzyka.mp3 od autora: przycięta, ściszona i wyciszona na końcu; syntezowana ma to już w sobie
  const len = total - musicAt;
  const musicIn = music.endsWith('.wav')
    ? '[1:a]'
    : `[1:a]aresample=48000,atrim=duration=${len.toFixed(3)},afade=t=in:d=0.4,afade=t=out:st=${(len - 2.5).toFixed(3)}:d=2.5,volume=0.55,`;
  ffmpeg([
    '-f', 'concat', '-safe', '0', '-i', list, '-i', music,
    '-filter_complex',
    `${musicIn}adelay=${ms}|${ms}[m];[0:a][m]amix=inputs=2:duration=first:normalize=0,alimiter=limit=0.9[a]`,
    '-map', '0:v', '-map', '[a]', '-c:v', 'copy', '-c:a', 'aac', '-b:a', '192k', '-movflags', '+faststart', out,
  ]);
  return out;
}

/** Scena ze spotu (klatka po klatce, jak render-video.mjs) → MP4 1920×1080 bez dźwięku. */
async function renderSpotScene(browser, s) {
  const page = await browser.newPage({ viewport: { width: 1920, height: 1080 }, deviceScaleFactor: 1 });
  await page.goto(`${pathToFileURL(join(here, 'icanhelp-spot.html')).href}?reklama`, { waitUntil: 'networkidle' });
  await page.evaluate(() => document.fonts.ready);
  await page.evaluate(() => window.__spot.enterExport());
  const scene = await page.evaluate((title) => window.__spot.scenes.find((x) => x.title === title), s.spotScene);
  if (!scene) throw new Error(`Brak sceny „${s.spotScene}” w icanhelp-spot.html`);
  const frames = [];
  for (let f = 0; f < Math.round(s.dur * 30); f++) {
    await page.evaluate((t) => window.__spot.renderAt(t), scene.start + f / 30);
    frames.push(await page.screenshot({ type: 'jpeg', quality: 92 }));
  }
  await page.close();
  const r = spawnSync('ffmpeg', ['-hide_banner', '-loglevel', 'error', '-y', '-f', 'image2pipe', '-framerate', '30', '-c:v', 'mjpeg', '-i', '-',
    '-c:v', 'libx264', '-crf', '17', '-pix_fmt', 'yuv420p', s.src], { input: Buffer.concat(frames), maxBuffer: 1 << 30 });
  if (r.status !== 0) throw new Error(`ffmpeg: ${r.stderr}`);
}

const browser = await chromium.launch();
for (const s of segments.filter((x) => x.spotScene)) await renderSpotScene(browser, s);
await renderOverlays(browser);
const userMusic = join(aiDir, 'muzyka.mp3');
const music = existsSync(userMusic) ? userMusic : join(tmp, 'muzyka.wav');
if (!existsSync(userMusic)) await renderMusic(browser);
await browser.close();

const only = process.argv[3]; // 'h' albo 'v' – jeden format
for (const f of only ? [only] : ['h', 'v']) {
  const files = segments.map((s, i) => encodeSegment(s, i, f));
  const out = finalMix(files, f, music);
  console.log(`✓ ${out} (${total.toFixed(1)} s)`);
}
