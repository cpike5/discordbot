# Notification Bell Dropdown - UI/UX Specification

**Version:** 1.1
**Last Updated:** 2026-01-08
**Status:** Design Specification
**Related Issues:** #607, #739, #740, #741, #742, #743, #744

---

## Overview

This specification defines the UI/UX design for a **global** notification bell dropdown component in the admin UI navbar. The component is available on **all authenticated pages** and provides real-time notification delivery for performance alerts, bot status changes, guild events, and command errors through SignalR integration.

### Global Architecture

The notification system is designed to work across all pages:

- **Navbar Integration:** Bell icon in `_Navbar.cshtml`, included via `_Layout.cshtml` on every page
- **SignalR Connection:** `DashboardHub` connection established globally in `_Layout.cshtml`
- **User-Specific Delivery:** Notifications broadcast to specific users via `Clients.User(userId)`
- **Persistent State:** Unread count and notifications persist across page navigation

### Design Principles

- **Unobtrusive**: Non-blocking notifications that don't interrupt workflow
- **Scannable**: Clear visual hierarchy with type-based grouping
- **Actionable**: Direct links to relevant pages for investigation
- **Accessible**: Keyboard navigation, ARIA labels, screen reader support
- **Consistent**: Follows existing design system patterns (user menu dropdown)

---

## Notification Types

| Type | Priority | Icon | Color | Example |
|------|----------|------|-------|---------|
| **Performance Alert** | Critical/Warning/Info | Exclamation Triangle | Critical: `error`, Warning: `warning`, Info: `info` | "High API latency detected: 850ms avg" |
| **Bot Status** | High | Power/Warning | Online: `success`, Offline: `error` | "Bot disconnected from Discord" |
| **Guild Event** | Medium | Server/Users | `accent-blue` | "Bot joined new server: GamersHub" |
| **Command Error** | Medium | Code/Bug | `error` | "Command /ban failed in GamersHub" |

---

## Component Structure

### 1. Bell Button (Trigger)

**Location:** Global navbar (`_Navbar.cshtml`), right section, between connection status and user menu. Available on all authenticated pages.

**States:**
- **Default**: Gray bell icon, no badge
- **Unread**: Bell icon with orange badge showing count
- **Active**: Dropdown open, button highlighted

**HTML Structure:**

```html
<!-- Notification Bell Button -->
<div class="relative">
  <button
    id="notificationBellButton"
    onclick="toggleNotificationDropdown()"
    class="relative p-2 rounded-lg text-text-secondary hover:text-text-primary hover:bg-bg-hover transition-colors"
    aria-label="Notifications"
    aria-expanded="false"
    aria-haspopup="menu"
    aria-controls="notificationDropdown"
  >
    <!-- Bell Icon (Heroicon: bell) -->
    <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9" />
    </svg>

    <!-- Unread Badge (hidden when count = 0) -->
    <span
      id="notificationBadge"
      class="notification-badge hidden"
      aria-label="3 unread notifications"
    >3</span>
  </button>

  <!-- Dropdown (see section 2) -->
</div>
```

**CSS Classes:**

```css
/* Notification badge - extends existing notification-dot pattern */
.notification-badge {
  position: absolute;
  top: 0;
  right: 0;
  min-width: 1.125rem;        /* 18px */
  height: 1.125rem;
  padding: 0 0.25rem;
  background-color: var(--color-accent-orange);
  color: #ffffff;
  font-size: 0.625rem;        /* 10px */
  font-weight: 700;
  line-height: 1.125rem;
  text-align: center;
  border-radius: 0.5625rem;   /* Pill shape */
  border: 2px solid var(--color-bg-secondary);
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.2);
}

.notification-badge.hidden {
  display: none;
}

/* Badge pulse animation for new notifications */
.notification-badge.pulse-once {
  animation: badge-pulse 0.6s cubic-bezier(0.4, 0, 0.2, 1);
}

@keyframes badge-pulse {
  0%, 100% {
    transform: scale(1);
  }
  50% {
    transform: scale(1.2);
  }
}
```

---

### 2. Notification Dropdown

**Pattern:** Extends `user-menu-dropdown` pattern from existing navbar

**Dimensions:**
- Width: `360px` (fixed)
- Max Height: `480px` (scrollable)
- Position: Right-aligned under bell button with 8px gap

**HTML Structure:**

