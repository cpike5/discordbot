# Blazor Component Library

Conventions for the design-system component library being built at
`src/DiscordBot.Bot/Blazor/Shared/` — Phase 2 of the Blazor port
(`docs/plans/blazor-port-plan.md` §4.6, §5 "Phase 2"). Every component in this library derives
from a shipped Graphite v2 Razor Pages partial in `Pages/Shared/Components/_*.cshtml` (view
models in `ViewModels/Components/`); ground truth for markup, classes, ARIA attributes and
variants is always the partial and the `@layer components` classes in `wwwroot/css/site.css`,
never the (pre-v2, stale) samples in §4 of `design-system.md`.

See "Blazor Components" in `docs/architecture/patterns.md` for the hosting model, render-mode
rules, and auth-in-circuits — this page is about the component library specifically.

## Component contract

Every component in this library follows the same shape. Read this section before adding one.

1. **Location and namespace.** Files live in `src/DiscordBot.Bot/Blazor/Shared/<Group>/<Name>.razor`
   (+ an optional `<Name>.razor.cs` code-behind partial, + an optional `<Name>.razor.css`
   isolation file), and every `.razor` file declares `@namespace DiscordBot.Bot.Blazor.Shared`
   explicitly at the top — regardless of which `<Group>` subfolder it lives in. That one
   namespace, already covered by a single `@using DiscordBot.Bot.Blazor.Shared` in
   `Blazor/_Imports.razor`, is what lets a consumer use any component in the library without
   knowing which group it's filed under. The groups are:

   | Group | Contents |
   | --- | --- |
   | `Primitives` | Button, Badge, Alert, Card, Skeleton, EmptyState, ... |
   | `Forms` | FormField, TextInput, Select, Toggle, ... |
   | `Navigation` | TabGroup, Breadcrumb, Pagination, ... |
   | `Overlays` | Modal, ConfirmModal, ToastHost, ... |
   | `Widgets` | Live/dashboard components (ActivityFeed, BotStatusCard, ...) |
   | `Tts` | VoiceSelector, StyleSelector, PresetBar, ... |
   | `Icons` | `Icon`, `IconPaths` |

2. **Parameters mirror the partial's view model**, PascalCase, `[Parameter]`. Use
   `[Parameter, EditorRequired]` for the ones the view model marks `required` — none of the view
   models ported so far use C#'s `required` modifier (they're all mutable records with defaults),
   so `EditorRequired` is applied by judgment: a parameter with no sensible default (an icon
   `Path`, a required id) gets it; everything else stays optional with the same default the view
   model had. **Never use the C# `required` modifier or an `init` accessor on a `[Parameter]`** —
   Blazor sets parameters via reflection after construction, which both features defeat (see
   `05-components.md` in the Blazor skill).

3. **Slots are `RenderFragment` / `RenderFragment<T>`, never HTML strings.** Every partial's
   `HeaderContent`/`BodyContent`/`FooterContent`/`HeaderActions` is a `string?` filled by the
   caller with `@Html.Raw(...)`-injected markup — the single biggest structural mismatch with
   Blazor (see `blazor-port-inventory.md` Part 3 §2, "Slot pattern used throughout"). Every one of
   those becomes a typed slot on the component:
   - The main body slot is always named `ChildContent` (the Blazor convention — it's what
     `<Card>...</Card>` child markup binds to without an explicit tag).
   - Named slots keep the partial's name: `HeaderContent`, `FooterContent`, `HeaderActions`.
   - A slot that iterates typed data (a table row template, a list item) is `RenderFragment<T>`.

4. **Every raw-JS-string callback becomes an `EventCallback`.** `OnClick`, `DismissCallback`,
   `OnRemove`, `OnModeChange`, `OnFormatChange`, and the rest of the `string?` "JavaScript handler
   name" parameters throughout `ViewModels/Components/` become `EventCallback` (no argument) or
   `EventCallback<T>` (`MouseEventArgs`, a typed value, ...). The two view models that
   regex-validate their callback as a JS identifier in their `init` setter
   (`EmphasisToolbarViewModel.OnFormatChange`, `PauseModalViewModel.OnInsertCallback`) lose that
   validation code entirely — an `EventCallback` can't be an invalid identifier.

5. **Pass-through attributes and classes.** Every component declares
   `[Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }`
   splatted on the root element with `@attributes="AdditionalAttributes"`, plus a
   `[Parameter] public string? Class { get; set; }` appended to the root element's own computed
   class list (after it, so a caller's override class can win on specificity ties the same way
   the partials merge `Model.CssClass`/`Model.AdditionalAttributes["class"]` today).

