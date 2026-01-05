/**
 * Real-time UI Helpers
 * Provides utilities for real-time data visualization and feedback.
 */

/**
 * Animate a value change with a flash highlight effect.
 * @param {string} elementId - The ID of the element to animate
 * @param {*} newValue - The new value to display
 * @param {function} formatter - Optional formatter function for the value
 */
function animateValueChange(elementId, newValue, formatter = null) {
    const element = document.getElementById(elementId);
    if (!element) return;

    const oldValue = element.textContent;
    const displayValue = formatter ? formatter(newValue) : String(newValue);
    element.textContent = displayValue;

    if (oldValue !== displayValue) {
        element.classList.add('value-changed');
        setTimeout(() => element.classList.remove('value-changed'), 500);
    }
}

/**
 * Update multiple metric values with animation
 * @param {Object} metrics - Object with elementId keys and value objects
 */
function updateMetrics(metrics) {
    for (const [elementId, { value, formatter }] of Object.entries(metrics)) {
        animateValueChange(elementId, value, formatter);
    }
}

/**
 * Set live indicator state
 * @param {string} elementId - The ID of the live indicator element
 * @param {boolean} isLive - Whether data is streaming
 */
function setLiveIndicatorState(elementId, isLive) {
    const element = document.getElementById(elementId);
    if (!element) return;

    if (isLive) {
        element.classList.remove('paused');
    } else {
        element.classList.add('paused');
    }
}

// Export for module usage
if (typeof module !== 'undefined' && module.exports) {
    module.exports = { animateValueChange, updateMetrics, setLiveIndicatorState };
}
