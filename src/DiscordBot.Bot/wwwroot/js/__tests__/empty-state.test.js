const test = require('node:test');
const assert = require('node:assert/strict');
const { FakeNode, installFakeDocument, removeFakeDocument } = require('./fake-dom.js');
const EmptyState = require('../empty-state.js');

test.beforeEach(installFakeDocument);
test.afterEach(removeFakeDocument);

test.describe('resolve', () => {
    test('picks the icon for the type and falls back to noData', () => {
        assert.equal(EmptyState.resolve({ type: 'noResults' }).iconPath, EmptyState.ICONS.noResults);
        assert.equal(EmptyState.resolve({ type: 'bogus' }).type, 'noData');
        assert.equal(EmptyState.resolve({}).iconPath, EmptyState.ICONS.noData);
    });

    test('an explicit icon wins over the type icon', () => {
        assert.equal(EmptyState.resolve({ type: 'error', icon: 'M0 0' }).iconPath, 'M0 0');
    });

    test('noData draws a thinner stroke', () => {
        assert.equal(EmptyState.resolve({ type: 'noData' }).strokeWidth, '1.5');
        assert.equal(EmptyState.resolve({ type: 'error' }).strokeWidth, '2');
    });

    test('clamps the heading level to 1-6, default 3', () => {
        assert.equal(EmptyState.resolve({}).heading, 'h3');
        assert.equal(EmptyState.resolve({ headingLevel: 2 }).heading, 'h2');
        assert.equal(EmptyState.resolve({ headingLevel: 0 }).heading, 'h3');
        assert.equal(EmptyState.resolve({ headingLevel: 9 }).heading, 'h6');
    });

    test('the action icon defaults to a plus; an empty string removes it', () => {
        assert.equal(EmptyState.resolve({ action: { text: 'Add' } }).actionIconPath, 'M12 4v16m8-8H4');
        assert.equal(EmptyState.resolve({ action: { text: 'Retry', iconPath: '' } }).actionIconPath, '');
    });
});

test.describe('create', () => {
    test('renders icon, heading and description with the partial classes', () => {
        const node = EmptyState.create({ type: 'noResults', title: 'No matches', description: 'Try again.' });

        assert.match(node.className, /^empty-state flex flex-col items-center text-center /);
        assert.match(node.className, /max-w-\[400px\]/);
        assert.equal(node.findByTag('h3')[0].textContent, 'No matches');
        assert.equal(node.findByTag('p')[0].textContent, 'Try again.');
        const path = node.findByTag('path')[0];
        assert.equal(path.getAttribute('d'), EmptyState.ICONS.noResults);
        assert.equal(node.findByTag('svg')[0].getAttribute('aria-hidden'), 'true');
    });

    test('puts user text in through textContent, never as markup', () => {
        const hostile = '<img src=x onerror=alert(1)>';
        const node = EmptyState.create({ title: hostile, description: hostile, action: { text: hostile } });

        assert.equal(node.findByTag('img').length, 0);
        assert.equal(node.findByTag('h3')[0].textContent, hostile);
        assert.equal(node.findByTag('button')[0].textContent, hostile);
    });

    test('has no action area without an action', () => {
        const node = EmptyState.create({ title: 'Nothing here' });
        assert.equal(node.findByTag('button').length, 0);
        assert.equal(node.findByTag('a').length, 0);
    });

    test('an action with a url is a link, without one a button, and onClick is a listener', () => {
        let clicked = 0;
        const button = EmptyState.create({ action: { text: 'Retry', onClick: () => { clicked++; } } });
        const btn = button.findByTag('button')[0];
        assert.equal(btn.getAttribute('type'), 'button');
        assert.match(btn.className, /^btn btn-primary/);
        btn.click();
        assert.equal(clicked, 1);

        const link = EmptyState.create({ action: { text: 'Add user', url: '/Admin/Users/Create' } });
        const a = link.findByTag('a')[0];
        assert.equal(a.getAttribute('href'), '/Admin/Users/Create');
        assert.equal(link.findByTag('button').length, 0);
    });

    test('size changes the button size class', () => {
        const compact = EmptyState.create({ size: 'compact', action: { text: 'Go' } });
        assert.match(compact.findByTag('button')[0].className, /btn-sm/);
        const large = EmptyState.create({ size: 'large', action: { text: 'Go' } });
        assert.match(large.findByTag('button')[0].className, /btn-lg/);
    });

    test('an action with no icon draws no svg inside the button', () => {
        const node = EmptyState.create({ action: { text: 'Retry', iconPath: '' } });
        assert.equal(node.findByTag('button')[0].findByTag('svg').length, 0);
    });

    test('action attributes are applied but event-handler attributes are refused', () => {
        const node = EmptyState.create({
            action: { text: 'Go', attributes: { 'data-action': 'retry', onclick: 'alert(1)', 'bad name': 'x' } }
        });
        const btn = node.findByTag('button')[0];
        assert.equal(btn.getAttribute('data-action'), 'retry');
        assert.equal(btn.getAttribute('onclick'), null);
        assert.equal(btn.getAttribute('bad name'), null);
    });

    test('announce sets role=status and id is applied', () => {
        const node = EmptyState.create({ announce: true, id: 'no-results' });
        assert.equal(node.getAttribute('role'), 'status');
        assert.equal(node.id, 'no-results');
        assert.equal(EmptyState.create({}).getAttribute('role'), null);
    });

    test('a secondary link needs both text and url', () => {
        assert.equal(EmptyState.create({ secondary: { text: 'Learn more' } }).findByTag('a').length, 0);
        const node = EmptyState.create({ secondary: { text: 'Learn more', url: '/docs' } });
        assert.equal(node.findByTag('a')[0].getAttribute('href'), '/docs');
    });
});

