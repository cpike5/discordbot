// Navigation JavaScript for Discord Bot Admin UI
// Handles sidebar toggle, user menu, and keyboard navigation

// Track sidebar state
let sidebarOpen = false;

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

// Sidebar Toggle (Mobile)
function toggleSidebar() {
  const sidebar = document.getElementById('sidebar');
  const overlay = document.getElementById('sidebarOverlay');
  const toggleButton = document.getElementById('sidebarToggle');

  if (!sidebar || !overlay || !toggleButton) return;

  sidebarOpen = !sidebarOpen;

  // Use explicit state-based class manipulation to avoid CSS/JS state desync
  if (sidebarOpen) {
    sidebar.classList.remove('-translate-x-full');
    overlay.classList.remove('hidden');
  } else {
    sidebar.classList.add('-translate-x-full');
    overlay.classList.add('hidden');
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

// Close sidebar and return focus to toggle button
function closeSidebar() {
  const sidebar = document.getElementById('sidebar');
  const overlay = document.getElementById('sidebarOverlay');
  const toggleButton = document.getElementById('sidebarToggle');

  if (!sidebar || !overlay || !toggleButton) return;

  if (sidebarOpen && window.innerWidth < 1024) {
    sidebarOpen = false;
    sidebar.classList.add('-translate-x-full');
    overlay.classList.add('hidden');
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

// Handle sidebar state on window resize
window.addEventListener('resize', function() {
  const sidebar = document.getElementById('sidebar');
  const overlay = document.getElementById('sidebarOverlay');
  const toggleButton = document.getElementById('sidebarToggle');

  if (!sidebar || !overlay || !toggleButton) return;

  if (window.innerWidth >= 1024) {
    // Desktop: show sidebar, hide overlay, reset state
    sidebarOpen = false;
    sidebar.classList.remove('-translate-x-full');
    overlay.classList.add('hidden');
    toggleButton.setAttribute('aria-expanded', 'false');
  } else {
    // Mobile: ensure sidebar is hidden if not explicitly opened
    if (!sidebarOpen) {
      sidebar.classList.add('-translate-x-full');
      overlay.classList.add('hidden');
    }
  }
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
