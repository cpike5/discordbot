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

**Cause (confirmed, with a correction to the original hypothesis).** An earlier version of this
note guessed `ScheduledMessageRepository.GetByIdAsync` used a *tracked* query and that identity
resolution alone should have deduplicated repeat fetches. That was wrong on both counts, and was
never checked against a real repro before time ran out on the PR that introduced it. The actual
mechanism, confirmed by a real SQLite-in-memory repro
(`tests/DiscordBot.Tests/Services/ScheduledMessageRepeatedUpdateTrackingTests.cs`) going through
the real `ScheduledMessageRepository`/`ScheduledMessageService`, not mocks:

`ScheduledMessageRepository.GetByIdAsync` is `DbSet.AsNoTracking().Include(s => s.Guild)
.FirstOrDefaultAsync(...)` - genuinely untracked, and always was. `Repository<T>.UpdateAsync` then
called `DbSet.Update(entity)` unconditionally. `DbSet.Update()` walks the entity's *whole reachable
graph* (the `Include`d `Guild`, not just the `ScheduledMessage` root) and attaches every node it
finds as tracked (`Modified`, since both have non-default keys) - and, critically, **does not
detach any of it after `SaveChangesAsync`**; every node stays tracked (`Unchanged`) for the rest of
the `DbContext`'s lifetime. A **Razor Page's own `BotDbContext` lives for exactly one HTTP
request**, so this never had a second chance to collide with itself - every action got a fresh,
empty change tracker. A **Blazor Server circuit's DI scope, and therefore its `BotDbContext`,
lives for the whole circuit** - so a *second* `GetByIdAsync` (a brand new, still-untracked
`ScheduledMessage` + a brand new, still-untracked `Guild` instance - `AsNoTracking` never returns
the same instance twice) followed by `DbSet.Update()` tries to attach two different instances for
keys the first `Update()` call left tracked, and EF's identity map throws "already tracked"
immediately - confirmed to happen on the **second `UpdateAsync` call itself**, not deferred to a
later `DeleteAsync` the way the first repro that motivated this note appeared to show (that
Playwright repro likely had a full page load between more of its steps than the "workaround" below
assumed; the root cause was never in doubt, just which call it surfaced on first).

**Fix.** `Repository<T>.UpdateAsync`/`DeleteAsync` (`src/DiscordBot.Infrastructure/Data/Repositories/Repository.cs`)
now reconcile instead of blindly attaching: before calling `DbSet.Update(entity)` (skipped entirely
when `entity` is already tracked - e.g. fetched via a tracked query and mutated in place, where
`SaveChangesAsync` picks up the change on its own) or `DbSet.Remove(entity)`, they walk `entity`'s
reachable graph (its own key, then every loaded navigation, recursively - the same graph
`DbSet.Update()`/`Remove()` themselves walk) and detach any already-tracked entry for a *different*
instance sharing a node's key. This is broader than the originally proposed "skip the redundant
`Update()` call when already tracked" - that alone doesn't help here, since the *second* fetch is a
genuinely new, untracked instance every time; the graph walk is what a fresh instance needs to
reconcile against a stale one left behind by an earlier call in the same long-lived `DbContext`.
Applied generically to `Repository<T>`, not narrowly to `ScheduledMessageRepository`, since the
same `AsNoTracking` + `Include` + `Update()` shape is common across repositories and the fix is a
no-op (one extra in-memory tracker scan) when nothing actually conflicts - the existing full
`dotnet test tests/DiscordBot.Tests` run (all repository/service suites, not just scheduled
messages) is green against it.

**Verified fixed**, not just worked around: `ScheduledMessageRepeatedUpdateTrackingTests` updates
the same entity twice (and, separately, four times) then deletes it against one `BotDbContext`,
matching a Blazor circuit's lifetime, and none of it throws.
`tests/DiscordBot.E2E/BrowserTests.cs`'s `Test_W_ScheduledMessages_CreateListEditDelete_RoundTrip`
no longer reloads the page between the Resume toggle and Delete - it deletes straight off the same
circuit the edit and both toggles just used.
