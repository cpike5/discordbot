# AJAX Support for SortDropdown Component

**Version:** 1.0
**Created:** 2026-01-26
**Status:** Draft
**Target:** v0.15.0-dev

---

## Overview

Add optional AJAX support to the existing `_SortDropdown` component to enable dynamic content updates without full page reloads. The component will remain backwards-compatible, with traditional navigation as the default behavior.

### Design Philosophy

**Loose Coupling via Events**: The component emits custom events rather than directly manipulating the DOM. Pages wire up event handlers to implement their specific reload logic, maintaining separation of concerns.

**Progressive Enhancement**: Non-AJAX mode works without JavaScript. AJAX mode enhances the experience but isn't required.

**URL State Management**: URLs update via `pushState` to maintain browser history and shareable links.

---

## 1. ViewModel Changes

**File:** `c:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\ViewModels\Components\SortDropdownViewModel.cs`

Add the following properties to `SortDropdownViewModel`:

```csharp
/// <summary>
/// ViewModel for the reusable SortDropdown component.
/// Provides a dropdown UI for selecting sort options with keyboard navigation and accessibility support.
/// </summary>
public record SortDropdownViewModel
{
    /// <summary>
    /// Unique identifier for this dropdown instance. Used for generating
    /// element IDs and managing multiple dropdowns on the same page.
    /// </summary>
    public string Id { get; init; } = "sortDropdown";

    /// <summary>
    /// Collection of sort options to display in the dropdown.
    /// </summary>
    public List<SortOption> SortOptions { get; init; } = new();

    /// <summary>
    /// The value of the currently selected sort option.
    /// </summary>
    public string CurrentSort { get; init; } = string.Empty;

    /// <summary>
    /// The query parameter name to use when constructing sort URLs.
    /// Default is "sort".
    /// </summary>
    public string ParameterName { get; init; } = "sort";

    // ========== NEW PROPERTIES ==========

    /// <summary>
    /// Enable AJAX mode. When true, clicking sort options emits a custom event
    /// instead of navigating. Default is false (traditional page navigation).
    /// </summary>
    public bool UseAjax { get; init; } = false;

    /// <summary>
    /// The CSS selector for the container element to replace with AJAX content.
    /// Required when UseAjax is true. Example: "#soundsList"
    /// </summary>
    public string? TargetSelector { get; init; }

    /// <summary>
    /// The URL endpoint that returns partial HTML for the sorted content.
    /// Query parameter will be appended automatically (e.g., "?sort=name-asc").
    /// Required when UseAjax is true. Example: "/Portal/Soundboard/123?handler=Partial"
    /// </summary>
    public string? PartialUrl { get; init; }
}
```

### Validation Rules

- When `UseAjax = true`:
  - `TargetSelector` must be non-null and non-empty
  - `PartialUrl` must be non-null and non-empty
- When `UseAjax = false`:
  - `TargetSelector` and `PartialUrl` are ignored

---

## 2. Component Changes

**File:** `c:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\Pages\Shared\_SortDropdown.cshtml`

### 2.1 HTML Changes

Replace the `<a>` tags with elements that support both modes:

**Current (lines 35-49):**
```cshtml
<a href="?@(Model.ParameterName)=@option.Value"
   class="block px-4 py-2 text-sm text-text-primary hover:bg-bg-hover @roundedClass @selectedClass"
   role="option"
   aria-selected="@isSelected.ToString().ToLower()"
   data-sort-value="@option.Value">
    <!-- content -->
</a>
```

**Proposed:**
```cshtml
@if (Model.UseAjax)
{
    <button type="button"
            class="block w-full text-left px-4 py-2 text-sm text-text-primary hover:bg-bg-hover @roundedClass @selectedClass"
            role="option"
            aria-selected="@isSelected.ToString().ToLower()"
            data-sort-value="@option.Value"
            data-ajax-sort>
        <div class="flex items-center justify-between">
            <span>@option.Label</span>
            @if (isSelected)
            {
                <svg class="w-4 h-4 text-accent-green" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
                </svg>
            }
        </div>
    </button>
}
else
{
    <a href="?@(Model.ParameterName)=@option.Value"
       class="block px-4 py-2 text-sm text-text-primary hover:bg-bg-hover @roundedClass @selectedClass"
       role="option"
       aria-selected="@isSelected.ToString().ToLower()"
       data-sort-value="@option.Value">
        <div class="flex items-center justify-between">
            <span>@option.Label</span>
            @if (isSelected)
            {
                <svg class="w-4 h-4 text-accent-green" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
                </svg>
            }
        </div>
    </a>
}
```

