# Discord Bot Admin UI - Design System

**Version:** 1.2
**Last Updated:** 2025-12-27
**Target Framework:** .NET Blazor / HTML/CSS Prototypes

---

## Overview

This design system provides a comprehensive visual language for the Discord bot admin management web UI. The aesthetic is modern, clean, and professional with a Discord-inspired dark theme suitable for extended administrative sessions.

### Design Principles

- **Clarity First**: Prioritize readability and usability over decorative elements
- **Consistency**: Maintain uniform patterns across all interfaces
- **Accessibility**: Ensure WCAG 2.1 AA compliance for all interactive elements
- **Performance**: Minimize CSS overhead and optimize for fast rendering
- **Dark-Optimized**: Designed for comfortable extended use in low-light environments

---

## 1. Color Palette

### Base Colors

#### Background Layers

```css
/* Primary Background */
--color-bg-primary: #1d2022;      /* Dark charcoal - main background */
--color-bg-secondary: #262a2d;    /* Slightly lighter - cards, panels */
--color-bg-tertiary: #2f3336;     /* Elevated elements - modals, dropdowns */
--color-bg-hover: #363a3e;        /* Interactive hover states */
```

**Usage Guidelines:**
- Use `bg-primary` for main page backgrounds
- Use `bg-secondary` for cards, panels, and content containers
- Use `bg-tertiary` for modals, popovers, and elevated UI elements
- Use `bg-hover` for hover states on interactive surfaces

#### Text Colors

```css
/* Text Hierarchy */
--color-text-primary: #d7d3d0;    /* Light warm gray - primary text */
--color-text-secondary: #a8a5a3;  /* Dimmed - secondary text, labels */
--color-text-tertiary: #7a7876;   /* Muted - disabled text, placeholders */
--color-text-inverse: #1d2022;    /* Dark text on light backgrounds */
```

**Contrast Ratios:**
- `text-primary` on `bg-primary`: **10.8:1** (AAA)
- `text-secondary` on `bg-primary`: **5.9:1** (AA)
- `text-tertiary` on `bg-primary`: **3.5:1** (AA for large text)

#### Brand Accent Colors

```css
/* Primary Accent */
--color-accent-orange: #cb4e1b;        /* Burnt orange - primary actions */
--color-accent-orange-hover: #e5591f;  /* Hover state */
--color-accent-orange-active: #b04517; /* Active/pressed state */
--color-accent-orange-muted: #cb4e1b33; /* 20% opacity - subtle backgrounds */

/* Secondary Accent */
--color-accent-blue: #098ecf;          /* Bright blue - secondary actions */
--color-accent-blue-hover: #0ba3ea;    /* Hover state */
--color-accent-blue-active: #0879b3;   /* Active/pressed state */
--color-accent-blue-muted: #098ecf33;  /* 20% opacity - subtle backgrounds */
```

**Usage Guidelines:**
- Use **orange** for primary CTAs, important actions, and active navigation
- Use **blue** for secondary actions, informational elements, and links

### Semantic Colors

#### Success, Warning, Error, Info

```css
/* Success - Green palette */
--color-success: #10b981;           /* Emerald green */
--color-success-bg: #10b98120;      /* Subtle background */
--color-success-border: #10b98150;  /* Border variant */

/* Warning - Amber palette */
--color-warning: #f59e0b;           /* Amber */
--color-warning-bg: #f59e0b20;      /* Subtle background */
--color-warning-border: #f59e0b50;  /* Border variant */

/* Error - Red palette */
--color-error: #ef4444;             /* Red */
--color-error-bg: #ef444420;        /* Subtle background */
--color-error-border: #ef444450;    /* Border variant */

/* Info - Cyan palette */
--color-info: #06b6d4;              /* Cyan */
--color-info-bg: #06b6d420;         /* Subtle background */
--color-info-border: #06b6d450;     /* Border variant */
```

**Accessibility Note:** All semantic colors meet WCAG AA standards when used with appropriate text colors (white or primary text).

### Border Colors

```css
--color-border-primary: #3f4447;    /* Default borders */
--color-border-secondary: #2f3336;  /* Subtle dividers */
--color-border-focus: #098ecf;      /* Focus rings (blue accent) */
```

---

## 2. Typography

### Font Stack

```css
/* Primary Font Family - System UI Stack */
--font-family-sans:
  -apple-system,
  BlinkMacSystemFont,
  "Segoe UI",
  Roboto,
  "Helvetica Neue",
  Arial,
  sans-serif,
  "Apple Color Emoji",
  "Segoe UI Emoji",
  "Segoe UI Symbol";

/* Monospace - For code, IDs, tokens */
--font-family-mono:
  ui-monospace,
  SFMono-Regular,
  "SF Mono",
  Menlo,
  Monaco,
  Consolas,
  "Liberation Mono",
  "Courier New",
  monospace;
```

**Rationale:** System font stack ensures optimal rendering on all platforms and zero web font load time.

### Type Scale

#### Headings

```css
/* Display - For hero sections, large headings */
.text-display {
  font-size: 3rem;        /* 48px */
  line-height: 1.1;
  font-weight: 700;
  letter-spacing: -0.02em;
  color: var(--color-text-primary);
}

/* H1 - Page titles */
.text-h1 {
  font-size: 2.25rem;     /* 36px */
  line-height: 1.2;
  font-weight: 700;
  letter-spacing: -0.01em;
  color: var(--color-text-primary);
}

/* H2 - Section titles */
.text-h2 {
  font-size: 1.875rem;    /* 30px */
  line-height: 1.3;
  font-weight: 600;
  letter-spacing: -0.01em;
  color: var(--color-text-primary);
}

/* H3 - Subsection titles */
.text-h3 {
  font-size: 1.5rem;      /* 24px */
  line-height: 1.35;
  font-weight: 600;
  color: var(--color-text-primary);
}

/* H4 - Card titles, component headers */
.text-h4 {
  font-size: 1.25rem;     /* 20px */
  line-height: 1.4;
  font-weight: 600;
  color: var(--color-text-primary);
}

/* H5 - Small section headers */
.text-h5 {
  font-size: 1.125rem;    /* 18px */
  line-height: 1.4;
  font-weight: 600;
  color: var(--color-text-primary);
}

/* H6 - Label headers */
.text-h6 {
  font-size: 1rem;        /* 16px */
  line-height: 1.5;
  font-weight: 600;
  color: var(--color-text-primary);
}
```

#### Body Text

```css
/* Large body text */
.text-lg {
  font-size: 1.125rem;    /* 18px */
  line-height: 1.75;
  font-weight: 400;
  color: var(--color-text-primary);
}

/* Base body text */
.text-base {
  font-size: 1rem;        /* 16px */
  line-height: 1.5;
  font-weight: 400;
  color: var(--color-text-primary);
}

/* Small text - labels, captions */
.text-sm {
  font-size: 0.875rem;    /* 14px */
  line-height: 1.4;
  font-weight: 400;
  color: var(--color-text-secondary);
}

/* Extra small - metadata, timestamps */
.text-xs {
  font-size: 0.75rem;     /* 12px */
  line-height: 1.3;
  font-weight: 400;
  color: var(--color-text-tertiary);
}
```

#### Utility Classes

```css
/* Font Weights */
.font-normal { font-weight: 400; }
.font-medium { font-weight: 500; }
.font-semibold { font-weight: 600; }
.font-bold { font-weight: 700; }

/* Text Colors */
.text-primary { color: var(--color-text-primary); }
.text-secondary { color: var(--color-text-secondary); }
.text-tertiary { color: var(--color-text-tertiary); }
.text-orange { color: var(--color-accent-orange); }
.text-blue { color: var(--color-accent-blue); }

/* Monospace */
.font-mono { font-family: var(--font-family-mono); }
```

---

## 3. Spacing & Layout

### Spacing Scale

Following Tailwind CSS conventions (base unit: 0.25rem = 4px):

```css
--space-0: 0;           /* 0px */
--space-1: 0.25rem;     /* 4px */
--space-2: 0.5rem;      /* 8px */
--space-3: 0.75rem;     /* 12px */
--space-4: 1rem;        /* 16px */
--space-5: 1.25rem;     /* 20px */
--space-6: 1.5rem;      /* 24px */
--space-8: 2rem;        /* 32px */
--space-10: 2.5rem;     /* 40px */
--space-12: 3rem;       /* 48px */
--space-16: 4rem;       /* 64px */
--space-20: 5rem;       /* 80px */
--space-24: 6rem;       /* 96px */
```

### Layout Guidelines

#### Container Widths