```html
<!-- Notification Dropdown -->
<div
  id="notificationDropdown"
  class="notification-dropdown"
  role="menu"
  aria-label="Notifications menu"
>
  <!-- Header -->
  <div class="notification-dropdown-header">
    <h3 class="text-sm font-semibold text-text-primary">Notifications</h3>
    <button
      onclick="markAllAsRead()"
      class="notification-mark-all-read"
      aria-label="Mark all as read"
    >
      <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
      </svg>
      <span>Mark all read</span>
    </button>
  </div>

  <!-- Notification List (scrollable) -->
  <div class="notification-list" role="list">
    <!-- Notification items rendered here (see section 3) -->
  </div>

  <!-- Footer -->
  <div class="notification-dropdown-footer">
    <a href="/Admin/Notifications" class="notification-view-all">
      View all notifications
      <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7" />
      </svg>
    </a>
  </div>
</div>
```

**CSS Classes:**

```css
/* Notification Dropdown - extends user-menu-dropdown pattern */
.notification-dropdown {
  position: absolute;
  right: 0;
  top: calc(100% + 0.5rem);
  width: 360px;
  background-color: var(--color-bg-tertiary);
  border: 1px solid var(--color-border-primary);
  border-radius: 0.5rem;
  box-shadow: 0 10px 40px rgba(0, 0, 0, 0.3);
  opacity: 0;
  visibility: hidden;
  transform: translateY(-8px);
  transition: all 0.15s ease-out;
  z-index: 350;
  display: flex;
  flex-direction: column;
  max-height: 480px;
}

.notification-dropdown.active {
  opacity: 1;
  visibility: visible;
  transform: translateY(0);
}

/* Header */
.notification-dropdown-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 1rem;
  border-bottom: 1px solid var(--color-border-primary);
  flex-shrink: 0;
}

.notification-mark-all-read {
  display: inline-flex;
  align-items: center;
  gap: 0.375rem;
  padding: 0.375rem 0.625rem;
  font-size: 0.75rem;
  font-weight: 500;
  color: var(--color-text-secondary);
  background-color: transparent;
  border: none;
  border-radius: 0.375rem;
  cursor: pointer;
  transition: all 0.15s ease-out;
}

.notification-mark-all-read:hover {
  color: var(--color-accent-blue);
  background-color: var(--color-bg-hover);
}

.notification-mark-all-read:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

/* Notification List (scrollable area) */
.notification-list {
  flex: 1;
  overflow-y: auto;
  overflow-x: hidden;
  /* Custom scrollbar styling */
  scrollbar-width: thin;
  scrollbar-color: var(--color-border-primary) transparent;
}

.notification-list::-webkit-scrollbar {
  width: 6px;
}

.notification-list::-webkit-scrollbar-track {
  background: transparent;
}

.notification-list::-webkit-scrollbar-thumb {
  background-color: var(--color-border-primary);
  border-radius: 3px;
}

.notification-list::-webkit-scrollbar-thumb:hover {
  background-color: var(--color-accent-blue);
}

/* Footer */
.notification-dropdown-footer {
  padding: 0.75rem 1rem;
  border-top: 1px solid var(--color-border-primary);
  background-color: var(--color-bg-secondary);
  flex-shrink: 0;
  border-radius: 0 0 0.5rem 0.5rem;
}

.notification-view-all {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  width: 100%;
  justify-content: center;
  padding: 0.5rem;
  font-size: 0.875rem;
  font-weight: 600;
  color: var(--color-accent-blue);
  text-decoration: none;
  border-radius: 0.375rem;
  transition: all 0.15s ease-out;
}

.notification-view-all:hover {
  background-color: var(--color-bg-hover);
  color: var(--color-accent-blue-hover);
}
```

---

### 3. Notification Item

**Structure:** Icon + Content + Actions + Timestamp

**States:**
- **Unread**: Blue accent background, bold text
- **Read**: Normal background, normal text weight
- **Hover**: Elevated background
- **Focus**: Blue outline for keyboard navigation

**HTML Structure:**

