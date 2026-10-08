/**
 * Moderation settings: one real form per tab.
 *
 * - Tabs come from tab-panel.js (the active tab lives in the URL hash).
 * - Each tab is a <form data-unsaved-changes>, so dirty tracking is per tab and the leave-page
 *   warning comes from unsaved-changes.js, once, and only while something is unsaved.
 * - A save sends only the fields that changed, so settings this page does not show are never
 *   overwritten; the server merges them onto what is saved.
 * - Numbers are checked before they are sent, and the server's per-field messages land on the field.
 * - Choosing a protection level asks first, because it replaces the rules on three tabs.
 * - Every request goes through ApiClient with a pending state; nothing reloads the page.
 *
 * Tag names are user-written, so they reach the DOM only through textContent and data-*.
 */
(function () {
    'use strict';

    var TAB_PANEL_ID = 'modSettingsTabs';
    var SECTION_FORMS = {
        spam: { formId: 'spamForm', handler: 'SaveSpam', configKey: 'spamConfig', label: 'spam' },
        content: { formId: 'contentForm', handler: 'SaveContent', configKey: 'contentFilterConfig', label: 'content' },
        raid: { formId: 'raidForm', handler: 'SaveRaid', configKey: 'raidProtectionConfig', label: 'raid' }
    };

    var guildId = null;
    var savedPreset = null;
    var baselines = new WeakMap();
    var busy = new WeakSet();

    function byId(id) { return document.getElementById(id); }

    function handlerUrl(handler, extra) {
        return '?handler=' + handler + '&guildId=' + encodeURIComponent(guildId) + (extra || '');
    }

    function failure(err, fallback) {
        // The session-expired toast already says it
        if (err && err.sessionExpired) return;
        toast.error((err && err.message) || fallback);
    }

    /* ---------------------------------------------------------------- field values */

    function controlsOf(form) {
        return Array.prototype.slice.call(form.querySelectorAll('[data-field]'));
    }

    /** The value a control would send, in the shape the server expects. NaN when a number is not valid. */
    function readValue(control) {
        if (control.type === 'checkbox') return control.checked;

        var raw = control.value.trim();
        switch (control.dataset.parse) {
            case 'int': return raw === '' || !/^-?\d+$/.test(raw) ? NaN : parseInt(raw, 10);
            case 'percent': return raw === '' || !/^-?\d+$/.test(raw) ? NaN : parseInt(raw, 10) / 100;
            case 'list': return raw.split(',').map(function (w) { return w.trim(); }).filter(Boolean);
            default: return raw;
        }
    }

    function sameValue(a, b) {
        if (Array.isArray(a) && Array.isArray(b)) {
            return a.length === b.length && a.every(function (v, i) { return v === b[i]; });
        }
        if (typeof a === 'number' && typeof b === 'number') return Math.abs(a - b) < 1e-9;
        return a === b;
    }

    function snapshot(form) {
        var values = {};
        controlsOf(form).forEach(function (c) { values[c.dataset.field] = readValue(c); });
        return values;
    }

    function rebase(form) {
        baselines.set(form, snapshot(form));
        if (window.UnsavedChanges) UnsavedChanges.markClean(form);
    }

    /** Only the fields whose value differs from what was last saved. */
    function changedFields(form) {
        var base = baselines.get(form) || {};
        var patch = {};
        controlsOf(form).forEach(function (c) {
            var value = readValue(c);
            if (!sameValue(value, base[c.dataset.field])) patch[c.dataset.field] = value;
        });
        return patch;
    }

    /** Write saved values back into a form's controls (after the server returns them). */
    function populate(form, settings) {
        if (!settings) return;
        controlsOf(form).forEach(function (c) {
            var key = c.dataset.field;
            if (!(key in settings)) return;
            var value = settings[key];
            if (c.type === 'checkbox') c.checked = !!value;
            else if (c.dataset.parse === 'percent') c.value = String(Math.round(value * 100));
            else if (c.dataset.parse === 'list') c.value = (value || []).join(', ');
            else c.value = String(value);
        });
    }

    /* ---------------------------------------------------------------- field errors */

    function labelOf(control) {
        var label = control.id && document.querySelector('label[for="' + control.id + '"]');
        return label ? label.textContent.replace('*', '').trim() : 'This field';
    }

    function clearFieldError(control) {
        control.classList.remove('input-validation-error');
        control.removeAttribute('aria-invalid');
        var message = byId(control.id + '-error');
        if (message) message.remove();
        var described = (control.getAttribute('aria-describedby') || '').split(' ').filter(function (id) {
            return id && id !== control.id + '-error';
        });
        if (described.length) control.setAttribute('aria-describedby', described.join(' '));
        else control.removeAttribute('aria-describedby');
    }

    function showFieldError(control, text) {
        clearFieldError(control);
        control.classList.add('input-validation-error');
        control.setAttribute('aria-invalid', 'true');

        var message = document.createElement('p');
        message.id = control.id + '-error';
        message.className = 'form-error';
        message.setAttribute('role', 'alert');
        message.textContent = text;

        var host = control.closest('.space-y-1\\.5') || control.parentElement;
        host.appendChild(message);

        var described = (control.getAttribute('aria-describedby') || '').split(' ').filter(Boolean);
        described.push(message.id);
        control.setAttribute('aria-describedby', described.join(' '));
    }

    function clearErrors(form) {
        controlsOf(form).forEach(clearFieldError);
    }

    /** Plain-language message for a number that is out of range, empty or not whole. */
    function problemWith(control) {
        if (control.type !== 'number') return null;
        var v = control.validity;
        var label = labelOf(control);
        if (v.valueMissing || v.badInput) return label + ' needs a whole number.';
        if (v.rangeUnderflow || v.rangeOverflow) return label + ' must be between ' + control.min + ' and ' + control.max + '.';
        if (v.stepMismatch) return label + ' must be a whole number.';
        if (!/^-?\d+$/.test(control.value.trim())) return label + ' must be a whole number.';
        return null;
    }

    /** Check every number in the form; show messages and focus the first bad one. */
    function validate(form) {
        var first = null;
        controlsOf(form).forEach(function (control) {
            var text = problemWith(control);
            if (text) {
                showFieldError(control, text);
                if (!first) first = control;
            }
        });
        if (first) first.focus();
        return !first;
    }

    /** Put the server's per-field messages on the fields; true when any landed. */
    function showServerErrors(form, errors) {
        if (!errors || typeof errors !== 'object') return false;
        var first = null;
        Object.keys(errors).forEach(function (key) {
            var control = form.querySelector('[data-field="' + key + '"]');
            if (!control) return;
            showFieldError(control, errors[key]);
            if (!first) first = control;
        });
        if (first) first.focus();
        return !!first;
    }

    /* ---------------------------------------------------------------- saving */

    async function saveForm(form, handler, payload, fallback) {
        if (busy.has(form)) return null;
        busy.add(form);

        var button = form.querySelector('button[type="submit"]');
        LoadingManager.setButtonLoading(button, true, button && button.dataset.loadingText);
        form.setAttribute('aria-busy', 'true');
        try {
            return await ApiClient.post(handlerUrl(handler), payload);
        } catch (err) {
            var onField = err && (err.status === 400 || err.status === 409) && showServerErrors(form, err.data && err.data.errors);
            if (onField) {
                // A conflict says what clashed ("a tag named X already exists"); a 400 says to look at the fields
                toast.error(err.status === 409 ? err.message : 'Some values need fixing. Check the highlighted fields.');
            } else {
                failure(err, fallback);
            }
            return null;
        } finally {
            busy.delete(form);
            form.removeAttribute('aria-busy');
            LoadingManager.setButtonLoading(button, false);
        }
    }

    function updateActiveRules(count) {
        var el = byId('activeRulesCount');
        if (el && typeof count === 'number') el.textContent = String(count);
    }

    async function submitSection(form, section) {
        clearErrors(form);
        if (!validate(form)) return;

        var patch = changedFields(form);
        if (Object.keys(patch).length === 0) {
            toast.info('No changes to save.');
            return;
        }

        var result = await saveForm(form, section.handler, patch, 'Could not save the ' + section.label + ' settings. Try again.');
        if (!result) return;

        populate(form, result.settings);
        rebase(form);
        updateActiveRules(result.activeRules);
        toast.success(result.message || 'Saved.');
    }

    /** The overview's values as the server expects them: the mode, the mod-log channel and its kinds. */
    function readOverview(form) {
        var checked = form.querySelector('input[name="mode"]:checked');
        var channel = byId('modlog-channel');
        var kinds = 0;
        modLogKinds(form).forEach(function (box) {
            if (box.checked) kinds |= parseInt(box.dataset.modlogKind, 10);
        });
        return {
            mode: checked ? parseInt(checked.value, 10) : null,
            modLogChannelId: channel ? channel.value : '',
            modLogEvents: kinds
        };
    }

    function modLogKinds(form) {
        return Array.prototype.slice.call(form.querySelectorAll('[data-modlog-kind]'));
    }

    /** The kind toggles mean nothing without a channel, so they follow the channel's state. */
    function syncModLogKinds(form) {
        var channel = byId('modlog-channel');
        var off = !channel || channel.value === '';
        modLogKinds(form).forEach(function (box) {
            box.disabled = off;
            var label = box.closest('.toggle');
            if (label) label.classList.toggle('toggle-disabled', off);
        });
    }

    async function submitOverview(form) {
        clearErrors(form);
        var current = readOverview(form);
        var base = baselines.get(form) || {};
        var patch = {};

        if (current.mode !== null && current.mode !== base.mode) patch.mode = current.mode;
        if (current.modLogChannelId !== base.modLogChannelId) patch.modLogChannelId = current.modLogChannelId;
        if (current.modLogEvents !== base.modLogEvents) patch.modLogEvents = current.modLogEvents;

        if (Object.keys(patch).length === 0) {
            toast.info('No changes to save.');
            return;
        }

        var result = await saveForm(form, 'SaveOverview', patch, 'Could not save the overview settings. Try again.');
        if (!result) return;

        baselines.set(form, {
            mode: typeof result.mode === 'number' ? result.mode : current.mode,
            modLogChannelId: typeof result.modLogChannelId === 'string' ? result.modLogChannelId : current.modLogChannelId,
            modLogEvents: typeof result.modLogEvents === 'number' ? result.modLogEvents : current.modLogEvents
        });
        if (window.UnsavedChanges) UnsavedChanges.markClean(form);
        toast.success(result.message || 'Saved.');
    }

    /* ---------------------------------------------------------------- mode and presets */

    function showMode(value) {
        var simple = String(value) === '0';
        byId('simpleMode').classList.toggle('hidden', !simple);
        byId('advancedMode').classList.toggle('hidden', simple);
    }

    function setupMode() {
        var form = byId('overviewForm');
        baselines.set(form, readOverview(form));
        syncModLogKinds(form);

        form.addEventListener('change', function (e) {
            if (e.target.name === 'mode') showMode(e.target.value);
            if (e.target.id === 'modlog-channel') {
                syncModLogKinds(form);
                clearFieldError(e.target);
            }
        });
    }

    function presetRadios() {
        return Array.prototype.slice.call(document.querySelectorAll('input[name="preset"]'));
    }

    function checkPreset(value) {
        presetRadios().forEach(function (r) { r.checked = r.value === value; });
    }

    function anySectionDirty() {
        return Object.keys(SECTION_FORMS).some(function (key) {
            return window.UnsavedChanges && UnsavedChanges.isDirty(byId(SECTION_FORMS[key].formId));
        });
    }

    async function choosePreset(radio) {
        var name = radio.value;
        if (name === savedPreset) return;

        var warning = 'This replaces your Spam, Content and Raid settings with the ' + name + ' level, ' +
            'including your custom blocklist.';
        if (anySectionDirty()) warning += ' Edits you have not saved on those tabs will be lost.';

        var confirmed = await quickActions.confirm({
            title: 'Apply the ' + name + ' level?',
            message: warning,
            variant: 'warning',
            confirmText: 'Apply ' + name,
            cancelText: 'Keep current settings'
        });
        if (!confirmed) {
            checkPreset(savedPreset);
            return;
        }

        var group = radio.closest('fieldset');
        var status = byId('presetStatus');
        presetRadios().forEach(function (r) { r.disabled = true; });
        if (group) group.setAttribute('aria-busy', 'true');
        if (status) status.textContent = 'Applying the ' + name + ' level…';

        try {
            var result = await ApiClient.post(handlerUrl('ApplyPreset'), { presetName: name });
            var config = result.config || {};

            populate(byId(SECTION_FORMS.spam.formId), config.spamConfig);
            populate(byId(SECTION_FORMS.content.formId), config.contentFilterConfig);
            populate(byId(SECTION_FORMS.raid.formId), config.raidProtectionConfig);
            Object.keys(SECTION_FORMS).forEach(function (key) {
                var form = byId(SECTION_FORMS[key].formId);
                clearErrors(form);
                rebase(form);
            });

            // A preset puts the guild in Simple mode
            var overview = byId('overviewForm');
            var simple = overview.querySelector('input[name="mode"][value="0"]');
            if (simple) simple.checked = true;
            baselines.set(overview, { mode: 0 });
            if (window.UnsavedChanges) UnsavedChanges.markClean(overview);
            showMode(0);

            savedPreset = name;
            updateActiveRules(result.activeRules);
            if (status) status.textContent = 'The ' + name + ' level is applied.';
            toast.success(result.message || 'Preset applied.');
        } catch (err) {
            checkPreset(savedPreset);
            if (status) status.textContent = '';
            failure(err, 'Could not apply the preset. Try again.');
        } finally {
            presetRadios().forEach(function (r) { r.disabled = false; });
            if (group) group.removeAttribute('aria-busy');
        }
    }

    /* ---------------------------------------------------------------- tabs: dirty marks and links */

    function markTabDirty(form, dirty) {
        var panel = form.closest('[data-tab-id]');
        if (!panel) return;
        var tab = byId(TAB_PANEL_ID + '-tab-' + panel.dataset.tabId);
        if (!tab) return;

        var dot = tab.querySelector('.tab-dirty-dot');
        if (dirty && !dot) {
            dot = document.createElement('span');
            dot.className = 'tab-dirty-dot';
            dot.setAttribute('role', 'img');
            dot.setAttribute('aria-label', 'Unsaved changes');
            tab.appendChild(dot);
        } else if (!dirty && dot) {
            dot.remove();
        }
    }

    function setupTabs() {
        document.addEventListener('unsavedchange', function (e) {
            if (e.target && e.target.matches && e.target.matches('form[data-settings-form]')) {
                markTabDirty(e.target, !!(e.detail && e.detail.dirty));
            }
        });

        document.addEventListener('click', function (e) {
            var link = e.target.closest && e.target.closest('[data-switch-tab]');
            if (link && window.TabPanel) {
                window.TabPanel.switchTo(TAB_PANEL_ID, link.dataset.switchTab);
            }
        });
    }

    /* ---------------------------------------------------------------- tags */

    function tagsList() { return byId('tags-list'); }

    function refreshTagsEmpty() {
        byId('tags-empty').classList.toggle('hidden', tagsList().children.length > 0);
    }

    /** One row of the tag list, built to match the server-rendered markup. */
    function buildTagRow(name, cssClass, userCount) {
        var row = document.createElement('li');
        row.className = 'flex flex-wrap items-center justify-between gap-3 p-3 bg-bg-tertiary rounded-lg';
        row.dataset.tagName = name;

        var left = document.createElement('div');
        left.className = 'flex flex-wrap items-center gap-3 min-w-0';

        var chip = document.createElement('span');
        chip.className = 'user-tag break-all' + (cssClass ? ' ' + cssClass : '');
        chip.textContent = name;
        left.appendChild(chip);

        var used = document.createElement('span');
        used.className = 'text-sm text-text-secondary';
        used.textContent = userCount > 0 ? 'Used by ' + Format.plural(userCount, 'member', 'members') : 'Not used yet';
        left.appendChild(used);

        var del = document.createElement('button');
        del.type = 'button';
        del.className = 'btn btn-ghost btn-icon text-error';
        del.dataset.deleteTag = name;
        del.setAttribute('aria-label', 'Delete tag ' + name);
        del.title = 'Delete tag';
        del.innerHTML = '<svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">' +
            '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" /></svg>';

        row.appendChild(left);
        row.appendChild(del);
        return row;
    }

    async function createTag(form) {
        var nameInput = byId('new-tag-name');
        var category = byId('new-tag-category');
        var name = nameInput.value.trim();

        clearFieldError(nameInput);
        if (!name) {
            showFieldError(nameInput, 'Give the tag a name.');
            nameInput.focus();
            return;
        }
        if (name.length > 50) {
            showFieldError(nameInput, 'Tag names can be up to 50 characters.');
            nameInput.focus();
            return;
        }

        var result = await saveForm(form, 'CreateTag', { name: name, category: parseInt(category.value, 10) }, 'Could not add the tag. Try again.');
        if (!result) return;

        tagsList().appendChild(buildTagRow(result.tag.name, result.cssClass, result.tag.userCount || 0));
        refreshTagsEmpty();
        nameInput.value = '';
        category.value = String(0);
        nameInput.focus();
        toast.success('Added the tag "' + result.tag.name + '".');
    }

    async function deleteTag(button) {
        var name = button.dataset.deleteTag;

        var confirmed = await quickActions.confirm({
            title: 'Delete tag',
            message: 'Delete the tag "' + name + '"? It is removed from every member who has it.',
            variant: 'danger',
            confirmText: 'Delete tag'
        });
        if (!confirmed) return;

        LoadingManager.setButtonLoading(button, true);
        try {
            await ApiClient.post(handlerUrl('DeleteTag', '&tagName=' + encodeURIComponent(name)));

            var row = button.closest('li');
            if (row) row.remove();
            refreshTagsEmpty();
            byId('new-tag-name').focus();
            toast.success('Deleted the tag "' + name + '".');
        } catch (err) {
            LoadingManager.setButtonLoading(button, false);
            failure(err, 'Could not delete the tag. Try again.');
        }
    }

    /* ---------------------------------------------------------------- import templates */

    function importError(text) {
        var el = byId('importTemplatesError');
        el.textContent = text || '';
        el.classList.toggle('hidden', !text);
    }

    function markImported(names) {
        var lower = names.map(function (n) { return n.toLowerCase(); });
        document.querySelectorAll('[data-template-option]').forEach(function (box) {
            if (lower.indexOf(box.value.toLowerCase()) === -1) return;
            box.checked = false;
            box.disabled = true;
            var label = box.closest('label');
            if (label) {
                label.classList.add('opacity-60');
                label.classList.remove('cursor-pointer');
                var note = document.createElement('span');
                note.className = 'ml-2 text-xs text-text-secondary';
                note.textContent = 'Already added';
                box.nextElementSibling.querySelector('.user-tag').after(note);
            }
        });
    }

    function setupImport() {
        var dialog = byId('importTemplatesDialog');
        var form = byId('importTemplatesForm');

        document.addEventListener('click', function (e) {
            if (e.target.closest && e.target.closest('[data-open-import]')) {
                importError('');
                quickActions.openDialog(dialog, { initialFocus: '[data-template-option]:not(:disabled)' });
            }
        });

        form.addEventListener('submit', async function (e) {
            e.preventDefault();
            var names = Array.prototype.slice.call(form.querySelectorAll('[data-template-option]:checked'))
                .map(function (box) { return box.value; });

            if (names.length === 0) {
                importError('Choose at least one template tag.');
                return;
            }
            importError('');

            var result = await saveForm(form, 'ImportTemplates', names, 'Could not import the templates. Try again.');
            if (!result) return;

            (result.tags || []).forEach(function (tag) {
                tagsList().appendChild(buildTagRow(tag.name, tag.cssClass, tag.userCount || 0));
            });
            refreshTagsEmpty();
            markImported((result.tags || []).map(function (t) { return t.name; }));
            quickActions.closeDialog(dialog);
            toast.success(result.message || 'Templates imported.');
        });
    }

    /* ---------------------------------------------------------------- wiring */

    function init() {
        var data = window.moderationData || {};
        guildId = data.guildId;
        savedPreset = data.simplePreset || null;

        setupMode();
        setupTabs();
        setupImport();

        Object.keys(SECTION_FORMS).forEach(function (key) {
            var section = SECTION_FORMS[key];
            var form = byId(section.formId);
            if (!form) return;
            baselines.set(form, snapshot(form));

            form.addEventListener('submit', function (e) {
                e.preventDefault();
                submitSection(form, section);
            });
            // Fix a field and its message goes away
            form.addEventListener('input', function (e) {
                if (e.target.dataset && e.target.dataset.field) clearFieldError(e.target);
            });
        });

        byId('overviewForm').addEventListener('submit', function (e) {
            e.preventDefault();
            submitOverview(byId('overviewForm'));
        });

        var newTagForm = byId('newTagForm');
        newTagForm.addEventListener('submit', function (e) {
            e.preventDefault();
            createTag(newTagForm);
        });
        byId('new-tag-name').addEventListener('input', function (e) { clearFieldError(e.target); });

        document.addEventListener('change', function (e) {
            if (e.target.matches && e.target.matches('input[name="preset"]')) choosePreset(e.target);
        });

        document.addEventListener('click', function (e) {
            var del = e.target.closest && e.target.closest('[data-delete-tag]');
            if (del) deleteTag(del);
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
