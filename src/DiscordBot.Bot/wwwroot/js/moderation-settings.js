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
    function switchTab(tabId) {
        if (isDirty && !confirm('You have unsaved changes. Are you sure you want to switch tabs?')) {
            return;
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

        isDirty = true;
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
        const token = document.querySelector('input[name="__RequestVerificationToken"]').value;
        const guildId = window.moderationData.guildId;

        try {
            const response = await fetch(`?handler=ApplyPreset&guildId=${guildId}`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token
                },
                body: JSON.stringify({ presetName })
            });

            const data = await response.json();

            if (response.ok && data.success) {
                window.quickActions?.showToast(data.message, 'success');
                isDirty = false;

                // Reload page to reflect new preset settings
                setTimeout(() => window.location.reload(), 1000);
            } else {
                window.quickActions?.showToast(data.message || 'Failed to apply preset.', 'error');
            }
        } catch (error) {
            console.error('Apply preset error:', error);
            window.quickActions?.showToast('An error occurred while applying preset.', 'error');
        }
    }

    /**
     * Save spam detection settings
     */
    async function saveSpamSettings() {
        const token = document.querySelector('input[name="__RequestVerificationToken"]').value;
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
            const response = await fetch(`?handler=SaveSpam&guildId=${guildId}`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token
                },
                body: JSON.stringify(config)
            });

            const data = await response.json();

            if (response.ok && data.success) {
                window.quickActions?.showToast(data.message, 'success');
                isDirty = false;
            } else {
                window.quickActions?.showToast(data.message || 'Failed to save spam settings.', 'error');
            }
        } catch (error) {
            console.error('Save spam error:', error);
            window.quickActions?.showToast('An error occurred while saving spam settings.', 'error');
        }
    }

    /**
     * Save content filter settings
     */
    async function saveContentSettings() {
        const token = document.querySelector('input[name="__RequestVerificationToken"]').value;
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
            const response = await fetch(`?handler=SaveContent&guildId=${guildId}`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token
                },
                body: JSON.stringify(config)
            });

            const data = await response.json();

            if (response.ok && data.success) {
                window.quickActions?.showToast(data.message, 'success');
                isDirty = false;
            } else {
                window.quickActions?.showToast(data.message || 'Failed to save content settings.', 'error');
            }
        } catch (error) {
            console.error('Save content error:', error);
            window.quickActions?.showToast('An error occurred while saving content settings.', 'error');
        }
    }

    /**
     * Save raid protection settings
     */
    async function saveRaidSettings() {
        const token = document.querySelector('input[name="__RequestVerificationToken"]').value;
        const guildId = window.moderationData.guildId;

        const config = {
            enabled: document.getElementById('raid-enabled').checked,
            maxJoinsPerWindow: parseInt(document.getElementById('raid-max-joins').value),
            windowSeconds: parseInt(document.getElementById('raid-window-seconds').value),
            minAccountAgeHours: parseInt(document.getElementById('raid-min-account-age').value),
            autoAction: parseInt(document.getElementById('raid-auto-action').value)
        };

        try {
            const response = await fetch(`?handler=SaveRaid&guildId=${guildId}`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token
                },
                body: JSON.stringify(config)
            });

            const data = await response.json();

            if (response.ok && data.success) {
                window.quickActions?.showToast(data.message, 'success');
                isDirty = false;
            } else {
                window.quickActions?.showToast(data.message || 'Failed to save raid settings.', 'error');
            }
        } catch (error) {
            console.error('Save raid error:', error);
            window.quickActions?.showToast('An error occurred while saving raid settings.', 'error');
        }
    }

    /**
     * Create a new mod tag
     */
    async function createTag() {
        const token = document.querySelector('input[name="__RequestVerificationToken"]').value;
        const guildId = window.moderationData.guildId;

        const tagName = document.getElementById('new-tag-name').value.trim();
        const tagCategory = parseInt(document.getElementById('new-tag-color').value);

        if (!tagName) {
            window.quickActions?.showToast('Please enter a tag name.', 'error');
            return;
        }

        // Map category to color
        const colorMap = {
            0: '#7a7876', // Default/Neutral
            1: '#10b981', // Positive/Success
            2: '#ef4444', // Negative/Danger
            3: '#06b6d4'  // Neutral/Info
        };

        const request = {
            guildId: guildId,
            name: tagName,
            color: colorMap[tagCategory],
            category: tagCategory,
            description: null
        };

        try {
            const response = await fetch(`?handler=CreateTag&guildId=${guildId}`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token
                },
                body: JSON.stringify(request)
            });

            const data = await response.json();

            if (response.ok && data.success) {
                window.quickActions?.showToast(data.message, 'success');

                // Clear form
                document.getElementById('new-tag-name').value = '';
                document.getElementById('new-tag-color').value = '0';

                // Reload page to show new tag
                setTimeout(() => window.location.reload(), 1000);
            } else {
                window.quickActions?.showToast(data.message || 'Failed to create tag.', 'error');
            }
        } catch (error) {
            console.error('Create tag error:', error);
            window.quickActions?.showToast('An error occurred while creating tag.', 'error');
        }
    }

    /**
     * Delete a mod tag
     * @param {string} tagName - The name of the tag to delete
     */
    async function deleteTag(tagName) {
        if (!confirm(`Are you sure you want to delete the tag "${tagName}"? This will remove it from all users.`)) {
            return;
        }

        const token = document.querySelector('input[name="__RequestVerificationToken"]').value;
        const guildId = window.moderationData.guildId;

        try {
            const response = await fetch(`?handler=DeleteTag&guildId=${guildId}&tagName=${encodeURIComponent(tagName)}`, {
                method: 'POST',
                headers: {
                    'RequestVerificationToken': token
                }
            });

            const data = await response.json();

            if (response.ok && data.success) {
                window.quickActions?.showToast(data.message, 'success');

                // Remove the tag from the list
                const tagElement = document.querySelector(`[data-tag-name="${tagName}"]`);
                if (tagElement) {
                    tagElement.remove();
                }
            } else {
                window.quickActions?.showToast(data.message || 'Failed to delete tag.', 'error');
            }
        } catch (error) {
            console.error('Delete tag error:', error);
            window.quickActions?.showToast('An error occurred while deleting tag.', 'error');
        }
    }

    /**
     * Show the import templates modal
     */
    function showImportTemplatesModal() {
        // For now, just alert. In a full implementation, would show a modal with checkboxes for template selection
        const templateNames = ['Spammer', 'Toxic User', 'New Account', 'Suspected Bot', 'Watch List'];
        const selected = prompt('Enter template names to import (comma-separated):\n' + templateNames.join(', '));

        if (selected) {
            const names = selected.split(',').map(n => n.trim()).filter(n => n.length > 0);
            importTemplates(names);
        }
    }

    /**
     * Import template tags
     * @param {string[]} templateNames - Array of template names to import
     */
    async function importTemplates(templateNames) {
        const token = document.querySelector('input[name="__RequestVerificationToken"]').value;
        const guildId = window.moderationData.guildId;

        try {
            const response = await fetch(`?handler=ImportTemplates&guildId=${guildId}`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token
                },
                body: JSON.stringify(templateNames)
            });

            const data = await response.json();

            if (response.ok && data.success) {
                window.quickActions?.showToast(data.message, 'success');

                // Reload page to show new tags
                setTimeout(() => window.location.reload(), 1000);
            } else {
                window.quickActions?.showToast(data.message || 'Failed to import templates.', 'error');
            }
        } catch (error) {
            console.error('Import templates error:', error);
            window.quickActions?.showToast('An error occurred while importing templates.', 'error');
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

        console.log('Moderation settings initialized');
    }

    // Expose public API
    window.moderationSettings = {
        switchTab,
        setMode,
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
