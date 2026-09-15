# EditForm's FormName routes a premature submit into a broken static fallback

**Symptom.** `Admin/Users/Create.razor`'s `<EditForm>` (`@rendermode InteractiveServer`,
`OnValidSubmit`, `DataAnnotationsValidator`) worked in every bUnit test but consistently failed
end to end under Playwright: clicking "Create User" right after filling the form left the browser
on the same page with no navigation, no toast, and no server-side log line at all from
`HandleValidSubmit` - as if the click had no effect. A retry-click loop made it *worse*, sometimes
surfacing a real server error ("A valid antiforgery token was not provided with the request." or
"The POST request does not specify which form is being submitted.").

**Cause.** A page rendered with `@rendermode InteractiveServer` is still **prerendered as static
HTML first**; the Interactive Server circuit attaches afterward, and that attach is not
instantaneous - Blazor Web App documents no reliable client-side signal for "the circuit has
attached to this DOM region" (the same gap `tests/DiscordBot.E2E/BrowserTests.cs`'s
`AssertCounterIncrementsAsync` already works around for a plain `@onclick` button). Setting
`FormName` on an `EditForm` opts that form into Blazor Web App's **named static form-post
fallback** - the mechanism that lets a form submit correctly even during that narrow prerender
window, via a real (non-circuit) HTTP POST handled statically. A submit that lands in that window
is routed into the fallback whether or not the page actually implements it. Neither
`Create.razor`/`Edit.razor` implemented it (no `<AntiforgeryToken />`, no
`[SupplyParameterFromForm]` on the bound model), so a submit that hit the window failed instead of
reaching `OnValidSubmit` - the antiforgery/form-name errors above are that fallback failing, not
the interactive path. **bUnit never exercises any of this**: it invokes `OnValidSubmit` directly
against the component instance and never touches the real `<form>`/JS/HTTP path a browser does, so
this class of bug is invisible to it entirely - it only surfaces against a real host.

**What a premature submit actually sends (verified, not assumed).** An earlier version of this
note claimed dropping `FormName` makes a pre-circuit submit "a harmless no-op" because, without
`method="post"`, the browser would fall back to a **GET** that puts the password fields in the
URL/history/logs. That specific claim is wrong, and was never checked against the real markup.
Booting the host web-only (`Discord__Enabled=false`, matching `BotHostFixture`) and curling the
authenticated, prerendered `/Admin/Users/Create` page shows `EditForm` always renders a real
`method="post"` form regardless of `FormName`, plus (independently of `FormName`) its own
antiforgery hidden input:

```html
<!-- WITHOUT FormName (this repo's actual code) -->
<form method="post" class="p-6 space-y-6" action="/Admin/Users/Create">
  <input type="hidden" name="__RequestVerificationToken" value="..." />
  ...
  <input type="password" id="Input_Password" name="Input_Password" ... />

<!-- WITH FormName="CreateUserRepro" (reproduced only for this comparison, not shipped) -->
<form method="post" class="p-6 space-y-6" action="/Admin/Users/Create">
  <input type="hidden" name="__RequestVerificationToken" value="..." />
  <input type="hidden" name="_handler" value="CreateUserRepro" />
  ...
```

The only difference `FormName` makes to the emitted markup is the extra hidden `_handler` field
that tells the server which named static handler to route the POST to. So a pre-circuit submit is
a real `POST` either way - the password travels in the request body, not the URL, and never reaches
browser history or a URL-keyed log. **Without `FormName`/`_handler` the server has nothing to route
the POST to** (no `[SupplyParameterFromForm]` model is listening for it), so the request is
processed like any other request to that route: the component boots fresh and prerenders again,
discarding the submitted values. That is the real, verified shape of "harmless no-op" - a wasted
POST and a reset form, not a data leak and not the antiforgery/`_handler` errors from the Symptom
section (those came specifically from `FormName` routing the request into a fallback path that was
never wired to succeed).

