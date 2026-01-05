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

        // Update tab buttons
        elements.tabButtons.forEach(button => {
            const isActive = button.dataset.tabId === tabId;
            button.classList.toggle('active', isActive);
            button.setAttribute('aria-selected', isActive ? 'true' : 'false');
            button.setAttribute('tabindex', isActive ? '0' : '-1');
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
        getCurrentTab: () => currentTab
    };

    // Initialize on DOMContentLoaded
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
