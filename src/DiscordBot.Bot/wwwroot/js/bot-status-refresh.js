// bot-status-refresh.js
// Auto-refresh bot status widget every 30 seconds

(function () {
    'use strict';

    // Configuration
    const REFRESH_INTERVAL_MS = 30000; // 30 seconds
    const API_ENDPOINT = '/api/bot/status';

    // Status color mappings
    const STATUS_COLORS = {
        'CONNECTED': { color: 'success', label: 'Connected' },
        'CONNECTING': { color: 'warning', label: 'Connecting' },
        'DISCONNECTING': { color: 'error', label: 'Disconnecting' },
        'DISCONNECTED': { color: 'text-tertiary', label: 'Disconnected' }
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
     * Initialize the bot status refresh functionality.
     */
    function init() {
        // Check if bot status card exists on the page
        const card = document.querySelector('[data-bot-status-card]');
        if (!card) {
            return;
        }

        // Initial refresh
        refreshBotStatus();

        // Set up recurring refresh
        setInterval(refreshBotStatus, REFRESH_INTERVAL_MS);

        console.log(`Bot status auto-refresh initialized (interval: ${REFRESH_INTERVAL_MS / 1000}s)`);
    }

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

})();
