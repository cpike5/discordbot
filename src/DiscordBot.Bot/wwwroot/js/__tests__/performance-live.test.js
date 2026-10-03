const test = require('node:test');
const assert = require('node:assert/strict');

const Live = require('../performance/live.js');

// A stand-in for DashboardHub that records what the page asked of it.
function fakeHub(initialState) {
    const hub = {
        state: initialState || 'connected',
        calls: [],
        handlers: {},
        stateCallbacks: [],
        on(name, fn) { (hub.handlers[name] = hub.handlers[name] || []).push(fn); },
        off(name, fn) { hub.handlers[name] = (hub.handlers[name] || []).filter(h => h !== fn); },
        getConnectionState() { return hub.state; },
        onStateChange(cb) { hub.stateCallbacks.push(cb); },
        async connect() { hub.calls.push('connect'); return hub.state === 'connected'; },
        async joinPerformanceGroup() { hub.calls.push('join:performance'); },
        async leavePerformanceGroup() { hub.calls.push('leave:performance'); },
        async joinAlertsGroup() { hub.calls.push('join:alerts'); },
        async leaveAlertsGroup() { hub.calls.push('leave:alerts'); },
        async joinSystemHealthGroup() { hub.calls.push('join:system'); },
        async leaveSystemHealthGroup() { hub.calls.push('leave:system'); },
        emit(name, data) { (hub.handlers[name] || []).slice().forEach(fn => fn(data)); },
        setState(state) { hub.state = state; hub.stateCallbacks.forEach(cb => cb({ state })); }
    };
    return hub;
}

test('a tab without a subscription is never live', async () => {
    const live = Live.create(fakeHub());
    await live.subscribe(null);
    assert.equal(live.status(), 'none');
});

test('subscribing joins the group once connected and reports live', async () => {
    const hub = fakeHub();
    const live = Live.create(hub);
    let snapshots = 0;
    await live.subscribe({ group: 'performance', events: {}, snapshot: async () => { snapshots++; } });
    assert.deepEqual(hub.calls, ['join:performance']);
    assert.equal(live.status(), 'live');
    assert.equal(snapshots, 1);
});

test('pushed updates reach the tab and are reported', async () => {
    const hub = fakeHub();
    const live = Live.create(hub);
    const seen = [];
    const updates = [];
    live.onUpdate(when => updates.push(when));
    await live.subscribe({ group: 'performance', events: { HealthMetricsUpdate: d => seen.push(d.latencyMs) } });
    updates.length = 0;
    hub.emit('HealthMetricsUpdate', { latencyMs: 42 });
    assert.deepEqual(seen, [42]);
    assert.equal(updates.length, 1);
});

test('a handler that throws does not break the others or the stream', async () => {
    const hub = fakeHub();
    const live = Live.create(hub);
    const seen = [];
    await live.subscribe({
        group: 'performance',
        events: { A: () => { throw new Error('bad'); }, B: d => seen.push(d) }
    });
    const originalError = console.error;
    console.error = () => {};
    try {
        hub.emit('A', 1);
        hub.emit('B', 2);
    } finally {
        console.error = originalError;
    }
    assert.deepEqual(seen, [2]);
});

test('leaving a tab leaves the group, so the server can stop broadcasting', async () => {
    const hub = fakeHub();
    const live = Live.create(hub);
    await live.subscribe({ group: 'alerts', events: { OnAlertTriggered() {} } });
    await live.unsubscribe();
    assert.deepEqual(hub.calls, ['join:alerts', 'leave:alerts']);
    assert.equal(live.status(), 'none');
    assert.equal((hub.handlers.OnAlertTriggered || []).length, 0);
});

test('moving between tabs of one group keeps the membership', async () => {
    const hub = fakeHub();
    const live = Live.create(hub);
    await live.subscribe({ group: 'performance', events: { HealthMetricsUpdate() {} } });
    await live.subscribe({ group: 'performance', events: { HealthMetricsUpdate() {} } });
    assert.deepEqual(hub.calls, ['join:performance']);
});

test('moving to a tab of another group leaves one and joins the other', async () => {
    const hub = fakeHub();
    const live = Live.create(hub);
    await live.subscribe({ group: 'performance', events: {} });
    await live.subscribe({ group: 'system-health', events: {} });
    assert.deepEqual(hub.calls, ['join:performance', 'leave:performance', 'join:system']);
});

test('group membership is joined again on reconnect, and the snapshot refetched', async () => {
    const hub = fakeHub();
    const live = Live.create(hub);
    let snapshots = 0;
    await live.subscribe({ group: 'performance', events: {}, snapshot: async () => { snapshots++; } });
    hub.setState('reconnecting');
    assert.equal(live.status(), 'paused');

    hub.setState('connected');
    hub.emit('connected', {});
    hub.emit('reconnected', {}); // the hub raises both for a recovered connection
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(hub.calls.filter(c => c === 'join:performance').length, 2, 'joined once more, not twice');
    assert.equal(live.status(), 'live');
    assert.equal(snapshots, 2);
});

test('a hub that is down at load joins when it comes up, and is paused until then', async () => {
    const hub = fakeHub('reconnecting');
    const live = Live.create(hub);
    await live.subscribe({ group: 'alerts', events: {} });
    assert.equal(live.status(), 'paused');
    assert.deepEqual(hub.calls, ['connect']);

    hub.setState('connected');
    hub.emit('connected', {});
    await new Promise(resolve => setImmediate(resolve));
    assert.deepEqual(hub.calls, ['connect', 'join:alerts']);
    assert.equal(live.status(), 'live');
});

test('a switch that happens while the first join is in flight does not leak the group', async () => {
    const hub = fakeHub();
    let release;
    hub.joinPerformanceGroup = () => {
        hub.calls.push('join:performance');
        return new Promise(resolve => { release = resolve; });
    };
    const live = Live.create(hub);
    const first = live.subscribe({ group: 'performance', events: {} });
    await new Promise(resolve => setImmediate(resolve));
    const second = live.unsubscribe();
    release();
    await Promise.all([first, second]);
    assert.ok(hub.calls.includes('leave:performance'), 'left the group it had asked to join');
    assert.equal(live.status(), 'none');
});