**Fix.** Drop `FormName` from an `EditForm` that has no legitimate no-JS/static-fallback use case
(this admin console has none) - `Create.razor`/`Edit.razor` already do this. `method="post"` needs
no separate fix; `EditForm` emits it unconditionally, `FormName` or not, as shown above. The
remaining gap is UX/robustness, not correctness or security: a user who submits in the prerender
window gets a silently reset form instead of an error, which is confusing even though nothing is
lost or leaked. Closed by disabling the submit button (and, on `AuditLogs/Details.razor`, the
Export JSON button) until `ComponentBase.RendererInfo.IsInteractive` is `true` - that flips from
`false` (prerendered/static) to `true` once the interactive circuit has actually attached and
re-rendered the component, so it is a real readiness signal available in `.razor` markup with no
extra state (`RendererInfo` is inherited from `ComponentBase`; confirmed present and working in
this repo's SDK, .NET 10). A short "Connecting…" hint sits next to the disabled button. The
alternative - actually implementing the static fallback (`<AntiforgeryToken />` +
`[SupplyParameterFromForm]` on the model, `[CascadingParameter] HttpContext` to distinguish a real
postback from a fresh load so a page like `Edit.razor` that also reloads its model from the
database on init doesn't clobber form-supplied values) - is the framework-documented alternative
and may be worth it for a page with real anonymous/no-JS traffic, but is meaningfully more code for
no benefit here.

**Rule that falls out of it.** For any `EditForm` on an `@rendermode InteractiveServer` page in
this codebase:

1. No `FormName` unless the static-fallback path is deliberately implemented end to end
   (antiforgery token, `[SupplyParameterFromForm]`, and - if the page has other
   `OnInitializedAsync` work - a way to tell a real postback apart from a fresh load). The
   `anthropic-skills` "blazor" skill's own `03-forms-validation.md` documents `FormName` as
   "**Required** unique name for SSR form handling" and lists `[SupplyParameterFromForm]` as one
   of the "SSR Form Requirements" - both true, but only for a form meant to work that way; an
   interactive-only form (the norm in this admin console) is simpler and more robust without
   either.
2. `method="post"` needs no action - `EditForm` always emits it. (Do not add an explicit
   `method="post"` attribute of your own to work around a "defaults to GET" theory; it doesn't,
   and a hand-added one would just duplicate what the framework already renders.)
3. Disable the submit button (`disabled="@(!RendererInfo.IsInteractive)"`) with a brief
   "Connecting…" hint, so a click in the prerender window is prevented rather than silently
   discarded. Apply the same pattern to any other first-interaction control on a freshly rendered
   interactive page (a plain `@onclick` button has the identical race - see `AuditLogs/Details.razor`'s
   Export JSON button).

This rule does **not** apply to `Profile.razor`: that page is static SSR by design (no
`@rendermode`), so its `<form>` is a real, always-working static POST with no circuit-attach window
to race. A static SSR page has its own, different `FormName` requirements — see the next section.

## A static SSR `EditForm` needs `FormName`, and two more requirements bUnit can't see (cluster 4c)

**Symptom.** `Blazor/Pages/Account/LinkDiscord.razor` and `Privacy.razor` (both static SSR, no
`@rendermode` — Phase 4 cluster 4c) shipped with one `<EditForm FormName="...">` per action (five
forms on `LinkDiscord`, four on `Privacy`), all green in bUnit. Curling a real running instance
reproduced a genuine .NET static SSR framework failure instead: a `400` ("Cannot submit the form
'x' because no form on the page currently has that name") or, further along, a `500` inside
`EndpointHtmlRenderer`'s `ProcessNamedSubmitEventAdditions`/`FindFormMappingContext` — matching the
publicly reported `dotnet/aspnetcore` issues #55808, #55893, #54854. Every unlink/refresh/verify/
consent/export/delete flow on both pages had been reporting green in bUnit while completely broken
end to end; the real Playwright run caught it (`Test_Z3` red; `Test_Z4` only accidentally green
because it happened to exercise only the validation-failure path, never a real submit).

**Cause — first requirement.** Unlike the interactive-race case above, a static SSR page has no
circuit to post back through at all: the browser's own `POST` is the *only* mechanism, so
`FormName` is not optional here — it's how ASP.NET Core's form-value binder
(`[SupplyParameterFromForm]`) tells one page's form apart from another's on the same route (this is
why `Login.razor`'s `FormName="login"` and `Profile.razor`'s `FormName="profile-theme"` both carry
one). But a named form's static mapping **never registers unless the render also contains at least
one real `InputBase`-derived bound field** (`InputText`, etc.). A form built only from plain
`<button name=/value=>` pairs — no bound input anywhere in it — is never recognized as "a form on
this page" at all; that's the 400/500 above. Both pages now carry a hidden marker `InputText`
wherever no other bound field is already present in that render path (`ActionForm.FormMarker` /
`ActionForm.ConsentMarker`, `@bind-Value`, `type="hidden"`).

**Cause — second requirement.** A posted field only binds through `[SupplyParameterFromForm]` when
its `name` carries the *exact* `"{ComponentPropertyName}.{ModelPropertyName}"` prefix Blazor's own
bound inputs emit — a bare `name="UnlinkAction"` is silently dropped, never populated, never an
error. It has to be `name="ActionForm.UnlinkAction"` (`ActionForm` being the `[SupplyParameterFromForm]`
model property the `<EditForm Model="ActionForm">` binds).

**Cause — third requirement (a design rule, not a framework quirk).** One named `EditForm` per
distinct **implicit-submit target**, not one per action and not necessarily one per page. HTML's
implicit submission (pressing Enter while focused in a text field) fires the *first* submit button
in whichever `<form>` that field belongs to — so any action sharing a form with an unrelated text
input can be triggered by accident. `LinkDiscord` collapses to exactly one form
(`link-discord-actions`, appearing in at most one of the page's two mutually exclusive branches —
not-linked or linked — so it's still only ever one form per render). `Privacy` needs two:
`privacy-actions` (every consent Grant/Revoke plus "Export My Data" — none of those branches has a
free-text field, so sharing is safe) and a separate `privacy-delete` (the typed-`DELETE`
confirmation box) — reproducing the legacy page's actual behaviour, since Enter in that box must
never land on a consent button instead.

**Why bUnit missed all three.** bUnit invokes a component's `OnSubmit`/`OnValidSubmit` handler
directly against the live component instance; it never renders the real `<form>` markup through the
static HTML pipeline, submits it, or has ASP.NET Core map the POST back to a
`[SupplyParameterFromForm]` model. None of the three requirements above is on that path at all —
only a real host, hit with a real HTTP POST (curl, or Playwright driving a real browser), exercises
static named-form mapping. This is the same gap the interactive-race section above describes for a
different reason (bUnit skips the DOM/circuit-attach race); here it skips the static-form-mapping
HTTP path entirely. `LinkDiscordTests`/`PrivacyTests` now drive the single `OnSubmit` handler via
reflection (setting the relevant `ActionForm.*Action`/`DeleteForm` field directly, the same pattern
`Admin/Users/EditTests.cs` already used for its own protected handlers) since bUnit has no way to
simulate *which* submit button posted.

**Rule that falls out of it, for any static SSR (no `@rendermode`) `EditForm` in this codebase:**

1. `FormName` is required — the opposite of the interactive rule above.
2. The render must contain at least one real `InputBase`-derived bound field, or the named
   mapping silently never registers. Add a hidden `InputText` marker if nothing else in that
   render path is already bound.
3. Every posted field name must carry its component property's exact
   `"{ComponentPropertyName}.{ModelPropertyName}"` prefix — never a bare model property name.
4. One `EditForm` per implicit-submit target: group actions into the same form only when no
   free-text input in it could have Enter fire a different action's button by accident; give a
   field like a typed confirmation its own form.
5. Verify against a real running host (curl or Playwright), not bUnit — bUnit cannot see any of
   requirements 1-4 failing.

Confirmed by `AccountEndpointExtensionsTests.MapAccountEndpoints_PostEndpointsRequireAntiforgery_GetEndpointsDoNot`
(the antiforgery half) and by manually curling every `LinkDiscord`/`Privacy` action (unlink,
refresh, initiate/verify/cancel verification, consent toggle, export, delete-with-wrong-confirmation)
against a real running host after the fix. See "Static-SSR account pages" in
`docs/architecture/patterns.md` for where the resulting page/service/endpoint structure lives.

**Test-side consequence.** An E2E test driving an `EditForm` submit (or any other first
interaction on a route Playwright just navigated to) now has a real DOM signal to wait on instead
of a blind sleep: `Expect(button).ToBeEnabledAsync()` on the submit/action button, which only
becomes enabled once `RendererInfo.IsInteractive` flips. `Test_R_Users_CreateEditDetails_RoundTrip`
and `Test_T_AuditLogDetails_ExportDownloadsJson` in `tests/DiscordBot.E2E/BrowserTests.cs` use this
instead of the earlier `page.WaitForTimeoutAsync(1_500)` per freshly-navigated page.
