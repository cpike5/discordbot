# Form Components Library - Implementation Plan

**Issue:** #37 - Form Components Prototype
**Version:** 1.0
**Created:** 2025-12-07
**Status:** Ready for Implementation

---

## 1. Requirement Summary

Build a comprehensive library of reusable HTML/CSS form component prototypes that follow the established design system specifications. These components will serve as the foundation for all form-based interfaces in the Discord Bot Admin web application.

**Scope:**
- 10 core form component categories
- All validation states (default, hover, focus, error, success, disabled)
- Three size variants (small, base, large)
- Full WCAG 2.1 AA accessibility compliance
- Interactive JavaScript behaviors for complex components
- Consistent with existing `dashboard.html` prototype patterns

**Out of Scope:**
- Backend integration
- Blazor component conversion (separate task)
- Form submission handling
- Real data binding

---

## 2. Architectural Considerations

### 2.1 Existing System Components

The prototype must integrate with:

| Component | Location | Notes |
|-----------|----------|-------|
| Design System | `docs/design-system.md` | Color tokens, typography, spacing definitions |
| Dashboard Prototype | `docs/prototypes/dashboard.html` | Reference for Tailwind config, page structure |
| Tailwind CSS CDN | External | Version used in dashboard prototype |

### 2.2 Technical Approach

**Tailwind CSS CDN with Custom Theme:**
```html
<script src="https://cdn.tailwindcss.com"></script>
<script>
  tailwind.config = {
    theme: {
      extend: {
        colors: {
          // Design system tokens
        }
      }
    }
  }
</script>
```

**Rationale:** Matches the existing dashboard prototype approach, enabling quick iteration without build tooling while maintaining design system consistency.

### 2.3 Design System Token Reference

**Input-Specific Colors:**
```css
/* Border States */
--border-default: #3f4447;
--border-hover: #098ecf;
--border-focus: #098ecf;
--border-error: #ef4444;
--border-success: #10b981;

/* Backgrounds */
--bg-input: #1d2022;
--bg-disabled: #262a2d;

/* Text */
--text-primary: #d7d3d0;
--text-placeholder: #7a7876;
--text-disabled: #7a7876;

/* Accent */
--accent-primary: #cb4e1b;
```

**Focus Shadow:**
```css
box-shadow: 0 0 0 3px rgba(9, 142, 207, 0.15);
```

**Size Variants:**
| Size | Padding | Font Size | Line Height |
|------|---------|-----------|-------------|
| Small | `0.375rem 0.75rem` | `0.75rem` (12px) | 1.4 |
| Base | `0.625rem 0.875rem` | `0.875rem` (14px) | 1.5 |
| Large | `0.75rem 1rem` | `1rem` (16px) | 1.5 |

### 2.4 Accessibility Requirements

All components MUST meet:

- **WCAG 2.1 AA** compliance
- **Keyboard Navigation:** Full tab navigation, Enter/Space activation
- **Screen Reader Support:** Proper ARIA labels, roles, and states
- **Focus Indicators:** Visible 2px blue outline with 2px offset
- **Color Contrast:** Minimum 4.5:1 for normal text, 3:1 for large text
- **Error Identification:** Not relying on color alone (icons + text)

---

## 3. File Structure

```
docs/prototypes/forms/
|
+-- implementation-plan.md        # This document
+-- index.html                    # Component showcase/gallery page
+-- shared-styles.html            # Reusable style definitions (included via iframe or copy)
|
+-- components/
|   +-- 01-text-input.html        # Text input variants
|   +-- 02-validation-states.html # All validation state demos
|   +-- 03-select-dropdown.html   # Select/dropdown components
|   +-- 04-checkbox.html          # Checkbox components
|   +-- 05-radio-button.html      # Radio button components
|   +-- 06-toggle-switch.html     # Toggle switch components
|   +-- 07-form-group.html        # Form group container
|   +-- 08-date-time.html         # Date/time picker components
|   +-- 09-number-input.html      # Number input components
|   +-- 10-file-upload.html       # File upload components
|
+-- examples/
    +-- login-form.html           # Complete login form example
    +-- settings-form.html        # Settings page form example
    +-- search-filters.html       # Search/filter form example
```

### File Organization Rationale

**Separate files per component type:**
- Easier to develop and test in isolation
- Allows parallel development
- Simpler code review
- Components can be viewed individually or via index gallery

**Index gallery page:**
- Provides overview of all components
- Links to individual component pages
- Serves as visual documentation

**Example pages:**
- Demonstrate real-world usage patterns
- Validate component composition works correctly
- Provide copy-paste templates for future pages

---

## 4. Component Specifications

### 4.1 Text Input Component (`01-text-input.html`)

**Variants to Build:**

| Variant | Description | Priority |
|---------|-------------|----------|
| Base Input | Standard text input | P0 |
| With Placeholder | Placeholder text styling | P0 |
| With Helper Text | Descriptive text below input | P0 |
| Disabled State | Non-interactive input | P0 |
| Read-only State | Displays value, not editable | P1 |
| With Left Icon | Icon positioned inside left | P1 |
| With Right Icon | Icon positioned inside right | P1 |
| With Both Icons | Icons on both sides | P2 |
| Character Counter | Shows current/max characters | P1 |
| Textarea | Multi-line text input | P0 |
| Textarea Auto-resize | Grows with content | P2 |
| Password Input | With show/hide toggle | P1 |
| Search Input | With search icon and clear button | P1 |

**HTML Structure (Base):**
```html
<div class="form-group">
  <label for="input-id" class="form-label">
    Label Text
    <span class="form-required">*</span>
  </label>
  <div class="form-input-wrapper">
    <input
      type="text"
      id="input-id"
      class="form-input"
      placeholder="Placeholder text"
      aria-describedby="input-id-help"
    />
  </div>
  <span id="input-id-help" class="form-help">Helper text goes here</span>
</div>
```

