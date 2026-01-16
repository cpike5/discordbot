/**
 * Notification Bell Module
 * Manages the notification bell dropdown in the global navbar.
 * Integrates with DashboardHub for real-time notification updates.
 */
const NotificationBell = (function () {
    'use strict';

    // State
    let notifications = [];
    let isOpen = false;
    let isLoading = false;
    let hasLoaded = false;

    // DOM elements (cached after init)
    let bellButton = null;
    let badge = null;
    let dropdown = null;
    let notificationList = null;
    let announcer = null;

    // Notification type to icon class mapping
    const typeIconClasses = {
        // NotificationType enum values
        1: 'notification-icon-critical', // PerformanceAlert - use severity to determine
        2: 'notification-icon-success',   // BotStatus
        3: 'notification-icon-guild',     // GuildEvent
        4: 'notification-icon-command'    // CommandError
    };

    // Severity to icon class mapping (for PerformanceAlert)
    const severityIconClasses = {
        0: 'notification-icon-info',     // Info
        1: 'notification-icon-warning',  // Warning
        2: 'notification-icon-critical'  // Critical
    };

    // Notification type SVG icons
    const typeIcons = {
        // PerformanceAlert (triangle exclamation)
        1: '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />',
        // BotStatus (power icon)
        2: '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" />',
        // GuildEvent (users icon)
        3: '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z" />',
        // CommandError (code icon)
        4: '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 20l4-16m4 4l4 4-4 4M6 16l-4-4 4-4" />'
    };

    /**
     * Initializes the notification bell module.
     * Called automatically when the DashboardHub connects.
     */
    function init() {
        // Cache DOM elements
        bellButton = document.getElementById('notificationBellButton');
        badge = document.getElementById('notificationBadge');
        dropdown = document.getElementById('notificationDropdown');
        notificationList = document.getElementById('notificationList');
        announcer = document.getElementById('notificationAnnouncer');

        if (!bellButton || !dropdown) {
            console.warn('[NotificationBell] Required DOM elements not found');
            return;
        }

        // Register SignalR event handlers
        registerSignalRHandlers();

        // Setup keyboard handlers
        setupKeyboardHandlers();

        // Setup click outside handler
        document.addEventListener('click', handleClickOutside);

        // Fetch initial notification summary when DashboardHub connects
        if (DashboardHub.isConnected()) {
            fetchInitialSummary();
        } else {
            // Wait for connection
            DashboardHub.on('connected', fetchInitialSummary);
        }

        console.log('[NotificationBell] Initialized');
    }

    /**
     * Registers SignalR event handlers for real-time notification updates.
     */
    function registerSignalRHandlers() {
        // New notification received
        DashboardHub.on('OnNotificationReceived', onNotificationReceived);

        // Notification count changed (from other tabs/sessions)
        DashboardHub.on('OnNotificationCountChanged', onNotificationCountChanged);

        // Single notification marked as read (from other tabs/sessions)
        DashboardHub.on('OnNotificationMarkedRead', onNotificationMarkedRead);

        // All notifications marked as read (from other tabs/sessions)
        DashboardHub.on('OnAllNotificationsRead', onAllNotificationsRead);
    }

    /**
     * Sets up keyboard handlers for accessibility.
     */
    function setupKeyboardHandlers() {
        document.addEventListener('keydown', (event) => {
            if (event.key === 'Escape' && isOpen) {
                close();
                bellButton?.focus();
            }
        });

        // Arrow key navigation within dropdown
        dropdown?.addEventListener('keydown', (event) => {
            if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
                event.preventDefault();
                navigateItems(event.key === 'ArrowDown' ? 1 : -1);
            }
        });
    }

    /**
     * Navigates between notification items using arrow keys.
     * @param {number} direction - 1 for down, -1 for up
     */
    function navigateItems(direction) {
        const items = notificationList?.querySelectorAll('.notification-item');
        if (!items || items.length === 0) return;

        const focused = document.activeElement;
        const currentIndex = Array.from(items).indexOf(focused);
        let nextIndex = currentIndex + direction;

        if (nextIndex < 0) nextIndex = items.length - 1;
        if (nextIndex >= items.length) nextIndex = 0;

        items[nextIndex]?.focus();
    }

    /**
     * Handles clicks outside the dropdown to close it.
     */
    function handleClickOutside(event) {
        if (!isOpen) return;

        if (!dropdown?.contains(event.target) && !bellButton?.contains(event.target)) {
            close();
        }
    }

    /**
     * Fetches the initial notification summary on connect.
     */
    async function fetchInitialSummary() {
        try {
            const summary = await DashboardHub.invoke('GetNotificationSummary');
            if (summary) {
                updateBadge(summary.totalUnread);
            }
        } catch (error) {
            console.error('[NotificationBell] Failed to fetch initial summary:', error);
        }
    }

    /**
     * Toggles the dropdown visibility.
     */
    function toggle() {
        if (isOpen) {
            close();
        } else {
            open();
        }
    }

    /**
     * Opens the notification dropdown.
     */
    async function open() {
        if (isOpen) return;

        isOpen = true;
        dropdown?.classList.add('active');
        bellButton?.setAttribute('aria-expanded', 'true');

        // Load notifications if not already loaded
        if (!hasLoaded) {
            await loadNotifications();
        }

        // Focus first interactive element
        setTimeout(() => {
            const firstAction = dropdown?.querySelector('button, a, [tabindex="0"]');
            firstAction?.focus();
        }, 100);
    }

    /**
     * Closes the notification dropdown.
     */
    function close() {
        if (!isOpen) return;

        isOpen = false;
        dropdown?.classList.remove('active');
        bellButton?.setAttribute('aria-expanded', 'false');
    }

    /**
     * Loads notifications from the server.
     */
    async function loadNotifications() {
        if (isLoading) return;

        isLoading = true;

        // Show loading state
        if (notificationList) {
            notificationList.innerHTML = '<div class="notification-loading">Loading notifications...</div>';
        }

        try {
            const result = await DashboardHub.invoke('GetNotifications', 15);
            if (result) {
                notifications = Array.isArray(result) ? result : [];
                hasLoaded = true;
                render();
            }
        } catch (error) {
            console.error('[NotificationBell] Failed to load notifications:', error);
            if (notificationList) {
                notificationList.innerHTML = '<div class="notification-empty"><p class="notification-empty-title">Unable to load notifications</p><p class="notification-empty-message">Please try again later</p></div>';
            }
        } finally {
            isLoading = false;
        }
    }

    /**
     * Marks a notification as read.
     * @param {string} notificationId - The notification GUID
     */
    async function markAsRead(notificationId) {
        try {
            await DashboardHub.invoke('MarkNotificationRead', notificationId);

            // Update local state
            const notification = notifications.find(n => n.id === notificationId);
            if (notification) {
                notification.isRead = true;
                render();
                updateBadgeFromLocal();
            }
        } catch (error) {
            console.error('[NotificationBell] Failed to mark notification as read:', error);
        }
    }

    /**
     * Marks all notifications as read.
     */
    async function markAllAsRead() {
        try {
            await DashboardHub.invoke('MarkAllNotificationsRead');

            // Update local state
            notifications.forEach(n => n.isRead = true);
            render();
            updateBadge(0);
        } catch (error) {
            console.error('[NotificationBell] Failed to mark all as read:', error);
        }
    }

    /**
     * Dismisses a notification.
     * @param {string} notificationId - The notification GUID
     */
    async function dismiss(notificationId) {
        try {
            await DashboardHub.invoke('DismissNotification', notificationId);

            // Remove from local state
            const wasUnread = notifications.find(n => n.id === notificationId)?.isRead === false;
            notifications = notifications.filter(n => n.id !== notificationId);
            render();

            if (wasUnread) {
                updateBadgeFromLocal();
            }
        } catch (error) {
            console.error('[NotificationBell] Failed to dismiss notification:', error);
        }
    }

    /**
     * Renders the notification list.
     */
    function render() {
        if (!notificationList) return;

        if (notifications.length === 0) {
            notificationList.innerHTML = renderEmptyState();
            return;
        }

        notificationList.innerHTML = notifications.map(n => renderItem(n)).join('');
    }

    /**
     * Renders a single notification item.
     * @param {Object} notification - The notification object
     * @returns {string} HTML string for the notification item
     */
    function renderItem(notification) {
        const iconClass = getIconClass(notification.type, notification.severity);
        const iconSvg = getIconSvg(notification.type);
        const isRead = notification.isRead;

        // Escape HTML in user-provided content
        const title = escapeHtml(notification.title || '');
        const message = escapeHtml(notification.message || '');
        const typeDisplay = escapeHtml(notification.typeDisplay || '');
        const timeAgo = escapeHtml(notification.timeAgo || '');

        const linkUrl = notification.linkUrl || '#';

        return `
            <div class="notification-item"
                 role="listitem"
                 data-notification-id="${notification.id}"
                 data-read="${isRead}"
                 tabindex="0"
                 onclick="NotificationBell.handleItemClick(event, '${notification.id}', '${escapeHtml(linkUrl)}')"
                 onkeydown="NotificationBell.handleItemKeydown(event, '${notification.id}', '${escapeHtml(linkUrl)}')">
                <div class="notification-icon ${iconClass}">
                    <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
                        ${iconSvg}
                    </svg>
                </div>
                <div class="notification-content">
                    <div class="notification-title">${title}</div>
                    <div class="notification-message">${message}</div>
                    <div class="notification-meta">
                        <span class="notification-timestamp" title="${notification.createdAt}">${timeAgo}</span>
                        <span class="notification-type-badge">${typeDisplay}</span>
                    </div>
                </div>
                <div class="notification-actions">
                    <button
                        onclick="event.stopPropagation(); NotificationBell.toggleRead('${notification.id}')"
                        class="notification-action-btn"
                        aria-label="${isRead ? 'Mark as unread' : 'Mark as read'}"
                        title="${isRead ? 'Mark as unread' : 'Mark as read'}">
                        <svg class="w-4 h-4 notification-unread-icon" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 8l7.89 5.26a2 2 0 002.22 0L21 8M5 19h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" />
                        </svg>
                        <svg class="w-4 h-4 notification-read-icon" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 19v-8.93a2 2 0 01.89-1.664l7-4.666a2 2 0 012.22 0l7 4.666A2 2 0 0121 10.07V19M3 19a2 2 0 002 2h14a2 2 0 002-2M3 19l6.75-4.5M21 19l-6.75-4.5M3 10l6.75 4.5M21 10l-6.75 4.5m0 0l-1.14.76a2 2 0 01-2.22 0l-1.14-.76" />
                        </svg>
                    </button>
                    <button
                        onclick="event.stopPropagation(); NotificationBell.dismiss('${notification.id}')"
                        class="notification-action-btn"
                        aria-label="Dismiss notification"
                        title="Dismiss">
                        <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
                        </svg>
                    </button>
                </div>
            </div>
        `;
    }

    /**
     * Renders the empty state HTML.
     * @returns {string} HTML string for the empty state
     */
    function renderEmptyState() {
        return `
            <div class="notification-empty">
                <svg class="w-12 h-12 text-text-tertiary opacity-50" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9" />
                </svg>
                <p class="notification-empty-title">No notifications</p>
                <p class="notification-empty-message">You're all caught up!</p>
            </div>
        `;
    }

    /**
     * Updates the badge count.
     * @param {number} count - The unread count
     */
    function updateBadge(count) {
        if (!badge) return;

        if (count > 0) {
            badge.textContent = count > 99 ? '99+' : count.toString();
            badge.setAttribute('aria-label', `${count} unread notification${count !== 1 ? 's' : ''}`);
            badge.classList.remove('hidden');
        } else {
            badge.classList.add('hidden');
        }
    }

    /**
     * Updates the badge from local notification state.
     */
    function updateBadgeFromLocal() {
        const unreadCount = notifications.filter(n => !n.isRead).length;
        updateBadge(unreadCount);
    }

    /**
     * Pulses the badge to indicate a new notification.
     */
    function pulseBadge() {
        if (!badge) return;

        badge.classList.add('pulse-once');
        setTimeout(() => badge.classList.remove('pulse-once'), 600);
    }

    /**
     * Announces a message to screen readers.
     * @param {string} message - The message to announce
     */
    function announce(message) {
        if (!announcer) return;

        announcer.textContent = message;
        // Clear after announcement
        setTimeout(() => announcer.textContent = '', 1000);
    }

    /**
     * Gets the icon CSS class for a notification.
     * @param {number} type - NotificationType enum value
     * @param {number|null} severity - AlertSeverity enum value (for PerformanceAlert)
     * @returns {string} The CSS class name
     */
    function getIconClass(type, severity) {
        // For PerformanceAlert, use severity-based class
        if (type === 1 && severity !== null && severity !== undefined) {
            return severityIconClasses[severity] || 'notification-icon-warning';
        }
        return typeIconClasses[type] || 'notification-icon-info';
    }

    /**
     * Gets the SVG path for a notification type icon.
     * @param {number} type - NotificationType enum value
     * @returns {string} The SVG path
     */
    function getIconSvg(type) {
        return typeIcons[type] || typeIcons[1]; // Default to alert icon
    }

    /**
     * Escapes HTML special characters.
     * @param {string} str - The string to escape
     * @returns {string} The escaped string
     */
    function escapeHtml(str) {
        if (!str) return '';
        const div = document.createElement('div');
        div.textContent = str;
        return div.innerHTML;
    }

    // =========================================================================
    // SignalR Event Handlers
    // =========================================================================

    /**
     * Handles new notification received via SignalR.
     * @param {Object} notification - The new notification
     */
    function onNotificationReceived(notification) {
        console.log('[NotificationBell] New notification received:', notification);

        // Add to beginning of list
        notifications.unshift(notification);

        // Limit to 15 notifications
        if (notifications.length > 15) {
            notifications.pop();
        }

        // Update UI
        if (hasLoaded) {
            render();
        }

        updateBadgeFromLocal();
        pulseBadge();

        // Announce to screen readers
        announce(`New notification: ${notification.title || 'New notification'}`);
    }

    /**
     * Handles notification count change via SignalR.
     * @param {Object} summary - The notification summary
     */
    function onNotificationCountChanged(summary) {
        console.log('[NotificationBell] Notification count changed:', summary);
        updateBadge(summary.totalUnread);
    }

    /**
     * Handles notification marked as read via SignalR (from another session).
     * @param {Object} data - Contains notificationId
     */
    function onNotificationMarkedRead(data) {
        const notification = notifications.find(n => n.id === data.notificationId);
        if (notification) {
            notification.isRead = true;
            render();
            updateBadgeFromLocal();
        }
    }

    /**
     * Handles all notifications marked as read via SignalR (from another session).
     */
    function onAllNotificationsRead() {
        notifications.forEach(n => n.isRead = true);
        render();
        updateBadge(0);
    }

    // =========================================================================
    // Item Interaction Handlers
    // =========================================================================

    /**
     * Handles click on a notification item.
     * @param {Event} event - The click event
     * @param {string} notificationId - The notification ID
     * @param {string} linkUrl - The link URL
     */
    function handleItemClick(event, notificationId, linkUrl) {
        // Mark as read
        markAsRead(notificationId);

        // Navigate if link exists
        if (linkUrl && linkUrl !== '#') {
            window.location.href = linkUrl;
        }
    }

    /**
     * Handles keydown on a notification item.
     * @param {KeyboardEvent} event - The keyboard event
     * @param {string} notificationId - The notification ID
     * @param {string} linkUrl - The link URL
     */
    function handleItemKeydown(event, notificationId, linkUrl) {
        if (event.key === 'Enter' || event.key === ' ') {
            event.preventDefault();
            handleItemClick(event, notificationId, linkUrl);
        }
    }

    /**
     * Toggles the read state of a notification.
     * @param {string} notificationId - The notification ID
     */
    async function toggleRead(notificationId) {
        const notification = notifications.find(n => n.id === notificationId);
        if (!notification) return;

        if (notification.isRead) {
            // Mark as unread - not supported by backend currently, just update local state
            notification.isRead = false;
            render();
            updateBadgeFromLocal();
        } else {
            await markAsRead(notificationId);
        }
    }

    // Initialize when DOM is ready and DashboardHub is available
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        // DOM already loaded, wait a tick for DashboardHub to be available
        setTimeout(init, 0);
    }

    // Public API
    return {
        toggle,
        open,
        close,
        markAsRead,
        markAllAsRead,
        dismiss,
        toggleRead,
        handleItemClick,
        handleItemKeydown,
        refresh: loadNotifications
    };
})();

// Auto-export for module systems if available
if (typeof module !== 'undefined' && module.exports) {
    module.exports = NotificationBell;
}
