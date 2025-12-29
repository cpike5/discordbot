# Connected Servers Widget - Design Specification

**Component Type:** Dashboard Widget / Data Table
**Version:** 1.0
**Status:** Active
**Last Updated:** 2025-12-29

---

## Overview

The Connected Servers widget displays a list of Discord servers (guilds) where the bot is currently connected. It provides at-a-glance server information including member counts, status, and command usage statistics in a clean, scannable table format.

**Primary Use Case:** Dashboard overview widget showing key server metrics
**Target Users:** Bot administrators, server moderators
**Location:** Dashboard page (2/3 width column in XL grid)

---

## Component Structure

### HTML Hierarchy

```html
<div class="xl:col-span-2 bg-bg-secondary border border-border-primary rounded-xl overflow-hidden">
  <!-- Card Header -->
  <div class="card-header">
    <h2>Connected Servers</h2>
    <a href="#">View All</a>
  </div>

  <!-- Scrollable Table Container -->
  <div class="overflow-x-auto">
    <table>
      <thead><!-- Column headers --></thead>
      <tbody><!-- Server rows --></tbody>
    </table>
  </div>
</div>
```

**Container:** Card pattern with header and body sections
**Layout:** Full-width table with horizontal scroll support on smaller screens
**Grid Placement:** `xl:col-span-2` (spans 2 columns in XL grid, full width on smaller screens)

---

## Visual Specifications

### Card Container

| Property | Value | Token Reference |
|----------|-------|-----------------|
| Background | `#262a2d` | `bg-bg-secondary` |
| Border | `1px solid #3f4447` | `border border-border-primary` |
| Border Radius | `12px` | `rounded-xl` |
| Overflow | Hidden (for rounded corners) | `overflow-hidden` |

### Card Header

| Property | Value | Token Reference |
|----------|-------|-----------------|
| Layout | Flexbox, space-between alignment | `flex items-center justify-between` |
| Padding | `16px 24px` | `px-6 py-4` |
| Border Bottom | `1px solid #3f4447` | `border-b border-border-primary` |

**Header Title:**
- Font Size: `18px` (`text-lg`)
- Font Weight: `600` (`font-semibold`)
- Color: `#d7d3d0` (`text-text-primary`)

**"View All" Link:**
- Font Size: `14px` (`text-sm`)
- Font Weight: `500` (`font-medium`)
- Color: `#098ecf` (`text-accent-blue`)
- Hover Color: `#0ba3ea` (`hover:text-accent-blue-hover`)
- Transition: `transition-colors` (0.15s ease-in-out)

### Table Structure

**Table Element:**
- Width: `100%` (`w-full`)
- Border Collapse: Collapse

**Table Header (thead):**
- Background: `rgba(29, 32, 34, 0.5)` (`bg-bg-primary/50`)
- Text Alignment: Left
- Padding: `12px 24px` (`px-6 py-3`)

**Header Cell (th):**
- Font Size: `12px` (`text-xs`)
- Font Weight: `600` (`font-semibold`)
- Color: `#a8a5a3` (`text-text-secondary`)
- Text Transform: Uppercase (`uppercase`)
- Letter Spacing: `0.05em` (`tracking-wider`)

**Table Body (tbody):**
- Dividers: `1px solid #3f4447` between rows (`divide-y divide-border-primary`)

**Table Row (tr):**
- Padding: `16px 24px` (`px-6 py-4`)
- Hover Background: `#363a3e` (`hover:bg-bg-hover`)
- Transition: `background-color 0.15s ease-in-out`

### Column Specifications

| Column | Width | Alignment | Content Type |
|--------|-------|-----------|--------------|
| Server | Flexible (auto) | Left | Avatar + Name + ID |
| Members | Fixed width | Left | Numeric count |
| Status | Fixed width | Left | Status badge |
| Commands | Fixed width | Left | Numeric count |
| Actions | Fixed width | Right | Kebab menu button |

---

## Server Avatar Component

### Visual Specifications

**Avatar Container:**
- Size: `40px x 40px` (`w-10 h-10`)
- Shape: Circular (`rounded-full`)
- Display: Flex with centered content (`flex items-center justify-center`)
- Background: Gradient (`bg-gradient-to-br`)
- Text Color: White (`text-white`)