**CSS Classes:**
- `.form-input` - Base input styling
- `.form-input-sm` - Small size variant
- `.form-input-lg` - Large size variant
- `.form-input-icon-left` - Left icon padding
- `.form-input-icon-right` - Right icon padding
- `.form-textarea` - Textarea specific styles
- `.form-textarea-resize` - Auto-resize textarea

**JavaScript Required:**
- Character counter update on input
- Textarea auto-resize calculation
- Password visibility toggle
- Search input clear button

---

### 4.2 Input Validation States (`02-validation-states.html`)

**States to Demonstrate:**

| State | Border Color | Icon | Message Style |
|-------|--------------|------|---------------|
| Default | `#3f4447` | None | Gray help text |
| Hover | `#098ecf` | None | Gray help text |
| Focus | `#098ecf` + shadow | None | Gray help text |
| Error | `#ef4444` | Error icon | Red error text |
| Success | `#10b981` | Check icon | Green success text |
| Disabled | `#3f4447` | None | Muted text |

**HTML Structure (Error State):**
```html
<div class="form-group">
  <label for="input-error" class="form-label">Email Address</label>
  <div class="form-input-wrapper">
    <input
      type="email"
      id="input-error"
      class="form-input form-input-error"
      value="invalid-email"
      aria-invalid="true"
      aria-describedby="input-error-message"
    />
    <svg class="form-input-icon-status" aria-hidden="true">
      <!-- Error icon -->
    </svg>
  </div>
  <span id="input-error-message" class="form-error" role="alert">
    <svg aria-hidden="true"><!-- icon --></svg>
    Please enter a valid email address
  </span>
</div>
```

**CSS Classes:**
- `.form-input-error` - Error border state
- `.form-input-success` - Success border state
- `.form-error` - Error message styling
- `.form-success` - Success message styling
- `.form-input-icon-status` - Status icon positioning

**Accessibility Notes:**
- Use `aria-invalid="true"` on error inputs
- Use `role="alert"` on error messages for screen reader announcement
- Error messages must include icon AND text (not color alone)
- Focus shadow changes color to match state

---

### 4.3 Select Dropdown Component (`03-select-dropdown.html`)

**Variants to Build:**

| Variant | Description | Priority |
|---------|-------------|----------|
| Native Select | Browser default with custom arrow | P0 |
| Single Select | Custom dropdown overlay | P1 |
| With Placeholder | "Select an option" state | P0 |
| Disabled | Non-interactive select | P0 |
| Searchable | Filter options by typing | P2 |
| Multi-select | Select multiple options | P2 |
| With Option Groups | Grouped options with headers | P1 |
| With Icons | Options with leading icons | P2 |

**HTML Structure (Native):**
```html
<div class="form-group">
  <label for="select-id" class="form-label">Select Option</label>
  <div class="form-select-wrapper">
    <select id="select-id" class="form-select">
      <option value="" disabled selected>Choose an option</option>
      <option value="1">Option 1</option>
      <option value="2">Option 2</option>
      <option value="3">Option 3</option>
    </select>
    <svg class="form-select-arrow" aria-hidden="true">
      <!-- Chevron down icon -->
    </svg>
  </div>
</div>
```

**HTML Structure (Custom Dropdown):**
```html
<div class="form-group">
  <label id="dropdown-label" class="form-label">Select Server</label>
  <div class="form-dropdown" data-dropdown>
    <button
      type="button"
      class="form-dropdown-trigger"
      aria-haspopup="listbox"
      aria-expanded="false"
      aria-labelledby="dropdown-label"
    >
      <span class="form-dropdown-value">Select a server</span>
      <svg class="form-dropdown-arrow" aria-hidden="true">
        <!-- Chevron icon -->
      </svg>
    </button>
    <ul
      class="form-dropdown-menu"
      role="listbox"
      aria-labelledby="dropdown-label"
      tabindex="-1"
    >
      <li role="option" aria-selected="false" data-value="1">
        Gaming Community
      </li>
      <li role="option" aria-selected="false" data-value="2">
        Dev Community
      </li>
    </ul>
  </div>
</div>
```

**JavaScript Required:**
- Dropdown open/close toggle
- Keyboard navigation (Arrow keys, Enter, Escape)
- Click outside to close
- Search filtering (searchable variant)
- Multi-select toggle (multi-select variant)

**Accessibility Notes:**
- Use `role="listbox"` and `role="option"`
- Manage `aria-expanded` on trigger
- Manage `aria-selected` on options
- Support full keyboard navigation

---

### 4.4 Checkbox Component (`04-checkbox.html`)

**Variants to Build:**

| Variant | Description | Priority |
|---------|-------------|----------|
| Standard | Single checkbox with label | P0 |
| Checked | Pre-selected state | P0 |
| Indeterminate | Partial selection state | P1 |
| Disabled | Non-interactive | P0 |
| Disabled Checked | Non-interactive, checked | P0 |
| Error State | With error styling | P1 |
| Checkbox Group | Multiple related checkboxes | P0 |
| Select All | With "Select All" option | P2 |
| With Description | Label + description text | P1 |

**HTML Structure (Single):**
```html
<label class="form-checkbox">
  <input
    type="checkbox"
    class="form-checkbox-input"
    aria-describedby="checkbox-desc"
  />
  <span class="form-checkbox-box" aria-hidden="true">
    <svg class="form-checkbox-icon">
      <!-- Checkmark icon -->
    </svg>
  </span>
  <span class="form-checkbox-label">
    Enable notifications
    <span id="checkbox-desc" class="form-checkbox-description">
      Receive alerts when new members join
    </span>
  </span>
</label>
```

