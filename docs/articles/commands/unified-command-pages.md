# Unified Command Pages Technical Implementation

**Document Version:** 1.0
**Status:** Complete (v0.12.0+)
**Last Updated:** January 2026

---

## Overview

The Commands page underwent a major redesign in Epic #1218 that consolidated three separate pages into a single unified interface with AJAX-powered tabs. This document provides comprehensive technical details for developers maintaining or extending this system.

### Purpose and Benefits

**Before:** Three separate pages (Commands, Execution Logs, Analytics) with independent navigation and state management, requiring full page reloads.

**After:** Single `/Commands` page with three tabs (Command List, Execution Logs, Analytics) featuring:
- Lazy-loaded tab content via AJAX (only command-list tab loads on initial page)
- Sophisticated filter system with date range presets
- URL-based state persistence for shareable links and browser back/forward support
- Coordinated pagination and filtering across all tabs
- Smooth, responsive user experience without full page reloads

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     Commands Page (/Commands)               │
├─────────────────────────────────────────────────────────────┤
│                                                               │
│  ┌────────────────────────────────────────────────────────┐  │
│  │  Tab Navigation (Tab Panel Component)                  │  │
│  │  [Command List] [Execution Logs] [Analytics]          │  │
│  └────────────────────────────────────────────────────────┘  │
│                                                               │
│  ┌────────────────────────────────────────────────────────┐  │
│  │  Command List Tab (Server-Rendered)                    │  │
│  │  - Module cards with expand/collapse                   │  │
│  │  - Command metadata, parameters, preconditions        │  │
│  └────────────────────────────────────────────────────────┘  │
│                                                               │
│  ┌────────────────────────────────────────────────────────┐  │
│  │  Execution Logs Tab (AJAX-Loaded)                      │  │
│  │  ┌──────────────────────────────────────────────────┐  │  │
│  │  │ Filter Panel (6 filters)                          │  │  │
│  │  │ [Quick Presets] [Date Range] [Guild] [Command]   │  │  │
│  │  │ [Search] [Status]                                 │  │  │
│  │  └──────────────────────────────────────────────────┘  │  │
│  │  ┌──────────────────────────────────────────────────┐  │  │
│  │  │ Results Table with Pagination                     │  │  │
│  │  └──────────────────────────────────────────────────┘  │  │
│  └────────────────────────────────────────────────────────┘  │
│                                                               │
│  ┌────────────────────────────────────────────────────────┐  │
│  │  Analytics Tab (AJAX-Loaded)                           │  │
│  │  ┌──────────────────────────────────────────────────┐  │  │
│  │  │ Filter Panel (3 filters)                          │  │  │
│  │  │ [Quick Presets] [Date Range] [Guild]             │  │  │
│  │  └──────────────────────────────────────────────────┘  │  │
│  │  ┌──────────────────────────────────────────────────┐  │  │
│  │  │ Charts and Metrics                                │  │  │
│  │  └──────────────────────────────────────────────────┘  │  │
│  └────────────────────────────────────────────────────────┘  │
│                                                               │
└─────────────────────────────────────────────────────────────┘

                        API Layer
┌─────────────────────────────────────────────────────────────┐
│  CommandsApiController                                       │
├─────────────────────────────────────────────────────────────┤
│  GET /api/commands/list    → Command List Partial (static)  │
│  GET /api/commands/logs    → Execution Logs Partial (AJAX)  │
│  GET /api/commands/analytics → Analytics Partial (AJAX)    │
│  GET /api/commands/log-details/{id} → Modal Content         │
└─────────────────────────────────────────────────────────────┘

                     JavaScript Modules
┌─────────────────────────────────────────────────────────────┐
│  command-tabs.js      - Tab navigation and UI updates       │
│  command-tab-loader.js - AJAX content loading               │
│  command-filters.js   - Filter form submission              │
│  command-pagination.js - Pagination handling                │
│  url-state.js         - URL parameter management            │
│  date-range-filter.js - Date preset functionality           │
│  command-log-modal.js - Details modal                       │
│  command-loading-states.js - Loading UI states              │
│  command-error-handler.js - Error management                │
└─────────────────────────────────────────────────────────────┘
```

---

## Architecture

### Page Structure

The Commands page (`Pages/Commands/Index.cshtml`) contains:

1. **Server-rendered content:**
   - Command List tab: All command modules and commands (always visible)
   - Filter panels for both Execution Logs and Analytics tabs (initially hidden)
   - Tab navigation interface

2. **AJAX-loaded content:**
   - Execution Logs tab: Loads on first click
   - Analytics tab: Loads on first click
   - These tabs include their own filter panels and content

3. **Modals:**
   - Command log details modal (for viewing full log entry)
   - Clear commands confirmation modal (admin action)

### Server-Side vs Client-Side Rendering

| Content | Rendered By | When | Why |
|---------|-------------|------|-----|
| Command List | Server (Razor) | Page load | Performance: all commands needed at once |
| Execution Logs tab | API endpoint | Tab click | Lazy loading: reduce initial page size |
| Analytics tab | API endpoint | Tab click | Lazy loading: reduce initial page size |
| Filter panels | Server (Razor) | Page load | Needed for form submission, no data dependency |
| Tab content | API endpoint | AJAX | Dynamic filtering/pagination |

### API Endpoint Design

Three main API endpoints in `CommandsApiController` serve tab content:

```
GET /api/commands/list
  Purpose: Returns the Command List tab content (Razor partial)
  Parameters: None
  Returns: HTML partial with command modules and commands
  Notes: Returns static content from InteractionService

GET /api/commands/logs
  Purpose: Returns the Execution Logs tab content with results
  Parameters: ?StartDate=...&EndDate=...&GuildId=...&CommandName=...&SearchTerm=...&StatusFilter=...&pageNumber=...
  Returns: HTML partial with filtered logs and pagination
  Notes: Supports filtering and pagination