6. **Styling.** Only the existing Tailwind utilities and the `@layer components` classes already
   in `site.css` (`btn-*`, `badge-*`, `card*`, `alert-*`, `skeleton`, `status-*`, the token colors
   `bg-bg-*`, `text-text-*`, `text-accent-*`, ...). No hex values, no inline `style` that sets a
   color. **Reproduce the partial's classes exactly — this phase does not restyle anything.** An
   inline `<style>` block that exists in a partial today moves to that component's `.razor.css`
   isolation file; no other component gets one. Because Tailwind purges by literal class-name
   token (the `safelist` in `tailwind.config.js` only covers the `btn-`/`badge-`/`card-`/...
   *prefixes*, not arbitrary interpolation), a class string built in C# must always contain the
   full literal token: `$"btn-{variant}"` where `variant` is a small closed enum is fine,
   `$"text-{colour}-500"` is not — enumerate the possibilities in a `switch` instead (see
   `LoadingSpinner`'s `border-accent-orange` / `border-accent-blue` for a worked example: two
   literal branches, not one interpolated one).

7. **Discord IDs are `string` in any parameter that reaches markup** — a `ulong` guild/user/channel
   ID is fine in C# logic, but the moment it is bound into an `href`, a `data-*` attribute, or an
   `IJSRuntime.InvokeAsync` call, convert it to `string` first (same rule as `'@Model.GuildId'` in
   Razor Pages — see the Gotchas section of `CLAUDE.md`).

8. **Icons go through `<Icon>`.** No inline `<svg>` for a stroke-outline icon and no raw path
   string sitting in a component parameter as a magic literal — reference a named constant on
   `Icons/IconPaths.cs` (`IconPaths.ChevronDown`, not a `"M19 9l-7 7-7-7"` literal at the call
   site). The one exception is a partial whose icon markup isn't a 24×24 stroke-outline icon at
   all — `_Badge.cshtml`'s `IconLeft` is a 20×20 **filled** Heroicon sized by the `.badge svg` CSS
   rule (0.75rem), not the `w-*/h-*` Tailwind sizing `<Icon>` uses — reproduce that markup
   literally instead of forcing it through `<Icon>`.

9. **Accessibility attributes identical to the partial** — every `aria-*`, `role`, and
   `aria-hidden` on a decorative SVG carries over unchanged. Where a partial is visibly
   inconsistent about this (some of the Tier 1a partials put `aria-hidden="true"` on a decorative
   icon, others of the same shape omit it), the component takes the more correct, more consistent
   behavior rather than reproducing the omission — `<Icon>` always sets `aria-hidden="true"`
   unless a `Title` is given, which is strictly an accessibility improvement on top of markup that
   is otherwise unchanged.

10. **Tests and showcase.** Every component ships a bUnit test class at
    `tests/DiscordBot.ComponentTests/Blazor/Shared/<Group>/<Name>Tests.cs` (derive from
    `BlazorComponentTestContext`, see its XML doc for what it wires up) covering every
    variant/size/state enum value, slot rendering, callback invocation, and `Class` /
    `AdditionalAttributes` pass-through — and an entry on the `/components` showcase page (built
    tier by tier as `*Showcase.razor` sections under `Blazor/Pages/Components/Sections/`, composed
    into the routable page in the PR that replaces `Pages/Components.cshtml`).

### Worked example

`Alert` from `_Alert.cshtml` / `AlertViewModel`, showing the pattern end to end — a view-model
`string?` becomes a typed parameter, a `DismissCallback` raw-JS-string becomes an `EventCallback`,
and (per the "self-manage" note already called out for this one in
`blazor-port-inventory.md`) the component owns its own dismissed state instead of relying on the
caller to stop rendering it:

```razor
@namespace DiscordBot.Bot.Blazor.Shared

@if (!_dismissed)
{
    <div class="flex items-start gap-3 p-4 rounded-lg border @BgClass @BorderClass @TextClass @Class"
         role="alert" aria-live="polite" @attributes="AdditionalAttributes">
        @if (ShowIcon)
        {
            <Icon Path="@IconPath" Size="IconSize.MD" Class="flex-shrink-0 mt-0.5" />
        }
        <div class="flex-1">
            @if (!string.IsNullOrEmpty(Title))
            {
                <h3 class="text-sm font-semibold">@Title</h3>
            }
            <p class="text-sm opacity-90 @(!string.IsNullOrEmpty(Title) ? "mt-1" : "")">
                @(ChildContent is not null ? ChildContent : (RenderFragment)(b => b.AddContent(0, Message)))
            </p>
        </div>
        @if (IsDismissible)
        {
            <button type="button" class="p-1 hover:opacity-70 transition-opacity" aria-label="Dismiss"
                    @onclick="HandleDismiss">
                <Icon Path="IconPaths.XMark" Size="IconSize.MD" />
            </button>
        }
    </div>
}

@code {
    [Parameter] public AlertVariant Variant { get; set; } = AlertVariant.Info;
    [Parameter] public string? Title { get; set; }
    [Parameter] public string Message { get; set; } = string.Empty;
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public bool IsDismissible { get; set; }
    [Parameter] public bool ShowIcon { get; set; } = true;
    [Parameter] public EventCallback OnDismiss { get; set; }
    [Parameter] public string? Class { get; set; }
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private bool _dismissed;

    // Self-hides when the caller doesn't care to be told (no OnDismiss delegate); a caller that
    // does supply one is assumed to own removing this Alert from whatever list rendered it, so
    // the component defers instead of hiding out from under a still-truthy caller-side flag.
    private async Task HandleDismiss()
    {
        if (OnDismiss.HasDelegate)
        {
            await OnDismiss.InvokeAsync();
        }
        else
        {
            _dismissed = true;
        }
    }

    private string IconPath => Variant switch
    {
        AlertVariant.Success => IconPaths.CheckCircle,
        AlertVariant.Warning => IconPaths.ExclamationTriangle,
        AlertVariant.Error => IconPaths.XCircle,
        _ => IconPaths.InformationCircle
    };

    private string BgClass => Variant switch { AlertVariant.Success => "bg-success/10", AlertVariant.Warning => "bg-warning/10", AlertVariant.Error => "bg-error/10", _ => "bg-info/10" };
    private string BorderClass => Variant switch { AlertVariant.Success => "border-success/30", AlertVariant.Warning => "border-warning/30", AlertVariant.Error => "border-error/30", _ => "border-info/30" };
    private string TextClass => Variant switch { AlertVariant.Success => "text-success", AlertVariant.Warning => "text-warning", AlertVariant.Error => "text-error", _ => "text-info" };
}
```

