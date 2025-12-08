# Issue #41: Extract Common Design Tokens and CSS from Prototypes - Implementation Plan

**Version:** 1.0
**Created:** 2025-12-07
**Status:** Planning
**Target Framework:** HTML/CSS Prototypes + Future .NET Blazor

---

## 1. Requirement Summary

Extract duplicated CSS and Tailwind configuration from 27 HTML prototype files into a shared stylesheet architecture. Currently, each prototype contains:

1. **Tailwind config block** (~65 lines) with identical design tokens
2. **Common CSS styles** in `<style>` tags (scrollbar, focus states, `.sr-only`, etc.)
3. **Component-specific styles** that vary by file type

The goal is to create a centralized CSS architecture that:
- Eliminates code duplication across all prototype files
- Provides a single source of truth for design tokens
- Creates a maintainable structure that mirrors production Blazor CSS
- Maintains browser live preview compatibility (no build step required)

---

## 2. Current State Analysis

### 2.1 Prototype Inventory (27 files)

| Category | Count | Files |
|----------|-------|-------|
| Root prototypes | 2 | `dashboard.html`, `component-showcase.html` |
| Feedback components | 9 | `feedback-alerts.html`, `feedback-confirmation.html`, `feedback-drawers.html`, `feedback-empty-states.html`, `feedback-loading.html`, `feedback-modals.html`, `feedback-skeletons.html`, `feedback-toasts.html`, `feedback-tooltips.html` |
| Form components | 12 | `forms/index.html`, `forms/components/01-text-input.html` through `forms/components/10-file-upload.html` |
| Data display | 5 | `components/data-display/cards.html`, `components/data-display/lists.html`, `components/data-display/primitives.html`, `components/data-display/showcase.html`, `components/data-display/tables.html` |

### 2.2 Duplicated Content Patterns

#### Tailwind Config Block (identical across most files)
Lines 13-76 in most files contain the same `tailwind.config` JavaScript object with:
- Background colors (`bg.primary`, `bg.secondary`, `bg.tertiary`, `bg.hover`)
- Text colors (`text.primary`, `text.secondary`, `text.tertiary`, `text.inverse`)
- Accent colors (`accent.orange`, `accent.blue` with variants)
- Semantic colors (`success`, `warning`, `error`, `info` with bg/border variants)
- Border colors
- Font families (`sans`, `mono`)

#### Common CSS Styles (duplicated with slight variations)
```css
/* Scrollbar styles - found in all files */
::-webkit-scrollbar { width: 8px; height: 8px; }
::-webkit-scrollbar-track { background: #1d2022; }
::-webkit-scrollbar-thumb { background: #3f4447; border-radius: 4px; }
::-webkit-scrollbar-thumb:hover { background: #4a4f52; }

/* Focus styles - found in all files */
*:focus-visible { outline: 2px solid #098ecf; outline-offset: 2px; }

/* Screen reader utility - found in form components */
.sr-only { position: absolute; width: 1px; height: 1px; ... }

/* Animations - found in multiple files */
@keyframes shimmer { ... }
@keyframes pulse { ... }
@keyframes slideIn { ... }
```

### 2.3 Inconsistencies Found

| File | Issue |
|------|-------|
| `feedback-alerts.html` | Uses different color scheme (`dark-900`, `dark-800`) not matching design system |
| Various files | Some use hex colors directly, others use rgba() |
| Form components | Most extensive component styles, well-organized |
| Dashboard | Contains sidebar/dropdown specific animations |

---

## 3. Architectural Considerations

### 3.1 Constraints

1. **No Build Step**: Prototypes must work with browser live preview (Tailwind CDN)
2. **Relative Path Complexity**: Files exist at multiple nesting levels:
   - `docs/prototypes/*.html` (2 levels from css/)
   - `docs/prototypes/forms/*.html` (3 levels)
   - `docs/prototypes/forms/components/*.html` (4 levels)
   - `docs/prototypes/components/data-display/*.html` (4 levels)
3. **Tailwind CDN Config**: The `tailwind.config` must remain inline in each file (CDN limitation)
4. **Browser Caching**: Changes to shared CSS should propagate to all prototypes

### 3.2 Tailwind CDN Constraint

