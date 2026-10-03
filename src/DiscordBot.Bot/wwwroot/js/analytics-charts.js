/**
 * Analytics charts: the helpers the analytics pages share (server, moderation, engagement,
 * Rat Watch). Loaded before the page's own module.
 *
 * - Every chart is built from `ChartTheme.colors()` and is repainted for the active theme:
 *   `create` remembers each chart's `recolor` function and a single `ChartTheme.onChange`
 *   listener calls it when the theme changes (chart-theme.js then redraws the chart).
 *   Axis, grid, legend and tooltip colours come from chart-theme.js; modules set series only.
 * - `heatmap` draws a day-by-hour grid with token classes (theme changes need no script).
 * - A chart is never the only way to read its numbers: each page renders a table of the same
 *   data next to the canvas (`_ChartDataTable`, `_HeatmapDataTable`), and `aria-describedby`
 *   on the canvas points at it.
 * - Dates arrive as calendar days ("2026-10-01"), not instants, so they are labelled in UTC
 *   (the zone they parse in) and never shift to the previous day west of Greenwich.
 *
 * Exposed as window.AnalyticsCharts (and module.exports for tests).
 */
(function (root, factory) {
    'use strict';
    const api = factory(root);
    if (typeof module !== 'undefined' && module.exports) {
        module.exports = api;
    }
    if (typeof window !== 'undefined' && root === window) {
        window.AnalyticsCharts = api;
    }
})(typeof window !== 'undefined' ? window : globalThis, function (root) {
    'use strict';

    const DAYS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];

    // Whole class names, so Tailwind (which scans wwwroot/js) generates them.
    const HEAT_CLASSES = {
        blue: ['bg-accent-blue/20', 'bg-accent-blue/40', 'bg-accent-blue/60', 'bg-accent-blue/80'],
        orange: ['bg-accent-orange/20', 'bg-accent-orange/40', 'bg-accent-orange/60', 'bg-accent-orange/80']
    };

    function formatApi() {
        return root.Format || null;
    }

    /** "1,234" in the viewer's locale. */
    function number(value) {
        const f = formatApi();
        return f && f.number ? f.number(value) : String(value);
    }

    /** "Oct 1" for a calendar day such as "2026-10-01". */
    function dayLabel(dateStr) {
        const f = formatApi();
        if (f && f.formatDate) {
            const text = f.formatDate(dateStr + 'T00:00:00Z', 'date-short', { timeZone: 'UTC' });
            if (text) return text;
        }
        return String(dateStr);
    }

    /** "1.2k" for axis ticks. */
    function compact(value) {
        const n = Number(value);
        return Math.abs(n) >= 1000 ? (n / 1000).toFixed(1) + 'k' : String(n);
    }

    /**
     * One colour for each of `count` bars. A per-bar array, not a single string: Chart.js shares
     * resolved options between bars when the colour is a plain value, and chart-theme.js redraws
     * with update('none'), which leaves shared options as they were, so a bar chart would keep
     * the old theme's colour.
     */
    function each(color, count) {
        return new Array(count).fill(color);
    }

    /** The JSON island a page embeds its chart data in, or null. */
    function readData(id) {
        const el = root.document && root.document.getElementById(id);
        if (!el) return null;
        try {
            return JSON.parse(el.textContent);
        } catch (e) {
            console.error('Analytics data on this page is not valid JSON', e);
            return null;
        }
    }

    function themeColors() {
        return root.ChartTheme.colors();
    }

    let listening = false;

    function listenForTheme() {
        if (listening || !root.ChartTheme) return;
        listening = true;
        root.ChartTheme.onChange(function (chart, colors) {
            if (typeof chart.$recolor === 'function') chart.$recolor(chart, colors);
        });
    }

    /**
     * Replaces a chart that cannot be drawn (Chart.js failed to load) with a plain message. The
     * data table beside it still carries the numbers.
     */
    function showUnavailable(canvas) {
        const holder = canvas.parentElement;
        if (!holder) return;
        holder.style.minHeight = '';
        holder.style.maxHeight = '';
        if (root.EmptyState && root.EmptyState.error) {
            root.EmptyState.error(holder, {
                title: 'Chart unavailable',
                description: 'The chart could not be drawn. The same figures are in the data table for this chart.',
                size: 'compact'
            });
        } else {
            holder.textContent = 'The chart could not be drawn.';
        }
    }

    /**
     * Builds one chart.
     * @param {string} canvasId
     * @param {function(object): object} build gets ChartTheme.colors(), returns the Chart.js config
     * @param {function(Chart, object)} [recolor] rewrites series colours for a new theme
     * @returns {Chart|null} null when the canvas is not on the page (empty state shown instead)
     */
    function create(canvasId, build, recolor) {
        const canvas = root.document.getElementById(canvasId);
        if (!canvas) return null;
        if (!root.Chart || !root.ChartTheme) {
            showUnavailable(canvas);
            return null;
        }
        root.ChartTheme.ensureRegistered();
        listenForTheme();
        const chart = new root.Chart(canvas, build(themeColors()));
        chart.$recolor = recolor || null;
        return chart;
    }

    /**
     * Intensity step 0-4 for a count against the maximum: 0 is "none".
     * @returns {number}
     */
    function heatStep(count, max) {
        if (!count || max <= 0) return 0;
        const ratio = count / max;
        if (ratio < 0.25) return 1;
        if (ratio < 0.5) return 2;
        if (ratio < 0.75) return 3;
        return 4;
    }

    function el(tag, className, text) {
        const node = root.document.createElement(tag);
        if (className) node.className = className;
        if (text !== undefined) node.textContent = text;
        return node;
    }

    /**
     * Draws a day-of-week by hour-of-day grid into `container`.
     * @param {HTMLElement} container
     * @param {Array<{dayOfWeek:number, hour:number}>} cells the count is read from `countKey`
     * @param {{countKey: string, unit: string, unitPlural: string, accent?: 'blue'|'orange', note?: string}} options
     */
    function heatmap(container, cells, options) {
        if (!container || !cells || cells.length === 0) return;
        const accent = HEAT_CLASSES[options.accent] || HEAT_CLASSES.blue;
        const grid = Array.from({ length: 7 }, () => new Array(24).fill(0));
        let max = 0;
        let peak = null;
        cells.forEach(function (cell) {
            const count = Number(cell[options.countKey]) || 0;
            if (cell.dayOfWeek < 0 || cell.dayOfWeek > 6 || cell.hour < 0 || cell.hour > 23) return;
            grid[cell.dayOfWeek][cell.hour] = count;
            if (count > max) {
                max = count;
                peak = { day: cell.dayOfWeek, hour: cell.hour, count: count };
            }
        });

        const hourText = function (hour) { return String(hour).padStart(2, '0') + ':00'; };

        // The grid is a picture of the table beside it, so it is hidden from assistive technology.
        const scroller = el('div', 'overflow-x-auto');
        scroller.setAttribute('aria-hidden', 'true');
        const rows = el('div', 'space-y-1');
        rows.style.minWidth = '22rem';

        const header = el('div', 'flex items-center gap-1');
        header.appendChild(el('div', 'w-10 shrink-0'));
        for (let hour = 0; hour < 24; hour++) {
            header.appendChild(el('div', 'flex-1 text-center text-[10px] text-text-tertiary',
                hour % 6 === 0 ? String(hour) : ''));
        }
        rows.appendChild(header);

        DAYS.forEach(function (day, dayIndex) {
            const row = el('div', 'flex items-center gap-1');
            row.appendChild(el('div', 'w-10 shrink-0 text-xs font-medium text-text-secondary', day));
            for (let hour = 0; hour < 24; hour++) {
                const count = grid[dayIndex][hour];
                const step = heatStep(count, max);
                const cell = el('div', 'flex-1 rounded-sm ' + (step === 0 ? 'bg-bg-tertiary' : accent[step - 1]));
                cell.style.aspectRatio = '1 / 1';
                cell.style.minWidth = '0.5rem';
                cell.title = day + ' ' + hourText(hour) + ': ' + number(count) + ' ' +
                    (count === 1 ? options.unit : options.unitPlural);
                row.appendChild(cell);
            }
            rows.appendChild(row);
        });
        scroller.appendChild(rows);

        const legend = el('div', 'mt-4 flex items-center justify-end gap-2 text-xs text-text-tertiary');
        legend.setAttribute('aria-hidden', 'true');
        legend.appendChild(el('span', '', 'Fewer'));
        legend.appendChild(el('div', 'h-4 w-4 rounded-sm bg-bg-tertiary'));
        accent.forEach(function (cls) { legend.appendChild(el('div', 'h-4 w-4 rounded-sm ' + cls)); });
        legend.appendChild(el('span', '', 'More'));

        container.textContent = '';
        if (options.note) container.appendChild(el('p', 'mb-3 text-xs text-text-tertiary', options.note));
        container.appendChild(scroller);
        container.appendChild(legend);
        if (peak) {
            container.appendChild(el('p', 'mt-3 text-sm text-text-secondary',
                'Busiest: ' + DAYS[peak.day] + ' at ' + hourText(peak.hour) + ' with ' +
                number(peak.count) + ' ' + (peak.count === 1 ? options.unit : options.unitPlural) + '.'));
        }
    }

    return { DAYS, number, dayLabel, compact, each, readData, create, heatmap, heatStep };
});
