/**
 * Shared Keyboard Shortcut Manager for the Audio Portal.
 * Provides registration, dispatch, and a help overlay for keyboard shortcuts.
 *
 * Usage:
 *   KeyboardShortcuts.register('/', 'Focus search input', () => { ... }, { category: 'Soundboard' });
 *   KeyboardShortcuts.init();
 */
var KeyboardShortcuts = (function () {
    'use strict';

    var shortcuts = [];
    var initialized = false;
    var overlayVisible = false;
    var overlayEl = null;

    /**
     * Register a keyboard shortcut.
     * @param {string} key - The key value (e.g. '/', '?', 'Enter', '1')
     * @param {string} description - Human-readable description shown in help overlay
     * @param {function} callback - Function to call when the shortcut fires; receives the KeyboardEvent
     * @param {object} [options] - Optional modifiers
     * @param {boolean} [options.ctrlKey] - Require Ctrl (or Meta on Mac)
     * @param {boolean} [options.shiftKey] - Require Shift
     * @param {string} [options.category] - Category label for the help overlay grouping
     */
    function register(key, description, callback, options) {
        options = options || {};
        shortcuts.push({
            key: key,
            description: description,
            callback: callback,
            ctrlKey: !!options.ctrlKey,
            shiftKey: !!options.shiftKey,
            category: options.category || 'Global'
        });
    }

    /**
     * Attach the global keydown listener. Safe to call multiple times.
     */
    function init() {
        if (initialized) return;
        initialized = true;

        // Always register the help toggle
        register('?', 'Show keyboard shortcuts', function () {
            toggleHelp();
        }, { category: 'Global' });

        document.addEventListener('keydown', handleKeydown);
    }

    function handleKeydown(e) {
        // Close overlay on Escape
        if (e.key === 'Escape' && overlayVisible) {
            hideHelp();
            e.preventDefault();
            return;
        }

        // Determine if the user is typing in a form field
        var tag = e.target.tagName;
        var isFormField = (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT');
        var hasModifier = e.ctrlKey || e.metaKey;

        for (var i = 0; i < shortcuts.length; i++) {
            var s = shortcuts[i];

            // Key must match
            if (e.key !== s.key) continue;

            // Modifier requirements
            if (s.ctrlKey && !hasModifier) continue;
            if (!s.ctrlKey && hasModifier) continue;
            if (s.shiftKey && !e.shiftKey) continue;

            // Skip non-modifier shortcuts when typing in form fields
            if (isFormField && !s.ctrlKey) continue;

            // Match found
            e.preventDefault();
            s.callback(e);
            return;
        }
    }

    /**
     * Build and show the help overlay listing all registered shortcuts.
     */
    function showHelp() {
        if (overlayVisible) return;
        overlayVisible = true;

        if (!overlayEl) {
            overlayEl = buildOverlay();
            document.body.appendChild(overlayEl);
        }

        // Rebuild content each time in case page-specific shortcuts were registered after first show
        populateOverlay();

        // Force reflow then add visible class for transition
        overlayEl.offsetHeight; // eslint-disable-line no-unused-expressions
        overlayEl.classList.add('kbd-overlay-visible');
        overlayEl.setAttribute('aria-hidden', 'false');
    }

    function hideHelp() {
        if (!overlayVisible) return;
        overlayVisible = false;

        if (overlayEl) {
            overlayEl.classList.remove('kbd-overlay-visible');
            overlayEl.setAttribute('aria-hidden', 'true');
        }
    }

    function toggleHelp() {
        if (overlayVisible) {
            hideHelp();
        } else {
            showHelp();
        }
    }

    function buildOverlay() {
        var overlay = document.createElement('div');
        overlay.className = 'kbd-overlay';
        overlay.setAttribute('role', 'dialog');
        overlay.setAttribute('aria-label', 'Keyboard shortcuts');
        overlay.setAttribute('aria-hidden', 'true');

        // Close when clicking the backdrop
        overlay.addEventListener('click', function (e) {
            if (e.target === overlay) {
                hideHelp();
            }
        });

        var dialog = document.createElement('div');
        dialog.className = 'kbd-dialog';

        var header = document.createElement('div');
        header.className = 'kbd-header';
        header.innerHTML =
            '<h2 class="kbd-title">Keyboard Shortcuts</h2>' +
            '<button class="kbd-close-btn" aria-label="Close shortcuts help">' +
            '<svg width="20" height="20" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">' +
            '<path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12"/>' +
            '</svg></button>';

        header.querySelector('.kbd-close-btn').addEventListener('click', hideHelp);

        var body = document.createElement('div');
        body.className = 'kbd-body';
        body.id = 'kbdShortcutList';

        dialog.appendChild(header);
        dialog.appendChild(body);
        overlay.appendChild(dialog);

        return overlay;
    }

    function populateOverlay() {
        var body = overlayEl.querySelector('#kbdShortcutList');
        body.innerHTML = '';

        // Group by category
        var groups = {};
        var categoryOrder = [];
        for (var i = 0; i < shortcuts.length; i++) {
            var s = shortcuts[i];
            if (!groups[s.category]) {
                groups[s.category] = [];
                categoryOrder.push(s.category);
            }
            groups[s.category].push(s);
        }

        // Move 'Global' to the end
        var globalIdx = categoryOrder.indexOf('Global');
        if (globalIdx > -1) {
            categoryOrder.splice(globalIdx, 1);
            categoryOrder.push('Global');
        }

        for (var c = 0; c < categoryOrder.length; c++) {
            var catName = categoryOrder[c];
            var items = groups[catName];

            var section = document.createElement('div');
            section.className = 'kbd-section';

            var catHeading = document.createElement('h3');
            catHeading.className = 'kbd-category';
            catHeading.textContent = catName;
            section.appendChild(catHeading);

            for (var j = 0; j < items.length; j++) {
                var item = items[j];
                var row = document.createElement('div');
                row.className = 'kbd-row';

                var keySpan = document.createElement('span');
                keySpan.className = 'kbd-keys';

                var label = formatKeyLabel(item);
                var parts = label.split('+');
                for (var p = 0; p < parts.length; p++) {
                    if (p > 0) {
                        var plus = document.createElement('span');
                        plus.className = 'kbd-plus';
                        plus.textContent = '+';
                        keySpan.appendChild(plus);
                    }
                    var kbd = document.createElement('kbd');
                    kbd.textContent = parts[p].trim();
                    keySpan.appendChild(kbd);
                }

                var desc = document.createElement('span');
                desc.className = 'kbd-desc';
                desc.textContent = item.description;

                row.appendChild(keySpan);
                row.appendChild(desc);
                section.appendChild(row);
            }

            body.appendChild(section);
        }
    }

    function formatKeyLabel(shortcut) {
        var parts = [];
        if (shortcut.ctrlKey) parts.push('Ctrl');
        if (shortcut.shiftKey) parts.push('Shift');

        // Friendly display names for special keys
        var keyName = shortcut.key;
        if (keyName === 'Enter') keyName = 'Enter';
        else if (keyName === '/') keyName = '/';
        else if (keyName === '?') keyName = '?';
        else if (keyName.length === 1) keyName = keyName.toUpperCase();

        parts.push(keyName);
        return parts.join(' + ');
    }

    return {
        register: register,
        init: init,
        showHelp: showHelp,
        hideHelp: hideHelp,
        toggleHelp: toggleHelp
    };
})();
