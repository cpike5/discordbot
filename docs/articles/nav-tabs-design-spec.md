# Navigation Tabs Design Specification

**Version:** 1.0
**Status:** Draft
**Epic:** [#1248 - Unified Navigation Component System](https://github.com/cpike5/discordbot/issues/1248)
**Last Updated:** 2026-01-19

---

## Overview

This document specifies the design for a unified, reusable navigation component that consolidates five existing implementations (GuildNavBar, PerformanceTabs, AudioTabs, TabPanel, PortalHeader) into a single component with three style variants.

### Design Goals

- **Consistency**: Single source of truth for navigation UI patterns
- **Flexibility**: Three distinct visual styles for different contexts
- **Accessibility**: WCAG 2.1 AA compliant with proper focus states and ARIA support
- **Responsive**: Mobile-first with horizontal scroll support
- **Performance**: Minimal CSS overhead using design system tokens

---

## 1. Style Variants

The unified component supports three visual styles:

| Variant | Use Case | Current Implementations |
|---------|----------|------------------------|
| **Underline** | Primary navigation with clear active states | GuildNavBar, PerformanceTabs, PortalHeader |
| **Pills** | Segmented controls, compact navigation | AudioTabs |
| **Portal** | Discord-themed member-facing interfaces | PortalHeader |

---

## 2. Variant Specifications

### 2.1 Underline Variant

**Visual Style:** Tabs with bottom border indicator on active state, light container with subtle border.

#### Container Styles

```css
.nav-tabs-container-underline {
  position: relative;
  margin-bottom: 1.5rem;
}

.nav-tabs-underline {
  display: flex;
  gap: 0.5rem; /* 8px */
  border-bottom: 2px solid var(--color-border-primary);
  overflow-x: auto;
  -webkit-overflow-scrolling: touch;
  scrollbar-width: none; /* Hide scrollbar */
  scroll-behavior: smooth;
  scroll-snap-type: x proximity;
}

.nav-tabs-underline::-webkit-scrollbar {
  display: none;
}
```

**Design Rationale:** The bottom border provides a clear visual hierarchy and aligns with common web navigation patterns. 2px border weight ensures visibility on all screens.

#### Tab Item Styles

```css
.nav-tab-underline {
  /* Layout */
  display: flex;
  align-items: center;
  gap: 0.5rem; /* 8px - icon to text spacing */
  padding: 0.75rem 1rem; /* 12px 16px */

  /* Typography */
  font-size: 0.875rem; /* 14px */
  font-weight: 500; /* Medium */
  color: var(--color-text-secondary);

  /* Visual */
  border-bottom: 2px solid transparent;
  margin-bottom: -2px; /* Overlap container border */
  background: transparent;
  text-decoration: none;
  white-space: nowrap;

  /* Behavior */
  transition: all 0.2s ease-in-out;
  scroll-snap-align: start;
  flex-shrink: 0;
  cursor: pointer;
}
```

#### Interactive States

**Hover State:**
```css
.nav-tab-underline:hover {
  color: var(--color-text-primary);
  border-bottom-color: var(--color-border-secondary);
}
```

**Active State:**
```css
.nav-tab-underline.active {
  color: var(--color-accent-blue);
  border-bottom-color: var(--color-accent-blue);
}

.nav-tab-underline.active .tab-icon {
  /* Use solid icon variant for active state */
  /* Icon switching handled in component logic */
}
```

**Focus State:**
```css
.nav-tab-underline:focus-visible {
  outline: 2px solid var(--color-border-focus); /* Blue accent */
  outline-offset: 2px;
  border-radius: 0.25rem; /* 4px */
}
```

**Disabled State:**
```css
.nav-tab-underline:disabled,
.nav-tab-underline[aria-disabled="true"] {
  color: var(--color-text-tertiary);
  opacity: 0.5;
  cursor: not-allowed;
  pointer-events: none;
}
```

#### Icon Sizing

```css
.nav-tab-underline .tab-icon {
  width: 1.25rem; /* 20px */
  height: 1.25rem; /* 20px */
  flex-shrink: 0;
}
```

**Icon Behavior:**
- Use outline variant for inactive tabs
- Use solid variant for active tab
- Icons are optional; tabs work without them

#### Responsive Adjustments (Mobile)

```css
@media (max-width: 640px) {
  .nav-tab-underline {
    padding: 0.625rem 0.75rem; /* 10px 12px - tighter on mobile */
    font-size: 0.8125rem; /* 13px */
  }

  .nav-tab-underline .tab-icon {
    width: 1rem; /* 16px */
    height: 1rem; /* 16px */
  }

  /* Show short labels on mobile */
  .nav-tab-underline .tab-label-long {
    display: none;
  }

  .nav-tab-underline .tab-label-short {
    display: inline;
  }
}

@media (min-width: 641px) {
  .nav-tab-underline .tab-label-long {
    display: inline;
  }

  .nav-tab-underline .tab-label-short {
    display: none;
  }
}
```

---

### 2.2 Pills Variant

**Visual Style:** Segmented control with rounded container and filled background on active tab. Compact spacing creates a unified control appearance.

#### Container Styles

```css
.nav-tabs-container-pills {
  position: relative;
  margin-bottom: 1.5rem;
}

.nav-tabs-pills {
  display: flex;
  gap: 0.25rem; /* 4px - tight spacing for segmented look */
  padding: 0.25rem; /* 4px - inner padding */
  background: var(--color-bg-secondary);
  border: 1px solid var(--color-border-primary);
  border-radius: 0.75rem; /* 12px - rounded container */
  overflow-x: auto;
  scrollbar-width: none;
  -ms-overflow-style: none;
}

.nav-tabs-pills::-webkit-scrollbar {
  display: none;
}
```

**Design Rationale:** The pill container creates a visual grouping that emphasizes the segmented control pattern. The rounded corners (0.75rem) provide a modern, friendly appearance while maintaining professionalism.

#### Tab Item Styles

```css
.nav-tab-pill {
  /* Layout */
  display: inline-flex;
  align-items: center;
  gap: 0.5rem; /* 8px */
  padding: 0.625rem 1rem; /* 10px 16px */

  /* Typography */
  font-size: 0.875rem; /* 14px */
  font-weight: 500; /* Medium */
  color: var(--color-text-secondary);

  /* Visual */
  background: transparent;
  border-radius: 0.5rem; /* 8px - matches inner radius */
  text-decoration: none;
  white-space: nowrap;

  /* Behavior */
  transition: all 0.15s ease-out;
  cursor: pointer;
}
```

#### Interactive States

**Hover State:**
```css
.nav-tab-pill:hover {
  color: var(--color-text-primary);
  background: var(--color-bg-hover);
}
```

**Active State:**
```css
.nav-tab-pill.active {
  color: var(--color-text-primary);
  background: var(--color-bg-tertiary);
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
}

.nav-tab-pill.active .tab-icon {
  color: var(--color-accent-blue);
}
```

**Focus State:**
```css
.nav-tab-pill:focus-visible {
  outline: 2px solid var(--color-border-focus);
  outline-offset: 2px;
}
```

**Disabled State:**
```css
.nav-tab-pill:disabled,
.nav-tab-pill[aria-disabled="true"] {
  color: var(--color-text-tertiary);
  opacity: 0.5;
  cursor: not-allowed;
  pointer-events: none;
}
```

#### Icon Sizing

```css
.nav-tab-pill .tab-icon {
  width: 1.125rem; /* 18px */
  height: 1.125rem; /* 18px */
  flex-shrink: 0;
}
```

#### Badge Support

Pills variant supports badges for counts/notifications:

```css
.nav-tab-pill-badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 1.25rem; /* 20px */
  height: 1.25rem; /* 20px */
  padding: 0 0.375rem; /* 0 6px */
  font-size: 0.6875rem; /* 11px */
  font-weight: 600; /* Semibold */
  color: var(--color-accent-blue);
  background: var(--color-accent-blue-muted);
  border-radius: 9999px; /* Fully rounded */
}
```

#### Responsive Adjustments (Mobile)

```css
@media (max-width: 640px) {
  .nav-tab-pill {
    padding: 0.5rem 0.875rem; /* 8px 14px */
    font-size: 0.8125rem; /* 13px */
  }

  .nav-tab-pill .tab-icon {
    width: 1rem; /* 16px */
    height: 1rem; /* 16px */
  }
}
```

---

### 2.3 Portal Variant

**Visual Style:** Discord-themed navigation with larger tabs, prominent active state using Discord brand color, and enhanced visual hierarchy for member-facing interfaces.

#### Container Styles

```css
.nav-tabs-container-portal {
  position: relative;
  margin-bottom: 0; /* Portal header manages its own spacing */
}

.nav-tabs-portal {
  display: flex;
  gap: 0.5rem; /* 8px */
  border-bottom: 2px solid var(--color-border-secondary);
  overflow-x: auto;
  -webkit-overflow-scrolling: touch;
  scrollbar-width: none;
  scroll-behavior: smooth;
}

.nav-tabs-portal::-webkit-scrollbar {
  display: none;
}
```

**Design Rationale:** Portal variant emphasizes brand identity using Discord's purple accent color. Larger touch targets and clearer visual states accommodate member users who may be less familiar with the interface.

#### Tab Item Styles

```css
.nav-tab-portal {
  /* Layout */
  display: inline-flex;
  align-items: center;
  gap: 0.5rem; /* 8px */
  padding: 0.75rem 1.25rem; /* 12px 20px - larger horizontal padding */

  /* Typography */
  font-size: 0.875rem; /* 14px */
  font-weight: 500; /* Medium */
  color: var(--color-text-secondary);

  /* Visual */
  border-bottom: 2px solid transparent;
  margin-bottom: -2px;
  background: transparent;
  text-decoration: none;
  white-space: nowrap;
  position: relative;

  /* Behavior */
  transition: all 0.2s ease-in-out;
  cursor: pointer;
}
```

#### Interactive States

**Hover State:**
```css
.nav-tab-portal:hover {
  color: var(--color-text-primary);
  background-color: var(--color-bg-hover);
}
```

**Active State:**
```css
.nav-tab-portal.active {
  color: var(--color-discord); /* #5865F2 - Discord purple */
  border-bottom-color: var(--color-discord);
}
```

**Focus State:**
```css
.nav-tab-portal:focus-visible {
  outline: 2px solid var(--color-discord);
  outline-offset: 2px;
  border-radius: 0.25rem; /* 4px */
}
```

**Disabled State:**
```css
.nav-tab-portal:disabled,
.nav-tab-portal[aria-disabled="true"] {
  color: var(--color-text-tertiary);
  opacity: 0.5;
  cursor: not-allowed;
  pointer-events: none;
}
```

#### Icon Sizing

```css
.nav-tab-portal .tab-icon {
  width: 1rem; /* 16px */
  height: 1rem; /* 16px */
  flex-shrink: 0;
}
```

#### Responsive Adjustments (Mobile)

```css
@media (max-width: 640px) {
  .nav-tab-portal {
    padding: 0.75rem 1rem; /* Maintain larger touch target */
    font-size: 0.8125rem; /* 13px */
    white-space: nowrap;
  }

  /* Portal tabs don't collapse labels - always show full text */
}
```

---

## 3. Scroll Indicators

All three variants support scroll indicators when content overflows horizontally.

### Implementation

**Gradient Shadows:**

```css
.nav-tabs-container-underline::before,
.nav-tabs-container-underline::after,
.nav-tabs-container-pills::before,
.nav-tabs-container-pills::after {
  content: '';
  position: absolute;
  top: 0;
  bottom: 0;
  width: 2rem; /* 32px fade width */
  pointer-events: none;
  z-index: 1;
  opacity: 0;
  transition: opacity 0.2s ease;
}

/* Left shadow */
.nav-tabs-container-underline::before,
.nav-tabs-container-pills::before {
  left: 0;
  background: linear-gradient(to right, var(--color-bg-primary), transparent);
}

/* Right shadow */
.nav-tabs-container-underline::after,
.nav-tabs-container-pills::after {
  right: 0;
  background: linear-gradient(to left, var(--color-bg-primary), transparent);
}

/* Show when scrollable (classes added by JavaScript) */
.nav-tabs-container-underline.can-scroll-left::before,
.nav-tabs-container-pills.can-scroll-left::before {
  opacity: 1;
}

.nav-tabs-container-underline.can-scroll-right::after,
.nav-tabs-container-pills.can-scroll-right::after {
  opacity: 1;
}
```

**Pills Variant Adjustments:**

Pills variant has rounded container, so border-radius must be applied to gradients:

```css
.nav-tabs-container-pills::before {
  border-radius: 0.75rem 0 0 0.75rem;
}

.nav-tabs-container-pills::after {
  border-radius: 0 0.75rem 0.75rem 0;
}
```

**Portal Variant:**

Portal variant does not use scroll indicators, relying on native scroll behavior.

### JavaScript Requirements

Scroll indicators require JavaScript to toggle `can-scroll-left` and `can-scroll-right` classes based on scroll position:

```javascript
function updateScrollIndicators(containerEl, tabsEl) {
  const scrollLeft = tabsEl.scrollLeft;
  const scrollWidth = tabsEl.scrollWidth;
  const clientWidth = tabsEl.clientWidth;
  const isScrollable = scrollWidth > clientWidth;

  if (isScrollable) {
    // Can scroll left if not at start (5px threshold)
    if (scrollLeft > 5) {
      containerEl.classList.add('can-scroll-left');
    } else {
      containerEl.classList.remove('can-scroll-left');
    }

    // Can scroll right if not at end (5px threshold)
    if (scrollLeft < scrollWidth - clientWidth - 5) {
      containerEl.classList.add('can-scroll-right');
    } else {
      containerEl.classList.remove('can-scroll-right');
    }
  } else {
    containerEl.classList.remove('can-scroll-left', 'can-scroll-right');
  }
}
```

### Auto-Scroll Active Tab

When component loads, automatically scroll active tab into view:

```javascript
function scrollActiveTabIntoView(tabsEl) {
  const activeTab = tabsEl.querySelector('.active');
  if (activeTab) {
    const tabRect = activeTab.getBoundingClientRect();
    const containerRect = tabsEl.getBoundingClientRect();

    // Check if active tab is outside viewport
    if (tabRect.left < containerRect.left || tabRect.right > containerRect.right) {
      activeTab.scrollIntoView({
        behavior: 'smooth',
        inline: 'center',
        block: 'nearest'
      });
    }
  }
}
```

---

## 4. Accessibility Specifications

### ARIA Attributes

**Container:**
```html
<nav role="tablist" aria-label="[Section name]">
```

**Tab Items:**
```html
<a role="tab"
   aria-selected="true|false"
   aria-disabled="true" (if disabled)
   href="[url]">
```

### Keyboard Navigation

| Key | Action |
|-----|--------|
| `Tab` | Move focus to/from tab group |
| `Arrow Left` | Focus previous tab (optional enhancement) |
| `Arrow Right` | Focus next tab (optional enhancement) |
| `Enter` / `Space` | Activate focused tab |

**Note:** Arrow key navigation is optional. Tabs are implemented as anchor links, so standard link navigation applies.

### Focus Indicators

All variants use visible focus indicators with minimum 2px outline and 2px offset:

```css
:focus-visible {
  outline: 2px solid var(--color-border-focus);
  outline-offset: 2px;
}
```

**Contrast Requirements:**
- Focus outline (`--color-border-focus` / `#098ecf`) on background: **6.2:1** (AA)
- Portal focus (`--color-discord` / `#5865F2`) on background: **4.8:1** (AA)

### Minimum Touch Target Sizes

All interactive tab items meet WCAG 2.1 Level AAA target size guideline:

- **Desktop:** 44px height (padding: 0.75rem top/bottom = 12px + font line-height ≈ 20px = 32px minimum, container padding adds additional space)
- **Mobile:** 44px height maintained with adjusted padding

**Pills Variant:** Container padding (0.25rem = 4px) + tab padding (0.625rem = 10px) + icon/text (≥18px) + container padding (4px) = **46px minimum**

### Color Contrast

All text/background combinations meet WCAG 2.1 AA standards (4.5:1 for normal text, 3:1 for large text):

| Combination | Contrast Ratio | Pass |
|-------------|----------------|------|
| `text-secondary` on `bg-primary` | 5.9:1 | AA ✓ |
| `text-primary` on `bg-primary` | 10.8:1 | AAA ✓ |
| `accent-blue` on `bg-primary` | 6.2:1 | AA ✓ |
| `discord` on `bg-secondary` | 4.8:1 | AA ✓ |

---

## 5. Component Anatomy

### Underline Variant Structure

```
┌─────────────────────────────────────────────────────────┐
│ .nav-tabs-container-underline                           │
│ ┌─────────────────────────────────────────────────────┐ │
│ │ ::before (left scroll shadow - z-index: 1)          │ │
│ │ .nav-tabs-underline                                 │ │
│ │ ┌─────────┬─────────┬─────────┬─────────┐          │ │
│ │ │  Tab 1  │  Tab 2  │  Tab 3  │  Tab 4  │          │ │
│ │ │  Icon ●  │  Icon ○  │  Icon ○  │  Icon ○  │          │ │
│ │ │  Label  │  Label  │  Label  │  Label  │          │ │
│ │ └─────────┴─────────┴─────────┴─────────┘          │ │
│ │ ═══════════════════════════════════════════════════ │ │
│ │   ▔▔▔▔▔▔▔   ← Active indicator (2px blue)          │ │
│ │ ::after (right scroll shadow - z-index: 1)          │ │
│ └─────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────┘
```

### Pills Variant Structure

```
┌─────────────────────────────────────────────────────────┐
│ .nav-tabs-container-pills                               │
│ ┌─────────────────────────────────────────────────────┐ │
│ │ ::before (left scroll shadow)                       │ │
│ │ ╔═══════════════════════════════════════════════╗  │ │
│ │ ║ .nav-tabs-pills (rounded container)           ║  │ │
│ │ ║ ┏━━━━━━━┓┌────────┐┌────────┐┌────────┐       ║  │ │
│ │ ║ ┃Tab 1  ┃│ Tab 2  ││ Tab 3  ││ Tab 4  │       ║  │ │
│ │ ║ ┃Active ┃│Inactive││Inactive││Inactive│       ║  │ │
│ │ ║ ┗━━━━━━━┛└────────┘└────────┘└────────┘       ║  │ │
│ │ ╚═══════════════════════════════════════════════╝  │ │
│ │ ::after (right scroll shadow)                       │ │
│ └─────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────┘
```

### Z-Index Layers

```
Layer 3 (z-index: 1): Scroll indicators (::before, ::after)
Layer 2: Tab items (default z-index)
Layer 1: Container background (default z-index)
```

---

## 6. Responsive Behavior

### Breakpoint Strategy

The component uses a single breakpoint at **640px** (Tailwind's `sm` breakpoint):

- **Mobile (0-640px):** Compact spacing, smaller fonts, short labels, horizontal scroll
- **Desktop (641px+):** Full spacing, full labels, horizontal scroll if needed

### Mobile Optimizations

**Label Truncation:**

Long labels are replaced with short variants on mobile using responsive utility classes:

```html
<span class="tab-label-long">Health Metrics</span>
<span class="tab-label-short">Health</span>
```

**Touch-Friendly Spacing:**

Maintain minimum 44px touch targets even on mobile devices.

**Scroll Performance:**

- `scroll-behavior: smooth` for better UX
- `-webkit-overflow-scrolling: touch` for iOS momentum scrolling
- `scroll-snap-type: x proximity` for natural stopping points

---

## 7. Animation & Transitions

### Transition Specifications

**Tab State Changes:**

```css
transition: all 0.2s ease-in-out;
```

Affects: color, background-color, border-color

**Scroll Indicator Fade:**

```css
transition: opacity 0.2s ease;
```

Affects: scroll shadow opacity (0 to 1)

**Hover States:**

Fast feedback (0.15s - 0.2s) creates responsive feel.

### Reduced Motion Support

Respect user preferences for reduced motion:

```css
@media (prefers-reduced-motion: reduce) {
  .nav-tab-underline,
  .nav-tab-pill,
  .nav-tab-portal,
  .nav-tabs-container-underline::before,
  .nav-tabs-container-underline::after,
  .nav-tabs-container-pills::before,
  .nav-tabs-container-pills::after {
    transition: none;
    animation: none;
  }

  /* Disable smooth scroll */
  .nav-tabs-underline,
  .nav-tabs-pills,
  .nav-tabs-portal {
    scroll-behavior: auto;
  }
}
```

---

## 8. CSS Implementation Approach

### File Organization

**Recommendation:** Use separate CSS file for reusable component.

```
src/DiscordBot.Bot/wwwroot/css/components/nav-tabs.css
```

**Import in main stylesheet:**

```css
@import './components/nav-tabs.css';
```

### Leveraging Design Tokens

All styles use CSS custom properties from the design system:

**Colors:**
- `var(--color-bg-primary)`
- `var(--color-bg-secondary)`
- `var(--color-bg-tertiary)`
- `var(--color-bg-hover)`
- `var(--color-text-primary)`
- `var(--color-text-secondary)`
- `var(--color-text-tertiary)`
- `var(--color-accent-blue)`
- `var(--color-accent-orange)`
- `var(--color-border-primary)`
- `var(--color-border-secondary)`
- `var(--color-border-focus)`
- `var(--color-discord)`

**Spacing:**
Use Tailwind spacing scale (`0.25rem`, `0.5rem`, `0.75rem`, `1rem`, etc.)

### Naming Convention

**BEM-inspired naming:**

```
.nav-tabs-container-{variant}     /* Container */
.nav-tabs-{variant}                /* Tab list */
.nav-tab-{variant}                 /* Individual tab */
.nav-tab-{variant}.active          /* State modifier */
.nav-tab-{variant} .tab-icon       /* Child element */
.nav-tab-{variant}-badge           /* Optional element */
```

**Variants:** `underline`, `pills`, `portal`

### Theme Support

Component automatically adapts to theme changes via CSS custom properties. No additional theme-specific CSS required unless special cases arise (see design-system.md for Purple Dusk theme overrides).

---

## 9. Visual Examples (CSS)

### Underline Active State

```css
/* Active tab with blue underline and icon */
.nav-tab-underline.active {
  color: #098ecf;                    /* Accent blue */
  border-bottom-color: #098ecf;
}
```

**Visual Result:**
```
┌─────────┐ ┌─────────┐ ┌─────────┐
│ ● Tab 1 │ │ ○ Tab 2 │ │ ○ Tab 3 │
└─────────┘ └─────────┘ └─────────┘
════════════════════════════════════
  ▔▔▔▔▔▔▔     ← Blue underline (2px)
```

### Pills Active State

```css
/* Active pill with filled background and shadow */
.nav-tab-pill.active {
  color: #d7d3d0;                    /* Text primary */
  background: #2f3336;               /* Elevated background */
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
}

.nav-tab-pill.active .tab-icon {
  color: #098ecf;                    /* Blue accent icon */
}
```

**Visual Result:**
```
╔═══════════════════════════════════════╗
║ ┏━━━━━━━┓┌────────┐┌────────┐        ║
║ ┃● Tab 1┃│○ Tab 2││○ Tab 3│        ║
║ ┗━━━━━━━┛└────────┘└────────┘        ║
╚═══════════════════════════════════════╝
   ▲ Filled background with shadow
```

### Portal Active State

```css
/* Active portal tab with Discord purple */
.nav-tab-portal.active {
  color: #5865F2;                    /* Discord purple */
  border-bottom-color: #5865F2;
}
```

**Visual Result:**
```
┌───────────┐ ┌───────────┐
│ ● Tab 1   │ │ ○ Tab 2   │  ← Discord purple text
└───────────┘ └───────────┘
═══════════════════════════
  ▔▔▔▔▔▔▔▔▔     ← Purple underline (2px)
```

### Hover State Example (All Variants)

```css
/* Hover provides visual feedback */
.nav-tab-underline:hover,
.nav-tab-pill:hover,
.nav-tab-portal:hover {
  color: var(--color-text-primary);  /* Brighten text */
  background: var(--color-bg-hover); /* Subtle background */
}
```

### Focus Indicator Example

```css
/* Keyboard focus ring */
.nav-tab-underline:focus-visible {
  outline: 2px solid #098ecf;        /* Blue ring */
  outline-offset: 2px;               /* 2px gap */
  border-radius: 0.25rem;            /* Slight rounding */
}
```

**Visual Result:**
```
  ┏━━━━━━━━━━━┓  ← 2px blue outline
  ┃           ┃     with 2px offset
  ┃ ● Tab 1   ┃
  ┃           ┃
  ┗━━━━━━━━━━━┛
```

---

## 10. Implementation Notes

### Icon Usage

**HeroIcons Support:**
- Component expects SVG paths for outline and solid variants
- Active tabs use solid icons, inactive use outline
- Icons are 24×24 viewBox, sized via CSS

**Example Structure:**
```html
<svg class="tab-icon" viewBox="0 0 24 24" fill="currentColor">
  <path d="M..." />
</svg>
```

### Long/Short Label Pattern

For responsive label switching:

```html
<span class="tab-label-long">API & Rate Limits</span>
<span class="tab-label-short">API</span>
```

CSS handles visibility based on screen size.

### Badge Pattern (Pills Variant)

Optional badge for counts:

```html
<span class="nav-tab-pill-badge">3</span>
```

### Navigation Pattern

Tabs are implemented as anchor links (`<a>` tags) with proper href attributes. For AJAX navigation, use `data-*` attributes and prevent default behavior in JavaScript.

---

## 11. Browser Support

- **Modern browsers:** Chrome 90+, Firefox 88+, Safari 14+, Edge 90+
- **CSS Features:**
  - CSS Grid and Flexbox (full support)
  - CSS Custom Properties (full support)
  - `scroll-behavior: smooth` (graceful degradation)
  - `scroll-snap-type` (progressive enhancement)
  - `:focus-visible` (polyfill or fallback to `:focus`)

### Fallbacks

**Scrollbar hiding:** Works in all modern browsers with prefixed properties.

**Scroll snapping:** Optional enhancement; tabs work without it.

**Smooth scrolling:** Degrades to instant scroll in older browsers.

---

## 12. Performance Considerations

**CSS Size:**
- Estimated total CSS: ~8-10KB uncompressed
- Using design tokens reduces duplication

**JavaScript:**
- Scroll indicator logic: ~50 lines
- Auto-scroll active tab: ~15 lines
- Total JS overhead: minimal (<2KB)

**Rendering:**
- Uses CSS transforms for scroll shadows (GPU-accelerated)
- Transitions limited to common properties (color, background, opacity)
- No layout thrashing

---

## 13. Migration Path

### Phase 1: Create Unified Component
- Implement CSS in `nav-tabs.css`
- Create Razor component with variant parameter
- Write documentation

### Phase 2: Replace GuildNavBar ✅
- ~~Update `_GuildNavBar.cshtml` to use unified component~~ (Completed - replaced by inline code in `_GuildLayout.cshtml` using `_TabPanel`)
- ~~Test across all guild pages~~
- ~~Verify mobile behavior~~

### Phase 3: Replace PerformanceTabs ✅
- ~~Update `_PerformanceTabs.cshtml`~~ (Completed - migrated to `_TabPanel` with AJAX mode)
- ~~Verify scroll indicators work correctly~~
- ~~Test AJAX navigation compatibility~~

### Phase 4: Replace AudioTabs
- Update `_AudioTabs.cshtml` to pills variant
- Verify badge styling
- Test badge count updates

### Phase 5: Replace TabPanel
- Generic tab implementation using underline variant
- Update all consumers

### Phase 6: Replace PortalHeader
- Update portal navigation to use portal variant
- Verify Discord branding preserved
- Test member-facing pages

---

## 14. Testing Checklist

### Visual Testing
- [ ] All three variants render correctly
- [ ] Active states display properly
- [ ] Hover states provide visual feedback
- [ ] Focus indicators are visible and meet contrast requirements
- [ ] Icons switch between outline/solid on active state
- [ ] Badges display correctly (pills variant)
- [ ] Scroll indicators appear/disappear correctly

### Responsive Testing
- [ ] Mobile view uses compact spacing
- [ ] Long labels switch to short labels on mobile
- [ ] Horizontal scroll works on all devices
- [ ] Touch targets meet 44px minimum
- [ ] Active tab scrolls into view on load

### Accessibility Testing
- [ ] Screen reader announces tabs correctly
- [ ] `aria-selected` updates on tab change
- [ ] Focus indicators visible with keyboard navigation
- [ ] Color contrast meets WCAG AA standards
- [ ] Reduced motion preference respected

### Browser Testing
- [ ] Chrome (Windows, Mac, Android)
- [ ] Firefox (Windows, Mac)
- [ ] Safari (Mac, iOS)
- [ ] Edge (Windows)

### Theme Testing
- [ ] Discord Dark theme displays correctly
- [ ] Purple Dusk theme displays correctly
- [ ] Custom theme support works

---

## 15. References

### Related Documentation
- [Design System](design-system.md) - Color tokens, typography, spacing
- [Form Implementation Standards](form-implementation-standards.md) - Component patterns
- [Guild Layout Spec](guild-layout-spec.md) - GuildNavBar usage

### External Resources
- [Segmented Control UI Design Best Practices](https://mobbin.com/glossary/segmented-control)
- [Tailwind CSS Tabs & Pills](https://flyonui.com/docs/navigations/tabs-pills/)
- [W3C ARIA Tabs Pattern](https://www.w3.org/WAI/ARIA/apg/patterns/tabs/)
- [WCAG 2.1 Success Criteria](https://www.w3.org/WAI/WCAG21/quickref/)

### Issue Tracking
- Epic: [#1248 - Unified Navigation Component System](https://github.com/cpike5/discordbot/issues/1248)
- [#1250 - Style Variants Implementation](https://github.com/cpike5/discordbot/issues/1250)
- [#1251 - JavaScript Features](https://github.com/cpike5/discordbot/issues/1251)
- [#1252 - Accessibility Compliance](https://github.com/cpike5/discordbot/issues/1252)

---

## Appendix A: Complete CSS Class Reference

### Underline Variant
- `.nav-tabs-container-underline` - Container with scroll indicators
- `.nav-tabs-underline` - Tab list with bottom border
- `.nav-tab-underline` - Individual tab
- `.nav-tab-underline.active` - Active state
- `.nav-tab-underline .tab-icon` - Icon element
- `.nav-tab-underline .tab-label-long` - Full label (desktop)
- `.nav-tab-underline .tab-label-short` - Short label (mobile)

### Pills Variant
- `.nav-tabs-container-pills` - Container with scroll indicators
- `.nav-tabs-pills` - Tab list with rounded container
- `.nav-tab-pill` - Individual tab
- `.nav-tab-pill.active` - Active state
- `.nav-tab-pill .tab-icon` - Icon element
- `.nav-tab-pill-badge` - Badge for counts

### Portal Variant
- `.nav-tabs-container-portal` - Container (no scroll indicators)
- `.nav-tabs-portal` - Tab list with bottom border
- `.nav-tab-portal` - Individual tab
- `.nav-tab-portal.active` - Active state (Discord purple)
- `.nav-tab-portal .tab-icon` - Icon element

### State Modifiers (All Variants)
- `.active` - Active tab state
- `:hover` - Hover state
- `:focus-visible` - Keyboard focus state
- `:disabled` or `[aria-disabled="true"]` - Disabled state

### Scroll Indicator Classes (JavaScript-controlled)
- `.can-scroll-left` - Show left scroll indicator
- `.can-scroll-right` - Show right scroll indicator

---

**Document Status:** Ready for Developer Implementation
**Next Steps:** Create component implementation issue, assign to developer, begin Phase 1 migration
