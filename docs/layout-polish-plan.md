# Layout Polish Plan

Improvements and fixes identified from a comprehensive review of `_Layout.cshtml`, `_Navbar.cshtml`, `_Sidebar.cshtml`, `_Breadcrumb.cshtml`, `_GuildLayout.cshtml`, `_LayoutLanding.cshtml`, `_MobileSearchOverlay.cshtml`, `_ToastContainer.cshtml`, and `site.css`.

## Structural / Code Fixes

### 1. Script Loading — 12 separate `<script>` tags

**File:** `_Layout.cshtml:68-98`

Loads 12 JS files synchronously at end of `<body>`. Each is a separate HTTP request that blocks parsing sequentially.

**Fix:** Bundle into 2-3 logical groups (e.g., `core.js` for navigation/toast/loading, `features.js` for search/notifications/preview, `realtime.js` for SignalR/dashboard-hub). Or at minimum add `defer` to non-dependent scripts. The SignalR CDN link should also use `integrity` for security.

---

### 2. Duplicate `tab-panel.css` include

**Files:** `_Layout.cshtml:20`, `_GuildLayout.cshtml:60`

`_Layout.cshtml` loads `tab-panel.css` globally, and `_GuildLayout.cshtml` loads it again in its `@section Styles`. One is redundant.

**Fix:** Remove the include from `_GuildLayout.cshtml:60` since the parent layout already loads it.

---

### 3. Navbar hamburger icon is identical for mobile and desktop toggles

**File:** `_Navbar.cshtml:14-16` (mobile) and `_Navbar.cshtml:26-28` (desktop)

Both use the same three-line hamburger SVG. No visual cue differentiates "open mobile drawer" from "collapse/expand sidebar."

**Fix:** Use a `<<`/`>>` chevron or sidebar-collapse icon for the desktop toggle (`#sidebarCollapseToggle`). Example: a left-pointing double-chevron when expanded, right-pointing when collapsed.

---

### 4. Sidebar active state detection is fragile

**File:** `_Sidebar.cshtml:15,28,42,54,68,81,101,110,121,131,140,158`

Every link re-derives active state from `ViewContext.RouteData.Values["page"]` with string matching. Doesn't handle nested routes reliably (e.g., `/Admin/Logs/Detail/123` may not match `StartsWith("/Admin/Logs")`).

**Fix:** Set `ViewData["ActiveNavItem"]` from each page model and match against it in the sidebar. Or create a tag helper that handles hierarchical matching.

---

### 5. Sidebar "Support" section is dead weight

**File:** `_Sidebar.cshtml:200-210`

A disabled "Documentation" link with a "Soon" badge. Dead UI creates a sense of incompleteness.

**Fix:** Either remove the section entirely or link to the existing docs site.

---

### 6. Bot status duplicated in navbar and sidebar

**Files:** `_Navbar.cshtml:77-80` (navbar pill), `_Sidebar.cshtml:212-227` (sidebar footer)

Two status indicators for the same thing. The sidebar version is richer (includes version number).

**Fix:** Remove the navbar status pill and keep only the sidebar version. Reclaim the navbar space.

---

### 7. Notification dropdown "Close" button is underwhelming

**File:** `_Navbar.cshtml:124-132`

The footer has a "Close" button where it used to say "View All." The `TODO` on line 124 acknowledges a missing notification center page. Clicking outside already closes the dropdown.

**Fix:** Remove the notification dropdown footer entirely until the notifications page exists.

---

### 8. Missing `<meta>` tags

**File:** `_Layout.cshtml` `<head>`

No `<meta name="description">` or `<meta name="theme-color">`.

**Fix:** Add `<meta name="theme-color" content="#1d2022">` so mobile browser chrome matches the dark theme. Optionally add a description meta tag.

---

### 9. Breadcrumb partial invoked on every page even when empty

**File:** `_Layout.cshtml:54`, `_Breadcrumb.cshtml:3`

When `ViewData["Breadcrumbs"]` is null (most pages), the partial still gets invoked and renders nothing. Wasted partial invocation.

**Fix:** Guard with `@if (ViewData["Breadcrumbs"] != null)` in `_Layout.cshtml` before calling the partial.

---

### 10. Main content has no max-width — ultrawide readability problem

**File:** `_Layout.cshtml:52`