### Gotchas that apply here specifically

- A component with `@onclick` (or any other interactive directive) only actually runs once the
  page hosting it opts into `@rendermode InteractiveServer` — that's the page's concern, not the
  component's; the component itself is render-mode agnostic and works identically under static
  SSR (minus the event handlers firing) or an interactive circuit.
- Don't call `StateHasChanged` from a parameter setter — react to parameter changes in
  `OnParametersSet(Async)` instead (see `05-components.md`).
- An `EventCallback` raised from a non-UI thread (a background subscription, a timer) must be
  invoked through `InvokeAsync` to marshal back onto the component's synchronization context —
  not relevant to any Tier 1a component (none of them subscribe to anything), but it applies the
  moment a Tier 4 live widget calls back into a parent.

## Tiers

Build order, from `blazor-port-plan.md` §4.6. Each tier only depends on tiers before it (a
Tier 2 form component may use a Tier 1 `Icon` or `Badge`; nothing in Tier 1 depends on Tier 2+).

| Tier | Components | Interop |
| --- | --- | --- |
| 1 Primitives | Button, Badge, Alert, Card, Skeleton, SkeletonCard, LoadingSpinner, EmptyState, StatusIndicator, StatusBadge, SeverityBadge, RuleTypeIcon, Icon, HeroMetricCard, GuildStatsCard, DashboardWidget, Breadcrumb, PageHeader, GuildHeader, Pagination, Kbd | none |
| 2 Forms | FormField, TextInput, Select (with optgroups), Toggle, TextArea, SettingField, Autocomplete (native rewrite), FilterPanel, SortDropdown, DateRangeFilter | `browser.js` for localStorage on DateRangeFilter |
| 3 Navigation & overlays | TabGroup, Modal, ConfirmModal, ToastHost + `IToastService`, PreviewPopover (user/guild), LoadingOverlay + `ILoadingState`, GuildContextSelector, Highlight, RestartBanner | `browser.js` (focus trap, click-outside, positioning) |
| 4 Live widgets | BotStatusBanner/Card (one subscription, no pollers), ActivityFeed, ConnectionStatus, NotificationBell, QuickActionsCard, ConnectedServersWidget, AuditLogCard, RecentActivityCard (make its refresh button work), CommandStatsCard, Chart, VoiceChannelPanel | `charts.js`; event bus |
| 5 TTS | VoiceSelector, StyleSelector, PresetBar, ModeSwitcher, SsmlPreview, EmphasisToolbar, PauseModal | `browser.js` textarea selection + clipboard; `ssml-markers.js` |

This document built **Tier 1a** — the base primitives every later component composes with:
`Icon`, `Button`, `Badge`, `Alert`, `Card`, `Skeleton`, `SkeletonCard`, `LoadingSpinner`,
`EmptyState`, `Kbd`. The rest of Tier 1 (StatusIndicator, StatusBadge, SeverityBadge,
RuleTypeIcon, HeroMetricCard, GuildStatsCard, DashboardWidget, Breadcrumb, PageHeader,
GuildHeader, Pagination) lands in a later PR of this same tier.

## Consolidations

The port merges several near-duplicate partials into one Blazor component rather than porting
both sides separately (full rationale for each pair is in `blazor-port-inventory.md` Part 3 §5.2,
"Inconsistencies between components"):

