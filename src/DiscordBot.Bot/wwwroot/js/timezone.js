/**
 * Timezone Utilities Module
 * Detects user timezone and converts server UTC timestamps for display.
 *
 * Formatting itself lives in format.js (load it first): browser locale, 12/24h preference, and a
 * zone-less timestamp read as UTC. This module finds the elements and keeps them converted:
 *   <span data-utc="2026-10-03T18:05:00Z" data-format="datetime-short"></span>
 *   <time data-utc="..." data-format="relative"></time>   (auto-refreshing, absolute time on hover)
 * Content inserted after load (AJAX tabs, row templates) is converted by a MutationObserver
 * before the browser paints it; `timezoneUtils.scan(root)` does the same on demand.
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
                const formatter = new Intl.DateTimeFormat(undefined, { timeZoneName: 'short' });
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
            const date = window.Format ? window.Format.parseUtc(utcIsoString) : new Date(utcIsoString);
            if (!date || isNaN(date.getTime())) return '';
            // No locale and no hour12: the browser's own language and clock preference apply.
            const defaultOptions = window.Format ? window.Format.DATE_STYLES.datetime : {
                year: 'numeric', month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit'
            };
            return new Intl.DateTimeFormat(undefined, options || defaultOptions).format(date);
        },

        /**
         * Initializes timezone hidden fields and indicators under `root` (default: the document)
         */
        initTimezoneFields: function(root) {
            const scope = root || document;
            const tz = this.getTimezone();
            scope.querySelectorAll('input[name$="UserTimezone"]').forEach(input => {
                input.value = tz;
            });

            // Update timezone indicator elements
            scope.querySelectorAll('.timezone-indicator').forEach(el => {
                const abbr = this.getTimezoneAbbreviation();
                el.textContent = abbr ? `${tz} (${abbr})` : tz;
            });
        },

        /**
         * Converts every [data-utc] element under `root` (default: the document) to local time.
         * An element is converted once per (timestamp, format) pair, so rescanning is cheap and
         * never rewrites text that is already right. A value that is not a date is left as the
         * server rendered it rather than blanked.
         * @returns {number} how many elements changed
         */
        convertDisplayTimes: function(root) {
            const scope = root || document;
            const elements = [];
            if (scope.nodeType === 1 && scope.hasAttribute && scope.hasAttribute('data-utc')) elements.push(scope);
            scope.querySelectorAll('[data-utc]').forEach(el => elements.push(el));

            let changed = 0;
            elements.forEach(el => {
                const utc = el.getAttribute('data-utc');
                if (!utc) return;
                const format = el.getAttribute('data-format') || 'datetime';
                const signature = utc + '|' + format;
                if (el.getAttribute('data-utc-done') === signature) return;

                if (format === 'relative') {
                    // Hand over to format.js, which keeps the wording fresh.
                    el.setAttribute('data-relative-time', utc);
                    if (window.Format) window.Format.scan(el);
                } else {
                    const text = window.Format
                        ? window.Format.formatDate(utc, format)
                        : this.formatLocalTime(utc, undefined);
                    if (!text) return;
                    if (el.textContent !== text) el.textContent = text;
                }
                el.setAttribute('data-utc-done', signature);
                changed++;
            });
            return changed;
        },

        /**
         * Everything that depends on the viewer's zone, for one subtree. Call it after inserting
         * markup by hand if you cannot wait for the observer.
         */
        scan: function(root) {
            this.initTimezoneFields(root);
            const changed = this.convertDisplayTimes(root);
            if (window.Format) window.Format.scan(root);
            return changed;
        },

        /**
         * Sets datetime-local input value from UTC
         * @param {string} inputId - The input element ID
         * @param {string} utcIsoString - UTC timestamp in ISO format
         */
        setDateTimeLocalFromUtc: function(inputId, utcIsoString) {
            const input = document.getElementById(inputId);
            if (input && utcIsoString) {
                const date = window.Format ? window.Format.parseUtc(utcIsoString) : new Date(utcIsoString);
                if (!date) return;
                // Format as local datetime string (YYYY-MM-DDTHH:mm)
                // datetime-local inputs expect LOCAL time, not UTC
                // Note: date is already in local time when accessed via getFullYear(), getMonth(), etc.
                const year = date.getFullYear();
                const month = String(date.getMonth() + 1).padStart(2, '0');
                const day = String(date.getDate()).padStart(2, '0');
                const hours = String(date.getHours()).padStart(2, '0');
                const minutes = String(date.getMinutes()).padStart(2, '0');
                input.value = `${year}-${month}-${day}T${hours}:${minutes}`;
            }
        },

        /**
         * Sets the default datetime-local value to now + offset minutes in LOCAL time
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
                // Format as local datetime string (YYYY-MM-DDTHH:mm)
                // datetime-local inputs expect LOCAL time, not UTC
                const year = now.getFullYear();
                const month = String(now.getMonth() + 1).padStart(2, '0');
                const day = String(now.getDate()).padStart(2, '0');
                const hours = String(now.getHours()).padStart(2, '0');
                const minutes = String(now.getMinutes()).padStart(2, '0');
                input.value = `${year}-${month}-${day}T${hours}:${minutes}`;
            }
        }
    };

    // Expose globally
    window.timezoneUtils = timezoneUtils;

    // Convert what is already in the page as soon as the parser has reached this script (it sits at
    // the end of <body>, so the markup above it exists) and again when parsing finishes, to catch
    // anything below. Doing it before first paint keeps the dates from visibly changing.
    timezoneUtils.scan(document);
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function() {
            timezoneUtils.scan(document);
            observe();
        });
    } else {
        observe();
    }

    /**
     * Converts content inserted after load. Only element nodes are inspected: this module's own
     * textContent writes add text nodes, which would otherwise re-trigger it forever. The
     * callback runs as a microtask, so converted text is in place before the next paint.
     */
    function observe() {
        if (typeof MutationObserver === 'undefined' || !document.body) return;
        const observer = new MutationObserver(function(mutations) {
            for (const mutation of mutations) {
                mutation.addedNodes.forEach(function(node) {
                    if (node.nodeType !== 1) return;
                    if (node.matches('[data-utc], [data-relative-time]') ||
                        node.querySelector('[data-utc], [data-relative-time], input[name$="UserTimezone"], .timezone-indicator')) {
                        timezoneUtils.scan(node);
                    }
                });
            }
        });
        observer.observe(document.body, { childList: true, subtree: true });
    }
})();
