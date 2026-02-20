/**
 * Blazor Chart.js Interop
 * Manages Chart.js instances for Blazor components.
 */
window.BlazorChartInterop = {
    _charts: new Map(),

    createChart: function (canvasId, config) {
        const existing = this._charts.get(canvasId);
        if (existing) existing.destroy();

        const ctx = document.getElementById(canvasId)?.getContext('2d');
        if (!ctx) return;

        this._charts.set(canvasId, new Chart(ctx, config));
    },

    updateChart: function (canvasId, data) {
        const chart = this._charts.get(canvasId);
        if (!chart) return;

        chart.data = data;
        chart.update();
    },

    destroyChart: function (canvasId) {
        const chart = this._charts.get(canvasId);
        if (chart) {
            chart.destroy();
            this._charts.delete(canvasId);
        }
    }
};

/**
 * Blazor Navigation Interop
 * Provides DOM navigation helpers for Blazor components.
 */
window.BlazorNavigationInterop = {
    scrollToElement: function (elementId) {
        const el = document.getElementById(elementId);
        if (el) {
            el.scrollIntoView({ behavior: 'smooth' });
        }
    }
};
