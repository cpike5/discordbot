/**
 * Dashboard actions - what happens after the dashboard's confirm dialogs and in its widgets.
 *
 * The confirm modals themselves (Restart bot, Sync all servers) are the shared ones; they post
 * to the page, show the result toast and close (quick-actions.js), then raise
 * `quickactions:confirmed` on the modal. This file follows up:
 *
 *  - Restart bot: the banner reads "Restarting" until the bot reports Connected again, then a
 *    toast says it is back (or says it has not come back, after 90 seconds).
 *  - Sync all servers: the Connected Servers card and the hero numbers are fetched again.
 *  - The Connected Servers rows are drawn from JSON by cloning #connected-server-template, the
 *    twin of the rows _ConnectedServersWidget.cshtml renders; change them together.
 *
 * Exposed as window.DashboardActions (browser) and module.exports (Node/tests).
 */
(function (root, factory) {
    if (typeof module === 'object' && module.exports) {
        module.exports = factory(root);
    } else {
        root.DashboardActions = factory(root);
        if (typeof document !== 'undefined') root.DashboardActions.init();
    }
})(typeof self !== 'undefined' ? self : this, function (root) {
    'use strict';

    var STATUS_STYLES = {
        Online: { badge: 'badge-success', title: 'Ran commands today' },
        Idle: { badge: 'badge-warning', title: 'Active, no commands today' },
        Offline: { badge: 'badge-gray', title: 'The bot is no longer in this server' }
    };

    /** Badge class and tooltip for a server status; anything unknown reads as Offline. */
    function statusStyle(status) {
        return STATUS_STYLES[status] || STATUS_STYLES.Offline;
    }

    /** "View all" or "View all 12" when the card shows only some of the servers. */
    function viewAllLabel(totalServerCount, shownCount, formatNumber) {
        if (totalServerCount > shownCount) {
            return 'View all ' + (formatNumber ? formatNumber(totalServerCount) : String(totalServerCount));
        }
        return 'View all';
    }

    /** An icon URL is only used when it is an absolute http(s) address. */
    function safeIconUrl(url) {
        return typeof url === 'string' && /^https?:\/\//i.test(url) ? url : null;
    }

    /** Only token background classes are applied from the payload. */
    function safeAvatarClass(value) {
        return typeof value === 'string' && /^bg-[a-z-]+$/.test(value) ? value : 'bg-accent-blue';
    }

    /** Only same-site paths are used as a row link. */
    function safeDetailUrl(url) {
        return typeof url === 'string' && /^\/(?!\/)/.test(url) ? url : '/Guilds';
    }

    // ---- Connected Servers rows ---------------------------------------------------------------

    function fill(row, server, doc, format) {
        var q = function (name) { return row.querySelector('[data-field="' + name + '"]'); };
        var number = function (n) { return format ? format.number(n) : String(n); };

        row.setAttribute('data-server-id', String(server.id));

        var link = q('link');
        link.setAttribute('href', safeDetailUrl(server.detailUrl));
        q('name').textContent = server.name || '';

        var avatar = q('avatar');
        avatar.textContent = '';
        var icon = safeIconUrl(server.iconUrl);
        if (icon) {
            var img = doc.createElement('img');
            img.setAttribute('src', icon);
            img.setAttribute('alt', '');
            img.setAttribute('loading', 'lazy');
            avatar.appendChild(img);
        } else {
            avatar.classList.add('text-white', safeAvatarClass(server.avatarClass));
            avatar.textContent = server.initials || '';
        }

        q('members').textContent = number(server.memberCount || 0);
        q('commands').textContent = number(server.commandsToday || 0);

        var style = statusStyle(server.status);
        var badge = q('status');
        badge.className = 'badge ' + style.badge;
        badge.setAttribute('title', style.title);
        badge.textContent = server.status || 'Offline';

        var copy = q('copy');
        copy.setAttribute('data-copy-server-id', String(server.id));
        copy.setAttribute('aria-label', 'Copy server ID for ' + (server.name || 'this server'));
        copy.setAttribute('title', 'Copy server ID');
    }

    /** Replaces the card's rows with `data.servers` (the ?handler=ConnectedServers payload). */
    function renderServers(doc, data, format) {
        var template = doc.getElementById('connected-server-template');
        var list = doc.getElementById('connected-servers-list');
        if (!template || !list || !data) return false;

        var servers = Array.isArray(data.servers) ? data.servers : [];
        list.textContent = '';
        servers.forEach(function (server) {
            var fragment = template.content.cloneNode(true);
            var row = fragment.querySelector('.connected-server');
            fill(row, server, doc, format);
            list.appendChild(fragment);
        });

        var any = servers.length > 0;
        list.hidden = !any;
        var head = doc.getElementById('connected-servers-head');
        if (head) head.hidden = !any;
        var empty = doc.getElementById('connected-servers-empty');
        if (empty) empty.hidden = any;

        var viewAll = doc.getElementById('connected-servers-view-all');
        if (viewAll) {
            viewAll.textContent = viewAllLabel(Number(data.totalServerCount) || 0, servers.length, format ? format.number : null);
        }
        return true;
    }

    // ---- Page behaviour (browser only) --------------------------------------------------------

    function notify(kind, message) {
        if (root.toast && typeof root.toast[kind] === 'function') root.toast[kind](message);
    }

    async function refreshServers() {
        var card = document.getElementById('connected-servers');
        if (!card) return;
        card.setAttribute('aria-busy', 'true');
        try {
            var data = await root.ApiClient.get(root.location.pathname + '?handler=ConnectedServers');
            renderServers(document, data, root.Format);
        } catch (error) {
            if (root.ApiClient && typeof root.ApiClient.showErrorToast === 'function') root.ApiClient.showErrorToast(error);
        } finally {
            card.removeAttribute('aria-busy');
        }
    }

    async function afterRestart() {
        if (!root.BotStatus || typeof root.BotStatus.watchRestart !== 'function') return;
        var online = await root.BotStatus.watchRestart();
        if (online) {
            notify('success', 'The bot is back online.');
            if (root.DashboardRealtime) root.DashboardRealtime.refreshStats();
        } else {
            notify('warning', 'The bot has not reconnected yet. Check the bot status again in a moment, or look at the logs.');
        }
    }

    async function afterSync() {
        await Promise.all([
            refreshServers(),
            root.DashboardRealtime ? root.DashboardRealtime.refreshStats() : Promise.resolve()
        ]);
    }

    async function copyServerId(button) {
        var id = button.getAttribute('data-copy-server-id');
        if (!id) return;
        try {
            await root.navigator.clipboard.writeText(id);
            notify('success', 'Server ID copied.');
        } catch (error) {
            notify('error', 'Could not copy the server ID. Open the server and copy it from the address bar.');
        }
    }

    function init() {
        document.addEventListener('quickactions:confirmed', function (event) {
            var id = event.detail && event.detail.modalId;
            if (id === 'restartBotModal') afterRestart();
            else if (id === 'syncGuildsModal') afterSync();
        });

        document.addEventListener('click', function (event) {
            var button = event.target.closest && event.target.closest('[data-copy-server-id]');
            if (button) copyServerId(button);
        });
    }

    return {
        init: init,
        statusStyle: statusStyle,
        viewAllLabel: viewAllLabel,
        safeIconUrl: safeIconUrl,
        safeAvatarClass: safeAvatarClass,
        safeDetailUrl: safeDetailUrl,
        renderServers: renderServers,
        refreshServers: refreshServers
    };
});
