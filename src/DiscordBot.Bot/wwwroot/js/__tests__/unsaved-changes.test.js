const test = require('node:test');
const assert = require('node:assert/strict');
const UnsavedChanges = require('../unsaved-changes.js');

const { serialize, createTracker, createRegistry, handleBeforeUnload } = UnsavedChanges;

/** A control-like object, the shape serialize reads from form.elements. */
function field(name, value, extra = {}) {
    return { name, value, type: 'text', ...extra };
}

/** A fake form: a mutable list of controls plus a snapshot over it. */
function fakeForm(controls) {
    return { controls, snapshot: () => serialize(controls) };
}

test.describe('serialize', () => {
    test('reads text-like controls in order', () => {
        const a = serialize([field('Name', 'Ada'), field('Email', 'ada@example.com', { type: 'email' })]);
        const b = serialize([field('Name', 'Ada'), field('Email', 'ada@example.com', { type: 'email' })]);
        assert.equal(a, b);
        assert.notEqual(a, serialize([field('Name', 'Ada'), field('Email', 'other@example.com', { type: 'email' })]));
    });

    test('counts a checkbox only while it is checked', () => {
        const off = serialize([field('Flag', 'true', { type: 'checkbox', checked: false })]);
        const on = serialize([field('Flag', 'true', { type: 'checkbox', checked: true })]);
        assert.notEqual(off, on);
        assert.equal(off, serialize([]));
    });

    test('counts only the checked radio of a group', () => {
        const first = serialize([
            field('Mode', 'a', { type: 'radio', checked: true }),
            field('Mode', 'b', { type: 'radio', checked: false })
        ]);
        const second = serialize([
            field('Mode', 'a', { type: 'radio', checked: false }),
            field('Mode', 'b', { type: 'radio', checked: true })
        ]);
        assert.notEqual(first, second);
    });

    test('reads every selected option of a multiple select', () => {
        const select = (selected) => field('Roles', '', {
            type: 'select-multiple',
            options: [
                { value: 'admin', selected: selected.includes('admin') },
                { value: 'mod', selected: selected.includes('mod') },
                { value: 'viewer', selected: selected.includes('viewer') }
            ]
        });
        assert.notEqual(serialize([select(['admin'])]), serialize([select(['admin', 'mod'])]));
        assert.equal(serialize([select(['mod', 'admin'])]), serialize([select(['admin', 'mod'])]));
    });

    test('leaves out buttons, files, disabled controls, unnamed controls and the antiforgery token', () => {
        const base = serialize([field('Name', 'Ada')]);
        const noisy = serialize([
            field('Name', 'Ada'),
            field('Save', 'go', { type: 'submit' }),
            field('Reset', 'x', { type: 'reset' }),
            field('Upload', 'C:\\a.png', { type: 'file' }),
            field('Locked', 'x', { disabled: true }),
            field('', 'orphan'),
            field('__RequestVerificationToken', 'abc', { type: 'hidden' })
        ]);
        assert.equal(noisy, base);
    });

    test('leaves out controls marked data-unsaved-ignore', () => {
        const base = serialize([field('Name', 'Ada')]);
        const withSearch = serialize([
            field('Name', 'Ada'),
            field('Search', 'typed', { dataset: { unsavedIgnore: '' } }),
            field('Filter', 'typed', { closest: (selector) => selector === '[data-unsaved-ignore]' ? {} : null })
        ]);
        assert.equal(withSearch, base);
    });

    test('keeps hidden fields a script may change', () => {
        assert.notEqual(
            serialize([field('Tags', 'a,b', { type: 'hidden' })]),
            serialize([field('Tags', 'a,b,c', { type: 'hidden' })])
        );
    });
});