### 2.2 JavaScript Changes

Modify the inline `<script>` section (lines 54-169) to add AJAX support:

**Add at the beginning of the IIFE (after line 58):**

```javascript
(function() {
    const toggleId = '@toggleId';
    const dropdownId = '@dropdownId';
    const wrapperId = '@wrapperId';

    // NEW: AJAX configuration
    const useAjax = @(Model.UseAjax ? "true" : "false");
    const targetSelector = '@(Model.TargetSelector ?? "")';
    const partialUrl = '@(Model.PartialUrl ?? "")';
    const paramName = '@Model.ParameterName';

    const toggle = document.getElementById(toggleId);
    const dropdown = document.getElementById(dropdownId);
    const wrapper = document.getElementById(wrapperId);

    if (!toggle || !dropdown || !wrapper) return;

    let currentFocusIndex = -1;
    const options = dropdown.querySelectorAll('[role="option"]');
```

**Add AJAX click handler (insert after line 106, after the "close when clicking outside" handler):**

```javascript
    // AJAX mode: Handle sort option clicks
    if (useAjax) {
        const ajaxButtons = dropdown.querySelectorAll('[data-ajax-sort]');

        ajaxButtons.forEach(button => {
            button.addEventListener('click', async function(e) {
                e.preventDefault();
                e.stopPropagation();

                const sortValue = this.dataset.sortValue;

                // Close dropdown
                toggle.setAttribute('aria-expanded', 'false');
                dropdown.classList.add('hidden');
                currentFocusIndex = -1;

                // Dispatch custom event for page to handle
                const event = new CustomEvent('sortchange', {
                    bubbles: true,
                    detail: {
                        dropdownId: wrapperId,
                        sortValue: sortValue,
                        paramName: paramName,
                        targetSelector: targetSelector,
                        partialUrl: partialUrl
                    }
                });

                wrapper.dispatchEvent(event);
            });
        });
    }
```

**Update keyboard navigation to handle buttons (modify line 139):**

```javascript
        } else if (e.key === 'Enter') {
            e.preventDefault();
            if (currentFocusIndex >= 0 && currentFocusIndex < options.length) {
                options[currentFocusIndex].click();
            }
        } else if (e.key === 'Escape') {
```

The `.click()` call works for both `<a>` and `<button>` elements, so no additional changes needed.

---

## 3. Page Integration Pattern

Pages that want to use AJAX mode must:

1. **Set ViewModel properties** in the PageModel
2. **Listen for the `sortchange` event**
3. **Fetch partial HTML** from the server
4. **Swap content** into the target container
5. **Update URL** with `pushState`

### Example Integration (Soundboard Page)

**JavaScript pattern to add to any page using AJAX sort:**

