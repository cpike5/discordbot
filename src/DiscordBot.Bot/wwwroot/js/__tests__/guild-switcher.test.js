const test = require('node:test');
const assert = require('node:assert/strict');
const gs = require('../guild-switcher.js');

const A = { id: '123456789012345678', name: 'Pixel Pirates', iconUrl: null };
const B = { id: '223456789012345678', name: 'Study Hall', iconUrl: 'https://cdn.discordapp.com/icons/2/x.png' };
const C = { id: '323456789012345678', name: 'Dev Sandbox', iconUrl: null };
const D = { id: '423456789012345678', name: 'Fourth', iconUrl: null };

function memoryStorage(initial) {
    const data = Object.assign({}, initial);
    return {
        data,
        getItem: (k) => (k in data ? data[k] : null),
        setItem: (k, v) => { data[k] = String(v); }
    };
}

const throwing = {
    getItem() { throw new Error('SecurityError'); },
    setItem() { throw new Error('QuotaExceededError'); }
};

// ---- Recent list -------------------------------------------------------------------------------

test('the newest guild goes first', () => {
    let list = gs.addRecent([], A);
    list = gs.addRecent(list, B);
    assert.deepEqual(list.map((e) => e.id), [B.id, A.id]);
});

test('visiting a guild again moves it to the front without a duplicate', () => {
    let list = [A, B, C].reduce((l, g) => gs.addRecent(l, g), []);
    list = gs.addRecent(list, A);
    assert.deepEqual(list.map((e) => e.id), [A.id, C.id, B.id]);
});

test('the list keeps at most 3 guilds', () => {
    const list = [A, B, C, D].reduce((l, g) => gs.addRecent(l, g), []);
    assert.equal(gs.MAX_RECENT, 3);
    assert.deepEqual(list.map((e) => e.id), [D.id, C.id, B.id]);
});

test('IDs stay strings and numeric IDs are rejected rather than rounded', () => {
    const list = gs.addRecent([{ id: 123456789012345678, name: 'Rounded' }], A);
    assert.deepEqual(list, [{ id: A.id, name: A.name, iconUrl: null }]);
    assert.equal(typeof list[0].id, 'string');
});

test('icons that are not https are dropped', () => {
    const list = gs.addRecent([], { id: A.id, name: A.name, iconUrl: 'javascript:alert(1)' });
    assert.equal(list[0].iconUrl, null);
});

test('pruning drops guilds the user can no longer open', () => {
    const list = gs.pruneRecent([A, B, C], [C.id, A.id]);
    assert.deepEqual(list.map((e) => e.id), [A.id, C.id]);
});

test('read and write round-trip through storage under recentGuilds', () => {
    const storage = memoryStorage();
    assert.equal(gs.writeRecent(storage, [A, B]), true);
    assert.ok(storage.data.recentGuilds);
    assert.deepEqual(gs.readRecent(storage).map((e) => e.id), [A.id, B.id]);
});

test('storage that throws gives an empty list and a failed write, never an exception', () => {
    assert.deepEqual(gs.readRecent(throwing), []);
    assert.equal(gs.writeRecent(throwing, [A]), false);
    assert.deepEqual(gs.readRecent(null), []);
});

test('corrupt storage reads as empty', () => {
    assert.deepEqual(gs.readRecent(memoryStorage({ recentGuilds: '{not json' })), []);
    assert.deepEqual(gs.readRecent(memoryStorage({ recentGuilds: '{"id":"1"}' })), []);
});

test('the sidebar markup escapes names and marks the current guild', () => {
    const escape = (v) => String(v).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    const evil = { id: A.id, name: '<img src=x onerror=alert(1)>', iconUrl: null };
    const html = gs.recentListHtml([evil, B], B.id, escape);
    assert.ok(!html.includes('<img src=x'));
    assert.ok(html.includes('&lt;img src=x onerror=alert(1)&gt;'));
    assert.ok(html.includes('href="/Guilds/Details/' + A.id + '"'));
    assert.match(html, new RegExp('class="sidebar-recent-guild active" href="/Guilds/Details/' + B.id + '"[^>]*aria-current="true"'));
    assert.ok(html.includes('src="' + B.iconUrl + '"'));
});

// ---- Switcher ----------------------------------------------------------------------------------

test('filterIndexes matches anywhere in the name, ignoring case', () => {
    assert.deepEqual(gs.filterIndexes(['Pixel Pirates', 'Study Hall', 'Dev Sandbox'], 'SAND'), [2]);
    assert.deepEqual(gs.filterIndexes(['a', 'b'], ''), [0, 1]);
});

test('typeAhead finds the next name starting with the letter, wrapping', () => {
    const names = ['Alpha', 'Beta', 'Bravo', 'Charlie'];
    assert.equal(gs.typeAhead(names, 0, 'b'), 1);
    assert.equal(gs.typeAhead(names, 1, 'b'), 2);
    assert.equal(gs.typeAhead(names, 2, 'b'), 1);
    assert.equal(gs.typeAhead(names, 0, 'z'), -1);
});

