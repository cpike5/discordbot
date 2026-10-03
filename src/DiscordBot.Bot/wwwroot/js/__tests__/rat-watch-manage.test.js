const test = require('node:test');
const assert = require('node:assert/strict');

const RatWatchManage = require('../rat-watch-manage.js');

test('cancel asks plainly and is a danger action', () => {
    const d = RatWatchManage.dialogFor({ action: 'cancel', accused: 'Ada' });
    assert.equal(d.variant, 'danger');
    assert.match(d.message, /Cancel the watch for Ada/);
    assert.equal(d.confirmText, 'Cancel watch');
    assert.equal(d.cancelText, 'Keep watch');
});

test('end vote repeats the tally in words', () => {
    const d = RatWatchManage.dialogFor({ action: 'end-vote', accused: 'Ada', guilty: '3', notGuilty: '1' });
    assert.match(d.message, /3 guilty and 1 not guilty/);
});

test('a name is only ever text in the message', () => {
    const d = RatWatchManage.dialogFor({ action: 'cancel', accused: '<img src=x onerror=alert(1)>' });
    assert.ok(d.message.includes('<img src=x onerror=alert(1)>'), 'passed through unchanged, escaped by the dialog');
});

test('an unknown action has no dialog', () => {
    assert.equal(RatWatchManage.dialogFor({ action: 'delete', accused: 'Ada' }), null);
});
