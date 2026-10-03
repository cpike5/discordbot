(function() {
    'use strict';

    // ========================================
    // Configuration
    // ========================================
    const CONFIG = {
        SEARCH_DEBOUNCE_MS: 150,
        BATCH_SIZE: 40,
        FULLSCREEN_AUTO_HIDE_MS: 3000,
        ANIMATION_DURATION_MS: 300
    };

    // ========================================
    // API Endpoints
    // ========================================
    const API = {
        favorites: (guildId) => `/api/portal/soundboard/${guildId}/favorites`,
        toggleFavorite: (guildId, soundId) => `/api/portal/soundboard/${guildId}/favorites/${soundId}`,
        play: (guildId, soundId) => `/api/portal/soundboard/${guildId}/play/${soundId}`,
        delete: (guildId, soundId) => `/api/portal/soundboard/${guildId}/sounds/${soundId}`,
        upload: (guildId) => `/api/portal/soundboard/${guildId}/sounds`,
        audio: (guildId, soundId) => `/api/portal/soundboard/${guildId}/sounds/${soundId}/audio`
    };

    // ========================================
    // State
    // ========================================
    let guildId = null;                    // CRITICAL: Always string, never parse to number
    let currentUserId = null;              // CRITICAL: Always string
    let favorites = [];
    let currentlyPlaying = null;
    let searchDebounceTimer = null;
    let signalRConnected = false;
    let isFullscreen = false;
    let fullscreenAutoHideTimer = null;
    let currentSort = 'name-asc';
    let previewAudio = null;
    let selectedFile = null;
    let isUploading = false;
    let uploadMessageSource = null;
    let maxSizeBytes = 0;
    let maxSounds = 0;
    let currentSoundCount = 0;
    let maxDurationSeconds = 0;
    const recentlyAddedSoundIds = new Set();

    // VirtualSoundGrid instance
    let virtualGrid = null;

    // ========================================
    // VirtualSoundGrid
    // ========================================
    class VirtualSoundGrid {
        constructor(container, options = {}) {
            this.container = container;
            this.allSounds = [];           // Full sound array
            this.filteredSounds = [];      // After search/sort/category filter
            this.renderedCount = 0;        // How many cards rendered so far
            this.batchSize = options.batchSize || CONFIG.BATCH_SIZE;
            this.observer = null;          // IntersectionObserver for sentinel
            this.sentinel = null;          // Sentinel element
            this.emptyState = document.getElementById('emptyState');
            this._createSentinel();
            this._setupObserver();
        }

        _createSentinel() {
            this.sentinel = document.createElement('div');
            this.sentinel.className = 'sound-grid-sentinel';
            this.sentinel.setAttribute('aria-hidden', 'true');
            // The sentinel is the grid's last child, so it sits inside whatever scrolls the cards
            this._placeSentinel();
        }

        _placeSentinel() {
            // Appending moves it to the end, after the cards just added
            this.container.appendChild(this.sentinel);
        }

        /**
         * The element that scrolls the grid: the grid itself on desktop, where it has its own
         * scrollbar, and the page on phones and tablets (null means the viewport). An observer
         * rooted on the viewport never sees the sentinel move inside a nested scroller.
         */
        _scrollRoot() {
            const overflowY = window.getComputedStyle(this.container).overflowY;
            return overflowY === 'auto' || overflowY === 'scroll' ? this.container : null;
        }

        _setupObserver() {
            if (this.observer) this.observer.disconnect();
            this.observedRoot = this._scrollRoot();
            this.observer = new IntersectionObserver((entries) => {
                for (const entry of entries) {
                    if (entry.isIntersecting && this.renderedCount < this.filteredSounds.length) {
                        this.loadMore();
                    }
                }
            }, {
                root: this.observedRoot,
                rootMargin: '200px',
                threshold: 0
            });
            this.observer.observe(this.sentinel);

            // The grid changes scroller when the window crosses the tablet breakpoint
            if (!this.resizeBound) {
                this.resizeBound = true;
                window.addEventListener('resize', () => {
                    if (this._scrollRoot() !== this.observedRoot) this._setupObserver();
                });
            }
        }

        /**
         * Set the full sound array (from page data or API).
         * @param {Array} sounds - Array of sound objects
         */
        setSounds(sounds) {
            this.allSounds = sounds.slice();
            this.filteredSounds = this.allSounds.slice();
        }

        /**
         * Render the initial batch of sound cards.
         */
        render() {
            this.renderedCount = 0;
            this.container.innerHTML = '';
            this._placeSentinel();
            this._updateEmptyState();
            if (this.filteredSounds.length > 0) {
                this.loadMore();
            }
        }

        /**
         * Render the next batch of cards when the sentinel scrolls into view.
         */
        loadMore() {
            const end = Math.min(this.renderedCount + this.batchSize, this.filteredSounds.length);
            const fragment = document.createDocumentFragment();

            for (let i = this.renderedCount; i < end; i++) {
                const card = createSoundCardElement(this.filteredSounds[i]);
                fragment.appendChild(card);
            }

            this.container.insertBefore(fragment, this.sentinel);
            this.renderedCount = end;

            // If we've rendered everything, hide the sentinel
            if (this.renderedCount >= this.filteredSounds.length) {
                this.sentinel.style.display = 'none';
            } else {
                this.sentinel.style.display = '';
                // The sentinel may still be in view after this batch (a tall window, a short batch).
                // An observer only reports changes, so look again.
                if (this.observer) {
                    this.observer.unobserve(this.sentinel);
                    this.observer.observe(this.sentinel);
                }
            }
        }

        /**
         * Re-filter the sounds, reset rendered count, and re-render.
         * @param {string} searchTerm - Search term to filter by
         */
        filter(searchTerm) {
            const term = (searchTerm || '').toLowerCase();
            if (term) {
                this.filteredSounds = this.allSounds.filter(s =>
                    s.name.toLowerCase().includes(term)
                );
            } else {
                this.filteredSounds = this.allSounds.slice();
            }
            this._sortFiltered();
            this.render();
        }

        /**
         * Re-sort filtered sounds, reset, and re-render.
         * @param {string} sortBy - Sort key
         */
        sort(sortBy) {
            currentSort = sortBy;
            this._sortFiltered();
            this.render();
            // Re-apply favorite visual state after re-render
            applyFavoriteState();
            // Re-apply playing state after re-render
            applyPlayingState();
        }

        /**
         * Internal sort of the filteredSounds array.
         */
        _sortFiltered() {
            this.filteredSounds.sort((a, b) => {
                const aFav = favorites.includes(a.id);
                const bFav = favorites.includes(b.id);

                // Favorites always sort first
                if (aFav && !bFav) return -1;
                if (!aFav && bFav) return 1;

                return compareBySort(a, b);
            });
        }

        /**
         * Insert a new sound into the allSounds array and re-render if it would be visible.
         * @param {object} sound - Sound data object
         */
        addSound(sound) {
            this.allSounds.push(sound);
            // Re-apply current filter and sort
            const searchTerm = document.getElementById('searchInput')?.value || '';
            this.filter(searchTerm);
            // Re-apply favorite state
            applyFavoriteState();
        }

        /**
         * Remove a sound from the array and remove the DOM element.
         * @param {string} soundId - Sound ID to remove
         */
        removeSound(soundId) {
            // Remove from allSounds
            this.allSounds = this.allSounds.filter(s => s.id !== soundId);

            // Remove from filteredSounds
            this.filteredSounds = this.filteredSounds.filter(s => s.id !== soundId);

            // Remove from DOM with animation
            const card = this.container.querySelector(`.sound-card[data-sound-id="${soundId}"]`);
            if (card) {
                card.style.transition = `opacity ${CONFIG.ANIMATION_DURATION_MS}ms ease, transform ${CONFIG.ANIMATION_DURATION_MS}ms ease`;
                card.style.opacity = '0';
                card.style.transform = 'scale(0.9)';

                setTimeout(() => {
                    card.remove();
                    this.renderedCount = Math.max(0, this.renderedCount - 1);
                    this._updateEmptyState();

                    // Try to load more if the sentinel is now visible
                    if (this.renderedCount < this.filteredSounds.length) {
                        this.sentinel.style.display = '';
                    }
                }, CONFIG.ANIMATION_DURATION_MS);
            } else {
                this._updateEmptyState();
            }
        }

        /**
         * Update a sound in the array and update the DOM element if rendered.
         * @param {object} sound - Updated sound data
         */
        updateSound(sound) {
            // Update in allSounds
            const allIdx = this.allSounds.findIndex(s => s.id === sound.id);
            if (allIdx !== -1) {
                this.allSounds[allIdx] = { ...this.allSounds[allIdx], ...sound };
            }

            // Update in filteredSounds
            const filtIdx = this.filteredSounds.findIndex(s => s.id === sound.id);
            if (filtIdx !== -1) {
                this.filteredSounds[filtIdx] = { ...this.filteredSounds[filtIdx], ...sound };
            }

            // Update DOM if rendered
            const card = this.container.querySelector(`.sound-card[data-sound-id="${sound.id}"]`);
            if (card) {
                if (sound.name !== undefined) {
                    card.setAttribute('data-sound-name', sound.name);
                    const nameEl = card.querySelector('.sound-name');
                    if (nameEl) nameEl.textContent = sound.name;
                }
                if (sound.playCount !== undefined) {
                    card.setAttribute('data-play-count', sound.playCount);
                    const playsEl = card.querySelector('.sound-plays');
                    if (playsEl) playsEl.textContent = `${sound.playCount} plays`;
                }
            }
        }

        /**
         * Update the empty state visibility.
         */
        _updateEmptyState() {
            if (!this.emptyState) return;
            if (this.filteredSounds.length === 0) {
                this.container.classList.add('hidden');
                this.emptyState.classList.remove('hidden');

                // Say why: nothing uploaded yet, or nothing matches the search
                const term = (document.getElementById('searchInput')?.value || '').trim();
                const title = this.emptyState.querySelector('.empty-state-title');
                const text = this.emptyState.querySelector('.empty-state-text');
                const clear = document.getElementById('emptyStateClear');
                if (term) {
                    if (title) title.textContent = 'No sounds match your search';
                    if (text) text.textContent = 'Try a different word, or clear the search to see every sound.';
                    if (clear) clear.classList.remove('hidden');
                } else {
                    if (title) title.textContent = 'No sounds yet';
                    if (text) text.textContent = 'Upload the first one and everyone here can play it.';
                    if (clear) clear.classList.add('hidden');
                }
            } else {
                this.container.classList.remove('hidden');
                this.emptyState.classList.add('hidden');
            }
        }

        /**
         * Destroy the observer and clean up.
         */
        destroy() {
            if (this.observer) {
                this.observer.disconnect();
                this.observer = null;
            }
            if (this.sentinel && this.sentinel.parentNode) {
                this.sentinel.parentNode.removeChild(this.sentinel);
            }
            this.resizeBound = true;
        }
    }

    // ========================================
    // Sort Comparison
    // ========================================
    function compareBySort(a, b) {
        switch (currentSort) {
            case 'name-desc':
                return b.name.localeCompare(a.name);
            case 'most-played': {
                const aPlays = a.playCount || 0;
                const bPlays = b.playCount || 0;
                if (bPlays !== aPlays) return bPlays - aPlays;
                return a.name.localeCompare(b.name);
            }
            case 'newest': {
                const aDate = a.uploadedAt || '';
                const bDate = b.uploadedAt || '';
                if (!aDate && !bDate) return 0;
                if (!aDate) return 1;
                if (!bDate) return -1;
                return new Date(bDate) - new Date(aDate);
            }
            case 'oldest': {
                const aDate = a.uploadedAt || '';
                const bDate = b.uploadedAt || '';
                if (!aDate && !bDate) return 0;
                if (!aDate) return 1;
                if (!bDate) return -1;
                return new Date(aDate) - new Date(bDate);
            }
            case 'name-asc':
            default:
                return a.name.localeCompare(b.name);
        }
    }

    // ========================================
    // Sound Card Element Creation
    // ========================================
    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML.replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    function formatDuration(seconds) {
        seconds = Math.floor(seconds);
        if (seconds < 60) return seconds + 's';
        return Math.floor(seconds / 60) + ':' + String(seconds % 60).padStart(2, '0');
    }

    function createSoundCardElement(sound) {
        const card = document.createElement('div');
        card.className = 'sound-card';
        card.setAttribute('data-row', '');
        card.setAttribute('data-sound-id', sound.id);
        card.setAttribute('data-sound-name', sound.name);
        card.setAttribute('data-uploaded-by', sound.uploadedById || '');
        card.setAttribute('data-play-count', sound.playCount || 0);
        card.setAttribute('data-uploaded-at', sound.uploadedAt || '');
        card.setAttribute('role', 'button');
        card.setAttribute('tabindex', '0');
        card.setAttribute('aria-label', `Play ${sound.name}`);

        const canDelete = sound.uploadedById && sound.uploadedById === currentUserId;
        const deleteButtonHtml = canDelete
            ? `<button class="delete-btn row-actions" data-sound-id="${sound.id}" title="Delete sound" aria-label="Delete ${escapeHtml(sound.name)}">
                   <svg style="width: 16px; height: 16px;" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                       <path stroke-linecap="round" stroke-linejoin="round" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                   </svg>
               </button>`
            : '';

        const durationHtml = sound.durationSeconds > 0
            ? `<div class="sound-duration">${formatDuration(sound.durationSeconds)}</div>`
            : '';

        // Priced sounds wear what they cost. Unpriced ones - every sound until an admin sets a
        // price - render nothing at all.
        const price = Number(sound.price);
        const priceLabel = Number.isFinite(price) && price > 0
            ? price + (sound.currencySymbol ? ' ' + sound.currencySymbol : '')
            : '';
        const priceHtml = priceLabel
            ? `<div class="sound-price" title="Costs ${escapeHtml(priceLabel)} to play">${escapeHtml(priceLabel)}</div>`
            : '';

        card.innerHTML = `
            <button class="preview-btn" data-sound-id="${sound.id}" title="Preview in browser" aria-label="Preview ${escapeHtml(sound.name)} in browser">
                <svg style="width: 20px; height: 20px;" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                    <path stroke-linecap="round" stroke-linejoin="round" d="M9 19V6l12-3v13M9 19c0 1.105-1.343 2-3 2s-3-.895-3-2 1.343-2 3-2 3 .895 3 2zm12-3c0 1.105-1.343 2-3 2s-3-.895-3-2 1.343-2 3-2 3 .895 3 2zM9 10l12-3" />
                </svg>
            </button>
            <button class="favorite-btn" data-sound-id="${sound.id}" title="Add to favorites" aria-pressed="false">
                <svg style="width: 20px; height: 20px;" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                    <path stroke-linecap="round" stroke-linejoin="round" d="M11.049 2.927c.3-.921 1.603-.921 1.902 0l1.519 4.674a1 1 0 00.95.69h4.915c.969 0 1.371 1.24.588 1.81l-3.976 2.888a1 1 0 00-.363 1.118l1.518 4.674c.3.922-.755 1.688-1.538 1.118l-3.976-2.888a1 1 0 00-1.176 0l-3.976 2.888c-.783.57-1.838-.197-1.538-1.118l1.518-4.674a1 1 0 00-.363-1.118l-3.976-2.888c-.784-.57-.38-1.81.588-1.81h4.914a1 1 0 00.951-.69l1.519-4.674z" />
                </svg>
            </button>
            <div class="play-icon-wrapper">
                <svg class="play-icon" fill="currentColor" viewBox="0 0 24 24">
                    <path d="M8 5v14l11-7z"/>
                </svg>
            </div>
            <div class="sound-name">${escapeHtml(sound.name)}</div>
            <div class="sound-plays">${sound.playCount || 0} plays</div>
            ${durationHtml}
            ${priceHtml}
            ${deleteButtonHtml}
        `;

        // Add event listeners
        card.querySelector('.preview-btn').addEventListener('click', function(e) {
            e.stopPropagation();
            previewSound(sound.id);
        });

        card.querySelector('.favorite-btn').addEventListener('click', function(e) {
            e.stopPropagation();
            toggleFavorite(sound.id);
        });

        const deleteBtn = card.querySelector('.delete-btn');
        if (deleteBtn) {
            deleteBtn.addEventListener('click', function(e) {
                e.stopPropagation();
                deleteSound(sound.id, sound.name);
            });
        }

        card.addEventListener('click', function() {
            playSound(sound.id, sound.name);
        });

        card.addEventListener('keydown', function(e) {
            if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                playSound(sound.id, sound.name);
            }
        });

        return card;
    }

    // ========================================
    // Browser storage: private windows and blocked site data throw on access
    // ========================================
    const storage = {
        get(key) {
            try { return window.localStorage.getItem(key); } catch (e) { return null; }
        },
        set(key, value) {
            try { window.localStorage.setItem(key, value); } catch (e) { /* the choice just is not remembered */ }
        }
    };

    // ========================================
    // Initialization
    // ========================================
    function init() {
        // Get configuration from data attributes
        const configEl = document.getElementById('soundboard-config');
        if (!configEl) return;

        guildId = configEl.dataset.guildId;
        currentUserId = configEl.dataset.currentUserId || null;
        maxSizeBytes = parseInt(configEl.dataset.maxSizeBytes, 10) || 0;
        maxSounds = parseInt(configEl.dataset.maxSounds, 10) || 0;
        currentSoundCount = parseInt(configEl.dataset.currentSoundCount, 10) || 0;
        maxDurationSeconds = parseInt(configEl.dataset.maxDurationSeconds, 10) || 0;

        if (!guildId) return;

        // Load fullscreen and sort preferences
        isFullscreen = storage.get('soundboard_fullscreen_' + guildId) === 'true';
        currentSort = storage.get('portal:soundboard:sort:' + guildId) || 'name-asc';

        // Apply saved sort preference to dropdown
        const sortSelect = document.getElementById('sortSelect');
        if (sortSelect) {
            sortSelect.value = currentSort;
        }

        // Parse initial sounds data from the page
        const soundsDataEl = document.getElementById('soundboard-data');
        let initialSounds = [];
        if (soundsDataEl) {
            try {
                initialSounds = JSON.parse(soundsDataEl.textContent);
            } catch (e) {
                // Failed to parse initial sounds
            }
        }

        // Initialize virtual grid
        const gridContainer = document.getElementById('soundGrid');
        if (gridContainer) {
            virtualGrid = new VirtualSoundGrid(gridContainer, {
                batchSize: CONFIG.BATCH_SIZE
            });
            virtualGrid.setSounds(initialSounds);
        }

        setupUploadLimitState();

        // Load favorites first, then render (favorites affect sort order). Whatever goes wrong, the
        // skeleton must not stay: show the grid, or say that it could not be shown.
        loadFavorites().then(() => {
            if (virtualGrid) {
                virtualGrid.sort(currentSort);
            }
            setupEventHandlers();
            initializeFullscreen();
            initSignalR();
        }).catch(() => {
            showGridError();
        });
    }

    function showGridError() {
        const grid = document.getElementById('soundGrid');
        const emptyState = document.getElementById('emptyState');
        if (grid) {
            grid.querySelectorAll('.sound-card-skeleton').forEach(el => el.remove());
            grid.classList.add('hidden');
        }
        if (emptyState) {
            emptyState.classList.remove('hidden');
            const title = emptyState.querySelector('.empty-state-title');
            const text = emptyState.querySelector('.empty-state-text');
            const clear = document.getElementById('emptyStateClear');
            if (title) title.textContent = 'The sounds could not be shown';
            if (text) text.textContent = 'Something went wrong while loading them. Reload the page to try again.';
            if (clear) {
                clear.textContent = 'Reload';
                clear.classList.remove('hidden');
                clear.onclick = () => window.location.reload();
            }
        }
    }

    // ========================================
    // Favorites
    // ========================================
    async function loadFavorites() {
        try {
            const { ok, data } = await ApiClient.getRaw(API.favorites(guildId));
            if (ok) {
                favorites = data.favorites || [];
            }
        } catch (error) {
            console.warn('Failed to load favorites from server:', error);
        }
    }

    function applyFavoriteState() {
        favorites.forEach(soundId => {
            const btn = document.querySelector(`.favorite-btn[data-sound-id="${soundId}"]`);
            if (btn) {
                btn.classList.add('active');
                btn.setAttribute('aria-pressed', 'true');
                btn.querySelector('svg').setAttribute('fill', 'currentColor');
            }
        });
    }

    function applyPlayingState() {
        if (!currentlyPlaying) return;
        const playingCard = document.querySelector(`.sound-card[data-sound-id="${currentlyPlaying}"]`);
        if (playingCard) {
            playingCard.classList.add('playing');
        }
    }

    function toggleFavorite(soundId) {
        const btn = document.querySelector(`.favorite-btn[data-sound-id="${soundId}"]`);
        if (!btn) return;
        const svg = btn.querySelector('svg');
        const index = favorites.indexOf(soundId);
        const adding = index === -1;

        // Optimistic UI update
        if (adding) {
            favorites.push(soundId);
            btn.classList.add('active');
            btn.setAttribute('aria-pressed', 'true');
            svg.setAttribute('fill', 'currentColor');
        } else {
            favorites.splice(index, 1);
            btn.classList.remove('active');
            btn.setAttribute('aria-pressed', 'false');
            svg.setAttribute('fill', 'none');
        }

        // Re-sort grid
        if (virtualGrid) {
            const searchTerm = document.getElementById('searchInput')?.value || '';
            virtualGrid.filter(searchTerm);
            applyFavoriteState();
            applyPlayingState();
        }

        // Persist to server
        const method = adding ? 'POST' : 'DELETE';
        ApiClient.requestRaw(API.toggleFavorite(guildId, soundId), { method })
            .then(({ ok }) => {
                if (!ok) throw new Error('Failed to update favorite');
            })
            .catch(error => {
                // Revert optimistic update on failure
                if (adding) {
                    const revertIndex = favorites.indexOf(soundId);
                    if (revertIndex > -1) favorites.splice(revertIndex, 1);
                    btn.classList.remove('active');
                    btn.setAttribute('aria-pressed', 'false');
                    svg.setAttribute('fill', 'none');
                } else {
                    if (!favorites.includes(soundId)) favorites.push(soundId);
                    btn.classList.add('active');
                    btn.setAttribute('aria-pressed', 'true');
                    svg.setAttribute('fill', 'currentColor');
                }
                if (virtualGrid) {
                    const searchTerm = document.getElementById('searchInput')?.value || '';
                    virtualGrid.filter(searchTerm);
                    applyFavoriteState();
                    applyPlayingState();
                }
                toast.error('Failed to update favorite. Please try again.');
            });
    }

    // ========================================
    // SignalR Real-Time Connection
    // ========================================
    async function initSignalR() {
        // Portal members hold no dashboard role, so the hub is not on the page for them and the voice
        // panel polls the portal status endpoint instead. Playing marks follow its changes.
        if (typeof DashboardHub === 'undefined') {
            document.addEventListener('voicepanel:change', (event) => {
                if (!event.detail.isPlaying || !event.detail.isConnected) {
                    clearPlayingState();
                }
            });
            return;
        }

        try {
            // Register event handlers before connecting
            DashboardHub.on('PlaybackStarted', handlePlaybackStarted);
            DashboardHub.on('PlaybackFinished', handlePlaybackFinished);
            DashboardHub.on('PlaybackProgress', handlePlaybackProgress);
            DashboardHub.on('AudioConnected', handleAudioConnected);
            DashboardHub.on('AudioDisconnected', handleAudioDisconnected);
            DashboardHub.on('VoiceChannelMemberCountUpdated', handleVoiceChannelMemberCountUpdated);
            DashboardHub.on('SoundUploaded', handleSoundUploaded);
            DashboardHub.on('SoundDeleted', handleSoundDeleted);
            DashboardHub.on('QueueUpdated', handleQueueUpdated);

            // Global events - register empty handlers to suppress SignalR warnings
            DashboardHub.on('BotStatusUpdated', () => {});

            // Connection state handlers
            DashboardHub.on('reconnected', async () => {
                await DashboardHub.joinGuildAudioGroup(guildId);
                const status = await DashboardHub.getCurrentAudioStatus(guildId);
                if (status) {
                    syncFromAudioStatus(status);
                }
            });

            DashboardHub.on('disconnected', () => {
                signalRConnected = false;
            });

            // Connect to SignalR hub
            const connected = await DashboardHub.connect();
            if (connected) {
                signalRConnected = true;
                await DashboardHub.joinGuildAudioGroup(guildId);

                const status = await DashboardHub.getCurrentAudioStatus(guildId);
                if (status) {
                    syncFromAudioStatus(status);
                }
            }
        } catch (error) {
            // SignalR initialization failed
        }
    }

    function syncFromAudioStatus(status) {
        if (!status.isPlaying && currentlyPlaying) {
            clearPlayingState();
        }
    }

    function clearPlayingState() {
        currentlyPlaying = null;
        document.querySelectorAll('.sound-card.playing').forEach(card => {
            card.classList.remove('playing');
        });
    }

    function handlePlaybackStarted(data) {
        currentlyPlaying = data.soundId;

        document.querySelectorAll('.sound-card').forEach(card => {
            card.classList.remove('playing');
        });
        const playingCard = document.querySelector(`.sound-card[data-sound-id="${data.soundId}"]`);
        if (playingCard) {
            playingCard.classList.add('playing');

            // Increment play count in UI and data attribute
            const playsEl = playingCard.querySelector('.sound-plays');
            if (playsEl) {
                const currentText = playsEl.textContent;
                const match = currentText.match(/^(\d+)/);
                if (match) {
                    const newCount = parseInt(match[1], 10) + 1;
                    playsEl.textContent = `${newCount} plays`;
                    playingCard.setAttribute('data-play-count', newCount);
                }
            }
        }

        // Also update play count in the virtual grid's data
        if (virtualGrid) {
            const sound = virtualGrid.allSounds.find(s => s.id === data.soundId);
            if (sound) {
                sound.playCount = (sound.playCount || 0) + 1;
            }
            const filtered = virtualGrid.filteredSounds.find(s => s.id === data.soundId);
            if (filtered) {
                filtered.playCount = (filtered.playCount || 0) + 1;
            }
        }
    }

    function handlePlaybackFinished(data) {
        currentlyPlaying = null;
        document.querySelectorAll('.sound-card').forEach(card => {
            card.classList.remove('playing');
        });
    }

    function handlePlaybackProgress(data) {
        // Could be used for a progress bar in the future
    }

    function handleAudioConnected(data) {
        // Voice panel UI updates are handled by voice-channel-panel.js
    }

    function handleVoiceChannelMemberCountUpdated(data) {
        // Voice panel UI updates are handled by voice-channel-panel.js
    }

    function handleAudioDisconnected(data) {
        currentlyPlaying = null;
        document.querySelectorAll('.sound-card').forEach(card => {
            card.classList.remove('playing');
        });
    }

    function handleSoundUploaded(data) {
        // Skip if this sound was already added (e.g. by the local upload handler)
        if (recentlyAddedSoundIds.has(data.soundId)) {
            return;
        }

        // Fallback: check if card already exists in the DOM
        const existingCard = document.querySelector(`.sound-card[data-sound-id="${data.soundId}"]`);
        if (existingCard) {
            return;
        }

        // Add the new sound through the virtual grid
        if (virtualGrid) {
            virtualGrid.addSound({
                id: data.soundId,
                name: data.name,
                playCount: data.playCount || 0,
                durationSeconds: data.durationSeconds || 0,
                uploadedById: data.uploadedById || '',
                uploadedAt: data.uploadedAt || new Date().toISOString()
            });
        }

        toast.info(`New sound "${data.name}" was added`);
    }

    function handleSoundDeleted(data) {
        if (virtualGrid) {
            const sound = virtualGrid.allSounds.find(s => s.id === data.soundId);
            const soundName = sound ? sound.name : 'Sound';

            // Clear playing state if this was the playing sound
            if (currentlyPlaying === data.soundId) {
                currentlyPlaying = null;
            }

            // Remove from favorites if present
            const favIndex = favorites.indexOf(data.soundId);
            if (favIndex > -1) {
                favorites.splice(favIndex, 1);
            }

            virtualGrid.removeSound(data.soundId);

            toast.warning(`Sound "${soundName}" was deleted`);
        }
    }

    function handleQueueUpdated(data) {
        // Currently the Portal doesn't display a queue UI
    }

    // ========================================
    // Event Handlers Setup
    // ========================================
    function setupEventHandlers() {
        // Search functionality with debounce
        const searchInput = document.getElementById('searchInput');
        const clearBtn = document.getElementById('searchClearBtn');

        if (searchInput) {
            searchInput.addEventListener('input', function() {
                clearBtn.classList.toggle('visible', this.value.length > 0);

                clearTimeout(searchDebounceTimer);
                if (this.value === '') {
                    filterSounds();
                } else {
                    searchDebounceTimer = setTimeout(filterSounds, CONFIG.SEARCH_DEBOUNCE_MS);
                }
            });
        }

        // Clear search button
        if (clearBtn) {
            clearBtn.addEventListener('click', function() {
                searchInput.value = '';
                clearBtn.classList.remove('visible');
                filterSounds();
                searchInput.focus();
            });
        }

        const emptyClear = document.getElementById('emptyStateClear');
        if (emptyClear) {
            emptyClear.addEventListener('click', function() {
                if (searchInput) searchInput.value = '';
                const fsInput = document.getElementById('fullscreenSearchInput');
                if (fsInput) fsInput.value = '';
                if (clearBtn) clearBtn.classList.remove('visible');
                filterSounds();
                if (searchInput) searchInput.focus();
            });
        }

        // Sort dropdown
        const sortSelect = document.getElementById('sortSelect');
        if (sortSelect) {
            sortSelect.addEventListener('change', function() {
                currentSort = this.value;
                storage.set('portal:soundboard:sort:' + guildId, currentSort);
                if (virtualGrid) {
                    virtualGrid.sort(currentSort);
                }
            });
        }

        // Upload dropzone
        const dropzone = document.getElementById('dropzone');
        if (dropzone) {
            dropzone.addEventListener('click', () => {
                if (dropzone.getAttribute('aria-disabled') === 'true') return;
                document.getElementById('fileInput').click();
            });
            dropzone.addEventListener('dragover', handleDragOver);
            dropzone.addEventListener('dragleave', handleDragLeave);
            dropzone.addEventListener('drop', handleDrop);
        }

        // File input
        const fileInput = document.getElementById('fileInput');
        if (fileInput) {
            fileInput.addEventListener('change', handleFileSelect);
        }

        // Upload form handlers
        setupUploadHandlers();

        // Fullscreen toggle button
        const fullscreenToggle = document.getElementById('fullscreenToggle');
        if (fullscreenToggle) {
            fullscreenToggle.addEventListener('click', toggleFullscreen);
        }

        // Fullscreen exit button
        const fullscreenExitBtn = document.getElementById('fullscreenExitBtn');
        if (fullscreenExitBtn) {
            fullscreenExitBtn.addEventListener('click', exitFullscreen);
        }

        // Fullscreen search input - sync with main search
        const fsSearch = document.getElementById('fullscreenSearchInput');
        if (fsSearch) {
            fsSearch.addEventListener('input', function() {
                document.getElementById('searchInput').value = this.value;
                const clearBtn2 = document.getElementById('searchClearBtn');
                if (clearBtn2) clearBtn2.classList.toggle('visible', this.value.length > 0);
                clearTimeout(searchDebounceTimer);
                if (this.value === '') {
                    filterSounds();
                } else {
                    searchDebounceTimer = setTimeout(filterSounds, CONFIG.SEARCH_DEBOUNCE_MS);
                }
                resetFullscreenAutoHide();
            });
        }

        // Auto-hide toolbar on mouse/touch activity
        document.addEventListener('mousemove', function() {
            if (isFullscreen) resetFullscreenAutoHide();
        });
        document.addEventListener('touchstart', function() {
            if (isFullscreen) resetFullscreenAutoHide();
        });

        // Escape key to exit fullscreen
        document.addEventListener('keydown', function(e) {
            if (e.key === 'Escape' && isFullscreen) {
                exitFullscreen();
            }
        });

        // Listen for browser fullscreen change
        document.addEventListener('fullscreenchange', function() {
            if (!document.fullscreenElement && isFullscreen) {
                exitFullscreen();
            }
        });

        // Cleanup on page unload
        window.addEventListener('beforeunload', async () => {
            if (signalRConnected && typeof DashboardHub !== 'undefined') {
                await DashboardHub.leaveGuildAudioGroup(guildId);
                DashboardHub.disconnect();
            }
        });
    }

    // ========================================
    // Search Functionality
    // ========================================
    function filterSounds() {
        const searchTerm = document.getElementById('searchInput')?.value || '';
        if (virtualGrid) {
            virtualGrid.filter(searchTerm);
            applyFavoriteState();
            applyPlayingState();
        }
    }

    // ========================================
    // Sound Preview (browser-side playback)
    // ========================================
    function previewSound(soundId) {
        // Stop any current preview
        if (previewAudio) {
            previewAudio.pause();
            previewAudio.currentTime = 0;
            document.querySelectorAll('.preview-btn').forEach(btn => btn.classList.remove('previewing'));

            // If clicking the same sound, just stop
            if (previewAudio.dataset && previewAudio.dataset.soundId === soundId) {
                previewAudio = null;
                return;
            }
        }

        previewAudio = new Audio(API.audio(guildId, soundId));
        // HTMLMediaElement.dataset is read-only; assigning it throws in strict mode
        previewAudio.dataset.soundId = soundId;

        const btn = document.querySelector(`.preview-btn[data-sound-id="${soundId}"]`);
        if (btn) btn.classList.add('previewing');

        previewAudio.addEventListener('ended', function() {
            document.querySelectorAll('.preview-btn').forEach(b => b.classList.remove('previewing'));
            previewAudio = null;
        });

        previewAudio.play().catch(err => {
            if (btn) btn.classList.remove('previewing');
            toast.error('Failed to preview sound');
        });
    }

    // ========================================
    // Sound Deletion (self-uploaded only)
    // ========================================
    async function deleteSound(soundId, soundName) {
        const confirmed = await quickActions.confirm({
            title: 'Delete this sound?',
            message: `"${soundName}" will be removed for everyone on this server. This cannot be undone.`,
            confirmText: 'Delete sound',
            variant: 'danger'
        });
        if (!confirmed) return;

        const card = document.querySelector(`.sound-card[data-sound-id="${soundId}"]`);
        const deleteBtn = card ? card.querySelector('.delete-btn') : null;
        if (deleteBtn) deleteBtn.disabled = true;

        try {
            await ApiClient.del(API.delete(guildId, soundId), { errorMessage: 'Could not delete the sound. Try again.' });
        } catch (error) {
            if (deleteBtn) deleteBtn.disabled = false;
            toast.error(error.message || 'Could not delete the sound. Try again.');
            return;
        }

        // Clear playing state if this was the playing sound
        if (currentlyPlaying === soundId) {
            currentlyPlaying = null;
        }

        // Remove from favorites if present
        const favIndex = favorites.indexOf(soundId);
        if (favIndex > -1) {
            favorites.splice(favIndex, 1);
        }

        if (virtualGrid) {
            virtualGrid.removeSound(soundId);
        }

        currentSoundCount = Math.max(0, currentSoundCount - 1);
        setupUploadLimitState();

        toast.success(`"${soundName}" deleted`);
    }

    // ========================================
    // Sound Playback
    // ========================================
    const pendingPlays = new Set();

    async function playSound(soundId, soundName) {
        const voicePanel = document.getElementById('voice-channel-panel');
        const isConnected = voicePanel && voicePanel.dataset.connected === 'true';

        if (!isConnected) {
            // The answer to "join a voice channel first" is the picker, not a message about it
            toast.warning('Join a voice channel first, then tap the sound again.', { key: 'soundboard-join-first' });
            if (window.VoiceChannelPanel) VoiceChannelPanel.reveal();
            return;
        }

        // One request per sound at a time: a second tap while the first is in flight does nothing
        if (pendingPlays.has(soundId)) return;
        pendingPlays.add(soundId);
        const card = document.querySelector(`.sound-card[data-sound-id="${soundId}"]`);
        setCardPending(card, true);

        try {
            const data = await ApiClient.post(API.play(guildId, soundId), undefined, { errorMessage: 'Could not play that sound. Try again.' });

            if (data && data.wasQueued) {
                // Behind another sound: say where, and do not mark it as playing yet
                const position = data.queuePosition ? ` (number ${data.queuePosition} in line)` : '';
                toast.info(`"${soundName}" is queued${position}.`, { key: 'soundboard-queued' });
                return;
            }

            currentlyPlaying = soundId;
            clearPlayingMarks();
            if (card) card.classList.add('playing');
            if (window.VoiceChannelPanel) VoiceChannelPanel.notePlaying(soundName, 'Soundboard');
        } catch (error) {
            if (error instanceof ApiClient.ApiClientError && error.data && error.data.errorCode === 'not_connected') {
                toast.warning('The bot is not in a voice channel. Join one first.', { key: 'soundboard-join-first' });
                if (window.VoiceChannelPanel) {
                    VoiceChannelPanel.refresh();
                    VoiceChannelPanel.reveal();
                }
            } else if (error instanceof ApiClient.ApiClientError && error.status === 402) {
                // 402 Payment Required: the sound is priced and the wallet could not cover it. That
                // is a refusal to show as a warning with the price, not a failure to retry.
                toast.warning(error.message || 'You cannot afford this sound.');
            } else if (!(error instanceof ApiClient.ApiClientError && error.sessionExpired)) {
                toast.error(error.message || 'Could not play that sound. Try again.');
            }
        } finally {
            pendingPlays.delete(soundId);
            setCardPending(card, false);
        }
    }

    function setCardPending(card, pending) {
        if (!card) return;
        card.classList.toggle('pending', pending);
        card.setAttribute('aria-busy', pending ? 'true' : 'false');
    }

    function clearPlayingMarks() {
        document.querySelectorAll('.sound-card.playing').forEach(c => c.classList.remove('playing'));
    }

    // ========================================
    // File Upload
    // ========================================

    /** At the sound limit the upload controls are off and say why, instead of failing after a file is chosen. */
    function setupUploadLimitState() {
        const atLimit = maxSounds > 0 && currentSoundCount >= maxSounds;
        const reason = document.getElementById('uploadLimitReason');
        const dropzone = document.getElementById('dropzone');
        const mobileBtn = document.getElementById('mobileUploadBtn');

        if (reason) {
            reason.classList.toggle('hidden', !atLimit);
            reason.textContent = atLimit
                ? `This server is at its limit of ${maxSounds} sounds. Delete one of yours to upload another.`
                : '';
        }
        if (dropzone) {
            dropzone.classList.toggle('disabled', atLimit);
            dropzone.setAttribute('aria-disabled', atLimit ? 'true' : 'false');
        }
        if (mobileBtn) {
            mobileBtn.disabled = atLimit;
            if (reason) mobileBtn.setAttribute('aria-describedby', 'uploadLimitReason');
        }
        const counter = document.getElementById('soundCountText');
        if (counter) counter.textContent = `${currentSoundCount} / ${maxSounds}`;
    }

    function handleFileSelect(event) {
        const files = event.target.files;
        if (files.length > 0) {
            selectFile(files[0]);
        }
    }

    function handleDragOver(event) {
        event.preventDefault();
        event.stopPropagation();
        document.getElementById('dropzone').classList.add('drag-over');
    }

    function handleDragLeave(event) {
        event.preventDefault();
        event.stopPropagation();
        document.getElementById('dropzone').classList.remove('drag-over');
    }

    function handleDrop(event) {
        event.preventDefault();
        event.stopPropagation();
        document.getElementById('dropzone').classList.remove('drag-over');

        const files = event.dataTransfer.files;
        if (files.length > 0) {
            selectFile(files[0]);
        }
    }

    function selectFile(file) {
        hideUploadMessage();

        // Validate file type
        const validTypes = ['audio/mpeg', 'audio/wav', 'audio/ogg', 'audio/mp3', 'audio/x-wav'];
        const validExtensions = /\.(mp3|wav|ogg)$/i;
        if (!validTypes.includes(file.type) && !file.name.match(validExtensions)) {
            showUploadMessage('error', 'Invalid file type. Please upload MP3, WAV, or OGG files.');
            return;
        }

        // Validate file size
        if (file.size > maxSizeBytes) {
            const maxMB = Math.floor(maxSizeBytes / (1024 * 1024));
            showUploadMessage('error', `File too large. Maximum size is ${maxMB} MB.`);
            return;
        }

        // Check sound count limit
        if (currentSoundCount >= maxSounds) {
            showUploadMessage('error', `This server is at its limit of ${maxSounds} sounds. Delete one of yours to upload another.`);
            return;
        }

        // Check audio duration
        const audio = new Audio();
        const objectUrl = URL.createObjectURL(file);
        audio.src = objectUrl;
        audio.onloadedmetadata = function() {
            URL.revokeObjectURL(objectUrl);
            if (!isFinite(audio.duration) || audio.duration > maxDurationSeconds) {
                showUploadMessage('error', !isFinite(audio.duration)
                    ? 'Could not determine audio duration. Please try a different file.'
                    : `Audio too long (${Math.round(audio.duration)}s). Maximum duration is ${maxDurationSeconds} seconds.`);
                return;
            }
            showFileReady(file);
        };
        audio.onerror = function() {
            URL.revokeObjectURL(objectUrl);
            showUploadMessage('error', 'Could not read audio file. The file may be corrupted.');
        };
    }

    function showFileReady(file) {
        selectedFile = file;

        const dropzone = document.getElementById('dropzone');
        dropzone.classList.add('has-file');
        document.getElementById('dropzoneText').textContent = 'File selected';
        document.getElementById('dropzoneHint').textContent = 'Click to change';

        document.getElementById('uploadForm').classList.remove('hidden');
        document.getElementById('filePreviewName').textContent = file.name;
        document.getElementById('filePreviewSize').textContent = formatFileSize(file.size);

        const nameWithoutExt = file.name.replace(/\.[^/.]+$/, '');
        document.getElementById('soundNameInput').value = nameWithoutExt;
        document.getElementById('soundNameInput').focus();

        updateUploadButton();
    }

    function clearFileSelection() {
        selectedFile = null;

        const dropzone = document.getElementById('dropzone');
        dropzone.classList.remove('has-file');
        document.getElementById('dropzoneText').textContent = 'Drop audio file here';
        document.getElementById('dropzoneHint').textContent = 'or click to browse';

        document.getElementById('uploadForm').classList.add('hidden');
        document.getElementById('soundNameInput').value = '';
        document.getElementById('fileInput').value = '';

        hideUploadMessage();
    }

    function updateUploadButton() {
        const btn = document.getElementById('uploadBtn');
        const name = document.getElementById('soundNameInput').value.trim();

        // Check for duplicate name using the virtual grid's data
        if (name && virtualGrid) {
            const existingNames = virtualGrid.allSounds.map(s => s.name.toLowerCase());
            if (existingNames.includes(name.toLowerCase())) {
                btn.disabled = true;
                showUploadMessage('error', `A sound named "${name}" already exists. Please choose a different name.`, 'duplicate');
                return;
            } else if (uploadMessageSource === 'duplicate') {
                hideUploadMessage();
            }
        }

        btn.disabled = !selectedFile || !name || isUploading;
    }

    function formatFileSize(bytes) {
        if (bytes < 1024) return bytes + ' B';
        if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
        return (bytes / (1024 * 1024)).toFixed(2) + ' MB';
    }

    function showUploadMessage(type, message, source) {
        const container = document.getElementById('uploadMessage');
        const icon = document.getElementById('uploadMessageIcon');
        const text = document.getElementById('uploadMessageText');

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
        document.getElementById('uploadMessage').classList.add('hidden');
    }

    function uploadFile() {
        if (!selectedFile || isUploading) return;

        const uploadBtn = document.getElementById('uploadBtn');
        uploadBtn.disabled = true;
        uploadBtn.style.pointerEvents = 'none';

        const name = document.getElementById('soundNameInput').value.trim();
        if (!name) {
            showUploadMessage('error', 'Please enter a name for the sound.');
            uploadBtn.style.pointerEvents = '';
            updateUploadButton();
            return;
        }

        // Check for duplicate name
        if (virtualGrid) {
            const existingNames = virtualGrid.allSounds.map(s => s.name.toLowerCase());
            if (existingNames.includes(name.toLowerCase())) {
                showUploadMessage('error', `A sound named "${name}" already exists. Please choose a different name.`);
                uploadBtn.style.pointerEvents = '';
                updateUploadButton();
                return;
            }
        }

        isUploading = true;
        updateUploadButton();

        const progressContainer = document.getElementById('uploadProgress');
        const progressBar = document.getElementById('progressBar');
        const progressText = document.getElementById('progressText');
        progressContainer.classList.remove('hidden');
        progressBar.style.width = '0%';
        progressText.textContent = 'Uploading...';

        const formData = new FormData();
        formData.append('file', selectedFile);
        formData.append('name', name);

        const xhr = new XMLHttpRequest();
        xhr.open('POST', API.upload(guildId), true);

        xhr.upload.onprogress = function(event) {
            if (event.lengthComputable) {
                const percent = Math.round((event.loaded / event.total) * 100);
                progressBar.style.width = percent + '%';
                progressText.textContent = `Uploading... ${percent}%`;
            }
        };

        xhr.onload = function() {
            isUploading = false;
            uploadBtn.style.pointerEvents = '';
            progressContainer.classList.add('hidden');

            if (xhr.status === 201) {
                const data = JSON.parse(xhr.responseText);
                showUploadMessage('success', `Sound "${data.name}" uploaded successfully!`);
                clearFileSelection();

                recentlyAddedSoundIds.add(data.id);
                setTimeout(() => recentlyAddedSoundIds.delete(data.id), 5000);

                // Add the new sound through the virtual grid
                if (virtualGrid) {
                    virtualGrid.addSound({
                        id: data.id,
                        name: data.name,
                        playCount: 0,
                        durationSeconds: data.durationSeconds || 0,
                        uploadedById: data.uploadedById || currentUserId || '',
                        uploadedAt: data.uploadedAt || new Date().toISOString()
                    });
                    applyFavoriteState();
                }

                currentSoundCount++;
                setupUploadLimitState();
            } else {
                let errorMessage = 'Upload failed. Please try again.';
                try {
                    const error = JSON.parse(xhr.responseText);
                    if (error.message) {
                        errorMessage = error.message;
                        if (error.detail) {
                            errorMessage += ' ' + error.detail;
                        }
                    }
                } catch (e) {
                    // Use default message
                }
                showUploadMessage('error', errorMessage);
                updateUploadButton();
            }
        };

        xhr.onerror = function() {
            isUploading = false;
            uploadBtn.style.pointerEvents = '';
            progressContainer.classList.add('hidden');
            showUploadMessage('error', 'Network error. Please check your connection and try again.');
            updateUploadButton();
        };

        xhr.send(formData);
    }

    function setupUploadHandlers() {
        const clearFileBtn = document.getElementById('clearFileBtn');
        if (clearFileBtn) {
            clearFileBtn.addEventListener('click', clearFileSelection);
        }

        const soundNameInput = document.getElementById('soundNameInput');
        if (soundNameInput) {
            soundNameInput.addEventListener('input', updateUploadButton);
            soundNameInput.addEventListener('keypress', function(e) {
                if (e.key === 'Enter') {
                    e.preventDefault();
                    uploadFile();
                }
            });
        }

        const uploadBtn = document.getElementById('uploadBtn');
        if (uploadBtn) {
            uploadBtn.addEventListener('click', uploadFile);
        }
    }

    // ========================================
    // Full-Screen Mode
    // ========================================
    function initializeFullscreen() {
        if (isFullscreen) {
            enterFullscreen();
        }
    }

    function toggleFullscreen() {
        if (isFullscreen) {
            exitFullscreen();
        } else {
            enterFullscreen();
        }
    }

    function enterFullscreen() {
        isFullscreen = true;
        document.body.classList.add('portal-fullscreen');
        storage.set('soundboard_fullscreen_' + guildId, 'true');

        const toggle = document.getElementById('fullscreenToggle');
        if (toggle) {
            toggle.setAttribute('title', 'Exit full screen');
            toggle.setAttribute('aria-label', 'Exit full screen');
        }

        const mainSearch = document.getElementById('searchInput');
        const fsSearch = document.getElementById('fullscreenSearchInput');
        if (mainSearch && fsSearch) {
            fsSearch.value = mainSearch.value;
        }

        resetFullscreenAutoHide();

        if (document.documentElement.requestFullscreen) {
            document.documentElement.requestFullscreen().catch(() => {});
        }
    }

    function exitFullscreen() {
        isFullscreen = false;
        document.body.classList.remove('portal-fullscreen');
        storage.set('soundboard_fullscreen_' + guildId, 'false');

        const toggle = document.getElementById('fullscreenToggle');
        if (toggle) {
            toggle.setAttribute('title', 'Enter full screen');
            toggle.setAttribute('aria-label', 'Enter full screen');
        }

        const mainSearch = document.getElementById('searchInput');
        const fsSearch = document.getElementById('fullscreenSearchInput');
        if (mainSearch && fsSearch) {
            mainSearch.value = fsSearch.value;
            const clearBtn = document.getElementById('searchClearBtn');
            if (clearBtn) clearBtn.classList.toggle('visible', mainSearch.value.length > 0);
        }

        clearTimeout(fullscreenAutoHideTimer);

        if (document.fullscreenElement) {
            document.exitFullscreen().catch(() => {});
        }
    }

    function resetFullscreenAutoHide() {
        const toolbar = document.getElementById('fullscreenToolbar');
        if (!toolbar) return;

        toolbar.classList.remove('auto-hidden');
        clearTimeout(fullscreenAutoHideTimer);

        fullscreenAutoHideTimer = setTimeout(() => {
            if (isFullscreen) {
                const fsSearch = document.getElementById('fullscreenSearchInput');
                if (document.activeElement === fsSearch) return;
                toolbar.classList.add('auto-hidden');
            }
        }, CONFIG.FULLSCREEN_AUTO_HIDE_MS);
    }

    // ========================================
    // Initialize when DOM is ready
    // ========================================
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    // Export public API for testing/debugging
    window.PortalSoundboard = {
        init: init,
        previewSound: previewSound,
        deleteSound: deleteSound,
        playSound: playSound,
        toggleFavorite: toggleFavorite
    };

})();
