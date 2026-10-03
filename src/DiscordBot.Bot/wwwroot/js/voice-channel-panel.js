/**
 * Voice Channel Panel
 *
 * Shows whether the bot is in a voice channel, lets the person join, leave and stop, and shows
 * what is playing. One panel, two ways to talk to the server:
 *
 *  - Admin pages (Guilds/Soundboard, Guilds/TextToSpeech): the Viewer-gated
 *    /api/guilds/{id}/audio endpoints, with live updates from the DashboardHub.
 *  - Member portal pages: the portal's own endpoints, named by the panel's `data-api-base`
 *    (e.g. /api/portal/soundboard/{id}). Portal members hold no Identity role, so the hub
 *    refuses them; the panel reads GET {apiBase}/status instead, after every action and every
 *    few seconds while the page is visible.
 *
 * "Connected" always means the bot is in a voice channel, never that the page's own connection
 * to the server is up. That comes from the server (data-connected on load, then join/leave
 * answers, status reads and hub events). The state of the hub only decides whether the
 * "live updates paused" note shows.
 *
 * Other page scripts listen for `voicepanel:change` on `document`
 * (detail: { isConnected, channelId, isPlaying, busy }) and may call
 * VoiceChannelPanel.notePlaying(name) when they start something.
 */
const VoiceChannelPanel = (function() {
    'use strict';

    const POLL_INTERVAL_MS = 5000;

    // DOM element references
    let panelElement = null;
    let channelSelector = null;
    let leaveButton = null;
    let stopButton = null;
    let barStopButton = null;
    let connectionStatusBadge = null;
    let connectionStatusDot = null;
    let connectionStatusText = null;
    let connectedChannelInfo = null;
    let connectedChannelName = null;
    let channelMemberCount = null;
    let channelMemberLabel = null;
    let mobileStatus = null;
    let mobileLabel = null;
    let statusNote = null;
    let nowPlayingSection = null;
    let nowPlayingName = null;
    let nowPlayingRequestedBy = null;
    let nowPlayingProgress = null;
    let nowPlayingPosition = null;
    let nowPlayingDuration = null;
    let queueList = null;
    let queueEmptyState = null;
    let queueCountBadge = null;

    // State: what the server last told us. Rendering reads only this.
    let guildId = null;
    let apiBase = null;          // set on portal pages
    let state = {
        isConnected: false,
        channelId: null,
        channelName: '',
        memberCount: null,
        isPlaying: false,
        nowPlaying: null,        // { name, requestedByDisplayName, source, durationSeconds, positionSeconds }
        busy: null,              // 'joining' | 'leaving' | 'stopping' | null
        hubState: null,          // admin pages only: 'connected' | 'reconnecting' | ...
        statusFailed: false      // portal pages: the last status read failed
    };
    let pollTimer = null;
    let statusInFlight = null;

    // ------------------------------------------------------------------
    // Init
    // ------------------------------------------------------------------

    function init() {
        panelElement = document.getElementById('voice-channel-panel');
        if (!panelElement) {
            return;
        }

        guildId = panelElement.dataset.guildId;
        apiBase = panelElement.dataset.apiBase || null;

        channelSelector = document.getElementById('channel-selector');
        leaveButton = document.getElementById('leave-channel-btn');
        stopButton = document.getElementById('stop-playback-btn');
        barStopButton = document.getElementById('voice-bar-stop-btn');
        connectionStatusBadge = document.getElementById('connection-status-badge');
        connectionStatusDot = document.getElementById('connection-status-dot');
        connectionStatusText = document.getElementById('connection-status-text');
        connectedChannelInfo = document.getElementById('connected-channel-info');
        connectedChannelName = document.getElementById('connected-channel-name');
        channelMemberCount = document.getElementById('channel-member-count');
        channelMemberLabel = document.getElementById('channel-member-label');
        mobileStatus = document.getElementById('voice-panel-mobile-status');
        mobileLabel = document.getElementById('voice-panel-mobile-label');
        statusNote = document.getElementById('voice-status-note');
        nowPlayingSection = document.getElementById('now-playing-section');
        nowPlayingName = document.getElementById('now-playing-name');
        nowPlayingRequestedBy = document.getElementById('now-playing-requested-by');
        nowPlayingProgress = document.getElementById('now-playing-progress');
        nowPlayingPosition = document.getElementById('now-playing-position');
        nowPlayingDuration = document.getElementById('now-playing-duration');
        queueList = document.getElementById('queue-list');
        queueEmptyState = document.getElementById('queue-empty-state');
        queueCountBadge = document.getElementById('queue-count-badge');

        // What the server rendered
        state.isConnected = panelElement.dataset.connected === 'true';
        state.channelId = panelElement.dataset.channelId || null;
        state.channelName = connectedChannelName ? connectedChannelName.textContent.trim() : '';
        const renderedCount = channelMemberCount ? parseInt(channelMemberCount.textContent, 10) : NaN;
        state.memberCount = Number.isFinite(renderedCount) ? renderedCount : null;
        state.isPlaying = !!(nowPlayingSection && !nowPlayingSection.classList.contains('hidden'));

        setupEventListeners();

        if (apiBase) {
            startPolling();
        } else if (typeof DashboardHub !== 'undefined') {
            setupSignalRHandlers();
        }

        render();
    }

    function setupEventListeners() {
        if (channelSelector) {
            channelSelector.addEventListener('change', handleChannelSelect);
        }
        if (leaveButton) {
            leaveButton.addEventListener('click', handleLeaveChannel);
        }
        [stopButton, barStopButton].forEach(function(button) {
            if (button) button.addEventListener('click', handleStopPlayback);
        });

        // Queue skip buttons (delegated). Admin pages only: skipping other people's queued sounds
        // is not something a member can do.
        if (queueList) {
            queueList.addEventListener('click', function(e) {
                const skipBtn = e.target.closest('.skip-queue-btn');
                if (skipBtn) {
                    handleSkipQueueItem(parseInt(skipBtn.dataset.position, 10));
                }
            });
        }

        // The voice bar is a disclosure on phones and tablets
        panelElement.addEventListener('click', function(e) {
            if (e.target.closest('[data-voice-panel-toggle]')) {
                toggleBody();
            }
        });

        document.addEventListener('visibilitychange', function() {
            if (!apiBase) return;
            if (document.visibilityState === 'visible') {
                refreshStatus();
                startPolling();
            } else {
                stopPolling();
            }
        });
    }

    // ------------------------------------------------------------------
    // Server calls
    // ------------------------------------------------------------------

    function endpoint(action, arg) {
        if (apiBase) {
            switch (action) {
                case 'join': return { url: apiBase + '/channel', method: 'POST' };
                case 'leave': return { url: apiBase + '/channel', method: 'DELETE' };
                case 'stop': return { url: apiBase + '/stop', method: 'POST' };
                case 'status': return { url: apiBase + '/status', method: 'GET' };
            }
        }
        const base = '/api/guilds/' + guildId + '/audio';
        switch (action) {
            case 'join': return { url: base + '/join/' + arg, method: 'POST' };
            case 'leave': return { url: base + '/leave', method: 'POST' };
            case 'stop': return { url: base + '/stop', method: 'POST' };
            case 'skip': return { url: base + '/queue/' + arg, method: 'DELETE' };
        }
        return null;
    }

    /**
     * Sends a voice command. Channel IDs are Discord snowflakes, too big for a JavaScript number,
     * so the portal's JSON body is written as text with the digits untouched.
     */
    function send(action, arg, errorMessage) {
        const target = endpoint(action, arg);
        const options = { method: target.method, errorMessage: errorMessage };
        if (apiBase && action === 'join') {
            options.body = '{"channelId":' + String(arg).replace(/\D/g, '') + '}';
            options.headers = { 'Content-Type': 'application/json' };
        }
        return ApiClient.request(target.url, options);
    }

    async function handleChannelSelect(e) {
        const selectedChannelId = e.target.value;
        if (!guildId) return;

        if (!selectedChannelId) {
            // "-- Select a channel --" while connected means leave
            if (state.isConnected && !state.busy) handleLeaveChannel();
            return;
        }
        if (state.busy || selectedChannelId === state.channelId) return;

        const selectedOption = e.target.selectedOptions && e.target.selectedOptions[0];
        const optionName = selectedOption ? selectedOption.textContent.replace(/\s*\(\d+\)\s*$/, '').trim() : '';

        setBusy('joining');
        try {
            await send('join', selectedChannelId, 'Could not join the voice channel. Try again.');
            // The server answered after joining: that is the confirmation. Apply it now rather
            // than wait for a live event that may never come (members have no hub).
            state.isConnected = true;
            state.channelId = selectedChannelId;
            state.channelName = optionName || state.channelName;
            state.statusFailed = false;
            setBusy(null);
            announce('Joined ' + (state.channelName || 'the voice channel'));
            if (apiBase) refreshStatus();
        } catch (error) {
            setBusy(null);
            reportError(error, 'Could not join the voice channel. Try again.');
            // Put the picker back on what is true
            if (channelSelector) channelSelector.value = state.channelId || '';
        }
    }

    async function handleLeaveChannel() {
        if (!guildId || state.busy) return;

        setBusy('leaving');
        try {
            await send('leave', null, 'Could not leave the voice channel. Try again.');
            setBusy(null);
            applyDisconnectedState();
            announce('Left the voice channel');
        } catch (error) {
            setBusy(null);
            if (error && error.status === 400 && error.data && error.data.errorCode === 'not_connected') {
                // Already out (someone else removed the bot): the screen should say so
                applyDisconnectedState();
                return;
            }
            reportError(error, 'Could not leave the voice channel. Try again.');
        }
    }

    async function handleStopPlayback() {
        if (!guildId || state.busy) return;

        setBusy('stopping');
        try {
            await send('stop', null, 'Could not stop playback. Try again.');
            state.isPlaying = false;
            state.nowPlaying = null;
            setBusy(null);
            if (apiBase) refreshStatus();
        } catch (error) {
            setBusy(null);
            reportError(error, 'Could not stop playback. Try again.');
        }
    }

    async function handleSkipQueueItem(position) {
        if (!guildId || apiBase) return;

        try {
            await send('skip', position, 'Could not skip that item. Try again.');
            // Success is shown by the QueueUpdated event
        } catch (error) {
            reportError(error, 'Could not skip that item. Try again.');
        }
    }

    /**
     * Reads the bot's real voice state from the portal status endpoint and applies it.
     * Portal pages only. Overlapping calls share one request.
     */
    function refreshStatus() {
        if (!apiBase) return Promise.resolve(null);
        if (statusInFlight) return statusInFlight;

        statusInFlight = ApiClient.getRaw(endpoint('status').url, { timeout: 8000 })
            .then(function(result) {
                if (!result.ok || !result.data) {
                    state.statusFailed = true;
                    render();
                    return null;
                }
                applyStatus(result.data);
                return result.data;
            })
            .catch(function() {
                state.statusFailed = true;
                render();
                return null;
            })
            .finally(function() { statusInFlight = null; });
        return statusInFlight;
    }

    function applyStatus(status) {
        state.statusFailed = false;
        state.isConnected = !!status.isConnected;
        state.channelId = status.isConnected && status.channelId ? String(status.channelId) : null;
        if (status.isConnected) {
            if (status.channelName) state.channelName = status.channelName;
            state.memberCount = typeof status.memberCount === 'number' ? status.memberCount : state.memberCount;
        } else {
            state.channelName = '';
            state.memberCount = null;
        }
        const wasPlaying = state.isPlaying;
        state.isPlaying = !!status.isPlaying;
        if (!state.isPlaying) {
            state.nowPlaying = null;
        } else if (!state.nowPlaying) {
            state.nowPlaying = { name: status.nowPlaying || 'Audio is playing', source: 'Soundboard' };
        }
        render();
        if (wasPlaying && !state.isPlaying) {
            // Playback ended while nobody was looking: let the page clear its "playing" marks
            document.dispatchEvent(new CustomEvent('voicepanel:playbackended'));
        }
    }

    function startPolling() {
        stopPolling();
        if (!apiBase || document.visibilityState === 'hidden') return;
        pollTimer = setInterval(refreshStatus, POLL_INTERVAL_MS);
    }

    function stopPolling() {
        if (pollTimer) {
            clearInterval(pollTimer);
            pollTimer = null;
        }
    }

    // ------------------------------------------------------------------
    // Live updates (admin pages)
    // ------------------------------------------------------------------

    function setupSignalRHandlers() {
        DashboardHub.onStateChange(function(change) {
            state.hubState = change.state;
            render();
            if (change.state === 'connected') {
                joinGuildAudioGroup();
            }
        });

        DashboardHub.on('AudioConnected', handleAudioConnected);
        DashboardHub.on('AudioDisconnected', handleAudioDisconnected);
        DashboardHub.on('PlaybackStarted', handlePlaybackStarted);
        DashboardHub.on('PlaybackProgress', handlePlaybackProgress);
        DashboardHub.on('PlaybackFinished', handlePlaybackFinished);
        DashboardHub.on('QueueUpdated', handleQueueUpdated);

        state.hubState = DashboardHub.getConnectionState();
        if (state.hubState === 'connected') {
            joinGuildAudioGroup();
        }
    }

    async function joinGuildAudioGroup() {
        if (!guildId) return;
        try {
            await DashboardHub.joinGuildAudioGroup(guildId);
            // Events missed while the hub was away: ask what is true now
            if (typeof DashboardHub.getCurrentAudioStatus === 'function') {
                const status = await DashboardHub.getCurrentAudioStatus(guildId);
                if (status) syncFromHubStatus(status);
            }
        } catch (error) {
            console.error('[VoiceChannelPanel] Failed to join guild audio group:', error);
        }
    }

    function syncFromHubStatus(status) {
        if (typeof status.isConnected === 'boolean' && !state.busy) {
            state.isConnected = status.isConnected;
            if (!status.isConnected) {
                state.channelId = null;
                state.channelName = '';
                state.memberCount = null;
            } else {
                if (status.channelId) state.channelId = String(status.channelId);
                if (status.channelName) state.channelName = status.channelName;
                if (typeof status.memberCount === 'number') state.memberCount = status.memberCount;
            }
        }
        if (typeof status.isPlaying === 'boolean' && !status.isPlaying) {
            state.isPlaying = false;
            state.nowPlaying = null;
        }
        render();
    }

    function handleAudioConnected(data) {
        if (String(data.guildId) !== guildId) return;
        state.isConnected = true;
        state.channelId = data.channelId ? String(data.channelId) : state.channelId;
        state.channelName = data.channelName || state.channelName;
        if (typeof data.memberCount === 'number') state.memberCount = data.memberCount;
        setBusy(null);
    }

    function handleAudioDisconnected(data) {
        if (String(data.guildId) !== guildId) return;
        applyDisconnectedState();
    }

    function handlePlaybackStarted(data) {
        if (String(data.guildId) !== guildId) return;
        state.isPlaying = true;
        state.nowPlaying = {
            id: data.soundId,
            name: data.name,
            durationSeconds: data.durationSeconds,
            positionSeconds: 0,
            requestedByDisplayName: data.requestedByDisplayName,
            source: data.source || 'Soundboard'
        };
        render();
    }

    function handlePlaybackProgress(data) {
        if (String(data.guildId) !== guildId) return;
        updatePlaybackProgress(data.positionSeconds, data.durationSeconds);
    }

    function handlePlaybackFinished(data) {
        if (String(data.guildId) !== guildId) return;
        state.isPlaying = false;
        state.nowPlaying = null;
        render();
    }

    function handleQueueUpdated(data) {
        if (String(data.guildId) !== guildId) return;
        updateQueue(data.queue || []);
    }

    /**
     * Resets the panel to "not in a voice channel". Idempotent: called from the AudioDisconnected
     * event, after a successful leave, and when a status read says the bot is out.
     */
    function applyDisconnectedState() {
        state.isConnected = false;
        state.channelId = null;
        state.channelName = '';
        state.memberCount = null;
        state.isPlaying = false;
        state.nowPlaying = null;
        setBusy(null);
        updateQueue([]);
    }

    // ------------------------------------------------------------------
    // Rendering: everything on screen comes from `state`
    // ------------------------------------------------------------------

    function setBusy(busy) {
        state.busy = busy;
        render();
    }

    function statusLabel() {
        if (state.busy === 'joining') return 'Joining…';
        if (state.busy === 'leaving') return 'Leaving…';
        return state.isConnected ? 'Connected' : 'Disconnected';
    }

    function render() {
        if (!panelElement) return;

        const label = statusLabel();
        const tone = state.busy ? 'busy' : (state.isConnected ? 'connected' : 'disconnected');

        // Badge and its dot
        if (connectionStatusText) connectionStatusText.textContent = label;
        if (connectionStatusBadge) {
            connectionStatusBadge.className = 'inline-flex items-center gap-1.5 px-2 py-0.5 text-xs font-medium rounded-full ' +
                (tone === 'connected' ? 'bg-success/20 text-success'
                    : tone === 'busy' ? 'bg-warning/20 text-warning'
                    : 'bg-bg-tertiary text-text-secondary');
        }
        if (connectionStatusDot) {
            connectionStatusDot.className = 'w-1.5 h-1.5 rounded-full ' +
                (tone === 'connected' ? 'bg-success' : tone === 'busy' ? 'bg-warning animate-pulse' : 'bg-text-tertiary');
        }

        // The phone bar: always visible, so it carries the same truth in fewer words
        if (mobileStatus) {
            mobileStatus.textContent = label;
            mobileStatus.className = 'voice-panel-mobile-status ' + tone;
        }
        if (mobileLabel) {
            mobileLabel.textContent = state.isConnected && state.channelName ? state.channelName : 'Voice channel';
        }

        // Channel name and head count
        if (connectedChannelInfo) {
            const show = state.isConnected && !!state.channelName;
            connectedChannelInfo.classList.toggle('hidden', !show);
            if (show) {
                if (connectedChannelName) connectedChannelName.textContent = state.channelName;
                if (channelMemberCount) {
                    channelMemberCount.textContent = state.memberCount === null ? '0' : String(state.memberCount);
                }
                if (channelMemberLabel) {
                    const count = state.memberCount === null ? 0 : state.memberCount;
                    channelMemberLabel.textContent = count === 1 ? 'member' : 'members';
                }
            }
        }

        // Picker and Leave
        if (channelSelector) {
            channelSelector.disabled = !!state.busy || channelSelector.options.length <= 1;
            channelSelector.value = state.isConnected && state.channelId ? state.channelId : '';
            channelSelector.setAttribute('aria-busy', state.busy === 'joining' ? 'true' : 'false');
        }
        if (leaveButton) {
            leaveButton.classList.toggle('hidden', !state.isConnected);
            leaveButton.disabled = !!state.busy;
            const leaveText = leaveButton.querySelector('.leave-label');
            if (leaveText) leaveText.textContent = state.busy === 'leaving' ? 'Leaving…' : 'Leave';
        }

        // Now playing and Stop
        updateNowPlaying(state.isPlaying ? (state.nowPlaying || { name: 'Audio is playing' }) : null);
        [stopButton, barStopButton].forEach(function(button) {
            if (!button) return;
            button.disabled = state.busy === 'stopping';
        });
        if (barStopButton) barStopButton.classList.toggle('hidden', !(state.isPlaying && state.isConnected));

        // A note when the picture may be out of date
        if (statusNote) {
            let note = '';
            if (apiBase && state.statusFailed) {
                note = "Can't check the voice channel right now. Retrying…";
            } else if (!apiBase && state.hubState && state.hubState !== 'connected') {
                note = 'Live updates are paused. Reconnecting…';
            }
            statusNote.textContent = note;
            statusNote.classList.toggle('hidden', !note);
        }

        // Data attributes other scripts read
        panelElement.dataset.connected = state.isConnected ? 'true' : 'false';
        panelElement.dataset.channelId = state.isConnected && state.channelId ? state.channelId : '';
        panelElement.dataset.playing = state.isPlaying ? 'true' : 'false';
        panelElement.dataset.busy = state.busy || '';

        document.dispatchEvent(new CustomEvent('voicepanel:change', {
            detail: { isConnected: state.isConnected, channelId: state.channelId, isPlaying: state.isPlaying, busy: state.busy }
        }));
    }

    function updateNowPlaying(nowPlaying) {
        if (!nowPlayingSection) return;

        if (nowPlaying) {
            nowPlayingSection.classList.remove('hidden');

            if (nowPlayingName) {
                nowPlayingName.textContent = nowPlaying.name;
                nowPlayingName.title = nowPlaying.name;
            }

            var iconContainer = nowPlayingSection.querySelector('.now-playing-icon');
            if (iconContainer) {
                const isTts = nowPlaying.source === 'TTS';
                iconContainer.innerHTML = isTts
                    ? '<svg class="w-5 h-5" fill="currentColor" viewBox="0 0 24 24" aria-hidden="true"><path d="M20 2H4c-1.1 0-2 .9-2 2v18l4-4h14c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2z"/></svg>'
                    : '<svg class="w-5 h-5" fill="currentColor" viewBox="0 0 24 24" aria-hidden="true"><path d="M12 3v10.55c-.59-.34-1.27-.55-2-.55-2.21 0-4 1.79-4 4s1.79 4 4 4 4-1.79 4-4V7h4V3h-6z"/></svg>';
                iconContainer.className = 'w-10 h-10 rounded-lg flex items-center justify-center flex-shrink-0 now-playing-icon ' +
                    (isTts ? 'bg-accent-purple/20 text-accent-purple' : 'bg-accent-blue/20 text-accent-blue');
            }

            if (nowPlayingRequestedBy) {
                if (nowPlaying.requestedByDisplayName) {
                    nowPlayingRequestedBy.textContent = 'Requested by ' + nowPlaying.requestedByDisplayName;
                    nowPlayingRequestedBy.classList.remove('hidden');
                } else {
                    nowPlayingRequestedBy.textContent = '';
                    nowPlayingRequestedBy.classList.add('hidden');
                }
            }

            updatePlaybackProgress(nowPlaying.positionSeconds || 0, nowPlaying.durationSeconds || 0);
        } else {
            nowPlayingSection.classList.add('hidden');
        }
    }

    function updatePlaybackProgress(position, duration) {
        // Progress elements may not exist (ShowProgress = false)
        if (nowPlayingProgress) {
            const percent = duration > 0 ? Math.round((position / duration) * 100) : 0;
            nowPlayingProgress.style.width = percent + '%';
        }
        if (nowPlayingPosition) nowPlayingPosition.textContent = formatDuration(position);
        if (nowPlayingDuration) nowPlayingDuration.textContent = formatDuration(duration);
    }

    function updateQueue(queue) {
        if (!queueList || !queueEmptyState || !queueCountBadge) return;

        const count = queue.length;
        queueCountBadge.textContent = count > 0
            ? (window.Format && Format.plural ? Format.plural(count, 'sound') : count + (count === 1 ? ' sound' : ' sounds'))
            : 'Empty';

        if (count === 0) {
            queueList.classList.add('hidden');
            queueList.innerHTML = '';
            queueEmptyState.classList.remove('hidden');
            return;
        }

        queueEmptyState.classList.add('hidden');
        queueList.classList.remove('hidden');

        let html = '';
        queue.forEach(function(item, index) {
            const position = index + 1;
            html += '<li class="flex items-center gap-3 p-2 bg-bg-tertiary rounded-lg group" data-queue-position="' + position + '">' +
                '<span class="w-5 h-5 flex items-center justify-center text-xs text-text-tertiary font-medium">' + position + '</span>' +
                '<div class="flex-1 min-w-0">' +
                '<p class="text-sm text-text-primary truncate">' + escapeHtml(item.name) + '</p>' +
                '<p class="text-xs text-text-tertiary">' + formatDuration(item.durationSeconds || 0) + '</p>' +
                '</div>' +
                '<button type="button" class="skip-queue-btn p-1 text-text-tertiary hover:text-accent-blue row-actions" ' +
                'data-position="' + position + '" title="Skip to next" aria-label="Skip ' + escapeHtml(item.name) + '">' +
                '<svg class="w-4 h-4" fill="currentColor" viewBox="0 0 24 24" aria-hidden="true"><path d="M6 18l8.5-6L6 6v12zM16 6v12h2V6h-2z"/></svg>' +
                '</button></li>';
        });
        queueList.innerHTML = html;
    }

    function formatDuration(seconds) {
        const totalSeconds = Math.floor(seconds);
        const hours = Math.floor(totalSeconds / 3600);
        const minutes = Math.floor((totalSeconds % 3600) / 60);
        const secs = totalSeconds % 60;

        if (hours > 0) {
            return hours + ':' + String(minutes).padStart(2, '0') + ':' + String(secs).padStart(2, '0');
        }
        return minutes + ':' + String(secs).padStart(2, '0');
    }

    function escapeHtml(text) {
        if (window.SafeHtml && SafeHtml.escape) return SafeHtml.escape(text);
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML.replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    // ------------------------------------------------------------------
    // Feedback
    // ------------------------------------------------------------------

    function reportError(error, fallback) {
        const message = (error && error.message) || fallback;
        if (window.toast && toast.error) {
            toast.error(message, { key: 'voice-panel-error' });
        }
    }

    /** Says a state change politely to screen readers; the badge alone is silent. */
    function announce(message) {
        if (window.toast && toast.info) {
            toast.info(message, { duration: 2500, key: 'voice-panel-note' });
        }
    }

    // ------------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------------

    /**
     * A page tells the panel what it just started, so "Now playing" names it. The next status
     * read keeps it only while the server says something is still playing.
     */
    function notePlaying(name, source) {
        if (!panelElement) return;
        state.isPlaying = true;
        state.nowPlaying = { name: name, source: source || 'Soundboard' };
        render();
        if (apiBase) {
            // A short sound can be over before the next poll; look soon
            setTimeout(refreshStatus, 1500);
        }
    }

    /** Folds or opens the controls under the voice bar (phones and tablets). */
    function toggleBody(force) {
        const body = document.getElementById('voice-panel-collapsible-body');
        const chevron = document.getElementById('voicePanelChevron');
        const toggle = document.getElementById('voice-panel-mobile-toggle');
        if (!body || !toggle) return;

        const open = typeof force === 'boolean' ? force : !body.classList.contains('expanded');
        body.classList.toggle('expanded', open);
        if (chevron) chevron.classList.toggle('expanded', open);
        toggle.setAttribute('aria-expanded', open ? 'true' : 'false');
    }

    /**
     * Brings the channel picker into view and focuses it. A page calls this when the person tries
     * to play without a voice channel: the answer to "join one first" is the picker, not a toast.
     */
    function reveal() {
        if (!panelElement || !channelSelector) return;
        toggleBody(true);
        panelElement.scrollIntoView({ block: 'nearest', behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth' });
        // The body is hidden until its open transition starts; focus once it is shown
        setTimeout(function() { channelSelector.focus({ preventScroll: true }); }, 50);
    }

    function getState() {
        return Object.assign({}, state);
    }

    return {
        init: init,
        refresh: refreshStatus,
        notePlaying: notePlaying,
        reveal: reveal,
        toggle: toggleBody,
        getState: getState,
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
