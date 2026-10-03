/**
 * Chart Theme
 * Chart.js colours from the design tokens in site.css, kept in step with the theme.
 *
 * - Sets Chart.defaults (text, grid, font, tooltip) from the tokens once Chart.js is on the page.
 * - Every chart's axis, grid, legend, title and tooltip colours follow the theme, including
 *   charts whose config hard-codes them: a Chart.js plugin repaints those options as each chart
 *   is created, and again on `themechange` (dispatched by theme.js).
 * - Series colours are the caller's; use ChartTheme.colors() for token-based ones, and
 *   ChartTheme.onChange(fn) to recolour datasets when the theme changes.
 * - Under prefers-reduced-motion, chart animation is off.
 *
 * Loaded by _Layout before any page script, so it is ready before pages build their charts.
 * Exposed as window.ChartTheme.
 */
(function () {
    'use strict';

    const listeners = [];
    let pluginRegistered = false;

    function token(name) {
        return getComputedStyle(document.documentElement).getPropertyValue(`--color-${name}`).trim();
    }

    function rgba(name, alpha) {
        const rgb = token(`${name}-rgb`);
        return rgb ? `rgba(${rgb}, ${alpha})` : token(name);
    }

    /**
     * Colours for the active theme, read fresh from the tokens.
     * Series colours are inks (readable as lines and labels on the surfaces);
     * `fills` are the darker fills for solid bars behind text.
     */
    function colors() {
        return {
            text: token('text-primary'),
            textMuted: token('text-secondary'),
            textSubtle: token('text-tertiary'),
            grid: rgba('text-primary', 0.08),
            border: token('border-strong'),
            surface: token('bg-tertiary'),
            canvas: token('bg-primary'),
            track: token('bg-hover'),
            primary: token('accent-orange'),
            secondary: token('accent-blue'),
            purple: token('accent-purple'),
            success: token('success'),
            warning: token('warning'),
            error: token('error'),
            info: token('info'),
            fills: {
                primary: token('accent-orange-fill'),
                secondary: token('accent-blue-fill'),
                success: token('success-fill'),
                warning: token('warning-fill'),
                error: token('error-fill'),
                info: token('info-fill')
            },
            series: [
                token('accent-orange'),
                token('accent-blue'),
                token('success'),
                token('warning'),
                token('info'),
                token('error'),
                token('accent-purple')
            ],
            /** A series colour with alpha, for area fills under lines. */
            alpha(name, alpha) {
                return rgba(name, alpha);
            }
        };
    }

    function reducedMotion() {
        return window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    }

    function fontFamily() {
        return getComputedStyle(document.documentElement).getPropertyValue('--font-body').trim() || undefined;
    }

    /** Points Chart.defaults at the active theme. */
    function applyDefaults() {
        const Chart = window.Chart;
        if (!Chart || !Chart.defaults) return;
        const c = colors();

        Chart.defaults.color = c.textMuted;
        Chart.defaults.borderColor = c.grid;
        const family = fontFamily();
        if (family) Chart.defaults.font.family = family;

        const tooltip = Chart.defaults.plugins.tooltip;
        tooltip.backgroundColor = c.surface;
        tooltip.titleColor = c.text;
        tooltip.bodyColor = c.textMuted;
        tooltip.footerColor = c.textMuted;
        tooltip.borderColor = c.border;
        tooltip.borderWidth = 1;
        Chart.defaults.plugins.legend.labels.color = c.textMuted;
        Chart.defaults.plugins.title.color = c.text;

        if (reducedMotion()) {
            Chart.defaults.animation = false;
            Chart.defaults.transitions.active.animation.duration = 0;
        }
    }

    /**
     * Repaints the theme-dependent chrome in a chart's own options: whatever the config sets
     * for ticks, grid, titles, legend and tooltip. Anything it does not set comes from
     * Chart.defaults. Dataset colours are left alone.
     */
    function paint(options) {
        if (!options) return;
        const c = colors();

        const scales = options.scales || {};
        Object.keys(scales).forEach(id => {
            const scale = scales[id];
            if (!scale || typeof scale !== 'object') return;
            if (scale.ticks && 'color' in scale.ticks) scale.ticks.color = c.textMuted;
            if (scale.grid && 'color' in scale.grid) scale.grid.color = c.grid;
            if (scale.border && 'color' in scale.border) scale.border.color = c.grid;
            if (scale.title && 'color' in scale.title) scale.title.color = c.textMuted;
            if (scale.pointLabels && 'color' in scale.pointLabels) scale.pointLabels.color = c.textMuted;
            if (scale.angleLines && 'color' in scale.angleLines) scale.angleLines.color = c.grid;
        });

        const plugins = options.plugins || {};
        if (plugins.legend && plugins.legend.labels && 'color' in plugins.legend.labels) {
            plugins.legend.labels.color = c.textMuted;
        }
        if (plugins.title && 'color' in plugins.title) plugins.title.color = c.text;
        if (plugins.subtitle && 'color' in plugins.subtitle) plugins.subtitle.color = c.textMuted;

        const tooltip = plugins.tooltip;
        if (tooltip) {
            if ('backgroundColor' in tooltip) tooltip.backgroundColor = c.surface;
            if ('titleColor' in tooltip) tooltip.titleColor = c.text;
            if ('bodyColor' in tooltip) tooltip.bodyColor = c.textMuted;
            if ('footerColor' in tooltip) tooltip.footerColor = c.textMuted;
            if ('borderColor' in tooltip) tooltip.borderColor = c.border;
        }

        if (reducedMotion()) options.animation = false;
    }

    /** Hooks into Chart.js once it is loaded: defaults, plus a repaint as each chart is created. */
    function ensureRegistered() {
        const Chart = window.Chart;
        if (!Chart || pluginRegistered) return;
        pluginRegistered = true;
        applyDefaults();
        Chart.register({
            id: 'chartTheme',
            beforeInit(chart) {
                paint(chart.config.options);
            }
        });
    }

    /** Recolours every chart on the page for the active theme. */
    function refresh() {
        const Chart = window.Chart;
        if (!Chart) return;
        ensureRegistered();
        applyDefaults();
        Object.values(Chart.instances || {}).forEach(chart => {
            paint(chart.config.options);
            listeners.forEach(fn => {
                try {
                    fn(chart, colors());
                } catch (e) {
                    console.error('ChartTheme: a theme listener failed', e);
                }
            });
            chart.update('none');
        });
    }

    /**
     * Runs fn(chart, colors) for every chart when the theme changes, after the chrome is
     * repainted and before the chart redraws. Use it for token-based dataset colours.
     */
    function onChange(fn) {
        if (typeof fn === 'function') listeners.push(fn);
    }

    // Chart.js is loaded by the page after this script, so hook in when the DOM is ready
    // (this listener is registered before any page script's, so it runs first).
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', ensureRegistered);
    } else {
        ensureRegistered();
    }
    window.addEventListener('load', ensureRegistered);
    window.addEventListener('themechange', refresh);

    window.ChartTheme = {
        colors,
        applyDefaults,
        paint,
        refresh,
        onChange,
        ensureRegistered
    };
})();