**HTML Structure (Group):**
```html
<fieldset class="form-checkbox-group">
  <legend class="form-label">Notification Preferences</legend>
  <div class="form-checkbox-group-items">
    <label class="form-checkbox">
      <input type="checkbox" class="form-checkbox-input" name="notifications" value="email" />
      <span class="form-checkbox-box" aria-hidden="true">
        <svg class="form-checkbox-icon"><!-- icon --></svg>
      </span>
      <span class="form-checkbox-label">Email</span>
    </label>
    <label class="form-checkbox">
      <input type="checkbox" class="form-checkbox-input" name="notifications" value="push" />
      <span class="form-checkbox-box" aria-hidden="true">
        <svg class="form-checkbox-icon"><!-- icon --></svg>
      </span>
      <span class="form-checkbox-label">Push notifications</span>
    </label>
  </div>
</fieldset>
```

**CSS Classes:**
- `.form-checkbox` - Checkbox container
- `.form-checkbox-input` - Hidden native input (visually hidden)
- `.form-checkbox-box` - Custom checkbox visual
- `.form-checkbox-icon` - Checkmark/indeterminate icon
- `.form-checkbox-label` - Label text
- `.form-checkbox-description` - Additional description
- `.form-checkbox-group` - Group fieldset
- `.form-checkbox-group-items` - Items container

**JavaScript Required:**
- Indeterminate state management
- Select all functionality
- Group state synchronization

**Accessibility Notes:**
- Use native `<input type="checkbox">` (visually hidden, not `display:none`)
- Custom visual must be `aria-hidden="true"`
- Use `<fieldset>` and `<legend>` for groups
- Indeterminate state: set via JavaScript `checkbox.indeterminate = true`

---

### 4.5 Radio Button Component (`05-radio-button.html`)

**Variants to Build:**

| Variant | Description | Priority |
|---------|-------------|----------|
| Standard | Single radio with label | P0 |
| Selected | Pre-selected state | P0 |
| Disabled | Non-interactive | P0 |
| Error State | With error styling | P1 |
| Vertical Group | Stacked radio options | P0 |
| Horizontal Group | Inline radio options | P1 |
| With Description | Label + description text | P1 |
| Card Style | Radio as selectable card | P2 |

**HTML Structure (Group):**
```html
<fieldset class="form-radio-group">
  <legend class="form-label">Server Region</legend>
  <div class="form-radio-group-items">
    <label class="form-radio">
      <input
        type="radio"
        class="form-radio-input"
        name="region"
        value="us-east"
      />
      <span class="form-radio-circle" aria-hidden="true"></span>
      <span class="form-radio-label">US East</span>
    </label>
    <label class="form-radio">
      <input
        type="radio"
        class="form-radio-input"
        name="region"
        value="eu-west"
      />
      <span class="form-radio-circle" aria-hidden="true"></span>
      <span class="form-radio-label">EU West</span>
    </label>
  </div>
</fieldset>
```

**CSS Classes:**
- `.form-radio` - Radio container
- `.form-radio-input` - Hidden native input
- `.form-radio-circle` - Custom radio visual
- `.form-radio-label` - Label text
- `.form-radio-group` - Group fieldset
- `.form-radio-group-items` - Items container
- `.form-radio-group-horizontal` - Horizontal layout modifier
- `.form-radio-card` - Card style variant

**Accessibility Notes:**
- Use native `<input type="radio">` with proper `name` attribute
- Use `<fieldset>` and `<legend>` for groups
- Arrow keys navigate between options in group

---

### 4.6 Toggle Switch Component (`06-toggle-switch.html`)

**Variants to Build:**

| Variant | Description | Priority |
|---------|-------------|----------|
| Standard | Basic on/off toggle | P0 |
| On State | Active/enabled state | P0 |
| Off State | Inactive/disabled state | P0 |
| Disabled | Non-interactive toggle | P0 |
| Small Size | Compact toggle | P1 |
| Large Size | Prominent toggle | P1 |
| With Labels | On/Off text labels | P1 |
| With Icons | On/Off icons inside | P2 |
| Label Left | Label on left side | P1 |
| Label Right | Label on right side (default) | P0 |

**HTML Structure:**
```html
<label class="form-toggle">
  <input
    type="checkbox"
    class="form-toggle-input"
    role="switch"
    aria-checked="false"
  />
  <span class="form-toggle-track" aria-hidden="true">
    <span class="form-toggle-thumb"></span>
  </span>
  <span class="form-toggle-label">Enable auto-moderation</span>
</label>
```

**CSS Classes:**
- `.form-toggle` - Toggle container
- `.form-toggle-input` - Hidden native input
- `.form-toggle-track` - Toggle background track
- `.form-toggle-thumb` - Toggle knob/thumb
- `.form-toggle-label` - Label text
- `.form-toggle-sm` - Small size
- `.form-toggle-lg` - Large size
- `.form-toggle-label-left` - Label positioning

**Size Specifications:**

| Size | Track Width | Track Height | Thumb Size |
|------|-------------|--------------|------------|
| Small | 36px | 20px | 16px |
| Base | 44px | 24px | 20px |
| Large | 52px | 28px | 24px |

**Animation:**
```css
.form-toggle-thumb {
  transition: transform 0.2s ease-in-out;
}
.form-toggle-track {
  transition: background-color 0.2s ease-in-out;
}
```

**JavaScript Required:**
- Update `aria-checked` on change
- Sync with native checkbox state

**Accessibility Notes:**
- Use `role="switch"` on the input
- Manage `aria-checked` attribute (not just `:checked`)
- Focusable via Tab key
- Toggle via Space key

---

### 4.7 Form Group Container (`07-form-group.html`)

**Variants to Build:**

| Variant | Description | Priority |
|---------|-------------|----------|
| Basic | Label + input + help text | P0 |
| Required | With required indicator | P0 |
| Optional | With "(optional)" text | P1 |
| With Error | Error state with message | P0 |
| With Success | Success state with message | P0 |
| Horizontal | Label and input side-by-side | P1 |
| Inline | Multiple inputs in one row | P1 |
| Grouped | Multiple form groups with dividers | P1 |

