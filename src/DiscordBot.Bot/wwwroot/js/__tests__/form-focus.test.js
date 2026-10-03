const test = require('node:test');
const assert = require('node:assert/strict');
const FormFocus = require('../form-focus.js');

/** A scope whose querySelector answers for the given selector fragments. */
function scopeWith(control, matches) {
    return { querySelector: (selector) => (matches.some((m) => selector.includes(m)) ? control : null) };
}

test('one selector covers aria-invalid, the validation class and invalid radio card groups', () => {
    let seen = '';
    FormFocus.firstInvalid({ querySelector: (s) => { seen = s; return null; } });
    assert.match(seen, /aria-invalid="true"/);
    assert.match(seen, /input-validation-error/);
    assert.match(seen, /radio-card-group-invalid/);
});

test('focusFirstInvalid focuses without a jump, scrolls to the control and returns it', () => {
    const calls = [];
    const control = {
        focus(options) { calls.push(['focus', options]); },
        scrollIntoView(options) { calls.push(['scroll', options.block]); }
    };

    assert.equal(FormFocus.focusFirstInvalid(scopeWith(control, ['aria-invalid'])), control);
    assert.deepEqual(calls[0], ['focus', { preventScroll: true }]);
    assert.deepEqual(calls[1], ['scroll', 'center']);
});

test('a scope without errors, or without a querySelector, is left alone', () => {
    assert.equal(FormFocus.focusFirstInvalid({ querySelector: () => null }), null);
    assert.equal(FormFocus.focusFirstInvalid({}), null);
    assert.equal(FormFocus.firstInvalid(null), null);
});

test('an invalid control inside an inert region does not take focus', () => {
    let focused = false;
    const control = { focus() { focused = true; }, closest: (s) => (s === '[inert]' ? {} : null) };

    assert.equal(FormFocus.focusFirstInvalid(scopeWith(control, ['aria-invalid'])), null);
    assert.equal(focused, false);
});

test('focusFirstError is the same function, so form-focus callers keep working', () => {
    assert.equal(FormFocus.focusFirstError, FormFocus.focusFirstInvalid);
});
