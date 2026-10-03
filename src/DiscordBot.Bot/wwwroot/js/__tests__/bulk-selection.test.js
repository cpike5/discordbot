const test = require('node:test');
const assert = require('node:assert/strict');
const { createSelection } = require('../bulk-selection.js');

// A table and a card list render the same 25 members, so the page offers each id twice.
// The selection is keyed by id, so it never counts a member twice.
function members(count) {
    return Array.from({ length: count }, (_, i) => String(100000000000000000n + BigInt(i)));
}

test.describe('createSelection', () => {
    test('select all on 25 rows reads 25, even when every row has two checkboxes', () => {
        const ids = members(25);
        const selection = createSelection(ids.concat(ids));
        selection.selectAll();
        assert.equal(selection.size, 25);
        assert.equal(selection.total, 25);
        assert.equal(selection.state, 'all');
    });

    test('ticking the same row in both layouts counts once', () => {
        const ids = members(3);
        const selection = createSelection(ids);
        selection.set(ids[0], true);
        selection.set(ids[0], true);
        assert.equal(selection.size, 1);
        assert.equal(selection.state, 'some');
        selection.set(ids[0], false);
        assert.equal(selection.size, 0);
        assert.equal(selection.state, 'none');
    });

    test('keeps snowflakes as exact strings', () => {
        const id = '1234567890123456789';
        const selection = createSelection([id]);
        selection.set(id, true);
        assert.deepEqual(selection.toArray(), [id]);
    });

    test('drops a selected id the page no longer offers', () => {
        const ids = members(3);
        const selection = createSelection(ids);
        selection.selectAll();
        selection.setAvailable(ids.slice(0, 2));
        assert.equal(selection.size, 2);
        assert.equal(selection.has(ids[2]), false);
    });

    test('select all is none with no rows', () => {
        const selection = createSelection([]);
        selection.selectAll();
        assert.equal(selection.state, 'none');
    });

    test('clear empties the selection', () => {
        const selection = createSelection(members(4));
        selection.selectAll();
        selection.clear();
        assert.equal(selection.size, 0);
    });
});