| Merge into | Absorbs | How |
| --- | --- | --- |
| `Card` | `_Card` + `_EnhancedCard` | One component; `Accent="CardAccent.None"` (default) renders the plain `.card` markup, any other `Accent` renders the `.card-enhanced` markup with the gradient top border, `HoverLift`/`CompactPadding` (enhanced-only concepts) apply only on that path. |
| `TabGroup` | `_NavTabs` + `_TabPanel` | One component with an in-page mode (conditional rendering, no JS) and a navigation mode (`NavLink`); the AJAX partial-swap mode both partials support is dropped — there is no reason to fetch and inject another route's HTML from inside a Blazor circuit. |
| `ConfirmModal` | `_ConfirmationModal` + `_TypedConfirmationModal` | One component; `RequiredText` (unset by default) turns on the typed-confirmation behavior — `CanConfirm` becomes `RequiredText is null || Input == RequiredText` instead of a second component. |
| `ActivityFeed` | `_ActivityFeed` + `_ActivityFeedTimeline` | One component that owns a `List<ActivityItem>` and appends from its own event-bus subscription — the `<template>`-cloning / SignalR-hub-script pattern both partials rely on has no reason to survive; a native Blazor re-render replaces it entirely. |
| `Breadcrumb` | `_Breadcrumb` (root) + `_GuildBreadcrumb` + `_CommandBreadcrumb` | One component taking a typed `IReadOnlyList<BreadcrumbItem>`; the root partial's untyped `ViewData["Breadcrumbs"]` tuple list and `_CommandBreadcrumb`'s hardcoded 3-tab special case both go away — callers build the list explicitly. |
| `Toggle` | `_FormToggle` (Forms tier) + the settings-page toggle | One `<InputCheckbox>`-style component; the `data-setting-toggle` DOM-scanning dirty-tracking convention becomes explicit component/`EditContext` state. |
| `ToastHost` | `_ToastContainer` (root, top-right, `TempData`-bridged) + `_ToastContainer` (Components/, bottom-right) | One component + `IToastService`; the `TempData` flash-message bridge has no equivalent need once a form action is an `EventCallback` inside the same circuit rather than a full page post. |
| `FilterPanel` | `FilterPanelTagHelper` + its orphaned view model | One component; the tag helper's attribute-based API becomes ordinary `[Parameter]`s. |
| `Highlight` | `HighlightTagHelper` | One component; same reasoning. |

## Component reference

Each tier appends its rows here as it lands. Full per-partial detail (view model, JS coupling,
proposed mapping) is in `blazor-port-inventory.md` Part 3 §2 — this table is the "what actually
shipped" record, kept current as the source of truth for what exists today.

### Icons

| Component | From partial | Parameters |
| --- | --- | --- |
| `Icon` | (new — replaces the inline `<svg>` mix every other partial uses) | `Path` (required), `Size` (`IconSize?`, no default — omit for a `Class`-driven size), `Class`, `Title`, `StrokeWidth` (`string`, default `"2"`), `AdditionalAttributes` |

`IconPaths` is a static class of Heroicons-outline `d` path constants, named by Heroicon name
(`XMark`, `ChevronDown`, `Plus`, `CheckCircle`, `ExclamationTriangle`, `XCircle`,
`InformationCircle`, `FolderOpen`, `MagnifyingGlass`, `Sparkles`, `ExclamationCircle`,
`LockClosed`, `SignalSlash` as of Tier 1a). Later tiers add to it rather than introducing a second
icon-constants class.

### Primitives