**HTML Structure (Standard):**
```html
<div class="form-group">
  <label for="field-id" class="form-label">
    Field Label
    <span class="form-required" aria-label="required">*</span>
  </label>
  <input
    type="text"
    id="field-id"
    class="form-input"
    required
    aria-describedby="field-id-help"
  />
  <span id="field-id-help" class="form-help">
    This is helper text that provides additional context.
  </span>
</div>
```

**HTML Structure (Horizontal):**
```html
<div class="form-group form-group-horizontal">
  <label for="field-id" class="form-label form-label-horizontal">
    Field Label
  </label>
  <div class="form-group-content">
    <input type="text" id="field-id" class="form-input" />
    <span class="form-help">Helper text</span>
  </div>
</div>
```

**HTML Structure (Inline):**
```html
<div class="form-group-inline">
  <div class="form-group form-group-flex">
    <label for="first-name" class="form-label">First Name</label>
    <input type="text" id="first-name" class="form-input" />
  </div>
  <div class="form-group form-group-flex">
    <label for="last-name" class="form-label">Last Name</label>
    <input type="text" id="last-name" class="form-input" />
  </div>
</div>
```

**CSS Classes:**
- `.form-group` - Base form group container
- `.form-label` - Label styling
- `.form-required` - Required indicator
- `.form-optional` - Optional indicator
- `.form-help` - Helper text
- `.form-error` - Error message
- `.form-success` - Success message
- `.form-group-horizontal` - Side-by-side layout
- `.form-group-inline` - Multiple groups in row
- `.form-group-flex` - Flexible width group

---

### 4.8 Date/Time Inputs (`08-date-time.html`)

**Variants to Build:**

| Variant | Description | Priority |
|---------|-------------|----------|
| Date Input (Native) | Browser date picker | P0 |
| Time Input (Native) | Browser time picker | P0 |
| DateTime Input | Combined date and time | P1 |
| Custom Date Picker | Calendar popup | P2 |
| Date Range | Start and end date | P2 |
| Time Zone Select | Timezone dropdown | P2 |

**HTML Structure (Native Date):**
```html
<div class="form-group">
  <label for="date-input" class="form-label">Event Date</label>
  <div class="form-input-wrapper form-input-date">
    <input
      type="date"
      id="date-input"
      class="form-input"
      min="2025-01-01"
      max="2025-12-31"
    />
    <svg class="form-input-icon-right" aria-hidden="true">
      <!-- Calendar icon -->
    </svg>
  </div>
</div>
```

**HTML Structure (Date Range):**
```html
<fieldset class="form-group">
  <legend class="form-label">Date Range</legend>
  <div class="form-date-range">
    <div class="form-group">
      <label for="start-date" class="sr-only">Start Date</label>
      <input type="date" id="start-date" class="form-input" />
    </div>
    <span class="form-date-range-separator" aria-hidden="true">to</span>
    <div class="form-group">
      <label for="end-date" class="sr-only">End Date</label>
      <input type="date" id="end-date" class="form-input" />
    </div>
  </div>
</fieldset>
```

**CSS Classes:**
- `.form-input-date` - Date input wrapper
- `.form-input-time` - Time input wrapper
- `.form-date-range` - Date range container
- `.form-date-range-separator` - "to" separator

**JavaScript Required:**
- Custom calendar picker (if implemented)
- Date range validation
- Date formatting display

**Notes:**
- Native date/time inputs vary by browser
- Custom calendar picker is P2 priority
- Consider using a library for complex date picking

---

### 4.9 Number Input (`09-number-input.html`)

**Variants to Build:**

| Variant | Description | Priority |
|---------|-------------|----------|
| Basic Number | Native number input | P0 |
| With Stepper | +/- buttons | P1 |
| With Min/Max | Bounded range | P0 |
| With Step | Custom increment | P0 |
| Currency | With currency symbol | P2 |
| Percentage | With % symbol | P2 |
| Disabled | Non-interactive | P0 |

**HTML Structure (With Stepper):**
```html
<div class="form-group">
  <label for="quantity" class="form-label">Quantity</label>
  <div class="form-number-stepper">
    <button
      type="button"
      class="form-number-btn form-number-decrement"
      aria-label="Decrease quantity"
      data-action="decrement"
    >
      <svg aria-hidden="true"><!-- Minus icon --></svg>
    </button>
    <input
      type="number"
      id="quantity"
      class="form-input form-number-input"
      value="1"
      min="0"
      max="100"
      step="1"
      aria-describedby="quantity-help"
    />
    <button
      type="button"
      class="form-number-btn form-number-increment"
      aria-label="Increase quantity"
      data-action="increment"
    >
      <svg aria-hidden="true"><!-- Plus icon --></svg>
    </button>
  </div>
  <span id="quantity-help" class="form-help">Enter a value between 0 and 100</span>
</div>
```

**CSS Classes:**
- `.form-number-stepper` - Stepper container
- `.form-number-input` - Number input (hides native spinners)
- `.form-number-btn` - Stepper buttons
- `.form-number-decrement` - Decrement button
- `.form-number-increment` - Increment button
- `.form-number-prefix` - Prefix text/symbol
- `.form-number-suffix` - Suffix text/symbol

**JavaScript Required:**
- Increment/decrement button handlers
- Min/max boundary enforcement
- Step value handling
- Input validation (numeric only)

**Accessibility Notes:**
- Use `aria-label` on stepper buttons
- Announce value changes to screen readers
- Disable buttons at min/max boundaries

---

### 4.10 File Upload (`10-file-upload.html`)

**Variants to Build:**

