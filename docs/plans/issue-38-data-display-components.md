# Issue #38: Data Display Components Prototype - Implementation Plan

**Version:** 1.0
**Created:** 2025-12-07
**Status:** Planning
**Target Framework:** .NET Blazor / HTML/CSS Prototypes

---

## 1. Architecture Overview

### Purpose

This implementation plan defines the creation of 10 reusable data display components for the Discord Bot Admin UI. These components will handle presenting information in tables, cards, lists, badges, and other visual formats with sorting, filtering, pagination, and responsive behaviors.

### Design System Alignment

All components will implement patterns defined in `docs/design-system.md`, specifically:
- **Cards:** Lines 545-622 (card variants, header/body/footer structure)
- **Tables:** Lines 1125-1252 (table structure, variants, row hover states)
- **Status Indicators & Badges:** Lines 1256-1364 (colors, sizing, status dots)
- **Color palette, typography, and spacing tokens** from the core design system

### Component Architecture Principles

1. **Blazor Component Model**: All components will be implemented as Razor components (.razor files)
2. **CSS Isolation**: Component-specific styles in .razor.css files
3. **Parameter-Driven**: Configurable via Blazor parameters
4. **Event Callbacks**: Expose events for sorting, selection, pagination changes
5. **Accessibility First**: WCAG 2.1 AA compliance with proper ARIA attributes
6. **Responsive Design**: Mobile-first with progressive enhancement

---

## 2. Component Groupings for Parallel Development

Components are organized into 4 development groups based on dependencies and complexity:

### Group A: Foundational Primitives (No Dependencies)
**Parallel development enabled - these have no inter-component dependencies**

| Component | Description | Complexity |
|-----------|-------------|------------|
| Badge | Colors, sizes, variants | Low |
| Status Indicator | Dot, colors, pulsing animation | Low |
| Avatar | Image, initials, sizes, status overlay | Medium |
| Loading States | Spinner, skeleton loaders, progress bar | Medium |

### Group B: Container Components (Depends on Group A)
**Can begin after Badge, Status Indicator, and Avatar are complete**

| Component | Description | Complexity |
|-----------|-------------|------------|
| Card | Header, body, footer, variants | Medium |
| Stat Card | Icon, value, trend indicator (uses Badge) | Medium |

### Group C: List-Based Components (Depends on Groups A & B)
**Can begin after Avatar and Card are complete**

| Component | Description | Complexity |
|-----------|-------------|------------|
| List Components | With icons, avatars, actions | Medium |
| Pagination | Page numbers, navigation, items per page | Medium |

### Group D: Complex Data Components (Depends on Groups A, B, C)
**Requires all foundational components**

| Component | Description | Complexity |
|-----------|-------------|------------|
| Data Table | Sorting, selection, expandable rows | High |
| Responsive Table | Mobile card layout, sticky header | High |

---

## 3. File Structure and Naming Conventions

### Directory Structure

```
src/DiscordBot.Bot/
+-- Components/
|   +-- DataDisplay/
|   |   +-- _Imports.razor              # Shared imports for data display components
|   |   |
|   |   +-- Primitives/                 # Group A: Foundational components
|   |   |   +-- Badge.razor
|   |   |   +-- Badge.razor.css
|   |   |   +-- StatusIndicator.razor
|   |   |   +-- StatusIndicator.razor.css
|   |   |   +-- Avatar.razor
|   |   |   +-- Avatar.razor.css
|   |   |   +-- Spinner.razor
|   |   |   +-- Spinner.razor.css
|   |   |   +-- SkeletonLoader.razor
|   |   |   +-- SkeletonLoader.razor.css
|   |   |   +-- ProgressBar.razor
|   |   |   +-- ProgressBar.razor.css
|   |   |
|   |   +-- Cards/                      # Group B: Container components
|   |   |   +-- Card.razor
|   |   |   +-- Card.razor.css
|   |   |   +-- CardHeader.razor
|   |   |   +-- CardBody.razor
|   |   |   +-- CardFooter.razor
|   |   |   +-- StatCard.razor
|   |   |   +-- StatCard.razor.css
|   |   |
|   |   +-- Lists/                      # Group C: List components
|   |   |   +-- ListGroup.razor
|   |   |   +-- ListGroup.razor.css
|   |   |   +-- ListItem.razor
|   |   |   +-- ListItem.razor.css
|   |   |   +-- Pagination.razor
|   |   |   +-- Pagination.razor.css
|   |   |
|   |   +-- Tables/                     # Group D: Table components
|   |       +-- DataTable.razor
|   |       +-- DataTable.razor.css
|   |       +-- DataTableColumn.razor
|   |       +-- DataTableRow.razor
|   |       +-- TableHeader.razor
|   |       +-- ResponsiveTable.razor
|   |       +-- ResponsiveTable.razor.css
|   |
|   +-- Shared/                         # Shared utilities
|       +-- Enums/
|       |   +-- BadgeColor.cs
|       |   +-- BadgeSize.cs
|       |   +-- StatusType.cs
|       |   +-- AvatarSize.cs
|       |   +-- CardVariant.cs
|       |   +-- SortDirection.cs
|       +-- Models/
|       |   +-- PaginationState.cs
|       |   +-- SortState.cs
|       |   +-- TableColumn.cs
|       +-- Extensions/
|           +-- CssClassBuilder.cs
|
docs/
+-- prototypes/
    +-- components/
        +-- data-display/
            +-- primitives.html         # Badge, Status, Avatar, Loading prototypes
            +-- cards.html              # Card and StatCard prototypes
            +-- lists.html              # List and Pagination prototypes
            +-- tables.html             # DataTable and ResponsiveTable prototypes
            +-- showcase.html           # All components integrated showcase
```