The Tailwind Play CDN (`cdn.tailwindcss.com`) requires configuration to be inline:
```html
<script>
  tailwind.config = { /* must be inline */ }
</script>
```

**Decision**: Create a shared JavaScript file for Tailwind config that can be loaded before the CDN script processes the page. This requires a specific script loading order.

### 3.3 CSS Custom Properties Strategy

CSS custom properties (variables) will be used to:
- Define all design tokens in one place
- Allow Tailwind utility classes to reference these tokens
- Enable easy theme modifications
- Support future dark/light mode if needed

---

## 4. Target File Structure

```
docs/prototypes/
+-- css/
|   +-- tokens.css              # CSS custom properties (design tokens)
|   +-- tailwind.config.js      # Shared Tailwind configuration (loaded via script)
|   +-- base.css                # Reset, scrollbar, focus states, box-sizing
|   +-- utilities.css           # Utility classes (sr-only, animations)
|   +-- components/
|   |   +-- buttons.css         # Button component styles
|   |   +-- forms.css           # Form input, select, checkbox, toggle styles
|   |   +-- cards.css           # Card container styles
|   |   +-- tables.css          # Table and data display styles
|   |   +-- alerts.css          # Alert and notification styles
|   |   +-- navigation.css      # Navbar, sidebar, breadcrumb styles
|   |   +-- modals.css          # Modal and drawer styles
|   |   +-- loading.css         # Spinner, skeleton, progress styles
|   +-- main.css                # Single import file (imports all above)
|   +-- README.md               # CSS architecture documentation
+-- [existing prototype files]
```

### 4.1 File Responsibilities

| File | Purpose | Approximate Size |
|------|---------|------------------|
| `tokens.css` | CSS custom properties for colors, spacing, typography, shadows, radii | ~120 lines |
| `tailwind.config.js` | Shared Tailwind theme extending design tokens | ~80 lines |
| `base.css` | Browser reset, scrollbar, focus ring, box-sizing | ~50 lines |
| `utilities.css` | `.sr-only`, animation keyframes, common utility classes | ~80 lines |
| `components/buttons.css` | `.btn`, `.btn-primary`, `.btn-secondary`, etc. | ~100 lines |
| `components/forms.css` | `.form-input`, `.form-select`, `.form-checkbox`, etc. | ~200 lines |
| `components/cards.css` | `.card`, `.card-header`, `.stat-card`, etc. | ~80 lines |
| `components/tables.css` | `.table`, `.table-header`, `.table-row`, etc. | ~100 lines |
| `components/alerts.css` | `.alert`, `.alert-info`, `.alert-success`, etc. | ~80 lines |
| `components/navigation.css` | `.navbar`, `.sidebar`, `.breadcrumb`, etc. | ~120 lines |
| `components/modals.css` | `.modal`, `.drawer`, `.modal-overlay`, etc. | ~80 lines |
| `components/loading.css` | `.skeleton`, `.spinner`, `.progress`, etc. | ~60 lines |
| `main.css` | Import statements only | ~20 lines |

---

## 5. Subagent Task Plan

### 5.1 design-specialist

**Deliverables:**
1. Review and validate CSS custom property naming conventions
2. Confirm all design tokens from `docs/design-system.md` are represented in `tokens.css`
3. Identify any missing tokens needed for existing prototypes
4. Provide accessibility validation for focus states and color contrast
5. Document any design system updates needed based on findings

**Acceptance Criteria:**
- [ ] All colors from design-system.md mapped to CSS custom properties
- [ ] All spacing values from design-system.md mapped
- [ ] All typography values from design-system.md mapped
- [ ] All shadow/elevation values from design-system.md mapped
- [ ] All border-radius values from design-system.md mapped
- [ ] Focus ring styling meets WCAG 2.1 AA requirements

### 5.2 html-prototyper

**Deliverables:**

**Phase 1: Create CSS Architecture**
1. Create `docs/prototypes/css/tokens.css` with all design tokens as CSS custom properties
2. Create `docs/prototypes/css/tailwind.config.js` referencing CSS custom properties
3. Create `docs/prototypes/css/base.css` with reset and focus styles
4. Create `docs/prototypes/css/utilities.css` with utility classes and animations

