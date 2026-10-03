/**
 * Notification Bell Module
 * Manages the notification bell dropdown in the global navbar.
 * Integrates with DashboardHub for real-time notification updates.
 *
 * The dropdown is a disclosure, not a menu: the bell button carries aria-expanded and
 * aria-controls, the panel is a labelled region, and everything inside it is reached with Tab.
 * Escape closes it and returns focus to the bell. A notification with a link renders its title
 * as a real link (stretched over the whole item with CSS), so keyboard and screen reader users
 * get the same target a mouse user clicks.
 */
const NotificationBell = (function () {
    'use strict';

    // State
    let notifications = [];
    let isOpen = false;
    let isLoading = false;
    let hasLoaded = false;
    let isInitialized = false;

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
        // Prevent double initialization
        if (isInitialized) {
            return;
        }

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

        isInitialized = true;

        // The bell and the two header buttons (no inline handlers in the markup)
        bellButton.addEventListener('click', toggle);
        dropdown.querySelector('[data-notification-mark-all]')?.addEventListener('click', markAllAsRead);
        dropdown.querySelector('[data-notification-close]')?.addEventListener('click', () => {
            close();
            bellButton.focus();
        });

        // Register SignalR event handlers
        registerSignalRHandlers();

        // Setup keyboard handlers
        setupKeyboardHandlers();

        // Setup click outside handler
        document.addEventListener('click', handleClickOutside);

        // Setup delegated event handlers for notification items
        setupDelegatedEventHandlers();

        // Fetch initial notification summary when DashboardHub connects
        if (DashboardHub.isConnected()) {
            fetchInitialSummary();
        } else {
            // Wait for connection
            DashboardHub.on('connected', fetchInitialSummary);
        }

        // A new connection missed whatever happened while it was down: take the count again, and
        // the list too if it has been opened before
        DashboardHub.on('reconnected', () => {
            fetchInitialSummary();
            if (hasLoaded) loadNotifications();
        });
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
     * Sets up keyboard handlers: Escape closes the panel and returns focus to the bell.
     */
    function setupKeyboardHandlers() {
        document.addEventListener('keydown', (event) => {
            if (event.key === 'Escape' && isOpen) {
                close();
                bellButton?.focus();
            }
        });
    }

    /**
     * Sets up delegated event handlers for notification items.
     * This prevents XSS risks from inline event handlers.
     */
    function setupDelegatedEventHandlers() {
        if (!notificationList) return;

        // Handle clicks on notification items and action buttons
        notificationList.addEventListener('click', (event) => {
            const target = event.target;

            // Check for mark-read button
            const markReadBtn = target.closest('[data-action="mark-read"]');
            if (markReadBtn) {
                event.stopPropagation();
                const notificationId = markReadBtn.closest('.notification-item')?.dataset.notificationId;
                if (notificationId) {
                    markAsRead(notificationId);
                }
                return;
            }

            // Check for dismiss button
            const dismissBtn = target.closest('[data-action="dismiss"]');
            if (dismissBtn) {
                event.stopPropagation();
                const notificationId = dismissBtn.closest('.notification-item')?.dataset.notificationId;
                if (notificationId) {
                    dismiss(notificationId);
                }
                return;
            }

            // The title link: mark the notification read, then follow the link
            const titleLink = target.closest('.notification-title-link');
            if (titleLink) {
                event.preventDefault();
                const item = titleLink.closest('.notification-item');
                if (item?.dataset.notificationId) {
                    handleItemClick(item.dataset.notificationId, titleLink.getAttribute('href'));
                }
                return;
            }

            // A click anywhere else on an item (mouse convenience): mark it read
            const item = target.closest('.notification-item');
            if (item?.dataset.notificationId && item.dataset.read === 'false') {
                markAsRead(item.dataset.notificationId);
            }
        });
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
            const firstAction = dropdown?.querySelector('button, a[href]');
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
                notificationList.innerHTML = '<div class="notification-empty" role="alert"><p class="notification-empty-title">Could not load notifications</p>' +
                    '<button type="button" class="btn btn-secondary btn-sm" data-notification-retry>Try again</button></div>';
                notificationList.querySelector('[data-notification-retry]')?.addEventListener('click', loadNotifications);
            }
        } finally {
            isLoading = false;
        }
    }

    /**
     * Shows an error toast.
     * @param {string} message - The error message
     */
    function showErrorToast(message) {
        if (typeof toast !== 'undefined' && toast.error) {
            toast.error(message);
        }
    }

    /**
     * Marks a notification as read.
     * @param {string} notificationId - The notification GUID
     */
    async function markAsRead(notificationId) {
        // Optimistic update
        const notification = notifications.find(n => n.id === notificationId);
        const previousState = notification?.isRead;

        if (notification) {
            notification.isRead = true;
            render();
            updateBadgeFromLocal();
            announce('Notification marked as read');
        }

        try {
            await DashboardHub.invoke('MarkNotificationRead', notificationId);
        } catch (error) {
            console.error('[NotificationBell] Failed to mark notification as read:', error);

            // Revert optimistic update
            if (notification && previousState !== undefined) {
                notification.isRead = previousState;
                render();
                updateBadgeFromLocal();
            }

            showErrorToast('Failed to mark notification as read');
        }
    }

    /**
     * Marks all notifications as read.
     */
    async function markAllAsRead() {
        // Store previous state for potential rollback
        const previousStates = notifications.map(n => ({ id: n.id, isRead: n.isRead }));

        // Optimistic update
        notifications.forEach(n => n.isRead = true);
        render();
        updateBadge(0);
        announce('All notifications marked as read');

        try {
            await DashboardHub.invoke('MarkAllNotificationsRead');
        } catch (error) {
            console.error('[NotificationBell] Failed to mark all as read:', error);

            // Revert optimistic update
            previousStates.forEach(ps => {
                const n = notifications.find(notif => notif.id === ps.id);
                if (n) n.isRead = ps.isRead;
            });
            render();
            updateBadgeFromLocal();

            showErrorToast('Failed to mark all notifications as read');
        }
    }

    /**
     * Dismisses a notification.
     * @param {string} notificationId - The notification GUID
     */
    async function dismiss(notificationId) {
        // Store notification for potential rollback
        const notificationIndex = notifications.findIndex(n => n.id === notificationId);
        const removedNotification = notifications[notificationIndex];
        const wasUnread = removedNotification?.isRead === false;

        // Optimistic update
        notifications = notifications.filter(n => n.id !== notificationId);
        render();
        if (wasUnread) {
            updateBadgeFromLocal();
        }
        announce('Notification dismissed');

        try {
            await DashboardHub.invoke('DismissNotification', notificationId);
        } catch (error) {
            console.error('[NotificationBell] Failed to dismiss notification:', error);

            // Revert optimistic update
            if (removedNotification) {
                notifications.splice(notificationIndex, 0, removedNotification);
                render();
                if (wasUnread) {
                    updateBadgeFromLocal();
                }
            }

            showErrorToast('Failed to dismiss notification');
        }
    }

    /**
     * Renders the notification list.
     */
    function render() {
        if (!notificationList) return;

        // Re-rendering replaces every node, which would drop the focus of a keyboard user who
        // just pressed a button in the list: remember where it was and put it back
        const active = document.activeElement;
        const hadFocus = !!active && notificationList.contains(active);
        const focusId = hadFocus ? active.closest('.notification-item')?.dataset.notificationId : null;
        const focusAction = hadFocus ? active.dataset?.action : null;

        if (notifications.length === 0) {
            notificationList.innerHTML = renderEmptyState();
        } else {
            notificationList.innerHTML = notifications.map(n => renderItem(n)).join('');
        }

        if (hadFocus) {
            const item = focusId
                ? notificationList.querySelector(`[data-notification-id="${CSS.escape(focusId)}"]`)
                : null;
            const target = item
                ? (item.querySelector(`[data-action="${focusAction}"]:not([hidden])`) || item.querySelector('a[href], button:not([hidden])'))
                : (notificationList.querySelector('a[href], button:not([hidden])') || dropdown?.querySelector('button'));
            target?.focus();
        }
    }

    /**
     * Renders a single notification item.
     * Uses data attributes instead of inline event handlers to prevent XSS.
     * @param {Object} notification - The notification object
     * @returns {string} HTML string for the notification item
     */
    function renderItem(notification) {
        const iconClass = getIconClass(notification.type, notification.severity);
        const iconSvg = getIconSvg(notification.type);
        const isRead = notification.isRead;

        // Escape HTML in user-provided content
        const title = SafeHtml.escape(notification.title || '');
        const message = SafeHtml.escape(notification.message || '');
        const typeDisplay = SafeHtml.escape(notification.typeDisplay || '');
        const timeAgo = SafeHtml.escape(notification.timeAgo || '');

        // The title is the link when there is one; CSS stretches it over the whole item
        const linkUrl = notification.linkUrl && notification.linkUrl !== '#' ? notification.linkUrl : '';
        const titleHtml = linkUrl
            ? `<a href="${SafeHtml.escape(linkUrl)}" class="notification-title-link">${title}</a>`
            : title;

        // The age as relative time; tabindex="-1" keeps it from becoming a Tab stop per item
        const createdAt = notification.createdAt || '';
        const ageHtml = createdAt
            ? `<span class="notification-timestamp" data-relative-time="${SafeHtml.escape(createdAt)}" tabindex="-1">${timeAgo}</span>`
            : `<span class="notification-timestamp">${timeAgo}</span>`;

        return `
            <div class="notification-item"
                 role="listitem"
                 data-notification-id="${SafeHtml.escape(notification.id)}"
                 data-read="${isRead}">
                <div class="notification-icon ${iconClass}">
                    <svg fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
                        ${iconSvg}
                    </svg>
                </div>
                <div class="notification-content">
                    <p class="notification-title">${titleHtml}</p>
                    <p class="notification-message">${message}</p>
                    <div class="notification-meta">
                        ${ageHtml}
                        <span class="notification-type-badge">${typeDisplay}</span>
                    </div>
                </div>
                <div class="notification-actions">
                    <button
                        type="button"
                        data-action="mark-read"
                        class="notification-action-btn"
                        aria-label="Mark as read"
                        title="Mark as read"
                        ${isRead ? 'hidden' : ''}>
                        <svg fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 8l7.89 5.26a2 2 0 002.22 0L21 8M5 19h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" />
                        </svg>
                    </button>
                    <button
                        type="button"
                        data-action="dismiss"
                        class="notification-action-btn"
                        aria-label="Dismiss notification"
                        title="Dismiss">
                        <svg fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
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

    // =========================================================================
    // SignalR Event Handlers
    // =========================================================================

    /**
     * Handles new notification received via SignalR.
     * @param {Object} notification - The new notification
     */
    function onNotificationReceived(notification) {

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
     * Handles a click on a notification's link: mark it read, then follow the link. The mark is
     * given a moment to reach the server before the page unloads; a slow hub does not hold the
     * navigation back.
     * @param {string} notificationId - The notification ID
     * @param {string} linkUrl - The link URL
     */
    async function handleItemClick(notificationId, linkUrl) {
        const pending = markAsRead(notificationId);
        if (linkUrl && linkUrl !== '#') {
            await Promise.race([pending, new Promise((resolve) => setTimeout(resolve, 800))]);
            window.location.href = linkUrl;
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
        refresh: loadNotifications
    };
})();

// Auto-export for module systems if available
if (typeof module !== 'undefined' && module.exports) {
    module.exports = NotificationBell;
}
