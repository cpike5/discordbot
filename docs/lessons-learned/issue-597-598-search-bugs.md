# Lessons Learned: Issues #597 & #598 - Global Search Bug Fixes

**Date:** 2026-01-02
**Issues:**
- [#597 - Bug: Global search querystring parameter mismatch - 'query' vs 'q'](https://github.com/cpike5/discordbot/issues/597)
- [#598 - Bug: Command search shows module name badge only, not the matched command name](https://github.com/cpike5/discordbot/issues/598)
**PR:** [#619](https://github.com/cpike5/discordbot/pull/619)

---

## Summary

Two search-related bugs were reported after the global search feature implementation. Both were simple fixes but highlight the importance of end-to-end testing across frontend and backend.

**Scope:** Small - straightforward parameter and rendering fixes.

---

## Issue #597: Querystring Parameter Mismatch

### Problem

Clicking on recent search suggestions or using mobile search redirected to `/Search?query=term`, but the page showed empty results with the "Start searching" message despite the URL containing the search term.

### Root Cause

**Parameter name mismatch between JavaScript and C# page model.**

The C# Search page model expected `q`:

```csharp
[BindProperty(SupportsGet = true, Name = "q")]
public string? SearchTerm { get; set; }
```

But JavaScript was generating URLs with `query`:

```javascript
// search.js - Multiple locations
window.location.href = '/Search?query=' + encodeURIComponent(value);
```

When the user navigated to `/Search?query=chriswave`:
- The `query` parameter was ignored by ASP.NET model binding
- `SearchTerm` was null (because it binds to `q`)
- Page returned early with empty results

### Fix

Updated all 3 locations in `search.js` to use `q`:
- Line 177: Desktop recent searches dropdown
- Line 346: Mobile recent searches
- Line 423: Mobile search submit

### Lesson

**Always verify parameter names match across the full stack.**

When connecting JavaScript to a Razor Page:
1. Check the `[BindProperty]` attribute's `Name` parameter
2. Ensure URL query parameters use that exact name
3. Test the full flow: click link → observe URL → verify results load

This is a classic "integration seam" bug - both sides worked correctly in isolation, but the contract between them was wrong.

---

## Issue #598: Command Name Not Displaying

### Problem

When searching for commands, the Commands section showed only the module badge (e.g., "GeneralModule") without the command name (`/ping`).

### Analysis

The data was being populated correctly:
- `SearchService.cs` set `Title = $"/{x.Command.FullName}"` (e.g., "/ping")
- `BadgeText` was set to `x.Command.ModuleName` (e.g., "GeneralModule")
- The badge was rendering, confirming data was flowing through

The Commands section used the highlight tag helper:

```razor
<span class="font-mono..."><highlight text="@cmd.Title" search-term="@Model.ViewModel.SearchTerm" /></span>
```

While Command Logs (which worked) used direct output:

```razor
<span class="font-mono...">/@log.CommandName</span>
```

### Root Cause

The `<highlight>` tag helper was producing empty output for command titles. The exact cause wasn't definitively identified but likely relates to:
- How the tag helper handles text starting with `/`
- Potential HTML encoding interactions with the slash character
- Regex pattern matching edge cases

### Fix

Removed the highlight tag helper for command titles, using direct output instead:

```razor
<!-- Before -->
<span class="..."><highlight text="@cmd.Title" search-term="..." /></span>

<!-- After -->
<span class="...">@cmd.Title</span>
```

Description highlighting was kept since it worked correctly.

### Lesson

**When debugging rendering issues, compare working vs non-working patterns.**

The Command Logs section rendered command names correctly without the highlight helper. Rather than debugging the tag helper edge case, the simpler solution was to match the working pattern.

**Pragmatism over perfection:** Highlighting the command name is nice-to-have, but displaying it at all is essential. The fix prioritized correctness over completeness.

---

## What Went Right

1. **Clear issue descriptions** - Both issues included screenshots, root cause analysis, and suggested fixes
2. **Quick diagnosis** - Reading the issue description pointed directly to the problem code
3. **Minimal changes** - Both fixes were under 10 lines total
4. **No regression** - All existing search tests passed

---

## Process Observations

### Testing Gap

Neither bug was caught before release because:
- Unit tests don't catch querystring parameter mismatches (backend tests use the C# property directly)
- The highlight tag helper wasn't tested with slash-prefixed text

**Action:** Consider adding integration tests for search functionality that:
- Navigate to `/Search?q=term` and verify results appear
- Verify search result content matches expected format

### Feature Complexity

The global search feature (#328) touched multiple areas:
- Backend: SearchService, multiple repositories
- Frontend: search.js, Search.cshtml, navbar components
- Both desktop and mobile interfaces

With this surface area, some bugs slipping through is expected.

---

## Checklist for Search Features

- [ ] Verify querystring parameter names match between JS and C# `[BindProperty]`
- [ ] Test recent searches feature (click saved search → verify results load)
- [ ] Test mobile search flow end-to-end
- [ ] Verify all search result types render title, description, and badge
- [ ] Compare rendering patterns between similar sections

---

## Files Modified

- `src/DiscordBot.Bot/wwwroot/js/search.js` - 3 querystring parameter fixes
- `src/DiscordBot.Bot/Pages/Search.cshtml` - Remove highlight helper from command title