`p-6 lg:p-8` with no width constraint. Content stretches edge-to-edge on ultrawide monitors. The guild layout already constrains to `max-w-7xl`.

**Fix:** Add `max-w-screen-xl mx-auto` (or `max-w-7xl mx-auto`) to the inner content div in `_Layout.cshtml:52`.

---

### 11. Hardcoded color in active sidebar state

**File:** `site.css:1179`

`background-color: rgba(203, 78, 27, 0.12)` hardcodes the accent-orange RGB instead of using `var(--color-accent-orange-muted)`. Won't adapt to the Purple Dusk theme.

**Fix:** Replace with `background-color: var(--color-accent-orange-muted);` (already defined as `rgba(203, 78, 27, 0.2)` in dark theme and `rgba(97, 73, 120, 0.2)` in Purple Dusk).

---

### 12. CDN dependency for SignalR without fallback

**File:** `_Layout.cshtml:68`

SignalR loaded from `cdn.jsdelivr.net`. If the CDN is down, real-time features break silently.

**Fix:** Bundle SignalR locally via npm, or add a `<script>` fallback pattern that loads from a local copy if the CDN fails.

---

## Visual Polish

### 13. Navbar feels flat — no visual separation from content

**File:** `site.css:1113-1118`

Uses `bg-secondary` + `1px border-bottom` + `backdrop-filter: blur(8px)`. On dark theme `bg-secondary` (#262a2d) and `bg-primary` (#1d2022) are close — the border is subtle. The blur does nothing because the navbar is opaque.

**Fix:** Either make the navbar semi-transparent so blur works:
```css
.navbar-redesign {
  background-color: rgba(38, 42, 45, 0.85);
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.3);
}
```
Or keep it opaque but add a shadow for depth:
```css
.navbar-redesign {
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.3);
}
```

---

### 14. Sidebar active indicator is thin and easy to miss

**File:** `site.css:1177-1192`

Active state uses a 3px-wide, 24px-tall orange bar on the left edge with faint `rgba(..., 0.12)` background. The indicator is subtle.

**Fix:** Make active background more noticeable (bump alpha to `0.18`) and make the left bar span the full link height:
```css
.sidebar-link-redesign.active {
  background-color: var(--color-accent-orange-muted);
}

.sidebar-link-redesign.active::before {
  /* Remove fixed height/translateY, use inset instead */
  top: 4px;
  bottom: 4px;
  height: auto;
  transform: none;
}
```

---

### 15. Sidebar section headers barely visible

**File:** `site.css:1195-1202`

At `0.6875rem` (11px) and `color: text-tertiary` (#7a7876), these labels visually disappear.

**Fix:** Bump to `0.75rem`, increase letter-spacing to `0.08em`, and optionally add a structural left accent:
```css
.sidebar-section-header {
  font-size: 0.75rem;
  letter-spacing: 0.08em;
}
```

---

### 16. Sidebar icons have no color differentiation on active state

**File:** `site.css` (no rule exists for active icon color)

Every sidebar icon uses the same `text-secondary` color. When active, only the text and background change — the icon stays muted.

**Fix:** Add accent color to the active icon:
```css
.sidebar-link-redesign.active svg {
  color: var(--color-accent-orange);
}
```

---

### 17. Search bar is visually heavy

**File:** `_Navbar.cshtml:54`

The search input has a visible border and `bg-bg-primary` fill. On the dark navbar, the lighter background creates a bright rectangle that draws disproportionate attention.

**Fix:** Use a subtler recessed style (like VS Code / GitHub):
```css
/* In _Navbar.cshtml, change the input classes: */
/* From: bg-bg-primary border border-border-primary */
/* To:   bg-bg-tertiary border border-transparent */
/* Keep: focus:border-border-focus focus:ring-1 focus:ring-border-focus */
```

---

### 18. Bot status pill in navbar is hard to read

**File:** `_Navbar.cshtml:77-80`, `site.css:1326-1344`

Green text on 15% green background at `0.75rem` (12px) is low contrast.

**Fix:** If keeping (see #6 to remove it), increase font to `0.8125rem` and use higher-contrast coloring:
```css
.bot-status-indicator.online {
  background-color: rgba(16, 185, 129, 0.2);
  color: #34d399; /* lighter green for better contrast */
  font-size: 0.8125rem;
}
```

---

### 19. User avatar dropdown chevron lacks hover feedback

**File:** `_Navbar.cshtml:168-170`

The chevron is static `text-text-secondary`. The parent button has hover styles but the chevron doesn't respond.

**Fix:** Add `group` to the button and `group-hover:text-text-primary` to the chevron:
```html
<button ... class="group flex items-center gap-2 ...">
  ...
  <svg class="... text-text-secondary group-hover:text-text-primary transition-colors">
```

---

### 20. Sidebar footer bot status card feels disconnected

**File:** `_Sidebar.cshtml:212-227`

Uses `bg-bg-primary/50` which is nearly invisible against the sidebar. No visual weight.

**Fix:** Give it more presence:
```html
<div class="bot-status-container p-3 bg-bg-primary rounded-lg border border-border-secondary">
```
And consider a green left-border to echo online status:
```css
.bot-status-container {
  border-left: 2px solid var(--color-success);
}
```

---

### 21. Font stack is system defaults — functional but generic

**File:** `tailwind.config.js:77-88`

Standard system font stack reads as "default Tailwind project."

**Fix:** Consider loading Inter or Geist Sans for headings only via `<link rel="preload">`. Even just heading differentiation gives the UI a more intentional feel:
```css
h1, h2, h3, h4, h5, h6 {
  font-family: 'Inter', var(--font-sans);
}
```

---

### 22. Content padding is generous — pushes content below the fold

**Files:** `_Layout.cshtml:52` (`p-6 lg:p-8`), `_Breadcrumb.cshtml` (`mb-6`)

With sidebar, navbar, content padding, and breadcrumb margin, there's ~112px of vertical space before actual page content on desktop. Combined with the breadcrumb's `mb-6` that's 48px of whitespace above content.

**Fix:** Tighten spacing:
```html
<!-- _Layout.cshtml -->
<div class="p-4 lg:p-6">
```
And reduce breadcrumb bottom margin to `mb-4`.

---

### 23. No transition on sidebar collapse for main content paint optimization

**File:** `site.css:1218-1224`

The sidebar and main content both transition `0.2s ease-out`, which is correct. But the main content repaint during margin-left animation can jank on slower machines.

**Fix:** Add `will-change: margin-left` to `.main-content-redesign` to hint the browser to optimize:
```css
.main-content-redesign {
  will-change: margin-left;
}
```

---

## Summary by Priority

### Quick Wins (minutes each)
| # | Fix | Files |
|---|-----|-------|
| 2 | Remove duplicate `tab-panel.css` | `_GuildLayout.cshtml` |
| 8 | Add `<meta name="theme-color">` | `_Layout.cshtml` |
| 9 | Guard breadcrumb partial invocation | `_Layout.cshtml` |
| 11 | Replace hardcoded sidebar active color | `site.css` |
| 13 | Add navbar shadow for depth | `site.css` |
| 16 | Accent color on active sidebar icon | `site.css` |
| 19 | Chevron hover feedback on avatar | `_Navbar.cshtml` |
| 23 | Add `will-change` to main content | `site.css` |

### Medium Effort (small edits across 1-2 files)
| # | Fix | Files |
|---|-----|-------|
| 3 | Different icon for desktop sidebar toggle | `_Navbar.cshtml` |
| 5 | Remove dead "Support" section | `_Sidebar.cshtml` |
| 6 | Remove duplicate navbar status pill | `_Navbar.cshtml` |
| 7 | Remove notification dropdown footer | `_Navbar.cshtml` |
| 10 | Add max-width to main content | `_Layout.cshtml` |
| 14 | Bolder sidebar active indicator | `site.css` |
| 15 | More visible section headers | `site.css` |
| 17 | Subtler search bar styling | `_Navbar.cshtml` |
| 18 | Better bot status pill contrast | `site.css` |
| 20 | Sidebar footer card visual weight | `_Sidebar.cshtml`, `site.css` |
| 22 | Tighten content padding | `_Layout.cshtml`, `_Breadcrumb.cshtml` |

### Larger Effort (requires planning or build tooling)
| # | Fix | Files |
|---|-----|-------|
| 1 | Bundle JS scripts | `_Layout.cshtml`, build config |
| 4 | Refactor sidebar active state detection | `_Sidebar.cshtml`, page models |
| 12 | Local SignalR fallback | `_Layout.cshtml`, npm/build |
| 21 | Custom heading font | `_Layout.cshtml`, `tailwind.config.js`, `site.css` |