| Variant | Description | Priority |
|---------|-------------|----------|
| Basic Upload | Simple file input button | P0 |
| Styled Button | Custom styled trigger | P0 |
| Drag & Drop Zone | Dropzone with visual feedback | P1 |
| With Preview | Image/file preview | P1 |
| Multiple Files | Multi-file selection | P1 |
| File List | Show selected files list | P1 |
| Progress Indicator | Upload progress bar | P2 |
| Size/Type Validation | Client-side validation | P1 |

**HTML Structure (Styled Button):**
```html
<div class="form-group">
  <label for="file-upload" class="form-label">Upload Avatar</label>
  <div class="form-file">
    <input
      type="file"
      id="file-upload"
      class="form-file-input"
      accept="image/*"
      aria-describedby="file-help"
    />
    <label for="file-upload" class="form-file-trigger">
      <svg aria-hidden="true"><!-- Upload icon --></svg>
      <span>Choose file</span>
    </label>
    <span class="form-file-name">No file chosen</span>
  </div>
  <span id="file-help" class="form-help">PNG, JPG up to 5MB</span>
</div>
```

**HTML Structure (Drag & Drop):**
```html
<div class="form-group">
  <label class="form-label">Upload Files</label>
  <div
    class="form-dropzone"
    data-dropzone
    role="button"
    tabindex="0"
    aria-label="Drop files here or click to upload"
  >
    <input
      type="file"
      class="form-dropzone-input"
      multiple
      accept=".pdf,.doc,.docx"
    />
    <div class="form-dropzone-content">
      <svg class="form-dropzone-icon" aria-hidden="true">
        <!-- Upload cloud icon -->
      </svg>
      <p class="form-dropzone-text">
        <span class="form-dropzone-primary">Click to upload</span>
        <span class="form-dropzone-secondary">or drag and drop</span>
      </p>
      <p class="form-dropzone-hint">PDF, DOC up to 10MB</p>
    </div>
  </div>

  <!-- File list -->
  <ul class="form-file-list" aria-live="polite">
    <li class="form-file-item">
      <svg aria-hidden="true"><!-- File icon --></svg>
      <span class="form-file-item-name">document.pdf</span>
      <span class="form-file-item-size">2.4 MB</span>
      <button type="button" class="form-file-item-remove" aria-label="Remove document.pdf">
        <svg aria-hidden="true"><!-- X icon --></svg>
      </button>
    </li>
  </ul>
</div>
```

**CSS Classes:**
- `.form-file` - File input container
- `.form-file-input` - Hidden native input
- `.form-file-trigger` - Styled upload button
- `.form-file-name` - Selected filename display
- `.form-dropzone` - Drag & drop zone
- `.form-dropzone-active` - Drag over state
- `.form-dropzone-content` - Dropzone inner content
- `.form-file-list` - Uploaded files list
- `.form-file-item` - Individual file row
- `.form-file-preview` - Image preview

**JavaScript Required:**
- File selection handler
- Drag and drop events (dragenter, dragover, dragleave, drop)
- File preview generation
- File list management
- Size/type validation
- Remove file handler

**Accessibility Notes:**
- Use `aria-live="polite"` on file list for dynamic updates
- Provide clear feedback on drag states
- Announce validation errors
- Remove buttons need accessible labels

---

## 5. Implementation Order

### Phase 1: Foundation (Components 1-2, 7)

**Estimated Time:** 4-6 hours

**Build Order:**
1. `07-form-group.html` - Container patterns first
2. `01-text-input.html` - Base input component
3. `02-validation-states.html` - State patterns

**Rationale:** Form groups provide the structural foundation. Text inputs are the most common and establish base patterns. Validation states are needed by all other components.

### Phase 2: Selection Controls (Components 4-6)

**Estimated Time:** 4-6 hours

**Build Order:**
1. `04-checkbox.html` - Single and group
2. `05-radio-button.html` - Similar patterns to checkbox
3. `06-toggle-switch.html` - Binary control variant

**Rationale:** These share similar patterns (hidden input + custom visual). Build in order of complexity.

### Phase 3: Advanced Inputs (Components 3, 8-9)

**Estimated Time:** 6-8 hours

**Build Order:**
1. `03-select-dropdown.html` - Native select first, then custom
2. `09-number-input.html` - With stepper buttons
3. `08-date-time.html` - Native inputs, optional custom picker

**Rationale:** These require more JavaScript interactivity. Custom dropdown is most complex.

### Phase 4: File Upload & Gallery (Component 10, Index)

**Estimated Time:** 4-6 hours

**Build Order:**
1. `10-file-upload.html` - All upload variants
2. `index.html` - Component gallery/showcase
3. Example pages (as time permits)

**Rationale:** File upload is self-contained. Index page brings everything together.

---

## 6. Shared Styles Template

Create consistent styles across all component files. Each file should include:

