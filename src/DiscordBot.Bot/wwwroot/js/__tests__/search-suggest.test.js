const test = require('node:test');
const assert = require('node:assert/strict');
const { FakeNode, installFakeDocument, removeFakeDocument } = require('./fake-dom.js');

// safe-html.js assigns window.SafeHtml; search-suggest.js escapes through it
global.window = globalThis;
require('../safe-html.js');
const SearchSuggest = require('../search-suggest.js');

function link(label, href, extra = {}) {
    const node = new FakeNode('a');
    node.setAttribute('href', href);
    node.setAttribute('title', label);
    if (extra.target) node.setAttribute('target', extra.target);
    const text = new FakeNode('span');
    text.className = 'sidebar-link-text';
    text.textContent = label;
    node.appendChild(text);
    node.querySelector = (selector) => (selector === '.sidebar-link-text' ? text : null);
    return node;
}

const SIDEBAR = [
    link('Dashboard', '/'),
    link('Servers', '/Guilds'),
    link('Commands', '/Commands'),
    link('Bot Performance', '/Admin/Performance'),
    link('Settings', '/Admin/Settings'),
    link('Users', '/Admin/Users'),
    link('Grafana', 'https://grafana.example', { target: '_blank' })
];

function readyModel(overrides = {}) {
    return Object.assign({
        term: 'pix',
        pages: [],
        guilds: [{ id: '123456789012345678', name: 'Pixel Pirates', url: '/Guilds/Details/123456789012345678', iconUrl: null }],
        commands: [{ name: '/play', url: '/Commands?q=play' }],
        status: 'ready'
    }, overrides);
}

function deferred() {
    let resolve;
    let reject;
    const promise = new Promise((res, rej) => { resolve = res; reject = rej; });
    return { promise, resolve, reject };
}

// ---- Pages index ----------------------------------------------------------------------------

test('the pages index reads label, href and target from the sidebar links', () => {
    const pages = SearchSuggest.pagesFromLinks(SIDEBAR);

    assert.equal(pages.length, 7);
    assert.deepEqual(pages[3], { label: 'Bot Performance', href: '/Admin/Performance', external: false });
    assert.equal(pages[6].external, true);
});

test('the pages index skips duplicate hrefs and in-page anchors', () => {
    const pages = SearchSuggest.pagesFromLinks([link('Dashboard', '/'), link('Home', '/'), link('Top', '#main')]);

    assert.deepEqual(pages.map((p) => p.label), ['Dashboard']);
});

test('"perf" matches Bot Performance by the start of a word', () => {
    const pages = SearchSuggest.pagesFromLinks(SIDEBAR);

    assert.deepEqual(SearchSuggest.matchPages(pages, 'perf').map((p) => p.label), ['Bot Performance']);
    assert.deepEqual(SearchSuggest.matchPages(pages, 'SETT').map((p) => p.label), ['Settings']);
});

test('word-prefix matches come before substring matches', () => {
    const pages = SearchSuggest.pagesFromLinks([link('Members', '/a'), link('Commands', '/b'), link('Commerce', '/c')]);

    // "m" starts Members only; "mm" is inside Commands and Commerce
    assert.deepEqual(SearchSuggest.matchPages(pages, 'mem').map((p) => p.label), ['Members']);
    assert.deepEqual(SearchSuggest.matchPages(pages, 'mm').map((p) => p.label), ['Commands', 'Commerce']);
    const mixed = SearchSuggest.pagesFromLinks([link('Ascomm', '/x'), link('Comms', '/y')]);
    assert.deepEqual(SearchSuggest.matchPages(mixed, 'comm').map((p) => p.label), ['Comms', 'Ascomm']);
});

test('page matches are capped', () => {
    const many = Array.from({ length: 9 }, (_, i) => link('Log ' + i, '/log/' + i));

    assert.equal(SearchSuggest.matchPages(SearchSuggest.pagesFromLinks(many), 'log').length, 5);
});

