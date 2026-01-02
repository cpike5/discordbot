// search.js - Global search keyboard shortcuts and behavior

(function() {
    'use strict';

    const SEARCH_INPUT_ID = 'navbar-search';

    function getSearchInput() {
        return document.getElementById(SEARCH_INPUT_ID);
    }

    function isTextInputFocused() {
        const active = document.activeElement;
        if (!active) return false;
        const tagName = active.tagName.toLowerCase();
        return tagName === 'input' || tagName === 'textarea' || active.isContentEditable;
    }

    function focusSearch() {
        const input = getSearchInput();
        if (input) {
            input.focus();
            input.select();
        }
    }

    function clearAndBlurSearch() {
        const input = getSearchInput();
        if (input) {
            input.value = '';
            input.blur();
        }
    }

    function handleKeydown(e) {
        // Ctrl+K (Windows) or Cmd+K (Mac) - focus search
        if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
            e.preventDefault();
            focusSearch();
            return;
        }

        // / key when not in text field - focus search
        if (e.key === '/' && !isTextInputFocused()) {
            e.preventDefault();
            focusSearch();
            return;
        }

        // Escape - clear and blur if in search
        if (e.key === 'Escape') {
            const input = getSearchInput();
            if (input && document.activeElement === input) {
                clearAndBlurSearch();
            }
        }
    }

    // Detect Mac for keyboard hint display
    if (navigator.platform.toUpperCase().indexOf('MAC') >= 0) {
        document.documentElement.classList.add('mac');
    }

    // Initialize
    document.addEventListener('keydown', handleKeydown);

    // Expose for external use
    window.SearchShortcuts = {
        focus: focusSearch,
        clear: clearAndBlurSearch
    };
})();
