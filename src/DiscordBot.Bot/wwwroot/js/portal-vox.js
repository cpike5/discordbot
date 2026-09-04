// portal-vox.js
// Extracted from Pages/Portal/VOX/Index.cshtml — VOX portal composer, clip browser, A-Z rail, history/favorites.
// Config is provided by window.portalVoxConfig (see Index.cshtml), set from server-rendered values.
(function () {
    'use strict';

    // Discord snowflake IDs are 64-bit; always treat as strings in JS.
    window.guildId = window.portalVoxConfig && window.portalVoxConfig.guildId;


        // VOX State
        const voxState = {
            activeGroup: 'vox',
            clipCache: { vox: [], fvox: [], hgrunt: [] },
            highlightedIndex: -1,
            isLoading: false,
            wordGapMs: 50,
            // A-Z navigation state
            activeLetter: null,
            availableLetters: new Set(),
            focusedTileIndex: -1,
            scrollIndicatorTimeout: null,
            sectionObserver: null,
            sectionPositions: new Map() // Stores scroll positions for each section letter
        };

        // DOM Elements
        const voxEls = {
            messageInput: null,
            autocompleteDropdown: null,
            autocompleteStatus: null,
            tokenPills: null,
            previewStats: null,
            playBtn: null,
            playBtnText: null,
            clipSearch: null,
            clipCount: null,
            clipGrid: null,
            // A-Z navigation elements
            azRail: null,
            scrollLetter: null,
            // Mobile clip browser elements
            mobileSearch: null,
            mobileLetterStrip: null,
            playbackStatus: null
        };

        // Debounce utility
        function debounce(func, wait) {
            let timeout;
            return function executedFunction(...args) {
                const later = () => {
                    clearTimeout(timeout);
                    func(...args);
                };
                clearTimeout(timeout);
                timeout = setTimeout(later, wait);
            };
        }

        // HTML escape utility to prevent XSS
        function escapeHtml(text) {
            const div = document.createElement('div');
            div.textContent = text;
            return div.innerHTML;
        }


        // Initialize VOX Portal
        document.addEventListener('DOMContentLoaded', async () => {
            // Connect to SignalR hub for voice channel panel updates
            if (typeof DashboardHub !== 'undefined') {
                await DashboardHub.connect();
            }

            // Initialize DOM references for active tab
            initializeVoxElements('vox');

            // Load clips for initial group
            loadClipsForGroup('vox');

            // Load history and favorites
            loadVoxHistory();

            // Initialize word gap slider
            initWordGapSlider();

            // Initialize settings toggle for responsive collapsed state
            initSettingsToggle();

            // Listen for tab switches (nav-tabs.js dispatches 'navtabchange')
            document.addEventListener('navtabchange', (e) => {
                if (e.detail.containerId === 'voxGroupTabs') {
                    const newGroup = e.detail.tabId;
                    voxState.activeGroup = newGroup;

                    // Re-initialize elements for the new tab
                    initializeVoxElements(newGroup);

                    // Clear input and reload clips
                    if (voxEls.messageInput) {
                        voxEls.messageInput.value = '';
                    }
                    hideAutocomplete();
                    updateTokenPreview();
                    loadClipsForGroup(newGroup);
                    loadVoxHistory();
                }
            });
        });

        function initWordGapSlider() {
            // Word gap is global across all tab groups, so we sync all sliders
            const sliders = document.querySelectorAll('.vox-word-gap-slider');
            const valueDisplays = document.querySelectorAll('.vox-word-gap-value');
            const presetButtons = document.querySelectorAll('.vox-word-gap-preset');

            function updateActivePreset(value) {
                presetButtons.forEach(btn => {
                    btn.classList.toggle('active', parseInt(btn.dataset.gap) === value);
                });
            }

            function syncAllSliders(value) {
                voxState.wordGapMs = value;
                sliders.forEach(s => {
                    s.value = value;
                    s.setAttribute('aria-valuenow', value);
                });
                valueDisplays.forEach(d => d.textContent = `${value}ms`);
                updateActivePreset(value);
                updateTokenPreview();
            }

            sliders.forEach(slider => {
                slider.addEventListener('input', (e) => {
                    syncAllSliders(parseInt(e.target.value));
                });
            });

            presetButtons.forEach(btn => {
                btn.addEventListener('click', () => {
                    syncAllSliders(parseInt(btn.dataset.gap));
                });
            });
        }

        function initSettingsToggle() {
            // On tablet/mobile, collapse settings by default
            const isMobileOrTablet = window.matchMedia('(max-width: 1023px)').matches;

            document.querySelectorAll('.vox-settings-toggle').forEach(toggle => {
                const section = toggle.closest('.vox-word-gap-section');
                if (!section) return;

                if (isMobileOrTablet) {
                    section.setAttribute('aria-expanded', 'false');
                    toggle.setAttribute('aria-expanded', 'false');
                }

                toggle.addEventListener('click', () => {
                    const isExpanded = toggle.getAttribute('aria-expanded') === 'true';
                    toggle.setAttribute('aria-expanded', !isExpanded);
                    section.setAttribute('aria-expanded', !isExpanded);
                });
            });

            // Listen for resize to auto-expand on desktop
            window.matchMedia('(min-width: 1024px)').addEventListener('change', (e) => {
                if (e.matches) {
                    document.querySelectorAll('.vox-settings-toggle').forEach(toggle => {
                        const section = toggle.closest('.vox-word-gap-section');
                        if (section) {
                            section.setAttribute('aria-expanded', 'true');
                            toggle.setAttribute('aria-expanded', 'true');
                        }
                    });
                }
            });
        }

        function initializeVoxElements(group) {
            const prefix = `vox-${group}`;
            voxEls.messageInput = document.getElementById(`${prefix}-message-input`);
            voxEls.autocompleteDropdown = document.getElementById(`${prefix}-autocomplete`);
            voxEls.autocompleteStatus = document.getElementById(`${prefix}-autocomplete-status`);
            voxEls.tokenPills = document.getElementById(`${prefix}-token-pills`);
            voxEls.previewStats = document.getElementById(`${prefix}-preview-stats`);
            voxEls.playBtn = document.getElementById(`${prefix}-play-btn`);
            voxEls.playBtnText = document.getElementById(`${prefix}-play-btn-text`);
            voxEls.clipSearch = document.getElementById(`${prefix}-clip-search`);
            voxEls.clipCount = document.getElementById(`${prefix}-clip-count`);
            voxEls.clipGrid = document.getElementById(`${prefix}-clip-grid`);
            // A-Z navigation elements
            voxEls.azRail = document.getElementById(`${prefix}-az-rail`);
            voxEls.scrollLetter = document.getElementById(`${prefix}-scroll-letter`);
            // Mobile clip browser elements
            voxEls.mobileSearch = document.getElementById(`${prefix}-mobile-search`);
            voxEls.mobileLetterStrip = document.getElementById(`${prefix}-mobile-letter-strip`);
            voxEls.playbackStatus = document.getElementById(`${prefix}-playback-status`);

            // Attach event listeners
            if (voxEls.messageInput) {
                voxEls.messageInput.addEventListener('input', debounce(handleInputChange, 200));
                voxEls.messageInput.addEventListener('keydown', handleKeyDown);
                voxEls.messageInput.addEventListener('blur', (e) => {
                    // Only hide if focus moved outside the autocomplete dropdown
                    if (!voxEls.autocompleteDropdown.contains(e.relatedTarget)) {
                        hideAutocomplete();
                    }
                });
            }

            if (voxEls.playBtn) {
                voxEls.playBtn.addEventListener('click', handlePlay);
            }

            if (voxEls.clipSearch) {
                voxEls.clipSearch.addEventListener('input', debounce(filterClipGrid, 150));
            }

            // Mobile search syncs with desktop search and triggers same filtering
            if (voxEls.mobileSearch) {
                voxEls.mobileSearch.addEventListener('input', debounce(() => {
                    // Sync mobile search value to desktop search input
                    if (voxEls.clipSearch) {
                        voxEls.clipSearch.value = voxEls.mobileSearch.value;
                    }
                    filterClipGrid();
                }, 150));
            }

            if (voxEls.clipGrid) {
                voxEls.clipGrid.addEventListener('click', handleClipTileClick);
                voxEls.clipGrid.addEventListener('keydown', handleGridKeyDown);
            }

            // Initialize A-Z rail
            initializeAZRail();

            // Initialize mobile letter strip
            initializeMobileLetterStrip();

            // Reset navigation state on tab switch
            voxState.activeLetter = null;
            voxState.focusedTileIndex = -1;
        }

        // Load clips from API
        async function loadClipsForGroup(group) {
            try {
                const response = await fetch(`/api/portal/vox/${window.guildId}/clips?group=${group}`);
                if (!response.ok) {
                    const error = await response.json().catch(() => ({}));
                    throw new Error(error.message || 'Failed to load clips');
                }

                const data = await response.json();
                voxState.clipCache[group] = data.clips || [];

                renderClipGrid();
                updateTokenPreview();
            } catch (error) {
                // Show specific error message in clip grid
                if (voxEls.clipGrid) {
                    const msg = document.createElement('div');
                    msg.className = 'vox-token-empty';
                    msg.textContent = error.message || 'Failed to load clips';
                    voxEls.clipGrid.innerHTML = '';
                    voxEls.clipGrid.appendChild(msg);
                }
            }
        }

        // Autocomplete handling
        function handleInputChange() {
            updateTokenPreview();
            updateAutocomplete();
        }

        function updateAutocomplete() {
            if (!voxEls.messageInput || !voxEls.autocompleteDropdown) return;

            const text = voxEls.messageInput.value.toLowerCase();
            const words = text.split(/\s+/);
            const lastWord = words[words.length - 1] || '';

            if (lastWord.length === 0) {
                hideAutocomplete();
                return;
            }

            const clips = voxState.clipCache[voxState.activeGroup] || [];
            const prefix = `vox-${voxState.activeGroup}`;

            // Prefix matches first, then substring matches
            const prefixMatches = clips.filter(c => c.name.startsWith(lastWord));
            const substringMatches = clips.filter(c => !c.name.startsWith(lastWord) && c.name.includes(lastWord));
            const matches = [...prefixMatches, ...substringMatches].slice(0, 15);

            if (matches.length === 0) {
                hideAutocomplete();
                return;
            }

            voxState.highlightedIndex = -1;
            voxEls.autocompleteDropdown.innerHTML = matches.map((clip, idx) => `
                <div class="vox-autocomplete-item" role="option" id="${prefix}-option-${idx}" data-clip-name="${escapeHtml(clip.name)}" data-index="${idx}">
                    <span class="vox-autocomplete-name">${escapeHtml(clip.name)}</span>
                    <span class="vox-autocomplete-duration">${clip.durationSeconds.toFixed(1)}s</span>
                </div>
            `).join('');

            voxEls.autocompleteDropdown.classList.add('active');
            voxEls.messageInput.setAttribute('aria-expanded', 'true');

            // Announce to screen readers
            if (voxEls.autocompleteStatus) {
                voxEls.autocompleteStatus.textContent = `${matches.length} suggestions available`;
            }

            // Attach mousedown handlers (not click) to prevent focus loss race condition
            voxEls.autocompleteDropdown.querySelectorAll('.vox-autocomplete-item').forEach(item => {
                item.addEventListener('mousedown', (e) => {
                    e.preventDefault(); // Prevent focus loss
                    selectSuggestion(item.dataset.clipName);
                });
            });
        }

        function hideAutocomplete() {
            if (voxEls.autocompleteDropdown) {
                voxEls.autocompleteDropdown.classList.remove('active');
            }
            if (voxEls.messageInput) {
                voxEls.messageInput.setAttribute('aria-expanded', 'false');
                voxEls.messageInput.removeAttribute('aria-activedescendant');
            }
            if (voxEls.autocompleteStatus) {
                voxEls.autocompleteStatus.textContent = '';
            }
            voxState.highlightedIndex = -1;
        }

        function handleKeyDown(e) {
            const isAutocompleteActive = voxEls.autocompleteDropdown &&
                                          voxEls.autocompleteDropdown.classList.contains('active');

            // Handle Enter key - either select suggestion or play message
            if (e.key === 'Enter') {
                e.preventDefault();
                if (isAutocompleteActive) {
                    const items = Array.from(voxEls.autocompleteDropdown.querySelectorAll('.vox-autocomplete-item'));
                    if (voxState.highlightedIndex >= 0 && items[voxState.highlightedIndex]) {
                        selectSuggestion(items[voxState.highlightedIndex].dataset.clipName);
                    } else if (items.length > 0) {
                        selectSuggestion(items[0].dataset.clipName);
                    }
                } else {
                    // Autocomplete not open - send the message
                    handlePlay();
                }
                return;
            }

            // Rest of key handling only applies when autocomplete is active
            if (!isAutocompleteActive) return;

            const items = Array.from(voxEls.autocompleteDropdown.querySelectorAll('.vox-autocomplete-item'));
            if (items.length === 0) return;

            switch (e.key) {
                case 'ArrowDown':
                    e.preventDefault();
                    voxState.highlightedIndex = Math.min(voxState.highlightedIndex + 1, items.length - 1);
                    updateHighlight(items);
                    break;
                case 'ArrowUp':
                    e.preventDefault();
                    voxState.highlightedIndex = Math.max(voxState.highlightedIndex - 1, -1);
                    updateHighlight(items);
                    break;
                case 'Tab':
                    e.preventDefault();
                    if (voxState.highlightedIndex >= 0) {
                        selectSuggestion(items[voxState.highlightedIndex].dataset.clipName);
                    } else if (items.length > 0) {
                        selectSuggestion(items[0].dataset.clipName);
                    }
                    break;
                case 'Escape':
                    e.preventDefault();
                    hideAutocomplete();
                    break;
            }
        }

        function updateHighlight(items) {
            items.forEach((item, idx) => {
                item.classList.toggle('highlighted', idx === voxState.highlightedIndex);
            });

            // Update aria-activedescendant for screen readers
            if (voxState.highlightedIndex >= 0 && voxEls.messageInput) {
                voxEls.messageInput.setAttribute('aria-activedescendant', items[voxState.highlightedIndex].id);
                items[voxState.highlightedIndex].scrollIntoView({ block: 'nearest' });
            } else if (voxEls.messageInput) {
                voxEls.messageInput.removeAttribute('aria-activedescendant');
            }
        }

        function selectSuggestion(clipName) {
            if (!voxEls.messageInput) return;

            const text = voxEls.messageInput.value;
            const words = text.split(/\s+/).filter(w => w.length > 0);
            words[words.length - 1] = clipName;
            voxEls.messageInput.value = words.join(' ') + ' ';

            hideAutocomplete();
            voxEls.messageInput.focus();
            updateTokenPreview();
        }

        // Token preview
        function updateTokenPreview() {
            if (!voxEls.messageInput || !voxEls.tokenPills || !voxEls.previewStats) return;

            // Parse punctuation as timing tokens (must match server-side TokenizeMessage)
            const text = voxEls.messageInput.value.toLowerCase()
                .replace(/,/g, ' _comma ')
                .replace(/\./g, ' _period ');
            const words = text.split(/\s+/).filter(w => w.length > 0);
            const clips = voxState.clipCache[voxState.activeGroup] || [];
            const clipMap = new Map(clips.map(c => [c.name, c]));

            let matchedCount = 0;
            let totalDuration = 0;

            if (words.length === 0) {
                voxEls.tokenPills.innerHTML = '<span class="vox-token-empty">No words</span>';
                voxEls.previewStats.textContent = '';
                updatePlayButton(false);
                return;
            }

            const pillsHtml = words.map((word, idx) => {
                const clip = clipMap.get(word);
                const isMatched = !!clip;

                if (isMatched) {
                    matchedCount++;
                    totalDuration += clip.durationSeconds;
                }

                const tokenClass = isMatched ? 'matched' : 'skipped';
                const gap = idx < words.length - 1 ? '<span class="vox-token-gap">·</span>' : '';
                return `<span class="vox-token ${tokenClass}">${escapeHtml(word)}</span>${gap}`;
            }).join('');

            // Add word gap duration
            if (matchedCount > 1) {
                totalDuration += (matchedCount - 1) * (voxState.wordGapMs / 1000);
            }

            voxEls.tokenPills.innerHTML = pillsHtml;
            voxEls.previewStats.textContent = `${matchedCount}/${words.length} clips, ~${totalDuration.toFixed(1)}s`;

            updatePlayButton(matchedCount > 0);
        }

        function updatePlayButton(canPlay) {
            if (!voxEls.playBtn) return;
            voxEls.playBtn.disabled = !canPlay || voxState.isLoading;
        }

        // Play
        async function handlePlay() {
            if (!voxEls.messageInput || voxState.isLoading) return;

            const message = voxEls.messageInput.value.trim();
            if (!message) return;

            voxState.isLoading = true;
            voxEls.playBtn.classList.add('loading');
            voxEls.playBtn.disabled = true;
            voxEls.playBtnText.innerHTML = '<span class="vox-spinner" aria-hidden="true"></span> Playing...';
            if (voxEls.playbackStatus) {
                voxEls.playbackStatus.textContent = 'Playing VOX message...';
            }

            try {
                const response = await fetch(`/api/portal/vox/${window.guildId}/play`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        message: message,
                        group: voxState.activeGroup,
                        wordGapMs: voxState.wordGapMs
                    })
                });

                if (!response.ok) {
                    const error = await response.json().catch(() => ({}));
                    throw new Error(error.message || 'Failed to play VOX message');
                }

                // Success - clear the message input
                voxEls.messageInput.value = '';
                updateTokenPreview();
                voxEls.playBtnText.textContent = 'Play VOX Announcement';
                if (voxEls.playbackStatus) {
                    voxEls.playbackStatus.textContent = 'VOX message sent successfully.';
                }

                // Refresh history panel
                loadVoxHistory();
            } catch (error) {
                // Display specific error message inline
                if (voxEls.playBtnText) {
                    const errMsg = (error.message || 'Failed to play').substring(0, 100);
                    voxEls.playBtnText.textContent = 'Error: ' + errMsg;
                    if (voxEls.playbackStatus) {
                        voxEls.playbackStatus.textContent = 'Error: ' + errMsg;
                    }
                    setTimeout(() => {
                        if (voxEls.playBtnText) {
                            voxEls.playBtnText.textContent = 'Play VOX Announcement';
                        }
                    }, 4000);
                }
            } finally {
                voxState.isLoading = false;
                voxEls.playBtn.classList.remove('loading');
                updateTokenPreview();
            }
        }

        // Clip Grid with A-Z Section Headers
        function renderClipGrid() {
            if (!voxEls.clipGrid) return;

            const clips = voxState.clipCache[voxState.activeGroup] || [];
            // Use desktop search value, or mobile search if desktop is empty/hidden
            const desktopSearch = voxEls.clipSearch ? voxEls.clipSearch.value : '';
            const mobileSearch = voxEls.mobileSearch ? voxEls.mobileSearch.value : '';
            const searchTerm = (desktopSearch || mobileSearch).toLowerCase();

            // Filter with prefix matches first, then substring matches
            const prefixMatches = clips.filter(c => c.name.startsWith(searchTerm));
            const substringMatches = clips.filter(c => !c.name.startsWith(searchTerm) && c.name.includes(searchTerm));
            const filtered = [...prefixMatches, ...substringMatches];

            if (voxEls.clipCount) {
                voxEls.clipCount.textContent = searchTerm ? `${filtered.length} matching` : `${filtered.length} clips`;
            }

            if (filtered.length === 0) {
                const escapedQuery = escapeHtml(searchTerm);
                voxEls.clipGrid.innerHTML = searchTerm
                    ? `<div class="vox-token-empty">No clips match '${escapedQuery}'</div>`
                    : '<div class="vox-token-empty">No clips found</div>';
                updateAZRail(new Set());
                return;
            }

            // Group clips by first letter
            const grouped = new Map();
            filtered.forEach(clip => {
                const letter = clip.name.charAt(0).toUpperCase();
                if (!grouped.has(letter)) {
                    grouped.set(letter, []);
                }
                grouped.get(letter).push(clip);
            });

            // Sort letters alphabetically
            const sortedLetters = Array.from(grouped.keys()).sort();
            voxState.availableLetters = new Set(sortedLetters);

            // Build HTML with section headers
            let html = '';
            let tileIndex = 0;

            sortedLetters.forEach(letter => {
                const letterClips = grouped.get(letter);

                // Section header
                html += `<div class="vox-section-header" data-letter="${letter}" id="section-${voxState.activeGroup}-${letter}">${letter}</div>`;

                // Clip tiles for this section
                letterClips.forEach(clip => {
                    html += `
                        <button class="vox-clip-tile"
                                data-clip-name="${escapeHtml(clip.name)}"
                                data-tile-index="${tileIndex}"
                                role="gridcell"
                                tabindex="-1">
                            <div class="vox-clip-name">${escapeHtml(clip.name)}</div>
                            <div class="vox-clip-duration">${clip.durationSeconds.toFixed(1)}s</div>
                        </button>
                    `;
                    tileIndex++;
                });
            });

            voxEls.clipGrid.innerHTML = html;

            // Update A-Z rail
            updateAZRail(voxState.availableLetters);

            // Store section positions BEFORE any scrolling (sticky headers would affect getBoundingClientRect)
            storeSectionPositions();

            // Setup section observers for sticky header tracking
            setupSectionObservers();

            // Reset focused tile
            voxState.focusedTileIndex = -1;
        }

        function storeSectionPositions() {
            if (!voxEls.clipGrid) return;

            // Clear previous positions
            voxState.sectionPositions.clear();

            // Get all section headers and store their offsetTop
            const headers = voxEls.clipGrid.querySelectorAll('.vox-section-header');
            headers.forEach(header => {
                const letter = header.dataset.letter;
                // offsetTop gives position relative to offsetParent (the grid with position: relative)
                voxState.sectionPositions.set(letter, header.offsetTop);
            });

        }

        function filterClipGrid() {
            renderClipGrid();
        }

        function handleClipTileClick(e) {
            const tile = e.target.closest('.vox-clip-tile');
            if (!tile || !voxEls.messageInput) return;

            const clipName = tile.dataset.clipName;
            const currentValue = voxEls.messageInput.value;

            // Append clip name with space
            voxEls.messageInput.value = currentValue + (currentValue.endsWith(' ') || currentValue === '' ? '' : ' ') + clipName + ' ';

            // Visual feedback
            tile.classList.add('clicked');
            setTimeout(() => tile.classList.remove('clicked'), 200);

            // Update focused tile index
            const tiles = Array.from(voxEls.clipGrid.querySelectorAll('.vox-clip-tile'));
            voxState.focusedTileIndex = tiles.indexOf(tile);

            // Update preview and focus
            updateTokenPreview();
            voxEls.messageInput.focus();
        }

        // ==========================================
        // A-Z INDEX RAIL FUNCTIONS
        // ==========================================

        function initializeAZRail() {
            if (!voxEls.azRail) return;

            // Generate 26 letter buttons
            const letters = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ'.split('');
            voxEls.azRail.innerHTML = letters.map(letter => `
                <button class="vox-az-letter"
                        data-letter="${letter}"
                        disabled
                        aria-label="Jump to ${letter}">
                    ${letter}
                </button>
            `).join('');

            // Event delegation for letter clicks
            voxEls.azRail.addEventListener('click', (e) => {
                const btn = e.target.closest('.vox-az-letter');
                if (!btn || btn.disabled) return;

                const letter = btn.dataset.letter;
                scrollToSection(letter);
            });
        }

        function updateAZRail(availableLetters) {
            if (!voxEls.azRail) return;

            const buttons = voxEls.azRail.querySelectorAll('.vox-az-letter');
            buttons.forEach(btn => {
                const letter = btn.dataset.letter;
                const isAvailable = availableLetters.has(letter);
                btn.disabled = !isAvailable;
                btn.classList.toggle('active', letter === voxState.activeLetter && isAvailable);
            });

            // Also update mobile letter strip
            updateMobileLetterStrip(availableLetters);
        }

        function scrollToSection(letter) {
            if (!voxEls.clipGrid) return;

            // Use stored position (calculated before sticky behavior affects positions)
            const targetScroll = voxState.sectionPositions.get(letter);

            if (targetScroll === undefined) return;

            // Check for reduced motion preference
            const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

            voxEls.clipGrid.scrollTo({
                top: targetScroll,
                behavior: prefersReducedMotion ? 'auto' : 'smooth'
            });

            // Show floating indicator
            showScrollIndicator(letter);

            // Update active state
            setActiveLetter(letter);
        }

        function setActiveLetter(letter) {
            voxState.activeLetter = letter;

            // Update A-Z rail active state
            if (voxEls.azRail) {
                const buttons = voxEls.azRail.querySelectorAll('.vox-az-letter');
                buttons.forEach(btn => {
                    const isActive = btn.dataset.letter === letter && !btn.disabled;
                    btn.classList.toggle('active', isActive);
                });
            }

            // Sync mobile letter strip
            syncMobileActiveLetter(letter);
        }

        // ==========================================
        // MOBILE LETTER STRIP FUNCTIONS
        // ==========================================

        function initializeMobileLetterStrip() {
            if (!voxEls.mobileLetterStrip) return;

            // Generate 26 letter buttons (matching desktop A-Z rail)
            const letters = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ'.split('');
            voxEls.mobileLetterStrip.innerHTML = letters.map(letter => `
                <button class="vox-mobile-letter-btn"
                        data-letter="${letter}"
                        disabled
                        role="tab"
                        aria-selected="false"
                        aria-label="Jump to ${letter}">
                    ${letter}
                </button>
            `).join('');

            // Event delegation for letter taps
            voxEls.mobileLetterStrip.addEventListener('click', (e) => {
                const btn = e.target.closest('.vox-mobile-letter-btn');
                if (!btn || btn.disabled) return;

                const letter = btn.dataset.letter;
                scrollToSection(letter);
            });
        }

        function updateMobileLetterStrip(availableLetters) {
            if (!voxEls.mobileLetterStrip) return;

            const buttons = voxEls.mobileLetterStrip.querySelectorAll('.vox-mobile-letter-btn');
            buttons.forEach(btn => {
                const letter = btn.dataset.letter;
                const isAvailable = availableLetters.has(letter);
                btn.disabled = !isAvailable;
                const isActive = letter === voxState.activeLetter && isAvailable;
                btn.classList.toggle('active', isActive);
                btn.setAttribute('aria-selected', isActive ? 'true' : 'false');
            });
        }

        function syncMobileActiveLetter(letter) {
            if (!voxEls.mobileLetterStrip) return;

            const buttons = voxEls.mobileLetterStrip.querySelectorAll('.vox-mobile-letter-btn');
            buttons.forEach(btn => {
                const isActive = btn.dataset.letter === letter && !btn.disabled;
                btn.classList.toggle('active', isActive);
                btn.setAttribute('aria-selected', isActive ? 'true' : 'false');
            });

            // Scroll the active letter button into view within the strip
            const activeBtn = voxEls.mobileLetterStrip.querySelector('.vox-mobile-letter-btn.active');
            if (activeBtn) {
                activeBtn.scrollIntoView({ behavior: 'smooth', block: 'nearest', inline: 'center' });
            }
        }

        // ==========================================
        // SCROLL POSITION INDICATOR
        // ==========================================

        function showScrollIndicator(letter) {
            if (!voxEls.scrollLetter) return;

            // Check reduced motion preference
            const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
            if (prefersReducedMotion) return;

            voxEls.scrollLetter.textContent = letter;
            voxEls.scrollLetter.classList.add('visible');

            // Clear existing timeout
            if (voxState.scrollIndicatorTimeout) {
                clearTimeout(voxState.scrollIndicatorTimeout);
            }

            // Hide after 600ms of no scroll activity
            voxState.scrollIndicatorTimeout = setTimeout(() => {
                voxEls.scrollLetter.classList.remove('visible');
            }, 600);
        }

        // ==========================================
        // INTERSECTION OBSERVER FOR SECTION TRACKING
        // ==========================================

        function setupSectionObservers() {
            // Clean up existing observer
            if (voxState.sectionObserver) {
                voxState.sectionObserver.disconnect();
            }

            if (!voxEls.clipGrid) return;

            const headers = voxEls.clipGrid.querySelectorAll('.vox-section-header');
            if (headers.length === 0) return;

            // Create observer for tracking which section is visible
            voxState.sectionObserver = new IntersectionObserver((entries) => {
                // Find the topmost visible section
                let topmostHeader = null;
                let topmostTop = Infinity;

                entries.forEach(entry => {
                    const header = entry.target;
                    const rect = header.getBoundingClientRect();

                    // Track headers that are at or above the grid's top
                    if (entry.isIntersecting || rect.top < 50) {
                        if (rect.top < topmostTop) {
                            topmostTop = rect.top;
                            topmostHeader = header;
                        }
                    }

                    // Add/remove stuck class
                    header.classList.toggle('stuck', rect.top <= 1);
                });

                // Also check for the header closest to the top when scrolling
                const gridRect = voxEls.clipGrid.getBoundingClientRect();
                headers.forEach(header => {
                    const rect = header.getBoundingClientRect();
                    if (rect.top <= gridRect.top + 10 && rect.bottom > gridRect.top) {
                        topmostHeader = header;
                    }
                });

                if (topmostHeader) {
                    const letter = topmostHeader.dataset.letter;
                    if (letter !== voxState.activeLetter) {
                        setActiveLetter(letter);
                    }
                }
            }, {
                root: voxEls.clipGrid,
                threshold: [0, 0.1, 0.5, 1],
                rootMargin: '0px 0px -80% 0px'
            });

            headers.forEach(header => {
                voxState.sectionObserver.observe(header);
            });

            // Also track scroll for real-time updates
            voxEls.clipGrid.addEventListener('scroll', handleGridScroll, { passive: true });
        }

        function handleGridScroll() {
            if (!voxEls.clipGrid) return;

            const headers = voxEls.clipGrid.querySelectorAll('.vox-section-header');
            const gridRect = voxEls.clipGrid.getBoundingClientRect();

            let currentLetter = null;

            // Find the header that's at or just above the top of the grid
            headers.forEach(header => {
                const rect = header.getBoundingClientRect();
                if (rect.top <= gridRect.top + 20) {
                    currentLetter = header.dataset.letter;
                }

                // Update stuck class
                header.classList.toggle('stuck', rect.top <= gridRect.top + 1);
            });

            if (currentLetter && currentLetter !== voxState.activeLetter) {
                setActiveLetter(currentLetter);
                showScrollIndicator(currentLetter);
            }
        }

        // ==========================================
        // VOX HISTORY & FAVORITES
        // ==========================================

        function toggleHistorySection(prefix, section) {
            const list = document.getElementById(`${prefix}-${section}-list`);
            const toggle = document.getElementById(`${prefix}-${section}-toggle`);
            if (!list || !toggle) return;

            const isHidden = list.hidden;
            list.hidden = !isHidden;
            toggle.classList.toggle('collapsed', !isHidden);

            // Update aria-expanded on the header button
            const header = toggle.closest('.vox-history-header');
            if (header) {
                header.setAttribute('aria-expanded', isHidden ? 'true' : 'false');
            }
        }

        async function loadVoxHistory() {
            const prefix = `vox-${voxState.activeGroup}`;

            try {
                const [historyRes, favoritesRes] = await Promise.all([
                    fetch(`/api/portal/vox/${window.guildId}/history?limit=20`),
                    fetch(`/api/portal/vox/${window.guildId}/favorites`)
                ]);

                if (historyRes.ok) {
                    const history = await historyRes.json();
                    renderHistoryList(prefix, 'recent', history);
                }

                if (favoritesRes.ok) {
                    const favorites = await favoritesRes.json();
                    renderHistoryList(prefix, 'favorites', favorites);
                }
            } catch (error) {
                // Silent fail - history is non-critical
                console.warn('Failed to load VOX history:', error);
            }
        }

        function renderHistoryList(prefix, section, entries) {
            const list = document.getElementById(`${prefix}-${section}-list`);
            const countBadge = document.getElementById(`${prefix}-${section}-count`);
            if (!list) return;

            if (countBadge) {
                countBadge.textContent = entries.length;
            }

            if (entries.length === 0) {
                list.innerHTML = `<div class="vox-history-empty">${section === 'favorites' ? 'No favorites yet' : 'No recent messages'}</div>`;
                return;
            }

            list.innerHTML = entries.map(entry => {
                const timeAgo = formatTimeAgo(new Date(entry.playedAt));
                const favClass = entry.isFavorite ? 'favorite-active' : '';
                const favIcon = entry.isFavorite ? '&#9733;' : '&#9734;';

                return `
                    <div class="vox-history-item" data-entry-id="${entry.id}">
                        <span class="vox-history-message"
                              title="${escapeHtml(entry.message)}"
                              onclick="replayFromHistory(${entry.id}, '${escapeHtml(entry.message)}', '${escapeHtml(entry.clipGroup)}', ${entry.wordGapMs})">
                            ${escapeHtml(entry.message)}
                        </span>
                        <span class="vox-history-meta">${escapeHtml(entry.clipGroup)} &middot; ${timeAgo}</span>
                        <div class="vox-history-actions">
                            <button class="vox-history-action-btn ${favClass}"
                                    title="${entry.isFavorite ? 'Remove from favorites' : 'Add to favorites'}"
                                    onclick="toggleFavorite(${entry.id})">
                                ${favIcon}
                            </button>
                            <button class="vox-history-action-btn delete"
                                    title="Delete"
                                    onclick="deleteHistoryEntry(${entry.id})">
                                &#10005;
                            </button>
                        </div>
                    </div>
                `;
            }).join('');
        }

        function replayFromHistory(entryId, message, clipGroup, wordGapMs) {
            if (!voxEls.messageInput) return;

            voxEls.messageInput.value = message;
            updateTokenPreview();
            voxEls.messageInput.focus();
        }

        async function toggleFavorite(entryId) {
            try {
                const response = await fetch(`/api/portal/vox/${window.guildId}/history/${entryId}/favorite`, {
                    method: 'POST'
                });

                if (!response.ok) {
                    const error = await response.json().catch(() => ({}));
                    throw new Error(error.message || 'Failed to toggle favorite');
                }

                // Refresh both lists
                await loadVoxHistory();
            } catch (error) {
                console.error('Failed to toggle favorite:', error);
                if (typeof ToastManager !== 'undefined') {
                    ToastManager.show('error', 'Failed to update favorite');
                }
            }
        }

        async function deleteHistoryEntry(entryId) {
            try {
                const response = await fetch(`/api/portal/vox/${window.guildId}/history/${entryId}`, {
                    method: 'DELETE'
                });

                if (!response.ok) {
                    const error = await response.json().catch(() => ({}));
                    throw new Error(error.message || 'Failed to delete entry');
                }

                // Refresh both lists
                await loadVoxHistory();
            } catch (error) {
                console.error('Failed to delete history entry:', error);
                if (typeof ToastManager !== 'undefined') {
                    ToastManager.show('error', 'Failed to delete entry');
                }
            }
        }

        function formatTimeAgo(date) {
            const now = new Date();
            const diffMs = now - date;
            const diffSec = Math.floor(diffMs / 1000);
            const diffMin = Math.floor(diffSec / 60);
            const diffHr = Math.floor(diffMin / 60);
            const diffDay = Math.floor(diffHr / 24);

            if (diffSec < 60) return 'just now';
            if (diffMin < 60) return `${diffMin}m ago`;
            if (diffHr < 24) return `${diffHr}h ago`;
            if (diffDay < 30) return `${diffDay}d ago`;
            return date.toLocaleDateString();
        }

        // ==========================================
        // KEYBOARD NAVIGATION
        // ==========================================

        function handleGridKeyDown(e) {
            const tiles = Array.from(voxEls.clipGrid.querySelectorAll('.vox-clip-tile'));
            if (tiles.length === 0) return;

            // Only handle navigation if a tile is focused or grid itself
            if (!voxEls.clipGrid.contains(document.activeElement)) return;

            // Calculate approximate columns from grid width
            const gridStyle = window.getComputedStyle(voxEls.clipGrid);
            const gridWidth = voxEls.clipGrid.clientWidth;
            const tileWidth = tiles[0] ? tiles[0].offsetWidth + 12 : 120; // Include gap
            const columns = Math.max(1, Math.floor(gridWidth / tileWidth));

            let newIndex = voxState.focusedTileIndex;

            switch (e.key) {
                case 'ArrowRight':
                    e.preventDefault();
                    newIndex = Math.min(newIndex + 1, tiles.length - 1);
                    if (newIndex < 0) newIndex = 0;
                    break;

                case 'ArrowLeft':
                    e.preventDefault();
                    newIndex = Math.max(newIndex - 1, 0);
                    break;

                case 'ArrowDown':
                    e.preventDefault();
                    newIndex = Math.min(newIndex + columns, tiles.length - 1);
                    if (newIndex < 0) newIndex = 0;
                    break;

                case 'ArrowUp':
                    e.preventDefault();
                    newIndex = Math.max(newIndex - columns, 0);
                    break;

                case 'Enter':
                case ' ':
                    e.preventDefault();
                    if (voxState.focusedTileIndex >= 0 && tiles[voxState.focusedTileIndex]) {
                        tiles[voxState.focusedTileIndex].click();
                    }
                    return;

                case 'Escape':
                    e.preventDefault();
                    // Return focus to message input
                    if (voxEls.messageInput) {
                        voxEls.messageInput.focus();
                    }
                    voxState.focusedTileIndex = -1;
                    tiles.forEach(t => t.setAttribute('tabindex', '-1'));
                    return;

                case 'Home':
                    e.preventDefault();
                    newIndex = 0;
                    break;

                case 'End':
                    e.preventDefault();
                    newIndex = tiles.length - 1;
                    break;

                default:
                    return; // Don't handle other keys
            }

            // Update focus
            if (newIndex !== voxState.focusedTileIndex && newIndex >= 0) {
                voxState.focusedTileIndex = newIndex;

                // Update tabindex
                tiles.forEach((tile, idx) => {
                    tile.setAttribute('tabindex', idx === newIndex ? '0' : '-1');
                });

                // Focus the tile
                tiles[newIndex].focus();

                // Scroll into view if needed
                const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
                tiles[newIndex].scrollIntoView({
                    block: 'nearest',
                    behavior: prefersReducedMotion ? 'auto' : 'smooth'
                });
            }
        }

        // ========================================
        // Keyboard Shortcuts (VOX)
        // ========================================
        (function () {
            if (typeof KeyboardShortcuts === 'undefined') return;

            // Ctrl+Enter: Play VOX message
            KeyboardShortcuts.register('Enter', 'Play VOX message', function () {
                if (typeof voxEls !== 'undefined' && voxEls.playBtn && !voxEls.playBtn.disabled) {
                    voxEls.playBtn.click();
                }
            }, { ctrlKey: true, category: 'VOX' });

            // /: Focus message input
            KeyboardShortcuts.register('/', 'Focus message input', function () {
                if (typeof voxEls !== 'undefined' && voxEls.messageInput) {
                    voxEls.messageInput.focus();
                }
            }, { category: 'VOX' });

            KeyboardShortcuts.init();
        })();

    // Expose functions referenced from dynamically-generated inline onclick handlers
    window.toggleHistorySection = toggleHistorySection;
    window.replayFromHistory = replayFromHistory;
    window.toggleFavorite = toggleFavorite;
    window.deleteHistoryEntry = deleteHistoryEntry;
})();