// ---- Rows and markup ------------------------------------------------------------------------

test('rows run pages, servers, commands, then See all', () => {
    const rows = SearchSuggest.buildRows(readyModel({ pages: [{ label: 'Settings', href: '/Admin/Settings' }] }));

    assert.deepEqual(rows.map((r) => r.group), ['pages', 'guilds', 'commands', 'all']);
    assert.equal(rows[1].guildId, '123456789012345678', 'the snowflake stays a string');
    assert.equal(rows[3].href, '/Search?q=pix');
    assert.equal(rows[3].label, "See all results for 'pix'");
});

test('server text with an <img onerror> payload is escaped in the markup', () => {
    const model = readyModel({
        term: '<img src=x onerror=alert(1)>',
        guilds: [{ id: '1', name: '<img src=x onerror=alert(2)>', url: '/Guilds/Details/1" onmouseover="alert(3)', iconUrl: 'javascript:alert(4)' }],
        commands: [{ name: '<script>alert(5)</script>', url: '/Commands?q=x' }]
    });
    const html = SearchSuggest.renderHtml(model, SearchSuggest.buildRows(model), { idPrefix: 'nav', listboxId: 'nav-list', activeIndex: -1 });

    assert.ok(!html.includes('<img src=x'), 'no raw img tag from server text');
    assert.ok(!html.includes('<script>'), 'no raw script tag');
    assert.ok(html.includes('&lt;img src=x onerror=alert(2)&gt;'));
    assert.ok(!html.includes('" onmouseover="'), 'an href cannot break out of its attribute');
    assert.ok(!html.includes('javascript:'), 'a non-https icon URL is not used');
});

test('the markup is a listbox of options with group headings and the active option marked', () => {
    const model = readyModel({ pages: [{ label: 'Settings', href: '/Admin/Settings' }] });
    const html = SearchSuggest.renderHtml(model, SearchSuggest.buildRows(model), { idPrefix: 'nav', listboxId: 'nav-list', activeIndex: 1 });

    assert.match(html, /id="nav-list" role="listbox"/);
    assert.equal((html.match(/role="option"/g) || []).length, 4);
    assert.match(html, />Pages</);
    assert.match(html, />Servers</);
    assert.match(html, />Commands</);
    assert.match(html, /id="nav-option-1" role="option" tabindex="-1" aria-selected="true"/);
    assert.equal((html.match(/aria-selected="true"/g) || []).length, 1);
});

test('loading shows a progress line; no matches and failures say so', () => {
    const loading = readyModel({ status: 'loading' });
    assert.match(SearchSuggest.renderHtml(loading, SearchSuggest.buildRows(loading), { idPrefix: 'n', listboxId: 'l' }), /search-suggest-progress/);

    const empty = readyModel({ term: 'xyz', guilds: [], commands: [] });
    assert.equal(SearchSuggest.messageFor(empty), "No servers or commands match 'xyz'");

    const failed = readyModel({ status: 'error', guilds: [], commands: [], pages: [{ label: 'Settings', href: '/Admin/Settings' }] });
    assert.equal(SearchSuggest.messageFor(failed), "Couldn't search servers. Press Enter for full search.");
    const failedHtml = SearchSuggest.renderHtml(failed, SearchSuggest.buildRows(failed), { idPrefix: 'n', listboxId: 'l' });
    assert.match(failedHtml, /Settings/, 'pages are still shown when the request fails');
});

// ---- Keyboard -------------------------------------------------------------------------------

test('arrow keys move through the rows and wrap', () => {
    assert.equal(SearchSuggest.moveActive(-1, 1, 4), 0);
    assert.equal(SearchSuggest.moveActive(-1, -1, 4), 3);
    assert.equal(SearchSuggest.moveActive(3, 1, 4), 0);
    assert.equal(SearchSuggest.moveActive(0, -1, 4), 3);
    assert.equal(SearchSuggest.moveActive(0, 1, 0), -1);
});