**Phase 2: Extract Component Styles**
1. Create `docs/prototypes/css/components/buttons.css`
2. Create `docs/prototypes/css/components/forms.css`
3. Create `docs/prototypes/css/components/cards.css`
4. Create `docs/prototypes/css/components/tables.css`
5. Create `docs/prototypes/css/components/alerts.css`
6. Create `docs/prototypes/css/components/navigation.css`
7. Create `docs/prototypes/css/components/modals.css`
8. Create `docs/prototypes/css/components/loading.css`
9. Create `docs/prototypes/css/main.css` that imports all component files

**Phase 3: Refactor Prototype Files**
1. Update all 27 prototype HTML files to use shared CSS imports
2. Remove duplicated inline styles
3. Update Tailwind config to load from shared file
4. Verify each prototype renders correctly

**Acceptance Criteria:**
- [ ] All CSS files created in `docs/prototypes/css/` structure
- [ ] `tokens.css` contains all design system tokens
- [ ] `tailwind.config.js` properly extends Tailwind with design tokens
- [ ] `base.css` contains scrollbar, focus, and reset styles
- [ ] `utilities.css` contains `.sr-only` and animation keyframes
- [ ] Each component CSS file contains relevant extracted styles
- [ ] `main.css` imports all files in correct order
- [ ] All 27 prototypes updated to use shared CSS
- [ ] All prototypes render identically to before refactoring
- [ ] No inline `<style>` blocks for common styles remain

### 5.3 dotnet-specialist

**Deliverables:**
1. Review CSS architecture for alignment with future Blazor structure
2. Ensure CSS file organization maps to planned Blazor component structure
3. Identify any CSS that should be component-scoped vs global
4. Validate CSS custom property naming for C# constant generation

**Acceptance Criteria:**
- [ ] CSS structure aligns with `src/DiscordBot.Bot/wwwroot/css/` target structure
- [ ] Component CSS files map to Blazor component directories
- [ ] Naming conventions support future CSS isolation files

### 5.4 docs-writer

**Deliverables:**
1. Create `docs/prototypes/css/README.md` documenting the CSS architecture
2. Update `docs/design-system.md` with CSS file references
3. Document how to add new components/styles
4. Document the Tailwind CDN configuration approach

**Acceptance Criteria:**
- [ ] README explains file structure and purpose
- [ ] README includes usage examples for adding new prototypes
- [ ] README documents Tailwind config loading mechanism
- [ ] design-system.md updated with CSS architecture section

---

## 6. Implementation Phases

### Phase 1: Foundation (Day 1-2)

| Task | Owner | Dependencies | Deliverables |
|------|-------|--------------|--------------|
| Create directory structure | html-prototyper | None | `css/` folder with subdirectories |
| Create tokens.css | html-prototyper | design-specialist review | CSS custom properties file |
| Create tailwind.config.js | html-prototyper | tokens.css | Shared Tailwind config |
| Create base.css | html-prototyper | tokens.css | Reset and focus styles |
| Create utilities.css | html-prototyper | tokens.css | Utility classes |
| Design system review | design-specialist | tokens.css draft | Validation report |

### Phase 2: Component Extraction (Day 2-3)

| Task | Owner | Dependencies | Deliverables |
|------|-------|--------------|--------------|
| Extract button styles | html-prototyper | Phase 1 | buttons.css |
| Extract form styles | html-prototyper | Phase 1 | forms.css |
| Extract card styles | html-prototyper | Phase 1 | cards.css |
| Extract table styles | html-prototyper | Phase 1 | tables.css |
| Extract alert styles | html-prototyper | Phase 1 | alerts.css |
| Extract navigation styles | html-prototyper | Phase 1 | navigation.css |
| Extract modal styles | html-prototyper | Phase 1 | modals.css |
| Extract loading styles | html-prototyper | Phase 1 | loading.css |
| Create main.css | html-prototyper | All component CSS | Import manifest |

### Phase 3: Prototype Refactoring (Day 3-4)

