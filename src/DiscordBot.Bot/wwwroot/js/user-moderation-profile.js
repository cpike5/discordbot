/**
 * User Moderation Profile Page
 * Handles tab switching, tag management, and note operations
 */

(function () {
    'use strict';

    // Global state
    let guildId = null;
    let userId = null;
    let currentUserId = null;

    /**
     * Initialize the page
     */
    function init() {
        // Get IDs from global scope (set by Razor page)
        guildId = window.moderationGuildId;
        userId = window.moderationUserId;
        currentUserId = window.currentUserId;

        // Close dropdown when clicking outside
        document.addEventListener('click', function (event) {
            const dropdown = document.getElementById('tagDropdown');
            const btn = document.getElementById('addTagBtn');
            if (dropdown && btn && !btn.contains(event.target) && !dropdown.contains(event.target)) {
                dropdown.classList.add('hidden');
            }
        });

        // Format timestamps on page load
        formatTimestamps();
    }

    /**
     * Switch between tabs (cases, notes, flags)
     */
    window.switchTab = function (tabId) {
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
        const selectedTab = document.getElementById('tab-' + tabId);
        if (selectedTab) {
            selectedTab.classList.remove('hidden');
        }
    };

    /**
     * Toggle the tag dropdown
     */
    window.toggleTagDropdown = function () {
        const dropdown = document.getElementById('tagDropdown');
        if (dropdown) {
            dropdown.classList.toggle('hidden');
        }
    };

    /**
     * Add a tag to the user
     */
    window.addTag = async function (tagName) {
        // Close dropdown first
        const dropdown = document.getElementById('tagDropdown');
        if (dropdown) {
            dropdown.classList.add('hidden');
        }

        try {
            // Convert currentUserId to number if it's a string (Discord snowflakes need proper handling)
            const appliedById = typeof currentUserId === 'string' ? currentUserId : String(currentUserId);

            const response = await fetch(`/api/guilds/${guildId}/users/${userId}/tags/${encodeURIComponent(tagName)}`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({
                    appliedById: appliedById
                })
            });

            if (response.ok) {
                // Add the tag to the UI dynamically instead of reloading
                const tag = await response.json();
                const tagsContainer = document.getElementById('userTagsContainer');
                if (tagsContainer) {
                    const tagHtml = `
                        <span class="user-tag user-tag-removable" data-tag-name="${tagName}" onclick="removeTag('${tagName}')">
                            ${tagName}
                            <span class="user-tag-remove" title="Remove tag">
                                <svg fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
                                </svg>
                            </span>
                        </span>
                    `;
                    tagsContainer.insertAdjacentHTML('beforeend', tagHtml);
                }

                // Remove the tag from the dropdown (already applied)
                const dropdownItem = document.querySelector(`#tagDropdown [data-tag-name="${tagName}"]`);
                if (dropdownItem) {
                    dropdownItem.remove();
                }

                if (typeof ToastManager !== 'undefined') {
                    ToastManager.show('success', `Tag "${tagName}" added successfully`);
                }
            } else {
                // Try to parse JSON, but handle non-JSON responses gracefully
                let errorMessage = `HTTP ${response.status}: ${response.statusText}`;
                try {
                    const error = await response.json();
                    errorMessage = error.message || error.detail || errorMessage;
                } catch (parseError) {
                    // Response wasn't JSON, use the status text
                    console.error('Response was not JSON:', parseError);
                }

                if (typeof ToastManager !== 'undefined') {
                    ToastManager.show('error', `Failed to add tag: ${errorMessage}`);
                } else {
                    alert(`Failed to add tag: ${errorMessage}`);
                }
            }
        } catch (error) {
            console.error('Error adding tag:', error);
            if (typeof ToastManager !== 'undefined') {
                ToastManager.show('error', 'Failed to add tag. Please try again.');
            } else {
                alert('Failed to add tag. Please try again.');
            }
        }
    };

    /**
     * Remove a tag from the user
     */
    window.removeTag = async function (tagName) {
        if (!confirm(`Remove tag "${tagName}" from this user?`)) {
            return;
        }

        try {
            const response = await fetch(`/api/guilds/${guildId}/users/${userId}/tags/${encodeURIComponent(tagName)}`, {
                method: 'DELETE'
            });

            if (response.ok || response.status === 204) {
                // Remove the tag element from DOM
                const tagElement = document.querySelector(`[data-tag-name="${tagName}"]`);
                if (tagElement) {
                    tagElement.remove();
                }

                if (typeof ToastManager !== 'undefined') {
                    ToastManager.show('success', `Tag "${tagName}" removed`);
                }
            } else {
                let errorMessage = `HTTP ${response.status}: ${response.statusText}`;
                try {
                    const error = await response.json();
                    errorMessage = error.message || error.detail || errorMessage;
                } catch (parseError) {
                    console.error('Response was not JSON:', parseError);
                }

                if (typeof ToastManager !== 'undefined') {
                    ToastManager.show('error', `Failed to remove tag: ${errorMessage}`);
                } else {
                    alert(`Failed to remove tag: ${errorMessage}`);
                }
            }
        } catch (error) {
            console.error('Error removing tag:', error);
            if (typeof ToastManager !== 'undefined') {
                ToastManager.show('error', 'Failed to remove tag. Please try again.');
            } else {
                alert('Failed to remove tag. Please try again.');
            }
        }
    };

    /**
     * Add a new note
     */
    window.addNote = async function () {
        const noteTextarea = document.getElementById('newNoteText');
        const content = noteTextarea.value.trim();

        if (!content) {
            alert('Please enter a note');
            return;
        }

        try {
            const response = await fetch(`/api/guilds/${guildId}/users/${userId}/notes`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({
                    guildId: guildId,
                    targetUserId: userId,
                    authorUserId: currentUserId,
                    content: content
                })
            });

            if (response.ok) {
                // Reload the page to show the new note
                window.location.reload();
            } else {
                const error = await response.json();
                alert(`Failed to add note: ${error.message || 'Unknown error'}`);
            }
        } catch (error) {
            console.error('Error adding note:', error);
            alert('Failed to add note. Please try again.');
        }
    };

    /**
     * Delete a note
     */
    window.deleteNote = async function (noteId) {
        if (!confirm('Are you sure you want to delete this note?')) {
            return;
        }

        try {
            const response = await fetch(`/api/guilds/${guildId}/users/${userId}/notes/${noteId}`, {
                method: 'DELETE'
            });

            if (response.ok || response.status === 204) {
                // Remove the note element from DOM
                const noteElement = document.querySelector(`[data-note-id="${noteId}"]`);
                if (noteElement) {
                    noteElement.remove();
                }
            } else {
                const error = await response.json();
                alert(`Failed to delete note: ${error.message || 'Unknown error'}`);
            }
        } catch (error) {
            console.error('Error deleting note:', error);
            alert('Failed to delete note. Please try again.');
        }
    };

    /**
     * Format all timestamps on the page to local time
     */
    function formatTimestamps() {
        const elements = document.querySelectorAll('[data-utc]');
        elements.forEach(element => {
            const utcString = element.getAttribute('data-utc');
            const format = element.getAttribute('data-format') || 'date';

            if (!utcString) return;

            try {
                const date = new Date(utcString);
                if (isNaN(date.getTime())) return;

                if (format === 'date') {
                    // Format as date only
                    element.textContent = date.toLocaleDateString(undefined, {
                        year: 'numeric',
                        month: 'short',
                        day: 'numeric'
                    });
                } else if (format === 'datetime') {
                    // Format as date and time
                    element.textContent = date.toLocaleString(undefined, {
                        year: 'numeric',
                        month: 'short',
                        day: 'numeric',
                        hour: '2-digit',
                        minute: '2-digit'
                    });
                } else if (format === 'relative') {
                    // Format as relative time
                    element.textContent = formatRelativeTime(date);
                } else {
                    // Default to date format
                    element.textContent = date.toLocaleDateString(undefined, {
                        year: 'numeric',
                        month: 'short',
                        day: 'numeric'
                    });
                }
            } catch (error) {
                console.error('Error formatting timestamp:', error);
            }
        });
    }

    /**
     * Format a date as relative time (e.g., "2 hours ago")
     */
    function formatRelativeTime(date) {
        const now = new Date();
        const diffMs = now - date;
        const diffSec = Math.floor(diffMs / 1000);
        const diffMin = Math.floor(diffSec / 60);
        const diffHour = Math.floor(diffMin / 60);
        const diffDay = Math.floor(diffHour / 24);
        const diffWeek = Math.floor(diffDay / 7);
        const diffMonth = Math.floor(diffDay / 30);
        const diffYear = Math.floor(diffDay / 365);

        if (diffSec < 60) {
            return 'Just now';
        } else if (diffMin < 60) {
            return `${diffMin} minute${diffMin === 1 ? '' : 's'} ago`;
        } else if (diffHour < 24) {
            return `${diffHour} hour${diffHour === 1 ? '' : 's'} ago`;
        } else if (diffDay < 7) {
            return `${diffDay} day${diffDay === 1 ? '' : 's'} ago`;
        } else if (diffWeek < 4) {
            return `${diffWeek} week${diffWeek === 1 ? '' : 's'} ago`;
        } else if (diffMonth < 12) {
            return `${diffMonth} month${diffMonth === 1 ? '' : 's'} ago`;
        } else {
            return `${diffYear} year${diffYear === 1 ? '' : 's'} ago`;
        }
    }

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