```javascript
<script>
(function() {
    'use strict';

    // Listen for sort changes
    document.addEventListener('sortchange', async function(e) {
        const { sortValue, paramName, targetSelector, partialUrl } = e.detail;

        if (!targetSelector || !partialUrl) {
            console.error('SortDropdown: Missing targetSelector or partialUrl');
            return;
        }

        const target = document.querySelector(targetSelector);
        if (!target) {
            console.error('SortDropdown: Target element not found:', targetSelector);
            return;
        }

        // Build URL with sort parameter
        const url = new URL(partialUrl, window.location.origin);
        url.searchParams.set(paramName, sortValue);

        // Show loading state
        target.setAttribute('aria-busy', 'true');
        const originalContent = target.innerHTML;

        // Optional: Add loading spinner
        const loadingIndicator = document.createElement('div');
        loadingIndicator.className = 'flex items-center justify-center p-8';
        loadingIndicator.innerHTML = `
            <svg class="animate-spin h-8 w-8 text-accent-orange" viewBox="0 0 24 24">
                <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4" fill="none"></circle>
                <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
            </svg>
        `;
        target.innerHTML = '';
        target.appendChild(loadingIndicator);

        try {
            const response = await fetch(url.toString(), {
                headers: {
                    'Accept': 'text/html',
                    'X-Requested-With': 'XMLHttpRequest'
                }
            });

            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }

            const html = await response.text();

            // Swap content
            target.innerHTML = html;

            // Update URL (preserve existing query params, update sort)
            const currentUrl = new URL(window.location);
            currentUrl.searchParams.set(paramName, sortValue);
            history.pushState(
                { sort: sortValue },
                '',
                currentUrl.toString()
            );

        } catch (error) {
            console.error('SortDropdown: Failed to load sorted content:', error);

            // Show error message
            target.innerHTML = `
                <div class="flex flex-col items-center justify-center p-8 text-center">
                    <svg class="w-12 h-12 text-accent-red mb-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                              d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
                    </svg>
                    <h3 class="text-lg font-semibold text-text-primary mb-2">Failed to Load</h3>
                    <p class="text-sm text-text-secondary mb-4">${error.message}</p>
                    <button type="button" class="btn btn-secondary" onclick="location.reload()">
                        Reload Page
                    </button>
                </div>
            `;

        } finally {
            target.removeAttribute('aria-busy');
        }
    });

    // Handle browser back/forward
    window.addEventListener('popstate', function(e) {
        if (e.state && e.state.sort) {
            // URL changed via back/forward - reload to restore state
            location.reload();
        }
    });
})();
</script>
```

**Alternative: Reusable Module** (recommended for multiple pages)

Extract the above into `wwwroot/js/ajax-sort.js`:

