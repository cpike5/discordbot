# Lessons Learned: Issues #597 & #598 - Global Search Bug Fixes

**Date:** 2026-01-02
**Issues:**
- [#597 - Bug: Global search querystring parameter mismatch - 'query' vs 'q'](https://github.com/cpike5/discordbot/issues/597)
- [#598 - Bug: Command search shows module name badge only, not the matched command name](https://github.com/cpike5/discordbot/issues/598)
**PR:** [#619](https://github.com/cpike5/discordbot/pull/619)

**Follow-up fix:** `3d6cb4d fix: set TagMode.StartTagAndEndTag on HighlightTagHelper to render content`

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

### Root Cause (initial understanding)

The `<highlight>` tag helper was producing empty output for command titles. At the time of the fix, the exact cause was not identified. The working theory was that the slash prefix (`/`) on command names caused an edge case in the regex or HTML encoding logic.

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

> **Note:** The true root cause was identified in a subsequent review and is documented below in [Root Cause Confirmed: ASP.NET Core TagMode.SelfClosing](#root-cause-confirmed-aspnet-core-tagmodeselfclosing). The slash prefix was a red herring.

---

## Root Cause Confirmed: ASP.NET Core TagMode.SelfClosing

**Commit:** `3d6cb4d fix: set TagMode.StartTagAndEndTag on HighlightTagHelper to render content`

**Date:** 2026-02-19

### Problem

During a broader search system review, it was confirmed that **all** `<highlight />` usages in search result templates were producing empty `<span></span>` elements — not just the command title. Titles and descriptions in every category where the tag helper was used rendered blank in the browser despite containing correct data throughout the entire C# pipeline.

Symptoms:
- Search result cards appeared with no title or description text
- The surrounding markup (badges, icons, links) rendered correctly
- Inspecting the HTML showed `<span></span>` elements with no inner content
- Server-side debugging showed `Text` and `SearchTerm` properties populated correctly on the tag helper instance
- The `TextHighlightHelper` methods produced correct HTML strings when called directly

The data was correct at every observable point in the pipeline. The output was silently empty.

### Why Debugging Was Misleading

The natural places to look first were all innocent:
1. `SearchService` — data was correctly populated in `Title` and `Description`
2. `SearchResultItemDto` — values were non-null and non-empty when passed to the view
3. `TextHighlightHelper` — produced correct `<mark>`-wrapped HTML strings
4. The Razor template — the tag helper attributes were correctly bound

Because every intermediate step looked correct, the failure appeared to be happening "between" steps rather than at any single identifiable point, which made isolation difficult.

### Root Cause

**ASP.NET Core tag helpers default to `TagMode.SelfClosing` when the element uses self-closing syntax.**

When a tag helper is written as `<highlight ... />`, the framework processes it in `TagMode.SelfClosing` by default. In this mode, the tag helper's `output.Content` is **silently discarded** — any call to `SetHtmlContent()` or `SetContent()` has no effect on the final rendered output.

The `HighlightTagHelper.Process` method was calling:

```csharp
output.TagName = "span";
// TagMode not set — defaults to SelfClosing due to self-closing usage syntax
output.Content.SetHtmlContent(highlighted); // silently discarded
```

The rendered output was therefore an empty self-closing `<span />` element, which browsers normalise to `<span></span>`.

This behaviour is documented in the ASP.NET Core source but not prominently flagged as a potential pitfall. The framework produces no warning, no exception, and no indication that content was discarded.

### Fix

One line added to `HighlightTagHelper.Process`:

```csharp
public override void Process(TagHelperContext context, TagHelperOutput output)
{
    output.TagName = "span";
    output.TagMode = TagMode.StartTagAndEndTag; // added — without this, SetHtmlContent is silently discarded
    // ...
    output.Content.SetHtmlContent(highlighted);
}
```

**File:** `src/DiscordBot.Bot/TagHelpers/HighlightTagHelper.cs`

### Rule for All Future Tag Helpers

> **Any tag helper that sets content via `SetHtmlContent()` or `SetContent()` MUST explicitly set `output.TagMode = TagMode.StartTagAndEndTag` in its `Process` method.**
>
> This is required regardless of whether the tag is used with self-closing or paired syntax in templates, because `TagMode` defaults are controlled by the ASP.NET Core framework based on how the element appears at the call site, not by the tag helper itself.

### Why the Command Title Seemed Different

The original workaround for Issue #598 removed `<highlight>` from command titles because those appeared blank. The description field (which also used `<highlight>`) was noted as "working correctly" at the time — but this was incorrect. Both were producing empty output. The description field was simply less visually obvious as broken because it is rendered at smaller size and lower visual prominence than the title.

The slash prefix (`/`) on command names was a red herring and had no bearing on the failure.

### Checklist Addition

See updated checklist below.

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
- [ ] Any new `<highlight />` usage must have `output.TagMode = TagMode.StartTagAndEndTag` set in the tag helper's `Process` method

## Checklist for New Tag Helpers

- [ ] If the tag helper renders content via `SetHtmlContent()` or `SetContent()`, set `output.TagMode = TagMode.StartTagAndEndTag` explicitly
- [ ] Write a smoke test that verifies rendered output is non-empty for a known-good input
- [ ] If the element can be used with self-closing syntax, verify output in browser developer tools — an empty `<tagname></tagname>` with no inner text is a strong signal that `TagMode` is wrong

---

## Files Modified

- `src/DiscordBot.Bot/wwwroot/js/search.js` - 3 querystring parameter fixes (PR #619)
- `src/DiscordBot.Bot/Pages/Search.cshtml` - Workaround: removed highlight helper from command title (PR #619)
- `src/DiscordBot.Bot/TagHelpers/HighlightTagHelper.cs` - Added `output.TagMode = TagMode.StartTagAndEndTag` (commit `3d6cb4d`)