| Task | Owner | Dependencies | Deliverables |
|------|-------|--------------|--------------|
| Update root prototypes (2) | html-prototyper | Phase 2 | Refactored HTML files |
| Update feedback prototypes (9) | html-prototyper | Phase 2 | Refactored HTML files |
| Update form prototypes (12) | html-prototyper | Phase 2 | Refactored HTML files |
| Update data-display prototypes (5) | html-prototyper | Phase 2 | Refactored HTML files |
| Visual regression testing | html-prototyper | All updates | Test report |

### Phase 4: Documentation & Review (Day 4-5)

| Task | Owner | Dependencies | Deliverables |
|------|-------|--------------|--------------|
| Create CSS README | docs-writer | Phase 2, 3 | README.md |
| Update design-system.md | docs-writer | Phase 2 | Updated documentation |
| Blazor alignment review | dotnet-specialist | Phase 2 | Alignment report |
| Accessibility audit | design-specialist | Phase 3 | Audit report |
| Final review | All | All phases | Sign-off |

---

## 7. Detailed File Specifications

### 7.1 tokens.css

```css
/* ===========================================
   DESIGN TOKENS
   Source of truth for all design system values
   =========================================== */

:root {
  /* ----------------------------------------
     Background Colors
     ---------------------------------------- */
  --color-bg-primary: #1d2022;
  --color-bg-secondary: #262a2d;
  --color-bg-tertiary: #2f3336;
  --color-bg-hover: #363a3e;

  /* ----------------------------------------
     Text Colors
     ---------------------------------------- */
  --color-text-primary: #d7d3d0;
  --color-text-secondary: #a8a5a3;
  --color-text-tertiary: #7a7876;
  --color-text-inverse: #1d2022;

  /* ----------------------------------------
     Accent Colors - Orange (Primary)
     ---------------------------------------- */
  --color-accent-orange: #cb4e1b;
  --color-accent-orange-hover: #e5591f;
  --color-accent-orange-active: #b04517;
  --color-accent-orange-muted: rgba(203, 78, 27, 0.2);

  /* ----------------------------------------
     Accent Colors - Blue (Secondary)
     ---------------------------------------- */
  --color-accent-blue: #098ecf;
  --color-accent-blue-hover: #0ba3ea;
  --color-accent-blue-active: #0879b3;
  --color-accent-blue-muted: rgba(9, 142, 207, 0.2);

  /* ----------------------------------------
     Semantic Colors
     ---------------------------------------- */
  --color-success: #10b981;
  --color-success-bg: rgba(16, 185, 129, 0.1);
  --color-success-border: rgba(16, 185, 129, 0.3);

  --color-warning: #f59e0b;
  --color-warning-bg: rgba(245, 158, 11, 0.1);
  --color-warning-border: rgba(245, 158, 11, 0.3);

  --color-error: #ef4444;
  --color-error-bg: rgba(239, 68, 68, 0.1);
  --color-error-border: rgba(239, 68, 68, 0.3);

  --color-info: #06b6d4;
  --color-info-bg: rgba(6, 182, 212, 0.1);
  --color-info-border: rgba(6, 182, 212, 0.3);

  /* ----------------------------------------
     Border Colors
     ---------------------------------------- */
  --color-border-primary: #3f4447;
  --color-border-secondary: #2f3336;
  --color-border-focus: #098ecf;

  /* ----------------------------------------
     Spacing Scale
     ---------------------------------------- */
  --space-0: 0;
  --space-1: 0.25rem;    /* 4px */
  --space-2: 0.5rem;     /* 8px */
  --space-3: 0.75rem;    /* 12px */
  --space-4: 1rem;       /* 16px */
  --space-5: 1.25rem;    /* 20px */
  --space-6: 1.5rem;     /* 24px */
  --space-8: 2rem;       /* 32px */
  --space-10: 2.5rem;    /* 40px */
  --space-12: 3rem;      /* 48px */
  --space-16: 4rem;      /* 64px */

  /* ----------------------------------------
     Typography
     ---------------------------------------- */
  --font-sans: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto,
               "Helvetica Neue", Arial, sans-serif;
  --font-mono: ui-monospace, SFMono-Regular, "SF Mono", Menlo,
               Monaco, Consolas, monospace;

  /* Font Sizes */
  --text-xs: 0.75rem;    /* 12px */
  --text-sm: 0.875rem;   /* 14px */
  --text-base: 1rem;     /* 16px */
  --text-lg: 1.125rem;   /* 18px */
  --text-xl: 1.25rem;    /* 20px */
  --text-2xl: 1.5rem;    /* 24px */
  --text-3xl: 1.875rem;  /* 30px */
  --text-4xl: 2.25rem;   /* 36px */

  /* ----------------------------------------
     Border Radius
     ---------------------------------------- */
  --radius-sm: 0.25rem;  /* 4px */
  --radius-md: 0.375rem; /* 6px */
  --radius-lg: 0.5rem;   /* 8px */
  --radius-xl: 0.75rem;  /* 12px */
  --radius-full: 9999px;

  /* ----------------------------------------
     Shadows
     ---------------------------------------- */
  --shadow-sm: 0 1px 2px 0 rgba(0, 0, 0, 0.3);
  --shadow-md: 0 4px 6px -1px rgba(0, 0, 0, 0.3),
               0 2px 4px -1px rgba(0, 0, 0, 0.2);
  --shadow-lg: 0 10px 15px -3px rgba(0, 0, 0, 0.3),
               0 4px 6px -2px rgba(0, 0, 0, 0.2);
  --shadow-xl: 0 20px 25px -5px rgba(0, 0, 0, 0.3),
               0 10px 10px -5px rgba(0, 0, 0, 0.2);

  /* ----------------------------------------
     Transitions
     ---------------------------------------- */
  --transition-fast: 0.15s ease-in-out;
  --transition-normal: 0.2s ease-in-out;
  --transition-slow: 0.3s ease-in-out;

  /* ----------------------------------------
     Z-Index Scale
     ---------------------------------------- */
  --z-dropdown: 10;
  --z-sticky: 20;
  --z-fixed: 30;
  --z-modal-backdrop: 40;
  --z-modal: 50;
  --z-popover: 60;
  --z-tooltip: 70;
}
```

