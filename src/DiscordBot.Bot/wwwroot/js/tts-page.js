/**
 * TTS Page Module
 * Handles AJAX form submissions for TTS message sending, settings updates, and message deletion
 */
(function() {
    'use strict';

    // Icon SVG templates for button states
    const icons = {
        save: '<svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" /></svg>',
        loading: '<svg class="w-4 h-4 animate-spin" fill="none" viewBox="0 0 24 24" stroke="currentColor"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path></svg>',
        success: '<svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>',
        error: '<svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" /></svg>',
        play: '<svg class="w-4 h-4" fill="currentColor" viewBox="0 0 24 24"><path d="M3 9v6h4l5 5V4L7 9H3zm13.5 3c0-1.77-1.02-3.29-2.5-4.03v8.05c1.48-.73 2.5-2.25 2.5-4.02zM14 3.23v2.06c2.89.86 5 3.54 5 6.71s-2.11 5.85-5 6.71v2.06c4.01-.91 7-4.49 7-8.77s-2.99-7.86-7-8.77z"/></svg>'
    };

    // Store original button states for reset
    const buttonOriginalStates = new WeakMap();

    /**
     * Build form data with proper checkbox handling
     * Checkboxes need special handling because unchecked boxes don't submit values
     * @param {HTMLFormElement} form - The form element
     * @returns {FormData} - FormData with correct checkbox values
     */
    function buildFormData(form) {
        const formData = new FormData();

        // Add the anti-forgery token
        const token = form.querySelector('input[name="__RequestVerificationToken"]');
        if (token) {
            formData.append('__RequestVerificationToken', token.value);
        }

        // Process all checkboxes - add their current state (true/false)
        const checkboxes = form.querySelectorAll('input[type="checkbox"]');
        checkboxes.forEach(checkbox => {
            if (checkbox.name && !checkbox.name.startsWith('__')) {
                formData.append(checkbox.name, checkbox.checked ? 'true' : 'false');
            }
        });

        // Process all other form inputs (text, number, select, textarea, range, etc.)
        const inputs = form.querySelectorAll('input:not([type="checkbox"]), select, textarea');
        inputs.forEach(input => {
            if (input.name && !input.name.startsWith('__')) {
                formData.append(input.name, input.value);
            }
        });

        return formData;
    }

    /**
     * Store the original state of a button for later reset
     * @param {HTMLButtonElement} button - The button element
     */
    function storeButtonState(button) {
        if (!button || buttonOriginalStates.has(button)) return;
        buttonOriginalStates.set(button, {
            innerHTML: button.innerHTML,
            disabled: button.disabled
        });
    }

    /**
     * Reset a button to its original state
     * @param {HTMLButtonElement} button - The button element
     */
    function resetButtonState(button) {
        if (!button) return;
        const original = buttonOriginalStates.get(button);
        if (original) {
            button.innerHTML = original.innerHTML;
            button.disabled = original.disabled;
        }
    }

    /**
     * Set button to loading state
     * @param {HTMLButtonElement} button - The button element
     * @param {string} loadingText - Optional loading text
     */
    function setButtonLoading(button, loadingText = 'Sending...') {
        if (!button) return;
        storeButtonState(button);
        button.disabled = true;
        button.innerHTML = `${icons.loading} ${loadingText}`;
    }

    /**
     * Set button to success state
     * @param {HTMLButtonElement} button - The button element
     * @param {string} successText - Optional success text
     * @param {boolean} autoReset - Whether to auto-reset after 2 seconds
     */
    function setButtonSuccess(button, successText = 'Sent!', autoReset = true) {
        if (!button) return;
        button.disabled = true;
        button.innerHTML = `${icons.success} ${successText}`;

        if (autoReset) {
            setTimeout(() => resetButtonState(button), 2000);
        }
    }

    /**
     * Set button to error state (allows retry)
     * @param {HTMLButtonElement} button - The button element
     * @param {string} errorText - Optional error text
     */
    function setButtonError(button, errorText = 'Failed - Retry') {
        if (!button) return;
        button.disabled = false; // Allow retry
        button.innerHTML = `${icons.error} ${errorText}`;

        // Reset after 3 seconds
        setTimeout(() => resetButtonState(button), 3000);
    }

    /**
     * Update stats cards with new data
     * @param {Object} stats - Stats data from server
     */
    function updateStats(stats) {
        if (!stats) return;

        // Update Messages Today
        const messagesToday = document.getElementById('statsMessagesToday');
        if (messagesToday && stats.messagesToday !== undefined) {
            messagesToday.textContent = stats.messagesToday;
        }

        // Update Total Playback
        const totalPlayback = document.getElementById('statsTotalPlayback');
        if (totalPlayback && stats.totalPlaybackFormatted) {
            totalPlayback.textContent = stats.totalPlaybackFormatted;
        }

        // Update Active Voices
        const activeVoices = document.getElementById('statsActiveVoices');
        if (activeVoices && stats.uniqueUsers !== undefined) {
            activeVoices.textContent = stats.uniqueUsers;
        }
    }

    /**
     * Add a new message to the recent messages list
     * @param {Object} messageData - Message data from server
     */
    function addRecentMessage(messageData) {
        if (!messageData) return;

        const messagesList = document.getElementById('recentMessagesList');
        if (!messagesList) return;

        // Check if empty state exists and remove it
        const emptyState = messagesList.querySelector('.p-8');
        if (emptyState) {
            emptyState.remove();
        }

        // Create message element
        const messageEl = document.createElement('div');
        messageEl.className = 'flex items-center gap-3 p-4 hover:bg-bg-hover transition-colors group';
        messageEl.dataset.messageId = messageData.id;

        // Extract initials from username (first 2 chars)
        const initials = messageData.username.substring(0, Math.min(2, messageData.username.length)).toUpperCase();

        messageEl.innerHTML = `
            <div class="w-9 h-9 rounded-full bg-accent-blue flex items-center justify-center text-white text-xs font-bold flex-shrink-0">
                ${initials}
            </div>
            <div class="flex-1 min-w-0">
                <div class="text-sm text-text-primary truncate">${escapeHtml(messageData.message)}</div>
                <div class="flex items-center gap-2 mt-1 text-xs text-text-tertiary">
                    <span class="preview-trigger" data-preview-type="user" data-user-id="${messageData.userId}" data-context-guild-id="${window.guildId}">${escapeHtml(messageData.username)}</span>
                    <span class="inline-flex items-center gap-1 px-2 py-0.5 bg-accent-blue-muted text-accent-blue rounded font-medium text-[0.65rem]">
                        ${escapeHtml(messageData.voice)}
                    </span>
                    <span>${escapeHtml(messageData.durationFormatted)}</span>
                </div>
            </div>
            <div class="flex items-center gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
                <button type="button"
                        disabled
                        title="Replay functionality coming soon"
                        class="p-2 rounded text-text-tertiary cursor-not-allowed opacity-50">
                    <svg class="w-4 h-4" fill="currentColor" viewBox="0 0 24 24">
                        <path d="M8 5v14l11-7z"/>
                    </svg>
                </button>
                <button type="button"
                        onclick="showDeleteModal('${messageData.id}', '${escapeHtml(messageData.message).replace(/'/g, "\\'")}')"
                        class="p-2 rounded text-error hover:bg-error/10 transition-colors"
                        title="Delete">
                    <svg class="w-4 h-4" fill="currentColor" viewBox="0 0 24 24">
                        <path d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                    </svg>
                </button>
            </div>
        `;

        // Prepend to list
        const container = messagesList.querySelector('.divide-y');
        if (container) {
            container.insertBefore(messageEl, container.firstChild);
        } else {
            // If no container exists, create one
            const newContainer = document.createElement('div');
            newContainer.className = 'divide-y divide-border-secondary';
            newContainer.appendChild(messageEl);
            messagesList.appendChild(newContainer);
        }

        // Update count badge
        const countBadge = document.querySelector('.inline-flex.items-center.justify-center.px-2\\.5.py-1.text-xs');
        if (countBadge) {
            const currentCount = parseInt(countBadge.textContent) || 0;
            countBadge.textContent = currentCount + 1;
        }
    }

    /**
     * Remove a message from the recent messages list
     * @param {string} messageId - The message ID to remove
     */
    function removeMessage(messageId) {
        const messageEl = document.querySelector(`[data-message-id="${messageId}"]`);
        if (messageEl) {
            messageEl.remove();

            // Update count badge
            const countBadge = document.querySelector('.inline-flex.items-center.justify-center.px-2\\.5.py-1.text-xs');
            if (countBadge) {
                const currentCount = parseInt(countBadge.textContent) || 0;
                countBadge.textContent = Math.max(0, currentCount - 1);
            }

            // Check if list is now empty
            const messagesList = document.getElementById('recentMessagesList');
            if (messagesList) {
                const container = messagesList.querySelector('.divide-y');
                if (!container || container.children.length === 0) {
                    // Show empty state
                    messagesList.innerHTML = `
                        <div class="p-8">
                            <div class="text-center space-y-3">
                                <div class="inline-flex items-center justify-center w-12 h-12 rounded-full bg-bg-tertiary text-text-tertiary">
                                    <svg class="w-6 h-6" fill="currentColor" viewBox="0 0 24 24">
                                        <path d="M20 2H4a2 2 0 0 0-2 2v18l4-4h14a2 2 0 0 0 2-2V4a2 2 0 0 0-2-2z"/>
                                    </svg>
                                </div>
                                <div>
                                    <h3 class="text-sm font-semibold text-text-primary">No Messages Yet</h3>
                                    <p class="mt-1 text-xs text-text-secondary">Send your first TTS message using the form above.</p>
                                </div>
                            </div>
                        </div>
                    `;
                }
            }
        }
    }

    /**
     * Escape HTML to prevent XSS
     * @param {string} text - Text to escape
     * @returns {string} - Escaped text
     */
    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    /**
     * Send TTS message via AJAX
     * @param {HTMLFormElement} form - The form element
     */
    async function sendMessage(form) {
        const formData = buildFormData(form);
        const submitButton = form.querySelector('button[type="submit"]');

        // Get guild ID from window (as string to preserve precision)
        const guildId = window.guildId;
        if (!guildId) {
            window.quickActions?.showToast('Guild ID not found.', 'error');
            return;
        }

        // Show loading state
        setButtonLoading(submitButton, 'Sending...');

        try {
            const response = await fetch(`?handler=SendMessage&guildId=${guildId}`, {
                method: 'POST',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: formData
            });

            const data = await response.json();

            if (response.ok && data.success) {
                // Show success state
                setButtonSuccess(submitButton, 'Sent!', true);

                // Show toast
                window.quickActions?.showToast(data.message, 'success');

                // Update stats if provided
                if (data.stats) {
                    updateStats(data.stats);
                }

                // Add new message to recent messages if provided
                if (data.recentMessage) {
                    addRecentMessage(data.recentMessage);
                }

                // Clear the form
                form.reset();

                // Reset character counter
                const counter = document.getElementById('charCounter');
                const textarea = document.getElementById('messageInput');
                if (counter && textarea) {
                    counter.textContent = `0/${textarea.maxLength}`;
                    counter.classList.remove('warning', 'error');
                }
            } else {
                // Show error state
                setButtonError(submitButton, 'Send Failed');

                // Show toast
                window.quickActions?.showToast(data.message || 'Failed to send message.', 'error');
            }
        } catch (error) {
            console.error('Send message error:', error);

            // Show error state
            setButtonError(submitButton, 'Send Failed');

            // Show toast
            window.quickActions?.showToast('An error occurred while sending the message.', 'error');
        }
    }

    /**
     * Update TTS settings via AJAX
     * @param {HTMLFormElement} form - The form element
     */
    async function updateSettings(form) {
        const formData = buildFormData(form);
        const submitButton = form.querySelector('button[type="submit"]');

        // Get guild ID from window (as string to preserve precision)
        const guildId = window.guildId;
        if (!guildId) {
            window.quickActions?.showToast('Guild ID not found.', 'error');
            return;
        }

        // Show loading state
        setButtonLoading(submitButton, 'Saving...');

        try {
            const response = await fetch(`?handler=UpdateSettings&guildId=${guildId}`, {
                method: 'POST',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: formData
            });

            const data = await response.json();

            if (response.ok && data.success) {
                // Show success state
                setButtonSuccess(submitButton, 'Saved!', true);

                // Show toast
                window.quickActions?.showToast(data.message, 'success');
            } else {
                // Show error state
                setButtonError(submitButton, 'Save Failed');

                // Show toast
                window.quickActions?.showToast(data.message || 'Failed to update settings.', 'error');
            }
        } catch (error) {
            console.error('Update settings error:', error);

            // Show error state
            setButtonError(submitButton, 'Save Failed');

            // Show toast
            window.quickActions?.showToast('An error occurred while updating settings.', 'error');
        }
    }

    /**
     * Delete TTS message via AJAX
     * @param {string} messageId - The message ID to delete
     */
    async function deleteMessage(messageId) {
        // Get guild ID from window (as string to preserve precision)
        const guildId = window.guildId;
        if (!guildId) {
            window.quickActions?.showToast('Guild ID not found.', 'error');
            return;
        }

        // Get anti-forgery token
        const token = document.querySelector('input[name="__RequestVerificationToken"]');
        if (!token) {
            window.quickActions?.showToast('Security token not found.', 'error');
            return;
        }

        // Create form data
        const formData = new FormData();
        formData.append('__RequestVerificationToken', token.value);
        formData.append('messageId', messageId);

        try {
            const response = await fetch(`?handler=DeleteMessage&guildId=${guildId}`, {
                method: 'POST',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: formData
            });

            const data = await response.json();

            if (response.ok && data.success) {
                // Show toast
                window.quickActions?.showToast(data.message, 'success');

                // Remove message from list
                if (data.messageId) {
                    removeMessage(data.messageId);
                }

                // Hide modal if it exists
                if (typeof hideDeleteModal === 'function') {
                    hideDeleteModal();
                }
            } else {
                // Show toast
                window.quickActions?.showToast(data.message || 'Failed to delete message.', 'error');
            }
        } catch (error) {
            console.error('Delete message error:', error);

            // Show toast
            window.quickActions?.showToast('An error occurred while deleting the message.', 'error');
        }
    }

    // Expose public API
    window.ttsPage = {
        sendMessage,
        updateSettings,
        deleteMessage
    };
})();
