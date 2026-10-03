const test = require('node:test');
const assert = require('node:assert/strict');

const HUB_PATH = require.resolve('../dashboard-hub.js');

// A stand-in for signalR.HubConnection: start() fails while `failStarts` > 0.
function installFakeSignalR(state) {
    class FakeConnection {
        constructor() {
            this.state = 'Disconnected';
            this.connectionId = 'conn-1';
            this.handlers = {};
            this.starts = 0;
        }
        onreconnecting(cb) { this.reconnecting = cb; }
        onreconnected(cb) { this.reconnected = cb; }
        onclose(cb) { this.closed = cb; }
        on(name, handler) { (this.handlers[name] = this.handlers[name] || []).push(handler); }
        off() {}
        async start() {
            this.starts++;
            if (state.failStarts > 0) {
                state.failStarts--;
                throw new Error('server down');
            }
            this.state = 'Connected';
        }
        async stop() {
            this.state = 'Disconnected';
            if (this.closed) this.closed();
        }
    }
    global.signalR = {
        HubConnectionBuilder: class {
            withUrl() { return this; }
            withAutomaticReconnect(options) { state.policy = options.nextRetryDelayInMilliseconds; return this; }
            configureLogging() { return this; }
            build() { state.connection = new FakeConnection(); return state.connection; }
        },
        LogLevel: { Information: 1 },
        HubConnectionState: { Disconnected: 'Disconnected', Reconnecting: 'Reconnecting', Connected: 'Connected' }
    };
}

function freshHub() {
    delete require.cache[HUB_PATH];
    return require(HUB_PATH);
}

// Let promise continuations run after a mocked timer fires.
const flush = () => new Promise(resolve => setImmediate(resolve));

test.beforeEach(() => {
    // Keep the module's own console chatter out of the test output.
    test.mock.method(console, 'log', () => {});
    test.mock.method(console, 'warn', () => {});
    test.mock.method(console, 'error', () => {});
});

test.afterEach(() => {
    test.mock.timers.reset();
    test.mock.restoreAll();
    delete global.signalR;
});

test('the retry policy is fast at first, then steady, and never stops', () => {
    const hub = freshHub();
    assert.deepEqual([0, 1, 2, 3].map(n => hub.nextRetryDelay(n)), [0, 2000, 5000, 10000]);

    for (const attempts of [4, 5, 10, 100, 10000]) {
        const low = hub.nextRetryDelay(attempts, () => 0);
        const high = hub.nextRetryDelay(attempts, () => 1);
        assert.equal(low, 25000);
        assert.equal(high, 35000);
    }
    for (let n = 0; n < 50; n++) {
        assert.notEqual(hub.nextRetryDelay(n), null);
    }
});

test('the automatic reconnect policy handed to SignalR matches nextRetryDelay', () => {
    const state = { failStarts: 0 };
    installFakeSignalR(state);
    const hub = freshHub();
    return hub.connect().then(() => {
        assert.equal(state.policy({ previousRetryCount: 1 }), 2000);
        assert.equal(typeof state.policy({ previousRetryCount: 500 }), 'number');
    });
});

test('connects on the first try and reports the states in order', async () => {
    const state = { failStarts: 0 };
    installFakeSignalR(state);
    const hub = freshHub();
    const seen = [];
    hub.onStateChange(({ state: s }) => seen.push(s));

    assert.equal(await hub.connect(), true);
    assert.deepEqual(seen, ['connecting', 'connected']);
    assert.equal(hub.isConnected(), true);
});

test('a first attempt that fails keeps retrying, then raises connected only', async () => {
    const state = { failStarts: 3 };
    installFakeSignalR(state);
    test.mock.timers.enable({ apis: ['setTimeout'] });
    const hub = freshHub();

    const seen = [];
    const events = [];
    hub.onStateChange(({ state: s }) => seen.push(s));
    for (const name of ['connected', 'reconnected', 'connectionFailed']) {
        hub.on(name, () => events.push(name));
    }

    assert.equal(await hub.connect(), false);
    assert.equal(hub.getConnectionState(), 'reconnecting');
    assert.deepEqual(events, ['connectionFailed']);

    // Attempt 2 (delay 0), attempt 3 (2s) fail; attempt 4 (5s) succeeds.
    test.mock.timers.tick(0);
    await flush();
    test.mock.timers.tick(2000);
    await flush();
    assert.equal(hub.isConnected(), false);
    test.mock.timers.tick(5000);
    await flush();

    assert.equal(state.connection.starts, 4);
    assert.equal(hub.isConnected(), true);
    assert.equal(hub.getConnectionState(), 'connected');
    // connectionFailed is raised once per outage, not on every retry.
    assert.deepEqual(events, ['connectionFailed', 'connected']);
    assert.deepEqual(seen, ['connecting', 'reconnecting', 'connected']);
});