### Naming Conventions

| Item | Convention | Example |
|------|------------|---------|
| Component files | PascalCase | `DataTable.razor` |
| CSS files | Match component | `DataTable.razor.css` |
| Enum files | PascalCase | `BadgeColor.cs` |
| Model files | PascalCase | `PaginationState.cs` |
| Parameters | PascalCase | `@Color`, `@Size`, `@OnClick` |
| CSS classes | kebab-case | `status-indicator`, `card-header` |
| CSS variables | kebab-case with prefix | `--dd-badge-padding` |

---

## 4. Implementation Order with Dependencies

### Phase 1: Shared Utilities (Week 1, Days 1-2)

**Prerequisites:** None

| Task | Owner | Deliverables |
|------|-------|--------------|
| Create enum definitions | dotnet-specialist | `BadgeColor.cs`, `BadgeSize.cs`, `StatusType.cs`, `AvatarSize.cs`, `CardVariant.cs`, `SortDirection.cs` |
| Create model classes | dotnet-specialist | `PaginationState.cs`, `SortState.cs`, `TableColumn.cs` |
| Create CSS class builder | dotnet-specialist | `CssClassBuilder.cs` |
| Create shared imports | dotnet-specialist | `_Imports.razor` |

### Phase 2: Group A - Foundational Primitives (Week 1, Days 2-5)

**Prerequisites:** Phase 1 complete

**Parallel Track 1:**
| Task | Owner | Deliverables |
|------|-------|--------------|
| Badge component | dotnet-specialist | `Badge.razor`, `Badge.razor.css` |
| Badge prototype | html-prototyper | Badge section in `primitives.html` |
| Badge documentation | docs-writer | Badge API documentation |

**Parallel Track 2:**
| Task | Owner | Deliverables |
|------|-------|--------------|
| Status Indicator component | dotnet-specialist | `StatusIndicator.razor`, `StatusIndicator.razor.css` |
| Status Indicator prototype | html-prototyper | Status section in `primitives.html` |
| Status Indicator documentation | docs-writer | Status Indicator API documentation |

**Parallel Track 3:**
| Task | Owner | Deliverables |
|------|-------|--------------|
| Avatar component | dotnet-specialist | `Avatar.razor`, `Avatar.razor.css` |
| Avatar prototype | html-prototyper | Avatar section in `primitives.html` |
| Avatar documentation | docs-writer | Avatar API documentation |

**Parallel Track 4:**
| Task | Owner | Deliverables |
|------|-------|--------------|
| Loading components | dotnet-specialist | `Spinner.razor`, `SkeletonLoader.razor`, `ProgressBar.razor` |
| Loading prototype | html-prototyper | Loading section in `primitives.html` |
| Loading documentation | docs-writer | Loading components API documentation |

### Phase 3: Group B - Container Components (Week 2, Days 1-3)

**Prerequisites:** Phase 2 complete (Badge, StatusIndicator available)

