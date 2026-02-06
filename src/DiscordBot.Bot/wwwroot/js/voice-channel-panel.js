/**
 * Voice Channel Panel Module
 * Handles real-time voice connection status, playback, and queue management.
 * Integrates with DashboardHub SignalR for live updates.
 */
const VoiceChannelPanel = (function() {
    'use strict';

    // DOM element references
    let panelElement = null;
    let channelSelector = null;
    let leaveButton = null;
    let stopButton = null;
    let connectionStatusBadge = null;
    let connectionStatusDot = null;
    let connectionStatusText = null;
    let connectedChannelInfo = null;
    let connectedChannelName = null;
    let channelMemberCount = null;
    let nowPlayingSection = null;
    let nowPlayingName = null;
    let nowPlayingProgress = null;
    let nowPlayingPosition = null;
    let nowPlayingDuration = null;
    let queueList = null;
    let queueEmptyState = null;
    let queueCountBadge = null;

    // State
    let guildId = null;
    let isConnected = false;
    let connectedChannelId = null;
    let isHubConnected = false;

    /**
     * Initializes the Voice Channel Panel module.
     * Call this after the DOM is ready and DashboardHub is connected.
     */
    function init() {
        panelElement = document.getElementById('voice-channel-panel');
        if (!panelElement) {
            console.log('[VoiceChannelPanel] Panel element not found, skipping initialization');
            return;
        }

        // Get guild ID from data attribute
        guildId = panelElement.dataset.guildId;
        isConnected = panelElement.dataset.connected === 'true';
        connectedChannelId = panelElement.dataset.channelId || null;

        // Cache DOM elements
        channelSelector = document.getElementById('channel-selector');
        leaveButton = document.getElementById('leave-channel-btn');
        stopButton = document.getElementById('stop-playback-btn');
        connectionStatusBadge = document.getElementById('connection-status-badge');
        connectionStatusDot = document.getElementById('connection-status-dot');
        connectionStatusText = document.getElementById('connection-status-text');
        connectedChannelInfo = document.getElementById('connected-channel-info');
        connectedChannelName = document.getElementById('connected-channel-name');
        channelMemberCount = document.getElementById('channel-member-count');
        nowPlayingSection = document.getElementById('now-playing-section');
        nowPlayingName = document.getElementById('now-playing-name');
        nowPlayingProgress = document.getElementById('now-playing-progress');
        nowPlayingPosition = document.getElementById('now-playing-position');
        nowPlayingDuration = document.getElementById('now-playing-duration');
        queueList = document.getElementById('queue-list');
        queueEmptyState = document.getElementById('queue-empty-state');
        queueCountBadge = document.getElementById('queue-count-badge');

        // Set up event listeners
        setupEventListeners();

        // Connect to SignalR if DashboardHub is available
        if (typeof DashboardHub !== 'undefined') {
            setupSignalRHandlers();
        } else {
            console.warn('[VoiceChannelPanel] DashboardHub not available!');
        }

        console.log('[VoiceChannelPanel] Initialized for guild:', guildId, {
            guildIdType: typeof guildId,
            isConnected,
            connectedChannelId,
            dashboardHubAvailable: typeof DashboardHub !== 'undefined'
        });
    }

    /**
     * Sets up DOM event listeners for user interactions.
     */
    function setupEventListeners() {
        // Channel selector change
        if (channelSelector) {
            channelSelector.addEventListener('change', handleChannelSelect);
        }

        // Leave button click
        if (leaveButton) {
            leaveButton.addEventListener('click', handleLeaveChannel);
        }

        // Stop button click
        if (stopButton) {
            stopButton.addEventListener('click', handleStopPlayback);
        }

        // Queue skip buttons (delegated)
        if (queueList) {
            queueList.addEventListener('click', function(e) {
                const skipBtn = e.target.closest('.skip-queue-btn');
                if (skipBtn) {
                    const position = parseInt(skipBtn.dataset.position, 10);
                    handleSkipQueueItem(position);
                }
            });
        }
    }

    /**
     * Sets up SignalR event handlers for real-time updates.
     */
    function setupSignalRHandlers() {
        console.log('[VoiceChannelPanel] Setting up SignalR handlers, DashboardHub.isConnected():', DashboardHub.isConnected());

        // Connection state handlers
        DashboardHub.on('connected', function() {
            console.log('[VoiceChannelPanel] SignalR connected event received');
            isHubConnected = true;
            joinGuildAudioGroup();
        });

        DashboardHub.on('reconnected', function() {
            console.log('[VoiceChannelPanel] SignalR reconnected event received');
            isHubConnected = true;
            joinGuildAudioGroup();
        });

        DashboardHub.on('disconnected', function() {
            console.log('[VoiceChannelPanel] SignalR disconnected event received');
            isHubConnected = false;
        });

        // Audio event handlers
        DashboardHub.on('AudioConnected', handleAudioConnected);
        DashboardHub.on('AudioDisconnected', handleAudioDisconnected);
        DashboardHub.on('PlaybackStarted', handlePlaybackStarted);
        DashboardHub.on('PlaybackProgress', handlePlaybackProgress);
        DashboardHub.on('PlaybackFinished', handlePlaybackFinished);
        DashboardHub.on('QueueUpdated', handleQueueUpdated);

        console.log('[VoiceChannelPanel] Audio event handlers registered');

        // If already connected, join the guild audio group
        if (DashboardHub.isConnected()) {
            console.log('[VoiceChannelPanel] DashboardHub already connected, joining audio group');
            isHubConnected = true;
            joinGuildAudioGroup();
        } else {
            console.log('[VoiceChannelPanel] DashboardHub not yet connected, waiting for connected event');
        }
    }

    /**
     * Joins the guild-specific audio group for SignalR events.
     */
    async function joinGuildAudioGroup() {
        console.log('[VoiceChannelPanel] joinGuildAudioGroup called', { isHubConnected, guildId });

        if (!isHubConnected) {
            console.warn('[VoiceChannelPanel] Cannot join audio group: hub not connected');
            return;
        }
        if (!guildId) {
            console.warn('[VoiceChannelPanel] Cannot join audio group: no guildId');
            return;
        }

        try {
            await DashboardHub.joinGuildAudioGroup(guildId);
            console.log('[VoiceChannelPanel] Joined guild audio group:', guildId);
        } catch (error) {
            console.error('[VoiceChannelPanel] Failed to join guild audio group:', error);
        }
    }

    /**
     * Handles channel selection change.
     */
    async function handleChannelSelect(e) {
        const selectedChannelId = e.target.value;
        console.log('[VoiceChannelPanel] Channel selected:', selectedChannelId, 'guildId:', guildId);

        if (!selectedChannelId || !guildId) {
            console.log('[VoiceChannelPanel] Skipping - no channel or guild ID');
            return;
        }

        try {
            setChannelSelectorLoading(true);

            const url = `/api/guilds/${guildId}/audio/join/${selectedChannelId}`;
            console.log('[VoiceChannelPanel] Calling API:', url);

            const response = await fetch(url, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                }
            });

            console.log('[VoiceChannelPanel] API response status:', response.status);

            if (!response.ok) {
                const error = await response.json();
                console.error('[VoiceChannelPanel] API error:', error);
                showToast(error.message || 'Failed to join channel', 'error');
                // Reset selector to previous value
                channelSelector.value = connectedChannelId || '';
            } else {
                const result = await response.json();
                console.log('[VoiceChannelPanel] API success:', result);
                console.log('[VoiceChannelPanel] Waiting for AudioConnected SignalR event...');
            }
        } catch (error) {
            console.error('[VoiceChannelPanel] Error joining channel:', error);
            showToast('Failed to join channel', 'error');
            channelSelector.value = connectedChannelId || '';
        } finally {
            setChannelSelectorLoading(false);
        }
    }

    /**
     * Handles leave channel button click.
     */
    async function handleLeaveChannel() {
        if (!guildId) return;

        try {
            setLeaveButtonLoading(true);

            const response = await fetch(`/api/guilds/${guildId}/audio/leave`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                }
            });

            if (!response.ok) {
                const error = await response.json();
                showToast(error.message || 'Failed to leave channel', 'error');
            }
            // Success will be handled by AudioDisconnected SignalR event
        } catch (error) {
            console.error('[VoiceChannelPanel] Error leaving channel:', error);
            showToast('Failed to leave channel', 'error');
        } finally {
            setLeaveButtonLoading(false);
        }
    }

    /**
     * Handles stop playback button click.
     */
    async function handleStopPlayback() {
        if (!guildId) return;

        try {
            const response = await fetch(`/api/guilds/${guildId}/audio/stop`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                }
            });

            if (!response.ok) {
                const error = await response.json();
                showToast(error.message || 'Failed to stop playback', 'error');
            }
            // Success will be handled by PlaybackFinished SignalR event
        } catch (error) {
            console.error('[VoiceChannelPanel] Error stopping playback:', error);
            showToast('Failed to stop playback', 'error');
        }
    }

    /**
     * Handles skip queue item button click.
     * @param {number} position - The queue position to skip.
     */
    async function handleSkipQueueItem(position) {
        if (!guildId) return;

        try {
            const response = await fetch(`/api/guilds/${guildId}/audio/queue/${position}`, {
                method: 'DELETE',
                headers: {
                    'Content-Type': 'application/json'
                }
            });

            if (!response.ok) {
                const error = await response.json();
                showToast(error.message || 'Failed to skip item', 'error');
            }
            // Success will be handled by QueueUpdated SignalR event
        } catch (error) {
            console.error('[VoiceChannelPanel] Error skipping queue item:', error);
            showToast('Failed to skip item', 'error');
        }
    }

    /**
     * Handles AudioConnected SignalR event.
     * @param {object} data - Event data with guildId, channelId, channelName.
     */
    function handleAudioConnected(data) {
        // Compare as strings - guildId from dataset is string, data.guildId from SignalR is string (serialized from ulong)
        if (String(data.guildId) !== guildId) return;

        console.log('[VoiceChannelPanel] Processing AudioConnected for this guild');

        isConnected = true;
        connectedChannelId = data.channelId;

        // Update connection status
        updateConnectionStatus(true, data.channelName, data.memberCount);

        // Update channel selector
        if (channelSelector) {
            channelSelector.value = data.channelId;
        }

        // Show leave button
        if (leaveButton) {
            leaveButton.classList.remove('hidden');
        }

        console.log('[VoiceChannelPanel] UI updated successfully');
    }

    /**
     * Handles AudioDisconnected SignalR event.
     * @param {object} data - Event data with guildId, reason.
     */
    function handleAudioDisconnected(data) {
        if (String(data.guildId) !== guildId) return;

        console.log('[VoiceChannelPanel] Audio disconnected:', data);

        isConnected = false;
        connectedChannelId = null;

        // Update connection status
        updateConnectionStatus(false);

        // Reset channel selector
        if (channelSelector) {
            channelSelector.value = '';
        }

        // Hide leave button
        if (leaveButton) {
            leaveButton.classList.add('hidden');
        }

        // Clear now playing
        updateNowPlaying(null);

        // Clear queue
        updateQueue([]);
    }

    /**
     * Handles PlaybackStarted SignalR event.
     * @param {object} data - Event data with guildId, soundId, name, durationSeconds.
     */
    function handlePlaybackStarted(data) {
        if (String(data.guildId) !== guildId) return;

        console.log('[VoiceChannelPanel] Playback started:', data);

        updateNowPlaying({
            id: data.soundId,
            name: data.name,
            durationSeconds: data.durationSeconds,
            positionSeconds: 0
        });
    }

    /**
     * Handles PlaybackProgress SignalR event.
     * @param {object} data - Event data with guildId, soundId, positionSeconds, durationSeconds.
     */
    function handlePlaybackProgress(data) {
        if (String(data.guildId) !== guildId) return;

        updatePlaybackProgress(data.positionSeconds, data.durationSeconds);
    }

    /**
     * Handles PlaybackFinished SignalR event.
     * @param {object} data - Event data with guildId, soundId.
     */
    function handlePlaybackFinished(data) {
        if (String(data.guildId) !== guildId) return;

        console.log('[VoiceChannelPanel] Playback finished:', data);

        updateNowPlaying(null);
    }

    /**
     * Handles QueueUpdated SignalR event.
     * @param {object} data - Event data with guildId, queue array.
     */
    function handleQueueUpdated(data) {
        if (String(data.guildId) !== guildId) return;

        console.log('[VoiceChannelPanel] Queue updated:', data);

        updateQueue(data.queue || []);
    }

    /**
     * Updates the connection status display.
     * @param {boolean} connected - Whether connected.
     * @param {string} channelName - Connected channel name.
     * @param {number} memberCount - Member count in channel.
     */
    function updateConnectionStatus(connected, channelName, memberCount) {
        if (connectionStatusDot) {
            connectionStatusDot.className = connected
                ? 'w-1.5 h-1.5 rounded-full bg-success'
                : 'w-1.5 h-1.5 rounded-full bg-text-tertiary';
        }

        if (connectionStatusText) {
            connectionStatusText.textContent = connected ? 'Connected' : 'Disconnected';
        }

        if (connectionStatusBadge) {
            connectionStatusBadge.className = connected
                ? 'inline-flex items-center gap-1.5 px-2 py-0.5 text-xs font-medium rounded-full bg-success/20 text-success'
                : 'inline-flex items-center gap-1.5 px-2 py-0.5 text-xs font-medium rounded-full bg-bg-tertiary text-text-tertiary';
        }

        if (connectedChannelInfo) {
            if (connected && channelName) {
                connectedChannelInfo.classList.remove('hidden');
                if (connectedChannelName) {
                    connectedChannelName.textContent = channelName;
                }
                if (channelMemberCount && memberCount !== undefined) {
                    channelMemberCount.textContent = memberCount;
                }
            } else {
                connectedChannelInfo.classList.add('hidden');
            }
        }

        // Update panel data attribute
        if (panelElement) {
            panelElement.dataset.connected = connected.toString();
            panelElement.dataset.channelId = connected ? connectedChannelId : '';
        }
    }

    /**
     * Updates the now playing display.
     * @param {object|null} nowPlaying - Now playing info or null.
     */
    function updateNowPlaying(nowPlaying) {
        if (!nowPlayingSection) return;

        if (nowPlaying) {
            nowPlayingSection.classList.remove('hidden');

            if (nowPlayingName) {
                nowPlayingName.textContent = nowPlaying.name;
            }

            updatePlaybackProgress(nowPlaying.positionSeconds || 0, nowPlaying.durationSeconds || 0);
        } else {
            nowPlayingSection.classList.add('hidden');
        }
    }

    /**
     * Updates the playback progress bar and time display.
     * Handles missing progress elements gracefully (when ShowProgress = false).
     * @param {number} position - Current position in seconds.
     * @param {number} duration - Total duration in seconds.
     */
    function updatePlaybackProgress(position, duration) {
        // Progress elements may not exist if ShowProgress = false, so check before updating
        if (nowPlayingProgress) {
            const percent = duration > 0 ? Math.round((position / duration) * 100) : 0;
            nowPlayingProgress.style.width = `${percent}%`;
        }

        if (nowPlayingPosition) {
            nowPlayingPosition.textContent = formatDuration(position);
        }

        if (nowPlayingDuration) {
            nowPlayingDuration.textContent = formatDuration(duration);
        }
    }

    /**
     * Updates the queue display.
     * @param {Array} queue - Array of queue items.
     */
    function updateQueue(queue) {
        if (!queueList || !queueEmptyState || !queueCountBadge) return;

        // Update count badge
        const count = queue.length;
        queueCountBadge.textContent = count > 0
            ? `${count} sound${count === 1 ? '' : 's'}`
            : 'Empty';

        if (count === 0) {
            queueList.classList.add('hidden');
            queueList.innerHTML = '';
            queueEmptyState.classList.remove('hidden');
            return;
        }

        queueEmptyState.classList.add('hidden');
        queueList.classList.remove('hidden');

        // Build queue HTML
        let html = '';
        queue.forEach((item, index) => {
            const position = index + 1;
            const duration = formatDuration(item.durationSeconds || 0);
            html += `
                <li class="flex items-center gap-3 p-2 bg-bg-tertiary rounded-lg group" data-queue-position="${position}">
                    <span class="w-5 h-5 flex items-center justify-center text-xs text-text-tertiary font-medium">
                        ${position}
                    </span>
                    <div class="flex-1 min-w-0">
                        <p class="text-sm text-text-primary truncate">${escapeHtml(item.name)}</p>
                        <p class="text-xs text-text-tertiary">${duration}</p>
                    </div>
                    <button type="button"
                            class="skip-queue-btn p-1 text-text-tertiary hover:text-accent-blue opacity-0 group-hover:opacity-100 transition-all"
                            data-position="${position}"
                            title="Skip to next">
                        <svg class="w-4 h-4" fill="currentColor" viewBox="0 0 24 24">
                            <path d="M6 18l8.5-6L6 6v12zM16 6v12h2V6h-2z"/>
                        </svg>
                    </button>
                </li>
            `;
        });

        queueList.innerHTML = html;
    }

    /**
     * Formats a duration in seconds to a display string.
     * @param {number} seconds - Duration in seconds.
     * @returns {string} Formatted string (e.g., "1:30" or "1:02:30").
     */
    function formatDuration(seconds) {
        const totalSeconds = Math.floor(seconds);
        const hours = Math.floor(totalSeconds / 3600);
        const minutes = Math.floor((totalSeconds % 3600) / 60);
        const secs = totalSeconds % 60;

        if (hours > 0) {
            return `${hours}:${minutes.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`;
        }
        return `${minutes}:${secs.toString().padStart(2, '0')}`;
    }

    /**
     * Escapes HTML characters to prevent XSS.
     * @param {string} text - Text to escape.
     * @returns {string} Escaped text.
     */
    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    /**
     * Sets the channel selector loading state.
     * @param {boolean} loading - Whether loading.
     */
    function setChannelSelectorLoading(loading) {
        if (channelSelector) {
            channelSelector.disabled = loading;
        }
    }

    /**
     * Sets the leave button loading state.
     * @param {boolean} loading - Whether loading.
     */
    function setLeaveButtonLoading(loading) {
        if (leaveButton) {
            leaveButton.disabled = loading;
            if (loading) {
                leaveButton.innerHTML = `
                    <svg class="w-4 h-4 animate-spin" fill="none" viewBox="0 0 24 24">
                        <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                        <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
                    </svg>
                    Leaving...
                `;
            } else {
                leaveButton.innerHTML = `
                    <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1" />
                    </svg>
                    Leave
                `;
            }
        }
    }

    /**
     * Shows a toast notification.
     * @param {string} message - Toast message.
     * @param {string} type - Toast type (success, error, info, warning).
     */
    function showToast(message, type) {
        // Use global Toast if available
        if (typeof Toast !== 'undefined' && Toast.show) {
            Toast.show(message, type);
        } else {
            console.log(`[VoiceChannelPanel] Toast (${type}):`, message);
        }
    }

    // Public API
    return {
        init: init,
        updateConnectionStatus: updateConnectionStatus,
        updateNowPlaying: updateNowPlaying,
        updateQueue: updateQueue
    };
})();

// Initialize when DOM is ready
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', VoiceChannelPanel.init);
} else {
    VoiceChannelPanel.init();
}
