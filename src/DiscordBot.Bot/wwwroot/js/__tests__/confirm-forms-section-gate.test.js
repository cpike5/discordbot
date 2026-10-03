const test = require('node:test');
const assert = require('node:assert/strict');
const { optionsFor, needsConfirmation } = require('../confirm-forms.js');
const { apply } = require('../section-gate.js');

function classList() {
    const set = new Set();
    return {
        set,
        toggle(name, force) { if (force) set.add(name); else set.delete(name); }
    };
}

test.describe('ConfirmForms', () => {
    test('a form with a message is held back until confirmed', () => {
        assert.equal(needsConfirmation({ dataset: { confirmMessage: 'Sure?' } }), true);
        assert.equal(needsConfirmation({ dataset: { confirmMessage: 'Sure?', confirmed: 'true' } }), false);
    });

    test('a form without a message is left alone', () => {
        assert.equal(needsConfirmation({ dataset: {} }), false);
        assert.equal(needsConfirmation(null), false);
    });

    test('dialog options default to a plain warning', () => {
        assert.deepEqual(optionsFor({ confirmMessage: 'x' }), {
            title: 'Are you sure?', message: 'x', confirmText: 'Confirm', cancelText: 'Cancel', variant: 'warning'
        });
    });

    test('dialog options follow the data attributes', () => {
        const o = optionsFor({ confirmTitle: 'Delete?', confirmMessage: 'Gone.', confirmText: 'Delete', confirmCancel: 'Keep', confirmVariant: 'danger' });
        assert.equal(o.title, 'Delete?');
        assert.equal(o.confirmText, 'Delete');
        assert.equal(o.cancelText, 'Keep');
        assert.equal(o.variant, 'danger');
    });
});

test.describe('SectionGate.apply', () => {
    test('off makes the body inert, marks it, and shows the reason', () => {
        const body = { inert: false, classList: classList() };
        const note = { hidden: true };
        apply(body, note, false);
        assert.equal(body.inert, true);
        assert.ok(body.classList.set.has('section-gated'));
        assert.equal(note.hidden, false);
    });

    test('on restores the section and hides the reason', () => {
        const body = { inert: true, classList: classList() };
        body.classList.set.add('section-gated');
        const note = { hidden: false };
        apply(body, note, true);
        assert.equal(body.inert, false);
        assert.ok(!body.classList.set.has('section-gated'));
        assert.equal(note.hidden, true);
    });

    test('a section without a note still works', () => {
        const body = { inert: false, classList: classList() };
        apply(body, null, false);
        assert.equal(body.inert, true);
    });
});