**Parallel Track 1:**
| Task | Owner | Deliverables |
|------|-------|--------------|
| Card components | dotnet-specialist | `Card.razor`, `CardHeader.razor`, `CardBody.razor`, `CardFooter.razor` |
| Card prototype | html-prototyper | `cards.html` |
| Card documentation | docs-writer | Card API documentation |

**Parallel Track 2:**
| Task | Owner | Deliverables |
|------|-------|--------------|
| StatCard component | dotnet-specialist | `StatCard.razor`, `StatCard.razor.css` |
| StatCard prototype | html-prototyper | StatCard section in `cards.html` |
| StatCard documentation | docs-writer | StatCard API documentation |

### Phase 4: Group C - List Components (Week 2, Days 3-5)

**Prerequisites:** Phase 3 complete (Card, Avatar available)

**Parallel Track 1:**
| Task | Owner | Deliverables |
|------|-------|--------------|
| List components | dotnet-specialist | `ListGroup.razor`, `ListItem.razor` |
| List prototype | html-prototyper | `lists.html` |
| List documentation | docs-writer | List components API documentation |

**Parallel Track 2:**
| Task | Owner | Deliverables |
|------|-------|--------------|
| Pagination component | dotnet-specialist | `Pagination.razor`, `Pagination.razor.css` |
| Pagination prototype | html-prototyper | Pagination section in `lists.html` |
| Pagination documentation | docs-writer | Pagination API documentation |

### Phase 5: Group D - Table Components (Week 3, Days 1-5)

**Prerequisites:** All previous phases complete

**Sequential Track (High complexity requires sequential work):**

| Day | Task | Owner | Deliverables |
|-----|------|-------|--------------|
| 1-2 | DataTable core structure | dotnet-specialist | `DataTable.razor`, `DataTableColumn.razor`, `DataTableRow.razor` |
| 1-2 | DataTable prototype | html-prototyper | Base table in `tables.html` |
| 3 | Sorting functionality | dotnet-specialist | Sorting logic, header click handlers |
| 3 | Sorting prototype | html-prototyper | Sorting UI in `tables.html` |
| 4 | Selection & expandable rows | dotnet-specialist | Checkbox selection, row expansion |
| 4 | Selection prototype | html-prototyper | Selection UI in `tables.html` |
| 5 | ResponsiveTable component | dotnet-specialist | `ResponsiveTable.razor`, mobile card layout |
| 5 | Responsive prototype | html-prototyper | Mobile layout in `tables.html` |

### Phase 6: Integration & Showcase (Week 4, Day 1-2)

**Prerequisites:** All components complete

| Task | Owner | Deliverables |
|------|-------|--------------|
| Showcase prototype | html-prototyper | `showcase.html` - all components integrated |
| Component library docs | docs-writer | Complete API reference, usage examples |
| Integration testing | dotnet-specialist | Component integration tests |
| Design review | design-specialist | Design QA, accessibility audit |

---

## 5. Prototype Specifications

### 5.1 Primitives Prototype (`docs/prototypes/components/data-display/primitives.html`)

**Sections to include:**

1. **Badge Section**
   - All color variants: orange, blue, gray, success, warning, error
   - Size variants: sm, md, lg
   - With/without icons
   - Pill and rounded variants

2. **Status Indicator Section**
   - Status types: online, idle, busy, offline
   - With/without text label
   - Pulsing animation variant
   - Dot-only variant

3. **Avatar Section**
   - Image avatar (various sizes: xs, sm, md, lg, xl)
   - Initials avatar (1-2 characters)
   - With status overlay (corner badge)
   - Avatar group/stack
   - Placeholder/loading state

4. **Loading States Section**
   - Spinner (sizes: sm, md, lg)
   - Skeleton loaders (text line, avatar, card, table row)
   - Progress bar (determinate, indeterminate)
   - Progress with percentage label

### 5.2 Cards Prototype (`docs/prototypes/components/data-display/cards.html`)

**Sections to include:**

1. **Base Card Section**
   - Standard card with header, body, footer
   - Header-only card
   - Body-only card
   - Card without border (flat variant)
   - Elevated card variant
   - Interactive card (hover state)

2. **Stat Card Section**
   - Stat with icon, value, and label
   - Stat with trend indicator (up/down arrow, percentage)
   - Stat with sparkline placeholder
   - Compact stat card
   - Grid of stat cards (dashboard layout)

