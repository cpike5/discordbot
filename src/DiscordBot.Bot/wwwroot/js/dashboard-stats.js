/**
 * DashboardStats - writes the dashboard's hero numbers into the page.
 *
 * The field names are the ones DashboardStatsDto sends (camelCased) for the StatsUpdated hub
 * event and for GET ?handler=Stats, so a field renamed on the server is renamed here. Each hero
 * value carries a matching data-stat-* attribute (see Index.cshtml.cs, BuildHeroMetrics).
 * Numbers go through Format so they match what the server rendered for the same locale.
 *
 * Exposed as window.DashboardStats (browser) and module.exports (Node/tests).
 */
(function (root, factory) {
    if (typeof module === 'object' && module.exports) {
        module.exports = factory();
    } else {
        root.DashboardStats = factory();
    }
})(typeof self !== 'undefined' ? self : this, function () {
    'use strict';

    /** DTO field -> the attribute on the card value that shows it, and how it is written. */
    var FIELDS = {
        totalServers: { selector: '[data-stat-servers]', text: function (v, fmt) { return fmt.number(v); } },
        totalMembers: { selector: '[data-stat-members]', text: function (v, fmt) { return fmt.number(v); } },
        commandsLast24Hours: { selector: '[data-stat-commands]', text: function (v, fmt) { return fmt.number(v); } },
        uptimePercent24Hours: { selector: '[data-stat-uptime]', text: function (v, fmt) { return fmt.number(v, { maximumFractionDigits: 1 }) + '%'; } }
    };

    /** Used when Format has not loaded: plain English-style grouping. */
    var FALLBACK_FORMAT = {
        number: function (value, options) {
            var digits = options && options.maximumFractionDigits;
            return Number(value).toLocaleString(undefined, digits === undefined ? undefined : { maximumFractionDigits: digits });
        }
    };

    /**
     * Applies a stats payload under `scope` (default: the document). A field that is missing or
     * not a number leaves its card untouched, so a partial payload never blanks a value.
     * @returns {number} how many cards were updated
     */
    function apply(scope, stats, format) {
        if (!scope || !stats) return 0;
        var fmt = format || FALLBACK_FORMAT;
        var updated = 0;
        Object.keys(FIELDS).forEach(function (field) {
            var value = stats[field];
            if (typeof value !== 'number' || !isFinite(value)) return;
            var element = scope.querySelector(FIELDS[field].selector);
            if (!element) return;
            var text = FIELDS[field].text(value, fmt);
            if (element.textContent !== text) element.textContent = text;
            updated++;
        });
        return updated;
    }

    return { FIELDS: FIELDS, apply: apply };
});
