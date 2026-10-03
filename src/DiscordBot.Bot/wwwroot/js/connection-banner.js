/**
 * Connection banner: the page-wide "Reconnecting…" notice and the stale badges.
 *
 * Driven by DashboardHub.onStateChange. Markup is Pages/Shared/_ConnectionBanner.cshtml (hidden
 * until needed) plus any number of <span data-stale-badge hidden>Stale</span> elements, which are
 * shown while live updates are paused: the sidebar footer has one, and a page that labels a block
 * "Live" can put one next to it.
 *
 * What it reports is the SignalR connection, not the bot. In offline mode the hub connects fine
 * and the banner stays hidden; the bot's own state is the sidebar footer's job
 * (bot-status-refresh.js).
 */
(function (root, factory) {
    'use strict';
    const api = factory();
    if (typeof module !== 'undefined' && module.exports) {
        module.exports = api;
    }
    if (typeof window !== 'undefined' && root === window) {
        window.ConnectionBanner = api;
        // DashboardHub is a top-level const, so it is a global binding but not a window property.
        const start = function () {
            api.init(typeof DashboardHub !== 'undefined' ? DashboardHub : null, document);
        };
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', start);
        } else {
            start();
        }
    }
})(typeof window !== 'undefined' ? window : globalThis, function () {
    'use strict';

    // A connection that is down for less than this is a blip (a navigation, a quick server
    // restart); showing the banner for it would flash on every page load.
    const SHOW_AFTER_MS = 1500;
    // How long "Live updates restored" stays up before the banner goes away.
    const RESTORED_MS = 3000;

    /**
     * What the banner should say for a hub state.
     * @returns {{pillState: string, pillText: string, text: string, announce: string}|null}
     *          null when the banner should not be shown.
     */
    function describeDown(state, reason) {
        if (state === 'disconnected' && reason === 'auth') {
            return {
                pillState: 'disconnected',
                pillText: 'Signed out',
                text: 'Your session has expired. Sign in again to get live updates.',
                announce: 'Your session has expired. Live updates stopped.',
                signIn: true
            };
        }
        if (state === 'reconnecting') {
            return {
                pillState: 'reconnecting',
                pillText: 'Reconnecting…',
                text: 'Live updates are paused, so this page may be out of date.',
                announce: 'Live updates lost. Reconnecting.'
            };
        }
        if (state === 'disconnected') {
            return {
                pillState: 'disconnected',
                pillText: 'Disconnected',
                text: 'Live updates are off. Reload the page or try again.',
                announce: 'Live updates are off.'
            };
        }
        return null;
    }

    function describeRestored() {
        return {
            pillState: 'connected',
            pillText: 'Connected',
            text: 'Live updates restored.',
            announce: 'Live updates restored.'
        };
    }

    function init(hub, doc) {
        const banner = doc.querySelector('[data-connection-banner]');
        if (!banner || !hub) return null;

        const pill = banner.querySelector('.connection-status');
        const pillText = banner.querySelector('.connection-text');
        const text = banner.querySelector('[data-connection-banner-text]');
        const retry = banner.querySelector('[data-connection-retry]');
        const signIn = banner.querySelector('[data-connection-signin]');
        const announcer = doc.getElementById('connection-announcer');

        let down = false;      // the hub is not connected
        let shown = false;     // the banner is on screen
        let showTimer = null;
        let hideTimer = null;
        let currentReason = null;

        function setStale(isStale) {
            doc.querySelectorAll('[data-stale-badge]').forEach(function (el) {
                el.hidden = !isStale;
            });
        }

        function render(view, showRetry) {
            if (pill) pill.setAttribute('data-state', view.pillState);
            if (pillText) pillText.textContent = view.pillText;
            if (text) text.textContent = view.text;
            // An ended session needs a sign-in, not a retry.
            if (retry) retry.hidden = !showRetry || !!view.signIn;
            if (signIn) {
                signIn.hidden = !view.signIn;
                if (view.signIn && typeof window !== 'undefined' && window.location) {
                    signIn.setAttribute('href', '/Account/Login?ReturnUrl=' +
                        encodeURIComponent(window.location.pathname + window.location.search));
                }
            }
            if (announcer) announcer.textContent = view.announce;
        }

        function show(state, reason) {
            const view = describeDown(state, reason);
            if (!view) return;
            render(view, true);
            banner.hidden = false;
            shown = true;
            setStale(true);
        }

        function hide() {
            banner.hidden = true;
            shown = false;
        }

        function clearTimers() {
            if (showTimer !== null) { clearTimeout(showTimer); showTimer = null; }
            if (hideTimer !== null) { clearTimeout(hideTimer); hideTimer = null; }
        }

        function handle(state, reason) {
            doc.documentElement.setAttribute('data-hub-state', state);
            currentReason = reason || null;

            if (state === 'reconnecting' || state === 'disconnected') {
                const wasDown = down;
                down = true;
                if (hideTimer !== null) { clearTimeout(hideTimer); hideTimer = null; }
                if (shown || currentReason === 'auth') {
                    // Wording follows the state; an ended session is not a blip, so no grace period.
                    clearTimers();
                    show(state, currentReason);
                } else if (!wasDown || showTimer === null) {
                    clearTimers();
                    showTimer = setTimeout(function () {
                        showTimer = null;
                        if (down) show(doc.documentElement.getAttribute('data-hub-state'), currentReason);
                    }, SHOW_AFTER_MS);
                }
                return;
            }

            if (state === 'connected') {
                const wasDown = down;
                down = false;
                clearTimers();
                setStale(false);
                if (wasDown && shown) {
                    render(describeRestored(), false);
                    hideTimer = setTimeout(function () {
                        hideTimer = null;
                        hide();
                    }, RESTORED_MS);
                }
            }
            // 'connecting' is the first attempt of a page load: nothing to say yet.
        }

        if (retry) {
            retry.addEventListener('click', function () {
                retry.disabled = true;
                const label = retry.textContent;
                retry.textContent = 'Retrying…';
                Promise.resolve(hub.retryNow ? hub.retryNow() : false).finally(function () {
                    retry.disabled = false;
                    retry.textContent = label;
                });
            });
        }

        hub.onStateChange(function (change) { handle(change.state, change.reason); });
        // 'disconnected' before connect() has run is just the starting state, not an outage.
        const initial = hub.getConnectionState();
        if (initial !== 'disconnected') handle(initial, hub.getDisconnectReason ? hub.getDisconnectReason() : null);

        return { handle: handle };
    }

    return { SHOW_AFTER_MS, RESTORED_MS, describeDown, describeRestored, init };
});
