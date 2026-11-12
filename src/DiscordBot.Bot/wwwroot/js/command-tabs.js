/**
 * Command Tabs Module
 * Updates the Commands page subtitle (the _PageHeader's [data-page-subtitle]) when the tab changes.
 * The tabs are sections of one page, so there is no breadcrumb segment to update.
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
         * Update the page subtitle when the tab changes.
         * @param {string} tabId - The ID of the active tab
         */
        updatePageElements: function(tabId) {
            // Validate tab ID
            if (!this.subtitleMap[tabId]) {
                console.warn(`CommandTabs: Invalid tab ID: ${tabId}`);
                return;
            }

            this.updateSubtitle(tabId);
        },

        /**
         * Update the page subtitle based on active tab.
         * @param {string} tabId - The ID of the active tab
         */
        updateSubtitle: function(tabId) {
            const subtitleElement = document.querySelector('[data-page-subtitle]');
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
         * Load execution logs page (called from pagination buttons).
         * @param {number} pageNumber - Page number to load
         */
        loadExecutionLogsPage: function(pageNumber) {
            if (window.CommandPagination) {
                window.CommandPagination.goToPage(pageNumber);
            }
        },

        /**
         * Clear execution logs filters (called from clear button).
         */
        clearExecutionLogsFilters: function() {
            if (window.CommandFilters) {
                window.CommandFilters.clearFilters();
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
