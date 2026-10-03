const test = require('node:test');
const assert = require('node:assert/strict');
const ConnectionBanner = require('../connection-banner.js');

// Just enough DOM for init(): a banner with a pill, a sentence and a retry button, a live-region
// announcer, and stale badges.
function fakeDocument() {
    const pill = { attrs: { 'data-state': 'reconnecting' }, setAttribute(k, v) { this.attrs[k] = v; } };
    const pillText = { textContent: '' };
    const text = { textContent: '' };
    const retry = { hidden: false, disabled: false, textContent: 'Retry now', listeners: {}, addEventListener(t, f) { this.listeners[t] = f; } };
    const signIn = { hidden: true, attrs: {}, setAttribute(k, v) { this.attrs[k] = v; } };
    const banner = {
        hidden: true,
        querySelector(sel) {
            if (sel === '.connection-status') return pill;
            if (sel === '.connection-text') return pillText;
            if (sel === '[data-connection-banner-text]') return text;
            if (sel === '[data-connection-retry]') return retry;
            if (sel === '[data-connection-signin]') return signIn;
            return null;
        }
    };
    const announcer = { textContent: '' };
    const badges = [{ hidden: true }, { hidden: true }];
    const root = { attrs: {}, setAttribute(k, v) { this.attrs[k] = v; }, getAttribute(k) { return this.attrs[k]; } };
    return {
        pill, pillText, text, retry, signIn, banner, announcer, badges, root,
        querySelector: sel => (sel === '[data-connection-banner]' ? banner : null),
        querySelectorAll: sel => (sel === '[data-stale-badge]' ? badges : []),
        getElementById: id => (id === 'connection-announcer' ? announcer : null),
        documentElement: root
    };
}

function fakeHub(initial) {
    const hub = {
        state: initial,
        callbacks: [],
        retries: 0,
        getConnectionState() { return this.state; },
        onStateChange(cb) { this.callbacks.push(cb); },
        retryNow() { this.retries++; return Promise.resolve(false); },
        set(state, reason) {
            const previousState = this.state;
            this.state = state;
            this.callbacks.forEach(cb => cb({ state, previousState, reason: reason || null }));
        },
        getDisconnectReason() { return null; }
    };
    return hub;
}

test.afterEach(() => {
    test.mock.timers.reset();
});

test('describeDown words each state and has nothing for the healthy ones', () => {
    assert.equal(ConnectionBanner.describeDown('reconnecting').pillText, 'Reconnecting…');
    assert.equal(ConnectionBanner.describeDown('disconnected').pillText, 'Disconnected');
    assert.equal(ConnectionBanner.describeDown('connected'), null);
    assert.equal(ConnectionBanner.describeDown('connecting'), null);
});

test('stays hidden for a blip shorter than the grace period', () => {
    test.mock.timers.enable({ apis: ['setTimeout'] });
    const doc = fakeDocument();
    const hub = fakeHub('connected');
    ConnectionBanner.init(hub, doc);

    hub.set('reconnecting');
    test.mock.timers.tick(ConnectionBanner.SHOW_AFTER_MS - 100);
    assert.equal(doc.banner.hidden, true);

    hub.set('connected');
    test.mock.timers.tick(5000);
    assert.equal(doc.banner.hidden, true);
    assert.equal(doc.badges.every(b => b.hidden), true);
});

test('shows Reconnecting and the stale badges after the grace period, and announces once', () => {
    test.mock.timers.enable({ apis: ['setTimeout'] });
    const doc = fakeDocument();
    const hub = fakeHub('connected');
    ConnectionBanner.init(hub, doc);

    hub.set('reconnecting');
    test.mock.timers.tick(ConnectionBanner.SHOW_AFTER_MS);

    assert.equal(doc.banner.hidden, false);
    assert.equal(doc.pillText.textContent, 'Reconnecting…');
    assert.equal(doc.pill.attrs['data-state'], 'reconnecting');
    assert.equal(doc.retry.hidden, false);
    assert.equal(doc.badges.every(b => !b.hidden), true);
    assert.equal(doc.announcer.textContent, 'Live updates lost. Reconnecting.');
    assert.equal(doc.root.attrs['data-hub-state'], 'reconnecting');
});

