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
            const response = await fetch(API_ENDPOINT);
            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }

            const data = await response.json();

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

    /**
     * Refreshes the bot status banner with latest data from the API.
     */
    async function refreshBotStatusBanner() {
        const banner = document.querySelector('[data-bot-status-banner]');
        if (!banner) {
            return;
        }

        try {
            const response = await fetch(API_ENDPOINT);
            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }

            const data = await response.json();
            const stateKey = data.connectionState.toUpperCase();
            const stateConfig = STATUS_COLORS[stateKey] || STATUS_COLORS['DISCONNECTED'];
            const isOnline = stateConfig.isOnline;
            const wasOnline = banner.dataset.isOnline === 'true';

            // Update banner online state
            banner.dataset.isOnline = isOnline.toString();

            // Update banner classes for online/offline styling
            if (isOnline !== wasOnline) {
                if (isOnline) {
                    banner.classList.remove('offline');
                } else {
                    banner.classList.add('offline');
                }

                // Update icon container
                const iconContainer = banner.querySelector('.w-12.h-12');
                if (iconContainer) {
                    iconContainer.classList.remove('bg-success/20', 'bg-error/20');
                    iconContainer.classList.add(isOnline ? 'bg-success/20' : 'bg-error/20');
                }

                // Update icon
                const icon = iconContainer?.querySelector('svg');
                if (icon) {
                    icon.classList.remove('text-success', 'text-error');
                    icon.classList.add(isOnline ? 'text-success' : 'text-error');
                }

                // Update status badge
                const badge = banner.querySelector('[data-status-badge]');
                if (badge) {
                    badge.classList.remove('text-success', 'bg-success/20', 'text-error', 'bg-error/20');
                    badge.classList.add(isOnline ? 'text-success' : 'text-error');
                    badge.classList.add(isOnline ? 'bg-success/20' : 'bg-error/20');
                }

                // Update status dot
                const dot = banner.querySelector('[data-status-dot]');
                if (dot) {
                    dot.classList.remove('bg-success', 'bg-error', 'animate-pulse');
                    dot.classList.add(isOnline ? 'bg-success' : 'bg-error');
                    if (isOnline) {
                        dot.classList.add('animate-pulse');
                    }
                }
            }

            // Update status heading
            const heading = banner.querySelector('[data-status-heading]');
            if (heading) {
                heading.textContent = isOnline ? 'Bot is Online' : 'Bot is Offline';
            }

            // Update status text
            const statusText = banner.querySelector('[data-status-text]');
            if (statusText) {
                statusText.textContent = stateConfig.label;
            }

            // Update summary text
            const summary = banner.querySelector('[data-summary-text]');
            if (summary) {
                if (isOnline) {
                    const serverWord = data.guildCount === 1 ? 'server' : 'servers';
                    const memberWord = data.memberCount === 1 ? 'member' : 'members';
                    summary.textContent = `Connected to ${data.guildCount.toLocaleString()} ${serverWord} with ${(data.memberCount || 0).toLocaleString()} total ${memberWord}`;
                } else {
                    summary.textContent = 'Not currently connected to Discord';
                }
            }

            // Update metrics section visibility
            const metricsSection = banner.querySelector('[data-metrics-section]');
            if (metricsSection) {
                if (isOnline) {
                    metricsSection.classList.remove('hidden');
                } else {
                    metricsSection.classList.add('hidden');
                }
            }

            // Update latency
            const latencyElement = banner.querySelector('[data-latency]');
            if (latencyElement) {
                latencyElement.textContent = data.latencyMs;
            }

            // Update uptime
            const uptimeElement = banner.querySelector('[data-uptime]');
            if (uptimeElement) {
                uptimeElement.textContent = formatUptime(data.uptime);
            }

            console.log('[BotStatusRefresh] Banner updated:', data.connectionState);

        } catch (error) {
            console.error('Failed to refresh bot status banner:', error);
        }
    }

    /**
     * Initialize the bot status refresh functionality.
     */
    function init() {
        const card = document.querySelector('[data-bot-status-card]');
        const banner = document.querySelector('[data-bot-status-banner]');

        if (!card && !banner) {
            return;
        }

        const refresh = () => {
            if (card) refreshBotStatus();
            if (banner) refreshBotStatusBanner();
        };

        // Initial refresh
        refresh();

        // Quick retry after 5 seconds (handles bot startup race condition)
        setTimeout(refresh, INITIAL_RETRY_MS);

        // Set up recurring refresh
        setInterval(refresh, REFRESH_INTERVAL_MS);

        console.log(`Bot status auto-refresh initialized (initial retry: ${INITIAL_RETRY_MS / 1000}s, interval: ${REFRESH_INTERVAL_MS / 1000}s)`);
    }

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

})();
