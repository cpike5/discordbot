/**
 * Scheduled messages list: the "Run now" row action.
 *
 * A [data-run-now] button carries the message ID, a preview and the channel name as data-*
 * attributes. Clicking it asks through the shared confirm dialog, then posts to the existing
 * execute endpoint and reports the outcome as a toast. The preview and channel name are
 * user-written, so they only reach the dialog as text, never as markup. Guild and message IDs
 * stay strings.
 */
(function () {
    'use strict';

    function executeUrl(guildId, messageId) {
        return '/api/guilds/' + encodeURIComponent(guildId) +
            '/scheduled-messages/' + encodeURIComponent(messageId) + '/execute';
    }

    async function runNow(button) {
        if (button.getAttribute('aria-busy') === 'true') return;

        var guildId = window.scheduledMessagesGuildId;
        var messageId = button.dataset.runNow;
        var label = button.dataset.runLabel || 'this message';
        var channel = button.dataset.runChannel;

        var confirmed = await quickActions.confirm({
            title: 'Run this message now?',
            message: '"' + label + '" will be sent' + (channel ? ' to ' + channel : '') +
                ' straight away. A repeating message keeps its normal schedule; a one-time message is used up.',
            variant: 'warning',
            confirmText: 'Run now'
        });
        if (!confirmed) return;

        LoadingManager.setButtonLoading(button, true, 'Running...');
        try {
            await ApiClient.post(executeUrl(guildId, messageId), {},
                { errorMessage: 'Could not run the message. Try again.' });
            toast.success('Message sent.');
        } catch (err) {
            if (!err || !err.sessionExpired) {
                toast.error((err && err.message) || 'Could not run the message. Try again.');
            }
        } finally {
            LoadingManager.setButtonLoading(button, false);
        }
    }

    function init() {
        document.addEventListener('click', function (event) {
            var target = event.target;
            if (!target || !target.closest) return;
            var button = target.closest('[data-run-now]');
            if (button) runNow(button);
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