### 7.2 tailwind.config.js

```javascript
// Shared Tailwind CSS configuration for prototypes
// Load this file BEFORE the Tailwind CDN script

window.tailwindConfig = {
  theme: {
    extend: {
      colors: {
        bg: {
          primary: 'var(--color-bg-primary)',
          secondary: 'var(--color-bg-secondary)',
          tertiary: 'var(--color-bg-tertiary)',
          hover: 'var(--color-bg-hover)',
        },
        text: {
          primary: 'var(--color-text-primary)',
          secondary: 'var(--color-text-secondary)',
          tertiary: 'var(--color-text-tertiary)',
          inverse: 'var(--color-text-inverse)',
        },
        accent: {
          orange: {
            DEFAULT: 'var(--color-accent-orange)',
            hover: 'var(--color-accent-orange-hover)',
            active: 'var(--color-accent-orange-active)',
            muted: 'var(--color-accent-orange-muted)',
          },
          blue: {
            DEFAULT: 'var(--color-accent-blue)',
            hover: 'var(--color-accent-blue-hover)',
            active: 'var(--color-accent-blue-active)',
            muted: 'var(--color-accent-blue-muted)',
          },
        },
        success: {
          DEFAULT: 'var(--color-success)',
          bg: 'var(--color-success-bg)',
          border: 'var(--color-success-border)',
        },
        warning: {
          DEFAULT: 'var(--color-warning)',
          bg: 'var(--color-warning-bg)',
          border: 'var(--color-warning-border)',
        },
        error: {
          DEFAULT: 'var(--color-error)',
          bg: 'var(--color-error-bg)',
          border: 'var(--color-error-border)',
        },
        info: {
          DEFAULT: 'var(--color-info)',
          bg: 'var(--color-info-bg)',
          border: 'var(--color-info-border)',
        },
        border: {
          primary: 'var(--color-border-primary)',
          secondary: 'var(--color-border-secondary)',
          focus: 'var(--color-border-focus)',
        },
      },
      fontFamily: {
        sans: ['var(--font-sans)'],
        mono: ['var(--font-mono)'],
      },
      borderRadius: {
        sm: 'var(--radius-sm)',
        DEFAULT: 'var(--radius-md)',
        md: 'var(--radius-md)',
        lg: 'var(--radius-lg)',
        xl: 'var(--radius-xl)',
        full: 'var(--radius-full)',
      },
      boxShadow: {
        sm: 'var(--shadow-sm)',
        DEFAULT: 'var(--shadow-md)',
        md: 'var(--shadow-md)',
        lg: 'var(--shadow-lg)',
        xl: 'var(--shadow-xl)',
      },
      transitionDuration: {
        fast: '150ms',
        normal: '200ms',
        slow: '300ms',
      },
    },
  },
};

// Auto-apply config when Tailwind loads
if (typeof tailwind !== 'undefined') {
  tailwind.config = window.tailwindConfig;
}
```

