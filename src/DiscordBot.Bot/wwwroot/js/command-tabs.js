/**
 * Command Tabs Module
 * Handles subtitle and breadcrumb updates for the Commands page tab switching.
 */
(function() {
    'use strict';

    const CommandTabs = {
        // Subtitle mapping based on tab ID
        subtitleMap: {
            'command-list': 'All registered slash commands',
            'execution-logs': 'Command execution history',
            'analytics': 'Usage statistics and performance'
        },

        // Breadcrumb mapping based on tab ID
        breadcrumbMap: {
            'command-list': 'Command List',
            'execution-logs': 'Execution Logs',
            'analytics': 'Analytics'
        },

        /**
         * Initialize command tabs module.
         * Sets up listeners for tab changes.
         */
        init: function() {
            // Find the tab panel container for Commands page
            const tabContainer = document.querySelector('[data-panel-id="commandTabs"]');
            if (!tabContainer) {
                console.warn('CommandTabs: Tab panel container not found');
                return;
            }

            // Listen for tab change events from TabPanel
            tabContainer.addEventListener('tabchange', (e) => {
                const { tabId } = e.detail;
                this.updatePageElements(tabId);
            });
        },

        /**
         * Update page subtitle and breadcrumb when tab changes.
         * @param {string} tabId - The ID of the active tab
         */
        updatePageElements: function(tabId) {
            // Validate tab ID
            if (!this.subtitleMap[tabId] || !this.breadcrumbMap[tabId]) {
                console.warn(`CommandTabs: Invalid tab ID: ${tabId}`);
                return;
            }

            // Update subtitle
            this.updateSubtitle(tabId);

            // Update breadcrumb
            this.updateBreadcrumb(tabId);
        },

        /**
         * Update the page subtitle based on active tab.
         * @param {string} tabId - The ID of the active tab
         */
        updateSubtitle: function(tabId) {
            const subtitleElement = document.querySelector('[data-command-subtitle]');
            if (!subtitleElement) {
                console.warn('CommandTabs: Subtitle element not found');
                return;
            }

            const newSubtitle = this.subtitleMap[tabId] || '';
            if (subtitleElement.textContent !== newSubtitle) {
                subtitleElement.textContent = newSubtitle;
            }
        },

        /**
         * Update the breadcrumb active segment based on active tab.
         * @param {string} tabId - The ID of the active tab
         */
        updateBreadcrumb: function(tabId) {
            const breadcrumbElement = document.querySelector('[data-command-breadcrumb-active]');
            if (!breadcrumbElement) {
                console.warn('CommandTabs: Breadcrumb element not found');
                return;
            }

            const newBreadcrumb = this.breadcrumbMap[tabId] || '';
            if (breadcrumbElement.textContent !== newBreadcrumb) {
                breadcrumbElement.textContent = newBreadcrumb;
            }
        }
    };

    // Expose to global scope
    window.CommandTabs = CommandTabs;

    // Auto-initialize when DOM is ready
    document.addEventListener('DOMContentLoaded', function() {
        CommandTabs.init();
    });

})();
