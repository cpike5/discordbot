/**
 * Member directory: filter panel, role picker, bulk selection and the member detail dialog.
 *
 * Selection comes from bulk-selection.js (one set per member, so the table and card layouts
 * agree); the dialog is a quickActions dialog; requests go through ApiClient. Discord IDs are
 * handled as strings throughout.
 */
(function () {
    'use strict';

    var selection = null;
    var memberDialog = null;
    var currentMemberId = null;
    var loadToken = 0;

    function byId(id) { return document.getElementById(id); }

    function guildId() { return window.memberDirectoryGuildId; }

    /* ---------------------------------------------------------------- filter panel */

    function setupFilterPanel() {
        var toggle = byId('filterToggle');
        var content = byId('filterContent');
        var chevron = byId('filterChevron');
        if (!toggle || !content) return;

        toggle.addEventListener('click', function () {
            var expanded = toggle.getAttribute('aria-expanded') === 'true';
            toggle.setAttribute('aria-expanded', String(!expanded));
            content.classList.toggle('hidden');
            if (chevron) chevron.classList.toggle('rotate-180');
        });
    }

    function setupRoleMultiSelect() {
        var toggle = byId('roleMultiSelectToggle');
        var dropdown = byId('roleMultiSelectDropdown');
        if (!toggle || !dropdown) return;

        function setOpen(open, returnFocus) {
            toggle.setAttribute('aria-expanded', String(open));
            dropdown.classList.toggle('hidden', !open);
            if (!open && returnFocus) toggle.focus();
        }

        toggle.addEventListener('click', function (e) {
            e.preventDefault();
            setOpen(toggle.getAttribute('aria-expanded') !== 'true');
        });

        document.addEventListener('click', function (e) {
            if (!toggle.contains(e.target) && !dropdown.contains(e.target)) setOpen(false);
        });

        // Escape closes it from the button or from inside the list; tabbing out closes it too
        dropdown.parentElement.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && toggle.getAttribute('aria-expanded') === 'true') {
                e.stopPropagation();
                setOpen(false, true);
            }
        });
        dropdown.parentElement.addEventListener('focusout', function (e) {
            if (e.relatedTarget && !dropdown.parentElement.contains(e.relatedTarget)) setOpen(false);
        });

        dropdown.addEventListener('change', function () {
            var count = dropdown.querySelectorAll('.role-checkbox:checked').length;
            byId('roleSelectedText').textContent = count === 0
                ? 'All roles'
                : Format.plural(count, 'role', 'roles') + ' selected';
        });
    }

    /** "Joined before" must not be earlier than "Joined after"; the browser shows the message. */
    function setupDateValidation() {
        var after = byId('JoinedAfter');
        var before = byId('JoinedBefore');
        if (!after || !before) return;

        function check() {
            var invalid = after.value && before.value && before.value < after.value;
            before.setCustomValidity(invalid ? '"Joined before" must be on or after "Joined after".' : '');
        }
        after.addEventListener('input', check);
        before.addEventListener('input', check);
        check();
    }

    /* ---------------------------------------------------------------- bulk selection */

    function setupBulkSelection() {
        if (typeof BulkSelection === 'undefined') return;
        selection = BulkSelection.init({ noun: ['member', 'members'] });

        var exportButton = document.querySelector('[data-export-selected]');
        if (!exportButton) return;

        exportButton.addEventListener('click', function () {
            var ids = selection.ids();
            if (ids.length === 0) {
                toast.warning('Select at least one member to export.');
                return;
            }

            var params = new URLSearchParams();
            ids.forEach(function (id) { params.append('UserIds', id); });
            var url = exportButton.dataset.exportUrl + (exportButton.dataset.exportUrl.indexOf('?') >= 0 ? '&' : '?') + params.toString();

            // A file download does not navigate, so give the button a short pending state by hand
            LoadingManager.setButtonLoading(exportButton, true, 'Exporting…');
            window.location.assign(url);
            toast.info('Exporting ' + Format.plural(ids.length, 'member', 'members') + '. The download starts in a moment.');
            setTimeout(function () { LoadingManager.setButtonLoading(exportButton, false); }, 3000);
        });
    }

    /* ---------------------------------------------------------------- member dialog */

    function showPanel(which) {
        byId('memberDetailLoading').classList.toggle('hidden', which !== 'loading');
        byId('memberDetailError').classList.toggle('hidden', which !== 'error');
        byId('memberDetailContent').classList.toggle('hidden', which !== 'content');
    }

    function openMember(userId, trigger) {
        var modal = byId('memberDetailModal');
        if (!modal) return;

        memberDialog = modal;
        if (trigger && trigger.focus) trigger.focus();
        quickActions.openDialog(modal, { initialFocus: '[data-modal-dismiss][aria-label]' });
        loadMember(userId);
    }

    async function loadMember(userId) {
        currentMemberId = String(userId);
        var token = ++loadToken;
        showPanel('loading');

        try {
            var member = await ApiClient.get('/api/guilds/' + encodeURIComponent(guildId()) + '/members/' + encodeURIComponent(userId));
            if (token !== loadToken) return;
            populateMemberModal(member);
            showPanel('content');
        } catch (err) {
            if (token !== loadToken) return;
            var text = err && err.status === 404
                ? 'This member may have left the server.'
                : (err && err.message) || 'Something went wrong. Try again.';
            byId('memberDetailErrorText').textContent = text;
            showPanel('error');
        }
    }

    function populateMemberModal(member) {
        var avatar = byId('modalAvatar');
        var placeholder = byId('modalAvatarPlaceholder');

        if (member.avatarHash) {
            var ext = member.avatarHash.indexOf('a_') === 0 ? 'gif' : 'png';
            avatar.src = 'https://cdn.discordapp.com/avatars/' + member.userId + '/' + member.avatarHash + '.' + ext + '?size=160';
            avatar.classList.remove('hidden');
            placeholder.classList.add('hidden');
        } else {
            avatar.classList.add('hidden');
            placeholder.classList.remove('hidden');
            byId('modalAvatarInitials').textContent = (member.displayName || '?').substring(0, 2).toUpperCase();
        }

        byId('modalDisplayName').textContent = member.displayName;
        byId('modalUsername').textContent = '@' + member.username;
        byId('modalUserId').textContent = String(member.userId);
        byId('modalNickname').textContent = member.nickname || 'None';

        byId('modalJoinDate').textContent = Format.formatDate(member.joinedAt, 'date');
        byId('modalJoinAge').textContent = Format.relativeTime(member.joinedAt);

        if (member.accountCreatedAt) {
            byId('modalAccountCreated').textContent = Format.formatDate(member.accountCreatedAt, 'date');
            byId('modalAccountAge').textContent = Format.relativeTime(member.accountCreatedAt);
        } else {
            byId('modalAccountCreated').textContent = 'Unknown';
            byId('modalAccountAge').textContent = '';
        }

        if (member.lastActiveAt) {
            byId('modalLastActive').textContent = Format.relativeTime(member.lastActiveAt);
            byId('modalLastActiveExact').textContent = Format.formatDate(member.lastActiveAt, 'datetime');
        } else {
            byId('modalLastActive').textContent = 'Never';
            byId('modalLastActiveExact').textContent = '';
        }

        var roleList = byId('modalRoleList');
        roleList.replaceChildren();
        var roles = (member.roles || []).slice().sort(function (a, b) { return b.position - a.position; });
        byId('modalNoRoles').classList.toggle('hidden', roles.length > 0);
        byId('modalRoleCount').textContent = '(' + roles.length + ')';
        byId('modalRoleCountStat').textContent = String(roles.length);

        roles.forEach(function (role) {
            var chip = document.createElement('span');
            chip.className = 'inline-flex items-center gap-2 px-3 py-1.5 rounded text-sm font-medium text-white break-all';
            // The colour is the role's own Discord colour, so it is data rather than a token
            chip.style.backgroundColor = role.color > 0 ? '#' + role.color.toString(16).padStart(6, '0') : '#99aab5';
            chip.textContent = role.name;
            roleList.appendChild(chip);
        });

        var status = byId('modalMemberStatus');
        status.textContent = member.isActive ? 'Active' : 'Inactive';
        status.className = 'text-2xl font-bold ' + (member.isActive ? 'text-success' : 'text-text-secondary');

        var link = byId('modalModerationLink');
        if (link) {
            link.href = '/Guilds/' + encodeURIComponent(guildId()) + '/Members/' + encodeURIComponent(member.userId) + '/Moderation';
        }
    }

    async function copyUserId() {
        var id = byId('modalUserId').textContent;
        try {
            await navigator.clipboard.writeText(id);
            toast.success('User ID copied.');
        } catch (e) {
            toast.error('Could not copy the user ID. Select it and copy it by hand.');
        }
    }

    function setupMemberDialog() {
        document.addEventListener('click', function (e) {
            var view = e.target.closest && e.target.closest('[data-member-view]');
            if (view) {
                openMember(view.dataset.memberView, view);
                return;
            }
            if (e.target.closest && e.target.closest('[data-member-retry]') && currentMemberId) {
                loadMember(currentMemberId);
                return;
            }
            if (e.target.closest && e.target.closest('[data-copy-user-id]')) {
                copyUserId();
            }
        });
    }

    function init() {
        setupFilterPanel();
        setupRoleMultiSelect();
        setupDateValidation();
        setupBulkSelection();
        setupMemberDialog();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
