# Implementation Plan: Feedback Components Prototype (Issue #39)

**Issue:** #39 - Feedback UI Component Prototypes
**Author:** Systems Architect
**Date:** 2025-12-07
**Status:** Ready for Implementation

---

## 1. Requirement Summary

Build a set of reusable feedback UI component prototypes as self-contained HTML/CSS/JS files. These prototypes will:

- Showcase design system patterns before Blazor implementation
- Demonstrate all component variants, states, and interactions
- Serve as a living specification for the dotnet-specialist during Blazor conversion
- Be viewable directly in a browser without any build process

**Components to Build:**
1. Alert Component (inline banners)
2. Toast Notification Component (stacking notifications)
3. Modal Component (overlay dialogs)
4. Confirmation Dialog Component (destructive action confirmations)
5. Drawer/Sidebar Component (slide-out panels)
6. Tooltip Component (hover hints)
7. Popover Component (click-triggered rich content)
8. Loading Overlay (blocking spinners/progress)
9. Skeleton Loader Components (content placeholders)
10. Empty State Component (no-data displays)

---

## 2. Architectural Considerations

### 2.1 Existing System Components

- **Project Type:** .NET 8 Web API with Discord.NET bot
- **Future Admin UI:** Razor Pages (referenced in CLAUDE.md)
- **Architecture:** Clean architecture (Core/Infrastructure/Bot layers)
- **No existing prototypes or design system files** - this plan establishes the foundation

### 2.2 Technology Stack for Prototypes

| Technology | Version/Source | Purpose |
|------------|----------------|---------|
| Tailwind CSS | CDN (v3.4+) | Utility-first styling |
| Hero Icons | Inline SVG | Iconography |
| Vanilla JavaScript | ES6+ | Interactivity |
| HTML5 | - | Semantic structure |

### 2.3 Design System Token Integration

The prototypes must embed the design system tokens directly in Tailwind config. Based on the task requirements, these are the core tokens:

```javascript
// Tailwind Config Extension
colors: {
  // Primary palette (Discord-inspired dark theme)
  primary: {
    DEFAULT: '#5865F2',  // Discord blurple
    hover: '#4752C4',
    light: '#7289DA',
  },
  surface: {
    dark: '#1e1f22',      // Darkest background
    DEFAULT: '#2b2d31',   // Card/panel background
    light: '#313338',     // Elevated surfaces
    lighter: '#383a40',   // Hover states
  },
  border: {
    DEFAULT: '#3f4147',
    light: '#4e5058',
  },
  text: {
    primary: '#f2f3f5',
    secondary: '#b5bac1',
    muted: '#949ba4',
  },
  // Semantic colors (feedback states)
  info: '#06b6d4',
  success: '#10b981',
  warning: '#f59e0b',
  error: '#ef4444',
}
```

### 2.4 Animation & Timing Standards

| Animation | Duration | Easing |
|-----------|----------|--------|
| Alert slide-in | 300ms | ease-out |
| Toast enter/exit | 200ms | ease-in-out |
| Modal fade | 150ms | ease-out |
| Drawer slide | 300ms | cubic-bezier(0.4, 0, 0.2, 1) |
| Tooltip fade | 150ms | ease-in-out |
| Skeleton pulse | 1500ms | ease-in-out (infinite) |

### 2.5 Z-Index Layer System

```
Layer 0:    Base content
Layer 100:  Sticky headers, floating elements
Layer 500:  Drawers, sidebars
Layer 900:  Tooltips, popovers
Layer 1000: Modal backdrop
Layer 1001: Modal content
Layer 1100: Toast notifications
Layer 1200: Loading overlays
```

### 2.6 Accessibility Requirements

- All interactive components must be keyboard navigable
- Focus trap for modals and drawers
- Escape key closes overlays
- ARIA attributes for screen readers
- Sufficient color contrast (WCAG AA minimum)
- Focus visible indicators

---

## 3. File Structure

### 3.1 Directory Layout

