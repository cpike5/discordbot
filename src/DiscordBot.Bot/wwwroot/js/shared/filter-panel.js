// Shared filter panel functions
// These functions are used across multiple command pages for consistent filter panel behavior

/**
 * Toggles the filter panel visibility
 * Expects elements with IDs: filterContent, filterChevron, filterToggle
 */
function toggleFilterPanel() {
    const content = document.getElementById('filterContent');
    const chevron = document.getElementById('filterChevron');
    const toggle = document.getElementById('filterToggle');

    if (!content || !chevron || !toggle) {
        console.warn('Filter panel elements not found');
        return;
    }

    if (content.style.maxHeight === '0px') {
        content.style.maxHeight = '1000px';
        chevron.style.transform = 'rotate(0deg)';
        toggle.setAttribute('aria-expanded', 'true');
    } else {
        content.style.maxHeight = '0px';
        chevron.style.transform = 'rotate(-90deg)';
        toggle.setAttribute('aria-expanded', 'false');
    }
}

/**
 * Sets date inputs based on a preset (today, 7days, 30days)
 * Expects date input elements with IDs: StartDate, EndDate
 * Auto-submits the form with ID: filterForm
 * @param {string} preset - 'today', '7days', or '30days'
 */
function setDatePreset(preset) {
    const today = new Date();
    const startDateInput = document.getElementById('StartDate');
    const endDateInput = document.getElementById('EndDate');

    if (!startDateInput || !endDateInput) {
        console.warn('Date input elements not found');
        return;
    }

    let startDate = new Date();

    switch(preset) {
        case 'today':
            startDate = today;
            break;
        case '7days':
            startDate.setDate(today.getDate() - 7);
            break;
        case '30days':
            startDate.setDate(today.getDate() - 30);
            break;
        default:
            console.warn('Unknown preset:', preset);
            return;
    }

    startDateInput.value = startDate.toISOString().split('T')[0];
    endDateInput.value = today.toISOString().split('T')[0];

    // Auto-submit the form after setting the date preset
    const form = document.getElementById('filterForm');
    if (form) {
        form.submit();
    }
}
