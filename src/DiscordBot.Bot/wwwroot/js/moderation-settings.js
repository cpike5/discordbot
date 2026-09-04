/**
 * Moderation Settings Page Module
 * Handles tab switching, AJAX form submissions, and settings management for guild moderation configuration
 */
(function() {
    'use strict';

    let currentTab = 'overview';
    let isDirty = false;

    /**
     * Switch between settings tabs
     * @param {string} tabId - The tab ID to switch to (overview, spam, content, raid, tags)
     */
    async function switchTab(tabId) {
        if (isDirty) {
            const confirmed = await quickActions.confirm({
                title: 'Unsaved Changes',
                message: 'You have unsaved changes. Are you sure you want to switch tabs?',
                variant: 'warning',
                confirmText: 'Switch Tab',
                cancelText: 'Stay'
            });
            if (!confirmed) return;
        }

        currentTab = tabId;

        // Update tab buttons
        document.querySelectorAll('.settings-tab').forEach(tab => {
            if (tab.dataset.tab === tabId) {
                tab.classList.add('settings-tab-active');
            } else {
                tab.classList.remove('settings-tab-active');
            }
        });

        // Update tab content
        document.querySelectorAll('.tab-content').forEach(content => {
            content.classList.add('hidden');
        });
        document.getElementById('tab-' + tabId).classList.remove('hidden');

        // Reset dirty flag when switching tabs
        isDirty = false;
    }

    // Track the current mode for saving
    let currentMode = 'simple';

    /**
     * Set configuration mode (Simple/Advanced)
     * @param {string} mode - The mode to set ('simple' or 'advanced')
     */
    function setMode(mode) {
        const buttons = document.querySelectorAll('.mode-toggle-btn');
        buttons.forEach(btn => {
            btn.classList.remove('mode-toggle-btn-active');
        });
        event.target.classList.add('mode-toggle-btn-active');

        const simpleMode = document.getElementById('simpleMode');
        const advancedMode = document.getElementById('advancedMode');
        if (mode === 'simple') {
            simpleMode.classList.remove('hidden');
            advancedMode.classList.add('hidden');
        } else {
            simpleMode.classList.add('hidden');
            advancedMode.classList.remove('hidden');
        }

        currentMode = mode;
        isDirty = true;
    }

    /**
     * Save overview settings (mode and preset)
     */
    async function saveOverviewSettings() {
        const guildId = window.moderationData.guildId;

        // Determine the active mode button
        const activeButton = document.querySelector('.mode-toggle-btn-active');
        const mode = activeButton?.dataset.mode === 'advanced' ? 1 : 0; // 0 = Simple, 1 = Advanced

        // Get the selected preset (if in simple mode)
        const selectedPreset = document.querySelector('input[name="preset"]:checked')?.value || window.moderationData.simplePreset || 'Moderate';

        const request = {
            mode: mode,
            simplePreset: selectedPreset
        };

        try {
            const { ok, data } = await window.ApiClient.postRaw(`?handler=SaveOverview&guildId=${guildId}`, request);

            if (ok && data.success) {
                quickActions.showToast(data.message, 'success');
                isDirty = false;
            } else {
                quickActions.showToast(data.message || 'Failed to save overview settings.', 'error');
            }
        } catch (error) {
            console.error('Save overview error:', error);
            quickActions.showToast('An error occurred while saving overview settings.', 'error');
        }
    }

    /**
     * Select a preset configuration
     * @param {string} presetName - The preset name (Relaxed, Moderate, Strict)
     */
    function selectPreset(presetName) {
        applyPreset(presetName);
    }

    /**
     * Apply a preset configuration via AJAX
     * @param {string} presetName - The preset name to apply
     */
    async function applyPreset(presetName) {
        const guildId = window.moderationData.guildId;

        try {
            const { ok, data } = await window.ApiClient.postRaw(`?handler=ApplyPreset&guildId=${guildId}`, { presetName });

            if (ok && data.success) {
                quickActions.showToast(data.message, 'success');
                isDirty = false;

                // Reload page to reflect new preset settings
                setTimeout(() => window.location.reload(), 1000);
            } else {
                quickActions.showToast(data.message || 'Failed to apply preset.', 'error');
            }
        } catch (error) {
            console.error('Apply preset error:', error);
            quickActions.showToast('An error occurred while applying preset.', 'error');
        }
    }

    /**
     * Save spam detection settings
     */
    async function saveSpamSettings() {
        const guildId = window.moderationData.guildId;

        const config = {
            enabled: document.getElementById('spam-enabled').checked,
            maxMessagesPerWindow: parseInt(document.getElementById('spam-max-messages').value),
            windowSeconds: parseInt(document.getElementById('spam-window-seconds').value),
            maxMentionsPerMessage: parseInt(document.getElementById('spam-max-mentions').value),
            duplicateMessageThreshold: parseInt(document.getElementById('spam-duplicate-threshold').value) / 100,
            autoAction: parseInt(document.getElementById('spam-auto-action').value)
        };

        try {
            const { ok, data } = await window.ApiClient.postRaw(`?handler=SaveSpam&guildId=${guildId}`, config);

            if (ok && data.success) {
                quickActions.showToast(data.message, 'success');
                isDirty = false;
            } else {
                quickActions.showToast(data.message || 'Failed to save spam settings.', 'error');
            }
        } catch (error) {
            console.error('Save spam error:', error);
            quickActions.showToast('An error occurred while saving spam settings.', 'error');
        }
    }

    /**
     * Save content filter settings
     */
    async function saveContentSettings() {
        const guildId = window.moderationData.guildId;

        const prohibitedWordsText = document.getElementById('content-prohibited-words').value;
        const prohibitedWords = prohibitedWordsText
            .split(',')
            .map(w => w.trim())
            .filter(w => w.length > 0);

        const config = {
            enabled: document.getElementById('content-enabled').checked,
            prohibitedWords: prohibitedWords,
            allowedLinkDomains: [],
            blockUnlistedLinks: false,
            blockInviteLinks: document.getElementById('content-block-invites').checked,
            autoAction: parseInt(document.getElementById('content-auto-action').value)
        };

        try {
            const { ok, data } = await window.ApiClient.postRaw(`?handler=SaveContent&guildId=${guildId}`, config);

            if (ok && data.success) {
                quickActions.showToast(data.message, 'success');
                isDirty = false;
            } else {
                quickActions.showToast(data.message || 'Failed to save content settings.', 'error');
            }
        } catch (error) {
            console.error('Save content error:', error);
            quickActions.showToast('An error occurred while saving content settings.', 'error');
        }
    }

    /**
     * Save raid protection settings
     */
    async function saveRaidSettings() {
        const guildId = window.moderationData.guildId;

        const config = {
            enabled: document.getElementById('raid-enabled').checked,
            maxJoinsPerWindow: parseInt(document.getElementById('raid-max-joins').value),
            windowSeconds: parseInt(document.getElementById('raid-window-seconds').value),
            minAccountAgeHours: parseInt(document.getElementById('raid-min-account-age').value),
            autoAction: parseInt(document.getElementById('raid-auto-action').value)
        };

        try {
            const { ok, data } = await window.ApiClient.postRaw(`?handler=SaveRaid&guildId=${guildId}`, config);

            if (ok && data.success) {
                quickActions.showToast(data.message, 'success');
                isDirty = false;
            } else {
                quickActions.showToast(data.message || 'Failed to save raid settings.', 'error');
            }
        } catch (error) {
            console.error('Save raid error:', error);
            quickActions.showToast('An error occurred while saving raid settings.', 'error');
        }
    }

    /**
     * Create a new mod tag
     */
    async function createTag() {
        const guildId = window.moderationData.guildId;

        const tagName = document.getElementById('new-tag-name').value.trim();
        const tagCategory = parseInt(document.getElementById('new-tag-color').value);

        if (!tagName) {
            quickActions.showToast('Please enter a tag name.', 'error');
            return;
        }

        // Map category to color
        const colorMap = {
            0: '#6d6a66', // Default/Neutral
            1: '#2fbf7f', // Positive/Success
            2: '#ef4f4f', // Negative/Danger
            3: '#2fb3cc'  // Neutral/Info
        };

        const request = {
            guildId: guildId,
            name: tagName,
            color: colorMap[tagCategory],
            category: tagCategory,
            description: null
        };

        try {
            const { ok, data } = await window.ApiClient.postRaw(`?handler=CreateTag&guildId=${guildId}`, request);

            if (ok && data.success) {
                quickActions.showToast(data.message, 'success');

                // Add new tag to the DOM
                const tagsList = document.getElementById('tags-list');
                const colorClassMap = {
                    0: '',
                    1: 'user-tag-success',
                    2: 'user-tag-danger',
                    3: 'user-tag-info'
                };
                const colorClass = colorClassMap[tagCategory] || '';

                const tagHtml = `
                    <div class="flex items-center justify-between p-3 bg-bg-tertiary rounded-lg" data-tag-name="${tagName}">
                        <div class="flex items-center gap-3">
                            <span class="user-tag ${colorClass}">${tagName}</span>
                            <span class="text-sm text-text-secondary">Used 0 times</span>
                        </div>
                        <div class="flex items-center gap-2">
                            <button type="button" class="p-1.5 text-text-tertiary hover:text-error hover:bg-error-bg rounded transition-colors" title="Delete" onclick="window.moderationSettings.deleteTag('${tagName}')">
                                <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                                </svg>
                            </button>
                        </div>
                    </div>
                `;
                tagsList.insertAdjacentHTML('beforeend', tagHtml);

                // Clear form
                document.getElementById('new-tag-name').value = '';
                document.getElementById('new-tag-color').value = '0';
            } else {
                quickActions.showToast(data.message || 'Failed to create tag.', 'error');
            }
        } catch (error) {
            console.error('Create tag error:', error);
            quickActions.showToast('An error occurred while creating tag.', 'error');
        }
    }

    /**
     * Delete a mod tag
     * @param {string} tagName - The name of the tag to delete
     */
    async function deleteTag(tagName) {
        const confirmed = await quickActions.confirm({
            title: 'Delete Tag',
            message: `Are you sure you want to delete the tag "${tagName}"? This will remove it from all users.`,
            variant: 'danger',
            confirmText: 'Delete Tag'
        });
        if (!confirmed) {
            return;
        }

        const guildId = window.moderationData.guildId;

        try {
            const { ok, data } = await window.ApiClient.postRaw(`?handler=DeleteTag&guildId=${guildId}&tagName=${encodeURIComponent(tagName)}`);

            if (ok && data.success) {
                quickActions.showToast(data.message, 'success');

                // Remove the tag from the list
                const tagElement = document.querySelector(`[data-tag-name="${tagName}"]`);
                if (tagElement) {
                    tagElement.remove();
                }
            } else {
                quickActions.showToast(data.message || 'Failed to delete tag.', 'error');
            }
        } catch (error) {
            console.error('Delete tag error:', error);
            quickActions.showToast('An error occurred while deleting tag.', 'error');
        }
    }

    /**
     * Show the import templates modal
     */
    async function showImportTemplatesModal() {
        await quickActions.alert({
            title: 'Not Available',
            message: 'Template import is not yet implemented.',
            variant: 'info'
        });
        return;
    }

    /**
     * Import template tags
     * @param {string[]} templateNames - Array of template names to import
     */
    async function importTemplates(templateNames) {
        const guildId = window.moderationData.guildId;

        try {
            const { ok, data } = await window.ApiClient.postRaw(`?handler=ImportTemplates&guildId=${guildId}`, templateNames);

            if (ok && data.success) {
                quickActions.showToast(data.message, 'success');

                // Reload page to show imported tags (import can add multiple tags with complex logic)
                // Stay on tags tab by reloading with hash
                setTimeout(() => {
                    window.location.hash = 'tags';
                    window.location.reload();
                }, 1000);
            } else {
                quickActions.showToast(data.message || 'Failed to import templates.', 'error');
            }
        } catch (error) {
            console.error('Import templates error:', error);
            quickActions.showToast('An error occurred while importing templates.', 'error');
        }
    }

    /**
     * Track form changes to set dirty flag
     */
    function trackFormChanges() {
        const form = document.getElementById('moderationForm');
        if (!form) return;

        // Track inputs, selects, and textareas
        document.querySelectorAll('input, select, textarea').forEach(input => {
            if (input.name !== '__RequestVerificationToken') {
                input.addEventListener('input', () => {
                    isDirty = true;
                });
                input.addEventListener('change', () => {
                    isDirty = true;
                });
            }
        });
    }

    /**
     * Warn user about unsaved changes before leaving
     */
    function setupUnloadWarning() {
        window.addEventListener('beforeunload', (e) => {
            if (isDirty) {
                e.preventDefault();
                e.returnValue = 'You have unsaved changes. Are you sure you want to leave?';
                return e.returnValue;
            }
        });
    }

    /**
     * Initialize the module
     */
    function init() {
        trackFormChanges();
        setupUnloadWarning();

        // Check for hash in URL to restore tab after reload
        const hash = window.location.hash.replace('#', '');
        if (hash && ['overview', 'spam', 'content', 'raid', 'tags'].includes(hash)) {
            switchTab(hash);
            // Clear hash from URL without triggering navigation
            history.replaceState(null, '', window.location.pathname + window.location.search);
        }

        console.log('Moderation settings initialized');
    }

    // Expose public API
    window.moderationSettings = {
        switchTab,
        setMode,
        saveOverviewSettings,
        selectPreset,
        applyPreset,
        saveSpamSettings,
        saveContentSettings,
        saveRaidSettings,
        createTag,
        deleteTag,
        showImportTemplatesModal,
        importTemplates
    };

    // Initialize on DOM ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
