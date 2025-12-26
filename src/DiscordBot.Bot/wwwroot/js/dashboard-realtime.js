/**
 * Dashboard Real-time Updates
 * Handles SignalR connection and DOM updates for real-time dashboard data.
 * Depends on: DashboardHub (dashboard-hub.js)
 */
const DashboardRealtime = (function() {
    'use strict';

    const CONFIG = {
        maxActivityItems: 15,
        updateDebounceMs: 100
    };

    let isPaused = false;
    let isInitialized = false;
    let pendingActivities = [];
    let elements = {};

    // Public API
    return {
        init,
        pause,
        resume,
        isPaused: () => isPaused,
        isConnected: () => DashboardHub.isConnected()
    };

    async function init() {
        if (isInitialized) return;
        isInitialized = true;

        console.log('[DashboardRealtime] Initializing...');

        cacheElements();
        setupPauseButton();

        // Connect to SignalR
        const connected = await DashboardHub.connect();

        if (connected) {
            updateConnectionStatus('connected');
            setupEventHandlers();
        } else {
            updateConnectionStatus('disconnected');
        }

        // Setup connection state handlers
        DashboardHub.on('reconnecting', () => updateConnectionStatus('reconnecting'));
        DashboardHub.on('reconnected', () => updateConnectionStatus('connected'));
        DashboardHub.on('disconnected', () => updateConnectionStatus('disconnected'));
        DashboardHub.on('connectionFailed', () => updateConnectionStatus('disconnected'));
    }

    function cacheElements() {
        elements = {
            connectionStatus: document.getElementById('connection-status'),
            botStatusCard: document.querySelector('[data-bot-status-card]'),
            activityFeed: document.getElementById('activity-feed'),
            activityItemTemplate: document.getElementById('activity-item-template'),
            pauseBtn: document.getElementById('pause-feed-btn'),
            pauseBtnText: document.getElementById('pause-btn-text'),
            pauseIcon: document.getElementById('pause-icon'),
            playIcon: document.getElementById('play-icon'),
            pausedIndicator: document.getElementById('feed-paused-indicator'),
            emptyState: document.getElementById('empty-state')
        };
    }

    function setupPauseButton() {
        if (elements.pauseBtn) {
            elements.pauseBtn.addEventListener('click', () => {
                if (isPaused) {
                    resume();
                } else {
                    pause();
                }
            });
        }
    }

    function setupEventHandlers() {
        DashboardHub.on('BotStatusUpdated', handleBotStatusUpdated);
        DashboardHub.on('CommandExecuted', handleCommandExecuted);
        DashboardHub.on('GuildActivity', handleGuildActivity);
        DashboardHub.on('StatsUpdated', handleStatsUpdated);
    }

    function handleBotStatusUpdated(data) {
        console.log('[DashboardRealtime] BotStatusUpdated:', data);

        const card = elements.botStatusCard;
        if (!card) return;

        // Update DOM elements
        updateElement(card, '[data-connection-state]', data.connectionState);
        updateElement(card, '[data-latency]', data.latency ? `${data.latency}ms` : 'N/A');
        updateElement(card, '[data-uptime]', formatUptime(data.uptime));
        updateElement(card, '[data-guild-count]', data.guildCount);
        updateElement(card, '[data-last-updated]', 'Just now');

        triggerUpdatePulse(card);
    }

    function handleCommandExecuted(data) {
        console.log('[DashboardRealtime] CommandExecuted:', data);

        if (isPaused) {
            pendingActivities.unshift({ type: 'command', data });
            return;
        }

        addActivityItem({
            icon: '🔧',
            timestamp: new Date(data.timestamp),
            description: `<span class="font-mono text-accent-orange">/${data.commandName}</span> executed by <span class="text-accent-blue font-medium">@${escapeHtml(data.username || 'Unknown')}</span>`,
            guild: escapeHtml(data.guildName || 'Direct Message'),
            success: data.success
        });
    }

    function handleGuildActivity(data) {
        console.log('[DashboardRealtime] GuildActivity:', data);

        if (isPaused) {
            pendingActivities.unshift({ type: 'guild', data });
            return;
        }

        const iconMap = {
            'MemberJoined': '➕',
            'MemberLeft': '➖',
            'MessageSent': '💬',
            'MessageDeleted': '🗑️',
            'MessageEdited': '✏️'
        };

        addActivityItem({
            icon: iconMap[data.eventType] || '📢',
            timestamp: new Date(data.timestamp),
            description: formatGuildEventDescription(data),
            guild: escapeHtml(data.guildName)
        });
    }

    function handleStatsUpdated(data) {
        console.log('[DashboardRealtime] StatsUpdated:', data);

        // Update stats cards if they exist
        if (data.totalCommands !== undefined) {
            updateElement(document.body, '[data-total-commands]', data.totalCommands);
        }
        if (data.activeUsers !== undefined) {
            updateElement(document.body, '[data-active-users]', data.activeUsers);
        }
        if (data.messagesProcessed !== undefined) {
            updateElement(document.body, '[data-messages-processed]', data.messagesProcessed);
        }
    }

    function updateConnectionStatus(state) {
        const statusEl = elements.connectionStatus;
        if (!statusEl) return;

        statusEl.setAttribute('data-state', state);

        const textEl = statusEl.querySelector('.connection-text');
        if (textEl) {
            const labels = {
                'connected': 'Connected',
                'connecting': 'Connecting...',
                'reconnecting': 'Reconnecting...',
                'disconnected': 'Disconnected'
            };
            textEl.textContent = labels[state] || 'Unknown';
        }

        console.log('[DashboardRealtime] Connection status:', state);
    }

    function addActivityItem(item) {
        const feed = elements.activityFeed;
        const template = elements.activityItemTemplate;
        const emptyState = elements.emptyState;

        if (!feed || !template) return;

        // Hide empty state if it exists
        if (emptyState) {
            emptyState.classList.add('hidden');
        }

        const clone = template.content.cloneNode(true);
        const itemEl = clone.querySelector('.activity-item');

        itemEl.querySelector('.activity-timestamp').textContent = formatTimestamp(item.timestamp);
        itemEl.querySelector('.activity-icon').textContent = item.icon;
        itemEl.querySelector('.activity-description').innerHTML = item.description;
        itemEl.querySelector('.activity-guild').textContent = item.guild;

        itemEl.classList.add('activity-item-enter');

        feed.insertBefore(clone, feed.firstChild);

        // Limit items
        while (feed.children.length > CONFIG.maxActivityItems) {
            const lastChild = feed.lastChild;
            if (lastChild && !lastChild.id) { // Don't remove empty-state
                feed.removeChild(lastChild);
            } else {
                break;
            }
        }
    }

    function pause() {
        isPaused = true;

        if (elements.pauseBtn) {
            elements.pauseBtn.classList.add('active');
            elements.pauseBtn.setAttribute('aria-pressed', 'true');
            elements.pauseBtn.setAttribute('aria-label', 'Resume activity feed');
        }
        if (elements.pauseBtnText) elements.pauseBtnText.textContent = 'Resume';
        if (elements.pauseIcon) elements.pauseIcon.classList.add('hidden');
        if (elements.playIcon) elements.playIcon.classList.remove('hidden');
        if (elements.pausedIndicator) elements.pausedIndicator.classList.remove('hidden');

        console.log('[DashboardRealtime] Feed paused');
    }

    function resume() {
        isPaused = false;

        if (elements.pauseBtn) {
            elements.pauseBtn.classList.remove('active');
            elements.pauseBtn.setAttribute('aria-pressed', 'false');
            elements.pauseBtn.setAttribute('aria-label', 'Pause activity feed');
        }
        if (elements.pauseBtnText) elements.pauseBtnText.textContent = 'Pause';
        if (elements.pauseIcon) elements.pauseIcon.classList.remove('hidden');
        if (elements.playIcon) elements.playIcon.classList.add('hidden');
        if (elements.pausedIndicator) elements.pausedIndicator.classList.add('hidden');

        // Process pending activities
        while (pendingActivities.length > 0) {
            const pending = pendingActivities.pop(); // Process oldest first
            if (pending.type === 'command') {
                handleCommandExecuted(pending.data);
            } else if (pending.type === 'guild') {
                handleGuildActivity(pending.data);
            }
        }

        console.log('[DashboardRealtime] Feed resumed');
    }

    // Helper functions
    function updateElement(parent, selector, value) {
        const el = parent.querySelector(selector);
        if (el) el.textContent = value;
    }

    function triggerUpdatePulse(element) {
        element.classList.remove('card-update-pulse');
        void element.offsetWidth; // Force reflow
        element.classList.add('card-update-pulse');
    }

    function formatTimestamp(date) {
        if (!(date instanceof Date) || isNaN(date)) {
            date = new Date();
        }
        return date.toTimeString().split(' ')[0]; // HH:MM:SS
    }

    function formatUptime(uptimeStr) {
        if (!uptimeStr) return '0m';

        // Parse TimeSpan format: "d.hh:mm:ss.fffffff" or "hh:mm:ss"
        const parts = uptimeStr.split(':');
        if (parts.length < 2) return uptimeStr;

        let days = 0, hours = 0, minutes = 0;

        if (parts[0].includes('.')) {
            const dayHour = parts[0].split('.');
            days = parseInt(dayHour[0]) || 0;
            hours = parseInt(dayHour[1]) || 0;
        } else {
            hours = parseInt(parts[0]) || 0;
        }
        minutes = parseInt(parts[1]) || 0;

        let result = '';
        if (days > 0) result += `${days}d `;
        if (hours > 0 || days > 0) result += `${hours}h `;
        result += `${minutes}m`;

        return result.trim();
    }

    function formatGuildEventDescription(data) {
        const eventDescriptions = {
            'MemberJoined': `<span class="text-accent-blue font-medium">New member</span> joined the server`,
            'MemberLeft': `A member left the server`,
            'MessageSent': `Message sent in <span class="font-mono text-accent-orange">#channel</span>`,
            'MessageDeleted': `Message deleted`,
            'MessageEdited': `Message edited`
        };
        return eventDescriptions[data.eventType] || `${escapeHtml(data.eventType)} event`;
    }

    function escapeHtml(unsafe) {
        if (!unsafe) return '';
        return unsafe
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }
})();

// Auto-initialize when DOM is ready
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', DashboardRealtime.init);
} else {
    DashboardRealtime.init();
}

// Export for module systems
if (typeof module !== 'undefined' && module.exports) {
    module.exports = DashboardRealtime;
}
