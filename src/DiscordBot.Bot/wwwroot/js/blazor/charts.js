/**
 * Blazor interop module: Chart.js lifecycle.
 *
 * Wrapped by `Blazor/Interop/ChartInterop.cs`. Chart.js is vendored (npm
 * package `chart.js`, copied to `/lib/chart.js/chart.umd.js` by the
 * `build:vendor` npm script — see package.json) rather than loaded from a
 * CDN. It is loaded lazily on first use: if `window.Chart` is already
 * defined (a legacy Razor page loaded it from the CDN first) it is reused;
 * otherwise the vendored `<script>` tag is injected once and awaited.
 *
 * Graphite theme defaults are ported from
 * `wwwroot/js/performance/components/chart-utils.js` and are read from the
 * document's CSS custom properties at create time, so a chart created after
 * a theme switch picks up the new palette (charts are not live-retinted).
 */

const CHART_SRC = '/lib/chart.js/chart.umd.js';

/** handle (number) -> Chart instance */
const charts = new Map();
let nextHandle = 1;
let chartJsPromise = null;

/**
 * Builds the Graphite-themed Chart.js default options. Takes a
 * `cssVarGetter(name)` function instead of reading `document` directly so
 * it is a pure function callable from tests without a DOM
 * (see `wwwroot/js/__tests__/blazor-charts.test.js`).
 *
 * @param {(name: string) => string | null | undefined} cssVarGetter
 * @returns {object} Chart.js default options object.
 */
export function buildDefaultOptions(cssVarGetter) {
    const read = (name, fallback) => {
        const value = cssVarGetter ? cssVarGetter(name) : null;
        return value && String(value).trim() ? String(value).trim() : fallback;
    };

    return {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
            legend: {
                labels: {
                    boxWidth: 12,
                    padding: 20,
                    color: read('--color-text-secondary', '#a09c96')
                }
            },
            tooltip: {
                backgroundColor: read('--color-bg-tertiary', '#1c2025'),
                titleColor: read('--color-text-primary', '#e7e4df'),
                bodyColor: read('--color-text-secondary', '#a09c96'),
                borderColor: read('--color-border-primary', 'rgba(255, 255, 255, 0.08)'),
                borderWidth: 1,
                padding: 12
            }
        },
        scales: {
            y: {
                grid: { color: read('--color-bg-tertiary', '#1c2025') },
                ticks: { color: read('--color-text-tertiary', '#6d6a66') }
            },
            x: {
                grid: { display: false },
                ticks: { color: read('--color-text-tertiary', '#6d6a66') }
            }
        }
    };
}

/**
 * The Graphite chart color palette, read the same way as
 * {@link buildDefaultOptions}. Exposed so callers building dataset colors in
 * .NET config objects don't have to; useful mainly from JS-side helpers.
 *
 * @param {(name: string) => string | null | undefined} cssVarGetter
 */
export function buildPalette(cssVarGetter) {
    const read = (name, fallback) => {
        const value = cssVarGetter ? cssVarGetter(name) : null;
        return value && String(value).trim() ? String(value).trim() : fallback;
    };

    return {
        primary: read('--color-accent-blue', '#3d9ad6'),
        secondary: read('--color-accent-orange', '#e6602b'),
        success: read('--color-success', '#2fbf7f'),
        warning: read('--color-warning', '#f0a323'),
        error: read('--color-error', '#ef4f4f'),
        info: read('--color-info', '#2fb3cc'),
        muted: read('--color-bg-tertiary', 'rgba(28, 32, 37, 0.8)')
    };
}

function readDocumentCssVar(name) {
    return getComputedStyle(document.documentElement).getPropertyValue(name);
}

function loadChartJs() {
    if (typeof window !== 'undefined' && window.Chart) {
        return Promise.resolve(window.Chart);
    }

    if (!chartJsPromise) {
        chartJsPromise = new Promise((resolve, reject) => {
            const existing = document.querySelector(`script[src="${CHART_SRC}"]`);
            if (existing) {
                // A tag we (or an earlier call) injected may have already fired its `load`
                // event by the time we get here - `data-loaded` is set below right when that
                // happens, so check it before subscribing to an event that will never fire
                // again and would otherwise hang this promise forever.
                if (existing.dataset.loaded === 'true') {
                    resolve(window.Chart);
                    return;
                }
                existing.addEventListener('load', () => resolve(window.Chart));
                existing.addEventListener('error', () => reject(new Error('Failed to load Chart.js from ' + CHART_SRC)));
                return;
            }

            const script = document.createElement('script');
            script.src = CHART_SRC;
            script.async = true;
            script.addEventListener('load', () => {
                script.dataset.loaded = 'true';
                resolve(window.Chart);
            });
            script.addEventListener('error', () => reject(new Error('Failed to load Chart.js from ' + CHART_SRC)));
            document.head.appendChild(script);
        });
    }

    return chartJsPromise;
}

const UNSAFE_MERGE_KEYS = new Set(['__proto__', 'constructor', 'prototype']);

/**
 * Deep-merges `override` onto `base`, returning a new object; neither input is
 * mutated. Exported (alongside {@link buildDefaultOptions}/{@link buildPalette})
 * purely so its prototype-pollution guard is testable without a DOM - see
 * `wwwroot/js/__tests__/blazor-charts.test.js`.
 *
 * @param {object} [base]
 * @param {object} [override]
 * @returns {object}
 */
export function mergeDeep(base, override) {
    const result = { ...(base || {}) };
    for (const key of Object.keys(override || {})) {
        // Config objects here ultimately come from .NET-serialized JSON handed across the
        // interop boundary; refuse to merge a key that could pollute the object/Object
        // prototype rather than trusting the caller never sends one.
        if (UNSAFE_MERGE_KEYS.has(key)) {
            continue;
        }
        const value = override[key];
        if (value && typeof value === 'object' && !Array.isArray(value)) {
            result[key] = mergeDeep(base ? base[key] : undefined, value);
        } else {
            result[key] = value;
        }
    }
    return result;
}

/**
 * Creates a Chart.js chart on the given canvas element.
 *
 * @param {HTMLCanvasElement} canvasElement
 * @param {object} config Chart.js config (`type`, `data`, and optional `options`).
 * @returns {Promise<number>} A numeric handle for `update`/`destroy`.
 */
export async function create(canvasElement, config) {
    const Chart = await loadChartJs();
    const defaults = buildDefaultOptions(readDocumentCssVar);
    const options = mergeDeep(defaults, (config && config.options) || {});
    const chart = new Chart(canvasElement, { ...config, options });

    const handle = nextHandle++;
    charts.set(handle, chart);
    return handle;
}

/**
 * Updates an existing chart's data and/or options in place, then redraws it.
 *
 * @param {number} handle
 * @param {object} [data] Replaces `chart.data` when provided.
 * @param {object} [options] Deep-merged into the chart's current options when provided.
 */
export function update(handle, data, options) {
    const chart = charts.get(handle);
    if (!chart) {
        return;
    }

    if (data) {
        chart.data = data;
    }
    if (options) {
        chart.options = mergeDeep(chart.options, options);
    }
    chart.update();
}

/**
 * Destroys a single chart instance and forgets its handle.
 * @param {number} handle
 */
export function destroy(handle) {
    const chart = charts.get(handle);
    if (!chart) {
        return;
    }
    chart.destroy();
    charts.delete(handle);
}

/**
 * Destroys every chart instance this module is currently tracking. Useful as
 * a last-resort cleanup from a layout's dispose path.
 */
export function destroyAll() {
    for (const chart of charts.values()) {
        chart.destroy();
    }
    charts.clear();
}
