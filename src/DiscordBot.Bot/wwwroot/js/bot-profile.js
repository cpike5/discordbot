/**
 * Bot profile card (Admin > Settings > Bot Control).
 *
 * Markup contract (Pages/Admin/Settings.cshtml, inside `[data-bot-profile]`):
 *  - `[data-bot-profile-loading]`, `[data-bot-profile-unavailable]` and `form[data-bot-profile-form]`
 *    are the card's three states; exactly one is shown.
 *  - The profile is read from Discord (`?handler=BotProfile`) the first time the Bot Control tab is
 *    shown, not with the page, so Settings never waits on Discord.
 *  - Saving posts the form as multipart to `?handler=SaveBotProfile` and then reads the profile
 *    again, so the card shows what Discord now has. The server sends Discord only what changed.
 *  - The form is tracked by unsaved-changes.js; it is not one of the `data-settings-form` tabs, so
 *    "Save all changes" leaves it alone and it has its own Save button.
 *
 * Exposed as window.botProfile (browser) and module.exports (Node/tests; the pure helpers).
 */
(function (root, factory) {
    if (typeof module === 'object' && module.exports) {
        module.exports = factory(root);
    } else {
        root.botProfile = factory(root);
        if (typeof document !== 'undefined') {
            if (document.readyState === 'loading') {
                document.addEventListener('DOMContentLoaded', root.botProfile.init);
            } else {
                root.botProfile.init();
            }
        }
    }
})(typeof self !== 'undefined' ? self : this, function (root) {
    'use strict';

    var TABS_ID = 'settingsTabs';
    var TAB_ID = 'BotControl';
    var MAX_IMAGE_BYTES = 8 * 1024 * 1024;
    var IMAGE_TYPES = ['image/png', 'image/jpeg', 'image/gif', 'image/webp'];

    // ------------------------------------------------------------------ pure helpers

    /**
     * Why a chosen file cannot be used, or null when it can. The server checks again (by the
     * file's bytes); this only saves an upload that is certain to be refused.
     * @param {{size: number, type: string}|null} file
     * @param {string} label "avatar" or "banner"
     */
    function imageProblem(file, label) {
        if (!file) return null;
        if (file.size === 0) return 'The ' + label + ' file is empty.';
        if (file.size > MAX_IMAGE_BYTES) return 'The ' + label + ' file is too large. The limit is 8 MB.';
        if (file.type && IMAGE_TYPES.indexOf(file.type) < 0) return 'The ' + label + ' must be a PNG, JPEG, GIF or WebP image.';
        return null;
    }

    /** Why the username cannot be used, or null when it can. */
    function usernameProblem(value) {
        var length = String(value || '').trim().length;
        return length < 2 || length > 32 ? 'The username must be 2 to 32 characters long.' : null;
    }

    /**
     * Reads a save answer: { kind: 'saved'|'unchanged'|'error', message }.
     * `unchanged` is a save that went through but changed nothing (changeCount 0).
     */
    function readSaveResult(result) {
        var data = (result && result.data && typeof result.data === 'object') ? result.data : {};
        if (result && result.ok && data.success !== false) {
            var unchanged = data.changeCount === 0;
            return { kind: unchanged ? 'unchanged' : 'saved', message: data.message || (unchanged ? 'Nothing changed.' : 'Bot profile saved.') };
        }
        var errors = Array.isArray(data.errors) ? data.errors.filter(Boolean) : [];
        return {
            kind: 'error',
            message: errors.length ? errors.join(' ') : (data.message || 'The profile could not be saved. Try again.')
        };
    }

    // ------------------------------------------------------------------ page behaviour

    var requested = false;
    var profile = null;
    var previewUrls = {};

    function qs(selector, scope) { return (scope || document).querySelector(selector); }

    function card() { return qs('[data-bot-profile]'); }
    function form() { return qs('form[data-bot-profile-form]'); }

    function notify(kind, message, options) {
        if (root.toast && typeof root.toast[kind] === 'function') root.toast[kind](message, options || {});
    }

    /** Shows one of the card's states: 'loading', 'unavailable' or 'form'. */
    function show(state, message) {
        var scope = card();
        if (!scope) return;
        qs('[data-bot-profile-loading]', scope).classList.toggle('hidden', state !== 'loading');
        var unavailable = qs('[data-bot-profile-unavailable]', scope);
        unavailable.classList.toggle('hidden', state !== 'unavailable');
        if (state === 'unavailable') {
            var text = qs('[data-alert] p', unavailable);
            if (text) text.textContent = message || 'The profile could not be loaded.';
        }
        form().classList.toggle('hidden', state !== 'form');
    }

    function showError(message) {
        var box = qs('[data-bot-profile-error]', form());
        if (!box) return;
        var text = qs('[data-alert] p', box);
        if (text) text.textContent = message;
        box.classList.remove('hidden');
        box.focus();
    }

    function hideError() {
        var box = qs('[data-bot-profile-error]', form());
        if (box) box.classList.add('hidden');
    }

    function releasePreview(kind) {
        if (previewUrls[kind]) {
            URL.revokeObjectURL(previewUrls[kind]);
            delete previewUrls[kind];
        }
    }

    function chosenFile(kind) {
        var input = qs('input[type="file"][name="' + kind + '"]', form());
        return input && input.files && input.files[0] ? input.files[0] : null;
    }

    function removing(kind) {
        var box = qs('[data-bot-profile-remove="' + kind + '"]', form());
        return !!(box && box.checked);
    }

    /** Draws the preview from the form as it stands: chosen files and "remove" boxes included. */
    function drawPreview() {
        var f = form();
        if (!f || !profile) return;

        var name = qs('[data-bot-profile-name]', f);
        name.textContent = f.elements.username.value.trim() || profile.username || '';
        var about = f.elements.aboutMe.value;
        qs('[data-bot-profile-about]', f).textContent = about;
        qs('[data-bot-profile-about-count]', f).textContent = String(about.length);

        ['avatar', 'banner'].forEach(function (kind) {
            var image = qs('[data-bot-profile-' + kind + ']', f);
            var file = chosenFile(kind);
            releasePreview(kind);
            var src;
            if (file && !imageProblem(file, kind)) {
                src = previewUrls[kind] = URL.createObjectURL(file);
            } else if (removing(kind)) {
                src = kind === 'avatar' ? profile.defaultAvatarUrl : null;
            } else {
                src = profile[kind + 'Url'];
            }
            if (src) image.src = src; else image.removeAttribute('src');
            // The avatar always has something to show; a missing banner is an empty strip
            if (kind === 'banner') image.classList.toggle('hidden', !src);
        });
    }

    /** Puts the profile Discord reported into the form and takes it as the saved baseline. */
    function fill(data) {
        var f = form();
        profile = {
            username: data.username || '',
            aboutMe: data.aboutMe || '',
            avatarUrl: data.avatarUrl || '',
            bannerUrl: data.bannerUrl || null,
            // What "remove the avatar" will look like: the current one when it already is the default
            defaultAvatarUrl: data.hasCustomAvatar ? 'https://cdn.discordapp.com/embed/avatars/0.png' : (data.avatarUrl || '')
        };
        f.elements.username.value = profile.username;
        f.elements.aboutMe.value = profile.aboutMe;
        f.elements.avatar.value = '';
        f.elements.banner.value = '';
        f.elements.removeAvatar.checked = false;
        f.elements.removeBanner.checked = false;
        f.elements.removeAvatar.disabled = !data.hasCustomAvatar;
        f.elements.removeBanner.disabled = !profile.bannerUrl;
        drawPreview();
        if (root.UnsavedChanges) root.UnsavedChanges.markClean(f);
    }

    async function load() {
        if (!card()) return;
        requested = true;
        show('loading');
        try {
            var data = await root.ApiClient.get('?handler=BotProfile');
            if (!data || !data.available) {
                show('unavailable', data && data.message);
                return;
            }
            fill(data);
            show('form');
        } catch (error) {
            show('unavailable', 'The profile could not be loaded. Try again.');
        }
    }

    async function save() {
        var f = form();
        hideError();

        var problem = usernameProblem(f.elements.username.value)
            || imageProblem(chosenFile('avatar'), 'avatar')
            || imageProblem(chosenFile('banner'), 'banner');
        if (problem) {
            showError(problem);
            return;
        }

        var body = new FormData();
        body.append('username', f.elements.username.value.trim());
        body.append('aboutMe', f.elements.aboutMe.value);
        ['avatar', 'banner'].forEach(function (kind) {
            var file = chosenFile(kind);
            if (file) body.append(kind, file);
        });
        body.append('removeAvatar', removing('avatar') ? 'true' : 'false');
        body.append('removeBanner', removing('banner') ? 'true' : 'false');

        var button = qs('[data-bot-profile-save]', f);
        var loading = root.LoadingManager;
        if (button && loading) loading.setButtonLoading(button, true, 'Saving...');

        var outcome;
        try {
            var result = await root.ApiClient.postRaw('?handler=SaveBotProfile', body);
            // An expired session already has its own "sign in again" toast
            if (result.sessionExpired) return;
            outcome = readSaveResult(result);
        } catch (error) {
            outcome = { kind: 'error', message: (error && error.message) || 'The profile could not be saved. Try again.' };
        } finally {
            if (button && loading) loading.setButtonLoading(button, false);
        }

        if (outcome.kind === 'error') {
            // The edits stay on screen. When part of the change went through, the message says which
            showError(outcome.message);
            return;
        }

        notify(outcome.kind === 'unchanged' ? 'info' : 'success', outcome.message, { key: 'bot-profile-save' });
        if (root.UnsavedChanges) root.UnsavedChanges.markClean(f);
        await load();
    }

    function isActiveTab() {
        var tab = qs('#' + TABS_ID + '-container .tab-panel-tab.active');
        return !!tab && tab.dataset.tabId === TAB_ID;
    }

    function init() {
        if (!card()) return;

        document.addEventListener('tabchange', function (event) {
            var detail = event.detail || {};
            if (detail.panelId === TABS_ID && detail.tabId === TAB_ID && !requested) load();
        });

        form().addEventListener('submit', function (event) {
            event.preventDefault();
            save();
        });
        form().addEventListener('input', drawPreview);
        form().addEventListener('change', function (event) {
            var target = event.target;
            // Choosing a file and ticking "remove" contradict each other; the last one wins
            if (target.type === 'file' && target.files && target.files[0]) {
                var box = qs('[data-bot-profile-remove="' + target.name + '"]', form());
                if (box) box.checked = false;
                var problem = imageProblem(target.files[0], target.name);
                if (problem) showError(problem); else hideError();
            } else if (target.dataset && target.dataset.botProfileRemove && target.checked) {
                form().elements[target.dataset.botProfileRemove].value = '';
            }
            drawPreview();
        });

        card().addEventListener('click', function (event) {
            if (event.target.closest && event.target.closest('[data-bot-profile-retry]')) load();
        });

        if (isActiveTab()) load();
    }

    return {
        init: init,
        load: load,
        // pure helpers, for tests
        imageProblem: imageProblem,
        usernameProblem: usernameProblem,
        readSaveResult: readSaveResult
    };
});