```html
<!-- Notification Item -->
<div
  class="notification-item"
  role="listitem"
  data-notification-id="550e8400-e29b-41d4-a716-446655440000"
  data-read="false"
  tabindex="0"
>
  <!-- Icon Container -->
  <div class="notification-icon notification-icon-critical">
    <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
    </svg>
  </div>

  <!-- Content -->
  <div class="notification-content">
    <div class="notification-title">High API Latency Detected</div>
    <div class="notification-message">
      Average response time: 850ms (threshold: 500ms)
    </div>
    <div class="notification-meta">
      <span class="notification-timestamp" title="2026-01-05 14:32:15">2 minutes ago</span>
      <span class="notification-type-badge">Performance Alert</span>
    </div>
  </div>

  <!-- Actions -->
  <div class="notification-actions">
    <!-- Mark as Read/Unread Toggle -->
    <button
      onclick="toggleNotificationRead(this, '550e8400-e29b-41d4-a716-446655440000')"
      class="notification-action-btn"
      aria-label="Mark as read"
      title="Mark as read"
    >
      <svg class="w-4 h-4 notification-unread-icon" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 8l7.89 5.26a2 2 0 002.22 0L21 8M5 19h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" />
      </svg>
      <svg class="w-4 h-4 notification-read-icon hidden" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 19v-8.93a2 2 0 01.89-1.664l7-4.666a2 2 0 012.22 0l7 4.666A2 2 0 0121 10.07V19M3 19a2 2 0 002 2h14a2 2 0 002-2M3 19l6.75-4.5M21 19l-6.75-4.5M3 10l6.75 4.5M21 10l-6.75 4.5m0 0l-1.14.76a2 2 0 01-2.22 0l-1.14-.76" />
      </svg>
    </button>

    <!-- Dismiss -->
    <button
      onclick="dismissNotification(this, '550e8400-e29b-41d4-a716-446655440000')"
      class="notification-action-btn"
      aria-label="Dismiss notification"
      title="Dismiss"
    >
      <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
      </svg>
    </button>
  </div>

  <!-- Click to navigate (overlay link) -->
  <a
    href="/Admin/Performance/Alerts"
    class="notification-overlay-link"
    aria-label="View performance alerts"
  ></a>
</div>
```

**CSS Classes:**

```css
/* Notification Item */
.notification-item {
  position: relative;
  display: grid;
  grid-template-columns: 2rem 1fr auto;
  gap: 0.75rem;
  padding: 0.875rem 1rem;
  border-bottom: 1px solid var(--color-border-secondary);
  transition: background-color 0.15s ease-out;
  cursor: pointer;
}

.notification-item:last-child {
  border-bottom: none;
}

.notification-item:hover {
  background-color: var(--color-bg-hover);
}

.notification-item:focus {
  outline: 2px solid var(--color-border-focus);
  outline-offset: -2px;
  background-color: var(--color-bg-hover);
}

/* Unread state - blue accent background */
.notification-item[data-read="false"] {
  background-color: rgba(9, 142, 207, 0.08);
}

.notification-item[data-read="false"]:hover {
  background-color: rgba(9, 142, 207, 0.12);
}

.notification-item[data-read="false"] .notification-title {
  font-weight: 600;
  color: var(--color-text-primary);
}

/* Unread indicator dot */
.notification-item[data-read="false"]::before {
  content: '';
  position: absolute;
  left: 0.375rem;
  top: 50%;
  transform: translateY(-50%);
  width: 6px;
  height: 6px;
  background-color: var(--color-accent-blue);
  border-radius: 50%;
}

/* Icon Container */
.notification-icon {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 2rem;
  height: 2rem;
  border-radius: 0.5rem;
  flex-shrink: 0;
}

/* Icon variants by type */
.notification-icon-critical {
  background-color: rgba(239, 68, 68, 0.15);
  color: var(--color-error);
}

.notification-icon-warning {
  background-color: rgba(245, 158, 11, 0.15);
  color: var(--color-warning);
}

.notification-icon-info {
  background-color: rgba(6, 182, 212, 0.15);
  color: var(--color-info);
}

.notification-icon-success {
  background-color: rgba(16, 185, 129, 0.15);
  color: var(--color-success);
}

.notification-icon-guild {
  background-color: rgba(203, 78, 27, 0.15);
  color: var(--color-accent-orange);
}

.notification-icon-command {
  background-color: rgba(9, 142, 207, 0.15);
  color: var(--color-accent-blue);
}

/* Content */
.notification-content {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  min-width: 0; /* Allow text truncation */
}

.notification-title {
  font-size: 0.8125rem;
  font-weight: 500;
  color: var(--color-text-primary);
  line-height: 1.3;
}

.notification-message {
  font-size: 0.75rem;
  color: var(--color-text-secondary);
  line-height: 1.4;
  overflow: hidden;
  text-overflow: ellipsis;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
}

.notification-meta {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-top: 0.125rem;
}

.notification-timestamp {
  font-size: 0.6875rem;
  color: var(--color-text-tertiary);
}

.notification-type-badge {
  font-size: 0.625rem;
  font-weight: 600;
  color: var(--color-text-tertiary);
  text-transform: uppercase;
  letter-spacing: 0.025em;
  padding: 0.125rem 0.375rem;
  background-color: var(--color-bg-primary);
  border-radius: 0.25rem;
}

/* Actions */
.notification-actions {
  display: flex;
  align-items: flex-start;
  gap: 0.25rem;
  opacity: 0;
  transition: opacity 0.15s ease-out;
}

.notification-item:hover .notification-actions,
.notification-item:focus .notification-actions {
  opacity: 1;
}

.notification-action-btn {
  padding: 0.375rem;
  background-color: transparent;
  border: none;
  border-radius: 0.25rem;
  color: var(--color-text-tertiary);
  cursor: pointer;
  transition: all 0.15s ease-out;
}

.notification-action-btn:hover {
  background-color: var(--color-bg-secondary);
  color: var(--color-text-primary);
}

/* Toggle read/unread icons */
.notification-item[data-read="false"] .notification-read-icon {
  display: none;
}

.notification-item[data-read="false"] .notification-unread-icon {
  display: block;
}

.notification-item[data-read="true"] .notification-read-icon {
  display: block;
}

.notification-item[data-read="true"] .notification-unread-icon {
  display: none;
}

/* Overlay link for entire item click */
.notification-overlay-link {
  position: absolute;
  inset: 0;
  z-index: 1;
}

/* Ensure action buttons are above overlay link */
.notification-actions {
  position: relative;
  z-index: 2;
}
```

