/**
 * Soundboard admin page (Pages/Guilds/Soundboard): categories, sound rows, uploads.
 *
 * The page sets window.soundboardAdminConfig = { guildId, maxFileBytes, maxSounds } and writes the
 * guild's categories into <script type="application/json" id="soundboardCategories">.
 *
 * User text (sound and category names) only ever reaches the DOM through textContent, attributes
 * and data-* values, never through markup strings or inline handlers. Every request goes through
 * ApiClient except the upload, which needs XMLHttpRequest for upload progress; that one reports
 * an expired session itself.
 *
 * The pure helpers (validateFile, uploadButtonLabel, formatBytes) are exported for node --test.
 */
(function (root, factory) {
    var api = factory(root);
    if (typeof module === 'object' && module.exports) {
        module.exports = api;
    } else {
        root.SoundboardAdmin = api;
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

    var ALLOWED_EXTENSIONS = ['.mp3', '.wav', '.ogg', '.m4a'];

    // ---------------------------------------------------------------- pure helpers

    function formatBytes(bytes) {
        if (bytes < 1024) return bytes + ' B';
        if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
        return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
    }

    function extensionOf(name) {
        var dot = String(name).lastIndexOf('.');
        return dot < 0 ? '' : String(name).slice(dot).toLowerCase();
    }

    /**
     * Check a file before it is sent: type, emptiness, size, and whether the server has room.
     * @returns {string|null} what is wrong, in plain language, or null when it can go
     */
    function validateFile(file, limits) {
        var maxBytes = limits.maxBytes;
        if (ALLOWED_EXTENSIONS.indexOf(extensionOf(file.name)) < 0) {
            return 'Not a supported type. Use MP3, WAV, OGG or M4A.';
        }
        if (!file.size) {
            return 'The file is empty.';
        }
        if (maxBytes && file.size > maxBytes) {
            return 'Larger than the ' + formatBytes(maxBytes).replace('.0 ', ' ') + ' limit (' + formatBytes(file.size) + ').';
        }
        if (limits.slotsLeft !== undefined && limits.slotsLeft <= 0) {
            return 'The server is at its limit of ' + limits.maxSounds + ' sounds.';
        }
        return null;
    }

    /** "Upload sound" for one file, "Upload 3 sounds" for several: the copy matches what is sent. */
    function uploadButtonLabel(count) {
        if (count <= 0) return 'Upload sounds';
        return count === 1 ? 'Upload sound' : 'Upload ' + count + ' sounds';
    }

    // ---------------------------------------------------------------- page behaviour

    var api = {
        ALLOWED_EXTENSIONS: ALLOWED_EXTENSIONS,
        formatBytes: formatBytes,
        validateFile: validateFile,
        uploadButtonLabel: uploadButtonLabel,
        init: init
    };

    function init() {
        var doc = root.document;
        var cfg = root.soundboardAdminConfig;
        if (!cfg || !doc.getElementById('soundsList')) return;

        var guildId = String(cfg.guildId);
        var pagePath = '/Guilds/Soundboard/' + guildId;
        var handlerUrl = function (handler, query) {
            return pagePath + '?handler=' + handler + (query || '');
        };
        var $ = function (id) { return doc.getElementById(id); };

        function showError(err, fallback) {
            if (root.ApiClient && typeof root.ApiClient.showErrorToast === 'function') {
                root.ApiClient.showErrorToast(err);
            } else {
                root.toast.error((err && err.message) || fallback);
            }
        }

        function setPending(button, pending, text) {
            loadingManager().setButtonLoading(button, pending, text || null);
        }

        // ------------------------------------------------------------ stats and counts

        function updateStats(stats) {
            if (!stats) return;
            var set = function (id, text) { var el = $(id); if (el) el.textContent = text; };
            set('stat-total-sounds', stats.totalSounds);
            set('stat-storage-used', stats.storageUsedFormatted);
            set('stat-storage-limit', 'of ' + stats.storageLimitFormatted + ' limit (' + stats.storagePercentage + '%)');
            var top = $('stat-top-sound');
            if (top) {
                top.textContent = stats.topSoundName || '--';
                top.title = stats.topSoundName || '';
                top.classList.toggle('text-text-tertiary', !stats.topSoundName);
                top.classList.toggle('text-text-primary', !!stats.topSoundName);
            }
            set('stat-top-sound-plays', stats.topSoundPlays > 0
                ? Format.plural(stats.topSoundPlays, 'play', 'plays')
                : 'No plays yet');
            set('sound-count-chip', Format.plural(stats.totalSounds, 'sound', 'sounds'));

            var exportBtn = $('exportAllBtn');
            if (exportBtn && exportBtn.tagName === 'BUTTON') {
                exportBtn.disabled = stats.totalSounds === 0;
            } else if (exportBtn && stats.totalSounds === 0) {
                exportBtn.setAttribute('aria-disabled', 'true');
            }
        }

        function currentSoundCount() {
            var el = $('stat-total-sounds');
            return el ? parseInt(el.textContent, 10) || 0 : 0;
        }

        /** Re-render the sounds list for the sort on screen, keeping filters and the old list on failure. */
        function reloadList() {
            var wrapper = doc.querySelector('.sort-dropdown-wrapper[data-partial-url]');
            if (!root.AjaxSort || !wrapper) { root.location.reload(); return Promise.resolve(false); }
            var detail = root.AjaxSort.detailFromPage();
            return root.AjaxSort.load(Object.assign({}, detail, { sortValue: wrapper.dataset.currentSort }), {}, false);
        }

        // ------------------------------------------------------------ categories

        var categoryData = [];
        try {
            var raw = $('soundboardCategories');
            categoryData = raw ? JSON.parse(raw.textContent) : [];
        } catch (e) { categoryData = []; }

        var editingId = null;
        var categoryBusy = false;
        var categoryList = $('categoryList');
        var categoryError = $('categoryError');

        function setCategoryError(message) {
            if (!categoryError) return;
            categoryError.textContent = message || '';
            categoryError.classList.toggle('hidden', !message);
            var input = $('newCategoryInput');
            if (input) {
                if (message) input.setAttribute('aria-invalid', 'true'); else input.removeAttribute('aria-invalid');
            }
        }

        function iconButton(label, pathD, extraClass) {
            var btn = doc.createElement('button');
            btn.type = 'button';
            btn.className = 'inline-flex items-center justify-center w-8 h-8 rounded transition-colors ' + extraClass;
            btn.setAttribute('aria-label', label);
            btn.title = label;
            var svg = doc.createElementNS('http://www.w3.org/2000/svg', 'svg');
            svg.setAttribute('class', 'w-4 h-4');
            svg.setAttribute('fill', 'none');
            svg.setAttribute('viewBox', '0 0 24 24');
            svg.setAttribute('stroke', 'currentColor');
            svg.setAttribute('stroke-width', '2');
            svg.setAttribute('aria-hidden', 'true');
            var path = doc.createElementNS('http://www.w3.org/2000/svg', 'path');
            path.setAttribute('stroke-linecap', 'round');
            path.setAttribute('stroke-linejoin', 'round');
            path.setAttribute('d', pathD);
            svg.appendChild(path);
            btn.appendChild(svg);
            return btn;
        }

        var ICON_EDIT = 'M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z';
        var ICON_DELETE = 'M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16';
        var ICON_CHECK = 'M5 13l4 4L19 7';
        var ICON_CLOSE = 'M6 18L18 6M6 6l12 12';

        function renderCategories(focusAfter) {
            if (!categoryList) return;
            categoryList.textContent = '';

            var empty = $('categoryEmptyState');
            var badge = $('categoryCountBadge');
            if (empty) empty.classList.toggle('hidden', categoryData.length > 0);
            if (badge) {
                badge.textContent = categoryData.length;
                badge.classList.toggle('hidden', categoryData.length === 0);
            }

            categoryData.forEach(function (cat) {
                var li = doc.createElement('li');
                li.className = 'flex items-center gap-2 px-2 py-1.5 rounded-lg hover:bg-bg-hover transition-colors';
                li.setAttribute('data-row', '');
                li.dataset.categoryId = cat.id;

                if (editingId === cat.id) {
                    var input = doc.createElement('input');
                    input.type = 'text';
                    input.maxLength = 50;
                    input.value = cat.name;
                    input.autocomplete = 'off';
                    input.className = 'flex-1 min-w-0 bg-bg-tertiary border border-accent-orange rounded px-2 py-1 text-sm text-text-primary focus:outline-none';
                    input.setAttribute('aria-label', 'New name for category ' + cat.name);
                    input.dataset.categoryEdit = cat.id;
                    li.appendChild(input);

                    var save = iconButton('Save the new name for ' + cat.name, ICON_CHECK, 'text-success hover:bg-success/10');
                    save.dataset.categorySave = cat.id;
                    var cancel = iconButton('Cancel renaming ' + cat.name, ICON_CLOSE, 'text-text-secondary hover:text-text-primary hover:bg-bg-hover');
                    cancel.dataset.categoryCancel = cat.id;
                    li.appendChild(save);
                    li.appendChild(cancel);
                } else {
                    var name = doc.createElement('span');
                    name.className = 'text-sm text-text-primary flex-1 min-w-0 break-words';
                    name.textContent = cat.name;
                    li.appendChild(name);

                    var actions = doc.createElement('span');
                    actions.className = 'flex items-center gap-1 row-actions';
                    var edit = iconButton('Rename category ' + cat.name, ICON_EDIT, 'text-text-secondary hover:text-accent-blue hover:bg-bg-hover');
                    edit.dataset.categoryRename = cat.id;
                    var del = iconButton('Delete category ' + cat.name, ICON_DELETE, 'text-text-secondary hover:text-error hover:bg-error/10');
                    del.dataset.categoryDelete = cat.id;
                    actions.appendChild(edit);
                    actions.appendChild(del);
                    li.appendChild(actions);
                }
                categoryList.appendChild(li);
            });

            if (editingId !== null) {
                var field = categoryList.querySelector('[data-category-edit]');
                if (field) { field.focus(); field.select(); }
            } else if (focusAfter) {
                var target = categoryList.querySelector(focusAfter);
                if (target) target.focus();
                else {
                    var addInput = $('newCategoryInput');
                    if (addInput) addInput.focus();
                }
            }
        }

        /** Rebuild every sound's category select from the current list, keeping each choice. */
        function syncCategorySelects() {
            doc.querySelectorAll('.category-assign-select').forEach(function (sel) {
                var current = sel.getAttribute('data-current-category') || '';
                while (sel.options.length > 1) sel.remove(1);
                categoryData.forEach(function (cat) {
                    var opt = doc.createElement('option');
                    opt.value = String(cat.id);
                    opt.textContent = cat.name;
                    sel.appendChild(opt);
                });
                sel.value = current;
                if (sel.value !== current) {
                    sel.value = '';
                    sel.setAttribute('data-current-category', '');
                }
            });
        }

        async function createCategory() {
            var input = $('newCategoryInput');
            var btn = $('addCategoryBtn');
            var name = input.value.trim();
            if (!name) {
                setCategoryError('Enter a category name.');
                input.focus();
                return;
            }
            if (categoryBusy) return;
            categoryBusy = true;
            setCategoryError('');
            setPending(btn, true, 'Adding…');
            try {
                var data = await ApiClient.post(handlerUrl('CreateCategory'), { name: name });
                categoryData.push(data.category);
                input.value = '';
                renderCategories();
                syncCategorySelects();
                toast.success(data.message);
                input.focus();
            } catch (err) {
                if (err && err.status === 400) {
                    setCategoryError(err.message);
                    input.focus();
                } else {
                    showError(err, 'Could not create the category.');
                }
            } finally {
                categoryBusy = false;
                setPending(btn, false);
            }
        }

        async function saveCategoryRename(id) {
            var input = categoryList.querySelector('[data-category-edit="' + id + '"]');
            var name = input ? input.value.trim() : '';
            if (!name) {
                setCategoryError('Enter a category name.');
                if (input) input.focus();
                return;
            }
            if (categoryBusy) return;
            categoryBusy = true;
            setCategoryError('');
            var saveBtn = categoryList.querySelector('[data-category-save="' + id + '"]');
            if (saveBtn) saveBtn.disabled = true;
            if (input) input.disabled = true;
            try {
                var data = await ApiClient.post(handlerUrl('RenameCategory'), { id: id, name: name });
                var idx = categoryData.findIndex(function (c) { return c.id === id; });
                if (idx >= 0) categoryData[idx] = data.category;
                editingId = null;
                renderCategories('[data-category-rename="' + id + '"]');
                syncCategorySelects();
                toast.success(data.message);
            } catch (err) {
                if (input) { input.disabled = false; input.focus(); }
                if (saveBtn) saveBtn.disabled = false;
                if (err && err.status === 400) setCategoryError(err.message);
                else showError(err, 'Could not rename the category.');
            } finally {
                categoryBusy = false;
            }
        }

        async function deleteCategory(id, button) {
            var cat = categoryData.find(function (c) { return c.id === id; });
            if (!cat || categoryBusy) return;
            var confirmed = await quickActions.confirm({
                title: 'Delete category',
                message: 'Delete the category "' + cat.name + '"? Sounds in it become uncategorized. This cannot be undone.',
                variant: 'danger',
                confirmText: 'Delete category'
            });
            if (!confirmed) return;

            categoryBusy = true;
            button.disabled = true;
            try {
                var data = await ApiClient.post(handlerUrl('DeleteCategory'), { id: id });
                categoryData = categoryData.filter(function (c) { return c.id !== id; });
                doc.querySelectorAll('.category-assign-select').forEach(function (sel) {
                    if (sel.getAttribute('data-current-category') === String(id)) {
                        sel.setAttribute('data-current-category', '');
                    }
                });
                renderCategories('#newCategoryInput');
                syncCategorySelects();
                toast.success(data.message);
            } catch (err) {
                button.disabled = false;
                showError(err, 'Could not delete the category.');
            } finally {
                categoryBusy = false;
            }
        }

        var toggleBtn = $('categoryToggleBtn');
        if (toggleBtn) {
            toggleBtn.addEventListener('click', function () {
                var content = $('categoryContent');
                var hidden = content.classList.toggle('hidden');
                toggleBtn.setAttribute('aria-expanded', hidden ? 'false' : 'true');
                var chevron = $('categoryChevron');
                if (chevron) chevron.style.transform = hidden ? '' : 'rotate(180deg)';
            });
        }

        var addForm = $('addCategoryForm');
        if (addForm) {
            addForm.addEventListener('submit', function (e) { e.preventDefault(); createCategory(); });
            $('newCategoryInput').addEventListener('input', function () { setCategoryError(''); });
        }

        if (categoryList) {
            categoryList.addEventListener('click', function (e) {
                var btn = e.target.closest('button');
                if (!btn) return;
                if (btn.dataset.categoryRename) {
                    editingId = Number(btn.dataset.categoryRename);
                    setCategoryError('');
                    renderCategories();
                } else if (btn.dataset.categoryCancel) {
                    var cancelledId = btn.dataset.categoryCancel;
                    editingId = null;
                    setCategoryError('');
                    renderCategories('[data-category-rename="' + cancelledId + '"]');
                } else if (btn.dataset.categorySave) {
                    saveCategoryRename(Number(btn.dataset.categorySave));
                } else if (btn.dataset.categoryDelete) {
                    deleteCategory(Number(btn.dataset.categoryDelete), btn);
                }
            });
            categoryList.addEventListener('keydown', function (e) {
                var field = e.target.closest('[data-category-edit]');
                if (!field) return;
                if (e.key === 'Enter' && !e.isComposing) {
                    e.preventDefault();
                    saveCategoryRename(Number(field.dataset.categoryEdit));
                } else if (e.key === 'Escape') {
                    e.preventDefault();
                    e.stopPropagation();
                    var id = field.dataset.categoryEdit;
                    editingId = null;
                    renderCategories('[data-category-rename="' + id + '"]');
                }
            });
        }

        renderCategories();

        // ------------------------------------------------------------ assign a category

        doc.addEventListener('change', async function (e) {
            var sel = e.target.closest && e.target.closest('.category-assign-select');
            if (!sel) return;
            var previous = sel.getAttribute('data-current-category') || '';
            sel.disabled = true;
            sel.setAttribute('aria-busy', 'true');
            try {
                var data = await ApiClient.post(handlerUrl('AssignCategory'), {
                    soundId: sel.dataset.soundId,
                    categoryId: sel.value ? parseInt(sel.value, 10) : null
                });
                sel.setAttribute('data-current-category', sel.value);
                toast.success(data.message);
            } catch (err) {
                sel.value = previous;
                showError(err, 'Could not change the category.');
            } finally {
                sel.disabled = false;
                sel.removeAttribute('aria-busy');
            }
        });

        // ------------------------------------------------------------ preview in this browser

        var audio = new Audio();
        var playingId = null;

        function playButton(id) {
            return doc.querySelector('[data-sound-play="' + id + '"]');
        }

        function setPlayState(id, state) {
            var btn = playButton(id);
            if (!btn) return;
            var name = btn.dataset.soundName || 'this sound';
            btn.classList.toggle('playing', state === 'playing');
            btn.querySelector('.play-icon').classList.toggle('hidden', state !== 'idle');
            btn.querySelector('.stop-icon').classList.toggle('hidden', state !== 'playing');
            btn.querySelector('.loading-icon').classList.toggle('hidden', state !== 'loading');
            var label = state === 'idle' ? 'Preview ' + name + ' in this browser' : 'Stop previewing ' + name;
            btn.setAttribute('aria-label', label);
            btn.title = label;
            btn.setAttribute('aria-busy', state === 'loading' ? 'true' : 'false');
        }

        function stopPreview() {
            if (!playingId) return;
            var id = playingId;
            playingId = null;
            audio.pause();
            audio.removeAttribute('src');
            audio.load();
            setPlayState(id, 'idle');
        }

        audio.addEventListener('playing', function () { if (playingId) setPlayState(playingId, 'playing'); });
        audio.addEventListener('ended', function () { stopPreview(); });
        audio.addEventListener('error', function () {
            if (!playingId) return;
            var btn = playButton(playingId);
            var name = btn ? btn.dataset.soundName : 'that sound';
            stopPreview();
            toast.error('Could not play "' + name + '" in this browser. The file may be missing or damaged.');
        });

        function startPreview(id) {
            stopPreview();
            playingId = id;
            setPlayState(id, 'loading');
            audio.src = '/api/guilds/' + guildId + '/sounds/' + id + '/download';
            var promise = audio.play();
            if (promise && promise.catch) {
                promise.catch(function (err) {
                    // The error event reports a bad file; this catches a blocked or interrupted play
                    if (playingId === id && err && err.name === 'NotAllowedError') {
                        stopPreview();
                        toast.warning('Your browser blocked the preview. Press play again.');
                    }
                });
            }
        }

        // ------------------------------------------------------------ sound row actions

        async function deleteSound(button) {
            var id = button.dataset.soundDelete;
            var name = button.dataset.soundName;
            var confirmed = await quickActions.confirm({
                title: 'Delete sound',
                message: 'Delete "' + name + '"? The file is removed from the server. This cannot be undone.',
                variant: 'danger',
                confirmText: 'Delete sound'
            });
            if (!confirmed) return;

            var row = button.closest('tr');
            button.disabled = true;
            button.setAttribute('aria-busy', 'true');
            try {
                var data = await ApiClient.post(handlerUrl('Delete', '&soundId=' + encodeURIComponent(id)));
                if (playingId === id) stopPreview();
                var tbody = row && row.parentNode;
                if (row) row.remove();
                updateStats(data.stats);
                if (!tbody || tbody.children.length === 0) {
                    await reloadList();
                } else {
                    // Keep the keyboard where it was
                    var next = tbody.querySelector('[data-sound-play]');
                    if (next) next.focus();
                }
                toast.success(data.message);
            } catch (err) {
                button.disabled = false;
                button.removeAttribute('aria-busy');
                showError(err, 'Could not delete the sound.');
            }
        }

        doc.getElementById('soundsList').addEventListener('click', function (e) {
            var play = e.target.closest('[data-sound-play]');
            if (play) {
                var id = play.dataset.soundPlay;
                if (playingId === id) stopPreview(); else startPreview(id);
                return;
            }
            var del = e.target.closest('[data-sound-delete]');
            if (del) deleteSound(del);
        });

        // A re-sort replaces the rows: put the preview button back in its state
        doc.addEventListener('ajaxsort:loaded', function () {
            if (playingId) setPlayState(playingId, audio.paused ? 'loading' : 'playing');
        });

        doc.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && playingId && !doc.querySelector('.qa-open')) stopPreview();
        });

        // Download: the browser handles the file, so say that something is happening
        var exportBtn = $('exportAllBtn');
        if (exportBtn && exportBtn.tagName === 'A') {
            exportBtn.addEventListener('click', function (e) {
                if (exportBtn.getAttribute('aria-disabled') === 'true') { e.preventDefault(); return; }
                if (exportBtn.dataset.busy) { e.preventDefault(); return; }
                exportBtn.dataset.busy = 'true';
                exportBtn.setAttribute('aria-busy', 'true');
                toast.info('Preparing the zip. The download starts when it is ready.', { key: 'sound-export' });
                setTimeout(function () {
                    delete exportBtn.dataset.busy;
                    exportBtn.removeAttribute('aria-busy');
                }, 5000);
            });
        }

        // ------------------------------------------------------------ upload

        var fileInput = $('file-input');
        var dropzone = $('dropzone');
        var queueEl = $('uploadQueue');
        var actionsEl = $('uploadActions');
        var uploadBtn = $('upload-btn');
        var clearBtn = $('uploadClearBtn');
        var statusEl = $('uploadStatus');
        var uploadForm = $('uploadForm');
        var queue = [];
        var uploading = false;
        var nextItemId = 1;

        function slotsLeft() {
            var pending = queue.filter(function (q) { return q.state === 'ready' || q.state === 'failed' || q.state === 'uploading'; }).length;
            return cfg.maxSounds - currentSoundCount() - pending;
        }

        function addFiles(files) {
            // Finished rows make way for the new batch
            queue = queue.filter(function (q) { return q.state !== 'done'; });
            Array.prototype.forEach.call(files, function (file) {
                var duplicate = queue.some(function (q) { return q.file.name === file.name && q.file.size === file.size; });
                if (duplicate) return;
                var problem = validateFile(file, { maxBytes: cfg.maxFileBytes, slotsLeft: slotsLeft(), maxSounds: cfg.maxSounds });
                queue.push({
                    id: nextItemId++,
                    file: file,
                    state: problem ? 'invalid' : 'ready',
                    message: problem,
                    progress: 0
                });
            });
            renderQueue();
            var rejected = queue.filter(function (q) { return q.state === 'invalid'; }).length;
            if (rejected) announce(Format.plural(rejected, 'file', 'files') + ' cannot be uploaded. See the list for why.');
        }

        function announce(text) {
            if (statusEl) statusEl.textContent = text;
        }

        function sendable() {
            return queue.filter(function (q) { return q.state === 'ready' || q.state === 'failed'; });
        }

        function renderQueue() {
            queueEl.textContent = '';
            queue.forEach(function (item) {
                var li = doc.createElement('li');
                li.className = 'p-3 bg-bg-tertiary rounded-lg';
                li.dataset.uploadItem = item.id;

                var top = doc.createElement('div');
                top.className = 'flex items-start justify-between gap-2';
                var info = doc.createElement('div');
                info.className = 'min-w-0';
                var name = doc.createElement('p');
                name.className = 'text-sm text-text-primary break-all';
                name.textContent = item.file.name;
                var size = doc.createElement('p');
                size.className = 'text-xs text-text-tertiary';
                size.textContent = formatBytes(item.file.size);
                info.appendChild(name);
                info.appendChild(size);
                top.appendChild(info);

                if (!uploading && item.state !== 'uploading') {
                    var remove = iconButton('Remove ' + item.file.name + ' from the list', ICON_CLOSE, 'text-text-tertiary hover:text-error shrink-0');
                    remove.dataset.uploadRemove = item.id;
                    top.appendChild(remove);
                }
                li.appendChild(top);

                var statusLine = doc.createElement('p');
                statusLine.className = 'mt-1 text-xs';
                if (item.state === 'invalid' || item.state === 'failed') {
                    statusLine.className += ' text-error';
                    statusLine.textContent = item.message;
                } else if (item.state === 'done') {
                    statusLine.className += ' text-success';
                    statusLine.textContent = item.message || 'Uploaded.';
                } else if (item.state === 'uploading') {
                    statusLine.className += ' text-text-secondary';
                    statusLine.textContent = item.progress >= 100 ? 'Processing the file…' : 'Uploading… ' + item.progress + '%';
                } else {
                    statusLine.className += ' text-text-secondary';
                    statusLine.textContent = 'Ready to upload.';
                }
                li.appendChild(statusLine);

                if (item.state === 'uploading') {
                    var bar = doc.createElement('progress');
                    bar.className = 'upload-progress mt-2 w-full';
                    bar.max = 100;
                    bar.value = item.progress;
                    bar.setAttribute('aria-label', 'Upload progress for ' + item.file.name);
                    li.appendChild(bar);
                }
                queueEl.appendChild(li);
            });

            var count = sendable().length;
            queueEl.classList.toggle('hidden', queue.length === 0);
            actionsEl.classList.toggle('hidden', queue.length === 0);
            uploadBtn.querySelector('[data-label]').textContent = uploadButtonLabel(count);
            uploadBtn.disabled = uploading || count === 0;
            clearBtn.disabled = uploading;
            dropzone.disabled = uploading;
            uploadForm.setAttribute('aria-busy', uploading ? 'true' : 'false');
        }

        function uploadOne(item) {
            return new Promise(function (resolve) {
                var xhr = new XMLHttpRequest();
                xhr.open('POST', handlerUrl('Upload'));
                xhr.setRequestHeader('X-Requested-With', 'XMLHttpRequest');
                xhr.setRequestHeader('Accept', 'application/json');
                var tokenInput = uploadForm.querySelector('input[name="__RequestVerificationToken"]');
                if (tokenInput) xhr.setRequestHeader('RequestVerificationToken', tokenInput.value);
                xhr.timeout = 5 * 60 * 1000;

                xhr.upload.onprogress = function (e) {
                    if (!e.lengthComputable) return;
                    item.progress = Math.min(100, Math.round(e.loaded / e.total * 100));
                    renderQueue();
                };
                xhr.onload = function () {
                    var body = null;
                    try { body = JSON.parse(xhr.responseText); } catch (err) { body = null; }
                    if (xhr.status >= 200 && xhr.status < 300 && body && body.success) {
                        resolve({ ok: true, body: body });
                    } else if (xhr.status === 401 || xhr.status === 403 || xhr.responseURL.indexOf('/Account/Login') >= 0) {
                        resolve({ ok: false, expired: true, message: 'Your session has expired. Sign in again to continue.' });
                    } else if (xhr.status === 413) {
                        resolve({ ok: false, message: 'The server refused a file that large.' });
                    } else if (body && body.message && xhr.status < 500) {
                        resolve({ ok: false, message: body.message });
                    } else {
                        resolve({ ok: false, message: 'Something went wrong on the server. Try again.' });
                    }
                };
                xhr.onerror = function () { resolve({ ok: false, message: 'Could not reach the server. Check your connection and try again.' }); };
                xhr.ontimeout = function () { resolve({ ok: false, message: 'The server took too long to respond. Try again.' }); };

                var form = new FormData();
                if (tokenInput) form.append('__RequestVerificationToken', tokenInput.value);
                form.append('file', item.file, item.file.name);
                xhr.send(form);
            });
        }

        async function runUploads() {
            var items = sendable();
            if (!items.length || uploading) return;
            uploading = true;
            var succeeded = 0;
            var failed = 0;
            var expired = false;

            for (var i = 0; i < items.length; i++) {
                var item = items[i];
                item.state = 'uploading';
                item.progress = 0;
                announce('Uploading ' + (i + 1) + ' of ' + items.length + ': ' + item.file.name);
                renderQueue();

                var result = await uploadOne(item);
                if (result.ok) {
                    item.state = 'done';
                    item.message = result.body.message;
                    succeeded++;
                    updateStats(result.body.stats);
                } else {
                    item.state = 'failed';
                    item.message = result.message;
                    failed++;
                    if (result.expired) {
                        expired = true;
                        // The rest would fail the same way
                        for (var j = i + 1; j < items.length; j++) { items[j].state = 'ready'; }
                        break;
                    }
                }
                renderQueue();
            }

            uploading = false;
            renderQueue();

            if (succeeded) await reloadList();

            if (expired) {
                toast.error('Your session has expired. Sign in again to continue.', {
                    key: 'upload-session',
                    action: { label: 'Sign in', onClick: function () {
                        root.location.href = '/Account/Login?ReturnUrl=' + encodeURIComponent(root.location.pathname + root.location.search);
                    } }
                });
            } else if (failed && succeeded) {
                toast.error(Format.plural(succeeded, 'sound', 'sounds') + ' uploaded, ' + failed + ' did not. The list says why.');
            } else if (failed) {
                toast.error(items.length === 1 ? items[0].message : failed + ' sounds did not upload. The list says why.');
            } else {
                toast.success(succeeded === 1 ? items[0].message : 'Uploaded ' + Format.plural(succeeded, 'sound', 'sounds') + '.');
            }
            announce(succeeded + ' uploaded' + (failed ? ', ' + failed + ' failed' : '') + '.');
            if (failed && !expired && uploadBtn && !uploadBtn.disabled) uploadBtn.focus();
        }

        if (fileInput && dropzone) {
            dropzone.addEventListener('click', function () { fileInput.click(); });
            fileInput.addEventListener('change', function () {
                if (fileInput.files && fileInput.files.length) addFiles(fileInput.files);
                fileInput.value = '';
            });

            ['dragenter', 'dragover', 'dragleave', 'drop'].forEach(function (name) {
                dropzone.addEventListener(name, function (e) { e.preventDefault(); e.stopPropagation(); });
            });
            ['dragenter', 'dragover'].forEach(function (name) {
                dropzone.addEventListener(name, function () { if (!uploading) dropzone.classList.add('drag-over'); });
            });
            ['dragleave', 'drop'].forEach(function (name) {
                dropzone.addEventListener(name, function () { dropzone.classList.remove('drag-over'); });
            });
            dropzone.addEventListener('drop', function (e) {
                if (uploading) return;
                var files = e.dataTransfer && e.dataTransfer.files;
                if (files && files.length) addFiles(files);
            });

            queueEl.addEventListener('click', function (e) {
                var remove = e.target.closest('[data-upload-remove]');
                if (!remove || uploading) return;
                var id = Number(remove.dataset.uploadRemove);
                queue = queue.filter(function (q) { return q.id !== id; });
                renderQueue();
                dropzone.focus();
            });
            clearBtn.addEventListener('click', function () {
                if (uploading) return;
                queue = [];
                renderQueue();
                announce('Upload list cleared.');
                dropzone.focus();
            });
            uploadForm.addEventListener('submit', function (e) {
                e.preventDefault();
                runUploads();
            });
        }

        // A queue on screen is work not yet sent
        root.addEventListener('beforeunload', function (e) {
            if (uploading) { e.preventDefault(); e.returnValue = ''; }
        });

        // Voice panel updates (now playing, Stop, queue) arrive over the hub
        if (root.DashboardHub && typeof root.DashboardHub.connect === 'function') {
            Promise.resolve(root.DashboardHub.connect()).catch(function () { /* the global connection banner reports it */ });
        }
    }

    return api;
});