```html
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>[Component Name] - Form Components Library</title>

  <!-- Tailwind CSS CDN -->
  <script src="https://cdn.tailwindcss.com"></script>

  <!-- Tailwind Config (Design System) -->
  <script>
    tailwind.config = {
      theme: {
        extend: {
          colors: {
            bg: {
              primary: '#1d2022',
              secondary: '#262a2d',
              tertiary: '#2f3336',
              hover: '#363a3e',
            },
            text: {
              primary: '#d7d3d0',
              secondary: '#a8a5a3',
              tertiary: '#7a7876',
              inverse: '#1d2022',
            },
            accent: {
              orange: {
                DEFAULT: '#cb4e1b',
                hover: '#e5591f',
                active: '#b04517',
                muted: 'rgba(203, 78, 27, 0.2)',
              },
              blue: {
                DEFAULT: '#098ecf',
                hover: '#0ba3ea',
                active: '#0879b3',
                muted: 'rgba(9, 142, 207, 0.2)',
              },
            },
            success: {
              DEFAULT: '#10b981',
              bg: 'rgba(16, 185, 129, 0.1)',
              border: 'rgba(16, 185, 129, 0.3)',
            },
            warning: {
              DEFAULT: '#f59e0b',
              bg: 'rgba(245, 158, 11, 0.1)',
              border: 'rgba(245, 158, 11, 0.3)',
            },
            error: {
              DEFAULT: '#ef4444',
              bg: 'rgba(239, 68, 68, 0.1)',
              border: 'rgba(239, 68, 68, 0.3)',
            },
            info: {
              DEFAULT: '#06b6d4',
              bg: 'rgba(6, 182, 212, 0.1)',
              border: 'rgba(6, 182, 212, 0.3)',
            },
            border: {
              primary: '#3f4447',
              secondary: '#2f3336',
              focus: '#098ecf',
            },
          },
          fontFamily: {
            sans: ['-apple-system', 'BlinkMacSystemFont', '"Segoe UI"', 'Roboto', '"Helvetica Neue"', 'Arial', 'sans-serif'],
            mono: ['ui-monospace', 'SFMono-Regular', '"SF Mono"', 'Menlo', 'Monaco', 'Consolas', 'monospace'],
          },
        },
      },
    }
  </script>

  <style>
    /* ========================================
       FORM COMPONENT STYLES
       ======================================== */

    /* Focus states */
    *:focus-visible {
      outline: 2px solid #098ecf;
      outline-offset: 2px;
    }

    /* Screen reader only */
    .sr-only {
      position: absolute;
      width: 1px;
      height: 1px;
      padding: 0;
      margin: -1px;
      overflow: hidden;
      clip: rect(0, 0, 0, 0);
      white-space: nowrap;
      border: 0;
    }

    /* ----------------------------------------
       Form Group
       ---------------------------------------- */
    .form-group {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
      margin-bottom: 1rem;
    }

    .form-label {
      font-size: 0.875rem;
      font-weight: 600;
      color: #d7d3d0;
    }

    .form-required {
      color: #ef4444;
      margin-left: 0.25rem;
    }

    .form-optional {
      font-weight: 400;
      color: #7a7876;
      margin-left: 0.25rem;
    }

    .form-help {
      font-size: 0.75rem;
      color: #a8a5a3;
    }

    .form-error {
      display: flex;
      align-items: center;
      gap: 0.375rem;
      font-size: 0.75rem;
      color: #ef4444;
    }

    .form-success {
      display: flex;
      align-items: center;
      gap: 0.375rem;
      font-size: 0.75rem;
      color: #10b981;
    }

    /* ----------------------------------------
       Text Input
       ---------------------------------------- */
    .form-input {
      width: 100%;
      padding: 0.625rem 0.875rem;
      font-size: 0.875rem;
      color: #d7d3d0;
      background-color: #1d2022;
      border: 1px solid #3f4447;
      border-radius: 0.375rem;
      transition: border-color 0.15s ease-in-out, box-shadow 0.15s ease-in-out;
    }

    .form-input::placeholder {
      color: #7a7876;
    }

    .form-input:hover:not(:disabled) {
      border-color: #098ecf;
    }

    .form-input:focus {
      outline: none;
      border-color: #098ecf;
      box-shadow: 0 0 0 3px rgba(9, 142, 207, 0.15);
    }

    .form-input:disabled {
      background-color: #262a2d;
      color: #7a7876;
      cursor: not-allowed;
      opacity: 0.6;
    }

    /* Size variants */
    .form-input-sm {
      padding: 0.375rem 0.75rem;
      font-size: 0.75rem;
    }

    .form-input-lg {
      padding: 0.75rem 1rem;
      font-size: 1rem;
    }

    /* Validation states */
    .form-input-error {
      border-color: #ef4444;
    }

    .form-input-error:focus {
      border-color: #ef4444;
      box-shadow: 0 0 0 3px rgba(239, 68, 68, 0.15);
    }

    .form-input-success {
      border-color: #10b981;
    }

    .form-input-success:focus {
      border-color: #10b981;
      box-shadow: 0 0 0 3px rgba(16, 185, 129, 0.15);
    }

    /* ----------------------------------------
       Textarea
       ---------------------------------------- */
    .form-textarea {
      min-height: 100px;
      resize: vertical;
    }

    .form-textarea-noresize {
      resize: none;
    }

    /* ----------------------------------------
       Select
       ---------------------------------------- */
    .form-select {
      width: 100%;
      padding: 0.625rem 2.5rem 0.625rem 0.875rem;
      font-size: 0.875rem;
      color: #d7d3d0;
      background-color: #1d2022;
      background-image: url("data:image/svg+xml,%3csvg xmlns='http://www.w3.org/2000/svg' fill='none' viewBox='0 0 20 20'%3e%3cpath stroke='%23a8a5a3' stroke-linecap='round' stroke-linejoin='round' stroke-width='1.5' d='M6 8l4 4 4-4'/%3e%3c/svg%3e");
      background-position: right 0.5rem center;
      background-repeat: no-repeat;
      background-size: 1.5em 1.5em;
      border: 1px solid #3f4447;
      border-radius: 0.375rem;
      cursor: pointer;
      appearance: none;
      transition: border-color 0.15s ease-in-out, box-shadow 0.15s ease-in-out;
    }

    .form-select:hover:not(:disabled) {
      border-color: #098ecf;
    }

    .form-select:focus {
      outline: none;
      border-color: #098ecf;
      box-shadow: 0 0 0 3px rgba(9, 142, 207, 0.15);
    }

    .form-select:disabled {
      background-color: #262a2d;
      color: #7a7876;
      cursor: not-allowed;
      opacity: 0.6;
    }

    /* ----------------------------------------
       Checkbox
       ---------------------------------------- */
    .form-checkbox {
      display: flex;
      align-items: flex-start;
      gap: 0.625rem;
      cursor: pointer;
    }

    .form-checkbox-input {
      position: absolute;
      opacity: 0;
      width: 0;
      height: 0;
    }

    .form-checkbox-box {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 1.125rem;
      height: 1.125rem;
      flex-shrink: 0;
      margin-top: 0.125rem;
      background-color: #1d2022;
      border: 1px solid #3f4447;
      border-radius: 0.25rem;
      transition: all 0.15s ease-in-out;
    }

    .form-checkbox-icon {
      width: 0.75rem;
      height: 0.75rem;
      color: white;
      opacity: 0;
      transform: scale(0.8);
      transition: all 0.15s ease-in-out;
    }

    .form-checkbox:hover .form-checkbox-box {
      border-color: #098ecf;
    }

    .form-checkbox-input:focus-visible + .form-checkbox-box {
      outline: 2px solid #098ecf;
      outline-offset: 2px;
    }

    .form-checkbox-input:checked + .form-checkbox-box {
      background-color: #cb4e1b;
      border-color: #cb4e1b;
    }

    .form-checkbox-input:checked + .form-checkbox-box .form-checkbox-icon {
      opacity: 1;
      transform: scale(1);
    }

    .form-checkbox-input:disabled + .form-checkbox-box {
      background-color: #262a2d;
      border-color: #3f4447;
      cursor: not-allowed;
      opacity: 0.6;
    }

    .form-checkbox-label {
      font-size: 0.875rem;
      color: #d7d3d0;
    }

    /* ----------------------------------------
       Radio
       ---------------------------------------- */
    .form-radio {
      display: flex;
      align-items: flex-start;
      gap: 0.625rem;
      cursor: pointer;
    }

    .form-radio-input {
      position: absolute;
      opacity: 0;
      width: 0;
      height: 0;
    }

    .form-radio-circle {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 1.125rem;
      height: 1.125rem;
      flex-shrink: 0;
      margin-top: 0.125rem;
      background-color: #1d2022;
      border: 1px solid #3f4447;
      border-radius: 50%;
      transition: all 0.15s ease-in-out;
    }

    .form-radio-circle::after {
      content: '';
      width: 0.5rem;
      height: 0.5rem;
      background-color: white;
      border-radius: 50%;
      opacity: 0;
      transform: scale(0);
      transition: all 0.15s ease-in-out;
    }

    .form-radio:hover .form-radio-circle {
      border-color: #098ecf;
    }

    .form-radio-input:focus-visible + .form-radio-circle {
      outline: 2px solid #098ecf;
      outline-offset: 2px;
    }

    .form-radio-input:checked + .form-radio-circle {
      background-color: #cb4e1b;
      border-color: #cb4e1b;
    }

    .form-radio-input:checked + .form-radio-circle::after {
      opacity: 1;
      transform: scale(1);
    }

    .form-radio-label {
      font-size: 0.875rem;
      color: #d7d3d0;
    }

    /* ----------------------------------------
       Toggle Switch
       ---------------------------------------- */
    .form-toggle {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      cursor: pointer;
    }

    .form-toggle-input {
      position: absolute;
      opacity: 0;
      width: 0;
      height: 0;
    }

    .form-toggle-track {
      position: relative;
      display: inline-block;
      width: 2.75rem;
      height: 1.5rem;
      background-color: #3f4447;
      border-radius: 9999px;
      transition: background-color 0.2s ease-in-out;
    }

    .form-toggle-thumb {
      position: absolute;
      top: 0.125rem;
      left: 0.125rem;
      width: 1.25rem;
      height: 1.25rem;
      background-color: #d7d3d0;
      border-radius: 50%;
      transition: transform 0.2s ease-in-out;
    }

    .form-toggle-input:checked + .form-toggle-track {
      background-color: #cb4e1b;
    }

    .form-toggle-input:checked + .form-toggle-track .form-toggle-thumb {
      transform: translateX(1.25rem);
    }

    .form-toggle-input:focus-visible + .form-toggle-track {
      outline: 2px solid #098ecf;
      outline-offset: 2px;
    }

    .form-toggle-input:disabled + .form-toggle-track {
      opacity: 0.5;
      cursor: not-allowed;
    }

    .form-toggle-label {
      font-size: 0.875rem;
      color: #d7d3d0;
    }

    /* ----------------------------------------
       File Upload
       ---------------------------------------- */
    .form-file-input {
      position: absolute;
      opacity: 0;
      width: 0;
      height: 0;
    }

    .form-file-trigger {
      display: inline-flex;
      align-items: center;
      gap: 0.5rem;
      padding: 0.625rem 1rem;
      font-size: 0.875rem;
      font-weight: 500;
      color: #d7d3d0;
      background-color: #262a2d;
      border: 1px solid #3f4447;
      border-radius: 0.375rem;
      cursor: pointer;
      transition: all 0.15s ease-in-out;
    }

    .form-file-trigger:hover {
      border-color: #098ecf;
      background-color: #363a3e;
    }

    .form-dropzone {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      padding: 2rem;
      border: 2px dashed #3f4447;
      border-radius: 0.5rem;
      cursor: pointer;
      transition: all 0.15s ease-in-out;
    }

    .form-dropzone:hover,
    .form-dropzone-active {
      border-color: #098ecf;
      background-color: rgba(9, 142, 207, 0.05);
    }

    .form-dropzone-icon {
      width: 3rem;
      height: 3rem;
      color: #7a7876;
      margin-bottom: 1rem;
    }

    .form-dropzone-text {
      text-align: center;
      color: #d7d3d0;
    }

    .form-dropzone-hint {
      font-size: 0.75rem;
      color: #7a7876;
      margin-top: 0.5rem;
    }

    /* Custom scrollbar for dark theme */
    ::-webkit-scrollbar {
      width: 8px;
      height: 8px;
    }
    ::-webkit-scrollbar-track {
      background: #1d2022;
    }
    ::-webkit-scrollbar-thumb {
      background: #3f4447;
      border-radius: 4px;
    }
    ::-webkit-scrollbar-thumb:hover {
      background: #4a4f52;
    }
  </style>
</head>
<body class="bg-bg-primary text-text-primary font-sans antialiased min-h-screen p-8">
  <!-- Component content here -->
</body>
</html>
```