| Component | From partial(s) | Key parameters |
| --- | --- | --- |
| `Button` | `_Button` / `ButtonViewModel` | `Text`, `ChildContent`, `Variant` (`ButtonVariant`), `Size` (`ButtonSize`), `Type`, `IconLeft`/`IconRight`, `IsDisabled`, `IsLoading` + `LoadingText`, `IsIconOnly` + `AriaLabel`, `OnClick`, `Href`, `Class`, `AdditionalAttributes` |
| `Badge` | `_Badge` / `BadgeViewModel` | `Text`, `ChildContent`, `Variant` (`BadgeVariant`), `Size` (`BadgeSize`), `Style` (`BadgeStyle`), `IconLeft`, `IsRemovable` + `OnRemove`, `Class`, `AdditionalAttributes` |
| `Alert` | `_Alert` / `AlertViewModel` | `Variant` (`AlertVariant`), `Title`, `Message`/`ChildContent`, `IsDismissible`, `ShowIcon`, `OnDismiss`, `Class`, `AdditionalAttributes` |
| `Card` | `_Card` + `_EnhancedCard` / `CardViewModel` + `EnhancedCardViewModel` | `Title`, `Subtitle`, `HeaderContent`, `HeaderActions`, `ChildContent`, `FooterContent`, `Variant` (`CardVariant`, plain-card path only), `Accent` (`CardAccent`), `HoverLift`, `CompactPadding`, `IsInteractive` + `OnClick`, `IsCollapsible` + `IsExpanded`/`IsExpandedChanged`, `Id`, `Class`, `AdditionalAttributes` |
| `Skeleton` | `_Skeleton` / `SkeletonViewModel` | `Type` (`SkeletonType`), `Width`, `Height`, `Rounded`, `Animate`, `Class`, `AdditionalAttributes` |
| `SkeletonCard` | `_SkeletonCard` / `SkeletonCardViewModel` | `Type` (`SkeletonCardType`), `ShowHeader`, `Class`, `AdditionalAttributes` |
| `LoadingSpinner` | `_LoadingSpinner` / `LoadingSpinnerViewModel` | `Variant` (`SpinnerVariant`), `Size` (`SpinnerSize`), `Message`, `SubMessage`, `Color` (`SpinnerColor`), `IsOverlay`, `Class`, `AdditionalAttributes` |
| `EmptyState` | `_EmptyState` / `EmptyStateViewModel` | `Type` (`EmptyStateType`), `Title`, `Description`, `IconPath` (override), `PrimaryActionText` + `PrimaryActionHref`/`OnPrimaryAction`, `SecondaryActionText` + `SecondaryActionHref`, `Size` (`EmptyStateSize`), `Class`, `AdditionalAttributes` |
| `Kbd` | (new — the `.kbd` class in `site.css`, not wired to any partial today) | `ChildContent`, `Class`, `AdditionalAttributes` |
| `StatusIndicator` | `_StatusIndicator` / `StatusIndicatorViewModel` | `Status` (`StatusType`), `Text` (override), `DisplayStyle` (`StatusDisplayStyle`), `IsPulsing`, `Size` (`StatusSize`), `Class`, `AdditionalAttributes` |
| `StatusBadge` | `_StatusBadge` (model: Core `FlaggedEventStatus` directly, no ViewModel) | `Status` (`FlaggedEventStatus`), `Class`, `AdditionalAttributes` |
| `SeverityBadge` | `_SeverityBadge` (model: Core `Severity` directly, no ViewModel) | `Severity` (`Severity`), `Class`, `AdditionalAttributes` |
| `RuleTypeIcon` | `_RuleTypeIcon` (model: Core `RuleType` directly, no ViewModel) | `RuleType` (`RuleType`), `Class`, `AdditionalAttributes` |
| `HeroMetricCard` | `_HeroMetricCard` / `HeroMetricCardViewModel` | `Title`, `Value`, `TrendValue`/`TrendDirection` (`TrendDirection`)/`TrendLabel`, `AccentColor` (`CardAccent`), `IconContent` (`RenderFragment`, raw inner `<svg>` content) or `IconPath` (single-path convenience, through `<Icon>`), `ShowSparkline` + `SparklineData`, `DataAttribute` (still emits a bare `data-*` attribute on the value element for the legacy realtime JS), `Id`, `Class`, `AdditionalAttributes` |
| `GuildStatsCard` | `_GuildStatsCard` (model: Pages-namespace `GuildStatsViewModel`, taken here as three primitive values) | `TotalGuilds`, `ActiveGuilds`, `InactiveGuilds`, `Class`, `AdditionalAttributes` |
| `DashboardWidget` | `_DashboardWidget` / `DashboardWidgetViewModel` | `Title`, `Subtitle`, `DetailUrl`/`DetailLinkText`, `IconPath`, `IsEnabled` + `EnabledLabel`/`DisabledLabel`, `ChildContent` (body) or `EmptyState` (`EmptyStateViewModel`, mapped onto `<EmptyState>`) or `EmptyContent` (`RenderFragment`) — precedence `ChildContent` > `EmptyContent` > `EmptyState`, `ColSpan` (1\|2), `HeaderActions` (`List<WidgetHeaderAction>`), `Class`, `AdditionalAttributes` |
| `Highlight` | `HighlightTagHelper` (`Helpers/TextHighlightHelper`) | `Text`, `SearchTerm`, `MaxLength`, `ShowContext`, `Class`, `AdditionalAttributes` |

### Forms

