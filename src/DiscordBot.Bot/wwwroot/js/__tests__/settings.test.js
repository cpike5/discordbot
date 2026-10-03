const test = require('node:test');
const assert = require('node:assert/strict');
const settings = require('../settings.js');

test('buildEntries: checkboxes always say true or false, never nothing', () => {
    const entries = settings.buildEntries([
        { name: 'FormSettings[A]', type: 'checkbox', checked: true, value: 'true' },
        { name: 'FormSettings[B]', type: 'checkbox', checked: false, value: 'true' }
    ]);
    assert.deepEqual(entries, [['FormSettings[A]', 'true'], ['FormSettings[B]', 'false']]);
});

test('buildEntries: radios count only when checked; text, number and select send their value', () => {
    const entries = settings.buildEntries([
        { name: 'R', type: 'radio', checked: false, value: 'one' },
        { name: 'R', type: 'radio', checked: true, value: 'two' },
        { name: 'N', type: 'number', value: '7' },
        { name: 'S', type: 'select-one', value: '' },
        { name: 'T', type: 'text', value: 'hello' }
    ]);
    assert.deepEqual(entries, [['R', 'two'], ['N', '7'], ['S', ''], ['T', 'hello']]);
});

test('buildEntries: leaves out disabled controls, buttons, unnamed controls and framework fields', () => {
    const entries = settings.buildEntries([
        { name: 'Core', type: 'checkbox', checked: true, disabled: true },
        { name: '', type: 'text', value: 'x' },
        { name: '__RequestVerificationToken', type: 'hidden', value: 'abc' },
        { name: 'save', type: 'submit', value: 'Save' },
        { type: 'button' },
        { name: 'Keep', type: 'text', value: 'yes' }
    ]);
    assert.deepEqual(entries, [['Keep', 'yes']]);
});

test('readSaveResult: a save that changed nothing is reported as unchanged, not saved', () => {
    const r = settings.readSaveResult({ ok: true, data: { success: true, message: 'Nothing changed.', changeCount: 0 } });
    assert.equal(r.kind, 'unchanged');
    assert.equal(r.message, 'Nothing changed.');
});

test('readSaveResult: changes, restart flag and missing changeCount', () => {
    const saved = settings.readSaveResult({ ok: true, data: { success: true, message: 'Saved 2 settings.', changeCount: 2, restartRequired: true } });
    assert.equal(saved.kind, 'saved');
    assert.equal(saved.restartRequired, true);

    // Appearance does not send a count: that is still a save
    assert.equal(settings.readSaveResult({ ok: true, data: { success: true, message: 'Theme saved.' } }).kind, 'saved');
});

test('readSaveResult: errors prefer the server list, then the message, then a plain fallback', () => {
    assert.equal(
        settings.readSaveResult({ ok: false, data: { success: false, message: 'Failed', errors: ['A is too long.', 'B is required.'] } }).message,
        'A is too long. B is required.');
    assert.equal(settings.readSaveResult({ ok: false, data: { success: false, message: 'Server error.' } }).message, 'Server error.');
    const fallback = settings.readSaveResult({ ok: false, data: null });
    assert.equal(fallback.kind, 'error');
    assert.match(fallback.message, /could not be saved/);
    // success:false on a 200 is still an error
    assert.equal(settings.readSaveResult({ ok: true, data: { success: false, message: 'No.' } }).kind, 'error');
});

test('summarizeSaveAll: nothing dirty, nothing changed, saved, and partial failure', () => {
    assert.deepEqual(settings.summarizeSaveAll([]), { kind: 'info', message: 'Nothing to save. No tab has unsaved changes.' });
    assert.equal(settings.summarizeSaveAll([{ category: 'General', kind: 'unchanged' }]).kind, 'info');

    const saved = settings.summarizeSaveAll([{ category: 'General', kind: 'saved' }, { category: 'AiModels', kind: 'saved' }]);
    assert.equal(saved.kind, 'success');
    assert.equal(saved.message, 'Saved General, AI Models tabs.');
    assert.equal(settings.summarizeSaveAll([{ category: 'Advanced', kind: 'saved' }]).message, 'Saved Advanced tab.');

    const partial = settings.summarizeSaveAll([{ category: 'General', kind: 'saved' }, { category: 'Commands', kind: 'error' }]);
    assert.equal(partial.kind, 'error');
    assert.equal(partial.message, 'Saved General. Could not save Commands. Open that tab to see why.');
});

test('categoryFromSearch: only a known tab counts', () => {
    const valid = ['General', 'Commands', 'BotControl'];
    assert.equal(settings.categoryFromSearch('?category=Commands', valid), 'Commands');
    assert.equal(settings.categoryFromSearch('?x=1&category=BotControl&y=2', valid), 'BotControl');
    assert.equal(settings.categoryFromSearch('?category=Nope', valid), null);
    assert.equal(settings.categoryFromSearch('', valid), null);
    assert.equal(settings.categoryFromSearch('?category=%E0%A4%A', valid), null);
});

test('formatUptime: days, hours, minutes and seconds', () => {
    assert.equal(settings.formatUptime('3.04:05:06.123'), '3d 4h 5m');
    assert.equal(settings.formatUptime('04:05:06'), '4h 5m 6s');
    assert.equal(settings.formatUptime('00:05:06'), '5m 6s');
    assert.equal(settings.formatUptime('00:00:06.5'), '6s');
    assert.equal(settings.formatUptime(''), '0s');
});
