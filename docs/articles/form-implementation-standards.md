# Form Implementation Standards

This document defines the standard patterns for implementing forms in Razor Pages to ensure consistency, reliability, and maintainability across the codebase.

## Quick Reference

| Use Case | Pattern | Example Pages |
|----------|---------|---------------|
| Create/Edit entities | Traditional POST + [BindProperty] | `Users/Create`, `Users/Edit`, `Guilds/Edit` |
| Settings with real-time feedback | AJAX POST + JSON response | `Admin/Settings` |
| Filtering/Search | GET form with query params | `CommandLogs/Index` |
| Modal confirmation | Hidden form inside modal | `ScheduledMessages/Edit`, `RatWatch/Index` |

## Pattern 1: Traditional POST Form (Recommended Default)

This is the **standard pattern** for most create/edit operations. Use this unless you have a specific reason to use AJAX.

### PageModel (Code-Behind)

```csharp
[Authorize(Policy = "RequireAdmin")]
public class EditModel : PageModel
{
    // 1. Inject services
    private readonly IMyService _myService;
    private readonly ILogger<EditModel> _logger;

    public EditModel(IMyService myService, ILogger<EditModel> logger)
    {
        _myService = myService;
        _logger = logger;
    }

    // 2. BindProperty for form input - this is the key binding mechanism
    [BindProperty]
    public InputModel Input { get; set; } = new();

    // 3. Separate view model for display-only data
    public MyViewModel ViewModel { get; set; } = new();

    // 4. TempData for success messages that survive redirect
    [TempData]
    public string? SuccessMessage { get; set; }

    // 5. Regular property for error messages (page redisplay)
    public string? ErrorMessage { get; set; }

    // 6. InputModel with validation attributes
    public class InputModel
    {
        public ulong EntityId { get; set; }

        [Required(ErrorMessage = "Name is required")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string? Email { get; set; }
    }

    // 7. OnGet loads data
    public async Task<IActionResult> OnGetAsync(ulong id, CancellationToken cancellationToken)
    {
        var entity = await _myService.GetByIdAsync(id, cancellationToken);
        if (entity == null)
            return NotFound();

        // Populate ViewModel for display-only data
        ViewModel = MyViewModel.FromDto(entity);

        // Populate Input for form binding
        Input = new InputModel
        {
            EntityId = entity.Id,
            Name = entity.Name,
            IsActive = entity.IsActive,
            Email = entity.Email
        };

        return Page();
    }

    // 8. OnPost handles form submission
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        // 8a. Check ModelState first
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Invalid ModelState: {Errors}",
                string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
            await LoadViewModelAsync(Input.EntityId, cancellationToken);
            return Page();
        }

        // 8b. Custom validation if needed
        if (Input.Name.Contains("bad"))
        {
            ModelState.AddModelError("Input.Name", "Name cannot contain 'bad'");
            await LoadViewModelAsync(Input.EntityId, cancellationToken);
            return Page();
        }

        // 8c. Perform the update
        var updateDto = new UpdateDto
        {
            Name = Input.Name,
            IsActive = Input.IsActive,
            Email = Input.Email
        };

        var result = await _myService.UpdateAsync(Input.EntityId, updateDto, cancellationToken);

        if (result == null)
        {
            ErrorMessage = "Entity not found. It may have been deleted.";
            await LoadViewModelAsync(Input.EntityId, cancellationToken);
            return Page();
        }

        // 8d. Success - redirect with TempData message
        SuccessMessage = "Settings saved successfully.";
        return RedirectToPage("Details", new { id = Input.EntityId });
    }

    // 9. Helper to reload ViewModel after validation failure
    private async Task LoadViewModelAsync(ulong id, CancellationToken cancellationToken)
    {
        var entity = await _myService.GetByIdAsync(id, cancellationToken);
        if (entity != null)
        {
            ViewModel = MyViewModel.FromDto(entity);
        }
    }
}
```

### Razor View