### 5.3 Lists Prototype (`docs/prototypes/components/data-display/lists.html`)

**Sections to include:**

1. **List Group Section**
   - Basic list with text items
   - List with leading icons
   - List with leading avatars
   - List with trailing actions (buttons, badges)
   - List with dividers
   - Selectable list items
   - List item hover states
   - Nested/indented list items

2. **Pagination Section**
   - Full pagination (prev, pages, next)
   - Simple pagination (prev/next only)
   - Pagination with page size selector
   - Pagination with "showing X of Y" text
   - Compact pagination (mobile)
   - Disabled states

### 5.4 Tables Prototype (`docs/prototypes/components/data-display/tables.html`)

**Sections to include:**

1. **Data Table Section**
   - Basic table with headers and rows
   - Table with sortable columns (sort icons)
   - Table with row selection (checkboxes)
   - Table with expandable rows
   - Table with inline actions
   - Table with row hover states
   - Striped table variant
   - Compact table variant
   - Bordered table variant
   - Empty state
   - Loading state (skeleton rows)

2. **Responsive Table Section**
   - Desktop view (standard table)
   - Tablet view (horizontal scroll)
   - Mobile view (card layout transformation)
   - Sticky header behavior
   - Sticky first column behavior

### 5.5 Showcase Prototype (`docs/prototypes/components/data-display/showcase.html`)

**Full integration demonstrating:**

1. Dashboard-style layout with stat cards
2. Server list table with avatars, badges, status indicators
3. Activity log list with timestamps
4. Pagination for table
5. Loading states during data fetch simulation
6. Responsive behavior at all breakpoints

---

## 6. Shared Utilities Specification

### 6.1 Enums

```csharp
// BadgeColor.cs
public enum BadgeColor
{
    Orange,     // Primary brand - Admin roles
    Blue,       // Secondary - Moderator roles
    Gray,       // Neutral - Member roles
    Success,    // Green - Active states
    Warning,    // Amber - Pending states
    Error       // Red - Banned/Error states
}

// BadgeSize.cs
public enum BadgeSize
{
    Small,      // 10px font, 4px/8px padding
    Medium,     // 12px font, 4px/12px padding (default)
    Large       // 14px font, 6px/16px padding
}

// StatusType.cs
public enum StatusType
{
    Online,     // Green - #10b981
    Idle,       // Amber - #f59e0b
    Busy,       // Red - #ef4444
    Offline     // Gray - #7a7876
}

// AvatarSize.cs
public enum AvatarSize
{
    ExtraSmall, // 24px
    Small,      // 32px
    Medium,     // 40px (default)
    Large,      // 48px
    ExtraLarge  // 64px
}

// CardVariant.cs
public enum CardVariant
{
    Default,    // Standard card
    Elevated,   // With shadow
    Interactive, // Hover effects
    Flat        // No border
}

// SortDirection.cs
public enum SortDirection
{
    None,
    Ascending,
    Descending
}
```

### 6.2 Models

```csharp
// PaginationState.cs
public class PaginationState
{
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalItems { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);
    public bool HasPrevious => CurrentPage > 1;
    public bool HasNext => CurrentPage < TotalPages;
}

// SortState.cs
public class SortState
{
    public string? ColumnKey { get; set; }
    public SortDirection Direction { get; set; } = SortDirection.None;
}

// TableColumn.cs
public class TableColumn<TItem>
{
    public string Key { get; set; } = string.Empty;
    public string Header { get; set; } = string.Empty;
    public Func<TItem, object>? ValueSelector { get; set; }
    public bool Sortable { get; set; }
    public string? Width { get; set; }
    public string? CssClass { get; set; }
    public RenderFragment<TItem>? Template { get; set; }
}
```

### 6.3 CSS Class Builder

```csharp
// CssClassBuilder.cs
public class CssClassBuilder
{
    private readonly List<string> _classes = new();

    public CssClassBuilder Add(string className)
    {
        if (!string.IsNullOrWhiteSpace(className))
            _classes.Add(className);
        return this;
    }

    public CssClassBuilder AddIf(string className, bool condition)
    {
        if (condition && !string.IsNullOrWhiteSpace(className))
            _classes.Add(className);
        return this;
    }

    public string Build() => string.Join(" ", _classes);

    public override string ToString() => Build();
}
```

---

## 7. Component API Specifications

### 7.1 Badge Component

