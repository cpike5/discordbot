const test = require('node:test');
const assert = require('node:assert/strict');
const { syncOverviewActions } = require('../moderation-settings.js');

/** A form whose only queryable part is the actions row, with a hidden class list. */
function fakeForm() {
    const classes = new Set(['hidden']);
    const actions = {
        classList: {
            toggle(name, force) { if (force) classes.add(name); else classes.delete(name); },
            contains: (name) => classes.has(name)
        }
    };
    return {
        actions,
        querySelector: (sel) => (sel === '[data-overview-actions]' ? actions : null)
    };
}

test('Save overview stays hidden until the mode changes', () => {
    const form = fakeForm();
    assert.equal(form.actions.classList.contains('hidden'), true);

    syncOverviewActions(form, true);
    assert.equal(form.actions.classList.contains('hidden'), false);

    // Changing back to the saved mode (or saving) hides it again
    syncOverviewActions(form, false);
    assert.equal(form.actions.classList.contains('hidden'), true);
});

test('a form without the actions row is left alone', () => {
    assert.doesNotThrow(() => syncOverviewActions({ querySelector: () => null }, true));
});