```css
/* Maximum content width */
.container-sm { max-width: 640px; }   /* Small - forms, modals */
.container-md { max-width: 768px; }   /* Medium - single column content */
.container-lg { max-width: 1024px; }  /* Large - two column layouts */
.container-xl { max-width: 1280px; }  /* Extra large - dashboards */
.container-2xl { max-width: 1536px; } /* Maximum - wide dashboards */
```

#### Grid System

**12-Column Grid** (using CSS Grid or Flexbox):

```css
.grid-12 {
  display: grid;
  grid-template-columns: repeat(12, 1fr);
  gap: var(--space-6); /* 24px default gap */
}

/* Common column spans */
.col-span-1 { grid-column: span 1; }
.col-span-2 { grid-column: span 2; }
.col-span-3 { grid-column: span 3; }
.col-span-4 { grid-column: span 4; }
.col-span-6 { grid-column: span 6; }
.col-span-8 { grid-column: span 8; }
.col-span-12 { grid-column: span 12; }
```

#### Responsive Breakpoints

```css
/* Mobile-first approach */
--breakpoint-sm: 640px;   /* Small devices */
--breakpoint-md: 768px;   /* Tablets */
--breakpoint-lg: 1024px;  /* Laptops */
--breakpoint-xl: 1280px;  /* Desktops */
--breakpoint-2xl: 1536px; /* Large desktops */
```

**Media Query Usage:**

```css
/* Default: Mobile styles */

/* Tablet and up */
@media (min-width: 768px) { }

/* Desktop and up */
@media (min-width: 1024px) { }
```

---

## 4. Component Guidelines

### Buttons

#### Primary Button (Orange Accent)

**Usage:** Main CTAs, primary actions (Save, Submit, Create, etc.)

```html
<button class="btn btn-primary">
  Create Server
</button>
```

```css
.btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 0.5rem;
  padding: 0.625rem 1.25rem;    /* 10px 20px */
  font-size: 0.875rem;          /* 14px */
  font-weight: 600;
  line-height: 1.5;
  border-radius: 0.375rem;      /* 6px */
  border: 1px solid transparent;
  cursor: pointer;
  transition: all 0.15s ease-in-out;
  white-space: nowrap;
}

.btn-primary {
  background-color: #cb4e1b;
  color: #ffffff;
  border-color: #cb4e1b;
}

.btn-primary:hover {
  background-color: #e5591f;
  border-color: #e5591f;
}

.btn-primary:active {
  background-color: #b04517;
  border-color: #b04517;
}

.btn-primary:focus-visible {
  outline: 2px solid #098ecf;
  outline-offset: 2px;
}

.btn-primary:disabled {
  background-color: #7a7876;
  border-color: #7a7876;
  cursor: not-allowed;
  opacity: 0.5;
}
```

#### Secondary Button (Outline)

**Usage:** Secondary actions, cancel buttons

```html
<button class="btn btn-secondary">
  Cancel
</button>
```

```css
.btn-secondary {
  background-color: transparent;
  color: #d7d3d0;
  border-color: #3f4447;
}

.btn-secondary:hover {
  background-color: #363a3e;
  border-color: #3f4447;
}

.btn-secondary:active {
  background-color: #2f3336;
}

.btn-secondary:focus-visible {
  outline: 2px solid #098ecf;
  outline-offset: 2px;
}
```

#### Accent Button (Blue)

**Usage:** Informational actions, links, secondary CTAs

```html
<button class="btn btn-accent">
  View Details
</button>
```

```css
.btn-accent {
  background-color: #098ecf;
  color: #ffffff;
  border-color: #098ecf;
}

.btn-accent:hover {
  background-color: #0ba3ea;
  border-color: #0ba3ea;
}

.btn-accent:active {
  background-color: #0879b3;
  border-color: #0879b3;
}
```

#### Danger Button

**Usage:** Destructive actions (Delete, Remove, Ban, etc.)

```html
<button class="btn btn-danger">
  Delete Server
</button>
```

```css
.btn-danger {
  background-color: #ef4444;
  color: #ffffff;
  border-color: #ef4444;
}

.btn-danger:hover {
  background-color: #dc2626;
  border-color: #dc2626;
}

.btn-danger:active {
  background-color: #b91c1c;
  border-color: #b91c1c;
}
```

#### Button Sizes

```css
/* Small button */
.btn-sm {
  padding: 0.375rem 0.75rem;   /* 6px 12px */
  font-size: 0.75rem;          /* 12px */
}

/* Large button */
.btn-lg {
  padding: 0.75rem 1.5rem;     /* 12px 24px */
  font-size: 1rem;             /* 16px */
}

/* Icon-only button */
.btn-icon {
  padding: 0.625rem;           /* 10px - square */
}
```

#### Button with Icon

```html
<!-- Icon left -->
<button class="btn btn-primary">
  <svg class="w-4 h-4"><!-- icon --></svg>
  <span>Add Member</span>
</button>

<!-- Icon right -->
<button class="btn btn-secondary">
  <span>Export Data</span>
  <svg class="w-4 h-4"><!-- icon --></svg>
</button>

<!-- Icon only -->
<button class="btn btn-secondary btn-icon" aria-label="Settings">
  <svg class="w-5 h-5"><!-- icon --></svg>
</button>
```

---

### Cards and Panels

**Usage:** Content containers, data grouping, dashboard widgets

```html
<div class="card">
  <div class="card-header">
    <h3 class="card-title">Server Statistics</h3>
    <button class="btn btn-sm btn-secondary">Refresh</button>
  </div>
  <div class="card-body">
    <!-- Card content -->
  </div>
  <div class="card-footer">
    <span class="text-xs text-tertiary">Last updated: 2 minutes ago</span>
  </div>
</div>
```

```css
.card {
  background-color: #262a2d;
  border: 1px solid #3f4447;
  border-radius: 0.5rem;      /* 8px */
  overflow: hidden;
}

.card-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 1rem 1.5rem;       /* 16px 24px */
  border-bottom: 1px solid #3f4447;
}

.card-title {
  font-size: 1.125rem;        /* 18px */
  font-weight: 600;
  color: #d7d3d0;
}

.card-body {
  padding: 1.5rem;            /* 24px */
}

.card-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 1rem 1.5rem;
  border-top: 1px solid #3f4447;
  background-color: #1d2022;
}
```

#### Card Variants

```css
/* Elevated card - for modals, popovers */
.card-elevated {
  background-color: #2f3336;
  box-shadow:
    0 10px 15px -3px rgba(0, 0, 0, 0.3),
    0 4px 6px -2px rgba(0, 0, 0, 0.2);
}

/* Interactive card - hover effect */
.card-interactive {
  cursor: pointer;
  transition: all 0.2s ease-in-out;
}

.card-interactive:hover {
  border-color: #098ecf;
  transform: translateY(-2px);
  box-shadow: 0 4px 12px rgba(9, 142, 207, 0.15);
}
```

---

### Form Inputs

#### Text Input

```html
<div class="form-group">
  <label for="server-name" class="form-label">Server Name</label>
  <input
    type="text"
    id="server-name"
    class="form-input"
    placeholder="Enter server name"
  />
  <span class="form-help">This will be displayed to all members</span>
</div>
```

```css
.form-group {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;              /* 8px */
  margin-bottom: 1rem;      /* 16px */
}

.form-label {
  font-size: 0.875rem;      /* 14px */
  font-weight: 600;
  color: #d7d3d0;
}

.form-input {
  width: 100%;
  padding: 0.625rem 0.875rem;  /* 10px 14px */
  font-size: 0.875rem;          /* 14px */
  color: #d7d3d0;
  background-color: #1d2022;
  border: 1px solid #3f4447;
  border-radius: 0.375rem;      /* 6px */
  transition: all 0.15s ease-in-out;
}

.form-input::placeholder {
  color: #7a7876;
}

.form-input:hover {
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

.form-help {
  font-size: 0.75rem;       /* 12px */
  color: #a8a5a3;
}
```

#### Input States

```css
/* Error state */
.form-input.error {
  border-color: #ef4444;
}

.form-input.error:focus {
  box-shadow: 0 0 0 3px rgba(239, 68, 68, 0.15);
}

.form-error {
  display: flex;
  align-items: center;
  gap: 0.375rem;
  font-size: 0.75rem;
  color: #ef4444;
}

/* Success state */
.form-input.success {
  border-color: #10b981;
}
```

#### Floating Label Pattern

The floating label pattern provides a modern, space-efficient form design where labels animate from placeholder position to above the input when focused or filled. This pattern is used on the login page and can be applied to other authentication or focused form experiences.

