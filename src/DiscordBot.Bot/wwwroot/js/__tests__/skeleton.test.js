const test = require('node:test');
const assert = require('node:assert/strict');
const { FakeNode, installFakeDocument, removeFakeDocument } = require('./fake-dom.js');
const Skeleton = require('../skeleton.js');

/** Injectable timers so the delay can be stepped without waiting. */
function fakeTimers() {
    const pending = new Map();
    let next = 1;
    return {
        schedule(fn, ms) { const id = next++; pending.set(id, { fn, ms }); return id; },
        cancel(id) { pending.delete(id); },
        fireAll() { [...pending.entries()].forEach(([id, t]) => { pending.delete(id); t.fn(); }); },
        count() { return pending.size; },
        lastDelay() { return [...pending.values()].pop()?.ms; }
    };
}

test.describe('createDelayedLoader', () => {
    test('draws nothing when the work finishes before the delay', () => {
        const timers = fakeTimers();
        const calls = [];
        const loader = Skeleton.createDelayedLoader({
            delay: 300,
            schedule: timers.schedule, cancel: timers.cancel,
            show: () => calls.push('show'), hide: () => calls.push('hide')
        });

        loader.start();
        assert.equal(timers.lastDelay(), 300);
        loader.stop();
        timers.fireAll();

        assert.deepEqual(calls, []);
        assert.equal(loader.isShown(), false);
    });

    test('draws after the delay and removes on stop', () => {
        const timers = fakeTimers();
        const calls = [];
        const loader = Skeleton.createDelayedLoader({
            delay: 300,
            schedule: timers.schedule, cancel: timers.cancel,
            show: () => calls.push('show'), hide: () => calls.push('hide')
        });

        loader.start();
        timers.fireAll();
        assert.equal(loader.isShown(), true);
        loader.stop();

        assert.deepEqual(calls, ['show', 'hide']);
        assert.equal(loader.isShown(), false);
    });

    test('a delay of 0 draws at once, without a timer', () => {
        const timers = fakeTimers();
        const calls = [];
        const loader = Skeleton.createDelayedLoader({
            delay: 0,
            schedule: timers.schedule, cancel: timers.cancel,
            show: () => calls.push('show'), hide: () => calls.push('hide')
        });

        loader.start();
        assert.deepEqual(calls, ['show']);
        assert.equal(timers.count(), 0);
    });

    test('the default delay is 300ms', () => {
        const timers = fakeTimers();
        const loader = Skeleton.createDelayedLoader({
            schedule: timers.schedule, cancel: timers.cancel, show() {}, hide() {}
        });
        loader.start();
        assert.equal(timers.lastDelay(), 300);
        assert.equal(Skeleton.DEFAULT_DELAY_MS, 300);
    });

    test('starting twice schedules once, and it can be reused after stop', () => {
        const timers = fakeTimers();
        const calls = [];
        const loader = Skeleton.createDelayedLoader({
            delay: 300,
            schedule: timers.schedule, cancel: timers.cancel,
            show: () => calls.push('show'), hide: () => calls.push('hide')
        });

        loader.start();
        loader.start();
        assert.equal(timers.count(), 1);
        loader.stop();
        loader.start();
        timers.fireAll();
        assert.deepEqual(calls, ['show']);
    });
});

test.describe('shapes and show', () => {
    test.beforeEach(installFakeDocument);
    test.afterEach(removeFakeDocument);

    test('lines makes the requested number of bones, the last one shorter', () => {
        const node = Skeleton.lines(4);
        const bones = node.find((n) => /\bskeleton\b/.test(n.className));
        assert.equal(bones.length, 4);
        assert.match(bones[3].className, /w-2\/3/);
        assert.match(bones[0].className, /w-full/);
    });

    test('table makes rows x columns bones', () => {
        const node = Skeleton.table(3, 5);
        assert.equal(node.find((n) => /\bskeleton\b/.test(n.className)).length, 15);
    });

    test('a built skeleton is hidden from assistive tech', () => {
        for (const kind of ['lines', 'table', 'list', 'card']) {
            assert.equal(Skeleton.build({ kind }).getAttribute('aria-hidden'), 'true', kind);
        }
    });

    test('every card type builds', () => {
        for (const type of ['stats', 'server', 'activity', 'table', 'list', 'form']) {
            const node = Skeleton.card(type, { showHeader: true });
            assert.match(node.className, /\bcard\b/, type);
            assert.ok(node.find((n) => /\bskeleton\b/.test(n.className)).length > 0, type);
        }
    });

    test('show marks the container busy at once and draws only after the delay', async () => {
        const container = new FakeNode('div');
        const handle = Skeleton.show(container, { kind: 'lines', delay: 20 });

        assert.equal(container.getAttribute('aria-busy'), 'true');
        assert.equal(container.children.length, 0);

        await new Promise((resolve) => setTimeout(resolve, 60));
        assert.equal(handle.isShown(), true);
        assert.ok(container.children.length > 0);
        const status = container.find((n) => n.getAttribute('role') === 'status')[0];
        assert.equal(status.textContent, 'Loading');

        handle.hide();
        assert.equal(container.getAttribute('aria-busy'), null);
        assert.equal(container.children.length, 0);
    });

    test('hiding before the delay leaves the container untouched', async () => {
        const container = new FakeNode('div');
        const original = new FakeNode('p');
        container.appendChild(original);

        const handle = Skeleton.show(container, { kind: 'table', delay: 30 });
        handle.hide();
        await new Promise((resolve) => setTimeout(resolve, 70));

        assert.deepEqual(container.children, [original]);
        assert.equal(container.getAttribute('aria-busy'), null);
    });

    test('a custom label becomes the status text', () => {
        const container = new FakeNode('div');
        Skeleton.show(container, { delay: 0, label: 'Loading commands' });
        assert.equal(container.find((n) => n.getAttribute('role') === 'status')[0].textContent, 'Loading commands');
    });
});