---

### 4. Empty State

**Display When:** No notifications exist

**HTML Structure:**

```html
<!-- Empty State -->
<div class="notification-empty">
  <svg class="w-12 h-12 text-text-tertiary opacity-50" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9" />
  </svg>
  <p class="notification-empty-title">No notifications</p>
  <p class="notification-empty-message">You're all caught up!</p>
</div>
```

**CSS Classes:**

```css
/* Empty State */
.notification-empty {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 0.75rem;
  padding: 3rem 1.5rem;
  text-align: center;
}

.notification-empty-title {
  font-size: 0.875rem;
  font-weight: 600;
  color: var(--color-text-secondary);
  margin: 0;
}

.notification-empty-message {
  font-size: 0.8125rem;
  color: var(--color-text-tertiary);
  margin: 0;
}
```

---

## Interactive States

### Bell Button States

| State | Visual Treatment |
|-------|------------------|
| **Default** | Gray icon (`text-text-secondary`), no badge |
| **Hover** | Light gray bg (`bg-bg-hover`), white icon (`text-text-primary`) |
| **Focus** | Blue outline ring (2px, `border-focus`) |
| **Active (Dropdown Open)** | Stays in hover state |
| **Has Unread** | Orange badge with count, pulse animation on new notification |

### Notification Item States

| State | Visual Treatment |
|-------|------------------|
| **Unread** | Blue accent background (`rgba(9, 142, 207, 0.08)`), blue dot indicator, bold title |
| **Read** | Default background, normal text weight, no dot |
| **Hover** | Darker background (`bg-bg-hover`), actions visible |
| **Focus** | Blue outline ring (2px inset), actions visible |
| **Click** | Navigate to link target, mark as read |

### Action Button States

| State | Visual Treatment |
|-------|------------------|
| **Default** | Transparent, gray icon |
| **Hover** | Light gray bg, white icon |
| **Active/Pressed** | Darker bg, visual feedback |
| **Disabled** | 50% opacity, no hover effect |

---

## Accessibility

### Keyboard Navigation

| Key | Action |
|-----|--------|
| `Tab` | Focus next interactive element (bell button → mark all read → notifications → view all) |
| `Shift + Tab` | Focus previous element |
| `Enter` / `Space` | Activate focused element (open dropdown, click notification, toggle read) |
| `Escape` | Close dropdown |
| `Arrow Down/Up` | Navigate between notification items (when dropdown open) |
| `Home` | Focus first notification |
| `End` | Focus last notification |

### ARIA Labels

```html
<!-- Bell button -->
<button aria-label="Notifications" aria-expanded="false" aria-haspopup="menu">

<!-- Badge -->
<span aria-label="3 unread notifications">3</span>

<!-- Dropdown -->
<div role="menu" aria-label="Notifications menu">

<!-- Notification list -->
<div role="list">
  <div role="listitem">

<!-- Action buttons -->
<button aria-label="Mark as read" title="Mark as read">
<button aria-label="Dismiss notification" title="Dismiss">

<!-- Overlay link -->
<a aria-label="View performance alerts">
```

### Screen Reader Announcements