```
docs/
├── design-system.md                    # Design tokens and guidelines (NEW)
├── implementation-plans/
│   └── issue-39-feedback-components.md # This document
└── prototypes/
    ├── _shared/
    │   ├── tailwind-config.js          # Shared Tailwind configuration
    │   ├── icons.js                    # Hero Icons as JS module
    │   └── utilities.js                # Shared JS utilities
    ├── feedback-alerts.html            # Alert components
    ├── feedback-toasts.html            # Toast notifications
    ├── feedback-modals.html            # Modal dialogs
    ├── feedback-confirmation.html      # Confirmation dialogs
    ├── feedback-drawers.html           # Drawer/sidebar components
    ├── feedback-tooltips.html          # Tooltips and popovers
    ├── feedback-loading.html           # Loading overlays and spinners
    ├── feedback-skeletons.html         # Skeleton loaders
    ├── feedback-empty-states.html      # Empty state patterns
    └── feedback-showcase.html          # Combined demo page (all components)
```

### 3.2 Rationale for Separate Files

**Decision:** Create separate prototype files per component category rather than one mega file.

**Reasons:**
1. **Focused development** - Each file can be developed and reviewed independently
2. **Browser performance** - Large HTML files with many JS handlers become unwieldy
3. **Clear ownership** - Easier to assign to specific implementation phases
4. **Reusable showcase** - `feedback-showcase.html` can iframe or link to individual prototypes
5. **Blazor conversion** - Maps 1:1 to Blazor component files

---

## 4. Subagent Task Plan

### 4.1 design-specialist Tasks

**Deliverables:**

1. **`docs/design-system.md`** - Create comprehensive design system documentation
   - Color tokens (primary, surface, semantic)
   - Typography scale
   - Spacing scale
   - Border radius standards
   - Shadow definitions
   - Animation timing functions
   - Component-specific design specs for all 10 feedback components

2. **Component Design Specifications** (within design-system.md)
   - Alert: Variants (info/success/warning/error), anatomy, icon placement
   - Toast: Stacking behavior, position options, progress bar styling
   - Modal: Size variants (sm/md/lg/xl), header/body/footer structure
   - Confirmation: Typed confirmation input styling
   - Drawer: Edge positions, overlay vs push mode visuals
   - Tooltip: Arrow styling, position variants
   - Popover: Rich content layout patterns
   - Loading: Spinner variants, progress bar styling
   - Skeleton: Pulse animation, shape variants
   - Empty State: Icon sizing, layout patterns

3. **Accessibility Guidelines**
   - Focus ring styling
   - Color contrast verification
   - Screen reader text patterns

### 4.2 html-prototyper Tasks

**Deliverables:**

#### Phase 1: Foundation (No Dependencies)

1. **`docs/prototypes/_shared/tailwind-config.js`**
   - Export Tailwind configuration with all design tokens
   - Include custom animation keyframes
   - Define component-specific utilities

2. **`docs/prototypes/_shared/icons.js`**
   - Export Hero Icons as inline SVG strings
   - Icons needed: info, check-circle, exclamation-triangle, x-circle, x-mark, chevron-right, chevron-down, spinner, document, folder, user, plus

3. **`docs/prototypes/_shared/utilities.js`**
   - `createFocusTrap(element)` - Focus trap implementation
   - `animate(element, keyframes, options)` - Animation helper
   - `generateId()` - Unique ID generator
   - `debounce(fn, delay)` - Debounce utility
   - `escapeHandler(callback)` - Escape key listener

#### Phase 2: Simple Components (Foundation Required)

4. **`docs/prototypes/feedback-alerts.html`**
   - Demo: All 4 variants (info, success, warning, error)
   - Demo: With/without icon
   - Demo: With/without dismiss button
   - Demo: Auto-dismiss after configurable timeout
   - Interactive: Toggle variants, trigger auto-dismiss

5. **`docs/prototypes/feedback-skeletons.html`**
   - Demo: Text skeleton (single line, multi-line)
   - Demo: Circle skeleton (avatar sizes)
   - Demo: Rectangle skeleton (card, image)
   - Demo: Table skeleton (rows, columns)
   - Interactive: Toggle animation on/off

6. **`docs/prototypes/feedback-empty-states.html`**
   - Demo: Basic (icon + title + description)
   - Demo: With CTA button
   - Demo: With secondary action
   - Demo: Various icon types (no data, no results, error, first-time)
   - Interactive: Switch between variants

#### Phase 3: Interactive Overlays (Foundation Required)

7. **`docs/prototypes/feedback-tooltips.html`**
   - Demo: All positions (top, right, bottom, left)
   - Demo: With arrow pointer
   - Demo: Configurable delay
   - Interactive: Hover targets for each position

