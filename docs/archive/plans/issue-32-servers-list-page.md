# Issue #32: Servers/Guilds List Page Prototype - Implementation Plan

**Version:** 1.0
**Created:** 2025-12-08
**Status:** Planning
**Target Framework:** HTML/CSS/JavaScript Prototype
**Epic:** #28 (Client UI Prototyping)

---

## 1. Requirement Summary

Create a fully functional HTML/CSS/JavaScript prototype for the Servers/Guilds List Page. This page displays all Discord servers where the bot is installed, featuring:

- Page header with title, server count badge, "Add Server" button, and breadcrumbs
- Search and filter bar with real-time filtering
- Responsive data table with sortable columns
- Status badges for server connection state
- Row-level actions (View Details, Configure, Leave Server)
- Pagination controls with page navigation and items-per-page selector
- Empty state display when no servers or no search results
- Mobile-responsive card layout for small screens

---

## 2. Architectural Considerations

### 2.1 Existing System Components

The prototype must integrate with:

| Component | Location | Purpose |
|-----------|----------|---------|
| Design System | `docs/design-system.md` | Color tokens, typography, spacing, component specs |
| Shared CSS | `docs/prototypes/css/main.css` | Imports all component styles |
| Tailwind Config | `docs/prototypes/css/tailwind.config.js` | Extended Tailwind with design tokens |
| Layout Components | `docs/prototypes/dashboard.html` | Navbar, sidebar, breadcrumb patterns |
| Table Components | `docs/prototypes/components/data-display/tables.html` | Table patterns and variants |
| Empty States | `docs/prototypes/feedback-empty-states.html` | Empty state patterns |

### 2.2 Design System References

| Feature | Design System Section | Line Numbers |
|---------|----------------------|--------------|
| Status Badges | Status Indicators & Badges | 1256-1364 |
| Table Structure | Tables for Data Display | 1125-1252 |
| Buttons | Buttons | 357-540 |
| Form Inputs | Form Inputs | 626-895 |
| Cards (Mobile View) | Cards and Panels | 545-622 |
| Breadcrumbs | Navigation Elements | 1064-1121 |
| Color Palette | Base Colors | 26-113 |

### 2.3 Responsive Breakpoints

Following the design system's mobile-first approach:

| Breakpoint | Width | Table Behavior |
|------------|-------|----------------|
| Mobile | < 768px | Card layout (stacked) |
| Tablet | 768px - 1023px | Table with hidden columns (ID, Join Date) |
| Desktop | >= 1024px | Full table view |

### 2.4 Accessibility Requirements

