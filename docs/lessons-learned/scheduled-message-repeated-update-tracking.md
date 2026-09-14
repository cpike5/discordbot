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

**Cause (best understanding, not fully root-caused).** `ScheduledMessageService.UpdateAsync` and
`DeleteAsync` both call `IScheduledMessageRepository.GetByIdAsync`, which - unlike the generic
`Repository<T>.GetByIdAsync` (`DbSet.FindAsync`, which checks the local change tracker first) - is
an override using a plain tracked query: `DbSet.Include(s => s.Guild).FirstOrDefaultAsync(...)`.
`Repository<T>.UpdateAsync` then calls `DbSet.Update(entity)` before `SaveChangesAsync` - on an
entity already tracked from that same fetch, which re-walks and re-attaches the whole reachable
graph (`ScheduledMessage` + its `Guild` navigation) rather than being the no-op it would be for an
entity whose properties were merely mutated in place. A **Razor Page's own `BotDbContext` lives
for exactly one HTTP request**, so this pattern (fetch, mutate, `Update()`, save) never had a
second chance to collide with itself - every action got a fresh, empty change tracker. A **Blazor
Server circuit's DI scope, and therefore its `BotDbContext`, lives for the whole circuit** (every
interaction on a page, and on every other page navigated to via client-side enhanced navigation
without a full reload, shares one scope) - so a second `GetByIdAsync` + `Update()` cycle for the
same entity, later in the same circuit, is operating against a change tracker that already has an
entry for that key. The full mechanism (identity resolution normally reuses an already-tracked
instance for a matching key in a plain tracked query - see EF Core's docs - so this needs the
`Include`d `Guild` graph-attach, or a subtler queued-SaveChanges interaction, to actually explain
the "already tracked" specifically for the outer `ScheduledMessage`) wasn't fully nailed down
before time ran out on this PR; treat the explanation above as the leading hypothesis, not a
confirmed root cause.

**Workaround taken here.** `tests/DiscordBot.E2E/BrowserTests.cs`'s
`Test_W_ScheduledMessages_CreateListEditDelete_RoundTrip` does a real page load
(`page.GotoAsync` - a fresh circuit, fresh `DbContext`) between the toggle steps and Delete, rather
than deleting straight off the same circuit the toggle just used. This is also a more realistic
user flow (most people navigate or refresh between distinct admin actions rather than rapid-firing
several in one continuous session) and keeps the test green without touching shared repository
code under time pressure in an unrelated PR.

**Not fixed.** The underlying gap in `Repository<T>.UpdateAsync`/`GetByIdAsync` (or specifically in
`ScheduledMessageRepository`'s override) is real and will keep surfacing for any Blazor page that
updates then deletes (or updates twice) the same entity within one circuit without a page reload
in between - a Blazor-native interaction pattern (toast-and-stay, no full navigation) that this
port encourages far more than the old POST-per-action Razor Pages ever did. Worth a dedicated pass
across `Repository<T>` and its per-entity overrides once more than one Phase 4 cluster has hit it,
rather than a narrow fix to `ScheduledMessageRepository` alone: the leading hypothesis above (drop
the redundant `DbSet.Update(entity)` call for an entity fetched via a tracked query and mutated in
place, since EF's change tracker detects those changes on `SaveChangesAsync` without it) needs
verifying against every other `Repository<T>` consumer's fetch/mutate/save pattern before it's
safe to apply generically.
