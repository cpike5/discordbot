(function() {
    'use strict';

    // ========================================
    // Configuration
    // ========================================
    const CONFIG = {
        STATUS_POLL_INTERVAL: 3000,        // 3 seconds
        CHARACTER_WARNING_THRESHOLD: 0.9,   // 90%
        SPEED_MIN: 0.5,
        SPEED_MAX: 2.0,
        SPEED_DEFAULT: 1.0,
        PITCH_MIN: 0.5,
        PITCH_MAX: 2.0,
        PITCH_DEFAULT: 1.0,
        STORAGE_KEY_VOICE: 'tts_selected_voice'  // localStorage key for voice persistence
    };

    // ========================================
    // API Endpoints
    // ========================================
    const API = {
        status: (guildId) => `/api/portal/tts/${guildId}/status`,
        send: (guildId) => `/api/portal/tts/${guildId}/send`,
        joinChannel: (guildId) => `/api/portal/tts/${guildId}/channel`,
        leaveChannel: (guildId) => `/api/portal/tts/${guildId}/channel`,
        stop: (guildId) => `/api/portal/tts/${guildId}/stop`,
        voiceCapabilities: (voiceName) => `/api/portal/tts/voices/${voiceName}/capabilities`,
        validateSsml: () => `/api/portal/tts/validate-ssml`,
        buildSsml: () => `/api/portal/tts/build-ssml`
    };

    // ========================================
    // State
    // ========================================
    let guildId = null;                    // CRITICAL: Always string, never parse to number
    let statusPollTimer = null;
    let isConnected = false;
    let isPlaying = false;
    let isSending = false;                 // Track if a message is currently being sent
    let currentMessage = null;
    let selectedChannel = null;
    let maxMessageLength = 500;            // Dynamic max length from server (default: 500)

    // SSML state
    let currentMode = 'standard';
    let currentStyle = '';
    let currentStyleIntensity = 1.0;
    let currentSsml = '';
    let formattedTextState = null;
    let ssmlDebounceTimer = null;

    // ========================================
    // Initialization
    // ========================================
    function init() {
        // Get guild ID from data attribute on page (preferred) or window.guildId (fallback)
        const guildIdElement = document.querySelector('[data-guild-id]');
        if (guildIdElement) {
            guildId = guildIdElement.dataset.guildId;
        } else if (window.guildId) {
            guildId = window.guildId;
            console.log('[PortalTTS] Using window.guildId fallback');
        }

        if (!guildId) {
            console.log('[PortalTTS] No guild ID provided, skipping initialization');
            return;
        }

        console.log('[PortalTTS] Initializing for guild:', guildId);

        setupEventHandlers();
        loadSavedVoice();
        loadSavedMode();
        startStatusPolling();
    }

    // ========================================
    // Event Handlers Setup
    // ========================================
    function setupEventHandlers() {
        // Message input - character counter and validation
        const messageInput = document.getElementById('ttsMessage');
        if (messageInput) {
            messageInput.addEventListener('input', function() {
                updateCharacterCount();
                // Debounced SSML preview for Pro mode
                if (currentMode === 'pro') {
                    clearTimeout(ssmlDebounceTimer);
                    ssmlDebounceTimer = setTimeout(buildSsmlFromCurrentState, 500);
                }
            });
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
                // Save selected voice to localStorage
                saveSelectedVoice(this.value);
                // Reload styles for new voice
                if (window.styleSelector_loadStyles) {
                    window.styleSelector_loadStyles('portalStyleSelector', this.value);
                }
                // Rebuild SSML if in Pro mode
                if (currentMode === 'pro') {
                    buildSsmlFromCurrentState();
                }
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
    // Voice Persistence
    // ========================================
    function loadSavedVoice() {
        try {
            const savedVoice = localStorage.getItem(CONFIG.STORAGE_KEY_VOICE);
            if (savedVoice) {
                const voiceSelect = document.getElementById('voiceSelect');
                if (voiceSelect) {
                    // Check if the saved voice still exists in the dropdown
                    const option = voiceSelect.querySelector(`option[value="${savedVoice}"]`);
                    if (option) {
                        voiceSelect.value = savedVoice;
                        console.log('[PortalTTS] Restored saved voice:', savedVoice);
                    }
                }
            }
        } catch (error) {
            console.warn('[PortalTTS] Failed to load saved voice:', error);
        }
    }

    function saveSelectedVoice(voice) {
        try {
            if (voice) {
                localStorage.setItem(CONFIG.STORAGE_KEY_VOICE, voice);
                console.log('[PortalTTS] Saved voice preference:', voice);
            }
        } catch (error) {
            console.warn('[PortalTTS] Failed to save voice:', error);
        }
    }

    // ========================================
    // Mode Persistence
    // ========================================
    function loadSavedMode() {
        try {
            const savedMode = localStorage.getItem('tts_mode_preference');
            if (savedMode && ['simple', 'standard', 'pro'].includes(savedMode)) {
                currentMode = savedMode;
                console.log('[PortalTTS] Restored saved mode:', savedMode);
            }
        } catch (error) {
            console.warn('[PortalTTS] Failed to load saved mode:', error);
        }

        // Always apply mode visibility on init (use requestAnimationFrame to ensure
        // DOM is settled after component inline scripts have executed)
        requestAnimationFrame(() => {
            window.portalHandleModeChange(currentMode);
        });
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

            // Update max message length if provided by server
            if (data.maxMessageLength && data.maxMessageLength !== maxMessageLength) {
                maxMessageLength = data.maxMessageLength;
                console.log('[PortalTTS] Max message length updated:', maxMessageLength);
                updateCharacterCount(); // Update UI with new limit
            }

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
        if (!messageInput) {
            console.error('[PortalTTS] Message input element not found');
            return;
        }
        const message = messageInput.value.trim();

        if (!message) {
            showToast('error', 'Please enter a message');
            return;
        }

        if (message.length > maxMessageLength) {
            showToast('error', `Message exceeds maximum length of ${maxMessageLength} characters`);
            return;
        }

        if (!isConnected) {
            showToast('warning', 'Please join a voice channel first!');
            highlightChannelSelector();
            return;
        }

        const voice = document.getElementById('voiceSelect').value;
        if (!voice) {
            showToast('warning', 'Please select a voice first!');
            return;
        }
        const speed = parseFloat(document.getElementById('speedSlider').value) || CONFIG.SPEED_DEFAULT;
        const pitch = parseFloat(document.getElementById('pitchSlider').value) || CONFIG.PITCH_DEFAULT;

        // Mark as sending to prevent duplicate submissions
        isSending = true;

        // Clear textarea immediately so user can start typing next message
        messageInput.value = '';
        updateCharacterCount();

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
                body: JSON.stringify({
                    message,
                    voice,
                    speed,
                    pitch,
                    ...(currentMode === 'standard' && currentStyle ? { style: currentStyle, styleIntensity: currentStyleIntensity } : {}),
                    ...(currentMode === 'pro' && currentSsml ? { ssml: currentSsml } : {})
                })
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

            showToast('success', 'Message sent successfully');
        } catch (error) {
            console.error('[PortalTTS] Send error:', error);
            showToast('error', error.message);
        } finally {
            isSending = false;
            sendBtn.innerHTML = originalHtml;
            updateCharacterCount(); // Re-evaluate button state based on current input
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
        if (!isPlaying) {
            return;
        }

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
        if (!messageInput) return;
        const count = messageInput.value.length;
        const charCount = document.getElementById('charCount');
        const charMax = document.getElementById('charMax');
        const maxLengthLabel = document.getElementById('maxLengthLabel');
        const charCounter = document.getElementById('charCounter');
        const sendBtn = document.getElementById('sendBtn');
        const messageTextarea = document.getElementById('ttsMessage');

        if (charCount) {
            charCount.textContent = count;
        }

        // Update max length displays
        if (charMax) {
            charMax.textContent = maxMessageLength;
        }
        if (maxLengthLabel) {
            maxLengthLabel.textContent = maxMessageLength;
        }
        if (messageTextarea) {
            messageTextarea.setAttribute('maxlength', maxMessageLength);
        }

        // Color coding
        if (charCounter) {
            if (count >= maxMessageLength) {
                charCounter.style.color = '#ef4444'; // Red - over limit
            } else if (count >= maxMessageLength * CONFIG.CHARACTER_WARNING_THRESHOLD) {
                charCounter.style.color = '#fbbf24'; // Orange warning
            } else {
                charCounter.style.color = '#949ba4'; // Normal gray
            }
        }

        // Update send button state (disabled if empty, not connected, over limit, or currently sending)
        if (sendBtn) {
            sendBtn.disabled = count === 0 || !isConnected || count > maxMessageLength || isSending;
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
    // SSML Support Functions
    // ========================================

    /**
     * Build SSML from current state (Pro mode only)
     */
    async function buildSsmlFromCurrentState() {
        if (currentMode !== 'pro') return;

        const messageInput = document.getElementById('ttsMessage');
        const voiceSelect = document.getElementById('voiceSelect');
        const speedSlider = document.getElementById('speedSlider');
        const pitchSlider = document.getElementById('pitchSlider');

        const message = messageInput?.value?.trim() || '';
        const voice = voiceSelect?.value || '';
        const speed = parseFloat(speedSlider?.value || '1.0');
        const pitch = parseFloat(pitchSlider?.value || '1.0');

        if (!message || !voice) {
            currentSsml = '';
            if (window.ssmlPreview_update) {
                window.ssmlPreview_update('portalSsmlPreview', '', 0);
            }
            return;
        }

        try {
            // Build elements array from formatted text markers (emphasis, breaks, etc.)
            // EmphasisToolbar markers use { start, end, type, level?, interpretAs?, duration? }
            // Backend SsmlElement expects { type, text?, attributes: {} }
            const plainText = formattedTextState?.plain || message;
            const elements = (formattedTextState?.markers || []).map(m => {
                switch (m.type) {
                    case 'emphasis':
                        return {
                            type: 'emphasis',
                            text: plainText.substring(m.start, m.end),
                            attributes: { level: m.level || 'moderate' }
                        };
                    case 'say-as':
                        return {
                            type: 'say-as',
                            text: plainText.substring(m.start, m.end),
                            attributes: { 'interpret-as': m.interpretAs || 'cardinal' }
                        };
                    case 'pause':
                        return {
                            type: 'break',
                            text: null,
                            attributes: { duration: (m.duration || 500) + 'ms' }
                        };
                    default:
                        return { type: m.type, text: null, attributes: {} };
                }
            });

            // Payload must match SsmlBuildRequest: { language, segments[] }
            // Each segment: { voice, style, rate, pitch, text, elements[] }
            const payload = {
                language: 'en-US',
                segments: [{
                    voice: voice,
                    style: currentStyle || null,
                    rate: speed !== CONFIG.SPEED_DEFAULT ? speed : null,
                    pitch: pitch !== CONFIG.PITCH_DEFAULT ? pitch : null,
                    text: message,
                    elements: elements
                }]
            };

            const response = await fetch('/api/portal/tts/build-ssml', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            });

            if (response.ok) {
                const data = await response.json();
                currentSsml = data.ssml;
                if (window.ssmlPreview_update) {
                    window.ssmlPreview_update('portalSsmlPreview', currentSsml, message.length);
                }
            }
        } catch (error) {
            console.error('[PortalTTS] Error building SSML:', error);
        }
    }

    // ========================================
    // Window-Level Callbacks for Shared Components
    // ========================================

    /**
     * Handle mode changes from ModeSelector component
     */
    window.portalHandleModeChange = function(mode) {
        currentMode = mode;
        const presetBar = document.getElementById('portalPresetBarContainer');
        const styleSelector = document.getElementById('portalStyleSelectorContainer');
        const emphasisToolbar = document.getElementById('portalEmphasisToolbarContainer');
        const ssmlPreview = document.getElementById('portalSsmlPreviewContainer');

        if (mode === 'simple') {
            presetBar?.classList.add('hidden');
            styleSelector?.classList.add('hidden');
            emphasisToolbar?.classList.add('hidden');
            ssmlPreview?.classList.add('hidden');
        } else if (mode === 'standard') {
            presetBar?.classList.remove('hidden');
            styleSelector?.classList.remove('hidden');
            emphasisToolbar?.classList.add('hidden');
            ssmlPreview?.classList.add('hidden');
        } else if (mode === 'pro') {
            presetBar?.classList.remove('hidden');
            styleSelector?.classList.remove('hidden');
            emphasisToolbar?.classList.remove('hidden');
            ssmlPreview?.classList.remove('hidden');
            buildSsmlFromCurrentState();
        }
    };

    /**
     * Handle preset application from PresetBar component
     */
    window.portalHandlePresetApply = function(presetData) {
        const voiceSelect = document.getElementById('voiceSelect');
        if (voiceSelect && presetData.voice) {
            voiceSelect.value = presetData.voice;
        }

        const speedSlider = document.getElementById('speedSlider');
        const speedValue = document.getElementById('speedValue');
        if (speedSlider && presetData.speed != null) {
            speedSlider.value = presetData.speed;
            if (speedValue) speedValue.textContent = parseFloat(presetData.speed).toFixed(1) + 'x';
        }

        const pitchSlider = document.getElementById('pitchSlider');
        const pitchValue = document.getElementById('pitchValue');
        if (pitchSlider && presetData.pitch != null) {
            pitchSlider.value = presetData.pitch;
            if (pitchValue) pitchValue.textContent = parseFloat(presetData.pitch).toFixed(1) + 'x';
        }

        if (presetData.style) {
            currentStyle = presetData.style;
            // Set the select value directly and sync the StyleSelector UI
            const styleSelect = document.getElementById('portalStyleSelector-select');
            if (styleSelect) {
                styleSelect.value = presetData.style;
                if (window.styleSelector_onStyleChange) {
                    window.styleSelector_onStyleChange('portalStyleSelector');
                }
            }
        }

        showToast('success', 'Applied "' + presetData.name + '" preset');
    };

    /**
     * Handle style changes from StyleSelector component
     */
    window.portalHandleStyleChange = function(style) {
        currentStyle = style;
        if (currentMode === 'pro') buildSsmlFromCurrentState();
    };

    /**
     * Handle intensity changes from StyleSelector component
     */
    window.portalHandleIntensityChange = function(intensity) {
        currentStyleIntensity = intensity;
        if (currentMode === 'pro') buildSsmlFromCurrentState();
    };

    /**
     * Handle format changes from EmphasisToolbar component
     */
    window.portalHandleFormatChange = function(formattedText) {
        formattedTextState = formattedText;
        buildSsmlFromCurrentState();
    };

    /**
     * Handle SSML copy from SsmlPreview component
     */
    window.portalHandleSsmlCopy = function() {
        showToast('success', 'SSML copied to clipboard');
    };

    /**
     * Handle pause insertion from EmphasisToolbar component
     */
    window.portalHandlePauseInsert = function(duration) {
        if (currentMode === 'pro') buildSsmlFromCurrentState();
    };

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
