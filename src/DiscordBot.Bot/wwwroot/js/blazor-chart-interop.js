/**
 * Chart.js lifecycle bridge for Blazor components (ChartJsInterop.cs).
 *
 * Blazor owns the canvas element and the data; this module owns the Chart.js
 * instances, keyed by canvas id, so a re-render or circuit reconnect can
 * replace a chart without leaking the old instance. Chart.js itself must be
 * loaded by the host page before any interop call.
 */
window.blazorChartInterop = (function () {
    'use strict';

    /** @type {Map<string, Chart>} chart instances keyed by canvas id */
    const charts = new Map();

    return {
        /**
         * Create (or replace) the chart bound to a canvas.
         * @param {string} canvasId - id of the canvas element
         * @param {object} config - Chart.js configuration
         */
        create: function (canvasId, config) {
            const existing = charts.get(canvasId);
            if (existing) {
                existing.destroy();
                charts.delete(canvasId);
            }

            const canvas = document.getElementById(canvasId);
            if (!canvas || typeof Chart === 'undefined') {
                return;
            }

            charts.set(canvasId, new Chart(canvas.getContext('2d'), config));
        },

        /**
         * Replace the chart's data (labels + datasets) and update in place.
         * @param {string} canvasId - id of the canvas element
         * @param {object} data - Chart.js data object ({ labels, datasets })
         */
        update: function (canvasId, data) {
            const chart = charts.get(canvasId);
            if (!chart) {
                return;
            }

            chart.data = data;
            chart.update('none'); // skip animation for real-time streams
        },

        /**
         * Destroy the chart for a canvas and release its resources.
         * @param {string} canvasId - id of the canvas element
         */
        destroy: function (canvasId) {
            const chart = charts.get(canvasId);
            if (chart) {
                chart.destroy();
                charts.delete(canvasId);
            }
        }
    };
})();
