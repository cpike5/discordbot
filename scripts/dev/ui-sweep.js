#!/usr/bin/env node
/*
 * UI sweep for docs/plans/ui-audit-improvements.md section 11.
 *
 * Signs in, visits the audit routes at 1440x900 and 390x844 in both themes, and records the
 * status code, console errors, horizontal overflow (scrollWidth - clientWidth) and axe-core
 * serious/critical violations, plus the number of <h1>s and the first one's left edge (every top-level
 * page's h1 should start at the same x). Screenshots and results.json go into the output folder.
 *
 *   node scripts/dev/ui-sweep.js <out-dir> [--base http://localhost:5124]
 *        [--email admin@example.com] [--password 'Audit-Pass-123!']
 *        [--guild 123456789012345678] [--guild2 223456789012345678]
 *        [--only substring] [--no-axe]
 *
 * Needs Playwright (npm i playwright, or NODE_PATH pointing at an install) and Chromium.
 * Seed data first with scripts/dev/seed-ui-audit.sql. Exit code is 1 when a route returns
 * 4xx/5xx or axe reports a serious/critical violation.
 */
'use strict';

const fs = require('fs');
const path = require('path');

let chromium;
try {
  ({ chromium } = require('playwright'));
} catch (e) {
  console.error('Cannot load playwright. Install it (npm i playwright) or set NODE_PATH.');
  process.exit(2);
}

const argv = process.argv.slice(2);
const opts = {
  base: 'http://localhost:5124',
  email: 'admin@example.com',
  password: 'Audit-Pass-123!',
  guild: '123456789012345678',
  guild2: '223456789012345678',
  only: null,
  axe: true,
};
let outDir = null;
for (let i = 0; i < argv.length; i++) {
  const a = argv[i];
  if (a === '--no-axe') opts.axe = false;
  else if (a.startsWith('--')) opts[a.slice(2)] = argv[++i];
  else outDir = a;
}
if (!outDir) {
  console.error('Usage: node scripts/dev/ui-sweep.js <out-dir> [--base url] [--only substring] [--no-axe]');
  process.exit(2);
}
fs.mkdirSync(outDir, { recursive: true });

const g = opts.guild;
const g2 = opts.guild2;
const ROUTES = [
  '/',
  '/Guilds',
  `/Guilds/Details/${g}`,
  `/Guilds/Details/${g2}`,
  `/Guilds/${g}/Members`,
  `/Guilds/Reminders/${g}`,
  `/Guilds/ScheduledMessages/${g}`,
  `/Guilds/Soundboard/${g}`,
  `/Guilds/AudioSettings/${g}`,
  `/Guilds/TextToSpeech/${g}`,
  `/Guilds/VOX/${g}`,
  `/Guilds/Analytics/${g}`,
  `/Guilds/ModerationSettings/${g}`,
  `/Guilds/AssistantSettings/${g}`,
  `/Guilds/Edit/${g}`,
  `/Portal/Soundboard/${g}`,
  `/Portal/TTS/${g}`,
  `/Portal/VOX/${g}`,
  '/Commands',
  '/Search?q=pixel',
  '/Admin/Performance',
  '/Admin/Performance/SystemHealth',
  '/Admin/Logs',
  '/Admin/Users',
  '/Admin/Settings',
  '/Admin/Notifications',
  '/Admin/LlmUsage',
  '/Admin/Currency',
  '/Admin/BulkPurge',
  '/Admin/UserPurge',
  '/Admin/RatWatchAnalytics',
  '/Account/Profile',
  '/Components',
  '/landing',
];
const VIEWPORTS = [
  { name: 'desktop', width: 1440, height: 900 },
  { name: 'mobile', width: 390, height: 844 },
];
const THEMES = ['dark', 'light'];
// The stylesheet's theme keys: 'light'/'dark' match no [data-theme] rule, so both runs used to render dark
const THEME_KEYS = { dark: 'discord-dark', light: 'purple-dusk' };