test('Enter opens the highlighted row, the only server, or falls through to the full search', () => {
    const model = readyModel({ pages: [{ label: 'Settings', href: '/Admin/Settings' }] });
    const rows = SearchSuggest.buildRows(model);

    assert.equal(SearchSuggest.resolveEnter(model, rows, 0).row.href, '/Admin/Settings');
    assert.equal(SearchSuggest.resolveEnter(model, rows, 2).row.href, '/Commands?q=play');
    assert.equal(SearchSuggest.resolveEnter(model, rows, 3).row.href, '/Search?q=pix');

    // Nothing highlighted and exactly one server: open it
    assert.equal(SearchSuggest.resolveEnter(model, rows, -1).row.href, '/Guilds/Details/123456789012345678');

    // Two servers, or results still loading: the form submits as before
    const two = readyModel({ guilds: [model.guilds[0], { id: '2', name: 'Pixel Two', url: '/Guilds/Details/2' }] });
    assert.deepEqual(SearchSuggest.resolveEnter(two, SearchSuggest.buildRows(two), -1), { type: 'submit' });
    const loading = readyModel({ status: 'loading' });
    assert.deepEqual(SearchSuggest.resolveEnter(loading, SearchSuggest.buildRows(loading), -1), { type: 'submit' });
});

// ---- Requests -------------------------------------------------------------------------------

test('a slow first response that arrives after a second one is ignored', async () => {
    const first = deferred();
    const second = deferred();
    const calls = [];
    const results = [];
    const requester = SearchSuggest.createRequester({
        fetch(term, signal) {
            calls.push({ term, signal });
            return term === 'pi' ? first.promise : second.promise;
        },
        onResult: (term, data) => results.push([term, data.guilds.length]),
        onError: () => results.push(['error'])
    });

    const a = requester.request('pi');
    const b = requester.request('pix');
    assert.equal(calls[0].signal.aborted, true, 'the newer keystroke aborts the older request');
    assert.equal(calls[1].signal.aborted, false);

    second.resolve({ guilds: [{}], commands: [] });
    await b;
    first.resolve({ guilds: [{}, {}, {}], commands: [] }); // the transport ignored the abort
    await a;

    assert.deepEqual(results, [['pix', 1]]);
});

test('an aborted request is not reported as a failure; a real one is', async () => {
    const errors = [];
    const requester = SearchSuggest.createRequester({
        fetch(term, signal) {
            if (term === 'boom') return Promise.reject(new Error('500'));
            return new Promise((_, reject) => signal.addEventListener('abort', () => {
                const error = new Error('aborted');
                error.name = 'AbortError';
                reject(error);
            }));
        },
        onResult() {},
        onError: (term) => errors.push(term)
    });

    const pending = requester.request('slow');
    requester.cancel();
    await pending;
    await requester.request('boom');

    assert.deepEqual(errors, ['boom']);
});

// ---- Wiring ---------------------------------------------------------------------------------

function setupAttach(fetchImpl) {
    installFakeDocument();
    global.document.body = new FakeNode('body');
    global.document.getElementById = () => null;
    global.location = { href: '/current' };

    const container = new FakeNode('div');
    const input = new FakeNode('input');
    input.value = '';
    const panel = new FakeNode('div');
    panel.innerHTML = '';
    container.appendChild(input);
    container.appendChild(panel);

    const shown = [];
    const opened = [];
    const submitted = [];
    const suggest = SearchSuggest.attach({
        input,
        panel,
        listboxId: 'nav-list',
        debounceMs: 0,
        show: () => shown.push('show'),
        hide: () => shown.push('hide'),
        onOpen: (term) => opened.push(term),
        onSubmit: (term) => submitted.push(term),
        pages: () => SearchSuggest.pagesFromLinks(SIDEBAR),
        fetch: fetchImpl
    });
    return { input, panel, suggest, shown, opened, submitted };
}

