(function() {
    'use strict';

    // ========================================
    // Configuration
    // ========================================
    const CONFIG = {
        STATUS_POLL_INTERVAL: 3000,        // 3 seconds
        MAX_MESSAGE_LENGTH: 200,
        CHARACTER_WARNING_THRESHOLD: 0.9,   // 90%
        SPEED_MIN: 0.5,
        SPEED_MAX: 2.0,
        SPEED_DEFAULT: 1.0,
        PITCH_MIN: 0.5,
        PITCH_MAX: 2.0,
        PITCH_DEFAULT: 1.0
    };

    // ========================================
    // API Endpoints
    // ========================================
    const API = {
        status: (guildId) => `/api/portal/tts/${guildId}/status`,
        send: (guildId) => `/api/portal/tts/${guildId}/send`,
        joinChannel: (guildId) => `/api/portal/tts/${guildId}/channel`,
        leaveChannel: (guildId) => `/api/portal/tts/${guildId}/channel`,
        stop: (guildId) => `/api/portal/tts/${guildId}/stop`
    };

    // ========================================
    // State
    // ========================================
    let guildId = null;                    // CRITICAL: Always string, never parse to number
    let statusPollTimer = null;
    let isConnected = false;
    let isPlaying = false;
    let currentMessage = null;
    let selectedChannel = null;

    // ========================================
    // Initialization
    // ========================================
    function init() {
        // Get guild ID from data attribute on page
        const guildIdElement = document.querySelector('[data-guild-id]');
        if (!guildIdElement) {
            console.log('[PortalTTS] Guild ID element not found, skipping initialization');
            return;
        }

        guildId = guildIdElement.dataset.guildId;
        if (!guildId) {
            console.log('[PortalTTS] No guild ID provided');
            return;
        }

        console.log('[PortalTTS] Initializing for guild:', guildId);

        setupEventHandlers();
        startStatusPolling();
    }

    // ========================================
    // Event Handlers Setup
    // ========================================
    function setupEventHandlers() {
        // Message input - character counter and validation
        const messageInput = document.getElementById('ttsMessage');
        if (messageInput) {
            messageInput.addEventListener('input', updateCharacterCount);
            messageInput.addEventListener('keypress', function(e) {
                if (e.key === 'Enter' && !e.shiftKey) {
                    e.preventDefault();
                    sendTtsMessage();
                }
            });
        }

        // Send button
        const sendBtn = document.getElementById('sendBtn');
        if (sendBtn) {
            sendBtn.addEventListener('click', sendTtsMessage);
        }

        // Channel selection
        const channelSelect = document.getElementById('channelSelect');
        if (channelSelect) {
            channelSelect.addEventListener('change', function() {
                selectedChannel = this.value;
            });
        }

        // Voice channel controls
        const joinBtn = document.getElementById('joinBtn');
        if (joinBtn) {
            joinBtn.addEventListener('click', joinChannel);
        }

        const leaveBtn = document.getElementById('leaveBtn');
        if (leaveBtn) {
            leaveBtn.addEventListener('click', leaveChannel);
        }

        // Stop button
        const stopBtn = document.getElementById('stopBtn');
        if (stopBtn) {
            stopBtn.addEventListener('click', stopPlayback);
        }

        // Voice selection
        const voiceSelect = document.getElementById('voiceSelect');
        if (voiceSelect) {
            voiceSelect.addEventListener('change', function() {
                // Voice selection change - no immediate action needed
            });
        }

        // Slider value displays
        const speedSlider = document.getElementById('speedSlider');
        if (speedSlider) {
            speedSlider.addEventListener('input', updateSpeedDisplay);
        }

        const pitchSlider = document.getElementById('pitchSlider');
        if (pitchSlider) {
            pitchSlider.addEventListener('input', updatePitchDisplay);
        }
    }

    // ========================================
    // Status Polling (every 3 seconds)
    // ========================================
    function startStatusPolling() {
        statusPollTimer = setInterval(pollStatus, CONFIG.STATUS_POLL_INTERVAL);
        // Poll immediately on start
        pollStatus();
    }

    function stopStatusPolling() {
        if (statusPollTimer) {
            clearInterval(statusPollTimer);
            statusPollTimer = null;
        }
    }

    async function pollStatus() {
        if (!guildId) return;

        try {
            const response = await fetch(API.status(guildId));
            if (!response.ok) {
                console.error('[PortalTTS] Status poll failed:', response.statusText);
                return;
            }

            const data = await response.json();

            // Update connection state if changed
            if (data.isConnected !== isConnected) {
                isConnected = data.isConnected;
                console.log('[PortalTTS] Connection state changed:', isConnected);
                updateConnectionUI(isConnected);

                if (!isConnected) {
                    // Bot disconnected externally
                    isPlaying = false;
                    currentMessage = null;
                    updateNowPlayingUI(null);
                }
            }

            // Update playing state if changed
            if (data.isPlaying && data.currentMessage) {
                if (!isPlaying || currentMessage !== data.currentMessage) {
                    isPlaying = true;
                    currentMessage = data.currentMessage;
                    console.log('[PortalTTS] Now playing:', currentMessage);
                    updateNowPlayingUI(data.currentMessage);
                }
            } else if (!data.isPlaying && isPlaying) {
                isPlaying = false;
                currentMessage = null;
                console.log('[PortalTTS] Playback finished');
                updateNowPlayingUI(null);
            }
        } catch (error) {
            console.error('[PortalTTS] Status poll error:', error);
        }
    }

    // ========================================
    // Send TTS Message
    // ========================================
    async function sendTtsMessage() {
        const messageInput = document.getElementById('ttsMessage');
        const message = messageInput.value.trim();

        if (!message) {
            showToast('error', 'Please enter a message');
            return;
        }

        if (message.length > CONFIG.MAX_MESSAGE_LENGTH) {
            showToast('error', `Message exceeds maximum length of ${CONFIG.MAX_MESSAGE_LENGTH} characters`);
            return;
        }

        if (!isConnected) {
            showToast('warning', 'Please join a voice channel first!');
            highlightChannelSelector();
            return;
        }

        const voice = document.getElementById('voiceSelect').value;
        const speed = parseFloat(document.getElementById('speedSlider').value) || CONFIG.SPEED_DEFAULT;
        const pitch = parseFloat(document.getElementById('pitchSlider').value) || CONFIG.PITCH_DEFAULT;

        // Disable send button and show loading
        const sendBtn = document.getElementById('sendBtn');
        const originalText = sendBtn.textContent;
        const originalHtml = sendBtn.innerHTML;
        sendBtn.disabled = true;
        sendBtn.innerHTML = `
            <svg class="inline-block animate-spin mr-2" fill="none" viewBox="0 0 24 24" style="width: 16px; height: 16px;">
                <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
            </svg>
            Sending...
        `;

        try {
            const response = await fetch(API.send(guildId), {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({ message, voice, speed, pitch })
            });

            if (response.status === 429) {
                const data = await response.json();
                showToast('warning', data.message || 'Rate limit exceeded. Please wait.');
                return;
            }

            if (!response.ok) {
                const errorData = await response.json().catch(() => ({}));
                throw new Error(errorData.message || 'Failed to send message');
            }

            // Clear textarea on success
            messageInput.value = '';
            updateCharacterCount();
            showToast('success', 'Message sent successfully');
        } catch (error) {
            console.error('[PortalTTS] Send error:', error);
            showToast('error', error.message);
        } finally {
            sendBtn.disabled = false;
            sendBtn.textContent = originalText;
            sendBtn.innerHTML = originalHtml;
        }
    }

    // ========================================
    // Voice Channel Controls
    // ========================================
    async function joinChannel() {
        if (!selectedChannel) {
            showToast('warning', 'Please select a voice channel first!');
            highlightChannelSelector();
            return;
        }

        const joinBtn = document.getElementById('joinBtn');
        const originalHtml = joinBtn.innerHTML;
        joinBtn.disabled = true;
        joinBtn.innerHTML = `
            <svg class="inline-block animate-spin mr-2" fill="none" viewBox="0 0 24 24" style="width: 16px; height: 16px;">
                <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
            </svg>
            Joining...
        `;

        try {
            const response = await fetch(API.joinChannel(guildId), {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ channelId: selectedChannel })
            });

            if (!response.ok) {
                const errorData = await response.json().catch(() => ({}));
                throw new Error(errorData.message || 'Failed to join channel');
            }

            isConnected = true;
            updateConnectionUI(true);
            showToast('success', 'Connected to voice channel');
        } catch (error) {
            console.error('[PortalTTS] Join error:', error);
            showToast('error', error.message);
        } finally {
            joinBtn.disabled = isConnected;
            joinBtn.innerHTML = originalHtml;
        }
    }

    async function leaveChannel() {
        const leaveBtn = document.getElementById('leaveBtn');
        const originalHtml = leaveBtn.innerHTML;
        leaveBtn.disabled = true;
        leaveBtn.innerHTML = `
            <svg class="inline-block animate-spin mr-2" fill="none" viewBox="0 0 24 24" style="width: 16px; height: 16px;">
                <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
            </svg>
            Leaving...
        `;

        try {
            const response = await fetch(API.leaveChannel(guildId), {
                method: 'DELETE',
                headers: { 'Content-Type': 'application/json' }
            });

            if (!response.ok) {
                const errorData = await response.json().catch(() => ({}));
                throw new Error(errorData.message || 'Failed to leave channel');
            }

            isConnected = false;
            isPlaying = false;
            currentMessage = null;

            updateConnectionUI(false);
            updateNowPlayingUI(null);

            const channelSelect = document.getElementById('channelSelect');
            if (channelSelect) {
                channelSelect.value = '';
            }

            showToast('info', 'Disconnected from voice channel');
        } catch (error) {
            console.error('[PortalTTS] Leave error:', error);
            showToast('error', error.message);
        } finally {
            leaveBtn.disabled = !isConnected;
            leaveBtn.innerHTML = originalHtml;
        }
    }

    async function stopPlayback() {
        if (!isPlaying) return;

        try {
            const response = await fetch(API.stop(guildId), {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' }
            });

            if (!response.ok) {
                const errorData = await response.json().catch(() => ({}));
                throw new Error(errorData.message || 'Failed to stop playback');
            }

            isPlaying = false;
            currentMessage = null;
            updateNowPlayingUI(null);
        } catch (error) {
            console.error('[PortalTTS] Stop error:', error);
            showToast('error', error.message);
        }
    }

    // ========================================
    // UI Updates
    // ========================================
    function updateCharacterCount() {
        const messageInput = document.getElementById('ttsMessage');
        const count = messageInput.value.length;
        const charCount = document.getElementById('charCount');
        const charCounter = document.getElementById('charCounter');
        const sendBtn = document.getElementById('sendBtn');

        if (charCount) {
            charCount.textContent = count;
        }

        // Color coding
        if (charCounter) {
            if (count >= CONFIG.MAX_MESSAGE_LENGTH) {
                charCounter.style.color = '#ef4444'; // Red - over limit
            } else if (count >= CONFIG.MAX_MESSAGE_LENGTH * CONFIG.CHARACTER_WARNING_THRESHOLD) {
                charCounter.style.color = '#fbbf24'; // Orange warning
            } else {
                charCounter.style.color = '#949ba4'; // Normal gray
            }
        }

        // Update send button state (disabled if empty or not connected)
        if (sendBtn) {
            sendBtn.disabled = count === 0 || !isConnected || count > CONFIG.MAX_MESSAGE_LENGTH;
        }
    }

    function updateConnectionUI(connected) {
        const statusBadge = document.getElementById('connectionStatus');
        const joinBtn = document.getElementById('joinBtn');
        const leaveBtn = document.getElementById('leaveBtn');
        const messageInput = document.getElementById('ttsMessage');

        if (statusBadge) {
            if (connected) {
                statusBadge.className = 'status-badge-voice connected';
                statusBadge.innerHTML = '<span class="status-dot"></span><span>Connected</span>';
            } else {
                statusBadge.className = 'status-badge-voice disconnected';
                statusBadge.innerHTML = '<span class="status-dot"></span><span>Disconnected</span>';
            }
        }

        if (joinBtn) {
            joinBtn.disabled = connected;
        }

        if (leaveBtn) {
            leaveBtn.disabled = !connected;
        }

        // Update message input disabled state
        if (messageInput) {
            messageInput.disabled = !connected;
        }

        updateCharacterCount(); // Update send button state
    }

    function updateNowPlayingUI(message) {
        const nowPlayingContent = document.getElementById('nowPlayingContent');
        const nowPlayingEmpty = document.getElementById('nowPlayingEmpty');
        const nowPlayingMessage = document.getElementById('nowPlayingMessage');

        if (message) {
            if (nowPlayingMessage) {
                // CRITICAL: Use textContent to prevent XSS
                const displayMessage = message.length > 100 ? message.substring(0, 100) + '...' : message;
                nowPlayingMessage.textContent = displayMessage;
            }

            if (nowPlayingContent) {
                nowPlayingContent.classList.remove('hidden');
            }
            if (nowPlayingEmpty) {
                nowPlayingEmpty.classList.add('hidden');
            }
        } else {
            if (nowPlayingContent) {
                nowPlayingContent.classList.add('hidden');
            }
            if (nowPlayingEmpty) {
                nowPlayingEmpty.classList.remove('hidden');
            }
        }
    }

    function updateSpeedDisplay() {
        const speedSlider = document.getElementById('speedSlider');
        const speedValue = document.getElementById('speedValue');

        if (speedSlider && speedValue) {
            const value = parseFloat(speedSlider.value) || CONFIG.SPEED_DEFAULT;
            speedValue.textContent = value.toFixed(1) + 'x';
        }
    }

    function updatePitchDisplay() {
        const pitchSlider = document.getElementById('pitchSlider');
        const pitchValue = document.getElementById('pitchValue');

        if (pitchSlider && pitchValue) {
            const value = parseFloat(pitchSlider.value) || CONFIG.PITCH_DEFAULT;
            pitchValue.textContent = value.toFixed(1) + 'x';
        }
    }

    function highlightChannelSelector() {
        const channelSelect = document.getElementById('channelSelect');
        if (!channelSelect) return;

        channelSelect.style.borderColor = '#fbbf24';
        channelSelect.style.boxShadow = '0 0 0 2px rgba(251, 191, 36, 0.2)';

        setTimeout(() => {
            channelSelect.style.borderColor = '';
            channelSelect.style.boxShadow = '';
        }, 3000);
    }

    // ========================================
    // Toast Notifications
    // ========================================
    function showToast(type, message) {
        const container = document.getElementById('portalToastContainer');
        if (!container) {
            console.warn('[PortalTTS] Toast container not found');
            return;
        }

        const icons = {
            success: '<svg class="portal-toast-icon" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>',
            error: '<svg class="portal-toast-icon" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 14l2-2m0 0l2-2m-2 2l-2-2m2 2l2 2m7-2a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>',
            warning: '<svg class="portal-toast-icon" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" /></svg>',
            info: '<svg class="portal-toast-icon" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>'
        };

        const toast = document.createElement('div');
        toast.className = `portal-toast ${type}`;
        toast.innerHTML = `
            ${icons[type] || icons.info}
            <span class="portal-toast-message"></span>
            <button class="portal-toast-close" aria-label="Dismiss">
                <svg fill="none" viewBox="0 0 24 24" stroke="currentColor" style="width: 16px; height: 16px;">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
                </svg>
            </button>
        `;

        // CRITICAL: Use textContent to prevent XSS
        toast.querySelector('.portal-toast-message').textContent = message;

        // Close button handler
        toast.querySelector('.portal-toast-close').addEventListener('click', () => dismissToast(toast));

        container.appendChild(toast);

        // Auto dismiss after 5 seconds
        setTimeout(() => dismissToast(toast), 5000);
    }

    function dismissToast(toast) {
        if (!toast || !toast.parentNode) return;

        toast.classList.add('dismissing');
        setTimeout(() => {
            if (toast.parentNode) {
                toast.remove();
            }
        }, 200);
    }

    // ========================================
    // Cleanup on page unload
    // ========================================
    window.addEventListener('beforeunload', function() {
        stopStatusPolling();
    });

    // ========================================
    // Initialize when DOM is ready
    // ========================================
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    // Export public API for testing/debugging
    window.PortalTTS = {
        init: init,
        pollStatus: pollStatus,
        updateConnectionUI: updateConnectionUI,
        updateNowPlayingUI: updateNowPlayingUI,
        showToast: showToast
    };

    console.log('[PortalTTS] Module loaded');
})();