```javascript
/**
 * AJAX Sort Module
 * Handles dynamic content updates when sort options change.
 */
(function() {
    'use strict';

    const AjaxSort = {
        /**
         * Initialize AJAX sort handling.
         * @param {Object} options - Configuration options
         * @param {Function} options.onBeforeLoad - Called before fetch starts
         * @param {Function} options.onAfterLoad - Called after content swapped
         * @param {Function} options.onError - Called on fetch error
         */
        init: function(options = {}) {
            document.addEventListener('sortchange', async function(e) {
                await AjaxSort.handleSortChange(e, options);
            });

            window.addEventListener('popstate', function(e) {
                if (e.state && e.state.sort) {
                    location.reload();
                }
            });
        },

        /**
         * Handle sort change event.
         */
        handleSortChange: async function(e, options) {
            const { sortValue, paramName, targetSelector, partialUrl } = e.detail;

            if (!targetSelector || !partialUrl) {
                console.error('AjaxSort: Missing targetSelector or partialUrl');
                return;
            }

            const target = document.querySelector(targetSelector);
            if (!target) {
                console.error('AjaxSort: Target element not found:', targetSelector);
                return;
            }

            // Build URL
            const url = new URL(partialUrl, window.location.origin);
            url.searchParams.set(paramName, sortValue);

            // Callbacks
            if (options.onBeforeLoad) {
                options.onBeforeLoad(target, sortValue);
            }

            // Show loading
            target.setAttribute('aria-busy', 'true');
            this.showLoading(target);

            try {
                const response = await fetch(url.toString(), {
                    headers: {
                        'Accept': 'text/html',
                        'X-Requested-With': 'XMLHttpRequest'
                    }
                });

                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}: ${response.statusText}`);
                }

                const html = await response.text();

                // Swap content
                target.innerHTML = html;

                // Update URL
                const currentUrl = new URL(window.location);
                currentUrl.searchParams.set(paramName, sortValue);
                history.pushState(
                    { sort: sortValue },
                    '',
                    currentUrl.toString()
                );

                // Callback
                if (options.onAfterLoad) {
                    options.onAfterLoad(target, sortValue);
                }

            } catch (error) {
                console.error('AjaxSort: Failed to load content:', error);
                this.showError(target, error.message);

                if (options.onError) {
                    options.onError(error, target);
                }

            } finally {
                target.removeAttribute('aria-busy');
            }
        },

        /**
         * Show loading indicator.
         */
        showLoading: function(target) {
            const spinner = document.createElement('div');
            spinner.className = 'flex items-center justify-center p-8';
            spinner.innerHTML = `
                <svg class="animate-spin h-8 w-8 text-accent-orange" viewBox="0 0 24 24">
                    <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4" fill="none"></circle>
                    <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
                </svg>
            `;
            target.innerHTML = '';
            target.appendChild(spinner);
        },

        /**
         * Show error message.
         */
        showError: function(target, message) {
            target.innerHTML = `
                <div class="flex flex-col items-center justify-center p-8 text-center">
                    <svg class="w-12 h-12 text-accent-red mb-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                              d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
                    </svg>
                    <h3 class="text-lg font-semibold text-text-primary mb-2">Failed to Load</h3>
                    <p class="text-sm text-text-secondary mb-4">${this.escapeHtml(message)}</p>
                    <button type="button" class="btn btn-secondary" onclick="location.reload()">
                        Reload Page
                    </button>
                </div>
            `;
        },

        /**
         * Escape HTML to prevent XSS.
         */
        escapeHtml: function(str) {
            const div = document.createElement('div');
            div.textContent = str;
            return div.innerHTML;
        }
    };

    // Expose to global scope
    window.AjaxSort = AjaxSort;

    // Auto-initialize on DOMContentLoaded
    document.addEventListener('DOMContentLoaded', function() {
        AjaxSort.init();
    });
})();
```

Then in pages, just include:
```cshtml
<script src="~/js/ajax-sort.js"></script>
```

---

## 4. Soundboard Implementation

First consumer of AJAX sort will be the **Portal Soundboard** page.

### 4.1 PageModel Changes

**File:** `c:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\Pages\Portal\Soundboard\Index.cshtml.cs`

Add a handler method to return partial HTML:

```csharp
/// <summary>
/// Handles AJAX requests for sorted sound list partial.
/// </summary>
/// <param name="guildId">The guild ID</param>
/// <param name="sort">The sort option (name-asc, name-desc, newest, oldest)</param>
/// <returns>Partial view with sorted sounds list</returns>
public async Task<IActionResult> OnGetPartialAsync(ulong guildId, string sort = "name-asc")
{
    // Reuse existing verification logic
    var verificationResult = await VerifyMembershipAsync(guildId);
    if (verificationResult != null)
    {
        // Return error partial for AJAX requests
        return new ContentResult
        {
            Content = "<div class=\"p-8 text-center text-text-secondary\">Access denied</div>",
            ContentType = "text/html"
        };
    }

    // Load sounds with specified sort
    var sounds = await _soundService.GetSoundsByGuildIdAsync(guildId);

    // Apply sorting
    Sounds = sort switch
    {
        "name-desc" => sounds.OrderByDescending(s => s.Name)
                             .Select(MapToViewModel)
                             .ToList(),
        "newest" => sounds.OrderByDescending(s => s.CreatedAt)
                          .Select(MapToViewModel)
                          .ToList(),
        "oldest" => sounds.OrderBy(s => s.CreatedAt)
                          .Select(MapToViewModel)
                          .ToList(),
        _ => sounds.OrderBy(s => s.Name)  // Default: name-asc
                   .Select(MapToViewModel)
                   .ToList()
    };

    return Partial("_SoundsList", this);
}

private PortalSoundViewModel MapToViewModel(Core.Entities.Sound sound)
{
    return new PortalSoundViewModel
    {
        Id = sound.Id,
        Name = sound.Name,
        Duration = sound.Duration,
        FileSizeBytes = sound.FileSizeBytes,
        CreatedAt = sound.CreatedAt,
        PlayCount = sound.PlayCount
    };
}
```

### 4.2 Extract Sounds List Partial

**File:** `c:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\Pages\Portal\Soundboard\_SoundsList.cshtml` (NEW)

Extract the sounds list section from `Index.cshtml` into a reusable partial:

```cshtml
@model DiscordBot.Bot.Pages.Portal.Soundboard.IndexModel