function key(name) {
    return { key: name, defaultPrevented: false, preventDefault() { this.defaultPrevented = true; }, stopPropagation() {} };
}

const tick = () => new Promise((resolve) => setTimeout(resolve, 5));

test('attach makes the input a combobox and follows the highlight with aria-activedescendant', async () => {
    const { input, panel, suggest } = setupAttach(() => Promise.resolve({
        guilds: [{ id: '123456789012345678', name: 'Pixel Pirates', url: '/Guilds/Details/123456789012345678' }],
        commands: []
    }));
    try {
        assert.equal(input.getAttribute('role'), 'combobox');
        assert.equal(input.getAttribute('aria-controls'), 'nav-list');
        assert.equal(input.getAttribute('aria-expanded'), 'false');

        assert.equal(suggest.update('p'), false, 'one character suggests nothing');
        assert.equal(input.getAttribute('aria-expanded'), 'false');

        suggest.update('sett');
        assert.equal(input.getAttribute('aria-expanded'), 'true');
        assert.match(panel.innerHTML, /Settings/, 'pages show before any response');

        suggest.update('pix');
        await tick();
        assert.match(panel.innerHTML, /Pixel Pirates/);

        input.listeners.keydown[0](key('ArrowDown'));
        assert.equal(input.getAttribute('aria-activedescendant'), 'nav-list-option-0');
        input.listeners.keydown[0](key('ArrowUp'));
        assert.equal(input.getAttribute('aria-activedescendant'), 'nav-list-option-1', 'up from the first row wraps to See all');

        input.listeners.keydown[0](key('Escape'));
        assert.equal(input.getAttribute('aria-expanded'), 'false');
        assert.equal(input.hasAttribute('aria-activedescendant'), false);
    } finally {
        removeFakeDocument();
        delete global.location;
    }
});

test('Enter on a highlighted row opens its href and saves the recent search', async () => {
    const { input, suggest, opened } = setupAttach(() => Promise.resolve({
        guilds: [],
        commands: [{ name: '/play', url: '/Commands?q=play' }]
    }));
    try {
        suggest.update('pla');
        await tick();

        input.listeners.keydown[0](key('ArrowDown'));
        const enter = key('Enter');
        input.listeners.keydown[0](enter);

        assert.equal(enter.defaultPrevented, true);
        assert.equal(global.location.href, '/Commands?q=play');
        assert.deepEqual(opened, ['pla']);
    } finally {
        removeFakeDocument();
        delete global.location;
    }
});

test('Enter with nothing highlighted and one server opens it; with none it submits', async () => {
    let answer = { guilds: [{ id: '123456789012345678', name: 'Pixel Pirates', url: '/Guilds/Details/123456789012345678' }], commands: [] };
    const { input, suggest, submitted } = setupAttach(() => Promise.resolve(answer));
    try {
        suggest.update('pix');
        await tick();
        input.listeners.keydown[0](key('Enter'));
        assert.equal(global.location.href, '/Guilds/Details/123456789012345678');

        answer = { guilds: [], commands: [] };
        global.location.href = '/current';
        suggest.update('nothing');
        await tick();
        input.listeners.keydown[0](key('Enter'));
        assert.equal(global.location.href, '/current');
        assert.deepEqual(submitted, ['nothing']);
    } finally {
        removeFakeDocument();
        delete global.location;
    }
});

test('a failed request keeps the pages and offers the full search', async () => {
    const { panel, suggest } = setupAttach(() => Promise.reject(new Error('down')));
    try {
        suggest.update('sett');
        await tick();

        assert.match(panel.innerHTML, /Settings/);
        assert.match(panel.innerHTML, /Couldn&#39;t search servers\. Press Enter for full search\./);
        assert.match(panel.innerHTML, /See all results for &#39;sett&#39;/);
    } finally {
        removeFakeDocument();
        delete global.location;
    }
});
