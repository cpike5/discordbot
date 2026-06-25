/*
 * soundboard-upload.js — upload widget for the Slice 6 soundboard portal island.
 *
 * The sound grid is a Blazor island (SoundboardIsland), but file uploads stay in JS:
 * streaming multi-MB audio over the Blazor Server circuit is an anti-pattern, and the
 * dropzone / File API / duration probe / XHR progress are inherently client-side. This
 * widget owns the upload sidebar's behavior and bridges the shared sound-list state to
 * the island through window.soundboardIsland (dup-name + count checks, and adding the
 * new card on success). Extracted from the upload half of the legacy portal-soundboard.js.
 */
(function () {
    'use strict';

    let guildId = null;
    let currentUserId = null;
    let maxSizeBytes = 0;
    let maxSounds = 0;
    let currentSoundCount = 0;
    let maxDurationSeconds = 0;

    let selectedFile = null;
    let isUploading = false;
    let uploadMessageSource = null;

    const uploadUrl = () => `/api/portal/soundboard/${guildId}/sounds`;

    function $(id) { return document.getElementById(id); }

    function init() {
        const configEl = $('soundboard-config');
        if (!configEl) return;

        guildId = configEl.dataset.guildId;
        currentUserId = configEl.dataset.currentUserId || null;
        maxSizeBytes = parseInt(configEl.dataset.maxSizeBytes, 10) || 0;
        maxSounds = parseInt(configEl.dataset.maxSounds, 10) || 0;
        currentSoundCount = parseInt(configEl.dataset.currentSoundCount, 10) || 0;
        maxDurationSeconds = parseInt(configEl.dataset.maxDurationSeconds, 10) || 0;

        if (!guildId) return;

        const dropzone = $('dropzone');
        if (dropzone) {
            dropzone.addEventListener('click', () => $('fileInput').click());
            dropzone.addEventListener('dragover', handleDragOver);
            dropzone.addEventListener('dragleave', handleDragLeave);
            dropzone.addEventListener('drop', handleDrop);
        }

        const fileInput = $('fileInput');
        if (fileInput) {
            fileInput.addEventListener('change', handleFileSelect);
        }

        const clearFileBtn = $('clearFileBtn');
        if (clearFileBtn) {
            clearFileBtn.addEventListener('click', clearFileSelection);
        }

        const soundNameInput = $('soundNameInput');
        if (soundNameInput) {
            soundNameInput.addEventListener('input', updateUploadButton);
            soundNameInput.addEventListener('keypress', function (e) {
                if (e.key === 'Enter') {
                    e.preventDefault();
                    uploadFile();
                }
            });
        }

        const uploadBtn = $('uploadBtn');
        if (uploadBtn) {
            uploadBtn.addEventListener('click', uploadFile);
        }
    }

    // ── Current sound count (island is source of truth, config is fallback) ──
    async function getSoundCount() {
        try {
            if (window.soundboardIsland && typeof window.soundboardIsland.getSoundCount === 'function') {
                const count = await window.soundboardIsland.getSoundCount();
                if (typeof count === 'number') return count;
            }
        } catch (e) { /* fall back */ }
        return currentSoundCount;
    }

    async function nameExists(name) {
        try {
            if (window.soundboardIsland && typeof window.soundboardIsland.hasName === 'function') {
                return await window.soundboardIsland.hasName(name) === true;
            }
        } catch (e) { /* assume no clash */ }
        return false;
    }

    // ── File selection / validation ─────────────────────────────────────
    function handleFileSelect(event) {
        const files = event.target.files;
        if (files.length > 0) selectFile(files[0]);
    }

    function handleDragOver(event) {
        event.preventDefault();
        event.stopPropagation();
        $('dropzone').classList.add('drag-over');
    }

    function handleDragLeave(event) {
        event.preventDefault();
        event.stopPropagation();
        $('dropzone').classList.remove('drag-over');
    }

    function handleDrop(event) {
        event.preventDefault();
        event.stopPropagation();
        $('dropzone').classList.remove('drag-over');
        const files = event.dataTransfer.files;
        if (files.length > 0) selectFile(files[0]);
    }

    async function selectFile(file) {
        hideUploadMessage();

        const validTypes = ['audio/mpeg', 'audio/wav', 'audio/ogg', 'audio/mp3', 'audio/x-wav'];
        const validExtensions = /\.(mp3|wav|ogg)$/i;
        if (!validTypes.includes(file.type) && !file.name.match(validExtensions)) {
            showUploadMessage('error', 'Invalid file type. Please upload MP3, WAV, or OGG files.');
            return;
        }

        if (file.size > maxSizeBytes) {
            const maxMB = Math.floor(maxSizeBytes / (1024 * 1024));
            showUploadMessage('error', `File too large. Maximum size is ${maxMB} MB.`);
            return;
        }

        const count = await getSoundCount();
        if (count >= maxSounds) {
            showUploadMessage('error', `Sound limit reached. This guild has ${maxSounds} sounds maximum. Please delete some before uploading new ones.`);
            return;
        }

        const audio = new Audio();
        const objectUrl = URL.createObjectURL(file);
        audio.src = objectUrl;
        audio.onloadedmetadata = function () {
            URL.revokeObjectURL(objectUrl);
            if (!isFinite(audio.duration) || audio.duration > maxDurationSeconds) {
                showUploadMessage('error', !isFinite(audio.duration)
                    ? 'Could not determine audio duration. Please try a different file.'
                    : `Audio too long (${Math.round(audio.duration)}s). Maximum duration is ${maxDurationSeconds} seconds.`);
                return;
            }
            showFileReady(file);
        };
        audio.onerror = function () {
            URL.revokeObjectURL(objectUrl);
            showUploadMessage('error', 'Could not read audio file. The file may be corrupted.');
        };
    }

    function showFileReady(file) {
        selectedFile = file;

        const dropzone = $('dropzone');
        dropzone.classList.add('has-file');
        $('dropzoneText').textContent = 'File selected';
        $('dropzoneHint').textContent = 'Click to change';

        $('uploadForm').classList.remove('hidden');
        $('filePreviewName').textContent = file.name;
        $('filePreviewSize').textContent = formatFileSize(file.size);

        const nameWithoutExt = file.name.replace(/\.[^/.]+$/, '');
        $('soundNameInput').value = nameWithoutExt;
        $('soundNameInput').focus();

        updateUploadButton();
    }

    function clearFileSelection() {
        selectedFile = null;

        const dropzone = $('dropzone');
        dropzone.classList.remove('has-file');
        $('dropzoneText').textContent = 'Drop audio file here';
        $('dropzoneHint').textContent = 'or click to browse';

        $('uploadForm').classList.add('hidden');
        $('soundNameInput').value = '';
        $('fileInput').value = '';

        hideUploadMessage();
    }

    async function updateUploadButton() {
        const btn = $('uploadBtn');
        const name = $('soundNameInput').value.trim();

        if (name && await nameExists(name)) {
            btn.disabled = true;
            showUploadMessage('error', `A sound named "${name}" already exists. Please choose a different name.`, 'duplicate');
            return;
        } else if (uploadMessageSource === 'duplicate') {
            hideUploadMessage();
        }

        btn.disabled = !selectedFile || !name || isUploading;
    }

    function formatFileSize(bytes) {
        if (bytes < 1024) return bytes + ' B';
        if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
        return (bytes / (1024 * 1024)).toFixed(2) + ' MB';
    }

    function showUploadMessage(type, message, source) {
        const container = $('uploadMessage');
        const icon = $('uploadMessageIcon');
        const text = $('uploadMessageText');

        uploadMessageSource = source || null;
        container.classList.remove('hidden', 'success', 'error');
        container.classList.add(type);

        if (type === 'success') {
            icon.innerHTML = '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />';
        } else {
            icon.innerHTML = '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />';
        }

        text.textContent = message;
    }

    function hideUploadMessage() {
        uploadMessageSource = null;
        $('uploadMessage').classList.add('hidden');
    }

    // ── Upload (XHR with progress, bytes off the circuit) ───────────────
    async function uploadFile() {
        if (!selectedFile || isUploading) return;

        const uploadBtn = $('uploadBtn');
        uploadBtn.disabled = true;
        uploadBtn.style.pointerEvents = 'none';

        const name = $('soundNameInput').value.trim();
        if (!name) {
            showUploadMessage('error', 'Please enter a name for the sound.');
            uploadBtn.style.pointerEvents = '';
            updateUploadButton();
            return;
        }

        if (await nameExists(name)) {
            showUploadMessage('error', `A sound named "${name}" already exists. Please choose a different name.`);
            uploadBtn.style.pointerEvents = '';
            updateUploadButton();
            return;
        }

        isUploading = true;
        updateUploadButton();

        const progressContainer = $('uploadProgress');
        const progressBar = $('progressBar');
        const progressText = $('progressText');
        progressContainer.classList.remove('hidden');
        progressBar.style.width = '0%';
        progressText.textContent = 'Uploading...';

        const formData = new FormData();
        formData.append('file', selectedFile);
        formData.append('name', name);

        const xhr = new XMLHttpRequest();
        xhr.open('POST', uploadUrl(), true);

        xhr.upload.onprogress = function (event) {
            if (event.lengthComputable) {
                const percent = Math.round((event.loaded / event.total) * 100);
                progressBar.style.width = percent + '%';
                progressText.textContent = `Uploading... ${percent}%`;
            }
        };

        xhr.onload = function () {
            isUploading = false;
            uploadBtn.style.pointerEvents = '';
            progressContainer.classList.add('hidden');

            if (xhr.status === 201) {
                const data = JSON.parse(xhr.responseText);
                showUploadMessage('success', `Sound "${data.name}" uploaded successfully!`);
                clearFileSelection();

                // Hand the new sound to the island grid (dedupes on its own SignalR echo).
                if (window.soundboardIsland && typeof window.soundboardIsland.addUploadedSound === 'function') {
                    window.soundboardIsland.addUploadedSound({
                        id: data.id,
                        name: data.name,
                        playCount: 0,
                        durationSeconds: data.durationSeconds || 0,
                        uploadedById: data.uploadedById || currentUserId || '',
                        uploadedAt: data.uploadedAt || new Date().toISOString()
                    });
                }

                currentSoundCount++;
            } else {
                let errorMessage = 'Upload failed. Please try again.';
                try {
                    const error = JSON.parse(xhr.responseText);
                    if (error.message) {
                        errorMessage = error.message;
                        if (error.detail) errorMessage += ' ' + error.detail;
                    }
                } catch (e) { /* default message */ }
                showUploadMessage('error', errorMessage);
                updateUploadButton();
            }
        };

        xhr.onerror = function () {
            isUploading = false;
            uploadBtn.style.pointerEvents = '';
            progressContainer.classList.add('hidden');
            showUploadMessage('error', 'Network error. Please check your connection and try again.');
            updateUploadButton();
        };

        xhr.send(formData);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