@if (!Model.Sounds.Any())
{
    <div class="flex flex-col items-center justify-center p-12 text-center">
        <svg class="w-16 h-16 text-text-tertiary mb-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                  d="M9 19V6l12-3v13M9 19c0 1.105-1.343 2-3 2s-3-.895-3-2 1.343-2 3-2 3 .895 3 2zm12-3c0 1.105-1.343 2-3 2s-3-.895-3-2 1.343-2 3-2 3 .895 3 2zM9 10l12-3" />
        </svg>
        <h3 class="text-lg font-semibold text-text-primary mb-2">No Sounds Yet</h3>
        <p class="text-sm text-text-secondary">This soundboard is empty. Upload your first sound to get started!</p>
    </div>
}
else
{
    <div class="divide-y divide-border-primary">
        @foreach (var sound in Model.Sounds)
        {
            <div class="p-4 hover:bg-bg-hover transition-colors flex items-center justify-between">
                <div class="flex-1 min-w-0">
                    <h3 class="font-medium text-text-primary truncate">@sound.Name</h3>
                    <div class="flex items-center gap-4 mt-1 text-xs text-text-secondary">
                        <span>@sound.DurationFormatted</span>
                        <span>@sound.FileSizeFormatted</span>
                        <span>@sound.PlayCount plays</span>
                    </div>
                </div>
                <div class="flex items-center gap-2 ml-4">
                    <button type="button"
                            class="p-2 rounded-lg bg-accent-orange hover:bg-accent-orange-hover text-white transition-colors"
                            data-sound-id="@sound.Id"
                            data-sound-name="@sound.Name"
                            onclick="playSoundFromPortal('@sound.Id')">
                        <svg class="w-5 h-5" fill="currentColor" viewBox="0 0 24 24">
                            <path d="M8 5v14l11-7z"/>
                        </svg>
                    </button>
                </div>
            </div>
        }
    </div>
}
```

### 4.3 Update Main View

**File:** `c:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\Pages\Portal\Soundboard\Index.cshtml`

Find the sounds list section and replace with:

```cshtml
<div class="bg-bg-secondary border border-border-primary rounded-lg overflow-hidden">
    <!-- Header with Sort -->
    <div class="px-6 py-4 border-b border-border-primary flex items-center justify-between">
        <h2 class="text-lg font-semibold text-text-primary">Sounds</h2>

        <!-- Sort Dropdown with AJAX -->
        <partial name="Shared/_SortDropdown" model='new SortDropdownViewModel {
            Id = "soundsSort",
            SortOptions = new List<SortOption>
            {
                new SortOption { Value = "name-asc", Label = "Name (A-Z)" },
                new SortOption { Value = "name-desc", Label = "Name (Z-A)" },
                new SortOption { Value = "newest", Label = "Newest First" },
                new SortOption { Value = "oldest", Label = "Oldest First" }
            },
            CurrentSort = Model.CurrentSort ?? "name-asc",
            ParameterName = "sort",
            UseAjax = true,
            TargetSelector = "#soundsList",
            PartialUrl = $"/Portal/Soundboard/{Model.GuildId}?handler=Partial"
        }' />
    </div>

    <!-- Sounds List Container (AJAX target) -->
    <div id="soundsList">
        <partial name="_SoundsList" model="Model" />
    </div>
</div>

