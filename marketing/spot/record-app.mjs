// Nagrania prawdziwej aplikacji do reklamy: Playwright klika jak człowiek w ekranie telefonu, nagrywa wideo,
// a ffmpeg zamienia je na MP4 (pion 1080×1920 oraz 16:9 1920×1080 z telefonem na tle w kolorze marki).
// Wymaga: API (dotnet run --project ICanHelp.Api, Development = dane demo) i frontu (npm start → http://localhost:4200).
// Uruchomienie: node marketing/spot/record-app.mjs [nazwa-ujęcia ...]   (bez nazw = wszystkie po kolei)
// Ujęcia tworzą prawdziwe dane w lokalnej bazie (ogłoszenie Marka, oferta Tomka, umowa, realizacja).
import { execFileSync } from 'node:child_process';
import { existsSync, mkdirSync, readdirSync, writeFileSync, readFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const root = join(here, '..', '..');
const require = createRequire(join(root, 'ICanHelp.Web', 'package.json'));
const { chromium } = require('@playwright/test');

const BASE = process.env.APP_URL ?? 'http://localhost:4200';
const PASSWORD = 'Test1234!'; // hasło kont demo (DemoDataSeeder, tylko lokalna baza deweloperska)
// Nagrywamy screencastem przeglądarki w rozmiarze CSS (wyższa gęstość pikseli nie trafia do klatek), więc telefon
// jest od razu w rozdzielczości kadru: w wersji 16:9 ma ok. 1000 px wysokości, czyli prawie bez powiększania.
const PHONE = { width: 450, height: 975 }; // ok. 9:19,5 – typowy Android
const FPS = 30;
const outDir = join(here, 'out', 'app');
const tmpDir = join(outDir, 'tmp');
const stateFile = join(tmpDir, 'state.json');
mkdirSync(tmpDir, { recursive: true });

const state = existsSync(stateFile) ? JSON.parse(readFileSync(stateFile, 'utf8')) : {};
const saveState = () => writeFileSync(stateFile, JSON.stringify(state, null, 2));
const pause = (page, ms) => page.waitForTimeout(ms);

function findFfmpeg() {
  try { execFileSync('ffmpeg', ['-version'], { stdio: 'ignore' }); return 'ffmpeg'; } catch { /* nie w PATH */ }
  const packages = join(process.env.LOCALAPPDATA ?? '', 'Microsoft', 'WinGet', 'Packages');
  for (const pkg of existsSync(packages) ? readdirSync(packages).filter((p) => /ffmpeg/i.test(p)) : []) {
    for (const build of readdirSync(join(packages, pkg))) {
      const exe = join(packages, pkg, build, 'bin', 'ffmpeg.exe');
      if (existsSync(exe)) return exe;
    }
  }
  throw new Error('Brak ffmpeg (winget install Gyan.FFmpeg).');
}
const ffmpeg = findFfmpeg();

// Język przeglądarki: polskie formaty dat i godzin w polach typu datetime-local (inaczej „11/17/2026 05:30 PM”).
const browser = await chromium.launch({ args: ['--lang=pl-PL'] });

/** Sesja użytkownika demo (token w localStorage) – logowanie poza nagraniem. */
async function login(user) {
  const file = join(tmpDir, `${user}.json`);
  const context = await browser.newContext({ viewport: PHONE });
  const page = await context.newPage();
  await page.goto(`${BASE}/login`);
  await page.locator('ion-input input[type="email"], ion-input[type="email"] input').first().fill(`${user}@test.pl`);
  await page.locator('ion-input input[type="password"], ion-input[type="password"] input').first().fill(PASSWORD);
  await page.keyboard.press('Enter');
  await page.waitForURL((url) => !url.pathname.startsWith('/login'), { timeout: 15000 });
  await context.storageState({ path: file });
  await context.close();
  return file;
}

/**
 * Jedno ujęcie: screencast przeglądarki (JPEG q92, klatka przy każdej zmianie ekranu) podczas kroków → MP4 30 kl./s
 * w dwóch wersjach: 16:9 1920×1080 (telefon na pomarańczowym tle) i pion 1080×1920. Przy błędzie zrzut ekranu do tmp.
 */
async function shot(name, user, steps) {
  const storage = await login(user);
  const context = await browser.newContext({
    viewport: PHONE, deviceScaleFactor: 1, colorScheme: 'light', locale: 'pl-PL', timezoneId: 'Europe/Warsaw',
    storageState: storage, isMobile: true, hasTouch: true,
  });
  const page = await context.newPage();
  const cdp = await context.newCDPSession(page);
  const frames = [];
  let recording = false, readyAt = null;
  cdp.on('Page.screencastFrame', async (f) => {
    if (recording) frames.push({ t: f.metadata.timestamp, jpg: Buffer.from(f.data, 'base64') });
    await cdp.send('Page.screencastFrameAck', { sessionId: f.sessionId }).catch(() => {});
  });
  await cdp.send('Page.startScreencast', { format: 'jpeg', quality: 92, maxWidth: PHONE.width, maxHeight: PHONE.height });
  recording = true;
  // „Gotowe do pokazania” – od tej chwili zaczyna się klip (ładowanie strony zostaje wycięte).
  const markReady = () => { readyAt = Date.now() / 1000; };
  try {
    await steps(page, markReady);
    await pause(page, 1200);
  } catch (error) {
    await page.screenshot({ path: join(tmpDir, `${name}-blad.png`) });
    await context.close();
    throw new Error(`${name}: ${error.message.split('\n')[0]}`);
  }
  recording = false;
  await cdp.send('Page.stopScreencast');
  await context.close();
  encode(name, frames, readyAt);
}

/** Klatki o nierównych odstępach → stałe 30 kl./s (ostatnia znana klatka trwa do następnej), dwa formaty naraz. */
function encode(name, frames, readyAt) {
  if (frames.length === 0) throw new Error(`${name}: brak klatek`);
  const start = Math.max(frames[0].t, readyAt ?? frames[0].t);
  const end = frames[frames.length - 1].t + 1.2;
  const jpgs = [];
  for (let t = start, i = 0; t <= end; t += 1 / FPS) {
    while (i + 1 < frames.length && frames[i + 1].t <= t) i++;
    jpgs.push(frames[i].jpg);
  }
  const x264 = ['-c:v', 'libx264', '-crf', '17', '-preset', 'slow', '-pix_fmt', 'yuv420p', '-an'];
  execFileSync(ffmpeg, ['-y', '-v', 'error', '-f', 'image2pipe', '-framerate', String(FPS), '-c:v', 'mjpeg', '-i', '-',
    '-filter_complex', '[0:v]split=2[a][b];'
      + '[a]scale=-2:1000:flags=lanczos,pad=1920:1080:(ow-iw)/2:(oh-ih)/2:color=0xff7a1a[w];'
      + '[b]scale=-2:1920:flags=lanczos,pad=1080:1920:(ow-iw)/2:(oh-ih)/2:color=0xfff4ea[v]',
    '-map', '[w]', ...x264, join(outDir, `${name}-16x9.mp4`),
    '-map', '[v]', ...x264, join(outDir, `${name}-pion.mp4`)], { input: Buffer.concat(jpgs), maxBuffer: 1 << 30 });
  console.log(`✓ ${name}: ${(jpgs.length / FPS).toFixed(1)} s → out/app/${name}-16x9.mp4, ${name}-pion.mp4`);
}

/** Pisanie jak człowiek (widać litery w nagraniu). */
async function type(page, locator, text, delay = 45) {
  await locator.click();
  await locator.pressSequentially(text, { delay });
}
const input = (page, label) => page.locator(`ion-input[label="${label}"] input`);
const textarea = (page, label) => page.locator(`ion-textarea[label="${label}"] textarea`);
/**
 * Wybór z listy w tempie, które widz zdąży przeczytać: lista otwarta ok. 1,5 s, wybrana opcja podświetlona
 * (najechanie) przed kliknięciem, a po zamknięciu chwila na odczytanie wybranej wartości w polu.
 */
async function choose(page, label, option) {
  await page.locator(`ion-select[label="${label}"]`).click();
  const sheet = page.locator('ion-action-sheet');
  await sheet.waitFor();
  await pause(page, 1500);
  const item = sheet.locator('button', { hasText: option }).first();
  await item.hover();
  await pause(page, 800);
  await item.click();
  await sheet.waitFor({ state: 'detached', timeout: 5000 }).catch(() => {});
  await pause(page, 1300);
}
/** Okno potwierdzenia (ion-alert): przycisk zatwierdzający = ostatni, nie „Nie”. */
async function confirmAlert(page) {
  const alert = page.locator('ion-alert');
  await alert.waitFor({ timeout: 8000 });
  await pause(page, 900);
  await alert.locator('button:not(.alert-button-role-cancel)').last().click();
}
async function clickButton(page, name) {
  const button = page.locator('ion-button', { hasText: name }).first();
  await button.scrollIntoViewIfNeeded();
  await pause(page, 500);
  await button.click();
}
/** Pasek z tytułem ogłoszenia w szczegółach – znak, że strona się wczytała. */
const detailsLoaded = (page) => page.locator('app-announcement-content').getByText('Cieknący kran w kuchni').first().waitFor({ timeout: 15000 });
const detailsUrl = () => `${BASE}/announcements/${state.announcementId}`;
const today = (hhmm) => { const d = new Date(); const p = (n) => String(n).padStart(2, '0'); return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${hhmm}`; };

/** Token użytkownika demo z zapisanej sesji (localStorage icanhelp.auth) – do sprzątania przez API. */
function tokenOf(user) {
  const origin = JSON.parse(readFileSync(join(tmpDir, `${user}.json`), 'utf8')).origins.find((o) => o.origin === BASE);
  const auth = JSON.parse(origin.localStorage.find((e) => e.name === 'icanhelp.auth').value);
  return auth.accessToken;
}

const SHOTS = {
  /* 0. Sprzątanie (bez nagrania): otwarte „Cieknący kran w kuchni” Marka z wcześniejszych prób – anulowane. */
  async sprzatanie() {
    await login('marek');
    const token = tokenOf('marek');
    const userId = JSON.parse(Buffer.from(token.split('.')[1], 'base64url').toString()).sub;
    const res = await fetch(`${BASE}/api/announcements?ownerId=${userId}&pageSize=100`, { headers: { Authorization: `Bearer ${token}` } });
    const items = (await res.json()).filter((a) => a.title === 'Cieknący kran w kuchni' && (a.status === 'Open' || a.status === 'Accepted'));
    for (const a of items) {
      const r = await fetch(`${BASE}/api/announcements/${a.id}/cancel`, { method: 'POST', headers: { Authorization: `Bearer ${token}` } });
      console.log(`  anulowano testowe ogłoszenie ${a.id}: ${r.status}`);
    }
    // Alerty Tomka z wcześniejszych nagrań – ujęcie 1 zakłada je od nowa.
    await login('tomek');
    const helperToken = tokenOf('tomek');
    const alerts = await (await fetch(`${BASE}/api/me/alerts`, { headers: { Authorization: `Bearer ${helperToken}` } })).json();
    for (const a of alerts) {
      const r = await fetch(`${BASE}/api/me/alerts/${a.id}`, { method: 'DELETE', headers: { Authorization: `Bearer ${helperToken}` } });
      console.log(`  usunięto testowy alert ${a.id}: ${r.status}`);
    }
  },

  /* 1. Tomek (pomocnik) ustawia alert „w okolicy”: hydraulika, okolice Piotrkowskiej. */
  async alert() {
    await shot('1-alert', 'tomek', async (page, ready) => {
      await page.goto(`${BASE}/profile/alerts`);
      await page.locator('[data-testid="add-area"]').waitFor({ timeout: 15000 });
      await pause(page, 800);
      ready();
      await pause(page, 1200);
      await page.locator('[data-testid="add-area"]').click();
      await page.locator('ion-select[label="Kategoria"]').waitFor();
      await pause(page, 900);
      await choose(page, 'Kategoria', 'Hydraulika');
      const address = page.locator('app-place-search ion-input input');
      await address.scrollIntoViewIfNeeded();
      await type(page, address, 'Piotrkowska 100 Łódź', 60);
      await page.locator('app-place-search ion-item[button]').first().waitFor({ timeout: 10000 });
      await pause(page, 600);
      await page.locator('app-place-search ion-item[button]').first().click();
      await pause(page, 1800);
      await page.locator('[data-testid="alert-save"]').scrollIntoViewIfNeeded();
      await pause(page, 800);
      await page.locator('[data-testid="alert-save"]').click();
      await page.waitForURL(/\/profile\/alerts$/, { timeout: 15000 });
      await pause(page, 2200);
    });
  },

  /* 1b. Tomek ustawia alert „po drodze”: trasa Zgierz → Piotrkowska (Łódź), pas po obu stronach drogi widać na mapie. */
  async trasa() {
    // Alerty „po drodze” z wcześniejszych prób – nagranie zakłada nowy.
    await login('tomek');
    const token = tokenOf('tomek');
    const alerts = await (await fetch(`${BASE}/api/me/alerts`, { headers: { Authorization: `Bearer ${token}` } })).json();
    for (const a of alerts.filter((x) => x.kind === 'Route')) {
      await fetch(`${BASE}/api/me/alerts/${a.id}`, { method: 'DELETE', headers: { Authorization: `Bearer ${token}` } });
      console.log(`  usunięto testowy alert ${a.id}`);
    }
    await shot('1b-trasa', 'tomek', async (page, ready) => {
      await page.goto(`${BASE}/profile/alerts`);
      await page.locator('[data-testid="add-route"]').waitFor({ timeout: 15000 });
      await pause(page, 800);
      ready();
      await pause(page, 1200);
      await page.locator('[data-testid="add-route"]').click();
      await page.locator('ion-select[label="Kategoria"]').waitFor();
      await pause(page, 900);
      await choose(page, 'Kategoria', 'Hydraulika');
      const address = page.locator('app-place-search ion-input input');
      // Podpowiedź z właściwym miastem (Photon podaje też np. Piotrkowską w Opocznie).
      const pick = async (text, city) => {
        await address.scrollIntoViewIfNeeded();
        await address.fill('');
        await type(page, address, text, 60);
        const items = page.locator('app-place-search ion-item[button]');
        await items.first().waitFor({ timeout: 10000 });
        await pause(page, 600);
        const match = items.filter({ hasText: city }).first();
        await ((await match.count()) ? match : items.first()).click();
        await pause(page, 1300);
      };
      await pick('Zgierz', 'zgierski');
      await page.locator('[data-testid="route-end"]').click();
      await pause(page, 700);
      await pick('Piotrkowska 250 Łódź', '90-');
      const plan = page.locator('[data-testid="route-plan"]');
      if (await plan.isVisible().catch(() => false)) await plan.click();
      await page.locator('[data-testid="route-ready"]').waitFor({ timeout: 20000 });
      await page.locator('app-route-picker app-map').scrollIntoViewIfNeeded();
      await pause(page, 3000); // trasa z pasem na mapie – chwila na obejrzenie
      await page.locator('[data-testid="alert-save"]').scrollIntoViewIfNeeded();
      await pause(page, 800);
      await page.locator('[data-testid="alert-save"]').click();
      await page.waitForURL(/\/profile\/alerts$/, { timeout: 15000 });
      await pause(page, 2200);
    });
  },

  /* 2. Marek dodaje zlecenie: kategoria, tytuł, opis, kwota, miejsce, zdjęcie → Opublikuj. */
  async zlecam() {
    await shot('2-zlecam', 'marek', async (page, ready) => {
      await page.goto(`${BASE}/add`);
      await page.locator('ion-select[label="Kategoria"]').waitFor();
      await pause(page, 800);
      ready();
      await pause(page, 600);
      await choose(page, 'Kategoria', 'Hydraulika');
      await choose(page, 'Podkategoria', 'Cieknący kran');
      await type(page, input(page, 'Tytuł'), 'Cieknący kran w kuchni');
      await type(page, textarea(page, 'Opis'), 'Kapie całą noc, chyba trzeba wymienić uszczelkę.', 30);
      await input(page, 'Ile chcesz zapłacić (zł)').scrollIntoViewIfNeeded();
      await type(page, input(page, 'Ile chcesz zapłacić (zł)'), '30', 120);
      const address = page.locator('app-place-search ion-input input');
      await address.scrollIntoViewIfNeeded();
      await type(page, address, 'Piotrkowska 100 Łódź', 60);
      await page.locator('app-place-search ion-item[button]').first().waitFor({ timeout: 10000 });
      await pause(page, 600);
      await page.locator('app-place-search ion-item[button]').first().click();
      await pause(page, 1500);
      await page.locator('app-photo-picker input[type="file"]').last().setInputFiles(join(here, 'assets', 'kran.jpg')); // prawdziwe zdjęcie kapiącego kranu
      await page.locator('app-photo-picker img').first().waitFor({ timeout: 10000 });
      await page.locator('app-photo-picker').scrollIntoViewIfNeeded();
      await pause(page, 1200);
      await page.getByRole('button', { name: /Opublikuj ogłoszenie/ }).click();
      await page.waitForURL(/\/announcements\/[0-9a-f-]{36}/, { timeout: 15000 });
      state.announcementId = page.url().match(/announcements\/([0-9a-f-]{36})/)[1];
      saveState();
      await detailsLoaded(page);
      await pause(page, 2000);
      await page.mouse.wheel(0, 500);
      await pause(page, 1800);
    });
  },

  /* 3. Tomek dostaje powiadomienie z alertu, otwiera ogłoszenie i składa ofertę: 35 zł, dziś 17:30–18:00, z wiadomością. */
  async oferta() {
    await shot('3-oferta', 'tomek', async (page, ready) => {
      await page.goto(`${BASE}/notifications`);
      const item = page.locator('ion-item', { hasText: 'Cieknący kran w kuchni' }).first();
      const hasNotice = await item.waitFor({ timeout: 10000 }).then(() => true, () => false);
      if (hasNotice) {
        await pause(page, 600);
        ready();
        await pause(page, 1500);
        await item.click();
        await detailsLoaded(page);
      } else {
        // Bez powiadomienia (np. lista się nie wczytała) – klip zaczyna się od wczytanego ogłoszenia, bez kółka ładowania.
        await page.goto(detailsUrl());
        await detailsLoaded(page);
        await pause(page, 600);
        ready();
      }
      await pause(page, 1800);
      await page.mouse.wheel(0, 400);
      await pause(page, 1200);
      await clickButton(page, 'Złóż ofertę');
      const modal = page.locator('ion-modal');
      await modal.locator('ion-input[label="Cena (zł)"] input').waitFor();
      await pause(page, 700);
      const price = modal.locator('ion-input[label="Cena (zł)"] input');
      await price.fill('');
      await type(page, price, '35', 150);
      await modal.locator('ion-input[label="Od"] input').fill(today('17:30'));
      await pause(page, 500);
      await modal.locator('ion-input[label="Do"] input').fill(today('18:00'));
      await pause(page, 500);
      await type(page, modal.locator('ion-textarea[label="Wiadomość (opcjonalnie)"] textarea'), 'Trochę więcej pracy, niż się wydaje – wymienię uszczelkę, mam ją ze sobą.', 28);
      await pause(page, 800);
      await modal.locator('ion-button', { hasText: 'Wyślij' }).click();
      if (await page.locator('ion-alert').waitFor({ timeout: 2500 }).then(() => true, () => false)) await confirmAlert(page);
      await page.locator('ion-modal').waitFor({ state: 'detached', timeout: 10000 }).catch(() => {});
      // Po wysłaniu aplikacja otwiera rozmowę z kartą oferty („Czeka na odpowiedź”).
      await page.getByText('Czeka na odpowiedź').first().waitFor({ timeout: 15000 });
      await pause(page, 3000);
    });
  },

  /* 4. Marek widzi ofertę Tomka i ją przyjmuje – pojawia się kontakt. */
  async przyjecie() {
    await shot('4-przyjecie', 'marek', async (page, ready) => {
      await page.goto(detailsUrl());
      await detailsLoaded(page);
      await pause(page, 600);
      ready();
      await pause(page, 1200);
      // Oferty są w sekcji „Oferty i rozmowy” – wątek z Tomkiem ma przycisk „Przyjmij”.
      const thread = page.locator('ion-list.negotiations ion-item').first();
      await thread.scrollIntoViewIfNeeded();
      await pause(page, 1200);
      await thread.click();
      await page.waitForURL(/\/conversations\//, { timeout: 15000 });
      await page.locator('ion-button', { hasText: 'Przyjmij' }).first().waitFor({ timeout: 15000 });
      await pause(page, 1800);
      await clickButton(page, 'Przyjmij');
      await confirmAlert(page);
      await pause(page, 3500);
    });
  },

  /* 5. Tomek na miejscu: „Rozpocznij zlecenie”, a po naprawie „Zgłoś wykonanie”. */
  async realizacja() {
    await shot('5-realizacja', 'tomek', async (page, ready) => {
      await page.goto(detailsUrl());
      await detailsLoaded(page);
      await pause(page, 600);
      ready();
      await pause(page, 1500);
      await clickButton(page, 'Rozpocznij zlecenie');
      await confirmAlert(page);
      await pause(page, 2500);
      await clickButton(page, 'Zgłoś wykonanie');
      await confirmAlert(page);
      await pause(page, 2500);
    });
  },

  /* 6. Marek potwierdza wykonanie i ocenia Tomka na 5 gwiazdek. */
  async potwierdzenie() {
    await shot('6-potwierdzenie', 'marek', async (page, ready) => {
      await page.goto(detailsUrl());
      await detailsLoaded(page);
      await pause(page, 600);
      ready();
      await pause(page, 1500);
      await clickButton(page, 'Potwierdź wykonanie');
      await confirmAlert(page);
      await pause(page, 2200);
      await clickButton(page, 'Oceń pomocnika');
      const modal = page.locator('ion-modal');
      await modal.locator('[aria-label="5 na 5"]').waitFor();
      await pause(page, 800);
      for (const n of [1, 2, 3, 4, 5]) { await modal.locator(`[aria-label="${n} na 5"]`).click(); await pause(page, 160); }
      await type(page, modal.locator('ion-textarea textarea'), 'Szybko, czysto i w umówionej cenie. Polecam!', 35);
      await pause(page, 700);
      await modal.locator('ion-button', { hasText: 'Wyślij ocenę' }).click();
      await page.locator('ion-modal').waitFor({ state: 'detached', timeout: 10000 }).catch(() => {});
      await pause(page, 2500);
    });
  },
};

const wanted = process.argv.slice(2);
try {
  for (const [name, run] of Object.entries(SHOTS)) {
    if (wanted.length === 0 || wanted.includes(name)) await run();
  }
} finally {
  await browser.close();
}
