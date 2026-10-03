/**
 * Users > Edit: copy the one-time temporary password after a reset.
 *
 * The password is rendered once by the server in #generated-password. "Copy password" puts it on
 * the clipboard; where the clipboard is not available (an insecure page) the text is selected so
 * Ctrl+C works, and the toast says so. "Done" removes the panel from the page.
 */
(function () {
    'use strict';

    function notify(kind, message) {
        if (window.toast && typeof window.toast[kind] === 'function') window.toast[kind](message);
    }

    function selectText(element) {
        var range = document.createRange();
        range.selectNodeContents(element);
        var selection = window.getSelection();
        selection.removeAllRanges();
        selection.addRange(range);
    }

    async function copyPassword() {
        var source = document.getElementById('generated-password');
        if (!source) return;
        var text = source.textContent.trim();
        try {
            await navigator.clipboard.writeText(text);
            notify('success', 'Password copied.');
        } catch (error) {
            selectText(source);
            notify('info', 'Press Ctrl+C (or Cmd+C) to copy the selected password.');
        }
    }

    document.addEventListener('click', function (event) {
        var target = event.target.closest ? event.target : event.target.parentElement;
        if (!target) return;
        if (target.closest('[data-copy-password]')) {
            copyPassword();
        } else if (target.closest('[data-hide-password]')) {
            var panel = document.querySelector('[data-generated-password-panel]');
            if (panel) panel.remove();
        }
    });
})();