<!-- Include AJAX Sort Module -->
<script src="~/js/ajax-sort.js"></script>
```

### 4.4 Add CurrentSort Property

**File:** `c:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\Pages\Portal\Soundboard\Index.cshtml.cs`

Add property to PageModel:

```csharp
/// <summary>
/// Gets or sets the current sort option.
/// </summary>
[BindProperty(SupportsGet = true)]
public string? CurrentSort { get; set; }
```

Update `OnGetAsync` to apply sorting:

```csharp
public async Task<IActionResult> OnGetAsync(ulong guildId)
{
    var verificationResult = await VerifyMembershipAsync(guildId);
    if (verificationResult != null)
    {
        return verificationResult;
    }

    // Load sounds
    var sounds = await _soundService.GetSoundsByGuildIdAsync(guildId);

    // Apply sorting based on CurrentSort
    Sounds = (CurrentSort ?? "name-asc") switch
    {
        "name-desc" => sounds.OrderByDescending(s => s.Name)
                             .Select(MapToViewModel)
                             .ToList(),
        "newest" => sounds.OrderByDescending(s => s.CreatedAt)
                          .Select(MapToViewModel)
                          .ToList(),
        "oldest" => sounds.OrderBy(s => s.CreatedAt)
                          .Select(MapToViewModel)
                          .ToList(),
        _ => sounds.OrderBy(s => s.Name)
                   .Select(MapToViewModel)
                   .ToList()
    };

    // ... rest of existing logic

    return Page();
}
```

---

## 5. Partial Handler Pattern

### Standard Pattern for AJAX Handlers

All pages implementing AJAX sort should follow this pattern:

```csharp
/// <summary>
/// Handles AJAX requests for [description] partial.
/// </summary>
/// <param name="sort">The sort option</param>
/// <returns>Partial view with sorted content</returns>
public async Task<IActionResult> OnGetPartialAsync(string sort = "default-value")
{
    // 1. Validate sort parameter
    if (!IsValidSortOption(sort))
    {
        sort = "default-value";
    }

    // 2. Load data
    var data = await _service.GetDataAsync();

    // 3. Apply sorting
    var sortedData = ApplySorting(data, sort);

    // 4. Map to ViewModel
    ViewModel.Items = sortedData.Select(MapToViewModel).ToList();
    ViewModel.CurrentSort = sort;

    // 5. Return partial view
    return Partial("_ListPartial", this);
}

private bool IsValidSortOption(string sort)
{
    return sort switch
    {
        "option1" => true,
        "option2" => true,
        "option3" => true,
        _ => false
    };
}
```

### Naming Conventions

- Handler method: `OnGetPartialAsync`
- Partial view: `_[Section]List.cshtml` (e.g., `_SoundsList.cshtml`)
- Container ID: `[section]List` (e.g., `soundsList`)
- Sort parameter: `sort` (lowercase, query string convention)

### Error Handling

Return appropriate content for error states:

```csharp
if (errorCondition)
{
    return new ContentResult
    {
        Content = "<div class=\"p-8 text-center text-text-secondary\">Error message</div>",
        ContentType = "text/html"
    };
}
```

---

## 6. Testing Checklist

### Component Tests

- [ ] Non-AJAX mode still works (traditional navigation)
- [ ] AJAX mode emits `sortchange` event with correct detail
- [ ] Dropdown closes after selection in AJAX mode
- [ ] Keyboard navigation works (Enter on option)
- [ ] Selected option shows checkmark
- [ ] Accessibility: `aria-selected` updates correctly

### Integration Tests (Soundboard)

- [ ] Sort dropdown appears in header
- [ ] Clicking sort option updates list without page reload
- [ ] URL updates with `?sort=value` parameter
- [ ] Browser back button restores previous sort
- [ ] Refresh maintains sort from URL parameter
- [ ] Loading indicator appears during fetch
- [ ] Error state displays if fetch fails
- [ ] Multiple rapid clicks don't cause race conditions

### Browser Compatibility

- [ ] Chrome/Edge (latest)
- [ ] Firefox (latest)
- [ ] Safari (latest)
- [ ] Mobile Safari (iOS)
- [ ] Chrome Mobile (Android)

### Accessibility

- [ ] Screen reader announces sort option selection
- [ ] Keyboard navigation (Tab, Arrow keys, Enter, Escape)
- [ ] Focus visible on all interactive elements
- [ ] `aria-busy` announced during loading
- [ ] No motion for users with `prefers-reduced-motion`

---

## 7. Implementation Order

1. **Phase 1: Component Infrastructure**
   - Update `SortDropdownViewModel.cs` with new properties
   - Modify `_SortDropdown.cshtml` to support AJAX mode
   - Create `wwwroot/js/ajax-sort.js` module

2. **Phase 2: Soundboard Integration**
   - Add `OnGetPartialAsync` handler to Soundboard PageModel
   - Extract `_SoundsList.cshtml` partial view
   - Update Soundboard `Index.cshtml` to use AJAX mode
   - Add `CurrentSort` property binding

3. **Phase 3: Testing & Documentation**
   - Manual testing across browsers
   - Update `component-api.md` with AJAX sort documentation
   - Add usage examples to docs

4. **Phase 4: Additional Pages** (future)
   - Apply pattern to Guild Soundboard (`/Guilds/Soundboard/{id}`)
   - Apply pattern to other sortable lists (Members, Command Logs, etc.)

---

## 8. Future Enhancements

### Multi-Parameter Support

Support additional filters alongside sort:

```csharp
public bool PreserveQueryParams { get; init; } = true;
```

When true, merge existing URL params instead of replacing them.

### Animation Transitions

Add CSS transitions when swapping content:

```javascript
// Before swap
target.style.opacity = '0';
target.style.transition = 'opacity 150ms ease-in-out';