### 7.3 Updated HTML Template Structure

After refactoring, prototype files will follow this pattern:

```html
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>[Page Title] - Discord Bot Admin</title>

  <!-- Design Tokens (CSS Custom Properties) -->
  <link rel="stylesheet" href="../css/tokens.css">

  <!-- Base Styles (Reset, Scrollbar, Focus) -->
  <link rel="stylesheet" href="../css/base.css">

  <!-- Component Styles -->
  <link rel="stylesheet" href="../css/main.css">

  <!-- Tailwind CSS CDN -->
  <script src="../css/tailwind.config.js"></script>
  <script src="https://cdn.tailwindcss.com"></script>
  <script>tailwind.config = window.tailwindConfig;</script>
</head>
<body class="bg-bg-primary text-text-primary font-sans antialiased">
  <!-- Page content -->
</body>
</html>
```

**Path Adjustment Examples:**
- `docs/prototypes/*.html` -> `href="css/tokens.css"`
- `docs/prototypes/forms/*.html` -> `href="../css/tokens.css"`
- `docs/prototypes/forms/components/*.html` -> `href="../../css/tokens.css"`
- `docs/prototypes/components/data-display/*.html` -> `href="../../css/tokens.css"`

---

## 8. Testing Strategy

### 8.1 Visual Regression Testing

| Test | Method | Pass Criteria |
|------|--------|---------------|
| Dashboard layout | Screenshot comparison | Pixel-perfect match |
| Form components | Manual inspection | All states functional |
| Feedback components | Interactive testing | Animations work correctly |
| Data display tables | Responsive testing | Breakpoints match original |
| Color consistency | Computed style check | All colors resolve correctly |

### 8.2 Browser Compatibility

| Browser | Version | Priority |
|---------|---------|----------|
| Chrome | Latest | Primary |
| Firefox | Latest | Primary |
| Safari | Latest | Secondary |
| Edge | Latest | Secondary |

### 8.3 Path Resolution Testing