```razor
@* Badge.razor *@
<span class="@CssClass" @attributes="AdditionalAttributes">
    @if (IconContent != null)
    {
        @IconContent
    }
    @ChildContent
</span>

@code {
    [Parameter] public BadgeColor Color { get; set; } = BadgeColor.Gray;
    [Parameter] public BadgeSize Size { get; set; } = BadgeSize.Medium;
    [Parameter] public bool Pill { get; set; } = true;
    [Parameter] public RenderFragment? IconContent { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }
}
```

### 7.2 Status Indicator Component

```razor
@* StatusIndicator.razor *@
<span class="@CssClass" @attributes="AdditionalAttributes">
    <span class="status-dot @DotClass"></span>
    @if (ShowLabel)
    {
        <span class="status-label">@Label</span>
    }
</span>

@code {
    [Parameter] public StatusType Status { get; set; } = StatusType.Offline;
    [Parameter] public bool ShowLabel { get; set; } = true;
    [Parameter] public string? Label { get; set; }
    [Parameter] public bool Pulse { get; set; }
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }
}
```

### 7.3 Avatar Component

```razor
@* Avatar.razor *@
<div class="@CssClass" @attributes="AdditionalAttributes">
    @if (!string.IsNullOrEmpty(ImageUrl))
    {
        <img src="@ImageUrl" alt="@Alt" class="avatar-image" />
    }
    else
    {
        <span class="avatar-initials">@Initials</span>
    }
    @if (Status.HasValue)
    {
        <StatusIndicator Status="@Status.Value" ShowLabel="false" class="avatar-status" />
    }
</div>

@code {
    [Parameter] public string? ImageUrl { get; set; }
    [Parameter] public string? Alt { get; set; }
    [Parameter] public string? Initials { get; set; }
    [Parameter] public AvatarSize Size { get; set; } = AvatarSize.Medium;
    [Parameter] public StatusType? Status { get; set; }
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }
}
```

### 7.4 DataTable Component

```razor
@* DataTable.razor *@
@typeparam TItem

<div class="table-container @ContainerClass">
    @if (IsLoading)
    {
        <SkeletonLoader Type="SkeletonType.Table" Rows="@PageSize" />
    }
    else if (!Items.Any())
    {
        @EmptyContent
    }
    else
    {
        <table class="table @TableClass">
            <thead class="table-header">
                <tr>
                    @if (Selectable)
                    {
                        <th class="table-cell-header table-cell-checkbox">
                            <input type="checkbox" @onchange="OnSelectAllChanged" />
                        </th>
                    }
                    @foreach (var column in Columns)
                    {
                        <TableHeader Column="@column"
                                     SortState="@SortState"
                                     OnSort="@HandleSort" />
                    }
                </tr>
            </thead>
            <tbody class="table-body">
                @foreach (var item in Items)
                {
                    <DataTableRow TItem="TItem"
                                  Item="@item"
                                  Columns="@Columns"
                                  Selectable="@Selectable"
                                  Expandable="@Expandable"
                                  IsSelected="@IsItemSelected(item)"
                                  OnSelectionChanged="@HandleSelectionChanged"
                                  ExpandedContent="@ExpandedRowContent" />
                }
            </tbody>
        </table>
    }
</div>

@if (ShowPagination && Pagination != null)
{
    <Pagination State="@Pagination" OnPageChanged="@HandlePageChanged" />
}

@code {
    [Parameter] public IEnumerable<TItem> Items { get; set; } = Enumerable.Empty<TItem>();
    [Parameter] public List<TableColumn<TItem>> Columns { get; set; } = new();
    [Parameter] public bool IsLoading { get; set; }
    [Parameter] public bool Selectable { get; set; }
    [Parameter] public bool Expandable { get; set; }
    [Parameter] public bool Striped { get; set; }
    [Parameter] public bool Compact { get; set; }
    [Parameter] public bool Bordered { get; set; }
    [Parameter] public PaginationState? Pagination { get; set; }
    [Parameter] public bool ShowPagination { get; set; }
    [Parameter] public RenderFragment? EmptyContent { get; set; }
    [Parameter] public RenderFragment<TItem>? ExpandedRowContent { get; set; }
    [Parameter] public EventCallback<SortState> OnSort { get; set; }
    [Parameter] public EventCallback<HashSet<TItem>> OnSelectionChanged { get; set; }
    [Parameter] public EventCallback<PaginationState> OnPageChanged { get; set; }
}
```

