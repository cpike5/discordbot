# Audio Portal UX Batch 2 - Implementation Plan

**Date:** 2026-03-22
**Issues:** #1781, #1780, #1786, #1789, #1788, #1777, #1790
**Milestone:** Audio Portal UX Improvements

## 1. Requirement Summary

Seven issues spanning mobile UX improvements, history/replay features, soundboard categorization, and portal polish. The work breaks into three themes: (A) Mobile and Discoverability, (B) History and Replay / Data, (C) Consistency and Polish. Two issues require new database entities and migrations (#1788, #1777); the rest are frontend-only.

## 2. Dependency Graph

```
#1781 (Tab Subtitles) ──────────────────────────────────────┐
                                                             │
#1780 (VoicePanel Consistency) ─────────────────────────────┤
                                                             │
#1786 (Mobile UX Pass) ←── depends on #1780, #1781, #1789  │
                                                             │
#1789 (VOX Mobile Clip Browser) ────────────────────────────┤
                                                             │
#1788 (TTS History + Replay) ───────────────────────────────┤
                                                             │
#1777 (Sound Categories) ───────────────────────────────────┤
                                                             │
#1790 (Keyboard Shortcuts) ←── should be last (polish)      │
```

**Parallel-safe groups:**
- **Wave 1 (parallel):** #1781, #1780, #1789, #1788-backend, #1777-backend
- **Wave 2 (parallel, after Wave 1):** #1788-frontend (needs backend), #1777-frontend (needs backend), #1786 (needs #1780, #1781, #1789 merged)
- **Wave 3:** #1790 (after all others merged)

## 3. Shared Infrastructure (New Entities + Migrations)

### 3a. TtsMessageHistory Entity (for #1788)

**Pattern to follow:** `src/DiscordBot.Core/Entities/VoxMessageHistory.cs` (identical pattern, different fields)

New entity at `src/DiscordBot.Core/Entities/TtsMessageHistory.cs`:
```
- Id (int, PK)
- GuildId (ulong)
- UserId (ulong)
- Message (string)
- VoiceName (string) - e.g., "en-US-AriaNeural"
- Style (string?) - e.g., "cheerful"
- Speed (decimal) - e.g., 1.0
- Pitch (decimal) - e.g., 1.0
- IsFavorite (bool)
- PlayedAt (DateTime, UTC)
- Guild (navigation)
```

### 3b. SoundCategory Entity (for #1777)

New entity at `src/DiscordBot.Core/Entities/SoundCategory.cs`:
```
- Id (int, PK)
- GuildId (ulong)
- Name (string, max 50)
- SortOrder (int, default 0)
- CreatedAt (DateTime, UTC)
- Guild (navigation)
- Sounds (ICollection<Sound>, navigation)
```

Modify `src/DiscordBot.Core/Entities/Sound.cs`:
- Add `CategoryId (int?, FK to SoundCategory)`
- Add `Category (SoundCategory?, navigation)`

### 3c. Migration Requirements

Both entities require migrations for **both** providers:

```bash
# TtsMessageHistory
dotnet ef migrations add AddTtsMessageHistory --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot --context SqliteBotDbContext -o Migrations/Sqlite
dotnet ef migrations add AddTtsMessageHistory --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot --context PostgresBotDbContext -o Migrations/Postgresql

# SoundCategory + Sound.CategoryId
dotnet ef migrations add AddSoundCategories --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot --context SqliteBotDbContext -o Migrations/Sqlite
dotnet ef migrations add AddSoundCategories --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot --context PostgresBotDbContext -o Migrations/Postgresql
```

---

## 4. Issue-by-Issue Implementation Details

---

### Issue #1781 - F19: Feature Descriptions/Subtitles on Audio Tabs

**Scope:** Small (frontend-only)
**Agent:** dotnet-specialist

#### Files to Modify

| File | Action |
|------|--------|
| `src/DiscordBot.Bot/ViewModels/Components/TabPanelViewModel.cs` | Add `Subtitle` property to `TabItemViewModel` |
| `src/DiscordBot.Bot/Pages/Portal/Shared/_PortalHeader.cshtml` | Render subtitle under each tab label |
| `src/DiscordBot.Bot/wwwroot/css/tab-panel.css` | Add `.tab-subtitle` styles for Portal variant |

#### Implementation Details

1. **Add `Subtitle` property** to `TabItemViewModel` (line 105 of `TabPanelViewModel.cs`):
   ```csharp
   public string? Subtitle { get; init; }
   public bool HasSubtitle => !string.IsNullOrEmpty(Subtitle);
   ```

2. **Update `_PortalHeader.cshtml`** (line 14-36): Add subtitles to the tab items:
   - Soundboard: `"Play uploaded sounds"`
   - Text-to-Speech: `"Text-to-speech with voices"`
   - VOX: `"Half-Life style announcements"`

3. **Update the `_TabPanel.cshtml` partial** to render the subtitle when present. Look for how the label is rendered and add a `<span class="tab-subtitle">` below it, conditioned on `tab.HasSubtitle`.

4. **CSS in `tab-panel.css`**: Add under the `.tab-panel-portal` section:
   ```css
   .tab-panel-portal .tab-subtitle {
       display: block;
       font-size: 0.65rem;
       color: var(--color-text-tertiary);
       font-weight: 400;
       margin-top: 0.125rem;
   }
   ```

5. **Responsive**: On mobile (< 768px), hide subtitles or ensure they don't break layout:
   ```css
   @media (max-width: 767px) {
       .tab-panel-portal .tab-subtitle { display: none; }
   }
   ```

#### Acceptance Criteria
- [ ] Each audio portal tab shows a descriptive subtitle
- [ ] Subtitles are concise (under 30 characters)
- [ ] Subtitles hidden on mobile viewports to prevent layout breakage

---

### Issue #1780 - F18: Consistent VoiceChannelPanel Across All Audio Features

**Scope:** Small (CSS + minor Razor changes)
**Agent:** dotnet-specialist

#### Files to Modify

| File | Action |
|------|--------|
| `src/DiscordBot.Bot/Pages/Shared/Components/_VoiceChannelPanel.cshtml` | Remove the `display: none` rule on `.voice-queue-section` in compact mode |

#### Implementation Details

1. **Remove the queue-hiding CSS rule.** In `_VoiceChannelPanel.cshtml` (line 86-88), remove:
   ```css
   #voice-channel-panel.voice-panel-compact .voice-queue-section {
       display: none;
   }
   ```

2. **Verify** all three portal pages already set `IsCompact = true`, `ShowNowPlaying = true` on VoicePanel construction:
   - `src/DiscordBot.Bot/Pages/Portal/Soundboard/Index.cshtml.cs` (line 177) -- confirmed: `IsCompact = true, ShowNowPlaying = true`
   - `src/DiscordBot.Bot/Pages/Portal/TTS/Index.cshtml.cs` (line 158) -- confirmed: `IsCompact = true, ShowNowPlaying = true`
   - `src/DiscordBot.Bot/Pages/Portal/VOX/Index.cshtml.cs` (line 142) -- confirmed: `IsCompact = true, ShowNowPlaying = true`

3. No page model changes needed. The voice panel partial is shared. Just removing the CSS hiding rule makes queue visible everywhere.

4. **Optional polish**: Add compact styling for the queue section (smaller font, less padding) since it was previously hidden in compact mode. Add to the compact styles block:
   ```css
   #voice-channel-panel.voice-panel-compact .voice-queue-section {
       padding: 0.5rem 0;
   }
   #voice-channel-panel.voice-panel-compact .voice-queue-section h4 {
       font-size: 0.6rem;
   }
   ```

#### Acceptance Criteria
- [ ] Voice panel uses same mode on all three audio pages
- [ ] Queue section visible on all three pages
- [ ] Now Playing visible on all three pages

---

### Issue #1789 - F26: VOX Mobile Clip Browser

**Scope:** Medium (frontend-only, significant JS + CSS)
**Agent:** dotnet-specialist

#### Files to Modify

| File | Action |
|------|--------|
| `src/DiscordBot.Bot/Pages/Portal/VOX/Index.cshtml` | Add mobile clip browser UI (horizontal letter strip + search) |

#### Implementation Details

The current VOX page hides the A-Z rail below 768px (`_VoiceChannelPanel.cshtml` line 777-780 equivalent in VOX's inline styles). Mobile users have zero clip browsing ability.

**Approach: Horizontal scrollable letter strip + prominent search input**

1. **Add mobile clip browser section** in the VOX cshtml. Above the existing clip browser container (around line 2214 in VOX `Index.cshtml`), add a mobile-only section:
   ```html
   <!-- Mobile Clip Browser (visible < 768px, hidden on desktop) -->
   <div class="vox-mobile-browser">
       <div class="vox-mobile-search">
           <input type="text" id="voxMobileSearch" placeholder="Search clips..."
                  class="vox-mobile-search-input" aria-label="Search VOX clips">
       </div>
       <div class="vox-mobile-letter-strip" id="voxMobileLetterStrip" role="tablist"
            aria-label="Browse clips by letter">
           <!-- A-Z letters populated by JavaScript -->
       </div>
   </div>
   ```

2. **CSS additions** (inline in VOX page styles):
   ```css
   .vox-mobile-browser {
       display: none; /* Hidden on desktop */
   }
   @media (max-width: 767px) {
       .vox-mobile-browser {
           display: flex;
           flex-direction: column;
           gap: 0.5rem;
           margin-bottom: 0.75rem;
       }
       .vox-mobile-search-input {
           width: 100%;
           min-height: 44px; /* Touch target */
           padding: 0.5rem 0.75rem;
           /* standard form input styling */
       }
       .vox-mobile-letter-strip {
           display: flex;
           overflow-x: auto;
           gap: 0.25rem;
           padding: 0.25rem 0;
           -webkit-overflow-scrolling: touch;
       }
       .vox-mobile-letter-strip button {
           min-width: 36px;
           min-height: 36px;
           flex-shrink: 0;
           /* touch-friendly letter buttons */
       }
   }
   ```

3. **JavaScript updates** in the VOX page's inline `<script>` section:
   - Initialize mobile letter strip from same data as desktop A-Z rail
   - Wire mobile search input to existing clip filtering logic
   - Sync active letter between mobile strip and desktop rail
   - Mobile letter tap scrolls clip grid to that section

4. **Keep desktop A-Z rail unchanged** - this only adds a mobile alternative.

#### Acceptance Criteria
- [ ] Mobile users (< 768px) can browse VOX clips by letter via horizontal scrollable strip
- [ ] Mobile search input filters clips
- [ ] Touch-friendly (44px minimum touch targets)
- [ ] Desktop experience unchanged

---

### Issue #1786 - F23: Mobile UX Pass for Audio Portal

**Scope:** Large (CSS across all three portal pages)
**Agent:** dotnet-specialist

**Dependencies:** Should be done after #1780, #1781, #1789 are merged.

#### Files to Modify

| File | Action |
|------|--------|
| `src/DiscordBot.Bot/wwwroot/css/portal.css` | Add/update mobile responsive rules |
| `src/DiscordBot.Bot/Pages/Portal/Soundboard/Index.cshtml` | Mobile upload button, touch targets |
| `src/DiscordBot.Bot/Pages/Portal/TTS/Index.cshtml` | Fix emphasis toolbar positioning |
| `src/DiscordBot.Bot/Pages/Portal/VOX/Index.cshtml` | Mobile clip browsing enhancements |
| `src/DiscordBot.Bot/Pages/Shared/Components/_VoiceChannelPanel.cshtml` | Sticky/collapsible voice panel on mobile |

#### Implementation Details - 5 Items

**Item 1: Hide drag-drop zone on touch, show upload button (Soundboard)**

In `Portal/Soundboard/Index.cshtml`, the drag-drop upload zone should be hidden on touch devices. Add CSS:
```css
@media (hover: none) and (pointer: coarse) {
    .upload-drop-zone { display: none; }
    .upload-btn-mobile { display: flex; }
}
```
Add a prominent `<button class="upload-btn-mobile">` that triggers the same file input. Style with 44px height, full width.

**Item 2: Increase touch targets to 44px (Soundboard)**

In the soundboard grid cards, ensure interactive elements (play button, favorite button, etc.) have `min-height: 44px; min-width: 44px;` on mobile. Add to `portal.css`:
```css
@media (max-width: 767px) {
    .sound-card-action { min-height: 44px; min-width: 44px; }
}
```

**Item 3: VOX mobile clip browser**

Already handled by #1789. This item validates it works correctly within the broader mobile pass.

**Item 4: Sticky/collapsible voice panel on mobile**

In `_VoiceChannelPanel.cshtml` or `portal.css`, make the voice panel collapsible on mobile to save screen space. Approach:
- On mobile, the sidebar becomes a top-of-page collapsible panel
- Default collapsed, showing only connection status badge
- Tap to expand for channel selection and queue

Add to `portal.css`:
```css
@media (max-width: 1023px) {
    .sidebar {
        position: sticky;
        top: 0;
        z-index: 10;
    }
}
```

Add Alpine.js `x-data="{ voicePanelOpen: false }"` to the voice panel wrapper on mobile, with a toggle button.

**Item 5: Fix emphasis toolbar positioning (TTS Pro mode)**

In `Portal/TTS/Index.cshtml`, the emphasis toolbar (`#portalEmphasisToolbarContainer`) needs responsive positioning. Add:
```css
@media (max-width: 767px) {
    .emphasis-toolbar {
        position: static;
        width: 100%;
        flex-wrap: wrap;
    }
    .emphasis-toolbar button {
        min-height: 44px;
        min-width: 44px;
    }
}
```

#### Acceptance Criteria
- [ ] Drag-drop zone hidden on touch devices; file picker button prominent
- [ ] All interactive elements meet 44px minimum touch target
- [ ] VOX clips browsable on mobile (verified via #1789)
- [ ] Voice panel doesn't consume excessive screen space on mobile
- [ ] TTS emphasis toolbar usable on small viewports

---

### Issue #1788 - F25: TTS Message History with Replay

**Scope:** Large (full-stack: entity, repo, API, frontend)
**Agent:** data-infrastructure (backend), dotnet-specialist (frontend)

#### Backend Files to Create/Modify

| File | Action |
|------|--------|
| `src/DiscordBot.Core/Entities/TtsMessageHistory.cs` | **Create** - new entity |
| `src/DiscordBot.Core/Interfaces/ITtsMessageHistoryRepository.cs` | **Create** - repository interface |
| `src/DiscordBot.Infrastructure/Data/Repositories/TtsMessageHistoryRepository.cs` | **Create** - repository implementation |
| `src/DiscordBot.Infrastructure/Data/BotDbContext.cs` | Add `DbSet<TtsMessageHistory>` |
| `src/DiscordBot.Infrastructure/Extensions/ServiceCollectionExtensions.cs` | Register `ITtsMessageHistoryRepository` |
| `src/DiscordBot.Bot/Controllers/PortalTtsController.cs` | Add history API endpoints |
| Migrations (SQLite + PostgreSQL) | Generate for both providers |

#### Frontend Files to Modify

| File | Action |
|------|--------|
| `src/DiscordBot.Bot/Pages/Portal/TTS/Index.cshtml` | Add history sidebar panel |
| `src/DiscordBot.Bot/wwwroot/js/portal-tts.js` | Add history fetch, replay, edit-load logic |

#### Backend Implementation

1. **Entity** (`TtsMessageHistory.cs`): Follow `VoxMessageHistory.cs` pattern exactly. Fields listed in Section 3a above.

2. **Repository Interface** (`ITtsMessageHistoryRepository.cs`): Mirror `IVoxMessageHistoryRepository.cs`:
   ```csharp
   public interface ITtsMessageHistoryRepository : IRepository<TtsMessageHistory>
   {
       Task<IReadOnlyList<TtsMessageHistory>> GetRecentAsync(ulong userId, ulong guildId, int limit = 20, CancellationToken ct = default);
       Task<IReadOnlyList<TtsMessageHistory>> GetFavoritesAsync(ulong userId, ulong guildId, CancellationToken ct = default);
       Task SetFavoriteAsync(int id, bool isFavorite, CancellationToken ct = default);
   }
   ```

3. **Repository Implementation** (`TtsMessageHistoryRepository.cs`): Follow `VoxMessageHistoryRepository.cs` pattern exactly (same structure, tracing, logging).

4. **DbContext** (`BotDbContext.cs`): Add after line 77:
   ```csharp
   public DbSet<TtsMessageHistory> TtsMessageHistory => Set<TtsMessageHistory>();
   ```

5. **DI Registration** (`ServiceCollectionExtensions.cs`): Add after line 120:
   ```csharp
   services.AddScoped<ITtsMessageHistoryRepository, TtsMessageHistoryRepository>();
   ```

6. **API Endpoints** in `PortalTtsController.cs`:
   ```
   GET  /api/portal/tts/{guildId}/history        → Get recent messages (last 20)
   POST /api/portal/tts/{guildId}/history         → Save a new history entry (called after send)
   POST /api/portal/tts/{guildId}/history/{id}/replay → Replay with original settings
   PUT  /api/portal/tts/{guildId}/history/{id}/favorite → Toggle favorite
   DELETE /api/portal/tts/{guildId}/history/{id}  → Delete history entry
   ```

   The `replay` endpoint should reuse the existing `/api/portal/tts/{guildId}/send` logic but accept settings from the history entry.

   **Important:** Extract Discord user ID from claims as `User.FindFirst("discord:user_id")?.Value` and parse to `ulong`. See pattern in `PortalVoxController.cs`.

7. **Migrations:** Generate for both SQLite and PostgreSQL (commands in Section 3c).

#### Frontend Implementation

1. **History Panel** in TTS `Index.cshtml`: Add a collapsible history section below the TTS form, or as a right sidebar on desktop / bottom panel on mobile:
   ```html
   <div class="tts-history-panel" id="ttsHistoryPanel">
       <h3>Recent Messages</h3>
       <div id="ttsHistoryList"></div>
   </div>
   ```

2. **JavaScript** in `portal-tts.js`:
   - On page load, fetch history via `GET /api/portal/tts/{guildId}/history`
   - Render each entry with: message text (truncated), voice name, timestamp, replay button, edit button, favorite toggle
   - **Replay button**: POST to `/history/{id}/replay` (sends with original voice/style/speed/pitch)
   - **Edit button**: Load message text + settings back into the TTS form fields
   - **After successful send**: POST to `/history` to save the entry, prepend to list
   - Discord IDs passed as strings (guildId already is `window.guildId = '@Model.GuildId'`)

#### Acceptance Criteria
- [ ] Recent TTS messages stored and displayed (last 20)
- [ ] One-click replay with original settings (voice, style, speed, pitch)
- [ ] Edit button loads message back into composer with all settings
- [ ] History is per-user, per-guild

---

### Issue #1777 - F15: Sound Categories/Tags for Soundboard

**Scope:** Large (full-stack: entity, repo, API, admin UI, portal filter)
**Agent:** data-infrastructure (backend), dotnet-specialist (frontend)

#### Backend Files to Create/Modify

| File | Action |
|------|--------|
| `src/DiscordBot.Core/Entities/SoundCategory.cs` | **Create** - new entity |
| `src/DiscordBot.Core/Entities/Sound.cs` | Add `CategoryId` FK and navigation property |
| `src/DiscordBot.Core/Interfaces/ISoundCategoryRepository.cs` | **Create** - repository interface |
| `src/DiscordBot.Infrastructure/Data/Repositories/SoundCategoryRepository.cs` | **Create** - repository implementation |
| `src/DiscordBot.Infrastructure/Data/BotDbContext.cs` | Add `DbSet<SoundCategory>`, configure relationship |
| `src/DiscordBot.Infrastructure/Extensions/ServiceCollectionExtensions.cs` | Register repository |
| `src/DiscordBot.Bot/Controllers/PortalSoundboardController.cs` | Add category filter endpoint, category CRUD |
| `src/DiscordBot.Bot/ViewModels/Portal/PortalSoundViewModel.cs` | Add `CategoryId`, `CategoryName` properties |
| Migrations (SQLite + PostgreSQL) | Generate for both providers |

#### Frontend Files to Modify

| File | Action |
|------|--------|
| `src/DiscordBot.Bot/Pages/Portal/Soundboard/Index.cshtml` | Category filter bar, category badge on cards |
| `src/DiscordBot.Bot/Pages/Portal/Soundboard/Index.cshtml.cs` | Load categories, pass to view |
| `src/DiscordBot.Bot/Pages/Guilds/Soundboard/Index.cshtml` | Admin category management UI |
| `src/DiscordBot.Bot/Pages/Guilds/Soundboard/Index.cshtml.cs` | Admin category CRUD handlers |

#### Backend Implementation

1. **Entity** (`SoundCategory.cs`): See Section 3b.

2. **Modify Sound entity** (`Sound.cs`): Add after `PlayCount` property:
   ```csharp
   public int? CategoryId { get; set; }
   public SoundCategory? Category { get; set; }
   ```

3. **Repository Interface** (`ISoundCategoryRepository.cs`):
   ```csharp
   public interface ISoundCategoryRepository : IRepository<SoundCategory>
   {
       Task<IReadOnlyList<SoundCategory>> GetByGuildAsync(ulong guildId, CancellationToken ct = default);
       Task ReorderAsync(ulong guildId, IEnumerable<(int Id, int SortOrder)> ordering, CancellationToken ct = default);
   }
   ```

4. **Repository Implementation** (`SoundCategoryRepository.cs`): Follow `VoxMessageHistoryRepository.cs` for structure (tracing, logging, slow operation checks).

5. **DbContext**: Add `DbSet<SoundCategory>` and configure the Sound-SoundCategory relationship in `OnModelCreating`:
   ```csharp
   modelBuilder.Entity<Sound>()
       .HasOne(s => s.Category)
       .WithMany(c => c.Sounds)
       .HasForeignKey(s => s.CategoryId)
       .OnDelete(DeleteBehavior.SetNull);
   ```

6. **API Endpoints** in `PortalSoundboardController.cs`:
   ```
   GET  /api/portal/soundboard/{guildId}/categories             → List categories
   POST /api/portal/soundboard/{guildId}/categories             → Create category (admin only)
   PUT  /api/portal/soundboard/{guildId}/categories/{id}        → Update category (admin only)
   DELETE /api/portal/soundboard/{guildId}/categories/{id}      → Delete category (admin only)
   PUT  /api/portal/soundboard/{guildId}/sounds/{soundId}/category → Assign sound to category
   ```

   For admin-only endpoints, check `User.IsInRole(IdentitySeeder.Roles.Admin)` or add `[Authorize(Roles = "Admin,SuperAdmin")]`.

7. **Update existing sounds endpoint**: Modify `GET /api/portal/soundboard/{guildId}/sounds` to include `CategoryId` and `CategoryName` in the response. Add optional `?categoryId=` query parameter for filtering.

8. **Update `PortalSoundViewModel.cs`**:
   ```csharp
   public int? CategoryId { get; set; }
   public string? CategoryName { get; set; }
   ```

#### Frontend Implementation

1. **Category filter bar** in `Portal/Soundboard/Index.cshtml`: Add a horizontal scrollable pill bar above the sound grid:
   ```html
   <div class="category-filter-bar">
       <button class="category-pill active" data-category="">All</button>
       <button class="category-pill" data-category="uncategorized">Uncategorized</button>
       <!-- categories populated from API -->
   </div>
   ```

2. **Category badge** on sound cards: Show the category name as a small badge/tag.

3. **Admin category management**: In the admin Soundboard page (`Pages/Guilds/Soundboard/Index.cshtml`), add a collapsible "Manage Categories" section with create/edit/delete/reorder.

4. **Category assignment**: In the admin sound management view, add a category dropdown to the sound edit form or a batch-assign feature.

#### Acceptance Criteria
- [ ] Admins can create/edit/delete categories
- [ ] Sounds can be assigned to a category
- [ ] Users can filter the soundboard grid by category
- [ ] "All" / "Uncategorized" options exist
- [ ] Category assignment visible on sound cards

---

### Issue #1790 - F27: Keyboard Shortcuts for Audio Portal

**Scope:** Medium (frontend-only, new shared JS module)
**Agent:** dotnet-specialist

#### Files to Create/Modify

| File | Action |
|------|--------|
| `src/DiscordBot.Bot/wwwroot/js/shared/keyboard-shortcuts.js` | **Create** - shared keyboard shortcut manager |
| `src/DiscordBot.Bot/Pages/Portal/Soundboard/Index.cshtml` | Register soundboard shortcuts, add help overlay |
| `src/DiscordBot.Bot/Pages/Portal/TTS/Index.cshtml` | Register TTS shortcuts |
| `src/DiscordBot.Bot/Pages/Portal/VOX/Index.cshtml` | Register VOX shortcuts |
| `src/DiscordBot.Bot/Pages/Portal/_PortalLayout.cshtml` | Include shared shortcut JS, add help overlay partial |
| `src/DiscordBot.Bot/wwwroot/css/portal.css` | Keyboard shortcut help overlay styles |

#### Implementation Details

1. **Shared Keyboard Shortcut Manager** (`keyboard-shortcuts.js`):
   ```javascript
   const KeyboardShortcuts = (function() {
       let shortcuts = [];
       let helpOverlayVisible = false;

       function register(key, description, callback, options = {}) {
           // options: { ctrlKey, shiftKey, category }
           shortcuts.push({ key, description, callback, ...options });
       }

       function handleKeydown(e) {
           // Skip when typing in input/textarea (except Ctrl combos)
           if (['INPUT', 'TEXTAREA', 'SELECT'].includes(e.target.tagName)) {
               if (!e.ctrlKey && !e.metaKey) return;
           }
           // Match and fire
       }

       function showHelp() { /* render overlay from shortcuts array */ }
       function hideHelp() { /* hide overlay */ }

       return { register, init, showHelp, hideHelp };
   })();
   ```

2. **Soundboard shortcuts:**
   - `1-9`: Quick-play first 9 favorites (if favorites exist)
   - `/`: Focus search input
   - `?`: Show help overlay

3. **VOX shortcuts:**
   - `Ctrl+Enter`: Play message
   - `/`: Focus search/message input
   - `?`: Show help overlay

4. **TTS shortcuts:**
   - `Ctrl+Enter`: Send message
   - `?`: Show help overlay
   - (Ctrl+B/E for emphasis already exist in Pro mode)

5. **Help overlay**: A modal/overlay listing all available shortcuts for the current page. Styled consistently with portal design.

6. **Include in layout** (`_PortalLayout.cshtml`): Add before `@await RenderSectionAsync("Scripts"...)`:
   ```html
   <script src="~/js/shared/keyboard-shortcuts.js" asp-append-version="true"></script>
   ```

7. **No browser conflicts**: Avoid `Ctrl+S`, `Ctrl+F`, `Ctrl+W`, `F5`, etc. Test that `?` doesn't fire in input fields.

#### Acceptance Criteria
- [ ] Keyboard shortcuts work on all three audio pages
- [ ] Help overlay accessible via `?` key
- [ ] No conflicts with browser defaults
- [ ] Shortcuts disabled when typing in input fields (except Ctrl combos)

---

## 5. Execution Order

### Wave 1 - Independent Work (All Parallel)

| Issue | Agent | Est. Effort | Notes |
|-------|-------|-------------|-------|
| #1781 Tab Subtitles | dotnet-specialist | Small | Frontend only, touches shared TabItemViewModel |
| #1780 VoicePanel Consistency | dotnet-specialist | Small | One CSS rule removal |
| #1789 VOX Mobile Clip Browser | dotnet-specialist | Medium | Frontend only, isolated to VOX page |
| #1788 Backend (entity, repo, migrations, API) | data-infrastructure | Medium | Database layer, follow VoxMessageHistory pattern |
| #1777 Backend (entity, repo, migrations, API) | data-infrastructure | Medium | Database layer, new entity + Sound FK |

**Conflict risk:** Low. #1781 and #1780 both touch portal shared files but different concerns (tabs vs voice panel).

### Wave 2 - Dependent on Wave 1

| Issue | Agent | Depends On | Notes |
|-------|-------|------------|-------|
| #1788 Frontend (TTS history UI) | dotnet-specialist | #1788 backend | Needs API endpoints available |
| #1777 Frontend (category filter UI, admin CRUD) | dotnet-specialist | #1777 backend | Needs API and entity available |
| #1786 Mobile UX Pass | dotnet-specialist | #1780, #1781, #1789 | Cross-cutting mobile polish needs prior changes merged |

### Wave 3 - Final Polish

| Issue | Agent | Depends On | Notes |
|-------|-------|------------|-------|
| #1790 Keyboard Shortcuts | dotnet-specialist | All others | Self-contained but should be last to avoid rebasing |

---

## 6. Key File Reference

| Layer | Key Files |
|-------|-----------|
| Portal Pages | `src/DiscordBot.Bot/Pages/Portal/{Soundboard,TTS,VOX}/Index.cshtml[.cs]` |
| Portal Layout | `src/DiscordBot.Bot/Pages/Portal/_PortalLayout.cshtml` |
| Portal Header | `src/DiscordBot.Bot/Pages/Portal/Shared/_PortalHeader.cshtml` |
| Portal Base Model | `src/DiscordBot.Bot/Pages/Portal/PortalPageModelBase.cs` |
| Voice Panel Partial | `src/DiscordBot.Bot/Pages/Shared/Components/_VoiceChannelPanel.cshtml` |
| Voice Panel VM | `src/DiscordBot.Bot/ViewModels/Components/VoiceChannelPanelViewModel.cs` |
| Tab Panel VM | `src/DiscordBot.Bot/ViewModels/Components/TabPanelViewModel.cs` |
| Portal Header VM | `src/DiscordBot.Bot/ViewModels/Portal/PortalHeaderViewModel.cs` |
| Portal Sound VM | `src/DiscordBot.Bot/ViewModels/Portal/PortalSoundViewModel.cs` |
| Portal CSS | `src/DiscordBot.Bot/wwwroot/css/portal.css` |
| Tab Panel CSS | `src/DiscordBot.Bot/wwwroot/css/tab-panel.css` |
| Portal TTS JS | `src/DiscordBot.Bot/wwwroot/js/portal-tts.js` |
| Voice Panel JS | `src/DiscordBot.Bot/wwwroot/js/voice-channel-panel.js` |
| Shared JS | `src/DiscordBot.Bot/wwwroot/js/shared/` |
| API Controllers | `src/DiscordBot.Bot/Controllers/Portal{Soundboard,Tts,Vox}Controller.cs` |
| Entities | `src/DiscordBot.Core/Entities/{Sound,VoxMessageHistory,UserSoundFavorite}.cs` |
| Interfaces | `src/DiscordBot.Core/Interfaces/I{VoxMessageHistoryRepository,SoundService,SoundRepository}.cs` |
| Repos | `src/DiscordBot.Infrastructure/Data/Repositories/VoxMessageHistoryRepository.cs` (pattern) |
| DbContext | `src/DiscordBot.Infrastructure/Data/BotDbContext.cs` |
| DI Registration | `src/DiscordBot.Infrastructure/Extensions/ServiceCollectionExtensions.cs` |
| Admin Soundboard | `src/DiscordBot.Bot/Pages/Guilds/Soundboard/Index.cshtml[.cs]` |

---

## 7. Risks and Gotchas

### Database Migrations
- **Both providers required.** Every new entity or FK change needs separate SQLite and PostgreSQL migrations. The EF CLI commands require `--context SqliteBotDbContext` or `--context PostgresBotDbContext`.
- **CategoryId FK on Sound** must be nullable (`int?`) with `DeleteBehavior.SetNull` so deleting a category does not cascade-delete sounds.
- **Npgsql legacy timestamp behavior** is enabled at startup. DateTime columns should use `DateTime` (not `DateTimeOffset`). Store UTC.

### Discord Snowflake IDs
- **All JavaScript** must treat Discord IDs (guild ID, user ID) as strings. Existing pattern: `window.guildId = '@Model.GuildId'` (with quotes).
- **API request/response DTOs**: Serialize `ulong` guild/user IDs as strings in JSON if consumed by JavaScript.

### Merge Conflicts
- **portal.css** is touched by #1786 and potentially #1780. Do #1780 first (small, targeted), merge, then #1786.
- **VOX Index.cshtml** is touched by both #1789 and #1786. Do #1789 first (adds mobile browser), merge, then #1786 validates and polishes.
- **Tab-related files** (`TabPanelViewModel.cs`, `_PortalHeader.cshtml`, `tab-panel.css`) only touched by #1781. No conflict risk.

### Touch/Mobile Detection
- For #1786 item 1 (hide drag-drop), use CSS `@media (hover: none) and (pointer: coarse)` rather than JavaScript UA sniffing. This correctly targets touch-primary devices.

### Keyboard Shortcuts (#1790)
- The `?` shortcut must not fire when user is typing in an input field. Check `document.activeElement.tagName` before firing non-Ctrl shortcuts.
- `Ctrl+Enter` in textarea: Some browsers may have default behavior. Use `e.preventDefault()` after matching.
