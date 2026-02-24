# Layout Polish — Phased Implementation

Phases for implementing the 23 fixes from `layout-polish-plan.md`.

## Phase 1: Zero-Risk Quick Wins (CSS-only + markup cleanup)

No behavior changes, purely additive or removing dead code.

| # | Fix | Files |
|---|-----|-------|
| 2 | Remove duplicate `tab-panel.css` include | `_GuildLayout.cshtml` |
| 5 | Remove dead "Support" section | `_Sidebar.cshtml` |
| 7 | Remove notification dropdown footer | `_Navbar.cshtml` |
| 8 | Add `<meta name="theme-color">` | `_Layout.cshtml` |
| 9 | Guard breadcrumb partial invocation | `_Layout.cshtml` |
| 11 | Replace hardcoded sidebar active color with CSS var | `site.css` |
| 16 | Accent color on active sidebar icon | `site.css` |
| 23 | Add `will-change` to main content | `site.css` |

## Phase 2: Visual Polish (CSS + minor markup)

Styling changes that improve appearance but don't alter structure or behavior.

| # | Fix | Files |
|---|-----|-------|
| 13 | Navbar shadow for depth | `site.css` |
| 14 | Bolder sidebar active indicator | `site.css` |
| 15 | More visible section headers | `site.css` |
| 17 | Subtler search bar styling | `_Navbar.cshtml` |
| 19 | Chevron hover feedback on avatar | `_Navbar.cshtml` |
| 20 | Sidebar footer card visual weight | `_Sidebar.cshtml`, `site.css` |
| 22 | Tighten content padding | `_Layout.cshtml`, `_Breadcrumb.cshtml` |

## Phase 3: Structural Improvements (behavior changes)

These change how things work, so they need more care and visual QA.

| # | Fix | Files |
|---|-----|-------|
| 3 | Different icon for desktop sidebar toggle | `_Navbar.cshtml` |
| 6 | Remove navbar bot status pill (keep sidebar one) | `_Navbar.cshtml` |
| 10 | Add max-width to main content | `_Layout.cshtml` |
| 18 | Better bot status pill contrast (only if #6 keeps pill) | `site.css` |

## Phase 4: Deferred / Needs Discussion

Requires build tooling changes or architectural decisions. Each can be a separate PR.

| # | Fix | Notes |
|---|-----|-------|
| 1 | Bundle JS scripts | Needs build pipeline (webpack/esbuild/vite) |
| 4 | Refactor sidebar active state detection | Touches every page model or needs tag helper |
| 12 | Local SignalR fallback | Needs npm + fallback script pattern |
| 21 | Custom heading font | Needs font loading strategy, performance tradeoff |