---

## 7. Testing Approach

### 7.1 Visual Testing

**Manual Checklist for Each Component:**
- [ ] All variants render correctly
- [ ] Colors match design system tokens
- [ ] Spacing is consistent
- [ ] Size variants display properly
- [ ] Matches dashboard.html visual style

### 7.2 Interaction Testing

**Manual Checklist:**
- [ ] Click/tap triggers expected behavior
- [ ] Hover states appear correctly
- [ ] Focus states are visible
- [ ] Disabled states prevent interaction
- [ ] JavaScript functionality works

### 7.3 Accessibility Testing

**Manual Checklist:**
- [ ] Tab navigation works correctly
- [ ] Focus order is logical
- [ ] Enter/Space activates controls
- [ ] Arrow keys work for groups/dropdowns
- [ ] Escape closes dropdowns/modals
- [ ] Screen reader announces labels and states
- [ ] ARIA attributes are correct
- [ ] Color contrast meets requirements

**Automated Tools:**
- Browser DevTools Accessibility panel
- axe DevTools browser extension
- WAVE browser extension

### 7.4 Responsive Testing

**Breakpoints to Test:**
- Mobile: 375px width
- Tablet: 768px width
- Desktop: 1280px width

**Check:**
- [ ] Components stack correctly on mobile
- [ ] Touch targets are minimum 44x44px
- [ ] Text remains readable
- [ ] No horizontal scroll