- WCAG 2.1 AA compliance
- Keyboard navigation for all interactive elements
- Focus visible states using `--color-border-focus` (#098ecf)
- Screen reader support with proper ARIA attributes
- Sortable columns announced to screen readers
- Skip links for main content

---

## 3. File Structure

### 3.1 New Files to Create

```
docs/prototypes/
+-- pages/
|   +-- servers-list.html     # Main servers list page prototype
|
+-- css/
    +-- components/
        +-- pagination.css    # Pagination component styles (if not exists)
```

### 3.2 File Naming Convention

- Page file: `servers-list.html`
- Located in new `pages/` subdirectory to separate page prototypes from component showcases

---

## 4. HTML Structure Specification

### 4.1 Page Layout

The page uses the existing layout structure from `dashboard.html`:

```html
<!DOCTYPE html>
<html lang="en">
<head>
  <!-- Standard meta tags -->
  <!-- Shared styles: css/main.css -->
  <!-- Tailwind CDN with shared config -->
</head>
<body class="bg-bg-primary text-text-primary font-sans antialiased">

  <!-- Mobile Sidebar Overlay -->
  <div id="sidebarOverlay">...</div>

  <!-- Top Navigation Bar (navbar) -->
  <nav class="navbar">...</nav>

  <!-- Sidebar Navigation -->
  <aside id="sidebar">
    <!-- Servers link should be marked as .active -->
  </aside>

  <!-- Main Content Area -->
  <main class="lg:ml-64 pt-16 min-h-screen">
    <div class="p-4 lg:p-8">

      <!-- Breadcrumb Navigation -->
      <!-- Page Header Section -->
      <!-- Search & Filter Bar -->
      <!-- Data Table (Desktop/Tablet) -->
      <!-- Card Layout (Mobile) -->
      <!-- Pagination Controls -->
      <!-- Empty State (conditional) -->

    </div>
  </main>

  <!-- JavaScript for interactivity -->
</body>
</html>
```

### 4.2 Breadcrumb Navigation

```html
<nav aria-label="Breadcrumb" class="breadcrumb mb-6">
  <ol class="breadcrumb-list flex items-center gap-2 text-sm">
    <li class="breadcrumb-item">
      <a href="dashboard.html" class="text-text-secondary hover:text-accent-blue transition-colors">Home</a>
    </li>
    <li class="breadcrumb-separator text-text-tertiary" aria-hidden="true">
      <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7" />
      </svg>
    </li>
    <li class="breadcrumb-item">
      <span class="text-text-primary font-medium" aria-current="page">Servers</span>
    </li>
  </ol>
</nav>
```

### 4.3 Page Header Section

```html
<div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 mb-6">
  <div class="flex items-center gap-3">
    <h1 class="text-2xl lg:text-3xl font-bold text-text-primary">Servers</h1>
    <span class="inline-flex items-center px-2.5 py-0.5 text-sm font-semibold text-accent-blue bg-accent-blue/10 border border-accent-blue/30 rounded-full" aria-label="15 total servers">
      15
    </span>
  </div>
  <div class="flex items-center gap-3">
    <span class="text-xs text-text-tertiary hidden sm:block">
      Last updated: <time datetime="2024-12-08T10:30:00">Today at 10:30 AM</time>
    </span>
    <button class="inline-flex items-center gap-2 px-4 py-2 bg-accent-orange hover:bg-accent-orange-hover text-white font-medium text-sm rounded-lg transition-colors">
      <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
      </svg>
      Add Server
    </button>
  </div>
</div>
```

### 4.4 Search & Filter Bar

```html
<div class="bg-bg-secondary border border-border-primary rounded-lg p-4 mb-6">
  <div class="flex flex-col md:flex-row md:items-center gap-4">

    <!-- Search Input -->
    <div class="flex-1">
      <div class="relative">
        <svg class="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-text-tertiary" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
        </svg>
        <input
          type="search"
          id="serverSearch"
          placeholder="Search servers by name or ID..."
          class="w-full pl-10 pr-4 py-2.5 text-sm bg-bg-primary border border-border-primary rounded-lg text-text-primary placeholder-text-tertiary focus:border-border-focus focus:ring-1 focus:ring-border-focus transition-colors"
          aria-label="Search servers"
        />
      </div>
    </div>

    <!-- Status Filter Dropdown -->
    <div class="w-full md:w-48">
      <select
        id="statusFilter"
        class="w-full px-3 py-2.5 text-sm bg-bg-primary border border-border-primary rounded-lg text-text-primary focus:border-border-focus focus:ring-1 focus:ring-border-focus transition-colors cursor-pointer appearance-none"
        style="background-image: url('data:image/svg+xml,%3csvg xmlns=\"http://www.w3.org/2000/svg\" fill=\"none\" viewBox=\"0 0 20 20\"%3e%3cpath stroke=\"%23a8a5a3\" stroke-linecap=\"round\" stroke-linejoin=\"round\" stroke-width=\"1.5\" d=\"M6 8l4 4 4-4\"/%3e%3c/svg%3e'); background-position: right 0.5rem center; background-repeat: no-repeat; background-size: 1.5em 1.5em;"
        aria-label="Filter by status"
      >
        <option value="">All Statuses</option>
        <option value="online">Online</option>
        <option value="offline">Offline</option>
        <option value="error">Error</option>
      </select>
    </div>

    <!-- Sort Dropdown -->
    <div class="w-full md:w-48">
      <select
        id="sortBy"
        class="w-full px-3 py-2.5 text-sm bg-bg-primary border border-border-primary rounded-lg text-text-primary focus:border-border-focus focus:ring-1 focus:ring-border-focus transition-colors cursor-pointer appearance-none"
        style="background-image: url('data:image/svg+xml,%3csvg xmlns=\"http://www.w3.org/2000/svg\" fill=\"none\" viewBox=\"0 0 20 20\"%3e%3cpath stroke=\"%23a8a5a3\" stroke-linecap=\"round\" stroke-linejoin=\"round\" stroke-width=\"1.5\" d=\"M6 8l4 4 4-4\"/%3e%3c/svg%3e'); background-position: right 0.5rem center; background-repeat: no-repeat; background-size: 1.5em 1.5em;"
        aria-label="Sort by"
      >
        <option value="name-asc">Name (A-Z)</option>
        <option value="name-desc">Name (Z-A)</option>
        <option value="members-desc">Members (High-Low)</option>
        <option value="members-asc">Members (Low-High)</option>
        <option value="joined-desc">Newest First</option>
        <option value="joined-asc">Oldest First</option>
      </select>
    </div>

    <!-- Clear Filters Button -->
    <button
      id="clearFilters"
      class="hidden md:inline-flex items-center gap-2 px-4 py-2.5 text-sm font-medium text-text-secondary hover:text-text-primary border border-border-primary hover:bg-bg-hover rounded-lg transition-colors"
      aria-label="Clear all filters"
    >
      <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
      </svg>
      Clear
    </button>

  </div>

  <!-- Active Filters Display (shown when filters are active) -->
  <div id="activeFilters" class="hidden mt-3 pt-3 border-t border-border-secondary">
    <div class="flex items-center gap-2 flex-wrap">
      <span class="text-xs text-text-tertiary">Active filters:</span>
      <!-- Filter tags will be inserted here dynamically -->
    </div>
  </div>

</div>
```

### 4.5 Data Table (Desktop/Tablet View)

```html
<div class="bg-bg-secondary border border-border-primary rounded-lg overflow-hidden hidden md:block">
  <div class="overflow-x-auto">
    <table class="w-full" id="serversTable" role="grid">
      <thead class="bg-bg-tertiary">
        <tr>
          <th scope="col" class="px-5 py-3 text-left text-xs font-semibold text-text-secondary uppercase tracking-wider cursor-pointer hover:text-text-primary group" data-sort="name" tabindex="0" role="columnheader" aria-sort="none">
            <span class="inline-flex items-center gap-1">
              Server Name
              <svg class="w-4 h-4 text-text-tertiary group-hover:text-text-secondary transition-colors sort-icon" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 9l4-4 4 4m0 6l-4 4-4-4" />
              </svg>
            </span>
          </th>
          <th scope="col" class="px-5 py-3 text-left text-xs font-semibold text-text-secondary uppercase tracking-wider hidden lg:table-cell font-mono" data-sort="id">
            Server ID
          </th>
          <th scope="col" class="px-5 py-3 text-left text-xs font-semibold text-text-secondary uppercase tracking-wider cursor-pointer hover:text-text-primary group" data-sort="members" tabindex="0" role="columnheader" aria-sort="none">
            <span class="inline-flex items-center gap-1">
              Members
              <svg class="w-4 h-4 text-text-tertiary group-hover:text-text-secondary transition-colors sort-icon" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 9l4-4 4 4m0 6l-4 4-4-4" />
              </svg>
            </span>
          </th>
          <th scope="col" class="px-5 py-3 text-left text-xs font-semibold text-text-secondary uppercase tracking-wider">
            Status
          </th>
          <th scope="col" class="px-5 py-3 text-left text-xs font-semibold text-text-secondary uppercase tracking-wider hidden lg:table-cell cursor-pointer hover:text-text-primary group" data-sort="joined" tabindex="0" role="columnheader" aria-sort="none">
            <span class="inline-flex items-center gap-1">
              Join Date
              <svg class="w-4 h-4 text-text-tertiary group-hover:text-text-secondary transition-colors sort-icon" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 9l4-4 4 4m0 6l-4 4-4-4" />
              </svg>
            </span>
          </th>
          <th scope="col" class="px-5 py-3 text-right text-xs font-semibold text-text-secondary uppercase tracking-wider">
            Actions
          </th>
        </tr>
      </thead>
      <tbody class="divide-y divide-border-primary" id="serversTableBody">
        <!-- Table rows will be generated by JavaScript -->
      </tbody>
    </table>
  </div>
</div>
```

### 4.6 Table Row Template

Each table row follows this structure:

```html
<tr class="hover:bg-bg-hover/50 transition-colors" tabindex="0" role="row">
  <!-- Server Name with Avatar -->
  <td class="px-5 py-4">
    <div class="flex items-center gap-3">
      <div class="w-10 h-10 rounded-full bg-gradient-to-br from-purple-500 to-pink-500 flex items-center justify-center text-white font-bold text-sm flex-shrink-0" aria-hidden="true">
        GC
      </div>
      <div class="min-w-0">
        <p class="font-medium text-text-primary truncate">Gaming Community</p>
        <p class="text-xs text-text-tertiary md:hidden font-mono">ID: 123456789012345678</p>
      </div>
    </div>
  </td>

  <!-- Server ID (hidden on tablet) -->
  <td class="px-5 py-4 text-text-secondary font-mono text-sm hidden lg:table-cell">
    123456789012345678
  </td>

  <!-- Member Count -->
  <td class="px-5 py-4 text-text-secondary">
    1,234
  </td>

  <!-- Status Badge -->
  <td class="px-5 py-4">
    <span class="inline-flex items-center gap-1.5 px-2.5 py-1 text-xs font-semibold text-success bg-success/10 rounded-full">
      <span class="w-1.5 h-1.5 bg-success rounded-full" aria-hidden="true"></span>
      Online
    </span>
  </td>

  <!-- Join Date (hidden on tablet) -->
  <td class="px-5 py-4 text-text-secondary text-sm hidden lg:table-cell">
    <time datetime="2024-01-15">Jan 15, 2024</time>
  </td>

  <!-- Actions -->
  <td class="px-5 py-4 text-right">
    <div class="flex items-center justify-end gap-2">
      <button class="p-1.5 text-text-secondary hover:text-accent-blue hover:bg-bg-hover rounded transition-colors" aria-label="View server details" title="View Details">
        <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
        </svg>
      </button>
      <button class="p-1.5 text-text-secondary hover:text-accent-orange hover:bg-bg-hover rounded transition-colors" aria-label="Configure server settings" title="Configure">
        <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z" />
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
        </svg>
      </button>
      <button class="p-1.5 text-text-secondary hover:text-error hover:bg-bg-hover rounded transition-colors" aria-label="Leave server" title="Leave Server">
        <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1" />
        </svg>
      </button>
    </div>
  </td>
</tr>
```

### 4.7 Mobile Card Layout

```html
<div class="md:hidden space-y-4" id="serverCards">
  <!-- Card template for each server -->
  <div class="bg-bg-secondary border border-border-primary rounded-lg p-4">
    <div class="flex items-start justify-between mb-3">
      <div class="flex items-center gap-3">
        <div class="w-10 h-10 rounded-full bg-gradient-to-br from-purple-500 to-pink-500 flex items-center justify-center text-white font-bold text-sm flex-shrink-0" aria-hidden="true">
          GC
        </div>
        <div class="min-w-0">
          <p class="font-medium text-text-primary truncate">Gaming Community</p>
          <p class="text-xs text-text-tertiary font-mono">123456789012345678</p>
        </div>
      </div>
      <span class="inline-flex items-center gap-1.5 px-2 py-0.5 text-xs font-semibold text-success bg-success/10 rounded-full">
        <span class="w-1.5 h-1.5 bg-success rounded-full" aria-hidden="true"></span>
        Online
      </span>
    </div>

    <div class="grid grid-cols-2 gap-3 mb-4 text-sm">
      <div>
        <span class="text-text-tertiary">Members:</span>
        <span class="text-text-primary ml-1">1,234</span>
      </div>
      <div>
        <span class="text-text-tertiary">Joined:</span>
        <span class="text-text-primary ml-1">Jan 15, 2024</span>
      </div>
    </div>

    <div class="flex items-center gap-2 pt-3 border-t border-border-secondary">
      <button class="flex-1 inline-flex items-center justify-center gap-2 px-3 py-2 text-sm font-medium text-text-secondary hover:text-text-primary border border-border-primary hover:bg-bg-hover rounded-lg transition-colors">
        <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
        </svg>
        View
      </button>
      <button class="flex-1 inline-flex items-center justify-center gap-2 px-3 py-2 text-sm font-medium text-text-secondary hover:text-text-primary border border-border-primary hover:bg-bg-hover rounded-lg transition-colors">
        <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z" />
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
        </svg>
        Configure
      </button>
    </div>
  </div>
</div>
```

### 4.8 Pagination Controls

```html
<div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 mt-6 px-1">
  <!-- Results Count -->
  <div class="text-sm text-text-secondary">
    Showing <span id="showingStart">1</span> to <span id="showingEnd">10</span> of <span id="totalResults">15</span> servers
  </div>

  <!-- Pagination Navigation -->
  <div class="flex items-center gap-2">
    <!-- Items Per Page Selector -->
    <div class="flex items-center gap-2 mr-4">
      <label for="perPage" class="text-sm text-text-secondary hidden sm:inline">Per page:</label>
      <select
        id="perPage"
        class="px-3 py-1.5 text-sm bg-bg-secondary border border-border-primary rounded-lg text-text-primary focus:border-border-focus focus:ring-1 focus:ring-border-focus transition-colors cursor-pointer appearance-none"
        style="background-image: url('data:image/svg+xml,%3csvg xmlns=\"http://www.w3.org/2000/svg\" fill=\"none\" viewBox=\"0 0 20 20\"%3e%3cpath stroke=\"%23a8a5a3\" stroke-linecap=\"round\" stroke-linejoin=\"round\" stroke-width=\"1.5\" d=\"M6 8l4 4 4-4\"/%3e%3c/svg%3e'); background-position: right 0.5rem center; background-repeat: no-repeat; background-size: 1.25em 1.25em; padding-right: 2rem;"
        aria-label="Items per page"
      >
        <option value="10">10</option>
        <option value="25">25</option>
        <option value="50">50</option>
      </select>
    </div>

    <!-- Previous Button -->
    <button
      id="prevPage"
      class="p-2 text-text-secondary hover:text-text-primary border border-border-primary hover:bg-bg-hover rounded-lg transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
      aria-label="Previous page"
      disabled
    >
      <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7" />
      </svg>
    </button>

    <!-- Page Numbers -->
    <div class="flex items-center gap-1" id="pageNumbers" role="navigation" aria-label="Pagination">
      <button class="px-3 py-1.5 text-sm font-medium text-white bg-accent-orange rounded-lg" aria-current="page">1</button>
      <button class="px-3 py-1.5 text-sm font-medium text-text-secondary hover:text-text-primary hover:bg-bg-hover rounded-lg transition-colors">2</button>
    </div>

    <!-- Next Button -->
    <button
      id="nextPage"
      class="p-2 text-text-secondary hover:text-text-primary border border-border-primary hover:bg-bg-hover rounded-lg transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
      aria-label="Next page"
    >
      <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7" />
      </svg>
    </button>
  </div>
</div>
```

### 4.9 Empty State

```html
<div class="bg-bg-secondary border border-border-primary rounded-lg p-12" id="emptyState" style="display: none;">
  <div class="flex flex-col items-center text-center max-w-md mx-auto">
    <div class="p-4 bg-bg-tertiary rounded-full mb-4">
      <svg class="w-16 h-16 text-text-tertiary" fill="none" viewBox="0 0 24 24" stroke="currentColor">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M5 12h14M5 12a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v4a2 2 0 01-2 2M5 12a2 2 0 00-2 2v4a2 2 0 002 2h14a2 2 0 002-2v-4a2 2 0 00-2-2m-2-4h.01M17 16h.01" />
      </svg>
    </div>
    <h3 class="text-lg font-semibold text-text-primary mb-2" id="emptyStateTitle">No Servers Found</h3>
    <p class="text-sm text-text-secondary mb-6" id="emptyStateDescription">
      Your search didn't match any servers. Try adjusting your filters or search terms.
    </p>
    <div class="flex items-center gap-3">
      <button id="clearFiltersEmpty" class="inline-flex items-center gap-2 px-5 py-2.5 bg-accent-orange hover:bg-accent-orange-hover text-white font-semibold text-sm rounded-md transition-colors">
        <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
        </svg>
        Clear Filters
      </button>
      <a href="#" class="text-sm text-accent-blue hover:text-accent-blue-hover transition-colors">
        Add Server
      </a>
    </div>
  </div>
</div>
```

---

## 5. Status Badge Specifications

### 5.1 Status Types and Styling

| Status | Color Token | Background | Text Color | Dot Color |
|--------|-------------|------------|------------|-----------|
| Online | `success` | `bg-success/10` | `text-success` | `bg-success` |
| Offline | `text-tertiary` | `bg-border-primary/50` | `text-text-tertiary` | `bg-text-tertiary` |
| Error | `error` | `bg-error/10` | `text-error` | `bg-error` |

### 5.2 Badge HTML Structure

```html
<!-- Online Status -->
<span class="inline-flex items-center gap-1.5 px-2.5 py-1 text-xs font-semibold text-success bg-success/10 rounded-full">
  <span class="w-1.5 h-1.5 bg-success rounded-full" aria-hidden="true"></span>
  Online
</span>

<!-- Offline Status -->
<span class="inline-flex items-center gap-1.5 px-2.5 py-1 text-xs font-semibold text-text-tertiary bg-border-primary/50 rounded-full">
  <span class="w-1.5 h-1.5 bg-text-tertiary rounded-full" aria-hidden="true"></span>
  Offline
</span>

<!-- Error Status -->
<span class="inline-flex items-center gap-1.5 px-2.5 py-1 text-xs font-semibold text-error bg-error/10 rounded-full">
  <span class="w-1.5 h-1.5 bg-error rounded-full" aria-hidden="true"></span>
  Error
</span>
```

---

## 6. Sample Data Specification

The prototype should include at least 15 sample server records:

```javascript
const sampleServers = [
  { id: '123456789012345678', name: 'Gaming Community', initials: 'GC', members: 1234, status: 'online', joinDate: '2024-01-15', gradient: 'from-purple-500 to-pink-500' },
  { id: '234567890123456789', name: 'Dev Community', initials: 'DC', members: 567, status: 'online', joinDate: '2024-02-20', gradient: 'from-blue-500 to-cyan-500' },
  { id: '345678901234567890', name: 'Music Lovers', initials: 'ML', members: 2891, status: 'offline', joinDate: '2023-11-08', gradient: 'from-orange-500 to-red-500' },
  { id: '456789012345678901', name: 'Art Studio', initials: 'AS', members: 342, status: 'error', joinDate: '2024-03-01', gradient: 'from-green-500 to-emerald-500' },
  { id: '567890123456789012', name: 'Tech Talk', initials: 'TT', members: 1567, status: 'online', joinDate: '2023-12-10', gradient: 'from-indigo-500 to-purple-500' },
  { id: '678901234567890123', name: 'Book Club', initials: 'BC', members: 234, status: 'online', joinDate: '2024-01-25', gradient: 'from-amber-500 to-yellow-500' },
  { id: '789012345678901234', name: 'Sports Fans', initials: 'SF', members: 4521, status: 'online', joinDate: '2023-10-15', gradient: 'from-red-500 to-pink-500' },
  { id: '890123456789012345', name: 'Movie Nights', initials: 'MN', members: 876, status: 'offline', joinDate: '2024-02-05', gradient: 'from-teal-500 to-cyan-500' },
  { id: '901234567890123456', name: 'Foodies Unite', initials: 'FU', members: 1123, status: 'online', joinDate: '2023-11-20', gradient: 'from-lime-500 to-green-500' },
  { id: '012345678901234567', name: 'Photography Club', initials: 'PC', members: 456, status: 'online', joinDate: '2024-01-08', gradient: 'from-violet-500 to-purple-500' },
  { id: '112345678901234567', name: 'Fitness Goals', initials: 'FG', members: 789, status: 'online', joinDate: '2024-02-15', gradient: 'from-rose-500 to-pink-500' },
  { id: '212345678901234567', name: 'Language Learning', initials: 'LL', members: 2345, status: 'offline', joinDate: '2023-09-01', gradient: 'from-sky-500 to-blue-500' },
  { id: '312345678901234567', name: 'Pet Lovers', initials: 'PL', members: 1678, status: 'online', joinDate: '2023-12-25', gradient: 'from-fuchsia-500 to-pink-500' },
  { id: '412345678901234567', name: 'Travel Adventures', initials: 'TA', members: 923, status: 'error', joinDate: '2024-01-30', gradient: 'from-cyan-500 to-teal-500' },
  { id: '512345678901234567', name: 'DIY Crafts', initials: 'DC', members: 567, status: 'online', joinDate: '2024-03-10', gradient: 'from-orange-500 to-amber-500' }
];
```

---

## 7. JavaScript Functionality Specification

### 7.1 Core Functions

```javascript
// State management
let currentPage = 1;
let perPage = 10;
let sortField = 'name';
let sortDirection = 'asc';
let searchQuery = '';
let statusFilter = '';

// Initialize the page
function init() {
  loadServers();
  attachEventListeners();
  updateSidebarActiveState();
}

// Load and render servers based on current filters
function loadServers() {
  let filtered = filterServers();
  let sorted = sortServers(filtered);
  let paginated = paginateServers(sorted);

  renderTable(paginated);
  renderCards(paginated);
  renderPagination(filtered.length);
  updateResultsCount(filtered.length);
  toggleEmptyState(filtered.length === 0);
}

// Filter servers by search query and status
function filterServers() {
  return sampleServers.filter(server => {
    const matchesSearch = searchQuery === '' ||
      server.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
      server.id.includes(searchQuery);
    const matchesStatus = statusFilter === '' || server.status === statusFilter;
    return matchesSearch && matchesStatus;
  });
}

// Sort servers by current sort field and direction
function sortServers(servers) {
  return [...servers].sort((a, b) => {
    let aVal = a[sortField];
    let bVal = b[sortField];

    if (sortField === 'members') {
      aVal = parseInt(aVal);
      bVal = parseInt(bVal);
    } else if (sortField === 'joinDate') {
      aVal = new Date(aVal);
      bVal = new Date(bVal);
    } else {
      aVal = aVal.toLowerCase();
      bVal = bVal.toLowerCase();
    }

    if (sortDirection === 'asc') {
      return aVal > bVal ? 1 : -1;
    } else {
      return aVal < bVal ? 1 : -1;
    }
  });
}

// Paginate servers
function paginateServers(servers) {
  const start = (currentPage - 1) * perPage;
  const end = start + perPage;
  return servers.slice(start, end);
}
```

### 7.2 Event Listeners

```javascript
function attachEventListeners() {
  // Search input with debounce
  const searchInput = document.getElementById('serverSearch');
  let debounceTimer;
  searchInput.addEventListener('input', (e) => {
    clearTimeout(debounceTimer);
    debounceTimer = setTimeout(() => {
      searchQuery = e.target.value;
      currentPage = 1;
      loadServers();
      updateActiveFilters();
    }, 300);
  });

  // Status filter
  document.getElementById('statusFilter').addEventListener('change', (e) => {
    statusFilter = e.target.value;
    currentPage = 1;
    loadServers();
    updateActiveFilters();
  });

  // Sort dropdown
  document.getElementById('sortBy').addEventListener('change', (e) => {
    const [field, direction] = e.target.value.split('-');
    sortField = field;
    sortDirection = direction;
    loadServers();
  });

  // Clear filters button
  document.getElementById('clearFilters').addEventListener('click', clearAllFilters);
  document.getElementById('clearFiltersEmpty').addEventListener('click', clearAllFilters);

  // Per page selector
  document.getElementById('perPage').addEventListener('change', (e) => {
    perPage = parseInt(e.target.value);
    currentPage = 1;
    loadServers();
  });

  // Pagination buttons
  document.getElementById('prevPage').addEventListener('click', () => {
    if (currentPage > 1) {
      currentPage--;
      loadServers();
    }
  });

  document.getElementById('nextPage').addEventListener('click', () => {
    const totalPages = Math.ceil(filterServers().length / perPage);
    if (currentPage < totalPages) {
      currentPage++;
      loadServers();
    }
  });

  // Column header sorting
  document.querySelectorAll('th[data-sort]').forEach(th => {
    th.addEventListener('click', () => {
      const field = th.dataset.sort;
      if (sortField === field) {
        sortDirection = sortDirection === 'asc' ? 'desc' : 'asc';
      } else {
        sortField = field;
        sortDirection = 'asc';
      }
      updateSortDropdown();
      loadServers();
      updateSortIndicators();
    });

    // Keyboard support for sorting
    th.addEventListener('keydown', (e) => {
      if (e.key === 'Enter' || e.key === ' ') {
        e.preventDefault();
        th.click();
      }
    });
  });
}
```

### 7.3 Render Functions

```javascript
function renderTable(servers) {
  const tbody = document.getElementById('serversTableBody');
  tbody.innerHTML = servers.map(server => `
    <tr class="hover:bg-bg-hover/50 transition-colors" tabindex="0" role="row">
      <td class="px-5 py-4">
        <div class="flex items-center gap-3">
          <div class="w-10 h-10 rounded-full bg-gradient-to-br ${server.gradient} flex items-center justify-center text-white font-bold text-sm flex-shrink-0">
            ${server.initials}
          </div>
          <div class="min-w-0">
            <p class="font-medium text-text-primary truncate">${escapeHtml(server.name)}</p>
            <p class="text-xs text-text-tertiary md:hidden font-mono">ID: ${server.id}</p>
          </div>
        </div>
      </td>
      <td class="px-5 py-4 text-text-secondary font-mono text-sm hidden lg:table-cell">${server.id}</td>
      <td class="px-5 py-4 text-text-secondary">${server.members.toLocaleString()}</td>
      <td class="px-5 py-4">${getStatusBadge(server.status)}</td>
      <td class="px-5 py-4 text-text-secondary text-sm hidden lg:table-cell">
        <time datetime="${server.joinDate}">${formatDate(server.joinDate)}</time>
      </td>
      <td class="px-5 py-4 text-right">
        <div class="flex items-center justify-end gap-2">
          ${getActionButtons()}
        </div>
      </td>
    </tr>
  `).join('');
}

function renderCards(servers) {
  const container = document.getElementById('serverCards');
  container.innerHTML = servers.map(server => `
    <div class="bg-bg-secondary border border-border-primary rounded-lg p-4">
      <div class="flex items-start justify-between mb-3">
        <div class="flex items-center gap-3">
          <div class="w-10 h-10 rounded-full bg-gradient-to-br ${server.gradient} flex items-center justify-center text-white font-bold text-sm flex-shrink-0">
            ${server.initials}
          </div>
          <div class="min-w-0">
            <p class="font-medium text-text-primary truncate">${escapeHtml(server.name)}</p>
            <p class="text-xs text-text-tertiary font-mono">${server.id}</p>
          </div>
        </div>
        ${getStatusBadge(server.status)}
      </div>
      <div class="grid grid-cols-2 gap-3 mb-4 text-sm">
        <div><span class="text-text-tertiary">Members:</span> <span class="text-text-primary ml-1">${server.members.toLocaleString()}</span></div>
        <div><span class="text-text-tertiary">Joined:</span> <span class="text-text-primary ml-1">${formatDate(server.joinDate)}</span></div>
      </div>
      <div class="flex items-center gap-2 pt-3 border-t border-border-secondary">
        ${getMobileActionButtons()}
      </div>
    </div>
  `).join('');
}
```

### 7.4 Utility Functions

```javascript
function getStatusBadge(status) {
  const badges = {
    online: '<span class="inline-flex items-center gap-1.5 px-2.5 py-1 text-xs font-semibold text-success bg-success/10 rounded-full"><span class="w-1.5 h-1.5 bg-success rounded-full"></span>Online</span>',
    offline: '<span class="inline-flex items-center gap-1.5 px-2.5 py-1 text-xs font-semibold text-text-tertiary bg-border-primary/50 rounded-full"><span class="w-1.5 h-1.5 bg-text-tertiary rounded-full"></span>Offline</span>',
    error: '<span class="inline-flex items-center gap-1.5 px-2.5 py-1 text-xs font-semibold text-error bg-error/10 rounded-full"><span class="w-1.5 h-1.5 bg-error rounded-full"></span>Error</span>'
  };
  return badges[status] || badges.offline;
}

function formatDate(dateString) {
  const date = new Date(dateString);
  return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
}

function escapeHtml(text) {
  const div = document.createElement('div');
  div.textContent = text;
  return div.innerHTML;
}

function clearAllFilters() {
  searchQuery = '';
  statusFilter = '';
  currentPage = 1;
  document.getElementById('serverSearch').value = '';
  document.getElementById('statusFilter').value = '';
  loadServers();
  updateActiveFilters();
}

function updateActiveFilters() {
  const container = document.getElementById('activeFilters');
  const hasFilters = searchQuery !== '' || statusFilter !== '';

  container.classList.toggle('hidden', !hasFilters);
  document.getElementById('clearFilters').classList.toggle('hidden', !hasFilters);

  if (hasFilters) {
    let tags = '';
    if (searchQuery) {
      tags += `<span class="inline-flex items-center gap-1 px-2 py-1 text-xs font-medium text-text-primary bg-bg-tertiary rounded-full">
        Search: "${escapeHtml(searchQuery)}"
        <button onclick="clearSearch()" class="ml-1 hover:text-error">&times;</button>
      </span>`;
    }
    if (statusFilter) {
      tags += `<span class="inline-flex items-center gap-1 px-2 py-1 text-xs font-medium text-text-primary bg-bg-tertiary rounded-full">
        Status: ${statusFilter}
        <button onclick="clearStatus()" class="ml-1 hover:text-error">&times;</button>
      </span>`;
    }
    container.querySelector('.flex').innerHTML = `<span class="text-xs text-text-tertiary">Active filters:</span>${tags}`;
  }
}
```

---

## 8. Keyboard Navigation Specification

### 8.1 Tab Order

1. Skip to main content link (hidden, appears on focus)
2. Sidebar toggle (mobile)
3. Search input
4. Status filter dropdown
5. Sort dropdown
6. Clear filters button
7. Add Server button
8. Table column headers (sortable)
9. Table rows (focusable)
10. Action buttons within rows
11. Pagination controls

### 8.2 Key Bindings

| Key | Action |
|-----|--------|
| Tab | Move focus to next focusable element |
| Shift+Tab | Move focus to previous focusable element |
| Enter/Space | Activate focused button or sortable column |
| Escape | Close mobile sidebar, clear search focus |
| Arrow Up/Down | Navigate within dropdowns |

---

## 9. Acceptance Criteria

### 9.1 Page Header
- [ ] Page title "Servers" displays correctly
- [ ] Server count badge shows correct total (updates with filters)
- [ ] "Add Server" button styled with orange accent
- [ ] Last updated timestamp displays correctly
- [ ] Breadcrumb navigation: Home > Servers

### 9.2 Search & Filter Bar
- [ ] Search input accepts text and filters in real-time (300ms debounce)
- [ ] Search is case-insensitive
- [ ] Search matches server name AND server ID
- [ ] Status filter dropdown with options: All, Online, Offline, Error
- [ ] Sort dropdown with options for name, members, join date (asc/desc)
- [ ] Clear filters button appears when filters are active
- [ ] Active filter tags display below filter bar

### 9.3 Data Table (Desktop/Tablet)
- [ ] Displays 6 columns: Server Name, Server ID, Members, Status, Join Date, Actions
- [ ] Server ID column hidden on tablet (< 1024px)
- [ ] Join Date column hidden on tablet (< 1024px)
- [ ] Server avatar with gradient background and initials
- [ ] Member count formatted with locale separators (e.g., 1,234)
- [ ] Status badges with correct colors (green/gray/red)
- [ ] Row hover state with subtle background change
- [ ] Sortable columns: Name, Members, Join Date
- [ ] Sort indicator icons update on click
- [ ] Column headers keyboard accessible

### 9.4 Table Actions
- [ ] View Details button with eye icon
- [ ] Configure button with cog icon
- [ ] Leave Server button with exit icon
- [ ] Hover states: blue for View, orange for Configure, red for Leave
- [ ] Accessible labels and tooltips

### 9.5 Mobile Card Layout
- [ ] Hidden on tablet and desktop (md:hidden)
- [ ] Visible only below 768px
- [ ] Card shows: avatar, name, ID, status badge, members, join date
- [ ] Action buttons at bottom: View, Configure
- [ ] Cards stack vertically with gap

### 9.6 Pagination
- [ ] Shows "Showing X to Y of Z servers"
- [ ] Items per page selector: 10, 25, 50
- [ ] Previous/Next buttons with disabled states
- [ ] Page number buttons with active state styling
- [ ] Current page highlighted with orange background

### 9.7 Empty State
- [ ] Displays when no servers match search/filter
- [ ] Server icon in muted style
- [ ] "No Servers Found" title
- [ ] Descriptive text suggesting filter adjustment
- [ ] "Clear Filters" button that resets all filters
- [ ] "Add Server" link

### 9.8 Accessibility
- [ ] All interactive elements keyboard accessible
- [ ] Focus visible styles using design system focus color
- [ ] ARIA labels on icon-only buttons
- [ ] aria-sort attributes on sortable columns
- [ ] Screen reader announcements for dynamic content
- [ ] Color contrast meets WCAG AA standards

### 9.9 Responsive Behavior
- [ ] Layout adapts at 768px (tablet) and 1024px (desktop) breakpoints
- [ ] Mobile: Card layout, full-width search/filters stacked vertically
- [ ] Tablet: Table with hidden ID/Date columns, filters in row
- [ ] Desktop: Full table, filters in single row

---

## 10. Implementation Notes for HTML Prototyper

### 10.1 File Setup

1. Create new file: `docs/prototypes/pages/servers-list.html`
2. Copy layout structure from `docs/prototypes/dashboard.html`
3. Update sidebar to mark "Servers" link as `.active`
4. Replace main content area with servers list components

### 10.2 CSS Integration

Include the shared styles in the same manner as existing prototypes:

```html
<head>
  <link rel="stylesheet" href="../css/main.css">
  <script src="../css/tailwind.config.js"></script>
  <script src="https://cdn.tailwindcss.com"></script>
  <script>tailwind.config = window.tailwindConfig;</script>
</head>
```

### 10.3 Design Token Usage

Always use design system tokens via Tailwind classes:

| Property | Use This | Not This |
|----------|----------|----------|
| Background | `bg-bg-primary`, `bg-bg-secondary` | `bg-gray-900` |
| Text | `text-text-primary`, `text-text-secondary` | `text-gray-100` |
| Border | `border-border-primary` | `border-gray-700` |
| Accent | `bg-accent-orange`, `text-accent-blue` | `bg-orange-600` |
| Status | `text-success`, `text-error` | `text-green-500` |

### 10.4 Gradient Backgrounds for Avatars

Use Tailwind's gradient utilities for server avatars:

```html
<div class="bg-gradient-to-br from-purple-500 to-pink-500">
```

Available gradient combinations (use variety for visual interest):
- `from-purple-500 to-pink-500`
- `from-blue-500 to-cyan-500`
- `from-orange-500 to-red-500`
- `from-green-500 to-emerald-500`
- `from-indigo-500 to-purple-500`
- `from-teal-500 to-cyan-500`

---

## 11. Timeline / Dependency Map

### Phase 1: Layout and Structure (Day 1)
- Create page file with layout from dashboard.html
- Implement breadcrumb navigation
- Implement page header section
- Update sidebar active state

### Phase 2: Filter Bar (Day 1-2)
- Implement search input with icon
- Add status filter dropdown
- Add sort dropdown
- Implement clear filters button
- Add active filters display

### Phase 3: Data Table (Day 2-3)
- Build table structure with all columns
- Implement sortable column headers
- Add table row template
- Style status badges
- Add action buttons
- Implement responsive column hiding

### Phase 4: Mobile Layout (Day 3)
- Implement card layout
- Ensure proper visibility toggling
- Test responsive breakpoints

### Phase 5: Pagination (Day 3-4)
- Add pagination UI components
- Style page number buttons
- Implement items per page selector

### Phase 6: JavaScript Functionality (Day 4-5)
- Implement sample data
- Add filtering logic
- Add sorting logic
- Add pagination logic
- Connect all event listeners
- Test keyboard navigation

### Phase 7: Empty State and Polish (Day 5)
- Implement empty state display
- Add loading states (optional enhancement)
- Final accessibility testing
- Cross-browser testing

---

## 12. Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Table responsiveness complex | Medium | Use established pattern from tables.html, hide columns progressively |
| JavaScript state management | Low | Keep state simple, use plain objects, avoid frameworks |
| Filter combinations edge cases | Low | Test all filter combinations, default to showing all |
| Mobile card layout consistency | Medium | Match existing feedback-empty-states.html card patterns |
| Keyboard navigation complexity | Medium | Follow existing dashboard.html patterns, test with screen reader |
| Performance with many rows | Low | Use pagination to limit rendered rows, debounce search |

---

## Appendix A: SVG Icons Reference

All icons use HeroIcons (outline style, 24x24 viewBox):

| Icon | Usage | Path |
|------|-------|------|
| Search | Search input | `M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z` |
| Plus | Add Server button | `M12 4v16m8-8H4` |
| X | Clear/Close | `M6 18L18 6M6 6l12 12` |
| Eye | View Details | `M15 12a3 3 0 11-6 0 3 3 0 016 0z` + `M2.458 12C3.732...` |
| Cog | Configure | `M10.325 4.317c.426-1.756...` + `M15 12a3 3 0 11-6 0 3 3 0 016 0z` |
| Logout | Leave Server | `M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6...` |
| Chevron Left | Previous page | `M15 19l-7-7 7-7` |
| Chevron Right | Next page / Breadcrumb | `M9 5l7 7-7 7` |
| Sort | Sortable column | `M8 9l4-4 4 4m0 6l-4 4-4-4` |
| Server | Empty state | `M5 12h14M5 12a2 2 0 01-2-2V6a2 2 0 012-2h14...` |

---

**Document Version:** 1.0
**Author:** Systems Architect
**Reviewers:** HTML Prototyper, Design Specialist
**Approval Status:** Ready for Implementation