8. **`docs/prototypes/feedback-toasts.html`**
   - Demo: All variants (info, success, warning, error)
   - Demo: Stacking behavior (up to 5 visible)
   - Demo: All positions (top-right, top-left, bottom-right, bottom-left, top-center, bottom-center)
   - Demo: With progress bar (countdown to auto-dismiss)
   - Demo: Manual dismiss
   - Interactive: Buttons to trigger each type, position switcher

9. **`docs/prototypes/feedback-loading.html`**
   - Demo: Full-screen overlay with spinner
   - Demo: Container-scoped overlay
   - Demo: Progress bar (determinate)
   - Demo: Progress bar (indeterminate)
   - Demo: With status text
   - Interactive: Toggle overlays, simulate progress

#### Phase 4: Complex Overlays (Foundation + Utilities Required)

10. **`docs/prototypes/feedback-modals.html`**
    - Demo: Size variants (sm: 400px, md: 500px, lg: 600px, xl: 800px)
    - Demo: With header, body, footer sections
    - Demo: Scrollable body content
    - Demo: Without close button
    - Features: Focus trap, escape close, click-outside close (configurable)
    - Interactive: Open/close buttons, size switcher

11. **`docs/prototypes/feedback-confirmation.html`**
    - Demo: Simple confirm/cancel
    - Demo: Destructive action (red confirm button)
    - Demo: Typed confirmation ("delete" to confirm)
    - Demo: With additional warning text
    - Features: Inherit modal focus trap and keyboard handling
    - Interactive: Trigger different confirmation types

12. **`docs/prototypes/feedback-drawers.html`**
    - Demo: All edge positions (left, right, top, bottom)
    - Demo: Overlay mode (dims background)
    - Demo: Push mode (shifts content)
    - Demo: Various widths (sm: 280px, md: 380px, lg: 480px)
    - Features: Focus trap, escape close, click-outside close
    - Interactive: Open drawers from each edge

13. **`docs/prototypes/feedback-tooltips.html`** (extend with Popover)
    - Demo: Click-triggered popover
    - Demo: Rich content (title, description, actions)
    - Demo: All positions
    - Features: Click-outside close
    - Interactive: Click targets for each variant

#### Phase 5: Showcase Integration

14. **`docs/prototypes/feedback-showcase.html`**
    - Navigation sidebar linking to all prototype files
    - Embedded demos of key components
    - Theme toggle (if applicable)
    - Component status checklist

### 4.3 dotnet-specialist Tasks

**Note:** These tasks come AFTER prototype completion and approval.

1. **Review completed prototypes** for Blazor implementation feasibility
2. **Identify component parameters** needed for each Blazor component
3. **Plan component library structure** in `src/DiscordBot.Bot/Components/Feedback/`
4. **Document any prototype patterns** that need adaptation for Blazor (e.g., JS interop requirements)

### 4.4 docs-writer Tasks

1. **Update `CLAUDE.md`** - Add reference to prototypes directory
2. **Create `docs/prototypes/README.md`** - Usage instructions for viewing prototypes
3. **Component API documentation** (after Blazor implementation) - Document component parameters, events, slots

---

## 5. Timeline / Dependency Map

```
Week 1: Foundation & Design
├── [design-specialist] Create docs/design-system.md (Day 1-2)
├── [html-prototyper] Create _shared/ utilities (Day 2-3) ←── Depends on design-system.md
│
Week 1-2: Simple Components (Parallel after Foundation)
├── [html-prototyper] feedback-alerts.html (Day 3-4)
├── [html-prototyper] feedback-skeletons.html (Day 3-4)
├── [html-prototyper] feedback-empty-states.html (Day 4-5)
│
Week 2: Interactive Overlays
├── [html-prototyper] feedback-tooltips.html (Day 5-6)
├── [html-prototyper] feedback-toasts.html (Day 6-7)
├── [html-prototyper] feedback-loading.html (Day 7-8)
│
Week 2-3: Complex Overlays
├── [html-prototyper] feedback-modals.html (Day 8-9)
├── [html-prototyper] feedback-confirmation.html (Day 9-10) ←── Depends on modals
├── [html-prototyper] feedback-drawers.html (Day 10-11)
│
Week 3: Integration & Documentation
├── [html-prototyper] feedback-showcase.html (Day 11-12)
├── [docs-writer] Update CLAUDE.md, create README (Day 12)
└── [dotnet-specialist] Review and plan Blazor conversion (Day 12-13)
```

