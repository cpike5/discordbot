(function() {
    'use strict';

    // ========================================
    // Configuration
    // ========================================
    const CONFIG = {
        CHARACTER_WARNING_THRESHOLD: 0.9,   // 90%
        SPEED_MIN: 0.5,
        SPEED_MAX: 2.0,
        SPEED_DEFAULT: 1.0,
        PITCH_MIN: 0.5,
        PITCH_MAX: 2.0,
        PITCH_DEFAULT: 1.0,
        STORAGE_KEY_VOICE: 'tts_selected_voice',  // localStorage key for voice persistence
        DRAFT_DEBOUNCE_MS: 2000                    // 2-second debounce for draft auto-save
    };

    // ========================================
    // API Endpoints
    // ========================================
    const API = {
        send: (guildId) => `/api/portal/tts/${guildId}/send`,
        preview: (guildId) => `/api/portal/tts/${guildId}/preview`,
        voiceCapabilities: (voiceName) => `/api/portal/tts/voices/${voiceName}/capabilities`,
        validateSsml: () => `/api/portal/tts/validate-ssml`,
        buildSsml: () => `/api/portal/tts/build-ssml`,
        customPresets: (guildId) => `/api/portal/tts/${guildId}/presets/custom`
    };

    // ========================================
    // State
    // ========================================
    let guildId = null;                    // CRITICAL: Always string, never parse to number
    let isSending = false;                 // Track if a message is currently being sent
    let isPreviewing = false;              // Track if a preview is currently playing
    let selectedChannel = null;
    let maxMessageLength = 500;            // Dynamic max length from server (default: 500)

    // SSML state
    let currentMode = 'standard';
    let currentStyle = '';
    let currentStyleIntensity = 1.0;
    let currentSsml = '';
    // formattedTextState removed - SSML builder parses visual markers directly from textarea
    let ssmlDebounceTimer = null;
    let draftDebounceTimer = null;
    let isInitializing = false;

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
        }

        if (!guildId) {
            return;
        }

        // Initialize unified preferences with background server sync
        if (window.UserPreferences) {
            window.UserPreferences.init(guildId);
        }

        isInitializing = true;
        setupEventHandlers();
        loadSavedVoice();
        loadSavedMode();
        loadDraft();
        isInitializing = false;
        observeConnectionState();
        loadCustomPresets();
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
                    ssmlDebounceTimer = setTimeout(buildSsmlFromCurrentState, 250);
                }
                saveDraftDebounced();
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

        // Preview button
        const previewBtn = document.getElementById('previewBtn');
        if (previewBtn) {
            previewBtn.addEventListener('click', previewTtsMessage);
        }

        // Clear draft button
        const clearDraftBtn = document.getElementById('clearDraftBtn');
        if (clearDraftBtn) {
            clearDraftBtn.addEventListener('click', function() {
                const messageInput = document.getElementById('ttsMessage');
                if (messageInput) {
                    messageInput.value = '';
                    updateCharacterCount();
                }
                clearDraft();
            });
        }

        // Note: Voice channel join/leave are handled by voice-channel-panel.js
        // Note: Stop button is now handled by voice-channel-panel.js

        // Voice selection - handled by VoiceSelector component via portalHandleVoiceChange callback

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
            var savedVoice = window.UserPreferences
                ? window.UserPreferences.get(CONFIG.STORAGE_KEY_VOICE)
                : localStorage.getItem(CONFIG.STORAGE_KEY_VOICE);
            if (savedVoice && window.voiceSelector_setValue) {
                window.voiceSelector_setValue('portalVoiceSelector', savedVoice, true);
            }
        } catch (error) {
            // Failed to load saved voice
        }
    }

    function saveSelectedVoice(voice) {
        try {
            if (voice) {
                if (window.UserPreferences) {
                    window.UserPreferences.set(CONFIG.STORAGE_KEY_VOICE, voice);
                } else {
                    localStorage.setItem(CONFIG.STORAGE_KEY_VOICE, voice);
                }
            }
        } catch (error) {
            // Failed to save voice
        }
    }

    // ========================================
    // Mode Persistence
    // ========================================
    function loadSavedMode() {
        try {
            var savedMode = window.UserPreferences
                ? window.UserPreferences.get('tts_mode_preference')
                : localStorage.getItem('tts_mode_preference');
            if (savedMode && ['simple', 'standard', 'pro'].includes(savedMode)) {
                currentMode = savedMode;
            }
        } catch (error) {
            // Failed to load saved mode
        }

        // Always apply mode visibility on init (use requestAnimationFrame to ensure
        // DOM is settled after component inline scripts have executed)
        requestAnimationFrame(() => {
            window.portalHandleModeChange(currentMode);
        });
    }

    // ========================================
    // Draft Persistence
    // ========================================
    function getDraftKey() {
        return `portal:tts:draft:${guildId}`;
    }

    function saveDraft() {
        if (isInitializing) return;
        try {
            const messageInput = document.getElementById('ttsMessage');
            const voice = window.voiceSelector_getValue ? window.voiceSelector_getValue('portalVoiceSelector') : null;
            const draft = {
                message: messageInput?.value || '',
                voice: voice || '',
                mode: currentMode,
                timestamp: Date.now()
            };
            // Only save if there's an actual message
            if (draft.message) {
                localStorage.setItem(getDraftKey(), JSON.stringify(draft));
            }
        } catch (error) {
            // Failed to save draft
        }
    }

    function saveDraftDebounced() {
        clearTimeout(draftDebounceTimer);
        draftDebounceTimer = setTimeout(saveDraft, CONFIG.DRAFT_DEBOUNCE_MS);
    }

    function loadDraft() {
        try {
            const raw = localStorage.getItem(getDraftKey());
            if (!raw) return;

            const draft = JSON.parse(raw);
            // Only restore if there's an actual message
            if (!draft || !draft.message) return;

            // Restore message
            const messageInput = document.getElementById('ttsMessage');
            if (messageInput) {
                messageInput.value = draft.message;
                updateCharacterCount();
            }

            // Restore voice (if saved and different from current)
            if (draft.voice && window.voiceSelector_setValue) {
                window.voiceSelector_setValue('portalVoiceSelector', draft.voice, true);
            }

            // Restore mode (if saved)
            if (draft.mode && ['simple', 'standard', 'pro'].includes(draft.mode)) {
                currentMode = draft.mode;
                // Re-apply mode UI. Use requestAnimationFrame to ensure DOM is settled.
                requestAnimationFrame(() => {
                    window.portalHandleModeChange(currentMode);
                });
            }

            // Show draft restored indicator
            showDraftBanner();
        } catch (error) {
            // Failed to load draft
        }
    }

    function clearDraft() {
        try {
            localStorage.removeItem(getDraftKey());
        } catch (error) {
            // Failed to clear draft
        }
        hideDraftBanner();
    }

    function showDraftBanner() {
        const banner = document.getElementById('draftRestoredBanner');
        if (banner) {
            banner.classList.remove('hidden');
        }
    }

    function hideDraftBanner() {
        const banner = document.getElementById('draftRestoredBanner');
        if (banner) {
            banner.classList.add('hidden');
        }
    }

    // ========================================
    // Connection State Observer
    // ========================================
    function observeConnectionState() {
        const panel = document.getElementById('voice-channel-panel');
        if (!panel) {
            return;
        }

        // Initial state
        updateCharacterCount();

        // Watch for data-connected attribute changes
        const observer = new MutationObserver(function(mutations) {
            mutations.forEach(function(mutation) {
                if (mutation.type === 'attributes' && mutation.attributeName === 'data-connected') {
                    updateCharacterCount(); // Update send button state
                }
            });
        });

        observer.observe(panel, {
            attributes: true,
            attributeFilter: ['data-connected']
        });

    }

    function checkIsConnected() {
        const panel = document.getElementById('voice-channel-panel');
        return panel?.dataset.connected === 'true';
    }

    // ========================================
    // Request Body Builder
    // ========================================

    /**
     * Build the common TTS request body from the current form state.
     * Shared by both send and preview flows.
     */
    function buildTtsRequestBody() {
        const messageInput = document.getElementById('ttsMessage');
        const message = messageInput?.value?.trim() || '';
        const voice = window.voiceSelector_getValue ? window.voiceSelector_getValue('portalVoiceSelector') : null;
        const speed = parseFloat(document.getElementById('speedSlider').value) || CONFIG.SPEED_DEFAULT;
        const pitch = parseFloat(document.getElementById('pitchSlider').value) || CONFIG.PITCH_DEFAULT;

        return {
            message,
            voice,
            speed,
            pitch,
            ...(currentMode === 'standard' && currentStyle ? { style: currentStyle, styleIntensity: currentStyleIntensity } : {}),
            ...(currentMode === 'pro' && currentSsml ? { ssml: currentSsml } : {})
        };
    }

    // ========================================
    // Preview TTS Message
    // ========================================
    async function previewTtsMessage() {
        const messageInput = document.getElementById('ttsMessage');
        if (!messageInput) return;

        const message = messageInput.value.trim();
        if (!message) {
            showToast('error', 'Please enter a message');
            return;
        }

        if (message.length > maxMessageLength) {
            showToast('error', `Message exceeds maximum length of ${maxMessageLength} characters`);
            return;
        }

        const voice = window.voiceSelector_getValue ? window.voiceSelector_getValue('portalVoiceSelector') : null;
        if (!voice) {
            showToast('warning', 'Please select a voice first!');
            return;
        }

        if (isPreviewing) return;
        isPreviewing = true;

        const previewBtn = document.getElementById('previewBtn');
        const originalHtml = previewBtn.innerHTML;
        previewBtn.disabled = true;
        previewBtn.innerHTML = `
            <svg class="inline-block animate-spin" fill="none" viewBox="0 0 24 24" style="width: 16px; height: 16px;">
                <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
            </svg>
            Loading...
        `;

        try {
            const body = buildTtsRequestBody();
            const response = await fetch(API.preview(guildId), {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(body)
            });

            if (response.status === 429) {
                const data = await response.json().catch(() => ({}));
                showToast('warning', data.message || 'Rate limit exceeded. Please wait.');
                return;
            }

            if (!response.ok) {
                const errorData = await response.json().catch(() => ({}));
                throw new Error(errorData.message || 'Failed to generate preview');
            }

            const blob = await response.blob();
            const url = URL.createObjectURL(blob);
            const audio = new Audio(url);
            audio.addEventListener('ended', () => URL.revokeObjectURL(url));
            audio.addEventListener('error', () => URL.revokeObjectURL(url));
            audio.play();
        } catch (error) {
            showToast('error', error.message);
        } finally {
            isPreviewing = false;
            previewBtn.innerHTML = originalHtml;
            previewBtn.disabled = false;
        }
    }

    // ========================================
    // Send TTS Message
    // ========================================
    async function sendTtsMessage() {
        const messageInput = document.getElementById('ttsMessage');
        if (!messageInput) {
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

        if (!checkIsConnected()) {
            showToast('warning', 'Please join a voice channel first!');
            highlightChannelSelector();
            return;
        }

        const voice = window.voiceSelector_getValue ? window.voiceSelector_getValue('portalVoiceSelector') : null;
        if (!voice) {
            showToast('warning', 'Please select a voice first!');
            return;
        }

        // Mark as sending to prevent duplicate submissions
        isSending = true;

        // Capture request body BEFORE clearing the textarea
        const body = buildTtsRequestBody();

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
                body: JSON.stringify(body)
            });

            if (response.status === 429) {
                const data = await response.json().catch(() => ({}));
                showToast('warning', data.message || 'Rate limit exceeded. Please wait.');
                return;
            }

            if (!response.ok) {
                const errorData = await response.json().catch(() => ({}));
                throw new Error(errorData.message || 'Failed to send message');
            }

            showToast('success', 'Message sent successfully');
            clearDraft();
        } catch (error) {
            // Send error occurred
            showToast('error', error.message);
        } finally {
            isSending = false;
            sendBtn.innerHTML = originalHtml;
            updateCharacterCount(); // Re-evaluate button state based on current input
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
            sendBtn.disabled = count === 0 || !checkIsConnected() || count > maxMessageLength || isSending;
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
    // Toast Notifications (delegates to shared ToastManager from toast.js)
    // ========================================
    function showToast(type, message) {
        ToastManager.show(type, message);
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
        const speedSlider = document.getElementById('speedSlider');
        const pitchSlider = document.getElementById('pitchSlider');

        const message = messageInput?.value?.trim() || '';
        const voice = window.voiceSelector_getValue ? window.voiceSelector_getValue('portalVoiceSelector') : '';
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
            // Parse visual markers directly from textarea text (single source of truth).
            const elements = window.SsmlMarkers.parseMarkers(message);

            // Payload: text is null, all content is interleaved in elements array
            const payload = {
                language: 'en-US',
                segments: [{
                    voice: voice,
                    style: currentStyle || null,
                    rate: speed !== CONFIG.SPEED_DEFAULT ? speed : null,
                    pitch: pitch !== CONFIG.PITCH_DEFAULT ? pitch : null,
                    text: null,
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
            // Error building SSML
        }
    }

    // ========================================
    // Custom Presets
    // ========================================

    /**
     * Load custom presets from the API via the PresetBar component's loader.
     * The actual rendering is handled by _PresetBar.cshtml's presetBar_loadCustomPresets.
     */
    function loadCustomPresets() {
        if (window.presetBar_loadCustomPresets) {
            window.presetBar_loadCustomPresets('presetBar');
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

        // Save mode change immediately
        try {
            if (window.UserPreferences) {
                window.UserPreferences.set('tts_mode_preference', mode);
            } else {
                localStorage.setItem('tts_mode_preference', mode);
            }
        } catch(e) {}
        saveDraft();
    };

    /**
     * Handle voice changes from VoiceSelector component
     */
    window.portalHandleVoiceChange = function(voiceValue) {
        saveSelectedVoice(voiceValue);
        saveDraft();
        if (window.styleSelector_loadStyles) {
            window.styleSelector_loadStyles('portalStyleSelector', voiceValue);
        }
        if (currentMode === 'pro') {
            buildSsmlFromCurrentState();
        }
    };

    /**
     * Handle preset application from PresetBar component
     */
    window.portalHandlePresetApply = function(presetData) {
        if (presetData.voice && window.voiceSelector_setValue) {
            window.voiceSelector_setValue('portalVoiceSelector', presetData.voice, true);
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

    // portalHandleFormatChange removed - SSML builder parses textarea directly.
    // The toolbar fires the textarea 'input' event which triggers the existing debounced rebuild.

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
        showToast: showToast,
        clearDraft: clearDraft
    };

})();