**On new notification arrival:**
```javascript
// Use aria-live region for screen reader announcements
<div id="notification-announcer" class="sr-only" aria-live="polite" aria-atomic="true">
  New performance alert: High API latency detected
</div>
```

### Focus Management

- When dropdown opens, focus moves to first actionable element (mark all read button or first notification)
- When dropdown closes via Escape, focus returns to bell button
- Tab order: Bell button → Mark all read → Notifications (top to bottom) → View all link

---

## Responsive Behavior

### Desktop (≥1024px)

- Dropdown: 360px width, right-aligned
- Max 10 visible notifications (480px height)
- Scrollable list with custom scrollbar

### Tablet (768px - 1023px)

- Same as desktop
- May overlap with other navbar elements (use z-index: 350)

### Mobile (<768px)

**Option A: Full-Screen Modal** (Recommended)

```html
<!-- Mobile: Full-screen overlay -->
<div class="notification-mobile-overlay">
  <div class="notification-mobile-header">
    <h2>Notifications</h2>
    <button onclick="closeNotifications()" aria-label="Close">×</button>
  </div>
  <div class="notification-mobile-list">
    <!-- Notification items -->
  </div>
</div>
```

```css
.notification-mobile-overlay {
  position: fixed;
  inset: 0;
  z-index: 400;
  background-color: var(--color-bg-primary);
  display: flex;
  flex-direction: column;
  transform: translateX(100%);
  transition: transform 0.3s cubic-bezier(0.4, 0, 0.2, 1);
}

.notification-mobile-overlay.active {
  transform: translateX(0);
}

.notification-mobile-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 1rem;
  border-bottom: 1px solid var(--color-border-primary);
  background-color: var(--color-bg-secondary);
}

.notification-mobile-list {
  flex: 1;
  overflow-y: auto;
  -webkit-overflow-scrolling: touch;
}

@media (max-width: 767px) {
  .notification-dropdown {
    display: none; /* Hide desktop dropdown on mobile */
  }
}
```

---

## Animations & Transitions

### Dropdown Entrance/Exit

```css
/* Enter: fade in + slide down */
.notification-dropdown {
  opacity: 0;
  visibility: hidden;
  transform: translateY(-8px);
  transition: all 0.15s ease-out;
}

.notification-dropdown.active {
  opacity: 1;
  visibility: visible;
  transform: translateY(0);
}
```

### Badge Pulse (New Notification)

```css
.notification-badge.pulse-once {
  animation: badge-pulse 0.6s cubic-bezier(0.4, 0, 0.2, 1);
}

@keyframes badge-pulse {
  0%, 100% { transform: scale(1); }
  50% { transform: scale(1.2); }
}
```

### Notification Item Entrance (SignalR Push)

```css
.notification-item-enter {
  animation: slide-in-from-top 0.3s ease-out;
}

@keyframes slide-in-from-top {
  0% {
    opacity: 0;
    transform: translateY(-10px);
  }
  100% {
    opacity: 1;
    transform: translateY(0);
  }
}
```

### Mark as Read Transition

```css
.notification-item[data-read="false"] {
  transition: background-color 0.3s ease-out;
}
```

### Reduced Motion Support

```css
@media (prefers-reduced-motion: reduce) {
  .notification-dropdown,
  .notification-badge,
  .notification-item,
  .notification-mobile-overlay {
    animation: none !important;
    transition: opacity 0.01ms !important;
  }

  .notification-dropdown.active {
    transform: none;
  }
}
```

---

## JavaScript Integration

### Global Initialization

The notification bell initializes automatically on every page via `_Layout.cshtml`:

```html
<!-- Already included globally in _Layout.cshtml -->
<script src="~/js/dashboard-hub.js"></script>
<script src="~/js/notification-bell.js"></script>
```

On page load, the notification bell:
1. Connects to `DashboardHub` (if not already connected)
2. Fetches initial unread count via `GetNotificationSummary()`
3. Registers event handlers for real-time updates
4. Restores dropdown state if previously open

### SignalR Event Handlers

```javascript
// Listen for new notifications from SignalR (works on any page)
DashboardHub.on('NotificationReceived', (notification) => {
  addNotificationToDropdown(notification);
  updateUnreadBadge();
  playNotificationSound(); // Optional
  showToast(notification); // Optional toast for critical alerts
});

// Update unread count
DashboardHub.on('UnreadCountChanged', (count) => {
  updateUnreadBadge(count);
});
```

### Core Functions

