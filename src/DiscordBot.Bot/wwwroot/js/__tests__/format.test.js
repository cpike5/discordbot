const test = require('node:test');
const assert = require('node:assert/strict');
const Format = require('../format.js');

const NOW = new Date('2026-10-03T12:00:00Z');
const at = (ms) => new Date(NOW.getTime() + ms);
const MIN = 60 * 1000;
const HOUR = 60 * MIN;
const DAY = 24 * HOUR;

test('parseUtc reads a timestamp with no zone designator as UTC', () => {
    assert.equal(Format.parseUtc('2026-10-03T12:00:00').toISOString(), '2026-10-03T12:00:00.000Z');
    assert.equal(Format.parseUtc('2026-10-03 12:00:00.5').toISOString(), '2026-10-03T12:00:00.500Z');
    assert.equal(Format.parseUtc('2026-10-03T12:00:00Z').toISOString(), '2026-10-03T12:00:00.000Z');
    assert.equal(Format.parseUtc('2026-10-03T14:00:00+02:00').toISOString(), '2026-10-03T12:00:00.000Z');
});

test('parseUtc returns null for empty and invalid values', () => {
    assert.equal(Format.parseUtc(''), null);
    assert.equal(Format.parseUtc(null), null);
    assert.equal(Format.parseUtc('not a date'), null);
});

test('formatDate follows the locale and time zone it is given', () => {
    const us = Format.formatDate('2026-10-03T18:05:00Z', 'datetime', { locale: 'en-US', timeZone: 'America/New_York' });
    assert.match(us, /Oct 3, 2026/);
    assert.match(us, /2:05\s?PM/);

    const de = Format.formatDate('2026-10-03T18:05:00Z', 'datetime', { locale: 'de-DE', timeZone: 'Europe/Berlin' });
    assert.match(de, /20:05/);
    assert.doesNotMatch(de, /PM/);
});

test('formatDate uses the locale 12/24 hour convention instead of forcing one', () => {
    const gb = Format.formatDate('2026-10-03T18:05:00Z', 'time', { locale: 'en-GB', timeZone: 'UTC' });
    assert.equal(gb, '18:05');
    const us = Format.formatDate('2026-10-03T18:05:00Z', 'time', { locale: 'en-US', timeZone: 'UTC' });
    assert.match(us, /^6:05\s?PM$/);
});

test('formatDate date style has no time and returns empty text for bad input', () => {
    const d = Format.formatDate('2026-10-03T01:00:00Z', 'date', { locale: 'en-US', timeZone: 'UTC' });
    assert.equal(d, 'Oct 3, 2026');
    assert.equal(Format.formatDate('nope', 'date'), '');
});

test('formatDate shifts the calendar day with the time zone', () => {
    const tokyo = Format.formatDate('2026-10-03T20:00:00Z', 'date', { locale: 'en-US', timeZone: 'Asia/Tokyo' });
    assert.equal(tokyo, 'Oct 4, 2026');
});

test('relativeTime words for the past and future', () => {
    const o = { locale: 'en', now: NOW };
    assert.equal(Format.relativeTime(at(-10 * 1000), o), 'now');
    assert.equal(Format.relativeTime(at(-5 * MIN), o), '5 minutes ago');
    assert.equal(Format.relativeTime(at(-1 * MIN - 5000), o), '1 minute ago');
    assert.equal(Format.relativeTime(at(-3 * HOUR), o), '3 hours ago');
    assert.equal(Format.relativeTime(at(-1 * DAY), o), 'yesterday');
    assert.equal(Format.relativeTime(at(-4 * DAY), o), '4 days ago');
    assert.equal(Format.relativeTime(at(2 * HOUR), o), 'in 2 hours');
    assert.equal(Format.relativeTime(at(1 * DAY), o), 'tomorrow');
});