---

## 8. Subagent Task Assignments

### design-specialist

**Deliverables:**
1. Review and validate component designs against design system
2. Provide design specifications for components not fully defined in design-system.md:
   - Loading states (spinner, skeleton, progress bar)
   - Avatar with status overlay positioning
   - Stat card trend indicator styles
   - Pagination control styling
   - Expandable row animation/transition
3. Accessibility audit for all prototype HTML
4. Responsive breakpoint recommendations for table-to-card transformation
5. Animation/transition specifications for interactive states

### html-prototyper

**Deliverables:**
1. `docs/prototypes/components/data-display/primitives.html` - Badge, Status, Avatar, Loading
2. `docs/prototypes/components/data-display/cards.html` - Card variants, StatCard
3. `docs/prototypes/components/data-display/lists.html` - List components, Pagination
4. `docs/prototypes/components/data-display/tables.html` - DataTable, ResponsiveTable
5. `docs/prototypes/components/data-display/showcase.html` - Integrated demo

**Requirements:**
- Use Tailwind CSS CDN with design system token configuration (copy from `dashboard.html`)
- Include all variants and states for each component
- Add responsive views (desktop, tablet, mobile)
- Include interactive states via CSS (:hover, :focus, :active)
- Add accessibility attributes (ARIA labels, roles)

### dotnet-specialist

**Deliverables:**
1. Shared utilities: enums, models, CssClassBuilder
2. Group A components: Badge, StatusIndicator, Avatar, Spinner, SkeletonLoader, ProgressBar
3. Group B components: Card, CardHeader, CardBody, CardFooter, StatCard
4. Group C components: ListGroup, ListItem, Pagination
5. Group D components: DataTable, DataTableColumn, DataTableRow, TableHeader, ResponsiveTable
6. Unit tests for all components
7. Component CSS isolation files

**Requirements:**
- Follow Blazor component best practices
- Implement proper parameter validation
- Use EventCallback for all user interactions
- Support RenderFragment for customizable content
- Include XML documentation comments

### docs-writer

**Deliverables:**
1. Component API reference documentation
2. Usage examples for each component
3. Props/parameters table for each component
4. Accessibility guidelines
5. Responsive behavior documentation
6. Migration guide from prototype to Blazor component

---

## 9. Testing Strategy

### Unit Testing (dotnet-specialist)

| Component | Test Cases |
|-----------|------------|
| Badge | Color rendering, size classes, icon slot, pill variant |
| StatusIndicator | Status colors, pulse animation class, label visibility |
| Avatar | Image vs initials, size classes, status overlay |
| Loading | Spinner sizes, skeleton variants, progress percentage |
| Card | Variant classes, child content rendering, slot population |
| StatCard | Value formatting, trend direction, icon rendering |
| ListGroup | Item rendering, dividers, selection state |
| Pagination | Page calculation, navigation, disabled states |
| DataTable | Sorting, selection, expansion, empty/loading states |
| ResponsiveTable | Breakpoint detection, layout transformation |

### Visual Testing (html-prototyper)

1. Cross-browser testing (Chrome, Firefox, Safari, Edge)
2. Responsive breakpoint testing (320px, 768px, 1024px, 1280px)
3. Dark theme consistency validation
4. Animation performance testing

### Accessibility Testing (design-specialist)

1. Keyboard navigation for all interactive components
2. Screen reader compatibility (NVDA, VoiceOver)
3. Color contrast verification
4. Focus indicator visibility
5. ARIA attribute validation

---

## 10. Acceptance Criteria

### Group A: Foundational Primitives

- [ ] Badge renders all 6 color variants correctly
- [ ] Badge renders all 3 size variants correctly
- [ ] StatusIndicator shows correct color for each status type
- [ ] StatusIndicator pulse animation works when enabled
- [ ] Avatar displays image when URL provided
- [ ] Avatar displays initials when no image URL
- [ ] Avatar shows status overlay in correct position
- [ ] Spinner renders at all 3 sizes
- [ ] SkeletonLoader animates with shimmer effect
- [ ] ProgressBar shows percentage when determinate

### Group B: Container Components

- [ ] Card renders with header, body, and footer slots
- [ ] Card supports all 4 variants (default, elevated, interactive, flat)
- [ ] Interactive card shows hover state
- [ ] StatCard displays value with proper formatting
- [ ] StatCard shows trend indicator with correct color (green up, red down)
- [ ] StatCard icon renders in correct position

