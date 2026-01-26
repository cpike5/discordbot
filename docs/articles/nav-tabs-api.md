# Navigation Tabs JavaScript API Reference

**Version:** 1.0
**Last Updated:** 2026-01-26
**Related Issue:** [#1253](https://github.com/cpike5/discordbot/issues/1253)

---

## Overview

The Navigation Tabs JavaScript module (`nav-tabs.js`) provides a unified, accessible tab navigation system for the Discord Bot Admin UI. It handles three distinct navigation modes:

1. **In-Page Mode** - Content panels switch without page reload
2. **Page Navigation Mode** - Each tab navigates to a different URL
3. **AJAX Mode** - Content loads dynamically via AJAX requests

The module implements full WAI-ARIA tab pattern compliance, keyboard navigation, URL hash persistence, localStorage persistence, and automatic scroll-to-view functionality.

### When You Need This

- **In-Page & AJAX Modes**: The JavaScript module must be loaded to enable tab switching
- **Page Navigation Mode**: JavaScript is optional (tabs work as links), but recommended for keyboard navigation
- All modes benefit from keyboard accessibility and scroll indicators

---

## Initialization

### Auto-Initialization

The module automatically initializes all navigation tab instances on the page when the DOM is ready:

```html
<!-- Simply include the script -->
<script src="~/js/nav-tabs.js" asp-append-version="true"></script>

<!-- Component HTML will be automatically initialized -->
@await Html.PartialAsync("Components/_NavTabs", Model)
```

The script automatically:
- Finds all elements with `data-nav-tabs` attribute
- Sets up keyboard navigation
- Configures persistence based on `data-persistence-mode`
- Binds event handlers based on `data-navigation-mode`
- Initializes scroll indicators for overflow detection

### Manual Initialization

For dynamically added tabs, manually reinitialize the module:

```javascript
// Reinitialize all tabs on the page
NavTabs.init();

// Or initialize a specific container
const container = document.getElementById('myTabsContainer');
NavTabs.initContainer(container);
```

---

## Public API Methods

### init()

Initialize all navigation tab instances on the page. Safe to call multiple times.

**Signature:**
```javascript
NavTabs.init() : void
```

**Parameters:** None

**Returns:** Nothing

**Example:**
```javascript
// Called automatically on DOMContentLoaded
// Call manually after dynamically adding tabs
document.addEventListener('DOMContentLoaded', () => {
    NavTabs.init();
});

// After AJAX content load
fetch('/api/tabs/more')
    .then(r => r.text())
    .then(html => {
        document.getElementById('container').innerHTML = html;
        NavTabs.init(); // Reinitialize
    });
```

---

### initContainer(container)

Initialize a specific navigation tab container.

**Signature:**
```javascript
NavTabs.initContainer(element: HTMLElement) : void
```

**Parameters:**
- `element` (HTMLElement, required) - The container element with `data-nav-tabs` attribute

**Returns:** Nothing

**Example:**
```javascript
// Initialize a specific container
const tabsContainer = document.getElementById('performanceTabs-container');
NavTabs.initContainer(tabsContainer);
```

---

### activateTab(containerId, tabId, persistenceMode)

Programmatically activate a tab and switch content.

**Signature:**
```javascript
NavTabs.activateTab(
    containerId: string,
    tabId: string,
    persistenceMode?: string
) : void
```

**Parameters:**
- `containerId` (string, required) - The container ID (from `data-container-id`)
- `tabId` (string, required) - The tab ID to activate
- `persistenceMode` (string, optional) - Override persistence mode ('hash', 'localstorage', 'none')

**Returns:** Nothing

**Example:**
```javascript
// Activate the 'health' tab in the 'performanceMetrics' container
NavTabs.activateTab('performanceMetrics', 'health');

// Activate with specific persistence mode
NavTabs.activateTab('performanceMetrics', 'health', 'hash');
```

---

### getActiveTab(containerId)

Get the currently active tab ID for a specific container.

**Signature:**
```javascript
NavTabs.getActiveTab(containerId: string) : string | null
```

**Parameters:**
- `containerId` (string, required) - The container ID (from `data-container-id`)

**Returns:** The active tab ID, or `null` if no tab is active

**Example:**
```javascript
const activeTab = NavTabs.getActiveTab('performanceMetrics');
if (activeTab === 'health') {
    console.log('Health metrics tab is active');
}
```

---

### switchTo(containerId, tabId)

Alias for `activateTab()`. Programmatically switch to a tab.

**Signature:**
```javascript
NavTabs.switchTo(containerId: string, tabId: string) : void
```

**Parameters:**
- `containerId` (string, required) - The container ID
- `tabId` (string, required) - The tab ID to activate

**Returns:** Nothing

**Example:**
```javascript
// Switch to the 'commands' tab
NavTabs.switchTo('performanceMetrics', 'commands');
```

---

### setLoading(containerId, isLoading, message)

Set loading state on a tab container and announce to screen readers.

**Signature:**
```javascript
NavTabs.setLoading(
    containerId: string,
    isLoading: boolean,
    message?: string
) : void
```

**Parameters:**
- `containerId` (string, required) - The container ID
- `isLoading` (boolean, required) - Whether content is loading
- `message` (string, optional) - Message to announce to screen readers

**Returns:** Nothing

**Example:**
```javascript
// Show loading state during AJAX request
NavTabs.setLoading('performanceMetrics', true, 'Loading health metrics...');

// Clear loading state
NavTabs.setLoading('performanceMetrics', false, 'Health metrics loaded');
```

---

### persistActiveTab(containerId, tabId, mode)

Manually persist the active tab state using the specified mode.

**Signature:**
```javascript
NavTabs.persistActiveTab(
    containerId: string,
    tabId: string,
    mode: string
) : void
```

**Parameters:**
- `containerId` (string, required) - The container ID
- `tabId` (string, required) - The tab ID to persist
- `mode` (string, required) - Persistence mode: 'hash', 'localstorage', or 'none'

**Returns:** Nothing

**Example:**
```javascript
// Persist the current tab to URL hash
NavTabs.persistActiveTab('performanceMetrics', 'health', 'hash');

// Persist to localStorage
NavTabs.persistActiveTab('performanceMetrics', 'health', 'localstorage');
```

---

### restoreActiveTab(containerId, mode)

Restore the active tab from persisted state (hash or localStorage).

**Signature:**
```javascript
NavTabs.restoreActiveTab(
    containerId: string,
    mode: string
) : string | null
```

**Parameters:**
- `containerId` (string, required) - The container ID
- `mode` (string, required) - Persistence mode: 'hash', 'localstorage', or 'none'

**Returns:** The restored tab ID, or `null` if no persisted state exists

**Example:**
```javascript
// Restore from URL hash
const restoredTab = NavTabs.restoreActiveTab('performanceMetrics', 'hash');
if (restoredTab) {
    NavTabs.activateTab('performanceMetrics', restoredTab);
}

// Restore from localStorage
const localTab = NavTabs.restoreActiveTab('performanceMetrics', 'localstorage');
```

---

### announce(message)

Announce a message to screen readers using aria-live region.

**Signature:**
```javascript
NavTabs.announce(message: string) : void
```

**Parameters:**
- `message` (string, required) - The message to announce

**Returns:** Nothing

**Example:**
```javascript
// Announce tab activation
NavTabs.announce('Health metrics tab selected');

// Announce error
NavTabs.announce('Failed to load metrics');
```

---

## Events

### tabchange

Custom event fired when a tab is activated. Useful for running custom code when tabs change.

**Event Details:**
```javascript
{
    bubbles: true,
    detail: {
        containerId: string,  // Container ID
        tabId: string        // Activated tab ID
    }
}
```

**Example:**
```javascript
// Listen for tab changes
document.addEventListener('tabchange', (e) => {
    const { containerId, tabId } = e.detail;
    console.log(`Tab ${tabId} activated in ${containerId}`);

    // Run custom logic
    if (tabId === 'health') {
        refreshHealthMetrics();
    }
});

// Listen for changes on specific container
const container = document.getElementById('performanceMetrics-container');
container.addEventListener('tabchange', (e) => {
    console.log('Tab changed:', e.detail.tabId);
});
```

---

## Configuration Options

Configuration is set via data attributes on the container element. These are automatically set by the Razor partial based on the ViewModel.

### Container Data Attributes

| Attribute | Type | Values | Description |
|-----------|------|--------|-------------|
| `data-nav-tabs` | boolean | Present/absent | Marks element as nav tabs container (required) |
| `data-container-id` | string | Any string | Unique identifier for this container |
| `data-navigation-mode` | string | 'inpage', 'pagenavigation', 'ajax' | How tabs navigate |
| `data-persistence-mode` | string | 'none', 'hash', 'localstorage' | How state is persisted |

**Example:**
```html
<div class="nav-tabs-container"
     id="performanceTabs-container"
     data-nav-tabs
     data-navigation-mode="ajax"
     data-persistence-mode="hash"
     data-container-id="performanceTabs">
    <!-- Tabs and content -->
</div>
```

### Tab Data Attributes

| Attribute | Type | Purpose |
|-----------|------|---------|
| `data-tab-id` | string | Unique ID for this tab (required) |
| `data-ajax-url` | string | AJAX mode only - endpoint to fetch content |
| `data-icon-outline` | string | SVG path for outline icon (inactive) |
| `data-icon-solid` | string | SVG path for solid icon (active) |

**Example:**
```html
<button type="button"
        role="tab"
        data-tab-id="health"
        data-ajax-url="/api/performance/health"
        data-icon-outline="M20.84 4.61a5.5..."
        data-icon-solid="M12 21.35l-1.45...">
    Health Metrics
</button>
```

### Content Panel Attributes

| Attribute | Type | Purpose |
|-----------|------|---------|
| `data-nav-panel-for` | string | Container ID this panel belongs to |
| `data-tab-id` | string | Tab ID this panel displays for |
| `hidden` | attribute | Hide inactive panels (CSS class also toggles) |

**Example:**
```html
<!-- Active panel (no hidden attribute) -->
<div data-nav-panel-for="performanceMetrics" data-tab-id="overview">
    <div class="card">Overview content</div>
</div>

<!-- Inactive panel (hidden attribute) -->
<div data-nav-panel-for="performanceMetrics" data-tab-id="health" hidden>
    <div class="card">Health content</div>
</div>
```

---

## DOM Structure

### Required HTML Structure

The component expects specific HTML structure for proper functioning:

```html
<!-- Container -->
<div class="nav-tabs-container"
     id="[containerId]-container"
     data-nav-tabs
     data-navigation-mode="[mode]"
     data-persistence-mode="[persistence]"
     data-container-id="[containerId]">

    <!-- Tab List -->
    <nav class="nav-tabs-list"
         id="[containerId]-nav"
         role="tablist"
         aria-label="Navigation">

        <!-- Individual Tab (in-page mode) -->
        <button type="button"
                class="nav-tabs-item active"
                id="[containerId]-tab-[tabId]"
                role="tab"
                aria-selected="true"
                aria-controls="[containerId]-panel-[tabId]"
                tabindex="0"
                data-tab-id="[tabId]">
            [Tab Label]
        </button>

        <!-- Individual Tab (AJAX mode) -->
        <button type="button"
                class="nav-tabs-item"
                role="tab"
                aria-selected="false"
                aria-controls="[containerId]-panel-[tabId]"
                tabindex="-1"
                data-tab-id="[tabId]"
                data-ajax-url="/api/endpoint">
            [Tab Label]
        </button>
    </nav>
</div>

<!-- Content Panels (in-page mode) -->
<div data-nav-panel-for="[containerId]" data-tab-id="[tabId]">
    <!-- Tab content -->
</div>

<div data-nav-panel-for="[containerId]" data-tab-id="[tabId]" hidden>
    <!-- Tab content -->
</div>

<!-- AJAX Content Container (AJAX mode) -->
<div id="[containerId]-content">
    <!-- AJAX content loaded here -->
</div>
```

---

## AJAX Mode

### How AJAX Loading Works

In AJAX mode, clicking a tab triggers an HTTP request to load content dynamically:

1. User clicks a tab with `data-ajax-url` attribute
2. Module shows loading indicator
3. Content from URL is fetched
4. Response HTML is inserted into `{containerId}-content` container
5. URL hash is updated (if Hash persistence enabled)
6. `tabchange` event is dispatched

### AJAX Endpoint Requirements

**Expected Response:**
- Plain HTML markup (no wrapper tags needed)
- Content that can be directly inserted into the DOM
- Should be lightweight and performant

**Example Endpoint:**
```csharp
[HttpGet("/api/performance/{section}")]
public IActionResult GetPerformanceSection(string section)
{
    // Generate HTML content
    var html = $@"
        <div class=""card"">
            <h2>{section} Metrics</h2>
            <div class=""metrics"">
                <!-- Metrics content -->
            </div>
        </div>";

    return Content(html, "text/html");
}
```

### AJAX Error Handling

The module includes error handling for failed requests:

```javascript
// Listen for errors via console
// Failed AJAX requests log warnings and remove loading state

// Custom error handling
document.addEventListener('tabchange', (e) => {
    // Check for errors (implement your own error state)
    if (/* request failed */) {
        NavTabs.announce('Failed to load content. Please try again.');
    }
});
```

### AJAX with Loading Indicators

```javascript
// Show loading state before AJAX request
NavTabs.setLoading('performanceMetrics', true, 'Loading metrics...');

// AJAX request completes automatically
NavTabs.setLoading('performanceMetrics', false);
```

---

## URL Hash Persistence

### How Hash Persistence Works

When `data-persistence-mode="hash"` is set:

1. Active tab ID is stored in URL hash: `#tabId`
2. Clicking a tab updates the hash
3. Page reload restores the previously selected tab
4. URLs can be shared with the tab pre-selected

### Examples

```
https://example.com/performance#overview   <!-- Overview tab active -->
https://example.com/performance#health     <!-- Health tab active -->
https://example.com/performance#commands   <!-- Commands tab active -->
```

### JavaScript Handling

```javascript
// Listen for hash changes
window.addEventListener('hashchange', () => {
    const tabId = window.location.hash.slice(1);
    if (tabId) {
        NavTabs.activateTab('performanceMetrics', tabId);
    }
});

// Manual hash updates
window.location.hash = 'health';  // User can also do this
```

### Hash Restoration on Page Load

The module automatically restores the active tab from the hash on page load:

```javascript
// Happens automatically during init()
// User navigates to: /performance#health
// Tab 'health' is automatically activated
```

---

## Keyboard Navigation

The module implements full WAI-ARIA tab pattern keyboard support:

### Keyboard Controls

| Key | Behavior |
|-----|----------|
| **Tab / Shift+Tab** | Navigate to tab list and between tabs |
| **Left Arrow** | Focus previous tab (wraps to last) |
| **Right Arrow** | Focus next tab (wraps to first) |
| **Home** | Focus first tab |
| **End** | Focus last tab |
| **Enter / Space** | Activate focused tab |

### Keyboard Navigation Example

```javascript
// Keyboard navigation is automatic - no code needed
// User can:
// 1. Tab to reach the tab list
// 2. Use arrow keys to move between tabs
// 3. Press Enter/Space to activate a focused tab
// 4. Use Home/End to jump to first/last tab

// For page navigation mode, Enter/Space follows the link
// For in-page/AJAX mode, Enter/Space switches content
```

### Disabled Tabs

Disabled tabs are skipped during keyboard navigation:

```html
<!-- This tab won't be focused by arrow keys -->
<button type="button"
        disabled
        aria-disabled="true"
        data-tab-id="advanced">
    Advanced Settings
</button>
```

---

## Examples

### Basic In-Page Tabs

```html
<!-- HTML Structure -->
<script src="~/js/nav-tabs.js" asp-append-version="true"></script>

<div class="nav-tabs-container"
     id="features-container"
     data-nav-tabs
     data-navigation-mode="inpage"
     data-persistence-mode="hash"
     data-container-id="features">

    <nav class="nav-tabs-list" role="tablist">
        <button type="button"
                class="nav-tabs-item active"
                role="tab"
                aria-selected="true"
                data-tab-id="soundboard">
            Soundboard
        </button>
        <button type="button"
                class="nav-tabs-item"
                role="tab"
                aria-selected="false"
                data-tab-id="tts">
            Text-to-Speech
        </button>
    </nav>
</div>

<!-- Content Panels -->
<div data-nav-panel-for="features" data-tab-id="soundboard">
    <h2>Soundboard</h2>
    <p>Soundboard content here...</p>
</div>

<div data-nav-panel-for="features" data-tab-id="tts" hidden>
    <h2>Text-to-Speech</h2>
    <p>TTS content here...</p>
</div>

<script>
    // Auto-initialization happens automatically
    // No additional code needed
</script>
```

### AJAX Tabs with Custom Event Handler

```javascript
// HTML (generated by Razor partial)
// <div data-nav-tabs
//      data-navigation-mode="ajax"
//      data-container-id="metrics">
//
//     <button data-tab-id="health"
//             data-ajax-url="/api/metrics/health">
//         Health
//     </button>
// </div>
//
// <div id="metrics-content"></div>

// Listen for tab changes and log
document.addEventListener('tabchange', (e) => {
    console.log('Tab changed:', e.detail);
});

// Activate a tab programmatically
NavTabs.switchTo('metrics', 'health');

// Show loading state
NavTabs.setLoading('metrics', true, 'Loading health data...');
// ... AJAX request completes ...
NavTabs.setLoading('metrics', false, 'Health data loaded');
```

### Manual Reinitialization

```javascript
// After dynamically adding new tabs via AJAX
const response = await fetch('/api/tabs/more');
const html = await response.text();

// Insert new tabs
document.getElementById('tabsContainer').innerHTML += html;

// Reinitialize the module to bind events
NavTabs.init();
```

### Getting Active Tab

```javascript
// Determine which tab is currently active
const activeTab = NavTabs.getActiveTab('performanceMetrics');

if (activeTab === 'health') {
    console.log('Health metrics are displayed');

    // Refresh health data
    refreshHealthData();
}

// Switch to a different tab
NavTabs.activateTab('performanceMetrics', 'commands');
```

### Hash-Based Navigation

```javascript
// When user accesses: /performance#health

// The module automatically:
// 1. Reads the hash 'health'
// 2. Activates the 'health' tab
// 3. Shows the health panel

// Users can also navigate via hash
window.location.hash = 'commands';  // Switches to commands tab

// Listen for hash changes
window.addEventListener('hashchange', () => {
    const tabId = window.location.hash.slice(1);
    console.log('User navigated to tab:', tabId);
});
```

---

## Best Practices

### 1. Always Include Required Attributes

```html
<!-- Good -->
<div data-nav-tabs
     data-container-id="unique-id"
     data-navigation-mode="inpage"
     data-persistence-mode="hash">
    ...
</div>

<!-- Bad - missing required attributes -->
<div class="tabs">
    ...
</div>
```

### 2. Use Semantic HTML

```html
<!-- Good - proper roles -->
<nav role="tablist">
    <button role="tab" aria-selected="true">Tab 1</button>
</nav>

<!-- Bad - missing semantic structure -->
<div>
    <span>Tab 1</span>
</div>
```

### 3. Handle AJAX Loading States

```javascript
// Good - inform users of loading
NavTabs.setLoading('container', true, 'Loading data...');

// Bad - silent loading can confuse users
// (no feedback while waiting)
```

### 4. Defer to Auto-Initialization

```javascript
// Good - let the module handle initialization
<script src="~/js/nav-tabs.js"></script>

// Bad - duplicate initialization
document.addEventListener('DOMContentLoaded', () => {
    NavTabs.init();
    NavTabs.init();  // Redundant
});
```

### 5. Use Container IDs Consistently

```javascript
// Good - use the same ID everywhere
const containerId = 'performanceMetrics';
NavTabs.activateTab(containerId, 'health');

// Access container
const container = document.querySelector(
    `[data-container-id="${containerId}"]`
);

// Bad - hardcoding different IDs
NavTabs.activateTab('perf', 'health');  // Different ID
```

### 6. Provide Meaningful Loading Messages

```javascript
// Good - descriptive messages
NavTabs.setLoading('metrics', true, 'Fetching health metrics...');
NavTabs.setLoading('metrics', false, 'Health metrics loaded');

// Bad - vague messages
NavTabs.setLoading('metrics', true, 'Loading...');
```

---

## Troubleshooting

### Tabs Not Responding

**Problem:** Clicking tabs does nothing

**Solutions:**
1. Verify script is loaded:
   ```html
   <script src="~/js/nav-tabs.js" asp-append-version="true"></script>
   ```

2. Check browser console for errors (F12)

3. Verify `data-nav-tabs` attribute is present:
   ```html
   <div data-nav-tabs ...>
   ```

4. Ensure `data-container-id` is set and unique:
   ```html
   <div data-container-id="uniqueId" ...>
   ```

### Content Panels Not Switching

**Problem:** Clicking tabs doesn't show/hide content

**Solutions:**
1. Verify panel attributes match container ID:
   ```html
   <!-- Container -->
   <div data-container-id="myTabs" ...>

   <!-- Panel - ID must match -->
   <div data-nav-panel-for="myTabs" data-tab-id="tab1">
   ```

2. Check panels have correct `data-tab-id`:
   ```html
   <!-- Tab -->
   <button data-tab-id="overview">Overview</button>

   <!-- Panel - ID must match -->
   <div data-nav-panel-for="container" data-tab-id="overview">
   ```

3. Ensure mode is 'inpage' for in-page navigation:
   ```html
   <div data-navigation-mode="inpage" ...>
   ```

### AJAX Content Not Loading

**Problem:** AJAX requests fail

**Solutions:**
1. Verify AJAX URL in browser console (F12 → Network tab)

2. Check endpoint returns valid HTML

3. Verify endpoint permissions (authentication/authorization)

4. Check for CORS issues if cross-origin

5. Use browser DevTools to inspect request/response

### Hash Not Updating

**Problem:** URL hash doesn't change when tabs are clicked

**Solutions:**
1. Verify persistence mode is 'hash':
   ```html
   <div data-persistence-mode="hash" ...>
   ```

2. Check browser history API is available (all modern browsers)

3. For page navigation mode, hash updates may be handled differently

### Keyboard Navigation Not Working

**Problem:** Arrow keys don't navigate tabs

**Solutions:**
1. Ensure tab list has `role="tablist"`

2. Ensure tabs have `role="tab"`

3. Check tabs aren't disabled with `disabled` attribute

4. Focus the tab list (Tab key) before using arrow keys

5. Verify `tabindex` attribute exists

---

## Related Documentation

- [Navigation Tabs Component Guide](nav-tabs-component.md)
- [Navigation Tabs Design Specification](nav-tabs-design-spec.md)
- [Navigation Tabs Migration Guide](nav-tabs-migration.md)
- [Interactive Components](interactive-components.md)
- [Accessibility & ARIA](https://www.w3.org/WAI/ARIA/apg/patterns/tabs/)

---

## Files

- **JavaScript:** `src/DiscordBot.Bot/wwwroot/js/nav-tabs.js` (to be created)
- **Component Partial:** `src/DiscordBot.Bot/Pages/Shared/Components/_NavTabs.cshtml`
- **Styles:** `src/DiscordBot.Bot/wwwroot/css/nav-tabs.css`
- **ViewModel:** `src/DiscordBot.Bot/ViewModels/Components/NavTabsViewModel.cs`

---

## Changelog

### Version 1.0 (2026-01-26)
- Initial JavaScript API reference documentation
- Documented all public methods and events
- Documented configuration via data attributes
- Provided comprehensive examples for all usage patterns
- Documented AJAX mode, hash persistence, and keyboard navigation
- Created troubleshooting guide for common issues
- Related to Issue #1253: Feature: Navigation Component Documentation and Usage Guide