GET /api/commands/analytics
  Purpose: Returns the Analytics tab content with charts
  Parameters: ?StartDate=...&EndDate=...&GuildId=...
  Returns: HTML partial with analytics data and embedded scripts
  Notes: Includes Chart.js initialization scripts
```

### JavaScript Module Organization

All JavaScript modules follow the IIFE pattern (Immediately Invoked Function Expression) to avoid global scope pollution. Each module manages its own state and exposes a public API via `window.ModuleName`.

**Module Dependencies:**
```
url-state.js (standalone)
    ↓
command-tab-loader.js (depends on tab-panel.js)
    ↓
command-tabs.js (depends on command-tab-loader.js)
command-filters.js (depends on command-tab-loader.js)
command-pagination.js (depends on command-tab-loader.js)
date-range-filter.js (depends on command-tab-loader.js)
command-log-modal.js (standalone)
command-loading-states.js (depends on LoadingManager)
command-error-handler.js (depends on ToastManager)
```

---

## JavaScript Modules Reference

### 1. command-tabs.js

**Purpose:** Updates page header, subtitle, and breadcrumb when users switch tabs.

**Responsibilities:**
- Listen for tab change events from TabPanel component
- Update subtitle text based on active tab
- Update breadcrumb navigation

**Public API:**
```javascript
window.CommandTabs = {
    init(),                           // Initialize module (auto-called)
    updatePageElements(tabId),        // Update subtitle and breadcrumb
    updateSubtitle(tabId),            // Update page subtitle
    updateBreadcrumb(tabId),          // Update breadcrumb navigation
    loadExecutionLogsPage(pageNumber), // Navigate to specific page
    clearExecutionLogsFilters()        // Clear all filters
}
```

**Events:**
- Listens: `tabchange` event from TabPanel
- Emits: None

**Key Implementation Details:**
- Uses data attributes for mapping (`[data-command-subtitle]`, `[data-command-breadcrumb-active]`)
- Maps tab IDs to display text via `subtitleMap` and `breadcrumbMap`

---

### 2. command-tab-loader.js

**Purpose:** Core AJAX tab content loader. Handles lazy loading of Execution Logs and Analytics tabs.

**Responsibilities:**
- Load tab content via AJAX when tab is clicked
- Manage loaded state to prevent redundant loads
- Execute scripts in AJAX-loaded content
- Handle request cancellation and errors
- Coordinate with filter and pagination modules

**Public API:**
```javascript
window.CommandTabLoader = {
    init(options),               // Initialize module
    reloadActiveTab(filters),    // Reload current tab with filters
    reloadTab(tabId, filters),   // Reload specific tab
    getActiveTab(),              // Get current tab ID
    isTabLoaded(tabId),          // Check if tab cached
    clearLoadedState(tabId),     // Clear cache (force reload)
    isLoading(),                 // Check if load in progress
    destroy()                    // Cleanup
}
```

**Callbacks:**
```javascript
init({
    onTabChange(tabId, previousTabId),    // Tab switched
    onLoadStart(tabId),                   // Load started
    onLoadComplete(tabId, html),          // Load completed
    onError(error, tabId)                 // Load failed
})
```

**Key Implementation Details:**
- Uses `AbortController` to cancel previous requests when switching tabs
- Maps tab IDs to API routes via `apiRouteMap`
- Reads filter form values when building API URLs
- Manually executes `<script>` tags from AJAX response (CSP-compliant)
- Shows error state with retry button in UI

**Important Note:** The tab loader reads from form elements (`#executionLogsFilterForm`, `#analyticsFilterForm`) to build query parameters. This means filters must be present in the HTML even if tabs aren't loaded yet.

---

### 3. command-filters.js

**Purpose:** Handles filter form submission and state management.

**Responsibilities:**
- Listen for form submissions and input changes
- Apply date presets when buttons clicked
- Debounce search input (300ms delay)
- Submit filters via AJAX (coordinates with tab loader)
- Handle clear filters action

**Public API:**
```javascript
window.CommandFilters = {
    init(options),      // Initialize module
    setFilters(obj),    // Programmatically set filter values
    getFilters(),       // Get current filter values
    clearFilters(),     // Reset all filters
    applyFilters(),     // Trigger submission
    destroy()           // Cleanup
}
```

**Callbacks:**
```javascript
init({
    formSelector: '#executionLogsFilterForm, #analyticsFilterForm',
    debounceDelay: 300,
    onFiltersApplied(filterData),   // Filters submitted
    onFiltersClear(),               // Filters cleared
    onSearchChange(searchValue)     // Search input changed
})
```

**Key Implementation Details:**
- Uses **event delegation** to handle forms replaced by AJAX
- Debounces search input to prevent excessive submissions
- Supports both direct form ID and CSS selector modes
- Handles checkboxes, radio buttons, select dropdowns, and text inputs
- Cleans up empty filter values before submission

---

### 4. command-pagination.js

**Purpose:** Handles pagination link clicks and state management.

**Responsibilities:**
- Detect current page from pagination UI
- Handle pagination link clicks via event delegation
- Coordinate with tab loader to reload with new page
- Handle scroll behavior after pagination
- Provide scroll-to-top on page change

**Public API:**
```javascript
window.CommandPagination = {
    init(options),                  // Initialize module
    getCurrentPage(),               // Get current page number
    setCurrentPage(pageNumber),     // Set page without triggering load
    goToPage(pageNumber),           // Navigate to page
    goToFirstPage(),                // Navigate to page 1
    goToLastPage(),                 // Navigate to last page
    goToNextPage(),                 // Navigate to next page
    goToPreviousPage(),             // Navigate to previous page
    updatePaginationState(),        // Refresh pagination state
    setScrollBehavior(behavior),    // Set scroll behavior
    getScrollBehavior(),            // Get scroll behavior
    isLoading(),                    // Check loading state
    setLoading(bool),               // Set loading state
    buildPageUrl(baseUrl, pageNum, params), // Build paginated URL
    extractPageNumber(url),         // Extract page from URL
    destroy()                       // Cleanup
}
```

