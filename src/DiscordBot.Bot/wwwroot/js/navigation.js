// Navigation JavaScript for Discord Bot Admin UI
// Handles sidebar toggle, user menu, and keyboard navigation

// Track sidebar state
let sidebarOpen = false;

// Track sidebar collapsed state (desktop only)
let sidebarCollapsed = false;

// Track viewport mode to detect threshold crossings
let isDesktopMode = window.innerWidth >= 1024;

// Debounce utility for resize handler
let resizeTimeout = null;

// LocalStorage key for persisting collapsed state
const SIDEBAR_COLLAPSED_KEY = 'sidebarCollapsed';

// Get all focusable elements within the sidebar
function getSidebarFocusableElements() {
  const sidebar = document.getElementById('sidebar');
  if (!sidebar) return [];
  return sidebar.querySelectorAll(
    'a[href], button, input, select, textarea, [tabindex]:not([tabindex="-1"])'
  );
}

// Focus trap for mobile sidebar
function trapFocus(event) {
  if (!sidebarOpen || window.innerWidth >= 1024) return;

  const focusableElements = getSidebarFocusableElements();
  if (focusableElements.length === 0) return;

  const firstFocusable = focusableElements[0];
  const lastFocusable = focusableElements[focusableElements.length - 1];

  // Handle Tab key
  if (event.key === 'Tab') {
    if (event.shiftKey) {
      // Shift + Tab: if on first element, go to last
      if (document.activeElement === firstFocusable) {
        event.preventDefault();
        lastFocusable.focus();
      }
    } else {
      // Tab: if on last element, go to first
      if (document.activeElement === lastFocusable) {
        event.preventDefault();
        firstFocusable.focus();
      }
    }
  }
}

// Mobile Sidebar Toggle
function toggleMobileSidebar() {
  const sidebar = document.getElementById('sidebar');
  const overlay = document.getElementById('mobileOverlay');
  const toggleButton = document.getElementById('sidebarToggle');

  if (!sidebar || !overlay || !toggleButton) return;

  sidebarOpen = !sidebarOpen;

  // Use explicit state-based class manipulation to avoid CSS/JS state desync
  if (sidebarOpen) {
    sidebar.classList.remove('-translate-x-full');
    overlay.classList.add('active');
  } else {
    sidebar.classList.add('-translate-x-full');
    overlay.classList.remove('active');
  }

  // Update aria-expanded state
  toggleButton.setAttribute('aria-expanded', sidebarOpen.toString());

  // Focus management
  if (sidebarOpen) {
    // Focus the first focusable element in sidebar
    const focusableElements = getSidebarFocusableElements();
    if (focusableElements.length > 0) {
      focusableElements[0].focus();
    }
  } else {
    // Return focus to the toggle button
    toggleButton.focus();
  }
}

// Desktop Sidebar Collapse Toggle
function toggleSidebarCollapse() {
  const sidebar = document.getElementById('sidebar');
  const mainContent = document.querySelector('.main-content-redesign');

  if (!sidebar || !mainContent) return;

  sidebarCollapsed = !sidebarCollapsed;

  // Toggle collapsed class
  if (sidebarCollapsed) {
    sidebar.classList.add('collapsed');
  } else {
    sidebar.classList.remove('collapsed');
  }

  // Persist state in localStorage
  try {
    localStorage.setItem(SIDEBAR_COLLAPSED_KEY, sidebarCollapsed.toString());
  } catch (e) {
    console.warn('Unable to save sidebar state to localStorage:', e);
  }
}

// Legacy function name for backward compatibility
function toggleSidebar() {
  toggleMobileSidebar();
}

// Close mobile sidebar and return focus to toggle button
function closeSidebar() {
  const sidebar = document.getElementById('sidebar');
  const overlay = document.getElementById('mobileOverlay');
  const toggleButton = document.getElementById('sidebarToggle');

  if (!sidebar || !overlay || !toggleButton) return;

  if (sidebarOpen && window.innerWidth < 1024) {
    sidebarOpen = false;
    sidebar.classList.add('-translate-x-full');
    overlay.classList.remove('active');
    toggleButton.setAttribute('aria-expanded', 'false');
    toggleButton.focus();
  }
}

// User Menu Toggle
function toggleUserMenu() {
  const menu = document.getElementById('userMenu');
  const button = document.getElementById('userMenuButton');
  if (!menu || !button) return;

  const isExpanded = menu.classList.toggle('active');
  button.setAttribute('aria-expanded', isExpanded.toString());
}

// Close dropdowns when clicking outside
document.addEventListener('click', function(event) {
  const userMenu = document.getElementById('userMenu');
  const userMenuButton = document.getElementById('userMenuButton');
  if (!userMenu) return;

  const userButton = event.target.closest('[aria-label="User menu"]');

  if (!userButton && !event.target.closest('#userMenu')) {
    userMenu.classList.remove('active');
    if (userMenuButton) {
      userMenuButton.setAttribute('aria-expanded', 'false');
    }
  }
});

// Handle sidebar state on window resize with debouncing
window.addEventListener('resize', function() {
  // Debounce resize events to prevent rapid-fire updates
  if (resizeTimeout) {
    clearTimeout(resizeTimeout);
  }

  resizeTimeout = setTimeout(function() {
    const sidebar = document.getElementById('sidebar');
    const overlay = document.getElementById('mobileOverlay');
    const toggleButton = document.getElementById('sidebarToggle');

    if (!sidebar || !overlay || !toggleButton) return;

    const nowDesktop = window.innerWidth >= 1024;

    // Only act when crossing the mobile/desktop threshold
    if (nowDesktop !== isDesktopMode) {
      isDesktopMode = nowDesktop;

      if (nowDesktop) {
        // Crossed TO desktop: reset mobile state, hide overlay
        // Don't touch -translate-x-full - CSS lg:translate-x-0 handles visibility
        sidebarOpen = false;
        overlay.classList.remove('active');
        toggleButton.setAttribute('aria-expanded', 'false');
      } else {
        // Crossed TO mobile: ensure sidebar is properly hidden
        sidebarOpen = false;
        sidebar.classList.add('-translate-x-full');
        overlay.classList.remove('active');
        toggleButton.setAttribute('aria-expanded', 'false');
      }
    }
  }, 100); // 100ms debounce delay
});

// Keyboard navigation for accessibility
document.addEventListener('keydown', function(event) {
  // Handle focus trap in sidebar
  trapFocus(event);

  // Close menus on Escape
  if (event.key === 'Escape') {
    const userMenu = document.getElementById('userMenu');
    const userMenuButton = document.getElementById('userMenuButton');
    if (userMenu) {
      userMenu.classList.remove('active');
      if (userMenuButton) {
        userMenuButton.setAttribute('aria-expanded', 'false');
        // Return focus to the menu button for better keyboard navigation
        userMenuButton.focus();
      }
    }

    // Close mobile sidebar and return focus to toggle button
    closeSidebar();
  }
});

// Initialize sidebar state on page load
document.addEventListener('DOMContentLoaded', function() {
  // Restore sidebar collapsed state from localStorage
  try {
    const savedState = localStorage.getItem(SIDEBAR_COLLAPSED_KEY);
    if (savedState === 'true' && window.innerWidth >= 1024) {
      const sidebar = document.getElementById('sidebar');
      if (sidebar) {
        sidebarCollapsed = true;
        sidebar.classList.add('collapsed');
      }
    }
  } catch (e) {
    console.warn('Unable to restore sidebar state from localStorage:', e);
  }
});
