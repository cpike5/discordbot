const test = require('node:test');
const assert = require('node:assert/strict');

// filter-panel.js reads DateRangeFilter from the global scope, as the browser provides it
globalThis.DateRangeFilter = require('../date-range-filter.js');
const FilterPanel = require('../shared/filter-panel.js');

// Built from local components, so the assertions hold in whatever zone the test runs in
const range = (preset) => globalThis.DateRangeFilter.presetRange(preset);

/** A form with start and end inputs and one button per preset, shaped like _DateRangeFilter. */
function fakeForm(start, end, presets) {
    const startInput = { value: start };
    const endInput = { value: end };
    const form = {
        submitted: 0,
        requestSubmit() { this.submitted++; },
        querySelector(selector) {
            if (selector === '[data-date-start]') return startInput;
            if (selector === '[data-date-end]') return endInput;
            return null;
        }
    };
    const buttons = presets.map((key) => ({
        attrs: { 'data-date-preset': key, 'aria-pressed': 'false' },
        getAttribute(name) { return this.attrs[name]; },
        setAttribute(name, value) { this.attrs[name] = value; },
        closest(selector) { return selector === 'form' ? form : null; }
    }));
    const group = {
        querySelectorAll() { return buttons; },
        getAttribute() { return null; }
    };
    const scope = {
        querySelectorAll(selector) { return selector === '[data-date-range-presets]' ? [group] : []; }
    };
    form.querySelectorAll = scope.querySelectorAll;
    return { form, startInput, endInput, buttons, scope };
}

test('a preset button fills the inputs with the viewer\'s local range and submits the form', () => {
    const f = fakeForm('', '', ['today', '7days', '30days']);
    const seven = f.buttons[1];

    assert.equal(FilterPanel.applyPresetButton(seven), true);

    assert.equal(f.startInput.value, range('7days').start);
    assert.equal(f.endInput.value, range('7days').end);
    assert.equal(f.form.submitted, 1);
    assert.equal(seven.attrs['aria-pressed'], 'true');
    assert.equal(f.buttons[0].attrs['aria-pressed'], 'false');
});

test('markPresets marks the button matching the dates and none for a custom range', () => {
    const month = range('30days');
    const f = fakeForm(month.start, month.end, ['today', '7days', '30days']);
    FilterPanel.markPresets(f.scope);
    assert.deepEqual(f.buttons.map((b) => b.attrs['aria-pressed']), ['false', 'false', 'true']);

    f.startInput.value = '2020-01-01';
    FilterPanel.markPresets(f.scope);
    assert.deepEqual(f.buttons.map((b) => b.attrs['aria-pressed']), ['false', 'false', 'false']);
});

test('an unknown preset changes nothing and submits nothing', () => {
    const f = fakeForm('2026-01-01', '2026-01-02', ['fortnight']);
    assert.equal(FilterPanel.applyPresetButton(f.buttons[0]), false);
    assert.equal(f.startInput.value, '2026-01-01');
    assert.equal(f.form.submitted, 0);
});

test('a button outside a form with date inputs is ignored', () => {
    const orphan = {
        getAttribute() { return '7days'; },
        closest() { return null; }
    };
    assert.equal(FilterPanel.applyPresetButton(orphan), false);
});

// ---- live search -----------------------------------------------------------------------------

/** A search input and a hand-driven clock, so "after 300ms and not before" is exact. */
function liveHarness(initial, href) {
    const listeners = {};
    const input = {
        value: initial,
        id: 'SearchTerm',
        selectionStart: initial.length,
        getAttribute(name) { return name === 'name' ? 'SearchTerm' : null; },
        addEventListener(type, handler) { listeners[type] = handler; }
    };
    let now = 0;
    let pending = null;
    const timers = {
        setTimeout(fn, ms) { pending = { fn, at: now + ms }; return 1; },
        clearTimeout() { pending = null; }
    };
    const navigations = [];
    const live = FilterPanel.attachLive(input, {
        timers,
        href: () => href,
        navigate: (url) => navigations.push(url)
    });
    const advance = (ms) => {
        now += ms;
        if (pending && pending.at <= now) { const p = pending; pending = null; p.fn(); }
    };
    return { input, listeners, navigations, live, advance };
}

test('typing navigates only after the 300ms debounce, with the term in the query', () => {
    const h = liveHarness('', '/Guilds/1/Members');
    h.input.value = 'user1';
    h.listeners.input();
    h.advance(299);
    assert.deepEqual(h.navigations, []);
    h.advance(1);
    assert.deepEqual(h.navigations, ['/Guilds/1/Members?SearchTerm=user1']);
});

test('each keystroke restarts the wait, so only the last value navigates', () => {
    const h = liveHarness('', '/Members');
    h.input.value = 'u';
    h.listeners.input();
    h.advance(200);
    h.input.value = 'us';
    h.listeners.input();
    h.advance(200);
    assert.deepEqual(h.navigations, []);
    h.advance(100);
    assert.deepEqual(h.navigations, ['/Members?SearchTerm=us']);
});

test('a live search keeps other filters, drops paging, and clearing the box drops the term', () => {
    assert.equal(
        FilterPanel.liveUrl('/Members?RoleFilter=1&RoleFilter=2&pageNumber=3&SearchTerm=old', 'SearchTerm', ' new '),
        '/Members?RoleFilter=1&RoleFilter=2&SearchTerm=new');
    assert.equal(FilterPanel.liveUrl('/Members?pageNumber=3&SearchTerm=old', 'SearchTerm', ''), '/Members');
});

test('Enter searches at once and an unchanged value does not reload', () => {
    const h = liveHarness('abc', '/Members?SearchTerm=abc');
    let prevented = false;
    h.listeners.keydown({ key: 'Enter', preventDefault() { prevented = true; } });
    assert.equal(prevented, true);
    assert.deepEqual(h.navigations, []);

    h.input.value = 'abcd';
    h.listeners.keydown({ key: 'Enter', preventDefault() {} });
    assert.deepEqual(h.navigations, ['/Members?SearchTerm=abcd']);
});

// ---- chips -----------------------------------------------------------------------------------

test('chipsHtml gives every remove button an accessible name and escapes the label', () => {
    const html = FilterPanel.chipsHtml([
        { label: 'Role: <b>Mod</b>', key: 'RoleFilter' },
        { label: 'Status: Failure', key: 'StatusFilter' }
    ]);
    assert.ok(html.includes('aria-label="Remove filter: Role: &lt;b&gt;Mod&lt;/b&gt;"'));
    assert.ok(html.includes('data-chip-key="StatusFilter"'));
    assert.ok(!html.includes('<b>'));
    assert.ok(html.includes('data-chip-clear-all'));
});

test('chipsHtml writes nothing for no chips and no Clear all for one chip', () => {
    assert.equal(FilterPanel.chipsHtml([]), '');
    assert.ok(!FilterPanel.chipsHtml([{ label: 'A', key: 'a' }]).includes('data-chip-clear-all'));
});
