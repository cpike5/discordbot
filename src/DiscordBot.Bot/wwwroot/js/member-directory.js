/**
 * Member Directory JavaScript
 * Handles filter panel, bulk selection, and member detail modal
 */

(function () {
    'use strict';

    // Get guild ID from page
    const guildId = window.memberDirectoryGuildId;

    // DOM element references
    let filterToggle, filterContent, filterChevron;
    let roleMultiSelectToggle, roleMultiSelectDropdown;
    let selectAllCheckbox, memberCheckboxes, bulkActionsToolbar, selectedCountSpan;
    let memberDetailModal, memberDetailLoading, memberDetailError, memberDetailContent;
    let lastFocusedElement = null;

    /**
     * Initialize the member directory functionality
     */
    function init() {
        // Cache DOM elements
        filterToggle = document.getElementById('filterToggle');
        filterContent = document.getElementById('filterContent');
        filterChevron = document.getElementById('filterChevron');
        roleMultiSelectToggle = document.getElementById('roleMultiSelectToggle');
        roleMultiSelectDropdown = document.getElementById('roleMultiSelectDropdown');
        selectAllCheckbox = document.getElementById('selectAll');
        bulkActionsToolbar = document.getElementById('bulkActionsToolbar');
        selectedCountSpan = document.getElementById('selectedCount');
        memberDetailModal = document.getElementById('memberDetailModal');
        memberDetailLoading = document.getElementById('memberDetailLoading');
        memberDetailError = document.getElementById('memberDetailError');
        memberDetailContent = document.getElementById('memberDetailContent');

        // Set up event listeners
        setupFilterPanel();
        setupRoleMultiSelect();
        setupBulkSelection();
        setupModalKeyboardHandling();
    }

    /**
     * Set up filter panel expand/collapse
     */
    function setupFilterPanel() {
        if (!filterToggle || !filterContent || !filterChevron) return;

        filterToggle.addEventListener('click', function () {
            const isExpanded = this.getAttribute('aria-expanded') === 'true';
            this.setAttribute('aria-expanded', !isExpanded);
            filterContent.classList.toggle('hidden');
            filterChevron.classList.toggle('rotate-180');
        });
    }

    /**
     * Set up role multi-select dropdown
     */
    function setupRoleMultiSelect() {
        if (!roleMultiSelectToggle || !roleMultiSelectDropdown) return;

        // Toggle dropdown
        roleMultiSelectToggle.addEventListener('click', function (e) {
            e.preventDefault();
            const isExpanded = this.getAttribute('aria-expanded') === 'true';
            this.setAttribute('aria-expanded', !isExpanded);
            roleMultiSelectDropdown.classList.toggle('hidden');
        });

        // Close dropdown when clicking outside
        document.addEventListener('click', function (e) {
            if (!roleMultiSelectToggle.contains(e.target) && !roleMultiSelectDropdown.contains(e.target)) {
                roleMultiSelectToggle.setAttribute('aria-expanded', 'false');
                roleMultiSelectDropdown.classList.add('hidden');
            }
        });

        // Update selected text when checkboxes change
        const roleCheckboxes = roleMultiSelectDropdown.querySelectorAll('.role-checkbox');
        roleCheckboxes.forEach(function (checkbox) {
            checkbox.addEventListener('change', updateRoleSelectedText);
        });
    }

    /**
     * Update the role multi-select button text
     */
    function updateRoleSelectedText() {
        const roleCheckboxes = roleMultiSelectDropdown.querySelectorAll('.role-checkbox:checked');
        const selectedText = document.getElementById('roleSelectedText');

        if (roleCheckboxes.length === 0) {
            selectedText.textContent = 'All roles';
        } else if (roleCheckboxes.length === 1) {
            selectedText.textContent = '1 role selected';
        } else {
            selectedText.textContent = roleCheckboxes.length + ' roles selected';
        }
    }

    /**
     * Set up bulk selection functionality
     */
    function setupBulkSelection() {
        // Select all checkbox
        if (selectAllCheckbox) {
            selectAllCheckbox.addEventListener('change', function () {
                const checkboxes = document.querySelectorAll('.member-checkbox');
                checkboxes.forEach(function (cb) {
                    cb.checked = selectAllCheckbox.checked;
                });
                updateBulkSelection();
            });
        }

        // Individual checkbox changes
        document.querySelectorAll('.member-checkbox').forEach(function (cb) {
            cb.addEventListener('change', updateBulkSelection);
        });
    }

    /**
     * Update bulk selection state and toolbar visibility
     */
    function updateBulkSelection() {
        memberCheckboxes = document.querySelectorAll('.member-checkbox');
        const checkedCheckboxes = document.querySelectorAll('.member-checkbox:checked');
        const checkedCount = checkedCheckboxes.length;

        if (bulkActionsToolbar && selectedCountSpan) {
            if (checkedCount > 0) {
                bulkActionsToolbar.classList.remove('hidden');
                selectedCountSpan.textContent = checkedCount;
            } else {
                bulkActionsToolbar.classList.add('hidden');
            }
        }

        // Update select-all checkbox state (indeterminate)
        if (selectAllCheckbox && memberCheckboxes.length > 0) {
            selectAllCheckbox.checked = checkedCount === memberCheckboxes.length;
            selectAllCheckbox.indeterminate = checkedCount > 0 && checkedCount < memberCheckboxes.length;
        }
    }

    /**
     * Deselect all members
     */
    window.deselectAll = function () {
        document.querySelectorAll('.member-checkbox').forEach(function (cb) {
            cb.checked = false;
        });
        if (selectAllCheckbox) {
            selectAllCheckbox.checked = false;
            selectAllCheckbox.indeterminate = false;
        }
        updateBulkSelection();
    };

    /**
     * Export selected members
     */
    window.exportSelected = function () {
        const selectedCheckboxes = document.querySelectorAll('.member-checkbox:checked');
        const selectedIds = Array.from(selectedCheckboxes).map(function (cb) {
            return cb.value;
        });

        if (selectedIds.length === 0) {
            if (typeof showToast === 'function') {
                showToast('No members selected', 'warning');
            }
            return;
        }

        // Build export URL with selected IDs
        const exportUrl = '/api/guilds/' + guildId + '/members/export?userIds=' + selectedIds.join(',');

        // Trigger download
        window.location.href = exportUrl;

        if (typeof showToast === 'function') {
            showToast('Exporting ' + selectedIds.length + ' member(s)...', 'info');
        }
    };

    /**
     * View member details in modal
     * @param {string} userId - The user ID to view
     */
    window.viewMemberDetails = async function (userId) {
        if (!memberDetailModal) return;

        // Store the button that triggered the modal for focus return
        lastFocusedElement = document.activeElement;

        // Show modal with loading state
        memberDetailModal.classList.remove('hidden');
        document.body.style.overflow = 'hidden';

        memberDetailLoading.classList.remove('hidden');
        memberDetailError.classList.add('hidden');
        memberDetailContent.classList.add('hidden');

        try {
            const response = await fetch('/api/guilds/' + guildId + '/members/' + userId);

            if (!response.ok) {
                throw new Error('Member not found');
            }

            const member = await response.json();
            populateMemberModal(member);

            memberDetailLoading.classList.add('hidden');
            memberDetailContent.classList.remove('hidden');

        } catch (error) {
            console.error('Failed to load member details:', error);
            memberDetailLoading.classList.add('hidden');
            memberDetailError.classList.remove('hidden');
        }

        // Focus first focusable element in modal
        const closeButton = memberDetailModal.querySelector('button[aria-label="Close modal"]');
        if (closeButton) {
            closeButton.focus();
        }
    };

    /**
     * Populate the member detail modal with data
     * @param {Object} member - The member data
     */
    function populateMemberModal(member) {
        // Avatar
        const avatarImg = document.getElementById('modalAvatar');
        const avatarPlaceholder = document.getElementById('modalAvatarPlaceholder');
        const avatarInitials = document.getElementById('modalAvatarInitials');

        if (member.avatarHash) {
            const ext = member.avatarHash.startsWith('a_') ? 'gif' : 'png';
            avatarImg.src = 'https://cdn.discordapp.com/avatars/' + member.userId + '/' + member.avatarHash + '.' + ext + '?size=160';
            avatarImg.alt = member.displayName;
            avatarImg.classList.remove('hidden');
            avatarPlaceholder.classList.add('hidden');
        } else {
            avatarImg.classList.add('hidden');
            avatarPlaceholder.classList.remove('hidden');
            avatarInitials.textContent = member.displayName.substring(0, 2).toUpperCase();
        }

        // Basic info
        document.getElementById('modalDisplayName').textContent = member.displayName;
        document.getElementById('modalUsername').textContent = '@' + member.username;
        document.getElementById('modalUserId').textContent = member.userId;
        document.getElementById('modalNickname').textContent = member.nickname || 'None';

        // Dates
        const joinDate = new Date(member.joinedAt);
        document.getElementById('modalJoinDate').textContent = formatDate(joinDate);
        document.getElementById('modalJoinAge').textContent = formatRelativeTime(joinDate);

        if (member.accountCreatedAt) {
            const accountDate = new Date(member.accountCreatedAt);
            document.getElementById('modalAccountCreated').textContent = formatDate(accountDate);
            document.getElementById('modalAccountAge').textContent = formatRelativeTime(accountDate);
        } else {
            document.getElementById('modalAccountCreated').textContent = 'Unknown';
            document.getElementById('modalAccountAge').textContent = '';
        }

        if (member.lastActiveAt) {
            const lastActiveDate = new Date(member.lastActiveAt);
            document.getElementById('modalLastActive').textContent = formatRelativeTime(lastActiveDate);
            document.getElementById('modalLastActiveExact').textContent = formatDateTime(lastActiveDate);
        } else {
            document.getElementById('modalLastActive').textContent = 'Never';
            document.getElementById('modalLastActiveExact').textContent = '';
        }

        // Roles
        const roleList = document.getElementById('modalRoleList');
        const noRoles = document.getElementById('modalNoRoles');
        const roleCount = document.getElementById('modalRoleCount');
        const roleCountStat = document.getElementById('modalRoleCountStat');

        roleList.innerHTML = '';

        if (member.roles && member.roles.length > 0) {
            noRoles.classList.add('hidden');
            roleCount.textContent = '(' + member.roles.length + ')';
            roleCountStat.textContent = member.roles.length;

            // Sort roles by position (highest first)
            const sortedRoles = [...member.roles].sort((a, b) => b.position - a.position);

            sortedRoles.forEach(function (role) {
                const colorHex = role.color > 0 ? '#' + role.color.toString(16).padStart(6, '0') : '#99aab5';
                const roleSpan = document.createElement('span');
                roleSpan.className = 'inline-flex items-center gap-2 px-3 py-1.5 rounded text-sm font-medium text-white';
                roleSpan.style.backgroundColor = colorHex;
                roleSpan.innerHTML = '<span class="w-2 h-2 rounded-full bg-white/30"></span>' + escapeHtml(role.name);
                roleList.appendChild(roleSpan);
            });
        } else {
            noRoles.classList.remove('hidden');
            roleCount.textContent = '(0)';
            roleCountStat.textContent = '0';
        }

        // Status
        const memberStatus = document.getElementById('modalMemberStatus');
        if (member.isActive) {
            memberStatus.textContent = 'Active';
            memberStatus.className = 'text-2xl font-bold text-success';
        } else {
            memberStatus.textContent = 'Inactive';
            memberStatus.className = 'text-2xl font-bold text-error';
        }
    }

    /**
     * Close the member detail modal
     */
    window.closeMemberModal = function () {
        if (!memberDetailModal) return;

        memberDetailModal.classList.add('hidden');
        document.body.style.overflow = '';

        // Return focus to the trigger element
        if (lastFocusedElement) {
            lastFocusedElement.focus();
            lastFocusedElement = null;
        }
    };

    /**
     * Copy user ID to clipboard
     */
    window.copyUserId = function () {
        const userId = document.getElementById('modalUserId').textContent;
        navigator.clipboard.writeText(userId).then(function () {
            if (typeof showToast === 'function') {
                showToast('User ID copied to clipboard', 'success');
            }
        }).catch(function () {
            if (typeof showToast === 'function') {
                showToast('Failed to copy user ID', 'error');
            }
        });
    };

    /**
     * Set up keyboard handling for modal
     */
    function setupModalKeyboardHandling() {
        document.addEventListener('keydown', function (e) {
            if (!memberDetailModal || memberDetailModal.classList.contains('hidden')) return;

            // Close on Escape
            if (e.key === 'Escape') {
                closeMemberModal();
                return;
            }

            // Focus trap
            if (e.key === 'Tab') {
                const focusableElements = memberDetailModal.querySelectorAll(
                    'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])'
                );
                const firstElement = focusableElements[0];
                const lastElement = focusableElements[focusableElements.length - 1];

                if (e.shiftKey && document.activeElement === firstElement) {
                    e.preventDefault();
                    lastElement.focus();
                } else if (!e.shiftKey && document.activeElement === lastElement) {
                    e.preventDefault();
                    firstElement.focus();
                }
            }
        });
    }

    /**
     * Format a date for display
     * @param {Date} date - The date to format
     * @returns {string} Formatted date string
     */
    function formatDate(date) {
        return date.toLocaleDateString(undefined, {
            year: 'numeric',
            month: 'short',
            day: 'numeric'
        });
    }

    /**
     * Format a date and time for display
     * @param {Date} date - The date to format
     * @returns {string} Formatted date/time string
     */
    function formatDateTime(date) {
        return date.toLocaleDateString(undefined, {
            year: 'numeric',
            month: 'short',
            day: 'numeric',
            hour: '2-digit',
            minute: '2-digit'
        });
    }

    /**
     * Format relative time (e.g., "2 hours ago")
     * @param {Date} date - The date to format
     * @returns {string} Relative time string
     */
    function formatRelativeTime(date) {
        const now = new Date();
        const diffMs = now - date;
        const diffSecs = Math.floor(diffMs / 1000);
        const diffMins = Math.floor(diffSecs / 60);
        const diffHours = Math.floor(diffMins / 60);
        const diffDays = Math.floor(diffHours / 24);
        const diffWeeks = Math.floor(diffDays / 7);
        const diffMonths = Math.floor(diffDays / 30);
        const diffYears = Math.floor(diffDays / 365);

        if (diffSecs < 60) return 'Just now';
        if (diffMins < 60) return diffMins + ' minute' + (diffMins === 1 ? '' : 's') + ' ago';
        if (diffHours < 24) return diffHours + ' hour' + (diffHours === 1 ? '' : 's') + ' ago';
        if (diffDays === 1) return 'Yesterday';
        if (diffDays < 7) return diffDays + ' day' + (diffDays === 1 ? '' : 's') + ' ago';
        if (diffWeeks < 4) return diffWeeks + ' week' + (diffWeeks === 1 ? '' : 's') + ' ago';
        if (diffMonths < 12) return diffMonths + ' month' + (diffMonths === 1 ? '' : 's') + ' ago';
        return diffYears + ' year' + (diffYears === 1 ? '' : 's') + ' ago';
    }

    /**
     * Escape HTML special characters
     * @param {string} text - The text to escape
     * @returns {string} Escaped text
     */
    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
