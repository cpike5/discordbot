# Repeated update-then-delete on one entity, in one Blazor circuit, can crash the circuit

**Symptom.** Building the Phase 4 cluster 4b Playwright round-trip for `Blazor/Pages/Guilds/ScheduledMessages/Index.razor`
(edit → toggle pause → toggle resume → delete, all against the same seeded row), the Delete step
started throwing after the toggle steps were added, with the whole circuit going down:

```
System.InvalidOperationException: The instance of entity type 'ScheduledMessage' cannot be tracked
because another instance with the same key value for {'Id'} is already being tracked. When
attaching existing entities, ensure that only one entity instance with a given key value is
attached.
   at DiscordBot.Infrastructure.Data.Repositories.Repository`1.DeleteAsync(T entity, ...)
   at DiscordBot.Bot.Services.ScheduledMessageService.DeleteAsync(Guid id, ...)
```

`Microsoft.AspNetCore.Components.Server.Circuits.CircuitHost` logs it as an unhandled circuit
exception and tears the circuit down - the browser shows Blazor's generic reconnect/error UI, not
a toast, and no amount of retrying the click helps (a fresh circuit is required).

**Reproduction.** Only reproduces when a scheduled message is **updated more than once** (edit
save, or a pause/resume toggle, or both) and then **deleted, all within the same Blazor circuit**
- i.e. without an intervening full page navigation. A single toggle followed by delete, or a
delete with no prior update in that circuit, does not trigger it.

**Root cause.** `ScheduledMessageRepository.GetByIdAsync` is `DbSet.AsNoTracking().Include(s =>
s.Guild).FirstOrDefaultAsync(...)` - genuinely untracked, and always was.
`Repository<T>.UpdateAsync` calls `DbSet.Update(entity)` unconditionally. `DbSet.Update()` walks
the entity's *whole reachable graph* (the `Include`d `Guild`, not just the `ScheduledMessage` root)
and attaches every node it finds as tracked (`Modified`, since both have non-default keys) - and,
critically, **does not detach any of it after `SaveChangesAsync`**; every node stays tracked
(`Unchanged`) for the rest of the `DbContext`'s lifetime.

That was never a problem for a Razor Page: a Razor Page's own `BotDbContext` lives for exactly one
HTTP request, so every action got a fresh, empty change tracker and never had a second chance to
collide with itself. **A Blazor Server circuit's DI scope, and therefore its `BotDbContext`, lives
for the whole circuit** (docs/plans/blazor-port-plan.md §4.1 "Data access in components") - so a
*second* `GetByIdAsync` (a brand-new, still-untracked `ScheduledMessage` + a brand-new,
still-untracked `Guild` instance - `AsNoTracking` never returns the same instance twice) followed
by `DbSet.Update()` tries to attach two different instances for keys the first `Update()` call
left tracked, and EF's identity map throws "already tracked" - confirmed against a real
SQLite-in-memory repro to happen on the *second* `UpdateAsync` call itself, not deferred to a
later `DeleteAsync` the way the Playwright repro that motivated this note first appeared to show
(that repro's page interactions crossed more full page loads between steps than assumed at first;
the mechanism above was never in doubt, just which call it happened to surface on first).

**First fix tried, and rejected.** An earlier version of this note (and the PR that shipped it)
made `Repository<T>.UpdateAsync`/`DeleteAsync` reconcile instead of blindly attaching: before
calling `DbSet.Update(entity)`/`DbSet.Remove(entity)`, walk `entity`'s reachable graph and detach
any already-tracked entry for a *different* instance sharing a node's key. It worked, and every
existing repository/service test stayed green against it - but a later review caught what that
green run couldn't: the fix is in the wrong place, and it is not safe in general.

- **It can lose a concurrent edit.** `DetachConflictingTrackedEntries` walks the whole tracker and
  detaches *any* entry with a matching key, with no way to tell "a stale instance from an earlier
  call in this circuit" apart from "a different in-flight operation's pending, unsaved edit to the
  same entity". Detaching the latter silently drops that edit - `SaveChangesAsync` simply never
  sees it - instead of the loud, debuggable exception EF throws today. A generic fix that trades a
  loud failure for a silent one is worse than the bug it fixes.
- **It reads keys via reflection**, which breaks for an entity with a shadow key (no CLR property
  EF can reflect `PropertyInfo.GetValue` against).
- **It calls `ChangeTracker.Entries()` per node** in the reachable graph, an O(tracked entries)
  scan repeated for every node - a no-op cost when nothing conflicts, but still the wrong
  granularity: it treats a symptom that only `Repository<T>` sees (a second `Update()` colliding)
  rather than the actual cause, which is that the `DbContext` backing that repository call should
  never have lived long enough to collide with itself.

**Actual fix: per-operation scopes, not a smarter repository.** `Repository<T>.UpdateAsync`/
`DeleteAsync` are back to their original, unconditional `DbSet.Update(entity)`/`DbSet.Remove(entity)`
bodies - identical to what they were before this bug was investigated. Instead, every mutation
handler on an interactive Blazor page (and the reload that follows it) resolves its service
through a **fresh `IServiceScopeFactory` scope** instead of the page's injected, circuit-scoped
instance, via `Blazor/Common/ScopedOperations.cs`:

```csharp
await ScopeFactory.RunAsync<IScheduledMessageService>(s => s.UpdateAsync(id, dto));
```

Each call gets its own scope, and therefore its own `BotDbContext` with an empty change tracker,
disposed the moment the call returns - so there is nothing left over for a later call in the same
circuit to collide with, ever, regardless of how many navigations `Include`, how many services are
involved, or whether an entity has a shadow key. This is the pattern
docs/plans/blazor-port-plan.md §4.1 already called out as "the old branch's proven pattern" before
this bug was ever hit; see "Per-operation scopes" in `docs/architecture/patterns.md` § Blazor
Components for the rule and which pages follow it.

**Verified fixed**, not just worked around:
`tests/DiscordBot.Tests/Services/ScheduledMessageRepeatedUpdateTrackingTests.cs` updates the same
entity twice, and separately four times, then deletes it - each operation against its *own*
`BotDbContext` on the same shared file-backed SQLite database (`TestDbContextFactory.CreateSharedDatabase()`),
mirroring exactly what `ScopedOperations.RunAsync` produces for a real page - and none of it
throws. `tests/DiscordBot.E2E/BrowserTests.cs`'s `Test_W_ScheduledMessages_CreateListEditDelete_RoundTrip`
does not reload the page between the Resume toggle and Delete - it exercises update → update →
delete straight off the same circuit the edit and both toggles just used, and passes without any
reload workaround.

**Follow-up, deliberately not addressed here.** `Repository<T>.UpdateAsync`'s `DbSet.Update(entity)`
marks every `Include`d navigation as `Modified` too, not just the entity itself - so saving a
`ScheduledMessage` (which `ScheduledMessageRepository.GetByIdAsync` fetches with `.Include(s =>
s.Guild)`) also rewrites the `Guilds` row it came with, even though nothing about the guild
changed. Per-operation scopes don't make this better or worse; it is pre-existing behaviour,
tracked as a follow-up for a separate PR, not fixed as part of this one.
