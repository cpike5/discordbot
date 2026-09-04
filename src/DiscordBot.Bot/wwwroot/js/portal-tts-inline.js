// portal-tts-inline.js
// Extracted from Pages/Portal/TTS/Index.cshtml — small page-local behaviours that don't
// belong in the shared portal-tts.js module. Config is provided by window.portalTtsInlineConfig,
// set from server-rendered values in Index.cshtml.
(function () {
    'use strict';

    // Discord snowflake IDs are 64-bit; always treat as strings in JS.
    window.guildId = window.portalTtsInlineConfig && window.portalTtsInlineConfig.guildId;

    // Collapsible voice settings toggle (mobile only)
    function toggleVoiceSettings() {
        const header = document.querySelector('.voice-controls-collapsible-header');
        const content = document.getElementById('voiceSettingsContent');
        const chevron = document.getElementById('voiceSettingsChevron');
        const isExpanded = content.classList.contains('expanded');

        if (isExpanded) {
            content.classList.remove('expanded');
            chevron.classList.remove('expanded');
            header.setAttribute('aria-expanded', 'false');
        } else {
            content.classList.add('expanded');
            chevron.classList.add('expanded');
            header.setAttribute('aria-expanded', 'true');
        }
    }
    window.toggleVoiceSettings = toggleVoiceSettings;

    // Connect to SignalR hub
    document.addEventListener('DOMContentLoaded', async function () {
        if (typeof DashboardHub !== 'undefined') {
            await DashboardHub.connect();
        }
    });
})();

// portal-tts-shortcuts.js
// Extracted from Pages/Portal/TTS/Index.cshtml — keyboard shortcuts for the TTS portal page.
(function () {
    'use strict';
    // ========================================
    // Keyboard Shortcuts (TTS)
    // ========================================
    if (typeof KeyboardShortcuts === 'undefined') return;

    // Ctrl+Enter: Send TTS message
    KeyboardShortcuts.register('Enter', 'Send TTS message', function () {
        var sendBtn = document.getElementById('sendBtn');
        if (sendBtn && !sendBtn.disabled) {
            sendBtn.click();
        }
    }, { ctrlKey: true, category: 'Text-to-Speech' });

    // /: Focus message input
    KeyboardShortcuts.register('/', 'Focus message input', function () {
        var input = document.getElementById('ttsMessage');
        if (input) {
            input.focus();
        }
    }, { category: 'Text-to-Speech' });

    KeyboardShortcuts.init();
})();
