/**
 * Timezone Utilities Module
 * Detects user timezone and provides conversion helpers
 */
(function() {
    'use strict';

    const timezoneUtils = {
        /**
         * Gets the user's IANA timezone name
         * @returns {string} IANA timezone identifier (e.g., "America/New_York")
         */
        getTimezone: function() {
            try {
                return Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC';
            } catch (e) {
                console.warn('Timezone detection failed, defaulting to UTC', e);
                return 'UTC';
            }
        },

        /**
         * Gets a display-friendly timezone abbreviation
         * @returns {string} Timezone abbreviation (e.g., "EST", "PST")
         */
        getTimezoneAbbreviation: function() {
            try {
                const date = new Date();
                const formatter = new Intl.DateTimeFormat('en-US', { timeZoneName: 'short' });
                const parts = formatter.formatToParts(date);
                const tzPart = parts.find(p => p.type === 'timeZoneName');
                return tzPart ? tzPart.value : '';
            } catch (e) {
                return '';
            }
        },

        /**
         * Converts a UTC ISO string to local time display
         * @param {string} utcIsoString - UTC timestamp in ISO format
         * @param {Object} options - Intl.DateTimeFormat options
         * @returns {string} Formatted local time string
         */
        formatLocalTime: function(utcIsoString, options) {
            const date = new Date(utcIsoString);
            const defaultOptions = {
                year: 'numeric',
                month: 'short',
                day: 'numeric',
                hour: 'numeric',
                minute: '2-digit',
                hour12: true
            };
            return date.toLocaleString('en-US', options || defaultOptions);
        },

        /**
         * Initializes timezone hidden fields on the page
         */
        initTimezoneFields: function() {
            const tz = this.getTimezone();
            document.querySelectorAll('input[name$="UserTimezone"]').forEach(input => {
                input.value = tz;
            });

            // Update timezone indicator elements
            document.querySelectorAll('.timezone-indicator').forEach(el => {
                const abbr = this.getTimezoneAbbreviation();
                el.textContent = abbr ? `${tz} (${abbr})` : tz;
            });
        },

        /**
         * Converts all elements with data-utc attribute to local time
         */
        convertDisplayTimes: function() {
            document.querySelectorAll('[data-utc]').forEach(el => {
                const utc = el.getAttribute('data-utc');
                if (utc) {
                    const format = el.getAttribute('data-format') || 'datetime';
                    let options;
                    switch (format) {
                        case 'date':
                            options = { year: 'numeric', month: 'short', day: 'numeric' };
                            break;
                        case 'time':
                            options = { hour: 'numeric', minute: '2-digit', hour12: true };
                            break;
                        default:
                            options = {
                                year: 'numeric',
                                month: 'short',
                                day: 'numeric',
                                hour: 'numeric',
                                minute: '2-digit',
                                hour12: true
                            };
                    }
                    el.textContent = this.formatLocalTime(utc, options);
                }
            });
        },

        /**
         * Sets datetime-local input value from UTC
         * @param {string} inputId - The input element ID
         * @param {string} utcIsoString - UTC timestamp in ISO format
         */
        setDateTimeLocalFromUtc: function(inputId, utcIsoString) {
            const input = document.getElementById(inputId);
            if (input && utcIsoString) {
                const date = new Date(utcIsoString);
                // Format as YYYY-MM-DDTHH:mm for datetime-local input
                const local = new Date(date.getTime() - (date.getTimezoneOffset() * 60000));
                input.value = local.toISOString().slice(0, 16);
            }
        },

        /**
         * Sets the default datetime-local value to now + offset minutes
         * @param {string} inputId - The input element ID
         * @param {number} offsetMinutes - Minutes to add to current time
         */
        setDefaultDateTime: function(inputId, offsetMinutes) {
            const input = document.getElementById(inputId);
            if (input && !input.value) {
                const now = new Date();
                now.setMinutes(now.getMinutes() + (offsetMinutes || 5));
                // Round to next 5 minutes
                now.setMinutes(Math.ceil(now.getMinutes() / 5) * 5);
                now.setSeconds(0);
                now.setMilliseconds(0);
                input.value = now.toISOString().slice(0, 16);
            }
        }
    };

    // Expose globally
    window.timezoneUtils = timezoneUtils;

    // Auto-initialize on DOMContentLoaded
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function() {
            timezoneUtils.initTimezoneFields();
            timezoneUtils.convertDisplayTimes();
        });
    } else {
        timezoneUtils.initTimezoneFields();
        timezoneUtils.convertDisplayTimes();
    }
})();
