/**
 * Format: dates, relative time, plurals, numbers, durations and currency.
 *
 * One place for how the app writes these (UX plan D6). Everything uses the browser's locale and its
 * 12/24-hour preference: the locale is never hard-coded and `hour12` is never forced. Dates come
 * from the server as UTC and are shown in the viewer's own time zone.
 *
 * `Helpers/DisplayFormat.cs` mirrors the helpers the server needs (fallback text for markup,
 * plurals, numbers, durations); keep the two in step.
 *
 * Every function takes an optional `{ locale, timeZone, now }` so tests and unusual callers are
 * deterministic; production code leaves them out.
 *
 * Markup: `<time data-relative-time="2026-10-03T12:00:00Z"></time>` shows "5 minutes ago", refreshes
 * itself, and shows the absolute time on hover and keyboard focus. `Format.scan(root)` upgrades
 * elements inserted after load (timezone.js calls it from its MutationObserver).
 */
(function (root, factory) {
    'use strict';
    const api = factory(root);
    if (typeof module !== 'undefined' && module.exports) {
        module.exports = api;
    }
    if (typeof window !== 'undefined' && root === window) {
        window.Format = api;
    }
})(typeof window !== 'undefined' ? window : globalThis, function (root) {
    'use strict';

    /** Date styles understood by `formatDate`; the names match the `data-format` attribute. */
    const DATE_STYLES = {
        'date': { year: 'numeric', month: 'short', day: 'numeric' },
        'date-short': { month: 'short', day: 'numeric' },
        'datetime': { year: 'numeric', month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' },
        'datetime-short': { month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' },
        'datetime-seconds': {
            year: 'numeric', month: 'short', day: 'numeric',
            hour: 'numeric', minute: '2-digit', second: '2-digit'
        },
        'time': { hour: 'numeric', minute: '2-digit' },
        'full': {
            weekday: 'long', year: 'numeric', month: 'long', day: 'numeric',
            hour: 'numeric', minute: '2-digit', second: '2-digit', timeZoneName: 'short'
        }
    };

    const MINUTE = 60 * 1000;
    const HOUR = 60 * MINUTE;
    const DAY = 24 * HOUR;

    /** Relative wording is used up to this age; older values show as a date. */
    const RELATIVE_LIMIT_DAYS = 30;

    function resolve(opts) {
        return {
            // `undefined` lets Intl use the browser's own language and regional preferences.
            locale: opts && opts.locale,
            timeZone: opts && opts.timeZone,
            now: opts && opts.now !== undefined ? toDate(opts.now) : new Date()
        };
    }

    /**
     * Parses a timestamp into a Date. A string with no zone designator is treated as UTC, because
     * the server stores UTC and a `DateTime` of kind Unspecified serializes without a `Z`; the
     * browser would otherwise read it as local time and shift it by the viewer's offset.
     * @returns {Date|null} null when the value is empty or not a date.
     */
    function parseUtc(value) {
        const d = toDate(value);
        return d && !isNaN(d.getTime()) ? d : null;
    }

    function toDate(value) {
        if (value instanceof Date) return value;
        if (typeof value === 'number') return new Date(value);
        if (typeof value !== 'string' || value.trim() === '') return null;
        let text = value.trim();
        // ISO date-time without an offset: 2026-10-03T12:00:00 or 2026-10-03 12:00:00(.123)
        if (/^\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}(:\d{2}(\.\d+)?)?$/.test(text)) {
            text = text.replace(' ', 'T') + 'Z';
        }
        return new Date(text);
    }

    function intlOptions(style) {
        return Object.assign({}, DATE_STYLES[style] || DATE_STYLES.datetime);
    }

    /**
     * Formats a date in the viewer's locale and time zone.
     * @param {string|number|Date} value UTC timestamp.
     * @param {string} [style] one of the DATE_STYLES names; default 'datetime'.
     * @returns {string} '' when the value is not a date.
     */
    function formatDate(value, style, opts) {
        const d = parseUtc(value);
        if (!d) return '';
        const o = resolve(opts);
        const options = intlOptions(style || 'datetime');
        if (o.timeZone) options.timeZone = o.timeZone;
        try {
            return new Intl.DateTimeFormat(o.locale, options).format(d);
        } catch (e) {
            return d.toISOString();
        }
    }

    /** The full absolute time with zone, used for the hover/focus tooltip. */
    function formatAbsolute(value, opts) {
        const d = parseUtc(value);
        if (!d) return '';
        const o = resolve(opts);
        const options = {
            year: 'numeric', month: 'short', day: 'numeric',
            hour: 'numeric', minute: '2-digit', second: '2-digit', timeZoneName: 'short'
        };
        if (o.timeZone) options.timeZone = o.timeZone;
        try {
            return new Intl.DateTimeFormat(o.locale, options).format(d);
        } catch (e) {
            return d.toISOString();
        }
    }

    /**
     * How long ago (or from now) a time is, in words: "just now", "5 minutes ago", "yesterday",
     * "in 2 hours". After 30 days it falls back to a date, since "47 days ago" is harder to place
     * than a date.
     */
    function relativeTime(value, opts) {
        const d = parseUtc(value);
        if (!d) return '';
        const o = resolve(opts);
        const diff = d.getTime() - o.now.getTime(); // negative = past
        const abs = Math.abs(diff);

        if (abs >= RELATIVE_LIMIT_DAYS * DAY) return formatDate(d, 'date', opts);

        let rtf;
        try {
            rtf = new Intl.RelativeTimeFormat(o.locale, { numeric: 'auto' });
        } catch (e) {
            return formatDate(d, 'datetime', opts);
        }

        if (abs < 45 * 1000) return rtf.format(0, 'second'); // "now"
        if (abs < HOUR) return rtf.format(signedRound(diff / MINUTE), 'minute');
        if (abs < DAY) return rtf.format(signedRound(diff / HOUR), 'hour');
        return rtf.format(signedRound(diff / DAY), 'day');
    }

    // Round toward the nearer whole unit, but never to 0 (that would read as "now").
    function signedRound(n) {
        const r = Math.round(n);
        return r === 0 ? (n < 0 ? -1 : 1) : r;
    }

    /**
     * "1 server" / "2 servers". `other` defaults to `one + 's'`. The count is formatted with the
     * locale's digit grouping.
     */
    function plural(count, one, other, opts) {
        const n = Number(count);
        let category = n === 1 ? 'one' : 'other';
        try {
            category = new Intl.PluralRules(resolve(opts).locale).select(n);
        } catch (e) { /* keep the default */ }
        const word = category === 'one' ? one : (other !== undefined ? other : one + 's');
        return number(n, opts) + ' ' + word;
    }

    /**
     * The text split into user-perceived characters (an emoji or a letter with its accent is one),
     * so slicing never leaves half of one. Uses Intl.Segmenter, else code points.
     */
    function graphemes(text) {
        const str = text === null || text === undefined ? '' : String(text);
        if (typeof Intl !== 'undefined' && typeof Intl.Segmenter === 'function') {
            return Array.from(new Intl.Segmenter(undefined, { granularity: 'grapheme' }).segment(str), (part) => part.segment);
        }
        return Array.from(str);
    }

    /**
     * The first `count` characters of a name, upper-cased, for an avatar placeholder; blank text
     * gives `fallback`. The server twin is TextDisplay.Initials. Emoji stay whole.
     */
    function initials(text, count, fallback) {
        const trimmed = String(text === null || text === undefined ? '' : text).trim();
        const taken = graphemes(trimmed).slice(0, count === undefined ? 2 : count).join('');
        return taken ? taken.toUpperCase() : (fallback === undefined ? '?' : fallback);
    }

    /** Text cut to at most `max` characters (ellipsis not counted), never inside an emoji. */
    function truncate(text, max, ellipsis) {
        const parts = graphemes(text);
        if (parts.length <= max) return parts.join('');
        return parts.slice(0, max).join('') + (ellipsis === undefined ? '...' : ellipsis);
    }

    /** A number with the locale's grouping and decimal marks. */
    function number(value, opts) {
        const n = Number(value);
        if (!isFinite(n)) return '';
        const o = resolve(opts);
        const options = {};
        if (opts && opts.maximumFractionDigits !== undefined) options.maximumFractionDigits = opts.maximumFractionDigits;
        if (opts && opts.minimumFractionDigits !== undefined) options.minimumFractionDigits = opts.minimumFractionDigits;
        try {
            return new Intl.NumberFormat(o.locale, options).format(n);
        } catch (e) {
            return String(n);
        }
    }

    /**
     * A length of time as at most `maxUnits` (default 2) of its largest units: "2d 5h", "5h 30m",
     * "45s", "<1s".
     * @param {number} ms milliseconds.
     */
    function duration(ms, opts) {
        const total = Number(ms);
        if (!isFinite(total) || total < 0) return '';
        const maxUnits = opts && opts.maxUnits ? opts.maxUnits : 2;
        const units = [['d', DAY], ['h', HOUR], ['m', MINUTE], ['s', 1000]];
        let rest = Math.floor(total / 1000) * 1000;
        if (rest === 0) return '<1s';
        const parts = [];
        for (const [label, size] of units) {
            if (parts.length >= maxUnits) break;
            const amount = Math.floor(rest / size);
            if (amount > 0 || parts.length > 0) {
                // Once a unit has started, a zero in the middle ("2d 0h") is dropped, not shown.
                if (amount > 0) parts.push(amount + label);
                rest -= amount * size;
            }
        }
        return parts.join(' ');
    }

    /**
     * An amount of money. By default `symbolOrCode` is a virtual-currency symbol and is written
     * after the whole-unit amount, as the Discord commands do ("1,250 🪙"). Pass `{ iso: true }`
     * when it is an ISO 4217 code ("USD") to use the locale's currency style. The two are not
     * guessed apart: "GEM" is both a valid code shape and a plausible virtual currency.
     */
    function currency(amount, symbolOrCode, opts) {
        const n = Number(amount);
        if (!isFinite(n)) return '';
        const o = resolve(opts);
        const code = typeof symbolOrCode === 'string' ? symbolOrCode.trim() : '';
        if (opts && opts.iso && /^[A-Za-z]{3}$/.test(code)) {
            try {
                return new Intl.NumberFormat(o.locale, { style: 'currency', currency: code.toUpperCase() }).format(n);
            } catch (e) { /* not a real ISO code: fall through and treat it as a symbol */ }
        }
        return (number(n, { locale: o.locale, maximumFractionDigits: 0 }) + ' ' + code).trim();
    }

    // ---- Auto-refreshing relative time in the page ---------------------------------------------

    const SELECTOR = '[data-relative-time]';
    const REFRESH_MS = 30 * 1000;
    let timer = null;
    let tooltip = null;
    let tooltipFor = null;
    let tooltipDescribedBy = null; // the element's own aria-describedby, restored on hide
    let tooltipTitle = null;       // its title, held back while the tooltip shows so it is not doubled

    function describe(el) {
        const iso = el.getAttribute('data-relative-time');
        const text = relativeTime(iso);
        if (text && el.textContent !== text) el.textContent = text;
        return text;
    }

    const FOCUSABLE = 'a[href], button, input, select, textarea, summary, [tabindex], [contenteditable]';

    function needsOwnTabStop(el) {
        if (!el.closest) return true;
        return !el.closest(FOCUSABLE) && !el.closest('table');
    }

    function bindElement(el) {
        const iso = el.getAttribute('data-relative-time');
        if (!parseUtc(iso)) return;
        const abs = formatAbsolute(iso);
        el.setAttribute('data-absolute', abs);
        el.classList.add('relative-time');
        if (el.tagName === 'TIME' && !el.hasAttribute('datetime')) {
            el.setAttribute('datetime', parseUtc(iso).toISOString());
        }
        // Keyboard users get the absolute time too. A lone timestamp becomes a Tab stop so focus can
        // show the tooltip; one inside a focusable control (its focus already shows the tooltip) or
        // in a table (a Tab stop per row is too many) does not, and carries the absolute time as its
        // title instead, which assistive technology reads.
        if (el.hasAttribute('tabindex')) {
            // already focusable: nothing to add
        } else if (needsOwnTabStop(el)) {
            el.setAttribute('tabindex', '0');
        } else if (!el.hasAttribute('title')) {
            el.setAttribute('title', abs);
        }
        describe(el);
        el.setAttribute('data-relative-bound', 'true');
    }

    /** Binds every relative-time element under `rootNode` (default: the document). */
    function scan(rootNode) {
        if (typeof document === 'undefined') return 0;
        const scope = rootNode || document;
        const found = [];
        if (scope.nodeType === 1 && scope.matches && scope.matches(SELECTOR)) found.push(scope);
        if (scope.querySelectorAll) scope.querySelectorAll(SELECTOR).forEach(el => found.push(el));
        let bound = 0;
        found.forEach(el => {
            if (el.getAttribute('data-relative-bound') === 'true' &&
                el.getAttribute('data-relative-source') === el.getAttribute('data-relative-time')) {
                return;
            }
            bindElement(el);
            el.setAttribute('data-relative-source', el.getAttribute('data-relative-time'));
            bound++;
        });
        if (found.length > 0) startTimer();
        return bound;
    }

    /** Rewrites every bound element's wording against the current time. */
    function refresh() {
        if (typeof document === 'undefined') return;
        document.querySelectorAll(SELECTOR + '[data-relative-bound="true"]').forEach(describe);
    }

    function startTimer() {
        if (timer !== null || typeof setInterval === 'undefined') return;
        timer = setInterval(function () {
            // No point repainting words nobody can see.
            if (typeof document !== 'undefined' && document.hidden) return;
            refresh();
        }, REFRESH_MS);
    }

    function stopTimer() {
        if (timer !== null) {
            clearInterval(timer);
            timer = null;
        }
    }

    // ---- Tooltip: absolute time on hover and focus ---------------------------------------------

    function ensureTooltip() {
        if (tooltip) return tooltip;
        tooltip = document.createElement('div');
        tooltip.id = 'format-tooltip';
        tooltip.className = 'format-tooltip';
        tooltip.setAttribute('role', 'tooltip');
        tooltip.hidden = true;
        document.body.appendChild(tooltip);
        return tooltip;
    }

    function showTooltip(el) {
        const abs = el.getAttribute('data-absolute');
        if (!abs) return;
        const tip = ensureTooltip();
        hideTooltip();
        tip.textContent = abs;
        tip.hidden = false;
        tooltipFor = el;
        tooltipDescribedBy = el.getAttribute('aria-describedby');
        el.setAttribute('aria-describedby', tooltipDescribedBy ? tooltipDescribedBy + ' ' + tip.id : tip.id);
        if (el.getAttribute('title') === abs) {
            tooltipTitle = abs;
            el.removeAttribute('title');
        }

        // Fixed position, clamped to the viewport so it never creates horizontal scroll.
        const rect = el.getBoundingClientRect();
        const tipRect = tip.getBoundingClientRect();
        const margin = 8;
        let left = rect.left + rect.width / 2 - tipRect.width / 2;
        left = Math.max(margin, Math.min(left, document.documentElement.clientWidth - tipRect.width - margin));
        let top = rect.top - tipRect.height - 6;
        if (top < margin) top = rect.bottom + 6;
        tip.style.left = left + 'px';
        tip.style.top = top + 'px';
    }

    function hideTooltip() {
        if (!tooltip || tooltip.hidden) return;
        tooltip.hidden = true;
        if (tooltipFor) {
            if (tooltipDescribedBy) tooltipFor.setAttribute('aria-describedby', tooltipDescribedBy);
            else tooltipFor.removeAttribute('aria-describedby');
            if (tooltipTitle !== null) tooltipFor.setAttribute('title', tooltipTitle);
        }
        tooltipFor = null;
        tooltipDescribedBy = null;
        tooltipTitle = null;
    }

    function closestRelative(target) {
        return target && target.closest ? target.closest(SELECTOR + '[data-absolute]') : null;
    }

    function installTooltipEvents() {
        document.addEventListener('mouseover', function (e) {
            const el = closestRelative(e.target);
            if (el) showTooltip(el);
        });
        document.addEventListener('mouseout', function (e) {
            if (closestRelative(e.target) && document.activeElement !== closestRelative(e.target)) hideTooltip();
        });
        document.addEventListener('focusin', function (e) {
            // A timestamp inside a focused link or button has no Tab stop of its own: use the one in it.
            const el = closestRelative(e.target) ||
                (e.target && e.target.querySelector ? e.target.querySelector(SELECTOR + '[data-absolute]') : null);
            if (el) showTooltip(el);
        });
        document.addEventListener('focusout', hideTooltip);
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && tooltip && !tooltip.hidden) hideTooltip();
        });
        window.addEventListener('scroll', hideTooltip, true);
    }

    function init() {
        scan(document);
        installTooltipEvents();
        document.addEventListener('visibilitychange', function () {
            if (!document.hidden) refresh();
        });
    }

    if (typeof document !== 'undefined' && typeof window !== 'undefined' && root === window) {
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', init);
        } else {
            init();
        }
    }

    return {
        DATE_STYLES,
        parseUtc,
        formatDate,
        formatAbsolute,
        relativeTime,
        plural,
        graphemes,
        initials,
        truncate,
        number,
        duration,
        currency,
        scan,
        refresh,
        stopTimer
    };
});
