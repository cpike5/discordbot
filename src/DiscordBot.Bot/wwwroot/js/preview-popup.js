/**
 * Preview Popup Module
 * Handles user and guild preview popups on hover/click/focus
 *
 * Usage:
 *   Add data attributes to trigger elements:
 *   - data-preview-type="user" data-user-id="123456789"
 *   - data-preview-type="guild" data-guild-id="987654321"
 *   - data-context-guild-id="..." (optional, for user previews with guild context)
 *
 *   Add .preview-trigger class for styling
 */
const PreviewPopup = (() => {
    // Configuration
    const CONFIG = {
        hoverDelay: 300,         // ms before showing popup on hover
        hideDelay: 150,          // ms before hiding on mouse leave
        cacheTtl: 5 * 60 * 1000, // 5 minute cache
        userPopupWidth: 288,     // 18rem = 288px
        guildPopupWidth: 320,    // 20rem = 320px
        viewportPadding: 16,     // Minimum distance from viewport edges
        minTouchTarget: 44       // Minimum touch target size in pixels (WCAG 2.5.5)
    };

    // State
    let activePopup = null;
    let activeTrigger = null;
    let lastFocusedTrigger = null; // Track last focused trigger for focus return
    let hoverTimer = null;
    let hideTimer = null;
    let isTouchDeviceCache = null; // Cache touch device detection

    // Cache module
    const PreviewCache = (() => {
        const cache = new Map();
        let cleanupInterval = null;

        function buildKey(type, id, context) {
            return context
                ? `${type}:${id}:${context}`
                : `${type}:${id}`;
        }

        function get(type, id, context = null) {
            const key = buildKey(type, id, context);
            const entry = cache.get(key);

            if (!entry) return null;

            if (Date.now() - entry.timestamp > CONFIG.cacheTtl) {
                cache.delete(key);
                return null;
            }

            return entry.data;
        }

        function set(type, id, data, context = null) {
            const key = buildKey(type, id, context);
            cache.set(key, {
                data,
                timestamp: Date.now()
            });
        }

        function clear() {
            cache.clear();
        }

        function clearExpired() {
            const now = Date.now();
            for (const [key, entry] of cache) {
                if (now - entry.timestamp > CONFIG.cacheTtl) {
                    cache.delete(key);
                }
            }
        }

        function getStats() {
            const stats = {
                entries: cache.size,
                users: 0,
                guilds: 0,
                expired: 0
            };

            const now = Date.now();
            for (const [key, entry] of cache) {
                if (key.startsWith('user:')) stats.users++;
                if (key.startsWith('guild:')) stats.guilds++;
                if (now - entry.timestamp > CONFIG.cacheTtl) stats.expired++;
            }

            return stats;
        }

        function startCleanup() {
            if (!cleanupInterval) {
                cleanupInterval = setInterval(clearExpired, 60000); // Every minute
            }
        }

        function stopCleanup() {
            if (cleanupInterval) {
                clearInterval(cleanupInterval);
                cleanupInterval = null;
            }
        }

        return { get, set, clear, clearExpired, getStats, startCleanup, stopCleanup };
    })();

    // API endpoints
    const API = {
        userPreview: (userId, guildId) => guildId
            ? `/api/preview/users/${userId}/guild/${guildId}`
            : `/api/preview/users/${userId}`,
        guildPreview: (guildId) => `/api/preview/guilds/${guildId}`
    };

    /**
     * Fetch user preview data from API
     */
    async function fetchUserPreview(userId, guildId = null) {
        // Check cache first
        const cached = PreviewCache.get('user', userId, guildId || 'global');
        if (cached) {
            return cached;
        }

        const response = await fetch(API.userPreview(userId, guildId));
        if (!response.ok) {
            throw new Error(response.status === 404 ? 'User not found' : 'Failed to fetch user preview');
        }

        const data = await response.json();

        // Store in cache
        PreviewCache.set('user', userId, data, guildId || 'global');
        return data;
    }

    /**
     * Fetch guild preview data from API
     */
    async function fetchGuildPreview(guildId) {
        // Check cache first
        const cached = PreviewCache.get('guild', guildId);
        if (cached) {
            return cached;
        }

        const response = await fetch(API.guildPreview(guildId));
        if (!response.ok) {
            throw new Error(response.status === 404 ? 'Guild not found' : 'Failed to fetch guild preview');
        }

        const data = await response.json();

        // Store in cache
        PreviewCache.set('guild', guildId, data);
        return data;
    }

    /**
     * Create the popup DOM element
     */
    function createPopupElement(type) {
        const popup = document.createElement('div');
        const width = type === 'user' ? 'w-72' : 'w-80';
        popup.className = `preview-popup-container fixed z-[1100] bg-bg-tertiary border border-border-primary rounded-lg ${width} shadow-xl overflow-hidden opacity-0 transform -translate-y-1 scale-[0.98] transition-all duration-150 ease-out`;
        popup.setAttribute('role', 'dialog');
        popup.setAttribute('aria-label', type === 'user' ? 'User preview' : 'Guild preview');
        return popup;
    }

    /**
     * Position popup relative to trigger element
     */
    function positionPopup(popup, trigger) {
        const triggerRect = trigger.getBoundingClientRect();
        const isGuild = popup.classList.contains('w-80');
        const popupWidth = isGuild ? CONFIG.guildPopupWidth : CONFIG.userPopupWidth;
        const popupHeight = 220; // Approximate height

        let left = triggerRect.left;
        let top = triggerRect.bottom + 8;

        // Check right edge
        if (left + popupWidth > window.innerWidth - CONFIG.viewportPadding) {
            left = window.innerWidth - popupWidth - CONFIG.viewportPadding;
        }

        // Check left edge
        if (left < CONFIG.viewportPadding) {
            left = CONFIG.viewportPadding;
        }

        // Check bottom edge - flip to top if needed
        if (top + popupHeight > window.innerHeight - CONFIG.viewportPadding) {
            top = triggerRect.top - popupHeight - 8;
        }

        // Ensure top isn't negative
        if (top < CONFIG.viewportPadding) {
            top = CONFIG.viewportPadding;
        }

        popup.style.left = `${left}px`;
        popup.style.top = `${top}px`;
    }

    /**
     * Render loading skeleton in popup
     */
    function renderLoading(popup, type) {
        const avatarSize = type === 'guild' ? 'w-14 h-14 rounded-lg' : 'w-12 h-12 rounded-full';
        popup.innerHTML = `
            <div class="preview-header flex items-center gap-3 p-4 border-b border-border-secondary bg-bg-secondary">
                <div class="${avatarSize} bg-bg-hover animate-pulse"></div>
                <div class="flex-1 space-y-2">
                    <div class="h-4 w-24 bg-bg-hover rounded animate-pulse"></div>
                    <div class="h-3 w-16 bg-bg-hover rounded animate-pulse"></div>
                </div>
            </div>
            <div class="preview-body p-4 space-y-2">
                <div class="h-3 w-full bg-bg-hover rounded animate-pulse"></div>
                <div class="h-3 w-3/4 bg-bg-hover rounded animate-pulse"></div>
                <div class="h-3 w-1/2 bg-bg-hover rounded animate-pulse"></div>
            </div>
            <div class="preview-footer flex gap-2 p-3 border-t border-border-secondary bg-bg-secondary">
                <div class="flex-1 h-7 bg-bg-hover rounded animate-pulse"></div>
                <div class="flex-1 h-7 bg-bg-hover rounded animate-pulse"></div>
            </div>
        `;
    }

    /**
     * Render user preview content
     */
    function renderUserContent(popup, data) {
        const avatarHtml = data.avatarUrl
            ? `<img src="${escapeHtml(data.avatarUrl)}" alt="${escapeHtml(data.username)}" class="w-12 h-12 rounded-full object-cover" />`
            : `<div class="w-12 h-12 rounded-full bg-accent-blue-muted flex items-center justify-center">
                   <span class="text-lg font-semibold text-accent-blue">${escapeHtml(data.username.charAt(0).toUpperCase())}</span>
               </div>`;

        const displayNameHtml = data.displayName && data.displayName !== data.username
            ? `<span class="block text-xs text-text-secondary truncate">${escapeHtml(data.displayName)}</span>`
            : '';

        const verifiedBadge = data.isVerified
            ? `<span class="px-1.5 py-0.5 bg-success-bg text-success text-xs font-medium rounded" title="Verified">
                   <svg class="w-3.5 h-3.5 inline" fill="currentColor" viewBox="0 0 20 20">
                       <path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clip-rule="evenodd"/>
                   </svg>
               </span>`
            : '';

        // Build meta items
        let metaItems = '';
        if (data.memberSince) {
            const memberDate = new Date(data.memberSince);
            metaItems += `
                <div class="flex justify-between">
                    <span class="text-text-tertiary">Member since</span>
                    <span class="text-text-secondary">${memberDate.toLocaleDateString('en-US', { month: 'short', year: 'numeric' })}</span>
                </div>`;
        }
        if (data.roles && data.roles.length > 0) {
            const rolesDisplay = data.roles.slice(0, 3).join(', ') + (data.roles.length > 3 ? ` +${data.roles.length - 3}` : '');
            metaItems += `
                <div class="flex justify-between items-start">
                    <span class="text-text-tertiary">Roles</span>
                    <span class="text-text-secondary text-right max-w-[140px] truncate">${escapeHtml(rolesDisplay)}</span>
                </div>`;
        }
        if (data.lastActive) {
            const lastActiveDate = new Date(data.lastActive);
            metaItems += `
                <div class="flex justify-between">
                    <span class="text-text-tertiary">Last active</span>
                    <span class="text-text-secondary">${formatRelativeTime(lastActiveDate)}</span>
                </div>`;
        }

        // Build profile URL - use current guild context if available
        const guildId = activeTrigger?.dataset.contextGuildId;
        const profileUrl = guildId
            ? `/Guilds/${guildId}/Members/${data.userId}/Moderation`
            : `/Admin/Users/Details?id=${data.userId}`;

        const modHistoryUrl = guildId ? `/Guilds/${guildId}/Members/${data.userId}/Moderation` : null;
        const modHistoryHtml = modHistoryUrl
            ? `<a href="${modHistoryUrl}" class="flex-1 min-w-0 px-3 py-1.5 text-xs font-medium text-text-secondary bg-bg-tertiary border border-border-primary rounded hover:bg-bg-hover transition-colors text-center inline-flex items-center justify-center gap-1 whitespace-nowrap">
                   Mod History
                   ${data.hasActiveModeration ? '<span class="w-1.5 h-1.5 bg-warning rounded-full flex-shrink-0" title="Active case"></span>' : ''}
               </a>`
            : '';

        popup.innerHTML = `
            <div class="preview-header flex items-center gap-3 p-4 border-b border-border-secondary bg-bg-secondary">
                ${avatarHtml}
                <div class="flex-1 min-w-0">
                    <span class="block text-sm font-semibold text-text-primary truncate">${escapeHtml(data.username)}</span>
                    ${displayNameHtml}
                </div>
                ${verifiedBadge}
            </div>
            <div class="preview-body p-4">
                <div class="space-y-2 text-xs">
                    ${metaItems}
                </div>
            </div>
            <div class="preview-footer flex items-center gap-2 p-3 border-t border-border-secondary bg-bg-secondary flex-wrap">
                <a href="${profileUrl}" class="flex-1 min-w-0 px-3 py-1.5 text-xs font-medium text-white bg-accent-blue rounded hover:bg-accent-blue-hover transition-colors text-center whitespace-nowrap">
                    View Profile
                </a>
                ${modHistoryHtml}
            </div>
        `;
    }

    /**
     * Render guild preview content
     */
    function renderGuildContent(popup, data) {
        const iconHtml = data.iconUrl
            ? `<img src="${escapeHtml(data.iconUrl)}" alt="${escapeHtml(data.name)}" class="w-14 h-14 rounded-lg object-cover" />`
            : `<div class="w-14 h-14 rounded-lg bg-accent-blue-muted flex items-center justify-center">
                   <span class="text-xl font-semibold text-accent-blue">${escapeHtml(data.name.charAt(0).toUpperCase())}</span>
               </div>`;

        const onlineCount = data.onlineMemberCount
            ? ` <span class="text-text-tertiary">(${data.onlineMemberCount.toLocaleString()} online)</span>`
            : '';

        const inactiveBadge = !data.isActive
            ? '<span class="px-1.5 py-0.5 bg-warning-bg text-warning text-xs font-medium rounded">Inactive</span>'
            : '';

        // Build meta items
        let metaItems = `
            <div class="flex justify-between">
                <span class="text-text-tertiary">Owner</span>
                <span class="text-accent-blue">${escapeHtml(data.ownerUsername)}</span>
            </div>
            <div class="flex justify-between">
                <span class="text-text-tertiary">Bot joined</span>
                <span class="text-text-secondary">${new Date(data.botJoinedAt).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })}</span>
            </div>`;

        if (data.activeFeatures && data.activeFeatures.length > 0) {
            const featureBadges = data.activeFeatures.slice(0, 4).map(feature => {
                const badgeClass = getFeatureBadgeClass(feature);
                return `<span class="px-1.5 py-0.5 text-[10px] font-medium rounded ${badgeClass}">${escapeHtml(feature)}</span>`;
            }).join('');
            const extraBadge = data.activeFeatures.length > 4
                ? `<span class="px-1.5 py-0.5 text-[10px] font-medium rounded bg-bg-hover text-text-tertiary">+${data.activeFeatures.length - 4}</span>`
                : '';

            metaItems += `
                <div class="flex justify-between items-start">
                    <span class="text-text-tertiary">Features</span>
                    <div class="flex flex-wrap gap-1 justify-end max-w-[180px]">
                        ${featureBadges}${extraBadge}
                    </div>
                </div>`;
        }

        popup.innerHTML = `
            <div class="preview-header flex items-center gap-3 p-4 border-b border-border-secondary bg-bg-secondary">
                ${iconHtml}
                <div class="flex-1 min-w-0">
                    <span class="block text-sm font-semibold text-text-primary truncate">${escapeHtml(data.name)}</span>
                    <span class="block text-xs text-text-secondary">
                        ${data.memberCount.toLocaleString()} members${onlineCount}
                    </span>
                </div>
                ${inactiveBadge}
            </div>
            <div class="preview-body p-4">
                <div class="space-y-2 text-xs">
                    ${metaItems}
                </div>
            </div>
            <div class="preview-footer flex items-center gap-2 p-3 border-t border-border-secondary bg-bg-secondary flex-wrap">
                <a href="/Guilds/Details/${data.guildId}" class="flex-1 min-w-0 px-3 py-1.5 text-xs font-medium text-white bg-accent-blue rounded hover:bg-accent-blue-hover transition-colors text-center whitespace-nowrap">
                    View Guild
                </a>
                <a href="/Guilds/Edit/${data.guildId}" class="flex-1 min-w-0 px-3 py-1.5 text-xs font-medium text-text-secondary bg-bg-tertiary border border-border-primary rounded hover:bg-bg-hover transition-colors text-center whitespace-nowrap">
                    Settings
                </a>
            </div>
        `;
    }

    /**
     * Render error state
     */
    function renderError(popup, type) {
        const errorTitle = type === 'guild' ? 'Guild Unavailable' : 'User Not Found';
        const errorDescription = type === 'guild'
            ? 'The bot may have been removed from this server or the server was deleted.'
            : 'This user may have been deleted or is unavailable.';

        popup.innerHTML = `
            <div class="p-4">
                <div class="text-center py-4">
                    <div class="w-12 h-12 mx-auto mb-3 rounded-full bg-error-bg flex items-center justify-center">
                        <svg class="w-6 h-6 text-error" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
                        </svg>
                    </div>
                    <p class="text-sm font-medium text-text-primary mb-1">${errorTitle}</p>
                    <p class="text-xs text-text-tertiary">${errorDescription}</p>
                </div>
            </div>
        `;
    }

    /**
     * Get badge class for feature type
     */
    function getFeatureBadgeClass(feature) {
        const featureLower = feature.toLowerCase();
        if (featureLower === 'moderation' || featureLower === 'automod') {
            return 'bg-accent-orange-muted text-accent-orange';
        }
        if (featureLower === 'ratwatch' || featureLower === 'rat watch') {
            return 'bg-success-bg text-success';
        }
        if (featureLower === 'welcome') {
            return 'bg-accent-blue-muted text-accent-blue';
        }
        if (featureLower === 'logging') {
            return 'bg-info-bg text-info';
        }
        return 'bg-bg-hover text-text-secondary';
    }

    /**
     * Format relative time (e.g., "2 hours ago")
     */
    function formatRelativeTime(date) {
        const now = new Date();
        const diffMs = now - date;
        const diffSec = Math.floor(diffMs / 1000);
        const diffMin = Math.floor(diffSec / 60);
        const diffHour = Math.floor(diffMin / 60);
        const diffDay = Math.floor(diffHour / 24);

        if (diffSec < 60) return 'Just now';
        if (diffMin < 60) return `${diffMin} minute${diffMin !== 1 ? 's' : ''} ago`;
        if (diffHour < 24) return `${diffHour} hour${diffHour !== 1 ? 's' : ''} ago`;
        if (diffDay < 7) return `${diffDay} day${diffDay !== 1 ? 's' : ''} ago`;
        return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
    }

    /**
     * Escape HTML to prevent XSS
     */
    function escapeHtml(text) {
        if (!text) return '';
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    /**
     * Detect if device supports touch input
     */
    function isTouchDevice() {
        if (isTouchDeviceCache !== null) {
            return isTouchDeviceCache;
        }
        isTouchDeviceCache = 'ontouchstart' in window ||
            navigator.maxTouchPoints > 0 ||
            navigator.msMaxTouchPoints > 0;
        return isTouchDeviceCache;
    }

    /**
     * Ensure trigger has required ARIA attributes for accessibility
     */
    function ensureTriggerAccessibility(trigger) {
        // Only add tabindex if not already focusable (not a button, link, or already has tabindex)
        const tagName = trigger.tagName.toLowerCase();
        const isFocusable = tagName === 'button' || tagName === 'a' || trigger.hasAttribute('tabindex');
        if (!isFocusable) {
            trigger.setAttribute('tabindex', '0');
        }

        // Add ARIA attributes if not present
        if (!trigger.hasAttribute('role')) {
            trigger.setAttribute('role', 'button');
        }
        if (!trigger.hasAttribute('aria-haspopup')) {
            trigger.setAttribute('aria-haspopup', 'dialog');
        }
        if (!trigger.hasAttribute('aria-expanded')) {
            trigger.setAttribute('aria-expanded', 'false');
        }
    }

    /**
     * Show popup for a trigger element
     */
    async function showPopup(trigger) {
        // Hide any existing popup
        hidePopup();

        const type = trigger.dataset.previewType;
        const userId = trigger.dataset.userId;
        const guildId = trigger.dataset.guildId;
        const contextGuildId = trigger.dataset.contextGuildId;

        if (!type || (!userId && !guildId)) return;

        // Ensure trigger has accessibility attributes
        ensureTriggerAccessibility(trigger);

        activeTrigger = trigger;
        lastFocusedTrigger = trigger; // Track for focus return

        // Create and position popup
        const popup = createPopupElement(type);
        document.body.appendChild(popup);
        activePopup = popup;

        positionPopup(popup, trigger);

        // Show loading state
        renderLoading(popup, type);

        // Animate in
        requestAnimationFrame(() => {
            popup.classList.remove('opacity-0', '-translate-y-1', 'scale-[0.98]');
            popup.classList.add('opacity-100', 'translate-y-0', 'scale-100');
        });

        // Update ARIA
        trigger.setAttribute('aria-expanded', 'true');

        // Add hover listeners to popup
        popup.addEventListener('mouseenter', handlePopupMouseEnter);
        popup.addEventListener('mouseleave', handlePopupMouseLeave);

        // Fetch data
        try {
            let data;
            if (type === 'user') {
                data = await fetchUserPreview(userId, contextGuildId);
                renderUserContent(popup, data);
            } else if (type === 'guild') {
                data = await fetchGuildPreview(guildId);
                renderGuildContent(popup, data);
            }

            // Announce to screen readers
            announcePopup(type, data);
        } catch (error) {
            console.error('Preview popup error:', error);
            renderError(popup, type);
        }
    }

    /**
     * Hide the active popup
     * @param {boolean} returnFocus - Whether to return focus to the last trigger
     */
    function hidePopup(returnFocus = false) {
        if (activePopup) {
            activePopup.classList.remove('opacity-100', 'translate-y-0', 'scale-100');
            activePopup.classList.add('opacity-0', '-translate-y-1', 'scale-[0.98]');

            const popup = activePopup;
            setTimeout(() => {
                popup.remove();
            }, 150);

            activePopup = null;
        }

        if (activeTrigger) {
            activeTrigger.setAttribute('aria-expanded', 'false');
            activeTrigger = null;
        }

        // Return focus to the last trigger when explicitly requested (e.g., Escape key)
        if (returnFocus && lastFocusedTrigger && document.body.contains(lastFocusedTrigger)) {
            lastFocusedTrigger.focus();
        }
    }

    /**
     * Announce popup content to screen readers
     */
    function announcePopup(type, data) {
        let announcement;
        if (type === 'user') {
            announcement = `User preview for ${data.username}`;
            if (data.isVerified) announcement += ', verified user';
        } else {
            announcement = `Guild preview for ${data.name}, ${data.memberCount} members`;
        }

        // Use existing toast live region or create temporary one
        const liveRegion = document.getElementById('toastLiveRegion') || createLiveRegion();
        liveRegion.textContent = announcement;
        setTimeout(() => { liveRegion.textContent = ''; }, 1000);
    }

    /**
     * Create a live region for announcements if one doesn't exist
     */
    function createLiveRegion() {
        const region = document.createElement('div');
        region.id = 'previewPopupLiveRegion';
        region.setAttribute('aria-live', 'polite');
        region.setAttribute('aria-atomic', 'true');
        region.className = 'sr-only';
        document.body.appendChild(region);
        return region;
    }

    // Event handlers
    function handleMouseEnter(e) {
        const trigger = e.target.closest('[data-preview-type]');
        if (!trigger) return;

        clearTimeout(hideTimer);
        hoverTimer = setTimeout(() => showPopup(trigger), CONFIG.hoverDelay);
    }

    function handleMouseLeave(e) {
        clearTimeout(hoverTimer);
        hideTimer = setTimeout(() => {
            // Only hide if not hovering over popup
            if (activePopup && !activePopup.matches(':hover')) {
                hidePopup();
            }
        }, CONFIG.hideDelay);
    }

    function handlePopupMouseEnter() {
        clearTimeout(hideTimer);
    }

    function handlePopupMouseLeave() {
        hideTimer = setTimeout(() => hidePopup(), CONFIG.hideDelay);
    }

    function handleClick(e) {
        const trigger = e.target.closest('[data-preview-type]');
        if (!trigger) return;

        e.preventDefault();
        e.stopPropagation();

        if (activeTrigger === trigger && activePopup) {
            hidePopup();
        } else {
            clearTimeout(hoverTimer);
            showPopup(trigger);
        }
    }

    function handleFocusIn(e) {
        const trigger = e.target.closest('[data-preview-type]');
        if (!trigger) return;

        clearTimeout(hideTimer);
        hoverTimer = setTimeout(() => showPopup(trigger), CONFIG.hoverDelay);
    }

    function handleFocusOut(e) {
        clearTimeout(hoverTimer);
        // Delay to allow focus to move to popup buttons
        hideTimer = setTimeout(() => {
            if (!activePopup?.contains(document.activeElement)) {
                hidePopup();
            }
        }, 150);
    }

    function handleKeyDown(e) {
        if (e.key === 'Escape' && activePopup) {
            e.preventDefault();
            hidePopup(true); // Return focus to trigger
        }

        if ((e.key === 'Enter' || e.key === ' ') && e.target.closest('[data-preview-type]')) {
            e.preventDefault();
            handleClick(e);
        }
    }

    /**
     * Handle touch events for tap-outside-to-close
     */
    function handleTouchStart(e) {
        if (!activePopup) return;

        const touchedElement = e.target;
        const isInsidePopup = activePopup.contains(touchedElement);
        const isOnTrigger = touchedElement.closest('[data-preview-type]');

        // Close popup if touch is outside both popup and trigger
        if (!isInsidePopup && !isOnTrigger) {
            hidePopup();
        }
    }

    function handleClickOutside(e) {
        if (activePopup && !activePopup.contains(e.target) && !e.target.closest('[data-preview-type]')) {
            hidePopup();
        }
    }

    /**
     * Debounce helper for scroll events
     */
    function debounce(fn, delay) {
        let timeoutId;
        return function (...args) {
            clearTimeout(timeoutId);
            timeoutId = setTimeout(() => fn.apply(this, args), delay);
        };
    }

    /**
     * Check if element is in viewport
     */
    function isInViewport(element) {
        const rect = element.getBoundingClientRect();
        return (
            rect.top >= 0 &&
            rect.left >= 0 &&
            rect.bottom <= (window.innerHeight || document.documentElement.clientHeight) &&
            rect.right <= (window.innerWidth || document.documentElement.clientWidth)
        );
    }

    /**
     * Prefetch preview data for visible triggers (optional enhancement)
     */
    function prefetchVisiblePreviews() {
        const triggers = document.querySelectorAll('[data-preview-type]:not([data-prefetched])');
        const visibleTriggers = Array.from(triggers).filter(isInViewport);

        visibleTriggers.forEach(trigger => {
            const type = trigger.dataset.previewType;
            const userId = trigger.dataset.userId;
            const guildId = trigger.dataset.guildId;
            const contextGuildId = trigger.dataset.contextGuildId;

            if (type === 'user' && userId && !PreviewCache.get('user', userId, contextGuildId || 'global')) {
                // Low priority background fetch
                if ('requestIdleCallback' in window) {
                    requestIdleCallback(() => {
                        fetchUserPreview(userId, contextGuildId).catch(() => { });
                    });
                } else {
                    setTimeout(() => {
                        fetchUserPreview(userId, contextGuildId).catch(() => { });
                    }, 100);
                }
            }

            if (type === 'guild' && guildId && !PreviewCache.get('guild', guildId)) {
                if ('requestIdleCallback' in window) {
                    requestIdleCallback(() => {
                        fetchGuildPreview(guildId).catch(() => { });
                    });
                } else {
                    setTimeout(() => {
                        fetchGuildPreview(guildId).catch(() => { });
                    }, 100);
                }
            }

            trigger.dataset.prefetched = 'true';
        });
    }

    // Debounced prefetch for scroll events
    const debouncedPrefetch = debounce(prefetchVisiblePreviews, 500);

    /**
     * Initialize accessibility attributes on all triggers
     */
    function initTriggerAccessibility() {
        const triggers = document.querySelectorAll('[data-preview-type]');
        triggers.forEach(ensureTriggerAccessibility);
    }

    /**
     * Initialize the preview popup system
     */
    function init() {
        // Use event delegation for dynamic content
        document.addEventListener('mouseenter', handleMouseEnter, true);
        document.addEventListener('mouseleave', handleMouseLeave, true);
        document.addEventListener('click', handleClick);
        document.addEventListener('click', handleClickOutside);
        document.addEventListener('focusin', handleFocusIn);
        document.addEventListener('focusout', handleFocusOut);
        document.addEventListener('keydown', handleKeyDown);

        // Touch device support
        document.addEventListener('touchstart', handleTouchStart, { passive: true });

        // Initialize ARIA attributes on existing triggers
        initTriggerAccessibility();

        // Observe DOM for dynamically added triggers
        if (typeof MutationObserver !== 'undefined') {
            const observer = new MutationObserver((mutations) => {
                for (const mutation of mutations) {
                    for (const node of mutation.addedNodes) {
                        if (node.nodeType === Node.ELEMENT_NODE) {
                            // Check if the node itself is a trigger
                            if (node.matches && node.matches('[data-preview-type]')) {
                                ensureTriggerAccessibility(node);
                            }
                            // Check for triggers within the added subtree
                            if (node.querySelectorAll) {
                                const triggers = node.querySelectorAll('[data-preview-type]');
                                triggers.forEach(ensureTriggerAccessibility);
                            }
                        }
                    }
                }
            });
            observer.observe(document.body, { childList: true, subtree: true });
        }

        // Start periodic cache cleanup
        PreviewCache.startCleanup();

        // Prefetch visible previews after page load
        if (document.readyState === 'complete') {
            prefetchVisiblePreviews();
        } else {
            window.addEventListener('load', prefetchVisiblePreviews);
        }
        window.addEventListener('scroll', debouncedPrefetch);
    }

    /**
     * Destroy the preview popup system
     */
    function destroy() {
        document.removeEventListener('mouseenter', handleMouseEnter, true);
        document.removeEventListener('mouseleave', handleMouseLeave, true);
        document.removeEventListener('click', handleClick);
        document.removeEventListener('click', handleClickOutside);
        document.removeEventListener('focusin', handleFocusIn);
        document.removeEventListener('focusout', handleFocusOut);
        document.removeEventListener('keydown', handleKeyDown);
        document.removeEventListener('touchstart', handleTouchStart);
        window.removeEventListener('load', prefetchVisiblePreviews);
        window.removeEventListener('scroll', debouncedPrefetch);
        hidePopup();
        PreviewCache.stopCleanup();
        PreviewCache.clear();
        lastFocusedTrigger = null;
        isTouchDeviceCache = null;
    }

    // Public API
    return {
        init,
        destroy,
        showUserPreview: (userId, anchor, guildId) => {
            activeTrigger = anchor;
            return showPopup({
                dataset: { previewType: 'user', userId, contextGuildId: guildId },
                getAttribute: () => null,
                setAttribute: () => {},
                getBoundingClientRect: () => anchor.getBoundingClientRect()
            });
        },
        showGuildPreview: (guildId, anchor) => {
            activeTrigger = anchor;
            return showPopup({
                dataset: { previewType: 'guild', guildId },
                getAttribute: () => null,
                setAttribute: () => {},
                getBoundingClientRect: () => anchor.getBoundingClientRect()
            });
        },
        hide: hidePopup,
        clearCache: () => PreviewCache.clear(),
        clearExpiredCache: () => PreviewCache.clearExpired(),
        getCacheStats: () => PreviewCache.getStats(),
        prefetch: prefetchVisiblePreviews,
        isTouchDevice,
        refreshAccessibility: initTriggerAccessibility
    };
})();

// Auto-initialize on DOMContentLoaded
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', PreviewPopup.init);
} else {
    PreviewPopup.init();
}

// Export for use in other scripts
if (typeof module !== 'undefined' && module.exports) {
    module.exports = PreviewPopup;
}
