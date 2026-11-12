const test = require('node:test');
const assert = require('node:assert/strict');
const { computeLayout, createMoreMenu } = require('../guild-nav.js');

const eleven = new Array(11).fill(100);
const indexes = (flags) => flags.map((v, i) => (v ? i : -1)).filter((i) => i >= 0);

test('everything stays in the strip when it all fits, and there is no More', () => {
    const r = computeLayout({ widths: eleven, available: 1100, moreWidth: 100, gap: 0, activeIndex: 2 });
    assert.equal(r.overflow, false);
    assert.equal(indexes(r.visible).length, 11);
});

test('tabs that do not fit move to More, in config order', () => {
    const r = computeLayout({ widths: eleven, available: 700, moreWidth: 100, gap: 0, activeIndex: 0 });
    assert.equal(r.overflow, true);
    assert.deepEqual(indexes(r.visible), [0, 1, 2, 3, 4, 5]);
    assert.equal(r.activeInOverflow, false);
});

test('the gap between items counts against the width', () => {
    // budget is 700 - 100 - 10 = 590; six tabs need 600 + 50 of gaps, five need 500 + 40
    const r = computeLayout({ widths: eleven, available: 700, moreWidth: 100, gap: 10, activeIndex: 0 });
    assert.deepEqual(indexes(r.visible), [0, 1, 2, 3, 4]);
});

test('an active tab beyond the cut stays visible and pushes the last fitting tab out', () => {
    const r = computeLayout({ widths: eleven, available: 700, moreWidth: 100, gap: 0, activeIndex: 9 });
    assert.deepEqual(indexes(r.visible), [0, 1, 2, 3, 4, 9]);
    assert.equal(r.activeInOverflow, false);
});

test('an active tab that cannot fit even alone goes into More and marks it active', () => {
    const r = computeLayout({ widths: [300, 300, 350], available: 400, moreWidth: 100, gap: 0, activeIndex: 2 });
    assert.equal(r.overflow, true);
    assert.equal(r.activeInOverflow, true);
    assert.equal(r.visible[2], false);
});

test('no active tab (unknown page) is fine', () => {
    const r = computeLayout({ widths: eleven, available: 700, moreWidth: 100, gap: 0, activeIndex: -1 });
    assert.deepEqual(indexes(r.visible), [0, 1, 2, 3, 4, 5]);
    assert.equal(r.activeInOverflow, false);
});

function menuHarness(itemCount = 3) {
    const classes = new Set();
    const attrs = {};
    const log = [];
    const items = Array.from({ length: itemCount }, (_, i) => ({ focus: () => log.push('item' + i) }));
    const button = { setAttribute: (k, v) => { attrs[k] = v; }, focus: () => log.push('button') };
    const menu = {
        classList: {
            add: (c) => classes.add(c),
            remove: (c) => classes.delete(c),
            contains: (c) => classes.has(c)
        }
    };
    const m = createMoreMenu({ button, menu, getItems: () => items, getActiveItem: () => items[1] });
    return { m, items, attrs, log, classes };
}

const key = (k, target) => ({ key: k, target, preventDefault() { this.prevented = true; } });

test('opening sets aria-expanded and focuses the current page item', () => {
    const h = menuHarness();
    h.m.toggle();
    assert.equal(h.attrs['aria-expanded'], 'true');
    assert.deepEqual(h.log, ['item1']);
    assert.equal(h.m.isOpen(), true);
});

test('ArrowDown on the button opens and focuses the first item; ArrowUp the last', () => {
    const h = menuHarness();
    const e = key('ArrowDown');
    h.m.onButtonKeydown(e);
    assert.equal(e.prevented, true);
    assert.deepEqual(h.log, ['item0']);
    h.m.close(false);
    h.m.onButtonKeydown(key('ArrowUp'));
    assert.deepEqual(h.log, ['item0', 'item2']);
});

test('arrow keys move through the items and wrap', () => {
    const h = menuHarness();
    h.m.open('first');
    h.m.onMenuKeydown(key('ArrowDown', h.items[0]));
    h.m.onMenuKeydown(key('ArrowDown', h.items[2]));
    h.m.onMenuKeydown(key('ArrowUp', h.items[0]));
    h.m.onMenuKeydown(key('End', h.items[2]));
    h.m.onMenuKeydown(key('Home', h.items[2]));
    assert.deepEqual(h.log, ['item0', 'item1', 'item0', 'item2', 'item2', 'item0']);
});

test('Escape closes the menu and returns focus to the button', () => {
    const h = menuHarness();
    h.m.open('first');
    h.m.onMenuKeydown(key('Escape', h.items[0]));
    assert.equal(h.m.isOpen(), false);
    assert.equal(h.attrs['aria-expanded'], 'false');
    assert.equal(h.log[h.log.length - 1], 'button');
});

test('Escape on the button also closes an open menu', () => {
    const h = menuHarness();
    h.m.open('first');
    h.m.onButtonKeydown(key('Escape'));
    assert.equal(h.m.isOpen(), false);
});

test('Tab closes the menu without stealing focus', () => {
    const h = menuHarness();
    h.m.open('first');
    h.m.onMenuKeydown(key('Tab', h.items[0]));
    assert.equal(h.m.isOpen(), false);
    assert.deepEqual(h.log, ['item0']);
});

test('closing an already closed menu does nothing', () => {
    const h = menuHarness();
    h.m.close(true);
    assert.deepEqual(h.log, []);
});