Each nesting level must be verified:
- [ ] Root prototypes load CSS correctly
- [ ] forms/index.html loads CSS correctly
- [ ] forms/components/*.html files load CSS correctly
- [ ] components/data-display/*.html files load CSS correctly

---

## 9. Acceptance Criteria

### 9.1 CSS Architecture

- [ ] All files created in `docs/prototypes/css/` directory
- [ ] `tokens.css` contains all design tokens from design-system.md
- [ ] CSS custom properties follow `--color-*`, `--space-*`, `--text-*` naming
- [ ] `tailwind.config.js` properly references CSS custom properties
- [ ] `base.css` contains scrollbar, focus, and reset styles
- [ ] `utilities.css` contains `.sr-only` and all animation keyframes
- [ ] Component CSS files contain extracted component-specific styles
- [ ] `main.css` imports all files in correct cascade order
- [ ] No duplicate style definitions across CSS files

### 9.2 Prototype Refactoring

- [ ] All 27 prototype files updated with shared CSS imports
- [ ] No inline `<style>` blocks for common/shared styles
- [ ] Tailwind config loaded from shared JavaScript file
- [ ] Relative paths correct for each nesting level
- [ ] All prototypes render identically after refactoring

### 9.3 Documentation

- [ ] `docs/prototypes/css/README.md` created
- [ ] Architecture overview documented
- [ ] Usage instructions for new prototypes
- [ ] `docs/design-system.md` updated with CSS file references

### 9.4 Quality

- [ ] All browsers render consistently
- [ ] No CSS parse errors in browser console
- [ ] Focus states visible and accessible
- [ ] Scrollbar styling applied correctly

---

## 10. Risks and Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Tailwind CDN caching issues | Medium | Low | Add version query param to script URL |
| CSS specificity conflicts | Medium | Medium | Use consistent selector patterns, document cascade |
| Relative path errors | High | High | Create path mapping table, test all files |
| Missing component styles | Medium | Medium | Comprehensive style audit before extraction |
| Browser CSS variable support | Low | High | All modern browsers support; document minimum versions |
| Tailwind config not loading | Medium | High | Test script load order, add fallback |

---

## 11. Timeline Summary

| Phase | Duration | Deliverables |
|-------|----------|--------------|
| Phase 1: Foundation | 2 days | tokens.css, tailwind.config.js, base.css, utilities.css |
| Phase 2: Component Extraction | 1.5 days | All component CSS files, main.css |
| Phase 3: Prototype Refactoring | 1.5 days | All 27 HTML files updated |
| Phase 4: Documentation & Review | 1 day | README, design-system.md updates, testing |
| **Total** | **6 days** | Complete CSS architecture + refactored prototypes |

---

## 12. Future Considerations

### 12.1 Blazor Production CSS

This CSS architecture is designed to migrate to production:

```
src/DiscordBot.Bot/wwwroot/css/
+-- tokens.css              # Same file, production path
+-- base.css                # Same file
+-- utilities.css           # Same file
+-- components/             # Component styles (or use CSS isolation)
+-- main.css                # Production import manifest
```

### 12.2 CSS Isolation Mapping

| Prototype CSS | Blazor CSS Isolation |
|---------------|---------------------|
| `buttons.css` | `Button.razor.css` |
| `forms.css` | `Input.razor.css`, `Select.razor.css`, etc. |
| `cards.css` | `Card.razor.css` |
| `tables.css` | `DataTable.razor.css` |
| Global styles | `App.razor` or site.css |

### 12.3 Potential Enhancements

1. **CSS Custom Property Fallbacks**: Add fallback values for older browsers
2. **Dark/Light Mode**: Add alternate token sets for theme switching
3. **Print Styles**: Add print media queries if needed
4. **Prefers-reduced-motion**: Add motion preferences support

---

## Appendix A: Complete File List to Create

```
docs/prototypes/css/
+-- tokens.css
+-- tailwind.config.js
+-- base.css
+-- utilities.css
+-- main.css
+-- README.md
+-- components/
    +-- buttons.css
    +-- forms.css
    +-- cards.css
    +-- tables.css
    +-- alerts.css
    +-- navigation.css
    +-- modals.css
    +-- loading.css
```

## Appendix B: Files to Refactor

```
docs/prototypes/dashboard.html
docs/prototypes/component-showcase.html
docs/prototypes/feedback-alerts.html
docs/prototypes/feedback-confirmation.html
docs/prototypes/feedback-drawers.html
docs/prototypes/feedback-empty-states.html
docs/prototypes/feedback-loading.html
docs/prototypes/feedback-modals.html
docs/prototypes/feedback-skeletons.html
docs/prototypes/feedback-toasts.html
docs/prototypes/feedback-tooltips.html
docs/prototypes/forms/index.html
docs/prototypes/forms/components/01-text-input.html
docs/prototypes/forms/components/02-validation-states.html
docs/prototypes/forms/components/03-select-dropdown.html
docs/prototypes/forms/components/04-checkbox.html
docs/prototypes/forms/components/05-radio-button.html
docs/prototypes/forms/components/06-toggle-switch.html
docs/prototypes/forms/components/07-form-group.html
docs/prototypes/forms/components/08-date-time.html
docs/prototypes/forms/components/09-number-input.html
docs/prototypes/forms/components/10-file-upload.html
docs/prototypes/components/data-display/cards.html
docs/prototypes/components/data-display/lists.html
docs/prototypes/components/data-display/primitives.html
docs/prototypes/components/data-display/showcase.html
docs/prototypes/components/data-display/tables.html
```

---

*Document Version: 1.0*
*Created: 2025-12-07*
*Author: Systems Architect*