```html
@page "{id:long}"
@model EditModel
@using DiscordBot.Bot.ViewModels.Components
@{
    ViewData["Title"] = $"Edit {Model.ViewModel.Name}";
}

<div class="max-w-3xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
    <!-- Success Message (from TempData after redirect) -->
    @if (!string.IsNullOrEmpty(Model.SuccessMessage))
    {
        <div class="mb-6">
            <partial name="Shared/Components/_Alert" model="new AlertViewModel {
                Variant = AlertVariant.Success,
                Message = Model.SuccessMessage,
                IsDismissible = true
            }" />
        </div>
    }

    <!-- Error Message (from validation or service failure) -->
    @if (!string.IsNullOrEmpty(Model.ErrorMessage))
    {
        <div class="mb-6">
            <partial name="Shared/Components/_Alert" model="new AlertViewModel {
                Variant = AlertVariant.Error,
                Message = Model.ErrorMessage,
                IsDismissible = true
            }" />
        </div>
    }

    <!-- The Form -->
    <form method="post">
        <!-- Hidden field for entity ID -->
        <input type="hidden" name="Input.EntityId" value="@Model.Input.EntityId" />

        <!-- Model-level validation summary -->
        <div asp-validation-summary="ModelOnly" class="text-error text-sm mb-4"></div>

        <div class="bg-bg-secondary border border-border-primary rounded-lg p-6 space-y-6">
            <!-- Text Input using component -->
            <div>
                @{
                    var nameValidation = ViewData.ModelState["Input.Name"]?.Errors.Count > 0
                        ? ValidationState.Error : ValidationState.None;
                    var nameError = ViewData.ModelState["Input.Name"]?.Errors.FirstOrDefault()?.ErrorMessage;
                }
                <partial name="Shared/Components/_FormInput" model="new FormInputViewModel {
                    Id = \"Input_Name\",
                    Name = \"Input.Name\",
                    Label = \"Name\",
                    Type = \"text\",
                    Value = Model.Input.Name,
                    IsRequired = true,
                    ValidationState = nameValidation,
                    ValidationMessage = nameError
                }" />
            </div>

            <!-- Checkbox/Toggle -->
            <div class="flex items-center justify-between py-2">
                <div class="flex-1 pr-4">
                    <label for="Input_IsActive" class="text-sm font-medium text-text-primary">Active</label>
                    <p class="text-xs text-text-tertiary mt-0.5">Enable this feature</p>
                </div>
                <label class="relative inline-flex items-center cursor-pointer">
                    <input asp-for="Input.IsActive" class="sr-only peer" />
                    <div class="w-11 h-6 bg-bg-tertiary peer-focus:outline-none peer-focus:ring-2 peer-focus:ring-border-focus/50 rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-text-primary after:border-border-primary after:border after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-accent-orange"></div>
                </label>
            </div>
        </div>

        <!-- Form Actions -->
        <div class="flex items-center justify-end gap-4 mt-6">
            <a asp-page="Details" asp-route-id="@Model.ViewModel.Id" class="btn btn-secondary">
                Cancel
            </a>
            <button type="submit" class="btn btn-primary">
                <svg class="w-5 h-5 mr-2" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
                </svg>
                Save Settings
            </button>
        </div>
    </form>
</div>

@section Scripts {
    <partial name="_ValidationScriptsPartial" />
}
```

## Pattern 2: AJAX Form (For Real-Time Feedback)

Use this pattern when you need:
- Real-time feedback without full page reload
- Save individual sections independently
- Complex multi-tab forms

### When to Use AJAX

| Scenario | Use AJAX? |
|----------|-----------|
| Simple create/edit form | No - use Pattern 1 |
| Settings page with multiple tabs | Yes |
| Forms requiring instant feedback | Yes |
| Forms with file upload | No - use Pattern 1 |
| Delete confirmations | No - use Pattern 3 |

### PageModel (Code-Behind)

```csharp
public class SettingsModel : PageModel
{
    [BindProperty]
    public Dictionary<string, string> FormSettings { get; set; } = new();

    [BindProperty]
    public string? ActiveCategory { get; set; }

    public SettingsViewModel ViewModel { get; set; } = new();

    public async Task OnGetAsync(string? category = null)
    {
        ActiveCategory = category ?? "General";
        await LoadViewModelAsync();
    }

    // POST handler returns JSON instead of redirect
    public async Task<IActionResult> OnPostSaveCategoryAsync(string category)
    {
        if (!User.IsInRole("Admin"))
            return Forbid();

        try
        {
            var updateDto = new SettingsUpdateDto { Settings = FormSettings };
            var result = await _settingsService.UpdateSettingsAsync(updateDto, User.Identity?.Name ?? "Unknown");

            if (result.Success)
            {
                return new JsonResult(new
                {
                    success = true,
                    message = $"Settings saved successfully. {result.Changes.Count} setting(s) updated.",
                    restartRequired = result.RestartRequired
                });
            }

            return new JsonResult(new
            {
                success = false,
                message = "Failed to save settings.",
                errors = result.Errors
            }) { StatusCode = 400 };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving settings");
            return new JsonResult(new
            {
                success = false,
                message = "An error occurred while saving settings."
            }) { StatusCode = 500 };
        }
    }
}
```