```javascript
// Toggle dropdown visibility
function toggleNotificationDropdown() {
  const dropdown = document.getElementById('notificationDropdown');
  const button = document.getElementById('notificationBellButton');
  const isActive = dropdown.classList.contains('active');

  if (isActive) {
    closeNotificationDropdown();
  } else {
    openNotificationDropdown();
  }
}

// Open dropdown
function openNotificationDropdown() {
  const dropdown = document.getElementById('notificationDropdown');
  const button = document.getElementById('notificationBellButton');

  dropdown.classList.add('active');
  button.setAttribute('aria-expanded', 'true');

  // Focus first interactive element
  const firstAction = dropdown.querySelector('button, a');
  firstAction?.focus();

  // Load notifications if not already loaded
  loadNotifications();

  // Add click-outside handler
  document.addEventListener('click', handleClickOutside);
}

// Close dropdown
function closeNotificationDropdown() {
  const dropdown = document.getElementById('notificationDropdown');
  const button = document.getElementById('notificationBellButton');

  dropdown.classList.remove('active');
  button.setAttribute('aria-expanded', 'false');
  button.focus(); // Return focus to trigger

  document.removeEventListener('click', handleClickOutside);
}

// Mark all as read
async function markAllAsRead() {
  try {
    await fetch('/api/notifications/mark-all-read', { method: 'POST' });

    // Update UI
    document.querySelectorAll('.notification-item[data-read="false"]').forEach(item => {
      item.setAttribute('data-read', 'true');
    });

    updateUnreadBadge(0);
  } catch (error) {
    console.error('Failed to mark all as read:', error);
  }
}

// Toggle individual notification read state
async function toggleNotificationRead(button, notificationId) {
  const item = button.closest('.notification-item');
  const isRead = item.getAttribute('data-read') === 'true';

  try {
    await fetch(`/api/notifications/${notificationId}/toggle-read`, { method: 'POST' });

    // Toggle UI state
    item.setAttribute('data-read', isRead ? 'false' : 'true');

    // Update badge count
    const unreadCount = document.querySelectorAll('.notification-item[data-read="false"]').length;
    updateUnreadBadge(unreadCount);
  } catch (error) {
    console.error('Failed to toggle read state:', error);
  }
}

// Dismiss notification
async function dismissNotification(button, notificationId) {
  const item = button.closest('.notification-item');

  try {
    await fetch(`/api/notifications/${notificationId}/dismiss`, { method: 'DELETE' });

    // Animate out and remove
    item.style.opacity = '0';
    item.style.transform = 'translateX(100%)';

    setTimeout(() => {
      item.remove();

      // Show empty state if no notifications left
      const list = document.querySelector('.notification-list');
      if (list.children.length === 0) {
        list.innerHTML = `<div class="notification-empty">...</div>`;
      }

      // Update badge
      const unreadCount = document.querySelectorAll('.notification-item[data-read="false"]').length;
      updateUnreadBadge(unreadCount);
    }, 300);
  } catch (error) {
    console.error('Failed to dismiss notification:', error);
  }
}

// Update unread badge
function updateUnreadBadge(count = null) {
  const badge = document.getElementById('notificationBadge');

  if (count === null) {
    count = document.querySelectorAll('.notification-item[data-read="false"]').length;
  }

  if (count > 0) {
    badge.textContent = count > 99 ? '99+' : count;
    badge.setAttribute('aria-label', `${count} unread notification${count !== 1 ? 's' : ''}`);
    badge.classList.remove('hidden');

    // Pulse animation on new notification
    badge.classList.add('pulse-once');
    setTimeout(() => badge.classList.remove('pulse-once'), 600);
  } else {
    badge.classList.add('hidden');
  }
}

// Add notification to dropdown (real-time)
function addNotificationToDropdown(notification) {
  const list = document.querySelector('.notification-list');

  // Remove empty state if present
  const emptyState = list.querySelector('.notification-empty');
  if (emptyState) {
    emptyState.remove();
  }

  // Create notification HTML
  const notificationHtml = renderNotification(notification);

  // Insert at top with entrance animation
  list.insertAdjacentHTML('afterbegin', notificationHtml);
  const newItem = list.firstElementChild;
  newItem.classList.add('notification-item-enter');

  // Limit to max 15 notifications
  const items = list.querySelectorAll('.notification-item');
  if (items.length > 15) {
    items[items.length - 1].remove();
  }
}

// Render notification HTML
function renderNotification(notification) {
  const iconClass = getNotificationIconClass(notification.type, notification.severity);
  const iconSvg = getNotificationIconSvg(notification.type);
  const relativeTime = getRelativeTimeString(notification.timestamp);

  return `
    <div class="notification-item" role="listitem"
         data-notification-id="${notification.id}"
         data-read="${notification.isRead ? 'true' : 'false'}"
         tabindex="0">
      <div class="notification-icon ${iconClass}">
        ${iconSvg}
      </div>
      <div class="notification-content">
        <div class="notification-title">${notification.title}</div>
        <div class="notification-message">${notification.message}</div>
        <div class="notification-meta">
          <span class="notification-timestamp" title="${notification.timestamp}">${relativeTime}</span>
          <span class="notification-type-badge">${notification.typeName}</span>
        </div>
      </div>
      <div class="notification-actions">
        <button onclick="toggleNotificationRead(this, '${notification.id}')"
                class="notification-action-btn"
                aria-label="${notification.isRead ? 'Mark as unread' : 'Mark as read'}"
                title="${notification.isRead ? 'Mark as unread' : 'Mark as read'}">
          ${/* Read/unread icons */}
        </button>
        <button onclick="dismissNotification(this, '${notification.id}')"
                class="notification-action-btn"
                aria-label="Dismiss notification"
                title="Dismiss">
          ${/* Dismiss icon */}
        </button>
      </div>
      <a href="${notification.link}" class="notification-overlay-link"
         aria-label="${notification.linkLabel}"></a>
    </div>
  `;
}
```