test.describe('createTracker', () => {
    test('starts clean', () => {
        const form = fakeForm([field('Name', 'Ada')]);
        const tracker = createTracker({ snapshot: form.snapshot });
        assert.equal(tracker.isDirty(), false);
        assert.equal(tracker.shouldWarn(), false);
    });

    test('becomes dirty when a value changes, and clean again when it is typed back', () => {
        const name = field('Name', 'Ada');
        const form = fakeForm([name]);
        const changes = [];
        const tracker = createTracker({ snapshot: form.snapshot, onChange: (dirty) => changes.push(dirty) });

        name.value = 'Grace';
        assert.equal(tracker.evaluate(), true);
        assert.equal(tracker.shouldWarn(), true);

        name.value = 'Ada';
        assert.equal(tracker.evaluate(), false);
        assert.equal(tracker.shouldWarn(), false);

        assert.deepEqual(changes, [true, false]);
    });

    test('reports a change once, not on every input event', () => {
        const name = field('Name', 'Ada');
        const form = fakeForm([name]);
        const changes = [];
        const tracker = createTracker({ snapshot: form.snapshot, onChange: (dirty) => changes.push(dirty) });

        name.value = 'G';
        tracker.evaluate();
        name.value = 'Gr';
        tracker.evaluate();
        name.value = 'Gra';
        tracker.evaluate();

        assert.deepEqual(changes, [true]);
    });

    test('markClean takes the current values as the new baseline', () => {
        const name = field('Name', 'Ada');
        const form = fakeForm([name]);
        const tracker = createTracker({ snapshot: form.snapshot });

        name.value = 'Grace';
        tracker.evaluate();
        assert.equal(tracker.isDirty(), true);

        tracker.markClean();
        assert.equal(tracker.isDirty(), false);

        name.value = 'Ada';
        assert.equal(tracker.evaluate(), true, 'the old value is now a change');
    });

    test('startDirty stays dirty until marked clean, whatever the fields say', () => {
        const form = fakeForm([field('Name', 'Ada')]);
        const tracker = createTracker({ snapshot: form.snapshot, startDirty: true });

        assert.equal(tracker.isDirty(), true);
        assert.equal(tracker.evaluate(), true);

        tracker.markClean();
        assert.equal(tracker.isDirty(), false);
    });

    test('does not warn while the form is submitting, and warns again if the submit is cancelled', () => {
        const name = field('Name', 'Ada');
        const form = fakeForm([name]);
        const tracker = createTracker({ snapshot: form.snapshot });

        name.value = 'Grace';
        tracker.evaluate();
        tracker.markSubmitting();
        assert.equal(tracker.isDirty(), true);
        assert.equal(tracker.shouldWarn(), false);

        tracker.cancelSubmitting();
        assert.equal(tracker.shouldWarn(), true);
    });

    test('a checkbox toggled on and back off is clean', () => {
        const flag = field('Flag', 'true', { type: 'checkbox', checked: false });
        const form = fakeForm([flag]);
        const tracker = createTracker({ snapshot: form.snapshot });

        flag.checked = true;
        assert.equal(tracker.evaluate(), true);
        flag.checked = false;
        assert.equal(tracker.evaluate(), false);
    });
});

test.describe('createRegistry and handleBeforeUnload', () => {
    function dirtyTracker() {
        const name = field('Name', 'Ada');
        const tracker = createTracker({ snapshot: () => serialize([name]) });
        name.value = 'Changed';
        tracker.evaluate();
        return tracker;
    }

    function cleanTracker() {
        const name = field('Name', 'Ada');
        return createTracker({ snapshot: () => serialize([name]) });
    }

    function fakeEvent() {
        return { prevented: false, returnValue: undefined, preventDefault() { this.prevented = true; } };
    }

    test('an empty registry never warns', () => {
        const registry = createRegistry();
        const event = fakeEvent();
        assert.equal(handleBeforeUnload(event, registry), false);
        assert.equal(event.prevented, false);
    });

    test('clean forms do not warn', () => {
        const registry = createRegistry();
        registry.add(cleanTracker());
        registry.add(cleanTracker());
        const event = fakeEvent();
        assert.equal(handleBeforeUnload(event, registry), false);
        assert.equal(event.prevented, false);
    });

    test('one dirty form among clean ones asks the browser to confirm', () => {
        const registry = createRegistry();
        registry.add(cleanTracker());
        registry.add(dirtyTracker());
        const event = fakeEvent();
        assert.equal(handleBeforeUnload(event, registry), true);
        assert.equal(event.prevented, true);
        assert.equal(event.returnValue, '');
    });

    test('a dirty form that is submitting does not block navigation', () => {
        const registry = createRegistry();
        const tracker = registry.add(dirtyTracker());
        tracker.markSubmitting();
        const event = fakeEvent();
        assert.equal(handleBeforeUnload(event, registry), false);
        assert.equal(event.prevented, false);
    });

    test('anyDirty ignores the submitting flag, shouldWarn does not', () => {
        const registry = createRegistry();
        const tracker = registry.add(dirtyTracker());
        tracker.markSubmitting();
        assert.equal(registry.anyDirty(), true);
        assert.equal(registry.shouldWarn(), false);
    });

    test('a removed tracker no longer counts', () => {
        const registry = createRegistry();
        const tracker = registry.add(dirtyTracker());
        assert.equal(registry.size(), 1);
        registry.remove(tracker);
        assert.equal(registry.size(), 0);
        assert.equal(registry.shouldWarn(), false);
    });
});