**Callbacks:**
```javascript
init({
    scrollBehavior: 'top',  // 'top' | 'maintain' | 'none'
    onPageChange(pageNumber),
    onLoadStart(),
    onLoadComplete(pageNumber),
    onError(error)
})
```

**Key Implementation Details:**
- Uses **event delegation** on `document` for resilience after AJAX updates
- Looks for pagination links with `data-page` attribute
- Uses `requestAnimationFrame` for smooth scroll behavior
- Scroll-to-top behavior finds `.command-results` or `[data-results-container]`

---

### 5. command-loading-states.js

**Purpose:** Simplified loading state API wrapping LoadingManager.

**Responsibilities:**
- Show/hide loading spinners in containers
- Show skeleton loaders for data placeholders
- Disable/enable form elements during operations
- Provide command-page-specific defaults

**Public API:**
```javascript
window.CommandLoadingStates = {
    showLoadingSpinner(container, message),    // Show spinner
    hideLoadingSpinner(container),             // Hide spinner
    showSkeletonLoader(container),             // Show skeleton
    hideSkeletonLoader(container),             // Hide skeleton
    disableForm(form),                         // Disable all form inputs
    enableForm(form)                           // Re-enable form
}
```

**Key Implementation Details:**
- Spinner shows after 150ms delay (prevents flashing on fast operations)
- Supports both element ID (string) and HTMLElement references
- Form disabling stores original states in WeakMap for restoration
- Requires LoadingManager and ToastManager to be available

---

### 6. command-error-handler.js

**Purpose:** Comprehensive error handling for AJAX operations.

**Responsibilities:**
- Classify errors (network, timeout, server, client)
- Generate user-friendly error messages
- Determine if errors are retryable
- Show error toasts with optional retry button
- Create timeout checkers for fetch operations

**Public API:**
```javascript
window.CommandErrorHandler = {
    handleAjaxError(error, context),    // Main error handler
    showErrorToast(message, options),   // Show error notification
    showRetryOption(message, callback, operationId), // Show retry option
    isNetworkError(error),              // Detect network errors
    getErrorMessage(error),             // Get user-friendly message
    createTimeoutChecker(timeoutMs),    // Create timeout checker
    resetRetryCount(operationId)        // Reset retry counter
}
```

**Error Classification:**
```javascript
Error Type          | Retryable | Message
Network/TypeError   | Yes       | "Network error. Please check your connection..."
Timeout             | Yes       | "Request timed out. Please try again."
AbortError          | No        | (silent - user-initiated)
400                 | No        | "Invalid request. Please check your input..."
401                 | No        | "Your session has expired..."
403                 | No        | "You are not authorized..."
404                 | No        | "The requested data was not found."
408, 429            | Yes       | "Request timed out..." / "Too many requests..."
500, 502, 503, 504  | Yes       | "Server error. Please try again later."
```

**Retry Logic:**
- Max 3 retries by default
- Tracks retry attempts per operation ID
- Offers retry only if error is retryable and retries remain
- After max retries exceeded, shows error without retry option

---

### 7. command-log-modal.js

**Purpose:** Modal for displaying full command log details.

**Responsibilities:**
- Open/close modal with AJAX content loading
- Manage focus trap within modal (accessibility)
- Handle keyboard shortcuts (ESC to close)
- Deep link support via URL hash
- Timezone conversion for displayed timestamps

**Public API:**
```javascript
window.CommandLogModal = {
    open(logId),     // Open modal and load content
    close()          // Close modal
}
// Instance also auto-initialized as: window.commandLogModal
```

**Key Implementation Details:**
- Focus trap ensures keyboard navigation stays within modal
- Auto-focuses first focusable element on open
- Returns focus to trigger element on close
- Updates URL hash for deep linking: `#execution-logs/details/{logId}`
- Manually executes scripts from AJAX response (CSP-compliant)
- Calls `window.TimezoneConverter.convertAll()` after loading

**Accessibility Features:**
- Focus trap with Tab/Shift+Tab cycling
- ARIA attributes for modal role
- ESC key to close
- Auto-focus on first element
- Return focus to trigger element

---

### 8. date-range-filter.js

**Purpose:** Date preset functionality for filter panels.

**Responsibilities:**
- Toggle filter panel visibility
- Apply date presets (Today, Last 7 Days, Last 30 Days)
- Update preset button styling
- Detect which preset matches current date range
- Clear filters and reload
- Persist filter panel expanded state in localStorage

**Public API:**
```javascript
window.DateRangeFilter = {
    togglePanel(filterId),              // Toggle filter panel
    setPreset(filterId, preset),        // Apply date preset
    preserveHashAndSubmit(form),        // Submit form via AJAX
    clearFiltersAndReload(formId),      // Clear and reload
    applyDefaultFilterIfNeeded(filterId), // Set default 7-day range
    initFilterPanels()                  // Re-initialize panels
}
```

**Date Presets:**
- `'today'` - Start: today, End: today
- `'7days'` - Start: 7 days ago, End: today
- `'30days'` - Start: 30 days ago, End: today

**Key Implementation Details:**
- Calculates dates in local timezone (important for consistency)
- Filters mapped to form IDs: `analyticsFilter` ↔ `analyticsFilterForm`
- Persists panel expansion state across tab switches
- Updates button styling dynamically (removes/adds classes)
- Default 7-day filter applied if no date range set and form is empty
- Integrates with `CommandTabLoader.reloadActiveTab()`