---

## Data Structure

### Notification Object (from API/SignalR)

```typescript
interface Notification {
  id: string;                    // GUID
  type: NotificationType;        // Enum: PerformanceAlert, BotStatus, GuildEvent, CommandError
  severity: Severity;            // Enum: Critical, Warning, Info
  title: string;                 // "High API Latency Detected"
  message: string;               // "Average response time: 850ms (threshold: 500ms)"
  typeName: string;              // "Performance Alert"
  timestamp: string;             // ISO 8601: "2026-01-05T14:32:15Z"
  isRead: boolean;               // false
  link: string;                  // "/Admin/Performance/Alerts"
  linkLabel: string;             // "View performance alerts"
  metadata?: Record<string, any>; // Optional additional data
}

enum NotificationType {
  PerformanceAlert = 0,
  BotStatus = 1,
  GuildEvent = 2,
  CommandError = 3
}

enum Severity {
  Info = 0,
  Warning = 1,
  Critical = 2
}
```

---

## API Endpoints

### GET /api/notifications

Fetch paginated notifications for current user.

**Query Parameters:**
- `page` (int, default: 1): Page number
- `pageSize` (int, default: 15): Items per page
- `unreadOnly` (bool, default: false): Filter to unread only

**Response:**

```json
{
  "items": [/* Notification[] */],
  "totalCount": 42,
  "unreadCount": 8,
  "page": 1,
  "pageSize": 15,
  "totalPages": 3
}
```

### GET /api/notifications/unread-count

Get count of unread notifications.

**Response:**

```json
{
  "count": 8
}
```

### POST /api/notifications/mark-all-read

Mark all notifications as read for current user.

**Response:** `204 No Content`

### POST /api/notifications/{id}/toggle-read

Toggle read/unread state for a notification.

