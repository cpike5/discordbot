// moderation-analytics.js
// Moderation analytics dashboard with Chart.js visualizations

(function () {
    'use strict';

    function getDesignToken(name) {
        return getComputedStyle(document.documentElement)
            .getPropertyValue(`--color-${name}`).trim();
    }

    function initColors() {
        return {
            accentOrange: getDesignToken('accent-orange') || '#cb4e1b',
            accentBlue: getDesignToken('accent-blue') || '#098ecf',
            success: getDesignToken('success') || '#10b981',
            warning: getDesignToken('warning') || '#f59e0b',
            info: getDesignToken('info') || '#06b6d4',
            error: getDesignToken('error') || '#ef4444',
            bgPrimary: getDesignToken('bg-primary') || '#1d2022',
            bgSecondary: getDesignToken('bg-secondary') || '#262a2d',
            bgTertiary: getDesignToken('bg-tertiary') || '#2f3336',
            textPrimary: getDesignToken('text-primary') || '#d7d3d0',
            textSecondary: getDesignToken('text-secondary') || '#a8a5a3',
            textTertiary: getDesignToken('text-tertiary') || '#7a7876',
            borderPrimary: getDesignToken('border-primary') || '#3f4447',
        };
    }

    let colors = initColors();

    const CHART_COLORS = [
        colors.accentOrange,
        colors.accentBlue,
        colors.success,
        colors.warning,
        colors.info,
        colors.error,
        '#8b5cf6',
        '#ec4899',
        '#14b8a6',
        '#6366f1',
    ];

    const BG_COLORS = {
        primary: colors.bgPrimary,
        secondary: colors.bgSecondary,
        tertiary: colors.bgTertiary,
    };

    const TEXT_COLORS = {
        primary: colors.textPrimary,
        secondary: colors.textSecondary,
        tertiary: colors.textTertiary,
    };

    const BORDER_COLOR = colors.borderPrimary;

    let moderationTrendsChart = null;
    let caseDistributionChart = null;
    let moderatorWorkloadChart = null;

    const commonOptions = {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
            legend: {
                display: false,
            },
            tooltip: {
                backgroundColor: BG_COLORS.tertiary,
                titleColor: TEXT_COLORS.primary,
                bodyColor: TEXT_COLORS.secondary,
                borderColor: BORDER_COLOR,
                borderWidth: 1,
                padding: 12,
                cornerRadius: 6,
            },
        },
    };

    const gridConfig = {
        color: 'rgba(63, 68, 71, 0.5)',
        drawBorder: false,
    };

    const ticksConfig = {
        color: TEXT_COLORS.tertiary,
        font: {
            size: 12,
        },
    };

    function formatNumber(num) {
        return num.toString().replace(/\B(?=(\d{3})+(?!\d))/g, ',');
    }

    function formatDate(dateStr) {
        const date = new Date(dateStr);
        const monthNames = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
            'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
        return `${monthNames[date.getMonth()]} ${date.getDate()}`;
    }

    /**
     * Center text plugin for doughnut charts.
     */
    const centerTextPlugin = {
        id: 'centerText',
        afterDatasetsDraw: function (chart) {
            if (!chart.config.options.plugins.centerText) {
                return;
            }

            const { ctx, chartArea: { width, height } } = chart;
            const centerX = width / 2;
            const centerY = height / 2;
            const text = chart.config.options.plugins.centerText.text;

            ctx.save();
            ctx.font = 'bold 32px ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto';
            ctx.fillStyle = TEXT_COLORS.primary;
            ctx.textAlign = 'center';
            ctx.textBaseline = 'middle';
            ctx.fillText(text, centerX, centerY);
            ctx.restore();
        }
    };

    /**
     * Initializes the Moderation Trends stacked area chart.
     * @param {Array} trendsData - Array of {date, warnings, mutes, kicks, bans} objects
     */
    function initModerationTrendsChart(trendsData) {
        const canvas = document.getElementById('moderationTrendsChart');
        if (!canvas || !trendsData || trendsData.length === 0) {
            return;
        }

        const ctx = canvas.getContext('2d');
        const labels = trendsData.map(item => formatDate(item.date));

        moderationTrendsChart = new Chart(ctx, {
            type: 'line',
            data: {
                labels: labels,
                datasets: [
                    {
                        label: 'Warnings',
                        data: trendsData.map(item => item.warnings),
                        borderColor: CHART_COLORS[3],
                        backgroundColor: 'rgba(245, 158, 11, 0.2)',
                        borderWidth: 2,
                        fill: true,
                        tension: 0.3,
                        pointRadius: 3,
                    },
                    {
                        label: 'Mutes',
                        data: trendsData.map(item => item.mutes),
                        borderColor: CHART_COLORS[4],
                        backgroundColor: 'rgba(6, 182, 212, 0.2)',
                        borderWidth: 2,
                        fill: true,
                        tension: 0.3,
                        pointRadius: 3,
                    },
                    {
                        label: 'Kicks',
                        data: trendsData.map(item => item.kicks),
                        borderColor: CHART_COLORS[0],
                        backgroundColor: 'rgba(203, 78, 27, 0.2)',
                        borderWidth: 2,
                        fill: true,
                        tension: 0.3,
                        pointRadius: 3,
                    },
                    {
                        label: 'Bans',
                        data: trendsData.map(item => item.bans),
                        borderColor: CHART_COLORS[5],
                        backgroundColor: 'rgba(239, 68, 68, 0.2)',
                        borderWidth: 2,
                        fill: true,
                        tension: 0.3,
                        pointRadius: 3,
                    }
                ]
            },
            options: {
                ...commonOptions,
                plugins: {
                    ...commonOptions.plugins,
                    legend: {
                        display: true,
                        position: 'bottom',
                        labels: {
                            color: TEXT_COLORS.primary,
                            padding: 16,
                            font: { size: 12 },
                            usePointStyle: true,
                        }
                    },
                    tooltip: {
                        ...commonOptions.plugins.tooltip,
                        mode: 'index',
                        intersect: false,
                    }
                },
                scales: {
                    x: {
                        grid: gridConfig,
                        ticks: ticksConfig,
                    },
                    y: {
                        stacked: true,
                        beginAtZero: true,
                        grid: gridConfig,
                        ticks: ticksConfig,
                    }
                },
                interaction: {
                    intersect: false,
                    mode: 'index',
                }
            }
        });

        console.log('Moderation trends chart initialized');
    }

    /**
     * Initializes the Case Type Distribution doughnut chart.
     * @param {Object} distributionData - Object with case type counts
     */
    function initCaseDistributionChart(distributionData) {
        const canvas = document.getElementById('caseDistributionChart');
        if (!canvas || !distributionData) {
            return;
        }

        const ctx = canvas.getContext('2d');
        const labels = Object.keys(distributionData);
        const data = Object.values(distributionData);
        const total = data.reduce((a, b) => a + b, 0);

        caseDistributionChart = new Chart(ctx, {
            type: 'doughnut',
            data: {
                labels: labels,
                datasets: [{
                    data: data,
                    backgroundColor: [
                        CHART_COLORS[3], // warnings - yellow
                        CHART_COLORS[4], // mutes - cyan
                        CHART_COLORS[0], // kicks - orange
                        CHART_COLORS[5], // bans - red
                        CHART_COLORS[6], // other - purple
                    ],
                    borderColor: BG_COLORS.primary,
                    borderWidth: 3,
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                cutout: '70%',
                plugins: {
                    legend: {
                        display: true,
                        position: 'bottom',
                        labels: {
                            color: TEXT_COLORS.primary,
                            padding: 16,
                            font: { size: 13 },
                            usePointStyle: true,
                            pointStyle: 'circle',
                        }
                    },
                    tooltip: {
                        ...commonOptions.plugins.tooltip,
                        callbacks: {
                            label: function (context) {
                                const label = context.label || '';
                                const value = context.parsed;
                                const percentage = total > 0 ? ((value / total) * 100).toFixed(1) : '0.0';
                                return `${label}: ${formatNumber(value)} (${percentage}%)`;
                            }
                        }
                    },
                    centerText: {
                        text: formatNumber(total)
                    }
                }
            },
            plugins: [centerTextPlugin]
        });

        console.log('Case distribution chart initialized');
    }

    /**
     * Initializes the Moderator Workload horizontal bar chart.
     * @param {Array} workloadData - Array of {moderatorName, actionCount} objects
     */
    function initModeratorWorkloadChart(workloadData) {
        const canvas = document.getElementById('moderatorWorkloadChart');
        if (!canvas || !workloadData || workloadData.length === 0) {
            return;
        }

        const ctx = canvas.getContext('2d');
        const labels = workloadData.map(item => item.moderatorName);
        const data = workloadData.map(item => item.actionCount);

        const colorRange = labels.map((_, index) => CHART_COLORS[index % CHART_COLORS.length]);

        moderatorWorkloadChart = new Chart(ctx, {
            type: 'bar',
            data: {
                labels: labels,
                datasets: [{
                    label: 'Actions',
                    data: data,
                    backgroundColor: colorRange,
                    borderColor: colorRange,
                    borderWidth: 1,
                    borderRadius: 4,
                    barThickness: 24,
                }]
            },
            options: {
                indexAxis: 'y',
                ...commonOptions,
                plugins: {
                    ...commonOptions.plugins,
                    tooltip: {
                        ...commonOptions.plugins.tooltip,
                        callbacks: {
                            label: function (context) {
                                return `Actions: ${formatNumber(context.parsed.x)}`;
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        beginAtZero: true,
                        grid: gridConfig,
                        ticks: ticksConfig,
                    },
                    y: {
                        grid: { display: false },
                        ticks: {
                            color: TEXT_COLORS.primary,
                            font: { size: 12 },
                            padding: 8,
                        }
                    }
                },
                animation: {
                    duration: 500,
                    easing: 'easeOutQuart',
                }
            }
        });

        console.log('Moderator workload chart initialized');
    }

    /**
     * Initialize all moderation analytics charts.
     */
    function init() {
        const dataElement = document.getElementById('moderationAnalyticsChartData');
        if (!dataElement) {
            console.log('Moderation analytics chart data not found on this page');
            return;
        }

        try {
            const chartData = JSON.parse(dataElement.textContent);

            if (chartData.trends && chartData.trends.length > 0) {
                initModerationTrendsChart(chartData.trends);
            }

            if (chartData.distribution) {
                initCaseDistributionChart(chartData.distribution);
            }

            if (chartData.workload && chartData.workload.length > 0) {
                initModeratorWorkloadChart(chartData.workload);
            }

            console.log('Moderation analytics charts initialized');

        } catch (error) {
            console.error('Failed to initialize moderation analytics charts:', error);
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

})();