test('keeps retrying past the old limit of five attempts', async () => {
    const state = { failStarts: 1000 };
    installFakeSignalR(state);
    test.mock.timers.enable({ apis: ['setTimeout'] });
    const hub = freshHub();

    await hub.connect();
    for (let i = 0; i < 12; i++) {
        test.mock.timers.tick(35000);
        await flush();
    }
    assert.ok(state.connection.starts >= 10, `only ${state.connection.starts} attempts`);
    assert.equal(hub.getConnectionState(), 'reconnecting');
});

test('handlers added before the connection exists are registered once', async () => {
    const state = { failStarts: 0 };
    installFakeSignalR(state);
    const hub = freshHub();
    const handler = () => {};

    hub.on('SomethingHappened', handler);
    await hub.connect();
    assert.equal(state.connection.handlers.SomethingHappened.length, 1);

    // Added while connected: attached directly, not twice.
    hub.on('SomethingElse', handler);
    assert.equal(state.connection.handlers.SomethingElse.length, 1);
});

test('retryNow tries again immediately while waiting out a delay', async () => {
    const state = { failStarts: 1 };
    installFakeSignalR(state);
    test.mock.timers.enable({ apis: ['setTimeout'] });
    const hub = freshHub();

    await hub.connect(); // fails; a retry is scheduled
    assert.equal(hub.isConnected(), false);

    assert.equal(await hub.retryNow(), true);
    assert.equal(hub.getConnectionState(), 'connected');
});

test('disconnect() stops retrying and reports disconnected', async () => {
    const state = { failStarts: 1000 };
    installFakeSignalR(state);
    test.mock.timers.enable({ apis: ['setTimeout'] });
    const hub = freshHub();

    await hub.connect();
    await hub.disconnect();
    const startsAfterStop = state.connection.starts;

    test.mock.timers.tick(60000);
    await flush();
    assert.equal(state.connection.starts, startsAfterStop);
    assert.equal(hub.getConnectionState(), 'disconnected');
});

test('the automatic reconnect path reports reconnecting then connected', async () => {
    const state = { failStarts: 0 };
    installFakeSignalR(state);
    const hub = freshHub();
    const events = [];
    hub.on('reconnecting', () => events.push('reconnecting'));
    hub.on('reconnected', () => events.push('reconnected'));
    await hub.connect();

    state.connection.state = 'Reconnecting';
    state.connection.reconnecting(new Error('lost'));
    assert.equal(hub.getConnectionState(), 'reconnecting');
    assert.equal(hub.isConnected(), false);

    state.connection.state = 'Connected';
    state.connection.reconnected('conn-2');
    assert.equal(hub.getConnectionState(), 'connected');
    assert.deepEqual(events, ['reconnecting', 'reconnected']);
});

// ---- Auth failures and a missing client library --------------------------------------------

function unauthorized() {
    // What SignalR's negotiate step throws for a 401: the status text, no status code.
    return new Error('Failed to complete negotiation with the server: Error: Unauthorized');
}

// Makes the first start() of every connection built from now on throw `error` once.
function failFirstStartWith(error) {
    const origBuild = global.signalR.HubConnectionBuilder.prototype.build;
    let failNext = true;
    global.signalR.HubConnectionBuilder.prototype.build = function () {
        const conn = origBuild.call(this);
        const realStart = conn.start.bind(conn);
        conn.start = async function () {
            if (failNext) {
                failNext = false;
                this.starts++;
                throw error;
            }
            return realStart();
        };
        return conn;
    };
}

test('isAuthError recognises 401 and 403 however SignalR reports them', () => {
    const hub = freshHub();
    assert.equal(hub.isAuthError(Object.assign(new Error('x'), { statusCode: 401 })), true);
    assert.equal(hub.isAuthError(Object.assign(new Error('x'), { statusCode: 403 })), true);
    assert.equal(hub.isAuthError(unauthorized()), true);
    assert.equal(hub.isAuthError(new Error('Failed to complete negotiation with the server: Error: Forbidden')), true);
    assert.equal(hub.isAuthError(new Error('Failed to complete negotiation with the server: TypeError: Failed to fetch')), false);
    assert.equal(hub.isAuthError(new Error("Status code '503'")), false);
    assert.equal(hub.isAuthError(null), false);
});