### Parallel Execution Opportunities

These can be developed simultaneously after foundation is complete:
- `feedback-alerts.html` + `feedback-skeletons.html` + `feedback-empty-states.html`
- `feedback-tooltips.html` + `feedback-toasts.html` + `feedback-loading.html`

### Sequential Dependencies

1. `design-system.md` must be complete before any prototypes
2. `_shared/` utilities must exist before complex components
3. `feedback-modals.html` must be complete before `feedback-confirmation.html`
4. All individual prototypes before `feedback-showcase.html`

---

## 6. Acceptance Criteria

### 6.1 design-specialist Deliverables

| Deliverable | Acceptance Criteria |
|-------------|---------------------|
| `design-system.md` | Contains all color tokens, typography, spacing, shadows, animations |
| Component Specs | Each of 10 components has documented variants, anatomy, states |
| Accessibility | Focus styles, contrast ratios, ARIA patterns documented |

### 6.2 html-prototyper Deliverables

| Prototype File | Acceptance Criteria |
|----------------|---------------------|
| `_shared/tailwind-config.js` | All design tokens defined, custom animations included |
| `_shared/icons.js` | All required icons exported as SVG strings |
| `_shared/utilities.js` | Focus trap works, animations smooth, no console errors |
| `feedback-alerts.html` | 4 variants visible, dismiss works, auto-dismiss works |
| `feedback-toasts.html` | Stacking works (5+), all positions work, progress bar animates |
| `feedback-modals.html` | All sizes render correctly, focus trap works, ESC closes, click-outside closes |
| `feedback-confirmation.html` | Typed confirmation validates input, buttons disabled until valid |
| `feedback-drawers.html` | All 4 edges work, overlay dims background, push mode shifts content |
| `feedback-tooltips.html` | All positions render correctly, delay configurable, arrow points correctly |
| `feedback-loading.html` | Full-screen blocks interaction, progress bar animates, spinner spins |
| `feedback-skeletons.html` | Pulse animation runs, all shapes render |
| `feedback-empty-states.html` | All variants display correctly, CTA button clickable |
| `feedback-showcase.html` | All components accessible, navigation works |

### 6.3 Global Acceptance Criteria

All prototype files must:
- [ ] Open directly in browser without build step
- [ ] Load Tailwind via CDN successfully
- [ ] Have no JavaScript console errors
- [ ] Be keyboard navigable (Tab, Shift+Tab, Enter, Escape)
- [ ] Include ARIA attributes where appropriate
- [ ] Match design system color tokens exactly
- [ ] Include interactive controls to demonstrate all states/variants
- [ ] Contain inline comments explaining component structure

---

## 7. Risks & Mitigations

### 7.1 Technical Risks

| Risk | Impact | Likelihood | Mitigation |
|------|--------|------------|------------|
| Tailwind CDN unavailable | Prototypes unstyled | Low | Include fallback CDN URLs, document offline viewing |
| Focus trap edge cases | Accessibility failures | Medium | Test with screen readers, use established focus-trap patterns |
| Animation performance | Janky UX on low-end devices | Low | Use CSS transforms, avoid layout-triggering properties |
| Z-index conflicts | Overlapping elements | Medium | Strict z-index layer system documented above |

### 7.2 Process Risks

| Risk | Impact | Likelihood | Mitigation |
|------|--------|------------|------------|
| Design system incomplete | Prototypes inconsistent | Medium | design-specialist completes tokens before html-prototyper starts |
| Prototype divergence from Blazor capability | Rework required | Medium | dotnet-specialist reviews prototypes early for feasibility |
| Scope creep on individual components | Timeline slip | Medium | Strict acceptance criteria, MVP-first approach |

### 7.3 Recommended Safeguards

1. **Design Review Gate** - design-specialist signs off on `design-system.md` before prototype work begins
2. **Prototype Review** - Each prototype file reviewed against acceptance criteria before next begins
3. **Blazor Feasibility Check** - dotnet-specialist reviews modal and drawer prototypes for JS interop complexity
4. **Browser Testing** - Test all prototypes in Chrome, Firefox, Edge before final approval

---

## 8. Implementation Notes

### 8.1 HTML Template Structure

Each prototype should follow this structure:

