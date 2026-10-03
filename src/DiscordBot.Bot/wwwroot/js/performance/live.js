/**
 * Performance Dashboard - live updates.
 *
 * One SignalR group subscription at a time, owned by the tab on screen. A tab module declares
 * what it listens to:
 *
 *   Performance.Tabs.Health.live = {
 *       group: 'performance',                       // 'performance' | 'system-health' | 'alerts'
 *       events: { HealthMetricsUpdate: handler },   // hub events to apply
 *       snapshot: async () => { ... }               // optional: fetch and apply the current values
 *   };
 *
 * dashboard.js calls Live.subscribe(spec) when such a tab opens and Live.unsubscribe() when it is
 * left (a joined group keeps the server broadcasting; PerformanceSubscriptionTracker skips the
 * broadcast when nobody is in the group). Tabs without a spec never claim to be live.
 *
 * Group membership does not survive a new connection, so the group is joined again on the hub's
 * `connected` and `reconnected` events, and the snapshot is fetched again to close the gap.
 *
 * `status()` is 'none' (no subscription), 'live' (connected and joined) or 'paused' (subscribed,
 * but the hub is down or reconnecting). onChange(fn) reports changes of that, and
 * onUpdate(fn) reports each pushed update, so the page can say when data last arrived.
 *
 * Exposed as Performance.Live (browser) and module.exports (tests, with an injected hub).
 */
(function (root, factory) {
    const api = factory();
    if (typeof module === 'object' && module.exports) {
        module.exports = api;
    } else {
        root.Performance = root.Performance || {};
        root.Performance.LiveFactory = api;
        // DashboardHub is a top-level const of dashboard-hub.js: a global binding, not a window property.
        if (typeof DashboardHub !== 'undefined') {
            root.Performance.Live = api.create(DashboardHub);
        }
    }
})(typeof self !== 'undefined' ? self : this, function () {
    'use strict';

    const GROUPS = {
        'performance': { join: 'joinPerformanceGroup', leave: 'leavePerformanceGroup' },
        'system-health': { join: 'joinSystemHealthGroup', leave: 'leaveSystemHealthGroup' },
        'alerts': { join: 'joinAlertsGroup', leave: 'leaveAlertsGroup' }
    };

    /**
     * @param {Object} hub DashboardHub (or a fake): connect, on, off, getConnectionState,
     *        onStateChange, and the join/leave functions named in GROUPS.
     */
    function create(hub) {
        let spec = null;
        let wrapped = {};          // event name -> handler registered on the hub
        let groupSent = null;      // group a join was sent for (leave must follow even if it is in flight)
        let joined = false;        // the join finished and the connection has stayed up since
        let generation = 0;        // bumped on every subscribe/unsubscribe; stale async work stops
        let bound = false;
        let joining = false;       // a join is in flight (the hub raises connected and reconnected together)
        const changeListeners = [];
        const updateListeners = [];

        function hubState() {
            return typeof hub.getConnectionState === 'function' ? hub.getConnectionState() : 'connected';
        }

        function status() {
            if (!spec) return 'none';
            return hubState() === 'connected' && joined ? 'live' : 'paused';
        }

        function notify() {
            const s = status();
            changeListeners.forEach(fn => {
                try { fn(s, hubState()); } catch (e) { console.error('[Performance.Live] listener failed', e); }
            });
        }

        function emitUpdate() {
            updateListeners.forEach(fn => {
                try { fn(new Date()); } catch (e) { console.error('[Performance.Live] listener failed', e); }
            });
        }

        function bindEvents(events) {
            wrapped = {};
            Object.keys(events || {}).forEach(name => {
                const handler = events[name];
                const fn = function (data) {
                    try {
                        handler(data);
                    } catch (e) {
                        console.error('[Performance.Live] could not apply ' + name, e);
                    }
                    emitUpdate();
                };
                wrapped[name] = fn;
                hub.on(name, fn);
            });
        }

        function unbindEvents() {
            Object.keys(wrapped).forEach(name => hub.off(name, wrapped[name]));
            wrapped = {};
        }

        async function applySnapshot(gen) {
            if (!spec || typeof spec.snapshot !== 'function') return;
            try {
                await spec.snapshot();
            } catch (e) {
                console.error('[Performance.Live] snapshot failed', e);
            }
            if (gen === generation) emitUpdate();
        }

        async function join(gen) {
            if (!spec) return;
            const group = GROUPS[spec.group];
            if (!group || hubState() !== 'connected') {
                joined = false;
                notify();
                return;
            }
            groupSent = spec.group;
            joining = true;
            try {
                await hub[group.join]();
            } finally {
                joining = false;
            }
            if (gen !== generation) return;
            joined = hubState() === 'connected';
            notify();
            await applySnapshot(gen);
        }

        async function leave(group) {
            const g = GROUPS[group];
            if (!g || hubState() !== 'connected') return;
            try {
                await hub[g.leave]();
            } catch (e) {
                console.warn('[Performance.Live] could not leave ' + group, e);
            }
        }

        function bindHub() {
            if (bound) return;
            bound = true;
            // Membership is per connection: after a reconnect (or a first connect that only worked
            // after retries) join again and refetch.
            const rejoin = function () {
                if (!spec || joining) return;
                joined = false;
                groupSent = null;
                join(generation);
            };
            hub.on('connected', rejoin);
            hub.on('reconnected', rejoin);
            if (typeof hub.onStateChange === 'function') {
                hub.onStateChange(function (e) {
                    if (e && e.state !== 'connected') joined = false;
                    notify();
                });
            }
        }

        /**
         * Starts listening for a tab. Replaces any earlier subscription; staying in the same group
         * keeps the membership and only swaps the handlers.
         */
        async function subscribe(next) {
            const previous = spec;
            const previousGroup = groupSent;
            unbindEvents();
            generation++;
            const gen = generation;
            spec = next || null;
            bindHub();

            if (!spec) {
                joined = false;
                groupSent = null;
                if (previous && previousGroup) await leave(previousGroup);
                notify();
                return;
            }

            bindEvents(spec.events);
            const sameGroup = previousGroup && previousGroup === spec.group && joined;
            if (previousGroup && !sameGroup) {
                joined = false;
                groupSent = null;
                await leave(previousGroup);
                if (gen !== generation) return;
            }
            notify();

            if (sameGroup) {
                await applySnapshot(gen);
                return;
            }
            if (typeof hub.connect === 'function' && hubState() !== 'connected') {
                // A failed first attempt is retried by the hub itself; 'connected' then joins.
                await hub.connect();
                if (gen !== generation) return;
            }
            await join(gen);
        }

        function unsubscribe() {
            return subscribe(null);
        }

        return {
            subscribe,
            unsubscribe,
            status,
            onChange(fn) { if (typeof fn === 'function') changeListeners.push(fn); },
            onUpdate(fn) { if (typeof fn === 'function') updateListeners.push(fn); }
        };
    }

    return { create, GROUPS };
});
