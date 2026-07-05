/*
 * soundboard-island.js — JS bridge for the Slice 6 SoundboardIsland Blazor island.
 *
 * The island (Blazor Server) owns the grid UI/state. This module keeps the inherently
 * client-side bits in JS:
 *   - browser-side audio *preview* (an <audio> element),
 *   - bot *play* (reads the voice-channel-panel DOM state, POSTs to the play API),
 *   - real-time DashboardHub events (playback/upload/delete) bridged back into the
 *     circuit through the island's DotNetObjectReference,
 *   - the responsive column count for <Virtualize> row chunking,
 *   - the sort + fullscreen preferences in localStorage,
 *   - and proxy helpers the upload widget (soundboard-upload.js) calls to reach the
 *     island's JSInvokable bridge.
 *
 * Mirrors the audio/real-time behavior of the legacy portal-soundboard.js.
 */
(function () {
    'use strict';

    let dotNetRef = null;
    let guildId = null;
    let previewAudio = null;
    let previewId = null;
    let signalRConnected = false;
    let resizeHandler = null;
    let keydownHandler = null;
    let fullscreen = false;

    const API = {
        play: (g, s) => `/api/portal/soundboard/${g}/play/${s}`,
        audio: (g, s) => `/api/portal/soundboard/${g}/sounds/${s}/audio`
    };

    function toast(type, message) {
        try {
            if (typeof ToastManager !== 'undefined') {
                ToastManager.show(type, message);
            }
        } catch (e) { /* toast unavailable */ }
    }

    function computeColumns() {
        const w = window.innerWidth || 1024;
        if (w >= 1024) return 4;
        if (w > 768) return 3;
        return 2;
    }

    async function invoke(method) {
        if (!dotNetRef) return null;
        const args = Array.prototype.slice.call(arguments, 1);
        try {
            return await dotNetRef.invokeMethodAsync.apply(dotNetRef, [method].concat(args));
        } catch (e) {
            // Circuit disposed mid-call — ignore.
            return null;
        }
    }

    function normalize(data) {
        return {
            id: (data.id || data.soundId || '').toString(),
            name: data.name || '',
            playCount: data.playCount || 0,
            durationSeconds: data.durationSeconds || 0,
            uploadedById: (data.uploadedById || '').toString(),
            uploadedAt: data.uploadedAt || new Date().toISOString()
        };
    }

    // ── Real-time (DashboardHub) ─────────────────────────────────────────
    async function initSignalR() {
        if (typeof DashboardHub === 'undefined') return;
        try {
            DashboardHub.on('PlaybackStarted', (d) => invoke('OnPlaybackStarted', (d.soundId || '').toString()));
            DashboardHub.on('PlaybackFinished', () => invoke('OnPlaybackStopped'));
            DashboardHub.on('AudioDisconnected', () => invoke('OnPlaybackStopped'));
            DashboardHub.on('SoundUploaded', (d) => invoke('AddSound', normalize(d), true));
            DashboardHub.on('SoundDeleted', (d) => invoke('OnSoundDeleted', (d.soundId || '').toString()));

            // Suppress unhandled-event warnings for streams the island doesn't use.
            DashboardHub.on('PlaybackProgress', () => {});
            DashboardHub.on('QueueUpdated', () => {});
            DashboardHub.on('BotStatusUpdated', () => {});

            DashboardHub.on('reconnected', async () => {
                await DashboardHub.joinGuildAudioGroup(guildId);
                const status = await DashboardHub.getCurrentAudioStatus(guildId);
                if (status && !status.isPlaying) {
                    invoke('OnPlaybackStopped');
                }
            });
            DashboardHub.on('disconnected', () => { signalRConnected = false; });

            const connected = await DashboardHub.connect();
            if (connected) {
                signalRConnected = true;
                await DashboardHub.joinGuildAudioGroup(guildId);
                const status = await DashboardHub.getCurrentAudioStatus(guildId);
                if (status && !status.isPlaying) {
                    invoke('OnPlaybackStopped');
                }
            }
        } catch (e) {
            // SignalR init failed — grid still works without real-time.
        }
    }

    // ── Public API ───────────────────────────────────────────────────────
    window.soundboardIsland = {
        register: function (ref, gId, _userId) {
            dotNetRef = ref;
            guildId = gId;

            resizeHandler = function () {
                invoke('OnColumnsChanged', computeColumns());
            };
            window.addEventListener('resize', resizeHandler);

            keydownHandler = function (e) {
                if (e.key === 'Escape' && fullscreen) {
                    window.soundboardIsland.toggleFullscreen();
                }
            };
            document.addEventListener('keydown', keydownHandler);

            // Restore persisted fullscreen state.
            try {
                if (localStorage.getItem('soundboard_fullscreen_' + guildId) === 'true') {
                    enterFullscreen();
                }
            } catch (e) { /* ignore */ }

            initSignalR();
        },

        unregister: function () {
            try {
                if (previewAudio) { previewAudio.pause(); previewAudio = null; previewId = null; }
                if (resizeHandler) { window.removeEventListener('resize', resizeHandler); resizeHandler = null; }
                if (keydownHandler) { document.removeEventListener('keydown', keydownHandler); keydownHandler = null; }
                if (signalRConnected && typeof DashboardHub !== 'undefined') {
                    DashboardHub.leaveGuildAudioGroup(guildId);
                }
            } catch (e) { /* ignore */ }
            dotNetRef = null;
        },

        getColumns: function () { return computeColumns(); },

        getSort: function () {
            try { return localStorage.getItem('portal:soundboard:sort:' + guildId); }
            catch (e) { return null; }
        },

        setSort: function (value) {
            try { localStorage.setItem('portal:soundboard:sort:' + guildId, value); }
            catch (e) { /* ignore */ }
        },

        // Browser-side preview. Returns true if this sound is now previewing, false if
        // it was toggled off. The island tracks the highlight; 'ended' clears it.
        preview: function (soundId) {
            if (previewAudio) {
                previewAudio.pause();
                previewAudio.currentTime = 0;
                const wasSame = previewId === soundId;
                previewAudio = null;
                previewId = null;
                if (wasSame) {
                    return false;
                }
            }

            const audio = new Audio(API.audio(guildId, soundId));
            previewAudio = audio;
            previewId = soundId;
            audio.addEventListener('ended', function () {
                if (previewId === soundId) { previewAudio = null; previewId = null; }
                invoke('OnPreviewEnded', soundId);
            });
            audio.play().catch(function () {
                if (previewId === soundId) { previewAudio = null; previewId = null; }
                toast('error', 'Failed to preview sound');
                invoke('OnPreviewEnded', soundId);
            });
            return true;
        },

        // Bot playback. Reads the voice panel connection state (rendered + driven by
        // voice-channel-panel.js, outside the island), POSTs to the play API.
        play: function (soundId) {
            const panel = document.getElementById('voice-channel-panel');
            const isConnected = panel && panel.dataset.connected === 'true';
            if (!isConnected) {
                toast('warning', 'Please join a voice channel first!');
                return;
            }

            fetch(API.play(guildId, soundId), {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' }
            }).then(async function (response) {
                if (!response.ok) {
                    const err = await response.json().catch(() => ({}));
                    if (err.errorCode === 'not_connected') {
                        toast('warning', 'Please join a voice channel first!');
                        return;
                    }
                    toast('error', err.message || 'Failed to play sound. Please try again.');
                }
                // Success → DashboardHub PlaybackStarted drives the playing highlight.
            }).catch(function () {
                toast('error', 'Failed to play sound. Please try again.');
            });
        },

        toggleFullscreen: function () {
            if (fullscreen) { exitFullscreen(); } else { enterFullscreen(); }
        },

        // Proxies for soundboard-upload.js → island JSInvokable bridge.
        hasName: function (name) { return invoke('HasName', name); },
        getSoundCount: function () { return invoke('GetSoundCount'); },
        addUploadedSound: function (data) { return invoke('AddSound', normalize(data), false); }
    };

    function enterFullscreen() {
        fullscreen = true;
        document.body.classList.add('portal-fullscreen');
        try { localStorage.setItem('soundboard_fullscreen_' + guildId, 'true'); } catch (e) { /* ignore */ }
        if (document.documentElement.requestFullscreen) {
            document.documentElement.requestFullscreen().catch(() => {});
        }
    }

    function exitFullscreen() {
        fullscreen = false;
        document.body.classList.remove('portal-fullscreen');
        try { localStorage.setItem('soundboard_fullscreen_' + guildId, 'false'); } catch (e) { /* ignore */ }
        if (document.fullscreenElement) {
            document.exitFullscreen().catch(() => {});
        }
    }
})();
