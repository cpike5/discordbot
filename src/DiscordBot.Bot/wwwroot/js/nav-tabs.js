/**
 * Navigation Tabs Component JavaScript
 * Unified navigation component with scroll indicators, tab switching, AJAX loading,
 * persistence, and keyboard navigation (issue #1250)
 */

(function () {
    'use strict';

    // ===== Initialization =====
    function initializeNavTabs() {
        const containers = document.querySelectorAll('[data-nav-tabs]');
        containers.forEach(container => {
            new NavTabsController(container);
        });
    }

    // ===== NavTabsController Class =====
    class NavTabsController {
        constructor(container) {
            this.container = container;
            this.containerId = container.dataset.containerId;
            this.navList = container.querySelector('.nav-tabs-list');
            this.items = Array.from(container.querySelectorAll('.nav-tabs-item:not(.disabled)'));

            // Configuration from data attributes
            this.navigationMode = container.dataset.navigationMode || 'pagenavigation';
            this.persistenceMode = container.dataset.persistenceMode || 'none';

            // State
            this.isLoading = false;

            // Initialize features
            this.initScrollIndicators();

            if (this.navigationMode === 'inpage') {
                this.initInPageNavigation();
            } else if (this.navigationMode === 'ajax') {
                this.initAjaxNavigation();
            }

            this.initKeyboardNavigation();
            this.initPersistence();

            // Scroll active tab into view on load
            this.scrollActiveTabIntoView();
        }

        // ===== Scroll Indicators =====
        initScrollIndicators() {
            if (!this.navList) return;

            const updateScrollIndicators = () => {
                const scrollLeft = this.navList.scrollLeft;
                const scrollWidth = this.navList.scrollWidth;
                const clientWidth = this.navList.clientWidth;

                // Check if content is scrollable
                const isScrollable = scrollWidth > clientWidth;

                if (isScrollable) {
                    // Can scroll left if not at the start (5px threshold)
                    if (scrollLeft > 5) {
                        this.container.classList.add('can-scroll-left');
                    } else {
                        this.container.classList.remove('can-scroll-left');
                    }

                    // Can scroll right if not at the end (5px threshold)
                    if (scrollLeft < scrollWidth - clientWidth - 5) {
                        this.container.classList.add('can-scroll-right');
                    } else {
                        this.container.classList.remove('can-scroll-right');
                    }
                } else {
                    this.container.classList.remove('can-scroll-left', 'can-scroll-right');
                }
            };

            // Initial update
            updateScrollIndicators();

            // Update on scroll
            this.navList.addEventListener('scroll', updateScrollIndicators, { passive: true });

            // Update on resize
            window.addEventListener('resize', updateScrollIndicators, { passive: true });
        }

        scrollActiveTabIntoView() {
            if (!this.navList) return;

            const activeTab = this.navList.querySelector('.nav-tabs-item.active');
            if (activeTab) {
                const tabRect = activeTab.getBoundingClientRect();
                const containerRect = this.navList.getBoundingClientRect();

                // Check if active tab is partially or fully outside view
                if (tabRect.left < containerRect.left || tabRect.right > containerRect.right) {
                    activeTab.scrollIntoView({
                        behavior: 'smooth',
                        inline: 'center',
                        block: 'nearest'
                    });
                }
            }
        }

        // ===== In-Page Navigation =====
        initInPageNavigation() {
            this.panels = new Map();

            // Find all panels associated with this container
            const allPanels = document.querySelectorAll(`[data-nav-panel-for="${this.containerId}"]`);
            allPanels.forEach(panel => {
                const tabId = panel.dataset.tabId;
                this.panels.set(tabId, panel);
            });

            // Add click handlers to tabs
            this.items.forEach(item => {
                if (item.tagName === 'BUTTON' || item.tagName === 'A') {
                    item.addEventListener('click', (e) => {
                        e.preventDefault();
                        const tabId = item.dataset.tabId;
                        this.switchToTab(tabId);
                    });
                }
            });
        }

        switchToTab(tabId) {
            if (this.isLoading) return;

            // Update active state on tabs
            this.items.forEach(item => {
                if (item.dataset.tabId === tabId) {
                    item.classList.add('active');
                    item.setAttribute('aria-selected', 'true');
                } else {
                    item.classList.remove('active');
                    item.setAttribute('aria-selected', 'false');
                }
            });

            // Show/hide panels
            this.panels.forEach((panel, panelTabId) => {
                if (panelTabId === tabId) {
                    panel.hidden = false;
                } else {
                    panel.hidden = true;
                }
            });

            // Update persistence
            this.saveActiveTab(tabId);

            // Scroll tab into view
            const activeItem = this.items.find(item => item.dataset.tabId === tabId);
            if (activeItem) {
                activeItem.scrollIntoView({
                    behavior: 'smooth',
                    inline: 'center',
                    block: 'nearest'
                });
            }
        }

        // ===== AJAX Navigation =====
        initAjaxNavigation() {
            this.ajaxTarget = document.getElementById(`${this.containerId}-content`);

            if (!this.ajaxTarget) {
                console.warn(`NavTabs: AJAX target element not found: ${this.containerId}-content`);
                return;
            }

            // Add click handlers to tabs
            this.items.forEach(item => {
                if (item.tagName === 'BUTTON' || item.tagName === 'A') {
                    item.addEventListener('click', (e) => {
                        e.preventDefault();
                        const tabId = item.dataset.tabId;
                        const url = item.dataset.ajaxUrl;

                        if (url) {
                            this.loadTabContent(tabId, url, item);
                        }
                    });
                }
            });
        }

        async loadTabContent(tabId, url, item) {
            if (this.isLoading) return;

            this.isLoading = true;
            this.container.dataset.loading = 'true';

            // Update active state
            this.items.forEach(i => {
                i.classList.remove('active');
                i.setAttribute('aria-selected', 'false');
            });
            item.classList.add('active');
            item.setAttribute('aria-selected', 'true');

            // Show loading state
            this.showLoadingState();

            try {
                const response = await fetch(url, {
                    headers: {
                        'X-Requested-With': 'XMLHttpRequest'
                    }
                });

                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}: ${response.statusText}`);
                }

                const html = await response.text();
                // SECURITY: AJAX content is inserted directly into DOM. This assumes the server endpoint
                // is trusted and returns safe HTML. If content comes from user input, sanitization is required.
                this.ajaxTarget.innerHTML = html;

                // Update persistence
                this.saveActiveTab(tabId);

                // Dispatch event for other scripts to hook into
                this.container.dispatchEvent(new CustomEvent('navtabs:loaded', {
                    detail: { tabId, url }
                }));

            } catch (error) {
                console.error('NavTabs: Failed to load tab content', error);
                this.showErrorState(error.message);
            } finally {
                this.isLoading = false;
                delete this.container.dataset.loading;
            }
        }

        showLoadingState() {
            if (!this.ajaxTarget) return;

            this.ajaxTarget.innerHTML = `
                <div class="nav-tabs-loading">
                    <div class="nav-tabs-loading-spinner"></div>
                    <div class="nav-tabs-loading-text">Loading...</div>
                </div>
            `;
        }

        showErrorState(message) {
            if (!this.ajaxTarget) return;

            // Clear existing content
            this.ajaxTarget.innerHTML = '';

            // Create error container
            const errorContainer = document.createElement('div');
            errorContainer.className = 'nav-tabs-error';

            // Create SVG icon
            const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
            svg.setAttribute('class', 'nav-tabs-error-icon');
            svg.setAttribute('fill', 'none');
            svg.setAttribute('viewBox', '0 0 24 24');
            svg.setAttribute('stroke', 'currentColor');
            const path = document.createElementNS('http://www.w3.org/2000/svg', 'path');
            path.setAttribute('stroke-linecap', 'round');
            path.setAttribute('stroke-linejoin', 'round');
            path.setAttribute('stroke-width', '2');
            path.setAttribute('d', 'M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z');
            svg.appendChild(path);

            // Create error text
            const errorText = document.createElement('div');
            errorText.className = 'nav-tabs-error-text';
            errorText.textContent = 'Failed to load content';

            // Create error detail with message (using textContent to prevent XSS)
            const errorDetail = document.createElement('div');
            errorDetail.className = 'nav-tabs-error-detail';
            errorDetail.textContent = message;

            // Assemble elements
            errorContainer.appendChild(svg);
            errorContainer.appendChild(errorText);
            errorContainer.appendChild(errorDetail);
            this.ajaxTarget.appendChild(errorContainer);
        }

        // ===== Keyboard Navigation =====
        initKeyboardNavigation() {
            this.items.forEach((item, index) => {
                item.addEventListener('keydown', (e) => {
                    let targetIndex = -1;

                    switch (e.key) {
                        case 'ArrowLeft':
                            e.preventDefault();
                            targetIndex = index - 1;
                            break;
                        case 'ArrowRight':
                            e.preventDefault();
                            targetIndex = index + 1;
                            break;
                        case 'Home':
                            e.preventDefault();
                            targetIndex = 0;
                            break;
                        case 'End':
                            e.preventDefault();
                            targetIndex = this.items.length - 1;
                            break;
                        default:
                            return;
                    }

                    // Wrap around
                    if (targetIndex < 0) {
                        targetIndex = this.items.length - 1;
                    } else if (targetIndex >= this.items.length) {
                        targetIndex = 0;
                    }

                    // Focus target tab
                    const targetItem = this.items[targetIndex];
                    if (targetItem) {
                        targetItem.focus();

                        // Optionally activate on arrow key (some UIs do this)
                        // Uncomment if you want arrow keys to switch tabs immediately:
                        // targetItem.click();
                    }
                });
            });
        }

        // ===== Persistence =====
        initPersistence() {
            if (this.persistenceMode === 'hash') {
                this.initHashPersistence();
            } else if (this.persistenceMode === 'localstorage') {
                this.initLocalStoragePersistence();
            }
        }

        initHashPersistence() {
            // Check if hash matches a tab on load
            const hash = window.location.hash.slice(1); // Remove '#'
            if (hash) {
                const matchingItem = this.items.find(item => item.dataset.tabId === hash);
                if (matchingItem && this.navigationMode === 'inpage') {
                    this.switchToTab(hash);
                }
            }

            // Listen for hash changes
            window.addEventListener('hashchange', () => {
                const newHash = window.location.hash.slice(1);
                if (newHash && this.navigationMode === 'inpage') {
                    const matchingItem = this.items.find(item => item.dataset.tabId === newHash);
                    if (matchingItem) {
                        this.switchToTab(newHash);
                    }
                }
            });
        }

        initLocalStoragePersistence() {
            // Try to restore from localStorage
            try {
                const storageKey = `navtabs:${this.containerId}`;
                const savedTabId = localStorage.getItem(storageKey);

                if (savedTabId) {
                    const matchingItem = this.items.find(item => item.dataset.tabId === savedTabId);
                    if (matchingItem && this.navigationMode === 'inpage') {
                        this.switchToTab(savedTabId);
                    }
                }
            } catch (e) {
                // localStorage might not be available
                console.warn('NavTabs: localStorage not available', e);
            }
        }

        saveActiveTab(tabId) {
            if (this.persistenceMode === 'hash') {
                // Update hash without triggering scroll or reload
                if (history.replaceState) {
                    history.replaceState(null, null, `#${tabId}`);
                } else {
                    window.location.hash = tabId;
                }
            } else if (this.persistenceMode === 'localstorage') {
                try {
                    const storageKey = `navtabs:${this.containerId}`;
                    localStorage.setItem(storageKey, tabId);
                } catch (e) {
                    console.warn('NavTabs: Failed to save to localStorage', e);
                }
            }
        }
    }

    // ===== Auto-initialize on DOMContentLoaded =====
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initializeNavTabs);
    } else {
        initializeNavTabs();
    }

    // ===== Export for manual initialization =====
    window.NavTabs = {
        init: initializeNavTabs,
        Controller: NavTabsController
    };

})();
