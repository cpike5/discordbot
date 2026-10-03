const test = require('node:test');
const assert = require('node:assert/strict');
const FormFocus = require('../form-focus.js');
const Purge = require('../purge.js');

test('FormFocus: focuses the first invalid control and reports it', () => {
    let focused = null;
    const control = { focus() { focused = this; } };
    const form = { querySelector: (selector) => (selector.includes('aria-invalid') ? control : null) };
    assert.equal(FormFocus.focusFirstError(form), control);
    assert.equal(focused, control);
});

test('FormFocus: a form with no errors is left alone', () => {
    const form = { querySelector: () => null };
    assert.equal(FormFocus.focusFirstError(form), null);
    assert.equal(FormFocus.firstInvalid(null), null);
});

test('FormFocus: the selector also finds the first radio of an invalid card group', () => {
    let seen = '';
    FormFocus.firstInvalid({ querySelector: (s) => { seen = s; return null; } });
    assert.match(seen, /radio-card-group-invalid/);
});

test('Purge.progressView: percent from the payload, or worked out, and clamped', () => {
    assert.equal(Purge.progressView({ percentComplete: 40, processedCount: 4, totalCount: 10 }).percent, 40);
    assert.equal(Purge.progressView({ processedCount: 3, totalCount: 12 }).percent, 25);
    assert.equal(Purge.progressView({ percentComplete: 140 }).percent, 100);
    assert.equal(Purge.progressView({ processedCount: 0, totalCount: 0 }).percent, null);
});

test('Purge.progressView: detail line and message', () => {
    const view = Purge.progressView({ processedCount: 1200, totalCount: 5000, message: 'Deleting records...' });
    assert.equal(view.message, 'Deleting records...');
    assert.match(view.detail, /of/);
    assert.deepEqual(Purge.progressView(null), { percent: null, message: null, detail: null });
});
