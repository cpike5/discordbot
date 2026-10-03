/**
 * Audio Settings page (Pages/Guilds/AudioSettings).
 *
 * One form, one Save: every setting (general, limits, TTS, command permissions) is checked in the
 * browser and again on the server, then saved together, so the page is either saved or not. The
 * form opts in to unsaved-changes tracking; a successful save or reset marks it clean.
 *
 * Numbers are read as whole numbers or refused. An empty or garbled field is an error on that
 * field, never a quiet 0 or a default. The pure helpers are exported for node --test.
 */
(function (root, factory) {
    var api = factory(root);
    if (typeof module === 'object' && module.exports) {
        module.exports = api;
    } else {
        root.audioSettings = api;
        if (root.document.readyState === 'loading') {
            root.document.addEventListener('DOMContentLoaded', function () { api.init(); });
        } else {
            api.init();
        }
    }
})(typeof self !== 'undefined' ? self : this, function (root) {
    'use strict';

    // loading-manager.js declares a top-level const, so it is a global binding but not window.LoadingManager
    function loadingManager() {
        return typeof LoadingManager !== 'undefined' ? LoadingManager : { setButtonLoading: function (b, on) { b.disabled = on; } };
    }

    // ---------------------------------------------------------------- pure helpers

    /** A whole number from field text, or null. Empty, decimals, "12abc" and "1e3" are all null. */
    function parseWholeNumber(raw) {
        var text = String(raw === undefined || raw === null ? '' : raw).trim();
        return /^-?\d+$/.test(text) ? parseInt(text, 10) : null;
    }

    /**
     * What is wrong with one numeric field, in plain language, or null when it is fine.
     * @param {string} raw - the field's text
     * @param {{label: string, min: number, max: number, unit?: string}} rule
     */
    function checkNumber(raw, rule) {
        var n = parseWholeNumber(raw);
        if (n === null) return 'Enter ' + rule.label + ' as a whole number.';
        if (n < rule.min || n > rule.max) {
            var label = rule.label.charAt(0).toUpperCase() + rule.label.slice(1);
            return label + ' must be from ' + rule.min + ' to ' + rule.max + (rule.unit ? ' ' + rule.unit : '') + '.';
        }
        return null;
    }

    var api = {
        parseWholeNumber: parseWholeNumber,
        checkNumber: checkNumber,
        init: init
    };

    // ---------------------------------------------------------------- page behaviour

    function init() {
        var doc = root.document;
        var form = doc.getElementById('audioSettingsForm');
        var cfg = root.audioSettingsConfig;
        if (!form || !cfg) return;

        var guildId = String(cfg.guildId);
        var url = function (handler) { return '/Guilds/AudioSettings/' + guildId + '?handler=' + handler; };
        var $ = function (id) { return doc.getElementById(id); };
        var saveBtn = $('saveSettingsBtn');
        var resetBtn = $('resetDefaultsBtn');
        var busy = false;

        var NUMBER_IDS = ['autoLeaveTimeout', 'maxDuration', 'maxFileSize', 'maxSounds', 'maxSsmlComplexity'];

        // ------------------------------------------------------------ field errors

        function setFieldError(id, message) {
            var input = $(id);
            var holder = $(id + '-error');
            if (holder) {
                holder.textContent = message || '';
                holder.classList.toggle('hidden', !message);
            }
            if (input) {
                input.classList.toggle('input-validation-error', !!message);
                if (message) input.setAttribute('aria-invalid', 'true'); else input.removeAttribute('aria-invalid');
            }
        }

        function clearFieldErrors() {
            NUMBER_IDS.concat(['defaultStyle']).forEach(function (id) { setFieldError(id, ''); });
        }

        function ruleFor(input) {
            return {
                label: input.dataset.label,
                min: parseInt(input.min, 10),
                max: parseInt(input.max, 10),
                unit: input.dataset.unit
            };
        }

        /** Check every number on the page. Returns { id: message } for the bad ones. */
        function checkNumbers() {
            var errors = {};
            NUMBER_IDS.forEach(function (id) {
                var input = $(id);
                if (!input) return;
                // badInput: the browser holds text it could not read as a number, and reports ''
                var raw = input.validity && input.validity.badInput ? 'x' : input.value;
                var message = checkNumber(raw, ruleFor(input));
                if (message) errors[id] = message;
            });
            return errors;
        }

        function showErrors(errors) {
            clearFieldErrors();
            var first = null;
            Object.keys(errors).forEach(function (id) {
                setFieldError(id, errors[id]);
                if (!first && $(id)) first = $(id);
            });
            if (first) {
                // A hidden SSML row cannot take focus; the rows only hide while SSML is off
                first.focus();
            }
            return first;
        }

        NUMBER_IDS.forEach(function (id) {
            var input = $(id);
            if (input) input.addEventListener('input', function () { setFieldError(id, ''); });
        });
        var styleSelect = $('defaultStyle');
        if (styleSelect) styleSelect.addEventListener('change', function () { setFieldError('defaultStyle', ''); });

        // ------------------------------------------------------------ conditional rows

        $('ssmlEnabled').addEventListener('change', function () {
            var on = this.checked;
            doc.querySelectorAll('.ssml-conditional-field').forEach(function (row) { row.hidden = !on; });
        });

        // ------------------------------------------------------------ role pickers

        function refreshSummary(card) {
            var holder = card.querySelector('[data-role-summary]');
            var checked = card.querySelectorAll('.role-checkbox:checked');
            holder.textContent = '';
            if (checked.length === 0) {
                var placeholder = doc.createElement('span');
                placeholder.className = 'role-select-placeholder';
                placeholder.textContent = 'Everyone can use this command';
                holder.appendChild(placeholder);
            } else {
                Array.prototype.forEach.call(checked, function (box) {
                    var tag = doc.createElement('span');
                    tag.className = 'role-tag';
                    tag.textContent = box.dataset.roleName;
                    holder.appendChild(tag);
                });
            }
            card.querySelectorAll('.role-select-option').forEach(function (label) {
                var box = label.querySelector('.role-checkbox');
                label.classList.toggle('selected', !!(box && box.checked));
            });
        }

        form.addEventListener('change', function (e) {
            if (e.target.classList && e.target.classList.contains('role-checkbox')) {
                refreshSummary(e.target.closest('.command-permission-card'));
            }
        });

        form.addEventListener('click', function (e) {
            var clear = e.target.closest('[data-clear-roles]');
            if (!clear) return;
            var card = clear.closest('.command-permission-card');
            card.querySelectorAll('.role-checkbox:checked').forEach(function (box) {
                box.checked = false;
                // Let the unsaved-changes tracker see it
                box.dispatchEvent(new Event('change', { bubbles: true }));
            });
            refreshSummary(card);
            var summary = card.querySelector('summary');
            if (summary) summary.focus();
        });

        // Escape closes an open picker and returns to its summary
        form.addEventListener('keydown', function (e) {
            if (e.key !== 'Escape') return;
            var open = e.target.closest && e.target.closest('details.role-picker[open]');
            if (!open) return;
            e.preventDefault();
            e.stopPropagation();
            open.open = false;
            open.querySelector('summary').focus();
        });

        // One open picker at a time keeps the page short
        doc.querySelectorAll('details.role-picker').forEach(function (details) {
            details.addEventListener('toggle', function () {
                if (!details.open) return;
                doc.querySelectorAll('details.role-picker[open]').forEach(function (other) {
                    if (other !== details) other.open = false;
                });
            });
        });

        // ------------------------------------------------------------ collect, save, reset

        function collect() {
            var commandRoles = {};
            doc.querySelectorAll('.command-permission-card').forEach(function (card) {
                commandRoles[card.dataset.command] = Array.prototype.map.call(
                    card.querySelectorAll('.role-checkbox:checked'),
                    function (box) { return box.value; });
            });
            return {
                audioEnabled: $('audioEnabled').checked,
                autoLeaveTimeoutMinutes: parseWholeNumber($('autoLeaveTimeout').value),
                queueEnabled: $('queueEnabled').checked,
                enableMemberPortal: $('enableMemberPortal').checked,
                silentPlayback: $('silentPlayback').checked,
                maxDurationSeconds: parseWholeNumber($('maxDuration').value),
                maxFileSizeMB: parseWholeNumber($('maxFileSize').value),
                maxSoundsPerGuild: parseWholeNumber($('maxSounds').value),
                ssmlEnabled: $('ssmlEnabled').checked,
                strictSsmlValidation: $('strictSsmlValidation').checked,
                maxSsmlComplexity: parseWholeNumber($('maxSsmlComplexity').value),
                defaultStyle: $('defaultStyle').value || null,
                commandRoles: commandRoles
            };
        }

        function setBusy(isBusy) {
            busy = isBusy;
            form.setAttribute('aria-busy', isBusy ? 'true' : 'false');
            resetBtn.disabled = isBusy;
        }

        function markClean() {
            if (root.UnsavedChanges && typeof root.UnsavedChanges.markClean === 'function') {
                root.UnsavedChanges.markClean(form);
            }
        }

        function isDirty() {
            return form.dataset.dirty === 'true';
        }

        function showPortalLinks() {
            $('portalLinksRow').hidden = !$('enableMemberPortal').checked;
        }

        // Saved/unsaved wording beside the buttons
        form.addEventListener('unsavedchange', function (e) {
            var note = form.querySelector('[data-saved-note]');
            if (note) note.classList.toggle('hidden', !!(e.detail && e.detail.dirty));
        });

        async function save() {
            if (busy) return;

            var errors = checkNumbers();
            if (Object.keys(errors).length) {
                showErrors(errors);
                root.toast.error('Some settings are not valid. Fix the fields marked on the page and save again.', { key: 'audio-settings-invalid' });
                return;
            }
            clearFieldErrors();

            if (!isDirty()) {
                root.toast.info('There is nothing to save: no settings have changed.', { key: 'audio-settings-clean' });
                return;
            }

            setBusy(true);
            loadingManager().setButtonLoading(saveBtn, true, 'Saving…');
            try {
                var data = await root.ApiClient.post(url('SaveAll'), collect());
                markClean();
                showPortalLinks();
                root.toast.success(data.message || 'Audio settings saved.');
            } catch (err) {
                if (err && err.status === 400 && err.data && err.data.errors) {
                    var first = showErrors(err.data.errors);
                    root.toast.error(err.message);
                    if (!first) return;
                } else {
                    root.ApiClient.showErrorToast(err);
                }
            } finally {
                loadingManager().setButtonLoading(saveBtn, false);
                setBusy(false);
            }
        }

        function applyDefaults(s) {
            $('audioEnabled').checked = !!s.audioEnabled;
            $('autoLeaveTimeout').value = s.autoLeaveTimeoutMinutes;
            $('queueEnabled').checked = !!s.queueEnabled;
            $('enableMemberPortal').checked = !!s.enableMemberPortal;
            $('silentPlayback').checked = !!s.silentPlayback;
            $('maxDuration').value = s.maxDurationSeconds;
            $('maxFileSize').value = s.maxFileSizeMB;
            $('maxSounds').value = s.maxSoundsPerGuild;
            doc.querySelectorAll('.role-checkbox:checked').forEach(function (box) { box.checked = false; });
            doc.querySelectorAll('.command-permission-card').forEach(refreshSummary);
            clearFieldErrors();
            showPortalLinks();
        }

        async function resetToDefaults() {
            if (busy) return;
            var confirmed = await root.quickActions.confirm({
                title: 'Reset audio settings',
                message: 'Put the general settings, limits and command permissions back to their defaults? '
                    + 'TTS settings are not changed.' + (isDirty() ? ' Edits you have not saved will be lost.' : '')
                    + ' This cannot be undone.',
                variant: 'danger',
                confirmText: 'Reset to defaults'
            });
            if (!confirmed) return;

            setBusy(true);
            loadingManager().setButtonLoading(resetBtn, true, 'Resetting…');
            try {
                var data = await root.ApiClient.post(url('ResetToDefaults'));
                applyDefaults(data.settings);
                markClean();
                root.toast.success(data.message);
            } catch (err) {
                root.ApiClient.showErrorToast(err);
            } finally {
                loadingManager().setButtonLoading(resetBtn, false);
                setBusy(false);
            }
        }

        form.addEventListener('submit', function (e) { e.preventDefault(); save(); });
        resetBtn.addEventListener('click', resetToDefaults);
    }

    return api;
});
