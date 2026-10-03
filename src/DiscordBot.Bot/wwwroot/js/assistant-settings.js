/**
 * Assistant settings page: keeps the "N of M selected" lines in step with the checkboxes.
 * The lines are rendered by the server, so they are right on load; this only updates them as the
 * boxes change. The counts use Format.plural so one channel reads "1 channel", not "1 channel(s)".
 */
(function () {
    'use strict';

    var form = document.getElementById('assistant-form');
    if (!form) return;

    function plural(count, one, other) {
        return window.Format && typeof window.Format.plural === 'function'
            ? window.Format.plural(count, one, other)
            : count + ' ' + (count === 1 ? one : other);
    }

    function boxes(group) {
        return Array.prototype.slice.call(form.querySelectorAll('input[data-count-group="' + group + '"]'));
    }

    function update() {
        var channelBoxes = boxes('channels');
        var channelLine = document.getElementById('channels-count');
        if (channelLine) {
            var picked = channelBoxes.filter(function (box) { return box.checked; }).length;
            channelLine.textContent = picked + ' of ' + plural(channelBoxes.length, 'channel', 'channels') + ' selected';
        }

        var toolBoxes = boxes('tools');
        var toolLine = document.getElementById('tools-count');
        if (toolLine) {
            var pickedTools = toolBoxes.filter(function (box) { return box.checked; }).length;
            toolLine.textContent = pickedTools + ' of ' + plural(toolBoxes.length, 'tool', 'tools') +
                ' selected. Clearing every box restores the default set.';
        }
    }

    form.addEventListener('change', function (event) {
        if (event.target && event.target.getAttribute && event.target.getAttribute('data-count-group')) update();
    });
})();
