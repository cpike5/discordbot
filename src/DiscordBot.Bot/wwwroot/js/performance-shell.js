/**
 * Performance Dashboard Shell
 * Handles tab switching, URL hash routing, time range persistence, and scroll management.
 * Part of issue #721 - Performance Dashboard Shell Layout.
 */

(function() {
    'use strict';

    // ===== Configuration =====
    const STORAGE_KEYS = {
        TIME_RANGE: 'performanceDashboard.timeRange',
        SCROLL_POSITIONS: 'performanceDashboard.scrollPositions'
    };

    const DEFAULT_TIME_RANGE = 24; // hours

    // Tab ID to hash mapping
    const TAB_HASH_MAP = {
        'overview': '',
        'health': 'health-metrics',
        'commands': 'commands',
        'api': 'api-metrics',
        'system': 'system-health',
        'alerts': 'alerts'
    };

    // Reverse mapping: hash to tab ID
    const HASH_TAB_MAP = Object.fromEntries(
        Object.entries(TAB_HASH_MAP).map(([tab, hash]) => [hash, tab])
    );
    // Also map 'overview' hash to overview tab
    HASH_TAB_MAP['overview'] = 'overview';

    // Tab icon paths (outline and solid versions)
    const TAB_ICONS = {
        'overview': {
            outline: 'M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z',
            solid: 'M2 11a1 1 0 011-1h2a1 1 0 011 1v5a1 1 0 01-1 1H3a1 1 0 01-1-1v-5zm6-4a1 1 0 011-1h2a1 1 0 011 1v9a1 1 0 01-1 1H9a1 1 0 01-1-1V7zm6-3a1 1 0 011-1h2a1 1 0 011 1v12a1 1 0 01-1 1h-2a1 1 0 01-1-1V4z'
        },
        'health': {
            outline: 'M4.318 6.318a4.5 4.5 0 000 6.364L12 20.364l7.682-7.682a4.5 4.5 0 00-6.364-6.364L12 7.636l-1.318-1.318a4.5 4.5 0 00-6.364 0z',
            solid: 'M3.172 5.172a4 4 0 015.656 0L12 8.343l3.172-3.171a4 4 0 115.656 5.656L12 19.657l-8.828-8.829a4 4 0 010-5.656z'
        },
        'commands': {
            outline: 'M13 10V3L4 14h7v7l9-11h-7z',
            solid: 'M11.3 1.046A1 1 0 0112 2v5h4a1 1 0 01.82 1.573l-7 10A1 1 0 018 18v-5H4a1 1 0 01-.82-1.573l7-10a1 1 0 011.12-.38z'
        },
        'api': {
            outline: 'M8 9l3 3-3 3m5 0h3M5 20h14a2 2 0 002-2V6a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z',
            solid: 'M14.447 3.027a.75.75 0 01.527.92l-4.5 16.5a.75.75 0 01-1.448-.394l4.5-16.5a.75.75 0 01.921-.526zM16.72 6.22a.75.75 0 011.06 0l5.25 5.25a.75.75 0 010 1.06l-5.25 5.25a.75.75 0 11-1.06-1.06L21.44 12l-4.72-4.72a.75.75 0 010-1.06zm-9.44 0a.75.75 0 010 1.06L2.56 12l4.72 4.72a.75.75 0 11-1.06 1.06L.97 12.53a.75.75 0 010-1.06l5.25-5.25a.75.75 0 011.06 0z'
        },
        'system': {
            outline: 'M5 12h14M5 12a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v4a2 2 0 01-2 2M5 12a2 2 0 00-2 2v4a2 2 0 002 2h14a2 2 0 002-2v-4a2 2 0 00-2-2m-2-4h.01M17 16h.01',
            solid: 'M4.5 3A1.5 1.5 0 003 4.5v4A1.5 1.5 0 004.5 10h11a1.5 1.5 0 001.5-1.5v-4A1.5 1.5 0 0015.5 3h-11zm0 11A1.5 1.5 0 003 15.5v4A1.5 1.5 0 004.5 21h11a1.5 1.5 0 001.5-1.5v-4a1.5 1.5 0 00-1.5-1.5h-11zM13 7a1 1 0 100-2 1 1 0 000 2zm-3 0a1 1 0 100-2 1 1 0 000 2zm6 11a1 1 0 100-2 1 1 0 000 2zm-3 0a1 1 0 100-2 1 1 0 000 2z'
        },
        'alerts': {
            outline: 'M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9',
            solid: 'M10 2a6 6 0 00-6 6v3.586l-.707.707A1 1 0 004 14h12a1 1 0 00.707-1.707L16 11.586V8a6 6 0 00-6-6zM10 18a3 3 0 01-3-3h6a3 3 0 01-3 3z'
        }
    };

    // ===== State =====
    let currentTab = 'overview';
    let currentTimeRange = DEFAULT_TIME_RANGE;

    // ===== DOM Elements =====
    function getElements() {
        return {
            tabsContainer: document.getElementById('performanceShellTabsContainer'),
            tabs: document.getElementById('performanceShellTabs'),
            tabButtons: document.querySelectorAll('.performance-shell-tab'),
            tabPanels: document.querySelectorAll('.performance-tab-panel'),
            timeRangeButtons: document.querySelectorAll('.time-range-option'),
            contentArea: document.getElementById('tabContent')
        };
    }

    // ===== Tab Management =====

    /**
     * Update tab icon to solid (active) or outline (inactive)
     * @param {HTMLElement} button - The tab button element
     * @param {string} tabId - The tab identifier
     * @param {boolean} isActive - Whether the tab is active
     */
    function updateTabIcon(button, tabId, isActive) {
        const iconData = TAB_ICONS[tabId];
        if (!iconData) return;

        const svg = button.querySelector('.tab-icon');
        if (!svg) return;

        const path = svg.querySelector('path');
        if (!path) return;

        if (isActive) {
            // Switch to solid icon
            svg.setAttribute('fill', 'currentColor');
            svg.removeAttribute('stroke');
            path.setAttribute('d', iconData.solid);
            path.removeAttribute('stroke-linecap');
            path.removeAttribute('stroke-linejoin');
            path.removeAttribute('stroke-width');
        } else {
            // Switch to outline icon
            svg.setAttribute('fill', 'none');
            svg.setAttribute('stroke', 'currentColor');
            path.setAttribute('d', iconData.outline);
            path.setAttribute('stroke-linecap', 'round');
            path.setAttribute('stroke-linejoin', 'round');
            path.setAttribute('stroke-width', '2');
        }
    }

    /**
     * Switch to a specific tab
     * @param {string} tabId - The tab identifier to switch to
     * @param {boolean} updateHash - Whether to update the URL hash
     */
    function switchTab(tabId, updateHash = true) {
        const elements = getElements();

        if (!TAB_HASH_MAP.hasOwnProperty(tabId)) {
            console.warn('Unknown tab ID:', tabId);
            return;
        }

        // Save scroll position of current tab
        saveScrollPosition(currentTab);

        // Update tab buttons and icons
        elements.tabButtons.forEach(button => {
            const buttonTabId = button.dataset.tabId;
            const isActive = buttonTabId === tabId;
            button.classList.toggle('active', isActive);
            button.setAttribute('aria-selected', isActive ? 'true' : 'false');
            button.setAttribute('tabindex', isActive ? '0' : '-1');
            updateTabIcon(button, buttonTabId, isActive);
        });

        // Update tab panels
        elements.tabPanels.forEach(panel => {
            const panelTabId = panel.id.replace('tabPanel-', '');
            const isActive = panelTabId === tabId;
            panel.classList.toggle('active', isActive);
            panel.hidden = !isActive;
        });

        // Update URL hash
        if (updateHash) {
            const hash = TAB_HASH_MAP[tabId];
            if (hash) {
                history.replaceState(null, '', '#' + hash);
            } else {
                // For overview tab, remove hash
                history.replaceState(null, '', window.location.pathname + window.location.search);
            }
        }

        // Update current tab
        currentTab = tabId;

        // Restore scroll position for new tab
        restoreScrollPosition(tabId);

        // Dispatch tab change event for other components to listen
        document.dispatchEvent(new CustomEvent('performanceTabChanged', {
            detail: { tabId, timeRange: currentTimeRange }
        }));

        // Scroll active tab into view if needed
        scrollActiveTabIntoView(tabId);
    }

    /**
     * Get the tab ID from the current URL hash
     * @returns {string} The tab ID
     */
    function getTabFromHash() {
        const hash = window.location.hash.replace('#', '');
        if (!hash) return 'overview';
        return HASH_TAB_MAP[hash] || 'overview';
    }

    /**
     * Scroll the active tab button into view in the tab bar
     * @param {string} tabId - The tab ID
     */
    function scrollActiveTabIntoView(tabId) {
        const elements = getElements();
        const activeButton = elements.tabs?.querySelector(`[data-tab-id="${tabId}"]`);

        if (activeButton && elements.tabs) {
            const tabRect = activeButton.getBoundingClientRect();
            const containerRect = elements.tabs.getBoundingClientRect();

            if (tabRect.left < containerRect.left || tabRect.right > containerRect.right) {
                activeButton.scrollIntoView({ behavior: 'smooth', inline: 'center', block: 'nearest' });
            }
        }
    }

    // ===== Scroll Position Management =====

    /**
     * Save the current scroll position for a tab
     * @param {string} tabId - The tab ID
     */
    function saveScrollPosition(tabId) {
        try {
            const positions = JSON.parse(sessionStorage.getItem(STORAGE_KEYS.SCROLL_POSITIONS) || '{}');
            positions[tabId] = window.scrollY;
            sessionStorage.setItem(STORAGE_KEYS.SCROLL_POSITIONS, JSON.stringify(positions));
        } catch (e) {
            console.warn('Failed to save scroll position:', e);
        }
    }

    /**
     * Restore the scroll position for a tab
     * @param {string} tabId - The tab ID
     */
    function restoreScrollPosition(tabId) {
        try {
            const positions = JSON.parse(sessionStorage.getItem(STORAGE_KEYS.SCROLL_POSITIONS) || '{}');
            const savedPosition = positions[tabId];

            if (typeof savedPosition === 'number') {
                // Use requestAnimationFrame to ensure DOM is updated
                requestAnimationFrame(() => {
                    window.scrollTo(0, savedPosition);
                });
            }
        } catch (e) {
            console.warn('Failed to restore scroll position:', e);
        }
    }

    // ===== Time Range Management =====

    /**
     * Set the time range and persist to localStorage
     * @param {number} hours - Time range in hours (24, 168, or 720)
     */
    function setTimeRange(hours) {
        if (![24, 168, 720].includes(hours)) {
            console.warn('Invalid time range:', hours);
            return;
        }

        currentTimeRange = hours;

        // Persist to localStorage
        try {
            localStorage.setItem(STORAGE_KEYS.TIME_RANGE, String(hours));
        } catch (e) {
            console.warn('Failed to save time range:', e);
        }

        // Update UI
        updateTimeRangeUI();

        // Dispatch time range change event
        document.dispatchEvent(new CustomEvent('timeRangeChanged', {
            detail: { hours, tabId: currentTab }
        }));
    }

    /**
     * Load the saved time range from localStorage
     * @returns {number} Time range in hours
     */
    function loadTimeRange() {
        try {
            const saved = localStorage.getItem(STORAGE_KEYS.TIME_RANGE);
            if (saved) {
                const hours = parseInt(saved, 10);
                if ([24, 168, 720].includes(hours)) {
                    return hours;
                }
            }
        } catch (e) {
            console.warn('Failed to load time range:', e);
        }
        return DEFAULT_TIME_RANGE;
    }

    /**
     * Update the time range button UI
     */
    function updateTimeRangeUI() {
        const elements = getElements();
        elements.timeRangeButtons.forEach(button => {
            const hours = parseInt(button.dataset.hours, 10);
            button.classList.toggle('active', hours === currentTimeRange);
        });
    }

    // ===== Scroll Indicators =====

    /**
     * Update the scroll fade indicators on the tab bar
     */
    function updateScrollIndicators() {
        const elements = getElements();
        if (!elements.tabsContainer || !elements.tabs) return;

        const { scrollLeft, scrollWidth, clientWidth } = elements.tabs;
        const isScrollable = scrollWidth > clientWidth;

        if (isScrollable) {
            elements.tabsContainer.classList.toggle('can-scroll-left', scrollLeft > 5);
            elements.tabsContainer.classList.toggle('can-scroll-right', scrollLeft < scrollWidth - clientWidth - 5);
        } else {
            elements.tabsContainer.classList.remove('can-scroll-left', 'can-scroll-right');
        }
    }

    // ===== Keyboard Navigation =====

    /**
     * Handle keyboard navigation in the tab bar
     * @param {KeyboardEvent} event
     */
    function handleTabKeydown(event) {
        const elements = getElements();
        const tabs = Array.from(elements.tabButtons);
        const currentIndex = tabs.findIndex(tab => tab.classList.contains('active'));

        let newIndex = currentIndex;

        switch (event.key) {
            case 'ArrowLeft':
                newIndex = currentIndex > 0 ? currentIndex - 1 : tabs.length - 1;
                event.preventDefault();
                break;
            case 'ArrowRight':
                newIndex = currentIndex < tabs.length - 1 ? currentIndex + 1 : 0;
                event.preventDefault();
                break;
            case 'Home':
                newIndex = 0;
                event.preventDefault();
                break;
            case 'End':
                newIndex = tabs.length - 1;
                event.preventDefault();
                break;
            default:
                return;
        }

        if (newIndex !== currentIndex) {
            const newTab = tabs[newIndex];
            const tabId = newTab.dataset.tabId;
            switchTab(tabId);
            newTab.focus();
        }
    }

    // ===== Event Binding =====

    /**
     * Bind all event listeners
     */
    function bindEvents() {
        const elements = getElements();

        // Tab button clicks
        elements.tabButtons.forEach(button => {
            button.addEventListener('click', () => {
                const tabId = button.dataset.tabId;
                switchTab(tabId);
            });

            // Keyboard navigation
            button.addEventListener('keydown', handleTabKeydown);
        });

        // Time range button clicks
        elements.timeRangeButtons.forEach(button => {
            button.addEventListener('click', () => {
                const hours = parseInt(button.dataset.hours, 10);
                setTimeRange(hours);
            });
        });

        // Hash change (browser back/forward)
        window.addEventListener('hashchange', () => {
            const tabId = getTabFromHash();
            if (tabId !== currentTab) {
                switchTab(tabId, false);
            }
        });

        // Scroll indicators
        if (elements.tabs) {
            elements.tabs.addEventListener('scroll', updateScrollIndicators, { passive: true });
        }
        window.addEventListener('resize', updateScrollIndicators, { passive: true });
    }

    // ===== Initialization =====

    /**
     * Initialize the performance shell
     */
    function init() {
        // Load saved time range
        currentTimeRange = loadTimeRange();
        updateTimeRangeUI();

        // Determine initial tab from hash or default to overview
        const initialTab = getTabFromHash();
        currentTab = initialTab;

        // Bind all events
        bindEvents();

        // Update scroll indicators
        updateScrollIndicators();

        // Scroll active tab into view on load
        requestAnimationFrame(() => {
            scrollActiveTabIntoView(currentTab);
        });

        // If there's a hash, make sure we switch to the correct tab
        if (initialTab !== 'overview') {
            switchTab(initialTab, false);
        }
    }

    // ===== Public API =====
    window.PerformanceShell = {
        switchTab,
        setTimeRange,
        getTimeRange: () => currentTimeRange,
        getCurrentHours: () => currentTimeRange, // Alias for getTimeRange
        getCurrentTab: () => currentTab
    };

    // Initialize on DOMContentLoaded
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