**Response:**

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "isRead": true
}
```

### DELETE /api/notifications/{id}/dismiss

Dismiss (soft delete) a notification.

**Response:** `204 No Content`

---

## SignalR Hub Events

### Server → Client

**NotificationReceived**
- **Payload:** `Notification` object
- **When:** New notification created (performance alert, guild event, etc.)
- **Action:** Add to dropdown, update badge, optionally show toast

**UnreadCountChanged**
- **Payload:** `{ count: number }`
- **When:** Notification read state changes
- **Action:** Update badge count

**NotificationDismissed**
- **Payload:** `{ notificationId: string }`
- **When:** Notification dismissed from another session
- **Action:** Remove from dropdown if present

---

## Implementation Checklist

### Phase 1: UI Components
- [ ] Add bell button to navbar (between connection status and user menu)
- [ ] Implement notification dropdown HTML structure
- [ ] Add CSS classes to `site.css`
- [ ] Create empty state UI
- [ ] Implement notification item template

### Phase 2: JavaScript Functionality
- [ ] Create `notification-dropdown.js` module
- [ ] Implement open/close dropdown logic
- [ ] Add click-outside handler
- [ ] Implement keyboard navigation (Escape, Arrow keys)
- [ ] Add mark all as read functionality
- [ ] Add toggle read/unread for individual items
- [ ] Add dismiss notification functionality

### Phase 3: API Integration
- [ ] Create `NotificationsController` with endpoints
- [ ] Implement notification service in Core layer
- [ ] Add notification repository (if persisted)
- [ ] Wire up AJAX calls from JavaScript

### Phase 4: SignalR Integration
- [ ] Add SignalR event handlers for notifications
- [ ] Implement real-time notification push
- [ ] Add unread count updates via SignalR
- [ ] Test multi-tab synchronization

### Phase 5: Accessibility
- [ ] Add ARIA labels and roles
- [ ] Implement keyboard navigation
- [ ] Add screen reader announcements (aria-live)
- [ ] Test with screen reader (NVDA/JAWS)
- [ ] Add focus management

### Phase 6: Mobile Responsive
- [ ] Implement mobile full-screen overlay
- [ ] Add mobile-specific CSS
- [ ] Test touch interactions
- [ ] Ensure 44x44px touch targets

### Phase 7: Polish
- [ ] Add entrance/exit animations
- [ ] Implement badge pulse on new notifications
- [ ] Add notification sound (optional, with user preference)
- [ ] Add loading states
- [ ] Add error handling and retry logic
- [ ] Test reduced motion support

---

## Future Enhancements

### Notification Preferences

Allow users to configure which notification types they want to receive:

```html
<!-- Settings page -->
<div class="notification-preferences">
  <label class="toggle">
    <input type="checkbox" class="toggle-input" checked />
    <span class="toggle-slider"></span>
    <span class="toggle-label">Performance Alerts</span>
  </label>
  <!-- More toggle controls for each type -->
</div>
```

### Notification Grouping

Group related notifications by type or time:

```html
<!-- Grouped by type -->
<div class="notification-group">
  <div class="notification-group-header">
    <span>Performance Alerts</span>
    <span class="notification-group-count">3</span>
  </div>
  <!-- Notification items -->
</div>
```

### Notification Actions

Add inline actions for specific notification types:

```html
<!-- Example: Acknowledge alert action -->
<div class="notification-inline-actions">
  <button class="btn btn-sm btn-primary">Acknowledge</button>
  <button class="btn btn-sm btn-secondary">Snooze 1hr</button>
</div>
```

### Desktop Notifications

Request browser permission for desktop notifications:

```javascript
if ('Notification' in window && Notification.permission === 'granted') {
  new Notification('High API Latency Detected', {
    body: 'Average response time: 850ms (threshold: 500ms)',
    icon: '/favicon.ico',
    tag: 'performance-alert-12345'
  });
}
```

### Notification Filtering

Add filter controls in dropdown header:

```html
<div class="notification-filters">
  <button class="notification-filter active" data-filter="all">All</button>
  <button class="notification-filter" data-filter="unread">Unread</button>
  <button class="notification-filter" data-filter="performance">Performance</button>
  <button class="notification-filter" data-filter="guilds">Guilds</button>
</div>
```

---

## Notes

- **Global Availability:** The notification bell appears on all authenticated pages via `_Layout.cshtml`. Unauthenticated pages (Landing, Login) do not show the bell.
- **SignalR Connection Sharing:** The `DashboardHub` connection is shared across all notification features. The bell subscribes to user-specific notifications without joining additional groups.
- **Discord Snowflake IDs:** When passing Discord IDs (guild IDs, user IDs) to JavaScript, always use strings to avoid precision loss with 64-bit integers
- **Local Timezone:** Display timestamps in user's local timezone with relative time ("2 minutes ago")
- **Performance:** Limit dropdown to 15 notifications, lazy-load older notifications on "View all" page
- **Security:** Validate user permissions for notification access (users should only see notifications relevant to their guilds/permissions)
- **Persistence:** Consider storing dismissed notifications for 30 days for audit trail
- **Rate Limiting:** Throttle notification creation to prevent spam (e.g., max 1 alert per metric per 5 minutes)
- **Cross-Tab Sync:** Notifications sync across browser tabs via SignalR (all tabs receive updates)

---

## Related Documentation

- [Design System](design-system.md) - Color tokens, typography, component patterns
- [SignalR Real-Time Dashboard](../articles/signalr-realtime.md) - SignalR hub setup and event patterns
- [Bot Performance Dashboard](../articles/bot-performance-dashboard.md) - Performance alert context
- [Settings Page](../articles/settings-page.md) - User preference patterns
- [Form Implementation Standards](../articles/form-implementation-standards.md) - AJAX form patterns

---

**End of Specification**
