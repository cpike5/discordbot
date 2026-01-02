// search.js - Global search keyboard shortcuts and behavior

(function() {
    'use strict';

    const SEARCH_INPUT_ID = 'navbar-search';
    const RECENT_SEARCHES_KEY = 'recentSearches';
    const MAX_RECENT_SEARCHES = 5;
    const DROPDOWN_ID = 'recent-searches-dropdown';

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

    // localStorage functions
    function getRecentSearches() {
        try {
            const stored = localStorage.getItem(RECENT_SEARCHES_KEY);
            return stored ? JSON.parse(stored) : [];
        } catch (e) {
            console.error('Error reading recent searches:', e);
            return [];
        }
    }

    function saveRecentSearch(term) {
        if (!term || term.trim().length === 0) return;

        try {
            let searches = getRecentSearches();

            // Remove term if it already exists (to move it to top)
            searches = searches.filter(s => s !== term);

            // Add to beginning
            searches.unshift(term);

            // Limit to max
            if (searches.length > MAX_RECENT_SEARCHES) {
                searches = searches.slice(0, MAX_RECENT_SEARCHES);
            }

            localStorage.setItem(RECENT_SEARCHES_KEY, JSON.stringify(searches));
        } catch (e) {
            console.error('Error saving recent search:', e);
        }
    }

    function removeRecentSearch(term) {
        try {
            let searches = getRecentSearches();
            searches = searches.filter(s => s !== term);
            localStorage.setItem(RECENT_SEARCHES_KEY, JSON.stringify(searches));
            renderRecentSearches();
        } catch (e) {
            console.error('Error removing recent search:', e);
        }
    }

    function clearAllRecentSearches() {
        try {
            localStorage.removeItem(RECENT_SEARCHES_KEY);
            renderRecentSearches();
        } catch (e) {
            console.error('Error clearing recent searches:', e);
        }
    }

    // Dropdown functions
    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    function createDropdown() {
        let dropdown = document.getElementById(DROPDOWN_ID);
        if (dropdown) return dropdown;

        const input = getSearchInput();
        if (!input) return null;

        dropdown = document.createElement('div');
        dropdown.id = DROPDOWN_ID;
        dropdown.className = 'recent-searches-dropdown';

        // Insert after the search input's parent container
        const container = input.closest('.search-container');
        if (container) {
            container.appendChild(dropdown);
        } else {
            input.parentNode.insertBefore(dropdown, input.nextSibling);
        }

        return dropdown;
    }

    function renderRecentSearches() {
        const dropdown = createDropdown();
        if (!dropdown) return;

        const searches = getRecentSearches();

        if (searches.length === 0) {
            dropdown.innerHTML = `
                <div class="recent-searches-empty">
                    <svg class="recent-search-icon" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z"></path>
                    </svg>
                    <span>No recent searches</span>
                </div>
            `;
            return;
        }

        let html = `
            <div class="recent-searches-header">
                <span>Recent Searches</span>
                <button type="button" class="recent-searches-clear" data-action="clear-all">Clear all</button>
            </div>
            <ul class="recent-searches-list">
        `;

        searches.forEach(term => {
            html += `
                <li class="recent-search-item">
                    <a href="/Search?query=${encodeURIComponent(term)}" class="recent-search-link">
                        <svg class="recent-search-icon" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z"></path>
                        </svg>
                        <span class="recent-search-term">${escapeHtml(term)}</span>
                    </a>
                    <button type="button" class="recent-search-remove" data-term="${escapeHtml(term)}" title="Remove">
                        <svg fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"></path>
                        </svg>
                    </button>
                </li>
            `;
        });

        html += '</ul>';
        dropdown.innerHTML = html;

        // Add event listeners for remove buttons
        dropdown.querySelectorAll('.recent-search-remove').forEach(btn => {
            btn.addEventListener('click', (e) => {
                e.preventDefault();
                e.stopPropagation();
                const term = btn.getAttribute('data-term');
                removeRecentSearch(term);
            });
        });

        // Add event listener for clear all
        const clearAllBtn = dropdown.querySelector('[data-action="clear-all"]');
        if (clearAllBtn) {
            clearAllBtn.addEventListener('click', (e) => {
                e.preventDefault();
                clearAllRecentSearches();
            });
        }
    }

    function showRecentSearches() {
        const dropdown = createDropdown();
        if (!dropdown) return;

        renderRecentSearches();
        dropdown.classList.add('active');
    }

    function hideRecentSearches() {
        const dropdown = document.getElementById(DROPDOWN_ID);
        if (dropdown) {
            dropdown.classList.remove('active');
        }
    }

    function setupSearchInputListeners() {
        const input = getSearchInput();
        if (!input) return;

        const form = input.closest('form');

        // Show recent searches on focus when input is empty
        input.addEventListener('focus', () => {
            if (input.value.trim().length === 0) {
                showRecentSearches();
            }
        });

        // Hide recent searches on blur (with delay for click handling)
        let blurTimeout;
        input.addEventListener('blur', () => {
            blurTimeout = setTimeout(() => {
                hideRecentSearches();
            }, 200);
        });

        // On input, hide dropdown if typing, show if empty
        input.addEventListener('input', () => {
            if (blurTimeout) {
                clearTimeout(blurTimeout);
            }

            if (input.value.trim().length === 0) {
                showRecentSearches();
            } else {
                hideRecentSearches();
            }
        });

        // Save search term on form submit
        if (form) {
            form.addEventListener('submit', () => {
                const term = input.value.trim();
                if (term.length > 0) {
                    saveRecentSearch(term);
                }
            });
        }
    }

    // Detect Mac for keyboard hint display
    if (navigator.platform.toUpperCase().indexOf('MAC') >= 0) {
        document.documentElement.classList.add('mac');
    }

    // Initialize
    document.addEventListener('keydown', handleKeydown);
    document.addEventListener('DOMContentLoaded', setupSearchInputListeners);

    // Expose for external use
    window.SearchShortcuts = {
        focus: focusSearch,
        clear: clearAndBlurSearch,
        getRecent: getRecentSearches,
        saveRecent: saveRecentSearch,
        clearRecent: clearAllRecentSearches
    };
})();
