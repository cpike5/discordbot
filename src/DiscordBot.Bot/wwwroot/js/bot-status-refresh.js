// bot-status-refresh.js
// Auto-refresh bot status widget and banner every 30 seconds

(function () {
    'use strict';

    // Configuration
    const REFRESH_INTERVAL_MS = 30000; // 30 seconds
    const INITIAL_RETRY_MS = 5000; // 5 seconds - quick retry after initial load for bot startup
    const API_ENDPOINT = '/api/bot/status';

    // Status color mappings
    const STATUS_COLORS = {
        'CONNECTED': { color: 'success', label: 'Connected', isOnline: true },
        'CONNECTING': { color: 'warning', label: 'Connecting', isOnline: false },
        'DISCONNECTING': { color: 'error', label: 'Disconnecting', isOnline: false },
        'DISCONNECTED': { color: 'text-tertiary', label: 'Disconnected', isOnline: false }
    };

    /**
     * Formats a TimeSpan string (e.g., "2.05:30:15") into human-readable format.
     * @param {string} timeSpanString - The TimeSpan string from the API
     * @returns {string} Formatted uptime (e.g., "2d 5h 30m" or "5h 30m" or "30m" or "<1m")
     */
    function formatUptime(timeSpanString) {
        // Parse TimeSpan format: "days.hours:minutes:seconds.fraction" or "hours:minutes:seconds.fraction"
        // First, strip off any fractional seconds (after the last dot if it comes after a colon)
        let cleanedString = timeSpanString;
        const lastColonIndex = timeSpanString.lastIndexOf(':');
        const lastDotIndex = timeSpanString.lastIndexOf('.');
        if (lastDotIndex > lastColonIndex) {
            // There's a fractional part in the seconds, remove it
            cleanedString = timeSpanString.substring(0, lastDotIndex);
        }

        let days = 0, hours = 0, minutes = 0, seconds = 0;

        // Check for days component (format: "days.hours:minutes:seconds")
        const daysSplit = cleanedString.split('.');
        if (daysSplit.length === 2 && daysSplit[1].includes(':')) {
            // Has days component
            days = parseInt(daysSplit[0], 10);
            const timeParts = daysSplit[1].split(':');
            hours = parseInt(timeParts[0], 10);
            minutes = parseInt(timeParts[1], 10);
            seconds = timeParts.length > 2 ? parseInt(timeParts[2], 10) : 0;
        } else {
            // No days component, format: "hours:minutes:seconds"
            const timeParts = cleanedString.split(':');
            hours = parseInt(timeParts[0], 10);
            minutes = parseInt(timeParts[1], 10);
            seconds = timeParts.length > 2 ? parseInt(timeParts[2], 10) : 0;
        }

        // Format output based on duration
        if (days > 0) {
            return `${days}d ${hours}h ${minutes}m`;
        } else if (hours > 0) {
            return `${hours}h ${minutes}m`;
        } else if (minutes > 0) {
            return `${minutes}m`;
        } else {
            return '<1m';
        }
    }

    // One request serves every consumer on the page (sidebar footer, card, banner) when they ask
    // together; the answer is reused for a second.
    let inFlight = null;
    let lastFetchAt = 0;
    let lastData = null;
    let authLost = false;   // the API answered 401/403: do not keep polling it

    function fetchStatus() {
        if (inFlight) return inFlight;
        if (lastData && Date.now() - lastFetchAt < 1000) return Promise.resolve(lastData);
        inFlight = fetch(API_ENDPOINT, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(response => {
                if (!response.ok) {
                    const error = new Error(`HTTP error! status: ${response.status}`);
                    error.status = response.status;
                    throw error;
                }
                return response.json();
            })
            .then(data => {
                lastData = data;
                lastFetchAt = Date.now();
                return data;
            })
            .finally(() => { inFlight = null; });
        return inFlight;
    }

    // ---- Sidebar footer: what the bot is doing, not whether the page loaded -------------------

    const FOOTER_STATES = {
        online: { text: 'Bot online', ledClass: '' },
        connecting: { text: 'Bot connecting…', ledClass: 'connecting' },
        offline: { text: 'Bot offline', ledClass: 'offline' },
        unknown: { text: 'Status unknown', ledClass: 'unknown' }
    };

    /** Maps the API's connection state ("Connected", "Disconnected", ...) to a footer state. */
    function footerStateFor(connectionState) {
        const key = String(connectionState || '').toUpperCase();
        if (key === 'CONNECTED') return 'online';
        if (key === 'CONNECTING') return 'connecting';
        return 'offline';
    }

    function renderFooter(stateKey) {
        const footer = document.querySelector('[data-bot-footer]');
        if (!footer) return;
        const config = FOOTER_STATES[stateKey] || FOOTER_STATES.unknown;
        const offlineMode = footer.getAttribute('data-offline-mode') === 'true';

        footer.setAttribute('data-bot-state', stateKey);
        const text = footer.querySelector('[data-bot-footer-text]');
        if (text && text.textContent !== config.text) text.textContent = config.text;

        const led = footer.querySelector('[data-bot-led]');
        if (led) {
            led.classList.remove('offline', 'connecting', 'unknown');
            if (config.ledClass) led.classList.add(config.ledClass);
        }

        const container = footer.querySelector('[data-bot-footer-container]');
        if (container) {
            container.title = offlineMode && stateKey === 'offline'
                ? 'Not connected to Discord (offline mode)'
                : config.text;
        }
    }

    /**
     * Applies a bot status payload to the page. Accepts either hub payload (BotStatusDto or
     * BotStatusUpdateDto) and the REST one; only the connection state is read.
     */
    function applyBotStatus(data) {
        if (!data) return;
        const state = data.connectionState !== undefined ? data.connectionState : data.ConnectionState;
        renderFooter(footerStateFor(state));
        // On the dashboard the same push redraws the banner (the footer is on every page)
        renderBanner(data);
    }

    async function refreshFooter() {
        try {
            applyBotStatus(await fetchStatus());
        } catch (error) {
            // Signed out, or not allowed to see this: that says nothing about the bot, so keep
            // what the server rendered (and stop asking; the session banner covers the rest).
            if (error && (error.status === 401 || error.status === 403)) {
                authLost = true;
                return;
            }
            // The server did not answer: say we do not know rather than keep a stale "online".
            renderFooter('unknown');
        }
    }

    window.BotStatus = { apply: applyBotStatus, refresh: refreshFooter, footerStateFor, watchRestart };

    /**
     * Refreshes the bot status card with latest data from the API.
     */
    async function refreshBotStatus() {
        const card = document.querySelector('[data-bot-status-card]');
        if (!card) {
            console.warn('Bot status card not found on page');
            return;
        }

        try {
            const data = await fetchStatus();

            // Update latency
            const latencyElement = card.querySelector('[data-latency]');
            if (latencyElement) {
                latencyElement.textContent = data.latencyMs;
            }

            // Update uptime
            const uptimeElement = card.querySelector('[data-uptime]');
            if (uptimeElement) {
                uptimeElement.textContent = formatUptime(data.uptime);
            }

            // Update guild count
            const guildCountElement = card.querySelector('[data-guild-count]');
            if (guildCountElement) {
                guildCountElement.textContent = data.guildCount;
            }

            // Update connection state
            const connectionStateElement = card.querySelector('[data-connection-state]');
            if (connectionStateElement) {
                const stateKey = data.connectionState.toUpperCase();
                const stateConfig = STATUS_COLORS[stateKey] || STATUS_COLORS['DISCONNECTED'];
                connectionStateElement.textContent = stateConfig.label;
            }

            // Update last updated timestamp
            const lastUpdatedElement = card.querySelector('[data-last-updated]');
            if (lastUpdatedElement) {
                lastUpdatedElement.textContent = 'Just now';
            }

        } catch (error) {
            console.error('Failed to refresh bot status:', error);
            // Optionally show error state in UI
            const lastUpdatedElement = card.querySelector('[data-last-updated]');
            if (lastUpdatedElement) {
                lastUpdatedElement.textContent = 'Update failed';
                lastUpdatedElement.classList.add('text-error');
            }
        }
    }

    // ---- Dashboard banner ----------------------------------------------------------------------

    let onlineSummaryHtml = null;

    // The banner's three looks. Whole class names, so Tailwind keeps them.
    const BANNER_TONES = {
        online: { icon: 'text-success', iconBg: 'bg-success/20', badgeText: 'text-success', badgeBg: 'bg-success/20', dot: 'bg-success' },
        offline: { icon: 'text-error', iconBg: 'bg-error/20', badgeText: 'text-error', badgeBg: 'bg-error/20', dot: 'bg-error' },
        restarting: { icon: 'text-warning', iconBg: 'bg-warning/20', badgeText: 'text-warning', badgeBg: 'bg-warning/20', dot: 'bg-warning' }
    };
    const ALL_TONE_CLASSES = ['text-success', 'bg-success/20', 'text-error', 'bg-error/20', 'text-warning', 'bg-warning/20', 'bg-success', 'bg-error', 'bg-warning'];

    function swapClasses(element, toneClasses) {
        if (!element) return;
        element.classList.remove(...ALL_TONE_CLASSES);
        element.classList.add(...toneClasses);
    }

    /**
     * Draws the dashboard banner from a status payload: the REST one (`latencyMs`) or the hub's
     * (`latency`). While a restart the user asked for is in progress (data-restarting) a state
     * other than Connected reads "Restarting", not "Offline".
     */
    function renderBanner(data) {
        const banner = document.querySelector('[data-bot-status-banner]');
        if (!banner || !data) return;

        const stateKey = String(data.connectionState !== undefined ? data.connectionState : data.ConnectionState || '').toUpperCase();
        const stateConfig = STATUS_COLORS[stateKey] || STATUS_COLORS['DISCONNECTED'];
        const connected = stateConfig.isOnline;
        const restarting = banner.dataset.restarting === 'true' && !connected;
        const toneKey = connected ? 'online' : (restarting ? 'restarting' : 'offline');
        const tone = BANNER_TONES[toneKey];
        const wasOnline = banner.dataset.isOnline === 'true';

        banner.dataset.isOnline = connected.toString();
        banner.classList.toggle('offline', !connected && !restarting);
        banner.classList.toggle('restarting', restarting);

        if (banner.dataset.tone !== toneKey) {
            banner.dataset.tone = toneKey;
            swapClasses(banner.querySelector('[data-status-icon]'), [tone.iconBg]);
            swapClasses(banner.querySelector('[data-status-icon] svg'), [tone.icon]);
            swapClasses(banner.querySelector('[data-status-badge]'), [tone.badgeText, tone.badgeBg]);
            const dot = banner.querySelector('[data-status-dot]');
            swapClasses(dot, [tone.dot]);
            if (dot) dot.classList.toggle('animate-pulse', connected || restarting);
        }

        const heading = banner.querySelector('[data-status-heading]');
        if (heading) heading.textContent = connected ? 'Bot is Online' : (restarting ? 'Bot is restarting' : 'Bot is Offline');

        const statusText = banner.querySelector('[data-status-text]');
        if (statusText) statusText.textContent = restarting ? 'Restarting' : stateConfig.label;

        const guildCount = Number(data.guildCount !== undefined ? data.guildCount : data.GuildCount);
        const summary = banner.querySelector('[data-summary-text]');
        if (summary) {
            // Keep the server-rendered "Connected to N servers with M members" sentence while the
            // banner shows something else, so coming back online does not lose the member count.
            if (wasOnline && !connected && summary.querySelector('[data-guild-count]')) {
                onlineSummaryHtml = summary.innerHTML;
            }
            if (connected && !wasOnline && onlineSummaryHtml) {
                summary.innerHTML = onlineSummaryHtml;
            }
            const countElement = summary.querySelector('[data-guild-count]');
            if (connected && countElement) {
                // The status payload has no member count, so keep the server-rendered sentence
                // and refresh only the number it does report.
                if (!isNaN(guildCount)) countElement.textContent = guildCount.toLocaleString();
            } else if (connected) {
                const serverWord = guildCount === 1 ? 'server' : 'servers';
                const countText = isNaN(guildCount) ? '' : guildCount.toLocaleString() + ' ';
                summary.textContent = `Connected to ${countText}${serverWord}`;
            } else if (restarting) {
                summary.textContent = 'Reconnecting to Discord. This takes a few seconds.';
            } else {
                summary.textContent = 'Not currently connected to Discord';
            }
        }

        const metricsSection = banner.querySelector('[data-metrics-section]');
        if (metricsSection) metricsSection.classList.toggle('hidden', !connected);

        const latency = data.latencyMs !== undefined ? data.latencyMs : data.latency;
        const latencyElement = banner.querySelector('[data-latency]');
        if (latencyElement && latency !== undefined && latency !== null) latencyElement.textContent = latency;

        const uptimeElement = banner.querySelector('[data-uptime]');
        if (uptimeElement && data.uptime) uptimeElement.textContent = formatUptime(String(data.uptime));
    }

    /**
     * Refreshes the bot status banner with latest data from the API.
     */
    async function refreshBotStatusBanner() {
        try {
            renderBanner(await fetchStatus());
        } catch (error) {
            console.warn('Failed to refresh bot status banner:', error);
        }
    }

    /**
     * Marks the banner as restarting and waits for the bot to report Connected again, polling the
     * status API. Resolves true when it does, false after the timeout. The banner shows
     * "Restarting" in between instead of flipping to Offline and back.
     * @param {object} [options]
     * @param {number} [options.timeoutMs=90000]
     * @param {number} [options.intervalMs=2000]
     */
    async function watchRestart(options) {
        const timeoutMs = (options && options.timeoutMs) || 90000;
        const intervalMs = (options && options.intervalMs) || 2000;
        const banner = document.querySelector('[data-bot-status-banner]');
        if (banner) banner.dataset.restarting = 'true';

        const deadline = Date.now() + timeoutMs;
        let connected = false;
        // The status API is polled directly here: fetchStatus() reuses an answer for a second, so
        // this loop would otherwise keep reading the same one.
        lastData = null;
        while (Date.now() < deadline) {
            let data = null;
            try {
                data = await fetchStatus();
            } catch (error) {
                // The server may itself be restarting; keep trying until the deadline
            }
            if (data) {
                connected = String(data.connectionState || '').toUpperCase() === 'CONNECTED';
                renderBanner(data);
                if (connected) break;
            }
            await new Promise(resolve => setTimeout(resolve, intervalMs));
            lastData = null;
        }

        if (banner) delete banner.dataset.restarting;
        // Draw the final state (Offline after a timeout) without the restarting wording
        if (!connected && lastData) renderBanner(lastData);
        else if (!connected) await refreshBotStatusBanner();
        return connected;
    }

    /**
     * Initialize the bot status refresh functionality.
     */
    function init() {
        const card = document.querySelector('[data-bot-status-card]');
        const banner = document.querySelector('[data-bot-status-banner]');
        const footer = document.querySelector('[data-bot-footer]');

        if (!card && !banner && !footer) {
            return;
        }

        const refresh = () => {
            if (document.hidden || authLost) return;
            if (footer) refreshFooter();
            if (card) refreshBotStatus();
            if (banner) refreshBotStatusBanner();
        };

        // Initial refresh. The footer was rendered by the server, so it only needs the live check.
        refresh();

        // Quick retry after 5 seconds (handles bot startup race condition)
        setTimeout(refresh, INITIAL_RETRY_MS);

        // Set up recurring refresh
        setInterval(refresh, REFRESH_INTERVAL_MS);

        // Coming back to a tab that sat in the background: do not show 30 seconds of old state.
        document.addEventListener('visibilitychange', () => {
            if (!document.hidden) refresh();
        });

        // The hub pushes the bot's state when it changes, and a reconnect may have missed pushes.
        if (typeof DashboardHub !== 'undefined') {
            DashboardHub.on('BotStatusUpdated', applyBotStatus);
            DashboardHub.on('reconnected', refresh);
            DashboardHub.on('connected', refresh);
        }

        console.log(`Bot status auto-refresh initialized (initial retry: ${INITIAL_RETRY_MS / 1000}s, interval: ${REFRESH_INTERVAL_MS / 1000}s)`);
    }

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

})();
