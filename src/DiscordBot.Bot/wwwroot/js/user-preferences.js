/**
 * Unified User Preferences module.
 *
 * Provides a localStorage-first cache with background server sync.
 * - get(key, defaultValue) reads from localStorage (instant), returns cached value
 * - set(key, value) writes to localStorage immediately, queues server sync
 * - delete(key) removes from localStorage, queues server delete
 * - sync() fetches all preferences from server, updates localStorage cache
 * - init(guildId) called on page load, triggers background sync
 *
 * localStorage key format: `pref:${guildId}:${key}`
 * Server sync is debounced (500ms) to batch rapid changes.
 * On conflict, server value wins (last-write-wins by UpdatedAt).
 *
 * CRITICAL: guildId is always treated as a string (Discord snowflake IDs
 * exceed JavaScript's Number.MAX_SAFE_INTEGER).
 */
(function () {
    'use strict';

    const DEBOUNCE_MS = 500;
    const STORAGE_PREFIX = 'pref:';

    let _guildId = null;
    let _syncTimer = null;
    let _pendingWrites = {};   // key -> value (null means delete)
    let _syncing = false;

    // ========================================
    // localStorage helpers
    // ========================================

    function storageKey(key) {
        return STORAGE_PREFIX + _guildId + ':' + key;
    }

    function readLocal(key) {
        try {
            return localStorage.getItem(storageKey(key));
        } catch (e) {
            return null;
        }
    }

    function writeLocal(key, value) {
        try {
            localStorage.setItem(storageKey(key), value);
        } catch (e) {
            // Storage full or blocked — degrade silently
        }
    }

    function removeLocal(key) {
        try {
            localStorage.removeItem(storageKey(key));
        } catch (e) {
            // Ignore
        }
    }

    // ========================================
    // Server API helpers
    // ========================================

    function apiBase() {
        return '/api/portal/preferences/' + _guildId;
    }

    async function apiFetchAll() {
        var response = await fetch(apiBase(), {
            credentials: 'same-origin',
            headers: { 'Accept': 'application/json' }
        });
        if (!response.ok) return null;
        return await response.json();
    }

    async function apiPut(key, value) {
        await fetch(apiBase() + '/' + encodeURIComponent(key), {
            method: 'PUT',
            credentials: 'same-origin',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ value: value })
        });
    }

    async function apiDelete(key) {
        await fetch(apiBase() + '/' + encodeURIComponent(key), {
            method: 'DELETE',
            credentials: 'same-origin'
        });
    }

    // ========================================
    // Debounced flush
    // ========================================

    function scheduleFlush() {
        if (_syncTimer) clearTimeout(_syncTimer);
        _syncTimer = setTimeout(flush, DEBOUNCE_MS);
    }

    async function flush() {
        if (_syncing) {
            // Re-schedule if currently syncing
            scheduleFlush();
            return;
        }

        var batch = _pendingWrites;
        _pendingWrites = {};
        var keys = Object.keys(batch);
        if (keys.length === 0) return;

        _syncing = true;
        try {
            var promises = keys.map(function (key) {
                var value = batch[key];
                if (value === null) {
                    return apiDelete(key);
                }
                return apiPut(key, value);
            });
            await Promise.allSettled(promises);
        } catch (e) {
            // Network failure — values remain in localStorage as cache
        } finally {
            _syncing = false;
        }
    }

    // ========================================
    // Public API
    // ========================================

    /**
     * Initialize the preferences module for a guild.
     * Triggers a background sync from the server.
     * @param {string} guildId - Discord guild snowflake ID (must be a string).
     */
    function init(guildId) {
        if (!guildId) return;
        _guildId = String(guildId);
        // Background sync — do not block page load
        sync();
    }

    /**
     * Get a preference value. Reads from localStorage (instant).
     * @param {string} key - The preference key.
     * @param {string|null} defaultValue - Value to return if not found.
     * @returns {string|null} The preference value, or defaultValue.
     */
    function get(key, defaultValue) {
        if (!_guildId) return defaultValue !== undefined ? defaultValue : null;
        var value = readLocal(key);
        if (value === null) return defaultValue !== undefined ? defaultValue : null;
        return value;
    }

    /**
     * Set a preference value. Writes to localStorage immediately and
     * queues a debounced server sync.
     * @param {string} key - The preference key.
     * @param {string} value - The preference value.
     */
    function set(key, value) {
        if (!_guildId || !key) return;
        writeLocal(key, value);
        _pendingWrites[key] = value;
        scheduleFlush();
    }

    /**
     * Delete a preference. Removes from localStorage immediately and
     * queues a debounced server delete.
     * @param {string} key - The preference key.
     */
    function deleteKey(key) {
        if (!_guildId || !key) return;
        removeLocal(key);
        _pendingWrites[key] = null;
        scheduleFlush();
    }

    /**
     * Fetch all preferences from the server and update localStorage.
     * Server values win on conflict (last-write-wins).
     * @returns {Promise<Object|null>} The preferences dictionary, or null on failure.
     */
    async function sync() {
        if (!_guildId) return null;
        try {
            var data = await apiFetchAll();
            if (data && typeof data === 'object') {
                // Update localStorage with server values
                var serverKeys = Object.keys(data);
                for (var i = 0; i < serverKeys.length; i++) {
                    writeLocal(serverKeys[i], data[serverKeys[i]]);
                }
                return data;
            }
        } catch (e) {
            // Network error — use cached values
        }
        return null;
    }

    // Expose the module globally
    window.UserPreferences = {
        init: init,
        get: get,
        set: set,
        delete: deleteKey,
        sync: sync
    };
})();