**Use Cases:**
- Login and registration forms
- Focused single-purpose forms (e.g., search, contact)
- Forms where vertical space is limited
- Modern, minimal form aesthetics

**HTML Structure:**

```html
<div class="form-group-floating relative mb-6">
  <input
    type="email"
    id="email"
    class="form-input-floating w-full pt-5 pb-2 px-4 text-[0.9375rem] text-text-primary bg-bg-primary border border-border-primary rounded-lg shadow-[inset_0_1px_2px_rgba(0,0,0,0.1)] transition-all duration-200 outline-none appearance-none placeholder:text-transparent hover:border-accent-blue focus:border-accent-blue focus:bg-bg-secondary focus:shadow-[inset_0_1px_2px_rgba(0,0,0,0.1),0_0_0_3px_rgba(9,142,207,0.15)]"
    placeholder=" "
    autocomplete="email"
    required
    aria-required="true"
    aria-describedby="email-error"
  />
  <label for="email" class="form-label-floating absolute top-4 left-4 text-[0.9375rem] font-medium text-text-tertiary bg-transparent pointer-events-none transition-all duration-200 origin-left">
    Email address
  </label>
  <span id="email-error" class="form-error block mt-2 text-xs text-error opacity-0 -translate-y-1 transition-all duration-200" role="alert"></span>
</div>
```

**CSS (defined in site.css):**

```css
/* Floating Label Pattern - Form inputs with floating labels */
.form-input-floating:focus + .form-label-floating,
.form-input-floating:not(:placeholder-shown) + .form-label-floating {
  transform: translateY(-0.625rem) scale(0.85);
  color: theme('colors.accent.blue');
  font-weight: 600;
}

/* Floating label error state */
.form-input-floating.error {
  border-color: theme('colors.error');
  background-color: rgba(239, 68, 68, 0.05);
}

.form-input-floating.error:focus {
  box-shadow: inset 0 1px 2px rgba(0, 0, 0, 0.1), 0 0 0 3px rgba(239, 68, 68, 0.15);
}

.form-input-floating.error + .form-label-floating {
  color: theme('colors.error');
}

.form-input-floating.error ~ .form-error {
  opacity: 1;
  transform: translateY(0);
}

/* Floating label success state */
.form-input-floating.success {
  border-color: theme('colors.success');
}
```

**Key Implementation Details:**

1. **Placeholder trick:** The `placeholder=" "` (single space) is required for the `:placeholder-shown` selector to work correctly
2. **Label positioning:** Labels start at `top-4 left-4` and transform upward when input is focused or has content
3. **Padding asymmetry:** Input uses `pt-5 pb-2` to create space for the floating label
4. **Accessibility:** Always include proper `aria-*` attributes and `<label>` elements linked via `for`/`id`

**States:**

| State | Visual Change |
|-------|---------------|
| Default | Label appears as placeholder text inside input |
| Hover | Border color changes to accent-blue |
| Focus | Label floats up, turns blue, input background changes |
| Filled | Label stays floating, returns to blue color |
| Error | Border/label turn red, error message fades in |
| Success | Border turns green |

**When NOT to Use:**

- Long forms with many fields (use standard labels for better scannability)
- Forms requiring help text visible before focus
- Complex form layouts with grouped fields

.form-success {
  display: flex;
  align-items: center;
  gap: 0.375rem;
  font-size: 0.75rem;
  color: #10b981;
}
```

#### Select Dropdown

```html
<div class="form-group">
  <label for="bot-role" class="form-label">Bot Role</label>
  <select id="bot-role" class="form-select">
    <option value="">Select a role</option>
    <option value="admin">Administrator</option>
    <option value="mod">Moderator</option>
    <option value="member">Member</option>
  </select>
</div>
```

```css
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
}

.form-select:hover {
  border-color: #098ecf;
}

.form-select:focus {
  outline: none;
  border-color: #098ecf;
  box-shadow: 0 0 0 3px rgba(9, 142, 207, 0.15);
}
```

#### Checkbox and Radio

```html
<!-- Checkbox -->
<label class="form-checkbox">
  <input type="checkbox" />
  <span class="form-checkbox-label">Enable auto-moderation</span>
</label>

<!-- Radio -->
<label class="form-radio">
  <input type="radio" name="visibility" value="public" />
  <span class="form-radio-label">Public</span>
</label>
```

```css
.form-checkbox,
.form-radio {
  display: flex;
  align-items: center;
  gap: 0.625rem;
  cursor: pointer;
}

.form-checkbox input[type="checkbox"],
.form-radio input[type="radio"] {
  width: 1.125rem;
  height: 1.125rem;
  color: #cb4e1b;
  background-color: #1d2022;
  border: 1px solid #3f4447;
  border-radius: 0.25rem;      /* Checkbox */
  cursor: pointer;
  transition: all 0.15s ease-in-out;
}

.form-radio input[type="radio"] {
  border-radius: 50%;          /* Radio */
}

.form-checkbox input[type="checkbox"]:checked,
.form-radio input[type="radio"]:checked {
  background-color: #cb4e1b;
  border-color: #cb4e1b;
}

.form-checkbox input[type="checkbox"]:focus,
.form-radio input[type="radio"]:focus {
  outline: 2px solid #098ecf;
  outline-offset: 2px;
}

.form-checkbox-label,
.form-radio-label {
  font-size: 0.875rem;
  color: #d7d3d0;
}
```

#### Toggle Switch

```html
<label class="toggle">
  <input type="checkbox" class="toggle-input" />
  <span class="toggle-slider"></span>
  <span class="toggle-label">Enable notifications</span>
</label>
```

```css
.toggle {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  cursor: pointer;
}

.toggle-input {
  position: absolute;
  opacity: 0;
  width: 0;
  height: 0;
}

.toggle-slider {
  position: relative;
  display: inline-block;
  width: 2.75rem;           /* 44px */
  height: 1.5rem;           /* 24px */
  background-color: #3f4447;
  border-radius: 9999px;
  transition: background-color 0.2s ease-in-out;
}

.toggle-slider::before {
  content: "";
  position: absolute;
  top: 0.125rem;            /* 2px */
  left: 0.125rem;
  width: 1.25rem;           /* 20px */
  height: 1.25rem;
  background-color: #d7d3d0;
  border-radius: 50%;
  transition: transform 0.2s ease-in-out;
}

.toggle-input:checked + .toggle-slider {
  background-color: #cb4e1b;
}

.toggle-input:checked + .toggle-slider::before {
  transform: translateX(1.25rem);  /* 20px */
}

.toggle-input:focus-visible + .toggle-slider {
  outline: 2px solid #098ecf;
  outline-offset: 2px;
}

.toggle-label {
  font-size: 0.875rem;
  color: #d7d3d0;
}
```

#### Timezone-Aware Inputs

**Feature Reference:** [Timezone Handling Documentation](timezone-handling.md)

Timezone-aware inputs automatically handle the conversion between the user's local timezone and UTC storage. The system uses JavaScript for browser timezone detection and C# `TimezoneHelper` for server-side conversion.

**Pattern Components:**

1. `datetime-local` input for user's local time
2. Hidden timezone field (auto-populated by JavaScript)
3. Timezone indicator showing user's timezone
4. `data-utc` attributes for displaying stored UTC timestamps

**Usage Example:**

```html
<div class="form-group">
  <label for="scheduled-time" class="form-label">Schedule For</label>

  <!-- datetime-local input shows local time -->
  <input
    type="datetime-local"
    id="scheduled-time"
    name="Input.ScheduledAt"
    class="form-input"
  />

  <!-- Hidden timezone field (auto-populated) -->
  <input
    type="hidden"
    name="Input.UserTimezone"
  />

  <!-- Timezone indicator (auto-populated) -->
  <span class="form-help timezone-indicator"></span>
</div>

<!-- Load timezone utilities -->
<script src="~/js/timezone.js"></script>
<script>
  // Optional: Set default time to 5 minutes from now
  timezoneUtils.setDefaultDateTime('scheduled-time', 5);
</script>
```

**CSS Styling:**

```css
.timezone-indicator {
  display: inline-flex;
  align-items: center;
  gap: 0.375rem;
  font-size: 0.75rem;
  color: #a8a5a3;
  margin-top: 0.25rem;
}

.timezone-indicator::before {
  content: "🌍";
  font-size: 0.875rem;
}
```

**Displaying UTC Timestamps:**

Use the `data-utc` attribute pattern to automatically convert stored UTC timestamps to the user's local timezone:

```html
<!-- Full datetime display -->
<span data-utc="2025-12-27T15:30:00Z">
  Dec 27, 2025, 10:30 AM