| Component | From partial(s) | Key parameters |
| --- | --- | --- |
| `FormField` | wrapper markup shared by `_FormInput`/`_FormSelect` | `Label`, `For`, `HelpText`, `ValidationState`, `ValidationMessage`, `IsRequired`, `ChildContent`, `FooterContent`, `Class`, `AdditionalAttributes`; static `ComputeDescribedByIds` helper shared by every input below |
| `TextInput` | `_FormInput` / `FormInputViewModel` | `Id` (required), `Name`, `Label`, `Type`, `Placeholder`, `HelpText`, `Size` (`InputSize`), `ValidationState`/`ValidationMessage`, `IsRequired`/`IsDisabled`/`IsReadOnly`, `IconLeft`/`IconRight`, `MaxLength` + `ShowCharacterCount`, `Value`/`ValueChanged`/`ValueExpression`, `Class`, `AdditionalAttributes` |
| `TextArea` | (new — no source partial; see `TextArea.razor`'s file comment) | Same shape as `TextInput` plus `Rows` (default 4), minus `IconLeft`/`IconRight` |
| `Select<TValue>` | `_FormSelect` / `FormSelectViewModel` | `Id` (required), `Name`, `Label`, `Placeholder`, `Options` (`List<SelectOption>`), `OptionGroups` (`List<SelectOptionGroup>`), `HelpText`, `Size`, `ValidationState`/`ValidationMessage`, `IsRequired`/`IsDisabled`, `AllowMultiple` + `SelectedValues`/`SelectedValuesChanged` (separate from `Value`/`ValueChanged`/`ValueExpression` - see the component's file comment on why multi-select doesn't reuse `TValue`), `Class`, `AdditionalAttributes` |
| `Toggle` | `_FormToggle` / `FormToggleViewModel` | `Id` (required), `Name`, `Label`, `Description`, `IsDisabled`, `SettingToggle` (opt-in `data-setting-toggle="true"` for legacy JS), `Value`/`ValueChanged`/`ValueExpression`, `Class`, `AdditionalAttributes` |
| `SettingField` | `Pages/Shared/_SettingField.cshtml` (model `Core.DTOs.SettingDto`) | `Setting` (required), `OnValueChanged` (`EventCallback<(string Key, string Value)>`) - dispatches to `Toggle`/`Select`/`TextInput` by `SettingDataType`, renders the "Restart Required" indicator via `Badge` |
| `Autocomplete` | `_AutocompleteInput` / `AutocompleteInputViewModel` (native rewrite of `autocomplete.js`) | `Id` (required), `Label`, `Placeholder`, `SearchFunc` (required, `Func<string, CancellationToken, Task<IReadOnlyList<AutocompleteItem>>>` - no HTTP inside the component), `Value`/`ValueChanged`, `DisplayText`, `IsRequired`, `MinChars`, `DebounceMs`, `MaxResults`, `NoResultsMessage`, `HelpText`, `Class`, `AdditionalAttributes` |
| `FilterPanel` | `TagHelpers/FilterPanelTagHelper.cs` (+ the orphaned `FilterPanelViewModel`) | `Title`, `IsCollapsible`, `DefaultExpanded`, `IsExpanded`/`IsExpandedChanged`, `ActiveFilterCount`, `Id` (content region id), `ChildContent`, `Class`, `AdditionalAttributes` |
| `SortDropdown` | `Pages/Shared/_SortDropdown.cshtml` / `SortDropdownViewModel` (AJAX mode dropped) | `Id` (required), `Options` (`List<SortOption>`), `Value`/`ValueChanged`, `Label` (fallback button text), `Class`, `AdditionalAttributes` |
| `DateRangeFilter` | date-range markup in `Pages/Commands/Index.cshtml` + `wwwroot/js/date-range-filter.js` | `Start`/`StartChanged`, `End`/`EndChanged` (`DateOnly?`), `OnChanged`, `Id`, `Class`, `AdditionalAttributes` - Today/7 days/30 days presets and Clear as buttons, no localStorage |

`AutocompleteItem` (`Blazor/Shared/Forms/AutocompleteItem.cs`) is a `record(string Id, string Text, string? Description)` - the shape `Autocomplete.SearchFunc` returns.

New icon paths (`Blazor/Shared/Icons/IconPaths.Forms.cs`): `ExclamationCircleOutline`, `Funnel`, `BarsArrowDown`, `Check`, `ArrowPath`, `User`, `SpeakerWave`, `Hashtag`.

### Navigation

| Component | From partial(s) | Key parameters |
| --- | --- | --- |
| `Breadcrumb` | `_Breadcrumb` (root) + `_GuildBreadcrumb` + `_CommandBreadcrumb` / `BreadcrumbItem` | `Items` (`IReadOnlyList<BreadcrumbItem>`, reused from `GuildBreadcrumbViewModel.cs`), `Class`, `AdditionalAttributes` — `aria-current="page"` is applied to the last item consistently regardless of which source partial did or didn't |
| `PageHeader` | `_CommandHeader` / `CommandHeaderViewModel` | `Title`, `Subtitle`, `Actions` (`RenderFragment`, new — the partial had no actions slot), `Class`, `AdditionalAttributes` |
| `GuildHeader` | `_GuildHeader` / `GuildHeaderViewModel` | `GuildId` (`string`), `Name`, `IconUrl`, `PageTitle`/`PageDescription`, `Actions` (`List<HeaderAction>`, reused from the view model), `StatusBadge` (`BadgeViewModel`, rendered through the Tier 1a `<Badge>`), `Class`, `AdditionalAttributes` |
| `Pagination` | `_Pagination` / `PaginationViewModel` | `CurrentPage`/`TotalPages`/`TotalItems`/`PageSize`, `PageSizeOptions`, `Style` (`PaginationStyle`), `ShowPageSizeSelector`/`ShowItemCount`/`ShowFirstLast`, `BaseUrl` (link mode — builds `<a href>` like the partial) or, when `null`, callback mode (`<button>`s) via `OnPageChanged`/`OnPageSizeChanged` (`EventCallback<int>`), `PageParameterName`/`PageSizeParameterName`, `Class`, `AdditionalAttributes` — the page-size `<select onchange="location.href=...">` becomes `NavigationManager.NavigateTo` in link mode |
| `TabGroup` | `_NavTabs` + `_TabPanel` / `NavTabsViewModel` + `TabPanelViewModel` | `Tabs` (`IReadOnlyList<TabItem>`), `ActiveTabId` (two-way), `StyleVariant` (`TabStyleVariant`), `Mode` (`TabGroupMode`: InPage/Navigation — AJAX dropped), `Compact`, `AriaLabel`, `ChildContent` (InPage mode's `TabPanel` children), `Id`, `Class`, `AdditionalAttributes`. `TabItem` (record): `Id`, `Label`, `ShortLabel`, `Href`, `IconPath`, `BadgeCount`/`BadgeVariant`, `Subtitle`, `Disabled`. |
| `TabPanel` | (child of `TabGroup`, Mode=InPage) | `Id`, `ChildContent`, `Class`, `AdditionalAttributes` — reads the parent `TabGroup` via cascading value, renders only when `Id == ActiveTabId` |
| `GuildContextSelector` | `_GuildContextSelector` / `GuildContextSelectorViewModel` | `RouteTemplate`, `Guilds` (`IReadOnlyList<GuildSelectorItem>`), `Class`, `AdditionalAttributes` |

### Overlays

| Component | From partial(s) | Key parameters |
| --- | --- | --- |
| `Modal` | `_ConfirmationModal` (generic dialog shell) | `Id`, `Title`, `ChildContent`, `FooterContent`, `Size` (`ModalSize`), `IsOpen` (two-way), `OnClosed`, `ReturnFocusTo`, `Class`, `AdditionalAttributes` |
| `ConfirmModal` | `_ConfirmationModal` + `_TypedConfirmationModal` / `ConfirmationModalViewModel` + `TypedConfirmationModalViewModel` | `Id`, `Title`, `Message`/`MessageContent`, `ConfirmText`, `CancelText`, `Variant` (`ConfirmationVariant`), `CustomIconPath`, `RequiredText` + `InputLabel` (typed-confirmation mode), `OnConfirm`, `OnCancel`, and an awaitable `Task<bool> ShowAsync()` (via `@ref`) — built on `Modal` |
| `ToastHost` | `_ToastContainer` (root, top-right) + `_ToastContainer` (Components/, bottom-right) / `IToastService` | `Class`, `AdditionalAttributes` — subscribes to `IToastService.Changed`, renders `IToastService.Toasts` |
| `LoadingOverlay` | `_PageLoadingOverlay` / `PageLoadingOverlayViewModel` + `ILoadingState` | `Variant` (`SpinnerVariant`), `Size` (`SpinnerSize`), `Message`, `SubMessage`, `OnCancel`, `CancelText`, `AdditionalAttributes` — subscribes to `ILoadingState.Changed` |
| `PreviewPopover<TModel>` | `_UserPreviewPopup` + `_GuildPreviewPopup` + `_PreviewPopupLoading` + `_PreviewPopupError` | `Kind` (`PreviewKind`), `Id`, `GuildId`, `Loader` (`Func<Task<TModel?>>`), `ContentTemplate` (`RenderFragment<TModel>`), `ChildContent` (trigger), `Placement` (`PopoverPlacement`), `Class`, `AdditionalAttributes` — the caller supplies `Loader`, this component never calls a controller/service itself |
| `UserPreviewPopoverContent` | `_UserPreviewPopup` / `UserPreviewViewModel` | `Model` |
| `GuildPreviewPopoverContent` | `_GuildPreviewPopup` / `GuildPreviewViewModel` | `Model` |

### Widgets

| Component | From partial(s) | Key parameters | Live source |
| --- | --- | --- | --- |
| `BotStatusBanner` | `_BotStatusBanner` / `BotStatusBannerViewModel` | `Class`, `AdditionalAttributes` (fully self-loading, no data parameters) | `IDashboardMetricsService.GetCurrentStatus` initial; `BotStatusUpdatedEvent`/`BotStatusBroadcastEvent` live (`IGuildService`/`IVersionService` for `TotalMembers`/`Version`, not live-updated — matches the legacy JS, which doesn't patch those either) |
| `BotStatusCard` | `_BotStatusCard` / (Pages) `BotStatusViewModel` | `Class`, `AdditionalAttributes` | Same as `BotStatusBanner`; `StatusIndicator`'s markup reproduced inline (Tier 1b fallback, see file header) |
| `ConnectionStatus` | `_ConnectionStatus` / `ConnectionStatusViewModel` | `State` (`ConnectionState?`, caller-controlled when set), `CustomText`, `Class`, `AdditionalAttributes` | Bus-driven from `BotStatusUpdatedEvent`/`BotStatusBroadcastEvent` when `State` is unset ("Reconnecting" only reachable by passing `State` explicitly) |
| `ActivityFeed` | `_ActivityFeed` + `_ActivityFeedTimeline` merge / `ActivityFeedViewModel` + `ActivityFeedTimelineViewModel` + `ActivityFeedItemViewModel` | `Items`, `MaxItems`, `EmptyMessage`, `IsPaused`/`IsPausedChanged`, `Title`, `ShowRefreshButton`/`OnRefresh`, `ViewAllUrl`, `MaxHeight`, `GuildId` | `CommandExecutedEvent` (filtered on `Update.GuildId` when `GuildId` given) + `GuildActivityEvent` (guild-scoped `Subscribe` overload when `GuildId` given) |
| `NotificationBell` | Navbar bell markup (`_Navbar.cshtml`) + `notification-bell.js` | `Class`, `AdditionalAttributes` (self-loading from the current user) | `IDashboardNotificationQueryService` for summary/list/mark-read/mark-all/dismiss; `NotificationReceivedEvent`/`NotificationCountChangedEvent`/`NotificationMarkedReadEvent`/`AllNotificationsReadEvent` via the user-scoped `Subscribe` overload (current user id from `AuthenticationStateProvider`) |
| `QuickActionsCard` | `_QuickActionsCard` / `QuickActionsCardViewModel` + `QuickActionItemViewModel` | `Title`, `Actions`, `UserIsAdmin`, `OnAction`, `OnConfirmRequested` | none (presentation only) |
| `ConnectedServersWidget` | `_ConnectedServersWidget` / `ConnectedServersWidgetViewModel` + `ConnectedServerItemViewModel` | `Title`, `ViewAllUrl`, `Servers`, `TotalServerCount`, `Class`, `AdditionalAttributes` | none (presentation only); row menu/copy-id are component state + `BrowserInterop.CopyToClipboardAsync` |
| `AuditLogCard` | `_AuditLogCard` / `AuditLogCardViewModel` (`.FromLogs` reused) | `Logs`, `Class`, `AdditionalAttributes` | none (presentation only) |
| `RecentActivityCard` | `_RecentActivityCard` / (Pages) `RecentActivityViewModel` | `Activities`, `OnRefresh`, `Class`, `AdditionalAttributes` | none (presentation only); `OnRefresh` is a real callback — the legacy button was non-functional |
| `CommandStatsCard` | `_CommandStatsCard` + `command-stats-chart.js` / (Pages) `CommandStatsViewModel` | `TopCommands`, `TotalCommands`, `TimeRangeHours`, `OnTimeRangeChanged`, `Class`, `AdditionalAttributes` | none (presentation only); renders through `<Chart>` |
| `Chart` | (new — generic Chart.js wrapper) | `Type` (required), `Data` (required), `Options`, `Height`, `Class`, `AdditionalAttributes` | none; owns `ChartInterop` create/update (on `Data`/`Options` reference change)/destroy |
| `VoiceChannelPanel` | `_VoiceChannelPanel` + `voice-channel-panel.js` / `VoiceChannelPanelViewModel` + `VoiceChannelInfo`/`NowPlayingInfo`/`QueueItemInfo` | `GuildId` (string, required), `IsCompact`, `ShowNowPlaying`, `ShowProgress`, `AvailableChannels`, `Queue` (caller-seeded, like `ActivityFeed.Items` — not part of `AudioStatusDto`), `OnJoined`/`OnLeft`/`OnStopped`/`OnSkipped` | `IDashboardAudioStatusService.GetCurrentAudioStatus` initial; `AudioConnectedEvent`/`AudioDisconnectedEvent`/`PlaybackStartedEvent`/`PlaybackProgressEvent`/`PlaybackFinishedEvent`/`QueueUpdatedEvent`/`VoiceChannelMemberCountUpdatedEvent` live, all guild-scoped; join/leave/stop/skip call `IAudioService`/`IPlaybackService` directly |
| `RestartBanner` | `_RestartBanner` (`<authorize policy="RequireAdmin">`) | `OnOpenBotControl`, `Class`, `AdditionalAttributes` — wrapped in `<AuthorizeView Policy="RequireAdmin">`

Tier 1b (`StatusIndicator`, `HeroMetricCard`, `DashboardWidget`) and Tier 3 (`TabGroup`, `Modal`)
were being built in parallel and hadn't landed when this tier shipped — `BotStatusCard`
reproduces `_StatusIndicator.cshtml`'s markup inline as a documented fallback (see the component's
file header) rather than depending on either; no other Tier 4 component needed them. Every
component ships an `IconPaths.Widgets.cs` partial-class addition rather than a second icon file;
a handful of *filled* (non stroke-outline) icons specific to `VoiceChannelPanel`/`RecentActivityCard`
stay as literal inline `<svg>` per the same exception `Badge.razor` documents.

### Tts

_Tier 5 — not yet built._