### Group C: List Components

- [ ] ListGroup renders items with proper spacing
- [ ] ListItem supports leading icons/avatars
- [ ] ListItem supports trailing actions
- [ ] Selectable list items show selection state
- [ ] Pagination calculates total pages correctly
- [ ] Pagination disables prev on first page
- [ ] Pagination disables next on last page
- [ ] Page size selector updates items per page

### Group D: Table Components

- [ ] DataTable renders columns and rows correctly
- [ ] Sortable columns show sort indicator
- [ ] Clicking sortable header triggers OnSort callback
- [ ] Selectable rows show checkbox column
- [ ] Select all checkbox selects/deselects all rows
- [ ] Expandable rows show expand/collapse toggle
- [ ] Expanded row displays ExpandedRowContent
- [ ] Empty state renders EmptyContent
- [ ] Loading state shows skeleton rows
- [ ] ResponsiveTable transforms to card layout on mobile

### Cross-Cutting

- [ ] All components meet WCAG 2.1 AA standards
- [ ] All components work with keyboard navigation
- [ ] All components use design system tokens
- [ ] All prototype HTML passes W3C validation
- [ ] All Blazor components have XML documentation

---

## 11. Risks and Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Design system gaps for new components | Medium | Medium | Design-specialist to provide specs before implementation |
| Complex table sorting logic | Medium | High | Start with simple single-column sort, add multi-column later |
| Responsive table performance | Medium | Medium | Use CSS-only transformation where possible, JS as fallback |
| Component interdependencies | Low | High | Strict phase ordering, clear interfaces between components |
| Browser compatibility issues | Low | Medium | Test early with all target browsers, document known issues |
| Accessibility compliance gaps | Medium | High | Include accessibility testing in each phase, not just final |

---

## 12. Timeline Summary

| Phase | Duration | Components |
|-------|----------|------------|
| Phase 1: Shared Utilities | 2 days | Enums, Models, CssClassBuilder |
| Phase 2: Foundational Primitives | 4 days | Badge, StatusIndicator, Avatar, Loading |
| Phase 3: Container Components | 3 days | Card, StatCard |
| Phase 4: List Components | 3 days | ListGroup, ListItem, Pagination |
| Phase 5: Table Components | 5 days | DataTable, ResponsiveTable |
| Phase 6: Integration | 2 days | Showcase, Documentation, Testing |
| **Total** | **19 days (~4 weeks)** | **10 components** |

---

## Appendix A: Design System Token Reference

### Colors (from design-system.md)

```css
/* Backgrounds */
--color-bg-primary: #1d2022;
--color-bg-secondary: #262a2d;
--color-bg-tertiary: #2f3336;
--color-bg-hover: #363a3e;

/* Text */
--color-text-primary: #d7d3d0;
--color-text-secondary: #a8a5a3;
--color-text-tertiary: #7a7876;

/* Accents */
--color-accent-orange: #cb4e1b;
--color-accent-blue: #098ecf;

/* Semantic */
--color-success: #10b981;
--color-warning: #f59e0b;
--color-error: #ef4444;
--color-info: #06b6d4;

/* Borders */
--color-border-primary: #3f4447;
--color-border-secondary: #2f3336;
```

### Spacing Scale

```css
--spacing-1: 0.25rem;   /* 4px */
--spacing-2: 0.5rem;    /* 8px */
--spacing-3: 0.75rem;   /* 12px */
--spacing-4: 1rem;      /* 16px */
--spacing-5: 1.25rem;   /* 20px */
--spacing-6: 1.5rem;    /* 24px */
--spacing-8: 2rem;      /* 32px */
```

### Border Radius

```css
--radius-sm: 0.25rem;   /* 4px */
--radius-md: 0.375rem;  /* 6px */
--radius-lg: 0.5rem;    /* 8px */
--radius-full: 9999px;  /* Pill */
```

---

## Appendix B: Existing Prototype Reference

The `docs/prototypes/dashboard.html` file contains working examples of:
- Stat cards with trend indicators
- Server list table with avatars and status badges
- Status indicator with pulse animation
- Tailwind configuration with design system tokens

Use this as reference for consistent styling.

---

*Document Version: 1.0*
*Created: 2025-12-07*
*Author: Systems Architect*
