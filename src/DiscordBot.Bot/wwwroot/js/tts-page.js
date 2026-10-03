/**
 * TTS admin page (Pages/Guilds/TextToSpeech): send, preview, server defaults, history.
 *
 * Send and Preview use the voice settings on screen (voice, speed, pitch, volume, style) and,
 * in Pro mode, the SSML built from the message; the server falls back to the saved defaults for
 * anything left out. "Save as server defaults" is the only thing that changes what /tts uses.
 *
 * Every request goes through ApiClient. History rows are built with DOM calls, so message text
 * never reaches markup or an inline handler. The pure helpers are exported for node --test.
 */
(function (root, factory) {
    var api = factory(root);
    if (typeof module === 'object' && module.exports) {
        module.exports = api;
    } else {
        root.ttsPage = api;
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

    /**
     * The body for a Send or Preview request, from what is on screen.
     * @param {object} s - { message, voice, speed, pitch, volume, mode, style, styleIntensity, ssml }
     */
    function buildRequestBody(s) {
        var body = {
            message: (s.message || '').trim(),
            voice: s.voice || null,
            speed: finiteOrNull(s.speed),
            pitch: finiteOrNull(s.pitch),
            volume: finiteOrNull(s.volume)
        };
        // The style selector is hidden in Simple mode, so a style picked earlier does not apply
        if (s.mode !== 'simple' && s.style) {
            body.style = s.style;
            body.styleIntensity = finiteOrNull(s.styleIntensity);
        }
        if (s.mode === 'pro' && s.ssml) {
            body.ssml = s.ssml;
        }
        return body;
    }

    function finiteOrNull(value) {
        var n = typeof value === 'number' ? value : parseFloat(value);
        return Number.isFinite(n) ? n : null;
    }

    /** A whole number in range, or null: never a silent 0 for an empty or garbled field. */
    function parseWholeNumber(raw, min, max) {
        var text = String(raw === undefined || raw === null ? '' : raw).trim();
        if (!/^-?\d+$/.test(text)) return null;
        var n = parseInt(text, 10);
        return n >= min && n <= max ? n : null;
    }

    function sliderText(type, value) {
        return type === 'volume' ? Math.round(value * 100) + '%' : value.toFixed(1) + 'x';
    }

    var api = {
        buildRequestBody: buildRequestBody,
        parseWholeNumber: parseWholeNumber,
        sliderText: sliderText,
        init: init
    };

    // ---------------------------------------------------------------- page behaviour

    function init() {
        var doc = root.document;
        var cfg = root.ttsPageConfig;
        if (!cfg || !doc.getElementById('ttsForm')) return;

        var guildId = String(cfg.guildId);
        var pageUrl = function (handler, query) {
            return '/Guilds/TextToSpeech/' + guildId + '?handler=' + handler + (query || '');
        };
        var $ = function (id) { return doc.getElementById(id); };

        var currentMode = 'standard';
        var currentSsml = '';
        var ssmlTimer = null;
        var ssmlSequence = 0;
        var sending = false;
        var previewing = false;
        var previewAudio = null;

        var textarea = $('messageInput');
        var sendBtn = $('sendBtn');
        var previewBtn = $('previewBtn');

        function pending(button, isPending, text) {
            loadingManager().setButtonLoading(button, isPending, text || null);
        }

        function fieldError(id, message, control) {
            var el = $(id);
            if (!el) return;
            el.textContent = message || '';
            el.classList.toggle('hidden', !message);
            if (control) {
                if (message) control.setAttribute('aria-invalid', 'true'); else control.removeAttribute('aria-invalid');
            }
        }

        // ------------------------------------------------------------ stats

        function updateStats(stats) {
            if (!stats) return;
            var messagesToday = $('statsMessagesToday');
            if (messagesToday && stats.messagesToday !== undefined) messagesToday.textContent = stats.messagesToday;
            var totalPlayback = $('statsTotalPlayback');
            if (totalPlayback && stats.totalPlaybackFormatted) totalPlayback.textContent = stats.totalPlaybackFormatted;
            var activeVoices = $('statsActiveVoices');
            if (activeVoices && stats.uniqueUsers !== undefined) activeVoices.textContent = stats.uniqueUsers;
        }

        // ------------------------------------------------------------ history

        function historyList() { return doc.querySelector('[data-recent-messages]'); }

        function setCount(delta) {
            var badge = $('recentMessagesCount');
            if (badge) badge.textContent = Math.max(0, (parseInt(badge.textContent, 10) || 0) + delta);
        }

        function showEmptyHistory() {
            var holder = $('recentMessagesList');
            if (holder && root.EmptyState) {
                var wrap = doc.createElement('div');
                wrap.className = 'p-8';
                holder.textContent = '';
                holder.appendChild(wrap);
                root.EmptyState.render(wrap, {
                    type: 'firstTime',
                    title: 'No messages yet',
                    description: 'Send your first TTS message using the form above.',
                    iconPath: 'M20 2H4a2 2 0 0 0-2 2v18l4-4h14a2 2 0 0 0 2-2V4a2 2 0 0 0-2-2z'
                });
            }
        }

        function addRecentMessage(m) {
            if (!m) return;
            var holder = $('recentMessagesList');
            var list = historyList();
            if (!list) {
                holder.textContent = '';
                list = doc.createElement('ul');
                list.className = 'divide-y divide-border-secondary';
                list.setAttribute('data-recent-messages', '');
                holder.appendChild(list);
            }

            var li = doc.createElement('li');
            li.className = 'flex items-center gap-3 p-4 hover:bg-bg-hover transition-colors';
            li.setAttribute('data-row', '');
            li.dataset.messageId = m.id;

            var avatar = doc.createElement('div');
            avatar.className = 'w-9 h-9 rounded-full bg-accent-blue flex items-center justify-center text-white text-xs font-bold flex-shrink-0';
            avatar.setAttribute('aria-hidden', 'true');
            avatar.textContent = String(m.username || '').substring(0, 2).toUpperCase();
            li.appendChild(avatar);

            var body = doc.createElement('div');
            body.className = 'flex-1 min-w-0';
            var text = doc.createElement('div');
            text.className = 'text-sm text-text-primary break-words line-clamp-2';
            text.setAttribute('dir', 'auto');
            text.textContent = m.message;
            body.appendChild(text);

            var meta = doc.createElement('div');
            meta.className = 'flex flex-wrap items-center gap-2 mt-1 text-xs text-text-tertiary';
            var who = doc.createElement('span');
            who.className = 'preview-trigger';
            who.dataset.previewType = 'user';
            who.dataset.userId = m.userId;
            who.dataset.contextGuildId = guildId;
            who.textContent = m.username;
            var voice = doc.createElement('span');
            voice.className = 'inline-flex items-center gap-1 px-2 py-0.5 bg-accent-blue-muted text-accent-blue rounded font-medium text-[0.65rem]';
            voice.textContent = m.voice;
            var duration = doc.createElement('span');
            duration.textContent = m.durationFormatted;
            meta.appendChild(who);
            meta.appendChild(voice);
            meta.appendChild(duration);
            body.appendChild(meta);
            li.appendChild(body);

            var actions = doc.createElement('div');
            actions.className = 'flex items-center gap-1 row-actions';
            var del = doc.createElement('button');
            del.type = 'button';
            del.className = 'p-2 rounded text-error hover:bg-error/10 transition-colors';
            del.dataset.messageDelete = m.id;
            del.dataset.messageText = m.message;
            del.setAttribute('aria-label', 'Delete the message from ' + m.username);
            del.title = 'Delete this message from the history';
            del.innerHTML = '<svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2" aria-hidden="true"><path stroke-linecap="round" stroke-linejoin="round" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16"/></svg>';
            actions.appendChild(del);
            li.appendChild(actions);

            list.insertBefore(li, list.firstChild);
            setCount(1);
        }

        async function deleteMessage(button) {
            var id = button.dataset.messageDelete;
            var preview = button.dataset.messageText || '';
            if (preview.length > 80) preview = preview.slice(0, 80) + '…';
            var confirmed = await root.quickActions.confirm({
                title: 'Delete message',
                message: 'Remove "' + preview + '" from the history? This cannot be undone.',
                variant: 'danger',
                confirmText: 'Delete message'
            });
            if (!confirmed) return;

            var row = button.closest('li');
            button.disabled = true;
            button.setAttribute('aria-busy', 'true');
            try {
                var data = await root.ApiClient.post(pageUrl('DeleteMessage', '&messageId=' + encodeURIComponent(id)));
                var list = row && row.parentNode;
                if (row) row.remove();
                setCount(-1);
                updateStats(data.stats);
                if (list && list.children.length === 0) showEmptyHistory();
                root.toast.success(data.message);
            } catch (err) {
                button.disabled = false;
                button.removeAttribute('aria-busy');
                root.ApiClient.showErrorToast(err);
            }
        }

        // ------------------------------------------------------------ the request

        function readScreen() {
            return {
                message: textarea.value,
                voice: $('voiceSelect').value,
                speed: $('speedSlider').value,
                pitch: $('pitchSlider').value,
                volume: $('volumeSlider').value,
                mode: currentMode,
                style: ($('hiddenStyle') || {}).value,
                styleIntensity: ($('hiddenStyleIntensity') || {}).value,
                ssml: currentSsml
            };
        }

        /**
         * Build the SSML for the message on screen (Pro mode). Resolves to '' when there is
         * nothing to build; rejects when the server refuses the markup.
         */
        async function buildSsml() {
            var message = textarea.value.trim();
            var voice = $('voiceSelect').value;
            if (!message || !voice || !root.SsmlMarkers) return '';

            var speed = parseFloat($('speedSlider').value);
            var pitch = parseFloat($('pitchSlider').value);
            var style = ($('hiddenStyle') || {}).value || null;
            var payload = {
                language: 'en-US',
                segments: [{
                    voice: voice,
                    style: style,
                    rate: speed !== 1 ? speed : null,
                    pitch: pitch !== 1 ? pitch : null,
                    text: null,
                    elements: root.SsmlMarkers.parseMarkers(message)
                }]
            };
            var data = await root.ApiClient.post('/api/portal/tts/build-ssml', payload);
            return data.ssml || '';
        }

        function showSsml(ssml) {
            currentSsml = ssml;
            if (root.ssmlPreview_update) root.ssmlPreview_update('ssmlPreview', ssml, textarea.value.length);
        }

        function scheduleSsmlBuild() {
            if (currentMode !== 'pro') return;
            clearTimeout(ssmlTimer);
            ssmlTimer = setTimeout(async function () {
                var mine = ++ssmlSequence;
                try {
                    var ssml = await buildSsml();
                    if (mine === ssmlSequence) showSsml(ssml);
                } catch (e) {
                    // The preview just stays as it was; Send reports a build failure properly
                }
            }, 250);
        }

        /** The request body, with fresh SSML in Pro mode. Throws if the markup cannot be built. */
        async function currentBody() {
            if (currentMode === 'pro') {
                clearTimeout(ssmlTimer);
                ssmlSequence++;
                showSsml(await buildSsml());
            }
            return buildRequestBody(readScreen());
        }

        function checkMessage() {
            var message = textarea.value.trim();
            if (!message) {
                fieldError('messageError', 'Type a message first.', textarea);
                textarea.focus();
                return false;
            }
            fieldError('messageError', '', textarea);
            return true;
        }

        function applyFieldError(err) {
            if (err && err.data && err.data.field === 'message') {
                fieldError('messageError', err.message, textarea);
                textarea.focus();
                return true;
            }
            return false;
        }

        async function sendMessage() {
            if (sending || previewing) return;
            if (!checkMessage()) return;

            sending = true;
            pending(sendBtn, true, 'Sending…');
            previewBtn.disabled = true;
            try {
                var body = await currentBody();
                // No timeout: the request lasts as long as the bot is speaking
                var data = await root.ApiClient.post(pageUrl('SendMessage'), body, { timeout: 0 });
                root.toast.success(data.message);
                updateStats(data.stats);
                addRecentMessage(data.recentMessage);

                // Clear only the message: the voice settings stay for the next one
                textarea.value = '';
                updateCounter();
                if (currentMode === 'pro') showSsml('');
                textarea.focus();
            } catch (err) {
                // The text stays so it can be sent again
                if (!applyFieldError(err)) {
                    if (err && err.data && err.data.code === 'not_connected' && root.VoiceChannelPanel && root.VoiceChannelPanel.reveal) {
                        root.VoiceChannelPanel.reveal();
                    }
                    root.ApiClient.showErrorToast(err);
                }
            } finally {
                sending = false;
                pending(sendBtn, false);
                previewBtn.disabled = !cfg.configured;
                if (!cfg.configured) sendBtn.disabled = true;
            }
        }

        async function previewMessage() {
            if (sending || previewing) return;
            if (!checkMessage()) return;

            previewing = true;
            pending(previewBtn, true, 'Making preview…');
            sendBtn.disabled = true;
            try {
                var body = await currentBody();
                var blob = await root.ApiClient.post(pageUrl('Preview'), body, { responseType: 'blob', timeout: 60000 });
                if (previewAudio) previewAudio.pause();
                var url = URL.createObjectURL(blob);
                previewAudio = new Audio(url);
                var release = function () { URL.revokeObjectURL(url); };
                previewAudio.addEventListener('ended', release);
                previewAudio.addEventListener('error', function () {
                    release();
                    root.toast.error('Your browser could not play the preview.');
                });
                await previewAudio.play().catch(function () {
                    release();
                    root.toast.warning('Your browser blocked the preview from playing. Press Preview again.');
                });
            } catch (err) {
                if (!applyFieldError(err)) root.ApiClient.showErrorToast(err);
            } finally {
                previewing = false;
                pending(previewBtn, false);
                sendBtn.disabled = !cfg.configured;
            }
        }

        // ------------------------------------------------------------ server defaults

        async function saveSettings() {
            var form = $('settingsForm');
            var saveBtn = $('saveSettingsBtn');
            var rateInput = $('rateLimitInput');

            var rate = parseWholeNumber(rateInput.value, 1, 60);
            if (rate === null) {
                fieldError('rateLimitError', 'Enter a whole number from 1 to 60.', rateInput);
                rateInput.focus();
                return;
            }
            fieldError('rateLimitError', '', rateInput);

            pending(saveBtn, true, 'Saving…');
            try {
                var data = await root.ApiClient.post(pageUrl('UpdateSettings'), {
                    defaultVoice: $('voiceSelect').value,
                    defaultSpeed: parseFloat($('speedSlider').value),
                    defaultPitch: parseFloat($('pitchSlider').value),
                    defaultVolume: parseFloat($('volumeSlider').value),
                    autoPlayOnSend: form.elements.autoPlayOnSend.checked,
                    announceJoinsLeaves: form.elements.announceJoinsLeaves.checked,
                    rateLimitPerMinute: rate
                });
                root.toast.success(data.message);
            } catch (err) {
                if (err && err.data && err.data.field === 'rateLimitPerMinute') {
                    fieldError('rateLimitError', err.message, rateInput);
                    rateInput.focus();
                } else {
                    root.ApiClient.showErrorToast(err);
                }
            } finally {
                pending(saveBtn, false);
            }
        }

        // ------------------------------------------------------------ controls

        function updateCounter() {
            var counter = $('charCounter');
            if (!counter) return;
            var count = textarea.value.length;
            var max = textarea.maxLength;
            counter.textContent = count + '/' + max;
            counter.classList.toggle('error', count >= max);
            counter.classList.toggle('warning', count < max && count >= max * 0.8);
        }

        function updateSliderValue(type) {
            var slider = $(type + 'Slider');
            var display = $(type + 'Value');
            if (slider && display) display.textContent = sliderText(type, parseFloat(slider.value));
        }

        doc.querySelectorAll('[data-slider]').forEach(function (slider) {
            slider.addEventListener('input', function () {
                updateSliderValue(slider.dataset.slider);
                scheduleSsmlBuild();
            });
        });
        $('voiceSelect').addEventListener('change', scheduleSsmlBuild);

        textarea.addEventListener('input', function () {
            updateCounter();
            if (textarea.value.trim()) fieldError('messageError', '', textarea);
            scheduleSsmlBuild();
        });
        // Enter sends on a keyboard; Shift+Enter is a new line. Not while an IME is composing
        // (Enter then confirms the candidate), and not on a touch keyboard, where Enter is how you
        // start a new line and the Send button is right there.
        textarea.addEventListener('keydown', function (e) {
            if (e.key !== 'Enter' || e.shiftKey || e.ctrlKey || e.metaKey || e.isComposing) return;
            if (root.matchMedia && root.matchMedia('(pointer: coarse)').matches) return;
            e.preventDefault();
            if (!sendBtn.disabled) sendMessage();
        });

        $('ttsForm').addEventListener('submit', function (e) { e.preventDefault(); sendMessage(); });
        previewBtn.addEventListener('click', previewMessage);
        $('settingsForm').addEventListener('submit', function (e) { e.preventDefault(); saveSettings(); });
        $('rateLimitInput').addEventListener('input', function () { fieldError('rateLimitError', '', $('rateLimitInput')); });

        doc.getElementById('recentMessagesList').addEventListener('click', function (e) {
            var del = e.target.closest('[data-message-delete]');
            if (del) deleteMessage(del);
        });

        // ------------------------------------------------------------ component callbacks

        function handleModeChange(mode) {
            currentMode = mode;
            var show = function (id, visible) {
                var el = $(id);
                if (el) el.classList.toggle('hidden', !visible);
            };
            show('presetBarContainer', mode !== 'simple');
            show('styleSelectorContainer', mode !== 'simple');
            show('emphasisToolbarContainer', mode === 'pro');
            show('ssmlPreviewContainer', mode === 'pro');
            if (mode === 'pro') scheduleSsmlBuild();
        }

        function handlePresetApply(preset) {
            var voiceSelect = $('voiceSelect');
            if (voiceSelect && preset.voice) voiceSelect.value = preset.voice;

            var styleSelect = $('styleSelector-select');
            if (styleSelect) {
                styleSelect.value = preset.style || '';
                if (root.styleSelector_onStyleChange) root.styleSelector_onStyleChange('styleSelector');
            }
            $('hiddenStyle').value = preset.style || '';

            if (preset.speed) { $('speedSlider').value = preset.speed; updateSliderValue('speed'); }
            if (preset.pitch) { $('pitchSlider').value = preset.pitch; updateSliderValue('pitch'); }

            root.toast.success('Applied the "' + preset.name + '" preset. It is used for your next message.', { key: 'tts-preset' });
            scheduleSsmlBuild();
        }

        function handleStyleChange(style) {
            $('hiddenStyle').value = style || '';
            scheduleSsmlBuild();
        }

        function handleIntensityChange(intensity) {
            $('hiddenStyleIntensity').value = intensity;
        }

        function handleFormatChange() { scheduleSsmlBuild(); }

        function handleSsmlCopy() {
            root.toast.success('SSML copied to clipboard.', { key: 'ssml-copied' });
        }

        root.updateSliderValue = updateSliderValue;
        root.handleModeChange = handleModeChange;
        root.handlePresetApply = handlePresetApply;
        root.handleStyleChange = handleStyleChange;
        root.handleIntensityChange = handleIntensityChange;
        root.handleFormatChange = handleFormatChange;
        root.handleSsmlCopy = handleSsmlCopy;

        // ------------------------------------------------------------ start up

        updateCounter();

        // The mode switcher restores the saved mode before this script is loaded, so ask it
        var switcher = $('modeSwitcher');
        handleModeChange((switcher && switcher.dataset.currentMode) || 'standard');

        // Voice panel updates (now playing, Stop, queue) arrive over the hub
        if (root.DashboardHub && typeof root.DashboardHub.connect === 'function') {
            Promise.resolve(root.DashboardHub.connect()).catch(function () { /* the global connection banner reports it */ });
        }
    }

    return api;
});