### 7.5 Browser Testing

**Target Browsers:**
- Chrome (latest)
- Firefox (latest)
- Safari (latest)
- Edge (latest)

**Check:**
- [ ] Native inputs render acceptably
- [ ] CSS transitions work
- [ ] JavaScript functions correctly

---

## 8. Acceptance Criteria

### Overall
- [ ] All 10 component files created and functional
- [ ] Index gallery page links to all components
- [ ] Shared styles are consistent across files
- [ ] Components match design system specifications

### Per Component
- [ ] All priority P0 and P1 variants implemented
- [ ] All interaction states work (hover, focus, active, disabled)
- [ ] Keyboard navigation functions correctly
- [ ] Screen reader announces component properly
- [ ] Error and success states display correctly
- [ ] Size variants (sm, base, lg) work where applicable

### Code Quality
- [ ] HTML is semantic and valid
- [ ] CSS follows design system tokens
- [ ] JavaScript is modular and reusable
- [ ] No console errors
- [ ] Comments explain complex logic

---

## 9. Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Native inputs vary by browser | High | Medium | Accept variation; custom components for critical UI |
| Complex JavaScript for dropdowns | Medium | Medium | Use simple patterns; defer to library if needed |
| Accessibility requirements missed | Medium | High | Test early; use automated tools; follow ARIA patterns |
| Design inconsistency across files | Low | Medium | Use shared styles template; review before finalizing |
| Scope creep on P2 features | Medium | Low | Strictly follow priority; defer P2 if time constrained |

---

## 10. Subagent Task Summary

### html-prototyper

**Primary Assignment:** Execute this implementation plan.

**Deliverables:**
1. All component files in `docs/prototypes/forms/components/`
2. Index gallery page `docs/prototypes/forms/index.html`
3. At least one example page in `docs/prototypes/forms/examples/`

**Working Order:**
1. Phase 1: Form Group, Text Input, Validation States
2. Phase 2: Checkbox, Radio, Toggle
3. Phase 3: Select/Dropdown, Number Input, Date/Time
4. Phase 4: File Upload, Index Gallery

**Key Constraints:**
- Follow the shared styles template exactly
- Implement P0 and P1 priorities; P2 if time permits
- Test accessibility manually
- Match visual style of existing dashboard.html

### docs-writer

**Future Task:** Document the form component library after implementation.

**Deliverables (deferred):**
- Component usage documentation
- Accessibility guidelines per component
- Copy-paste code snippets

---

## Appendix A: Hero Icons Reference

Use Hero Icons (outline style, 24x24) for all component icons. Common icons needed:

| Icon | Usage |
|------|-------|
| `check` | Checkbox checkmark, success indicator |
| `minus` | Indeterminate checkbox, number decrement |
| `plus` | Number increment |
| `chevron-down` | Select dropdown arrow |
| `x-mark` | Clear button, remove file |
| `exclamation-circle` | Error indicator |
| `check-circle` | Success indicator |
| `eye` | Show password |
| `eye-slash` | Hide password |
| `magnifying-glass` | Search icon |
| `calendar` | Date picker |
| `clock` | Time picker |
| `arrow-up-tray` | Upload icon |
| `document` | File icon |
| `photo` | Image icon |

**SVG Example (Checkmark):**
```html
<svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
  <path stroke-linecap="round" stroke-linejoin="round" d="M5 13l4 4L19 7" />
</svg>
```

---

## Appendix B: Color Token Quick Reference

```css
/* Backgrounds */
#1d2022  /* bg-primary - input background */
#262a2d  /* bg-secondary - disabled background */
#2f3336  /* bg-tertiary - dropdown menu */
#363a3e  /* bg-hover - hover states */

/* Text */
#d7d3d0  /* text-primary - input text */
#a8a5a3  /* text-secondary - labels */
#7a7876  /* text-tertiary - placeholder, disabled */

/* Borders */
#3f4447  /* border-primary - default border */
#098ecf  /* border-focus - focus/hover border */
#ef4444  /* error border */
#10b981  /* success border */

/* Accents */
#cb4e1b  /* accent-orange - primary action, checked state */
#098ecf  /* accent-blue - focus, secondary action */

/* Semantic */
#ef4444  /* error */
#10b981  /* success */
#f59e0b  /* warning */
#06b6d4  /* info */
```

---

*End of Implementation Plan*