test.describe('render, error and filtered', () => {
    test('render replaces the container content', () => {
        const container = new FakeNode('div');
        container.appendChild(new FakeNode('p'));
        const node = EmptyState.render(container, { title: 'Empty' });
        assert.equal(container.children.length, 1);
        assert.equal(container.children[0], node);
    });

    test('error is a status with a Retry button wired to onRetry, and plain default text', () => {
        const container = new FakeNode('div');
        let retried = 0;
        const node = EmptyState.error(container, { onRetry: () => { retried++; } });

        assert.equal(node.getAttribute('role'), 'status');
        assert.match(node.findByTag('h3')[0].textContent, /Could not load/);
        const button = node.findByTag('button')[0];
        assert.equal(button.textContent, 'Retry');
        assert.equal(button.findByTag('svg').length, 0);
        button.click();
        assert.equal(retried, 1);
    });

    test('error without onRetry has no button', () => {
        const node = EmptyState.error(new FakeNode('div'), {});
        assert.equal(node.findByTag('button').length, 0);
    });

    test('filtered names the noun and offers Clear filters', () => {
        const container = new FakeNode('div');
        let cleared = 0;
        const node = EmptyState.filtered(container, { noun: 'users', onClear: () => { cleared++; } });

        assert.equal(node.findByTag('h3')[0].textContent, 'No users match your filters');
        const button = node.findByTag('button')[0];
        assert.equal(button.textContent, 'Clear filters');
        button.click();
        assert.equal(cleared, 1);
    });
});

test.describe('url safety', () => {
    test('isSafeUrl allows relative and http(s) URLs only', () => {
        for (const ok of ['/Guilds/1', 'Guilds/1', '?page=2', '#top', '//example.com/x', 'http://example.com', 'HTTPS://example.com/a?b=c']) {
            assert.equal(EmptyState.isSafeUrl(ok), true, ok);
        }
        for (const bad of ['javascript:alert(1)', ' JavaScript:alert(1)', 'java\tscript:alert(1)', 'java\nscript:alert(1)',
            'data:text/html,<script>alert(1)</script>', 'vbscript:x', 'file:///etc/passwd', '', '   ', null, undefined, 42]) {
            assert.equal(EmptyState.isSafeUrl(bad), false, String(bad));
        }
    });

    test('an action with a javascript: url renders no href', () => {
        const node = EmptyState.create({ action: { text: 'Go', url: 'javascript:alert(1)' } });
        assert.equal(node.findByTag('a').length, 0);
        assert.equal(node.find((n) => n.getAttribute('href') !== null).length, 0);
    });

    test('a secondary link with a javascript: url is left out; a safe one keeps its href', () => {
        const bad = EmptyState.create({ action: { text: 'Go' }, secondary: { text: 'Docs', url: 'javascript:alert(1)' } });
        assert.equal(bad.findByTag('a').length, 0);
        const good = EmptyState.create({ action: { text: 'Go' }, secondary: { text: 'Docs', url: '/docs' } });
        assert.equal(good.findByTag('a')[0].getAttribute('href'), '/docs');
    });

    test('an unsafe href passed through action.attributes is dropped', () => {
        const node = EmptyState.create({ action: { text: 'Go', url: '/ok', attributes: { href: 'javascript:alert(1)', 'data-x': 'y' } } });
        const link = node.findByTag('a')[0];
        assert.equal(link.getAttribute('href'), '/ok');
        assert.equal(link.getAttribute('data-x'), 'y');
    });
});
