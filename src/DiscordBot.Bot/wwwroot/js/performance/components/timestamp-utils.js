/**
 * Performance Dashboard - Timestamp Utilities
 * UTC/local timezone conversion for display
 */
(function() {
    'use strict';

    window.Performance = window.Performance || {};

    const TimestampUtils = {
        /**
         * Convert all UTC timestamps in container to local timezone
         */
        convertTimestamps: function(container) {
            container = container || document;
            const elements = container.querySelectorAll('[data-utc-time]');

            elements.forEach(el => {
                const utc = el.getAttribute('data-utc-time');
                const format = el.getAttribute('data-format');
                if (!utc) return;

                try {
                    const date = new Date(utc);

                    if (format === 'relative') {
                        el.textContent = this.formatRelative(date);
                    } else {
                        el.textContent = this.formatAbsolute(date);
                    }
                } catch (e) {
                    console.error('Failed to parse timestamp:', utc, e);
                }
            });
        },

        /**
         * Format as relative time
         */
        formatRelative: function(date) {
            const now = new Date();
            const diff = now - date;
            const mins = Math.floor(diff / 60000);
            const hrs = Math.floor(mins / 60);
            const days = Math.floor(hrs / 24);

            if (days >= 1) {
                return days === 1 ? '1 day ago' : `${days} days ago`;
            } else if (hrs >= 1) {
                return hrs === 1 ? '1 hour ago' : `${hrs} hours ago`;
            } else if (mins >= 1) {
                return mins === 1 ? '1 minute ago' : `${mins} minutes ago`;
            }
            return 'Just now';
        },

        /**
         * Format as absolute date/time
         */
        formatAbsolute: function(date) {
            return date.toLocaleDateString('en-US', {
                month: 'short',
                day: '2-digit',
                year: 'numeric'
            }) + ' at ' + date.toLocaleTimeString('en-US', {
                hour: '2-digit',
                minute: '2-digit',
                hour12: false
            });
        },

        /**
         * Format for chart labels based on granularity
         */
        formatChartLabel: function(date, isDaily) {
            if (isDaily) {
                return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
            }
            const month = date.toLocaleDateString('en-US', { month: 'short' });
            const day = date.getDate();
            const hour = date.toLocaleTimeString('en-US', { hour: 'numeric', hour12: true });
            return `${month} ${day}, ${hour}`;
        }
    };

    window.Performance.TimestampUtils = TimestampUtils;
})();