test('relativeTime never rounds a real gap down to "now"', () => {
    const o = { locale: 'en', now: NOW };
    assert.equal(Format.relativeTime(at(-50 * 1000), o), '1 minute ago');
    assert.equal(Format.relativeTime(at(-HOUR - 10 * MIN), o), '1 hour ago');
});

test('relativeTime falls back to a date after 30 days', () => {
    const text = Format.relativeTime(at(-45 * DAY), { locale: 'en-US', timeZone: 'UTC', now: NOW });
    assert.equal(text, 'Aug 19, 2026');
});

test('relativeTime follows the locale', () => {
    assert.equal(Format.relativeTime(at(-5 * MIN), { locale: 'fr', now: NOW }), 'il y a 5 minutes');
});

test('relativeTime treats a zone-less timestamp as UTC', () => {
    assert.equal(Format.relativeTime('2026-10-03T11:55:00', { locale: 'en', now: NOW }), '5 minutes ago');
});

test('plural picks the form and groups the count', () => {
    assert.equal(Format.plural(1, 'server', undefined, { locale: 'en' }), '1 server');
    assert.equal(Format.plural(0, 'server', undefined, { locale: 'en' }), '0 servers');
    assert.equal(Format.plural(2, 'server', undefined, { locale: 'en' }), '2 servers');
    assert.equal(Format.plural(1234, 'entry', 'entries', { locale: 'en-US' }), '1,234 entries');
    assert.equal(Format.plural(1, 'entry', 'entries', { locale: 'en' }), '1 entry');
});

test('number uses the locale grouping and decimal marks', () => {
    assert.equal(Format.number(1234567.5, { locale: 'en-US' }), '1,234,567.5');
    assert.equal(Format.number(1234567.5, { locale: 'de-DE' }), '1.234.567,5');
    assert.equal(Format.number(1.23456, { locale: 'en-US', maximumFractionDigits: 2 }), '1.23');
    assert.equal(Format.number('abc'), '');
});

test('duration shows the two largest units', () => {
    assert.equal(Format.duration(0), '<1s');
    assert.equal(Format.duration(500), '<1s');
    assert.equal(Format.duration(45 * 1000), '45s');
    assert.equal(Format.duration(90 * 1000), '1m 30s');
    assert.equal(Format.duration(30 * MIN), '30m');
    assert.equal(Format.duration(5 * HOUR + 30 * MIN + 10 * 1000), '5h 30m');
    assert.equal(Format.duration(2 * DAY + 5 * HOUR + 30 * MIN), '2d 5h');
    assert.equal(Format.duration(2 * DAY + 5 * HOUR + 30 * MIN, { maxUnits: 3 }), '2d 5h 30m');
    assert.equal(Format.duration(-1), '');
});

test('currency uses the locale style for ISO codes when asked', () => {
    assert.equal(Format.currency(1234.5, 'USD', { locale: 'en-US', iso: true }), '$1,234.50');
    assert.match(Format.currency(1234.5, 'EUR', { locale: 'de-DE', iso: true }), /1\.234,50\s€/);
});

test('currency writes a virtual currency symbol after the amount, in whole units', () => {
    assert.equal(Format.currency(1250, '🪙', { locale: 'en-US' }), '1,250 🪙');
    // A three-letter virtual currency is not mistaken for an ISO code.
    assert.equal(Format.currency(5, 'GEM', { locale: 'en-US' }), '5 GEM');
    assert.equal(Format.currency(1250.9, 'GEM', { locale: 'en-US' }), '1,251 GEM');
});

test('DATE_STYLES never forces hour12', () => {
    for (const [name, style] of Object.entries(Format.DATE_STYLES)) {
        assert.equal('hour12' in style, false, name);
    }
});

// ---- Relative-time elements in a page (tab stops and the tooltip's ARIA wiring) ---------------

const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

