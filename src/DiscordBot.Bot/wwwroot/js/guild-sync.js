/**
 * Servers list: sync one server from Discord, in place.
 *
 * A "Sync" button (table row or card) carries `data-sync-guild="<id>"`. Clicking it posts to the
 * page's SyncGuild handler through ApiClient, shows a pending state on that button, and on success
 * writes the fresh name and member count into the row. There is no reload and no toast-then-refresh,
 * so scroll position and the filters stay where they are. A failure says so and re-enables the
 * button.
 *
 * "Sync all" is not here: it is an ordinary confirmed form post (confirm-forms.js) that redirects
 * back with a toast.
 *
 * Markup the row exposes (both layouts): `data-guild-row="<id>"`, `[data-guild-name]`,
 * `[data-guild-members]`.
 */
(function () {
    'use strict';

    function rowsFor(id) {
        return document.querySelectorAll('[data-guild-row="' + id + '"]');
    }

    function applyGuild(guild) {
        if (!guild || !guild.id) return;
        rowsFor(guild.id).forEach(function (row) {
            row.querySelectorAll('[data-guild-name]').forEach(function (el) { el.textContent = guild.name; });
            row.querySelectorAll('[data-guild-members]').forEach(function (el) {
                el.textContent = window.Format ? window.Format.number(guild.memberCount) : String(guild.memberCount);
            });
        });
    }

    async function syncGuild(button) {
        var id = button.getAttribute('data-sync-guild');
        if (!id || button.disabled) return;

        if (window.LoadingManager) window.LoadingManager.setButtonLoading(button, true, 'Syncing...');
        else button.disabled = true;

        try {
            var result = await window.ApiClient.post('?handler=SyncGuild&id=' + encodeURIComponent(id), null);
            if (result && result.success) {
                applyGuild(result.guild);
                window.toast.success('Server synced.');
            } else {
                window.toast.error('The bot cannot see that server right now, so it could not be synced.');
            }
        } catch (error) {
            // ApiClient has already worked out plain-language text (or shown the sign-in toast)
            window.ApiClient.showErrorToast(error);
        } finally {
            if (window.LoadingManager) window.LoadingManager.setButtonLoading(button, false);
            else button.disabled = false;
        }
    }

    document.addEventListener('click', function (event) {
        var target = event.target;
        var button = target.closest && target.closest('[data-sync-guild]');
        if (button) {
            event.preventDefault();
            syncGuild(button);
            return;
        }

        // A table row opens the server when clicked anywhere but on a control. Keyboard and
        // screen reader users have the real link in the name cell.
        var row = target.closest && target.closest('[data-row-href]');
        if (row && !target.closest('a, button, input, select, textarea, label') && !window.getSelection().toString()) {
            window.location.assign(row.getAttribute('data-row-href'));
        }
    });

    window.GuildSync = { syncGuild: syncGuild, applyGuild: applyGuild };
})();
