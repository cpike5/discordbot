# Implementation Plan: Guild Context Selector for Search Results (#1710)

## Problem

When search results include guild-scoped pages (Soundboard, Audio Settings, VOX, TTS, Reminders, etc.), the result links are broken because `PageMetadataService` stores routes like `/Guilds/Soundboard` without a guild ID, but the actual pages require `/Guilds/Soundboard/{guildId}`. Users have no way to select which guild's page to navigate to.

## Current Architecture

The project uses Razor Pages + partial views (not Blazor yet). Reusable components follow this pattern:

- **ViewModel**: `src/DiscordBot.Bot/ViewModels/Components/FooViewModel.cs`
- **Partial view**: `src/DiscordBot.Bot/Pages/Shared/Components/_Foo.cshtml`
- **Usage**: `@await Html.PartialAsync("Components/_Foo", viewModel)`

Blazor migration is in progress on the `blazor-migration` branch. This component should be designed to map cleanly to a `.razor` component later.

## Design

### Component: GuildContextSelector

**Purpose**: Inline dropdown that appears on guild-scoped search results, allowing the user to pick which guild to navigate to.

**Behavior**:
- **Single guild**: Render as a direct link — "Open in [GuildName]" (no dropdown)
- **Multiple guilds**: Render a dropdown button that expands inline with guild options
- **Zero guilds**: Show disabled state with "No guilds available" text

### ViewModel

```csharp
// src/DiscordBot.Bot/ViewModels/Components/GuildContextSelectorViewModel.cs
public record GuildContextSelectorViewModel
{
    public string RouteTemplate { get; init; } = string.Empty; // e.g., "/Guilds/Soundboard/{guildId}"
    public IReadOnlyList<GuildSelectorItem> Guilds { get; init; } = [];
}

public record GuildSelectorItem
{
    public string GuildId { get; init; } = string.Empty; // string for JS snowflake safety
    public string GuildName { get; init; } = string.Empty;
    public string? GuildIconUrl { get; init; }
}
```

### Partial View

`src/DiscordBot.Bot/Pages/Shared/Components/_GuildContextSelector.cshtml` — renders the selector using Alpine.js for dropdown toggle, matching existing project patterns (see `guild-nav.js`).

## Files to Create/Modify

### New Files
| File | Purpose |
|------|---------|
| `ViewModels/Components/GuildContextSelectorViewModel.cs` | Component ViewModel + GuildSelectorItem record |
| `Pages/Shared/Components/_GuildContextSelector.cshtml` | Partial view with Alpine.js dropdown |

### Modified Files
| File | Change |
|------|--------|
| `src/DiscordBot.Core/DTOs/PageMetadataDto.cs` | Add `RequiresGuildContext` (bool) and `RouteTemplate` (string?) |
| `src/DiscordBot.Core/DTOs/SearchDtos.cs` | Add `RequiresGuildContext` (bool) and `RouteTemplate` (string?) to `SearchResultItemDto` |
| `src/DiscordBot.Bot/Services/PageMetadataService.cs` | For all 22 `Section = "Guild"` pages: set `RequiresGuildContext = true`, set `RouteTemplate = "{Route}/{guildId}"` |
| `src/DiscordBot.Bot/Services/SearchService.cs` | In `SearchPagesAsync`, propagate `RequiresGuildContext` and `RouteTemplate` to results; set `Url = ""` for guild-scoped pages |
| `src/DiscordBot.Bot/Pages/Search.cshtml.cs` | Inject `IUserDiscordGuildService` + `UserManager`; load user's guilds (intersected with bot's active guilds) when results contain guild-scoped pages |
| `src/DiscordBot.Bot/Pages/Search.cshtml` | In Pages section (~line 482), use `_GuildContextSelector` partial for guild-scoped results instead of direct `<a href>` |
| `src/DiscordBot.Bot/wwwroot/css/site.css` | Add `.guild-context-selector` and `.guild-dropdown` styles using design system tokens |

## Implementation Details

### 1. DTO Changes

**PageMetadataDto** — add:
```csharp
bool RequiresGuildContext { get; set; } = false;
string? RouteTemplate { get; set; } // e.g., "/Guilds/Soundboard/{guildId}"
```

**SearchResultItemDto** — add same two properties.

### 2. PageMetadataService Changes

For every page with `Section = "Guild"` (22 pages), set:
- `RequiresGuildContext = true`
- `RouteTemplate = $"{Route}/{{guildId}}"`

The `/Guilds` index page (`Section = "Main"`) stays unchanged.

All guild-scoped pages use `@page "{guildId:long}"`, so the template pattern `{Route}/{guildId}` is correct for all of them.

### 3. SearchService Changes

In `SearchPagesAsync`, when mapping `PageMetadataDto` → `SearchResultItemDto`:
- Propagate `RequiresGuildContext` and `RouteTemplate`
- For guild-scoped items, set `Url = ""` to prevent accidental 404 navigation
- Non-guild pages unchanged

### 4. Search Page Changes

In `Search.cshtml.cs`:
- Inject `IUserDiscordGuildService` and `UserManager<ApplicationUser>`
- In `OnGetAsync`, after search completes, check if any page results have `RequiresGuildContext`
- If yes, load user's guilds via `GetUserGuildsAsync()` and intersect with bot's known active guilds
- Map to `GuildSelectorItem` list (guild IDs as strings for snowflake safety)

In `Search.cshtml`:
- In the Pages foreach loop, split rendering based on `RequiresGuildContext`
- Non-guild pages: existing `<a href>` pattern
- Guild-scoped pages: render `_GuildContextSelector` partial with the guild list and route template

### 5. URL Construction

In the partial view, URLs are built via simple string replacement:
```razor
@item.RouteTemplate.Replace("{guildId}", guild.GuildId)
```

This produces URLs like `/Guilds/Soundboard/123456789012345678`.

## Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| User has guilds the bot isn't in (broken pages) | Intersect user guilds with bot's active guilds |
| Nested routes (e.g., `/Guilds/RatWatch/Analytics`) | All guild pages use `@page "{guildId:long}"` — template pattern `{Route}/{guildId}` works for all |
| Performance: loading guilds on every search | Only load when results contain guild-scoped pages; `GetUserGuildsAsync` is already cached |
| Discord snowflake IDs exceed JS MAX_SAFE_INTEGER | `GuildSelectorItem.GuildId` is `string`; all template attributes use strings |

## Blazor Migration Notes

When migrating to Blazor, this maps to:
- `GuildContextSelector.razor` with `[Parameter]` properties matching the ViewModel
- CSS isolation via `.razor.css`
- Alpine.js dropdown replaced with Blazor `@onclick` toggle (requires `InteractiveServer` render mode)
- The ViewModel records can be reused directly as parameter types

## Acceptance Criteria

1. Searching for "soundboard" shows the result with an inline guild selector instead of a broken link
2. Single-guild users see a direct "Open in [GuildName]" link
3. Multi-guild users see a dropdown listing their accessible guilds
4. Selecting a guild navigates to the correct URL
5. Non-guild pages continue showing direct links
6. Guild IDs handled as strings in all JS/template contexts
7. `dotnet build` and `dotnet test` pass
