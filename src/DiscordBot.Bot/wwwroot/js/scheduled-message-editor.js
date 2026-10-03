/**
 * Scheduled message form (create and edit): character count, live preview, schedule type,
 * and the next-run time field.
 *
 * Time zones: the server stores UTC and the browser shows local time.
 *  - First render of the edit page: the field carries `data-utc-value`, and it is converted into
 *    the viewer's zone once, here.
 *  - Create page: the field is empty and gets "now + 5 minutes" in the viewer's zone.
 *  - After a failed save: the field already holds what the user typed (their own zone) and has no
 *    `data-utc-value`, so it is left alone. Converting it again as if it were UTC is what used to
 *    shift the time by the UTC offset on every failed submit.
 *
 * Runs straight away (the script sits after the markup) rather than on DOMContentLoaded, so the
 * unsaved-changes baseline, taken on DOMContentLoaded, sees the final values.
 */
(function () {
    'use strict';

    var form = document.getElementById('message-form');
    if (!form) return;

    var content = document.getElementById('Input_Content');
    var count = document.getElementById('char-count');
    var preview = document.getElementById('preview-message');
    var next = document.getElementById('Input_NextExecutionAt');
    var cron = document.getElementById('cron-expression-container');
    var frequencyDisplay = document.getElementById('schedule-frequency-display');
    var nextRun = document.getElementById('schedule-next-run');
    var customValue = form.getAttribute('data-custom-frequency');

    function updateCount() {
        if (count && content) count.textContent = String(content.value.length);
    }

    function updatePreview() {
        if (!preview || !content) return;
        preview.innerHTML = content.value.trim()
            ? window.DiscordMarkdown.render(content.value)
            : '<span class="discord-preview-muted italic">Enter a message to see the preview</span>';
    }

    function selectedFrequency() {
        return form.querySelector('input[name="Input.Frequency"]:checked');
    }

    function updateFrequency() {
        var selected = selectedFrequency();
        if (!selected) return;
        if (cron) cron.classList.toggle('hidden', selected.value !== customValue);
        var label = selected.getAttribute('data-frequency-label');
        if (frequencyDisplay && label) frequencyDisplay.textContent = label;
    }

    function updateNextRun() {
        if (!nextRun || !next) return;
        if (!next.value) {
            nextRun.textContent = 'Not set';
            return;
        }
        // The field is in the viewer's own zone, so it parses as local time
        var date = new Date(next.value);
        if (isNaN(date.getTime())) return;
        nextRun.removeAttribute('data-utc');
        nextRun.textContent = window.Format
            ? window.Format.formatDate(date.toISOString(), 'datetime-short')
            : date.toLocaleString();
    }

    // The next-run field
    if (next && window.timezoneUtils) {
        var utc = next.getAttribute('data-utc-value');
        if (utc) {
            window.timezoneUtils.setDateTimeLocalFromUtc('Input_NextExecutionAt', utc);
        } else if (!next.value) {
            window.timezoneUtils.setDefaultDateTime('Input_NextExecutionAt', 5);
        }
        next.removeAttribute('data-utc-value');
    }

    if (content) content.addEventListener('input', function () { updateCount(); updatePreview(); });
    form.addEventListener('change', function (event) {
        if (event.target && event.target.name === 'Input.Frequency') updateFrequency();
    });
    if (next) next.addEventListener('input', updateNextRun);

    updateCount();
    updatePreview();
    updateFrequency();
    // The summary keeps its server-rendered time until the field is edited
    if (nextRun && !nextRun.getAttribute('data-utc')) updateNextRun();
})();