const slug = (r) => (r.replace(/[^A-Za-z0-9]+/g, '_').replace(/^_|_$/g, '') || 'home').slice(0, 80);
const AXE_URL = 'https://cdnjs.cloudflare.com/ajax/libs/axe-core/4.10.2/axe.min.js';

async function signIn(browser) {
  const ctx = await browser.newContext({ viewport: VIEWPORTS[0] });
  const page = await ctx.newPage();
  await page.goto(`${opts.base}/Account/Login`, { waitUntil: 'domcontentloaded' });
  await page.fill('input[name="Input.Email"], input[type="email"]', opts.email);
  await page.fill('input[name="Input.Password"], input[type="password"]', opts.password);
  await Promise.all([
    page.waitForLoadState('domcontentloaded'),
    page.click('form button[type="submit"]:has-text("Sign in"), form button[type="submit"]:has-text("Log in"), form button[type="submit"]'),
  ]);
  if (page.url().includes('/Account/Login')) throw new Error('Sign-in failed; check --email/--password');
  const state = await ctx.storageState();
  await ctx.close();
  return state;
}

(async () => {
  const browser = await chromium.launch();
  const state = await signIn(browser);
  const results = [];
  let failed = false;

  for (const vp of VIEWPORTS) {
    for (const theme of THEMES) {
      const ctx = await browser.newContext({
        viewport: { width: vp.width, height: vp.height },
        storageState: state,
        colorScheme: theme,
      });
      await ctx.addInitScript((t) => {
        try { localStorage.setItem('theme', t); } catch (e) { /* storage may be blocked */ }
      }, THEME_KEYS[theme]);
      for (const route of ROUTES) {
        if (opts.only && !route.includes(opts.only)) continue;
        const page = await ctx.newPage();
        const consoleErrors = [];
        page.on('console', (m) => { if (m.type() === 'error') consoleErrors.push(m.text()); });
        page.on('pageerror', (e) => consoleErrors.push(String(e)));
        const row = { route, viewport: vp.name, theme, status: null, consoleErrors, overflow: null, h1Count: null, h1Left: null, axe: [] };
        try {
          const resp = await page.goto(opts.base + route, { waitUntil: 'networkidle', timeout: 30000 });
          row.status = resp ? resp.status() : null;
          await page.evaluate((t) => document.documentElement.setAttribute('data-theme', t), THEME_KEYS[theme]);
          row.overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
          ({ count: row.h1Count, left: row.h1Left } = await page.evaluate(() => {
            const h1s = document.querySelectorAll('h1');
            return { count: h1s.length, left: h1s.length ? Math.round(h1s[0].getBoundingClientRect().left) : null };
          }));
          if (opts.axe) {
            try {
              await page.addScriptTag({ url: AXE_URL });
              const violations = await page.evaluate(async () => (await window.axe.run()).violations
                .filter((v) => v.impact === 'serious' || v.impact === 'critical')
                .map((v) => ({ id: v.id, impact: v.impact, nodes: v.nodes.length })));
              row.axe = violations;
            } catch (e) {
              row.axeError = String(e.message || e).split('\n')[0];
            }
          }
          await page.screenshot({ path: path.join(outDir, `${slug(route)}__${vp.name}__${theme}.png`), fullPage: true });
        } catch (e) {
          row.error = String(e.message || e).split('\n')[0];
        }
        if (row.error || (row.status !== null && row.status >= 400) || row.axe.length) failed = true;
        results.push(row);
        console.log(`${row.status ?? 'ERR'} ${vp.name}/${theme} ${route}` +
          ` overflow=${row.overflow} h1=${row.h1Count}@${row.h1Left} console=${consoleErrors.length} axe=${row.axe.length}` +
          (row.error ? ` error=${row.error}` : ''));
        await page.close();
      }
      await ctx.close();
    }
  }

  fs.writeFileSync(path.join(outDir, 'results.json'), JSON.stringify(results, null, 2));
  await browser.close();
  process.exit(failed ? 1 : 0);
})().catch((e) => { console.error(e); process.exit(2); });
