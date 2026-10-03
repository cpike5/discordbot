/**
 * Dashboard Real-time Updates
 * Draws the live activity feed and the hero numbers from DashboardHub events.
 * Depends on: DashboardHub (dashboard-hub.js), DashboardStats (dashboard-stats.js), Format.
 *
 * What it does not do: show connection state (the page-wide banner and the Stale badge follow
 * the hub), or draw the bot status banner (bot-status-refresh.js does, from the same
 * BotStatusUpdated push).
 *
 * Every event goes to every dashboard (no hub groups), so nothing is rejoined after a reconnect.
 * What a reconnect does lose is the pushes sent while the connection was down, so the hero
 * numbers are fetched again then.
 */
const DashboardRealtime = (function() {
    'use strict';

    const CONFIG = {
        maxActivityItems: 15
    };

    let isPaused = false;
    let isInitialized = false;
    let pendingActivities = [];
    let elements = {};
    let statsRequest = 0;

    // Public API
    return {
        init,
        pause,
        resume,
        refreshStats,
        isPaused: () => isPaused,
        isConnected: () => DashboardHub.isConnected()
    };

    async function init() {
        if (isInitialized) return;
        isInitialized = true;

        cacheElements();
        setupPauseButton();

        // The hub keeps retrying after a failed first attempt and attaches handlers when it gets
        // through, so register them whether or not the first attempt connected.
        setupEventHandlers();
        await DashboardHub.connect();
    }

    function cacheElements() {
        elements = {
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
        DashboardHub.on('CommandExecuted', handleCommandExecuted);
        DashboardHub.on('GuildActivity', handleGuildActivity);
        DashboardHub.on('StatsUpdated', handleStatsUpdated);
        // Pushes sent while the connection was down are gone; ask for the numbers again.
        DashboardHub.on('reconnected', () => { refreshStats(); });
        // A tab that sat in the background may have missed pushes too.
        document.addEventListener('visibilitychange', () => {
            if (!document.hidden && DashboardHub.isConnected()) refreshStats();
        });
    }

    function handleCommandExecuted(data) {
        if (isPaused) {
            pendingActivities.unshift({ type: 'command', data });
            return;
        }

        addActivityItem({
            icon: '🔧',
            timestamp: data.timestamp,
            description: `<span class="font-mono text-accent-orange">/${SafeHtml.escape(data.commandName)}</span> ${data.success === false ? 'failed for' : 'executed by'} <span class="text-accent-blue font-medium">@${SafeHtml.escape(data.username || 'Unknown')}</span>`,
            guild: data.guildName || 'Direct Message',
            success: data.success
        });
    }

    function handleGuildActivity(data) {
        if (isPaused) {
            pendingActivities.unshift({ type: 'guild', data });
            return;
        }

        const iconMap = {
            'MemberJoined': '➕',
            'MemberLeft': '➖',
            'MessageSent': '💬',
            'MessageDeleted': '🗑️',
            'MessageEdited': '✏️',
            'RatWatchCreated': '🐀',
            'RatWatchVotingStarted': '🗳️',
            'RatWatchVotingEnded': '⚖️',
            'RatWatchCheckIn': '✅',
            'RatWatchCancelled': '❌'
        };

        addActivityItem({
            icon: iconMap[data.eventType] || '📢',
            timestamp: data.timestamp,
            description: formatGuildEventDescription(data),
            guild: data.guildName
        });
    }

    /** The StatsUpdated push: a DashboardStatsDto, camelCased. */
    function handleStatsUpdated(data) {
        window.DashboardStats.apply(document, data, window.Format);
    }

    /**
     * Fetches the hero numbers and draws them. Safe to call often: only the newest answer is
     * applied, so a slow request cannot overwrite a fresher push.
     * @returns {Promise<boolean>} true if the numbers were applied
     */
    async function refreshStats() {
        const ticket = ++statsRequest;
        try {
            const stats = await window.ApiClient.get(window.location.pathname + '?handler=Stats');
            if (ticket !== statsRequest) return false;
            window.DashboardStats.apply(document, stats, window.Format);
            return true;
        } catch (error) {
            // The connection banner already says what is wrong; the numbers stay as they were.
            console.warn('[DashboardRealtime] Could not refresh stats:', error && error.message);
            return false;
        }
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

        const setText = (selector, value) => {
            const el = itemEl.querySelector(selector);
            if (el) el.textContent = value ?? '';
        };

        // A real <time>: it reads "just now", then keeps itself current (format.js)
        const timeEl = itemEl.querySelector('.activity-timestamp');
        const when = item.timestamp ? new Date(item.timestamp) : new Date();
        const iso = (isNaN(when) ? new Date() : when).toISOString();
        if (timeEl) timeEl.setAttribute('data-relative-time', iso);
        setText('.activity-icon', item.icon);
        // description is built from escaped values by the callers above
        const descriptionEl = itemEl.querySelector('.activity-description');
        if (descriptionEl) descriptionEl.innerHTML = item.description;
        setText('.activity-guild', item.guild);

        itemEl.classList.add('activity-item-enter');

        feed.insertBefore(clone, feed.firstChild);
        if (window.Format && typeof window.Format.scan === 'function') window.Format.scan(feed.firstElementChild);

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
    }

    function formatGuildEventDescription(data) {
        // Handle Rat Watch events with dynamic content
        if (data.eventType === 'RatWatchCreated') {
            return `Rat Watch created for <span class="text-accent-blue font-medium">@${SafeHtml.escape(data.username || 'Unknown')}</span>`;
        }
        if (data.eventType === 'RatWatchVotingStarted') {
            return `Voting started for <span class="text-accent-blue font-medium">@${SafeHtml.escape(data.username || 'Unknown')}</span>`;
        }
        if (data.eventType === 'RatWatchVotingEnded') {
            const verdictClass = data.details === 'Guilty' ? 'text-error' : 'text-success';
            return `Verdict: <span class="font-semibold ${verdictClass}">${SafeHtml.escape(data.details || 'Unknown')}</span> for @${SafeHtml.escape(data.username || 'Unknown')}`;
        }
        if (data.eventType === 'RatWatchCheckIn') {
            return `<span class="text-accent-blue font-medium">@${SafeHtml.escape(data.username || 'Unknown')}</span> checked in early!`;
        }
        if (data.eventType === 'RatWatchCancelled') {
            return `Rat Watch cancelled for @${SafeHtml.escape(data.username || 'Unknown')}`;
        }

        const eventDescriptions = {
            'MemberJoined': `<span class="text-accent-blue font-medium">New member</span> joined the server`,
            'MemberLeft': `A member left the server`,
            'MessageSent': `Message sent in <span class="font-mono text-accent-orange">#channel</span>`,
            'MessageDeleted': `Message deleted`,
            'MessageEdited': `Message edited`,
            'BotJoined': `The bot was added to a server`,
            'BotLeft': `The bot was removed from a server`
        };
        return eventDescriptions[data.eventType] || `${SafeHtml.escape(data.eventType)} event`;
    }

})();

// Expose for dashboard-actions.js (a top-level const is not a window property)
window.DashboardRealtime = DashboardRealtime;

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