### JavaScript (AJAX Handler)

```javascript
/**
 * Settings Page AJAX Handler
 *
 * Key patterns:
 * 1. Build FormData correctly (especially for checkboxes)
 * 2. Include anti-forgery token
 * 3. Handle response and show toast feedback
 */
(function() {
    'use strict';

    /**
     * Build form data with proper checkbox handling.
     * Unchecked checkboxes don't submit values by default!
     */
    function buildFormData(form) {
        const formData = new FormData();

        // Add anti-forgery token
        const token = form.querySelector('input[name="__RequestVerificationToken"]');
        if (token) {
            formData.append('__RequestVerificationToken', token.value);
        }

        // Process checkboxes - explicitly set true/false
        const toggles = form.querySelectorAll('input[data-setting-toggle]');
        toggles.forEach(toggle => {
            formData.append(toggle.name, toggle.checked ? 'true' : 'false');
        });

        // Process other inputs
        const inputs = form.querySelectorAll('input:not([type="checkbox"]):not([type="hidden"]), select, textarea');
        inputs.forEach(input => {
            if (input.name && !input.name.startsWith('__')) {
                formData.append(input.name, input.value);
            }
        });

        return formData;
    }

    /**
     * Save settings for a category via AJAX
     */
    async function saveCategory(category) {
        const form = document.getElementById('settingsForm');
        if (!form) return;

        const formData = buildFormData(form);
        const token = document.querySelector('input[name="__RequestVerificationToken"]').value;

        try {
            const response = await fetch(`?handler=SaveCategory&category=${category}`, {
                method: 'POST',
                headers: {
                    'RequestVerificationToken': token
                },
                body: formData
            });

            const data = await response.json();

            if (response.ok && data.success) {
                window.quickActions?.showToast(data.message, 'success');

                if (data.restartRequired) {
                    setTimeout(() => window.location.reload(), 1500);
                }
            } else {
                const errorMsg = data.errors ? data.errors.join(', ') : data.message;
                window.quickActions?.showToast(errorMsg || 'Failed to save.', 'error');
            }
        } catch (error) {
            console.error('Save error:', error);
            window.quickActions?.showToast('An error occurred.', 'error');
        }
    }

    // Expose to window
    window.settingsManager = { saveCategory };
})();
```

### Razor View (AJAX Form)

```html
<form method="post" id="settingsForm">
    @Html.AntiForgeryToken()

    <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
        @foreach (var setting in Model.ViewModel.Settings)
        {
            @await Html.PartialAsync("_SettingField", setting)
        }
    </div>

    <div class="flex justify-end gap-3 mt-6">
        <button type="button"
                onclick="window.settingsManager?.saveCategory('General')"
                class="btn btn-primary">
            Save Changes
        </button>
    </div>
</form>

@section Scripts {
    <script src="~/js/settings.js" asp-append-version="true"></script>
}
```

## Pattern 3: Page Handler Forms (Secondary Actions)

Use this pattern for actions like:
- Delete confirmations
- Cancel operations
- Reset operations

### PageModel

```csharp
public class IndexModel : PageModel
{
    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnPostCancelAsync(
        ulong guildId,
        Guid watchId,
        [FromQuery] int page = 1,
        CancellationToken cancellationToken = default)
    {
        var success = await _service.CancelAsync(watchId, cancellationToken);

        if (success)
        {
            SuccessMessage = "Cancelled successfully.";
        }
        else
        {
            ErrorMessage = "Could not cancel. It may already be completed.";
        }

        return RedirectToPage("Index", new { guildId, page });
    }

    public async Task<IActionResult> OnPostEndVoteAsync(
        ulong guildId,
        Guid watchId,
        [FromQuery] int page = 1,
        CancellationToken cancellationToken = default)
    {
        // Similar pattern...
        return RedirectToPage("Index", new { guildId, page });
    }
}
```

### Razor View with Modal