**Avatar Text (Initials):**
- Font Size: `14px` (`text-sm`)
- Font Weight: `700` (`font-bold`)
- Text Transform: Uppercase (2-letter initials)

### Gradient Color Palette

| Gradient | Tailwind Classes |
|----------|------------------|
| Purple to Pink | `from-purple-500 to-pink-500` |
| Blue to Cyan | `from-blue-500 to-cyan-500` |
| Orange to Red | `from-orange-500 to-red-500` |
| Green to Emerald | `from-green-500 to-emerald-500` |
| Indigo to Purple | `from-indigo-500 to-purple-500` |
| Yellow to Orange | `from-yellow-500 to-orange-500` |

**Selection Logic:** Hash server ID modulo gradient count for deterministic assignment.

---

## Status Badge Component

### Status Variants

#### Online Status
- Text Color: `#10b981` (`text-success`)
- Background: `rgba(16, 185, 129, 0.1)` (`bg-success/10`)
- Dot Color: `#10b981` (`bg-success`)
- Label: "Online"

#### Idle Status
- Text Color: `#f59e0b` (`text-warning`)
- Background: `rgba(245, 158, 11, 0.1)` (`bg-warning/10`)
- Dot Color: `#f59e0b` (`bg-warning`)
- Label: "Idle"

#### Offline Status
- Text Color: `#7a7876` (`text-text-tertiary`)
- Background: `rgba(63, 68, 71, 0.5)` (`bg-border-primary/50`)
- Dot Color: `#7a7876` (`bg-text-tertiary`)
- Label: "Offline"

### Badge Structure
```html
<span class="inline-flex items-center gap-1.5 px-2.5 py-1 text-xs font-semibold rounded-full">
  <span class="w-1.5 h-1.5 rounded-full"></span>
  Status Text
</span>
```

---

## Actions Menu (Kebab Menu)

**Button Element:**
- Padding: `8px` (`p-2`)
- Icon Size: `20px x 20px` (`w-5 h-5`)
- Border Radius: `8px` (`rounded-lg`)

**Color States:**
- Default: `#a8a5a3` (`text-text-secondary`)
- Hover Text: `#d7d3d0` (`hover:text-text-primary`)
- Hover Background: `#363a3e` (`hover:bg-bg-hover`)

**Accessibility:**
- ARIA Label: `aria-label="Server settings"`
- Focus: Blue outline on `:focus-visible`

---

## Responsive Behavior

### Desktop (1280px+)
- Card spans 2 columns in 3-column grid (`xl:col-span-2`)
- Full table view with all columns

### Tablet (768px - 1279px)
- Card spans full width
- Horizontal scrollable table

### Mobile (< 768px)
- Consider card-based layout instead of table
- Stack server info vertically

---

## Accessibility Requirements

- Table uses semantic `<table>`, `<thead>`, `<tbody>`, `<th>`, `<td>` elements
- Column headers use `<th scope="col">`
- Kebab menu buttons include descriptive `aria-label`
- Status communicated through text, not just color
- Focus indicators visible on all interactive elements
- Color contrast meets WCAG 2.1 AA standards

---

## Design Tokens Reference

### Colors Used
| Token | Value | Usage |
|-------|-------|-------|
| `bg-bg-secondary` | `#262a2d` | Card background |
| `bg-bg-primary` | `#1d2022` | Table header (50% opacity) |
| `bg-bg-hover` | `#363a3e` | Row/button hover |
| `border-border-primary` | `#3f4447` | Borders, dividers |
| `text-text-primary` | `#d7d3d0` | Server name, header title |
| `text-text-secondary` | `#a8a5a3` | Column headers, numeric data |
| `text-text-tertiary` | `#7a7876` | Server ID |
| `accent-blue` | `#098ecf` | "View All" link |
| `success` | `#10b981` | Online status |
| `warning` | `#f59e0b` | Idle status |

### Spacing
| Token | Value | Usage |
|-------|-------|-------|
| `px-6` | `24px` | Card header, table cell horizontal padding |
| `py-4` | `16px` | Card header, table cell vertical padding |
| `py-3` | `12px` | Table header vertical padding |
| `gap-3` | `12px` | Avatar + server name gap |

---

## Changelog

### Version 1.0 (2025-12-29)
- Initial design specification created
- Documented component structure and visual specifications
- Defined server avatar gradient palette
- Specified status badge variants
- Documented responsive behavior patterns
- Added accessibility requirements