</span>

<!-- Date only -->
<span data-utc="2025-12-27T15:30:00Z" data-format="date">
  Dec 27, 2025
</span>

<!-- Time only -->
<span data-utc="2025-12-27T15:30:00Z" data-format="time">
  10:30 AM
</span>
```

**Format Options (via `data-format` attribute):**

| Format | Output Example |
|--------|----------------|
| `datetime` (default) | "Dec 27, 2025, 10:30 AM" |
| `date` | "Dec 27, 2025" |
| `time` | "10:30 AM" |

**Behavior:**

- JavaScript automatically detects user's timezone on page load
- Hidden `UserTimezone` field populated with IANA timezone (e.g., "America/New_York")
- Timezone indicator shows timezone name and abbreviation (e.g., "America/New_York (EST)")
- `[data-utc]` elements converted to local time automatically
- Server receives local datetime + timezone, converts to UTC for storage

**Pre-filling Edit Forms:**

```html
<input type="datetime-local" id="scheduled-time" />

<script>
  // Set input from UTC timestamp (auto-converts to local)
  var utcTime = '@Model.ScheduledAt.ToString("o")';
  timezoneUtils.setDateTimeLocalFromUtc('scheduled-time', utcTime);
</script>
```

**Accessibility:**

- Timezone indicator provides context for screen readers
- `datetime-local` input follows standard form accessibility guidelines
- Always include visible timezone information so users understand what timezone they're working in

**Implementation Notes:**

- All timestamps stored in database as UTC (server timezone independent)
- Server-side conversion uses `TimezoneHelper.ConvertToUtc()` and `TimezoneHelper.ConvertFromUtc()`
- JavaScript module handles all client-side detection and conversion
- Supports Daylight Saving Time (DST) transitions automatically
- Falls back to UTC if timezone detection fails

**See Also:**
- [Timezone Handling Documentation](timezone-handling.md) - Complete implementation guide
- [JavaScript timezone.js API](timezone-handling.md#javascript-module-timezonejs) - Client-side utilities
- [TimezoneHelper C# API](timezone-handling.md#timezonehelper-utility) - Server-side conversion

---

### Navigation Elements

#### Top Navigation Bar

```html
<nav class="navbar">
  <div class="navbar-brand">
    <img src="/logo.svg" alt="Logo" class="navbar-logo" />
    <span class="navbar-title">Bot Admin</span>
  </div>
  <div class="navbar-menu">
    <a href="#" class="navbar-link active">Dashboard</a>
    <a href="#" class="navbar-link">Servers</a>
    <a href="#" class="navbar-link">Members</a>
    <a href="#" class="navbar-link">Settings</a>
  </div>
  <div class="navbar-actions">
    <button class="btn btn-icon btn-secondary" aria-label="Notifications">
      <svg class="w-5 h-5"><!-- bell icon --></svg>
    </button>
    <button class="btn btn-icon btn-secondary" aria-label="User menu">
      <svg class="w-5 h-5"><!-- user icon --></svg>
    </button>
  </div>
</nav>
```

```css
.navbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 1rem 1.5rem;
  background-color: #262a2d;
  border-bottom: 1px solid #3f4447;
}