```html
<!-- Action button that opens modal -->
<button type="button"
        onclick="showCancelModal('@item.Id', '@item.Name')"
        class="btn btn-danger-outline">
    Cancel
</button>

<!-- Modal -->
<div id="cancel-modal" class="hidden fixed inset-0 z-50" role="alertdialog" aria-modal="true">
    <!-- Backdrop -->
    <div class="fixed inset-0 bg-black/70 backdrop-blur-sm" onclick="hideCancelModal()"></div>

    <!-- Dialog -->
    <div class="fixed inset-0 flex items-center justify-center p-4">
        <div class="bg-bg-tertiary border border-border-primary rounded-lg shadow-xl max-w-md w-full">
            <div class="p-6">
                <h3 class="text-lg font-semibold text-text-primary">Confirm Cancel</h3>
                <p class="mt-2 text-sm text-text-secondary">
                    Are you sure you want to cancel <span id="cancel-name" class="font-medium"></span>?
                </p>
            </div>

            <div class="flex justify-end gap-3 px-6 py-4 bg-bg-secondary border-t border-border-primary">
                <button type="button" onclick="hideCancelModal()" class="btn btn-secondary">
                    Keep
                </button>
                <!-- Form with page handler -->
                <form method="post" asp-page-handler="Cancel" asp-route-guildId="@Model.GuildId" class="inline-block">
                    <input type="hidden" id="cancel-item-id" name="watchId" value="" />
                    <button type="submit" class="btn btn-danger">
                        Cancel Item
                    </button>
                </form>
            </div>
        </div>
    </div>
</div>

@section Scripts {
    <script>
        function showCancelModal(id, name) {
            document.getElementById('cancel-item-id').value = id;
            document.getElementById('cancel-name').textContent = name;
            document.getElementById('cancel-modal').classList.remove('hidden');
            document.body.style.overflow = 'hidden';
        }

        function hideCancelModal() {
            document.getElementById('cancel-modal').classList.add('hidden');
            document.body.style.overflow = '';
        }

        document.addEventListener('keydown', (e) => {
            if (e.key === 'Escape' && !document.getElementById('cancel-modal').classList.contains('hidden')) {
                hideCancelModal();
            }
        });
    </script>
}
```

## Pattern 4: GET Filter Forms

Use for search/filter operations where you want bookmarkable URLs.

### PageModel

```csharp
public class IndexModel : PageModel
{
    // No [BindProperty] - bind from query string instead
    public string? SearchTerm { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Status { get; set; }
    public int CurrentPage { get; set; } = 1;

    public async Task<IActionResult> OnGetAsync(
        [FromQuery] string? searchTerm,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        CancellationToken cancellationToken = default)
    {
        SearchTerm = searchTerm;
        StartDate = startDate;
        EndDate = endDate;
        Status = status;
        CurrentPage = page;

        var results = await _service.SearchAsync(
            searchTerm, startDate, endDate, status, page, cancellationToken);

        // Populate ViewModel...
        return Page();
    }
}
```

### Razor View

```html
<!-- GET form - note method="get" -->
<form method="get" class="bg-bg-secondary border border-border-primary rounded-lg p-4">
    <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
        <div>
            <label for="searchTerm" class="block text-sm font-medium text-text-secondary mb-1">Search</label>
            <input type="text"
                   id="searchTerm"
                   name="searchTerm"
                   value="@Model.SearchTerm"
                   class="form-input w-full"
                   placeholder="Search..." />
        </div>

        <div>
            <label for="startDate" class="block text-sm font-medium text-text-secondary mb-1">Start Date</label>
            <input type="date"
                   id="startDate"
                   name="startDate"
                   value="@Model.StartDate?.ToString("yyyy-MM-dd")"
                   class="form-input w-full" />
        </div>

        <div>
            <label for="status" class="block text-sm font-medium text-text-secondary mb-1">Status</label>
            <select id="status" name="status" class="form-select w-full">
                <option value="">All</option>
                <option value="Active" selected="@(Model.Status == "Active")">Active</option>
                <option value="Completed" selected="@(Model.Status == "Completed")">Completed</option>
            </select>
        </div>

        <div class="flex items-end">
            <button type="submit" class="btn btn-primary w-full">Apply Filters</button>
        </div>
    </div>

    <!-- Reset link -->
    <div class="mt-2">
        <a asp-page="Index" class="text-sm text-accent-blue hover:underline">Reset Filters</a>
    </div>
</form>
```

## Common Mistakes to Avoid

### 1. Missing [BindProperty] Attribute

```csharp
// WRONG - Input won't bind on POST
public InputModel Input { get; set; } = new();

// CORRECT
[BindProperty]
public InputModel Input { get; set; } = new();
```