```html
<!DOCTYPE html>
<html lang="en" class="dark">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>[Component Name] - Discord Bot Admin Prototype</title>
    <script src="https://cdn.tailwindcss.com"></script>
    <script>
        // Inline Tailwind config with design tokens
        tailwind.config = { /* ... */ }
    </script>
    <style type="text/tailwindcss">
        /* Custom component styles */
    </style>
</head>
<body class="bg-surface-dark text-text-primary min-h-screen">
    <!-- Navigation header -->
    <header>...</header>

    <!-- Component demos -->
    <main>
        <section id="variant-1">...</section>
        <section id="variant-2">...</section>
    </main>

    <!-- Interactive controls panel -->
    <aside>...</aside>

    <!-- Scripts -->
    <script>
        // Component JavaScript
    </script>
</body>
</html>
```

### 8.2 JavaScript Patterns

**Event Delegation:**
```javascript
document.addEventListener('click', (e) => {
    if (e.target.matches('[data-dismiss="alert"]')) {
        dismissAlert(e.target.closest('.alert'));
    }
});
```

**Focus Trap Pattern:**
```javascript
function createFocusTrap(container) {
    const focusable = container.querySelectorAll(
        'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])'
    );
    const first = focusable[0];
    const last = focusable[focusable.length - 1];

    container.addEventListener('keydown', (e) => {
        if (e.key !== 'Tab') return;
        if (e.shiftKey && document.activeElement === first) {
            e.preventDefault();
            last.focus();
        } else if (!e.shiftKey && document.activeElement === last) {
            e.preventDefault();
            first.focus();
        }
    });
}
```

### 8.3 CSS Animation Keyframes

```css
@keyframes slideInRight {
    from { transform: translateX(100%); opacity: 0; }
    to { transform: translateX(0); opacity: 1; }
}

@keyframes fadeIn {
    from { opacity: 0; }
    to { opacity: 1; }
}

@keyframes pulse {
    0%, 100% { opacity: 1; }
    50% { opacity: 0.5; }
}

@keyframes spin {
    from { transform: rotate(0deg); }
    to { transform: rotate(360deg); }
}
```

---

## 9. Next Steps

1. **Immediate:** design-specialist begins `docs/design-system.md`
2. **Day 2:** html-prototyper reviews design system, begins `_shared/` files
3. **Day 3:** html-prototyper begins Phase 2 components in parallel
4. **Ongoing:** Regular check-ins to verify prototype quality against acceptance criteria

---

## Appendix A: Alert Component Design Spec

```
+--------------------------------------------------+
| [icon] Alert Title                         [x]   |
| Alert description text goes here.                |
+--------------------------------------------------+

Variants:
- info:    cyan icon/border, dark cyan bg
- success: green icon/border, dark green bg
- warning: amber icon/border, dark amber bg
- error:   red icon/border, dark red bg

States:
- default: full opacity
- dismissing: fade out (200ms)
- auto-dismiss: progress bar at bottom

Spacing:
- Padding: 16px
- Icon size: 20px
- Gap between icon and text: 12px
- Border radius: 8px
- Border width: 1px
```

## Appendix B: Toast Component Design Spec

```
+----------------------------------+
| [icon] Toast message       [x]   |
| [=========>                    ] |  <- progress bar
+----------------------------------+

Positions (from viewport edge):
- top-right, top-left, top-center
- bottom-right, bottom-left, bottom-center

Stacking:
- Max 5 visible
- 8px gap between toasts
- Older toasts pushed down/up
- Overflow toasts queued

Dimensions:
- Min width: 300px
- Max width: 420px
- Right/left offset: 16px
- Top/bottom offset: 16px
```

## Appendix C: Modal Component Design Spec

```
+==========================================+
|  Modal Title                       [x]   |  <- header
+------------------------------------------+
|                                          |
|  Modal body content                      |  <- body (scrollable)
|                                          |
+------------------------------------------+
|                    [Cancel] [Confirm]    |  <- footer
+==========================================+

Size variants:
- sm: max-width 400px
- md: max-width 500px (default)
- lg: max-width 600px
- xl: max-width 800px

Backdrop:
- Color: rgba(0, 0, 0, 0.5)
- Blur: 2px (optional)
- Click to close: configurable

Animation:
- Backdrop fade: 150ms
- Modal scale: 0.95 -> 1.0 (150ms)
```

---

**Document Version:** 1.0
**Last Updated:** 2025-12-07
**Prepared By:** Systems Architect
