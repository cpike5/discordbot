/**
 * Classic script (not an ES module - loaded from App.razor after shell.js, same shape as
 * error-pages.js/landing.js) that converts every `<LocalTime>` component's rendered
 * `[data-utc]`/`[data-format]` markup to the viewer's local time. A faithful port of
 * wwwroot/js/timezone.js's `convertDisplayTimes`/`initTimezoneFields` (same formats, same
 * Intl.DateTimeFormat options, same `hour12: true`) - see
 * Blazor/Shared/Primitives/LocalTime.razor and docs/plans/blazor-port-plan.md Phase 4 cluster 4a
 * ("adds the browser.js timezone conversion (or a LocalTime component) that Search's own
 * data-utc rows pick up at the same time").
 *
 * Exposed as `window.DiscordBotLocalTime.convert(root?)` so:
 *   - this script's own DOMContentLoaded/enhancedload handlers can call it for the whole
 *     document, matching timezone.js's own auto-init under the legacy shell;
 *   - wwwroot/js/blazor/browser.js's `convertLocalTimes(root)` export (used by
 *     Blazor/Interop/BrowserInterop.cs's ConvertLocalTimesAsync) can call the *same* function
 *     after an interactive component re-renders new rows, without a second copy of the
 *     conversion logic.
 *
 * Idempotent per node via `data-localtime-converted="1"` (set once a node's text is rewritten) -
 * unlike timezone.js, which re-walks and re-formats every `[data-utc]` node on every call
 * (harmless there because it only ever runs once, on DOMContentLoaded), this script's `convert`
 * can be called many times over the life of one circuit (every enhancedload, every interactive
 * re-render), so a node already converted is skipped rather than reformatted - reformatting is
 * pure but wasted work at scale, and the flag also lets a future caller tell "already handled"
 * apart from "server fallback still showing" if it ever needs to.
 */
(function () {
    'use strict';

    function getTimezone() {
        try {
            return Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC';
        } catch (e) {
            return 'UTC';
        }
    }

    function getTimezoneAbbreviation() {
        try {
            var formatter = new Intl.DateTimeFormat('en-US', { timeZoneName: 'short' });
            var parts = formatter.formatToParts(new Date());
            var tzPart = parts.find(function (p) { return p.type === 'timeZoneName'; });
            return tzPart ? tzPart.value : '';
        } catch (e) {
            return '';
        }
    }

    function formatLocalTime(utcIsoString, options) {
        var date = new Date(utcIsoString);
        return date.toLocaleString('en-US', options);
    }

    function optionsForFormat(format) {
        switch (format) {
            case 'date':
                return { year: 'numeric', month: 'short', day: 'numeric' };
            case 'date-short':
                return { month: 'short', day: 'numeric' };
            case 'datetime-short':
                return { month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit', hour12: true };
            case 'time':
                return { hour: 'numeric', minute: '2-digit', hour12: true };
            case 'datetime-seconds':
                return {
                    year: 'numeric', month: 'short', day: 'numeric',
                    hour: 'numeric', minute: '2-digit', second: '2-digit', hour12: true
                };
            default:
                return { year: 'numeric', month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit', hour12: true };
        }
    }

    /**
     * Converts every not-yet-converted `[data-utc]` element under `root` (default: the whole
     * document) to the viewer's local time.
     *
     * @param {ParentNode} [root]
     */
    function convert(root) {
        var scope = root || document;
        scope.querySelectorAll('[data-utc]:not([data-localtime-converted="1"])').forEach(function (el) {
            var utc = el.getAttribute('data-utc');
            if (!utc) {
                return;
            }
            var format = el.getAttribute('data-format') || 'datetime';
            el.textContent = formatLocalTime(utc, optionsForFormat(format));
            el.setAttribute('data-localtime-converted', '1');
        });
    }

    /**
     * Fills `input[name$="UserTimezone"]` with the IANA zone and `.timezone-indicator` elements
     * with a "Zone (ABBR)" label - ports timezone.js's `initTimezoneFields`.
     *
     * @param {ParentNode} [root]
     */
    function initTimezoneFields(root) {
        var scope = root || document;
        var tz = getTimezone();
        scope.querySelectorAll('input[name$="UserTimezone"]').forEach(function (input) {
            input.value = tz;
        });
        scope.querySelectorAll('.timezone-indicator').forEach(function (el) {
            var abbr = getTimezoneAbbreviation();
            el.textContent = abbr ? tz + ' (' + abbr + ')' : tz;
        });
    }

    function runFullScan() {
        initTimezoneFields();
        convert();
    }

    window.DiscordBotLocalTime = {
        convert: convert,
        initTimezoneFields: initTimezoneFields
    };

    var enhancedLoadHooked = false;

    function hookEnhancedLoad() {
        if (!enhancedLoadHooked && window.Blazor && typeof window.Blazor.addEventListener === 'function') {
            window.Blazor.addEventListener('enhancedload', runFullScan);
            enhancedLoadHooked = true;
        }
    }

    document.addEventListener('DOMContentLoaded', function () {
        runFullScan();
        // By DOMContentLoaded time blazor.web.js (end of <body>) has already run synchronously
        // during parsing, so window.Blazor is defined here even though it wasn't when this
        // script's own top-level code ran (it's loaded in <body>, right after shell.js, so this
        // is largely belt-and-braces - see error-pages.js's header comment for the fuller
        // rationale of running the same check at both points).
        hookEnhancedLoad();
    });
    if (document.readyState !== 'loading') {
        runFullScan();
        hookEnhancedLoad();
    }
})();