.navbar-brand {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.navbar-logo {
  width: 2rem;
  height: 2rem;
}

.navbar-title {
  font-size: 1.25rem;
  font-weight: 700;
  color: #d7d3d0;
}

.navbar-menu {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.navbar-link {
  padding: 0.5rem 1rem;
  font-size: 0.875rem;
  font-weight: 500;
  color: #a8a5a3;
  text-decoration: none;
  border-radius: 0.375rem;
  transition: all 0.15s ease-in-out;
}

.navbar-link:hover {
  color: #d7d3d0;
  background-color: #363a3e;
}

.navbar-link.active {
  color: #cb4e1b;
  background-color: rgba(203, 78, 27, 0.1);
}

.navbar-actions {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}
```

#### Sidebar Navigation

```html
<aside class="sidebar">
  <nav class="sidebar-nav">
    <a href="#" class="sidebar-link active">
      <svg class="sidebar-icon"><!-- dashboard icon --></svg>
      <span>Dashboard</span>
    </a>
    <a href="#" class="sidebar-link">
      <svg class="sidebar-icon"><!-- server icon --></svg>
      <span>Servers</span>
      <span class="sidebar-badge">12</span>
    </a>
    <a href="#" class="sidebar-link">
      <svg class="sidebar-icon"><!-- users icon --></svg>
      <span>Members</span>
    </a>
  </nav>
</aside>
```

```css
.sidebar {
  width: 16rem;              /* 256px */
  min-height: 100vh;
  background-color: #262a2d;
  border-right: 1px solid #3f4447;
  padding: 1.5rem 1rem;
}

.sidebar-nav {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.sidebar-link {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.75rem 1rem;
  font-size: 0.875rem;
  font-weight: 500;
  color: #a8a5a3;
  text-decoration: none;
  border-radius: 0.5rem;
  transition: all 0.15s ease-in-out;
}

.sidebar-link:hover {
  color: #d7d3d0;
  background-color: #363a3e;
}

.sidebar-link.active {
  color: #d7d3d0;
  background-color: rgba(203, 78, 27, 0.15);
  border-left: 3px solid #cb4e1b;
}

.sidebar-icon {
  width: 1.25rem;
  height: 1.25rem;
  flex-shrink: 0;
}

.sidebar-badge {
  margin-left: auto;
  padding: 0.125rem 0.5rem;
  font-size: 0.75rem;
  font-weight: 600;
  color: #d7d3d0;
  background-color: #3f4447;
  border-radius: 9999px;
}
```

#### Breadcrumbs

```html
<nav aria-label="Breadcrumb" class="breadcrumb">
  <ol class="breadcrumb-list">
    <li class="breadcrumb-item">
      <a href="#" class="breadcrumb-link">Dashboard</a>
    </li>
    <li class="breadcrumb-separator">/</li>
    <li class="breadcrumb-item">
      <a href="#" class="breadcrumb-link">Servers</a>
    </li>
    <li class="breadcrumb-separator">/</li>
    <li class="breadcrumb-item breadcrumb-current" aria-current="page">
      Server Settings
    </li>
  </ol>
</nav>
```

```css
.breadcrumb {
  padding: 1rem 0;
}

.breadcrumb-list {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  list-style: none;
  margin: 0;
  padding: 0;
}

.breadcrumb-item {
  font-size: 0.875rem;
}

.breadcrumb-link {
  color: #a8a5a3;
  text-decoration: none;
  transition: color 0.15s ease-in-out;
}

.breadcrumb-link:hover {
  color: #098ecf;
}

.breadcrumb-current {
  color: #d7d3d0;
  font-weight: 500;
}

.breadcrumb-separator {
  color: #7a7876;
  font-size: 0.875rem;
}
```

---

### Tables for Data Display

```html
<div class="table-container">
  <table class="table">
    <thead class="table-header">
      <tr>
        <th class="table-cell-header">Member</th>
        <th class="table-cell-header">Role</th>
        <th class="table-cell-header">Joined</th>
        <th class="table-cell-header">Status</th>
        <th class="table-cell-header">Actions</th>
      </tr>
    </thead>
    <tbody class="table-body">
      <tr class="table-row">
        <td class="table-cell">
          <div class="flex items-center gap-3">
            <img src="/avatar.png" alt="" class="avatar" />
            <div>
              <div class="font-medium text-primary">John Doe</div>
              <div class="text-xs text-secondary">#1234</div>
            </div>
          </div>
        </td>
        <td class="table-cell">
          <span class="badge badge-orange">Admin</span>
        </td>
        <td class="table-cell text-secondary">2025-01-15</td>
        <td class="table-cell">
          <span class="status-indicator status-online">Online</span>
        </td>
        <td class="table-cell">
          <button class="btn btn-sm btn-secondary">Edit</button>
        </td>
      </tr>
    </tbody>
  </table>
</div>
```

```css
.table-container {
  width: 100%;
  overflow-x: auto;
  border: 1px solid #3f4447;
  border-radius: 0.5rem;
}

.table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.875rem;
}

.table-header {
  background-color: #262a2d;
  border-bottom: 1px solid #3f4447;
}

.table-cell-header {
  padding: 0.75rem 1rem;
  text-align: left;
  font-size: 0.75rem;
  font-weight: 600;
  color: #a8a5a3;
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.table-body {
  background-color: #1d2022;
}

.table-row {
  border-bottom: 1px solid #3f4447;
  transition: background-color 0.15s ease-in-out;
}

.table-row:hover {
  background-color: #262a2d;
}

.table-row:last-child {
  border-bottom: none;
}

.table-cell {
  padding: 1rem;
  color: #d7d3d0;
  vertical-align: middle;
}

.avatar {
  width: 2.5rem;
  height: 2.5rem;
  border-radius: 50%;
  object-fit: cover;
}
```

#### Table Variants

```css
/* Striped rows */
.table-striped .table-row:nth-child(even) {
  background-color: #262a2d;
}

/* Bordered table */
.table-bordered {
  border: 1px solid #3f4447;
}

.table-bordered .table-cell {
  border-right: 1px solid #3f4447;
}

.table-bordered .table-cell:last-child {
  border-right: none;
}

/* Compact table */
.table-compact .table-cell,
.table-compact .table-cell-header {
  padding: 0.5rem 0.75rem;
}
```

---

### Status Indicators & Badges

#### Badges

```html
<span class="badge badge-orange">Admin</span>
<span class="badge badge-blue">Moderator</span>
<span class="badge badge-gray">Member</span>
<span class="badge badge-success">Active</span>
<span class="badge badge-warning">Pending</span>
<span class="badge badge-error">Banned</span>
```

```css
.badge {
  display: inline-flex;
  align-items: center;
  padding: 0.25rem 0.75rem;
  font-size: 0.75rem;
  font-weight: 600;
  line-height: 1;
  border-radius: 9999px;
  white-space: nowrap;
}

.badge-orange {
  color: #ffffff;
  background-color: #cb4e1b;
}

.badge-blue {
  color: #ffffff;
  background-color: #098ecf;
}

.badge-gray {
  color: #d7d3d0;
  background-color: #3f4447;
}

.badge-success {
  color: #ffffff;
  background-color: #10b981;
}

.badge-warning {
  color: #1d2022;
  background-color: #f59e0b;
}

.badge-error {
  color: #ffffff;
  background-color: #ef4444;
}
```

#### Status Indicators

```html
<span class="status-indicator status-online">Online</span>
<span class="status-indicator status-idle">Idle</span>
<span class="status-indicator status-busy">Do Not Disturb</span>
<span class="status-indicator status-offline">Offline</span>
```

```css
.status-indicator {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.875rem;
  font-weight: 500;
}

.status-indicator::before {
  content: "";
  width: 0.5rem;
  height: 0.5rem;
  border-radius: 50%;
}

.status-online {
  color: #10b981;
}
.status-online::before {
  background-color: #10b981;
}

.status-idle {
  color: #f59e0b;
}
.status-idle::before {
  background-color: #f59e0b;
}

.status-busy {
  color: #ef4444;
}
.status-busy::before {
  background-color: #ef4444;
}

.status-offline {
  color: #7a7876;
}
.status-offline::before {
  background-color: #7a7876;
}
```

#### Alert/Notification Banners

```html
<div class="alert alert-info">
  <svg class="alert-icon"><!-- info icon --></svg>
  <div class="alert-content">
    <p class="alert-title">Information</p>
    <p class="alert-message">Your changes have been saved successfully.</p>
  </div>
  <button class="alert-close" aria-label="Dismiss">×</button>
</div>
```

```css
.alert {
  display: flex;
  align-items: flex-start;
  gap: 0.75rem;
  padding: 1rem;
  border-radius: 0.5rem;
  border: 1px solid;
}

.alert-icon {
  width: 1.25rem;
  height: 1.25rem;
  flex-shrink: 0;
  margin-top: 0.125rem;
}

.alert-content {
  flex: 1;
}

.alert-title {
  font-size: 0.875rem;
  font-weight: 600;
  margin-bottom: 0.25rem;
}

.alert-message {
  font-size: 0.875rem;
}

.alert-close {
  background: none;
  border: none;
  font-size: 1.5rem;
  line-height: 1;
  cursor: pointer;
  opacity: 0.7;
  transition: opacity 0.15s ease-in-out;
}

.alert-close:hover {
  opacity: 1;
}

/* Alert variants */
.alert-info {
  background-color: rgba(6, 182, 212, 0.1);
  border-color: rgba(6, 182, 212, 0.3);
  color: #06b6d4;
}

.alert-success {
  background-color: rgba(16, 185, 129, 0.1);
  border-color: rgba(16, 185, 129, 0.3);
  color: #10b981;
}

.alert-warning {
  background-color: rgba(245, 158, 11, 0.1);
  border-color: rgba(245, 158, 11, 0.3);
  color: #f59e0b;
}

.alert-error {
  background-color: rgba(239, 68, 68, 0.1);
  border-color: rgba(239, 68, 68, 0.3);
  color: #ef4444;
}
```

---

## 5. Loading States

Loading states provide visual feedback during asynchronous operations, enhancing user experience by indicating progress and preventing confusion during wait times. The system includes spinners, page overlays, skeleton loaders, and button loading states.

### Spinner Components

**Component:** `_LoadingSpinner.cshtml`

Three spinner variants are available for different loading contexts:

#### Spinner Variants

```csharp
// Simple Spinner - Rotating circle (default)
SpinnerVariant.Simple

// Dots Spinner - Three bouncing dots
SpinnerVariant.Dots

// Pulse Spinner - Expanding circle with pulse effect
SpinnerVariant.Pulse
```

#### Spinner Sizes

```csharp
// Small - 24px (w-6 h-6)
SpinnerSize.Small

// Medium - 40px (w-10 h-10) [default]
SpinnerSize.Medium

// Large - 64px (w-16 h-16)
SpinnerSize.Large
```

#### Spinner Colors

```csharp
// Blue - Primary loading color
SpinnerColor.Blue

// Orange - Accent loading color
SpinnerColor.Orange

// White - For dark overlays
SpinnerColor.White
```

#### Usage Example

```html
@{
    var spinnerModel = new LoadingSpinnerViewModel
    {
        Variant = SpinnerVariant.Simple,
        Size = SpinnerSize.Medium,
        Color = SpinnerColor.Blue,
        Message = "Loading data...",
        SubMessage = "Please wait",
        IsOverlay = false
    };
}

@await Html.PartialAsync("Components/_LoadingSpinner", spinnerModel)
```

**Visual Specifications:**
- **Simple Spinner:** Circular border with animated top segment
- **Dots Spinner:** Three circles with staggered bounce animation (delays: -0.32s, -0.16s, 0s)
- **Pulse Spinner:** Outer ring with ping animation + inner circle with pulse animation

---

### Page Loading Overlay

**Component:** `_PageLoadingOverlay.cshtml`

Full-screen loading overlay that blocks all interaction during critical operations.

#### Specifications

```css
.loading-overlay {
  position: fixed;
  inset: 0;
  z-index: 1100;                           /* Above all content */
  background-color: rgba(29, 32, 34, 0.8); /* Semi-transparent backdrop */
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 1rem;
  opacity: 0;
  visibility: hidden;
  transition: opacity 200ms ease, visibility 200ms ease;
}

.loading-overlay.active {
  opacity: 1;
  visibility: visible;
}
```

#### JavaScript API

```javascript
// Show page loading overlay
LoadingManager.showPageLoading('Processing request...', {
  subMessage: 'This may take a few moments',
  showCancel: true,
  cancelCallback: () => {
    console.log('User cancelled operation');
  },
  timeout: 30000  // Auto-hide after 30 seconds
});

// Hide page loading overlay
LoadingManager.hidePageLoading();
```

#### Features

- **Body Scroll Locking:** Prevents scrolling when overlay is active
- **Optional Cancel Button:** Allow users to cancel long-running operations
- **Timeout Protection:** Auto-hide after configurable timeout (default: 30s)
- **Customizable Messages:** Primary and secondary message support
- **Keyboard Accessible:** Proper ARIA attributes for screen readers

#### Accessibility Attributes

```html
<div class="loading-overlay"
     role="alert"
     aria-live="polite"
     aria-busy="true">
  <!-- Spinner and messages -->
</div>
```

---

### Skeleton Loaders

**Components:** `_Skeleton.cshtml` and `_SkeletonCard.cshtml`

Skeleton loaders provide content placeholders that mimic the layout of the actual content, reducing perceived loading time.

#### CSS Animation

```css
.skeleton {
  background: linear-gradient(90deg, #2f3336 0%, #3f4447 50%, #2f3336 100%);
  background-size: 200% 100%;
  animation: skeleton-pulse 1.5s ease-in-out infinite;
}

@keyframes skeleton-pulse {
  0% { background-position: 200% 0; }
  100% { background-position: -200% 0; }
}

/* Static variant (no animation) */
.skeleton-static {
  background: #2f3336;
  animation: none;
}
```

**Gradient Colors:**
- Start/End: `#2f3336` (bg-tertiary)
- Mid-point: `#3f4447` (border-primary)
- Animation Duration: 1.5s ease-in-out infinite

#### Skeleton Types

```csharp
// Text - Single line text placeholder (w-full h-4)
SkeletonType.Text

// Title - Larger heading placeholder (w-full h-6)
SkeletonType.Title

// Avatar - Circular avatar placeholder (w-10 h-10)
SkeletonType.Avatar

// AvatarSmall - Small avatar (w-8 h-8)
SkeletonType.AvatarSmall

// AvatarLarge - Large avatar (w-16 h-16)
SkeletonType.AvatarLarge

// Button - Button-shaped placeholder (w-24 h-10)
SkeletonType.Button

// Card - Card-shaped placeholder (w-full h-32)
SkeletonType.Card

// Rectangle - Generic rectangle (w-full h-20)
SkeletonType.Rectangle
```

#### Basic Skeleton Usage

```html
@{
    var textSkeleton = new SkeletonViewModel
    {
        Type = SkeletonType.Text,
        Width = "w-3/4",      // Override default width
        Height = null,         // Use default height
        Rounded = true,        // Apply rounded corners
        Animate = true,        // Enable shimmer animation
        CssClass = "mb-2"      // Additional CSS classes
    };
}

@await Html.PartialAsync("Components/_Skeleton", textSkeleton)
```

#### Composite Skeleton Cards

**Component:** `_SkeletonCard.cshtml`

Pre-built skeleton patterns for common card layouts:

```csharp
// Stats Card - Icon + Value + Label
SkeletonCardType.Stats

// Server Card - Avatar + Name + Stats Row
SkeletonCardType.Server

// Activity Feed - Icon + 2 Lines (3 items)
SkeletonCardType.Activity

// Table Row - Avatar + Name + Columns (5 rows)
SkeletonCardType.Table
```

**Example: Stats Card Skeleton**

```html
@{
    var statsSkeletonModel = new SkeletonCardViewModel
    {
        Type = SkeletonCardType.Stats,
        ShowHeader = true,
        CssClass = "mb-4"
    };
}

@await Html.PartialAsync("Components/_SkeletonCard", statsSkeletonModel)
```

**Renders:**
```html
<div class="card mb-4">
  <div class="card-header">
    <div class="skeleton w-32 h-6 rounded"></div>
  </div>
  <div class="card-body">
    <div class="flex items-start gap-4">
      <div class="skeleton w-12 h-12 rounded-lg"></div>
      <div class="flex-1 space-y-3">
        <div class="skeleton w-20 h-8 rounded"></div>
        <div class="skeleton w-24 h-4 rounded"></div>
      </div>
    </div>
  </div>
</div>
```

#### JavaScript API for Skeleton Content Swapping

```javascript
// Container structure with skeleton and content
<div id="userListContainer">
  <div data-skeleton>
    <!-- Skeleton cards -->
  </div>
  <div data-content class="hidden">
    <!-- Actual content (hidden initially) -->
  </div>
</div>

// Show skeleton, hide content
LoadingManager.showSkeleton('userListContainer');

// Hide skeleton, show content (after data loads)
LoadingManager.hideSkeleton('userListContainer');
```

**Requirements:**
- Container must have an `id`
- Skeleton wrapper must have `data-skeleton` attribute
- Content wrapper must have `data-content` attribute

---

### Button Loading States

Buttons can display inline loading indicators with optional custom text.

#### JavaScript API

```javascript
// Set button to loading state
const submitBtn = document.getElementById('submitBtn');
LoadingManager.setButtonLoading(submitBtn, true, 'Saving...');

// Or by button ID
LoadingManager.setButtonLoading('submitBtn', true, 'Saving...');

// Restore button to normal state
LoadingManager.setButtonLoading(submitBtn, false);
```

#### Visual State

**Before Loading:**
```html
<button id="submitBtn" class="btn btn-primary">
  Save Changes
</button>
```

**During Loading:**
```html
<button id="submitBtn" class="btn btn-primary" disabled aria-busy="true">
  <svg class="animate-spin w-4 h-4"><!-- spinner --></svg>
  <span>Saving...</span>
</button>
```

**Features:**
- Button disabled during loading
- Original text restored after loading
- Spinner icon positioned before text
- ARIA attributes for accessibility (`aria-busy`, `aria-disabled`)

#### C# ViewModel Approach

```csharp
// ButtonViewModel supports IsLoading property
var buttonModel = new ButtonViewModel
{
    Text = "Submit Form",
    Type = ButtonType.Primary,
    Size = ButtonSize.Medium,
    IsLoading = Model.IsProcessing,
    LoadingText = "Submitting..."
};
```

---

### Container Loading Overlays

Apply loading overlays to specific containers instead of the entire page.

#### CSS Classes

```css
.loading-container {
  position: relative;  /* Required for absolute overlay positioning */
}

.loading-container-overlay {
  position: absolute;
  inset: 0;
  z-index: 10;
  background-color: rgba(29, 32, 34, 0.8);
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 0.75rem;
  border-radius: inherit;
  opacity: 0;
  visibility: hidden;
  transition: opacity 200ms ease, visibility 200ms ease;
}

.loading-container-overlay.active {
  opacity: 1;
  visibility: visible;
}
```

#### JavaScript API

```javascript
// Show loading overlay on specific container
LoadingManager.showContainerLoading('statsCardContainer', 'Refreshing stats...');

// Hide container loading overlay
LoadingManager.hideContainerLoading('statsCardContainer');
```

**Features:**
- Overlays specific containers, not the whole page
- Automatically disables all interactive elements within container
- Restores disabled state after hiding
- Inherits border-radius from parent container
- Spinner + optional message

#### Usage Example

```html
<div id="statsCardContainer" class="card">
  <div class="card-header">
    <h3>Server Statistics</h3>
    <button onclick="refreshStats()">Refresh</button>
  </div>
  <div class="card-body">
    <!-- Stats content -->
  </div>
</div>

<script>
function refreshStats() {
  LoadingManager.showContainerLoading('statsCardContainer', 'Refreshing...');

  fetch('/api/stats')
    .then(response => response.json())
    .then(data => {
      updateStatsUI(data);
      LoadingManager.hideContainerLoading('statsCardContainer');
    })
    .catch(error => {
      console.error(error);
      LoadingManager.hideContainerLoading('statsCardContainer');
    });
}
</script>
```

---

### Form Submission Helper

**JavaScript API** for automatic form submission handling:

```javascript
LoadingManager.handleFormSubmit('myForm', {
  buttonSelector: '[type="submit"]',
  loadingText: 'Submitting...',
  onSuccess: async (response) => {
    console.log('Form submitted successfully');
    window.location.href = '/success';
  },
  onError: async (error) => {
    console.error('Form submission failed', error);
    alert('Submission failed. Please try again.');
  }
});
```

**Automatic behavior:**
- Submit button shows loading state on submit
- Form data sent via Fetch API
- Success/error callbacks invoked based on response
- Button loading state automatically cleared

---

### Accessibility Features

All loading components follow WCAG 2.1 AA accessibility standards:

#### ARIA Attributes

```html
<!-- Loading overlays -->
<div role="alert" aria-live="polite" aria-busy="true">
  <!-- Spinner and messages -->
</div>

<!-- Loading buttons -->
<button aria-busy="true" aria-disabled="true" disabled>
  <!-- Spinner and text -->
</button>

<!-- Skeleton loaders -->
<div aria-hidden="true" class="skeleton">
  <!-- Decorative placeholder -->
</div>
```

#### Reduced Motion Support

Users with `prefers-reduced-motion` preference see static loading states:

```css
@media (prefers-reduced-motion: reduce) {
  .skeleton {
    animation: none;
    background: #2f3336;
  }
}
```

**Impact:**
- Skeleton shimmer animation disabled
- Spinner animations may continue (essential for indicating loading state)
- Transitions remain for smooth state changes

#### Screen Reader Announcements

- `role="alert"` ensures loading overlays are announced
- `aria-live="polite"` prevents interrupting user's current activity
- `aria-busy="true"` indicates active loading state
- `aria-hidden="true"` hides decorative skeleton elements from screen readers

---

### Loading State Guidelines

#### When to Use Each Loading Type

| Loading Type | Use Case | Example |
|--------------|----------|---------|
| **Page Overlay** | Full-page operations that block all interaction | Login, form submission, critical operations |
| **Container Overlay** | Refreshing specific sections without blocking page | Refreshing stats card, reloading table data |
| **Skeleton Loaders** | Initial page load or navigation | Loading dashboard, user profile, data lists |
| **Button Loading** | Individual button actions | Save, delete, submit actions |
| **Inline Spinner** | Small content areas or inline messages | Loading notification count, updating status |

#### Best Practices

1. **Choose appropriate feedback:**
   - Use skeleton loaders for initial content loads
   - Use overlays for user-initiated actions
   - Provide clear messaging for long operations

2. **Timeout protection:**
   - Always set reasonable timeouts for overlays (default: 30s)
   - Show error messages if operations fail
   - Provide cancel options for long operations

3. **Performance:**
   - Skeleton loaders reduce perceived loading time
   - Show loading states immediately (no delay)
   - Hide loading states promptly when complete

4. **Accessibility:**
   - Always include ARIA attributes
   - Respect reduced motion preferences
   - Provide text alternatives for visual indicators

5. **User experience:**
   - Don't show loading states for operations under 300ms
   - Provide progress indication for operations over 3 seconds
   - Avoid nested or overlapping loading states

---

## 6. Icon Usage

### Recommended Icon Library

**Hero Icons** (https://heroicons.com)

- **License:** MIT (free for commercial use)
- **Styles:** Outline (24x24), Solid (20x20), Mini (16x16)
- **Format:** Optimized SVG
- **CDN:** Available via CDN or NPM package

### Icon Sizing Guidelines

```css
/* Icon size utilities */
.icon-xs { width: 1rem; height: 1rem; }      /* 16px - inline with small text */
.icon-sm { width: 1.25rem; height: 1.25rem; } /* 20px - inline with body text */
.icon-md { width: 1.5rem; height: 1.5rem; }   /* 24px - buttons, navigation */
.icon-lg { width: 2rem; height: 2rem; }       /* 32px - section headers */
.icon-xl { width: 2.5rem; height: 2.5rem; }   /* 40px - feature highlights */
```

### Icon Color Utilities

```css
.icon-primary { color: #d7d3d0; }
.icon-secondary { color: #a8a5a3; }
.icon-tertiary { color: #7a7876; }
.icon-orange { color: #cb4e1b; }
.icon-blue { color: #098ecf; }
.icon-success { color: #10b981; }
.icon-warning { color: #f59e0b; }
.icon-error { color: #ef4444; }
```

### Common Icon Usage

```html
<!-- Button with icon -->
<button class="btn btn-primary">
  <svg class="icon-sm" fill="none" viewBox="0 0 24 24" stroke="currentColor">
    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
          d="M12 4v16m8-8H4" />
  </svg>
  <span>Add Server</span>
</button>

<!-- Navigation with icon -->
<a href="#" class="sidebar-link">
  <svg class="icon-md" fill="none" viewBox="0 0 24 24" stroke="currentColor">
    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
          d="M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6" />
  </svg>
  <span>Dashboard</span>
</a>

<!-- Status indicator with icon -->
<div class="flex items-center gap-2">
  <svg class="icon-sm icon-success" fill="currentColor" viewBox="0 0 20 20">
    <path fill-rule="evenodd"
          d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z"
          clip-rule="evenodd" />
  </svg>
  <span>Connected</span>
</div>
```

### Accessibility Guidelines

- Always include meaningful `aria-label` for icon-only buttons
- Use `aria-hidden="true"` for decorative icons
- Ensure sufficient color contrast for icon colors
- Provide text alternatives for important icons

```html
<!-- Icon-only button (accessible) -->
<button class="btn btn-icon" aria-label="Settings">
  <svg aria-hidden="true" class="icon-md">
    <!-- icon path -->
  </svg>
</button>

<!-- Decorative icon (skip from screen reader) -->
<div>
  <svg aria-hidden="true" class="icon-sm icon-blue">
    <!-- icon path -->
  </svg>
  <span>User Settings</span>
</div>
```

---

## 7. Shadows & Elevation

```css
/* Shadow scale */
--shadow-sm: 0 1px 2px 0 rgba(0, 0, 0, 0.3);
--shadow-md: 0 4px 6px -1px rgba(0, 0, 0, 0.3),
             0 2px 4px -1px rgba(0, 0, 0, 0.2);
--shadow-lg: 0 10px 15px -3px rgba(0, 0, 0, 0.3),
             0 4px 6px -2px rgba(0, 0, 0, 0.2);
--shadow-xl: 0 20px 25px -5px rgba(0, 0, 0, 0.3),
             0 10px 10px -5px rgba(0, 0, 0, 0.2);

/* Glow effects for interactive elements */
--glow-orange: 0 0 20px rgba(203, 78, 27, 0.4);
--glow-blue: 0 0 20px rgba(9, 142, 207, 0.4);
```

**Usage:**
- `shadow-sm`: Subtle elevation (cards)
- `shadow-md`: Moderate elevation (dropdowns)
- `shadow-lg`: High elevation (modals)
- `shadow-xl`: Maximum elevation (notifications)

---

## 8. Border Radius

```css
--radius-sm: 0.25rem;   /* 4px - small elements */
--radius-md: 0.375rem;  /* 6px - buttons, inputs */
--radius-lg: 0.5rem;    /* 8px - cards, panels */
--radius-xl: 0.75rem;   /* 12px - large containers */
--radius-full: 9999px;  /* Fully rounded - badges, pills */
```

---

## 9. Tailwind CSS Configuration

Add this configuration to your `tailwind.config.js` to extend Tailwind with the custom design tokens:

```javascript
/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./Pages/**/*.{razor,cshtml}",
    "./Components/**/*.{razor,cshtml}",
    "./wwwroot/**/*.html",
  ],
  theme: {
    extend: {
      colors: {
        // Background layers
        bg: {
          primary: '#1d2022',
          secondary: '#262a2d',
          tertiary: '#2f3336',
          hover: '#363a3e',
        },
        // Text colors
        text: {
          primary: '#d7d3d0',
          secondary: '#a8a5a3',
          tertiary: '#7a7876',
          inverse: '#1d2022',
        },
        // Brand accent colors
        accent: {
          orange: {
            DEFAULT: '#cb4e1b',
            hover: '#e5591f',
            active: '#b04517',
            muted: '#cb4e1b33',
          },
          blue: {
            DEFAULT: '#098ecf',
            hover: '#0ba3ea',
            active: '#0879b3',
            muted: '#098ecf33',
          },
        },
        // Semantic colors
        success: {
          DEFAULT: '#10b981',
          bg: '#10b98120',
          border: '#10b98150',
        },
        warning: {
          DEFAULT: '#f59e0b',
          bg: '#f59e0b20',
          border: '#f59e0b50',
        },
        error: {
          DEFAULT: '#ef4444',
          bg: '#ef444420',
          border: '#ef444450',
        },
        info: {
          DEFAULT: '#06b6d4',
          bg: '#06b6d420',
          border: '#06b6d450',
        },
        // Border colors
        border: {
          primary: '#3f4447',
          secondary: '#2f3336',
          focus: '#098ecf',
        },
      },
      fontFamily: {
        sans: [
          '-apple-system',
          'BlinkMacSystemFont',
          '"Segoe UI"',
          'Roboto',
          '"Helvetica Neue"',
          'Arial',
          'sans-serif',
          '"Apple Color Emoji"',
          '"Segoe UI Emoji"',
          '"Segoe UI Symbol"',
        ],
        mono: [
          'ui-monospace',
          'SFMono-Regular',
          '"SF Mono"',
          'Menlo',
          'Monaco',
          'Consolas',
          '"Liberation Mono"',
          '"Courier New"',
          'monospace',
        ],
      },
      fontSize: {
        'display': ['3rem', { lineHeight: '1.1', letterSpacing: '-0.02em', fontWeight: '700' }],
        'h1': ['2.25rem', { lineHeight: '1.2', letterSpacing: '-0.01em', fontWeight: '700' }],
        'h2': ['1.875rem', { lineHeight: '1.3', letterSpacing: '-0.01em', fontWeight: '600' }],
        'h3': ['1.5rem', { lineHeight: '1.35', fontWeight: '600' }],
        'h4': ['1.25rem', { lineHeight: '1.4', fontWeight: '600' }],
        'h5': ['1.125rem', { lineHeight: '1.4', fontWeight: '600' }],
        'h6': ['1rem', { lineHeight: '1.5', fontWeight: '600' }],
      },
      spacing: {
        // Additional spacing values (Tailwind already includes 0-96)
        '128': '32rem',
        '144': '36rem',
      },
      boxShadow: {
        'sm': '0 1px 2px 0 rgba(0, 0, 0, 0.3)',
        'DEFAULT': '0 4px 6px -1px rgba(0, 0, 0, 0.3), 0 2px 4px -1px rgba(0, 0, 0, 0.2)',
        'md': '0 4px 6px -1px rgba(0, 0, 0, 0.3), 0 2px 4px -1px rgba(0, 0, 0, 0.2)',
        'lg': '0 10px 15px -3px rgba(0, 0, 0, 0.3), 0 4px 6px -2px rgba(0, 0, 0, 0.2)',
        'xl': '0 20px 25px -5px rgba(0, 0, 0, 0.3), 0 10px 10px -5px rgba(0, 0, 0, 0.2)',
        'glow-orange': '0 0 20px rgba(203, 78, 27, 0.4)',
        'glow-blue': '0 0 20px rgba(9, 142, 207, 0.4)',
      },
      borderRadius: {
        'sm': '0.25rem',
        'DEFAULT': '0.375rem',
        'md': '0.375rem',
        'lg': '0.5rem',
        'xl': '0.75rem',
      },
    },
  },
  plugins: [],
}
```

---

## 10. Accessibility Guidelines

### WCAG 2.1 AA Compliance Checklist

#### Color Contrast
- ✅ Text primary on bg-primary: **10.8:1** (AAA)
- ✅ Text secondary on bg-primary: **5.9:1** (AA)
- ✅ All interactive elements meet **4.5:1** minimum
- ✅ Large text (18px+) meets **3:1** minimum

#### Keyboard Navigation
- All interactive elements must be keyboard accessible (Tab/Shift+Tab)
- Focus indicators must be visible (2px blue outline)
- Skip links for main content navigation
- Logical tab order throughout the interface

#### Focus Management
```css
/* Consistent focus style across all interactive elements */
*:focus-visible {
  outline: 2px solid #098ecf;
  outline-offset: 2px;
}
```

#### Semantic HTML
- Use proper heading hierarchy (h1 → h2 → h3)
- Use `<button>` for actions, `<a>` for navigation
- Use `<nav>`, `<main>`, `<aside>`, `<section>`, `<article>` landmarks
- Include ARIA labels for icon-only buttons
- Use `aria-current` for active navigation items

#### Screen Reader Support
- Meaningful alt text for all images
- `aria-label` for icon-only interactive elements
- `aria-hidden="true"` for decorative elements
- `aria-live` regions for dynamic content updates
- Proper form labels and error messages

---

## 11. Responsive Design Strategy

### Mobile-First Approach

Start with mobile styles, then enhance for larger screens:

```css
/* Mobile: Default styles */
.container {
  padding: 1rem;
}

/* Tablet: 768px and up */
@media (min-width: 768px) {
  .container {
    padding: 1.5rem;
  }
}

/* Desktop: 1024px and up */
@media (min-width: 1024px) {
  .container {
    padding: 2rem;
  }
}
```

### Common Responsive Patterns

#### Responsive Navigation
- **Mobile:** Hamburger menu with slide-out drawer
- **Tablet:** Top navigation bar
- **Desktop:** Sidebar + top bar combination

#### Responsive Tables
- **Mobile:** Stacked cards with key information
- **Tablet:** Horizontal scroll with fixed headers
- **Desktop:** Full table view

#### Responsive Cards
```css
.card-grid {
  display: grid;
  gap: 1.5rem;
  grid-template-columns: 1fr;           /* Mobile: 1 column */
}

@media (min-width: 768px) {
  .card-grid {
    grid-template-columns: repeat(2, 1fr); /* Tablet: 2 columns */
  }
}

@media (min-width: 1024px) {
  .card-grid {
    grid-template-columns: repeat(3, 1fr); /* Desktop: 3 columns */
  }
}
```

---

## 12. Implementation Guidelines

### CSS Organization

```
/wwwroot/css/
├── base/
│   ├── reset.css          # Browser reset/normalize
│   ├── typography.css     # Font definitions, type scale
│   └── variables.css      # CSS custom properties
├── components/
│   ├── buttons.css        # Button styles
│   ├── forms.css          # Form input styles
│   ├── cards.css          # Card and panel styles
│   ├── navigation.css     # Nav, sidebar, breadcrumbs
│   ├── tables.css         # Table styles
│   └── badges.css         # Badges and status indicators
├── utilities/
│   └── helpers.css        # Utility classes
└── main.css               # Main stylesheet (imports all)
```

### Component Development Workflow

1. **Design Review**: Check this design system document
2. **Markup Structure**: Use semantic HTML with accessibility in mind
3. **Base Styles**: Apply core styles from design tokens
4. **Interactive States**: Implement hover, focus, active, disabled states
5. **Responsive Behavior**: Test across breakpoints
6. **Accessibility Audit**: Verify keyboard navigation, screen reader support, color contrast
7. **Documentation**: Update component library with usage examples

---

## 13. Code Examples & Quick Reference

### Basic Page Layout

```html
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>Discord Bot Admin</title>
  <link rel="stylesheet" href="/css/main.css">
</head>
<body class="bg-bg-primary text-text-primary">

  <!-- Top Navigation -->
  <nav class="navbar">
    <!-- Navbar content -->
  </nav>

  <div class="flex">
    <!-- Sidebar Navigation -->
    <aside class="sidebar">
      <!-- Sidebar content -->
    </aside>

    <!-- Main Content -->
    <main class="flex-1 p-6 lg:p-8">
      <!-- Breadcrumbs -->
      <nav class="breadcrumb">
        <!-- Breadcrumb items -->
      </nav>

      <!-- Page Header -->
      <header class="mb-8">
        <h1 class="text-h1">Dashboard</h1>
        <p class="text-base text-secondary mt-2">
          Welcome to your Discord bot admin panel
        </p>
      </header>

      <!-- Content Grid -->
      <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        <!-- Cards -->
      </div>
    </main>
  </div>

</body>
</html>
```

### Form Example

```html
<form class="space-y-6 max-w-md">
  <!-- Text Input -->
  <div class="form-group">
    <label for="server-name" class="form-label">Server Name</label>
    <input
      type="text"
      id="server-name"
      class="form-input"
      placeholder="Enter server name"
      required
    />
  </div>

  <!-- Select -->
  <div class="form-group">
    <label for="region" class="form-label">Server Region</label>
    <select id="region" class="form-select">
      <option value="">Select region</option>
      <option value="us-east">US East</option>
      <option value="eu-west">EU West</option>
    </select>
  </div>

  <!-- Toggle -->
  <label class="toggle">
    <input type="checkbox" class="toggle-input" />
    <span class="toggle-slider"></span>
    <span class="toggle-label">Enable auto-moderation</span>
  </label>

  <!-- Buttons -->
  <div class="flex gap-3">
    <button type="submit" class="btn btn-primary">
      Save Changes
    </button>
    <button type="button" class="btn btn-secondary">
      Cancel
    </button>
  </div>
</form>
```

---

## Changelog

### Version 1.2 (2025-12-27)
- Added Timezone-Aware Inputs subsection to Forms (Section 4)
  - Documented timezone input pattern with hidden field
  - Documented timezone indicator UI pattern
  - Documented `data-utc` attribute pattern for time display
  - Added CSS styling for timezone indicators
  - Linked to comprehensive [Timezone Handling Documentation](timezone-handling.md)
  - Related to Issue #319: Timezone handling implementation

### Version 1.1 (2025-12-23)
- Added comprehensive Loading States section (Section 5)
  - Documented spinner components with variants (Simple, Dots, Pulse)
  - Documented page loading overlay with JavaScript API
  - Documented skeleton loaders with CSS animation details
  - Documented button loading states
  - Documented container loading overlays
  - Added accessibility guidelines for loading states
  - Added loading state usage guidelines and best practices

### Version 1.0 (2025-12-07)
- Initial design system release
- Defined color palette, typography, spacing
- Documented all core components
- Created Tailwind configuration
- Established accessibility guidelines

---

## Resources

- **Hero Icons:** https://heroicons.com
- **Tailwind CSS Docs:** https://tailwindcss.com/docs
- **WCAG 2.1 Guidelines:** https://www.w3.org/WAI/WCAG21/quickref/
- **WebAIM Contrast Checker:** https://webaim.org/resources/contrastchecker/

---

## Support & Maintenance

For questions, updates, or contributions to this design system, please contact the design team or create an issue in the project repository.

**Maintained by:** Design & UI Team
**Last Review:** 2025-12-23
**Next Review:** Quarterly
