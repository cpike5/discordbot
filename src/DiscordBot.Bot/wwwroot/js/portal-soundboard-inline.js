// portal-soundboard-inline.js
// Extracted from Pages/Portal/Soundboard/Index.cshtml — small page-local bootstrap that runs
// before portal-soundboard.js. Config is provided by window.portalSoundboardInlineConfig,
// set from server-rendered values in Index.cshtml.
(function () {
    'use strict';

    // Discord snowflake IDs are 64-bit; always treat as strings in JS.
    var config = window.portalSoundboardInlineConfig || {};
    window.guildId = config.guildId;
    window.currentUserId = config.currentUserId;

    // Initialize unified preferences with background server sync
    if (window.UserPreferences) {
        window.UserPreferences.init(window.guildId);
    }
})();