test('a 401 on the first attempt stops retrying and reports disconnected with reason auth', async () => {
    const state = { failStarts: 0 };
    installFakeSignalR(state);
    test.mock.timers.enable({ apis: ['setTimeout'] });
    failFirstStartWith(unauthorized());
    const hub = freshHub();
    const changes = [];
    hub.onStateChange(c => changes.push([c.state, c.reason]));

    assert.equal(await hub.connect(), false);
    assert.equal(hub.getConnectionState(), 'disconnected');
    assert.equal(hub.getDisconnectReason(), 'auth');
    assert.deepEqual(changes, [['connecting', null], ['disconnected', 'auth']]);

    // No retry is scheduled: time passing does not start another attempt.
    test.mock.timers.tick(120000);
    await flush();
    assert.equal(state.connection.starts, 1);
});

test('retryNow after an auth failure tries once more and recovers', async () => {
    const state = { failStarts: 0 };
    installFakeSignalR(state);
    failFirstStartWith(unauthorized());
    const hub = freshHub();

    await hub.connect();
    assert.equal(hub.getDisconnectReason(), 'auth');

    assert.equal(await hub.retryNow(), true);
    assert.equal(hub.getConnectionState(), 'connected');
    assert.equal(hub.getDisconnectReason(), null);
});

test('the automatic reconnect policy gives up on a 401 and the close reports auth', async () => {
    const state = { failStarts: 0 };
    installFakeSignalR(state);
    const hub = freshHub();
    await hub.connect();

    assert.equal(state.policy({ previousRetryCount: 0, retryReason: new Error('connection reset') }), 0);
    assert.equal(state.policy({ previousRetryCount: 1, retryReason: unauthorized() }), null);

    state.connection.closed(); // SignalR closes after the policy returned null
    assert.equal(hub.getConnectionState(), 'disconnected');
    assert.equal(hub.getDisconnectReason(), 'auth');
});

test('connect() resolves false, without throwing, when the SignalR library did not load', async () => {
    delete global.signalR;
    const hub = freshHub();
    assert.equal(await hub.connect(), false);
    assert.equal(hub.getConnectionState(), 'disconnected');
});

test('after a connection that was up has dropped and closed, recovery raises connected and reconnected', async () => {
    const state = { failStarts: 0 };
    installFakeSignalR(state);
    test.mock.timers.enable({ apis: ['setTimeout'] });
    const hub = freshHub();
    const events = [];
    for (const name of ['connected', 'reconnected']) hub.on(name, () => events.push(name));

    await hub.connect();
    assert.deepEqual(events, ['connected']);

    state.connection.state = 'Disconnected';
    state.connection.closed(new Error('server went away'));
    test.mock.timers.tick(0);
    await flush();
    assert.deepEqual(events, ['connected', 'connected', 'reconnected']);
});

test('coming back online or visible does not retry after an auth failure; retryNow does', async () => {
    const state = { failStarts: 0 };
    installFakeSignalR(state);
    const listeners = { window: {}, document: {} };
    global.window = { addEventListener: (t, f) => { listeners.window[t] = f; } };
    global.document = { hidden: false, addEventListener: (t, f) => { listeners.document[t] = f; } };
    try {
        failFirstStartWith(unauthorized());
        const hub = freshHub();
        await hub.connect();
        assert.equal(hub.getDisconnectReason(), 'auth');
        assert.equal(state.connection.starts, 1);

        listeners.window.online();
        listeners.document.visibilitychange();
        await flush();
        assert.equal(state.connection.starts, 1);
        assert.equal(hub.getConnectionState(), 'disconnected');

        assert.equal(await hub.retryNow(), true);
        assert.equal(state.connection.starts, 2);
    } finally {
        delete global.window;
        delete global.document;
    }
});

test('the performance, alerts and system health joins answer true only when the server accepted them', async () => {
    const state = { failStarts: 0 };
    installFakeSignalR(state);
    const hub = freshHub();

    // Not connected yet: nothing was joined
    assert.equal(await hub.joinPerformanceGroup(), false);
    assert.equal(await hub.joinAlertsGroup(), false);
    assert.equal(await hub.joinSystemHealthGroup(), false);

    await hub.connect();
    const invoked = [];
    state.connection.invoke = async name => { invoked.push(name); };
    assert.equal(await hub.joinPerformanceGroup(), true);
    assert.equal(await hub.joinAlertsGroup(), true);
    assert.equal(await hub.joinSystemHealthGroup(), true);
    assert.deepEqual(invoked, ['JoinPerformanceGroup', 'JoinAlertsGroup', 'JoinSystemHealthGroup']);

    // The server refuses (for example a missing role): false, not a silent success
    state.connection.invoke = async () => { throw new Error('HubException: not allowed'); };
    assert.equal(await hub.joinPerformanceGroup(), false);
    assert.equal(await hub.joinAlertsGroup(), false);
    assert.equal(await hub.joinSystemHealthGroup(), false);
});