### 2. Wrong Input Name Format

```html
<!-- WRONG - won't bind to Input.Name -->
<input type="text" name="Name" value="@Model.Input.Name" />

<!-- CORRECT -->
<input type="text" name="Input.Name" value="@Model.Input.Name" />

<!-- OR use tag helper -->
<input asp-for="Input.Name" />
```

### 3. Not Reloading ViewModel on Validation Failure

```csharp
// WRONG - ViewModel will be empty on redisplay
if (!ModelState.IsValid)
{
    return Page();
}

// CORRECT
if (!ModelState.IsValid)
{
    await LoadViewModelAsync(Input.EntityId, cancellationToken);
    return Page();
}
```

### 4. Checkbox Binding Issues

HTML checkboxes don't submit a value when unchecked. For boolean properties:

```html
<!-- Method 1: asp-for handles this automatically -->
<input asp-for="Input.IsActive" class="sr-only peer" />

<!-- Method 2: Manual with hidden field fallback -->
<input type="hidden" name="Input.IsActive" value="false" />
<input type="checkbox" name="Input.IsActive" value="true" @(Model.Input.IsActive ? "checked" : "") />
```

For AJAX forms, handle explicitly in JavaScript:
```javascript
const toggles = form.querySelectorAll('input[type="checkbox"]');
toggles.forEach(toggle => {
    formData.append(toggle.name, toggle.checked ? 'true' : 'false');
});
```

### 5. Missing Hidden Fields

```html
<!-- WRONG - EntityId won't be posted -->
<form method="post">
    <input asp-for="Input.Name" />
</form>

<!-- CORRECT - Include ID for updates -->
<form method="post">
    <input type="hidden" name="Input.EntityId" value="@Model.Input.EntityId" />
    <input asp-for="Input.Name" />
</form>
```

### 6. Forgetting Anti-Forgery Token in AJAX

```javascript
// WRONG - Will get 400 error
const response = await fetch('?handler=Save', {
    method: 'POST',
    body: formData
});

// CORRECT - Include token in header
const token = document.querySelector('input[name="__RequestVerificationToken"]').value;
const response = await fetch('?handler=Save', {
    method: 'POST',
    headers: {
        'RequestVerificationToken': token
    },
    body: formData
});
```

### 7. Using ErrorMessage for Both Scenarios

```csharp
// WRONG - ErrorMessage survives redirect
public string? ErrorMessage { get; set; }
SuccessMessage = "Saved!"; // Won't show after redirect

// CORRECT - Use TempData for redirect messages
[TempData]
public string? SuccessMessage { get; set; } // Survives redirect

public string? ErrorMessage { get; set; } // For validation redisplay
```

## Form Component Reference

Use these shared components for consistent styling:

| Component | Usage |
|-----------|-------|
| `Shared/Components/_FormInput` | Text, email, password, number inputs |
| `Shared/Components/_FormSelect` | Dropdown select |
| `Shared/Components/_FormToggle` | Boolean toggle switch |
| `Shared/Components/_FormTextarea` | Multi-line text |
| `Shared/Components/_Alert` | Success/error messages |
| `Shared/Components/_ConfirmationModal` | Delete/reset confirmations |

### FormInputViewModel Properties

```csharp
var inputModel = new FormInputViewModel
{
    Id = "Input_Email",              // HTML id attribute
    Name = "Input.Email",            // HTML name attribute (for binding)
    Label = "Email Address",         // Label text
    Type = "email",                  // HTML input type
    Value = Model.Input.Email,       // Current value
    Placeholder = "user@example.com",
    IsRequired = true,
    HelpText = "Your login email",
    ValidationState = ValidationState.Error,  // None, Success, Error
    ValidationMessage = "Email is required"
};
```

## Checklist for New Forms

- [ ] `[BindProperty]` on InputModel
- [ ] InputModel has validation attributes
- [ ] OnGet populates both ViewModel and Input
- [ ] OnPost checks ModelState.IsValid first
- [ ] OnPost reloads ViewModel on validation failure
- [ ] Hidden field for entity ID on edit forms
- [ ] `asp-validation-summary="ModelOnly"` in form
- [ ] `[TempData]` on SuccessMessage for redirects
- [ ] Form components use `Input.PropertyName` naming
- [ ] Cancel button links back to list/details page
- [ ] `@section Scripts { <partial name="_ValidationScriptsPartial" /> }` included
