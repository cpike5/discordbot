/**
 * Performance Dashboard - Time Range State Management
 * Centralized time range persistence and event dispatching
 */
(function() {
    'use strict';

    window.Performance = window.Performance || {};

    const STORAGE_KEY = 'performance-dashboard-time-range';
    const VALID_RANGES = [24, 168, 720];
    const DEFAULT_RANGE = 24;

    const TimeRange = {
        _currentHours: DEFAULT_RANGE,

        /**
         * Initialize time range from localStorage
         */
        init: function() {
            try {
                const stored = localStorage.getItem(STORAGE_KEY);
                if (stored) {
                    const hours = parseInt(stored, 10);
                    if (VALID_RANGES.includes(hours)) {
                        this._currentHours = hours;
                    }
                }
            } catch (e) {
                console.warn('Failed to restore time range:', e);
            }
        },

        /**
         * Get current time range in hours
         */
        get: function() {
            return this._currentHours;
        },

        /**
         * Set time range and persist
         */
        set: function(hours) {
            if (!VALID_RANGES.includes(hours)) {
                console.warn('Invalid time range:', hours);
                return;
            }

            this._currentHours = hours;

            try {
                localStorage.setItem(STORAGE_KEY, String(hours));
            } catch (e) {
                console.warn('Failed to persist time range:', e);
            }

            // Dispatch event for listeners
            document.dispatchEvent(new CustomEvent('timeRangeChanged', {
                detail: { hours }
            }));
        },

        /**
         * Get granularity for API calls
         */
        getGranularity: function() {
            return this._currentHours <= 24 ? 'hour' : 'day';
        },

        /**
         * Get human-readable label
         */
        getLabel: function() {
            switch (this._currentHours) {
                case 24: return 'last 24 hours';
                case 168: return 'last 7 days';
                case 720: return 'last 30 days';
                default: return `last ${this._currentHours} hours`;
            }
        }
    };

    // Auto-initialize
    TimeRange.init();

    window.Performance.TimeRange = TimeRange;
})();
