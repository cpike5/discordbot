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
