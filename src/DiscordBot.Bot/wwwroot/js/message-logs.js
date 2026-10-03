/**
 * Message Logs Page JavaScript
 * Handles autocomplete initialization and channel/guild dependency logic.
 */
(function() {
    'use strict';

    const CHANNEL_SEARCH_INPUT_ID = 'ChannelId-search';
    const CHANNEL_HIDDEN_INPUT_ID = 'ChannelId';
    // The guild filter's hidden input; the channel autocomplete reads it through guildIdSource
    const GUILD_HIDDEN_INPUT_ID = 'MessageGuildId';
    const GUILD_SEARCH_INPUT_ID = 'MessageGuildId-search';

    /**
     * Updates the channel input state based on guild selection.
     */
    function updateChannelInputState() {
        const guildHiddenInput = document.getElementById(GUILD_HIDDEN_INPUT_ID);
        const channelSearchInput = document.getElementById(CHANNEL_SEARCH_INPUT_ID);

        if (!channelSearchInput) return;

        const hasGuildSelected = guildHiddenInput && guildHiddenInput.value;

        if (hasGuildSelected) {
            channelSearchInput.disabled = false;
            channelSearchInput.placeholder = 'Search by channel name...';
        } else {
            channelSearchInput.disabled = true;
            channelSearchInput.placeholder = 'Select a server first...';

            // Clear channel value when guild is cleared
            const channelHiddenInput = document.getElementById(CHANNEL_HIDDEN_INPUT_ID);
            if (channelHiddenInput) {
                channelHiddenInput.value = '';
            }
            channelSearchInput.value = '';

            // Hide clear button if present
            const wrapper = channelSearchInput.closest('.autocomplete-wrapper');
            const clearButton = wrapper?.querySelector('.autocomplete-clear');
            if (clearButton) {
                clearButton.classList.add('hidden');
            }
        }
    }

    /**
     * Initializes the page when DOM is ready.
     */
    function init() {
        // Listen for guild selection changes
        const guildHiddenInput = document.getElementById(GUILD_HIDDEN_INPUT_ID);
        if (guildHiddenInput) {
            guildHiddenInput.addEventListener('change', function() {
                updateChannelInputState();

                // Clear channel when guild changes
                const channelInstance = (typeof AutocompleteManager !== 'undefined' ? AutocompleteManager : null)?.get(CHANNEL_SEARCH_INPUT_ID);
                if (channelInstance) {
                    channelInstance.clear();
                }
            });
        }

        // Listen for guild autocomplete clear events
        const guildSearchInput = document.getElementById(GUILD_SEARCH_INPUT_ID);
        if (guildSearchInput) {
            guildSearchInput.addEventListener('autocomplete:clear', function() {
                updateChannelInputState();

                // Clear channel when guild is cleared
                const channelInstance = (typeof AutocompleteManager !== 'undefined' ? AutocompleteManager : null)?.get(CHANNEL_SEARCH_INPUT_ID);
                if (channelInstance) {
                    channelInstance.clear();
                }
            });
        }

        // Set initial channel input state
        updateChannelInputState();
    }

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
