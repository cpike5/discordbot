// Commands island interop (Slice 4).
//
// The CommandsIsland renders the Command List and Execution Logs tabs natively in
// Blazor, but the Analytics tab is deliberately left on the existing Chart.js path
// (plan §5.2 #3 — "Analytics stays Chart.js; don't block on charting"). This shim
// lets the island delegate the Analytics tab to the unchanged server partial +
// Chart.js init code, and refresh the shared timezone display conversion after the
// circuit re-renders the logs table.
//
// Loaded as a classic script in the Commands host page's @section Scripts, after
// chart.umd.min.js / command-analytics.js / timezone.js.
(function () {
    "use strict";

    window.commandsIslandInterop = {
        // Fetches the Analytics tab partial (/api/commands/analytics) and injects it
        // into the island's host element, then runs the embedded chart-init script.
        // Mirrors what command-tab-loader.js did for this tab: innerHTML does not run
        // <script> tags, so they are re-created to execute, after which the partial's
        // own window.initializeAnalyticsCharts() is invoked to draw the charts.
        loadAnalytics: async function (hostId, queryString) {
            var host = document.getElementById(hostId);
            if (!host) {
                console.warn("commandsIslandInterop.loadAnalytics: host not found:", hostId);
                return;
            }

            try {
                var url = "/api/commands/analytics" + (queryString ? "?" + queryString : "");
                var response = await fetch(url, {
                    method: "GET",
                    headers: { "X-Requested-With": "XMLHttpRequest" }
                });

                if (!response.ok) {
                    throw new Error("HTTP " + response.status + ": " + response.statusText);
                }

                host.innerHTML = await response.text();

                // Re-execute injected scripts (innerHTML won't run them).
                var scripts = host.querySelectorAll("script");
                for (var i = 0; i < scripts.length; i++) {
                    var oldScript = scripts[i];
                    var newScript = document.createElement("script");
                    if (oldScript.src) {
                        newScript.src = oldScript.src;
                    } else {
                        newScript.textContent = oldScript.textContent;
                    }
                    oldScript.parentNode.replaceChild(newScript, oldScript);
                }

                // The partial defines but does not auto-run the chart initializer.
                if (typeof window.initializeAnalyticsCharts === "function") {
                    window.initializeAnalyticsCharts();
                }
            } catch (error) {
                console.error("commandsIslandInterop.loadAnalytics failed:", error);
                host.innerHTML =
                    '<div class="bg-bg-secondary border border-border-primary rounded-lg p-8 text-center">' +
                    '<p class="text-error">Failed to load analytics. Please try again.</p>' +
                    "</div>";
            }
        },

        // Re-runs the shared timezone conversion over any [data-utc] spans the island
        // just rendered (logs timestamps, details modal). Safe to call repeatedly.
        convertTimes: function () {
            try {
                if (window.timezoneUtils && typeof window.timezoneUtils.convertDisplayTimes === "function") {
                    window.timezoneUtils.convertDisplayTimes();
                }
            } catch (e) {
                console.error("commandsIslandInterop.convertTimes failed", e);
            }
        }
    };
})();