// After swap
setTimeout(() => {
    target.style.opacity = '1';
}, 10);
```

### Debouncing

Add debounce to prevent rapid-fire requests:

```javascript
let debounceTimer;
button.addEventListener('click', function() {
    clearTimeout(debounceTimer);
    debounceTimer = setTimeout(() => {
        // Handle sort
    }, 200);
});
```

### Cache Results

Store fetched HTML in memory to avoid redundant requests:

```javascript
const cache = new Map();
const cacheKey = `${partialUrl}?${paramName}=${sortValue}`;

if (cache.has(cacheKey)) {
    target.innerHTML = cache.get(cacheKey);
    return;
}

// After fetch
cache.set(cacheKey, html);
```

---

## 9. Risks & Mitigations

### Risk: Breaking Existing Pages

**Mitigation:** Default behavior unchanged (`UseAjax = false`). Existing dropdowns continue to use traditional navigation.

### Risk: JavaScript Disabled

**Mitigation:** Component degrades gracefully. If AJAX fails to initialize, links/buttons still work via standard navigation (though `<button>` elements won't navigate without JS - use feature detection to render `<a>` tags when JS unavailable).

**Improved Mitigation:** Use progressive enhancement - render `<a>` tags always, intercept clicks with JavaScript:

```cshtml
<a href="?@(Model.ParameterName)=@option.Value"
   data-sort-value="@option.Value"
   @(Model.UseAjax ? "data-ajax-sort" : "")>
```

Then in JS, prevent default only when AJAX is enabled.

### Risk: SEO Impact

**Mitigation:** Portal pages are behind authentication - no SEO concerns. Admin pages should use traditional navigation for search engine visibility (already the case).

### Risk: Race Conditions

**Mitigation:** Abort previous fetch when new sort triggered. The `ajax-sort.js` module handles this automatically.

---

## 10. Success Criteria

- [ ] Component supports both AJAX and traditional modes
- [ ] Soundboard page uses AJAX sort without full reload
- [ ] URL state maintained for back/forward navigation
- [ ] No accessibility regressions
- [ ] Documentation updated in `component-api.md`
- [ ] Pattern documented for future pages

---

## References

- **Existing Patterns:** `nav-tabs.js` AJAX implementation (lines 476-584)
- **URL State:** `url-state.js` for inspiration on `pushState` usage
- **Design System:** `component-api.md` for component conventions
- **Discord IDs:** Always use strings in JavaScript (see CLAUDE.md gotchas)

---

## Questions for Review

1. Should we add a global configuration option to default all dropdowns to AJAX mode?
2. Do we want animation/transitions when content swaps?
3. Should the module cache fetched results?
4. Is the reusable `ajax-sort.js` module preferred, or inline scripts per page?

---

**End of Specification**