---

### 9. url-state.js

**Purpose:** URL query parameter management for filter persistence and deep linking.

**Responsibilities:**
- Map between form field names and URL parameter names
- Restore filters from URL on page load
- Update URL when filters change
- Handle browser back/forward buttons
- Enable shareable/bookmarkable links

**Public API:**
```javascript
window.UrlState = {
    init(options),               // Initialize module
    getStateFromUrl(),           // Read state from URL
    getStateForUrl(filters, page), // Convert state for URL
    updateUrl(urlState, options), // Update URL parameters
    restoreStateFromUrl(),       // Restore from URL
    clearUrl(),                  // Remove query params
    isRestoringFromUrl(),        // Check if restoring
    getParamMapping(),           // Get parameter mappings
    getReverseMapping(),         // Get reverse mappings
    destroy()                    // Cleanup
}
```

**Parameter Mapping:**
```javascript
Form Field Name  → URL Parameter
StartDate        → dateFrom
EndDate          → dateTo
GuildId          → guildId
CommandName      → commandName
SearchTerm       → searchTerm
StatusFilter     → status
pageNumber       → pageNumber (not 'page')
```

**Key Implementation Details:**
- Maintains both forward and reverse parameter mappings
- Uses `replaceState` by default (doesn't fill browser history)
- Skips page=1 in URL (default page, no need to show)
- Prevents infinite loops with `isRestoringFromUrl` flag
- Listens for `popstate` events to handle browser back/forward
- Always includes URL hash (preserves tab selection)

---

## Filter System

### How Filters Work Across Tabs

Filters are managed separately for each tab:

| Tab | Filters | Panel |
|-----|---------|-------|
| Execution Logs | 6 filters | `executionLogsFilterForm` |
| Analytics | 3 filters | `analyticsFilterForm` |

**Shared Filters:** StartDate, EndDate, GuildId
**Execution Logs Only:** SearchTerm, CommandName, StatusFilter

When switching tabs:
1. Filter forms remain in the page (not replaced by AJAX)
2. Tab loader reads from appropriate form when building API URL
3. Filter values persist in the DOM (not cleared on tab switch)
4. Users can switch tabs and see their filters still active

### Quick Date Presets

Three preset buttons appear above date inputs:
- **Today** - Sets date range to current day only
- **Last 7 Days** - Sets range to past 7 days (default)
- **Last 30 Days** - Sets range to past 30 days

**Implementation:**
```javascript
// When preset clicked:
1. Calculate date range
2. Populate StartDate and EndDate inputs
3. Update button styling (highlight active preset)
4. Submit form with CommandTabLoader.reloadActiveTab()
```

**Preset Detection:**
- `detectActivePreset(startDateStr, endDateStr)` compares current form values to preset dates
- Returns preset name if match found, null otherwise
- Used to highlight correct button after filter submission

### Default Filter Behavior

When loading Execution Logs tab for first time:
1. Check if URL has any date parameters
2. If not, apply default 7-day range
3. Set StartDate to 7 days ago, EndDate to today
4. Trigger tab load with defaults

This ensures consistent experience but doesn't override URL-based restoration.

### Filter Persistence in URL

Filters are persisted in URL query parameters:
```
/Commands?dateFrom=2026-01-12&dateTo=2026-01-19&guildId=123&status=true&pageNumber=2#analytics
```

When visiting URL:
1. `url-state.js` reads parameters
2. Sets form field values
3. Triggers tab reload with filters
4. Results display with filters applied

### Tab-Specific vs Shared Filters

```
Shared Filters (both tabs):
  - StartDate, EndDate, GuildId

Execution Logs Only:
  - SearchTerm (debounced 300ms)
  - CommandName (autocomplete)
  - StatusFilter (success/failure)

Analytics Only:
  - None (only uses shared filters)
```

---

## URL State Management

### Query Parameter Mapping

The system maintains two mappings:

**Forward (Form → URL):**
```javascript
StartDate → dateFrom
EndDate → dateTo
GuildId → guildId
SearchTerm → searchTerm
CommandName → commandName
StatusFilter → status
```

**Reverse (URL → Form):**
```javascript
dateFrom → StartDate
dateTo → EndDate
guildId → GuildId
searchTerm → SearchTerm
commandName → CommandName
status → StatusFilter
```

### Browser History Integration

- **Tab click** → `replaceState` (don't clutter history)
- **Filter change** → `replaceState` (update current page)
- **Pagination** → `replaceState` (page is transient)
- **Browser back** → `popstate` event triggers restore

### Deep Linking Support

Users can share links like:
```
https://example.com/Commands?dateFrom=2026-01-12&dateTo=2026-01-19#execution-logs
```

When visited:
1. Tab panel opens `#execution-logs` tab
2. `url-state.js` reads query parameters
3. Form fields populated with values
4. Tab loader fetches with those filters
5. Results display immediately

### Hash-Based Tab Navigation

Tab selection uses URL hash:
```javascript
#command-list   - Command List tab
#execution-logs - Execution Logs tab
#analytics      - Analytics tab
```

Hash is preserved when filters change (only query string updated).

---

## AJAX Interaction Flows

### Tab Switching Flow

```
User clicks tab
  ↓
TabPanel (tab-panel.js) fires 'tabchange' event
  ↓
command-tab-loader.js catches event
  ↓
Check if tab already loaded
  ├─ If yes: Show cached content
  └─ If no: Fetch from API
      ↓
      Build URL from filter form values
      ↓
      Fetch /api/commands/{logs|analytics}?params
      ↓
      Inject HTML into panel
      ↓
      Execute embedded scripts
      ↓
      Call onLoadComplete callback
      ↓
      CommandTabs.initializeTabSpecificModules() runs:
        - Convert UTC timestamps to local time
        - Re-initialize autocomplete
        - Re-initialize filter panels
        - Initialize charts (if analytics tab)
```

### Filter Submission Flow

```
User submits filter form (Apply Filters button)
  ↓
Form submit handler fires
  ↓
CommandFilters module catches event
  ↓
Build filter object from form data
  ↓
Call DateRangeFilter.preserveHashAndSubmit()
  ↓
Update preset button styling if date preset detected
  ↓
Call CommandTabLoader.reloadActiveTab(filters)
  ↓
Tab loader reads form again + merges filters
  ↓
Fetch /api/commands/{logs|analytics}?merged_params
  ↓
Inject new content
  ↓
Re-initialize modules (pagination, timestamps, etc.)
  ↓
Call onLoadComplete callback
```

### Pagination Flow

```
User clicks pagination link
  ↓
CommandPagination catches click (event delegation)
  ↓
Extract page number from data-page attribute
  ↓
Call onPageChange callback
  ↓
CommandTabLoader.reloadActiveTab({ pageNumber: X })
  ↓
Tab loader merges page param with current filters
  ↓
Fetch /api/commands/{logs|analytics}?...&pageNumber=X
  ↓
Content loads, pagination updates
  ↓
Scroll behavior (scroll to top or maintain position)
```

### Error Handling and Retry

```
AJAX request fails
  ↓
CommandTabLoader catches error
  ↓
Call CommandErrorHandler.handleAjaxError()
  ↓
Error handler classifies error:
  ├─ Network error → Retryable
  ├─ Timeout → Retryable
  ├─ 500/503 → Retryable
  ├─ 400/401/403/404 → Not retryable
  └─ AbortError → Silent (no toast)
      ↓
      If retryable and retries < 3:
        Show error toast with "Retry" button
      Else:
        Show error toast without retry
      ↓
      User clicks retry (or callback called)
      ↓
      Increment retry counter
      ↓
      Execute original callback (reload tab)
```

### Loading State Transitions

```
Tab load starts
  ↓
Show loading spinner (after 150ms delay)
  ↓
Show skeleton loader in content area
  ↓
Disable filter form (optional)
  ↓
Fetch starts
  ↓
Fetch completes (success or error)
  ↓
Hide loading spinner
  ↓
Hide skeleton loader
  ↓
Update content
  ↓
Re-enable form
  ↓
Initialize modules
```

---

## Modal System

### Command Log Details Modal

The modal component displays full details for a command execution log entry.

**Trigger:**
```html
<button onclick="window.commandLogModal.open('logId')">View Details</button>
```

**Load Flow:**
```
Modal open() called with logId
  ↓
Store trigger element for focus restoration
  ↓
Show modal with loading spinner
  ↓
Fetch /api/commands/log-details/{logId}
  ↓
Inject HTML response into modal content
  ↓
Manually execute <script> tags (CSP requirement)
  ↓
Setup focus trap (Tab cycling within modal)
  ↓
Update URL hash: #execution-logs/details/{logId}
  ↓
Convert UTC timestamps to local time
  ↓
Modal visible to user
```

**Focus Trap Implementation:**
- Find all focusable elements on modal load
- Store first and last focusable elements
- Add Tab/Shift+Tab listeners
- If user presses Tab at last element, cycle to first
- If user presses Shift+Tab at first element, cycle to last
- ESC key closes modal

**Deep Linking:**
- Hash becomes `#execution-logs/details/{logId}`
- Users can bookmark or share URL
- On page load, hash triggers modal open if present

---

## Error Handling

### Error Classification

**Network Errors:**
- Cause: No internet, server unreachable, DNS failure
- Detection: `TypeError` or message contains "Failed to fetch"
- Retryable: Yes
- Message: "Network error. Please check your connection..."

**Timeout Errors:**
- Cause: Server not responding within 15 seconds
- Detection: `AbortError` or `error.isTimeout === true`
- Retryable: Yes
- Message: "Request timed out. Please try again."

**Server Errors (5xx):**
- Cause: Internal server error, bad gateway, service unavailable
- Retryable: Yes (500, 502, 503, 504)
- Message: "An error occurred on the server. Please try again later."

**Client Errors (4xx):**
- Most are not retryable (400, 401, 403, 404)
- Message varies by status code
- 408 (timeout) and 429 (rate limit) are retryable

**Abort Errors:**
- Cause: User clicked another tab or window navigation
- Retryable: No
- Message: (silent - not shown to user)

### Retry Logic (Max 3 Attempts)

```
Retry Attempt 1:
  ├─ Error occurs
  ├─ Show toast with Retry button
  └─ currentAttempts = 1

User clicks Retry:
  ├─ currentAttempts = 2
  └─ Fetch again

Retry Attempt 2:
  ├─ Another error
  ├─ Show toast with Retry button again
  └─ currentAttempts = 2

User clicks Retry:
  ├─ currentAttempts = 3
  └─ Fetch again

Retry Attempt 3:
  ├─ Another error
  ├─ Show toast WITHOUT retry button
  ├─ currentAttempts = 3
  └─ Retry counter cleared
```

### Timeout Handling (15 seconds)

```javascript
const { signal, isTimedOut, clear } = CommandErrorHandler.createTimeoutChecker(15000);

fetch(url, { signal })
  .then(response => {
    clear();  // Cancel timeout
    // process response
  })
  .catch(error => {
    if (isTimedOut()) {
      // Handle timeout
    }
  });
```

---

## Performance Optimizations

### Lazy Tab Loading

Command List tab loads with page (server-rendered). Other tabs load only when clicked.

**Benefit:** Reduces initial page load time by ~60%

### Search Input Debouncing (300ms)

```javascript
User types in search input
  ↓
Wait 300ms without typing
  ↓
If no new input received:
  └─ Submit form
Else:
  └─ Reset timer
```

**Benefit:** Reduces API calls from N characters typed to 1 call

### AbortController for Request Cancellation

```javascript
User on analytics tab (request in flight)
  ↓
User clicks execution-logs tab
  ↓
Previous request abort() called
  ↓
Fetch cancelled, no wasted data transfer
```

**Benefit:** Prevents stale responses and network waste

### Spinner Delay (150ms)

Loading spinner only shows if request takes > 150ms.

**Benefit:** Prevents spinner flashing on fast responses

### requestAnimationFrame for Smooth Scrolling

```javascript
requestAnimationFrame(() => {
  element.scrollIntoView({ behavior: 'smooth' });
});
```

**Benefit:** Smoother, more performant scroll animations

### Filter Panel State Caching

Filter panel expanded/collapsed state saved in `localStorage`:

```javascript
localStorage.setItem('commandsPage-filterPanel-expanded', 'true');
```

**Benefit:** Users' preferred view state persists across sessions

---

## Accessibility Features

### ARIA Attributes

```html
<!-- Tab panel -->
<div role="tabpanel" aria-labelledby="tab-id" aria-hidden="false"></div>

<!-- Tab -->
<button role="tab" aria-selected="true" aria-controls="panel-id"></button>

<!-- Filter panel toggle -->
<button aria-expanded="true" aria-controls="content-id"></button>

<!-- Modal -->
<div role="dialog" aria-modal="true" aria-labelledby="title-id"></div>
```

### Keyboard Navigation

| Key | Behavior |
|-----|----------|
| Tab | Navigate between focusable elements |
| Shift+Tab | Navigate backwards |
| Enter | Activate button/submit form |
| Space | Toggle button/checkbox |
| Escape | Close modal |
| Arrow keys | Tab panel navigation (if implemented) |

### Focus Management

- Focus trap in modal prevents focus leaving dialog
- Auto-focus first element in modal on open
- Return focus to trigger element on modal close
- Focus indicators visible on all interactive elements

### Screen Reader Support

- All buttons have accessible labels
- Form inputs have associated labels
- Tables have proper header structure
- Error messages announced via toasts
- Loading states indicated via spinner and aria-busy

### Semantic HTML

- Proper heading hierarchy (h1, h2, h3)
- Form elements with `<label>` associations
- Tables with `<thead>`, `<tbody>` structure
- Buttons for interactive elements (not links)
- Links for navigation

---

## Design Patterns

### IIFE (Immediately Invoked Function Expression)

All modules use IIFE to create private scope:

```javascript
(function() {
    'use strict';

    // Private state and functions
    const state = { /* ... */ };
    function privateFunction() { /* ... */ }

    // Public API
    window.ModuleName = {
        publicMethod: publicMethod
    };
})();
```

**Benefit:** Prevents global namespace pollution

### Event Delegation

Modules listen on document for events to work with AJAX-replaced content:

```javascript
document.addEventListener('click', function(e) {
    const button = e.target.closest('[data-action]');
    if (!button) return;

    // Handle click
});
```

**Benefit:** Works with dynamically added elements

### Callback Hooks for Coordination

Modules don't call each other directly; they use callbacks:

```javascript
CommandTabLoader.init({
    onLoadComplete: function(tabId) {
        // Tab loaded, other modules can react
    }
});
```

**Benefit:** Loose coupling between modules

### Memory Cleanup (Destroy Methods)

All modules provide `destroy()` to clean up:

```javascript
function destroy() {
    // Remove event listeners
    document.removeEventListener('click', handler);

    // Clear state
    state.callbacks = {};
    state.data = null;
}
```

**Benefit:** Prevents memory leaks during page transitions

---

## Common Pitfalls and Solutions

### Issue: Discord Snowflake ID Precision in JavaScript

**Problem:** Discord IDs (`ulong` = 64-bit) exceed JavaScript's `Number.MAX_SAFE_INTEGER`. Storing as numbers causes precision loss.

```javascript
// WRONG - loses precision on large IDs
const id = 123456789012345678; // Stored as: 123456789012345680
```

**Solution:** Treat IDs as strings in JavaScript:

```razor
<!-- In Razor view -->
<span data-user-id='@log.UserId'>@log.Username</span>

<!-- In JavaScript -->
const userId = element.dataset.userId; // String, not number
```

---

### Issue: Chart.js Property Naming (PascalCase vs camelCase)

**Problem:** C# returns `SuccessCount`, but JavaScript code expected `successCount`.

```javascript
// WRONG - C# sends PascalCase
{
    Date: "2026-01-19",
    SuccessCount: 10,  // PascalCase!
    FailureCount: 2
}

// JavaScript expects camelCase
data.successCount  // undefined!
```

**Solution:** Use C# property names in JavaScript:

```javascript
const successCount = data.SuccessCount;  // Correct
```

---

### Issue: Filter Form Submission Pattern

**Problem:** Form submission used `window.location.href` causing full page reload.

```javascript
// WRONG - defeats AJAX
form.onsubmit = function(e) {
    e.preventDefault();
    const url = '/Commands?filters=...';
    window.location.href = url;  // Full page reload!
};
```

**Solution:** Use AJAX tab loader:

```javascript
// CORRECT - AJAX reload
form.onsubmit = function(e) {
    e.preventDefault();
    const filters = getFilterData(form);
    window.CommandTabLoader.reloadActiveTab(filters);
};
```

---

### Issue: Button State After AJAX

**Problem:** Server-rendered button states (active/inactive styling) don't update after AJAX.

```razor
<!-- Server renders based on @isPresetActive -->
<button class="@(isPresetActive ? "bg-blue" : "bg-gray")">
    Last 7 Days
</button>

<!-- After AJAX, button still has old classes! -->
```

**Solution:** Update button styling via JavaScript:

```javascript
function updatePresetButtonStyles(filterId, activePreset) {
    // Find preset buttons
    const buttons = document.querySelectorAll('[onclick*="setPreset"]');

    // Update each button's classes based on active preset
    buttons.forEach(btn => {
        if (buttonPreset === activePreset) {
            btn.classList.add('bg-accent-blue', 'text-white');
            btn.classList.remove('bg-bg-tertiary', 'text-text-secondary');
        } else {
            btn.classList.remove('bg-accent-blue', 'text-white');
            btn.classList.add('bg-bg-tertiary', 'text-text-secondary');
        }
    });
}
```

---

### Issue: Timezone Handling for Date Filters

**Problem:** Server and client use different timezones, causing filter mismatches.

```javascript
// WRONG - client uses local time, server compares to UTC
const today = new Date();  // Local timezone
const startDate = today.toISOString();  // Converts to UTC

// If client is UTC-5, "today" in UTC is "yesterday"
```

**Solution:** Use midnight in local timezone:

```javascript
const today = new Date();
today.setHours(0, 0, 0, 0);  // Midnight local time

// Format as YYYY-MM-DD (no time component)
const formatted = formatDateForInput(today);
```

When sending to server, server interprets `YYYY-MM-DD` as that day in server's timezone.

---

## Adding New Features

### How to Add a New Tab

1. **Add tab definition in Index.cshtml:**
```razor
var tabs = new List<TabItemViewModel>
{
    new TabItemViewModel { Id = "command-list", Label = "Command List" },
    new TabItemViewModel { Id = "execution-logs", Label = "Execution Logs" },
    new TabItemViewModel { Id = "analytics", Label = "Analytics" },
    new TabItemViewModel { Id = "my-new-tab", Label = "My New Tab" }  // Add here
};
```

2. **Add empty tab panel in HTML:**
```html
<div data-tab-panel-for="commandTabs" data-tab-id="my-new-tab" role="tabpanel" hidden>
    <div data-tab-content></div>
</div>
```

3. **Add API endpoint:**
```csharp
[HttpGet("my-new-tab")]
public async Task<IActionResult> GetMyNewTab(CancellationToken cancellationToken)
{
    // Load data and return partial view
    return PartialView("Tabs/_MyNewTab", viewModel);
}
```

4. **Update command-tabs.js mappings:**
```javascript
const CommandTabs = {
    subtitleMap: {
        // ...
        'my-new-tab': 'My New Tab Description'
    },
    breadcrumbMap: {
        // ...
        'my-new-tab': 'My New Tab'
    }
};
```

5. **Create partial view:**
```
Pages/Commands/Tabs/_MyNewTab.cshtml
```

---

### How to Add a New Filter to Existing Tab

1. **Add input to filter form in Index.cshtml:**
```html
<form id="executionLogsFilterForm">
    <!-- Existing filters -->

    <!-- New filter -->
    <div>
        <label for="MyFilter" class="block text-sm font-medium">My Filter</label>
        <input type="text" id="MyFilter" name="MyFilter" />
    </div>
</form>
```

2. **Handle in command-filters.js** (already delegates, no changes needed)

3. **Add to URL state mapping in url-state.js:**
```javascript
state.config.paramMapping = {
    // ...
    'MyFilter': 'myFilter'  // Add here
};
```

4. **Accept in API endpoint:**
```csharp
[HttpGet("logs")]
public async Task<IActionResult> GetExecutionLogs(
    string myFilter,  // Add parameter
    CancellationToken cancellationToken)
{
    // Use filter
}
```

---

### How to Add a New JavaScript Module

1. **Create file: `src/DiscordBot.Bot/wwwroot/js/command-my-feature.js`**

```javascript
(function() {
    'use strict';

    const state = {
        initialized: false,
        data: null
    };

    function init(options) {
        if (state.initialized) return;
        options = options || {};
        // Initialize
        state.initialized = true;
    }

    function myPublicMethod() {
        // Implementation
    }

    function destroy() {
        state.initialized = false;
        state.data = null;
    }

    window.CommandMyFeature = {
        init: init,
        myPublicMethod: myPublicMethod,
        destroy: destroy
    };
})();
```

2. **Reference in Index.cshtml:**
```razor
@section Scripts {
    <script src="~/js/command-my-feature.js"></script>
}
```

3. **Initialize in page load handler:**
```javascript
document.addEventListener('DOMContentLoaded', function() {
    if (window.CommandMyFeature) {
        window.CommandMyFeature.init(options);
    }
});
```

---

## Testing Considerations

### Manual Testing Checklist

**Tab Switching:**
- [ ] Click each tab - content loads smoothly
- [ ] Tab name updates in breadcrumb
- [ ] Subtitle changes based on active tab
- [ ] Back/forward buttons work correctly

**Filtering:**
- [ ] Filters apply without page reload
- [ ] Quick presets set correct date range
- [ ] Clearing filters resets form
- [ ] Manual date entry works
- [ ] Search input doesn't spam API (debounced)
- [ ] Filters persist in URL

**Pagination:**
- [ ] Pagination links work
- [ ] Page scrolls to top on page change
- [ ] Page number in URL updates
- [ ] Back button returns to previous page

**Error Handling:**
- [ ] Network error shows appropriate message
- [ ] Retry button appears and works
- [ ] Max retries prevents infinite loops
- [ ] Timeout error displays correctly

**Deep Linking:**
- [ ] Shareable URL with filters works
- [ ] Closing and reopening link restores state
- [ ] Hash-based tabs work

**Accessibility:**
- [ ] Tab navigation with keyboard
- [ ] Modal focus trap working
- [ ] ESC closes modal
- [ ] All buttons reachable via Tab
- [ ] Focus indicators visible

### Browser Compatibility

**Tested Browsers:**
- Chrome 90+
- Firefox 88+
- Safari 14+
- Edge 90+

**Critical Features:**
- Fetch API (no IE11 support)
- AbortController for request cancellation
- requestAnimationFrame for animations
- WeakMap for internal state tracking
- URLSearchParams for query building

### Edge Cases

- **Empty results:** Pagination hidden, empty state shown
- **Single page results:** Pagination hidden if only 1 page
- **Rapid tab switching:** Previous request aborted when switching tabs
- **Form already submitted:** Debouncing prevents double submission
- **Large datasets:** Pagination handles 100+ pages gracefully
- **Special characters in search:** URL encoding handled by URLSearchParams

---

## File Reference

### Razor Pages

| File | Purpose | Notes |
|------|---------|-------|
| `Pages/Commands/Index.cshtml` | Main page layout | Server-renders Command List tab |
| `Pages/Commands/Index.cshtml.cs` | Page model | Loads guilds, modal config |
| `Pages/Commands/Tabs/_CommandListTab.cshtml` | Command modules partial | Server-rendered |
| `Pages/Commands/Tabs/_ExecutionLogsTab.cshtml` | Execution logs partial | AJAX-loaded |
| `Pages/Commands/Tabs/_AnalyticsTab.cshtml` | Analytics partial | AJAX-loaded |

### Controllers

| File | Purpose |
|------|---------|
| `Controllers/CommandsApiController.cs` | API endpoints for tabs |

### JavaScript

| File | Size | Purpose |
|------|------|---------|
| `wwwroot/js/command-tabs.js` | ~3KB | Tab UI updates |
| `wwwroot/js/command-tab-loader.js` | ~9KB | AJAX content loading |
| `wwwroot/js/command-filters.js` | ~13KB | Filter management |
| `wwwroot/js/command-pagination.js` | ~10KB | Pagination handling |
| `wwwroot/js/date-range-filter.js` | ~11KB | Date presets |
| `wwwroot/js/url-state.js` | ~11KB | URL state management |
| `wwwroot/js/command-log-modal.js` | ~8KB | Modal component |
| `wwwroot/js/command-loading-states.js` | ~11KB | Loading UI |
| `wwwroot/js/command-error-handler.js` | ~12KB | Error handling |

**Total JS:** ~88KB (minified)

---

## Lessons Learned from Implementation

### Critical Insights

1. **Always use existing infrastructure instead of reinventing.** The `CommandTabLoader.reloadActiveTab()` already existed with proper error handling, request cancellation, and script execution.

2. **Server-side state ≠ Client-side state after AJAX.** When using partial page updates, UI state like button styling must be updated client-side. Don't rely on server-rendered classes.

3. **Form submission must be AJAX, not navigation.** Mixing AJAX tab loading with full page navigation breaks user experience and makes state management impossible.

4. **Direct element lookup beats DOM traversal.** Use IDs and maps instead of complex selectors. `getElementById(idMap[key])` is more robust than `.closest('form').find('...').querySelector(...)`.

5. **Test assumptions with console output first.** Before changing code, verify the actual data structure and error messages. Guessing leads to wasted time.

6. **Consistency matters for UX.** If some actions use AJAX, all similar actions should use AJAX. Mixing AJAX and full-page navigation is confusing.

---

## Related Documentation

- [commands-page.md](commands-page.md) - User-facing Commands page documentation
- [api-endpoints.md](api-endpoints.md) - Complete API reference
- [design-system.md](design-system.md) - UI components and styling
- [form-implementation-standards.md](form-implementation-standards.md) - Razor Pages form patterns
- [signalr-realtime.md](signalr-realtime.md) - Real-time updates (for future integration)
- [troubleshooting-guide.md](troubleshooting-guide.md) - Common issues and solutions

---

## Troubleshooting

### Tab not loading

**Check:**
1. API endpoint exists at `/api/commands/{logs|analytics}`
2. Network tab shows successful request (200 status)
3. Console has no JavaScript errors
4. Correct form IDs for filters: `executionLogsFilterForm`, `analyticsFilterForm`

**Fix:**
```javascript
console.log('Active tab:', window.CommandTabLoader.getActiveTab());
console.log('Tab loaded:', window.CommandTabLoader.isTabLoaded('execution-logs'));
```

---

### Filters not applying

**Check:**
1. Form elements have correct names (StartDate, EndDate, GuildId, etc.)
2. CommandTabLoader module initialized
3. Filters object being built correctly

**Debug:**
```javascript
// Check filter form exists
console.log(document.getElementById('executionLogsFilterForm'));

// Test filter submission
window.CommandTabLoader.reloadActiveTab({
    StartDate: '2026-01-12',
    EndDate: '2026-01-19'
});
```

---

### Modal not opening

**Check:**
1. Modal element exists: `document.getElementById('commandLogDetailsModal')`
2. No JavaScript errors in console
3. API endpoint returns valid HTML

**Debug:**
```javascript
// Try opening manually
window.commandLogModal.open('log-id-here');

// Check if commandLogModal initialized
console.log(window.commandLogModal);
```

---

### URL not updating with filters

**Check:**
1. `UrlState` module initialized
2. Tab loader calling `onLoadComplete` callback
3. Check browser DevTools History/Network for URL changes

**Debug:**
```javascript
// Get current URL state
console.log('URL State:', window.UrlState.getStateFromUrl());

// Manually update URL
window.UrlState.updateUrl({ dateFrom: '2026-01-12' });
```

---

*Document Version: 1.0 - Complete Implementation Reference*
*Last Updated: January 2026*
*Status: Stable*