function harness(opts) {
    const o = opts || {};
    const classes = new Set();
    const log = [];
    const buttonAttrs = {};
    const controlAttrs = {};
    const names = o.names || ['Dev Sandbox', 'Pixel Pirates', 'Study Hall'];
    const options = names.map((name, i) => ({
        id: 'opt' + i,
        name,
        isCurrent: i === (o.current === undefined ? 1 : o.current),
        active: false,
        hidden: false,
        setActive(on) { this.active = on; },
        setHidden(h) { this.hidden = h; }
    }));
    const went = [];
    const s = gs.createSwitcher({
        button: { setAttribute: (k, v) => { buttonAttrs[k] = v; }, focus: () => log.push('button') },
        popover: { classList: { add: (c) => classes.add(c), remove: (c) => classes.delete(c), contains: (c) => classes.has(c) } },
        control: {
            value: '',
            setAttribute: (k, v) => { controlAttrs[k] = v; },
            removeAttribute: (k) => { delete controlAttrs[k]; },
            focus: () => log.push('control')
        },
        hasFilter: !!o.hasFilter,
        options,
        go: (opt) => went.push(opt.name)
    });
    return { s, options, log, buttonAttrs, controlAttrs, went };
}

const key = (k) => ({ key: k, preventDefault() { this.prevented = true; } });

test('opening sets aria-expanded, focuses the control and highlights the current guild', () => {
    const h = harness();
    h.s.toggle();
    assert.equal(h.s.isOpen(), true);
    assert.equal(h.buttonAttrs['aria-expanded'], 'true');
    assert.deepEqual(h.log, ['control']);
    assert.equal(h.controlAttrs['aria-activedescendant'], 'opt1');
});

test('ArrowUp on the button opens at the last guild', () => {
    const h = harness();
    const e = key('ArrowUp');
    h.s.onButtonKeydown(e);
    assert.equal(e.prevented, true);
    assert.equal(h.controlAttrs['aria-activedescendant'], 'opt2');
});

test('arrow keys, Home and End move the highlight and wrap', () => {
    const h = harness();
    h.s.open();
    h.s.onControlKeydown(key('ArrowDown'));
    assert.equal(h.controlAttrs['aria-activedescendant'], 'opt2');
    h.s.onControlKeydown(key('ArrowDown'));
    assert.equal(h.controlAttrs['aria-activedescendant'], 'opt0');
    h.s.onControlKeydown(key('ArrowUp'));
    assert.equal(h.controlAttrs['aria-activedescendant'], 'opt2');
    h.s.onControlKeydown(key('Home'));
    assert.equal(h.controlAttrs['aria-activedescendant'], 'opt0');
    h.s.onControlKeydown(key('End'));
    assert.equal(h.controlAttrs['aria-activedescendant'], 'opt2');
    assert.equal(h.options.filter((x) => x.active).length, 1);
});

test('Enter goes to the highlighted guild', () => {
    const h = harness();
    h.s.open();
    h.s.onControlKeydown(key('ArrowDown'));
    h.s.onControlKeydown(key('Enter'));
    assert.deepEqual(h.went, ['Study Hall']);
});

test('Enter on the current guild just closes and returns focus', () => {
    const h = harness();
    h.s.open();
    h.s.onControlKeydown(key('Enter'));
    assert.deepEqual(h.went, []);
    assert.equal(h.s.isOpen(), false);
    assert.equal(h.log[h.log.length - 1], 'button');
});

test('Escape closes, clears the highlight and returns focus to the button', () => {
    const h = harness();
    h.s.open();
    h.s.onControlKeydown(key('Escape'));
    assert.equal(h.s.isOpen(), false);
    assert.equal(h.buttonAttrs['aria-expanded'], 'false');
    assert.equal(h.controlAttrs['aria-activedescendant'], undefined);
    assert.equal(h.log[h.log.length - 1], 'button');
});

test('Tab closes without moving focus back', () => {
    const h = harness();
    h.s.open();
    h.s.onControlKeydown(key('Tab'));
    assert.equal(h.s.isOpen(), false);
    assert.deepEqual(h.log, ['control']);
});

test('a letter jumps to the next guild starting with it when there is no filter', () => {
    const h = harness();
    h.s.open();
    h.s.onControlKeydown(key('s'));
    assert.equal(h.controlAttrs['aria-activedescendant'], 'opt2');
});

test('with a filter, typing narrows the list and highlights the first match', () => {
    const h = harness({ hasFilter: true });
    h.s.open();
    assert.equal(h.controlAttrs['aria-expanded'], 'true');
    h.s.onInput('hall');
    assert.deepEqual(h.options.map((x) => x.hidden), [true, true, false]);
    assert.equal(h.controlAttrs['aria-activedescendant'], 'opt2');
    h.s.onControlKeydown(key('ArrowDown')); // wraps within the one match
    assert.equal(h.controlAttrs['aria-activedescendant'], 'opt2');
    h.s.onInput('zzz');
    assert.equal(h.controlAttrs['aria-activedescendant'], undefined);
    h.s.onControlKeydown(key('Enter'));
    assert.deepEqual(h.went, []);
});
