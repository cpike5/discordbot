/**
 * Notification History Page JavaScript
 * Handles bulk selection, bulk actions, and individual notification actions.
 */
(function() {
    'use strict';

    let selectedIds = new Set();

    /**
     * Initialize the notification history page functionality.
     */
    function init() {
        initializeCheckboxes();
        initializeBulkActions();
        initializeRowActions();
    }

    /**
     * Initialize checkbox selection functionality.
     */
    function initializeCheckboxes() {
        const selectAllCheckbox = document.getElementById('selectAll');
        const rowCheckboxes = document.querySelectorAll('.notification-checkbox');

        if (selectAllCheckbox) {
            selectAllCheckbox.addEventListener('change', function() {
                rowCheckboxes.forEach(cb => {
                    cb.checked = this.checked;
                    updateSelection(cb.value, this.checked);
                });
                updateBulkActionsVisibility();
            });
        }

        rowCheckboxes.forEach(cb => {
            cb.addEventListener('change', function() {
                updateSelection(this.value, this.checked);
                updateSelectAllState();
                updateBulkActionsVisibility();
            });
        });
    }

    /**
     * Update the selection set.
     * @param {string} id - The notification ID.
     * @param {boolean} isSelected - Whether the notification is selected.
     */
    function updateSelection(id, isSelected) {
        if (isSelected) {
            selectedIds.add(id);
        } else {
            selectedIds.delete(id);
        }
    }

    /**
     * Update the select all checkbox state based on individual selections.
     */
    function updateSelectAllState() {
        const selectAllCheckbox = document.getElementById('selectAll');
        const rowCheckboxes = document.querySelectorAll('.notification-checkbox');
        const checkedCount = document.querySelectorAll('.notification-checkbox:checked').length;

        if (selectAllCheckbox) {
            selectAllCheckbox.checked = checkedCount === rowCheckboxes.length && checkedCount > 0;
            selectAllCheckbox.indeterminate = checkedCount > 0 && checkedCount < rowCheckboxes.length;
        }
    }

    /**
     * Update the visibility of bulk action buttons.
     */
    function updateBulkActionsVisibility() {
        const bulkActions = document.getElementById('bulkActions');
        const selectedCount = document.getElementById('selectedCount');

        if (bulkActions && selectedCount) {
            bulkActions.classList.toggle('hidden', selectedIds.size === 0);
            selectedCount.textContent = selectedIds.size;
        }
    }

    /**
     * Initialize bulk action button handlers.
     */
    function initializeBulkActions() {
        const markSelectedReadBtn = document.getElementById('markSelectedRead');
        const deleteSelectedBtn = document.getElementById('deleteSelected');
        const markAllReadBtn = document.getElementById('markAllRead');

        if (markSelectedReadBtn) {
            markSelectedReadBtn.addEventListener('click', function() {
                if (selectedIds.size === 0) return;
                bulkAction('/api/notifications/mark-read', Array.from(selectedIds), 'marked as read');
            });
        }

        if (deleteSelectedBtn) {
            deleteSelectedBtn.addEventListener('click', function() {
                if (selectedIds.size === 0) return;
                if (confirm(`Delete ${selectedIds.size} notification(s)? This cannot be undone.`)) {
                    bulkAction('/api/notifications/delete', Array.from(selectedIds), 'deleted');
                }
            });
        }

        if (markAllReadBtn) {
            markAllReadBtn.addEventListener('click', async function() {
                if (confirm('Mark all notifications as read?')) {
                    try {
                        const response = await fetch('/api/notifications/mark-all-read', {
                            method: 'POST',
                            headers: {
                                'Content-Type': 'application/json',
                                'RequestVerificationToken': getAntiForgeryToken()
                            }
                        });

                        if (response.ok) {
                            location.reload();
                        } else {
                            const errorText = await response.text();
                            console.error('Failed to mark all as read:', errorText);
                            alert('Failed to mark all notifications as read. Please try again.');
                        }
                    } catch (error) {
                        console.error('Error marking all as read:', error);
                        alert('An error occurred. Please try again.');
                    }
                }
            });
        }
    }

    /**
     * Perform a bulk action on selected notifications.
     * @param {string} url - The API endpoint URL.
     * @param {string[]} ids - The notification IDs to act on.
     * @param {string} actionName - The action description for messages.
     */
    async function bulkAction(url, ids, actionName) {
        try {
            const response = await fetch(url, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': getAntiForgeryToken()
                },
                body: JSON.stringify(ids)
            });

            if (response.ok) {
                location.reload();
            } else {
                const errorText = await response.text();
                console.error(`Failed to ${actionName}:`, errorText);
                alert(`Failed to ${actionName} notifications. Please try again.`);
            }
        } catch (error) {
            console.error('Bulk action failed:', error);
            alert(`Failed to ${actionName} notifications. Please try again.`);
        }
    }

    /**
     * Initialize individual row action handlers.
     */
    function initializeRowActions() {
        // Toggle read/unread
        document.querySelectorAll('[data-action="toggle-read"]').forEach(btn => {
            btn.addEventListener('click', async function() {
                const id = this.dataset.id;
                const isRead = this.dataset.isRead === 'true';
                const url = `/api/notifications/${id}/${isRead ? 'unread' : 'read'}`;

                try {
                    const response = await fetch(url, {
                        method: 'POST',
                        headers: {
                            'RequestVerificationToken': getAntiForgeryToken()
                        }
                    });

                    if (response.ok) {
                        location.reload();
                    } else {
                        console.error('Failed to toggle read status');
                        alert('Failed to update notification. Please try again.');
                    }
                } catch (error) {
                    console.error('Error toggling read status:', error);
                    alert('An error occurred. Please try again.');
                }
            });
        });

        // Delete single notification
        document.querySelectorAll('[data-action="delete"]').forEach(btn => {
            btn.addEventListener('click', async function() {
                if (!confirm('Delete this notification? This cannot be undone.')) return;

                const id = this.dataset.id;

                try {
                    const response = await fetch(`/api/notifications/${id}`, {
                        method: 'DELETE',
                        headers: {
                            'RequestVerificationToken': getAntiForgeryToken()
                        }
                    });

                    if (response.ok) {
                        location.reload();
                    } else {
                        console.error('Failed to delete notification');
                        alert('Failed to delete notification. Please try again.');
                    }
                } catch (error) {
                    console.error('Error deleting notification:', error);
                    alert('An error occurred. Please try again.');
                }
            });
        });
    }

    /**
     * Get the anti-forgery token for AJAX requests.
     * @returns {string} The anti-forgery token value.
     */
    function getAntiForgeryToken() {
        const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        return tokenInput ? tokenInput.value : '';
    }

    // Initialize on DOM ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