function fakeEl({ closest = {}, attrs = {} } = {}) {
    const el = {
        nodeType: 1,
        tagName: 'TIME',
        attrs: { 'data-relative-time': '2026-10-03T11:00:00Z', ...attrs },
        classes: [],
        classList: { add: (c) => el.classes.push(c) },
        textContent: '',
        getAttribute: (k) => (k in el.attrs ? el.attrs[k] : null),
        setAttribute: (k, v) => { el.attrs[k] = String(v); },
        removeAttribute: (k) => { delete el.attrs[k]; },
        hasAttribute: (k) => k in el.attrs,
        matches: () => true,
        getBoundingClientRect: () => ({ width: 10, height: 10, left: 0, top: 0, bottom: 0 }),
        // `closest` answers per selector family: 'focusable' for the interactive-element selector, 'table'.
        closest: (sel) => {
            if (sel === 'table') return closest.table ? {} : null;
            if (sel.includes('[tabindex]')) return closest.focusable ? {} : null;
            return sel.includes('data-relative-time') ? el : null;
        }
    };
    return el;
}

// Loads format.js against a fake window so init() wires its listeners, and returns them.
function loadInPage(elements) {
    const listeners = {};
    const tip = fakeEl();
    tip.hidden = true;
    tip.style = {};
    tip.getBoundingClientRect = () => ({ width: 10, height: 10, left: 0, top: 0, bottom: 0 });
    const doc = {
        readyState: 'complete',
        hidden: false,
        documentElement: { clientWidth: 1000 },
        body: { appendChild() {} },
        createElement: () => tip,
        querySelectorAll: () => elements,
        addEventListener: (t, f) => { (listeners[t] = listeners[t] || []).push(f); }
    };
    const ctx = {
        document: doc,
        setInterval: () => 1,
        clearInterval: () => {},
        Intl,
        Date,
        addEventListener: () => {}
    };
    ctx.window = ctx;
    vm.runInNewContext(fs.readFileSync(path.join(__dirname, '..', 'format.js'), 'utf8'), ctx);
    return { listeners, tip };
}

test('a lone relative time becomes a Tab stop', () => {
    const el = fakeEl();
    loadInPage([el]);
    assert.equal(el.attrs.tabindex, '0');
    assert.equal(el.hasAttribute('title'), false);
});

test('a relative time inside a focusable control or a table gets no Tab stop but keeps the absolute time as its title', () => {
    for (const closest of [{ focusable: true }, { table: true }]) {
        const el = fakeEl({ closest });
        loadInPage([el]);
        assert.equal(el.hasAttribute('tabindex'), false, JSON.stringify(closest));
        assert.equal(el.attrs.title, el.attrs['data-absolute']);
        assert.ok(el.attrs.title.length > 0);
    }
});

test('an element that already has a tabindex keeps it and gets no title', () => {
    const el = fakeEl({ attrs: { tabindex: '-1' } });
    loadInPage([el]);
    assert.equal(el.attrs.tabindex, '-1');
    assert.equal(el.hasAttribute('title'), false);
});

test('the tooltip appends to an existing aria-describedby and restores it on hide', () => {
    const el = fakeEl({ attrs: { 'aria-describedby': 'hint' } });
    const { listeners } = loadInPage([el]);
    const target = { closest: () => el };

    listeners.mouseover[0]({ target });
    assert.equal(el.attrs['aria-describedby'], 'hint format-tooltip');

    listeners.focusout[0]();
    assert.equal(el.attrs['aria-describedby'], 'hint');
});

test('the tooltip removes the aria-describedby it added when there was none, and holds the title back meanwhile', () => {
    const el = fakeEl({ closest: { table: true } });
    const { listeners } = loadInPage([el]);
    const abs = el.attrs.title;
    const target = { closest: () => el };

    listeners.mouseover[0]({ target });
    assert.equal(el.attrs['aria-describedby'], 'format-tooltip');
    assert.equal(el.hasAttribute('title'), false);

    listeners.focusout[0]();
    assert.equal(el.hasAttribute('aria-describedby'), false);
    assert.equal(el.attrs.title, abs);
});
