/**
 * Performance Dashboard - System Tab Module
 * Displays system health metrics including database and memory
 */
(function() {
    'use strict';

    window.Performance = window.Performance || {};
    window.Performance.Tabs = window.Performance.Tabs || {};

    const state = {
        charts: [],
        isInitialized: false
    };

    const ChartUtils = window.Performance.ChartUtils;
    const TimestampUtils = window.Performance.TimestampUtils;
    const TimeRange = window.Performance.TimeRange;

    // Toggle slow query details row - exposed globally for onclick handlers
    window.toggleQueryDetails = function(index) {
        const detailsRow = document.getElementById(`queryDetails-${index}`);
        const chevron = document.getElementById(`queryChevron-${index}`);

        if (detailsRow && chevron) {
            const isHidden = detailsRow.classList.contains('hidden');
            detailsRow.classList.toggle('hidden');
            chevron.style.transform = isHidden ? 'rotate(90deg)' : 'rotate(0deg)';
        }
    };

    // Show/hide chart states
    function showChartState(chartName, stateName) {
        const states = ['Loading', 'Empty', 'Error'];
        states.forEach(s => {
            const element = document.getElementById(`system${chartName}${s}`);
            if (element) {
                if (s.toLowerCase() === stateName) {
                    element.classList.remove('hidden');
                } else {
                    element.classList.add('hidden');
                }
            }
        });
    }

    async function initQueryTimeChart(hours) {
        const ctx = document.getElementById('systemQueryTimeChart');
        if (!ctx) return;

        showChartState('QueryTime', 'loading');

        try {
            const response = await fetch(`/api/metrics/system/history/database?hours=${hours}`);

            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }

            const data = await response.json();

            // Hide all states
            showChartState('QueryTime', null);

            if (!data.samples || data.samples.length === 0) {
                showChartState('QueryTime', 'empty');
                return;
            }

            const labels = data.samples.map(s => ChartUtils.formatLabel(s.timestamp, hours));
            const avgQueryTimeData = data.samples.map(s => s.avgQueryTimeMs);
            const showPoints = hours <= 6;

            const chart = new Chart(ctx, {
                type: 'line',
                data: {
                    labels: labels,
                    datasets: [{
                        label: 'Avg Query Time',
                        data: avgQueryTimeData,
                        borderColor: ChartUtils.colors.primary,
                        backgroundColor: 'rgba(9, 142, 207, 0.1)',
                        fill: true,
                        tension: 0.4,
                        pointRadius: showPoints ? 3 : 0,
                        pointHoverRadius: 5
                    }]
                },
                options: ChartUtils.mergeOptions(ChartUtils.defaultOptions, {
                    plugins: { legend: { display: false } },
                    scales: {
                        y: {
                            beginAtZero: true,
                            ticks: {
                                callback: function(value) {
                                    return value + ' ms';
                                }
                            }
                        }
                    }
                })
            });

            state.charts.push(chart);
        } catch (error) {
            console.error('Failed to load database metrics:', error);
            showChartState('QueryTime', 'error');
        }
    }

    async function initMemoryChart(hours) {
        const ctx = document.getElementById('systemMemoryChart');
        if (!ctx) return;

        showChartState('Memory', 'loading');

        try {
            const response = await fetch(`/api/metrics/system/history/memory?hours=${hours}`);

            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }

            const data = await response.json();

            // Hide all states
            showChartState('Memory', null);

            if (!data.samples || data.samples.length === 0) {
                showChartState('Memory', 'empty');
                return;
            }

            const labels = data.samples.map(s => ChartUtils.formatLabel(s.timestamp, hours));
            const workingSetData = data.samples.map(s => s.workingSetMB);
            const heapData = data.samples.map(s => s.heapSizeMB);

            const showPoints = hours <= 6;

            const chart = new Chart(ctx, {
                type: 'line',
                data: {
                    labels: labels,
                    datasets: [
                        {
                            label: 'Working Set',
                            data: workingSetData,
                            borderColor: ChartUtils.colors.primary,
                            backgroundColor: 'transparent',
                            tension: 0.4,
                            pointRadius: showPoints ? 2 : 0,
                            pointHoverRadius: 5
                        },
                        {
                            label: 'Heap Size',
                            data: heapData,
                            borderColor: ChartUtils.colors.success,
                            backgroundColor: 'transparent',
                            tension: 0.4,
                            pointRadius: showPoints ? 2 : 0,
                            pointHoverRadius: 5
                        }
                    ]
                },
                options: ChartUtils.mergeOptions(ChartUtils.defaultOptions, {
                    interaction: {
                        mode: 'index',
                        intersect: false
                    },
                    plugins: {
                        legend: {
                            position: 'bottom',
                            labels: {
                                boxWidth: 12,
                                padding: 20
                            }
                        },
                        tooltip: {
                            callbacks: {
                                label: function(context) {
                                    return context.dataset.label + ': ' + context.parsed.y.toFixed(1) + ' MB';
                                }
                            }
                        }
                    },
                    scales: {
                        y: {
                            beginAtZero: false,
                            ticks: {
                                callback: function(value) {
                                    return value.toFixed(0) + ' MB';
                                }
                            }
                        }
                    }
                })
            });

            state.charts.push(chart);
        } catch (error) {
            console.error('Failed to load memory metrics:', error);
            showChartState('Memory', 'error');
        }
    }

    const System = {
        init: async function(hours) {
            this.destroy();
            hours = hours || TimeRange.get();

            TimestampUtils.convertTimestamps();

            await Promise.all([
                initQueryTimeChart(hours),
                initMemoryChart(hours)
            ]);

            // Retry button handlers
            const queryTimeRetry = document.getElementById('systemQueryTimeRetry');
            if (queryTimeRetry) {
                queryTimeRetry.addEventListener('click', function() {
                    initQueryTimeChart(hours);
                });
            }

            const memoryRetry = document.getElementById('systemMemoryRetry');
            if (memoryRetry) {
                memoryRetry.addEventListener('click', function() {
                    initMemoryChart(hours);
                });
            }

            state.isInitialized = true;
        },

        destroy: function() {
            ChartUtils.destroyCharts(state.charts);
            state.charts = [];
            state.isInitialized = false;
        }
    };

    window.Performance.Tabs.System = System;
    window.initSystemTab = function(hours) { System.init(hours); };
    window.destroySystemTab = function() { System.destroy(); };
})();