test('recovery shows "restored" briefly, clears the badges, then hides', () => {
    test.mock.timers.enable({ apis: ['setTimeout'] });
    const doc = fakeDocument();
    const hub = fakeHub('connected');
    ConnectionBanner.init(hub, doc);

    hub.set('reconnecting');
    test.mock.timers.tick(ConnectionBanner.SHOW_AFTER_MS);
    hub.set('connected');

    assert.equal(doc.banner.hidden, false);
    assert.equal(doc.pillText.textContent, 'Connected');
    assert.equal(doc.text.textContent, 'Live updates restored.');
    assert.equal(doc.retry.hidden, true);
    assert.equal(doc.badges.every(b => b.hidden), true);
    assert.equal(doc.announcer.textContent, 'Live updates restored.');

    test.mock.timers.tick(ConnectionBanner.RESTORED_MS);
    assert.equal(doc.banner.hidden, true);
});

test('a second outage right after recovery cancels the pending hide', () => {
    test.mock.timers.enable({ apis: ['setTimeout'] });
    const doc = fakeDocument();
    const hub = fakeHub('connected');
    ConnectionBanner.init(hub, doc);

    hub.set('reconnecting');
    test.mock.timers.tick(ConnectionBanner.SHOW_AFTER_MS);
    hub.set('connected');
    test.mock.timers.tick(1000);
    hub.set('reconnecting');
    test.mock.timers.tick(ConnectionBanner.RESTORED_MS);

    assert.equal(doc.banner.hidden, false);
    assert.equal(doc.pillText.textContent, 'Reconnecting…');
});

test('the first connection of a page load never shows the banner', () => {
    test.mock.timers.enable({ apis: ['setTimeout'] });
    const doc = fakeDocument();
    const hub = fakeHub('disconnected');
    ConnectionBanner.init(hub, doc);

    hub.set('connecting');
    test.mock.timers.tick(ConnectionBanner.SHOW_AFTER_MS + 500);
    assert.equal(doc.banner.hidden, true);
    hub.set('connected');
    assert.equal(doc.banner.hidden, true);
});

test('an ended session shows at once, says to sign in, and offers Sign in instead of Retry', () => {
    test.mock.timers.enable({ apis: ['setTimeout'] });
    const doc = fakeDocument();
    const hub = fakeHub('connected');
    ConnectionBanner.init(hub, doc);

    hub.set('disconnected', 'auth');

    // No grace period: nothing will reconnect, so there is nothing to wait for.
    assert.equal(doc.banner.hidden, false);
    assert.equal(doc.pillText.textContent, 'Signed out');
    assert.match(doc.text.textContent, /Sign in again/);
    assert.equal(doc.retry.hidden, true);
    assert.equal(doc.signIn.hidden, false);
    assert.equal(doc.badges.every(b => !b.hidden), true);
    assert.equal(ConnectionBanner.describeDown('disconnected', 'auth').signIn, true);
    assert.equal(ConnectionBanner.describeDown('disconnected').signIn, undefined);
});

test('a plain outage does not offer Sign in', () => {
    test.mock.timers.enable({ apis: ['setTimeout'] });
    const doc = fakeDocument();
    const hub = fakeHub('connected');
    ConnectionBanner.init(hub, doc);

    hub.set('reconnecting');
    test.mock.timers.tick(ConnectionBanner.SHOW_AFTER_MS);
    assert.equal(doc.signIn.hidden, true);
    assert.equal(doc.retry.hidden, false);
});

test('the Retry now button asks the hub to retry', async () => {
    const doc = fakeDocument();
    const hub = fakeHub('connected');
    ConnectionBanner.init(hub, doc);

    doc.retry.listeners.click();
    assert.equal(hub.retries, 1);
    assert.equal(doc.retry.disabled, true);
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(doc.retry.disabled, false);
    assert.equal(doc.retry.textContent, 'Retry now');
});

test('init does nothing without a banner or a hub', () => {
    assert.equal(ConnectionBanner.init(null, fakeDocument()), null);
    const doc = fakeDocument();
    doc.querySelector = () => null;
    assert.equal(ConnectionBanner.init(fakeHub('connected'), doc), null);
});
